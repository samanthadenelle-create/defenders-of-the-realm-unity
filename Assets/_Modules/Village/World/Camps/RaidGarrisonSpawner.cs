// =============================================================================
// RaidGarrisonSpawner — the runtime garrison spawner for a CONFIG-GENERATED raid
// base (the RaidBase_<id>.unity scenes RaidBaseGenerator.BuildFromConfig bakes).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World.Camps
//
// THE GAP THIS CLOSES: a baked RaidBase_<id> scene has walls + Watchtower_* towers
// + a BossSpawn marker but spawns NO defenders. This component (attached to the
// RaidBase_<id> root by RaidBaseGenerator with the config id stored) reads the
// garrison block from scene-configs.json and, one frame after Start, spawns:
//   * the BOSS at the BossSpawn marker (MiniBoss brain, x3 HP / x1.5 dmg, min
//     height 2.6) — mirrors EnemyOutpost.SpawnBoss.
//   * the composition[{enemyId,count}] guard ring at baseRadius * 0.5 inside the
//     perimeter, NavMesh-snapped, scaled by player level + difficultyMultiplier,
//     staggered 1-2/frame so an Extreme garrison doesn't hitch on mobile.
//
// KEY CATCH (verified): the baked scene name (RaidBase_<id>) does NOT match the
// config's sceneName, so SceneOwnership can't resolve it by name — this spawner
// carries a STORED configId and calls SceneOwnership.SetEnemyOwned(true) itself so
// the turret-armer + the home-hub death-retreat treat the raid as enemy-owned.
//
// SPAWNING IS NOT REINVENTED (CLAUDE.md §9). Every defender is built through the
// ONE canonical path: EnemyFactory.Build -> Enemy.Configure -> SetBrainTarget(anchor)
// (HOLD the garrison; hero aggro still pulls them into the fight via TargetManager).
// Stat blocks + level scale come from the SHARED GarrisonStatBlocks; the towers are
// armed by the SHARED GarrisonTurretArmer — no duplication with GarrisonController.
//
// Enemies are Hostile by default (no faction code). LogWarning + degrade, never
// crash. Canon: the village is Elarion (never Avalon). ASCII-only runtime strings.
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using DeNelle.Core.Diagnostics;   // TGVRU — FlowTrace/Guard on the boss + guard spawn path
// EnemyDef / Enemy / EnemyFactory / EnemyBrain / EnemyRole all live in the parent
// namespace DeNelle.Village, visible here (DeNelle.Village.World.Camps nests under it).

namespace DeNelle.Village.World.Camps
{
    /// <summary>
    /// Lives on a baked <c>RaidBase_&lt;id&gt;</c> root. Spawns the config's garrison
    /// (boss + composition) scaled by player level on Start, and arms the authored
    /// Watchtower_* props. Spawning routes through the canonical
    /// <see cref="EnemyFactory"/> path — no parallel spawner.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RaidGarrisonSpawner : MonoBehaviour
    {
        // -- Builder-wired (RaidBaseGenerator sets this) -----------------------

        [Header("Config")]
        [Tooltip("scene-configs.json id this raid base was generated from (NOT the scene " +
                 "name — the baked scene is RaidBase_<id>, which won't match sceneName).")]
        [SerializeField] private string configId;

        [Header("Tuning")]
        [Tooltip("Hard cap on simultaneously-spawned live defenders (boss + guards) so an " +
                 "Extreme composition can't flood the scene / hitch mobile.")]
        [SerializeField] private int liveCombatantCap = 36;

        [Tooltip("Boss HP multiplier on top of the level/difficulty-scaled stat block.")]
        [SerializeField] private float bossHpMult = 3f;
        [Tooltip("Boss contact-damage multiplier on top of the level/difficulty-scaled stat block.")]
        [SerializeField] private float bossDamageMult = 1.5f;
        [Tooltip("Minimum boss body height (so the boss reads bigger than its guards).")]
        [SerializeField] private float bossMinHeight = 2.6f;

        [Header("Garrison turrets")]
        [Tooltip("Range (world units) for an armed garrison watchtower turret.")]
        [SerializeField] private float turretRange = 16f;
        [Tooltip("Damage per shot for an armed garrison watchtower turret.")]
        [SerializeField] private float turretDamage = 8f;
        [Tooltip("Shots per second for an armed garrison watchtower turret.")]
        [SerializeField] private float turretFireRate = 0.8f;

        // -- Runtime state -----------------------------------------------------

        /// <summary>Living defenders remaining (0 once the garrison is wiped).</summary>
        public int AliveCount => _aliveCount;
        /// <summary>Total defenders this garrison spawned (boss + guards).</summary>
        public int TotalGarrison => _garrison.Count;
        /// <summary>True once the whole garrison is dead (the raid base is clear).</summary>
        public bool Cleared { get; private set; }
        /// <summary>Raised once every defender is dead (the garrison is cleared).</summary>
        public event System.Action<RaidGarrisonSpawner> OnCleared;

        private readonly List<Enemy> _garrison = new List<Enemy>();
        private Transform _garrisonRoot;
        private int _aliveCount;
        private bool _activated;

        /// <summary>Test/inspect hook: set the config id in code before Start.</summary>
        public void SetConfigId(string id) => configId = id;

        private void Start()
        {
            StartCoroutine(ActivateRoutine());
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _garrison.Count; i++)
                if (_garrison[i] != null) _garrison[i].Died -= HandleGarrisonDied;
        }

        // =====================================================================
        // ACTIVATE — one frame after Start (so the additive scene's NavMesh is live),
        // read the config + spawn the garrison + arm the turrets. Idempotent.
        // =====================================================================
        private IEnumerator ActivateRoutine()
        {
            if (_activated) yield break;
            _activated = true;

            // Let the scene + NavMesh settle one frame (EnemyFactory snaps each spawn
            // to the nearest navmesh point, but the surface must be live first).
            yield return null;

            var def = SceneConfigCatalog.Find(configId);
            if (def == null)
            {
                Debug.LogWarning($"[RaidGarrisonSpawner] no scene-config '{configId}' — no garrison spawned.");
                yield break;
            }

            // WO-430 — apply this raid's modifier override (if authored) BEFORE anything spawns,
            // so troops + the garrison are born with the right perks ("before landing"). Empty →
            // clear the override, so the player's REAL upgrade tiers apply (the normal raid path).
            if (!string.IsNullOrEmpty(def.modifierOverride))
                DeNelle.Core.State.ModifierService.SetOverrideJson(def.modifierOverride);
            else
                DeNelle.Core.State.ModifierService.ClearOverride();

            if (!def.IsEnemy)
            {
                Debug.LogWarning($"[RaidGarrisonSpawner] config '{configId}' is not Enemy-owned — no garrison spawned.");
                yield break;
            }

            var g = def.garrison;
            if (g == null)
            {
                Debug.LogWarning($"[RaidGarrisonSpawner] config '{configId}' has no garrison block — no garrison spawned.");
                yield break;
            }

            // KEY CATCH: the baked scene name won't match the config's sceneName, so
            // SceneOwnership.Resolve read this scene Player-owned. Force it enemy-owned
            // (carry the STORED config id, not a scene-name match) so the turret-armer
            // arms + the home-hub death-retreat fires.
            SceneOwnership.SetEnemyOwned(true);

            // Raid BGM — driving brass for the assault (WO-453, brass-rampart.mp3).
            DeNelle.Core.CoreServices.Audio?.PlayMusic(DeNelle.Core.Audio.MusicTrack.Raid);

            // Player level (HeroProgression.Instance.Level — HeroProgression lives in the
            // parent DeNelle.Village namespace), fallback 1 if not yet up.
            int playerLevel = HeroProgression.Instance != null
                ? Mathf.Max(1, HeroProgression.Instance.Level)
                : 1;
            int enemyLevel = Mathf.Max(g.baseEnemyLevel, playerLevel + g.levelOffset);
            float difficulty = g.difficultyMultiplier > 0f ? g.difficultyMultiplier : 1f;
            float ring = Mathf.Max(2f, def.baseRadius * 0.5f);

            _garrisonRoot = new GameObject("[RaidGarrison]").transform;
            _garrisonRoot.SetParent(transform, false);
            _garrisonRoot.localPosition = Vector3.zero;

            yield return SpawnGarrisonStaggered(g, enemyLevel, difficulty, ring);

            // Arm the authored Watchtower_* props (SHARED armer; ownership-guarded inside).
            int armed = GarrisonTurretArmer.ArmWatchtowers(
                gameObject.scene, turretRange, turretDamage, turretFireRate);
            if (armed > 0)
                Debug.Log($"[RaidGarrisonSpawner] '{configId}' armed {armed} watchtower turret(s) (EnemyOwned).");

            if (_aliveCount == 0)
            {
                // R — spawned nothing: self-report (loud) instead of a silent auto-clear.
                FlowTrace.Fail("Garrison", $"'{configId}' spawned 0 living defenders " +
                               "(no navmesh under the base? empty composition?) — treating as cleared.");
                MarkCleared();
            }
            else
            {
                FlowTrace.Step("Garrison", $"'{configId}' garrison spawned: {_aliveCount} defender(s), " +
                          $"enemyLevel {enemyLevel} (player {playerLevel}), difficulty x{difficulty:F2}, ring {ring:F1}m.");
            }
        }

        // =====================================================================
        // SPAWN — boss this frame, then the composition guard ring 1-2/frame. The
        // empty-garrison handling lives in ActivateRoutine after this returns.
        // =====================================================================
        private IEnumerator SpawnGarrisonStaggered(GarrisonDef g, int enemyLevel, float difficulty, float ring)
        {
            // The BOSS holds the BossSpawn marker (MiniBoss role), counts toward the cap.
            SpawnBoss(g, enemyLevel, difficulty);
            yield return null;

            // Expand the composition into a flat id list, capped to the live budget
            // (the boss already took one slot). Hostile by default — no faction code.
            var ids = ExpandComposition(g);
            int budget = Mathf.Max(0, liveCombatantCap - _aliveCount);
            int toSpawn = Mathf.Min(ids.Count, budget);

            for (int i = 0; i < toSpawn; i++)
            {
                if (this == null || _garrisonRoot == null) yield break;

                SpawnGuard(ids[i], i, toSpawn, enemyLevel, difficulty, ring);

                // Stagger 1-2 units/frame so an Extreme garrison doesn't hitch on mobile.
                if ((i & 1) == 1) yield return null;
            }
        }

        // Flatten composition[{enemyId,count}] into a per-unit id list (count>0 guarded).
        private static List<string> ExpandComposition(GarrisonDef g)
        {
            var ids = new List<string>();
            if (g == null || g.composition == null) return ids;
            for (int i = 0; i < g.composition.Count; i++)
            {
                var entry = g.composition[i];
                if (entry == null || string.IsNullOrEmpty(entry.enemyId)) continue;
                int count = Mathf.Max(0, entry.count);
                for (int c = 0; c < count; c++) ids.Add(entry.enemyId);
            }
            return ids;
        }

        private void SpawnBoss(GarrisonDef g, int enemyLevel, float difficulty)
        {
            // Boss id: the config's boss field, else fall back to the orc-warlord template.
            string bossId = string.IsNullOrEmpty(g.boss) ? "orc-warlord" : g.boss;

            // Build through the SHARED stat blocks + level scale, then fold difficulty
            // and the boss multipliers (x3 HP / x1.5 dmg, min height) on top.
            EnemyDef def = GarrisonStatBlocks.BuildTypedDef(bossId, enemyLevel);
            GarrisonStatBlocks.ApplyLevelScale(def, enemyLevel);
            FoldDifficulty(def, difficulty);
            def.Hp            *= Mathf.Max(1f, bossHpMult);
            def.ContactDamage *= Mathf.Max(1f, bossDamageMult);
            def.Height         = Mathf.Max(def.Height, bossMinHeight);

            Transform marker = transform.Find("BossSpawn");
            Vector3 want = marker != null ? marker.position : transform.position;
            Vector3 pos = SnapToNav(want);

            var boss = EnemyFactory.Build(def, pos, Quaternion.identity, _garrisonRoot);
            if (boss == null)
            {
                // R/U — was a silent LogWarning. A null boss leaves the keep undefended at its
                // centre; self-report so the missing boss is loud.
                FlowTrace.Fail("Garrison", $"'{configId}' SpawnBoss: EnemyFactory returned null for boss '{bossId}' at {pos} — boss NOT spawned.");
                return;
            }
            boss.gameObject.name = $"RaidBoss ({def.Id}-Lv{enemyLevel})";

            var anchor = MakeAnchor("BossAnchor", pos);
            boss.Configure($"raidboss-{configId}", def, anchor);
            boss.SetBrainTarget(anchor);   // HOLD the keep; hero aggro still pulls it in

            // V — the boss must RENDER, else the hero fights an invisible keep defender.
            VerifyGuardRenders(boss, $"boss ({def.Id})", pos);

            // MiniBoss brain (tougher, holds the keep) — mirrors EnemyOutpost.SpawnBoss.
            var brain = boss.gameObject.GetComponent<EnemyBrain>();
            if (brain == null) brain = boss.gameObject.AddComponent<EnemyBrain>();
            brain.Role = EnemyRole.MiniBoss;

            Track(boss);
        }

        private void SpawnGuard(string enemyId, int index, int count, int enemyLevel, float difficulty, float ring)
        {
            // A single RING of stand positions at baseRadius * 0.5 inside the perimeter,
            // NavMesh-snapped (DECISION: one ring). Centred on the base root (BossSpawn
            // lives at the keep centre = root origin).
            float ang = (count > 0 ? (index / (float)count) : 0f) * Mathf.PI * 2f;
            Vector3 want = transform.position +
                           new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * ring;
            Vector3 pos = SnapToNav(want);

            // SHARED stat block -> level scale -> fold difficulty. Hostile by default.
            EnemyDef def = GarrisonStatBlocks.BuildTypedDef(enemyId, enemyLevel);
            GarrisonStatBlocks.ApplyLevelScale(def, enemyLevel);
            FoldDifficulty(def, difficulty);

            var guard = EnemyFactory.Build(def, pos, Quaternion.identity, _garrisonRoot);
            if (guard == null)
            {
                // R/U — was a silent LogWarning. A null guard shrinks the garrison; self-report.
                FlowTrace.Fail("Garrison", $"'{configId}' SpawnGuard[{index}]: EnemyFactory returned null for '{enemyId}' at {pos} — guard NOT spawned.");
                return;
            }
            guard.gameObject.name = $"RaidGuard ({def.Id}-Lv{enemyLevel}-{index})";

            var anchor = MakeAnchor($"GuardAnchor-{index}", pos);
            guard.Configure($"raidguard-{configId}-{index}", def, anchor);
            guard.SetBrainTarget(anchor);   // HOLD the garrison; hero aggro still pulls them in

            // V — the guard must RENDER, else the hero fights an invisible garrison defender.
            VerifyGuardRenders(guard, $"guard-{index} ({def.Id})", pos);

            Track(guard);
        }

        // V — confirm a spawned defender actually RENDERS (>=1 enabled renderer carrying a mesh),
        // else the hero fights an invisible garrison member. Mirrors CampGuards.VerifyGuardRenders:
        // a Warn that self-reports to the break-log; the defender stays in the garrison either way
        // (a hittable-but-invisible defender is still clearable; removing it could deadlock the raid).
        private void VerifyGuardRenders(Enemy enemy, string what, Vector3 pos)
        {
            if (enemy == null) return;
            var go = enemy.gameObject;
            int enabledWithMesh = 0;
            foreach (var smr in go.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (smr != null && smr.enabled && smr.sharedMesh != null) enabledWithMesh++;
            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr == null || !mr.enabled) continue;
                var mf = mr.GetComponent<MeshFilter>();
                if (mf != null && mf.sharedMesh != null) enabledWithMesh++;
            }
            if (enabledWithMesh == 0)
                FlowTrace.Warn("Garrison",
                    $"INVISIBLE DEFENDER: '{configId}' {what} at {pos} has 0 enabled renderers with a mesh — " +
                    "hero would fight an unseen garrison defender (EnemyFactory fallback should have tinted a capsule).");
        }

        // Fold the config's difficultyMultiplier into HP + contact damage (the ONE place
        // it touches combat). >0 guard so a missing/zero value is a no-op (x1).
        private static void FoldDifficulty(EnemyDef def, float difficulty)
        {
            if (def == null || difficulty <= 0f || Mathf.Approximately(difficulty, 1f)) return;
            def.Hp            *= difficulty;
            def.ContactDamage *= difficulty;
        }

        private void Track(Enemy e)
        {
            e.Died += HandleGarrisonDied;
            _garrison.Add(e);
            _aliveCount++;
        }

        // A local tether anchor so the defender HOLDS the garrison instead of marching
        // off. Mirrors EnemyOutpost.MakeAnchor / GarrisonController.MakeAnchor.
        private Transform MakeAnchor(string anchorName, Vector3 pos)
        {
            var go = new GameObject(anchorName);
            go.transform.SetParent(_garrisonRoot, false);
            go.transform.position = pos;
            return go.transform;
        }

        private static Vector3 SnapToNav(Vector3 want)
        {
            if (NavMesh.SamplePosition(want, out NavMeshHit hit, 8f, NavMesh.AllAreas))
                return hit.position;
            return want;
        }

        // =====================================================================
        // CLEAR — last defender dies -> mark cleared + raise OnCleared.
        // =====================================================================

        private void HandleGarrisonDied(Enemy enemy)
        {
            if (enemy != null) enemy.Died -= HandleGarrisonDied;
            _aliveCount = Mathf.Max(0, _aliveCount - 1);
            if (_aliveCount == 0) MarkCleared();
        }

        private void MarkCleared()
        {
            if (Cleared) return;
            Cleared = true;
            Debug.Log($"[RaidGarrisonSpawner] '{configId}' CLEARED — garrison wiped.");
            OnCleared?.Invoke(this);
        }
    }
}
