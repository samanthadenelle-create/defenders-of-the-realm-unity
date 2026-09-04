// =============================================================================
// EchoGuideService -- WHICH Echo guides the next expedition (WO-1380).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village.World.Camps
//
// Before each expedition the player picks an Echo Guide.
// THE ECHO DOES NOT FIGHT. IT REMEMBERS. (creative canon 2026-09-04 sec.7)
//
// ---------------------------------------------------------------------------
// SCOPE FENCE -- READ THIS BEFORE ADDING A FIELD (owner ruling 2026-09-04)
// ---------------------------------------------------------------------------
// A Guide grants NO stat, NO yield, NO combat effect in V1. This service therefore
// exposes EXACTLY three verbs -- which Echo is selected, which Echoes may be
// selected, and what the selected Echo SAYS at a target -- and it must never grow a
// bonus/multiplier/modifier/damage/loot accessor. EchoGuideMemoryRegression
// source-lints this file for those tokens and FAILS if one appears. Adding a
// mechanical effect later is a deliberate design decision, never a quiet one.
//
// ---------------------------------------------------------------------------
// WHY PlayerPrefs AND NOT THE SAVE SCHEMA
// ---------------------------------------------------------------------------
// The choice is a narrative PREFERENCE with no economic consequence: nothing is
// spent, nothing is earned, and losing it costs the player one tap. A save-schema
// bump is a migration + a version ceiling + a cloud round-trip for a value that
// grants nothing, so this rides PlayerPrefs like the other per-device preferences
// (FeatureFlags' ff.* keys are the existing idiom). If a Guide ever becomes
// mechanical, THAT is the change that earns a schema field -- and the fence above
// says such a change is deliberate and announced.
//
// ONE APPEARANCE OWNER, unchanged: the Echo's world body still belongs solely to
// EchoWorldPresence (WO-1108 Lane B) and PetDeployer.DespawnEcho is still the one
// despawn path. This service spawns nothing and destroys nothing; it decides WHO is
// speaking and hands the words to the existing presence.
//
// Every step self-reports through FlowTrace under the "EchoGuide" tag (CLAUDE.md
// sec.12). ASCII-only strings.
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;

namespace DeNelle.Village.World.Camps
{
    /// <summary>The player's Echo Guide choice for the next expedition. Narrative only.</summary>
    public static class EchoGuideService
    {
        private const string Sys = "EchoGuide";

        /// <summary>Per-device preference key. Stable forever -- it keys a stored roster id.</summary>
        public const string PrefKey = "echo.guide.selectedId";

        // The target the player last committed an expedition against, so the Echo has
        // something to remember when EchoWorldPresence brings it back after the battle.
        // Session-scoped by design: it mirrors EchoWorldPresence's own session statics
        // rather than inventing a second lifetime for the same beat.
        private static string s_lastExpeditionTargetId;

        /// <summary>The canonical target id of the expedition the player last committed to
        /// (null before the first one this session).</summary>
        public static string LastExpeditionTargetId => s_lastExpeditionTargetId;

        /// <summary>Roster entry for a stable Echo id, or null when unknown.</summary>
        public static EchoRosterEntry ById(string echoId)
        {
            if (string.IsNullOrEmpty(echoId)) return null;
            var all = EchoRosterCatalog.All;
            if (all == null) return null;
            for (int i = 0; i < all.Length; i++)
                if (all[i] != null && string.Equals(all[i].Id, echoId, StringComparison.OrdinalIgnoreCase))
                    return all[i];
            return null;
        }

        /// <summary>How many roster Echoes the player owns (>=1). Reads EchoService when it is
        /// alive, the persisted state otherwise, and 1 headless -- the same ladder
        /// EchoBonusCalculator already uses, not a fourth spelling.</summary>
        public static int OwnedCount()
        {
            int count = 1;
            Guard.Try(Sys, "read owned echo count", () =>
            {
                if (EchoService.Instance != null) { count = EchoService.Instance.EchoCount; return; }
                var svc = GameStateService.Instance;
                if (svc != null && svc.State != null) count = Mathf.Max(1, svc.State.EchoCount);
            });
            return Mathf.Clamp(count, 1, EchoRosterCatalog.Count);
        }

        /// <summary>True when the player owns this Echo (roster Order is 1-based and equals the
        /// owned count at which it unlocks).</summary>
        public static bool IsOwned(EchoRosterEntry entry)
        {
            return entry != null && entry.Order >= 1 && entry.Order <= OwnedCount();
        }

        /// <summary>The Echoes the player may pick as a Guide, in roster order (never null, never empty
        /// -- the founding Echo is always owned).</summary>
        public static IReadOnlyList<EchoRosterEntry> AvailableGuides()
        {
            var list = new List<EchoRosterEntry>();
            var all = EchoRosterCatalog.All;
            if (all == null) return list;
            for (int i = 0; i < all.Length; i++)
                if (IsOwned(all[i])) list.Add(all[i]);
            if (list.Count == 0 && all.Length > 0) list.Add(all[0]);
            return list;
        }

        /// <summary>
        /// The selected Guide's stable id. Defaults to the catalog default (Corvin, the scout who
        /// has already been out there). Falls back -- LOUDLY -- to the newest owned Echo when the
        /// stored or default choice is not owned yet, so the picker never opens on an Echo the
        /// player cannot see.
        /// </summary>
        public static string SelectedGuideEchoId => ResolveSelected().Id;

        /// <summary>The selected Guide's roster entry (never null while the roster is non-empty).</summary>
        public static EchoRosterEntry SelectedGuide => ResolveSelected();

        private static EchoRosterEntry ResolveSelected()
        {
            string stored = null;
            Guard.Try(Sys, "read stored guide preference",
                () => stored = PlayerPrefs.GetString(PrefKey, null));

            var entry = ById(stored);
            if (entry != null && IsOwned(entry)) return entry;
            if (entry != null)
                FlowTrace.Warn(Sys,
                    "stored guide " + entry.Id + " (order " + entry.Order + ") is NOT owned yet (owned=" +
                    OwnedCount() + ") -- falling back. A save carried across a reset can do this.");
            else if (!string.IsNullOrEmpty(stored))
                FlowTrace.Warn(Sys,
                    "stored guide id " + stored + " is not in the roster -- falling back to the default. " +
                    "Roster ids are save keys and must never be renamed.");

            var preferred = ById(EchoGuideCatalog.DefaultGuideEchoId);
            if (preferred != null && IsOwned(preferred)) return preferred;

            // The default (Corvin) is roster order 3, so an early player does not own him yet.
            // Pick the newest Echo they DO own -- ByCount is clamped and never returns null.
            var newest = EchoRosterCatalog.ByCount(OwnedCount());
            FlowTrace.Step(Sys,
                "default guide " + EchoGuideCatalog.DefaultGuideEchoId + " is not owned yet (owned=" +
                OwnedCount() + "); the picker opens on " + (newest != null ? newest.Id : "(none)") +
                ". Corvin becomes the default as soon as he is awakened.");
            return newest;
        }

        /// <summary>
        /// Choose a Guide. Refuses (returns false, WARNED) an unknown or unowned Echo rather than
        /// storing a value the picker cannot show. Narrative only -- nothing else changes.
        /// </summary>
        public static bool SelectGuide(string echoId, string reason)
        {
            var entry = ById(echoId);
            if (entry == null)
            {
                FlowTrace.Warn(Sys,
                    "SelectGuide(" + (echoId ?? "(null)") + ") refused: no such Echo in the roster (" + reason + ").");
                return false;
            }
            if (!IsOwned(entry))
            {
                FlowTrace.Warn(Sys,
                    "SelectGuide(" + entry.Id + ") refused: order " + entry.Order + " > owned " + OwnedCount() +
                    " (" + reason + "). The picker should only offer owned Echoes.");
                return false;
            }

            bool saved = false;
            Guard.Try(Sys, "persist guide preference", () =>
            {
                PlayerPrefs.SetString(PrefKey, entry.Id);
                PlayerPrefs.Save();
                saved = true;
            });

            FlowTrace.Step(Sys,
                "guide selected: " + entry.DisplayName + " (" + entry.Id + ") -- " + reason +
                (saved ? "" : " [preference NOT persisted; the choice holds for this session only]") +
                ". Narrative only: no stat, no yield, no combat effect.");
            return true;
        }

        /// <summary>The selected Guide's authored line for a target id or raid scene name.
        /// Null when the target has no authored memories (WARNED by the catalog).</summary>
        public static string MemoryLineFor(string targetIdOrSceneName)
        {
            var guide = SelectedGuide;
            if (guide == null) return null;
            return EchoGuideCatalog.LineFor(guide.Id, targetIdOrSceneName);
        }

        /// <summary>
        /// Record the target the player just committed an expedition against, so the Echo has
        /// something to remember when EchoWorldPresence brings it back after the battle.
        /// Stores the resolved id only -- an unresolvable target is warned by the catalog and
        /// simply leaves the Echo with nothing to say, never a wrong line.
        /// </summary>
        public static void NoteExpeditionTarget(string targetIdOrSceneName, string reason)
        {
            string resolved = EchoGuideCatalog.ResolveTargetId(targetIdOrSceneName);
            s_lastExpeditionTargetId = resolved;
            var guide = SelectedGuide;
            FlowTrace.Step(Sys,
                "expedition target noted: " + (resolved ?? "(unresolved:" + (targetIdOrSceneName ?? "null") + ")") +
                " with guide " + (guide != null ? guide.Id : "(none)") + " (" + reason +
                "). The Guide speaks this memory when the Echo returns from the battle.");
        }

        /// <summary>Clears the session-scoped expedition memory. Used by the regression oracle,
        /// which needs a defined starting point; nothing in gameplay calls it.</summary>
        public static void ResetSessionState()
        {
            s_lastExpeditionTargetId = null;
        }
    }
}
