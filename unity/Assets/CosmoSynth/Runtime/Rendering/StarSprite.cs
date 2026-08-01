using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// The point-spread function every star in the scene is drawn with.
    ///
    /// Stars are drawn as quads, and a quad with no texture is a SQUARE — Unity's default
    /// _MainTex is a solid white 4x4, so the whole sky came out as a field of hard-edged
    /// coloured tiles. Nothing about the geometry was wrong; there was simply nothing telling
    /// the quad to be round.
    ///
    /// The profile is a Gaussian rather than a disc or a linear ramp, because that is roughly
    /// what a real point source does through an aperture and, more practically, because it is
    /// the only shape with no edge at all: a disc has a visible rim once it covers more than a
    /// few pixels, and a linear ramp has a discontinuity in its slope at the centre that reads
    /// as a faint cross. A small central core is added on top so bright stars keep a definite
    /// point instead of dissolving into a smudge.
    ///
    /// One texture, shared by every star in the app — the procedural background field and the
    /// real catalogue stars on the sky sphere — so they cannot drift apart in appearance.
    /// </summary>
    public static class StarSprite
    {
        /// <summary>
        /// Texture edge, pixels. Small on purpose — see <see cref="Texture"/>.
        /// </summary>
        private const int Resolution = 32;

        private static Texture2D _texture;

        /// <summary>The shared soft dot. Created on first use and kept for the session.</summary>
        public static Texture2D Texture
        {
            get
            {
                if (_texture != null) return _texture;

                _texture = new Texture2D(Resolution, Resolution, TextureFormat.RGBA32,
                    mipChain: true, linear: false)
                {
                    name = "StarSprite",
                    // Clamp, not repeat: a repeating soft dot bleeds neighbouring copies into the
                    // edge of every quad and puts a faint grid over the sky.
                    wrapMode = TextureWrapMode.Clamp,

                    // Trilinear, and biased toward the sharper mip.
                    //
                    // This is the other half of "the stars look square", and it is not a property
                    // of the profile at all. A star's quad is a few pixels across, so a 64-pixel
                    // texture lands four or five mip levels down — a 4x4 or 2x2 image — and a 4x4
                    // image magnified back up under bilinear filtering *is* a visible grid of
                    // blocks. The Gaussian was fine; it was being sampled from a version of
                    // itself that no longer contained one. A small top level and a negative bias
                    // keep the sampled mip close to the size actually being drawn, and trilinear
                    // stops the level change popping as a star drifts across the view.
                    filterMode = FilterMode.Trilinear,
                    mipMapBias = -0.75f
                };

                var pixels = new Color32[Resolution * Resolution];
                const float centre = (Resolution - 1) * 0.5f;

                for (int y = 0; y < Resolution; y++)
                {
                    for (int x = 0; x < Resolution; x++)
                    {
                        // Normalised radius: 1.0 at the edge of the inscribed circle.
                        float dx = (x - centre) / centre;
                        float dy = (y - centre) / centre;
                        float r = Mathf.Sqrt(dx * dx + dy * dy);

                        // Broad Gaussian halo plus a tight core, WEIGHTED TO SUM TO EXACTLY ONE.
                        //
                        // The weights are the whole point. 0.55 + 0.85 is 1.40, and clamping that
                        // to 1 does not scale the profile — it flattens the top of it. The centre
                        // fourteen per cent of the radius all came out at the same maximum, which
                        // is a disc, and a disc a few pixels across is a square. That was the
                        // "square patches" complaint surviving the fix that was supposed to cure
                        // it: the dot had been given a round falloff and a flat top at the same
                        // time, so the falloff was real and the part you actually looked at was
                        // not. Weights that sum to one give a genuine peak and a monotonic
                        // decrease, which is what a point spread function is.
                        //
                        // The widths are set by how few pixels this has to work in, not by optics,
                        // and the first attempt set them too narrow. Half maximum was at 0.22 of
                        // the quad radius, which on a four-pixel quad is a core 0.9 pixels wide —
                        // NARROWER THAN A PIXEL. A profile finer than the sampling grid does not
                        // render as a fine profile, it renders as whatever the grid happens to
                        // catch on its flanks, so the peak is simply thrown away. It showed up as
                        // a rendered brightness that tracked the star's WIDTH rather than the
                        // brightness it had been given: across a captured frame the correlation
                        // between the two was 0.80, the median two-pixel star peaked at 11 out of
                        // 255, and the one five-pixel star reached 72. No amount of colour could
                        // have fixed that, because the colour was never being sampled.
                        //
                        // Half maximum now falls at a third of the quad radius, so the core is
                        // about a third of the quad's full width and stays above a pixel across
                        // at every size a star is actually drawn.
                        float halo = Mathf.Exp(-r * r * 2.6f);
                        float core = Mathf.Exp(-r * r * 9.0f);
                        float intensity = halo * 0.34f + core * 0.66f;

                        // Force it to zero by the edge of the quad, so the corners cannot show
                        // even a faint residue of the square they are cut from.
                        intensity *= Mathf.Clamp01(1f - Mathf.SmoothStep(0.70f, 1f, r));

                        byte level = (byte)(intensity * 255f);
                        pixels[y * Resolution + x] = new Color32(level, level, level, level);
                    }
                }

                _texture.SetPixels32(pixels);
                _texture.Apply(updateMipmaps: true, makeNoLongerReadable: true);
                return _texture;
            }
        }

        /// <summary>
        /// An additive material that draws the dot.
        ///
        /// <b>Alpha is not a brightness dial here and must never be used as one.</b> The blend is
        /// additive, which by construction consumes colour and ignores coverage — and on top of
        /// that, <c>Particles/Standard Unlit</c> in additive mode does not modulate by the tint's
        /// alpha at all, which this project has already found once and written down in
        /// <c>BodySelector.BuildHighlight</c>. The star field found it a second time the hard
        /// way: magnitude was moved into vertex alpha, every star in the sky then rendered at the
        /// same 25% peak whatever its magnitude, and the quads were enlarged to compensate for a
        /// signal that had simply been disconnected. Brightness goes in the RGB; see
        /// <see cref="StarLook"/>.
        ///
        /// <b>Destination alpha is a passthrough hazard and is not verified here.</b> Setting
        /// <c>_SrcBlend</c> and <c>_DstBlend</c> configures the colour blend only; this shader
        /// exposes no separate alpha blend, so whether stars accumulate into the framebuffer's
        /// alpha depends on whether its additive pass happens to declare <c>ColorMask RGB</c>.
        /// On Quest the compositor reads alpha as coverage, so if it does not, every star stamps
        /// an opaque hole into the room. That cannot be observed in the flat fallback, which
        /// clears alpha to 1 everywhere — it needs checking on the device, and a purpose-built
        /// star shader would let it be declared rather than hoped for.
        /// </summary>
        public static Material CreateMaterial()
        {
            Material material = CosmoShaders.Material(CosmoShaders.ParticlesUnlit);
            material.SetFloat("_Mode", 4f);                       // additive
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_ALPHABLEND_ON");
            material.mainTexture = Texture;
            return material;
        }
    }
}
