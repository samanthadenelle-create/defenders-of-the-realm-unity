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
                ElarionUiKit.ModalArchetype.Compact, Close, sortingOrder: 31020);
            _panel = PanelManager.Register("Harvest Result", Close,
                () => _modal != null && _modal.canvas != null && _modal.canvas.activeInHierarchy);
            if (!PanelManager.NotifyOpened(_panel)) { Close(); return; }

            var content = _modal.chrome.content.transform;
            string body = BuildBody(results);
            var label = ElarionUiKit.Label(content, body, 0.22f, 0.88f, ElarionUi.Parchment,
                ElarionUi.FontBody, TextAlignmentOptions.TopLeft, 0.07f, 0.93f, bold: false);
            ElarionUiKit.FitBlock(label, ElarionUi.FontFloorMobile, ElarionUi.FontBody);
            FlowTrace.Step("Bank", $"harvest-result modal OPEN with {results.Count} aggregated resource row(s).");
        }

        public static string BuildBody(IReadOnlyList<BankOverflowStatus> results)
        {
            var lines = new List<string>();
            for (int i = 0; i < results.Count; i++)
            {
                var s = results[i];
                lines.Add($"{s.ResourceName}\nCollected: {s.Granted} of {s.Requested}\nUncollected: {s.Lost}");
                lines.Add(s.OverCap
                    ? $"Storage: {s.Current} / {s.Max}. You are already above capacity; earned {s.ResourceName.ToLowerInvariant()} resumes after you spend below {s.Max}."
                    : $"Storage capacity: {s.Current} / {s.Max}. Build or upgrade a {s.ContainerName}, or spend {s.ResourceName.ToLowerInvariant()}, before collecting again.");
            }
            lines.Add("The uncollected amount was not added to storage.");
            return string.Join("\n\n", lines);
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
