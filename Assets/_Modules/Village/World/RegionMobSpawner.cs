// =============================================================================
// RegionMobSpawner (WO-155) — roaming region mobs, region-appropriate + threat-scaled.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The THREAT half of the open-world explore loop (the reward half is MineNode /
// CrystalMineNode). As the player roams the outer world, this maintains a small
// live population of roaming mobs AROUND them, each:
//   • REGION-APPROPRIATE  — picked from RegionSpawnTable by ZoneManager.GetZone +
//     depth band (Wildlands living in Goldfields/Stoneback; Wound-tied in Mirewood/
//     Ashwood) — the owner's REGION_ENEMY_ROSTER.md assignment, as data.
//   • THREAT-SCALED       — stats + level set from ZoneManager.ThreatLevel (danger
//     tier × depth). Deeper in-region = tougher; Ashwood core is the deadliest.
//   • RED-SKULL TELLED     — a ThreatSkullPlate nameplate shows a Fallout-style skull
//     when the mob's ThreatLevel out-paces the player's level (risky / lethal).
//
// RECONCILIATION (no parallel spawn/enemy system — CLAUDE.md §9):
//   • Reuses Enemy + Enemy.Configure (the SAME path as TribeManager / WaveManager /
//     EnemyFamilyTestSpawner) — code-built capsules, NavMesh-seated, no prefab/SO.
//   • Reuses ZoneManager (region classifier) + RegionSpawnTable (roster) + ThreatLevel
//     (the single difficulty read) — does NOT re-implement any of them.
//   • Self-bootstrapping DDOL (mirrors TribeManager / EnemyFamilyTestSpawner) so it
//     needs NO scene edit and fires NO bake. Works the moment OuterWorld is loaded
//     additively over Village (WorldSceneLoader) and a baked NavMesh exists.
//
// ROAMING vs RAIDING: TribeManager (WO-160) anchors raider BANDS to a camp that
// raze SETTLEMENTS. This spawner is the ambient wilderness population that wanders
// near the PLAYER and aggros them — a different, complementary layer. They never
// march the Heart (the roam anchor overrides it) so they don't trickle into town.
//
// HOLLOW ONES are NOT spawned here (RegionSpawnTable omits them) — they stay the
// village wave faction per the roster doc, pending owner confirm on the haunted seam.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using DeNelle.Core.World;
using DeNelle.Core.State;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Maintains a small live population of region-appropriate, threat-scaled roaming
    /// mobs around the player in the outer world. One self-bootstrapping DDOL singleton;
    /// no prefab / SO / scene wiring, no bake.
    /// </summary>
    public sealed class RegionMobSpawner : MonoBehaviour
    {
        public static RegionMobSpawner Instance { get; private set; }

        [Header("Population")]
        [Tooltip("CAP on roaming mobs kept alive around the player at HIGH progress. The " +
                 "EFFECTIVE target ramps up to this from a gentle early floor (see EarlyTargetFloor) " +
                 "as the player progresses (BestWave) — a new player meets 1-2 wanderers, not a pack.")]
        [Min(0)] public int TargetPopulation = 8;

        [Tooltip("EFFECTIVE roaming-mob target for a brand-new player (BestWave 0). Ramps toward " +
                 "TargetPopulation as progress climbs (WO-216: wandering primary, gentle onboarding).")]
        [Min(0)] public int EarlyTargetFloor = 2;

        [Tooltip("How many cleared waves it takes to add one more roamer to the effective target " +
                 "(WO-216 ramp slope). Smaller = the wilderness fills in faster as you progress.")]
        [Min(1)] public int WavesPerExtraMob = 2;

        [Tooltip("Seconds between population/aggro maintenance passes (throttled — never per-frame heavy).")]
        [Min(0.1f)] public float TickInterval = 1.0f;

        [Header("Ranges (world units)")]
        [Tooltip("Inner radius of the spawn ring around the player (don't pop in their face).")]
        [Min(2f)] public float SpawnRingInner = 22f;
        [Tooltip("Outer radius of the spawn ring around the player.")]
        [Min(4f)] public float SpawnRingOuter = 38f;
        [Tooltip("Beyond this distance from the player a live mob is culled (out of sight, save budget).")]
        [Min(10f)] public float CullRadius = 70f;
        [Tooltip("A roaming mob aggros (paths to) the player within this radius; beyond it, it wanders its anchor.")]
        [Min(2f)] public float AggroRadius = 18f;
        [Tooltip("A chasing mob GIVES UP and walks home if dragged this far from where it began the " +
                 "chase — so running away actually breaks pursuit. It won't re-aggro until it's home.")]
        [Min(4f)] public float LeashRadius = 28f;
        [Tooltip("Radius a wandering mob picks new roam points within, around its spawn anchor.")]
        [Min(1f)] public float WanderRadius = 8f;

        [Header("Wander")]
        [Tooltip("Seconds between a wandering (non-aggro) mob re-picking a roam point.")]
        [Min(0.5f)] public float WanderRepathInterval = 4f;

        // Region tints so mobs read at a glance in the placeholder-capsule build.
        private static readonly Color LivingTint = new Color(0.38f, 0.55f, 0.28f);  // mossy green — Wildlands
        private static readonly Color WoundTint  = new Color(0.52f, 0.18f, 0.42f);  // sickly violet — Wound-tied

        private float _tickTimer;
        private Transform _player;
        private Transform _root;
        private int _counter;

        // One live roaming mob + its wander bookkeeping.
        private sealed class Mob
        {
            public Enemy Enemy;
            public Transform RoamAnchor;   // a child transform the mob wanders around / paths to when not aggroed
            public float NextWanderAt;
            public bool Aggroed;
            public bool Leashing;        // walking home after being dragged past the leash; ignores aggro until back
            public Vector3 LeashOrigin;  // home territory captured when the chase began
        }

        private readonly List<Mob> _live = new List<Mob>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("RegionMobSpawner").AddComponent<RegionMobSpawner>();
        }

        private void Awake()
        {
            // Destroy(this), not the host — this may share a GameObject (CLAUDE.md memory).
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // WO-482 LIGHT WORLD: when the overworld-encounter loop is on, the open world
            // holds ONLY the wandering orc "reps" (OverworldEncounterSpawner) — no ambient
            // roamers. Suppress this entire population so the player's attacks land on a rep
            // that drops to battle, not on dozens of non-encounter wanderers. Flag OFF =
            // today's behavior (fully reversible — the spawner is gated, not deleted).
            if (DeNelle.Core.FeatureFlags.OverworldEncounter) return;

            _tickTimer -= Time.deltaTime;
            if (_tickTimer > 0f) return;
            _tickTimer = TickInterval;

            ResolvePlayer();
            if (_player == null) return;

            // Only run while the player is actually in an outer region. In the safe
            // Village home zone we spawn nothing (and let any stragglers cull out).
            Vector3 pp = _player.position;
            // WO-606: prefer the geotagged spawn AREAS when authored (data-driven); else the legacy
            // origin-relative region roster. Outside every authored area = no spawn (emergent exclusion,
            // composes with the moat carve). Non-breaking: no JSON/areas -> HasAny false -> legacy path.
            bool playerOutside = SpawnAreaTable.HasAny
                ? SpawnAreaTable.HasAreaAt(pp)
                : RegionSpawnTable.HasRoster(ZoneManager.GetZone(pp));

            PruneAndDrive(pp);

            if (playerOutside)
                TopUpPopulation(pp);
        }

        // ── Maintenance: cull dead/far mobs, drive wander vs aggro ────────────────

        private void PruneAndDrive(Vector3 playerPos)
        {
            float cullSqr = CullRadius * CullRadius;
            float aggroSqr = AggroRadius * AggroRadius;

            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var mob = _live[i];
                if (mob == null || mob.Enemy == null || mob.Enemy.IsDead)
                {
                    Despawn(mob);
                    _live.RemoveAt(i);
                    continue;
                }

                float dSqr = (mob.Enemy.transform.position - playerPos).sqrMagnitude;
                if (dSqr > cullSqr)
                {
                    // Too far — recycle the budget (the population tops up near the player).
                    Despawn(mob);
                    _live.RemoveAt(i);
                    continue;
                }

                bool playerInRange = dSqr <= aggroSqr;

                // Leash (owner 2026-06-01): a chasing mob keeps PACE, so player-distance de-aggro
                // alone never triggers — it stuck to you forever. Add a HOME leash: once dragged
                // past LeashRadius from where the chase began, it gives up, walks home, and ignores
                // aggro until it's back. So running away actually breaks pursuit.
                if (mob.Leashing &&
                    (mob.Enemy.transform.position - mob.LeashOrigin).sqrMagnitude <= 9f)
                    mob.Leashing = false;   // reached home — resume normal behaviour

                bool wantAggro = playerInRange && !mob.Leashing;
                if (wantAggro)
                {
                    // P23 (HUD_OBSIDIAN A4.5): the ENGAGEMENT WINDOW — a live pursuit is
                    // re-reported every drive tick; the report self-expires (PursuitTtl)
                    // when the chase ends by ANY path (leash, death, despawn, scene swap),
                    // so the HUD's hostile(prebattle) posture can never stick on.
                    if (mob.Aggroed || playerInRange)
                        DeNelle.Core.HudModel.PostureSignals.ReportPursuit(mob.Enemy.GetInstanceID());

                    if (!mob.Aggroed)
                    {
                        // Begin the chase — capture home territory for the leash.
                        mob.Aggroed = true;
                        mob.LeashOrigin = mob.RoamAnchor != null
                            ? mob.RoamAnchor.position : mob.Enemy.transform.position;
                        mob.Enemy.SetBrainTarget(_player);
                        // "Spotted" tell — a one-shot "!" so the player feels the moment a
                        // wanderer locks on instead of getting blindsided (owner 2026-06-02).
                        EnemyAlertTell.Flash(mob.Enemy.transform);
                    }
                    else if ((mob.Enemy.transform.position - mob.LeashOrigin).sqrMagnitude
                             > LeashRadius * LeashRadius)
                    {
                        // Dragged past the leash — disengage and head home.
                        mob.Aggroed  = false;
                        mob.Leashing = true;
                        if (mob.RoamAnchor != null) mob.RoamAnchor.position = mob.LeashOrigin;
                        mob.Enemy.SetBrainTarget(mob.RoamAnchor);
                    }
                }
                else
                {
                    // Player out of range (or leashing home): wander the anchor.
                    if (mob.Aggroed)
                    {
                        mob.Aggroed = false;
                        mob.Enemy.SetBrainTarget(mob.RoamAnchor);
                    }
                    if (!mob.Leashing && Time.time >= mob.NextWanderAt)
                    {
                        RepickRoamPoint(mob);
                        mob.NextWanderAt = Time.time + WanderRepathInterval;
                    }
                }
            }
        }

        private void RepickRoamPoint(Mob mob)
        {
            if (mob.RoamAnchor == null) return;
            Vector2 r = Random.insideUnitCircle * WanderRadius;
            Vector3 want = mob.Enemy.transform.position + new Vector3(r.x, 0f, r.y);
            if (NavMesh.SamplePosition(want, out NavMeshHit hit, WanderRadius, NavMesh.AllAreas))
                mob.RoamAnchor.position = hit.position;
        }

        // ── Population top-up around the player ───────────────────────────────────

        private void TopUpPopulation(Vector3 playerPos)
        {
            int deficit = EffectiveTarget() - _live.Count;
            if (deficit <= 0) return;

            using var _ = FlowTrace.Enter("RegionMobs", $"TopUpPopulation deficit={deficit} live={_live.Count}");

            if (_root == null) _root = new GameObject("[RegionMobs]").transform;

            // WO-316: spawn a small FAMILY PACK per top-up rather than one lone mob —
            // a lead (brute/charger), a fast skirmisher DPS, and (room permitting) a
            // caster support, clustered around one anchor so they roam + aggro as a
            // group. Capped by the remaining deficit so the population target holds.
            if (!TryFindSpawnPoint(playerPos, out Vector3 packPos))
            {
                FlowTrace.Warn("RegionMobs", "TopUpPopulation: no NavMesh-valid spawn point in the ring — no pack this tick.");
                return;
            }

            RegionId region = ZoneManager.GetZone(packPos);
            bool areaHere = SpawnAreaTable.HasAny && SpawnAreaTable.HasAreaAt(packPos);
            if (!areaHere && !RegionSpawnTable.HasRoster(region))
            {
                FlowTrace.Step("RegionMobs", $"TopUpPopulation: {region} has no area/roster — not a spawn region, skipping.");
                return;   // not an outer region / not inside an authored area
            }

            float depth = ZoneManager.Depth(packPos);
            int threat  = ZoneManager.ThreatLevel(packPos);

            SpawnPack(region, packPos, depth, threat, deficit);
        }

        // ── WO-316: compose + spawn a small region family pack ────────────────────
        // Tank lead + skirmisher DPS + (optional) caster support, picked from the
        // region roster and clustered around the pack origin. Each member gets an
        // EnemyBrain ROLE so the pack reads as a squad (tank screens, DPS flanks,
        // support hangs back). Reuses SpawnMob for the per-body build (one enemy-
        // creation path) and only adds the role assignment + clustering on top.
        private void SpawnPack(RegionId region, Vector3 origin, float depth, int threat, int budget)
        {
            using var _ = FlowTrace.Enter("RegionMobs", $"SpawnPack region={region} budget={budget}");
            // Pack size scales gently with the remaining deficit (2-3), never more
            // than budget so a top-up can't overshoot the population target.
            int packSize = Mathf.Clamp(budget, 1, 3);

            // WO-606: when a geotagged area is authored here, the pack's enemy IDS + LEVEL come from
            // its resolved family/composition (data-driven) instead of the legacy RegionSpawnTable pick.
            // seedBudget lightly caps a single top-up's pack size (owner-tunable). Falls back per-member
            // to RegionSpawnTable.PickEnemyId when no area/draw is present (non-breaking).
            SpawnDraw areaDraw = default;
            if (SpawnAreaTable.HasAny)
            {
                areaDraw = SpawnAreaTable.BuildDraw(origin);
                if (areaDraw.Valid)
                {
                    packSize = Mathf.Clamp(packSize, 1, Mathf.Max(1, Mathf.Min(3, areaDraw.SeedBudget)));
                    threat = Mathf.Max(threat, areaDraw.Level);
                }
            }

            for (int i = 0; i < packSize; i++)
            {
                // Cluster members within a couple metres of the pack origin.
                Vector2 jitter = i == 0 ? Vector2.zero : Random.insideUnitCircle * 3.5f;
                Vector3 want = origin + new Vector3(jitter.x, 0f, jitter.y);
                Vector3 pos = want;
                if (NavMesh.SamplePosition(want, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                    pos = hit.position;

                // MOAT EXCLUSION: the pack origin cleared the band, but a clustered member can jitter
                // into the moat/seam edge — drop just this member rather than spawn it in the water.
                if (MoatExclusion.IsInMoatBand(pos))
                {
                    FlowTrace.Warn("RegionMobs", $"SpawnPack: member {i} jittered into the moat band at {pos} — skipped (no water spawn).");
                    continue;
                }

                // Member 0 = the lead (any roster pick → tends to the brute/charger);
                // later members fill complementary roles for a real squad mix.
                string enemyId = (areaDraw.Valid && areaDraw.EnemyIds != null && areaDraw.EnemyIds.Length > 0)
                    ? areaDraw.EnemyIds[i % areaDraw.EnemyIds.Length]           // WO-606: role-ordered area draw
                    : RegionSpawnTable.PickEnemyId(region, depth, Random.value); // legacy fallback
                if (string.IsNullOrEmpty(enemyId)) continue;

                var mob = SpawnMob(enemyId, region, pos, threat);
                if (mob == null || mob.Enemy == null || mob.Enemy.gameObject == null)
                {
                    // SpawnMob already Failed-loud on a null Build — just skip the role stamp so a
                    // null member never NRE's the pack loop (the rest of the pack still spawns).
                    FlowTrace.Step("RegionMobs", $"SpawnPack: member {i} ('{enemyId}') did not spawn — skipped.");
                    continue;
                }

                // Stamp a tactical role so the pack behaves as tank + DPS + support.
                // EnemyFactory builds a plain body (no brain) — add one for the role.
                var brain = mob.Enemy.GetComponent<EnemyBrain>();
                if (brain == null) brain = mob.Enemy.gameObject.AddComponent<EnemyBrain>();
                brain.Role = PackRoleForIndex(enemyId, i);

                _live.Add(mob);
            }
        }

        // Pack role by slot: lead = Tank (screens), then a Ranged skirmisher (DPS),
        // then a Healer support. Casters always read as Healer regardless of slot.
        private static EnemyRole PackRoleForIndex(string enemyId, int index)
        {
            if (enemyId == "tiefling-cultist" || enemyId == "orc-shaman" || enemyId == "necromancer")
                return EnemyRole.Healer;
            switch (index)
            {
                case 0:  return EnemyRole.Tank;
                case 1:  return EnemyRole.Ranged;
                default: return EnemyRole.Healer;
            }
        }

        // ── Progress-ramped effective target (WO-216 / DEF-118) ──────────────────
        // The flat TargetPopulation of 6 swarmed brand-new players the moment they
        // stepped outside. Instead the EFFECTIVE target ramps with player progress:
        // it starts at EarlyTargetFloor (1-2 wanderers to learn the fight on) and adds
        // one mob per WavesPerExtraMob cleared waves, capped at TargetPopulation.
        //
        // PROGRESS SIGNAL = GameState.BestWave (highest village wave ever cleared). Chosen
        // because it (a) already exists and is the canonical persisted "how far am I"
        // field — no new persistence invented (WO-216 constraint), (b) is in Core so it
        // reads cleanly from Village, (c) survives across runs (a returning veteran keeps
        // their fuller wilderness), and (d) is the same notion of progress the wave siege
        // cadence keys off, so the wandering layer and the siege layer ramp together.
        // Hero level was the alternative but it's in-memory only (resets each run), so a
        // veteran's outer world would reset to 1-2 mobs every session — wrong feel.
        private int EffectiveTarget()
        {
            int bestWave = GameStateService.Instance?.State?.BestWave ?? 0;
            int slope = Mathf.Max(1, WavesPerExtraMob);
            int ramped = EarlyTargetFloor + bestWave / slope;
            return Mathf.Clamp(ramped, EarlyTargetFloor, TargetPopulation);
        }

        // Find a NavMesh-valid point in the spawn ring around the player.
        // MOAT EXCLUSION: any candidate that snaps into the castle moat water band / RegionGate
        // seam (MoatExclusion.IsInMoatBand) is REJECTED and re-rolled — never spawn a mob in the
        // water or on the seam. Retries are capped; a rejection is traced so a headless run shows it.
        private bool TryFindSpawnPoint(Vector3 playerPos, out Vector3 pos)
        {
            const int MaxAttempts = 10;   // was 6 — allow a few extra rolls to dodge the moat band
            int moatRejects = 0;
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                float ang = Random.value * Mathf.PI * 2f;
                float rad = Random.Range(SpawnRingInner, SpawnRingOuter);
                Vector3 want = playerPos + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                if (NavMesh.SamplePosition(want, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                {
                    if (MoatExclusion.IsInMoatBand(hit.position))
                    {
                        moatRejects++;
                        continue;   // in the moat/seam band — re-roll
                    }
                    pos = hit.position;
                    return true;
                }
            }
            if (moatRejects > 0)
                FlowTrace.Warn("RegionMobs",
                    $"TryFindSpawnPoint: no ring point cleared the moat band in {MaxAttempts} tries ({moatRejects} moat-rejects) — no pack this tick.");
            pos = playerPos;
            return false;
        }

        // ── Spawn one roaming mob (reuse Enemy, code-built capsule — TribeManager precedent) ──

        private Mob SpawnMob(string enemyId, RegionId region, Vector3 pos, int threat)
        {
            using var _ = FlowTrace.Enter("RegionMobs", $"SpawnMob id='{enemyId}' region={region}");
            var def = BuildRoamerDef(enemyId, threat);

            // Overly-easy welcome (owner 2026-06-02: "the beginning should be overly easy" —
            // "hello welcome to town... dead" is the opposite of the onboarding we want).
            // Scale early-game enemy HP + contact damage WAY down for a brand-new player
            // (BestWave 0 -> x0.35), ramping to full strength by ~BestWave 6. Threat-scaling
            // still ramps the LATE game up; this only softens the first hours into a power
            // fantasy so a fresh hero (or a grant reviewer) wins while they learn the controls.
            float ease = Mathf.Lerp(0.35f, 1f,
                Mathf.Clamp01((GameStateService.Instance?.State?.BestWave ?? 0) / 6f));
            def.Hp = Mathf.Max(1f, def.Hp * ease);
            def.ContactDamage = Mathf.Max(0f, def.ContactDamage * ease);

            // One skinned enemy body via the shared EnemyFactory — no parallel spawn code
            // (CLAUDE.md §9). The factory handles layer + collider + skin + animator + agent.
            // P0 NRE GUARD (TGVRU-R): Build can return null (def-null / internal throw). The
            // OLD code dereferenced enemy.gameObject immediately — a null result NRE'd here and
            // aborted the ENTIRE top-up pass (whole pack lost). Guard the result, Fail-loud, and
            // SKIP this one mob so the rest of the pack still spawns.
            var enemy = Guard.Try("RegionMobs", $"EnemyFactory.Build {enemyId}",
                () => EnemyFactory.Build(def, pos, Quaternion.identity, _root), null);
            if (enemy == null || enemy.gameObject == null)
            {
                FlowTrace.Fail("RegionMobs",
                    $"SpawnMob: EnemyFactory.Build returned null for '{enemyId}' ({region}) — skipping this mob (pack continues, no NRE-abort).");
                return null;
            }
            var go = enemy.gameObject;
            go.name = $"RegionMob ({enemyId} · {region})";

            // A per-mob roam anchor (child of the root) it wanders around when not aggroed.
            // Using a Transform target keeps it OFF the Heart-march — these mobs never
            // trickle into the village. SetBrainTarget(anchor) overrides the Heart fallback.
            var anchorGo = new GameObject($"RoamAnchor-{enemyId}-{_counter}");
            anchorGo.transform.SetParent(_root, false);
            anchorGo.transform.position = pos;

            enemy.Configure($"region-{region}-{enemyId}-{_counter++}", def, ResolveHeart());
            enemy.SetBrainTarget(anchorGo.transform);   // roam, not Heart-march

            // Red-skull readiness tell — code-built nameplate (no UXML).
            ThreatSkullPlate.Attach(go, () => threat);

            // V (TGVRU-V): prove the committed mob actually renders + has an agent on the
            // navmesh, so a "spawned but invisible / stuck-off-mesh" mob self-reports in the
            // capture instead of silently failing. Non-fatal — the mob still joins the pack.
            VerifySpawnedMob("RegionMobs", go, $"{enemyId}/{region}");

            FlowTrace.Step("RegionMobs", $"SpawnMob committed '{go.name}' at {go.transform.position}.");
            return new Mob
            {
                Enemy = enemy,
                RoamAnchor = anchorGo.transform,
                NextWanderAt = Time.time + Random.Range(0f, WanderRepathInterval),
                Aggroed = false,
            };
        }

        // Archetype -> skeleton model (the readable visual variety). Thematic note: the
        // Wildlands roster names (orc/caveman/wolf) currently borrow the skeleton family —
        // the only humanoid enemy models in Resources/Enemies — chosen by ROLE/SIZE so the
        // silhouette reads the threat. Swap to bespoke models here (one line per id) when
        // the new character packs land.
        private static string ModelForRoamer(string enemyId)
        {
            switch (enemyId)
            {
                case "orc-raider":       return "Skeleton_Warrior"; // heavy melee
                case "caveman":          return "Skeleton_Golem";   // big brute
                case "feral-wolf":       return "Skeleton_Rogue";   // fast skirmisher
                case "tiefling-cultist": return "Skeleton_Mage";    // caster
                case "necromancer":      return "Necromancer";      // dedicated elite
                default:                  return "Skeleton_Minion";
            }
        }

        private static void Despawn(Mob mob)
        {
            if (mob == null) return;
            if (mob.RoamAnchor != null) Destroy(mob.RoamAnchor.gameObject);
            if (mob.Enemy != null) Destroy(mob.Enemy.gameObject);
        }

        // ── Synthesised, threat-scaled stat blocks (TribeManager precedent) ───────
        // The Wildlands roster ids (orc-raider/caveman/feral-wolf) + tiefling-cultist
        // are NOT in enemies.json yet (forward design in docs/enemy-codex.md). We
        // synthesise a code-built EnemyDef per id here — this does NOT re-stat any
        // JSON-owned enemy; it gives the not-yet-statted roamers a sensible body that
        // ThreatLevel then scales. When enemies.json gains these ids, swap this for a
        // catalog lookup with no other change. necromancer EXISTS in JSON — but as a
        // 1700-HP village wave-boss; a roaming "lieutenant" reads it down to a tough
        // caster body so the open world isn't gated behind a raid boss.
        private static EnemyDef BuildRoamerDef(string enemyId, int threat)
        {
            float scale = 1f + 0.10f * Mathf.Max(0, threat);   // ThreatLevel-driven stat scaling

            // Per-roster archetype base. Codex roles: Wolf = fast skirmisher, Caveman =
            // brute walker, Orc Raider = heavy charger, Tiefling = demonic skirmisher,
            // Necromancer = caster lieutenant.
            string id = enemyId ?? "feral-wolf";
            string name; string ai; float hp; float spd; float dmg; float interval; float height; int xp;
            switch (id)
            {
                case "orc-raider":
                    name = "Orc Raider";     ai = "charger";    hp = 95f;  spd = 3.1f; dmg = 12f; interval = 1.3f; height = 2.0f; xp = 22; break;
                case "caveman":
                    name = "Wildlands Caveman"; ai = "walker";  hp = 70f;  spd = 2.7f; dmg = 9f;  interval = 1.4f; height = 1.9f; xp = 16; break;
                case "feral-wolf":
                    name = "Feral Wolf";     ai = "skirmisher"; hp = 42f;  spd = 4.2f; dmg = 7f;  interval = 1.0f; height = 1.2f; xp = 12; break;
                case "tiefling-cultist":
                    name = "Tiefling Cultist"; ai = "skirmisher"; hp = 80f; spd = 3.4f; dmg = 11f; interval = 1.2f; height = 1.9f; xp = 20; break;
                case "necromancer":
                    name = "Wound Necromancer"; ai = "walker";  hp = 140f; spd = 2.2f; dmg = 15f; interval = 1.4f; height = 2.1f; xp = 34; break;
                default:
                    name = "Wildlands Beast"; ai = "walker";    hp = 55f;  spd = 3.0f; dmg = 8f;  interval = 1.3f; height = 1.7f; xp = 14; break;
            }

            return new EnemyDef
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
        }

        // ── Helpers ───────────────────────────────────────────────────────────────

        private static bool IsWoundTied(RegionId region) =>
            region == RegionId.Mirewood || region == RegionId.Ashwood;

        // TGVRU-V (shared verify): a freshly-built enemy can render==false (no enabled mesh)
        // or its NavMeshAgent can be OFF the baked navmesh (spawned just off-surface) — both
        // are "spawned but broken" states that used to pass silently. Trace the exact counts so
        // a capture splits "invisible" vs "off-mesh" with zero guessing. Non-fatal (the mob is
        // already committed); a Warn rolls up to the F8 break-log. Null-safe throughout.
        private static void VerifySpawnedMob(string system, GameObject go, string label)
        {
            if (go == null)
            {
                FlowTrace.Warn(system, $"VerifySpawnedMob: '{label}' has a null GameObject — cannot verify render/agent.");
                return;
            }

            int enabledRenderers = 0;
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
                if (r != null && r.enabled) enabledRenderers++;

            var agent = go.GetComponentInChildren<NavMeshAgent>();
            bool hasAgent = agent != null;
            bool onMesh = hasAgent && agent.isOnNavMesh;

            if (enabledRenderers == 0)
                FlowTrace.Warn(system,
                    $"VerifySpawnedMob: '{label}' has 0 enabled renderer(s) — spawned but INVISIBLE (check skin build).");
            if (!hasAgent)
                FlowTrace.Warn(system,
                    $"VerifySpawnedMob: '{label}' has no NavMeshAgent — it will not path (check EnemyFactory).");
            else if (!onMesh)
                FlowTrace.Warn(system,
                    $"VerifySpawnedMob: '{label}' agent is OFF the navmesh at {go.transform.position} — it will idle (point off baked surface).");

            FlowTrace.Step(system,
                $"VerifySpawnedMob '{label}': enabledRenderers={enabledRenderers} hasAgent={hasAgent} onNavMesh={onMesh}.");
        }

        private void ResolvePlayer()
        {
            if (_player != null) return;
            var p = GameObject.FindWithTag("Player");
            _player = p != null ? p.transform : null;
        }

        private static Transform ResolveHeart()
        {
            var hc = FindAnyObjectByType<HeartController>();
            if (hc != null) return hc.transform;
            var byName = GameObject.Find("HeartOfTown") ?? GameObject.Find("Tree of Life") ?? GameObject.Find("Heart");
            return byName != null ? byName.transform : null;
        }
    }
}
