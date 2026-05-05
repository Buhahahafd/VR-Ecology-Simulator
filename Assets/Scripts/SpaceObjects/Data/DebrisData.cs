using UnityEngine;

namespace SpaceDebris
{
    /// <summary>
    /// ScriptableObject describing a piece of space debris and its hazard classification.
    /// </summary>
    [CreateAssetMenu(fileName = "NewDebrisData", menuName = "Space Debris/Debris Data")]
    public class DebrisData : SpaceObjectData
    {
        [Header("Debris Properties")]
        public DebrisFragmentType fragmentType = DebrisFragmentType.SmallFragment;
        public DebrisSource source = DebrisSource.OldSatellite;

        [Tooltip("Physical size of the fragment in centimetres.")]
        public float sizeCm = 10f;

        [Header("Display")]
        [Tooltip("Base colour of the glowing point (overridden at runtime by danger level).")]
        public Color displayColor = Color.gray;

        [Tooltip("World-space scale of the glowing sphere.")]
        public float displaySize = 0.06f;
    }

    public enum DebrisFragmentType
    {
        SmallFragment,   // малый фрагмент
        MediumFragment,  // средний фрагмент
        LargeObject      // крупный объект
    }

    public enum DebrisSource
    {
        OldSatellite,
        Collision,
        RocketStage
    }
}
