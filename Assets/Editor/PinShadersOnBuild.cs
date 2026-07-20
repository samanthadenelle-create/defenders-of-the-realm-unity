// =============================================================================
// PinShadersOnBuild - MAT-02 build-time shader pin (pink-floor / magenta durable fix).
// -----------------------------------------------------------------------------
// SHIP-BLOCKER MAT-02: EnsureShadersIncluded (the pass that adds URP Terrain/Lit +
// URP/Lit + URP/Unlit + particle + video shaders to GraphicsSettings'
// m_AlwaysIncludedShaders) was a MANUAL menu item ("Defenders/Build/Ensure Shaders
// Included"). If a build shipped without someone remembering to run it, the URP
// Terrain/Lit shader is stripped -> Shader.Find returns null -> the ground renders
// PINK on other machines (and particles render magenta).
//
// This IPreprocessBuildWithReport hook runs that same pass AUTOMATICALLY on EVERY
// build (OnPreprocessBuild), so no build can ever ship without the shaders pinned.
// callbackOrder is deeply negative so the pin lands BEFORE Unity packages shaders.
// Editor-only, idempotent (EnsureShadersIncluded.Run adds nothing already present).
// =============================================================================

using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace DeNelle.Editor
{
    public sealed class PinShadersOnBuild : IPreprocessBuildWithReport
    {
        // Run as early as possible so the always-included list is updated before the
        // build's shader-stripping/packaging reads it. Lower = earlier.
        public int callbackOrder => -1000;

        public void OnPreprocessBuild(BuildReport report)
        {
            Debug.Log("[PinShadersOnBuild] MAT-02: auto-pinning always-included shaders (URP Terrain/Lit + Lit/Unlit + particles/video) before build.");
            EnsureShadersIncluded.Run();
        }
    }
}
