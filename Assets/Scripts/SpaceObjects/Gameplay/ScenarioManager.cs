using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceDebris
{
    public enum ScenarioState
    {
        Idle,
        Briefing,
        Running,
        Success,
        Failure
    }

    /// <summary>
    /// Orchestrates a scripted gameplay scenario:
    ///   1. Displays a briefing.
    ///   2. Starts a countdown.
    ///   3. Watches for the player's action via SatelliteController events.
    ///   4. Resolves success (action taken in time) or failure (countdown expired).
    ///
    /// Scenario 01 — "One Maneuver":
    ///   Two high-risk satellites approach each other. Player may execute exactly
    ///   one orbit-maneuver before time runs out.
    /// </summary>
    public class ScenarioManager : MonoBehaviour
    {
        // ── Configuration ─────────────────────────────────────────────────────

        [Header("Scenario")]
        [SerializeField] private ScenarioData scenarioData;

        [Header("References")]
        [SerializeField] private RiskCalculator riskCalculator;
        [SerializeField] private CollisionConsequenceManager consequenceManager;

        [Header("UI (optional — populate from world-space canvas)")]
        [SerializeField] private TMPro.TMP_Text briefingText;
        [SerializeField] private TMPro.TMP_Text countdownText;
        [SerializeField] private TMPro.TMP_Text outcomeText;
        [SerializeField] private UnityEngine.UI.Image timerBar;

        [Header("Auto-Start")]
        [Tooltip("If true, the scenario starts automatically when the scene loads.")]
        [SerializeField] private bool autoStart = true;

        // ── Events ────────────────────────────────────────────────────────────

        public event Action<ScenarioState> OnStateChanged;
        public event Action<float>         OnCountdownTick;   // remaining seconds

        // ── Public state ──────────────────────────────────────────────────────

        public ScenarioState State          { get; private set; } = ScenarioState.Idle;
        public float         TimeRemaining  { get; private set; }
        public int           ManeuversUsed  { get; private set; }

        // ── Private ───────────────────────────────────────────────────────────

        private readonly List<SatelliteController> trackedControllers = new();
        private SatelliteObject threatSatA;
        private SatelliteObject threatSatB;
        private bool scenarioResolved;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Start()
        {
            if (riskCalculator == null)
                riskCalculator = FindFirstObjectByType<RiskCalculator>();

            if (consequenceManager == null)
                consequenceManager = FindFirstObjectByType<CollisionConsequenceManager>();

            if (autoStart)
                StartCoroutine(RunScenario());
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Starts the scenario from the beginning.</summary>
        public void StartScenario() => StartCoroutine(RunScenario());

        /// <summary>Aborts the current scenario and returns to Idle.</summary>
        public void AbortScenario()
        {
            StopAllCoroutines();
            UnsubscribeControllers();
            SetState(ScenarioState.Idle);
        }

        // ── Private: flow ─────────────────────────────────────────────────────

        private IEnumerator RunScenario()
        {
            if (scenarioData == null)
            {
                Debug.LogError("[ScenarioManager] ScenarioData is not assigned.");
                yield break;
            }

            scenarioResolved = false;
            ManeuversUsed    = 0;

            // ── Briefing ──────────────────────────────────────────────────────
            SetState(ScenarioState.Briefing);
            ShowBriefing(scenarioData.briefingText);

            // Wait for spawner to finish and RiskCalculator to populate.
            yield return new WaitForSeconds(2f);

            // Find the highest-risk pair to use as the scenario's focal threat.
            FindThreatPair();
            RegisterControllers();

            // ── Countdown ─────────────────────────────────────────────────────
            SetState(ScenarioState.Running);
            HideBriefing();

            TimeRemaining = scenarioData.countdownSeconds;

            while (TimeRemaining > 0f && !scenarioResolved)
            {
                TimeRemaining -= Time.deltaTime;
                OnCountdownTick?.Invoke(TimeRemaining);
                UpdateCountdownUI();
                yield return null;
            }

            // ── Resolution ────────────────────────────────────────────────────
            if (!scenarioResolved)
                ResolveFailure();
        }

        private void FindThreatPair()
        {
            // Scan all satellite pairs to find the one at highest risk.
            SatelliteObject[] sats = FindObjectsByType<SatelliteObject>(FindObjectsSortMode.None);
            float closestDist = float.MaxValue;

            for (int i = 0; i < sats.Length; i++)
            {
                for (int j = i + 1; j < sats.Length; j++)
                {
                    float d = Vector3.Distance(sats[i].transform.position, sats[j].transform.position);
                    if (d < closestDist)
                    {
                        closestDist = d;
                        threatSatA  = sats[i];
                        threatSatB  = sats[j];
                    }
                }
            }

            if (threatSatA != null)
                Debug.Log($"[ScenarioManager] Threat pair: {threatSatA.name} ↔ {threatSatB.name} " +
                          $"(distance: {closestDist:F3} units)");
        }

        private void RegisterControllers()
        {
            trackedControllers.Clear();
            SatelliteObject[] sats = FindObjectsByType<SatelliteObject>(FindObjectsSortMode.None);

            foreach (SatelliteObject sat in sats)
            {
                SatelliteController ctrl = sat.GetComponent<SatelliteController>();
                if (ctrl == null) continue;

                trackedControllers.Add(ctrl);
                ctrl.OnActionExecuted += HandleActionExecuted;
            }
        }

        private void UnsubscribeControllers()
        {
            foreach (SatelliteController ctrl in trackedControllers)
            {
                if (ctrl != null)
                    ctrl.OnActionExecuted -= HandleActionExecuted;
            }
            trackedControllers.Clear();
        }

        // ── Private: resolution ───────────────────────────────────────────────

        private void HandleActionExecuted(SatelliteAction action, SatelliteController controller)
        {
            if (State != ScenarioState.Running || scenarioResolved) return;

            if (action == SatelliteAction.OrbitManeuver || action == SatelliteAction.DeorbitBrake)
            {
                ManeuversUsed++;

                // Check if the maximum number of allowed maneuvers was consumed.
                bool withinLimit = scenarioData.maxManeuvers <= 0
                                   || ManeuversUsed <= scenarioData.maxManeuvers;

                if (withinLimit)
                {
                    // Give a brief moment for the orbit to change, then check risk.
                    StartCoroutine(CheckRiskAfterManeuver());
                }
            }
        }

        private IEnumerator CheckRiskAfterManeuver()
        {
            yield return new WaitForSeconds(1.5f);

            if (threatSatA == null || threatSatB == null)
            {
                ResolveSuccess();
                yield break;
            }

            RiskLevel riskA = riskCalculator != null
                ? riskCalculator.GetSatelliteRiskLevel(threatSatA)
                : RiskLevel.Low;

            if (riskA != RiskLevel.High)
                ResolveSuccess();
            // else: countdown continues, player used their one maneuver unsuccessfully
        }

        private void ResolveSuccess()
        {
            if (scenarioResolved) return;
            scenarioResolved = true;
            StopAllCoroutines();
            UnsubscribeControllers();
            SetState(ScenarioState.Success);
            ShowOutcome(scenarioData.successMessage, Color.green);
            Debug.Log("[ScenarioManager] SUCCESS — collision averted.");
        }

        private void ResolveFailure()
        {
            if (scenarioResolved) return;
            scenarioResolved = true;
            UnsubscribeControllers();
            SetState(ScenarioState.Failure);
            ShowOutcome(scenarioData.failureMessage, new Color(1f, 0.2f, 0.1f));

            if (consequenceManager != null && threatSatA != null && threatSatB != null)
                consequenceManager.ForceCollision(threatSatA, threatSatB);

            Debug.Log("[ScenarioManager] FAILURE — collision occurred.");
        }

        private void SetState(ScenarioState state)
        {
            State = state;
            OnStateChanged?.Invoke(state);
        }

        // ── Private: UI helpers ───────────────────────────────────────────────

        private void ShowBriefing(string text)
        {
            if (briefingText == null) return;
            briefingText.gameObject.SetActive(true);
            briefingText.text = text;
        }

        private void HideBriefing()
        {
            if (briefingText != null)
                briefingText.gameObject.SetActive(false);
        }

        private void UpdateCountdownUI()
        {
            if (countdownText != null)
                countdownText.text = $"Время: {FormatTime(TimeRemaining)}";

            if (timerBar != null && scenarioData != null)
                timerBar.fillAmount = TimeRemaining / scenarioData.countdownSeconds;
        }

        private void ShowOutcome(string message, Color color)
        {
            if (outcomeText == null) return;
            outcomeText.gameObject.SetActive(true);
            outcomeText.text  = message;
            outcomeText.color = color;

            if (countdownText != null)
                countdownText.gameObject.SetActive(false);
        }

        private static string FormatTime(float seconds)
        {
            int m = Mathf.FloorToInt(seconds / 60f);
            int s = Mathf.FloorToInt(seconds % 60f);
            return $"{m:D2}:{s:D2}";
        }
    }
}
