// =============================================================================
// EnemyOutpost - a real, walk-to ENEMY OUTPOST in the open world (RAID bite of the
// outpost -> raid -> loot elephant). Clear it by killing the garrison.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World.Camps
//
// THE LOOP (this slice): walk to the outpost -> the hero + party AUTO-FIGHT the
// guards (real Enemy via TargetManager - ZERO new combat code) -> kill the whole
// garrison (1 boss + ~5-6 guards) -> the outpost is CLEARED, fires OnCleared, and
// pays a FLAT reward (XP + a little crystals). Loot DROPS are the NEXT bite.
//
// REUSE (no reinvented wheels - mirrors the proven CampGuards pattern):
//   * OutpostFoundationGenerator  - the WOOD catalog-piece fortification visual
//     (GenerateFootprintRecipe + Realize), the SAME StructureFactory.Create path
//     the village build mode uses. LOCAL cell math; no village-grid involvement.
//   * EnemyFactory.Build()        - the ONE enemy creation path (CLAUDE.md §9).
//   * Enemy / EnemyBrain          - the boss gets an EnemyBrain in the MiniBoss
//     role (tougher stat block); guards are plain charger/walker Enemy.
//   * Enemy.SetBrainTarget(anchor) - tethers each garrison member to the outpost so
//     they HOLD the outpost instead of marching the Heart of Elarion. The hero's
//     own aggro + TargetManager still pull them into the fight when she arrives.
//   * Enemy.Died                  - subscribe-only kill counting; AllDead -> Clear.
//   * ZoneManager                 - region/threat scaling (deeper = deadlier).
//
// PERSISTENCE: PlayerPrefs only (mirrors ClaimableCamp) - a cleared raid stays
// cleared on reload; the save SCHEMA is untouched (save-owner follow-up).
//
// ISOLATION: created/owned entirely by RaidOutpostSystem at runtime. Touches NO
// existing file. References only PUBLIC read-only APIs. Code-built; LogWarning,
// never error, if optional art/registry pieces are missing (pack-missing-safe).
// Canon: the village is Elarion (never Avalon). ASCII-only runtime strings.
// =============================================================================
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.AI;
using DeNelle.Core.World;
using DeNelle.Core.State;
using DeNelle.Core.Quests;
using DeNelle.Core.Diagnostics;   // FlowTrace — WO-449 garrison-live verification marker
using DeNelle.Core.Catalog;     // CatalogRegistry — Arena defender STRUCTURES (WO-389)
using DeNelle.Village.Arena;    // ArenaDefenseCatalog / ArenaDefenseDef (WO-389)

namespace DeNelle.Village.World.Camps
{
    /// <summary>A walk-to enemy outpost: a WOOD fort held by a boss-led garrison.
    /// Clear the whole garrison (hero + party auto-fight) to CLEAR it and collect a
    /// flat reward. Raises <see cref="OnCleared"/> once the last defender dies.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyOutpost : MonoBehaviour
    {
        // -- Garrison sizing --------------------------------------------------
        /// <summary>Base guard count (excludes the boss). Scaled up a touch by threat.</summary>
        public const int BaseGuardCount = 5;

        /// <summary>Radius the garrison stand-ring is laid out across, around the centre.</summary>
        public const float GarrisonRing = 6f;

        // -- Fortification footprint (LOCAL grid cells; cellSize = 3 m) --------
        private const int FortGridWidth = 6;
        private const int FortGridDepth = 6;

        // -- Clear reward base (threat-scaled in GrantClearReward) -------------
        /// <summary>Aether Crystals banked on clear (before the threat-tier bonus).</summary>
        public int BaseClearCrystals { get; private set; } = 40;
        /// <summary>Hero XP granted on clear (before the threat-tier bonus).</summary>
        public int BaseClearXp { get; private set; } = 120;

        // =====================================================================
        // LOOT TABLE (data-tunable) - a THREAT-SCALED roll on clear that routes
        // each drop type to its EXISTING system (no parallel loot-economy):
        //   * Resources (wood/iron)  -> EconomyService.Grant
        //   * Crystals / rare gems   -> GameState.AetherCrystals (premium wallet)
        //   * Weapon / armor          -> GearLoadout.EquipWeaponById/EquipArmorById
        //   * Raid quest progress     -> DailyQuestService.Report (clean hook)
        // Rarity/quantity rise with the outpost's ZoneManager threat tier, so a
        // deadlier outpost pays better/rarer loot. All knobs are const here.
        // =====================================================================

        // ALWAYS: base resources, scaled per threat tier.
        private const int BaseLootWood       = 40;   // + WoodPerThreat * threat
        private const int WoodPerThreat      = 12;
        private const int BaseLootIron       = 20;   // + IronPerThreat * threat
        private const int IronPerThreat      = 8;

        // CHANCE: a GEAR drop. Probability rises with threat (clamped). On a hit,
        // the rolled rarity is biased UP by threat (see RollGearRarity).
        private const float GearDropChanceBase    = 0.35f;  // at threat 0
        private const float GearDropChancePerTier = 0.06f;  // + per threat tier
        private const float GearDropChanceMax     = 0.85f;

        // RARER (high threat): a rare-gem bonus crystal payout on top of the base.
        private const int   RareGemThreatGate   = 4;     // only at threat >= this
        private const float RareGemChance       = 0.30f;
        private const int   RareGemCrystals     = 75;    // + RareGemPerThreat * threat
        private const int   RareGemPerThreat    = 15;

        // RARER (high threat): a QUEST ITEM token. No clean "grant quest item to
        // inventory" hook exists (DailyQuests rewards on completion, not the
        // reverse) - so we tick raid-clear quest PROGRESS (the clean hook) always,
        // and only deposit a tangible token when the item-drops larder lane is on.
        private const int   QuestItemThreatGate = 6;
        private const float QuestItemChance     = 0.25f;
        private const string QuestRaidEventId   = "combat.raid";   // DailyQuests Report() id
        private const string QuestItemMaterial  = "warlord-seal";  // token into the larder (lane-gated)

        // -- Persistence (PlayerPrefs only - schema untouched; mirror ClaimableCamp) --
        private const string PrefClearedKey = "dotr-raid-cleared-";   // +OutpostId -> "1"

        /// <summary>Raised once the entire garrison is dead (the outpost is cleared).</summary>
        public event Action<EnemyOutpost> OnCleared;

        /// <summary>
        /// ARENA only: raised when the garrison FAILED to spawn (0 alive after
        /// SpawnGarrison) on a raid whose clear-reward is suppressed (the Arena path).
        /// In the open world an empty garrison auto-Clear()s as an anti-deadlock; in the
        /// Arena that would mis-fire as an instant WIN + purse, so the Arena listens to
        /// this instead and ends the raid as a NON-win (no purse). Never raised on the
        /// open-world path.
        /// </summary>
        public event Action<EnemyOutpost> OnArenaSpawnFailed;

        // -- Config -----------------------------------------------------------
        public RegionId Region { get; private set; } = RegionId.Goldfields;
        public int ThreatLevel { get; private set; }
        /// <summary>Stable id (region-based) used as the PlayerPrefs persistence key.</summary>
        public string OutpostId { get; private set; }

        // -- Runtime state ----------------------------------------------------
        /// <summary>True once the whole garrison is dead (or it was restored cleared).</summary>
        public bool Cleared { get; private set; }
        /// <summary>Living garrison members remaining (0 once cleared).</summary>
        public int AliveCount => _aliveCount;
        /// <summary>Total garrison members this outpost started with (boss + guards).</summary>
        public int TotalGarrison => _garrison.Count;

        private Transform _garrisonRoot;
        private readonly List<Enemy> _garrison = new List<Enemy>();
        private int _aliveCount;
        private bool _spawned;
        private bool _rewardPaid;

        // -- ARENA override (async-PvP) ---------------------------------------
        // The Arena raid (DeNelle.Village.Arena) reuses this whole component but
        // points it at a SEEDED opponent's base recipe + a fixed garrison size and
        // suppresses the open-world clear-loot (the Arena pays the SKR purse itself).
        // All null/zero = the original open-world behaviour is untouched.
        private List<PlacedStructureData> _arenaRecipe;   // opponent base, or null = auto-gen fort
        private int _arenaGuardCount = -1;                // fixed guard count, or <0 = threat-scaled
        private bool _suppressClearReward;                // Arena pays its own purse; skip open-world loot
        private bool _everCleared;                        // for ConfigureArena re-clear guard

        /// <summary>Called by RaidOutpostSystem immediately after AddComponent.</summary>
        public void Configure(RegionId region, int threat)
        {
            Region = region;
            ThreatLevel = Mathf.Max(0, threat);
            OutpostId = "raid_" + region;
        }

        /// <summary>
        /// Same as <see cref="Configure(RegionId,int)"/> but with an explicit id suffix so
        /// multiple open-world outposts that happen to share a region (e.g. the 4 cardinal
        /// raid outposts) get DISTINCT persistence keys — clearing one never marks another
        /// cleared. Pass a stable suffix (e.g. "E"/"W"/"N"/"S").
        /// </summary>
        public void Configure(RegionId region, int threat, string idSuffix)
        {
            Region = region;
            ThreatLevel = Mathf.Max(0, threat);
            OutpostId = string.IsNullOrEmpty(idSuffix)
                ? "raid_" + region
                : "raid_" + region + "_" + idSuffix;
        }

        /// <summary>
        /// ARENA-MVP entry: configure this outpost as a SEEDED async-PvP opponent base
        /// instead of an open-world raid. Reuses the ENTIRE spawn/clear/combat path —
        /// only the fortification recipe, garrison size, persistence id and clear-loot
        /// are overridden. Combat is still FULL REUSE (real Enemy via TargetManager);
        /// no new combat code. Must be called BEFORE the component's Start() runs (the
        /// Arena AddComponents this then calls it on the same frame).
        /// </summary>
        /// <param name="outpostId">Unique persistence id for this opponent (e.g. "arena_ironhold").</param>
        /// <param name="threat">Threat tier driving stat scaling (1 / 4 / 8 for the seeded tiers).</param>
        /// <param name="baseRecipe">The opponent's base layout (LOCAL cells). Null = auto-gen wood fort.</param>
        /// <param name="guardCount">Fixed guard count (excludes the boss). &lt;0 = threat-scaled default.</param>
        public void ConfigureArena(string outpostId, int threat,
                                   List<PlacedStructureData> baseRecipe, int guardCount)
        {
            OutpostId = string.IsNullOrEmpty(outpostId) ? ("arena_" + Region) : outpostId;
            ThreatLevel = Mathf.Max(0, threat);
            _arenaRecipe = baseRecipe;
            _arenaGuardCount = guardCount;
            _suppressClearReward = true;   // the Arena credits the SKR purse + records the win itself
        }

        /// <summary>True once this outpost has ever been cleared this session (Arena re-raid guard).</summary>
        public bool EverCleared => _everCleared;

        private void Start()
        {
            // ARENA: an opponent base is ALWAYS spawned fresh for each raid (the Arena
            // owns the lifetime — it Destroys + recreates per raid), so skip the
            // open-world "stay cleared on reload" persistence restore entirely.
            if (_suppressClearReward)
            {
                StartCoroutine(RealizeOutpostRoutine(arena: true));
                return;
            }

            // Restore a previously-cleared raid: stay peaceful, no garrison, no fort
            // re-raise (the fight is over). A fresh raid spawns the fort + garrison.
            if (PlayerPrefs.GetString(PrefClearedKey + OutpostId, null) == "1")
            {
                Cleared = true;
                Debug.Log($"[EnemyOutpost] {OutpostId} restored as already CLEARED - peaceful.");
                return;
            }

            StartCoroutine(RealizeOutpostRoutine(arena: false));
        }

        // =====================================================================
        // STAGGERED REALIZATION - spread the ~20 fort pieces + ~7 garrison spawns
        // across frames so an outpost realized on OuterWorld load (RaidOutpostSystem)
        // no longer freezes a single frame for multiple seconds. WHAT spawns is
        // identical to the old synchronous Start(); only the timing changes.
        // Guards against the outpost being Destroyed mid-build (scene swap / Arena
        // recreate) by bailing the moment `this` is gone.
        // =====================================================================
        private IEnumerator RealizeOutpostRoutine(bool arena)
        {
            // 1) Fort pieces — ~2 per frame (yields internally).
            yield return BuildFortificationStaggered();
            if (this == null) yield break;

            if (arena)
            {
                // WO-389: the defender's PLACED Arena defenders (units + structures)
                // port WITH the base and spawn FRIENDLY to auto-fight the raider. The
                // defense travels with the city — spawn it right after the fortification
                // is realized, before the (Hostile) garrison. No-op if no defense placed.
                SpawnDefenders();
                if (this == null) yield break;
            }

            // 2) Garrison — boss + guards, 1 spawn per frame (yields internally).
            yield return SpawnGarrisonStaggered();
            if (this == null) yield break;

            if (!arena)
            {
                // WO-360: when the player walks into this outpost's combat zone, summon
                // their Echo (pet) to fight alongside them + show a mini-tutorial. The
                // trigger is idempotent + session-guarded (Echo persists once summoned),
                // and is open-world only (the Arena suppresses the open-world beats).
                EchoAutoDeployTrigger.Attach(gameObject, GarrisonRing + 6f);

                // WO-449 — continuous-walk verification marker (no behavior change): the garrison is
                // now LIVE at this anchor; combat begins when the hero walks within ~aggro range. This
                // line lets the headless trace confirm the walk-to outpost actually materialised + is
                // garrisoned (i.e. the hero has something to fight on approach), not just scheduled.
                FlowTrace.Step("Raid", $"{OutpostId} garrison live at {transform.position} — combat begins when the hero approaches (~{GarrisonRing + 6}m).");

                // WO-VFX-POI — far-field ENEMY FORTRESS beacon: a tall looping pillar visible from
                // range (colorblind-safe: verticality/motion/luminance, not hue) that stands until the
                // outpost is cleared. Landmark tier is NOT discovery-gated. Open-world only (this !arena
                // branch — the Arena suppresses the open-world beats), so the beacon never shows in-arena.
                PoiBeacon.Attach(gameObject, PoiBeacon.PoiTier.Landmark,
                    calloutRadius: float.PositiveInfinity, handoffRadius: 35f,
                    tint: new Color(1f, 0.94f, 0.72f, 1f),
                    isSpent: () => Cleared);
            }
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _garrison.Count; i++)
                if (_garrison[i] != null) _garrison[i].Died -= HandleGarrisonDied;
        }

        // =====================================================================
        // FORTIFICATION - the WOOD visual (full reuse of OutpostFoundationGenerator).
        // =====================================================================

        // Staggered fort build — same recipe + same OutpostFoundationGenerator.Realize
        // path as the synchronous version, but spread ~2 pieces per frame so the ~20
        // StructureFactory.Create calls don't all hit one frame.
        private IEnumerator BuildFortificationStaggered()
        {
            // A small ~6x6 wood ring (perimeter walls + corner towers + one gate),
            // generated + realized through the SAME StructureFactory.Create path the
            // village build mode uses. LOCAL cell math against this root only - never
            // the village-scoped PlacementGrid / GameState.BaseLayout singletons.
            // ARENA: realize the SEEDED opponent's base recipe; else auto-gen the
            // open-world wood fort. SAME OutpostFoundationGenerator path, sliced.
            var recipe = _arenaRecipe ?? OutpostFoundationGenerator.GenerateFootprintRecipe(
                FortGridWidth, FortGridDepth, OutpostTier.Wood);
            yield return OutpostFoundationGenerator.RealizeStaggered(recipe, transform, ~0, piecesPerFrame: 2);
        }

        // =====================================================================
        // ARENA DEFENDERS (WO-389) - the player's PRE-PLACED Arena defense, spawned
        // FRIENDLY to auto-fight the raider. This is the connective tissue that makes
        // a placed DefenseSetup actually FIGHT: read GameState.ArenaDefense, resolve
        // each PlacedDefenderData against ArenaDefenseCatalog, and spawn a UNIT (a
        // friendly StoryCompanion body, guard-post tethered) or a STRUCTURE (via
        // StructureFactory) at the placed cell. FULL REUSE - StoryCompanionInjector
        // .SpawnDefender + StructureFactory.Create; NO new spawn/skin/AI/targeting
        // code. Friend/foe is the CombatFaction flag (units target Hostile via
        // TargetManager; DefenseTower structures target Hostile too) - zero new combat.
        //
        // MVP: hardcoded behavior-id -> catalog-entry map; no tuning knobs / no
        // data-driven plumbing yet (a later pass). No-op when no defense is placed,
        // so the seeded open-world raid path is never disturbed.
        // =====================================================================

        // BehaviorId (ArenaDefenseDef) -> structures-catalog entry id. Both the
        // Ballista and (for now) the Healing Shrine resolve to the Ballista/Siege-Tower
        // entry (visual "Structures/Ballista" + the DefenseTower behavior, which already
        // targets CombatFaction.Hostile). There is no heal-aura behavior yet.
        private const string StructEntryBallista = "tower_siege_tower";

        private void SpawnDefenders()
        {
            using var _ = FlowTrace.Enter("Raid", $"{OutpostId} SpawnDefenders (arena)");
            var placed = GameStateService.Instance?.State?.ArenaDefense;
            if (placed == null || placed.Count == 0) return;   // no defense placed -> no-op

            int units = 0, structures = 0;

            for (int i = 0; i < placed.Count; i++)
            {
                var rec = placed[i];
                var def = ArenaDefenseCatalog.Get(rec.itemId);
                if (def == null)
                {
                    // R/U — route through FlowTrace.Warn so a missing catalog id self-reports.
                    FlowTrace.Warn("Raid", $"{OutpostId} Arena defender id '{rec.itemId}' not in catalog - skipped.");
                    continue;
                }

                // LOCAL cell coords -> WORLD via the outpost root's TRS, identical to
                // OutpostFoundationGenerator.Realize so a defender lands on its placed
                // cell of the SAME grid the fortification uses (CellSize = 3 m).
                Vector3 localOffset = new Vector3(rec.cellX * OutpostFoundationGenerator.CellSize,
                                                  0f,
                                                  rec.cellZ * OutpostFoundationGenerator.CellSize);
                Vector3 worldPos = SnapToNav(transform.TransformPoint(localOffset));
                Quaternion worldRot = transform.rotation * Quaternion.Euler(0f, rec.yawSteps * 90f, 0f);

                if (def.Kind == DefenderKind.Unit && def.UnitClass.HasValue)
                {
                    // FRIENDLY unit: a guard-post-tethered StoryCompanion body. It targets
                    // Hostile raiders via TargetManager and is hit back through its hitbox
                    // (IDamageableStructure). Does NOT leash to / chase the attacking hero.
                    var go = StoryCompanionInjector.SpawnDefender(def.UnitClass.Value, worldPos, transform);
                    if (go != null) units++;
                }
                else if (def.Kind == DefenderKind.Structure)
                {
                    // FRIENDLY structure: the DefenseTower behavior already shoots Hostile
                    // targets, so a placed Ballista is a friendly defender with zero new
                    // combat code. TODO: heal-aura behavior - the Healing Shrine falls back
                    // to the Ballista/DefenseTower entry for now (no HealAura behavior yet).
                    var entry = CatalogRegistry.Get(StructEntryBallista);
                    if (entry == null)
                    {
                        Debug.LogWarning($"[EnemyOutpost] structure entry '{StructEntryBallista}' not in registry - '{rec.itemId}' skipped.");
                        continue;
                    }
                    var go = Guard.Try("Raid", $"{OutpostId} create defender structure '{rec.itemId}'",
                        () => StructureFactory.Create(entry, new Pose(worldPos, worldRot), transform), fallback: null);
                    if (go != null) { structures++; VerifyStructureRenders(go, rec.itemId, worldPos); }
                }
            }

            FlowTrace.Step("Raid", $"{OutpostId} spawned {units + structures} defenders ({units} units, {structures} structures).");
        }

        // V — a placed FRIENDLY defender structure must RENDER; otherwise a Ballista that
        // shoots the raider is invisible. Self-reports the renderer counts.
        private static void VerifyStructureRenders(GameObject go, string itemId, Vector3 pos)
        {
            if (go == null) return;
            int total = 0, enabledR = 0, withGeom = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                total++;
                if (r.enabled && r.gameObject.activeInHierarchy) enabledR++;
                if (r.sharedMaterial != null) withGeom++;
            }
            if (enabledR == 0 || withGeom == 0)
                FlowTrace.Fail("Raid",
                    $"INVISIBLE DEFENDER STRUCTURE: '{itemId}' at {pos} spawned but does NOT render (renderers total={total} enabled={enabledR} withGeom={withGeom}).");
        }

        // =====================================================================
        // GARRISON - boss + guards via EnemyFactory, tethered to the outpost.
        // Combat itself is FULL REUSE: these are real Enemy; the hero + party
        // auto-fight them via TargetManager. We write NO combat/targeting code.
        // =====================================================================

        // Staggered garrison spawn — boss this frame, then 1 guard per frame, so the
        // ~7 EnemyFactory.Build calls don't all hit one frame. The empty-garrison
        // handling at the end is IDENTICAL to the synchronous version.
        private IEnumerator SpawnGarrisonStaggered()
        {
            if (_spawned) yield break;
            _spawned = true;

            _garrisonRoot = new GameObject("[Garrison]").transform;
            _garrisonRoot.SetParent(transform, false);
            _garrisonRoot.localPosition = Vector3.zero;

            // The BOSS holds the centre of the outpost (MiniBoss role).
            SpawnBoss();
            yield return null;

            // The guard ring (charger/walker Enemy). ARENA = a fixed seeded count;
            // open-world = threat-scaled 5..8. One guard per frame.
            int guards = _arenaGuardCount >= 0
                ? _arenaGuardCount
                : BaseGuardCount + Mathf.Clamp(ThreatLevel / 2, 0, 3);
            for (int i = 0; i < guards; i++)
            {
                if (this == null || _garrisonRoot == null) yield break;
                SpawnGuard(i, guards);
                yield return null;
            }

            if (_aliveCount == 0)
            {
                // Nothing could spawn (no NavMesh / no roster).
                if (_suppressClearReward)
                {
                    // ARENA: do NOT Clear() — that fires OnCleared -> instant WIN + purse
                    // even though the player never fought a thing (the empty-Arena bug).
                    // Treat it as a FAILED spawn: log an error and signal the Arena to end
                    // the raid as a NON-win (no purse). The Arena owns this outpost's
                    // lifetime + result.
                    FlowTrace.Fail("Raid", $"{OutpostId} ARENA garrison FAILED to spawn " +
                                   "(no NavMesh under the raid anchor?) - aborting raid as a NON-win, NOT an auto-win.");
                    OnArenaSpawnFailed?.Invoke(this);
                    yield break;
                }

                // OPEN WORLD: treat as cleared so the raid loop never deadlocks waiting on
                // defenders that never existed (legit anti-deadlock — keep unchanged).
                FlowTrace.Warn("Raid", $"{OutpostId} no garrison spawned for {Region} outpost - auto-clearing (anti-deadlock).");
                Clear();
            }
        }

        private void SpawnBoss()
        {
            Vector3 want = transform.position;
            Vector3 pos = SnapToNav(want);

            // T/R — the boss anchors the outpost. If SnapToNav found no navmesh (pos == want
            // unchanged AND no mesh under it) the boss may stand OFF-mesh and never path/aggro.
            // Warn loudly so an off-mesh spawn never silently makes the outpost un-clearable.
            VerifyOnNavMesh("boss", want, pos);

            var def = BuildBossDef(ThreatLevel);

            // G — EnemyFactory.Build can throw (pack/model fault); guard it so a boss fault
            // logs + skips instead of aborting the whole garrison realize coroutine.
            Enemy boss = Guard.Try("Raid", $"{OutpostId} build boss",
                () => EnemyFactory.Build(def, pos, Quaternion.identity, _garrisonRoot), fallback: null);
            if (boss == null)
            {
                // R/U — was a SILENT null-return; now self-reports. A bossless outpost still
                // clears on its guards, but a never-spawned boss should never be invisible.
                FlowTrace.Fail("Raid", $"{OutpostId} SpawnBoss: EnemyFactory returned null at {pos} — outpost has NO boss.");
                return;
            }
            boss.gameObject.name = $"OutpostBoss ({Region})";

            var anchor = MakeAnchor("BossAnchor", pos);
            boss.Configure($"raidboss-{Region}", def, anchor);
            boss.SetBrainTarget(anchor);

            // The boss gets a brain in the MiniBoss role (tougher, holds the outpost).
            var brain = boss.gameObject.GetComponent<EnemyBrain>();
            if (brain == null) brain = boss.gameObject.AddComponent<EnemyBrain>();
            brain.Role = EnemyRole.MiniBoss;

            // V — the boss must RENDER + be hittable, else the hero faces an invisible defender.
            VerifyEnemyRenders(boss, "boss", pos);

            Track(boss);
        }

        private void SpawnGuard(int index, int count)
        {
            // A ring of stand positions around the outpost centre, NavMesh-snapped.
            float ang = (count > 0 ? (index / (float)count) : 0f) * Mathf.PI * 2f;
            Vector3 want = transform.position +
                           new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (GarrisonRing * 0.7f);
            Vector3 pos = SnapToNav(want);
            VerifyOnNavMesh($"guard-{index}", want, pos);

            float depth = ZoneManager.Depth(transform.position);
            string enemyId = RegionSpawnTable.HasRoster(Region)
                ? RegionSpawnTable.PickEnemyId(Region, depth, UnityEngine.Random.value)
                : "orc-raider";
            if (string.IsNullOrEmpty(enemyId)) enemyId = "orc-raider";

            var def = BuildGuardDef(enemyId, ThreatLevel);

            Enemy guard = Guard.Try("Raid", $"{OutpostId} build guard '{enemyId}'",
                () => EnemyFactory.Build(def, pos, Quaternion.identity, _garrisonRoot), fallback: null);
            if (guard == null)
            {
                // R/U — was a SILENT null-return. A missing guard shrinks the garrison; report it.
                FlowTrace.Fail("Raid", $"{OutpostId} SpawnGuard[{index}]: EnemyFactory returned null for '{enemyId}' at {pos} — guard NOT spawned.");
                return;
            }
            guard.gameObject.name = $"OutpostGuard ({enemyId} - {Region})";

            var anchor = MakeAnchor($"GuardAnchor-{index}", pos);
            guard.Configure($"raidguard-{Region}-{index}", def, anchor);
            guard.SetBrainTarget(anchor);

            // V — the guard must RENDER, else the hero fights an invisible enemy.
            VerifyEnemyRenders(guard, $"guard-{index} ({enemyId})", pos);

            Track(guard);
        }

        // V — verify a spawned enemy actually RENDERS (>=1 enabled renderer carrying geometry),
        // so an invisible garrison member self-reports rather than leaving the hero swinging at
        // nothing. Traces the renderer counts for a capture-driven split (no renderer / no mesh).
        private static void VerifyEnemyRenders(Enemy e, string label, Vector3 pos)
        {
            if (e == null) return;
            int total = 0, enabledR = 0, withGeom = 0;
            foreach (var r in e.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                total++;
                if (r.enabled && r.gameObject.activeInHierarchy) enabledR++;
                if (r.sharedMaterial != null) withGeom++;
            }
            if (enabledR == 0 || withGeom == 0)
                FlowTrace.Fail("Raid",
                    $"INVISIBLE ENEMY: outpost {label} at {pos} spawned but does NOT render (renderers total={total} enabled={enabledR} withGeom={withGeom}).");
            else
                FlowTrace.Step("Raid", $"Outpost {label} at {pos} renders (renderers={total} enabled={enabledR}).");
        }

        // R — verify the snap landed ON the navmesh. SnapToNav returns the WANT position
        // unchanged when no navmesh is within range, so a defender can spawn off-mesh and never
        // path/aggro — an effectively-broken garrison. Warn loudly when the snap was a no-op.
        private static void VerifyOnNavMesh(string label, Vector3 want, Vector3 snapped)
        {
            if (NavMesh.SamplePosition(snapped, out _, 1.0f, NavMesh.AllAreas)) return;
            FlowTrace.Warn("Raid",
                $"OFF-MESH SPAWN: outpost {label} snapped to {snapped} (wanted {want}) but no navmesh within 1m — defender may never path/aggro.");
        }

        private void Track(Enemy e)
        {
            e.Died += HandleGarrisonDied;
            _garrison.Add(e);
            _aliveCount++;
        }

        private Transform MakeAnchor(string name, Vector3 pos)
        {
            // A local tether anchor so the defender HOLDS the outpost rather than
            // marching the Heart. Enemy.SetBrainTarget(anchor) overrides the Heart-
            // march; Enemy's own hero-aggro still pulls it into the fight on approach.
            var go = new GameObject(name);
            go.transform.SetParent(_garrisonRoot, false);
            go.transform.position = pos;
            return go.transform;
        }

        private static Vector3 SnapToNav(Vector3 want)
        {
            if (NavMesh.SamplePosition(want, out NavMeshHit hit, GarrisonRing + 6f, NavMesh.AllAreas))
                return hit.position;
            return want;
        }

        // =====================================================================
        // CLEAR - the last defender dies -> mark CLEARED, pay reward, persist.
        // =====================================================================

        private void HandleGarrisonDied(Enemy enemy)
        {
            if (enemy != null) enemy.Died -= HandleGarrisonDied;
            _aliveCount = Mathf.Max(0, _aliveCount - 1);
            if (_aliveCount == 0)
                Clear();
        }

        /// <summary>Mark the outpost cleared, pay the flat reward, and persist. Idempotent.</summary>
        public void Clear()
        {
            if (Cleared) return;
            Cleared = true;
            _everCleared = true;

            // ARENA: the Arena pays the SKR purse + GrantClearReward loot + records the
            // win itself (off OnCleared), and owns this outpost's lifetime, so we skip
            // the open-world flat-reward + the "stay cleared on reload" persistence.
            if (!_suppressClearReward)
            {
                GrantClearReward();
                PlayerPrefs.SetString(PrefClearedKey + OutpostId, "1");
                PlayerPrefs.Save();
            }

            Debug.Log($"[EnemyOutpost] {OutpostId} CLEARED - garrison wiped.");
            OnCleared?.Invoke(this);
        }

        /// <summary>
        /// ARENA hook: run the standard threat-scaled clear-loot roll on demand (the
        /// open-world clear suppresses it for an Arena outpost, then the Arena calls
        /// this on a WIN so the victor still gets the gear/resource drop ON TOP of the
        /// SKR purse). Idempotent (paid at most once). Full reuse of the loot table —
        /// no parallel loot economy.
        /// </summary>
        public void GrantArenaLoot() => GrantClearReward();

        // THREAT-SCALED LOOT TABLE: rolled once on clear, every drop routed to an
        // EXISTING system (no parallel loot-economy). Always pays resources +
        // crystals + XP; a chance (rising with threat) adds a GEAR drop; high-threat
        // outposts can also drop a rare gem (bonus crystals) and a quest token.
        // Idempotent - paid at most once per outpost.
        private void GrantClearReward()
        {
            if (_rewardPaid) return;
            _rewardPaid = true;

            var summary = new StringBuilder();

            // --- ALWAYS: resources -> EconomyService.Grant -------------------
            int wood = BaseLootWood + WoodPerThreat * ThreatLevel;
            int iron = BaseLootIron + IronPerThreat * ThreatLevel;
            if (EconomyService.Instance != null)
            {
                EconomyService.Instance.Grant(wood: wood, iron: iron);
                summary.Append($"{wood} wood, {iron} iron");
            }
            else
            {
                Debug.LogWarning("[EnemyOutpost] EconomyService null - resources not granted.");
            }

            // --- ALWAYS: crystals -> GameState.AetherCrystals (premium wallet) -
            int crystals = BaseClearCrystals + ThreatLevel * 10;

            // --- RARER (high threat): a rare GEM = bonus crystals ------------
            string gemNote = null;
            if (ThreatLevel >= RareGemThreatGate && UnityEngine.Random.value < RareGemChance)
            {
                int gem = RareGemCrystals + RareGemPerThreat * ThreatLevel;
                crystals += gem;
                gemNote = $"rare gem (+{gem} crystals)";
            }

            var state = GameStateService.Instance?.State;
            if (state != null)
            {
                GameStateService.Instance.AddCrystals(crystals);   // unified onto Resources.Crystals; persists + raises ResourcesChanged
                summary.Append(summary.Length > 0 ? ", " : "").Append($"{crystals} crystals");
            }
            else
            {
                Debug.LogWarning("[EnemyOutpost] GameState null - clear crystals not banked.");
            }
            if (gemNote != null) summary.Append(", ").Append(gemNote);

            // --- ALWAYS: hero XP -> HeroProgression -------------------------
            int xp = BaseClearXp + ThreatLevel * 25;
            HeroProgression.Instance?.AddXp(xp);
            summary.Append(summary.Length > 0 ? ", " : "").Append($"{xp} XP");

            // --- CHANCE (rises with threat): a GEAR drop -> GearLoadout ------
            string gear = TryGrantGearDrop();
            if (gear != null) summary.Append(", [").Append(gear).Append("]");

            // --- RARER (high threat): a QUEST ITEM token --------------------
            // Clean hook = tick raid-clear quest PROGRESS (always). A literal
            // "grant quest item into inventory" has NO clean API (FLAGGED): the
            // closest tangible store is the item-drops larder, which only accepts
            // deposits when that lane is enabled - so the token is best-effort.
            DailyQuestService.Instance?.Report(QuestRaidEventId, 1);
            if (ThreatLevel >= QuestItemThreatGate && UnityEngine.Random.value < QuestItemChance)
            {
                DeNelle.Village.Items.ItemInventory.GrantDrop(QuestItemMaterial, 1); // no-op unless ItemDropSystem lane is on
                summary.Append(", [Warlord's Seal (quest)]");
            }

            Debug.Log($"[EnemyOutpost] Raid cleared ({OutpostId}, threat {ThreatLevel}) - looted: {summary}.");
        }

        // Roll the gear-drop chance (rising with threat); on a hit, pick a catalog
        // weapon OR armor at a threat-biased rarity that the hero qualifies for, and
        // grant it via the REAL armory API (GearLoadout.EquipWeaponById/EquipArmorById).
        // Returns the granted item's display name, or null if nothing dropped.
        private string TryGrantGearDrop()
        {
            float chance = Mathf.Min(GearDropChanceMax,
                GearDropChanceBase + GearDropChancePerTier * Mathf.Max(0, ThreatLevel));
            if (UnityEngine.Random.value > chance) return null;

            var hero = FindHeroLoadout();
            if (hero == null)
            {
                Debug.LogWarning("[EnemyOutpost] gear dropped but no hero GearLoadout - skipped.");
                return null;
            }

            string job   = hero.HeroClass;
            int    level = hero.HeroLevel;
            string targetRarity = RollGearRarity(ThreatLevel);

            // 50/50 weapon vs armor; fall back to the other type if the first yields none.
            bool wantWeapon = UnityEngine.Random.value < 0.5f;

            if (wantWeapon)
            {
                var w = PickWeapon(job, level, targetRarity);
                if (w != null) { hero.EquipWeaponById(w.id); return w.name; }
                var a = PickArmor(level, targetRarity);
                if (a != null) { hero.EquipArmorById(a.id); return a.name; }
            }
            else
            {
                var a = PickArmor(level, targetRarity);
                if (a != null) { hero.EquipArmorById(a.id); return a.name; }
                var w = PickWeapon(job, level, targetRarity);
                if (w != null) { hero.EquipWeaponById(w.id); return w.name; }
            }
            return null;
        }

        // Bias rarity UP with threat: low threat = mostly common/uncommon, high
        // threat = a real shot at rare/epic. Pure weighted roll over the catalog
        // rarity tiers; the actual eligible item is gated by the hero's job+level.
        private static string RollGearRarity(int threat)
        {
            // Weights shift toward higher tiers as threat rises.
            float t = Mathf.Clamp01(Mathf.Max(0, threat) / 10f);
            float wCommon   = Mathf.Lerp(0.55f, 0.10f, t);
            float wUncommon = 0.30f;
            float wRare     = Mathf.Lerp(0.12f, 0.35f, t);
            float wEpic     = Mathf.Lerp(0.03f, 0.25f, t);

            float total = wCommon + wUncommon + wRare + wEpic;
            float pick  = UnityEngine.Random.value * total;
            if ((pick -= wCommon)   <= 0f) return "common";
            if ((pick -= wUncommon) <= 0f) return "uncommon";
            if ((pick -= wRare)     <= 0f) return "rare";
            return "epic";
        }

        // Pick the eligible weapon nearest the target rarity (prefer exact rarity;
        // else the best the hero qualifies for). Returns null if the catalog is empty.
        private static WeaponDef PickWeapon(string job, int level, string rarity)
        {
            WeaponDef exact = null;
            foreach (var w in GearCatalog.AllWeapons())
            {
                if (w == null) continue;
                if (!JobOk(w.job, job)) continue;
                if (w.req != null && level < w.req.level) continue;
                if (string.Equals(w.rarity, rarity, StringComparison.OrdinalIgnoreCase))
                {
                    if (exact == null || w.damageMult > exact.damageMult) exact = w;
                }
            }
            if (exact != null) return exact;
            return GearCatalog.BestWeapon(job, level); // fallback: best the hero qualifies for
        }

        private static ArmorDef PickArmor(int level, string rarity)
        {
            ArmorDef exact = null;
            foreach (var a in GearCatalog.AllArmors())
            {
                if (a == null) continue;
                if (a.req != null && level < a.req.level) continue;
                if (string.Equals(a.rarity, rarity, StringComparison.OrdinalIgnoreCase))
                {
                    if (exact == null || a.defense > exact.defense) exact = a;
                }
            }
            if (exact != null) return exact;
            return GearCatalog.BestArmor("any", level); // fallback (armor jobs are "any")
        }

        private static bool JobOk(string itemJob, string heroJob)
        {
            if (string.IsNullOrEmpty(itemJob)) return true;
            if (itemJob.Equals("any", StringComparison.OrdinalIgnoreCase)) return true;
            return itemJob.Equals(heroJob ?? string.Empty, StringComparison.OrdinalIgnoreCase);
        }

        // Locate the active hero's GearLoadout (the real armory grant surface),
        // mirroring ShopPanel.FindActiveHeroGO: Player tag -> add/get GearLoadout.
        private static HeroGearRef FindHeroLoadout()
        {
            GameObject heroGo = GameObject.FindWithTag("Player");
            if (heroGo == null) return null;

            var loadout = heroGo.GetComponent<GearLoadout>();
            if (loadout == null) loadout = heroGo.AddComponent<GearLoadout>();

            var abilities    = heroGo.GetComponent<HeroAbilities>();
            var progression  = heroGo.GetComponent<HeroProgression>();
            return new HeroGearRef(loadout, abilities, progression);
        }

        // Small bundle so the loot roll can read job/level + grant gear through ONE
        // handle (the real GearLoadout equip API), without re-finding the hero.
        private sealed class HeroGearRef
        {
            private readonly GearLoadout _loadout;
            public string HeroClass { get; }
            public int    HeroLevel { get; }

            public HeroGearRef(GearLoadout loadout, HeroAbilities abilities, HeroProgression progression)
            {
                _loadout  = loadout;
                HeroClass = abilities != null ? abilities.HeroClass : AbilityCatalog.DefaultClass;
                HeroLevel = progression != null ? progression.Level : 1;
            }

            public void EquipWeaponById(string id) => _loadout?.EquipWeaponById(id);
            public void EquipArmorById(string id)  => _loadout?.EquipArmorById(id);
        }

        // =====================================================================
        // Stat blocks (code-built EnemyDef, threat-scaled). The boss is a tougher
        // tank/miniboss; the guards mirror CampGuards' synthesised roster so they
        // read the same as the rest of the open-world enemies.
        // =====================================================================

        private static EnemyDef BuildBossDef(int threat)
        {
            float scale = 1f + 0.12f * Mathf.Max(0, threat);
            var def = new EnemyDef
            {
                Id = "orc-warlord",
                Name = "Outpost Warlord",
                DisplayName = "Outpost Warlord",
                Ai = "charger",
                Hp = 420f * scale,
                MoveSpeed = 2.6f,
                ContactDamage = 22f * scale,
                AttackInterval = 1.4f,
                Height = 2.6f,
                AggroRadius = 16f,
                XpReward = 80 + threat * 5,
                GlimmerReward = 12,
            };
            return ApplyEarlyEase(def);
        }

        private static EnemyDef BuildGuardDef(string enemyId, int threat)
        {
            float scale = 1f + 0.10f * Mathf.Max(0, threat);

            string id = string.IsNullOrEmpty(enemyId) ? "orc-raider" : enemyId;
            string name; string ai; float hp; float spd; float dmg; float interval; float height; int xp;
            switch (id)
            {
                case "orc-raider":
                    name = "Outpost Raider";  ai = "charger";    hp = 95f;  spd = 3.1f; dmg = 12f; interval = 1.3f; height = 2.0f; xp = 22; break;
                case "caveman":
                    name = "Outpost Brute";   ai = "walker";     hp = 70f;  spd = 2.7f; dmg = 9f;  interval = 1.4f; height = 1.9f; xp = 16; break;
                case "feral-wolf":
                    name = "Outpost Hound";   ai = "skirmisher"; hp = 42f;  spd = 4.2f; dmg = 7f;  interval = 1.0f; height = 1.2f; xp = 12; break;
                case "tiefling-cultist":
                    name = "Outpost Cultist"; ai = "skirmisher"; hp = 80f;  spd = 3.4f; dmg = 11f; interval = 1.2f; height = 1.9f; xp = 20; break;
                case "necromancer":
                    name = "Outpost Warden";  ai = "walker";     hp = 140f; spd = 2.2f; dmg = 15f; interval = 1.4f; height = 2.1f; xp = 34; break;
                default:
                    name = "Outpost Guard";   ai = "walker";     hp = 60f;  spd = 3.0f; dmg = 8f;  interval = 1.3f; height = 1.8f; xp = 15; break;
            }

            var def = new EnemyDef
            {
                Id = id,
                Name = name,
                DisplayName = name,
                Ai = ai,
                Hp = hp * scale,
                MoveSpeed = spd,
                ContactDamage = dmg * scale,
                AttackInterval = interval,
                Height = height,
                AggroRadius = 14f,
                XpReward = xp + threat,
                GlimmerReward = 3,
            };
            return ApplyEarlyEase(def);
        }

        // Early-game ease (same ramp as CampGuards / RegionMobSpawner): a brand-new
        // player meets soft defenders (x0.35 HP/damage) that ramp to full by ~BestWave 6,
        // so the FIRST raid is a winnable power-fantasy and scales with progression.
        private static EnemyDef ApplyEarlyEase(EnemyDef def)
        {
            float ease = Mathf.Lerp(0.35f, 1f,
                Mathf.Clamp01((GameStateService.Instance?.State?.BestWave ?? 0) / 6f));
            def.Hp = Mathf.Max(1f, def.Hp * ease);
            def.ContactDamage = Mathf.Max(0f, def.ContactDamage * ease);
            return def;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Cleared
                ? new Color(0.2f, 0.9f, 0.4f, 0.35f)
                : new Color(0.9f, 0.2f, 0.2f, 0.35f);
            Gizmos.DrawWireSphere(transform.position, GarrisonRing);
        }
#endif
    }
}
