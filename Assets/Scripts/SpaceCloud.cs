using UnityEngine;

public class SpaceCloud : MonoBehaviour
{
    private float speed;
    private Vector3 direction;
    private bool rotationEnabled;
    private Vector3 rotationAxis;
    private float rotationSpeed;

    private MeshRenderer meshRenderer;
    private Material materialInstance;
    private float baseDensity;
    private float currentAlpha = 1f;
    private float targetAlpha = 1f;
    private float fadeSpeed = 1f;
    private bool isFading = false;

    // Cached shader property ID to avoid per-frame string lookup overhead
    private static readonly int CloudDensityId = Shader.PropertyToID("_CloudDensity");

    /// <summary>Initializes movement and rotation parameters. Call SetMaterial separately to assign the material.</summary>
    public void Initialize(float cloudSpeed, Vector3 moveDirection, bool enableRotation = false, float rotSpeed = 0f)
    {
        speed = cloudSpeed;
        direction = moveDirection.normalized;
        rotationEnabled = enableRotation;
        rotationSpeed = rotSpeed;

        if (rotationEnabled)
            rotationAxis = Random.onUnitSphere;

        meshRenderer = GetComponent<MeshRenderer>();
    }

    /// <summary>Creates a unique material instance from the given shared material and triggers a fade-in.</summary>
    public void SetMaterial(Material sharedMaterial, float fadeDuration = 2f)
    {
        if (materialInstance != null)
            Destroy(materialInstance);

        // Explicitly create the instance ourselves so we control its lifetime
        materialInstance = new Material(sharedMaterial);

        // sharedMaterial assignment avoids Unity creating an additional internal copy
        if (meshRenderer != null)
            meshRenderer.sharedMaterial = materialInstance;

        baseDensity = materialInstance.GetFloat(CloudDensityId);
        currentAlpha = 0f;
        SetMaterialAlpha(0f);
        FadeIn(fadeDuration);
    }

    /// <summary>Sets the cloud movement speed.</summary>
    public void SetSpeed(float newSpeed) => speed = newSpeed;

    /// <summary>Sets the cloud movement direction.</summary>
    public void SetDirection(Vector3 newDirection) => direction = newDirection.normalized;

    /// <summary>Sets the cloud rotation parameters.</summary>
    public void SetRotation(bool enable, float rotSpeed)
    {
        rotationEnabled = enable;
        rotationSpeed = rotSpeed;
        if (rotationEnabled)
            rotationAxis = Random.onUnitSphere;
    }

    /// <summary>Triggers a fade-in over the given duration in seconds.</summary>
    public void FadeIn(float duration)
    {
        targetAlpha = 1f;
        fadeSpeed = 1f / duration;
        isFading = true;
    }

    /// <summary>Triggers a fade-out over the given duration in seconds.</summary>
    public void FadeOut(float duration, System.Action onComplete = null)
    {
        targetAlpha = 0f;
        fadeSpeed = 1f / duration;
        isFading = true;
    }

    private void SetMaterialAlpha(float alpha)
    {
        if (materialInstance != null)
            // Use cached baseDensity to avoid compounding multiplication error each frame
            materialInstance.SetFloat(CloudDensityId, baseDensity * alpha);
    }

    private void Update()
    {
        transform.position += direction * speed * Time.deltaTime;

        if (rotationEnabled)
            transform.Rotate(rotationAxis, rotationSpeed * Time.deltaTime, Space.World);

        if (isFading)
        {
            currentAlpha = Mathf.MoveTowards(currentAlpha, targetAlpha, fadeSpeed * Time.deltaTime);
            SetMaterialAlpha(currentAlpha);

            if (Mathf.Approximately(currentAlpha, targetAlpha))
                isFading = false;
        }
    }

    private void OnDestroy()
    {
        if (materialInstance != null)
            Destroy(materialInstance);
    }
}
