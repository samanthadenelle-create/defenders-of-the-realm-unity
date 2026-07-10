// =============================================================================
// MusicRequest — a typed "play this track on this layer" request (2026-07-09).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Audio.
//
// The immutable value a requester hands the single music authority
// (IMusicAuthority.Push). It carries WHICH track (the Core-side MusicTrack enum)
// and WHICH layer it belongs to. The authority owns the one playback pair and
// resolves the highest active layer — the requester never touches an AudioSource.
// Readonly struct: allocation-free, deterministic.
// =============================================================================

namespace DeNelle.Core.Audio
{
    /// <summary>
    /// A request to sound <see cref="Track"/> on <see cref="Layer"/>. Pushed to
    /// <see cref="IMusicAuthority.Push"/>; the authority replaces that layer's
    /// track and re-resolves the top. Immutable, allocation-free.
    /// </summary>
    public readonly struct MusicRequest
    {
        /// <summary>The track to sound on this layer.</summary>
        public readonly MusicTrack Track;

        /// <summary>The priority layer this request occupies.</summary>
        public readonly MusicLayer Layer;

        public MusicRequest(MusicTrack track, MusicLayer layer)
        {
            Track = track;
            Layer = layer;
        }
    }
}
