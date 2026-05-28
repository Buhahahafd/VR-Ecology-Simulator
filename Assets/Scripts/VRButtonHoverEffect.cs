using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Smoothly scales a menu button on pointer enter/exit when hovered by an XR ray.
/// Attach to the root Button GameObject (not to a child Text/Image).
/// The XRRayInteractor may hit a child element, so this script also registers itself
/// as an event handler on all child Graphic objects via GraphicRaycaster propagation.
/// </summary>
[RequireComponent(typeof(RectTransform))]
[RequireComponent(typeof(Button))]
public class VRButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
{
    private const float DefaultHoveredScale = 1.08f;
    private const float DefaultAnimationDuration = 0.15f;

    [Header("Scale Settings")]
    [SerializeField] private float hoveredScale = DefaultHoveredScale;
    [SerializeField] private float animationDuration = DefaultAnimationDuration;

    private RectTransform _rectTransform;
    private Vector3 _originalScale;
    private Coroutine _scaleCoroutine;
    private Button _button;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _originalScale = _rectTransform.localScale;
        _button = GetComponent<Button>();
    }

    /// <summary>Called when the VR ray enters the button area.</summary>
    public void OnPointerEnter(PointerEventData eventData)
    {
        AnimateTo(_originalScale * hoveredScale);
    }

    /// <summary>Called when the VR ray exits the button area.</summary>
    public void OnPointerExit(PointerEventData eventData)
    {
        AnimateTo(_originalScale);
    }

    /// <summary>
    /// Forwards the click to the Button component.
    /// XRUIInputModule sends IPointerClickHandler when the trigger is pressed
    /// while the ray is hovering this element — Button.onClick fires automatically,
    /// but this ensures it also works when the hit lands on a child graphic.
    /// </summary>
    public void OnPointerClick(PointerEventData eventData)
    {
        if (_button != null && _button.interactable)
            _button.onClick.Invoke();
    }

    private void AnimateTo(Vector3 targetScale)
    {
        if (_scaleCoroutine != null)
            StopCoroutine(_scaleCoroutine);

        _scaleCoroutine = StartCoroutine(ScaleRoutine(targetScale));
    }

    private IEnumerator ScaleRoutine(Vector3 targetScale)
    {
        Vector3 startScale = _rectTransform.localScale;
        float elapsed = 0f;

        while (elapsed < animationDuration)
        {
            float t = Mathf.SmoothStep(0f, 1f, elapsed / animationDuration);
            _rectTransform.localScale = Vector3.Lerp(startScale, targetScale, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        _rectTransform.localScale = targetScale;
    }
}
