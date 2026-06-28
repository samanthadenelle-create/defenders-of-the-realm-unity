// CastlePlaceCrossing — drop a HeroLinkCrossing PAIR for the castle<->OuterWorld seam
// (migrate the legacy slide to the owner's paired-warp). Castle-side marker goes in
// MainCastle_Hall, its partner in OuterWorld, both crossingId='castle_outerworld', at
// the known seam endpoints (SeamCastleEnd / SeamOuterWorldEnd). Owner nudges. Adds the
// runtime component by reflection (editor exemption). Saves both scenes.
// Run: DeNelle.Editor.CastlePlaceCrossing.Run  (EDITOR CLOSED)
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class CastlePlaceCrossing
    {
        private const string CastleScene = "Assets/Scenes/MainCastle_Hall.unity";
        private const string OuterScene  = "Assets/Scenes/OuterWorld.unity";
        private const string TypeName    = "DeNelle.Village.HeroLinkCrossing";

        [MenuItem("Defenders/Castle/Place Castle<->OuterWorld Crossing Pair")]
        public static void Run()
        {
            var type = FindType(TypeName);
            if (type == null) { Debug.LogError("[CastleCrossing] HeroLinkCrossing type not found."); return; }

            // Castle side (in MainCastle_Hall) at the known seam castle endpoint.
            var s1 = EditorSceneManager.OpenScene(CastleScene, OpenSceneMode.Single);
            Make(type, "Crossing_CastleSeam_Castle", new Vector3(-4.37f, 0f, -63f));
            EditorSceneManager.MarkSceneDirty(s1);
            EditorSceneManager.SaveScene(s1, CastleScene);
            Debug.Log("[CastleCrossing] castle-side marker placed in MainCastle_Hall @ (-4.37,0,-63).");

            // OuterWorld side at the known seam outer endpoint.
            var s2 = EditorSceneManager.OpenScene(OuterScene, OpenSceneMode.Single);
            Make(type, "Crossing_CastleSeam_Outer", new Vector3(-4.37f, 0f, -76f));
            EditorSceneManager.MarkSceneDirty(s2);
            EditorSceneManager.SaveScene(s2, OuterScene);
            Debug.Log("[CastleCrossing] outerworld-side marker placed in OuterWorld @ (-4.37,0,-76).");

            Debug.Log("[CastleCrossing] DONE. crossingId='castle_outerworld', bidirectional. Both scenes load additively " +
                      "so the registry pairs them. Nudge each onto the walkable seam; once it crosses, the slide can be removed.");
        }

        private static void Make(Type type, string name, Vector3 pos)
        {
            var existing = GameObject.Find(name);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);
            var go = new GameObject(name);
            go.transform.position = pos;
            var comp = go.AddComponent(type);
            var so = new SerializedObject(comp);
            var p1 = so.FindProperty("crossingId");   if (p1 != null) p1.stringValue = "castle_outerworld";
            var p2 = so.FindProperty("enterRadius");  if (p2 != null) p2.floatValue = 2.5f;
            var p3 = so.FindProperty("bidirectional"); if (p3 != null) p3.boolValue = true;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Type FindType(string full)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            { var t = a.GetType(full); if (t != null) return t; }
            return null;
        }
    }
}
