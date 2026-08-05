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
