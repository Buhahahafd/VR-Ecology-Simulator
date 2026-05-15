using UnityEngine;
using UnityEngine.Rendering;

namespace SpaceDebris
{
    /// <summary>
    /// Applies the cyberpunk/synthwave visual style to all spawned orbital objects.
    ///
    /// Wires into OrbitSpawner.OnObjectSpawned to intercept every newly spawned
    /// OrbitalObject and:
    ///   1. Replace its material with the NeonWireframe shader variant.
    ///   2. Add NeonWireframeBaker so UV2 barycentric coords are ready for edge rendering.
    ///   3. Set per-instance HDR neon colors via MaterialPropertyBlock.
    ///
    /// Also exposes SetRiskColor() so RiskCalculator can push colour changes without
    /// touching the shared material.
    /// </summary>
    public class CyberpunkStyleManager : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Materials (auto-created if left empty)")]
        [SerializeField] private Material satelliteNeonMaterial;
        [SerializeField] private Material debrisNeonMaterial;

        // ── HDR Neon palette ─────────────────────────────────────────────────
        // Moderate HDR (1.2–1.6) — enough to trigger Bloom at threshold 0.9
        // but not so high that everything merges into white haze.

        public static readonly Color CyanNeon    = new Color(0f,   1.1f, 1.3f, 1f);
        public static readonly Color MagentaNeon = new Color(1.2f, 0.05f, 1.1f, 1f);
        public static readonly Color RedNeon     = new Color(1.8f, 0.04f, 0.08f, 1f);
        public static readonly Color YellowNeon  = new Color(1.6f, 1.2f,  0f,   1f);

        private static readonly Color CyanFill    = new Color(0f,   0.7f, 1f,   0.05f);
        private static readonly Color MagentaFill = new Color(1f,   0.1f, 0.9f, 0.04f);

        private static readonly int EdgeColorID    = Shader.PropertyToID("_EdgeColor");
        private static readonly int EmissionColorID= Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorID    = Shader.PropertyToID("_BaseColor");

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Start()
        {
            EnsureMaterials();

            OrbitSpawner spawner = GetComponentInParent<OrbitSpawner>()
                                ?? FindFirstObjectByType<OrbitSpawner>();
            if (spawner != null)
                spawner.OnObjectSpawned += OnObjectSpawned;
        }

        private void OnDestroy()
        {
            OrbitSpawner spawner = FindFirstObjectByType<OrbitSpawner>();
            if (spawner != null)
                spawner.OnObjectSpawned -= OnObjectSpawned;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Applies neon wireframe style to a newly spawned OrbitalObject.
        /// Automatically called via OnObjectSpawned event from OrbitSpawner.
        /// </summary>
        public void ApplyStyle(OrbitalObject obj)
        {
            if (obj == null) return;

            bool isSatellite = obj is SatelliteObject;

            // Ensure barycentric UV2 is baked.
            if (!obj.TryGetComponent<NeonWireframeBaker>(out _))
                obj.gameObject.AddComponent<NeonWireframeBaker>();

            Renderer rend = obj.GetComponent<Renderer>();
            if (rend == null) return;

            // Assign shared material (no per-material instancing overhead).
            rend.sharedMaterial = isSatellite ? satelliteNeonMaterial : debrisNeonMaterial;

            // Per-instance colors via MPB.
            Color neonEdge = isSatellite ? CyanNeon    : MagentaNeon;
            Color fill     = isSatellite ? CyanFill    : MagentaFill;

            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            rend.GetPropertyBlock(mpb);
            mpb.SetColor(EdgeColorID,     neonEdge);
            mpb.SetColor(EmissionColorID, neonEdge);
            mpb.SetColor(BaseColorID,     fill);
            rend.SetPropertyBlock(mpb);
        }

        /// <summary>
        /// Updates only the edge/emission color based on risk level.
        /// Called by risk evaluation code when a satellite's danger changes.
        /// </summary>
        public static void SetRiskColor(Renderer rend, RiskLevel risk)
        {
            if (rend == null) return;

            Color neon = risk switch
            {
                RiskLevel.High   => RedNeon,
                RiskLevel.Medium => YellowNeon,
                _                => CyanNeon
            };

            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            rend.GetPropertyBlock(mpb);
            mpb.SetColor(EdgeColorID,     neon);
            mpb.SetColor(EmissionColorID, neon);
            rend.SetPropertyBlock(mpb);
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void OnObjectSpawned(OrbitalObject obj) => ApplyStyle(obj);

        private void EnsureMaterials()
        {
            Shader neonShader = Shader.Find("SpaceDebris/NeonWireframe");
            if (neonShader == null)
            {
                Debug.LogWarning("[CyberpunkStyleManager] NeonWireframe shader not found — " +
                                 "falling back to URP Unlit.");
                neonShader = Shader.Find("Universal Render Pipeline/Unlit");
            }

            if (neonShader == null) return;

            if (satelliteNeonMaterial == null)
                satelliteNeonMaterial = BuildNeonMaterial(neonShader, "NeonWireframe_Sat",
                    CyanFill, CyanNeon, edgeThickness: 1.3f, edgeSoftness: 0.4f);

            if (debrisNeonMaterial == null)
                debrisNeonMaterial = BuildNeonMaterial(neonShader, "NeonWireframe_Deb",
                    MagentaFill, MagentaNeon, edgeThickness: 1.0f, edgeSoftness: 0.5f);
        }

        private static Material BuildNeonMaterial(Shader shader, string matName,
                                                   Color fill, Color edge,
                                                   float edgeThickness, float edgeSoftness)
        {
            Material mat           = new Material(shader);
            mat.name               = matName;
            mat.enableInstancing   = true;

            mat.SetColor("_BaseColor",     fill);
            mat.SetColor("_EdgeColor",     edge);
            mat.SetColor("_EmissionColor", edge);
            mat.SetFloat("_EdgeThickness", edgeThickness);
            mat.SetFloat("_EdgeSoftness",  edgeSoftness);

            return mat;
        }
    }
}
