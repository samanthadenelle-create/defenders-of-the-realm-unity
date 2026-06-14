// =============================================================================
// SceneOwnership — the ONE runtime signal for "is the active scene enemy-owned?"
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// scene-configs.json (Data/Canonical) marks every base scene ownership:
// "Player" | "Enemy" (Garrison_troll_outpost / Garrison_ruined_keep /
// Garrison_frost_keep are Enemy; player scenes are Player). NOTHING read it at
// runtime, so enemy bases ran player behaviours (buildable, in-place respawn).
//
// This static resolves ownership on EVERY scene load by matching the loaded
// scene's name against configs[].sceneName and exposes a single bool:
//   • build mode is gated off in enemy-owned scenes (BuildModeController.Enter)
//   • hero death in an enemy-owned scene RETREATS to the home hub (HeroHealth)
//
// Default is Player-owned/safe (IsEnemyOwned == false): a scene with no matching
// config — i.e. every existing player scene — is unaffected. JSON read goes
// through the WebGL-safe CanonicalJson loader (Resources dual-copy first), parsed
// with Newtonsoft.Json — the SAME pattern GarrisonRecipeCatalog / Theme use.
// Parse failures LogWarning and leave IsEnemyOwned=false (no silent failure).
// =============================================================================

using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core;

namespace DeNelle.Village
{
    /// <summary>Runtime ownership flag for the active scene (enemy-owned vs player-owned/safe).</summary>
    public static class SceneOwnership
    {
        /// <summary>StreamingAssets-relative path (CanonicalJson strips the extension for Resources).</summary>
        public const string StreamingRelativePath = "Data/Canonical/scene-configs.json";

        /// <summary>True when the active scene is an enemy garrison/outpost; default false (safe).</summary>
        public static bool IsEnemyOwned { get; private set; }

        /// <summary>
        /// Force the enemy-owned flag ON/OFF for a scene the config map can't resolve
        /// by NAME. The config-generated raid scenes bake as <c>RaidBase_&lt;id&gt;</c>,
        /// which does NOT match the configs' <c>sceneName</c>, so the JSON-driven
        /// <see cref="Resolve"/> always reads them Player-owned. The RaidGarrisonSpawner
        /// (which carries the STORED config id, not a scene name) calls this so the
        /// turret-armer + the home-hub death-retreat treat the raid as enemy-owned.
        /// Internal: only sibling DeNelle.Village runtime sets this; never the JSON path.
        /// </summary>
        internal static void SetEnemyOwned(bool v)
        {
            IsEnemyOwned = v;
        }

        // Cached scene-name -> ownership map, built once from the JSON on first need.
        private static Dictionary<string, bool> _enemyByScene;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            // Subscribe once for all future loads, then resolve the already-active scene.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            Resolve(SceneManager.GetActiveScene().name);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Resolve(scene.name);
        }

        /// <summary>Sets <see cref="IsEnemyOwned"/> for <paramref name="sceneName"/>.</summary>
        private static void Resolve(string sceneName)
        {
            EnsureLoaded();

            bool enemy = false;
            if (!string.IsNullOrEmpty(sceneName) && _enemyByScene != null)
                _enemyByScene.TryGetValue(sceneName, out enemy);

            IsEnemyOwned = enemy;
            Debug.Log($"[Flow:World] SceneOwnership resolved '{sceneName}' -> " +
                      $"{(enemy ? "Enemy" : "Player")}-owned (IsEnemyOwned={enemy}).");
        }

        private static void EnsureLoaded()
        {
            if (_enemyByScene != null) return;
            _enemyByScene = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            try
            {
                string text = CanonicalJson.Read(StreamingRelativePath);
                if (string.IsNullOrEmpty(text))
                {
                    Debug.LogWarning($"[Flow:World] SceneOwnership: {StreamingRelativePath} not found — " +
                                     "all scenes default to Player-owned/safe.");
                    return;
                }

                var file = JsonConvert.DeserializeObject<SceneConfigFile>(text);
                if (file != null && file.configs != null)
                {
                    foreach (var c in file.configs)
                    {
                        if (c == null || string.IsNullOrEmpty(c.sceneName)) continue;
                        _enemyByScene[c.sceneName] =
                            string.Equals(c.ownership, "Enemy", StringComparison.OrdinalIgnoreCase);
                    }
                    Debug.Log($"[Flow:World] SceneOwnership loaded {_enemyByScene.Count} scene-config entr(ies).");
                }
                else
                {
                    Debug.LogWarning("[Flow:World] SceneOwnership: scene-configs.json parsed empty — " +
                                     "all scenes default to Player-owned/safe.");
                }
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Flow:World] SceneOwnership: failed to read scene-configs.json " +
                                 $"({ex.Message}) — all scenes default to Player-owned/safe.");
            }
        }

        // ── JSON shape (mirrors scene-configs.json: { configs:[{ sceneName, ownership, ... }] }) ──
        [Serializable]
        private sealed class SceneConfigFile
        {
            public List<SceneConfigEntry> configs;
        }

        [Serializable]
        private sealed class SceneConfigEntry
        {
            public string id;
            public string sceneName;
            public string ownership;
        }
    }
}
