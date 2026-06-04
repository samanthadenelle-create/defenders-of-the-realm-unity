// =============================================================================
// SfxClipLibrary — ScriptableObject mapping SfxId → AudioClip. WO-62.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Audio   Namespace: DeNelle.Audio
//
// Create via: Assets → Create → Defenders/Audio/SFX Clip Library
// Assign the resulting asset to AudioService._sfxLibrary in the Inspector.
//
// WORKFLOW:
//   1. Add a row in the Entries array in the Inspector.
//   2. Set Id to the SfxId enum value.
//   3. Drag the authored WAV/MP3 clip into the Clip field.
//   4. Any SfxId with a null Clip is a silent no-op at runtime — no errors.
//
// When no clip is wired, AudioService.PlaySfxAtPosition silently no-ops so the
// game never crashes on a missing sound (same guard philosophy as VFXManager's
// null-prefab fallback).
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Audio
{
    [CreateAssetMenu(
        menuName  = "Defenders/Audio/SFX Clip Library",
        fileName  = "SfxClipLibrary",
        order     = 56)]
    public sealed class SfxClipLibrary : ScriptableObject
    {
        // ── Entry ─────────────────────────────────────────────────────────────

        [Serializable]
        public struct Entry
        {
            [Tooltip("The SfxId enum value this row covers.")]
            public SfxId Id;

            [Tooltip("AudioClip to play for this SFX event. Null = silent no-op.")]
            public AudioClip Clip;

            [Tooltip("Pre-mixer volume scalar, 0-1. Applied on top of AudioService voice volume.")]
            [Range(0f, 1f)] public float Volume;
        }

        // ── Inspector array ───────────────────────────────────────────────────

        [Tooltip("One row per SfxId. Rows with null Clip are silent no-ops.")]
        public Entry[] Entries = Array.Empty<Entry>();

        // ── Runtime lookup ────────────────────────────────────────────────────

        private Dictionary<SfxId, Entry> _map;

        private void BuildLookup()
        {
            _map = new Dictionary<SfxId, Entry>(Entries.Length);
            foreach (var e in Entries)
            {
                if (e.Id == SfxId.None) continue;
                _map[e.Id] = e;   // last entry wins on duplicate ids
            }
        }

        /// <summary>
        /// Returns the AudioClip wired for <paramref name="id"/>, or null when
        /// not wired. A null return is always a safe, silent no-op at the call site.
        /// </summary>
        public AudioClip GetClip(SfxId id)
        {
            if (_map == null) BuildLookup();
            return _map.TryGetValue(id, out var e) ? e.Clip : null;
        }

        /// <summary>
        /// Returns the volume scalar for <paramref name="id"/> (default 1 when not wired).
        /// </summary>
        public float GetVolume(SfxId id)
        {
            if (_map == null) BuildLookup();
            return _map.TryGetValue(id, out var e) ? Mathf.Clamp01(e.Volume > 0f ? e.Volume : 1f) : 1f;
        }

        private void OnEnable()   => BuildLookup();
        private void OnValidate() => BuildLookup();
    }
}
