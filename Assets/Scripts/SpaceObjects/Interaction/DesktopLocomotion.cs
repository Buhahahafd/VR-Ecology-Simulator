using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceDebris
{
    /// <summary>
    /// Desktop keyboard + mouse locomotion for editor testing without a headset.
    /// Orbits the XR Origin around a central body (Earth) the same way OrbitalLocomotion does.
    ///
    /// Controls:
    ///   Right Mouse Button (hold) + Move Mouse — rotate around Earth
    ///   W / S                                  — zoom in / out
    ///   A / D                                  — strafe left / right (tangential)
    ///   Z / X                                  — strafe up / down (tangential)
    ///   Mouse Scroll Wheel                     — zoom in / out (fast)
    ///   F1                                     — toggle desktop locomotion on/off
    ///
    /// NOTE: Q key is reserved for the OrbitalMonitorHUD toggle.
    ///
    /// Add this component to the same GameObject that has OrbitalLocomotion (or XR Origin).
    /// Disable it in production builds if not needed.
    /// </summary>
    [DefaultExecutionOrder(10)] // run after OrbitalLocomotion
    public class DesktopLocomotion : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("References")]
        [Tooltip("The XR Origin that will be moved. Auto-found if not set.")]
        [SerializeField] private XROrigin xrOrigin;

        [Tooltip("The central body (Earth) to orbit around. Auto-found by tag 'Earth' if not set.")]
        [SerializeField] private Transform centralBody;

        [Header("Orbit Constraints")]
        [SerializeField] private float minOrbitRadius = 1.5f;
        [SerializeField] private float maxOrbitRadius = 12f;

        [Header("Sensitivity")]
        [Tooltip("Mouse rotation sensitivity (degrees per pixel).")]
        [SerializeField] private float mouseSensitivity = 0.25f;

        [Tooltip("WASD tangential movement speed (world units per second).")]
        [SerializeField] private float moveSpeed = 3f;

        [Tooltip("Scroll-wheel zoom speed multiplier.")]
        [SerializeField] private float scrollZoomSpeed = 2f;

        [Tooltip("WASD zoom speed (W/S keys).")]
        [SerializeField] private float keyZoomSpeed = 1.5f;

        // ── Runtime ───────────────────────────────────────────────────────────

        private float   currentRadius;
        private float   yaw;    // horizontal angle around Earth's Y axis
        private float   pitch;  // vertical angle

        private bool    isActive = true; // can be toggled via F1

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (xrOrigin == null)
                xrOrigin = GetComponent<XROrigin>() ?? GetComponentInParent<XROrigin>();

            if (centralBody == null)
            {
                GameObject earthGo = GameObject.Find("Earth");
                if (earthGo != null) centralBody = earthGo.transform;
            }
        }

        private void Start()
        {
            InitFromCurrentPosition();
        }

        private void Update()
        {
            // F1 — toggle desktop locomotion on/off at runtime.
            if (Keyboard.current != null && Keyboard.current.f1Key.wasPressedThisFrame)
                isActive = !isActive;

            if (!isActive || xrOrigin == null) return;

            HandleMouseRotation();
            HandleKeyboardMove();
            HandleScrollZoom();

            ApplyPosition();
        }

        // ── Private ───────────────────────────────────────────────────────────

        /// <summary>Derives initial yaw/pitch/radius from the XR Origin's current world position.</summary>
        private void InitFromCurrentPosition()
        {
            if (xrOrigin == null) return;

            Vector3 earthPos = EarthPos();
            Vector3 toPlayer = xrOrigin.transform.position - earthPos;

            currentRadius = Mathf.Clamp(toPlayer.magnitude, minOrbitRadius, maxOrbitRadius);

            if (toPlayer.sqrMagnitude < 0.0001f) toPlayer = Vector3.forward * currentRadius;

            Vector3 flat = new Vector3(toPlayer.x, 0f, toPlayer.z);
            yaw   = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
            pitch = Mathf.Clamp(Mathf.Atan2(toPlayer.y, flat.magnitude) * Mathf.Rad2Deg, -89f, 89f);
        }

        /// <summary>Rotates around Earth while right mouse button is held.</summary>
        private void HandleMouseRotation()
        {
            if (Mouse.current == null) return;
            if (!Mouse.current.rightButton.isPressed) return;

            Vector2 delta = Mouse.current.delta.ReadValue();
            yaw   += delta.x * mouseSensitivity;
            pitch -= delta.y * mouseSensitivity;
            pitch  = Mathf.Clamp(pitch, -89f, 89f);
        }

        /// <summary>WASD tangential movement and Z/X vertical strafe.</summary>
        private void HandleKeyboardMove()
        {
            if (Keyboard.current == null) return;

            // Build an orthonormal frame tangent to the sphere at the current position.
            Vector3 radial  = SphericalToCartesian(yaw, pitch, 1f).normalized;
            Vector3 right   = Vector3.Cross(radial, Vector3.up).normalized;
            // If near the poles, cross product degenerates — fall back to world right.
            if (right.sqrMagnitude < 0.01f) right = Vector3.right;
            Vector3 up = Vector3.Cross(right, radial).normalized;

            // Angular movement: convert tangential distance to angle change.
            float angularMove = moveSpeed * Time.deltaTime / currentRadius * Mathf.Rad2Deg;

            if (Keyboard.current.aKey.isPressed)
                RotateTangentially(-right, angularMove);
            if (Keyboard.current.dKey.isPressed)
                RotateTangentially( right, angularMove);
            // Z / X — vertical strafe (Q is reserved for OrbitalMonitorHUD toggle)
            if (Keyboard.current.zKey.isPressed)
                RotateTangentially( up, angularMove);
            if (Keyboard.current.xKey.isPressed)
                RotateTangentially(-up, angularMove);

            // W/S — zoom
            if (Keyboard.current.wKey.isPressed)
                currentRadius -= keyZoomSpeed * Time.deltaTime;
            if (Keyboard.current.sKey.isPressed)
                currentRadius += keyZoomSpeed * Time.deltaTime;

            currentRadius = Mathf.Clamp(currentRadius, minOrbitRadius, maxOrbitRadius);
        }

        /// <summary>Scroll-wheel zoom.</summary>
        private void HandleScrollZoom()
        {
            if (Mouse.current == null) return;
            float scroll = Mouse.current.scroll.ReadValue().y;
            if (Mathf.Abs(scroll) < 0.001f) return;

            currentRadius -= scroll * scrollZoomSpeed * Time.deltaTime;
            currentRadius  = Mathf.Clamp(currentRadius, minOrbitRadius, maxOrbitRadius);
        }

        /// <summary>
        /// Rotates the virtual "look direction" tangentially along the sphere surface
        /// by moving yaw/pitch in the direction of <paramref name="tangent"/>.
        /// </summary>
        private void RotateTangentially(Vector3 tangent, float angleDeg)
        {
            // Project tangent onto the yaw and pitch axes to adjust the angles directly.
            yaw   += tangent.x * angleDeg;
            pitch += tangent.y * angleDeg;
            pitch  = Mathf.Clamp(pitch, -89f, 89f);
        }

        /// <summary>Moves the XR Origin to the computed spherical position and faces it toward Earth.</summary>
        private void ApplyPosition()
        {
            Vector3 earthPos = EarthPos();
            Vector3 offset   = SphericalToCartesian(yaw, pitch, currentRadius);

            Vector3 targetPos = earthPos + offset;
            xrOrigin.transform.position = targetPos;

            // Look toward Earth.
            Vector3 lookDir = (earthPos - targetPos).normalized;
            if (lookDir.sqrMagnitude > 0.001f)
                xrOrigin.transform.rotation = Quaternion.LookRotation(lookDir, Vector3.up);
        }

        private Vector3 EarthPos() => centralBody != null ? centralBody.position : Vector3.zero;

        /// <summary>Converts spherical angles to a Cartesian direction scaled by radius.</summary>
        private static Vector3 SphericalToCartesian(float yawDeg, float pitchDeg, float radius)
        {
            float yRad = yawDeg   * Mathf.Deg2Rad;
            float pRad = pitchDeg * Mathf.Deg2Rad;

            float cosP = Mathf.Cos(pRad);
            return new Vector3(
                Mathf.Sin(yRad) * cosP,
                Mathf.Sin(pRad),
                Mathf.Cos(yRad) * cosP
            ) * radius;
        }
    }
}
