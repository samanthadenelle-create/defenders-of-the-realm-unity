// =============================================================================
// TalkHudBridge — gates the HUD's Talk button on "a talkable NPC is in range" and
// routes a Talk press to the NEAREST in-range NPC's dialogue.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// SEAM (least cross-level exposure — owner directive): the HUD's Talk method + event
// are NOT on IVillageHud (Core stays clean); this Village-side bridge reaches them by
// REFLECTION, exactly like HeroAbilitiesHudBridge does AbilityRequested. So "Talk"
// lives only in HUD + this bridge — Core never learns it exists.
//
// NO per-frame scan: availability is an O(1) read of TalkPromptRegistry.Count on a
// throttled poll, pushed to the HUD only on CHANGE (edge-triggered). The HUD-resolve
// scan stops the moment it hooks (capped). Honors the OuterWorld-leak hardening lesson.
// =============================================================================
using System.Reflection;
using UnityEngine;
using UnityEngine.Events;

namespace DeNelle.Village
{
    public sealed class TalkHudBridge : MonoBehaviour
    {
        private const float PollInterval = 0.25f;
        private const int   MaxResolveAttempts = 240;   // ~60s of throttled hook attempts, then give up

        private float _timer;
        private int _resolveAttempts;
        private Transform _hero;

        private Object _hud;                 // VillageHudController (held as Object — no DeNelle.HUD ref)
        private MethodInfo _setTalkAvailable; // SetTalkAvailable(bool)
        private UnityEvent _talkRequested;    // TalkRequested
        private UnityAction _onTalk;
        private readonly object[] _availArgs = new object[1];
        private bool _hooked;
        private bool _lastAvailable;
        private bool _haveLast;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            var go = new GameObject("TalkHudBridge");
            DontDestroyOnLoad(go);
            go.AddComponent<TalkHudBridge>();
        }

        private void OnDisable()
        {
            if (_talkRequested != null && _onTalk != null) _talkRequested.RemoveListener(_onTalk);
            _talkRequested = null;
            _onTalk = null;
            _hooked = false;
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = PollInterval;

            if (!_hooked) { TryHook(); if (!_hooked) return; }

            // O(1) registry read — NOT a scan. Push to the HUD only when it changes.
            bool available = TalkPromptRegistry.Count > 0;
            if (!_haveLast || available != _lastAvailable)
            {
                _lastAvailable = available;
                _haveLast = true;
                PushAvailable(available);
            }
        }

        private void PushAvailable(bool available)
        {
            if (_setTalkAvailable == null || _hud == null) return;
            _availArgs[0] = available;
            _setTalkAvailable.Invoke(_hud, _availArgs);
        }

        private void TryHook()
        {
            if (_resolveAttempts++ > MaxResolveAttempts) return;

            if (_hud == null)
            {
                foreach (var mb in FindObjectsByType<MonoBehaviour>(FindObjectsInactive.Include))
                    if (mb != null && mb.GetType().Name == "VillageHudController") { _hud = mb; break; }
            }
            if (_hud == null) return;

            var t = _hud.GetType();
            _setTalkAvailable = t.GetMethod("SetTalkAvailable",
                BindingFlags.Public | BindingFlags.Instance, null, new[] { typeof(bool) }, null);

            var field = t.GetField("TalkRequested", BindingFlags.Public | BindingFlags.Instance);
            _talkRequested = field?.GetValue(_hud) as UnityEvent;

            if (_setTalkAvailable == null || _talkRequested == null) return;

            _onTalk = OnTalkPressed;
            _talkRequested.AddListener(_onTalk);
            _hooked = true;
            _haveLast = false;   // force an initial push on the next tick
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
