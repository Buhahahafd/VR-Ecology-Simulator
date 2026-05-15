using UnityEngine;
using UnityEngine.InputSystem;
using Unity.XR.CoreUtils;

namespace SpaceDebris
{
    /// <summary>
    /// Panoptic-style locomotion: the player grabs the air (Grip) and pulls themselves
    /// through space. Pressing Grip records the controller's world position; while held,
    /// the XR Origin is moved by the inverse of how far the controller has moved from
    /// the grab point, so it feels like grabbing and dragging the world around you.
    /// The position is clamped to a spherical shell around the central body.
    /// </summary>
    public class OrbitalLocomotion : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("The XR Origin that will be moved.")]
        [SerializeField] private XROrigin xrOrigin;

        [Tooltip("The central body (Earth) to orbit around.")]
        [SerializeField] private Transform centralBody;

        [Header("Movement Settings")]
        [Tooltip("Multiplier applied on top of raw hand displacement. 1 = 1:1 grab-and-drag.")]
        [SerializeField] private float dragScale = 1.5f;

        [Tooltip("Minimum orbit radius (distance from Earth centre). Prevents clipping.")]
        [SerializeField] private float minOrbitRadius = 1.5f;

        [Tooltip("Maximum orbit radius.")]
        [SerializeField] private float maxOrbitRadius = 12f;

        [Header("Input Actions")]
        [SerializeField] private InputActionReference leftGripAction;
        [SerializeField] private InputActionReference rightGripAction;
        [SerializeField] private InputActionReference leftControllerPositionAction;
        [SerializeField] private InputActionReference rightControllerPositionAction;

        // ── Constants ─────────────────────────────────────────────────────────
        private const float GripThreshold = 0.5f;

        // ── Runtime state ─────────────────────────────────────────────────────

        // World position where each hand grabbed (XR Origin space anchor point).
        private Vector3 leftGrabWorldPos;
        private Vector3 rightGrabWorldPos;

        private bool leftGrabActive;
        private bool rightGrabActive;

        private float orbitRadius;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (xrOrigin == null)
                xrOrigin = GetComponentInParent<XROrigin>();
        }

        private void Start()
        {
            RecalcOrbitRadius();
        }

        private void OnEnable()
        {
            Enable(leftGripAction);
            Enable(rightGripAction);
            Enable(leftControllerPositionAction);
            Enable(rightControllerPositionAction);
        }

        private void OnDisable()
        {
            Disable(leftGripAction);
            Disable(rightGripAction);
            Disable(leftControllerPositionAction);
            Disable(rightControllerPositionAction);
        }

        private void Update()
        {
            float leftGrip  = leftGripAction  != null ? leftGripAction.action.ReadValue<float>()  : 0f;
            float rightGrip = rightGripAction != null ? rightGripAction.action.ReadValue<float>() : 0f;

            bool leftHeld  = leftGrip  > GripThreshold;
            bool rightHeld = rightGrip > GripThreshold;

            // Controller positions from XRI are already in world space.
            Vector3 leftWorld  = leftControllerPositionAction  != null
                ? leftControllerPositionAction.action.ReadValue<Vector3>()  : Vector3.zero;
            Vector3 rightWorld = rightControllerPositionAction != null
                ? rightControllerPositionAction.action.ReadValue<Vector3>() : Vector3.zero;

            // ── Left grip grab-start ──────────────────────────────────────────
            if (leftHeld && !leftGrabActive)
            {
                leftGrabWorldPos = leftWorld;
                leftGrabActive   = true;
            }
            else if (!leftHeld)
            {
                leftGrabActive = false;
            }

            // ── Right grip grab-start ─────────────────────────────────────────
            if (rightHeld && !rightGrabActive)
            {
                rightGrabWorldPos = rightWorld;
                rightGrabActive   = true;
            }
            else if (!rightHeld)
            {
                rightGrabActive = false;
            }

            // ── Compute total drag delta ──────────────────────────────────────
            Vector3 totalDelta = Vector3.zero;
            int     activeCount = 0;

            if (leftGrabActive)
            {
                // Delta = current hand pos - grab anchor.  Move origin by -delta
                // so it feels like the hand is stuck at the grab point in world space.
                Vector3 delta = leftWorld - leftGrabWorldPos;
                totalDelta  += delta;
                activeCount++;

                // Update grab anchor to current position so drag is continuous.
                leftGrabWorldPos = leftWorld;
            }

            if (rightGrabActive)
            {
                Vector3 delta = rightWorld - rightGrabWorldPos;
                totalDelta  += delta;
                activeCount++;

                rightGrabWorldPos = rightWorld;
            }

            if (activeCount == 0) return;

            // Average the two hands if both are active.
            totalDelta /= activeCount;

            // Move origin in the OPPOSITE direction (you pull the world, world pulls you).
            ApplyDrag(-totalDelta * dragScale);
        }

        // ── Private ───────────────────────────────────────────────────────────

        /// <summary>
        /// Applies a world-space displacement to the XR Origin and re-projects it
        /// onto the spherical shell at the current orbit radius.
        /// </summary>
        private void ApplyDrag(Vector3 worldDelta)
        {
            Vector3 earthPos = centralBody != null ? centralBody.position : Vector3.zero;
            Vector3 newPos   = xrOrigin.transform.position + worldDelta;

            Vector3 toNew    = newPos - earthPos;
            float   dist     = toNew.magnitude;

            if (dist < 0.001f) return;

            // Clamp to the spherical shell.
            float clampedRadius = Mathf.Clamp(orbitRadius, minOrbitRadius, maxOrbitRadius);
            xrOrigin.transform.position = earthPos + toNew.normalized * clampedRadius;
        }

        private void RecalcOrbitRadius()
        {
            if (xrOrigin == null || centralBody == null) return;
            orbitRadius = Vector3.Distance(xrOrigin.transform.position, centralBody.position);
            orbitRadius = Mathf.Clamp(orbitRadius, minOrbitRadius, maxOrbitRadius);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private static void Enable(InputActionReference r)  { if (r != null) r.action.Enable(); }
        private static void Disable(InputActionReference r) { if (r != null) r.action.Disable(); }
    }
}
