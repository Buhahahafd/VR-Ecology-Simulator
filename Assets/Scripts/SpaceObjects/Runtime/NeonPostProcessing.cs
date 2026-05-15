using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SpaceDebris
{
    /// <summary>
    /// Configures the Global Volume's post-processing parameters at runtime
    /// to match the cyberpunk/synthwave visual style:
    ///   - Bloom:              cyan-tinted, high intensity, feeds neon glow.
    ///   - Vignette:           dark edges focus the eye on the centre.
    ///   - Color Adjustments:  slight contrast boost, cool temperature.
    ///   - Chromatic Aberration: subtle fringe for a retro-digital feel.
    ///
    /// Attach to any active GameObject in the scene.
    /// Requires the URP "Post Processing" feature to be enabled in the
    /// renderer and "Post Processing" toggle on the Main Camera.
    /// </summary>
    public class NeonPostProcessing : MonoBehaviour
    {
        [Header("Bloom")]
        [SerializeField] private float bloomIntensity  = 1.2f;
        [SerializeField] private float bloomThreshold  = 0.9f;
        [SerializeField] private float bloomScatter    = 0.5f;
        [SerializeField] private Color bloomTint       = new Color(0.88f, 0.94f, 1.0f, 1f);

        [Header("Vignette")]
        [SerializeField] private float vignetteIntensity  = 0.28f;
        [SerializeField] private float vignetteSmoothness = 0.5f;

        [Header("Color Adjustments")]
        [SerializeField] private float contrast    =  8f;
        [SerializeField] private float saturation  =  5f;
        [SerializeField] private Color colorFilter = new Color(0.92f, 0.96f, 1.0f, 1f);

        [Header("Chromatic Aberration")]
        [SerializeField] [Range(0f, 1f)] private float chromaticIntensity = 0.03f;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Start()
        {
            Volume vol = FindFirstObjectByType<Volume>();
            if (vol == null)
            {
                Debug.LogWarning("[NeonPostProcessing] No Global Volume found in scene.");
                return;
            }

            VolumeProfile profile = vol.sharedProfile;
            if (profile == null)
            {
                Debug.LogWarning("[NeonPostProcessing] Global Volume has no shared profile.");
                return;
            }

            ApplyBloom(profile);
            ApplyVignette(profile);
            ApplyColorAdjustments(profile);
            ApplyChromaticAberration(profile);
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void ApplyBloom(VolumeProfile profile)
        {
            if (!profile.TryGet<Bloom>(out Bloom bloom))
                bloom = profile.Add<Bloom>(overrides: true);

            bloom.active = true;
            bloom.intensity.Override(bloomIntensity);
            bloom.threshold.Override(bloomThreshold);
            bloom.scatter.Override(bloomScatter);
            bloom.tint.Override(bloomTint);
            bloom.highQualityFiltering.Override(true);
        }

        private void ApplyVignette(VolumeProfile profile)
        {
            if (!profile.TryGet<Vignette>(out Vignette vignette))
                vignette = profile.Add<Vignette>(overrides: true);

            vignette.active = true;
            vignette.color.Override(Color.black);
            vignette.intensity.Override(vignetteIntensity);
            vignette.smoothness.Override(vignetteSmoothness);
        }

        private void ApplyColorAdjustments(VolumeProfile profile)
        {
            if (!profile.TryGet<ColorAdjustments>(out ColorAdjustments ca))
                ca = profile.Add<ColorAdjustments>(overrides: true);

            ca.active = true;
            ca.contrast.Override(contrast);
            ca.saturation.Override(saturation);
            ca.colorFilter.Override(colorFilter);
        }

        private void ApplyChromaticAberration(VolumeProfile profile)
        {
            if (!profile.TryGet<ChromaticAberration>(out ChromaticAberration chroma))
                chroma = profile.Add<ChromaticAberration>(overrides: true);

            chroma.active = true;
            chroma.intensity.Override(chromaticIntensity);
        }
    }
}
