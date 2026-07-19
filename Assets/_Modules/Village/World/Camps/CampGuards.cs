// =============================================================================
// CampGuards - the hostile enemy pack that guards a Hostile ClaimableCamp.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World.Camps
//
// WO-216 / DEF-187: a camp is a real early-game ENCOUNTER. When a camp is Hostile
// this spawns a small dedicated group of guards STANDING ON the camp (not the
// ambient RegionMobSpawner wanderers). Each guard:
//   * is built via the shared EnemyFactory (CLAUDE.md §9 - the ONE enemy path),
//   * is region-appropriate (RegionSpawnTable.PickEnemyId) + threat-scaled,
//   * roams a tight tether around the camp until the hero is in range, then aggros
//     (Enemy's own DEF-224 hero-aggro + this tether anchor keep it ON the camp),
//   * raises Enemy.Died -> we count it; when ALL guards are dead the camp clears.
//
// ISOLATION: created/owned entirely by ClaimableCamp at runtime. Reuses EnemyFactory
// + Enemy.Configure + RegionSpawnTable + ZoneManager (all public, read-only). Edits
// NO existing file. Code-built only. ASCII-only. Canon: village is Elarion.
// =============================================================================
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using DeNelle.Core.World;
using DeNelle.Core.State;
using DeNelle.Core.Diagnostics;   // TGVRU — FlowTrace/Guard on the guard spawn path

namespace DeNelle.Village.World.Camps
{
    /// <summary>Spawns + tracks the guard pack that holds a Hostile camp; raises
    /// <see cref="AllCleared"/> once every guard is dead.</summary>
    [DisallowMultipleComponent]
    public sealed class CampGuards : MonoBehaviour
    {
        /// <summary>How many guards a camp spawns (scaled up a touch by threat tier).</summary>
        public const int BaseGuardCount = 3;

        /// <summary>Tether radius the guards roam around the camp centre.</summary>
        public const float GuardTether = 5f;

        /// <summary>Raised once the last living guard dies (the camp is cleared).</summary>
        public event Action AllCleared;

        private RegionId _region;
        private int _threat;
        private Transform _root;
        private readonly List<Enemy> _guards = new List<Enemy>();
        private int _aliveCount;
        private bool _spawned;
        private bool _cleared;

        /// <summary>Number of guards still alive (0 once cleared).</summary>
        public int AliveCount => _aliveCount;

        /// <summary>Total guards this pack started with.</summary>
        public int TotalGuards => _guards.Count;

        /// <summary>Spawn the guard pack around the camp centre. Idempotent.</summary>
        public void Spawn(RegionId region, int threat)
        {
            if (_spawned) return;
            _spawned = true;
            _region = region;
            _threat = Mathf.Max(0, threat);

            _root = new GameObject("[CampGuards]").transform;
            _root.SetParent(transform, false);
            _root.localPosition = Vector3.zero;

            int count = BaseGuardCount + Mathf.Clamp(_threat / 2, 0, 3);  // 3..6 by tier

            // Owner balance (2026-07-16): a hero UNDER the low-level threshold is never swarmed.
            // Cap the camp guard pack to the shared LowLevelEnemyCap so a sub-level-5 player meets
            // at most 3 defenders here (mirrors the arena/family encounter cap).
            int heroLevel = OverworldEncounterSpawner.CurrentHeroLevel();
            if (heroLevel < OverworldEncounterSpawner.LowLevelThreshold && count > OverworldEncounterSpawner.LowLevelEnemyCap)
            {
                FlowTrace.Step("Encounter", $"enemy count capped: level={heroLevel} requested={count} -> {OverworldEncounterSpawner.LowLevelEnemyCap} (camp guards).");
                count = OverworldEncounterSpawner.LowLevelEnemyCap;
            }
            // G — guard EACH spawn independently so one bad guard logs + is skipped, never
            // aborting the pack (which would leave the camp un-clearable on a single fault).
            for (int i = 0; i < count; i++)
            {
                int idx = i;
                Guard.Try("Camp", $"{_region} spawn guard {idx}", () => SpawnOneGuard(idx, count));
            }

            if (_aliveCount == 0)
            {
                // Nothing could spawn (no NavMesh / no roster) - treat as cleared so
                // the camp loop never deadlocks waiting on guards that never existed.
                FlowTrace.Warn("Camp", $"{_region} camp: no guards spawned - auto-clearing (anti-deadlock).");
                RaiseClearedOnce();
            }
        }

        private void SpawnOneGuard(int index, int count)
        {
            Vector3 campCentre = transform.position;

            // A ring of stand positions around the camp centre, NavMesh-snapped.
            float ang = (count > 0 ? (index / (float)count) : 0f) * Mathf.PI * 2f;
            Vector3 want = campCentre + new Vector3(Mathf.Cos(ang), 0f, Mathf.Sin(ang)) * (GuardTether * 0.6f);
            Vector3 pos = want;
            bool snapped = NavMesh.SamplePosition(want, out NavMeshHit hit, GuardTether + 4f, NavMesh.AllAreas);
            if (snapped) pos = hit.position;
            else
                // R — no navmesh under the tether ring; the guard may stand off-mesh and never
                // path/aggro. Self-report so an un-clearable camp never fails silently.
                FlowTrace.Warn("Camp", $"OFF-MESH SPAWN: {_region} camp guard {index} found no navmesh near {want} — may never path/aggro.");

            // Region-appropriate enemy id (falls back to a sensible default off-roster).
            float depth = ZoneManager.Depth(campCentre);
            string enemyId = RegionSpawnTable.HasRoster(_region)
                ? RegionSpawnTable.PickEnemyId(_region, depth, UnityEngine.Random.value)
                : "orc-raider";
            if (string.IsNullOrEmpty(enemyId)) enemyId = "orc-raider";

            var def = BuildGuardDef(enemyId, _threat);

            var enemy = EnemyFactory.Build(def, pos, Quaternion.identity, _root);
            if (enemy == null)
            {
                // R/U — was a SILENT null-return. A missing guard shrinks the pack; report it.
                FlowTrace.Fail("Camp", $"{_region} SpawnOneGuard[{index}]: EnemyFactory returned null for '{enemyId}' at {pos} — guard NOT spawned.");
                return;
            }
            enemy.gameObject.name = $"CampGuard ({enemyId} - {_region})";

            // V — the guard must RENDER, else the hero fights an invisible camp defender.
            VerifyGuardRenders(enemy, $"guard-{index} ({enemyId})", pos);

            // A tight tether anchor at the camp so the guard holds the camp instead of
            // marching the Heart. Enemy.SetBrainTarget(anchor) overrides the Heart-march;
            // Enemy's own DEF-224 hero-aggro still lets it break off to fight a near hero.
            var anchorGo = new GameObject($"GuardAnchor-{index}");
            anchorGo.transform.SetParent(_root, false);
            anchorGo.transform.position = pos;

            enemy.Configure($"campguard-{_region}-{index}", def, anchorGo.transform);
            enemy.SetBrainTarget(anchorGo.transform);

            enemy.Died += HandleGuardDied;
            _guards.Add(enemy);
            _aliveCount++;
        }

        // V — confirm a spawned guard actually RENDERS (>=1 enabled renderer carrying a mesh),
        // else the hero fights an invisible camp defender. EnemyFactory already render-verifies +
        // falls back to a tinted capsule (Wave-0), so this is belt-and-braces: a Warn that
        // self-reports to the break-log; the guard stays in the pack either way (a hittable but
        // invisible defender is still clearable, and removing it could deadlock the camp).
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
                FlowTrace.Warn("Camp",
                    $"INVISIBLE GUARD: {_region} {what} at {pos} has 0 enabled renderers with a mesh — " +
                    "hero would fight an unseen defender (EnemyFactory fallback should have tinted a capsule).");
        }

        private void HandleGuardDied(Enemy enemy)
        {
            if (enemy != null) enemy.Died -= HandleGuardDied;
            _aliveCount = Mathf.Max(0, _aliveCount - 1);
            if (_aliveCount == 0)
                RaiseClearedOnce();
        }

        private void RaiseClearedOnce()
        {
            if (_cleared) return;
            _cleared = true;
            AllCleared?.Invoke();
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _guards.Count; i++)
                if (_guards[i] != null) _guards[i].Died -= HandleGuardDied;
        }

        // Synthesised, threat-scaled guard stat block (mirrors RegionMobSpawner's
        // BuildRoamerDef so guards read the same as the roaming roster). The early-game
        // ease ramp (BestWave-driven) keeps the FIRST camps an "overly easy" power
        // fantasy and scales them up as the player progresses - same dial the rest of
        // the open world uses, so camps never spike difficulty out of band.
        private static EnemyDef BuildGuardDef(string enemyId, int threat)
        {
            float scale = 1f + 0.10f * Mathf.Max(0, threat);

            string id = string.IsNullOrEmpty(enemyId) ? "orc-raider" : enemyId;
            string name; string ai; float hp; float spd; float dmg; float interval; float height; int xp;
            switch (id)
            {
                case "orc-raider":
                    // SSOT: pull orc-raider stats from the single Wildlands roster (enemies.json,
                    // code-fallback matched) so camp guards stay unified with the roamer / outpost /
                    // garrison tables (CombatAtbRegression Check H). Keep the camp-flavored name; the
                    // threat scale + early ease below still apply on top.
                    {
                        var b = WildlandsRoster.BaseDef("orc-raider");
                        name = "Camp Raider"; ai = "charger"; hp = b.Hp; spd = b.MoveSpeed; dmg = b.ContactDamage;
                        interval = b.AttackInterval; height = b.Height; xp = b.XpReward;
                    }
                    break;
                case "caveman":
                    name = "Camp Brute";     ai = "walker";     hp = 70f;  spd = 2.7f; dmg = 9f;  interval = 1.4f; height = 1.9f; xp = 16; break;
                case "feral-wolf":
                    name = "Camp Hound";     ai = "skirmisher"; hp = 42f;  spd = 4.2f; dmg = 7f;  interval = 1.0f; height = 1.2f; xp = 12; break;
                case "tiefling-cultist":
                    name = "Camp Cultist";   ai = "skirmisher"; hp = 80f;  spd = 3.4f; dmg = 11f; interval = 1.2f; height = 1.9f; xp = 20; break;
                case "necromancer":
                    name = "Camp Warden";    ai = "walker";     hp = 140f; spd = 2.2f; dmg = 15f; interval = 1.4f; height = 2.1f; xp = 34; break;
                default:
                    name = "Camp Guard";     ai = "walker";     hp = 60f;  spd = 3.0f; dmg = 8f;  interval = 1.3f; height = 1.8f; xp = 15; break;
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

            // Early-game ease (same ramp as RegionMobSpawner.SpawnMob): a brand-new
            // player meets soft guards (x0.35 HP/damage) that ramp to full by ~BestWave 6.
            float ease = Mathf.Lerp(0.35f, 1f,
                Mathf.Clamp01((GameStateService.Instance?.State?.BestWave ?? 0) / 6f));
            def.Hp = Mathf.Max(1f, def.Hp * ease);
            def.ContactDamage = Mathf.Max(0f, def.ContactDamage * ease);
            return def;
        }
    }
}
