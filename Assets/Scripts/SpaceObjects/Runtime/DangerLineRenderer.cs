using UnityEngine;

namespace SpaceDebris
{
    /// <summary>
    /// Draws a warning line between the two satellites with the highest collision risk.
    /// Listens to RiskCalculator.OnSatelliteRiskChanged and updates the LineRenderer accordingly.
    /// Line colour reflects severity: amber for Medium, red for High.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class DangerLineRenderer : MonoBehaviour
    {
        [SerializeField] private RiskCalculator riskCalculator;

        [Tooltip("Line colour when two satellites are at HIGH collision risk.")]
        [SerializeField] private Color dangerLineColor  = new Color(1f, 0.1f, 0.1f, 0.9f);

        [Tooltip("Line colour when two satellites are at MEDIUM collision risk.")]
        [SerializeField] private Color warningLineColor = new Color(1f, 0.75f, 0f, 0.7f);

        [SerializeField] private float lineWidth = 0.015f;

        private LineRenderer lineRenderer;

        // Currently tracked pair.
        private SatelliteObject trackedSatelliteA;
        private SatelliteObject trackedSatelliteB;
        private RiskLevel       trackedRisk;

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.positionCount = 0;
            lineRenderer.startWidth    = lineWidth;
            lineRenderer.endWidth      = lineWidth;
            lineRenderer.useWorldSpace = true;
            lineRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            lineRenderer.receiveShadows    = false;
        }

        private void OnEnable()
        {
            if (riskCalculator != null)
                riskCalculator.OnSatelliteRiskChanged += OnRiskChanged;
        }

        private void OnDisable()
        {
            if (riskCalculator != null)
                riskCalculator.OnSatelliteRiskChanged -= OnRiskChanged;
        }

        private void OnRiskChanged(SatelliteObject satellite, SatelliteObject nearestThreat, RiskLevel risk)
        {
            if (risk >= RiskLevel.Medium && nearestThreat != null)
            {
                trackedSatelliteA = satellite;
                trackedSatelliteB = nearestThreat;
                trackedRisk       = risk;
                lineRenderer.positionCount = 2;

                Color lineColor = risk == RiskLevel.High ? dangerLineColor : warningLineColor;
                lineRenderer.startColor = lineColor;
                lineRenderer.endColor   = lineColor;
            }
            else if (trackedSatelliteA == satellite)
            {
                // Clear line only if we were tracking this satellite.
                trackedSatelliteA = null;
                trackedSatelliteB = null;
                lineRenderer.positionCount = 0;
            }
        }

        private void LateUpdate()
        {
            if (trackedSatelliteA == null || trackedSatelliteB == null)
            {
                lineRenderer.positionCount = 0;
                return;
            }

            lineRenderer.SetPosition(0, trackedSatelliteA.transform.position);
            lineRenderer.SetPosition(1, trackedSatelliteB.transform.position);
        }
    }
}
