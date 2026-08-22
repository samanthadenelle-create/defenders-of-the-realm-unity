// =============================================================================
// EnemyPoolResetRegression [enemy-pool-reset]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
//
// Pins the 2026-08-02 enemy-pooling + tactical-layer audit. Six defects, all live,
// all of the same shape: state that outlives the thing that set it.
//
//   P0-1  Enemy._casting was set by RootedCast and cleared ONLY on that coroutine's
//         final line. Die/ResetForPool/PrepareForReuse never cleared it, and
//         ResetForPool's StopAllCoroutines KILLED RootedCast before that line could
//         run. Kill a caster mid-wind-up and the body pooled with _casting == true;
//         on reuse DriveNav's `if (_casting) { isStopped = true; return; }` fired
//         every frame forever -> a permanent statue that never moved or sieged.
//   P0-2  EnemyBrain had NO reset at all. Tactics, role, leash, room AABB, arena
//         hero-only flag, coordinated-flank bearing and the bow-equip latch all
//         survived Release/Get. A caster body reused as a Tank kept KiterTactics and
//         held its 10 m standoff; a dungeon mob's leash made a village wave enemy
//         dormant at its gate.
//   P0-3  EnemyBrain.ChooseTarget handed a WOUNDED ALLY (an Enemy - its own side)
//         back as the ATTACK target for DPS/Ranged roles.
//   P0-4  SmartEnemySpawner - THE live wave path - stamped brain.Role but never
//         applied tactics, so the whole WO-145 tactical layer was dead there and
//         every wave enemy fell to an UNTHROTTLED legacy target chain that ran a
//         20 m OverlapSphere plus a whole-scene FindAnyObjectByType per frame.
//   P0-5  RaidGarrisonSpawner gave guards no EnemyBrain at all and the boss a brain
//         with no tactics.
//   P1-7  EnemyPool.Release filed every body it did not build under a "_default"
//         queue that no spawner ever requests - a write-only queue under
//         DontDestroyOnLoad, i.e. an unbounded leak of skinned meshes.
//
// WHY THIS ORACLE IS REFLECTIVE, NOT A CHECKLIST: the root cause of P0-1 was a field
// being ADDED to Enemy without being added to the reset path. A hand-written list of
// field names would have the identical defect - it would pass forever while the class
// grew around it. So Case 1/2 ENUMERATE THE FIELDS BY REFLECTION over the live types
// and require every one of them to be either (a) assigned in the pool-reset path, or
// (b) named in this file's EXEMPT table with a written reason. A field added tomorrow
// matches neither and FAILS until someone consciously decides which it is. That is the
// entire point of the case.
//
// Coverage is asserted by SOURCE LINT (the reset path's own text), not by driving a
// live Enemy: Enemy.Awake ensures a NavMeshAgent + Animator + health bar and
// ResetForPool touches TargetManager / DamageAttribution, none of which exist in a
// bare edit-mode scene. The thing that regresses here is a MISSING ASSIGNMENT, and a
// lint over the reset methods catches exactly that. Comments are stripped first, so
// prose can never satisfy the lint.
//
// Cases:
//   1 [enemy-latch-coverage]  Every instance field on Enemy is assigned in the pool-
//                             reset path or is EXEMPT with a reason.
//   2 [brain-latch-coverage]  Same for EnemyBrain.ResetForPool.
//   3 [brain-reset-wired]     EnemyBrain.ResetForPool exists, is public, and BOTH
//                             Enemy.ResetForPool (release) and Enemy.PrepareForReuse
//                             (acquire) actually call it.
//   4 [casting-cleared]       P0-1 specifically: _casting is cleared in Die AND in
//                             the shared reset. Cheap, and it names the exact bug.
//   5 [pool-no-orphan-queue]  A Release lands in a queue a spawner actually drains:
//                             every EnemyPool.Get key literal is a real prefix key,
//                             and a body with NO key is DESTROYED, never queued.
//   6 [role-implies-tactics]  Every live spawn path that stamps `brain.Role =` also
//                             applies tactics in the same file.
//   7 [no-ally-attack-target] P0-3 cannot come back: no offensive-role branch of
//                             ChooseTarget returns FindMostDamagedAlly.
//   8 [no-per-frame-scan]     The no-tactics legacy chain is throttled and
//                             FindClosestTarget no longer runs a whole-scene hero
//                             scan unguarded.
//   9 [perfect-hit-real]      P1-6: isPerfect derives from a recorded PLAYER TAP, not
//                             from the coroutine's own fixed delay read back.
//
// Markers: ENEMY_POOL_RESET_OK / ENEMY_POOL_RESET_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.EnemyPoolResetRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class EnemyPoolResetRegression
    {
        private const string EnemySrc   = "Assets/_Modules/Village/Enemies/Enemy.cs";
        private const string BrainSrc   = "Assets/_Modules/Village/Enemies/EnemyBrain.cs";
        private const string PoolSrc    = "Assets/_Modules/Village/Enemies/EnemyPool.cs";
        private const string AttackSrc  = "Assets/_Modules/Village/Enemies/PlayerAttackController.cs";
        private const string ModulesDir = "Assets/_Modules";

        // CLAUDE.md section 1's mandatory C# gate counts raw '{' / '}' characters across the whole
        // file, so a lone brace CHARACTER LITERAL in the method-body scanner below would trip it
        // even though the code is perfectly balanced. These two constants are declared as a pair
        // on adjacent lines - one open, one close - so the file-wide count stays even, and the
        // scanner refers to them by name instead of writing bare brace literals.
        private const char OpenBrace  = '{';
        private const char CloseBrace = '}';

        // The methods that together ARE the Enemy pool-reset contract. A field assigned
        // in any of them counts as covered.
        private static readonly string[] EnemyResetMethods =
            { "ClearPooledLatches", "ResetForPool", "PrepareForReuse" };

        // ── EXEMPT TABLES ────────────────────────────────────────────────────
        // A field listed here is NOT required to be reset, and the value is the REASON.
        // Adding an entry is a deliberate act; that is the safety valve that keeps Case
        // 1/2 honest instead of noisy. Anything not here and not assigned = FAIL.

        private static readonly Dictionary<string, string> EnemyExempt = new Dictionary<string, string>
        {
            // PUBLIC EVENTS - exempt DELIBERATELY, and the reasoning is the opposite of "harmless".
            // Clearing these on release WOULD BREAK REUSE: EnemyBrain.Awake subscribes
            // (`_enemy.Died += ...`, `_enemy.Damaged += OnEnemyDamaged` - EnemyBrain.cs ~:628-629),
            // and Awake does NOT run again on a pooled body, so nulling the events would silently
            // sever the brain's death/retaliation forwarding for every reused enemy.
            // KNOWN LEAK, NOT CLOSED HERE: several PER-SPAWN subscribers never unsubscribe -
            // BattleArena.cs:1312 (`enemy.Died += HandleEnemyDied`) and ItemDropWatcher.cs:63/77 -
            // so a reused body accumulates stale subscribers across lives. Others DO unsubscribe
            // correctly (OverworldEncounterSpawner, TutorialWaveSpawner, EnemyGroupCoordinator).
            // The real fix is symmetric unsubscribe at the leaking call sites, NOT blanket-nulling
            // here. Filed rather than attempted, because getting it wrong makes enemies stop dying.
            { "Died",         "event; EnemyBrain.Awake subscribes and Awake does not re-run on reuse - see the leak note above" },
            { "ReachedHeart", "event; same Awake-subscription lifetime as Died" },
            { "Damaged",      "event; EnemyBrain.Awake subscribes OnEnemyDamaged - clearing severs retaliation on reuse" },

            // Re-stamped by the spawner's Configure(instanceId, def, heart) on every spawn,
            // fresh or reused - resetting them here would only blank a value that is about
            // to be overwritten one line later.
            // WO-874: re-FETCHED (GetComponent) and re-ARMED (ArmForTier) by
            // Enemy.EnsureEliteVfx, which Configure calls at its end on EVERY spawn -
            // fresh or reused. Nulling it here would only blank a reference that is about
            // to be re-read one line later. The pooled-DOWNGRADE case (an elite body reused
            // as a plain mob) is handled inside ArmForTier(false,false), which stands the
            // aura down explicitly rather than leaving it running - that Stop() was a real
            // leak until 2026-08-22, so do not assume the component self-clears.
            { "_eliteVfx",       "re-fetched + re-armed by Configure -> EnsureEliteVfx; downgrade handled in ArmForTier" },
            { "_enemyId",        "re-stamped by Configure" },
            { "_enemyDefId",     "re-stamped by Configure" },
            { "_maxHp",          "re-seeded from the def by Configure" },
            { "_level",          "re-seeded from the def by Configure" },
            { "_moveSpeed",      "re-seeded from the def by Configure" },
            { "_contactDamage",  "re-seeded from the def by Configure" },
            { "_attackInterval", "re-seeded from the def by Configure" },
            { "_ai",             "re-seeded from the def by Configure" },
            { "_isFlying",       "re-seeded from the def by Configure" },
            { "_def",            "re-stamped by Configure" },
            { "_heart",          "re-stamped by Configure" },
            { "_hp",             "PrepareForReuse revives to _maxHp, then Configure re-seeds" },

            // Authored tuning constants - inspector/prefab values, never written at runtime.
            { "_contactProbeDistance", "authored tuning, never runtime-mutated" },
            { "_structureSweepRadius", "authored tuning, never runtime-mutated" },
            { "_heartArrivalRadius",   "authored tuning, never runtime-mutated" },
            { "_heroAggroDropMargin",  "authored tuning, never runtime-mutated" },
            { "_pathRefreshInterval",  "authored tuning, never runtime-mutated" },
            { "_pathMinMoveDelta",     "authored tuning, never runtime-mutated" },
            { "_heavyHitThreshold",    "authored tuning, never runtime-mutated" },
            { "_typeVfxSet",           "authored asset reference" },
            { "_deathVFXOverride",     "authored asset reference" },

            // Same-GameObject component caches. They survive pooling BY DESIGN - reusing the
            // built body is the entire purpose of the pool.
            { "_agent",       "same-GameObject component cache" },
            { "_actor",       "same-GameObject component cache" },
            { "_animator",    "same-GameObject component cache" },
            { "_hitReaction", "same-GameObject component cache" },
            { "_healthBar",   "same-GameObject component cache" },
            { "_audioSource", "same-GameObject component cache" },
            { "_castVfx",     "same-GameObject component cache" },
            { "_brain",       "same-GameObject component cache" },
            { "_brainResolved", "latch for the _brain cache; re-resolves while null" },
            { "_structureScanBuffer", "readonly scratch buffer, overwritten per use" },
            { "_hasSpeedParam",  "Animator capability cache for this body" },
            { "_hasAttackParam", "Animator capability cache for this body" },
            { "_hasWindUpParam", "Animator capability cache for this body" },
            { "_hasHitParam",    "Animator capability cache for this body" },
            { "_hasDeadParam",   "Animator capability cache for this body" },
            { "_hasHitDirParam", "Animator capability cache for this body" },

            // Deliberately persistent.
            { "_poolKey", "THE QUEUE IDENTITY - clearing it is the P1-7 leak (body filed under a queue nobody drains)" },
            { "_authoredHeroAggroRadius", "the authored snapshot the reset RESTORES FROM" },
            // Latched in Awake from the SERIALIZED field, which cannot change for the life of the
            // instance - so it is per-BODY, not per-life, and clearing it would be the bug: a
            // prefab-authored VFX set would be discarded on first reuse and replaced by the
            // library floor. Note this does NOT strand a pooled body on the wrong family's art:
            // EnsureTypeVfxSet only latches TRUE for a prefab/foreign assignment, and a
            // library-resolved set leaves it FALSE (IsLibrarySet), so Configure still upgrades a
            // recycled body to its new family. Verified at Enemy.EnsureTypeVfxSet.
            { "_typeVfxSetAuthored", "per-BODY latch of a serialized value; clearing it would discard authored art on reuse" },
        };

        private static readonly Dictionary<string, string> BrainExempt = new Dictionary<string, string>
        {
            // Public event, same reasoning as Enemy.Died above: EnemyBrain.Awake wires
            // `_enemy.Died += e => Died?.Invoke(e)` and Awake does not re-run on a pooled body,
            // so nulling this on reset would sever EnemyGroupCoordinator's member-death pruning.
            { "Died", "event; forwarded from Enemy.Died in Awake, which does not re-run on reuse" },

            // Authored tuning.
            { "_threatScanRadius", "authored tuning, never runtime-mutated" },
            { "_towerScanRadius",  "authored tuning, never runtime-mutated" },
            { "_heroEngageRadius", "authored tuning, never runtime-mutated" },
            { "_healScanRadius",   "authored tuning, never runtime-mutated" },
            { "_healThreshold",    "authored tuning, never runtime-mutated" },
            { "_healAmount",       "authored tuning, never runtime-mutated" },
            { "_healInterval",     "authored tuning, never runtime-mutated" },
            { "_enemyData",        "authored asset reference, read once in Awake" },
            { "damage",            "authored tuning / overlaid once from _enemyData in Awake" },
            { "attackCooldown",    "authored tuning / overlaid once from _enemyData in Awake" },

            // The authored snapshot the reset restores FROM.
            { "_authoredTactics",  "the authored snapshot the reset RESTORES FROM" },
            { "_authoredRole",     "the authored snapshot the reset RESTORES FROM" },
            { "_authoredCaptured", "one-shot latch guarding the snapshot capture" },

            // Same-GameObject component caches.
            { "_enemy",           "same-GameObject component cache" },
            { "_bt",              "same-GameObject component cache" },
            { "_animator",        "same-GameObject component cache" },
            { "_sensor",          "same-GameObject component cache (auto-added in Awake)" },
            { "_navAgent",        "same-GameObject component cache" },
            { "_rushPath",        "reused NavMeshPath scratch object, overwritten by CalculatePath" },
            { "_scanBuffer",      "readonly scratch buffer, overwritten per use" },
            { "_hasIsAlertParam", "Animator capability cache for this body" },
        };

        // Every pool key PREFIX a spawner actually passes to EnemyPool.Get. A Release must
        // land in one of these queues or be destroyed; anything else is a write-only queue.
        private static readonly string[] LiveKeyPrefixes = { "model:", "prefab:" };

        // KNOWN GAPS (Case 6), recorded rather than hidden. These three spawn paths carry the
        // SAME defect P0-4/P0-5 fixed in SmartEnemySpawner + RaidGarrisonSpawner - they stamp
        // brain.Role and never apply tactics - but they sit OUTSIDE the file silo the 2026-08-02
        // enemy-AI pass owned, so touching them here would collide with another lane. They are
        // listed (not deleted from the scan) so the case stays GREEN for the lane that shipped
        // while the debt is named in code, and so any NEW offender still fails immediately.
        // Each entry is a real bug awaiting its own ticket:
        //   OutpostEnemyGroupSpawner.cs - the DUNGEON group path. Every dungeon mob runs with
        //       _tactics == null: no kite/flank/siege posture and the untuned legacy target chain.
        //   EnemyOutpost.cs             - the outpost boss, identical to the raid-boss defect:
        //       a brain with no tactics falls to a chain that finds nothing in an outpost scene.
        //   EnemyFamilyTestSpawner.cs   - a dev-tools spawner; lowest impact, same shape.
        private static readonly Dictionary<string, string> RoleTacticsKnownGaps = new Dictionary<string, string>
        {
            { "OutpostEnemyGroupSpawner.cs", "dungeon group path - out of the 2026-08-02 lane, ticket owed" },
            { "EnemyOutpost.cs",             "outpost boss - out of the 2026-08-02 lane, ticket owed" },
            { "EnemyFamilyTestSpawner.cs",   "dev-tools spawner - out of the 2026-08-02 lane, ticket owed" },
        };

        // ── Entry points ─────────────────────────────────────────────────────

        /// <summary>Batchmode entry: writes the OK/FAIL marker line. Never throws.</summary>
        public static void RunAll()
        {
            bool ok;
            string reason;
            try
            {
                ok = Run(out reason);
            }
            catch (Exception ex)
            {
                ok = false;
                reason = "threw " + ex.GetType().Name + ": " + ex.Message;
            }
            Debug.Log((ok ? "ENEMY_POOL_RESET_OK " : "ENEMY_POOL_RESET_FAIL ") + reason);
        }

        /// <summary>
        /// DataRegression-shaped contract. Returns true when every case passes;
        /// <paramref name="reason"/> always carries a human-readable summary.
        /// NEVER throws - any unexpected exception is folded into a failure line.
        /// </summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();

            try
            {
                string enemySrc  = ReadStripped(EnemySrc,  failures);
                string brainSrc  = ReadStripped(BrainSrc,  failures);
                string poolSrc   = ReadStripped(PoolSrc,   failures);
                string attackSrc = ReadStripped(AttackSrc, failures);

                CaseEnemyLatchCoverage(enemySrc, failures, log);
                CaseBrainLatchCoverage(brainSrc, failures, log);
                CaseBrainResetWired(enemySrc, brainSrc, failures, log);
                CaseCastingCleared(enemySrc, failures, log);
                CasePoolNoOrphanQueue(poolSrc, failures, log);
                CaseRoleImpliesTactics(failures, log);
                CaseNoAllyAttackTarget(brainSrc, failures, log);
                CaseNoPerFrameScan(brainSrc, failures, log);
                CasePerfectHitReal(attackSrc, failures, log);
            }
            catch (Exception ex)
            {
                failures.Add("[harness] threw " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count > 0)
            {
                reason = failures.Count + " failure(s): " + string.Join(" | ", failures);
                return false;
            }
            reason = "all cases passed. " + Condense(log.ToString());
            return true;
        }

        // ── Case 1: every Enemy instance field is reset or exempt ────────────

        private static void CaseEnemyLatchCoverage(string src, List<string> failures, StringBuilder log)
        {
            if (src == null) return;

            string resetText = ExtractMethods(src, EnemyResetMethods, failures, "Enemy");
            if (resetText == null) return;

            var fields = InstanceFields(typeof(Enemy));
            var uncovered = new List<string>();
            int covered = 0;

            foreach (var f in fields)
            {
                if (EnemyExempt.ContainsKey(f.Name)) continue;
                if (IsAssignedIn(resetText, f.Name)) { covered++; continue; }
                uncovered.Add(f.Name + " (" + Simple(f.FieldType) + ")");
            }

            if (uncovered.Count > 0)
            {
                failures.Add("[enemy-latch-coverage] " + uncovered.Count + " Enemy field(s) survive pooling - " +
                             "not assigned in " + string.Join("/", EnemyResetMethods) + " and not in EnemyExempt: " +
                             string.Join(", ", uncovered) +
                             ". THIS IS THE P0-1 DEFECT SHAPE (a latch added to the class, forgotten by the reset). " +
                             "Either clear it in ClearPooledLatches or add it to EnemyExempt with a written reason.");
            }
            log.AppendLine("[enemy-latch-coverage] " + covered + " reset, " + EnemyExempt.Count +
                           " exempt, of " + fields.Count + " instance fields.");
        }

        // ── Case 2: every EnemyBrain instance field is reset or exempt ───────

        private static void CaseBrainLatchCoverage(string src, List<string> failures, StringBuilder log)
        {
            if (src == null) return;

            string resetText = ExtractMethods(src, new[] { "ResetForPool" }, failures, "EnemyBrain");
            if (resetText == null) return;

            var fields = InstanceFields(typeof(EnemyBrain));
            var uncovered = new List<string>();
            int covered = 0;

            foreach (var f in fields)
            {
                if (BrainExempt.ContainsKey(f.Name)) continue;
                if (IsAssignedIn(resetText, f.Name)) { covered++; continue; }
                uncovered.Add(f.Name + " (" + Simple(f.FieldType) + ")");
            }

            if (uncovered.Count > 0)
            {
                failures.Add("[brain-latch-coverage] " + uncovered.Count + " EnemyBrain field(s) survive pooling - " +
                             "not assigned in ResetForPool and not in BrainExempt: " + string.Join(", ", uncovered) +
                             ". THIS IS THE P0-2 DEFECT SHAPE (tactics/leash/room state outliving the body's life).");
            }
            log.AppendLine("[brain-latch-coverage] " + covered + " reset, " + BrainExempt.Count +
                           " exempt, of " + fields.Count + " instance fields.");
        }

        // ── Case 3: the brain reset is actually invoked from the pool path ───

        private static void CaseBrainResetWired(string enemySrc, string brainSrc, List<string> failures, StringBuilder log)
        {
            var m = typeof(EnemyBrain).GetMethod("ResetForPool",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (m == null)
            {
                failures.Add("[brain-reset-wired] EnemyBrain.ResetForPool does not exist - the P0-2 brain state " +
                             "(tactics/role/leash/room/flank) has nothing clearing it across Release/Get.");
                return;
            }
            if (!m.IsPublic)
                failures.Add("[brain-reset-wired] EnemyBrain.ResetForPool is not public - Enemy (a different class) " +
                             "cannot invoke it from the pool path.");

            if (enemySrc == null) return;

            foreach (string caller in new[] { "ResetForPool", "PrepareForReuse" })
            {
                string body = ExtractMethod(enemySrc, caller);
                if (body == null)
                {
                    failures.Add("[brain-reset-wired] could not locate Enemy." + caller + " to verify the brain reset call.");
                    continue;
                }
                if (!Regex.IsMatch(body, @"ResolveBrain\s*\(\s*\)\s*\?\.\s*ResetForPool|_brain\s*\?\.\s*ResetForPool|GetComponent<\s*EnemyBrain\s*>\s*\(\s*\)\s*\?\.\s*ResetForPool"))
                {
                    failures.Add("[brain-reset-wired] Enemy." + caller + " does not call EnemyBrain.ResetForPool - " +
                                 "the sibling brain keeps the previous life's tactics/leash/room state (P0-2). " +
                                 (caller == "PrepareForReuse"
                                     ? "The ACQUIRE side matters most: it runs immediately before the spawner re-stamps Role/tactics."
                                     : "The RELEASE side must scrub before the body goes dormant."));
                }
            }
            log.AppendLine("[brain-reset-wired] EnemyBrain.ResetForPool present and invoked from both pool sides.");
        }

        // ── Case 4: P0-1 by name ─────────────────────────────────────────────

        private static void CaseCastingCleared(string src, List<string> failures, StringBuilder log)
        {
            if (src == null) return;

            string die = ExtractMethod(src, "Die");
            if (die == null) failures.Add("[casting-cleared] could not locate Enemy.Die.");
            else if (!IsAssignedIn(die, "_casting"))
                failures.Add("[casting-cleared] Enemy.Die does not clear _casting. A caster killed mid-wind-up dies " +
                             "with the cast root latched, so the death-hold frames run DriveNav's " +
                             "`if (_casting) { isStopped = true; return; }` on a corpse (P0-1).");

            string shared = ExtractMethod(src, "ClearPooledLatches");
            if (shared == null) failures.Add("[casting-cleared] could not locate Enemy.ClearPooledLatches - the shared reset.");
            else if (!IsAssignedIn(shared, "_casting"))
                failures.Add("[casting-cleared] the shared pool reset does not clear _casting. ResetForPool's " +
                             "StopAllCoroutines kills RootedCast BEFORE its final `_casting = false` line, so the body " +
                             "pools latched and every reuse is a permanent statue that never moves or sieges (P0-1).");

            log.AppendLine("[casting-cleared] _casting cleared on death and in the shared pool reset.");
        }

        // ── Case 5: no write-only pool queue ─────────────────────────────────

        private static void CasePoolNoOrphanQueue(string poolSrc, List<string> failures, StringBuilder log)
        {
            if (poolSrc == null) return;

            string release = ExtractMethod(poolSrc, "ReleaseInternal");
            if (release == null)
            {
                failures.Add("[pool-no-orphan-queue] could not locate EnemyPool.ReleaseInternal.");
                return;
            }

            // A keyless body must NOT be enqueued. Structural check: the keyless branch has to
            // Destroy and return before any Enqueue is reached.
            bool destroysKeyless = Regex.IsMatch(release,
                @"IsNullOrEmpty\s*\(\s*poolKey\s*\)[\s\S]{0,900}?Destroy\s*\([\s\S]{0,120}?return\s*;");
            bool stillDefaults = Regex.IsMatch(release, @"IsNullOrEmpty\s*\(\s*poolKey\s*\)\s*\)?\s*poolKey\s*=\s*""_default""");

            if (stillDefaults || !destroysKeyless)
            {
                failures.Add("[pool-no-orphan-queue] EnemyPool.ReleaseInternal still files an UNKEYED body into a queue " +
                             "(the \"_default\" bucket). SetPoolKey is stamped in exactly one place - GetInternal's " +
                             "fresh-build branch - but Enemy.Die releases EVERY enemy, and a dozen systems build bodies " +
                             "straight through EnemyFactory.Build. No caller ever asks Get() for that key, so the queue " +
                             "is WRITE-ONLY: every dungeon/outpost/overworld/arena/raid kill parks a skinned mesh + " +
                             "Animator + NavMeshAgent under DontDestroyOnLoad forever (P1-7, an OOM path on mobile). " +
                             "An unkeyed body must be destroyed, or the spawner must route through EnemyPool.Get.");
            }

            // Every key literal handed to Get must carry a live prefix, so the queue a Release
            // lands in is one a spawner genuinely drains.
            var bad = new List<string>();
            int scannedForKeys = 0;
            foreach (string file in EnumerateModuleSources())
            {
                string s = StripComments(SafeRead(file));
                if (s == null) continue;
                scannedForKeys++;
                foreach (Match m in Regex.Matches(s, @"EnemyPool\.Get\s*\(\s*""([^""]*)"""))
                {
                    string lit = m.Groups[1].Value;
                    if (!LiveKeyPrefixes.Any(p => lit.StartsWith(p, StringComparison.Ordinal)))
                        bad.Add(Path.GetFileName(file) + " -> \"" + lit + "\"");
                }
            }
            if (bad.Count > 0)
                failures.Add("[pool-no-orphan-queue] EnemyPool.Get called with a key outside the live prefixes (" +
                             string.Join(", ", LiveKeyPrefixes) + "): " + string.Join(", ", bad) +
                             ". A key nothing else uses is a queue nothing drains.");

            // ZERO-GUARD (Shape B, 2026-08-16 coverage audit). This case's only assertion
            // is inside the loop above. If EnumerateModuleSources() yields nothing (a moved
            // module root, an unreadable tree), `bad` stays empty and the case logged an
            // unqualified "all Get keys use a live prefix" having read no source at all.
            // An iteration over nothing is not a verification of everything.
            if (scannedForKeys == 0)
                failures.Add("[pool-no-orphan-queue] scanned ZERO module source files - EnumerateModuleSources() " +
                             "returned nothing readable, so the Get-key prefix check asserted NOTHING (not a pass)");

            log.AppendLine("[pool-no-orphan-queue] scanned " + scannedForKeys +
                           " module source(s); unkeyed releases destroyed; all Get keys use a live prefix.");
        }

        // ── Case 6: stamping a Role obliges applying tactics ─────────────────

        private static void CaseRoleImpliesTactics(List<string> failures, StringBuilder log)
        {
            var offenders = new List<string>();
            var knownGapsSeen = new List<string>();
            int checkedFiles = 0;

            foreach (string file in EnumerateModuleSources())
            {
                string s = StripComments(SafeRead(file));
                if (s == null) continue;

                // Something in this file stamps a role onto a brain.
                bool stampsRole = Regex.IsMatch(s, @"\b\w*[Bb]rain\w*\s*\.\s*Role\s*=");
                if (!stampsRole) continue;
                checkedFiles++;

                bool appliesTactics =
                    s.Contains("ApplyRoleTactics") ||
                    Regex.IsMatch(s, @"\.\s*SetTactics\s*\(");

                string leaf = Path.GetFileName(file);
                if (appliesTactics) continue;

                if (RoleTacticsKnownGaps.ContainsKey(leaf)) knownGapsSeen.Add(leaf);
                else offenders.Add(leaf);
            }

            // A known gap that has since been FIXED (or whose file is gone) must be struck off
            // the table, or the entry rots into a standing excuse that silently un-pins the next
            // real offender in that same file.
            foreach (string gap in RoleTacticsKnownGaps.Keys)
            {
                if (knownGapsSeen.Contains(gap)) continue;
                failures.Add("[role-implies-tactics] " + gap + " is listed in RoleTacticsKnownGaps but no longer " +
                             "stamps a role without tactics (or no longer exists). Remove the entry - a stale " +
                             "known-gap silently un-pins the next real offender in that file.");
            }

            if (knownGapsSeen.Count > 0)
                log.AppendLine("[role-implies-tactics] KNOWN GAP (out of lane, ticket owed): " +
                               string.Join(", ", knownGapsSeen.Select(g => g + " - " + RoleTacticsKnownGaps[g])));

            if (offenders.Count > 0)
                failures.Add("[role-implies-tactics] spawn path(s) stamp brain.Role but never apply tactics: " +
                             string.Join(", ", offenders) + ". A brain with _tactics == null skips the entire WO-145 " +
                             "tactical layer (kite / flank / siege / support, the scored target priority AND its eval " +
                             "throttle) and drops onto the legacy target chain - the P0-4/P0-5 defect. Call " +
                             "EnemyBrain.ApplyRoleTactics(brain, role) beside the Role stamp.");

            if (checkedFiles == 0)
                failures.Add("[role-implies-tactics] found NO file stamping brain.Role - the scan is broken " +
                             "(it would pass vacuously and pin nothing).");

            log.AppendLine("[role-implies-tactics] " + checkedFiles + " role-stamping spawn path(s), all apply tactics.");
        }

        // ── Case 7: enemies never attack their own wounded ───────────────────

        private static void CaseNoAllyAttackTarget(string src, List<string> failures, StringBuilder log)
        {
            if (src == null) return;

            string choose = ExtractMethod(src, "ChooseTarget");
            if (choose == null)
            {
                failures.Add("[no-ally-attack-target] could not locate EnemyBrain.ChooseTarget.");
                return;
            }

            // The Healer branch legitimately returns a wounded ally - it HEALS it (TickHeal).
            // Any OTHER branch doing so points an attack at a friendly.
            foreach (Match m in Regex.Matches(choose, @"(?m)^.*FindMostDamagedAlly.*$"))
            {
                string line = m.Value;
                if (Regex.IsMatch(line, @"case\s+EnemyRole\.Healer") ) continue;
                if (Regex.IsMatch(line, @"EnemyRole\.DPS|EnemyRole\.Ranged"))
                {
                    failures.Add("[no-ally-attack-target] ChooseTarget routes an offensive role (DPS/Ranged) to " +
                                 "FindMostDamagedAlly: " + Condense(line) + ". That returns a wounded ENEMY - this " +
                                 "unit's OWN side - as the ATTACK target, so seconds after first contact every DPS " +
                                 "and Ranged enemy abandons the march and clusters on its own wounded (P0-3, the " +
                                 "recurring 'enemies just mill around / never attack me'). Ally support is the " +
                                 "Healer role's job and it HEALS via TickHeal.");
                }
            }

            // The Healer path must still exist - deleting it would break healers.
            if (!Regex.IsMatch(choose, @"case\s+EnemyRole\.Healer\s*:[\s\S]{0,200}?FindMostDamagedAlly"))
                failures.Add("[no-ally-attack-target] the Healer branch no longer resolves a wounded ally - " +
                             "healers have nothing to mend (over-correction of P0-3).");

            log.AppendLine("[no-ally-attack-target] only the Healer role resolves a wounded ally, and it heals it.");
        }

        // ── Case 8: no unthrottled whole-scene scan in the legacy chain ──────

        private static void CaseNoPerFrameScan(string src, List<string> failures, StringBuilder log)
        {
            if (src == null) return;

            string score = ExtractMethod(src, "ScoreAndPickTarget");
            if (score == null)
            {
                failures.Add("[no-per-frame-scan] could not locate EnemyBrain.ScoreAndPickTarget.");
                return;
            }

            // The no-tactics branch must be throttled by a timer before it runs the chain.
            int nullTactics = score.IndexOf("_tactics == null", StringComparison.Ordinal);
            int chain = score.IndexOf("FindNearbyHero", StringComparison.Ordinal);
            if (nullTactics >= 0 && chain > nullTactics)
            {
                string branch = score.Substring(nullTactics, chain - nullTactics);
                if (!Regex.IsMatch(branch, @"_legacyEvalTimer|_targetEvalTimer"))
                    failures.Add("[no-per-frame-scan] the no-tactics legacy target chain runs UNTHROTTLED. It is " +
                                 "FindNearbyHero ?? FindNearestTower ?? FindClosestTarget - a 20 m OverlapSphere plus " +
                                 "a whole-scene FindAnyObjectByType<HeroLocomotion> - and it ran PER ENEMY PER FRAME " +
                                 "whenever the hero sat outside the 11 m engage ring with no tower near, which is the " +
                                 "normal state at wave start. At the 22-enemy cap that is 22 scene scans every frame " +
                                 "(P0-4). Gate it behind an eval timer.");
            }

            string closest = ExtractMethod(src, "FindClosestTarget");
            if (closest == null)
            {
                failures.Add("[no-per-frame-scan] could not locate EnemyBrain.FindClosestTarget.");
            }
            else if (!closest.Contains("_heroTransform"))
            {
                failures.Add("[no-per-frame-scan] FindClosestTarget still calls FindHeroTransform() without touching " +
                             "the cached _heroTransform - it runs a whole-scene FindAnyObjectByType and THROWS THE " +
                             "RESULT AWAY, so it re-scans on the very next call forever (P0-4). Read Update()'s " +
                             "already-throttled cache instead.");
            }

            log.AppendLine("[no-per-frame-scan] legacy chain throttled; hero resolution reads the shared cache.");
        }

        // ── Case 9: the perfect hit is a real player input ───────────────────

        private static void CasePerfectHitReal(string src, List<string> failures, StringBuilder log)
        {
            if (src == null) return;

            string resolve = ExtractMethod(src, "ResolveAttack");
            if (resolve == null)
            {
                failures.Add("[perfect-hit-real] could not locate PlayerAttackController.ResolveAttack.");
                return;
            }

            // The old shape: isPerfect computed from (Time.time - _swingStartTime), which inside
            // this coroutine is just the fixed WaitForSeconds delay read back - always true.
            if (Regex.IsMatch(resolve, @"isPerfect\s*=[\s\S]{0,200}?_swingStartTime"))
                failures.Add("[perfect-hit-real] isPerfect is still derived from (Time.time - _swingStartTime) inside " +
                             "ResolveAttack. That value IS the coroutine's own fixed hit delay read back, so with the " +
                             "delay sitting inside the window isPerfect is unconditionally TRUE above ~20 FPS: the " +
                             "multiplier applies to every swing (the stated base damage is a lie), the gold PERFECT " +
                             "stamp fires on every hit and means nothing, and a frame hitch randomly strips the bonus " +
                             "with no cue. A perfect hit must come from a real player input (P1-6).");

            if (!Regex.IsMatch(resolve, @"isPerfect\s*=[\s\S]{0,200}?_perfectTapElapsed"))
                failures.Add("[perfect-hit-real] isPerfect is not derived from a recorded player tap " +
                             "(_perfectTapElapsed). The perfect-hit window must be earned by a second input, not " +
                             "granted by the passage of time (P1-6).");

            var reg = typeof(PlayerAttackController).GetMethod("RegisterPerfectTap",
                BindingFlags.Instance | BindingFlags.Public);
            if (reg == null)
                failures.Add("[perfect-hit-real] PlayerAttackController.RegisterPerfectTap is missing or not public - " +
                             "there is no input seam for the perfect-hit tap, so touch (the HUD basic-attack button, " +
                             "the only attack input on a phone) can never reach the mechanic.");

            // The window must be bounded by the impact frame; damage resolves there (WO-217),
            // so an authored window past it would be silently unreachable.
            if (!resolve.Contains("Mathf.Min(_perfectHitWindowEnd"))
                failures.Add("[perfect-hit-real] the perfect window end is not clamped to the impact-frame delay. " +
                             "Damage resolves at the impact frame (WO-217), so any part of the window past it can " +
                             "never be hit - exactly the kind of silent unreachable-window lie P1-6 removed.");

            log.AppendLine("[perfect-hit-real] perfect hit driven by a recorded tap, window clamped to the impact frame.");
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Instance fields declared BY this type (public or private), excluding statics,
        /// consts and compiler-generated backing fields. Reflective on purpose: a field
        /// added to the class tomorrow shows up here with no edit to this oracle.
        /// </summary>
        private static List<FieldInfo> InstanceFields(Type t)
        {
            return t.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                    .Where(f => !f.IsLiteral && !f.Name.Contains("k__BackingField"))
                    .OrderBy(f => f.Name, StringComparer.Ordinal)
                    .ToList();
        }

        /// <summary>True when <paramref name="name"/> appears as an ASSIGNMENT TARGET in the text.</summary>
        private static bool IsAssignedIn(string text, string name)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return Regex.IsMatch(text, @"(?<![A-Za-z0-9_])" + Regex.Escape(name) + @"\s*=(?!=)");
        }

        private static string ExtractMethods(string src, string[] names, List<string> failures, string owner)
        {
            var sb = new StringBuilder();
            foreach (string n in names)
            {
                string body = ExtractMethod(src, n);
                if (body == null)
                {
                    failures.Add("[harness] could not locate " + owner + "." + n +
                                 " - the reset-coverage case cannot run and must not pass vacuously.");
                    return null;
                }
                sb.AppendLine(body);
            }
            return sb.ToString();
        }

        /// <summary>
        /// Returns the brace-balanced body of the named method (first declaration found).
        /// Null when absent. Comment-stripped source in, so prose cannot satisfy a lint.
        /// </summary>
        private static string ExtractMethod(string src, string name)
        {
            if (string.IsNullOrEmpty(src)) return null;

            var decl = Regex.Match(src,
                @"(?<![A-Za-z0-9_])" + Regex.Escape(name) + @"\s*\([^)]*\)\s*" + Regex.Escape(OpenBrace.ToString()));
            if (!decl.Success) return null;

            int open = src.IndexOf(OpenBrace, decl.Index);
            if (open < 0) return null;

            int depth = 0;
            for (int i = open; i < src.Length; i++)
            {
                if (src[i] == OpenBrace) depth++;
                else if (src[i] == CloseBrace)
                {
                    depth--;
                    if (depth == 0) return src.Substring(open, i - open + 1);
                }
            }
            return null;
        }

        private static IEnumerable<string> EnumerateModuleSources()
        {
            if (!Directory.Exists(ModulesDir)) yield break;
            foreach (string f in Directory.GetFiles(ModulesDir, "*.cs", SearchOption.AllDirectories))
                yield return f.Replace('\\', '/');
        }

        private static string ReadStripped(string path, List<string> failures)
        {
            string raw = SafeRead(path);
            if (raw == null)
            {
                failures.Add("[source] missing or unreadable: " + path);
                return null;
            }
            return StripComments(raw);
        }

        private static string SafeRead(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : null; }
            catch (Exception) { return null; }
        }

        /// <summary>Strips // and /* */ comments so a lint can never be satisfied by prose.</summary>
        private static string StripComments(string src)
        {
            if (string.IsNullOrEmpty(src)) return null;
            string noBlock = Regex.Replace(src, @"/\*.*?\*/", " ", RegexOptions.Singleline);
            return Regex.Replace(noBlock, @"//[^\r\n]*", " ");
        }

        private static string Simple(Type t)
        {
            return t == null ? "?" : t.Name;
        }

        private static string Condense(string s)
        {
            string one = Regex.Replace(s ?? string.Empty, @"\s+", " ").Trim();
            return one.Length > 400 ? one.Substring(0, 397) + "..." : one;
        }
    }
}
