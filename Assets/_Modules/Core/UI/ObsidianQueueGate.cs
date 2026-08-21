// =============================================================================
// ObsidianQueueGate — the Core-level "open/close the work-queue panel" seam (WO-773).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI
//
// The common work-queue HUD (ObsidianQueueHud, in DeNelle.Village) is opened by a
// HUD button (DeNelle.HUD, VillageHudController). Those two assemblies cannot
// reference each other (CLAUDE.md §5 — both reference DeNelle.Core only), so the
// toggle request routes through this tiny static gate in Core — the SAME cross-
// assembly pattern as HarvestPanelGate / PauseGate / PanelManager.
//
// PLAYER-FACING NAMING: the panel title/copy says "Builders" / "Training" — never
// "Obsidian" (internal code name only).
// =============================================================================

using System;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// Static toggle arbiter for the common work-queue panel. A HUD button calls
    /// <see cref="RequestToggle"/>; the panel (ObsidianQueueHud, in DeNelle.Village)
    /// subscribes to <see cref="ToggleRequested"/> and flips its own visibility.
    /// </summary>
    public static class ObsidianQueueGate
    {
        /// <summary>Raised when the work-queue button is tapped. The panel subscribes and toggles.</summary>
        public static event Action ToggleRequested;

        /// <summary>
        /// WO-911 — true when a panel is actually listening. The re-pointed bar face checks this so
        /// a boot race (face tapped before ManageScreenPanel installs) falls back to
        /// <c>PanelRouter.Open(PanelId.Manage)</c> instead of being a dead tap the player has no way
        /// to distinguish from a broken button.
        /// </summary>
        public static bool HasSubscriber => ToggleRequested != null;

        /// <summary>Raise the toggle request (null-safe; safe from any assembly referencing DeNelle.Core).</summary>
        public static void RequestToggle()
        {
            FlowTrace.Step("HUD", "ObsidianQueueGate.RequestToggle — work-queue panel toggle requested");
            ToggleRequested?.Invoke();
        }

        // ── WO-778: persistent status snapshot (Village publishes, HUD polls) ──
        // BuildTimerService (DeNelle.Village) owns queue + clock; it pushes a
        // presentation-ready snapshot here on QueueChanged + its 1s tick. The HUD
        // chip polls Status (the HudBuildingFocus precedent) — no cross-assembly read.

        /// <summary>One visible queue row (owner 2026-07-30: WC3-style "show like 5 deep").</summary>
        /// <remarks>
        /// WO-864 widened this from a text row into a CARD record. The extra fields are all
        /// PRESENTATION-READY — the publisher (BuildTimerService, Village) resolves verb/icon/
        /// stack ONCE per publish so every host (the always-on HUD rail, the Work Queue modal,
        /// a future Manage screen) renders the SAME card from the SAME data and can never
        /// disagree. Nothing here is timer or economy logic.
        /// </remarks>
        public struct QueueEntry
        {
            public string Label;         // player-facing job name ("Barracks", "Arcane Spire")
            public int RemainingSec;     // active job countdown; -1 for a queued (waiting) job
            public bool Queued;          // true = waiting for a free crew, false = in progress

            // ── WO-864 card fields ───────────────────────────────────────────
            /// <summary>ASCII uppercase verb — BUILD / UPGRADE / REPAIR / TRAIN / RESEARCH / FREE.
            /// The card is designed VERB-FIRST (owner ruling 2026-08-03: "if no images we can use
            /// verbs") so a portrait is a bonus, never a requirement.</summary>
            public string Verb;
            /// <summary>Empty => resolve art from <see cref="JobId"/> under Resources/Portraits.
            /// Non-empty => an RpgUiCatalog ROLE ("icons") paired with <see cref="IconKey"/>.</summary>
            public string IconRole;
            /// <summary>Sprite key within <see cref="IconRole"/>. Ignored when IconRole is empty.</summary>
            public string IconKey;
            /// <summary>&gt;=1. When &gt;1 the card draws an "xN" badge (N identical troop trains
            /// collapse to ONE card — the CoC "Barbarian x5" read).</summary>
            public int StackCount;
            /// <summary>Opaque action token (the job's structure id) for Instant/Ad hooks + icon
            /// resolution. Never shown to the player.</summary>
            public string JobId;
            /// <summary>Upgrade target tier (0 = none). Selects tier art where it exists.</summary>
            public int TargetTier;
            /// <summary>TRUE => this is an EMPTY SLOT placeholder, not a job. A free slot must
            /// render as a visible empty-slot CARD, never as blank space (WO-864 bug 2).</summary>
            public bool Free;
        }

        /// <summary>Presentation-ready queue summary for the persistent HUD chip + card rails.</summary>
        public struct WorkQueueStatus
        {
            public bool Available;                       // false until the service publishes
            public int BuilderBusy, BuilderSlots, BuilderQueued;
            public int TrainBusy, TrainSlots, TrainQueued;
            public int ResearchBusy, ResearchSlots, ResearchQueued;
            public int SoonestRemainingSec;              // min across all channels; -1 = idle
            public QueueEntry[] Entries;                 // Builder channel, active first (back-compat name)
            public QueueEntry[] TrainEntries;            // WO-864 — Train channel, active first
            public QueueEntry[] ResearchEntries;         // WO-864 — Research channel, active first
            public int Version;                          // bumps per publish (change-detect)

            /// <summary>The published entries for a channel (never null — empty array when idle).</summary>
            public QueueEntry[] EntriesOf(DeNelle.Core.Jobs.ChannelId c)
            {
                QueueEntry[] a;
                switch (c)
                {
                    case DeNelle.Core.Jobs.ChannelId.Train: a = TrainEntries; break;
                    case DeNelle.Core.Jobs.ChannelId.Research: a = ResearchEntries; break;
                    default: a = Entries; break;
                }
                return a ?? System.Array.Empty<QueueEntry>();
            }

            /// <summary>Total worker slots on a channel (the empty-slot card count comes from this).</summary>
            public int SlotsOf(DeNelle.Core.Jobs.ChannelId c)
            {
                switch (c)
                {
                    case DeNelle.Core.Jobs.ChannelId.Train: return TrainSlots;
                    case DeNelle.Core.Jobs.ChannelId.Research: return ResearchSlots;
                    default: return BuilderSlots;
                }
            }

            /// <summary>Busy (active) worker count on a channel.</summary>
            public int BusyOf(DeNelle.Core.Jobs.ChannelId c)
            {
                switch (c)
                {
                    case DeNelle.Core.Jobs.ChannelId.Train: return TrainBusy;
                    case DeNelle.Core.Jobs.ChannelId.Research: return ResearchBusy;
                    default: return BuilderBusy;
                }
            }

            // ─────────────────────────────────────────────────────────────────
            //  WO-1027 — THE SESSION-SHAPE DERIVATION. ONE sentence, one home.
            // ─────────────────────────────────────────────────────────────────
            // IDLE == ZERO ACTIVE WORKERS ON THE LINE. Deliberately NOT (Busy < Slots):
            // a line running 1 of 2 builders is WORKING, not aching, and a bar that said
            // "idle" while a building is visibly under construction reads as a bug. The
            // finer has-a-free-slot fact is the FREE CARD's business (QueueRailView), and
            // it is deliberately a different, finer signal.
            //
            // ⚠ queueDepthPerLine (5) IS NOT AN INPUT. Depth is the LINE LENGTH;
            // freeBuildSlots (2) is CONCURRENCY (ObsidianQueueEngine: "Depth is NOT
            // concurrency ... conflating them would delete the waiting pain the crystal
            // sink monetizes"). Idleness is a concurrency fact only.
            //
            // These live ON the struct every reader already holds so the bar face, the
            // rail and the Manage screen cannot disagree — this project's dominant bug is
            // duplicate authority, and three surfaces each deriving their own idleness is
            // exactly how it starts. A `BusyOf(...) == 0` written anywhere else is the bug.

            /// <summary>The three queue channels — the denominator of the "N of 3 idle" glance.</summary>
            public const int LineCount = 3;

            /// <summary>TRUE when nothing at all is running on this channel. False before the
            /// first publish (<see cref="Available"/>): never claim idleness we have not been told
            /// about — at boot that would put "3 of 3 idle" on a screen that has heard nothing.</summary>
            public bool IsLineIdle(DeNelle.Core.Jobs.ChannelId c) => Available && BusyOf(c) == 0;

            /// <summary>How many of the three channels have NOTHING running. 0 before first publish.</summary>
            public int IdleLineCount()
            {
                if (!Available) return 0;
                int n = 0;
                if (IsLineIdle(DeNelle.Core.Jobs.ChannelId.Builder)) n++;
                if (IsLineIdle(DeNelle.Core.Jobs.ChannelId.Train)) n++;
                if (IsLineIdle(DeNelle.Core.Jobs.ChannelId.Research)) n++;
                return n;
            }

            /// <summary>
            /// WO-1027 §3.3 — the QUIET INVERSE of the ache: every line is not merely busy but
            /// FULLY crewed, so there is nothing left to start. Deliberately STRICTER than
            /// <c>IdleLineCount()==0</c> (which only means each line has one worker going): telling
            /// a player she is done while a slot is still free would be a lie, and a wrong
            /// session-complete signal is worse than none.
            /// </summary>
            public bool AllLinesLoaded()
            {
                if (!Available) return false;
                return BusyOf(DeNelle.Core.Jobs.ChannelId.Builder) >= SlotsOf(DeNelle.Core.Jobs.ChannelId.Builder)
                    && BusyOf(DeNelle.Core.Jobs.ChannelId.Train) >= SlotsOf(DeNelle.Core.Jobs.ChannelId.Train)
                    && BusyOf(DeNelle.Core.Jobs.ChannelId.Research) >= SlotsOf(DeNelle.Core.Jobs.ChannelId.Research)
                    && IdleLineCount() == 0;
            }

            /// <summary>Free (uncrewed) worker slots on a channel — the FREE-CARD axis, and the
            /// single home for it. QueueRailView reads this instead of re-deriving it.</summary>
            public int FreeSlotsOf(DeNelle.Core.Jobs.ChannelId c)
            {
                int f = SlotsOf(c) - BusyOf(c);
                return f > 0 ? f : 0;
            }

            /// <summary>Player-facing channel name — never "Obsidian" (naming law).</summary>
            public static string LabelOf(DeNelle.Core.Jobs.ChannelId c)
            {
                switch (c)
                {
                    case DeNelle.Core.Jobs.ChannelId.Train: return "TRAINING";
                    case DeNelle.Core.Jobs.ChannelId.Research: return "RESEARCH";
                    default: return "BUILDERS";
                }
            }
        }

        private static int _statusVersion;

        /// <summary>Latest published snapshot (default/Available=false before first publish).</summary>
        public static WorkQueueStatus Status { get; private set; }

        /// <summary>Village-side publisher (BuildTimerService). Bumps Version.</summary>
        public static void PublishStatus(WorkQueueStatus s)
        {
            s.Version = ++_statusVersion;
            Status = s;
        }
    }
}
