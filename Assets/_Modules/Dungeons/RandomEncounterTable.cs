// =============================================================================
// RandomEncounterTable — seeded random-encounter math for dungeons (Week 6).
// -----------------------------------------------------------------------------
// Port spec Part 3 row:
//   src/modules/dungeons/encounters/ -> EncounterTrigger.cs + RandomEncounterTable.cs
//
// C# port of src/modules/dungeons/3d/encounters/encounterTriggers.ts — the
// pure decision layer of the dungeon encounter engine. Given the run's
// cooldown clock + a few flags, it decides DETERMINISTICALLY whether a random
// encounter should fire this tick and, if so, which enemies it spawns.
//
// Pure C# — no MonoBehaviour, no Unity types beyond Mathf (port spec Part 3:
// "Pure C# for engine math … Easy to unit-test"). EncounterTrigger owns the
// scene wiring; this owns the math.
//
// ── v1 status ──
// The Healer's Cottage gates random encounters OFF
// (DungeonLayout.disableRandomEncounters) — v1 ships scripted, room-placed
// fights only. This table is the v1.1-ready engine; it stays intact and tested
// so v1.1 flips the dungeon flag with no code change.
//
// ── Determinism ──
// The roll consumes a seedable xorshift PRNG (matching DeNelle.BattleATB.Engine
// .Rng's algorithm — same family the ATB engine uses). Same seed + same inputs
// = same verdict sequence. NEVER call UnityEngine.Random here.
// =============================================================================

using UnityEngine;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// The seeded random-encounter decision engine for a dungeon — port of
    /// <c>encounterTriggers.ts</c>. Owns the cooldown / quiet-stretch ramp /
    /// reward-cap math. Pure: holds only the dungeon tier; the run's clock +
    /// counters are passed into <see cref="Roll"/> each tick.
    /// </summary>
    public sealed class RandomEncounterTable
    {
        // ── Tuning constants (encounterTriggers.ts — verbatim) ───────────────

        /// <summary>Hard cooldown after any encounter — no random fire for this long.</summary>
        public const float CooldownSec = 25f;

        /// <summary>Base per-second random-encounter probability past the cooldown.</summary>
        public const float BaseRatePerSec = 0.012f;

        /// <summary>Per-tier rate multiplier — index by <c>tier - 1</c>.</summary>
        public static readonly float[] TierRate = { 1.0f, 1.3f, 1.7f };

        /// <summary>Rate multiplier applied while the Keeper explores in the dark.</summary>
        public const float DarknessRateMult = 1.6f;

        /// <summary>The quiet-stretch ramp — rate lift per second past the cooldown.</summary>
        public const float QuietRampPerSec = 0.03f;

        /// <summary>Ceiling on the quiet-stretch ramp multiplier.</summary>
        public const float QuietRampMaxMult = 3.0f;

        /// <summary>Only the first 12 random encounters of a run pay out (anti-farm).</summary>
        public const int RewardCap = 12;

        /// <summary>Encounter-pool bucket weight — apprentice 60%.</summary>
        public const float WeightApprentice = 0.6f;
        /// <summary>Encounter-pool bucket weight — veteran 30%.</summary>
        public const float WeightVeteran = 0.3f;
        // cellar = the remaining 10%.

        // ── State ────────────────────────────────────────────────────────────

        /// <summary>The dungeon's difficulty tier (1-based).</summary>
        private readonly int _tier;

        /// <summary>The xorshift PRNG cursor — advances every consumed draw.</summary>
        private uint _rngState;

        /// <summary>Creates a table for a dungeon of <paramref name="tier"/> (1-based).</summary>
        public RandomEncounterTable(int tier)
        {
            _tier = Mathf.Max(1, tier);
            _rngState = 0x9E3779B9u; // seeded properly on the first Roll() call
        }

        // ── The verdict ──────────────────────────────────────────────────────

        /// <summary>The outcome of one <see cref="Roll"/> — port of <c>EncounterTriggerVerdict</c>.</summary>
        public readonly struct Verdict
        {
            /// <summary>True when a random encounter should fire this tick.</summary>
            public readonly bool Fire;
            /// <summary>True when the firing encounter grants loot (honours the cap).</summary>
            public readonly bool Rewarded;
            /// <summary>The effective per-tick probability the roll used — for dev HUD.</summary>
            public readonly float RolledChance;

            public Verdict(bool fire, bool rewarded, float rolledChance)
            {
                Fire = fire;
                Rewarded = rewarded;
                RolledChance = rolledChance;
            }

            /// <summary>A negative verdict — no encounter, no reward.</summary>
            public static readonly Verdict NoFire = new Verdict(false, false, 0f);
        }

        /// <summary>
        /// Decides whether a random encounter should fire this tick. Port of
        /// <c>shouldFireRandomEncounter</c>.
        ///
        /// Gating (any → immediate NoFire, no RNG draw): no time step; the
        /// cooldown has not elapsed. (Checkpoint / boss-room and in-combat
        /// gating is done by the caller, <see cref="EncounterTrigger"/>.)
        ///
        /// Past the gates the effective per-tick chance is
        /// <c>base × tierMult × darknessMult × quietRampMult × dt</c> and the
        /// roll fires when the next PRNG draw is below it.
        /// </summary>
        /// <param name="secondsSinceLastEncounter">The run's cooldown clock.</param>
        /// <param name="randomEncounterCount">Random encounters fired so far this run.</param>
        /// <param name="inDarkness">True while exploring without enough light.</param>
        /// <param name="dtSeconds">The roll-tick duration (the frame dt).</param>
        /// <param name="seed">Per-roll seed — keeps the sequence replay-stable.</param>
        public Verdict Roll(
            float secondsSinceLastEncounter,
            int randomEncounterCount,
            bool inDarkness,
            float dtSeconds,
            int seed)
        {
            if (dtSeconds <= 0f) return Verdict.NoFire;
            if (secondsSinceLastEncounter < CooldownSec) return Verdict.NoFire;

            SeedRng(seed);

            float tierMult = (_tier - 1 >= 0 && _tier - 1 < TierRate.Length)
                ? TierRate[_tier - 1]
                : TierRate[0];
            float darknessMult = inDarkness ? DarknessRateMult : 1f;

            float secsPastCooldown = secondsSinceLastEncounter - CooldownSec;
            float quietRampMult = Mathf.Min(
                QuietRampMaxMult,
                1f + Mathf.Max(0f, secsPastCooldown) * QuietRampPerSec);

            float rolledChance =
                BaseRatePerSec * tierMult * darknessMult * quietRampMult * dtSeconds;

            bool fire = NextFloat() < rolledChance;
            if (!fire) return new Verdict(false, false, rolledChance);

            // The 13th+ random encounter still fires but pays nothing.
            bool rewarded = randomEncounterCount < RewardCap;
            return new Verdict(true, rewarded, rolledChance);
        }

        // ── Enemy roster ─────────────────────────────────────────────────────

        /// <summary>
        /// Rolls the enemy roster for a random encounter. Port of
        /// <c>rollEncounterEnemies</c>. Tier 1 spawns one enemy; tier 2+ spawns
        /// 2-3. Each slot is drawn from the weighted pool (apprentice 60% /
        /// veteran 30% / cellar 10%), falling across buckets when one is empty.
        /// </summary>
        public string[] RollEnemies(DungeonEncounterPool pool)
        {
            if (pool == null) return System.Array.Empty<string>();

            int count = _tier <= 1 ? 1 : (NextFloat() < 0.5f ? 2 : 3);
            var enemies = new System.Collections.Generic.List<string>(count);
            for (int i = 0; i < count; i++)
            {
                string picked = PickFromPool(pool);
                if (!string.IsNullOrEmpty(picked)) enemies.Add(picked);
            }
            return enemies.ToArray();
        }

        /// <summary>
        /// Picks one enemy-type id from the weighted pool buckets, falling across
        /// buckets so a sparse pool never yields an empty result. Port of
        /// <c>pickFromPool</c>.
        /// </summary>
        private string PickFromPool(DungeonEncounterPool pool)
        {
            float r = NextFloat();
            string[][] order;
            if (r < WeightApprentice)
                order = new[] { pool.apprentice, pool.veteran, pool.cellar };
            else if (r < WeightApprentice + WeightVeteran)
                order = new[] { pool.veteran, pool.apprentice, pool.cellar };
            else
                order = new[] { pool.cellar, pool.veteran, pool.apprentice };

            foreach (var bucket in order)
            {
                if (bucket != null && bucket.Length > 0)
                    return bucket[NextIndex(bucket.Length)];
            }
            return null;
        }

        // ── Seeded xorshift PRNG (matches the ATB engine's Rng family) ───────

        /// <summary>Seeds the PRNG cursor — a non-zero state from any int seed.</summary>
        private void SeedRng(int seed)
        {
            uint s = unchecked((uint)seed);
            _rngState = s == 0u ? 0x9E3779B9u : s;
        }

        /// <summary>Advances the xorshift cursor and returns the raw 32-bit value.</summary>
        private uint NextUInt()
        {
            uint x = _rngState;
            x ^= x << 13;
            x ^= x >> 17;
            x ^= x << 5;
            _rngState = x;
            return x;
        }

        /// <summary>The next PRNG draw as a float in [0, 1).</summary>
        private float NextFloat() => (NextUInt() & 0xFFFFFFu) / 16777216f;

        /// <summary>The next PRNG draw as an index in [0, <paramref name="length"/>).</summary>
        private int NextIndex(int length) =>
            length <= 0 ? 0 : (int)(NextUInt() % (uint)length);
    }
}
