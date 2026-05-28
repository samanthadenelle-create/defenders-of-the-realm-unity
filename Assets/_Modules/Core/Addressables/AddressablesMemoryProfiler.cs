// =============================================================================
// AddressablesMemoryProfiler — runtime handle tracker + leak guard.
// -----------------------------------------------------------------------------
// Tracks every AsyncOperationHandle opened through SkinController so that
// unreleased handles (memory leaks) are surfaced during development. Integrates
// with DebugCanvasUI (F12 overlay) via the static HandleCount property.
//
// In player builds the static tracking is compiled out (#if UNITY_EDITOR or the
// EnableInDevelopmentBuild flag). The MonoBehaviour's leak-warning coroutine
// only runs when the component is active in scene.
//
// Static API (used by SkinController):
//   AddressablesMemoryProfiler.TrackHandle(address, handle)
//   AddressablesMemoryProfiler.UntrackHandle(address)
//   AddressablesMemoryProfiler.HandleCount        // for DebugCanvasUI overlay
//   AddressablesMemoryProfiler.GetReport()        // full handle list as string
//
// Scene setup: add to the same persistent GameObject as DebugCanvasUI.
// Not required for production; handles are tracked via static fields so the
// report is available even if the MonoBehaviour is not present.
// =============================================================================

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.ResourceManagement.AsyncOperations;

namespace DeNelle.Core.AssetDelivery
{
    /// <summary>
    /// Runtime profiler for Addressables handles. Surfaces leaks during
    /// development via log warnings and the DebugCanvasUI F12 overlay.
    /// </summary>
    public sealed class AddressablesMemoryProfiler : MonoBehaviour
    {
        // ── Inspector ─────────────────────────────────────────────────────────

        [Tooltip("Enable leak-warning logs outside the Unity Editor (e.g., development player builds).")]
        [SerializeField] private bool _enableInDevelopmentBuild = true;

        [Tooltip("How long a handle must be open (seconds) before it is flagged as a suspected leak.")]
        [SerializeField, Min(30f)] private float _leakThresholdSeconds = 120f;

        [Tooltip("How often to scan for suspected leaks (seconds).")]
        [SerializeField, Min(10f)] private float _scanIntervalSeconds  = 30f;

        // ── Static tracking ───────────────────────────────────────────────────

        private static readonly Dictionary<string, TrackedHandle> _handles
            = new Dictionary<string, TrackedHandle>(32);

        private static readonly object _lock = new object();

        private struct TrackedHandle
        {
            public AsyncOperationHandle Handle;
            public float OpenedAtTime;       // Time.realtimeSinceStartup
        }

        // ── Public API ────────────────────────────────────────────────────────

        /// <summary>Number of Addressables handles currently open and tracked.</summary>
        public static int HandleCount
        {
            get { lock (_lock) { return _handles.Count; } }
        }

        /// <summary>
        /// Registers a handle with the profiler. Called by <see cref="SkinController"/>
        /// immediately after a successful asset load.
        /// </summary>
        public static void TrackHandle(string address,
                                       AsyncOperationHandle<UnityEngine.Object> handle)
        {
            if (string.IsNullOrEmpty(address)) return;
            lock (_lock)
            {
                _handles[address] = new TrackedHandle
                {
                    Handle = handle,
                    OpenedAtTime = Time.realtimeSinceStartup
                };
            }
        }

        /// <summary>
        /// Removes a handle from the profiler. Called by <see cref="SkinController"/>
        /// just before releasing the handle.
        /// </summary>
        public static void UntrackHandle(string address)
        {
            if (string.IsNullOrEmpty(address)) return;
            lock (_lock) { _handles.Remove(address); }
        }

        /// <summary>
        /// Returns a formatted report of all currently open handles — address,
        /// load status, and seconds open. Used by DebugCanvasUI F12 overlay.
        /// </summary>
        public static string GetReport()
        {
            lock (_lock)
            {
                if (_handles.Count == 0) return "Addressables: 0 handles open.";

                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"Addressables handles open: {_handles.Count}");
                float now = Time.realtimeSinceStartup;
                foreach (var kv in _handles)
                {
                    float age = now - kv.Value.OpenedAtTime;
                    string status = kv.Value.Handle.IsValid()
                        ? kv.Value.Handle.Status.ToString()
                        : "Invalid";
                    sb.AppendLine($"  [{status}] {kv.Key}  ({age:F0} s)");
                }
                return sb.ToString();
            }
        }

        /// <summary>
        /// Forcibly releases ALL tracked handles. Only for emergencies (scene wipe).
        /// Prefer targeted ReleaseCurrent() via SkinController.RemoveSkin().
        /// </summary>
        public static void ReleaseAll()
        {
            lock (_lock)
            {
                foreach (var kv in _handles)
                {
                    if (kv.Value.Handle.IsValid())
                        UnityEngine.AddressableAssets.Addressables.Release(kv.Value.Handle);
                }
                _handles.Clear();
            }
            Debug.Log("[AddressablesMemoryProfiler] All tracked handles released.");
        }

        // ── MonoBehaviour — periodic leak scan ────────────────────────────────

        private void OnEnable()
        {
#if !UNITY_EDITOR
            if (!_enableInDevelopmentBuild || !Debug.isDebugBuild) return;
#endif
            StartCoroutine(LeakScanLoop());
        }

        private IEnumerator LeakScanLoop()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(_scanIntervalSeconds);
                ScanForLeaks();
            }
        }

        private void ScanForLeaks()
        {
            float now = Time.realtimeSinceStartup;
            List<string> suspected = null;

            lock (_lock)
            {
                foreach (var kv in _handles)
                {
                    float age = now - kv.Value.OpenedAtTime;
                    if (age >= _leakThresholdSeconds)
                    {
                        suspected ??= new List<string>();
                        suspected.Add($"'{kv.Key}' open for {age:F0} s");
                    }
                }
            }

            if (suspected != null && suspected.Count > 0)
            {
                Debug.LogWarning(
                    $"[AddressablesMemoryProfiler] {suspected.Count} suspected handle leak(s):\n" +
                    string.Join("\n", suspected) +
                    "\nCall SkinController.RemoveSkin() or Addressables.Release() on the owning component.");
            }
        }

        private void OnDisable()
        {
            StopAllCoroutines();
        }

#if UNITY_EDITOR
        [UnityEditor.MenuItem("Defenders/Debug/Addressables Handle Report")]
        private static void EditorPrintReport()
        {
            Debug.Log(GetReport());
        }

        [UnityEditor.MenuItem("Defenders/Debug/Addressables Release All (EMERGENCY)")]
        private static void EditorReleaseAll()
        {
            ReleaseAll();
        }
#endif
    }
}
