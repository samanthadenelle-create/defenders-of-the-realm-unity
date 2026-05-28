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
                Addressables.Release(handle);
                _handles.Remove(address);
            }
        }

        /// <summary>Releases all cached handles. Called in OnDestroy.</summary>
        public void ReleaseAll()
        {
            foreach (var go in _instances.Values)
                if (go != null) Destroy(go);
            _instances.Clear();

            foreach (var h in _handles.Values)
                Addressables.Release(h);
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
            AsyncOperationHandle<GameObject> handle;
            try
            {
                handle = Addressables.LoadAssetAsync<GameObject>(address);
                await handle;
            }
            catch (System.Exception ex)
            {
                Debug.LogError($"[AddressableUIManager] Failed to load '{address}': {ex.Message}");
                return null;
            }

            if (handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[AddressableUIManager] Load failed for '{address}'.");
                Addressables.Release(handle);
                return null;
            }

            var go = Instantiate(handle.Result, parent);
            go.name = handle.Result.name;

            _handles[address]   = handle;
            _instances[address] = go;

            Debug.Log($"[AddressableUIManager] Loaded '{address}'.");
            return go;
        }
    }
}
