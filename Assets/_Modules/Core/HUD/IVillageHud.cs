namespace DeNelle.Core.HUD
{
    public interface IVillageHud
    {
        void SetWave(int waveNumber);
        void SetCountdown(float secondsRemaining);
        void SetHeartHp(float normalisedHp);
        void SetCrystals(int amount);
        void SetAttackDirections(bool north, bool east, bool south, bool west);
        void SetWaveImminent(bool imminent);
        void ShowWaveClearBanner(int waveNumber, int enemiesDefeated, string flavourLine);
        void HideWaveClearBanner();
        void ShowRepairPrompt(string wallLabel, float damagePercent);
    }
}
