// Village2PlaceCrossing — drop a starter HeroLinkCrossing PAIR at the Village2 gate
// for the owner to nudge. crossingId='village2_gate', entry outside / destination inside,
// bidirectional. Adds the runtime component by reflection (DeNelle.Editor can't ref the
// runtime type directly — same exemption as Village2Playable). Saves the scene.
// Run: DeNelle.Editor.Village2PlaceCrossing.Run  (EDITOR CLOSED)
using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class Village2PlaceCrossing
    {
        private const string ScenePath = "Assets/Scenes/Village2.unity";
        private const string TypeName  = "DeNelle.Village.HeroLinkCrossing";

        [MenuItem("Defenders/Village2/Place Gate Crossing Pair (HeroLinkCrossing)")]
        public static void Run()
        {
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            var type = FindType(TypeName);
            if (type == null) { Debug.LogError("[V2Crossing] HeroLinkCrossing type not found (is DeNelle.Village compiled?)."); return; }

            GameObject root = null;
            foreach (var r in scene.GetRootGameObjects())
                if (r.name == "StrongholdRoot") { root = r; break; }

            Make(root, type, "Crossing_Village2Gate_Entry", new Vector3(0f, 0f, -17f));   // outside the gate (approach)
            Make(root, type, "Crossing_Village2Gate_Dest",  new Vector3(0f, 0f,  -7f));   // inside the stronghold floor

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            Debug.Log($"[V2Crossing] Placed Entry(0,0,-17) + Dest(0,0,-7), crossingId='village2_gate', bidirectional. " +
                      $"Saved={saved}. Nudge them: Entry onto the outside approach, Dest onto the inside floor.");
        }

        private static void Make(GameObject root, Type type, string name, Vector3 pos)
        {
            var existing = GameObject.Find(name);
            if (existing != null) UnityEngine.Object.DestroyImmediate(existing);   // idempotent
            var go = new GameObject(name);
            if (root != null) go.transform.SetParent(root.transform, true);
            go.transform.position = pos;
            var comp = go.AddComponent(type);
            var so = new SerializedObject(comp);
            SetStr(so, "crossingId", "village2_gate");
            SetFloat(so, "enterRadius", 2.5f);
            SetBool(so, "bidirectional", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            Debug.Log($"[V2Crossing] {name} @ {pos}.");
        }

        private static void SetStr(SerializedObject so, string f, string v)   { var p = so.FindProperty(f); if (p != null) p.stringValue = v; }
        private static void SetFloat(SerializedObject so, string f, float v)  { var p = so.FindProperty(f); if (p != null) p.floatValue = v; }
        private static void SetBool(SerializedObject so, string f, bool v)    { var p = so.FindProperty(f); if (p != null) p.boolValue = v; }

        private static Type FindType(string full)
        {
            foreach (var a in AppDomain.CurrentDomain.GetAssemblies())
            { var t = a.GetType(full); if (t != null) return t; }
            return null;
        }
    }
}
