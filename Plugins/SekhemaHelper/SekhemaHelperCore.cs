// <copyright file="SekhemaHelperCore.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace SekhemaHelper
{
    using System;
    using System.IO;
    using System.Numerics;
    using GameHelper;
    using GameHelper.Plugin;
    using GameHelper.RemoteObjects.Components;
    using GameHelper.Utils;
    using ImGuiNET;
    using Newtonsoft.Json;

    /// <summary>Trial of the Sekhemas helper. Scores the floor and draws it on the trial map.</summary>
    public sealed class SekhemaHelperCore : PCore<SekhemaHelperSettings>
    {
        private string SettingsPath => Path.Join(this.DllDirectory, "config", "settings.txt");
        private string loggedSignature = string.Empty;
        private string search = string.Empty;
        private SekhemaFloor? drawnFloor;
        private long nextReadMs;

        /// <inheritdoc/>
        public override void OnEnable(bool isGameOpened)
        {
            if (!File.Exists(this.SettingsPath))
            {
                return;
            }

            try
            {
                var loaded = JsonConvert.DeserializeObject<SekhemaHelperSettings>(
                    File.ReadAllText(this.SettingsPath),
                    new JsonSerializerSettings { ObjectCreationHandling = ObjectCreationHandling.Replace });
                this.Settings = loaded ?? new SekhemaHelperSettings();
                DedupeProfiles(this.Settings);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Sekhema] Failed to load settings: {ex.Message}");
                this.Settings = new SekhemaHelperSettings();
            }

            if (this.Settings.Profiles.Count == 0)
            {
                this.Settings.Profiles = SekhemaProfiles.Both();
            }
        }

        /// <inheritdoc/>
        public override void OnDisable()
        {
            SekhemaMemory.Close();
        }

        /// <inheritdoc/>
        public override void SaveSettings()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(this.SettingsPath) ?? string.Empty);
                File.WriteAllText(this.SettingsPath, JsonConvert.SerializeObject(this.Settings, Formatting.Indented));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Sekhema] Failed to save settings: {ex.Message}");
            }
        }

        /// <inheritdoc/>
        public override void DrawSettings()
        {
            var changed = false;
            changed |= ImGui.Checkbox(this.PluginText.Label("settings.enabled", "Enabled", "skEnabled"), ref this.Settings.Enabled);
            changed |= ImGui.Checkbox(this.PluginText.Label("settings.room_labels", "Room labels", "skLabels"), ref this.Settings.DrawMapOverlay);
            changed |= ImGui.Checkbox(this.PluginText.Label("settings.best_path", "Draw best path", "skPath"), ref this.Settings.DrawBestPath);
            changed |= ImGui.SliderFloat(this.PluginText.Label("settings.frame", "Frame thickness", "skFrame"), ref this.Settings.FrameThickness, 1f, 6f);
            changed |= ImGui.ColorEdit4(this.PluginText.Label("settings.path_color", "Best path color", "skColor"), ref this.Settings.BestPathColor);

            if (ImGui.BeginCombo(this.PluginText.Label("settings.profile", "Active profile", "skProf"), this.Settings.ActiveProfileName))
            {
                for (var i = 0; i < this.Settings.Profiles.Count; i++)
                {
                    var p = this.Settings.Profiles[i];
                    if (ImGui.Selectable(p.Name + "##skp" + i, p.Name == this.Settings.ActiveProfileName))
                    {
                        this.Settings.ActiveProfileName = p.Name;
                        changed = true;
                    }
                }

                ImGui.EndCombo();
            }

            if (ImGui.Button(this.PluginText.T("settings.reset", "Reset profile")))
            {
                var fresh = this.Settings.ActiveProfileName == "No-Hit" ? SekhemaProfiles.NoHit() : SekhemaProfiles.Default();
                var i = this.Settings.Profiles.FindIndex(p => p.Name == this.Settings.ActiveProfileName);
                if (i >= 0)
                {
                    this.Settings.Profiles[i] = fresh;
                    changed = true;
                }
            }

            var profile = this.Settings.Active();
            changed |= ImGui.DragFloat(this.PluginText.Label("settings.avoid_merchant", "Avoid Merchant when water is below", "skMerch"), ref profile.AvoidMerchantBelowWater, 1f, 0f, 1000f);
            changed |= ImGui.DragFloat(this.PluginText.Label("settings.avoid_honour", "Avoid Honour when honour percent is above", "skHon"), ref profile.AvoidHonourAbovePct, 1f, 0f, 100f);
            ImGui.InputText(this.PluginText.Label("settings.search", "Search afflictions", "skSearch"), ref this.search, 64);
            if (ImGui.CollapsingHeader("Afflictions"))
            {
                changed |= EditAfflictions(profile.AfflictionWeights, this.search);
            }

            if (ImGui.CollapsingHeader("Room types"))
            {
                changed |= EditWeights(profile.RoomTypeWeights, string.Empty);
            }

            if (ImGui.CollapsingHeader("Rewards"))
            {
                changed |= EditWeights(profile.RewardWeights, string.Empty);
            }

            ImGui.Separator();
            changed |= ImGui.Checkbox(this.PluginText.Label("settings.show_portals", "Portals", "skPortals"), ref this.Settings.ShowPortals);
            ImGui.SameLine();
            changed |= ImGui.ColorEdit4("##skPortalColor", ref this.Settings.PortalColor);
            changed |= ImGui.Checkbox(this.PluginText.Label("settings.show_levers", "Levers", "skLevers"), ref this.Settings.ShowLevers);
            ImGui.SameLine();
            changed |= ImGui.ColorEdit4("##skLeverColor", ref this.Settings.LeverColor);
            changed |= ImGui.Checkbox(this.PluginText.Label("settings.show_crystals", "Crystals", "skCrystals"), ref this.Settings.ShowCrystals);
            ImGui.SameLine();
            changed |= ImGui.ColorEdit4("##skCrystalColor", ref this.Settings.CrystalColor);
            changed |= ImGui.Checkbox(this.PluginText.Label("settings.show_chests", "Chests", "skChests"), ref this.Settings.ShowChests);
            changed |= ImGui.Checkbox(this.PluginText.Label("settings.chest_labels", "Chest labels", "skChestLabels"), ref this.Settings.ShowChestLabels);
            if (ImGui.CollapsingHeader("Chest types"))
            {
                foreach (var chest in this.Settings.ChestTypes)
                {
                    changed |= ImGui.Checkbox(chest.Id, ref chest.Show);
                    ImGui.SameLine();
                    var c = chest.Color;
                    if (ImGui.ColorEdit4("##skc" + chest.Id, ref c))
                    {
                        chest.Color = c;
                        changed = true;
                    }

                    ImGui.SameLine();
                    changed |= ImGui.Checkbox("Ring##" + chest.Id, ref chest.Highlight);
                }
            }

            if (changed)
            {
                this.SaveSettings();
            }
        }

        /// <inheritdoc/>
        public override void DrawUI()
        {
            if (!this.Settings.Enabled)
            {
                return;
            }

            var state = Core.States.InGameStateObject;
            var areaId = state.CurrentWorldInstance.AreaDetails.Id;
            if (areaId.Length == 0 || !areaId.StartsWith("Sanctum", StringComparison.Ordinal))
            {
                this.loggedSignature = string.Empty;
                this.drawnFloor = null;
                return;
            }

            var panel = state.GameUi.SekhemasTrialMapPanel;
            var mapOpen = panel.Address != IntPtr.Zero && panel.IsVisible;
            SekhemaMarkers.Draw(state, this.Settings, areaId, this.DllDirectory, mapOpen);
            if (!mapOpen)
            {
                return;
            }

            var now = Environment.TickCount64;
            if (this.drawnFloor == null || now >= this.nextReadMs)
            {
                this.nextReadMs = now + 250;
                SekhemaMemory.Bind(Core.Process.Pid);
                var player = state.CurrentAreaInstance.Player;
                var defenses = new Defenses();
                if (player.TryGetComponent<Stats>(out var stats))
                {
                    defenses = SekhemaFloorRead.ReadDefenses(stats.StatsChangedByBuffAndActions, stats.StatsChangedByItems);
                }

                var floor = SekhemaFloorRead.TryRead(panel.Address.ToInt64(), defenses, this.Settings.Active());
                if (floor == null)
                {
                    return;
                }

                this.drawnFloor = floor;
                var sig = areaId + SekhemaFloorRead.Signature(floor);
                if (sig != this.loggedSignature)
                {
                    this.loggedSignature = sig;
                    var text = areaId + Environment.NewLine + SekhemaFloorRead.FormatLog(floor);
                    try
                    {
                        var logPath = Path.Join(this.DllDirectory, "config", "floor-log.txt");
                        Directory.CreateDirectory(Path.GetDirectoryName(logPath) ?? string.Empty);
                        File.WriteAllText(logPath, text);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Sekhema] Failed to write floor log: {ex.Message}");
                    }
                }
            }

            this.DrawFloor(panel, this.drawnFloor);
        }

        private void DrawFloor(GameHelper.RemoteObjects.UiElement.UiElementBase panel, SekhemaFloor floor)
        {
            var columns = panel[0]?[1];
            if (columns == null || columns.TotalChildrens != floor.Layers.Count)
            {
                return;
            }

            var draw = ImGui.GetForegroundDrawList();
            var pathColor = ImGuiHelper.Color(this.Settings.BestPathColor);
            foreach (var layer in floor.Layers)
            {
                var column = columns[layer[0].Layer];
                if (column == null)
                {
                    continue;
                }

                foreach (var room in layer)
                {
                    var widget = column[room.Index];
                    if (widget == null)
                    {
                        continue;
                    }

                    var pos = widget.Position;
                    var size = widget.Size;
                    var end = pos + size;
                    if (this.Settings.DrawBestPath && room.OnBestPath)
                    {
                        draw.AddRect(pos, end, pathColor, 0f, ImDrawFlags.None, this.Settings.FrameThickness);
                    }

                    if (room.Risk != SekhemaRisk.None)
                    {
                        var c = pos + new Vector2(9f, 9f);
                        draw.AddCircleFilled(c, 5.5f, RiskColor(room.Risk));
                        draw.AddCircle(c, 5.5f, 0xAA000000, 0, 1.5f);
                    }

                    if (!this.Settings.DrawMapOverlay)
                    {
                        continue;
                    }

                    var label = room.RoomType.Length > 0 ? room.RoomType : room.Reward;
                    if (label.Length == 0)
                    {
                        continue;
                    }

                    var textSize = ImGui.CalcTextSize(label);
                    draw.AddText(new Vector2(pos.X + ((size.X - textSize.X) * 0.5f), end.Y - textSize.Y - 2f), 0xEBFFFFFF, label);
                }
            }
        }

        private static uint RiskColor(SekhemaRisk risk) => risk switch
        {
            SekhemaRisk.Minor => ImGuiHelper.Color(170, 210, 110, 255),
            SekhemaRisk.Moderate => ImGuiHelper.Color(235, 180, 70, 255),
            SekhemaRisk.Severe => ImGuiHelper.Color(225, 85, 85, 255),
            _ => ImGuiHelper.Color(120, 200, 120, 255),
        };

        private static void DedupeProfiles(SekhemaHelperSettings settings)
        {
            var seen = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
            for (var i = settings.Profiles.Count - 1; i >= 0; i--)
            {
                if (!seen.Add(settings.Profiles[i].Name))
                {
                    settings.Profiles.RemoveAt(i);
                }
            }

            if (settings.Profiles.Count == 0)
            {
                settings.Profiles = SekhemaProfiles.Both();
            }
        }

        private static bool EditAfflictions(System.Collections.Generic.Dictionary<string, float> weights, string filter)
        {
            var changed = false;
            var q = filter.Trim();
            foreach (var key in weights.Keys)
            {
                if (q.Length > 0 && !key.Contains(q, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var v = weights[key];
                ImGui.SetNextItemWidth(110f);
                if (ImGui.DragFloat("##skaff" + key, ref v, 50f, -1_000_000f, 1_000_000f, "%.0f"))
                {
                    weights[key] = v;
                    changed = true;
                }

                ImGui.SameLine();
                ImGui.BeginGroup();
                var known = SekhemaAfflictionText.TryGet(key, out var info);
                var name = known && info.Major ? key + "  (Major)" : key;
                ImGui.TextColored(AfflictionColor(v), name);
                if (known)
                {
                    ImGui.PushStyleColor(ImGuiCol.Text, new Vector4(0.65f, 0.65f, 0.65f, 1f));
                    ImGui.TextWrapped(info.Description);
                    ImGui.PopStyleColor();
                }

                ImGui.EndGroup();
            }

            return changed;
        }

        private static Vector4 AfflictionColor(float weight)
        {
            if (weight <= -2000f)
            {
                return new Vector4(0.88f, 0.33f, 0.33f, 1f);
            }

            if (weight <= -300f)
            {
                return new Vector4(0.92f, 0.71f, 0.27f, 1f);
            }

            if (weight < 0f)
            {
                return new Vector4(0.67f, 0.82f, 0.43f, 1f);
            }

            return new Vector4(1f, 1f, 1f, 1f);
        }

        private static bool EditWeights(System.Collections.Generic.Dictionary<string, float> weights, string filter)
        {
            var changed = false;
            var q = filter.Trim();
            foreach (var key in weights.Keys)
            {
                if (q.Length > 0 && !key.Contains(q, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var v = weights[key];
                if (ImGui.DragFloat(key + "##skw", ref v, 50f, -1_000_000f, 1_000_000f))
                {
                    weights[key] = v;
                    changed = true;
                }
            }

            return changed;
        }
    }
}
