using UnityEngine;

namespace SpaceDebris
{
    /// <summary>
    /// Instantiates satellite and debris objects at runtime from an OrbitSpawnConfig.
    /// Notifies RiskCalculator to refresh its lists after spawning.
    /// </summary>
    public class OrbitSpawner : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField] private OrbitSpawnConfig spawnConfig;

        [Header("Prefabs")]
        [SerializeField] private GameObject satellitePrefab;
        [SerializeField] private GameObject debrisPrefab;

        [Header("References")]
        [SerializeField] private RiskCalculator riskCalculator;

        [Tooltip("The Earth Transform. All orbital objects will orbit around it.")]
        [SerializeField] private Transform earthTransform;

        [Header("Organization")]
        [SerializeField] private Transform satelliteParent;
        [SerializeField] private Transform debrisParent;

        private void Start()
        {
            if (spawnConfig == null)
            {
                Debug.LogError("[OrbitSpawner] OrbitSpawnConfig is not assigned.");
                return;
            }

            SpawnAll();
        }

        private void SpawnAll()
        {
            // Auto-find Earth if not assigned.
            if (earthTransform == null)
            {
                GameObject earthObj = GameObject.Find("Earth");
                if (earthObj != null)
                    earthTransform = earthObj.transform;
                else
                    Debug.LogWarning("[OrbitSpawner] earthTransform not assigned and 'Earth' not found. Objects will orbit world origin.");
            }

            foreach (SatelliteData satData in spawnConfig.satellites)
            {
                SpawnSatellite(satData);
            }

            foreach (DebrisData debData in spawnConfig.debrisList)
            {
                SpawnDebris(debData);
            }

            riskCalculator?.RefreshObjectLists();
        }

        private void SpawnSatellite(SatelliteData data)
        {
            if (satellitePrefab == null)
            {
                Debug.LogError("[OrbitSpawner] satellitePrefab is not assigned.");
                return;
            }

            GameObject go = Instantiate(satellitePrefab, Vector3.zero, Quaternion.identity, satelliteParent);
            go.name = data.objectName;

            SatelliteObject sat = go.GetComponent<SatelliteObject>();
            if (sat == null)
            {
                Debug.LogError("[OrbitSpawner] satellitePrefab is missing SatelliteObject component.");
                return;
            }

            sat.SetData(data);
            sat.SetCentralBody(earthTransform);
        }

        private void SpawnDebris(DebrisData data)
        {
            if (debrisPrefab == null)
            {
                Debug.LogError("[OrbitSpawner] debrisPrefab is not assigned.");
                return;
            }

            GameObject go = Instantiate(debrisPrefab, Vector3.zero, Quaternion.identity, debrisParent);
            go.name = data.objectName;

            DebrisObject debris = go.GetComponent<DebrisObject>();
            if (debris == null)
            {
                Debug.LogError("[OrbitSpawner] debrisPrefab is missing DebrisObject component.");
                return;
            }

            debris.SetData(data);
            debris.SetCentralBody(earthTransform);
        }
    }
}
