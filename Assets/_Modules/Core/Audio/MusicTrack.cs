namespace DeNelle.Core.Audio
{
    // Title = 6 appended at the END so existing serialized values (save data /
    // ambient-choice PlayerPrefs) keep their indices. DEF-228: the cold-open intro
    // needs to prime the TITLE theme (title.mp3), not fall back to Overworld.
    // Arena = 7 appended at the END (same index-preservation rule): the Arena raid
    // BGM ("Echo's theme", echo_theme.mp3) — soft, looping background for a raid.
    // Raid = 8 appended at the END: the offensive-raid BGM ("brass-rampart.mp3",
    // WO-453) — driving brass for marching a troop army on an enemy fortress.
    public enum MusicTrack { Village = 0, Battle = 1, Victory = 2, Dungeon = 3, Overworld = 4, Defeat = 5, Title = 6, Arena = 7, Raid = 8 }
}
