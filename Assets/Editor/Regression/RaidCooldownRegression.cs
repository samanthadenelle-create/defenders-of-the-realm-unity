// =============================================================================
// RaidCooldownRegression [raid-cooldown]  --  markers RAID_COOLDOWN_OK / _FAIL
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression.  Edit mode, no PlayMode: part BEHAVIOURAL
// (it drives the real DeNelle.Village statics and a real save/load round trip) and
// part SOURCE-LINT (it reads the raid runtime files with comments AND string
// literals stripped, so a symbol named only inside a comment or a log message can
// never satisfy a pin).  Registered ONCE in DataRegression.RunAll.  NEVER throws.
//
// WHAT IT PROTECTS, and why each pin is worth its line:
//
//   PIN A  THE COOLDOWN SURVIVES A REAL SAVE/LOAD ROUND TRIP.
//          Not "the field serialises" - the whole device path: GameStateService
//          .Save -> provider -> integrity gate -> SaveMigrator -> SaveSchema
//          .Validate -> ApplyPersisted, ANY of which can reject a payload and
//          silently keep fresh state (Load returns false and every camp is simply
//          raidable again). A cooldown that evaporates on relaunch is not a
//          cooldown, it is a loading screen. Driven against a SECOND, FRESH
//          GameState - the only headless way to simulate a cold boot.
//
//   PIN B  A BACKWARDS CLOCK CANNOT SHORTEN A COOLDOWN.
//          The attack is forward (roll the phone clock on, skip the wait) and the
//          defence against THAT is the server-anchored monotonic clock, pinned by
//          PIN C. This pin covers the other direction, which is the one that bites
//          honest players: a clock that moved BACKWARDS makes elapsed negative, so
//          a naive implementation locks the camp out forever. The service re-stamps
//          instead - REFUSE, DON'T PUNISH - which caps the wait at ONE FULL window
//          and, load-bearingly, never less. Asserted both ways: the remaining time
//          after a backwards jump is >= the full duration (never shortened) AND
//          <= the full duration (never unbounded).
//
//   PIN C  THE CLOCK IS TimeSource, NEVER DateTime.UtcNow.
//          Source-lint, because this can only be seen at the call site. A single
//          DateTimeOffset.UtcNow anywhere in the service re-opens the device-clock
//          exploit in full, and it would test GREEN against every behavioural case
//          in this file - the runtime cannot tell which clock it was handed. Also
//          pins that the trust flag is RECORDED (anchored vs not) and that nothing
//          PUNISHES an unanchored clock: a cold launch is always unanchored, so a
//          client-side penalty would tax every honest offline player (WO-1128).
//
//   PIN D  THE STAMP SURVIVES, THE RECORD READS BACK, AND ITS WORDS ARE SENTENCES.
//          (!) RE-POINTED 2026-09-05 by WO-1379. This pin USED to read "a camp on
//          cooldown cannot be entered" and source-linted RaidSelectionScreen.
//          OnCardTapped for an IsOnCooldown( check ahead of RaidDeployScreen.Open.
//          The owner ruled "Heartfire replaces the camp wall", so that lint was the
//          exact thing that had to go RED under the new canon - it has been REMOVED
//          from this suite, and the door is now owned by HeartfireRegression PIN F,
//          which reds the opposite (any RaidCooldownService reference on the raid
//          surface). Not duplicated here: one door, one pin owner.
//          What this pin still holds: BOTH victory paths stamp BeginAfterClear (the
//          record is save evidence - SaveMigrator v41 derives everCompletedRaid from
//          it); the state machine reads back (IsOnCooldown / RemainingSeconds as
//          RECORD queries); and the record's canon copy is still a SENTENCE in BOTH
//          canonical copies, ASCII-only, while those keys exist - no runtime surface
//          shows them any more, but a placeholder marker in a shipped string table
//          is a defect regardless of who reads it.
//
//   PIN E  THE BALANCE NUMBERS ARE THE OWNER'S, AND THE DATA AGREES WITH THE CODE.
//          Cooldown 4h/8h/12h and attrition 5/20/45min are OWNER RULINGS
//          (2026-08-21), not feel knobs - the cooldown is the only bound on the
//          game's one unbounded crystal faucet. Because they are authored in BOTH
//          scene-configs.json AND the code fallback table, they can DRIFT; this
//          pins them equal. It also pins that attrition is NOT FLAT, which is the
//          exact defect the ruling closed (120s for every camp made raiding free).
//
// Standalone:
//   -Method DeNelle.Editor.Regression.RaidCooldownRegression.RunStandalone
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using DeNelle.Core.State;
using DeNelle.Core.UI;
using DeNelle.Village;
using DeNelle.Village.World.Camps;

namespace DeNelle.Editor.Regression
{
    public static class RaidCooldownRegression
    {
        // Relative to Application.dataPath.
        private const string ServiceRel  = "_Modules/Village/World/Camps/RaidCooldownService.cs";
        // SelectRel (RaidSelectionScreen.cs) was removed 2026-09-05: the door lint moved to
        // HeartfireRegression PIN F (WO-1379). This suite no longer reads that file.
        private const string VictoryRel  = "_Modules/Village/World/Camps/RaidVictoryController.cs";
        private const string V2Rel       = "_Modules/Village/World/Camps/Village2RaidController.cs";
        private const string DeployRel   = "_Modules/Village/Troops/RaidDeployController.cs";

        private const string CanonResRel    = "Resources/Data/Canonical/canon-strings.json";
        private const string CanonStreamRel = "StreamingAssets/Data/Canonical/canon-strings.json";

        // A scratch camp id no scene-config will ever use, so the round trip cannot
        // disturb a real save. Cleared on every exit path, including the throwing one.
        private const string ScratchId = "zz-regression-scratch-raid-cooldown";

        /// <summary>DataRegression-shaped contract. NEVER throws.</summary>
        public static bool Run(out string reason)
        {
            try { return RunCore(out reason); }
            catch (Exception ex)
            {
                reason = "raid-cooldown: oracle threw " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        /// <summary>Batchmode entry point (marker on a fresh log; never the exit code).</summary>
        public static void RunStandalone()
        {
            bool ok = Run(out string reason);
            Debug.Log(ok ? "RAID_COOLDOWN_OK " + reason : "RAID_COOLDOWN_FAIL " + reason);
        }

        private static bool RunCore(out string reason)
        {
            var f = new List<string>();

            BalanceTableCases(f);          // PIN E
            SourceCases(f);                // PIN C + D (lint half)
            CanonCopyCases(f);             // PIN D (words half)
            StateMachineCases(f, out bool skipped, out string skipWhy);   // PIN A + B + D

            if (skipped)
            {
                // The GameStateService singleton/state seam moved -- genuinely unrunnable
                // headless. NAMED SKIP, never a false FAIL (harness-integrity rule).
                return RegressionOutcome.Skip(out reason, "RAID COOLDOWN", "needs fleet -- " + skipWhy);
            }

            if (f.Count == 0)
            {
                reason = "RAID COOLDOWN OK -- the window survives a real save/load cold boot; a BACKWARDS " +
                         "clock re-stamps to exactly one full window (never shorter, never unbounded); the " +
                         "service reads TimeSource only (no DateTime.UtcNow) and RECORDS the anchor without " +
                         "punishing an unanchored one; both victory paths still STAMP the record (WO-1379 " +
                         "retired the gate, not the stamp - the door is Heartfire's, pinned in " +
                         "HeartfireRegression PIN F) and its canon words are sentences in both canonical " +
                         "copies, ASCII-only; and the owner-ruled numbers (cooldown 4h/8h/12h retained as " +
                         "superseded, attrition 5/20/45min, not flat) agree between scene-configs.json and " +
                         "the code fallback table";
                return true;
            }
            reason = "RAID COOLDOWN FAIL x" + f.Count + ": " + string.Join(" | ", f);
            return false;
        }

        // =====================================================================
        //  PIN E — the owner's numbers, and data/code agreement
        // =====================================================================

        private static void BalanceTableCases(List<string> f)
        {
            // -- Cooldown: the ruled 4h / 8h / 12h ---------------------------------
            AssertSeconds(f, "cooldown Regular", RaidCooldownService.DurationForDifficulty("Regular"), 4d * 3600d);
            AssertSeconds(f, "cooldown Hard",    RaidCooldownService.DurationForDifficulty("Hard"),    8d * 3600d);
            AssertSeconds(f, "cooldown Extreme", RaidCooldownService.DurationForDifficulty("Extreme"), 12d * 3600d);

            // Case-insensitive, and an UNKNOWN difficulty must resolve to the SHORTEST
            // window. The forgiving direction: a mis-authored camp must never inherit the
            // twelve-hour lockout, and must never resolve to zero (= no cooldown at all,
            // which would silently restore the unbounded crystal faucet).
            AssertSeconds(f, "cooldown extreme (lowercase)",
                RaidCooldownService.DurationForDifficulty("extreme"), 12d * 3600d);
            AssertSeconds(f, "cooldown unknown -> Regular",
                RaidCooldownService.DurationForDifficulty("Nonsense"), 4d * 3600d);
            AssertSeconds(f, "cooldown null -> Regular",
                RaidCooldownService.DurationForDifficulty(null), 4d * 3600d);

            // -- Attrition: the ruled 5 / 20 / 45 min ------------------------------
            AssertSeconds(f, "attrition Regular",
                DeNelle.Village.RaidDeployController.RecoveryForDifficulty("Regular"), 300d);
            AssertSeconds(f, "attrition Hard",
                DeNelle.Village.RaidDeployController.RecoveryForDifficulty("Hard"), 1200d);
            AssertSeconds(f, "attrition Extreme",
                DeNelle.Village.RaidDeployController.RecoveryForDifficulty("Extreme"), 2700d);
            AssertSeconds(f, "attrition unknown -> Regular",
                DeNelle.Village.RaidDeployController.RecoveryForDifficulty("Nonsense"), 300d);

            // THE DEFECT THE RULING CLOSED: attrition used to be a flat 120s for every camp,
            // so a failed Extreme assault cost exactly what a failed practice run cost and
            // there was no loop, only a faucet with a pause in front of it. If these three
            // ever collapse back to one number this pin is the thing that says so.
            if (!(DeNelle.Village.RaidDeployController.RecoveryForDifficulty("Regular") <
                  DeNelle.Village.RaidDeployController.RecoveryForDifficulty("Hard") &&
                  DeNelle.Village.RaidDeployController.RecoveryForDifficulty("Hard") <
                  DeNelle.Village.RaidDeployController.RecoveryForDifficulty("Extreme")))
            {
                f.Add("attrition is FLAT or inverted across difficulties -- the exact defect the " +
                      "2026-08-21 ruling closed (a flat 120s made raiding effectively free)");
            }

            // Recovery must stay well inside the camp's own cooldown, or the wait the player
            // actually feels stops being the cooldown and the tuned crystal bound is moot.
            foreach (string d in new[] { "Regular", "Hard", "Extreme" })
            {
                double cd = RaidCooldownService.DurationForDifficulty(d);
                double rec = DeNelle.Village.RaidDeployController.RecoveryForDifficulty(d);
                if (!(rec > 0d)) f.Add("attrition for " + d + " is non-positive -- a wipe would be a free retry");
                if (rec >= cd)
                    f.Add("attrition for " + d + " (" + rec.ToString("F0") + "s) is >= its cooldown (" +
                          cd.ToString("F0") + "s) -- recovery, not the cooldown, would be the felt wait");
            }

            // -- The authored data must agree with the code fallback ---------------
            // Both exist by design (JSON wins so a retune is a data edit; the table is the
            // fallback), which means they can DRIFT. Silent drift here is a balance change
            // nobody made.
            CheckAuthored(f, "raider_camp_small",  4d * 3600d);
            CheckAuthored(f, "fortified_garrison", 8d * 3600d);
            CheckAuthored(f, "mage_enclave",       12d * 3600d);
        }

        private static void CheckAuthored(List<string> f, string configId, double expectedSeconds)
        {
            SceneConfigDef def = null;
            try { def = SceneConfigCatalog.Find(configId); }
            catch (Exception ex)
            {
                f.Add("scene-config lookup for '" + configId + "' threw " + ex.GetType().Name);
                return;
            }
            if (def == null)
            {
                f.Add("scene-config '" + configId + "' not found -- the three flagship raid camps are " +
                      "the catalog this cooldown was balanced against");
                return;
            }
            if (def.raidCooldownSeconds <= 0f)
            {
                f.Add("scene-config '" + configId + "' has no authored raidCooldownSeconds -- the owner's " +
                      "ruled value is missing from the data and only the code fallback is holding it up");
                return;
            }
            AssertSeconds(f, "authored raidCooldownSeconds for '" + configId + "'",
                def.raidCooldownSeconds, expectedSeconds);
            // And the resolver must actually PREFER the authored value.
            AssertSeconds(f, "DurationFor('" + configId + "') resolves the authored value",
                RaidCooldownService.DurationFor(def), expectedSeconds);
        }

        private static void AssertSeconds(List<string> f, string what, double actual, double expected)
        {
            if (Math.Abs(actual - expected) > 0.5d)
                f.Add(what + " is " + actual.ToString("F0") + "s, owner ruling says " +
                      expected.ToString("F0") + "s");
        }

        // =====================================================================
        //  PIN C + D (lint) — the clock seam and the entry gate, at the call site
        // =====================================================================

        private static void SourceCases(List<string> f)
        {
            string service = SourceLint.ReadCode(ServiceRel, f);
            if (!string.IsNullOrEmpty(service))
            {
                // THE ONE THAT MATTERS MOST. A device-clock read here is invisible to every
                // behavioural case in this file -- the runtime cannot tell which clock it was
                // handed -- and it re-opens the exploit completely.
                if (service.Contains("DateTime.UtcNow") || service.Contains("DateTimeOffset.UtcNow") ||
                    service.Contains("DateTime.Now") || service.Contains("DateTimeOffset.Now"))
                {
                    f.Add("RaidCooldownService reads the DEVICE clock (DateTime/DateTimeOffset.UtcNow). " +
                          "A cooldown on the device clock is rolled forward in ten seconds; every " +
                          "behavioural pin in this suite would still pass");
                }
                if (!service.Contains("TimeSource.NowUnixMs()"))
                    f.Add("RaidCooldownService never calls TimeSource.NowUnixMs() -- the server-anchored " +
                          "clock seam is not being read at all");
                if (!service.Contains("TimeSource.IsServerAnchored"))
                    f.Add("RaidCooldownService never reads TimeSource.IsServerAnchored -- an offline clear " +
                          "would be indistinguishable from a trusted one and nothing could reconcile it");

                // WO-1128: the client RECORDS the trust and REFUSES NOTHING. A cold launch is
                // always unanchored, so a client-side penalty taxes every honest offline player.
                var begin = SourceLint.Body(service, @"public\s+static\s+double\s+Begin\s*\(\s*string\s+configId\s*,\s*double\s+durationSeconds\s*\)");
                if (string.IsNullOrEmpty(begin))
                    f.Add("RaidCooldownService.Begin(string,double) not found -- the write seam moved");
                else if (!begin.Contains("IsServerAnchored"))
                    f.Add("RaidCooldownService.Begin does not stamp the anchor flag onto the record");

                var remaining = SourceLint.Body(service, @"public\s+static\s+double\s+RemainingSeconds\s*\(\s*string\s+configId\s*\)");
                if (string.IsNullOrEmpty(remaining))
                    f.Add("RaidCooldownService.RemainingSeconds(string) not found -- the read seam moved");
                else if (remaining.IndexOf("FlowTrace.Warn", StringComparison.Ordinal) < 0)
                    f.Add("RaidCooldownService.RemainingSeconds has no FlowTrace.Warn -- a backwards-clock " +
                          "re-stamp would be a SILENT repair, and CLAUDE.md forbids silent failures");
            }

            // -- The door lint that USED to sit here is gone on purpose (WO-1379, 2026-09-05) --
            // It asserted "OnCardTapped checks IsOnCooldown( before RaidDeployScreen.Open(" -
            // the per-camp wall AT THE DOOR. The owner ruled "Heartfire replaces the camp wall",
            // so under the new canon that assertion is the defect. HeartfireRegression PIN F
            // now owns the door and pins the OPPOSITE (no RaidCooldownService reference on the
            // raid surface; HasCharge before Open). Not duplicated here: one door, one owner.

            // -- Every victory path must still STAMP the record (the gate went, the stamp stays) --
            string victory = SourceLint.ReadCode(VictoryRel, f);
            if (!string.IsNullOrEmpty(victory) && victory.IndexOf("RaidCooldownService.BeginAfterClear(", StringComparison.Ordinal) < 0)
                f.Add("RaidVictoryController never calls RaidCooldownService.BeginAfterClear -- clearing a " +
                      "camp starts no cooldown, so the whole gate is inert");
            string v2 = SourceLint.ReadCode(V2Rel, f);
            if (!string.IsNullOrEmpty(v2) && v2.IndexOf("RaidCooldownService.BeginAfterClear(", StringComparison.Ordinal) < 0)
                f.Add("Village2RaidController never calls RaidCooldownService.BeginAfterClear -- that camp " +
                      "stays instantly repeatable and it pays no loot, so the cooldown is its only pacing");

            // -- Attrition must be resolved, not hardcoded -------------------------
            string deploy = SourceLint.ReadCode(DeployRel, f);
            if (!string.IsNullOrEmpty(deploy))
            {
                var rec = SourceLint.Body(deploy, @"public\s+void\s+ReconcileRaidEnd\s*\(\s*int\s+starsEarned\s*\)");
                if (string.IsNullOrEmpty(rec))
                    f.Add("RaidDeployController.ReconcileRaidEnd(int) not found -- the attrition seam moved");
                else if (rec.IndexOf("ResolveRecoverySeconds(", StringComparison.Ordinal) < 0)
                    f.Add("ReconcileRaidEnd no longer resolves recovery from the camp -- attrition has gone " +
                          "back to a flat rate, which is what made raiding free");
            }
        }

        // =====================================================================
        //  PIN D (words) — the copy exists, in BOTH copies, in ASCII
        // =====================================================================

        private static void CanonCopyCases(List<string> f)
        {
            RaidStrings.Reload();   // never trust a map cached by an earlier suite

            string res = TryReadText(Path.Combine(Application.dataPath, CanonResRel));
            string stream = TryReadText(Path.Combine(Application.dataPath, CanonStreamRel));
            if (res == null) f.Add("canon-strings.json missing under Resources");
            if (stream == null) f.Add("canon-strings.json missing under StreamingAssets");

            foreach (string key in RaidStrings.AllKeys)
            {
                string v = RaidStrings.Get(key);
                if (string.IsNullOrEmpty(v) || v.StartsWith("[[missing:", StringComparison.Ordinal))
                {
                    f.Add("canon key '" + key + "' does not resolve -- a shipped string table carries a " +
                          "placeholder marker (no runtime surface shows the cooldown copy since WO-1379, " +
                          "but a key that exists must resolve)");
                    continue;
                }
                if (!IsAscii(v))
                    f.Add("canon key '" + key + "' has non-ASCII characters -- TMP renders them as tofu");

                // BOTH copies or neither. Patching one is the classic way this drifts, and the
                // build that reads the other one is the one the owner plays.
                string needle = "\"" + key + "\"";
                if (res != null && res.IndexOf(needle, StringComparison.Ordinal) < 0)
                    f.Add("canon key '" + key + "' missing from the Resources copy");
                if (stream != null && stream.IndexOf(needle, StringComparison.Ordinal) < 0)
                    f.Add("canon key '" + key + "' missing from the StreamingAssets copy");
            }

            // The humaniser must produce WORDS, and must never round DOWN: a card that says
            // "1m" while still refusing taps reads as broken.
            string twoHoursFifteen = RaidStrings.Humanise(2d * 3600d + 15d * 60d);
            if (twoHoursFifteen.IndexOf("2", StringComparison.Ordinal) < 0 ||
                twoHoursFifteen.IndexOf("15", StringComparison.Ordinal) < 0)
                f.Add("Humanise(2h15m) produced '" + twoHoursFifteen + "' -- the hours/minutes are not both there");
            string tenSeconds = RaidStrings.Humanise(10d);
            if (tenSeconds.IndexOf("0m", StringComparison.Ordinal) >= 0)
                f.Add("Humanise(10s) produced '" + tenSeconds + "' -- a sub-minute wait must not read as '0m'");
            string oneSecondOverAMinute = RaidStrings.Humanise(61d);
            if (oneSecondOverAMinute.IndexOf("1m", StringComparison.Ordinal) < 0 &&
                oneSecondOverAMinute.IndexOf("2m", StringComparison.Ordinal) < 0)
                f.Add("Humanise(61s) produced '" + oneSecondOverAMinute + "' -- expected a whole-minute reading");
        }

        private static bool IsAscii(string s)
        {
            for (int i = 0; i < s.Length; i++) if (s[i] > 127) return false;
            return true;
        }

        private static string TryReadText(string path)
        {
            try { return File.Exists(path) ? File.ReadAllText(path) : null; }
            catch (IOException) { return null; }
        }

        // =====================================================================
        //  PIN A + B + D (behaviour) — the real state machine on a real save
        // =====================================================================

        private static void StateMachineCases(List<string> f, out bool skipped, out string skipWhy)
        {
            skipped = false; skipWhy = null;

            GameStateService prior = GameStateService.Instance;
            GameObject gssGo = null;
            GameState throwaway = null;
            bool installed = false;
            string rawSave = DeNelle.Editor.HeadlessState.SnapshotSave(out bool hadSave);

            try
            {
                throwaway = ScriptableObject.CreateInstance<GameState>();
                gssGo = new GameObject("GameStateService (raid-cooldown-oracle)");
                var gss = gssGo.AddComponent<GameStateService>();
                if (!DeNelle.Editor.HeadlessState.TryInstall(gss, throwaway, out string installErr))
                { skipped = true; skipWhy = installErr; return; }
                installed = true;

                var state = gss.State;
                if (state == null) { skipped = true; skipWhy = "throwaway state did not install"; return; }

                // Anchor the clock so the whole run is measured on the same seam the device
                // uses when it has reached the backend. The VALUE is irrelevant; what matters
                // is that the anchor is the thing being read.
                ServerClock.ResetForTests();
                ServerClock.Sync(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                if (!TimeSource.IsServerAnchored)
                    f.Add("ServerClock.Sync did not make TimeSource.IsServerAnchored true -- the anchor " +
                          "seam moved and the whole forward-clock defence is off");

                const double Window = 3600d;   // 1h, so nothing here depends on the balance table

                // ── Case 1: a fresh camp is raidable ──────────────────────────────
                RaidCooldownService.ClearCooldown(ScratchId);
                if (RaidCooldownService.IsOnCooldown(ScratchId))
                    f.Add("case1 a camp with no record reads as ON COOLDOWN -- every camp would be locked");
                if (RaidCooldownService.RemainingSeconds(ScratchId) != 0d)
                    f.Add("case1 a camp with no record reports non-zero remaining");

                // ── Case 2: Begin opens the window, and it reads back ─────────────
                double stamped = RaidCooldownService.Begin(ScratchId, Window);
                if (Math.Abs(stamped - Window) > 0.5d)
                    f.Add("case2 Begin returned " + stamped.ToString("F0") + "s, expected " + Window.ToString("F0"));
                if (!RaidCooldownService.IsOnCooldown(ScratchId))
                    f.Add("case2 the camp is NOT on cooldown immediately after Begin -- the window never opened");
                double rem2 = RaidCooldownService.RemainingSeconds(ScratchId);
                if (rem2 > Window + 1d || rem2 < Window - 60d)
                    f.Add("case2 remaining was " + rem2.ToString("F0") + "s right after a " +
                          Window.ToString("F0") + "s Begin");

                // The anchored open must be RECORDED as anchored.
                var rec = FindRecord(state, ScratchId);
                if (rec == null) f.Add("case2 no record was written to GameState.RaidCooldowns");
                else if (!rec.ServerAnchored)
                    f.Add("case2 an ANCHORED open recorded ServerAnchored=false -- a trustworthy window " +
                          "would be reported as provisional");

                // -- Case 2b: the record's copy is still a SENTENCE naming the wait --
                // (WO-1379: no runtime surface shows these any more - the door refuses in
                // HeartfireService.BlockedMessage words - but while the keys exist in
                // canon-strings.json they must resolve to sentences, not placeholder markers.)
                string blocked = RaidCooldownService.BlockedMessage(ScratchId);
                if (string.IsNullOrEmpty(blocked) || blocked.StartsWith("[[missing:", StringComparison.Ordinal))
                    f.Add("case2b the blocked-tap refusal is a placeholder, not a sentence");
                else if (blocked.IndexOf("m", StringComparison.OrdinalIgnoreCase) < 0)
                    f.Add("case2b the refusal '" + blocked + "' names no duration -- a player told 'no' with " +
                          "no 'when' cannot act on it");
                string cardLine = RaidCooldownService.DescribeState(ScratchId);
                if (string.IsNullOrEmpty(cardLine) || cardLine.StartsWith("[[missing:", StringComparison.Ordinal))
                    f.Add("case2b the card state line is a placeholder, not a sentence");

                // ── Case 3 (PIN B): a BACKWARDS clock cannot shorten the wait ─────
                // Simulate the clock having moved backwards by pushing the STAMP into the
                // future, which is arithmetically identical and needs no clock control.
                rec = FindRecord(state, ScratchId);
                if (rec != null)
                {
                    double before = RaidCooldownService.RemainingSeconds(ScratchId);
                    rec = FindRecord(state, ScratchId);   // re-fetch: the read above may prune/re-stamp
                    if (rec != null)
                    {
                        rec.StartedUnixMs = TimeSource.NowUnixMs() + 10d * 60d * 1000d;   // "clock went back 10 min"
                        double after = RaidCooldownService.RemainingSeconds(ScratchId);

                        if (after < before - 1d)
                            f.Add("case3 a BACKWARDS clock SHORTENED the cooldown (" + before.ToString("F0") +
                                  "s -> " + after.ToString("F0") + "s) -- the wait is bypassable by setting " +
                                  "the phone clock back");
                        if (after > Window + 1d)
                            f.Add("case3 a BACKWARDS clock left the cooldown at " + after.ToString("F0") +
                                  "s, LONGER than the full " + Window.ToString("F0") + "s window -- an honest " +
                                  "player who crossed a timezone would be locked out. REFUSE, DON'T PUNISH");
                        if (Math.Abs(after - Window) > 2d)
                            f.Add("case3 after a backwards clock the window is " + after.ToString("F0") +
                                  "s, expected exactly one full " + Window.ToString("F0") + "s re-stamp");

                        var repaired = FindRecord(state, ScratchId);
                        if (repaired != null && repaired.StartedUnixMs > TimeSource.NowUnixMs() + 1000d)
                            f.Add("case3 the record's stamp is still in the FUTURE after the read -- the " +
                                  "repair did not happen and the camp will re-detect this forever");
                    }
                }

                // ── Case 4 (PIN A): a REAL save/load cold boot ────────────────────
                // Not a serialiser round trip: the whole device path (provider -> integrity
                // gate -> migrator -> validator -> ApplyPersisted), any step of which can
                // reject a payload and leave a fresh, cooldown-free state behind.
                RaidCooldownService.ClearCooldown(ScratchId);
                RaidCooldownService.Begin(ScratchId, Window);
                double preSave = RaidCooldownService.RemainingSeconds(ScratchId);

                var rebooted = ScriptableObject.CreateInstance<GameState>();   // a COLD BOOT's state
                try
                {
                    if (!DeNelle.Editor.HeadlessState.TryInstall(gss, rebooted, out string reErr))
                    {
                        f.Add("case4 could not install the rebooted state: " + reErr);
                    }
                    else if (!gss.Load())
                    {
                        f.Add("case4 GameStateService.Load() returned FALSE after a Save that wrote a raid " +
                              "cooldown -- the window did not survive the restart path (integrity gate / " +
                              "migrator / validator rejected it, or nothing was written)");
                    }
                    else
                    {
                        var back = FindRecord(gss.State, ScratchId);
                        if (back == null)
                        {
                            f.Add("case4 the cooldown was GONE after a real save/load restart -- a camp " +
                                  "cleared before a relaunch would be instantly raidable again, which is " +
                                  "the whole exploit the ticket exists to close");
                        }
                        else
                        {
                            if (Math.Abs(back.DurationSeconds - Window) > 0.5d)
                                f.Add("case4 the window LENGTH did not survive the restart (" +
                                      back.DurationSeconds.ToString("F0") + "s, expected " + Window.ToString("F0") + ")");
                            if (!(back.StartedUnixMs > 0d))
                                f.Add("case4 the window START did not survive the restart -- with no stamp " +
                                      "the remaining time cannot be computed");
                            if (!RaidCooldownService.IsOnCooldown(ScratchId))
                                f.Add("case4 the camp reads RAIDABLE after the restart even though the record " +
                                      "came back -- the read path disagrees with the persisted state");
                            double postLoad = RaidCooldownService.RemainingSeconds(ScratchId);
                            if (Math.Abs(postLoad - preSave) > 120d)
                                f.Add("case4 remaining moved from " + preSave.ToString("F0") + "s to " +
                                      postLoad.ToString("F0") + "s across the restart");
                        }
                    }
                }
                finally
                {
                    // ⛔ PUT A LIVE STATE BACK **BEFORE** DESTROYING THE COLD-BOOT ONE.
                    // Case 4 installed `rebooted` on the service; destroying it while it is still
                    // the installed state leaves GameStateService._state pointing at a DESTROYED
                    // ScriptableObject. Unity's fake-null then makes RaidCooldownService.Records()
                    // return null, so every later case runs with NO SAVE: case 5 passes vacuously
                    // and case 6's Begin() returns 0, which reads as "an unanchored clock changed
                    // the window length (0s vs 3600s)" -- a fixture lifetime bug wearing the
                    // costume of a clock-discipline defect. The assertions were right; the state
                    // under them had been demolished. Re-install the throwaway (still alive; the
                    // OUTER finally owns its destruction) so cases 5 and 6 measure a real save.
                    if (!DeNelle.Editor.HeadlessState.TryInstall(gss, throwaway, out string reinstallErr))
                        f.Add("case4 teardown could not re-install the throwaway state (" + reinstallErr +
                              ") -- every case after this one would run against no save and pass vacuously");
                    if (rebooted != null) UnityEngine.Object.DestroyImmediate(rebooted);
                }

                // FIXTURE HEALTH, asserted rather than assumed: if the save is not live here, the
                // cases below cannot fail no matter what the service does. A vacuous green is the
                // one outcome this suite must never produce.
                if (gss.State == null)
                    f.Add("case4 teardown left NO live GameState installed -- cases 5 and 6 would assert " +
                          "nothing (RaidCooldownService reads the save through GameStateService.Instance.State)");

                // ── Case 5: the dev/test hook actually clears ─────────────────────
                RaidCooldownService.ClearCooldown(ScratchId);
                if (RaidCooldownService.IsOnCooldown(ScratchId))
                    f.Add("case5 ClearCooldown left the camp on cooldown -- an unexercised hook proves nothing " +
                          "(the lesson RaidClaimService.ClearClaim was written to record)");

                // ── Case 6: an UNANCHORED clock is recorded, never punished ───────
                ServerClock.ResetForTests();
                if (TimeSource.IsServerAnchored)
                    f.Add("case6 ServerClock.ResetForTests left IsServerAnchored true (test seam broken)");
                double unanchored = RaidCooldownService.Begin(ScratchId, Window);
                if (Math.Abs(unanchored - Window) > 0.5d)
                    f.Add("case6 an UNANCHORED clock changed the window length (" + unanchored.ToString("F0") +
                          "s vs " + Window.ToString("F0") + "s). A cold launch is ALWAYS unanchored, so any " +
                          "client-side penalty taxes every honest offline player. Refuse server-side, never here");
                // NOTE: read through gss.State, not the `state` local — case 4 swapped the
                // installed state for the rebooted one, so the local is now a detached object
                // and asserting against it would silently test nothing.
                var uRec = FindRecord(gss.State, ScratchId);
                if (uRec != null && uRec.ServerAnchored)
                    f.Add("case6 an UNANCHORED open recorded ServerAnchored=true -- every window would claim " +
                          "to be trustworthy and the server's audit becomes a lie");
            }
            catch (Exception ex)
            {
                f.Add("state-machine cases threw: " + ex.GetType().Name + ": " + ex.Message);
            }
            finally
            {
                // The anchor is a process-wide static; leave it exactly as un-synced as a fresh
                // process so a later suite never inherits this oracle's trust.
                try { ServerClock.ResetForTests(); } catch (Exception) { }

                if (gssGo != null) UnityEngine.Object.DestroyImmediate(gssGo);
                if (throwaway != null) UnityEngine.Object.DestroyImmediate(throwaway);
                if (installed) DeNelle.Editor.HeadlessState.TrySetInstance(prior);
                DeNelle.Editor.HeadlessState.RestoreSave(hadSave, rawSave);
            }
        }

        private static RaidCooldownRecord FindRecord(GameState s, string configId)
        {
            if (s == null || s.RaidCooldowns == null) return null;
            for (int i = 0; i < s.RaidCooldowns.Count; i++)
            {
                var r = s.RaidCooldowns[i];
                if (r != null && string.Equals(r.ConfigId, configId, StringComparison.OrdinalIgnoreCase))
                    return r;
            }
            return null;
        }
    }
}
