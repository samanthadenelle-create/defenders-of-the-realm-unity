using UnityEngine;

namespace DeNelle.Core
{
    /// <summary>
    /// Central demo/web feature gate. THE DEMO LAW: a reachable feature must either WORK or be
    /// HIDDEN — a broken-but-visible feature is worse than an absent one, especially on the WEB
    /// build (the grant-demo target). Features default to their *proven* state; anything not
    /// verified end-to-end ships OFF and is flipped ON only once it's confirmed ("unflag when proven").
    ///
    /// Each entry point checks the matching flag before spawning/binding/opening. To test a gated
    /// feature without a rebuild, set PlayerPrefs "ff.&lt;name&gt;" to 1 (on) or 0 (off); -1/absent
    /// uses the default below.
    ///
    /// Status set by the 2026-06-16 demo-readiness audit:
    ///   RAID  = ON  — the core loop is closed: RaidVictoryController now subscribes to
    ///           RaidGarrisonSpawner.OnCleared and runs victory -> CLAIM (RaidClaimService +
    ///           SceneOwnership flip player-owned) -> NEXT COMPANION (AddToParty) -> RETURN
    ///           (victory banner + GoCastle, with an auto-return safety timer), so a cleared
    ///           raid no longer soft-locks. The full WO-431 star-scoring/reward SCREEN and the
    ///           WO-441 Phase-C auto-harvest outpost are follow-ups layered on this spine.
    ///   ARENA = ON  — full loop verified (enter→fight→win/lose→reward→return); SKR wallet is an
    ///           intentional client-side MVP stub. Demo-ready.
    /// </summary>
    public static class FeatureFlags
    {
        public static bool Raid  => Get("raid",  defaultOn: true);

        /// <summary>V1 DESCOPE (2026-06-26): the isolated battle Arena is CUT from the V1 build —
        /// V1 is the farm-&gt;build-&gt;level-&gt;raid loop (base = Village2, raid = walk-to EnemyOutpost),
        /// so the Arena entry/reward path ships OFF. Default flipped to false (was true). The Arena
        /// code is gated, NOT deleted — re-enable for V2 via PlayerPrefs "ff.arena" = 1.</summary>
        public static bool Arena => Get("arena", defaultOn: false);

        /// <summary>PIVOT (owner 2026-06-22): SINGLE-HERO combat. When ON, the ATB battle party is
        /// JUST the hero — no pets/companions are surfaced as combatants (see
        /// <see cref="DeNelle.BattleATB.BattleController"/> BuildParty). Companions were a net negative
        /// (they didn't heal/engage), so the direction is one hero + a wider skill tree (heal + ranged).
        /// Default ON. Flag-gated so the hero+pets party is reversible: PlayerPrefs "ff.singlehero" = 0.</summary>
        public static bool SingleHero => Get("singlehero", defaultOn: true);

        // EYES-SWEEP 2026-07-06: ff.herotalents REMOVED (was the OWNER 2026-07-04 consolidation
        // shim). A stale PlayerPrefs "ff.herotalents"=1 re-armed the dead legacy HeroTalents route
        // and the capture fleet rendered panel_HeroTalents fully black. The consolidation is now
        // unconditional: every talents entry point routes to PanelId.HeroSkillTree.

        /// <summary>PIVOT (owner 2026-06-22): Blink armor is JUNKED. When OFF (default), HeroArmorVisual
        /// is inert — no addressable armored-body swap, no rig bone-mapping (which spammed
        /// "ShareBaseSkeleton FAILED" in the F8 logs), and the owner dislikes the look. The hero keeps
        /// its base body; armor/cosmetics move to the Tripo self-rigged direction. Flip ON to restore the
        /// Blink armor swap: PlayerPrefs "ff.blinkarmor" = 1.</summary>
        public static bool BlinkArmor => Get("blinkarmor", defaultOn: false);

        /// <summary>PIVOT (owner 2026-06-22): lock to ONE polished hero (Knight) for now — do it well,
        /// then fold in the other classes. When ON (default), <see cref="DeNelle.Core.State.GameStateService"/>
        /// ChooseHero forces the class to Knight. Flip OFF to restore free class choice:
        /// PlayerPrefs "ff.knightonly" = 0.</summary>
        public static bool KnightOnly => Get("knightonly", defaultOn: true);

        /// <summary>PIVOT (owner 2026-06-22): base-building / CoC base-defense is GATED OFF — NOT on the
        /// V1 critical path. Polish the solo-hero OFFENSE loop first; revisit base-building only if the
        /// polished loop shows it would add value. This gates: convert-on-clear (WO-475 "cleared outpost
        /// -> your base" — base CREATION), the troop auto-defense + watch/continue raid-on-base event, and
        /// any build-a-base UI. V1 outpost reward = skill points / gear (NOT a base). Existing barracks /
        /// WaveManager / towers / GarrisonController stay dormant behind this. Flip ON when V2 is greenlit:
        /// PlayerPrefs "ff.basebuilding" = 1. See docs/COMBAT_PIVOT_NORTHSTAR.md.</summary>
        public static bool BaseBuilding => Get("basebuilding", defaultOn: false);

        /// <summary>WO-612 (owner 2026-07-06): CoC-style construction timers on structure placement —
        /// wires the existing WO-172 BuildTimerService into BuildModeController.Place (15 s base,
        /// 2 free slots, offline-fair). Timer is pacing, never a wall: no free slot = instant build.
        /// Growth path = option-3 "free income" (rewarded-ad skip, no real player cost) — those hooks
        /// exist in the service but stay unsurfaced. Default ON; PlayerPrefs "ff.buildtimers" = 0 to
        /// restore instant builds.</summary>
        public static bool BuildTimers => Get("buildtimers", defaultOn: true);

        /// <summary>WO-449 — when ON, the raid loop IS the continuous distance-gated WALK: the raid
        /// target is a live EnemyOutpost spawned in the merged world (~70m out a gate), the hero walks
        /// to it on one continuous NavMesh, combat triggers on approach (Enemy hero-aggro), and clearing
        /// it claims the base + grants the next companion IN PLACE — there is NO DEPLOY screen and NO
        /// teleport (the hero never leaves the open world). When OFF, the legacy
        /// RaidSelectionScreen -> RaidDeployScreen -> SceneRouter.GoRaid teleport path is restored
        /// verbatim (the raid icon opens the selection screen; RaidOutpostSystem does not spawn the
        /// walk-to outpost). Default ON. PlayerPrefs "ff.raidwalk".</summary>
        public static bool RaidContinuousWalk => Get("raidwalk", defaultOn: true);

        /// <summary>When ON (default), only the family REP/leader roams the overworld; the full
        /// family (leader + followers) spawns in the BattleArena on engage from the recipe carried
        /// by RepEngageWatcher.Init. Owner 2026-07-10: perf — bounded roaming agents (the overworld
        /// followers were redundant; the arena rebuilds the family regardless). OFF = legacy
        /// full-family roam. PlayerPrefs "ff.overworldleaderonlyroam".</summary>
        public static bool OverworldLeaderOnlyRoam => Get("overworldleaderonlyroam", defaultOn: true);

        /// <summary>When OFF, the "Travel to &lt;outpost&gt;" confirm-to-cross prompt on garrison /
        /// raid-outpost seams (<see cref="DeNelle.Village.World.SceneTransitionTrigger"/> whose target is a
        /// <c>Garrison_*</c> / <c>Outpost_*</c> / <c>RaidBase_*</c> scene) is SUPPRESSED — the player can NOT
        /// fast-travel to an outpost area; reaching it must be earned by walking (the WO-453 distance-gated
        /// region vision). The castle hub transitions are NOT outpost destinations and are never
        /// gated by this flag. Default OFF (owner 2026-06-19: "i dont want that as a fast travel option, at
        /// least not yet"). Flip ON via PlayerPrefs "ff.outposttravel" = 1 to restore the travel prompt.</summary>
        public static bool OutpostTravel => Get("outposttravel", defaultOn: false);

        /// <summary>When ON, our decorative CHROME (gilt inner-rim / bottom rule / header shadow+rule /
        /// niche backings + per-panel solid fills + glows) does NOT render, so the Blink "Obsidian" panel
        /// sprite + functional content (text/rows/grid/buttons) show clean. Content/structure and the
        /// world-occluding backdrops are never hidden. Default OFF (current look). PlayerPrefs
        /// "ff.blinkchrome". Gated in ElarionUiKit + per-panel (memory ui-chrome-composition-and-blink-flag).</summary>
        public static bool BlinkChrome => Get("blinkchrome", defaultOn: false);

        /// <summary>WO-443 — when ON, the WebGL build streams its diagnostic logs (FlowTrace +
        /// Unity errors/exceptions) to the backend remote-trace sink (<see cref="DeNelle.Core.Diagnostics.WebTrace"/>)
        /// so a real web player's issue can be triaged from the DB. Default OFF (don't spam the DB).
        /// PlayerPrefs "ff.webtrace". Can also be flipped ON for ONE session via the WebGL URL
        /// query-param <c>?trace=1</c> (see <see cref="ApplyUrlActivationOnce"/>) so support can turn it
        /// on without a rebuild. The sink itself is a clean no-op on standalone/editor and stays dormant
        /// until a backend endpoint is configured.</summary>
        public static bool WebTrace => Get("webtrace", defaultOn: true);

        /// <summary>When ON, tapping an upgradable building opens the code-built MVVM
        /// <c>BuildingUpgradePanelMvvm</c> — the Warcraft-3-style ENHANCEMENT perk grid
        /// (tap a tile to unlock a tier/perk; owner redo 2026-07-02). Presentation only —
        /// the unlock math (BuildingUpgradeService / ResourceBuildingState) is unchanged.
        /// Default ON. PlayerPrefs "ff.buildingupgradepanel". The legacy UIDocument twin was
        /// DELETED 2026-07-02 (audit §3.1); OFF is now a kill-switch (no panel spawns).</summary>
        public static bool BuildingUpgradePanel => Get("buildingupgradepanel", defaultOn: true);

        /// <summary>When ON, opening a weapon/armor shop opens the native code-built MVVM
        /// <c>PartyShopPanelMvvm</c> (party-member selector + tap-to-filter + unified single-tap
        /// buy/equip/sell + real item images + stat/buff deltas) instead of the legacy
        /// <c>ShopPanel</c> (two sell bars, no party selection, blank icons). Presentation +
        /// transaction routing through the proven IEconomy / IInventoryStore / IEquipTarget seams;
        /// the catalog + equip math is unchanged. Default OFF. PlayerPrefs "ff.partyshop". The MVVM
        /// bootstrap only spawns when ON, and CmdOpenShop routes to PanelRouter→PartyShop only when
        /// ON (legacy ShopPanel path when OFF), so the two never double-open.</summary>
        public static bool PartyShop => Get("partyshop", defaultOn: true);

        /// <summary>WO-455 / WO-557 — dialogue runs through OUR code-built system (DeNelle.Core.Dialogue:
        /// data-driven nodes + DialogueRunner + MVVM DialogueView styled via ElarionUiKit). Lifecycle WE
        /// control = no "No node" race, no Stop()-teardown NRE. Default ON (WO-557): YarnSpinner is FULLY
        /// REMOVED — there is no longer a legacy path to fall back to, so the custom sink/View MUST register.
        /// PlayerPrefs "ff.customdialogue".</summary>
        public static bool CustomDialogue => Get("customdialogue", defaultOn: true);

        /// <summary>WO-482 — when ON, the overworld touch-encounter loop is live: wandering enemy "rep" mobs
        /// in the open world that aggro/chase (chase-music sting, wide leash, ~+5% player speed) and, on
        /// engage, transition into an ISOLATED real-time battle arena (the generic <c>BattleArena</c>) where the
        /// single hero (Knight) fights the full Tripo orc family in an OPEN kite arena, then returns to where you
        /// were. Separate from ATB (its own system). Default OFF until the vertical is felt-verified
        /// ("unflag when proven"). PlayerPrefs "ff.overworldencounter". Spec: WORK_ORDER_482. See
        /// docs/COMBAT_PIVOT_NORTHSTAR.md + memory overworld-encounter-isolated-battle.</summary>
        public static bool OverworldEncounter => Get("overworldencounter", defaultOn: true);  // PREVIEW 2026-06-26 (HUD): temporarily ON so the BattleArena runs and the 9-zone HUD renders for the owner to felt-judge. REVERT to false after the HUD decision. (V1 DESCOPE note: arena was CUT for V1; V1 raid = walk-to EnemyOutpost (RaidOutpostSystem). PlayerPrefs "ff.overworldencounter"=0 to force the V1 walk-to raid.)

        /// <summary>WO-473 / PIVOT (owner 2026-06-22): SINGLE-HERO V1 onboarding has NO pet step. When ON
        /// (default), the intro flow skips the PetSelect screen entirely — after the hero pick (Title in-flow
        /// pick OR HeroSelect confirm) the player routes STRAIGHT to the castle (MainCastle_Hall). Hero-pick
        /// persistence (<c>svc.ChooseHero</c>) is preserved; PetSelect persists nothing since the 2026-06-13
        /// pet-acquisition rework (real bonding moved to the in-town Echo Hollow), so bypassing it loses
        /// nothing. The PetSelect scene/controller/router const stay INTACT — flip OFF to restore the
        /// Title/HeroSelect -> PetSelect -> Castle step: PlayerPrefs "ff.bypasspetselect" = 0.</summary>
        public static bool BypassPetSelect => Get("bypasspetselect", defaultOn: true);

        /// <summary>WO-467 runtime variant (owner 2026-06-23, "world seam still broken" x3): when ON,
        /// <c>RuntimeRegionGate</c> self-bootstraps on a hub scene and BUILDS crossing infrastructure
        /// from the <c>region-gates.json</c> recipe AT RUNTIME — a walkable approach deck welded to the
        /// source navmesh (runtime <c>NavMeshSurface</c> re-bake, NO editor bake), a deck-seated
        /// <c>SceneTransitionTrigger</c> masked-warp for the hero, a GUID-keyed <c>HeroLinkCrossing</c> entry/dest
        /// pair, gate-funnel choke panels, and a narrow cross-scene
        /// <c>NavMeshLink</c> for AI. No scene hand-edit, no stale baked coord. Default OFF (2026-07-04):
        /// SUPERSEDED by merged-world (ff.mergedworld ON) — the seam infrastructure is now DEAD CODE pending
        /// removal. Flip to PlayerPrefs "ff.runtimeworldseam" = 1 only to test legacy two-scene seam during
        /// due-diligence unwiring. Spec: WORK_ORDER_467 §"Runtime auto-seam".</summary>
        public static bool RuntimeWorldSeam => Get("runtimeworldseam", defaultOn: false);

        /// <summary>WO-491 — when ON (default), low-HP orcs drive the <c>Injured</c> wounded-stance
        /// locomotion (ActorAnimator.SetInjured from Enemy.DriveAnimator below the HP cutoff). This is
        /// the slide-fix-adjacent locomotion polish; the slide fix itself (Speed param + walk state) is
        /// in the rebuilt controller and needs no flag. Default ON; PlayerPrefs "ff.enemyinjured" = 0
        /// to disable the wounded swap (the orc keeps the healthy locomotion at all HP).</summary>
        public static bool EnemyInjuredStance => Get("enemyinjured", defaultOn: true);

        /// <summary>WO-493 #5 / WO-497 — when ON (default), the HERO reads a wounded look + feel below the
        /// low-HP cutoff (~30%): ActorAnimator.SetInjured drives the Injured locomotion swap on the hero
        /// body, a screen-edge red vignette pulses (HeroInjuredVignette), an optional heartbeat cue plays,
        /// and the hero moves slightly slower (HeroHealth.MoveSpeedMultiplier). Restored on heal above the
        /// cutoff. Mirrors the EnemyInjuredStance polish on the enemy half. Default ON; PlayerPrefs
        /// "ff.heroinjured" = 0 to disable (the hero keeps the healthy stance/speed at all HP).</summary>
        public static bool HeroInjuredStance => Get("heroinjured", defaultOn: true);

        /// <summary>WO-491 — when ON (default), an enemy's RANGED/cast attack is ROOTED + telegraphed:
        /// the NavMeshAgent stops for the cast window (the caster commits, does not slide while casting)
        /// and a WindUp -> Cast animation + audio charge cue play so the strike is readable/dodgeable.
        /// When OFF the legacy instant ranged hit (no root, no telegraph) is restored. Default ON;
        /// PlayerPrefs "ff.enemyrootedcast" = 0 to disable.</summary>
        public static bool EnemyRootedCast => Get("enemyrootedcast", defaultOn: true);

        /// <summary>WO (enemy structure awareness) — DATA-PROVEN fix (headless [Flow:EnemyAggro] run:
        /// wave / overworld-rep enemies spawn with NO EnemyBrain, so their ONLY structure-targeting was
        /// <c>Enemy.ProbeForStructure</c>'s forward-only SphereCast, which missed ~99.7% — they march to
        /// the Heart / roam past defences instead of attacking them). When ON (default),
        /// <see cref="DeNelle.Village.Enemy"/> ALSO runs a short all-direction sweep that lets a brain-less
        /// enemy lock + attack a nearby live structure (a side tower/wall, or the Heart tree) it would
        /// otherwise walk straight past. HERO-PRIMARY is preserved: the sweep is suppressed while the hero
        /// is within aggro range (the verified hero-chase path wins) and it never targets the hero. When
        /// OFF, the exact legacy forward-only probe runs (fully reversible, no rebuild). PlayerPrefs
        /// "ff.enemystructureaware".</summary>
        public static bool EnemyStructureAwareness => Get("enemystructureaware", defaultOn: true);

        /// <summary>ENEMY WEAPONS-IN-HANDS (owner F8 2026-07-04: "enemies spamming weapons in all sorts of
        /// odd ways — maybe we not add a weapon unless we perfect one"). Gates <c>EnemyFactory.AttachEnemyWeapon</c>
        /// (the berserker's held axe seat on CC_Base_R_Hand). When OFF (default), enemies spawn WEAPONLESS —
        /// the grip/prop attach is SKIPPED until the Offset Forge grip is perfected on a single weapon. The
        /// attach code path + NormalizeEnemyProp + AttachmentOffsetRegistry grip are INTACT and reversible:
        /// disabling, not deleting. Flip ON to re-enable held weapons once the grip is dialed in:
        /// PlayerPrefs "ff.enemyweapons" = 1.</summary>
        public static bool EnemyWeapons => Get("enemyweapons", defaultOn: false);

        /// <summary>WO-498 — when ON, the new 9-zone mobile battle HUD (<see cref="DeNelle.Village.Arena.BattleHud9Zone"/>)
        /// spawns alongside <see cref="DeNelle.Village.Arena.BattleArenaHud"/> when a battle stages: a 3x3
        /// tic-tac-toe layout (TL Knight HP+resources, TC enemy family role overview, TR timer+pause,
        /// ML current-target portrait, MR quick-focus, BL joystick (mobile), BC basic attack pill,
        /// BR ability arc with radial cooldown rings). Dark semi-transparent premium-fantasy panels
        /// wired to HeroHealth / HeroAbilities+AbilityCatalog / HeroTargetIndicator. Default OFF — this is
        /// the BONES (WO-498 overnight) that the owner finesses look/feel on tomorrow; togglable without a
        /// rebuild. PlayerPrefs "ff.battlehud9zone" = 1 to preview.
        /// APPLIED 2026-06-23 (owner: "the new 9-slice HUD should be applied"): default flipped ON
        /// so the 9-zone bones spawn on every BattleArena fight via BattleArenaHud.Create ->
        /// BattleHud9Zone.Create. The owner finesses look/feel on top (WO-507). To revert to the
        /// legacy overlay only: PlayerPrefs "ff.battlehud9zone" = 0.
        /// V1 DESCOPE 2026-06-26: the 9-zone HUD belongs to the CUT BattleArena loop, so it ships
        /// OFF for V1. Default flipped to false; PlayerPrefs "ff.battlehud9zone" = 1 to preview (V2).</summary>
        public static bool BattleHud9Zone => Get("battlehud9zone", defaultOn: true);   // PREVIEW 2026-06-26 (HUD): ON so the 9-zone spawns in-arena for the owner to felt-judge. REVERT to false (V2) after the decision.

        /// <summary>WO-579 — the village wave loop AUTO-ARMS its prepare-phase countdown on home-hub
        /// entry (MainCastle_Hall) so a "next wave in MM:SS" timer ticks top-left and the wave AUTO-starts
        /// at zero (towers + hero auto-defend in-hub). The HUD "Start Wave" button is then a manual EARLY
        /// OVERRIDE (skip the remaining countdown), not the only kickoff. Default ON (owner felt-test
        /// 2026-06-28: "start wave was an OVERRIDE, otherwise should AUTO attack"). PlayerPrefs
        /// "ff.waveautostart" = 0 to revert to the old defend-gated (button-only) start.</summary>
        public static bool WaveAutoStart => Get("waveautostart", defaultOn: true);

        /// <summary>WO-579 — DEPRECATED breach→ATB handoff. When an enemy reached the Heart ring the
        /// village wave used to PAUSE and load the flat/static ATBBattle scene (memory
        /// atb-flat-vs-overworld: ATB is the deprecated side-path). Owner felt-test 2026-06-28: the
        /// village wave must resolve IN-HUB (towers + hero auto-defend; enemies contact-damage the Heart;
        /// Heart at 0 = defeat) and must NOT launch ATBBattle. Default OFF → no scene swap, so placed
        /// towers + the wave counter never reset across the (removed) round-trip. The ATB system itself is
        /// untouched (dungeons / sandbox still use SceneRouter.GoBattle). PlayerPrefs "ff.wavebreachtoatb"
        /// = 1 to restore the legacy breach→ATB route.</summary>
        public static bool WaveBreachToAtb => Get("wavebreachtoatb", defaultOn: false);

        /// <summary>WO-591 / owner directive 2026-06-29 ("we should retire ATB"): when ON (default), the
        /// two remaining DUNGEON encounter entry points (<see cref="DeNelle.Dungeons.DungeonStubEncounter"/>
        /// and <see cref="DeNelle.Dungeons.EncounterTrigger"/>) route the fight into the REAL-TIME isolated
        /// <see cref="DeNelle.Village.Arena.BattleArena"/> (BeginEncounter — the verified open-kite combat
        /// stack the overworld + arena already use), instead of loading the FLAT/static ATBBattle scene via
        /// <c>SceneRouter.GoBattle</c>. Canon: the dungeon is a SKIN of the BattleArena; ATB is the weaker
        /// system (static enemy defs, never reads the talent tree/loadout) and is being retired. The arena
        /// stages additively + warps the hero in/out IN the dungeon scene (no scene round-trip), so victory
        /// lands the hero back where the fight started. Default ON. This is a REVERSIBLE RETIRE, not removal:
        /// the ATBBattle scene + BattleController are untouched — set PlayerPrefs "ff.dungeonrealtime" = 0 to
        /// restore the legacy ATB GoBattle path verbatim.</summary>
        public static bool DungeonRealtimeBattle => Get("dungeonrealtime", defaultOn: true);

        /// <summary>Global runtime kill-switch for ALL dev keyboard hotkeys (DevPanel F1, DebugCanvas
        /// F12, AdminOverlay Ctrl+Shift+A, the test spawners J/K/L, the tower dev harness B/J/K/N/U,
        /// the jukebox J open, etc.). Default OFF — so every dev hotkey is DEAD everywhere (editor AND
        /// build) unless a developer explicitly opts in by setting PlayerPrefs "ff.devhotkeys" = 1.
        /// This is the single gate the dev hotkeys check at the top of their key-read; it replaces the
        /// old <c>#if UNITY_EDITOR</c> wraps that left the keys live in the editor (where the owner
        /// tests). Movement (WASD/arrows), weapon skills/spells, F8 capture and F9 are NOT dev hotkeys
        /// and are unaffected by this flag.</summary>
        public static bool DevHotkeys => Get("devhotkeys", defaultOn: false);

        /// <summary>HUB AMBIENT DEPTH (owner 2026-06-23, overnight first-pass) -- when ON (default), the
        /// <see cref="DeNelle.Village.HubAmbientVfxInjector"/> attaches tasteful looping ambient VFX to the
        /// home hub (MainCastle_Hall) at runtime, WITHOUT hand-editing the scene: a soft glowing aura around
        /// the Tree of Life (Heart of Elarion) at plaza centre, plus a small warm flame/ember accent at the
        /// top of each of the 4 castle corner towers. Self-built procedural ParticleSystems rendered with the
        /// committed URP-safe material helper (no gitignored VFX-pack dependency, clean-clone safe). This is
        /// the BONES -- exact effect/scale/colour are tunables in the injector the owner finesses by eye
        /// tomorrow. Default ON so the draft is visible; PlayerPrefs "ff.hubambientvfx" = 0 to disable.</summary>
        public static bool HubAmbientVfx => Get("hubambientvfx", defaultOn: true);

        /// <summary>WO-512 — when ON, the battle SOFT LOCK-ON is live: auto-lock the nearest enemy on
        /// engaging a battle (BattleArena.StageRoutine), tap the HUD Lock toggle to release to free-look,
        /// and switch via the roster/cycle. The single lock owner is <see cref="DeNelle.Village.HeroTargetIndicator"/>
        /// (the HUD routes through it; no duplicate aim writes). Slices 0-1 add NO camera motion and NO
        /// hero-facing change — they are the safe foundation (flag-gated; OFF == today's exact behavior).
        /// Camera framing + face/strafe (slices 2-3) are layered behind this same flag later. Default OFF
        /// until felt-proven (mobile-nausea is the top risk). PlayerPrefs "ff.lockon". Spec: WORK_ORDER_512.</summary>
        public static bool LockOn => Get("lockon", defaultOn: false);

        /// <summary>WO-509 (overnight BONES) — when ON, a diegetic WATER MOAT ring is built around the
        /// MainCastle_Hall perimeter with 4 WIDE DRAWBRIDGE decks at the cardinal gates (N/E/S/W), so the
        /// "you cannot go past here" castle edge READS as deliberate (water = natural impassable boundary)
        /// and the exits read as intentional WIDE crossings, not dead-ends. The bridges are also the
        /// defensive CHOKEPOINTS (single-lane crossings towers/troops cover; raisable to seal a lane is a
        /// future hook) and ARE the WO-509 four RegionGates (only the south is wired today). FIRST-PASS:
        /// visual moat + visible bridge decks only (no footprint shrink, no navmesh re-bake, no functional
        /// N/E/W crossings yet — those are the editor/architect follow per docs/CASTLE_MOAT_DESIGN_NOTE).
        /// Default ON so the owner sees the bones; PlayerPrefs "ff.castlemoat" = 0 to hide. Tunables live in
        /// <see cref="DeNelle.Village.World.CastleMoatBuilder"/>.</summary>
        public static bool CastleMoat => Get("castlemoat", defaultOn: true);

        /// <summary>WO-608 (owner 2026-07-04) — when ON, the game uses the ONE merged scene
        /// <c>Main_Castle_Overworld</c> (castle + full outer world welded into a single continuous
        /// navmesh, built by <see cref="DeNelle.Editor.WorldMergeBuilder"/>) instead of the legacy
        /// two-scene additive model. When ON the seam is GONE, so the runtime SKIPS any additive
        /// world load and SKIPS masked warp / RegionGate crossing (<see cref="DeNelle.Village.RuntimeRegionGate"/>) on the
        /// merged scene — the descent is a plain walk down the 4 bridge ramps on one navmesh. The
        /// additive path + RegionGate primitive stay intact for other hubs / dungeon / outpost / arena
        /// (disable-not-delete). Default OFF until the merged scene is baked + owner felt-verified
        /// (ten-year-old test: descend the ramp, seam invisible). PlayerPrefs "ff.mergedworld" = 1 to
        /// preview once the bake lands.</summary>
        public static bool MergedWorld => Get("mergedworld", defaultOn: true);   // TEST-BUILD 07-04: ON for owner merged-world felt-walk; merge lane commit HELD until verified + felt-approved (set final default per owner verdict)

        /// <summary>WO-602 (fleet-proven P0: <c>HOME_RETURN_FAIL :: gate=&lt;none&gt;</c> x4) — when ON,
        /// <see cref="DeNelle.Village.World.HomeReturnPortalInjector"/> authors four "Enter Elarion"
        /// return portals (SceneTransitionTrigger targeting the hub) at the moat-bridge outer ends,
        /// so a player who leaves the castle always has a discoverable, tap-to-confirm way back to
        /// the courtyard. Runtime-authored (no scene edit, no rebake). PlayerPrefs
        /// "ff.homereturnportal" = 0 to hide/disable.</summary>
        public static bool HomeReturnPortal => Get("homereturnportal", defaultOn: true);

        /// <summary>The editor-baked south CastleBridgeSeam deck (CastleHubBuilder.AddCastleBridgeSeam).
        /// Default OFF (2026-06-29): the editor deck stacked a 2nd navmesh deck on top of the runtime
        /// RuntimeRegionGate south deck, splitting the south navmesh -> spawn->gate PathPartial. South now
        /// uses the runtime-only crossing like W/N/E. Set "ff.castleeditorbridgeseam"=1 to restore.</summary>
        public static bool CastleEditorBridgeSeam => Get("castleeditorbridgeseam", defaultOn: false);

        /// <summary>The runtime gate-finding BEACON pillar (RuntimeRegionGate.BuildGateBeacon) — the tall
        /// white emissive cube + point light at each of the 4 crossings. Owner 2026-06-29: "the white thing"
        /// that doesn't belong; the gates should be VERY SIMPLE AND FLAT. Default OFF so no beacon pillars
        /// render — the crossing still works (deck + trigger). Set "ff.gatebeacon"=1 to restore findability
        /// pillars.</summary>
        public static bool GateBeacon => Get("gatebeacon", defaultOn: false);

        /// <summary>OUTPOST ENTRANCES (owner 2026-06-28: "create a few caves in the bake, we just don't
        /// wire them and flag them on till ready"). When ON, the walk-in CAVE MOUTHS placed in the
        /// merged world by <see cref="DeNelle.Editor.CavePortalBuilder"/> become live OUTPOST
        /// entrances — a deck-seated <c>SceneTransitionTrigger</c> warps the hero into the (future)
        /// loading-zone → outpost RESOLVER. That resolver/loading-zone DOES NOT EXIST YET, so this ships
        /// OFF: the bake places the cave GEOMETRY only and leaves the entrance behavior INERT (no trigger /
        /// no destination) until the resolver slice lands. Canon: outposts/dungeons are entered by a
        /// placeable warp gate (cave skin = outpost) → loading zone → resolver. Default OFF. Flip ON once
        /// the resolver is wired: PlayerPrefs "ff.outpostcaves" = 1.</summary>
        public static bool OutpostCaves => Get("outpostcaves", defaultOn: true);

        /// <summary>DUNGEON ENTRANCES (owner 2026-06-28, same theory as <see cref="OutpostCaves"/>): when
        /// ON, the KayKit-skinned DUNGEON PORTAL points placed in the merged world by
        /// <see cref="DeNelle.Editor.CavePortalBuilder"/> become live dungeon entrances routed
        /// into the (future) loading-zone → dungeon resolver. Same unbuilt-resolver caveat: the bake places
        /// the portal GEOMETRY only and leaves the behavior INERT until the resolver lands. Kept SEPARATE
        /// from <see cref="OutpostCaves"/> so cave-outposts and portal-dungeons can be enabled
        /// independently (cave skin = outpost, portal skin = dungeon). Default OFF. Flip ON when ready:
        /// PlayerPrefs "ff.dungeonportals" = 1.</summary>
        public static bool DungeonPortals => Get("dungeonportals", defaultOn: false);

        /// <summary>WORLD FEEL (owner felt-test 2026-07-01: "world feels empty / very flat / not polished").
        /// When ON (default), <c>DeNelle.Village.World.WorldFeelInjector</c> applies the world-aesthetics
        /// pass at runtime on the outdoor scenes (MainCastle_Hall / Main_Castle_Overworld / Village2), WITHOUT
        /// hand-editing any scene: (1) camera clearFlags forced to Skybox — the hub camera ships
        /// SolidColor near-black (MainCastle_Hall.unity m_ClearFlags:2, bg 0.16/0.17/0.19), which IS the
        /// black-void sky in every screenshot; (2) a dusk "hold the last light" procedural skybox +
        /// warm trilight ambient + low warm sun + soft haze fog; (3) a subtle global URP post volume
        /// (bloom for torch/aura pop, gentle vignette, slight warm grade); (4) drifting ambient motes
        /// around the camera in the open world. Every knob is a tunable const in the injector.
        /// Default ON so the owner feels the draft; PlayerPrefs "ff.worldfeel" = 0 restores the
        /// exact prior look (no rebuild).</summary>
        public static bool WorldFeel => Get("worldfeel", defaultOn: true);

        /// <summary>SURVIVAL RULE (owner 2026-06-29): Health AND Mana do NOT auto-restore after combat.
        /// When ON (default), the post-combat "return heal" (BattleArena.ReturnHomeWithFade) is SKIPPED —
        /// in the field the hero keeps the HP/MP it ended the fight with and relies on crafted potions.
        /// Full passive recovery happens ONLY at a SAFE ZONE (Castle/Town/Base — see
        /// <see cref="DeNelle.Village.SafeZoneRecovery"/> + <see cref="DeNelle.Core.HubScenes.IsHub"/>), which
        /// ALWAYS fully heals regardless of this flag (that is the design, not the auto-heal this gates).
        /// Reversible: PlayerPrefs "ff.noautoheal" = 0 restores the post-combat auto-heal-to-full.</summary>
        public static bool NoAutoHeal => Get("noautoheal", defaultOn: true);

        /// <summary>Combat-feel polish layer (2026-07-02 arena feel pass) — when ON (default), the
        /// presentation-side feel additions run: recorded sword-clash / enemy-death SFX VARIANT pools
        /// (GameSfx / EnemyCombatAudio pick a random authored take per hit instead of one repeated
        /// clip), and the arena stage RETHEME (stone-biome fights swap the forest-clearing green
        /// lawn + toy-tree ring for the biome's stone ground + rock-only silhouette so the floor
        /// matches the colosseum backdrop — owner F8 "this looks awful" visual-vocabulary clash).
        /// Pure presentation: no damage, timing or AI change. OFF restores the previous look/sound
        /// exactly. PlayerPrefs "ff.combatfeel" = 0 to disable.</summary>
        public static bool CombatFeel => Get("combatfeel", defaultOn: true);

        /// <summary>WO-T1 (Tutorial V2, docs/TUTORIAL_V2_SPEC_2026-07-02.md) — when ON, the data-driven
        /// tutorial runs: <c>tutorial-steps.json</c> walked by the thin <c>DeNelle.Village.TutorialFlow</c>
        /// interpreter (7 mandatory steps + the contextual one-shot registry), Sylas speaking through the
        /// standard dialogue template, completion signals via <c>TutorialSignals</c>, tutorial_* telemetry
        /// through EventTracker. The legacy <c>TutorialDirector</c> FTUE stands down while this is ON
        /// (it is deleted only in WO-T5, after the flip is fleet-verified + owner felt-verified).
        /// Default ON since 2026-07-03: the WO-T3/T4 self-driving fixes landed (prepaid-tower grant,
        /// scripted town wave, staged world rep, contextual triggers) — V2 is now the runnable FTUE.
        /// PlayerPrefs "ff.tutorialv2" = 0 to force the legacy director.</summary>
        public static bool TutorialV2 => Get("tutorialv2", defaultOn: true);

        /// <summary>Hero package pipeline (owner ruling 2026-07-03: the PALADIN is the new Knight body) —
        /// when ON (default), the runtime hero pipeline loads the published PALADIN hero package for the
        /// Knight: <c>Resources/Heroes/KnightPackage.prefab</c> (a variant of Knight_Hero.fbx that BINDS
        /// <c>KnightPackage.controller</c>, the full posture-tree controller) instead of the legacy Tripo
        /// armored Knight. The package prefab carries its own Animator + controller + baked sword/shield/
        /// helmet, and runs at a single 1.0 cadence authority (not the legacy 0.5 global anim speed).
        /// OFF restores the legacy Tripo Knight (legacy slug "Knight", 0.5 anim speed, +15 forward yaw)
        /// exactly. A failed package load also degrades to the legacy Knight so the hero is never bodyless.
        /// PlayerPrefs "ff.heropackage" = 0 to force the legacy Tripo Knight.</summary>
        public static bool HeroPackage => Get("heropackage", defaultOn: true);

        /// <summary>KnightV3 hero body (owner "try this" 2026-07-03) — when ON (default), the Knight loads
        /// the owner's NEW <c>Resources/Heroes/KnightV3.fbx</c> body: a Character-Creator / AccuRIG export
        /// (CC_Base_* skeleton auto-mapped to a STANDARD Unity humanoid, one embedded 'Material_Pbr'
        /// diffuse, embedded WALK + custom DANCE clips). It retargets the shared Knight animations via the
        /// proven <c>Knight.controller</c> (locomotion + injured + cast states), keeps its OWN embedded
        /// texture (RetargetMaterialsToUrp), and falls back to a flat color only on null-albedo slots.
        /// Checked BEFORE ff.heropackage, so V3 supersedes the Paladin package for the Knight. A failed V3
        /// load degrades to the legacy Tripo Knight (never bodyless). PlayerPrefs "ff.knightv3" = 0 to
        /// restore the Paladin package / legacy Knight.</summary>
        public static bool KnightV3 => Get("knightv3", defaultOn: true);

        /// <summary>STUDIO-MOCAP KNIGHT LOCOMOTION (owner 2026-07-04) — when ON, the KnightV3 body binds
        /// the studio-mocap locomotion twin <c>Resources/Heroes/KnightMocap.controller</c> instead of
        /// <c>Knight.controller</c>. KnightMocap is IDENTICAL to the Knight controller EXCEPT its
        /// Locomotion 1-D blend tree Idle/Walk/Run sources are the professional sword+shield studio-mocap
        /// clips (idle_ready / walkforward01 / runforward_218667 from
        /// Assets/Action/Knight/Motion/studio-mocap-sword-and-shield-moves/) — HUMANOID on the SAME CC_Base
        /// rig, so the retarget is ~1:1 (no lossy cross-rig Mixamo look, the "off" walk the owner flagged).
        /// Cast/Attack/Hit/Death/Block/Victory/Injured/UpperBody are unchanged. Default OFF until the owner
        /// felt-approves — the existing Knight.controller is byte-untouched when OFF (a missing KnightMocap
        /// controller also degrades to Knight). idle/walk/run FORWARD only this pass; strafe/turn/combat are
        /// Phase 2. PlayerPrefs "ff.mocaploco" = 1 to preview.</summary>
        public static bool MocapLocomotion => Get("mocaploco", defaultOn: true);   // 2026-07-04: owner felt-approved ("player feels ok") — ON. Studio-mocap idle/walk/run FORWARD; strafe/turn/shield-carry = Phase 2.

        /// <summary>WO-478 — DEPRECATED geometry grip inference for NATIVE melee props (Blink
        /// grip-at-origin prefabs such as sword_A). When ON, native melee weapons discard the
        /// authored pivot and run NormalizeInto + SeatHiltLowerHalf + ComputeMeleeGripRotation
        /// (the pre-WO-478 path superseding stale WO-435). Default OFF → native melee trusts
        /// SeatNative (authored grip-at-origin + scale + per-archetype nudge only). PlayerPrefs
        /// "ff.weapongripinfer" = 1 to restore the legacy inference path.</summary>
        public static bool WeaponGripInfer => Get("weapongripinfer", defaultOn: false);

        /// <summary>SEEKERTHON stake-rewards DEMO surface — when ON, <see cref="DeNelle.Core.Platform.StakeRewardsDemoBootstrap"/>
        /// seeds a real-looking active native SKR stake (~1M, a Genesis holder) into
        /// <see cref="DeNelle.Core.Platform.StakeRewardsResolver"/> and auto-opens the read-only
        /// <see cref="DeNelle.Core.UI.StakeRewardsPanel"/>, so the video shows "active stake -> in-game
        /// rewards, automatically, we never hold your SKR" WITHOUT any live wallet. Purely presentation +
        /// a mock READ — it custodies nothing, mutates no game state, and mints nothing (canon
        /// skr-separate-ingame-currency-real-token-readonly). Default OFF so production NEVER shows it.
        /// Forceable ON for ONE WebGL session via the page URL <c>?stakedemo=1</c> (allow-listed in
        /// <see cref="ApplyUrlActivationOnce"/>) — no rebuild, no prod-default change. Off-web:
        /// PlayerPrefs "ff.stakedemo" = 1 (or the Defenders/Debug editor menu).</summary>
        public static bool StakeDemo => Get("stakedemo", defaultOn: false);

        /// <summary>"POWERED WITH SKR" GRANT PREVIEW (owner 2026-07-04) — when ON, the aspirational SKR
        /// integration story is presentable for the grant recording: a "Powered with SKR" badge appears
        /// on the Title screen and opens <see cref="DeNelle.Core.UI.SkrShowcasePanel"/> — the branding +
        /// the honest value-prop (real token / non-custodial / server-verified / cosmetic-only), clearly
        /// stamped "PREVIEW · TESTNET — NOT LIVE". Every action is a NO-OP or opens the read-only
        /// StakeRewardsPanel; NOTHING calls a wallet, signs, or moves funds (the pay rail stays the devnet
        /// StubWalletProvider). Default OFF so normal players never see the aspirational flow. The
        /// grant-recording build flips it ON: the Defenders/Debug menu, PlayerPrefs "ff.skrpreview" = 1,
        /// or the WebGL URL <c>?skrpreview=1</c> (allow-listed in <see cref="ApplyUrlActivationOnce"/> —
        /// read-only presentation, so a crafted link can at most show an info panel).</summary>
        // Owner 2026-07-06: demo/grant recording — badge + showcase ON by default (the panel is
        // self-labeling: PREVIEW · TESTNET stamp, no wallet call). "ff.skrpreview" = 0 to hide.
        public static bool SkrPreview => Get("skrpreview", defaultOn: true);

        /// <summary>WO-611 — when ON, the owner-designed COMBAT HUD renders inside the HudKit: a
        /// VIRTUAL D-PAD (cross/plus, steel body + gold chevrons + centre hub) for movement instead of
        /// the 4-round-button cluster; an oblong stadium ATTACK PILL (gold-trimmed, energy-sword) at the
        /// bottom-right thumb anchor; the Q/W/E/R abilities as round gold MEDALLIONS arcing up-left around
        /// the pill with a SOFT under-glow cooldown (not a hard clock sweep); the hot-swap action bar HOUSED
        /// in an obsidian panel with a gold-trim inner ring; the HP/MP bars recessed in an inset WELL inside
        /// the vitals plate; and an animated LOCK CROSSHAIR badge on the target frame (hud/crosshair_1|2|3:
        /// unlocked -> acquiring -> locked, bound to TargetModel.HasTarget/Locked). On the posture flip to
        /// hostile(prebattle|activebattle) it also calls PanelManager.CloseAll() so every other screen closes
        /// and ONLY the combat HUD renders. Default OFF — the shipping HUD is BYTE-IDENTICAL when OFF (every
        /// combat-HUD widget is flag-gated at its build site). PlayerPrefs "ff.combathud611" = 0 reverts.
        /// DEFAULT ON (owner 2026-07-08, F8-25): the v8 combat HUD is the approved design — after the
        /// FULL RESET wiped her PlayerPrefs override, the legacy battle HUD (old arrow pad / legacy
        /// right-thumb layout) resurfaced because this still defaulted OFF. Approved = default.</summary>
        public static bool CombatHud611 => Get("combathud611", defaultOn: true);

        /// <summary>2026-07-07 sheathed-pose fallback (owner A/B): when a weapon has NO explicit
        /// "&lt;mesh&gt;@sheathed" registry entry, its DRAWN offset falls back onto the built-in back
        /// pose. OFF (default) = the fallback nudges POSITION ONLY — frame-safe, since the drawn
        /// rotation was authored in the HAND frame and composing it onto the chest-socket sheathe
        /// rotation is a frame mismatch (e.g. sword_A's drawn euler (117,-61,-111) swings the back
        /// carry arbitrarily). ON = the 0492d7dc behavior (position + rotation compose) as the
        /// BACKUP if position-only doesn't carry the town fix. Explicit @sheathed entries are
        /// identical under both. PlayerPrefs "ff.sheathdrawnrot" = 0 to flip back to pos-only.
        /// 2026-07-07 owner A/B: default ON — pos-only preserved the exact plank pose she flagged
        /// (felt-verdict from flag_00 10:25); trying the full compose next. Toggle lives in the
        /// OwnerDevToolsOverlay flag list for live comparison.</summary>
        public static bool SheathedDrawnRotFallback => Get("sheathdrawnrot", defaultOn: true);

        /// <summary>PET COMBAT (owner 2026-07-08) — gates whether deployed pets FIGHT. When OFF (default),
        /// pets are HARVEST / COMPANION only per docs/COMBAT_PIVOT_NORTHSTAR.md ("no pets in battle"; pets =
        /// autonomous harvesters in V1): a Defend pet's hunt/target/attack loop (<see cref="DeNelle.Pets.Pet"/>
        /// Update/Attack/OverlapSphere scan) NO-OPs — the pet acquires no target and deals no damage, and it
        /// earns no combat XP (<see cref="DeNelle.Pets.PetProgression"/> does not register as an IXpEarner).
        /// The pet stays alive as a companion and <see cref="DeNelle.Pets.PetHarvester"/> keeps gathering
        /// (harvest no longer yields to a fight that can't happen). When ON, the full pet combat behaviour is
        /// restored. Default OFF — reversible: PlayerPrefs "ff.petcombat" = 1.</summary>
        public static bool PetCombat => Get("petcombat", defaultOn: false);

        /// <summary>Owner directive 2026-07-10: the ENTIRE Barracks feature is hidden for V1 —
        /// structure (building/mesh), the drillmaster NPC, and all barracks dialogue/training.
        /// When OFF (default): the baked CastleBarracks is hidden at runtime, BarracksNpcInjector
        /// no-ops, and the barracks dialogue/training entry points are unreachable. Disable-not-
        /// delete; flip ON for V2 via PlayerPrefs "ff.barracks" = 1.</summary>
        public static bool Barracks => Get("barracks", defaultOn: false);

        /// <summary>WO-703 / ticket BLANK-1 (owner ruling 2026-07-13 "should be completely flagged
        /// off for now"): the Colosseum / arena-entrance structure visual in the home hub. When OFF
        /// (default), <see cref="DeNelle.Village.HubStructureVisualInjector"/> skips the
        /// Colosseum_ArenaEntrance placement entirely — the structure model, its fitted
        /// StructureCollider, and anything parented to that host (attached emitters included) never
        /// spawn. Disable-not-delete; reversible via PlayerPrefs "ff.colosseum" = 1. The arena's
        /// interaction path (ArenaHeraldSpawner) stays independently gated by "ff.arena".</summary>
        public static bool Colosseum => Get("colosseum", defaultOn: false);

        /// <summary>WO-VFX-POI (owner is red/green colorblind — callouts read by MOTION / SHAPE /
        /// LUMINANCE / VERTICALITY, never hue): when ON, <see cref="DeNelle.Village.PoiCalloutSystem"/>
        /// self-bootstraps and drives point-of-interest callouts off <see cref="DeNelle.Village.PoiRegistry"/>
        /// — a small looping ground AURA on near-field harvest nodes (mine reserves / harvest sites /
        /// active collectors) that appears within ~28m and hands off to the interact prompt on arrival
        /// (capped to the nearest ~6 to respect the VFX loop budget), plus a tall looping PILLAR/beacon on
        /// far-field landmarks (enemy fortress outposts) visible from range until cleared. Presentation
        /// only — no gameplay/economy change; null-safe (no-ops until the "Poi_*" catalog keys exist).
        /// Default OFF (dark-ship until the catalog rows are generated + owner felt-verifies). PlayerPrefs
        /// "ff.poicallouts" = 0 to disable.</summary>
        // Owner asked to felt-test the node auras + fortress beacon (2026-07-10) — flipped ON for
        // build 2; the catalog Poi_* keys are authored. Set ff.poicallouts=0 to turn off if too busy.
        public static bool PoiCallouts => Get("poicallouts", defaultOn: true);

        // WO-682 (owner ruling 2026-07-12 "have that ff removed and set to lock in build"):
        // ff.strategicplacement is REMOVED — strategic building placement (WO-673) is
        // ALWAYS ON in every build. All former call sites are the unconditional TRUE path.

        /// <summary>Per-feature resolve: PlayerPrefs override ("ff.&lt;name&gt;" = 0/1) wins, else the default.</summary>
        private static bool Get(string name, bool defaultOn)
        {
            int pref = PlayerPrefs.GetInt("ff." + name, -1);
            if (pref == 0) return false;
            if (pref == 1) return true;
            return defaultOn;
        }

        // ── WO-443 — WebGL one-session URL activation (?trace=1) ──────────────────
        private static bool s_urlActivationChecked;

        // SECURITY (audit B-URLFLAG): a URL query can ONLY activate flags on this explicit
        // allow-list. Anything else (gameplay / economy / monetization flags) is rejected so
        // a crafted link can never flip game state. Today the only URL-activatable flag is the
        // diagnostic web-trace toggle. Map: URL query key → PlayerPrefs flag key.
        private static readonly System.Collections.Generic.Dictionary<string, string> s_urlActivatableFlags =
            new System.Collections.Generic.Dictionary<string, string>(System.StringComparer.OrdinalIgnoreCase)
            {
                { "trace", "ff.webtrace" },      // diagnostic-only; safe to flip per session
                // Seekerthon stake-rewards DEMO. Safe to allow-list despite the "no monetization flags"
                // rule because the surface is READ-ONLY PRESENTATION: it opens an informational panel
                // over a MOCK stake and mutates NO game/economy state, so a crafted ?stakedemo=1 link
                // can at most show a cosmetic panel — it can never flip real state. Default OFF => prod
                // is unaffected unless the demo link is used.
                { "stakedemo", "ff.stakedemo" },
                // "Powered with SKR" grant PREVIEW. Same rationale as stakedemo: READ-ONLY
                // presentation (branding + a labeled testnet preview) — no wallet call, no state
                // mutation — so a crafted ?skrpreview=1 link can at most show an info panel. Default
                // OFF => prod is unaffected unless the grant-recording link is used.
                { "skrpreview", "ff.skrpreview" },
            };

        /// <summary>
        /// WO-443 — reads <see cref="Application.absoluteURL"/> on WebGL and, if it carries
        /// <c>?trace=1</c> (or <c>&amp;trace=1</c>), turns the <see cref="WebTrace"/> flag ON for THIS
        /// session only (writes PlayerPrefs "ff.webtrace"=1) so support can activate web tracing for a
        /// single player without a rebuild. Idempotent (runs its parse once) and safe to call on every
        /// platform — on editor/standalone <c>absoluteURL</c> is empty so it is a no-op. Never throws.
        /// </summary>
        public static void ApplyUrlActivationOnce()
        {
            if (s_urlActivationChecked) return;
            s_urlActivationChecked = true;
            try
            {
                string url = Application.absoluteURL;
                if (string.IsNullOrEmpty(url)) return;

                int q = url.IndexOf('?');
                if (q < 0) return;
                string query = url.Substring(q + 1);

                foreach (var pair in query.Split('&'))
                {
                    int eq = pair.IndexOf('=');
                    string key = (eq < 0 ? pair : pair.Substring(0, eq)).Trim();
                    string val = (eq < 0 ? "" : pair.Substring(eq + 1)).Trim();

                    // Allow-list gate (B-URLFLAG): only diagnostic flags here may be set via URL.
                    if (!s_urlActivatableFlags.TryGetValue(key, out string prefKey))
                    {
                        if (!string.IsNullOrEmpty(key))
                            Debug.LogWarning($"[FeatureFlags] URL flag '{key}' is NOT URL-activatable — rejected.");
                        continue;
                    }

                    if (val == "1" || val.Equals("true", System.StringComparison.OrdinalIgnoreCase))
                    {
                        PlayerPrefs.SetInt(prefKey, 1);
                        PlayerPrefs.Save();
                        Debug.Log($"[FeatureFlags] ?{key}=1 detected — '{prefKey}' activated for this session.");
                        // continue (not return) so MULTIPLE allow-listed flags in one URL all activate
                        // (e.g. ?trace=1&stakedemo=1). Each is independently allow-list-gated above.
                        continue;
                    }
                }
            }
            catch (System.Exception ex)
            {
                Debug.LogWarning("[FeatureFlags] URL trace-activation parse skipped: " + ex.Message);
            }
        }

#if UNITY_EDITOR
        // ── Editor flag toggles (Defenders > Debug) — no registry editing, no Play Mode needed.
        // Flip, then re-open the panel to see it. Checkmark shows the current resolved state.
        private const string BlinkChromeMenu = "Defenders/Debug/Blink Chrome (hide our UI dressing)";

        [UnityEditor.MenuItem(BlinkChromeMenu, priority = 200)]
        private static void ToggleBlinkChrome()
        {
            bool on = !BlinkChrome;                       // resolved value, then invert
            PlayerPrefs.SetInt("ff.blinkchrome", on ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log("[FeatureFlags] ff.blinkchrome = " + (on ? "ON (Blink panels show clean)" : "OFF (our chrome)"));
        }

        [UnityEditor.MenuItem(BlinkChromeMenu, validate = true)]
        private static bool ToggleBlinkChromeValidate()
        {
            UnityEditor.Menu.SetChecked(BlinkChromeMenu, BlinkChrome);
            return true;
        }

        // WO-482 — flip the overworld-encounter battle loop on/off from the menu (no PlayerPrefs
        // fiddling). ON => orc reps spawn in the merged world; engage one -> the isolated BattleArena.
        private const string OverworldEncounterMenu = "Defenders/Debug/Overworld Encounter (WO-482 battle loop)";

        [UnityEditor.MenuItem(OverworldEncounterMenu, priority = 201)]
        private static void ToggleOverworldEncounter()
        {
            bool on = !OverworldEncounter;
            PlayerPrefs.SetInt("ff.overworldencounter", on ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log("[FeatureFlags] ff.overworldencounter = " + (on ? "ON (orc reps spawn in merged world; engage -> BattleArena)" : "OFF"));
        }

        [UnityEditor.MenuItem(OverworldEncounterMenu, validate = true)]
        private static bool ToggleOverworldEncounterValidate()
        {
            UnityEditor.Menu.SetChecked(OverworldEncounterMenu, OverworldEncounter);
            return true;
        }

        // WO-512 — flip the battle soft lock-on on/off from the menu (no PlayerPrefs fiddling).
        // ON => auto-lock nearest enemy on engage + HUD lock toggle routes through HeroTargetIndicator.
        private const string LockOnMenu = "Defenders/Debug/Lock-On (WO-512 battle soft lock)";

        [UnityEditor.MenuItem(LockOnMenu, priority = 202)]
        private static void ToggleLockOn()
        {
            bool on = !LockOn;
            PlayerPrefs.SetInt("ff.lockon", on ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log("[FeatureFlags] ff.lockon = " + (on ? "ON (auto-lock nearest enemy on engage; HUD lock routes through HeroTargetIndicator)" : "OFF"));
        }

        [UnityEditor.MenuItem(LockOnMenu, validate = true)]
        private static bool ToggleLockOnValidate()
        {
            UnityEditor.Menu.SetChecked(LockOnMenu, LockOn);
            return true;
        }

        // Seekerthon — flip the stake-rewards demo surface from the menu (no PlayerPrefs fiddling).
        // ON => a seeded ~1M SKR stake opens the read-only StakeRewardsPanel on play.
        private const string StakeDemoMenu = "Defenders/Debug/Stake Rewards Demo (Seekerthon SKR)";

        [UnityEditor.MenuItem(StakeDemoMenu, priority = 203)]
        private static void ToggleStakeDemo()
        {
            bool on = !StakeDemo;
            PlayerPrefs.SetInt("ff.stakedemo", on ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log("[FeatureFlags] ff.stakedemo = " + (on ? "ON (seeded ~1M SKR stake opens StakeRewardsPanel on play)" : "OFF"));
        }

        [UnityEditor.MenuItem(StakeDemoMenu, validate = true)]
        private static bool ToggleStakeDemoValidate()
        {
            UnityEditor.Menu.SetChecked(StakeDemoMenu, StakeDemo);
            return true;
        }

        // "Powered with SKR" grant PREVIEW — flip the aspirational SKR story on/off from the menu.
        // ON => a "Powered with SKR" badge shows on the Title screen and opens the SkrShowcasePanel.
        private const string SkrPreviewMenu = "Defenders/Debug/Powered with SKR (grant preview)";

        [UnityEditor.MenuItem(SkrPreviewMenu, priority = 204)]
        private static void ToggleSkrPreview()
        {
            bool on = !SkrPreview;
            PlayerPrefs.SetInt("ff.skrpreview", on ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log("[FeatureFlags] ff.skrpreview = " + (on ? "ON (Title 'Powered with SKR' badge -> SkrShowcasePanel; no wallet call)" : "OFF"));
        }

        [UnityEditor.MenuItem(SkrPreviewMenu, validate = true)]
        private static bool ToggleSkrPreviewValidate()
        {
            UnityEditor.Menu.SetChecked(SkrPreviewMenu, SkrPreview);
            return true;
        }

        // WO-611 — flip the owner-designed combat HUD on/off from the menu (no PlayerPrefs fiddling).
        // ON => virtual d-pad + attack pill + Q/W/E/R medallion arc + housed action bar + HP/MP inset
        // + lock crosshair badge; hostile posture closes all other screens (PanelManager.CloseAll).
        private const string CombatHud611Menu = "Defenders/Debug/Combat HUD (WO-611 owner-designed)";

        [UnityEditor.MenuItem(CombatHud611Menu, priority = 205)]
        private static void ToggleCombatHud611()
        {
            bool on = !CombatHud611;
            PlayerPrefs.SetInt("ff.combathud611", on ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log("[FeatureFlags] ff.combathud611 = " + (on
                ? "ON (owner-designed combat HUD: d-pad/pill/medallion-arc/housed-bar/HP-MP-inset/lock-crosshair; hostile -> CloseAll)"
                : "OFF (shipping HUD unchanged)"));
        }

        [UnityEditor.MenuItem(CombatHud611Menu, validate = true)]
        private static bool ToggleCombatHud611Validate()
        {
            UnityEditor.Menu.SetChecked(CombatHud611Menu, CombatHud611);
            return true;
        }
#endif
    }
}
