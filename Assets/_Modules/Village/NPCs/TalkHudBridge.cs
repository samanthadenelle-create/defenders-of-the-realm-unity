// =============================================================================
// TalkHudBridge — gates the HUD's Talk button on "a talkable NPC is in range" and
// routes a Talk press to the NEAREST in-range NPC's dialogue.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// P23 ROOT-CAUSE FIX (§0 "talk button not appearing", HUD_OBSIDIAN 2026-07-03):
// the old bridge hooked the PER-SCENE VillageHudController by ONE-SHOT reflection
// (cached MethodInfo + instance, `_hooked = true`, MaxResolveAttempts = 240).
// After any scene swap the cached instance was DESTROYED; PushAvailable invoked
// a dead target, OnDisable never ran (the bridge itself is DontDestroyOnLoad),
// and once the attempt budget drained it could never re-hook — so availability
// was never pushed again and the Talk button never appeared. PROOF: the hook
// design at :25/:36/:62/:83-105 vs. VillageHudBootstrap's per-scene, non-DDoL
// HUD ("Per-scene-ensure keeps exactly one live HUD", VillageHudBootstrap.cs).
//
// THE FIX: no reflection, no cached instance. Availability pushes the Core
// static PostureSignals.SetTalkAvailable (cannot go stale); the Talk press
// registers into HudCommands.RegisterTalk (re-registered every scene load).
// The HUD kit binds both. NO per-frame scan: availability stays an O(1)
// TalkPromptRegistry.Count read on a throttled poll, pushed edge-triggered.
// =============================================================================
using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.HUD;
using DeNelle.Core.HudModel;

namespace DeNelle.Village
{
    /// <summary>Pushes talk availability + handles Talk presses (see header).</summary>
    public sealed class TalkHudBridge : MonoBehaviour
    {
        private const float PollInterval = 0.25f;

        private float _timer;
        private Transform _hero;
        private bool _lastAvailable;
        private bool _haveLast;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("TalkHudBridge");
            DontDestroyOnLoad(go);
            var bridge = go.AddComponent<TalkHudBridge>();
            // Re-register the press handler on every scene load (never-stale law).
            SceneManager.sceneLoaded += (_, __) => bridge.RegisterTalkHandler();
            bridge.RegisterTalkHandler();
        }

        private void RegisterTalkHandler()
        {
            HudCommands.RegisterTalk(OnTalkPressed);
            _hero = null;          // re-resolve the hero for the new scene
            _haveLast = false;     // force an availability re-push
            FlowTrace.Step("HudKit", "TalkHudBridge: talk handler registered (scene '" +
                           SceneManager.GetActiveScene().name + "')");
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = PollInterval;

            // O(1) registry read — NOT a scan. Push only on change (edge-triggered).
            bool available = TalkPromptRegistry.Count > 0;
            if (!_haveLast || available != _lastAvailable)
            {
                _lastAvailable = available;
                _haveLast = true;
                PostureSignals.SetTalkAvailable(available);   // Core static — cannot go stale
            }
        }

        private void OnTalkPressed()
        {
            if (_hero == null)
            {
                var p = GameObject.FindWithTag("Player");
                _hero = p != null ? p.transform : null;
            }
            Vector3 from = _hero != null ? _hero.position : Vector3.zero;
            TalkPromptRegistry.NearestTalk(from)?.Invoke();
        }
    }
}
