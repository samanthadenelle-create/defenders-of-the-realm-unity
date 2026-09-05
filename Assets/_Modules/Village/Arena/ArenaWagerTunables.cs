// =============================================================================
// ArenaWagerTunables - the Arena wager tiers + purse multiplier as TUNABLES (WO-1366).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Arena   (STATIC)
//
// Standing rule, owner 2026-09-02 (KEY_FACTS.md): "be smart, dont make it need a code
// change, make it tweakable from a db call". A balance value is a TUNABLE, not a
// constant. 50 / 100 / 200 and the 2x purse were hardcoded in ArenaCatalog.cs until
// this file; with Crystals wagered on Google Play they are the price of a REAL
// currency, so they move here and are read live through the PROD-022 rail.
//
// !! THE DEFAULTS ARE TODAY'S VALUES, EXACTLY. No row, no network, no parse, and -
// until the orchestrator registers the keys - no spec => 50 / 100 / 200 / 200%.
// The amounts are NOT re-picked here (SAMANTHA.md rule 8): they were authored against
// a free 500-seed stub and carry no information about Crystals; the owner feels them
// with the knob live and rules.
//
// TUNABLE (WO-1366): wire to the RemoteTunables rail. This file DECLARES the keys,
// defaults and ranges in ONE place; the rail itself (RemoteTunables.Registry +
// RemoteTunablesService + api/_lib/tunables.js TUNABLE_KEYS + docs/PROD022_TUNABLE_FLAGS.md
// + RemoteTunablesDefaultsRegression) is wired by the CLI orchestrator in its own commit.
// Until a key is registered there, RemoteTunables.Int(key) answers 0 for it
// (RemoteTunables.cs "UNREGISTERED tunable key" path), which would zero every wager -
// so Read() consults RemoteTunables.SpecFor(key) first and falls back to the default
// here. Once the key is registered the rail value is picked up with NO change to this
// file or to any caller.
//
// ASCII-only strings. Pure static; no scene dependency.
// =============================================================================

using DeNelle.Core.Ops;

namespace DeNelle.Village.Arena
{
    /// <summary>
    /// The Arena wager tiers + win-purse multiplier, resolved live through the
    /// RemoteTunables rail with today's hardcoded values as the defaults.
    /// </summary>
    public static class ArenaWagerTunables
    {
        // ---- Keys (lower camel, dotted, ASCII - the TunableSpec.Key grammar) ----

        /// <summary>Tier-1 wager (Ironhold Marauders). TUNABLE (WO-1366): wire to the RemoteTunables rail.</summary>
        public const string KeyWagerTier1 = "arena.wagerTier1";
        /// <summary>Tier-2 wager (Grimwatch Reavers). TUNABLE (WO-1366): wire to the RemoteTunables rail.</summary>
        public const string KeyWagerTier2 = "arena.wagerTier2";
        /// <summary>Tier-3 wager (Blackbanner Host). TUNABLE (WO-1366): wire to the RemoteTunables rail.</summary>
        public const string KeyWagerTier3 = "arena.wagerTier3";
        /// <summary>Win purse as a PERCENT of the wager (200 = stake back + theirs). TUNABLE (WO-1366): wire to the RemoteTunables rail.</summary>
        public const string KeyWinPursePct = "arena.winPursePct";

        // ---- Defaults = the values ArenaCatalog.cs hardcoded before WO-1366 ----

        public const int WagerTier1Default = 50;
        public const int WagerTier2Default = 100;
        public const int WagerTier3Default = 200;
        public const int WinPursePctDefault = 200;

        // ---- Ranges (clamp bounds for the rail row; a wager of 0 would make Arena free) ----

        public const int WagerMin = 1;
        public const int WagerMax = 100000;
        /// <summary>100% = you only get your stake back; below that a WIN loses money.</summary>
        public const int WinPursePctMin = 100;
        public const int WinPursePctMax = 1000;

        /// <summary>The live tier-1 wager.</summary>
        public static long WagerTier1 => Read(KeyWagerTier1, WagerTier1Default, WagerMin, WagerMax);
        /// <summary>The live tier-2 wager.</summary>
        public static long WagerTier2 => Read(KeyWagerTier2, WagerTier2Default, WagerMin, WagerMax);
        /// <summary>The live tier-3 wager.</summary>
        public static long WagerTier3 => Read(KeyWagerTier3, WagerTier3Default, WagerMin, WagerMax);
        /// <summary>The live win-purse percent of the wager.</summary>
        public static int WinPursePct => (int)Read(KeyWinPursePct, WinPursePctDefault, WinPursePctMin, WinPursePctMax);

        /// <summary>The wager for an authored opponent tier (1/2/3). Unknown tiers fall to tier 1.</summary>
        public static long WagerForTier(int tier)
        {
            switch (tier)
            {
                case 2: return WagerTier2;
                case 3: return WagerTier3;
                default: return WagerTier1;
            }
        }

        /// <summary>The purse a win pays for <paramref name="wager"/> (wager * WinPursePct / 100).</summary>
        public static long PurseFor(long wager) => wager * WinPursePct / 100L;

        /// <summary>
        /// Rail read with the unregistered-key guard: a key with no TunableSpec answers the
        /// default HERE (the rail would answer 0), a registered key answers the rail's value
        /// clamped to [min, max]. Never throws.
        /// </summary>
        private static long Read(string key, int def, int min, int max)
        {
            int value = RemoteTunables.SpecFor(key) != null ? RemoteTunables.Int(key) : def;
            if (value < min) value = min;
            if (value > max) value = max;
            return value;
        }
    }
}
