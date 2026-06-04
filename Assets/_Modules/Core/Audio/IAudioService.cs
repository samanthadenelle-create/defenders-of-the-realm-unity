using UnityEngine;
namespace DeNelle.Core.Audio
{
    public interface IAudioService
    {
        void PlaySfx(AudioClip clip, float volume);
        void PlayMusic(MusicTrack track);
    }
}
