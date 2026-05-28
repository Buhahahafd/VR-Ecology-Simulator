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

    public enum DangerLevel
    {
        Safe,     // green  — no threat
        Watch,    // yellow — monitor
        Maneuver, // orange — action required
        Critical  // red    — imminent collision
    }

    /// <summary>
    /// Periodically evaluates proximity between all active satellites (and debris)
    /// and assigns a collision risk level to each one based on its nearest neighbour distance.
    ///
    /// Also computes estimated time to closest approach (TCA) and a four-tier
    /// DangerLevel for use in the UI and consequence systems.
    /// </summary>
    public class RiskCalculator : MonoBehaviour
    {
        [Header("Risk Thresholds (scene units)")]
        [Tooltip("Satellites closer than this are HIGH risk (red).")]
        [SerializeField] private float highRiskDistance = 0.4f;

        [Tooltip("Satellites closer than this are MEDIUM risk (yellow).")]
        [SerializeField] private float mediumRiskDistance = 0.9f;

        [Header("Danger Level Thresholds (scene units)")]
        [Tooltip("Below this distance → Critical (red).")]
        [SerializeField] private float criticalDistance = 0.35f;

        [Tooltip("Below this distance → Maneuver required (orange).")]
        [SerializeField] private float maneuverDistance = 0.65f;

        [Tooltip("Below this distance → Watch (yellow).")]
        [SerializeField] private float watchDistance = 1.1f;

        [Header("Timing")]
        [Tooltip("How often risk is re-evaluated in seconds.")]
        [SerializeField] private float evaluationInterval = 0.3f;

        [Header("Warning Colours")]
        [SerializeField] private Color mediumRiskColor = new Color(1f, 0.75f, 0f);
        [SerializeField] private Color highRiskColor   = new Color(1f, 0.15f, 0.1f);

        // ── Internal state ────────────────────────────────────────────────────

        private readonly List<SatelliteObject> satellites = new();
        private readonly List<DebrisObject>    debrisList = new();

        private readonly Dictionary<SatelliteObject, RiskEntry> riskEntries = new();

        private float timer;

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Raised when a satellite's risk level changes (satellite-satellite pair).</summary>
        public event System.Action<SatelliteObject, SatelliteObject, RiskLevel> OnSatelliteRiskChanged;

        /// <summary>Raised when a satellite's risk changes due to debris proximity.</summary>
        public event System.Action<SatelliteObject, DebrisObject, RiskLevel> OnDebrisRiskChanged;

        // Kept for DangerLineRenderer compatibility.
        public event System.Action<SatelliteObject, DebrisObject, RiskLevel> OnSatelliteRiskChangedLegacy;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Start() => RefreshObjectLists();

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer < evaluationInterval) return;
            timer = 0f;
            EvaluateAllRisks();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Re-populates satellite and debris lists. Call after spawning new objects.</summary>
        public void RefreshObjectLists()
        {
            satellites.Clear();
            debrisList.Clear();
            riskEntries.Clear();

            satellites.AddRange(FindObjectsByType<SatelliteObject>(FindObjectsSortMode.None));
            debrisList.AddRange(FindObjectsByType<DebrisObject>(FindObjectsSortMode.None));
        }

        /// <summary>Returns the current risk level of the given satellite.</summary>
        public RiskLevel GetSatelliteRiskLevel(SatelliteObject sat) =>
            riskEntries.TryGetValue(sat, out RiskEntry e) ? e.Risk : RiskLevel.Low;

        /// <summary>Returns the current danger level of the given satellite.</summary>
        public DangerLevel GetDangerLevel(SatelliteObject sat) =>
            riskEntries.TryGetValue(sat, out RiskEntry e) ? e.Danger : DangerLevel.Safe;

        /// <summary>Returns the nearest threatening orbital object (satellite or debris), or null.</summary>
        public OrbitalObject GetNearestThreat(SatelliteObject sat) =>
            riskEntries.TryGetValue(sat, out RiskEntry e) ? e.NearestThreat : null;

        /// <summary>Returns the world-space distance to the nearest threat in scene units.</summary>
        public float GetNearestThreatDistance(SatelliteObject sat) =>
            riskEntries.TryGetValue(sat, out RiskEntry e) ? e.NearestDistance : float.MaxValue;

        /// <summary>
        /// Returns estimated time (seconds) until the nearest threat reaches its closest point.
        /// Returns float.MaxValue if no threat exists or objects are diverging.
        /// </summary>
        public float GetEstimatedTimeToClosestApproach(SatelliteObject sat) =>
            riskEntries.TryGetValue(sat, out RiskEntry e) ? e.EstimatedTCA : float.MaxValue;

        /// <summary>Returns the collision probability estimate (0–1).</summary>
        public float GetCollisionProbability(SatelliteObject sat) =>
            riskEntries.TryGetValue(sat, out RiskEntry e) ? e.CollisionProbability : 0f;

        /// <summary>Returns a localised recommended action string for the given satellite.</summary>
        public string GetRecommendedAction(SatelliteObject sat)
        {
            DangerLevel danger = GetDangerLevel(sat);
            return danger switch
            {
                DangerLevel.Critical  => "Немедленный манёвр уклонения!",
                DangerLevel.Maneuver  => "Изменить орбиту или снизить скорость",
                DangerLevel.Watch     => "Продолжать наблюдение",
                _                     => "Угроза отсутствует"
            };
        }

        // Legacy API stubs kept for compatibility.
        public RiskLevel GetDebrisRisk(DebrisObject debris) => RiskLevel.Low;
        public (DebrisObject nearestDebris, RiskLevel risk) GetSatelliteRisk(SatelliteObject satellite)
            => (null, GetSatelliteRiskLevel(satellite));

        // ── Private: evaluation ───────────────────────────────────────────────

        private void EvaluateAllRisks()
        {
            for (int i = 0; i < satellites.Count; i++)
            {
                SatelliteObject sat = satellites[i];
                if (sat == null) continue;

                RiskLevel    maxRisk     = RiskLevel.Low;
                DangerLevel  danger      = DangerLevel.Safe;
                OrbitalObject nearestThreat = null;
                float nearestDist  = float.MaxValue;
                float tca          = float.MaxValue;
                float probability  = 0f;

                // ── Satellite-satellite pairs ────────────────────────────────
                for (int j = 0; j < satellites.Count; j++)
                {
                    if (i == j) continue;
                    SatelliteObject other = satellites[j];
                    if (other == null) continue;

                    float dist = Vector3.Distance(sat.transform.position, other.transform.position);
                    RiskLevel    risk   = ClassifyRisk(dist);
                    DangerLevel  dLevel = ClassifyDanger(dist);

                    if (dist < nearestDist)
                    {
                        nearestDist   = dist;
                        nearestThreat = other;
                        tca = EstimateTCA(sat, other, dist);
                    }

                    if (risk > maxRisk)   maxRisk = risk;
                    if (dLevel > danger)  danger  = dLevel;
                }

                // ── Debris-satellite pairs ───────────────────────────────────
                DebrisObject nearestDebrisThreat = null;
                RiskLevel debrisMaxRisk = RiskLevel.Low;

                for (int d = 0; d < debrisList.Count; d++)
                {
                    DebrisObject debris = debrisList[d];
                    if (debris == null) continue;

                    float dist = Vector3.Distance(sat.transform.position, debris.transform.position);
                    RiskLevel    risk   = ClassifyRisk(dist);
                    DangerLevel  dLevel = ClassifyDanger(dist);

                    if (dist < nearestDist)
                    {
                        nearestDist   = dist;
                        nearestThreat = debris;
                        tca = EstimateTCA(sat, debris, dist);
                    }

                    if (risk > maxRisk)   maxRisk = risk;
                    if (dLevel > danger)  danger  = dLevel;

                    // Track nearest debris separately for the debris-specific event.
                    if (risk > debrisMaxRisk)
                    {
                        debrisMaxRisk = risk;
                        nearestDebrisThreat = debris;
                    }
                }

                // Collision probability ∈ [0,1]: smooth falloff based on distance to high-risk threshold.
                if (nearestThreat != null)
                {
                    float t = Mathf.InverseLerp(mediumRiskDistance, highRiskDistance, nearestDist);
                    probability = Mathf.Clamp01(t * t);
                }

                // Write / update entry.
                bool existed = riskEntries.TryGetValue(sat, out RiskEntry prev);
                var entry = new RiskEntry(maxRisk, danger, nearestThreat, nearestDist, tca, probability);
                riskEntries[sat] = entry;

                // Apply colour to satellite.
                Color targetColor = maxRisk switch
                {
                    RiskLevel.High   => highRiskColor,
                    RiskLevel.Medium => mediumRiskColor,
                    _                => sat.SatelliteData != null
                                           ? sat.SatelliteData.displayColor
                                           : Color.cyan
                };
                sat.SetColor(targetColor);

                // Apply danger colour to nearest debris threat.
                if (nearestDebrisThreat != null && debrisMaxRisk >= RiskLevel.Medium)
                {
                    nearestDebrisThreat.ApplyDangerColor(debrisMaxRisk);
                }

                bool changed = !existed || prev.Risk != maxRisk;
                if (changed)
                {
                    // Determine if the threat is a satellite or debris and fire the appropriate event.
                    SatelliteObject satThreat = nearestThreat as SatelliteObject;
                    if (satThreat != null)
                    {
                        OnSatelliteRiskChanged?.Invoke(sat, satThreat, maxRisk);
                    }

                    // Fire debris-specific event when debris is the closest or a significant threat.
                    if (nearestDebrisThreat != null && debrisMaxRisk >= RiskLevel.Medium)
                    {
                        OnDebrisRiskChanged?.Invoke(sat, nearestDebrisThreat, debrisMaxRisk);
                    }

                    // Push risk level to orbit path so it changes colour/thickness reactively.
                    if (sat.TryGetComponent<OrbitPathRenderer>(out OrbitPathRenderer opr))
                        opr.UpdateRiskLevel(maxRisk);
                }
            }
        }

        /// <summary>
        /// Estimates time to closest approach between two orbital objects.
        /// Uses relative velocity projection onto the separation axis.
        /// </summary>
        public static float EstimateTCA(OrbitalObject a, OrbitalObject b, float currentDist)
        {
            Vector3 separation = b.transform.position - a.transform.position;
            Vector3 relVel     = b.GetLinearVelocity() - a.GetLinearVelocity();

            // Closing speed: negative = approaching.
            float closingSpeed = Vector3.Dot(relVel, separation.normalized);
            if (closingSpeed >= 0f) return float.MaxValue; // diverging

            return currentDist / (-closingSpeed);
        }

        private RiskLevel ClassifyRisk(float distance)
        {
            if (distance <= highRiskDistance)   return RiskLevel.High;
            if (distance <= mediumRiskDistance) return RiskLevel.Medium;
            return RiskLevel.Low;
        }

        private DangerLevel ClassifyDanger(float distance)
        {
            if (distance <= criticalDistance)  return DangerLevel.Critical;
            if (distance <= maneuverDistance)  return DangerLevel.Maneuver;
            if (distance <= watchDistance)     return DangerLevel.Watch;
            return DangerLevel.Safe;
        }

        // ── Nested data ───────────────────────────────────────────────────────

        private readonly struct RiskEntry
        {
            public readonly RiskLevel       Risk;
            public readonly DangerLevel     Danger;
            public readonly OrbitalObject   NearestThreat;
            public readonly float           NearestDistance;
            public readonly float           EstimatedTCA;
            public readonly float           CollisionProbability;

            public RiskEntry(RiskLevel risk, DangerLevel danger,
                             OrbitalObject threat, float dist, float tca, float prob)
            {
                Risk                = risk;
                Danger              = danger;
                NearestThreat       = threat;
                NearestDistance     = dist;
                EstimatedTCA        = tca;
                CollisionProbability = prob;
            }
        }
    }
}
