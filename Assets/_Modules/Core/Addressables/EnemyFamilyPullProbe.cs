#if WO1257_CONTENT_PROBE
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using UnityEngine.ResourceManagement.ResourceLocations;

namespace DeNelle.Core
{
    /// <summary>
    /// Tester-only device measurement for WO-1257. It is absent unless the APK build explicitly
    /// supplies WO1257_CONTENT_PROBE; normal Seeker/store artifacts do not execute this pull.
    /// </summary>
    public static class EnemyFamilyPullProbe
    {
        private const string Label = "enemyfam-hollow";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            var host = new GameObject("~WO1257EnemyFamilyPullProbe");
            Object.DontDestroyOnLoad(host);
            host.AddComponent<Driver>();
        }

        private sealed class Driver : MonoBehaviour
        {
            private IEnumerator Start()
            {
                var initialized = Addressables.InitializeAsync();
                yield return initialized;
                if (initialized.Status != AsyncOperationStatus.Succeeded)
                {
                    Debug.LogError("WO1257_HOLLOW_PULL_FAIL initialize");
                    Destroy(gameObject);
                    yield break;
                }

                var locationsHandle = Addressables.LoadResourceLocationsAsync(Label);
                yield return locationsHandle;
                if (locationsHandle.Status != AsyncOperationStatus.Succeeded ||
                    locationsHandle.Result == null || locationsHandle.Result.Count == 0)
                {
                    Debug.LogError("WO1257_HOLLOW_PULL_FAIL no-locations label=" + Label);
                    if (locationsHandle.IsValid()) Addressables.Release(locationsHandle);
                    Destroy(gameObject);
                    yield break;
                }

                var bundleNames = new HashSet<string>(System.StringComparer.OrdinalIgnoreCase);
                foreach (var location in locationsHandle.Result)
                    CollectBundleNames(location, bundleNames, new HashSet<IResourceLocation>());

                var beforeHandle = Addressables.GetDownloadSizeAsync(Label);
                yield return beforeHandle;
                long before = beforeHandle.Status == AsyncOperationStatus.Succeeded
                    ? beforeHandle.Result : -1L;

                var download = Addressables.DownloadDependenciesAsync(Label, false);
                yield return download;

                var afterHandle = Addressables.GetDownloadSizeAsync(Label);
                yield return afterHandle;
                long after = afterHandle.Status == AsyncOperationStatus.Succeeded
                    ? afterHandle.Result : -1L;

                string names = string.Join(",", bundleNames.OrderBy(value => value));
                bool foreign = bundleNames.Any(value => value.Contains("enemyfam-orc") ||
                    value.Contains("enemyfam-troll") || value.Contains("enemyfam-bosses"));
                bool hollow = bundleNames.Any(value => value.Contains("enemyfam-hollow"));
                if (download.Status == AsyncOperationStatus.Succeeded && after == 0L && hollow && !foreign)
                    Debug.Log($"WO1257_HOLLOW_PULL_OK beforeBytes={before} afterBytes={after} bundles=[{names}]");
                else
                    Debug.LogError($"WO1257_HOLLOW_PULL_FAIL download={download.Status} beforeBytes={before} " +
                        $"afterBytes={after} hollow={hollow} foreign={foreign} bundles=[{names}]");

                if (download.IsValid()) Addressables.Release(download);
                if (beforeHandle.IsValid()) Addressables.Release(beforeHandle);
                if (afterHandle.IsValid()) Addressables.Release(afterHandle);
                if (locationsHandle.IsValid()) Addressables.Release(locationsHandle);
                Destroy(gameObject);
            }

            private static void CollectBundleNames(IResourceLocation location,
                HashSet<string> names, HashSet<IResourceLocation> visited)
            {
                if (location == null || !visited.Add(location)) return;
                string id = location.InternalId ?? string.Empty;
                if (id.IndexOf(".bundle", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string clean = id.Replace('\\', '/');
                    int query = clean.IndexOf('?');
                    if (query >= 0) clean = clean.Substring(0, query);
                    names.Add(Path.GetFileName(clean));
                }
                if (location.Dependencies == null) return;
                foreach (var dependency in location.Dependencies)
                    CollectBundleNames(dependency, names, visited);
            }
        }
    }
}
#endif
