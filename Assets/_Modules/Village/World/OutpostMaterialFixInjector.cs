// =============================================================================
// OutpostMaterialFixInjector — NO-REBAKE runtime fix for the Tripo outpost going magenta.
// -----------------------------------------------------------------------------
// SYMPTOM (owner F8 2026-06-30, flag_00.png): the OuterWorld outpost-portal entrance
// ("Enter the enemy stronghold") renders as a solid purple/magenta blob.
//
// ROOT CAUSE (verified from data): the outpost entrance was swapped to the Tripo model
// Resources/Dungeons/enemy_outpost.fbx (OuterWorldCavePortalBuilder, named "CavePortal"
// in the scene). Tripo FBXs import with FbxSurfacePhong / non-URP materials that Unity 6
// URP cannot render -> InternalErrorShader magenta (dimmed to purple by the night light).
// The builder LOADS + offsets the model but never runs the project's Tripo material fix,
// so the magenta is baked into the OuterWorld scene instance.
//
// THE BUILDER COULD BE FIXED — but the outpost is already baked into OuterWorld.unity and
// re-baking is the owner-gated path. This runtime component lands the fix WITHOUT a rebake,
// EXACTLY mirroring the sibling OutpostConnectorConfirmInjector (same outpost, same no-rebake
// rationale): on every scene load it finds the "CavePortal" structure and attaches a
// DeNelle.Core.TripoMaterialFixer, which on Start() rebuilds every renderer's material as a
// clean URP/Lit carrying the source texture (with Resources/Dungeons/enemy_outpost as the
// fallback texture in case the Tripo embedded texture didn't auto-link on import).
//
//   • [RuntimeInitializeOnLoadMethod(AfterSceneLoad)] + SceneManager.sceneLoaded re-arm —
//     the player boots into Title and reaches OuterWorld LATER, so a one-shot check misses it.
//   • WEBGL-SAFE: an uncaught exception in a sceneLoaded handler HALTS the WebGL player, so
//     every entry point is wrapped in try/catch (warn, never throw out of the handler).
//   • IDEMPOTENT: skips if the outpost already carries a TripoMaterialFixer, so repeated
//     loads (and a future baked-in fixer) are harmless no-ops.
//
// Village -> Core only (TripoMaterialFixer is DeNelle.Core). No reflection.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village.World
{
    public static class OutpostMaterialFixInjector
    {
        // GameObject name the outpost structure is given by OuterWorldCavePortalBuilder.CaveName.
        private const string OutpostObjectName = "CavePortal";
        // Resources path to the Tripo texture (Assets/Resources/Dungeons/enemy_outpost.jpg) —
        // the safety-net base map if the FBX's embedded texture didn't auto-link on import.
        private const string FallbackTexture   = "Dungeons/enemy_outpost";

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
            try { FixOutpostMaterials(); }
            catch (System.Exception e)
            {
                Debug.LogWarning("[OutpostMaterialFix] fix threw (non-fatal): " + e);
            }
        }

        /// <summary>
        /// Attach a TripoMaterialFixer to the outpost structure so its materials rebuild as
        /// URP/Lit at runtime (kills the magenta). Public so a builder/test can call it. Idempotent.
        /// </summary>
        public static void FixOutpostMaterials()
        {
            var outpost = GameObject.Find(OutpostObjectName);
            if (outpost == null) return;                                   // not in this scene (Title/etc.) — no-op
            if (outpost.GetComponent<TripoMaterialFixer>() != null) return; // already fixed — idempotent

            var fix = outpost.AddComponent<TripoMaterialFixer>();
            // Setter must land THIS frame; TripoMaterialFixer.Start() defers Run() to next frame.
            fix.SetFallbackTexture(FallbackTexture);
            FlowTrace.Step("Outpost",
                $"'{OutpostObjectName}' Tripo material fix attached (URP rebuild, fallback '{FallbackTexture}') — no-rebake magenta fix.");
        }
    }
}
