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
    }

    /// <summary>One row in the "UPGRADES" browse section — the WO-905 affordability answer.</summary>
    public sealed class BrowseRowVM
    {
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

        /// <summary>Last command's player-facing message (ASCII), or null. The View toasts it.</summary>
        public string Notice { get; private set; }

        /// <summary>True when <see cref="Notice"/> is the broke case and the View should offer the store.</summary>
        public bool NoticeIsBrokeCase { get; private set; }

        /// <summary>Extra-slot purchase price for the active tab's channel (0 = not for sale).</summary>
        public int SlotPrice { get; private set; }

        /// <summary>ASCII sentence describing the extra-slot offer or why it is locked.</summary>
        public string SlotOfferText { get; private set; } = "";

        /// <summary>WO-911 (Q2) — crystal-free instant repair cost, or null when nothing is damaged.</summary>
        public string RepairOfferText { get; private set; }

        private readonly HashSet<string> _expandedStacks = new HashSet<string>();

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

                BuildChannelSummaries();
                BuildQueueRows(ChannelOf(Tab));
                BuildSlotOffer(ChannelOf(Tab));
                BuildRepairOffer();
                BuildBrowseRows();
            });
            Changed?.Invoke();
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
            SlotPrice = 0;
            SlotOfferText = "";
            var svc = BuildTimerService.Instance;
            if (svc == null) return;

            int entitled = svc.EchoEntitledSlots();
            int bought = svc.BoughtSlotsOf(channel);
            if (bought >= entitled)
            {
                // Two-step gate (ruling Q6): say which step is missing, in words.
                SlotOfferText = entitled <= 0
                    ? "Extra slot: locked - awaken a 3rd Echo"
                    : "Extra slot: locked - all " + entitled + " Echo slot(s) used";
                return;
            }
            SlotPrice = svc.NextSlotPrice(channel);
            SlotOfferText = SlotPrice > 0
                ? "Extra slot: " + SlotPrice + " crystals"
                : "Extra slot: unavailable";
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
                return a.CostWeight.CompareTo(b.CostWeight);
            });
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
                if (entry.repo.maxLevel <= 1) continue;                  // nothing to upgrade to

                int level = Mathf.Max(1, placed.level);
                if (level >= entry.repo.maxLevel) continue;              // already maxed
                string key = placed.itemId + "@" + level;
                if (!seen.Add(key)) continue;                            // one row per id+level

                var cost = BuildModeController.UpgradeCostFor(entry, level);
                string id = placed.itemId;
                AddBrowseRow(NameOf(entry, id) + " -> L" + (level + 1), cost, "Open",
                             () => OpenUpgradePanel(id));
            }
        }

        private void BuildBuildingsBrowse()
        {
            var all = BuildingTierCatalog.All;
            if (all == null) return;

            for (int i = 0; i < all.Count; i++)
            {
                var def = all[i];
                if (def == null || string.IsNullOrEmpty(def.Id)) continue;

                int tier = ModifierService.TierOf(def.Id);
                if (tier < 1) continue;                                  // not built / locked
                var next = BuildingTierCatalog.TierOf(def.Id, tier + 1);
                if (next == null) continue;                              // already at max tier

                var cost = new CoreCost { wood = next.CostWood, food = next.CostFood, crystals = next.CostCrystal };
                string label = (string.IsNullOrEmpty(def.DisplayName) ? def.Id : def.DisplayName)
                             + " -> T" + next.Tier;
                string id = def.Id;
                AddBrowseRow(label, cost, "Open", () => OpenUpgradePanel(id));
            }
        }

        private void BuildTroopsBrowse()
        {
            var all = TroopCatalog.All;
            if (all == null) return;

            for (int i = 0; i < all.Count; i++)
            {
                var def = all[i];
                if (def == null || string.IsNullOrEmpty(def.Id)) continue;
                if (!BarracksService.IsTroopUnlocked(def.Id)) continue;

                int level = BarracksService.TroopLevel(def.Id);
                if (!BarracksProgression.HasNextTroopLevel(def.Id, level)) continue;

                var econCost = BarracksProgression.TroopUpgradeCost(def.Id, level + 1);
                var cost = new CoreCost
                {
                    wood = econCost.Wood,
                    food = econCost.Food,
                    iron = econCost.Iron,
                    crystals = econCost.Crystals,
                };
                string id = def.Id;
                AddBrowseRow(NameOfTroop(def) + " -> L" + (level + 1), cost, "Upgrade",
                             () => UpgradeTroop(id));
            }
        }

        private void BuildResearchBrowse()
        {
            // Research's "items" are the per-building perk grid. Its costs are authored inside the
            // perk defs and are already rendered by BuildingUpgradeVM, so this tab BROWSES the
            // buildings that have something researchable and DRILLS IN to the existing panel rather
            // than duplicating a second cost model that could disagree with the one that charges.
            var all = BuildingTierCatalog.All;
            if (all == null) return;

            for (int i = 0; i < all.Count; i++)
            {
                var def = all[i];
                if (def == null || string.IsNullOrEmpty(def.Id) || def.Tiers == null) continue;
                if (ModifierService.TierOf(def.Id) < 1) continue;        // not built

                bool hasPerk = false;
                for (int t = 0; t < def.Tiers.Count && !hasPerk; t++)
                {
                    var tierDef = def.Tiers[t];
                    if (tierDef?.Perks == null) continue;
                    for (int p = 0; p < tierDef.Perks.Count; p++)
                    {
                        var perk = tierDef.Perks[p];
                        if (perk == null || string.IsNullOrEmpty(perk.Id)) continue;
                        if (!Buildings.Progression.BuildingPerkService.IsOwned(def.Id, perk.Id)) { hasPerk = true; break; }
                    }
                }
                if (!hasPerk) continue;

                string id = def.Id;
                BrowseRows.Add(new BrowseRowVM
                {
                    Label = (string.IsNullOrEmpty(def.DisplayName) ? def.Id : def.DisplayName) + " research",
                    CostText = "",
                    StateText = "Open to see costs",
                    Affordable = false,
                    CostWeight = float.MaxValue,     // sorts after everything priced here
                    ActionText = "Open",
                    Activate = () => OpenUpgradePanel(id),
                });
            }
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
            Notice = svc.WatchAdToSkip(channel, jobId) ? "Time skipped." : "No ad available right now.";
            NoticeIsBrokeCase = false;
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

            if (svc.CancelChannelJobWithRefund(channel, jobId, out JobCost refunded))
                Notice = refunded.IsZero ? "Cancelled. Nothing to refund." : "Cancelled. Refunded " + refunded.Describe() + ".";
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

        /// <summary>Ruling Q6 — the two-step Echo-gated, crystal-priced extra slot.</summary>
        public void BuySlot(ChannelId channel)
        {
            var svc = BuildTimerService.Instance;
            if (svc == null) return;
            if (svc.TryBuySlot(channel, out string failure))
            {
                Notice = "Extra slot unlocked.";
                NoticeIsBrokeCase = false;
            }
            else
            {
                Notice = failure ?? "Could not buy a slot.";
                NoticeIsBrokeCase = failure != null &&
                                    failure.StartsWith(BuildTimerService.InsufficientCrystalsPrefix, StringComparison.Ordinal);
            }
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

        /// <summary>ASCII cost summary ("400 wood, 200 food"); "free" when nothing is charged.</summary>
        public static string DescribeCost(CoreCost c)
        {
            var sb = new System.Text.StringBuilder();
            if (c.wood > 0) sb.Append(c.wood).Append(" wood");
            if (c.food > 0) { if (sb.Length > 0) sb.Append(", "); sb.Append(c.food).Append(" food"); }
            if (c.iron > 0) { if (sb.Length > 0) sb.Append(", "); sb.Append(c.iron).Append(" iron"); }
            if (c.crystals > 0) { if (sb.Length > 0) sb.Append(", "); sb.Append(c.crystals).Append(" crystals"); }
            return sb.Length > 0 ? sb.ToString() : "free";
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
