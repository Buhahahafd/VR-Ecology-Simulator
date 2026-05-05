using UnityEngine;

namespace SpaceDebris
{
    /// <summary>
    /// ScriptableObject describing a satellite's mission type, status and display parameters.
    /// </summary>
    [CreateAssetMenu(fileName = "NewSatelliteData", menuName = "Space Debris/Satellite Data")]
    public class SatelliteData : SpaceObjectData
    {
        [Header("Satellite Properties")]
        public SatelliteType satelliteType = SatelliteType.Communication;
        public SatelliteStatus status = SatelliteStatus.Active;

        [Header("Speed Description")]
        public SpeedLevel speedLevel = SpeedLevel.High;

        [Header("Display")]
        [Tooltip("Colour of the glowing point in scene.")]
        public Color displayColor = Color.cyan;

        [Tooltip("World-space scale of the glowing sphere.")]
        public float displaySize = 0.12f;
    }

    public enum SatelliteType
    {
        Communication,
        Navigation,
        EarthObservation,
        Scientific
    }

    public enum SatelliteStatus
    {
        Active,
        Inactive,
        Decommissioned
    }

    public enum SpeedLevel
    {
        Low,
        Medium,
        High,
        VeryHigh
    }
}
