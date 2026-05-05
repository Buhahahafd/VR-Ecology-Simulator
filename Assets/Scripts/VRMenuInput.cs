using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.UI;

/// <summary>
/// Listens to the left and right controller trigger inputs and submits a UI click
/// on the currently hovered button when either trigger is pressed.
/// </summary>
public class VRMenuInput : MonoBehaviour
{
    [Header("XR Ray Interactors")]
    [SerializeField] private XRRayInteractor leftRayInteractor;
    [SerializeField] private XRRayInteractor rightRayInteractor;

    [Header("Trigger Input Actions")]
    [SerializeField] private InputActionReference leftTriggerAction;
    [SerializeField] private InputActionReference rightTriggerAction;

    private void OnEnable()
    {
        leftTriggerAction.action.Enable();
        rightTriggerAction.action.Enable();

        leftTriggerAction.action.performed += OnLeftTrigger;
        rightTriggerAction.action.performed += OnRightTrigger;
    }

    private void OnDisable()
    {
        leftTriggerAction.action.performed -= OnLeftTrigger;
        rightTriggerAction.action.performed -= OnRightTrigger;

        leftTriggerAction.action.Disable();
        rightTriggerAction.action.Disable();
    }

    private void OnLeftTrigger(InputAction.CallbackContext context)
    {
        TryClick(leftRayInteractor);
    }

    private void OnRightTrigger(InputAction.CallbackContext context)
    {
        TryClick(rightRayInteractor);
    }

    /// <summary>
    /// Attempts to click the UI element currently hovered by the given ray interactor.
    /// </summary>
    private void TryClick(XRRayInteractor rayInteractor)
    {
        if (rayInteractor == null) return;

        // Get the current hovered UI object from the XR UI input module
        if (rayInteractor.TryGetCurrentUIRaycastResult(out RaycastResult raycastResult))
        {
            GameObject hitObject = raycastResult.gameObject;
            if (hitObject == null) return;

            // Walk up the hierarchy to find a clickable IPointerClickHandler
            ExecuteEvents.ExecuteHierarchy(hitObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
        }
    }
}
