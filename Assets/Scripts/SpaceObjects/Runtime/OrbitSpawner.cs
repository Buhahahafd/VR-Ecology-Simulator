using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpaceDebris
{
    /// <summary>
    /// Instantiates and manages orbital objects at runtime.
    /// Supports two modes:
    ///   - Manual:      spawns objects from a fixed OrbitSpawnConfig asset.
    ///   - Procedural:  generates objects from ProceduralSimulationConfig ranges each run.
    /// Call Regenerate() at runtime to clear the current simulation and spawn a new one.
    /// Fires OnObjectSpawned after each object is fully configured (data, central body, altitude)
    /// so style managers can apply visual overrides.
    /// </summary>
    public class OrbitSpawner : MonoBehaviour
    {
        /// <summary>Raised after each OrbitalObject is spawned and configured.</summary>
        public event Action<OrbitalObject> OnObjectSpawned;
        // ── Mode ────────────────────────────────────────────────────────────────

        [Header("Spawn Mode")]
        [Tooltip("Procedural: generate objects randomly from config ranges each run.\n" +
                 "Manual: use the fixed OrbitSpawnConfig asset.")]
        [SerializeField] private SpawnMode spawnMode = SpawnMode.Procedural;

        // ── Manual config ────────────────────────────────────────────────────────

        [Header("Manual Configuration")]
        [Tooltip("Used only when Spawn Mode = Manual.")]
        [SerializeField] private OrbitSpawnConfig spawnConfig;

        // ── Procedural config ────────────────────────────────────────────────────

        [Header("Procedural Configuration")]
        [Tooltip("Used only when Spawn Mode = Procedural.")]
        [SerializeField] private ProceduralSimulationConfig proceduralConfig;

        // ── Prefabs ──────────────────────────────────────────────────────────────

        [Header("Prefabs")]
        [SerializeField] private GameObject satellitePrefab;
        [SerializeField] private GameObject debrisPrefab;

        // ── References ───────────────────────────────────────────────────────────

        [Header("References")]
        [SerializeField] private RiskCalculator riskCalculator;

        [Tooltip("The Earth Transform. All orbital objects orbit around it.")]
        [SerializeField] private Transform earthTransform;

        [Header("Organization")]
        [SerializeField] private Transform satelliteParent;
        [SerializeField] private Transform debrisParent;

        // ── Orbit path visual settings ───────────────────────────────────────────

        [Header("Orbit Path Lines")]
        [Tooltip("Number of line segments per orbit circle. Higher = smoother.")]
        [SerializeField] private int orbitPathSegments = 96;

        [Tooltip("Width of satellite orbit path lines in world units.")]
        [SerializeField] private float orbitPathWidth = 0.0008f;

        [Tooltip("Opacity of satellite orbit path lines.")]
        [Range(0f, 1f)]
        [SerializeField] private float satellitePathAlpha = 0.07f;

        [Tooltip("Opacity of debris orbit path lines.")]
        [Range(0f, 1f)]
        [SerializeField] private float debrisPathAlpha = 0.04f;

        // ── Altitude fallback for manual mode ────────────────────────────────────

        [Header("Manual Mode Altitude")]
        [Tooltip("Orbit altitude in scene units applied to manually-authored objects " +
                 "that do not have an altitude baked in.")]
        [SerializeField] private float manualDefaultAltitudeUnits = 0.5f;

        // ── State ────────────────────────────────────────────────────────────────

        private readonly List<GameObject> spawnedObjects = new();
        private int lastUsedSeed = -1;

        // ── Unity lifecycle ──────────────────────────────────────────────────────

        private void Start()
        {
            ResolveEarth();
            SpawnAll();
        }

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>
        /// Destroys all currently spawned objects and runs the simulation again.
        /// In Procedural mode a new seed is used unless the config has a fixed seed.
        /// </summary>
        public void Regenerate()
        {
            ClearAll();
            ResolveEarth();
            SpawnAll();
        }

        /// <summary>Returns the seed used for the last procedural generation pass.</summary>
        public int LastUsedSeed => lastUsedSeed;

        // ── Private: spawn logic ─────────────────────────────────────────────────

        private void SpawnAll()
        {
            if (spawnMode == SpawnMode.Procedural)
                SpawnProcedural();
            else
                SpawnManual();

            riskCalculator?.RefreshObjectLists();
        }

        private void SpawnProcedural()
        {
            if (proceduralConfig == null)
            {
                Debug.LogError("[OrbitSpawner] ProceduralSimulationConfig is not assigned.");
                return;
            }

            GenerationResult result = ProceduralOrbitGenerator.Generate(proceduralConfig);
            lastUsedSeed = result.Seed;
            Debug.Log($"[OrbitSpawner] Procedural generation — seed: {result.Seed}, " +
                      $"satellites: {result.Satellites.Count}, debris: {result.Debris.Count}");

            foreach (SatelliteData sat in result.Satellites)
            {
                SpawnSatellite(sat, sat.altitudeSceneUnits);
            }

            foreach (DebrisData deb in result.Debris)
            {
                float altUnits = UnityEngine.Random.Range(proceduralConfig.minDebrisAltitude, proceduralConfig.maxDebrisAltitude);
                SpawnDebris(deb, altUnits);
            }
        }

        private void SpawnManual()
        {
            if (spawnConfig == null)
            {
                Debug.LogError("[OrbitSpawner] OrbitSpawnConfig is not assigned.");
                return;
            }

            foreach (SatelliteData sat in spawnConfig.satellites)
                SpawnSatellite(sat, manualDefaultAltitudeUnits);

            foreach (DebrisData deb in spawnConfig.debrisList)
                SpawnDebris(deb, manualDefaultAltitudeUnits);
        }

        // ── Private: instantiation ───────────────────────────────────────────────

        private void SpawnSatellite(SatelliteData data, float altitudeUnits)
        {
            if (satellitePrefab == null)
            {
                Debug.LogError("[OrbitSpawner] satellitePrefab is not assigned.");
                return;
            }

            GameObject go = Instantiate(satellitePrefab, Vector3.zero, Quaternion.identity, satelliteParent);
            go.name = data.objectName;
            spawnedObjects.Add(go);

            SatelliteObject sat = go.GetComponent<SatelliteObject>();
            if (sat == null)
            {
                Debug.LogError("[OrbitSpawner] satellitePrefab is missing SatelliteObject component.");
                return;
            }

            sat.SetData(data);
            sat.SetCentralBody(earthTransform);
            sat.SetOrbitAltitudeUnits(altitudeUnits);

            AttachOrbitPath(go, satellitePathAlpha, orbitPathWidth);

            // Ensure selection highlight component is present.
            if (!go.TryGetComponent<OrbitalObjectHighlight>(out _))
                go.AddComponent<OrbitalObjectHighlight>();

            OnObjectSpawned?.Invoke(sat);
        }

        private void SpawnDebris(DebrisData data, float altitudeUnits)
        {
            if (debrisPrefab == null)
            {
                Debug.LogError("[OrbitSpawner] debrisPrefab is not assigned.");
                return;
            }

            GameObject go = Instantiate(debrisPrefab, Vector3.zero, Quaternion.identity, debrisParent);
            go.name = data.objectName;
            spawnedObjects.Add(go);

            DebrisObject debris = go.GetComponent<DebrisObject>();
            if (debris == null)
            {
                Debug.LogError("[OrbitSpawner] debrisPrefab is missing DebrisObject component.");
                return;
            }

            debris.SetData(data);
            debris.SetCentralBody(earthTransform);
            debris.SetOrbitAltitudeUnits(altitudeUnits);

            AttachOrbitPath(go, debrisPathAlpha, orbitPathWidth * 0.67f);

            // Ensure selection highlight component is present.
            if (!go.TryGetComponent<OrbitalObjectHighlight>(out _))
                go.AddComponent<OrbitalObjectHighlight>();

            OnObjectSpawned?.Invoke(debris);
        }

        // ── Private: orbit path ──────────────────────────────────────────────────

        private void AttachOrbitPath(GameObject go, float alpha, float width)
        {
            LineRenderer lr = go.GetComponent<LineRenderer>();
            if (lr == null)
                lr = go.AddComponent<LineRenderer>();

            lr.loop = true;
            lr.useWorldSpace = true;
            lr.shadowCastingMode = ShadowCastingMode.Off;
            lr.receiveShadows = false;
            lr.generateLightingData = false;
            lr.alignment = LineAlignment.View;
            lr.startWidth = width;
            lr.endWidth = width;
            lr.positionCount = orbitPathSegments;

            OrbitPathRenderer opr = go.GetComponent<OrbitPathRenderer>();
            if (opr == null)
                opr = go.AddComponent<OrbitPathRenderer>();

            opr.Configure(orbitPathSegments, width, alpha);
        }

        // ── Private: cleanup ─────────────────────────────────────────────────────

        private void ClearAll()
        {
            foreach (GameObject go in spawnedObjects)
            {
                if (go != null)
                    Destroy(go);
            }
            spawnedObjects.Clear();
        }

        private void ResolveEarth()
        {
            if (earthTransform != null) return;

            GameObject earthObj = GameObject.Find("Earth");
            if (earthObj != null)
                earthTransform = earthObj.transform;
            else
                Debug.LogWarning("[OrbitSpawner] earthTransform not assigned and 'Earth' not found.");
        }
    }

    public enum SpawnMode
    {
        Procedural,
        Manual
    }
}
