using System;
using System.Collections;
using UnityEngine;

namespace SpaceDebris
{
    public enum SatelliteAction
    {
        OrbitManeuver,   // raise or lower orbit — costs fuel
        DeorbitBrake,    // retrograde burn — deorbits the satellite
        Disable,         // shuts down — removes from active risk calc
        LaunchDrone,     // dispatches debris-cleanup drone (used in scenario logic)
    }

    /// <summary>
    /// Manages player-driven actions on a specific satellite.
    /// Actions cost fuel; fuel starts at MaxFuel and cannot be refilled.
    /// Events let CollisionConsequenceManager and ScenarioManager react to outcomes.
    /// </summary>
    [RequireComponent(typeof(SatelliteObject))]
    public class SatelliteController : MonoBehaviour
    {
        // ── Configuration ─────────────────────────────────────────────────────

        [Header("Resources")]
        [Tooltip("Total fuel available for this satellite (arbitrary units).")]
        [SerializeField] private float maxFuel = 100f;

        [Header("Action Costs (fuel units)")]
        [SerializeField] private float orbitManeuverCost = 30f;
        [SerializeField] private float deBrakeDeorbitCost = 60f;
        [SerializeField] private float droneDeployCost = 20f;

        [Header("Maneuver Effect")]
        [Tooltip("How many scene units the orbit radius changes per maneuver.")]
        [SerializeField] private float maneuverAltitudeDelta = 0.3f;

        [Header("Timing")]
        [Tooltip("Duration of the orbit-change animation in seconds.")]
        [SerializeField] private float maneuverDuration = 3f;

        // ── State ─────────────────────────────────────────────────────────────

        private float currentFuel;
        private bool  isDisabled;
        private bool  isDroneDeployed;

        private SatelliteObject satelliteObject;

        // ── Events ────────────────────────────────────────────────────────────

        /// <summary>Raised when any action is executed. Parameters: action, satellite.</summary>
        public event Action<SatelliteAction, SatelliteController> OnActionExecuted;

        /// <summary>Raised when fuel reaches zero.</summary>
        public event Action<SatelliteController> OnFuelDepleted;

        /// <summary>Raised when the satellite is destroyed (collision or deorbit).</summary>
        public event Action<SatelliteController> OnSatelliteDestroyed;

        /// <summary>Raised when a drone is deployed. Payload: this controller.</summary>
        public event Action<SatelliteController> OnDroneDeployed;

        // ── Public properties ─────────────────────────────────────────────────

        public float CurrentFuel   => currentFuel;
        public float MaxFuel       => maxFuel;
        public float FuelFraction  => maxFuel > 0f ? currentFuel / maxFuel : 0f;
        public bool  IsDisabled    => isDisabled;
        public bool  IsDroneDeployed => isDroneDeployed;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            satelliteObject = GetComponent<SatelliteObject>();
            currentFuel = maxFuel;
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Returns whether the given action is currently executable.</summary>
        public bool CanExecute(SatelliteAction action)
        {
            if (isDisabled) return false;
            return action switch
            {
                SatelliteAction.OrbitManeuver => currentFuel >= orbitManeuverCost,
                SatelliteAction.DeorbitBrake  => currentFuel >= deBrakeDeorbitCost,
                SatelliteAction.Disable       => true,
                SatelliteAction.LaunchDrone   => !isDroneDeployed && currentFuel >= droneDeployCost,
                _                             => false
            };
        }

        /// <summary>Returns fuel cost of a given action (0 if none).</summary>
        public float GetActionCost(SatelliteAction action) => action switch
        {
            SatelliteAction.OrbitManeuver => orbitManeuverCost,
            SatelliteAction.DeorbitBrake  => deBrakeDeorbitCost,
            SatelliteAction.LaunchDrone   => droneDeployCost,
            _                             => 0f
        };

        /// <summary>Executes the specified action. Returns true if successful.</summary>
        public bool Execute(SatelliteAction action)
        {
            if (!CanExecute(action))
            {
                Debug.Log($"[SatelliteController] {name}: cannot execute {action} " +
                          $"(fuel={currentFuel:F0}, disabled={isDisabled}).");
                return false;
            }

            SpendFuel(GetActionCost(action));

            switch (action)
            {
                case SatelliteAction.OrbitManeuver:
                    StartCoroutine(PerformOrbitManeuver());
                    break;

                case SatelliteAction.DeorbitBrake:
                    StartCoroutine(PerformDeorbit());
                    break;

                case SatelliteAction.Disable:
                    DisableSatellite();
                    break;

                case SatelliteAction.LaunchDrone:
                    DeployDrone();
                    break;
            }

            OnActionExecuted?.Invoke(action, this);
            return true;
        }

        /// <summary>Triggers an immediate collision — called by ConsequenceManager.</summary>
        public void TriggerCollision()
        {
            Debug.Log($"[SatelliteController] {name}: collision triggered.");
            OnSatelliteDestroyed?.Invoke(this);
            Destroy(gameObject);
        }

        // ── Private: actions ──────────────────────────────────────────────────

        private IEnumerator PerformOrbitManeuver()
        {
            OrbitalObject orbital = GetComponent<OrbitalObject>();
            float startAlt = orbital.OrbitAltitudeUnits;
            float endAlt   = startAlt + maneuverAltitudeDelta;

            float elapsed = 0f;
            while (elapsed < maneuverDuration)
            {
                elapsed += Time.deltaTime;
                float t   = Mathf.SmoothStep(0f, 1f, elapsed / maneuverDuration);
                float alt = Mathf.Lerp(startAlt, endAlt, t);
                orbital.SetOrbitAltitudeUnits(alt);
                yield return null;
            }

            orbital.SetOrbitAltitudeUnits(endAlt);
            Debug.Log($"[SatelliteController] {name}: orbit raised by {maneuverAltitudeDelta:F2} units. New alt: {endAlt:F2}");
        }

        private IEnumerator PerformDeorbit()
        {
            Debug.Log($"[SatelliteController] {name}: retrograde burn — deorbiting.");

            OrbitalObject orbital = GetComponent<OrbitalObject>();
            float startAlt = orbital.OrbitAltitudeUnits;
            float elapsed  = 0f;

            while (elapsed < maneuverDuration)
            {
                elapsed += Time.deltaTime;
                float t   = Mathf.SmoothStep(0f, 1f, elapsed / maneuverDuration);
                float alt = Mathf.Lerp(startAlt, 0f, t);
                orbital.SetOrbitAltitudeUnits(alt);
                yield return null;
            }

            OnSatelliteDestroyed?.Invoke(this);
            Destroy(gameObject);
        }

        private static float GetEarthVisualRadius(OrbitalObject orbital) => 0.5f;

        private void DisableSatellite()
        {
            isDisabled = true;
            SatelliteData d = satelliteObject.SatelliteData;
            if (d != null)
            {
                d.status = SatelliteStatus.Decommissioned;
                satelliteObject.SetColor(d.displayColor); // resets to dim inactive colour
            }
            Debug.Log($"[SatelliteController] {name}: satellite disabled.");
        }

        private void DeployDrone()
        {
            isDroneDeployed = true;
            Debug.Log($"[SatelliteController] {name}: debris-cleanup drone deployed.");
            OnDroneDeployed?.Invoke(this);
        }

        private void SpendFuel(float amount)
        {
            currentFuel = Mathf.Max(0f, currentFuel - amount);
            if (currentFuel <= 0f)
                OnFuelDepleted?.Invoke(this);
        }
    }
}
