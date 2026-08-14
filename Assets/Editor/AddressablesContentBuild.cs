// =============================================================================
// AddressablesContentBuild — WO-974: give the Addressables content build a SEAM.
//
// THE DEFECT THIS CLOSES.
// AddressableAssetSettings.asset carries `m_BuildAddressablesWithPlayerBuild: 0`.
// That does NOT mean "do not build" — at package source the enum reads
// PlayerBuildOption.PreferencesValue = 0, i.e. *"use the global settings stored in
// preferences"*. Those preferences are an UNCOMMITTED, PER-MACHINE Editor setting.
// Meanwhile there was ZERO explicit content build anywhere under Assets/: WebGLBuild,
// DesktopBuild and AndroidBuild each called BuildPipeline.BuildPlayer and nothing else.
//
// So whether the bundles were rebuilt was decided by a preference no one can see, that
// travels with nobody. It is evidently ON on the machine this was written on — which is
// exactly what makes it dangerous: it works here by luck, and a fresh clone, a CI runner,
// or a seat that ever toggled it ships STALE OR ABSENT StreamingAssets/aa with NO loud
// failure. Addressables simply cannot resolve at runtime, and the player sees nothing.
//
// This class makes the decision explicit, logged, and identical on every machine.
//
// WHY IT IS LOUD RATHER THAN FATAL.
// A failed content build must not silently produce a player, so it emits an ERROR-level
// marker the caller can gate on. It returns bool rather than throwing so each build entry
// point decides its own policy — a throw here would abort a 40-minute APK at minute one
// for a fault the operator may already know about.
//
// READ THE MARKER, NOT THE EXIT CODE (CLAUDE.md §8): ADDRESSABLES_CONTENT_OK / _FAIL.
// =============================================================================
using UnityEditor;
using UnityEditor.AddressableAssets;
using UnityEditor.AddressableAssets.Build;
using UnityEditor.AddressableAssets.Settings;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class AddressablesContentBuild
    {
        public const string MarkerOk   = "ADDRESSABLES_CONTENT_OK";
        public const string MarkerFail = "ADDRESSABLES_CONTENT_FAIL";

        /// <summary>
        /// Build Addressables content before a player build. Returns false when the content
        /// build reported an error — the caller decides whether to continue.
        /// </summary>
        public static bool EnsureBuilt(string caller)
        {
            var settings = AddressableAssetSettingsDefaultObject.Settings;
            if (settings == null)
            {
                // No settings asset = Addressables is not configured for this project at all.
                // Not an error: say so plainly and let the player build proceed unchanged.
                Debug.Log($"[Addressables] {caller}: no AddressableAssetSettings — nothing to build (skipped).");
                return true;
            }

            Debug.Log($"[Addressables] {caller}: building content explicitly (WO-974 — never trust the per-machine Editor preference).");

            AddressablesPlayerBuildResult result = null;
            try
            {
                AddressableAssetSettings.BuildPlayerContent(out result);
            }
            catch (System.Exception e)
            {
                Debug.LogError($"{MarkerFail} :: {caller} — BuildPlayerContent THREW: {e.GetType().Name}: {e.Message}. " +
                               "The player would ship stale or absent StreamingAssets/aa and fail to resolve every " +
                               "Addressable at runtime.");
                return false;
            }

            if (result == null)
            {
                Debug.LogError($"{MarkerFail} :: {caller} — BuildPlayerContent returned no result object; " +
                               "cannot prove content was built.");
                return false;
            }

            if (!string.IsNullOrEmpty(result.Error))
            {
                Debug.LogError($"{MarkerFail} :: {caller} — {result.Error}");
                return false;
            }

            // Assert the OUTCOME, not the call (INSTRUMENTATION_STANDARD §1.4b): a content build
            // that produces ZERO locations is a successful no-op that ships an empty catalog, and
            // would otherwise read as a pass.
            if (result.LocationCount <= 0)
            {
                Debug.LogError($"{MarkerFail} :: {caller} — content built with ZERO locations " +
                               $"({result.Duration:0.0}s). An empty catalog resolves nothing at runtime.");
                return false;
            }

            Debug.Log($"{MarkerOk} {result.LocationCount} locations :: {caller} " +
                      $"({result.Duration:0.0}s -> {result.OutputPath})");
            return true;
        }
    }
}
