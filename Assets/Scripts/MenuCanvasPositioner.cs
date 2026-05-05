using UnityEngine;

/// <summary>
/// Positions the menu canvas in front of the player camera at startup.
/// Places it at exact camera height so the title appears slightly above center.
/// Attach to the MenuCanvas GameObject.
/// </summary>
public class MenuCanvasPositioner : MonoBehaviour
{
    [Tooltip("Distance in front of the camera along its forward direction")]
    [SerializeField] private float distanceFromCamera = 2.5f;

    [Tooltip("Additional vertical offset on top of camera height (0 = exact eye level)")]
    [SerializeField] private float verticalOffset = 0f;

    private void Awake()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Transform camTransform = cam.transform;

        // Use only horizontal forward so the canvas stays upright
        Vector3 forward = camTransform.forward;
        forward.y = 0f;
        forward.Normalize();

        // Place canvas at camera eye level + optional offset
        Vector3 position = new Vector3(
            camTransform.position.x + forward.x * distanceFromCamera,
            camTransform.position.y + verticalOffset,
            camTransform.position.z + forward.z * distanceFromCamera
        );

        transform.position = position;
        transform.rotation = Quaternion.LookRotation(forward);
    }
}
