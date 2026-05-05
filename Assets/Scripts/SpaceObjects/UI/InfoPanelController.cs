using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceDebris
{
    /// <summary>
    /// World-space UI panel that displays information about the selected orbital object.
    /// The panel follows the main camera so it's always readable in VR.
    /// </summary>
    public class InfoPanelController : MonoBehaviour
    {
        [Header("Panel Root")]
        [SerializeField] private GameObject panelRoot;

        [Header("Text Fields")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_Text riskText;

        [Header("Risk Colours")]
        [SerializeField] private Color riskLowColor = new Color(0.2f, 1f, 0.2f);
        [SerializeField] private Color riskMediumColor = new Color(1f, 0.75f, 0f);
        [SerializeField] private Color riskHighColor = new Color(1f, 0.2f, 0.1f);

        [Header("Positioning")]
        [Tooltip("Offset from the camera in local camera space.")]
        [SerializeField] private Vector3 panelOffset = new Vector3(0f, -0.25f, 1.2f);
        [SerializeField] private float smoothSpeed = 8f;

        [Header("Close Button")]
        [SerializeField] private Button closeButton;

        private Transform cameraTransform;
        private bool isVisible;

        private void Awake()
        {
            panelRoot.SetActive(false);

            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);
        }

        private void Start()
        {
            Camera mainCamera = Camera.main;
            if (mainCamera != null)
                cameraTransform = mainCamera.transform;
        }

        private void LateUpdate()
        {
            if (!isVisible || cameraTransform == null) return;

            // Position panel in front of the camera.
            Vector3 targetPos = cameraTransform.TransformPoint(panelOffset);
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * smoothSpeed);

            // Face the camera.
            transform.rotation = Quaternion.LookRotation(transform.position - cameraTransform.position);
        }

        /// <summary>
        /// Populates and shows the panel for a satellite.
        /// </summary>
        public void ShowSatelliteInfo(SatelliteObject satellite, DebrisObject nearestDanger, RiskLevel risk)
        {
            SatelliteData d = satellite.SatelliteData;
            if (d == null) return;

            titleText.text = $"Объект: {d.objectName}";

            var sb = new StringBuilder();
            sb.AppendLine($"Тип: {LocalizeSatelliteType(d.satelliteType)}");
            sb.AppendLine($"Орбита: {d.orbitType}");
            sb.AppendLine($"Высота: {d.altitudeKm:F0} км");
            sb.AppendLine($"Скорость: {LocalizeSpeed(d.speedLevel)}");
            sb.AppendLine($"Состояние: {LocalizeSatelliteStatus(d.status)}");

            if (nearestDanger != null)
                sb.AppendLine($"Ближайшая угроза: {nearestDanger.DebrisData?.objectName ?? nearestDanger.name}");
            else
                sb.AppendLine("Ближайшая угроза: нет");

            bodyText.text = sb.ToString();

            SetRisk(risk);
            Show();
        }

        /// <summary>
        /// Populates and shows the panel for a debris object.
        /// </summary>
        public void ShowDebrisInfo(DebrisObject debris, RiskLevel risk)
        {
            DebrisData d = debris.DebrisData;
            if (d == null) return;

            titleText.text = $"Объект: {d.objectName}";

            var sb = new StringBuilder();
            sb.AppendLine($"Тип: {LocalizeFragmentType(d.fragmentType)}");
            sb.AppendLine($"Размер: {d.sizeCm:F0} см");
            sb.AppendLine($"Источник: {LocalizeDebrisSource(d.source)}");
            sb.AppendLine($"Орбита: {d.orbitType}");
            sb.AppendLine($"Высота: {d.altitudeKm:F0} км");

            bodyText.text = sb.ToString();

            SetRisk(risk);
            Show();
        }

        private void SetRisk(RiskLevel risk)
        {
            switch (risk)
            {
                case RiskLevel.Low:
                    riskText.text = "Риск: Низкий";
                    riskText.color = riskLowColor;
                    break;
                case RiskLevel.Medium:
                    riskText.text = "Риск: Средний";
                    riskText.color = riskMediumColor;
                    break;
                case RiskLevel.High:
                    riskText.text = "Риск: Высокий";
                    riskText.color = riskHighColor;
                    break;
            }
        }

        private void Show()
        {
            panelRoot.SetActive(true);
            isVisible = true;
        }

        /// <summary>
        /// Hides the info panel.
        /// </summary>
        public void Hide()
        {
            panelRoot.SetActive(false);
            isVisible = false;
        }

        // ── Localization helpers ──────────────────────────────────────────────

        private static string LocalizeSatelliteType(SatelliteType t) => t switch
        {
            SatelliteType.Communication => "Спутник связи",
            SatelliteType.Navigation => "Навигационный спутник",
            SatelliteType.EarthObservation => "Наблюдение Земли",
            SatelliteType.Scientific => "Научный аппарат",
            _ => t.ToString()
        };

        private static string LocalizeSatelliteStatus(SatelliteStatus s) => s switch
        {
            SatelliteStatus.Active => "Активен",
            SatelliteStatus.Inactive => "Неактивен",
            SatelliteStatus.Decommissioned => "Выведен из эксплуатации",
            _ => s.ToString()
        };

        private static string LocalizeSpeed(SpeedLevel s) => s switch
        {
            SpeedLevel.Low => "Низкая",
            SpeedLevel.Medium => "Средняя",
            SpeedLevel.High => "Высокая",
            SpeedLevel.VeryHigh => "Очень высокая",
            _ => s.ToString()
        };

        private static string LocalizeFragmentType(DebrisFragmentType t) => t switch
        {
            DebrisFragmentType.SmallFragment => "Малый фрагмент",
            DebrisFragmentType.MediumFragment => "Средний фрагмент",
            DebrisFragmentType.LargeObject => "Крупный объект",
            _ => t.ToString()
        };

        private static string LocalizeDebrisSource(DebrisSource s) => s switch
        {
            DebrisSource.OldSatellite => "Разрушенный спутник",
            DebrisSource.Collision => "Столкновение",
            DebrisSource.RocketStage => "Ступень ракеты",
            _ => s.ToString()
        };
    }
}
