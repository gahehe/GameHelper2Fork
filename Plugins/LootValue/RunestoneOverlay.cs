// <copyright file="RunestoneOverlay.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace LootValue
{
    using System.Collections.Concurrent;
    using System.Numerics;
    using GameHelper;
    using GameHelper.RemoteObjects.Components;
    using GameHelper.RemoteObjects.States.InGameStateObjects;
    using GameOffsets.Objects.UiElement;
    using ImGuiNET;

    /// <summary>
    ///     Prices Expedition2 runestones from the live station plus <see cref="RunestoneCatalog"/>,
    ///     then draws a map label and a top-right list. The sleeping-entity scan is throttled and
    ///     off the UI thread; pricing and drawing use the published snapshot.
    /// </summary>
    internal sealed class RunestoneOverlay
    {
        private const int SleepScanMs = 2000;
        private const double CameraAngle = 38.7 * Math.PI / 180;
        private const float LargeMapScaleBaseline = 0.187812f;
        private const float MiniMapZoomBaseline = 0.748f;

        private readonly object gate = new();
        private readonly RunestoneCatalog catalog = RunestoneCatalog.Load();
        private List<Sight> sleeping = new();
        private string sleepingArea = string.Empty;
        private Task? sleepingTask;
        private long nextSleepScan;
        private DateTime nextAwakeUtc = DateTime.MinValue;
        private string builtArea = string.Empty;
        private RunestoneMarker[] markers = Array.Empty<RunestoneMarker>();

        public void Draw(LootValueSettings settings)
        {
            if (!settings.ShowRunestoneLabels)
            {
                return;
            }

            if (!this.catalog.IsLoaded)
            {
                return;
            }

            var area = Core.States.InGameStateObject.CurrentAreaInstance;
            if (area.AreaHash != this.builtArea)
            {
                this.builtArea = area.AreaHash;
                this.markers = Array.Empty<RunestoneMarker>();
                lock (this.gate)
                {
                    this.sleeping = new List<Sight>();
                    this.sleepingArea = string.Empty;
                }
            }

            this.ScheduleSleepScan(area);
            if (DateTime.UtcNow >= this.nextAwakeUtc)
            {
                this.nextAwakeUtc = DateTime.UtcNow.AddMilliseconds(Math.Max(16, settings.RescanIntervalMs));
                this.Rebuild(area, settings);
            }

            if (this.markers.Length == 0)
            {
                return;
            }

            this.DrawMapLabels(area, settings);
        }

        private void ScheduleSleepScan(AreaInstance area)
        {
            var now = Environment.TickCount64;
            if (now < this.nextSleepScan)
            {
                return;
            }

            if (this.sleepingTask is { IsCompleted: false })
            {
                return;
            }

            this.nextSleepScan = now + SleepScanMs;
            var hash = area.AreaHash;
            this.sleepingTask = Task.Run(() =>
            {
                var found = new ConcurrentBag<Sight>();
                try
                {
                    area.ScanSleepingEntities(
                        path => path.Contains("Expedition2Encounter", StringComparison.Ordinal),
                        (_, entity) =>
                        {
                            if (TryReadSight(entity, out var sight))
                            {
                                found.Add(sight);
                            }
                        });
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[LootValue] Runestone scan failed: {ex.Message}");
                    return;
                }

                if (Core.States.InGameStateObject.CurrentAreaInstance.AreaHash != hash)
                {
                    return;
                }

                lock (this.gate)
                {
                    this.sleeping = found.ToList();
                    this.sleepingArea = hash;
                }
            });
        }

        private void Rebuild(AreaInstance area, LootValueSettings settings)
        {
            List<Sight> sleep;
            lock (this.gate)
            {
                sleep = this.sleepingArea == area.AreaHash ? this.sleeping : new List<Sight>();
            }

            var byId = new Dictionary<uint, Sight>(sleep.Count + 8);
            foreach (var sight in sleep)
            {
                byId[sight.Id] = sight;
            }

            foreach (var entity in area.AwakeEntities.Values)
            {
                if (entity.Path.IndexOf("Expedition2Encounter", StringComparison.Ordinal) < 0)
                {
                    continue;
                }

                if (TryReadSight(entity, out var sight))
                {
                    byId[sight.Id] = sight;
                }
            }

            var list = new List<RunestoneMarker>(byId.Count);
            foreach (var sight in byId.Values)
            {
                if (settings.HideCollectedRunestones && sight.Collected)
                {
                    continue;
                }

                if (!this.TryPrice(sight, area.CurrentAreaLevel, settings, out var marker))
                {
                    continue;
                }

                list.Add(marker);
            }

            list.Sort((a, b) => b.BestEx.CompareTo(a.BestEx));
            this.markers = list.ToArray();
        }

        private bool TryPrice(in Sight sight, int areaLevel, LootValueSettings settings, out RunestoneMarker marker)
        {
            marker = null!;
            var offers = this.catalog.Offers(sight.AnchorIndex, sight.AnchorPos, sight.Holes, sight.IsUnique, areaLevel);
            double best = 0;
            var bestName = string.Empty;
            var rewards = new List<RunestoneReward>(3);
            foreach (var offer in offers)
            {
                var ex = PriceEx(offer.Name, offer.Count);
                if (ex <= 0)
                {
                    continue;
                }

                if (ex > best)
                {
                    best = ex;
                    bestName = offer.Name;
                }

                rewards.Add(new RunestoneReward(offer.Name, ex));
            }

            if (settings.RunestoneMinValueEx > 0 && best < settings.RunestoneMinValueEx)
            {
                return false;
            }

            rewards.Sort((a, b) => b.Ex.CompareTo(a.Ex));
            if (rewards.Count > 3)
            {
                rewards.RemoveRange(3, rewards.Count - 3);
            }

            var anchor = sight.IsUnique
                ? "Unique"
                : sight.AnchorIndex >= 0 ? this.catalog.RuneName(sight.AnchorIndex) : "?";
            marker = new RunestoneMarker(sight.X, sight.Y, sight.Height, sight.Holes, anchor, best, bestName, rewards);
            return true;
        }

        private static double PriceEx(string name, int count)
        {
            var price = PoeNinjaPriceFetcher.GetPrice(name);
            if (price == null)
            {
                return 0;
            }

            var scaled = new PoeNinjaPrice { PriceChaos = price.PriceChaos * Math.Max(1, count) };
            return PoeNinjaPriceFetcher.GetDisplayPrice(scaled, 1).Value;
        }

        private static bool TryReadSight(Entity entity, out Sight sight)
        {
            sight = default;
            if (!entity.TryGetComponent<Render>(out var render))
            {
                return false;
            }

            if (!entity.TryGetComponent<StateMachine>(out var machine) || !machine.TryReadRuneStation(out var station))
            {
                return false;
            }

            var collected = entity.TryGetComponent<MinimapIcon>(out var icon) && icon.IsEncounterComplete();
            sight = new Sight(
                entity.Id,
                render.GridPosition.X,
                render.GridPosition.Y,
                render.TerrainHeight,
                station.HoleCount,
                station.AnchorIndex,
                station.AnchorPosition,
                station.IsUnique,
                collected);
            return true;
        }

        private void DrawMapLabels(AreaInstance area, LootValueSettings settings)
        {
            if (!area.Player.TryGetComponent<Render>(out var player))
            {
                return;
            }

            var playerGrid = new Vector2(player.GridPosition.X, player.GridPosition.Y);
            var ui = Core.States.InGameStateObject.GameUi;
            if (ui.IsAnyLargePanelOpen)
            {
                return;
            }

            var fg = ImGui.GetBackgroundDrawList();
            var font = ImGui.GetFont();
            var large = ui.LargeMap;
            if (large.IsVisible && !ui.WorldMapPanel.IsVisible)
            {
                var center = large.Center + large.Shift + large.DefaultShift + new Vector2(0.6f, 0.3f);
                this.DrawLabelsOnMap(fg, font, settings, center, Diagonal(large.Size.Y), large.Zoom * LargeMapScaleBaseline, playerGrid, player.TerrainHeight, null);
            }

            var mini = ui.MiniMap;
            if (mini.IsVisible)
            {
                var center = mini.Position + (mini.Size / 2f) + mini.DefaultShift + mini.Shift + new Vector2(-5f, 0f);
                var clip = (mini.Position, mini.Position + mini.Size);
                this.DrawLabelsOnMap(fg, font, settings, center, Diagonal(mini.Size.Y), mini.Zoom * MiniMapZoomBaseline, playerGrid, player.TerrainHeight, clip);
            }
        }

        private void DrawLabelsOnMap(
            ImDrawListPtr fg,
            ImFontPtr font,
            LootValueSettings settings,
            Vector2 center,
            double diagonal,
            float zoom,
            Vector2 playerGrid,
            float playerHeight,
            (Vector2 Min, Vector2 Max)? clip)
        {
            if (zoom <= 0 || diagonal <= 0)
            {
                return;
            }

            var mapScale = 240f / zoom;
            var cos = (float)(diagonal * Math.Cos(CameraAngle) / mapScale);
            var sin = (float)(diagonal * Math.Sin(CameraAngle) / mapScale);
            var threshold = settings.RunestoneHighlightMinEx;
            foreach (var marker in this.markers)
            {
                var delta = new Vector2(marker.X, marker.Y) - playerGrid;
                var dz = (marker.Height - playerHeight) / 10.86957f;
                var screen = center + new Vector2((delta.X - delta.Y) * cos, (dz - (delta.X + delta.Y)) * sin);
                if (clip is { } box && (screen.X < box.Min.X || screen.Y < box.Min.Y || screen.X > box.Max.X || screen.Y > box.Max.Y))
                {
                    continue;
                }

                var text = marker.BestEx > 0 ? $"{marker.BestEx:0}ex · {marker.BestName}" : $"{marker.Anchor} {marker.Holes}h";
                var color = TierColor(marker.BestEx, threshold);
                var fontSize = ImGui.GetFontSize() * (marker.BestEx >= threshold && threshold > 0 ? 1.35f : 1.15f);
                var pos = new Vector2(screen.X + 22f, screen.Y - 8f);
                var packed = ImGui.ColorConvertFloat4ToU32(color);
                fg.AddText(font, fontSize, pos + new Vector2(1f, 1f), 0xCC000000u, text);
                fg.AddText(font, fontSize, pos, packed, text);
            }
        }

        private static Vector4 TierColor(double bestEx, float threshold)
        {
            if (bestEx <= 0 || threshold <= 0)
            {
                return Vector4.One;
            }

            if (bestEx >= threshold)
            {
                return new Vector4(0.4f, 0.88f, 0.4f, 1f);
            }

            if (bestEx >= 0.6 * threshold)
            {
                return new Vector4(0.90f, 0.78f, 0.30f, 1f);
            }

            return Vector4.One;
        }

        private static double Diagonal(float sizeY)
        {
            var baseRes = UiElementBaseFuncs.BaseResolution;
            var baseDiag = Math.Sqrt((baseRes.X * baseRes.X) + (baseRes.Y * baseRes.Y));
            return baseDiag * sizeY / baseRes.Y;
        }

        private readonly record struct Sight(
            uint Id, float X, float Y, float Height, int Holes, int AnchorIndex, int AnchorPos, bool IsUnique, bool Collected);

        private sealed class RunestoneMarker
        {
            public RunestoneMarker(float x, float y, float height, int holes, string anchor, double bestEx, string bestName, List<RunestoneReward> rewards)
            {
                this.X = x;
                this.Y = y;
                this.Height = height;
                this.Holes = holes;
                this.Anchor = anchor;
                this.BestEx = bestEx;
                this.BestName = bestName;
                this.Rewards = rewards;
            }

            public float X { get; }

            public float Y { get; }

            public float Height { get; }

            public int Holes { get; }

            public string Anchor { get; }

            public double BestEx { get; }

            public string BestName { get; }

            public List<RunestoneReward> Rewards { get; }
        }

        private readonly record struct RunestoneReward(string Name, double Ex);
    }
}
