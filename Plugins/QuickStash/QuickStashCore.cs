// <copyright file="QuickStashCore.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace QuickStash
{
    using System;
    using System.Collections.Generic;
    using System.IO;
    using System.Numerics;
    using System.Threading;
    using ClickableTransparentOverlay.Win32;
    using GameHelper;
    using GameHelper.Plugin;
    using GameHelper.RemoteEnums;
    using GameHelper.Utils;
    using ImGuiNET;
    using Newtonsoft.Json;

    /// <summary>
    ///     Ctrl+clicks backpack items that are not marked keep into the open left panel
    ///     (stash, NPC shop, or another transfer window).
    /// </summary>
    public sealed class QuickStashCore : PCore<QuickStashSettings>
    {
        private readonly object statusLock = new();
        private int busy;
        private string status = string.Empty;

        private string SettingPathname => Path.Join(this.DllDirectory, "config", "settings.txt");

        /// <inheritdoc />
        public override void OnEnable(bool isGameOpened)
        {
            if (File.Exists(this.SettingPathname))
            {
                try
                {
                    var content = File.ReadAllText(this.SettingPathname);
                    this.Settings = JsonConvert.DeserializeObject<QuickStashSettings>(content) ?? new QuickStashSettings();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[QuickStash] Failed to load settings: {ex.Message}");
                    this.Settings = new QuickStashSettings();
                }
            }

            this.NormalizeSettings();
        }

        /// <inheritdoc />
        public override void OnDisable()
        {
        }

        /// <inheritdoc />
        public override void SaveSettings()
        {
            Directory.CreateDirectory(Path.GetDirectoryName(this.SettingPathname) ?? string.Empty);
            var data = JsonConvert.SerializeObject(this.Settings, Formatting.Indented);
            File.WriteAllText(this.SettingPathname, data);
        }

        /// <inheritdoc />
        public override void DrawUI()
        {
            if (!Utils.IsKeyPressedAndNotTimeout(this.Settings.DumpKey, 400))
            {
                return;
            }

            this.TryStartDump();
        }

        /// <inheritdoc />
        public override void DrawSettings()
        {
            ImGui.TextWrapped(this.PluginText.T(
                "settings.hint",
                "Green cells are kept. Press the key while inventory and a left panel (stash or NPC shop) are open. Each other occupied cell is Ctrl+clicked. Do not use this while the character sheet is the left panel."));

            ImGui.Text(this.PluginText.T("settings.dump_key", "Move key"));
            ImGui.SameLine();
            ImGuiHelper.NonContinuousEnumComboBox("##QSDumpKey", ref this.Settings.DumpKey);

            var min = this.Settings.MinDelayMs;
            var max = this.Settings.MaxDelayMs;
            if (ImGui.SliderInt(
                this.PluginText.Label("settings.min_delay", "Min delay (ms)", "QSMinDelay"),
                ref min, 0, 500))
            {
                this.Settings.MinDelayMs = min;
                if (this.Settings.MaxDelayMs < min)
                {
                    this.Settings.MaxDelayMs = min;
                }
            }

            if (ImGui.SliderInt(
                this.PluginText.Label("settings.max_delay", "Max delay (ms)", "QSMaxDelay"),
                ref max, 0, 500))
            {
                this.Settings.MaxDelayMs = max;
                if (this.Settings.MinDelayMs > max)
                {
                    this.Settings.MinDelayMs = max;
                }
            }

            ImGuiHelper.ToolTip(this.PluginText.T(
                "settings.delay.tooltip",
                "After each click finishes, the cursor stays on that item for a new random time in this range, then moves."));

            if (ImGui.Button(this.PluginText.T("settings.keep_none", "Keep none")))
            {
                Array.Clear(this.Settings.KeepSlots);
            }

            ImGui.SameLine();
            if (ImGui.Button(this.PluginText.T("settings.keep_all", "Keep all")))
            {
                Array.Fill(this.Settings.KeepSlots, true);
            }

            this.DrawGrid();

            string current;
            lock (this.statusLock)
            {
                current = this.status;
            }

            if (current.Length > 0)
            {
                ImGui.TextWrapped(current);
            }
        }

        /// <inheritdoc />
        public override string GetDescription() =>
            this.PluginText.T(
                "plugin.description",
                "Ctrl+clicks backpack items that are not marked keep into the open left panel.");

        private void DrawGrid()
        {
            var keep = this.Settings.KeepSlots;
            var green = new Vector4(0.18f, 0.62f, 0.28f, 1f);
            var greenHot = new Vector4(0.25f, 0.75f, 0.36f, 1f);
            var dim = new Vector4(0.18f, 0.18f, 0.18f, 1f);
            var dimHot = new Vector4(0.30f, 0.30f, 0.30f, 1f);
            for (var y = 0; y < QuickStashSettings.Rows; y++)
            {
                for (var x = 0; x < QuickStashSettings.Cols; x++)
                {
                    if (x > 0)
                    {
                        ImGui.SameLine(0f, 3f);
                    }

                    var index = (y * QuickStashSettings.Cols) + x;
                    var on = keep[index];
                    ImGui.PushStyleColor(ImGuiCol.Button, on ? green : dim);
                    ImGui.PushStyleColor(ImGuiCol.ButtonHovered, on ? greenHot : dimHot);
                    ImGui.PushStyleColor(ImGuiCol.ButtonActive, on ? greenHot : dimHot);
                    if (ImGui.Button($"##qs{index}", new Vector2(22f, 22f)))
                    {
                        keep[index] = !on;
                    }

                    ImGui.PopStyleColor(3);
                }
            }
        }

        private void TryStartDump()
        {
            if (Interlocked.CompareExchange(ref this.busy, 1, 0) != 0)
            {
                this.SetStatus(this.PluginText.T("status.busy", "Already moving items."));
                return;
            }

            List<DumpClick>? clicks = null;
            NativeInput.POINT origin = default;
            var minDelay = 0;
            var maxDelay = 0;
            try
            {
                if (!Core.Process.Foreground)
                {
                    this.SetStatus(this.PluginText.T("status.unfocused", "PoE2 is not focused."));
                    return;
                }

                if (Core.States.GameCurrentState != GameStateTypes.InGameState)
                {
                    this.SetStatus(this.PluginText.T("status.not_in_game", "Not in game."));
                    return;
                }

                var ui = Core.States.InGameStateObject.GameUi;
                if (!ui.RightPanel.IsVisible || !ui.LeftPanel.IsVisible)
                {
                    this.SetStatus(this.PluginText.T(
                        "status.panels",
                        "Open inventory and a left panel (stash or NPC shop)."));
                    return;
                }

                if (!QuickStashScan.TryCollect(this.Settings.KeepSlots, out var found, out var reason))
                {
                    this.SetStatus(this.ReasonText(reason));
                    return;
                }

                if (!NativeInput.TryGetCursor(out origin))
                {
                    this.SetStatus(this.PluginText.T("status.cursor", "Could not read the cursor."));
                    return;
                }

                clicks = found;
                minDelay = Math.Clamp(this.Settings.MinDelayMs, 0, 2000);
                maxDelay = Math.Clamp(this.Settings.MaxDelayMs, 0, 2000);
                if (maxDelay < minDelay)
                {
                    (minDelay, maxDelay) = (maxDelay, minDelay);
                }

                this.SetStatus(this.PluginText.F("status.moving", "Moving {0} item(s).", clicks.Count));
            }
            finally
            {
                if (clicks == null)
                {
                    Interlocked.Exchange(ref this.busy, 0);
                }
            }

            if (clicks == null)
            {
                return;
            }

            var captured = clicks;
            var originCap = origin;
            ThreadPool.QueueUserWorkItem(_ => this.RunDump(originCap, captured, minDelay, maxDelay));
        }

        private void RunDump(NativeInput.POINT origin, List<DumpClick> clicks, int minDelay, int maxDelay)
        {
            var finished = true;
            try
            {
                for (var i = 0; i < clicks.Count; i++)
                {
                    if (!Core.Process.Foreground)
                    {
                        finished = false;
                        this.SetStatus(this.PluginText.T("status.focus_lost", "Stopped because PoE2 lost focus."));
                        break;
                    }

                    var click = clicks[i];
                    var delay = minDelay == maxDelay
                        ? minDelay
                        : Random.Shared.Next(minDelay, maxDelay + 1);
                    if (!NativeInput.CtrlLeftClickAt(click.ScreenX, click.ScreenY, delay))
                    {
                        continue;
                    }
                }

                NativeInput.MoveCursor(origin.X, origin.Y);
                if (finished)
                {
                    this.SetStatus(this.PluginText.F("status.done", "Finished {0} click(s).", clicks.Count));
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[QuickStash] {ex.Message}");
                this.SetStatus(this.PluginText.T("status.failed", "Move failed."));
            }
            finally
            {
                Interlocked.Exchange(ref this.busy, 0);
            }
        }

        private string ReasonText(string reason) => reason switch
        {
            "cursor" => this.PluginText.T("status.cursor_item", "Cursor is holding an item."),
            "empty" => this.PluginText.T("status.empty", "Backpack has no items."),
            "kept" => this.PluginText.T("status.kept", "Every occupied cell is marked keep."),
            "nomatch" => this.PluginText.T(
                "status.no_match",
                "No backpack item widgets were found on the right panel."),
            "panels" => this.PluginText.T(
                "status.panels",
                "Open inventory and a left panel (stash or NPC shop)."),
            "nogame" => this.PluginText.T("status.not_in_game", "Not in game."),
            "window" => this.PluginText.T("status.window", "Game window size is not ready."),
            _ => this.PluginText.T("status.memory", "Could not read inventory."),
        };

        private void SetStatus(string text)
        {
            lock (this.statusLock)
            {
                this.status = text;
            }

            if (text.Length > 0)
            {
                Console.WriteLine($"[QuickStash] {text}");
            }
        }

        private void NormalizeSettings()
        {
            var slots = this.Settings.KeepSlots;
            if (slots == null || slots.Length != QuickStashSettings.SlotCount)
            {
                var next = new bool[QuickStashSettings.SlotCount];
                if (slots != null)
                {
                    Array.Copy(slots, next, Math.Min(slots.Length, next.Length));
                }

                this.Settings.KeepSlots = next;
            }

            if (this.Settings.DumpKey == 0)
            {
                this.Settings.DumpKey = VK.F4;
            }

            this.Settings.MinDelayMs = Math.Clamp(this.Settings.MinDelayMs, 0, 2000);
            this.Settings.MaxDelayMs = Math.Clamp(this.Settings.MaxDelayMs, 0, 2000);
        }
    }
}
