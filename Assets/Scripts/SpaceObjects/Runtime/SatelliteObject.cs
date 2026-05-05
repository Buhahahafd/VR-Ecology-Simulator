using UnityEngine;

namespace SpaceDebris
{
    /// <summary>
    /// Orbital object representing an active or inactive satellite.
    /// Applies cyan/blue glow and emits danger events via RiskCalculator.
    /// </summary>
    public class SatelliteObject : OrbitalObject
    {
        private SatelliteData satelliteData;
        private MaterialPropertyBlock propertyBlock;

        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorID = Shader.PropertyToID("_BaseColor");

        protected override void Awake()
        {
            base.Awake();
            propertyBlock = new MaterialPropertyBlock();
        }


        protected override void ApplyDisplaySettings()
        {
            satelliteData = data as SatelliteData;
            if (satelliteData == null) return;

            transform.localScale = Vector3.one * satelliteData.displaySize;
            SetColor(satelliteData.displayColor);
        }

        /// <summary>
        /// Updates the glow colour, e.g. when danger level changes.
        /// Satellites use high emission (3×) to stand out as active operational objects.
        /// </summary>
        public void SetColor(Color color)
        {
            objectRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorID, color);
            propertyBlock.SetColor(EmissionColorID, color * 3f);
            objectRenderer.SetPropertyBlock(propertyBlock);
        }

        public SatelliteData SatelliteData => satelliteData;
    }
}
