// =============================================================================
// MusicLayer — the priority-ordered music layers (MUSIC_AUTHORITY_DESIGN 2026-07-09).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Audio (Core owns the contract;
// the DeNelle.Audio MusicDirector and the DeNelle.Village policy providers all
// reference this Core type — never HUD).
//
// The single music authority always sounds the HIGHEST active layer. A layer is
// "active" while some system holds a track on it (Push); it goes inactive on
// Release. Because exactly one class owns the playback pair and it resolves the
// top layer, two beds are impossible by construction.
//
// Order is load-bearing: a numerically higher layer wins. Do NOT reorder without
// re-checking MusicDirector.Resolve + MusicDirector.LayerFor.
// =============================================================================

namespace DeNelle.Core.Audio
{
    /// <summary>
    /// Priority order, low → high. The <c>MusicDirector</c> always sounds the
    /// highest active layer; releasing a layer auto-falls back to the next-highest.
    /// </summary>
    public enum MusicLayer
    {
        /// <summary>No layer — sentinel / "nothing requested here".</summary>
        None = 0,
        /// <summary>Hub / village idle bed (town ambient explore music).</summary>
        Ambient = 1,
        /// <summary>Open-world exploration bed.</summary>
        Overworld = 2,
        /// <summary>Wave-loop combat/exploration (WaveMusicController).</summary>
        Wave = 3,
        /// <summary>Staged battle / arena bed (BattleMusicManager, BattleArena).</summary>
        Battle = 4,
        /// <summary>Victory / defeat sting bed.</summary>
        Outcome = 5,
        /// <summary>Title / story intro — tops everything.</summary>
        Cutscene = 6,
    }
}
