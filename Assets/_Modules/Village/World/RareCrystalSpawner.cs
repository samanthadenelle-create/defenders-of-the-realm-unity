// =============================================================================
// RareCrystalSpawner (WO-154 / DEF-190) — timed, region-gated RARE crystal spawns.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// The OCCASIONAL-BONUS half of the crystal economy. CrystalMineNode (WO-153) is the
// RELIABLE, persistent faucet you build and upgrade; this is its counterpoint: a
// rare, fleeting bonus vein that BLOOMS somewhere in the outer world, can be
// harvested for a richer-than-usual crystal payout, then WITHERS (despawns) after a
// window — whether or not anyone reached it. The fantasy: "a crystal bloom appeared
// out in Ashwood — get there before it fades."
//
// RECONCILIATION (no parallel node / economy / spawn system — CLAUDE.md §9):
//   • Reuses MineNode (WO-142) as the harvestable. The spawned node is a one-shot
//     crystal vein: Resource=AetherCrystal, a single rich Extract, NO respawn. It
//     banks through MineNode's EXISTING GameState path (Extract -> BankYield ->
//     GameState.AetherCrystals) — this spawner NEVER writes the wallet directly
//     (the "award crystals via GameState / node path" rule; Core can't ref Village).
//   • Reuses ZoneManager (DangerTierAt / GetZone / ThreatLevel) + CrystalRegion
//     (TopGradeFor) for the SAME danger=reward dial CrystalMineNode reads — deadlier
//     region => rarer to spawn there, but a richer payout + higher grade when it does.
//   • Self-bootstrapping DDOL (mirrors RegionMobSpawner / TribeManager): no scene
//     edit, no prefab/SO, no bake. Fires the moment a NavMesh + Player exist.
//
// DESPAWN: a bloom lives for BloomLifetime seconds OR until harvested (MineNode goes
// IsDepleted after its single extract), whichever comes first — then the spawner
// destroys it. No leak: blooms are tracked and culled if the node vanishes for any
// other reason too.
//
// TIMING: interval-based off Time, jittered per-cycle (no Random.Range seeding games
// that desync) so blooms don't appear on a metronome. Region is rolled per attempt
// against a danger-weighted rarity so the rich grades genuinely feel rare.
// =============================================================================
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using DeNelle.Core.World;

namespace DeNelle.Village
{
    /// <summary>
    /// Periodically spawns a temporary, region-gated RARE crystal node (a "bloom")
    /// at a valid outer-world position near the player, which banks a rich crystal
    /// payout via the existing <see cref="MineNode"/> path, then despawns after a
    /// window or once harvested. One self-bootstrapping DDOL singleton — no scene
    /// wiring, no bake. Companion to the reliable <see cref="CrystalMineNode"/> faucet.
    /// </summary>
    public sealed class RareCrystalSpawner : MonoBehaviour
    {
        public static RareCrystalSpawner Instance { get; private set; }

        [Header("Cadence")]
        [Tooltip("Average seconds between bloom-spawn ATTEMPTS. An attempt may still fail " +
                 "(player in the safe Village, no NavMesh point, danger roll missed) so the " +
                 "observed gap between actual blooms is a bit longer than this.")]
        [Min(15f)] public float AttemptInterval = 150f;

        [Tooltip("Random +/- jitter (seconds) applied to each interval so blooms never appear " +
                 "on a fixed metronome.")]
        [Min(0f)] public float IntervalJitter = 45f;

        [Tooltip("Hard cap on concurrent live blooms. Keeps the world from filling with bonus " +
                 "nodes if the player ignores them.")]
        [Min(1)] public int MaxConcurrentBlooms = 2;

        [Header("Lifetime")]
        [Tooltip("Seconds a bloom persists before it withers and despawns if left unharvested. " +
                 "Long enough to travel to it from across a region, short enough to feel fleeting.")]
        [Min(10f)] public float BloomLifetime = 90f;

        [Header("Placement (world units, relative to the player)")]
        [Tooltip("Inner radius of the ring around the player a bloom may appear in (don't pop " +
                 "it in their lap — it should be a short trek).")]
        [Min(4f)] public float SpawnRingInner = 30f;
        [Tooltip("Outer radius of the ring around the player a bloom may appear in.")]
        [Min(8f)] public float SpawnRingOuter = 90f;

        [Header("Payout")]
        [Tooltip("Base crystals a bloom yields on harvest BEFORE the region danger bonus " +
                 "(MineNode applies +25% per danger tier). Richer than a routine mine extract — " +
                 "this is the occasional jackpot.")]
        [Min(1)] public int BasePayout = 40;

        [Tooltip("Extra base crystals added PER danger tier of the region the bloom spawns in " +
                 "(on top of MineNode's own per-tier multiplier) so the deadly-edge bloom is " +
                 "the real prize.")]
        [Min(0)] public int PayoutPerTier = 20;

        [Tooltip("How close the player must be to harvest the bloom (passed to the MineNode).")]
        [Min(1f)] public float HarvestRadius = 3.5f;

        // Visual tints by grade so a bloom reads its rarity at a glance (placeholder art).
        private static readonly Color[] GradeTints =
        {
            new Color(0.55f, 0.85f, 1.00f),  // Aether  — pale blue
            new Color(0.45f, 1.00f, 0.60f),  // Verdant — green
            new Color(0.80f, 0.40f, 1.00f),  // Mire    — violet
            new Color(1.00f, 0.35f, 0.55f),  // Wraith  — crimson (rarest)
        };

        private float _nextAttempt;
        private Transform _player;
        private Transform _root;
        private int _counter;

        // One live bloom + its wither deadline.
        private sealed class Bloom
        {
            public MineNode Node;
            public GameObject Go;
            public float WitherAt;     // Time.time at which an unharvested bloom despawns
            public bool Harvested;     // latched once the node reports depleted
        }

        private readonly List<Bloom> _live = new List<Bloom>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            new GameObject("RareCrystalSpawner").AddComponent<RareCrystalSpawner>();
        }

        private void Awake()
        {
            // Destroy(this), not the host — defensive against a shared GameObject
            // (CLAUDE.md singleton-dedup memory).
            if (Instance != null && Instance != this) { Destroy(this); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            _nextAttempt = Time.time + NextGap();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Update()
        {
            // Cull / harvest-despawn live blooms every frame (cheap — at most MaxConcurrentBlooms).
            PruneBlooms();

            if (Time.time < _nextAttempt) return;
            _nextAttempt = Time.time + NextGap();

            TrySpawnBloom();
        }

        private float NextGap()
        {
            float jitter = IntervalJitter > 0f ? Random.Range(-IntervalJitter, IntervalJitter) : 0f;
            return Mathf.Max(5f, AttemptInterval + jitter);
        }

        // ── Lifetime: wither unharvested blooms + clean up harvested / vanished ones ──
        private void PruneBlooms()
        {
            for (int i = _live.Count - 1; i >= 0; i--)
            {
                var b = _live[i];
                if (b == null || b.Node == null || b.Go == null)
                {
                    // Node vanished by some other path — drop the record.
                    _live.RemoveAt(i);
                    continue;
                }

                // Harvested: MineNode banks the payout via its own Extract path, then goes
                // IsDepleted (single extract, no respawn). We despawn the spent bloom.
                if (b.Node.IsDepleted)
                {
                    if (!b.Harvested)
                    {
                        b.Harvested = true;
                        Debug.Log("[RareCrystalSpawner] Crystal bloom harvested — withering.");
                    }
                    Despawn(b);
                    _live.RemoveAt(i);
                    continue;
                }

                // Withered unharvested: lifetime elapsed.
                if (Time.time >= b.WitherAt)
                {
                    Debug.Log("[RareCrystalSpawner] Crystal bloom faded unharvested.");
                    Despawn(b);
                    _live.RemoveAt(i);
                }
            }
        }

        // ── Spawn one rare bloom near the player, region-gated by danger ──────────────
        private void TrySpawnBloom()
        {
            if (_live.Count >= MaxConcurrentBlooms) return;

            ResolvePlayer();
            if (_player == null) return;

            // No-op in the safe home zone — blooms are an OUTER-world event.
            Vector3 pp = _player.position;
            if (ZoneManager.GetZone(pp) == RegionId.Village)
                return;

            if (!TryFindSpawnPoint(pp, out Vector3 pos))
                return;

            int tier = Mathf.Max(0, ZoneManager.DangerTierAt(pos));
            if (tier <= 0) return;   // landed back inside the Village footprint — skip.

            // Danger-weighted RARITY: the deadlier the region, the LESS often a bloom
            // actually forms there (but the richer it is when it does). Roll a deterministic-
            // ish gate from Time + tier so we don't spawn one every attempt at the deep edge.
            // P(spawn) falls from ~1.0 at tier 1 to ~0.4 at tier 4.
            float spawnChance = Mathf.Clamp01(1f - 0.18f * (tier - 1));
            if (Random.value > spawnChance)
            {
                Debug.Log($"[RareCrystalSpawner] Bloom gate missed in danger tier {tier} (rare edge).");
                return;
            }

            CrystalGrade grade = CrystalRegion.TopGradeFor(tier);
            int payout = BasePayout + PayoutPerTier * tier;   // MineNode adds its own per-tier %.

            var bloom = BuildBloom(pos, tier, grade, payout);
            if (bloom != null)
            {
                _live.Add(bloom);
                Debug.Log($"[RareCrystalSpawner] Crystal bloom ({grade}, ~{payout}+ crystals) " +
                          $"appeared in danger tier {tier} — lasts {BloomLifetime:0}s.");
            }
        }

        // Build a one-shot crystal MineNode "bloom" — code-built primitive marker so it
        // needs no art/prefab, harvested with the SAME [F] verb + banking path as any mine.
        private Bloom BuildBloom(Vector3 pos, int tier, CrystalGrade grade, int payout)
        {
            if (_root == null) _root = new GameObject("[RareCrystalBlooms]").transform;

            var go = new GameObject($"CrystalBloom-{grade}-{_counter++}");
            go.transform.SetParent(_root, false);
            go.transform.position = pos;

            // A small floating crystal cluster (placeholder visual — readable by grade tint).
            var crystal = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crystal.name = "Crystal";
            // Drop the auto box collider — the MineNode uses a radius check, not trigger.
            var autoCol = crystal.GetComponent<Collider>();
            if (autoCol != null) Destroy(autoCol);
            crystal.transform.SetParent(go.transform, false);
            crystal.transform.localPosition = new Vector3(0f, 1.0f, 0f);
            crystal.transform.localScale = new Vector3(0.6f, 1.4f, 0.6f);
            crystal.transform.localRotation = Quaternion.Euler(20f, 35f, 12f);
            ApplyCrystalTint(crystal.GetComponent<Renderer>(), grade);

            // The harvestable. One rich extract, NO respawn — a fleeting bloom. Banking
            // flows through MineNode.Extract -> BankYield -> GameState.AetherCrystals
            // (the single, existing wallet path; this spawner never touches the wallet).
            var node = go.AddComponent<MineNode>();
            node.Resource         = MineResource.AetherCrystal;
            node.YieldPerExtract  = Mathf.Max(1, payout);   // MineNode applies the +25%/tier on top.
            node.ExtractCooldown  = 0f;
            node.TotalExtracts    = 1;     // single jackpot extract
            node.RespawnSeconds   = 0f;    // never respawns — it's a one-shot bloom
            node.UseFiniteReserve = false; // legacy [F]-harvest path (player taps it)
            node.InteractRadius   = HarvestRadius;

            return new Bloom
            {
                Node = node,
                Go = go,
                WitherAt = Time.time + BloomLifetime,
                Harvested = false,
            };
        }

        private static void ApplyCrystalTint(Renderer renderer, CrystalGrade grade)
        {
            if (renderer == null) return;
            int idx = Mathf.Clamp((int)grade, 0, GradeTints.Length - 1);
            Color colour = GradeTints[idx];

            Shader s = Shader.Find("Universal Render Pipeline/Lit")
                       ?? Shader.Find("Universal Render Pipeline/Unlit")
                       ?? Shader.Find("Standard");
            if (s == null) return;
            var mat = new Material(s);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colour);
            if (mat.HasProperty("_Color"))     mat.SetColor("_Color",     colour);
            // A faint emissive glow so a bloom catches the eye across a region.
            if (mat.HasProperty("_EmissionColor"))
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", colour * 0.6f);
            }
            renderer.sharedMaterial = mat;
        }

        // ── Placement helper: a NavMesh-valid point in the ring around the player ─────
        private bool TryFindSpawnPoint(Vector3 playerPos, out Vector3 pos)
        {
            for (int attempt = 0; attempt < 8; attempt++)
            {
                float ang = Random.value * Mathf.PI * 2f;
                float rad = Random.Range(SpawnRingInner, SpawnRingOuter);
                Vector3 want = playerPos + new Vector3(Mathf.Cos(ang) * rad, 0f, Mathf.Sin(ang) * rad);
                if (NavMesh.SamplePosition(want, out NavMeshHit hit, 10f, NavMesh.AllAreas))
                {
                    pos = hit.position;
                    return true;
                }
            }
            pos = playerPos;
            return false;
        }

        private static void Despawn(Bloom bloom)
        {
            if (bloom?.Go != null) Destroy(bloom.Go);
        }

        private void ResolvePlayer()
        {
            if (_player != null) return;
            var p = GameObject.FindWithTag("Player");
            _player = p != null ? p.transform : null;
        }
    }
}
