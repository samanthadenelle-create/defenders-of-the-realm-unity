// =============================================================================
// Canonical data — WaveData / EnemyCatalog loader tests (EditMode)
// -----------------------------------------------------------------------------
// qa-test-plan.md TC-XC-09 / TC-VIL-10: waves.json + enemies.json must load via
// the async WaveDataLoader and hydrate the typed wave schedule + village-enemy
// catalog.
//
// WaveDataLoader is ASYNC (UniTask) because Android packs StreamingAssets inside
// the APK. In the Editor StreamingAssets is a plain directory, so the UniTask
// completes synchronously on the same frame — these EditMode tests therefore
// drive it with `.GetAwaiter().GetResult()`, no PlayMode coroutine needed.
// =============================================================================

using Cysharp.Threading.Tasks;
using NUnit.Framework;
using DeNelle.Village;

namespace DeNelle.Data.Tests
{
    [TestFixture]
    public class WaveDataTest
    {
        /// <summary>
        /// Synchronously drains a UniTask. The Editor reads StreamingAssets from a
        /// real directory via File.ReadAllTextAsync, which completes immediately —
        /// so blocking on the result is safe here.
        /// </summary>
        private static T Await<T>(UniTask<T> task)
        {
            return task.GetAwaiter().GetResult();
        }

        // =====================================================================
        //  waves.json
        // =====================================================================

        [Test]
        public void waves_json_loads_a_non_empty_schedule()
        {
            var schedule = Await(WaveDataLoader.LoadWavesAsync());
            Assert.That(schedule, Is.Not.Null, "waves.json must load.");
            Assert.That(schedule.Waves, Is.Not.Null.And.Not.Empty,
                "the wave schedule must contain at least one wave.");
        }

        [Test]
        public void wave_one_is_the_authored_first_light_wave()
        {
            var schedule = Await(WaveDataLoader.LoadWavesAsync());
            var w1 = schedule.Find(1);
            Assert.That(w1, Is.Not.Null, "waves.json must contain wave 1.");
            Assert.That(w1.WaveId, Is.EqualTo(1));
            Assert.That(w1.TotalEnemyCount, Is.GreaterThan(0),
                "wave 1 must release at least one enemy.");
            Assert.That(w1.CountdownSeconds, Is.GreaterThan(0f),
                "wave 1 must have a Prepare-Phase countdown.");
        }

        [Test]
        public void every_wave_has_a_unique_ascending_id_and_at_least_one_batch()
        {
            var schedule = Await(WaveDataLoader.LoadWavesAsync());
            int prevId = 0;
            var seen = new System.Collections.Generic.HashSet<int>();
            foreach (var wave in schedule.Waves)
            {
                Assert.That(wave, Is.Not.Null);
                Assert.That(seen.Add(wave.WaveId), Is.True,
                    $"duplicate waveId {wave.WaveId}.");
                Assert.That(wave.WaveId, Is.GreaterThan(prevId),
                    "wave ids must be authored in ascending order.");
                prevId = wave.WaveId;
                // A wave is valid if it spawns a normal-enemy batch OR is a
                // boss-only wave (e.g. wave 4 is the apex-dragon BOSS wave with
                // enemies: [] by design — the dragon spawns from the boss/apexBoss
                // block, see waves.json _comment). Mirrors the sibling cross-ref
                // test that tolerates boss-only waves via wave.Boss.
                bool hasBatch = wave.Enemies != null && wave.Enemies.Count > 0;
                bool hasBoss = !string.IsNullOrEmpty(wave.Boss) || wave.IsApexBossWave;
                Assert.That(hasBatch || hasBoss, Is.True,
                    $"wave {wave.WaveId} must have at least one spawn batch " +
                    "or a boss/apexBoss block.");
            }
        }

        [Test]
        public void total_enemy_count_sums_every_batch()
        {
            var schedule = Await(WaveDataLoader.LoadWavesAsync());
            foreach (var wave in schedule.Waves)
            {
                int expected = 0;
                foreach (var batch in wave.Enemies)
                    expected += System.Math.Max(0, batch.Count);
                Assert.That(wave.TotalEnemyCount, Is.EqualTo(expected),
                    $"wave {wave.WaveId} TotalEnemyCount must sum every batch's count.");
            }
        }

        [Test]
        public void find_returns_null_for_an_unscheduled_wave()
        {
            var schedule = Await(WaveDataLoader.LoadWavesAsync());
            Assert.That(schedule.Find(9999), Is.Null);
        }

        // =====================================================================
        //  enemies.json
        // =====================================================================

        [Test]
        public void enemies_json_loads_a_non_empty_catalog()
        {
            var catalog = Await(WaveDataLoader.LoadEnemiesAsync());
            Assert.That(catalog, Is.Not.Null, "enemies.json must load.");
            Assert.That(catalog.Enemies, Is.Not.Null.And.Not.Empty);
        }

        [Test]
        public void the_canon_hollow_walker_is_present()
        {
            var catalog = Await(WaveDataLoader.LoadEnemiesAsync());
            var walker = catalog.Find("hollow-walker");
            Assert.That(walker, Is.Not.Null,
                "enemies.json must contain the Wave-1 Hollow Walker.");
            Assert.That(walker.AiKind, Is.EqualTo(EnemyAiKind.Walker),
                "the Hollow Walker's ai token must parse to Walker.");
        }

        [Test]
        public void every_enemy_has_valid_stats()
        {
            var catalog = Await(WaveDataLoader.LoadEnemiesAsync());
            foreach (var enemy in catalog.Enemies)
            {
                Assert.That(enemy, Is.Not.Null);
                Assert.That(string.IsNullOrEmpty(enemy.Id), Is.False, "every enemy has an id.");
                Assert.That(enemy.Hp, Is.GreaterThan(0f), $"{enemy.Id} HP must be > 0.");
                Assert.That(enemy.MoveSpeed, Is.GreaterThan(0f), $"{enemy.Id} moveSpeed must be > 0.");
                Assert.That(enemy.ContactDamage, Is.GreaterThanOrEqualTo(0f),
                    $"{enemy.Id} contactDamage must be >= 0.");
                Assert.That(enemy.AttackInterval, Is.GreaterThan(0f),
                    $"{enemy.Id} attackInterval must be > 0.");
                Assert.That(System.Enum.IsDefined(typeof(EnemyAiKind), enemy.AiKind), Is.True,
                    $"{enemy.Id} ai token must resolve to a known archetype.");
            }
        }

        [Test]
        public void exactly_one_enemy_is_flagged_boss()
        {
            // enemies.json — the Necromancer leads boss waves.
            var catalog = Await(WaveDataLoader.LoadEnemiesAsync());
            int bossCount = 0;
            foreach (var enemy in catalog.Enemies)
                if (enemy.Boss) bossCount++;
            Assert.That(bossCount, Is.EqualTo(1),
                "exactly one village enemy (the Necromancer) must carry the boss flag.");
        }

        [Test]
        public void enemy_find_returns_null_for_an_unknown_id()
        {
            var catalog = Await(WaveDataLoader.LoadEnemiesAsync());
            Assert.That(catalog.Find("griffin"), Is.Null);
            Assert.That(catalog.Find(null), Is.Null);
        }

        // =====================================================================
        //  Cross-reference — every wave batch keys into the enemy catalog
        // =====================================================================

        [Test]
        public void every_wave_batch_type_resolves_in_the_enemy_catalog()
        {
            var schedule = Await(WaveDataLoader.LoadWavesAsync());
            var catalog = Await(WaveDataLoader.LoadEnemiesAsync());

            foreach (var wave in schedule.Waves)
            {
                foreach (var batch in wave.Enemies)
                {
                    Assert.That(catalog.Find(batch.Type), Is.Not.Null,
                        $"wave {wave.WaveId} references enemy '{batch.Type}' " +
                        "which is not in enemies.json.");
                }
                if (!string.IsNullOrEmpty(wave.Boss))
                {
                    Assert.That(catalog.Find(wave.Boss), Is.Not.Null,
                        $"wave {wave.WaveId} boss '{wave.Boss}' is not in enemies.json.");
                }
            }
        }
    }
}
