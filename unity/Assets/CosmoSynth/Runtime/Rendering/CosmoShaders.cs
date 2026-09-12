using System;
using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// Every shader the runtime looks up by name, in one place.
    ///
    /// This exists because of a failure that is invisible in the editor and total on the device.
    /// CosmoSynth builds its entire scene at runtime, so no asset in the project references any
    /// of these shaders — and Unity's build step strips shaders nothing references. In the
    /// editor every shader is always loaded, so it all works; in the player <c>Shader.Find</c>
    /// returns null, the material falls back to the error shader, and every single object
    /// renders magenta.
    ///
    /// Two things prevent it recurring. The names live here rather than scattered through
    /// twenty <c>Shader.Find</c> calls, so the editor's always-included list is generated from
    /// the same array the runtime uses and cannot drift from it. And <see cref="Load"/> fails
    /// loudly rather than handing back a null that silently becomes pink.
    ///
    /// <see cref="All"/> is the single source of truth for what the build must include. Adding a
    /// shader to the runtime means adding it here and nowhere else; a shader file that is not in
    /// this array is not in the player, whatever else references it.
    /// </summary>
    public static class CosmoShaders
    {
        // ---- The eleven. Contract 7.2 and Appendix A.1. --------------------------------------
        // All are single-variant URP HLSL under Runtime/Shaders, sharing CosmoCore.hlsl. Queues
        // are the contract's 2.2 table and are declared in the shader, not set here.

        /// <summary>Opaque lit body: planets, moons, spacecraft. Queue 2000.</summary>
        public const string Body = "CosmoSynth/Body";

        /// <summary>Additive point-sprite billboards: stars, galaxies, markers. Queue 3050
        /// (a material in sky mode sets 1010).</summary>
        public const string Point = "CosmoSynth/Point";

        /// <summary>Screen-constant-width anti-aliased line: orbits, figures, grids. Queue 3060.</summary>
        public const string Ribbon = "CosmoSynth/Ribbon";

        /// <summary>Inside-out eye-anchored sky sphere: the CMB. Queue 1000.</summary>
        public const string Sky = "CosmoSynth/Sky";

        /// <summary>Bounded raymarch volume: the Milky Way dust cube. Queue 3010.</summary>
        public const string Volume = "CosmoSynth/Volume";

        /// <summary>Analytic single-scattering atmosphere. Queue 3100. See CosmoAtmosphere.shader.</summary>
        public const string Atmosphere = "CosmoSynth/Atmosphere";

        /// <summary>Soft selection outline. Queue 3100. See CosmoRim.shader.</summary>
        public const string Rim = "CosmoSynth/Rim";

        /// <summary>Planetary rings, premultiplied over with an analytic planet shadow. Queue 3000.</summary>
        public const string Ring = "CosmoSynth/Ring";

        /// <summary>Flat opaque colour: beacon bars, the pointer ray. Queue 2000.</summary>
        public const string Unlit = "CosmoSynth/Unlit";

        /// <summary>Panel backing (queue 3200) and accent (the accent material sets 3201).</summary>
        public const string Panel = "CosmoSynth/Panel";

        /// <summary>
        /// What <c>TextMesh</c> draws glyphs with. Queue 3210.
        ///
        /// Listed for the same reason as everything else here: every label and panel in the app is
        /// created at runtime, so no asset references this shader and the build step is free to
        /// strip it. The failure would be the whole interface — every panel, every body label —
        /// rendering as pink quads on the device while the editor showed it working perfectly.
        /// </summary>
        public const string Text = "CosmoSynth/Text";

        /// <summary>
        /// The complete set. The editor writes exactly this into Always Included Shaders, so
        /// adding a shader to the runtime means adding it here and nowhere else.
        ///
        /// These are the only shader names the runtime may ask for. In particular no
        /// <c>Universal Render Pipeline/*</c> shader may be referenced at runtime (gate G0d):
        /// URP's own shaders are multi-variant, and a runtime-built scene strips the variants it
        /// never referenced, which is the keyword half of the same magenta failure.
        /// </summary>
        public static readonly string[] All =
        {
            Body,
            Point,
            Ribbon,
            Sky,
            Volume,
            Atmosphere,
            Rim,
            Ring,
            Unlit,
            Panel,
            Text,
        };

        // ---- Built-in names, on their way out. ------------------------------------------------
        //
        // These are NOT in All, so they are not in the player and Shader.Find returns null for
        // every one of them in a build. They are kept only so the seven call sites that still ask
        // for them compile while their own lanes migrate. The [Obsolete] attribute is the point:
        // it makes the compiler print the migration list rather than leaving it in a report to go
        // stale. Delete each constant as its last caller moves, and this block with the last one.

        [Obsolete("Use CosmoShaders.Body. Not in All: Shader.Find returns null in a player build.")]
        public const string Standard = "Standard";

        [Obsolete("Use CosmoShaders.Unlit. Not in All: Shader.Find returns null in a player build.")]
        public const string UnlitColor = "Unlit/Color";

        [Obsolete("Use CosmoShaders.Panel. Not in All: Shader.Find returns null in a player build.")]
        public const string UnlitTransparent = "Unlit/Transparent";

        [Obsolete("Use CosmoShaders.Panel or .Ribbon. Not in All: Shader.Find returns null in a player build.")]
        public const string SpritesDefault = "Sprites/Default";

        [Obsolete("Use CosmoShaders.Point. Not in All: Shader.Find returns null in a player build.")]
        public const string ParticlesUnlit = "Particles/Standard Unlit";

        [Obsolete("Use CosmoShaders.Text. Not in All: Shader.Find returns null in a player build.")]
        public const string GuiText = "GUI/Text Shader";

        /// <summary>
        /// Look up a shader, complaining loudly if it is missing.
        ///
        /// Returning null here would produce Unity's magenta error material with no message at
        /// all — the single most confusing failure mode in a headset, where there is no console
        /// to check. A log line naming the shader turns "everything is pink" into a one-line fix.
        /// </summary>
        public static Shader Load(string name)
        {
            Shader shader = Shader.Find(name);
            if (shader == null)
            {
                Debug.LogError(
                    $"[CosmoSynth] Shader '{name}' is not in the build — everything using it will " +
                    "render magenta. It must be listed in CosmoShaders.All and written into " +
                    "Project Settings > Graphics > Always Included Shaders by " +
                    "CosmoSynth.EditorTools.XrSetup.");
            }
            return shader;
        }

        /// <summary>
        /// Create a material from a named shader, with the same loud failure.
        ///
        /// <b>Returns null when even the fallback is missing.</b> Callers must handle that — skip
        /// the renderer, leave the component inert — rather than being handed a Material wrapping
        /// a null shader. <c>new Material(null)</c> throws from inside Unity, and it throws inside
        /// whichever <c>Awake</c> was building its material, taking that component down with it.
        /// That is not degrading; it is the same total, unexplained failure this class exists to
        /// replace, with two error lines in front of it. A null return degrades: the object does
        /// not draw, the log says which shader is missing, and the rest of the scene still runs.
        /// </summary>
        public static Material Material(string name)
        {
            Shader shader = Load(name);
            // Falling back to Unlit keeps the object visible and flat rather than magenta, so a
            // missing shader degrades instead of screaming. It used to fall back to Standard,
            // which under URP is itself missing — the fallback had the same failure as the thing
            // it was catching.
            if (shader == null && name != Unlit) shader = Load(Unlit);
            if (shader == null)
            {
                Debug.LogError(
                    "[CosmoSynth] No material for '" + name + "' and none for the " + Unlit +
                    " fallback either — the CosmoSynth shader set is not in this build at all. " +
                    "Nothing that asked for a material will draw. Run " +
                    "CosmoSynth.EditorTools.XrSetup and rebuild.");
                return null;
            }
            return new Material(shader);
        }
    }
}
