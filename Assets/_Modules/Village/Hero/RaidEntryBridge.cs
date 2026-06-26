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
            // V1 DESCOPE (2026-06-26): the icon used to be a dead no-op here. Now it POINTS the player
            // at the nearest live raid: it finds the closest uncleared EnemyOutpost and logs a
            // "head <direction>" hint relative to the hero. Simple + null-safe; no portal/teleport.
            // Flip ff.raidwalk OFF to restore the legacy RaidSelectionScreen->Deploy->GoRaid path.
            if (DeNelle.Core.FeatureFlags.RaidContinuousWalk)
            {
                PingNearestRaidOutpost();
                return;
            }

            FlowTrace.Step("Raid", "raid icon fired — opening RaidSelectionScreen.");
            RaidSelectionScreen.Open();
        }

        // -- V1 raid-icon ping: point the player at the nearest live raid outpost --
        // Continuous-walk has no portal; tapping the icon nudges the player toward the
        // closest uncleared EnemyOutpost (a "head <direction>" hint), so the icon is
        // useful instead of dead. Fully null-safe — degrades to a soft hint if no live
        // outpost is found yet (they spawn ~10s after entering the OuterWorld).
        private static void PingNearestRaidOutpost()
        {
            DeNelle.Village.World.Camps.EnemyOutpost[] outposts = null;
            Guard.Try("Raid", "resolve live raid outposts", () =>
            {
                outposts = DeNelle.Village.World.Camps.RaidOutpostSystem.Outposts;
            });

            // Hero origin for the direction hint (component lookup per canon §7; no HeroTarget tag).
            Vector3 origin = Vector3.zero;
            var hero = FindFirstObjectByType<HeroLocomotion>();
            if (hero != null) origin = hero.transform.position;

            DeNelle.Village.World.Camps.EnemyOutpost nearest = null;
            float bestSqr = float.MaxValue;
            if (outposts != null)
            {
                for (int i = 0; i < outposts.Length; i++)
                {
                    var o = outposts[i];
                    if (o == null) continue;          // not realized yet
                    if (o.Cleared) continue;          // skip cleared raids
                    float d = (o.transform.position - origin).sqrMagnitude;
                    if (d < bestSqr) { bestSqr = d; nearest = o; }
                }
            }

            if (nearest == null)
            {
                FlowTrace.Step("Raid", "raid icon: no live outpost yet (they spawn ~10s into the OuterWorld) — walk out a gate.");
                return;
            }

            Vector3 to = nearest.transform.position - origin;
            string dir = CompassHint(to);
            float dist = new Vector2(to.x, to.z).magnitude;
            FlowTrace.Step("Raid",
                $"raid icon -> nearest outpost '{nearest.OutpostId}' ({nearest.Region}) ~{dist:0}m to the {dir}; head that way to raid.");
            Debug.Log($"[RaidEntryBridge] Raid this way: {nearest.Region} outpost ~{dist:0}m to the {dir}.");
        }

        // 8-point compass label from a world-space XZ direction (Z+ = North, X+ = East).
        private static string CompassHint(Vector3 to)
        {
            if (to.x * to.x + to.z * to.z < 0.01f) return "here";
            float ang = Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg;   // 0 = North, 90 = East
            if (ang < 0f) ang += 360f;
            string[] pts = { "north", "north-east", "east", "south-east", "south", "south-west", "west", "north-west" };
            int idx = Mathf.RoundToInt(ang / 45f) % 8;
            return pts[idx];
        }
    }
}
