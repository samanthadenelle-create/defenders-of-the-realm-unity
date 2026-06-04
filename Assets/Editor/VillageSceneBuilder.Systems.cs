using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Editor
{
    // WO-181: enemy/building prefabs, wave manager, spawn/boss wiring -- split out of VillageSceneBuilder.cs. Same partial class; moves only.
    public static partial class VillageSceneBuilder
    {
        private static GameObject EnsureEnemyPrefab()
        {
            string prefabPath = GeneratedPrefabDir + "/Enemy_HollowWalker.prefab";

            var enemyType = FindType(TypeEnemy);
            var enemyDamageableType = FindType(TypeEnemyDamageable);
            if (enemyType == null)
            {
                Debug.LogError("[VillageSceneBuilder] DeNelle.Village.Enemy not found -- " +
                               "enemy prefab skipped; WaveManager will spawn placeholders.");
                return null;
            }

            // Build the prefab content in a temp scene object.
            var go = new GameObject("Enemy_HollowWalker");
            try
            {
                go.layer = EnemyLayer;

                // KayKit skeleton mesh as the visual child (placeholder capsule
                // on a miss — same fallback discipline as the rest of the builder).
                var skeleton = LoadModel(SkeletonMinionPath);
                GameObject visual = InstantiateModel(skeleton, "Skeleton_Blade.fbx",
                    "Hollow Walker enemy");
                visual.transform.SetParent(go.transform, false);
                if (skeleton == null)
                {
                    visual.transform.localScale = new Vector3(0.6f, 1.0f, 0.6f);
                    visual.transform.localPosition = new Vector3(0f, 1.0f, 0f);
                    ApplyColor(visual, new Color(0.78f, 0.80f, 0.74f)); // bone
                }
                // The skeleton mesh + children should not collide / block — the
                // Enemy's own capsule collider is the single physics body.
                StripColliders(visual);
                SetLayerRecursive(go, EnemyLayer);

                // Capsule collider — the body hero abilities + pets sweep for
                // (Physics.OverlapSphere with QueryTriggerInteraction.Collide
                // still finds a trigger). It is a TRIGGER for the same reason
                // WaveManager's placeholder capsule is: Enemy.ProbeForStructure
                // forward-SphereCasts with QueryTriggerInteraction.Ignore, so a
                // trigger body is skipped and never shadows the real structure
                // ahead. A trigger body also keeps enemies from physically
                // jostling each other / the navmesh agents.
                var capsule = go.AddComponent<CapsuleCollider>();
                capsule.height = 2.0f;
                capsule.radius = 0.45f;
                capsule.center = new Vector3(0f, 1.0f, 0f);
                capsule.isTrigger = true;

                // Enemy — [RequireComponent(typeof(NavMeshAgent))] adds the agent.
                go.AddComponent(enemyType);
                // EnemyDamageable adapter — hero abilities + pets find IDamageable
                // through it (week4-hero-pets-gate.md item 1).
                if (enemyDamageableType != null)
                {
                    if (go.GetComponent(enemyDamageableType) == null)
                        go.AddComponent(enemyDamageableType);
                }
                else
                {
                    Debug.LogWarning("[VillageSceneBuilder] EnemyDamageable type not found -- " +
                                     "hero abilities + pets will not be able to hit enemies.");
                }

                // Tune the NavMeshAgent so it sits cleanly on the baked mesh.
                var agent = go.GetComponent<UnityEngine.AI.NavMeshAgent>();
                if (agent != null)
                {
                    agent.radius = 0.4f;
                    agent.height = 2.0f;
                    agent.baseOffset = 0f;
                    agent.speed = 2.5f;            // EnemyDef overrides at Configure
                    agent.angularSpeed = 360f;
                    agent.acceleration = 24f;
                }

                var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                if (prefab == null)
                    Debug.LogError($"[VillageSceneBuilder] Failed to save enemy prefab at '{prefabPath}'.");
                return prefab;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(go);
            }
        }

        // =====================================================================
        //  Building prefabs — one per BuildingType, each with a Building
        // =====================================================================

        /// <summary>
        /// One built building prefab paired with its BuildingType ordinal — fed
        /// to BuildMenu's <c>_buildingPrefabs</c> list.
        /// </summary>
        private struct BuiltBuildingPrefab
        {
            public int TypeOrdinal;     // BuildingType enum ordinal
            public GameObject Prefab;
        }

        /// <summary>
        /// Builds (or refreshes) one placeable prefab per BuildingType, each
        /// carrying a <c>Building</c> component + the KayKit building mesh, and
        /// returns them. Fed into <c>BuildMenu._buildingPrefabs</c> so the build
        /// menu can place player-built buildings (week4-buildings.md item 5).
        /// Reuses the <see cref="Buildings"/> placement table for the mesh names.
        /// </summary>
        private static List<BuiltBuildingPrefab> EnsureBuildingPrefabs()
        {
            var result = new List<BuiltBuildingPrefab>();
            var buildingType = FindType(TypeBuilding);
            if (buildingType == null)
            {
                Debug.LogError("[VillageSceneBuilder] DeNelle.Village.Building not found -- " +
                               "building prefabs skipped; the build menu will have no prefabs.");
                return result;
            }

            foreach (var b in Buildings)
            {
                string prefabPath = $"{GeneratedPrefabDir}/Building_{b.Id}.prefab";
                var go = new GameObject($"Building_{b.Id}");
                try
                {
                    var model = LoadModel(Building(b.Fbx));
                    GameObject visual = InstantiateModel(model,
                        b.Fbx + "_" + BuildingColor + ".fbx", $"{b.Fbx} -> {b.Label}");
                    visual.transform.SetParent(go.transform, false);
                    if (model == null)
                    {
                        visual.transform.localScale = new Vector3(3f, 3f, 3f);
                        visual.transform.localPosition = new Vector3(0f, 1.5f, 0f);
                        ApplyColor(visual, b.PlaceholderColor);
                    }

                    // Building.EnsureBlocker() adds a BoxCollider at runtime; add
                    // one now so the saved prefab carries the footprint blocker.
                    var blocker = go.AddComponent<BoxCollider>();
                    blocker.size = new Vector3(3.2f, 3f, 3.2f);
                    blocker.center = new Vector3(0f, 1.5f, 0f);

                    // Building component — Configure(BuildingDef) is called at
                    // runtime by BuildMenu after the prefab is instantiated.
                    var comp = go.AddComponent(buildingType);
                    InvokeConfigure(comp, "Configure", b.Type, b.Id, b.Label);
                    // Pull HP / cost / display-name key from buildings.json so the
                    // saved prefab is data-correct even before BuildMenu re-Configures.
                    InvokeConfigure(comp, "ConfigureFromCatalog", b.Id);

                    var prefab = PrefabUtility.SaveAsPrefabAsset(go, prefabPath);
                    if (prefab != null)
                        result.Add(new BuiltBuildingPrefab { TypeOrdinal = b.Type, Prefab = prefab });
                    else
                        Debug.LogError($"[VillageSceneBuilder] Failed to save building prefab '{prefabPath}'.");
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(go);
                }
            }

            Debug.Log($"[VillageSceneBuilder] Built {result.Count}/5 building prefabs for the build menu.");
            return result;
        }

        // =====================================================================
        //  WaveManager
        // =====================================================================

        /// <summary>
        /// Adds the <c>WaveManager</c> sub-system GameObject and wires it to the
        /// Heart, the scene's WaveSpawnPoints and the enemy prefab. WaveManager
        /// also auto-finds the Heart + spawn points at Start, but wiring them
        /// here makes the scene self-describing (week4-waves.md item 2).
        /// </summary>
        private static void BuildWaveManager(Transform parent, Component heart, GameObject enemyPrefab)
        {
            var go = new GameObject("WaveManager");
            go.transform.SetParent(parent, false);

            var comp = AddVillageComponent(go, TypeWaveManager);
            if (comp == null) return;

            var so = new SerializedObject(comp);

            // _heart — the HeartController the enemies march toward.
            if (heart != null) SetObjectField(so, "_heart", heart);

            // _enemyRoot — a tidy parent for spawned enemies.
            var enemyRoot = NewChild(parent, "WaveEnemies");
            SetObjectField(so, "_enemyRoot", enemyRoot);

            // _enemyPrefab — typed `Enemy`; assign the prefab's Enemy component.
            if (enemyPrefab != null)
            {
                var enemyType = FindType(TypeEnemy);
                var enemyComp = enemyType != null ? enemyPrefab.GetComponent(enemyType) : null;
                if (enemyComp != null) SetObjectField(so, "_enemyPrefab", enemyComp);
            }

            // _apexBossPrefab — typed `DragonBoss`; assign the Boss_Dragon
            // prefab's DragonBoss component so the apex wave (waves.json wave 4,
            // "The Last Wing") can release the flying boss. The prefab is built
            // by DragonAnimatorSetup; a miss is non-fatal — the apex wave then
            // logs an error at runtime and clears (the loop never stalls).
            WireApexBossPrefab(so);

            // _spawnPoints — the list of WaveSpawnPoints already placed by the
            // approach-lane builder. Populate the serialized List<WaveSpawnPoint>.
            WireSpawnPointList(so, "_spawnPoints");

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        /// <summary>
        /// Loads the Boss_Dragon prefab and wires its <c>DragonBoss</c> component
        /// into <c>WaveManager._apexBossPrefab</c> (the field is typed
        /// <c>DragonBoss</c>). The prefab is produced by
        /// <c>DragonAnimatorSetup.BuildDragonBossPrefab</c> — if it has not been
        /// built yet the wiring is skipped with a warning; the apex wave then
        /// logs its own error at runtime rather than the loop stalling.
        /// </summary>
        private static void WireApexBossPrefab(SerializedObject so)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BossDragonPrefabPath);
            if (prefab == null)
            {
                Debug.LogWarning(
                    "[VillageSceneBuilder] Boss_Dragon prefab not found at " +
                    $"'{BossDragonPrefabPath}' -- apex-boss wave will have no dragon. " +
                    "Run Defenders > Animation > Build Dragon Boss first.");
                return;
            }

            var dragonType = FindType(TypeDragonBoss);
            if (dragonType == null)
            {
                Debug.LogError("[VillageSceneBuilder] DeNelle.Village.DragonBoss not found -- " +
                               "is the DeNelle.Village assembly compiled? Apex-boss prefab not wired.");
                return;
            }

            var dragonComp = prefab.GetComponent(dragonType);
            if (dragonComp == null)
            {
                Debug.LogError(
                    $"[VillageSceneBuilder] Boss_Dragon prefab at '{BossDragonPrefabPath}' " +
                    "carries no DragonBoss component -- apex-boss prefab not wired.");
                return;
            }

            SetObjectField(so, "_apexBossPrefab", dragonComp);
            Debug.Log("[VillageSceneBuilder] WaveManager._apexBossPrefab wired to Boss_Dragon " +
                      "(apex wave 'The Last Wing' will release Syndrath the Devourer).");
        }

        /// <summary>
        /// Fills a serialized <c>List&lt;WaveSpawnPoint&gt;</c> field with every
        /// WaveSpawnPoint component in the open scene.
        /// </summary>
        private static void WireSpawnPointList(SerializedObject so, string field)
        {
            var prop = so.FindProperty(field);
            if (prop == null || !prop.isArray)
            {
                Debug.LogWarning($"[VillageSceneBuilder] Serialized list '{field}' not found / not an array " +
                                 $"on {so.targetObject.GetType().Name} -- spawn points left for auto-find.");
                return;
            }

            var spawnType = FindType(TypeWaveSpawnPoint);
            if (spawnType == null) return;

            var spawns = UnityEngine.Object.FindObjectsByType(
                spawnType, FindObjectsSortMode.None);
            prop.arraySize = spawns.Length;
            for (int i = 0; i < spawns.Length; i++)
                prop.GetArrayElementAtIndex(i).objectReferenceValue = spawns[i];
        }
    }
}
