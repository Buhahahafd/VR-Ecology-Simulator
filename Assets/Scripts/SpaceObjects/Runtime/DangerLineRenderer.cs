using UnityEngine;

namespace SpaceDebris
{
    /// <summary>
    /// Draws a red line between a satellite and its nearest high-risk debris object.
    /// Listens to RiskCalculator.OnSatelliteRiskChanged and updates the LineRenderer accordingly.
    /// </summary>
    [RequireComponent(typeof(LineRenderer))]
    public class DangerLineRenderer : MonoBehaviour
    {
        [SerializeField] private RiskCalculator riskCalculator;
        [SerializeField] private Color dangerLineColor = new Color(1f, 0.1f, 0.1f, 0.85f);
        [SerializeField] private float lineWidth = 0.02f;

        private LineRenderer lineRenderer;

        // Currently tracked satellite-debris pair.
        private SatelliteObject trackedSatellite;
        private DebrisObject trackedDebris;

        private void Awake()
        {
            lineRenderer = GetComponent<LineRenderer>();
            lineRenderer.positionCount = 0;
            lineRenderer.startWidth = lineWidth;
            lineRenderer.endWidth = lineWidth;
            lineRenderer.startColor = dangerLineColor;
            lineRenderer.endColor = dangerLineColor;
            lineRenderer.useWorldSpace = true;
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

        private void OnRiskChanged(SatelliteObject satellite, DebrisObject nearestDebris, RiskLevel risk)
        {
            if (risk == RiskLevel.High && nearestDebris != null)
            {
                trackedSatellite = satellite;
                trackedDebris = nearestDebris;
                lineRenderer.positionCount = 2;
            }
            else
            {
                trackedSatellite = null;
                trackedDebris = null;
                lineRenderer.positionCount = 0;
            }
        }

        private void LateUpdate()
        {
            if (trackedSatellite == null || trackedDebris == null)
            {
                lineRenderer.positionCount = 0;
                return;
            }

            lineRenderer.SetPosition(0, trackedSatellite.transform.position);
            lineRenderer.SetPosition(1, trackedDebris.transform.position);
        }
    }
}
