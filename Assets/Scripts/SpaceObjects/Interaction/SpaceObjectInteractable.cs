using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SpaceDebris
{
    /// <summary>
    /// Bridges XR Interaction Toolkit hover/select events to the InfoPanelController.
    /// Attach alongside XRSimpleInteractable on each orbital object prefab.
    /// On hover: scales the object up slightly.
    /// On select (trigger press): opens the info panel.
    /// </summary>
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class SpaceObjectInteractable : MonoBehaviour
    {
        [Header("Hover Feedback")]
        [SerializeField] private float hoverScaleMultiplier = 1.6f;

        [Header("References")]
        [SerializeField] private InfoPanelController infoPanel;

        private XRSimpleInteractable xrInteractable;
        private Vector3 baseScale;
        private OrbitalObject orbitalObject;
        private RiskCalculator riskCalculator;

        private void Awake()
        {
            xrInteractable = GetComponent<XRSimpleInteractable>();
            orbitalObject = GetComponent<OrbitalObject>();
            baseScale = transform.localScale;
        }

        private void Start()
        {
            riskCalculator = FindFirstObjectByType<RiskCalculator>();

            if (infoPanel == null)
                infoPanel = FindFirstObjectByType<InfoPanelController>();
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

        private void OnHoverEntered(HoverEnterEventArgs args)
        {
            transform.localScale = baseScale * hoverScaleMultiplier;
        }

        private void OnHoverExited(HoverExitEventArgs args)
        {
            transform.localScale = baseScale;
        }

        private void OnSelectEntered(SelectEnterEventArgs args)
        {
            if (infoPanel == null) return;

            switch (orbitalObject)
            {
                case SatelliteObject sat:
                    var (nearestDebris, risk) = riskCalculator != null
                        ? riskCalculator.GetSatelliteRisk(sat)
                        : (null, RiskLevel.Low);
                    infoPanel.ShowSatelliteInfo(sat, nearestDebris, risk);
                    break;

                case DebrisObject debris:
                    RiskLevel debrisRisk = riskCalculator != null
                        ? riskCalculator.GetDebrisRisk(debris)
                        : RiskLevel.Low;
                    infoPanel.ShowDebrisInfo(debris, debrisRisk);
                    break;
            }
        }
    }
}
