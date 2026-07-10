// =============================================================================
// IMusicAuthority — the ONE seam through which music is requested (2026-07-09).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Audio.
//
// The invariant (MUSIC_AUTHORITY_DESIGN 2026-07-09): exactly one class owns the
// music AudioSources; every other system REQUESTS a track through this contract
// and none can play one. Implemented by DeNelle.Audio.MusicDirector (the single
// A/B playback pair). Consumers that only see DeNelle.Core resolve it the same
// way they resolve IAudioService — but the concrete director is also reachable
// directly (MusicDirector.Instance) from modules that reference DeNelle.Audio.
// =============================================================================

namespace DeNelle.Core.Audio
{
    /// <summary>
    /// The single music authority. <see cref="Push"/> sets/replaces the track for
    /// a layer and re-resolves the highest active layer; <see cref="Release"/>
    /// clears a layer and auto-falls back to the next-highest (no caller ever has
    /// to "restore ambient"). Two beds are impossible: one implementor owns the
    /// one playback pair.
    /// </summary>
    public interface IMusicAuthority
    {
        /// <summary>Set/replace the track for <c>req.Layer</c>, then re-resolve the top.</summary>
        void Push(MusicRequest req);

        /// <summary>Clear <paramref name="layer"/>, then re-resolve the top (auto-fallback).</summary>
        void Release(MusicLayer layer);

        /// <summary>The single resolved bed currently sounding (best-effort track key).</summary>
        MusicTrack Current { get; }
    }
}
