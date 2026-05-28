using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Provides ray-based VR interaction with world-space menu buttons.
/// Uses inline InputActions bound by device path — no InputActionReference assets required.
/// Each frame it geometrically tests both controller rays against registered button RectTransforms,
/// dispatches IPointerEnterHandler / IPointerExitHandler events for hover effects,
/// and dispatches IPointerClickHandler + Button.onClick on trigger press.
/// </summary>
public class VRMenuRayInput : MonoBehaviour
{
    private const string LeftTriggerBinding  = "<XRController>{LeftHand}/trigger";
    private const string RightTriggerBinding = "<XRController>{RightHand}/trigger";

    [Header("Ray Origins")]
    [Tooltip("Transform of the Left Controller Near-Far Interactor (or any child used as the ray source).")]
    [SerializeField] private Transform leftInteractorTransform;

    [Tooltip("Transform of the Right Controller Near-Far Interactor (or any child used as the ray source).")]
    [SerializeField] private Transform rightInteractorTransform;

    [Header("Settings")]
    [SerializeField] private float maxRayDistance = 10f;

    private readonly List<Button> _buttons = new();

    private InputAction _leftTrigger;
    private InputAction _rightTrigger;

    // Per-hand currently hovered button tracked to send exit events
    private Button _hoveredLeft;
    private Button _hoveredRight;

    private void Awake()
    {
        _leftTrigger  = new InputAction("VRMenu_LeftTrigger",  binding: LeftTriggerBinding);
        _rightTrigger = new InputAction("VRMenu_RightTrigger", binding: RightTriggerBinding);

        _leftTrigger.performed  += _ => TryClick(leftInteractorTransform);
        _rightTrigger.performed += _ => TryClick(rightInteractorTransform);
    }

    private void OnEnable()
    {
        _leftTrigger.Enable();
        _rightTrigger.Enable();
    }

    private void OnDisable()
    {
        _leftTrigger.Disable();
        _rightTrigger.Disable();
    }

    private void OnDestroy()
    {
        _leftTrigger?.Dispose();
        _rightTrigger?.Dispose();
    }

    private void Update()
    {
        UpdateHover(leftInteractorTransform,  ref _hoveredLeft);
        UpdateHover(rightInteractorTransform, ref _hoveredRight);
    }

    // ── Public API ────────────────────────────────────────────────────────────

    /// <summary>Registers a button to be tested against controller rays.</summary>
    public void RegisterButton(Button button)
    {
        if (button != null && !_buttons.Contains(button))
            _buttons.Add(button);
    }

    /// <summary>Removes a previously registered button.</summary>
    public void UnregisterButton(Button button)
    {
        _buttons.Remove(button);
    }

    // ── Hover ─────────────────────────────────────────────────────────────────

    private void UpdateHover(Transform interactor, ref Button currentHover)
    {
        if (interactor == null) return;

        Ray ray = new Ray(interactor.position, interactor.forward);
        Button hit = FindButtonUnderRay(ray);

        if (hit == currentHover) return;

        // Exit old hover
        if (currentHover != null)
            SendPointerEvent(currentHover.gameObject, ExecuteEvents.pointerExitHandler);

        // Enter new hover
        if (hit != null)
            SendPointerEvent(hit.gameObject, ExecuteEvents.pointerEnterHandler);

        currentHover = hit;
    }

    // ── Click ─────────────────────────────────────────────────────────────────

    private void TryClick(Transform interactor)
    {
        if (interactor == null) return;

        Ray ray = new Ray(interactor.position, interactor.forward);
        Button hit = FindButtonUnderRay(ray);

        if (hit == null || !hit.interactable) return;

        SendPointerEvent(hit.gameObject, ExecuteEvents.pointerClickHandler);
        hit.onClick.Invoke();
    }

    // ── Geometry ──────────────────────────────────────────────────────────────

    private Button FindButtonUnderRay(Ray ray)
    {
        foreach (Button button in _buttons)
        {
            if (button == null || !button.interactable || !button.gameObject.activeInHierarchy)
                continue;

            RectTransform rt = button.GetComponent<RectTransform>();
            if (rt == null) continue;

            if (RayHitsRect(ray, rt, maxRayDistance))
                return button;
        }

        return null;
    }

    /// <summary>
    /// Returns true when <paramref name="ray"/> intersects the world-space quad
    /// described by <paramref name="rt"/> within <paramref name="maxDist"/> metres.
    /// </summary>
    private static bool RayHitsRect(Ray ray, RectTransform rt, float maxDist)
    {
        Vector3[] corners = new Vector3[4];
        rt.GetWorldCorners(corners);
        // corners: [0]=bottom-left, [1]=top-left, [2]=top-right, [3]=bottom-right

        // Build a plane from three corners
        Plane plane = new Plane(corners[0], corners[1], corners[2]);

        if (!plane.Raycast(ray, out float distance)) return false;
        if (distance > maxDist) return false;

        Vector3 hitWorld = ray.GetPoint(distance);
        Vector3 hitLocal = rt.InverseTransformPoint(hitWorld);
        return rt.rect.Contains(new Vector2(hitLocal.x, hitLocal.y));
    }

    // ── Events ────────────────────────────────────────────────────────────────

    private static void SendPointerEvent<T>(GameObject target, ExecuteEvents.EventFunction<T> eventFunction)
        where T : IEventSystemHandler
    {
        PointerEventData data = new PointerEventData(EventSystem.current);
        ExecuteEvents.Execute(target, data, eventFunction);
    }
}
