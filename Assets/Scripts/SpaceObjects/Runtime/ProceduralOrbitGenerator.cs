using System.Collections.Generic;
using UnityEngine;

namespace SpaceDebris
{
    /// <summary>
    /// Generates SatelliteData and DebrisData instances purely in memory
    /// using randomised parameters from a ProceduralSimulationConfig.
    /// No ScriptableObject assets are written to disk.
    ///
    /// Each generated SatelliteData carries its scene-unit altitude in
    /// a dedicated runtime field (altitudeSceneUnits) so OrbitSpawner
    /// does not need to repurpose altitudeKm for this purpose.
    /// </summary>
    public static class ProceduralOrbitGenerator
    {
        private static readonly string[] SatellitePrefixes =
            { "SPUTNIK", "COSMOS", "INTELSAT", "STARLINK", "LANDSAT", "NOAA", "GPS", "IRIDIUM", "METEOSAT", "SENTINEL" };

        private static readonly string[] DebrisPrefixes =
            { "DEB", "FRAG", "OBJ", "R/B", "SCR", "WASTE", "SHARD", "JUNK" };

        private static readonly SatelliteType[]     SatelliteTypes = (SatelliteType[])System.Enum.GetValues(typeof(SatelliteType));
        private static readonly DebrisFragmentType[] FragmentTypes  = (DebrisFragmentType[])System.Enum.GetValues(typeof(DebrisFragmentType));
        private static readonly DebrisSource[]       DebrisSources  = (DebrisSource[])System.Enum.GetValues(typeof(DebrisSource));
        private static readonly OrbitType[]          OrbitTypes     = (OrbitType[])System.Enum.GetValues(typeof(OrbitType));

        // ── Public API ──────────────────────────────────────────────────────────

        /// <summary>
        /// Generates a fresh simulation dataset from the given config.
        /// Debris is only generated when config.spawnDebris is true.
        /// </summary>
        public static GenerationResult Generate(ProceduralSimulationConfig config)
        {
            int resolvedSeed = config.seed >= 0 ? config.seed : Random.Range(0, int.MaxValue);
            Random.State previousState = Random.state;
            Random.InitState(resolvedSeed);

            int satelliteCount = Random.Range(config.minSatellites, config.maxSatellites + 1);

            List<SatelliteData> satellites = GenerateSatellites(satelliteCount, config);

            List<DebrisData> debris = new();
            if (config.spawnDebris)
            {
                int debrisCount = Random.Range(config.minDebris, config.maxDebris + 1);
                debris = GenerateDebris(debrisCount, config);
            }

            Random.state = previousState;

            return new GenerationResult(resolvedSeed, satellites, debris);
        }

        // ── Private: satellite generation ────────────────────────────────────────

        private static List<SatelliteData> GenerateSatellites(int count, ProceduralSimulationConfig cfg)
        {
            List<SatelliteData> list = new(count);

            for (int i = 0; i < count; i++)
            {
                SatelliteData sat = ScriptableObject.CreateInstance<SatelliteData>();

                bool isActive = Random.value <= cfg.activeSatelliteFraction;

                sat.objectName         = $"{SatellitePrefixes[Random.Range(0, SatellitePrefixes.Length)]}-{1000 + i}";
                sat.orbitType          = OrbitTypes[Random.Range(0, OrbitTypes.Length)];
                sat.altitudeKm         = Random.Range(160f, 36000f); // reference value only
                sat.angularSpeed       = Random.Range(cfg.minSatelliteSpeed, cfg.maxSatelliteSpeed);
                sat.orbitalInclination = Random.Range(-cfg.maxSatelliteInclination, cfg.maxSatelliteInclination);
                sat.startAngle         = Random.Range(0f, 360f);
                sat.satelliteType      = SatelliteTypes[Random.Range(0, SatelliteTypes.Length)];
                sat.status             = isActive
                    ? SatelliteStatus.Active
                    : (Random.value > 0.5f ? SatelliteStatus.Inactive : SatelliteStatus.Decommissioned);
                sat.speedLevel         = AngularSpeedToLevel(sat.angularSpeed);
                sat.displayColor       = isActive ? cfg.activeSatelliteColor : cfg.inactiveSatelliteColor;
                sat.displaySize        = Random.Range(cfg.minSatelliteSize, cfg.maxSatelliteSize);

                // Store scene-unit altitude in a dedicated runtime field.
                sat.altitudeSceneUnits = Random.Range(cfg.minSatelliteAltitude, cfg.maxSatelliteAltitude);

                list.Add(sat);
            }

            return list;
        }

        // ── Private: debris generation ────────────────────────────────────────────

        private static List<DebrisData> GenerateDebris(int count, ProceduralSimulationConfig cfg)
        {
            List<DebrisData> list = new(count);

            for (int i = 0; i < count; i++)
            {
                DebrisData deb = ScriptableObject.CreateInstance<DebrisData>();

                deb.objectName         = $"{DebrisPrefixes[Random.Range(0, DebrisPrefixes.Length)]}-{2000 + i}";
                deb.orbitType          = OrbitTypes[Random.Range(0, OrbitTypes.Length)];
                deb.altitudeKm         = Random.Range(160f, 36000f);
                deb.angularSpeed       = Random.Range(cfg.minDebrisSpeed, cfg.maxDebrisSpeed);
                deb.orbitalInclination = Random.Range(-cfg.maxDebrisInclination, cfg.maxDebrisInclination);
                deb.startAngle         = Random.Range(0f, 360f);
                deb.fragmentType       = FragmentTypes[Random.Range(0, FragmentTypes.Length)];
                deb.source             = DebrisSources[Random.Range(0, DebrisSources.Length)];
                deb.sizeCm             = Random.Range(cfg.minDebrisSizeCm, cfg.maxDebrisSizeCm);
                deb.displayColor       = Color.gray;
                deb.displaySize        = Random.Range(cfg.minDebrisSize, cfg.maxDebrisSize);

                list.Add(deb);
            }

            return list;
        }

        private static SpeedLevel AngularSpeedToLevel(float speed)
        {
            if (speed < 2f)  return SpeedLevel.Low;
            if (speed < 5f)  return SpeedLevel.Medium;
            if (speed < 8f)  return SpeedLevel.High;
            return SpeedLevel.VeryHigh;
        }
    }

    /// <summary>
    /// Result returned by ProceduralOrbitGenerator.Generate().
    /// </summary>
    public readonly struct GenerationResult
    {
        public readonly int Seed;
        public readonly List<SatelliteData> Satellites;
        public readonly List<DebrisData>    Debris;

        public GenerationResult(int seed, List<SatelliteData> satellites, List<DebrisData> debris)
        {
            Seed       = seed;
            Satellites = satellites;
            Debris     = debris;
        }
    }
}
