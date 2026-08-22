// =============================================================================
// RaidCooldownService — the per-camp raid COOLDOWN (WO-728, unblocking WO-1134).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World.Camps
//
// THE GAP THIS CLOSES: raids were repeatable INSTANTLY. RaidClaimService already
// separates a first clear from a replay (a replay pays a fraction of ordinary
// resources and never crystals), but nothing stopped the player re-entering the
// same camp the second the victory screen dismissed. A raid you can re-run without
// waiting is not a loop, it is a FAUCET — and WO-1134's measured model makes the
// repeatable endgame the retention answer, so the wait is the mechanic.
//
// SIBLING, NOT REPLACEMENT, OF RaidClaimService:
//   * RaidClaimService answers "have I EVER taken this camp?" -> the LOOT gate.
//   * This answers "may I raid it AGAIN YET?"                 -> the ENTRY gate.
//   Two different questions with two different lifetimes; folding them into one
//   flag would make the first clear's permanent record expire, which is exactly
//   the bug the 2026-08-15 sweep closed.
//
// =============================================================================
//  ⛔ CLOCK DISCIPLINE — READ THIS BEFORE TOUCHING ANY TIME LINE
// =============================================================================
// EVERY "now" here comes from TimeSource.NowUnixMs(). NEVER DateTime.UtcNow, never
// DateTimeOffset.UtcNow, never Time.time. TimeSource is server-anchored when a
// handshake has happened this process (ServerClock anchors to a MONOTONIC Stopwatch,
// so a wall-clock edit cannot move it). A cooldown stamped off the device clock is
// rolled forward in ten seconds by anyone who opens Settings > Date & Time — which
// makes the entire retention mechanic optional.
//
// WHEN THE CLOCK IS NOT ANCHORED (cold launch, offline, never reached the backend):
// we STILL run the cooldown off the device fallback, and we RECORD that we did
// (RaidCooldownRecord.ServerAnchored). We do NOT refuse to raid and we do NOT
// lengthen the wait. A cold launch is ALWAYS unanchored, so punishing an unanchored
// clock taxes every honest offline player on every launch — the WO-1128 ruling:
// refuse server-side, never punish client-side. The recorded flag is what lets a
// server reconcile it later; nothing here branches on it.
//
// A BACKWARDS CLOCK — REFUSE, DON'T PUNISH (BuildTimerService.RollWindowIfNeeded's
// pattern, WO-912 §7.3). If now < StartedUnixMs the clock moved backwards since the
// stamp. Left alone that reads as a NEGATIVE elapsed, i.e. a cooldown that keeps
// growing — a player who crossed a timezone or corrected a wrong clock would be
// locked out of a camp indefinitely. So we RE-STAMP the window to now: the player
// waits AT MOST one full duration, never longer, and never LESS (a backwards clock
// can therefore never shorten a cooldown — the whole point). We log it, and we do
// NOT wipe the record, flag the account, or tell the player they were caught. A
// false positive here is ordinary life (DST, a dead coin cell, a corrected clock);
// breaking a paying player's save over it would be the worse error. We also do not
// teach an attacker what the detector measures.
//
// PERSISTENCE: GameState.RaidCooldowns -> SaveSchema "raidCooldowns", additive and
// default-on-read, so NO SCHEMA BUMP (a version bump on a LIVE published game is an
// OWNER decision). Deliberately NOT PlayerPrefs, unlike RaidClaimService: WO-728
// task 4 calls for it in the save, and a cooldown that survives a reinstall but not
// a cloud restore is worse than one that survives neither.
//
// ⚠ THE DURATIONS BELOW ARE A PROPOSAL AWAITING AN OWNER RULING — see DurationTable.
//
// ASCII-only. Canon: the village is Elarion (never Avalon).
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using DeNelle.Core.UI;

namespace DeNelle.Village.World.Camps
{
    /// <summary>
    /// The per-camp raid cooldown: how long after a clear a camp stays un-raidable,
    /// persisted in the save and measured on the server-anchored clock.
    /// </summary>
    public static class RaidCooldownService
    {
        // =====================================================================
        //  ⛔ BALANCE — OWNER RULING 2026-08-21. THESE NUMBERS ARE NOT A FEEL KNOB.
        // =====================================================================
        //      Regular ("raider_camp_small")    4 h
        //      Hard    ("fortified_garrison")   8 h
        //      Extreme ("mage_enclave")        12 h
        //
        //  WHY, recorded here so nobody "optimises" it later:
        //
        //  ⭑ THIS NUMBER IS THE CRYSTAL BOUND. It is not pacing garnish.
        //    RaidScoring.ComputeLoot returns a ResourceCost of FOOD and CRYSTALS and
        //    NOTHING ELSE — raids pay zero wood and zero iron, while every troop costs
        //    wood + iron + food. So the raid loop structurally cannot fund its own
        //    input, food is already capped by storage, and CRYSTALS ARE THE ONE
        //    UNBOUNDED FAUCET IN THE GAME. This cooldown is the only thing bounding it.
        //
        //  ⭑ THE ARITHMETIC THE RULING WAS MADE ON: an Extreme clear at 3 stars / 100%
        //    razed pays 121 crystals. At 12 h that is ~2 clears/day = ~242 crystals/day,
        //    sitting alongside the 200-350/day committed income the WO-1129 economy
        //    model measured. That roughly DOUBLES endgame crystal income without
        //    trivialising the 45,690-crystal content ladder.
        //
        //  ⭑ WHY SHORTENING IT IS SELF-DEFEATING: crystals buy INSTANT-FINISH on the
        //    Obsidian queue. A shorter cooldown therefore defunds the very timer ladder
        //    that paces the whole game — it does not make the game faster, it deletes
        //    the thing the game is made of. "It feels like a long wait in a play
        //    session" is not evidence against it; a play session is not a day.
        //
        //  The table below is the FALLBACK. The ruled numbers are ALSO authored per
        //  camp in scene-configs.json (raidCooldownSeconds), which wins — so a retune
        //  is a data edit. The two must be kept in agreement; RaidCooldownRegression
        //  pins that every authored camp matches its difficulty's ruled default.
        // =====================================================================

        /// <summary>OWNER-RULED default for a Regular-difficulty camp: 4 h (seconds).</summary>
        public const double DefaultRegularSeconds = 4d * 60d * 60d;
        /// <summary>OWNER-RULED default for a Hard-difficulty camp: 8 h (seconds).</summary>
        public const double DefaultHardSeconds = 8d * 60d * 60d;
        /// <summary>OWNER-RULED default for an Extreme-difficulty camp: 12 h (seconds).</summary>
        public const double DefaultExtremeSeconds = 12d * 60d * 60d;

        /// <summary>
        /// The cooldown a camp of this difficulty runs, in seconds. PURE + static (no save,
        /// no scene, no catalog) so an oracle can assert the table with nothing loaded.
        /// An unknown/blank difficulty falls back to Regular — the FORGIVING direction:
        /// a mis-authored camp must never inherit the six-hour lockout.
        /// </summary>
        public static double DurationForDifficulty(string difficulty)
        {
            switch ((difficulty ?? "Regular").Trim().ToLowerInvariant())
            {
                case "extreme": return DefaultExtremeSeconds;
                case "hard":    return DefaultHardSeconds;
                default:        return DefaultRegularSeconds;
            }
        }

        /// <summary>
        /// The cooldown this specific camp runs. An authored
        /// <c>SceneConfigDef.raidCooldownSeconds</c> &gt; 0 WINS (the owner's per-camp
        /// override, tunable in scene-configs.json with no code change); otherwise the
        /// difficulty table applies. A null def resolves to the Regular default rather
        /// than to zero, so a lookup miss can never silently remove the cooldown.
        /// </summary>
        public static double DurationFor(SceneConfigDef def)
        {
            if (def == null) return DefaultRegularSeconds;
            if (def.raidCooldownSeconds > 0f) return def.raidCooldownSeconds;
            return DurationForDifficulty(def.difficulty);
        }

        /// <summary>The cooldown the camp with this config id runs (catalog lookup + <see cref="DurationFor"/>).</summary>
        public static double DurationForConfigId(string configId)
        {
            if (string.IsNullOrEmpty(configId)) return DefaultRegularSeconds;
            SceneConfigDef def = null;
            Guard.Try("Raid", "resolve scene-config for cooldown", () => { def = SceneConfigCatalog.Find(configId); });
            return DurationFor(def);
        }

        // =====================================================================
        //  Reading the cooldown
        // =====================================================================

        /// <summary>
        /// Seconds until <paramref name="configId"/> is raidable again; 0 when it is
        /// raidable RIGHT NOW (no record, an expired record, or no save loaded).
        ///
        /// <para>SIDE EFFECT BY DESIGN: an expired record is pruned and a BACKWARDS clock
        /// re-stamps the window (see the file header). Both are self-healing repairs, both
        /// are traced, and both mark the save dirty through <see cref="Persist"/> so the
        /// repair survives. A pure query that leaves a corrupted window in place would just
        /// re-detect it every frame.</para>
        /// </summary>
        public static double RemainingSeconds(string configId)
        {
            if (string.IsNullOrEmpty(configId)) return 0d;
            var list = Records();
            if (list == null)
            {
                // §12, NO SILENT FAILURES. Answering 0 here means "raidable", i.e. the FORGIVING
                // direction -- correct, because refusing to raid over a missing save would punish
                // the player for our problem. But it must never be SILENT: a returned 0 is
                // indistinguishable from "the window elapsed", so an absent save reads exactly like
                // a healthy expired cooldown, in the one direction that costs us the mechanic.
                // This is the trace that names the difference (and it is what turned a fixture that
                // had demolished its own GameState into a one-line read instead of a clock theory).
                FlowTrace.Warn("Raid",
                    "raid cooldown read on '" + configId + "' with NO GameState available -- reporting " +
                    "RAIDABLE (0s) because refusing over a missing save would punish the player, but " +
                    "nothing is being enforced or persisted while this holds.");
                return 0d;
            }

            int idx = IndexOf(list, configId);
            if (idx < 0) return 0d;

            var rec = RaidCooldownRecord.Normalize(list[idx]);
            list[idx] = rec;
            if (rec.DurationSeconds <= 0d)
            {
                // Inert record (a zero-length window, or one authored away). Prune it so the
                // list cannot grow a tail of dead entries across hundreds of clears.
                list.RemoveAt(idx);
                Persist();
                return 0d;
            }

            double now = TimeSource.NowUnixMs();
            double elapsedSeconds = (now - rec.StartedUnixMs) / 1000d;

            if (elapsedSeconds < 0d)
            {
                // ── REFUSE, DON'T PUNISH (see the header). The clock moved backwards, which
                // would otherwise read as a cooldown that keeps growing. Re-stamp to now: the
                // player waits at most one FULL duration — never more, and never less, so a
                // backwards clock can never shorten the wait. No wipe, no accusation.
                FlowTrace.Warn("Raid",
                    "raid cooldown on '" + configId + "' RE-STAMPED — the clock moved BACKWARDS " +
                    ((-elapsedSeconds)).ToString("F0") + "s since the stamp (serverAnchored=" +
                    TimeSource.IsServerAnchored + "). Restarting the window at now rather than " +
                    "leaving it to grow forever. Not punishing the save; a rising rate here is the " +
                    "signal to move the window fully server-side.");
                rec.StartedUnixMs = now;
                rec.ServerAnchored = TimeSource.IsServerAnchored;
                Persist();
                return rec.DurationSeconds;
            }

            double remaining = rec.DurationSeconds - elapsedSeconds;
            if (remaining <= 0d)
            {
                list.RemoveAt(idx);
                Persist();
                FlowTrace.Step("Raid", "raid cooldown on '" + configId + "' ELAPSED — the camp is raidable again.");
                return 0d;
            }
            return remaining;
        }

        /// <summary>True when the camp is still recovering and MUST NOT be entered.</summary>
        public static bool IsOnCooldown(string configId) => RemainingSeconds(configId) > 0d;

        /// <summary>
        /// The player-facing sentence for this camp's state, ALWAYS in words — "Recovering:
        /// raidable in 2h 15m" or "Ready to raid". Never colour alone (the owner is
        /// red/green colourblind; see RaidStrings' header).
        /// </summary>
        public static string DescribeState(string configId)
        {
            double remaining = RemainingSeconds(configId);
            if (remaining <= 0d) return RaidStrings.Get(RaidStrings.KeyReadyCardLine);
            return RaidStrings.Format(RaidStrings.KeyCooldownCardLine, RaidStrings.Humanise(remaining));
        }

        /// <summary>The short badge word for this camp's state ("RECOVERING" / "READY").</summary>
        public static string BadgeFor(string configId) =>
            IsOnCooldown(configId)
                ? RaidStrings.Get(RaidStrings.KeyCooldownBadge)
                : RaidStrings.Get(RaidStrings.KeyReadyBadge);

        /// <summary>The refusal sentence for a tap on a recovering camp; names the wait.</summary>
        public static string BlockedMessage(string configId) =>
            RaidStrings.Format(RaidStrings.KeyCooldownBlocked,
                RaidStrings.Humanise(RemainingSeconds(configId)));

        // =====================================================================
        //  Writing the cooldown
        // =====================================================================

        /// <summary>
        /// Opens (or re-opens) the cooldown window on <paramref name="configId"/> using this
        /// camp's authored/derived duration, stamped from the server-anchored clock seam.
        /// Called by the raid VICTORY paths — a clear is what starts the wait. Returns the
        /// duration actually stamped (0 = nothing was started, and the reason is traced).
        /// Idempotent per clear: re-calling simply restarts the window, which is the correct
        /// behaviour when a camp is cleared twice.
        /// </summary>
        public static double BeginAfterClear(string configId)
        {
            return Begin(configId, DurationForConfigId(configId));
        }

        /// <summary>
        /// Opens the cooldown window with an EXPLICIT duration (seconds). Split out from
        /// <see cref="BeginAfterClear"/> so an oracle can drive the state machine without a
        /// catalog, and so a future ruling (a "cooldown reduced by X" perk) has one seam.
        /// </summary>
        public static double Begin(string configId, double durationSeconds)
        {
            if (string.IsNullOrEmpty(configId))
            {
                FlowTrace.Warn("Raid", "RaidCooldownService.Begin: empty configId — no cooldown started.");
                return 0d;
            }
            if (double.IsNaN(durationSeconds) || durationSeconds <= 0d)
            {
                FlowTrace.Warn("Raid", "RaidCooldownService.Begin: '" + configId + "' resolved a " +
                                       "non-positive duration — no cooldown started, the camp stays " +
                                       "instantly repeatable. Check scene-configs.json.");
                return 0d;
            }

            var list = Records();
            if (list == null)
            {
                FlowTrace.Warn("Raid", "RaidCooldownService.Begin: no GameState available — the cooldown " +
                                       "on '" + configId + "' could not be persisted. The camp will be " +
                                       "instantly repeatable this session.");
                return 0d;
            }

            double now = TimeSource.NowUnixMs();
            // RECORDED ONLY -- NEVER BRANCHED ON. `anchored` is written into the record so a
            // server can reconcile the window later; it must not touch `durationSeconds`, the
            // stamp, or the return value. A cold launch is ALWAYS unanchored, so the moment this
            // flag shortens/lengthens/refuses a window it is taxing every honest offline player on
            // every launch. Refuse server-side, never punish client-side (WO-1128); pinned by
            // RaidCooldownRegression case 6, which measures the returned length with the anchor
            // dropped and requires it to be identical.
            bool anchored = TimeSource.IsServerAnchored;
            int idx = IndexOf(list, configId);
            var rec = new RaidCooldownRecord(configId, now, durationSeconds, anchored);
            if (idx >= 0) list[idx] = rec; else list.Add(rec);
            Persist();

            FlowTrace.Step("Raid", "raid cooldown OPENED on '" + configId + "' for " +
                                   durationSeconds.ToString("F0") + "s (serverAnchored=" + anchored +
                                   "). An unanchored open is legitimate (offline / fresh launch) and is " +
                                   "reconciled by the server on the next save round trip.");
            return durationSeconds;
        }

        /// <summary>
        /// Test/dev hook: drop the cooldown on a camp so it can be raided immediately.
        /// Exercised by RaidCooldownRegression (an unexercised hook proves nothing — the
        /// lesson RaidClaimService.ClearClaim was written to record).
        /// </summary>
        public static void ClearCooldown(string configId)
        {
            if (string.IsNullOrEmpty(configId)) return;
            var list = Records();
            if (list == null) return;
            int idx = IndexOf(list, configId);
            if (idx < 0) return;
            list.RemoveAt(idx);
            Persist();
            FlowTrace.Step("Raid", "raid cooldown on '" + configId + "' CLEARED — the camp is raidable again.");
        }

        // =====================================================================
        //  Internals — one place that touches the save
        // =====================================================================

        private static GameState State =>
            GameStateService.Instance != null ? GameStateService.Instance.State : null;

        /// <summary>The live cooldown list, lazily initialised. Null only when no save is loaded.</summary>
        private static List<RaidCooldownRecord> Records()
        {
            var s = State;
            if (s == null) return null;
            if (s.RaidCooldowns == null) s.RaidCooldowns = new List<RaidCooldownRecord>();
            return s.RaidCooldowns;
        }

        private static int IndexOf(List<RaidCooldownRecord> list, string configId)
        {
            if (list == null || string.IsNullOrEmpty(configId)) return -1;
            for (int i = 0; i < list.Count; i++)
            {
                var r = list[i];
                if (r == null) continue;
                if (string.Equals(r.ConfigId, configId, StringComparison.OrdinalIgnoreCase)) return i;
            }
            return -1;
        }

        private static void Persist()
        {
            // Cross-module call, always null-conditional (CLAUDE.md §10). Guarded because a
            // save throw must never take down a victory screen mid-celebration — but it is
            // LOGGED, never swallowed (§12: no silent failures).
            Guard.Try("Raid", "persist raid cooldowns", () => GameStateService.Instance?.Save());
        }
    }
}
