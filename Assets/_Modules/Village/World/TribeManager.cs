// =============================================================================
// TribeManager — wandering tribes: radius-triggered spawn + state-saving (WO-160).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// A tribe is a persistent roaming raider band anchored to a region. It is a tiny
// STATE RECORD at all times (TribeState in GameState.Tribes) and only materialises
// into live enemy GameObjects when the player enters its activation radius — then
// despawns + writes its state back when the player leaves. This is the per-encounter
// twin of zone streaming: cheap while far, remembered across visits.
//
// THE TWO MECHANICS (owner):
//   1. Radius-triggered spawn (the perf gate) — a throttled distance check flips a
//      tribe ACTIVE within ActivationRadius, DORMANT beyond DeactivationRadius
//      (hysteresis, no thrash). Active = members spawned; dormant = record-only.
//   2. State-saving — on de-activation, write members-remaining / cleared back to
//      the record; on re-activation, respawn FROM the record (damaged returns
//      reduced; a wiped tribe returns smaller each clear — ClearCount curve).
//
// WO-159 ⇄ WO-160 — tribes are the THREAT to settlements: when active, a tribe's
// members target the nearest Settlement in roam (IDamageableStructure) so an
// undefended claim is overrun and razed. Raid size is RANDOMISED within the
// region's threat band (min..max from TribeDef, scaled by ThreatLevel = tier ×
// depth) — some raids light, some brutal, never a fixed solvable number.
//
// RECONCILIATION (no parallel spawn/enemy system, CLAUDE.md §9):
//   • Spawns reuse Enemy + Enemy.Configure (the SAME path as EnemyFamilyTestSpawner
//     / WaveManager.BuildPlaceholderEnemy) — code-built capsules, no scene/prefab/SO
//     needed, NavMesh-seated. Enemy.TickContactAttack already damages any
//     IDamageableStructure it reaches, so a raider razes a settlement with no extra AI.
//   • Scaling reuses ZoneManager.ThreatLevel (the single difficulty read).
//   • State persists via GameState.Tribes (save owner wires the schema round-trip).
//   • Self-bootstrapping DDOL (mirrors EnemyFamilyTestSpawner) so it needs no
//     scene edit / no bake.
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
    /// Activates/de-activates wandering tribes by player proximity and persists their
    /// state. One self-bootstrapping scene singleton; no prefab/SO/scene wiring.
    /// </summary>
    public sealed class TribeManager : MonoBehaviour
    {
        public static TribeManager Instance { get; private set; }

        [Header("Activation")]
        [Tooltip("Seconds between proximity checks (throttled — never per-frame O(tribes)).")]
        [Min(0.1f)] public float CheckInterval = 0.4f;

        [Header("Raid scaling (data-driven; clamps the random raid-size band)")]
        [Tooltip("Hard floor on a rolled raid — fairness guardrail (no zero-member raid).")]
        [Min(1)] public int MinRaidFloor = 1;
        [Tooltip("Hard ceiling on a rolled raid — fairness guardrail (no freak unwinnable roll).")]
        [Min(1)] public int MaxRaidCeiling = 16;

        [Tooltip("Members removed from the band per previous clear (reduced respawn). " +
                 "Owner-locked: a wiped tribe returns smaller/weaker each time.")]
        [Min(0)] public int RespawnReductionPerClear = 1;

        [Tooltip("After this many clears a tribe stops respawning (fully dominated). 0 = always respawns (floored).")]
        [Min(0)] public int ClearsUntilGone = 4;

        [Header("Early-game ramp (WO-216 / DEF-118)")]
        [Tooltip("How many raiders are shaved off a FRESH raid roll for a brand-new player (BestWave 0), " +
                 "easing back to 0 as progress climbs. Keeps early camps small so a new player meets a " +
                 "couple of wanderers, not a full raid band. Never drops a raid below MinRaidFloor.")]
        [Min(0)] public int EarlyRaidReduction = 4;

        [Tooltip("Cleared-wave count at which the early-game raid reduction has fully faded (full-size raids). " +
                 "Below it, the reduction scales down linearly with BestWave (WO-216 ramp).")]
        [Min(1)] public int FullRaidByWave = 8;

        // ── Region tints so the families read at a glance (debug-friendly) ──
        private static readonly Color RaiderTint = new Color(0.62f, 0.22f, 0.20f);

        private float _checkTimer;
        private Transform _player;
        private Transform _root;

        // Live spawned members per active tribe id (so de-activation can despawn + count survivors).
        private readonly Dictionary<string, List<Enemy>> _live = new Dictionary<string, List<Enemy>>();
        private readonly HashSet<string> _activeIds = new HashSet<string>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("TribeManager").AddComponent<TribeManager>();
        }

        private void Awake()
        {
            // Destroy(this) not the host — this may share a GameObject (CLAUDE.md memory).
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            EnsureSeededTribes();
        }

        private void Update()
        {
            // WO-482 LIGHT WORLD: when the overworld-encounter loop is on, the open world holds
            // ONLY the wandering orc "reps" — no materialized tribe raids. Suppress the whole
            // activation/retarget pass so a tribe never spawns a band the player attacks that
            // doesn't drop to battle. Flag OFF = today's behavior (gated, not deleted, reversible).
            if (DeNelle.Core.FeatureFlags.OverworldEncounter) return;

            _checkTimer -= Time.deltaTime;
            if (_checkTimer > 0f) return;
            _checkTimer = CheckInterval;

            ResolvePlayer();
            if (_player == null) return;

            var tribes = Tribes();
            if (tribes == null) return;

            Vector3 pp = _player.position;
            for (int i = 0; i < tribes.Count; i++)
            {
                var t = tribes[i];
                if (t == null) continue;
                float distSqr = (AnchorPos(t.Anchor) - pp).sqrMagnitude;
                bool isActive = _activeIds.Contains(t.Id);

                var def = DefFor(t.Id);
                float actR = def != null ? def.ActivationRadius : 45f;
                float deactR = def != null ? def.DeactivationRadius : 60f;

                if (!isActive && distSqr <= actR * actR)
                    Activate(t, def);
                else if (isActive && distSqr >= deactR * deactR)
                    Deactivate(t);
            }

            // Drive active members toward the nearest settlement so undefended claims get raided.
            RetargetActiveMembers();
        }

        // ── Activation ────────────────────────────────────────────────────────

        private void Activate(TribeState t, TribeDef def)
        {
            using var _ = FlowTrace.Enter("Tribes", $"Activate tribe='{t?.Id}'");
            // Fully-dominated tribes do not respawn.
            if (ClearsUntilGone > 0 && t.ClearCount >= ClearsUntilGone)
            {
                _activeIds.Add(t.Id);                 // mark "handled" so we don't re-roll every check
                _live[t.Id] = new List<Enemy>();      // empty roster
                return;
            }

            int count = ResolveSpawnCount(t, def);
            if (count <= 0) { _activeIds.Add(t.Id); _live[t.Id] = new List<Enemy>(); return; }

            if (_root == null) _root = new GameObject("[WanderingTribes]").transform;

            int threat = ZoneManager.ThreatLevel(AnchorPos(t.Anchor));
            var members = new List<Enemy>(count);
            Vector3 anchor = AnchorPos(t.Anchor);
            for (int i = 0; i < count; i++)
            {
                Vector3 pos = anchor + new Vector3(
                    Random.Range(-4f, 4f), 0f, Random.Range(-4f, 4f));
                var e = SpawnRaider(t.Id, i, pos, threat);
                if (e != null) members.Add(e);
            }

            _live[t.Id] = members;
            _activeIds.Add(t.Id);
            t.MembersRemaining = members.Count;   // record what actually rolled this cycle

            // U (TGVRU-U): Debug.Log -> FlowTrace so the activation lands in the capture, and
            // a Warn surfaces a partial spawn (rolled N but fewer materialised — a null Build).
            if (members.Count < count)
                FlowTrace.Warn("Tribes",
                    $"Tribe '{t.Id}' ACTIVE but only {members.Count}/{count} raiders materialised " +
                    "(some Build returned null — see SpawnRaider Fail lines).");
            FlowTrace.Step("Tribes",
                $"Tribe '{t.Id}' ACTIVE — {members.Count} raiders (threat {threat}, clearCount {t.ClearCount}).");
        }

        private void Deactivate(TribeState t)
        {
            // Count survivors, write state back, despawn the live members.
            int survivors = 0;
            if (_live.TryGetValue(t.Id, out var members) && members != null)
            {
                for (int i = 0; i < members.Count; i++)
                {
                    var e = members[i];
                    if (e != null && !e.IsDead) { survivors++; }
                    if (e != null) Destroy(e.gameObject);
                }
            }

            t.MembersRemaining = survivors;
            t.LastSeenAtMs = System.DateTime.UtcNow.Subtract(
                new System.DateTime(1970, 1, 1, 0, 0, 0, System.DateTimeKind.Utc)).TotalMilliseconds;

            if (survivors <= 0)
            {
                // Wiped this visit — bump clearCount so the NEXT respawn is reduced.
                t.Cleared = true;
                t.ClearCount++;
            }
            else
            {
                // Survivors remain — this is a DAMAGED tribe, not a fresh-roll one. Clear
                // the just-wiped flag so the next activation honours MembersRemaining
                // (the persisted reduced count) instead of re-rolling the band.
                t.Cleared = false;
            }

            _live.Remove(t.Id);
            _activeIds.Remove(t.Id);

            Debug.Log($"[TribeManager] Tribe '{t.Id}' DORMANT — {survivors} survivor(s) " +
                      $"persisted{(survivors <= 0 ? $", WIPED (clearCount now {t.ClearCount})" : "")}.");
        }

        // ── Raid-size roll (randomised WITHIN the region's threat band) ──────

        private int ResolveSpawnCount(TribeState t, TribeDef def)
        {
            // A damaged tribe returns at its persisted reduced count (state-saving).
            if (t.MembersRemaining >= 0 && !t.Cleared)
                return t.MembersRemaining;

            int min = def != null ? def.MinMembers : 3;
            int max = def != null ? def.MaxMembers : 6;
            if (max < min) max = min;

            // Roll within the band — never a fixed, solvable number.
            int rolled = Random.Range(min, max + 1);

            // Reduced respawn after clears (owner-locked): smaller each wipe.
            rolled -= t.ClearCount * RespawnReductionPerClear;

            // WO-216 early-game ramp: shave a FRESH raid down for new players so the
            // first camps are small wanderers, not full bands; the reduction fades to 0
            // as progress climbs. Same progress signal as RegionMobSpawner — GameState.BestWave
            // (persisted, Core-readable, shared with the wave-siege cadence). Only the fresh-roll
            // path reaches here (damaged tribes return at their persisted count above), so this
            // never corrupts the state-saving curve.
            rolled -= EarlyRaidReductionNow();

            return Mathf.Clamp(rolled, MinRaidFloor, MaxRaidCeiling);
        }

        // Current early-game raid-size reduction (WO-216): EarlyRaidReduction at BestWave 0,
        // easing linearly to 0 once BestWave reaches FullRaidByWave. Returns 0 for veterans.
        private int EarlyRaidReductionNow()
        {
            if (EarlyRaidReduction <= 0) return 0;
            int bestWave = GameStateService.Instance?.State?.BestWave ?? 0;
            int fadeBy = Mathf.Max(1, FullRaidByWave);
            if (bestWave >= fadeBy) return 0;
            float t01 = (float)bestWave / fadeBy;            // 0 fresh → 1 at FullRaidByWave
            return Mathf.RoundToInt(EarlyRaidReduction * (1f - t01));
        }

        // ── Spawn one raider (reuse Enemy, code-built capsule) ──

        private Enemy SpawnRaider(string tribeId, int index, Vector3 pos, int threat)
        {
            using var _ = FlowTrace.Enter("Tribes", $"SpawnRaider tribe='{tribeId}' idx={index}");
            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                pos = hit.position;

            // Compose the raider from the region's roster (RegionSpawnTable) so a tribe's
            // members match the region theme (Wildlands living vs Wound-tied) — the SAME
            // roster RegionMobSpawner reads. ThreatLevel still scales the stats.
            RegionId region = ZoneManager.GetZone(pos);
            string rosterId = RegionSpawnTable.HasRoster(region)
                ? RegionSpawnTable.PickEnemyId(region, ZoneManager.Depth(pos), Random.value)
                : null;
            var def = BuildRaiderDef(tribeId, threat, rosterId);

            // One skinned body via the shared EnemyFactory — no parallel spawn code (CLAUDE.md §9).
            // P0 NRE GUARD (TGVRU-R): Build can return null (def-null / internal throw). The OLD
            // code dereferenced enemy.gameObject.name immediately — a null NRE'd here and aborted
            // the WHOLE raid spawn loop (Activate's for-loop), leaving the tribe half-spawned or
            // empty. Guard the result, Fail-loud, and return null so Activate skips just this raider.
            var enemy = Guard.Try("Tribes", $"EnemyFactory.Build raider {tribeId}-{index}",
                () => EnemyFactory.Build(def, pos, Quaternion.identity, _root), null);
            if (enemy == null || enemy.gameObject == null)
            {
                FlowTrace.Fail("Tribes",
                    $"SpawnRaider: EnemyFactory.Build returned null for tribe '{tribeId}' raider {index} — skipping (raid continues, no NRE-abort).");
                return null;
            }
            enemy.gameObject.name = $"Raider ({tribeId}-{index})";
            // March goal = the nearest standing settlement (the claim to raze), falling
            // back to the Heart when no settlement is in range. We DON'T add EnemyBrain:
            // its no-tactics DPS path targets only hero/towers/tagged targets (never a
            // settlement) and would stomp the march goal each frame via
            // SetBrainTargetPosition. Enemy.DriveNav marches to the goal and
            // Enemy.TickContactAttack damages ANY IDamageableStructure it reaches — so a
            // settlement (IDamageableStructure) is raided with zero extra code, exactly
            // like the Heart/walls. RetargetActiveMembers refreshes the goal as the
            // player builds/loses settlements.
            // Configure the STABLE Heart as the fallback march goal (_heart) so a raider
            // never strands when its settlement target is razed. The settlement redirect
            // rides on top via SetBrainTarget (priority over _heart, cleared back to the
            // Heart by passing null) — refreshed every check in RetargetActiveMembers.
            enemy.Configure($"tribe-{tribeId}-{index}", def, ResolveHeart());
            var firstTarget = NearestSettlementTransform(pos);
            if (firstTarget != null) enemy.SetBrainTarget(firstTarget);

            // V (TGVRU-V): prove the committed raider renders + has an agent on the navmesh, so a
            // "spawned but invisible / stuck-off-mesh" raider self-reports in the capture.
            VerifySpawnedMob("Tribes", enemy.gameObject, $"{tribeId}-{index}");
            FlowTrace.Step("Tribes", $"SpawnRaider committed '{enemy.gameObject.name}' at {enemy.gameObject.transform.position}.");
            return enemy;
        }

        // TGVRU-V (verify): a freshly-built raider can render==false (no enabled mesh) or its
        // NavMeshAgent can be OFF the baked navmesh — both are "spawned but broken" states that
        // used to pass silently. Trace the exact counts so a capture splits "invisible" vs
        // "off-mesh" with zero guessing. Non-fatal (the raider is already committed). Null-safe.
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
                FlowTrace.Warn(system, $"VerifySpawnedMob: '{label}' has 0 enabled renderer(s) — spawned but INVISIBLE (check skin build).");
            if (!hasAgent)
                FlowTrace.Warn(system, $"VerifySpawnedMob: '{label}' has no NavMeshAgent — it will not path (check EnemyFactory).");
            else if (!onMesh)
                FlowTrace.Warn(system, $"VerifySpawnedMob: '{label}' agent is OFF the navmesh at {go.transform.position} — it will idle (point off baked surface).");

            FlowTrace.Step(system, $"VerifySpawnedMob '{label}': enabledRenderers={enabledRenderers} hasAgent={hasAgent} onNavMesh={onMesh}.");
        }

        // Threat-scaled stat block (ThreatLevel = danger tier × depth). Deadlier
        // region ⇒ stronger raiders ⇒ more defence required (danger ⇄ reward).
        // WO-155: when a region-roster id is supplied, the raider takes that roster
        // body's name + archetype (Wildlands Caveman, Orc Raider, etc.) so a tribe
        // reads as its region's faction; otherwise it's the generic wandering raider.
        private static EnemyDef BuildRaiderDef(string tribeId, int threat, string rosterId = null)
        {
            float scale = 1f + 0.12f * Mathf.Max(0, threat);

            string name = "Wandering Raider"; string ai = "walker";
            float hp = 45f, dmg = 8f, height = 1.8f; int xp = 14;
            switch (rosterId)
            {
                case "orc-raider":       name = "Orc Raider";        ai = "charger";    hp = 60f; dmg = 11f; height = 2.0f; xp = 20; break;
                case "caveman":          name = "Wildlands Caveman"; ai = "walker";     hp = 50f; dmg = 9f;  height = 1.9f; xp = 16; break;
                case "feral-wolf":       name = "Feral Wolf";        ai = "skirmisher"; hp = 36f; dmg = 7f;  height = 1.2f; xp = 12; break;
                case "tiefling-cultist": name = "Tiefling Cultist";  ai = "skirmisher"; hp = 55f; dmg = 10f; height = 1.9f; xp = 18; break;
                case "necromancer":      name = "Wound Necromancer"; ai = "walker";     hp = 90f; dmg = 13f; height = 2.1f; xp = 28; break;
            }

            return new EnemyDef
            {
                Id = string.IsNullOrEmpty(rosterId) ? $"raider-{tribeId}" : rosterId,
                Name = name,
                DisplayName = name,
                Ai = ai,
                Hp = hp * scale,
                MoveSpeed = 3.2f,
                ContactDamage = dmg * scale,
                AttackInterval = 1.2f,
                Height = height,
                AggroRadius = 14f,
                XpReward = xp + threat,
                GlimmerReward = 3,
            };
        }

        // Each check, point active raiders at the nearest standing settlement so an
        // undefended claim is raided. Settlements implement IDamageableStructure, so
        // Enemy's contact attack damages them with no extra code.
        private void RetargetActiveMembers()
        {
            if (_live.Count == 0) return;
            foreach (var kv in _live)
            {
                var members = kv.Value;
                if (members == null) continue;
                for (int i = 0; i < members.Count; i++)
                {
                    var e = members[i];
                    if (e == null || e.IsDead) continue;
                    var target = NearestSettlementTransform(e.transform.position);
                    e.SetBrainTarget(target);   // null → falls back to Heart-march
                }
            }
        }

        private Transform NearestSettlementTransform(Vector3 from)
        {
            var all = Settlement.All;
            Transform best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < all.Count; i++)
            {
                var s = all[i];
                if (s == null || !s.IsAlive) continue;
                float d = (s.transform.position - from).sqrMagnitude;
                if (d < bestSqr) { bestSqr = d; best = s.transform; }
            }
            return best;
        }

        // ── Seeding — author a few tribes per region from the zone graph ────

        private void EnsureSeededTribes()
        {
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            if (state == null) return;
            if (state.Tribes == null) state.Tribes = new List<TribeState>();
            if (state.Tribes.Count > 0) return;   // already seeded / loaded — don't duplicate

            foreach (var def in DefaultTribeDefs())
                state.Tribes.Add(new TribeState(def));

            Debug.Log($"[TribeManager] Seeded {state.Tribes.Count} wandering tribes across the regions.");
        }

        // Cache the def list so DefFor() lookups don't rebuild it each check.
        private List<TribeDef> _defs;

        private List<TribeDef> Defs()
        {
            if (_defs == null) _defs = DefaultTribeDefs();
            return _defs;
        }

        private TribeDef DefFor(string id)
        {
            var defs = Defs();
            for (int i = 0; i < defs.Count; i++)
                if (defs[i].Id == id) return defs[i];
            return null;
        }

        // 2 tribes per outer region (owner default 2–3), anchored in-region near the
        // node fields. Raid-size band scales with region danger (Goldfields small,
        // Ashwood big) — the data-driven swing the player can't perfectly solve.
        private static List<TribeDef> DefaultTribeDefs()
        {
            var list = new List<TribeDef>();

            // (region, anchorA, anchorB, bandMin, bandMax)
            AddPair(list, RegionId.Goldfields, new WorldPoint(78f, 0f, 6f),  new WorldPoint(86f, 0f, -12f), 2, 4);
            AddPair(list, RegionId.Stoneback,  new WorldPoint(-76f, 0f, 8f), new WorldPoint(-86f, 0f, -8f), 3, 6);
            AddPair(list, RegionId.Mirewood,   new WorldPoint(8f, 0f, -80f), new WorldPoint(-16f, 0f, -88f), 5, 9);
            AddPair(list, RegionId.Ashwood,    new WorldPoint(6f, 0f, 82f),  new WorldPoint(-14f, 0f, 90f),  8, 14);

            return list;
        }

        private static void AddPair(List<TribeDef> list, RegionId region,
            WorldPoint a, WorldPoint b, int min, int max)
        {
            string r = region.ToString().ToLowerInvariant();
            list.Add(new TribeDef($"{r}-1", region, a, min, max));
            list.Add(new TribeDef($"{r}-2", region, b, min, max));
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private List<TribeState> Tribes()
        {
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            return state != null ? state.Tribes : null;
        }

        private static Vector3 AnchorPos(WorldPoint p) => new Vector3(p.x, p.y, p.z);

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
