// <copyright file="SekhemaMarkers.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace SekhemaHelper
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Numerics;
    using GameHelper;
    using GameHelper.RemoteObjects.Components;
    using GameHelper.RemoteObjects.States;
    using GameHelper.RemoteObjects.States.InGameStateObjects;
    using GameHelper.RemoteObjects.UiElement;
    using GameHelper.Utils;
    using GameOffsets.Natives;
    using ImGuiNET;

    internal static class SekhemaMarkers
    {
        private const float WorldPerGrid = 250f / 23f;
        private const float RingCells = 12f;
        private static string logged = string.Empty;

        public static void Draw(InGameState state, SekhemaHelperSettings settings, string areaId, string dllDirectory, bool mapOpen)
        {
            var area = state.CurrentAreaInstance;
            var world = state.CurrentWorldInstance;
            var lines = new List<string>();
            var marks = new List<Mark>(32);
            var doors = new List<Vector2>(4);
            var draw = ImGui.GetBackgroundDrawList();
            foreach (var entity in area.AwakeEntities.Values)
            {
                if (!entity.IsValid || entity.Path.Length == 0)
                {
                    continue;
                }

                var path = entity.Path;
                if (IsCompletionDoor(path) && entity.TryGetComponent<Render>(out var doorRender) && !Used(entity, out _))
                {
                    var dg = doorRender.GridPosition;
                    doors.Add(new Vector2(dg.X, dg.Y));
                    lines.Add($"Door id={entity.Id} grid={dg.X:0},{dg.Y:0} {path}");
                }

                var kind = Classify(path);
                if (kind == MarkerKind.None)
                {
                    continue;
                }

                if (!entity.TryGetComponent<Render>(out var render))
                {
                    continue;
                }

                var opened = entity.TryGetComponent<Chest>(out var chest) && chest.IsOpened;
                var used = Used(entity, out var states);
                var g = render.GridPosition;
                lines.Add($"{kind} id={entity.Id} grid={g.X:0},{g.Y:0} opened={opened} used={used} {path} states={states}");

                if (opened || used)
                {
                    continue;
                }

                var chestId = kind == MarkerKind.Chest ? ChestId(path) : string.Empty;
                var color = ColorFor(kind, settings);
                if (kind == MarkerKind.Chest)
                {
                    var row = settings.ChestTypes.Find(c => c.Id == chestId);
                    if (row is { Show: false })
                    {
                        continue;
                    }

                    if (row != null)
                    {
                        color = ImGuiHelper.Color(row.Color);
                    }
                }

                if (color == 0)
                {
                    continue;
                }

                var mark = new Mark(kind, color, new Vector2(g.X, g.Y), render.TerrainHeight, render.WorldPosition, chestId);
                marks.Add(mark);
            }

            if (area.Player.TryGetComponent<Render>(out var playerRender))
            {
                var playerGrid = new Vector2(playerRender.GridPosition.X, playerRender.GridPosition.Y);
                var room = ClosestRoomMechanic(marks, playerGrid);
                marks.RemoveAll(m => IsRoomMechanic(m.Kind) && m.Kind != room);
                if (room == MarkerKind.Crystal)
                {
                    MergeSleepingCrystals(area, settings, marks);
                }
            }
            else
            {
                marks.RemoveAll(m => IsRoomMechanic(m.Kind));
            }

            if (!mapOpen)
            {
                foreach (var mark in marks)
                {
                    DrawRing(draw, world, mark.World, mark.Height, mark.Color);
                }
            }

            MergeSleepingDoors(area, doors, lines);
            DrawMaps(state, settings, marks, doors);

            var sig = areaId + string.Join("|", lines);
            if (sig == logged || lines.Count == 0)
            {
                return;
            }

            logged = sig;
            try
            {
                var logPath = Path.Join(dllDirectory, "config", "markers-log.txt");
                Directory.CreateDirectory(Path.GetDirectoryName(logPath) ?? string.Empty);
                File.WriteAllText(logPath, string.Join(Environment.NewLine, lines));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Sekhema] Failed to write marker log: {ex.Message}");
            }
        }

        private static uint ColorFor(MarkerKind kind, SekhemaHelperSettings settings) => kind switch
        {
            MarkerKind.Crystal when settings.ShowCrystals => ImGuiHelper.Color(settings.CrystalColor),
            MarkerKind.Lever when settings.ShowLevers => ImGuiHelper.Color(settings.LeverColor),
            MarkerKind.Portal when settings.ShowPortals => ImGuiHelper.Color(settings.PortalColor),
            MarkerKind.Chest when settings.ShowChests => ImGuiHelper.Color(new Vector4(1f, 0.85f, 0.3f, 1f)),
            MarkerKind.Hourglass => ImGuiHelper.Color(new Vector4(0.91f, 0.64f, 0.09f, 1f)),
            MarkerKind.Chalice => ImGuiHelper.Color(new Vector4(0.49f, 0.78f, 1f, 1f)),
            _ => 0,
        };

        private static MarkerKind Classify(string path)
        {
            if (path.Contains("HourglassLethal", StringComparison.Ordinal))
            {
                return MarkerKind.Crystal;
            }

            if (path.Contains("Hourglass", StringComparison.Ordinal))
            {
                return MarkerKind.Hourglass;
            }

            if (path.Contains("Chalice", StringComparison.Ordinal))
            {
                return MarkerKind.Chalice;
            }

            if (path.Contains("SanctumGenericLever", StringComparison.Ordinal))
            {
                return MarkerKind.Lever;
            }

            if (path.Contains("PortalPlatform", StringComparison.Ordinal))
            {
                return MarkerKind.Portal;
            }

            if (path.Contains("MarakethSanctum", StringComparison.Ordinal) && path.Contains("Chest", StringComparison.Ordinal))
            {
                return MarkerKind.Chest;
            }

            return MarkerKind.None;
        }

        private static bool Used(GameHelper.RemoteObjects.States.InGameStateObjects.Entity entity, out string states)
        {
            states = string.Empty;
            // Entity.UpdateData stops refreshing cached components once the crystal is marked
            // useless, so the cached StateMachine stays at deactivated=0 after you touch it.
            if (!entity.TryGetComponent<StateMachine>(out var machine, false))
            {
                return false;
            }

            var parts = new List<string>(machine.States.Count);
            var used = false;
            foreach (var state in machine.States)
            {
                parts.Add(state.Name + "=" + state.Value);
                if (state.Value != 0 && state.Name is "deactivated" or "destroyed" or "lever_pull")
                {
                    used = true;
                }
            }

            states = string.Join(",", parts);
            return used;
        }

        private static void DrawRing(ImDrawListPtr draw, WorldData world, StdTuple3D<float> center, float height, uint color)
        {
            const int seg = 24;
            var radius = RingCells * WorldPerGrid;
            Vector2? prev = null;
            for (var i = 0; i <= seg; i++)
            {
                var a = i * MathF.Tau / seg;
                var screen = world.WorldToScreen(
                    new StdTuple3D<float>
                    {
                        X = center.X + (radius * MathF.Cos(a)),
                        Y = center.Y + (radius * MathF.Sin(a)),
                        Z = height,
                    },
                    height);
                if (screen == Vector2.Zero)
                {
                    prev = null;
                    continue;
                }

                if (prev is { } p && Vector2.Distance(p, screen) < 800f)
                {
                    draw.AddLine(p, screen, color, 2.6f);
                }

                prev = screen;
            }
        }

        private static long nextDoorScan;
        private static readonly List<Vector2> sleepingDoors = [];

        private static void MergeSleepingDoors(AreaInstance area, List<Vector2> doors, List<string> lines)
        {
            var now = Environment.TickCount64;
            if (now >= nextDoorScan)
            {
                nextDoorScan = now + 2000;
                var found = new System.Collections.Concurrent.ConcurrentBag<(Vector2 Grid, string Line)>();
                area.ScanSleepingEntities(
                    IsCompletionDoor,
                    (_, entity) =>
                    {
                        if (!entity.TryGetComponent<Render>(out var render))
                        {
                            return;
                        }

                        var g = render.GridPosition;
                        var grid = new Vector2(g.X, g.Y);
                        found.Add((grid, $"Door id={entity.Id} grid={g.X:0},{g.Y:0} {entity.Path}"));
                    });
                sleepingDoors.Clear();
                foreach (var item in found)
                {
                    sleepingDoors.Add(item.Grid);
                    lines.Add(item.Line);
                }
            }

            foreach (var grid in sleepingDoors)
            {
                doors.Add(grid);
            }
        }

        private static long nextSleepScan;
        private static readonly List<Mark> sleepingCrystals = [];

        private static void MergeSleepingCrystals(AreaInstance area, SekhemaHelperSettings settings, List<Mark> marks)
        {
            var now = Environment.TickCount64;
            if (now >= nextSleepScan)
            {
                nextSleepScan = now + 2000;
                var found = new System.Collections.Concurrent.ConcurrentBag<Mark>();
                var color = ImGuiHelper.Color(settings.CrystalColor);
                area.ScanSleepingEntities(
                    path => path.Contains("HourglassLethal", StringComparison.Ordinal),
                    (_, entity) =>
                    {
                        if (!entity.TryGetComponent<Render>(out var render) || Used(entity, out var _))
                        {
                            return;
                        }

                        var g = render.GridPosition;
                        found.Add(new Mark(MarkerKind.Crystal, color, new Vector2(g.X, g.Y), render.TerrainHeight, render.WorldPosition, string.Empty));
                    });
                sleepingCrystals.Clear();
                sleepingCrystals.AddRange(found);
            }

            foreach (var extra in sleepingCrystals)
            {
                var duplicate = false;
                foreach (var mark in marks)
                {
                    if (mark.Kind == MarkerKind.Crystal && Vector2.DistanceSquared(mark.Grid, extra.Grid) < 16f)
                    {
                        duplicate = true;
                        break;
                    }
                }

                if (!duplicate)
                {
                    marks.Add(extra);
                }
            }
        }

        private static string entryArea = string.Empty;
        private static Vector2 entryGrid;

        private static void DrawMaps(InGameState state, SekhemaHelperSettings settings, List<Mark> marks, List<Vector2> doors)
        {
            if (!state.CurrentAreaInstance.Player.TryGetComponent<Render>(out var playerRender))
            {
                return;
            }

            var player = new Vector2(playerRender.GridPosition.X, playerRender.GridPosition.Y);
            var areaId = state.CurrentWorldInstance.AreaDetails.Id;
            if (entryArea != areaId)
            {
                entryArea = areaId;
                entryGrid = player;
            }

            var crystals = new List<Vector2>();
            var seals = new List<Vector2>(doors);
            Vector2? lever = null;
            var leverD = float.MaxValue;
            foreach (var mark in marks)
            {
                if (mark.Kind == MarkerKind.Crystal)
                {
                    crystals.Add(mark.Grid);
                }
                else if (mark.Kind == MarkerKind.Lever)
                {
                    var d = Vector2.DistanceSquared(player, mark.Grid);
                    if (d < leverD)
                    {
                        leverD = d;
                        lever = mark.Grid;
                    }
                }
            }

            crystals = SekhemaRoute.InRoom(state.CurrentAreaInstance.GridWalkableData, state.CurrentAreaInstance.TerrainMetadata.BytesPerRow, player, seals, crystals);
            marks.RemoveAll(m => m.Kind == MarkerKind.Crystal && !crystals.Contains(m.Grid));

            Vector2? door = null;
            var doorD = -1f;
            foreach (var d in doors)
            {
                var dist = Vector2.DistanceSquared(entryGrid, d);
                if (dist > doorD)
                {
                    doorD = dist;
                    door = d;
                }
            }

            var key = areaId + ":" + string.Join(";", crystals.ConvertAll(c => $"{(int)c.X},{(int)c.Y}")) + ":" + (door?.X ?? 0);
            var walk = state.CurrentAreaInstance.GridWalkableData;
            var bytes = state.CurrentAreaInstance.TerrainMetadata.BytesPerRow;
            SekhemaRoute.Update(key, walk, bytes, player, crystals, settings.ShowCrystals ? door : null, settings.ShowLevers ? lever : null);

            var ui = state.GameUi;
            if (ui.LargeMap.IsVisible && !ui.WorldMapPanel.IsVisible)
            {
                DrawOnMap(ui.LargeMap, true, settings, marks, player, playerRender.TerrainHeight);
            }

            if (ui.MiniMap.IsVisible)
            {
                DrawOnMap(ui.MiniMap, false, settings, marks, player, playerRender.TerrainHeight);
            }
        }

        private static void DrawOnMap(MapUiElement map, bool large, SekhemaHelperSettings settings, List<Mark> marks, Vector2 player, float playerHeight)
        {
            var draw = ImGui.GetForegroundDrawList();
            var min = map.Position;
            var max = map.Position + map.Size;
            void Line(IReadOnlyList<Vector2> poly, uint color)
            {
                Vector2? prev = null;
                foreach (var gp in poly)
                {
                    var p = SekhemaRoute.MapPoint(map, large, gp, player, 0f);
                    if (p.X < min.X || p.Y < min.Y || p.X > max.X || p.Y > max.Y)
                    {
                        prev = null;
                        continue;
                    }

                    if (prev is { } a)
                    {
                        draw.AddLine(a, p, color, large ? 2.4f : 1.5f);
                    }

                    prev = p;
                }
            }

            if (settings.ShowCrystals)
            {
                Line(SekhemaRoute.CrystalPath, ImGuiHelper.Color(settings.CrystalColor));
            }

            if (settings.ShowLevers)
            {
                Line(SekhemaRoute.LeverPath, ImGuiHelper.Color(settings.LeverColor));
            }

            var crystalIndex = 0;
            foreach (var mark in marks)
            {
                var p = SekhemaRoute.MapPoint(map, large, mark.Grid, player, mark.Height - playerHeight);
                if (p.X < min.X || p.Y < min.Y || p.X > max.X || p.Y > max.Y)
                {
                    continue;
                }

                var r = large ? settings.PoiRadius : settings.PoiRadius * 0.6f;
                if (mark.Kind == MarkerKind.Chest)
                {
                    r = large ? settings.ChestRadius : settings.ChestRadius * 0.6f;
                }

                draw.AddCircleFilled(p, r, mark.Color);
                if (mark.Kind == MarkerKind.Chest && settings.ShowChestLabels)
                {
                    var row = settings.ChestTypes.Find(c => c.Id == mark.ChestId);
                    if (row is { Highlight: true })
                    {
                        draw.AddCircle(p, r + 2.5f, 0xEBFFFFFF, 0, 2.2f);
                    }

                    draw.AddText(p + new Vector2(-20f, r), mark.Color, mark.ChestId);
                }
                else if (mark.Kind == MarkerKind.Crystal && large)
                {
                    crystalIndex++;
                    draw.AddText(p + new Vector2(-4f, -6f), 0xFFFFFFFF, crystalIndex.ToString());
                }
                else if (large && mark.Kind is MarkerKind.Portal or MarkerKind.Lever)
                {
                    draw.AddText(p + new Vector2(-16f, r), 0xEBFFFFFF, mark.Kind.ToString());
                }
            }
        }

        private static bool IsRoomMechanic(MarkerKind kind) =>
            kind is MarkerKind.Crystal or MarkerKind.Portal or MarkerKind.Lever or MarkerKind.Hourglass or MarkerKind.Chalice;

        private static MarkerKind ClosestRoomMechanic(List<Mark> marks, Vector2 player)
        {
            var best = MarkerKind.None;
            var bestD = float.MaxValue;
            foreach (var mark in marks)
            {
                if (!IsRoomMechanic(mark.Kind))
                {
                    continue;
                }

                var d = Vector2.DistanceSquared(player, mark.Grid);
                if (d < bestD)
                {
                    bestD = d;
                    best = mark.Kind;
                }
            }

            return best;
        }

        private static bool IsCompletionDoor(string path) =>
            path.Contains("SanctumLogicDoor", StringComparison.Ordinal)
            || path.Contains("FloorTransitionDoor", StringComparison.Ordinal)
            || path.Contains("KethDoor", StringComparison.Ordinal)
            || path.Contains("SanctumAirlockBlocker", StringComparison.Ordinal);

        private static string ChestId(string path)
        {
            var slash = path.LastIndexOf('/');
            var seg = slash < 0 ? path : path[(slash + 1)..];
            (string Id, string Needle)[] map =
            [
                ("GrandSpectrum", "Spectrum"), ("RadiusJewels", "Radius"), ("LargeRelic", "LargeRelic"),
                ("Jewels", "Jewel"), ("Currency", "Currency"), ("MediumRelic", "MediumRelic"),
                ("Maps", "Map"), ("Ring", "Ring"), ("Amulet", "Amulet"), ("SmallRelic", "SmallRelic"),
                ("Gold", "Gold"), ("Belt", "Belt"), ("BodyArmour", "Body"), ("Helmet", "Helm"),
                ("Gloves", "Glove"), ("Boots", "Boot"), ("Shield", "Shield"), ("MeleeWeapons", "Melee"),
                ("RangedWeapons", "Ranged"), ("CasterWeapons", "Caster"), ("Burial", "Burial"),
            ];
            foreach (var row in map)
            {
                if (seg.Contains(row.Needle, StringComparison.OrdinalIgnoreCase))
                {
                    return row.Id;
                }
            }

            return "Generic";
        }

        private readonly record struct Mark(MarkerKind Kind, uint Color, Vector2 Grid, float Height, StdTuple3D<float> World, string ChestId);

        private enum MarkerKind
        {
            None,
            Crystal,
            Hourglass,
            Chalice,
            Lever,
            Portal,
            Chest,
        }
    }
}
