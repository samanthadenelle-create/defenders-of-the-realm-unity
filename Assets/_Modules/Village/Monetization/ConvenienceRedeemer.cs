// =============================================================================
// ConvenienceRedeemer — WO-1246. Spends pack convenience tokens that used to
// accumulate in GearInventory and get read by NOTHING.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Monetization
//
// LEGAL != REDEEMABLE. PackCatalog.ConvenienceAllowList is the covenant firewall;
// PackCatalog.IsRedeemableConvenience is the CURRENT-BUILD consumer list. This
// file is the consumer. Adding a kind here without adding it to
// PackCatalog.RedeemableConvenienceKinds in the SAME commit re-creates the lie
// WO-1118 exists to stop. Wallet cannot reference Village, so the allowlist
// cannot call this type — the two stay in step by the same-commit rule.
//
// FOUR KINDS this file actually spends:
//   instant-build         count token, consumed when a Builder job starts
//   instant-repair        count token, consumed when a Repair job enqueues
//   harvest-auto-collect  24h window; AutoHarvestService ticks CollectAll
//   xp-weekend            24h 2x hero XP window; HeroProgression.AddXp asks
//
// harvest_boost is NOT redeemed here — HarvestBoostService already owns that
// rate-only 2x and is crystal/ad granted, not a pack-token consumer. keepers-satchel
// no longer authors the token.
//
// CLOCK: TimeSource.NowUnixMs() (same as HarvestBoostService / BuildTimerService).
// PERSISTENCE: PlayerPrefs for timed windows (no schema bump; same declared
// limitation as HarvestBoostService). Count tokens live in GameState.GearInventory
// under "convenience:<kind>" — the key PackStoreVM.ApplyPackContents already writes.
//
// ⛔ NEVER COMBAT POWER. Duration 0 on a build/repair is TIME. Auto-collect is a
// tap the player would have made. 2x XP is a weekend rate, not a damage stat.
// =============================================================================

using System.Globalization;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using UnityEngine;

namespace DeNelle.Village.Monetization
{
    /// <summary>
    /// The ONE spender of pack convenience tokens besides <c>Lantern</c>'s expedition oil.
    /// </summary>
    public static class ConvenienceRedeemer
    {
        public const string KindInstantBuild = "instant-build";
        public const string KindInstantRepair = "instant-repair";
        public const string KindHarvestAutoCollect = "harvest-auto-collect";
        public const string KindXpWeekend = "xp-weekend";

        /// <summary>Authored duration of a harvest-auto-collect / xp-weekend charge (24h).</summary>
        public const double TimedWindowSeconds = 24.0 * 3600.0;

        /// <summary>Hard ceiling on the XP weekend multiplier. Stacking extends DURATION.</summary>
        public const float XpMaxMultiplier = 2.0f;

        private const string PrefHarvestEnds = "convenience.harvest-auto-collect.endsatms";
        private const string PrefXpEnds = "convenience.xp-weekend.endsatms";
        private const string PrefXpMult = "convenience.xp-weekend.mult";

        // =====================================================================
        //  Count tokens (instant-build / instant-repair)
        // =====================================================================

        /// <summary>How many charges of <paramref name="kind"/> sit in GearInventory.</summary>
        public static int Count(string kind)
        {
            var inv = Inventory();
            if (inv == null || string.IsNullOrEmpty(kind)) return 0;
            int n = 0;
            if (inv.TryGetValue(InventoryKey(kind), out int a)) n += a;
            string alt = AlternateKey(kind);
            if (alt != null && inv.TryGetValue(alt, out int b)) n += b;
            return n < 0 ? 0 : n;
        }

        /// <summary>
        /// Spends one charge. Returns false when none remain. Persists through GameStateService
        /// so a consumed token cannot resurrect on the next load.
        /// </summary>
        public static bool TryConsume(string kind)
        {
            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            if (state == null)
            {
                FlowTrace.Warn("Convenience", "TryConsume('" + (kind ?? "") + "') with no GameState — nothing spent.");
                return false;
            }
            if (state.GearInventory == null) return false;

            string primary = InventoryKey(kind);
            string alt = AlternateKey(kind);
            if (TryDecrement(state.GearInventory, primary) || (alt != null && TryDecrement(state.GearInventory, alt)))
            {
                FlowTrace.Try("Convenience", "save after consuming '" + kind + "'", () => svc.Save());
                FlowTrace.Step("Convenience", "consumed 1x '" + kind + "' (" + Count(kind) + " left).");
                return true;
            }
            return false;
        }

        /// <summary>
        /// Builder-job hook: spend one instant-build and skip the timer. False leaves duration alone.
        /// </summary>
        public static bool TrySkipBuildTimer()
        {
            if (Count(KindInstantBuild) <= 0) return false;
            if (!TryConsume(KindInstantBuild)) return false;
            FlowTrace.Step("Convenience", "instant-build: Builder job duration skipped.");
            return true;
        }

        /// <summary>
        /// Repair-job hook: spend one instant-repair and skip the timer. False leaves duration alone.
        /// </summary>
        public static bool TrySkipRepairTimer()
        {
            if (Count(KindInstantRepair) <= 0) return false;
            if (!TryConsume(KindInstantRepair)) return false;
            FlowTrace.Step("Convenience", "instant-repair: Repair job duration skipped.");
            return true;
        }

        // =====================================================================
        //  Timed windows (harvest-auto-collect / xp-weekend)
        // =====================================================================

        /// <summary>True while a harvest-auto-collect window is running RIGHT NOW.</summary>
        public static bool IsHarvestAutoCollectActive => TimedActive(PrefHarvestEnds);

        /// <summary>True while an xp-weekend window is running RIGHT NOW.</summary>
        public static bool IsXpWeekendActive => TimedActive(PrefXpEnds);

        /// <summary>
        /// AutoHarvestService asks this every tick. Starts a 24h window from inventory when
        /// none is running. The Ancient Sawmill perk is a SEPARATE, permanent flag — this
        /// does not consume a token while that perk already covers auto-collect.
        /// </summary>
        public static bool HarvestAutoCollectShouldTick(bool perkOwnsAutoCollect)
        {
            if (perkOwnsAutoCollect) return true;
            if (IsHarvestAutoCollectActive) return true;
            if (Count(KindHarvestAutoCollect) <= 0) return false;
            if (!TryConsume(KindHarvestAutoCollect)) return false;
            StartTimed(PrefHarvestEnds, TimedWindowSeconds, "harvest-auto-collect");
            return true;
        }

        /// <summary>
        /// HeroProgression.AddXp asks this. Returns 1 when nothing is running and no token
        /// remains; otherwise 2 (capped). A first grant starts the 24h window.
        /// </summary>
        public static float XpMultiplier()
        {
            if (IsXpWeekendActive)
            {
                float m = (float)ReadDouble(PrefXpMult);
                if (m < 1f) m = XpMaxMultiplier;
                return Mathf.Min(m, XpMaxMultiplier);
            }
            if (Count(KindXpWeekend) <= 0) return 1f;
            if (!TryConsume(KindXpWeekend)) return 1f;
            WriteDouble(PrefXpMult, XpMaxMultiplier);
            StartTimed(PrefXpEnds, TimedWindowSeconds, "xp-weekend");
            return XpMaxMultiplier;
        }

        /// <summary>Test/QA reset. Never called by gameplay.</summary>
        public static void ClearTimedWindowsForTests()
        {
            PlayerPrefs.DeleteKey(PrefHarvestEnds);
            PlayerPrefs.DeleteKey(PrefXpEnds);
            PlayerPrefs.DeleteKey(PrefXpMult);
            PlayerPrefs.Save();
        }

        // =====================================================================
        //  Internals
        // =====================================================================

        private static System.Collections.Generic.Dictionary<string, int> Inventory()
        {
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            return state != null ? state.GearInventory : null;
        }

        private static string InventoryKey(string kind)
        {
            if (string.IsNullOrEmpty(kind)) return "convenience:";
            return "convenience:" + kind.Trim().ToLowerInvariant();
        }

        /// <summary>The hyphen/underscore twin of the authored key, or null when they already match.</summary>
        private static string AlternateKey(string kind)
        {
            if (string.IsNullOrEmpty(kind)) return null;
            string k = kind.Trim().ToLowerInvariant();
            string swapped = k.IndexOf('-') >= 0 ? k.Replace('-', '_') : k.Replace('_', '-');
            if (string.Equals(swapped, k, System.StringComparison.Ordinal)) return null;
            return "convenience:" + swapped;
        }

        private static bool TryDecrement(System.Collections.Generic.Dictionary<string, int> inv, string key)
        {
            if (string.IsNullOrEmpty(key) || !inv.TryGetValue(key, out int n) || n <= 0) return false;
            inv[key] = n - 1;
            return true;
        }

        private static bool TimedActive(string endsKey)
        {
            double ends = ReadDouble(endsKey);
            if (ends <= 0.0) return false;
            double now = TimeSource.NowUnixMs();
            if (ends > now) return true;
            PlayerPrefs.DeleteKey(endsKey);
            PlayerPrefs.Save();
            FlowTrace.Step("Convenience", endsKey + " EXPIRED.");
            return false;
        }

        private static void StartTimed(string endsKey, double seconds, string source)
        {
            double now = TimeSource.NowUnixMs();
            double current = ReadDouble(endsKey);
            bool extending = current > now;
            double newEnds = (extending ? current : now) + (seconds * 1000.0);
            WriteDouble(endsKey, newEnds);
            PlayerPrefs.Save();
            FlowTrace.Step("Convenience",
                (extending ? "EXTENDED" : "STARTED") + " '" + source + "' window +" + (seconds / 3600.0).ToString("0.#") +
                "h -> ends " + newEnds.ToString("F0") + ".");
        }

        private static double ReadDouble(string key)
        {
            string raw = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrEmpty(raw)) return 0.0;
            return double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out double v) ? v : 0.0;
        }

        private static void WriteDouble(string key, double value) =>
            PlayerPrefs.SetString(key, value.ToString("R", CultureInfo.InvariantCulture));
    }
}
