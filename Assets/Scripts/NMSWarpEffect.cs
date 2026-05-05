using UnityEngine;

/// <summary>
/// Controls a multi-layer No Man's Sky-style warp nebula effect using Particle Systems.
/// Attach to a root GameObject that has 3 child Particle Systems: Background, Mid, Foreground.
/// </summary>
[ExecuteAlways]
public class NMSWarpEffect : MonoBehaviour
{
    [System.Serializable]
    public class NebulaLayer
    {
        public ParticleSystem particleSystem;

        [Header("Particles")]
        public int maxParticles = 60;
        public Vector2 sizeRange = new Vector2(20f, 60f);
        public float lifetime = 6f;

        [Header("Movement")]
        public float speedMin = 5f;
        public float speedMax = 12f;

        [Header("Spawn Volume")]
        public float spawnRadius = 40f;
        public float spawnDepthMin = 20f;
        public float spawnDepthMax = 120f;

        [Header("Fade")]
        public float fadeInTime = 1.5f;
        public float fadeOutTime = 1.5f;

        [Header("Color")]
        public Gradient colorOverLifetime;
    }

    [Header("Layers")]
    [SerializeField] private NebulaLayer backgroundLayer;
    [SerializeField] private NebulaLayer midLayer;
    [SerializeField] private NebulaLayer foregroundLayer;

    [Header("VR Safety")]
    [Tooltip("Particles closer than this distance fade out to avoid eye strain in VR")]
    [SerializeField] private float vrFadeStartDistance = 3f;
    [SerializeField] private float vrFadeEndDistance = 1f;

    [Header("Global")]
    [SerializeField] private bool playOnStart = true;

    private Transform cameraTransform;

    // Cached curve to avoid allocating new Keyframe arrays on every OnValidate call
    private AnimationCurve cachedSizeCurve;

    private void OnEnable()
    {
        cameraTransform = Camera.main != null ? Camera.main.transform : null;

        if (playOnStart)
        {
            ApplyLayerSettings(backgroundLayer);
            ApplyLayerSettings(midLayer);
            ApplyLayerSettings(foregroundLayer);
        }
    }

    /// <summary>Applies NMSWarpEffect layer settings to the given Particle System via script.</summary>
    private void ApplyLayerSettings(NebulaLayer layer)
    {
        if (layer == null || layer.particleSystem == null) return;

        var main = layer.particleSystem.main;
        main.maxParticles = layer.maxParticles;
        main.startLifetime = layer.lifetime;
        main.startSpeed = new ParticleSystem.MinMaxCurve(layer.speedMin, layer.speedMax);
        main.startSize = new ParticleSystem.MinMaxCurve(layer.sizeRange.x, layer.sizeRange.y);
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.loop = true;

        // Color over lifetime for fade in/out
        var col = layer.particleSystem.colorOverLifetime;
        col.enabled = true;
        if (layer.colorOverLifetime != null && layer.colorOverLifetime.colorKeys.Length > 0)
        {
            col.color = new ParticleSystem.MinMaxGradient(layer.colorOverLifetime);
        }
        else
        {
            col.color = new ParticleSystem.MinMaxGradient(BuildDefaultFadeGradient());
        }

        // Shape: cone/box pointing toward camera (forward)
        var shape = layer.particleSystem.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Box;
        shape.scale = new Vector3(
            layer.spawnRadius * 2f,
            layer.spawnRadius * 2f,
            layer.spawnDepthMax - layer.spawnDepthMin);

        // Velocity toward camera (-Z in world if system faces camera)
        var vel = layer.particleSystem.velocityOverLifetime;
        vel.enabled = false;

        // Rotate over lifetime for organic feel
        var rot = layer.particleSystem.rotationOverLifetime;
        rot.enabled = true;
        rot.z = new ParticleSystem.MinMaxCurve(-15f * Mathf.Deg2Rad, 15f * Mathf.Deg2Rad);

        // Size over lifetime: grow slightly then shrink
        var sizeOL = layer.particleSystem.sizeOverLifetime;
        sizeOL.enabled = true;

        if (cachedSizeCurve == null)
        {
            cachedSizeCurve = new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.15f, 1f),
                new Keyframe(0.85f, 1f),
                new Keyframe(1f, 0f));
        }

        sizeOL.size = new ParticleSystem.MinMaxCurve(1f, cachedSizeCurve);

        // Emission rate
        var emission = layer.particleSystem.emission;
        emission.enabled = true;
        float emissionRate = layer.maxParticles / layer.lifetime;
        emission.rateOverTime = emissionRate;
    }

    private Gradient BuildDefaultFadeGradient()
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(Color.white, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(1f, 0.15f),
                new GradientAlphaKey(1f, 0.85f),
                new GradientAlphaKey(0f, 1f)
            });
        return gradient;
    }

    private void Update()
    {
        if (cameraTransform == null)
        {
            cameraTransform = Camera.main != null ? Camera.main.transform : null;
            return;
        }

        // Face particle systems toward camera
        FaceTowardCamera(backgroundLayer);
        FaceTowardCamera(midLayer);
        FaceTowardCamera(foregroundLayer);
    }

    private void FaceTowardCamera(NebulaLayer layer)
    {
        if (layer == null || layer.particleSystem == null) return;

        // Position the layer root ahead of the camera
        Transform t = layer.particleSystem.transform;
        t.position = cameraTransform.position + cameraTransform.forward * GetLayerOffset(layer);
        t.rotation = Quaternion.LookRotation(-cameraTransform.forward, cameraTransform.up);
    }

    private float GetLayerOffset(NebulaLayer layer)
    {
        return (layer.spawnDepthMin + layer.spawnDepthMax) * 0.5f;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        ApplyLayerSettings(backgroundLayer);
        ApplyLayerSettings(midLayer);
        ApplyLayerSettings(foregroundLayer);
    }
#endif
}
