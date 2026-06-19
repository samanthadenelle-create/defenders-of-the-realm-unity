// =============================================================================
// AddressableUIManager — async UI load / unload via Unity Addressables
// -----------------------------------------------------------------------------
// Manages four label groups:  UI-Core | UI-Debug | UI-Menus | UI-Tower
//
// SETUP (one-time in Unity Editor):
//   1. Window → Asset Management → Addressables → Groups
//   2. Create four groups:  UI-Core, UI-Debug, UI-Menus, UI-Tower
//   3. Assign the label matching the group name to each asset
//   4. Mark DebugCanvas.prefab as Addressable with address "UI/DebugCanvas"
//      and label "UI-Debug"
//   5. Build Addressables (Build → New Build → Default Build Script)
//
// Usage:
//   await AddressableUIManager.Instance.ShowAsync("UI/DebugCanvas", parent);
//   AddressableUIManager.Instance.Hide("UI/DebugCanvas");
// =============================================================================

using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.UI
{
    /// <summary>
    /// Singleton — async load / unload of UI prefabs via Addressables.
    /// Caches loaded handles so each address is only fetched once per session.
    /// </summary>
    public sealed class AddressableUIManager : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────────
        private static AddressableUIManager _instance;
        public  static AddressableUIManager Instance => _instance;

        // ── Addressable group labels — match the names in the Addressables window
        public const string LabelCore  = "UI-Core";
        public const string LabelDebug = "UI-Debug";
        public const string LabelMenus = "UI-Menus";
        public const string LabelTower = "UI-Tower";

        // ── Cached handles: address → (handle, instantiated root GO) ─────────
        private readonly Dictionary<string, AsyncOperationHandle<GameObject>> _handles =
            new Dictionary<string, AsyncOperationHandle<GameObject>>();
        private readonly Dictionary<string, GameObject> _instances =
            new Dictionary<string, GameObject>();

        // ── Lifecycle ─────────────────────────────────────────────────────────
        private void Awake()
        {
            if (_instance != null && _instance != this) { Destroy(gameObject); return; }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            ReleaseAll();
            if (_instance == this) _instance = null;
        }

        // ── Core API ──────────────────────────────────────────────────────────

        /// <summary>
        /// Loads (first call) or shows (subsequent calls) the prefab at
        /// <paramref name="address"/>. Instantiated under <paramref name="parent"/>
        /// if provided, otherwise under this manager's transform.
        /// Returns the instantiated root, or null on failure.
        /// </summary>
        public async UniTask<GameObject> ShowAsync(string address, Transform parent = null)
        {
            if (_instances.TryGetValue(address, out var existing))
            {
                existing.SetActive(true);
                return existing;
            }

            return await LoadAndInstantiate(address, parent ?? transform);
        }

        /// <summary>
        /// Deactivates (does NOT unload) the prefab at <paramref name="address"/>.
        /// The handle and instance remain cached for fast re-show.
        /// </summary>
        public void Hide(string address)
        {
            if (_instances.TryGetValue(address, out var go) && go != null)
                go.SetActive(false);
        }

        /// <summary>
        /// Destroys the instance and releases the Addressable handle for
        /// <paramref name="address"/>. Next <see cref="ShowAsync"/> re-fetches.
        /// </summary>
        public void Release(string address)
        {
            if (_instances.TryGetValue(address, out var go))
            {
                if (go != null) Destroy(go);
                _instances.Remove(address);
            }
            if (_handles.TryGetValue(address, out var handle))
            {
                // GUARD (WO-465): a release on an already-invalid handle throws — never let a teardown
                // throw out of Release. Self-report instead of crashing the caller.
                Guard.Try("UI", $"AddressableUIManager.Release '{address}'", () =>
                {
                    if (handle.IsValid()) Addressables.Release(handle);
                });
                _handles.Remove(address);
            }
        }

        /// <summary>Releases all cached handles. Called in OnDestroy.</summary>
        public void ReleaseAll()
        {
            foreach (var go in _instances.Values)
                if (go != null) Destroy(go);
            _instances.Clear();

            // GUARD each release independently (WO-465) so one invalid handle can't abort the rest
            // of the teardown and leak the others.
            foreach (var h in _handles.Values)
            {
                var handle = h;
                Guard.Try("UI", "AddressableUIManager.ReleaseAll handle", () =>
                {
                    if (handle.IsValid()) Addressables.Release(handle);
                });
            }
            _handles.Clear();
        }

        // ── Debug canvas convenience ──────────────────────────────────────────

        /// <summary>
        /// Loads and shows the debug overlay (address <c>"UI/DebugCanvas"</c>,
        /// label <c>UI-Debug</c>). Respects #if UNITY_EDITOR / DEVELOPMENT_BUILD.
        /// </summary>
        public async UniTask<GameObject> ShowDebugCanvasAsync()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            return await ShowAsync("UI/DebugCanvas");
#else
            return null;
#endif
        }

        // ── Internal ──────────────────────────────────────────────────────────

        private async UniTask<GameObject> LoadAndInstantiate(string address, Transform parent)
        {
            using var _ = FlowTrace.Enter("UI", $"AddressableUIManager.LoadAndInstantiate '{address}'");

            // GUARD the load (WO-465): a throwing/failed load self-reports via FlowTrace.Fail rather
            // than a Debug.LogError the harness never captures — so a missing UI address is pinpointed.
            AsyncOperationHandle<GameObject> handle;
            try
            {
                handle = Addressables.LoadAssetAsync<GameObject>(address);
                await handle;
            }
            catch (System.Exception ex)
            {
                FlowTrace.Fail("UI",
                    $"AddressableUIManager: load THREW for '{address}': {ex.GetType().Name}: {ex.Message} — returning null.");
                return null;
            }

            if (handle.Status != AsyncOperationStatus.Succeeded || handle.Result == null)
            {
                FlowTrace.Fail("UI",
                    $"AddressableUIManager: load FAILED for '{address}' (status={handle.Status}, result={(handle.Result == null ? "<null>" : "ok")}) — returning null.");
                if (handle.IsValid()) Addressables.Release(handle);
                return null;
            }

            GameObject go = null;
            FlowTrace.Try("UI", $"instantiate UI '{address}'", () =>
            {
                go = Instantiate(handle.Result, parent);
                go.name = handle.Result.name;
            });
            if (go == null)
            {
                // Instantiate threw / produced nothing — release the handle and report; do NOT
                // cache a null instance (a blank entry would masquerade as a loaded UI).
                FlowTrace.Fail("UI",
                    $"AddressableUIManager: Instantiate returned null for '{address}' — returning null, releasing handle.");
                if (handle.IsValid()) Addressables.Release(handle);
                return null;
            }

            _handles[address]   = handle;
            _instances[address] = go;

            // VISIBILITY VERIFY (WO-465 invisible-scrim class): a loaded+instantiated UI can STILL
            // render nothing — inactive root, or no usable draw surface (no UIDocument+PanelSettings
            // for UI Toolkit, and no Canvas for uGUI). "Instantiated" != "visible". Verify the root is
            // active in the hierarchy AND carries a usable surface; Fail-loud (the instance is kept so
            // the caller's own fallback decides, but the run self-reports the blank UI).
            VerifyInstantiatedRenders(go, address);

            FlowTrace.Step("UI", $"AddressableUIManager: loaded + instantiated '{address}'.");
            return go;
        }

        // Post-instantiate visibility verify (WO-465). A UI root must be active AND have a usable
        // draw surface: either a UI Toolkit UIDocument bound to a PanelSettings, or a uGUI Canvas.
        // No usable surface / inactive root => FlowTrace.Fail so a blank UI self-reports instead of
        // silently rendering nothing (the owner's empty-store / blocked-button symptoms).
        private static void VerifyInstantiatedRenders(GameObject go, string address)
        {
            if (go == null)
            {
                FlowTrace.Fail("UI", $"AddressableUIManager: '{address}' instance is null at verify.");
                return;
            }

            bool active = go.activeInHierarchy;

            var doc = go.GetComponentInChildren<UnityEngine.UIElements.UIDocument>(true);
            bool docOk = doc != null && doc.panelSettings != null;

            var canvas = go.GetComponentInChildren<Canvas>(true);
            bool canvasOk = canvas != null;

            bool hasSurface = docOk || canvasOk;

            FlowTrace.Step("UI",
                $"AddressableUIManager verify '{address}': active={active} uiDocument={(doc == null ? "<none>" : "ok")} " +
                $"panelSettings={(docOk ? "ok" : "<missing>")} canvas={(canvasOk ? "ok" : "<none>")} => hasSurface={hasSurface}");

            if (!active || !hasSurface)
            {
                FlowTrace.Fail("UI",
                    $"AddressableUIManager: '{address}' instantiated but NOT visible (active={active}, hasSurface={hasSurface}) " +
                    "— blank UI with no usable PanelSettings/Canvas (WO-465 invisible-scrim class).");
            }
        }
    }
}
