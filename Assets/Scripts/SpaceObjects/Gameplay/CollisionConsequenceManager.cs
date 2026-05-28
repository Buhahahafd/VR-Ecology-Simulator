using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceDebris
{
    /// <summary>
    /// Monitors high-risk satellite pairs and debris-satellite pairs.
    /// If neither object has its orbit changed before the collision timer expires,
    /// a collision is simulated:
    ///   1. Satellites are destroyed; debris persists (realistic).
    ///   2. New DebrisObject instances spawn at the collision point.
    ///   3. Player resources (communication, energy, score) are reduced.
    ///   4. A chain-reaction check schedules further collisions (Kessler cascade).
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

        [Header("Kessler Cascade")]
        [Tooltip("Debris count multiplier once cascade threshold is reached.")]
        [SerializeField] private int cascadeDebrisMultiplier = 2;

        [Tooltip("Probability that a spawned fragment migrates to an adjacent orbit layer.")]
        [SerializeField, Range(0f, 1f)] private float debrisMigrationChance = 0.25f;

        [Tooltip("Angular speed boost applied to cascade debris.")]
        [SerializeField] private float cascadeSpeedBoost = 1.5f;

        [Tooltip("Maximum debris objects per orbit layer (performance cap).")]
        [SerializeField] private int maxDebrisPerLayer = 50;

        [Tooltip("Number of collisions before cascade multiplier activates.")]
        [SerializeField] private int cascadeThreshold = 3;

        [Header("References")]
        [SerializeField] private RiskCalculator riskCalculator;

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Raised whenever a collision occurs. Param: world position.</summary>
        public event Action<Vector3> OnCollision;

        /// <summary>Raised when resources change. Params: score, communicationFraction, energyFraction.</summary>
        public event Action<float, float, float> OnResourcesChanged;

        /// <summary>Raised when cascade conditions are met — score zero or collision count exceeds threshold.</summary>
        public event Action OnCascadeStarted;

        // ── Public state ──────────────────────────────────────────────────────

        public float Score                  { get; private set; }
        public float CommunicationFraction  { get; private set; } = 1f;
        public float EnergyFraction         { get; private set; } = 1f;

        /// <summary>Total collision count this session.</summary>
        public int CollisionCount => collisionCount;

        // ── Private state ─────────────────────────────────────────────────────

        // Tracks pairs already scheduled for collision to avoid duplicates.
        private readonly HashSet<(SatelliteObject, SatelliteObject)> scheduledCollisions = new();

        // Tracks debris-satellite pairs already scheduled.
        private readonly HashSet<(int, int)> scheduledDebrisCollisions = new();

        // Cooldown per debris to prevent one debris from causing rapid repeated collisions.
        private readonly Dictionary<int, float> debrisCollisionCooldowns = new();
        private const float DebrisCooldownDuration = 5f;

        private int collisionCount;
        private bool cascadeTriggered;

        // ── Layer penalty multipliers ────────────────────────────────────────
        private const float LeoPenaltyMultiplier = 1f;
        private const float MeoPenaltyMultiplier = 1.5f;
        private const float GeoPenaltyMultiplier = 3f;

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
            {
                riskCalculator.OnSatelliteRiskChanged += HandleRiskChanged;
                riskCalculator.OnDebrisRiskChanged    += HandleDebrisRiskChanged;
            }
        }

        private void OnDestroy()
        {
            if (riskCalculator != null)
            {
                riskCalculator.OnSatelliteRiskChanged -= HandleRiskChanged;
                riskCalculator.OnDebrisRiskChanged    -= HandleDebrisRiskChanged;
            }
        }

        private void Update()
        {
            // Tick cooldowns.
            if (debrisCollisionCooldowns.Count > 0)
            {
                List<int> expired = null;
                foreach (var kvp in debrisCollisionCooldowns)
                {
                    if (Time.time > kvp.Value)
                    {
                        expired ??= new List<int>();
                        expired.Add(kvp.Key);
                    }
                }
                if (expired != null)
                {
                    foreach (int id in expired)
                        debrisCollisionCooldowns.Remove(id);
                }
            }
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

        // ── Private: satellite-satellite risk response ────────────────────────

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

        // ── Private: debris-satellite risk response ──────────────────────────

        private void HandleDebrisRiskChanged(SatelliteObject sat, DebrisObject debris, RiskLevel risk)
        {
            if (risk != RiskLevel.High || sat == null || debris == null) return;

            int debrisId = debris.GetInstanceID();
            int satId = sat.GetInstanceID();

            // Check cooldown — prevent rapid repeated hits from same debris.
            if (debrisCollisionCooldowns.ContainsKey(debrisId)) return;

            // Canonical pair key.
            var pairKey = satId < debrisId ? (satId, debrisId) : (debrisId, satId);
            if (scheduledDebrisCollisions.Contains(pairKey)) return;
            scheduledDebrisCollisions.Add(pairKey);

            float tca = riskCalculator.GetEstimatedTimeToClosestApproach(sat);
            float delay = tca < float.MaxValue ? Mathf.Max(tca, 1f) : 10f;

            StartCoroutine(DebrisCollisionCountdown(sat, debris, pairKey, delay));
        }

        // ── Coroutines ───────────────────────────────────────────────────────

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

        private IEnumerator DebrisCollisionCountdown(SatelliteObject sat, DebrisObject debris,
            (int, int) pairKey, float delay)
        {
            float elapsed = 0f;

            while (elapsed < delay)
            {
                elapsed += Time.deltaTime;

                if (sat == null || debris == null)
                {
                    scheduledDebrisCollisions.Remove(pairKey);
                    yield break;
                }

                RiskLevel risk = riskCalculator != null
                    ? riskCalculator.GetSatelliteRiskLevel(sat)
                    : RiskLevel.Low;

                if (risk != RiskLevel.High)
                {
                    scheduledDebrisCollisions.Remove(pairKey);
                    yield break;
                }

                yield return null;
            }

            if (sat != null && debris != null)
                SimulateDebrisSatelliteCollision(sat, debris);

            scheduledDebrisCollisions.Remove(pairKey);
        }

        // ── Collision simulation ─────────────────────────────────────────────

        private void SimulateCollision(SatelliteObject a, SatelliteObject b)
        {
            Vector3 collisionPoint = (a.transform.position + b.transform.position) * 0.5f;
            OrbitType layer = DetermineCollisionLayer(a, b);

            Debug.Log($"[ConsequenceManager] Sat-Sat collision: {a.name} x {b.name} at {collisionPoint} (layer: {layer})");

            collisionCount++;
            OnCollision?.Invoke(collisionPoint);
            ApplyPenalties(layer);
            SpawnDebrisCloud(collisionPoint, layer);

            // Destroy both satellites.
            var ctrlA = a.GetComponent<SatelliteController>();
            var ctrlB = b.GetComponent<SatelliteController>();

            if (ctrlA != null) ctrlA.TriggerCollision();
            else Destroy(a.gameObject);

            if (ctrlB != null) ctrlB.TriggerCollision();
            else Destroy(b.gameObject);

            CheckCascade();

            // Refresh risk system after objects are removed.
            if (riskCalculator != null)
                StartCoroutine(RefreshAfterDelay(0.1f));
        }

        /// <summary>
        /// Simulates a debris-satellite collision. The satellite is destroyed,
        /// the debris persists (realistic), and new fragments are spawned.
        /// </summary>
        private void SimulateDebrisSatelliteCollision(SatelliteObject sat, DebrisObject debris)
        {
            Vector3 collisionPoint = (sat.transform.position + debris.transform.position) * 0.5f;
            OrbitType layer = DetermineCollisionLayer(sat, debris);

            Debug.Log($"[ConsequenceManager] Debris-Sat collision: {debris.name} → {sat.name} at {collisionPoint} (layer: {layer})");

            collisionCount++;
            OnCollision?.Invoke(collisionPoint);
            ApplyPenalties(layer);
            SpawnDebrisCloud(collisionPoint, layer);

            // Destroy the satellite only; debris persists realistically.
            var ctrl = sat.GetComponent<SatelliteController>();
            if (ctrl != null) ctrl.TriggerCollision();
            else Destroy(sat.gameObject);

            // Set cooldown on debris so it doesn't immediately trigger again.
            debrisCollisionCooldowns[debris.GetInstanceID()] = Time.time + DebrisCooldownDuration;

            CheckCascade();

            if (riskCalculator != null)
                StartCoroutine(RefreshAfterDelay(0.1f));
        }

        // ── Penalties ────────────────────────────────────────────────────────

        private void ApplyPenalties(OrbitType layer = OrbitType.LEO)
        {
            float multiplier = layer switch
            {
                OrbitType.GEO => GeoPenaltyMultiplier,
                OrbitType.MEO => MeoPenaltyMultiplier,
                _             => LeoPenaltyMultiplier
            };

            Score = Mathf.Max(0f, Score - collisionScorePenalty * multiplier);
            CommunicationFraction = Mathf.Max(0f, CommunicationFraction - 0.15f * multiplier);
            EnergyFraction        = Mathf.Max(0f, EnergyFraction        - 0.10f * multiplier);

            OnResourcesChanged?.Invoke(Score, CommunicationFraction, EnergyFraction);
        }

        // ── Debris spawning with layer migration ─────────────────────────────

        private void SpawnDebrisCloud(Vector3 origin, OrbitType collisionLayer = OrbitType.LEO)
        {
            if (debrisPrefab == null || debrisParent == null) return;

            int count = GetEffectiveDebrisCount();

            for (int i = 0; i < count; i++)
            {
                // Determine target layer: migrate to adjacent with debrisMigrationChance.
                OrbitType targetLayer = collisionLayer;
                float targetAlt;

                if (OrbitLayerManager.Instance != null)
                {
                    if (UnityEngine.Random.value < debrisMigrationChance)
                    {
                        int direction = UnityEngine.Random.value > 0.5f ? 1 : -1;
                        targetLayer = OrbitLayerManager.Instance.GetAdjacentLayer(collisionLayer, direction);
                    }

                    // Check layer debris cap.
                    if (CountDebrisInLayer(targetLayer) >= maxDebrisPerLayer)
                        continue;

                    targetAlt = OrbitLayerManager.Instance.GetRandomAltitudeInLayer(targetLayer);
                }
                else
                {
                    // Fallback without OrbitLayerManager.
                    targetAlt = origin.magnitude - 0.5f + UnityEngine.Random.Range(-0.15f, 0.15f);
                    targetAlt = Mathf.Max(0.1f, targetAlt);
                }

                // Create a minimal DebrisData in memory.
                DebrisData d = ScriptableObject.CreateInstance<DebrisData>();
                d.objectName       = $"Fragment-{UnityEngine.Random.Range(1000, 9999)}";
                d.fragmentType     = DebrisFragmentType.SmallFragment;
                d.source           = DebrisSource.Collision;
                d.sizeCm           = UnityEngine.Random.Range(5f, 50f);
                d.displayColor     = new Color(0.8f, 0.5f, 0.2f);
                d.displaySize      = UnityEngine.Random.Range(0.03f, 0.06f);
                d.orbitType        = targetLayer;
                d.orbitalInclination = UnityEngine.Random.Range(-30f, 30f);
                d.startAngle       = UnityEngine.Random.Range(0f, 360f);

                // Cascade debris moves faster.
                float baseSpeed = UnityEngine.Random.Range(2f, 9f);
                d.angularSpeed = collisionCount > cascadeThreshold
                    ? baseSpeed * cascadeSpeedBoost
                    : baseSpeed;

                GameObject go = Instantiate(debrisPrefab, origin, Quaternion.identity, debrisParent);
                go.name = d.objectName;

                DebrisObject dObj = go.GetComponent<DebrisObject>();
                if (dObj != null)
                {
                    dObj.SetData(d);
                    dObj.SetOrbitAltitudeUnits(Mathf.Max(0.1f, targetAlt));
                }
            }

            // Let risk calculator know new debris exists.
            if (riskCalculator != null)
                StartCoroutine(RefreshAfterDelay(0.1f));
        }

        // ── Cascade logic ────────────────────────────────────────────────────

        /// <summary>Returns the effective debris count, accounting for cascade multiplier.</summary>
        private int GetEffectiveDebrisCount()
        {
            if (collisionCount > cascadeThreshold)
                return debrisPerCollision * cascadeDebrisMultiplier;
            return debrisPerCollision;
        }

        private void CheckCascade()
        {
            bool shouldTrigger = Score <= 0f || collisionCount > cascadeThreshold;
            if (shouldTrigger && !cascadeTriggered)
            {
                cascadeTriggered = true;
                Debug.LogWarning($"[ConsequenceManager] Kessler cascade triggered! Collisions: {collisionCount}, Score: {Score}");
                OnCascadeStarted?.Invoke();
            }
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        private OrbitType DetermineCollisionLayer(OrbitalObject a, OrbitalObject b)
        {
            if (OrbitLayerManager.Instance != null)
                return OrbitLayerManager.Instance.ClassifyObject(a);
            return a != null && a.GetData() != null ? a.GetData().orbitType : OrbitType.LEO;
        }

        private int CountDebrisInLayer(OrbitType layer)
        {
            if (OrbitLayerManager.Instance == null) return 0;

            int count = 0;
            DebrisObject[] allDebris = FindObjectsByType<DebrisObject>(FindObjectsSortMode.None);
            foreach (DebrisObject d in allDebris)
            {
                if (d != null && OrbitLayerManager.Instance.ClassifyObject(d) == layer)
                    count++;
            }
            return count;
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
