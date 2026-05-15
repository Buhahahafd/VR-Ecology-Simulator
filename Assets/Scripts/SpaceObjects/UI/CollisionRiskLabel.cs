using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceDebris
{
    /// <summary>
    /// World-space floating label that appears above a high-risk satellite.
    /// Shows:  "COLLISION RISK"
    ///          "82%"
    ///          "T- 00:02:17"
    ///
    /// Spawned and pooled by OrbitalMonitorHUD.
    /// Always billboards toward Main Camera.
    /// </summary>
    public class CollisionRiskLabel : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────

        [Tooltip("Offset above the satellite in world units.")]
        [SerializeField] private Vector3 offset = new Vector3(0f, 0.08f, 0f);

        // ── Runtime ───────────────────────────────────────────────────────

        private SatelliteObject  target;
        private RiskCalculator   riskCalculator;
        private Canvas           canvas;
        private TMP_Text         lblPercent;
        private TMP_Text         lblTime;

        private static readonly Color BgColor     = new Color(0.65f, 0.02f, 0.02f, 0.88f);
        private static readonly Color BorderColor  = new Color(1f,    0.18f, 0.08f, 1f);
        private static readonly Color TextTitle    = new Color(1f,    0.7f,  0.7f,  1f);
        private static readonly Color TextPct      = Color.white;
        private static readonly Color TextTime     = new Color(1f,    0.8f,  0.8f,  1f);

        // ── Unity lifecycle ───────────────────────────────────────────────

        private void Awake()
        {
            BuildLabel();
        }

        private void LateUpdate()
        {
            if (target == null)
            {
                Destroy(gameObject);
                return;
            }

            // Follow satellite.
            transform.position = target.transform.position + offset;

            // Billboard toward camera.
            Camera cam = Camera.main;
            if (cam != null)
                transform.rotation = Quaternion.LookRotation(
                    transform.position - cam.transform.position);

            // Update values.
            RefreshValues();
        }

        // ── Public API ────────────────────────────────────────────────────

        /// <summary>Binds this label to a satellite. Called by OrbitalMonitorHUD after instantiation.</summary>
        public void Bind(SatelliteObject satellite, RiskCalculator calc)
        {
            target        = satellite;
            riskCalculator = calc;
        }

        // ── Private ───────────────────────────────────────────────────────

        private void RefreshValues()
        {
            if (riskCalculator == null || lblPercent == null) return;

            float prob = riskCalculator.GetCollisionProbability(target);
            float tca  = riskCalculator.GetEstimatedTimeToClosestApproach(target);

            lblPercent.text = $"{prob * 100f:F0}%";
            lblTime.text    = tca < float.MaxValue ? $"T- {FormatTCA(tca)}" : "T- --:--:--";
        }

        private void BuildLabel()
        {
            // World-space canvas — renders in scene, always visible to both eyes.
            canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            gameObject.AddComponent<CanvasScaler>();

            RectTransform rt = GetComponent<RectTransform>();
            // 160×80 pixels internal, scaled to ~0.06 world units wide.
            rt.sizeDelta  = new Vector2(160f, 80f);
            float s = 0.0004f;
            rt.localScale = new Vector3(s, s, s);

            // Background
            Image bg = CreateImage(rt, "BG", BgColor,
                                   Vector2.zero, Vector2.one, new Vector2(2f, 2f), new Vector2(-2f, -2f));

            // Border frame
            Color bc = BorderColor;
            CreateLine(rt, "Top",    bc, new Vector2(0f,1f), new Vector2(1f,1f), new Vector2(0f,-1.5f), new Vector2(0f,0f));
            CreateLine(rt, "Bot",    bc, new Vector2(0f,0f), new Vector2(1f,0f), new Vector2(0f,0f),    new Vector2(0f,1.5f));
            CreateLine(rt, "Left",   bc, new Vector2(0f,0f), new Vector2(0f,1f), new Vector2(0f,0f),    new Vector2(1.5f,0f));
            CreateLine(rt, "Right",  bc, new Vector2(1f,0f), new Vector2(1f,1f), new Vector2(-1.5f,0f), new Vector2(0f,0f));

            // "СТОЛКНОВЕНИЕ" header
            TMP_Text hdr = CreateText(rt, "Header", "РИСК СТОЛКНОВЕНИЯ",
                                      10f, FontStyles.Bold, TextTitle,
                                      new Vector2(0f, 0.6f), Vector2.one,
                                      new Vector2(6f, 0f), new Vector2(-6f, -2f));
            hdr.alignment = TextAlignmentOptions.Center;

            // Percentage (large)
            lblPercent = CreateText(rt, "Percent", "—%",
                                    22f, FontStyles.Bold, TextPct,
                                    new Vector2(0f, 0.22f), new Vector2(1f, 0.68f),
                                    new Vector2(6f, 0f), new Vector2(-6f, 0f));
            lblPercent.alignment = TextAlignmentOptions.Center;

            // TCA countdown
            lblTime = CreateText(rt, "Time", "T- 00:00:00",
                                 9f, FontStyles.Normal, TextTime,
                                 Vector2.zero, new Vector2(1f, 0.28f),
                                 new Vector2(6f, 2f), new Vector2(-6f, -2f));
            lblTime.alignment = TextAlignmentOptions.Center;
        }

        // ── UGUI helpers ──────────────────────────────────────────────────

        private static Image CreateImage(RectTransform parent, string name, Color color,
                                          Vector2 anchorMin, Vector2 anchorMax,
                                          Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;
            Image img = go.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private static void CreateLine(RectTransform parent, string name, Color color,
                                        Vector2 anchorMin, Vector2 anchorMax,
                                        Vector2 offsetMin, Vector2 offsetMax)
        {
            CreateImage(parent, name, color, anchorMin, anchorMax, offsetMin, offsetMax);
        }

        private static TMP_Text CreateText(RectTransform parent, string name, string text,
                                            float size, FontStyles style, Color color,
                                            Vector2 anchorMin, Vector2 anchorMax,
                                            Vector2 offsetMin, Vector2 offsetMax)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(parent, false);
            RectTransform rt = go.AddComponent<RectTransform>();
            rt.anchorMin = anchorMin; rt.anchorMax = anchorMax;
            rt.offsetMin = offsetMin; rt.offsetMax = offsetMax;

            TMP_Text t = go.AddComponent<TextMeshProUGUI>();
            t.text               = text;
            t.fontSize           = size;
            t.fontStyle          = style;
            t.color              = color;
            t.enableWordWrapping = false;
            t.overflowMode       = TextOverflowModes.Ellipsis;
            return t;
        }

        private static string FormatTCA(float seconds)
        {
            int h = Mathf.FloorToInt(seconds / 3600f);
            int m = Mathf.FloorToInt((seconds % 3600f) / 60f);
            int s = Mathf.FloorToInt(seconds % 60f);
            return $"{h:00}:{m:00}:{s:00}";
        }
    }
}
