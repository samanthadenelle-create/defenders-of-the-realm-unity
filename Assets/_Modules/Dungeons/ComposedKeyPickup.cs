// =============================================================================
// ComposedKeyPickup — WO-1001 slice 7 walk-over key pickup for Pipeline A.
// -----------------------------------------------------------------------------
// SPLIT OUT OF ComposedKeyLock.cs ON PURPOSE. Unity deserializes a MonoBehaviour
// from a scene by matching the class to a script asset whose FILE NAME equals the
// class name. While this lived alongside ComposedKeyBag and ComposedLockedPort in
// ComposedKeyLock.cs, the baker's AddComponent succeeded at bake time and the
// component did NOT survive the scene load — so every baked key pickup was gone,
// and with it the only way to open a locked deep floor. The bake still logged
// "KEY 'crypt-key' @ ..." and reported saved=True, which is what made it invisible.
// One MonoBehaviour per file, named for the file. Do not re-merge these.
// =============================================================================

using DeNelle.Core.Diagnostics;
using DeNelle.Village;
using UnityEngine;

namespace DeNelle.Dungeons
{
    /// <summary>Walk-over key pickup (baked near a side vault / under a chest).</summary>
    [DisallowMultipleComponent]
    public sealed class ComposedKeyPickup : MonoBehaviour
    {
        [SerializeField] private string _keyId = "crypt-key";
        private bool _taken;

        /// <summary>Bake-time wiring. <c>_keyId</c> is [SerializeField] so it survives SaveScene.</summary>
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
}
