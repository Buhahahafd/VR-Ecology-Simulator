using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpaceDebris
{
    /// <summary>
    /// Procedurally generates a neon wireframe grid around Earth using LineRenderers.
    /// Creates two-level grid (minor + major lines) in a disk around the equatorial plane.
    /// Lines use additive blending so they glow through Bloom without washing out the background.
    /// </summary>
    public class NeonGridGenerator : MonoBehaviour
    {
        [Header("Grid Shape")]
        [Tooltip("Outer radius of the grid disk in scene units (should exceed max orbit radius).")]
        [SerializeField] private float outerRadius = 10f;

        [Tooltip("Number of radial divisions across the grid diameter.")]
        [SerializeField] private int lineCount = 16;

        [Tooltip("Number of concentric ring divisions.")]
        [SerializeField] private int ringCount = 8;

        [Tooltip("Segments per ring (higher = smoother rings).")]
        [SerializeField] private int ringSegments = 48;

        [Header("Appearance")]
        [Tooltip("Color of minor grid lines. Alpha controls opacity — keep low to avoid clutter.")]
        [SerializeField] private Color minorLineColor = new Color(0.15f, 0.25f, 0.4f, 0.08f);

        [Tooltip("Color of major grid lines (every N-th line).")]
        [SerializeField] private Color majorLineColor = new Color(0.2f, 0.35f, 0.55f, 0.18f);

        [Tooltip("Every N-th line is major (slightly brighter).")]
        [SerializeField] private int majorEvery = 4;

        [Tooltip("Width of minor lines in scene units.")]
        [SerializeField] private float minorWidth = 0.002f;

        [Tooltip("Width of major lines in scene units.")]
        [SerializeField] private float majorWidth = 0.003f;

        [Header("Earth Reference")]
        [SerializeField] private Transform earthTransform;

        // ── Runtime ───────────────────────────────────────────────────────────

        private readonly List<GameObject> lineObjects = new();
        private Material lineMaterial;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Start()
        {
            if (earthTransform == null)
            {
                GameObject earth = GameObject.Find("Earth");
                if (earth != null) earthTransform = earth.transform;
            }

            BuildMaterial();
            GenerateGrid();
        }

        private void OnDestroy()
        {
            foreach (GameObject go in lineObjects)
                if (go != null) Destroy(go);
            lineObjects.Clear();

            if (lineMaterial != null) Destroy(lineMaterial);
        }

        // ── Grid generation ───────────────────────────────────────────────────

        private void GenerateGrid()
        {
            Vector3 centre = earthTransform != null ? earthTransform.position : Vector3.zero;

            // ── Straight radial lines (like spokes) ───────────────────────────
            for (int i = 0; i < lineCount; i++)
            {
                float t     = (float)i / lineCount;
                float angle = t * 360f * Mathf.Deg2Rad;

                bool  isMajor = (i % majorEvery == 0);
                Color color   = isMajor ? majorLineColor : minorLineColor;
                float width   = isMajor ? majorWidth     : minorWidth;

                // One line from -outerRadius to +outerRadius through centre.
                Vector3 dir = new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle));
                Vector3 a   = centre + dir *  outerRadius;
                Vector3 b   = centre + dir * -outerRadius;

                CreateLine($"Radial_{i}", new[] { a, b }, color, width, loop: false);
            }

            // ── Concentric rings ──────────────────────────────────────────────
            for (int r = 1; r <= ringCount; r++)
            {
                float radius = outerRadius * r / ringCount;
                bool  isMajor = (r % majorEvery == 0);
                Color color   = isMajor ? majorLineColor : minorLineColor;
                float width   = isMajor ? majorWidth     : minorWidth;

                Vector3[] points = new Vector3[ringSegments];
                for (int s = 0; s < ringSegments; s++)
                {
                    float a = s * (360f / ringSegments) * Mathf.Deg2Rad;
                    points[s] = centre + new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                }

                CreateLine($"Ring_{r}", points, color, width, loop: true);
            }
        }

        private void CreateLine(string objName, Vector3[] positions, Color color, float width, bool loop)
        {
            GameObject go = new GameObject(objName);
            go.transform.SetParent(transform, worldPositionStays: true);
            lineObjects.Add(go);

            LineRenderer lr           = go.AddComponent<LineRenderer>();
            lr.sharedMaterial         = lineMaterial;
            lr.useWorldSpace          = true;
            lr.loop                   = loop;
            lr.positionCount          = positions.Length;
            lr.startWidth             = width;
            lr.endWidth               = width;
            lr.startColor             = color;
            lr.endColor               = color;
            lr.shadowCastingMode      = ShadowCastingMode.Off;
            lr.receiveShadows         = false;
            lr.generateLightingData   = false;
            lr.alignment              = LineAlignment.View;
            lr.SetPositions(positions);
        }

        private void BuildMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            lineMaterial = new Material(shader);
            lineMaterial.name = "NeonGrid_Line";

            // Alpha-blend: lines are translucent and don't stack into white wash.
            lineMaterial.SetFloat("_Surface",  1f); // Transparent
            lineMaterial.SetFloat("_Blend",    0f); // Alpha blend
            lineMaterial.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            lineMaterial.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            lineMaterial.SetFloat("_ZWrite",   0f);
            lineMaterial.SetColor("_BaseColor", Color.white);
            lineMaterial.renderQueue = (int)RenderQueue.Transparent;
            lineMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        }
    }
}
