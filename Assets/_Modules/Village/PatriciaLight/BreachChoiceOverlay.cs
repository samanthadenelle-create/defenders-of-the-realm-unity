// =============================================================================
// BreachChoiceOverlay — the breach-time "Last Stand vs Defend the Tower" prompt.
// -----------------------------------------------------------------------------
// WO-47 (Patricia Light), Phase 1 part A. When an enemy breaches the Heart the
// player no longer drops straight into the ATB battle — they CHOOSE:
//   ⚔ Last Stand    -> the existing 2D turn-based ATB battle (unchanged).
//   🏹 Defend the Tower -> Patricia Light, the real-time defend-the-tower shooter.
//
// HARD CONSTRAINT (WO-47): code-built UI ONLY. UXML-sourced UIDocuments come up
// EMPTY in this project's player builds (BattleHUD.uxml / BuildMenu.uxml both
// fail to clone). So this overlay is built entirely in C# with new VisualElement
// / Button / Label + inline styles, and hosts itself on a code-created UIDocument
// that BORROWS a PanelSettings from a live scene UIDocument — exactly the idiom
// in DeNelle.Settings.MusicToggleBootstrap.Install and BattleController's code HUD.
//
// Lives in DeNelle.Village (no new asmdef — WO-47 constraint 2).
// =============================================================================

using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Village
{
    /// <summary>
    /// A modal two-button breach choice (ATB Last Stand vs the Defend-the-Tower
    /// shooter), built entirely in code on a borrowed-PanelSettings UIDocument.
    /// Pausing is the caller's concern; this just presents the choice and invokes
    /// exactly one of the two callbacks, then tears itself down.
    /// </summary>
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class BreachChoiceOverlay : MonoBehaviour
    {
        private Action _onLastStand;
        private Action _onDefendTower;
        private bool _resolved;

        /// <summary>
        /// Builds + shows the breach choice. Returns true when the overlay was
        /// created (a PanelSettings host was found); false when it could not be —
        /// the caller then falls back to the ATB path so the breach is never a
        /// dead end. Exactly one of the two callbacks fires on the player's pick.
        /// </summary>
        /// <param name="heart">The Heart (unused for layout; kept for parity / future framing).</param>
        /// <param name="onLastStand">Invoked when the player picks ⚔ Last Stand (ATB).</param>
        /// <param name="onDefendTower">Invoked when the player picks 🏹 Defend the Tower.</param>
        public static bool Show(HeartController heart, Action onLastStand, Action onDefendTower)
        {
            // Borrow a PanelSettings from the top-most scene UIDocument so the
            // overlay renders at the scene's UI scale (a code-built UIDocument
            // with no PanelSettings renders nothing — commit 30ff18b).
            PanelSettings ps = BorrowPanelSettings(out float topSort);
            if (ps == null) return false;

            // Inactive-then-activate so the UIDocument builds its root with the
            // PanelSettings already assigned (mirrors MusicToggleBootstrap).
            var go = new GameObject("BreachChoiceOverlay");
            go.SetActive(false);
            var doc = go.AddComponent<UIDocument>();
            doc.panelSettings = ps;
            doc.sortingOrder = topSort + 100;   // above everything else in the scene

            var overlay = go.AddComponent<BreachChoiceOverlay>();
            overlay._onLastStand = onLastStand;
            overlay._onDefendTower = onDefendTower;
            go.SetActive(true);
            return true;
        }

        private static PanelSettings BorrowPanelSettings(out float topSort)
        {
            PanelSettings ps = null;
            topSort = float.MinValue;
            foreach (var d in UnityEngine.Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include))
            {
                if (d == null || d.panelSettings == null) continue;
                if (d.sortingOrder >= topSort) { topSort = d.sortingOrder; ps = d.panelSettings; }
            }
            return ps;
        }

        private void OnEnable()
        {
            var doc = GetComponent<UIDocument>();
            var root = doc != null ? doc.rootVisualElement : null;
            if (root == null) return;

            root.Clear();
            root.style.flexGrow = 1f;
            root.style.alignItems = Align.Center;
            root.style.justifyContent = Justify.Center;
            // Dim the village behind the modal so the choice reads as a pause.
            root.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.66f));

            // ── Panel ──────────────────────────────────────────────────────────
            var panel = new VisualElement();
            var p = panel.style;
            p.minWidth = 320f; p.maxWidth = 520f;
            p.paddingTop = 28f; p.paddingBottom = 28f; p.paddingLeft = 28f; p.paddingRight = 28f;
            p.alignItems = Align.Center;
            p.backgroundColor = new StyleColor(new Color(0.10f, 0.08f, 0.16f, 0.97f));
            p.borderTopLeftRadius = 14f; p.borderTopRightRadius = 14f;
            p.borderBottomLeftRadius = 14f; p.borderBottomRightRadius = 14f;
            p.borderTopWidth = 2f; p.borderBottomWidth = 2f; p.borderLeftWidth = 2f; p.borderRightWidth = 2f;
            var edge = new StyleColor(new Color(0.86f, 0.27f, 0.27f, 1f)); // breach red
            p.borderTopColor = edge; p.borderBottomColor = edge;
            p.borderLeftColor = edge; p.borderRightColor = edge;
            root.Add(panel);

            var title = new Label("The Heart is Breached");
            title.style.color = new StyleColor(new Color(0.97f, 0.92f, 0.74f, 1f));
            title.style.fontSize = 26f;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.unityTextAlign = TextAnchor.MiddleCenter;
            panel.Add(title);

            var sub = new Label("Hollow Ones have crossed the inner ring. How do you answer?");
            sub.style.color = new StyleColor(new Color(0.82f, 0.82f, 0.88f, 1f));
            sub.style.fontSize = 14f;
            sub.style.marginTop = 8f; sub.style.marginBottom = 22f;
            sub.style.whiteSpace = WhiteSpace.Normal;
            sub.style.unityTextAlign = TextAnchor.MiddleCenter;
            panel.Add(sub);

            panel.Add(BuildChoiceButton(
                "⚔ Last Stand",
                "Turn-based — face the breach in the ATB battle.",
                new Color(0.45f, 0.16f, 0.16f, 1f),
                Choose_LastStand));

            panel.Add(BuildChoiceButton(
                "🏹 Defend the Tower",
                "Real-time — hold the spire and loose arrows / spells.",
                new Color(0.18f, 0.34f, 0.52f, 1f),
                Choose_DefendTower));
        }

        private Button BuildChoiceButton(string text, string hint, Color bg, Action onClick)
        {
            var btn = new Button(() => onClick());
            var s = btn.style;
            s.width = 300f; s.height = 56f;
            s.marginTop = 8f; s.marginBottom = 8f;
            s.fontSize = 18f;
            s.unityFontStyleAndWeight = FontStyle.Bold;
            s.unityTextAlign = TextAnchor.MiddleCenter;
            s.color = new StyleColor(Color.white);
            s.backgroundColor = new StyleColor(bg);
            s.borderTopWidth = 0f; s.borderBottomWidth = 0f; s.borderLeftWidth = 0f; s.borderRightWidth = 0f;
            s.borderTopLeftRadius = 10f; s.borderTopRightRadius = 10f;
            s.borderBottomLeftRadius = 10f; s.borderBottomRightRadius = 10f;
            btn.text = text;
            btn.tooltip = hint;
            return btn;
        }

        private void Choose_LastStand() => Resolve(_onLastStand);
        private void Choose_DefendTower() => Resolve(_onDefendTower);

        private void Resolve(Action chosen)
        {
            if (_resolved) return;   // guard double-clicks before teardown
            _resolved = true;

            // Tear down the overlay BEFORE invoking so the dimmer / buttons don't
            // linger over the mode that the callback starts.
            Destroy(gameObject);

            try { chosen?.Invoke(); }
            catch (Exception ex) { Debug.LogError($"[BreachChoiceOverlay] choice handler threw: {ex}"); }
        }
    }
}
