namespace DeNelle.Core.HUD
{
    public interface IVillageHud
    {
        void SetWave(int waveNumber);
        void SetCountdown(float secondsRemaining);
        void SetHeartHp(float current, float maxHp);
        void SetCrystals(int amount);

        /// <summary>
        /// Pushes the full build-economy wallet onto the on-screen resource bar:
        /// Wood / Iron / Food / Gems(Crystals). Mirrors <see cref="SetCrystals"/> but
        /// for all four banked totals players spend on building/tower upgrades — fed
        /// by the Village-side HeartHudBridge from EconomyService.Snapshot. The Gems
        /// slot reuses the existing crystal counter (so it never double-counts).
        /// </summary>
        void SetResources(int wood, int iron, int food, int gems);
        void SetAttackDirections(bool north, bool east, bool south, bool west);
        void SetWaveImminent(bool imminent);
        /// <summary>
        /// ⚠ WO-1309 — DELIBERATELY CALLER-LESS AS OF 2026-09-02. NOT DEAD BY ACCIDENT.
        ///
        /// The wave-clear announcement is the END-STATE MODAL (WaveCelebrationManager ->
        /// EndStateView.Show(EndStateVM.FromWaveClear)), which carries the spoils rows, the
        /// damage rows and the Repair CTA. This push seam raised a SECOND, thinner
        /// announcement of the same WaveManager.OnWaveCleared event, and its one caller
        /// (WaveFeedbackDirector.OnWaveCleared) was passing the player's CRYSTAL BALANCE as
        /// `enemiesDefeated` — the owner's felt-test screenshot read "400 foes defeated" over
        /// her 400 crystals. That caller is cut; see the block comment there.
        ///
        /// The member is KEPT rather than removed because it is a published contract on a
        /// Core interface and removing it is a wider architectural change than this ticket
        /// was scoped for (owner not consulted). Nothing calls it today. Before wiring
        /// anything new to it, settle the duplicate-announcement question first, and NEVER
        /// pass a wallet balance into `enemiesDefeated`.
        /// </summary>
        void ShowWaveClearBanner(int waveNumber, int enemiesDefeated, string flavourLine);

        /// <summary>Paired dismiss for <see cref="ShowWaveClearBanner"/>; likewise caller-less (WO-1309).</summary>
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
