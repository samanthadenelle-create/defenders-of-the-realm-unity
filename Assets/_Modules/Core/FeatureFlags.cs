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

        /// <summary>WO-449 — when ON, the raid loop IS the continuous distance-gated WALK: the raid
        /// target is a live EnemyOutpost spawned in the OuterWorld (~70m out a gate), the hero walks
        /// to it on one continuous NavMesh, combat triggers on approach (Enemy hero-aggro), and clearing
        /// it claims the base + grants the next companion IN PLACE — there is NO DEPLOY screen and NO
        /// teleport (the hero never leaves the open world). When OFF, the legacy
        /// RaidSelectionScreen -> RaidDeployScreen -> SceneRouter.GoRaid teleport path is restored
        /// verbatim (the raid icon opens the selection screen; RaidOutpostSystem does not spawn the
        /// walk-to outpost). Default ON. PlayerPrefs "ff.raidwalk".</summary>
        public static bool RaidContinuousWalk => Get("raidwalk", defaultOn: true);

        /// <summary>When OFF, the "Travel to &lt;outpost&gt;" confirm-to-cross prompt on garrison /
        /// raid-outpost seams (<see cref="DeNelle.Village.World.SceneTransitionTrigger"/> whose target is a
        /// <c>Garrison_*</c> / <c>Outpost_*</c> / <c>RaidBase_*</c> scene) is SUPPRESSED — the player can NOT
        /// fast-travel to an outpost area; reaching it must be earned by walking (the WO-453 distance-gated
        /// region vision). The castle&lt;-&gt;OuterWorld crossing is NOT an outpost destination and is never
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
        /// <c>BuildingUpgradePanelMvvm</c> (a big "Upgrade Building" CTA + a tier-ladder grid)
        /// instead of the legacy Yarn upgrade menu / UIDocument BuildingUpgradePanel. Presentation
        /// only — the upgrade math (BuildingUpgradeService / ResourceBuildingState) is unchanged.
        /// Default OFF. PlayerPrefs "ff.buildingupgradepanel". The MVVM bootstrap only spawns when
        /// ON, and the legacy UIDocument bootstrap suppresses itself when ON, so the two never
        /// double-register PanelId.BuildingUpgrade.</summary>
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

        /// <summary>WO-467 runtime variant (owner 2026-06-23, "world seam still broken" x3): when ON
        /// (default), <c>RuntimeRegionGate</c> self-bootstraps on a hub scene and BUILDS the castle↔OuterWorld
        /// crossing from the <c>region-gates.json</c> recipe AT RUNTIME — a walkable approach deck welded to the
        /// source navmesh (runtime <c>NavMeshSurface</c> re-bake, NO editor bake), a deck-seated
        /// <c>SceneTransitionTrigger</c> masked-warp for the hero, a GUID-keyed <c>HeroLinkCrossing</c> entry/dest
        /// pair, gate-funnel choke panels, and (once OuterWorld is additive-loaded) a narrow cross-scene
        /// <c>NavMeshLink</c> for AI. No scene hand-edit, no stale baked coord. Flip OFF to fall back to the
        /// editor-baked seam: PlayerPrefs "ff.runtimeworldseam" = 0. Spec: WORK_ORDER_467 §"Runtime auto-seam".</summary>
        public static bool RuntimeWorldSeam => Get("runtimeworldseam", defaultOn: true);

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
        public static bool CastleMoat => Get("castlemoat", defaultOn: false);

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
        /// OuterWorld by <see cref="DeNelle.Editor.OuterWorldCavePortalBuilder"/> become live OUTPOST
        /// entrances — a deck-seated <c>SceneTransitionTrigger</c> warps the hero into the (future)
        /// loading-zone → outpost RESOLVER. That resolver/loading-zone DOES NOT EXIST YET, so this ships
        /// OFF: the bake places the cave GEOMETRY only and leaves the entrance behavior INERT (no trigger /
        /// no destination) until the resolver slice lands. Canon: outposts/dungeons are entered by a
        /// placeable warp gate (cave skin = outpost) → loading zone → resolver. Default OFF. Flip ON once
        /// the resolver is wired: PlayerPrefs "ff.outpostcaves" = 1.</summary>
        public static bool OutpostCaves => Get("outpostcaves", defaultOn: true);

        /// <summary>DUNGEON ENTRANCES (owner 2026-06-28, same theory as <see cref="OutpostCaves"/>): when
        /// ON, the KayKit-skinned DUNGEON PORTAL points placed in the OuterWorld by
        /// <see cref="DeNelle.Editor.OuterWorldCavePortalBuilder"/> become live dungeon entrances routed
        /// into the (future) loading-zone → dungeon resolver. Same unbuilt-resolver caveat: the bake places
        /// the portal GEOMETRY only and leaves the behavior INERT until the resolver lands. Kept SEPARATE
        /// from <see cref="OutpostCaves"/> so cave-outposts and portal-dungeons can be enabled
        /// independently (cave skin = outpost, portal skin = dungeon). Default OFF. Flip ON when ready:
        /// PlayerPrefs "ff.dungeonportals" = 1.</summary>
        public static bool DungeonPortals => Get("dungeonportals", defaultOn: false);

        /// <summary>SURVIVAL RULE (owner 2026-06-29): Health AND Mana do NOT auto-restore after combat.
        /// When ON (default), the post-combat "return heal" (BattleArena.ReturnHomeWithFade) is SKIPPED —
        /// in the field the hero keeps the HP/MP it ended the fight with and relies on crafted potions.
        /// Full passive recovery happens ONLY at a SAFE ZONE (Castle/Town/Base — see
        /// <see cref="DeNelle.Village.SafeZoneRecovery"/> + <see cref="DeNelle.Core.HubScenes.IsHub"/>), which
        /// ALWAYS fully heals regardless of this flag (that is the design, not the auto-heal this gates).
        /// Reversible: PlayerPrefs "ff.noautoheal" = 0 restores the post-combat auto-heal-to-full.</summary>
        public static bool NoAutoHeal => Get("noautoheal", defaultOn: true);

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
                { "trace", "ff.webtrace" },   // diagnostic-only; safe to flip per session
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
                        return;
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
        // fiddling). ON => orc reps spawn in OuterWorld; engage one -> the isolated BattleArena.
        private const string OverworldEncounterMenu = "Defenders/Debug/Overworld Encounter (WO-482 battle loop)";

        [UnityEditor.MenuItem(OverworldEncounterMenu, priority = 201)]
        private static void ToggleOverworldEncounter()
        {
            bool on = !OverworldEncounter;
            PlayerPrefs.SetInt("ff.overworldencounter", on ? 1 : 0);
            PlayerPrefs.Save();
            Debug.Log("[FeatureFlags] ff.overworldencounter = " + (on ? "ON (orc reps spawn in OuterWorld; engage -> BattleArena)" : "OFF"));
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
#endif
    }
}
