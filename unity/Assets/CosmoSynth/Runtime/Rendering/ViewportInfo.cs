using UnityEngine;

namespace CosmoSynth.Runtime
{
    /// <summary>
    /// How tall the thing being rendered into actually is, in pixels.
    ///
    /// This is the denominator of every apparent-size calculation in the project — LOD selection,
    /// the minimum-visible-size floor, orbit line width — so a wrong answer does not produce a
    /// slightly wrong picture, it produces a broken one. Two ways it goes wrong:
    ///
    /// In XR the camera renders into swapchain textures provided by the runtime rather than into
    /// the screen, and <c>Camera.pixelHeight</c> can report the desktop mirror's height, or
    /// something small, or zero. Too small a value demotes every body to a sub-pixel point and
    /// blanks the scene while passthrough carries on compositing normally.
    ///
    /// In batch mode there is no Game view, and it reports a default that has nothing to do with
    /// the render target — which is how orbit lines ended up drawn as centimetre-thick slabs
    /// sweeping across the view, each one correctly sized for a viewport that did not exist.
    /// </summary>
    public static class ViewportInfo
    {
        /// <summary>Vertical resolution the camera is really rendering at.</summary>
        public static int VerticalPixels(Camera camera)
        {
            if (UnityEngine.XR.XRSettings.isDeviceActive && UnityEngine.XR.XRSettings.eyeTextureHeight > 0)
                return UnityEngine.XR.XRSettings.eyeTextureHeight;

            if (camera != null)
            {
                // A render texture is authoritative: it is exactly what is being drawn into.
                if (camera.targetTexture != null) return camera.targetTexture.height;
                if (camera.pixelHeight > 1) return camera.pixelHeight;
            }

            // Last resort: a sane default beats a zero that blanks the scene.
            return Mathf.Max(Screen.height, 1080);
        }

        /// <summary>World-space size of one pixel at a given distance from the camera.</summary>
        public static float WorldPerPixel(Camera camera, float distance)
        {
            if (camera == null) return 0f;
            return 2f * distance * Mathf.Tan(camera.fieldOfView * 0.5f * Mathf.Deg2Rad)
                   / Mathf.Max(1, VerticalPixels(camera));
        }
    }
}
