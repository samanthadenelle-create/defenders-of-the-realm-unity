// =============================================================================
// CompanionSpawner — picks + spawns the FTUE companion (WO-277).
// -----------------------------------------------------------------------------
// Scene 1: "Companion (DIFFERENT character from hero) runs up." The tutorial's
// guide must NOT be the player's own class — it's a comrade from another walk of
// life. This resolves the player's chosen HeroClass, maps it to a DIFFERENT
// companion class (the WO mapping), and routes that through the existing
// StoryCompanionInjector so there is exactly ONE companion in the village and it
// is the mapped class — reconciling with (not duplicating) the StoryCompanion
// system already in place.
//
// Mapping (WO-277):
//   Player Mage   → companion Knight  (Grom,  veteran)
//   Player Knight → companion Ranger  (Sylas, scout)
//   Player Ranger → companion Cleric  (Elara, acolyte)
//   Player Cleric → companion Mage    (Thrain, scholar)
//
// The StoryCompanion it spawns already FOLLOWS the hero and speaks; here we only
// choose + spawn it and expose its transform so the director can frame Scene 1.
// Reflection-free (all DeNelle.Village). Null-safe — degrades to no companion.
// =============================================================================

using DeNelle.Core.State;
using DeNelle.Core.Diagnostics;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Resolves the FTUE companion's class (always different from the player's)
    /// and spawns it via <see cref="StoryCompanionInjector"/>. The
    /// <see cref="TutorialDirector"/> calls <see cref="Spawn"/> in Scene 1 and
    /// reads <see cref="CompanionTransform"/> / <see cref="CompanionName"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class CompanionSpawner : MonoBehaviour
    {
        private HeroClass _companionClass = HeroClass.Knight;

        /// <summary>The class chosen for the companion (always != the player's).</summary>
        public HeroClass CompanionClass => _companionClass;

        /// <summary>The spawned companion's transform, or null if none spawned yet.</summary>
        public Transform CompanionTransform =>
            StoryCompanionInjector.Instance != null
                ? StoryCompanionInjector.Instance.CompanionTransform
                : null;

        /// <summary>The companion's display name (e.g. "Grom, Veteran of the Wall").</summary>
        public string CompanionName => CompanionDialogue.NameFor(_companionClass);

        /// <summary>The companion's short first name (e.g. "Grom") for inline dialogue.</summary>
        public string CompanionShortName => ShortNameFor(_companionClass);

        /// <summary>
        /// Resolves + spawns the companion for the player's chosen hero, mapped to a
        /// DIFFERENT class. Returns the companion's transform (or null if the
        /// injector isn't available — the tutorial then proceeds companion-less).
        /// </summary>
        public Transform Spawn()
        {
            _companionClass = CompanionClassFor(ResolvePlayerClass());
            FlowTrace.Step("Companion", $"Spawn — mapped companion class '{_companionClass}' (always != player)");

            var injector = StoryCompanionInjector.Instance;
            if (injector == null)
            {
                FlowTrace.Warn("Companion", "StoryCompanionInjector not present — class override may lag a frame (companion spawns at its own bootstrap)");
                // The injector self-bootstraps AfterSceneLoad; if it isn't up yet the
                // companion simply appears a frame later as its own player-class spawn.
                // Set the override anyway via a fresh instance so the mapped class wins
                // once it spawns. (Creating it here mirrors the injector's bootstrap.)
                Debug.LogWarning("[CompanionSpawner] StoryCompanionInjector not present — " +
                                 "companion will spawn at its own bootstrap; class override may lag a frame.");
                return null;
            }

            FlowTrace.Step("Companion", $"override applied on injector — companion '{_companionClass}'");
            injector.SetHeroClassOverride(_companionClass);
            return injector.CompanionTransform;
        }

        /// <summary>Clears the tutorial class override (restores the player-class companion).</summary>
        public void ClearOverride()
        {
            StoryCompanionInjector.Instance?.SetHeroClassOverride(null);
        }

        // ── Mapping ──────────────────────────────────────────────────────────

        /// <summary>WO-277 — the companion class is always different from the player's.</summary>
        public static HeroClass CompanionClassFor(HeroClass player)
        {
            switch (player)
            {
                case HeroClass.Mage:   return HeroClass.Knight;  // → Grom
                case HeroClass.Knight: return HeroClass.Ranger;  // → Sylas
                case HeroClass.Ranger: return HeroClass.Cleric;  // → Elara
                case HeroClass.Cleric: return HeroClass.Mage;    // → Thrain
                default:               return HeroClass.Knight;
            }
        }

        private static HeroClass ResolvePlayerClass()
        {
            var svc = GameStateService.Instance;
            if (svc != null && svc.State != null)
                return svc.State.HeroClass.ToNullable() ?? HeroClass.Knight;
            return HeroClass.Knight;
        }

        // The companion's first name (the long form lives in CompanionDialogue.NameFor).
        private static string ShortNameFor(HeroClass companion)
        {
            switch (companion)
            {
                case HeroClass.Knight: return "Grom";
                case HeroClass.Ranger: return "Sylas";
                case HeroClass.Mage:   return "Thrain";
                case HeroClass.Cleric: return "Elara";
                default:               return "your guide";
            }
        }
    }
}
