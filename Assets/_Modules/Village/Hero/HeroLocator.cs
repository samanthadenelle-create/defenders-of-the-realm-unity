// =============================================================================
// HeroLocator (WO-1513 follow-up) — the ONE place that answers "where is the hero".
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHY THIS FILE EXISTS. Resolving the hero was copy-pasted into three call sites
// (GateIntelHud.EnsureHero, SceneTransitionTrigger.ResolveHero,
// HubSpawnInjector.ResolveHero) and each copy carried its own version of the
// dead-tag history. Two of the three now route here; HubSpawnInjector is
// DELIBERATELY UNTOUCHED by this edit-only lane (it was not a gate offender, and
// changing a spawn-injection path unverified is not worth the tidiness) — its
// exact shape is preserved in ResolveLocomotion below so the swap is a one-liner
// whenever someone can run the gate. That is duplicated state, and it is how the
// "HeroTarget" tag survived in two of them long after TagManager.asset stopped
// declaring it (CLAUDE.md §7: the declared tags are Tower/Building/HeartTarget/
// Player, and FindGameObjectsWithTag THROWS on an undeclared tag).
//
// THE RULE (CLAUDE.md §7): the hero carries the "Player" tag AND a HeroLocomotion
// component. The tag is the cheap path; the component is the definitive fallback.
// No other tag is ever consulted.
//
// SECOND REASON, and it is a real one: presentation must not scan the scene for
// game objects itself. Assets/Editor/Regression/UiMvvmConformanceRegression.cs
// bans FindFirstObjectByType inside any file that CONSTRUCTS uGUI. GateIntelHud
// builds a proximity label and SceneTransitionTrigger builds a fade overlay, so
// both are classified as Views by that oracle even though neither is a panel.
// Asking a resolver for a Transform is the seam the architecture already wants
// (a View is handed what it needs; it does not go looking), so the two call sites
// route here instead of earning an allow-list row.
//
// Null-safe: every accessor returns null rather than throwing when no hero is in
// the scene (loading screens, menu scenes, headless boot).
// =============================================================================
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>Single seam for locating the live hero. Tag first, component as the
    /// definitive fallback. Returns null when no hero exists in the loaded scenes.</summary>
    public static class HeroLocator
    {
        /// <summary>The canonical hero tag (WO-450). Declared in TagManager.asset.</summary>
        private const string HeroTag = "Player";

        /// <summary>Resolves the hero's root GameObject, or null when none is loaded.</summary>
        public static GameObject ResolveGameObject()
        {
            GameObject tagged = null;
            try { tagged = GameObject.FindWithTag(HeroTag); }
            catch (UnityException) { tagged = null; }   // tag not declared in this project state
            if (tagged != null) return tagged;

            var loco = Object.FindFirstObjectByType<HeroLocomotion>();
            return loco != null ? loco.gameObject : null;
        }

        /// <summary>Resolves the hero's transform, or null when no hero is loaded.</summary>
        public static Transform ResolveTransform()
        {
            var go = ResolveGameObject();
            return go != null ? go.transform : null;
        }

        /// <summary>Resolves the hero's HeroLocomotion, or null when no hero is loaded.
        /// Falls through to the scene scan when the tagged object carries no locomotion,
        /// so this is a true drop-in for HubSpawnInjector.ResolveHero (see header).</summary>
        public static HeroLocomotion ResolveLocomotion()
        {
            GameObject tagged = null;
            try { tagged = GameObject.FindWithTag(HeroTag); }
            catch (UnityException) { tagged = null; }
            if (tagged != null)
            {
                var onTag = tagged.GetComponent<HeroLocomotion>();
                if (onTag != null) return onTag;
            }
            return Object.FindFirstObjectByType<HeroLocomotion>();
        }
    }
}
