using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Smoothly scales a menu button up on pointer enter and back to normal on pointer exit.
/// Works with XR Interaction Toolkit's TrackedDeviceGraphicRaycaster via UGUI events.
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class VRButtonHoverEffect : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Scale Settings")]
    [SerializeField] private float hoveredScale = 1.08f;
    [SerializeField] private float animationDuration = 0.15f;

    private RectTransform _rectTransform;
    private Vector3 _originalScale;
    private Coroutine _scaleCoroutine;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _originalScale = _rectTransform.localScale;
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
