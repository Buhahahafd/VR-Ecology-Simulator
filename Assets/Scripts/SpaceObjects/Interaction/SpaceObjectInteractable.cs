using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SpaceDebris
{
    /// <summary>
    /// Bridges XR Interaction Toolkit hover/select events to InfoPanelController
    /// and ActionPanelController.
    ///
    /// On hover: scales the object up slightly.
    /// On select (trigger press):
    ///   - Satellites: open info panel with live risk data + action panel.
    ///   - Debris: open info panel only.
    ///   - Activates OrbitalObjectHighlight (pulse + halo ring) on the selected object.
    ///     Deselects the previously selected object automatically.
    ///   - Fires OnMonitorSelect so OrbitalMonitorHUD can update the right panel.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class SpaceObjectInteractable : MonoBehaviour
    {
        /// <summary>Raised when a satellite is selected. Consumed by OrbitalMonitorHUD.</summary>
        public event System.Action<SatelliteObject> OnMonitorSelect;

        [Header("Hover Feedback")]
        [SerializeField] private float hoverScaleMultiplier = 1.6f;

        [Header("References")]
        [SerializeField] private InfoPanelController   infoPanel;
        [SerializeField] private ActionPanelController actionPanel;

        private XRSimpleInteractable xrInteractable;
        private Vector3 baseScale;
        private OrbitalObject orbitalObject;
        private RiskCalculator riskCalculator;
        private VisibilityFilterController visibilityFilter;
        private OrbitalObjectHighlight highlight;

        // Track the globally selected highlight so we can deselect it when another is picked.
        private static OrbitalObjectHighlight currentlySelected;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            xrInteractable = GetComponent<XRSimpleInteractable>();
            orbitalObject  = GetComponent<OrbitalObject>();
            highlight      = GetComponent<OrbitalObjectHighlight>();
            baseScale      = transform.localScale;
        }

        private void Start()
        {
            riskCalculator    = FindFirstObjectByType<RiskCalculator>();
            visibilityFilter  = FindFirstObjectByType<VisibilityFilterController>();

            if (infoPanel == null)
                infoPanel = FindFirstObjectByType<InfoPanelController>();

            if (actionPanel == null)
                actionPanel = FindFirstObjectByType<ActionPanelController>();
        }

        private void OnEnable()
        {
            xrInteractable.hoverEntered.AddListener(OnHoverEntered);
            xrInteractable.hoverExited.AddListener(OnHoverExited);
            xrInteractable.selectEntered.AddListener(OnSelectEntered);
        }

        private void OnDisable()
        {
            xrInteractable.hoverEntered.RemoveListener(OnHoverEntered);
            xrInteractable.hoverExited.RemoveListener(OnHoverExited);
            xrInteractable.selectEntered.RemoveListener(OnSelectEntered);
        }

        // ── Interaction callbacks ─────────────────────────────────────────────

        private void OnHoverEntered(HoverEnterEventArgs args)
        {
            if (!IsSelected())
                transform.localScale = baseScale * hoverScaleMultiplier;
        }

        private void OnHoverExited(HoverExitEventArgs args)
        {
            if (!IsSelected())
                transform.localScale = baseScale;
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            // Deselect the previously highlighted object.
            if (currentlySelected != null && currentlySelected != highlight)
                currentlySelected.Deselect();

            // Activate highlight on this object.
            if (highlight != null)
            {
                highlight.Select();
                currentlySelected = highlight;
            }

            switch (orbitalObject)
            {
                case SatelliteObject sat:
                    infoPanel?.ShowSatelliteInfo(sat, riskCalculator);
                    visibilityFilter?.SetSelectedSatellite(sat);
                    OnMonitorSelect?.Invoke(sat);
                    SatelliteController ctrl = sat.GetComponent<SatelliteController>();
                    if (ctrl != null)
                        actionPanel?.ShowForSatellite(ctrl);
                    break;

                case DebrisObject debris:
                    RiskLevel debrisRisk = riskCalculator != null
                        ? riskCalculator.GetDebrisRisk(debris)
                        : RiskLevel.Low;
                    infoPanel?.ShowDebrisInfo(debris, debrisRisk);
                    actionPanel?.Hide();
                    break;
            }
        }

        private bool IsSelected() => highlight != null && currentlySelected == highlight;
    }
}
