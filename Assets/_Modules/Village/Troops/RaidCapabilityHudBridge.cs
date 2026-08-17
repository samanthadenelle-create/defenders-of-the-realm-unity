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
