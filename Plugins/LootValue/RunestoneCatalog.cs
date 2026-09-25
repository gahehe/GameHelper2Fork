// <copyright file="RunestoneCatalog.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace LootValue
{
    using System.Reflection;
    using Newtonsoft.Json;

    /// <summary>
    ///     Offline Expedition2 recipe table. Given a runestone's live hole count, anchor, and
    ///     area level, returns the reward names that station will offer. No memory reads.
    /// </summary>
    internal sealed class RunestoneCatalog
    {
        private readonly List<Recipe> recipes;
        private readonly Dictionary<int, string> runeNames;
        private readonly Dictionary<long, int> partialMinLevel;

        private RunestoneCatalog(List<Recipe> recipes, Dictionary<int, string> runeNames, Dictionary<long, int> partialMinLevel)
        {
            this.recipes = recipes;
            this.runeNames = runeNames;
            this.partialMinLevel = partialMinLevel;
        }

        public bool IsLoaded => this.recipes.Count > 0;

        public string RuneName(int index) => this.runeNames.TryGetValue(index, out var name) ? name : $"#{index}";

        public readonly record struct Offer(string Name, int Count);

        /// <summary>
        ///     Recipes this station offers. <paramref name="isUnique"/> is the anchor-less station.
        ///     A failed anchor decode (<paramref name="anchorIndex"/> &lt; 0 on a normal station)
        ///     returns nothing rather than every recipe that fits the hole count.
        /// </summary>
        public List<Offer> Offers(int anchorIndex, int anchorPos, int holeCount, bool isUnique, int areaLevel)
        {
            var result = new List<Offer>();
            if (holeCount <= 0)
            {
                return result;
            }

            foreach (var rec in this.recipes)
            {
                if (rec.Size > holeCount)
                {
                    continue;
                }

                if (areaLevel > 0 && rec.MaxLevel > 0 && (areaLevel < rec.MinLevel || areaLevel > rec.MaxLevel))
                {
                    continue;
                }

                if (isUnique)
                {
                    Add(result, rec);
                    continue;
                }

                if (anchorIndex < 0 || rec.RuneIdx == null || rec.RuneIdx.Count <= anchorPos)
                {
                    continue;
                }

                if (rec.RuneIdx[anchorPos] != anchorIndex)
                {
                    continue;
                }

                if (rec.Size != holeCount && !this.IsPartialAllowed(anchorIndex, anchorPos, rec.Size, areaLevel))
                {
                    continue;
                }

                Add(result, rec);
            }

            return result;
        }

        private static void Add(List<Offer> result, Recipe rec)
        {
            var name = rec.Reward?.Name;
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            result.Add(new Offer(name, Math.Max(1, rec.RewardCount)));
        }

        private bool IsPartialAllowed(int index, int pos, int size, int areaLevel)
        {
            if (!this.partialMinLevel.TryGetValue(PartialKey(index, pos + 1, size), out var minLevel))
            {
                return false;
            }

            return areaLevel <= 0 || areaLevel >= minLevel;
        }

        private static long PartialKey(int rune, int pos1Based, int size)
            => ((long)rune << 16) | ((long)pos1Based << 8) | (uint)size;

        public static RunestoneCatalog Load()
        {
            try
            {
                var asm = typeof(RunestoneCatalog).Assembly;
                var name = Array.Find(asm.GetManifestResourceNames(), n => n.EndsWith("expedition2_recipes.json", StringComparison.Ordinal));
                if (name == null)
                {
                    return Empty();
                }

                using var stream = asm.GetManifestResourceStream(name);
                if (stream == null)
                {
                    return Empty();
                }

                using var reader = new StreamReader(stream);
                var file = JsonConvert.DeserializeObject<CatalogFile>(reader.ReadToEnd());
                if (file?.Recipes == null)
                {
                    return Empty();
                }

                var names = new Dictionary<int, string>();
                if (file.Runes != null)
                {
                    foreach (var kv in file.Runes)
                    {
                        if (int.TryParse(kv.Key, out var index))
                        {
                            names[index] = kv.Value;
                        }
                    }
                }

                var partial = new Dictionary<long, int>();
                if (file.RuneWeights != null)
                {
                    foreach (var weight in file.RuneWeights)
                    {
                        var key = PartialKey(weight.Rune, weight.Pos, weight.Size);
                        if (!partial.TryGetValue(key, out var current) || weight.MinLevel < current)
                        {
                            partial[key] = weight.MinLevel;
                        }
                    }
                }

                return new RunestoneCatalog(file.Recipes, names, partial);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[LootValue] Runestone catalog load failed: {ex.Message}");
                return Empty();
            }
        }

        private static RunestoneCatalog Empty()
            => new(new List<Recipe>(), new Dictionary<int, string>(), new Dictionary<long, int>());

        private sealed class CatalogFile
        {
            [JsonProperty("runes")]
            public Dictionary<string, string>? Runes { get; set; }

            [JsonProperty("recipes")]
            public List<Recipe>? Recipes { get; set; }

            [JsonProperty("runeWeights")]
            public List<RuneWeight>? RuneWeights { get; set; }
        }

        private sealed class RuneWeight
        {
            [JsonProperty("rune")]
            public int Rune { get; set; }

            [JsonProperty("pos")]
            public int Pos { get; set; }

            [JsonProperty("size")]
            public int Size { get; set; }

            [JsonProperty("minLevel")]
            public int MinLevel { get; set; }
        }

        private sealed class Recipe
        {
            [JsonProperty("size")]
            public int Size { get; set; }

            [JsonProperty("runeIdx")]
            public List<int>? RuneIdx { get; set; }

            [JsonProperty("reward")]
            public Reward? Reward { get; set; }

            [JsonProperty("rewardCount")]
            public int RewardCount { get; set; }

            [JsonProperty("minLevel")]
            public int MinLevel { get; set; }

            [JsonProperty("maxLevel")]
            public int MaxLevel { get; set; }
        }

        private sealed class Reward
        {
            [JsonProperty("name")]
            public string Name { get; set; } = string.Empty;
        }
    }
}
