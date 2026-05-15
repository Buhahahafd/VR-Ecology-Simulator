using UnityEngine;

namespace SpaceDebris
{
    /// <summary>
    /// Provides visual selection feedback for an OrbitalObject when the player
    /// interacts with it via XR.
    ///
    /// On Select:  scale pulses and a halo ring expands around the object.
    /// On Deselect: everything returns to baseline.
    ///
    /// Attach alongside SpaceObjectInteractable. Requires no external assets.
    /// </summary>
    [RequireComponent(typeof(OrbitalObject))]
    public class OrbitalObjectHighlight : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Header("Selection Scale Pulse")]
        [Tooltip("Scale multiplier applied while selected. Slightly larger than hover scale so selection is distinct.")]
        [SerializeField] private float selectedScaleMultiplier = 2.2f;

        [Tooltip("Speed of the continuous gentle pulse while selected (Hz).")]
        [SerializeField] private float pulseFrequency = 1.4f;

        [Tooltip("Fraction of selected scale that pulses in/out.")]
        [Range(0f, 0.4f)]
        [SerializeField] private float pulseAmplitude = 0.15f;

        [Header("Halo Ring")]
        [Tooltip("Radius of the selection halo ring relative to object scale.")]
        [SerializeField] private float haloRadius = 2.8f;

        [Tooltip("Width of the halo ring line in scene units.")]
        [SerializeField] private float haloWidth = 0.0006f;

        [Tooltip("Segments used to draw the halo circle.")]
        [SerializeField] private int haloSegments = 48;

        [Tooltip("Halo colour while selected (HDR feeds Bloom).")]
        [SerializeField] private Color haloColor = new Color(1f, 1f, 1f, 0.9f);

        // ── Runtime state ─────────────────────────────────────────────────────

        private Vector3     baseScale;
        private bool        isSelected;
        private GameObject  haloObject;
        private LineRenderer haloLine;
        private float       pulseTimer;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            baseScale = transform.localScale;
        }

        private void Update()
        {
            if (!isSelected) return;

            pulseTimer += Time.deltaTime * pulseFrequency * Mathf.PI * 2f;
            float pulse = 1f + Mathf.Sin(pulseTimer) * pulseAmplitude;
            transform.localScale = baseScale * selectedScaleMultiplier * pulse;

            // Keep halo oriented toward camera and centred on object.
            if (haloObject != null)
                UpdateHaloOrientation();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Activates pulsing scale and halo ring. Call on XR select enter.</summary>
        public void Select()
        {
            isSelected  = true;
            pulseTimer  = 0f;
            baseScale   = transform.localScale; // re-cache in case SpaceObjectInteractable scaled it
            SpawnHalo();
        }

        /// <summary>Removes highlight. Call on XR select exit or when another object is selected.</summary>
        public void Deselect()
        {
            isSelected = false;
            transform.localScale = baseScale;
            DestroyHalo();
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void SpawnHalo()
        {
            DestroyHalo(); // guard against double-spawn

            haloObject = new GameObject("SelectionHalo");
            haloObject.transform.SetParent(transform, worldPositionStays: false);
            haloObject.transform.localPosition = Vector3.zero;

            haloLine                    = haloObject.AddComponent<LineRenderer>();
            haloLine.loop               = true;
            haloLine.useWorldSpace      = false; // local space — follows object automatically
            haloLine.positionCount      = haloSegments;
            haloLine.startWidth         = haloWidth;
            haloLine.endWidth           = haloWidth;
            haloLine.startColor         = haloColor;
            haloLine.endColor           = haloColor;
            haloLine.shadowCastingMode  = UnityEngine.Rendering.ShadowCastingMode.Off;
            haloLine.receiveShadows     = false;
            haloLine.generateLightingData = false;
            haloLine.material           = BuildHaloMaterial();

            float step = 360f / haloSegments;
            for (int i = 0; i < haloSegments; i++)
            {
                float rad = i * step * Mathf.Deg2Rad;
                haloLine.SetPosition(i, new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * haloRadius);
            }
        }

        private void UpdateHaloOrientation()
        {
            // Keep the halo ring facing the camera so it's always visible.
            Camera cam = Camera.main;
            if (cam == null) return;

            haloObject.transform.rotation = Quaternion.LookRotation(
                cam.transform.position - haloObject.transform.position,
                cam.transform.up);
        }

        private void DestroyHalo()
        {
            if (haloObject != null)
            {
                Destroy(haloObject);
                haloObject = null;
                haloLine   = null;
            }
        }

        private static Material BuildHaloMaterial()
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Unlit/Color");

            Material mat = new Material(shader) { name = "SelectionHalo" };

            // Additive blend — white halo glows on top of everything.
            mat.SetFloat("_Surface",  1f);
            mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.One);
            mat.SetFloat("_ZWrite",   0f);
            mat.SetColor("_BaseColor", Color.white);
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay;
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

            return mat;
        }

        private void OnDestroy() => DestroyHalo();
    }
}
