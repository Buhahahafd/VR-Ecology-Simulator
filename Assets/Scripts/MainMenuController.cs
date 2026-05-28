using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Controls the main menu intro sequence:
/// 1. Star object animates from y=-4000 to y=0 with ease-out curve (fast start, gentle landing).
/// 2. Menu canvas fades in as a world-space object fixed relative to XR Origin.
/// 3. After a hold delay the title fades in, then buttons appear sequentially.
/// </summary>
public class MainMenuController : MonoBehaviour
{
    [Header("Star Intro Animation")]
    [SerializeField] private Transform starTransform;
    [SerializeField] private float starStartY = -4000f;
    [SerializeField] private float starTargetY = 0f;
    [SerializeField] private float starIntroDuration = 5f;

    [Tooltip("Controls the star's motion curve: x = normalized time, y = normalized position (0-1). " +
             "Use a curve that starts steep and flattens toward the end for an ease-out feel.")]
    [SerializeField] private AnimationCurve starEaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [SerializeField] private float holdBeforeUIDelay = 1.5f;

    [Header("World-Space Menu Canvas")]
    [Tooltip("The world-space Canvas parented to XR Origin — stays at a fixed position in the player's space.")]
    [SerializeField] private GameObject menuCanvasObject;

    [Tooltip("The CanvasGroup on the root Canvas for fade-in.")]
    [SerializeField] private CanvasGroup menuCanvasGroup;

    [Tooltip("Duration of the menu canvas fade-in after the star lands.")]
    [SerializeField] private float menuFadeInDuration = 1.0f;

    [Header("Title")]
    [SerializeField] private CanvasGroup titleCanvasGroup;
    [SerializeField] private RectTransform titleRectTransform;

    [Header("Buttons")]
    [SerializeField] private CanvasGroup playButtonGroup;
    [SerializeField] private CanvasGroup quitButtonGroup;
    [SerializeField] private Button playButton;
    [SerializeField] private Button quitButton;

    [Header("VR Ray Input")]
    [Tooltip("VRMenuRayInput on the MenuController — registers buttons for ray interaction.")]
    [SerializeField] private VRMenuRayInput vrRayInput;

    [Header("Scene Names")]
    [SerializeField] private string gameSceneName = "Game";

    [Header("Music")]
    [SerializeField] private AudioClip menuMusicClip;
    [SerializeField] private float musicFadeInDuration = 2.5f;
    [SerializeField] [Range(0f, 1f)] private float musicTargetVolume = 0.5f;

    [Header("Animation Settings")]
    [SerializeField] private float titleFadeInDuration = 1.8f;
    [SerializeField] private float titleHoldDuration = 1.5f;
    [SerializeField] private float buttonSequenceDelay = 0.4f;
    [SerializeField] private float buttonFadeInDuration = 0.8f;
    [SerializeField] private float titleScaleFrom = 0.8f;

    private AudioSource _audioSource;

    private void Awake()
    {
        InitialiseState();
        InitialiseAudio();
    }

    private void Start()
    {
        if (playButton != null) playButton.onClick.AddListener(OnPlayClicked);
        if (quitButton != null) quitButton.onClick.AddListener(OnQuitClicked);

        if (vrRayInput != null)
        {
            if (playButton != null) vrRayInput.RegisterButton(playButton);
            if (quitButton != null) vrRayInput.RegisterButton(quitButton);
        }

        StartCoroutine(RunIntroSequence());
        StartCoroutine(FadeMusicIn());
    }

    // ── Initialisation ───────────────────────────────────────────────────────

    private void InitialiseState()
    {
        if (starTransform != null)
        {
            Vector3 pos = starTransform.position;
            starTransform.position = new Vector3(pos.x, starStartY, pos.z);
        }

        // Canvas is fixed in space — just hide it until the intro ends
        if (menuCanvasObject != null)
            menuCanvasObject.SetActive(false);

        if (menuCanvasGroup != null)
        {
            menuCanvasGroup.alpha          = 0f;
            menuCanvasGroup.interactable   = false;
            menuCanvasGroup.blocksRaycasts = false;
        }

        if (titleCanvasGroup != null)
        {
            titleCanvasGroup.alpha          = 0f;
            titleCanvasGroup.interactable   = false;
            titleCanvasGroup.blocksRaycasts = false;
        }

        if (titleRectTransform != null)
            titleRectTransform.localScale = new Vector3(titleScaleFrom, titleScaleFrom, 1f);

        if (playButtonGroup != null) SetButtonGroupState(playButtonGroup, 0f, false);
        if (quitButtonGroup != null) SetButtonGroupState(quitButtonGroup, 0f, false);
    }

    private void InitialiseAudio()
    {
        if (menuMusicClip == null) return;

        _audioSource             = gameObject.AddComponent<AudioSource>();
        _audioSource.clip        = menuMusicClip;
        _audioSource.loop        = true;
        _audioSource.spatialBlend = 0f;
        _audioSource.volume      = 0f;
        _audioSource.playOnAwake = false;
    }

    // ── Intro Sequence ────────────────────────────────────────────────────────

    private IEnumerator RunIntroSequence()
    {
        yield return StartCoroutine(AnimateStar());
        yield return new WaitForSeconds(holdBeforeUIDelay);
        yield return StartCoroutine(FadeInMenuCanvas());
        yield return StartCoroutine(AnimateTitle());
        yield return new WaitForSeconds(titleHoldDuration);
        yield return StartCoroutine(RevealButton(playButtonGroup));
        yield return new WaitForSeconds(buttonSequenceDelay);
        yield return StartCoroutine(RevealButton(quitButtonGroup));
    }

    private IEnumerator AnimateStar()
    {
        if (starTransform == null) yield break;

        Vector3 startPos  = starTransform.position;
        Vector3 targetPos = new Vector3(startPos.x, starTargetY, startPos.z);
        float   elapsed   = 0f;

        if (starEaseCurve == null || starEaseCurve.length == 0)
            starEaseCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

        while (elapsed < starIntroDuration)
        {
            float normalizedTime = elapsed / starIntroDuration;
            float curveValue     = starEaseCurve.Evaluate(normalizedTime);
            starTransform.position = Vector3.LerpUnclamped(startPos, targetPos, curveValue);
            elapsed += Time.deltaTime;
            yield return null;
        }

        starTransform.position = targetPos;
    }

    private IEnumerator FadeInMenuCanvas()
    {
        if (menuCanvasObject == null) yield break;

        menuCanvasObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < menuFadeInDuration)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / menuFadeInDuration);
            if (menuCanvasGroup != null)
                menuCanvasGroup.alpha = t;
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (menuCanvasGroup != null)
        {
            menuCanvasGroup.alpha          = 1f;
            menuCanvasGroup.interactable   = true;
            menuCanvasGroup.blocksRaycasts = true;
        }
    }

    private IEnumerator FadeMusicIn()
    {
        if (_audioSource == null) yield break;

        _audioSource.Play();

        float elapsed = 0f;
        while (elapsed < musicFadeInDuration)
        {
            _audioSource.volume = Mathf.Lerp(0f, musicTargetVolume,
                Mathf.SmoothStep(0f, 1f, elapsed / musicFadeInDuration));
            elapsed += Time.deltaTime;
            yield return null;
        }

        _audioSource.volume = musicTargetVolume;
    }

    private IEnumerator AnimateTitle()
    {
        if (titleCanvasGroup == null) yield break;

        Vector3 startScale = titleRectTransform != null
            ? new Vector3(titleScaleFrom, titleScaleFrom, 1f)
            : Vector3.one;

        float elapsed = 0f;
        while (elapsed < titleFadeInDuration)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / titleFadeInDuration);
            titleCanvasGroup.alpha = t;

            if (titleRectTransform != null)
                titleRectTransform.localScale = Vector3.Lerp(startScale, Vector3.one, t);

            elapsed += Time.deltaTime;
            yield return null;
        }

        titleCanvasGroup.alpha = 1f;
        if (titleRectTransform != null)
            titleRectTransform.localScale = Vector3.one;
    }

    private IEnumerator RevealButton(CanvasGroup group)
    {
        if (group == null) yield break;

        float elapsed = 0f;
        while (elapsed < buttonFadeInDuration)
        {
            group.alpha = Mathf.SmoothStep(0f, 1f, elapsed / buttonFadeInDuration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        SetButtonGroupState(group, 1f, true);
    }

    // ── Button Handlers ───────────────────────────────────────────────────────

    /// <summary>Loads the main game scene with a fade transition.</summary>
    public void OnPlayClicked()
    {
        if (SceneTransitionManager.Instance != null)
            SceneTransitionManager.Instance.LoadScene(gameSceneName);
        else
            SceneManager.LoadScene(gameSceneName);
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

    // ── Utility ───────────────────────────────────────────────────────────────

    private static void SetButtonGroupState(CanvasGroup group, float alpha, bool interactive)
    {
        group.alpha          = alpha;
        group.interactable   = interactive;
        group.blocksRaycasts = interactive;
    }
}
