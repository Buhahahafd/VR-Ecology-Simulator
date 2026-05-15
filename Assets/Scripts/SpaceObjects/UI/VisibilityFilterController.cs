using System.Collections.Generic;
using UnityEngine;

namespace SpaceDebris
{
    public enum VisibilityMode
    {
        ShowAll,         // all objects and paths visible at full opacity
        DangerOnly,      // only medium/high-risk satellites visible
        HideFar,         // objects beyond maxVisibleRadius are faded out
        HighlightSelected // selected orbit at full opacity; everything else dim
    }

    /// <summary>
    /// Controls orbit-path and object visibility based on the current display mode.
    /// Reduces visual noise by fading irrelevant orbits and hiding safe objects.
    ///
    /// Wire up:
    ///   - Assign RiskCalculator reference.
    ///   - Call SetMode() from UI buttons or ScenarioManager.
    ///   - Call SetSelectedSatellite() when the player selects an object.
    /// </summary>
    public class VisibilityFilterController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private RiskCalculator riskCalculator;

        [Header("Filter Settings")]
        [Tooltip("Maximum orbit radius for HideFar mode (scene units from Earth centre).")]
        [SerializeField] private float maxVisibleRadius = 2f;

        [Tooltip("Alpha applied to 'dimmed' orbit lines.")]
        [Range(0f, 1f)]
        [SerializeField] private float dimmedPathAlpha = 0.05f;

        [Tooltip("Alpha applied to 'normal' orbit lines.")]
        [Range(0f, 1f)]
        [SerializeField] private float normalPathAlpha = 0.35f;

        [Tooltip("Alpha applied to the selected satellite's orbit line.")]
        [Range(0f, 1f)]
        [SerializeField] private float selectedPathAlpha = 1f;

        [Header("Update Rate")]
        [Tooltip("How often (seconds) the filter is re-applied.")]
        [SerializeField] private float updateInterval = 0.5f;

        // ── State ─────────────────────────────────────────────────────────────

        private VisibilityMode currentMode = VisibilityMode.ShowAll;
        private SatelliteObject selectedSatellite;
        private float timer;

        private readonly List<SatelliteObject> satellites = new();
        private readonly List<DebrisObject>    debrisList = new();

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Start()
        {
            if (riskCalculator == null)
                riskCalculator = FindFirstObjectByType<RiskCalculator>();

            RefreshObjectLists();
        }

        private void Update()
        {
            timer += Time.deltaTime;
            if (timer < updateInterval) return;
            timer = 0f;
            ApplyFilter();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Switches the active visibility mode and immediately re-applies the filter.</summary>
        public void SetMode(VisibilityMode mode)
        {
            currentMode = mode;
            ApplyFilter();
        }

        public VisibilityMode CurrentMode => currentMode;

        /// <summary>Sets the satellite whose orbit should be highlighted in HighlightSelected mode.</summary>
        public void SetSelectedSatellite(SatelliteObject sat)
        {
            selectedSatellite = sat;
            if (currentMode == VisibilityMode.HighlightSelected)
                ApplyFilter();
        }

        /// <summary>Re-scans the scene for orbital objects. Call after Regenerate().</summary>
        public void RefreshObjectLists()
        {
            satellites.Clear();
            debrisList.Clear();
            satellites.AddRange(FindObjectsByType<SatelliteObject>(FindObjectsSortMode.None));
            debrisList.AddRange(FindObjectsByType<DebrisObject>(FindObjectsSortMode.None));
        }

        // ── Private: filter logic ─────────────────────────────────────────────

        private void ApplyFilter()
        {
            foreach (SatelliteObject sat in satellites)
            {
                if (sat == null) continue;
                bool visible = ShouldShowSatellite(sat);
                ApplyToObject(sat.gameObject, sat, visible);
            }

            foreach (DebrisObject deb in debrisList)
            {
                if (deb == null) continue;
                bool visible = ShouldShowDebris(deb);
                ApplyToObject(deb.gameObject, null, visible);
            }
        }

        private bool ShouldShowSatellite(SatelliteObject sat)
        {
            return currentMode switch
            {
                VisibilityMode.ShowAll          => true,
                VisibilityMode.HighlightSelected => true, // all shown, only alpha differs
                VisibilityMode.DangerOnly        => IsDangerous(sat),
                VisibilityMode.HideFar           => sat.OrbitRadius <= maxVisibleRadius,
                _                                => true
            };
        }

        private bool ShouldShowDebris(DebrisObject deb)
        {
            return currentMode switch
            {
                VisibilityMode.DangerOnly => false,   // hide all debris in danger-only mode
                VisibilityMode.HideFar    => deb.OrbitRadius <= maxVisibleRadius,
                _                         => true
            };
        }

        private void ApplyToObject(GameObject go, SatelliteObject sat, bool visible)
        {
            // Object mesh renderer.
            Renderer rend = go.GetComponent<Renderer>();
            if (rend != null)
                rend.enabled = visible;

            // Orbit path line renderer.
            LineRenderer lr = go.GetComponent<LineRenderer>();
            if (lr == null) return;

            if (!visible)
            {
                lr.enabled = false;
                return;
            }

            lr.enabled = true;

            float alpha = currentMode switch
            {
                VisibilityMode.HighlightSelected when sat != null && sat == selectedSatellite
                    => selectedPathAlpha,
                VisibilityMode.HighlightSelected
                    => dimmedPathAlpha,
                VisibilityMode.DangerOnly when sat != null && IsHighRisk(sat)
                    => normalPathAlpha,
                VisibilityMode.DangerOnly
                    => dimmedPathAlpha,
                _ => normalPathAlpha
            };

            SetLineAlpha(lr, alpha);
        }

        private bool IsDangerous(SatelliteObject sat)
        {
            if (riskCalculator == null) return false;
            RiskLevel risk = riskCalculator.GetSatelliteRiskLevel(sat);
            return risk == RiskLevel.Medium || risk == RiskLevel.High;
        }

        private bool IsHighRisk(SatelliteObject sat)
        {
            if (riskCalculator == null) return false;
            return riskCalculator.GetSatelliteRiskLevel(sat) == RiskLevel.High;
        }

        private static void SetLineAlpha(LineRenderer lr, float alpha)
        {
            Gradient g = lr.colorGradient;
            GradientAlphaKey[] alphaKeys = g.alphaKeys;
            for (int i = 0; i < alphaKeys.Length; i++)
                alphaKeys[i].alpha = alpha;

            g.SetKeys(g.colorKeys, alphaKeys);
            lr.colorGradient = g;
        }
    }
}
