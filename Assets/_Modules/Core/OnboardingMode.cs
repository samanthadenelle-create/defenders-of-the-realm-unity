// =============================================================================
// OnboardingMode — the FAST-PATH vs FULL-TUTORIAL switch for a new game.
// -----------------------------------------------------------------------------
// Owner directive (2026-06-06): "fast into battle + a little story; lumpy first
// minutes = I leave." The default "Start New" must drop the player into combat in
// ~20-40s with only a BRIEF companion hook — NOT the full 7-scene FTUE / 9-screen
// cinematic, which front-loads 3-5 minutes of teaching before the first wave.
//
// This is a tiny cross-module switch. TitleController (DeNelle.Onboarding) sets it
// when the player taps a splash button; TutorialDirector / CompanionMeetingTrigger
// (DeNelle.Village) read it to choose the brief hook vs the full meeting. It lives
// in DeNelle.Core because that is the only assembly both Onboarding and Village
// reference (mirrors IntroLauncher's decoupling-hook pattern).
//
// PERSISTENCE: also mirrored to PlayerPrefs so the choice survives the scene swap
// from Title → (PetSelect) → Village even though this static resets per process
// only at app launch. The PlayerPrefs key is the source of truth a fresh Village
// scene reads; the static is the fast in-process cache.
// =============================================================================

using UnityEngine;

namespace DeNelle.Core
{
    /// <summary>
    /// Selects the new-game onboarding flavour: the default FAST PATH (brief hook
    /// then straight into Wave 1) or the FULL tutorial (the 7-scene FTUE / cinematic
    /// intro). Set by the Title splash buttons, read by the Village tutorial host.
    /// </summary>
    public static class OnboardingMode
    {
        private const string FullTutorialKey = "onboarding.fullTutorial";

        // In-process cache. Default = false = FAST PATH (the owner-mandated default).
        private static bool _full;
        private static bool _loaded;

        /// <summary>
        /// True when the player explicitly chose the FULL tutorial / cinematic intro
        /// ("Play Intro"); false (the default) means the FAST PATH — a brief companion
        /// hook then immediately Wave 1. Reads through to PlayerPrefs so the choice
        /// survives the Title → Village scene swap.
        /// </summary>
        public static bool FullTutorial
        {
            get
            {
                if (!_loaded)
                {
                    _full = PlayerPrefs.GetInt(FullTutorialKey, 0) != 0;
                    _loaded = true;
                }
                return _full;
            }
            set
            {
                _full = value;
                _loaded = true;
                PlayerPrefs.SetInt(FullTutorialKey, value ? 1 : 0);
                PlayerPrefs.Save();
            }
        }

        /// <summary>True (the default) when the new game should take the fast path.</summary>
        public static bool FastPath => !FullTutorial;

        /// <summary>The Title "Start New" button — take the fast path into battle.</summary>
        public static void ChooseFastPath() => FullTutorial = false;

        /// <summary>The Title "Play Intro" button — take the full tutorial / cinematic.</summary>
        public static void ChooseFullTutorial() => FullTutorial = true;
    }
}
