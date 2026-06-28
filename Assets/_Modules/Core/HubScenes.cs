// =============================================================================
// HubScenes — the ONE canonical list of home/hub scenes.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core
//
// WHY: "is this a town/hub scene?" was answered in TWO drifted places — the HUD
// (`VillageHudController.EvaluateInVillage` only knew "Village2") and the world
// streamer (`WorldSceneLoader.HubSceneNames` knew the full set). That drift hid the
// whole town HUD on MainCastle_Hall (WO-411 root cause A). This is the single source
// both read, so adding a hub scene = one edit + the test below stays green.
//
// Lives in Core so BOTH DeNelle.Village (WorldSceneLoader) and DeNelle.HUD
// (VillageHudController) can reference it (Village→Core, HUD→Core; never Village↔HUD).
// =============================================================================
using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEngine;

namespace DeNelle.Core
{
    public static class HubScenes
    {
        /// <summary>Canonical home/hub scene names. Add a new hub here (and only here).</summary>
        public static readonly string[] Names = { "Village2", "MainCastle_Hall", "CastleHub", "CastleHub_MainKeep" };

        /// <summary>True if <paramref name="sceneName"/> is a home/hub scene (exact or prefix-contains,
        /// matching WorldSceneLoader's prior behaviour so CastleHub* variants still count).</summary>
        public static bool IsHub(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            for (int i = 0; i < Names.Length; i++)
                if (sceneName == Names[i] || sceneName.Contains(Names[i])) return true;
            return false;
        }

        /// <summary>True if <paramref name="sceneName"/> is an enemy RAID scene
        /// (<c>RaidBase_*</c>) — the single source both the HUD (combat-cluster gate,
        /// WO-457) and the Village (RaidDeployController self-install) read so the raid
        /// naming convention lives in ONE place.</summary>
        public static bool IsRaid(string sceneName)
        {
            return !string.IsNullOrEmpty(sceneName)
                && sceneName.StartsWith("RaidBase", System.StringComparison.OrdinalIgnoreCase);
        }

        // ─────────────────────────────────────────────────────────────────────
        //  Enemy-owned scene test (WO-470 / HUD-RCA)
        // -----------------------------------------------------------------------
        //  WHY HERE (architecture): the authoritative ownership flag lives in
        //  DeNelle.Village.SceneOwnership, but the HUD (DeNelle.HUD) may NOT
        //  reference DeNelle.Village (CLAUDE.md §5). It CAN reference Core. The
        //  ownership DATA — scene-configs.json — is read through the Core-side
        //  CanonicalJson loader (the SAME loader SceneOwnership/SceneConfigCatalog
        //  use). So we expose a Core-clean test that reads the SAME canonical data
        //  here, rather than adding a CoreServices delegate + a Village-side setter
        //  (more surface). One JSON parse, cached. SceneOwnership remains the
        //  runtime gameplay flag; this is the read-only mirror the HUD consumes.
        // ─────────────────────────────────────────────────────────────────────

        /// <summary>StreamingAssets-relative path (CanonicalJson strips the ext for Resources).</summary>
        private const string SceneConfigsPath = "Data/Canonical/scene-configs.json";

        // Cached sceneName(lower) -> isEnemy map, built once from scene-configs.json.
        private static Dictionary<string, bool> _enemyByScene;

        /// <summary>True if <paramref name="sceneName"/> is an ENEMY-OWNED scene per
        /// scene-configs.json (ownership == "Enemy"). HUD-readable mirror of
        /// DeNelle.Village.SceneOwnership without a Village reference (WO-470).
        /// Default false (safe) for any scene without a matching config entry.</summary>
        public static bool IsEnemyOwnedScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            EnsureOwnershipLoaded();
            return _enemyByScene != null
                && _enemyByScene.TryGetValue(sceneName, out bool enemy)
                && enemy;
        }

        /// <summary>WO-550 chokepoint: should TOWN / SOCIAL / ECONOMY HUD panels be SUPPRESSED
        /// in <paramref name="sceneName"/>? True for enemy-owned raid scenes (Village2), false for
        /// the home hub (MainCastle_Hall) and every other scene. The single semantic test the
        /// ~14 per-panel bootstraps gate on so combat scenes stop bootstrapping town panels
        /// (jukebox / clan chat / shops / crafting / quests / skill trees / building upgrade / swap).
        /// Combat-appropriate HUD (BattleHud9Zone, Compass, vitals, loadout) is NOT gated by this.</summary>
        public static bool SuppressTownHud(string sceneName) => IsEnemyOwnedScene(sceneName);

        private static void EnsureOwnershipLoaded()
        {
            if (_enemyByScene != null) return;
            _enemyByScene = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            try
            {
                string text = CanonicalJson.Read(SceneConfigsPath);
                if (string.IsNullOrEmpty(text))
                {
                    Debug.LogWarning("[Flow:HUD] HubScenes: scene-configs.json not found — " +
                                     "all scenes default to NOT enemy-owned.");
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
                }
            }
            catch (Exception ex)
            {
                // §12 no silent failure: report + default to not-enemy-owned (safe).
                Debug.LogWarning("[Flow:HUD] HubScenes: failed to read scene-configs.json (" +
                                 ex.Message + ") — all scenes default to NOT enemy-owned.");
            }
        }

        // Minimal JSON shape (mirrors scene-configs.json: { configs:[{ sceneName, ownership }] }).
        [Serializable]
        private sealed class SceneConfigFile
        {
            public List<SceneConfigEntry> configs;
        }

        [Serializable]
        private sealed class SceneConfigEntry
        {
            public string sceneName;
            public string ownership;
        }
    }
}
