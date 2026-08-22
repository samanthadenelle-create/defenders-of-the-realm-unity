// =============================================================================
// WorldHoldWatchdog — the ticker behind WorldHold's two safety nets (WO-1149).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.UI
//
// A hidden, DontDestroyOnLoad component installed lazily by WorldHold on the FIRST
// hold and never referenced by gameplay. It exists because both of WorldHold's safety
// nets need a per-frame tick, and WorldHold itself is static (deliberately — the money
// path must reach it from any assembly and any scene, with no scene object required).
//
// Its own file, matching its class name, purely to satisfy Unity's script/filename
// convention: nothing here should ever be added to a GameObject by hand.
//
// WHAT IT TICKS, and why each one is not optional:
//
//   Update  -> WorldHold.WatchdogTick()
//     Force-releases a hold that has been outstanding past WorldHold.StuckHoldSeconds.
//     This is the ONE exit a `using` declaration cannot cover: the app backgrounded
//     mid-transaction and an await that never resumes. A hold that fails to release is
//     worse than no hold — a frozen game after a completed purchase is a support ticket
//     AND a refund — so the last resort exists even though it should never fire, and it
//     fires LOUDLY (FlowTrace.Fail) rather than quietly papering over the leak.
//
//   LateUpdate -> WorldHold.ReassertTick()
//     Several combat/VFX effects also write the engine-global timeScale, and an UNSCALED
//     cleanup can finish after the freeze and stamp 1 — leaving a Paused screen, or a
//     live transaction, over running gameplay. Running LATE means those short-lived
//     owners cannot steal the lease within the frame they take it. This behaviour was
//     PauseController.LateUpdate before WO-1149 and moved here with the ownership, so it
//     now protects the transaction hold too, in scenes with no pause menu in them.
//
// Both must keep working while the world is frozen, so neither reads Time.deltaTime.
// =============================================================================

using UnityEngine;

namespace DeNelle.Core.UI
{
    /// <summary>Hidden per-frame ticker for <see cref="WorldHold"/>. Installed lazily; never
    /// added by hand.</summary>
    // ORDERED LAST (32000), inherited from PauseController when the clock-reassert moved here.
    // The reassert only works if it runs AFTER the short-lived timeScale owners it defends
    // against; a default execution order would let a VFX cleanup stamp the clock after us and
    // leave a Paused screen — or a live transaction — over running gameplay.
    [DefaultExecutionOrder(32000)]
    internal sealed class WorldHoldWatchdog : MonoBehaviour
    {
        private void Update() => WorldHold.WatchdogTick();

        private void LateUpdate() => WorldHold.ReassertTick();
    }
}
