using System.Collections.Generic;
using TMPro;
using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Economy;

namespace DeNelle.Core.UI
{
    /// <summary>Phone-safe, modal-owned presentation for one player-chosen harvest batch.</summary>
    public sealed class HarvestOverflowModal : MonoBehaviour
    {
        private static HarvestOverflowModal _active;
        private ElarionUiKit.ObsidianModal _modal;
        private PanelHandle _panel;
        private WorldHold.Handle _hold;

        /// <summary>
        /// WO-1392 - the <see cref="BankOverflowStatus.Source"/> tag of a row produced by the
        /// COLLECTOR sweep (ResourceCollectorService.BuildCollectorRows). Those rows report units
        /// that are STILL WAITING in the collectors - nothing was burned - so the body copy for
        /// them says "waiting", never "lost". Every other source (the Echo silo dump's
        /// TownBankCapacity clamp, the offline haul) still burns its overflow today and keeps
        /// the "lost" sentence.
        /// </summary>
        public const string CollectorSource = "Collectors";

        // WO-1392 - ONE screen per tap. CollectAll banks the collectors AND dumps the Echo silo,
        // and each half used to reach Present on its own (the silo through its warn scope), so
        // the second call closed the first. While a batch is open, Present only QUEUES; the
        // outermost EndBatch presents everything once, collector rows first.
        private static readonly List<BankOverflowStatus> _batch = new List<BankOverflowStatus>();
        private static int _batchDepth;
        private static string _batchTag;

        /// <summary>Open a presentation batch (nesting counted; dispose it - prefer <c>using</c>).</summary>
        public static Batch BeginBatch(string tag) => new Batch(tag);

        /// <summary>True while a batch is open (the oracle seam for [one-modal-per-tap]).</summary>
        public static bool BatchOpen => _batchDepth > 0;

        /// <summary>Rows queued in the open batch (the oracle seam; empty outside a batch).</summary>
        public static IReadOnlyList<BankOverflowStatus> BatchedRows => _batch;

        public struct Batch : System.IDisposable
        {
            private bool _open;
            internal Batch(string tag)
            {
                _open = true;
                if (_batchDepth == 0) { _batch.Clear(); _batchTag = string.IsNullOrEmpty(tag) ? "?" : tag; }
                _batchDepth++;
            }
            public void Dispose()
            {
                if (!_open) return;
                _open = false;
                if (_batchDepth > 0) _batchDepth--;
                if (_batchDepth == 0) EndBatch();
            }
        }

        private static void EndBatch()
        {
            if (_batch.Count == 0) { _batchTag = null; return; }
            var rows = new List<BankOverflowStatus>(_batch);
            _batch.Clear();
            FlowTrace.Step("Bank", $"harvest-result batch [{_batchTag ?? "?"}] closing with {rows.Count} row(s) -> one modal.");
            _batchTag = null;
            Present(rows);
        }

        public static void Present(IReadOnlyList<BankOverflowStatus> results)
        {
            if (results == null || results.Count == 0) return;
            if (_batchDepth > 0)
            {
                for (int i = 0; i < results.Count; i++) _batch.Add(results[i]);
                FlowTrace.Step("Bank", $"harvest-result: {results.Count} row(s) queued into batch [{_batchTag ?? "?"}].");
                return;
            }
            if (!Application.isPlaying) return;
            if (_active != null) _active.Close();
            var host = new GameObject("HarvestOverflowModalHost");
            DontDestroyOnLoad(host);
            _active = host.AddComponent<HarvestOverflowModal>();
            _active.Open(results);
        }

        private void Open(IReadOnlyList<BankOverflowStatus> results)
        {
            _hold = WorldHold.Acquire("harvest-overflow-result");
            _modal = ElarionUiKit.BuildObsidianModal("HarvestOverflowUI", "HARVEST RESULT",
                new Vector2(0.16f, 0.08f), new Vector2(0.84f, 0.92f), Close,
                sortingOrder: 31020);
            MedievalUiSkin.ApplyShell(_modal.chrome, compact: false);
            _panel = PanelManager.Register("Harvest Result", Close,
                () => _modal != null && _modal.canvas != null && _modal.canvas.activeInHierarchy);
            if (!PanelManager.NotifyOpened(_panel)) { Close(); return; }

            var content = _modal.chrome.content.transform;
            string body = BuildBody(results);
            var label = ElarionUiKit.Label(content, body, 0.27f, 0.84f, ElarionUi.Parchment,
                ElarionUi.FontBody, TextAlignmentOptions.TopLeft, 0.09f, 0.91f, bold: false);
            ElarionUiKit.FitBlock(label, ElarionUi.FontFloorMobile, ElarionUi.FontBody);
            var close = ElarionUiKit.Button(content, "Close", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.34f, 0.09f), new Vector2(0.66f, 0.22f), Close);
            MedievalUiSkin.ApplyButton(close, primary: false);
            // WO-1370 §12 - the modal's OWN numbers are traced, so a screenshot of unreadable copy
            // can be checked against what the code actually put on the screen (the previous trace
            // logged only a row COUNT, which proved nothing about legibility).
            var trace = new System.Text.StringBuilder();
            for (int i = 0; i < results.Count; i++)
            {
                var r = results[i];
                if (i > 0) trace.Append(" | ");
                trace.Append($"{r.ResourceName} granted={r.Granted}/{r.Requested} lost={r.Lost} " +
                             $"store={r.Current}/{r.Max} overCap={r.OverCap}");
            }
            FlowTrace.Step("Bank",
                $"harvest-result modal OPEN with {results.Count} aggregated resource row(s): {trace}");
        }

        /// <summary>
        /// WO-1370 - the body copy, rebuilt so ONE resource reads as one paragraph.
        ///
        /// <para>THE DEFECT (owner, 2026-09-04, on a single-resource overflow): the old loop was
        /// written for a LIST and emitted three independent blocks separated by blank lines -
        /// "Stone" / "Collected: 0 of 90 | Uncollected: 90" / "Storage: 3000 / 3000..." / "Each
        /// uncollected amount was not added to storage." The resource WORD and its storage FIGURE
        /// sat in different blocks, so <c>3000 / 3000</c> had no visible owner and the owner could
        /// not tell what it referred to; and the trailing sentence said "Each" about a list of
        /// one.</para>
        ///
        /// <para>THE RULES THIS COPY HOLDS:
        /// (1) the resource NAME and ITS storage figure share the FIRST line, always;
        /// (2) the loss is named with the word "lost", not implied by a subtraction;
        /// (3) no redundant restatement after the list - each block is self-contained;
        /// (4) singular and plural both read correctly ("1 unit was" / "90 units were"), and the
        ///     sentence subject is never the resource noun itself, because "crystals was" and
        ///     "stone were" cannot both be right from one template;
        /// (5) ASCII ONLY - a non-ASCII dash or bullet renders as tofu on the device;
        /// (6) no meaning is carried by colour anywhere (the owner is red/green colourblind).</para>
        ///
        /// <para>The literals <c>Collected: {s.Granted} of {s.Requested}</c>,
        /// <c>Uncollected: {s.Lost}</c> and <c>was not added to storage</c> are PINNED by
        /// <c>TownBankCapRegression</c> [clamped-grant-warns] as the authoritative
        /// collected/uncollected truth - they are preserved verbatim, only re-seated.</para>
        /// </summary>
        public static string BuildBody(IReadOnlyList<BankOverflowStatus> results)
        {
            var blocks = new List<string>();
            for (int i = 0; i < results.Count; i++)
            {
                var s = results[i];
                string name = string.IsNullOrEmpty(s.ResourceName) ? "Resource" : s.ResourceName;
                string unit = name.ToLowerInvariant();

                // LINE 1 - the resource and ITS storage figure, on one line, in that order. This
                // is the whole fix: "3000 / 3000" can never again float free of the word it
                // describes. The state word is TEXT, never a tint.
                //
                // WO-1392 - the figure is the store AFTER this collect. BankOverflowStatus.Current
                // is the wallet BEFORE the grant was applied (that is what the clamp weighed), and
                // printing it raw put "Wood storage: 2021 / 4000" beside "414 lost" on the owner's
                // 2026-09-04 screen - 2021 + 414 < 4000, so the cap that bit looked like a lie.
                // The number the player can check against the rail is Current + Granted (= 4000).
                long after = (long)Mathf.Max(0, s.Current) + Mathf.Max(0, s.Granted);
                string state = s.OverCap ? " (over capacity)" : (after >= s.Max ? " (full)" : "");
                bool fromCollectors = string.Equals(s.Source, CollectorSource, System.StringComparison.Ordinal);
                string from = fromCollectors ? " from your collectors"
                    : (!string.IsNullOrEmpty(s.Source) && s.Source.IndexOf("DumpSilos", System.StringComparison.Ordinal) >= 0
                        ? " from the Echo silo" : "");
                var block = new List<string>
                {
                    $"{name} storage: {after} / {s.Max}{state}",
                    $"Collected: {s.Granted} of {s.Requested}{from}   |   Uncollected: {s.Lost}",
                };

                if (fromCollectors)
                {
                    // WO-1392 - NEVER BURN. A collector row's "uncollected" units are STILL IN THE
                    // COLLECTOR (ResourceCollector.Collect only drains what banked), so the copy
                    // says WAITING and names the real cap by its player word - the storage figure
                    // the rail shows - never "lost". Singular and plural authored separately.
                    block.Add(s.Lost == 1
                        ? $"That 1 {unit} is still waiting in your collectors - nothing was lost."
                        : $"Those {s.Lost} {unit} are still waiting in your collectors - nothing was lost.");
                    block.Add(s.OverCap
                        ? $"{name} storage {s.Max} is exceeded. It banks by itself once you spend below {s.Max} and collect again."
                        : $"{name} storage {s.Max} is full. Spend {unit}, or upgrade a {s.ContainerName}, then collect again.");
                }
                else
                {
                    // LINE 3 - the loss, said out loud. Two authored branches rather than one
                    // interpolated verb, so the singular reads "was" and the plural reads "were"
                    // without a template that can only be right for one of them.
                    block.Add(s.Lost == 1
                        ? $"That 1 {unit} was not added to storage - it is lost."
                        : $"Those {s.Lost} {unit} were not added to storage - they are lost.");

                    // LINE 4 - what to do about it. Over-cap is a DIFFERENT situation from a full
                    // bank (see BankOverflowStatus.OverCap) and keeps its own, non-punitive wording.
                    // WO-1392: the cap that bit is named by its player word ("Wood storage 4000").
                    block.Add(s.OverCap
                        ? $"Earned {unit} resumes once you spend below {s.Max}."
                        : $"{name} storage {s.Max} is full. Upgrade a {s.ContainerName}, or spend {unit}, before collecting again.");
                }

                blocks.Add(string.Join("\n", block));
            }
            // Blocks are separated by a blank line; there is NO trailing summary sentence. With
            // one resource a summary read as a stray fragment ("Each ..." about a list of one),
            // and with several it only repeated what every block already said.
            return string.Join("\n\n", blocks);
        }

        private void Update() { if (_hold != null) WorldHold.Renew(_hold); }

        private void Close()
        {
            if (_panel != null) PanelManager.NotifyClosed(_panel);
            _hold?.Dispose();
            _hold = null;
            if (_modal != null && _modal.canvas != null) Destroy(_modal.canvas);
            if (_active == this) _active = null;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            _hold?.Dispose();
            if (_active == this) _active = null;
        }
    }
}
