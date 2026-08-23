// =============================================================================
// WardTetherService — the ward-tether reach authority + forgetting driver (WO-112).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The exploration spine of Rung 3. The Keeper cannot leave the Heart for long, so the
// first Keepers planted ward-stones along the four marches to carry the song outward.
// Relight a march's ward-stone and you may walk that far — exploration earned one stone
// at a time. This service:
//   • SEEDS the ward ladder (code-built WardStoneDefs — §5: 2 Goldfields, 3 each
//     Stoneback/Mirewood/Ashwood) and binds each to a persisted WardStoneState.
//   • BUILDS the in-field WardStone objects at runtime (no prefab/scene/bake).
//   • COMPUTES per-march reach (the furthest LIT ward on that march) via the pure
//     Core helper WardReach — marches are independent.
//   • DRIVES the "forgetting": stepping past the furthest lit ward gently desaturates
//     the HUD + quiets the Heart's voice, scaling with distance, fully reversible.
//     NEVER damage, death, a timer, or a hard wall (WO-112 is dread, not punishment).
//   • RUNS the relight interaction: proximity (~3m) + resource cost (rising with order,
//     paid from GameState) + a short hold-the-ward KINDLE trickle; survive → lit, claim
//     the node-ward's MineNode; retreat → cold, spend refunded.
//   • CLAIMS the node-ward's MineNode (WO-110/111 hook) by flipping the existing claim
//     gate (MineNode.SetWorkerClaim) — it does NOT duplicate node state.
//   • SURFACES the Arcane Tower "Wards of the Marches" readout via CoreServices.Hud.
//
// RECONCILIATION (no parallel systems, CLAUDE.md §9):
//   • Self-bootstrapping DDOL singleton (mirrors TribeManager / RegionMobSpawner) —
//     needs NO scene edit and fires NO bake. Works the moment OuterWorld loads over
//     Village (WorldSceneLoader) and a NavMesh exists.
//   • The kindle is a SMALL TIMED TRICKLE built on the SAME shared Enemy.Configure +
//     RegionSpawnTable path the other world spawners use — NOT a forked WaveManager and
//     NOT a new persistent spawner; it self-clears when the kindle ends.
//   • Reach/forgetting math is the pure Core WardReach (headless) — one source of truth.
//   • State persists in GameState.Wards (save owner wires the schema round-trip).
//   • HUD calls go through CoreServices.Hud with ?. — never a direct DeNelle.HUD ref.
//
// FOUNDATION vs DEFERRED CONTENT:
//   foundation (here) — the seam set: reach authority, forgetting driver, relight loop,
//   node-claim hook, save model, Arcane Tower readout, code-seeded ward ladder.
//   deferred — final art/VFX for the kindle + glow, the relight UI affordance widget,
//   Maeren's tutorial dialogue, and tuned per-march kindle rosters/sizes (left as data).
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using DeNelle.Core;
using DeNelle.Core.World;
using DeNelle.Core.State;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// The ward-tether reach authority + forgetting driver (WO-112). One self-bootstrapping
    /// DDOL singleton; seeds + builds the ward-stones, computes per-march reach, drives the
    /// gentle forgetting past the song's edge, runs the relight interaction, and persists
    /// lit state. No prefab/SO/scene wiring, no bake.
    /// </summary>
    public sealed class WardTetherService : MonoBehaviour
    {
        public static WardTetherService Instance { get; private set; }

        [Header("Relight")]
        [Tooltip("How close (m) the Keeper must be to a cold ward to relight it.")]
        [Min(0.5f)] public float RelightRadius = 3f;

        [Tooltip("Seconds the hold-the-ward KINDLE trickle lasts while the stone kindles.")]
        [Min(1f)] public float KindleSeconds = 18f;

        [Header("Tether evaluation")]
        [Tooltip("Seconds between forgetting re-evaluations (throttled — never per-frame heavy).")]
        [Min(0.05f)] public float EvaluateInterval = 0.2f;

        [Header("Forgetting onset (metres past the furthest lit ward)")]
        [Tooltip("Distance past reach at which the forgetting reaches FULL (level 1). The fray " +
                 "edge fades in over 0..this. Halved automatically on the Ashwood march (thin song).")]
        [Min(2f)] public float ForgettingFullDistance = 14f;

        private float _evalTimer;
        private Transform _player;
        private Transform _root;
        private bool _seeded;

        // Live ward objects by id (the built WardStones).
        private readonly Dictionary<string, WardStone> _wards = new Dictionary<string, WardStone>();

        // One in-flight kindle per ward id (so a retreat can refund + clean up).
        private sealed class Kindle
        {
            public WardStone Ward;
            public float EndsAt;
            public int CoinsPaid;
            public int CrystalsPaid;
            public readonly List<Enemy> Mobs = new List<Enemy>();
        }
        private readonly Dictionary<string, Kindle> _kindles = new Dictionary<string, Kindle>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("WardTetherService").AddComponent<WardTetherService>();
        }

        private void Awake()
        {
            // Destroy(this) not the host — may share a GameObject (CLAUDE.md memory).
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
            EnsureSeededAndBuilt();
            RefreshTowerReadout();
        }

        private void Update()
        {
            // Drive any in-flight kindles (timeout → light; retreat handled in TickKindle).
            TickKindles();

            _evalTimer -= Time.deltaTime;
            if (_evalTimer > 0f) return;
            _evalTimer = EvaluateInterval;

            ResolvePlayer();
            if (_player == null) return;

            EvaluateTether(_player.position);
        }

        // =====================================================================
        //  Seeding + building the ward ladder (§5)
        // =====================================================================

        private void EnsureSeededAndBuilt()
        {
            if (_seeded) return;
            _seeded = true;

            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            if (state == null)
            {
                Debug.LogWarning("[WardTetherService] GameState null — wards not built this session.");
                return;
            }
            if (state.Wards == null) state.Wards = new List<WardStoneState>();

            if (_root == null) _root = new GameObject("[WardStones]").transform;

            foreach (var def in DefaultWardDefs())
            {
                var record = FindOrCreateRecord(state.Wards, def);
                var ward = BuildWard(def, record);
                if (ward != null) _wards[def.Id] = ward;
            }

            Debug.Log($"[WardTetherService] Built {_wards.Count} ward-stones across the four marches " +
                      $"({CountLit()} already lit from the save).");
        }

        private static WardStoneState FindOrCreateRecord(List<WardStoneState> records, WardStoneDef def)
        {
            for (int i = 0; i < records.Count; i++)
                if (records[i] != null && records[i].Id == def.Id) return records[i];
            var fresh = new WardStoneState(def);
            records.Add(fresh);
            return fresh;
        }

        private WardStone BuildWard(WardStoneDef def, WardStoneState record)
        {
            using var _ = FlowTrace.Enter("Wards", $"BuildWard id='{def?.Id}'");
            if (def == null)
            {
                FlowTrace.Fail("Wards", "BuildWard: null def — skipping this ward (no NRE-abort of the ward ladder).");
                return null;
            }

            Vector3 pos = new Vector3(def.Position.x, def.Position.y, def.Position.z);
            // Seat on the navmesh if one exists (so the Keeper can actually reach it).
            if (NavMesh.SamplePosition(pos, out NavMeshHit hit, 6f, NavMesh.AllAreas))
                pos = hit.position;

            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"WardStone-{def.Id}";
            go.transform.SetParent(_root, false);
            go.transform.position = pos + Vector3.up * 1.0f;
            go.transform.localScale = new Vector3(0.8f, 2.0f, 0.8f);   // a standing stone

            // A non-blocking trigger so the Keeper can walk up to it.
            if (go.TryGetComponent(out Collider col)) col.isTrigger = true;

            // GUARD (TGVRU-G): Initialise builds the WardStone's visuals (material/shader/glow). If
            // it throws (e.g. a missing shader → grey-cube fallback path), the OLD code let the
            // exception bubble and abort EnsureSeededAndBuilt — every later ward in the ladder was
            // then never built. Guard it so one bad ward logs + is skipped, the ladder continues.
            var ward = go.AddComponent<WardStone>();
            if (!Guard.Try("Wards", $"WardStone.Initialise '{def.Id}'", () => ward.Initialise(def, record)))
            {
                FlowTrace.Fail("Wards", $"BuildWard: Initialise threw for ward '{def.Id}' — destroying the half-built stone, ladder continues.");
                Destroy(go);
                return null;
            }

            FlowTrace.Step("Wards", $"BuildWard: ward '{def.Id}' built at {go.transform.position} (lit={record != null && record.Lit}).");
            return ward;
        }

        // =====================================================================
        //  Reach authority (pure WardReach — marches independent)
        // =====================================================================

        /// <summary>The reach granted on <paramref name="region"/> right now (the furthest lit
        /// ward on that march, floored at the Heart's bare song).</summary>
        public float ReachForRegion(RegionId region)
        {
            var wards = Records();
            return WardReach.ReachForRegion(wards, region);
        }

        /// <summary>How far the Keeper may range given their bearing from the Heart (their
        /// current march's reach). Convenience for callers that have only a position.</summary>
        public float CurrentReach(Vector3 keeperPos) =>
            ReachForRegion(ZoneManager.GetZone(keeperPos));

        /// <summary>Recompute on a ward state change: persist (in-memory now), claim the
        /// node-ward's node if newly lit, refresh the Arcane Tower readout.</summary>
        public void OnWardStateChanged(WardStone ward)
        {
            if (ward != null && ward.IsLit && ward.Def != null &&
                !string.IsNullOrEmpty(ward.Def.UnlocksNodeId))
            {
                ClaimNode(ward.Def.UnlocksNodeId);
            }
            RefreshTowerReadout();
        }

        // =====================================================================
        //  The forgetting (gentle, reversible — never punishing)
        // =====================================================================

        /// <summary>
        /// Each tick: how far past the furthest lit ward is the Keeper? Map that distance to
        /// a 0..1 forgetting level and push it to the HUD (desaturate + quiet). Inside reach →
        /// 0 (fully warm) — the effect lifts the instant they step back. Ashwood's song is
        /// thinnest, so its onset distance is halved.
        /// </summary>
        public void EvaluateTether(Vector3 keeperPos)
        {
            var wards = Records();
            float past = WardReach.DistancePastReach(wards, keeperPos);   // <=0 inside reach

            float full = ForgettingFullDistance;
            if (ZoneManager.GetZone(keeperPos) == RegionId.Ashwood)
                full *= 0.5f;   // thinner song on the front-line march — the forgetting bites sooner

            float level = past <= 0f ? 0f : Mathf.Clamp01(past / Mathf.Max(0.01f, full));

            // Passive HUD only — no gameplay. Null-safe cross-module call.
            CoreServices.Hud?.SetForgettingLevel(level);
        }

        // =====================================================================
        //  Relight interaction (proximity + cost + kindle; refund on retreat)
        // =====================================================================

        /// <summary>
        /// Attempt to begin relighting the ward identified by <paramref name="wardId"/>. The
        /// Keeper must be within <see cref="RelightRadius"/> and afford the cost; on success
        /// the cost is deducted and a short KINDLE trickle starts. Returns false (no spend)
        /// when out of range, already lit, already kindling, or unaffordable.
        /// </summary>
        public bool TryBeginRelight(string wardId)
        {
            if (!_wards.TryGetValue(wardId, out var ward) || ward == null) return false;
            if (ward.IsLit) return false;
            if (_kindles.ContainsKey(wardId)) return false;   // already kindling

            ResolvePlayer();
            if (_player == null) return false;
            if ((ward.transform.position - _player.position).sqrMagnitude >
                RelightRadius * RelightRadius)
                return false;   // not close enough

            var (coins, crystals) = ward.Cost;
            if (!TrySpend(coins, crystals)) return false;   // can't afford → no kindle

            BeginKindle(ward, coins, crystals);
            return true;
        }

        /// <summary>Begin relighting the NEAREST cold ward within range (convenience for a
        /// single "relight" input). Returns the ward id begun, or null.</summary>
        public string TryBeginRelightNearest()
        {
            ResolvePlayer();
            if (_player == null) return null;
            WardStone best = null;
            float bestSqr = RelightRadius * RelightRadius;
            foreach (var kv in _wards)
            {
                var w = kv.Value;
                if (w == null || w.IsLit || _kindles.ContainsKey(w.Id)) continue;
                float d = (w.transform.position - _player.position).sqrMagnitude;
                if (d <= bestSqr) { bestSqr = d; best = w; }
            }
            if (best == null) return null;
            return TryBeginRelight(best.Id) ? best.Id : null;
        }

        private void BeginKindle(WardStone ward, int coinsPaid, int crystalsPaid)
        {
            var kindle = new Kindle
            {
                Ward = ward,
                EndsAt = Time.time + KindleSeconds,
                CoinsPaid = coinsPaid,
                CrystalsPaid = crystalsPaid,
            };

            // A small, region-appropriate, order-scaled trickle from the SHARED Enemy path.
            int count = 2 + Mathf.Clamp(ward.Def != null ? ward.Def.Order : 1, 0, 3);
            int threat = ZoneManager.ThreatLevel(ward.transform.position);
            for (int i = 0; i < count; i++)
            {
                var e = SpawnKindleMob(ward, i, threat);
                if (e != null) kindle.Mobs.Add(e);
            }

            _kindles[ward.Id] = kindle;
            Debug.Log($"[WardTetherService] Kindle started at '{ward.Id}' " +
                      $"({count} defenders, {KindleSeconds:0}s) — survive to light the ward.");
        }

        private void TickKindles()
        {
            if (_kindles.Count == 0) return;
            ResolvePlayer();

            // Snapshot keys so we can mutate the dictionary while iterating.
            _kindleKeys.Clear();
            foreach (var k in _kindles.Keys) _kindleKeys.Add(k);

            for (int i = 0; i < _kindleKeys.Count; i++)
            {
                var id = _kindleKeys[i];
                if (!_kindles.TryGetValue(id, out var kindle) || kindle == null) continue;
                var ward = kindle.Ward;
                if (ward == null) { CleanupKindle(id, kindle, false); continue; }

                // Retreat check: if the Keeper leaves the ward's vicinity, the kindle fails
                // and the spend is refunded (owner default: refund, not escrow).
                bool retreated = _player == null ||
                    (ward.transform.position - _player.position).sqrMagnitude >
                    (RelightRadius * 4f) * (RelightRadius * 4f);
                if (retreated)
                {
                    Refund(kindle);
                    CleanupKindle(id, kindle, false);
                    Debug.Log($"[WardTetherService] Kindle at '{id}' ABANDONED — ward stays cold, spend refunded.");
                    continue;
                }

                // Survived to the end → the ward lights + claims its node.
                if (Time.time >= kindle.EndsAt)
                {
                    ward.SetLit(true);
                    if (ward.Def != null && !string.IsNullOrEmpty(ward.Def.LitFlavor))
                        Debug.Log($"[WardTetherService] {ward.Def.LitFlavor}");
                    CleanupKindle(id, kindle, true);
                }
            }
        }
        private readonly List<string> _kindleKeys = new List<string>();

        private void CleanupKindle(string id, Kindle kindle, bool _kept)
        {
            if (kindle != null)
                for (int i = 0; i < kindle.Mobs.Count; i++)
                    if (kindle.Mobs[i] != null) Destroy(kindle.Mobs[i].gameObject);
            _kindles.Remove(id);
        }

        private void Refund(Kindle kindle)
        {
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            if (state == null || kindle == null) return;
            var bal = state.Resources;
            bal.Coins += kindle.CoinsPaid;
            bal.Crystals += kindle.CrystalsPaid;   // crystals unified onto Resources.Crystals
            state.Resources = bal;
            GameStateService.Instance?.ResourcesChanged?.Invoke();
        }

        // A kindle defender — reuse the SAME Enemy + RegionSpawnTable path as the other
        // world spawners (no forked WaveManager). Capsule, NavMesh-seated, region-themed.
        private Enemy SpawnKindleMob(WardStone ward, int index, int threat)
        {
            using var _ = FlowTrace.Enter("Wards", $"SpawnKindleMob ward='{ward?.Id}' idx={index}");
            if (ward == null)
            {
                FlowTrace.Fail("Wards", "SpawnKindleMob: null ward — skipping this defender (no NRE-abort of the kindle).");
                return null;
            }
            Vector3 anchor = ward.transform.position;
            Vector3 want = anchor + new Vector3(Random.Range(-6f, 6f), 0f, Random.Range(-6f, 6f));
            if (NavMesh.SamplePosition(want, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                want = hit.position;

            RegionId region = ZoneManager.GetZone(want);
            string rosterId = RegionSpawnTable.HasRoster(region)
                ? RegionSpawnTable.PickEnemyId(region, ZoneManager.Depth(want), Random.value)
                : null;
            var def = BuildKindleDef(ward, rosterId, threat);

            // One skinned body via the shared EnemyFactory — no parallel spawn code (CLAUDE.md §9).
            // P0 NRE GUARD (TGVRU-R): Build can return null (def-null / internal throw). The OLD
            // code dereferenced enemy.gameObject.name immediately — a null NRE'd here and aborted
            // the WHOLE kindle spawn loop (BeginKindle's for-loop), leaving the relight with no
            // defenders. Guard the result, Fail-loud, return null so BeginKindle skips just this one.
            var enemy = Guard.Try("Wards", $"EnemyFactory.Build kindle {ward.Id}-{index}",
                () => EnemyFactory.Build(def, want, Quaternion.identity, _root), null);
            if (enemy == null || enemy.gameObject == null)
            {
                FlowTrace.Fail("Wards",
                    $"SpawnKindleMob: EnemyFactory.Build returned null for ward '{ward.Id}' defender {index} — skipping (kindle continues, no NRE-abort).");
                return null;
            }
            enemy.gameObject.name = $"KindleDefender ({ward.Id}-{index})";

            enemy.Configure($"kindle-{ward.Id}-{index}", def, ResolveHeart());
            // March the kindle mob at the player (the ritual they defend), not the Heart.
            ResolvePlayer();
            if (_player != null) enemy.SetBrainTarget(_player);

            // V (TGVRU-V): prove the committed defender renders + has an agent on the navmesh.
            VerifySpawnedMob("Wards", enemy.gameObject, $"{ward.Id}-{index}");
            FlowTrace.Step("Wards", $"SpawnKindleMob committed '{enemy.gameObject.name}' at {enemy.gameObject.transform.position}.");
            return enemy;
        }

        // TGVRU-V (verify): a freshly-built kindle defender can render==false (no enabled mesh) or
        // its NavMeshAgent can be OFF the baked navmesh — both "spawned but broken" states that used
        // to pass silently. Trace the exact counts so a capture splits "invisible" vs "off-mesh".
        // Non-fatal (the defender is already committed). Null-safe throughout.
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

        private static EnemyDef BuildKindleDef(WardStone ward, string rosterId, int threat)
        {
            float scale = 1f + 0.10f * Mathf.Max(0, threat);
            string name = "Withering Echo"; string ai = "walker";
            float hp = 40f, dmg = 7f, height = 1.7f; int xp = 12;
            switch (rosterId)
            {
                case "orc-raider":       name = "Orc Raider";        ai = "charger";    hp = 55f; dmg = 10f; height = 2.0f; xp = 18; break;
                case "caveman":          name = "Wildlands Caveman"; ai = "walker";     hp = 48f; dmg = 8f;  height = 1.9f; xp = 15; break;
                case "feral-wolf":       name = "Feral Wolf";        ai = "skirmisher"; hp = 34f; dmg = 6f;  height = 1.2f; xp = 11; break;
                case "tiefling-cultist": name = "Tiefling Cultist";  ai = "skirmisher"; hp = 52f; dmg = 9f;  height = 1.9f; xp = 17; break;
                case "necromancer":      name = "Wound Necromancer"; ai = "walker";     hp = 85f; dmg = 12f; height = 2.1f; xp = 26; break;
            }
            return new EnemyDef
            {
                Id = string.IsNullOrEmpty(rosterId) ? "ward-echo" : rosterId,
                Name = name,
                DisplayName = name,
                Ai = ai,
                Hp = hp * scale,
                MoveSpeed = 3.1f,
                ContactDamage = dmg * scale,
                AttackInterval = 1.2f,
                Height = height,
                AggroRadius = 16f,
                XpReward = xp + threat
            };
        }

        // =====================================================================
        //  Node-claim hook (WO-110/111) — flip the EXISTING claim gate, no dup state
        // =====================================================================

        // The node-ward claims its MineNode by flipping MineNode's worker-claim gate
        // (the SAME gate Settlement uses). We do NOT duplicate node/harvest state — the
        // ward only marks the node claimed so a settlement/harvest may begin there. If the
        // mine is later razed, the ward stays lit (reach decoupled from harvest).
        private void ClaimNode(string nodeId)
        {
            if (string.IsNullOrEmpty(nodeId)) return;
#if UNITY_2023_1_OR_NEWER
            var nodes = Object.FindObjectsByType<MineNode>();
#else
            var nodes = Object.FindObjectsByType<MineNode>();
#endif
            for (int i = 0; i < nodes.Length; i++)
            {
                var n = nodes[i];
                if (n == null) continue;
                if (n.name == nodeId || n.name.Contains(nodeId))
                {
                    n.SetWorkerClaim(true);
                    Debug.Log($"[WardTetherService] Node-ward lit — claimed node '{n.name}' (harvest may begin).");
                    return;
                }
            }
            Debug.LogWarning($"[WardTetherService] Node-ward claim: no MineNode matching '{nodeId}' " +
                             "in the loaded world (OuterWorld not loaded yet?). Reach still granted.");
        }

        // =====================================================================
        //  Arcane Tower readout (passive — Tower reads the tether, never reverse)
        // =====================================================================

        private void RefreshTowerReadout()
        {
            int lit = CountLit();
            int total = _wards.Count;
            string summary = BuildReadoutSummary();
            CoreServices.Hud?.SetWardsReadout(lit, total, summary);
        }

        private string BuildReadoutSummary()
        {
            // Per-march "lit/total — reach" line set (the Wards of the Marches readout).
            var sb = new System.Text.StringBuilder();
            sb.Append("Wards of the Marches\n");
            foreach (var region in MarchOrder)
            {
                int lit = 0, total = 0;
                foreach (var kv in _wards)
                {
                    var w = kv.Value;
                    if (w == null || w.Region != region) continue;
                    total++;
                    if (w.IsLit) lit++;
                }
                if (total == 0) continue;
                float reach = ReachForRegion(region);
                sb.Append($"{ZoneManager.Regions[region].DisplayName}: {lit}/{total} lit · reach {reach:0}m\n");
            }
            return sb.ToString().TrimEnd();
        }

        private static readonly RegionId[] MarchOrder =
        {
            RegionId.Goldfields, RegionId.Stoneback, RegionId.Mirewood, RegionId.Ashwood,
        };

        // =====================================================================
        //  The ward ladder (§5) — code-seeded defs (data, freely tunable)
        // =====================================================================

        // 2 Goldfields (E), 3 each Stoneback (W) / Mirewood (S) / Ashwood (N). Positions
        // ride out from each cardinal gate matching ZoneManager's quadrant model + the
        // OuterWorld mine-node placement. Cost + reach rise with order; the FINAL ward on
        // each march is the node-ward (UnlocksNodeId) that claims that march's resource node.
        private static List<WardStoneDef> DefaultWardDefs()
        {
            var list = new List<WardStoneDef>();

            // Goldfields (E, +X) — tutorial march: 2 wards, cheapest, lightest.
            list.Add(new WardStoneDef("ward_goldfields_1", RegionId.Goldfields, 1,
                new WorldPoint(48f, 0f, 4f), 40f, 40, 0, "",
                "The first stone wakes. The road east remembers your name."));
            list.Add(new WardStoneDef("ward_goldfields_2", RegionId.Goldfields, 2,
                new WorldPoint(72f, 0f, 8f), 80f, 90, 1, "MineNode-Wood-Goldfields",
                "Goldfields hears the Heart again — the grain is yours to gather."));

            // Stoneback (W, -X) — mid-game: 3 wards.
            list.Add(new WardStoneDef("ward_stoneback_1", RegionId.Stoneback, 1,
                new WorldPoint(-38f, 0f, 6f), 38f, 60, 0));
            list.Add(new WardStoneDef("ward_stoneback_2", RegionId.Stoneback, 2,
                new WorldPoint(-62f, 0f, 4f), 64f, 120, 1));
            list.Add(new WardStoneDef("ward_stoneback_3", RegionId.Stoneback, 3,
                new WorldPoint(-84f, 0f, 8f), 88f, 200, 2, "MineNode-Iron-Stoneback",
                "Stoneback's old veins answer — the cold iron is reachable now."));

            // Mirewood (S, -Z) — heavy: 3 wards.
            list.Add(new WardStoneDef("ward_mirewood_1", RegionId.Mirewood, 1,
                new WorldPoint(6f, 0f, -38f), 38f, 70, 0));
            list.Add(new WardStoneDef("ward_mirewood_2", RegionId.Mirewood, 2,
                new WorldPoint(2f, 0f, -62f), 64f, 140, 2));
            list.Add(new WardStoneDef("ward_mirewood_3", RegionId.Mirewood, 3,
                new WorldPoint(10f, 0f, -84f), 88f, 240, 3, "MineNode-AetherCrystal-Mirewood",
                "The mire stills. For a breath, the drowned valley knows the song."));

            // Ashwood (N, +Z) — endgame, the forgetting march: 3 wards (thinnest song).
            list.Add(new WardStoneDef("ward_ashwood_1", RegionId.Ashwood, 1,
                new WorldPoint(4f, 0f, 38f), 38f, 90, 1));
            list.Add(new WardStoneDef("ward_ashwood_2", RegionId.Ashwood, 2,
                new WorldPoint(6f, 0f, 62f), 64f, 180, 2));
            list.Add(new WardStoneDef("ward_ashwood_3", RegionId.Ashwood, 3,
                new WorldPoint(8f, 0f, 84f), 88f, 320, 4, "MineNode-AetherCrystal-Ashwood",
                "At the last warden's stand, the dead wood flinches toward the light."));

            return list;
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private static List<WardStoneState> Records()
        {
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            return state != null ? state.Wards : null;
        }

        private int CountLit()
        {
            int n = 0;
            foreach (var kv in _wards)
                if (kv.Value != null && kv.Value.IsLit) n++;
            return n;
        }

        // Spend coins + crystals from GameState (same store CrystalMine/Settlement use).
        private static bool TrySpend(int coins, int crystals)
        {
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            if (state == null) return false;
            if (state.Resources.Coins < coins || state.Resources.Crystals < crystals) return false;
            var bal = state.Resources;
            bal.Coins -= coins;
            bal.Crystals -= crystals;   // crystals unified onto Resources.Crystals
            state.Resources = bal;
            GameStateService.Instance?.ResourcesChanged?.Invoke();
            return true;
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
