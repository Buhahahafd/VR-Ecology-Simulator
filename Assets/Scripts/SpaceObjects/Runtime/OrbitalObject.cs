using UnityEngine;

namespace SpaceDebris
{
    /// <summary>
    /// Moves a GameObject along a circular inclined orbit around a central body (Earth).
    /// The orbit radius is driven by SpaceObjectData.altitudeKm scaled to scene units.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public abstract class OrbitalObject : MonoBehaviour
    {
        [Header("Orbital Object")]
        [SerializeField] protected SpaceObjectData data;

        [Tooltip("The Transform of the central body (Earth). Assigned automatically if left empty.")]
        [SerializeField] private Transform centralBody;

        [Tooltip("Distance from Earth's visual surface in scene units. " +
                 "Earth visual radius = localScale.x * 0.5. " +
                 "Set 0.2–2.0 to orbit visibly above the surface.")]
        [SerializeField] private float orbitAltitudeUnits = 0.5f;

        protected Renderer objectRenderer;
        private float currentAngle;
        private float orbitRadius;
        private Quaternion orbitalPlaneRotation;

        protected virtual void Awake()
        {
            objectRenderer = GetComponent<Renderer>();
        }

        protected virtual void Start()
        {
            if (data == null)
            {
                Debug.LogWarning($"[OrbitalObject] {name}: SpaceObjectData is not assigned.");
                return;
            }

            // Auto-find Earth if not assigned in Inspector.
            if (centralBody == null)
            {
                GameObject earthObj = GameObject.Find("Earth");
                if (earthObj != null)
                    centralBody = earthObj.transform;
                else
                    Debug.LogWarning($"[OrbitalObject] {name}: centralBody not assigned and 'Earth' GameObject not found. Orbiting world origin.");
            }

            // Visual radius of Earth in world units = half of lossy scale (unit sphere).
            float earthVisualRadius = centralBody != null ? centralBody.lossyScale.x * 0.5f : 300f;

            // Orbit radius = Earth surface + explicit altitude offset in scene units.
            orbitRadius = earthVisualRadius + orbitAltitudeUnits;
            currentAngle = data.startAngle;
            orbitalPlaneRotation = Quaternion.Euler(data.orbitalInclination, 0f, 0f);

            // Enable the emission keyword so _EmissionColor is picked up by URP.
            objectRenderer.material.EnableKeyword("_EMISSION");

            ApplyDisplaySettings();
        }

        protected virtual void Update()
        {
            if (data == null) return;

            currentAngle += data.angularSpeed * Time.deltaTime;
            if (currentAngle >= 360f) currentAngle -= 360f;

            float rad = currentAngle * Mathf.Deg2Rad;
            Vector3 localOffset = new Vector3(Mathf.Cos(rad), 0f, Mathf.Sin(rad)) * orbitRadius;

            // Orbit around the central body's world position (Earth), not the world origin.
            Vector3 center = centralBody != null ? centralBody.position : Vector3.zero;
            transform.position = center + orbitalPlaneRotation * localOffset;
        }

        /// <summary>Sets the orbit altitude above Earth's surface in scene units at runtime.</summary>
        public void SetOrbitAltitudeUnits(float units)
        {
            orbitAltitudeUnits = units;
        }

        /// <summary>Returns the assigned SpaceObjectData.</summary>
        public SpaceObjectData GetData() => data;

        /// <summary>
        /// Assigns data at runtime (used by OrbitSpawner).
        /// </summary>
        public void SetData(SpaceObjectData spaceObjectData)
        {
            data = spaceObjectData;
        }

        /// <summary>
        /// Assigns the central body transform at runtime (used by OrbitSpawner).
        /// </summary>
        public void SetCentralBody(Transform body)
        {
            centralBody = body;
        }

        /// <summary>
        /// Applies size and base colour from data to the renderer.
        /// Override in subclasses to set specific colours.
        /// </summary>
        protected abstract void ApplyDisplaySettings();
    }
}
