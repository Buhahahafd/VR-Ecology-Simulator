using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Editor utility that configures the Global Volume Profile for a NMS loading screen look:
/// Bloom, Vignette, Color Adjustments (purple/magenta grade), Tonemapping.
/// </summary>
public static class NMSPostProcessSetup
{
    private const string ProfilePath = "Assets/Scenes/Vr/Global Volume Profile.asset";

    [MenuItem("Tools/NMS Setup/Apply NMS Loading Screen Post-Process")]
    public static void Apply()
    {
        VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        if (profile == null)
        {
            Debug.LogError($"[NMSPostProcess] Profile not found at: {ProfilePath}");
            return;
        }

        SetupBloom(profile);
        SetupColorAdjustments(profile);
        SetupTonemapping(profile);
        SetupVignette(profile);

        EditorUtility.SetDirty(profile);
        AssetDatabase.SaveAssets();
        Debug.Log("[NMSPostProcess] NMS Loading Screen post-process applied.");
    }

    // ── Bloom ─────────────────────────────────────────────────────────────────

    private static void SetupBloom(VolumeProfile profile)
    {
        if (!profile.TryGet<Bloom>(out var bloom))
            bloom = profile.Add<Bloom>(false);

        bloom.active = true;

        // Threshold: только самые яркие объекты (звёзды) цветут
        bloom.threshold.Override(0.85f);

        // Умеренная интенсивность — свечение есть, но не перебивает темноту
        bloom.intensity.Override(3.5f);

        // Широкий scatter — мягкое размытие, а не резкий ореол
        bloom.scatter.Override(0.78f);

        // Розово-фиолетовый тинт — характерный тон NMS
        bloom.tint.Override(new Color(0.95f, 0.80f, 1.0f, 1f));

        bloom.highQualityFiltering.Override(true);
    }

    // ── Color Adjustments ─────────────────────────────────────────────────────

    private static void SetupColorAdjustments(VolumeProfile profile)
    {
        if (!profile.TryGet<ColorAdjustments>(out var ca))
            ca = profile.Add<ColorAdjustments>(false);

        ca.active = true;

        // Экспозиция: чуть темнее базовой — космос должен быть тёмным
        ca.postExposure.Override(-0.4f);

        // Контраст: усиливает разницу между тёмным фоном и яркими звёздами
        ca.contrast.Override(22f);

        // Color Filter: лёгкий розово-лавандовый общий тон
        ca.colorFilter.Override(new Color(0.95f, 0.88f, 1.0f, 1f));

        // Hue Shift: нет — цвета звёзд должны сохраниться
        ca.hueShift.Override(0f);

        // Насыщенность: +15 — чуть живее чем реальный космос
        ca.saturation.Override(15f);
    }

    // ── Tonemapping ───────────────────────────────────────────────────────────

    private static void SetupTonemapping(VolumeProfile profile)
    {
        if (!profile.TryGet<Tonemapping>(out var tm))
            tm = profile.Add<Tonemapping>(false);

        tm.active = true;

        // ACES: сохраняет HDR highlights (яркие звёзды), мягко компрессирует
        tm.mode.Override(TonemappingMode.ACES);
    }

    // ── Vignette ──────────────────────────────────────────────────────────────

    private static void SetupVignette(VolumeProfile profile)
    {
        if (!profile.TryGet<Vignette>(out var vignette))
            vignette = profile.Add<Vignette>(false);

        vignette.active = true;

        // Цвет виньетки — чистый чёрный
        vignette.color.Override(new Color(0f, 0f, 0f, 1f));

        // Центр: немного смещён вниз как в NMS (взгляд чуть выше центра)
        vignette.center.Override(new Vector2(0.5f, 0.52f));

        // Интенсивность: заметная, но не агрессивная
        vignette.intensity.Override(0.42f);

        // Мягкость перехода
        vignette.smoothness.Override(0.55f);

        // Не округлая (rounded = false): растянута по горизонтали — кино-формат
        vignette.rounded.Override(false);
    }
}
