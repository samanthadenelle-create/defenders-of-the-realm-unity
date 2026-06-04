namespace DeNelle.Core.Audio
{
    // Title = 6 appended at the END so existing serialized values (save data /
    // ambient-choice PlayerPrefs) keep their indices. DEF-228: the cold-open intro
    // needs to prime the TITLE theme (title.mp3), not fall back to Overworld.
    public enum MusicTrack { Village = 0, Battle = 1, Victory = 2, Dungeon = 3, Overworld = 4, Defeat = 5, Title = 6 }
}
