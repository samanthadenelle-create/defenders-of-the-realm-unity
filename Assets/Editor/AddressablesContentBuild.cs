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
        /// <summary>
        /// Back-compat overload: builds for whatever the ACTIVE target happens to be and does not
        /// check it. Prefer the overload that takes an expected target — WO-1124 exists because a
        /// content build that cannot state which platform it built for shipped Windows bundles
        /// inside an Android APK, with every marker in the chain green.
        /// </summary>
        public static bool EnsureBuilt(string caller) => EnsureBuilt(caller, null);

        /// <summary>
        /// Build Addressables content and PROVE it was built for <paramref name="expectedTarget"/>.
        ///
        /// <para>WO-1124. Addressables builds for the ACTIVE build target, so content lands in
        /// whichever platform folder the editor happened to be on. An APK built from an editor left
        /// on Win64 got Windows bundles: the device then asked the CDN for an Android catalog that
        /// was never uploaded and resolved NOTHING - no buildings, no enemies - silently, on a build
        /// that gated clean. Passing the expected target makes that state impossible to reach
        /// quietly: mismatch is a hard FAIL with a named reason, not a log line.</para>
        ///
        /// <para>Pass null to keep the old "build for whatever is active" behaviour.</para>
        /// </summary>
        public static bool EnsureBuilt(string caller, BuildTarget? expectedTarget)
        {
            // Check BEFORE building. Building 175 MB for the wrong platform and then complaining
            // wastes the minutes; the caller is supposed to have switched the target already.
            BuildTarget active = EditorUserBuildSettings.activeBuildTarget;
            if (expectedTarget.HasValue && active != expectedTarget.Value)
            {
                Debug.LogError($"{MarkerFail} :: {caller} — WRONG BUILD TARGET. Addressables builds for the " +
                               $"ACTIVE target, which is '{active}', but the caller expects '{expectedTarget.Value}'. " +
                               "Content would land in the wrong ServerData/<platform>/ folder and the shipped " +
                               "player would resolve nothing (WO-1124). Switch the target BEFORE building content.");
                return false;
            }

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

            // State the PLATFORM in the success line, not just the count. WO-1124 survived because
            // every marker was green and none of them named a platform - the one fact that was wrong.
            Debug.Log($"{MarkerOk} {result.LocationCount} locations :: {caller} " +
                      $"target={active} ({result.Duration:0.0}s -> {result.OutputPath})");
            return true;
        }
    }
}
