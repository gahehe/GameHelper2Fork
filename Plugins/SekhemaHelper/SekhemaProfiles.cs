// <copyright file="SekhemaProfiles.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace SekhemaHelper
{
    using System.Collections.Generic;

    internal static class SekhemaProfiles
    {
        public static WeightProfile Default()
        {
            var p = new WeightProfile { Name = "Default" };
            p.RoomTypeWeights = new Dictionary<string, float>
            {
                ["Gauntlet"] = -1000f,
                ["Hourglass"] = -200f,
                ["Chalice"] = 0f,
                ["Ritual"] = 0f,
                ["Escape"] = 100f,
                ["Boss"] = 0f,
            };
            p.AfflictionWeights = new Dictionary<string, float>
            {
                ["Glass Shard"] = -4000f,
                ["Ghastly Scythe"] = -4000f,
                ["Veiled Sight"] = -4000f,
                ["Myriad Aspersions"] = -4000f,
                ["Deceptive Mirror"] = -4000f,
                ["Purple Smoke"] = -4000f,
                ["Red Smoke"] = -4000f,
                ["Black Smoke"] = -4000f,
                ["Rapid Quicksand"] = -1000f,
                ["Deadly Snare"] = -1000f,
                ["Forgotten Traditions"] = -1000f,
                ["Season of Famine"] = -1000f,
                ["Orb of Negation"] = -1000f,
                ["Winter Drought"] = -1000f,
                ["Branded Balbalakh"] = -1000f,
                ["Chiselled Stone"] = -1000f,
                ["Untouchable"] = -1000f,
                ["Blunt Sword"] = -1000f,
                ["Spiked Shell"] = -1000f,
                ["Unassuming Brick"] = -1000f,
                ["Costly Aid"] = -900f,
                ["Orbala's Leathers"] = -800f,
                ["Tradition's Demand"] = -800f,
                ["Chains of Binding"] = -600f,
                ["Hungry Fangs"] = -600f,
                ["Death Toll"] = -400f,
                ["Worn Sandals"] = -400f,
                ["Golden Smoke"] = -400f,
                ["Fiendish Wings"] = -400f,
                ["Honed Claws"] = -400f,
                ["Trade Tariff"] = -300f,
                ["Spiked Exit"] = -300f,
                ["Dark Pit"] = -300f,
                ["Tattered Blindfold"] = -300f,
                ["Rusted Mallet"] = -300f,
                ["Suspected Sympathiser"] = -200f,
                ["Unquenched Thirst"] = -200f,
                ["Exhausted Wells"] = -200f,
                ["Dishonoured Tattoo"] = -200f,
                ["Weakened Flesh"] = -100f,
                ["Haemorrhage"] = -100f,
                ["Gate Toll"] = -100f,
                ["Leaking Waterskin"] = -100f,
                ["Low Rivers"] = -100f,
                ["Corrosive Concoction"] = 0f,
                ["Iron Manacles"] = 0f,
                ["Shattered Shield"] = 0f,
                ["Sharpened Arrowhead"] = 0f,
            };
            p.RewardWeights = new Dictionary<string, float>
            {
                ["Large Fountain"] = 100f,
                ["Fountain"] = 50f,
                ["Honour"] = 50f,
                ["Honour Galai"] = 300f,
                ["Boon"] = 200f,
                ["Merchant"] = 20f,
                ["Pledge to Kochai"] = 20f,
                ["Bronze Key"] = 50f,
                ["Silver Key"] = 100f,
                ["Gold Key"] = 200f,
                ["Bronze Cache"] = 30f,
                ["Silver Cache"] = 60f,
                ["Golden Cache"] = 120f,
                ["Curse"] = -200f,
                ["Random"] = 20f,
            };
            return p;
        }

        public static WeightProfile NoHit()
        {
            var p = Default();
            p.Name = "No-Hit";
            p.RoomTypeWeights["Hourglass"] = -1000f;
            p.RoomTypeWeights["Gauntlet"] = -200f;
            p.AfflictionWeights["Death Toll"] = -500_000f;
            p.AfflictionWeights["Spiked Exit"] = -600_000f;
            p.AfflictionWeights["Deceptive Mirror"] = -400_000f;
            p.AfflictionWeights["Glass Shard"] = -50_000f;
            p.AfflictionWeights["Myriad Aspersions"] = -50_000f;
            return p;
        }

        public static List<WeightProfile> Both() => [Default(), NoHit()];
    }
}
