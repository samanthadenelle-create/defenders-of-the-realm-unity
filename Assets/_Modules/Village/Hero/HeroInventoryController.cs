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
        private Tab _tab = Tab.Weapons;

        // The current selection (one of these is non-null while a cell is selected).
        private WeaponDef _selWeapon;
        private ArmorDef _selArmor;
        private ConsumableSel _selConsumable;

        private GearLoadout _loadout;     // the live hero's gear model (drives the hero)

        // ── LIGHT PARCHMENT palette (north-star: warm-parchment, airy, calm) ──────
        // SELF-CONTAINED to the inventory (do NOT change the global ElarionUi/Kit
        // palette — that's a separate pass). This is a TONE INVERSION of the old
        // dark-glass kit look: light warm-parchment fills, DARK INK text (readable
        // on light), THIN gilt frames, subtle runic accents. The role names are kept
        // (Glass/GlassDeep/Cell/etc.) so the layout code below is untouched — only
        // the VALUES flip dark->light. ALL text colour now routes through the local
        // Ink* tones (see below) so nothing stays cream-on-light (unreadable).
        //
        // Panel fills — warm parchment, lightly translucent so the scrim warms it.
        private static readonly Color Glass      = new Color(0.929f, 0.902f, 0.839f, 0.97f);  // ~#EDE6D6 parchment
        private static readonly Color GlassDeep  = new Color(0.965f, 0.945f, 0.890f, 0.985f); // brighter paper (panels/sidebar)
        // Recessed wells / tracks — a faint warm shadow, NOT black, so it stays light.
        private static readonly Color Track      = new Color(0.788f, 0.741f, 0.643f, 0.55f);  // soft tan recess
        // Item cells — clean light paper; selected = a warm gilt-tinted paper.
        private static readonly Color Cell       = new Color(0.957f, 0.933f, 0.875f, 1f);     // light cell paper
        private static readonly Color CellSel    = new Color(0.965f, 0.886f, 0.690f, 1f);     // warm gilt-tinted selected paper
        // Gilt frame accents (thin) — gold rims on a light ground.
        private static readonly Color AccentSoft = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.45f);
        private static readonly Color Accent     = new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.95f);
        // Paper-doll "niche": a slightly deeper aged-parchment alcove (still LIGHT).
        private static readonly Color StoneBack  = new Color(0.886f, 0.847f, 0.761f, 1f);     // aged parchment alcove
        private static readonly Color StoneNiche = new Color(0.851f, 0.808f, 0.714f, 1f);     // niche backing (light tan)
        // Aether tint — a faint violet bloom (kept very soft so it reads on light).
        private static readonly Color AetherSoft = new Color(ElarionUi.Aether.r, ElarionUi.Aether.g, ElarionUi.Aether.b, 0.16f);

        // ── INK text tones (dark, readable on the new light parchment) ────────────
        // Primary ink ~#2C2115 (matches ElarionUi.Ink), a softer ink for secondary,
        // and a muted ink for micro/labels. These REPLACE every prior use of the
        // cream Parchment / ParchmentDim text colours within this screen.
        private static readonly Color Ink        = new Color(0.157f, 0.118f, 0.078f, 1f);     // primary dark ink
        private static readonly Color InkDim      = new Color(0.345f, 0.290f, 0.220f, 1f);    // secondary / flavour ink
        private static readonly Color InkMicro    = new Color(0.439f, 0.376f, 0.286f, 1f);    // micro caps / hints
        // Gilt-on-light reads as a dark warm bronze for HEADINGS (true gilt is too
        // pale on parchment); use this where a title used ElarionUi.Gilt on dark.
        private static readonly Color GiltInk     = new Color(0.561f, 0.408f, 0.110f, 1f);    // bronze heading ink

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
                if (_ui == null) return;            // BuildRoot failed hard — nothing to show
                _ui.SetActive(true);
                Subscribe();
                _tab = Tab.Weapons;
                ClearSelection();
                // Each section is isolated so a failure in one (e.g. a single bad
                // catalog row) leaves the rest of the modal rendered, not blank.
                SafeRun(RebuildPaperDoll, "RebuildPaperDoll");
                SafeRun(RebuildGrid,      "RebuildGrid");
                SafeRun(RebuildSidebar,   "RebuildSidebar");
            }
            catch (System.Exception e)
            {
                Debug.LogError("[HeroInventoryController] Open failed (UI may be partial): " + e);
            }
        }

        // Runs a UI-rebuild step, swallowing+logging any exception so one bad
        // section can't blank the whole inventory (WebGL hardening).
        private static void SafeRun(System.Action step, string label)
        {
            try { step(); }
            catch (System.Exception e)
            {
                Debug.LogError("[HeroInventoryController] " + label + " failed: " + e);
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
            canvas.sortingOrder = 2600;                 // above HUD + Arena (1100)

            var scaler = _ui.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 1920);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _ui.AddComponent<GraphicRaycaster>();

            // Full-screen warm dim that blocks click-through. Kept fairly dark so the
            // LIGHT parchment panel reads as a lifted, lit sheet floating above it.
            var scrim = AddImage(_ui.transform, "Scrim", Vector2.zero, Vector2.one,
                                 new Color(0.10f, 0.075f, 0.05f, 0.80f), rounded: false);
            // Tapping the scrim closes (a button covering the whole backdrop, behind the panel).
            var scrimBtn = scrim.AddComponent<Button>();
            scrimBtn.transition = Selectable.Transition.None;
            scrimBtn.onClick.AddListener(Close);

            // ── DEPTH: a thin gilt backboard halo just behind the parchment panel, so
            // the modal reads as a framed, lifted sheet rather than a flat fill. ──
            AddImage(_ui.transform, "Backboard", new Vector2(0.025f, 0.018f), new Vector2(0.975f, 0.982f),
                     new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.35f));

            // The main panel fills most of the screen (mobile-first).
            var panel = AddPanel(_ui.transform, new Vector2(0.04f, 0.03f), new Vector2(0.96f, 0.97f), deep: true);

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

            // Header: bronze crest + title (dark ink on light parchment; the prior soft
            // dark drop-shadow now reads as a gentle emboss on the light ground).
            AddLabelShadow(panel.transform, ElarionUi.CrestGlyph + "  INVENTORY", 0.918f, 0.958f,
                           GiltInk, ElarionUi.FontTitle, 0.05f, 0.80f, spacing: 6f);
            AddRule(panel.transform, 0.908f, 0.04f, 0.96f);

            // Close X — top-right, in a deeper-tan parchment chip (so it reads on light).
            AddButton(panel.transform, "X", new Vector2(0.92f, 0.035f), new Vector2(0.916f, 0.962f),
                      new Color(0.847f, 0.804f, 0.710f, 1f), Close, ButtonKind.Neutral);

            // Tabs row (top, under the header) — host so the active pill can be rebuilt.
            _tabsRoot = AddImage(panel.transform, "TabsRow",
                                 new Vector2(0.04f, 0.838f), new Vector2(0.96f, 0.898f), new Color(0, 0, 0, 0));
            NoRaycast(_tabsRoot);
            BuildTabs(_tabsRoot.transform);

            // ── LEFT column (~28%): the paper-doll RING. A recessed aged-parchment
            // alcove the hero "stands" in, with a soft gold rim = a display niche. ──
            var niche = AddImage(panel.transform, "PaperDollNiche",
                                 new Vector2(0.04f, 0.115f), new Vector2(0.335f, 0.822f), StoneNiche);
            AddInnerRim(niche, AccentSoft);
            _paperDoll = AddImage(niche.transform, "PaperDollArea",
                                  new Vector2(0.03f, 0.02f), new Vector2(0.97f, 0.98f), new Color(0, 0, 0, 0));

            // ── RIGHT region (~70%, mockup): the scrollable item grid DOMINATES (top),
            // with a compact horizontal DETAIL / EQUIP strip beneath it. The grid is the
            // mockup's "center/right ~70% item grid (4-5 cols)"; the strip carries the
            // selected-item icon + stats + the EQUIP CTA laid out left-to-right. ──
            _gridRoot = AddImage(panel.transform, "GridArea",
                                 new Vector2(0.355f, 0.305f), new Vector2(0.96f, 0.822f), Track);
            AddInnerRim(_gridRoot, AccentSoft);
            _sidebarRoot = AddImage(panel.transform, "SidebarArea",
                                    new Vector2(0.355f, 0.115f), new Vector2(0.96f, 0.293f), GlassDeep);
            AddInnerRim(_sidebarRoot, AccentSoft);

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
            AddButton(tray.transform, "Sort", new Vector2(0.115f, 0.085f), new Vector2(0.18f, 0.82f),
                      chip, () => { /* TODO owned-list re-sort */ }, ButtonKind.Neutral);
            AddButton(tray.transform, "Filter", new Vector2(0.305f, 0.085f), new Vector2(0.18f, 0.82f),
                      chip, () => { /* TODO owned-list filter */ }, ButtonKind.Neutral);

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

        // ── Paper-doll: the mockup-#41 SIGNATURE — a large ornate CIRCULAR RUNE FRAME
        // with the class-crest hero medallion at its centre and the EQUIPPED-SLOT tiles
        // arranged in a RING around it (not the old left/right columns). The hero name +
        // class + level banner sits above. This is the composed fallback for the
        // aspirational live render.
        //
        // RING LAYOUT — six sockets evenly spaced on a circle (60° apart), placed by
        // trig around the medallion centre so the look is the radial "rune-frame" of the
        // mockup. Two are LIVE gear (WEAPON + ARMOR, driven by GearLoadout); the other
        // four are cosmetic placeholders (HELM / AMULET / TRINKET / BOOTS) shown as dim
        // empty sockets so the ring always reads full. When those slots get real data
        // (cosmetics / accessories catalog) they fill in place — the ring is the frame.
        //
        // TODO live 3D preview — a RenderTexture camera on the hero (isolated layer +
        // key light) would drop a rotating model into the central medallion. Deferred
        // here to avoid touching the live scene camera / hero in-world rendering (this
        // is a visual polish pass that must not change gameplay). The medallion + crest
        // is sized and centred so a RawImage can later replace it 1:1.
        private void RebuildPaperDoll()
        {
            if (_paperDoll == null) return;
            for (int i = _paperDoll.transform.childCount - 1; i >= 0; i--)
                Destroy(_paperDoll.transform.GetChild(i).gameObject);

            string job = HeroJob;
            int level = HeroLevel();

            // Hero name banner (top of the left column).
            AddLabelShadow(_paperDoll.transform, HeroDisplayName(job), 0.935f, 0.99f,
                           Ink, ElarionUi.FontHead, 0.02f, 0.98f, spacing: 1f);
            AddLabel(_paperDoll.transform, Cap(job).ToUpperInvariant() + "   •   LV " + level, 0.895f, 0.93f,
                     InkMicro, ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.02f, 0.98f, spacing: 3f);

            // ── The circular rune frame. Concentric rings = an ornate gilt halo:
            //   outer gilt ring -> aether-tinted mid ring -> inner parchment disc.
            // All built with the rounded sprite (max corner radius => reads circular at
            // these sizes); WebGL-safe (RoundedSprite falls back to a soft quad). The
            // ring lives in a SQUARE band of the left column so the circle isn't ovalled
            // by the column's portrait aspect. ──
            // Square band: centred horizontally, vertical span 0.085..0.575 (~0.49 tall).
            var ringHost = AddImage(_paperDoll.transform, "RuneRing",
                                    new Vector2(0.04f, 0.085f), new Vector2(0.96f, 0.575f), new Color(0, 0, 0, 0));
            NoRaycast(ringHost);

            // Outer gilt halo ring.
            var haloOuter = AddCircle(ringHost.transform, "HaloOuter", 0.5f, 0.5f, 0.50f,
                                      new Color(ElarionUi.Gold.r, ElarionUi.Gold.g, ElarionUi.Gold.b, 0.55f));
            NoRaycast(haloOuter);
            // Aether-tinted mid ring (the runic glow).
            var haloMid = AddCircle(ringHost.transform, "HaloMid", 0.5f, 0.5f, 0.455f,
                                    new Color(ElarionUi.Aether.r, ElarionUi.Aether.g, ElarionUi.Aether.b, 0.20f));
            NoRaycast(haloMid);
            // Inner parchment disc the hero medallion sits on.
            var disc = AddCircle(ringHost.transform, "RingDisc", 0.5f, 0.5f, 0.43f, StoneNiche);
            NoRaycast(disc);

            // A faint rune ring around the disc edge — Elarion magic on the frame.
            var runeRing = AddLabel(ringHost.transform, ElarionUi.RuneGlyphs, 0.86f, 0.99f,
                                    new Color(GiltInk.r, GiltInk.g, GiltInk.b, 0.45f),
                                    ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.05f, 0.95f, spacing: 4f);
            runeRing.raycastTarget = false;

            // ── Center: the class-crest hero medallion (stand-in for the live render). ──
            var medallion = AddCircle(ringHost.transform, "Medallion", 0.5f, 0.5f, 0.30f, StoneBack);
            AddCircleRim(medallion, Accent);
            AddCircle(medallion.transform, "Bloom", 0.5f, 0.5f, 0.40f, AetherSoft);   // centred bloom
            AddLabel(medallion.transform, ClassCrest(job), 0.30f, 0.82f, GiltInk,
                     ElarionUi.FontTitle + 22, TMPro.TextAlignmentOptions.Center, 0.06f, 0.94f, bold: true);
            AddLabel(medallion.transform, Cap(job).ToUpperInvariant(), 0.12f, 0.28f, InkMicro,
                     ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0.04f, 0.96f, spacing: 2f);

            // ── The six ring sockets, placed by angle around the medallion. ──
            WeaponDef w = _loadout != null ? _loadout.EquippedWeapon : null;
            ArmorDef  a = _loadout != null ? _loadout.EquippedArmor  : null;

            // Socket radius (distance from centre, in ringHost-normalised units) + tile
            // half-size. Sockets ride the gap between the medallion (r=0.30) and the disc
            // edge (r=0.43), so ~0.385 keeps them framed inside the ring.
            const float orbit = 0.385f, half = 0.085f;
            // angle 90° = top, going clockwise. Slot mapping around the ring:
            //   top(90)=HELM, upper-right(30)=AMULET, lower-right(-30)=WEAPON,
            //   bottom(-90)=BOOTS, lower-left(-150)=TRINKET, upper-left(150)=ARMOR.
            RingSocket(ringHost.transform, "HELM",   90f,  orbit, half, "", null, "Locked", null, false);
            RingSocket(ringHost.transform, "AMULET", 30f,  orbit, half, "", null, "Locked", null, false);
            RingSocket(ringHost.transform, "WEAPON", -30f, orbit, half,
                       w != null ? WeaponTypeGlyph(w) : "", w != null ? ItemIconCatalog.ForWeapon(w) : null,
                       w != null ? w.name : "Empty", w != null ? w.rarity : null, w != null);
            RingSocket(ringHost.transform, "BOOTS",  -90f, orbit, half, "", null, "Locked", null, false);
            RingSocket(ringHost.transform, "TRINKET", -150f, orbit, half, "", null, "Locked", null, false);
            RingSocket(ringHost.transform, "ARMOR",  150f, orbit, half,
                       a != null ? ArmorTypeGlyph(a) : "", a != null ? ItemIconCatalog.ForArmor(a) : null,
                       a != null ? a.name : "Empty", a != null ? a.rarity : null, a != null);
        }

        // Places one equipped-slot socket on the rune ring at `angleDeg` (90°=top, CW),
        // `orbit` units from centre. Parent is the square ringHost (anchors 0..1), so the
        // polar->cartesian maths is in that square's normalised space (circle stays round).
        private void RingSocket(Transform ringHost, string label, float angleDeg, float orbit, float half,
                                string icon, Sprite iconSprite, string value, string rarity, bool filled)
        {
            float rad = angleDeg * Mathf.Deg2Rad;
            float cx = 0.5f + orbit * Mathf.Cos(rad);
            float cy = 0.5f + orbit * Mathf.Sin(rad);
            EquipSlotTile(ringHost, label, icon, iconSprite, value, rarity, filled,
                          new Vector2(cx - half, cy - half), new Vector2(cx + half, cy + half));
        }

        // A framed equipped-slot tile: rarity-tinted CIRCULAR socket, icon glyph, and a
        // label/value caption beneath it. filled=false renders a dim empty/locked socket
        // so the ring always reads full. Sized small (it's a ring node, not a card).
        private void EquipSlotTile(Transform parent, string label, string icon, Sprite iconSprite, string value,
                                   string rarity, bool filled, Vector2 min, Vector2 max)
        {
            // On light parchment, DARKEN the rarity hue for text/glyph so it stays
            // readable; the frame still uses the bright rarity tint as a thin rim.
            Color rc    = filled ? RarityColor(rarity) : new Color(0.55f, 0.50f, 0.42f, 1f);
            Color rcInk = filled ? RarityInk(rarity)   : InkMicro;

            // Host (transparent) so the circular frame + caption can stack within it.
            var host = AddImage(parent, "SlotHost_" + label, min, max, new Color(0, 0, 0, 0));
            NoRaycast(host);

            // Rarity-tinted circular frame (the "rune socket").
            var frame = AddCircle(host.transform, "SlotFrame_" + label, 0.5f, 0.62f, 0.40f,
                                  new Color(rc.r, rc.g, rc.b, filled ? 0.85f : 0.45f));
            NoRaycast(frame);
            var tile = AddCircle(frame.transform, "Slot_" + label, 0.5f, 0.5f, 0.86f,
                                 filled ? Cell : new Color(Cell.r, Cell.g, Cell.b, 0.65f));
            NoRaycast(tile);

            // Icon (center of the socket) — real item art when present, else a TYPE glyph
            // (empty/locked = a "+" hint).
            Color glyphCol = filled ? Ink : new Color(0.55f, 0.50f, 0.42f, 0.8f);
            string glyph = string.IsNullOrEmpty(icon) ? (filled ? "?" : "+") : icon;
            var iconHost = AddImage(tile.transform, "SlotIcon", new Vector2(0.12f, 0.12f), new Vector2(0.88f, 0.88f),
                                    new Color(0, 0, 0, 0));
            NoRaycast(iconHost);
            AddIcon(iconHost.transform, iconSprite, glyph, ElarionUi.FontBody + 2, glyphCol, 1f);

            // Slot label (spaced micro caps, beneath the socket).
            AddLabel(host.transform, label, 0.10f, 0.24f, InkMicro,
                     ElarionUi.FontMicro, TMPro.TextAlignmentOptions.Center, 0f, 1f, spacing: 1f);
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
            var well = AddImage(cell.transform, "IconWell", new Vector2(0.28f, 0.40f), new Vector2(0.72f, 0.93f),
                                new Color(rc.r, rc.g, rc.b, 0.12f));
            NoRaycast(well);
            AddInnerRim(well, new Color(rc.r, rc.g, rc.b, 0.40f));
            // Sprite-first: real item art when we have it, else the type glyph.
            AddIcon(well.transform, iconSprite, icon, ElarionUi.FontTitle + 2,
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

            // Icon medallion (upper-left of the block).
            var med = AddImage(_sidebarRoot.transform, "DetailIcon",
                               new Vector2(0.060f, 0.40f), new Vector2(0.190f, 0.92f),
                               new Color(rc.r, rc.g, rc.b, 0.14f));
            AddInnerRim(med, new Color(rc.r, rc.g, rc.b, 0.55f));
            AddIcon(med.transform, iconSprite, string.IsNullOrEmpty(icon) ? "?" : icon,
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

            BuildEquipButton(equipped, locked,
                () => { if (_loadout != null) _loadout.EquipArmorById(a.id); },
                () => { /* no explicit unequip in v1 */ });
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

        // A DARKENED rarity hue for TEXT/GLYPHS on the light parchment ground. The
        // bright RarityColor (used for frames/gems/wells) is too pale to read as
        // text on cream, so labels route through this richer, readable variant.
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
            switch ((rarity ?? "common").ToLowerInvariant())
            {
                case "legendary": return "*";
                case "epic":      return "+";
                case "rare":      return "=";
                case "uncommon":  return "-";
                default:          return ".";
            }
        }

        // How loud the rarity frame glows: common is a quiet hairline, legendary a
        // strong gilt halo — so the tiers feel visibly escalating.
        private static float RarityFrameStrength(string rarity)
        {
            switch ((rarity ?? "common").ToLowerInvariant())
            {
                case "legendary": return 0.95f;
                case "epic":      return 0.85f;
                case "rare":      return 0.72f;
                case "uncommon":  return 0.58f;
                default:          return 0.40f;
            }
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

        // A circular Image positioned by CENTER + RADIUS in the parent's normalised
        // space (cx,cy in 0..1; radius in 0..1 of the parent's WIDTH so it stays round
        // when the parent is square). Uses the soft circle sprite; WebGL-safe (falls
        // back to the rounded quad if the circle build fails).
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
            var sprite = CircleSprite;
            if (sprite != null) { img.sprite = sprite; img.type = Image.Type.Simple; }
            else ApplyRounded(img);
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

        // A 1px inner rim hugging an element's edges — gives panels/wells/tiles a
        // crisp framed "depth" without a heavy stone border.
        private void AddInnerRim(GameObject host, Color color)
        {
            var go = new GameObject("Rim", typeof(Image));
            go.transform.SetParent(host.transform, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
            rt.offsetMin = new Vector2(1f, 1f); rt.offsetMax = new Vector2(-1f, -1f);
            var img = go.GetComponent<Image>();
            img.color = new Color(color.r, color.g, color.b, color.a * 0.5f);
            ApplyRounded(img);
            img.raycastTarget = false;
            // Render behind siblings added after it so content stays on top.
            go.transform.SetAsFirstSibling();
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

        // ── Procedural soft CIRCLE sprite (lazily built once; WebGL failure-safe) ──
        // Used for the mockup-#41 rune ring + circular equipped-slot sockets. A solid
        // white disc with a 1px antialiased edge; tint via the Image colour.
        private static Sprite _circle;
        private static bool _circleTried;
        private static Sprite CircleSprite
        {
            get
            {
                if (!_circleTried)
                {
                    _circleTried = true;
                    try { _circle = BuildCircleSprite(); }
                    catch (System.Exception e)
                    {
                        Debug.LogWarning("[HeroInventoryController] circle sprite build failed (rounded quad): " + e.Message);
                        _circle = null;
                    }
                }
                return _circle;
            }
        }

        private static Sprite BuildCircleSprite()
        {
            const int size = 64;
            float c = (size - 1) * 0.5f;
            float rOuter = c;                  // touches the texture edge
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var px = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - c, dy = y - c;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    // 1px antialiased falloff at the rim.
                    float a = Mathf.Clamp01(rOuter - dist);
                    px[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f,
                                 0, SpriteMeshType.FullRect);
        }
    }
}
