// =============================================================================
// PurgeAtbBattleUiDocument - removes the dead BattleHUD.uxml UIDocument from
// Assets/Scenes/ATBBattle.unity.
// -----------------------------------------------------------------------------
// WHY: UXML-sourced UIDocuments come up BLANK in player builds (project memory,
// stated at PetSelectController.cs:9-11, which names BattleHUD as one of the two
// that "learned the hard way"). ATBBattle.unity still carried an ENABLED
// UIDocument bound to BattleHUD.uxml even though the shipping HUD is the
// code-built uGUI one: BattleController.cs builds `new GameObject("BattleHudUgui")`
// + AddComponent<BattleHudUgui>() + Build() at runtime, under a comment that reads
// "Self-contained - creates BattleHUD_Canvas if needed. No UIDocument / UXML."
//
// SAFETY, verified at source before this was written:
//   * The UIDocument (fileID 1032066911) shares its GameObject (1032066908) with
//     BattleController, so the COMPONENT is removed and the GameObject is LEFT
//     ALONE. Destroying the object would take BattleController with it.
//   * The scene YAML still carries a stale `_hudDocument` entry on the
//     BattleController MonoBehaviour, but the C# class has NO such field any more
//     (grep of BattleController.cs returns zero hits), so Unity already ignores it
//     and nothing in code dereferences the document.
//
// Scene edits go through the Unity API here, never a hand-edit of the .unity YAML
// (CLAUDE.md 3 - resave-corruption history).
//
// Run: DeNelle.Editor.PurgeAtbBattleUiDocument.Run   (batchmode, editor closed)
// Emits: ATB_UIDOC_PURGE_OK <n>  |  ATB_UIDOC_PURGE_FAIL <reason>
// =============================================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;

namespace DeNelle.Editor
{
    public static class PurgeAtbBattleUiDocument
    {
        private const string ScenePath = "Assets/Scenes/ATBBattle.unity";

        [MenuItem("Defenders/Maintenance/Purge ATBBattle UIDocument")]
        public static void Run()
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                Debug.LogError("ATB_UIDOC_PURGE_FAIL scene-not-found " + ScenePath);
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                Debug.LogError("ATB_UIDOC_PURGE_FAIL scene-invalid " + ScenePath);
                return;
            }

            var docs = new List<UIDocument>();
            foreach (var root in scene.GetRootGameObjects())
                docs.AddRange(root.GetComponentsInChildren<UIDocument>(true));

            if (docs.Count == 0)
            {
                // Idempotent: already purged is a success, not a failure.
                Debug.Log("ATB_UIDOC_PURGE_OK 0 (already clean)");
                return;
            }

            int removed = 0;
            foreach (var doc in docs)
            {
                if (doc == null) continue;
                var owner = doc.gameObject;
                var asset = doc.visualTreeAsset != null ? doc.visualTreeAsset.name : "<null>";
                Debug.Log("[PurgeAtbBattleUiDocument] removing UIDocument on '" + owner.name +
                          "' sourceAsset='" + asset + "' (GameObject KEPT - it also carries " +
                          owner.GetComponents<Component>().Length + " other component(s))");
                Object.DestroyImmediate(doc, true);
                removed++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            if (!EditorSceneManager.SaveScene(scene, ScenePath))
            {
                Debug.LogError("ATB_UIDOC_PURGE_FAIL save-failed " + ScenePath);
                return;
            }

            AssetDatabase.SaveAssets();
            Debug.Log("ATB_UIDOC_PURGE_OK " + removed);
        }
    }
}
