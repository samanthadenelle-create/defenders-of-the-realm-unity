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
// THE PREDICATE (WO-835 §3b, single-source discipline):
//   capable = FeatureFlags.Raid
//          AND StructureSingleton.IsBuilt("barracks")      (the raid building)
//          AND ArmyReadiness.Compute(st).DeployableSlots >= 1
// Deployable count comes from ArmyReadiness.Compute — THE one army formula
// (owner review 2026-08-01, WO-823); never re-roll the math locally.
//
// NEVER-FALSE-BLOCK (WO-813/WO-820 precedent, mirrored from ArmyReadiness):
// a missing GameState/Army (headless, AutoPilot, pre-boot) publishes CAPABLE —
// absent state must never hide the raid door. A real fresh save (empty army)
// publishes NOT capable, which is exactly the owner-intended hide.
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

            bool capable = ComputeCapable(out string refuseReason);
            if (_haveLast && capable == _lastCapable) return;   // edge-triggered push
            bool wasCapable = _haveLast && _lastCapable;
            _lastCapable = capable;
            _haveLast = true;
            FlowTrace.Step("Raid", "capability edge -> " + (capable ? "CAPABLE" : "NOT CAPABLE") +
                           " (flag=" + FeatureFlags.Raid +
                           ", building=" + StructureSingleton.IsBuilt(RaidBuildingId) +
                           (string.IsNullOrEmpty(refuseReason) ? "" : ", why=" + refuseReason) + ")");
            PostureSignals.SetRaidCapable(capable);   // Core static — cannot go stale

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

        private static bool ComputeCapable() => ComputeCapable(out _);

        /// <summary>WO-932: same predicate as header, with a player-facing refuse line.</summary>
        private static bool ComputeCapable(out string refuseReason)
        {
            refuseReason = null;
            var gs = GameStateService.Instance;
            var st = gs != null ? gs.State : null;
            if (st == null || st.Army == null)
                return true;   // never-false-block: absent state must not hide the raid door

            if (!FeatureFlags.Raid)
            {
                refuseReason = "Raids are turned off in this build.";
                return false;
            }
            if (!StructureSingleton.IsBuilt(RaidBuildingId))
            {
                refuseReason = "Build a Barracks and train troops to unlock Raids.";
                return false;
            }

            // THE one army formula (ArmyReadiness, WO-823) — >=1 deployable slot
            // means at least one healthy troop exists.
            if (ArmyReadiness.Compute(st).DeployableSlots < 1)
            {
                refuseReason = "Train at least one troop at the Barracks to unlock Raids.";
                return false;
            }
            return true;
        }
    }
}
