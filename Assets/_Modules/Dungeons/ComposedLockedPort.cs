// =============================================================================
// ComposedLockedPort — WO-1001 slice 7 locked traversal for Pipeline A.
// -----------------------------------------------------------------------------
// SPLIT OUT OF ComposedKeyLock.cs ON PURPOSE — see ComposedKeyPickup.cs for the
// full reasoning. Unity matches a serialized MonoBehaviour to a script asset by
// FILE NAME, so while this class lived in ComposedKeyLock.cs it did not survive
// the scene load and every baked locked port silently vanished.
//
// Two defects had to be fixed together for a lock to work at all:
//   1. this file split (the component must survive deserialization), and
//   2. [SerializeField] on the config fields (the baker configures at BAKE time
//      and then saves; plain privates were discarded by SaveScene).
// Either one alone still leaves the lock inert.
// =============================================================================

using DeNelle.Core.Diagnostics;
using DeNelle.Village;
using DeNelle.Village.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// Locked stair/door port: shows "Locked" until the key is held, then "Unlock &amp; pass".
    /// Wraps the same fade+warp as <see cref="DungeonPortLink"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class ComposedLockedPort : MonoBehaviour
    {
        // [SerializeField] IS LOAD-BEARING: DungeonBaker.PlaceComposeLocks configures these at
        // BAKE time and the scene is then saved. As plain privates every value was discarded and
        // Update() bailed on `_hero == null` forever.
        [SerializeField] private string _keyId = "crypt-key";
        [SerializeField] private string _promptLocked = "Locked";
        [SerializeField] private string _promptOpen = "Unlock";
        [SerializeField] private Vector3 _target;
        [SerializeField] private float _faceY;
        [SerializeField] private Transform _hero;
        [SerializeField] private float _radius = 2.2f;

        private bool _inRange;
        private bool _porting;
        private bool _heroRebindTried;

        public void Configure(string keyId, Vector3 target, float faceY, Transform hero,
            string promptLocked = "Locked - need key", string promptOpen = "Unlock & pass",
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

        // WO-1112: same defect as ComposedKeyPickup — DungeonBaker.PlaceComposeLocks bakes this
        // as a bare GameObject with no Renderer, so the barrier the player is meant to READ as
        // "you need a key" was invisible; all they got was a floating prompt with no object.
        // The plate is yawed to the serialized _faceY so it stands across the way it leads.
        private void Start() => ComposedPropVisuals.BuildLock(gameObject, _faceY);

        /// <summary>Rebind ONCE off the Player tag if the serialized hero is missing, and say so.</summary>
        private bool TryRebindHero()
        {
            if (_hero != null) return true;
            if (_heroRebindTried) return false;
            _heroRebindTried = true;

            var tagged = GameObject.FindGameObjectWithTag("Player");
            if (tagged == null)
            {
                FlowTrace.Warn("ComposedKey", $"LockedPort '{name}': no serialized hero AND no Player-tagged object - this lock is INERT, the floor behind it is unreachable.");
                return false;
            }
            _hero = tagged.transform;
            FlowTrace.Step("ComposedKey", $"LockedPort '{name}': hero reference was null - rebound via the Player tag.");
            return true;
        }

        private void Update()
        {
            if (_porting) return;
            if (_hero == null && !TryRebindHero()) return;

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
            // Step IN / step OUT: an ENTER with no matching EXIT means the port started and never
            // completed, which is otherwise indistinguishable from the prompt doing nothing.
            using var _scope = FlowTrace.Enter("ComposedKey", $"LockedPort '{name}' key='{_keyId}' hasKey={hasKey}");

            if (_porting) return;
            if (!hasKey)
            {
                FlowTrace.Step("ComposedKey",
                    $"port BLOCKED - missing key '{_keyId}' @ {transform.position}");
                return;
            }
            _porting = true;
            MobileInteractButton.Release(this);

            // Reuse the DungeonPortLink warp path (fade -> teleport -> face onward).
            var link = gameObject.GetComponent<DungeonPortLink>();
            if (link == null) link = gameObject.AddComponent<DungeonPortLink>();
            link.Configure(_promptOpen, _target, _faceY, _hero, null, "locked", "unlocked", _radius);
            link.Port();
            _porting = false;
            FlowTrace.Step("ComposedKey", $"UNLOCKED port with '{_keyId}' -> {_target}");
        }

        private void OnDisable() => MobileInteractButton.Release(this);
    }
}
