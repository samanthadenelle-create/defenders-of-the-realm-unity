// =============================================================================
// SeekerBootstrap — runtime frame-pacing + Seeker device auto-detect.
// -----------------------------------------------------------------------------
// Mobile-performance audit P0-6 (§1.5): nothing in the project reads
// SystemInfo.deviceModel to pick a quality tier, and nothing sets
// Application.targetFrameRate — so the build runs uncapped or vsync-locked to
// the display's 120 Hz, which wastes battery and makes the Week-8 "60 FPS held"
// gate unmeasurable.
//
// This is the runtime counterpart to the editor-side MobileSettings.cs: that
// script builds the Seeker_Low / Seeker_High / Desktop quality tiers; this one
// SELECTS one at startup and enforces its frame budget.
//
// It runs automatically — no scene wiring — via [RuntimeInitializeOnLoadMethod]
// (BeforeSceneLoad), so it is in force before the Title scene loads:
//   1. Reads SystemInfo.deviceModel; a Solana Seeker (or any Android phone)
//      gets Seeker_High, weaker/headless hardware gets Seeker_Low, a desktop
//      player gets Desktop.
//   2. Switches QualitySettings to that tier (if it exists — MobileSettings.cs
//      must have been run; if the named tiers are absent it logs and leaves the
//      current tier alone).
//   3. Sets QualitySettings.vSyncCount = 0 so Application.targetFrameRate is
//      authoritative, then sets targetFrameRate to the tier's target (30/60).
//
// Lives in DeNelle.Core so it loads with the core assembly and has no
// dependency on any gameplay module.
// =============================================================================

using UnityEngine;

namespace DeNelle.Core
{
    /// <summary>
    /// Startup frame-pacing and quality-tier selection. Auto-runs before the
    /// first scene loads — see <see cref="Init"/>. Audit P0-6 / §1.5.
    /// </summary>
    public static class SeekerBootstrap
    {
        /// <summary>The named quality tiers built by the editor-side MobileSettings.cs.</summary>
        public const string TierSeekerLow = "Seeker_Low";
        /// <summary>Default tier for a Seeker / Android phone.</summary>
        public const string TierSeekerHigh = "Seeker_High";
        /// <summary>Desktop / Vercel-parity tier.</summary>
        public const string TierDesktop = "Desktop";

        /// <summary>Frame-rate cap for the Seeker_Low tier (audit §1.4).</summary>
        public const int FpsSeekerLow = 30;
        /// <summary>Frame-rate cap for Seeker_High / Desktop (audit §1.4).</summary>
        public const int FpsSeekerHigh = 60;

        /// <summary>The quality tier this run selected. Empty until <see cref="Init"/> runs.</summary>
        public static string SelectedTier { get; private set; } = string.Empty;

        /// <summary>True when the running hardware looks like a Solana Seeker.</summary>
        public static bool IsSeeker { get; private set; }

        /// <summary>
        /// Auto-invoked by Unity before the first scene loads. Selects the
        /// quality tier from the device model and enforces its frame budget.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        public static void Init()
        {
            string model = SystemInfo.deviceModel ?? string.Empty;
            IsSeeker = LooksLikeSeeker(model);

            // Tier selection (audit §1.5):
            //   - a Seeker, or any Android handheld, targets Seeker_High;
            //   - non-Android (desktop player / Vercel-parity EXE) targets Desktop;
            //   - a weak/unknown Android device falls back to Seeker_Low.
            string tier;
            if (Application.platform == RuntimePlatform.Android)
                tier = (IsSeeker || IsCapableAndroidDevice()) ? TierSeekerHigh : TierSeekerLow;
            else if (Application.isMobilePlatform)
                tier = TierSeekerHigh;            // iOS etc. — treat as a phone tier
            else
                tier = TierDesktop;               // desktop / editor

            ApplyTier(tier, model);
        }

        /// <summary>
        /// Switches to the named quality tier (if present) and sets the
        /// frame-rate cap. Public + idempotent so a future settings screen can
        /// re-invoke it when the player changes the tier manually.
        /// </summary>
        public static void ApplyTier(string tierName, string deviceModel = null)
        {
            SelectedTier = tierName;

            // Switch QualitySettings to the named tier. The tiers are created by
            // the editor-side MobileSettings.cs; if it has not been run the
            // named tier is absent — log it and leave the current tier alone
            // rather than guessing an index.
            int tierIndex = System.Array.IndexOf(QualitySettings.names, tierName);
            if (tierIndex >= 0)
            {
                if (QualitySettings.GetQualityLevel() != tierIndex)
                    QualitySettings.SetQualityLevel(tierIndex, applyExpensiveChanges: true);
            }
            else
            {
                Debug.LogWarning($"[SeekerBootstrap] Quality tier '{tierName}' not found " +
                                 "(run Defenders/Setup/Apply Mobile Settings to create the " +
                                 "Seeker_Low / Seeker_High / Desktop tiers). Frame cap still applied.");
            }

            // vSync OFF so Application.targetFrameRate is authoritative — audit
            // §1.5. With vSync on, targetFrameRate is ignored and the build runs
            // at the display's refresh (120 Hz on the Seeker).
            QualitySettings.vSyncCount = 0;

            int targetFps = TargetFpsFor(tierName);
            Application.targetFrameRate = targetFps;

            Debug.Log($"[SeekerBootstrap] device='{deviceModel ?? SystemInfo.deviceModel}' " +
                      $"platform={Application.platform} isSeeker={IsSeeker} -> " +
                      $"tier='{tierName}'{(tierIndex < 0 ? " (NOT APPLIED — tier missing)" : "")}, " +
                      $"vSyncCount=0, targetFrameRate={targetFps}.");
        }

        /// <summary>The frame-rate cap for a tier name (audit §1.4 table).</summary>
        public static int TargetFpsFor(string tierName)
        {
            return tierName == TierSeekerLow ? FpsSeekerLow : FpsSeekerHigh;
        }

        // ── Device detection ─────────────────────────────────────────────────

        /// <summary>
        /// Basic Solana Seeker check against <c>SystemInfo.deviceModel</c>.
        /// The Seeker reports an OSOM/Solana-Mobile device string; "saga" is
        /// also matched as the Seeker's predecessor in the same product line.
        /// Case-insensitive and deliberately loose — the cost of a wrong guess
        /// is only the starting tier, which a settings screen can override.
        /// </summary>
        public static bool LooksLikeSeeker(string deviceModel)
        {
            if (string.IsNullOrEmpty(deviceModel)) return false;
            string m = deviceModel.ToLowerInvariant();
            return m.Contains("seeker")
#if !GOOGLE_PLAY
                // WO-1363: the vendor string is a shipping literal. The Play artifact keeps
                // "seeker"/"osom"/"saga", which already identify every device in this product
                // line, so the quality tier still resolves correctly on the hardware.
                || m.Contains("solana")
#endif
                || m.Contains("osom")
                || m.Contains("saga");
        }

        /// <summary>
        /// Heuristic for "this Android device can handle Seeker_High" when it is
        /// not a recognised Seeker — used so generic modern phones still get the
        /// 60-FPS tier. Gates on a rough memory / processor-count floor; a
        /// device below it falls back to Seeker_Low.
        /// </summary>
        private static bool IsCapableAndroidDevice()
        {
            // ~3 GB system RAM and a 6-core SoC is a comfortable floor for the
            // 60-FPS Seeker_High tier; below that, Seeker_Low (30 FPS) is safer.
            return SystemInfo.systemMemorySize >= 3000
                && SystemInfo.processorCount >= 6;
        }
    }
}
