using UnityEngine;
using UnityEngine.XR;

namespace SpaceDebris
{
    /// <summary>
    /// Keeps a World Space Canvas positioned and oriented in front of the player's head.
    /// Attach to Scenario HUD or any always-visible UI canvas.
    /// </summary>
    public class CameraFollowCanvas : MonoBehaviour
    {
        [Header("Position")]
        [Tooltip("Offset from the camera in local camera space (right, up, forward).")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 0.1f, 2f);

        [Header("Smoothing")]
        [SerializeField] private float positionSmoothSpeed = 3f;
        [SerializeField] private float rotationSmoothSpeed = 5f;

        [Header("Follow Behaviour")]
        [Tooltip("Only update position/rotation when angular difference exceeds this threshold (degrees). Reduces jitter.")]
        [SerializeField] private float angularDeadzone = 20f;

        private Transform cameraTransform;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Start()
        {
            cameraTransform = ResolveCamera();
            if (cameraTransform != null)
                SnapToTarget();
        }

        private void LateUpdate()
        {
            if (cameraTransform == null)
            {
                cameraTransform = ResolveCamera();
                return;
            }

            Vector3 targetPos = cameraTransform.TransformPoint(offset);
            Quaternion targetRot = Quaternion.LookRotation(targetPos - cameraTransform.position);

            float angleDiff = Quaternion.Angle(transform.rotation, targetRot);
            if (angleDiff > angularDeadzone)
            {
                transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * positionSmoothSpeed);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, Time.deltaTime * rotationSmoothSpeed);
            }
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private void SnapToTarget()
        {
            Vector3 pos = cameraTransform.TransformPoint(offset);
            transform.position = pos;
            transform.rotation = Quaternion.LookRotation(pos - cameraTransform.position);
        }

        /// <summary>
        /// Finds the XR or main camera reliably across different XR rigs.
        /// Priority: Camera tagged MainCamera → any active Camera in scene.
        /// </summary>
        private static Transform ResolveCamera()
        {
            Camera cam = Camera.main;
            if (cam != null) return cam.transform;

            cam = FindFirstObjectByType<Camera>();
            return cam != null ? cam.transform : null;
        }
    }
}
