using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.UI;

namespace DeNelle.Village.UI
{
    /// <summary>
    /// One-tap summary of the whole away window: the authoritative offline haul, Echo
    /// mending, the QUEUE JOBS THAT FINISHED, and what the collectors are holding.
    ///
    /// <para>LANE G -- THE RETURNING SESSION ACTUALLY REPORTS. The economy map
    /// (docs/PROGRAM_RAID_ECONOMY_2026-09-04.md sec.7) opens the ideal returning session on
    /// two beats this screen could not say: "BUILD COMPLETE -&gt; collect" and
    /// "Resources full -&gt; collect". Measured at source before the change: the gate here
    /// read <c>result.Total &lt;= 0 &amp;&amp; !result.HasMendNews</c>, so a player who
    /// finished three builds overnight and accrued no node harvest got NO SCREEN AT ALL, and
    /// the COLLECT button called <see cref="Dismiss"/> -- a button labelled with a verb that
    /// performed no verb.</para>
    ///
    /// <para>! THIS IS THE ONE RETURN-TIME SURFACE. Do NOT add a second popup for jobs or
    /// for collectors; rows go HERE. It is already registered with
    /// <see cref="PanelManager"/>, already suppressed during combat AND deferred until a hub
    /// scene is active (never over Title -- owner felt-test 2026-09-04) by
    /// OfflineHarvestService.TryShowPopup, and a second screen would have to re-earn
    /// all of it.</para>
    /// </summary>
    public sealed class WelcomeBackPopup : MonoBehaviour
    {
        private static WelcomeBackPopup s_active;
        private OfflineHarvestResult _result;
        private ElarionUiKit.ObsidianModal _modal;
        private PanelHandle _panelHandle;
        private bool _open;

        // -- Row geometry, shared by every row builder below --------------------
        /// <summary>Height of one data row in body-normalized units.</summary>
        private const float RowH = 0.095f;

        /// <summary>Gap under a data row.</summary>
        private const float RowGap = 0.012f;

        /// <summary>Rows stop here. Below this the body has run out and a row would be drawn
        /// off the bottom of the modal (the report can already carry seven lines -- see the
        /// COLLECT seating note further down).</summary>
        private const float MinRowY = 0.06f;

        /// <summary>At most this many finished jobs get their own row; the rest collapse into
        /// one aggregate line, so a long night can never push the report off-screen.</summary>
        private const int MaxJobRows = 3;

        /// <summary>At most this many per-resource collector rows (the rail has four resources;
        /// the body also has to hold the haul + job rows), the rest collapse into one "+N MORE" line.</summary>
        private const int MaxCollectorRows = 4;

        private static bool HasRoom(float y) => y - RowH >= MinRowY;

        public static void Show(OfflineHarvestResult result)
        {
            // THE GATE LIVES ON THE RESULT, not here (OfflineHarvestResult.HasSummaryContent):
            // haul OR mend OR a finished job OR resources waiting to be collected. Re-deriving
            // it at this call site is what let the collector-only town fall through the crack.
            if (result == null || !result.HasSummaryContent)
            {
                FlowTrace.Step("Offline",
                    "welcome-back: no summary content (haul/mend/jobs/collectors all empty) -- not shown.");
                return;
            }
            // NEVER REPLACE A SHOWN REPORT WITH A SMALLER ONE (owner felt-test 2026-09-04 22:29:
            // "YOUR REALM WORKED FOR 0m" after 12.6h away). The cold-load and resume triggers
            // raced during boot; the second claim measured ~0s and this rebuild threw the real
            // 12h report away. OfflineHarvestService now latches the claim itself; this is the
            // belt to that brace: a later result covering LESS away time than the one on screen
            // is not news, it is the same window measured again.
            if (s_active != null && s_active._result != null && result.AwaySeconds < s_active._result.AwaySeconds)
            {
                FlowTrace.Warn("Offline",
                    $"welcome-back: a later result (away {result.AwaySeconds:0}s, haul={result.Total}) would REPLACE the " +
                    $"open report (away {s_active._result.AwaySeconds:0}s, haul={s_active._result.Total}) with a smaller " +
                    "window -- kept the first; the later result is a re-measure of the same window.");
                return;
            }
            FlowTrace.Step("Offline",
                $"welcome-back REVEAL: haul={result.Total} mendNews={result.HasMendNews} " +
                $"jobs={result.CompletedJobCount} collectorsPending={result.PendingCollectorTotal} " +
                $"across {result.PendingCollectorCount} collector(s).");
            if (s_active != null) s_active.Dismiss();
            var host = new GameObject("WelcomeBackPopup");
            var popup = host.AddComponent<WelcomeBackPopup>();
            s_active = popup;
            popup._result = result;
            popup.BuildUi();
        }

        private void BuildUi()
        {
            _modal = ElarionUiKit.BuildObsidianModal("WelcomeBackUI", "WELCOME BACK, KEEPER",
                new Vector2(0.18f, 0.08f), new Vector2(0.82f, 0.92f), Dismiss,
                sortingOrder: 32020, frameName: RpgUiCatalog.FrameCore);
            if (_modal == null || _modal.canvas == null) { Dismiss(); return; }
            MedievalUiSkin.ApplyShell(_modal.chrome, compact: false);

            // ONE BUTTON (owner felt-test 2026-09-04 22:30: "collect close over top of each other,
            // dont need both"). The kit's modal builder seats its own shared CLOSE face in the bottom
            // thumb band with no label/hook parameter, and COLLECT below is seated on the same
            // band -- so the two faces overprinted. COLLECT already dismisses; the shell's Close
            // face is hidden, not destroyed (the kit still owns it; the scrim + PanelManager back
            // path still reach Dismiss for a no-collect exit).
            if (_modal.chrome != null && _modal.chrome.close != null)
                _modal.chrome.close.gameObject.SetActive(false);

            if (_modal.chrome.layout != null && _modal.chrome.layout.body != null)
            {
                var bodyRect = _modal.chrome.layout.body;
                bodyRect.anchorMin = new Vector2(bodyRect.anchorMin.x, 0.22f);
                bodyRect.anchorMax = new Vector2(bodyRect.anchorMax.x, 0.82f);
                bodyRect.offsetMin = Vector2.zero;
                bodyRect.offsetMax = Vector2.zero;
            }

            var body = _modal.chrome.layout != null && _modal.chrome.layout.body != null
                ? (Transform)_modal.chrome.layout.body : _modal.chrome.content.transform;

            var summary = ElarionUiKit.Label(body, AwayText(), 0.86f, 0.98f,
                ElarionUi.Parchment, ElarionUi.FontLabel, TextAlignmentOptions.Center,
                0.05f, 0.95f, bold: false);
            ElarionUiKit.FitSingleLine(summary);

            float y = 0.82f;
            // WO-1434 -- the BANKED haul first (what already landed in the wallet during the
            // claim), then the one waiting-table. These are different facts and they are never
            // the same units: Grant() has already applied the haul, while the table below is
            // what COLLECT would move. On the owner's 2026-09-06 capture the haul was 0 on
            // every axis ("accrued over 13221s: worker-owned=0 node(s), total=0"), which is why
            // no haul row drew -- correctly.
            AddResourceRow(body, ref y, _result.AetherCrystals, "AETHER CRYSTALS");
            AddResourceRow(body, ref y, _result.Food, "STONE");
            AddResourceRow(body, ref y, _result.Iron, "IRON");
            AddResourceRow(body, ref y, _result.Wood, "WOOD");
            AddMendRows(body, ref y);
            AddCompletedJobRows(body, ref y);
            AddCollectorRow(body, ref y);

            if (_result.WasCapped)
            {
                // WO-1434 - THE SUBJECT OF THIS SENTENCE WAS WRONG. `WasCapped` is
                // `window.ExceedsCap(OfflineCapHours)` (OfflineHarvestService:385) -- the AWAY
                // WINDOW hit its 10h ceiling. It says nothing about storage, and the old line
                // ("Storage filled while you were away. Check in sooner to keep every reward.")
                // named storage AND implied a reward had been taken away. Neither is true: the
                // window cap means later hours never accrued, and nothing that DID accrue is
                // ever discarded (see the proof block on OfflineHarvestService.BuildReturnRows).
                // !! The header suffix "(STORAGE FULL)" in AwayTextFor carries the SAME wrong
                // subject and is pinned by AwaySummaryReportRegression case8 (line 243), so it
                // is left alone here rather than half-moved -- it needs its own pin move.
                var capped = ElarionUiKit.Label(body,
                    // No number here ON PURPOSE: the ceiling is OfflineHarvestService
                    // .OfflineCapHours, and a copy of it in this string is duplicated state that
                    // goes stale the day the storage ladder raises the window (offline-storage
                    // .json authors 10h/12h/16h/24h/36h per tier). Name the rule, not the value.
                    "Your realm gathers for a limited stretch while you are away. Nothing gathered is lost.",
                    Mathf.Max(0.03f, y - 0.12f), y, ElarionUi.Gold,
                    ElarionUi.FontMicro, TextAlignmentOptions.Center, 0.06f, 0.94f, bold: true);
                ElarionUiKit.FitBlock(capped, 26f, ElarionUi.FontMicro);
            }

            // This report can contain seven data lines; the generic footer zone is
            // re-seated above the shared Close reservation and lands in that data stack.
            // Seat the sole action directly in the shell's bottom thumb band instead.
            var collect = ElarionUiKit.BuildObsidianButton(_modal.chrome.content.transform, "COLLECT",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                new Vector2(0.37f, 0.045f), new Vector2(0.63f, 0.155f), CollectAndDismiss);
            MedievalUiSkin.ApplyButton(collect, primary: true);
            var face = collect != null ? collect.targetGraphic as Image : null;
            if (face != null) face.type = Image.Type.Simple;

            _open = true;
            _panelHandle = PanelManager.Register("Welcome Back", Dismiss, () => _open);
            if (!PanelManager.NotifyOpened(_panelHandle)) Dismiss();
        }

        private static void AddResourceRow(Transform body, ref float y, int amount, string label)
        {
            if (amount <= 0) return;
            const float h = 0.095f;
            var plate = ElarionUiKit.AddImage(body, "Reward_" + label,
                new Vector2(0.08f, y - h), new Vector2(0.92f, y),
                new Color(0.05f, 0.045f, 0.04f, 0.96f), rounded: false);
            var name = ElarionUiKit.Label(plate.transform, label, 0f, 1f,
                ElarionUi.Parchment, ElarionUi.FontMicro, TextAlignmentOptions.Left,
                0.05f, 0.70f, bold: false);
            var value = ElarionUiKit.Label(plate.transform, "+" + amount, 0f, 1f,
                ElarionUi.Gold, ElarionUi.FontLabel, TextAlignmentOptions.Right,
                0.70f, 0.95f, bold: true);
            ElarionUiKit.FitSingleLine(name); ElarionUiKit.FitSingleLine(value);
            y -= h + 0.012f;
        }

        private void AddMendRows(Transform body, ref float y)
        {
            var mend = _result != null ? _result.Mend : null;
            if (mend == null || !mend.HasContent) return;
            AddMendLine(body, ref y, EchoMendCopy.AwayMendedLine(mend), ElarionUi.Parchment);
            AddMendLine(body, ref y, EchoMendCopy.AwaySpentLine(mend), ElarionUi.ParchmentDim);
            AddMendLine(body, ref y, EchoMendCopy.AwayStallLine(mend), ElarionUi.Gold);
        }

        private static void AddMendLine(Transform body, ref float y, string text, Color color)
        {
            if (string.IsNullOrEmpty(text)) return;
            const float h = 0.09f;
            var label = ElarionUiKit.Label(body, text, y - h, y, color,
                ElarionUi.FontMicro, TextAlignmentOptions.Center, 0.07f, 0.93f, bold: true);
            ElarionUiKit.FitBlock(label, 24f, ElarionUi.FontMicro);
            y -= h + 0.01f;
        }

        // =====================================================================
        //  LANE G -- the two rows the returning session was missing
        // =====================================================================

        /// <summary>
        /// "BUILD COMPLETE -&gt; collect" (economy map sec.7 beat 1). One row per finished
        /// queue job, capped at <see cref="MaxJobRows"/> with an aggregate line for the rest.
        /// <para>The verb and the label come from the SHARED card seam
        /// (BuildTimerService.EntryFor, carried on OfflineHarvestResult.OfflineJobLine), so
        /// the away summary says the same words the queue card said while the job was
        /// running -- never a second vocabulary.</para>
        /// </summary>
        private void AddCompletedJobRows(Transform body, ref float y)
        {
            var jobs = _result != null ? _result.CompletedJobs : null;
            if (jobs == null || jobs.Count == 0) return;

            int rendered = 0;
            for (int i = 0; i < jobs.Count && rendered < MaxJobRows; i++)
            {
                var j = jobs[i];
                if (j == null) continue;
                if (!HasRoom(y)) break;
                string verb = string.IsNullOrEmpty(j.Verb) ? "COMPLETE" : j.Verb;
                string label = string.IsNullOrEmpty(j.Label) ? "JOB" : j.Label;
                AddPlateRow(body, ref y, verb + " COMPLETE", label, ElarionUi.Gold);
                rendered++;
            }

            int remaining = jobs.Count - rendered;
            if (remaining <= 0) return;
            if (HasRoom(y)) AddPlateRow(body, ref y, "ALSO FINISHED", "+" + remaining + " MORE", ElarionUi.Gold);
            else
                FlowTrace.Warn("Offline",
                    $"welcome-back: {remaining} finished job(s) had no room left in the report body " +
                    "(the haul + mend lines filled it) -- they are NOT lost, only unlisted.");
        }

        /// <summary>
        /// "Resources full -&gt; collect" (economy map sec.7 beat 2). What the collectors are
        /// STILL HOLDING -- this claim did not bank it, the COLLECT button does.
        /// <para>! COPY LAW (CollectorStatusGate's header): "Storage" / "Bank" belongs to the
        /// WALLET; this surface says COLLECTORS. The existing capped-haul line above does say
        /// STORAGE, and that is the same distinction rather than a contradiction: that line is
        /// about the wallet cap, this one is about collectors.</para>
        /// </summary>
        private void AddCollectorRow(Transform body, ref float y)
        {
            if (_result == null) return;
            if (!_result.HasCollectorNews && !_result.HasSiloNews) return;

            // =================================================================
            //  WO-1434 -- ONE ROW PER RESOURCE, AMOUNT AND DESTINY TOGETHER.
            // -----------------------------------------------------------------
            //  WHAT THIS METHOD USED TO DRAW, from the owner's device 2026-09-06
            //  (build 358161, screencap 12:50:59):
            //      WOOD  WAITING   +10609        <- this loop
            //      IRON  WAITING    +6365
            //      STONE WAITING   +25808
            //      Storage nearly full - 10609 wood will wait     <- AddCollectWaitRows
            //      Storage nearly full - 6365 iron will wait
            //      Storage nearly full - 25808 stone will wait
            //  Six lines for three facts, each integer printed twice, and every row
            //  wearing a reward's "+" while its ENTIRE amount was going to wait. She
            //  tapped COLLECT and banked ZERO ("[Flow:Eco] Grant +W0 +I0").
            //
            //  AND IT WAS DRAWING THREE OF FIVE. The Echo silo -- 57,600 units, at cap,
            //  the largest single thing that happened while she was away -- had no row
            //  here and no term in the reveal gate. It reached the player only AFTER the
            //  tap, in the harvest-result modal, described as "lost".
            //
            //  The table below is the fix: OfflineHarvestService.BuildReturnRows merges
            //  BOTH producers per resource and pairs each amount with what becomes of it,
            //  so there is one row per resource and no line repeats another's number.
            //  Nothing here is lost -- see the proof block on BuildReturnRows.
            // =================================================================
            List<OfflineHarvestService.ReturnRow> rows = null;
            Guard.Try("Offline", "build the welcome-back resource table",
                () => rows = OfflineHarvestService.BuildReturnRows(_result));

            // The old single "3 COLLECTORS WAITING +16716" line survives ONLY as the fallback for
            // a result that carries totals but no per-resource lines (older producers / an oracle
            // fixture that fills PendingCollectorTotal and nothing else).
            if (rows == null || rows.Count == 0)
            {
                if (!_result.HasCollectorNews || !HasRoom(y)) return;
                string left = _result.PendingCollectorCount == 1
                    ? "COLLECTOR WAITING"
                    : _result.PendingCollectorCount + " COLLECTORS WAITING";
                AddPlateRow(body, ref y, left, "+" + _result.PendingCollectorTotal, ElarionUi.Gold);
                return;
            }

            int rendered = 0, next = 0;
            while (next < rows.Count && rendered < MaxCollectorRows && HasRoom(y))
            {
                var r = rows[next];
                next++;
                string label = OfflineHarvestService.ReturnRowLabel(r);
                string destiny = OfflineHarvestService.ReturnRowDestiny(r);
                FlowTrace.Step("Offline",
                    $"welcome-back row: {r.Word} pending={r.Pending} (collectors {r.FromCollectors} + silo {r.FromSilo}) " +
                    $"headroom={r.Headroom} banks={r.Banks} stays={r.Waits} -> '{label}' | '{destiny}'.");
                // The value column is WIDE here because it carries a sentence, not a number.
                AddPlateRow(body, ref y, label, destiny, ElarionUi.Gold, valueSplit: 0.46f);
                rendered++;
            }

            // Whatever the row budget (or the body) could not seat collapses into one line.
            int remaining = 0, remainingUnits = 0;
            for (int i = next; i < rows.Count; i++) { remaining++; remainingUnits += rows[i].Pending; }
            if (remaining > 0)
            {
                if (HasRoom(y)) AddPlateRow(body, ref y, "ALSO WAITING", remainingUnits + " MORE", ElarionUi.Gold);
                else
                    FlowTrace.Warn("Offline",
                        $"welcome-back: {remaining} more resource row(s) ({remainingUnits} units) had no room left " +
                        "in the report body -- they stay where they are, only the row is unlisted.");
            }

            AddDestinyFooter(body, ref y, rows);
        }

        /// <summary>
        /// WO-1434 - the two sentences that belong to the TABLE rather than to any one row.
        /// <para>(1) What happens to everything that will not fit, said plainly and without the
        /// word "lost" - because nothing is lost, on either producer (proof block on
        /// OfflineHarvestService.BuildReturnRows).</para>
        /// <para>(2) `FOUNDATIONAL_RULINGS.md` section 7: a player earning nothing into a
        /// resource must be TOLD, in words. A silo at its ceiling means the Echoes have stopped
        /// gathering entirely, which no row can say - a half-full silo also has a waiting row and
        /// is still filling.</para>
        /// Words and layout, never hue: the owner is red/green colourblind.
        /// </summary>
        private void AddDestinyFooter(Transform body, ref float y,
                                      IReadOnlyList<OfflineHarvestService.ReturnRow> rows)
        {
            string stalled = OfflineHarvestService.SiloStalledLine(_result);
            if (!string.IsNullOrEmpty(stalled))
            {
                if (HasRoom(y)) AddMendLine(body, ref y, stalled, ElarionUi.Gold);
                else FlowTrace.Warn("Offline",
                    "welcome-back: the Echo-silo-full line had no room left in the report body -- the silo is " +
                    "still full and still gathering nothing; only the sentence is unlisted.");
            }

            string footer = OfflineHarvestService.ReturnFooterLine(rows);
            if (string.IsNullOrEmpty(footer)) return;
            if (!HasRoom(y))
            {
                FlowTrace.Warn("Offline",
                    $"welcome-back: the footer '{footer}' had no room left in the report body -- the units still " +
                    "stay where they are on COLLECT (never burned), only the sentence is unlisted.");
                return;
            }
            AddMendLine(body, ref y, footer, ElarionUi.Gold);
        }

        /// <summary>The shared two-column plate row: label left, value right. Extracted so a
        /// job row and a collector row cannot drift from a haul row.
        /// <para>WO-1434 - <paramref name="valueSplit"/> is where the value column starts (0..1
        /// across the plate). The default 0.70 suits a NUMBER; a WO-1434 destiny row's value is a
        /// short sentence ("258 FITS, 10351 STAYS") and needs the wider 0.46, or FitSingleLine
        /// shrinks it to unreadable on a phone. Layout carries meaning here, so the split is a
        /// parameter rather than a second row builder that could drift.</para></summary>
        private static void AddPlateRow(Transform body, ref float y, string left, string right, Color rightColor,
                                        float valueSplit = 0.70f)
        {
            if (string.IsNullOrEmpty(left)) return;
            var plate = ElarionUiKit.AddImage(body, "Row_" + left,
                new Vector2(0.08f, y - RowH), new Vector2(0.92f, y),
                new Color(0.05f, 0.045f, 0.04f, 0.96f), rounded: false);
            float split = Mathf.Clamp(valueSplit, 0.30f, 0.90f);
            // The label column ends AT the split for the default (0.70), byte-identical to the
            // pre-WO-1434 geometry the job/haul rows were tuned against; only a WIDE value column
            // takes the 0.02 gutter, where the two texts would otherwise touch.
            float labelEnd = split >= 0.60f ? split : split - 0.02f;
            var name = ElarionUiKit.Label(plate.transform, left, 0f, 1f,
                ElarionUi.Parchment, ElarionUi.FontMicro, TextAlignmentOptions.Left,
                0.05f, labelEnd, bold: false);
            // A number gets the display face; a sentence gets the body face, or FitSingleLine
            // shrinks it past legibility on a phone. Keyed off the split so a caller cannot pick
            // a wide column and a display font together by accident.
            int valueFont = split < 0.60f ? ElarionUi.FontMicro : ElarionUi.FontLabel;
            var value = ElarionUiKit.Label(plate.transform, right ?? string.Empty, 0f, 1f,
                rightColor, valueFont, TextAlignmentOptions.Right,
                split, 0.95f, bold: true);
            ElarionUiKit.FitSingleLine(name); ElarionUiKit.FitSingleLine(value);
            y -= RowH + RowGap;
        }

        /// <summary>
        /// The COLLECT button's verb, finally performed. It carries the tap to the EXISTING
        /// command -- <see cref="CollectorStatusGate.RequestCollectAll"/>, the same seam the
        /// ambient HUD collectors chip taps -- and then dismisses. No second collect command is
        /// minted here, and this screen never touches a collector directly.
        /// <para>With no Village-side listener installed (a boot race) the tap must still close
        /// the screen rather than dead-end, and it says so in the trace.</para>
        /// </summary>
        private void CollectAndDismiss()
        {
            // WO-1434 - THE SILO IS A REASON TO COLLECT TOO. This gate read HasCollectorNews
            // alone, so a town whose collectors were empty but whose Echo silo was FULL got a
            // COLLECT button that only dismissed -- and ResourceCollectorService.CollectAll is
            // the very call that dumps the silo (it wraps `echo.DumpSilos()`). Same verb, same
            // one command; only the gate was too narrow.
            if (_result != null && (_result.HasCollectorNews || _result.HasSiloNews))
            {
                if (CollectorStatusGate.HasSubscriber)
                {
                    FlowTrace.Step("Offline",
                        "welcome-back COLLECT -> CollectorStatusGate.RequestCollectAll (" +
                        _result.PendingCollectorTotal + " waiting across " +
                        _result.PendingCollectorCount + " collector(s), plus " +
                        _result.SiloTotal + " in the Echo silo).");
                    Guard.Try("Offline", "welcome-back collect-all",
                        () => CollectorStatusGate.RequestCollectAll());
                }
                else
                {
                    FlowTrace.Warn("Offline",
                        "welcome-back COLLECT tapped but no Village listener is installed on " +
                        "CollectorStatusGate -- nothing banked; the pending is untouched and the " +
                        "collectors chip can still bank it.");
                }
            }
            else
            {
                FlowTrace.Step("Offline", "welcome-back COLLECT tapped with nothing pending -- dismiss only.");
            }
            Dismiss();
        }

        private string AwayText() => AwayTextFor(_result.AwaySeconds, _result.WasCapped);

        /// <summary>The whole summary line, exposed for the away-summary oracle.</summary>
        public static string AwayTextFor(double awaySeconds, bool wasCapped)
        {
            string span = FormatAwaySpan(awaySeconds);
            return wasCapped ? $"YOUR REALM WORKED FOR {span} (STORAGE FULL)" : $"YOUR REALM WORKED FOR {span}";
        }

        /// <summary>
        /// Away span as hours AND minutes (owner ruling 2026-09-04 22:29: "minutes and hours if
        /// applicable should show"). Deterministic, ASCII, floor-based, and it can never print
        /// "0m": under a minute reads "under 1m".
        /// <code>
        ///   45328 s -> "12h 35m"     59 s -> "under 1m"     3600 s -> "1h 0m"
        ///   90000 s -> "1d 1h"       2100 s -> "35m"
        /// </code>
        /// Days carry hours only (a day-scale absence does not need its minutes).
        /// </summary>
        public static string FormatAwaySpan(double awaySeconds)
        {
            if (double.IsNaN(awaySeconds) || awaySeconds < 0.0) awaySeconds = 0.0;
            long total = (long)System.Math.Floor(awaySeconds);
            if (total < 60L) return "under 1m";
            long days = total / 86400L;
            long hours = (total % 86400L) / 3600L;
            long minutes = (total % 3600L) / 60L;
            if (days >= 1L) return days + "d " + hours + "h";
            if (hours >= 1L) return hours + "h " + minutes + "m";
            return minutes + "m";
        }

        /// <summary>
        /// WO-1414 A -- close the open report, if any, because it is no longer about this game.
        /// The one caller is <c>OfflineHarvestService</c>'s New Game hook: a summary already ON
        /// SCREEN when START NEW is pressed was measured on the PREVIOUS save, and the reset has
        /// no other way to reach it (this popup is code-built and owns its own host object).
        /// Safe when nothing is open -- a no-op that still traces, so a capture can tell "nothing
        /// was open" from "the hook never ran".
        /// </summary>
        public static void DismissIfOpen(string why)
        {
            var open = s_active;
            if (open == null)
            {
                FlowTrace.Step("Offline", $"welcome-back DismissIfOpen({why}): nothing open.");
                return;
            }
            double away = open._result != null ? open._result.AwaySeconds : 0.0;
            FlowTrace.Step("Offline",
                $"welcome-back DISMISSED ({why}): the open report covered {away:0}s of away time on the " +
                "previous save.");
            open.Dismiss();
        }

        private void Dismiss()
        {
            _open = false;
            if (_panelHandle != null) { PanelManager.NotifyClosed(_panelHandle); _panelHandle = null; }
            if (_modal != null && _modal.canvas != null)
            {
                if (Application.isPlaying) Destroy(_modal.canvas); else DestroyImmediate(_modal.canvas);
            }
            _modal = null;
            if (s_active == this) s_active = null;
            if (gameObject != null)
            {
                if (Application.isPlaying) Destroy(gameObject); else DestroyImmediate(gameObject);
            }
        }

        private void OnDestroy()
        {
            _open = false;
            if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle);
            if (s_active == this) s_active = null;
        }
    }
}
