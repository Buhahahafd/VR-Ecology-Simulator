using UnityEngine;
using UnityEngine.InputSystem;

namespace SpaceDebris
{
    /// <summary>
    /// Casts a thin visual ray from a VR controller.
    /// On Trigger press: interacts with the first OrbitalObject hit.
    ///
    /// - Hovering over an object: highlights it (calls OnHover) and tints the ray cyan.
    /// - Pressing Trigger on an object: calls OnInteract (select), tints the ray white.
    /// - Ray is always visible while the controller is active; length shortens to hit point.
    ///
    /// Attach one to each controller hand GameObject.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class VRInteractionRay : MonoBehaviour
    {
        [Header("Ray Settings")]
        [SerializeField] private float rayLength = 30f;
        [SerializeField] private float rayWidth   = 0.004f;

        [Header("Colours")]
        [SerializeField] private Color colorIdle    = new Color(0.5f, 0.8f, 1f, 0.4f);
        [SerializeField] private Color colorHover   = new Color(0.2f, 1f,   1f, 0.9f);
        [SerializeField] private Color colorSelect  = new Color(1f,   1f,   1f, 1f);

        [Header("Input")]
        [Tooltip("Trigger action for this hand (float, pressed when > 0.5).")]
        [SerializeField] private InputActionReference triggerAction;

        [Header("References")]
        [Tooltip("InfoPanelController in the scene. Auto-found if empty.")]
        [SerializeField] private InfoPanelController infoPanel;
        [Tooltip("ActionPanelController in the scene. Auto-found if empty.")]
        [SerializeField] private ActionPanelController actionPanel;

        // ── Constants ─────────────────────────────────────────────────────────
        private const float TriggerThreshold = 0.5f;
        private const int   RaycastMask      = ~0; // All layers

        // ── Runtime state ─────────────────────────────────────────────────────
        private LineRenderer line;
        private RiskCalculator riskCalculator;
        private VisibilityFilterController visibilityFilter;

        private OrbitalObject hoveredObject;
        private bool wasTriggered;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            line = GetComponent<LineRenderer>();
            ConfigureLineRenderer();
        }

        private void Start()
        {
            riskCalculator   = FindFirstObjectByType<RiskCalculator>();
            visibilityFilter = FindFirstObjectByType<VisibilityFilterController>();

            if (infoPanel == null)
                infoPanel = FindFirstObjectByType<InfoPanelController>();
            if (actionPanel == null)
                actionPanel = FindFirstObjectByType<ActionPanelController>();
        }

        private void OnEnable()
        {
            if (triggerAction != null) triggerAction.action.Enable();
        }

        private void OnDisable()
        {
            if (triggerAction != null) triggerAction.action.Disable();
            ClearHover();
        }

        private void Update()
        {
            float triggerVal = triggerAction != null
                ? triggerAction.action.ReadValue<float>()
                : 0f;

            bool triggerPressed = triggerVal > TriggerThreshold;
            bool justPressed    = triggerPressed && !wasTriggered;
            wasTriggered        = triggerPressed;

            PerformRaycast(justPressed);
        }

        // ── Private: raycast ──────────────────────────────────────────────────

        private void PerformRaycast(bool justPressed)
        {
            Ray ray = new Ray(transform.position, transform.forward);

            if (Physics.Raycast(ray, out RaycastHit hit, rayLength, RaycastMask))
            {
                OrbitalObject obj = hit.collider.GetComponentInParent<OrbitalObject>();
                if (obj != null)
                {
                    // Hit an orbital object.
                    if (obj != hoveredObject)
                    {
                        ClearHover();
                        hoveredObject = obj;
                        OnHoverEnter(obj);
                    }

                    SetRay(hit.distance, justPressed ? colorSelect : colorHover);

                    if (justPressed)
                        OnInteract(obj);

                    return;
                }
            }

            // No hit.
            ClearHover();
            SetRay(rayLength, colorIdle);

            // Clicking on empty space → hide panels.
            if (justPressed)
            {
                infoPanel?.Hide();
                actionPanel?.Hide();
            }
        }

        // ── Private: interaction callbacks ────────────────────────────────────

        private void OnHoverEnter(OrbitalObject obj)
        {
            // Scale up slightly as hover feedback.
            obj.transform.localScale *= 1.5f;
        }

        private void OnHoverExit(OrbitalObject obj)
        {
            // Restore scale.
            obj.transform.localScale /= 1.5f;
        }

        private void OnInteract(OrbitalObject obj)
        {
            switch (obj)
            {
                case SatelliteObject sat:
                    infoPanel?.ShowSatelliteInfo(sat, riskCalculator);
                    visibilityFilter?.SetSelectedSatellite(sat);
                    SatelliteController ctrl = sat.GetComponent<SatelliteController>();
                    if (ctrl != null)
                        actionPanel?.ShowForSatellite(ctrl);
                    break;

                case DebrisObject debris:
                    RiskLevel risk = riskCalculator != null
                        ? riskCalculator.GetDebrisRisk(debris)
                        : RiskLevel.Low;
                    infoPanel?.ShowDebrisInfo(debris, risk);
                    actionPanel?.Hide();
                    break;
            }
        }

        private void ClearHover()
        {
            if (hoveredObject == null) return;
            OnHoverExit(hoveredObject);
            hoveredObject = null;
        }

        // ── Private: visual ───────────────────────────────────────────────────

        private void SetRay(float length, Color color)
        {
            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, Vector3.forward * length);

            // Fade tip to transparent for a nice beam look.
            line.startColor = color;
            line.endColor   = new Color(color.r, color.g, color.b, 0f);
        }

        private void ConfigureLineRenderer()
        {
            line.useWorldSpace        = false;
            line.positionCount        = 2;
            line.startWidth           = rayWidth;
            line.endWidth             = 0f;
            line.shadowCastingMode    = UnityEngine.Rendering.ShadowCastingMode.Off;
            line.receiveShadows       = false;
            line.generateLightingData = false;
            line.textureMode          = LineTextureMode.Stretch;

            // Use a simple unlit material so the ray is always visible.
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"))
            {
                name = "VRRay_Runtime"
            };
            mat.SetFloat("_Surface", 1f);   // Transparent
            mat.SetFloat("_Blend",   0f);   // Alpha
            mat.renderQueue = 3000;
            line.material = mat;

            SetRay(rayLength, colorIdle);
        }
    }
}
