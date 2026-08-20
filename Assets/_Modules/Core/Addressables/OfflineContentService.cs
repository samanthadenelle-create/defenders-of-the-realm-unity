// =============================================================================
// OfflineContentService — PROD-010. Opt-in offline mode: pull the whole remote
// content set ONCE over Wi-Fi, then fall back to the local cache whenever the
// network is gone.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core (Core/Addressables). References Addressables only — no
// Village/HUD types, so the seam stays usable from any module.
//
// OWNER SPEC, 2026-08-19 verbatim:
//   "I want prod ten done as an overnight activity where it's gonna opt in for an
//    offline mode. If they do the offline mode, they are going to... on first time,
//    they're gonna have a CDN pull of everything that they need. and that does still
//    need Wi Fi to download initially. But then after that, we need to somehow tell
//    it to default to local if it can't get to Wi Fi."
//
// So three obligations, in this order:
//   1. OPT-IN. Nothing downloads until the player says yes. This is ~88 MB.
//   2. FIRST-RUN PULL. On yes, fetch every remote dependency while online.
//   3. LOCAL FALLBACK. Afterwards, a launch with no network must use the cached
//      bundles instead of failing.
//
// ⛔ WHY (3) NEEDS CODE AT ALL, since bundles already cache.
// AddressableAssetSettings has m_DisableCatalogUpdateOnStart: 0 — the catalog is
// refreshed from the CDN at launch. That refresh THROWS or hangs with no network.
// Caching the bundles does not help if the step before them fails. So the fallback
// is not "use the cache" (Addressables does that already) — it is "do not let the
// catalog refresh take the launch down with it." That is what CheckOnlineThenLoad
// exists for, and it is the whole difference between a cached game that opens on a
// plane and one that does not.
//
// ⛔ WHY NOT JUST FLIP m_DisableCatalogUpdateOnStart TO 1.
// Because installed players adopt the new remote catalog at launch — that is how a
// shipped build learns about content we upload later. Disabling it globally freezes
// every existing install on the catalog it shipped with. The owner's CDN ruling
// ("lets keep the cdn") depends on that update path staying alive. We degrade on
// FAILURE instead of disabling the feature.
//
// SIZE IS MEASURED, NEVER TYPED. GetDownloadSizeAsync answers in bytes for the real
// content set; the prompt shows that number. An earlier plan promised "10 seconds",
// which was true only while PROD-009 was going to shrink the download. The owner
// retired PROD-009 ("PROD 10 kills 10 and 09"), so the honest figure is the whole
// set: ~88 MB, about 141 s at 5 Mbps and 471 s at 1.5 Mbps. Never promise seconds.
// =============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core
{
    /// <summary>Where the content for this launch is coming from.</summary>
    public enum ContentSource
    {
        /// <summary>Not decided yet this launch.</summary>
        Unknown = 0,
        /// <summary>Network reachable; the remote catalog was refreshed normally.</summary>
        Online = 1,
        /// <summary>No network. Running on the cached catalog + cached bundles.</summary>
        LocalCache = 2,
        /// <summary>No network AND nothing cached — the honest bad case.</summary>
        Unavailable = 3,
    }

    /// <summary>
    /// PROD-010. Opt-in offline download + local fallback. Static, no scene authoring:
    /// call <see cref="ResolveContentSource"/> at boot and
    /// <see cref="DownloadAllForOffline"/> from the opt-in prompt.
    /// </summary>
    public static class OfflineContentService
    {
        private const string Sys = "OfflineContent";

        /// <summary>Player said yes to offline mode (persisted).</summary>
        private const string PrefOptedIn = "offline.optedin";
        /// <summary>The full pull completed at least once (persisted).</summary>
        private const string PrefPulled  = "offline.pulled";
        /// <summary>bundleVersion the completed pull belongs to (content is per-build).</summary>
        private const string PrefPulledBuild = "offline.pulledbuild";

        /// <summary>Every remote label/key the pull must cover. Addressables resolves a
        /// label to its whole dependency closure, so this is the content set, not a file list.</summary>
        private static readonly string[] ContentKeys = { "Structure_Art", "Enemy_Art" };

        /// <summary>Resolved once per launch by <see cref="ResolveContentSource"/>.</summary>
        public static ContentSource Source { get; private set; } = ContentSource.Unknown;

        /// <summary>True when the player has opted into offline mode.</summary>
        public static bool OptedIn => PlayerPrefs.GetInt(PrefOptedIn, 0) == 1;

        /// <summary>
        /// True when a full pull has completed FOR THIS BUILD. Content is content-hashed per
        /// build, so a pull from the previous APK does not cover this one — treating it as
        /// covered is how a player who opted in still hits the network on a fresh install.
        /// </summary>
        public static bool PulledForThisBuild =>
            PlayerPrefs.GetInt(PrefPulled, 0) == 1 &&
            PlayerPrefs.GetString(PrefPulledBuild, "") == Application.version;

        /// <summary>Record the player's answer. Opting out never deletes an existing cache -
        /// the bytes are already paid for and deleting them helps nobody.</summary>
        public static void SetOptedIn(bool yes)
        {
            PlayerPrefs.SetInt(PrefOptedIn, yes ? 1 : 0);
            PlayerPrefs.Save();
            FlowTrace.Step(Sys, $"offline mode opt-in = {yes}");
        }

        // =====================================================================
        //  1. Boot: decide where content comes from, and NEVER let this throw
        // =====================================================================

        /// <summary>
        /// Decide this launch's <see cref="ContentSource"/> and, when the network is gone,
        /// keep the game on the cached catalog instead of failing the launch.
        ///
        /// <para>Call once at boot, before the first content load. Runs to completion even
        /// with no network - the failure path is the POINT of this method, not an edge case.</para>
        /// </summary>
        public static IEnumerator ResolveContentSource(Action<ContentSource> onDone = null)
        {
            using var _ = FlowTrace.Enter(Sys, "ResolveContentSource");

            bool reachable = Application.internetReachability != NetworkReachability.NotReachable;
            FlowTrace.Step(Sys, $"reachability={Application.internetReachability} optedIn={OptedIn} " +
                                $"pulledForThisBuild={PulledForThisBuild} build={Application.version}");

            if (!reachable)
            {
                // OFFLINE. Do NOT touch the catalog - a refresh here is what hangs a
                // no-network launch. Cached bundles are usable without it.
                Source = PulledForThisBuild ? ContentSource.LocalCache : ContentSource.Unavailable;
                if (Source == ContentSource.LocalCache)
                    FlowTrace.Step(Sys, "no network -> LOCAL CACHE (full pull completed for this build; " +
                                        "catalog refresh deliberately SKIPPED so it cannot stall the launch)");
                else
                    FlowTrace.Warn(Sys, "no network AND no completed pull for this build -> content UNAVAILABLE. " +
                                        "Buildings and enemies will not resolve; the player must be told plainly, " +
                                        "never left staring at an empty town (PROD-012).");
                onDone?.Invoke(Source);
                yield break;
            }

            // ONLINE. Let Addressables refresh the catalog, but survive a failure: a
            // reachable radio is not a reachable CDN (captive portals, DNS, an R2 outage).
            AsyncOperationHandle<List<string>> check = default;
            bool started = false;
            try
            {
                check = Addressables.CheckForCatalogUpdates(false);
                started = true;
            }
            catch (Exception ex)
            {
                FlowTrace.Warn(Sys, $"CheckForCatalogUpdates threw immediately ({ex.GetType().Name}: {ex.Message}) " +
                                    "-> falling back to the cached catalog.");
            }

            if (started)
            {
                while (!check.IsDone) yield return null;

                if (check.Status == AsyncOperationStatus.Succeeded && check.Result != null && check.Result.Count > 0)
                {
                    FlowTrace.Step(Sys, $"catalog updates available: {check.Result.Count}");
                    AsyncOperationHandle<List<UnityEngine.AddressableAssets.ResourceLocators.IResourceLocator>> upd = default;
                    bool updStarted = false;
                    try { upd = Addressables.UpdateCatalogs(check.Result, false); updStarted = true; }
                    catch (Exception ex)
                    {
                        FlowTrace.Warn(Sys, $"UpdateCatalogs threw ({ex.Message}) -> cached catalog kept.");
                    }
                    if (updStarted)
                    {
                        while (!upd.IsDone) yield return null;
                        FlowTrace.Step(Sys, $"UpdateCatalogs {(upd.Status == AsyncOperationStatus.Succeeded ? "OK" : "FAILED - cached catalog kept")}");
                        Addressables.Release(upd);
                    }
                }
                else if (check.Status != AsyncOperationStatus.Succeeded)
                {
                    FlowTrace.Warn(Sys, "CheckForCatalogUpdates FAILED with a reachable network " +
                                        "(captive portal / DNS / CDN outage) -> cached catalog kept, launch continues.");
                }

                Addressables.Release(check);
            }

            Source = ContentSource.Online;
            FlowTrace.Step(Sys, "content source = ONLINE");
            onDone?.Invoke(Source);
        }

        // =====================================================================
        //  2. The opt-in pull
        // =====================================================================

        /// <summary>Total bytes still to download for the whole content set. 0 = fully cached.</summary>
        public static IEnumerator GetDownloadSize(Action<long> onSize)
        {
            long total = 0;
            foreach (string key in ContentKeys)
            {
                AsyncOperationHandle<long> h = default;
                bool ok = true;
                try { h = Addressables.GetDownloadSizeAsync(key); }
                catch (Exception ex)
                {
                    ok = false;
                    FlowTrace.Warn(Sys, $"GetDownloadSizeAsync('{key}') threw: {ex.Message}");
                }
                if (!ok) continue;
                while (!h.IsDone) yield return null;
                if (h.Status == AsyncOperationStatus.Succeeded) total += h.Result;
                else FlowTrace.Warn(Sys, $"GetDownloadSizeAsync('{key}') failed - size unknown, excluded from the total.");
                Addressables.Release(h);
            }
            FlowTrace.Step(Sys, $"download size for offline mode = {total} bytes ({total / (1024f * 1024f):F1} MB)");
            onSize?.Invoke(total);
        }

        /// <summary>
        /// THE FIRST-RUN CDN PULL. Downloads every remote dependency so the game can run
        /// without a network afterwards. Requires Wi-Fi/data - the owner's spec says so and
        /// this refuses rather than pretending otherwise.
        /// </summary>
        /// <param name="onProgress">0..1, for a real progress bar. Never fake it.</param>
        /// <param name="onDone">true only when EVERY key downloaded.</param>
        public static IEnumerator DownloadAllForOffline(Action<float> onProgress, Action<bool> onDone)
        {
            using var _ = FlowTrace.Enter(Sys, "DownloadAllForOffline");

            if (Application.internetReachability == NetworkReachability.NotReachable)
            {
                FlowTrace.Warn(Sys, "offline pull REFUSED: no network. The first pull needs Wi-Fi by design; " +
                                    "saying so is better than a progress bar that never moves.");
                onDone?.Invoke(false);
                yield break;
            }

            bool allOk = true;
            for (int i = 0; i < ContentKeys.Length; i++)
            {
                string key = ContentKeys[i];
                AsyncOperationHandle h = default;
                bool started = false;
                try { h = Addressables.DownloadDependenciesAsync(key, false); started = true; }
                catch (Exception ex)
                {
                    allOk = false;
                    FlowTrace.Fail(Sys, $"DownloadDependenciesAsync('{key}') threw: {ex.Message}");
                }
                if (!started) continue;

                while (!h.IsDone)
                {
                    // Weight each key equally: per-key byte totals are not known up front
                    // without a second round of size queries, and a slightly coarse bar that
                    // always moves beats an exact one that stalls.
                    float within = h.GetDownloadStatus().Percent;
                    onProgress?.Invoke((i + Mathf.Clamp01(within)) / ContentKeys.Length);
                    yield return null;
                }

                if (h.Status != AsyncOperationStatus.Succeeded)
                {
                    allOk = false;
                    FlowTrace.Fail(Sys, $"offline pull FAILED for '{key}' - the player is NOT offline-ready. " +
                                        "Do not record the pull as complete.");
                }
                else FlowTrace.Step(Sys, $"offline pull OK for '{key}'");

                Addressables.Release(h);
            }

            onProgress?.Invoke(1f);

            if (allOk)
            {
                // Stamp the BUILD, not just a bool - content is content-hashed per build.
                PlayerPrefs.SetInt(PrefPulled, 1);
                PlayerPrefs.SetString(PrefPulledBuild, Application.version);
                PlayerPrefs.SetInt(PrefOptedIn, 1);
                PlayerPrefs.Save();
                FlowTrace.Step(Sys, $"OFFLINE PULL COMPLETE for build {Application.version} - " +
                                    "later launches with no network will use the local cache.");
            }
            else
            {
                FlowTrace.Warn(Sys, "offline pull incomplete - NOT stamped. The player stays online-dependent, " +
                                    "which is the truthful state; a half-cache recorded as complete is worse.");
            }

            onDone?.Invoke(allOk);
        }
    }
}
