// =============================================================================
// LevelPlayPackageInstall — install the Unity "Ads Mediation" (LevelPlay) package
// through Unity's OWN package resolver, from batchmode.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor (editor-only). Batch:
//   -executeMethod DeNelle.Editor.LevelPlayPackageInstall.Search    (report only)
//   -executeMethod DeNelle.Editor.LevelPlayPackageInstall.Install
//
// ⛔ WHY THIS EXISTS INSTEAD OF A LINE IN manifest.json.
// Hand-writing "com.unity.services.levelplay": "<version>" into the manifest means
// GUESSING a version and bypassing dependency resolution. This project has already
// paid for that class of mistake once this week (WO-1124: an APK that gated green
// and shipped content the CDN did not host). UnityEditor.PackageManager.Client.Add
// is exactly what the Package Manager window calls, so the registry picks the
// version and pulls the transitive dependencies - including the Mobile Dependency
// Resolver that fetches the native Android libraries. Without MDR the package
// compiles fine in the Editor and fails at Gradle on the Android build.
//
// Search() runs FIRST and is report-only, so the package id and the version the
// registry actually offers are recorded in a log BEFORE anything is written. A
// package id assumed from documentation is the same failure mode as a version
// assumed from memory.
//
// Markers: LEVELPLAY_SEARCH_OK / LEVELPLAY_INSTALL_OK / *_FAIL.
// =============================================================================

using System;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class LevelPlayPackageInstall
    {
        /// <summary>The Unity Registry id for the "Ads Mediation" (LevelPlay) package.</summary>
        private const string PackageId = "com.unity.services.levelplay";

        /// <summary>Batchmode has no editor loop driving requests, so we pump and wait.</summary>
        private const int TimeoutSeconds = 600;

        [MenuItem("Defenders/Monetization/1. Search LevelPlay package (report only)")]
        public static void Search()
        {
            SearchRequest req = Client.Search(PackageId);
            if (!Wait(req, "search"))
            {
                Debug.LogError($"LEVELPLAY_SEARCH_FAIL :: {Describe(req)}");
                EditorApplication.Exit(1);
                return;
            }

            if (req.Result == null || req.Result.Length == 0)
            {
                Debug.LogError($"LEVELPLAY_SEARCH_FAIL :: registry returned NO package for '{PackageId}'. " +
                               "The id is wrong or this registry does not serve it - do NOT fall back to " +
                               "writing a manifest line, that only hides the same problem until Gradle.");
                EditorApplication.Exit(1);
                return;
            }

            foreach (var p in req.Result)
            {
                Debug.Log($"[LevelPlay] found '{p.name}' displayName='{p.displayName}' " +
                          $"latestCompatible='{p.versions.latestCompatible}' latest='{p.versions.latest}'");
            }
            Debug.Log($"LEVELPLAY_SEARCH_OK {req.Result.Length} package(s)");
        }

        [MenuItem("Defenders/Monetization/2. Install LevelPlay package")]
        public static void Install()
        {
            var existing = Client.List(offlineMode: false, includeIndirectDependencies: true);
            if (Wait(existing, "list") && existing.Result != null)
            {
                foreach (var p in existing.Result)
                {
                    if (p.name != PackageId) continue;
                    Debug.Log($"LEVELPLAY_INSTALL_OK already present: {p.name}@{p.version} (no change)");
                    return;
                }
            }

            AddRequest req = Client.Add(PackageId);
            if (!Wait(req, "add") || req.Result == null)
            {
                Debug.LogError($"LEVELPLAY_INSTALL_FAIL :: {Describe(req)}");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"LEVELPLAY_INSTALL_OK {req.Result.name}@{req.Result.version} " +
                      $"(source={req.Result.source}, resolvedPath={req.Result.resolvedPath})");
        }

        /// <summary>
        /// Pump a package request to completion. Batchmode has no editor update loop turning these
        /// over, so a plain `while (!req.IsCompleted) {}` spins forever - the sleep yields the thread
        /// and the timeout means a registry that never answers FAILS rather than hanging the build.
        /// </summary>
        private static bool Wait(Request req, string what)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(TimeoutSeconds);
            while (!req.IsCompleted)
            {
                if (DateTime.UtcNow > deadline)
                {
                    Debug.LogError($"[LevelPlay] {what} TIMED OUT after {TimeoutSeconds}s");
                    return false;
                }
                System.Threading.Thread.Sleep(100);
            }
            return req.Status == StatusCode.Success;
        }

        private static string Describe(Request req)
            => req.Error != null
                ? $"{req.Error.errorCode}: {req.Error.message}"
                : $"status={req.Status} (no error object)";
    }
}
