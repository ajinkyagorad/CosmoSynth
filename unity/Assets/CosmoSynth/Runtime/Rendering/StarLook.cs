using CosmoSynth.Core;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// The one place "how bright is this star" becomes "how big, and what colour, is its dot".
    ///
    /// There are two star populations in this app — the 2500-star procedural field on the sky
    /// sphere, and the real catalogue stars pulled onto it — and they were answering that
    /// question in different units through different channels. The field carried magnitude in
    /// vertex <b>alpha</b> and the catalogue stars carried it in <b>diameter</b>, so the two
    /// looked like different kinds of object on the same sky: measured off a captured frame,
    /// every field star peaked at 25% of full brightness regardless of its magnitude while every
    /// catalogue star peaked at 100%, and the field's quads were roughly twice the angular size.
    /// <c>CosmoSynth/Point</c> already draws both populations with one analytic profile for
    /// reason; sharing the texture was never going to be enough while the two things the texture
    /// is multiplied by were decided separately.
    ///
    /// <b>Brightness lives in RGB, never in alpha.</b> These are drawn with an additive blend,
    /// which by construction ignores alpha — and this project has already found the harder
    /// version of that: <c>Particles/Standard Unlit</c> in additive mode does not modulate by the
    /// tint's alpha at all, which is written down in <c>BodySelector.BuildHighlight</c>. A
    /// brightness dial on the alpha channel is a dial connected to nothing. Scaling the colour
    /// itself is what an additive blend actually consumes, and it cannot be silently ignored by
    /// whatever shader ends up drawing the quad.
    ///
    /// <b>Sizes are angular</b>, in dmm — millimetres subtended at one metre, a milliradian —
    /// exactly as <see cref="CosmoTheme"/> states type and spacing. That is not decoration: the
    /// field's size used to be written as a fraction of the star sphere's radius, which is a
    /// linear quantity chosen for depth-buffer reasons, and stating it that way hid the fact that
    /// the faintest star was being drawn 11 dmm across. The full Moon is 9.1.
    /// </summary>
    public static class StarLook
    {
        /// <summary>
        /// Angular width of the quad a star at the faint end is drawn on, in dmm.
        ///
        /// The quad, not the visible dot: the Gaussian in <c>CosmoSynth/Point</c> is half its
        /// maximum at a third of the quad's width, so what the eye reads as the star is about a
        /// third of this.
        ///
        /// 2.7 dmm was too small, and the measurement says why. A star's rendered peak turns out
        /// to be governed by its <i>width</i> far more than by the brightness it is given, because
        /// a profile narrower than a pixel never has a pixel land near its peak: across a captured
        /// frame the median 2-pixel star peaked at 11/255 and the single 5-pixel one at 72, with
        /// the correlation between width and peak at 0.80. Below about four pixels the dot cannot
        /// be made bright by any amount of colour — the sampling throws the brightness away. So
        /// the floor is set where a star can still be lit, and the shader's Gaussian core was
        /// widened to match; the two are one decision.
        /// </summary>
        public const float FaintestQuadDmm = 3.8f;

        /// <summary>
        /// Angular width of the quad at the bright end, in dmm. About 8.6 pixels per eye.
        ///
        /// This has been wrong in both directions now, which is worth recording. It was 11 to 26
        /// dmm — every one of 2500 background stars drawn wider than the full Moon, which is what
        /// made the sky read as cotton wool. Correcting that to 2.7-6.5 overshot: the brightest
        /// star anywhere in the frame reached 72 out of 255 and the median 16, so the sky came
        /// back nearly empty. Nine dmm is just under the Moon's 9.1 and is reached by roughly one
        /// star in a thousand, which is the right frequency for something that should be
        /// conspicuous.
        ///
        /// The range is wide because the rendered peak depends on it so strongly, not because a
        /// star's apparent size genuinely varies this much. Size is the only channel that
        /// reliably survives to the screen here; brightness rides along with it.
        /// </summary>
        public const float BrightestQuadDmm = 9.0f;

        /// <summary>
        /// Peak brightness of the faintest star, as a fraction of its own colour.
        ///
        /// Not zero. The faintest stars should sit at the edge of visibility, which is what gives
        /// a sky depth instead of a uniform sprinkle — but this is a mixed reality app, and the
        /// floor is composited over a lit room rather than over black.
        ///
        /// 0.16 put them below the floor of perception even against black: a measured frame had
        /// its median star peaking at 16 out of 255 and only 125 of roughly 300 stars in view
        /// detectable at all. The faint majority has to read as pinpricks, which is not the same
        /// as reading as nothing.
        /// </summary>
        public const float FaintestBrightness = 0.40f;

        /// <summary>
        /// Quad width in dmm for a normalised brightness, 0 at the faint end and 1 at the bright.
        /// </summary>
        public static float QuadDmm(float brightness) =>
            Mathf.Lerp(FaintestQuadDmm, BrightestQuadDmm, Mathf.Clamp01(brightness));

        /// <summary>Quad width in radians, which is what a world-space size is derived from.</summary>
        public static float QuadRadians(float brightness) => QuadDmm(brightness) * 0.001f;

        /// <summary>
        /// The colour to draw a star of this black-body hue and this brightness.
        ///
        /// Two things happen here, and the second is the one that makes a field of dots read as a
        /// sky rather than as confetti.
        ///
        /// The magnitude scales the RGB, for the reasons in the class remarks.
        ///
        /// And <b>saturation falls with brightness</b>, because that is what the eye does. Colour
        /// vision is a function of the cones, which faint stars do not excite; a real sky is white
        /// pinpricks with colour in the handful of bright ones, and the colour of those few is
        /// information. Drawing every star at full black-body saturation inverts that. Since a
        /// steep power law puts almost the whole field at the faint end, the practical effect was
        /// that the most strongly coloured objects on screen were the ones the eye should have
        /// been reading as colourless — measured off a frame, the faintest stars were rendering
        /// as saturated (26,34,63) blues and (61,41,15) oranges at a luminance of 40 out of 255.
        /// </summary>
        public static Color Colour(Double3 blackBody, float brightness)
        {
            brightness = Mathf.Clamp01(brightness);

            Color hue = CosmoTheme.LinearRgb(blackBody);
            float saturation = 0.30f + brightness * 0.70f;
            Color tinted = CosmoTheme.Desaturate(hue, saturation);

            float magnitude = FaintestBrightness + brightness * (1f - FaintestBrightness);

            // Alpha is 1 and means nothing. Left explicit so nobody reaches for it as a dial again.
            return CosmoTheme.WithAlpha(CosmoTheme.Scaled(tinted, magnitude), 1f);
        }

        /// <summary>
        /// Normalised brightness from an apparent magnitude.
        ///
        /// Anchored on what the eye knows: magnitude 6 is the naked-eye limit and lands at the
        /// faint floor, magnitude -2 is a shade brighter than Sirius and lands at the top. The
        /// scale is compressed and unapologetically so — the Sun at -26.7 and Sirius at -1.5
        /// differ by ten orders of magnitude in flux, and no display expresses that in a dot.
        /// The local star gets its true angular size instead, which is a better answer than a
        /// bigger dot; see <c>CelestialBodyView.UpdateStarPointScale</c>.
        /// </summary>
        /// <remarks>
        /// Retuned when the real catalogue replaced the invented field, against a measurement.
        /// The two populations have completely different magnitude distributions: the procedural
        /// sky drew rank = u^2.2, whose median is 0.22, while the real sky to magnitude 6.5 has a
        /// median star at about magnitude 5.8 — because a real sky is overwhelmingly faint stars,
        /// which is the whole reason the naked-eye limit is where it is. Under the previous
        /// (6 - m) / 8 that median landed at 0.025, i.e. the floor, and the measured frame showed
        /// it: 460 stars detected against the invented sky's 191, but p50 23 against 32 and
        /// nothing at all above 100.
        ///
        /// So the window is narrowed to put the real median back where the tuned invented one
        /// sat. Magnitude 1.5 and brighter now saturates this channel; those stars are told apart
        /// by the halo and spikes instead, which is the correct division of labour — beyond a
        /// point a brighter star is not a bigger dot, it is a dot with more around it.
        /// </remarks>
        public static float BrightnessFromMagnitude(double apparentMagnitude) =>
            Mathf.Clamp01((float)((MagnitudeFaint - apparentMagnitude) /
                                  (MagnitudeFaint - MagnitudeBright)));

        /// <summary>
        /// Apparent magnitude at brightness 0 and at brightness 1: the window above, named.
        ///
        /// <c>CosmoSynth/Point</c> evaluates the same law in its vertex stage from
        /// <c>_MagWindow</c>, so the two must be the same pair of numbers or the star field and
        /// anything the CPU sizes disagree about what a magnitude looks like. That is the defect
        /// this class was written to prevent, one layer out.
        /// </summary>
        public const float MagnitudeFaint = 7.0f;

        /// <inheritdoc cref="MagnitudeFaint"/>
        public const float MagnitudeBright = 1.5f;

        /// <summary>
        /// What a host star's black-body colour is multiplied by before it reaches
        /// <c>_LightColor</c> on every body it lights.
        ///
        /// 0.85, which is what the point light this replaces was finally tuned to: 3.2 blew every
        /// planet out to pure white. It is one of the knobs contract 7.4's re-tune turns after the
        /// switch to Linear, because the numbers it was measured against were gamma-era.
        /// </summary>
        public const float StarLightIntensity = 0.85f;

        /// <summary>The inverse, for a procedural sky that has a rank but no real magnitude.</summary>
        public static float MagnitudeFromBrightness(float brightness) =>
            MagnitudeFaint - Mathf.Clamp01(brightness) * (MagnitudeFaint - MagnitudeBright);
    }
}
