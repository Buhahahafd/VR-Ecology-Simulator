using UnityEngine;

/// <summary>
/// Locks the SpaceEnvironment parent to the camera position every frame,
/// creating the illusion of infinite space — nebula volumes always surround
/// the viewer regardless of physical camera movement.
/// </summary>
public class SpaceEnvironmentAnchor : MonoBehaviour
{
    [SerializeField] private Transform cameraTransform;

    [Tooltip("Smoothing speed. 0 = instant snap, higher = faster follow.")]
    [SerializeField] private float followSpeed = 0f;

    private void Start()
    {
        if (cameraTransform == null)
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
                cameraTransform = mainCamera.transform;
            else
                Debug.LogError($"[SpaceEnvironmentAnchor] No camera assigned and Camera.main not found.");
        }

        if (cameraTransform != null)
            transform.position = cameraTransform.position;
    }

    private void LateUpdate()
    {
        if (cameraTransform == null) return;

        if (followSpeed <= 0f)
            transform.position = cameraTransform.position;
        else
            transform.position = Vector3.Lerp(transform.position, cameraTransform.position, followSpeed * Time.deltaTime);
    }
}
