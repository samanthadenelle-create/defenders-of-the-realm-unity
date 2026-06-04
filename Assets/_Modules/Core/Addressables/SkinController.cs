// =============================================================================
// SkinController — reusable async Addressables skin loader for Tower / Pet / Hero.
// -----------------------------------------------------------------------------
// Attach one (or more) SkinController components to any prefab that can wear a
// cosmetic skin. Each instance manages a single "slot" (e.g. "body", "muzzle")
// so towers can independently skin their barrel vs. base.
//
// Lifecycle contract:
//   1. Call ApplySkinAsync(address) to load + apply a skin. Awaitable.
//   2. Call RemoveSkin() to release the handle and revert to the default visual.
//   3. On OnDestroy, any open handle is automatically released — no leaks.
//
// Supported skin asset types (detected automatically from the loaded Object type):
//   • Material   → swaps renderer.sharedMaterial
//   • Texture2D  → sets _BaseMap on a per-instance clone of the existing material
//   • GameObject → swaps the visual child (mirrors Tower.ApplyVisualForLevel)
//   • Mesh       → swaps meshFilter.sharedMesh
//
// Memory note: the handle is held on this component. If the component is part of
// a pooled object (e.g. projectile), call RemoveSkin() before returning to pool
// rather than relying on OnDestroy.
//
// Inspector setup:
//   • SkinSlot   — label for multi-slot prefabs ("body", "muzzle", etc.)
//   • TargetRenderer — the Renderer to receive Material / Texture swaps
//   • VisualParent  — parent Transform for GameObject swaps (Tower visual root)
// =============================================================================

using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DeNelle.Core.AssetDelivery
{
    // ── Public contract ───────────────────────────────────────────────────────

    /// <summary>
    /// Marker interface for anything that wears a cosmetic skin via Addressables.
    /// Implemented by Tower, Pet, and Hero adapters; checked by the skin picker UI.
    /// </summary>
    public interface ISkinnable
    {
        /// <summary>Addressables address of the currently equipped skin, or null.</summary>
        string CurrentSkinAddress { get; }
        /// <summary>Async — loads the asset at <paramref name="address"/> and applies it.</summary>
        UniTask ApplySkinAsync(string address, CancellationToken ct = default);
        /// <summary>Releases the current skin handle and reverts to the default visual.</summary>
        void RemoveSkin();
    }

    // ── SkinController ────────────────────────────────────────────────────────

    /// <summary>
    /// Per-slot cosmetic skin loader. Attach to Tower, Pet, and Hero prefabs.
    /// Multiple instances can coexist on one prefab for independent body/muzzle
    /// slot management.
    /// </summary>
    public sealed class SkinController : MonoBehaviour, ISkinnable
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Tooltip("Logical slot name — 'body', 'muzzle', 'base'. Used by skin picker to target the right controller.")]
        [SerializeField] public string SkinSlot = "body";

        [Tooltip("The Renderer that receives Material / Texture2D skin swaps.")]
        [SerializeField] private Renderer _targetRenderer;

        [Tooltip("Parent Transform for GameObject skin swaps. The previous child is destroyed; the new skin is instantiated here. Defaults to this transform.")]
        [SerializeField] private Transform _visualParent;

        // ── Runtime ───────────────────────────────────────────────────────────

        private AsyncOperationHandle<UnityEngine.Object> _currentHandle;
        private bool _handleOpen;

        private GameObject _spawnedVisual;
        private Material _clonedMaterial;   // per-instance mat clone for Texture2D path

        // ── ISkinnable ────────────────────────────────────────────────────────

        /// <inheritdoc/>
        public string CurrentSkinAddress { get; private set; }

        /// <inheritdoc/>
        public async UniTask ApplySkinAsync(string address, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(address)) { RemoveSkin(); return; }
            if (address == CurrentSkinAddress) return;   // already wearing this skin

            // Release any previously loaded skin handle.
            ReleaseCurrent();

            CurrentSkinAddress = address;

            var handle = UnityEngine.AddressableAssets.Addressables
                .LoadAssetAsync<UnityEngine.Object>(address);

            _currentHandle = handle;
            _handleOpen = true;

            try
            {
                await handle.ToUniTask(cancellationToken: ct);
            }
            catch (OperationCanceledException)
            {
                ReleaseCurrent();
                return;
            }
            catch (Exception ex)
            {
                Debug.LogError($"[SkinController] Failed to load skin '{address}': {ex.Message}");
                ReleaseCurrent();
                return;
            }

            if (!handle.IsValid() || handle.Status != AsyncOperationStatus.Succeeded)
            {
                Debug.LogError($"[SkinController] Handle invalid after load for '{address}'.");
                ReleaseCurrent();
                return;
            }

            ApplyAsset(handle.Result);

            // Register with the memory profiler so leak detection works.
            AddressablesMemoryProfiler.TrackHandle(address, handle);
        }

        /// <inheritdoc/>
        public void RemoveSkin()
        {
            ReleaseCurrent();
            CurrentSkinAddress = null;
            RevertToDefault();
        }

        // ── Apply logic ───────────────────────────────────────────────────────

        private void ApplyAsset(UnityEngine.Object asset)
        {
            switch (asset)
            {
                case Material mat:
                    ApplyMaterial(mat);
                    break;
                case Texture2D tex:
                    ApplyTexture(tex);
                    break;
                case GameObject prefab:
                    ApplyGameObject(prefab);
                    break;
                case Mesh mesh:
                    ApplyMesh(mesh);
                    break;
                default:
                    Debug.LogWarning($"[SkinController] Unsupported skin asset type: {asset?.GetType().Name} at '{CurrentSkinAddress}'.");
                    break;
            }
        }

        private void ApplyMaterial(Material mat)
        {
            var r = ResolveRenderer();
            if (r == null) return;
            r.sharedMaterial = mat;
        }

        private void ApplyTexture(Texture2D tex)
        {
            var r = ResolveRenderer();
            if (r == null) return;

            // Clone the existing material once so we don't mutate the shared asset.
            if (_clonedMaterial == null)
                _clonedMaterial = new Material(r.sharedMaterial);

            _clonedMaterial.SetTexture("_BaseMap", tex);     // URP
            if (_clonedMaterial.HasProperty("_MainTex"))
                _clonedMaterial.SetTexture("_MainTex", tex); // legacy fallback
            r.sharedMaterial = _clonedMaterial;
        }

        private void ApplyGameObject(GameObject prefab)
        {
            // Destroy the previously spawned visual (if any).
            if (_spawnedVisual != null) Destroy(_spawnedVisual);

            var parent = _visualParent != null ? _visualParent : transform;
            _spawnedVisual = Instantiate(prefab, parent);
            _spawnedVisual.transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
        }

        private void ApplyMesh(Mesh mesh)
        {
            var mf = GetComponentInChildren<MeshFilter>();
            if (mf != null) mf.sharedMesh = mesh;
        }

        // ── Revert ───────────────────────────────────────────────────────────

        private void RevertToDefault()
        {
            if (_spawnedVisual != null) { Destroy(_spawnedVisual); _spawnedVisual = null; }
            if (_clonedMaterial != null) { Destroy(_clonedMaterial); _clonedMaterial = null; }
            // Material / Mesh reverts: the caller (TowerVisualManager, PetDeployer, etc.)
            // is responsible for re-applying the base visual when RemoveSkin is called.
        }

        // ── Handle management ─────────────────────────────────────────────────

        private void ReleaseCurrent()
        {
            if (!_handleOpen) return;
            if (_currentHandle.IsValid())
            {
                AddressablesMemoryProfiler.UntrackHandle(CurrentSkinAddress);
                UnityEngine.AddressableAssets.Addressables.Release(_currentHandle);
            }
            _handleOpen = false;
        }

        // ── Lifecycle ─────────────────────────────────────────────────────────

        private void OnDestroy()
        {
            ReleaseCurrent();
            if (_clonedMaterial != null) Destroy(_clonedMaterial);
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private Renderer ResolveRenderer()
        {
            if (_targetRenderer != null) return _targetRenderer;
            _targetRenderer = GetComponentInChildren<Renderer>();
            if (_targetRenderer == null)
                Debug.LogWarning($"[SkinController] No Renderer found on '{name}' for slot '{SkinSlot}'.");
            return _targetRenderer;
        }
    }
}
