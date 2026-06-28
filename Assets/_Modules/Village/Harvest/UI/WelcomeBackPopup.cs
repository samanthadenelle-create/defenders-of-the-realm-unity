// =============================================================================
// WelcomeBackPopup — the "your realm gathered while you slept" summary (WO-115 §2).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.UI
//
// CODE-BUILT UI Toolkit — NOT UXML. PIPELINE_STATE.md §8 hard rule: UXML-sourced
// UIDocuments render empty in this project's player builds. We build the
// VisualElement tree in code and host it on a UIDocument whose PanelSettings is
// BORROWED from the top-most live UIDocument in the scene (a code UIDocument with
// no PanelSettings renders NOTHING — the documented empty-UI trap). This mirrors
// LevelUpSkillPopupBootstrap's self-install exactly.
//
// REVEAL, NOT TRANSACTION: the grant already happened in OfflineHarvestService
// before this is shown. Collect just dismisses. A player who closes the app
// without tapping Collect keeps the haul — it was banked + persisted on claim.
// =============================================================================
using UnityEngine;
using UnityEngine.UIElements;
using DeNelle.Village;

namespace DeNelle.Village.UI
{
    /// <summary>
    /// One-tap welcome-back summary of the offline haul. Self-installs a code-built
    /// UI Toolkit panel on a borrowed PanelSettings; <see cref="Show"/> is the only
    /// entry point (called by OfflineHarvestService after a non-zero claim).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public sealed class WelcomeBackPopup : MonoBehaviour
    {
        private static bool s_open;   // at most one welcome-back panel at a time

        private OfflineHarvestResult _result;

        /// <summary>
        /// Reveal the welcome-back summary for <paramref name="result"/>. No-op when
        /// the haul is empty or a panel is already open. Borrows a PanelSettings from
        /// the scene's top-most UIDocument; if none exists (a scene with no UI to host
        /// it), the reveal is skipped — the haul is already banked regardless.
        /// </summary>
        public static void Show(OfflineHarvestResult result)
        {
            if (result == null || result.Total <= 0) return;
            if (s_open) return;

            PanelSettings ps = null;
            float topSort = float.MinValue;
            foreach (var doc in Object.FindObjectsByType<UIDocument>(FindObjectsInactive.Include))
            {
                if (doc == null || doc.panelSettings == null) continue;
                if (doc.sortingOrder >= topSort) { topSort = doc.sortingOrder; ps = doc.panelSettings; }
            }
            if (ps == null) return;   // no UI in this scene to host the reveal

            var go = new GameObject("WelcomeBackPopup");
            go.SetActive(false);
            var doc2 = go.AddComponent<UIDocument>();
            doc2.panelSettings = ps;
            doc2.sortingOrder = topSort + 70;   // above the scene UI (and the level-up popup)
            var popup = go.AddComponent<WelcomeBackPopup>();
            popup._result = result;
            go.SetActive(true);   // OnEnable builds the tree with PanelSettings already set
        }

        private void OnEnable()
        {
            s_open = true;
            BuildUi();
        }

        private void OnDisable()
        {
            s_open = false;
        }

        private void Dismiss()
        {
            Destroy(gameObject);
        }

        // --- Code-built panel ----------------------------------------------------

        private void BuildUi()
        {
            var doc = GetComponent<UIDocument>();
            var root = doc != null ? doc.rootVisualElement : null;
            if (root == null || _result == null) return;

            var overlay = new VisualElement { name = "welcomeback-overlay" };
            overlay.pickingMode = PickingMode.Ignore;   // children still pick; backdrop doesn't steal input
            var os = overlay.style;
            os.position = Position.Absolute;
            os.top = 0f; os.left = 0f; os.right = 0f; os.bottom = 0f;
            os.alignItems = Align.Center;
            os.justifyContent = Justify.Center;

            var card = new VisualElement { name = "welcomeback-card" };
            var cs = card.style;
            cs.minWidth = 320f;
            cs.maxWidth = 420f;
            cs.paddingTop = 20f; cs.paddingBottom = 20f; cs.paddingLeft = 24f; cs.paddingRight = 24f;
            // WO-562 obsidian canon: near-black card + a 2px gold trim (was a hand-rolled blue-grey fill).
            cs.backgroundColor = new Color(0.02f, 0.02f, 0.025f, 0.98f);
            cs.borderTopLeftRadius = 14f; cs.borderTopRightRadius = 14f;
            cs.borderBottomLeftRadius = 14f; cs.borderBottomRightRadius = 14f;
            var goldTrim = new Color(0.831f, 0.686f, 0.216f, 1f);
            cs.borderTopWidth = 2f; cs.borderBottomWidth = 2f; cs.borderLeftWidth = 2f; cs.borderRightWidth = 2f;
            cs.borderTopColor = goldTrim; cs.borderBottomColor = goldTrim;
            cs.borderLeftColor = goldTrim; cs.borderRightColor = goldTrim;

            card.Add(MakeLabel("Welcome back, Keeper.", 22f, FontStyle.Bold, new Color(0.933f, 0.784f, 0.282f)));

            var awayLine = MakeLabel(AwayText(), 14f, FontStyle.Normal, new Color(0.78f, 0.82f, 0.9f));
            awayLine.style.marginTop = 4f;
            awayLine.style.marginBottom = 12f;
            card.Add(awayLine);

            AddRowIf(card, _result.AetherCrystals, "Aether Crystals", new Color(0.55f, 0.8f, 1f));
            AddRowIf(card, _result.Food, "Food", new Color(0.85f, 0.78f, 0.5f));
            AddRowIf(card, _result.Iron, "Iron", new Color(0.85f, 0.7f, 0.6f));
            AddRowIf(card, _result.Wood, "Wood", new Color(0.7f, 0.85f, 0.55f));

            if (_result.WasCapped)
            {
                var nudge = MakeLabel(
                    "Your mines filled up — keep them defended and check in sooner to catch every shard.",
                    12f, FontStyle.Italic, new Color(0.95f, 0.78f, 0.45f));
                nudge.style.whiteSpace = WhiteSpace.Normal;
                nudge.style.marginTop = 12f;
                card.Add(nudge);
            }

            var collect = new Button(Dismiss) { text = "Collect" };
            var bs = collect.style;
            bs.height = 42f;
            bs.marginTop = 16f;
            bs.fontSize = 16f;
            bs.unityFontStyleAndWeight = FontStyle.Bold;
            bs.unityTextAlign = TextAnchor.MiddleCenter;
            bs.color = Color.white;
            bs.backgroundColor = new Color(0.18f, 0.5f, 0.32f, 0.98f);
            bs.borderTopLeftRadius = 9f; bs.borderTopRightRadius = 9f;
            bs.borderBottomLeftRadius = 9f; bs.borderBottomRightRadius = 9f;
            card.Add(collect);

            overlay.Add(card);
            root.Add(overlay);
        }

        private string AwayText()
        {
            double hours = _result.AwaySeconds / 3600.0;
            string span = hours >= 1.0
                ? $"{hours:0.#}h"
                : $"{Mathf.RoundToInt((float)(_result.AwaySeconds / 60.0))}m";
            return _result.WasCapped
                ? $"Your realm gathered for {span} (capped)."
                : $"Your realm gathered for {span}.";
        }

        private void AddRowIf(VisualElement card, int amount, string label, Color accent)
        {
            if (amount <= 0) return;

            var row = new VisualElement();
            var rs = row.style;
            rs.flexDirection = FlexDirection.Row;
            rs.justifyContent = Justify.SpaceBetween;
            rs.marginTop = 4f; rs.marginBottom = 4f;

            var name = MakeLabel(label, 15f, FontStyle.Normal, new Color(0.85f, 0.88f, 0.92f));
            name.style.unityTextAlign = TextAnchor.MiddleLeft;

            var amt = MakeLabel($"+{amount}", 16f, FontStyle.Bold, accent);
            amt.style.unityTextAlign = TextAnchor.MiddleRight;

            row.Add(name);
            row.Add(amt);
            card.Add(row);
        }

        private Label MakeLabel(string text, float size, FontStyle weight, Color color)
        {
            var l = new Label(text);
            var s = l.style;
            s.color = color;
            s.fontSize = size;
            s.unityFontStyleAndWeight = weight;
            s.unityTextAlign = TextAnchor.MiddleCenter;
            return l;
        }
    }
}
