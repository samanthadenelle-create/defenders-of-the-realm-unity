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

        // ── Ward-tether (WO-112) — passive display only ──────────────────────────
        /// <summary>
        /// The "forgetting" intensity as the Keeper steps past the furthest lit ward,
        /// 0 (fully warm, inside reach) → 1 (the song silent, screen muted toward grey).
        /// Drives HUD desaturation / vignette / readout fade only — NEVER damage or a hard
        /// wall (WO-112 is gentle + fully reversible). Resolved via CoreServices.Hud by the
        /// Village-side WardTetherService; the HUD never reads the tether back.
        /// </summary>
        void SetForgettingLevel(float level01);

        /// <summary>
        /// Passive "Wards of the Marches" readout for the Arcane Tower panel (WO-112 §8):
        /// how many wards are lit and how far the song now reaches. Fed by WardTetherService
        /// through CoreServices — the Tower reads the tether, never the reverse. A no-op-safe
        /// summary string; the HUD chooses whether/where to surface it.
        /// </summary>
        void SetWardsReadout(int wardsLit, int wardsTotal, string summary);
    }
}
