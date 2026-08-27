// =============================================================================
// SiegeStakesBalance -- the AUTHORED bounds of siege bank theft (WO-1026).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Defense
//
// THE OWNER RULING THIS SERVES (2026-08-27, recorded at the end of
// WorkOrders/WORK_ORDER_1026_IMPLEMENTATION_PLAN.md):
//
//     BANK THEFT REPLACES COLLECTOR LOOTING. A siege bills ONCE per attack.
//     A siege takes exactly three things: structural damage, a repair bill, and
//     theft of a PERCENTAGE of UNPROTECTED bank resources under a PROTECTED
//     FLOOR and a PER-ATTACK CAP.
//
//     LOOTABLE      Wood, Iron, Stone, Coins
//     UNTOUCHABLE   Crystals, SKR, purchased goods, equipped gear
//
// ============================ OWNER-PENDING ==================================
// !! EVERY NUMBER THIS FILE SERVES IS PROVISIONAL AND AWAITS AN OWNER RULING. !!
//
// The 2026-08-26 ruling REQUIRES a protected floor and a per-attack cap but gave
// NO numbers, and the retired 2026-08-21 pair (15% steal / 20%-of-capacity floor)
// belongs to a DELETED system -- reusing it as a default would smuggle a superseded
// ruling back in under the new one's name. The MECHANISM lands with the numbers
// open, which is why they live in data (Data/Canonical/siege-stakes.json) behind
// this one clearly-named seam: a ruling is a JSON edit, never a recompile.
//
// THE FIELD DEFAULTS BELOW MIRROR THE AUTHORED JSON. Keep them in step -- a missing
// file must degrade to the same balance the file describes, never to a harsher one.
// =============================================================================
//
// WHY A FRACTION OF CAPACITY AND NOT A FLAT NUMBER (for the capped resources):
//   Storage containers climb to SIX levels (2000 -> 34000, WO-1108b). A flat floor
//   would be most of an early player's store and a rounding error to a late one, so
//   the same rule would mean two different games. Fractions of the ceiling keep the
//   sting proportional. Coins have NO ceiling by design (TownBankCapacity's
//   UncappableResources, owner ruling 2026-08-04), so they -- and only they -- use
//   the two flat numbers.
//
// WHAT IS NOT A KNOB, AND HAS NO FIELD HERE ON PURPOSE:
//   * A HELD defence takes nothing. That is structural (StakeRules.StealFractionFor),
//     not a number someone can tune up to "holding costs a little".
//   * Crystals / SKR / purchased goods / equipped gear. There is no fraction, cap or
//     floor for them because there is no expression anywhere that could take one.
//     SiegeUntouchableRegression fails the gate if that ever stops being true.
// =============================================================================

using Newtonsoft.Json;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.Defense
{
    /// <summary>The parsed siege-stakes.json root. Field defaults ARE the built-in fallback and
    /// mirror the authored file.</summary>
    [System.Serializable]
    public sealed class SiegeStakesData
    {
        /// <summary>Authoring version. Additive changes do not bump it.</summary>
        [JsonProperty("version")] public int Version = 1;

        /// <summary>OWNER-PENDING. Fraction of the UNPROTECTED balance a BREACHED defence loses.
        /// Lower than an overrun on purpose: holding the line partway has to be worth something,
        /// or the report's "your east wall fell first" line has nothing riding on it.</summary>
        [JsonProperty("breachedStealFraction")] public float BreachedStealFraction = 0.05f;

        /// <summary>OWNER-PENDING. Fraction of the UNPROTECTED balance an OVERRUN loses.</summary>
        [JsonProperty("overrunStealFraction")] public float OverrunStealFraction = 0.10f;

        /// <summary>OWNER-PENDING. The PROTECTED FLOOR for a CAPPED resource, as a fraction of that
        /// resource's town-bank ceiling. Anything at or below the floor is untouchable, so a player
        /// who is already down is never kicked.</summary>
        [JsonProperty("protectedFloorFractionOfCapacity")] public float ProtectedFloorFractionOfCapacity = 0.25f;

        /// <summary>OWNER-PENDING. The PER-ATTACK CAP for a CAPPED resource, as a fraction of that
        /// resource's ceiling. One attack can never take more than this, however full the store is.</summary>
        [JsonProperty("perAttackCapFractionOfCapacity")] public float PerAttackCapFractionOfCapacity = 0.05f;

        /// <summary>OWNER-PENDING. The PROTECTED FLOOR for COINS, as a FLAT amount -- coins are
        /// uncapped by design, so a fraction of capacity is undefined for them.</summary>
        [JsonProperty("coinsProtectedFloor")] public int CoinsProtectedFloor = 500;

        /// <summary>OWNER-PENDING. The PER-ATTACK CAP for COINS, as a FLAT amount.</summary>
        [JsonProperty("coinsPerAttackCap")] public int CoinsPerAttackCap = 2000;
    }

    /// <summary>
    /// Static surface over siege-stakes.json -- load + cache + clamped getters. Mirrors
    /// EchoBalanceCatalog's strategy (CanonicalJson: the Resources dual-copy wins, WebGL-safe,
    /// then StreamingAssets), Guard-wrapped with the built-in defaults as the fallback so a
    /// missing or malformed file can never hand the theft arithmetic a wild number.
    /// </summary>
    public static class SiegeStakesBalance
    {
        private const string StreamingRelativePath = "Data/Canonical/siege-stakes.json";
        private const int ExpectedVersion = 1;

        /// <summary>
        /// The hard ceiling on ANY authored steal fraction. A fat-fingered "1.0" in the json must
        /// not be able to strip a store to the floor in one attack; it clamps here and says so.
        /// The bound is deliberately generous -- it is a guard rail, not the ruling.
        /// </summary>
        public const float MaxStealFraction = 0.25f;

        private static SiegeStakesData _data;

        /// <summary>The full parsed balance (never null -- defaults if the file is absent).</summary>
        public static SiegeStakesData Data { get { EnsureLoaded(); return _data; } }

        /// <summary>OWNER-PENDING. Breached steal fraction, clamped to [0, MaxStealFraction].</summary>
        public static float BreachedStealFraction
        {
            get { EnsureLoaded(); return ClampSteal(_data.BreachedStealFraction); }
        }

        /// <summary>OWNER-PENDING. Overrun steal fraction, clamped to [0, MaxStealFraction].</summary>
        public static float OverrunStealFraction
        {
            get { EnsureLoaded(); return ClampSteal(_data.OverrunStealFraction); }
        }

        /// <summary>OWNER-PENDING. Protected-floor fraction of capacity, clamped to [0, 0.9].</summary>
        public static float ProtectedFloorFractionOfCapacity
        {
            get { EnsureLoaded(); return ClampFraction(_data.ProtectedFloorFractionOfCapacity, 0.9f); }
        }

        /// <summary>OWNER-PENDING. Per-attack cap fraction of capacity, clamped to [0, 1].</summary>
        public static float PerAttackCapFractionOfCapacity
        {
            get { EnsureLoaded(); return ClampFraction(_data.PerAttackCapFractionOfCapacity, 1f); }
        }

        /// <summary>OWNER-PENDING. Flat protected floor for coins (never negative).</summary>
        public static int CoinsProtectedFloor
        {
            get { EnsureLoaded(); return _data.CoinsProtectedFloor < 0 ? 0 : _data.CoinsProtectedFloor; }
        }

        /// <summary>OWNER-PENDING. Flat per-attack cap for coins (never negative).</summary>
        public static int CoinsPerAttackCap
        {
            get { EnsureLoaded(); return _data.CoinsPerAttackCap < 0 ? 0 : _data.CoinsPerAttackCap; }
        }

        /// <summary>Force a re-read (oracle / hot-reload).</summary>
        public static void Reload() { _data = null; EnsureLoaded(); }

        private static float ClampSteal(float v)
        {
            if (v < 0f) return 0f;
            return v > MaxStealFraction ? MaxStealFraction : v;
        }

        private static float ClampFraction(float v, float max)
        {
            if (v < 0f) return 0f;
            return v > max ? max : v;
        }

        private static void EnsureLoaded()
        {
            if (_data != null) return;
            _data = LoadData();
        }

        private static SiegeStakesData LoadData()
        {
            var parsed = Guard.Try("Siege", "load siege-stakes.json", () =>
            {
                string json = CanonicalJson.Read(StreamingRelativePath);
                if (string.IsNullOrEmpty(json))
                {
                    FlowTrace.Warn("Siege",
                        "siege-stakes.json not found (Resources or StreamingAssets) -- using the built-in " +
                        "default bounds. They MIRROR the authored file, so the balance is unchanged; a " +
                        "missing file is still a packaging defect worth fixing.");
                    return (SiegeStakesData)null;
                }

                var d = JsonConvert.DeserializeObject<SiegeStakesData>(json);
                if (d == null)
                {
                    FlowTrace.Warn("Siege", "siege-stakes.json parsed null -- using the built-in default bounds.");
                    return (SiegeStakesData)null;
                }

                if (d.Version != ExpectedVersion)
                    FlowTrace.Warn("Siege",
                        "siege-stakes.json version " + d.Version + " != expected " + ExpectedVersion +
                        " -- loading anyway (additive).");

                FlowTrace.Step("Siege",
                    "SiegeStakesBalance loaded (v" + d.Version + ") OWNER-PENDING bounds: steal breached=" +
                    d.BreachedStealFraction + " overrun=" + d.OverrunStealFraction + "; floor=" +
                    d.ProtectedFloorFractionOfCapacity + " of capacity; cap=" +
                    d.PerAttackCapFractionOfCapacity + " of capacity; coins floor=" +
                    d.CoinsProtectedFloor + " cap=" + d.CoinsPerAttackCap + ".");
                return d;
            }, fallback: null);

            if (parsed != null) return parsed;

            FlowTrace.Warn("Siege",
                "SiegeStakesBalance falling back to the built-in default bounds (file missing/invalid).");
            return new SiegeStakesData();
        }
    }
}
