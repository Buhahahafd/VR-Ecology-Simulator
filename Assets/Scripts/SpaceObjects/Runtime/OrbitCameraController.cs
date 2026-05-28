using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceDebris
{
    /// <summary>
    /// Desktop orbit camera controller for testing without a VR headset.
    /// A/D — horizontal orbit rotation, W/S — zoom in/out,
    /// Right mouse button held — free orbit rotation in all directions.
    /// Attach to the XR Origin (or any parent of the camera).
    /// </summary>
    public class OrbitCameraController : MonoBehaviour
    {
        private const float DefaultRotationSpeed = 40f;
        private const float DefaultZoomSpeed = 15f;
        private const float DefaultMinDistance = 8f;
        private const float DefaultMaxDistance = 120f;
        private const float DefaultMouseSensitivity = 0.3f;
        private const float DefaultMinPitch = -85f;
        private const float DefaultMaxPitch = 85f;

        [Header("Target")]
        [Tooltip("The object to orbit around. Auto-finds 'Earth' if left empty.")]
        [SerializeField] private Transform target;

        [Header("Keyboard Rotation")]
        [Tooltip("Horizontal rotation speed in degrees per second.")]
        [SerializeField] private float rotationSpeed = DefaultRotationSpeed;

        [Header("Mouse Orbit (Right Click)")]
        [Tooltip("Mouse sensitivity for orbit rotation (degrees per pixel).")]
        [SerializeField] private float mouseSensitivity = DefaultMouseSensitivity;

        [Tooltip("Minimum vertical angle in degrees (looking up limit).")]
        [SerializeField] private float minPitch = DefaultMinPitch;

        [Tooltip("Maximum vertical angle in degrees (looking down limit).")]
        [SerializeField] private float maxPitch = DefaultMaxPitch;

        [Header("Zoom")]
        [Tooltip("Zoom speed in scene units per second.")]
        [SerializeField] private float zoomSpeed = DefaultZoomSpeed;

        [Tooltip("Minimum distance from the target center.")]
        [SerializeField] private float minDistance = DefaultMinDistance;

        [Tooltip("Maximum distance from the target center.")]
        [SerializeField] private float maxDistance = DefaultMaxDistance;

        private float currentDistance;
        private float currentYaw;
        private float currentPitch;

        private void Start()
        {
            if (target == null)
            {
                GameObject earth = GameObject.Find("Earth");
                if (earth != null)
                {
                    target = earth.transform;
                }
                else
                {
                    Debug.LogWarning("[OrbitCameraController] No target assigned and 'Earth' not found. Controller disabled.");
                    enabled = false;
                    return;
                }
            }

            // Initialize from current position relative to target.
            Vector3 offset = transform.position - target.position;
            currentDistance = offset.magnitude;
            currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);

            currentYaw = Mathf.Atan2(offset.x, offset.z) * Mathf.Rad2Deg;
            currentPitch = -Mathf.Asin(Mathf.Clamp(offset.y / currentDistance, -1f, 1f)) * Mathf.Rad2Deg;

            ApplyTransform();
        }

        private void Update()
        {
            bool changed = false;

            changed |= HandleKeyboardInput();
            changed |= HandleMouseOrbit();

            if (changed)
            {
                ApplyTransform();
            }
        }

        /// <summary>
        /// Handles A/D rotation and W/S zoom via keyboard.
        /// </summary>
        private bool HandleKeyboardInput()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return false;

            float rotateInput = 0f;
            if (keyboard.aKey.isPressed) rotateInput -= 1f;
            if (keyboard.dKey.isPressed) rotateInput += 1f;

            float zoomInput = 0f;
            if (keyboard.wKey.isPressed) zoomInput -= 1f;
            if (keyboard.sKey.isPressed) zoomInput += 1f;

            if (Mathf.Approximately(rotateInput, 0f) && Mathf.Approximately(zoomInput, 0f))
                return false;

            currentYaw += rotateInput * rotationSpeed * Time.deltaTime;
            currentDistance += zoomInput * zoomSpeed * Time.deltaTime;
            currentDistance = Mathf.Clamp(currentDistance, minDistance, maxDistance);

            return true;
        }

        /// <summary>
        /// Handles free orbit rotation while holding the right mouse button.
        /// </summary>
        private bool HandleMouseOrbit()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return false;

            if (!mouse.rightButton.isPressed) return false;

            Vector2 delta = mouse.delta.ReadValue();
            if (Mathf.Approximately(delta.x, 0f) && Mathf.Approximately(delta.y, 0f))
                return false;

            currentYaw += delta.x * mouseSensitivity;
            currentPitch -= delta.y * mouseSensitivity;
            currentPitch = Mathf.Clamp(currentPitch, minPitch, maxPitch);

            return true;
        }

        /// <summary>
        /// Positions the rig at the correct distance and rotation around the target.
        /// </summary>
        private void ApplyTransform()
        {
            if (target == null) return;

            Quaternion rotation = Quaternion.Euler(currentPitch, currentYaw, 0f);
            Vector3 offset = rotation * (Vector3.forward * -currentDistance);

            transform.position = target.position + offset;
            transform.LookAt(target.position, Vector3.up);
        }
    }
}
