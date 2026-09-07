// =============================================================================
// VillageBridgeService — the DeNelle.Village side of the Core seam (WO-1510).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Implements DeNelle.Core.Bridging.IVillageBridge and registers it into
// CoreServices, so DeNelle.Core stops naming Village types by string. Everything
// below is a COMPILER-CHECKED call: rename HeroLocomotion.WarpTo or
// WaveManager.OnWaveCleared and this file fails to build, where the old
// Type.GetType path would have returned null and severed the seam in silence
// (CLAUDE.md §12 — no silent failures).
//
// WHY A RuntimeInitializeOnLoadMethod AND NOT A MonoBehaviour: every consumer
// (SceneRouter's scene-loaded handler, PersistenceBridge, the F8 watchdog) can run
// before any Village scene object has Awoken, and the bridge holds NO state that
// belongs to a scene — it resolves live objects on each call. Installing before
// the first scene load means the slot is never transiently null while Village is
// loaded. In a build without DeNelle.Village the hook never runs, the slot stays
// null, and callers take their existing null path.
// =============================================================================

using DeNelle.Core;
using DeNelle.Core.Bridging;
using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.Events;

namespace DeNelle.Village
{
    /// <summary>
    /// Village-side implementation of <see cref="IVillageBridge"/>. Stateless apart from the
    /// WaveManager it is currently subscribed to.
    /// </summary>
    public sealed class VillageBridgeService : IVillageBridge
    {
        private static VillageBridgeService _instance;

        private WaveManager _subscribedWaves;

        /// <summary>Installs the bridge into <see cref="CoreServices"/> before the first scene loads.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Install()
        {
            if (_instance == null) _instance = new VillageBridgeService();
            CoreServices.RegisterVillageBridge(_instance);
            FlowTrace.Step("VillageBridge", "IVillageBridge installed — Core no longer reflects into DeNelle.Village.");
        }

        // ── Hero ──────────────────────────────────────────────────────────────

        /// <inheritdoc />
        public GameObject FindHeroObject()
        {
            var loco = Object.FindAnyObjectByType<HeroLocomotion>();
            return loco != null ? loco.gameObject : null;
        }

        /// <inheritdoc />
        public bool HasHero(GameObject candidate)
        {
            return candidate != null && candidate.GetComponent<HeroLocomotion>() != null;
        }

        /// <inheritdoc />
        public bool WarpHero(GameObject hero, Vector3 position, Quaternion rotation)
        {
            if (hero == null) return false;
            var loco = hero.GetComponent<HeroLocomotion>();
            if (loco == null) return false;

            // Disables the agent, moves, re-warps onto the NavMesh and raises OnTeleported
            // for the camera — the whole reason Core must not do a bare transform move.
            loco.WarpTo(position, rotation);
            return true;
        }

        /// <inheritdoc />
        public bool IsHeroInputSuppressed => HeroLocomotion.InputSuppressed;

        // ── Waves ─────────────────────────────────────────────────────────────

        /// <inheritdoc />
        public WaveClearedSubscription SubscribeWaveCleared(UnityAction<int> handler)
        {
            if (handler == null) return WaveClearedSubscription.NoWaveManager;

            var wm = Object.FindAnyObjectByType<WaveManager>();
            if (wm == null) return WaveClearedSubscription.NoWaveManager;

            if (wm.OnWaveCleared == null)
            {
                // The seam is severed: a WaveManager is live but has no event to listen to.
                // Reported, never swallowed — the caller turns this into a FlowTrace.Fail.
                _subscribedWaves = null;
                return WaveClearedSubscription.EventNull;
            }

            wm.OnWaveCleared.AddListener(handler);
            _subscribedWaves = wm;
            return WaveClearedSubscription.Subscribed;
        }

        /// <inheritdoc />
        public void UnsubscribeWaveCleared(UnityAction<int> handler)
        {
            if (_subscribedWaves != null && handler != null && _subscribedWaves.OnWaveCleared != null)
                _subscribedWaves.OnWaveCleared.RemoveListener(handler);
            _subscribedWaves = null;
        }
    }
}
