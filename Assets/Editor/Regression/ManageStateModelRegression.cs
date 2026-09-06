// =============================================================================
// ManageStateModelRegression [manage-state-model] -- WO-2011.
// -----------------------------------------------------------------------------
// WO-2011's acceptance criteria are assertions about COMBINATIONS of three axes, so
// they are executable, not a review checklist:
//   * all existing captured edge cases map cleanly
//   * no contradictory combination is rendered
//   * MAX does not suppress valid non-upgrade actions
//   * queue-blocked has a first-class representation
//
// This suite builds the FOUR CANON EXAMPLES from the work order as fixtures and drives
// DeNelle.Core.Manage.ManageStateInvariants.Validate over them, then drives a set of
// deliberately CONTRADICTORY fixtures and asserts the validator catches each one. A
// validator nobody has seen fail is not evidence (CLI_DRIVING_PLAN.md section 3), so the
// negative half is as load-bearing as the positive half and lives in the same file.
//
// It tests the CONTRACT, not a screen: DeNelle.Core.Manage has no game-rule dependency,
// so this suite needs no GameState, no catalog and no scene. The VMs that populate the
// contract are tested where they live.
//
// Marker: MANAGE_STATE_MODEL_OK / MANAGE_STATE_MODEL_FAIL. Expected: GREEN.
//
// Wire (DataRegression.RunAll):
//   DeNelle.Core.Diagnostics.Guard.Try("Regression", "manage-state-model suite", () => { if (!DeNelle.Editor.ManageStateModelRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[manage-state-model] " + r); });
// =============================================================================
using System;
using System.Collections.Generic;
using System.Text;
using DeNelle.Core.Manage;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class ManageStateModelRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder("=== ManageStateModelRegression (WO-2011) ===\n");
            try
            {
                CheckCanonExamples(failures, log);
                CheckContradictionsAreCaught(failures, log);
            }
            catch (Exception ex)
            {
                failures.Add("suite threw " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "MANAGE_STATE_MODEL_OK the four WO-2011 canon examples validate clean and every " +
                         "authored contradiction is caught (ownership / upgrade-track / action state stay separable)";
                Debug.Log(reason + "\n" + log);
                return true;
            }

            reason = "MANAGE_STATE_MODEL_FAIL " + string.Join(" | ", failures);
            Debug.LogError(reason + "\n" + log);
            return false;
        }

        // ── CASE 1  [canon-examples] ─────────────────────────────────────────
        // The four examples WO-2011 lists under "Canon examples" ARE the acceptance criteria.
        // Each must validate clean AND report the exact axis values the work order states.
        //
        // REVERT RECIPE (RED): in ManageStateInvariants, change the [owned-is-not-locked] rule to
        // also fire when Ownership==Owned and any action is PrerequisiteBlocked -- the Lumber Mill
        // example (owner ruling 15: gate the ACTION, not the item) then fails and this case names it.
        private static void CheckCanonExamples(List<string> failures, StringBuilder log)
        {
            // -- "Built Lumber Mill, next upgrade Heart-gated": owned + upgradable + upgrade
            //    action PrerequisiteBlocked, CTA VIEW HEART. The item is NOT locked (ruling 15).
            var mill = new ManageItemState
            {
                ItemId = "lumbermill",
                DisplayName = "Lumber Mill",
                Ownership = ManageOwnership.Owned,
                UpgradeTrack = ManageUpgradeTrack.Upgradable,
                Level = 2,
                MaxLevel = 4,
                Badge = ManageTileBadge.Idle,
                BadgeText = "Operating"
            };
            mill.Add(new ManageAction
            {
                Kind = ManageActionKind.Upgrade,
                Availability = ManageActionAvailability.PrerequisiteBlocked,
                Cta = "VIEW HEART",
                BlockerReason = "Raise the Heart to Level 3 first.",
                Route = ManageRoute.ToHeart(),
                IsPrimary = true
            });
            Expect(mill, failures, log, "built Lumber Mill, next upgrade Heart-gated");
            if (mill.Ownership != ManageOwnership.Owned || mill.Badge == ManageTileBadge.Locked)
                failures.Add("[canon-examples] the Lumber Mill fixture is not Owned-and-not-Locked");
            if (mill.PrimaryAction == null || mill.PrimaryAction.Route.Kind != ManageRouteKind.HeartCard)
                failures.Add("[canon-examples] the Lumber Mill's blocked upgrade must route to the Heart (ruling 18)");

            // -- "Max-level Footman, train queue open": owned + MAX track + Train Available.
            //    MAX must not suppress training (ruling 13).
            var footmanOpen = MaxFootman();
            footmanOpen.Add(new ManageAction
            {
                Kind = ManageActionKind.Train,
                Availability = ManageActionAvailability.Available,
                Cta = "TRAIN",
                IsPrimary = true
            });
            Expect(footmanOpen, failures, log, "max-level Footman, train queue open");
            var train = footmanOpen.ActionOf(ManageActionKind.Train);
            if (train == null || train.Availability != ManageActionAvailability.Available)
                failures.Add("[max-does-not-suppress] a maxed Footman must still be trainable (ruling 13)");

            // -- "Max-level Footman, train queue full": Train QueueBlocked, CTA VIEW QUEUE.
            //    Queue-blocked is a first-class state with the Queue as its destination (rulings 14/17).
            var footmanFull = MaxFootman();
            footmanFull.Badge = ManageTileBadge.QueueBlocked;
            footmanFull.BadgeText = "Queue full";
            footmanFull.Add(new ManageAction
            {
                Kind = ManageActionKind.Train,
                Availability = ManageActionAvailability.QueueBlocked,
                Cta = "VIEW QUEUE",
                BlockerReason = "The Train line is full.",
                Route = ManageRoute.ToQueue(),
                IsPrimary = true
            });
            Expect(footmanFull, failures, log, "max-level Footman, train queue full");

            // -- A maxed, IDLE Footman composed with a uniform Cancel/InstantFinish pair. Those are
            //    legitimately NotApplicable when nothing is running; [max-suppressed-an-action] is
            //    scoped to the PRODUCING kinds precisely so a well-formed tile like this validates.
            //    Without this fixture the rule would fire on every VM that composes actions
            //    uniformly, which is how an over-strict oracle gets waived instead of trusted.
            var footmanIdle = MaxFootman();
            footmanIdle.Add(new ManageAction
            {
                Kind = ManageActionKind.Train, Availability = ManageActionAvailability.Available,
                Cta = "TRAIN", IsPrimary = true
            });
            footmanIdle.Add(ManageAction.NotApplicable(ManageActionKind.Cancel));
            footmanIdle.Add(ManageAction.NotApplicable(ManageActionKind.InstantFinish));
            Expect(footmanIdle, failures, log, "max-level idle Footman with uniform Cancel/InstantFinish actions");

            // -- "Locked Outrider": not unlocked + Train PrerequisiteBlocked, CTA VIEW BARRACKS.
            //    Owner ruling 21 is what makes that door genuinely open: the barracks BUILDING card
            //    in BUILD already exists and already works, and the building tier is now the gate.
            var outrider = new ManageItemState
            {
                ItemId = "troop-outrider",
                DisplayName = "Outrider",
                Ownership = ManageOwnership.NotUnlocked,
                UpgradeTrack = ManageUpgradeTrack.NotApplicable,
                Badge = ManageTileBadge.Locked,
                BadgeText = "Locked",
                LockReason = "Unlocks at Barracks Tier 4 - Standing Army."
            };
            outrider.Add(new ManageAction
            {
                Kind = ManageActionKind.Train,
                Availability = ManageActionAvailability.PrerequisiteBlocked,
                Cta = "VIEW BARRACKS",
                BlockerReason = "Unlocks at Barracks Tier 4 - Standing Army.",
                Route = ManageRoute.ToBuildCard("barracks", "VIEW BARRACKS"),
                IsPrimary = true
            });
            Expect(outrider, failures, log, "locked Outrider");
            var otrain = outrider.ActionOf(ManageActionKind.Train);
            if (otrain == null || otrain.Route.Kind != ManageRouteKind.BuildCard || otrain.Route.TargetId != "barracks")
                failures.Add("[canon-examples] the locked Outrider must route to the barracks BUILD card - " +
                             "ruling 21 makes that the real gate, so the door opens on something that exists");
        }

        private static ManageItemState MaxFootman() => new ManageItemState
        {
            ItemId = "troop-footman",
            DisplayName = "Footman",
            Ownership = ManageOwnership.Owned,
            UpgradeTrack = ManageUpgradeTrack.Max,
            Level = 5,
            MaxLevel = 5,
            Badge = ManageTileBadge.Trainable,
            BadgeText = "Trainable"
        };

        private static void Expect(ManageItemState item, List<string> failures, StringBuilder log, string label)
        {
            var violations = new List<string>();
            if (!ManageStateInvariants.Validate(item, violations))
                failures.Add("[canon-examples] '" + label + "' is a WO-2011 canon example and must validate " +
                             "clean, but reports: " + string.Join(" ; ", violations));
            else
                log.AppendLine("clean: " + label);
        }

        // ── CASE 2  [contradictions-are-caught] ──────────────────────────────
        // Every rule the validator owns, driven RED on purpose. Without this half the suite would
        // pass just as happily against a Validate() that returns true unconditionally.
        //
        // REVERT RECIPE (RED): make ManageStateInvariants.Validate return true immediately -- every
        // entry below then reports "was NOT caught" and this case names all of them.
        private static void CheckContradictionsAreCaught(List<string> failures, StringBuilder log)
        {
            int caught = 0, total = 0;

            // Owned but badged Locked (ruling 15 - the defect the owner met on the Lumber Mill).
            var ownedLocked = new ManageItemState
            {
                ItemId = "x-owned-locked", DisplayName = "X", Ownership = ManageOwnership.Owned,
                Badge = ManageTileBadge.Locked, BadgeText = "Locked"
            };
            MustFail(ownedLocked, "an Owned item badged Locked", failures, log, ref caught, ref total);

            // MAX with a live Upgrade action (ruling 13 - MAX belongs to the track).
            var maxUpgrading = new ManageItemState
            {
                ItemId = "x-max", DisplayName = "X", Ownership = ManageOwnership.Owned,
                UpgradeTrack = ManageUpgradeTrack.Max, Level = 4, MaxLevel = 4,
                Badge = ManageTileBadge.Max, BadgeText = "Max"
            };
            maxUpgrading.Add(new ManageAction
            {
                Kind = ManageActionKind.Upgrade, Availability = ManageActionAvailability.Unaffordable,
                Cta = "UPGRADE", BlockerReason = "Need more Wood."
            });
            MustFail(maxUpgrading, "a maxed item whose Upgrade action is merely Unaffordable",
                     failures, log, ref caught, ref total);

            // MAX suppressing a non-upgrade action (the literal acceptance criterion).
            var maxSuppressed = new ManageItemState
            {
                ItemId = "x-max-suppressed", DisplayName = "X", Ownership = ManageOwnership.Owned,
                UpgradeTrack = ManageUpgradeTrack.Max, Level = 4, MaxLevel = 4,
                Badge = ManageTileBadge.Max, BadgeText = "Max"
            };
            maxSuppressed.Add(ManageAction.NotApplicable(ManageActionKind.Train));
            MustFail(maxSuppressed, "a maxed item whose Train action was switched off",
                     failures, log, ref caught, ref total);

            // PrerequisiteBlocked with no route (ruling 18 - a lock without a door).
            var noDoor = new ManageItemState
            {
                ItemId = "x-no-door", DisplayName = "X", Ownership = ManageOwnership.NotUnlocked,
                Badge = ManageTileBadge.Locked, BadgeText = "Locked", LockReason = "Not yet."
            };
            noDoor.Add(new ManageAction
            {
                Kind = ManageActionKind.Train, Availability = ManageActionAvailability.PrerequisiteBlocked,
                Cta = "LOCKED", BlockerReason = "Not yet.", Route = ManageRoute.None
            });
            MustFail(noDoor, "a PrerequisiteBlocked action with no destination", failures, log, ref caught, ref total);

            // QueueBlocked routed anywhere but the Queue (rulings 14 + 17).
            var wrongDoor = new ManageItemState
            {
                ItemId = "x-wrong-door", DisplayName = "X", Ownership = ManageOwnership.Owned,
                Badge = ManageTileBadge.QueueBlocked, BadgeText = "Queue full"
            };
            wrongDoor.Add(new ManageAction
            {
                Kind = ManageActionKind.Train, Availability = ManageActionAvailability.QueueBlocked,
                Cta = "VIEW BARRACKS", BlockerReason = "The Train line is full.",
                Route = ManageRoute.ToBuildCard("barracks", "VIEW BARRACKS")
            });
            MustFail(wrongDoor, "a QueueBlocked action routed away from the Queue", failures, log, ref caught, ref total);

            // A blocked action with no sentence (canon 11 question 6).
            var silent = new ManageItemState
            {
                ItemId = "x-silent", DisplayName = "X", Ownership = ManageOwnership.Owned,
                Badge = ManageTileBadge.UpgradeUnaffordable, BadgeText = "Cannot afford"
            };
            silent.Add(new ManageAction
            {
                Kind = ManageActionKind.Upgrade, Availability = ManageActionAvailability.Unaffordable, Cta = "UPGRADE"
            });
            MustFail(silent, "an Unaffordable action with no BlockerReason", failures, log, ref caught, ref total);

            // No badge at all (canon 8 - tile state is mandatory).
            var badgeless = new ManageItemState
            {
                ItemId = "x-badgeless", DisplayName = "X", Ownership = ManageOwnership.Owned
            };
            MustFail(badgeless, "a tile with no state badge", failures, log, ref caught, ref total);

            // Progress on an action that is not running.
            var ghostProgress = new ManageItemState
            {
                ItemId = "x-ghost", DisplayName = "X", Ownership = ManageOwnership.Owned,
                Badge = ManageTileBadge.Idle, BadgeText = "Operating"
            };
            ghostProgress.Add(new ManageAction
            {
                Kind = ManageActionKind.Upgrade, Availability = ManageActionAvailability.Available,
                Cta = "UPGRADE", Progress01 = 0.5f, RemainingSeconds = 30f
            });
            MustFail(ghostProgress, "an Available action carrying live progress", failures, log, ref caught, ref total);

            // A null item must FAIL, never pass quietly (CLAUDE.md section 12 - no silent success).
            total++;
            var nullViolations = new List<string>();
            if (ManageStateInvariants.Validate(null, nullViolations)) failures.Add(
                "[contradictions-are-caught] a null ManageItemState validated CLEAN - a missing state must be a " +
                "failure, not a pass");
            else caught++;

            log.AppendLine("contradictions caught " + caught + "/" + total);
        }

        private static void MustFail(ManageItemState item, string label, List<string> failures,
                                     StringBuilder log, ref int caught, ref int total)
        {
            total++;
            var violations = new List<string>();
            if (ManageStateInvariants.Validate(item, violations))
                failures.Add("[contradictions-are-caught] " + label + " was NOT caught by " +
                             "ManageStateInvariants.Validate - the contradiction would reach the player");
            else
                caught++;
        }
    }
}
