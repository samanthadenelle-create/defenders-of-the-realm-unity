// =============================================================================
// ObsidianQueueHud — the common work-queue panel (WO-773 + WO-778). DUMB SKIN.
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
//   • The HUD button (HudKitController, DeNelle.HUD) calls
//     ObsidianQueueGate.RequestToggle() (Core seam — HUD never references Village, §5)
//     via OpenWorkQueue() (public static for regression reachability).
//   • This view subscribes to ObsidianQueueGate.ToggleRequested + BuildTimerService.
//     QueueChanged and repaints (plus a 1s tick for the live countdowns).
//
// WO-778: kind labels cover BarracksUpgrade/TroopUpgrade; job lines carry target
// identity (Footman ×1 / Barracks → L2 / Archer → L3); list parents to layout.body
// (NOT chrome.content) inside a MakeScrollZone so busy queues scroll instead of
// clipping; sell-time Instant / Ad-skip / Buy-slot buttons call the existing
// BuildTimerService APIs (no new economy logic).
//
// WO-864 (2026-08-03): the vertical ASCII job rows are GONE. Each channel now renders
// its own titled CoC-style CARD RAIL via the shared DeNelle.Core.UI.QueueRailView —
// the SAME component the always-on HUD Builders panel hosts, so the two surfaces can
// never show a different queue visual. This view owns only the chrome (header, +slot,
// the per-channel Instant/Ad action rows); the rail owns card anatomy, empty-slot
// cards, stack badges and its own cheap tick. The 1s Refresh() tick is REMOVED: it
// used to destroy and rebuild every row once a second (WO-836 cheap-tick lesson).
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
// Alias, not a plain using: this file lives in DeNelle.Village and C# does NOT search INNER
// namespaces, so BuildingPerkService (DeNelle.Village.Buildings.Progression) is unreachable
// unqualified. Mirrors BarracksService's `using Ledger = ...` convention.
using Perks = DeNelle.Village.Buildings.Progression;

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
        private const float LineHeightPx = 30f;
        private const float ActionRowHeightPx = 56f;
        // 80, not the old 34 and not the WO-864 66: the header label renders at
        // ElarionUi.FontBody (50), whose line box is ~60px, and AddStretchLabel seats it in
        // 0.05..0.95 of the row — i.e. 90% of this constant. 34px culled the descenders
        // outright (2026-08-03 capture). 66px gave the line 59.4px, a hair UNDER its own box,
        // so the descenders of "...busy" spilled below the row and the next row's opaque card
        // plate painted over them — the "descenders kiss the first card" defect in the
        // 2026-08-04 capture (docs/ui-review/screens-2026-08-04/QueueCardRail_2340x1080.png).
        // 80px seats the 60px box in 72px with ~6px of slack top and bottom, and the scroll
        // column's own 4px spacing then reads as a real gap above the cards.
        // (Side benefit: the +slot button sits at 0.10..0.90 of this row, so a taller row also
        // shrinks how far ClampMinTouch has to grow it toward MinTouchPx=112.)
        private const float HeaderHeightPx = 80f;

        private GameObject _modal;
        private RectTransform _listContent;   // MakeScrollZone content (layout.body host)
        private bool _open;
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

        // ─────────────────────────────────────────────────────────────────────
        //  ⚠ SUPERSEDED 2026-08-06 BY WO-911 — this modal NO LONGER SELF-INSTALLS.
        //  -------------------------------------------------------------------
        //  The unified Manage/Queues screen (ManageScreenPanel) is now the ONE
        //  queue surface and the ONE door: it subscribes to the same
        //  ObsidianQueueGate.ToggleRequested verb the re-pointed bar face raises.
        //  If BOTH panels installed, a single tap would open two stacked modals.
        //
        //  The CLASS is deliberately kept, not deleted:
        //   • ObsidianQueueRegression reflects on OpenWorkQueue() and on this
        //     file's shape (checks 7c / 9);
        //   • its public FormatKindLabel / FormatJobTarget / FormatJobLine
        //     helpers are the shared job-label vocabulary and are consumed by
        //     ManageScreenVM — the labels stay defined in exactly one place.
        //
        //  A developer can still raise it explicitly via OpenWorkQueue() after
        //  adding the component; nothing spawns it automatically any more.
        // ─────────────────────────────────────────────────────────────────────
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            FlowTrace.Step("HUD",
                "ObsidianQueueHud NOT installed — superseded by the WO-911 Manage/Queues screen " +
                "(ManageScreenPanel owns ObsidianQueueGate.ToggleRequested; two subscribers would " +
                "stack two modals on one tap).");
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

        // WO-864: NO 1s repaint here any more. The old tick called Refresh() every second,
        // which destroyed and rebuilt EVERY row — per-second layout churn for text that only
        // needed its digits changed (the WO-836 cheap-tick lesson). Countdowns now live on
        // QueueRailView cards, which update their own timer TEXT and rebuild only when the
        // queue SHAPE moves. This view repaints on QueueChanged + on open, and nothing else.

        /// <summary>
        /// Public static entry the HUD (or tests/regression) can call to open/close the
        /// work-queue panel. Delegates to the Core seam <see cref="ObsidianQueueGate.RequestToggle"/>.
        /// </summary>
        public static void OpenWorkQueue()
        {
            ObsidianQueueGate.RequestToggle();
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

            // WO-778: parent the list to layout.body (NOT chrome.content) so title/Close
            // never clip the job lines — mirrors MusicSelectionPanel / PackStore.
            Transform body = built.chrome.layout != null && built.chrome.layout.body != null
                ? built.chrome.layout.body.transform
                : built.chrome.content.transform;

            // Scrollable job list inside layout.body (busy 3-channel queues exceed fixed lines).
            var scroll = ElarionUiKit.MakeScrollZone(body, spacing: 4f, padding: 6);
            _listContent = scroll != null ? scroll.content : null;
            if (_listContent == null)
            {
                // Defensive: if MakeScrollZone ever fails, still parent into body.
                var fallback = new GameObject("QueueList", typeof(RectTransform));
                fallback.transform.SetParent(body, false);
                _listContent = (RectTransform)fallback.transform;
            }
        }

        // ── view refresh (service → view, one direction) ──────────────────────
        private void Refresh()
        {
            if (_listContent == null) return;

            // Clear previous rows (dynamic — no fixed 16-line pool).
            for (int i = _listContent.childCount - 1; i >= 0; i--)
                Destroy(_listContent.GetChild(i).gameObject);

            var svc = BuildTimerService.Instance;
            if (svc == null)
            {
                AddTextRow("Work queue unavailable.", LineHeightPx, new Color(0.88f, 0.88f, 0.92f, 1f), bold: false);
                return;
            }

            // WO-864: THREE separate, visually-distinct queues — each channel gets its OWN
            // titled rail (own header, own row of cards, own +slot), never one merged list.
            // The rail is the SHARED DeNelle.Core.UI.QueueRailView, the same component the
            // always-on HUD Builders panel hosts, so the two surfaces cannot disagree about
            // what the queue looks like. This view supplies the chrome; the rail owns the
            // cards, the empty-slot placeholders, the stack badges and its own cheap tick.
            var opts = QueueRailView.Options.Default;
            float railH = QueueRailView.HeightOf(opts);

            foreach (var (id, label) in Channels)
            {
                var active = svc.ActiveJobsOf(id);
                var pending = svc.PendingJobsOf(id);
                int slots = svc.SlotCount(id);

                // Channel header + Buy-slot CTA. NO TIMER here — the card owns the
                // countdown, and printing it in both places is exactly the double-timer
                // the owner reported on 2026-08-03.
                string header = $"{label}   {active.Count}/{slots} busy" +
                                (pending.Count > 0 ? $"   ({pending.Count} queued)" : "");
                AddChannelHeader(header, id);

                var railRow = MakeRowHost("Rail_" + id, railH);
                QueueRailView.Build((RectTransform)railRow.transform, id, opts);

                // Sell-time actions stay reachable as a per-channel action row (WO-864 §3):
                // one row per ACTIVE job that actually offers Instant or Ad. Cards are
                // raycast-off decoration, so the buttons remain the only tap targets and
                // nothing on the rail can swallow a tap.
                for (int i = 0; i < active.Count; i++)
                    AddJobActionRow(active[i], svc);
            }
        }

        // ── row builders (VerticalLayoutGroup children with fixed preferred height) ──

        private void AddChannelHeader(string text, ChannelId channel)
        {
            var row = MakeRowHost("ChHeader", HeaderHeightPx);
            var lbl = AddStretchLabel(row.transform, text, new Color(0.95f, 0.88f, 0.55f, 1f), bold: true,
                x0: 0.02f, x1: 0.72f);
            lbl.fontSize = ElarionUi.FontBody;

            // Buy-slot: BuildTimerService.BuySlot does not spend crystals (caller handles
            // premium). V1 wires the button + warns that economy hook is stub.
            ElarionUiKit.BuildObsidianButton(row.transform, "+slot",
                ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Gray,
                new Vector2(0.74f, 0.10f), new Vector2(0.98f, 0.90f),
                () => OnBuySlot(channel));
        }

        // One ACTIVE job's sell-time actions. Renders nothing at all when the job offers
        // neither (Train/Research jobs never resolve InstantFinish by structureId), so an
        // idle channel adds no empty rows.
        private void AddJobActionRow(BuildJobData job, BuildTimerService svc)
        {
            if (job.StartMs <= 0 || svc == null) return;

            // InstantFinish/AdSkip only resolve Builder jobs via structureId — price>0
            // gates the Instant button so Train/Research never show a false Instant CTA.
            int price = svc.InstantFinishPrice(job.StructureId);
            // RELEASE BLOCKER GATE (2026-08-07): no ad SDK exists, so the "Ad" button is ABSENT
            // (not greyed, not silently dead) until FeatureFlags.RewardedAdSkip's prerequisites
            // land — a real SDK plus WO-912 server-side ad-window validation. The row falls back
            // to Instant-only, or renders nothing at all when there is no Instant either.
            bool adOk = DeNelle.Core.FeatureFlags.RewardedAdSkip && svc.CanWatchAdToSkip(job.StructureId);
            if (price <= 0 && !adOk) return;

            var row = MakeRowHost("JobActions", ActionRowHeightPx);
            AddStretchLabel(row.transform, "   " + FormatJobTarget(job),
                new Color(0.88f, 0.88f, 0.92f, 1f), bold: false, x0: 0.02f, x1: 0.52f);

            string sid = job.StructureId;
            float x = 0.54f;
            if (price > 0)
            {
                float w = 0.22f;
                ElarionUiKit.BuildObsidianButton(row.transform, price + "c",
                    ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Yellow,
                    new Vector2(x, 0.12f), new Vector2(x + w, 0.88f),
                    () => OnInstantFinish(sid));
                x += w + 0.02f;
            }
            if (adOk)
            {
                float w = 0.18f;
                ElarionUiKit.BuildObsidianButton(row.transform, "Ad",
                    ElarionUiKit.ObsidianButtonStyle.Style1, ElarionUiKit.ObsidianButtonColor.Green,
                    new Vector2(x, 0.12f), new Vector2(Mathf.Min(0.98f, x + w), 0.88f),
                    () => OnAdSkip(sid));
            }
        }

        private void AddTextRow(string text, float height, Color color, bool bold)
        {
            var row = MakeRowHost("Line", height);
            AddStretchLabel(row.transform, text, color, bold, x0: 0.02f, x1: 0.98f);
        }

        private GameObject MakeRowHost(string name, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(LayoutElement));
            go.transform.SetParent(_listContent, false);
            var le = go.GetComponent<LayoutElement>();
            le.preferredHeight = height;
            le.minHeight = height;
            le.flexibleWidth = 1f;
            // MakeScrollZone's column runs childControlHeight=FALSE (kit rows carry their own
            // height), so the row's OWN rect is what positions it — set it explicitly or a
            // tall row (the card rail) collapses to zero and its cards render off-parent.
            var rt = (RectTransform)go.transform;
            rt.sizeDelta = new Vector2(rt.sizeDelta.x, height);
            return go;
        }

        private static TextMeshProUGUI AddStretchLabel(Transform parent, string text, Color color,
            bool bold, float x0, float x1)
        {
            var go = new GameObject("Txt", typeof(RectTransform), typeof(TextMeshProUGUI));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = new Vector2(x0, 0.05f);
            rt.anchorMax = new Vector2(x1, 0.95f);
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var tmp = go.GetComponent<TextMeshProUGUI>();
            ElarionUiKit.EnsureFont(tmp);
            tmp.text = text ?? "";
            tmp.fontSize = ElarionUi.FontBody;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.MidlineLeft;
            tmp.raycastTarget = false;
            tmp.enableWordWrapping = false;
            tmp.overflowMode = TextOverflowModes.Ellipsis;
            if (bold) tmp.fontStyle = FontStyles.Bold;
            return tmp;
        }

        // ── sell-time handlers (existing BuildTimerService APIs only) ─────────

        private static void OnBuySlot(ChannelId channel)
        {
            var svc = BuildTimerService.Instance;
            if (svc == null) return;
            // WO-911 (Q6 / B3): the free increment is GONE. TryBuySlot applies the owner's
            // TWO-STEP gate — the Echo count unlocks the RIGHT to buy, crystals complete it — and
            // reports a player-readable reason instead of silently granting a free worker.
            if (svc.TryBuySlot(channel, out string failure))
                ElarionUiKit.ShowToast("Extra " + channel + " slot unlocked.", ElarionUiKit.ToastTone.Confirm);
            else
                ElarionUiKit.ShowToast(failure ?? "Could not buy a slot.", ElarionUiKit.ToastTone.Danger);
        }

        private static void OnInstantFinish(string structureId)
        {
            var svc = BuildTimerService.Instance;
            if (svc == null || string.IsNullOrEmpty(structureId)) return;
            bool ok = svc.TryInstantFinish(structureId);
            if (ok)
                ElarionUiKit.ShowToast("Finished instantly.", ElarionUiKit.ToastTone.Confirm);
            else
                ElarionUiKit.ShowToast("Can't finish now (need crystals or job not active).",
                    ElarionUiKit.ToastTone.Danger);
            FlowTrace.Step("HUD", "ObsidianQueueHud TryInstantFinish '" + structureId + "' ok=" + ok);
        }

        private static void OnAdSkip(string structureId)
        {
            var svc = BuildTimerService.Instance;
            if (svc == null || string.IsNullOrEmpty(structureId)) return;
            // WO-1125: the ASYNC overload. The bool one reports "reward earned", which a real SDK
            // can never answer synchronously - the callback lands seconds after the return, so the
            // player would watch a full ad and be toasted "unavailable". The outcome now arrives
            // when the ad actually finishes.
            svc.WatchAdToSkip(DeNelle.Core.Jobs.ChannelId.Builder, structureId, result =>
            {
                if (result.Rewarded)
                {
                    ElarionUiKit.ShowToast("Time skipped.", ElarionUiKit.ToastTone.Info);
                }
                else if (result.Reason == DeNelle.Core.Ads.AdUnavailableReason.Abandoned)
                {
                    // Dismissed early is NOT an error and must not read as one - the player chose
                    // to stop watching, and telling them something broke is a lie.
                    ElarionUiKit.ShowToast("Ad closed early - no time skipped.", ElarionUiKit.ToastTone.Info);
                }
                else
                {
                    ElarionUiKit.ShowToast("Ad skip unavailable right now.", ElarionUiKit.ToastTone.Danger);
                }
                FlowTrace.Step("HUD",
                    "ObsidianQueueHud WatchAdToSkip '" + structureId + "' outcome=" + result);
            });
        }

        // ── public format helpers (regression + Refresh) ──────────────────────

        /// <summary>Player-facing kind label — never returns a raw enum for known kinds.</summary>
        public static string FormatKindLabel(JobKind kind) => KindLabel(kind);

        /// <summary>Target identity for a job (troop name, barracks tier, structure id short form).</summary>
        public static string FormatJobTarget(BuildJobData job) => JobTargetLabel(job);

        /// <summary>
        /// Full job line for an active or pending entry: kind+target + timer or "(queued)".
        /// e.g. "Footman ×1  1m 30s" / "Footman ×1 (queued)".
        /// </summary>
        public static string FormatJobLine(BuildJobData job, double nowUnixMs, bool queued)
        {
            string target = JobTargetLabel(job);
            if (string.IsNullOrEmpty(target))
                target = KindLabel(job.JobKind);

            if (queued || job.StartMs <= 0)
                return target + " (queued)";

            double remMs = job.FinishMs - nowUnixMs;
            if (remMs < 0) remMs = 0;
            return target + "  " + FormatTime(remMs / 1000.0);
        }

        private static string JobTargetLabel(BuildJobData job)
        {
            string id = job.StructureId ?? "";
            var kind = job.JobKind;

            // Train: barracks-train:<troopId>:<uid> → "Footman ×1"
            if (kind == JobKind.TrainTroop || id.StartsWith(BarracksService.TrainPrefix))
            {
                string troopId = TroopIdFromTrain(id);
                string name = TroopDisplayName(troopId);
                return name + " x1";
            }

            // Troop upgrade: barracks-troop-upgrade:<troopId> → "Archer → L{targetTier}"
            if (kind == JobKind.TroopUpgrade || id.StartsWith(BarracksService.TroopUpgradePrefix))
            {
                string troopId = TroopIdFromUpgrade(id);
                string name = TroopDisplayName(troopId);
                int tier = job.TargetTier > 0 ? job.TargetTier : 0;
                // ASCII "->" only — LiberationSans SDF lacks U+2192 (tofu oracle).
                return tier > 0 ? (name + " -> L" + tier) : (name + " upgrade");
            }

            // Building-perk research: building-research:<buildingId>:<perkId> -> "Arcane Basics"
            // (the perk's authored player-facing Name, falling back to a spaced id). Without this
            // the row read as the raw job id, which is the exact "player-facing leak" the WO-778
            // label oracle exists to stop.
            if (kind == JobKind.BuildingResearch || id.StartsWith(Perks.BuildingPerkService.ResearchJobPrefix))
            {
                // ASK THE SERVICE for the name; do NOT resolve the catalog row here. A View reading
                // BuildingTierCatalog directly is an MVVM conformance violation and the oracle
                // rightly failed the gate on it (UiMvvmConformance: "NEW View reading game state
                // without a ViewModel"). The service owns the lookup + the spaced-id fallback.
                return Perks.BuildingPerkService.DisplayNameForJob(id) ?? "Research";
            }

            // Barracks building upgrade.
            if (kind == JobKind.BarracksUpgrade || id == BarracksService.BarracksJobId)
            {
                int tier = job.TargetTier > 0 ? job.TargetTier : 0;
                return tier > 0 ? ("Barracks -> L" + tier) : "Barracks upgrade";
            }

            // Generic structure / tower / wall — short form of StructureId.
            string shortId = ShortStructureId(id);
            switch (kind)
            {
                case JobKind.Build:
                case JobKind.TowerBuild:
                    return shortId;
                case JobKind.Upgrade:
                case JobKind.TowerUpgrade:
                case JobKind.WallUpgrade:
                    return job.TargetTier > 0 ? (shortId + " -> L" + job.TargetTier) : shortId;
                case JobKind.Repair:
                    return "Repair " + shortId;
                case JobKind.UnlockTier:
                    return "Unlock " + shortId;
                case JobKind.LearnMagic:
                    return "Learn " + shortId;
                default:
                    return shortId;
            }
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
                case JobKind.BarracksUpgrade: return "Barracks upgrade";
                case JobKind.TroopUpgrade: return "Troop upgrade";
                case JobKind.BuildingResearch: return "Research";
                default: return kind.ToString();
            }
        }

        private static string TroopIdFromTrain(string jobId)
        {
            if (string.IsNullOrEmpty(jobId)) return jobId;
            // Format: barracks-train:<troopId>:<uid>. Troop ids carry hyphens, never colons.
            var parts = jobId.Split(':');
            return parts.Length >= 2 ? parts[1] : jobId;
        }

        private static string TroopIdFromUpgrade(string jobId)
        {
            if (string.IsNullOrEmpty(jobId) || !jobId.StartsWith(BarracksService.TroopUpgradePrefix))
                return jobId;
            return jobId.Substring(BarracksService.TroopUpgradePrefix.Length);
        }

        private static string TroopDisplayName(string troopId)
        {
            if (string.IsNullOrEmpty(troopId)) return "Troop";
            var def = TroopCatalog.Find(troopId);
            if (def != null && !string.IsNullOrEmpty(def.DisplayName)) return def.DisplayName;
            return SpacedName(troopId);
        }

        private static string ShortStructureId(string id)
        {
            if (string.IsNullOrEmpty(id)) return "Job";
            // Drop cell suffix like "@1_2" for a cleaner player line.
            int at = id.IndexOf('@');
            string core = at > 0 ? id.Substring(0, at) : id;
            return SpacedName(core);
        }

        private static string SpacedName(string id)
        {
            if (string.IsNullOrEmpty(id)) return "";
            var raw = id.Replace('-', ' ').Replace('_', ' ').Trim();
            if (raw.Length == 0) return "";
            var parts = raw.Split(' ');
            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i].Length == 0) continue;
                parts[i] = char.ToUpperInvariant(parts[i][0])
                           + (parts[i].Length > 1 ? parts[i].Substring(1) : "");
            }
            return string.Join(" ", parts);
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
