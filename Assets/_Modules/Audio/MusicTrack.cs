// =============================================================================
// MusicTrack — the music track keys + the owner-locked mix registry.
// -----------------------------------------------------------------------------
// Port of the React audioManager.ts TRACKS table, owner-locked by
// docs/audio-mix-spec.md §2 (2026-05-18). Each track carries its default volume
// (pre-master, 0..1), loop flag, and fade-in / fade-out durations in seconds.
//
// audio-mix-spec.md §7 ("Unity port note") says: this entire spec maps to
// Unity's AudioMixer; per-track defaults live in a registry so the values stay
// owner-tunable. This is that registry. It is a plain static C# class — NOT a
// MonoBehaviour — so the engine math is testable and has no scene dependency.
//
// The six tracks are: title / village / dungeon / battle / victory / defeat.
// `dungeon`'s source file (echoes-beneath-elarion.mp3) is a KNOWN-MISSING asset
// (see docs/port-notes/audio-system.md "FLAGGED"); the registry still carries
// its path + mix so AudioService can wire it, guard the missing clip, and log.
// =============================================================================

using System.Collections.Generic;

namespace DeNelle.Audio
{
    /// <summary>
    /// The six canonical music tracks (audio-mix-spec.md §2). Used as the key
    /// into <see cref="MusicTrackRegistry"/> and as the public argument to
    /// <see cref="AudioService.PlayMusic"/>.
    /// </summary>
    public enum MusicTrack
    {
        /// <summary>No music — used to fade the current track out to silence.</summary>
        None = 0,
        /// <summary>Title screen + opening cinematic. Default volume 0.6.</summary>
        Title,
        /// <summary>Village long-session exploration. Default volume 0.4 (soft — fatigue).</summary>
        Village,
        /// <summary>Dungeon ambient. Default volume 0.25 (very soft — ambient only).</summary>
        Dungeon,
        /// <summary>ATB Last-Stand combat. Default volume 0.7 (featured — drives tension).</summary>
        Battle,
        /// <summary>Battle-win sting. Default volume 0.7. Does not loop.</summary>
        Victory,
        /// <summary>Battle-loss sting. Default volume 0.5. Does not loop.</summary>
        Defeat,
        /// <summary>Open-world exploration ambient. Default volume 0.4 (soft — long sessions). WO-171.</summary>
        Overworld,
        /// <summary>Arena raid BGM — "Echo's theme" (echo_theme.mp3). Default volume 0.4 (soft background). Loops.</summary>
        Arena,
        /// <summary>Offensive-raid BGM — "brass-rampart" (Music/Raid/brass-rampart.mp3, WO-453). Driving brass for marching an army on a fortress. Loops.</summary>
        Raid,
    }

    /// <summary>
    /// One selectable entry in the ambient-music jukebox (WO-162) — a track key
    /// plus the player-facing display name. Pure data; authorable in
    /// <see cref="AudioService.AmbientChoicesFor"/>. Immutable.
    /// </summary>
    public sealed class MusicChoice
    {
        /// <summary>The track this choice plays.</summary>
        public readonly MusicTrack Track;

        /// <summary>The player-facing name shown in the selection UI.</summary>
        public readonly string DisplayName;

        public MusicChoice(MusicTrack track, string displayName)
        {
            Track = track;
            DisplayName = displayName;
        }
    }

    /// <summary>
    /// One track's owner-locked mix definition — the Unity port of the React
    /// <c>TrackDefinition</c> shape (audio-mix-spec.md §6). Immutable.
    /// </summary>
    public sealed class MusicTrackDef
    {
        /// <summary>The track key.</summary>
        public readonly MusicTrack Track;

        /// <summary>
        /// The expected import path under <c>Assets/Audio/</c>, mirroring the
        /// React <c>/audio/</c> layout. The AudioClip itself is wired in the
        /// inspector or resolved by an import pass; this string is documentation
        /// + the log line when a clip is missing.
        /// </summary>
        public readonly string AssetPath;

        /// <summary>Pre-master default volume, 0..1 (audio-mix-spec.md §2). No track defaults to 1.0.</summary>
        public readonly float DefaultVolume;

        /// <summary>True if the track loops (title / village / dungeon / battle). Stings do not.</summary>
        public readonly bool Loop;

        /// <summary>Crossfade-in duration, seconds (audio-mix-spec.md §2).</summary>
        public readonly float FadeInSeconds;

        /// <summary>Crossfade-out duration, seconds (audio-mix-spec.md §2).</summary>
        public readonly float FadeOutSeconds;

        internal MusicTrackDef(MusicTrack track, string assetPath, float defaultVolume,
                               bool loop, float fadeInSeconds, float fadeOutSeconds)
        {
            Track = track;
            AssetPath = assetPath;
            DefaultVolume = defaultVolume;
            Loop = loop;
            FadeInSeconds = fadeInSeconds;
            FadeOutSeconds = fadeOutSeconds;
        }
    }

    /// <summary>
    /// The owner-locked track registry — a static port of audio-mix-spec.md §2.
    /// Every value here is owner-tunable: edit the constant, no architecture
    /// change (audio-mix-spec.md §9 "Owner-tunable knobs").
    /// </summary>
    public static class MusicTrackRegistry
    {
        // ── Owner-locked default volumes (audio-mix-spec.md §2) ───────────────
        // Kept as named constants so a playtest tuning is a one-line edit.
        /// <summary>Title default volume — moderate, supports the cold-open narrative.</summary>
        public const float TitleVolume = 0.6f;
        /// <summary>Village default volume — soft, anti-fatigue for long sessions.</summary>
        public const float VillageVolume = 0.4f;
        /// <summary>Dungeon default volume — very soft, ambient only (owner directive 2026-05-18).</summary>
        public const float DungeonVolume = 0.25f;
        /// <summary>Battle default volume — featured, drives combat tension.</summary>
        public const float BattleVolume = 0.7f;
        /// <summary>Victory sting volume — celebratory beat.</summary>
        public const float VictoryVolume = 0.7f;
        /// <summary>Defeat sting volume — softer; the loss lands slowly.</summary>
        public const float DefeatVolume = 0.5f;
        /// <summary>Overworld default volume — soft, anti-fatigue for open-world exploration (WO-171).</summary>
        public const float OverworldVolume = 0.4f;
        /// <summary>Arena ("Echo's theme") default volume — soft background, sits under the raid combat SFX.</summary>
        public const float ArenaVolume = 0.4f;   // arena BGM — soft background

        private static readonly Dictionary<MusicTrack, MusicTrackDef> Defs =
            new Dictionary<MusicTrack, MusicTrackDef>
        {
            // track            assetPath (mirrors React /audio/)              vol             loop  fadeIn  fadeOut
            { MusicTrack.Title,   new MusicTrackDef(MusicTrack.Title,
                "Assets/Audio/title.mp3",                              TitleVolume,   true,  1.2f, 1.0f) },
            { MusicTrack.Village, new MusicTrackDef(MusicTrack.Village,
                "Assets/Audio/village.mp3",                            VillageVolume, true,  1.2f, 1.0f) },
            { MusicTrack.Dungeon, new MusicTrackDef(MusicTrack.Dungeon,
                "Assets/Audio/dungeons/echoes-beneath-elarion.mp3",    DungeonVolume, true,  1.2f, 1.0f) },
            { MusicTrack.Battle,  new MusicTrackDef(MusicTrack.Battle,
                "Assets/Audio/battle.mp3",                             BattleVolume,  true,  0.6f, 0.6f) },
            { MusicTrack.Victory, new MusicTrackDef(MusicTrack.Victory,
                "Assets/Audio/victory.mp3",                            VictoryVolume, false, 0.2f, 0.8f) },
            { MusicTrack.Defeat,  new MusicTrackDef(MusicTrack.Defeat,
                "Assets/Audio/defeat.mp3",                             DefeatVolume,  false, 1.5f, 1.5f) },
            { MusicTrack.Overworld, new MusicTrackDef(MusicTrack.Overworld,
                "Assets/Audio/world.mp3",                              OverworldVolume, true, 1.2f, 1.0f) },
            { MusicTrack.Arena,   new MusicTrackDef(MusicTrack.Arena,
                "Assets/Audio/Resources/Music/echo_theme.mp3",         ArenaVolume,   true,  1.2f, 1.0f) },
        };

        /// <summary>
        /// The mix definition for a track, or null for <see cref="MusicTrack.None"/>
        /// (or any unmapped value). Callers guard the null.
        /// </summary>
        public static MusicTrackDef Get(MusicTrack track)
        {
            return Defs.TryGetValue(track, out MusicTrackDef def) ? def : null;
        }

        /// <summary>True if the registry has a mix definition for the track.</summary>
        public static bool Has(MusicTrack track) => Defs.ContainsKey(track);
    }
}
