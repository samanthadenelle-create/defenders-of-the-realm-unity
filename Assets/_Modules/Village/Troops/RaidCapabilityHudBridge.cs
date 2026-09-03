// =============================================================================
// RaidCapabilityHudBridge — publishes "the player CAN raid" into the Core signal
// PostureSignals.RaidCapable (WO-835 — the TalkHudBridge / SetTalkAvailable
// mirror pattern: Village writes, Core holds, HUD reads).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHY: WO-835 hides the HUD Raids face entirely when the player cannot raid
// (owner 2026-08-02: "if they cannot do raids — no troops or the building — no
// reason to confuse them with the button"). The applicability decision lives in
// the Core HudActionBarModel, but the FACTS are Village-side, so this bridge
// mirrors them into Core (DeNelle.HUD references Core ONLY — CLAUDE.md §5).
//
// THE PREDICATE (WO-835 §3b, as AMENDED by WO-1008 2026-08-16):
//   capable = FeatureFlags.Raid
//          AND StructureSingleton.IsBuilt("barracks")      (the raid building)
//
// ⚠ THE THIRD CLAUSE IS GONE ON PURPOSE. It used to read
//   AND ArmyReadiness.Compute(st).DeployableSlots >= 1
// and it cost the owner a session: a built Barracks with an empty army rendered NO
// Raids face at all, so she reported "I do not see a way to start a raid". Owner ask,
// verbatim: "can we add a greyed out option once we have a barracks with build troops
// to raid". Troop count is now a DIM REASON on a VISIBLE face
// (HudActionBarModel.RaidDimReason.NoTroops), not a hide reason. Do not restore it —
// RaidsDiscoverabilityRegression fails the build if it comes back.
//
// NEVER-FALSE-BLOCK (WO-813/WO-820 precedent, mirrored from ArmyReadiness):
// a missing GameState/Army (headless, AutoPilot, pre-boot) publishes CAPABLE —
// absent state must never hide the raid door. Post-WO-1008 a real fresh save with a
// Barracks ALSO publishes CAPABLE (and dims); only "no Barracks" / "flag off" hide.
//
// Distinct from the WO-820 FULL-ARMY gate: RaidEntryGate.ArmyStatus.Ready still
// DIMS a visible Raids face (capable but not full — tap redirects to the
// drillmaster). This bridge only decides whether the face exists at all.
//
// Edge-triggered 0.5 s poll (the TalkHudBridge cadence discipline): the reads
// are cheap (per-frame-memoized IsBuilt + a small roster walk); the Core push
// fires only on transitions.
//
// WO-932 Phase 1: when the face HIDES (not capable), toast ONCE per session with
// a concrete unlock line so a fresh save is not a silent empty HUD.
//
// WO-1357 (owner 2026-09-03: "Raid button under journey should fail gracefully...
// should show locked if doesnt have one yet or its destroyed"): this bridge now also
// publishes a PostureSignals.RaidLockReason so the Journey deck card can say WHY, and
// the Journey card reads THIS predicate instead of the `Available = () => true` it
// carried. ⛔ THE PREDICATE'S BOUNDARY DID NOT MOVE — WO-1357 added an out-parameter,
// never a clause. Two documented boundaries, both deliberate, both LEFT ALONE:
//   * UNDER CONSTRUCTION / QUEUED counts as CAPABLE. BuildModeController.Place spawns
//     the structure and appends its BaseLayout record BEFORE the build timer starts
//     (BuildModeController.cs:2071), so a barracks mid-build has always read IsBuilt.
//     Locking it would change the working path the owner fenced off. Flagged to her as
//     a design call, not silently altered.
//   * A RESURFACED BAKED TWIN counts as CAPABLE. After a WO-753 destruction the WO-819
//     stand-in barracks re-activates, so IsBuilt clause 2 can hold while the build card
//     correctly reads BUILDABLE (StructureSingleton.IsPlayerBuilt). That asymmetry is
//     ON PURPOSE here: a barracks is visibly STANDING in the world, and locking raids in
//     front of it would be the more confusing outcome. Do NOT "fix" this to IsPlayerBuilt
//     without the owner — on a pre-handover Default-Town save the founding barracks has
//     no placement record yet, so IsPlayerBuilt would lock raids for a player who has one.
// =============================================================================

using UnityEngine;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.HudModel;
using DeNelle.Core.State;
using DeNelle.Core.UI;

namespace DeNelle.Village
{
    /// <summary>Pushes raid capability (building + troops + flag) into Core (see header).</summary>
    public sealed class RaidCapabilityHudBridge : MonoBehaviour
    {
        private const float PollInterval = 0.5f;
        private const string RaidBuildingId = "barracks";   // StructureSingleton id (BarracksSystemInjector precedent)

        private float _timer;          // 0 on spawn/scene load -> first Update publishes immediately
        private bool _lastCapable;
        // WO-1357: the edge is (capable, reason), not capable alone. NoBarracks -> BarracksLost
        // never flips the bool, but the Journey card's sentence has to change with it.
        private PostureSignals.RaidLockReason _lastLock = PostureSignals.RaidLockReason.None;
        private bool _haveLast;
        // WO-932: one teach toast per process for "why is Raids missing".
        private static bool s_unlockTeachToasted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("RaidCapabilityHudBridge");
            DontDestroyOnLoad(go);
            go.AddComponent<RaidCapabilityHudBridge>();
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = PollInterval;

            bool capable = ComputeCapable(out string refuseReason, out var lockReason);
            if (_haveLast && capable == _lastCapable && lockReason == _lastLock) return;   // edge-triggered push
            bool wasCapable = _haveLast && _lastCapable;
            _lastCapable = capable;
            _lastLock = lockReason;
            _haveLast = true;
            FlowTrace.Step("Raid", "capability edge -> " + (capable ? "CAPABLE" : "NOT CAPABLE") +
                           " (flag=" + FeatureFlags.Raid +
                           ", building=" + StructureSingleton.IsBuilt(RaidBuildingId) +
                           ", lock=" + lockReason +
                           (string.IsNullOrEmpty(refuseReason) ? "" : ", why=" + refuseReason) + ")");
            PostureSignals.SetRaidCapable(capable, lockReason);   // Core static — cannot go stale

            // WO-932: teach unlock when we first learn the player cannot raid (or lose capability).
            if (!capable && !s_unlockTeachToasted && !string.IsNullOrEmpty(refuseReason))
            {
                // Skip the never-false-block headless path (refuseReason empty when st null).
                s_unlockTeachToasted = true;
                ElarionUiKit.ShowToast(refuseReason, ElarionUiKit.ToastTone.Info);
            }
            else if (capable && wasCapable == false)
            {
                // Optional positive edge — quiet, once-ish: capability regained.
                FlowTrace.Step("Raid", "capability restored — Raids face should appear on the bar.");
            }
        }

        private static bool ComputeCapable() => ComputeCapable(out _, out _);

        /// <summary>
        /// WO-932: same predicate as header, with a player-facing refuse line.
        /// WO-1357 adds the machine-readable <paramref name="lockReason"/> that travels with
        /// it into Core, so EVERY surface (bar face, Journey card) explains the same shut door
        /// in the same words. ⛔ THE CAPABLE/NOT-CAPABLE BOUNDARY IS BIT-IDENTICAL TO BEFORE
        /// WO-1357 — the owner's fence is "it works great if there is a barracks", so this
        /// method gained an out-parameter and NOT a clause. Anything that returned true still
        /// returns true.
        /// </summary>
        private static bool ComputeCapable(out string refuseReason,
                                           out PostureSignals.RaidLockReason lockReason)
        {
            refuseReason = null;
            lockReason = PostureSignals.RaidLockReason.None;
            var gs = GameStateService.Instance;
            var st = gs != null ? gs.State : null;
            if (st == null || st.Army == null)
                return true;   // never-false-block: absent state must not hide the raid door

            if (!FeatureFlags.Raid)
            {
                refuseReason = "Raids are turned off in this build.";
                lockReason = PostureSignals.RaidLockReason.FlagOff;
                return false;
            }
            if (!StructureSingleton.IsBuilt(RaidBuildingId))
            {
                // WO-1357 — "doesnt have one yet" vs "or its destroyed" are DIFFERENT player
                // situations with different remedies, and only the save can tell them apart.
                //
                // WHAT A DESTROYED STRUCTURE LOOKS LIKE AT RUNTIME (read at source, WO-753):
                // it is GONE, not flagged. Destructible.NotifyBroken frees the footprint, calls
                // BaseLayoutLoader.Forget, DROPS the persisted BaseLayout record, burns the
                // free-build, and Destroy()s the GameObject. Building.IsDestroyed exists only
                // for the single frame between hp0 and that Destroy, and IsBuilt clause 4 already
                // demands IsAlive — so there is no lingering wreck for an existence check to
                // trip over. IsBuilt therefore ALREADY goes false on destruction; what it cannot
                // do is say WHY, because "never had one" looks identical to "had one, lost it".
                //
                // GameState.EverBuiltStructureIds (v36, WO-834) is the discriminator and it is
                // MONOTONIC — selling or losing a structure never removes its id — which is
                // exactly the "you have owned one before" question we need. No new state.
                bool everHadOne = st.HasEverBuilt(RaidBuildingId);
                lockReason = everHadOne
                    ? PostureSignals.RaidLockReason.BarracksLost
                    : PostureSignals.RaidLockReason.NoBarracks;
                refuseReason = everHadOne
                    ? "Your Barracks is gone - rebuild it at full cost to raid again."
                    : "Build a Barracks and train troops to unlock Raids.";
                return false;
            }

            // ⚠ WO-1008 (owner ask 2026-08-16, "can we add a greyed out option once we have a
            // barracks with build troops to raid"): the old third clause
            //     ArmyReadiness.Compute(st).DeployableSlots >= 1
            // is DELETED FROM VISIBILITY ON PURPOSE. Do not restore it. The owner played a save
            // with a built Barracks and an empty army; the Raids face was completely ABSENT and
            // she reported "I do not see a way to start a raid" — a feature that hides itself is
            // indistinguishable from a broken one. Zero troops is now a DIMMED, WORDED state
            // (HudActionBarModel.RaidDimReason.NoTroops -> face reads "Raids 0/N", tap toasts
            // "train troops at the Barracks"), reusing the existing WO-820 dim mechanism rather
            // than inventing a second one.
            //
            // NOTHING IS WEAKENED: RaidSelectionScreen.Open still recomputes ArmyReadiness and
            // still refuses + redirects to the drillmaster. Only the LEGIBILITY of the rule
            // changed, never the rule.
            FlowTrace.Once("Raid", "wo1008-capable",
                "capability = flag + barracks ONLY (WO-1008). Troop count is a DIM reason on a " +
                "visible face, never a hide reason.");
            return true;
        }
    }
}
