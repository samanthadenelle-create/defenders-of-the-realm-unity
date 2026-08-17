// =============================================================================
// SettingsModel — the persisted options store + apply layer (audit P0-8).
// -----------------------------------------------------------------------------
// The data + persistence layer behind the Settings screen. The missing-
// components audit §2.1 calls out "no settings menu anywhere"; this is the
// store the menu reads and writes.
//
// PERSISTENCE — a deliberate split, both durable across launches:
//   * Audio MUSIC + SFX volumes and the global MUTE toggle already have first-
//     class fields in the Core GameState SO (#20/#21/#22) and typed setters on
//     GameStateService. Those route through GameStateService so they land in
//     the canonical `dotr-save` blob and stay consistent with the rest of the
//     save layer. (GameState stores them 0..100; the UI works in 0..1.5 — this
//     model converts.)
//   * MASTER volume, the QUALITY TIER and the SCREEN-SHAKE toggle have NO
//     GameState field — adding fields to the 41-field persisted save schema is
//     a Core/SaveSchema change out of this task's scope. They persist to
//     PlayerPrefs under their own keys instead. PlayerPrefs is the same backing
//     store GameStateService itself uses, so this is consistent, not a
//     parallel system — just a lighter-weight slot for options the save schema
//     does not model.
//
// APPLY — Apply() / ApplyAll() push the live values at their targets:
//   * audio volumes  -> AudioMixerBridge (the parallel-Audio seam; no-ops
//                       safely until the mixer asset exists).
//   * quality tier   -> SeekerBootstrap.ApplyTier (public + idempotent — its
//                       header explicitly invites a settings screen to call it).
//   * screen-shake   -> ScreenShakeSetting.Enabled (the typed static) AND the
//                       PlayerPrefs key "camerashake", which is what
//                       DeNelle.Village's CameraShakeBridge actually gates on.
//                       Both, because the assemblies cannot see each other — see
//                       the long note on ApplyScreenShake().
//
// ApplyAllOnLaunch() is invoked once at startup by SettingsBootstrap so settings
// REAPPLY ON LAUNCH (audit requirement).
//
// Lives in DeNelle.Settings; references DeNelle.Core only — no gameplay module.
// =============================================================================

using DeNelle.Core;
using DeNelle.Core.State;
using UnityEngine;

namespace DeNelle.Settings
{
    /// <summary>
    /// The graphics quality tiers the player can pick. Mirrors the three named
    /// QualitySettings tiers <see cref="SeekerBootstrap"/> manages.
    /// </summary>
    public enum QualityTier
    {
        /// <summary>30 FPS, lightest render path — weak Android hardware.</summary>
        SeekerLow = 0,
        /// <summary>60 FPS, the Android default tier — Seeker / capable phones.</summary>
        SeekerHigh = 1,
        /// <summary>60 FPS, richest render path — desktop / EXE build.</summary>
        Desktop = 2,
    }

    /// <summary>
    /// Reads / writes / applies every player option. A plain static surface —
    /// no MonoBehaviour, no scene presence — so any screen (Settings, the pause
    /// overlay, a future title-screen options entry) can use it directly.
    /// </summary>
    public static class SettingsModel
    {
        // ── PlayerPrefs keys (for options GameState's schema does not model) ──
        private const string MasterVolumeKey = "dotr-settings-master-volume";
        private const string QualityTierKey = "dotr-settings-quality-tier";
        private const string ScreenShakeKey = "dotr-settings-screen-shake";

        // ── Defaults ──────────────────────────────────────────────────────────
        /// <summary>Fresh master volume — 1.0 = unity gain (audio-mix-spec §2).</summary>
        public const float DefaultMasterVolume = 1.0f;
        /// <summary>Fresh music volume on the 0..1.5 UI scale (GameState fresh = 70/100).</summary>
        public const float DefaultMusicVolume = 0.70f;
        /// <summary>Fresh sfx volume on the 0..1.5 UI scale (GameState fresh = 80/100).</summary>
        public const float DefaultSfxVolume = 0.80f;
        /// <summary>Screen-shake on by default — a player who wants it off opts out.</summary>
        public const bool DefaultScreenShake = true;

        /// <summary>The slider range maximum (audio-mix-spec §2 — push past unity gain).</summary>
        public const float MaxVolume = AudioMixerBridge.MaxLinear;

        // GameState stores music/sfx on a 0..100 scale; the UI works 0..1.5.
        private const float GameStateVolumeScale = 100f;

        // =====================================================================
        //  Master volume — PlayerPrefs (no GameState field)
        // =====================================================================

        /// <summary>Master volume, 0..<see cref="MaxVolume"/>. Scales every audio group.</summary>
        public static float MasterVolume
        {
            get => Mathf.Clamp(
                PlayerPrefs.GetFloat(MasterVolumeKey, DefaultMasterVolume), 0f, MaxVolume);
            set
            {
                PlayerPrefs.SetFloat(MasterVolumeKey, Mathf.Clamp(value, 0f, MaxVolume));
                PlayerPrefs.Save();
            }
        }

        // =====================================================================
        //  Music + SFX volume — routed through GameState (0..100 <-> 0..1.5)
        // =====================================================================

        /// <summary>
        /// Music volume on the 0..<see cref="MaxVolume"/> UI scale. Backed by
        /// <see cref="GameState.MusicVolume"/> (0..100); the setter routes through
        /// <see cref="GameStateService.SetMusicVolume"/> so it lands in the save.
        /// </summary>
        public static float MusicVolume
        {
            get
            {
                var s = GameStateService.Instance;
                float stored = s != null ? s.State.MusicVolume : DefaultMusicVolume * GameStateVolumeScale;
                return Mathf.Clamp(stored / GameStateVolumeScale, 0f, MaxVolume);
            }
            set
            {
                var clamped = Mathf.Clamp(value, 0f, MaxVolume);
                GameStateService.Instance?.SetMusicVolume(clamped * GameStateVolumeScale);
            }
        }

        /// <summary>
        /// SFX volume on the 0..<see cref="MaxVolume"/> UI scale. Backed by
        /// <see cref="GameState.SfxVolume"/> (0..100); routes through
        /// <see cref="GameStateService.SetSfxVolume"/>.
        /// </summary>
        public static float SfxVolume
        {
            get
            {
                var s = GameStateService.Instance;
                float stored = s != null ? s.State.SfxVolume : DefaultSfxVolume * GameStateVolumeScale;
                return Mathf.Clamp(stored / GameStateVolumeScale, 0f, MaxVolume);
            }
            set
            {
                var clamped = Mathf.Clamp(value, 0f, MaxVolume);
                GameStateService.Instance?.SetSfxVolume(clamped * GameStateVolumeScale);
            }
        }

        /// <summary>
        /// Global audio mute. Backed by <see cref="GameState.Muted"/> (#20) — note
        /// the fresh-game default there is <c>true</c> (a11y: a new visitor starts
        /// muted). Routes through <see cref="GameStateService.SetMuted"/>.
        /// </summary>
        public static bool Muted
        {
            get => GameStateService.Instance?.State.Muted ?? true;
            set => GameStateService.Instance?.SetMuted(value);
        }

        // =====================================================================
        //  Difficulty — routed through GameState (#23, settingsSlice)
        // =====================================================================

        /// <summary>
        /// Gameplay difficulty (Easy / Normal / Hard). Backed by the first-class
        /// <see cref="GameState.Difficulty"/> save field (#23) — the setter routes
        /// through <see cref="GameStateService.SetDifficulty"/> so the choice lands
        /// in the canonical <c>dotr-save</c> blob and survives a relaunch.
        ///
        /// Difficulty currently controls the WaveManager between-wave countdown
        /// (see <see cref="DifficultyTuning"/>): Easy ~10 min, Normal ~5 min,
        /// Hard ~3 min between waves. Fresh-save default = Normal.
        /// </summary>
        public static Difficulty Difficulty
        {
            get => GameStateService.Instance?.State?.Difficulty ?? Difficulty.Normal;
            set => GameStateService.Instance?.SetDifficulty(value);
        }

        // =====================================================================
        //  Quality tier — PlayerPrefs (no GameState field)
        // =====================================================================

        /// <summary>
        /// The player-selected graphics quality tier. The fresh default is read
        /// from whatever tier <see cref="SeekerBootstrap"/> auto-picked for the
        /// hardware, so a first launch shows the right tier without the player
        /// having chosen one.
        /// </summary>
        public static QualityTier Quality
        {
            get
            {
                int stored = PlayerPrefs.GetInt(QualityTierKey, -1);
                if (stored >= 0 && stored <= (int)QualityTier.Desktop)
                    return (QualityTier)stored;
                return TierFromName(SeekerBootstrap.SelectedTier);
            }
            set
            {
                PlayerPrefs.SetInt(QualityTierKey, (int)value);
                PlayerPrefs.Save();
            }
        }

        /// <summary>True once the player has explicitly chosen a tier (vs. the auto-detected default).</summary>
        public static bool HasExplicitQuality => PlayerPrefs.GetInt(QualityTierKey, -1) >= 0;

        // =====================================================================
        //  Screen-shake — PlayerPrefs (no GameState field)
        // =====================================================================

        /// <summary>
        /// Whether camera screen-shake effects are enabled. An accessibility /
        /// comfort option (audit §2.7 reduce-motion family). Gameplay reads the
        /// PlayerPrefs key <c>"camerashake"</c> (via CameraShakeBridge);
        /// <see cref="ScreenShakeSetting.Enabled"/> is the typed mirror for any
        /// in-assembly reader. <see cref="ApplyScreenShake"/> publishes to both —
        /// a write to this property alone does NOT reach gameplay.
        /// </summary>
        public static bool ScreenShake
        {
            get => PlayerPrefs.GetInt(ScreenShakeKey, DefaultScreenShake ? 1 : 0) != 0;
            set
            {
                PlayerPrefs.SetInt(ScreenShakeKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        // =====================================================================
        //  Apply — push the stored values at their live targets
        // =====================================================================

        /// <summary>
        /// Re-pushes every setting at its live target — audio onto the mixer,
        /// quality onto QualitySettings, screen-shake onto its static flag.
        /// Called once at launch by <see cref="SettingsBootstrap"/> and after any
        /// bulk change.
        /// </summary>
        public static void ApplyAll()
        {
            ApplyAudio();
            ApplyQuality();
            ApplyScreenShake();
        }

        /// <summary>
        /// Pushes the three volumes onto the AudioMixer through
        /// <see cref="AudioMixerBridge"/>. Mute collapses every group to silence
        /// without disturbing the stored slider values, so un-muting restores them.
        /// </summary>
        public static void ApplyAudio()
        {
            bool muted = Muted;
            AudioMixerBridge.SetMaster(muted ? 0f : MasterVolume);
            AudioMixerBridge.SetMusic(muted ? 0f : MusicVolume);
            AudioMixerBridge.SetSfx(muted ? 0f : SfxVolume);
        }

        /// <summary>
        /// Applies the stored quality tier through
        /// <see cref="SeekerBootstrap.ApplyTier"/> (idempotent — switches the
        /// QualitySettings level and re-sets the frame cap).
        /// </summary>
        public static void ApplyQuality()
        {
            SeekerBootstrap.ApplyTier(TierName(Quality));
        }

        /// <summary>
        /// Publishes the stored screen-shake flag to BOTH consumers: the
        /// <see cref="ScreenShakeSetting.Enabled"/> static, and the PlayerPrefs key
        /// <c>"camerashake"</c> that the gameplay shake bridge actually reads.
        /// </summary>
        public static void ApplyScreenShake()
        {
            ScreenShakeSetting.Enabled = ScreenShake;

            // ── THE KEY IS THE SEAM. DO NOT "CLEAN THIS UP" INTO A DIRECT CALL. ──
            // DeNelle.Village.CameraShakeBridge (Tower.cs) is the single entry point for every
            // gameplay shake, and it gates on PlayerPrefs.GetInt("camerashake", 1). A direct type
            // reference from here WILL NOT COMPILE: DeNelle.Village.asmdef does not reference
            // DeNelle.Settings, and DeNelle.Settings references only DeNelle.Core — neither side
            // can see the other. The shared PlayerPrefs key is therefore the correct, deliberate
            // seam between the settings layer and the gameplay layer, exactly as CoreServices is
            // for typed calls.
            //
            // WHY THIS LINE EXISTS (defect fixed 2026-08-16): the toggle was INERT IN BOTH
            // DIRECTIONS. The settings UI wrote "dotr-settings-screen-shake" and mirrored it onto
            // ScreenShakeSetting.Enabled, which NOTHING in Assets read; the bridge read
            // "camerashake", which NOTHING wrote. So the accessibility control moved no shake at
            // all, and the ~15 shake sites funnelling through the bridge could never be turned off.
            PlayerPrefs.SetInt("camerashake", ScreenShake ? 1 : 0);
            PlayerPrefs.Save();
        }

        // =====================================================================
        //  Reset to defaults
        // =====================================================================

        /// <summary>
        /// Restores every option to its fresh default and re-applies. Mute is
        /// reset to <c>false</c> here (a player on the Settings screen is past the
        /// first-visit a11y mute), distinct from the fresh-save default of true.
        /// </summary>
        public static void ResetToDefaults()
        {
            MasterVolume = DefaultMasterVolume;
            MusicVolume = DefaultMusicVolume;
            SfxVolume = DefaultSfxVolume;
            Muted = false;
            Quality = TierFromName(SeekerBootstrap.SelectedTier);
            ScreenShake = DefaultScreenShake;
            // Difficulty resets to the fresh-save default (Normal). Unlike a New
            // Game (GameStateService.Reset keeps Difficulty as a preference),
            // an explicit "Reset to defaults" on the options screen restores it.
            Difficulty = Difficulty.Normal;
            ApplyAll();
        }

        // =====================================================================
        //  Quality-tier <-> name mapping
        // =====================================================================

        /// <summary>The QualitySettings tier name for a <see cref="QualityTier"/>.</summary>
        public static string TierName(QualityTier tier)
        {
            switch (tier)
            {
                case QualityTier.SeekerLow: return SeekerBootstrap.TierSeekerLow;
                case QualityTier.Desktop: return SeekerBootstrap.TierDesktop;
                default: return SeekerBootstrap.TierSeekerHigh;
            }
        }

        /// <summary>The <see cref="QualityTier"/> for a QualitySettings tier name (defaults to SeekerHigh).</summary>
        public static QualityTier TierFromName(string name)
        {
            if (name == SeekerBootstrap.TierSeekerLow) return QualityTier.SeekerLow;
            if (name == SeekerBootstrap.TierDesktop) return QualityTier.Desktop;
            return QualityTier.SeekerHigh;
        }

        /// <summary>A short player-facing label for a tier (shown on the selector).</summary>
        public static string TierLabel(QualityTier tier)
        {
            switch (tier)
            {
                case QualityTier.SeekerLow: return "Low (30 FPS)";
                case QualityTier.Desktop: return "Desktop (60 FPS)";
                default: return "High (60 FPS)";
            }
        }
    }

    /// <summary>
    /// A tiny static flag for deciding whether to apply screen-shake. Kept separate
    /// from <see cref="SettingsModel"/> so a caller can check it without taking a
    /// dependency on the settings persistence layer — it only needs this one bool.
    /// <para>
    /// REACH, honestly stated: this static is only visible to assemblies that
    /// REFERENCE DeNelle.Settings. DeNelle.Village does not, so the gameplay shake
    /// sites cannot read it and instead gate on the PlayerPrefs key "camerashake",
    /// which <see cref="SettingsModel.ApplyScreenShake"/> writes alongside this flag.
    /// If you are adding a shake caller inside DeNelle.Village, use
    /// CameraShakeBridge — do NOT try to reference this type.
    /// </para>
    /// </summary>
    public static class ScreenShakeSetting
    {
        /// <summary>
        /// True when screen-shake camera effects are permitted. Mirrored from
        /// <see cref="SettingsModel.ScreenShake"/> by <c>ApplyScreenShake</c>;
        /// defaults to enabled until settings are applied.
        /// </summary>
        public static bool Enabled { get; set; } = SettingsModel.DefaultScreenShake;
    }
}
