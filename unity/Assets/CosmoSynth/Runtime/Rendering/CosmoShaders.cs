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
    /// </summary>
    public static class CosmoShaders
    {
        public const string Standard = "Standard";
        public const string UnlitColor = "Unlit/Color";
        public const string UnlitTransparent = "Unlit/Transparent";
        public const string SpritesDefault = "Sprites/Default";
        public const string ParticlesUnlit = "Particles/Standard Unlit";

        /// <summary>Ours, in Runtime/Shaders. Still needs listing — a runtime-built scene
        /// references it no more than it references Unity's own.</summary>
        public const string Atmosphere = "CosmoSynth/Atmosphere";

        /// <summary>Soft selection outline. See CosmoRim.shader.</summary>
        public const string Rim = "CosmoSynth/Rim";

        /// <summary>
        /// What <c>TextMesh</c> draws glyphs with.
        ///
        /// Listed for the same reason as everything else here: every label and panel in the app is
        /// created at runtime, so no asset references this shader and the build step is free to
        /// strip it. The failure would be the whole interface — every panel, every body label —
        /// rendering as pink quads on the device while the editor showed it working perfectly.
        /// </summary>
        public const string Text = "GUI/Text Shader";

        /// <summary>
        /// The complete set. The editor writes exactly this into Always Included Shaders, so
        /// adding a shader to the runtime means adding it here and nowhere else.
        /// </summary>
        public static readonly string[] All =
        {
            Standard,
            UnlitColor,
            UnlitTransparent,
            SpritesDefault,
            ParticlesUnlit,
            Atmosphere,
            Rim,
            Text,
        };

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

        /// <summary>Create a material from a named shader, with the same loud failure.</summary>
        public static Material Material(string name)
        {
            Shader shader = Load(name);
            // Falling back to Standard keeps the object visible and grey rather than magenta, so
            // a missing shader degrades instead of screaming.
            return new Material(shader != null ? shader : Shader.Find(Standard));
        }
    }
}
