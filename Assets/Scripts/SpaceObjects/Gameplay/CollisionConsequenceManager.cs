using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceDebris
{
    /// <summary>
    /// Monitors high-risk satellite pairs. If neither satellite has its orbit changed
    /// before the collision timer expires, a collision is simulated:
    ///   1. Both satellites are destroyed.
    ///   2. New DebrisObject instances spawn at the collision point.
    ///   3. Player resources (communication, energy, score) are reduced.
    ///   4. A chain-reaction check schedules further collisions.
    /// </summary>
    public class CollisionConsequenceManager : MonoBehaviour
    {
        // ── Configuration ─────────────────────────────────────────────────────

        [Header("Resources")]
        [Tooltip("Score the player starts with. Decremented by collisions.")]
        [SerializeField] private float startingScore = 1000f;

        [Tooltip("Score penalty per collision.")]
        [SerializeField] private float collisionScorePenalty = 150f;

        [Header("Debris Spawning")]
        [Tooltip("How many debris fragments spawn per collision.")]
        [SerializeField] private int debrisPerCollision = 4;

        [Tooltip("Prefab for runtime-spawned debris. Must have DebrisObject component.")]
        [SerializeField] private GameObject debrisPrefab;

        [Tooltip("Parent transform for spawned debris.")]
        [SerializeField] private Transform debrisParent;

        [Header("References")]
        [SerializeField] private RiskCalculator riskCalculator;

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Raised whenever a collision occurs. Param: world position.</summary>
        public event Action<Vector3> OnCollision;

        /// <summary>Raised when resources change. Params: score, communicationFraction, energyFraction.</summary>
        public event Action<float, float, float> OnResourcesChanged;

        /// <summary>Raised when score reaches zero — game-over state.</summary>
        public event Action OnCascadeStarted;

        // ── Public state ──────────────────────────────────────────────────────

        public float Score                  { get; private set; }
        public float CommunicationFraction  { get; private set; } = 1f;
        public float EnergyFraction         { get; private set; } = 1f;

        // ── Private state ─────────────────────────────────────────────────────

        // Tracks pairs already scheduled for collision to avoid duplicates.
        private readonly HashSet<(SatelliteObject, SatelliteObject)> scheduledCollisions = new();

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            Score = startingScore;
        }

        private void Start()
        {
            if (riskCalculator == null)
                riskCalculator = FindFirstObjectByType<RiskCalculator>();

            if (riskCalculator != null)
                riskCalculator.OnSatelliteRiskChanged += HandleRiskChanged;
        }

        private void OnDestroy()
        {
            if (riskCalculator != null)
                riskCalculator.OnSatelliteRiskChanged -= HandleRiskChanged;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Forces an immediate collision between two satellites.
        /// Call from ScenarioManager when the countdown expires.
        /// </summary>
        public void ForceCollision(SatelliteObject a, SatelliteObject b)
        {
            if (a == null || b == null) return;
            SimulateCollision(a, b);
        }

        // ── Private: risk response ────────────────────────────────────────────

        private void HandleRiskChanged(SatelliteObject sat, SatelliteObject threat, RiskLevel risk)
        {
            if (risk != RiskLevel.High || sat == null || threat == null) return;

            // Create a canonical pair (lower instance ID first) to avoid duplicates.
            var pair = sat.GetInstanceID() < threat.GetInstanceID()
                ? (sat, threat)
                : (threat, sat);

            if (scheduledCollisions.Contains(pair)) return;
            scheduledCollisions.Add(pair);

            float tca = riskCalculator.GetEstimatedTimeToClosestApproach(sat);
            float delay = tca < float.MaxValue ? Mathf.Max(tca, 1f) : 10f;

            StartCoroutine(CollisionCountdown(pair.Item1, pair.Item2, delay));
        }

        private IEnumerator CollisionCountdown(SatelliteObject a, SatelliteObject b, float delay)
        {
            float elapsed = 0f;

            while (elapsed < delay)
            {
                elapsed += Time.deltaTime;

                // Abort if either satellite was saved (destroyed or risk dropped).
                if (a == null || b == null)
                {
                    ClearPair(a, b);
                    yield break;
                }

                RiskLevel riskA = riskCalculator != null
                    ? riskCalculator.GetSatelliteRiskLevel(a)
                    : RiskLevel.Low;

                if (riskA != RiskLevel.High)
                {
                    ClearPair(a, b);
                    yield break;
                }

                yield return null;
            }

            if (a != null && b != null)
                SimulateCollision(a, b);

            ClearPair(a, b);
        }

        private void SimulateCollision(SatelliteObject a, SatelliteObject b)
        {
            Vector3 collisionPoint = (a.transform.position + b.transform.position) * 0.5f;

            Debug.Log($"[ConsequenceManager] Collision: {a.name} x {b.name} at {collisionPoint}");

            OnCollision?.Invoke(collisionPoint);
            ApplyPenalties();
            SpawnDebrisCloud(collisionPoint);

            // Destroy both satellites.
            var ctrlA = a.GetComponent<SatelliteController>();
            var ctrlB = b.GetComponent<SatelliteController>();

            if (ctrlA != null) ctrlA.TriggerCollision();
            else Destroy(a.gameObject);

            if (ctrlB != null) ctrlB.TriggerCollision();
            else Destroy(b.gameObject);

            // Refresh risk system after objects are removed.
            if (riskCalculator != null)
                StartCoroutine(RefreshAfterDelay(0.1f));
        }

        private void ApplyPenalties()
        {
            Score = Mathf.Max(0f, Score - collisionScorePenalty);
            CommunicationFraction = Mathf.Max(0f, CommunicationFraction - 0.15f);
            EnergyFraction        = Mathf.Max(0f, EnergyFraction        - 0.10f);

            OnResourcesChanged?.Invoke(Score, CommunicationFraction, EnergyFraction);

            if (Score <= 0f)
                OnCascadeStarted?.Invoke();
        }

        private void SpawnDebrisCloud(Vector3 origin)
        {
            if (debrisPrefab == null || debrisParent == null) return;

            for (int i = 0; i < debrisPerCollision; i++)
            {
                // Create a minimal DebrisData in memory.
                DebrisData d = ScriptableObject.CreateInstance<DebrisData>();
                d.objectName       = $"Fragment-{UnityEngine.Random.Range(1000, 9999)}";
                d.fragmentType     = DebrisFragmentType.SmallFragment;
                d.source           = DebrisSource.Collision;
                d.sizeCm           = UnityEngine.Random.Range(5f, 50f);
                d.displayColor     = new Color(0.8f, 0.5f, 0.2f);
                d.displaySize      = UnityEngine.Random.Range(0.03f, 0.06f);
                d.angularSpeed     = UnityEngine.Random.Range(2f, 9f);
                d.orbitalInclination = UnityEngine.Random.Range(-30f, 30f);
                d.startAngle       = UnityEngine.Random.Range(0f, 360f);
                d.orbitType        = OrbitType.LEO;

                GameObject go = Instantiate(debrisPrefab, origin, Quaternion.identity, debrisParent);
                go.name = d.objectName;

                DebrisObject dObj = go.GetComponent<DebrisObject>();
                if (dObj != null)
                {
                    dObj.SetData(d);
                    // Orbit at the same radius as the collision point.
                    float alt = origin.magnitude - 0.5f + UnityEngine.Random.Range(-0.15f, 0.15f);
                    dObj.SetOrbitAltitudeUnits(Mathf.Max(0.1f, alt));
                }
            }

            // Let risk calculator know new debris exists.
            if (riskCalculator != null)
                StartCoroutine(RefreshAfterDelay(0.1f));
        }

        private IEnumerator RefreshAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            riskCalculator?.RefreshObjectLists();
        }

        private void ClearPair(SatelliteObject a, SatelliteObject b)
        {
            if (a != null && b != null)
            {
                var pair = a.GetInstanceID() < b.GetInstanceID() ? (a, b) : (b, a);
                scheduledCollisions.Remove(pair);
            }
        }
    }
}
