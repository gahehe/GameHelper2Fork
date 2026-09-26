// <copyright file="SekhemaHelperSettings.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace SekhemaHelper
{
    using System.Collections.Generic;
    using System.Numerics;
    using GameHelper.Plugin;

    /// <summary>Trial of the Sekhemas plugin settings.</summary>
    public sealed class WeightProfile
    {
        /// <summary>Profile name.</summary>
        public string Name = string.Empty;

        /// <summary>Room type weights.</summary>
        public Dictionary<string, float> RoomTypeWeights = new();

        /// <summary>Affliction weights. Build-aware names ignore this number.</summary>
        public Dictionary<string, float> AfflictionWeights = new();

        /// <summary>Reward weights.</summary>
        public Dictionary<string, float> RewardWeights = new();

        /// <summary>Merchant rewards drop when sacred water is below this.</summary>
        public float AvoidMerchantBelowWater = 120f;

        /// <summary>Honour rewards drop when honour percent is above this.</summary>
        public float AvoidHonourAbovePct = 75f;
    }

    /// <summary>One cache type row. Drawing uses this later.</summary>
    public sealed class ChestTypeSetting
    {
        /// <summary>Cache id.</summary>
        public string Id = string.Empty;

        /// <summary>Draw this cache.</summary>
        public bool Show = true;

        /// <summary>Extra ring.</summary>
        public bool Highlight;

        /// <summary>Circle color.</summary>
        public Vector4 Color = new(1f, 0.85f, 0.3f, 1f);
    }

    /// <summary>Saved SekhemaHelper settings.</summary>
    public sealed class SekhemaHelperSettings : IPSettings
    {
        /// <summary>Master gate.</summary>
        public bool Enabled = true;

        /// <summary>Room type labels on the trial map. Drawing comes after the floor log matches.</summary>
        public bool DrawMapOverlay = true;

        /// <summary>Best-path frames.</summary>
        public bool DrawBestPath = true;

        /// <summary>Frame thickness in pixels.</summary>
        public float FrameThickness = 2.5f;

        /// <summary>Best-path frame color.</summary>
        public Vector4 BestPathColor = new(0.2f, 0.85f, 1f, 1f);

        /// <summary>Active weight profile name.</summary>
        public string ActiveProfileName = "Default";

        /// <summary>Weight profiles.</summary>
        public List<WeightProfile> Profiles = SekhemaProfiles.Both();

        /// <summary>Portal markers.</summary>
        public bool ShowPortals = true;

        /// <summary>Lever markers.</summary>
        public bool ShowLevers = true;

        /// <summary>Crystal markers.</summary>
        public bool ShowCrystals = true;

        /// <summary>Chest markers.</summary>
        public bool ShowChests = true;

        /// <summary>Short chest names.</summary>
        public bool ShowChestLabels = true;

        /// <summary>Portal color.</summary>
        public Vector4 PortalColor = new(0.85f, 0.45f, 1f, 1f);

        /// <summary>Lever color.</summary>
        public Vector4 LeverColor = new(1f, 0.8f, 0.2f, 1f);

        /// <summary>Crystal color.</summary>
        public Vector4 CrystalColor = new(0.3f, 1f, 0.9f, 1f);

        /// <summary>POI radius.</summary>
        public float PoiRadius = 9f;

        /// <summary>Chest radius.</summary>
        public float ChestRadius = 6f;

        /// <summary>Per-cache rows.</summary>
        public List<ChestTypeSetting> ChestTypes = DefaultChests();

        /// <summary>The profile used for scoring.</summary>
        public WeightProfile Active()
        {
            foreach (var p in this.Profiles)
            {
                if (p.Name == this.ActiveProfileName)
                {
                    return p;
                }
            }

            return this.Profiles.Count > 0 ? this.Profiles[0] : SekhemaProfiles.Default();
        }

        private static List<ChestTypeSetting> DefaultChests()
        {
            (string Id, bool Show, bool Hi, Vector4 Color)[] rows =
            [
                ("GrandSpectrum", true, true, new(1f, 0.30f, 1f, 1f)),
                ("RadiusJewels", true, true, new(0.65f, 0.40f, 1f, 1f)),
                ("LargeRelic", true, true, new(0.30f, 1f, 0.90f, 1f)),
                ("Jewels", true, true, new(0.55f, 0.55f, 1f, 1f)),
                ("Currency", true, false, new(1f, 0.85f, 0.30f, 1f)),
                ("MediumRelic", true, false, new(0.25f, 0.85f, 0.75f, 1f)),
                ("Maps", true, false, new(1f, 0.60f, 0.20f, 1f)),
                ("Ring", true, false, new(1f, 0.50f, 0.70f, 1f)),
                ("Amulet", true, false, new(1f, 0.55f, 0.45f, 1f)),
                ("SmallRelic", true, false, new(0.20f, 0.70f, 0.60f, 1f)),
                ("Gold", true, false, new(0.95f, 0.80f, 0.10f, 1f)),
                ("Belt", true, false, new(0.85f, 0.70f, 0.50f, 1f)),
                ("BodyArmour", true, false, new(0.90f, 0.35f, 0.30f, 1f)),
                ("Helmet", true, false, new(0.95f, 0.50f, 0.40f, 1f)),
                ("Gloves", true, false, new(0.90f, 0.60f, 0.50f, 1f)),
                ("Boots", true, false, new(0.80f, 0.55f, 0.35f, 1f)),
                ("Shield", true, false, new(0.50f, 0.70f, 0.90f, 1f)),
                ("MeleeWeapons", true, false, new(0.85f, 0.30f, 0.50f, 1f)),
                ("RangedWeapons", true, false, new(0.40f, 0.85f, 0.40f, 1f)),
                ("CasterWeapons", true, false, new(0.35f, 0.60f, 1f, 1f)),
                ("Generic", true, false, new(0.75f, 0.75f, 0.75f, 1f)),
                ("Burial", false, false, new(0.50f, 0.50f, 0.50f, 1f)),
            ];
            var list = new List<ChestTypeSetting>(rows.Length);
            foreach (var row in rows)
            {
                list.Add(new ChestTypeSetting { Id = row.Id, Show = row.Show, Highlight = row.Hi, Color = row.Color });
            }

            return list;
        }
    }
}
