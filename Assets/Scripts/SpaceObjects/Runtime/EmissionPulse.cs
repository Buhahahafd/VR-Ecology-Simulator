using UnityEngine;

namespace SpaceDebris
{
    /// <summary>
    /// Pulsates the emission intensity of the attached Renderer,
    /// creating a "blinking beacon" effect visible from a distance.
    /// Frequency is randomized per-instance so objects blink out of sync.
    /// </summary>
    [RequireComponent(typeof(Renderer))]
    public class EmissionPulse : MonoBehaviour
    {
        private const float DefaultMinIntensity = 0.3f;
        private const float DefaultMaxIntensity = 4.0f;
        private const float DefaultMinFrequency = 0.8f;
        private const float DefaultMaxFrequency = 2.5f;

        private static readonly int EmissionColorID = Shader.PropertyToID("_EmissionColor");

        [Tooltip("Minimum emission multiplier at the dimmest point of the pulse.")]
        [SerializeField] private float minIntensity = DefaultMinIntensity;

        [Tooltip("Maximum emission multiplier at the brightest point of the pulse.")]
        [SerializeField] private float maxIntensity = DefaultMaxIntensity;

        [Tooltip("Pulse cycles per second (randomized between min and max on Awake).")]
        [SerializeField] private float minFrequency = DefaultMinFrequency;
        [SerializeField] private float maxFrequency = DefaultMaxFrequency;

        private Renderer objectRenderer;
        private MaterialPropertyBlock propertyBlock;
        private Color baseEmissionColor;
        private float frequency;
        private float phaseOffset;

        private void Awake()
        {
            objectRenderer = GetComponent<Renderer>();
            propertyBlock = new MaterialPropertyBlock();
            frequency = Random.Range(minFrequency, maxFrequency);
            phaseOffset = Random.Range(0f, Mathf.PI * 2f);
        }

        private void Start()
        {
            // Capture whatever emission color was already set by SatelliteObject / DebrisObject.
            objectRenderer.GetPropertyBlock(propertyBlock);
            baseEmissionColor = propertyBlock.GetColor(EmissionColorID);

            if (baseEmissionColor == Color.clear)
            {
                baseEmissionColor = Color.white;
            }
        }

        private void Update()
        {
            float t = (Mathf.Sin((Time.time * frequency + phaseOffset) * Mathf.PI * 2f) + 1f) * 0.5f;
            float intensity = Mathf.Lerp(minIntensity, maxIntensity, t);

            objectRenderer.GetPropertyBlock(propertyBlock);
            propertyBlock.SetColor(EmissionColorID, baseEmissionColor.linear * intensity);
            objectRenderer.SetPropertyBlock(propertyBlock);
        }

        /// <summary>
        /// Updates the base emission color (e.g. when risk level changes).
        /// </summary>
        public void SetBaseColor(Color color)
        {
            baseEmissionColor = color;
        }
    }
}
