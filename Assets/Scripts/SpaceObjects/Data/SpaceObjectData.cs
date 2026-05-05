using UnityEngine;

namespace SpaceDebris
{
    /// <summary>
    /// Base ScriptableObject with shared fields for any orbital object (satellite or debris).
    /// </summary>
    public abstract class SpaceObjectData : ScriptableObject
    {
        [Header("Identity")]
        public string objectName = "Unknown";
        public OrbitType orbitType = OrbitType.LEO;

        [Header("Orbital Parameters")]
        [Tooltip("Altitude above Earth surface in kilometres.")]
        public float altitudeKm = 400f;

        [Tooltip("Angular velocity around the orbit in degrees per second.")]
        public float angularSpeed = 2f;

        [Tooltip("Tilt of the orbital plane in degrees.")]
        public float orbitalInclination = 0f;

        [Tooltip("Starting angle in degrees along the orbit.")]
        public float startAngle = 0f;
    }

    public enum OrbitType
    {
        LEO,  // Low Earth Orbit  160–2 000 km
        MEO,  // Medium            2 000–35 786 km
        GEO,  // Geostationary    35 786 km
        HEO   // Highly Elliptical
    }
}
