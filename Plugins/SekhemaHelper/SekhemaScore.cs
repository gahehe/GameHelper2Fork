// <copyright file="SekhemaScore.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace SekhemaHelper
{
    using System;
    using System.Collections.Generic;

    internal enum SekhemaRisk
    {
        None,
        Minor,
        Moderate,
        Severe,
    }

    internal sealed class SekhemaRoom
    {
        public int Layer;
        public int Index;
        public string RoomType = string.Empty;
        public string Affliction = string.Empty;
        public string Reward = string.Empty;
        public List<int> Connections = [];
        public double Score;
        public double NormScore;
        public SekhemaRisk Risk;
        public bool OnBestPath;
        public long Widget;
        public float X;
        public float Y;
        public float W;
        public float H;
    }

    internal sealed class SekhemaFloor
    {
        public List<List<SekhemaRoom>> Layers = [];
        public int PlayerLayer = -1;
        public int PlayerRoom = -1;
        public int Water;
        public int KeysBronze;
        public int KeysSilver;
        public int KeysGold;
        public float HonourPct;
        public bool ResourcesValid;
    }

    internal sealed class Defenses
    {
        public int Armour;
        public int Evasion;
        public int EnergyShield;
        public int Life;
        public bool HasQueenOfTheForest;
    }

    internal static class SekhemaScore
    {
        public const double RoomScoreBase = 1_000_000.0;

        public static void Evaluate(SekhemaFloor floor, WeightProfile profile, Defenses defenses)
        {
            var lo = double.MaxValue;
            var hi = double.MinValue;
            foreach (var layer in floor.Layers)
            {
                foreach (var room in layer)
                {
                    var aw = AfflictionWeight(room.Affliction, profile, defenses);
                    room.Score = RoomScoreBase
                        + Weight(profile.RoomTypeWeights, room.RoomType)
                        + aw
                        + RewardWeight(room.Reward, profile, floor);
                    room.Risk = RiskFor(aw, room.Affliction.Length > 0);
                    lo = Math.Min(lo, room.Score);
                    hi = Math.Max(hi, room.Score);
                }
            }

            var span = hi - lo;
            foreach (var layer in floor.Layers)
            {
                foreach (var room in layer)
                {
                    room.NormScore = span > 1e-9 ? 10.0 * (room.Score - lo) / span : 10.0;
                }
            }

            BestPath(floor);
        }

        public static double AfflictionWeight(string name, WeightProfile profile, Defenses d)
        {
            if (name.Length == 0)
            {
                return 0;
            }

            const double k = 5000.0;
            var armourPool = Math.Max(d.Armour + d.Evasion, 4000.0);
            var esPool = Math.Max(d.EnergyShield + d.Life, 4000.0);
            return name switch
            {
                "Sharpened Arrowhead" => -k * (d.Armour / armourPool),
                "Iron Manacles" => -k * (d.Evasion / armourPool),
                "Shattered Shield" => -k * (d.EnergyShield / esPool) * 0.5,
                "Corrosive Concoction" => -k * ((d.Armour / armourPool) + (d.Evasion / armourPool)) - (k * (d.EnergyShield / esPool)),
                "Worn Sandals" when d.HasQueenOfTheForest => 0,
                _ => Weight(profile.AfflictionWeights, name),
            };
        }

        private static double RewardWeight(string reward, WeightProfile profile, SekhemaFloor floor)
        {
            var w = Weight(profile.RewardWeights, reward);
            if (!floor.ResourcesValid)
            {
                return w;
            }

            if (reward.Contains("Merchant", StringComparison.Ordinal) && floor.Water < (int)profile.AvoidMerchantBelowWater)
            {
                w -= 300;
            }

            if ((reward.Contains("Honour", StringComparison.Ordinal) || reward.Contains("Honor", StringComparison.Ordinal))
                && floor.HonourPct > profile.AvoidHonourAbovePct)
            {
                w -= 300;
            }

            return w;
        }

        private static SekhemaRisk RiskFor(double afflictionWeight, bool hasAffliction)
        {
            if (!hasAffliction || afflictionWeight >= 0)
            {
                return SekhemaRisk.None;
            }

            if (afflictionWeight <= -2000)
            {
                return SekhemaRisk.Severe;
            }

            return afflictionWeight <= -300 ? SekhemaRisk.Moderate : SekhemaRisk.Minor;
        }

        private static double Weight(Dictionary<string, float> map, string key)
            => key.Length > 0 && map.TryGetValue(key, out var v) ? v : 0;

        private static void BestPath(SekhemaFloor floor)
        {
            foreach (var layer in floor.Layers)
            {
                foreach (var room in layer)
                {
                    room.OnBestPath = false;
                }
            }

            var lCount = floor.Layers.Count;
            if (lCount == 0 || floor.PlayerLayer >= lCount)
            {
                return;
            }

            var dp = new double[lCount][];
            var next = new int[lCount][];
            for (var l = 0; l < lCount; l++)
            {
                dp[l] = new double[floor.Layers[l].Count];
                next[l] = new int[floor.Layers[l].Count];
                Array.Fill(next[l], -1);
            }

            for (var l = lCount - 1; l >= 0; l--)
            {
                for (var r = 0; r < floor.Layers[l].Count; r++)
                {
                    var room = floor.Layers[l][r];
                    var best = 0.0;
                    var bestIdx = -1;
                    var bestConns = -1;
                    if (l + 1 < lCount)
                    {
                        foreach (var c in room.Connections)
                        {
                            if (c < 0 || c >= floor.Layers[l + 1].Count)
                            {
                                continue;
                            }

                            var cand = dp[l + 1][c];
                            var conns = floor.Layers[l + 1][c].Connections.Count;
                            if (bestIdx < 0 || cand > best || (cand == best && conns > bestConns))
                            {
                                best = cand;
                                bestIdx = c;
                                bestConns = conns;
                            }
                        }
                    }

                    dp[l][r] = (room.Score - RoomScoreBase) + (bestIdx >= 0 ? best : 0);
                    next[l][r] = bestIdx;
                }
            }

            var walkLayer = floor.PlayerLayer;
            var roomIdx = floor.PlayerRoom;
            if (walkLayer < 0)
            {
                walkLayer = 0;
                var best = 0.0;
                var bestIdx = -1;
                for (var i = 0; i < floor.Layers[0].Count; i++)
                {
                    if (bestIdx < 0 || dp[0][i] > best)
                    {
                        best = dp[0][i];
                        bestIdx = i;
                    }
                }

                roomIdx = bestIdx;
            }

            while (walkLayer >= 0 && walkLayer < lCount && roomIdx >= 0 && roomIdx < floor.Layers[walkLayer].Count)
            {
                floor.Layers[walkLayer][roomIdx].OnBestPath = true;
                var nxt = next[walkLayer][roomIdx];
                if (nxt < 0)
                {
                    break;
                }

                walkLayer++;
                roomIdx = nxt;
            }
        }
    }
}
