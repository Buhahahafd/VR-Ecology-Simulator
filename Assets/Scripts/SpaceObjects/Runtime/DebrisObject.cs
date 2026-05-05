using UnityEngine;

namespace SpaceDebris
{
    /// <summary>
    /// Orbital object representing a piece of space debris.
    /// Rendered as a rotating cube to visually distinguish it from spherical satellites.
    /// Colour reflects current danger level: green → yellow → red.
    /// Emission is intentionally dimmer than satellites — debris is passive, not operational.
    /// </summary>
    public class DebrisObject : OrbitalObject
    {
        private const float EmissionIntensity = 1.4f;

        [Tooltip("Rotation speed in degrees per second on each axis, giving a tumbling effect.")]
        [SerializeField] private Vector3 tumbleSpeed = new Vector3(23f, 47f, 11f);

        private DebrisData debrisData;
        private MaterialPropertyBlock propertyBlock;

        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");
        private static readonly int BaseColorID     = Shader.PropertyToID("_BaseColor");

        private static readonly Color ColorSafe    = new Color(0.25f, 0.85f, 0.25f);
        private static readonly Color ColorWarning = new Color(1f,    0.65f, 0f);
        private static readonly Color ColorDanger  = new Color(1f,    0.1f,  0.05f);

        protected override void Awake()
        {
            base.Awake();
            propertyBlock = new MaterialPropertyBlock();
            SwapMeshToCube();
        }

        protected override void Update()
        {
            base.Update();
            // Tumble the cube on all axes to sell the "floating junk" look.
            transform.Rotate(tumbleSpeed * Time.deltaTime, Space.Self);
        }

        protected override void ApplyDisplaySettings()
        {
            debrisData = data as DebrisData;
            if (debrisData == null) return;

            transform.localScale = Vector3.one * debrisData.displaySize;
            ApplyDangerColor(RiskLevel.Low);
        }

        /// <summary>
        /// Updates the colour of the debris cube to reflect risk level.
        /// </summary>
        public void ApplyDangerColor(RiskLevel risk)
        {
            Color color = risk switch
            {
                RiskLevel.Low    => ColorSafe,
                RiskLevel.Medium => ColorWarning,
                RiskLevel.High   => ColorDanger,
                _                => ColorSafe
            };

            objectRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(BaseColorID, color);
            propertyBlock.SetColor(EmissionColorID, color * EmissionIntensity);
            objectRenderer.SetPropertyBlock(propertyBlock);
        }

        public DebrisData DebrisData => debrisData;

        // Replaces the sphere mesh with a cube so debris is visually distinct from satellites.
        private void SwapMeshToCube()
        {
            MeshFilter meshFilter = GetComponent<MeshFilter>();
            if (meshFilter == null) return;

            GameObject tempCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            meshFilter.sharedMesh = tempCube.GetComponent<MeshFilter>().sharedMesh;
            Destroy(tempCube);
        }
    }
}
