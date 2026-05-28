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

        private EmissionPulse pulse;

        protected override void Awake()
        {
            base.Awake();
            propertyBlock = new MaterialPropertyBlock();

            // Add blinking beacon effect if not already present.
            pulse = GetComponent<EmissionPulse>();
            if (pulse == null)
                pulse = gameObject.AddComponent<EmissionPulse>();
        }


        protected override void ApplyDisplaySettings()
        {
            satelliteData = data as SatelliteData;
            if (satelliteData == null) return;

            transform.localScale = Vector3.one * satelliteData.displaySize;
            SetColor(satelliteData.displayColor);
        }

        /// <summary>
        /// Updates the glow colour.
        /// Active satellites: high emission (3×) — bright and readable.
        /// Inactive/decommissioned: dim emission (1×) — still visible but not dominant.
        /// Risk colours (yellow, red) always use full emission regardless of status.
        /// </summary>
        public void SetColor(Color color)
        {
            bool isRiskColor = satelliteData == null
                               || color != satelliteData.displayColor;

            float emissionMultiplier = isRiskColor
                ? 3.5f
                : (satelliteData?.status == SatelliteStatus.Active ? 3f : 1.2f);

            objectRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorID, color);
            propertyBlock.SetColor(EmissionColorID, color * emissionMultiplier);
            objectRenderer.SetPropertyBlock(propertyBlock);

            // Sync the pulse base color so blinking uses the updated tint.
            if (pulse != null)
                pulse.SetBaseColor(color * emissionMultiplier);
        }

        public SatelliteData SatelliteData => satelliteData;
    }
}
