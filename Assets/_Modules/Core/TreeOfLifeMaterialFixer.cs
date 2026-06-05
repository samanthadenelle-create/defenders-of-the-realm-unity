// =============================================================================
// TreeOfLifeMaterialFixer — DEF-267 go-live fix for the grey Tree of Life.
// -----------------------------------------------------------------------------
// SYMPTOM (owner screenshot): the village centrepiece "Tree of Life" renders as a
// bare GREY untextured trunk in the playable Village2 scene.
//
// ROOT CAUSE (corrected 2026-06-04): the original DEF-267 fixer was built on a
// false premise. It searched for an object literally named "Tree_Of_Life" /
// "TreeOfLife" and tried to load a material at
// "Resources/Structures/Materials/TreeofLife_basecolor". On THIS branch NONE of
// those exist:
//   • there is no Tree_Of_Life.fbx and no TreeofLife_basecolor material/texture
//     (they belong to a different repo lineage);
//   • the real centrepiece is a polyperfect tree — Village2Generator places the
//     `treeOfLife` prefab (SM_Tree_Round) at world origin and scales it to ~14 m.
//     Its scene clone is named "SM_Tree_Round(Clone)", NOT "TreeOfLife".
// So the old FindTree() always returned null and the fixer was a silent no-op —
// the tree stayed grey.
//
// Polyperfect ships built-in/Standard materials that render grey/pink under URP
// (see PolyperfectUrpFix). The CANONICAL full fix is the editor op
// "Defenders/Art/Fix Polyperfect URP Materials" (converts ALL pack materials to
// URP/Lit in place). This runtime component is the no-scene-edit safety net for
// the already-baked Village2 build and for freshly generated towns.
//
// WHAT THIS NOW DOES (asset-independent, so it always lands):
//   1. Finds the centrepiece by POSITION + name — the tree-ish renderer nearest
//      world origin (covers "SM_Tree_*", "TreeOfLife", "Tree of Life", etc.),
//      never the gameplay Heart anchor (it has no renderer).
//   2. For each grey / default / built-in / null slot on that tree it tries, in
//      order: (a) borrow a good material already used by a SIBLING tree renderer
//      in the scene (so the centrepiece matches the rest of the foliage with zero
//      asset deps); (b) if every tree is grey, URP-convert the tree's OWN material
//      in place, carrying its texture (same logic as PolyperfectUrpFix); (c) an
//      optional Resources override material if one happens to exist; (d) a foliage
//      tint as the last resort. Never grey.
//
// Two activation paths:
//   • [RuntimeInitializeOnLoadMethod] after scene load — rescues the live baked
//     Village2 build with no .unity change.
//   • As a MonoBehaviour AddComponent'd by Village2Generator — newly generated
//     towns fix their tree on Start().
//
// Idempotent: only replaces grey/default/null slots, never an art-authored
// (already-textured-URP) material.
// =============================================================================

using UnityEngine;

namespace DeNelle.Core
{
    [DisallowMultipleComponent]
    public sealed class TreeOfLifeMaterialFixer : MonoBehaviour
    {
        // OPTIONAL override material under Resources. Does not exist on this branch;
        // kept so a future authored Tree-of-Life material can drop in by path with
        // no code change. The fixer no longer DEPENDS on it.
        private const string MaterialResourcePath = "Structures/Materials/TreeofLife_basecolor";
        private const string DiffuseResourcePath = "Structures/TreeofLife_basecolor";

        // Foliage-leaning tint, applied only when nothing better resolves — better a
        // green-ish tree than a grey one. Owner can refine later.
        private static readonly Color FallbackTint = new Color(0.36f, 0.52f, 0.30f);

        // How close to world origin (XZ) a tree renderer must sit to count as the
        // centrepiece. The plaza is ~13 m clear; ring trees start ~18 m out.
        private const float CentrepieceRadius = 8f;

        // Start (not Awake) mirrors TripoMaterialFixer: lets the generator AddComponent
        // and set anything on the next line before Apply() fires.
        private void Start() => Apply(gameObject);

        /// <summary>
        /// DEF-267: after the scene loads, find the centrepiece tree and give it a
        /// material so it doesn't render grey. No scene edit (rescues the live baked
        /// Village2 build). No-op when no tree exists.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoFixOnSceneLoad()
        {
            GameObject tree = FindCentrepieceTree();
            if (tree == null) return;

            // Skip if a real instance of this component already lives on the tree (it
            // will have fixed itself via Start) — avoid double-processing.
            if (tree.GetComponentInParent<TreeOfLifeMaterialFixer>() != null) return;

            Apply(tree);
        }

        // ---------------------------------------------------------------------
        // FIND: the centrepiece is the tree-ish renderer nearest world origin.
        // ---------------------------------------------------------------------
        private static GameObject FindCentrepieceTree()
        {
            var all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);

            // Pass 1: a tree-named renderer inside the centre plaza (best signal —
            // both the prefab name "SM_Tree_Round" and friendly "TreeOfLife" match).
            GameObject best = null;
            float bestDist = float.MaxValue;
            foreach (var r in all)
            {
                if (r == null) continue;
                if (!NameIsTree(NameOfTreeRoot(r.transform))) continue;
                Vector3 p = r.bounds.center;
                float d = new Vector2(p.x, p.z).sqrMagnitude;
                if (d <= CentrepieceRadius * CentrepieceRadius && d < bestDist)
                {
                    bestDist = d;
                    best = TreeRoot(r.transform);
                }
            }
            if (best != null) return best;

            // Pass 2 (fallback): any tree-named renderer at all, nearest origin.
            foreach (var r in all)
            {
                if (r == null) continue;
                if (!NameIsTree(NameOfTreeRoot(r.transform))) continue;
                Vector3 p = r.bounds.center;
                float d = new Vector2(p.x, p.z).sqrMagnitude;
                if (d < bestDist)
                {
                    bestDist = d;
                    best = TreeRoot(r.transform);
                }
            }
            return best;
        }

        // Walk up to the highest ancestor that still reads as a tree, so we fix the
        // whole tree hierarchy (returns the renderer's own object if no tree ancestor).
        private static GameObject TreeRoot(Transform t)
        {
            Transform best = null;
            Transform cur = t;
            while (cur != null)
            {
                if (NameIsTree(cur.name)) best = cur;
                cur = cur.parent;
            }
            return best != null ? best.gameObject : t.gameObject;
        }

        private static string NameOfTreeRoot(Transform t)
        {
            Transform cur = t;
            while (cur != null)
            {
                if (NameIsTree(cur.name)) return cur.name;
                cur = cur.parent;
            }
            return t.name;
        }

        private static bool NameIsTree(string n)
        {
            if (string.IsNullOrEmpty(n)) return false;
            string lower = n.ToLowerInvariant();
            // Covers "SM_Tree_Round(Clone)", "Tree_Of_Life(Clone)", "TreeOfLife",
            // "Tree of Life", and any other "*tree*" centrepiece prefab.
            return lower.Contains("tree")
                || lower.Contains("treeoflife")
                || lower.Contains("tree of life")
                || lower.Contains("world tree")
                || lower.Contains("life tree");
        }

        /// <summary>
        /// Paint a real (non-grey) material onto every grey / default / null renderer
        /// slot in the tree hierarchy. Preserves any genuinely art-authored material.
        /// </summary>
        public static void Apply(GameObject treeRoot)
        {
            if (treeRoot == null) return;

            // Optional authored override (does not exist on this branch — null is fine).
            Material overrideMat = Resources.Load<Material>(MaterialResourcePath);
            if (overrideMat == null)
            {
                Texture2D diffuse = Resources.Load<Texture2D>(DiffuseResourcePath);
                if (diffuse != null) overrideMat = BuildLit(diffuse, Color.white);
            }

            // Best in-scene match: a good (non-grey, textured) material another tree
            // is already using — makes the centrepiece blend with the rest of the wood.
            Material siblingMat = overrideMat == null ? FindSiblingTreeMaterial(treeRoot) : null;

            int fixedSlots = 0;
            string source = "none";
            foreach (var r in treeRoot.GetComponentsInChildren<Renderer>(true))
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;

                if (mats == null || mats.Length == 0)
                {
                    Material m = overrideMat ?? siblingMat ?? FoliageMat();
                    r.sharedMaterial = m;
                    fixedSlots++;
                    source = overrideMat != null ? "override" : (siblingMat != null ? "sibling" : "tint");
                    continue;
                }

                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!SlotNeedsFix(mats[i])) continue;

                    Material repl;
                    if (overrideMat != null) { repl = overrideMat; source = "override"; }
                    else if (siblingMat != null) { repl = siblingMat; source = "sibling"; }
                    else if (mats[i] != null) { repl = UrpConvert(mats[i]); source = "urp-convert"; }
                    else { repl = FoliageMat(); source = "tint"; }

                    mats[i] = repl;
                    changed = true;
                    fixedSlots++;
                }
                if (changed) r.sharedMaterials = mats;
            }

            Debug.Log("[TreeOfLifeMaterialFixer] DEF-267 — fixed " + fixedSlots +
                      " grey/default slot(s) on '" + treeRoot.name + "' via " + source +
                      ". (Full pack fix: Defenders/Art/Fix Polyperfect URP Materials.)");
        }

        // A non-grey, textured material another tree in the scene already uses.
        private static Material FindSiblingTreeMaterial(GameObject exclude)
        {
            var all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            foreach (var r in all)
            {
                if (r == null) continue;
                if (exclude != null && r.transform.IsChildOf(exclude.transform)) continue;
                if (!NameIsTree(NameOfTreeRoot(r.transform))) continue;
                foreach (var m in r.sharedMaterials)
                {
                    if (m != null && !SlotNeedsFix(m)) return m;  // good, textured, URP
                }
            }
            return null;
        }

        // Rebuild a built-in/grey material as URP/Lit carrying its texture+colour
        // (same conversion PolyperfectUrpFix does at edit-time, but at runtime).
        private static Material UrpConvert(Material src)
        {
            Texture tex = null;
            if (src.HasProperty("_MainTex")) tex = src.GetTexture("_MainTex");
            if (tex == null && src.HasProperty("_BaseMap")) tex = src.GetTexture("_BaseMap");
            Color col = src.HasProperty("_Color") ? src.GetColor("_Color")
                      : src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor")
                      : Color.white;
            return tex != null ? BuildLit(tex, col) : FoliageMat();
        }

        private static Material BuildLit(Texture tex, Color col)
        {
            Shader lit = LitShader();
            if (lit == null) return FoliageMat();
            var m = new Material(lit) { name = "TreeOfLife (runtime URP)" };
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Color")) m.SetColor("_Color", col);
            return m;
        }

        private static Material FoliageMat()
        {
            Shader lit = LitShader();
            if (lit == null)
            {
                Debug.LogWarning("[TreeOfLifeMaterialFixer] DEF-267 — no URP/Lit shader; cannot fix tree.");
                return null;
            }
            var m = new Material(lit) { name = "TreeOfLife (runtime foliage)" };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", FallbackTint);
            if (m.HasProperty("_Color")) m.SetColor("_Color", FallbackTint);
            return m;
        }

        private static Shader LitShader()
        {
            return Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                ?? Shader.Find("Standard");
        }

        // A slot needs fixing if it is null, on Unity's built-in default/error/legacy
        // shader (the grey fallback), or carries no basecolor texture at all.
        private static bool SlotNeedsFix(Material m)
        {
            if (m == null) return true;
            string sn = m.shader != null ? m.shader.name : "";
            if (sn == "Standard"
                || sn == "Standard (Specular setup)"
                || sn.StartsWith("Legacy Shaders/")
                || sn.Contains("InternalErrorShader")
                || string.IsNullOrEmpty(sn))
                return true;
            // Already a URP material with a real basecolor texture = art-authored; keep it.
            Texture tex = null;
            if (m.HasProperty("_BaseMap")) tex = m.GetTexture("_BaseMap");
            if (tex == null && m.HasProperty("_MainTex")) tex = m.GetTexture("_MainTex");
            return tex == null;
        }
    }
}
