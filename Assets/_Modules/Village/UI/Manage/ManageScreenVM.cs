// =============================================================================
// ManageScreenVM — the pure ViewModel behind the unified MANAGE / QUEUES screen.
// -----------------------------------------------------------------------------
// WO-911 (absorbs WO-905).   Assembly: DeNelle.Village   Namespace: DeNelle.Village.UI
//
// WHAT THIS SCREEN IS (owner, 2026-08-05/06):
//   "move [the builders queue] to its own dedicated button at the bottom where we
//    can open up the queue and see the different types of queues ... Anything
//    that's applicable should be in a single screen"
//   "ability to see all the items in the queue and cancel the second thing and
//    refund the amount and bump up the next item ... max of five things"
//   Framing: "Think Warcraft-style parallel production lines."
//
// ⚠ THE STRUCTURAL FACT THIS MODEL IS BUILT AROUND (do not re-derive it):
//   The owner's CONTENT tabs CROSS the queue CHANNELS. There are only THREE
//   channels (Builder / Train / Research, JobKind.cs) but FOUR content tabs, and
//   two of them ride the SAME rail:
//
//     TAB          ->  CHANNEL           note
//     Defense      ->  Builder           towers / walls / gates
//     Buildings    ->  Builder           SHARES the Builders line with Defense
//     Troops       ->  Train             training + the WO-897 muster
//     Research     ->  Research          troop / tech upgrades
//
//   Defense and Buildings are one shared capacity. A player queuing a tower is
//   spending the same builder as a player queuing a farm. The tabs filter the
//   BROWSE list by content; they never imply two Builder pools.
//
//   Weapons / armour are deliberately ABSENT: GearProgression.Improve is instant
//   ("instant V1 — no job/channel") so gear has NO wall-clock cost and nothing to
//   put on a rail. WO-905 §7.3 resolved them as FUTURE, and Q3's ruled tab set
//   does not include them. Adding them would mean two of six tabs behaving unlike
//   the rest. The tab model takes them later without a rewrite.
//
// MVVM: no UnityEngine.UI here. The View reads these rows and calls these
// commands; every mutation goes through BuildTimerService / BarracksService, and
// this class never charges, grants or enqueues anything itself.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Catalog;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Jobs;
using DeNelle.Core.State;
using DeNelle.Core.UI;
using DeNelle.Wallet;     // WO-1282 - PackCatalog now ships in DeNelle.Commerce but KEEPS this
                          // namespace (PromoCodeService resolves it as a reflection string literal).
using DeNelle.Commerce;   // WO-1282 - StoreFocusRequest, the rail-neutral store focus latch.
using UnityEngine;
using CoreCost = DeNelle.Core.Catalog.ResourceCost;

namespace DeNelle.Village.UI
{
    /// <summary>The four CONTENT tabs. Ordinal is the tab-row order.</summary>
    public enum ManageTab
    {
        /// <summary>Towers, walls and gates. Rides the BUILDER line (shared with Buildings).</summary>
        Defense = 0,
        /// <summary>Economy / production buildings. Rides the BUILDER line (shared with Defense).</summary>
        Buildings = 1,
        /// <summary>Troop training + the army muster. Rides the TRAIN line.</summary>
        Troops = 2,
        /// <summary>Troop / tech upgrades and building research. Rides the RESEARCH line.</summary>
        Research = 3,
    }

    /// <summary>One line's at-a-glance state, for the always-visible three-channel strip.</summary>
    public struct ChannelSummary
    {
        /// <summary>Which production line.</summary>
        public ChannelId Channel;
        /// <summary>ASCII display word ("Builders" / "Training" / "Research").</summary>
        public string Name;
        /// <summary>Jobs running right now.</summary>
        public int Busy;
        /// <summary>Worker slots (concurrency).</summary>
        public int Slots;
        /// <summary>Items lined up (active + pending).</summary>
        public int Depth;
        /// <summary>Line-length cap (0 = uncapped).</summary>
        public int DepthCap;

        /// <summary>
        /// ASCII, colour-independent one-liner: "Builders 2/3 busy - 4/5 queued".
        /// State is TEXT because the owner is red/green colourblind.
        /// </summary>
        public string Describe()
            => DepthCap > 0
                ? $"{Name} {Busy}/{Slots} busy - {Depth}/{DepthCap} queued"
                : $"{Name} {Busy}/{Slots} busy - {Depth} queued";
    }

    /// <summary>
    /// One row in the "IN QUEUE" section. Either ONE addressable job, or a COLLAPSED stack of
    /// identical pending jobs (ruling Q12), which carries no destructive affordance at all.
    /// </summary>
    public sealed class QueueRowVM
    {
        /// <summary>Player-facing ASCII label ("Barracks -&gt; L2", "Footman").</summary>
        public string Label;
        /// <summary>State as TEXT, never colour ("Building 2m 10s", "Queued - 3rd in line").</summary>
        public string StateText;
        /// <summary>The line this job runs on.</summary>
        public ChannelId Channel;
        /// <summary>The engine key. Null ONLY on a collapsed stack header.</summary>
        public string JobId;
        /// <summary>True when the job has not started yet.</summary>
        public bool Queued;
        /// <summary>Position among pending jobs (0-based); -1 for an active job.</summary>
        public int PendingIndex;

        /// <summary>
        /// Q12 — a COLLAPSED stack header standing for <see cref="StackCount"/> identical pending
        /// jobs. It has NO JobId, so it can never be the target of a cancel or a paid finish.
        /// </summary>
        public bool IsStackHeader;
        /// <summary>How many identical jobs this header stands for (1 when not a stack).</summary>
        public int StackCount = 1;
        /// <summary>Grouping key for the stack (used to expand/collapse).</summary>
        public string StackKey;
        /// <summary>True when this header's stack is currently expanded below it.</summary>
        public bool Expanded;
        /// <summary>True when this row is one of an expanded stack's children (indented).</summary>
        public bool IsStackChild;

        /// <summary>Crystal price to Complete Now, or 0 when unavailable.</summary>
        public int FinishPrice;
        /// <summary>True when the player can afford <see cref="FinishPrice"/> right now.</summary>
        public bool CanAffordFinish;

        /// <summary>
        /// The Finish CTA's SECOND LINE, in ASCII words: the price with its currency SPELLED OUT,
        /// plus the shortfall when the player is short ("5 crystals" / "5 crystals - need 3 more").
        ///
        /// Owner felt-test 2026-08-08: "finish five c is a little vague ... it's really hard to tell
        /// if five c doesn't really say anything". "5c" assumed the player already knew that c meant
        /// crystals AND that the price scales with time remaining (cheap because the job is nearly
        /// done, not because finishing is cheap) - neither is knowable on day one. The old
        /// unaffordable face said "(short)", which silently meant "you cannot afford this" and read
        /// like part of the price.
        ///
        /// Composed HERE, not in the View: this is the same MVVM law the rest of the row follows
        /// (StateText / RefundText / CostText are all VM-composed ASCII), and the crystal balance
        /// needed for the shortfall is already in hand at the one place rows are built. The PRICE
        /// ITSELF IS UNTOUCHED - this is presentation only.
        /// </summary>
        public string FinishCostText;
        /// <summary>True when a rewarded-ad skip is offered (running jobs only).</summary>
        public bool AdAvailable;
        /// <summary>True when this row may be cancelled (never on a collapsed stack header).</summary>
        public bool CanCancel;
        /// <summary>True when this row may be moved one place up the pending FIFO.</summary>
        public bool CanBumpUp;
        /// <summary>What a cancel would hand back, as ASCII ("400 wood, 200 food" / "nothing").</summary>
        public string RefundText;

        /// <summary>
        /// WO-898 item 1 — how far along this job is, 0..1 (filled = elapsed). A RUNNING job
        /// reports real progress from StartMs/DurationMs; a QUEUED job is 0 by definition (it has
        /// not started, StartMs &lt;= 0) and a collapsed stack header is 0 because it stands for
        /// several jobs at different points.
        ///
        /// This is the half of WO-898 that drives the spend: "Complete now" already worked, but a
        /// bare countdown does not communicate "the wall is nearly up and the raid is inbound" the
        /// way a filling bar does. -1 means "do not draw a bar".
        /// </summary>
        public float Progress01 = -1f;

        // ── Row identity icon (owner: "should be a select icon") ──────────────
        // KEYS, not a Sprite: the VM must not touch UnityEngine.UI/art loading (the MVVM
        // conformance oracle fails a View/VM that reads game state or resolves assets itself).
        // The View hands these to QueueIconResolver - the SAME resolver the card rail uses, so
        // a job can never look like one thing in the rail and another here.
        /// <summary>RpgUiCatalog role, or empty to resolve art from <see cref="JobId"/>.</summary>
        public string IconRole;
        /// <summary>Sprite key within <see cref="IconRole"/>. Ignored when IconRole is empty.</summary>
        public string IconKey;
        /// <summary>ASCII uppercase verb (BUILD / UPGRADE / TRAIN / RESEARCH) - the icon's fallback.</summary>
        public string Verb;
        /// <summary>Target tier, part of the icon cache key.</summary>
        public int TargetTier;
    }

    /// <summary>One row in the "UPGRADES" browse section — the WO-905 affordability answer.</summary>
    public sealed class BrowseRowVM
    {
        /// <summary>Stable subject id for destination-specific grouping (for example a troop id).</summary>
        public string SubjectId;
        /// <summary>What it is, ASCII ("Arrow Tower -&gt; L3").</summary>
        public string Label;
        /// <summary>Its cost, ASCII ("400 wood, 200 food"), or "" when the cost lives in the panel.</summary>
        public string CostText;
        /// <summary>Affordability as TEXT, never colour ("Ready" / "Short 150 wood").</summary>
        public string StateText;
        /// <summary>True when the player can pay for it right now (drives the affordable-first sort).</summary>
        public bool Affordable;
        /// <summary>Sort weight within the affordable/unaffordable groups (cheapest first).</summary>
        public float CostWeight;
        /// <summary>Invoked on drill-in. Never null.</summary>
        public Action Activate;
        /// <summary>ASCII verb for the drill-in control ("Open" / "Upgrade").</summary>
        public string ActionText;
    }

    /// <summary>One authoritative troop selector entry for Manage → Troops.</summary>
    public sealed class TroopChoiceVM
    {
        public string Id;
        public string Name;
        public string Description;
        public string IconId;
        public int Level;
        public bool Unlocked;
        public string Requirement;
    }

    /// <summary>
    /// ViewModel for the unified Manage / Queues screen. Rebuilt on demand; holds no Unity objects.
    /// </summary>
    public sealed class ManageScreenVM
    {
        /// <summary>Raised whenever the rows change and the View must repaint.</summary>
        public event Action Changed;

        /// <summary>The selected content tab.</summary>
        public ManageTab Tab { get; private set; } = ManageTab.Buildings;

        /// <summary>All three lines' at-a-glance state — every channel stays visible on every tab.</summary>
        public readonly List<ChannelSummary> Channels = new List<ChannelSummary>(3);

        /// <summary>The selected tab's channel queue, in line order.</summary>
        public readonly List<QueueRowVM> QueueRows = new List<QueueRowVM>(16);

        /// <summary>The selected tab's upgrade browse list, affordable-first.</summary>
        public readonly List<BrowseRowVM> BrowseRows = new List<BrowseRowVM>(32);

        /// <summary>All authored troops, including locked entries, for explicit selector disclosure.</summary>
        public readonly List<TroopChoiceVM> TroopChoices = new List<TroopChoiceVM>(12);

        /// <summary>Categories earned by structures standing in the current town. Defense is the
        /// one intentional empty-state exception: it remains visible before the first placement
        /// so a fresh-town player can discover the defensive build route.</summary>
        public readonly List<ManageTab> VisibleTabs = new List<ManageTab>(4);

        /// <summary>Last command's player-facing message (ASCII), or null. The View toasts it.</summary>
        public string Notice { get; private set; }

        /// <summary>True when <see cref="Notice"/> is the broke case and the View should offer the store.</summary>
        public bool NoticeIsBrokeCase { get; private set; }

        /// <summary>WO-1253 Manage button copy. Measured: 11 chars, shorter than the old
        /// "Buy slot 250c" (14) that already fit the 0.33-width slot button.</summary>
        public const string BuyBuilderButtonCopy = "Buy builder";

        /// <summary>WO-1253 Manage label. 20 chars. Words carry the product (concurrency), not hue.</summary>
        public const string BuyBuilderLabelCopy = "Permanent builder +1";

        /// <summary>WO-1253 Manage label when the SKU is already owned. 20 chars.</summary>
        public const string BuyBuilderOwnedLabelCopy = "You own this builder";

        /// <summary>Retired crystal-price field. Always 0 after WO-1253: Manage no longer sells a crystal slot.</summary>
        public int SlotPrice { get; private set; }

        /// <summary>ASCII sentence describing the permanent-builder store offer.</summary>
        public string SlotOfferText { get; private set; } = "";

        /// <summary>WO-911 (Q2) — crystal-free instant repair cost, or null when nothing is damaged.</summary>
        public string RepairOfferText { get; private set; }

        private readonly HashSet<string> _expandedStacks = new HashSet<string>();

        /// <summary>
        /// Placed ids already reported by <see cref="WarnNoLadder"/>. STATIC on purpose: the warning
        /// is about the DATA (an unauthored ladder row), not about one screen instance, so opening
        /// Manage a second time must not re-print the same to-do list.
        /// </summary>
        private static readonly HashSet<string> _noLadderWarned =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // =====================================================================
        //  Tab -> channel. The ONE place the crossing is expressed.
        // =====================================================================

        /// <summary>
        /// The queue CHANNEL a content tab's work runs on. Defense and Buildings deliberately map to
        /// the SAME channel — they share one Builders line and one set of slots (WO-905 §2a).
        /// </summary>
        public static ChannelId ChannelOf(ManageTab tab)
        {
            switch (tab)
            {
                case ManageTab.Troops: return ChannelId.Train;
                case ManageTab.Research: return ChannelId.Research;
                default: return ChannelId.Builder;   // Defense AND Buildings
            }
        }

        /// <summary>ASCII tab labels, in tab order.</summary>
        public static readonly string[] TabLabels = { "Defense", "Buildings", "Troops", "Research" };

        /// <summary>Select a tab and rebuild.</summary>
        public void SelectTab(ManageTab tab)
        {
            if (Tab == tab) return;
            Tab = tab;
            FlowTrace.Step("Manage", $"tab -> {tab} (line {ChannelOf(tab)})");
            Rebuild();
        }

        /// <summary>Expand or collapse a Q12 stack of identical pending jobs.</summary>
        public void ToggleStack(string stackKey)
        {
            if (string.IsNullOrEmpty(stackKey)) return;
            if (!_expandedStacks.Remove(stackKey)) _expandedStacks.Add(stackKey);
            FlowTrace.Step("Manage",
                $"stack '{stackKey}' {( _expandedStacks.Contains(stackKey) ? "EXPANDED" : "collapsed")} " +
                "(Q12: cancel is only reachable on an expanded child).");
            Rebuild();
        }

        /// <summary>Clear the transient notice (after the View has shown it).</summary>
        public void ClearNotice() { Notice = null; NoticeIsBrokeCase = false; }

        // =====================================================================
        //  BUILD
        // =====================================================================

        /// <summary>Recompute every row from live state and raise <see cref="Changed"/>.</summary>
        public void Rebuild()
        {
            Guard.Try("Manage", "rebuild manage rows", () =>
            {
                Channels.Clear();
                QueueRows.Clear();
                BrowseRows.Clear();
                TroopChoices.Clear();

                BuildVisibleTabs();
                if (VisibleTabs.Count > 0 && !VisibleTabs.Contains(Tab))
                    Tab = VisibleTabs[0];

                BuildChannelSummaries();
                BuildQueueRows(ChannelOf(Tab));
                BuildSlotOffer(ChannelOf(Tab));
                BuildRepairOffer();
                BuildBrowseRows();
            });
            Changed?.Invoke();
        }

        private void BuildVisibleTabs()
        {
            VisibleTabs.Clear();
            var placed = CountPlacedThisTown();
            bool defense = false, buildings = false, troops = false, research = false;
            foreach (var kv in placed)
            {
                var tier = BuildingTierCatalog.Find(kv.Key);
                if (tier != null)
                {
                    buildings = true;
                    if (kv.Key.IndexOf("barracks", StringComparison.OrdinalIgnoreCase) >= 0)
                        troops = true;
                    if (HasAuthoredPerk(tier)) research = true;
                }
                if (HasLevelLadder(kv.Value)) defense = true;
            }
            // WO-1285: hiding Defense until after the first defense is placed makes its route
            // circular. Keep one actionable empty-state tab; its View CTA opens the Defense builder.
            VisibleTabs.Add(ManageTab.Defense);
            if (buildings) VisibleTabs.Add(ManageTab.Buildings);
            if (troops) VisibleTabs.Add(ManageTab.Troops);
            if (research) VisibleTabs.Add(ManageTab.Research);

            // NO SILENT DISCLOSURE (§12). This is the single decision that answers the recurring
            // felt-test "there is no way to get to the upgrade/defensive screen", and until now it
            // left NO trace at all — so the only way to tell a correctly-gated fresh save from a
            // genuinely orphaned door was to read the source. It is a Step, not a Warn: zero tabs
            // on an empty BaseLayout is the DESIGNED progressive-disclosure state (the player is
            // sent to the "Build new" route), and the tabs appear as soon as something is placed.
            // A DEFENSE tab specifically needs a placed id whose repo.maxLevel > 1 — baked scene
            // walls and towers are NOT BaseLayout records and therefore never raise it.
            FlowTrace.Step("Manage",
                "visible tabs: " + string.Join(", ", VisibleTabs) + " (from " + placed.Count +
                " placed type(s); defenseOwned=" + defense + " buildings=" + buildings +
                " troops=" + troops + " research=" + research + ").");
        }

        private static bool HasAuthoredPerk(BuildingUpgradeDef def)
        {
            if (def?.Tiers == null) return false;
            for (int i = 0; i < def.Tiers.Count; i++)
                if (def.Tiers[i]?.Perks != null && def.Tiers[i].Perks.Count > 0) return true;
            return false;
        }

        private void BuildChannelSummaries()
        {
            var svc = BuildTimerService.Instance;
            if (svc == null)
            {
                FlowTrace.Warn("Manage", "no BuildTimerService — the queue strip renders empty.");
                return;
            }
            AddSummary(svc, ChannelId.Builder);
            AddSummary(svc, ChannelId.Train);
            AddSummary(svc, ChannelId.Research);
        }

        private void AddSummary(BuildTimerService svc, ChannelId id)
        {
            Channels.Add(new ChannelSummary
            {
                Channel = id,
                Name = BuildTimerService.ChannelWord(id),
                Busy = svc.ActiveJobsOf(id).Count,
                Slots = svc.SlotCount(id),
                Depth = svc.QueueDepth(id),
                DepthCap = svc.QueueDepthLimit(id),
            });
        }

        // ── Queue rows ────────────────────────────────────────────────────────

        private void BuildQueueRows(ChannelId channel)
        {
            var svc = BuildTimerService.Instance;
            if (svc == null) return;

            int crystals = CrystalBalance();

            // ACTIVE jobs first, never collapsed — a running job is always individually addressable.
            var active = svc.ActiveJobsOf(channel);
            for (int i = 0; i < active.Count; i++)
                QueueRows.Add(MakeJobRow(svc, channel, active[i], queued: false, pendingIndex: -1,
                                         crystals: crystals, isChild: false));

            // PENDING jobs, with the Q12 collapse.
            //
            // Owner ruling Q12, verbatim: "can not cancel on a collapsed card, must expand then
            // select item to cancel and others automatically move up." So identical pending jobs
            // publish as ONE header with NO JobId and NO cancel/finish affordance; expanding it
            // reveals the REAL per-job ids (the collapse is a PRESENTATION concern — the engine
            // keys cancel by id, not index, so the underlying jobs were always addressable).
            // CONTENT TABS CROSS CHANNELS (canon: Defence and Buildings share the ONE Builder rail).
            // The Troops tab must therefore also show TROOP UPGRADES, which the engine runs on the
            // RESEARCH channel (BarracksService enqueues them there). Without this, tapping Upgrade
            // on a Troops row put the job on a tab the player was not looking at and Troops kept
            // saying "Nothing queued on this line" - it read as a dead button.
            if (Tab == ManageTab.Troops)
            {
                var xActive = svc.ActiveJobsOf(ChannelId.Research);
                for (int i = 0; i < xActive.Count; i++)
                    QueueRows.Add(MakeJobRow(svc, ChannelId.Research, xActive[i], queued: false,
                                             pendingIndex: -1, crystals: crystals, isChild: false));
            }

            var pending = svc.PendingJobsOf(channel);
            int idx = 0;
            while (idx < pending.Count)
            {
                string key = StackKeyOf(pending[idx]);
                int run = 1;
                while (idx + run < pending.Count && StackKeyOf(pending[idx + run]) == key) run++;

                if (run <= 1)
                {
                    QueueRows.Add(MakeJobRow(svc, channel, pending[idx], queued: true, pendingIndex: idx,
                                             crystals: crystals, isChild: false));
                    idx += 1;
                    continue;
                }

                bool expanded = _expandedStacks.Contains(key);
                QueueRows.Add(new QueueRowVM
                {
                    Label = ObsidianQueueHud.FormatJobTarget(pending[idx]) + " x" + run,
                    StateText = expanded ? "Queued - expanded, pick one to cancel"
                                         : "Queued x" + run + " - expand to cancel one",
                    Channel = channel,
                    JobId = null,                 // ⚠ Q12: an aggregate is never a cancel target
                    Queued = true,
                    PendingIndex = idx,
                    IsStackHeader = true,
                    StackCount = run,
                    StackKey = key,
                    Expanded = expanded,
                    FinishPrice = 0,              // ⚠ Q11/Q12: no paid verb on an aggregate either
                    CanCancel = false,
                    CanBumpUp = false,
                    RefundText = null,
                });

                if (expanded)
                    for (int k = 0; k < run; k++)
                        QueueRows.Add(MakeJobRow(svc, channel, pending[idx + k], queued: true,
                                                 pendingIndex: idx + k, crystals: crystals, isChild: true));
                idx += run;
            }
        }

        private QueueRowVM MakeJobRow(BuildTimerService svc, ChannelId channel, BuildJobData job,
                                      bool queued, int pendingIndex, int crystals, bool isChild)
        {
            // Icon keys come from the SERVICE's card shape - the same one the queue rail uses.
            var card = BuildTimerService.EntryFor(job);
            int price = svc.InstantFinishPrice(channel, job.StructureId);
            double rem = svc.RemainingSeconds(channel, job.StructureId);

            return new QueueRowVM
            {
                Label = ObsidianQueueHud.FormatJobTarget(job),
                // Colourblind law: the state is a SENTENCE, never a tint.
                // The percentage is stated IN WORDS beside the bar (colourblind law: the fill is
                // never the only signal). WO-898's monetization driver is the player SEEING how
                // close the wall is when a raid is inbound; a bare countdown does not carry that.
                StateText = queued
                    ? "Queued - " + Ordinal(pendingIndex + 1) + " in line (" + FormatTime(rem) + " of work)"
                    : "Building - " + FormatTime(rem) + " left" + PercentSuffix(svc, channel, job.StructureId),
                Channel = channel,
                JobId = job.StructureId,
                Queued = queued,
                PendingIndex = pendingIndex,
                IsStackChild = isChild,
                StackCount = 1,
                // Ruling Q5: a QUEUED job is Finish-Now-able and priced, exactly like a running one.
                // The button is offered even when unaffordable (owner: "always show Finish while a
                // job runs, plus a get-crystals route when broke") — never hidden on price.
                FinishPrice = price,
                CanAffordFinish = price > 0 && crystals >= price,
                FinishCostText = DescribeFinishCost(price, crystals),
                // RELEASE BLOCKER GATE (2026-08-07): no ad SDK is wired, so the ad affordance is
                // ABSENT on every row of every channel until FeatureFlags.RewardedAdSkip's two
                // prerequisites land (real SDK + WO-912 server-side ad-window validation). The
                // service refuses too; this keeps the VM honest so the view builds no dead control.
                AdAvailable = DeNelle.Core.FeatureFlags.RewardedAdSkip &&
                              svc.CanWatchAdToSkip(channel, job.StructureId),
                CanCancel = true,
                CanBumpUp = queued && pendingIndex > 0,
                RefundText = job.Paid.Describe(),
                Progress01 = ProgressOf(job, queued, rem),
                IconRole = card.IconRole,
                IconKey = card.IconKey,
                Verb = card.Verb,
                TargetTier = card.TargetTier,
            };
        }

        /// <summary>
        /// WO-898 item 1. Elapsed fraction 0..1 for a RUNNING job; 0 for a queued one (it has not
        /// started - StartMs &lt;= 0 by the engine's contract, and RemainingSeconds deliberately
        /// reports the FULL duration for such a job, so deriving progress from `rem` alone would
        /// wrongly read as 0% forever on a job that is genuinely half done).
        /// </summary>
        private static float ProgressOf(BuildJobData job, bool queued, double remainingSec)
        {
            if (queued || job.StartMs <= 0d) return 0f;
            if (job.DurationMs <= 0d) return -1f;   // unknown duration: draw no bar rather than a lie

            double totalSec = job.DurationMs / 1000d;
            double elapsed = totalSec - remainingSec;
            return Mathf.Clamp01((float)(elapsed / totalSec));
        }

        /// <summary>
        /// Grouping key for the Q12 collapse. Mirrors the publish-time rule: only TRAINING jobs
        /// stack (their ids are "train:&lt;troop&gt;:&lt;guid&gt;"), so two different buildings never
        /// merge into one card.
        /// </summary>
        private static string StackKeyOf(BuildJobData job)
        {
            string id = job.StructureId ?? "";
            if (job.JobKind != JobKind.TrainTroop) return id;
            var parts = id.Split(':');
            return parts.Length >= 2 ? parts[0] + ":" + parts[1] : id;
        }

        // ── Extra-slot offer + repair fold ───────────────────────────────────

        private void BuildSlotOffer(ChannelId channel)
        {
            // WO-1253: Manage sells a PERMANENT BUILDER in the store, not a crystal extra slot.
            // Crystal extra-queue DEPTH is KEEP BOTH and still lives on the upgrade-queue-full
            // surface and ObsidianQueueHud. Channel is the visible tab's line; the SKU is always
            // the Builder crew.
            _ = channel;
            SlotPrice = 0;
            var ownedIds = GameStateService.Instance != null
                ? GameStateService.Instance.State?.OwnedItemIds
                : null;
            bool owned = PackCatalog.OwnsPermanentBuilder(ownedIds);
            SlotOfferText = owned ? BuyBuilderOwnedLabelCopy : BuyBuilderLabelCopy;
        }

        private void BuildRepairOffer()
        {
            // Ruling Q2: repair stays the EXISTING instant crystal spend-and-heal. It is surfaced
            // here "if it fits" and is NEVER converted into a queued job. WallRepairController is
            // CALLED, never restructured.
            RepairOfferText = null;
            if (Tab != ManageTab.Defense) return;

            Guard.Try("Manage", "read repair offer", () =>
            {
                var repair = UnityEngine.Object.FindFirstObjectByType<WallRepairController>();
                if (repair == null)
                {
                    // NO SILENT FAILURE (CLAUDE.md section 12.2). Nothing in a non-wave scene
                    // installs a WallRepairController except HubRepairAffordance, so when that
                    // affordance does not install, THIS offer silently vanishes too and the
                    // player is left with no repair surface anywhere while fire still renders.
                    DeNelle.Core.Diagnostics.FlowTrace.Throttle("Manage", "repair-offer-no-controller", 5f,
                        "Manage repair offer SUPPRESSED - no WallRepairController in this scene. " +
                        "This is the second surface lost when HubRepairAffordance does not install.");
                    return;
                }
                var cost = repair.RepairAllCost();
                if (cost.wood <= 0 && cost.food <= 0 && cost.iron <= 0 && cost.crystals <= 0) return;
                RepairOfferText = "Repair all (instant): " + DescribeCost(cost);
            });
        }

        // ── Browse rows ──────────────────────────────────────────────────────

        private void BuildBrowseRows()
        {
            switch (Tab)
            {
                case ManageTab.Defense: BuildDefenseBrowse(); break;
                case ManageTab.Buildings: BuildBuildingsBrowse(); break;
                case ManageTab.Troops: BuildTroopsBrowse(); break;
                case ManageTab.Research: BuildResearchBrowse(); break;
            }

            // "Sorting is the feature" (WO-905 §3): affordable first, then cheapest first, so the
            // player sees what they can act on immediately without doing arithmetic.
            BrowseRows.Sort((a, b) =>
            {
                if (a.Affordable != b.Affordable) return a.Affordable ? -1 : 1;
                // Troops has two actions on the same unit. Training is the primary reason this
                // destination exists and must not be paged behind zero-cost upgrade rows. The
                // approved hierarchy leads with TRAIN, then exposes upgrade options; keep that
                // verb order while preserving affordable-first within each action family.
                if (Tab == ManageTab.Troops)
                {
                    int ap = TroopActionPriority(a.ActionText);
                    int bp = TroopActionPriority(b.ActionText);
                    if (ap != bp) return ap.CompareTo(bp);
                }
                return a.CostWeight.CompareTo(b.CostWeight);
            });
        }

        private static int TroopActionPriority(string action)
        {
            if (string.Equals(action, "Train", StringComparison.OrdinalIgnoreCase)) return 0;
            if (string.Equals(action, "Upgrade", StringComparison.OrdinalIgnoreCase)) return 1;
            return 2;
        }

        private void BuildDefenseBrowse()
        {
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            if (state == null || state.BaseLayout == null) return;

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 0; i < state.BaseLayout.Count; i++)
            {
                var placed = state.BaseLayout[i];
                if (string.IsNullOrEmpty(placed.itemId)) continue;

                var entry = CatalogRegistry.Get(placed.itemId);
                if (entry == null || entry.repo == null) continue;
                // The SHARED ceiling (clamped to RepoProps.MaxStructureLevel) — the same number
                // the upgrade page and BuildModeController use. Reading raw repo.maxLevel here
                // would offer a row for a rung the controller then refuses.
                int ceiling = Buildings.Progression.PlacedStructureUpgradeService.MaxLevelFor(entry);
                if (ceiling <= 1) continue;                              // nothing to upgrade to

                int level = Mathf.Max(1, placed.level);
                if (level >= ceiling) continue;                          // already maxed
                string dedupe = placed.itemId + "#" + level;
                if (!seen.Add(dedupe)) continue;                         // one row per id+level

                var cost = BuildModeController.UpgradeCostFor(entry, level);
                // THE JOB KEY, NOT THE BARE ID (defect fixed 2026-08-16). This CTA used to pass
                // placed.itemId, which UpgradeFamilyResolver classifies as None -> the panel's
                // BuildUnknown set MaxTier = 0 and rendered "has reached tier 0 of 0 - there is
                // nothing left to upgrade here" for a tower standing at level 1 of 3. Manage told
                // the player a tower was maxed. The '@' in the key is what makes the resolver
                // answer PlacedStructure and the page show the real ladder.
                //
                // ONE ROW PER id+level (the dedupe above) means this key names the FIRST placed
                // instance at that level — the row says "Stone Wall -> L2" and lands on a Stone
                // Wall that is at L1. Deliberate: keying rows per instance would emit one row per
                // wall segment. The trace names which instance was chosen.
                string jobKey = Buildings.Progression.PlacedUpgradeKey.Compose(
                    placed.itemId, placed.cellX, placed.cellZ);
                FlowTrace.Step("Manage", "defense row '" + placed.itemId + "' L" + level
                    + "/" + ceiling + " -> inline placed key '" + jobKey + "'");
                string location = "grid " + placed.cellX + ", " + placed.cellZ;
                AddBrowseRow(NameOf(entry, placed.itemId) + " - " + location + " - L" + level + " -> L" + (level + 1), cost, "Upgrade",
                             () => UpgradePlaced(jobKey));
            }
        }

        /// <summary>How many buildings of ONE ladder id stand in this town, and the placed catalog
        /// ids that folded into it (kept for the diagnostics — a warning must be able to name the
        /// id the player actually placed, not just the id its ladder is spelled with).</summary>
        private sealed class PlacedTally
        {
            /// <summary>Live BaseLayout instances resolving to this ladder id.</summary>
            public int Count;
            /// <summary>Distinct placed catalog ids that resolved here (e.g. "collector_farm").</summary>
            public readonly List<string> SourceIds = new List<string>();
        }

        /// <summary>
        /// The LIVE placements of THIS town, counted per UPGRADE-LADDER id.
        ///
        /// ⚠ KEYED THROUGH <see cref="CatalogRegistry.ResolveUpgradeId"/>, THE SHIPPED RESOLVER —
        /// not a mapping table written here (owner 2026-08-08 forbade INVENTING a translation layer
        /// that would drift; this is the opposite move, REUSE). A resource COLLECTOR is placed under
        /// its catalog id ("collector_farm") while its ladder is authored under the bare
        /// <c>repo.collectorBuildingId</c> ("farm") — the mapping is AUTHORED IN structures-catalog.json,
        /// not hardcoded, and this is the same resolver <c>BuildingUpgradeVM</c> (:139) and
        /// <c>BuildModeController.UpgradeSelected</c> (:2275) already call. Using anything else here
        /// would make Manage and the in-world upgrade panel disagree about the same building.
        ///
        /// It also settles the duplicate-row question: "lumbermill" (catalog "Sawmill") and
        /// "collector_lumbermill" BOTH resolve to ladder "lumbermill", and the tier is stored per
        /// LADDER id (GameState.BuildingTiers["lumbermill"]), so they are one upgradable kind and
        /// must fold into ONE row. Counting on the raw itemId would emit the same row twice.
        ///
        /// ⚠ READS <see cref="GameState.BaseLayout"/> ON PURPOSE — and must keep doing so (owner
        /// ruling 2026-08-08). BaseLayout is the only per-TOWN answer to "do I own one of these
        /// right now"; it drops the record when a building is sold or destroyed, which IS the
        /// owner's "if destroyed = 0" rule, already implemented. The two nearby sets are the wrong
        /// question and would BOTH be wrong in a second town, which is where this breaks visibly:
        ///
        ///   * <c>GameState.FreeBuildsUsed</c> (v32) — ACCOUNT-scoped and monotonic: it burns at
        ///     the committed placement and never resets. It answers "have you had your free one",
        ///     not "do you own one HERE". On a prefab/Default Town the buildings are already
        ///     placed, so it is largely spent and would hide things you own while offering things
        ///     you do not.
        ///   * <c>GameState.EverBuiltStructureIds</c> (v36) — MONOTONIC BY DESIGN: selling never
        ///     removes an id, because the WO-819 sell -> baked-twin-resurface contract depends on
        ///     that. A destroyed building would keep offering upgrades forever.
        ///
        /// Both are correct for their own jobs. Here, "their other town has everything, this town
        /// doesn't" is the deciding case: the player is looking at the town they are standing in.
        /// When multi-town lands and BaseLayout shards per base, this counting site is correct for
        /// free; anything reading the account-scoped sets would have to be unpicked.
        /// </summary>
        private static Dictionary<string, PlacedTally> CountPlacedThisTown()
        {
            var counts = new Dictionary<string, PlacedTally>(StringComparer.OrdinalIgnoreCase);
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            if (state == null || state.BaseLayout == null) return counts;

            for (int i = 0; i < state.BaseLayout.Count; i++)
            {
                string placedId = state.BaseLayout[i].itemId;
                if (string.IsNullOrEmpty(placedId)) continue;

                // Pass-through for every non-collector id (and for any id the registry has not
                // loaded), so this is a no-op everywhere except the three collectors.
                string ladderId = CatalogRegistry.ResolveUpgradeId(placedId);
                if (string.IsNullOrEmpty(ladderId)) ladderId = placedId;

                if (!counts.TryGetValue(ladderId, out var tally))
                {
                    tally = new PlacedTally();
                    counts[ladderId] = tally;
                }
                tally.Count++;
                if (!tally.SourceIds.Contains(placedId)) tally.SourceIds.Add(placedId);
            }
            return counts;
        }

        /// <summary>
        /// The BUILDINGS tab — every building STANDING IN THIS TOWN that has a next tier authored,
        /// offered at that tier's real price.
        ///
        /// ⚠ TWO DIFFERENT QUESTIONS, DELIBERATELY KEPT APART (owner ruling 2026-08-08, felt-test
        /// "no building upgrades are on the manage button anywhere"):
        ///     WHETHER a row appears -> do you OWN one right now?  -> COUNT the placements.
        ///     WHICH   row appears   -> what tier are you on?      -> ModifierService.TierOf.
        /// Conflating them is the defect this replaces. The old code asked TierOf for BOTH and
        /// skipped on <c>tier &lt; 1</c> under the comment "not built / locked" — but TierOf reads
        /// GameState.BuildingTiers, which only ever contains ids that have been UPGRADED, so the
        /// filter really asked "have you already upgraded this?" and the tab was EMPTY for exactly
        /// the player the browser exists for: the one who has never bought a tier.
        ///
        /// ⛔ DO NOT "fix" a future variant of this by writing tier=1 at placement. Tier 1 is a PAID
        /// upgrade (barracks T1 = 900 wood / 750 food / 150 crystals) and it grants
        /// <c>structureHpBonusPct 0.20</c> through ModifierService.StructureHpMultFor (which returns
        /// 1f below tier 1 and 1.2 at tier 1), so seeding it would gift every newly placed building
        /// a free upgrade. The ladder is 1-based for UPGRADES, not for existence: tier 0 = placed.
        ///
        /// The lookup is a STRAIGHT read of building-tiers.json under the id the SHIPPED resolver
        /// gives (<see cref="CountPlacedThisTown"/> keys on <c>CatalogRegistry.ResolveUpgradeId</c>).
        /// No mapping table is written here — the owner's 2026-08-08 objection was to INVENTING a
        /// translation layer that would drift, and inventing a second resolver beside the game's own
        /// is exactly that; reusing hers is not. A resolved id with nothing authored under it is a
        /// CONTENT GAP, and <see cref="WarnNoLadder"/> announces it as the to-do list.
        /// </summary>
        private void BuildBuildingsBrowse()
        {
            var placed = CountPlacedThisTown();
            if (placed.Count == 0)
            {
                // NO SILENT EMPTY LIST (§12): an empty tab must be diagnosable from a log line
                // rather than a felt-test. This is the "nothing placed / no town state" case.
                FlowTrace.Step("Manage", "buildings browse (this town): 0 placements in BaseLayout -> no rows.");
                return;
            }

            int rows = 0, maxed = 0, noLadder = 0, onDefenseTab = 0;
            foreach (var kv in placed)
            {
                string ladderId = kv.Key;                                // already resolved
                var tally = kv.Value;

                // WHICH row: the tier ABOVE the one you own. A placed, never-upgraded building is
                // tier 0, so it offers tier 1 at tier 1's real price — nothing is granted here.
                var next = BuildingTierCatalog.TierOf(ladderId, ModifierService.TierOf(ladderId) + 1);
                if (next == null)
                {
                    if (BuildingTierCatalog.IsUpgradable(ladderId)) { maxed++; continue; }   // topped out

                    // Not a gap: this id runs the OTHER ladder. Towers/walls/mines/stockpiles carry
                    // a per-instance repo.maxLevel and are already browsed by BuildDefenseBrowse, so
                    // naming them in the "author some rows" warning would make that to-do list lie.
                    if (HasLevelLadder(tally)) { onDefenseTab++; continue; }

                    noLadder++;
                    WarnNoLadder(ladderId, tally);
                    continue;
                }

                var def = BuildingTierCatalog.Find(ladderId);
                string name = (def != null && !string.IsNullOrEmpty(def.DisplayName)) ? def.DisplayName : ladderId;
                var cost = BuildingTierBasket(next);
                // Upgrade against the ladder id so the inline row uses the same authoritative
                // progression identity as the detailed building-management view.
                string rowId = ladderId;                                 // captured by the CTA closure
                int targetTier = next.Tier;
                AddGoldBrowseRow(Ascii(name) + " -> T" + targetTier, cost, next.CostGold, "Upgrade",
                    () => UpgradeBuilding(rowId, targetTier));
                rows++;
            }

            FlowTrace.Step("Manage",
                "buildings browse (this town): " + placed.Count + " placed type(s) -> " + rows +
                " upgrade row(s); " + maxed + " at max tier, " + onDefenseTab +
                " on the level ladder (Defense tab), " + noLadder + " with no authored ladder.");
        }

        /// <summary>
        /// True when ANY of the placed ids behind this tally carries a per-instance level ladder
        /// (<c>repo.maxLevel &gt; 1</c>) — i.e. it upgrades through <see cref="BuildDefenseBrowse"/>
        /// on the Defense tab. Such an id is NOT missing an upgrade path, so it must never land on
        /// the "author some rows" to-do list.
        /// </summary>
        private static bool HasLevelLadder(PlacedTally tally)
        {
            if (tally == null) return false;
            for (int i = 0; i < tally.SourceIds.Count; i++)
            {
                var entry = CatalogRegistry.Get(tally.SourceIds[i]);
                if (entry != null && entry.repo != null && entry.repo.maxLevel > 1) return true;
            }
            return false;
        }

        /// <summary>
        /// LOUD, ONCE PER LADDER ID PER SESSION: a building is standing in this town, its id has
        /// already been through the shipped resolver, and building-tiers.json STILL has nothing
        /// authored under the result — so Manage can offer it nothing.
        ///
        /// This is the TO-DO LIST of buildings that still need upgrade rows authored. Do not downgrade
        /// it to a Step: a silently skipped id is precisely how the empty-tab defect hid for so long.
        ///
        /// Because the id is resolved BEFORE this point, the collector case ("collector_farm" -> "farm")
        /// no longer reaches here at all. If one ever does, the message says so explicitly and that is
        /// a REAL SECOND DEFECT to chase, not noise: it means <c>repo.collectorBuildingId</c> points at
        /// a ladder that does not exist, so the in-world upgrade panel is equally dead for that
        /// building. Never fix that by authoring rows under the PLACED id — tiers persist under the
        /// RESOLVED id (GameState.BuildingTiers), so the copy would be a ghost that never advances.
        /// </summary>
        private static void WarnNoLadder(string ladderId, PlacedTally tally)
        {
            // Deduped HERE rather than through FlowTrace.Once because Once logs at INFO and this
            // must stay at WARNING level to survive an F8 harvest. Rebuild() runs on every tab
            // change / economy tick, so an undeduped Warn would bury the rest of the capture.
            if (!_noLadderWarned.Add(ladderId)) return;

            int count = tally != null ? tally.Count : 0;
            string sources = (tally != null) ? string.Join(", ", tally.SourceIds.ToArray()) : "";
            bool resolvedAway = !string.IsNullOrEmpty(sources) &&
                                !string.Equals(sources, ladderId, StringComparison.OrdinalIgnoreCase);

            FlowTrace.Warn("Manage",
                "no upgrade ladder authored for '" + ladderId + "' (x" + count + " in this town, placed as: " +
                sources + ") - the Buildings tab can offer it nothing. " +
                (resolvedAway
                    ? "SECOND DEFECT: that id came from repo.collectorBuildingId, so the resolver points at " +
                      "a ladder that does not exist and the in-world upgrade panel is dead for it too. " +
                      "Fix the pointer or author '" + ladderId + "' - never author rows under the placed id, " +
                      "because tiers persist under the resolved one."
                    : "CONTENT GAP: author tier rows for '" + ladderId + "' in building-tiers.json."));
        }

        /// <summary>
        /// The Troops tab's browse list — TRAIN rows, UPGRADE rows and the ARMIES/muster entry.
        ///
        /// ⚠ WHY THE TRAIN ROWS EXIST (PROD-013, 2026-08-20 — do not remove them). This method used
        /// to emit UPGRADE rows ONLY. PROD-002 (commit 233613615, 2026-08-18) closed the barracks
        /// talk-door on the stated premise that "Manage owns training" — but that premise was FALSE
        /// when it was written: nothing on this tab could ever start a training job, so closing the
        /// door left the player with an Upgrade-only Troops tab and NO way to train at all. The
        /// owner reported exactly that ("under manage i see option to upgrade the troops, but i
        /// dont se a way to train troops"). This method is what makes PROD-002's premise true.
        ///
        /// TRAIN and UPGRADE are DIFFERENT ACTIONS ON THE SAME TROOP and both belong here — the
        /// labels are prefixed with the verb ("Train Footman" / "Upgrade Footman -&gt; L2") so the
        /// two can never be mistaken for each other. Everything still routes through
        /// BarracksService; this screen charges and enqueues nothing itself.
        /// </summary>
        private void BuildTroopsBrowse()
        {
            var all = TroopCatalog.All;
            if (all == null)
            {
                // NO SILENT EMPTY LIST (§12): an empty Troops tab is read off a log, never guessed.
                FlowTrace.Warn("Manage", "troops browse: TroopCatalog.All is null - the tab can offer nothing.");
                return;
            }

            int trainRows = 0, upgradeRows = 0, locked = 0;

            for (int i = 0; i < all.Count; i++)
            {
                var def = all[i];
                // Guard.TryEach semantics by hand: ONE malformed TroopDef logs and is SKIPPED
                // rather than throwing out of the loop and blanking the whole tab (§12 step 2).
                Guard.Try("Manage", "troops browse row " + i, () =>
                {
                    if (def == null || string.IsNullOrEmpty(def.Id)) return;

                    // Two authorities, both real: BarracksService.IsTroopUnlocked is the gate
                    // EnqueueTraining itself enforces (barracks.json unlocksTroopIds); TroopUnlock
                    // .IsTrainable is the WO-733 tier authority every other train path asks. A row
                    // is offered only when BOTH say yes, so this tab can never show a CTA the
                    // service will refuse. They are filters, not a defect.
                    bool unlocked = BarracksService.IsTroopUnlocked(def.Id);
                    bool trainable = TroopUnlock.IsTrainable(def);
                    string id = def.Id;
                    string name = NameOfTroop(def);
                    int level = BarracksService.TroopLevel(id);
                    TroopChoices.Add(new TroopChoiceVM
                    {
                        Id = id,
                        Name = name,
                        Description = Ascii(def.ShortDescription ?? ""),
                        IconId = def.IconId,
                        Level = Mathf.Max(1, level),
                        Unlocked = unlocked && trainable,
                        Requirement = unlocked && trainable
                            ? "Available"
                            : "Requires Barracks Tier " + Mathf.Max(1, def.UnlockBarracksTier),
                    });
                    if (!unlocked || !trainable)
                    {
                        locked++;
                        return;
                    }

                    // ── TRAIN ──────────────────────────────────────────────────
                    // Cost is the authored per-unit build cost (TroopDef.costWood/Food/Iron) —
                    // the SAME numbers BarracksService.EnqueueTraining charges. No balance is
                    // decided here; this only displays and routes.
                    AddGoldBrowseRow("Train " + name, default, def.CostGold, "Train", () => TrainTroop(id));
                    BrowseRows[BrowseRows.Count - 1].SubjectId = id;
                    trainRows++;

                    // ── UPGRADE (unchanged path) ───────────────────────────────
                    if (!BarracksProgression.HasNextTroopLevel(id, level)) return;

                    var econCost = BarracksProgression.TroopUpgradeCost(id, level + 1);
                    var cost = new CoreCost
                    {
                        wood = econCost.Wood,
                        food = econCost.Food,
                        iron = econCost.Iron,
                        crystals = econCost.Crystals,
                    };
                    AddBrowseRow("Upgrade " + name + " -> L" + (level + 1), cost, "Upgrade",
                                 () => UpgradeTroop(id));
                    BrowseRows[BrowseRows.Count - 1].SubjectId = id;
                    upgradeRows++;
                });
            }

            AddMusterRow();

            FlowTrace.Step("Manage",
                "troops browse: " + all.Count + " troop def(s) -> " + trainRows + " Train row(s), " +
                upgradeRows + " Upgrade row(s), " + locked + " still locked, + 1 Armies/muster entry.");

            if (trainRows == 0)
                FlowTrace.Warn("Manage",
                    "troops browse produced NO Train row - every troop is locked or the catalog is empty. " +
                    "This is the PROD-013 defect shape: the Troops tab is the ONLY door to training.");
        }

        /// <summary>
        /// WO-897 army muster / loadout bank entry (save schema v38, 3 named composition slots).
        /// It ships and, until PROD-013, had no player-reachable door either — the barracks Yarn
        /// verb &lt;&lt;ShowMusterUI&gt;&gt; was its only caller and that door is closed. Free, so the
        /// affordable-first sort floats it to the top of the tab where an entry point belongs.
        /// </summary>
        private void AddMusterRow()
        {
            BrowseRows.Add(new BrowseRowVM
            {
                Label = "Armies - saved compositions",
                CostText = "",
                StateText = "Muster a saved army onto the Training line",
                Affordable = true,
                CostWeight = 0f,
                ActionText = "Open",
                Activate = OpenMuster,
            });
        }

        /// <summary>Opens the WO-897 Armies/muster panel. The panel owns its own locked refusal.</summary>
        private static void OpenMuster()
        {
            FlowTrace.Step("Manage", "Armies CTA - opening the muster panel.");
            TroopDialogueCommands.ShowMusterUI();
        }

        private void BuildResearchBrowse()
        {
            // ⚠ REWRITTEN 2026-08-07 (owner ruling: building-perk research is now TIME-BASED, like
            // Warcraft 3). The old version emitted ONE row per BUILDING with CostText="",
            // Affordable=false and StateText="Open to see costs", pinned to CostWeight=MaxValue so
            // it always sorted last. That was correct while research was an instant purchase this
            // screen had no business pricing — but it made the Research tab the one tab that could
            // never answer the question the whole screen exists to answer ("can I act on this
            // now?"), and it could never produce a Research QUEUE row because "Open" only drilled
            // into another panel.
            //
            // Now a perk is a real priced+timed action, so this browses PER PERK and states the
            // real numbers: the authored goldCost (perks are the ONLY gold-priced work in the
            // game — the other three tabs are wood/food/iron/crystals, which is why this method
            // cannot reuse AddBrowseRow / CoreCost, whose struct has no coins field), the derived
            // duration, and a CTA that calls the same BuildingPerkService.TryResearch the panel
            // calls. This screen still charges NOTHING itself.
            var all = BuildingTierCatalog.All;
            if (all == null) return;

            int gold = GoldBalance();

            // SAME DEFECT AS THE BUILDINGS TAB, SAME FIX (2026-08-08). This gate used to be
            // `ModifierService.TierOf(def.Id) < 1  // not built`, which is NOT what TierOf answers:
            // BuildingTiers only holds ids that have been UPGRADED, so a player who owned a barracks
            // but had never bought a tier saw an empty Research tab too. Ownership is the LIVE
            // per-town placement count (CountPlacedThisTown); the tier gate is BuildingPerkService's
            // job and it already states the requirement in words ("Upgrade the building to Tier N
            // first"), which is the sentence that teaches the loop instead of hiding it.
            //
            // CountPlacedThisTown is keyed on the RESOLVED ladder id, which is the same id space
            // building-tiers.json uses — so this ContainsKey compares like with like, and a placed
            // collector ("collector_lumbermill" -> "lumbermill") correctly unlocks its perks.
            var placedThisTown = CountPlacedThisTown();
            int owned = 0;
            int before = BrowseRows.Count;

            for (int i = 0; i < all.Count; i++)
            {
                var def = all[i];
                if (def == null || string.IsNullOrEmpty(def.Id) || def.Tiers == null) continue;
                if (!placedThisTown.ContainsKey(def.Id)) continue;       // you do not own one HERE

                owned++;
                string buildingName = Ascii(string.IsNullOrEmpty(def.DisplayName) ? def.Id : def.DisplayName);

                for (int t = 0; t < def.Tiers.Count; t++)
                {
                    var tierDef = def.Tiers[t];
                    if (tierDef?.Perks == null) continue;

                    for (int p = 0; p < tierDef.Perks.Count; p++)
                    {
                        var perk = tierDef.Perks[p];
                        if (perk == null || string.IsNullOrEmpty(perk.Id)) continue;

                        // Captured by the CTA closure — never the loop variables.
                        string bId = def.Id;
                        string pId = perk.Id;
                        if (Buildings.Progression.BuildingPerkService.IsOwned(bId, pId)) continue;

                        bool can = Buildings.Progression.BuildingPerkService.CanResearch(bId, pId, out _);
                        // Progressive disclosure: prerequisites teach themselves when satisfied;
                        // a locked perk is not a manageable structure action yet.
                        if (!can) continue;
                        int price = Mathf.Max(0, perk.GoldCost);
                        bool affordable = can && gold >= price;
                        float seconds = Buildings.Progression.BuildingPerkService.ResearchSeconds(bId, pId);

                        // Colourblind law: the state is a SENTENCE. "Ready" now also carries the
                        // WAIT, because with a timed research the price is no longer the only cost.
                        string state;
                        if (!affordable) state = "Short " + (price - gold) + " gold";
                        else state = "Ready - takes " + FormatTime(seconds);

                        BrowseRows.Add(new BrowseRowVM
                        {
                            Label = buildingName + " - " +
                                    Ascii(string.IsNullOrEmpty(perk.Name) ? pId : perk.Name),
                            CostText = price > 0 ? price + " gold" : "free",
                            StateText = state,
                            Affordable = affordable,
                            // Every row on THIS tab is priced in gold, so a raw gold weight sorts
                            // cheapest-first consistently. It is never compared against the other
                            // tabs' CostBasket weight — BrowseRows is rebuilt per tab.
                            CostWeight = price,
                            ActionText = "Research",
                            Activate = () => Research(bId, pId),
                        });
                    }
                }
            }

            // NO SILENT EMPTY LIST (§12): say how many ladder buildings this town actually owns and
            // how many perk rows that produced, so an empty Research tab is read off a log instead
            // of a felt-test. owned==0 with placements present means no PLACED id matches a
            // building-tiers.json id — see the [Flow:Manage] "no upgrade ladder authored" warnings.
            FlowTrace.Step("Manage",
                "research browse (this town): " + placedThisTown.Count + " placed type(s), " + owned +
                " with a tier ladder -> " + (BrowseRows.Count - before) + " perk row(s).");
        }

        private void AddBrowseRow(string label, CoreCost cost, string actionText, Action activate)
        {
            bool affordable = CanAfford(cost);
            BrowseRows.Add(new BrowseRowVM
            {
                Label = label,
                CostText = DescribeCost(cost),
                // The point of the whole screen: whether it is buyable, and if not WHAT is short.
                // Reuses the resolver that actually charges so the screen cannot lie (WO-905 §4).
                StateText = affordable ? "Ready" : ShortfallOf(cost),
                Affordable = affordable,
                CostWeight = BuildTimerConfig.CostBasket(cost),
                ActionText = actionText,
                Activate = activate ?? (() => { }),
            });
        }

        // =====================================================================
        //  COMMANDS — every one acts on the EXPLICIT item the player picked
        // =====================================================================

        /// <summary>
        /// Ruling Q5 + Q11 — pay crystals to complete THIS ONE job (running or queued). Never a
        /// game-wide pass. On the broke case the notice is flagged so the View routes to the store.
        /// </summary>
        public void FinishNow(ChannelId channel, string jobId)
        {
            if (string.IsNullOrEmpty(jobId))
            {
                // Q12 defence in depth: a stack header carries no JobId and must never get here.
                FlowTrace.Warn("Manage", "FinishNow called with no job id — an aggregate is not a target.");
                return;
            }
            var svc = BuildTimerService.Instance;
            if (svc == null) return;

            if (svc.TryInstantFinish(channel, jobId, out string failure))
            {
                Notice = "Finished.";
                NoticeIsBrokeCase = false;
            }
            else
            {
                Notice = failure ?? "Could not finish that.";
                NoticeIsBrokeCase = failure != null &&
                                    failure.StartsWith(BuildTimerService.InsufficientCrystalsPrefix, StringComparison.Ordinal);
            }
            Rebuild();
        }

        /// <summary>Watch the rewarded ad to knock time off THIS running job.</summary>
        public void WatchAd(ChannelId channel, string jobId)
        {
            if (string.IsNullOrEmpty(jobId)) return;
            var svc = BuildTimerService.Instance;
            if (svc == null) return;
            // RELEASE BLOCKER GATE (2026-08-07): the flag is OFF and no ad SDK is wired, so this
            // entry point can only be reached by a stale row. Say so in plain ASCII rather than
            // reporting a skip that did not happen.
            if (!DeNelle.Core.FeatureFlags.RewardedAdSkip)
            {
                FlowTrace.Warn("Manage",
                    "WatchAd tapped while ff.rewardedadskip is OFF - a stale row survived a rebuild. " +
                    "No ad, no reward. See FeatureFlags.RewardedAdSkip.");
                Notice = "Ad rewards are not available in this build.";
                NoticeIsBrokeCase = false;
                Rebuild();
                return;
            }
            // WO-1125: ASYNC. The bool overload answers "was the reward earned", which is
            // unanswerable at return time once a real SDK is wired - this screen would tell a
            // player who just watched thirty seconds of video "No ad available right now."
            NoticeIsBrokeCase = false;
            svc.WatchAdToSkip(channel, jobId, result =>
            {
                if (result.Rewarded)
                    Notice = "Time skipped.";
                else if (result.Reason == DeNelle.Core.Ads.AdUnavailableReason.Abandoned)
                    Notice = "Ad closed early - no time skipped.";   // their choice, not a failure
                else if (result.Reason == DeNelle.Core.Ads.AdUnavailableReason.CappedByGame)
                    Notice = "You have used your ad skips for now.";  // OUR cap, said plainly
                else
                    Notice = "No ad available right now.";
                NoticeIsBrokeCase = false;
                Rebuild();
            });
            Rebuild();
        }

        /// <summary>
        /// Ruling Q1 + Q12 — cancel THIS ONE job and refund 100% of what was paid for it, flat.
        /// The remaining items close the gap automatically (an active cancel frees its slot and the
        /// next pending job starts; a pending cancel shifts the rest up).
        /// </summary>
        public void Cancel(ChannelId channel, string jobId)
        {
            if (string.IsNullOrEmpty(jobId))
            {
                FlowTrace.Warn("Manage", "Cancel called with no job id — a collapsed stack is not a cancel target (Q12).");
                return;
            }
            var svc = BuildTimerService.Instance;
            if (svc == null) return;

            if (svc.CancelChannelJobWithRefund(channel, jobId, out JobCost refunded, out string unrefunded))
            {
                if (!refunded.IsZero)
                    Notice = "Cancelled. Refunded " + refunded.Describe() + ".";
                else
                    Notice = "Cancelled. Nothing to refund.";
            }
            else
                Notice = "Could not cancel that.";
            NoticeIsBrokeCase = false;
            Rebuild();
        }

        /// <summary>The owner's "bump up the next item" — drives the existing ReorderPending.</summary>
        public void BumpUp(ChannelId channel, string jobId, int pendingIndex)
        {
            if (string.IsNullOrEmpty(jobId) || pendingIndex <= 0) return;
            var svc = BuildTimerService.Instance;
            if (svc == null) return;
            Notice = svc.ReorderPending(channel, jobId, pendingIndex - 1) ? "Moved up." : "Could not move that.";
            NoticeIsBrokeCase = false;
            Rebuild();
        }

        /// <summary>
        /// WO-1253 — Manage "Buy builder" drops to the store focused on the permanent-builder SKU.
        /// Does NOT spend crystals. Crystal extra-slot (DEPTH) remains on the queue-full surface.
        /// </summary>
        public void BuySlot(ChannelId channel)
        {
            StoreFocusRequest.RequestFocusSku(PackCatalog.PermanentBuilderSku);
            if (!PanelRouter.Open(PanelId.RealmStore))
            {
                Notice = "Store is not open right now.";
                FlowTrace.Warn("Manage", "RealmStore opener not registered - builder SKU route dead-ends.");
            }
            else
            {
                Notice = null;
                FlowTrace.Step("Manage", "buy builder from " + channel + " -> store sku=" + PackCatalog.PermanentBuilderSku);
            }
            NoticeIsBrokeCase = false;
            Rebuild();
        }

        /// <summary>Ruling Q2 — the EXISTING instant repair, surfaced here. Never a queued job.</summary>
        public void RepairAll()
        {
            Guard.Try("Manage", "repair all", () =>
            {
                var repair = UnityEngine.Object.FindFirstObjectByType<WallRepairController>();
                if (repair == null) { Notice = "Nothing to repair."; return; }
                var result = repair.RepairAll();
                Notice = result.repairedCount > 0
                    ? "Repaired " + result.repairedCount + " structure(s)."
                    : "Nothing repaired - check resources.";
            });
            NoticeIsBrokeCase = false;
            Rebuild();
        }

        /// <summary>The broke-case route the owner's rule requires: a way to GET crystals.</summary>
        public void OpenCrystalStore()
        {
            if (!PanelRouter.Open(PanelId.RealmStore))
                FlowTrace.Warn("Manage", "RealmStore opener not registered — the broke-case route dead-ends.");
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private static void OpenUpgradePanel(string id)
        {
            if (!PanelRouter.Open(PanelId.BuildingUpgrade, id))
                FlowTrace.Warn("Manage", $"BuildingUpgrade opener not registered — cannot drill into '{id}'.");
        }

        private void UpgradePlaced(string jobKey)
        {
            using var _ = FlowTrace.Enter("Manage", $"Placed upgrade CTA '{jobKey}'");
            var result = Buildings.Progression.PlacedStructureUpgradeService.TryStart(jobKey);
            Notice = result.Success
                ? (result.Outcome == Buildings.Progression.PlacedUpgradeOutcome.Queued
                    ? "Upgrade queued."
                    : "Upgrade started.")
                : Ascii(string.IsNullOrEmpty(result.Message)
                    ? "Could not start that upgrade."
                    : result.Message);
            NoticeIsBrokeCase = false;
            Rebuild();
        }

        private void UpgradeBuilding(string buildingId, int targetTier)
        {
            using var _ = FlowTrace.Enter("Manage", $"Building upgrade CTA '{buildingId}' -> T{targetTier}");
            bool started = Buildings.Progression.BuildingUpgradeService.TryUpgrade(buildingId, targetTier);
            Notice = started
                ? "Upgrade started."
                : "Could not start that upgrade - check requirements and resources.";
            NoticeIsBrokeCase = false;
            Rebuild();
        }

        /// <summary>
        /// Start ONE building perk's research (the Research tab's CTA). Routes through
        /// BuildingPerkService so the gate, the gold charge, the Research-channel enqueue and the
        /// depth cap all behave identically to the building panel's perk tile — this screen never
        /// charges or enqueues anything itself. Unlike the other tabs' CTAs this is an INSTANCE
        /// method: starting a research puts a row on the line the player is currently looking at,
        /// so it sets a Notice and rebuilds rather than leaving the screen stale.
        /// </summary>
        private void Research(string buildingId, string perkId)
        {
            using var _ = FlowTrace.Enter("Manage", $"Research CTA '{buildingId}:{perkId}'");

            if (!Buildings.Progression.BuildingPerkService.CanResearch(buildingId, perkId, out string reason))
            {
                FlowTrace.Warn("Manage", $"research '{buildingId}:{perkId}' refused: {reason}");
                Notice = ManageScreenVM.Ascii(string.IsNullOrEmpty(reason) ? "Cannot research that yet." : reason);
                NoticeIsBrokeCase = false;
                Rebuild();
                return;
            }

            if (Buildings.Progression.BuildingPerkService.TryResearch(buildingId, perkId))
            {
                Notice = "Research started.";
                NoticeIsBrokeCase = false;
            }
            else
            {
                // TryResearch only gets here on a spend failure, a missing service or a refused
                // enqueue - each of which has already left its own [Flow:Research] line naming
                // which one it was, so this message never has to guess in the log.
                Notice = "Could not start that research - check your gold.";
                NoticeIsBrokeCase = false;
            }
            Rebuild();
        }

        /// <summary>
        /// PROD-013 — the Troops tab's TRAIN CTA: enqueue ONE unit of <paramref name="troopId"/> on
        /// the Train channel. Routes through <see cref="BarracksService.EnqueueTraining(string,int,out string)"/>
        /// so the unlock gate, the army-cap check, the resource charge and the queue depth cap all
        /// behave identically to every other train path — this screen charges and enqueues nothing
        /// itself, exactly like <see cref="UpgradeTroop"/> and <see cref="Research"/>.
        ///
        /// An INSTANCE method (like Research, unlike UpgradeTroop) because a successful train puts a
        /// row on the very line the player is looking at: it sets a Notice and rebuilds rather than
        /// leaving the screen stale.
        /// </summary>
        private void TrainTroop(string troopId)
        {
            using var _ = FlowTrace.Enter("Manage", $"Train CTA '{troopId}'");

            int enqueued = BarracksService.EnqueueTraining(troopId, 1, out string stopReason);
            if (enqueued > 0)
            {
                // §12 proving line: the id, what it cost, and the job that now exists. BarracksService
                // logs the jobId itself at enqueue ("train job enqueued 1/1 ... jobId=barracks-train:...");
                // this line names the SCREEN the request came from so the two can be paired in a capture.
                var def = TroopCatalog.Find(troopId);
                string costText = def != null
                    ? def.CostGold + " gold"
                    : "unknown cost";
                FlowTrace.Step("Manage",
                    $"train enqueued from Manage: id={troopId} qty={enqueued} cost=[{costText}] " +
                    $"channel=Train jobIdPrefix={BarracksService.TrainPrefix}{troopId}");
                Notice = "Training started.";
                NoticeIsBrokeCase = false;
            }
            else
            {
                // Refused: locked / army full / unaffordable / queue depth full. BarracksService
                // hands back the ASCII sentence naming WHICH, so the notice never has to guess.
                FlowTrace.Warn("Manage",
                    $"train '{troopId}' refused: {(string.IsNullOrEmpty(stopReason) ? "no reason given" : stopReason)}");
                Notice = Ascii(string.IsNullOrEmpty(stopReason) ? "Could not start that training." : stopReason);
                NoticeIsBrokeCase = false;
            }
            Rebuild();
        }

        private static void UpgradeTroop(string troopId)
        {
            // Routes through the existing service so the charge, the queue and the cap all behave
            // identically to the barracks panel. This screen never charges anything itself.
            if (!BarracksService.CanUpgradeTroop(troopId, out string reason))
            {
                FlowTrace.Warn("Manage", $"troop upgrade '{troopId}' refused: {reason}");
                return;
            }
            BarracksService.UpgradeTroop(troopId);
        }

        private static int CrystalBalance()
        {
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            return state != null ? state.Resources.Crystals : 0;
        }

        /// <summary>
        /// GOLD (economy Coins) — the currency building-perk research charges, and the ONLY one of
        /// the four tabs that uses it. Reads EconomyService.Coins, which is itself a view onto
        /// GameState.Resources.Coins, so the number shown is the number the spend will check; the
        /// direct-state read is the headless / pre-boot fallback (same shape as <see cref="CanAfford"/>).
        /// </summary>
        private static int GoldBalance()
        {
            var econ = EconomyService.Instance;
            if (econ != null) return econ.Coins;
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            return state != null ? state.Resources.Coins : 0;
        }

        private static bool CanAfford(CoreCost cost)
        {
            var econ = EconomyService.Instance;
            if (econ != null) return econ.CanAfford(BuildModeController.ToEconomy(cost));
            // Headless / pre-boot: fall back to the ledger, which reads the same GameState fields.
            var state = GameStateService.Instance != null ? GameStateService.Instance.State : null;
            if (state == null) return false;
            return state.Wood >= cost.wood && state.Iron >= cost.iron
                && state.Resources.Food >= cost.food && state.Resources.Crystals >= cost.crystals;
        }

        private static string ShortfallOf(CoreCost cost)
        {
            string msg = BuildModeController.ShortfallMessage(cost);
            return string.IsNullOrEmpty(msg) ? "Short on resources" : Ascii(msg);
        }

        /// <summary>
        /// The Finish CTA's cost sub-line (see <see cref="QueueRowVM.FinishCostText"/>): the currency
        /// SPELLED OUT and singular/plural correct, plus the shortfall in words when the player is
        /// short. ASCII only - a currency glyph renders as tofu in TMP.
        ///
        /// The shortfall is stated as a NUMBER OF CRYSTALS rather than left to a grey face, because
        /// the owner is red/green colourblind and no affordance may convey its meaning by colour
        /// alone: "cannot afford" has to be readable as text.
        /// </summary>
        public static string DescribeFinishCost(int price, int crystals)
        {
            if (price <= 0) return "";
            int missing = price - crystals;
            // "Short N <currency>" is THIS screen's existing shortfall idiom (BuildResearchBrowse
            // already says "Short 40 gold"), so the two tabs read alike. It also stays inside the
            // CTA's width budget, which "5 crystals - need 3 more" would not: the sub-line has only
            // ~313-350 reference px, and a 20+ character string auto-shrinks to the font floor and
            // then ellipsizes — which would put us right back at an unreadable face.
            return missing > 0 ? "Short " + Crystals(missing) : Crystals(price);
        }

        /// <summary>"1 crystal" / "5 crystals" — the currency SPELLED OUT, singular/plural correct.
        /// Never "5c": the owner's felt-test is that the abbreviation says nothing to a new player.</summary>
        private static string Crystals(int n) => n + (n == 1 ? " crystal" : " crystals");

        /// <summary>ASCII cost summary ("400 wood, 200 food"); "free" when nothing is charged.</summary>
        public static string DescribeCost(CoreCost c)
        {
            var parts = DeNelle.Core.UI.CostFormat.Parts(new[] { ("wood", "Wood", c.wood), ("stone", "Stone", c.food), ("iron", "Iron", c.iron), ("crystal", "Crystals", c.crystals) });
            return parts.Count > 0 ? DeNelle.Core.UI.CostFormat.Words(parts) : "free";
        }

        private void AddGoldBrowseRow(string label, CoreCost materials, int gold, string actionText, Action activate)
        {
            bool affordable = CanAfford(materials) && GoldBalance() >= gold;
            string materialText = DescribeCost(materials);
            string costText = materialText == "free" ? gold + " gold" : materialText + ", " + gold + " gold";
            BrowseRows.Add(new BrowseRowVM {
                Label = label, CostText = costText, Affordable = affordable,
                StateText = affordable ? "Ready" : "Short on resources",
                CostWeight = materials.wood + materials.food + materials.iron + materials.crystals + gold,
                ActionText = actionText, Activate = activate
            });
        }

        private static CoreCost BuildingTierBasket(BuildingTierDef tier)
        {
            if (tier == null) return default;
            int primary = tier.PrimaryMaterialCost;
            return new CoreCost {
                wood = tier.Tier == 1 ? primary : 0,
                food = tier.Tier == 2 ? primary : 0,
                iron = tier.Tier >= 3 ? primary : 0,
            };
        }

        private static string NameOf(CatalogEntry entry, string fallbackId)
            => !string.IsNullOrEmpty(entry.displayName) ? Ascii(entry.displayName) : Ascii(fallbackId);

        private static string NameOfTroop(TroopDef def)
            => !string.IsNullOrEmpty(def.DisplayName) ? Ascii(def.DisplayName) : Ascii(def.Id);

        /// <summary>"1st" / "2nd" / "3rd" / "4th" — ASCII ordinal for the line position.</summary>
        /// <summary>
        /// WO-898 item 1 — live elapsed fraction for a RUNNING job, by id. The 1 Hz tick uses this
        /// so a bar advances while the screen is open without rebuilding a single row.
        /// Returns 0 when the job is not running or its duration is unknown.
        /// </summary>
        public static float ProgressOfLive(BuildTimerService svc, ChannelId channel, string jobId)
        {
            if (svc == null || string.IsNullOrEmpty(jobId)) return 0f;
            var active = svc.ActiveJobsOf(channel);
            for (int i = 0; i < active.Count; i++)
            {
                if (!string.Equals(active[i].StructureId, jobId, StringComparison.Ordinal)) continue;
                var job = active[i];
                if (job.DurationMs <= 0d || job.StartMs <= 0d) return 0f;
                double totalSec = job.DurationMs / 1000d;
                double rem = svc.RemainingSeconds(channel, jobId);
                return Mathf.Clamp01((float)((totalSec - rem) / totalSec));
            }
            return 0f;   // not in the active list => queued or finished; the bar stays empty
        }

        /// <summary>
        /// The percentage rendered IN WORDS, e.g. " (63% done)". The colourblind law forbids the
        /// fill being the only signal, so every bar is paired with this. Empty string when there is
        /// no meaningful progress to state.
        /// </summary>
        public static string PercentSuffix(BuildTimerService svc, ChannelId channel, string jobId)
        {
            float p = ProgressOfLive(svc, channel, jobId);
            if (p <= 0f) return "";
            return " (" + Mathf.RoundToInt(p * 100f) + "% done)";
        }

        internal static string Ordinal(int n)
        {
            if (n <= 0) return "next";
            int mod100 = n % 100;
            if (mod100 >= 11 && mod100 <= 13) return n + "th";
            switch (n % 10)
            {
                case 1: return n + "st";
                case 2: return n + "nd";
                case 3: return n + "rd";
                default: return n + "th";
            }
        }

        /// <summary>ASCII countdown ("2m 10s"). No non-ASCII: TMP renders it as tofu.</summary>
        public static string FormatTime(double seconds)
        {
            int s = Mathf.Max(0, Mathf.CeilToInt((float)seconds));
            if (s >= 3600) return (s / 3600) + "h " + ((s % 3600) / 60) + "m";
            if (s >= 60) return (s / 60) + "m " + (s % 60) + "s";
            return s + "s";
        }

        /// <summary>Strip anything outside printable ASCII — the LiberationSans-SDF tofu rule.</summary>
        public static string Ascii(string s)
        {
            if (string.IsNullOrEmpty(s)) return s;
            var sb = new System.Text.StringBuilder(s.Length);
            for (int i = 0; i < s.Length; i++)
            {
                char c = s[i];
                if (c >= ' ' && c <= '~') sb.Append(c);
                else if (c == '→') sb.Append("->");
                else if (c == '×') sb.Append('x');
                else sb.Append(' ');
            }
            return sb.ToString();
        }
    }
}
