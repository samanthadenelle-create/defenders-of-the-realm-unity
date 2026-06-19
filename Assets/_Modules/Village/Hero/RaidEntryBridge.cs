// =============================================================================
// RaidEntryBridge — wires the town-HUD "Raids" icon to the raid selection screen.
// (WO-457 Part 3.)
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.Hero
//
// OWNER ASK (WO-457): "no button for raid." The HUD already draws a top-right
// crossed-swords icon that raises VillageHudController.RaidRequested (a public
// UnityEvent), but nothing subscribed to it — so tapping it did nothing. This
// bridge closes that gap: it resolves the live HUD through CoreServices.Hud and,
// on RaidRequested, opens RaidSelectionScreen.
//
// CROSS-ASMDEF (CLAUDE.md §5): DeNelle.HUD must NEVER reference DeNelle.Village.
// So the HUD only FIRES the event; this Village-side subscriber turns it into the
// raid-screen open. RaidRequested is a HUD extra (not on the IVillageHud
// interface), so — exactly like StartWaveHudBridge — we reflect it by name off
// the object behind CoreServices.Hud.
//
// SELF-BOOTSTRAP: a static RuntimeInitializeOnLoadMethod ensures one bridge lives
// in every HUB scene (MainCastle_Hall / Village2 / OuterWorld), mirroring
// CameraModeControllerBootstrap. Idempotent, per-scene, WebGL-safe (guarded).
// =============================================================================

using System.Reflection;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace DeNelle.Village.Hero
{
    /// <summary>
    /// Subscribes to the HUD's <c>RaidRequested</c> event (resolved via
    /// <see cref="CoreServices.Hud"/> + reflection) and opens
    /// <see cref="RaidSelectionScreen"/> when the town-HUD raid icon is tapped.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class RaidEntryBridge : MonoBehaviour
    {
        private object _hud;
        private UnityEvent _raidRequestedEvent;
        private UnityAction _onRaidRequested;

        // ── Self-bootstrap (one per HUB scene) ────────────────────────────────
        private static bool s_hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => s_hooked = false;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (s_hooked) return;
            s_hooked = true;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            TryInstall(SceneManager.GetActiveScene()); // first scene is already loaded
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => TryInstall(scene);

        private static void TryInstall(Scene scene)
        {
            try
            {
                if (!scene.IsValid()) return;
                // Raids are entered from the home HUB (castle / village / over-world).
                if (!HubScenes.IsHub(scene.name)) return;
                if (FindAnyObjectByType<RaidEntryBridge>() != null) return;

                var go = new GameObject("RaidEntryBridge");
                if (scene.isLoaded) SceneManager.MoveGameObjectToScene(go, scene);
                go.AddComponent<RaidEntryBridge>();
                FlowTrace.Step("Raid", $"RaidEntryBridge installed in hub scene '{scene.name}'.");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[RaidEntryBridge] install failed: " + e.Message);
            }
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private void OnDisable()
        {
            Unbind();
        }

        private void Update()
        {
            // The HUD registers itself with CoreServices in Start; it may not exist on
            // this bridge's first frame, so resolve lazily + re-bind if the instance
            // changes (a HUD re-instance across a scene reload).
            object hudNow = CoreServices.Hud as object;
            if (hudNow == null)
            {
                if (_hud != null) Unbind();
                return;
            }
            if (!ReferenceEquals(hudNow, _hud)) Bind(hudNow);
        }

        private void Bind(object hud)
        {
            Unbind();
            _hud = hud;

            var field = hud.GetType().GetField("RaidRequested",
                BindingFlags.Public | BindingFlags.Instance);
            _raidRequestedEvent = field?.GetValue(hud) as UnityEvent;
            if (_raidRequestedEvent == null)
            {
                FlowTrace.Warn("Raid", "HUD.RaidRequested not found — the raid icon tap will be silent.");
                return;
            }
            _onRaidRequested = OnRaidRequested;
            _raidRequestedEvent.AddListener(_onRaidRequested);
            FlowTrace.Step("Raid", "RaidEntryBridge bound to HUD.RaidRequested.");
        }

        private void Unbind()
        {
            if (_raidRequestedEvent != null && _onRaidRequested != null)
                _raidRequestedEvent.RemoveListener(_onRaidRequested);
            _raidRequestedEvent = null;
            _onRaidRequested = null;
            _hud = null;
        }

        private void OnRaidRequested()
        {
            if (!DeNelle.Core.FeatureFlags.Raid)
            {
                FlowTrace.Step("Raid", "raid icon fired but RAID is feature-flagged OFF (victory/return not built) — ignored.");
                return;
            }

            // WO-449 — continuous-walk loop: the raid target is a LIVE outpost out in the OuterWorld
            // (RaidOutpostSystem spawns it ~70m past each gate). There is NO selection/deploy screen
            // and NO teleport — the player just walks out a gate to it and combat starts on approach.
            // So the raid icon is a deliberate no-op here (a nudge, not a portal). Flip ff.raidwalk OFF
            // to restore the legacy RaidSelectionScreen->Deploy->GoRaid teleport path below verbatim.
            if (DeNelle.Core.FeatureFlags.RaidContinuousWalk)
            {
                FlowTrace.Step("Raid", "continuous-walk mode: raid icon no-op; walk out a gate to the outpost (~70m).");
                return;
            }

            FlowTrace.Step("Raid", "raid icon fired — opening RaidSelectionScreen.");
            RaidSelectionScreen.Open();
        }
    }
}
