// =============================================================================
// BuildingUpgradePanel — code-built UI Toolkit panel for upgrading the three
// resource buildings (Farm / Lumbermill / Forge). WO-151 / DEF-121 (WO-230).
// -----------------------------------------------------------------------------
// CRITICAL (project memory "UXML UIDocuments don't render in builds"): this
// panel builds its ENTIRE tree in C# against UIDocument.rootVisualElement — it
// loads NO .uxml / .uss. That is the SAME approach as VillageCraftingPanel /
// HeroTalentPanel / PetSkillTreePanel, all of which render correctly in player
// (incl. WebGL) builds. The UXML-doesn't-render trap is specific to .uxml-SOURCED
// documents; code-built trees are fine.
//
// Layout (one card per building, stacked):
//   ┌──────────────────────────────────────────────┐
//   │ Upgrade Buildings                       [ X ] │
//   ├──────────────────────────────────────────────┤
//   │ Farm                                    Lv 2  │
//   │ Yields: +32 Food / harvest                    │
//   │ Next: 64 Wood · 64 Crystals     [ Upgrade ]   │
//   ├──────────────────────────────────────────────┤
//   │ Lumbermill ...                                │
//   ├──────────────────────────────────────────────┤
//   │ Forge ...                                     │
//   ├──────────────────────────────────────────────┤
//   │ Have: 250 Crystals · 80 Food · 15 Wood · 5 Fe │
//   └──────────────────────────────────────────────┘
//
// Tap-friendly: every interactive element is a UI Toolkit Button, which handles
// touch + mouse via the panel's own pointer events (no EventSystem / uGUI
// GraphicRaycaster needed — same as the crafting panel). Public API mirrors the
// crafting panel: Open / Close / Toggle / IsOpen.
// =============================================================================

using System.Text;
using DeNelle.Core.State;
using DeNelle.Core.UI;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Village.Buildings.Progression
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(UIDocument))]
    public sealed class BuildingUpgradePanel : MonoBehaviour
    {
        public static BuildingUpgradePanel Instance { get; private set; }

        private UIDocument _doc;
        private VisualElement _root;
        private VisualElement _shell;
        private VisualElement _cardList;
        private Label _walletLabel;
        private Label _toast;
        // Modal arbiter handle (DEF-212): one panel open at a time.
        private PanelHandle _panelHandle;

        // Palette — matches VillageCraftingPanel's amber-on-dark theme.
        private static readonly Color Gold = new Color(0.99f, 0.86f, 0.50f, 1f);
        private static readonly Color GoldRim = new Color(0.85f, 0.66f, 0.30f, 1f);
        private static readonly Color Cream = new Color(0.97f, 0.94f, 0.84f, 1f);
        private static readonly Color Dim = new Color(0.78f, 0.74f, 0.66f, 1f);
        private static readonly Color Good = new Color(0.55f, 0.85f, 0.45f, 1f);
        private static readonly Color Bad = new Color(0.85f, 0.45f, 0.40f, 1f);
        private static readonly Color ShellBg = new Color(0.09f, 0.07f, 0.06f, 0.98f);
        private static readonly Color CardBg = new Color(0.12f, 0.09f, 0.05f, 1f);

        public bool IsOpen => _shell != null && _shell.style.display.value != DisplayStyle.None;

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _panelHandle = PanelManager.Register("Upgrade Buildings", Close, () => IsOpen);
        }

        private void OnEnable()
        {
            Build();
            ResourceBuildingState.LevelChanged += OnLevelChanged;
            var svc = GameStateService.Instance;
            if (svc != null) svc.ResourcesChanged.AddListener(Repaint);
        }

        private void OnDisable()
        {
            ResourceBuildingState.LevelChanged -= OnLevelChanged;
            var svc = GameStateService.Instance;
            if (svc != null) svc.ResourcesChanged.RemoveListener(Repaint);
            if (Instance == this) Instance = null;
        }

        private void OnLevelChanged(string _) => Repaint();

        // ── Public open/close ───────────────────────────────────────────────

        public void Toggle() { if (IsOpen) Close(); else Open(); }

        public void Open()
        {
            if (_shell == null) Build();
            if (_shell == null) return;
            _shell.style.display = DisplayStyle.Flex;
            // Show the scrim + capture input only while open (DEF-212 item 5: near-opaque
            // so world-space labels don't bleed through behind the modal).
            if (_root != null)
            {
                _root.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0.92f));
                _root.pickingMode = PickingMode.Position;
            }
            // Arbiter closes any other open panel first (DEF-212).
            PanelManager.NotifyOpened(_panelHandle);
            Repaint();
        }

        public void Close()
        {
            if (_shell == null) return;
            _shell.style.display = DisplayStyle.None;
            // Drop the scrim so a closed panel never darkens or blocks the HUD (DEF-212).
            if (_root != null)
            {
                _root.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
                _root.pickingMode = PickingMode.Ignore;
            }
            PanelManager.NotifyClosed(_panelHandle);
        }

        // ── Build ────────────────────────────────────────────────────────────

        private void Build()
        {
            _root = _doc != null ? _doc.rootVisualElement : null;
            if (_root == null) return;

            _root.Clear();
            _root.style.position = Position.Absolute;
            _root.style.left = 0; _root.style.right = 0;
            _root.style.top = 0; _root.style.bottom = 0;
            _root.style.alignItems = Align.Center;
            _root.style.justifyContent = Justify.Center;
            // Start transparent + click-through; Open() turns the scrim on per-open, so a
            // CLOSED panel never darkens / eats clicks on the HUD (DEF-212).
            _root.style.backgroundColor = new StyleColor(new Color(0f, 0f, 0f, 0f));
            _root.pickingMode = PickingMode.Ignore;
            _root.RegisterCallback<KeyDownEvent>(OnRootKey, TrickleDown.TrickleDown);

            _shell = new VisualElement { name = "UpgradeShell" };
            _shell.style.width = 560;
            _shell.style.maxWidth = new Length(95, LengthUnit.Percent);
            _shell.style.maxHeight = new Length(92, LengthUnit.Percent);
            _shell.style.flexDirection = FlexDirection.Column;
            _shell.style.backgroundColor = new StyleColor(ShellBg);
            Round(_shell, 12);
            Border(_shell, GoldRim, 2);
            _root.Add(_shell);

            BuildHeader(_shell);

            _cardList = new VisualElement { name = "CardList" };
            _cardList.style.flexDirection = FlexDirection.Column;
            _cardList.style.paddingLeft = 12; _cardList.style.paddingRight = 12;
            _cardList.style.paddingTop = 8; _cardList.style.paddingBottom = 8;
            _shell.Add(_cardList);

            // Wallet footer.
            var footer = new VisualElement { name = "Footer" };
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.flexWrap = Wrap.Wrap;
            footer.style.alignItems = Align.Center;
            footer.style.paddingLeft = 14; footer.style.paddingRight = 14;
            footer.style.paddingTop = 8; footer.style.paddingBottom = 10;
            footer.style.borderTopWidth = 1;
            footer.style.borderTopColor = new StyleColor(new Color(0.85f, 0.66f, 0.30f, 0.45f));
            _shell.Add(footer);

            _walletLabel = new Label();
            _walletLabel.style.color = new StyleColor(Cream);
            _walletLabel.style.fontSize = 12;
            _walletLabel.style.whiteSpace = WhiteSpace.Normal;
            footer.Add(_walletLabel);

            // Toast line (upgrade feedback), hidden by default.
            _toast = new Label();
            _toast.style.color = new StyleColor(Good);
            _toast.style.fontSize = 12;
            _toast.style.unityTextAlign = TextAnchor.MiddleCenter;
            _toast.style.paddingBottom = 6;
            _toast.style.display = DisplayStyle.None;
            _shell.Add(_toast);

            _shell.style.display = DisplayStyle.None; // opened by bootstrap / input
            Repaint();
        }

        private void BuildHeader(VisualElement parent)
        {
            var header = new VisualElement { name = "Header" };
            header.style.flexDirection = FlexDirection.Row;
            header.style.alignItems = Align.Center;
            header.style.justifyContent = Justify.SpaceBetween;
            header.style.paddingLeft = 16; header.style.paddingRight = 10;
            header.style.paddingTop = 10; header.style.paddingBottom = 10;
            header.style.borderBottomWidth = 1;
            header.style.borderBottomColor = new StyleColor(new Color(0.85f, 0.66f, 0.30f, 0.45f));
            parent.Add(header);

            var title = new Label("Upgrade Buildings");
            title.style.color = new StyleColor(Gold);
            title.style.fontSize = 20;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            header.Add(title);

            var closeBtn = new Button(Close) { text = "X" };
            closeBtn.style.width = 32; closeBtn.style.height = 28;
            closeBtn.style.fontSize = 14;
            closeBtn.style.color = new StyleColor(Cream);
            closeBtn.style.backgroundColor = new StyleColor(new Color(0.18f, 0.10f, 0.06f, 1f));
            Round(closeBtn, 6);
            Border(closeBtn, new Color(0.85f, 0.66f, 0.30f, 0.65f), 1);
            header.Add(closeBtn);
        }

        // ── Repaint ─────────────────────────────────────────────────────────

        private void Repaint()
        {
            if (_cardList == null) return;
            _cardList.Clear();

            foreach (var id in ResourceBuildingProgression.OrderedIds)
            {
                var def = ResourceBuildingProgression.Find(id);
                if (def != null) _cardList.Add(BuildCard(def));
            }

            RefreshWallet();
        }

        private VisualElement BuildCard(ResourceBuildingDef def)
        {
            int level = ResourceBuildingState.GetLevel(def.BuildingId);
            var lvlDef = def.LevelDef(level);
            bool maxed = lvlDef == null || lvlDef.IsMaxLevel;
            bool magicGated = !maxed && lvlDef.IsMagicGated;
            bool affordable = !maxed
                && ResourceLedger.CanAfford(lvlDef.UpgradeCost)
                && (!magicGated || ResourceLedger.MagicBalance() >= lvlDef.MagicCost);

            var card = new VisualElement { name = "Card_" + def.BuildingId };
            card.style.flexDirection = FlexDirection.Column;
            card.style.marginBottom = 8;
            card.style.paddingLeft = 12; card.style.paddingRight = 12;
            card.style.paddingTop = 10; card.style.paddingBottom = 10;
            card.style.backgroundColor = new StyleColor(CardBg);
            Round(card, 8);
            Border(card, new Color(0.85f, 0.66f, 0.30f, 0.35f), 1);

            // Title row: name + level badge.
            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.justifyContent = Justify.SpaceBetween;
            card.Add(titleRow);

            var name = new Label(def.DisplayName);
            name.style.color = new StyleColor(Gold);
            name.style.fontSize = 16;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleRow.Add(name);

            var levelBadge = new Label(maxed ? $"Lv {level} · MAX" : $"Lv {level}");
            levelBadge.style.color = new StyleColor(Cream);
            levelBadge.style.fontSize = 13;
            titleRow.Add(levelBadge);

            // Yield line.
            int yield = lvlDef != null ? lvlDef.YieldPerTick : 0;
            var yieldLine = new Label($"Yields  +{yield} {ResourceBuildingProgression.LabelFor(def.Yields)} / harvest");
            yieldLine.style.color = new StyleColor(Dim);
            yieldLine.style.fontSize = 12;
            yieldLine.style.marginTop = 4;
            card.Add(yieldLine);

            // Cost + upgrade row.
            var costRow = new VisualElement();
            costRow.style.flexDirection = FlexDirection.Row;
            costRow.style.alignItems = Align.Center;
            costRow.style.justifyContent = Justify.SpaceBetween;
            costRow.style.marginTop = 8;
            card.Add(costRow);

            if (maxed)
            {
                var maxLine = new Label("Fully upgraded.");
                maxLine.style.color = new StyleColor(Good);
                maxLine.style.fontSize = 12;
                costRow.Add(maxLine);
            }
            else
            {
                // For a Magic-gated tier, surface the Magic requirement + the tech node
                // it unlocks so the player sees WHY it differs from an ordinary tier.
                var costColumn = new VisualElement();
                costColumn.style.flexGrow = 1;
                costColumn.style.flexDirection = FlexDirection.Column;
                costRow.Add(costColumn);

                var costLine = new Label("Next:  " + FormatCost(lvlDef));
                costLine.style.color = new StyleColor(affordable ? Dim : Bad);
                costLine.style.fontSize = 12;
                costLine.style.whiteSpace = WhiteSpace.Normal;
                costColumn.Add(costLine);

                if (magicGated)
                {
                    var techLine = new Label("Unlocks tech: Arcane Forge");
                    techLine.style.color = new StyleColor(new Color(0.70f, 0.55f, 0.95f, 1f));
                    techLine.style.fontSize = 11;
                    techLine.style.whiteSpace = WhiteSpace.Normal;
                    costColumn.Add(techLine);
                }

                var upBtn = new Button(() => OnUpgradeClicked(def.BuildingId)) { text = magicGated ? "Empower" : "Upgrade" };
                upBtn.style.minWidth = 110;
                upBtn.style.height = 40; // tap-friendly target
                upBtn.style.fontSize = 14;
                upBtn.style.unityFontStyleAndWeight = FontStyle.Bold;
                upBtn.style.marginLeft = 10;
                Round(upBtn, 8);
                upBtn.SetEnabled(affordable);
                upBtn.style.color = new StyleColor(affordable
                    ? new Color(0.10f, 0.08f, 0.04f, 1f)
                    : new Color(0.55f, 0.50f, 0.42f, 1f));
                upBtn.style.backgroundColor = new StyleColor(affordable
                    ? Gold
                    : new Color(0.30f, 0.24f, 0.18f, 1f));
                Border(upBtn,
                    affordable ? new Color(1f, 0.78f, 0.32f, 1f) : new Color(0.50f, 0.40f, 0.22f, 1f),
                    2);
                costRow.Add(upBtn);
            }

            return card;
        }

        private static string FormatCost(ResourceLevelDef lvlDef)
        {
            if (lvlDef == null) return "—";
            bool hasHarvest = lvlDef.UpgradeCost != null && lvlDef.UpgradeCost.Count > 0;
            if (!hasHarvest && lvlDef.MagicCost <= 0) return "—";

            var sb = new StringBuilder();
            if (hasHarvest)
            {
                for (int i = 0; i < lvlDef.UpgradeCost.Count; i++)
                {
                    var c = lvlDef.UpgradeCost[i];
                    if (i > 0) sb.Append("  ·  ");
                    sb.Append(c.Amount).Append(' ').Append(ResourceBuildingProgression.LabelFor(c.Resource));
                }
            }
            // Magic is the tech axis (not a harvestable) — append it as the gating cost.
            if (lvlDef.MagicCost > 0)
            {
                if (sb.Length > 0) sb.Append("  ·  ");
                sb.Append(lvlDef.MagicCost).Append(" Magic");
            }
            return sb.ToString();
        }

        private void RefreshWallet()
        {
            if (_walletLabel == null) return;
            _walletLabel.text =
                $"Have:  {ResourceLedger.Balance(HarvestResource.Crystals)} Crystals  ·  " +
                $"{ResourceLedger.Balance(HarvestResource.Food)} Food  ·  " +
                $"{ResourceLedger.Balance(HarvestResource.Wood)} Wood  ·  " +
                $"{ResourceLedger.Balance(HarvestResource.Iron)} Iron  ·  " +
                $"{ResourceLedger.MagicBalance()} Magic";
        }

        private void OnUpgradeClicked(string buildingId)
        {
            var result = ResourceBuildingState.TryUpgrade(buildingId);
            var def = ResourceBuildingProgression.Find(buildingId);
            string label = def != null ? def.DisplayName : buildingId;

            switch (result)
            {
                case UpgradeResult.Upgraded:
                    ShowToast($"{label} upgraded to Lv {ResourceBuildingState.GetLevel(buildingId)}", Good);
                    break;
                case UpgradeResult.Insufficient:
                    ShowToast($"Not enough resources to upgrade {label}.", Bad);
                    break;
                case UpgradeResult.NeedMagic:
                    ShowToast($"{label}'s arcane tier needs Magic — earn it from bosses & dungeons.", Bad);
                    break;
                case UpgradeResult.MaxLevel:
                    ShowToast($"{label} is already fully upgraded.", Good);
                    break;
                default:
                    ShowToast("Upgrade unavailable.", Bad);
                    break;
            }
            // TryUpgrade raises LevelChanged / ResourcesChanged which repaint, but
            // a max/insufficient path may not — repaint defensively.
            Repaint();
        }

        private void ShowToast(string text, Color color)
        {
            if (_toast == null) return;
            _toast.text = text;
            _toast.style.color = new StyleColor(color);
            _toast.style.display = DisplayStyle.Flex;
            CancelInvoke(nameof(HideToast));
            Invoke(nameof(HideToast), 2.5f);
        }

        private void HideToast()
        {
            if (_toast != null) _toast.style.display = DisplayStyle.None;
        }

        private void OnRootKey(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape && IsOpen)
            {
                Close();
                evt.StopPropagation();
            }
        }

        // ── Style helpers ────────────────────────────────────────────────────

        private static void Round(VisualElement ve, float r)
        {
            ve.style.borderTopLeftRadius = r;
            ve.style.borderTopRightRadius = r;
            ve.style.borderBottomLeftRadius = r;
            ve.style.borderBottomRightRadius = r;
        }

        private static void Border(VisualElement ve, Color color, float width)
        {
            ve.style.borderLeftColor = new StyleColor(color);
            ve.style.borderRightColor = new StyleColor(color);
            ve.style.borderTopColor = new StyleColor(color);
            ve.style.borderBottomColor = new StyleColor(color);
            ve.style.borderLeftWidth = width;
            ve.style.borderRightWidth = width;
            ve.style.borderTopWidth = width;
            ve.style.borderBottomWidth = width;
        }
    }
}
