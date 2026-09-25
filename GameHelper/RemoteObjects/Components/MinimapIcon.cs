// <copyright file="MinimapIcon.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace GameHelper.RemoteObjects.Components
{
    using System;
    using GameOffsets.Objects.Components;
    using ImGuiNET;

    /// <summary>
    ///     The <see cref="MinimapIcon" /> component in the entity.
    /// </summary>
    public class MinimapIcon : ComponentBase
    {
        /// <summary>
        ///     Initializes a new instance of the <see cref="MinimapIcon" /> class.
        /// </summary>
        /// <param name="address">address of the <see cref="MinimapIcon" /> component.</param>
        public MinimapIcon(IntPtr address)
            : base(address) { }

        /// <summary>
        ///     The icon name from MinimapIcons.dat (e.g. "RewardChestExpedition").
        /// </summary>
        public string? IconName { get; private set; }

        /// <summary>
        ///     Live read of the int immediately after the component header. Non-zero means the
        ///     encounter icon is faded because the reward was claimed. Read on demand: the flag
        ///     flips while the component address stays put.
        /// </summary>
        public bool IsEncounterComplete()
        {
            if (this.Address == IntPtr.Zero)
            {
                return false;
            }

            return Core.Process.Handle.ReadMemory<int>(this.Address + 0x10) != 0;
        }

        /// <inheritdoc/>
        internal override void ToImGui()
        {
            base.ToImGui();
            ImGui.Text($"Icon Name: {this.IconName ?? "(none)"}");
        }

        private string? TryReadUtf16String(IntPtr address)
        {
            try
            {
                var reader = Core.Process.Handle;
                var bytes = reader.ReadMemoryArray<byte>(address, 512);
                for (var i = 0; i < bytes.Length - 1; i += 2)
                {
                    if (bytes[i] == 0 && bytes[i + 1] == 0)
                    {
                        if (i == 0) return null;
                        return System.Text.Encoding.Unicode.GetString(bytes, 0, i);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MinimapIcon.TryReadUtf16String] {address.ToInt64():X}: {ex.Message}");
            }

            return null;
        }

        /// <inheritdoc/>
        protected override void UpdateData(bool hasAddressChanged)
        {
            var reader = Core.Process.Handle;
            var header = reader.ReadMemory<ComponentHeader>(this.Address);
            this.OwnerEntityAddress = header.EntityPtr;

            // Read icon name: offset 0x20 -> dat row pointer -> +0x00 -> UTF-16 string
            try
            {
                var ptr20 = reader.ReadMemory<IntPtr>(this.Address + 0x20);
                if (ptr20 != IntPtr.Zero && (long)ptr20 > 0x10000)
                {
                    var namePtr = reader.ReadMemory<IntPtr>(ptr20);
                    if (namePtr != IntPtr.Zero && (long)namePtr > 0x10000)
                    {
                        this.IconName = this.TryReadUtf16String(namePtr);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[MinimapIcon.UpdateData] {this.Address.ToInt64():X}: {ex.Message}");
            }
        }
    }
}
