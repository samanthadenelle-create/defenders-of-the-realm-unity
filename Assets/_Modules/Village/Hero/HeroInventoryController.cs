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
        private GameObject _tabsRoot;     // tab row host (rebuilt on tab change)
        private Sprite _profileFrameSprite; // the gold sunburst PORTRAIT MEDALLION (profile_frame); null = procedural fallback
        private Tab _tab = Tab.Weapons;  // Default to weapons + armor focus for this inventory view.

        // The current selection (one of these is non-null while a cell is selected).
        private WeaponDef _selWeapon;
        private ArmorDef _selArmor;
        private ConsumableSel _selConsumable;

        private GearLoadout _loadout;     // the live hero's gear model (drives the hero)

        // DEF-212 single-modal arbiter. The inventory is a full-screen, click-eating
        // modal exactly like HelpMenu / AdminOverlay / CosmeticShop; without this it
        // could stack over an open Help menu (and vice-versa) in MainCastle_Hall —
        // the same gap ModalPanelDisciplineTests pins for the other panels.
        private DeNelle.Core.UI.PanelHandle _panelHandle;

        // ── DARK-GLASS palette — SOURCED from the shared presentation layer ───────
        // This screen now reads in the SAME dark glass + gold-rune language as the
        // town HUD / store / combat HUD. The role names (Glass/GlassDeep/Cell/etc.)
        // are kept so the layout code below is untouched — only the VALUES route to
        // the canonical ElarionUiKit / ElarionUi tones. Text routes through the
        // cream Parchment tones (readable on dark glass), headings through Gilt.
        //
        // Panel + surface fills (the consolidated dark-glass tints from the kit).
        private static readonly Color Glass      = ElarionUiKit.Glass;
        private static readonly Color GlassDeep  = ElarionUiKit.GlassDeep;
        private static readonly Color Track      = ElarionUiKit.Track;
        private static readonly Color Cell       = ElarionUiKit.Cell;
        private static readonly Color CellSel    = ElarionUiKit.CellSelected;
        // Gilt frame accents (thin gold rims on the dark glass).
        private static readonly Color AccentSoft = ElarionUiKit.AccentSoft;
        private static readonly Color Accent     = ElarionUiKit.Accent;
        // Paper-doll "niche": the warm stone alcove the hero stands in.
        private static readonly Color StoneBack  = ElarionUiKit.StoneNiche;
        private static readonly Color StoneNiche = ElarionUiKit.StoneNiche;
        // Aether tint — a faint violet bloom over the dark ground.
        private static readonly Color AetherSoft = new Color(ElarionUi.Aether.r, ElarionUi.Aether.g, ElarionUi.Aether.b, 0.16f);

        // ── TEXT tones — cream parchment on the dark glass (readable), gilt for
        // headings. These restore the canonical ElarionUi text language (the role
        // names Ink/InkDim/InkMicro/GiltInk are kept so call sites are untouched).
        private static readonly Color Ink        = ElarionUi.Parchment;                       // primary text on dark glass
        private static readonly Color InkDim      = ElarionUi.ParchmentDim;                   // secondary / flavour
        private static readonly Color InkMicro    = new Color(ElarionUi.ParchmentDim.r, ElarionUi.ParchmentDim.g, ElarionUi.ParchmentDim.b, 0.85f); // micro caps / hints
        // Headings read as warm gilt on the dark ground.
        private static readonly Color GiltInk     = ElarionUi.Gilt;                            // gilt heading

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
        // HARDENING (owner: "opens nothing"): EVERY stage is now isolated so a throw in
        // one stage can never leave the player with a blank screen + a single vague log.
        // The two stages that used to ride the broad outer try/catch — ResolveLoadout()
        // and BuildRoot() — are now individually guarded and emit a SPECIFIC message
        // naming the stage and the live hero/loadout state. A half-built root (BuildRoot
        // threw partway, leaving a non-null-but-broken _ui) is detected and torn down so
        // the NEXT Open() rebuilds from scratch instead of re-activating garbage.
        public void Open()
        {
            // 1) Resolve the live hero's loadout. A null loadout is NOT fatal — the modal
            //    still opens (paper-doll falls back to the default starter armor display),
            //    so a missing/just-spawned hero never produces "opens nothing".
            SafeRun(ResolveLoadout, "ResolveLoadout");

            // 2) Build the chrome. If this throws, tear down any partial root so it can't
            //    be re-activated broken on the next Open(), and bail with a loud, specific
            //    message — this is the most likely real "opens nothing" culprit.
            if (_ui == null)
            {
                try { BuildRoot(); }
                catch (System.Exception e)
                {
                    Debug.LogError("[HeroInventoryController] BuildRoot FAILED — inventory could not open. "
                                   + DescribeState() + "\n" + e);
                    if (_ui != null) { Destroy(_ui); _ui = null; }
                    _gridRoot = _sidebarRoot = _paperDoll = _tabsRoot = null;
                    return;
                }
            }
            if (_ui == null)
            {
                Debug.LogError("[HeroInventoryController] BuildRoot produced no UI (root is null) — "
                               + "inventory has nothing to show. " + DescribeState());
                return;
            }

            _ui.SetActive(true);

            // 3) Modal arbiter registration (isolated: a PanelManager hiccup must not blank
            //    the already-built, already-active modal).
            SafeRun(() =>
            {
                if (_panelHandle == null)
                    _panelHandle = DeNelle.Core.UI.PanelManager.Register("Inventory", Close, () => IsOpen);
                DeNelle.Core.UI.PanelManager.NotifyOpened(_panelHandle);
            }, "PanelManager.Register/NotifyOpened");

            SafeRun(Subscribe, "Subscribe");
            _tab = Tab.Weapons;
            ClearSelection();

            // 4) Each content section is isolated so a failure in one (e.g. a single bad
            //    catalog row) leaves the rest of the modal rendered, not blank.
            SafeRun(RebuildPaperDoll, "RebuildPaperDoll");
            SafeRun(RebuildGrid,      "RebuildGrid");
            SafeRun(RebuildSidebar,   "RebuildSidebar");

            // A loud, single success line so the next playtest console PROVES the modal
            // built + activated at the top-most sort order (vs. the old silent "nothing").
            var c = _ui != null ? _ui.GetComponent<Canvas>() : null;
            Debug.Log("[HeroInventoryController] Open() complete — modal active="
                      + (_ui != null && _ui.activeInHierarchy)
                      + " sort=" + (c != null ? c.sortingOrder : -1)
                      + " " + DescribeState());
        }

        // A one-line snapshot of the live hero/data state, appended to failure logs so the
        // exact open-time condition (no hero? no loadout? empty catalog?) is obvious in the
        // console on the next playtest — the goal: never silently "open nothing" again.
        private string DescribeState()
        {
            string hero;
            try { hero = GameObject.FindWithTag("Player") != null ? "Player-found" : "Player-MISSING"; }
            catch { hero = "Player-tag-error"; }
            int weapons = 0, armors = 0;
            try { weapons = GearCatalog.AllWeapons().Count; armors = GearCatalog.AllArmors().Count; }
            catch { /* catalog read failed — reported as 0 below */ }
            string job = "?";
            try { job = HeroJob; } catch { /* loadout/abilities not ready */ }
            return "[state hero=" + hero
                   + " loadout=" + (_loadout != null ? "present" : "NULL")
                   + " equippedArmor=" + (_loadout != null && _loadout.EquippedArmor != null ? _loadout.EquippedArmor.id : "none")
                   + " job=" + job
                   + " catalog(weapons=" + weapons + ",armor=" + armors + ")]";
        }

        // Runs a UI-rebuild step, swallowing+logging any exception so one bad
        // section can't blank the whole inventory (WebGL hardening). The log now names
        // the failing section AND the live hero/data state so the exact open-time failure
        // point is obvious in the console on the next playtest.
        private void SafeRun(System.Action step, string label)
        {
            try { step(); }
            catch (System.Exception e)
            {
                Debug.LogError("[HeroInventoryController] " + label + " FAILED (rest of inventory still shown). "
                               + DescribeState() + "\n" + e);
            }
        }

        /// <summary>Tear the overlay down (keeps the controller alive for re-open).</summary>
        public void Close()
        {
            Unsubscribe();
            if (_ui != null) Destroy(_ui);
            _ui = null;
            _gridRoot = _sidebarRoot = _paperDoll = _tabsRoot = null;
            ClearSelection();
            // Release the modal slot so no invisible backdrop lingers / traps input.
            if (_panelHandle != null) DeNelle.Core.UI.PanelManager.NotifyClosed(_panelHandle);
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
            SafeRun(RebuildPaperDoll, "RebuildPaperDoll");
            SafeRun(RebuildGrid,      "RebuildGrid");      // refresh equipped indicators
            SafeRun(RebuildSidebar,   "RebuildSidebar");   // refresh Equip/Unequip button state
        }

        // ====================================================================
        // ROOT + CHROME
        // ====================================================================
        private void BuildRoot()
        {
            _ui = new GameObject("HeroInventoryUI");

            var canvas = _ui.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            // ROOT-CAUSE (T-022 "no inventory" / opens-nothing): the modal used to ride
            // sortingOrder 2600 — the stale comment ("above HUD + Arena 1100") predates a
            // whole BAND of always-on world-HUD canvases that were added LATER and sit far
            // ABOVE 2600: VirtualJoystick + CampPromptUI (30000), MobileInteractButton
            // (30050), NodeDiscovery / GateIntelHud (29000). In MainCastle_Hall those are
            // live the moment the hub loads, so the inventory built fine but rendered
            // UNDERNEATH the full-screen joystick/interact canvases — the player tapped BAG
            // and "saw nothing" because the modal was buried under the world HUD. We now
            // sit ABOVE that world-HUD band but BELOW the true top overlays (GameOver /
            // VillageLoadOverlay = 32760) so a load fade or game-over still wins.
            canvas.sortingOrder = 31000;
            canvas.overrideSorting = true;              // defensively pin our order vs parent canvases

            var scaler = _ui.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _ui.AddComponent<GraphicRaycaster>();

            // Full-screen dark dim that blocks click-through + tap-to-close, built by
            // the shared kit so the backdrop matches every other modal in the game.
            ElarionUiKit.Scrim(_ui.transform, Close);

            // Near-black backdrop (mirrors ShopPanel) so the world behind vanishes and the
            // inventory reads as its own premium space, NOT light parchment. Visual-only
            // (raycast off) so the scrim below still owns tap-to-close.
            var backdrop = AddImage(_ui.transform, "InvBackdrop", Vector2.zero, Vector2.one,
                                    new Color(0.02f, 0.015f, 0.012f, 0.94f));
            NoRaycast(backdrop);

            // ── DEPTH: a thin gilt backboard halo just behind the panel, so the modal
            // reads as a framed, lifted sheet rather than a flat fill. ──
            AddImage(_ui.transform, "Backboard", new Vector2(0.025f, 0.018f), new Vector2(0.975f, 0.982f),
                     new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.35f));

            // The main panel fills most of the screen (mobile-first) — dressed SPRITE-FIRST
            // with the D3 dark-WOOD vendor board (RolePanel/panel_vendor) so the inventory
            // reads as the SAME designed game as the shop we just shipped. When the pack is
            // absent it stays the procedural glass+gold-rim panel (no regress).
            var panel = ElarionUiKit.PanelFramed(_ui.transform, new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.97f),
                                                 deep: true, packSpriteName: RpgUiCatalog.PanelVendor);

            // Solid heavy dark fill inside the frame so it reads premium, not see-through
            // (inset so the carved wood border still shows). Same recipe as ShopPanel; drawn
            // first so the header/tabs/grid/paper-doll all sit on top of it.
            var solidFill = AddImage(panel.transform, "InvSolidFill",
                                     new Vector2(0.025f, 0.02f), new Vector2(0.975f, 0.98f),
                                     new Color(0.08f, 0.06f, 0.045f, 0.985f));
            NoRaycast(solidFill);
            solidFill.transform.SetAsFirstSibling();

            // ── MOCKUP #41 LAYOUT (WO-400) ──────────────────────────────────────
            //   [ rune strip ............................................... ]  top
            //   [ ✦ INVENTORY ...................................... [X] ]      header
            //   [ Weapons | Armor | Accessories | Consumables ]               tab row
            //   ┌── LEFT 28% ──┐ ┌──────── RIGHT 70% ────────┐
            //   │ hero name    │ │ scrollable item grid       │
            //   │  ╭ rune ╮    │ │ (4-5 cols, rune frames)    │              main row
            //   │ ◯ ring  ◯   │ │                            │
            //   │  ╰ hero ╯    │ ├─ detail / equip strip ─────┤
            //   └──────────────┘ └────────────────────────────┘
            //   [ Sort  Filter ............... ● Gold  ◆ Cryst  ◈ SKR ]       footer
            // The signature look = the equipped slots arranged in a RING around the
            // hero medallion (see RebuildPaperDoll), vs the old vertical columns.
            // ────────────────────────────────────────────────────────────────────

            // A faint rune strip across the very top edge — a HINT of Elarion magic.
            AddRuneStrip(panel.transform, 0.965f, 0.992f);

            // Header: a gold-rune tech-pack banner (panel_tab) carries the bronze crest +
            // title, so the heading reads as the SAME ornate gilt header as the town HUD.
            // The banner is a sprite-first, NON-RAYCAST plate behind the gilt title (no-op
            // procedural-clean when the pack is absent).
            var headerBanner = AddImage(panel.transform, "HeaderBanner",
                                        new Vector2(0.04f, 0.910f), new Vector2(0.84f, 0.965f),
                                        new Color(0, 0, 0, 0));
            NoRaycast(headerBanner);
            DressPanel(headerBanner, RpgUiCatalog.PanelTab, keepWhite: true);
            AddLabelShadow(panel.transform, ElarionUi.CrestGlyph + "  INVENTORY", 0.918f, 0.958f,
                           GiltInk, ElarionUi.FontTitle, 0.05f, 0.80f, spacing: 6f);
            AddRule(panel.transform, 0.908f, 0.04f, 0.96f);

            // Close X — top-right, dressed sprite-first with the tech-pack gold button frame.
            var closeBtn = AddButton(panel.transform, "X", new Vector2(0.92f, 0.035f), new Vector2(0.916f, 0.962f),
                      new Color(0.847f, 0.804f, 0.710f, 1f), Close, ButtonKind.Neutral);
            DressButtonPack(closeBtn);

            // Tabs row (top, under the header) — host so the active pill can be rebuilt.
            _tabsRoot = AddImage(panel.transform, "TabsRow",
                                 new Vector2(0.04f, 0.838f), new Vector2(0.96f, 0.898f), new Color(0, 0, 0, 0));
            NoRaycast(_tabsRoot);
            BuildTabs(_tabsRoot.transform);

            // ── LEFT column (~28%): the character paper-doll. A recessed alcove the hero
            // "stands" in (the procedural Niche rim is the always-present backing). The TOP
            // band is dressed sprite-FIRST with the gold sunburst PORTRAIT MEDALLION
            // (profile_frame): the hero crest in the left sunburst circle + name & HP/MP
            // bars in the right slots (see RebuildPaperDoll). When the pack art is absent the
            // column degrades cleanly to the procedural niche + portrait disc (no regress). ──
            var niche = ElarionUiKit.Niche(panel.transform,
                                           new Vector2(0.04f, 0.115f), new Vector2(0.335f, 0.822f));
            _paperDoll = AddImage(niche.transform, "PaperDollArea",
                                  new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.98f), new Color(0, 0, 0, 0));
            NoRaycast(_paperDoll);
            // The medallion sprite for the top band — cached so RebuildPaperDoll seats the
            // portrait/name/bars into the frame's regions when present.
            _profileFrameSprite = RpgUiCatalog.Get(RpgUiCatalog.RolePanel, RpgUiCatalog.PanelProfile);

            // ── RIGHT region (~70%, mockup): the scrollable item grid DOMINATES (top),
            // with a compact horizontal DETAIL / EQUIP strip beneath it. The grid is the
            // mockup's "center/right ~70% item grid (4-5 cols)"; the strip carries the
            // selected-item icon + stats + the EQUIP CTA laid out left-to-right. ──
            _gridRoot = ElarionUiKit.Well(panel.transform,
                                          new Vector2(0.355f, 0.305f), new Vector2(0.96f, 0.822f));
            // Dress the grid tray with the tech-pack's ornate inventory panel frame.
            DressPanel(_gridRoot, RpgUiCatalog.PanelInventory, keepWhite: true);
            _sidebarRoot = ElarionUiKit.Panel(panel.transform,
                                              new Vector2(0.355f, 0.115f), new Vector2(0.96f, 0.293f),
                                              deep: true, innerRim: true);
            // The selected-item detail/equip strip reads as the ornate "Model selection"
            // wood PORTRAIT frame (panel_portrait) — the same detail surface the shop uses,
            // so the inspected item sits in a matching carved frame.
            DressPanel(_sidebarRoot, RpgUiCatalog.PanelPortrait, keepWhite: true);

            // ── Footer bar: Sort / Filter on the left, resource wells on the right. ──
            BuildFooterBar(panel.transform);
        }

        // ── Footer bar (mockup #41 bottom): Sort / Filter chips on the LEFT, the
        // Gold + Crystals + SKR resource wells on the RIGHT. The Sort/Filter chips are
        // visual affordances for the catalog (no owned-list to re-sort yet — TODO when
        // a real ownership list lands they re-order the grid); kept here so the footer
        // matches the mockup. Resource wells read live GameState (Gold/Crystals); SKR
        // is the Web3 purse, surfaced as a styled badge (its balance lives in the
        // DeNelle.Wallet stack which this assembly does not reference — so it shows the
        // rail glyph, not a live figure, to avoid a cross-assembly/async dependency).
        private void BuildFooterBar(Transform panel)
        {
            // A thin recessed footer tray spanning the panel bottom.
            var tray = AddImage(panel, "FooterTray",
                                new Vector2(0.04f, 0.035f), new Vector2(0.96f, 0.100f), Track);
            AddInnerRim(tray, AccentSoft);

            // LEFT: Sort + Filter chips (parchment chips with dark-ink labels).
            Color chip = new Color(0.847f, 0.804f, 0.710f, 1f);
            DressButtonPack(AddButton(tray.transform, "Sort", new Vector2(0.115f, 0.085f), new Vector2(0.18f, 0.82f),
                      chip, () => { /* TODO owned-list re-sort */ }, ButtonKind.Neutral));
            DressButtonPack(AddButton(tray.transform, "Filter", new Vector2(0.305f, 0.085f), new Vector2(0.18f, 0.82f),
                      chip, () => { /* TODO owned-list filter */ }, ButtonKind.Neutral));

            // RIGHT: resource wells.
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

            ResourceWell(tray.transform, "GoldWell", 0.470f, 0.640f, "o " + coins, "GOLD", GiltInk);
            ResourceWell(tray.transform, "CrystalWell", 0.650f, 0.820f, "* " + crystals, "CRYSTALS",
                         new Color(0.42f, 0.26f, 0.62f, 1f));
            // SKR = Web3 rail; show the rail glyph (no live figure from this assembly).
            ResourceWell(tray.transform, "SkrWell", 0.830f, 0.985f, "* SKR", "WALLET",
                         new Color(0.18f, 0.43f, 0.40f, 1f));
        }

        // A single footer currency well (icon+value on top, micro caps label below),
        // anchored within the footer tray by [x0..x1].
        private void ResourceWell(Transform tray, string name, float x0, float x1,
                                  string value, string caps, Color valueColor)
        {
            var well = AddImage(tray, name, new Vector2(x0, 0.10f), new Vector2(x1, 0.90f), GlassDeep);
            AddInnerRim(well, AccentSoft);
            AddLabel(well.transform, value, 0.40f, 0.98f, valueColor,
                     ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);
            AddLabel(well.transform, caps, 0.04f, 0.42f, InkMicro,
                     ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, spacing: 2f);
        }

        // ── Paper-doll: a CLEAN, READABLE equipment column (T-022 "this layout is awful").
        // The previous look was a gimmicky six-socket RING with four loud red "Locked"
        // cosmetic placeholders that read as broken. This is the standard RPG paperdoll:
        //   [ hero name + class • LV ]            banner
        //   [   ◯ class-crest medallion   ]       portrait niche (RawImage-ready)
        //   ── EQUIPMENT ──                        section caption
        //   [ ⬚ WEAPON  | item name ]             live slot row (GearLoadout)
        //   [ ⬚ ARMOR   | item name ]             live slot row (GearLoadout)
        //   [ ⬚ HELM    | Empty     ]             quiet empty slots (cosmetics later)
        //   [ ⬚ TRINKET | Empty     ]
        // Two slots are LIVE (WEAPON + ARMOR, driven by GearLoadout / ResolveDisplayArmor);
        // the rest are tidy EMPTY slots (no alarming red), so the column always reads full
        // without pretending to be broken. When cosmetics/accessories land they fill the
        // empty rows in place.
        //
        // TODO live 3D preview — a RenderTexture camera on the hero would drop a rotating
        // model into the central medallion (sized + centred so a RawImage replaces it 1:1).
        // Deferred: it must not touch the live scene camera / in-world hero rendering.
        private void RebuildPaperDoll()
        {
            if (_paperDoll == null) return;
            for (int i = _paperDoll.transform.childCount - 1; i >= 0; i--)
                Destroy(_paperDoll.transform.GetChild(i).gameObject);

            string job = HeroJob;
            int level = HeroLevel();

            if (_profileFrameSprite != null)
            {
                // ── GOLD SUNBURST PORTRAIT MEDALLION (profile_frame) across the TOP band ──
                // The 758x396 frame: circular gold sunburst portrait socket on the LEFT,
                // name + two horizontal bar slots on the RIGHT. We seat the hero crest in the
                // left circle, the name on the right-top, and drive the two bar slots as HP/MP.
                var medBand = AddImage(_paperDoll.transform, "ProfileMedallion",
                                       new Vector2(0.0f, 0.610f), new Vector2(1.0f, 0.995f), Color.white);
                var mbImg = medBand.GetComponent<Image>();
                if (mbImg != null)
                {
                    mbImg.sprite = _profileFrameSprite; mbImg.type = Image.Type.Simple;
                    mbImg.color = Color.white; mbImg.preserveAspect = true; mbImg.raycastTarget = false;
                }

                // Hero crest in the LEFT sunburst circle (no portrait art yet — the class
                // crest reads as the hero token, same as the procedural disc below).
                AddLabel(medBand.transform, ClassCrest(job), 0.18f, 0.82f, GiltInk,
                         ElarionUi.FontTitle + 16, TMPro.TextAlignmentOptions.Center, 0.02f, 0.42f, bold: true);

                // Hero name + class/level on the RIGHT-top.
                AddLabel(medBand.transform, HeroDisplayName(job), 0.62f, 0.92f, Ink,
                         ElarionUi.FontHead, TMPro.TextAlignmentOptions.Left, 0.46f, 0.98f, spacing: 1f);
                AddLabel(medBand.transform, Cap(job).ToUpperInvariant() + "  LV " + level, 0.50f, 0.62f,
                         InkMicro, ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Left, 0.46f, 0.98f, spacing: 2f);

                // TWO bar slots on the RIGHT — HP (red) over MP (blue). Shown full (no live
                // HP/MP feed in this assembly); the fills sit in the frame's bar windows.
                PaperDollBar("HP", 0.30f, 0.46f, RpgUiCatalog.BarFrameRed, RpgUiCatalog.BarFillRed,
                             new Color(0.62f, 0.16f, 0.14f, 1f), medBand.transform);
                PaperDollBar("MP", 0.10f, 0.26f, RpgUiCatalog.BarFrameBlue, RpgUiCatalog.BarFillBlue,
                             new Color(0.18f, 0.33f, 0.62f, 1f), medBand.transform);
            }
            else
            {
                // ── Procedural fallback (pack art absent): name banner + circular portrait
                // disc — the original tidy look, untouched, so a null sprite never blanks it.
                AddLabelShadow(_paperDoll.transform, HeroDisplayName(job), 0.945f, 0.995f,
                               Ink, ElarionUi.FontHead, 0.02f, 0.98f, spacing: 1f);
                AddLabel(_paperDoll.transform, Cap(job).ToUpperInvariant() + "   •   LV " + level, 0.910f, 0.945f,
                         InkMicro, ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.02f, 0.98f, spacing: 3f);

                var portraitHost = AddImage(_paperDoll.transform, "PortraitHost",
                                            new Vector2(0.14f, 0.605f), new Vector2(0.86f, 0.895f), new Color(0, 0, 0, 0));
                NoRaycast(portraitHost);
                var disc = AddCircle(portraitHost.transform, "PortraitDisc", 0.5f, 0.5f, 0.50f, StoneNiche);
                NoRaycast(disc);
                AddCircleRim(disc, new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.55f));
                var medallion = AddCircle(disc.transform, "Medallion", 0.5f, 0.5f, 0.78f, StoneBack);
                NoRaycast(medallion);
                AddCircleRim(medallion, Accent);
                AddLabel(medallion.transform, ClassCrest(job), 0.20f, 0.86f, GiltInk,
                         ElarionUi.FontTitle + 18, TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f, bold: true);
                AddLabel(medallion.transform, Cap(job).ToUpperInvariant(), 0.06f, 0.20f, InkMicro,
                         ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, spacing: 2f);
            }

            // ── EQUIPMENT section caption + rule. ──
            AddLabel(_paperDoll.transform, "EQUIPMENT", 0.555f, 0.595f, GiltInk,
                     ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f, spacing: 4f);
            AddRule(_paperDoll.transform, 0.550f, 0.06f, 0.94f);

            // ── The equipment slot column — four rows, top to bottom. ──
            WeaponDef w = _loadout != null ? _loadout.EquippedWeapon : null;
            ArmorDef  a = ResolveDisplayArmor();

            // Rows stack from y 0.530 downward, each ~0.115 tall with a small gap.
            const float rowH = 0.115f, gap = 0.018f;
            float top = 0.530f;
            PaperDollRow(0, top, rowH, gap, "WEAPON",
                         w != null ? WeaponTypeGlyph(w) : "", w != null ? ItemIconCatalog.ForWeapon(w) : null,
                         w != null ? w.name : "Empty", w != null ? w.rarity : null, w != null);
            PaperDollRow(1, top, rowH, gap, "ARMOR",
                         a != null ? ArmorTypeGlyph(a) : "", a != null ? ItemIconCatalog.ForArmor(a) : null,
                         a != null ? a.name : "Empty", a != null ? a.rarity : null, a != null);
            PaperDollRow(2, top, rowH, gap, "HELM",    "", null, "Empty", null, false);
            PaperDollRow(3, top, rowH, gap, "TRINKET", "", null, "Empty", null, false);
        }

        // One horizontal HP/MP bar slot in the profile medallion's right column. Sprite-
        // first frame + fill from the RPG pack's bars role; procedural tinted fallback when
        // absent. Shown full (no live HP/MP feed in this assembly). Non-raycast (decorative).
        private void PaperDollBar(string caps, float y0, float y1, string frameSprite, string fillSprite,
                                  Color fallbackFill, Transform host)
        {
            const float x0 = 0.50f, x1 = 0.97f;
            var frameGo = AddImage(host, "Bar_" + caps + "_frame",
                                   new Vector2(x0, y0), new Vector2(x1, y1), Color.white, rounded: false);
            var fImg = frameGo.GetComponent<Image>();
            if (fImg != null)
            {
                fImg.raycastTarget = false;
                var fs = RpgUiCatalog.Get(RpgUiCatalog.RoleBars, frameSprite);
                if (fs != null) { fImg.sprite = fs; fImg.type = Image.Type.Sliced; fImg.color = Color.white; }
                else { fImg.color = new Color(0f, 0f, 0f, 0.35f); ApplyRounded(fImg); }
            }
            var fillGo = AddImage(frameGo.transform, "Bar_" + caps + "_fill",
                                  new Vector2(0.04f, 0.20f), new Vector2(0.97f, 0.80f), fallbackFill, rounded: false);
            var fillImg = fillGo.GetComponent<Image>();
            if (fillImg != null)
            {
                fillImg.raycastTarget = false;
                var fl = RpgUiCatalog.Get(RpgUiCatalog.RoleBars, fillSprite);
                if (fl != null) { fillImg.sprite = fl; fillImg.type = Image.Type.Sliced; fillImg.color = Color.white; }
                else ApplyRounded(fillImg);
            }
            AddLabel(frameGo.transform, caps, 0f, 1f, Ink, ElarionUi.FontMicro,
                     TMPro.TextAlignmentOptions.Left, 0.03f, 0.30f, bold: true);
        }

        // One horizontal equipment-slot row in the paper-doll column: a rarity-tinted
        // square icon socket on the LEFT, the slot label + equipped item name stacked on
        // the RIGHT. filled=false renders a quiet EMPTY slot (soft, not alarming) so the
        // column always reads tidy. `slot` (0..3) stacks the row vertically.
        private void PaperDollRow(int slot, float top, float rowH, float gap, string label,
                                  string icon, Sprite iconSprite, string value, string rarity, bool filled)
        {
            float y1 = top - slot * (rowH + gap);
            float y0 = y1 - rowH;

            Color rc    = filled ? RarityColor(rarity) : AccentSoft;
            Color rcInk = filled ? RarityInk(rarity)   : InkDim;

            // Row plate (recessed glass tray) so each slot reads as a distinct chip.
            var row = AddImage(_paperDoll.transform, "EquipRow_" + label,
                               new Vector2(0.04f, y0), new Vector2(0.96f, y1),
                               filled ? Cell : new Color(Cell.r, Cell.g, Cell.b, 0.55f));
            NoRaycast(row);
            AddInnerRim(row, new Color(rc.r, rc.g, rc.b, filled ? 0.55f : 0.30f));

            // LEFT: ornate gear socket from Tech hud elements pack (Profile tabs / Green UI make rich frames for weapons & armor).
            // Tinted to rarity. Large touch-friendly size.
            Color socketTint = filled ? rc : AccentSoft;
            var sock = ElarionUiKit.TechGearSocket(row.transform, "TechSocket", new Vector2(0.04f, 0.10f), new Vector2(0.32f, 0.90f), socketTint, isWeapon: label == "WEAPON");
            NoRaycast(sock);
            Color glyphCol = filled ? rcInk : new Color(InkDim.r, InkDim.g, InkDim.b, 0.7f);
            string glyph = string.IsNullOrEmpty(icon) ? (filled ? "?" : "+") : icon;
            AddIcon(sock.transform, iconSprite, glyph, ElarionUi.FontHead, glyphCol, filled ? 1f : 0.7f);

            // RIGHT: slot label (micro caps) over the item name.
            AddLabel(row.transform, label, 0.50f, 0.92f, InkMicro,
                     ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Left, 0.40f, 0.97f, spacing: 2f);
            AddLabel(row.transform, value, 0.10f, 0.55f,
                     filled ? rcInk : new Color(InkDim.r, InkDim.g, InkDim.b, 0.7f),
                     ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Left, 0.40f, 0.97f, bold: filled);
        }

        // ====================================================================
        // TABS
        // ====================================================================
        // Tabs fill the full width of their host row (the _tabsRoot band under the
        // header). "Accessories" is the mockup label for the Tab.Outfits slot (the
        // cosmetic/accessory tab — enum + logic unchanged).
        private void BuildTabs(Transform host)
        {
            string[] names = { "Weapons", "Armor", "Accessories", "Consumables" };
            Tab[] tabs = { Tab.Weapons, Tab.Armor, Tab.Outfits, Tab.Consumables };
            float y0 = 0.06f, y1 = 0.94f;
            float gap = 0.012f;
            float w = (1f - gap * (names.Length - 1)) / names.Length;   // four equal pills
            float x = 0f;
            for (int i = 0; i < names.Length; i++)
            {
                Tab t = tabs[i];
                bool sel = _tab == t;
                float cx = x + w * 0.5f;
                // Active = filled gold pill (dark-ink label) with a bronze underline.
                // Inactive = quiet parchment pill (dark-ink label). Escalating state.
                // Inactive pill = a slightly deeper tan than the panel so it reads as a
                // distinct, tappable chip (plain parchment would vanish into the bg).
                Color inactive = new Color(0.847f, 0.804f, 0.710f, 1f);
                Color bg = sel ? ElarionUi.GoldButton : inactive;
                var btn = AddButton(host, names[i], new Vector2(cx, w * 0.5f), new Vector2(y0, y1),
                                    bg, () => SelectTab(t), sel ? ButtonKind.Gold : ButtonKind.Neutral);
                // Sprite-first tech-pack dressing: the ACTIVE pill takes the gilded gold
                // button frame; INACTIVE pills take the ornate banner-tab plate. So the tab
                // row reads as the tech-pack's gold-rune tabs (procedural when pack absent).
                if (sel) DressButtonPack(btn);
                else { var pi = btn.targetGraphic as Image; if (pi != null) { var ts = RpgUiCatalog.Get(RpgUiCatalog.RolePanel, RpgUiCatalog.PanelTab); if (ts != null) { pi.sprite = ts; pi.type = Image.Type.Sliced; pi.color = Color.white; } } }
                // Sprite-FIRST pack icon tucked in the tab's top-left (decorative, non-
                // raycast). Only shows when the RPG pack is imported AND the tab has a
                // matching bronze icon; otherwise the pill stays text-only (unchanged).
                var tabIcon = TabPackIcon(t);
                if (tabIcon != null)
                {
                    var ic = AddImage(btn.transform, "TabIcon",
                                      new Vector2(0.04f, 0.30f), new Vector2(0.30f, 0.92f), new Color(0, 0, 0, 0));
                    NoRaycast(ic);
                    var im = ic.GetComponent<Image>();
                    im.sprite = tabIcon; im.color = Color.white; im.type = Image.Type.Simple;
                    im.preserveAspect = true;
                }
                // Active-tab glow halo behind the gold pill (mockup: "active tab glow").
                if (sel)
                {
                    var glow = AddImage(host, "TabGlow_" + names[i],
                                        new Vector2(cx - w * 0.5f - 0.006f, y0 - 0.06f),
                                        new Vector2(cx + w * 0.5f + 0.006f, y1 + 0.06f),
                                        new Color(ElarionUi.Gilt.r, ElarionUi.Gilt.g, ElarionUi.Gilt.b, 0.30f));
                    NoRaycast(glow);
                    glow.transform.SetSiblingIndex(btn.transform.GetSiblingIndex());
                }
                if (sel)
                {
                    // Gilt underline hugging the active pill's bottom edge.
                    var rule = new GameObject("TabUnderline", typeof(Image));
                    rule.transform.SetParent(btn.transform, false);
                    var rr = rule.GetComponent<RectTransform>();
                    rr.anchorMin = new Vector2(0.12f, 0f); rr.anchorMax = new Vector2(0.88f, 0f);
                    rr.pivot = new Vector2(0.5f, 0f);
                    rr.sizeDelta = new Vector2(0f, 3f);
                    rr.anchoredPosition = new Vector2(0f, -4f);
                    var ri = rule.GetComponent<Image>();
                    ri.color = GiltInk; ri.raycastTarget = false;
                }
                x += w + gap;
            }
        }

        // The RPG pack's bronze icon for a tab (Weapons→sword, Armor→shield,
        // Consumables→health potion), or null (Accessories has no clean pack icon, and
        // null when the pack isn't imported → the tab stays text-only).
        private static Sprite TabPackIcon(Tab t)
        {
            switch (t)
            {
                case Tab.Weapons:     return RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconSword);
                case Tab.Armor:       return RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, RpgUiCatalog.IconShield);
                case Tab.Consumables: return RpgUiCatalog.Get(RpgUiCatalog.RolePotion, RpgUiCatalog.PotionHealth);
                default:              return null; // Outfits/Accessories — no cohesive pack icon
            }
        }

        private void SelectTab(Tab t)
        {
            if (_tab == t) return;
            _tab = t;
            ClearSelection();
            // Rebuild the tab strip (so the selected pill updates) by re-opening chrome cheaply:
            // simplest reliable path — rebuild whole UI's tab row + grid + sidebar.
            SafeRun(RebuildTabsRow, "RebuildTabsRow");
            SafeRun(RebuildGrid,    "RebuildGrid");
            SafeRun(RebuildSidebar, "RebuildSidebar");
        }

        private void RebuildTabsRow()
        {
            if (_tabsRoot == null) return;
            for (int i = _tabsRoot.transform.childCount - 1; i >= 0; i--)
                Destroy(_tabsRoot.transform.GetChild(i).gameObject);
            BuildTabs(_tabsRoot.transform);
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

            // Mockup #41: a denser 4-column rune-framed grid (the right column is now
            // ~60% of the panel, so cells shrink from the old 3-up to 4-up). Cells stay
            // > 44px tap targets. (Empty-note cells span via LayoutElement.)
            var grid = content.AddComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(128f, 116f);
            grid.spacing = new Vector2(10f, 10f);
            grid.padding = new RectOffset(10, 10, 10, 10);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 4;
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
                BuildGearCell(content, WeaponTypeGlyph(w), ItemIconCatalog.ForWeapon(w), w.name, w.rarity, equipped, locked,
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
                BuildGearCell(content, ArmorTypeGlyph(a), ItemIconCatalog.ForArmor(a), a.name, a.rarity, equipped, locked,
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
                string glyph = ConsumableTypeGlyph(kv.Key, name);
                var sel = new ConsumableSel { id = kv.Key, def = def, count = kv.Value };
                BuildGearCell(content, glyph, ConsumableIcon(kv.Key, name), name + "  x" + kv.Value, "common", false, false, "",
                              () => { _selConsumable = sel; _selWeapon = null; _selArmor = null; RebuildSidebar(); });
            }
        }

        // A single grid cell: rarity-framed glass tile with an icon medallion, name in
        // the rarity colour, a rarity gem, and equipped ✓ / level-lock 🔒 overlays.
        private void BuildGearCell(Transform content, string icon, Sprite iconSprite, string name, string rarity,
                                   bool equipped, bool locked, string lockText, System.Action onTap)
        {
            Color rc    = RarityColor(rarity);
            Color rcInk = RarityInk(rarity);

            // Rarity frame (the cell sits inset inside it, so the rarity reads as a thin
            // tinted rim). Legendary/epic get a stronger rim. On the LIGHT ground a soft
            // alpha keeps it a quiet gilt-like border, not a loud glow.
            float frameAlpha = RarityFrameStrength(rarity);
            var frame = new GameObject("CellFrame", typeof(Image));
            frame.transform.SetParent(content, false);
            var fimg = frame.GetComponent<Image>();
            fimg.color = new Color(rc.r, rc.g, rc.b, locked ? frameAlpha * 0.5f : frameAlpha);
            ApplyRounded(fimg);

            var cell = new GameObject("Cell", typeof(Image), typeof(Button));
            cell.transform.SetParent(frame.transform, false);
            var crt = cell.GetComponent<RectTransform>();
            crt.anchorMin = new Vector2(0.04f, 0.05f); crt.anchorMax = new Vector2(0.96f, 0.95f);
            crt.offsetMin = Vector2.zero; crt.offsetMax = Vector2.zero;
            var img = cell.GetComponent<Image>();
            img.color = locked ? new Color(Cell.r, Cell.g, Cell.b, 0.85f)
                       : equipped ? CellSel : Cell;
            ApplyRounded(img);

            var btn = cell.GetComponent<Button>();
            btn.targetGraphic = img;
            StyleButtonColors(btn);
            if (onTap != null) btn.onClick.AddListener(() => onTap());

            // Icon medallion (a soft recessed well behind the TYPE glyph). On light
            // parchment the well is a faint warm tint, not a dark hole. Decorative
            // overlays are non-raycast so the whole cell stays a single tap target.
            // Use Tech hud elements pack socket (Healing Tabs/Profile for weapons) - same flow as paper doll and vendor buy rows.
            var techSock = ElarionUiKit.TechGearSocket(cell.transform, "TechIconWell", new Vector2(0.26f, 0.38f), new Vector2(0.74f, 0.95f),
                new Color(rc.r, rc.g, rc.b, locked ? 0.30f : 0.55f), isWeapon: true);
            NoRaycast(techSock);
            // Sprite-first: real item art when we have it, else the type glyph (sword icons etc from pack or catalog).
            AddIcon(techSock.transform, iconSprite, icon, ElarionUi.FontTitle + 2,
                    locked ? InkMicro : rcInk, locked ? 0.6f : 1f);

            // Name (rarity-INK coloured) along the bottom band — readable on light.
            AddLabel(cell.transform, name, 0.07f, 0.36f,
                     locked ? new Color(rcInk.r, rcInk.g, rcInk.b, 0.6f) : rcInk,
                     ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);

            // Rarity gem — a small tinted pip top-left so the tier reads at a glance.
            NoRaycast(AddImage(cell.transform, "Gem", new Vector2(0.05f, 0.80f), new Vector2(0.20f, 0.95f),
                               new Color(rc.r, rc.g, rc.b, 0.95f)));

            if (equipped)
            {
                var chip = AddImage(cell.transform, "Equipped", new Vector2(0.62f, 0.80f), new Vector2(0.96f, 0.96f),
                                    new Color(ElarionUi.Affordable.r, ElarionUi.Affordable.g, ElarionUi.Affordable.b, 0.95f));
                NoRaycast(chip);
                AddLabel(chip.transform, "v", 0f, 1f, ElarionUi.Ink, ElarionUi.FontLabel,
                         TMPro.TextAlignmentOptions.Center, 0f, 1f, bold: true);
            }
            if (locked)
            {
                // A soft parchment veil + a gilt lock chip, so level-locked gear clearly
                // reads as "not yet" WITHOUT going dark (stays in the light tone).
                NoRaycast(AddImage(cell.transform, "Veil", Vector2.zero, Vector2.one,
                                   new Color(0.965f, 0.945f, 0.890f, 0.45f)));
                var chip = AddImage(cell.transform, "Locked", new Vector2(0.26f, 0.40f), new Vector2(0.74f, 0.62f),
                                    new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.90f));
                NoRaycast(chip);
                AddLabel(chip.transform, "[ " + lockText + " ]", 0f, 1f, Ink,
                         ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0f, 1f, bold: true);
            }
        }

        private static void NoRaycast(GameObject go)
        {
            var img = go.GetComponent<Image>();
            if (img != null) img.raycastTarget = false;
        }

        private void BuildEmptyNote(Transform content, string msg)
        {
            var note = new GameObject("Empty", typeof(RectTransform));
            note.transform.SetParent(content, false);
            // Span across the grid (LayoutElement keeps it from being a tiny cell).
            var le = note.AddComponent<LayoutElement>();
            le.preferredWidth = 600f; le.preferredHeight = 120f;
            AddLabel(note.transform, msg, 0f, 1f, InkDim, ElarionUi.FontLabel,
                     TMPro.TextAlignmentOptions.Center, 0f, 1f);
        }

        // ====================================================================
        // DETAIL / EQUIP STRIP  (compact HORIZONTAL band beneath the grid)
        // -----------------------------------------------------------------------------
        // The mockup keeps the grid dominant, so the selected-item detail is a short,
        // wide strip laid out LEFT->RIGHT: [icon + name] | [stat chips + flavour] |
        // [EQUIP CTA]. The equip logic (GearLoadout.Equip*ById) is unchanged — only the
        // arrangement is horizontal now. Three column bands keep every element readable.
        //   LEFT block : x 0.015..0.235   (icon medallion + name + rarity)
        //   MID  block : x 0.250..0.690   (up to 3 stat chips, stacked + flavour)
        //   RIGHT block: x 0.705..0.985   (EQUIP / EQUIPPED / LOCKED button)
        // ====================================================================
        private const float SbMidX0 = 0.250f, SbMidX1 = 0.690f;

        private void RebuildSidebar()
        {
            if (_sidebarRoot == null) return;
            for (int i = _sidebarRoot.transform.childCount - 1; i >= 0; i--)
                Destroy(_sidebarRoot.transform.GetChild(i).gameObject);

            if (_selWeapon == null && _selArmor == null && _selConsumable == null)
            {
                AddLabel(_sidebarRoot.transform, "Tap an item to view + equip.", 0.40f, 0.60f,
                         InkDim, ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f);
                return;
            }

            if (_selWeapon != null) BuildWeaponSidebar(_selWeapon);
            else if (_selArmor != null) BuildArmorSidebar(_selArmor);
            else if (_selConsumable != null) BuildConsumableSidebar(_selConsumable);
        }

        // LEFT block: rarity-tinted icon medallion + name + rarity/job subline, all
        // stacked in the left ~22% of the strip.
        private void BuildDetailHeader(string icon, Sprite iconSprite, string name, string rarity, string subline)
        {
            Color rc    = RarityColor(rarity);
            Color rcInk = RarityInk(rarity);

            // Tech pack frame for the selected gear detail icon (differentiated weapon vs armor).
            // Replaces plain medallion with ornate socket from Profile tabs / Healing tabs.
            var techMed = ElarionUiKit.TechGearSocket(_sidebarRoot.transform, "TechDetailSocket",
                new Vector2(0.060f, 0.40f), new Vector2(0.190f, 0.92f), new Color(rc.r, rc.g, rc.b, 0.14f),
                isWeapon: _selWeapon != null);
            NoRaycast(techMed);
            AddIcon(techMed.transform, iconSprite, string.IsNullOrEmpty(icon) ? "?" : icon,
                    ElarionUi.FontHead, rcInk, 1f);

            // Name + rarity band below the medallion.
            var band = AddImage(_sidebarRoot.transform, "RarityBand",
                                new Vector2(0.015f, 0.22f), new Vector2(0.235f, 0.37f),
                                new Color(rc.r, rc.g, rc.b, 0.22f));
            AddInnerRim(band, new Color(rc.r, rc.g, rc.b, 0.70f));
            AddLabel(band.transform, name, 0f, 1f, rcInk, ElarionUi.FontLabel,
                     TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, bold: true);
            AddLabel(_sidebarRoot.transform, RarityGlyph(rarity) + " " + subline, 0.045f, 0.19f,
                     InkDim, ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.015f, 0.235f, spacing: 1f);
        }

        // A recessed stat chip in the MIDDLE column. `slot` (0,1,2) stacks it vertically
        // within the strip. label left, value right, optional ▲/▼ delta vs the worn piece.
        private void StatRow(int slot, string label, string value, float delta)
        {
            // Three rows from y 0.86 down to 0.18, each ~0.22 tall with a small gap.
            float y1 = 0.88f - slot * 0.245f;
            float y0 = y1 - 0.205f;
            var row = AddImage(_sidebarRoot.transform, "Stat_" + label,
                               new Vector2(SbMidX0, y0), new Vector2(SbMidX1, y1), Track);
            AddLabel(row.transform, label, 0f, 1f, InkDim, ElarionUi.FontLabel,
                     TMPro.TextAlignmentOptions.Left, 0.06f, 0.55f);
            AddLabel(row.transform, value, 0f, 1f, Ink, ElarionUi.FontLabel,
                     TMPro.TextAlignmentOptions.Right, 0.45f, 0.74f, bold: true);
            if (Mathf.Abs(delta) > 0.0001f)
            {
                bool up = delta > 0f;
                // Darkened up/down deltas so they read on the light tan stat row.
                AddLabel(row.transform, up ? "^" : "v", 0f, 1f,
                         up ? new Color(0.20f, 0.45f, 0.18f, 1f) : new Color(0.62f, 0.16f, 0.14f, 1f),
                         ElarionUi.FontLabel, TMPro.TextAlignmentOptions.Right, 0.45f, 0.94f, bold: true);
            }
        }

        // A one-line flavour caption beneath the stat chips in the middle column.
        private void DetailFlavour(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            AddLabel(_sidebarRoot.transform, "\"" + text + "\"", 0.05f, 0.165f,
                     InkDim, ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, SbMidX0, SbMidX1);
        }

        private void BuildWeaponSidebar(WeaponDef w)
        {
            int level = HeroLevel();
            bool locked = w.req != null && level < w.req.level;
            bool equipped = _loadout != null && _loadout.EquippedWeapon != null &&
                            string.Equals(_loadout.EquippedWeapon.id, w.id, System.StringComparison.OrdinalIgnoreCase);
            WeaponDef cur = _loadout != null ? _loadout.EquippedWeapon : null;

            BuildDetailHeader(WeaponTypeGlyph(w), ItemIconCatalog.ForWeapon(w), w.name, w.rarity, Cap(w.rarity) + " - " + Cap(w.job));

            // Compare-to-equipped stat block (▲/▼ vs the worn weapon).
            int slot = 0;
            float curDmg = cur != null ? cur.damageMult : 0f;
            StatRow(slot++, "Damage", $"x{w.damageMult:0.0#}", equipped ? 0f : w.damageMult - curDmg);
            if (w.reach > 0f)
            {
                float curReach = cur != null ? cur.reach : 0f;
                StatRow(slot++, "Reach", $"{w.reach:0.0} m", equipped ? 0f : w.reach - curReach);
            }
            if (w.req != null && w.req.level > 1)
                StatRow(slot++, "Requires", "Lv " + w.req.level, 0f);

            DetailFlavour(!string.IsNullOrEmpty(w.flavor) ? w.flavor : w.saga);

            // Prominent Equip CTA skinned from Tech pack Play Buttons (ornate gold frame, large thumb target).
            var equipBtn = ElarionUiKit.TechPrimaryButton(_sidebarRoot.transform, equipped ? "EQUIPPED" : (locked ? "LOCKED" : "EQUIP"),
                                                            new Vector2(0.72f, 0.25f), new Vector2(0.98f, 0.75f),
                                                            () =>
                                                            {
                                                                if (_loadout != null && !equipped && !locked)
                                                                    _loadout.EquipWeaponById(w.id);
                                                            });
            if (equipped || locked) equipBtn.interactable = false;
        }

        private void BuildArmorSidebar(ArmorDef a)
        {
            int level = HeroLevel();
            bool locked = a.req != null && level < a.req.level;
            bool equipped = _loadout != null && _loadout.EquippedArmor != null &&
                            string.Equals(_loadout.EquippedArmor.id, a.id, System.StringComparison.OrdinalIgnoreCase);
            ArmorDef cur = _loadout != null ? _loadout.EquippedArmor : null;

            BuildDetailHeader(ArmorTypeGlyph(a), ItemIconCatalog.ForArmor(a), a.name, a.rarity, Cap(a.rarity) + " - " + Cap(a.job));

            int slot = 0;
            float curDef = cur != null ? cur.defense : 0f;
            StatRow(slot++, "Defense", $"{a.defense * 100f:0}%", equipped ? 0f : a.defense - curDef);
            if (a.hpBonus > 0f)
            {
                float curHp = cur != null ? cur.hpBonus : 0f;
                StatRow(slot++, "HP Bonus", $"+{a.hpBonus:0}", equipped ? 0f : a.hpBonus - curHp);
            }
            if (a.req != null && a.req.level > 1)
                StatRow(slot++, "Requires", "Lv " + a.req.level, 0f);

            DetailFlavour(!string.IsNullOrEmpty(a.flavor) ? a.flavor : a.saga);

            // Prominent Equip CTA skinned from Tech pack (large ornate Play-button frame for armor focus).
            var equipBtn = ElarionUiKit.TechPrimaryButton(_sidebarRoot.transform, equipped ? "EQUIPPED" : (locked ? "LOCKED" : "EQUIP"),
                                                            new Vector2(0.72f, 0.25f), new Vector2(0.98f, 0.75f),
                                                            () =>
                                                            {
                                                                if (_loadout != null && !locked)
                                                                {
                                                                    if (equipped) _loadout.EquipArmorById(null);
                                                                    else _loadout.EquipArmorById(a.id);
                                                                }
                                                            });
            if (locked) equipBtn.interactable = false;
        }

        private void BuildConsumableSidebar(ConsumableSel c)
        {
            string name = c.def != null && !string.IsNullOrEmpty(c.def.DisplayName) ? c.def.DisplayName : c.id;
            string glyph = ConsumableTypeGlyph(c.id, name);
            BuildDetailHeader(glyph, ConsumableIcon(c.id, name), name, "common", "Owned x" + c.count);

            int slot = 0;
            if (c.def != null)
            {
                StatRow(slot++, Cap(c.def.EffectRaw), c.def.Magnitude.ToString("0"), 0f);
                if (c.def.Duration > 0f)
                    StatRow(slot++, "Duration", $"{c.def.Duration:0}s", 0f);
                StatRow(slot++, "Use", c.def.UsableInFight ? "In combat" : "Rest only", 0f);
            }

            // Consumables aren't "equipped" — v1 surfaces them read-only here.
            // TODO use-consumable — wire to the use-service when its public entry is settled.
            AddLabel(_sidebarRoot.transform, "Use from the combat hotbar.", 0.40f, 0.62f,
                     InkDim, ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.705f, 0.985f);
        }

        // RIGHT block: the EQUIP call-to-action (or EQUIPPED / LOCKED state). A tall pill
        // filling the right ~28% of the strip so it's the obvious thumb target.
        private void BuildEquipButton(bool equipped, bool locked, System.Action equip, System.Action unequip)
        {
            string label;
            Color color;
            ButtonKind kind;
            System.Action action;

            if (locked)
            {
                label = "LOCKED"; color = new Color(ElarionUi.Danger.r, ElarionUi.Danger.g, ElarionUi.Danger.b, 0.55f);
                kind = ButtonKind.Danger; action = null;
            }
            else if (equipped)
            {
                label = "v EQUIPPED";
                color = new Color(ElarionUi.Affordable.r, ElarionUi.Affordable.g, ElarionUi.Affordable.b, 0.55f);
                kind = ButtonKind.Confirm; action = null;   // already worn
            }
            else
            {
                label = "EQUIP"; color = ElarionUi.GoldButton; kind = ButtonKind.Gold; action = equip;
            }

            // A soft glow plate behind the CTA when it's actionable, so EQUIP "pops".
            if (action != null)
                NoRaycast(AddImage(_sidebarRoot.transform, "EquipGlow", new Vector2(0.700f, 0.18f), new Vector2(0.990f, 0.82f),
                                   new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.20f)));

            var btn = AddButton(_sidebarRoot.transform, label, new Vector2(0.845f, 0.140f),
                                new Vector2(0.24f, 0.76f), color, action, kind);
            // The EQUIP CTA wears the tech-pack's gilded gold button frame (sprite-first).
            DressButtonPack(btn);
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

        // The armor to SHOW in the paper-doll's ARMOR socket. Prefers the live loadout's
        // equipped piece; but if the loadout is null OR somehow has no armor (e.g. the modal
        // opened a frame before GearLoadout.Refresh ran on a just-spawned hero), fall back to
        // the SAME default the loadout itself resolves — GearCatalog.BestArmor(job, level),
        // which for a fresh level-1 hero is the existing starter "Wanderer's Cloth"
        // (armor_cloth). This guarantees a fresh player always SEES their basic armor on the
        // paper-doll, sourced entirely from the existing armor.json catalog (no new item/art).
        private ArmorDef ResolveDisplayArmor()
        {
            if (_loadout != null && _loadout.EquippedArmor != null) return _loadout.EquippedArmor;
            try { return GearCatalog.BestArmor(HeroJob, HeroLevel()); }
            catch { return null; }
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
        // Canonical rarity colour — routed to the shared kit's ONE rarity map.
        private static Color RarityColor(string rarity)
        {
            return ElarionUiKit.RarityColor(rarity);
        }

        // A rarity hue for TEXT/GLYPHS. On the dark glass the bright RarityColor reads
        // fine, but we keep this richer variant for labels so each tier stays vivid.
        private static Color RarityInk(string rarity)
        {
            switch ((rarity ?? "common").ToLowerInvariant())
            {
                case "uncommon":  return new Color(0.22f, 0.44f, 0.20f, 1f);   // deep green
                case "rare":      return new Color(0.16f, 0.33f, 0.62f, 1f);   // deep blue
                case "epic":      return new Color(0.45f, 0.24f, 0.62f, 1f);   // deep purple
                case "legendary": return new Color(0.64f, 0.40f, 0.10f, 1f);   // bronze/amber
                default:          return new Color(0.30f, 0.27f, 0.22f, 1f);   // common ink-grey
            }
        }

        private static string RarityGlyph(string rarity)
        {
            return ElarionUiKit.RarityGlyph(rarity);
        }

        // How loud the rarity frame glows — routed to the shared kit's escalation map.
        private static float RarityFrameStrength(string rarity)
        {
            return ElarionUiKit.RarityFrameStrength(rarity);
        }

        // ====================================================================
        // ITEM TYPE GLYPHS — denote a sword vs staff vs bow vs armor AT A GLANCE
        // ====================================================================
        // The catalog has NO explicit type/subType field (GearCatalog WeaponDef/
        // ArmorDef = id/name/icon/job/rarity only — see GearCatalog.cs), and the
        // `icon` field is an EMOJI placeholder (🗡️🪄🏹🛡️…) that is (a) astral-plane /
        // variation-selector heavy = inconsistent in TMP default font + WebGL, and
        // (b) does not cleanly denote the weapon CLASS. So we derive a TYPE from id +
        // name keyword matching (then job as a fallback) and map it to ONE clear,
        // BMP-only glyph per type. All glyphs below are in the Basic-Multilingual-
        // Plane (Misc Symbols / Dingbats / Geometric / Punctuation) so they render in
        // the TMP default font on every platform incl. WebGL — NO astral-plane risk.
        //
        // When real per-type art lands, swap these returns for sprite icons (mirror
        // PetPortraitRenderer's render-to-Sprite); the call sites already centralise
        // here. TYPE GLYPHS are the agreed acceptable bar ("just something to denote").
        private static string WeaponTypeGlyph(WeaponDef w)
        {
            if (w == null) return "?";
            string k = ((w.id ?? "") + " " + (w.name ?? "")).ToLowerInvariant();
            // Most specific first.
            if (Has(k, "dagger", "knife", "dirk", "stiletto"))          return "D"; // dagger
            if (Has(k, "bow", "recurve", "longbow", "shortbow"))        return "B"; // bow / ranged shot
            if (Has(k, "wand"))                                         return "W"; // wand
            if (Has(k, "staff", "scepter", "sceptre", "stave", "rod"))  return "S"; // arcane staff
            if (Has(k, "censer", "censor", "thurible"))                 return "C"; // cleric censer
            if (Has(k, "axe", "hatchet"))                               return "A"; // axe
            if (Has(k, "hammer", "maul", "mace"))                       return "H"; // hammer/mace
            if (Has(k, "greatsword", "claymore", "sword", "blade",
                       "longsword", "saber", "sabre", "edge", "brand",
                       "breaker", "keeper")) return "/";                            // sword
            // Fallback by class.
            switch ((w.job ?? "").ToLowerInvariant())
            {
                case "mage":   return "S"; // staff
                case "ranger": return "B"; // bow
                case "cleric": return "C"; // censer
                case "knight": return "/"; // sword
                default:        return "/";
            }
        }

        private static string ArmorTypeGlyph(ArmorDef a)
        {
            if (a == null) return "?";
            string k = ((a.id ?? "") + " " + (a.name ?? "")).ToLowerInvariant();
            if (Has(k, "shield", "aegis", "buckler", "ward"))           return "O"; // shield boss
            if (Has(k, "plate", "platemail"))                           return "#"; // plate
            if (Has(k, "chain", "mail", "chainmail"))                   return "x"; // mail
            if (Has(k, "leather", "hide"))                              return "x"; // leather
            if (Has(k, "cloth", "robe", "cloak", "garb", "wanderer"))   return "~"; // cloth/robe
            if (Has(k, "helm", "helmet", "hood", "crown", "cap"))       return "^"; // helm
            return "x";                                                            // generic armor
        }

        // Sprite-FIRST consumable icon: prefer the existing sliced item art
        // (ItemIconCatalog); when that's absent, use the RPG pack's framed magic-bottle
        // potion keyed by effect (health=red b1, mana=blue b2, fire/burst=orange b3);
        // null falls through to the TYPE GLYPH in AddIcon. WebGL-safe (RpgUiCatalog
        // loads from Resources only). The pack potions are the cohesive upgrade for the
        // larder cells which otherwise show a bare "+"/"*" glyph.
        private static Sprite ConsumableIcon(string id, string name)
        {
            var art = ItemIconCatalog.ForConsumable(id, name);
            if (art != null) return art;

            string k = ((id ?? "") + " " + (name ?? "")).ToLowerInvariant();
            if (Has(k, "mana", "aether", "ether", "arcane"))
                return RpgUiCatalog.Get(RpgUiCatalog.RolePotion, RpgUiCatalog.PotionMana);
            if (Has(k, "bomb", "fire", "flask", "oil", "burn"))
                return RpgUiCatalog.Get(RpgUiCatalog.RolePotion, RpgUiCatalog.PotionFire);
            if (Has(k, "potion", "elixir", "draught", "tonic", "heal",
                       "health", "hp", "regen", "life"))
                return RpgUiCatalog.Get(RpgUiCatalog.RolePotion, RpgUiCatalog.PotionHealth);
            // Generic consumable → the health bottle reads as a representative potion.
            return RpgUiCatalog.Get(RpgUiCatalog.RolePotion, RpgUiCatalog.PotionHealth);
        }

        private static string ConsumableTypeGlyph(string id, string name)
        {
            string k = ((id ?? "") + " " + (name ?? "")).ToLowerInvariant();
            if (Has(k, "potion", "elixir", "draught", "tonic", "heal",
                       "health", "hp", "regen"))                        return "+"; // potion / heal
            if (Has(k, "mana", "aether", "ether", "arcane"))            return "*"; // mana spark
            if (Has(k, "food", "bread", "meat", "ration", "feast",
                       "stew", "meal"))                                 return "%"; // food / sustenance
            if (Has(k, "scroll", "tome", "rune"))                       return "="; // scroll
            if (Has(k, "bomb", "fire", "flask", "oil"))                 return "o"; // burst
            return "."; // generic pip
        }

        private static bool Has(string haystack, params string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
                if (haystack.IndexOf(needles[i], System.StringComparison.Ordinal) >= 0) return true;
            return false;
        }

        private static string Cap(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            return char.ToUpperInvariant(s[0]) + s.Substring(1);
        }

        // ====================================================================
        // SLEEK uGUI helpers (mirrored from ArenaPanel.cs)
        // ====================================================================
        // The framed dark-glass panel — routes to the shared presentation kit.
        private GameObject AddPanel(Transform parent, Vector2 min, Vector2 max, bool deep = false)
        {
            return ElarionUiKit.Panel(parent, min, max, deep: deep, innerRim: false);
        }

        // Fraction-anchored Image — delegates to the shared kit primitive.
        private static GameObject AddImage(Transform parent, string name, Vector2 min, Vector2 max,
            Color color, bool rounded = true)
        {
            return ElarionUiKit.AddImage(parent, name, min, max, color, rounded);
        }

        private static void ApplyRounded(Image img)
        {
            ElarionUiKit.ApplyRounded(img);
        }

        // ── Tech-pack sprite dressing (sprite-FIRST, with the procedural fallback) ──
        // Drop the named ornate pack PANEL frame (RolePanel) onto an Image as a 9-sliced
        // sprite so a plate/socket/well reads as the gilt-framed tech-pack art. No-op when
        // the pack isn't imported (the Image keeps its tinted rounded fill) so nothing
        // regresses. The tint is kept (Color.white loses rarity colour) UNLESS keepWhite.
        private static void DressPanel(GameObject host, string packSpriteName, bool keepWhite = false)
        {
            if (host == null) return;
            var sp = RpgUiCatalog.Get(RpgUiCatalog.RolePanel, packSpriteName);
            if (sp == null) return;
            var img = host.GetComponent<Image>();
            if (img == null) return;
            img.sprite = sp;
            img.type = Image.Type.Sliced;
            if (keepWhite) img.color = Color.white;
        }

        // Dress an AddButton-produced Button sprite-FIRST with the tech-pack's ornate gold
        // button frame (RoleButton/button_gold) so every CTA reads as the gilded pack
        // button. The fill tint is preserved when the pack is absent. No-op (procedural
        // rounded glass) when the pack isn't imported.
        private static void DressButtonPack(Button btn)
        {
            if (btn == null) return;
            var sp = RpgUiCatalog.Get(RpgUiCatalog.RoleButton, RpgUiCatalog.ButtonGold);
            if (sp == null) return;
            var img = btn.targetGraphic as Image;
            if (img == null) return;
            img.sprite = sp;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
        }

        // The tech-pack's slot-socket art behind an equip/grid slot. The pack ships NO
        // dedicated square socket sprite (only bar-shaped frames + the larger inventory/
        // bar/tab panels — see RpgUiImporter); the ornate "panel_bar" plate is the closest
        // cohesive socket frame, so we drop it BEHIND the rarity tint as a NON-RAYCAST,
        // rarity-coloured overlay. Sprite-first: no-op (procedural tint) when absent.
        private static void DressSocket(GameObject host, Color rarityTint)
        {
            if (host == null) return;
            var sp = RpgUiCatalog.Get(RpgUiCatalog.RolePanel, RpgUiCatalog.PanelBar);
            if (sp == null) return;
            var img = host.GetComponent<Image>();
            if (img == null) return;
            img.sprite = sp;
            img.type = Image.Type.Sliced;
            // Keep the rarity hue but let the gilded plate read — a soft rarity wash.
            img.color = new Color(rarityTint.r, rarityTint.g, rarityTint.b,
                                  Mathf.Max(rarityTint.a, 0.65f));
        }

        // A circular Image positioned by CENTER + RADIUS in the parent's normalised
        // space (cx,cy in 0..1; radius in 0..1 of the parent's WIDTH so it stays round
        // when the parent is square). Uses the shared kit circle sprite; WebGL-safe
        // (falls back to the kit rounded quad if the circle build fails).
        private static GameObject AddCircle(Transform parent, string name, float cx, float cy, float radius, Color color)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var r = go.GetComponent<RectTransform>();
            r.anchorMin = new Vector2(cx - radius, cy - radius);
            r.anchorMax = new Vector2(cx + radius, cy + radius);
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = color;
            var sprite = ElarionUiKit.CircleSprite;
            if (sprite != null) { img.sprite = sprite; img.type = Image.Type.Simple; }
            else ElarionUiKit.ApplyRounded(img);
            return go;
        }

        // A thin circular rim AROUND a circular host: a slightly larger disc rendered
        // BEHIND the host so a gilt ring peeks out past its edge.
        private void AddCircleRim(GameObject host, Color color)
        {
            var rim = AddCircle(host.transform, "CircleRim", 0.5f, 0.5f, 0.54f,
                                new Color(color.r, color.g, color.b, color.a * 0.85f));
            rim.GetComponent<Image>().raycastTarget = false;
            rim.transform.SetAsFirstSibling();
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
            ElarionUiKit.AddRimUnderline(panel);
        }

        // A 1px inner rim hugging an element's edges — delegates to the shared kit.
        private void AddInnerRim(GameObject host, Color color)
        {
            ElarionUiKit.AddInnerRim(host, color);
        }

        // A faint runic glyph strip — a HINT of Elarion magic across a header band.
        // Bronze-tinted (not pale gold) so it actually reads on the light parchment.
        private void AddRuneStrip(Transform parent, float y0, float y1)
        {
            var t = AddLabel(parent, ElarionUi.RuneGlyphs + ElarionUi.RuneGlyphs, y0, y1,
                             new Color(GiltInk.r, GiltInk.g, GiltInk.b, 0.42f),
                             ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f, spacing: 4f);
            t.raycastTarget = false;
        }

        // A title label with a soft LIGHT emboss behind it. On the light parchment a
        // dark drop-shadow would muddy dark-ink text, so the offset layer is a pale
        // parchment highlight (down-right) giving a gentle pressed-into-paper feel.
        private void AddLabelShadow(Transform parent, string text, float y0, float y1, Color color,
                                    int size, float x0, float x1, float spacing)
        {
            var emboss = AddLabel(parent, text, y0, y1, new Color(1f, 0.98f, 0.92f, 0.55f), size,
                                  TMPro.TextAlignmentOptions.Center, x0, x1, spacing: spacing, bold: true);
            var srt = emboss.GetComponent<RectTransform>();
            srt.anchoredPosition += new Vector2(1f, -1f);
            AddLabel(parent, text, y0, y1, color, size, TMPro.TextAlignmentOptions.Center,
                     x0, x1, spacing: spacing, bold: true);
        }

        // ── Hero display helpers ───────────────────────────────────────────────
        private string HeroDisplayName(string job)
        {
            // No per-hero name field on the loadout; surface the class as the title
            // (the roster pairs each class with a canon name — wire that here when a
            // hero-name accessor exists). TODO hero-name — read from the active hero.
            switch ((job ?? "").ToLowerInvariant())
            {
                case "mage":   return "Thrain the Wise";
                case "knight": return "Grom Ironhand";
                case "ranger": return "Sylas Swift";
                case "healer": return "Elara Dawnlight";
                default:        return Cap(job) + " Hero";
            }
        }

        private static string ClassCrest(string job)
        {
            switch ((job ?? "").ToLowerInvariant())
            {
                case "mage":   return "S";   // staff
                case "knight": return "/";   // blade
                case "ranger": return "B";   // bow
                case "healer": return "+";   // cross
                default:        return ElarionUi.CrestGlyph;
            }
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
            t.text = text ?? string.Empty;
            t.fontSize = size;
            t.color = color;
            t.alignment = align;
            t.characterSpacing = spacing;
            t.raycastTarget = false;
            if (bold) t.fontStyle = TMPro.FontStyles.Bold;
            return t;
        }

        // Sprite-first icon: if `sprite` is non-null, draw the real item art (preserving
        // its aspect ratio, inset slightly so it sits inside the well); otherwise fall
        // back to the existing TYPE GLYPH label. This is the single chokepoint that lets
        // every icon site (grid cell / paper-doll slot / detail medallion) upgrade to art
        // without touching its layout. tint = glyph colour; alpha = glyph fade (locked).
        private static void AddIcon(Transform parent, Sprite sprite, string glyph, int glyphSize,
                                    Color glyphColor, float alpha)
        {
            if (sprite != null)
            {
                var go = new GameObject("Icon", typeof(Image));
                go.transform.SetParent(parent, false);
                var r = go.GetComponent<RectTransform>();
                r.anchorMin = new Vector2(0.08f, 0.08f); r.anchorMax = new Vector2(0.92f, 0.92f);
                r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
                var img = go.GetComponent<Image>();
                img.sprite = sprite;
                img.preserveAspect = true;
                img.raycastTarget = false;
                img.color = new Color(1f, 1f, 1f, alpha);
                return;
            }
            // Glyph fallback (unchanged look).
            AddLabel(parent, string.IsNullOrEmpty(glyph) ? "?" : glyph, 0f, 1f,
                     new Color(glyphColor.r, glyphColor.g, glyphColor.b, glyphColor.a * alpha),
                     glyphSize, TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f);
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

            // On the LIGHT parchment screen, ALL button labels are dark ink (Gold CTA
            // sits on gold = ink; Neutral sits on light parchment = ink; Confirm/Danger
            // tints below are kept light/soft enough that ink stays readable).
            Color textColor = Ink;
            var tt = AddLabel(go.transform, label, 0f, 1f, textColor, ElarionUi.FontBody,
                              TMPro.TextAlignmentOptions.Center, 0f, 1f, spacing: 1f, bold: true);
            tt.raycastTarget = false;
            return btn;
        }

        private static void StyleButtonColors(Button button)
        {
            ElarionUiKit.StyleButtonColors(button);
        }
    }
}
