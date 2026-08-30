// =============================================================================
// ProgressionUnlocks -- the persisted catalog-id unlock flag store (WO-1013).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// ONE flag class: "has the player earned catalog id X yet?" -- the visible-locked
// build-palette card (BuildPaletteVM) reads it, and the earn moment (the Castle
// Defense Plans pickup) writes it. Persisted in the EXISTING GameState.SeenTutorials
// keyed store ("unlock.<catalogId>" -> true) via GameStateService.MarkTutorialSeen,
// which is the idiomatic SeenTutorials-class one-shot flag home: it is idempotent,
// Save()s in the same call, round-trips the save schema (v-any: seenTutorials is an
// open string->bool map, so NO SaveSchema field or version bump is needed), and is
// wiped by ResetToNewGame like every other progression flag.
//
// This is NOT a drop/reward framework (WO-1013 SS3): it stores booleans keyed by
// catalog id, nothing more. Do not grow it into one.
// =============================================================================

using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>
    /// Persisted "catalog id earned" flags (WO-1013). Keyed into the existing
    /// GameState.SeenTutorials store as <c>unlock.&lt;catalogId&gt;</c> -- no new
    /// save field, no schema bump. Null-safe: no GameStateService (EditMode tests /
    /// headless boots before Awake) reads as locked and refuses to write.
    /// </summary>
    public static class ProgressionUnlocks
    {
        private const string KeyPrefix = "unlock.";

        /// <summary>Raised only after a new unlock has been persisted.</summary>
        public static event System.Action<string> Changed;

        /// <summary>The SeenTutorials key an unlock flag for <paramref name="catalogId"/> lives under.</summary>
        public static string KeyFor(string catalogId) => KeyPrefix + (catalogId ?? string.Empty);

        /// <summary>True when the persisted unlock flag for <paramref name="catalogId"/> is set.
        /// No service / no state / no flag all read FALSE (locked) -- the safe default.</summary>
        public static bool IsUnlocked(string catalogId)
        {
            if (string.IsNullOrEmpty(catalogId)) return false;
            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            if (state == null || state.SeenTutorials == null) return false;
            return state.SeenTutorials.TryGetValue(KeyFor(catalogId), out var v) && v;
        }

        /// <summary>
        /// Set (and persist) the unlock flag for <paramref name="catalogId"/>. Returns TRUE
        /// only when the flag was NEWLY set -- an already-unlocked id returns false, which is
        /// what makes the plans collection once-ever idempotent. A missing service refuses
        /// (returns false) WITH a trace, never a silent no-op (SS12).
        /// </summary>
        public static bool Unlock(string catalogId)
        {
            if (string.IsNullOrEmpty(catalogId)) return false;
            if (IsUnlocked(catalogId)) return false;
            var svc = GameStateService.Instance;
            if (svc == null)
            {
                FlowTrace.Fail("Progression",
                    $"Unlock('{catalogId}') dropped: GameStateService unavailable (flag NOT persisted)");
                return false;
            }
            // MarkTutorialSeen is the idiomatic one-shot flag write: idempotent + Save().
            svc.MarkTutorialSeen(KeyFor(catalogId));
            FlowTrace.Step("Progression",
                $"unlock persisted: '{KeyFor(catalogId)}' = true (SeenTutorials store, saved)");
            Guard.Try("Progression", "unlock changed notification", () => Changed?.Invoke(catalogId));
            return true;
        }
    }
}
