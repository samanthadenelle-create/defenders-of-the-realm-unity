// =============================================================================
// HeroInventoryController — full-screen, mobile-first Inventory + Gear/Armor UI.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// CODE-BUILT uGUI ONLY (Canvas/Image/Button/ScrollRect/TextMeshProUGUI). This is
// the proven-reliable path in this project — UXML/UI-Toolkit HUDs come up empty in
// player builds (PIPELINE_STATE §8, "UXML in builds: does NOT work"). The whole
// look + helper recipe (Scrim, RoundedSprite, AddImage/AddLabel/AddButton,
// StyleButtonColors, WebGL-safe try/catch) is mirrored from the canonical
// ArenaPanel.cs so this reads as the same designed game.
//
// WHAT IT DRIVES (no new equip system, no new item DB — CLAUDE.md / memory rule):
//   • Weapons / Armor come from GearCatalog (weapons.json / armor.json). The hero's
//     CLASS gates which items appear (GearCatalog.JobMatches), level gates which are
//     EQUIPPABLE now (GearReq.level). We list every class-eligible item; level-locked
//     ones show a lock + are not equippable yet.
//   • Equipping drives GearLoadout.EquipWeaponById / EquipArmorById on the live hero.
//     GearLoadout fires OnGearChanged -> EquipmentController swaps the held mesh, so
//     equipping here VISIBLY changes the hero. We don't rebuild that link.
//   • Consumables come from the persisted larder via ItemInventory.OwnedConsumables()
//     (id -> count), described by ConsumableCatalog.
//
// DATA GAP (documented, not invented):
//   There is no per-player "owned weapons/armor" list today — gear is class+level
//   auto-equip (GearLoadout.Refresh picks the BEST eligible). So "owned" for the
//   Weapons/Armor/Outfits tabs == "class-eligible from the catalog" (level-locked =
//   greyed). When a real ownership list lands (loot/craft/shop grants), filter the
//   grid by it where marked `// TODO owned-list`. The plumbing (tabs, grid, equip)
//   is already correct against GearLoadout.
//
// Entry points mirror ArenaAttackRecruitController: EnsureExists() / Open() / Close().
// ASCII-only runtime strings. WebGL-safe (RoundedSprite falls back to a flat quad).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.UI;
using DeNelle.Village.Items;

namespace DeNelle.Village
{
    /// <summary>Full-screen inventory + gear modal. Singleton; Open()/Close() driven.</summary>
    public sealed class HeroInventoryController : MonoBehaviour
    {
        public static HeroInventoryController Instance { get; private set; }

        private enum Tab { Weapons, Armor, Outfits, Consumables }

        private GameObject _ui;
        private GameObject _gridRoot;     // re-built per tab
        private GameObject _sidebarRoot;  // re-built per selection
        private GameObject _paperDoll;    // rebuilt on equip-change
        private Tab _tab = Tab.Weapons;

        // The current selection (one of these is non-null while a cell is selected).
        private WeaponDef _selWeapon;
        private ArmorDef _selArmor;
        private ConsumableSel _selConsumable;

        private GearLoadout _loadout;     // the live hero's gear model (drives the hero)

        // ── Sleek palette (mirrors ArenaPanel — rebuilt from ElarionUi, no HUD ref) ──
        private static readonly Color Glass     = new Color(0.06f, 0.07f, 0.09f, 0.66f);
        private static readonly Color GlassDeep = new Color(0.04f, 0.05f, 0.07f, 0.82f);
        private static readonly Color Track     = new Color(0.0f,  0.0f,  0.0f,  0.45f);
        private static readonly Color Cell      = new Color(0.10f, 0.11f, 0.14f, 0.86f);
        private static readonly Color CellSel    = new Color(0.16f, 0.18f, 0.24f, 0.95f);
        private static readonly Color AccentSoft = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.30f);
        private static readonly Color Accent     = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.85f);

        public bool IsOpen => _ui != null && _ui.activeSelf;

        // -- lifecycle -------------------------------------------------------
        public static HeroInventoryController EnsureExists()
        {
            if (Instance != null) return Instance;
            var go = new GameObject("HeroInventoryController");
            Instance = go.AddComponent<HeroInventoryController>();
            return Instance;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            Unsubscribe();
            if (_ui != null) Destroy(_ui);
            if (Instance == this) Instance = null;
        }

        /// <summary>Open the inventory modal (builds the overlay if needed).</summary>
        public void Open()
        {
            try
            {
                ResolveLoadout();
                if (_ui == null) BuildRoot();
                _ui.SetActive(true);
                Subscribe();
                _tab = Tab.Weapons;
                ClearSelection();
                RebuildPaperDoll();
                RebuildGrid();
                RebuildSidebar();
            }
            catch (System.Exception e)
            {
                Debug.LogError("[HeroInventoryController] Open failed (UI may be partial): " + e);
            }
        }

        /// <summary>Tear the overlay down (keeps the controller alive for re-open).</summary>
        public void Close()
        {
            Unsubscribe();
            if (_ui != null) Destroy(_ui);
            _ui = null;
            _gridRoot = _sidebarRoot = _paperDoll = null;
            ClearSelection();
        }

        public void Toggle() { if (IsOpen) Close(); else Open(); }

        // -- hero / gear resolution -----------------------------------------
        private void ResolveLoadout()
        {
            if (_loadout != null) return;
            var hero = GameObject.FindWithTag("Player");
            if (hero == null) hero = SafeFindByTag("HeroTarget");
            if (hero != null) _loadout = hero.GetComponentInChildren<GearLoadout>();
        }

        private static GameObject SafeFindByTag(string tag)
        {
            try { return GameObject.FindWithTag(tag); }
            catch { return null; }
        }

        private string HeroJob =>
            _loadout != null && _loadout.GetComponent<HeroAbilities>() != null
                ? _loadout.GetComponent<HeroAbilities>().HeroClass
                : AbilityCatalog.DefaultClass;

        private void Subscribe()
        {
            if (_loadout != null) _loadout.OnGearChanged += HandleGearChanged;
        }

        private void Unsubscribe()
        {
            if (_loadout != null) _loadout.OnGearChanged -= HandleGearChanged;
        }

        private void HandleGearChanged()
        {
            // The hero's equipped pieces changed (here or via auto-equip on level-up).
            RebuildPaperDoll();
            RebuildGrid();      // refresh equipped indicators
            RebuildSidebar();   // refresh Equip/Unequip button state
        }

        // ====================================================================
        // ROOT + CHROME
        // ====================================================================
        private void BuildRoot()
        {
            _ui = new GameObject("HeroInventoryUI");

            var canvas = _ui.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 2600;                 // above HUD + Arena (1100)

            var scaler = _ui.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _ui.AddComponent<GraphicRaycaster>();

            // Full-screen dark scrim (alpha ~0.85) that blocks click-through.
            var scrim = AddImage(_ui.transform, "Scrim", Vector2.zero, Vector2.one,
                                 new Color(0.02f, 0.015f, 0.04f, 0.85f), rounded: false);
            // Tapping the scrim closes (a button covering the whole backdrop, behind the panel).
            var scrimBtn = scrim.AddComponent<Button>();
            scrimBtn.transition = Selectable.Transition.None;
            scrimBtn.onClick.AddListener(Close);

            // The main panel fills most of the screen (mobile-first).
            var panel = AddPanel(_ui.transform, new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.97f), deep: true);

            // Title row + points/gold + Close (X).
            AddLabel(panel.transform, ElarionUi.CrestGlyph + "  INVENTORY", 0.945f, 0.99f,
                     ElarionUi.Gilt, ElarionUi.FontTitle, TMPro.TextAlignmentOptions.Center,
                     0.06f, 0.94f, spacing: 6f, bold: true);
            AddRule(panel.transform, 0.935f, 0.06f, 0.94f);

            BuildWalletStrip(panel.transform);

            // Close X — top-right.
            AddButton(panel.transform, "X", new Vector2(0.90f, 0.05f), new Vector2(0.945f, 0.99f),
                      Glass, Close, ButtonKind.Neutral);

            // Top half: paper-doll / character preview area.
            _paperDoll = AddImage(panel.transform, "PaperDollArea",
                                  new Vector2(0.04f, 0.62f), new Vector2(0.96f, 0.86f), Track);

            // Tabs row.
            BuildTabs(panel.transform);

            // Grid area (rebuilt per tab) lives in a fixed band; sidebar to its right.
            // Grid: left ~64%; Sidebar: right ~34%.
            _gridRoot = AddImage(panel.transform, "GridArea",
                                 new Vector2(0.04f, 0.04f), new Vector2(0.635f, 0.50f), Track);
            _sidebarRoot = AddImage(panel.transform, "SidebarArea",
                                    new Vector2(0.655f, 0.04f), new Vector2(0.96f, 0.50f), GlassDeep);
        }

        private void BuildWalletStrip(Transform panel)
        {
            int coins = 0, crystals = 0;
            try
            {
                var s = DeNelle.Core.State.GameStateService.Instance;
                if (s != null && s.State != null)
                {
                    coins = s.State.Resources.Coins;
                    crystals = s.State.Resources.Crystals;
                }
            }
            catch { /* GameState not ready — show zeros */ }

            var well = AddImage(panel, "WalletWell", new Vector2(0.30f, 0.895f), new Vector2(0.70f, 0.93f), Track);
            AddLabel(well.transform, $"{coins} Gold      {crystals} Crystals", 0f, 1f,
                     ElarionUi.Gilt, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);
        }

        // ── Paper-doll: a slot summary of the hero's equipped pieces. ──────────
        // TODO live hero preview — a RenderTexture of the hero model is the stretch
        // goal; v1 shows the equipped Weapon/Armor slots + class so the screen reads.
        private void RebuildPaperDoll()
        {
            if (_paperDoll == null) return;
            for (int i = _paperDoll.transform.childCount - 1; i >= 0; i--)
                Destroy(_paperDoll.transform.GetChild(i).gameObject);

            AddLabel(_paperDoll.transform, "EQUIPPED", 0.86f, 0.99f, ElarionUi.ParchmentDim,
                     ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, spacing: 3f);

            string job = HeroJob;
            AddLabel(_paperDoll.transform, "Class: " + Cap(job), 0.74f, 0.85f, ElarionUi.Parchment,
                     ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f);

            // Weapon slot.
            WeaponDef w = _loadout != null ? _loadout.EquippedWeapon : null;
            EquipSlotTile(_paperDoll.transform, "WEAPON",
                          w != null ? RarityGlyph(w.rarity) + " " + w.name : "(none)",
                          w != null ? RarityColor(w.rarity) : ElarionUi.ParchmentDim,
                          new Vector2(0.06f, 0.10f), new Vector2(0.48f, 0.66f));

            // Armor slot.
            ArmorDef a = _loadout != null ? _loadout.EquippedArmor : null;
            EquipSlotTile(_paperDoll.transform, "ARMOR",
                          a != null ? RarityGlyph(a.rarity) + " " + a.name : "(none)",
                          a != null ? RarityColor(a.rarity) : ElarionUi.ParchmentDim,
                          new Vector2(0.52f, 0.10f), new Vector2(0.94f, 0.66f));
        }

        private void EquipSlotTile(Transform parent, string label, string value, Color valueColor,
                                   Vector2 min, Vector2 max)
        {
            var tile = AddImage(parent, "Slot_" + label, min, max, Cell);
            AddLabel(tile.transform, label, 0.66f, 0.94f, ElarionUi.ParchmentDim,
                     ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, spacing: 3f);
            AddLabel(tile.transform, value, 0.08f, 0.62f, valueColor,
                     ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f, bold: true);
        }

        // ====================================================================
        // TABS
        // ====================================================================
        private void BuildTabs(Transform panel)
        {
            string[] names = { "Weapons", "Armor", "Outfits", "Consumables" };
            Tab[] tabs = { Tab.Weapons, Tab.Armor, Tab.Outfits, Tab.Consumables };
            float y0 = 0.535f, y1 = 0.595f;
            float w = 0.225f, gap = 0.015f, x = 0.04f;
            for (int i = 0; i < names.Length; i++)
            {
                Tab t = tabs[i];
                bool sel = _tab == t;
                float cx = x + w * 0.5f;
                Color bg = sel ? new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.30f) : Glass;
                AddButton(panel, names[i], new Vector2(cx, w * 0.5f), new Vector2(y0, y1),
                          bg, () => SelectTab(t), sel ? ButtonKind.Gold : ButtonKind.Neutral);
                x += w + gap;
            }
        }

        private void SelectTab(Tab t)
        {
            if (_tab == t) return;
            _tab = t;
            ClearSelection();
            // Rebuild the tab strip (so the selected pill updates) by re-opening chrome cheaply:
            // simplest reliable path — rebuild whole UI's tab row + grid + sidebar.
            RebuildTabsRow();
            RebuildGrid();
            RebuildSidebar();
        }

        private void RebuildTabsRow()
        {
            if (_ui == null) return;
            var panel = _ui.transform.GetChild(_ui.transform.childCount - 1); // last child = main panel
            // Destroy existing tab buttons (named "Btn_Weapons" etc.) and rebuild.
            string[] names = { "Btn_Weapons", "Btn_Armor", "Btn_Outfits", "Btn_Consumables" };
            foreach (var n in names)
            {
                var existing = panel.Find(n);
                if (existing != null) Destroy(existing.gameObject);
            }
            BuildTabs(panel);
        }

        // ====================================================================
        // GRID (scrollable, 3 columns, large touch targets)
        // ====================================================================
        private void RebuildGrid()
        {
            if (_gridRoot == null) return;
            for (int i = _gridRoot.transform.childCount - 1; i >= 0; i--)
                Destroy(_gridRoot.transform.GetChild(i).gameObject);

            // A ScrollRect with a content holder + GridLayoutGroup for the cells.
            var viewport = AddImage(_gridRoot.transform, "Viewport",
                                    new Vector2(0.02f, 0.02f), new Vector2(0.98f, 0.98f),
                                    new Color(0, 0, 0, 0));
            var mask = viewport.AddComponent<RectMask2D>();
            var scroll = viewport.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;

            var content = new GameObject("Content", typeof(RectTransform));
            content.transform.SetParent(viewport.transform, false);
            var crt = content.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0f, 1f);
            crt.anchorMax = new Vector2(1f, 1f);
            crt.pivot = new Vector2(0.5f, 1f);
            crt.anchoredPosition = Vector2.zero;
            crt.sizeDelta = new Vector2(0f, 0f);
            scroll.content = crt;
            scroll.viewport = viewport.GetComponent<RectTransform>();

            var grid = content.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(196f, 132f);     // large touch targets
            grid.spacing = new Vector2(12f, 12f);
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 3;
            grid.childAlignment = TextAnchor.UpperCenter;

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            switch (_tab)
            {
                case Tab.Weapons:     BuildWeaponCells(content.transform); break;
                case Tab.Armor:       BuildArmorCells(content.transform); break;
                case Tab.Outfits:     BuildOutfitCells(content.transform); break;
                case Tab.Consumables: BuildConsumableCells(content.transform); break;
            }
        }

        private void BuildWeaponCells(Transform content)
        {
            string job = HeroJob;
            int level = HeroLevel();
            bool any = false;
            foreach (var w in GearCatalog.AllWeapons())
            {
                if (w == null || !JobEligible(w.job, job)) continue;   // TODO owned-list filter
                any = true;
                bool equipped = _loadout != null && _loadout.EquippedWeapon != null &&
                                string.Equals(_loadout.EquippedWeapon.id, w.id, System.StringComparison.OrdinalIgnoreCase);
                bool locked = w.req != null && level < w.req.level;
                var def = w;
                BuildGearCell(content, w.icon, w.name, w.rarity, equipped, locked,
                              locked ? "Lv " + w.req.level : "",
                              () => { _selWeapon = def; _selArmor = null; _selConsumable = null; RebuildSidebar(); });
            }
            if (!any) BuildEmptyNote(content, "No weapons for this class.");
        }

        private void BuildArmorCells(Transform content)
        {
            string job = HeroJob;
            int level = HeroLevel();
            bool any = false;
            foreach (var a in GearCatalog.AllArmors())
            {
                if (a == null || !JobEligible(a.job, job)) continue;   // TODO owned-list filter
                any = true;
                bool equipped = _loadout != null && _loadout.EquippedArmor != null &&
                                string.Equals(_loadout.EquippedArmor.id, a.id, System.StringComparison.OrdinalIgnoreCase);
                bool locked = a.req != null && level < a.req.level;
                var def = a;
                BuildGearCell(content, a.icon, a.name, a.rarity, equipped, locked,
                              locked ? "Lv " + a.req.level : "",
                              () => { _selArmor = def; _selWeapon = null; _selConsumable = null; RebuildSidebar(); });
            }
            if (!any) BuildEmptyNote(content, "No armor for this class yet.\n(armor.json may be empty)");
        }

        private void BuildOutfitCells(Transform content)
        {
            // TODO outfits — cosmetic outfit/skin slots are not yet a data catalog.
            // Cosmetics live in DeNelle.Cosmetics; when an owned-skins list exists, list
            // it here and equip via the cosmetics service. v1 = informative placeholder.
            BuildEmptyNote(content, "Outfits arrive with the cosmetics pass.\n(no owned skins yet)");
        }

        private void BuildConsumableCells(Transform content)
        {
            var owned = ItemInventory.OwnedConsumables();   // id -> count (persisted larder)
            if (owned == null || owned.Count == 0)
            {
                BuildEmptyNote(content, "No consumables.\nCraft potions at the Workshop.");
                return;
            }
            foreach (var kv in owned)
            {
                var def = ConsumableCatalog.Find(kv.Key);
                string name = def != null && !string.IsNullOrEmpty(def.DisplayName) ? def.DisplayName : kv.Key;
                string glyph = def != null && !string.IsNullOrEmpty(def.Glyph) ? def.Glyph : "!";
                var sel = new ConsumableSel { id = kv.Key, def = def, count = kv.Value };
                BuildGearCell(content, glyph, name + "  x" + kv.Value, "common", false, false, "",
                              () => { _selConsumable = sel; _selWeapon = null; _selArmor = null; RebuildSidebar(); });
            }
        }

        // A single grid cell: rarity-tinted border, icon, name, equipped/lock indicator.
        private void BuildGearCell(Transform content, string icon, string name, string rarity,
                                   bool equipped, bool locked, string lockText, System.Action onTap)
        {
            var cell = new GameObject("Cell", typeof(Image), typeof(Button));
            cell.transform.SetParent(content, false);
            var img = cell.GetComponent<Image>();
            img.color = locked ? new Color(Cell.r, Cell.g, Cell.b, 0.5f) : Cell;
            ApplyRounded(img);

            var btn = cell.GetComponent<Button>();
            btn.targetGraphic = img;
            StyleButtonColors(btn);
            if (onTap != null) btn.onClick.AddListener(() => onTap());

            // Rarity border (a thin frame tinted by rarity).
            var border = new GameObject("Border", typeof(Image));
            border.transform.SetParent(cell.transform, false);
            var brt = border.GetComponent<RectTransform>();
            brt.anchorMin = Vector2.zero; brt.anchorMax = Vector2.one;
            brt.offsetMin = new Vector2(-2f, -2f); brt.offsetMax = new Vector2(2f, 2f);
            var bimg = border.GetComponent<Image>();
            bimg.color = new Color(RarityColor(rarity).r, RarityColor(rarity).g, RarityColor(rarity).b, 0.7f);
            ApplyRounded(bimg);
            bimg.raycastTarget = false;
            border.transform.SetAsFirstSibling();   // behind the cell fill

            // Icon (emoji placeholder until art ships).
            AddLabel(cell.transform, string.IsNullOrEmpty(icon) ? "?" : icon, 0.42f, 0.92f,
                     ElarionUi.Parchment, ElarionUi.FontTitle, TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f);

            // Name (rarity-coloured).
            AddLabel(cell.transform, name, 0.10f, 0.40f, RarityColor(rarity),
                     ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);

            if (equipped)
            {
                var chip = AddImage(cell.transform, "Equipped", new Vector2(0.04f, 0.80f), new Vector2(0.34f, 0.97f),
                                    new Color(ElarionUi.Affordable.r, ElarionUi.Affordable.g, ElarionUi.Affordable.b, 0.92f));
                AddLabel(chip.transform, "✓ E", 0f, 1f, ElarionUi.Ink, ElarionUi.FontMicro,
                         TMPro.TextAlignmentOptions.Center, 0f, 1f, bold: true);
            }
            if (locked)
            {
                var chip = AddImage(cell.transform, "Locked", new Vector2(0.62f, 0.80f), new Vector2(0.97f, 0.97f),
                                    new Color(0f, 0f, 0f, 0.6f));
                AddLabel(chip.transform, "🔒 " + lockText, 0f, 1f, ElarionUi.ParchmentDim,
                         ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0f, 1f, bold: true);
            }
        }

        private void BuildEmptyNote(Transform content, string msg)
        {
            var note = new GameObject("Empty", typeof(RectTransform));
            note.transform.SetParent(content, false);
            // Span across the grid (LayoutElement keeps it from being a tiny cell).
            var le = note.AddComponent<LayoutElement>();
            le.preferredWidth = 600f; le.preferredHeight = 120f;
            AddLabel(note.transform, msg, 0f, 1f, ElarionUi.ParchmentDim, ElarionUi.FontLabel,
                     TMPro.TextAlignmentOptions.Center, 0f, 1f);
        }

        // ====================================================================
        // SIDEBAR (selected item stats + Equip/Unequip)
        // ====================================================================
        private void RebuildSidebar()
        {
            if (_sidebarRoot == null) return;
            for (int i = _sidebarRoot.transform.childCount - 1; i >= 0; i--)
                Destroy(_sidebarRoot.transform.GetChild(i).gameObject);

            if (_selWeapon == null && _selArmor == null && _selConsumable == null)
            {
                AddLabel(_sidebarRoot.transform, "Tap an item\nto view + equip.", 0.4f, 0.6f,
                         ElarionUi.ParchmentDim, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f);
                return;
            }

            AddLabel(_sidebarRoot.transform, "DETAILS", 0.92f, 0.98f, ElarionUi.ParchmentDim,
                     ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, spacing: 3f);
            AddRule(_sidebarRoot.transform, 0.905f, 0.06f, 0.94f);

            if (_selWeapon != null) BuildWeaponSidebar(_selWeapon);
            else if (_selArmor != null) BuildArmorSidebar(_selArmor);
            else if (_selConsumable != null) BuildConsumableSidebar(_selConsumable);
        }

        private void BuildWeaponSidebar(WeaponDef w)
        {
            int level = HeroLevel();
            bool locked = w.req != null && level < w.req.level;
            bool equipped = _loadout != null && _loadout.EquippedWeapon != null &&
                            string.Equals(_loadout.EquippedWeapon.id, w.id, System.StringComparison.OrdinalIgnoreCase);

            AddLabel(_sidebarRoot.transform, (string.IsNullOrEmpty(w.icon) ? "" : w.icon + "  ") + w.name,
                     0.80f, 0.89f, RarityColor(w.rarity), ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center,
                     0.05f, 0.95f, bold: true);
            AddLabel(_sidebarRoot.transform, Cap(w.rarity) + "  -  " + Cap(w.job), 0.74f, 0.79f,
                     ElarionUi.ParchmentDim, ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f);

            string stats = $"Damage  x{w.damageMult:0.0#}";
            if (w.reach > 0f) stats += $"\nReach   {w.reach:0.0} m";
            if (w.req != null && w.req.level > 1) stats += $"\nRequires Lv {w.req.level}";
            if (!string.IsNullOrEmpty(w.makersMark)) stats += $"\nMark: {w.makersMark}";
            AddLabel(_sidebarRoot.transform, stats, 0.42f, 0.72f, ElarionUi.Parchment,
                     ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f);

            if (!string.IsNullOrEmpty(w.flavor) || !string.IsNullOrEmpty(w.saga))
                AddLabel(_sidebarRoot.transform, "\"" + (w.flavor ?? w.saga) + "\"", 0.20f, 0.40f,
                         ElarionUi.ParchmentDim, ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f);

            BuildEquipButton(equipped, locked,
                () => { if (_loadout != null) _loadout.EquipWeaponById(w.id); },
                () => { /* weapons have no explicit unequip in v1 */ });
        }

        private void BuildArmorSidebar(ArmorDef a)
        {
            int level = HeroLevel();
            bool locked = a.req != null && level < a.req.level;
            bool equipped = _loadout != null && _loadout.EquippedArmor != null &&
                            string.Equals(_loadout.EquippedArmor.id, a.id, System.StringComparison.OrdinalIgnoreCase);

            AddLabel(_sidebarRoot.transform, (string.IsNullOrEmpty(a.icon) ? "" : a.icon + "  ") + a.name,
                     0.80f, 0.89f, RarityColor(a.rarity), ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center,
                     0.05f, 0.95f, bold: true);
            AddLabel(_sidebarRoot.transform, Cap(a.rarity) + "  -  " + Cap(a.job), 0.74f, 0.79f,
                     ElarionUi.ParchmentDim, ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f);

            string stats = $"Defense  {a.defense * 100f:0}%";
            if (a.hpBonus > 0f) stats += $"\nHP Bonus {a.hpBonus:0}";
            if (a.req != null && a.req.level > 1) stats += $"\nRequires Lv {a.req.level}";
            AddLabel(_sidebarRoot.transform, stats, 0.42f, 0.72f, ElarionUi.Parchment,
                     ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f);

            BuildEquipButton(equipped, locked,
                () => { if (_loadout != null) _loadout.EquipArmorById(a.id); },
                () => { /* no explicit unequip in v1 */ });
        }

        private void BuildConsumableSidebar(ConsumableSel c)
        {
            string name = c.def != null && !string.IsNullOrEmpty(c.def.DisplayName) ? c.def.DisplayName : c.id;
            AddLabel(_sidebarRoot.transform, name, 0.80f, 0.89f, ElarionUi.Parchment,
                     ElarionUi.FontBody, TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f, bold: true);
            AddLabel(_sidebarRoot.transform, "Owned: x" + c.count, 0.74f, 0.79f, ElarionUi.ParchmentDim,
                     ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f);

            if (c.def != null)
            {
                string stats = $"{Cap(c.def.EffectRaw)}  {c.def.Magnitude:0}";
                if (c.def.Duration > 0f) stats += $"\nDuration {c.def.Duration:0}s";
                stats += c.def.UsableInFight ? "\nUsable in combat" : "\nRest-only";
                AddLabel(_sidebarRoot.transform, stats, 0.44f, 0.72f, ElarionUi.Parchment,
                         ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f);
            }

            // Consumables aren't "equipped" — v1 surfaces them read-only here.
            // TODO use-consumable — wire to the use-service when its public entry is settled.
            AddLabel(_sidebarRoot.transform, "(use from the combat hotbar)", 0.20f, 0.30f,
                     ElarionUi.ParchmentDim, ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f);
        }

        private void BuildEquipButton(bool equipped, bool locked, System.Action equip, System.Action unequip)
        {
            string label;
            Color color;
            ButtonKind kind;
            System.Action action;

            if (locked)
            {
                label = "LOCKED"; color = new Color(ElarionUi.Danger.r, ElarionUi.Danger.g, ElarionUi.Danger.b, 0.4f);
                kind = ButtonKind.Danger; action = null;
            }
            else if (equipped)
            {
                label = "EQUIPPED ✓";
                color = new Color(ElarionUi.Affordable.r, ElarionUi.Affordable.g, ElarionUi.Affordable.b, 0.5f);
                kind = ButtonKind.Confirm; action = null;   // already worn
            }
            else
            {
                label = "EQUIP"; color = ElarionUi.GoldButton; kind = ButtonKind.Gold; action = equip;
            }

            var btn = AddButton(_sidebarRoot.transform, label, new Vector2(0.5f, 0.40f),
                                new Vector2(0.04f, 0.16f), color, action, kind);
            btn.interactable = action != null;
        }

        // ====================================================================
        // SELECTION
        // ====================================================================
        private void ClearSelection()
        {
            _selWeapon = null; _selArmor = null; _selConsumable = null;
        }

        private sealed class ConsumableSel
        {
            public string id;
            public ConsumableDef def;
            public int count;
        }

        // ====================================================================
        // HELPERS — hero data
        // ====================================================================
        private int HeroLevel()
        {
            var prog = _loadout != null ? _loadout.GetComponent<HeroProgression>() : null;
            return prog != null ? prog.Level : 1;
        }

        private static bool JobEligible(string itemJob, string heroJob)
        {
            if (string.IsNullOrEmpty(itemJob)) return true;
            if (itemJob.Equals("any", System.StringComparison.OrdinalIgnoreCase)) return true;
            return itemJob.Equals(heroJob ?? string.Empty, System.StringComparison.OrdinalIgnoreCase);
        }

        // ====================================================================
        // HELPERS — rarity
        // ====================================================================
        private static Color RarityColor(string rarity)
        {
            switch ((rarity ?? "common").ToLowerInvariant())
            {
                case "uncommon":  return new Color(0.46f, 0.74f, 0.42f, 1f);   // green
                case "rare":      return new Color(0.32f, 0.58f, 0.92f, 1f);   // blue
                case "epic":      return new Color(0.66f, 0.42f, 0.86f, 1f);   // purple
                case "legendary": return new Color(0.92f, 0.62f, 0.24f, 1f);   // orange
                default:          return new Color(0.80f, 0.80f, 0.78f, 1f);   // common grey
            }
        }

        private static string RarityGlyph(string rarity)
        {
            switch ((rarity ?? "common").ToLowerInvariant())
            {
                case "legendary": return "★";
                case "epic":      return "◆";
                case "rare":      return "◈";
                default:          return "";
            }
        }

        private static string Cap(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        // ====================================================================
        // SLEEK uGUI helpers (mirrored from ArenaPanel.cs)
        // ====================================================================
        private GameObject AddPanel(Transform parent, Vector2 min, Vector2 max, bool deep = false)
        {
            var p = AddImage(parent, "Panel", min, max, deep ? GlassDeep : Glass);
            AddRimUnderline(p);
            return p;
        }

        private static GameObject AddImage(Transform parent, string name, Vector2 min, Vector2 max,
            Color color, bool rounded = true)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = min; r.anchorMax = max;
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            if (rounded) ApplyRounded(img);
            return go;
        }

        private static void ApplyRounded(Image img)
        {
            var sprite = RoundedSprite;
            if (sprite != null) { img.sprite = sprite; img.type = Image.Type.Sliced; }
        }

        private void AddRule(Transform parent, float y, float x0, float x1)
        {
            var go = new GameObject("Rule", typeof(Image));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(x0, y); r.anchorMax = new Vector2(x1, y);
            r.offsetMin = new Vector2(0f, -1f); r.offsetMax = new Vector2(0f, 1f);
            var img = go.GetComponent<Image>();
            img.color = Accent;
            img.raycastTarget = false;
        }

        private void AddRimUnderline(GameObject panel)
        {
            var go = new GameObject("Accent", typeof(Image));
            go.transform.SetParent(panel.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(0.06f, 0f);
            rt.anchorMax = new Vector2(0.94f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.sizeDelta = new Vector2(0f, 1.5f);
            rt.anchoredPosition = new Vector2(0f, 1.5f);
            var img = go.GetComponent<Image>();
            img.color = AccentSoft;
            img.raycastTarget = false;
            go.transform.SetAsLastSibling();
        }

        private static TMPro.TextMeshProUGUI AddLabel(Transform parent, string text, float y0, float y1,
            Color color, int size, TMPro.TextAlignmentOptions align,
            float x0 = 0.03f, float x1 = 0.97f, float spacing = 0f, bool bold = false)
        {
            var go = new GameObject("Label", typeof(TMPro.TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1);
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            var t = go.GetComponent<TMPro.TextMeshProUGUI>();
            t.text = text;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.characterSpacing = spacing;
            t.raycastTarget = false;
            if (bold) t.fontStyle = TMPro.FontStyles.Bold;
            return t;
        }

        private enum ButtonKind { Gold, Neutral, Confirm, Danger }

        // anchorX = (centerX, halfWidth); anchorY = (y0, y1) of the button rect.
        private Button AddButton(Transform parent, string label, Vector2 anchorX, Vector2 anchorY,
            Color bg, System.Action onClick, ButtonKind kind)
        {
            var go = new GameObject("Btn_" + label, typeof(Image), typeof(Button));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(anchorX.x - anchorX.y, anchorY.x);
            r.anchorMax = new Vector2(anchorX.x + anchorX.y, anchorY.y);
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;

            var img = go.GetComponent<Image>();
            img.color = bg;
            ApplyRounded(img);

            var btn = go.GetComponent<Button>();
            btn.targetGraphic = img;
            StyleButtonColors(btn);
            if (onClick != null) btn.onClick.AddListener(() => onClick());

            Color textColor = kind == ButtonKind.Gold ? ElarionUi.Ink : ElarionUi.Parchment;
            var tt = AddLabel(go.transform, label, 0f, 1f, textColor, ElarionUi.FontBody,
                              TMPro.TextAlignmentOptions.Center, 0f, 1f, spacing: 1f, bold: true);
            tt.raycastTarget = false;
            return btn;
        }

        private static void StyleButtonColors(Button button)
        {
            if (button == null) return;
            button.transition = Selectable.Transition.ColorTint;
            var cb = button.colors;
            cb.normalColor      = Color.white;
            cb.highlightedColor = new Color(1.10f, 1.10f, 1.10f, 1f);
            cb.pressedColor     = new Color(0.82f, 0.82f, 0.82f, 1f);
            cb.selectedColor    = cb.highlightedColor;
            cb.disabledColor    = new Color(0.5f, 0.5f, 0.5f, 0.5f);
            cb.colorMultiplier  = 1f;
            cb.fadeDuration     = 0.07f;
            button.colors = cb;
        }

        // ── Procedural rounded sprite (lazily built once; WebGL failure-safe) ──
        private static Sprite _rounded;
        private static bool _roundedTried;
        private static Sprite RoundedSprite
        {
            get
            {
                if (!_roundedTried)
                {
                    _roundedTried = true;
                    try { _rounded = BuildRoundedSprite(); }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning("[HeroInventoryController] rounded sprite build failed (flat quad): " + e.Message);
                        _rounded = null;
                    }
                }
                return _rounded;
            }
        }

        private static Sprite BuildRoundedSprite()
        {
            const int size = 32;
            const int radius = 6;
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedRectDistance(x, y, size, size, radius);
                    byte a = (byte)Mathf.Clamp((int)((1f - d) * 255f), 0, 255);
                    px[y * size + x] = new Color32(255, 255, 255, a);
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
                                 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
        }

        private static float RoundedRectDistance(int x, int y, int w, int h, int radius)
        {
            float fx = x + 0.5f, fy = y + 0.5f;
            float dx = Mathf.Max(Mathf.Max(radius - fx, fx - (w - radius)), 0f);
            float dy = Mathf.Max(Mathf.Max(radius - fy, fy - (h - radius)), 0f);
            float dist = Mathf.Sqrt(dx * dx + dy * dy) - radius;
            return Mathf.Clamp01(dist + 0.5f);
        }
    }
}
