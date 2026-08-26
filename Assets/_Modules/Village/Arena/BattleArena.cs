// =============================================================================
// BattleArena — the GENERIC isolated real-time battle controller (WO-482).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Arena
//
// Owner directive 2026-06-23: "redo arena as a more generic class to handle both"
// + "no mapping of structures in battle, just a large enough arena to fight and
// kite." This is the GENERIC battle spine the overworld ENCOUNTER (PvE, this file's
// BeginEncounter) drives; the verified async-PvP ArenaMode keeps working untouched
// and is generalized ONTO this spine as a SEPARATE, regression-guarded follow-up
// (generalize-by-extraction, never rewrite the verified path in the risky step).
//
// THE LOOP (PvE encounter): engage -> build an OPEN kite arena (a large bounded
// floor + runtime NavMesh, NO fort/structures) staged at a far offset so it is
// isolated from the open world (which stays in memory, the owner's additive/keep-
// in-memory intent) -> warp the hero in (south) + spawn the enemy family (north)
// via the SHARED EnemyFactory + EnemyBrain roles -> REAL-TIME fight via the EXISTING
// combat stack (PlayerAttackController / HeroAbilities / hero-aggro DEF-224 /
// HeroHealth -- ZERO new combat code) -> WIN (all enemies dead) / LOSE (hero down)
// / FLEE -> reward + warp the hero back to the engagement spot -> OnBattleEnded.
//
// LOGIC vs PRESENTATION (HP-B2B law): this controller is LOGIC (build/spawn/watch/
// resolve/return + which abilities the skill tree allows). Models/anim/VFX/HUD are
// the PRESENTATION layer it reuses (EnemyFactory skins, HeroAbilities VFX, the HUD
// bridge) -- it never bakes presentation in.
//
// REUSE (CLAUDE.md "use items we have"): ArenaNavMeshBaker (runtime NavMesh),
// EnemyFactory/EnemyBrain (the orc family), BattleLock (input gate), CoreServices.
// Audio (BGM), HeroHealth/HeroLocomotion (hero), ArenaHudBridge (HUD show/hide).
//
// Instrumented per CLAUDE.md S12 (FlowTrace "BattleArena") so a HEADLESS run --
// which this isolated design makes fully self-contained -- pinpoints any dead step.
// ASCII-only logs; LogWarning, never error. Flag-gated by FeatureFlags.OverworldEncounter.
// =============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;            // URP Volume / VolumeProfile (arena bloom, WO-504 #2)
using UnityEngine.Rendering.Universal;  // Bloom override + UniversalAdditionalCameraData
using DeNelle.Core;
using DeNelle.Core.Audio;
using DeNelle.Core.Combat;
using DeNelle.Core.Diagnostics;
using DeNelle.Village.UI;                // ScreenFader — masks the ~7km arena warps (encounter feedback)

namespace DeNelle.Village.Arena
{
    /// <summary>
    /// WO-556 ITEM 1 — the itemized totals a WIN actually granted, returned by
    /// <see cref="BattleArena"/>'s reward path and handed to the victory summary VIEW
    /// (<see cref="BattleArenaHud.ShowResult"/>) so it lists exactly what was awarded.
    /// Plain value object (logic -> view), no presentation. <see cref="GearName"/> is
    /// null when no gear dropped.
    /// </summary>
    public struct BattleRewardSummary
    {
        /// <summary>Hero XP granted (star-scaled).</summary>
        public int Xp;
        /// <summary>Wisdom (skill points) granted (star-scaled).</summary>
        public int Wisdom;
        /// <summary>Wood granted (star-scaled).</summary>
        public int Wood;
        /// <summary>Iron granted (star-scaled).</summary>
        public int Iron;
        /// <summary>WO-1104: GOLD banked during the fight (the per-kill stream Enemy.Die
        /// already credited). It was granted but never REPORTED before, so a fight's coin
        /// income was invisible on the victory screen — owner felt-test 2026-08-16
        /// ("I couldn't tell if it awarded anything").</summary>
        public int Gold;
        /// <summary>WO-1104: bodies actually downed this fight. Shown on the victory screen so
        /// a five-kill win reads as a bigger fight than a one-kill win, not just a bigger
        /// number nobody can attribute.</summary>
        public int Kills;
        /// <summary>Display name of the gear that dropped, or null if none.</summary>
        public string GearName;
    }

    /// <summary>
    /// Generic real-time battle controller. PvE entry: <see cref="BeginEncounter"/>.
    /// One battle at a time; a runtime singleton the engage trigger drives.
    /// </summary>
    public sealed class BattleArena : MonoBehaviour
    {
        // Stage the arena FAR from the world origin so it is spatially isolated from
        // the open world (which stays loaded in memory -> cheap return). The global
        // skybox/ambient persist, so the backdrop still "matches where you were".
        private static readonly Vector3 ArenaCentre = new Vector3(5000f, 0f, 5000f);

        // Radius around ArenaCentre that counts as "inside the staged arena". Generous (well past
        // the ~30x24 footprint) so any arena actor reads as arena; anything else is the home scene.
        private const float ArenaWorldRadius = 200f;

        /// <summary>
        /// True when <paramref name="worldPos"/> lies inside the far-offset staged arena (vs. the
        /// home/open-world scene that stays in memory). Lets shared systems (e.g. combat feel) tell
        /// a LIVE arena hit from a home-scene bleed-through hit by where it happened — the arena is
        /// staged ~7km away, so distance is an unambiguous discriminator.
        /// </summary>
        public static bool IsArenaPosition(Vector3 worldPos)
            => (worldPos - ArenaCentre).sqrMagnitude <= ArenaWorldRadius * ArenaWorldRadius;

        // Open kite arena footprint. Tightened -25% (owner 2026-06-28: a tighter battle forces
        // better engagement — less open kiting, enemies close sooner). Was 30 x 24.
        private const float ArenaHalfWidth = 22.5f;  // X half-extent (~45 wide) — -25% from 30
        private const float ArenaHalfDepth = 18f;    // Z half-extent (~36 deep) — -25% from 24

        private const float BattleTimeoutSeconds = 240f; // generous; a stuck fight ends, never soft-locks

        // SELF-HEAL WATCHDOG grace (overworld-wedge fix): how long the hero may read as OUTSIDE the
        // staged arena during a LIVE fight before we force-resolve to un-freeze the home reps. Long
        // enough that a legit mid-warp frame — the hero is briefly between home and the ~7km arena
        // during the staged WarpHero — never trips it, short enough that a real failed warp-in or an
        // orphaned _battlePaused self-heals in ~2-3s instead of waiting out the 240s battle timeout.
        private const float HeroOutOfArenaGraceSeconds = 2.5f;

        // FLED-PACK LEASH + DISENGAGE (fled-enemy fix — same non-resolution class as the wedge): the
        // OPENING FTUE fight can stage an Orc Warband that kites/retreats OUT of reach (combo stays 0,
        // _liveEnemies never empties, BattleLock + the HUD + hero inputs pin for up to 240s).
        //  • LeashRadius     — how far a staged enemy may drift from the hero before we pull it back.
        //    Generous vs the ~10m kite band (EnemyBrain KiterTactics) so NORMAL kiting is untouched;
        //    only a true flee past this is clamped so the foe stays reachable ("turn and fight").
        //  • EngageContactRadius — kept > LeashRadius so a leashed (still-in-play) enemy always reads
        //    as in-contact; the disengage timer therefore only elapses when the pack is genuinely
        //    unreachable (leash failed / off-mesh island / hero not truly present).
        //  • DisengageResolveSeconds — no-contact window before we break off the encounter (loss).
        //    Well under the 240s timeout so the HUD/BattleLock release promptly, not on hero death.
        //
        // BAIT ALLOWANCE (owner live-play 2026-08-16: "i was trying to target and bait an enemy
        // out and i think we need to allow aggro targets to extend leash alot more"). These three
        // moved from hardcoded consts (16 / 18 / 7) to canonical data - Data/Canonical/
        // aggro-tuning.json via AggroTuning - and the leash opened up to cover the WHOLE arena.
        // WHY IT HAD TO CHANGE: the arena footprint is 45 x 36 (ArenaHalfWidth/Depth below) with
        // the enemy rear rank at Z ~ +15, so a bait that uses the arena end-to-end is ~33m. At a
        // 16m leash EVERY staged enemy was teleported back to within 15.2m of the hero every
        // 0.25s tick - back off to pull one orc out of the pack and the entire pack snapped along
        // with her. Baiting was not "too short", it was structurally impossible. The fled-pack
        // softlock this leash was written for is still fixed: an enemy that has genuinely left
        // the fight (past the wider bound) is still pulled back, so the encounter always resolves.
        private static float LeashRadius             => AggroTuning.ArenaChaseLeashRadius;
        private static float EngageContactRadius     => AggroTuning.EffectiveArenaEngageContactRadius;
        private static float DisengageResolveSeconds => AggroTuning.ArenaDisengageSeconds;

        // ABANDONMENT WATCHDOG grace (patch 6, F8 2026-07-30): how long the 'Player'-tagged hero may
        // read as MISSING during a live fight before we tear the encounter down as ABANDONED. Long
        // enough that a body-swap / re-tag frame (HeroBodySwapper, HeroControlEnsurer) never trips it,
        // short enough that a dungeon exit / death-EVAC scene route is caught in ~1s — well before the
        // win gate can read an emptied _liveEnemies as a victory. Deliberately SHORTER than
        // HeroOutOfArenaGraceSeconds: a MISSING hero is unambiguous, an out-of-arena one is not.
        private const float HeroMissingGraceSeconds = 1.0f;

        // ── SQUAD FORMATION spacing (owner-adjustable — 2026-07-03 "spawn in a proper formation") ──
        // Three ranks laid out on the NORTH side facing the SOUTH-standing hero: TANKS front (nearest
        // the hero), DPS/ranged mid, HEALERS rear. Tune these by eye — they are the whole formation
        // shape. FormationRearAnchorZ is the rear (healer) rank; each rank forward of it is
        // FormationRankDepth closer to the hero; members within a rank are FormationLateralGap apart.
        private const float FormationRearAnchorZ  = ArenaHalfDepth - 3f; // rear (healer) rank Z, just inside the north wall
        private const float FormationRankDepth    = 4.5f;                // Z gap between ranks (front line = 2 ranks closer)
        private const float FormationLateralGap   = 3.5f;                // X gap between neighbours within a rank

        // WO-556 ITEM 2 — RARE BOSS CHALLENGE. On staging an encounter we roll this chance to
        // ADD a boss mob to the family (a rare, harder fight that pays boss-only loot). Named
        // consts so the owner felt-tunes the rate without code spelunking. The boss id resolves
        // through EnemyFactory.ModelForEnemy -> "Orc_Necromancer" (a VERIFIED Resources/Enemies
        // model, the existing outpost raid-boss silhouette), NOT a capsule. We use a GROUND boss
        // (orc-warlord) not the flying DragonBoss: the dragon flies its own kinematic orbit and
        // does not path the kite-arena navmesh, so it would never engage the hero here.
        private const float BossSpawnChance = 0.05f;        // ~5% of arena fights gain a boss
        private const string BossEnemyId    = "orc-warlord"; // -> Orc_Necromancer model (verified)

        // WO-556 ITEM 4 — stars scale the gear-DROP chance (more stars = better odds). Bonus
        // applied per star ABOVE 1 (1*=+0, 2*=+0.10, 3*=+0.20) on top of the threat curve.
        private const float GearDropPerStar = 0.10f;

        // ENCOUNTER FEEDBACK (2026-06-27): both ~7km WarpHero transitions (into the arena, back
        // home) were unmasked hard cuts. We now bracket each with a black ScreenFader fade so the
        // camera snap reads as an intentional transition. Snappy by design (a flicker of black, not
        // a load screen). Owner-tunable.
        private const float StageFadeOutSeconds = 0.35f;  // to black before the warp-in
        private const float StageFadeInSeconds  = 0.45f;  // reveal the staged arena
        private const float HomeFadeOutSeconds  = 0.35f;  // to black before the home warp
        private const float HomeFadeInSeconds   = 0.45f;  // reveal home on return
        private const float IntroCardSeconds    = 1.6f;   // the "<foe> - Battle!" centre card hold

        // WO-505 "battle closing": the wall-clock time the fight went live (set in
        // BeginEncounter). Resolve subtracts it to get the duration the star rating reads.
        private float _battleStartTime;

        // WO-505: how long the victory/defeat cue is allowed to breathe before the explore
        // BGM crossfades back in. Matched to the result banner's ~2.5s hold so the climax
        // music plays under the banner, then the open-world ambient returns. Owner-tunable.
        private const float RewardCueSeconds = 2.5f;

        // LOSE-FLOW (owner TOP priority): on a LOSS the hero must land SAFE, not back inside the
        // rep's aggro (which re-fought instantly). We pull the return point BACK along the hero's
        // approach heading by this much — far enough to clear a rep's ~14m aggro radius — so the
        // hero recovers with breathing room instead of re-engaging on the spot. Win returns to the
        // exact engagement spot (the rep is dead) unchanged.
        private const float LossSafeRetreatMeters = 18f;   // > rep AggroRange (14f) so the warp clears aggro

        // ---------------------------------------------------------------------
        //  WO-504 #2: arena BLOOM tunables (BONES - owner felt-tunes the numbers).
        // ---------------------------------------------------------------------
        // The global DefaultVolumeProfile ships Bloom intensity 0 (effectively OFF) and the
        // URP asset has HDR off, so the arena's HDR materials (Crystal 4.2 / Arcane Shield 4.5
        // / FireTrail 3.0 / additive particles) do NOT glow. We add a LOCAL global Volume +
        // a code-built Bloom profile under _arenaRoot, force the combat camera's post-process
        // + HDR on for the fight, and restore both on Resolve. Cheap (bloom only) for mobile.
        //
        // Defaults: intensity moderate so HDR pops without washing the scene; threshold ~1.0
        // so only HDR > 1 (the VFX) blooms, not lit geometry. Owner tunes these by eye.
        // WO-678 (2026-07-12): aligned to the overworld's new demo-parity bloom
        // (WorldFeelInjector 4.5/1.1, from the Hovl demo VolumeURP.asset) — at 1.4 this
        // priority-100 volume would DIM combat relative to town, inverting the intent.
        private const float ArenaBloomIntensity = 4.5f;   // demo-parity glow (was 1.4)
        private const float ArenaBloomThreshold = 1.1f;   // only true-HDR VFX blooms; lit ground stays out of bloom
        private const float ArenaBloomScatter   = 0.7f;   // glow spread (URP default)
        private const int   ArenaBloomPriority  = 100;    // outrank the global DefaultVolumeProfile (priority 0)

        private static BattleArena _instance;

        /// <summary>The live BattleArena (creates a persistent host on first access).</summary>
        public static BattleArena Instance
        {
            get
            {
                if (_instance == null)
                {
                    var host = new GameObject("BattleArena");
                    // DontDestroyOnLoad is play-mode-only and THROWS in an editor script
                    // (the headless ArenaCombatOracle stands this host up in batchmode to
                    // drive the real Resolve path). Guard it so the host is editor-
                    // instantiable; in a build/playtest the persist behaviour is unchanged.
                    if (Application.isPlaying) DontDestroyOnLoad(host);
                    _instance = host.AddComponent<BattleArena>();
                }
                return _instance;
            }
        }

        /// <summary>The live BattleArena WITHOUT creating one (null until first staged). Use this in
        /// hot paths / probes that must not spawn the singleton just to ask if a battle is running.</summary>
        public static BattleArena Existing => _instance;

        /// <summary>True iff a battle is currently staged — non-creating (safe in hot combat paths).</summary>
        public static bool AnyBattleInProgress => _instance != null && _instance.BattleInProgress;

        /// <summary>True while a battle is staged (blocks a second start + locks panels/hotkeys).</summary>
        public bool BattleInProgress { get; private set; }

        /// <summary>Raised when a battle resolves: (params, won).</summary>
        public event Action<EncounterParams, bool> OnBattleEnded;

        /// <summary>Raised the moment a battle is STAGED (in <see cref="BeginEncounter"/>), before the
        /// stage coroutine runs. Lets a scene owner switch camera framing for the fight — the dungeon
        /// FPV rig forces over-the-shoulder here and restores FPV on <see cref="OnBattleEnded"/>.</summary>
        public event Action<EncounterParams> OnBattleStaged;

        /// <summary>
        /// The enemies STAGED for the current encounter (the orc family + any rare boss), in spawn
        /// order. Read-only view so presentation (BattleHud9Zone roster) binds to the ENCOUNTER's
        /// enemies only and never leaks paused home reps (frozen "OrcRep_*" Enemy components that
        /// still live in the home scene during a fight). Entries are removed as members die/despawn.
        /// </summary>
        public IReadOnlyList<Enemy> StagedEnemies => _liveEnemies;

        private Func<bool> _battleProbe;
        private GameObject _arenaRoot;
        private readonly List<Enemy> _liveEnemies = new List<Enemy>();
        private EncounterParams _current;
        private bool _resolved;
        // WO-1103: kills (not roster) drive the battle payout; the stream is the summed
        // ROLLED per-enemy grants Enemy.Die banked during THIS fight (fed to the SUMMARY).
        private int _killCount;
        private int _killStreamXp;
        private int _killStreamGold;
        private BattleArenaHud _hud;
        private FamilyLeader _familyLeader;   // WO-146 MonsterFamily — the orc pack's leader
        private bool _familyEngaged;          // disbanded-on-arrival latch (formation -> real 1vN)

        // SELF-HEAL WATCHDOG transients (overworld-wedge fix — reset per battle in WatchToResolution).
        // _heroOutOfArenaSince: Time.time the hero was FIRST seen OUTSIDE the staged arena during a live
        //   fight; -1 = hero in-arena / not tracking. Drives the out-of-arena grace timer.
        // _marchPosLogged: one-shot latch so the §12 post-warp HERO-POS capture in MaybeDisbandOnArrival
        //   fires once per battle (proves warp-in landed the hero in the arena, or did not).
        // _lastCloseContactTime: Time.time a live enemy was last within engage range of the hero; drives
        //   the DISENGAGE-RESOLVE path (a scattered/fled pack releases the HUD instead of pinning it 240s).
        private float _heroOutOfArenaSince = -1f;
        private bool  _marchPosLogged;
        private float _lastCloseContactTime;

        // _heroMissingSince: Time.time the 'Player'-tagged hero FIRST read as absent during a live
        // fight; -1 = hero present / not tracking. Drives the ABANDONMENT grace timer (patch 6).
        private float _heroMissingSince = -1f;

        private string _activeBiome;          // WO-499 resolved biome (backdrop + particles)
        private ArenaDeathCam _deathCam;      // WO-493 #4 climactic death-camera hold

        // WO-493 #4: the dying actor to linger on for the climactic death-cam (the LAST enemy
        // killed, or the hero on a loss). Captured at the resolving moment so the camera frames
        // the right body even as _liveEnemies empties / the hero ragdolls.
        private Transform _climaxBody;

        // Cavern mood: saved RenderSettings to restore on Resolve so the open world is untouched.
        private bool _moodSaved;
        private bool _savedFog;
        private Color _savedFogColor;
        private float _savedFogDensity;
        private Color _savedAmbientLight;
        private float _savedAmbientIntensity;

        // WO-504 #2: combat-camera post-process/HDR overrides, saved so the open-world camera
        // is untouched on return. Saved when staged, restored in Resolve.
        private bool _camStateSaved;
        private Camera _bloomCam;
        private bool _savedCamAllowHDR;
        private bool _savedCamPostFx;
        private bool _hadCamData;

        // WO (2026-06-28) arena sky override: the enclosure cap + a SolidColor camera clear KILL the
        // persisted NIGHT skybox over/through the backdrop. Saved when staged, restored on Resolve.
        private bool _skyOverridden;
        private Camera _skyCam;
        private CameraClearFlags _savedClearFlags;
        private Color _savedCamBg;

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(this); return; }
            _instance = this;
            _battleProbe = () => BattleInProgress;
            BattleLock.RegisterProbe(_battleProbe);

            RegisterQuiescenceProbes();
        }

        /// <summary>
        /// WO-1127: hand the gate the invariants only Village can see. They cannot live in Core -
        /// DeNelle.Core cannot reference Enemy or HeroLocomotion - so they arrive as delegates
        /// instead of dissolving the asmdef boundary. Register() replaces by name, so a scene
        /// reload that re-runs Awake cannot accumulate duplicates.
        /// </summary>
        private void RegisterQuiescenceProbes()
        {
            // Orphaned stage actors. The in-place arena stages its enemies ~7 km out, so a survivor
            // is INVISIBLE to the player while still ticking, pathing and holding references. On a
            // resolved battle _liveEnemies must be empty; anything left is a teardown miss.
            BattleQuiescenceGate.Register(new QuiescenceProbe
            {
                Name = "arena-actors",
                Check = () =>
                {
                    if (BattleInProgress) return null;   // a new battle started; not our business
                    int alive = 0;
                    for (int i = 0; i < _liveEnemies.Count; i++)
                        if (_liveEnemies[i] != null) alive++;
                    return alive == 0
                        ? null
                        : $"{alive} staged arena enemy(ies) still alive after resolve. They sit ~7 km " +
                          "from the player, so they are invisible while still ticking and pathing.";
                }
            });

            // Hero owner. 'owner=FOREIGN-CC' is a known movement failure on the dungeon side and
            // belongs in the same net: a foreign mover holding the transform looks exactly like
            // frozen controls to the player, which is the symptom this whole ticket started from.
            BattleQuiescenceGate.Register(new QuiescenceProbe
            {
                Name = "hero-owner",
                Check = () =>
                {
                    var loco = UnityEngine.Object.FindFirstObjectByType<HeroLocomotion>();
                    if (loco == null) return null;   // no hero in this scene; nothing to assert
                    var cc = loco.GetComponent<CharacterController>();
                    return (cc != null && cc.enabled)
                        ? "a foreign CharacterController is ENABLED on the hero, so HeroLocomotion is " +
                          "not the mover. The player reads this as unresponsive movement."
                        : null;
                }
            });
        }

        private void OnDestroy()
        {
            BattleLock.UnregisterProbe(_battleProbe);
            if (_instance == this) _instance = null;
        }

        /// <summary>
        /// Start the PvE encounter described by <paramref name="p"/>. Returns false (no
        /// stage) if a battle is already running, the params/family are empty, or the
        /// feature flag is off. The hero is warped into the arena; on resolve it is warped
        /// back to <see cref="EncounterParams.ReturnPosition"/>.
        /// </summary>
        public bool BeginEncounter(EncounterParams p)
        {
            if (BattleInProgress) { Debug.LogWarning("[BattleArena] a battle is already in progress - ignored."); return false; }
            if (p == null || p.EnemyIds == null || p.EnemyIds.Length == 0)
            {
                Debug.LogWarning("[BattleArena] null/empty EncounterParams - ignored.");
                return false;
            }
            // GATE: the arena stages for an OVERWORLD encounter (ff.overworldencounter) OR a retired-ATB
            // DUNGEON encounter (ff.dungeonrealtime, WO-591 — the two dungeon call sites route here instead
            // of the flat ATBBattle scene). Either flag being ON authorizes a stage; both OFF suppresses it.
            if (!FeatureFlags.OverworldEncounter && !FeatureFlags.DungeonRealtimeBattle)
            {
                Debug.LogWarning("[BattleArena] ff.overworldencounter + ff.dungeonrealtime both OFF - encounter suppressed.");
                return false;
            }

            // SOFTLOCK FIX (owner F8 "on battle why am i still in build" + battleLock stuck True in
            // MainCastle_Hall): if a battle is called while Build Mode is active, the build camera +
            // input override steal control — the hero is warped to the arena but the player "never sees
            // anything", can't fight, the battle never resolves, and BattleLock (this arena's probe)
            // stays True forever -> hub softlock (no movement 180s). Force-exit Build Mode FIRST so
            // control returns to the hero before staging. Exit() is idempotent (no-op if not active).
            if (BuildModeController.Instance != null && BuildModeController.Instance.IsActive)
            {
                FlowTrace.Step("BattleArena", "BeginEncounter: Build Mode was ACTIVE — force-exiting before staging (softlock guard).");
                Guard.Try("BattleArena", "force-exit build mode", () => BuildModeController.Instance.Exit());
            }

            // F8 2026-07-30 (dungeon phantom fight): with NO 'Player'-tagged hero a staged
            // fight can NEVER resolve — WarpHero skips, the family can't close on anyone,
            // and the out-of-arena self-heal has no hero to measure, so BattleLock latches
            // and the HUD flips to combat posture over an invisible fight (captured:
            // "BeginEncounter HERO-POS: pos=<no Player>" + "WarpHero: no 'Player' hero
            // found"). REFUSE to stage instead; callers handle false
            // (EncounterTrigger.RollbackHandoff clears the dungeon combat lock + re-arms).
            var stageHero = GameObject.FindWithTag("Player");
            if (stageHero == null)
            {
                FlowTrace.Fail("BattleArena",
                    "BeginEncounter REFUSED: no 'Player'-tagged hero in scene — a hero-less stage is a " +
                    "phantom fight that can never resolve. Caller rolls back its combat lock.");
                return false;
            }

            // F8 2026-08-05 (dungeon unplayable from the first encounter) — the SAME refusal, widened.
            // A hero-less stage was never the only unwinnable stage: the dungeon Keeper staged
            // 'Player'-TAGGED but PARTIAL — no PlayerAttackController (she could not damage anything:
            // captured 5x "[Flow:HudKit] attack fired but no PlayerAttackController in scene") and no
            // HeroHealth (EnemyBrain deals damage ONLY through HeroHealth, so the enemy stood aware and
            // in range doing nothing: 77x Idle_A against 69x inRange=True). Neither side could land a
            // hit, so the fight could never resolve, so BattleLock never released — the run was
            // softlocked from the first encounter. An UNWINNABLE fight must NEVER stage: refuse, NAME
            // the missing component, and let the caller unwind (EncounterTrigger.RollbackHandoff
            // clears the pending handoff + combat lock and re-arms the trigger).
            bool canDealDamage = stageHero.GetComponent<DeNelle.Village.PlayerAttackController>() != null;
            bool canTakeDamage = stageHero.GetComponent<DeNelle.Village.HeroHealth>() != null;
            if (!canDealDamage || !canTakeDamage)
            {
                string missing = (!canDealDamage ? "PlayerAttackController" : string.Empty) +
                                 (!canDealDamage && !canTakeDamage ? " + " : string.Empty) +
                                 (!canTakeDamage ? "HeroHealth" : string.Empty);
                FlowTrace.Fail("BattleArena",
                    $"BeginEncounter REFUSED: hero '{stageHero.name}' is PARTIAL — missing {missing}. " +
                    (!canDealDamage ? "Without PlayerAttackController the hero cannot damage the family. " : string.Empty) +
                    (!canTakeDamage ? "Without HeroHealth nothing can damage the hero (EnemyBrain routes all hero damage through it). " : string.Empty) +
                    "Either way the fight can NEVER resolve and BattleLock would latch forever. " +
                    "Caller rolls back its combat lock. FIX THE RIG (HeroControlEnsurer." +
                    "EnsureHeroCombatComponents), do not relax this gate.");
                return false;
            }

            _current = p;
            _resolved = false;
            _climaxBody = null;
            _returnWarpCancelled = false;   // F8 seq512: fresh fight, the return warp is live again
            _battleStartTime = Time.time;   // WO-505: start the star-rating clock.
            // WO-1103: fresh fight -> zero the kill counter + the per-enemy reward stream
            // (kills, not roster, drive the battle payout; the stream feeds the SUMMARY total).
            _killCount = 0;
            _killStreamXp = 0;
            _killStreamGold = 0;
            BattleInProgress = true;

            DeNelle.Village.GameSfx.PlayWeaponDraw(); // #51: hero unsheathes as the fight begins

            // BATTLE ISOLATION: freeze EVERY home-scene rep for the duration of the fight. The
            // open world stays in memory (additive intent), so without this its reps keep roaming/
            // chasing/aggroing + running home combat — bleeding rumble/feedback into the battle and
            // double-simulating (choppy). One static gate removes all three sources at once; reps
            // resume on Resolve. (Static so no per-rep FindObjectsByType scan is needed.)
            RepEngageWatcher.PauseAll();

            // TOWN SUSPENSION - THE ARENA GETS THE SAME PAUSE AS A DUNGEON (owner ruling
            // 2026-08-07). This is a deliberate call, stated rather than implied:
            //
            // The arena is the THIRD case, and it is the hardest one to see, because unlike a
            // dungeon there is NO scene change - the fight is staged 7 km away at ArenaCentre
            // in the SAME scene, so the scene-driven evaluator in TownSuspension never fires
            // for it. It has to be driven by hand, from here.
            //
            // It qualifies on the ruling's own terms: the player is ACTIVE, the town is
            // unattended, and they cannot reach it to defend it. It is also the case that
            // PROVED the defect - a village wave cleared 2.7 s after an arena victory and
            // stranded the player, because the wave clock ran the whole fight with the hero
            // 7 km away. The return grace exists precisely so that cannot happen again.
            //
            // Reversible in one line: delete this call and the arena reverts to a running
            // town, with dungeons still paused.
            DeNelle.Core.TownSuspension.Suspend("arena battle staged at ArenaCentre (hero 7km away, player active)");

            // WO-556 ITEM 2: roll the rare boss. On a hit, append the boss id to the family so
            // SpawnFamily stages it alongside the rest. Instrumented so the rate is PROVABLE from
            // the break-log / Editor.log (CLAUDE.md S12) rather than inferred from the const.
            MaybeAddBoss(p);

            FlowTrace.Step("BattleArena", $"BeginEncounter: family=[{string.Join(",", p.EnemyIds)}] threat={p.Threat} theme='{p.BackdropContext}' return='{p.ReturnScene}'.");

            // §12 ROOT-CAUSE CAPTURE (overworld-wedge): log WHERE the hero is at encounter start so the
            // NEXT occurrence tells us whether the warp-in FAILED (candidate A: hero starts home and never
            // reaches the arena) or this is an ORPHANED _battlePaused carried from a prior encounter
            // (candidate B: hero already reads in-arena / elsewhere before this stage even runs).
            var beHero = GameObject.FindWithTag("Player");
            FlowTrace.Step("BattleArena",
                $"BeginEncounter HERO-POS: pos={(beHero != null ? beHero.transform.position.ToString() : "<no Player>")} inArena={(beHero != null && IsArenaPosition(beHero.transform.position))} centre={ArenaCentre}.");

            // Battle-STAGED signal (before the stage coroutine): lets a scene owner switch camera
            // framing for the fight (dungeon FPV -> over-the-shoulder). Null-safe; never blocks staging.
            OnBattleStaged?.Invoke(p);

            StartCoroutine(StageRoutine(p));
            return true;
        }

        // ---------------------------------------------------------------------
        //  Stage: build arena -> bake navmesh -> warp hero -> spawn family -> watch
        // ---------------------------------------------------------------------
        private IEnumerator StageRoutine(EncounterParams p)
        {
            // 1) Build the open kite arena (floor + boundary) at the far offset.
            BuildArena(p.BackdropContext);

            // 2) Runtime-bake a local NavMesh over the arena floor (REUSE ArenaNavMeshBaker:
            //    it adds a walkable plane + a NavMeshSurface and BuildNavMesh()es over the
            //    children colliders). The far-offset arena has no pre-baked mesh, so this is
            //    the genuine need the baker was built for (the WO-388 castle path).
            var baker = _arenaRoot.AddComponent<ArenaNavMeshBaker>();
            // Bigger floor plane than the castle default (8f => ~80m plane) so it comfortably
            // covers the 60x48 kite floor + the wider ground/treeline with margin. Far orcs
            // could not reach the hero because the default ~50m plane under-covered the kite
            // footprint (captured: "[Flow:EnemyAggro] no COMPLETE path to Hero -> ... last
            // reachable corner"). The castle path is untouched (it calls BakeForCastle with no arg).
            Guard.Try("BattleArena", "bake arena navmesh", () => baker.BakeForCastle(_arenaRoot.transform, 6f));
            // Give the (synchronous) bake + the floor realize a couple frames to settle.
            yield return null;
            yield return null;

            // INSTRUMENT (CLAUDE.md S12): PROVE the bake covers the kite corners instead of
            // re-guessing. Sample the four kite corners (just inside the boundary walls) and log
            // each corner's on-mesh result so the next headless/felt run shows coverage in the
            // trace. ASCII-only.
            Guard.Try("BattleArena", "probe arena navmesh corners", () =>
            {
                Vector3[] corners =
                {
                    ArenaCentre + new Vector3( 21f, 0f,  16.5f),
                    ArenaCentre + new Vector3( 21f, 0f, -16.5f),
                    ArenaCentre + new Vector3(-21f, 0f,  16.5f),
                    ArenaCentre + new Vector3(-21f, 0f, -16.5f),
                };
                foreach (var c in corners)
                {
                    bool onMesh = NavMesh.SamplePosition(c, out NavMeshHit chit, 3f, NavMesh.AllAreas);
                    Vector3 rel = c - ArenaCentre;
                    FlowTrace.Step("BattleArena",
                        $"ARENA navmesh corner ({rel.x:0},{rel.z:0}): onMesh={onMesh}" +
                        (onMesh ? $" snapDist={Vector3.Distance(c, chit.position):0.00}" : ""));
                }
            });

            // 3) MASK THE WARP-IN (encounter feedback): fade to black BEFORE the ~7km warp so the
            //    camera snap reads as an intentional transition, not a hard cut. Reuses the project's
            //    ISceneFader contract — ScreenFader installs the long-declared-but-never-wired
            //    SceneRouter.Fader. Unscaled-time fade -> timescale-safe. Held in `fader` for the
            //    fade-in once the arena is staged + HUD up.
            var fader = ScreenFader.EnsureInstalled();
            FlowTrace.Step("BattleArena", "FADE OUT before arena warp-in (mask the 7km hard-cut).");
            if (fader != null) yield return StartCoroutine(fader.FadeOutCo(StageFadeOutSeconds));

            // Warp the hero to the SOUTH stance, facing north toward the enemies (under black).
            // BACKPEDAL ROOM (RCA 2026-07-04): was -ArenaHalfDepth + 2f (Z=-16), only 2m off the south
            // wall (Z=-18) — backpedal hit the wall instantly and kiting was impossible even though the
            // hero (6 m/s) outruns enemies (~3 m/s). Spawn 9m inward (Z=-9) for real backpedal room.
            // Box size (ArenaHalfDepth/Width) is UNCHANGED — the -25% tighten is a deliberate design call.
            Vector3 heroStance = ArenaCentre + new Vector3(0f, 0f, -ArenaHalfDepth + 9f);
            WarpHero(heroStance, Quaternion.LookRotation(Vector3.forward));

            // FACING FIX (owner on-device 2026-07-15: "loading into the arena, the hero ALWAYS faces
            // the wrong direction / away from the fight"). The hero IS warped to face NORTH (+Z) into the
            // enemy line above — that part is correct. The wrong-facing is the CAMERA: the orbit camera
            // (SmartMobileCamera) keeps its STALE open-world pan yaw across the ~7km warp (_panYaw is
            // seeded once and never re-seated on a teleport), so it rotates the behind-offset by the old
            // world yaw and lands in FRONT of the hero — framing the hero's face with the enemies off
            // behind it. Re-seat the camera BEHIND the hero's NEW facing so the shot looks INTO the fight.
            // Instrumented (S12) so a run PROVES the hero yaw + the re-seat instead of us guessing.
            Guard.Try("BattleArena", "arena stage-in hero-facing + camera re-seat", () =>
            {
                var heroGo = GameObject.FindWithTag("Player");
                float heroYaw = heroGo != null ? heroGo.transform.eulerAngles.y : -1f;
                FlowTrace.Step("BattleArena",
                    $"hero spawn facing yaw={heroYaw:0} stance={heroStance} (expect ~0 = +Z toward the NORTH enemy line).");
                // Re-seat the orbit camera behind the hero's new facing (no-op if orbit-behind is off /
                // no camera in a headless run). This is the actual 'wrong direction' cure — the hero
                // rotation itself is already correct.
                SmartMobileCamera.Instance?.SnapBehindTarget();
                FlowTrace.Step("BattleArena", "camera re-seated BEHIND hero on stage-in (stale-yaw framing cleared).");
            });

            // 4) Spawn the enemy FAMILY across the NORTH side (loose formation, 1..6).
            SpawnFamily(p);

            if (_liveEnemies.Count == 0)
            {
                // Nothing staged -> abort cleanly rather than a phantom win.
                FlowTrace.Fail("BattleArena", "StageRoutine: no enemies spawned - aborting encounter (no phantom win).");
                Resolve(false);
                yield break;
            }

            // The fight now STARTS UNLOCKED (owner deliberate-lock design). The prior WO-512 slice 1
            // auto-lock (EngageLock(null) + camera SetLockTarget on the nearest hostile) is REMOVED so
            // the Knight begins in free-kite: _lockFaceActive stays false → normal LookRotation(Velocity).
            // Locking is now a purely DELIBERATE player action — desktop middle-click / mobile tap on an
            // enemy engages the full lock-on (camera frame + face + strafe) via HeroTargetIndicator, and
            // clicking/tapping the locked foe (or empty space) releases it. FeatureFlags.LockOn still
            // gates that deliberate feature. No camera bind here: MaybeRebindLockCamera (ticked from
            // WatchToResolution) reads the live locked target each tick and clears framing when nothing
            // is locked, so starting unlocked needs no explicit camera clear.
            FlowTrace.Step("BattleArena", "LOCKON fight starts UNLOCKED — lock is now a deliberate player action (middle-click / tap enemy).");

            // 5) Present: battle HUD + combat BGM. (Presentation layer; logic already staged.)
            // HUD ISOLATION (was: hide the WHOLE kit via ArenaHudBridge.SetVisible(false), owner F8
            // flag_25 2026-07-02). That force-hide set the single kit CanvasGroup alpha=0, which ALSO
            // blanked the posture system's combat widgets (health/target/cast/ability/attack/flee) —
            // the P1 "no HUD overlay in the arena". Removed: the kit stays VISIBLE and the BattleLock
            // battle probe -> HudPosture.HostileActiveBattle occupancy (hud-areas.json) swaps town
            // widgets OFF and combat widgets ON, so no town HUD bleeds through (verified: the
            // hostile(activebattle) row carries no town-only widget — no build/resource/chat/heart/wave).
            FlowTrace.Step("BattleArena", "BATTLE HUD: kit left visible; posture swaps to hostile(activebattle) combat widgets.");
            Guard.Try("BattleArena", "build battle overlay", () =>
            {
                _hud = BattleArenaHud.Create();
                _hud.SetFleeHandler(Flee);
                // WO-563: SetPrimary removed — the 9-zone battle HUD owns the enemy-target readout.
                // ENGAGE INTRO CARD (encounter feedback): a centre overlay naming the foe, so the
                // pull-into-the-fight has an on-screen cause. Built on the HUD's own canvas so it
                // shows EVEN with the 9-zone HUD up. Derived from the family ids; self-destructs.
                _hud.ShowIntro(FoeLabel(p) + " - Battle!", IntroCardSeconds);
            });
            FlowTrace.Step("BattleArena", $"INTRO card '{FoeLabel(p)} - Battle!' shown (visible even under the 9-zone HUD).");
            CoreServices.Audio?.PlayMusic(MusicTrack.Arena);

            // WO-504 #2: force the combat camera's post-processing + HDR ON so the arena Volume's
            // Bloom is applied and HDR > 1 VFX actually glow. Saved + restored on Resolve.
            EnableCombatBloomCamera();

            FlowTrace.Step("BattleArena", $"StageRoutine: staged {_liveEnemies.Count} enemies; fight live.");

            // REVEAL (encounter feedback): fade back in now that the hero is in, the family is
            // spawned and the HUD + intro card are up — completing the masked warp-in.
            FlowTrace.Step("BattleArena", "FADE IN: arena staged + HUD up (masked warp-in complete).");
            if (fader != null) yield return StartCoroutine(fader.FadeInCo(StageFadeInSeconds));

            // 6) Watch to resolution.
            yield return StartCoroutine(WatchToResolution());
        }

        // Build a large bounded floor (+ invisible boundary walls) at the arena centre.
        // NO structures (owner: "no mapping of structures, just a large enough arena").
        private void BuildArena(string theme)
        {
            // F8-37 STEP-IN (§12): name the build entry + theme so the trace opens the arena
            // BUILD/DRESS flow the giant untextured "pole" is captured inside.
            FlowTrace.Step("BattleArena", $"BuildArena ENTER theme='{theme ?? "<null>"}' at {ArenaCentre} (dressing the kite stage).");
            _arenaRoot = new GameObject("[BattleArena_Stage]");
            _arenaRoot.transform.position = ArenaCentre;

            // WO-499 #3 danger gradient: resolve the BIOME from the context + threat so the
            // backdrop SIGNALS the fight (forest=easy ... volcanic=hard family ... castle=tanky).
            // Ground/edge/cavern-mood still key off the raw 'theme' (their existing keys); the
            // biome only drives the painted backdrop + the per-biome particles.
            int threat = _current != null ? _current.Threat : 0;
            _activeBiome = ArenaBiomeDressing.ResolveBiome(theme, threat);
            // F8-37: the SKIN/theme chosen for this stage (the branch the dressing takes).
            FlowTrace.Step("BattleArena", $"BuildArena SKIN resolved biome='{_activeBiome}' (theme='{theme ?? "<null>"}' threat={threat}).");

            // WO-506: the REAL authored landscape. Load + instantiate the forest-clearing
            // PREFAB (Resources/Arena/ForestClearingArena, built by ArenaPrefabBuilder) onto
            // _arenaRoot -> a real ground mesh + dressed treeline + soft light, NOT a primitive
            // box. The prefab's Ground carries a Default-layer MeshCollider so the StageRoutine
            // ArenaNavMeshBaker bakes over it (no duplicate bake here). Idempotent loaders are
            // Guard-wrapped; a null prefab degrades to a plain LIT ground (never white/primitive-box).
            GameObject stage = null;
            Guard.Try("BattleArena", "load arena landscape prefab", () =>
            {
                stage = Resources.Load<GameObject>("Arena/ForestClearingArena");
            });
            if (stage != null)
            {
                Guard.Try("BattleArena", "instantiate arena landscape", () =>
                {
                    var go = UnityEngine.Object.Instantiate(stage, _arenaRoot.transform, false);
                    go.name = "ArenaLandscape";
                    // ARENA VISUAL COHERENCE (owner F8 flag_25 2026-07-02 "this looks awful"): the
                    // forest-clearing prefab (green lawn + toy-tree ring) was ALWAYS used, even when
                    // the resolved biome paints a dark stone colosseum backdrop — a visual-vocabulary
                    // clash. For the STONE biomes, retheme the same prefab in place: swap the Ground
                    // to the biome's real stone material + strip the tree silhouettes (rocks stay).
                    RethemeLandscapeForBiome(go, _activeBiome);
                    // F8 2026-08-05 LEAK 1 (dungeon win -> "the screen went black"): the stage
                    // prefab carries a SCENE-WIDE sun. Scope it before anything renders.
                    ScopeStageLights(go);
                });
                FlowTrace.Step("BattleArena", "BuildArena: loaded landscape prefab 'Arena/ForestClearingArena'.");
            }
            else
            {
                // SAFE FALLBACK: a plain lit ground plane (real _BaseColor, NO white emission)
                // so the fight degrades to a real floor, never a white box / missing ground.
                BuildFallbackFloor(theme);
                FlowTrace.Warn("BattleArena", "BuildArena: landscape prefab missing -> plain lit ground fallback.");
            }

            // Invisible boundary walls so neither hero nor enemy can wander off the stage.
            // (The NavMesh already confines agents; the walls are belt-and-braces + block
            //  the off-mesh hero-translation fallback.)
            BuildWall(new Vector3(0f,  2f,  ArenaHalfDepth + 0.5f), new Vector3(ArenaHalfWidth * 2f + 2f, 6f, 1f));
            BuildWall(new Vector3(0f,  2f, -ArenaHalfDepth - 0.5f), new Vector3(ArenaHalfWidth * 2f + 2f, 6f, 1f));
            BuildWall(new Vector3( ArenaHalfWidth + 0.5f, 2f, 0f),  new Vector3(1f, 6f, ArenaHalfDepth * 2f + 2f));
            BuildWall(new Vector3(-ArenaHalfWidth - 0.5f, 2f, 0f),  new Vector3(1f, 6f, ArenaHalfDepth * 2f + 2f));

            // WO-506: the edge treeline now lives IN the loaded landscape prefab (EdgeProps),
            // authored with the same DressArenaEdge ring math. The old runtime DressArenaEdge
            // call is dropped from the build path (the prefab provides the silhouette ring).

            // THE WOW (WO-499): a painted biome backdrop ringing the arena behind the treeline. Skip-safe.
            BuildBackdrop(_activeBiome);

            // WO-504 #2: the BLOOM multiplier so the HDR VFX/materials actually GLOW. A local
            // global Volume (priority above the global profile) + a code-built Bloom override,
            // staged with the arena (torn down with _arenaRoot). Camera post-fx/HDR forced on
            // separately (EnableCombatBloomCamera). Skip-safe -> no glow, never breaks the fight.
            BuildArenaBloom();

            // WO-499 #2: subtle per-biome particles (leaves/motes/embers/dust + mist) parented to the
            // stage so they tear down with it. Cheap, short-lived, capped -> "effects clear out fast".
            ArenaBiomeDressing.BuildParticles(_arenaRoot.transform, _activeBiome);

            // Cavern ONLY: dim the persisted sky/ambient/fog to a stone-cave mood (restored on Resolve).
            // Default (outerworld/castle) leaves the persisted dawn sky untouched -- it already matches.
            if ((theme ?? "outerworld").ToLowerInvariant() == "cavern")
                ApplyCavernMood();

            // F8-37 (§12): with the whole stage now dressed (landscape prefab + walls + backdrop +
            // bloom + particles), AUDIT every staged MESH so a headless/felt run NAMES the giant
            // untextured "arena pole" instead of guessing. This is the trace that identifies it even
            // WITHOUT a render (it reads mesh name + world size + material state, not pixels).
            AuditArenaRenderers();

            FlowTrace.Step("BattleArena", $"BuildArena: open kite floor {ArenaHalfWidth * 2f}x{ArenaHalfDepth * 2f} at {ArenaCentre} (theme '{theme}', no structures).");
        }

        // ---------------------------------------------------------------------
        //  ARENA GLOBALS OWNERSHIP (F8 2026-08-05 — "won a dungeon fight, the screen
        //  went black with the HUD still visible")
        //
        //  It was never a stuck fader: ScreenFader is sortingOrder 10000 (ScreenFader.cs:58)
        //  vs the HUD kit's 4000 (HudAreasHost.cs:85), so an opaque fader would have BURIED
        //  the HUD — it didn't. The world was genuinely unlit, because the arena leaks TWO
        //  SCENE-WIDE globals into whatever scene it stages inside:
        //    1) the landscape prefab's 'KeyLight' is a DIRECTIONAL light (the ONLY light in
        //       Resources/Arena/ForestClearingArena.prefab:1491, m_Type:1, intensity 1.05).
        //       A directional light lights the WHOLE active scene regardless of where the
        //       stage sits, so for the length of the fight the arena's sun WAS the dungeon's
        //       sun — and Destroy(stage) on return was the moment it "went black".
        //    2) ApplyCavernMood writes GLOBAL RenderSettings ambient/fog.
        //  Both are legitimate in the arena's OWN home (the open world / hub), where the arena
        //  is the only thing on screen. Inside a composed dungeon they are trespass.
        //
        //  The dungeon's darkness is DESIGN, not a defect (Lantern.cs, TorchWardenDress.cs;
        //  owner 2026-08-05: "add a torch so we can give extremely minimal light till torch").
        //  So we do NOT raise it, compensate for it, or add a light of our own — we only stop
        //  leaking ours in. Consequence, and it is the INTENDED one: a dungeon fight now
        //  renders at the dungeon's authored darkness. How dark "extremely minimal" should be
        //  is the dungeon's own authoring ticket, not the arena's.
        // ---------------------------------------------------------------------
        private static bool StagingInsideForeignScene()
        {
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            string name = scene.name ?? string.Empty;
            // Follows the established convention for "this is a composed dungeon scene"
            // (AudioService.cs:971, JupiterSwapBootstrap.cs:133), widened from "Dungeon_" to
            // "Dungeon" so Dungeon.unity / Dungeon_Demo are covered too.
            return name.StartsWith("Dungeon", StringComparison.Ordinal);
        }

        // LEAK 1 FIX. Neutralise the stage's scene-wide sun when the arena stages inside a
        // scene it does not own.
        //
        // WHY DISABLE rather than mask: a cullingMask would be the better answer, but there is
        // no layer to mask TO. ProjectSettings/TagManager.asset declares layers 0-8 only
        // (Default/TransparentFX/Ignore Raycast/Tower/Water/UI/Building/Enemy/Structure) —
        // 9..31 are all empty — and URP's m_RenderingLayers has only "Default", so
        // renderingLayerMask is no help either. Minting an arena layer means editing
        // ProjectSettings AND re-layering the whole stage, which would break the stage's
        // Default-layer MeshCollider contract that ArenaNavMeshBaker bakes over (see the
        // BuildArena comment above). Intensity/renderMode do not help: a directional light at
        // ANY intensity is still the scene's sun. So inside a foreign scene the light is simply
        // switched off; in the arena's own home nothing changes at all.
        private static void ScopeStageLights(GameObject stageRoot)
        {
            if (stageRoot == null) return;
            Guard.Try("BattleArena", "scope stage lights", () =>
            {
                bool foreign = StagingInsideForeignScene();
                var lights = stageRoot.GetComponentsInChildren<Light>(true);
                int directional = 0, disabled = 0;
                for (int i = 0; i < lights.Length; i++)
                {
                    var l = lights[i];
                    if (l == null || l.type != LightType.Directional) continue;
                    directional++;
                    if (!foreign || !l.enabled) continue;
                    l.enabled = false;
                    disabled++;
                }

                if (foreign)
                    FlowTrace.Warn("BattleArena",
                        $"ScopeStageLights: staging INSIDE '{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}' " +
                        $"(a scene the arena does not own) — DISABLED {disabled}/{directional} stage DIRECTIONAL light(s). " +
                        "A directional light is scene-wide, so leaving it on made the arena's sun the dungeon's sun and " +
                        "its teardown the 'screen went black'. The fight now renders at the scene's AUTHORED lighting — " +
                        "that is the owner's intent (the dark is a built mechanic), not a regression.");
                else
                    FlowTrace.Step("BattleArena",
                        $"ScopeStageLights: arena owns this scene — {directional} stage directional light(s) left ON (unchanged).");
            });
        }

        // ---------------------------------------------------------------------
        //  F8-37 INSTRUMENTATION (§12, INSTRUMENT-ONLY — NO fix): audit every mesh
        //  renderer staged under _arenaRoot so a run PROVES what the giant untextured
        //  "arena pole" is and where it was created, rather than inferring it.
        //  Per renderer we log: root-relative PATH, MESH name (a "Cylinder"/"Capsule"
        //  primitive is the prime pole suspect AND is provable from the mesh name with
        //  NO render), world-space SIZE (proves "giant"), and material/shader/texture
        //  state. Classification:
        //    • an untextured/default/error material            -> Warn (soft suspect)
        //    • ANY Cylinder/Capsule mesh (never legitimately built in this arena path)
        //                                                       -> Fail (loud -> break-log; the pole)
        //  Guard.TryEach so one bad renderer NAMES itself instead of blanking the audit.
        //  This adds ZERO gameplay/logic change — it only reads + logs.
        // ---------------------------------------------------------------------
        private void AuditArenaRenderers()
        {
            if (_arenaRoot == null)
            {
                FlowTrace.Warn("BattleArena", "AuditArenaRenderers: _arenaRoot null - nothing to audit.");
                return;
            }

            var mrs = _arenaRoot.GetComponentsInChildren<MeshRenderer>(true);
            var smrs = _arenaRoot.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var all = new List<Renderer>(mrs.Length + smrs.Length);
            all.AddRange(mrs);
            all.AddRange(smrs);
            FlowTrace.Step("BattleArena", $"AUDIT arena renderers: mesh={mrs.Length} skinned={smrs.Length} (naming any untextured primitive -> F8-37 pole).");

            var res = Guard.TryEach("BattleArena", "audit arena renderer", all, r =>
            {
                if (r == null) return;
                string path = RendererPath(r.transform);

                // Mesh name reveals a CreatePrimitive Cylinder/Capsule/Cube/Sphere/Plane/Quad
                // (a runtime primitive) vs an imported FBX mesh (the authored props).
                string meshName = "<none>";
                var mf = r.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) meshName = mf.sharedMesh.name;
                else if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null) meshName = smr.sharedMesh.name;

                // World-space size proves "giant" (the pole dwarfs the ~1-2m props).
                Vector3 wsize = r.bounds.size;

                // Material / shader / texture state.
                var mat = r.sharedMaterial;
                string matName = mat != null ? mat.name : "<null>";
                string shName = (mat != null && mat.shader != null) ? mat.shader.name : "<null>";
                bool errorShader = shName.IndexOf("InternalErrorShader", StringComparison.OrdinalIgnoreCase) >= 0;
                bool defaultMat = matName.IndexOf("Default-", StringComparison.OrdinalIgnoreCase) >= 0
                               || matName.IndexOf("Default_", StringComparison.OrdinalIgnoreCase) >= 0;
                bool hasTex = mat != null &&
                              ((mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null)
                            || (mat.HasProperty("_MainTex") && mat.GetTexture("_MainTex") != null));
                bool untextured = mat == null || errorShader || defaultMat || !hasTex;

                string mlow = meshName.ToLowerInvariant();
                bool poleShape = mlow == "cylinder" || mlow == "capsule";

                string line = $"path='{path}' mesh='{meshName}' size=({wsize.x:0.0},{wsize.y:0.0},{wsize.z:0.0})" +
                              $" mat='{matName}' shader='{shName}' textured={hasTex}";

                // A Cylinder/Capsule is NEVER built by the arena BUILD/DRESS path (it makes only
                // Plane + Quads + FBX props) -> its mere presence IS the F8-37 finding. Fail loud
                // (error-level -> break-log.jsonl + screenshot) so one headless run pins it.
                if (poleShape)
                    FlowTrace.Fail("BattleArena", "F8-37 ARENA POLE SUSPECT untextured=" + untextured + " " + line);
                else if (untextured)
                    FlowTrace.Warn("BattleArena", "F8-37 untextured/default-material renderer (soft suspect): " + line);
                else
                    FlowTrace.Step("BattleArena", "AUDIT " + line);
            });
            FlowTrace.Step("BattleArena", $"AUDIT arena renderers done: {res.built} audited, {res.failed} threw.");
        }

        // Root-relative hierarchy path for the F8-37 audit line (ASCII, depth-capped).
        private static string RendererPath(Transform t)
        {
            if (t == null) return "<null>";
            var sb = new System.Text.StringBuilder(t.name);
            var p = t.parent;
            int guard = 0;
            while (p != null && guard++ < 16)
            {
                sb.Insert(0, p.name + "/");
                p = p.parent;
            }
            return sb.ToString();
        }

        // ---------------------------------------------------------------------
        //  WO-506 SAFE FALLBACK: a plain lit ground plane (real _BaseColor, NO white
        //  emission) used only when the landscape prefab is missing, so the fight
        //  degrades to a real floor, NEVER a white/primitive-box void. The prefab is
        //  the normal path; this guarantees a bakeable Default-layer floor regardless.
        // ---------------------------------------------------------------------
        private void BuildFallbackFloor(string theme)
        {
            var floor = GameObject.CreatePrimitive(PrimitiveType.Plane); // MeshFilter + MeshRenderer + MeshCollider
            floor.name = "ArenaFloor_Fallback";
            floor.transform.SetParent(_arenaRoot.transform, false);
            floor.layer = 0; // Default layer so ArenaNavMeshBaker (PhysicsColliders) bakes it.
            floor.transform.localScale = new Vector3((ArenaHalfWidth * 2f) / 10f + 0.4f, 1f, (ArenaHalfDepth * 2f) / 10f + 0.4f);
            ApplyGroundTheme(floor, theme);
        }

        // ---------------------------------------------------------------------
        //  ARENA VISUAL COHERENCE (feel pass 2026-07-02, ff.combatfeel; §12 capture:
        //  F8 flag_25 — green lawn + toy trees under a stone colosseum backdrop).
        //  For STONE biomes (cavern/dungeon/volcanic/castle) the forest-clearing
        //  prefab is rethemed IN PLACE — presentation only, geometry untouched:
        //    1) Ground child -> the biome's real stone material (the SAME
        //       Resources/Arena mats ApplyGroundTheme uses: castle->Dwarven_Ground,
        //       else Floor_Sharp_Stones) so the floor matches the painted backdrop.
        //    2) EdgeProps tree silhouettes deactivated (Rock_* props stay — stone
        //       vocabulary). Forest/ruins biomes keep the authored clearing as-is.
        //  Flag OFF (ff.combatfeel=0) = exact legacy look. Skip-safe throughout.
        // ---------------------------------------------------------------------
        private static void RethemeLandscapeForBiome(GameObject landscape, string biome)
        {
            if (landscape == null || !FeatureFlags.CombatFeel) return;
            string key = (biome ?? "").ToLowerInvariant();
            bool stone = key == ArenaBiomeDressing.Cavern || key == ArenaBiomeDressing.Dungeon
                      || key == ArenaBiomeDressing.Volcanic || key == ArenaBiomeDressing.Castle;
            if (!stone)
            {
                FlowTrace.Step("BattleArena", "RETHEME skipped: biome '" + key + "' keeps the forest clearing.");
                return;
            }

            Guard.Try("BattleArena", "retheme landscape for stone biome", () =>
            {
                // 1) Ground -> the biome's stone material (reuse ApplyGroundTheme's mapping:
                //    'castle' -> Dwarven_Ground, everything else stone -> 'cavern' sharp stones).
                string groundTheme = key == ArenaBiomeDressing.Castle ? "castle" : "cavern";
                int grounds = 0;
                foreach (var mr in landscape.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (mr == null) continue;
                    if (mr.gameObject.name.IndexOf("Ground", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    ApplyGroundTheme(mr.gameObject, groundTheme);
                    grounds++;
                }

                // 2) Strip the tree silhouettes from the edge ring; keep the rocks.
                int treesHidden = 0;
                foreach (var t in landscape.GetComponentsInChildren<Transform>(true))
                {
                    if (t == null || !t.gameObject.activeSelf) continue;
                    if (t.name.IndexOf("Tree", StringComparison.OrdinalIgnoreCase) < 0) continue;
                    t.gameObject.SetActive(false);
                    treesHidden++;
                }

                // ORACLE FIRE-POINT (permanent, behind the FlowTrace toggle): prove the retheme
                // ran on the real staging path — grounds swapped + trees hidden per biome.
                FlowTrace.Step("BattleArena",
                    "RETHEME applied biome=" + key + " groundTheme=" + groundTheme +
                    " grounds=" + grounds + " treesHidden=" + treesHidden + " (ff.combatfeel).");
                if (grounds == 0)
                    FlowTrace.Warn("BattleArena", "RETHEME: no 'Ground' renderer found in landscape prefab — floor keeps the grass material.");
            });
        }

        // Strip every collider so an edge prop is a pure silhouette (never blocks the kite/navmesh).
        // THE WOW (WO-499): a painted biome backdrop ringing the arena behind the treeline. Loads
        // Resources/Arena/Backdrops/<theme>_backdrop onto 4 inward-facing UNLIT quads (cyclorama) so any
        // camera angle shows a painted horizon. Unlit = glows like a matte painting; fog fades the seams.
        // Skip-safe: no texture -> keep the persisted sky. Parented to _arenaRoot (auto teardown).
        private void BuildBackdrop(string theme)
        {
            Guard.Try("BattleArena", "build backdrop", () =>
            {
                string key = (theme ?? ArenaBiomeDressing.Forest).ToLowerInvariant();
                var tex = Resources.Load<Texture2D>("Arena/Backdrops/" + key + "_backdrop");
                if (tex == null) tex = Resources.Load<Texture2D>("Arena/Backdrops/forest_backdrop");
                if (tex == null) tex = Resources.Load<Texture2D>("Arena/Backdrops/outerworld_backdrop");
                if (tex == null)
                {
                    // No painted art -> NEVER degrade to bare sky. Build a runtime vertical gradient
                    // (biome sky -> horizon) so the arena always reads as an enclosed environment.
                    tex = BuildGradientBackdrop(key);
                    FlowTrace.Warn("BattleArena", "BuildBackdrop: no painted texture for '" + key + "' -> runtime gradient backdrop.");
                }
                else
                {
                    FlowTrace.Step("BattleArena", "BuildBackdrop: painted texture '" + tex.name + "' for '" + key + "'.");
                }

                // Build-safe shader chain: prefer Unlit (matte-painting glow), then legacy unlit, then
                // URP/Lit (guaranteed present — referenced by scene materials + Always-Included). The
                // unlit shaders are also in EnsureShadersIncluded's Always-Included list; this is the
                // belt-and-suspenders so a strip can never null the material into a no-show.
                var sh = Shader.Find("Universal Render Pipeline/Unlit");
                if (sh == null) sh = Shader.Find("Unlit/Texture");
                if (sh == null) sh = Shader.Find("Universal Render Pipeline/Lit");
                if (sh == null)
                {
                    FlowTrace.Warn("BattleArena", "BuildBackdrop: no usable shader found -> skipping backdrop (sky kept).");
                    return;
                }
                FlowTrace.Step("BattleArena", "BuildBackdrop: shader '" + sh.name + "'.");
                var mat = new Material(sh) { name = "ArenaBackdrop_" + key };
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                // DOUBLE-SIDED: the ring quads face inward; render BOTH faces so a winding/facing
                // mismatch can NEVER backface-cull them to invisible (the "built-but-invisible ->
                // bare sky" bug, authored headless/blind). Cull Off is winding-agnostic.
                MakeDoubleSided(mat);

                // Sky-top colour (sampled from the painting top, biome fallback) tints BOTH the top cap
                // and the camera clear so the seam where the ring meets the cap/sky is invisible.
                Color skyTop = SampleBackdropTop(tex, key);
                var capMat = new Material(sh) { name = "ArenaBackdropCap_" + key };
                if (capMat.HasProperty("_BaseColor")) capMat.SetColor("_BaseColor", skyTop);
                if (capMat.HasProperty("_Color")) capMat.SetColor("_Color", skyTop);
                capMat.color = skyTop;
                MakeDoubleSided(capMat);   // cap quad faces down; double-side so winding can't hide it.

                float r = Mathf.Max(ArenaHalfWidth, ArenaHalfDepth) + 16f;   // behind the treeline ring
                float h = 110f;                                              // tall: no combat camera angle sees over it
                var root = new GameObject("ArenaBackdrop");
                root.transform.SetParent(_arenaRoot.transform, false);
                root.transform.localPosition = new Vector3(0f, h * 0.32f, 0f);

                // FULL ENCLOSURE: 8 inward-facing quads at 45-degree steps close the old 45-degree CORNER
                // gaps the 4-cardinal cyclorama left open (where the persisted night skybox leaked through).
                // Quad faces inward: position at angle a -> yaw a+180.
                int built = 0;
                for (int i = 0; i < 8; i++)
                {
                    float a = i * 45f;
                    float ar = a * Mathf.Deg2Rad;
                    var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    q.name = "Backdrop_" + i;
                    q.transform.SetParent(root.transform, false);
                    q.transform.localPosition = new Vector3(r * Mathf.Sin(ar), 0f, r * Mathf.Cos(ar));
                    q.transform.localRotation = Quaternion.Euler(0f, a + 180f, 0f);   // face inward toward centre
                    q.transform.localScale = new Vector3(r * 1.25f, h, 1f);            // overlap neighbours -> no seams
                    var mr = q.GetComponent<MeshRenderer>();
                    mr.sharedMaterial = mat;
                    mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    StripColliders(q);
                    built++;
                }

                // TOP CAP: a large downward-facing quad sealing the top of the ring so a raised combat
                // camera can never look straight up into the persisted starry skybox. Sky-tinted unlit.
                var cap = GameObject.CreatePrimitive(PrimitiveType.Quad);
                cap.name = "Backdrop_Cap";
                cap.transform.SetParent(root.transform, false);
                cap.transform.localPosition = new Vector3(0f, h * 0.5f, 0f);          // top of the ring
                cap.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);          // normal points DOWN at the camera
                cap.transform.localScale = new Vector3(r * 2.8f, r * 2.8f, 1f);
                var capMr = cap.GetComponent<MeshRenderer>();
                capMr.sharedMaterial = capMat;
                capMr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                StripColliders(cap);

                // BELT-AND-SUSPENDERS: override the arena camera's clear to a SolidColor sky tint so even
                // if a sliver gap remained the persisted NIGHT skybox can never leak. Restored on teardown.
                ApplySkyOverride(skyTop);

                FlowTrace.Step("BattleArena", "BuildBackdrop: full enclosure '" + key + "' (8 quads + top cap, h=" + h.ToString("0") + ", r=" + r.ToString("0") + ").");
                // PERMANENT live instrumentation (owner steer 2026-06-23 "debug line background loaded"):
                // a headless encounter run / F8 felt-test self-PROVES the enclosure actually built
                // (quads + cap + sky override on the success path) — not inferred from code-reading.
                FlowTrace.Step("BattleArena", "BACKDROP loaded theme=" + key + " tex=" + tex.name + " quads=" + built + " cap=1 skyOverride=1");
            });
        }

        // Force a material to render BOTH faces (Cull Off) so inward-facing backdrop quads stay visible
        // regardless of Unity Quad winding — the decisive fix for "built-but-invisible -> bare sky".
        // URP _Cull: 0 = RenderFace.Both. Safe no-op if the shader lacks the property.
        private static void MakeDoubleSided(Material m)
        {
            if (m == null) return;
            if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);
            m.doubleSidedGI = true;
        }

        // Runtime vertical-gradient backdrop (biome sky at TOP -> horizon at BOTTOM) used only when no
        // painted art loads. CPU-readable, no committed asset — guarantees the arena is enclosed, never
        // bare sky. Colours mirror the SampleBackdropTop biome fallbacks for a seamless cap tint.
        private static Texture2D BuildGradientBackdrop(string key)
        {
            string k = key ?? "";
            Color top = k.Contains("cavern")   ? new Color(0.10f, 0.10f, 0.13f)
                      : k.Contains("desert")   ? new Color(0.85f, 0.78f, 0.62f)
                      : k.Contains("volcanic") ? new Color(0.32f, 0.14f, 0.12f)
                      :                          new Color(0.62f, 0.74f, 0.86f); // soft daylight sky
            Color horizon = k.Contains("cavern")   ? new Color(0.05f, 0.05f, 0.07f)
                          : k.Contains("desert")   ? new Color(0.70f, 0.60f, 0.45f)
                          : k.Contains("volcanic") ? new Color(0.55f, 0.22f, 0.12f)
                          :                          new Color(0.78f, 0.82f, 0.74f);
            const int h = 64;
            var t = new Texture2D(2, h, TextureFormat.RGB24, false)
            {
                name = "ArenaBackdropGradient_" + k,
                wrapMode = TextureWrapMode.Clamp
            };
            for (int y = 0; y < h; y++)
            {
                Color c = Color.Lerp(horizon, top, y / (float)(h - 1));
                t.SetPixel(0, y, c);
                t.SetPixel(1, y, c);
            }
            t.Apply();
            return t;
        }

        // Sky colour for the cap + camera clear. Tries the top row of the painting (so the cap/ring seam
        // is invisible); falls back to a biome-appropriate tint when the texture is not CPU-readable.
        private static Color SampleBackdropTop(Texture2D tex, string key)
        {
            string k = key ?? "";
            Color fallback = k.Contains("cavern") ? new Color(0.10f, 0.10f, 0.13f)
                           : k.Contains("desert") ? new Color(0.85f, 0.78f, 0.62f)
                           : new Color(0.62f, 0.74f, 0.86f); // default soft daylight sky
            Color result = fallback;
            Guard.Try("BattleArena", "sample backdrop top", () =>
            {
                if (tex != null && tex.isReadable)
                    result = tex.GetPixelBilinear(0.5f, 0.98f);
            });
            return result;
        }

        // Arena-camera sky override (belt-and-suspenders against the persisted NIGHT skybox leaking
        // over/through the enclosure). Per the spec we prefer the arena/bloom camera's clearFlags over
        // global RenderSettings so the open-world sky is untouched. Saved here, restored on Resolve.
        private void ApplySkyOverride(Color sky)
        {
            if (_skyOverridden) return;
            Guard.Try("BattleArena", "apply sky override", () =>
            {
                var cam = _bloomCam != null ? _bloomCam : Camera.main;
                if (cam == null)
                {
                    FlowTrace.Warn("BattleArena", "ApplySkyOverride: no arena camera -> night-sky leak still masked by the enclosure cap.");
                    return;
                }
                _skyCam = cam;
                _savedClearFlags = cam.clearFlags;
                _savedCamBg = cam.backgroundColor;
                _skyOverridden = true;
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = sky;
                FlowTrace.Step("BattleArena", "ApplySkyOverride: arena camera clearFlags=SolidColor (night skybox killed, saved for restore).");
            });
        }

        // Restore the saved arena-camera clear on Resolve so the open-world sky is unchanged on return.
        private void RestoreSkyOverride()
        {
            if (!_skyOverridden) return;
            Guard.Try("BattleArena", "restore sky override", () =>
            {
                if (_skyCam != null)
                {
                    _skyCam.clearFlags = _savedClearFlags;
                    _skyCam.backgroundColor = _savedCamBg;
                }
                FlowTrace.Step("BattleArena", "RestoreSkyOverride: arena camera clearFlags restored (open-world sky unchanged).");
            });
            _skyOverridden = false;
            _skyCam = null;
        }

        // ---------------------------------------------------------------------
        //  WO-504 #2: arena BLOOM - a local global URP Volume + code-built Bloom profile.
        // ---------------------------------------------------------------------
        // Why a LOCAL volume (not the shipped DefaultVolumeProfile): that profile's Bloom is
        // intensity 0 (off), and the far-offset arena should glow ONLY during the fight (no
        // global post-fx change leaking to the open world). The Volume is parented to
        // _arenaRoot so it tears down automatically on Resolve. Global mode (isGlobal=true)
        // so it covers the whole far-offset stage without a box collider. Priority above the
        // global profile so its Bloom override wins. Skip-safe (Guard) -> no glow, never throws.
        private void BuildArenaBloom()
        {
            Guard.Try("BattleArena", "build arena bloom volume", () =>
            {
                // Code-built profile (ScriptableObject, not an asset) so there is no Resources
                // dependency and nothing to drag-drop. A single Bloom override, HDR-thresholded.
                var profile = ScriptableObject.CreateInstance<VolumeProfile>();
                profile.name = "ArenaBloomProfile";

                var bloom = profile.Add<Bloom>(overrides: true);
                bloom.intensity.Override(Mathf.Max(0f, ArenaBloomIntensity));
                bloom.threshold.Override(Mathf.Max(0f, ArenaBloomThreshold));
                bloom.scatter.Override(Mathf.Clamp01(ArenaBloomScatter));
                // Mobile-cheap: leave high-quality filtering OFF (default) -> bloom only, light cost.

                // Tonemapping safety net: compress HDR back to SDR so the lit ground can never blow
                // out to white even with HDR camera + bloom. Neutral mode = perceptually faithful.
                var tonemap = profile.Add<UnityEngine.Rendering.Universal.Tonemapping>(true);
                tonemap.mode.Override(UnityEngine.Rendering.Universal.TonemappingMode.Neutral);

                var go = new GameObject("ArenaBloomVolume");
                go.transform.SetParent(_arenaRoot.transform, false);
                var vol = go.AddComponent<Volume>();
                vol.isGlobal = true;                 // covers the whole far-offset stage, no collider needed
                vol.priority = ArenaBloomPriority;   // outrank the global DefaultVolumeProfile
                vol.sharedProfile = profile;

                FlowTrace.Step("BattleArena", $"BuildArenaBloom: local bloom volume (intensity={ArenaBloomIntensity}, threshold={ArenaBloomThreshold}).");
            });
        }

        // Force the combat camera's post-processing + HDR ON for the fight so the arena Bloom
        // volume is applied and HDR > 1 VFX glow (the URP asset ships m_SupportsHDR off, so the
        // camera-level allowHDR is what lets the HDR materials exceed 1.0 into bloom). The prior
        // state is saved and restored on Resolve so the open-world camera is untouched. Skip-safe.
        private void EnableCombatBloomCamera()
        {
            if (_camStateSaved) return;
            Guard.Try("BattleArena", "enable combat bloom camera", () =>
            {
                var cam = Camera.main;
                if (cam == null) { FlowTrace.Warn("BattleArena", "EnableCombatBloomCamera: no Camera.main - bloom volume still applies if another camera has post-fx."); return; }

                _bloomCam = cam;
                _savedCamAllowHDR = cam.allowHDR;
                cam.allowHDR = true;

                var data = cam.GetComponent<UniversalAdditionalCameraData>();
                if (data != null)
                {
                    _hadCamData = true;
                    _savedCamPostFx = data.renderPostProcessing;
                    data.renderPostProcessing = true;
                }
                else
                {
                    _hadCamData = false;
                }

                _camStateSaved = true;
                FlowTrace.Step("BattleArena", $"EnableCombatBloomCamera: post-fx+HDR forced on (hadData={_hadCamData}).");
            });
        }

        // Restore the combat camera's saved post-fx/HDR state on Resolve. One-shot + null-safe
        // (the camera may have been destroyed between stage and resolve; we only touch it if live).
        private void RestoreCombatBloomCamera()
        {
            if (!_camStateSaved) return;
            Guard.Try("BattleArena", "restore combat bloom camera", () =>
            {
                if (_bloomCam != null)
                {
                    _bloomCam.allowHDR = _savedCamAllowHDR;
                    if (_hadCamData)
                    {
                        var data = _bloomCam.GetComponent<UniversalAdditionalCameraData>();
                        if (data != null) data.renderPostProcessing = _savedCamPostFx;
                    }
                }
                FlowTrace.Step("BattleArena", "RestoreCombatBloomCamera: open-world camera post-fx/HDR restored.");
            });
            _camStateSaved = false;
            _bloomCam = null;
        }

        private static void StripColliders(GameObject go)
        {
            var cols = go.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < cols.Length; i++)
                if (cols[i] != null) UnityEngine.Object.Destroy(cols[i]);
        }

        // Save the persisted RenderSettings then dim them to a stone-cave mood (cavern only).
        // Null-safe + one-shot (a second call without restore is ignored so we never clobber the save).
        private void ApplyCavernMood()
        {
            if (_moodSaved) return;

            // F8 2026-08-05 LEAK 2 (see the ARENA GLOBALS OWNERSHIP block above). RenderSettings
            // is GLOBAL to the active scene. In a composed dungeon these five writes stomp the
            // scene's AUTHORED mood — the dungeon ships ambient 0.05/0.05/0.055 at intensity 0.05
            // (pitch dark BY DESIGN: Lantern.cs, TorchWardenDress.cs), and this sets 0.18/0.17/0.22
            // at 0.55, a ~20x lift. RestoreCavernMood then correctly puts the authored values back
            // on Resolve — which is precisely the beat the owner saw as "the screen went black".
            // The arena never owned that mood, so it must not borrow it: SKIP.
            //
            // SYMMETRY IS STRUCTURAL, not a second gate: skipping here leaves _moodSaved == false,
            // and RestoreCavernMood's first line is `if (!_moodSaved) return;`. A skipped apply can
            // therefore NEVER be followed by a restore. That is why the gate lives here and only
            // here — a duplicated predicate on the restore side could drift and half-apply the pair.
            if (StagingInsideForeignScene())
            {
                FlowTrace.Warn("BattleArena",
                    $"ApplyCavernMood SKIPPED — staging inside '{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}', " +
                    "which OWNS its own RenderSettings ambient/fog (authored dark by design). Global mood left untouched; " +
                    "_moodSaved stays false so RestoreCavernMood is a no-op — apply+restore stay symmetric by construction.");
                return;
            }

            Guard.Try("BattleArena", "save+apply cavern mood", () =>
            {
                _savedFog = RenderSettings.fog;
                _savedFogColor = RenderSettings.fogColor;
                _savedFogDensity = RenderSettings.fogDensity;
                _savedAmbientLight = RenderSettings.ambientLight;
                _savedAmbientIntensity = RenderSettings.ambientIntensity;
                _moodSaved = true;

                RenderSettings.fog = true;
                RenderSettings.fogColor = new Color(0.10f, 0.10f, 0.13f);
                RenderSettings.fogMode = FogMode.Exponential;
                RenderSettings.fogDensity = 0.02f;
                RenderSettings.ambientLight = new Color(0.18f, 0.17f, 0.22f);
                RenderSettings.ambientIntensity = 0.55f;
                FlowTrace.Step("BattleArena", "ApplyCavernMood: dim stone-cave fog/ambient set (saved for restore).");
            });
        }

        // Restore the saved RenderSettings on Resolve so the open world is untouched on return.
        private void RestoreCavernMood()
        {
            if (!_moodSaved)
            {
                // Names the branch so the pairing is PROVABLE from a trace instead of inferred:
                // nothing was applied (non-cavern theme, or the F8 2026-08-05 foreign-scene skip),
                // so there is nothing to restore. This is the symmetric half of that gate.
                FlowTrace.Step("BattleArena",
                    "RestoreCavernMood: NO-OP — no cavern mood was applied (non-cavern theme, or the " +
                    "arena skipped it because the scene owns its own RenderSettings). Nothing restored.");
                return;
            }
            Guard.Try("BattleArena", "restore cavern mood", () =>
            {
                RenderSettings.fog = _savedFog;
                RenderSettings.fogColor = _savedFogColor;
                RenderSettings.fogDensity = _savedFogDensity;
                RenderSettings.ambientLight = _savedAmbientLight;
                RenderSettings.ambientIntensity = _savedAmbientIntensity;
                FlowTrace.Step("BattleArena", "RestoreCavernMood: open-world RenderSettings restored.");
            });
            _moodSaved = false;
        }

        private void BuildWall(Vector3 localPos, Vector3 size)
        {
            var wall = new GameObject("ArenaBound");
            wall.transform.SetParent(_arenaRoot.transform, false);
            wall.transform.localPosition = localPos;
            var box = wall.AddComponent<BoxCollider>();
            box.size = size;
            // No renderer -> invisible boundary.
        }

        // Tracks whether the textured-ground null fallback has already warned (once per session).
        private static bool _groundFallbackWarned;

        // Ground theme: lay the SOURCE REGION's real textured material on the floor so the
        // arena reads as an extension of where you stood (grass / dwarven-stone / sharp-stone),
        // tiled across the big plane. Skip-safe: a null Resources.Load keeps today's per-theme
        // Color tint exactly (LogWarning once) so a missing asset NEVER breaks the fight.
        private static void ApplyGroundTheme(GameObject floor, string theme)
        {
            var r = floor.GetComponent<Renderer>();
            if (r == null) return;

            string key = (theme ?? "outerworld").ToLowerInvariant();

            // theme -> Resources/Arena/<name> ground material (copied into Resources for runtime load).
            string matName;
            switch (key)
            {
                case "castle": matName = "Arena/Dwarven_Ground"; break;
                case "cavern": matName = "Arena/Floor_Sharp_Stones"; break;
                default:       matName = "Arena/Grass_1"; break;
            }

            Material loaded = null;
            Guard.Try("BattleArena", "load ground material '" + matName + "'", () =>
            {
                loaded = Resources.Load<Material>(matName);
            });

            if (loaded != null)
            {
                // Instance the shared material so tiling on the big plane does not mutate the asset.
                var inst = new Material(loaded) { name = "ArenaGround_" + key };
                if (inst.HasProperty("_BaseMap")) inst.SetTextureScale("_BaseMap", new Vector2(12f, 10f));
                if (inst.HasProperty("_MainTex")) inst.SetTextureScale("_MainTex", new Vector2(12f, 10f));
                r.sharedMaterial = inst;
                FlowTrace.Step("BattleArena", "ApplyGroundTheme: textured ground '" + matName + "' (theme '" + key + "').");
                return;
            }

            // Fallback: keep the original per-theme flat tint exactly as before.
            if (!_groundFallbackWarned)
            {
                _groundFallbackWarned = true;
                Debug.LogWarning("[BattleArena] ground material '" + matName + "' not found in Resources - using flat tint fallback.");
            }
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (sh == null) return;
            Color c;
            switch (key)
            {
                case "castle": c = new Color(0.55f, 0.55f, 0.60f); break;   // stone
                case "cavern": c = new Color(0.34f, 0.30f, 0.36f); break;   // cave
                default:       c = new Color(0.40f, 0.52f, 0.30f); break;   // grassy overworld
            }
            var m = new Material(sh) { name = "ArenaGround_" + key };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
            r.sharedMaterial = m;
        }

        // Spawn the orc family across the NORTH side as a BUILT MonsterFamily (WO-146): index 0 is
        // the FamilyLeader, the rest are FamilyMembers in formation. The pack APPROACHES the hero in
        // formation (the pivot's animated "led pack" feel), then disbands on arrival (WatchToResolution)
        // so every member fights the real 1vN (kite + peel one at a time, per the design doc). Reuses
        // the canonical FamilyTestSpawner pattern + EnemyFactory (the single spawn path, CLAUDE.md §9).
        private void SpawnFamily(EncounterParams p)
        {
            _liveEnemies.Clear();
            _familyLeader = null;
            _familyEngaged = false;
            Transform heart = _arenaRoot.transform; // arena-centre tether; hero-aggro (DEF-224) pulls them to the hero
            int n = Mathf.Clamp(p.EnemyIds.Length, 1, 7);   // WO-556: 6 family + 1 rare boss

            // Owner balance (2026-07-16): a hero UNDER the low-level threshold is never swarmed.
            // Cap the concurrent arena attackers to the shared LowLevelEnemyCap. Belt-and-suspenders
            // to the roll-time cap in OverworldEncounterSpawner.RollFamilyPack — this also catches
            // arena entries whose EnemyIds were pre-built (data-driven SpawnArea / catalog) and never
            // passed through that roll. Count-only; does not touch spawn placement/facing.
            int heroLevel = OverworldEncounterSpawner.CurrentHeroLevel();
            if (heroLevel < OverworldEncounterSpawner.LowLevelThreshold && n > OverworldEncounterSpawner.LowLevelEnemyCap)
            {
                FlowTrace.Step("Encounter", $"enemy count capped: level={heroLevel} requested={n} -> {OverworldEncounterSpawner.LowLevelEnemyCap} (arena).");
                n = OverworldEncounterSpawner.LowLevelEnemyCap;
            }

            // ── PROPER SQUAD FORMATION (owner 2026-07-03: "spawn in a proper formation") ──
            // The hero stands at the SOUTH edge (see StageRoutine: -ArenaHalfDepth+2) facing NORTH into
            // the spawn, so "nearest the hero" == the SMALLEST Z on the north side. We rank the family by
            // ROLE (RoleForId) into a real battle line, facing the hero:
            //   • FRONT rank  (nearest hero) = TANKS — the wall the hero meets first, spread laterally.
            //   • MID rank    (a rank back)  = DPS / Ranged / everything else.
            //   • REAR rank   (furthest)     = HEALERS — protected behind the line.
            // Spacing is a small owner-adjustable table (below), not scattered literals. If a rank has no
            // members the others simply close up. Robust: RoleForId always resolves (defaults DPS), and a
            // solo enemy just spawns at the front-centre — so this degrades gracefully to the old feel.
            int[] formRank = new int[n];   // 0 = front, 1 = mid, 2 = rear
            int[] formSlot = new int[n];   // lateral index within the rank (spawn order)
            int[] rankFill = new int[3];
            for (int i = 0; i < n; i++)
            {
                formRank[i] = FormationRankForRole(EnemyBrain.RoleForId(p.EnemyIds[i]));
                formSlot[i] = rankFill[formRank[i]]++;
            }

            for (int i = 0; i < n; i++)
            {
                string id = p.EnemyIds[i];
                EnemyDef def = BuildEncounterDef(id, p.Threat);

                // Rank -> Z (front rank sits FormationRankDepth closer to the hero per rank forward of
                // the rear anchor). Lateral slot -> X, centred on x=0 and spread by FormationLateralGap.
                int rankMembers = Mathf.Max(1, rankFill[formRank[i]]);
                float lateral = (formSlot[i] - (rankMembers - 1) * 0.5f) * FormationLateralGap;
                float z = FormationRearAnchorZ - (2 - formRank[i]) * FormationRankDepth;
                Vector3 pos = ArenaCentre + new Vector3(lateral, 0f, z);
                if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 8f, NavMesh.AllAreas)) pos = hit.position;

                Vector3 toHero = (ArenaCentre + new Vector3(0f, 0f, -ArenaHalfDepth)) - pos; toHero.y = 0f;
                Quaternion rot = toHero.sqrMagnitude > 0.001f ? Quaternion.LookRotation(toHero) : Quaternion.identity;

                int idx = i;
                Enemy enemy = null;
                Guard.Try("BattleArena", $"spawn '{id}'", () =>
                {
                    enemy = EnemyFactory.Build(def, pos, rot, _arenaRoot.transform);
                    if (enemy == null) return;
                    enemy.gameObject.name = $"ArenaEnemy_{id}_{idx}";
                    enemy.Configure($"encounter-{id}-{idx}", def, heart);
                    var brain = enemy.gameObject.AddComponent<EnemyBrain>();
                    EnemyRole role = EnemyBrain.RoleForId(id);
                    brain.Role = role;
                    brain.RosterId = id;   // owner ruling 2026-08-06: gates weapon attach (casters carry nothing)
                    // WO-482 (felt-fix 2026-06-24): the arena is an ISOLATED duel -- there is NO
                    // base to siege here. Mark the brain hero-only so target selection ALWAYS
                    // picks the hero and never falls back to the home-scene HeartOfElarion (~7000m
                    // away), which is what made the orcs mill ("no COMPLETE path to HeartOfElarion").
                    brain.SetHeroOnlyTarget(true);
                    FlowTrace.Step("BattleArena", $"ARENA orc '{id}' target = hero-only (no heart siege).");
                    EnemyBrain.ApplyRoleTactics(brain, role);
                    FlowTrace.Step("BattleArena", $"ROLE '{id}': tactics applied for {role}.");
                    // MonsterFamily wiring: first unit leads; the rest follow in formation.
                    if (idx == 0)
                        _familyLeader = enemy.gameObject.AddComponent<FamilyLeader>();
                    else if (_familyLeader != null)
                        _familyLeader.RegisterMember(enemy.gameObject.AddComponent<FamilyMember>());
                });

                if (enemy != null)
                {
                    _liveEnemies.Add(enemy);
                    enemy.Died += HandleEnemyDied;
                    FlowTrace.Step("BattleArena", $"SpawnFamily: '{id}' (role {EnemyBrain.RoleForId(id)}) at {pos}{(idx == 0 ? " [LEADER]" : " [follower]")}.");
                }
            }
        }

        // The pack approaches in formation; once the LEADER reaches the hero, disband the family so
        // every member breaks to fight (FamilyLeader.OnDisable -> Disband -> members StopFollowing ->
        // their EnemyBrain re-enables -> all engage via hero-aggro). One-shot.
        private void MaybeDisbandOnArrival()
        {
            if (_familyEngaged || _familyLeader == null) return;
            var heroGo = GameObject.FindWithTag("Player");
            if (heroGo == null) return;

            // §12 ROOT-CAUSE CAPTURE (overworld-wedge): one-shot POST-WARP hero position on the first
            // march tick. With BeginEncounter's pre-stage capture this pins the culprit: if the hero
            // still reads OUTSIDE the arena here, the warp-in FAILED (candidate A) and the family can
            // never close the 4.5m disband gate (the leader is ~7km away); if in-arena, the warp landed
            // and any non-resolution is a fled/scattered pack (candidate B) instead.
            if (!_marchPosLogged)
            {
                _marchPosLogged = true;
                Vector3 hp = heroGo.transform.position;
                FlowTrace.Step("BattleArena",
                    $"MARCH HERO-POS (post-warp): pos={hp} inArena={IsArenaPosition(hp)} leader={_familyLeader.transform.position} centre={ArenaCentre}.");
            }

            float dist = Vector3.Distance(_familyLeader.transform.position, heroGo.transform.position);
            FlowTrace.Throttle("BattleArena", "march-dist", 1f, $"MARCH leader dist={dist:0.0}m to hero (4.5m gate).");
            if (dist <= 4.5f)
            {
                _familyEngaged = true;
                _familyLeader.enabled = false;   // triggers Disband(): the pack breaks to fight
                FlowTrace.Step("BattleArena", "family reached the hero -> DISBAND (formation -> 1vN melee).");
            }
        }

        // WO-556 ITEM 2: roll the rare boss and, on a hit, APPEND it to the family ids. Mutates
        // p.EnemyIds in place (the staging reads it next). Instrumented so the rate is provable.
        // No-op if the boss is already present (idempotent) so a re-roll can't double-stack it.
        private static void MaybeAddBoss(EncounterParams p)
        {
            if (p == null || p.EnemyIds == null) return;
            foreach (var existing in p.EnemyIds)
                if (string.Equals(existing, BossEnemyId, StringComparison.OrdinalIgnoreCase)) return;

            float roll = UnityEngine.Random.value;
            bool add = roll <= BossSpawnChance;
            FlowTrace.Step("BattleArena",
                $"BOSS ROLL chance={BossSpawnChance:0.00} rolled={roll:0.000} -> {(add ? "ADD boss '" + BossEnemyId + "'" : "none")}.");
            if (!add) return;

            var augmented = new string[p.EnemyIds.Length + 1];
            System.Array.Copy(p.EnemyIds, augmented, p.EnemyIds.Length);
            augmented[augmented.Length - 1] = BossEnemyId;
            p.EnemyIds = augmented;
        }

        // ENCOUNTER FEEDBACK: a player-facing label for the engaged family, derived from the
        // EncounterParams ids (presentation only — no logic). An all-orc family reads "Orc Warband"
        // (matching the rep's DisplayName); otherwise the leader id is humanised. ASCII-only (the
        // legacy runtime font is ASCII).
        private static string FoeLabel(EncounterParams p)
        {
            if (p == null || p.EnemyIds == null || p.EnemyIds.Length == 0) return "Foes";
            bool allOrc = true;
            foreach (var id in p.EnemyIds)
                if (id == null || id.IndexOf("orc", StringComparison.OrdinalIgnoreCase) < 0) { allOrc = false; break; }
            if (allOrc) return "Orc Warband";
            string lead = p.EnemyIds[0] ?? "Foes";
            lead = lead.Replace('-', ' ').Replace('_', ' ').Trim();
            if (lead.Length == 0) return "Foes";
            return char.ToUpperInvariant(lead[0]) + (lead.Length > 1 ? lead.Substring(1) : "");
        }

        // Map an EnemyRole -> a formation RANK bucket (0 = front line, 1 = mid, 2 = rear) so the
        // squad spawns as a real battle line facing the hero: TANKS take the front, HEALERS the
        // protected rear, and DPS/Ranged/everything-else the middle. Owner-tunable by moving a role
        // between buckets here (data-light, no scattered literals).
        private static int FormationRankForRole(EnemyRole role)
        {
            switch (role)
            {
                case EnemyRole.Tank:   return 0;  // front line, nearest the hero
                case EnemyRole.Healer: return 2;  // rear, furthest from the hero
                default:               return 1;  // DPS / Ranged / MiniBoss — mid rank behind the tanks
            }
        }

        // Synthesise a code EnemyDef for an encounter id (the orc family ids are not in
        // enemies.json -- same forward-design pattern as RegionMobSpawner.BuildRoamerDef).
        // Stats mirror the ATB engine orc defs (Defs.ENEMY_DEFS) so the two stay coherent;
        // threat lightly scales HP/damage.
        private static EnemyDef BuildEncounterDef(string id, int threat)
        {
            float t = 1f + Mathf.Clamp(threat - 1, 0, 20) * 0.08f;   // +8% per threat tier
            string s = (id ?? "").ToLowerInvariant();

            float hp, dmg, spd, atk, height; string display;
            // WO-556 ITEM 2 — the rare BOSS (orc-warlord). Big, beefy, slower; its id resolves
            // to the verified Orc_Necromancer model (heaviest orc silhouette). Height 2.6 so it
            // reads visibly bigger than the family. Stats well above the family so it is a real
            // boss beat, lightly threat-scaled like the rest.
            // Owner 2026-07-02 — PRECISE one-title display names ("Orcish Mage", never a
            // stacked "Orc Mage Wizard"); the HUD target frame/cycle list read this
            // DisplayName via Enemy.DisplayName (single source of truth).
            if (s.Contains("warlord") || s.Contains("boss")) { display = "Orcish Warlord"; hp = 520; dmg = 34; spd = 2.6f; atk = 1.8f; height = 2.6f; }
            // F8-8 — HOLLOW family rows. The overworld scatter records stage hollow packs through
            // this same synthesizer; without these branches "hollow-warrior" substring-matches the
            // ORC "warrior" row below and titles a skeleton "Orcish Warrior" (owner canon: one
            // PRECISE title per enemy). Checked BEFORE the orc rows. Stats sit in the same softened
            // early-game band as the orcs, coherent with the ATB Defs.ENEMY_DEFS hollow entries.
            else if (s.Contains("hollow-rogue"))   { display = "Hollow Rogue";   hp = 58;  dmg = 13; spd = 3.6f; atk = 1.0f; height = 1.9f; }
            else if (s.Contains("acolyte"))        { display = "Hollow Acolyte"; hp = 50;  dmg = 8;  spd = 2.8f; atk = 1.6f; height = 1.9f; }
            else if (s.Contains("hollow-warrior")) { display = "Hollow Warrior"; hp = 84;  dmg = 15; spd = 3.0f; atk = 1.2f; height = 2.0f; }
            else if (s.Contains("hollow"))         { display = "Hollow Walker";  hp = 55;  dmg = 9;  spd = 2.8f; atk = 1.3f; height = 1.9f; }
            // 2026-07-01 owner call — early overworld orcs ~35% softer (HP+dmg ×0.65) so new
            // players aren't slaughtered. Warlord (boss above) left intact. NOTE: these are
            // HARDCODED here (not read from enemies.json) — see follow-up ticket to make the arena
            // read the canonical enemy catalog so future balance is a data tune, not a code edit.
            else if (s.Contains("tank"))    { display = "Orcish Bulwark"; hp = 124; dmg = 12; spd = 2.2f; atk = 1.6f; height = 2.3f; }
            else if (s.Contains("mage"))    { display = "Orcish Mage";    hp = 55;  dmg = 14; spd = 3.0f; atk = 1.4f; height = 1.9f; }
            else if (s.Contains("warrior")) { display = "Orcish Warrior"; hp = 78;  dmg = 16; spd = 3.2f; atk = 1.2f; height = 2.0f; }
            else                            { display = "Orcish Raider";  hp = 65;  dmg = 10; spd = 3.0f; atk = 1.2f; height = 1.9f; }

            // WO-1103 item 2: REWARDS now read the CANONICAL CATALOG (the follow-up the
            // 2026-07-01 note above promised) — base xp/coin + rewardVariance come
            // from the enemies.json row for this id, threat-scaled by the same t multiplier
            // the stats use. Ids with no catalog row (the arena-only orc-warrior/tank/mage/
            // warlord synthetics) keep the legacy synthesized values plus a code-default
            // variance so they stay range-bound too. Combat stats stay synthesized either
            // way (this WO touches rewards only).
            int xpBase, coinBase; float variance;
            EnemyDef row = CatalogRow(id);
            if (row != null)
            {
                xpBase      = Mathf.RoundToInt(row.XpReward * t);
                coinBase    = row.CoinReward > 0 ? Mathf.RoundToInt(row.CoinReward * t) : 0;
                variance    = row.RewardVariance;
                FlowTrace.Step("BattleArena",
                    $"REWARD DEF id={id} source=catalog baseXp={row.XpReward} baseCoin={row.CoinReward} " +
                    $"var={variance:0.00} threatScale={t:0.00} -> xp={xpBase} coin={coinBase}");
            }
            else
            {
                xpBase      = Mathf.RoundToInt(14 * t);
                coinBase    = 0;   // Enemy.Die's XP-derived gold fallback keeps the kill paying
                variance    = FallbackRewardVariance;
                FlowTrace.Warn("BattleArena",
                    $"REWARD DEF id={id} source=synthesized (no enemies.json row) baseXp={xpBase} " +
                    $"var={variance:0.00} threatScale={t:0.00} — add a catalog row to data-tune this id.");
            }

            return new EnemyDef
            {
                Id = id, Name = display, DisplayName = display, Ai = "walker",
                Hp = hp * t, MoveSpeed = spd, ContactDamage = dmg * t, AttackInterval = atk,
                Height = height, AggroRadius = 18f,
                XpReward = xpBase, CoinReward = coinBase,
                RewardVariance = variance,
            };
        }

        // WO-1103: code-default variance for arena ids that have NO enemies.json row
        // (orc-warrior/tank/mage/warlord synthetics). TUNABLE, mirrors the regular-enemy
        // data seed (0.15); catalog rows always win when present.
        private const float FallbackRewardVariance = 0.15f;

        // WO-1103: canonical enemies.json catalog, read ONCE per session through the same
        // CanonicalJson bytes the wave loader uses (the WildlandsRoster pattern). Null when
        // the catalog is missing/malformed — callers fall back to synthesized rewards.
        private static EnemyCatalog _rewardCatalog;
        private static bool _rewardCatalogLoaded;

        private static EnemyDef CatalogRow(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (!_rewardCatalogLoaded)
            {
                _rewardCatalogLoaded = true;   // one attempt per session; a failed read stays on fallback
                try
                {
                    string json = DeNelle.Core.CanonicalJson.Read(WaveDataLoader.EnemiesRelativePath);
                    if (!string.IsNullOrEmpty(json))
                        _rewardCatalog = Newtonsoft.Json.JsonConvert.DeserializeObject<EnemyCatalog>(json);
                    if (_rewardCatalog == null)
                        FlowTrace.Warn("BattleArena", "CatalogRow: enemies.json unreadable/empty — arena rewards stay synthesized this session.");
                }
                catch (Exception ex)
                {
                    FlowTrace.Warn("BattleArena",
                        $"CatalogRow: enemies.json parse failed ({ex.GetType().Name}: {ex.Message}) — arena rewards stay synthesized this session.");
                    _rewardCatalog = null;
                }
            }
            return _rewardCatalog != null ? _rewardCatalog.Find(id) : null;
        }

        // Warp the hero (by "Player" tag) to a stance. Reuses HeroLocomotion.WarpTo via
        // reflection (BattleArena is DeNelle.Village, but the hero may not be resolvable by
        // type here in all call orders, so a tag + reflection lookup is the safe path that
        // also raises OnTeleported so SmartMobileCamera snaps).
        private static void WarpHero(Vector3 pos, Quaternion rot)
        {
            var hero = GameObject.FindWithTag("Player");
            if (hero == null) { FlowTrace.Warn("BattleArena", "WarpHero: no 'Player' hero found - skipped."); return; }

            // F8-15 death forensic window: name the arena warp (stage-in / win-return /
            // loss-safe-retreat all route here). WarpTo logs the executed move with its caller;
            // this note carries the arena context either way.
            DeathTrace.Note($"HERO MOVE REQUESTED: BattleArena.WarpHero {hero.transform.position} -> {pos} (arena stage/return warp)");

            // ── F8 2026-08-05 (owner, live on device): "when i land i could not move at all" /
            //    "the dungeon is unmovable" — after WINNING a dungeon fight. Owner ruling:
            //    "IF YOU CANNOT WALK AND NAVIGATE THROUGH THE DUNGEON ITS A FAIL."
            //
            //    The arena writes a hero pose into a scene whose MOVER it does not own, and the
            //    write itself was unsafe on BOTH of that scene's movement components:
            //
            //    (a) CharacterController. The dungeon Keeper is driven by DungeonHero's CC, and
            //        DungeonController re-ENABLES it the instant OnBattleEnded fires
            //        (DungeonController.cs:1466) — which is the END of Resolve (:OnBattleEnded
            //        Invoke), i.e. BEFORE this return warp runs (a WIN defers the return behind
            //        the victory summary for up to 20s). HeroLocomotion.WarpTo then does a RAW
            //        `transform.position = worldPos` (HeroLocomotion.cs:317) onto a LIVE CC. That
            //        is not a teleport: the CC caches its own capsule pose and re-asserts /
            //        depenetrates it, so the hero can land wedged and unable to move. The scene's
            //        own teleport authority already does the only safe thing — disable the CC,
            //        move, re-enable (DungeonHero.Teleport, DungeonHero.cs:182-196). The arena
            //        must do the same for the pose IT writes. The toggle is same-frame, so
            //        DungeonHero.Update never observes the disabled CC (its own comment,
            //        DungeonHero.cs:207-208, relies on exactly that).
            //
            //    (b) NavMeshAgent. WarpTo unconditionally leaves the agent ENABLED
            //        (HeroLocomotion.cs:319) — that is correct for a seam warp onto a baked mesh,
            //        but a dungeon has NO bake, and DungeonController deliberately keeps that
            //        agent DISABLED so the CC is the sole mover (EnsureSingleDungeonMover,
            //        DungeonController.cs:816-820). Re-enabling it hands the scene back two live
            //        movers on one transform. So: snapshot the agent's enabled state before the
            //        warp and RESTORE it after. Where the agent was already enabled (every
            //        overworld / seam case) this is byte-identical to today's behaviour; only the
            //        deliberately-disabled dungeon agent changes, and there it is simply not
            //        re-enabled behind the scene owner's back.
            var cc = hero.GetComponent<CharacterController>();
            bool ccWasEnabled = cc != null && cc.enabled;
            var agent = hero.GetComponent<NavMeshAgent>();
            bool agentWasEnabled = agent != null && agent.enabled;
            if (ccWasEnabled) cc.enabled = false;

            try
            {
                var loco = hero.GetComponent("HeroLocomotion") as MonoBehaviour;
                var warp = loco != null
                    ? loco.GetType().GetMethod("WarpTo", new[] { typeof(Vector3), typeof(Quaternion?) })
                    : null;
                if (warp != null)
                {
                    warp.Invoke(loco, new object[] { pos, (Quaternion?)rot });
                    FlowTrace.Step("BattleArena",
                        $"WarpHero -> {pos} (mover-safe: cc={(cc == null ? "none" : ccWasEnabled ? "suspended+restored" : "already off")}, " +
                        $"agent={(agent == null ? "none" : agentWasEnabled ? "enabled" : "kept DISABLED — scene owner's sole-mover rule")}).");
                }
                else
                {
                    // F8-15: the raw-transform fallback bypasses WarpTo's chokepoint log — attribute it here.
                    DeathTrace.HeroMoved(hero.transform.position, pos,
                        "BattleArena.WarpHero", "transform fallback (WarpTo not found)", always: true);
                    hero.transform.SetPositionAndRotation(pos, rot);
                    FlowTrace.Warn("BattleArena", "WarpHero: WarpTo not found - used transform fallback.");
                }
            }
            finally
            {
                // Restore the movers in the order the scene owner expects: agent state first
                // (so nothing re-acquires a mesh it must not), then the collision body.
                if (agent != null && agent.enabled != agentWasEnabled) agent.enabled = agentWasEnabled;
                if (ccWasEnabled && cc != null) cc.enabled = true;
            }
        }

        // How far the hero may end up from the pose the arena asked for before we call it a
        // failed return. Generous enough for a ground-snap / depenetration settle, far tighter
        // than any of the real strandings seen in the F8 capture.
        private const float ReturnPoseDriftMeters = 2.5f;

        /// <summary>
        /// SAFETY NET (owner ruling 2026-08-05: "IF YOU CANNOT WALK AND NAVIGATE THROUGH THE
        /// DUNGEON ITS A FAIL"). One frame after the return warp — long enough for every other
        /// writer on the hero transform to have had its say — PROVE the hero actually landed
        /// where the arena put her, and that she still has a mover that can carry her.
        ///
        /// A player who cannot move must never be a SILENT state, so every failure here is a
        /// FlowTrace.Fail (loud -> break-log) that NAMES what is wrong, and is recovered where
        /// the arena legitimately can:
        ///   • DRIFTED  — something else wrote the pose after us (see the report: the ±50 clamp
        ///     in HeroLocomotion.Update:1085-1090 is one such writer). Re-assert ONCE, mover-safe.
        ///   • STRANDED — off the navmesh AND with no live CharacterController, i.e. no component
        ///     left that can move her at all. Sample the nearest valid point and place her there.
        /// Deliberately NOT treated as a fault: off-mesh WITH a live CharacterController. Every
        /// dungeon is unbaked, so off-mesh is the NORMAL, correct state there and the CC is the
        /// real mover — "snapping" her to a navmesh that does not exist would be a fake fix.
        /// </summary>
        private static void VerifyReturnPose(Vector3 wanted)
        {
            var hero = GameObject.FindWithTag("Player");
            if (hero == null)
            {
                FlowTrace.Fail("BattleArena",
                    "RETURN POSE VERIFY: no 'Player'-tagged hero after the return warp — cannot prove the hero landed anywhere.");
                return;
            }

            Vector3 got = hero.transform.position;
            float drift = Vector3.Distance(got, wanted);
            var cc = hero.GetComponent<CharacterController>();
            var agent = hero.GetComponent<NavMeshAgent>();
            bool onMesh = agent != null && agent.enabled && agent.isOnNavMesh;
            bool ccMover = cc != null && cc.enabled;
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;

            FlowTrace.Step("BattleArena",
                $"RETURN POSE VERIFY: scene='{scene}' wanted={wanted} got={got} drift={drift:F2}m " +
                $"agentOnMesh={onMesh} ccMover={ccMover} (a hero with neither is IMMOBILE).");

            if (drift > ReturnPoseDriftMeters)
            {
                FlowTrace.Fail("BattleArena",
                    $"RETURN POSE DRIFT {drift:F2}m > {ReturnPoseDriftMeters}m — another writer moved the hero " +
                    $"after the arena's return warp (wanted {wanted}, got {got} in '{scene}'). RE-ASSERTING the " +
                    "return pose once, mover-safe. If this line repeats, the other writer owns the hero and the " +
                    "arena cannot win the fight from here — fix the writer, not this net.");
                WarpHero(wanted, hero.transform.rotation);
                got = hero.transform.position;
                onMesh = agent != null && agent.enabled && agent.isOnNavMesh;
            }

            if (onMesh || ccMover) return;   // she has a mover — nothing to recover

            // No navmesh under her AND no live CharacterController: nothing on this hero can
            // move her. This is the unrecoverable-by-the-player state; never ship it silently.
            Vector3 recovered = got;
            bool sampled = false;
            Guard.Try("BattleArena", "recover stranded hero to nearest navmesh", () =>
            {
                if (NavMesh.SamplePosition(got, out var hit, 25f, NavMesh.AllAreas))
                {
                    recovered = hit.position;
                    sampled = true;
                }
            });

            if (sampled)
            {
                FlowTrace.Fail("BattleArena",
                    $"HERO STRANDED after the return: off-mesh with NO live CharacterController @ {got} in '{scene}' — " +
                    $"nothing could move her. RECOVERED to the nearest navmesh point {recovered} ({Vector3.Distance(got, recovered):F2}m). " +
                    "This net firing is itself a defect report: the return should never land her mover-less.");
                WarpHero(recovered, hero.transform.rotation);
            }
            else
            {
                FlowTrace.Fail("BattleArena",
                    $"HERO STRANDED after the return and UNRECOVERABLE from here: off-mesh with NO live " +
                    $"CharacterController @ {got} in '{scene}', and no navmesh within 25m to snap to. The player " +
                    "cannot move. The scene owner must hand back a mover (or bake) — the arena has no valid pose to give.");
            }
        }

        // LOSE-FLOW: a SAFE return point for a loss — pull back from the engagement spot along the
        // hero's (reversed) approach heading by LossSafeRetreatMeters so the hero lands OUTSIDE a
        // rep's ~14m aggro radius. Snapped onto the navmesh so the hero doesn't strand off-mesh /
        // below the floor; falls back to the raw offset (then the engagement spot) if no mesh is hit.
        private static Vector3 SafeLossReturnPosition(Vector3 engagePos, float engageYaw)
        {
            // The hero faced engageYaw at engage; retreat is BEHIND that facing (back toward where
            // the hero came from — typically toward the castle/seam, away from the rep ahead).
            Vector3 fwd = Quaternion.Euler(0f, engageYaw, 0f) * Vector3.forward;
            Vector3 retreat = engagePos - fwd.normalized * LossSafeRetreatMeters;

            // F8 2026-07-30 seq512 (dungeon defeat freeze): the comment above always promised an
            // engage-spot fallback, but the code shipped the RAW 18m offset when no mesh answered.
            // In an UNBAKED scene (every dungeon) the sample always misses, so the overworld-sized
            // retreat manufactured a void coordinate 10m outside the cottage's west wall and the
            // hero warped into nothing. Default = the ENGAGEMENT SPOT (known-good ground the hero
            // was literally standing on); only a successful navmesh sample upgrades it to the
            // retreat point. Baked scenes (village/overworld) sample fine - byte-identical there.
            Vector3 result = engagePos;
            Guard.Try("BattleArena", "snap loss-safe return to navmesh", () =>
            {
                if (NavMesh.SamplePosition(retreat, out var hit, 12f, NavMesh.AllAreas))
                    result = hit.position;
            });
            FlowTrace.Step("BattleArena", $"SafeLossReturnPosition: engage={engagePos} -> safe={result} (retreat {LossSafeRetreatMeters}m).");
            return result;
        }

        // CAMERA RE-LOCK: re-enable the follow camera and have it re-acquire the hero after the
        // death-cam + warp. SmartMobileCamera owns the open-world follow; the death-cam disabled
        // it and our warp moved the hero, so explicitly enable + snap it here. Reflection-soft so a
        // missing API never throws into Resolve.
        private static void ReacquireFollowCamera()
        {
            Guard.Try("BattleArena", "reacquire follow camera", () =>
            {
                var smc = SmartMobileCamera.Instance;
                if (smc == null) return;
                if (!smc.enabled) smc.enabled = true;   // death-cam disabled it; re-enable the follow
                // Snap the rig onto the hero immediately so it doesn't ease across the whole world
                // on return (the warp moved the hero while the camera was suspended).
                smc.ForceFollowImmediate();
                FlowTrace.Step("BattleArena", "ReacquireFollowCamera: follow camera re-enabled + snapped to hero.");
            });
        }

        // CAMERA RE-LOCK (cont.): clear any stale target lock on the hero's reticle so a loss return
        // doesn't keep the dead/old foe locked (which would re-aim abilities at nothing). Resolved
        // off the live hero; guarded.
        private static void ClearHeroTargetLock()
        {
            Guard.Try("BattleArena", "clear hero target lock", () =>
            {
                var hero = GameObject.FindWithTag("Player");
                if (hero == null) return;
                var indicator = hero.GetComponent<HeroTargetIndicator>();
                indicator?.ClearLock();
            });
            // WO-512 slice 2: also release the lock-on CAMERA framing so the open-world camera
            // returns to its normal auto-framing / free-look on resolve (loss + win). No-op if no
            // lock target was ever bound; eases back via the shared damp (never a snap).
            Guard.Try("BattleArena", "clear lock camera framing",
                () => SmartMobileCamera.Instance?.ClearLockTarget());
        }

        // WO-512 slice 2: re-bind the lock-on camera to the hero's CURRENT locked enemy each watch
        // tick. The HUD/indicator can switch or drop the lock without BattleArena knowing, so we
        // read the live LockedEnemyTarget and hand its transform to the camera (SetLockTarget eases
        // via the shared damp; null/no-lock clears the camera framing). Guard-wrapped + reflection-
        // soft so a missing actor never throws into the watch loop.
        private static void MaybeRebindLockCamera()
        {
            Guard.Try("BattleArena", "rebind lock camera", () =>
            {
                var smc = SmartMobileCamera.Instance;
                if (smc == null) return;
                var hero = GameObject.FindWithTag("Player");
                var indicator = hero != null ? hero.GetComponent<HeroTargetIndicator>() : null;
                var locked = indicator != null ? indicator.LockedEnemyTarget as MonoBehaviour : null;
                if (locked != null) smc.SetLockTarget(locked.transform);
                else                smc.ClearLockTarget();
            });
        }

        // ---------------------------------------------------------------------
        //  Watch -> resolve
        // ---------------------------------------------------------------------
        private void HandleEnemyDied(Enemy e)
        {
            _liveEnemies.Remove(e);
            // WO-1103 item 3 (fixes B-1 + B-2): count ACTUAL kills. The battle payout is
            // paid from this counter, not p.EnemyIds.Length — so a low-level CAPPED spawn
            // pays only what was fought, and the 5% bonus boss (spawned into the family)
            // pays exactly like any other body it added.
            _killCount++;
            // WO-493 #4: remember the BATTLE-WINNING body so the death-cam lingers on the
            // climactic kill (only when this death empties the family). Reserved for the
            // last death -- not every kill.
            if (_liveEnemies.Count == 0 && e != null) _climaxBody = e.transform;
            FlowTrace.Step("BattleArena", $"enemy down; kills={_killCount}, {_liveEnemies.Count} remain.");
        }

        /// <summary>
        /// WO-1103: bank one arena kill's ROLLED per-enemy grant (already paid to the hero
        /// by Enemy.Die) into this battle's stream, so the victory SUMMARY can report the
        /// TOTAL actually banked (battle slice + per-enemy stream) instead of under-
        /// reporting. Called by Enemy.Die for kills inside a live staged arena only.
        /// </summary>
        public void ReportArenaKillGrant(int xp, int gold)
        {
            if (!BattleInProgress) return;
            _killStreamXp   += Mathf.Max(0, xp);
            _killStreamGold += Mathf.Max(0, gold);
            FlowTrace.Step("BattleArena",
                $"KILL STREAM banked +{xp} XP +{gold} gold (stream total {_killStreamXp} XP / {_killStreamGold} gold).");
        }

        private IEnumerator WatchToResolution()
        {
            float deadline = Time.time + BattleTimeoutSeconds;
            // Reset the self-heal transients for THIS battle (see field decls).
            _heroOutOfArenaSince = -1f;
            _marchPosLogged      = false;
            _lastCloseContactTime = Time.time;
            _heroMissingSince    = -1f;
            while (!_resolved)
            {
                // Pack approaches in formation, then breaks to fight when it reaches the hero.
                MaybeDisbandOnArrival();

                // ── SELF-HEAL WATCHDOGS (overworld-wedge + fled-pack fix) ─────────────────────────
                // A staged battle freezes EVERY home rep (RepEngageWatcher.PauseAll in BeginEncounter)
                // and only ResumeAll()s on Resolve. Two failure modes leave the battle unable to
                // resolve on its own — the home reps then stay frozen mid-aggro (and the HUD + hero
                // inputs stay locked on BattleLock) for the full 240s timeout. Both self-heal here by
                // force-resolving, which runs ResumeAll():
                //   (A) HERO NOT IN THE ARENA — a failed warp-in, or an ORPHANED _battlePaused from a
                //       prior encounter: the family can never reach the 4.5m disband gate (~7km away),
                //       so the fight never ends. Grace-timered so a legit mid-warp frame never trips it.
                //   (B) SCATTERED / FLED PACK — the hero IS in the arena but the enemies kited/fled out
                //       of reach (combo stays 0, _liveEnemies never empties). We LEASH them back within
                //       reach, and if none is reachable for a sustained window we break off the encounter.
                var watchHeroGo = GameObject.FindWithTag("Player");
                Vector3 heroPos = watchHeroGo != null ? watchHeroGo.transform.position : Vector3.zero;
                bool heroInArena = watchHeroGo != null && IsArenaPosition(heroPos);

                // ── (C) ABANDONMENT (patch 6, F8 2026-07-30 — same phantom-fight family) ──────────
                // A scene change / dungeon exit / hero death-EVAC mid-encounter ORPHANS this fight.
                // The host survives (DontDestroyOnLoad) but [BattleArena_Stage] is a plain ROOT object
                // in the ACTIVE scene and every enemy is parented under it — the load DESTROYS them.
                // _liveEnemies then empties on the RemoveAll below and HeroHealth.Instance is gone
                // (heroAlive defaults TRUE on a null), so the win gate fires Resolve(true): an unearned
                // 3-star payout plus a WarpHero that teleports the owner out of the scene he just
                // entered. Caught HERE, ABOVE the outcome arbitration, so it can never be read as a win.
                //   • stage gone = definitive: only Resolve() nulls _arenaRoot and _resolved gates this loop.
                //   • hero gone  = grace-timered so a body-swap/re-tag frame never trips it.
                if (_arenaRoot == null)
                {
                    ResolveAbandoned("the staged arena was destroyed under a live fight (scene unloaded)");
                    yield break;
                }
                if (watchHeroGo == null)
                {
                    if (_heroMissingSince < 0f) _heroMissingSince = Time.time;
                    float goneFor = Time.time - _heroMissingSince;
                    if (goneFor >= HeroMissingGraceSeconds)
                    {
                        ResolveAbandoned($"no 'Player'-tagged hero for {goneFor:0.0}s (dungeon exit / death-EVAC / scene change)");
                        yield break;
                    }
                }
                else _heroMissingSince = -1f;

                if (watchHeroGo != null && !heroInArena)
                {
                    // (A) hero out of arena — accumulate grace, then force-resolve to ResumeAll() reps.
                    if (_heroOutOfArenaSince < 0f) _heroOutOfArenaSince = Time.time;
                    float outFor = Time.time - _heroOutOfArenaSince;
                    if (outFor >= HeroOutOfArenaGraceSeconds)
                    {
                        FlowTrace.Fail("BattleArena",
                            $"WATCHDOG: hero OUT of arena {outFor:0.0}s (pos={heroPos} centre={ArenaCentre}) - failed warp-in / orphaned battle; force-resolving to ResumeAll() reps.");
                        Resolve(false);
                        yield break;
                    }
                }
                else
                {
                    _heroOutOfArenaSince = -1f;   // in-arena (or hero briefly absent) — reset the grace timer

                    // (B) leash + disengage apply only AFTER the family has broken formation to fight
                    // (_familyEngaged). Before that, the pack is legitimately marching in from the far
                    // rank (~30m+) and must NOT be leashed or counted as "out of contact" — that is the
                    // intended approach, not a flee. Post-engage is when a scatter/flee is the failure.
                    if (heroInArena && _familyEngaged)
                    {
                        // Leash the pack to the hero so a kiter/retreater can't flee out of reach, then
                        // track the last time ANY live enemy was within engage range.
                        LeashStagedEnemies(heroPos);
                        if (AnyEnemyWithin(heroPos, EngageContactRadius))
                        {
                            _lastCloseContactTime = Time.time;
                        }
                        else if (Time.time - _lastCloseContactTime >= DisengageResolveSeconds)
                        {
                            FlowTrace.Fail("BattleArena",
                                $"WATCHDOG: no live enemy within {EngageContactRadius:0}m for {DisengageResolveSeconds:0}s post-engage - pack scattered/unreachable; breaking off encounter (loss).");
                            Resolve(false);
                            yield break;
                        }
                    }
                }

                // WO-512 slice 2: keep the lock-on camera framed on the CURRENT locked foe. The
                // lock can switch (HUD cycle/tap) or auto-drop (target died) without BattleArena
                // observing it, so we re-read the live locked transform each tick and re-bind the
                // camera (a Transform set eased by the shared _leadPoint damp -> smooth re-frame,
                // never a snap). Flag-gated + Guard-wrapped so flag-off is today's exact path.
                if (FeatureFlags.LockOn) MaybeRebindLockCamera();

                // Outcome arbitration — DEATH PREEMPTS VICTORY. When the hero and the last
                // enemy die inside the same 0.25s tick (trade fatal blows / DoT / Last Stand
                // reflect), the old WIN-first order fired Resolve(true) on a dead hero — the
                // owner's F8 "on death the victory screen still loaded" (2026-07-06 t=278).
                _liveEnemies.RemoveAll(e => e == null || e.IsDead);
                // WO-563: SetPrimary removed — the 9-zone HUD reads enemy HP/target directly.
                var hh = HeroHealth.Instance;
                bool heroAlive = hh == null || hh.IsAlive;   // null = no HeroHealth (test scenes) — treat as alive
                if (_liveEnemies.Count == 0 && heroAlive)
                {
                    // WO-493 #4: linger on the climactic kill (slow-mo) BEFORE teardown/return.
                    yield return StartCoroutine(PlayDeathCam(_climaxBody, slowMo: true));
                    Resolve(true);
                    yield break;
                }

                // LOSE: hero down (checked AFTER the win gate so a heroAlive=false always lands here).
                if (!heroAlive)
                {
                    FlowTrace.Step("BattleArena", "hero down - loss.");
                    // Linger on the hero's defeat (no slow-mo -- the defeat beat plays at speed).
                    var heroGo = GameObject.FindWithTag("Player");
                    yield return StartCoroutine(PlayDeathCam(heroGo != null ? heroGo.transform : null, slowMo: false));
                    Resolve(false);
                    yield break;
                }

                // Safety: a stuck/AFK fight ends (loss) rather than soft-locking.
                if (Time.time >= deadline) { FlowTrace.Warn("BattleArena", "battle timeout - loss."); Resolve(false); yield break; }

                yield return new WaitForSeconds(0.25f);
            }
        }

        // FLED-PACK LEASH (fled-enemy fix): clamp any staged enemy that has drifted BEYOND LeashRadius
        // from the hero back to just inside that radius, so a kiter / low-HP retreater stays reachable
        // ("turn and fight") and the normal win condition can resolve. Normal kiting (~10m band) is
        // untouched — only a true flee past LeashRadius is pulled back, and the per-tick correction is
        // small and NavMesh-projected (never an off-mesh teleport). Contained to BattleArena; the enemy
        // AI is not modified. Called only while the hero is confirmed in-arena.
        private void LeashStagedEnemies(Vector3 heroPos)
        {
            // Read the (data-driven) bound ONCE per pass so every enemy in this tick is judged
            // against the same number, and so the captured trace below quotes the bound that
            // actually fired - the owner can then read a capture and retune aggro-tuning.json.
            float leash = LeashRadius;
            for (int i = 0; i < _liveEnemies.Count; i++)
            {
                var e = _liveEnemies[i];
                if (e == null || e.IsDead) continue;
                Vector3 pos = e.transform.position;
                Vector3 flat = pos - heroPos; flat.y = 0f;
                float dist = flat.magnitude;
                if (dist <= leash || dist < 0.001f) continue;

                Vector3 clamped = heroPos + flat.normalized * (leash * 0.95f);
                clamped.y = pos.y;
                if (NavMesh.SamplePosition(clamped, out NavMeshHit hit, 4f, NavMesh.AllAreas)) clamped = hit.position;
                e.transform.position = clamped;
                FlowTrace.Throttle("BattleArena", "leash", 1f,
                    $"LEASH: pulled '{e.name}' from {dist:0.0}m back to ~{leash * 0.95f:0.0}m of hero " +
                    $"(fled-pack guard; bound {leash:0.#}m from aggro-tuning.json - a BAIT inside this " +
                    "range is never pulled).");
            }
        }

        // True if ANY live (non-dead) staged enemy is within <paramref name="radius"/> of the hero
        // (flat XZ). Drives the disengage-resolve no-contact timer in WatchToResolution.
        private bool AnyEnemyWithin(Vector3 heroPos, float radius)
        {
            float r2 = radius * radius;
            for (int i = 0; i < _liveEnemies.Count; i++)
            {
                var e = _liveEnemies[i];
                if (e == null || e.IsDead) continue;
                Vector3 flat = e.transform.position - heroPos; flat.y = 0f;
                if (flat.sqrMagnitude <= r2) return true;
            }
            return false;
        }

        // WO-493 #4: run the climactic death-camera hold and WAIT for it before teardown/return.
        // Skip-safe: a null body or any failure ends instantly (the fight resolves as before, the
        // existing return-to-engagement-spot flow is untouched -- this only inserts a brief linger).
        private IEnumerator PlayDeathCam(Transform body, bool slowMo)
        {
            if (body == null)
            {
                FlowTrace.Warn("BattleArena", "PlayDeathCam: no body to linger on - skipping.");
                yield break;
            }

            if (_deathCam == null)
                Guard.Try("BattleArena", "create death cam", () => { _deathCam = gameObject.AddComponent<ArenaDeathCam>(); });
            if (_deathCam == null) yield break;

            FlowTrace.Step("BattleArena", $"PlayDeathCam: linger on '{body.name}' (slowMo={slowMo}).");
            _deathCam.Hold(body, slowMo);
            // Wait out the linger (safety-capped so a stuck hold never soft-locks the return).
            float guardDeadline = Time.unscaledTime + 7f;
            while (_deathCam.IsHolding && Time.unscaledTime < guardDeadline)
                yield return null;
        }

        // F8 2026-07-30 seq512: set when a scene-routing settle (dungeon defeat -> ExitToVillage)
        // owns the hero's next position — the pending ReturnHomeWithFade warp must not fire into
        // the scene that is leaving. Reset per BeginEncounter.
        private bool _returnWarpCancelled;

        /// <summary>
        /// Suppress the pending post-battle return warp (teardown/revive/camera/fade still run).
        /// Called by a settle path that is routing its OWN scene exit (e.g. the dungeon DEFEAT
        /// settle) so the hero is never warped into a scene that is unloading. Distinct from
        /// <see cref="ResolveAbandoned"/> — this battle HAS an outcome; only the warp is ceded.
        /// </summary>
        public void CancelPendingReturnWarp(string reason)
        {
            if (_returnWarpCancelled) return;
            _returnWarpCancelled = true;
            FlowTrace.Warn("BattleArena", $"CancelPendingReturnWarp: {reason}");
        }

        /// <summary>Retreat from the battle (Flee button): ends it as a loss + returns. No reward.</summary>
        public void Flee()
        {
            if (!BattleInProgress || _resolved) return;
            FlowTrace.Step("BattleArena", "Flee -> retreat (return to the open world, no reward).");
            Resolve(false);
        }

        /// <summary>
        /// ABANDONMENT TEARDOWN (patch 6, F8 2026-07-30 — the phantom-fight family). The encounter was
        /// ORPHANED mid-fight: the scene unloaded under the stage, the dungeon was exited, or the hero
        /// left / EVAC'd. There is NO OUTCOME, so this is deliberately NOT <see cref="Resolve"/> — no
        /// duration, no stars, no <c>GrantWinReward</c>, no victory burst, no result HUD, and above all
        /// NO <c>WarpHero</c> (the owner is wherever he went; the arena must never yank him back into a
        /// fight that stopped existing). It ONLY releases what staging took: the frozen home reps, the
        /// BattleLock/HUD posture gate, the render/camera overrides, the spawned combatants and the
        /// stage itself. Idempotent (_resolved-latched) and safe to call from a scene teardown.
        /// </summary>
        /// <param name="reason">Human-readable abandonment cause — lands in the [Flow:BattleArena] trace.</param>
        public void ResolveAbandoned(string reason)
        {
            if (!BattleInProgress || _resolved) return;
            _resolved = true;

            FlowTrace.Fail("BattleArena",
                $"ABANDONED: {reason} — tearing the encounter down with NO reward, NO victory UI, NO return warp.");

            // Stop the in-flight coroutines FIRST (StageRoutine's fade, WatchToResolution, PlayDeathCam)
            // so nothing on this persistent host keeps driving a fight whose scene is gone.
            StopAllCoroutines();

            // Give back every override staging took (mirrors Resolve's restore block exactly).
            RestoreCavernMood();
            RestoreSkyOverride();
            RestoreCombatBloomCamera();

            // Despawn the combatants, then the stage. Survivors ARE parented under _arenaRoot, but they
            // are destroyed explicitly so a re-parented/leaked one can never outlive the encounter.
            foreach (var e in _liveEnemies)
                if (e != null) Guard.Try("BattleArena", "despawn abandoned enemy", () => Destroy(e.gameObject));
            _liveEnemies.Clear();
            if (_arenaRoot != null) Guard.Try("BattleArena", "destroy abandoned stage", () => Destroy(_arenaRoot));
            _arenaRoot     = null;
            _familyLeader  = null;
            _familyEngaged = false;
            _climaxBody    = null;

            // Release the PRESENTATION gates. BattleArenaHud is DontDestroyOnLoad, so without this
            // Close() the overlay canvas survives the scene load as an orphan AND leaves the HUD kit's
            // Flee command bound to a dead fight. No ShowResult — there is no result to show.
            var hud = _hud;
            _hud = null;
            if (hud != null) Guard.Try("BattleArena", "close abandoned battle hud", () => hud.Close());
            _pendingLossBanner = null;   // never present a defeat panel for a fight nobody lost

            // Un-freeze the home reps (BeginEncounter's RepEngageWatcher.PauseAll). The single most
            // damaging leak if this teardown is skipped: every rep stays frozen mid-aggro for the rest
            // of the session. No post-loss grace / QuietNonPursuers — neither outcome happened.
            RepEngageWatcher.ResumeAll();

            // Same teardown, same leak class: an abandoned battle that skipped this would leave
            // the TOWN suspended for the rest of the session - a village that never ticks again.
            // Idempotent, so it is safe here even though this path never "resolved".
            DeNelle.Core.TownSuspension.Resume("arena battle abandoned");

            // Hand the camera back (a death-cam hold may have disabled the follow rig) and drop the
            // stale reticle/lock framing. Both are Guard-wrapped + hero-null-safe. Still NO WarpHero.
            ReacquireFollowCamera();
            ClearHeroTargetLock();

            // Arena BGM must not outlive the arena; and a StageRoutine stopped mid-fade would leave the
            // screen BLACK in the scene the player actually landed in, so reveal explicitly.
            Guard.Try("BattleArena", "restore ambient context after abandon", RestoreAmbientContext);
            Guard.Try("BattleArena", "clear fade after abandon", () =>
            {
                var fader = ScreenFader.EnsureInstalled();
                if (fader != null) StartCoroutine(fader.FadeInCo(HomeFadeInSeconds));
            });

            // Release the LOGIC gates last: BattleInProgress=false drops the BattleLock probe (hero
            // input + HudPosture hostile(activebattle)) and lets a fresh encounter stage cleanly.
            _current = null;
            BattleInProgress = false;

            // DELIBERATELY NOT raising OnBattleEnded: its dungeon listener settles the run
            // (SettleEncounter -> loot grant / boss credit / a SECOND ExitToVillage scene load). An
            // abandoned fight has no settlement — whoever abandoned it owns its own unwind
            // (DungeonController.AbandonRealtimeBattle), and a teardown-triggered abandon has no
            // listener left to hear it anyway.
            FlowTrace.Step("BattleArena",
                "ABANDONED: combatants despawned, stage destroyed, HUD released, reps resumed, " +
                "battle lock cleared — no reward granted, no return warp.");
        }

        // ---------------------------------------------------------------------
        //  HEADLESS ORACLE SEAM (WO-505) — drive the REAL Resolve() path with a
        //  synthetic context, no full PlayMode fight. Lets DeNelle.Editor's
        //  ArenaCombatOracle EXECUTE the win/loss resolve (audio cue + star/reward
        //  computation + GrantWinReward) and assert the FlowTrace FIRED lines were
        //  emitted — closing the "rests on code-reading inference" gap. This calls
        //  the SAME private Resolve the live fight calls (zero behaviour fork); it
        //  only pre-seeds the fields BeginEncounter would have set. Editor/QA seam
        //  only — never called by gameplay. duration lets the oracle pin a star tier.
        // ---------------------------------------------------------------------
        /// <remarks>
        /// WO-1103: <paramref name="kills"/> pre-seeds the kill counter the payout reads
        /// (default -1 = "clean sweep": every roster body killed). <paramref name="streamXp"/>/
        /// <paramref name="streamGold"/> pre-seed the per-enemy banked stream so the oracle can
        /// assert the SUMMARY total = battle slice + stream. This mirrors what HandleEnemyDied +
        /// ReportArenaKillGrant would have set — zero behaviour fork.
        /// </remarks>
        public void ResolveForTest(EncounterParams p, bool won, float durationSeconds,
                                   int kills = -1, int streamXp = 0, int streamGold = 0)
        {
            _current = p;
            _resolved = false;
            _climaxBody = null;
            _battleStartTime = Time.time - Mathf.Max(0f, durationSeconds);
            _killCount = kills >= 0 ? kills : (p != null && p.EnemyIds != null ? p.EnemyIds.Length : 0);
            _killStreamXp = Mathf.Max(0, streamXp);
            _killStreamGold = Mathf.Max(0, streamGold);
            BattleInProgress = true;
            Resolve(won);
        }

        // ---------------------------------------------------------------------
        //  Resolve + return (reward, tear down the stage, warp hero home)
        // ---------------------------------------------------------------------
        private void Resolve(bool won)
        {
            if (_resolved) return;
            _resolved = true;

            // WO-505 "battle closing": how long the fight took -> a 1..3 star rating. The
            // duration is read at the resolving moment so it reflects the actual fight.
            float durationSeconds = Mathf.Max(0f, Time.time - _battleStartTime);
            int stars = won ? BattleStarRating.StarsForDuration(durationSeconds) : 0;
            float rewardMult = won ? BattleStarRating.MultiplierForStars(stars) : 1f;
            FlowTrace.Step("BattleArena", $"Resolve: {(won ? "WIN" : "LOSS")} in {durationSeconds:0.0}s -> {stars} star(s), reward x{rewardMult:0.00}.");
            // WO-505 ORACLE FIRE-POINT (permanent live instrumentation — leave in behind the
            // FlowTrace toggle): the star/reward computation the headless ArenaCombatOracle reads
            // to PROVE (not infer) stars + multiplier were computed on this resolve path.
            FlowTrace.Step("BattleArena", $"STARS={stars} rewardMult={rewardMult:0.00} applied (won={won}).");

            // WO-505: play the climax CUE so the death-cam beat is not silent. Victory fanfare
            // on a win / defeat sting on a loss (the clips exist: Audio/Resources/victory.mp3,
            // defeat.mp3, wired by AudioBootstrap). Overworld BGM is restored after the banner
            // beat (RestoreAmbientAfter) so we never cut the climax to silence.
            // WO-505 ORACLE FIRE-POINT: an explicit FIRED line AT the PlayMusic call so the
            // headless oracle (and the owner's F8 felt-test break-log) prove the victory/defeat
            // cue actually fired on the real resolve path — not from code-reading. Permanent.
            FlowTrace.Step("BattleArena", won
                ? "VICTORY AUDIO FIRED track=Victory"
                : "DEFEAT AUDIO FIRED track=Defeat");
            Guard.Try("BattleArena", "battle result music cue",
                () => CoreServices.Audio?.PlayMusic(won ? MusicTrack.Victory : MusicTrack.Defeat));

            // REWARD (logic): grant the win payout and CAPTURE the itemized totals so the
            // victory SUMMARY (WO-556 ITEM 1) can list exactly what was awarded. Star count feeds
            // the gear-drop odds (ITEM 4). Defaults to zero on a loss (no reward).
            BattleRewardSummary totals = default;
            if (won) Guard.Try("BattleArena", "grant win reward",
                () => totals = GrantWinReward(_current, rewardMult, stars,
                                              _killCount, _killStreamXp, _killStreamGold));
            // WO-556 ITEM 1 ORACLE FIRE-POINT (permanent): the captured totals the summary reads,
            // so the headless ArenaCombatOracle PROVES the reward totals were captured + available
            // to the view (not inferred from code-reading). gear='-' when nothing dropped.
            if (won)
                FlowTrace.Step("BattleArena",
                    $"SUMMARY xp={totals.Xp} wisdom={totals.Wisdom} wood={totals.Wood} iron={totals.Iron} " +
                    $"gold={totals.Gold} kills={totals.Kills} gear={(string.IsNullOrEmpty(totals.GearName) ? "-" : totals.GearName)}");

            // WO-560: VICTORY REWARD BURST (juice). On a win, fire a celebratory VFX at the
            // hero + a small loot-pop per reward granted, escalating with the star rating
            // (1/2/3 -> more bursts). Uses ONLY existing celebration types (procedural gold
            // fallbacks — no pack prefab). Guarded + FlowTrace'd; does NOT touch the WO-556
            // summary (HUD) which is pushed separately below.
            if (won)
                Guard.Try("BattleArena", "victory reward burst",
                    () => PlayVictoryBurst(stars, totals));

            // Restore any cavern mood RenderSettings BEFORE the open world is back in view.
            RestoreCavernMood();

            // Restore the arena-camera sky clear (enclosure backdrop) so the open-world sky returns.
            RestoreSkyOverride();

            // WO-504 #2: hand the combat camera's post-fx/HDR back to its open-world state
            // (the Volume itself tears down with _arenaRoot below).
            RestoreCombatBloomCamera();

            // ENCOUNTER FEEDBACK: the stage teardown + the ~7km home WarpHero are now deferred into
            // ReturnHomeWithFade (below) so a black fade MASKS them — without that, fading out from a
            // live arena would flash the empty void at 5000,5000 once the stage is destroyed. We CAPTURE
            // the stage + survivors into locals and null the fields NOW (so a fresh BeginEncounter can
            // never collide with the still-standing far arena), and the coroutine destroys the captured
            // references under black. (The audio/stars/reward/banner above stay synchronous — the
            // headless ArenaCombatOracle reads those FlowTrace lines on this same call.)
            var capturedStage = _arenaRoot;
            _arenaRoot = null;
            var capturedSurvivors = new List<Enemy>(_liveEnemies);
            _liveEnemies.Clear();

            // LOSE-FLOW (owner TOP priority): make a LOSS RECOVERABLE — no instant re-engage.
            //   1) Despawn the triggering rep NOW (DestroyImmediate) if it somehow still exists,
            //      beating the queued-Destroy race that could leave it live as the hero returns.
            //   2) Open a post-loss re-aggro GRACE so NO rep can engage the hero for a few seconds,
            //      regardless of any rep's exact state — this is the loop-breaker.
            //   3) Warp the hero to a SAFE spot (pulled back along its approach heading past a rep's
            //      aggro radius), not the exact engagement spot inside aggro.
            if (!won && _current != null)
            {
                RepEngageWatcher.DespawnRepImmediate(_current.RepId);
                RepEngageWatcher.BeginPostLossGrace();   // ~3.5s no-engage window
            }

            // Compute the return pose. WIN: exact engagement spot (rep is dead). LOSS: safe retreat.
            Vector3 returnPos = _current != null ? _current.ReturnPosition : Vector3.zero;
            float   returnYaw = _current != null ? _current.ReturnYaw : 0f;
            if (!won && _current != null)
            {
                returnPos = SafeLossReturnPosition(_current.ReturnPosition, _current.ReturnYaw);

                // WO-949 (owner F8 2026-08-10 10:20 "On Death I should respawn in town not where I
                // died", ruling: respawn location = TOWN, every death context). PROVEN at HEAD from
                // the same session's break-log: the loss return WANTED (-53.29,0.08,5.28) then
                // (-71.13,0.08,34.72) - both SafeLossReturnPosition anchors metres from the engage
                // spot out in the field - and ReturnHomeWithFade then revived the hero IN PLACE
                // there, which is the felt "here is where I respawn". A LOSS where the hero DIED now
                // returns to the canonical TOWN anchor (HeroHealth.ResolveTownSpawn - the same
                // marker/injector point HandleDeath's hub branch uses), so the single return warp +
                // VerifyReturnPose + the in-place revive all land at town. A loss with the hero
                // still ALIVE (flee/regroup) keeps the safe pull-back - retreating is not dying.
                // Hub-gated: dungeon defeats cancel this warp anyway (CancelPendingReturnWarp) and
                // their scene exit owns the hero; a non-hub scene has no town anchor to resolve.
                var hhDead = HeroHealth.Instance;
                string lossScene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
                if (hhDead != null && !hhDead.IsAlive && DeNelle.Core.HubScenes.IsHub(lossScene))
                {
                    Vector3 lossAnchor = returnPos;
                    Guard.Try("BattleArena", "resolve TOWN respawn for death loss", () =>
                    {
                        returnPos = HeroHealth.ResolveTownSpawn();
                        return true;
                    }, false);
                    FlowTrace.Step("Respawn",
                        "death loss -> TOWN return (WO-949): loss anchor " + lossAnchor +
                        " overridden to town " + returnPos + " (scene='" + lossScene +
                        "') - the hero wakes at the town anchor, never on the corpse.");
                }
            }

            // MASKED RETURN (encounter feedback): hand the stage teardown + the ~7km home warp +
            // the camera re-lock to a short coroutine that brackets them in a black fade (mirrors the
            // RestoreAmbientAfter coroutine pattern). Resolve is synchronous, so this is how the
            // home hard-cut becomes an intentional fade. Only fires when a battle was actually staged
            // (_current set) — the headless ResolveForTest seam passes a synthetic _current.
            bool hadEncounter = _current != null;

            // WO-556 ITEM 1: the masked HOME RETURN (teardown + warp + fade). On a WIN it is
            // DEFERRED until the player taps Continue on the victory summary (or a ~20s timeout) so
            // the summary can breathe over the dead family; on a LOSS it fires IMMEDIATELY so the
            // recoverable lose-flow timing (post-loss grace) is preserved exactly as before. Latched
            // so it runs at most once (Continue + timeout both route here harmlessly).
            bool returnStarted = false;
            Action doMaskedReturn = () =>
            {
                if (returnStarted) return;
                returnStarted = true;
                if (hadEncounter)
                    Guard.Try("BattleArena", "schedule masked return",
                        () => StartCoroutine(ReturnHomeWithFade(returnPos, returnYaw, won, capturedStage, capturedSurvivors)));
                else
                {
                    // No params (defensive): tear down immediately, no warp/fade needed.
                    foreach (var e in capturedSurvivors) if (e != null) Guard.Try("BattleArena", "despawn enemy", () => Destroy(e.gameObject));
                    if (capturedStage != null) Destroy(capturedStage);
                    // HUD ISOLATION: kit is no longer force-hidden at stage time (posture handles the
                    // town->combat widget swap), so there is nothing to restore here. Posture re-evaluates
                    // to calm on return and re-populates the town widgets automatically.
                }
            };

            // Push the result to the VIEW. WIN -> the rich summary screen with a Continue button
            // that fires the deferred return (auto-times-out as a softlock guard). LOSS -> the quick
            // banner + immediate return (unchanged). If a WIN has NO hud (build failure), return
            // immediately so the hero can never be stranded at the far arena.
            var hud = _hud;
            _hud = null;
            if (won && hud != null)
            {
                // WO-969 (owner F8 seq 2315): doMaskedReturn is passed TWICE, and the second one is
                // the fix. As onContinue it is the player's CHOICE (tap Continue -> go home). As
                // onAbandon it is the arena RE-CLAIMING the transition the moment the screen is
                // destroyed without that choice - which is exactly what opening Pause over the
                // victory summary does (PROVEN BY CAPTURE: PanelManager.NotifyOpened 'Pause' ->
                // EndStateView.CloseFromArbiter -> Destroy -> the 45s watchdog had to rescue her).
                // The transition is now independent of the panel's lifetime; latched by
                // returnStarted, so exactly one of the two ever does anything.
                Guard.Try("BattleArena", "battle victory summary",
                    () => hud.ShowResult(true, stars, durationSeconds, totals, doMaskedReturn,
                                         onAbandon: doMaskedReturn));

                // =============================================================
                // STRANDING WATCHDOG (owner-reported twice: Seeker 313763 and desktop
                // EXE F8 seq=2140, "after I killed the enemies it spawned me back in arena").
                // -------------------------------------------------------------
                // doMaskedReturn is the ONLY path home from a won arena, and the line
                // above hands sole ownership of it to a UI object that THREE other code
                // paths may destroy without firing it:
                //   EndStateView.cs:92         a NEW Show() replaces the open panel
                //   EndStateView.cs:1414-1420  OnSceneLoaded
                //   EndStateView.cs:1425-1429  CloseFromArbiter (ANY other modal opens)
                // The worst offender is the village wave banner: WaveCelebrationManager
                // fires on OnWaveCleared with no battle guard, and the village WaveManager
                // keeps running while the hero is staged 7km away at ArenaCentre. On the
                // device a wave cleared 2.7s after the victory panel appeared, replaced it,
                // and the owner's tap hit the banner's action=dismiss instead.
                // Its AutoDismissAfter softlock guard dies with the same GameObject, so
                // BOTH escape routes are lost at once and the hero is left AT the arena
                // (proof: GetZone(x=5019.6,z=5010.0) vs ArenaCentre 5000,0,5000).
                //
                // The panel being destroyed without firing is CORRECT - a displaced
                // end-state must never silently trigger continue/respawn. The defect is
                // that ARENA OWNERSHIP was delegated to it. This watchdog takes that
                // ownership back: the arena guarantees the player gets home regardless of
                // what happens to any UI. Latched via returnStarted, so a normal Continue
                // still wins and this is a no-op.
                // =============================================================
                Guard.Try("BattleArena", "arm stranding watchdog",
                    () => StartCoroutine(StrandingWatchdog(() => returnStarted, doMaskedReturn)));
            }
            else if (won)
                doMaskedReturn();   // no HUD to host the summary -> return now (no softlock)
            else
            {
                // DCA fix (owner F8 2026-07-06 t=397 "two death screens"): the loss banner used
                // to show HERE (in-arena, pre-warp) and its only self-teardown listens for a
                // scene LOAD — but the masked return is an in-scene WarpHero teleport, so the
                // panel straddled fade-out -> warp -> fade-in and read as a SECOND death screen
                // in town (Player.log 224787 Show -> 225233 FADE IN -> 225458 user Close).
                // Present the banner ON ARRIVAL instead (mirrors the WIN summary semantics);
                // the return itself still fires immediately, so recovery timing is unchanged.
                _pendingLossBanner = () => Guard.Try("BattleArena", "battle result banner (on arrival)",
                    () => hud?.ShowResult(false, 0, durationSeconds, default, null));
                doMaskedReturn();   // loss returns immediately (recovery timing preserved)
            }

            // BATTLE ISOLATION: the fight is over — let home reps roam/chase/aggro again. (On a
            // loss the post-loss grace above still suppresses ENGAGE for a few seconds even though
            // the pause is lifted, so the hero recovers; a win lifts both gates cleanly.) The reps
            // live in the home scene; while the masked return fades, the hero is still at the far
            // arena, so a resumed rep reads a ~7km distance and cannot aggro until the warp lands.
            RepEngageWatcher.ResumeAll();

            // Release the town, with the return grace. This pairs with the Suspend in
            // BeginEncounter and MUST run on every exit path for the same reason ResumeAll
            // above must: a freeze that leaks is permanent for the rest of the session. It is
            // idempotent and safe when not suspended, so the abandon/watchdog teardowns that
            // force-resolve specifically to reach ResumeAll() cover this call too.
            //
            // The grace is the fix for the captured "wave cleared 2.7s after an arena victory"
            // stranding: the held wave cannot land while the hero is still being warped home.
            DeNelle.Core.TownSuspension.Resume("arena battle resolved");

            // RETURN TO PEACEFUL (owner F8 2026-07-10 "after battle is over should return to peaceful if not
            // being aggroed"): on a WIN, quiet every NON-pursuing rep back to calm roam so the overworld does
            // not read as still-in-combat with leftover reps milling in combat pose. Active chasers
            // (RepEngageWatcher.IsPursuing) are PRESERVED. A loss keeps the post-loss grace path above.
            if (won) RepEngageWatcher.QuietNonPursuersOnBattleEnd();

            // WO-505: restore explore BGM AFTER the victory/defeat cue has had its beat, so
            // the climax is not cut to silence (the banner shows for ~2.5s; we let the sting
            // breathe, then crossfade back to Overworld). Coroutine on this persistent
            // singleton; guarded so a teardown race never throws.
            Guard.Try("BattleArena", "schedule ambient restore",
                () => StartCoroutine(RestoreAmbientAfter(RewardCueSeconds)));

            var done = _current;
            _current = null;
            BattleInProgress = false;

            OnBattleEnded?.Invoke(done, won);

            // HONEST LOG. This used to read "stage torn down, hero returned" on a WIN -- while the
            // return was still DEFERRED behind the player's Continue tap. It claimed success ~3
            // seconds before anything moved, and it is the line a future reader would trust while
            // chasing exactly this bug. On a win, say what is actually true: we are WAITING.
            // The proving line for an actual arrival is "FADE IN: home arrival" (ReturnHomeWithFade).
            if (won)
                FlowTrace.Step("BattleArena",
                    "Resolve: battle ended, victory summary shown - home return is DEFERRED until " +
                    "Continue (watchdog armed). NOT yet returned; 'FADE IN: home arrival' proves arrival.");
            else
                FlowTrace.Step("BattleArena", "Resolve: stage torn down, hero retreated SAFE, battle ended.");

            // WO-1127: THE TEARDOWN CONTRACT. Nothing used to assert the world was back to baseline
            // when a battle ended, which is why a leaked hit-stop left the owner at 4% world speed
            // for three minutes on 2026-08-20 and read to her as frozen controls. The gate waits out
            // the reward screen, settles on the UNSCALED clock, then names any invariant still wrong.
            //
            // Guarded and fire-and-forget: this is diagnostics and it must never be able to break a
            // battle resolve. It is armed on BOTH outcomes - a retreat tears down the same systems a
            // win does, and a contract with a hole in it is not a contract.
            Guard.Try("BattleArena", "arm battle-end quiescence gate", () =>
                StartCoroutine(BattleQuiescenceGate.Arm(
                    won ? (System.Func<bool>)(() => EndStateView.IsShowing) : null,
                    won ? "arena win" : "retreat")));
        }

        /// <summary>
        /// Seconds the victory summary may sit before the arena stops trusting the UI and walks the
        /// player home itself. Generous: a player reading their spoils must never be yanked out.
        /// EndStateView's own AutoDismissAfter is ~20s, so this only ever fires when that guard died
        /// with its GameObject -- i.e. exactly the stranding case.
        /// </summary>
        private const float StrandWatchdogSeconds = 45f;

        /// <summary>
        /// WO-969: hard cap (unscaled seconds) on how long the masked home return will wait out a
        /// PAUSED game before proceeding anyway. Generous - a player may sit in the pause menu - but
        /// finite: a timeScale left at 0 by any other system must never become a new way to strand
        /// the hero. This is a COURTESY gate on presentation order, NOT a softlock net; the return
        /// itself is already owned by the arena the moment the screen hands it back.
        /// </summary>
        private const float PausedReturnHoldCapSeconds = 300f;

        /// <summary>
        /// Guarantees the masked home return happens even if the victory panel that owned it is
        /// destroyed without firing (see the call site for the three paths that do that).
        /// No-op when the player taps Continue normally -- <paramref name="alreadyReturned"/> latches.
        /// </summary>
        private System.Collections.IEnumerator StrandingWatchdog(Func<bool> alreadyReturned, Action doMaskedReturn)
        {
            float waited = 0f;
            while (waited < StrandWatchdogSeconds)
            {
                if (alreadyReturned != null && alreadyReturned())
                    yield break;                      // Continue fired - normal path, nothing to do.
                yield return null;
                waited += Time.unscaledDeltaTime;     // unscaled: a slow-mo cam must not stretch this
            }

            if (alreadyReturned != null && alreadyReturned()) yield break;

            // Section 12: never silent. This firing means a UI object ate the only route home.
            FlowTrace.Fail("BattleArena",
                $"STRANDING WATCHDOG FIRED after {StrandWatchdogSeconds:0}s - the victory panel was " +
                "destroyed without firing its Continue action, so the deferred home return never ran. " +
                "Returning the hero anyway. If you are reading this, find WHAT destroyed the end-state " +
                "(a wave banner or another modal opening over it) - the watchdog is a safety net, NOT the fix.");

            Guard.Try("BattleArena", "watchdog masked return", () => doMaskedReturn());
        }

        // WO-505: wait out the victory/defeat cue, then crossfade back to the open-world
        // ambient. Unscaled wait so a slow-mo death-cam time scale doesn't stretch it.
        private System.Collections.IEnumerator RestoreAmbientAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, seconds));
            RestoreAmbientContext();
        }

        /// <summary>
        /// Arena scenes are additive, so returning the hero does not retrigger scene music.
        /// Delegate to the position-aware world director instead of assuming every arena
        /// exit lands in the overworld. The fallback is only for bootstrap/teardown races
        /// where the director has not installed yet; it derives from the same zone source.
        /// </summary>
        private static void RestoreAmbientContext()
        {
            var director = WorldMusicDirector.Instance;
            if (director != null && director.ReapplyCurrentContext()) return;

            var hero = GameObject.FindGameObjectWithTag("Player");
            bool inWorld = hero != null &&
                           DeNelle.Core.World.ZoneManager.GetZone(hero.transform.position) !=
                           DeNelle.Core.World.RegionId.Village;
            CoreServices.Audio?.PlayMusic(inWorld ? MusicTrack.Overworld : MusicTrack.Village);
            FlowTrace.Warn("BattleArena",
                "WorldMusicDirector unavailable during arena ambient restore; derived " +
                (inWorld ? "Overworld" : "Village") + " directly from the return position.");
        }

        // WO-560: celebratory VFX burst on a WIN. A single WaveClear_Celebration at the
        // burst centre, plus a small loot-pop per reward actually granted, and (stars-1)
        // extra celebratory bursts ringing the centre so a 3-star clear reads bigger than a
        // 1-star. Centre = the hero (falls back to the climax body = last dead enemy). Uses
        // only existing celebration VFXType values (procedural gold fallbacks, no pack
        // prefab). FlowTrace the fire so it is observable headless / in the F8 break-log.
        private void PlayVictoryBurst(int stars, BattleRewardSummary totals)
        {
            var heroGo = GameObject.FindWithTag("Player");
            Vector3 centre = heroGo != null
                ? heroGo.transform.position
                : (_climaxBody != null ? _climaxBody.position : Vector3.zero);
            Vector3 lift = Vector3.up * 1.2f;

            // Main celebration burst at the hero.
            VFXManager.Play(VFXType.WaveClear_Celebration, centre + lift);

            // Loot-pop per reward granted (gold level-up pop), ringed around the centre so
            // each award reads as its own little burst.
            int pops = 0;
            void LootPop(bool granted)
            {
                if (!granted) return;
                float ang = pops * 1.1f;            // fan the pops out around the centre
                Vector3 off = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 1.0f;
                VFXManager.Play(VFXType.Juice_LevelUp, centre + lift + off, Quaternion.identity, playSound: false);
                pops++;
            }
            LootPop(totals.Xp > 0);
            LootPop(totals.Wisdom > 0);
            LootPop(totals.Wood > 0);
            LootPop(totals.Iron > 0);
            LootPop(!string.IsNullOrEmpty(totals.GearName));

            // Escalate with the star rating: each star ABOVE 1 adds an extra ringing burst.
            int extra = Mathf.Max(0, stars - 1);
            for (int i = 0; i < extra; i++)
            {
                float ang = (i + 0.5f) * 2.4f;
                Vector3 off = new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * 2.2f;
                VFXManager.Play(VFXType.Juice_WaveClear, centre + lift + off, Quaternion.identity, playSound: false);
            }

            FlowTrace.Step("BattleArena",
                $"VICTORY BURST FIRED stars={stars} lootPops={pops} extraBursts={extra} centre={centre}");
        }

        // ENCOUNTER FEEDBACK (2026-06-27): the masked home return. Fades to black, then UNDER black
        // tears down the captured far arena + survivors, warps the hero home, heals, and re-locks the
        // follow camera, then fades back in — so the ~7km return reads as an intentional transition,
        // not a hard cut. The stage + survivors are passed in (captured + fields-nulled in Resolve) so
        // a fresh battle can never collide with the still-standing arena. Unscaled fade -> timescale-
        // safe under a slow-mo death-cam. Guarded throughout; never throws into the return.
        private System.Collections.IEnumerator ReturnHomeWithFade(
            Vector3 returnPos, float returnYaw, bool won, GameObject stage, List<Enemy> survivors)
        {
            // WO-969: HOLD WHILE THE GAME IS PAUSED. The hand-back re-claims the return the instant
            // the victory screen dies, and the commonest killer is the player opening PAUSE over it
            // (owner F8 seq 2315). Pause zeroes Time.timeScale (PauseController.Pause), while every
            // fade here is UNSCALED - so without this gate the black fade + the 7km warp would play
            // out underneath the pause menu and she would un-pause already home, mid-fade. Gate on
            // timeScale (not on PauseController: DeNelle.Village must not reference DeNelle.Settings),
            // unscaled and hard-capped so a stuck timeScale can never wedge the route home.
            {
                float held = 0f;
                bool announced = false;
                while (Time.timeScale <= 0.0001f && held < PausedReturnHoldCapSeconds)
                {
                    if (!announced)
                    {
                        announced = true;
                        FlowTrace.Step("BattleArena",
                            "masked return re-claimed while the game is PAUSED - holding the fade until " +
                            "the player resumes (cap " + PausedReturnHoldCapSeconds.ToString("0") + "s).");
                    }
                    held += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (announced)
                    FlowTrace.Step("BattleArena",
                        "resumed after " + held.ToString("0.0") + "s paused - masked home return proceeding.");
            }

            var fader = ScreenFader.EnsureInstalled();
            FlowTrace.Step("BattleArena", "FADE OUT before home warp (mask the 7km return).");
            if (fader != null) yield return StartCoroutine(fader.FadeOutCo(HomeFadeOutSeconds));

            // Under black: tear the far arena down (no void flash), then warp the hero home.
            if (survivors != null)
                foreach (var e in survivors) if (e != null) Guard.Try("BattleArena", "despawn enemy", () => Destroy(e.gameObject));
            if (stage != null) Destroy(stage);

            // F8 2026-07-30 seq512: when a dungeon DEFEAT settle is already routing a scene exit
            // (ExitToVillage -> LoadSceneWithFade), warping the hero to the pre-fight spot fires
            // into a scene that is leaving — off-mesh landing + frozen Keeper. The settle cancels
            // the pending warp (CancelPendingReturnWarp) BEFORE this coroutine resumes from its
            // fade (OnBattleEnded runs while we are parked at the fade-out yield), so the flag is
            // always observed here. Teardown/revive/camera/fade still run either way.
            //
            // F8 2026-08-05 — WHY THIS WARP IS *NOT* SUPPRESSED ON A DUNGEON WIN. It was proposed
            // that the arena simply skip the warp because DungeonController.SettleEncounter logs
            // "hero resumes in place" (DungeonController.cs:1371). That log is about the RUN, not
            // the pose: the victory branch (DungeonController.cs:1368-1372) grants loot and clears
            // the combat lock and moves NOTHING. The hero is physically at ArenaCentre (5000,0,5000)
            // when this runs, so suppressing the warp on a WIN would strand her in the void 7km
            // from the dungeon with the stage just destroyed under her. Suppression stays exactly
            // what it was built for — the DEFEAT path, where ExitToVillage's scene load is the
            // thing that repositions her (DungeonController.cs:1356-1364).
            if (!_returnWarpCancelled)
            {
                WarpHero(returnPos, Quaternion.Euler(0f, returnYaw, 0f));
                // Give every other writer on this transform one frame to have its say, then PROVE
                // she landed with a working mover (owner: not being able to walk is a hard fail).
                yield return null;
                Guard.Try("BattleArena", "verify return pose", () => VerifyReturnPose(returnPos));
            }
            else
                FlowTrace.Warn("BattleArena", "return warp SUPPRESSED — a scene exit owns the hero.");

            // DEATH-CYCLE OWNERSHIP (F8 "Regroup breaks the death cycle", RCA 2026-07-12): on a
            // LOSS the hero arrives home still dead — ff.noautoheal skips RestoreToFull, the only
            // call on this path that cleared the death latch — and recovery leaned on HeroHealth's
            // RACING HandleDeath respawn (a SECOND warp to the town anchor: two HeroMoved in one
            // death window). The arena now OWNS the recovery: revive in place at the loss anchor
            // (Respawn clears the latch, restores control, applies the respawn HP fraction + grace).
            // HandleDeath defers while a battle owns the death (see HeroHealth.HandleDeath).
            {
                var hhLoss = HeroHealth.Instance;
                FlowTrace.Step("BattleArena",
                    $"loss-return state: won={won} heroAlive={(hhLoss != null && hhLoss.IsAlive)} " +
                    "(Regroup must never present over a dead hero).");
                if (!won && hhLoss != null && !hhLoss.IsAlive)
                {
                    Guard.Try("BattleArena", "revive hero on loss return", () =>
                        hhLoss.Respawn(hhLoss.transform.position));
                    // WO-949: a DEATH loss was warped to the TOWN anchor above (Resolve override),
                    // so "in place" here is town; a cancelled warp (dungeon settle) revives where
                    // the scene exit owns. Name the actual anchor so the capture proves which.
                    FlowTrace.Step("BattleArena",
                        "loss return: hero revived IN PLACE at " + hhLoss.transform.position +
                        " (death loss = the TOWN anchor since WO-949) — arena owns the death cycle.");
                }
            }

            // RETURN HEAL (owner felt-test 2026-06-24): top the hero off to FULL HP — the "rest up at
            // home base" beat. Null-safe; no-op on a downed hero (Respawn owns that). HP only.
            // SURVIVAL RULE (owner 2026-06-29): when ff.noautoheal is ON (default) HP/MP do NOT
            // auto-restore after combat — the field hero keeps what it ended the fight with and relies
            // on crafted potions; full recovery happens only at a SAFE ZONE (SafeZoneRecovery). The
            // return heal is GATED, not removed (reversible: PlayerPrefs "ff.noautoheal" = 0).
            if (FeatureFlags.NoAutoHeal)
            {
                FlowTrace.Step("Combat", "battle ended — no auto-heal (ff.noautoheal); use potions or a safe zone");
            }
            else
            {
                Guard.Try("BattleArena", "return heal hero to full", () =>
                {
                    var hh = HeroHealth.Instance;
                    if (hh != null) hh.RestoreToFull();
                });
                FlowTrace.Step("BattleArena", "RETURN heal: hero restored to full HP on town return.");
            }

            // CAMERA RE-LOCK: the death-cam released SmartMobileCamera and the warp moved the hero —
            // re-enable + snap the follow camera and clear the stale reticle lock, all under black so
            // the snap is invisible. (Unchanged behaviour; only the timing moved under the fade.)
            ReacquireFollowCamera();
            ClearHeroTargetLock();

            // HUD ISOLATION: the kit is no longer force-hidden at stage time, so there is no
            // whole-kit restore to do here. The posture system re-evaluates to calm on home arrival
            // and re-populates the town widgets automatically as the fade reveals home.

            yield return null;   // let the camera snap settle one frame under black

            FlowTrace.Step("BattleArena", "FADE IN: home arrival (masked return complete).");
            if (fader != null) yield return StartCoroutine(fader.FadeInCo(HomeFadeInSeconds));

            // Deferred loss banner (t=397 double-death fix): present the ONE defeat panel now,
            // at home, after the reveal — never straddling the warp. No-op on a win/no-banner.
            if (_pendingLossBanner != null)
            {
                var show = _pendingLossBanner;
                _pendingLossBanner = null;
                show();
            }
        }

        // Loss-banner presentation deferred to home arrival (set in Resolve's loss branch,
        // consumed at the end of ReturnHomeWithFade; cleared on consume so it fires once).
        private Action _pendingLossBanner;

        // Win reward (C2 — close the FELT reward loop): a staged-family/threat-scaled
        // payout the player FEELS, every drop routed to an EXISTING system (no parallel
        // economy — mirrors EnemyOutpost.GrantClearReward):
        //   1) hero XP        -> HeroProgression (kept; reflection, no ref-order assumption)
        //   2) skill points   -> WisdomCurrencyService.Grant (the talent-tree currency)
        //   3) resources      -> EconomyService.Grant (a small wood/iron bundle)
        //   4) gear (chance)  -> GearLoadout.Equip*ById (a low-tier weapon/armor drop)
        // V1-simple + deterministic-ish (formulas, not data files). Cross-module lookups
        // are Unity-fake-null-guarded (explicit != null, not ?.) per the lint.
        // WO-556 ITEM 1: returns the itemized totals it granted so the victory summary can list
        // them. WO-556 ITEM 4: stars feed the gear-drop odds. rewardMult is the star multiplier.
        // WO-1103 item 3: the battle payout scales on KILLS (the actual bodies downed, incl. the
        // bonus boss; fixes B-1 capped-spawn overpay + B-2 uncounted boss), never the roster.
        // streamXp/streamGold are the per-enemy ROLLED grants Enemy.Die already banked during
        // the fight — folded into the returned summary so the victory screen reports the TOTAL
        // actually banked (they are NOT granted again here).
        private static BattleRewardSummary GrantWinReward(EncounterParams p, float rewardMult, int stars,
                                                          int kills, int streamXp, int streamGold)
        {
            var summary = new BattleRewardSummary();
            if (p == null) return summary;
            int paidKills = Mathf.Max(0, kills);
            int threat = Mathf.Max(0, p.Threat);

            // WO-505: the star rating scales the FELT payout (1x / 1.25x / 1.5x). Applied to
            // every quantified grant below (XP, wisdom, resources) so a faster, cleaner win
            // pays more. Guarded to a sane floor so a bad value never zeroes the reward.
            float mult = Mathf.Max(1f, rewardMult);

            // 1) XP — unchanged grant path (HeroProgression via reflection); the count term is
            // now KILLS. §12 trace prints base/mult/final so the formula change is provable.
            int xpBase = 20 + 8 * paidKills + 4 * threat;
            int xp = Mathf.RoundToInt(xpBase * mult);
            var prog = GameObject.FindAnyObjectByType(Type.GetType("DeNelle.Village.HeroProgression, DeNelle.Village")) as MonoBehaviour;
            if (prog != null)
            {
                var add = prog.GetType().GetMethod("AddXp", new[] { typeof(float) });
                add?.Invoke(prog, new object[] { (float)xp });
            }
            summary.Xp = xp + Mathf.Max(0, streamXp);
            // WO-1104: report the coin stream + the kill count the payout was computed from.
            // Both are already-banked facts (Enemy.Die credited the gold; _killCount is the
            // measured body count) — nothing is granted a second time here.
            summary.Gold = Mathf.Max(0, streamGold);
            summary.Kills = paidKills;
            FlowTrace.Step("BattleArena",
                $"GrantWinReward: battle slice +{xp} XP (kills={paidKills} threat={threat} base={xpBase} mult={mult:0.00}); " +
                $"per-enemy stream +{Mathf.Max(0, streamXp)} XP / +{Mathf.Max(0, streamGold)} gold already banked " +
                $"-> summary total {summary.Xp} XP.");

            // 2) SKILL POINTS (Wisdom) — WO-763 (owner 2026-07-25): arena wins NO LONGER
            // grant Wisdom DIRECTLY. Wisdom is minted only at LEVEL-UP (+ level-gated tier
            // milestones) so new skills/magic feel EARNED over real time, not farmed by
            // repeat arena wins (the old (1 + family/2 + threat/2)*mult paid ~3–8/win and
            // was re-payable — the "lots of wisdom on exit of win" leak). The win STILL
            // earns Wisdom INDIRECTLY: its generous XP (below) levels the hero, and level-up
            // is the Wisdom gate. Summary Wisdom stays 0 so the victory screen is honest.
            summary.Wisdom = 0;

            // 3) RESOURCES — a small wood/iron bundle via the existing EconomyService
            // (same grant surface EnemyOutpost uses; no new resource path).
            int wood = Mathf.RoundToInt((10 + 4 * threat) * mult);
            int iron = Mathf.RoundToInt((4 + 2 * threat) * mult);
            var econ = EconomyService.Instance;
            if (econ != null)
            {
                // ⛔ REPORT WHAT THE BANK TOOK, NOT WHAT WE ASKED FOR (WO-1207, owner device
                // 2026-08-25: "7 foes killed earned 12 iron?" against a trace reading +15 iron).
                // Grant(ResourceCost) RETURNS the applied amount after the town bank cap clamps it;
                // the void convenience overload throws that answer away, so a victory screen with a
                // near-full store printed a number the player never received. Owner ruling the same
                // evening: battle rewards do NOT warn - collecting is a choice, a battle reward is
                // not - but silence is not a licence to be wrong. Silent and true, not loud and false.
                var appliedReward = econ.Grant(new ResourceCost(wood: wood, iron: iron));
                summary.Wood = appliedReward.Wood;
                summary.Iron = appliedReward.Iron;
                FlowTrace.Step("BattleArena",
                    $"GrantWinReward: +{appliedReward.Wood} wood, +{appliedReward.Iron} iron banked" +
                    (appliedReward.Wood != wood || appliedReward.Iron != iron
                        ? $" (requested +{wood}/+{iron}; the town bank cap trimmed it - no warning by ruling)"
                        : "."));
            }
            else
            {
                FlowTrace.Warn("BattleArena", "GrantWinReward: EconomyService null - resources not granted.");
            }

            // 4) GEAR (chance) — a low-tier drop equipped through the REAL armory API
            // (GearLoadout.Equip*ById), exactly like the outpost loot path but capped at
            // the low tiers so the arena stays a light, frequent reward.
            string gear = TryGrantArenaGear(threat, stars);
            summary.GearName = gear;
            if (gear != null)
                FlowTrace.Step("BattleArena", $"GrantWinReward: gear drop [{gear}] equipped.");

            return summary;
        }

        // Low-tier gear drop for an arena win — reuses the outpost's armory-grant pattern
        // (find the Player-tagged hero's GearLoadout, pick a catalog item the hero qualifies
        // for, equip it) but biased to common/uncommon. Drop chance rises a little with
        // threat. Returns the equipped item's display name, or null on no drop. Fake-null-safe.
        private static string TryGrantArenaGear(int threat, int stars)
        {
            // GEAR IS RARE (owner directive 2026-07-18): weapons/armor must feel like a special
            // find. Flat ~4% per-roll -> ~2% PER slot - NOT the old 30-85% (dropped on nearly
            // every win). Flat: no threat scaling, and the hard cap holds it there even with the
            // star bonus. ONE knob to tune -> baseChance. NOTE: on a hit the roll splits ~50/50
            // weapon vs armor, so 0.04 here == ~2% armor + ~2% weapon (~4% any-gear per-roll).
            // Materials/consumables (loot-tables.json) are UNAFFECTED - this only gates gear.
            const float baseChance = 0.04f;   // ~4% per-roll -> ~2%/slot (GEAR RARE)
            const float perTier    = 0.00f;   // no threat scaling - stays rare
            const float maxChance  = 0.04f;   // hard cap: gear never exceeds ~4% per-roll (~2%/slot, star bonus clamped away)
            // WO-556 ITEM 4 star bonus is retained for reference but clamped by maxChance above,
            // so extra stars no longer raise the gear-drop odds (gear stays rare regardless of stars).
            float starBonus = GearDropPerStar * Mathf.Max(0, stars - 1);
            float chance = Mathf.Min(maxChance, baseChance + perTier * Mathf.Max(0, threat) + starBonus);
            FlowTrace.Step("BattleArena", $"TryGrantArenaGear: stars={stars} threat={threat} -> dropChance={chance:0.00}.");
            if (UnityEngine.Random.value > chance) return null;

            GameObject heroGo = GameObject.FindWithTag("Player");
            if (heroGo == null) return null;

            var loadout = heroGo.GetComponent<DeNelle.Village.GearLoadout>();
            if (loadout == null) loadout = heroGo.AddComponent<DeNelle.Village.GearLoadout>();
            if (loadout == null) return null;

            var abilities   = heroGo.GetComponent<DeNelle.Village.HeroAbilities>();
            var progression = heroGo.GetComponent<DeNelle.Village.HeroProgression>();
            string job   = abilities != null ? abilities.HeroClass : DeNelle.Village.AbilityCatalog.DefaultClass;
            int    level = progression != null ? progression.Level : 1;

            // Bias low: arena drops stay common/uncommon (the outpost owns the rare/epic curve).
            string targetRarity = UnityEngine.Random.value < 0.65f ? "common" : "uncommon";

            // 50/50 weapon vs armor; fall back to the other type if the first yields none.
            if (UnityEngine.Random.value < 0.5f)
            {
                var w = PickArenaWeapon(job, level, targetRarity);
                if (w != null) { loadout.EquipWeaponById(w.id); return w.name; }
                var a = PickArenaArmor(job, level, targetRarity);
                if (a != null) { loadout.EquipArmorById(a.id); return a.name; }
            }
            else
            {
                var a = PickArenaArmor(job, level, targetRarity);
                if (a != null) { loadout.EquipArmorById(a.id); return a.name; }
                var w = PickArenaWeapon(job, level, targetRarity);
                if (w != null) { loadout.EquipWeaponById(w.id); return w.name; }
            }
            return null;
        }

        // Pick the eligible weapon at the target rarity the hero qualifies for; else the
        // best weapon for the hero's job/level (GearCatalog fallback). Null if none.
        //
        // ELIGIBILITY IS ASKED OF GearCatalog, NOT RE-IMPLEMENTED HERE (WO loot-class-gate,
        // 2026-08-02). This used to inline the job/level test; the ARMOR half inlined a
        // level-only test and so awarded gear the class cannot wear. Both halves now call
        // the ONE authority (GearCatalog.CanEquipWeapon / CanEquipArmor), which is the same
        // question BestWeapon/BestArmor answer - so the exact-rarity pick and the fallback
        // can no longer disagree about what the hero may hold.
        private static DeNelle.Village.WeaponDef PickArenaWeapon(string job, int level, string rarity)
        {
            DeNelle.Village.WeaponDef exact = null;
            foreach (var w in DeNelle.Village.GearCatalog.AllWeapons())
            {
                if (w == null) continue;
                if (!DeNelle.Village.GearCatalog.CanEquipWeapon(w, job, level, out _)) continue;
                if (string.Equals(w.rarity, rarity, StringComparison.OrdinalIgnoreCase))
                {
                    if (exact == null || w.damageMult > exact.damageMult) exact = w;
                }
            }
            return exact ?? DeNelle.Village.GearCatalog.BestWeapon(job, level);
        }

        // Armor is gated on CLASS exactly like the weapon half above. Two gates matter and
        // both live in CanEquipArmor: the legacy `job` field AND the light/heavy WEIGHT class
        // (Ranger/Mage = light, Knight/Cleric = heavy). Without them a Mage was handed heavy
        // plate, which GearLoadout.Refresh then silently DROPS (ArmorFitsClass, GearLoadout.cs
        // :352) - the player saw a reward line and then an empty armor slot. The fallback also
        // passes the REAL job; it used to hardcode "any", which is not a class and made
        // BestArmor's own ArmorFitsClass gate resolve against ClassWeight("any") == "heavy".
        private static DeNelle.Village.ArmorDef PickArenaArmor(string job, int level, string rarity)
        {
            DeNelle.Village.ArmorDef exact = null;
            foreach (var a in DeNelle.Village.GearCatalog.AllArmors())
            {
                if (a == null) continue;
                if (!DeNelle.Village.GearCatalog.CanEquipArmor(a, job, level, out _)) continue;
                if (string.Equals(a.rarity, rarity, StringComparison.OrdinalIgnoreCase))
                {
                    if (exact == null || a.defense > exact.defense) exact = a;
                }
            }
            return exact ?? DeNelle.Village.GearCatalog.BestArmor(job, level);
        }
    }
}
