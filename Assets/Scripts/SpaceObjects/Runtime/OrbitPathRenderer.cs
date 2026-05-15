using UnityEngine;

namespace SpaceDebris
{
    /// <summary>
    /// Draws the full circular orbit path of an OrbitalObject as a closed LineRenderer.
    ///
    /// Visual priority system:
    ///   - Safe orbits: very thin, low-alpha, cool-grey tint — readable but not dominant.
    ///   - Watch orbits: slightly brighter yellow-white.
    ///   - Maneuver / Critical: full neon color at higher alpha — draws the eye.
    ///
    /// UpdateRiskLevel() is called by RiskCalculator to upgrade line appearance
    /// without rebuilding geometry.
    /// </summary>
    [RequireComponent(typeof(OrbitalObject))]
    [RequireComponent(typeof(LineRenderer))]
    public class OrbitPathRenderer : MonoBehaviour
    {
        private const int DefaultSegmentCount = 96;

        [Header("Path Appearance")]
        [Tooltip("Vertices used to approximate the orbit circle. 96 is smooth enough for most scales.")]
        [SerializeField] private int segments = DefaultSegmentCount;

        [Tooltip("Width of the safe-state orbital path line in world units.")]
        [SerializeField] private float lineWidth = 0.0008f;

        [Tooltip("Width multiplier applied when an orbit reaches Critical risk.")]
        [SerializeField] private float dangerWidthMultiplier = 2.5f;

        [Tooltip("Alpha for safe (low-priority) orbits. Keep low to avoid visual clutter.")]
        [Range(0f, 1f)]
        [SerializeField] private float safeAlpha = 0.07f;

        [Tooltip("Alpha for watch/medium-priority orbits.")]
        [Range(0f, 1f)]
        [SerializeField] private float watchAlpha = 0.2f;

        [Tooltip("Alpha for maneuver/critical-priority orbits.")]
        [Range(0f, 1f)]
        [SerializeField] private float dangerAlpha = 0.55f;

        [Tooltip("If assigned, overrides the auto-built material.")]
        [SerializeField] private Material orbitLineMaterial;

        // ── State ─────────────────────────────────────────────────────────────

        private OrbitalObject orbitalObject;
        private LineRenderer  lineRenderer;
        private RiskLevel     currentRisk = RiskLevel.Low;
        private bool          pathDrawn;

        // ── Palette (non-HDR; these are line colours, Bloom is disabled on lines) ──

        // Safe: very dim blue-white — barely visible, doesn't compete with objects.
        private static readonly Color SafeColor      = new Color(0.45f, 0.55f, 0.65f, 1f);
        // Watch: soft yellow-white — noticeable but not alarming.
        private static readonly Color WatchColor     = new Color(0.8f,  0.75f, 0.4f,  1f);
        // Maneuver: amber-orange.
        private static readonly Color ManeuverColor  = new Color(1f,    0.55f, 0.1f,  1f);
        // Critical: bright red — demands attention.
        private static readonly Color CriticalColor  = new Color(1f,    0.12f, 0.05f, 1f);

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            orbitalObject = GetComponent<OrbitalObject>();
            lineRenderer  = GetComponent<LineRenderer>();
        }

        private void Start()
        {
            ConfigureLineRenderer();
        }

        private void LateUpdate()
        {
            // Draw once on the first LateUpdate (OrbitalObject.Start has run by then).
            if (!pathDrawn)
            {
                DrawOrbitPath();
                pathDrawn = true;
                enabled   = false; // disable Update loop — path is static for circular orbits.
            }
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Sets path parameters before Start(). Called by OrbitSpawner.
        /// </summary>
        public void Configure(int segmentCount, float width, float alpha)
        {
            segments  = segmentCount;
            lineWidth = width;
            safeAlpha = alpha;
        }

        /// <summary>
        /// Updates line colour and width to reflect the satellite's current risk level.
        /// Called by RiskCalculator whenever risk changes.
        /// </summary>
        public void UpdateRiskLevel(RiskLevel risk)
        {
            if (currentRisk == risk && pathDrawn) return;
            currentRisk = risk;
            ApplyRiskAppearance();
        }

        /// <summary>
        /// Forcefully redraws geometry. Call if orbit parameters changed at runtime.
        /// </summary>
        public void RedrawPath()
        {
            DrawOrbitPath();
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void ConfigureLineRenderer()
        {
            lineRenderer.loop                = true;
            lineRenderer.useWorldSpace       = true;
            lineRenderer.positionCount       = segments;
            lineRenderer.shadowCastingMode   = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows      = false;
            lineRenderer.generateLightingData = false;
            lineRenderer.alignment           = LineAlignment.View;

            lineRenderer.material = orbitLineMaterial != null
                ? orbitLineMaterial
                : BuildLineMaterial();

            ApplyRiskAppearance();
        }

        private void DrawOrbitPath()
        {
            float radius = orbitalObject.OrbitRadius;
            if (radius <= 0f) return;

            Quaternion planeRot = orbitalObject.OrbitalPlaneRotation;
            Vector3    center   = orbitalObject.CentralBodyPosition;
            float      step     = 360f / segments;

            for (int i = 0; i < segments; i++)
            {
                float rad = i * step * Mathf.Deg2Rad;
                Vector3 local = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * radius;
                lineRenderer.SetPosition(i, center + planeRot * local);
            }
        }

        private void ApplyRiskAppearance()
        {
            Color  baseColor;
            float  alpha;
            float  width;

            switch (currentRisk)
            {
                case RiskLevel.High:
                    baseColor = orbitalObject is SatelliteObject ? CriticalColor : ManeuverColor;
                    alpha     = dangerAlpha;
                    width     = lineWidth * dangerWidthMultiplier;
                    break;

                case RiskLevel.Medium:
                    baseColor = WatchColor;
                    alpha     = watchAlpha;
                    width     = lineWidth * 1.5f;
                    break;

                default: // Low
                    baseColor = orbitalObject is SatelliteObject
                        ? SafeColor
                        : new Color(0.55f, 0.35f, 0.55f, 1f); // dim purple for debris
                    alpha = safeAlpha;
                    width = lineWidth;
                    break;
            }

            Color lineColor = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
            lineRenderer.startColor = lineColor;
            lineRenderer.endColor   = lineColor;
            lineRenderer.startWidth = width;
            lineRenderer.endWidth   = width;
        }

        /// <summary>
        /// Builds a simple alpha-blend (not additive) unlit material.
        /// Alpha-blend keeps lines readable without stacking into white wash.
        /// </summary>
        private static Material BuildLineMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null)
                shader = Shader.Find("Unlit/Color");

            Material mat = new Material(shader) { name = "OrbitPath_AlphaBlend" };

            // Standard alpha blend: Src=SrcAlpha, Dst=OneMinusSrcAlpha.
            mat.SetFloat("_Surface",  1f); // Transparent mode
            mat.SetFloat("_Blend",    0f); // Alpha blend
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetFloat("_ZWrite",   0f);
            mat.SetColor("_BaseColor", Color.white);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            return mat;
        }
    }
}
