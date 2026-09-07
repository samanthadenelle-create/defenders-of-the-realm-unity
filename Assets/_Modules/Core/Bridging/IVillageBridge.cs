// =============================================================================
// IVillageBridge — the sanctioned Core -> Village seam (WO-1510).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Bridging
//
// WHY THIS EXISTS. Before WO-1510, four sites inside DeNelle.Core reached UP into
// DeNelle.Village by name:
//
//     SceneRouter.cs:510, 523      Type.GetType("DeNelle.Village.HeroLocomotion, DeNelle.Village")
//     PersistenceBridge.cs:174     Type.GetType("DeNelle.Village.WaveManager,    DeNelle.Village")
//     BreakCaptureHarness.cs:491   Type.GetType("DeNelle.Village.HeroLocomotion, DeNelle.Village")
//
// That is a layering INVERSION: the lower assembly naming the higher one. It buys
// nothing the compiler could not give the Village side, and it costs the three
// things CLAUDE.md §12 cares about — a rename in Village silently severs the seam,
// every failure path returns a null/false with no compiler complaint, and the
// resulting no-op looks identical to "nothing to do".
//
// THE SHAPE. Core declares the CONTRACT; DeNelle.Village implements it and
// registers through CoreServices — exactly the CoreServices.Hud / IVillageHud
// pattern (CLAUDE.md §5: "Cross-module calls go through CoreServices.*, always
// with ?."). Core now names no Village type at all, and the Village side gets
// compiler-checked members instead of string literals.
//
// NULL IS A LEGITIMATE STATE. In a Core-only context (headless boot, a scene with
// no Village assembly loaded) nothing registers and CoreServices.VillageBridge is
// null — the same "type not found" case the reflection had, but now explicit.
// Callers MUST null-check and MUST trace when the seam is absent but expected.
// =============================================================================

using UnityEngine;
using UnityEngine.Events;

namespace DeNelle.Core.Bridging
{
    /// <summary>
    /// The outcome of <see cref="IVillageBridge.SubscribeWaveCleared"/>. Three states, because
    /// the three cases mean different things and the old reflection deliberately traced them
    /// differently: no wave manager is NORMAL (most scenes have none), a present manager with a
    /// null event is a SEVERED seam (wave-clear -> backend sync silently stops).
    /// </summary>
    public enum WaveClearedSubscription
    {
        /// <summary>No WaveManager in the loaded scenes. Normal — not a failure.</summary>
        NoWaveManager = 0,

        /// <summary>Subscribed; wave-clear -> backend sync is live.</summary>
        Subscribed = 1,

        /// <summary>A WaveManager exists but its OnWaveCleared event is null. The seam is SEVERED.</summary>
        EventNull = 2,
    }

    /// <summary>
    /// Core-defined contract for the handful of DeNelle.Village facts that DeNelle.Core
    /// legitimately needs (hero pose, hero input suppression, wave-clear notification).
    /// Implemented in DeNelle.Village and registered via
    /// <see cref="CoreServices.RegisterVillageBridge"/>. Always null-check the registry slot.
    /// </summary>
    public interface IVillageBridge
    {
        /// <summary>
        /// The GameObject hosting the active hero locomotion component, or null when none is
        /// in the loaded scenes. Replaces the reflected HeroLocomotion lookup in SceneRouter.
        /// </summary>
        GameObject FindHeroObject();

        /// <summary>
        /// True when <paramref name="candidate"/> itself carries the hero locomotion component.
        /// Used by SceneRouter to prefer the 'Player'-tagged object when it is the real hero.
        /// </summary>
        bool HasHero(GameObject candidate);

        /// <summary>
        /// Warps the hero to <paramref name="position"/>/<paramref name="rotation"/> through the
        /// Village-side warp (agent disable -> move -> NavMesh re-warp -> camera event).
        /// Returns false when <paramref name="hero"/> carries no locomotion component, so the
        /// caller can fall back to a plain transform move and TRACE that it did.
        /// </summary>
        bool WarpHero(GameObject hero, Vector3 position, Quaternion rotation);

        /// <summary>
        /// True while hero input is suppressed (dialogue / cutscene / autowalk). Read by the
        /// F8 stall watchdog so a deliberately-frozen hero is not counted as a softlock.
        /// </summary>
        bool IsHeroInputSuppressed { get; }

        /// <summary>
        /// Subscribes <paramref name="handler"/> to the active WaveManager's wave-cleared event.
        /// Call <see cref="UnsubscribeWaveCleared"/> before re-subscribing on a new scene.
        /// </summary>
        WaveClearedSubscription SubscribeWaveCleared(UnityAction<int> handler);

        /// <summary>Removes a previously-subscribed wave-cleared handler. Safe to call unsubscribed.</summary>
        void UnsubscribeWaveCleared(UnityAction<int> handler);
    }
}
