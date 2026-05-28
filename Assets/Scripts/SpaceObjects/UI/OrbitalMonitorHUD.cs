using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpaceDebris
{
    /// <summary>
    /// Orbital Monitor HUD. Builds all UGUI elements in code.
    /// Toggled open/closed via Q key (desktop) or an optional InputActionReference (VR controller).
    /// While open, smoothly follows the camera so it stays in front of the player.
    /// </summary>
    public class OrbitalMonitorHUD : MonoBehaviour
    {
        // ────────────────────────────────────────────────────────────────────
        // Inspector
        // ────────────────────────────────────────────────────────────────────

        [Header("HUD Position")]
        [Tooltip("Distance in front of the camera.")]
        [SerializeField] private float panelDistance = 1.4f;

        [Tooltip("Vertical offset relative to eye level.")]
        [SerializeField] private float panelHeightOffset = -0.15f;

        [Tooltip("Panel width in world units.")]
        [SerializeField] private float panelWorldWidth = 2.4f;

        [Tooltip("Panel height in world units.")]
        [SerializeField] private float panelWorldHeight = 1.5f;

        [Header("Follow Behaviour")]
        [Tooltip("How fast the panel lerps to the target position when the camera moves.")]
        [SerializeField] private float followSpeed = 5f;

        [Header("Input")]
        [Tooltip("Optional VR button action to toggle the HUD. Q key always works as fallback.")]
        [SerializeField] private InputActionReference toggleAction;

        [Header("References")]
        [SerializeField] private Transform earthTransform;

        // ────────────────────────────────────────────────────────────────────
        // Colour palette  (cyberpunk dark HUD)
        // ────────────────────────────────────────────────────────────────────

        private static readonly Color BgMain        = new Color(0.02f, 0.05f, 0.09f, 0.93f);
        private static readonly Color BgPanel       = new Color(0.03f, 0.08f, 0.13f, 0.97f);
        private static readonly Color BorderCyan    = new Color(0f,    0.76f, 1f,    1f);
        private static readonly Color AccentCyan    = new Color(0f,    0.85f, 1f,    1f);
        private static readonly Color AccentGreen   = new Color(0.1f,  0.95f, 0.35f, 1f);
        private static readonly Color AccentYellow  = new Color(1f,    0.85f, 0.1f,  1f);
        private static readonly Color AccentRed     = new Color(1f,    0.18f, 0.08f, 1f);
        private static readonly Color TextDim       = new Color(0.45f, 0.62f, 0.75f, 1f);
        private static readonly Color TextBright    = new Color(0.88f, 0.96f, 1f,    1f);
        private static readonly Color ScanLine      = new Color(0.05f, 0.35f, 0.55f, 0.06f);

        // ────────────────────────────────────────────────────────────────────
        // Runtime references updated each frame
        // ────────────────────────────────────────────────────────────────────

        private RiskCalculator riskCalculator;
        private OrbitSpawner   orbitSpawner;

        // ── Left panel labels ─────────────────────────────────────────────
        private TMP_Text lblTotalCount;
        private TMP_Text lblSafeCount;
        private TMP_Text lblCautionCount;
        private TMP_Text lblDangerCount;
        private TMP_Text lblUnknownCount;

        // ── Right panel – selected object ─────────────────────────────────
        private TMP_Text lblSelName;
        private TMP_Text lblSelStatus;
        private TMP_Text lblSelType;
        private TMP_Text lblSelAlt;
        private TMP_Text lblSelVel;
        private TMP_Text lblSelInc;
        private TMP_Text lblSelOrbit;
        private TMP_Text lblApproachObj;
        private TMP_Text lblApproachTime;
        private TMP_Text lblApproachDist;
        private TMP_Text lblApproachRisk;

        // ── Clock ─────────────────────────────────────────────────────────
        private TMP_Text lblClock;

        // ── Timeline ──────────────────────────────────────────────────────
        private RectTransform timelineContent;

        // ── Filter state ──────────────────────────────────────────────────
        private enum FilterMode { All, Satellites, Debris }
        private FilterMode activeFilter = FilterMode.All;
        private Button btnAll, btnSatellites, btnDebris;

        // ── Selected object ───────────────────────────────────────────────
        private SatelliteObject selectedSatellite;

        // ── HUD canvas ────────────────────────────────────────────────────
        private Canvas           hudCanvas;
        private GraphicRaycaster hudRaycaster;
        private Transform        cameraTransform;

        // ── Visibility / toggle ───────────────────────────────────────────
        private bool isOpen = false;

        // ────────────────────────────────────────────────────────────────────
        // Unity lifecycle
        // ────────────────────────────────────────────────────────────────────

        private void Start()
        {
            riskCalculator  = FindFirstObjectByType<RiskCalculator>();
            orbitSpawner    = FindFirstObjectByType<OrbitSpawner>();
            cameraTransform = Camera.main != null ? Camera.main.transform : null;

            if (earthTransform == null)
            {
                GameObject e = GameObject.Find("Earth");
                if (e != null) earthTransform = e.transform;
            }

            if (orbitSpawner != null)
                orbitSpawner.OnObjectSpawned += OnObjectSpawned;

            if (toggleAction != null)
                toggleAction.action.Enable();

            BuildHUD();

            // Start hidden — player opens with Q or controller button.
            // NOTE: we keep the GameObject active so Update() always runs;
            // visibility is controlled via Canvas.enabled instead of SetActive.
            hudCanvas.enabled    = false;
            hudRaycaster.enabled = false;
        }

        private void OnDestroy()
        {
            if (orbitSpawner != null)
                orbitSpawner.OnObjectSpawned -= OnObjectSpawned;

            if (toggleAction != null)
                toggleAction.action.Disable();
        }

        private void Update()
        {
            if (cameraTransform == null)
                cameraTransform = Camera.main != null ? Camera.main.transform : null;

            HandleToggleInput();

            if (!isOpen) return;

            SmoothFollow();
            UpdateClock();
            UpdateObjectCounts();
            UpdateSelectedPanel();
        }

        // ────────────────────────────────────────────────────────────────────
        // Public API used by SpaceObjectInteractable
        // ────────────────────────────────────────────────────────────────────

        /// <summary>Highlights a satellite in the right telemetry panel.</summary>
        public void SelectSatellite(SatelliteObject sat)
        {
            selectedSatellite = sat;
            RefreshSelectedPanel();
        }

        public void Deselect() => selectedSatellite = null;

        // ────────────────────────────────────────────────────────────────────
        // Toggle & follow
        // ────────────────────────────────────────────────────────────────────

        private void HandleToggleInput()
        {
            bool pressed = false;

            // Q key (desktop / editor).
            if (Keyboard.current != null && Keyboard.current.qKey.wasPressedThisFrame)
                pressed = true;

            // Optional VR button action.
            if (toggleAction != null && toggleAction.action.WasPressedThisFrame())
                pressed = true;

            if (!pressed) return;

            isOpen = !isOpen;
            hudCanvas.enabled    = isOpen;
            hudRaycaster.enabled = isOpen;

            // Snap to camera immediately when opening so it doesn't lerp from old position.
            if (isOpen)
                SnapToCamera();
        }

        /// <summary>Snaps the panel directly in front of the camera (no lerp).</summary>
        private void SnapToCamera()
        {
            if (cameraTransform == null) return;

            Vector3 fwd = GetCameraForwardFlat();
            transform.position = cameraTransform.position
                + fwd * panelDistance
                + Vector3.up * panelHeightOffset;
            transform.rotation = Quaternion.LookRotation(fwd);
        }

        /// <summary>Smoothly moves the panel toward the target position in front of the camera.</summary>
        private void SmoothFollow()
        {
            if (cameraTransform == null) return;

            Vector3 fwd       = GetCameraForwardFlat();
            Vector3 targetPos = cameraTransform.position
                + fwd * panelDistance
                + Vector3.up * panelHeightOffset;
            Quaternion targetRot = Quaternion.LookRotation(fwd);

            float t = 1f - Mathf.Exp(-followSpeed * Time.deltaTime);
            transform.position = Vector3.Lerp(transform.position, targetPos, t);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRot, t);
        }

        /// <summary>Returns the camera's horizontal forward vector (Y zeroed and normalised).</summary>
        private Vector3 GetCameraForwardFlat()
        {
            Vector3 fwd = cameraTransform.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 0.01f) fwd = Vector3.forward;
            return fwd.normalized;
        }

        // ────────────────────────────────────────────────────────────────────
        // Event handlers
        // ────────────────────────────────────────────────────────────────────

        private void OnObjectSpawned(OrbitalObject obj)
        {
            if (obj.TryGetComponent<SpaceObjectInteractable>(out var soi))
                soi.OnMonitorSelect += SelectSatellite;
        }

        // ────────────────────────────────────────────────────────────────────
        // Per-frame updates
        // ────────────────────────────────────────────────────────────────────

        private void UpdateClock()
        {
            if (lblClock == null) return;
            DateTime utc = DateTime.UtcNow;
            lblClock.text = $"UTC  {utc:yyyy-MM-dd}  {utc:HH:mm:ss}";
        }

        private void UpdateObjectCounts()
        {
            if (riskCalculator == null || lblTotalCount == null) return;

            var sats = FindObjectsByType<SatelliteObject>(FindObjectsSortMode.None);
            int safe = 0, caution = 0, danger = 0;

            foreach (SatelliteObject s in sats)
            {
                RiskLevel r = riskCalculator.GetSatelliteRiskLevel(s);
                if      (r == RiskLevel.Low)    safe++;
                else if (r == RiskLevel.Medium) caution++;
                else                            danger++;
            }

            lblTotalCount.text   = sats.Length.ToString();
            lblSafeCount.text    = safe.ToString();
            lblCautionCount.text = caution.ToString();
            lblDangerCount.text  = danger.ToString();
            lblUnknownCount.text = "0";
        }

        private void UpdateSelectedPanel()
        {
            if (selectedSatellite == null) return;
            // Throttle: refresh only every ~0.3 s via a simple timer check.
            RefreshSelectedPanel();
        }

        private void RefreshSelectedPanel()
        {
            if (selectedSatellite == null || lblSelName == null) return;

            SatelliteData d = selectedSatellite.SatelliteData;
            if (d == null) return;

            lblSelName.text   = d.objectName;
            lblSelType.text   = LocalizeSatType(d.satelliteType);
            lblSelAlt.text    = $"{d.altitudeSceneUnits * 800f:F0} km";
            lblSelVel.text    = $"{d.angularSpeed * 0.17f:F2} km/s";
            lblSelInc.text    = $"{Mathf.Abs(d.orbitalInclination):F1}°";
            lblSelOrbit.text  = d.orbitType.ToString();

            RiskLevel risk = riskCalculator != null
                ? riskCalculator.GetSatelliteRiskLevel(selectedSatellite)
                : RiskLevel.Low;

            lblSelStatus.text  = LocalizeStatus(d.status);
            lblSelStatus.color = risk switch
            {
                RiskLevel.High   => AccentRed,
                RiskLevel.Medium => AccentYellow,
                _                => AccentGreen
            };

            if (riskCalculator != null)
            {
                OrbitalObject threat = riskCalculator.GetNearestThreat(selectedSatellite);
                float dist  = riskCalculator.GetNearestThreatDistance(selectedSatellite);
                float tca   = riskCalculator.GetEstimatedTimeToClosestApproach(selectedSatellite);
                float prob  = riskCalculator.GetCollisionProbability(selectedSatellite);

                string threatName = threat != null
                    ? ((threat as SatelliteObject)?.SatelliteData?.objectName
                       ?? threat.GetData()?.objectName
                       ?? threat.name)
                    : null;
                lblApproachObj.text  = threatName ?? "—";
                lblApproachTime.text = tca < float.MaxValue ? FormatTCA(tca) : "—";
                lblApproachDist.text = dist < float.MaxValue ? $"{dist * 800f:F0} km" : "—";
                lblApproachRisk.text = $"{prob * 100f:F0}%";
                lblApproachRisk.color = prob > 0.6f ? AccentRed : prob > 0.3f ? AccentYellow : AccentGreen;
            }
        }

        // ────────────────────────────────────────────────────────────────────
        // HUD BUILDER
        // ────────────────────────────────────────────────────────────────────

        private void BuildHUD()
        {
            // ── Root canvas ───────────────────────────────────────────────
            Canvas canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            gameObject.AddComponent<CanvasScaler>();
            hudRaycaster = gameObject.AddComponent<GraphicRaycaster>();

            RectTransform rt = GetComponent<RectTransform>();
            // 1600×1000 internal resolution → maps to panelWorldWidth × panelWorldHeight
            rt.sizeDelta = new Vector2(1600f, 1000f);
            float scaleX = panelWorldWidth  / 1600f;
            float scaleY = panelWorldHeight / 1000f;
            rt.localScale = new Vector3(scaleX, scaleY, scaleX);

            hudCanvas = canvas;

            // ── Full background ───────────────────────────────────────────
            CreateImage(rt, "BG", BgMain, Vector2.zero, Vector2.one,
                        Vector2.zero, Vector2.zero);

            // Subtle scanline overlay (very dark horizontal stripe repeating image)
            // — approximated by a very dim panel-wide Image
            Image scanOverlay = CreateImage(rt, "Scanlines", ScanLine,
                                            Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            scanOverlay.type = Image.Type.Tiled;

            // ── Top bar ───────────────────────────────────────────────────
            BuildTopBar(rt);

            // ── Left panel (object counts + filter) ───────────────────────
            BuildLeftPanel(rt);

            // ── Right panel (selected object telemetry) ───────────────────
            BuildRightPanel(rt);

            // ── Bottom bar (timeline + buttons + legend) ───────────────────
            BuildBottomBar(rt);

            // ── Centre border lines (decorative) ─────────────────────────
            BuildCentreDecor(rt);
        }

        // ────────────────────────────────────────────────────────────────────
        // TOP BAR
        // ────────────────────────────────────────────────────────────────────

        private void BuildTopBar(RectTransform parent)
        {
            // Background strip
            RectTransform bar = CreatePanel(parent, "TopBar", BgPanel,
                                            anchorMin: new Vector2(0f, 0.88f),
                                            anchorMax: new Vector2(1f, 1f),
                                            offsetMin: Vector2.zero, offsetMax: Vector2.zero);

            // Bottom border line of top bar
            CreateHLine(bar, "TopBorderBot", BorderCyan, 0f, 0f, 1f, 1.5f);

            // Title
            TMP_Text title = CreateText(bar, "Title", "ОРБИТАЛЬНЫЙ МОНИТОР",
                                        22, FontStyles.Bold, TextBright,
                                        new Vector2(0f, 0.5f), new Vector2(0.35f, 0.5f),
                                        new Vector2(20f, 0f), new Vector2(0f, 0f));
            title.alignment = TextAlignmentOptions.MidlineLeft;

            TMP_Text sub = CreateText(bar, "Subtitle", "ЗЕМЛЯ  //  ГЕО-ЦЕНТРИРОВАННЫЙ ВИД",
                                      9, FontStyles.Normal, TextDim,
                                      new Vector2(0f, 0f), new Vector2(0.35f, 0.45f),
                                      new Vector2(20f, 0f), new Vector2(0f, 0f));
            sub.alignment = TextAlignmentOptions.MidlineLeft;

            // Clock (right-aligned)
            lblClock = CreateText(bar, "Clock", "UTC  2024-01-01  00:00:00",
                                  10, FontStyles.Normal, AccentCyan,
                                  new Vector2(0.65f, 0f), new Vector2(1f, 1f),
                                  new Vector2(0f, 0f), new Vector2(-20f, 0f));
            lblClock.alignment = TextAlignmentOptions.MidlineRight;
        }

        // ────────────────────────────────────────────────────────────────────
        // LEFT PANEL
        // ────────────────────────────────────────────────────────────────────

        private void BuildLeftPanel(RectTransform parent)
        {
            RectTransform panel = CreatePanel(parent, "LeftPanel", BgPanel,
                                              new Vector2(0f, 0.13f), new Vector2(0.22f, 0.88f),
                                              new Vector2(10f, 0f), new Vector2(-5f, 0f));

            CreateBorderFrame(panel, BorderCyan, 1f);

            // "OBJECTS" header
            TMP_Text hdr = CreateText(panel, "ObjHdr", "ОБЪЕКТЫ",
                                      10, FontStyles.Bold, TextDim,
                                      new Vector2(0f, 0.88f), new Vector2(0.6f, 1f),
                                      new Vector2(12f, 0f), new Vector2(0f, -4f));
            hdr.alignment = TextAlignmentOptions.MidlineLeft;

            lblTotalCount = CreateText(panel, "TotalCount", "—",
                                       22, FontStyles.Bold, TextBright,
                                       new Vector2(0.55f, 0.82f), new Vector2(1f, 1f),
                                       new Vector2(0f, 0f), new Vector2(-12f, -4f));
            lblTotalCount.alignment = TextAlignmentOptions.MidlineRight;

            // Divider
            CreateHLine(panel, "Div1", BorderCyan * 0.5f, 0.8f, 0.02f, 0.98f, 0.8f);

            // Count rows
            (lblSafeCount, _)    = BuildCountRow(panel, "SAFE",    "НОРМА",    AccentGreen,  0.68f);
            (lblCautionCount, _) = BuildCountRow(panel, "CAUTION", "ВНИМАНИЕ", AccentYellow, 0.57f);
            (lblDangerCount, _)  = BuildCountRow(panel, "DANGER",  "ОПАСНОСТЬ",AccentRed,    0.46f);
            (lblUnknownCount, _) = BuildCountRow(panel, "UNKNOWN", "НЕИЗВЕСТНО",TextDim,     0.35f);

            // Divider
            CreateHLine(panel, "Div2", BorderCyan * 0.5f, 0.3f, 0.02f, 0.98f, 0.8f);

            // FILTER label
            TMP_Text filterHdr = CreateText(panel, "FilterHdr", "ФИЛЬТР",
                                            9, FontStyles.Bold, TextDim,
                                            new Vector2(0f, 0.22f), new Vector2(1f, 0.3f),
                                            new Vector2(12f, 0f), new Vector2(0f, 0f));
            filterHdr.alignment = TextAlignmentOptions.MidlineLeft;

            // Filter buttons row
            RectTransform filterRow = CreateLayoutGroup(panel, "FilterRow",
                                                        new Vector2(0f, 0.05f),
                                                        new Vector2(1f, 0.22f),
                                                        new Vector2(10f, 4f),
                                                        new Vector2(-10f, -4f));

            btnAll        = CreateFilterButton(filterRow, "ВСЕ",      FilterMode.All);
            btnSatellites = CreateFilterButton(filterRow, "СПУТНИКИ", FilterMode.Satellites);
            btnDebris     = CreateFilterButton(filterRow, "МУСОР",    FilterMode.Debris);
            UpdateFilterButtons();
        }

        private (TMP_Text count, TMP_Text label) BuildCountRow(
            RectTransform parent, string key, string displayName, Color color, float anchorY)
        {
            float rowH = 0.1f;

            // Dot indicator
            CreateImage(parent, $"Dot_{key}", color,
                        new Vector2(0f, anchorY), new Vector2(0f, anchorY + rowH),
                        new Vector2(12f, 6f), new Vector2(20f, -6f));

            TMP_Text lbl = CreateText(parent, $"Lbl_{key}", displayName,
                                      9, FontStyles.Normal, color,
                                      new Vector2(0f, anchorY), new Vector2(0.7f, anchorY + rowH),
                                      new Vector2(26f, 0f), new Vector2(0f, 0f));
            lbl.alignment = TextAlignmentOptions.MidlineLeft;

            TMP_Text count = CreateText(parent, $"Count_{key}", "—",
                                        9, FontStyles.Bold, TextBright,
                                        new Vector2(0.65f, anchorY), new Vector2(1f, anchorY + rowH),
                                        new Vector2(0f, 0f), new Vector2(-12f, 0f));
            count.alignment = TextAlignmentOptions.MidlineRight;

            return (count, lbl);
        }

        // ────────────────────────────────────────────────────────────────────
        // RIGHT PANEL
        // ────────────────────────────────────────────────────────────────────

        private void BuildRightPanel(RectTransform parent)
        {
            RectTransform panel = CreatePanel(parent, "RightPanel", BgPanel,
                                              new Vector2(0.78f, 0.13f), new Vector2(1f, 0.88f),
                                              new Vector2(5f, 0f), new Vector2(-10f, 0f));
            CreateBorderFrame(panel, BorderCyan, 1f);

            // Satellite name + status
            lblSelName = CreateText(panel, "SelName", "—",
                                    14, FontStyles.Bold, TextBright,
                                    new Vector2(0f, 0.9f), new Vector2(0.7f, 1f),
                                    new Vector2(12f, 0f), new Vector2(0f, -4f));
            lblSelName.alignment = TextAlignmentOptions.MidlineLeft;

            lblSelStatus = CreateText(panel, "SelStatus", "ВЫБЕРИТЕ ОБЪЕКТ",
                                      9, FontStyles.Bold, AccentGreen,
                                      new Vector2(0.65f, 0.9f), new Vector2(1f, 1f),
                                      new Vector2(0f, 0f), new Vector2(-12f, -4f));
            lblSelStatus.alignment = TextAlignmentOptions.MidlineRight;

            CreateHLine(panel, "SelDiv", BorderCyan * 0.6f, 0.88f, 0.02f, 0.98f, 0.8f);

            // Data rows
            float y = 0.82f;
            const float rowStep = 0.095f;

            lblSelType  = BuildDataRow(panel, "ТИП ОБЪЕКТА",  ref y, rowStep);
            BuildDataRow(panel, "ОПЕРАТОР",    ref y, rowStep); // static for now
            lblSelAlt   = BuildDataRow(panel, "ВЫСОТА",       ref y, rowStep);
            lblSelVel   = BuildDataRow(panel, "СКОРОСТЬ",     ref y, rowStep);
            lblSelInc   = BuildDataRow(panel, "НАКЛОНЕНИЕ",   ref y, rowStep);
            lblSelOrbit = BuildDataRow(panel, "ОРБИТА",       ref y, rowStep);

            // "NEXT CLOSE APPROACH" sub-header
            TMP_Text ncaHdr = CreateText(panel, "NCAHdr", "СБЛИЖЕНИЕ",
                                         9, FontStyles.Bold, AccentCyan,
                                         new Vector2(0f, y - 0.02f), new Vector2(1f, y + 0.07f),
                                         new Vector2(12f, 0f), new Vector2(0f, 0f));
            ncaHdr.alignment = TextAlignmentOptions.MidlineLeft;
            y -= 0.09f;

            CreateHLine(panel, "NCADiv", BorderCyan * 0.35f, y + 0.06f, 0.02f, 0.98f, 0.7f);
            y -= 0.01f;

            lblApproachObj  = BuildDataRow(panel, "ОБЪЕКТ",    ref y, rowStep);
            lblApproachTime = BuildDataRow(panel, "ВРЕМЯ",     ref y, rowStep);
            lblApproachDist = BuildDataRow(panel, "ДИСТАНЦИЯ", ref y, rowStep);
            lblApproachRisk = BuildDataRow(panel, "РИСК",      ref y, rowStep);
            lblApproachRisk.color = AccentGreen;
        }

        private TMP_Text BuildDataRow(RectTransform parent, string label, ref float anchorY, float step)
        {
            float top = anchorY;
            float bot = anchorY - step + 0.005f;

            TMP_Text lbl = CreateText(parent, $"RowLbl_{label}", label,
                                      8, FontStyles.Normal, TextDim,
                                      new Vector2(0f, bot), new Vector2(0.52f, top),
                                      new Vector2(12f, 0f), new Vector2(0f, 0f));
            lbl.alignment = TextAlignmentOptions.MidlineLeft;

            TMP_Text val = CreateText(parent, $"RowVal_{label}", "—",
                                      9, FontStyles.Bold, TextBright,
                                      new Vector2(0.5f, bot), new Vector2(1f, top),
                                      new Vector2(0f, 0f), new Vector2(-12f, 0f));
            val.alignment = TextAlignmentOptions.MidlineRight;

            anchorY -= step;
            return val;
        }

        // ────────────────────────────────────────────────────────────────────
        // BOTTOM BAR
        // ────────────────────────────────────────────────────────────────────

        private void BuildBottomBar(RectTransform parent)
        {
            RectTransform bar = CreatePanel(parent, "BottomBar", BgPanel,
                                            new Vector2(0f, 0f), new Vector2(1f, 0.13f),
                                            new Vector2(10f, 5f), new Vector2(-10f, -5f));
            CreateBorderFrame(bar, BorderCyan, 1f);

            // ── Timeline (left third) ──────────────────────────────────────
            RectTransform tl = CreatePanel(bar, "Timeline", new Color(0.03f, 0.07f, 0.12f, 1f),
                                           new Vector2(0f, 0f), new Vector2(0.33f, 1f),
                                           new Vector2(6f, 6f), new Vector2(-6f, -6f));

            TMP_Text tlHdr = CreateText(tl, "TimelineHdr", "ВРЕМЕННАЯ ОСЬ",
                                        8, FontStyles.Bold, TextDim,
                                        new Vector2(0f, 0.6f), new Vector2(1f, 1f),
                                        new Vector2(10f, 0f), new Vector2(0f, 0f));
            tlHdr.alignment = TextAlignmentOptions.MidlineLeft;

            // Timeline track
            RectTransform track = CreatePanel(tl, "Track", new Color(0.06f, 0.12f, 0.2f, 1f),
                                              new Vector2(0f, 0.1f), new Vector2(1f, 0.55f),
                                              new Vector2(10f, 0f), new Vector2(-10f, 0f));

            timelineContent = track;
            CreateHLine(track, "TrackLine", AccentCyan * 0.4f, 0.5f, 0.01f, 0.99f, 0.8f);

            // Time labels 0 / 15 / 30 / 45 / 60
            string[] tLabels = { "0 min", "15 min", "30 min", "45 min", "60 min" };
            for (int i = 0; i < tLabels.Length; i++)
            {
                float x = i / 4f;
                TMP_Text t = CreateText(tl, $"T{i}", tLabels[i],
                                        7, FontStyles.Normal, TextDim,
                                        new Vector2(x == 0 ? 0 : x - 0.12f, 0f),
                                        new Vector2(x == 1f ? 1f : x + 0.12f, 0.28f),
                                        new Vector2(8f, 0f), new Vector2(-8f, 0f));
                t.alignment = i == 0 ? TextAlignmentOptions.MidlineLeft : TextAlignmentOptions.Center;
            }

            // ── Action buttons (centre third) ─────────────────────────────
            RectTransform btnsArea = CreateLayoutGroup(bar, "ActionBtns",
                                                       new Vector2(0.33f, 0f),
                                                       new Vector2(0.67f, 1f),
                                                       new Vector2(10f, 10f),
                                                       new Vector2(-10f, -10f));

            BuildActionButton(btnsArea, "ФОКУС\nЗЕМЛЯ",    () => FocusEarth());
            BuildActionButton(btnsArea, "СБРОС\nВИДА",     () => SnapToCamera());
            BuildActionButton(btnsArea, "СЕТКА\nВКЛ/ВЫКЛ", () => ToggleGrid());
            BuildActionButton(btnsArea, "ИНФО\nОБЪЕКТА",   () => { });

            // ── Legend (right third) ──────────────────────────────────────
            RectTransform leg = CreatePanel(bar, "Legend", Color.clear,
                                            new Vector2(0.68f, 0f), new Vector2(1f, 1f),
                                            new Vector2(10f, 6f), new Vector2(-6f, -6f));

            BuildLegendRow(leg, "СПУТНИК",       AccentCyan,   new Color(0.1f,0.9f,0.35f,1f), "— НОРМА",    0.72f);
            BuildLegendRow(leg, "СТАНЦИЯ",       AccentYellow, new Color(1f,0.85f,0.1f,1f),   "— ВНИМАНИЕ", 0.4f);
            BuildLegendRow(leg, "МУСОР",         TextDim,      AccentRed,                      "— ОПАСНОСТЬ",0.08f);
        }

        private void BuildLegendRow(RectTransform parent, string objLabel, Color dotColor,
                                    Color lineColor, string lineLabel, float anchorY)
        {
            float h = 0.28f;

            CreateImage(parent, $"LDot_{objLabel}", dotColor,
                        new Vector2(0f, anchorY), new Vector2(0f, anchorY + h),
                        new Vector2(8f, 8f), new Vector2(20f, -8f));

            TMP_Text lbl = CreateText(parent, $"LLbl_{objLabel}", objLabel,
                                      8, FontStyles.Normal, TextDim,
                                      new Vector2(0f, anchorY), new Vector2(0.45f, anchorY + h),
                                      new Vector2(26f, 0f), new Vector2(0f, 0f));
            lbl.alignment = TextAlignmentOptions.MidlineLeft;

            // dashed line indicator
            CreateImage(parent, $"LLine_{objLabel}", lineColor,
                        new Vector2(0.46f, anchorY + 0.08f), new Vector2(0.46f, anchorY + h - 0.08f),
                        new Vector2(0f, 0f), new Vector2(18f, 0f));

            TMP_Text lineLbl = CreateText(parent, $"LLineLbl_{objLabel}", lineLabel,
                                          7, FontStyles.Normal, lineColor,
                                          new Vector2(0.5f, anchorY), new Vector2(1f, anchorY + h),
                                          new Vector2(6f, 0f), new Vector2(-6f, 0f));
            lineLbl.alignment = TextAlignmentOptions.MidlineLeft;
        }

        private void BuildCentreDecor(RectTransform parent)
        {
            // Left edge of centre zone
            CreateVLine(parent, "LeftEdge",  BorderCyan * 0.4f, 0.22f, 0.13f, 0.88f, 0.8f);
            // Right edge of centre zone
            CreateVLine(parent, "RightEdge", BorderCyan * 0.4f, 0.78f, 0.13f, 0.88f, 0.8f);
        }

        // ────────────────────────────────────────────────────────────────────
        // Filter button logic
        // ────────────────────────────────────────────────────────────────────

        private Button CreateFilterButton(RectTransform parent, string label, FilterMode mode)
        {
            GameObject go = new GameObject($"FilterBtn_{label}");
            go.transform.SetParent(parent, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;

            Image bg = go.AddComponent<Image>();
            bg.color = new Color(0.06f, 0.14f, 0.22f, 1f);

            Button btn = go.AddComponent<Button>();

            // Label
            GameObject lblGo = new GameObject("Label");
            lblGo.transform.SetParent(go.transform, false);
            RectTransform lblRt = lblGo.AddComponent<RectTransform>();
            lblRt.anchorMin = Vector2.zero;
            lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = Vector2.zero;
            lblRt.offsetMax = Vector2.zero;

            TMP_Text txt = lblGo.AddComponent<TextMeshProUGUI>();
            txt.text      = label;
            txt.fontSize  = 8f;
            txt.fontStyle = FontStyles.Bold;
            txt.color     = TextDim;
            txt.alignment = TextAlignmentOptions.Center;

            btn.onClick.AddListener(() =>
            {
                activeFilter = mode;
                UpdateFilterButtons();
                ApplyFilter(mode);
            });

            return btn;
        }

        private void UpdateFilterButtons()
        {
            SetFilterBtnActive(btnAll,        activeFilter == FilterMode.All);
            SetFilterBtnActive(btnSatellites, activeFilter == FilterMode.Satellites);
            SetFilterBtnActive(btnDebris,     activeFilter == FilterMode.Debris);
        }

        private void SetFilterBtnActive(Button btn, bool active)
        {
            if (btn == null) return;
            Image bg = btn.GetComponent<Image>();
            TMP_Text txt = btn.GetComponentInChildren<TMP_Text>();
            if (bg  != null) bg.color  = active ? BorderCyan * 0.35f : new Color(0.06f, 0.14f, 0.22f, 1f);
            if (txt != null) txt.color = active ? AccentCyan : TextDim;
        }

        private void ApplyFilter(FilterMode mode)
        {
            var sats  = FindObjectsByType<SatelliteObject>(FindObjectsSortMode.None);
            var debs  = FindObjectsByType<DebrisObject>(FindObjectsSortMode.None);

            foreach (SatelliteObject s in sats)
                s.gameObject.SetActive(mode == FilterMode.All || mode == FilterMode.Satellites);

            foreach (DebrisObject d in debs)
                d.gameObject.SetActive(mode == FilterMode.All || mode == FilterMode.Debris);
        }

        // ────────────────────────────────────────────────────────────────────
        // Action button handlers
        // ────────────────────────────────────────────────────────────────────

        private void FocusEarth()
        {
            if (earthTransform == null || cameraTransform == null) return;
            Vector3 dir = (cameraTransform.position - earthTransform.position).normalized;
            transform.position = earthTransform.position + dir * panelDistance;
            transform.LookAt(cameraTransform.position);
        }

        private void ToggleGrid()
        {
            NeonGridGenerator grid = FindFirstObjectByType<NeonGridGenerator>();
            if (grid != null) grid.gameObject.SetActive(!grid.gameObject.activeSelf);
        }

        // ────────────────────────────────────────────────────────────────────
        // Action button builder
        // ────────────────────────────────────────────────────────────────────

        private void BuildActionButton(RectTransform parent, string label, Action onClick)
        {
            GameObject go = new GameObject($"ActBtn_{label.Replace('\n', '_')}");
            go.transform.SetParent(parent, false);

            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;

            Image bg = go.AddComponent<Image>();
            bg.color = new Color(0.04f, 0.12f, 0.22f, 1f);

            // Cyan border via outline child
            CreateBorderFrame(rt, BorderCyan * 0.7f, 1f);

            Button btn = go.AddComponent<Button>();
            btn.onClick.AddListener(() => onClick?.Invoke());

            ColorBlock cb = btn.colors;
            cb.highlightedColor = BorderCyan * 0.3f;
            cb.pressedColor     = AccentCyan  * 0.5f;
            btn.colors = cb;

            GameObject lblGo = new GameObject("Label");
            lblGo.transform.SetParent(go.transform, false);
            RectTransform lblRt = lblGo.AddComponent<RectTransform>();
            lblRt.anchorMin = Vector2.zero;
            lblRt.anchorMax = Vector2.one;
            lblRt.offsetMin = new Vector2(4f, 4f);
            lblRt.offsetMax = new Vector2(-4f, -4f);

            TMP_Text txt = lblGo.AddComponent<TextMeshProUGUI>();
            txt.text      = label;
            txt.fontSize  = 8.5f;
            txt.fontStyle = FontStyles.Bold;
            txt.color     = AccentCyan;
            txt.alignment = TextAlignmentOptions.Center;
        }

        // ────────────────────────────────────────────────────────────────────
        // UGUI primitive helpers
        // ────────────────────────────────────────────────────────────────────

        private static Image CreateImage(RectTransform parent, string name, Color color,
                                         Vector2 anchorMin, Vector2 anchorMax,
                                         Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;
            Image img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static RectTransform CreatePanel(RectTransform parent, string name, Color color,
                                                  Vector2 anchorMin, Vector2 anchorMax,
                                                  Vector2 offsetMin, Vector2 offsetMax)
        {
            Image img = CreateImage(parent, name, color, anchorMin, anchorMax, offsetMin, offsetMax);
            return img.rectTransform;
        }

        private static TMP_Text CreateText(RectTransform parent, string name, string text,
                                           float fontSize, FontStyles style, Color color,
                                           Vector2 anchorMin, Vector2 anchorMax,
                                           Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;

            TMP_Text t = go.AddComponent<TextMeshProUGUI>();
            t.text      = text;
            t.fontSize  = fontSize;
            t.fontStyle = style;
            t.color     = color;
            t.enableWordWrapping = false;
            t.overflowMode = TextOverflowModes.Ellipsis;
            return t;
        }

        private static void CreateBorderFrame(RectTransform parent, Color color, float thickness)
        {
            // Top, Bottom, Left, Right lines
            CreateHLine(parent, "BorderTop",    color, 1f,   0f, 1f, thickness);
            CreateHLine(parent, "BorderBottom", color, 0f,   0f, 1f, thickness);
            CreateVLine(parent, "BorderLeft",   color, 0f,   0f, 1f, thickness);
            CreateVLine(parent, "BorderRight",  color, 1f,   0f, 1f, thickness);
        }

        private static void CreateHLine(RectTransform parent, string name, Color color,
                                        float anchorY, float anchorXMin, float anchorXMax, float pixHeight)
        {
            Image img = CreateImage(parent, name, color,
                                    new Vector2(anchorXMin, anchorY),
                                    new Vector2(anchorXMax, anchorY),
                                    new Vector2(0f, -pixHeight * 0.5f),
                                    new Vector2(0f,  pixHeight * 0.5f));
        }

        private static void CreateVLine(RectTransform parent, string name, Color color,
                                        float anchorX, float anchorYMin, float anchorYMax, float pixWidth)
        {
            Image img = CreateImage(parent, name, color,
                                    new Vector2(anchorX, anchorYMin),
                                    new Vector2(anchorX, anchorYMax),
                                    new Vector2(-pixWidth * 0.5f, 0f),
                                    new Vector2( pixWidth * 0.5f, 0f));
        }

        private static RectTransform CreateLayoutGroup(RectTransform parent, string name,
                                                        Vector2 anchorMin, Vector2 anchorMax,
                                                        Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin;
            rt.offsetMax = offsetMax;

            HorizontalLayoutGroup hlg = go.AddComponent<HorizontalLayoutGroup>();
            hlg.spacing            = 8f;
            hlg.padding            = new RectOffset(4, 4, 4, 4);
            hlg.childForceExpandWidth  = true;
            hlg.childForceExpandHeight = true;
            hlg.childAlignment     = TextAnchor.MiddleCenter;
            return rt;
        }

        // ────────────────────────────────────────────────────────────────────
        // String helpers
        // ────────────────────────────────────────────────────────────────────

        private static string FormatTCA(float seconds)
        {
            int h = Mathf.FloorToInt(seconds / 3600f);
            int m = Mathf.FloorToInt((seconds % 3600f) / 60f);
            int s = Mathf.FloorToInt(seconds % 60f);
            return $"{h:00}:{m:00}:{s:00}";
        }

        private static string LocalizeSatType(SatelliteType t) => t switch
        {
            SatelliteType.Communication    => "СВЯЗЬ",
            SatelliteType.Navigation       => "НАВИГАЦИЯ",
            SatelliteType.EarthObservation => "НАБЛЮДЕНИЕ ЗЕМЛИ",
            SatelliteType.Scientific       => "НАУЧНЫЙ",
            _                              => t.ToString().ToUpper()
        };

        private static string LocalizeStatus(SatelliteStatus s) => s switch
        {
            SatelliteStatus.Active         => "АКТИВЕН",
            SatelliteStatus.Inactive       => "НЕАКТИВЕН",
            SatelliteStatus.Decommissioned => "ВЫВЕДЕН ИЗ ЭКСПЛУАТАЦИИ",
            _                              => s.ToString().ToUpper()
        };
    }
}
