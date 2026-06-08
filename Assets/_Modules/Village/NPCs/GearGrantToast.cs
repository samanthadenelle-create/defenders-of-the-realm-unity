// =============================================================================
// GearGrantToast — WO-364 "+<Armor> / +<Weapon>" HUD popup for the gear-up beat.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// A small, NON-BLOCKING top-centre toast popped when the wave-3 companion outfits
// the hero ("+Iron Plate Armor" / "+Iron Sword"). It auto-dismisses after a few
// seconds. Code-built uGUI on its own Screen-Space-Overlay Canvas — NOT UXML —
// because UXML UIDocuments come up EMPTY in WebGL player builds (PIPELINE_STATE
// landmine). Self-contained + WebGL-safe, mirroring EchoTutorialUI exactly.
//
// Non-blocking: the card never raycast-blocks the screen (combat/movement keep
// working underneath). Isolation/safety: lives in DeNelle.Village; every step is
// null-guarded; ASCII-only strings.
// =============================================================================

using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Village
{
    /// <summary>
    /// A self-contained, code-built uGUI top-centre "+gear" toast. Create one via
    /// <see cref="Show"/>; it fades/auto-dismisses after <see cref="LifeSeconds"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GearGrantToast : MonoBehaviour
    {
        private const float LifeSeconds = 4.0f;
        private const float FadeSeconds = 0.5f;

        // One on-screen at a time.
        private static GearGrantToast s_active;

        private CanvasGroup _group;
        private float _shownAt;

        /// <summary>
        /// Builds + shows the gear-grant toast. Idempotent: replaces any toast already
        /// on screen. <paramref name="armorLabel"/> is the headline; the optional
        /// <paramref name="weaponLabel"/> shows as a second "+" line when provided.
        /// </summary>
        public static void Show(string armorLabel, string weaponLabel = null)
        {
            if (s_active != null) { Object.Destroy(s_active.gameObject); s_active = null; }

            var go = new GameObject("GearGrantToast");
            var ui = go.AddComponent<GearGrantToast>();
            ui.Build(armorLabel, weaponLabel);
            s_active = ui;
        }

        private void Build(string armorLabel, string weaponLabel)
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 710;   // just above the Echo toast / HUD

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920f, 1080f);
            scaler.matchWidthOrHeight = 0.5f;

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 1f;
            _group.interactable = false;
            _group.blocksRaycasts = false;   // never swallow gameplay input

            // ── The card (top-centre) ───────────────────────────────────────────
            var card = new GameObject("Card", typeof(RectTransform), typeof(Image));
            card.transform.SetParent(transform, false);
            var crt = (RectTransform)card.transform;
            crt.anchorMin = new Vector2(0.5f, 1f);
            crt.anchorMax = new Vector2(0.5f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = new Vector2(0f, -120f);
            crt.sizeDelta = new Vector2(460f, 110f);

            var bg = card.GetComponent<Image>();
            bg.color = new Color(0.07f, 0.08f, 0.12f, 0.92f);
            bg.raycastTarget = false;

            // Gold top accent bar (matches the project's prompt styling).
            var accent = new GameObject("Accent", typeof(RectTransform), typeof(Image));
            accent.transform.SetParent(card.transform, false);
            var art = (RectTransform)accent.transform;
            art.anchorMin = new Vector2(0f, 1f);
            art.anchorMax = new Vector2(1f, 1f);
            art.pivot = new Vector2(0.5f, 1f);
            art.anchoredPosition = Vector2.zero;
            art.sizeDelta = new Vector2(0f, 6f);
            var ai = accent.GetComponent<Image>();
            ai.color = new Color(0.85f, 0.72f, 0.36f, 1f);
            ai.raycastTarget = false;

            // Text — uGUI Text (no TMP dependency; WebGL-safe).
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(card.transform, false);
            var lrt = (RectTransform)label.transform;
            lrt.anchorMin = Vector2.zero;
            lrt.anchorMax = Vector2.one;
            lrt.offsetMin = new Vector2(18f, 12f);
            lrt.offsetMax = new Vector2(-18f, -16f);

            var text = label.GetComponent<Text>();
            text.text = BuildText(armorLabel, weaponLabel);
            text.color = new Color(0.93f, 0.92f, 0.88f, 1f);
            text.fontSize = 24;
            text.alignment = TextAnchor.MiddleCenter;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.raycastTarget = false;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                       ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null) text.font = font;

            _shownAt = Time.unscaledTime;
        }

        private static string BuildText(string armorLabel, string weaponLabel)
        {
            string a = string.IsNullOrWhiteSpace(armorLabel) ? "Armor" : armorLabel.Trim();
            string line = "+" + a;
            if (!string.IsNullOrWhiteSpace(weaponLabel))
                line += "\n+" + weaponLabel.Trim();
            return line;
        }

        private void Update()
        {
            float elapsed = Time.unscaledTime - _shownAt;
            if (elapsed >= LifeSeconds) { Destroy(gameObject); return; }

            // Fade out over the final FadeSeconds.
            float fadeStart = LifeSeconds - FadeSeconds;
            if (_group != null && elapsed > fadeStart)
                _group.alpha = Mathf.Clamp01((LifeSeconds - elapsed) / FadeSeconds);
        }

        private void OnDestroy()
        {
            if (s_active == this) s_active = null;
        }
    }
}
