// =============================================================================
// ManageStateInvariants - WO-2011 acceptance criteria, expressed as CODE.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Manage
//
// WO-2011's acceptance criteria are "no contradictory combination is rendered",
// "MAX does not suppress valid non-upgrade actions" and "queue-blocked has a
// first-class representation". Those are assertions about a COMBINATION of the
// three axes, so they belong next to the model as a function, not in a review
// checklist. The oracle (Assets/Editor/Regression/ManageStateModelRegression.cs)
// drives this with the four canon examples; a VM may also call it behind
// FlowTrace while a surface is being stabilised.
//
// PURE + ALLOCATION-LIGHT: returns false and fills a caller-owned list. No throw,
// no log, no service. A null item is a FAILURE, never a silent pass (CLAUDE.md 12
// forbids a swallow).
// =============================================================================

using System.Collections.Generic;

namespace DeNelle.Core.Manage
{
    /// <summary>
    /// The contradiction checks for <see cref="ManageItemState"/>. Every rule here is a
    /// sentence from the canon or an owner ruling, named in its own failure text so a RED
    /// result tells the reader which ruling it broke.
    /// </summary>
    public static class ManageStateInvariants
    {
        /// <summary>
        /// True when <paramref name="item"/> holds no contradictory combination. Appends one
        /// ASCII sentence per violation to <paramref name="failures"/>.
        ///
        /// <para>A null <paramref name="failures"/> is allowed (a caller who only wants the
        /// verdict) and the rules STILL RUN against a local list. An earlier draft short-circuited
        /// to <c>item != null</c> on a null list, which would have returned GREEN for every
        /// contradictory item a VM handed it - a silent pass inside the very file that exists to
        /// stop silent passes (CLAUDE.md §12).</para>
        /// </summary>
        public static bool Validate(ManageItemState item, List<string> failures)
        {
            if (failures == null) failures = new List<string>();
            int before = failures.Count;

            if (item == null)
            {
                failures.Add("[manage-state] a null ManageItemState reached the View - a tile with no state is the " +
                             "reverse-engineering canon 9 forbids");
                return false;
            }

            string id = string.IsNullOrEmpty(item.ItemId) ? "<no id>" : item.ItemId;

            // ── canon 9 / ruling 16: the View may not invent copy. ──
            if (string.IsNullOrEmpty(item.DisplayName))
                failures.Add("[manage-state] " + id + " has no DisplayName - the View would have to derive a label " +
                             "from the id, which canon 9 forbids");
            if (item.Badge != ManageTileBadge.None && string.IsNullOrEmpty(item.BadgeText))
                failures.Add("[manage-state] " + id + " sets Badge=" + item.Badge + " with no BadgeText - the View " +
                             "would have to derive words from the enum name, which canon 9 forbids");
            if (item.Badge == ManageTileBadge.None)
                failures.Add("[manage-badge-mandatory] " + id + " carries no tile badge. Canon 8: every BUILD and " +
                             "ARMY tile shows one actionable state indicator supplied by the model");

            // ── ruling 15: a built thing whose NEXT upgrade is gated is NOT locked. ──
            if (item.Ownership == ManageOwnership.Owned && item.Badge == ManageTileBadge.Locked)
                failures.Add("[owned-is-not-locked] " + id + " is Owned but badged Locked. Owner ruling 15: the item " +
                             "is owned and operating - gate the upgrade ACTION, never the item");
            if (item.Ownership == ManageOwnership.Owned && !string.IsNullOrEmpty(item.LockReason))
                failures.Add("[owned-is-not-locked] " + id + " is Owned but carries a LockReason ('" + item.LockReason +
                             "'). A blocked action's sentence belongs on the ACTION (ruling 15)");
            if (item.Ownership == ManageOwnership.NotUnlocked && string.IsNullOrEmpty(item.LockReason))
                failures.Add("[locked-needs-a-reason] " + id + " is NotUnlocked with no LockReason - the player is " +
                             "told no and never told why (canon 11 question 6)");

            // ── ruling 13: MAX belongs to the TRACK. ──
            var upgrade = item.ActionOf(ManageActionKind.Upgrade);
            if (item.UpgradeTrack == ManageUpgradeTrack.Max && upgrade != null &&
                upgrade.Availability != ManageActionAvailability.NotApplicable &&
                upgrade.Availability != ManageActionAvailability.InProgress)
                failures.Add("[max-is-track-only] " + id + " is at Max but its Upgrade action reports " +
                             upgrade.Availability + ". At max the upgrade action is NotApplicable - it is not " +
                             "Unaffordable and not blocked (ruling 13)");
            if (item.UpgradeTrack == ManageUpgradeTrack.Max)
            {
                // The criterion stated literally: MAX must not suppress valid non-upgrade actions.
                //
                // Scoped to the PRODUCING kinds (Build / Train / Research). Cancel, InstantFinish
                // and Navigate are legitimately NotApplicable whenever nothing is running or there
                // is nowhere to go - a maxed, idle Footman composed with a uniform Cancel action is
                // CORRECT, and flagging it would make this rule fire on every well-formed tile.
                for (int i = 0; i < item.Actions.Count; i++)
                {
                    var a = item.Actions[i];
                    if (a == null || !IsProducingKind(a.Kind)) continue;
                    if (a.Availability == ManageActionAvailability.NotApplicable)
                        failures.Add("[max-suppressed-an-action] " + id + " is at Max and its " + a.Kind +
                                     " action was switched off. MAX is a property of the upgrade track only " +
                                     "(ruling 13) - a maxed troop is still trainable");
                }
            }
            if (item.UpgradeTrack == ManageUpgradeTrack.NotApplicable && item.MaxLevel > 1)
                failures.Add("[track-mismatch] " + id + " reports no upgrade track but authors MaxLevel=" +
                             item.MaxLevel + " - two readings of the same ladder");
            if (item.UpgradeTrack == ManageUpgradeTrack.Upgradable && item.MaxLevel > 0 && item.Level >= item.MaxLevel)
                failures.Add("[track-mismatch] " + id + " reports Upgradable at Level=" + item.Level +
                             " of MaxLevel=" + item.MaxLevel + " - there is no rung above it");
            if (item.UpgradeTrack == ManageUpgradeTrack.Max && item.MaxLevel > 0 && item.Level < item.MaxLevel)
                failures.Add("[track-mismatch] " + id + " reports Max at Level=" + item.Level + " of MaxLevel=" +
                             item.MaxLevel);

            // ── per-action rules ──
            int primaries = 0;
            for (int i = 0; i < item.Actions.Count; i++)
                ValidateAction(item, item.Actions[i], id, failures, ref primaries);

            if (primaries > 1)
                failures.Add("[one-primary] " + id + " marks " + primaries + " actions IsPrimary - the tile cannot " +
                             "choose, and choosing is not the View's job (canon 9)");

            return failures.Count == before;
        }

        /// <summary>
        /// The action kinds that PRODUCE something and so must survive a maxed upgrade track
        /// (ruling 13). Cancel / InstantFinish / Navigate are situational by nature and are not
        /// evidence of suppression when absent.
        /// </summary>
        private static bool IsProducingKind(ManageActionKind kind) =>
            kind == ManageActionKind.Build ||
            kind == ManageActionKind.Train ||
            kind == ManageActionKind.Research;

        private static void ValidateAction(ManageItemState item, ManageAction a, string id,
                                           List<string> failures, ref int primaries)
        {
            if (a == null)
            {
                failures.Add("[manage-state] " + id + " carries a null action - the View would read it as absent, " +
                             "which is inference (canon 9)");
                return;
            }
            if (a.IsPrimary) primaries++;

            bool blocked = a.Availability == ManageActionAvailability.Unaffordable ||
                           a.Availability == ManageActionAvailability.PrerequisiteBlocked ||
                           a.Availability == ManageActionAvailability.QueueBlocked;

            if (a.Availability != ManageActionAvailability.NotApplicable && string.IsNullOrEmpty(a.Cta))
                failures.Add("[action-needs-words] " + id + ":" + a.Kind + " is " + a.Availability + " with no Cta - " +
                             "the View would have to invent the button text (canon 9)");

            if (blocked && string.IsNullOrEmpty(a.BlockerReason))
                failures.Add("[blocked-needs-a-reason] " + id + ":" + a.Kind + " is " + a.Availability +
                             " with no BlockerReason. Canon 11 question 6: if I cannot act, why?");

            if (!blocked && a.Availability != ManageActionAvailability.NotApplicable &&
                !string.IsNullOrEmpty(a.BlockerReason))
                failures.Add("[contradiction] " + id + ":" + a.Kind + " is " + a.Availability +
                             " yet carries a BlockerReason ('" + a.BlockerReason + "')");

            // Ruling 18 - direct prerequisite navigation is P0. A lock without a route is the
            // defect the whole program exists to kill; the barracks CTA pointed at a phantom.
            if (a.Availability == ManageActionAvailability.PrerequisiteBlocked && !a.Route.IsRoutable)
                failures.Add("[lock-without-a-door] " + id + ":" + a.Kind + " is PrerequisiteBlocked with " +
                             "Route.None. Owner ruling 18: every blocker must name a destination that opens");

            // Ruling 14 - queue-blocked is first class, and the Queue is its honest destination.
            if (a.Availability == ManageActionAvailability.QueueBlocked && a.Route.Kind != ManageRouteKind.Queue)
                failures.Add("[queue-blocked-routes-to-the-queue] " + id + ":" + a.Kind + " is QueueBlocked but " +
                             "routes to " + a.Route.Kind + ". Ruling 14 + 17: the global Queue is where a full " +
                             "line is resolved");

            if (a.Route.IsRoutable && string.IsNullOrEmpty(a.Route.Cta))
                failures.Add("[route-needs-words] " + id + ":" + a.Kind + " routes to " + a.Route.Kind +
                             " with no Cta - the View would have to name the door itself");

            if (a.Availability == ManageActionAvailability.InProgress)
            {
                if (a.Progress01 < 0f || a.Progress01 > 1f)
                    failures.Add("[progress-range] " + id + ":" + a.Kind + " reports Progress01=" + a.Progress01);
            }
            else if (a.Progress01 != 0f || a.RemainingSeconds != 0f)
            {
                failures.Add("[contradiction] " + id + ":" + a.Kind + " is " + a.Availability + " yet carries live " +
                             "progress (" + a.Progress01 + " / " + a.RemainingSeconds + "s). Only InProgress does");
            }

            // A NotUnlocked item cannot offer a runnable action on itself. Navigation is the exception -
            // that IS the door out (ruling 18).
            if (item.Ownership == ManageOwnership.NotUnlocked &&
                a.Availability == ManageActionAvailability.Available &&
                a.Kind != ManageActionKind.Navigate && a.Kind != ManageActionKind.Build)
                failures.Add("[contradiction] " + id + " is NotUnlocked yet offers " + a.Kind +
                             " as Available - the player would tap into a refusal");

            if (item.Ownership == ManageOwnership.Unavailable &&
                a.Availability != ManageActionAvailability.NotApplicable)
                failures.Add("[contradiction] " + id + " is Unavailable (not offered in this build) yet exposes a " +
                             a.Kind + " action reading " + a.Availability);
        }
    }
}
