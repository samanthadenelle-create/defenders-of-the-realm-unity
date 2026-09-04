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

        public static void Present(IReadOnlyList<BankOverflowStatus> results)
        {
            if (!Application.isPlaying || results == null || results.Count == 0) return;
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
                string state = s.OverCap ? " (over capacity)" : (s.Current >= s.Max ? " (full)" : "");
                var block = new List<string>
                {
                    $"{name} storage: {s.Current} / {s.Max}{state}",
                    $"Collected: {s.Granted} of {s.Requested}   |   Uncollected: {s.Lost}",
                };

                // LINE 3 - the loss, said out loud. Two authored branches rather than one
                // interpolated verb, so the singular reads "was" and the plural reads "were"
                // without a template that can only be right for one of them.
                block.Add(s.Lost == 1
                    ? $"That 1 {unit} was not added to storage - it is lost."
                    : $"Those {s.Lost} {unit} were not added to storage - they are lost.");

                // LINE 4 - what to do about it. Over-cap is a DIFFERENT situation from a full
                // bank (see BankOverflowStatus.OverCap) and keeps its own, non-punitive wording.
                block.Add(s.OverCap
                    ? $"Earned {unit} resumes once you spend below {s.Max}."
                    : $"Upgrade a {s.ContainerName}, or spend {unit}, before collecting again.");

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
