using UnityEngine;

/// <summary>
/// Continuously cycles the skybox material's _Tint colour around the HSV hue circle.
/// Attach to any always-active GameObject in the scene (e.g. SpaceEnvironment or MenuController).
/// </summary>
public class SkyboxTintCycler : MonoBehaviour
{
    [Header("Skybox Material")]
    [Tooltip("Leave empty to use RenderSettings.skybox automatically.")]
    [SerializeField] private Material skyboxMaterial;

    [Header("Cycle Settings")]
    [Tooltip("How many seconds it takes to complete one full hue rotation (360°).")]
    [SerializeField] private float cycleDuration = 12f;

    [Tooltip("HSV saturation of the tint colour (0 = grey, 1 = fully saturated).")]
    [SerializeField] [Range(0f, 1f)] private float saturation = 0.6f;

    [Tooltip("HSV value (brightness) of the tint colour.")]
    [SerializeField] [Range(0f, 1f)] private float brightness = 0.5f;

    [Tooltip("Alpha channel of the tint colour.")]
    [SerializeField] [Range(0f, 1f)] private float alpha = 0.5f;

    private static readonly int TintPropertyId = Shader.PropertyToID("_Tint");

    private Material _skyboxInstance;
    private float _hue;

    private void Start()
    {
        ResolveMaterial();
    }

    private void OnDestroy()
    {
        // Restore the original material when this component is destroyed.
        if (_skyboxInstance != null && RenderSettings.skybox == _skyboxInstance)
            RenderSettings.skybox = skyboxMaterial;

        if (_skyboxInstance != null)
            Destroy(_skyboxInstance);
    }

    private void Update()
    {
        if (_skyboxInstance == null) return;

        AdvanceHue();
        ApplyTint();
    }

    // ── Private helpers ──────────────────────────────────────────────────────

    private void ResolveMaterial()
    {
        // Fall back to the scene skybox if no material was explicitly assigned.
        if (skyboxMaterial == null)
            skyboxMaterial = RenderSettings.skybox;

        if (skyboxMaterial == null)
        {
            Debug.LogError("[SkyboxTintCycler] No skybox material found. Assign one in the Inspector or set it via RenderSettings.");
            enabled = false;
            return;
        }

        // Work on an instance so the original asset is never modified at runtime.
        _skyboxInstance = new Material(skyboxMaterial);
        _skyboxInstance.name = skyboxMaterial.name + " (Runtime Instance)";
        RenderSettings.skybox = _skyboxInstance;

        // Start from the current tint's hue so there is no sudden colour jump.
        Color currentTint = _skyboxInstance.GetColor(TintPropertyId);
        Color.RGBToHSV(currentTint, out _hue, out _, out _);
    }

    private void AdvanceHue()
    {
        if (cycleDuration <= 0f) return;
        _hue = (_hue + Time.deltaTime / cycleDuration) % 1f;
    }

    private void ApplyTint()
    {
        Color tint = Color.HSVToRGB(_hue, saturation, brightness);
        tint.a = alpha;
        _skyboxInstance.SetColor(TintPropertyId, tint);
    }
}
