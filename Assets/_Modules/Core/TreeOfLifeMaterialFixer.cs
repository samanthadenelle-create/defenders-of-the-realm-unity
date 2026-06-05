// =============================================================================
// TreeOfLifeMaterialFixer — DEF-267 go-live fix for the grey Tree of Life.
// -----------------------------------------------------------------------------
// The plaza centrepiece (Assets/Art/Tree_Of_Life.fbx) ships with ZERO usable
// materials: its FBX has materialImportMode=2 but no extracted/remapped material
// assets, so every renderer falls back to Unity's DEFAULT grey material and the
// tree renders flat grey in the build (the DEF-267 symptom).
//
// A ready URP/Lit material with the tree's real basecolor + normal already lives
// at Resources/Structures/Materials/TreeofLife_basecolor — it was just never
// assigned at runtime. This component loads it and paints it onto every grey /
// default / null renderer slot on the tree.
//
// Two activation paths so the LIVE (already-baked) Village2 scene is fixed WITHOUT
// a scene edit, while future generations carry the component explicitly:
//   1. [RuntimeInitializeOnLoadMethod] after scene load — scans for the tree by
//      name ("Tree_Of_Life", "TreeOfLife", "Tree of Life") and fixes it. This is
//      what rescues the current build with no .unity change required.
//   2. As a MonoBehaviour you can also AddComponent in a generator (Village2-
//      Generator) so newly generated towns fix themselves on Start().
//
// Idempotent: only ever replaces the DEFAULT/null/grey slot, never an art-authored
// material, and the runtime scan no-ops if a TreeOfLifeMaterialFixer already ran.
// =============================================================================

using UnityEngine;

namespace DeNelle.Core
{
    [DisallowMultipleComponent]
    public sealed class TreeOfLifeMaterialFixer : MonoBehaviour
    {
        // The ready URP/Lit material (basecolor + normal) under Resources.
        private const string MaterialResourcePath = "Structures/Materials/TreeofLife_basecolor";
        // Fallback basecolor texture if the .mat itself can't load for any reason.
        private const string DiffuseResourcePath = "Structures/TreeofLife_basecolor";

        // Foliage-leaning tint applied only when neither the material nor the texture
        // resolves — better a green-ish tree than a grey one. Owner can refine later.
        private static readonly Color FallbackTint = new Color(0.36f, 0.52f, 0.30f);

        // Start (not Awake) mirrors TripoMaterialFixer: lets a generator AddComponent and
        // then set anything on the next line before Run() fires.
        private void Start() => Apply(gameObject);

        /// <summary>
        /// DEF-267: after the scene loads, find the Tree of Life by name and give it a
        /// material so it doesn't render grey. Runs with no scene edit (rescues the live
        /// already-baked Village2 build). No-op when no matching object exists.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoFixOnSceneLoad()
        {
            // Only the visible decorative tree — NOT the gameplay Heart anchor (a clean
            // scale-1 child with no renderers). Match the FBX clone name and friendly aliases.
            GameObject tree = FindTree();
            if (tree == null) return;

            // Skip if a real instance of this component already lives on the tree (it will
            // have fixed itself via Start) — avoid double-processing.
            if (tree.GetComponentInParent<TreeOfLifeMaterialFixer>() != null) return;

            Apply(tree);
        }

        private static GameObject FindTree()
        {
            var all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (var r in all)
            {
                if (r == null) continue;
                // Walk up to a root whose name reads as the tree, so we fix the whole hierarchy.
                Transform t = r.transform;
                while (t != null)
                {
                    string n = t.name;
                    if (NameIsTree(n)) return t.gameObject;
                    t = t.parent;
                }
            }
            return null;
        }

        private static bool NameIsTree(string n)
        {
            if (string.IsNullOrEmpty(n)) return false;
            // "Tree_Of_Life(Clone)", "TreeOfLife", "Tree of Life", etc.
            string lower = n.ToLowerInvariant();
            return lower.Contains("tree_of_life")
                || lower.Contains("treeoflife")
                || lower.Contains("tree of life");
        }

        /// <summary>
        /// Paint a real URP material onto every grey / default / null renderer slot in the
        /// tree hierarchy. Preserves any genuinely art-authored material (never clobbers a
        /// material that already carries a basecolor texture).
        /// </summary>
        public static void Apply(GameObject treeRoot)
        {
            if (treeRoot == null) return;

            Material treeMat = Resources.Load<Material>(MaterialResourcePath);
            Texture2D diffuse = treeMat == null ? Resources.Load<Texture2D>(DiffuseResourcePath) : null;

            // If the ready material is missing, build a URP/Lit from the basecolor texture (or a
            // foliage tint as the last resort) so the tree never stays grey.
            if (treeMat == null)
            {
                Shader lit = Shader.Find("Universal Render Pipeline/Lit")
                             ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                             ?? Shader.Find("Standard");
                if (lit == null)
                {
                    Debug.LogWarning("[TreeOfLifeMaterialFixer] DEF-267 — no URP/Lit shader; cannot fix tree.");
                    return;
                }
                treeMat = new Material(lit) { name = "TreeOfLife (runtime URP)" };
                if (diffuse != null)
                {
                    if (treeMat.HasProperty("_BaseMap")) treeMat.SetTexture("_BaseMap", diffuse);
                    if (treeMat.HasProperty("_MainTex")) treeMat.SetTexture("_MainTex", diffuse);
                    if (treeMat.HasProperty("_BaseColor")) treeMat.SetColor("_BaseColor", Color.white);
                    if (treeMat.HasProperty("_Color")) treeMat.SetColor("_Color", Color.white);
                }
                else
                {
                    if (treeMat.HasProperty("_BaseColor")) treeMat.SetColor("_BaseColor", FallbackTint);
                    if (treeMat.HasProperty("_Color")) treeMat.SetColor("_Color", FallbackTint);
                }
            }

            int fixedSlots = 0;
            foreach (var r in treeRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0)
                {
                    r.sharedMaterial = treeMat;
                    fixedSlots++;
                    continue;
                }
                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!SlotNeedsFix(mats[i])) continue;
                    mats[i] = treeMat;
                    changed = true;
                    fixedSlots++;
                }
                if (changed) r.sharedMaterials = mats;
            }
            Debug.Log("[TreeOfLifeMaterialFixer] DEF-267 — fixed " + fixedSlots +
                      " grey/default slot(s) on '" + treeRoot.name + "' (material=" + treeMat.name + ").");
        }

        // A slot needs fixing if it is null, or it's Unity's built-in default material (the grey
        // one FBXs with no material fall back to), or it carries no basecolor texture at all.
        private static bool SlotNeedsFix(Material m)
        {
            if (m == null) return true;
            string sn = m.shader != null ? m.shader.name : "";
            // Built-in default / Standard with no art = grey. Always replace.
            if (sn == "Standard" || sn == "Hidden/InternalErrorShader" || string.IsNullOrEmpty(sn))
                return true;
            // Already a URP material that has a real basecolor texture = art-authored; leave it.
            Texture tex = null;
            if (m.HasProperty("_BaseMap")) tex = m.GetTexture("_BaseMap");
            if (tex == null && m.HasProperty("_MainTex")) tex = m.GetTexture("_MainTex");
            return tex == null;
        }
    }
}
