using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Toggles a world-space menu canvas when the player presses the B button.
/// The canvas is snapped in front of the player's face each time it opens.
/// Trigger (index finger) is used to click buttons via XR Ray Interactor —
/// that part is handled by the existing VRMenuInput component; this script
/// only manages open/close and face-placement.
/// </summary>
public class VRHeadMenu : MonoBehaviour
{
    [Header("Menu Canvas")]
    [Tooltip("The world-space Canvas GameObject to show/hide.")]
    [SerializeField] private GameObject menuCanvas;

    [Header("Placement")]
    [Tooltip("How far in front of the camera to place the menu (metres).")]
    [SerializeField] private float distanceFromHead = 1.8f;

    [Tooltip("Vertical offset relative to eye level (negative = slightly lower).")]
    [SerializeField] private float verticalOffset = -0.15f;

    [Tooltip("How quickly the canvas smoothly snaps into position when opened (0 = instant).")]
    [SerializeField] private float snapSpeed = 12f;

    [Header("Input")]
    [Tooltip("B button action (right controller secondary button).")]
    [SerializeField] private InputActionReference bButtonAction;

    [Header("Animation")]
    [SerializeField] private float fadeInDuration  = 0.18f;
    [SerializeField] private float fadeOutDuration = 0.14f;

    // ── Runtime ───────────────────────────────────────────────────────────────

    private bool         isOpen;
    private Transform    cameraTransform;
    private CanvasGroup  canvasGroup;
    private Coroutine    fadeCoroutine;

    // ── Unity lifecycle ───────────────────────────────────────────────────────

    private void Awake()
    {
        if (menuCanvas != null)
        {
            canvasGroup = menuCanvas.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = menuCanvas.AddComponent<CanvasGroup>();
        }
    }

    private void Start()
    {
        cameraTransform = ResolveCamera();

        // Start closed.
        SetMenuVisible(false, instant: true);
    }

    private void OnEnable()
    {
        if (bButtonAction != null)
        {
            bButtonAction.action.Enable();
            bButtonAction.action.performed += OnBPressed;
        }
    }

    private void OnDisable()
    {
        if (bButtonAction != null)
        {
            bButtonAction.action.performed -= OnBPressed;
            bButtonAction.action.Disable();
        }
    }

    private void Update()
    {
        // Resolve camera lazily if needed.
        if (cameraTransform == null)
            cameraTransform = ResolveCamera();

        // While menu is open, smoothly keep it in front of the head.
        if (isOpen && cameraTransform != null && menuCanvas != null)
            SmoothFollowHead();
    }

    // ── Input handler ─────────────────────────────────────────────────────────

    private void OnBPressed(InputAction.CallbackContext ctx)
    {
        ToggleMenu();
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Toggles the menu open/closed.</summary>
    public void ToggleMenu()
    {
        if (isOpen)
            CloseMenu();
        else
            OpenMenu();
    }

    /// <summary>Opens the menu and places it in front of the player's face.</summary>
    public void OpenMenu()
    {
        if (menuCanvas == null) return;

        SnapToFace();
        SetMenuVisible(true);
        isOpen = true;
    }

    /// <summary>Closes the menu.</summary>
    public void CloseMenu()
    {
        SetMenuVisible(false);
        isOpen = false;
    }

    // ── Private ───────────────────────────────────────────────────────────────

    /// <summary>
    /// Instantly places the canvas directly in front of the player's camera,
    /// facing them, at eye level with the configured offsets.
    /// </summary>
    private void SnapToFace()
    {
        if (cameraTransform == null || menuCanvas == null) return;

        Vector3 forward = cameraTransform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f)
            forward = Vector3.forward;
        forward.Normalize();

        Vector3 targetPos = cameraTransform.position
            + forward * distanceFromHead
            + Vector3.up * verticalOffset;

        menuCanvas.transform.position = targetPos;
        menuCanvas.transform.rotation = Quaternion.LookRotation(forward);
    }

    /// <summary>
    /// Smoothly keeps the canvas in front of the player while the menu is open,
    /// so it gently follows head rotation without being glued to the view.
    /// </summary>
    private void SmoothFollowHead()
    {
        Vector3 forward = cameraTransform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.001f) return;
        forward.Normalize();

        Vector3 targetPos = cameraTransform.position
            + forward * distanceFromHead
            + Vector3.up * verticalOffset;

        Quaternion targetRot = Quaternion.LookRotation(forward);

        menuCanvas.transform.position = Vector3.Lerp(
            menuCanvas.transform.position, targetPos, Time.deltaTime * snapSpeed);
        menuCanvas.transform.rotation = Quaternion.Slerp(
            menuCanvas.transform.rotation, targetRot, Time.deltaTime * snapSpeed);
    }

    private void SetMenuVisible(bool visible, bool instant = false)
    {
        if (menuCanvas == null) return;

        if (fadeCoroutine != null)
            StopCoroutine(fadeCoroutine);

        if (instant)
        {
            menuCanvas.SetActive(visible);
            if (canvasGroup != null)
            {
                canvasGroup.alpha          = visible ? 1f : 0f;
                canvasGroup.interactable   = visible;
                canvasGroup.blocksRaycasts = visible;
            }
            return;
        }

        if (visible)
        {
            menuCanvas.SetActive(true);
            fadeCoroutine = StartCoroutine(Fade(0f, 1f, fadeInDuration, onDone: null));
            if (canvasGroup != null)
            {
                canvasGroup.interactable   = true;
                canvasGroup.blocksRaycasts = true;
            }
        }
        else
        {
            fadeCoroutine = StartCoroutine(Fade(canvasGroup != null ? canvasGroup.alpha : 1f, 0f, fadeOutDuration,
                onDone: () =>
                {
                    menuCanvas.SetActive(false);
                    if (canvasGroup != null)
                    {
                        canvasGroup.interactable   = false;
                        canvasGroup.blocksRaycasts = false;
                    }
                }));
        }
    }

    private IEnumerator Fade(float from, float to, float duration, System.Action onDone)
    {
        if (canvasGroup == null) { onDone?.Invoke(); yield break; }

        float elapsed = 0f;
        canvasGroup.alpha = from;

        while (elapsed < duration)
        {
            canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
            elapsed += Time.deltaTime;
            yield return null;
        }

        canvasGroup.alpha = to;
        onDone?.Invoke();
    }

    private static Transform ResolveCamera()
    {
        Camera cam = Camera.main;
        if (cam != null) return cam.transform;
        cam = FindFirstObjectByType<Camera>();
        return cam != null ? cam.transform : null;
    }
}
