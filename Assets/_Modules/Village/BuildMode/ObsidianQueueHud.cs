// =============================================================================
// ObsidianQueueHud — the common work-queue panel (WO-773). DUMB SKIN.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// A code-built uGUI view (CLAUDE.md §8 — NO UXML) on the shared Obsidian chrome
// (ElarionUiKit), mirroring EchoWorkforceHud: a HIDDEN, button-opened modal. It
// shows each CHANNEL's active slots + its FIFO pending queue with per-job timers —
// Builders, Training, Research — so the player can see at a glance what's cooking
// and what's waiting. Channels are shown SEPARATELY (never one mixed global list),
// so it reads as CoC parallel workers, not an idle-game feed.
//
//   • The HUD button (VillageHudController, DeNelle.HUD) calls
//     ObsidianQueueGate.RequestToggle() (Core seam — HUD never references Village, §5).
//   • This view subscribes to ObsidianQueueGate.ToggleRequested + BuildTimerService.
//     QueueChanged and repaints (plus a 1s tick for the live countdowns).
//
// PLAYER-FACING NAMING: "Builders" / "Training" / "Research" — never "Obsidian".
// COLOURBLIND-SAFE: text + ASCII leading markers (">" running / "..." queued /
// "-" free) — no color-only state encoding, and no non-ASCII glyphs (LiberationSans
// SDF lacks the triangle/circle/ellipsis glyphs, which would render as tofu).
// =============================================================================

using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using DeNelle.Core.UI;
using DeNelle.Core.Jobs;
using DeNelle.Core.State;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// Tucked-away work-queue panel: per-channel slots + pending FIFO with live timers.
    /// Opened by the HUD via <see cref="ObsidianQueueGate"/>. Hidden by default — never
    /// persistent on-screen chrome. Self-installing (DDOL host, like BuildTimerService).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ObsidianQueueHud : MonoBehaviour
    {
        private const int LineCount = 16;   // fixed label pool (enough for 3 channels x slots+queue)

        private GameObject _modal;
        private readonly TextMeshProUGUI[] _lines = new TextMeshProUGUI[LineCount];
        private bool _open;
        private float _nextTick;
        private PanelHandle _panelHandle;

        // The three canonical channels + their player-facing labels.
        private static readonly (ChannelId id, string label)[] Channels =
        {
            (ChannelId.Builder,  "BUILDERS"),
            (ChannelId.Train,    "TRAINING"),
            (ChannelId.Research, "RESEARCH"),
        };

        // ── Self-install (mirrors BuildTimerService.Bootstrap) ────────────────
        private static ObsidianQueueHud _instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("ObsidianQueueHud");
            DontDestroyOnLoad(go);
            go.AddComponent<ObsidianQueueHud>();
        }

        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(this); return; }
            _instance = this;
        }

        private void Start()
        {
            Build();
            Hide();
            ObsidianQueueGate.ToggleRequested += Toggle;
            var svc = BuildTimerService.Instance;
            if (svc != null) svc.QueueChanged += Refresh;
            FlowTrace.Step("HUD", "ObsidianQueueHud built (hidden; opens via ObsidianQueueGate)");
        }

        private void OnDestroy()
        {
            ObsidianQueueGate.ToggleRequested -= Toggle;
            var svc = BuildTimerService.Instance;
            if (svc != null) svc.QueueChanged -= Refresh;
            if (_instance == this) _instance = null;
        }

        // Live countdown repaint while open.
        private void Update()
        {
            if (!_open) return;
            if (Time.unscaledTime < _nextTick) return;
            _nextTick = Time.unscaledTime + 1f;
            Refresh();
        }

        // ── open / close ──────────────────────────────────────────────────────
        private void Toggle() { if (_open) Hide(); else Show(); }

        private void Show()
        {
            if (_modal == null) return;
            _open = true;
            _modal.SetActive(true);
            if (_panelHandle == null)
                _panelHandle = PanelManager.Register("ObsidianQueue", Hide, () => _open);
            if (!PanelManager.NotifyOpened(_panelHandle))
            {
                FlowTrace.Warn("HUD", "ObsidianQueueHud open rejected by PanelManager (battle-lock).");
                return;
            }
            Refresh();
            FlowTrace.Step("HUD", "ObsidianQueueHud OPEN");
        }

        private void Hide()
        {
            if (_modal == null) return;
            _open = false;
            _modal.SetActive(false);
            if (_panelHandle != null) PanelManager.NotifyClosed(_panelHandle);
        }

        // ── build (shared Obsidian chrome) ────────────────────────────────────
        private void Build()
        {
            EnsureEventSystem();

            var built = ElarionUiKit.BuildObsidianModal(
                "WorkQueuePanel", "WORK QUEUE",
                new Vector2(0.28f, 0.20f), new Vector2(0.72f, 0.80f),
                onClose: Hide, sortingOrder: 31000,
                frameName: RpgUiCatalog.FrameCore);
            _modal = built.canvas;
            var content = built.chrome.content.transform;

            // Fixed label pool, stacked top→bottom; Refresh fills the text (unused = blank).
            for (int i = 0; i < LineCount; i++)
            {
                float yMax = 0.93f - i * 0.058f;
                float yMin = yMax - 0.052f;
                _lines[i] = ElarionUiKit.Label(content, "", yMin, yMax,
                    new Color(0.88f, 0.88f, 0.92f, 1f), ElarionUi.FontBody,
                    TextAlignmentOptions.Left, 0.06f, 0.94f, bold: false);
            }
        }

        // ── view refresh (service → view, one direction) ──────────────────────
        private void Refresh()
        {
            var svc = BuildTimerService.Instance;
            var texts = new List<string>(LineCount);

            if (svc == null)
            {
                texts.Add("Work queue unavailable.");
            }
            else
            {
                double now = TimeSource.NowUnixMs();
                foreach (var (id, label) in Channels)
                {
                    var active = svc.ActiveJobsOf(id);
                    var pending = svc.PendingJobsOf(id);
                    int slots = svc.SlotCount(id);
                    texts.Add($"{label}   {active.Count}/{slots} busy" +
                              (pending.Count > 0 ? $"   ({pending.Count} queued)" : ""));

                    // Active slots — running jobs (or an empty free slot). ASCII-only status
                    // markers (LiberationSans SDF has no ▶/○/… glyphs → they render as tofu):
                    // ">" = running, "-" = free slot, "..." = queued. Colourblind-safe via text.
                    for (int i = 0; i < slots; i++)
                    {
                        if (i < active.Count)
                            texts.Add("   > " + JobLine(active[i], now));   // > running
                        else
                            texts.Add("   - free");                          // - free slot
                    }
                    // Pending FIFO strip.
                    for (int i = 0; i < pending.Count; i++)
                        texts.Add("   ... " + KindLabel(pending[i].JobKind) + " (queued)"); // ... queued
                }
            }

            for (int i = 0; i < LineCount; i++)
                if (_lines[i] != null)
                    _lines[i].text = i < texts.Count ? texts[i] : "";
        }

        private static string JobLine(BuildJobData job, double now)
        {
            string kind = KindLabel(job.JobKind);
            if (job.StartMs <= 0) return kind + " (queued)";
            double remMs = job.FinishMs - now;
            if (remMs < 0) remMs = 0;
            return kind + "  " + FormatTime(remMs / 1000.0);
        }

        private static string FormatTime(double seconds)
        {
            if (seconds < 0) seconds = 0;
            int s = Mathf.RoundToInt((float)seconds);
            int h = s / 3600; s %= 3600;
            int m = s / 60; s %= 60;
            var sb = new StringBuilder();
            if (h > 0) sb.Append(h).Append("h ");
            if (h > 0 || m > 0) sb.Append(m).Append("m ");
            sb.Append(s).Append("s");
            return sb.ToString();
        }

        private static string KindLabel(JobKind kind)
        {
            switch (kind)
            {
                case JobKind.Build: return "Build";
                case JobKind.Upgrade: return "Upgrade";
                case JobKind.Repair: return "Repair";
                case JobKind.UnlockTier: return "Unlock tier";
                case JobKind.LearnMagic: return "Learn magic";
                case JobKind.TrainTroop: return "Train";
                case JobKind.TowerBuild: return "Tower";
                case JobKind.TowerUpgrade: return "Tower upgrade";
                case JobKind.WallUpgrade: return "Wall upgrade";
                default: return kind.ToString();
            }
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var es = new GameObject("EventSystem");
            es.AddComponent<EventSystem>();
            es.AddComponent<StandaloneInputModule>();
            DontDestroyOnLoad(es);
        }
    }
}
