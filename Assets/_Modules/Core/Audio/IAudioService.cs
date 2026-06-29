using UnityEngine;
namespace DeNelle.Core.Audio
{
    public interface IAudioService
    {
        void PlaySfx(AudioClip clip, float volume);
        void PlayMusic(MusicTrack track);

        /// <summary>Fades the current music track out to silence. Exposed so the cinematic
        /// intro can kill the boot/title music while the video's own voiceover plays
        /// (owner 2026-06-29: "only use the video"). Call via CoreServices.Audio?.StopMusic().</summary>
        void StopMusic();

        /// <summary>
        /// DEF-183: plays the shared UI button-click blip on the UI mixer group.
        /// The implementor owns the clip (generated or authored), so callers that
        /// can only see DeNelle.Core (e.g. DeNelle.HUD) get a click without
        /// reaching into the Audio assembly. Always call via the null-conditional
        /// <c>CoreServices.Audio?.PlayUiClick()</c>.
        /// </summary>
        void PlayUiClick();
    }
}
