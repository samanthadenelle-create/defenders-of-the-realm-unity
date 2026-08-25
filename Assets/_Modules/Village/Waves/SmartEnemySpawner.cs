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
//   SIDE PARTITION (WO-1179) — a wave is released from ONE **or more** sides.
//   The escalation ladder is 1 -> 2 -> 4 attacking sides (SideCountForWave), and
//   the rotation base still cycles N -> E -> S -> W across waves so no single side
//   is the "wave side". At 2 sides the pair is deliberately OPPOSITE (N+S / E+W):
//   the owner's note is that the 2-side step matters because it is the first time
//   the player must choose what to leave undefended, and adjacent sides can both be
//   covered from one position, which would make the step a non-event.
//
//   ⛔ THE SPLIT LIVES *INSIDE* ONE SpawnWave CALL, AND THAT IS THE WHOLE POINT.
//   There is exactly ONE `budget` local per invocation and EVERY side draws from it,
//   so N sides can never put more bodies on the field than one side would have.
//   Calling SpawnWave once per side would hand EACH call the full budget and DOUBLE
//   the field, silently defeating the WO-1113 concurrency cap that exists because of
//   a measured phone frame-rate cliff. WaveManager remains the single spawn authority
//   and still makes exactly two calls (the wave + the reinforcement drain).
//
//   Reinforcements need no side bookkeeping: DrainSmartReinforcements re-calls with the
//   SAME waveId, so SideCountForWave / ResolveSides re-derive the identical side set and
//   held bodies return to the sides they belong to.
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
        /// Spawns <paramref name="composition"/> across the 1 / 2 / 4 sides this wave attacks
        /// from (<see cref="SideCountForWave"/>), positioning each enemy by its SpawnRole.
        /// Returns every spawned <see cref="Enemy"/> so the caller can add them to
        /// its live roster and subscribe to Died / ReachedHeart. The caller owns
        /// wave-scaling + event hooks (parity with the legacy SpawnOne path).
        ///
        /// ⛔ CALL THIS ONCE PER RELEASE, NEVER ONCE PER SIDE. The side split happens INSIDE,
        /// under the SINGLE <paramref name="maxToSpawn"/> budget every side draws from. Calling
        /// it per side would hand each call the FULL budget and double the field, defeating the
        /// WO-1113 concurrency cap that exists because of a measured phone frame-rate cliff.
        /// </summary>
        /// <param name="composition">The role-tagged slots to spawn, split across the active sides.</param>
        /// <param name="catalog">Resolves each slot's enemy id → EnemyDef stats.</param>
        /// <param name="spawnPoints">
        /// All WaveSpawnPoint markers, resolved BY COMPONENT by the caller
        /// (<c>FindObjectsByType&lt;WaveSpawnPoint&gt;()</c>) — ⛔ never by a "SpawnPoint" tag,
        /// which does not exist and THROWS when read. Sides are their distinct GateIndex values.
        /// </param>
        /// <param name="heart">The Heart transform enemies march toward.</param>
        /// <param name="enemyRoot">Parent transform for spawned enemies (may be null).</param>
        /// <param name="waveId">
        /// Wave number — drives the side-count ladder, the side rotation base, and instance ids.
        /// The reinforcement drain passes the SAME waveId, which is exactly why held bodies
        /// return to the same sides without carrying any per-side state.
        /// </param>
        /// <param name="instanceCounter">Shared by-ref unique-id counter.</param>
        /// <param name="maxToSpawn">
        /// WO-1113 — CONCURRENCY BUDGET for this call: at most this many bodies are released,
        /// and everything past it is written to <paramref name="deferred"/> for the caller to
        /// release later as slots free. 0 = unlimited (the pre-WO-1113 behaviour).
        /// The cap itself lives on WaveManager (`_maxSimultaneousEnemies`); this path only ever
        /// receives a budget, so there is exactly one place the number is authored.
        /// </param>
        /// <param name="deferred">
        /// Optional sink for the slots this call did NOT release (same ids, reduced counts).
        /// Null = drop the remainder, which would THIN the wave — callers that pass a budget
        /// must pass a sink and drain it, or the wave is silently short.
        /// </param>
        public List<Enemy> SpawnWave(
            EnemyWaveComposition composition,
            EnemyCatalog         catalog,
            IReadOnlyList<WaveSpawnPoint> spawnPoints,
            Transform            heart,
            Transform            enemyRoot,
            int                  waveId,
            ref int              instanceCounter,
            int                  maxToSpawn = 0,
            List<WaveCompositionEntry> deferred = null)
        {
            var spawned = new List<Enemy>();
            if (composition == null || composition.Entries.Count == 0 || catalog == null)
                return spawned;

            // Remaining release budget for THIS call. int.MaxValue == uncapped.
            int budget = maxToSpawn > 0 ? maxToSpawn : int.MaxValue;
            if (maxToSpawn > 0 && deferred == null)
                FlowTrace.Warn("Enemy",
                    $"SpawnWave: wave {waveId} got a spawn budget of {maxToSpawn} with NO deferred sink — " +
                    "everything over the budget would be DROPPED (wave thinned). Caller must pass a sink.");

            // ── WO-1179 SIDE PARTITION: how many sides attack this wave ───────
            // Markers resolve BY COMPONENT (the caller's FindObjectsByType<WaveSpawnPoint>);
            // ⛔ there is NO "SpawnPoint" tag — it is undeclared and reading it THROWS
            // (CLAUDE.md §7; that exact mistake is how WO-1038 shipped a feature whose every
            // scan died and which therefore never spawned anything).
            int desiredSides = SideCountForWave(waveId);
            var sides = new List<WaveSpawnPoint>();
            ResolveSides(spawnPoints, waveId, desiredSides, sides);

            if (sides.Count == 0)
            {
                FlowTrace.Fail("Enemy",
                    $"SpawnWave: wave {waveId} resolved ZERO attacking sides from " +
                    $"{(spawnPoints == null ? "a NULL marker list" : spawnPoints.Count + " marker(s)")} — " +
                    "NOTHING spawns this wave. Expect 20 markers (5 per side x 4 sides) from " +
                    "CastleSpawnPointInjector, which SELF-SUPPRESSES if any WaveSpawnPoint already exists.");
                Debug.LogError("[SmartEnemySpawner] No WaveSpawnPoint available — cannot spawn wave.");
                return spawned;
            }

            int sideCount = sides.Count;
            if (sideCount < desiredSides)
                FlowTrace.Warn("Enemy",
                    $"SpawnWave: wave {waveId} wanted {desiredSides} attacking side(s) but only {sideCount} " +
                    "distinct GateIndex value(s) exist among the markers — the escalation step is CAPPED BY " +
                    "THE SCENE, not by the ladder. The 1 -> 2 -> 4 progression cannot be read from tuning alone.");

            // Approach basis PER SIDE: heading runs from that side's marker toward the
            // Heart; lateral is perpendicular in the ground plane. Enemies are placed in
            // this frame so "front" means toward the Heart and "back" means out past the gate.
            var origins  = new Vector3[sideCount];
            var headings = new Vector3[sideCount];
            var laterals = new Vector3[sideCount];

            // Per-role, PER-SIDE running lateral index so siblings of the same role fan out
            // symmetrically (0, +1, -1, +2, -2 …) instead of stacking. Per-side because a
            // shared index would start side B's formation off-centre by side A's width.
            var frontIdx  = new int[sideCount];
            var meleeIdx  = new int[sideCount];
            var archerIdx = new int[sideCount];
            var weakIdx   = new int[sideCount];

            // MEASURED, not intended: planned = what the partition asked for; actual = bodies
            // that really landed (a pool miss / unknown id / NavMesh refusal reduces it).
            var plannedPerSide = new int[sideCount];
            var actualPerSide  = new int[sideCount];
            var heldPerSide    = new int[sideCount];

            // Risky object op: a marker destroyed between resolve and use would throw here and
            // abort the whole wave silently. One closure per WAVE (never per frame — see header).
            bool basisOk = Guard.Try("Enemy",
                $"SpawnWave wave {waveId}: build approach basis for {sideCount} side(s)",
                () =>
                {
                    for (int s = 0; s < sideCount; s++)
                    {
                        WaveSpawnPoint sp = sides[s];
                        Vector3 o = sp.transform.position;
                        Vector3 h = sp.HeadingToGate;      // marker → its gate (toward the city)
                        if (heart != null)
                        {
                            Vector3 toHeart = heart.position - o;
                            toHeart.y = 0f;
                            if (toHeart.sqrMagnitude > 0.0001f) h = toHeart.normalized;
                        }
                        origins[s]  = o;
                        headings[s] = h;
                        laterals[s] = Vector3.Cross(Vector3.up, h);
                    }
                });
            if (!basisOk)
            {
                FlowTrace.Fail("Enemy",
                    $"SpawnWave: wave {waveId} could not build the approach basis for its {sideCount} " +
                    "side(s) (see the Guard line above) — NOTHING spawns this wave.");
                return spawned;
            }

            FlowTrace.Step("Enemy",
                $"SmartSpawner wave {waveId}: ladder wants {desiredSides} side(s), resolved {sideCount} — " +
                $"{DescribeSides(sides)} (budget {(maxToSpawn > 0 ? maxToSpawn.ToString() : "uncapped")} " +
                "SHARED across every side, one SpawnWave call).");

            // Reused per entry so the partition allocates nothing inside the loop.
            var share = new int[sideCount];

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

                // WO-1179 — PARTITION BY ROLE, NOT BY BLOCK. This slot is ONE role, and every
                // active side gets a proportional slice of it, so each side is a COHERENT threat.
                // (Handing whole slots to whole sides would make one side all-archers — trivial —
                // and the other all-tanks — unfair.) Remainders go to the FIRST active sides by
                // ordinal, so a 3-across-2 split is stable run to run.
                PartitionCount(entry.Count, sideCount, share);

                int entryHeld = 0;
                for (int s = 0; s < sideCount; s++)
                {
                    int want = share[s];
                    if (want <= 0) continue;
                    plannedPerSide[s] += want;

                    // WO-1113 + WO-1179: release only what the ONE SHARED budget allows; the rest
                    // is HELD (not dropped) so the wave's total count — and therefore its authored
                    // difficulty — is unchanged. `budget` is a single local for the whole call, so
                    // the sides COMPETE for it rather than each receiving a fresh cap.
                    int release = ReleaseCountFor(want, budget);
                    budget -= release;
                    entryHeld     += want - release;
                    heldPerSide[s] += want - release;

                    Vector3 origin  = origins[s];
                    Vector3 heading = headings[s];
                    Vector3 lateral = laterals[s];
                    WaveSpawnPoint gate = sides[s];

                    for (int i = 0; i < release; i++)
                    {
                        Vector3 offset;
                        switch (entry.SpawnRole)
                        {
                            case SpawnRole.Elite:
                                // Single, dead-centre on the approach line.
                                offset = heading * FrontDepth * 0.5f;
                                break;
                            case SpawnRole.FrontTank:
                                offset = heading * FrontDepth + lateral * Fan(ref frontIdx[s]);
                                break;
                            case SpawnRole.Melee:
                                offset = heading * MeleeDepth + lateral * Fan(ref meleeIdx[s]);
                                break;
                            case SpawnRole.Archer:
                                // Backline, away from the hero/Heart, wider lateral spread.
                                offset = heading * ArcherDepth + lateral * Fan(ref archerIdx[s]) * 1.25f;
                                break;
                            case SpawnRole.Weak:
                            default:
                                // Back / sides, trailing, widest spread.
                                offset = heading * WeakDepth + lateral * Fan(ref weakIdx[s]) * 1.5f;
                                break;
                        }

                        Vector3 rawPos = origin + offset;

                        // Snap onto the NavMesh so agents never start off-mesh.
                        //
                        // WO-1113 — THE MISS IS NO LONGER SILENT. This branch used to leave `pos` at
                        // the RAW marker position on a SamplePosition miss and say nothing, while the
                        // legacy WaveManager.SpawnOne path (which the player does NOT meet, since
                        // _smartComposition routes here) had already been fixed for exactly this:
                        // WO-430 warns and ground-snaps. So the live path could strand a whole wave
                        // off-mesh — floating/sunken enemies that never move toward the Heart — and
                        // leave no evidence in the break-log at all. Same miss, same remedy, and via
                        // FlowTrace so F8 can actually see it (a bare Debug.LogWarning cannot).
                        Vector3 pos = rawPos;
                        if (NavMesh.SamplePosition(rawPos, out NavMeshHit hit, NavSnap, NavMesh.AllAreas))
                        {
                            pos = hit.position;
                        }
                        else
                        {
                            FlowTrace.Warn("Enemy",
                                $"SmartSpawner: NavMesh.SamplePosition MISS (no mesh within {NavSnap:0} m) for def " +
                                $"'{def.Id}' at gate '{gate.SpawnId}' (side {s + 1}/{sideCount}) wave {waveId} — " +
                                $"attemptedPos={rawPos}. Ground-snapping by raycast instead of keeping the raw Y " +
                                "(would float/sink). WARNING: a whole SIDE can be off-mesh while the others " +
                                "are fine - read the side index before blaming the wave.");

                            // Ground/terrain/default layers only (mirrors WaveManager.SpawnOne +
                            // Enemy.SnapBodyToGround) — excludes the enemy's own Enemy-layer collider.
                            int groundMask = LayerMask.GetMask("Default", "Terrain", "Ground");
                            if (groundMask == 0) groundMask = Physics.DefaultRaycastLayers;
                            Vector3 rayOrigin = new Vector3(rawPos.x, rawPos.y + 50f, rawPos.z);
                            if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit groundHit, 200f,
                                    groundMask, QueryTriggerInteraction.Ignore))
                            {
                                pos.y = groundHit.point.y;
                            }
                            else
                            {
                                FlowTrace.Warn("Enemy",
                                    $"SmartSpawner: ground-snap raycast ALSO missed at XZ=({rawPos.x:F1},{rawPos.z:F1}) " +
                                    $"for def '{def.Id}' — using the '{gate.Direction}' marker Y {origin.y:F1} as last resort.");
                                pos.y = origin.y;
                            }
                        }

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
                        if (enemy == null)
                        {
                            // R(eturn-fallback never silent): the pool gave back no body — this slot's
                            // enemy silently never spawns, so the smart wave is short one unit. Warn
                            // naming the def/model so the gap self-reports (skip-not-abort: rest spawn).
                            // The body is NOT re-credited to the budget: the slot is spent either way,
                            // and re-crediting would let a starved pool spin the shared budget.
                            FlowTrace.Warn("Enemy",
                                $"SpawnWave: EnemyPool.Get returned null for def '{def?.Id ?? "<null>"}' " +
                                $"(model '{slotModel}') gate '{gate.SpawnId}' (side {s + 1}/{sideCount}) " +
                                $"wave {waveId} — slot enemy NOT spawned.");
                            continue;
                        }

                        // ROOT-CAUSE TRACE: the actual GameObject that landed, once per model.
                        FlowTrace.Once("Enemy", $"first-spawn-{slotModel}",
                            $"instantiated '{enemy.gameObject.name}' for id '{def.Id}' (family '{def.Family}', model '{slotModel}')");

                        // WO-1113 — V(erify) parity with WaveManager.VerifySpawnedEnemy, which only
                        // ever guarded the LEGACY path. An off-mesh NavMeshAgent never moves, so the
                        // enemy is live-but-frozen and the wave stalls with nothing in the log naming
                        // why. Warn (skip-not-abort): the body still counts toward the wave.
                        VerifyOnNavMesh(enemy, def, pos, waveId);

                        if (enemy.GetComponent<EnemyDamageable>() == null)
                            enemy.gameObject.AddComponent<EnemyDamageable>();

                        string instanceId = $"wave{waveId}-{def.Id}-{instanceCounter++}";
                        enemy.Configure(instanceId, def, heart);

                        // Stamp the EnemyBrain tactical role (add one if the factory body
                        // has none) so Tanks screen, Healers mend, DPS/Ranged focus-fire.
                        EnemyBrain brain = enemy.GetComponent<EnemyBrain>();
                        if (brain == null) brain = enemy.gameObject.AddComponent<EnemyBrain>();
                        brain.Role = entry.Role;
                        // P0-4 (2026-08-02): this is THE live wave path and it stamped the Role but
                        // NEVER applied the matching tactics — so every smart-composed wave enemy ran
                        // with _tactics == null, i.e. the whole WO-145 tactical layer (kite / flank /
                        // siege / support, the scored target priority, the eval throttle) was DEAD in
                        // the one place the player actually meets it. Null tactics also dropped the
                        // brain onto the untuned legacy target chain. ApplyRoleTactics assigns the
                        // SHARED runtime archetype singletons (KiterTactics / CoordinatedFlanker /
                        // Siege / Support) — never mutate what it hands back: one instance is shared by
                        // every enemy of that archetype for the whole session, so a per-enemy tweak
                        // would leak to all of them. Use ScriptableObject.CreateInstance if a genuine
                        // per-enemy override is ever needed.
                        EnemyBrain.ApplyRoleTactics(brain, entry.Role);

                        spawned.Add(enemy);
                        actualPerSide[s]++;     // MEASURED: a body that really landed on this side.
                    }
                }

                if (entryHeld > 0)
                {
                    // One aggregated held entry per slot: the drain re-derives the SAME side set
                    // from the SAME waveId, so held bodies do not need to carry a side with them.
                    if (deferred != null)
                        deferred.Add(new WaveCompositionEntry(
                            entry.EnemyId, entryHeld, entry.SpawnRole, entry.Role));
                    FlowTrace.Step("Enemy",
                        $"SmartSpawner budget: wave {waveId} slot '{entry.EnemyId}' releases " +
                        $"{entry.Count - entryHeld}/{entry.Count} now across {sideCount} side(s), " +
                        $"{entryHeld} HELD for reinforcement (ONE shared concurrency budget).");
                }
            }

            int held = 0;
            if (deferred != null)
                for (int d = 0; d < deferred.Count; d++) held += deferred[d].Count;

            // ── MEASURED partition, not the intended one ───────────────────────
            // Every number below is counted from bodies that actually landed, so a side that
            // planned 4 and delivered 0 prints differently from one that delivered 4.
            int sidesWithBodies = 0;
            for (int s = 0; s < sideCount; s++) if (actualPerSide[s] > 0) sidesWithBodies++;

            Debug.Log(
                $"[SmartEnemySpawner] Wave {waveId} — released {spawned.Count} enemies across " +
                $"{sidesWithBodies}/{sideCount} side(s) [{DescribeMeasured(sides, actualPerSide, plannedPerSide)}], " +
                $"{composition.Entries.Count} role slots{(composition.HasElite ? " incl. ELITE" : "")}" +
                (held > 0 ? $", {held} HELD by the concurrency cap (released as reinforcements)." : "."));

            FlowTrace.Step("Enemy",
                $"SmartSpawner wave {waveId}: MEASURED partition actual/planned per side = " +
                $"{DescribeMeasured(sides, actualPerSide, plannedPerSide)} | releasedTotal={spawned.Count} " +
                $"held={held} sharedBudget={(maxToSpawn > 0 ? maxToSpawn.ToString() : "uncapped")} " +
                $"budgetLeft={(maxToSpawn > 0 ? budget.ToString() : "uncapped")} " +
                $"sidesWithBodies={sidesWithBodies}/{sideCount} (ladder wanted {desiredSides}).");

            // ⚠ THE LINE THAT GATES THE WHOLE FEATURE (WO-1179 §6 / the owner's own note):
            // if the shared cap binds before a later side gets a body, a "two-sided" wave arrives
            // as a ONE-SIDED wave and the escalation reads as WEAKER, not harder. This fires on the
            // measured counts, so it can only print when that has actually happened.
            if (sideCount > 1 && sidesWithBodies < sideCount)
            {
                var starved = new System.Text.StringBuilder();
                for (int s = 0; s < sideCount; s++)
                {
                    if (actualPerSide[s] > 0) continue;
                    if (starved.Length > 0) starved.Append(", ");
                    starved.Append(sides[s].Direction).Append(" (planned ").Append(plannedPerSide[s])
                           .Append(", held ").Append(heldPerSide[s]).Append(')');
                }
                FlowTrace.Warn("Enemy",
                    $"SmartSpawner wave {waveId}: MULTI-SIDE WAVE ARRIVED SINGLE-SIDED — " +
                    $"{sidesWithBodies} of {sideCount} side(s) got a body; starved: {starved}. " +
                    $"Shared budget was {(maxToSpawn > 0 ? maxToSpawn.ToString() : "uncapped")} for " +
                    $"{composition.TotalCount} enemies. The escalation step will FEEL WEAKER, not harder — " +
                    "raise the concurrency cap or lower the side count before tuning roster sizes.");
            }

            return spawned;
        }

        /// <summary>
        /// WO-1113 — the concurrency budget, as PURE arithmetic so an oracle can drive it
        /// without a scene. How many more bodies may be on the field right now:
        /// <paramref name="cap"/> 0 or less means "no cap" and returns 0, which every caller
        /// reads as UNLIMITED (the same 0-means-off convention the serialized field uses).
        /// </summary>
        public static int BudgetFor(int cap, int liveCount)
            => cap <= 0 ? 0 : Mathf.Max(0, cap - Mathf.Max(0, liveCount));

        /// <summary>
        /// WO-1113 — how many of a slot's <paramref name="slotCount"/> may be released into
        /// <paramref name="remainingBudget"/> free places. Pure; the remainder is what the
        /// caller must HOLD (never drop, or the cap silently becomes a wave thinner).
        /// </summary>
        public static int ReleaseCountFor(int slotCount, int remainingBudget)
            => Mathf.Clamp(remainingBudget, 0, Mathf.Max(0, slotCount));

        /// <summary>
        /// WO-1113: a just-released enemy whose NavMeshAgent is OFF the mesh will never move
        /// toward the Heart — the wave then stalls with a live, frozen body. Warn via FlowTrace
        /// (never Debug alone: the F8 break-log only captures FlowTrace) so a capture pinpoints
        /// which def / wave / position stranded, instead of showing a wave that "just stopped".
        /// </summary>
        private static void VerifyOnNavMesh(Enemy enemy, EnemyDef def, Vector3 pos, int waveId)
        {
            if (enemy == null) return;

            var agent = enemy.GetComponentInChildren<NavMeshAgent>();
            if (agent != null && agent.enabled && !agent.isOnNavMesh)
                FlowTrace.Warn("Enemy",
                    $"SmartSpawner: enemy '{def?.Id ?? "<null>"}' on '{enemy.gameObject.name}' (wave {waveId}) is OFF " +
                    $"the NavMesh at {pos} (agent.isOnNavMesh==false) — it will NOT move toward the Heart; " +
                    "the wave can stall on it.");
        }

        // ─────────────────────────────────────────────────────────────────────
        // WO-1179 — THE ESCALATION LADDER: 1 -> 2 -> 4 attacking sides.
        //
        // ⭐ The step that matters is 1 -> 2, not the roster sizes: it is the first
        // moment the player must CHOOSE WHAT TO LEAVE UNDEFENDED. Everything before it
        // is a bigger version of the same fight. Tune these two wave numbers, not the
        // counts, when the two-side moment lands too early or too late.
        //
        // These are code constants, not a tuning row, DELIBERATELY: waves.json's
        // `enemies[]` batches are INERT under `_smartComposition:1` and a re-add FAILS
        // WaveDataTest, so the schedule file is not a safe home for a new spawn knob.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>First wave that attacks from TWO sides (the difficulty step that matters).</summary>
        public const int TwoSideFromWave = 5;

        /// <summary>First wave that attacks from all FOUR sides.</summary>
        public const int FourSideFromWave = 10;

        /// <summary>
        /// How many sides attack on <paramref name="waveId"/>: 1, then 2, then 4.
        /// PURE, so a regression can drive the whole ladder with no scene. The result is
        /// a REQUEST — <see cref="ResolveSides"/> clamps it to the sides that actually exist.
        /// </summary>
        public static int SideCountForWave(int waveId)
        {
            int w = Mathf.Max(1, waveId);
            if (w >= FourSideFromWave) return 4;
            if (w >= TwoSideFromWave)  return 2;
            return 1;
        }

        /// <summary>
        /// Splits <paramref name="total"/> across <paramref name="sideCount"/> sides, writing the
        /// per-side share into <paramref name="into"/> (length must be >= sideCount).
        /// Remainders go to the FIRST active sides by ordinal, so a 3-across-2 split is stable
        /// run to run instead of drifting with float rounding. PURE.
        /// </summary>
        public static void PartitionCount(int total, int sideCount, int[] into)
        {
            if (into == null || sideCount <= 0) return;
            int n = Mathf.Max(0, total);
            int each = n / sideCount;
            int rem  = n % sideCount;
            for (int s = 0; s < sideCount && s < into.Length; s++)
                into[s] = each + (s < rem ? 1 : 0);
        }

        /// <summary>
        /// Picks the attacking sides for a wave: one marker per distinct
        /// <see cref="WaveSpawnPoint.GateIndex"/> (0 N, 1 E, 2 S, 3 W), appended to
        /// <paramref name="into"/> in ROTATION order (the wave's base side first, then the
        /// stepped sides) - not ascending GateIndex order.
        ///
        /// ⛔ Resolution is BY COMPONENT — the caller hands in the result of
        /// <c>FindObjectsByType&lt;WaveSpawnPoint&gt;()</c>. There is NO "SpawnPoint" tag; it is
        /// undeclared and <c>FindGameObjectsWithTag</c> THROWS on it (CLAUDE.md §7).
        ///
        /// ⚠ <c>FindObjectsByType</c> is UNORDERED, so within a side the marker is chosen by
        /// <see cref="WaveSpawnResolver.FirstDeterministic"/> (ordinal by SpawnId) — the same
        /// helper that exists because a boss used to enter from a random side every session.
        /// The old PickGate returned "the first list element with this GateIndex", which was
        /// scene-enumeration order and therefore NOT reproducible.
        ///
        /// The rotation base still cycles N -> E -> S -> W across waves. At 2 sides the pair is
        /// deliberately OPPOSITE (step = distinctSides / desired), because adjacent sides can both
        /// be covered from one position — which would make the 1 -> 2 step a non-event.
        /// </summary>
        public static void ResolveSides(
            IReadOnlyList<WaveSpawnPoint> points, int waveId, int desiredSides, List<WaveSpawnPoint> into)
        {
            if (into == null) return;
            into.Clear();
            if (points == null || points.Count == 0) return;

            // Distinct GateIndex values actually present, ascending. Never assume 4 exist:
            // CastleSpawnPointInjector self-suppresses when any WaveSpawnPoint is already there,
            // so a hand-authored scene can legitimately carry fewer.
            var sideIds = new List<int>();
            for (int i = 0; i < points.Count; i++)
            {
                if (points[i] == null) continue;
                int gi = points[i].GateIndex;
                if (!sideIds.Contains(gi)) sideIds.Add(gi);
            }
            if (sideIds.Count == 0) return;
            sideIds.Sort();

            int want = Mathf.Clamp(desiredSides, 1, sideIds.Count);
            int start = (Mathf.Max(1, waveId) - 1) % sideIds.Count;   // legacy N->E->S->W rotation
            int step  = Mathf.Max(1, sideIds.Count / want);

            var bucket = new List<WaveSpawnPoint>();
            for (int k = 0; k < want; k++)
            {
                int sideId = sideIds[(start + k * step) % sideIds.Count];

                bucket.Clear();
                for (int i = 0; i < points.Count; i++)
                    if (points[i] != null && points[i].GateIndex == sideId) bucket.Add(points[i]);

                WaveSpawnPoint chosen = WaveSpawnResolver.FirstDeterministic(bucket);
                if (chosen == null)
                {
                    FlowTrace.Warn("Enemy",
                        $"ResolveSides: wave {waveId} side GateIndex {sideId} had " +
                        $"{bucket.Count} candidate(s) but none survived deterministic ordering — " +
                        "that side gets NO attackers this wave.");
                    continue;
                }
                if (!into.Contains(chosen)) into.Add(chosen);
            }
        }

        /// <summary>Human-readable side list for a trace line: <c>north[g0] east[g1]</c>.</summary>
        private static string DescribeSides(List<WaveSpawnPoint> sides)
        {
            if (sides == null || sides.Count == 0) return "<none>";
            var sb = new System.Text.StringBuilder();
            for (int s = 0; s < sides.Count; s++)
            {
                if (s > 0) sb.Append(' ');
                sb.Append(sides[s].Direction).Append("[g").Append(sides[s].GateIndex)
                  .Append(" '").Append(sides[s].SpawnId).Append("']");
            }
            return sb.ToString();
        }

        /// <summary>
        /// MEASURED per-side result for a trace line: <c>north=4/4 south=0/3</c> — bodies that
        /// really landed over bodies the partition planned. A side reading <c>0/N</c> is the
        /// concurrency cap binding before that side was served, which is the one failure mode
        /// that makes a multi-side wave feel WEAKER than a single-side one.
        /// </summary>
        private static string DescribeMeasured(List<WaveSpawnPoint> sides, int[] actual, int[] planned)
        {
            if (sides == null || sides.Count == 0) return "<none>";
            var sb = new System.Text.StringBuilder();
            for (int s = 0; s < sides.Count; s++)
            {
                if (s > 0) sb.Append(' ');
                sb.Append(sides[s].Direction).Append('=')
                  .Append(actual != null && s < actual.Length ? actual[s] : -1).Append('/')
                  .Append(planned != null && s < planned.Length ? planned[s] : -1);
            }
            return sb.ToString();
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
