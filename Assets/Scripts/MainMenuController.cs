using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controls the main menu intro animation sequence:
/// 1. Title "КОЛЬЦО" fades in with a scale effect.
/// 2. Pause on the title.
/// 3. Menu buttons slide up and fade in.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Title")]
    [SerializeField] private CanvasGroup titleCanvasGroup;
    [SerializeField] private RectTransform titleRectTransform;

    [Header("Menu Panel")]
    [SerializeField] private CanvasGroup menuCanvasGroup;
    [SerializeField] private RectTransform menuRectTransform;

    [Header("Buttons")]
    [SerializeField] private Button playButton;
    [SerializeField] private Button settingsButton;
    [SerializeField] private Button quitButton;

    [Header("Settings Panel")]
    [SerializeField] private CanvasGroup settingsCanvasGroup;

    [Header("Scene Names")]
    [SerializeField] private string gameSceneName = "Vr";

    [Header("Music")]
    [SerializeField] private AudioClip menuMusicClip;
    [SerializeField] private float musicFadeInDuration = 2.5f;
    [SerializeField] [Range(0f, 1f)] private float musicTargetVolume = 0.5f;

    [Header("Animation Settings")]
    [SerializeField] private float titleFadeInDuration = 1.8f;
    [SerializeField] private float titleHoldDuration = 1.2f;
    [SerializeField] private float menuFadeInDuration = 1.0f;
    [SerializeField] private float titleScaleFrom = 0.75f;
    [SerializeField] private float menuSlideOffsetY = 80f;

    private Vector2 _menuStartPos;
    private AudioSource _audioSource;

    private void Awake()
    {
        InitialiseState();
        InitialiseAudio();
    }

    private void Start()
    {
        playButton.onClick.AddListener(OnPlayClicked);
        settingsButton.onClick.AddListener(OnSettingsClicked);
        quitButton.onClick.AddListener(OnQuitClicked);

        StartCoroutine(PlayIntroSequence());
        StartCoroutine(FadeMusicIn());
    }

    // ── Initialisation ──────────────────────────────────────────────────────

    private void InitialiseState()
    {
        titleCanvasGroup.alpha = 0f;
        titleCanvasGroup.interactable = false;
        titleRectTransform.localScale = new Vector3(titleScaleFrom, titleScaleFrom, 1f);

        menuCanvasGroup.alpha = 0f;
        menuCanvasGroup.interactable = false;
        menuCanvasGroup.blocksRaycasts = false;

        // Push menu down so it can slide up into position
        _menuStartPos = menuRectTransform.anchoredPosition;
        menuRectTransform.anchoredPosition = new Vector2(
            _menuStartPos.x,
            _menuStartPos.y - menuSlideOffsetY);

        if (settingsCanvasGroup != null)
        {
            settingsCanvasGroup.alpha = 0f;
            settingsCanvasGroup.interactable = false;
            settingsCanvasGroup.blocksRaycasts = false;
        }
    }

    private void InitialiseAudio()
    {
        if (menuMusicClip == null) return;

        _audioSource = gameObject.AddComponent<AudioSource>();
        _audioSource.clip = menuMusicClip;
        _audioSource.loop = true;
        _audioSource.spatialBlend = 0f;
        _audioSource.volume = 0f;
        _audioSource.playOnAwake = false;
    }

    // ── Intro Sequence ───────────────────────────────────────────────────────

    private IEnumerator PlayIntroSequence()
    {
        yield return StartCoroutine(AnimateTitle());
        yield return new WaitForSeconds(titleHoldDuration);
        yield return StartCoroutine(AnimateMenu());
    }

    private IEnumerator FadeMusicIn()
    {
        if (_audioSource == null) yield break;

        _audioSource.Play();

        float elapsed = 0f;
        while (elapsed < musicFadeInDuration)
        {
            _audioSource.volume = Mathf.Lerp(0f, musicTargetVolume, Mathf.SmoothStep(0f, 1f, elapsed / musicFadeInDuration));
            elapsed += Time.deltaTime;
            yield return null;
        }

        _audioSource.volume = musicTargetVolume;
    }

    private IEnumerator AnimateTitle()
    {
        Vector3 startScale = new Vector3(titleScaleFrom, titleScaleFrom, 1f);
        float elapsed = 0f;

        while (elapsed < titleFadeInDuration)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / titleFadeInDuration);
            titleCanvasGroup.alpha = t;
            titleRectTransform.localScale = Vector3.Lerp(startScale, Vector3.one, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        titleCanvasGroup.alpha = 1f;
        titleRectTransform.localScale = Vector3.one;
    }

    private IEnumerator AnimateMenu()
    {
        Vector2 startPos = menuRectTransform.anchoredPosition;
        float elapsed = 0f;

        while (elapsed < menuFadeInDuration)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / menuFadeInDuration);
            menuCanvasGroup.alpha = t;
            menuRectTransform.anchoredPosition = Vector2.Lerp(startPos, _menuStartPos, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        menuCanvasGroup.alpha = 1f;
        menuRectTransform.anchoredPosition = _menuStartPos;
        menuCanvasGroup.interactable = true;
        menuCanvasGroup.blocksRaycasts = true;
    }

    // ── Button Handlers ──────────────────────────────────────────────────────

    /// <summary>Loads the main game scene.</summary>
    public void OnPlayClicked()
    {
        SceneManager.LoadScene(gameSceneName);
    }

    /// <summary>Toggles the settings panel visibility with a fade.</summary>
    public void OnSettingsClicked()
    {
        if (settingsCanvasGroup == null) return;

        bool isVisible = settingsCanvasGroup.alpha > 0.5f;
        float target = isVisible ? 0f : 1f;
        StopCoroutine(nameof(FadeCanvasGroup));
        StartCoroutine(FadeCanvasGroup(settingsCanvasGroup, target, 0.4f));
        settingsCanvasGroup.interactable = !isVisible;
        settingsCanvasGroup.blocksRaycasts = !isVisible;
    }

    /// <summary>Exits the application or stops Play mode in the editor.</summary>
    public void OnQuitClicked()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    // ── Utility ──────────────────────────────────────────────────────────────

    private IEnumerator FadeCanvasGroup(CanvasGroup group, float targetAlpha, float duration)
    {
        float startAlpha = group.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            group.alpha = Mathf.Lerp(startAlpha, targetAlpha, Mathf.SmoothStep(0f, 1f, elapsed / duration));
            elapsed += Time.deltaTime;
            yield return null;
        }

        group.alpha = targetAlpha;
    }
}
