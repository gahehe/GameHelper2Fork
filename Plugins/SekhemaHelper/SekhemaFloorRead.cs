// <copyright file="SekhemaFloorRead.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace SekhemaHelper
{
    using System;
    using System.Collections.Generic;
    using System.Globalization;
    using GameHelper.RemoteEnums;

    /// <summary>
    /// Trial floor read. Visual panel is GameUI child 84 child 0.
    /// Floor object is parent+0x318. Layers are floor+0x30.
    /// Room foreign keys: type +0x500, reward +0x510, affliction +0x520.
    /// Links: floor-object child 0, vector +0x2C8, 24-byte segments.
    /// HUD text is GameUI child 13, leaf +0x360.
    /// </summary>
    internal static class SekhemaFloorRead
    {
        public static SekhemaFloor? TryRead(long panelAddress, Defenses defenses, WeightProfile profile)
        {
            var host = SekhemaMemory.I64(panelAddress + 0xB8);
            if (host < 0x10000)
            {
                return null;
            }

            var floorObj = SekhemaMemory.I64(host + 0x318);
            var layers = floorObj > 0x10000 ? SekhemaMemory.I64(floorObj + 0x30) : 0;
            var layerEls = layers > 0x10000 ? SekhemaMemory.Children(layers) : [];
            if (layerEls.Length is < 2 or > 16)
            {
                return null;
            }

            var floor = new SekhemaFloor();
            var centers = new List<(int L, int I, float X, float Y)>();
            for (var li = 0; li < layerEls.Length; li++)
            {
                var rooms = SekhemaMemory.Children(layerEls[li]);
                if (rooms.Length is < 1 or > 16)
                {
                    return null;
                }

                if (li == 0 && rooms.Length != 1)
                {
                    return null;
                }

                var lx = SekhemaMemory.F32(layerEls[li] + 0x100);
                var ly = SekhemaMemory.F32(layerEls[li] + 0x104);
                var layer = new List<SekhemaRoom>(rooms.Length);
                for (var ri = 0; ri < rooms.Length; ri++)
                {
                    var widget = rooms[ri];
                    var room = new SekhemaRoom { Layer = li, Index = ri, Widget = widget };
                    var typeId = RowId(widget, 0x500);
                    var rewardId = RowId(widget, 0x510);
                    room.RoomType = RoomType(typeId);
                    room.Reward = Reward(rewardId);
                    room.Affliction = RowName(widget, 0x520);
                    var rw = SekhemaMemory.F32(widget + 0x270);
                    var rh = SekhemaMemory.F32(widget + 0x274);
                    if (rw < 1f)
                    {
                        rw = 143f;
                    }

                    if (rh < 1f)
                    {
                        rh = 143f;
                    }

                    var rx = SekhemaMemory.F32(widget + 0x100);
                    var ry = SekhemaMemory.F32(widget + 0x104);
                    room.X = lx + rx;
                    room.Y = ly + ry;
                    room.W = rw;
                    room.H = rh;
                    layer.Add(room);
                    centers.Add((li, ri, room.X + (rw * 0.5f), room.Y + (rh * 0.5f)));
                }

                floor.Layers.Add(layer);
            }

            if (floor.Layers[^1].Count != 1)
            {
                return null;
            }

            ApplyLinks(floor, floorObj, centers);
            if (floor.PlayerLayer < 0 && floor.Layers.Count > 0 && floor.Layers[0].Count == 1)
            {
                floor.PlayerLayer = 0;
                floor.PlayerRoom = 0;
            }

            ReadHud(SekhemaMemory.I64(host + 0xB8), floor);
            SekhemaScore.Evaluate(floor, profile, defenses);
            return floor;
        }

        public static Defenses ReadDefenses(IReadOnlyDictionary<GameStats, int> buffs, IReadOnlyDictionary<GameStats, int> items)
        {
            var d = new Defenses();
            var seen = 0;
            void Take(IReadOnlyDictionary<GameStats, int> src, bool skipSeen)
            {
                foreach (var kv in src)
                {
                    var key = (int)kv.Key;
                    var bit = key switch
                    {
                        235 => 1,
                        239 => 2,
                        241 => 4,
                        276 => 8,
                        _ => 0,
                    };
                    if (skipSeen && bit != 0 && (seen & bit) != 0)
                    {
                        continue;
                    }

                    switch (key)
                    {
                        case 235: d.Armour += kv.Value; break;
                        case 239: d.Life += kv.Value; break;
                        case 241: d.EnergyShield += kv.Value; break;
                        case 276: d.Evasion += kv.Value; break;
                        case 9490: if (kv.Value > 0) d.HasQueenOfTheForest = true; break;
                    }

                    seen |= bit;
                }
            }

            Take(buffs, false);
            Take(items, true);
            return d;
        }

        public static string Signature(SekhemaFloor floor)
        {
            var parts = new List<string>(32) { floor.PlayerLayer + ":" + floor.PlayerRoom };
            foreach (var layer in floor.Layers)
            {
                foreach (var room in layer)
                {
                    parts.Add(room.Layer + "." + room.Index + room.RoomType + room.Reward + room.Affliction + string.Join(',', room.Connections));
                }
            }

            return string.Join("|", parts);
        }

        public static string FormatLog(SekhemaFloor floor)
        {
            var lines = new List<string>
            {
                $"water={floor.Water} keys={floor.KeysBronze}/{floor.KeysSilver}/{floor.KeysGold} honour={floor.HonourPct.ToString("0.0", CultureInfo.InvariantCulture)} current={floor.PlayerLayer}.{floor.PlayerRoom}",
            };
            foreach (var layer in floor.Layers)
            {
                foreach (var room in layer)
                {
                    if (room.RoomType.Length == 0 && room.Reward.Length == 0 && room.Affliction.Length == 0)
                    {
                        continue;
                    }

                    lines.Add(
                        $"{room.Layer}.{room.Index} {room.RoomType} | {room.Reward} | {room.Affliction} conns={string.Join(',', room.Connections)} path={(room.OnBestPath ? "Y" : "N")} norm={room.NormScore.ToString("0.0", CultureInfo.InvariantCulture)} risk={room.Risk}");
                }
            }

            return string.Join(Environment.NewLine, lines);
        }

        private static void ApplyLinks(SekhemaFloor floor, long floorObj, List<(int L, int I, float X, float Y)> centers)
        {
            var kids = SekhemaMemory.Children(floorObj);
            if (kids.Length == 0)
            {
                return;
            }

            var link = kids[0];
            var first = SekhemaMemory.I64(link + 0x2C8);
            var last = SekhemaMemory.I64(link + 0x2D0);
            if (first < 0x10000 || last <= first)
            {
                return;
            }

            var count = (int)((last - first) / 24);
            if (count is <= 0 or > 80)
            {
                return;
            }

            var taken = new List<(int L, int I)>();
            for (var s = 0; s < count; s++)
            {
                var at = first + (s * 24);
                var state = SekhemaMemory.I32(at + 16);
                if (state == 0)
                {
                    continue;
                }

                var ia = Nearest(centers, SekhemaMemory.F32(at), SekhemaMemory.F32(at + 4), out var da);
                var ib = Nearest(centers, SekhemaMemory.F32(at + 8), SekhemaMemory.F32(at + 12), out var db);
                if (ia < 0 || ib < 0 || da > 120f || db > 120f)
                {
                    continue;
                }

                var a = centers[ia];
                var b = centers[ib];
                if (a.L == b.L + 1)
                {
                    (a, b) = (b, a);
                }
                else if (b.L != a.L + 1)
                {
                    continue;
                }

                var src = floor.Layers[a.L][a.I];
                if (!src.Connections.Contains(b.I))
                {
                    src.Connections.Add(b.I);
                }

                if (state == 3)
                {
                    taken.Add((a.L, a.I));
                    taken.Add((b.L, b.I));
                }
            }

            if (taken.Count == 0)
            {
                return;
            }

            var end = taken[^1];
            floor.PlayerLayer = end.L;
            floor.PlayerRoom = end.I;
        }

        private static int Nearest(List<(int L, int I, float X, float Y)> centers, float x, float y, out float dist)
        {
            var best = -1;
            var bestSq = float.MaxValue;
            for (var i = 0; i < centers.Count; i++)
            {
                var dx = centers[i].X - x;
                var dy = centers[i].Y - y;
                var d = (dx * dx) + (dy * dy);
                if (d >= bestSq)
                {
                    continue;
                }

                bestSq = d;
                best = i;
            }

            dist = MathF.Sqrt(bestSq);
            return best;
        }

        private static void ReadHud(long gameUi, SekhemaFloor floor)
        {
            var kids = SekhemaMemory.Children(gameUi);
            if (kids.Length <= 13)
            {
                return;
            }

            var hud = SekhemaMemory.Children(kids[13]);
            if (hud.Length < 6)
            {
                return;
            }

            floor.Water = LeadingInt(LeafText(hud[0]));
            floor.KeysBronze = LeadingInt(LeafText(hud[1]));
            floor.KeysSilver = LeadingInt(LeafText(hud[2]));
            floor.KeysGold = LeadingInt(LeafText(hud[3]));
            var slotW = SekhemaMemory.F32(hud[5] + 0x270);
            var fillKids = SekhemaMemory.Children(hud[5]);
            var fillW = fillKids.Length > 1 ? SekhemaMemory.F32(fillKids[1] + 0x270) : 0f;
            if (slotW > 0f)
            {
                floor.HonourPct = Math.Clamp(100f * fillW / slotW, 0f, 100f);
            }

            floor.ResourcesValid = floor.Water > 0 || floor.HonourPct > 0f;
        }

        private static string LeafText(long slot)
        {
            var kids = SekhemaMemory.Children(slot);
            return kids.Length > 1 ? SekhemaMemory.WString(kids[1] + 0x360) : string.Empty;
        }

        private static int LeadingInt(string text)
        {
            long v = 0;
            var started = false;
            foreach (var c in text)
            {
                if (c is >= '0' and <= '9')
                {
                    v = (v * 10) + (c - '0');
                    started = true;
                }
                else if (started)
                {
                    break;
                }
            }

            return started ? (int)v : 0;
        }

        private static string RowId(long widget, int offset)
        {
            var row = SekhemaMemory.I64(widget + offset);
            if (row < 0x10000)
            {
                return string.Empty;
            }

            return SekhemaMemory.Utf16(SekhemaMemory.I64(row));
        }

        private static string RowName(long widget, int offset)
        {
            var row = SekhemaMemory.I64(widget + offset);
            if (row < 0x10000)
            {
                return string.Empty;
            }

            return SekhemaMemory.Utf16(SekhemaMemory.I64(row + 0x28));
        }

        private static string RoomType(string id)
        {
            var parts = id.Split('_');
            if (parts.Length < 2)
            {
                return string.Empty;
            }

            return parts[1] switch
            {
                "Arena" => "Hourglass",
                "Lair" => "Chalice",
                "Explore" => "Escape",
                _ => parts[1],
            };
        }

        private static string Reward(string id)
        {
            var s = id.ToLowerInvariant();
            if (s.Contains("key", StringComparison.Ordinal))
            {
                return s.Contains("gold", StringComparison.Ordinal) ? "Gold Key" : s.Contains("silver", StringComparison.Ordinal) ? "Silver Key" : "Bronze Key";
            }

            if (s.Contains("chest", StringComparison.Ordinal) || s.Contains("cache", StringComparison.Ordinal))
            {
                return s.Contains("gold", StringComparison.Ordinal) ? "Golden Cache" : s.Contains("silver", StringComparison.Ordinal) ? "Silver Cache" : "Bronze Cache";
            }

            if (s.Contains("water", StringComparison.Ordinal) || s.Contains("fountain", StringComparison.Ordinal))
            {
                return s.Contains("legend", StringComparison.Ordinal) || s.Contains("major", StringComparison.Ordinal) || s.Contains("large", StringComparison.Ordinal)
                    ? "Large Fountain"
                    : "Fountain";
            }

            if (s.Contains("merchant", StringComparison.Ordinal))
            {
                return "Merchant";
            }

            if (s.Contains("pledge", StringComparison.Ordinal))
            {
                return "Pledge to Kochai";
            }

            if (s.Contains("honor", StringComparison.Ordinal) || s.Contains("honour", StringComparison.Ordinal))
            {
                return "Honour";
            }

            if (s.Contains("boon", StringComparison.Ordinal))
            {
                return "Boon";
            }

            if (s.Contains("curse", StringComparison.Ordinal))
            {
                return "Curse";
            }

            return s.Contains("random", StringComparison.Ordinal) ? "Random" : string.Empty;
        }
    }
}
