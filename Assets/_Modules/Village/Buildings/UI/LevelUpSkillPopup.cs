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

namespace DeNelle.Village.UI
{
    /// <summary>Level-up skill-point spend panel, driven by progression events.</summary>
    [RequireComponent(typeof(UIDocument))]
    public class LevelUpSkillPopup : MonoBehaviour
    {
        private VisualElement _overlay;   // full-screen centering layer (this is what we show/hide)
        private Label _title;
        private Label _points;
        private Button _blacksmith;
        private Button _woodworking;
        private Button _arcane;
        private Button _gathering;

        private void OnEnable()
        {
            BuildUiIfNeeded();
            Hide();

            if (HeroProgression.Instance != null)
                HeroProgression.Instance.OnLevelUp += Show;
            if (SkillSystem.Instance != null)
                SkillSystem.Instance.OnSkillsChanged += UpdateUI;

            UpdateUI();
        }

        private void OnDisable()
        {
            if (HeroProgression.Instance != null)
                HeroProgression.Instance.OnLevelUp -= Show;
            if (SkillSystem.Instance != null)
                SkillSystem.Instance.OnSkillsChanged -= UpdateUI;
        }

        /// <summary>Reveal the popup (arg = the level just reached — title text only).</summary>
        private void Show(int newLevel)
        {
            if (_overlay == null) return;
            if (_title != null) _title.text = $"Level {newLevel}!  Spend a skill point";
            _overlay.style.display = DisplayStyle.Flex;
            UpdateUI();
        }

        private void Hide()
        {
            if (_overlay != null) _overlay.style.display = DisplayStyle.None;
        }

        private void UpdateUI()
        {
            var sys = SkillSystem.Instance;
            if (_overlay == null || sys == null) return;

            int pts = sys.AvailablePoints;
            if (_points != null) _points.text = $"Available points: {pts}";

            SetSkillButton(_blacksmith,  "Blacksmith",  SkillType.Blacksmith,     pts);
            SetSkillButton(_woodworking, "Woodworking", SkillType.Woodworking,    pts);
            SetSkillButton(_arcane,      "Arcane",      SkillType.Arcane,         pts);
            SetSkillButton(_gathering,   "Gathering",   SkillType.GatheringSpeed, pts);
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
            var os = _overlay.style;
            os.position = Position.Absolute;
            os.top = 0f; os.left = 0f; os.right = 0f; os.bottom = 0f;
            os.alignItems = Align.Center;
            os.justifyContent = Justify.Center;

            var card = new VisualElement { name = "levelup-card" };
            var cs = card.style;
            cs.minWidth = 300f;
            cs.paddingTop = 18f; cs.paddingBottom = 18f; cs.paddingLeft = 20f; cs.paddingRight = 20f;
            cs.backgroundColor = new Color(0.10f, 0.12f, 0.18f, 0.97f);
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

            var close = new Button(Hide) { text = "Close" };
            StyleButton(close, new Color(0.30f, 0.30f, 0.36f, 0.95f));
            close.style.marginTop = 8f;

            card.Add(_title);
            card.Add(_points);
            card.Add(_blacksmith);
            card.Add(_woodworking);
            card.Add(_arcane);
            card.Add(_gathering);
            card.Add(close);

            _overlay.Add(card);
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
