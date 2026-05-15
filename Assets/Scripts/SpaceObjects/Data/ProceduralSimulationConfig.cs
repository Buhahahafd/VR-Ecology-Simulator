using UnityEngine;

namespace SpaceDebris
{
    /// <summary>
    /// ScriptableObject that defines the parameters for procedural generation of the orbital simulation.
    /// Satellites only — debris generation is controlled by a separate toggle.
    /// Adjust counts and orbital parameter ranges to model different congestion scenarios.
    /// </summary>
    [CreateAssetMenu(fileName = "NewProceduralSimConfig", menuName = "Space Debris/Procedural Simulation Config")]
    public class ProceduralSimulationConfig : ScriptableObject
    {
        [Header("Simulation Seed")]
        [Tooltip("Fixed seed for reproducible results. Set to -1 to use a random seed each run.")]
        public int seed = -1;

        // ── Satellite counts ────────────────────────────────────────────────────

        [Header("Satellite Count")]
        [Tooltip("Minimum number of satellites to generate.")]
        [Min(0)] public int minSatellites = 10;

        [Tooltip("Maximum number of satellites to generate. " +
                 "Increase significantly to demonstrate Kessler-syndrome-like crowding.")]
        [Min(0)] public int maxSatellites = 40;

        // ── Debris toggle ───────────────────────────────────────────────────────

        [Header("Debris")]
        [Tooltip("When false, no debris is generated. Disable to focus the demo on satellite crowding.")]
        public bool spawnDebris = false;

        [Tooltip("Minimum number of debris fragments (only used when Spawn Debris = true).")]
        [Min(0)] public int minDebris = 10;

        [Tooltip("Maximum number of debris fragments (only used when Spawn Debris = true).")]
        [Min(0)] public int maxDebris = 60;

        // ── Orbital altitude ranges ─────────────────────────────────────────────

        [Header("Orbital Altitude (scene units above Earth surface)")]
        [Tooltip("Satellites orbit in a narrow shell to maximise visible crowding. " +
                 "Spread the range wider to model multiple orbit types simultaneously.")]
        [Min(0f)] public float minSatelliteAltitude = 0.5f;

        [Min(0f)] public float maxSatelliteAltitude = 1.4f;

        [Min(0f)] public float minDebrisAltitude = 0.3f;
        [Min(0f)] public float maxDebrisAltitude = 1.8f;

        // ── Orbital speed ranges ────────────────────────────────────────────────

        [Header("Angular Speed (degrees / second)")]
        [Tooltip("Satellites in LEO move faster. Uniform speed band makes orbits comparable.")]
        [Min(0f)] public float minSatelliteSpeed = 3f;
        [Min(0f)] public float maxSatelliteSpeed = 8f;

        [Min(0f)] public float minDebrisSpeed = 1f;
        [Min(0f)] public float maxDebrisSpeed = 10f;

        // ── Inclination ranges ──────────────────────────────────────────────────

        [Header("Orbital Inclination (degrees)")]
        [Tooltip("0 = equatorial, 90 = polar. Crossing orbits increase collision probability.")]
        [Range(0f, 90f)] public float maxSatelliteInclination = 85f;
        [Range(0f, 90f)] public float maxDebrisInclination    = 90f;

        // ── Satellite visual parameters ─────────────────────────────────────────

        [Header("Satellite Display")]
        [Tooltip("Visual size of satellite GameObjects in scene units. " +
                 "Earth visual radius = 0.5 units (unit sphere). " +
                 "Keep values well below 0.02 for realistic proportions.")]
        [Min(0.001f)] public float minSatelliteSize = 0.008f;
        [Min(0.001f)] public float maxSatelliteSize = 0.015f;

        [Tooltip("Colour of operational satellites when not at risk.")]
        public Color activeSatelliteColor   = new Color(0.25f, 0.85f, 1f);   // bright cyan-blue

        [Tooltip("Colour of decommissioned / inactive satellites when not at risk.")]
        public Color inactiveSatelliteColor = new Color(0.55f, 0.55f, 0.75f); // muted violet

        [Tooltip("Fraction of generated satellites that are active (0 = all inactive, 1 = all active).")]
        [Range(0f, 1f)] public float activeSatelliteFraction = 0.75f;

        // ── Debris visual parameters (only relevant when spawnDebris = true) ────

        [Header("Debris Display (only when Spawn Debris = true)")]
        [Tooltip("Debris is smaller than satellites — fragments and rocket body parts. " +
                 "Keep values below satellite min size.")]
        [Min(0.001f)] public float minDebrisSize   = 0.003f;
        [Min(0.001f)] public float maxDebrisSize   = 0.007f;
        [Min(0.1f)]   public float minDebrisSizeCm = 1f;
        [Min(0.1f)]   public float maxDebrisSizeCm = 200f;
    }
}
