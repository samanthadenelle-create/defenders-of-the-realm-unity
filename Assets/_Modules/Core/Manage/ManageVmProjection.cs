// =============================================================================
// ManageVmProjection - WO-2002. The ONE model-side mapping from Wave 0's
// ManageItemState onto Wave 1's presentation contract.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Manage
//
// WHY A PROJECTION EXISTS AT ALL. Canon 10: "Do not create three independent UI
// systems with duplicated lock/cost/queue logic." If BUILD, ARMY and RESEARCH each
// turned their own ManageItemState into tiles, each would choose its own badge
// word, its own frame, its own "which action goes in the primary slot" rule - and
// the three would drift, which is the same duplicated-state failure that produced
// the stale WO-number block and the retired dependency table in CLAUDE.md. The
// three tab VMs compose ManageItemState (they own the game rules); THIS file turns
// it into pixels-facing records, once.
//
// ⛔ THIS IS MODEL-SIDE CODE, NOT VIEW CODE, and the distinction is load-bearing.
// It may collapse states, pick art keys and format a duration - all of which canon
// 9 forbids the VIEW from doing. It runs in DeNelle.Core, it touches no service,
// no catalog and no GameState, and the renderer never calls it: the composer does,
// and hands the renderer the finished VMs.
//
// ⛔ IT DECIDES NOTHING THE MODEL ALREADY DECIDED. Every WORD it emits
// (BadgeText, Cta, BlockerReason, Route.Cta, DisplayName) is carried VERBATIM off
// the ManageItemState. It never derives a label from an enum name - if the composer
// left BadgeText empty, the tile gets an empty state word and ManageStateInvariants
// says so out loud, which is the correct outcome. Inventing a fallback word here
// would hide exactly the defect Wave 0's validator exists to surface.
//
// INSTRUMENTED, NEVER SILENT (CLAUDE.md 12): every item is run through
// ManageStateInvariants.Validate and each violation is announced through FlowTrace
// under [Flow:Manage]. The projection still returns a tile - refusing to render is
// worse than rendering a flagged one - but the trace names the ruling that broke.
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.Manage
{
    /// <summary>Wave 0 state -> Wave 1 presentation. Pure mapping, no game rules.</summary>
    public static class ManageVmProjection
    {
        // ── the badge collapse (9 authored badges -> 5 painted states) ────────

        /// <summary>
        /// Collapses <see cref="ManageTileBadge"/> onto the five canon-7 states the delivered
        /// medallion set can paint. This is a NARROWING, not a second truth - the badge stays
        /// the authored state on the item.
        ///
        /// <para>⚠ <see cref="ManageTileBadge.UpgradeUnaffordable"/> maps to
        /// <see cref="ManageTileVisualState.Available"/>, NOT to Locked. Owner ruling 15 and the
        /// precedent already shipped at HeartPanel.cs:420-440: an owned thing you cannot afford
        /// yet is not locked, and a padlock teaches "you can never get there". The shortfall is
        /// carried by the CTA's DisabledReasonText.</para>
        ///
        /// <para>⚠ <see cref="ManageTileBadge.Max"/> maps to Max and says NOTHING about training
        /// (ruling 13). A maxed Footman still projects a Train action reading Available.</para>
        /// </summary>
        public static ManageTileVisualState VisualStateFor(ManageTileBadge badge)
        {
            switch (badge)
            {
                case ManageTileBadge.Locked: return ManageTileVisualState.Locked;
                case ManageTileBadge.QueueBlocked: return ManageTileVisualState.QueueBlocked;
                case ManageTileBadge.Upgrading: return ManageTileVisualState.InProgress;
                case ManageTileBadge.Training: return ManageTileVisualState.InProgress;
                case ManageTileBadge.Max: return ManageTileVisualState.Max;
                default: return ManageTileVisualState.Available;
            }
        }

        // ── one action ────────────────────────────────────────────────────────

        /// <summary>
        /// Projects one Wave-0 <see cref="ManageAction"/> onto a button.
        ///
        /// <para><b>The route is bound INTO the callback here</b>, which is the mechanism that
        /// lets the View stay dumb: a PrerequisiteBlocked or QueueBlocked action becomes a
        /// VISIBLE, ENABLED button whose words are the route's CTA ("VIEW HEART") and whose
        /// Activate walks the player to the blocker's home. Ruling 18 - a lock without a route
        /// is the defect this program exists to kill - and it is satisfied without the renderer
        /// ever seeing a <see cref="ManageRoute"/>.</para>
        ///
        /// <para>A blocked action with NO routable route stays visible and DISABLED, carrying
        /// its BlockerReason. That combination is itself a violation the Wave-0 validator
        /// reports; the projection surfaces it rather than hiding the button.</para>
        /// </summary>
        /// <param name="action">The composed action. Null yields a hidden button.</param>
        /// <param name="navigate">
        /// The composer's route handler. Null is legitimate (a surface with no navigation yet)
        /// and downgrades a routable CTA to a disabled one WITH a trace line - never a silent
        /// dead button.
        /// </param>
        public static ManageActionVM ProjectAction(ManageAction action, Action<ManageRoute> navigate)
        {
            if (action == null) return ManageActionVM.Hidden;
            if (action.Availability == ManageActionAvailability.NotApplicable) return ManageActionVM.Hidden;

            bool blocked = action.Availability == ManageActionAvailability.Unaffordable ||
                           action.Availability == ManageActionAvailability.PrerequisiteBlocked ||
                           action.Availability == ManageActionAvailability.QueueBlocked;

            var vm = new ManageActionVM
            {
                Visible = true,
                CostText = action.CostLine,
                Label = action.Cta,
                StyleRole = StyleRoleFor(action.Kind, blocked)
            };

            if (blocked && action.Route.IsRoutable)
            {
                // The blocker HAS a door. The button becomes the door, in the model's words.
                vm.Label = action.Route.Cta;
                vm.StyleRole = ManageActionStyleRole.Navigate;
                if (navigate != null)
                {
                    ManageRoute route = action.Route;
                    vm.Enabled = true;
                    vm.Activate = () => navigate(route);
                    vm.DisabledReasonText = null;
                }
                else
                {
                    FlowTrace.Warn("Manage", "action " + action.Kind + " is " + action.Availability +
                        " and routes to " + action.Route.Kind + " but the composer supplied no route " +
                        "handler - the CTA renders DISABLED rather than pointing at a phantom (ruling 18)");
                    vm.Enabled = false;
                    vm.DisabledReasonText = action.BlockerReason;
                }
                return vm;
            }

            if (blocked)
            {
                vm.Enabled = false;
                vm.DisabledReasonText = action.BlockerReason;
                return vm;
            }

            if (action.Availability == ManageActionAvailability.InProgress)
            {
                // Running work is not a button the player presses again. It is reported by the
                // activity strip and the tile timer; the face stays visible and inert so the
                // layout does not jump when a job starts.
                vm.Enabled = false;
                vm.DisabledReasonText = null;
                return vm;
            }

            vm.Enabled = true;
            Action invoke = action.Invoke;
            vm.Activate = invoke;
            return vm;
        }

        private static ManageActionStyleRole StyleRoleFor(ManageActionKind kind, bool blocked)
        {
            if (kind == ManageActionKind.Cancel) return ManageActionStyleRole.Destructive;
            if (kind == ManageActionKind.Navigate) return ManageActionStyleRole.Navigate;
            if (blocked) return ManageActionStyleRole.Secondary;
            return ManageActionStyleRole.Primary;
        }

        // ── one tile ──────────────────────────────────────────────────────────

        /// <summary>
        /// Projects an item onto a grid tile. <paramref name="onSelect"/> is the composer's
        /// selection command - the tile never decides what selection means.
        /// </summary>
        public static ManageTileVM ProjectTile(ManageItemState item, bool isSelected, Action onSelect)
        {
            if (item == null)
            {
                // A null item is a FAILURE, not an empty tile (same stance as
                // ManageStateInvariants.Validate). Say so and paint a placeholder that reads
                // as broken rather than as an ordinary empty slot.
                FlowTrace.Fail("Manage", "a null ManageItemState reached ProjectTile - the grid " +
                    "would show a blank cell with no explanation");
                return new ManageTileVM
                {
                    Id = null,
                    Title = "MISSING",
                    VisualState = ManageTileVisualState.Locked,
                    StateText = "NO DATA",
                    StateIconKey = ManageArt.StatusFor(ManageTileVisualState.Locked),
                    FrameKey = ManageArt.FrameFor(ManageTileVisualState.Locked)
                };
            }

            Report(item);

            ManageTileVisualState state = VisualStateFor(item.Badge);
            ManageAction running = FirstRunning(item);

            return new ManageTileVM
            {
                Id = item.ItemId,
                Title = item.DisplayName,
                Subtitle = item.MaxLevel > 0 && item.Level > 0 ? "LEVEL " + item.Level : null,
                PortraitKey = item.IconId,
                IsSelected = isSelected,
                VisualState = state,
                StateText = item.BadgeText,
                StateIconKey = ManageArt.StatusFor(state),
                FrameKey = ManageArt.FrameFor(state),
                Progress01 = running != null ? (float?)running.Progress01 : null,
                TimerText = running != null ? FormatDuration(running.RemainingSeconds) : null,
                Activate = onSelect
            };
        }

        // ── the selected-item card ────────────────────────────────────────────

        /// <summary>
        /// Projects an item onto the selection card. The composer supplies the two sentences
        /// that are its own to write (<paramref name="description"/> and any
        /// <paramref name="auxiliaryText"/>) plus the stat and cost rows; everything else
        /// comes off the item.
        /// </summary>
        public static ManageSelectionVM ProjectSelection(
            ManageItemState item,
            string description,
            IReadOnlyList<ManageStatVM> stats,
            IReadOnlyList<ManageCostVM> costs,
            Action<ManageRoute> navigate,
            string auxiliaryText = null)
        {
            if (item == null)
            {
                return new ManageSelectionVM { Visible = false, EmptyText = null };
            }

            Report(item);

            ManageTileVisualState state = VisualStateFor(item.Badge);
            ManageAction primary = item.PrimaryAction;
            ManageAction running = FirstRunning(item);

            // The requirement CTA is the FIRST blocked action that has a door. It is a separate
            // slot from the primary so the player can always see the exit even when the primary
            // face is a priced action they cannot pay for (canon 11 question 7).
            ManageAction blockedWithDoor = FirstBlockedWithRoute(item);

            var vm = new ManageSelectionVM
            {
                Visible = true,
                Title = item.DisplayName,
                // ⭐ EVERY DETAIL PANEL IN THE MOCKUP SHOWS A LEVEL UNDER THE NAME - panel 3
                // "Level 2", panel 5 "Level 1", panel 9 "Level 1". The capture showed troops with
                // NO level line at all, and the cause was this expression: it emitted a line only
                // when a CEILING was known, and ComposeTroopItem sets MaxLevel = 0 on purpose
                // ("TroopChoiceVM authors no ceiling, and asserting one here would be a second
                // reading of a ladder this VM does not own"). That reasoning is right and stands -
                // so the fallback states the level WITHOUT inventing a maximum.
                // ⛔ RESEARCH IS EXCLUDED, and deliberately: ManageResearchCardRegression's
                // [no-level-zero] case records that research has no level, and its items project
                // UpgradeTrack.NotApplicable. Gating on the TRACK rather than on the level keeps
                // that true instead of relying on a perk happening to have Level 0.
                // Case matches the mockup ("Level 2"), not the old shouted "LEVEL 2 OF 6".
                LevelText = item.MaxLevel > 0
                    ? "Level " + item.Level + " of " + item.MaxLevel
                    : (item.Level > 0 && item.UpgradeTrack != ManageUpgradeTrack.NotApplicable
                        ? "Level " + item.Level
                        : null),
                Description = description,
                State = state,
                StateText = item.BadgeText,
                StateIconKey = ManageArt.StatusFor(state),
                PortraitKey = item.IconId,
                Stats = stats ?? Array.Empty<ManageStatVM>(),
                Costs = costs ?? Array.Empty<ManageCostVM>(),
                PrimaryAction = ProjectAction(primary, navigate),
                SecondaryAction = ProjectAction(item.ActionOf(ManageActionKind.Cancel), navigate),
                RequirementAction = blockedWithDoor != null && blockedWithDoor != primary
                    ? ProjectAction(blockedWithDoor, navigate)
                    : ManageActionVM.Hidden,
                Progress = running != null ? (float?)running.Progress01 : null,
                ProgressText = running != null ? FormatDuration(running.RemainingSeconds) : null,
                AuxiliaryText = auxiliaryText
            };

            // "What changes next" (canon 11 question 3) is authored on the item; surface it as
            // the auxiliary line when the composer did not supply one of its own.
            if (string.IsNullOrEmpty(vm.AuxiliaryText)) vm.AuxiliaryText = item.NextRungLine;

            // The lock sentence belongs to a NotUnlocked item (ruling 15 forbids it on an owned
            // one, and the validator enforces that), so it is safe to prefer it here.
            if (item.Ownership == ManageOwnership.NotUnlocked && !string.IsNullOrEmpty(item.LockReason))
                vm.AuxiliaryText = item.LockReason;

            return vm;
        }

        // ── helpers ───────────────────────────────────────────────────────────

        private static ManageAction FirstRunning(ManageItemState item)
        {
            for (int i = 0; i < item.Actions.Count; i++)
            {
                var a = item.Actions[i];
                if (a != null && a.Availability == ManageActionAvailability.InProgress) return a;
            }
            return null;
        }

        private static ManageAction FirstBlockedWithRoute(ManageItemState item)
        {
            for (int i = 0; i < item.Actions.Count; i++)
            {
                var a = item.Actions[i];
                if (a == null || !a.Route.IsRoutable) continue;
                if (a.Availability == ManageActionAvailability.PrerequisiteBlocked ||
                    a.Availability == ManageActionAvailability.QueueBlocked ||
                    a.Availability == ManageActionAvailability.Unaffordable) return a;
            }
            return null;
        }

        /// <summary>
        /// Runs the Wave-0 validator and announces every violation. Never throws, never
        /// swallows, never suppresses the tile - a flagged tile the player can see beats a
        /// missing tile nobody can diagnose.
        /// </summary>
        private static void Report(ManageItemState item)
        {
            var failures = new List<string>();
            if (ManageStateInvariants.Validate(item, failures)) return;
            for (int i = 0; i < failures.Count; i++)
                FlowTrace.Warn("Manage", "projection saw an invalid item state: " + failures[i]);
        }

        /// <summary>
        /// ASCII countdown words. MODEL-SIDE ON PURPOSE: canon 9 forbids the VIEW deriving
        /// text, and a duration is derived text. It lives here so all three tabs read the same
        /// countdown grammar rather than three near-identical formatters drifting apart.
        /// The WO-2002 oracle bans this shape inside the renderer.
        /// </summary>
        public static string FormatDuration(float seconds)
        {
            if (seconds <= 0f) return "READY";
            int total = (int)(seconds + 0.5f);
            int hours = total / 3600;
            int minutes = (total % 3600) / 60;
            int secs = total % 60;
            if (hours > 0) return hours + "h " + minutes + "m";
            if (minutes > 0) return minutes + "m " + secs + "s";
            return secs + "s";
        }
    }
}
