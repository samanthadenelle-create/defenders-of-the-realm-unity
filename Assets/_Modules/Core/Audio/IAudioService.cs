using UnityEngine;
namespace DeNelle.Core.Audio
{
    public interface IAudioService
    {
        void PlaySfx(AudioClip clip, float volume);
        void PlayMusic(MusicTrack track);

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
