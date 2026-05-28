using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Per-scene fade controller. Place in every scene that needs a transition.
/// On start, optionally fades IN (from black). Call <see cref="LoadScene"/>
/// to fade OUT (to black) and then load the target scene.
/// Each scene has its own instance — no DontDestroyOnLoad required.
/// </summary>
public class SceneTransitionManager : MonoBehaviour
{
    public static SceneTransitionManager Instance { get; private set; }

    [Header("Fade Settings")]
    [SerializeField] private float fadeOutDuration = 0.8f;
    [SerializeField] private float fadeInDuration  = 0.8f;
    [SerializeField] private Color fadeColor       = Color.black;

    [Header("Behaviour")]
    [Tooltip("If true, the scene starts fully black and fades in automatically.")]
    [SerializeField] private bool fadeInOnStart = false;

    [Header("VR Overlay")]
    [Tooltip("Distance from the camera where the fade quad is placed.")]
    [SerializeField] private float canvasDistance = 0.05f;

    [Tooltip("Scale multiplier to guarantee full FOV coverage.")]
    [SerializeField] private float overDrawFactor = 2.5f;

    private Canvas _fadeCanvas;
    private Image  _fadeImage;
    private bool   _isFading;

    private void Awake()
    {
        Instance = this;
        BuildFadeOverlay();
    }

    private void Start()
    {
        if (fadeInOnStart)
            StartCoroutine(FadeInRoutine());
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Fades to black, then loads <paramref name="sceneName"/>.</summary>
    public void LoadScene(string sceneName)
    {
        if (_isFading) return;
        StartCoroutine(FadeOutAndLoad(sceneName));
    }

    // ── Routines ──────────────────────────────────────────────────────────────

    /// <summary>Scene opens fully black, then fades to transparent.</summary>
    private IEnumerator FadeInRoutine()
    {
        _isFading = true;

        AttachToCamera();
        SetAlpha(1f);
        SetOverlayActive(true);

        yield return StartCoroutine(Fade(1f, 0f, fadeInDuration));

        SetOverlayActive(false);
        _isFading = false;
    }

    /// <summary>Fades to black, then loads the target scene normally.</summary>
    private IEnumerator FadeOutAndLoad(string sceneName)
    {
        _isFading = true;

        AttachToCamera();
        SetAlpha(0f);
        SetOverlayActive(true);

        yield return StartCoroutine(Fade(0f, 1f, fadeOutDuration));

        // Scene is fully black — load the next scene.
        // The target scene's own SceneTransitionManager will handle the fade-in.
        SceneManager.LoadScene(sceneName);
    }

    // ── Fade ──────────────────────────────────────────────────────────────────

    private IEnumerator Fade(float from, float to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / duration);
            SetAlpha(Mathf.Lerp(from, to, t));
            elapsed += Time.deltaTime;
            yield return null;
        }

        SetAlpha(to);
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private void SetAlpha(float alpha)
    {
        if (_fadeImage == null) return;
        Color c = _fadeImage.color;
        c.a = alpha;
        _fadeImage.color = c;
    }

    private void SetOverlayActive(bool active)
    {
        if (_fadeCanvas != null)
            _fadeCanvas.gameObject.SetActive(active);
    }

    /// <summary>
    /// Parents the overlay to <c>Camera.main</c> and scales it to cover the FOV.
    /// </summary>
    private void AttachToCamera()
    {
        Camera cam = Camera.main;
        if (cam == null || _fadeCanvas == null) return;

        Transform ct = _fadeCanvas.transform;
        ct.SetParent(cam.transform, false);
        ct.localPosition = Vector3.forward * canvasDistance;
        ct.localRotation = Quaternion.identity;

        float halfH = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) * canvasDistance;
        float halfW = halfH * cam.aspect;
        float size  = Mathf.Max(halfW, halfH) * 2f * overDrawFactor;
        ct.localScale = Vector3.one * (size / 100f);
    }

    // ── Construction ──────────────────────────────────────────────────────────

    private void BuildFadeOverlay()
    {
        GameObject canvasGo = new GameObject("FadeOverlay");
        canvasGo.transform.SetParent(transform, false);

        _fadeCanvas              = canvasGo.AddComponent<Canvas>();
        _fadeCanvas.renderMode   = RenderMode.WorldSpace;
        _fadeCanvas.sortingOrder = 999;

        RectTransform rt = canvasGo.GetComponent<RectTransform>();
        rt.sizeDelta = Vector2.one * 100f;

        GameObject panelGo = new GameObject("FadePanel");
        panelGo.transform.SetParent(canvasGo.transform, false);

        RectTransform panelRt = panelGo.AddComponent<RectTransform>();
        panelRt.anchorMin = Vector2.zero;
        panelRt.anchorMax = Vector2.one;
        panelRt.offsetMin = Vector2.zero;
        panelRt.offsetMax = Vector2.zero;

        _fadeImage               = panelGo.AddComponent<Image>();
        _fadeImage.color         = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
        _fadeImage.raycastTarget = false;

        SetOverlayActive(false);
    }
}
