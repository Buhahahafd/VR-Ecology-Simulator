using System.Collections.Generic;
using UnityEngine;

namespace SpaceDebris
{
    /// <summary>
    /// ScriptableObject that lists all satellites and debris to spawn into the scene.
    /// </summary>
    [CreateAssetMenu(fileName = "NewOrbitSpawnConfig", menuName = "Space Debris/Orbit Spawn Config")]
    public class OrbitSpawnConfig : ScriptableObject
    {
        [Header("Satellites")]
        public List<SatelliteData> satellites = new();

        [Header("Debris")]
        public List<DebrisData> debrisList = new();
    }
}
