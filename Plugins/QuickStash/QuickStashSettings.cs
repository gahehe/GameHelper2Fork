// <copyright file="QuickStashSettings.cs" company="None">
// Copyright (c) None. All rights reserved.
// </copyright>

namespace QuickStash
{
    using ClickableTransparentOverlay.Win32;
    using GameHelper.Plugin;

    /// <summary>
    ///     Settings for the QuickStash plugin.
    /// </summary>
    public sealed class QuickStashSettings : IPSettings
    {
        /// <summary>Inventory columns. PoE2 backpack width.</summary>
        public const int Cols = 12;

        /// <summary>Inventory rows. PoE2 backpack height.</summary>
        public const int Rows = 5;

        /// <summary>Cell count in <see cref="KeepSlots"/>.</summary>
        public const int SlotCount = Cols * Rows;

        /// <summary>
        ///     Key that Ctrl+clicks non-kept backpack cells into the open left panel.
        /// </summary>
        public VK DumpKey = VK.F4;

        /// <summary>
        ///     Row-major 12x5 mask (<c>index = y * 12 + x</c>). True = keep that cell.
        ///     A multi-cell item is kept when its top-left cell is true.
        /// </summary>
        public bool[] KeepSlots = new bool[SlotCount];

        /// <summary>Inclusive lower bound (ms) of the per-click gap.</summary>
        public int MinDelayMs = 50;

        /// <summary>Inclusive upper bound (ms) of the per-click gap.</summary>
        public int MaxDelayMs = 100;
    }
}
