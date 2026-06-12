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
//   │ Farm                                  Lv 2 → 3 │
//   │ Yields: +32 → +44 Food / harvest              │
//   │ Next: 64 Wood (250) · 64 Crystals (40) [Upgr] │   (per-resource have/need color)
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
        // DEF-186: the building the player interacted with — its card is highlighted +
        // scrolled into view when the panel opens. Null = no focus (dev U-key / generic).
        private string _focusBuildingId;
        private ScrollView _scroll;

        // Palette — DARK-GLASS + RUNIC-GOLD shared presentation language. Every
        // colour SOURCES from ElarionUi (the ONE in-game theme) so this panel reads
        // as the SAME designed game as the town HUD / store / build-info preview
        // (which all route through ElarionUi.StylePanel / StyleButton). The earlier
        // WO-393 light-parchment local palette is retired here: this surface now
        // matches the dark-stone sibling screens, with runic gold reserved for
        // frames / titles / the primary CTA and parchment cream for body text.
        // Frame / accent gold (rims) — soft runic gold over the dark stone.
        private static readonly Color GoldRim = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.55f);
        // Heading "gilt" — bright runic gold on the dark stone (high contrast).
        private static readonly Color GiltInk = ElarionUi.Gilt;
        // Secondary / flavour text — muted parchment.
        private static readonly Color InkDim = ElarionUi.ParchmentDim;
        // Affordability tones — the shared theme green / red, harmonised with the palette.
        private static readonly Color Good = ElarionUi.Affordable;
        private static readonly Color Bad = ElarionUi.Danger;
        // Secondary-text alias kept so the rest of the file's references stay
        // behaviour-identical; maps to the dark-theme muted parchment.
        private static readonly Color Dim = ElarionUi.ParchmentDim;         // → secondary text
        // Panel fills — earthy stone (the dark-glass language), sourced from ElarionUi.
        private static readonly Color ShellBg = ElarionUi.PanelStone;       // modal sheet
        private static readonly Color CardBg = ElarionUi.PanelStoneDark;    // recessed card

        public bool IsOpen => _shell != null && _shell.style.display.value != DisplayStyle.None;

        private void Awake()
        {
            _doc = GetComponent<UIDocument>();
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            _panelHandle = PanelManager.Register("Upgrade Buildings", Close, () => IsOpen);
            // DEF-213: let resource-building / Armorer interactions open this panel by id.
            PanelRouter.Register(PanelId.BuildingUpgrade, Open);
            // DEF-186: context-aware open — focus the card for the building tapped.
            PanelRouter.Register(PanelId.BuildingUpgrade, (System.Action<string>)OpenFocused);
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
            PanelRouter.Unregister(PanelId.BuildingUpgrade, Open);
            PanelRouter.Unregister(PanelId.BuildingUpgrade, (System.Action<string>)OpenFocused);
        }

        private void OnLevelChanged(string _) => Repaint();

        // ── Public open/close ───────────────────────────────────────────────

        public void Toggle() { if (IsOpen) Close(); else Open(); }

        /// <summary>
        /// DEF-186: open the panel focused on a specific resource building (the one the
        /// player interacted with) — its card is highlighted and scrolled into view.
        /// Routed via PanelRouter's context-aware opener from BuildingInteractable.
        /// </summary>
        public void OpenFocused(string buildingId)
        {
            _focusBuildingId = ResourceBuildingProgression.IsResourceBuilding(buildingId)
                ? buildingId
                : null;
            Open();
        }

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
            // DEF-186: drop the focus so the NEXT generic open (dev U / mobile button)
            // doesn't inherit a stale highlight from a prior building interaction.
            _focusBuildingId = null;
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
            // Dark-glass stone sheet + runic-gold rim — the shared theme panel (matches
            // the town HUD / build-info preview). StylePanel applies fill + rounding +
            // gold border; we override the radius to the shell's larger corner.
            ElarionUi.StylePanel(_shell);
            ElarionUi.SetRadius(_shell, ElarionUi.RadiusLg);
            _root.Add(_shell);

            BuildHeader(_shell);

            // DEF-186: a ScrollView so a focused card can be scrolled into view and the
            // stacked list never overflows the shell. The cards live in its content.
            _scroll = new ScrollView(ScrollViewMode.Vertical) { name = "CardScroll" };
            _scroll.style.flexGrow = 1;
            _scroll.style.minHeight = 0; // allow it to shrink within the flex column
            _shell.Add(_scroll);

            _cardList = _scroll.contentContainer;
            _cardList.style.flexDirection = FlexDirection.Column;
            _cardList.style.paddingLeft = 12; _cardList.style.paddingRight = 12;
            _cardList.style.paddingTop = 8; _cardList.style.paddingBottom = 8;

            // Wallet footer.
            var footer = new VisualElement { name = "Footer" };
            footer.style.flexDirection = FlexDirection.Row;
            footer.style.flexWrap = Wrap.Wrap;
            footer.style.alignItems = Align.Center;
            footer.style.paddingLeft = 14; footer.style.paddingRight = 14;
            footer.style.paddingTop = 8; footer.style.paddingBottom = 10;
            footer.style.borderTopWidth = 1;
            footer.style.borderTopColor = new StyleColor(GoldRim);
            _shell.Add(footer);

            _walletLabel = new Label();
            _walletLabel.style.color = new StyleColor(InkDim);
            _walletLabel.style.fontSize = ElarionUi.FontLabel;
            _walletLabel.style.whiteSpace = WhiteSpace.Normal;
            footer.Add(_walletLabel);

            // Toast line (upgrade feedback), hidden by default.
            _toast = new Label();
            _toast.style.color = new StyleColor(Good);
            _toast.style.fontSize = ElarionUi.FontLabel;
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
            header.style.borderBottomColor = new StyleColor(GoldRim);
            parent.Add(header);

            // Gilt crest glyph + title — the shared header voice (matches MakeTitle).
            var title = new Label(ElarionUi.CrestGlyph + "  Upgrade Buildings");
            title.style.color = new StyleColor(GiltInk);
            title.style.fontSize = ElarionUi.FontTitle;
            title.style.unityFontStyleAndWeight = FontStyle.Bold;
            title.style.letterSpacing = 1.5f;
            header.Add(title);

            // Close — a themed Danger chip (red glass, parchment "X"), consistent with
            // the shared button language but compact for the corner.
            var closeBtn = new Button(Close) { text = "X" };
            ElarionUi.StyleButton(closeBtn, ElarionUi.ButtonKind.Danger);
            closeBtn.style.width = 32; closeBtn.style.minHeight = 28; closeBtn.style.height = 28;
            closeBtn.style.fontSize = ElarionUi.FontBody;
            closeBtn.style.paddingLeft = 0; closeBtn.style.paddingRight = 0;
            closeBtn.style.marginTop = 0; closeBtn.style.marginBottom = 0;
            header.Add(closeBtn);
        }

        // ── Repaint ─────────────────────────────────────────────────────────

        private void Repaint()
        {
            if (_cardList == null) return;
            _cardList.Clear();

            VisualElement focusCard = null;
            foreach (var id in ResourceBuildingProgression.OrderedIds)
            {
                var def = ResourceBuildingProgression.Find(id);
                if (def == null) continue;
                bool focused = !string.IsNullOrEmpty(_focusBuildingId) && id == _focusBuildingId;
                var card = BuildCard(def, focused);
                _cardList.Add(card);
                if (focused) focusCard = card;
            }

            RefreshWallet();

            // DEF-186: scroll the focused (interacted) building's card into view. Deferred
            // one layout pass so the ScrollView has measured its content before scrolling.
            if (focusCard != null && _scroll != null)
            {
                var target = focusCard;
                focusCard.schedule.Execute(() =>
                {
                    if (_scroll != null && target != null) _scroll.ScrollTo(target);
                }).StartingIn(0);
            }
        }

        private VisualElement BuildCard(ResourceBuildingDef def, bool focused)
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
            Round(card, ElarionUi.RadiusMd);
            // DEF-186: the interacted building's card gets a bright gilt rim so the
            // player sees the panel opened ON that building (the Lumbermill, say);
            // others wear the soft runic-gold hairline of the dark-glass language.
            if (focused)
                Border(card, ElarionUi.Gilt, 2);
            else
                Border(card, new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.35f), 1);

            // Title row: name + level badge.
            var titleRow = new VisualElement();
            titleRow.style.flexDirection = FlexDirection.Row;
            titleRow.style.alignItems = Align.Center;
            titleRow.style.justifyContent = Justify.SpaceBetween;
            card.Add(titleRow);

            var name = new Label(def.DisplayName);
            name.style.color = new StyleColor(GiltInk);
            name.style.fontSize = ElarionUi.FontHead;
            name.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleRow.Add(name);

            // WO-237: clear current → next tier badge so the player sees what the
            // upgrade moves them TO, not just where they are. Maxed shows "Lv N · MAX".
            var levelBadge = new Label(maxed ? $"Lv {level} · MAX" : $"Lv {level} → {level + 1}");
            // Aether-violet level badge — the theme's runic/progression accent (matches
            // the build-info tier badge), reading clean on the dark stone card.
            levelBadge.style.color = new StyleColor(ElarionUi.Aether);
            levelBadge.style.fontSize = ElarionUi.FontLabel;
            levelBadge.style.unityFontStyleAndWeight = FontStyle.Bold;
            titleRow.Add(levelBadge);

            // Yield line — WO-237: show the current → next yield delta so the upgrade's
            // benefit is explicit (e.g. "+20 Food → +32 Food / harvest"), not just the
            // current output. At max we show only the current (final) yield.
            int yield = lvlDef != null ? lvlDef.YieldPerTick : 0;
            string resLabel = ResourceBuildingProgression.LabelFor(def.Yields);
            string yieldText;
            if (maxed)
            {
                yieldText = $"Yields  +{yield} {resLabel} / harvest";
            }
            else
            {
                var nextDef = def.LevelDef(level + 1);
                int nextYield = nextDef != null ? nextDef.YieldPerTick : yield;
                yieldText = nextYield > yield
                    ? $"Yields  +{yield} → +{nextYield} {resLabel} / harvest"
                    : $"Yields  +{yield} {resLabel} / harvest";
            }
            var yieldLine = new Label(yieldText);
            yieldLine.style.color = new StyleColor(Dim);
            yieldLine.style.fontSize = ElarionUi.FontLabel;
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
                maxLine.style.fontSize = ElarionUi.FontLabel;
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

                // WO-237: per-resource cost with have/need coloring. Each line shows
                // "need (have)" and is GREEN when you have enough of that resource, RED
                // when short — so the player sees AT A GLANCE which resource is blocking
                // the upgrade, not just one flat-colored aggregate line.
                costColumn.Add(BuildCostBreakdown(lvlDef));

                if (magicGated)
                {
                    var techLine = new Label("Unlocks tech: Arcane Forge");
                    // Aether/violet — the theme's runic/magic accent, reading on dark stone.
                    techLine.style.color = new StyleColor(ElarionUi.Aether);
                    techLine.style.fontSize = ElarionUi.FontMicro;
                    techLine.style.whiteSpace = WhiteSpace.Normal;
                    costColumn.Add(techLine);
                }

                var upBtn = new Button(() => OnUpgradeClicked(def.BuildingId)) { text = magicGated ? "Empower" : "Upgrade" };
                // The shared CTA button language: gilt-rimmed gold glass when affordable
                // (dark-ink label), stone-grey Disabled when short — same as the build /
                // store / inventory buttons. StyleButton owns fill / radius / hover-press.
                ElarionUi.StyleButton(upBtn, affordable ? ElarionUi.ButtonKind.Gold : ElarionUi.ButtonKind.Disabled);
                upBtn.style.minWidth = 110;
                upBtn.style.height = 40; // tap-friendly target
                upBtn.style.marginLeft = 10;
                upBtn.SetEnabled(affordable);
                costRow.Add(upBtn);
            }

            return card;
        }

        /// <summary>
        /// WO-237: builds the per-resource cost breakdown for the next tier. One chip
        /// per cost line, reading "Next: 64 Wood (250)" with the line GREEN when the
        /// unified GameState wallet (via ResourceLedger) holds enough of THAT resource
        /// and RED when it is short — so affordability is legible per resource, not as a
        /// single aggregate color. Magic (the tech axis) is appended the same way.
        /// </summary>
        private static VisualElement BuildCostBreakdown(ResourceLevelDef lvlDef)
        {
            var wrap = new VisualElement();
            wrap.style.flexDirection = FlexDirection.Row;
            wrap.style.flexWrap = Wrap.Wrap;
            wrap.style.alignItems = Align.Center;

            var prefix = new Label("Next: ");
            prefix.style.color = new StyleColor(Dim);
            prefix.style.fontSize = ElarionUi.FontLabel;
            wrap.Add(prefix);

            bool hasHarvest = lvlDef != null && lvlDef.UpgradeCost != null && lvlDef.UpgradeCost.Count > 0;
            bool any = false;

            if (hasHarvest)
            {
                for (int i = 0; i < lvlDef.UpgradeCost.Count; i++)
                {
                    var c = lvlDef.UpgradeCost[i];
                    int have = ResourceLedger.Balance(c.Resource);
                    bool ok = have >= c.Amount;
                    wrap.Add(MakeCostChip(
                        $"{c.Amount} {ResourceBuildingProgression.LabelFor(c.Resource)}",
                        have, ok, i > 0, ResourceIcon(c.Resource)));
                    any = true;
                }
            }

            // Magic is the tech axis (not a harvestable) — surface it as a colored chip too.
            if (lvlDef != null && lvlDef.MagicCost > 0)
            {
                int haveMagic = ResourceLedger.MagicBalance();
                bool ok = haveMagic >= lvlDef.MagicCost;
                wrap.Add(MakeCostChip($"{lvlDef.MagicCost} Magic", haveMagic, ok, any, null));
                any = true;
            }

            if (!any)
            {
                var none = new Label("—");
                none.style.color = new StyleColor(Dim);
                none.style.fontSize = ElarionUi.FontLabel;
                wrap.Add(none);
            }

            return wrap;
        }

        /// <summary>
        /// A single colored cost line "{need} {Res} ({have})": Good (theme green) when
        /// affordable, Bad (theme red) when short. When a resource <paramref name="icon"/>
        /// is supplied it is shown as a small badge ahead of the text (the same per-resource
        /// art the town HUD uses), so the player reads the resource at a glance — the
        /// affordability colour still carries the have/need state. <paramref name="leadingSep"/>
        /// prepends a separator dot so the chips read as a list without depending on flex gaps.
        /// </summary>
        private static VisualElement MakeCostChip(string needText, int have, bool affordable, bool leadingSep, Sprite icon)
        {
            var chip = new VisualElement();
            chip.style.flexDirection = FlexDirection.Row;
            chip.style.alignItems = Align.Center;
            chip.style.marginLeft = leadingSep ? 4 : 0;
            chip.style.marginRight = 2;

            if (leadingSep)
            {
                var sep = new Label("·  ");
                sep.style.color = new StyleColor(Dim);
                sep.style.fontSize = ElarionUi.FontLabel;
                chip.Add(sep);
            }

            // Per-resource icon badge (town-HUD art), sprite-FIRST: shown only when the
            // art resolved — otherwise the chip is text-only (WebGL-safe, never blank).
            if (icon != null)
            {
                var badge = new VisualElement();
                badge.style.width = 14; badge.style.height = 14;
                badge.style.marginRight = 4;
                badge.style.flexShrink = 0;
                badge.style.backgroundImage = new StyleBackground(icon);
                // Tint to the affordability colour so the icon reads with the have/need state.
                badge.style.unityBackgroundImageTintColor = new StyleColor(affordable ? Good : Bad);
                chip.Add(badge);
            }

            var lbl = new Label($"{needText} ({have})");
            lbl.style.color = new StyleColor(affordable ? Good : Bad);
            lbl.style.fontSize = ElarionUi.FontLabel;
            lbl.style.unityFontStyleAndWeight = affordable ? FontStyle.Normal : FontStyle.Bold;
            chip.Add(lbl);
            return chip;
        }

        // ── Resource icon resolver (town-HUD art, WebGL-safe, cached) ─────────
        // The town HUD shows per-resource icons at Resources/HudIcons/hud_<res>
        // (hud_wood / hud_iron / hud_food / hud_crystal). We reuse that SAME art so
        // the upgrade panel reads as one game. Sprite-first: null when absent — the
        // cost chip then renders text-only (never blank). Cached per resource.
        private static readonly System.Collections.Generic.Dictionary<HarvestResource, Sprite> _resIcons
            = new System.Collections.Generic.Dictionary<HarvestResource, Sprite>();

        private static Sprite ResourceIcon(HarvestResource res)
        {
            if (_resIcons.TryGetValue(res, out var cached)) return cached;
            string id;
            switch (res)
            {
                case HarvestResource.Wood:     id = "hud_wood";    break;
                case HarvestResource.Iron:     id = "hud_iron";    break;
                case HarvestResource.Food:     id = "hud_food";    break;
                case HarvestResource.Crystals: id = "hud_crystal"; break;
                default:                       id = null;          break;
            }
            Sprite sp = null;
            if (id != null)
            {
                try { sp = Resources.Load<Sprite>("HudIcons/" + id); }
                catch (System.Exception e)
                {
                    Debug.LogWarning("[BuildingUpgradePanel] resource icon load failed for " + id + ": " + e.Message);
                }
            }
            _resIcons[res] = sp;
            return sp;
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
