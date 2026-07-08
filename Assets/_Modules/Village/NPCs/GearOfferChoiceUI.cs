// =============================================================================
// GearOfferChoiceUI — WO-364 two-button "outfit me?" choice for the gear-up beat.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// After the wave-3 companion offers gear, the player picks one of two options:
//   [ Visit the forge ]   — flavour: "we head to the forge"
//   [ Already equipped ]  — flavour: "outfit me right here"
// BOTH choices auto-equip IN PLACE (see CompanionGearSetup). The forge-visit is
// kept SIMPLE: there is NO walk-to-forge pathing cutscene (fragile, out of scope)
// — a real forge walk is flagged as a polish follow-up. The choice only flavours
// the companion's follow-up line.
//
// Code-built uGUI (NOT UXML — UXML UIDocuments come up empty in WebGL builds), on
// its own Screen-Space-Overlay Canvas, pointer/touch hit-tested manually (the
// proven CampPromptUI / GameOverScreen pattern — no EventSystem dependency). A
// short min-hold stops the tap that opened the offer from instantly choosing.
// Isolation/safety: lives in DeNelle.Village; null-guarded; ASCII-only strings.
// =============================================================================

using System;
using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Village
{
    /// <summary>
    /// A self-contained two-button choice prompt for the gear-up offer. Create via
    /// <see cref="Show"/> with a callback; it raises the callback with
    /// <c>true</c> for "visit the forge" or <c>false</c> for "already equipped",
    /// then destroys itself. Exactly one is shown at a time.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GearOfferChoiceUI : MonoBehaviour
    {
        // Don't let the tap that opened the offer instantly pick a button.
        private const float MinHold = 0.4f;
        // Failsafe: auto-choose "already equipped" (in-place) if input is lost.
        private const float AutoChooseSeconds = 12f;

        private static GearOfferChoiceUI s_active;

        private Action<bool> _onChosen;
        private RectTransform _forgeRect;
        private RectTransform _hereRect;
        private float _shownAt;
        private bool _done;

        /// <summary>
        /// Builds + shows the two-button gear offer. Idempotent (replaces any open
        /// offer). <paramref name="onChosen"/> receives true = visit forge,
        /// false = equip in place; it is always raised exactly once.
        /// </summary>
        public static void Show(Action<bool> onChosen)
        {
            FlowTrace.Step("CompanionGear", "GearOfferChoiceUI.Show — two-button gear offer up (await player pick).");
            if (s_active != null) { Destroy(s_active.gameObject); s_active = null; }
            var go = new GameObject("GearOfferChoiceUI");
            var ui = go.AddComponent<GearOfferChoiceUI>();
            ui._onChosen = onChosen;
            ui.Build();
            s_active = ui;
        }

        private void Build()
        {
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 720;   // above HUD + toasts

            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 0.5f;

            // Panel (bottom-centre, above the speech bubble area).
            var panel = new GameObject("Panel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(transform, false);
            var prt = (RectTransform)panel.transform;
            prt.anchorMin = new Vector2(0.5f, 0f);
            prt.anchorMax = new Vector2(0.5f, 0f);
            prt.pivot = new Vector2(0.5f, 0f);
            prt.anchoredPosition = new Vector2(0f, 150f);
            prt.sizeDelta = new Vector2(720f, 96f);
            panel.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.09f, 0.0f); // invisible spacer

            _forgeRect = MakeButton(panel.transform, "Visit the forge",
                new Vector2(0f, 0f), new Vector2(0.5f, 1f), new Vector2(8f, 0f), new Vector2(-8f, 0f),
                new Color(0.20f, 0.28f, 0.40f, 0.95f));
            _hereRect = MakeButton(panel.transform, "I'm already equipped",
                new Vector2(0.5f, 0f), new Vector2(1f, 1f), new Vector2(8f, 0f), new Vector2(-8f, 0f),
                new Color(0.18f, 0.30f, 0.18f, 0.95f));

            _shownAt = Time.unscaledTime;
        }

        private static RectTransform MakeButton(Transform parent, string label,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 offMin, Vector2 offMax, Color color)
        {
            var go = new GameObject("Btn_" + label, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform;
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = offMin;
            rt.offsetMax = offMax;
            go.GetComponent<Image>().color = color;

            var lblGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
            lblGo.transform.SetParent(go.transform, false);
            var lrt = (RectTransform)lblGo.transform;
            lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one;
            lrt.offsetMin = Vector2.zero; lrt.offsetMax = Vector2.zero;
            var text = lblGo.GetComponent<Text>();
            text.text = label;
            text.color = new Color(0.95f, 0.94f, 0.90f, 1f);
            text.fontSize = 26;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                       ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
            if (font != null) text.font = font;
            return rt;
        }

        private void Update()
        {
            if (_done) return;
            float elapsed = Time.unscaledTime - _shownAt;

            if (elapsed >= AutoChooseSeconds)
            {
                // Fallback/default (§2.3): input lost — auto-pick "already equipped" so the beat
                // can never wedge waiting on a tap. Warn so a capture shows the failsafe fired.
                FlowTrace.Warn("CompanionGear", $"GearOfferChoiceUI auto-chose (in-place) after {AutoChooseSeconds:F0}s — no player tap.");
                Choose(false);
                return;
            }
            if (elapsed < MinHold) return;

            if (TryGetTap(out Vector2 tap))
            {
                if (_forgeRect != null && RectTransformUtility.RectangleContainsScreenPoint(_forgeRect, tap, null))
                    Choose(true);
                else if (_hereRect != null && RectTransformUtility.RectangleContainsScreenPoint(_hereRect, tap, null))
                    Choose(false);
            }
        }

        private void Choose(bool visitForge)
        {
            if (_done) return;
            _done = true;
            FlowTrace.Step("CompanionGear", $"GearOfferChoiceUI.Choose -> {(visitForge ? "visit-forge" : "in-place")} (both auto-equip in place).");
            var cb = _onChosen;
            _onChosen = null;
            if (s_active == this) s_active = null;
            Destroy(gameObject);
            // The callback drives the gear-grant beat across into ElaraWaveThreeJoin — a throw here
            // would strand the join beat's WaitUntil(chosen). No silent swallow (§12): Fail rolls it up.
            try { cb?.Invoke(visitForge); }
            catch (Exception ex)
            {
                FlowTrace.Fail("CompanionGear", $"GearOfferChoiceUI choice callback threw: {ex.GetType().Name}: {ex.Message}");
                Debug.LogWarning($"[GearOfferChoiceUI] {ex.Message}");
            }
        }

        private void OnDestroy()
        {
            if (s_active == this) s_active = null;
        }

        /// <summary>First-touch / mouse-down screen position this frame (no EventSystem).</summary>
        private static bool TryGetTap(out Vector2 pos)
        {
            if (UnityEngine.Input.touchCount > 0)
            {
                var t = UnityEngine.Input.GetTouch(0);
                if (t.phase == UnityEngine.TouchPhase.Began) { pos = t.position; return true; }
            }
            if (UnityEngine.Input.GetMouseButtonDown(0)) { pos = (Vector2)UnityEngine.Input.mousePosition; return true; }
            pos = default;
            return false;
        }
    }
}
