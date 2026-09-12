using System.Collections.Generic;
using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>Why a chip is on, which is also the order they are shown in.</summary>
    public enum ChipPriority { Honesty, Validity, State, Hint }

    /// <summary>One header chip.</summary>
    public readonly struct HonestyChip
    {
        public readonly string Id;
        public readonly string Text;
        public readonly ChipPriority Priority;

        public HonestyChip(string id, string text, ChipPriority priority)
        {
            Id = id;
            Text = text;
            Priority = priority;
        }

        /// <summary>Honesty chips are coral with a <c>!</c>; hints are an Accent outline.</summary>
        public Color Colour => Priority switch
        {
            ChipPriority.Honesty => CosmoTheme.Warning,
            ChipPriority.Validity => CosmoTheme.Warning,
            ChipPriority.Hint => CosmoTheme.Accent,
            _ => CosmoTheme.TextSecondary
        };

        public override string ToString() => Text;
    }

    /// <summary>
    /// What the view is doing that the eye cannot tell, computed from <see cref="ViewOptions"/> and
    /// nothing else.
    ///
    /// The product is named for this and it had no mechanical check: the one rule fired on the
    /// <b>focus</b> only, so "Readable preset, nothing selected" drew Phobos at fifteen times life
    /// size with no marker at all, and it covered neither the 2.5 px star floor nor the automatic
    /// far-assist, both of which inflate size and brightness across the whole view.
    ///
    /// Two different rules (R43), which is why they are two methods here:
    /// the <b>chip</b> is on whenever the preset is not True <i>or any drawn body</i> exceeds
    /// x1.05; the <b>badge</b> names the focus's own factor.
    ///
    /// <see cref="All"/> returns the complete set; <see cref="Shown"/> returns the four the header
    /// has room for plus a <c>+N</c>. The G1c <c>honesty</c> assertion compares against
    /// <see cref="All"/>, because a plausible view produces nine at once and clipping would drop
    /// exactly the ones the product exists for.
    /// </summary>
    public static class HonestyState
    {
        /// <summary>How many chips the header row fits before collapsing the rest (R53).</summary>
        public const int VisibleChips = 4;

        /// <summary>Every active chip, in priority order.</summary>
        public static List<HonestyChip> All(ViewOptions options, string levelToPlane)
        {
            var chips = new List<HonestyChip>(10);
            if (options == null) return chips;

            // 1. Honesty.
            if (options.Sizes != SizePreset.True || options.MaxDrawnExaggeration > SizePresets.HonestyThreshold)
            {
                chips.Add(new HonestyChip("sizes",
                    "! SIZES NOT TO SCALE " + SizePresets.Name(options.Sizes) +
                    " · up to " + Format.Multiplier(options.MaxDrawnExaggeration),
                    ChipPriority.Honesty));
            }

            if (Differs(options.EffectiveStarGain))
                chips.Add(new HonestyChip("brightness", "! BRIGHTNESS " + Format.Multiplier(options.EffectiveStarGain), ChipPriority.Honesty));

            AddGain(chips, options, "galaxiesLocal", "glow", "GALAXY GLOW");
            AddGain(chips, options, "galaxies2mrs", "glow", "GALAXY GLOW");
            AddGain(chips, options, "sdss", "glow", "GALAXY GLOW");
            AddGain(chips, options, "dust", "density", "DUST");
            AddGain(chips, options, "nebulae", "density", "NEBULA");

            if (Differs(options.ReliefFactor))
                chips.Add(new HonestyChip("relief", "! RELIEF " + Format.Multiplier(options.ReliefFactor), ChipPriority.Honesty));

            // 2. Validity.
            if (options.EphemerisExtrapolated)
                chips.Add(new HonestyChip("ephemeris", "! EPHEMERIS EXTRAPOLATED", ChipPriority.Validity));
            if (options.StarMotionExtrapolated)
                chips.Add(new HonestyChip("starmotion", "! STAR MOTION EXTRAPOLATED", ChipPriority.Validity));

            // 3. State.
            if (options.Layers.Solo != SoloGroup.None)
                chips.Add(new HonestyChip("solo", "SOLO: " + options.Layers.Solo.ToString().ToUpperInvariant() + " x", ChipPriority.State));
            if (options.MoonsHidden > 0)
                chips.Add(new HonestyChip("moons", options.MoonsHidden + " moons hidden", ChipPriority.State));
            if (options.QualityReduced)
                chips.Add(new HonestyChip("quality", "QUALITY REDUCED", ChipPriority.State));

            // 4. Hints.
            if (!string.IsNullOrEmpty(levelToPlane))
                chips.Add(new HonestyChip("level", "Level to " + levelToPlane, ChipPriority.Hint));

            return chips;
        }

        /// <summary>The chips the header draws: the first four, then one <c>+N</c>.</summary>
        public static List<HonestyChip> Shown(List<HonestyChip> all)
        {
            if (all == null) return new List<HonestyChip>();
            if (all.Count <= VisibleChips) return all;

            var shown = new List<HonestyChip>(VisibleChips + 1);
            for (int i = 0; i < VisibleChips; i++) shown.Add(all[i]);
            shown.Add(new HonestyChip("more", "+" + (all.Count - VisibleChips), ChipPriority.State));
            return shown;
        }

        /// <summary>
        /// The on-universe badge under the focus's label, or null.
        ///
        /// Fires on the focus's own factor even in True mode, because the 2.5 px visibility floor
        /// inflates a distant body whatever the preset says.
        /// </summary>
        public static string FocusBadge(ViewOptions options)
        {
            if (options == null || options.FocusExaggeration <= SizePresets.HonestyThreshold) return null;
            return "! SIZES NOT TO SCALE " + Format.Multiplier(options.FocusExaggeration);
        }

        /// <summary>The sentence INFO -&gt; THIS VIEW opens with, in the headset and not only on the desktop.</summary>
        public static string Statement(ViewOptions options)
        {
            if (options == null) return string.Empty;
            if (options.Sizes == SizePreset.True && options.MaxDrawnExaggeration <= SizePresets.HonestyThreshold)
                return "Sizes, distances and directions are true at this view.";

            return "Distances and directions are true. Sizes are not: " +
                   SizePresets.Name(options.Sizes) + ", up to " +
                   Format.Multiplier(options.MaxDrawnExaggeration) + " life size.";
        }

        private static bool Differs(double gain) => System.Math.Abs(gain - 1.0) > 0.05;

        private static void AddGain(List<HonestyChip> chips, ViewOptions options,
            string layerId, string paramId, string caption)
        {
            double value = options.Layers.Param(layerId, paramId);
            if (!Differs(value)) return;

            // One chip per caption: three galaxy layers share GALAXY GLOW, and three identical
            // chips in a four-chip header would push the sizes warning off the end.
            for (int i = 0; i < chips.Count; i++)
                if (chips[i].Id == caption) return;

            chips.Add(new HonestyChip(caption, "! " + caption + " " + Format.Multiplier(value), ChipPriority.Honesty));
        }
    }
}
