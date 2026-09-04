// =============================================================================
// HeartfireRegression [heartfire]  --  markers HEARTFIRE_OK / HEARTFIRE_FAIL
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Edit mode, no PlayMode. Registered ONCE in
// DataRegression.RunAll. NEVER throws.
//
// WO-1379. Canon: docs/CREATIVE_CANON_ELARION_2026-09-04.md section 4.
//
// WHAT IT PROTECTS, and why each pin is worth its line:
//
//   PIN A  THE REGEN TABLE IS THE ACCEPTANCE CRITERION, LITERALLY.
//          0 charges stamped at T -> 1 at T+4h, 2 at +8h, 3 at +12h, STILL 3 at
//          +24h; spending one at +12h leaves 2. Driven against the PURE function
//          DeNelle.Core.State.HeartfireCharges.Regenerate with an injected "now",
//          so it needs no save, no clock, no scene and no PlayMode. The "+24h is
//          still 3" row is the one that catches the two opposite defects: a pool
//          that keeps counting past its ceiling, and a pool that banks a hidden
//          backlog and refills instantly the moment one is spent.
//
//   PIN B  HEARTFIRE NEVER BECOMES A CURRENCY.
//          Source-lint, because this is exactly the drift a behavioural test
//          cannot see: a wallet row, a ResourceType member, a storage cap or a
//          vendor price would all keep the regen table green while breaking the
//          one ruling the ticket is built on ("if the implementation grows a
//          balance, it is wrong"). Lints the two Heartfire files with comments
//          AND string literals stripped, so a symbol named only in a comment can
//          never satisfy - or trip - a pin, and lints the canonical resource
//          catalogs for any Heartfire row.
//
//   PIN C  "RAID ORDER" IS DEAD, AND "MARCH" IS ALIVE.
//          The rename is the ticket. A shipped string still saying "Raid Order"
//          means the fiction did not land, however correct the integer is. The
//          verb is deliberately NOT banned - canon section 2 keeps "march" and
//          bans only "Marches" as a NOUN for the pool.
//
//   PIN D  THE CLOCK IS TimeSource, NEVER DateTime.UtcNow.
//          Source-lint, because it can only be seen at the call site: a single
//          DateTimeOffset.UtcNow in the service re-opens the device-clock exploit
//          in full (roll the phone clock forward, refill the pool) and would test
//          GREEN against every behavioural case in this file - the runtime cannot
//          tell which clock it was handed. Same pin, same reasoning, as
//          RaidCooldownRegression PIN C.
//
//   PIN E  A PLAYER HOLDING HEARTFIRE ALWAYS HAS SOMEWHERE TO SPEND IT.
//          THE behavioural acceptance criterion of the three-gate stack, and the
//          only one that cannot be read off a single number. Pinned as the
//          relation the shipped values encode: the rekindle interval is <= the
//          SHORTEST authored per-camp cooldown, measured out of scene-configs.json
//          rather than restated here, so a charge can never land into a world with
//          every door shut. It goes red if either side moves alone - which is the
//          point, because the WO forbids fixing that by shortening the cooldowns.
//
// Standalone:
//   -Method DeNelle.Editor.Regression.HeartfireRegression.RunStandalone
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DeNelle.Core.State;

namespace DeNelle.Editor.Regression
{
    public static class HeartfireRegression
    {
        // Relative to Application.dataPath.
        private const string CoreRel    = "_Modules/Core/State/HeartfireCharges.cs";
        private const string ServiceRel = "_Modules/Village/World/Camps/HeartfireService.cs";
        private const string SceneCfgResRel    = "Resources/Data/Canonical/scene-configs.json";
        private const string SceneCfgStreamRel = "StreamingAssets/Data/Canonical/scene-configs.json";

        private const double Hour = 3600d * 1000d;   // one hour in unix-MS

        /// <summary>DataRegression-shaped contract. NEVER throws.</summary>
        public static bool Run(out string reason)
        {
            try { return RunCore(out reason); }
            catch (Exception ex)
            {
                reason = "heartfire: oracle threw " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        /// <summary>Batchmode entry point (marker on a fresh log; never the exit code).</summary>
        public static void RunStandalone()
        {
            bool ok = Run(out string reason);
            Debug.Log(ok ? "HEARTFIRE_OK " + reason : "HEARTFIRE_FAIL " + reason);
        }

        private static bool RunCore(out string reason)
        {
            var f = new List<string>();

            RegenTableCases(f);        // PIN A
            CopyCases(f);              // PIN A (words half)
            CurrencyLintCases(f);      // PIN B
            NamingCases(f);            // PIN C
            ClockLintCases(f);         // PIN D
            SpendRoomCases(f);         // PIN E

            if (f.Count == 0)
            {
                reason = "HEARTFIRE OK -- the pool regenerates 0->1->2->3 on the ruled interval and " +
                         "STOPS at the ceiling with no hidden backlog; spending one leaves the rest and " +
                         "does not restart the accrual window; a backwards clock can neither shorten the " +
                         "wait nor conjure a charge; Heartfire has no balance, no wallet row, no cap and " +
                         "no vendor anywhere; no shipped string says 'Raid Order' and 'Marches' is not a " +
                         "noun for the pool; the service reads TimeSource only; and the rekindle interval " +
                         "is no longer than the shortest authored camp cooldown, so a held charge always " +
                         "has somewhere to go";
                return true;
            }
            reason = "HEARTFIRE FAIL x" + f.Count + ": " + string.Join(" | ", f);
            return false;
        }

        // =====================================================================
        //  PIN A -- the acceptance table, driven on the pure function
        // =====================================================================

        private static void RegenTableCases(List<string> f)
        {
            const int Max = 3;
            const double Regen = 4d * 3600d;   // seconds

            // The SHIPPED defaults must be the ones this table is written against. If a
            // tunable default moves, the table below stops describing the game and the
            // failure has to name that, not silently re-derive.
            if (HeartfireCharges.MaxChargesDefault != Max)
                f.Add("A0 the shipped Heartfire ceiling is " + HeartfireCharges.MaxChargesDefault +
                      ", not " + Max + " -- canon section 4 says three charges, and this whole table " +
                      "is written against three");
            if (Math.Abs(HeartfireCharges.RegenSecondsDefault - Regen) > 0.5d)
                f.Add("A0 the shipped rekindle interval is " + HeartfireCharges.RegenSecondsDefault +
                      "s, not " + Regen + "s -- canon section 4 says one charge every four hours");

            double t = 1_700_000_000_000d;   // an arbitrary fixed epoch; only deltas matter

            // The WO's acceptance table, row for row. Each row RE-READS the pool stamped at
            // T with 0 charges, so a row cannot be carried by the row before it.
            var atT = new HeartfireCharges.Pool(0, t, true);

            AssertCharges(f, "A1 +0h", atT, t + 0d * Hour, Max, Regen, 0);
            AssertCharges(f, "A2 +4h", atT, t + 4d * Hour, Max, Regen, 1);
            AssertCharges(f, "A3 +8h", atT, t + 8d * Hour, Max, Regen, 2);
            AssertCharges(f, "A4 +12h", atT, t + 12d * Hour, Max, Regen, 3);
            AssertCharges(f, "A5 +24h", atT, t + 24d * Hour, Max, Regen, 3);

            // Just SHORT of an interval must not round up. A pool that grants at 3h59m is
            // a four-hour gate in name only.
            AssertCharges(f, "A6 +3h59m", atT, t + 4d * Hour - 60d * 1000d, Max, Regen, 0);

            // ── The spend row: spending one at +12h leaves 2 ───────────────────────
            var full = HeartfireCharges.Regenerate(atT, t + 12d * Hour, Max, Regen, out _);
            if (!HeartfireCharges.TrySpend(full, out var afterSpend))
                f.Add("A7 TrySpend refused with a FULL pool -- the march could never start");
            else if (afterSpend.Charges != 2)
                f.Add("A7 spending one charge at +12h left " + afterSpend.Charges + ", expected 2");

            // Spending must NOT restart the accrual window (a player who marches the instant
            // a charge lands would otherwise silently lose the partial progress toward the
            // next one -- a punishment nobody authored).
            if (Math.Abs(afterSpend.LastRegenUnixMs - full.LastRegenUnixMs) > 0.5d)
                f.Add("A7b spending moved the accrual stamp from " + full.LastRegenUnixMs.ToString("F0") +
                      " to " + afterSpend.LastRegenUnixMs.ToString("F0") + " -- marching must never " +
                      "restart the rekindle clock");

            // ── NO HIDDEN BACKLOG. A pool that sat FULL for two days must not refill the
            // instant a charge is spent. This is the defect the "+24h is still 3" row exists
            // to set up, and this is the row that actually catches it.
            var sattedFull = HeartfireCharges.Regenerate(atT, t + 48d * Hour, Max, Regen, out _);
            HeartfireCharges.TrySpend(sattedFull, out var spentAfterSitting);
            var oneSecondLater = HeartfireCharges.Regenerate(spentAfterSitting, t + 48d * Hour + 1000d,
                                                            Max, Regen, out _);
            if (oneSecondLater.Charges != 2)
                f.Add("A8 a pool that sat FULL for two days refilled to " + oneSecondLater.Charges +
                      " one second after a spend -- it banked a hidden backlog, which is a stacking " +
                      "pool with no ceiling");

            // ── An EMPTY pool refuses, and refuses without changing anything ───────
            var empty = new HeartfireCharges.Pool(0, t, true);
            if (HeartfireCharges.TrySpend(empty, out var afterEmptySpend))
                f.Add("A9 TrySpend SUCCEEDED on an empty pool -- the gate does not exist");
            else if (afterEmptySpend.Charges != 0 ||
                     Math.Abs(afterEmptySpend.LastRegenUnixMs - empty.LastRegenUnixMs) > 0.5d)
                f.Add("A9 a refused spend still mutated the pool -- a refusal must change nothing");

            // ── A BACKWARDS clock can neither shorten the wait nor conjure a charge ─
            // Arithmetically identical to (and cheaper than) moving a clock: push the STAMP
            // into the future. RaidCooldownRegression case 3's precedent.
            var future = new HeartfireCharges.Pool(0, t + 10d * 60d * 1000d, true);
            var repaired = HeartfireCharges.Regenerate(future, t, Max, Regen, out int backGranted);
            if (backGranted != 0 || repaired.Charges != 0)
                f.Add("A10 a BACKWARDS clock granted " + backGranted + " charge(s) -- rolling the phone " +
                      "clock back must never manufacture Heartfire");
            double waitAfterBack = HeartfireCharges.SecondsToNextCharge(repaired, t, Max, Regen);
            if (waitAfterBack > Regen + 1d)
                f.Add("A10 after a backwards clock the wait is " + waitAfterBack.ToString("F0") +
                      "s, LONGER than one full " + Regen.ToString("F0") + "s interval -- an honest " +
                      "player who crossed a timezone would be stalled. REFUSE, DON'T PUNISH");

            // ── An UNSTAMPED pool must not resolve ~57 years of accrual off the epoch ─
            var unstamped = new HeartfireCharges.Pool(0, 0d, false);
            var seeded = HeartfireCharges.Regenerate(unstamped, t, Max, Regen, out int epochGranted);
            if (epochGranted != 0)
                f.Add("A11 an unstamped pool granted " + epochGranted + " charge(s) off the epoch -- " +
                      "that looks identical to a working regen and would hide a real defect forever");
            if (Math.Abs(seeded.LastRegenUnixMs - t) > 0.5d)
                f.Add("A11 an unstamped pool was not anchored at now -- it will re-detect this every read");

            // ── The countdown must count DOWN and never lie ────────────────────────
            double next = HeartfireCharges.SecondsToNextCharge(atT, t + 1d * Hour, Max, Regen);
            if (Math.Abs(next - 3d * 3600d) > 1d)
                f.Add("A12 one hour into a four-hour rekindle the countdown reads " + next.ToString("F0") +
                      "s, expected 10800s");
            var fullPool = new HeartfireCharges.Pool(Max, t, true);
            if (HeartfireCharges.SecondsToNextCharge(fullPool, t, Max, Regen) != 0d)
                f.Add("A13 a FULL pool reports a countdown -- there is nothing pending to count down to");
        }

        private static void AssertCharges(List<string> f, string label, HeartfireCharges.Pool from,
                                          double nowUnixMs, int max, double regen, int expected)
        {
            var got = HeartfireCharges.Regenerate(from, nowUnixMs, max, regen, out _);
            if (got.Charges != expected)
                f.Add(label + " expected " + expected + " charge(s), got " + got.Charges);
            if (got.Charges > max)
                f.Add(label + " exceeded the ceiling (" + got.Charges + " > " + max + ")");
        }

        // =====================================================================
        //  PIN A (words) -- the copy is the point of the ticket
        // =====================================================================

        private static void CopyCases(List<string> f)
        {
            // THE sentence. Not "you may not raid because TIMER", but the Heart is not ready
            // to send you back yet (canon section 4). And it must always name the wait: a
            // player told "no" with no "when" cannot act on it.
            string blocked = HeartfireCharges.BlockedMessage(3d * 3600d + 42d * 60d + 18d);
            if (blocked.IndexOf("Heart is not ready", StringComparison.OrdinalIgnoreCase) < 0)
                f.Add("the refusal '" + blocked + "' does not say the Heart is not ready -- the rename " +
                      "is the whole ticket, and a bare timer sentence is what it replaced");
            if (blocked.IndexOf("3:42:18", StringComparison.Ordinal) < 0)
                f.Add("the refusal '" + blocked + "' does not name the wait");
            AssertAscii(f, "refusal", blocked);

            // The lit/spent row must be TEXT-ENCODED. The owner is red/green colourblind, so
            // a lit charge can never be "the orange one" (memory owner-colorblind-delegate-
            // visual-creative). Two different states, two different strings.
            string two = HeartfireCharges.FlameRow(2, 3);
            string three = HeartfireCharges.FlameRow(3, 3);
            string zero = HeartfireCharges.FlameRow(0, 3);
            if (two == three || two == zero)
                f.Add("FlameRow renders different charge counts identically ('" + two + "') -- the state " +
                      "would only be readable by colour, which says nothing to a colourblind player");
            if (two.IndexOf("[ ]", StringComparison.Ordinal) < 0)
                f.Add("FlameRow(2,3) '" + two + "' shows no SPENT slot -- a player cannot see what they lost");
            if (three.IndexOf("[ ]", StringComparison.Ordinal) >= 0)
                f.Add("FlameRow(3,3) '" + three + "' shows a spent slot on a FULL pool");
            AssertAscii(f, "flame row", two);

            // Out-of-range inputs must clamp, never throw and never render a ragged row.
            if (HeartfireCharges.FlameRow(9, 3) != three)
                f.Add("FlameRow(9,3) did not clamp to the ceiling");
            if (HeartfireCharges.FlameRow(-4, 3) != zero)
                f.Add("FlameRow(-4,3) did not clamp to empty");

            string fullLine = HeartfireCharges.RekindleLine(3, 3, 0d);
            if (fullLine.IndexOf("full", StringComparison.OrdinalIgnoreCase) < 0)
                f.Add("a FULL pool's line reads '" + fullLine + "' -- it must say so, not count down to nothing");
            string waitLine = HeartfireCharges.RekindleLine(1, 3, 90d);
            if (waitLine.IndexOf("1:30", StringComparison.Ordinal) < 0)
                f.Add("the rekindle line '" + waitLine + "' does not carry the countdown");
            AssertAscii(f, "rekindle line", waitLine);
            AssertAscii(f, "count label", HeartfireCharges.CountLabel(2, 3));

            // The clock formatter must not round a real wait down to nothing: a UI that says
            // "0:00" while still refusing reads as a frozen game (the WO-1110 dead-tap shape).
            if (HeartfireCharges.Clock(0.4d) == "0:00")
                f.Add("Clock(0.4s) rounded a live wait down to 0:00 -- a refused march with a zeroed " +
                      "countdown reads as a broken button");
        }

        private static void AssertAscii(List<string> f, string what, string s)
        {
            for (int i = 0; i < s.Length; i++)
            {
                if (s[i] <= 127) continue;
                f.Add("the " + what + " string carries non-ASCII ('" + s + "') -- TMP renders it as tofu " +
                      "on the mobile font atlas");
                return;
            }
        }

        // =====================================================================
        //  PIN B -- HEARTFIRE IS A CHARGE, NOT A CURRENCY
        // =====================================================================

        private static void CurrencyLintCases(List<string> f)
        {
            // Comments and string literals are STRIPPED by ReadCode, which matters in both
            // directions here: this file's own headers say the words "wallet", "vendor" and
            // "currency" repeatedly while forbidding them, and a lint that read comments
            // would fail on the prose that exists to prevent the defect.
            string core = SourceLint.ReadCode(CoreRel, f);
            string svc  = SourceLint.ReadCode(ServiceRel, f);

            // Every symbol that would mean a balance had appeared. This is a DENY list on
            // purpose: the ways to add a currency are few and named, while the ways to add a
            // charge are many, so denying is the direction that stays true as the code grows.
            string[] banned =
            {
                "ResourceType", "ResourceCost", "CurrencyKind", "Wallet", "PurchaseCatalog",
                "PackStore", "AddResource", "SpendResource", "TrySpendResources", "Vendor",
                "StorageCap", "TownBankCapacity", "Price",
            };

            AssertNoBanned(f, "HeartfireCharges", core, banned);
            AssertNoBanned(f, "HeartfireService", svc, banned);

            // The catalogs a currency would have to be authored into. A Heartfire row in any
            // of them means somebody made it buyable, cappable or tradeable.
            AssertNoHeartfireRow(f, "Resources/Data/Canonical/resources.json");
            AssertNoHeartfireRow(f, "Resources/Data/Canonical/storage-caps.json");
            AssertNoHeartfireRow(f, "Resources/Data/Canonical/packs.json");
            AssertNoHeartfireRow(f, "StreamingAssets/Data/Canonical/resources.json");
            AssertNoHeartfireRow(f, "StreamingAssets/Data/Canonical/storage-caps.json");
            AssertNoHeartfireRow(f, "StreamingAssets/Data/Canonical/packs.json");

            // The enum itself. NO NEW CURRENCY: Wood, Iron, Food, Gold, Crystals is the set.
            string enums = SourceLint.ReadCode("_Modules/Core/State/Enums.cs", null);
            if (!string.IsNullOrEmpty(enums) &&
                enums.IndexOf("Heartfire", StringComparison.OrdinalIgnoreCase) >= 0)
                f.Add("Core/State/Enums.cs now names Heartfire -- a charge has been promoted into an " +
                      "enum that carries currencies. Wood, Iron, Food, Gold, Crystals is the set");

            // And the shape of the pool itself: two numbers and a diagnostic flag. A pool that
            // grows an "amount", a "balance" or a "max" FIELD is on its way to being money.
            if (!string.IsNullOrEmpty(core))
            {
                if (core.IndexOf("public double Balance", StringComparison.Ordinal) >= 0 ||
                    core.IndexOf("public int Balance", StringComparison.Ordinal) >= 0 ||
                    core.IndexOf("public int Amount", StringComparison.Ordinal) >= 0)
                    f.Add("HeartfireCharges.Pool grew a Balance/Amount field -- if the implementation " +
                          "grows a balance, it is wrong (WO-1379 section 2)");
            }
        }

        private static void AssertNoBanned(List<string> f, string where, string code, string[] banned)
        {
            if (string.IsNullOrEmpty(code)) return;
            for (int i = 0; i < banned.Length; i++)
            {
                if (code.IndexOf(banned[i], StringComparison.Ordinal) < 0) continue;
                f.Add(where + " now references '" + banned[i] + "' -- Heartfire is a CHARGE, never a " +
                      "currency: never earned, traded, stored, gifted or bought. If the implementation " +
                      "grows a balance, it is wrong (WO-1379 section 2)");
            }
        }

        private static void AssertNoHeartfireRow(List<string> f, string relativeToAssets)
        {
            string path = Path.Combine(Application.dataPath, relativeToAssets);
            string text = TryReadText(path);
            if (text == null) return;   // an absent catalog is another suite's problem, not this one's
            if (text.IndexOf("heartfire", StringComparison.OrdinalIgnoreCase) >= 0)
                f.Add(relativeToAssets + " now carries a Heartfire row -- that file authors resources, " +
                      "storage caps or purchasable packs, and Heartfire is none of those things");
        }

        // =====================================================================
        //  PIN C -- "Raid Order" is dead; "march" survives as the VERB
        // =====================================================================

        private static void NamingCases(List<string> f)
        {
            // The retired name, and the superseded FIRST-PASS name (canon section 2:
            // implementing a superseded name is a defect, not a preference). Both are
            // checked in the two places a player could actually read them: the Heartfire
            // sources and the canonical string tables.
            string[] files =
            {
                CoreRel, ServiceRel,
            };
            for (int i = 0; i < files.Length; i++)
            {
                string code = SourceLint.ReadCode(files[i], null);
                if (string.IsNullOrEmpty(code)) continue;
                if (code.IndexOf("RaidOrder", StringComparison.OrdinalIgnoreCase) >= 0)
                    f.Add(files[i] + " still names 'Raid Order' in code -- the player is the ruler and " +
                          "nobody issues them orders (canon section 4)");
            }

            // Player-facing copy: the canon string tables. "Raid Orders" must be gone; the
            // VERB "march" is explicitly NOT banned, so nothing here looks for it.
            AssertNoRaidOrderCopy(f, "Resources/Data/Canonical/canon-strings.json");
            AssertNoRaidOrderCopy(f, "StreamingAssets/Data/Canonical/canon-strings.json");

            // The pool's own name must be the canon one, and must not have quietly become
            // the superseded first-pass noun.
            if (!string.Equals(HeartfireCharges.Name, "Heartfire", StringComparison.Ordinal))
                f.Add("the pool calls itself '" + HeartfireCharges.Name + "' -- canon section 2 rules " +
                      "the name Heartfire and records 'Marches' as the superseded first pass");
        }

        private static void AssertNoRaidOrderCopy(List<string> f, string relativeToAssets)
        {
            string text = TryReadText(Path.Combine(Application.dataPath, relativeToAssets));
            if (text == null) return;
            if (text.IndexOf("Raid Order", StringComparison.OrdinalIgnoreCase) >= 0)
                f.Add(relativeToAssets + " still ships the string 'Raid Order' -- that name is dead " +
                      "(canon section 4)");
        }

        // =====================================================================
        //  PIN D -- the clock is TimeSource, never the device clock
        // =====================================================================

        private static void ClockLintCases(List<string> f)
        {
            string svc = SourceLint.ReadCode(ServiceRel, f);
            if (string.IsNullOrEmpty(svc)) return;

            if (svc.IndexOf("DateTime.UtcNow", StringComparison.Ordinal) >= 0 ||
                svc.IndexOf("DateTimeOffset.UtcNow", StringComparison.Ordinal) >= 0)
                f.Add("HeartfireService reads the DEVICE clock -- the pool would refill in ten seconds " +
                      "for anyone who opens Settings > Date & Time, and every behavioural case in this " +
                      "file would still pass. Read TimeSource.NowUnixMs()");
            if (svc.IndexOf("TimeSource.NowUnixMs()", StringComparison.Ordinal) < 0)
                f.Add("HeartfireService never reads TimeSource.NowUnixMs() -- the server-anchored clock " +
                      "seam is not being read at all");
            if (svc.IndexOf("TimeSource.IsServerAnchored", StringComparison.Ordinal) < 0)
                f.Add("HeartfireService never records TimeSource.IsServerAnchored -- an offline rekindle " +
                      "would be indistinguishable from a trusted one and nothing could reconcile it");

            // The Core half must stay clock-FREE. That asymmetry is what makes reading the
            // wrong clock impossible from the file that owns the arithmetic.
            string core = SourceLint.ReadCode(CoreRel, f);
            if (!string.IsNullOrEmpty(core))
            {
                if (core.IndexOf("UtcNow", StringComparison.Ordinal) >= 0)
                    f.Add("HeartfireCharges now reads a clock -- 'now' is a PARAMETER there on purpose, " +
                          "which is what keeps the whole regen table drivable by an oracle");
                if (core.IndexOf("UnityEngine", StringComparison.Ordinal) >= 0)
                    f.Add("HeartfireCharges now references UnityEngine -- the pure half must stay pure");
            }

            // No silent failures (CLAUDE.md section 12): the refusal path must SAY it refused.
            var spend = SourceLint.Body(svc, @"public\s+static\s+bool\s+TrySpend\s*\(\s*string\s+reason\s*\)");
            if (string.IsNullOrEmpty(spend))
                f.Add("HeartfireService.TrySpend(string) not found -- the spend seam moved");
            else if (spend.IndexOf("FlowTrace", StringComparison.Ordinal) < 0)
                f.Add("HeartfireService.TrySpend has no FlowTrace line -- a refused march would be a " +
                      "SILENT no-op, and CLAUDE.md section 12 forbids silent failures");
        }

        // =====================================================================
        //  PIN E -- a player holding Heartfire always has somewhere to spend it
        // =====================================================================

        private static void SpendRoomCases(List<string> f)
        {
            // The shortest authored per-camp cooldown, MEASURED out of the canonical data
            // rather than restated here -- a number copied from a doc is hearsay
            // (CLAUDE.md section 11B).
            double shortest = ShortestAuthoredCooldownSeconds(SceneCfgResRel, out int campCount, f);

            if (campCount <= 0)
            {
                f.Add("scene-configs.json authors NO camp with a raidCooldownSeconds -- either the raid " +
                      "targets are gone or the field was renamed, and the spend-room criterion cannot " +
                      "be evaluated either way");
                return;
            }

            if (HeartfireCharges.RegenSecondsDefault > shortest + 0.5d)
                f.Add("a Heartfire charge rekindles every " + HeartfireCharges.RegenSecondsDefault +
                      "s but the SHORTEST authored camp cooldown is " + shortest.ToString("F0") + "s -- a " +
                      "player can be handed a charge with every camp still recovering, which breaks the " +
                      "WO-1379 criterion 'a player holding Heartfire always has somewhere to spend it'. " +
                      "The fix is NOT to shorten raidCooldownSeconds (that file's authoring note explains " +
                      "at length why those hours are not the lever) -- it is an owner ruling on the stack");

            // The two canonical copies must agree, because RESOURCES WINS at runtime and the
            // build the owner plays is the one that read the other file.
            double shortestStream = ShortestAuthoredCooldownSeconds(SceneCfgStreamRel, out int streamCount, null);
            if (streamCount != campCount || Math.Abs(shortestStream - shortest) > 0.5d)
                f.Add("the Resources and StreamingAssets copies of scene-configs.json disagree about the " +
                      "raid cooldowns (" + campCount + " camps / shortest " + shortest.ToString("F0") +
                      "s vs " + streamCount + " / " + shortestStream.ToString("F0") + "s) -- Resources " +
                      "wins at runtime, so the twin is a lie waiting to be read");
        }

        /// <summary>
        /// Smallest positive authored <c>raidCooldownSeconds</c> in a scene-configs file, and
        /// how many were found. Deliberately a plain scan rather than a JSON parse: the file's
        /// own SCHEMA BLOCK carries the key with a prose string value, and a numeric parse is
        /// what tells the two apart without dragging a serializer into an oracle.
        /// </summary>
        private static double ShortestAuthoredCooldownSeconds(string relativeToAssets, out int count,
                                                              List<string> f)
        {
            count = 0;
            double shortest = double.MaxValue;
            string text = TryReadText(Path.Combine(Application.dataPath, relativeToAssets));
            if (text == null)
            {
                if (f != null) f.Add("scene-configs.json missing at " + relativeToAssets);
                return 0d;
            }

            const string Needle = "\"raidCooldownSeconds\"";
            int i = text.IndexOf(Needle, StringComparison.Ordinal);
            while (i >= 0)
            {
                int colon = text.IndexOf(':', i + Needle.Length);
                if (colon > 0)
                {
                    int j = colon + 1;
                    while (j < text.Length && (text[j] == ' ' || text[j] == '\t')) j++;
                    int start = j;
                    while (j < text.Length && (char.IsDigit(text[j]) || text[j] == '.')) j++;
                    if (j > start &&
                        double.TryParse(text.Substring(start, j - start),
                                        System.Globalization.NumberStyles.Float,
                                        System.Globalization.CultureInfo.InvariantCulture, out double v) &&
                        v > 0d)
                    {
                        count++;
                        if (v < shortest) shortest = v;
                    }
                }
                i = text.IndexOf(Needle, i + Needle.Length, StringComparison.Ordinal);
            }

            return count > 0 ? shortest : 0d;
        }

        private static string TryReadText(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : null; }
            catch (IOException) { return null; }
        }
    }
}
