using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceDebris
{
    /// <summary>
    /// World-space action panel that appears when a satellite is selected.
    /// Displays action buttons (Maneuver, Brake, Disable, Deploy Drone) with
    /// fuel cost labels and enables/disables them based on SatelliteController state.
    ///
    /// Wire up:
    ///   - Four Button fields for each action.
    ///   - Fuel bar (Image with fillAmount).
    ///   - Attach to the same Canvas as InfoPanelController.
    ///   - Call ShowForSatellite() from SpaceObjectInteractable on select.
    /// </summary>
    public class ActionPanelController : MonoBehaviour
    {
        [Header("Panel Root")]
        [SerializeField] private GameObject panelRoot;

        [Header("Action Buttons")]
        [SerializeField] private Button maneuverButton;
        [SerializeField] private Button deorbitBrakeButton;
        [SerializeField] private Button disableButton;
        [SerializeField] private Button droneButton;

        [Header("Fuel UI")]
        [SerializeField] private Image   fuelBar;
        [SerializeField] private TMP_Text fuelText;

        [Header("Feedback")]
        [SerializeField] private TMP_Text feedbackText;

        [Header("Positioning")]
        [SerializeField] private Vector3 panelOffset = new Vector3(0.6f, -0.25f, 1.2f);
        [SerializeField] private float   smoothSpeed = 8f;

        // ── State ─────────────────────────────────────────────────────────────

        private SatelliteController currentController;
        private Transform cameraTransform;
        private bool isVisible;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            panelRoot.SetActive(false);

            Wire(maneuverButton,     () => ExecuteAction(SatelliteAction.OrbitManeuver));
            Wire(deorbitBrakeButton, () => ExecuteAction(SatelliteAction.DeorbitBrake));
            Wire(disableButton,      () => ExecuteAction(SatelliteAction.Disable));
            Wire(droneButton,        () => ExecuteAction(SatelliteAction.LaunchDrone));
        }

        private void Start()
        {
            cameraTransform = ResolveCamera();
        }

        private void Update()
        {
            if (cameraTransform == null)
                cameraTransform = ResolveCamera();
        }

        private void LateUpdate()
        {
            if (!isVisible || cameraTransform == null) return;

            Vector3 target = cameraTransform.TransformPoint(panelOffset);
            transform.position = Vector3.Lerp(transform.position, target, Time.deltaTime * smoothSpeed);
            transform.rotation = Quaternion.LookRotation(transform.position - cameraTransform.position);

            if (currentController != null)
                RefreshFuelUI();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Shows the action panel bound to the given satellite's controller.</summary>
        public void ShowForSatellite(SatelliteController controller)
        {
            UnbindController();
            currentController = controller;

            if (controller != null)
            {
                controller.OnActionExecuted += OnActionExecuted;
                controller.OnFuelDepleted   += OnFuelDepleted;
            }

            RefreshButtons();
            RefreshFuelUI();
            panelRoot.SetActive(true);
            isVisible = true;

            if (feedbackText != null)
                feedbackText.text = string.Empty;
        }

        /// <summary>Hides the action panel.</summary>
        public void Hide()
        {
            UnbindController();
            panelRoot.SetActive(false);
            isVisible = false;
        }

        // ── Private: button wiring ────────────────────────────────────────────

        private static void Wire(Button btn, System.Action action)
        {
            if (btn != null) btn.onClick.AddListener(() => action());
        }

        private void ExecuteAction(SatelliteAction action)
        {
            if (currentController == null) return;

            bool ok = currentController.Execute(action);
            if (feedbackText != null)
            {
                feedbackText.text = ok
                    ? $"Выполняется: {LocalizeAction(action)}…"
                    : "Недостаточно топлива или недоступно";
            }

            RefreshButtons();
        }

        // ── Private: UI refresh ───────────────────────────────────────────────

        private void RefreshButtons()
        {
            SetButtonState(maneuverButton,     SatelliteAction.OrbitManeuver);
            SetButtonState(deorbitBrakeButton, SatelliteAction.DeorbitBrake);
            SetButtonState(disableButton,      SatelliteAction.Disable);
            SetButtonState(droneButton,        SatelliteAction.LaunchDrone);
        }

        private void SetButtonState(Button btn, SatelliteAction action)
        {
            if (btn == null) return;
            bool can = currentController != null && currentController.CanExecute(action);
            btn.interactable = can;

            // Update cost label if child TMP_Text exists.
            TMP_Text label = btn.GetComponentInChildren<TMP_Text>();
            if (label != null && currentController != null)
            {
                float cost = currentController.GetActionCost(action);
                string baseLabel = LocalizeAction(action);
                label.text = cost > 0f ? $"{baseLabel}\n({cost:F0} топл.)" : baseLabel;
            }
        }

        private void RefreshFuelUI()
        {
            if (currentController == null) return;

            if (fuelBar != null)
                fuelBar.fillAmount = currentController.FuelFraction;

            if (fuelText != null)
                fuelText.text = $"Топливо: {currentController.CurrentFuel:F0}/{currentController.MaxFuel:F0}";
        }

        private void OnActionExecuted(SatelliteAction action, SatelliteController ctrl)
        {
            RefreshButtons();
        }

        private void OnFuelDepleted(SatelliteController ctrl)
        {
            RefreshButtons();
            if (feedbackText != null)
                feedbackText.text = "Топливо исчерпано";
        }

        private void UnbindController()
        {
            if (currentController == null) return;
            currentController.OnActionExecuted -= OnActionExecuted;
            currentController.OnFuelDepleted   -= OnFuelDepleted;
            currentController = null;
        }

        // ── Localization ──────────────────────────────────────────────────────

        private static string LocalizeAction(SatelliteAction action) => action switch
        {
            SatelliteAction.OrbitManeuver => "Изменить орбиту",
            SatelliteAction.DeorbitBrake  => "Торможение / деорбита",
            SatelliteAction.Disable       => "Отключить спутник",
            SatelliteAction.LaunchDrone   => "Запустить дрон",
            _                             => action.ToString()
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
