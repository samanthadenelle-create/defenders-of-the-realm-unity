// =============================================================================
// OutpostConnectorConfirmInjector — NO-REBAKE runtime fix for accidental warps.
// -----------------------------------------------------------------------------
// SYMPTOM (owner playtest): simply WALKING near the castle's WEST gate auto-
// teleports the hero into Garrison_troll_outpost — no intent, no key-press.
//
// ROOT CAUSE (verified RCA): the castle's outpost connectors — GameObjects named
// "OutpostConnector_*" in MainCastle_Hall, each carrying a
// DeNelle.Village.SceneTransitionTrigger — were baked with requireConfirm = false
// (set by CastleHubBuilder.cs:1185, frozen into MainCastle_Hall.unity:7768). With
// requireConfirm == false, SceneTransitionTrigger.Update() takes the legacy
// AUTO-CROSS path and fires Cross() the instant the hero enters the proximity
// radius — so brushing past a gate warps the hero across with no confirmation.
//
// THE BUILDER COULD BE FIXED — but the connectors are already baked into the
// shipped MainCastle_Hall.unity. Re-baking is the owner-gated path. This runtime
// component lands the fix WITHOUT a rebake: on every scene load it flips every
// outpost connector's SceneTransitionTrigger back to requireConfirm = true, which
// switches those seams onto the CONFIRM-TO-CROSS path (an intentional F-press /
// Interact tap is required; proximity + trigger-enter no longer auto-warp).
//
// Mirrors the runtime-fixer pattern of GroundZFightFixer:
//   • [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] + SceneManager.sceneLoaded
//     re-arm — the player boots into Title and reaches the castle LATER, so a
//     one-shot check would miss it; we re-run on every scene load.
//   • WEBGL-SAFE: an uncaught exception in a sceneLoaded handler HALTS the WebGL
//     player, so every entry point is wrapped in try/catch (log a warning, never
//     throw out of the handler).
//   • IDEMPOTENT: setting requireConfirm = true on a connector already at true is
//     a no-op, so repeated loads are harmless.
//
// requireConfirm is a PUBLIC bool field on SceneTransitionTrigger, so we set it
// with the public setter directly — no reflection needed (CastleHubBuilder:1185
// uses reflection only because it lives in an editor assembly without a typed
// reference; here we reference the type and field directly).
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.World
{
    public static class OutpostConnectorConfirmInjector
    {
        // GameObject name prefix that marks a castle outpost connector seam.
        private const string ConnectorNamePrefix = "outpostconnector";

        /// <summary>
        /// Registrar. Runs once at app start, then re-runs on EVERY scene load — the
        /// player boots into Title and reaches the castle hub LATER, so a one-shot
        /// check would miss it. Idempotent per load.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            // Also fix the scene already active at app start.
            SafeFix();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            SafeFix();
        }

        // Never let the fix throw out of a sceneLoaded handler (halts WebGL).
        private static void SafeFix()
        {
            try { FixOutpostConnectors(); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[OutpostConnectorConfirm] fix threw (non-fatal): " + e);
            }
        }

        /// <summary>
        /// Flip every outpost connector's SceneTransitionTrigger to requireConfirm =
        /// true so the seam requires an intentional F-press instead of auto-crossing
        /// on proximity. Public so a builder or test can call it. Idempotent.
        /// </summary>
        public static void FixOutpostConnectors()
        {
            var triggers = Object.FindObjectsByType<SceneTransitionTrigger>();
            if (triggers == null || triggers.Length == 0) return;

            int fixedCount = 0;
            for (int i = 0; i < triggers.Length; i++)
            {
                var t = triggers[i];
                if (t == null) continue;
                if (!NameIsOutpostConnector(t.name)) continue;
                if (t.requireConfirm) continue; // already confirm-to-cross — idempotent

                t.requireConfirm = true;
                fixedCount++;
                FlowTrace.Step("Seam",
                    $"OutpostConnector '{t.name}' -> requireConfirm=true (applied by injector, belt-and-suspenders)");
            }

            if (fixedCount > 0)
            {
                Debug.Log("[OutpostConnectorConfirm] set requireConfirm=true on " +
                          fixedCount + " outpost connectors.");
            }
        }

        private static bool NameIsOutpostConnector(string n)
        {
            if (string.IsNullOrEmpty(n)) return false;
            return n.ToLowerInvariant().StartsWith(ConnectorNamePrefix);
        }
    }
}
