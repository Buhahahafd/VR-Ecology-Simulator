using System.Collections.Generic;
using UnityEngine;

namespace SpaceDebris
{
    public enum RiskLevel
    {
        Low,
        Medium,
        High
    }

    /// <summary>
    /// Periodically evaluates distances between all satellites and debris objects.
    /// Updates debris glow colours and satellite warning colours accordingly.
    /// Raises events so the UI layer can react without being coupled to physics.
    /// </summary>
    public class RiskCalculator : MonoBehaviour
    {
        [Header("Risk Thresholds (scene units)")]
        [SerializeField] private float highRiskDistance = 5f;
        [SerializeField] private float mediumRiskDistance = 15f;

        [Header("Timing")]
        [Tooltip("How often risk is re-evaluated in seconds.")]
        [SerializeField] private float evaluationInterval = 0.5f;

        [Header("Warning Colours for Satellites")]
        [SerializeField] private Color satelliteHighRiskColor = new Color(1f, 0.2f, 0.2f);
        [SerializeField] private Color satelliteNormalColor = Color.cyan;

        private readonly List<SatelliteObject> satellites = new();
        private readonly List<DebrisObject> debrisList = new();
        private float timer;

        // Maps each debris → its current risk level (to avoid redundant material updates).
        private readonly Dictionary<DebrisObject, RiskLevel> debrisRiskCache = new();

        // Maps each satellite → nearest dangerous debris (can be null).
        private readonly Dictionary<SatelliteObject, DebrisObject> satelliteNearestDanger = new();

        /// <summary>
        /// Raised when a satellite's nearest dangerous debris changes.
        /// Parameters: satellite, nearestDangerousDebris (null if none), riskLevel.
        /// </summary>
        public event System.Action<SatelliteObject, DebrisObject, RiskLevel> OnSatelliteRiskChanged;

        private void Start()
        {
            // Auto-discover all orbital objects already in the scene.
            RefreshObjectLists();
        }

        /// <summary>
        /// Re-populates satellite and debris lists. Call after spawning new objects.
        /// </summary>
        public void RefreshObjectLists()
        {
            satellites.Clear();
            debrisList.Clear();
            debrisRiskCache.Clear();
            satelliteNearestDanger.Clear();

            satellites.AddRange(FindObjectsByType<SatelliteObject>(FindObjectsSortMode.None));
            debrisList.AddRange(FindObjectsByType<DebrisObject>(FindObjectsSortMode.None));
        }

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer < evaluationInterval) return;
            timer = 0f;
            EvaluateAllRisks();
        }

        private void EvaluateAllRisks()
        {
            // Reset debris risk cache each cycle.
            foreach (var debris in debrisList)
            {
                debrisRiskCache[debris] = RiskLevel.Low;
            }

            foreach (var satellite in satellites)
            {
                if (satellite == null) continue;

                DebrisObject nearestDanger = null;
                RiskLevel maxRisk = RiskLevel.Low;
                float nearestDist = float.MaxValue;

                foreach (var debris in debrisList)
                {
                    if (debris == null) continue;

                    float dist = Vector3.Distance(satellite.transform.position, debris.transform.position);
                    RiskLevel risk = ClassifyRisk(dist);

                    // Update per-debris risk (take the worst case across all satellites).
                    if (risk > debrisRiskCache[debris])
                        debrisRiskCache[debris] = risk;

                    if (risk > maxRisk || (risk == maxRisk && dist < nearestDist))
                    {
                        maxRisk = risk;
                        nearestDist = dist;
                        nearestDanger = debris;
                    }
                }

                // Update satellite colour.
                Color satColor = maxRisk == RiskLevel.High ? satelliteHighRiskColor : satelliteNormalColor;
                satellite.SetColor(satColor);

                // Raise event only when the situation changes.
                bool changed = !satelliteNearestDanger.TryGetValue(satellite, out DebrisObject prevDanger)
                               || prevDanger != nearestDanger;

                if (changed)
                {
                    satelliteNearestDanger[satellite] = nearestDanger;
                    OnSatelliteRiskChanged?.Invoke(satellite, nearestDanger, maxRisk);
                }
            }

            // Apply colours to debris based on worst-case risk across all satellites.
            foreach (var debris in debrisList)
            {
                if (debris == null) continue;
                debris.ApplyDangerColor(debrisRiskCache[debris]);
            }
        }

        private RiskLevel ClassifyRisk(float distance)
        {
            if (distance <= highRiskDistance) return RiskLevel.High;
            if (distance <= mediumRiskDistance) return RiskLevel.Medium;
            return RiskLevel.Low;
        }

        /// <summary>
        /// Returns the current risk level of the given debris object.
        /// </summary>
        public RiskLevel GetDebrisRisk(DebrisObject debris)
        {
            return debrisRiskCache.TryGetValue(debris, out RiskLevel r) ? r : RiskLevel.Low;
        }

        /// <summary>
        /// Returns the nearest dangerous debris for a satellite and its risk level.
        /// </summary>
        public (DebrisObject nearestDebris, RiskLevel risk) GetSatelliteRisk(SatelliteObject satellite)
        {
            if (satelliteNearestDanger.TryGetValue(satellite, out DebrisObject d))
            {
                RiskLevel r = d != null ? debrisRiskCache.GetValueOrDefault(d, RiskLevel.Low) : RiskLevel.Low;
                return (d, r);
            }
            return (null, RiskLevel.Low);
        }
    }
}
