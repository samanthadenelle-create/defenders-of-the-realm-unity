// =============================================================================
// ComposedKeyLock — WO-1001 slice 7 simple key + locked traversal for Pipeline A.
// -----------------------------------------------------------------------------
// Keys are run-local (DungeonRuntimeState flags via a static bag on the host).
// A locked port refuses Port() until the matching key id is held; key pickups
// are tiny trigger volumes (or granted when a marked chest breaks).
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using DeNelle.Village;
using DeNelle.Village.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeNelle.Dungeons
{
    /// <summary>Run-local key bag for composed dungeons (not saved to disk).</summary>
    public static class ComposedKeyBag
    {
        private static readonly HashSet<string> Keys = new HashSet<string>();

        public static void Clear() => Keys.Clear();
        public static void Grant(string keyId)
        {
            if (string.IsNullOrEmpty(keyId)) return;
            if (Keys.Add(keyId))
                FlowTrace.Step("ComposedKey", $"granted key '{keyId}' (held={Keys.Count})");
        }
        public static bool Has(string keyId) =>
            !string.IsNullOrEmpty(keyId) && Keys.Contains(keyId);
    }

    /// <summary>Walk-over key pickup (baked near a side vault / under a chest).</summary>
    [DisallowMultipleComponent]
    public sealed class ComposedKeyPickup : MonoBehaviour
    {
        [SerializeField] private string _keyId = "crypt-key";
        private bool _taken;

        public void Configure(string keyId) => _keyId = string.IsNullOrEmpty(keyId) ? "key" : keyId;

        private void OnTriggerEnter(Collider other)
        {
            if (_taken) return;
            if (other == null || other.GetComponentInParent<HeroHealth>() == null) return;
            _taken = true;
            ComposedKeyBag.Grant(_keyId);
            FlowTrace.Step("ComposedKey", $"pickup '{_keyId}' @ {transform.position}");
            gameObject.SetActive(false);
        }
    }

    /// <summary>
    /// Locked stair/door port: shows "Locked" until the key is held, then "Unlock &amp; pass".
    /// Wraps the same fade+warp as <see cref="DungeonPortLink"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ComposedLockedPort : MonoBehaviour
    {
        private string _keyId = "crypt-key";
        private string _promptLocked = "Locked";
        private string _promptOpen = "Unlock";
        private Vector3 _target;
        private float _faceY;
        private Transform _hero;
        private float _radius = 2.2f;
        private bool _inRange;
        private bool _porting;

        public void Configure(string keyId, Vector3 target, float faceY, Transform hero,
            string promptLocked = "Locked — need key", string promptOpen = "Unlock & pass",
            float radius = 2.2f)
        {
            _keyId = string.IsNullOrEmpty(keyId) ? "key" : keyId;
            _target = target;
            _faceY = faceY;
            _hero = hero;
            _promptLocked = promptLocked;
            _promptOpen = promptOpen;
            _radius = Mathf.Max(0.5f, radius);
        }

        private void Update()
        {
            if (_hero == null || _porting) return;
            bool now = (_hero.position - transform.position).sqrMagnitude <= _radius * _radius;
            if (now != _inRange)
            {
                _inRange = now;
                if (!_inRange) MobileInteractButton.Release(this);
            }
            if (!_inRange || MobileInteractButton.Suppressed) return;

            bool hasKey = ComposedKeyBag.Has(_keyId);
            string prompt = hasKey ? _promptOpen : _promptLocked;
            MobileInteractButton.Request(this, prompt, () => TryPort(hasKey));

            var kb = Keyboard.current;
            if (kb != null && kb.fKey.wasPressedThisFrame) TryPort(hasKey);
        }

        private void TryPort(bool hasKey)
        {
            if (_porting) return;
            if (!hasKey)
            {
                FlowTrace.Step("ComposedKey",
                    $"port blocked — missing key '{_keyId}' @ {transform.position}");
                return;
            }
            _porting = true;
            MobileInteractButton.Release(this);
            // Reuse DungeonPortLink warp path by temporary component.
            var link = gameObject.GetComponent<DungeonPortLink>();
            if (link == null) link = gameObject.AddComponent<DungeonPortLink>();
            link.Configure(_promptOpen, _target, _faceY, _hero, null, "locked", "unlocked", _radius);
            link.Port();
            _porting = false;
            FlowTrace.Step("ComposedKey", $"unlocked port with '{_keyId}' -> {_target}");
        }

        private void OnDisable() => MobileInteractButton.Release(this);
    }
}
