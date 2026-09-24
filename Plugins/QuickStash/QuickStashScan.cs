// <copyright file="QuickStashScan.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace QuickStash
{
    using System;
    using System.Collections.Generic;
    using System.Reflection;
    using GameHelper;
    using GameHelper.Plugin;
    using GameHelper.RemoteEnums;
    using GameOffsets.Natives;
    using GameOffsets.Objects.States.InGameState;
    using GameOffsets.Objects.UiElement;

    /// <summary>
    ///     One Ctrl+click target. <see cref="SlotX"/>/<see cref="SlotY"/> is the item origin cell.
    ///     Screen coordinates are the center of that item's visible inventory widget.
    /// </summary>
    internal readonly record struct DumpClick(int SlotX, int SlotY, int ScreenX, int ScreenY);

    /// <summary>
    ///     Reads the player backpack and matches those items to widgets on the right panel.
    /// </summary>
    internal static class QuickStashScan
    {
        // Same slot-item pointer LootValue already reads on inventory/stash widgets.
        private const int UiElementItemAddressOffset = 0x4E0;
        private const int MaxChildrenPerNode = 512;
        private const int MaxVisited = 4000;

        internal static bool TryCollect(
            bool[]? keepSlots,
            out List<DumpClick> clicks,
            out string blockReason)
        {
            clicks = new List<DumpClick>();
            blockReason = string.Empty;
            if (!GameMemory.TryBind())
            {
                blockReason = "memory";
                return false;
            }

            var serverData = Core.States.InGameStateObject.CurrentAreaInstance.ServerDataObject.Address;
            if (serverData == IntPtr.Zero)
            {
                blockReason = "nogame";
                return false;
            }

            if (!GameMemory.TryRead(serverData, out ServerDataOffsets server))
            {
                blockReason = "memory";
                return false;
            }

            var players = GameMemory.ReadVector<IntPtr>(server.PlayerServerDataPtr);
            if (players.Length == 0 || players[0] == IntPtr.Zero)
            {
                blockReason = "memory";
                return false;
            }

            if (!GameMemory.TryRead(players[0], out ServerDataStructure player))
            {
                blockReason = "memory";
                return false;
            }

            var inventories = GameMemory.ReadVector<InventoryArrayStruct>(player.PlayerInventories);
            if (CursorHoldsItem(inventories))
            {
                blockReason = "cursor";
                return false;
            }

            var occupied = ReadMainInventory(inventories);
            if (occupied.Count == 0)
            {
                blockReason = "empty";
                return false;
            }

            var wanted = new Dictionary<IntPtr, (int X, int Y)>(occupied.Count);
            foreach (var (item, cell) in occupied)
            {
                if (ShouldDump(keepSlots, cell.X, cell.Y))
                {
                    wanted[item] = cell;
                }
            }

            if (wanted.Count == 0)
            {
                blockReason = "kept";
                return false;
            }

            var panel = Core.States.InGameStateObject.GameUi.RightPanel;
            if (panel.Address == IntPtr.Zero)
            {
                blockReason = "panels";
                return false;
            }

            var window = Core.Process.WindowArea;
            if (window.Width <= 0 || window.Height <= 0)
            {
                blockReason = "window";
                return false;
            }

            MatchRightPanel(panel.Address, panel.Position, panel.Size, wanted, window.X, window.Y, clicks);
            clicks.Sort(static (a, b) => a.SlotY != b.SlotY ? a.SlotY.CompareTo(b.SlotY) : a.SlotX.CompareTo(b.SlotX));
            if (clicks.Count == 0)
            {
                blockReason = "nomatch";
                return false;
            }

            return true;
        }

        private static bool ShouldDump(bool[]? keepSlots, int x, int y)
        {
            if ((uint)x >= QuickStashSettings.Cols || (uint)y >= QuickStashSettings.Rows)
            {
                return false;
            }

            if (keepSlots == null || keepSlots.Length < QuickStashSettings.SlotCount)
            {
                return true;
            }

            return !keepSlots[(y * QuickStashSettings.Cols) + x];
        }

        private static bool CursorHoldsItem(InventoryArrayStruct[] inventories)
        {
            foreach (var inv in inventories)
            {
                if (inv.InventoryId != (int)InventoryName.Cursor1 || inv.InventoryPtr0 == IntPtr.Zero)
                {
                    continue;
                }

                if (!GameMemory.TryRead(inv.InventoryPtr0, out InventoryStruct info))
                {
                    continue;
                }

                foreach (var slotPtr in GameMemory.ReadVector<IntPtr>(info.ItemList))
                {
                    if (slotPtr == IntPtr.Zero)
                    {
                        continue;
                    }

                    if (GameMemory.TryRead(slotPtr, out InventoryItemStruct slot) && slot.Item != IntPtr.Zero)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static Dictionary<IntPtr, (int X, int Y)> ReadMainInventory(InventoryArrayStruct[] inventories)
        {
            var occupied = new Dictionary<IntPtr, (int X, int Y)>();
            foreach (var inv in inventories)
            {
                if (inv.InventoryId != (int)InventoryName.MainInventory1 || inv.InventoryPtr0 == IntPtr.Zero)
                {
                    continue;
                }

                if (!GameMemory.TryRead(inv.InventoryPtr0, out InventoryStruct info))
                {
                    break;
                }

                foreach (var slotPtr in GameMemory.ReadVector<IntPtr>(info.ItemList))
                {
                    if (slotPtr == IntPtr.Zero)
                    {
                        continue;
                    }

                    if (!GameMemory.TryRead(slotPtr, out InventoryItemStruct slot) || slot.Item == IntPtr.Zero)
                    {
                        continue;
                    }

                    var x = slot.SlotStart.X;
                    var y = slot.SlotStart.Y;
                    if ((uint)x >= QuickStashSettings.Cols || (uint)y >= QuickStashSettings.Rows)
                    {
                        continue;
                    }

                    if (!occupied.TryGetValue(slot.Item, out var existing) ||
                        y < existing.Y || (y == existing.Y && x < existing.X))
                    {
                        occupied[slot.Item] = (x, y);
                    }
                }

                break;
            }

            return occupied;
        }

        private static void MatchRightPanel(
            IntPtr panelAddress,
            System.Numerics.Vector2 panelPosition,
            System.Numerics.Vector2 panelSize,
            Dictionary<IntPtr, (int X, int Y)> wanted,
            int windowX,
            int windowY,
            List<DumpClick> clicks)
        {
            var matched = new HashSet<IntPtr>();
            var visited = new HashSet<IntPtr>();
            var queue = new Queue<IntPtr>();
            queue.Enqueue(panelAddress);
            var maxX = panelPosition.X + panelSize.X;
            var maxY = panelPosition.Y + panelSize.Y;

            while (queue.Count > 0 && visited.Count < MaxVisited && matched.Count < wanted.Count)
            {
                var element = queue.Dequeue();
                if (element == IntPtr.Zero || !visited.Add(element))
                {
                    continue;
                }

                if (!GameMemory.TryRead(element, out UiElementBaseOffset offset))
                {
                    continue;
                }

                if (!UiElementBaseFuncs.IsVisibleChecker(offset.Flags))
                {
                    continue;
                }

                var childCount = offset.ChildrensPtr.TotalElements(IntPtr.Size);
                if (childCount is > 0 and <= MaxChildrenPerNode)
                {
                    foreach (var child in GameMemory.ReadVector<IntPtr>(offset.ChildrensPtr))
                    {
                        queue.Enqueue(child);
                    }
                }

                if (!GameMemory.TryRead(element + UiElementItemAddressOffset, out IntPtr item) ||
                    item == IntPtr.Zero ||
                    !wanted.TryGetValue(item, out var cell) ||
                    !matched.Add(item))
                {
                    continue;
                }

                if (!TryClickCenter(element, offset.ParentPtr, out var centerX, out var centerY))
                {
                    matched.Remove(item);
                    continue;
                }

                if (centerX < panelPosition.X || centerX > maxX || centerY < panelPosition.Y || centerY > maxY)
                {
                    matched.Remove(item);
                    continue;
                }

                clicks.Add(new DumpClick(
                    cell.X,
                    cell.Y,
                    windowX + (int)MathF.Round(centerX),
                    windowY + (int)MathF.Round(centerY)));
            }
        }

        private static bool TryClickCenter(IntPtr element, IntPtr parent, out float centerX, out float centerY)
        {
            centerX = 0;
            centerY = 0;
            if (!PluginUiElementReflection.TryGetAbsoluteRect(element, out var position, out var size))
            {
                return false;
            }

            if (parent != IntPtr.Zero &&
                (size.X < 16f || size.Y < 16f) &&
                PluginUiElementReflection.TryGetAbsoluteRect(parent, out var parentPosition, out var parentSize) &&
                parentSize.X >= 20f && parentSize.Y >= 20f &&
                parentSize.X <= 256f && parentSize.Y <= 256f)
            {
                position = parentPosition;
                size = parentSize;
            }

            if (size.X < 8f || size.Y < 8f)
            {
                return false;
            }

            centerX = position.X + (size.X * 0.5f);
            centerY = position.Y + (size.Y * 0.5f);
            return float.IsFinite(centerX) && float.IsFinite(centerY);
        }

        private static class GameMemory
        {
            private static object? handle;
            private static MethodInfo? tryReadOpen;
            private static MethodInfo? readVectorOpen;
            private static readonly Dictionary<Type, MethodInfo> TryReadCache = new();
            private static readonly Dictionary<Type, MethodInfo> VectorCache = new();

            internal static bool TryBind()
            {
                if (handle != null && tryReadOpen != null && readVectorOpen != null)
                {
                    return true;
                }

                var prop = typeof(GameProcess).GetProperty(
                    "Handle",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                handle = prop?.GetValue(Core.Process);
                if (handle == null)
                {
                    return false;
                }

                MethodInfo? tryRead = null;
                MethodInfo? readVector = null;
                foreach (var method in handle.GetType().GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
                {
                    if (!method.IsGenericMethod)
                    {
                        continue;
                    }

                    var parameters = method.GetParameters();
                    if (method.Name == "TryReadMemory" && parameters.Length == 2)
                    {
                        tryRead = method;
                    }
                    else if (method.Name == "ReadStdVector" && parameters.Length == 1)
                    {
                        readVector = method;
                    }
                }

                if (tryRead == null || readVector == null)
                {
                    handle = null;
                    return false;
                }

                tryReadOpen = tryRead;
                readVectorOpen = readVector;
                return true;
            }

            internal static bool TryRead<T>(IntPtr address, out T value)
                where T : unmanaged
            {
                value = default;
                if (tryReadOpen == null || handle == null)
                {
                    return false;
                }

                if (!TryReadCache.TryGetValue(typeof(T), out var method))
                {
                    method = tryReadOpen.MakeGenericMethod(typeof(T));
                    TryReadCache[typeof(T)] = method;
                }

                var args = new object?[] { address, null };
                try
                {
                    if (method.Invoke(handle, args) is not true)
                    {
                        return false;
                    }
                }
                catch (TargetInvocationException)
                {
                    return false;
                }

                if (args[1] is not T read)
                {
                    return false;
                }

                value = read;
                return true;
            }

            internal static T[] ReadVector<T>(StdVector vector)
                where T : unmanaged
            {
                if (readVectorOpen == null || handle == null)
                {
                    return Array.Empty<T>();
                }

                var count = vector.TotalElements(System.Runtime.InteropServices.Marshal.SizeOf<T>());
                if (count <= 0 || count > 4096)
                {
                    return Array.Empty<T>();
                }

                if (!VectorCache.TryGetValue(typeof(T), out var method))
                {
                    method = readVectorOpen.MakeGenericMethod(typeof(T));
                    VectorCache[typeof(T)] = method;
                }

                try
                {
                    return method.Invoke(handle, new object[] { vector }) as T[] ?? Array.Empty<T>();
                }
                catch (TargetInvocationException)
                {
                    return Array.Empty<T>();
                }
            }
        }
    }
}
