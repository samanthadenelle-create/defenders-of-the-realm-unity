// =============================================================================
// PersistenceBridge — wires GameStateService save/load to game lifecycle events.
// -----------------------------------------------------------------------------
// WO1: Backend Persistence Foundation.
//
// This DontDestroyOnLoad MonoBehaviour closes the three gaps that prevented
// player progress from persisting across scene changes and app restarts:
//
//   GAP 1 — Wave clear → backend sync
//     CompleteWave() fires WaveManager.OnWaveCleared but nothing called
//     GameStateService.SyncAfterWave. This bridge auto-finds the WaveManager
//     after each scene load and subscribes. On clear: SyncAfterWave(waveId)
//     triggers a high-priority backend push immediately.
//
//   GAP 2 — Scene enter → load from backend
//     LoadFromBackend was never called at runtime. This bridge calls it on
//     every Village and TowerDefense scene enter, pulling the authoritative
//     server record and merging onto the local GameState SO. Merge policy:
//     server wins BestWave (anti-rollback); local wins Towers/Pets.
//
//   GAP 3 — App quit → local save
//     If the player force-quits between sync windows, unsaved progress is lost.
//     Application.quitting → Save() flushes everything to PlayerPrefs as a
//     last resort. (SaveBeforeSceneChange already handles deliberate transitions
//     via SceneRouter.)
//
// SETUP:
//   Add PersistenceBridge to the same persistent GameObject as GameStateService
//   (the CoreBootstrap / ServiceLocator root). It self-bootstraps via
//   PersistenceBridge.EnsureExists() which is called from GameStateService.Awake.
//   Do NOT place it in per-scene GameObjects — it must survive scene changes.
//
// SCENE NAMES — matches SceneRouter constants:
//   "Village", "ATBBattle", "PatriciaLight_TD"
//   Add any new scene that should trigger a load by extending _loadOnEnterScenes.
// =============================================================================

using System;
using System.Collections.Generic;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.SceneManagement;

namespace DeNelle.Core.State
{
    /// <summary>
    /// Persistent bridge that wires <see cref="GameStateService"/> save/load
    /// calls to wave-clear events, scene transitions, and application quit.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PersistenceBridge : MonoBehaviour
    {
        // ── Scenes that trigger a backend load on enter ───────────────────────

        private static readonly HashSet<string> _loadOnEnterScenes = new HashSet<string>
        {
            "Village",
            "ATBBattle",
            "PatriciaLight_TD",
        };

        // ── Runtime ───────────────────────────────────────────────────────────

        private static PersistenceBridge _instance;

        // WaveManager lives in DeNelle.Village, which this (DeNelle.Core) assembly
        // cannot reference without a circular dependency. Bridge to it via reflection
        // (the codebase's cross-asmdef pattern): cache the boxed instance, its boxed
        // OnWaveCleared UnityEvent<int>, and the listener delegate.
        private object _waveManager;
        private object _waveClearedEvent;
        private UnityAction<int> _waveClearedHandler;

        // ── Bootstrap ─────────────────────────────────────────────────────────

        /// <summary>
        /// Called by <see cref="GameStateService.Awake"/> to ensure exactly one
        /// PersistenceBridge exists across all scenes.
        /// </summary>
        public static void EnsureExists()
        {
            if (_instance != null) return;

            var go = new GameObject("[PersistenceBridge]");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<PersistenceBridge>();
        }

        // ── Unity lifecycle ───────────────────────────────────────────────────

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void OnEnable()
        {
            SceneManager.sceneLoaded += OnSceneLoaded;
            Application.quitting    += OnApplicationQuitting;
        }

        private void OnDisable()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            Application.quitting    -= OnApplicationQuitting;
            UnsubscribeWaveManager();
        }

        // ── GAP 2 — Scene enter → LoadFromBackend ────────────────────────────

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            // Wire WaveManager subscription via reflection (null-safe — most scenes
            // won't have one; Core can't reference Village directly).
            UnsubscribeWaveManager();
            WireWaveManager();

            // GAP 2: pull latest backend state when entering gameplay scenes.
            if (_loadOnEnterScenes.Contains(scene.name))
                LoadFromBackendAsync().Forget();
        }

        private async UniTaskVoid LoadFromBackendAsync()
        {
            var svc = GameStateService.Instance;
            if (svc == null) return;

            await UniTask.Delay(200); // brief yield — let scene Awakes run first
            await svc.LoadFromBackend();
            Debug.Log($"[PersistenceBridge] LoadFromBackend complete for scene '{SceneManager.GetActiveScene().name}'.");
        }

        // ── GAP 1 — Wave clear → SyncAfterWave ───────────────────────────────

        private void OnWaveCleared(int waveId)
        {
            SyncAfterWaveAsync(waveId).Forget();
        }

        private async UniTaskVoid SyncAfterWaveAsync(int waveId)
        {
            var svc = GameStateService.Instance;
            if (svc == null) return;

            await svc.SyncAfterWave(waveId);
            Debug.Log($"[PersistenceBridge] SyncAfterWave complete — wave {waveId} persisted.");
        }

        // ── GAP 3 — App quit → local save ────────────────────────────────────

        private void OnApplicationQuitting()
        {
            GameStateService.Instance?.Save();
            Debug.Log("[PersistenceBridge] Application quitting — local save flushed.");
        }

        // ── Helpers ───────────────────────────────────────────────────────────

        private void WireWaveManager()
        {
            var wmType = Type.GetType("DeNelle.Village.WaveManager, DeNelle.Village");
            if (wmType == null) return;

            var found = FindObjectsByType(wmType, FindObjectsSortMode.None);
            if (found == null || found.Length == 0) return;
            _waveManager = found[0];

            // OnWaveCleared is a public UnityEvent<int> (field, or property fallback).
            var field = wmType.GetField("OnWaveCleared");
            _waveClearedEvent = field != null
                ? field.GetValue(_waveManager)
                : wmType.GetProperty("OnWaveCleared")?.GetValue(_waveManager);
            if (_waveClearedEvent == null) { _waveManager = null; return; }

            _waveClearedHandler = OnWaveCleared;
            _waveClearedEvent.GetType().GetMethod("AddListener")
                ?.Invoke(_waveClearedEvent, new object[] { _waveClearedHandler });
        }

        private void UnsubscribeWaveManager()
        {
            if (_waveClearedEvent != null && _waveClearedHandler != null)
            {
                _waveClearedEvent.GetType().GetMethod("RemoveListener")
                    ?.Invoke(_waveClearedEvent, new object[] { _waveClearedHandler });
            }
            _waveManager = null;
            _waveClearedEvent = null;
            _waveClearedHandler = null;
        }
    }
}
