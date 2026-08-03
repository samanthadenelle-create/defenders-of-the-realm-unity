// =============================================================================
// CavePortalRepointInjector — NO-REBAKE runtime repoint of the walk-up outpost.
// -----------------------------------------------------------------------------
// OWNER DIRECTIVE (2026-07-10): the walk-up cave portal in the overworld must
// load the NEW bounded arena **KayKitChallengeOutpost** (built by
// KayKitChallengeOutpostBuilder) INSTEAD OF the old placeholder **Outpost1**
// (which "had no bounds").
//
// ROOT CAUSE (verified RCA, file:line):
//   • The live loader is a SceneTransitionTrigger BAKED into the overworld scene:
//     Main_Castle_Overworld.unity — GameObject "CavePortal_Trigger",
//     targetSceneName: Outpost1, targetPosition {0,0,-12}, loadAdditive 0 (Single).
//     (Matches the runtime "[SeamTrace] 'CavePortal_Trigger' ONLINE target=Outpost1".)
//   • The scene-links.json / SceneLinkResolverHost path is DEAD CODE — a repo-wide
//     grep for TravelTo( found ZERO runtime callers — so editing that JSON is a no-op.
//   ⇒ The baked trigger is the sole mechanism. It is frozen in the .unity, which we
//     never hand-edit (CLAUDE.md §3), so we repoint it at runtime instead.
//
// Mirrors the proven runtime-fixer pattern of OutpostConnectorConfirmInjector:
//   • [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] + SceneManager.sceneLoaded
//     re-arm — the player boots into Title and reaches the overworld LATER, so a
//     one-shot check would miss it; we re-run on every scene load.
//   • WEBGL-SAFE: every entry point is wrapped in try/catch (an uncaught throw in a
//     sceneLoaded handler halts the WebGL player).
//   • IDEMPOTENT: once a trigger already targets KayKitChallengeOutpost we skip it,
//     so repeated loads are harmless.
//
// Entry landing = (0,0,-24): the builder's Outpost_Entry marker
// (KayKitChallengeOutpostBuilder.Entry = -Outer*0.5 + 4 = -24). SceneTransitionTrigger
// warps to targetPosition DIRECTLY (it does not resolve the marker by name), so the
// position is set here alongside the scene name.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.World
{
    public static class CavePortalRepointInjector
    {
        // The old placeholder outpost the baked trigger points at.
        private const string OldTarget = "Outpost1";
        // Owner ruling 2026-07-13 evening, CORRECTED same hour: "the portal take to
        // dungeons" — DUNGEONS enter through the ANIMATED DungeonPortal objects
        // (ff.dungeonportals flipped ON, DungeonEntranceBootstrap Heart-ring doors +
        // world portals), NOT this cave. The cave stays the OUTPOST's door exactly as
        // before (the bounded KayKit arena with its own victory/return loop).
        private const string NewTarget = "KayKitChallengeOutpost";
        // The builder's Outpost_Entry world position (Entry = -Outer*0.5 + 4 = -24).
        private static readonly Vector3 EntryPos = new Vector3(0f, 0f, -24f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            // Also repoint the scene already active at app start.
            SafeRepoint();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => SafeRepoint();

        // Never let the repoint throw out of a sceneLoaded handler (halts WebGL).
        private static void SafeRepoint()
        {
            try { RepointCavePortals(); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[CavePortalRepoint] repoint threw (non-fatal): " + e);
            }
        }

        /// <summary>
        /// Repoint every SceneTransitionTrigger still aimed at the old placeholder
        /// outpost (by target name, with a belt-and-suspenders name match on the baked
        /// "CavePortal_Trigger") to the new bounded KayKitChallengeOutpost, landing the
        /// hero at the builder's Outpost_Entry. Public so a test can drive it. Idempotent.
        /// </summary>
        public static void RepointCavePortals()
        {
            // FIX A (WO-771, docs/RAID_NORTHSTAR.md §2A/§3): the walk-up-outpost loop is
            // RETIRED — the raid loop is Teleport/Deploy, so the player must NOT be able to
            // walk into the baked overworld cave and be dropped into the retired
            // KayKitChallengeOutpost. When ff.raidwalk is OFF (default), do NOT repoint the
            // portal; instead NEUTRALIZE the baked outpost trigger so it can't fire. This
            // mirrors the sibling gates on RaidOutpostSystem / OutpostVictoryController /
            // ChallengeOutpostVictoryController. Flip ff.raidwalk ON to restore the legacy
            // walk-to repoint verbatim.
            if (!DeNelle.Core.FeatureFlags.RaidContinuousWalk)
            {
                NeutralizeOutpostTriggers();
                return;
            }

            var triggers = Object.FindObjectsByType<SceneTransitionTrigger>();
            if (triggers == null || triggers.Length == 0) return;

            int repointed = 0;
            for (int i = 0; i < triggers.Length; i++)
            {
                var t = triggers[i];
                if (t == null) continue;
                if (t.targetSceneName == NewTarget) continue; // already repointed — idempotent

                bool isCavePortal =
                    t.targetSceneName == OldTarget ||
                    (t.name != null && t.name.StartsWith("CavePortal_Trigger", System.StringComparison.OrdinalIgnoreCase));
                if (!isCavePortal) continue;

                t.targetSceneName = NewTarget;
                t.targetPosition = EntryPos;
                // ff.raidwalk ON = the walk-up entry is LIVE again, so the "sealed" notice must
                // not linger (only reachable if the flag flipped inside a live session; a fresh
                // boot never installs it on this branch). DISABLE it -- same never-destroy
                // discipline as the neutralize path; its OnDisable drops any showing prompt.
                var staleNotice = t.GetComponent<RetiredSeamNotice>();
                if (staleNotice != null) staleNotice.enabled = false;
                repointed++;
                FlowTrace.Step("Seam",
                    $"CavePortal '{t.name}' repointed '{OldTarget}' -> '{NewTarget}' @ {EntryPos} (injector, no rebake)");
            }

            if (repointed > 0)
                Debug.Log($"[CavePortalRepoint] repointed {repointed} cave portal(s) -> {NewTarget}.");
        }

        /// <summary>
        /// FIX A neutralize path (ff.raidwalk OFF): DISABLE every overworld outpost seam so
        /// the player can't walk into a retired outpost — the baked "CavePortal_Trigger" and
        /// any SceneTransitionTrigger whose destination is an outpost (Outpost* / Garrison* /
        /// RaidBase*, incl. the old Outpost1 target and the KayKitChallengeOutpost repoint
        /// target). We only DISABLE the trigger component + its collider — we NEVER destroy the
        /// GameObject and NEVER hand-edit the .unity scene (CLAUDE.md §3), so flipping
        /// ff.raidwalk ON and reloading restores the walk-up entry cleanly. Idempotent + null-safe.
        /// Every seam we shut off also gets a <see cref="RetiredSeamNotice"/> so walking up to it
        /// TELLS the player it is sealed and reopens in a future update, instead of nothing at all.
        /// </summary>
        private static void NeutralizeOutpostTriggers()
        {
            var triggers = Object.FindObjectsByType<SceneTransitionTrigger>();
            if (triggers == null || triggers.Length == 0) return;

            int disabled = 0;
            for (int i = 0; i < triggers.Length; i++)
            {
                var t = triggers[i];
                if (t == null) continue;
                if (!IsRetiredOutpostSeam(t)) continue;

                // F8 seq 645 (owner 2026-08-02): "this is deactivated (outpost) as it is still
                // broken (we could add something about update coming)". A neutralized seam used
                // to be SILENT — the player walks up to the baked cave and gets nothing, which
                // reads as a bug. Hang the honest affordance here, where we already know exactly
                // which objects we disabled. Runtime-only + idempotent (AddComponent-if-missing),
                // and installed OUTSIDE the already-disabled early-out below so a seam
                // neutralized on an earlier pass still gets its notice on a later scene load.
                RetiredSeamNotice.Install(t.gameObject, t.ProximityRadius, t.targetSceneName);

                if (!t.enabled) continue; // already neutralized — idempotent

                t.enabled = false;                       // stop the proximity crossing behaviour
                var col = t.GetComponent<Collider>();
                if (col != null) col.enabled = false;    // stop the OnTriggerEnter fallback
                disabled++;
                FlowTrace.Step("Seam",
                    $"CavePortal '{t.name}' -> '{t.targetSceneName}' NEUTRALIZED (ff.raidwalk OFF; walk-up outpost retired, no scene edit).");
            }

            if (disabled > 0)
                Debug.Log($"[CavePortalRepoint] neutralized {disabled} retired outpost seam(s) (ff.raidwalk OFF).");
        }

        // A trigger the walk-up-outpost retire must shut off: the baked cave-portal by name,
        // its old Outpost1 target, the KayKitChallengeOutpost repoint target, or any outpost
        // destination (Outpost* / Garrison* / RaidBase*). Mirrors SceneTransitionTrigger's own
        // (private) IsOutpostDestination classifier so the neutralize matches exactly what the
        // gate is meant to cover.
        private static bool IsRetiredOutpostSeam(SceneTransitionTrigger t)
        {
            if (t.name != null && t.name.StartsWith("CavePortal_Trigger", System.StringComparison.OrdinalIgnoreCase))
                return true;
            string dest = t.targetSceneName;
            if (string.IsNullOrEmpty(dest)) return false;
            return dest == OldTarget
                || dest == NewTarget
                || dest.StartsWith("Outpost",  System.StringComparison.OrdinalIgnoreCase)
                || dest.StartsWith("Garrison", System.StringComparison.OrdinalIgnoreCase)
                || DeNelle.Core.HubScenes.IsRaid(dest);
        }
    }
}
