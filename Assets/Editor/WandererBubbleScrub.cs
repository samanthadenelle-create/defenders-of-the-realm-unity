// =============================================================================
// WandererBubbleScrub — re-snapshot the four WandererBubble scalars that the
// baked Healer's Cottage scene froze at their pre-fix values.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor (editor-only). Batch:
//   -executeMethod DeNelle.Editor.WandererBubbleScrub.Run
// Marker: WANDERER_BUBBLE_SCRUB_OK
//
// ⛔ WHY A SCRUB AND NOT A RE-BAKE — this is the load-bearing part, and it is a
// deliberate deviation from the suite's own advice ("RE-BAKE the dungeon").
//
// Unity deserialises a scene's serialized copy of a field OVER the code
// initialiser, so a corrected C# default has no effect in play for an object
// already baked. Dungeon_HealersCottage.unity was baked 2026-07-16; the defaults
// were corrected 2026-08-14 (8baed3014, "Bryn's bubble was oversized on every axis
// at once"). The scene is a fossil of the pre-fix numbers, and _textScale is absent
// because the field did not exist yet.
//
// A re-bake WOULD fix it. It would also regenerate 81,673 lines of a curated scene
// to correct four scalars — and re-baking this scene family is exactly what
// NUL-corrupted it before (memory: dungeon-scene-shared-tree-corruption).
//
// The reason a scrub is not a shortcut but the SEMANTICALLY IDENTICAL operation:
// DungeonSceneBuilder authors NONE of these fields. AddDungeonComponent is a bare
// go.AddComponent(type); the only SetSerialized* near the bubble is _bubbleBehaviour
// on Bryn. So the serialized block is nothing but a SNAPSHOT OF CODE DEFAULTS AT
// BAKE TIME. The control that proves it: the untouched neighbour _height reads 2.6
// in the scene and 2.6f in code — a field that never drifted. Re-snapshotting the
// four drifted scalars is the whole of what a faithful re-bake would change here.
//
// Precedent in-tree for surgical scene fixups through Unity's OWN serializer
// (never a hand-edit, which §3 forbids): CastleNavPlaneScrub, CastleWallStairsSeatFix,
// CastlePlaceCrossing.
//
// ⛔ IT READS THE CODE DEFAULTS, IT DOES NOT RE-TYPE THEM. The values are pulled off
// a fresh WandererBubble instance via SerializedObject, so this file can never
// disagree with the class the way the scene did. Hardcoding 1.8/0.7/22/0.07 here
// would just create a THIRD copy of the same numbers and a third thing to drift.
// =============================================================================

using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class WandererBubbleScrub
    {
        private const string ScenePath = "Assets/Scenes/Dungeon_HealersCottage.unity";

        /// <summary>The drifted fields, by serialized name. Values are READ from code, never typed.</summary>
        private static readonly string[] Fields = { "_panelWidth", "_panelHeight", "_wrapWidth", "_textScale" };

        [MenuItem("Defenders/Dungeons/Scrub Wanderer Bubble to code defaults")]
        public static void Run()
        {
            if (!File.Exists(ScenePath))
            {
                Debug.LogError($"WANDERER_BUBBLE_SCRUB_FAIL :: scene missing: {ScenePath}");
                EditorApplication.Exit(1);
                return;
            }

            // ---- 1. Read the CODE defaults off a throwaway instance -------------
            var probeGo = new GameObject("~WandererBubbleProbe");
            var probeType = System.Type.GetType("DeNelle.Dungeons.WandererBubble, DeNelle.Dungeons");
            if (probeType == null)
            {
                foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
                {
                    probeType = asm.GetType("DeNelle.Dungeons.WandererBubble");
                    if (probeType != null) break;
                }
            }
            if (probeType == null)
            {
                Object.DestroyImmediate(probeGo);
                Debug.LogError("WANDERER_BUBBLE_SCRUB_FAIL :: type DeNelle.Dungeons.WandererBubble not found.");
                EditorApplication.Exit(1);
                return;
            }

            var probe = probeGo.AddComponent(probeType) as MonoBehaviour;
            var probeSo = new SerializedObject(probe);
            var wanted = new Dictionary<string, SerializedProperty>();
            foreach (string f in Fields)
            {
                var p = probeSo.FindProperty(f);
                if (p == null)
                {
                    Object.DestroyImmediate(probeGo);
                    Debug.LogError($"WANDERER_BUBBLE_SCRUB_FAIL :: code has no serialized field '{f}'. " +
                                   "The class changed shape; update this scrub deliberately rather than " +
                                   "silently skipping a field.");
                    EditorApplication.Exit(1);
                    return;
                }
                wanted[f] = p;
            }

            string codeSummary = Describe(wanted);
            Debug.Log($"[WandererBubbleScrub] code defaults read from a live instance: {codeSummary}");

            // ---- 2. Apply them to every baked instance in the scene -------------
            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var bubbles = Object.FindObjectsByType(probeType, FindObjectsInactive.Include, FindObjectsSortMode.None);
            if (bubbles == null || bubbles.Length == 0)
            {
                Object.DestroyImmediate(probeGo);
                Debug.LogError($"WANDERER_BUBBLE_SCRUB_FAIL :: no WandererBubble in {ScenePath}. " +
                               "Nothing was scrubbed - do NOT report this as a pass.");
                EditorApplication.Exit(1);
                return;
            }

            int changedFields = 0;
            foreach (var obj in bubbles)
            {
                var so = new SerializedObject(obj);
                foreach (string f in Fields)
                {
                    var target = so.FindProperty(f);
                    var src = wanted[f];
                    if (target == null)
                    {
                        // The field is absent from the baked block (this is _textScale's case -
                        // the scene predates the field). FindProperty still resolves it because
                        // the TYPE has it; a null here would mean something else is wrong.
                        Debug.LogWarning($"[WandererBubbleScrub] '{f}' not resolvable on a baked instance - skipped.");
                        continue;
                    }
                    if (CopyIfDifferent(src, target)) changedFields++;
                }
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            Object.DestroyImmediate(probeGo);

            // ---- 3. Save + prove ------------------------------------------------
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            if (!saved)
            {
                Debug.LogError("WANDERER_BUBBLE_SCRUB_FAIL :: SaveScene returned false - nothing persisted.");
                EditorApplication.Exit(1);
                return;
            }

            // NUL guard: this scene family has NUL-corrupted on a bake before. Prove it did not.
            byte[] bytes = File.ReadAllBytes(ScenePath);
            bool hasNul = System.Array.IndexOf(bytes, (byte)0) >= 0;
            if (hasNul)
            {
                Debug.LogError($"WANDERER_BUBBLE_SCRUB_FAIL :: {ScenePath} contains NUL bytes after save. " +
                               "REVERT IT (git checkout) - this is the known corruption.");
                EditorApplication.Exit(1);
                return;
            }

            Debug.Log($"WANDERER_BUBBLE_SCRUB_OK {bubbles.Length} bubble(s), {changedFields} field(s) re-snapshotted " +
                      $"to {codeSummary}; scene {bytes.Length} bytes, NUL-clean.");
        }

        private static bool CopyIfDifferent(SerializedProperty src, SerializedProperty dst)
        {
            switch (src.propertyType)
            {
                case SerializedPropertyType.Float:
                    if (Mathf.Approximately(src.floatValue, dst.floatValue)) return false;
                    dst.floatValue = src.floatValue;
                    return true;
                case SerializedPropertyType.Integer:
                    if (src.intValue == dst.intValue) return false;
                    dst.intValue = src.intValue;
                    return true;
                default:
                    Debug.LogWarning($"[WandererBubbleScrub] '{src.name}' is {src.propertyType}, not a scalar - skipped.");
                    return false;
            }
        }

        private static string Describe(Dictionary<string, SerializedProperty> props)
        {
            var parts = new List<string>();
            foreach (var kv in props)
            {
                string v = kv.Value.propertyType == SerializedPropertyType.Float
                    ? kv.Value.floatValue.ToString("0.###")
                    : kv.Value.intValue.ToString();
                parts.Add($"{kv.Key}={v}");
            }
            return string.Join(" ", parts);
        }
    }
}
