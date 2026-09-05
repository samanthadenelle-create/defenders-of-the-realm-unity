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
            AddResourceRow(body, ref y, _result.AetherCrystals, "AETHER CRYSTALS");
            AddResourceRow(body, ref y, _result.Food, "STONE");
            AddResourceRow(body, ref y, _result.Iron, "IRON");
            AddResourceRow(body, ref y, _result.Wood, "WOOD");
            AddMendRows(body, ref y);
            AddCompletedJobRows(body, ref y);
            AddCollectorRow(body, ref y);

            if (_result.WasCapped)
            {
                var capped = ElarionUiKit.Label(body,
                    "Storage filled while you were away. Check in sooner to keep every reward.",
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
            if (_result == null || !_result.HasCollectorNews) return;
            if (!HasRoom(y))
            {
                FlowTrace.Warn("Offline",
                    $"welcome-back: {_result.PendingCollectorTotal} waiting in collectors had no room " +
                    "left in the report body -- not lost, only unlisted (COLLECT still banks it).");
                return;
            }

            // OWNER RULINGS 2026-09-04 22:30 ("the collectors need to be seperated" / "Wood Iron
            // Stone different rows"): one plate row PER RESOURCE in the HUD rail's order, the
            // resource word left ("WOOD WAITING") and the summed pending right ("+1240"), on the
            // SAME AddPlateRow geometry as the haul rows above so the report reads as one table.
            // The old single "3 COLLECTORS WAITING +16716" line survives ONLY as the fallback for
            // a result that carries totals but no per-resource lines (older producers / the oracle).
            var lines = _result.PendingCollectors;
            if (lines == null || lines.Count == 0)
            {
                string left = _result.PendingCollectorCount == 1
                    ? "COLLECTOR WAITING"
                    : _result.PendingCollectorCount + " COLLECTORS WAITING";
                AddPlateRow(body, ref y, left, "+" + _result.PendingCollectorTotal, ElarionUi.Gold);
                return;
            }

            int rendered = 0, next = 0;
            while (next < lines.Count && rendered < MaxCollectorRows && HasRoom(y))
            {
                var line = lines[next];
                next++;
                if (line == null || line.Pending <= 0) continue;
                string word = string.IsNullOrEmpty(line.Resource) ? "RESOURCE" : line.Resource.ToUpperInvariant();
                AddPlateRow(body, ref y, word + " WAITING", "+" + line.Pending, ElarionUi.Gold);
                rendered++;
            }

            // Whatever the row budget (or the body) could not seat collapses into one line.
            int remaining = 0, remainingUnits = 0;
            for (int i = next; i < lines.Count; i++)
            {
                if (lines[i] == null || lines[i].Pending <= 0) continue;
                remaining++;
                remainingUnits += lines[i].Pending;
            }
            if (remaining <= 0) return;
            if (HasRoom(y)) AddPlateRow(body, ref y, "ALSO WAITING", "+" + remainingUnits + " MORE", ElarionUi.Gold);
            else
                FlowTrace.Warn("Offline",
                    $"welcome-back: {remaining} more resource row(s) ({remainingUnits} units) had no room left " +
                    "in the report body -- not lost, only unlisted (COLLECT still banks it).");
        }

        /// <summary>The shared two-column plate row: label left, value right. Extracted so a
        /// job row and a collector row cannot drift from a haul row.</summary>
        private static void AddPlateRow(Transform body, ref float y, string left, string right, Color rightColor)
        {
            if (string.IsNullOrEmpty(left)) return;
            var plate = ElarionUiKit.AddImage(body, "Row_" + left,
                new Vector2(0.08f, y - RowH), new Vector2(0.92f, y),
                new Color(0.05f, 0.045f, 0.04f, 0.96f), rounded: false);
            var name = ElarionUiKit.Label(plate.transform, left, 0f, 1f,
                ElarionUi.Parchment, ElarionUi.FontMicro, TextAlignmentOptions.Left,
                0.05f, 0.70f, bold: false);
            var value = ElarionUiKit.Label(plate.transform, right ?? string.Empty, 0f, 1f,
                rightColor, ElarionUi.FontLabel, TextAlignmentOptions.Right,
                0.70f, 0.95f, bold: true);
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
            if (_result != null && _result.HasCollectorNews)
            {
                if (CollectorStatusGate.HasSubscriber)
                {
                    FlowTrace.Step("Offline",
                        "welcome-back COLLECT -> CollectorStatusGate.RequestCollectAll (" +
                        _result.PendingCollectorTotal + " waiting across " +
                        _result.PendingCollectorCount + " collector(s)).");
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

        private string AwayText()
        {
            double hours = _result.AwaySeconds / 3600.0;
            string span = hours >= 1.0 ? $"{hours:0.#}h" : $"{Mathf.RoundToInt((float)(_result.AwaySeconds / 60.0))}m";
            return _result.WasCapped ? $"YOUR REALM WORKED FOR {span} (STORAGE FULL)" : $"YOUR REALM WORKED FOR {span}";
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
