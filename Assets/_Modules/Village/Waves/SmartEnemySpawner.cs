// =============================================================================
// SmartEnemySpawner — tactical, role-positioned wave spawning (WO-362).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHAT IT DOES
//   Takes an EnemyWaveComposition (from WaveCompositionBuilder) and a chosen
//   gate, builds every enemy via the shared EnemyFactory, and POSITIONS each by
//   its SpawnRole relative to the gate→Heart approach line:
//
//     FrontTank → front-centre, leading the push.
//     Melee     → mid, spread ~2 m laterally.
//     Archer    → backline, pushed AWAY from the hero/Heart, spread laterally.
//     Weak      → back / sides, trailing.
//     Elite     → single, dead-centre on the approach line.
//
//   GATE ROTATION: a wave is released from ONE gate, and the gate cycles
//   N → E → S → W → N… across waves so the player must defend every gate, not
//   just the north one. WaveManager passes the wave number; the spawner picks
//   `gateIndex = (waveId - 1) % gateCount` against the available WaveSpawnPoints.
//
// WEBGL-SAFE / NO PER-FRAME ALLOC
//   Spawning happens once per wave (not per frame). The only allocation in the
//   spawn path is the returned List<Enemy> the caller needs to track lifecycle —
//   no LINQ, no closures, no boxing on the hot path. Positions are computed with
//   plain vector math. NavMesh-snapped exactly like EnemyFactory/EnemyGroupSpawner.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// WO-362: positions a composed wave tactically by role and rotates which
    /// gate the wave attacks from. Stateless beyond the gate cursor — call
    /// <see cref="SpawnWave"/> when a wave starts.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SmartEnemySpawner : MonoBehaviour
    {
        // Tactical spacing (world units). Tuned to read as a loose formation, not
        // a pile, while staying inside a typical approach-lane width.
        private const float FrontDepth   = 2.5f;   // tanks ahead of the line, toward the gate
        private const float MeleeDepth   = 0f;     // mid line
        private const float ArcherDepth  = -3.5f;  // backline, away from the hero/Heart
        private const float WeakDepth    = -5f;    // trailing fodder
        private const float LateralStep  = 2f;     // ~2 m horizontal spread between siblings
        private const float NavSnap      = 8f;     // NavMesh sample radius

        /// <summary>
        /// Spawns <paramref name="composition"/> at the gate chosen by rotating on
        /// <paramref name="waveId"/>, positioning each enemy by its SpawnRole.
        /// Returns every spawned <see cref="Enemy"/> so the caller can add them to
        /// its live roster and subscribe to Died / ReachedHeart. The caller owns
        /// wave-scaling + event hooks (parity with the legacy SpawnOne path).
        /// </summary>
        /// <param name="composition">The role-tagged slots to spawn.</param>
        /// <param name="catalog">Resolves each slot's enemy id → EnemyDef stats.</param>
        /// <param name="spawnPoints">Available gate spawn markers (gate rotates over these).</param>
        /// <param name="heart">The Heart transform enemies march toward.</param>
        /// <param name="enemyRoot">Parent transform for spawned enemies (may be null).</param>
        /// <param name="waveId">Wave number — drives gate rotation + instance ids.</param>
        /// <param name="instanceCounter">Shared by-ref unique-id counter.</param>
        public List<Enemy> SpawnWave(
            EnemyWaveComposition composition,
            EnemyCatalog         catalog,
            IReadOnlyList<WaveSpawnPoint> spawnPoints,
            Transform            heart,
            Transform            enemyRoot,
            int                  waveId,
            ref int              instanceCounter)
        {
            var spawned = new List<Enemy>();
            if (composition == null || composition.Entries.Count == 0 || catalog == null)
                return spawned;

            // ── Gate rotation: N → E → S → W → … across waves ─────────────────
            WaveSpawnPoint gate = PickGate(spawnPoints, waveId);
            if (gate == null)
            {
                Debug.LogError("[SmartEnemySpawner] No WaveSpawnPoint available — cannot spawn wave.");
                return spawned;
            }

            // Approach basis: heading runs from the gate toward the Heart; lateral
            // is perpendicular in the ground plane. Enemies are placed in this frame
            // so "front" means toward the Heart and "back" means out past the gate.
            Vector3 origin  = gate.transform.position;
            Vector3 heading = gate.HeadingToGate;            // gate → its gate (toward the city)
            if (heart != null)
            {
                Vector3 toHeart = heart.position - origin;
                toHeart.y = 0f;
                if (toHeart.sqrMagnitude > 0.0001f) heading = toHeart.normalized;
            }
            Vector3 lateral = Vector3.Cross(Vector3.up, heading);

            // Per-role running lateral index so siblings of the same role fan out
            // symmetrically (0, +1, -1, +2, -2 …) instead of stacking.
            int frontIdx = 0, meleeIdx = 0, archerIdx = 0, weakIdx = 0;

            for (int e = 0; e < composition.Entries.Count; e++)
            {
                WaveCompositionEntry entry = composition.Entries[e];
                EnemyDef def = catalog.Find(entry.EnemyId);
                if (def == null)
                {
                    FlowTrace.Fail("Enemy", $"SmartSpawner: unknown enemy id '{entry.EnemyId}' in wave {waveId} — slot SKIPPED (no body spawns)");
                    Debug.LogWarning($"[SmartEnemySpawner] Unknown enemy id '{entry.EnemyId}' in wave {waveId} — slot skipped.");
                    continue;
                }

                // ROOT-CAUSE TRACE: id → model resolution per composed slot. If every
                // slot resolves to a Skeleton_* model, the wave reads as one family
                // regardless of how many distinct ids the composition carries.
                string slotModel = EnemyFactory.ModelForEnemy(def);
                FlowTrace.Step("Enemy",
                    $"SmartSpawner resolve: id='{entry.EnemyId}' family='{def.Family}' count={entry.Count} " +
                    $"-> model '{slotModel}' (pos={entry.SpawnRole} brainRole={entry.Role})");

                for (int i = 0; i < entry.Count; i++)
                {
                    Vector3 offset;
                    switch (entry.SpawnRole)
                    {
                        case SpawnRole.Elite:
                            // Single, dead-centre on the approach line.
                            offset = heading * FrontDepth * 0.5f;
                            break;
                        case SpawnRole.FrontTank:
                            offset = heading * FrontDepth + lateral * Fan(ref frontIdx);
                            break;
                        case SpawnRole.Melee:
                            offset = heading * MeleeDepth + lateral * Fan(ref meleeIdx);
                            break;
                        case SpawnRole.Archer:
                            // Backline, away from the hero/Heart, wider lateral spread.
                            offset = heading * ArcherDepth + lateral * Fan(ref archerIdx) * 1.25f;
                            break;
                        case SpawnRole.Weak:
                        default:
                            // Back / sides, trailing, widest spread.
                            offset = heading * WeakDepth + lateral * Fan(ref weakIdx) * 1.5f;
                            break;
                    }

                    Vector3 rawPos = origin + offset;

                    // Snap onto the NavMesh so agents never start off-mesh.
                    Vector3 pos = rawPos;
                    if (NavMesh.SamplePosition(rawPos, out NavMeshHit hit, NavSnap, NavMesh.AllAreas))
                        pos = hit.position;

                    Vector3 toHeartDir = heart != null ? (heart.position - pos) : heading;
                    toHeartDir.y = 0f;
                    Quaternion rot = toHeartDir.sqrMagnitude > 0.0001f
                        ? Quaternion.LookRotation(toHeartDir)
                        : Quaternion.LookRotation(heading);

                    // POOLED: reuse a dormant body of this model instead of building a
                    // fresh skinned GameObject every wave (per-spawn churn was the main
                    // GC / stray source). Keyed by model id so a reused skeleton is never
                    // handed out where an orc/troll was asked for.
                    Enemy enemy = EnemyPool.Get("model:" + EnemyFactory.ModelForEnemy(def),
                                                null, def, pos, rot, enemyRoot);
                    if (enemy == null) continue;

                    // ROOT-CAUSE TRACE: the actual GameObject that landed, once per model.
                    FlowTrace.Once("Enemy", $"first-spawn-{slotModel}",
                        $"instantiated '{enemy.gameObject.name}' for id '{def.Id}' (family '{def.Family}', model '{slotModel}')");

                    if (enemy.GetComponent<EnemyDamageable>() == null)
                        enemy.gameObject.AddComponent<EnemyDamageable>();

                    string instanceId = $"wave{waveId}-{def.Id}-{instanceCounter++}";
                    enemy.Configure(instanceId, def, heart);

                    // Stamp the EnemyBrain tactical role (add one if the factory body
                    // has none) so Tanks screen, Healers mend, DPS/Ranged focus-fire.
                    EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
                    if (brain == null) brain = enemy.gameObject.AddComponent<EnemyBrain>();
                    brain.Role = entry.Role;

                    spawned.Add(enemy);
                }
            }

            Debug.Log(
                $"[SmartEnemySpawner] Wave {waveId} — released {spawned.Count} enemies " +
                $"from gate '{gate.SpawnId}' ({gate.Direction}), {composition.Entries.Count} " +
                $"role slots{(composition.HasElite ? " incl. ELITE" : "")}.");

            return spawned;
        }

        /// <summary>
        /// Rotates the attacking gate across waves: N → E → S → W → N…. Prefers
        /// ordering the markers by their <see cref="WaveSpawnPoint.GateIndex"/>
        /// (0 N, 1 E, 2 S, 3 W) so the cardinal cycle is honoured regardless of the
        /// list order; falls back to list order when gate indices aren't set.
        /// </summary>
        private static WaveSpawnPoint PickGate(IReadOnlyList<WaveSpawnPoint> points, int waveId)
        {
            if (points == null || points.Count == 0) return null;

            // Find the point whose GateIndex == the rotation slot for this wave.
            // gateCount is the number of distinct cardinal gates present.
            int gateCount = 0;
            for (int i = 0; i < points.Count; i++)
                if (points[i] != null && points[i].GateIndex + 1 > gateCount)
                    gateCount = points[i].GateIndex + 1;
            if (gateCount <= 0) gateCount = points.Count;

            int slot = (Mathf.Max(1, waveId) - 1) % gateCount;

            for (int i = 0; i < points.Count; i++)
                if (points[i] != null && points[i].GateIndex == slot)
                    return points[i];

            // Fallback: rotate over the raw list order.
            int idx = (Mathf.Max(1, waveId) - 1) % points.Count;
            for (int step = 0; step < points.Count; step++)
            {
                WaveSpawnPoint p = points[(idx + step) % points.Count];
                if (p != null) return p;
            }
            return null;
        }

        /// <summary>
        /// Symmetric fan-out multiplier from a running index: 0, +1, -1, +2, -2…
        /// times <see cref="LateralStep"/>. Advances <paramref name="idx"/> by ref.
        /// </summary>
        private static float Fan(ref int idx)
        {
            int n = idx++;
            int rank = (n + 1) / 2;               // 0,1,1,2,2,3,3…
            float sign = (n % 2 == 0) ? 1f : -1f; // +,-,+,-…  (n=0 → +0)
            if (n == 0) { rank = 0; sign = 0f; }
            return sign * rank * LateralStep;
        }
    }
}
