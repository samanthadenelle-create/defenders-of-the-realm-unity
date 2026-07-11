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
        // The new bounded arena we repoint to.
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
                repointed++;
                FlowTrace.Step("Seam",
                    $"CavePortal '{t.name}' repointed '{OldTarget}' -> '{NewTarget}' @ {EntryPos} (injector, no rebake)");
            }

            if (repointed > 0)
                Debug.Log($"[CavePortalRepoint] repointed {repointed} cave portal(s) -> {NewTarget}.");
        }
    }
}
