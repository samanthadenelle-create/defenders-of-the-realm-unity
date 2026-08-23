// =============================================================================
// TutorialWaveSpawner — the scripted first-combat wave for the FTUE (WO-277).
// -----------------------------------------------------------------------------
// Scene 3: "HORN BLAST — enemies spawn AT THIS GATE (nearest to hero, not
// random)". This spawns a small, fixed roster (2-3 enemies) at ONE specific
// gate's WaveSpawnPoint by reusing the wave loop's own spawn path —
// WaveManager.SpawnEnemyForExternalMode — so the tutorial enemies are real
// Enemy instances the hero + companion can fight and kill, configured to march
// the Heart exactly like a normal wave.
//
// It deliberately does NOT touch the WaveManager wave loop / wave balance: it
// borrows the spawn helper (already public, built for external modes like the
// Defend-the-Tower shooter) and owns the spawned enemies' lifecycle itself. The
// director awaits IsCleared before granting the post-battle supplies.
//
// Isolation/safety: lives in DeNelle.Village (drives WaveManager + Enemy
// directly). Null-safe — with no WaveManager / no spawn point / no enemy def it
// reports cleared so the tutorial proceeds rather than wedging.
// =============================================================================

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Spawns the tutorial's small first wave at one specific gate (the gate
    /// nearest the hero) via <see cref="WaveManager.SpawnEnemyForExternalMode"/>
    /// and tracks the spawned enemies until they are all dead.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TutorialWaveSpawner : MonoBehaviour
    {
        // Preferred basic enemy id for the teaching wave — a plain melee walker.
        // Falls back to the first catalog entry if this id isn't present.
        //
        // PUBLIC since 2026-08-20 (ordered enemy warm): UpcomingWaveWarmPlanner must know which
        // family the FTUE's "first two enemies" belong to so it can pull that bundle while the
        // player is still placing buildings. It READS this constant rather than copying the id,
        // so the teaching roster keeps exactly ONE authority — change it here and the warm order
        // follows. (The catalog fallback below is deliberately not modelled by the planner: it
        // only fires when the whole catalog is missing, in which case there is no roster to warm.)
        public const string PreferredEnemyId = "hollow-walker";

        // ── Teaching-wave roster (owner directive, felt-test 2026-07-08) ──────────
        // The FIRST/teaching defend-wave must be UNLOSABLE for a first-time player:
        // spawn EXACTLY 2 enemies, both VERY low level, so one prepaid tower clears
        // them easily. We cap + weaken HERE (the tutorial owns its own roster) rather
        // than in the wave loop or the shared enemies.json catalog, so ONLY the
        // tutorial is affected — the ambient wave loop keeps its authored balance.
        //  • Count: 2 (was 3 hollow-walkers).
        //  • Level: HP is dropped to the floor so Enemy.Configure's displayed level
        //    (round(def.Hp / 25)) resolves to Lv 1, and contact damage is trivial.
        private const int   TeachingWaveMaxCount     = 2;    // exactly 2 (down from 3)
        private const float TeachingWaveHp           = 12f;  // → Enemy.Level rounds to Lv 1
        private const float TeachingWaveContactDamage = 2f;  // barely scratches a wall / the hero

        private WaveManager _wave;
        private readonly List<Enemy> _spawned = new List<Enemy>();
        private bool _spawnRequested;

        // Tracks whether we've told BattleMusicManager the teaching-wave combat is live,
        // so the town→battle music swap fires exactly once on spawn and the battle→town
        // swap fires exactly once when the last tutorial enemy dies. The scripted teaching
        // wave bypasses the WaveManager wave loop (it spawns via SpawnEnemyForExternalMode),
        // so it never raises WaveManager.OnWaveStarted — without this explicit signal the
        // defend moment would keep playing town music. See BattleMusicManager.NotifyExternal*.
        private bool _combatMusicActive;

        // BattleLock probe (felt-fix 2026-07-26): registered while the teaching wave is live so
        // HudContextEvaluator/HeroPoseController read Battle mode (the wave bypasses WaveManager.
        // Phase). Stable field so Register/Unregister match the same delegate. Reads _combatMusicActive
        // (true from spawn-live until the last teaching enemy dies), so it self-clears on wave end.
        private System.Func<bool> _battleLockProbe;

        /// <summary>True once every spawned tutorial enemy is dead / gone.</summary>
        public bool IsCleared
        {
            get
            {
                // Not yet asked to spawn → not "cleared" (the director spawns first).
                if (!_spawnRequested) return false;
                PruneDead();
                return _spawned.Count == 0;
            }
        }

        /// <summary>Assigns the wave manager whose spawn path the tutorial reuses.</summary>
        public void SetWaveManager(WaveManager wave) => _wave = wave;

        /// <summary>
        /// Spawns <paramref name="count"/> enemies at <paramref name="spawnPoint"/>
        /// (the gate nearest the hero), marching the Heart. Reuses the wave loop's
        /// own instantiate/Configure path so they are real, killable wave enemies.
        /// </summary>
        public async UniTask SpawnAt(WaveSpawnPoint spawnPoint, int count)
        {
            FlowTrace.Step("TutorialWave", $"SpawnAt count={count} gate={(spawnPoint != null ? spawnPoint.SpawnId : "<null>")}");
            _spawnRequested = true;
            _spawned.Clear();

            if (_wave == null || spawnPoint == null)
            {
                FlowTrace.Warn("TutorialWave", $"skip: WaveManager={(_wave != null)} spawnPoint={(spawnPoint != null)} — IsCleared returns true, tutorial proceeds");
                Debug.LogWarning("[TutorialWaveSpawner] No WaveManager / spawn point — " +
                                 "skipping the tutorial wave (IsCleared returns true).");
                return;
            }

            // Pull a basic enemy def from the SAME catalog the wave loop uses, then
            // clone it into a WEAKENED teaching-wave variant so the tutorial fight is
            // unlosable — the shared catalog def is left untouched (cloning, not
            // mutating, so the ambient wave loop keeps hollow-walker's real stats).
            EnemyCatalog catalog = await _wave.GetEnemyCatalogAsync();
            EnemyDef baseDef = ResolveEnemyDef(catalog);
            if (baseDef == null)
            {
                FlowTrace.Warn("TutorialWave", "enemy catalog empty / no def resolved — no tutorial enemies spawned");
                Debug.LogWarning("[TutorialWaveSpawner] Enemy catalog empty — no tutorial enemies spawned.");
                return;
            }
            EnemyDef def = MakeTeachingVariant(baseDef);

            // The wave loop is held closed during the FTUE, so WaveManager.Heart may
            // not be resolved yet — find the HeartController directly so the tutorial
            // enemies still march the Heart (not a forward-fallback heading).
            HeartController heartCtrl = _wave.Heart != null
                ? _wave.Heart
                : FindAnyObjectByType<HeartController>();
            Transform heart = heartCtrl != null ? heartCtrl.transform : null;
            Vector3 basePos = spawnPoint.transform.position;
            Vector3 heading = spawnPoint.HeadingToGate;
            Vector3 lateral = Vector3.Cross(Vector3.up, heading);

            // Teaching wave is capped to EXACTLY 2 (owner directive): the caller's
            // requested count is honoured only up to that ceiling.
            int n = Mathf.Clamp(count, 1, TeachingWaveMaxCount);
            for (int i = 0; i < n; i++)
            {
                // Fan them out laterally so they advance as a small mob, not a
                // single-file line (mirrors WaveManager.SpawnOne's spread intent).
                float side = (i - (n - 1) * 0.5f) * 2.2f;
                Vector3 pos = basePos + lateral * side;

                string id = $"tutorial-{def.Id}-{i}";
                Enemy e = _wave.SpawnEnemyForExternalMode(def, pos, heart, id);
                if (e == null) { FlowTrace.Warn("TutorialWave", $"SpawnEnemyForExternalMode returned null for '{id}' — skipped"); continue; }

                e.Died += OnEnemyDied;
                _spawned.Add(e);
            }

            FlowTrace.Step("TutorialWave", $"spawned {_spawned.Count}/{n} tutorial enemy(ies)");
            Debug.Log($"[TutorialWaveSpawner] Spawned {_spawned.Count} tutorial enemy(ies) " +
                      $"at {spawnPoint.Direction} gate ({spawnPoint.SpawnId}).");

            // The teaching wave is now LIVE. It bypasses the WaveManager wave loop (spawned
            // via SpawnEnemyForExternalMode), so it never raises WaveManager.OnWaveStarted —
            // signal BattleMusicManager directly so the defend moment stops the town ambient
            // and plays the battle track, exactly like an ambient wave. Paired with the
            // battle→town return in ClearCombatMusicIfDone() when the last enemy dies.
            if (_spawned.Count > 0 && !_combatMusicActive)
            {
                _combatMusicActive = true;
                FlowTrace.Step("TutorialWave", "teaching wave live → BattleMusicManager.NotifyExternalCombatActive()");
                BattleMusicManager.NotifyExternalCombatActive();

                // BATTLE MODE (felt-fix 2026-07-26 "battle mode did NOT trigger"): the teaching
                // wave spawns via SpawnEnemyForExternalMode, bypassing the WaveManager wave loop —
                // so WaveManager.Phase stays Idle and HudContextEvaluator.IsWaveActive() reads
                // FALSE (the defend beat otherwise renders as Town). Its enemies march the Heart,
                // not the hero, so PostureSignals.PursuitActive never trips either. Register a
                // BattleLock probe for the wave's lifetime so HudContextEvaluator (BattleLock.
                // IsInBattle()) + HeroPoseController flip to Battle exactly like an ambient wave.
                // Paired with the unregister in ClearCombatMusicIfDone / OnDestroy.
                if (_battleLockProbe == null) _battleLockProbe = () => _combatMusicActive;
                DeNelle.Core.Combat.BattleLock.RegisterProbe(_battleLockProbe);
                FlowTrace.Step("TutorialWave", "teaching wave live → BattleLock probe registered (HUD/hero enter Battle mode)");
            }
        }

        /// <summary>
        /// When the last tutorial enemy is gone, hand the music back to the town ambient
        /// (battle→town). Fires exactly once, and only if we started the battle music.
        /// </summary>
        private void ClearCombatMusicIfDone()
        {
            if (!_combatMusicActive) return;
            if (_spawned.Count > 0) return;
            _combatMusicActive = false;
            FlowTrace.Step("TutorialWave", "teaching wave cleared → BattleMusicManager.NotifyExternalCombatEnded()");
            BattleMusicManager.NotifyExternalCombatEnded();

            // Leave Battle mode: drop the BattleLock probe now the last teaching enemy is down.
            if (_battleLockProbe != null)
            {
                DeNelle.Core.Combat.BattleLock.UnregisterProbe(_battleLockProbe);
                FlowTrace.Step("TutorialWave", "teaching wave cleared → BattleLock probe unregistered (leave Battle mode)");
            }
        }

        /// <summary>
        /// Clones <paramref name="src"/> into a VERY-low-level teaching variant
        /// (floor HP + trivial contact damage) so the tutorial fight is unlosable,
        /// leaving the shared catalog def untouched. Keeps the same id / family /
        /// model so it is still a real hollow-walker the hero + tower can fight —
        /// only the stats that make it dangerous are dialed to the floor. HP at the
        /// floor also drives <see cref="Enemy"/>'s displayed level (round(Hp/25)) to
        /// Lv 1.
        /// </summary>
        private EnemyDef MakeTeachingVariant(EnemyDef src)
        {
            if (src == null) return null;
            var v = new EnemyDef
            {
                Id             = src.Id,
                Name           = src.Name,
                Family         = src.Family,
                Role           = src.Role,
                Spawn          = src.Spawn != null ? new List<string>(src.Spawn) : null,
                DisplayName    = src.DisplayName,
                ModelKey       = src.ModelKey,
                Ai             = src.Ai,
                Movement       = src.Movement,
                // Weakened for the teaching wave:
                Hp             = TeachingWaveHp,
                MoveSpeed      = src.MoveSpeed,
                ContactDamage  = TeachingWaveContactDamage,
                AttackInterval = src.AttackInterval,
                Height         = src.Height,
                Boss           = src.Boss,
                Flavor         = src.Flavor,
                AggroRadius    = src.AggroRadius,
                GroupStaggerDelay = src.GroupStaggerDelay,
                XpReward       = src.XpReward,
                CoinReward     = src.CoinReward,
            };
            FlowTrace.Step("TutorialWave", $"teaching variant '{v.Id}' hp={v.Hp} contactDamage={v.ContactDamage} (from base hp={src.Hp}/dmg={src.ContactDamage})");
            return v;
        }

        private EnemyDef ResolveEnemyDef(EnemyCatalog catalog)
        {
            if (catalog == null) return null;
            var preferred = catalog.Find(PreferredEnemyId);
            if (preferred != null) return preferred;
            if (catalog.Enemies != null)
                foreach (var d in catalog.Enemies)
                    if (d != null) return d;
            return null;
        }

        private void OnEnemyDied(Enemy enemy)
        {
            if (enemy != null) enemy.Died -= OnEnemyDied;
            _spawned.Remove(enemy);
            // Last teaching enemy down → return the music to the town ambient.
            ClearCombatMusicIfDone();
        }

        private void PruneDead()
        {
            _spawned.RemoveAll(e => e == null || e.IsDead);
        }

        private void OnDestroy()
        {
            // Detach + clean up any lingering tutorial enemies so a torn-down
            // spawner never leaves orphan combatants or stale subscriptions.
            foreach (var e in _spawned)
            {
                if (e == null) continue;
                e.Died -= OnEnemyDied;
                e.Kill();
            }
            _spawned.Clear();
            // Never leave the battle music stuck on if the spawner is torn down mid-fight.
            ClearCombatMusicIfDone();
            // Belt-and-suspenders: drop the BattleLock probe on teardown even if the wave never
            // reached the cleared path (ClearCombatMusicIfDone no-ops when _combatMusicActive is
            // already false), so a torn-down spawner never leaves the HUD stuck in Battle mode.
            if (_battleLockProbe != null)
            {
                DeNelle.Core.Combat.BattleLock.UnregisterProbe(_battleLockProbe);
                _battleLockProbe = null;
            }
        }
    }
}
