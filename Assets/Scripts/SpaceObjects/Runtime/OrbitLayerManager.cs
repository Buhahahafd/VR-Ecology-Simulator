using UnityEngine;

namespace SpaceDebris
{
    /// <summary>
    /// Singleton that determines orbital layer boundaries (LEO/MEO/GEO) from scene markers
    /// and provides an API for classifying objects by their current orbit layer.
    /// Attach to the /Orbits GameObject which already contains the marker children.
    /// </summary>
    public class OrbitLayerManager : MonoBehaviour
    {
        /// <summary>Singleton instance.</summary>
        public static OrbitLayerManager Instance { get; private set; }

        // ── Fallback values (scene units above Earth surface) ────────────────
        private const float FallbackLeoMin = 6.5f;
        private const float FallbackLeoMax = 25f;
        private const float FallbackMeoMin = 30f;
        private const float FallbackMeoMax = 57.5f;
        private const float FallbackGeoMin = 62.5f;
        private const float FallbackGeoMax = 82.5f;

        // ── Cached bounds ────────────────────────────────────────────────────
        private OrbitLayerBounds leoBounds;
        private OrbitLayerBounds meoBounds;
        private OrbitLayerBounds geoBounds;

        private float earthVisualRadius;
        private bool initialized;

        // ── Marker names ─────────────────────────────────────────────────────
        private const string LeoInnerPath = "OrbitLEO/OrbitLEO";
        private const string LeoOuterPath = "OrbitLEO/OrbitLEO2";
        private const string MeoInnerPath = "OrbitMEO/OrbitMEO";
        private const string MeoOuterPath = "OrbitMEO/OrbitMEO2";
        private const string GeoInnerPath = "OrbitGEO/GEO";
        private const string GeoOuterPath = "OrbitGEO/GEO2";

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            InitializeBounds();
        }

        private void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        // ── Public API ───────────────────────────────────────────────────────

        /// <summary>Returns the min/max altitude bounds for the given orbit layer.</summary>
        public OrbitLayerBounds GetLayerBounds(OrbitType type)
        {
            return type switch
            {
                OrbitType.LEO => leoBounds,
                OrbitType.MEO => meoBounds,
                OrbitType.GEO => geoBounds,
                OrbitType.HEO => geoBounds, // fallback HEO → GEO
                _ => leoBounds
            };
        }

        /// <summary>Classifies an altitude (scene units above Earth surface) into an orbit layer.</summary>
        public OrbitType ClassifyByAltitude(float altitudeUnits)
        {
            if (altitudeUnits <= leoBounds.MaxAltitude)
                return OrbitType.LEO;
            if (altitudeUnits <= meoBounds.MaxAltitude)
                return OrbitType.MEO;
            return OrbitType.GEO;
        }

        /// <summary>Classifies a specific orbital object by its current altitude.</summary>
        public OrbitType ClassifyObject(OrbitalObject obj)
        {
            if (obj == null) return OrbitType.LEO;
            return ClassifyByAltitude(obj.OrbitAltitudeUnits);
        }

        /// <summary>Returns a random altitude within the given orbit layer bounds.</summary>
        public float GetRandomAltitudeInLayer(OrbitType type)
        {
            OrbitLayerBounds bounds = GetLayerBounds(type);
            return Random.Range(bounds.MinAltitude, bounds.MaxAltitude);
        }

        /// <summary>Returns the adjacent orbit layer in the given direction (+1 = higher, -1 = lower).</summary>
        public OrbitType GetAdjacentLayer(OrbitType current, int direction)
        {
            int index = (int)current + direction;
            // Clamp to LEO..GEO range (0..2).
            index = Mathf.Clamp(index, 0, 2);
            return (OrbitType)index;
        }

        // ── Initialization ───────────────────────────────────────────────────

        private void InitializeBounds()
        {
            // Resolve Earth visual radius.
            GameObject earthObj = GameObject.Find("Earth");
            earthVisualRadius = earthObj != null
                ? earthObj.transform.lossyScale.x * 0.5f
                : 5f; // fallback for scale 10 Earth

            bool success = true;

            success &= TryResolveBounds(LeoInnerPath, LeoOuterPath, out leoBounds);
            success &= TryResolveBounds(MeoInnerPath, MeoOuterPath, out meoBounds);
            success &= TryResolveBounds(GeoInnerPath, GeoOuterPath, out geoBounds);

            if (!success)
            {
                Debug.LogWarning("[OrbitLayerManager] Some orbit markers not found. Using fallback values.");
                if (leoBounds.MaxAltitude <= 0f)
                    leoBounds = new OrbitLayerBounds(FallbackLeoMin, FallbackLeoMax);
                if (meoBounds.MaxAltitude <= 0f)
                    meoBounds = new OrbitLayerBounds(FallbackMeoMin, FallbackMeoMax);
                if (geoBounds.MaxAltitude <= 0f)
                    geoBounds = new OrbitLayerBounds(FallbackGeoMin, FallbackGeoMax);
            }

            initialized = true;

            Debug.Log($"[OrbitLayerManager] Bounds initialized — " +
                      $"LEO: [{leoBounds.MinAltitude:F1}, {leoBounds.MaxAltitude:F1}], " +
                      $"MEO: [{meoBounds.MinAltitude:F1}, {meoBounds.MaxAltitude:F1}], " +
                      $"GEO: [{geoBounds.MinAltitude:F1}, {geoBounds.MaxAltitude:F1}]");
        }

        private bool TryResolveBounds(string innerPath, string outerPath, out OrbitLayerBounds bounds)
        {
            Transform inner = transform.Find(innerPath);
            Transform outer = transform.Find(outerPath);

            if (inner == null || outer == null)
            {
                bounds = default;
                return false;
            }

            float innerRadius = inner.lossyScale.x * 0.5f;
            float outerRadius = outer.lossyScale.x * 0.5f;

            float minAlt = innerRadius - earthVisualRadius;
            float maxAlt = outerRadius - earthVisualRadius;

            bounds = new OrbitLayerBounds(
                Mathf.Max(0f, minAlt),
                Mathf.Max(0f, maxAlt)
            );
            return true;
        }
    }

    /// <summary>
    /// Defines the altitude range of an orbital layer in scene units above Earth's surface.
    /// </summary>
    public readonly struct OrbitLayerBounds
    {
        public readonly float MinAltitude;
        public readonly float MaxAltitude;

        public OrbitLayerBounds(float min, float max)
        {
            MinAltitude = min;
            MaxAltitude = max;
        }
    }
}
