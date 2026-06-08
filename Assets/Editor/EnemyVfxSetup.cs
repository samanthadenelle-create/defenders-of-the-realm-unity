// =============================================================================
// EnemyVfxSetup (DEF-48 follow-up) — wires a default EnemyTypeVfxSet onto wave
// enemy prefabs so attacks get a wind-up telegraph instead of landing instantly.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor   Namespace: DeNelle.Editor   (editor-only)
//
// PROBLEM:
//   Enemy.cs reads `_typeVfxSet.TelegraphDuration` to decide whether to play a
//   wind-up before contact damage. Every wave enemy prefab had `_typeVfxSet`
//   null and NO EnemyTypeVfxSet asset existed, so telegraphDuration was always
//   0 -> instant attacks (felt unfair).
//
// WHAT THIS DOES (idempotent):
//   1. Creates (or reuses) an EnemyTypeVfxSet asset at
//      Assets/Resources/Enemies/EnemyVfxSet_Default.asset with
//      TelegraphDuration = 0.5s. Optional VFX-prefab fields stay null — the
//      telegraph DURATION is what plays the WindUp anim + grants reaction time.
//   2. Scans enemy prefabs (Assets/Prefabs/Village/Generated/Enemy_*.prefab and
//      Assets/Resources/Enemies/*.prefab) and, for any that has an Enemy
//      component with a null _typeVfxSet, assigns the default set via
//      SerializedObject and saves the prefab.
//
// WHY REFLECTION:
//   DeNelle.Editor.asmdef intentionally does NOT reference DeNelle.Village
//   (CLAUDE.md §5 boundary). Enemy / EnemyTypeVfxSet are resolved by type name,
//   and all field writes go through SerializedObject — which needs no
//   compile-time knowledge of the concrete types.
//
// RUN:
//   Editor menu : Defenders/Combat/Setup Enemy VFX Sets
//   Batchmode   : DeNelle.Editor.EnemyVfxSetup.Apply
// =============================================================================

using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    /// <summary>
    /// Editor tool that creates a default EnemyTypeVfxSet with a readable
    /// telegraph duration and assigns it to wave enemy prefabs whose _typeVfxSet
    /// field is still null. Idempotent — safe to re-run. Uses reflection +
    /// SerializedObject to honour the DeNelle.Editor → no-Village boundary.
    /// </summary>
    public static class EnemyVfxSetup
    {
        private const string AssetPath  = "Assets/Resources/Enemies/EnemyVfxSet_Default.asset";
        private const string AssetDir   = "Assets/Resources/Enemies";
        private const float  TelegraphSeconds = 0.5f;

        private const string EnemyTypeName    = "DeNelle.Village.Enemy, DeNelle.Village";
        private const string VfxSetTypeName   = "DeNelle.Village.EnemyTypeVfxSet, DeNelle.Village";

        // Folders scanned for enemy prefabs (any prefab here with an Enemy
        // component and a null _typeVfxSet gets the default set).
        private static readonly string[] PrefabSearchFolders =
        {
            "Assets/Prefabs/Village/Generated",
            "Assets/Resources/Enemies",
        };

        [MenuItem("Defenders/Combat/Setup Enemy VFX Sets")]
        public static void Apply()
        {
            Type enemyType  = Type.GetType(EnemyTypeName);
            Type vfxSetType = Type.GetType(VfxSetTypeName);
            if (enemyType == null || vfxSetType == null)
            {
                Debug.LogError($"ENEMY_VFX FAIL: could not resolve types " +
                               $"(Enemy={enemyType != null}, EnemyTypeVfxSet={vfxSetType != null}). " +
                               "Is DeNelle.Village compiled?");
                return;
            }

            // ── 1. Ensure the default EnemyTypeVfxSet asset exists ───────────────
            var set = AssetDatabase.LoadAssetAtPath(AssetPath, vfxSetType) as ScriptableObject;
            if (set == null)
            {
                if (!AssetDatabase.IsValidFolder(AssetDir))
                {
                    Debug.LogError($"ENEMY_VFX FAIL: folder not found: {AssetDir}");
                    return;
                }

                set = ScriptableObject.CreateInstance(vfxSetType);
                AssetDatabase.CreateAsset(set, AssetPath);
                Debug.Log($"[EnemyVfxSetup] Created EnemyTypeVfxSet asset at {AssetPath}");
            }
            else
            {
                Debug.Log($"[EnemyVfxSetup] Reusing existing EnemyTypeVfxSet asset at {AssetPath}");
            }

            // Ensure TelegraphDuration is set (private serialized field — write via
            // SerializedObject so we don't need a public setter on the SO).
            var setSO = new SerializedObject(set);
            SerializedProperty telegraphProp = setSO.FindProperty("_telegraphDuration");
            if (telegraphProp == null)
            {
                Debug.LogError("ENEMY_VFX FAIL: EnemyTypeVfxSet has no _telegraphDuration field.");
                return;
            }
            if (!Mathf.Approximately(telegraphProp.floatValue, TelegraphSeconds))
            {
                telegraphProp.floatValue = TelegraphSeconds;
                setSO.ApplyModifiedPropertiesWithoutUndo();
                EditorUtility.SetDirty(set);
                Debug.Log($"[EnemyVfxSetup] Set TelegraphDuration = {TelegraphSeconds:0.##}s on default set.");
            }

            // ── 2. Assign to enemy prefabs with a null _typeVfxSet ───────────────
            string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", PrefabSearchFolders);
            var processedPaths = new HashSet<string>();
            int assigned = 0, alreadySet = 0, noEnemy = 0;

            foreach (string guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (!processedPaths.Add(path)) continue; // de-dupe overlapping folders

                GameObject root = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (root == null) continue;

                // Look on the root and anywhere in the hierarchy for an Enemy.
                Component enemy = root.GetComponentInChildren(enemyType, true);
                if (enemy == null) { noEnemy++; continue; }

                var enemySO = new SerializedObject(enemy);
                SerializedProperty vfxProp = enemySO.FindProperty("_typeVfxSet");
                if (vfxProp == null)
                {
                    Debug.LogWarning($"[EnemyVfxSetup] {path}: Enemy has no _typeVfxSet field — skipped.");
                    continue;
                }

                if (vfxProp.objectReferenceValue != null)
                {
                    alreadySet++;
                    Debug.Log($"[EnemyVfxSetup] SKIP (already set): {path}");
                    continue;
                }

                vfxProp.objectReferenceValue = set;
                enemySO.ApplyModifiedPropertiesWithoutUndo();
                PrefabUtility.SavePrefabAsset(root);
                assigned++;
                Debug.Log($"[EnemyVfxSetup] ASSIGNED default VFX set -> {path}");
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"ENEMY_VFX_OK assigned={assigned} alreadySet={alreadySet} " +
                      $"noEnemyComponent={noEnemy} telegraph={TelegraphSeconds:0.##}s asset={AssetPath}");
        }
    }
}
