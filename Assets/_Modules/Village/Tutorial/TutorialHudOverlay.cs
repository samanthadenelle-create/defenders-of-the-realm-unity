// =============================================================================
// TutorialHudOverlay — the FTUE's on-screen OBJECTIVE banner, HINT line, and
// UI HIGHLIGHT, driven by the Yarn command bridge (DialogueCommandBridge).
// -----------------------------------------------------------------------------
// CompanionMeeting.yarn calls:
//   <<set_hud_objective "Build towers at the remaining 3 gates" 0 3>>
//   <<set_hud_hint "Explore beyond the gates to find resources">>
//   <<highlight_ui build_button>> / <<unhighlight_ui build_button>>
//
// This is a SELF-CONTAINED, code-built UI Toolkit overlay (the project rule:
// UXML doesn't render in builds — PIPELINE_STATE §8) on a runtime UIDocument
// whose PanelSettings is BORROWED from an existing scene UIDocument (the same
// trick PetIntroduction / JupiterSwap use). Tutorial-only chrome lives here, NOT
// in the core IVillageHud contract.
//
// HIGHLIGHT reaches into the live HUD UIDocument (the VillageHudController's
// panel) to pulse a named element ("build_button" → "build-button"). If the
// element/panel isn't found it degrades to a no-op — never throws.
//
// Isolation/safety: DeNelle.Village, code-built, every cross-call null-guarded.
// No PanelSettings ⇒ overlay simply doesn't show (commands become no-ops).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Village
{
    /// <summary>
    /// Code-built objective/hint banner + UI highlighter for the Yarn FTUE.
    /// Created + owned by <see cref="DialogueCommandBridge"/>; all entry points
    /// are null-safe so a missing panel never wedges the dialogue.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TutorialHudOverlay : MonoBehaviour
    {
        // Elarion palette (mirrors VillageHudController so the banner is cohesive).
        private static readonly Color CardBg  = new Color(0.10f, 0.08f, 0.16f, 0.94f);
        private static readonly Color CardRim = new Color(1f,    0.86f, 0.45f, 0.92f);
        private static readonly Color TitleTx = new Color(0.96f, 0.90f, 0.66f, 1f);
        private static readonly Color BodyTx  = new Color(0.90f, 0.93f, 0.98f, 1f);

        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _objective;
        private Label _objectiveText;
        private Label _objectiveProgress;
        private Label _hint;

        // Restorable styles for highlighted HUD elements, so unhighlight is exact.
        private readonly Dictionary<VisualElement, HighlightSnapshot> _highlighted =
            new Dictionary<VisualElement, HighlightSnapshot>();

        private struct HighlightSnapshot
        {
            public StyleColor BorderColor;
            public StyleFloat BorderWidth;
            public StyleScale Scale;
        }

        // ── Objective banner ─────────────────────────────────────────────────

        /// <summary>Show/update the top-centre objective banner ("title" + "cur/max").</summary>
        public void SetObjective(string title, int current, int max)
        {
            if (!EnsureRoot()) return;
            EnsureObjective();

            _objectiveText.text = string.IsNullOrEmpty(title) ? "" : title;
            _objectiveProgress.text = max > 0 ? $"{Mathf.Clamp(current, 0, max)} / {max}" : "";
            _objectiveProgress.style.display = max > 0 ? DisplayStyle.Flex : DisplayStyle.None;
            _objective.style.display = DisplayStyle.Flex;
        }

        /// <summary>Hide the objective banner.</summary>
        public void HideObjective()
        {
            if (_objective != null) _objective.style.display = DisplayStyle.None;
        }

        // ── Hint line ────────────────────────────────────────────────────────

        /// <summary>Show/update the hint line (just under the objective banner).</summary>
        public void SetHint(string text)
        {
            if (!EnsureRoot()) return;
            EnsureHint();
            _hint.text = string.IsNullOrEmpty(text) ? "" : text;
            _hint.style.display = string.IsNullOrEmpty(text) ? DisplayStyle.None : DisplayStyle.Flex;
        }

        /// <summary>Hide the hint line.</summary>
        public void HideHint()
        {
            if (_hint != null) _hint.style.display = DisplayStyle.None;
        }

        // ── UI highlight (reaches into the live HUD panel) ───────────────────

        /// <summary>Pulse-highlight a named HUD element ("build_button" → "build-button").</summary>
        public void Highlight(string elementName, bool on)
        {
            var el = FindHudElement(elementName);
            if (el == null) return;

            if (on)
            {
                if (_highlighted.ContainsKey(el)) return;
                _highlighted[el] = new HighlightSnapshot
                {
                    BorderColor = el.style.borderTopColor,
                    BorderWidth = el.style.borderTopWidth,
                    Scale       = el.style.scale,
                };
                ApplyBorder(el, CardRim, 3f);
                el.style.scale = new Scale(new Vector3(1.06f, 1.06f, 1f));
            }
            else
            {
                if (!_highlighted.TryGetValue(el, out var snap)) return;
                el.style.borderTopColor = snap.BorderColor;
                el.style.borderBottomColor = snap.BorderColor;
                el.style.borderLeftColor = snap.BorderColor;
                el.style.borderRightColor = snap.BorderColor;
                el.style.borderTopWidth = snap.BorderWidth;
                el.style.borderBottomWidth = snap.BorderWidth;
                el.style.borderLeftWidth = snap.BorderWidth;
                el.style.borderRightWidth = snap.BorderWidth;
                el.style.scale = snap.Scale;
                _highlighted.Remove(el);
            }
        }

        // ── Build / lazy chrome ──────────────────────────────────────────────

        private bool EnsureRoot()
        {
            if (_root != null) return true;

            PanelSettings panel = FindPanelSettings();
            if (panel == null)
            {
                Debug.LogWarning("[TutorialHudOverlay] No PanelSettings in scene — " +
                                 "objective/hint commands become no-ops this session.");
                return false;
            }

            _doc = gameObject.GetComponent<UIDocument>();
            if (_doc == null) _doc = gameObject.AddComponent<UIDocument>();
            _doc.panelSettings = panel;
            _doc.sortingOrder = 240f; // above HUD, below the pet prompt (260)

            _root = _doc.rootVisualElement;
            if (_root == null) return false;
            _root.pickingMode = PickingMode.Ignore;
            _root.style.flexGrow = 1;
            return true;
        }

        private void EnsureObjective()
        {
            if (_objective != null) return;

            _objective = new VisualElement { name = "tutorial-objective" };
            _objective.pickingMode = PickingMode.Ignore;
            var s = _objective.style;
            s.position = Position.Absolute;
            s.top = 74f;
            s.left = Length.Percent(50f);
            s.translate = new Translate(Length.Percent(-50f), 0f, 0f);
            s.flexDirection = FlexDirection.Row;
            s.alignItems = Align.Center;
            s.paddingLeft = 18; s.paddingRight = 18; s.paddingTop = 8; s.paddingBottom = 8;
            s.backgroundColor = CardBg;
            ApplyBorder(_objective, CardRim, 2f);
            SetRadius(_objective, 12);

            var label = new Label("OBJECTIVE");
            label.style.fontSize = 11;
            label.style.unityFontStyleAndWeight = FontStyle.Bold;
            label.style.color = CardRim;
            label.style.marginRight = 12;
            _objective.Add(label);

            _objectiveText = new Label("");
            _objectiveText.style.fontSize = 15;
            _objectiveText.style.color = TitleTx;
            _objectiveText.style.whiteSpace = WhiteSpace.Normal;
            _objective.Add(_objectiveText);

            _objectiveProgress = new Label("");
            _objectiveProgress.style.fontSize = 15;
            _objectiveProgress.style.unityFontStyleAndWeight = FontStyle.Bold;
            _objectiveProgress.style.color = CardRim;
            _objectiveProgress.style.marginLeft = 12;
            _objective.Add(_objectiveProgress);

            _root.Add(_objective);
        }

        private void EnsureHint()
        {
            if (_hint != null) return;

            _hint = new Label("") { name = "tutorial-hint" };
            _hint.pickingMode = PickingMode.Ignore;
            var s = _hint.style;
            s.position = Position.Absolute;
            s.top = 116f;
            s.left = Length.Percent(50f);
            s.translate = new Translate(Length.Percent(-50f), 0f, 0f);
            s.fontSize = 13;
            s.color = BodyTx;
            s.unityFontStyleAndWeight = FontStyle.Italic;
            s.whiteSpace = WhiteSpace.Normal;
            s.maxWidth = 520;
            s.unityTextAlign = TextAnchor.MiddleCenter;
            s.display = DisplayStyle.None;
            _root.Add(_hint);
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        // Resolve a Yarn element id ("build_button") to a live HUD VisualElement.
        // HUD element names use hyphens ("build-button"); query across all panels.
        private static VisualElement FindHudElement(string yarnName)
        {
            if (string.IsNullOrEmpty(yarnName)) return null;
            string hudName = yarnName.Replace('_', '-');

            var docs = UnityEngine.Object.FindObjectsByType<UIDocument>(
                FindObjectsInactive.Include);
            foreach (var d in docs)
            {
                if (d == null || d.rootVisualElement == null) continue;
                var hit = d.rootVisualElement.Q<VisualElement>(hudName);
                if (hit != null) return hit;
            }
            return null;
        }

        private static PanelSettings FindPanelSettings()
        {
            var docs = UnityEngine.Object.FindObjectsByType<UIDocument>(
                FindObjectsInactive.Include);
            foreach (var d in docs)
                if (d != null && d.panelSettings != null) return d.panelSettings;
            return null;
        }

        private static void ApplyBorder(VisualElement e, Color c, float w)
        {
            e.style.borderTopColor = c; e.style.borderBottomColor = c;
            e.style.borderLeftColor = c; e.style.borderRightColor = c;
            e.style.borderTopWidth = w; e.style.borderBottomWidth = w;
            e.style.borderLeftWidth = w; e.style.borderRightWidth = w;
        }

        private static void SetRadius(VisualElement e, float r)
        {
            e.style.borderTopLeftRadius = r; e.style.borderTopRightRadius = r;
            e.style.borderBottomLeftRadius = r; e.style.borderBottomRightRadius = r;
        }

        private void OnDestroy()
        {
            // Restore any elements we were highlighting on another (persistent) panel.
            foreach (var kv in _highlighted)
            {
                var el = kv.Key;
                if (el == null) continue;
                el.style.scale = kv.Value.Scale;
            }
            _highlighted.Clear();
        }
    }
}
