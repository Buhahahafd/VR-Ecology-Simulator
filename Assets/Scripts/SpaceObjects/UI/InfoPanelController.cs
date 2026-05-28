using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceDebris
{
    /// <summary>
    /// World-space UI panel that displays detailed threat information about the selected satellite.
    /// Includes: object ID, speed, orbit altitude, collision probability, estimated TCA,
    /// danger level badge, and recommended action.
    /// </summary>
    public class InfoPanelController : MonoBehaviour
    {
        [Header("Panel Root")]
        [SerializeField] private GameObject panelRoot;

        [Header("Text Fields")]
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text bodyText;
        [SerializeField] private TMP_Text riskText;
        [SerializeField] private TMP_Text dangerBadgeText;
        [SerializeField] private TMP_Text recommendationText;

        [Header("Danger Badge Background")]
        [SerializeField] private Image dangerBadgeBackground;

        [Header("Danger Level Colours")]
        [SerializeField] private Color dangerSafeColor     = new Color(0.15f, 0.80f, 0.25f);
        [SerializeField] private Color dangerWatchColor    = new Color(1f,    0.85f, 0f);
        [SerializeField] private Color dangerManeuverColor = new Color(1f,    0.45f, 0f);
        [SerializeField] private Color dangerCriticalColor = new Color(1f,    0.10f, 0.08f);

        [Header("Risk Label Colours")]
        [SerializeField] private Color riskLowColor    = new Color(0.2f, 1f,   0.2f);
        [SerializeField] private Color riskMediumColor = new Color(1f,   0.75f, 0f);
        [SerializeField] private Color riskHighColor   = new Color(1f,   0.2f,  0.1f);

        [Header("Positioning")]
        [Tooltip("Offset from the camera in local camera space.")]
        [SerializeField] private Vector3 panelOffset = new Vector3(0f, -0.25f, 1.2f);
        [SerializeField] private float smoothSpeed = 8f;

        [Header("Close Button")]
        [SerializeField] private Button closeButton;

        private Transform cameraTransform;
        private bool isVisible;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            panelRoot.SetActive(false);
            if (closeButton != null)
                closeButton.onClick.AddListener(Hide);
        }

        private void Start()
        {
            cameraTransform = ResolveCamera();
        }

        private void Update()
        {
            // Re-resolve camera if it wasn't found yet (XR rig may initialise after this script).
            if (cameraTransform == null)
                cameraTransform = ResolveCamera();
        }

        private void LateUpdate()
        {
            if (!isVisible || cameraTransform == null) return;

            Vector3 targetPos = cameraTransform.TransformPoint(panelOffset);
            transform.position = Vector3.Lerp(transform.position, targetPos, Time.deltaTime * smoothSpeed);
            transform.rotation = Quaternion.LookRotation(transform.position - cameraTransform.position);
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>
        /// Populates and shows the full threat panel for a satellite.
        /// Pulls live risk data from the provided RiskCalculator.
        /// </summary>
        public void ShowSatelliteInfo(SatelliteObject satellite, RiskCalculator riskCalc)
        {
            SatelliteData d = satellite.SatelliteData;
            if (d == null) return;

            titleText.text = $"Объект: {d.objectName}";

            // ── Body: orbital telemetry ──────────────────────────────────────
            var sb = new StringBuilder();
            sb.AppendLine($"Тип:       {LocalizeSatelliteType(d.satelliteType)}");
            sb.AppendLine($"Состояние: {LocalizeSatelliteStatus(d.status)}");
            sb.AppendLine($"Орбита:    {d.orbitType}");

            // Altitude from scene units → km representation (1 scene unit ≈ 800 km for LEO context).
            float altKm = d.altitudeSceneUnits > 0f ? d.altitudeSceneUnits * 800f : d.altitudeKm;
            sb.AppendLine($"Высота:    {altKm:F0} км");
            sb.AppendLine($"Скорость:  {LocalizeSpeed(d.speedLevel)}");

            if (riskCalc != null)
            {
                OrbitalObject threat = riskCalc.GetNearestThreat(satellite);
                float dist   = riskCalc.GetNearestThreatDistance(satellite);
                float tca    = riskCalc.GetEstimatedTimeToClosestApproach(satellite);
                float prob   = riskCalc.GetCollisionProbability(satellite);
                string action = riskCalc.GetRecommendedAction(satellite);

                sb.AppendLine();
                string threatName = threat != null
                    ? ((threat as SatelliteObject)?.SatelliteData?.objectName
                       ?? threat.GetData()?.objectName
                       ?? threat.name)
                    : null;
                sb.AppendLine(threatName != null
                    ? $"Угроза:    {threatName}"
                    : "Угроза:    нет");

                if (threat != null && dist < float.MaxValue)
                    sb.AppendLine($"Дистанция: {dist:F2} ед.");

                if (tca < float.MaxValue)
                    sb.AppendLine($"Время до ближ. сближ.: {FormatTime(tca)}");
                else if (threat != null)
                    sb.AppendLine("Время до ближ. сближ.: расходятся");

                sb.AppendLine($"Вер. столкновения: {prob * 100f:F0}%");

                if (recommendationText != null)
                    recommendationText.text = action;

                RiskLevel risk = riskCalc.GetSatelliteRiskLevel(satellite);
                DangerLevel danger = riskCalc.GetDangerLevel(satellite);
                SetRisk(risk);
                SetDanger(danger);
            }

            bodyText.text = sb.ToString();
            Show();
        }

        /// <summary>
        /// Legacy overload: shows satellite info without live risk calc data.
        /// </summary>
        public void ShowSatelliteInfo(SatelliteObject satellite, DebrisObject nearestDanger, RiskLevel risk)
        {
            SatelliteData d = satellite.SatelliteData;
            if (d == null) return;

            titleText.text = $"Объект: {d.objectName}";

            var sb = new StringBuilder();
            sb.AppendLine($"Тип: {LocalizeSatelliteType(d.satelliteType)}");
            sb.AppendLine($"Орбита: {d.orbitType}");
            sb.AppendLine($"Скорость: {LocalizeSpeed(d.speedLevel)}");
            sb.AppendLine($"Состояние: {LocalizeSatelliteStatus(d.status)}");
            sb.AppendLine(nearestDanger != null
                ? $"Ближайшая угроза: {nearestDanger.DebrisData?.objectName ?? nearestDanger.name}"
                : "Ближайшая угроза: нет");

            bodyText.text = sb.ToString();
            SetRisk(risk);
            if (recommendationText != null)
                recommendationText.text = risk == RiskLevel.Low ? "Угроза отсутствует" : "Наблюдать за объектом";

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

        /// <summary>Hides the info panel.</summary>
        public void Hide()
        {
            panelRoot.SetActive(false);
            isVisible = false;
        }

        // ── Private helpers ───────────────────────────────────────────────────

        private void SetRisk(RiskLevel risk)
        {
            if (riskText == null) return;
            (riskText.text, riskText.color) = risk switch
            {
                RiskLevel.High   => ("Риск: Высокий", riskHighColor),
                RiskLevel.Medium => ("Риск: Средний", riskMediumColor),
                _                => ("Риск: Низкий",  riskLowColor)
            };
        }

        private void SetDanger(DangerLevel danger)
        {
            Color color = danger switch
            {
                DangerLevel.Critical  => dangerCriticalColor,
                DangerLevel.Maneuver  => dangerManeuverColor,
                DangerLevel.Watch     => dangerWatchColor,
                _                     => dangerSafeColor
            };

            string label = danger switch
            {
                DangerLevel.Critical  => "КРИТИЧНО",
                DangerLevel.Maneuver  => "МАНЁВР",
                DangerLevel.Watch     => "НАБЛЮДЕНИЕ",
                _                     => "БЕЗОПАСНО"
            };

            if (dangerBadgeText != null)
            {
                dangerBadgeText.text  = label;
                dangerBadgeText.color = color;
            }

            if (dangerBadgeBackground != null)
            {
                Color bg = color;
                bg.a = 0.25f;
                dangerBadgeBackground.color = bg;
            }
        }

        private void Show()
        {
            panelRoot.SetActive(true);
            isVisible = true;
        }

        private static string FormatTime(float seconds)
        {
            if (seconds >= 3600f) return $"{seconds / 3600f:F1} ч";
            if (seconds >= 60f)   return $"{seconds / 60f:F0} мин";
            return $"{seconds:F0} сек";
        }

        // ── Localization helpers ──────────────────────────────────────────────

        private static string LocalizeSatelliteType(SatelliteType t) => t switch
        {
            SatelliteType.Communication    => "Спутник связи",
            SatelliteType.Navigation       => "Навигационный спутник",
            SatelliteType.EarthObservation => "Наблюдение Земли",
            SatelliteType.Scientific       => "Научный аппарат",
            _                              => t.ToString()
        };

        private static string LocalizeSatelliteStatus(SatelliteStatus s) => s switch
        {
            SatelliteStatus.Active           => "Активен",
            SatelliteStatus.Inactive         => "Неактивен",
            SatelliteStatus.Decommissioned   => "Выведен из эксплуатации",
            _                                => s.ToString()
        };

        private static string LocalizeSpeed(SpeedLevel s) => s switch
        {
            SpeedLevel.Low      => "Низкая",
            SpeedLevel.Medium   => "Средняя",
            SpeedLevel.High     => "Высокая",
            SpeedLevel.VeryHigh => "Очень высокая",
            _                   => s.ToString()
        };

        private static string LocalizeFragmentType(DebrisFragmentType t) => t switch
        {
            DebrisFragmentType.SmallFragment  => "Малый фрагмент",
            DebrisFragmentType.MediumFragment => "Средний фрагмент",
            DebrisFragmentType.LargeObject    => "Крупный объект",
            _                                 => t.ToString()
        };

        private static string LocalizeDebrisSource(DebrisSource s) => s switch
        {
            DebrisSource.OldSatellite => "Разрушенный спутник",
            DebrisSource.Collision    => "Столкновение",
            DebrisSource.RocketStage  => "Ступень ракеты",
            _                         => s.ToString()
        };

        private static Transform ResolveCamera()
        {
            Camera cam = Camera.main;
            if (cam != null) return cam.transform;
            cam = FindFirstObjectByType<Camera>();
            return cam != null ? cam.transform : null;
        }
    }
}
