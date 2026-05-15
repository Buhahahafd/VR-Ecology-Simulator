using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SpaceDebris
{
    /// <summary>
    /// Applies cyberpunk neon style to a world-space Canvas panel at runtime.
    /// Attach to the root GameObject of a Canvas or panel.
    ///
    /// Automatically:
    ///   - Paints panel backgrounds dark (near-black with slight blue tint).
    ///   - Adds a glowing cyan border via an outline Image child.
    ///   - Colors all TMP_Text elements in the panel hierarchy.
    ///   - Colors buttons with dark fill + cyan border.
    /// </summary>
    public class NeonUIStyle : MonoBehaviour
    {
        // ── Colors ────────────────────────────────────────────────────────────

        private static readonly Color PanelBackground  = new Color(0.02f, 0.03f, 0.06f, 0.92f);
        private static readonly Color BorderColor       = new Color(0f,    0.85f, 1f,    1f);
        private static readonly Color TextPrimary       = new Color(0.8f,  0.97f, 1f,    1f);
        private static readonly Color TextSecondary     = new Color(0.5f,  0.75f, 0.9f,  1f);
        private static readonly Color TextDanger        = new Color(1f,    0.2f,  0.15f, 1f);
        private static readonly Color TextWarning       = new Color(1f,    0.85f, 0f,    1f);
        private static readonly Color ButtonNormal      = new Color(0.03f, 0.07f, 0.12f, 0.95f);
        private static readonly Color ButtonHighlight   = new Color(0f,    0.5f,  0.8f,  0.3f);
        private static readonly Color ButtonDisabled    = new Color(0.1f,  0.1f,  0.12f, 0.7f);
        private static readonly Color ButtonText        = new Color(0.75f, 0.97f, 1f,    1f);

        // ── Inspector ─────────────────────────────────────────────────────────

        [Tooltip("If true, scans child hierarchy on Start and applies style to all UI elements.")]
        [SerializeField] private bool applyOnStart = true;

        [Tooltip("Override the border color (defaults to cyan).")]
        [SerializeField] private Color borderColorOverride = new Color(0f, 0.85f, 1f, 1f);

        [Tooltip("Thickness of the neon border as a fraction of panel size (0 = no border).")]
        [SerializeField] [Range(0f, 0.05f)] private float borderThickness = 0.015f;

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Start()
        {
            if (applyOnStart)
                Apply();
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Applies neon style to all children immediately.</summary>
        public void Apply()
        {
            Color border = borderColorOverride;

            StyleImages(border);
            StyleTexts();
            StyleButtons(border);
        }

        // ── Private ───────────────────────────────────────────────────────────

        private void StyleImages(Color border)
        {
            Image[] images = GetComponentsInChildren<Image>(includeInactive: true);
            foreach (Image img in images)
            {
                if (img == null) continue;

                string nameLower = img.gameObject.name.ToLowerInvariant();

                // Border / outline images stay bright.
                if (nameLower.Contains("border") || nameLower.Contains("outline")
                    || nameLower.Contains("frame") || nameLower.Contains("line"))
                {
                    img.color = border;
                    continue;
                }

                // Filled bar (fuel, timer) — keep as-is but tint toward cyan.
                if (nameLower.Contains("fill"))
                {
                    img.color = new Color(0f, 0.85f, 1f, img.color.a);
                    continue;
                }

                // Background bar containers — very dark.
                if (nameLower.Contains("bg") || nameLower.Contains("background")
                    || nameLower.Contains("back"))
                {
                    img.color = new Color(0.02f, 0.03f, 0.07f, 0.7f);
                    continue;
                }

                // Panel roots and general backgrounds.
                if (img.GetComponent<RectTransform>() != null)
                    img.color = PanelBackground;
            }
        }

        private void StyleTexts()
        {
            TMP_Text[] texts = GetComponentsInChildren<TMP_Text>(includeInactive: true);
            foreach (TMP_Text t in texts)
            {
                if (t == null) continue;

                string nameLower = t.gameObject.name.ToLowerInvariant();

                if (nameLower.Contains("danger") || nameLower.Contains("risk")
                    || nameLower.Contains("critical") || nameLower.Contains("alert"))
                {
                    t.color = TextDanger;
                }
                else if (nameLower.Contains("warn") || nameLower.Contains("watch"))
                {
                    t.color = TextWarning;
                }
                else if (nameLower.Contains("title") || nameLower.Contains("header"))
                {
                    t.color = BorderColor;                     // Cyan title
                    t.fontStyle = FontStyles.Bold;
                }
                else if (nameLower.Contains("body") || nameLower.Contains("info")
                         || nameLower.Contains("detail"))
                {
                    t.color = TextSecondary;
                }
                else
                {
                    t.color = TextPrimary;
                }
            }
        }

        private void StyleButtons(Color border)
        {
            Button[] buttons = GetComponentsInChildren<Button>(includeInactive: true);
            foreach (Button btn in buttons)
            {
                if (btn == null) continue;

                // Background image.
                Image img = btn.GetComponent<Image>();
                if (img != null) img.color = ButtonNormal;

                // Color block.
                ColorBlock cb = btn.colors;
                cb.normalColor      = ButtonNormal;
                cb.highlightedColor = ButtonHighlight;
                cb.pressedColor     = new Color(0f, 0.65f, 0.9f, 0.5f);
                cb.disabledColor    = ButtonDisabled;
                cb.selectedColor    = ButtonHighlight;
                cb.colorMultiplier  = 1f;
                btn.colors = cb;

                // Text inside button.
                TMP_Text label = btn.GetComponentInChildren<TMP_Text>();
                if (label != null)
                    label.color = ButtonText;
            }
        }
    }
}
