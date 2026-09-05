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
using DeNelle.Core.UI.Mvvm;
using DeNelle.Core.Diagnostics;   // FlowTrace — WO-1133 instruments rail selection (§12)
using DeNelle.Village.Hero;
using DeNelle.Village.Items;

namespace DeNelle.Village
{
    /// <summary>Full-screen inventory + gear modal. Singleton; Open()/Close() driven.
    /// WO-434 Phase C: now an <see cref="IPanelView"/> bound to <see cref="InventoryVM"/> —
    /// the View renders ONLY from vm.* and routes taps to VM commands (no direct state pulls).</summary>
    public sealed partial class HeroInventoryController : MonoBehaviour, IPanelView
    {
        public static HeroInventoryController Instance { get; private set; }

        private enum Tab { Weapons, OffHand, Armor, Outfits, Consumables }

        private GameObject _ui;
        private GameObject _stageRoot;    // the selected section's contents (re-built per section)
        private GameObject _paneRoot;     // detail / compare, ALWAYS present (re-built per selection)
        private GameObject _headerRoot;   // hero identity + vitals strip (rebuilt on equip-change)
        private GameObject _railRoot;     // the LEFT RAIL of sections (rebuilt on section change)
        private GameObject _purseRoot;    // wallet + the next-step hint
        private TMPro.TextMeshProUGUI _purseHint;   // the one sentence naming the next step

        // ── THE RAIL (WO-1133 D2) ────────────────────────────────────────────
        // The top tab strip is GONE; sections live in a left rail. These ordinals are the
        // rail's order on screen and nothing else indexes by them, so they are safe to read
        // literally — but they are named because "3" is not a section and "RailTrinkets" is.
        /// <summary>Rail entry one — the gear section (D1: the gear view is PROMOTED, not cut).</summary>
        private const int RailGear     = 0;
        /// <summary>Rail — loose weapons.</summary>
        private const int RailWeapons  = 1;
        private const int RailOffHand  = 2;
        /// <summary>Rail — loose armor.</summary>
        private const int RailArmor    = 3;
        /// <summary>Rail — trinkets (InventoryTabKind.Outfits under the hood).</summary>
        private const int RailTrinkets = 4;
        /// <summary>Rail — potions (InventoryTabKind.Consumables under the hood).</summary>
        private const int RailPotions  = 5;
        /// <summary>Rail — the talent tree. A PSEUDO-section: it routes out via PanelRouter.</summary>
        private const int RailSkills   = 6;
        /// <summary>Rail - realm travel. DORMANT and never built as an entry; it does NOT route
        /// (the flag-gated Bag door was deleted 2026-09-05, WO-1396 - the Realm Map's one door is the
        /// Journey deck card). Kept so the pseudo-section's authored locked sentence still resolves.</summary>
        private const int RailMap      = 7;

        /// <summary>The selected rail entry. Weapons is the landing section, as before.</summary>
        private int _railIndex = RailGear;
        // _profileFrameSprite removed in heavy Tech cleanup — W/A medallion now uses direct pack Profile tabs P1/fill.png (no Rpg legacy).

        // WO-434 Phase C — the bound ViewModel + the model seams injected at the open-site.
        // ALL inventory state/logic now lives in InventoryVM; this View only renders vm.* and
        // routes taps to vm commands. _tab MIRRORS vm.ActiveTab; _railIndex mirrors it back onto
        // the rail (WO-1133) for the four CONTENT sections, and holds its own value for the three
        // pseudo-sections (Gear / Skills / Map) that have no VM tab. The VM is the source of truth.
        private InventoryVM _vm;
        private InventoryStore _store;
        private GearLoadoutEquipTarget _equipTarget;
        private Tab _tab = Tab.Weapons;  // mirrors vm.ActiveTab — kept so the tab-row chrome is untouched.

        private GearLoadout _loadout;     // the live hero's gear model (drives the hero; resolved for the equip target)

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

        // Owner 07-06 "Clicking bag doesnt do anything" (RCA log-proven): the event chain's only
        // listener, HeroEquipHud, is scene-whitelisted and never spawns in Main_Castle_Overworld —
        // both Bag events fired into ZERO subscribers. Register a scene-INDEPENDENT PanelRouter
        // opener at boot so the kit Bag button routes reflection-free through Core, with no scene
        // whitelist to keep in sync. Lazy: nothing spawns until the first real open.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RegisterPanelOpener()
        {
            DeNelle.Core.UI.PanelRouter.Register(DeNelle.Core.UI.PanelId.Inventory,
                () => EnsureExists().Open());
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
        }

        private void OnDestroy()
        {
            DisposeHeroPreview();   // free the paper-doll preview rig + its RenderTexture (no leak)
            DisposeViewModel();
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

            // 1b) WO-434 Phase C — construct the model seams + the pure ViewModel at the open-site
            //     (mirrors ShopPanel.Open injecting EconomyService). The View binds the VM; all
            //     state/logic (owned-list projection, tabs, select->detail, equip routing) lives in it.
            SafeRun(ConstructViewModel, "ConstructViewModel");

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
                    ClearRoots();
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

            // 4) Bind the ViewModel: subscribe to vm.Changed -> Render and paint the initial
            //    state. Render() isolates each section (paperdoll / tabs / grid) so a failure in
            //    one leaves the rest of the modal rendered, not blank.
            _tab = _vm != null ? (Tab)_vm.ActiveTabIndex : Tab.Weapons;
            _railIndex = RailGear;
            SafeRun(() => Bind(_vm), "Bind");

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
            // Owned-count via the store (the inventory model handle the VM resolved) instead of a
            // direct GearCatalog read — strict-MVVM keeps the catalog out of this View.
            int ownedCount = -1;
            try { ownedCount = _store != null ? _store.OwnedCounts.Count : -1; } catch { /* store not ready */ }
            string job = "?";
            try { job = HeroJob; } catch { /* loadout/abilities not ready */ }
            return "[state hero=" + hero
                   + " loadout=" + (_loadout != null ? "present" : "NULL")
                   + " equippedArmor=" + (_loadout != null && _loadout.EquippedArmor != null ? _loadout.EquippedArmor.id : "none")
                   + " job=" + job
                   + " store(owned=" + ownedCount + ")]";
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
            DisposeHeroPreview();   // release the live paper-doll hero preview + its RenderTexture
            DisposeViewModel();
            if (_ui != null) Destroy(_ui);
            _ui = null;
            ClearRoots();
            // Release the modal slot so no invisible backdrop lingers / traps input.
            if (_panelHandle != null) DeNelle.Core.UI.PanelManager.NotifyClosed(_panelHandle);
        }

        /// <summary>Drop every cached host after a teardown, so a half-built root can never be
        /// re-activated broken on the next Open(). One place, so a new zone cannot be forgotten.</summary>
        private void ClearRoots()
        {
            _stageRoot = _paneRoot = _headerRoot = _railRoot = _purseRoot = null;
            _purseHint = null;
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

        // WO-434 Phase C — construct the model seams + the pure ViewModel at the open-site.
        // The View resolves the live VillageInventory + the active hero's GearLoadout (already in
        // _loadout via ResolveLoadout) and injects them as IInventoryStore / IEquipTarget — the VM
        // never names the concretes. Close is supplied as the dismiss command.
        private void ConstructViewModel()
        {
            DisposeViewModel();   // defensive: never leak a prior VM on a re-Open without Close
            _equipTarget = _loadout != null
                ? new GearLoadoutEquipTarget(_loadout, HeroDisplayName(HeroJob), HeroJob)
                : null;
            // DI-in-Open (strict-MVVM): InventoryVM.CreateDefault resolves the inventory model itself
            // (VillageInventory.Instance + EconomyService.Instance) — this View no longer names those
            // singletons. It still builds the equip target (which wraps the live hero loadout) and
            // keeps the returned store handle to dispose. WO-578 store UNION preserved inside the factory.
            _vm = InventoryVM.CreateDefault(_equipTarget, Close, out _store);
        }

        private void DisposeViewModel()
        {
            Unbind();
            _vm?.Dispose();
            _vm = null;
            _store?.Dispose();
            _store = null;
            _equipTarget?.Dispose();
            _equipTarget = null;
        }

        // ── IPanelView ─────────────────────────────────────────────────────────────
        public void Bind(IPanelViewModel vm)
        {
            Unbind();
            _vm = vm as InventoryVM;
            if (_vm == null) return;
            _vm.Changed += Render;
            Render();
        }

        public void Unbind()
        {
            if (_vm != null) _vm.Changed -= Render;
        }

        // Repaint every section from vm.* ONLY. Isolated so one bad section can't blank the modal.
        private void Render()
        {
            if (_vm == null) return;
            _tab = (Tab)_vm.ActiveTabIndex;        // mirror the VM's active tab for the chrome
            // A rail entry that maps to a content section follows the VM; the two PSEUDO-sections
            // (Gear, Skills, Map) have no VM tab, so they hold their own selection.
            if (_railIndex != RailGear && _railIndex != RailSkills && _railIndex != RailMap)
                _railIndex = RailIndexForTab(_tab);

            SafeRun(RebuildHeader, "RebuildHeader");
            SafeRun(RebuildRail,   "RebuildRail");
            SafeRun(RebuildStage,  "RebuildStage");
            SafeRun(RebuildPane,   "RebuildPane");
            SafeRun(RefreshPurseHint, "RefreshPurseHint");
        }

        // ── RAIL SELECTION (WO-1133 D2) ──────────────────────────────────────
        // Content sections route to the VM (it owns Slots + selection); the two pseudo-sections
        // route OUT through PanelRouter exactly as the old pseudo-tabs did. The dormant Map
        // entry selects to its authored locked sentence and NEVER routes: its flag-gated door
        // was deleted (WO-1396) - the Realm Map is opened from the Journey deck only.
        private void SelectRail(int railIndex)
        {
            FlowTrace.Step("Inventory", "Rail select index=" + railIndex + " (was " + _railIndex + ")");

            if (railIndex == RailSkills) { OpenSkillTree(); return; }
            if (railIndex == RailMap)
            {
                _railIndex = RailMap;
                Render();
                return;
            }

            _railIndex = railIndex;
            if (railIndex == RailGear) { Render(); return; }
            SelectTab(RailTab(railIndex));   // raises vm.Changed -> Render
        }

        /// <summary>The VM tab a content rail entry projects. Gear/Skills/Map have none.</summary>
        private static InventoryTabKind RailTab(int railIndex)
        {
            switch (railIndex)
            {
                case RailArmor:    return InventoryTabKind.Armor;
                case RailOffHand:  return InventoryTabKind.OffHand;
                case RailTrinkets: return InventoryTabKind.Outfits;
                case RailPotions:  return InventoryTabKind.Consumables;
                default:           return InventoryTabKind.Weapons;
            }
        }

        /// <summary>The rail ordinal a VM tab lands on — the inverse of <see cref="RailTab"/>.</summary>
        private static int RailIndexForTab(Tab tab)
        {
            switch (tab)
            {
                case Tab.Armor:       return RailArmor;
                case Tab.OffHand:     return RailOffHand;
                case Tab.Outfits:     return RailTrinkets;
                case Tab.Consumables: return RailPotions;
                default:              return RailWeapons;
            }
        }

        private void SelectTab(InventoryTabKind kind) => SelectTab((Tab)(int)kind);

        // ── SECTION SELECTION ───────────────────────────────────
        // Section taps route to the VM; vm.SelectTab rebuilds Slots + resets selection and
        // raises Changed -> Render repaints the rail, the stage and the pane. No local state
        // mutation here — the VM stays the source of truth for which section is open.
        //
        // (RebuildHeader provided by the InventoryPaperDoll partial — which builds the header
        //  band now; the hero CARD, its empty preview box and its VIEW GEAR ribbon were deleted
        //  in WO-1133 and must not come back. See that file's header for why.)
        // (BuildRoot / BuildRail / BuildPurseStrip provided by the InventoryUIBuilder partial)
        private void SelectTab(Tab t)
        {
            if (_vm == null) { _tab = t; return; }
            _vm.SelectTab((int)t);
        }

        private void RebuildRail()
        {
            if (_railRoot == null) return;
            for (int i = _railRoot.transform.childCount - 1; i >= 0; i--)
            {
                // Destroy is deferred to end-of-frame. Detach first so two authoritative
                // Changed events in one frame cannot leave duplicate live raycast targets.
                var child = _railRoot.transform.GetChild(i);
                child.SetParent(null, false);
                Destroy(child.gameObject);
            }
            BuildRail(_railRoot.transform);
        }

        // (RebuildStage + BuildGearSection + BuildItemGrid + BuildGearCell + NoRaycast provided by the InventoryGrid partial)

        // (RebuildPane + BuildEquipAction — the always-present detail/compare pane — provided by the InventorySidebar partial)

        // (SbMidX0/SbMidX1 deleted with the thin detail STRIP they positioned — WO-1133 replaced
        //  that strip with the always-present pane, which owns its own 30% column.)

        // (HELPERS — hero data / rarity / glyphs / uGUI low-levels live in this file as the canonical single definition for the merged partial type)
        private int HeroLevel()
        {
            var prog = _loadout != null ? _loadout.GetComponent<HeroProgression>() : null;
            return prog != null ? prog.Level : 1;
        }

        // (ResolveDisplayArmor + JobEligible retired in WO-434 Phase C: the grid is now a pure
        //  projection of InventoryVM.Slots — owned-vs-class filtering + equipped marks live in the
        //  VM, so the View no longer resolves catalog armor or eligibility itself.)

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
        //
        // WeaponTypeGlyph/ArmorTypeGlyph MOVED to GearIconCatalog.Glyph (strict-MVVM icon-leak
        // seam, UI_MVVM_MIGRATION_PLAN §1): they resolved GearCatalog.Find* inside this View, so
        // the glyph fallback now routes through GearIconCatalog.Glyph(role,id) — verbatim logic.

        // A faint GHOST glyph for an EMPTY equipment slot — hints at the slot TYPE (sword /
        // shield / helm / ring) so an empty socket reads as "weapon goes here", not blank. All
        // BMP glyphs (WebGL/TMP-safe, same bar as the type glyphs).
        private static string SlotGhostGlyph(string label)
        {
            switch ((label ?? "").ToUpperInvariant())
            {
                case "WEAPON":  return "/"; // blade
                case "ARMOR":   return "x"; // mail
                case "HELM":    return "^"; // helm
                case "TRINKET": return "O"; // ring / band
                default:         return "+";
            }
        }

        // Sprite-FIRST consumable icon: prefer the existing sliced item art
        // (ItemIconCatalog); when that's absent, use the RPG pack's framed magic-bottle
        // potion keyed by effect (health=red b1, mana=blue b2, fire/burst=orange b3);
        // null falls through to the TYPE GLYPH in AddIcon. WebGL-safe (RpgUiCatalog
        // loads from Resources only). The pack potions are the cohesive upgrade for the
        // larder cells which otherwise show a bare "+"/"*" glyph.
        //
        // F8-641 ROOT FIX ("shows as poition but says iron scrap"): the pack-potion
        // fallbacks below are only HONEST for an id consumables.json actually owns. They
        // used to run for ANY id, ending in an unconditional health bottle - so an owned
        // crafting material ("IronScrap" -> displayed "Iron Scrap") matched no keyword and
        // was painted as a potion. A row whose catalog does not call it a consumable now
        // returns null and the caller shows THAT row's own glyph.
        private static Sprite ConsumableIcon(string id, string name)
        {
            var art = ItemIconCatalog.ForConsumable(id, name);
            if (art != null) return art;

            if (!ItemIdentity.IsConsumable(id))
            {
                DeNelle.Core.Diagnostics.FlowTrace.Warn("ItemIcon",
                    "ConsumableIcon '" + (id ?? "<null>") + "' is not a consumables.json row (kind="
                    + ItemIdentity.KindOf(id) + ") -> no potion fallback; caller shows the row glyph.");
                return null;
            }

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
        //
        // ⚠ IT WOULD NOT BE BEHIND THE HOST - THIS HAS NO CALLERS, AND MUST NOT GAIN ONE AS
        // WRITTEN. AddCircle here builds a FILLED disc (CircleSprite, or ApplyRounded's filled
        // quad), grown to radius 0.54 against the host's 0.5, parented UNDER the host. A
        // parent's own Graphic draws BEFORE all of its children, so SetAsFirstSibling orders it
        // first among SIBLINGS and it still paints over the host's face - an 85%-alpha gilt wash
        // across the whole circle, not a ring peeking past its edge. Same defect as the skills
        // ConfirmRing and the Journey RAIDS card. To make it real: give it a HOLLOW ring sprite,
        // or build the rim as a SIBLING of the host rather than a child.
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
