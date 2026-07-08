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
using DeNelle.Core.UI;
using DeNelle.Core.Diagnostics;

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
            FlowTrace.Step("CompanionGear", $"GearGrantToast.Show armor='{armorLabel ?? "<null>"}' weapon='{weaponLabel ?? "<null>"}'.");
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
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            _group = gameObject.AddComponent<CanvasGroup>();
            _group.alpha = 1f;
            _group.interactable = false;
            _group.blocksRaycasts = false;   // never swallow gameplay input

            // ── The card (top-centre) ───────────────────────────────────────────
            // WO-562: built from the ONE shared obsidian toast (black fill + gold top accent +
            // WebGL-safe Text), so this surface no longer hand-rolls its own bg/accent/label colours.
            var parts = ElarionUiKit.ToastCard(transform, ElarionUiKit.ToastTone.Gold,
                                               accentLeft: false, align: TextAnchor.MiddleCenter);
            var crt = (RectTransform)parts.card.transform;
            crt.anchorMin = new Vector2(0.5f, 1f);
            crt.anchorMax = new Vector2(0.5f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = new Vector2(0f, -120f);
            crt.sizeDelta = new Vector2(460f, 110f);
            if (parts.label != null) parts.label.text = BuildText(armorLabel, weaponLabel);
            else
                // Built-but-invisible split (§2.5): the toast card came up with no label — the "+gear"
                // text will never render even though the card exists. Warn so a capture names it.
                FlowTrace.Warn("CompanionGear", "GearGrantToast: ToastCard returned a null label — '+gear' text will not render.");

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
