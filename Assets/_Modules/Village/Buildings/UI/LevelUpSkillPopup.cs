// =============================================================================
// LevelUpSkillPopup — DEF-77 (Linear). A panel that appears on hero level-up and
// lets the player spend the banked skill point on a craft skill immediately.
// -----------------------------------------------------------------------------
// namespace DeNelle.Village.UI. DEF-77 Correction Pass 1 + clarification applied:
//   • Subscribes in OnEnable / unsubscribes in OnDisable to HeroProgression.OnLevelUp
//     (→ Show) and SkillSystem.OnSkillsChanged (→ UpdateUI). NO FindObjectOfType
//     anywhere (CP1 Issue 6) — both are reached via their static Instance.
//   • Spending goes through SkillSystem.SpendPoint(type); the panel never mutates
//     skill fields directly.
//
// NOTE — HeroProgression.OnLevelUp is `Action<int>` in this codebase (it carries
// the new level for VFX/sound), so Show takes an int. The spec's parameterless
// `Show` is honoured in spirit; the level is just used for the title text.
//
// CODEBASE RECONCILIATION: built as CODE UI Toolkit (like TowerUpgradeButton),
// because UXML-sourced UIDocuments render empty in this project's player builds.
// Attach this to a GameObject carrying a UIDocument whose PanelSettings is assigned
// (same deployment model as the project's other code HUDs) — the HUD/scene wiring
// that attaches it is the AM integration step; the logic here is complete.
// =============================================================================

using UnityEngine;
using UnityEngine.UIElements;
using DeNelle.Core.Data;
using DeNelle.Core.Progression;
using DeNelle.Core.UI;

namespace DeNelle.Village.UI
{
    /// <summary>Level-up skill-point spend panel, driven by progression events.</summary>
    [RequireComponent(typeof(UIDocument))]
    public class LevelUpSkillPopup : MonoBehaviour
    {
        private VisualElement _overlay;   // full-screen layer (anchors the corner UI)
        private VisualElement _card;      // the expanded spend panel
        private Button _pill;             // collapsed persistent "N points — Spend" indicator
        private Label _title;
        private Label _points;
        private Button _blacksmith;
        private Button _woodworking;
        private Button _arcane;
        private Button _gathering;

        private int _lastLevel = 1;       // remembered for the pill → re-open path

        // DEF-261 — a level-up that lands during the Defend-the-Tower fight is QUEUED
        // (not silently dropped) so the spend screen surfaces the moment the fight ends.
        private int _pendingLevel = -1;

        // PanelManager mutual-exclusion handle (one panel at a time).
        private PanelHandle _panelHandle;

        private void OnEnable()
        {
            BuildUiIfNeeded();
            // Register with the modal arbiter so the spend card closes any other panel
            // (and vice-versa). Probe = the overlay's own visibility.
            if (_panelHandle == null)
                _panelHandle = PanelManager.Register("Level-Up Skill",
                    Hide,
                    () => _overlay != null && _overlay.style.display.value == DisplayStyle.Flex);
            Hide();

            // DEF-261 ROOT-CAUSE FIX: subscribe to the STATIC relay, not the instance
            // event. ProgressionManager destroys the BeforeSceneLoad standalone
            // HeroProgression and migrates XP onto the hero's own HeroProgression — an
            // instance-event subscription would dangle on the destroyed bootstrap and
            // never hear the hero's real level-ups. The static relay survives the swap.
            HeroProgression.OnAnyLevelUp += Show;
            if (SkillSystem.Instance != null)
                SkillSystem.Instance.OnSkillsChanged += UpdateUI;

            UpdateUI();

            // If a point was already banked before this popup installed (the first-
            // level-up gift, or a level-up that fired before the popup existed in this
            // scene), surface it now so banked points are never stranded with no way
            // to spend them. Show() itself defers if a fight is in progress.
            if (SkillSystem.Instance != null && SkillSystem.Instance.AvailablePoints > 0)
                Show(HeroProgression.Instance != null ? HeroProgression.Instance.Level : 1);
        }

        private void OnDisable()
        {
            HeroProgression.OnAnyLevelUp -= Show;
            if (SkillSystem.Instance != null)
                SkillSystem.Instance.OnSkillsChanged -= UpdateUI;
        }

        // DEF-261 — drain a level-up that arrived mid-fight once the fight is over.
        private void Update()
        {
            if (_pendingLevel < 0) return;
            int lvl = _pendingLevel;
            _pendingLevel = -1;
            Show(lvl);
        }

        // RETIRED (owner 2026-06-24): level-up popup not used - Wisdom shown via flashing skill-tree icon instead.
        // The popup/modal that asks the player to allocate points is disabled; the nice level-up
        // VFX/gold flash (LevelUpVFXController) and the floating "Level Up!" label (ProgressionManager
        // /FloatingXpText) stay. Show() is hard-gated to a no-op so nothing ever pops up to allocate;
        // the unspent-Wisdom announcement + skill-tree entry point now lives on the HUD's pulsing
        // skill-tree badge (VillageHudController). Code kept (not deleted) for an easy revert.
        private const bool PopupRetired = true;

        /// <summary>Reveal the popup (arg = the level just reached — title text only).</summary>
        private void Show(int newLevel)
        {
            if (PopupRetired) return;   // RETIRED — no allocate-popup; Wisdom shown via flashing skill-tree icon.
            if (_overlay == null) return;
            // DEF-266 — Level 1 is the baseline (account creation), not an achievement.
            // The DEF-82 starting skill-point gift banks points at level 1, which would
            // otherwise trip both the OnAnyLevelUp path and the OnEnable banked-points
            // fallback into auto-opening a "Level 1!" popup the instant a hero is picked.
            // Suppress the AUTO-popup below level 2 — the points are still granted and
            // remain spendable via the skill panel; alerts just start at genuine
            // level-UPs (2+). Guards on the level REACHED (not points-available).
            if (newLevel < 2) return;
            _lastLevel = newLevel;
            if (_title != null) _title.text = $"Level {newLevel}!  Spend a skill point";
            ShowCard();
            UpdateUI();
        }

        // Expanded spend panel visible, pill hidden.
        private void ShowCard()
        {
            if (_overlay == null) return;
            _overlay.style.display = DisplayStyle.Flex;
            if (_card != null) _card.style.display = DisplayStyle.Flex;
            if (_pill != null) _pill.style.display = DisplayStyle.None;
            // Announce open: closes any previously-open panel. Battle-lock may reject
            // (NotifyOpened==false) — revert and stay hidden, never force-show.
            if (!PanelManager.NotifyOpened(_panelHandle)) { Hide(); return; }
        }

        // Close/collapse: if points remain, fall back to the persistent pill so the
        // spend screen is ALWAYS findable again (DEF — owner: "once it loses focus
        // you can't find it again"). With no points left, hide everything.
        private void Collapse()
        {
            if (PopupRetired) { Hide(); return; }   // RETIRED — never surface the persistent spend pill.
            var sys = SkillSystem.Instance;
            if (sys != null && sys.AvailablePoints > 0)
            {
                if (_overlay != null) _overlay.style.display = DisplayStyle.Flex;
                if (_card != null) _card.style.display = DisplayStyle.None;
                if (_pill != null) _pill.style.display = DisplayStyle.Flex;
                UpdateUI();
            }
            else
            {
                Hide();
            }
        }

        // DEF/WO-333 — internal so the death/game-over screen can force-close any open
        // skill popup that would otherwise sit BEHIND the game-over overlay.
        internal void Hide()
        {
            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
            PanelManager.NotifyClosed(_panelHandle);
        }

        private void UpdateUI()
        {
            var sys = SkillSystem.Instance;
            if (_overlay == null || sys == null) return;
            if (PopupRetired) { Hide(); return; }   // RETIRED — keep the popup/pill hidden regardless of points.

            int pts = sys.AvailablePoints;
            if (_points != null) _points.text = $"Available points: {pts}";
            if (_pill != null) _pill.text = pts == 1 ? "1 skill point — Spend" : $"{pts} skill points — Spend";

            SetSkillButton(_blacksmith,  "Blacksmith",  SkillType.Blacksmith,     pts);
            SetSkillButton(_woodworking, "Woodworking", SkillType.Woodworking,    pts);
            SetSkillButton(_arcane,      "Arcane",      SkillType.Arcane,         pts);
            SetSkillButton(_gathering,   "Gathering",   SkillType.GatheringSpeed, pts);

            // Keep the persistent indicator honest: if points exist but nothing is on
            // screen, surface the pill so they can never be stranded; if they hit zero,
            // clear everything.
            if (pts <= 0)
            {
                Hide();
            }
            else if (_overlay != null && _overlay.style.display == DisplayStyle.None
                     && (HeroProgression.Instance == null || HeroProgression.Instance.Level >= 2))
            {
                Collapse();
            }
        }

        private void SetSkillButton(Button b, string label, SkillType type, int pts)
        {
            if (b == null) return;
            int lvl = SkillSystem.Instance != null ? SkillSystem.Instance.GetSkillLevel(type) : 0;
            b.text = $"{label}  (Lv {lvl})   +";
            b.SetEnabled(pts > 0);
        }

        private void Spend(SkillType type)
        {
            if (SkillSystem.Instance == null) return;
            SkillSystem.Instance.SpendPoint(type);   // fires OnSkillsChanged → UpdateUI
            if (SkillSystem.Instance.AvailablePoints <= 0) Hide();
        }

        // --- Code-built panel (UI Toolkit) ---------------------------------------

        private void BuildUiIfNeeded()
        {
            if (_overlay != null) return;
            var doc = GetComponent<UIDocument>();
            var root = doc != null ? doc.rootVisualElement : null;
            if (root == null) return;

            _overlay = new VisualElement { name = "levelup-overlay" };
            // The overlay is full-screen and sits at the top sorting order; without
            // this it captures every click/tap while shown (blocking other UI, and
            // movement on pointer control schemes). Children (card + buttons) still
            // pick, so the popup stays interactive — mirrors MusicToggleHud.
            _overlay.pickingMode = PickingMode.Ignore;
            var os = _overlay.style;
            os.position = Position.Absolute;
            os.top = 0f; os.left = 0f; os.right = 0f; os.bottom = 0f;
            // Owner (2026-06-05): the level-up panel must be SUBTLE — tuck it into the
            // top-right corner so it never covers the battle area (was centered + nearly
            // opaque, blanketing the fight). Picking is Ignore on the overlay, so combat
            // stays clickable; the card itself stays interactive.
            os.alignItems = Align.FlexEnd;
            os.justifyContent = Justify.FlexStart;

            var card = new VisualElement { name = "levelup-card" };
            var cs = card.style;
            cs.minWidth = 240f; cs.maxWidth = 280f;
            // Inset from the top-right corner, clear of the top HUD.
            cs.marginTop = 84f; cs.marginRight = 14f;
            cs.paddingTop = 14f; cs.paddingBottom = 14f; cs.paddingLeft = 16f; cs.paddingRight = 16f;
            // Softer + semi-transparent so the fight reads through behind it.
            cs.backgroundColor = new Color(0.10f, 0.12f, 0.18f, 0.82f);
            cs.borderTopLeftRadius = 12f; cs.borderTopRightRadius = 12f;
            cs.borderBottomLeftRadius = 12f; cs.borderBottomRightRadius = 12f;

            _title = MakeLabel("Level Up!", 20f, FontStyle.Bold);
            _title.style.marginBottom = 6f;
            _points = MakeLabel("Available points: 0", 14f, FontStyle.Normal);
            _points.style.marginBottom = 12f;

            _blacksmith  = MakeSkillButton(SkillType.Blacksmith);
            _woodworking = MakeSkillButton(SkillType.Woodworking);
            _arcane      = MakeSkillButton(SkillType.Arcane);
            _gathering   = MakeSkillButton(SkillType.GatheringSpeed);

            // Collapse (not Hide) so closing parks the points on the persistent pill.
            var close = new Button(Collapse) { text = "Later" };
            StyleButton(close, new Color(0.30f, 0.30f, 0.36f, 0.95f));
            close.style.marginTop = 8f;

            card.Add(_title);
            card.Add(_points);
            card.Add(_blacksmith);
            card.Add(_woodworking);
            card.Add(_arcane);
            card.Add(_gathering);
            card.Add(close);
            _card = card;
            _overlay.Add(card);

            // Persistent collapsed indicator: a gold "N skill points — Spend" pill in
            // the same corner. Tapping it re-opens the full card, so banked points are
            // always reachable after the level-up popup is dismissed.
            _pill = new Button(ShowCard) { name = "levelup-pill", text = "Skill points — Spend" };
            var ps = _pill.style;
            ps.marginTop = 84f; ps.marginRight = 14f;
            ps.paddingTop = 8f; ps.paddingBottom = 8f; ps.paddingLeft = 14f; ps.paddingRight = 14f;
            ps.fontSize = 14f;
            ps.unityFontStyleAndWeight = FontStyle.Bold;
            ps.color = new Color(0.12f, 0.10f, 0.05f, 1f);
            ps.backgroundColor = new Color(0.95f, 0.80f, 0.32f, 0.96f);   // gold = "reward waiting"
            ps.borderTopLeftRadius = 10f; ps.borderTopRightRadius = 10f;
            ps.borderBottomLeftRadius = 10f; ps.borderBottomRightRadius = 10f;
            ps.display = DisplayStyle.None;
            _overlay.Add(_pill);

            root.Add(_overlay);
        }

        private Label MakeLabel(string text, float size, FontStyle weight)
        {
            var l = new Label(text);
            var s = l.style;
            s.color = Color.white;
            s.fontSize = size;
            s.unityFontStyleAndWeight = weight;
            s.unityTextAlign = TextAnchor.MiddleCenter;
            return l;
        }

        private Button MakeSkillButton(SkillType type)
        {
            var b = new Button(() => Spend(type));
            StyleButton(b, new Color(0.16f, 0.40f, 0.62f, 0.95f));
            return b;
        }

        private void StyleButton(Button b, Color bg)
        {
            var s = b.style;
            s.height = 38f;
            s.marginTop = 4f; s.marginBottom = 0f;
            s.fontSize = 15f;
            s.unityFontStyleAndWeight = FontStyle.Bold;
            s.unityTextAlign = TextAnchor.MiddleCenter;
            s.color = Color.white;
            s.backgroundColor = bg;
            s.borderTopLeftRadius = 8f; s.borderTopRightRadius = 8f;
            s.borderBottomLeftRadius = 8f; s.borderBottomRightRadius = 8f;
        }
    }
}
