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

        // WEBGL SPAWN-GUARD (2026-06-07) — Resources-loadable Tree-of-Life mesh.
        // The centrepiece in the baked Village2/Village3 scene references the Tripo
        // mesh Assets/Art/Tree_Of_Life.fbx DIRECTLY by GUID. That asset is untracked
        // in git and is a Tripo FBX (isReadable:0, embedded Phong material); in the
        // WebGL build the owner saw the village load with NO tree at the centre — the
        // baked centrepiece did not resolve, so nothing renders at origin.
        // FIX: if no centrepiece tree is found near origin after the village scene
        // loads, spawn one from THIS Resources path (Resources.Load is WebGL-safe,
        // unlike a scene mesh ref / File.ReadAllText) at exactly (0,0,0), stand it
        // up, scale it to the plaza centrepiece height, then run the material fix so
        // it is never grey. Robust in every build incl. WebGL with no scene re-save.
        private const string TreeResourcePath = "Structures/tree_of_life";

        // Centrepiece sizing/orientation — mirrors Village2Generator's authored values
        // (targetTreeHeight 14 m; the Tripo tree imports lying down -> -90° X stands it
        // up). The spawn-guard re-derives upright from bounds so it is art-independent.
        private const float CentrepieceTargetHeight = 14f;
        private static readonly Vector3 TreeUprightEulerFallback = new Vector3(-90f, 0f, 0f);

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
        /// DEF-267 + WebGL spawn-guard registrar. Runs once at app start and then
        /// subscribes to <see cref="UnityEngine.SceneManagement.SceneManager.sceneLoaded"/>
        /// so the centrepiece check re-runs on EVERY scene load — the player boots into
        /// Title and navigates to Village2 LATER, after this RuntimeInitialize fires, so a
        /// one-shot check would miss the town entirely. Idempotent per load (the guard
        /// no-ops when a centrepiece already renders at origin).
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoFixOnSceneLoad()
        {
            // Re-arm on each scene load (de-dup the subscription first so domain reloads
            // / repeated init don't stack handlers).
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            // WEBGL-SAFETY (WO-331): never let the centrepiece fix throw at app start.
            try { EnsureCentrepiece(); } // also run for the scene already active at app start
            catch (System.Exception e)
            {
                Debug.LogWarning("[TreeOfLifeMaterialFixer] centrepiece fix threw at init (non-fatal): " + e);
            }
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                                          UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            // Use the just-loaded scene name so this works for BOTH single and additive
            // loads (an additively-loaded Village2 is not the "active" scene).
            // WEBGL-SAFETY (WO-331): an uncaught exception in a sceneLoaded handler halts
            // the WebGL player. Wrap EVERYTHING so a bad tree fix can never freeze the game.
            try
            {
                string n = scene.name;
                bool village = !string.IsNullOrEmpty(n) && n.StartsWith("Village");
                EnsureCentrepiece(village);
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[TreeOfLifeMaterialFixer] centrepiece fix threw (non-fatal): " + e);
            }
        }

        private static void EnsureCentrepiece(bool? villageHint = null)
        {
            bool village = villageHint ?? InVillageScene();

            // WO-311: GUARANTEE the centrepiece is the CANONICAL Tree of Life, never the
            // generic polyperfect SM_Tree_Round the Village2 generator falls back to when
            // the authored TreeOfLife prefab is absent (e.g. a fresh clone — Tree_Of_Life.fbx
            // is a gitignored Tripo asset). In the town:
            //   1. Remove any GENERIC tree sitting at origin (it would otherwise be mistaken
            //      for, and visually replace, the real centrepiece).
            //   2. Ensure exactly ONE canonical tree stands at origin (spawn from Resources
            //      if the baked one didn't resolve).
            if (village)
                RemoveGenericTreesAtOrigin();

            GameObject tree = FindCanonicalCentrepieceTree();

            // No CANONICAL tree at origin. Either the baked Tripo mesh ref failed to resolve
            // (WebGL), or the bake fell back to a generic tree we just removed. Spawn the
            // canonical Tree of Life from Resources (WebGL-safe) at exactly (0,0,0).
            if (tree == null)
            {
                if (!village) return;                   // only the town gets a centrepiece
                tree = SpawnCentrepieceFromResources();
                if (tree == null) return;               // resource missing — nothing to do
            }

            // Skip if a real instance of this component already lives on the tree (it
            // will have fixed itself via Start) — avoid double-processing.
            if (tree.GetComponentInParent<TreeOfLifeMaterialFixer>() != null) return;

            // DISPLACEMENT FIX (Tripo off-centre-pivot × large scale): whether this tree
            // was spawned from Resources OR is the existing baked centrepiece, its VISIBLE
            // mesh may be flung off (0,0,0) by an off-centre pivot — landing on a building.
            // Re-centre its COMBINED RENDERER BOUNDS at world origin on XZ and seat the
            // base at y=0 so the trunk you SEE sits at the plaza centre, on the ground.
            CenterBoundsAtOrigin(tree);

            Apply(tree);
        }

        // True when the active scene is one of the playable towns (Village2 canonical,
        // Village3, Village). Keeps the spawn-guard from dropping a tree into Title /
        // HeroSelect / DTT / dungeon scenes that this RuntimeInitialize hook also runs in.
        private static bool InVillageScene()
        {
            string n = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            return !string.IsNullOrEmpty(n) && n.StartsWith("Village");
        }

        // Instantiate the Tree-of-Life mesh from Resources at world origin, stand it
        // upright from its own bounds, scale it to the plaza centrepiece height, strip
        // colliders (the gameplay blocker is HeartController's capsule), and seat its
        // base at y=0. Resources.Load is WebGL-safe (no File I/O, no scene mesh ref).
        private static GameObject SpawnCentrepieceFromResources()
        {
            GameObject src = Resources.Load<GameObject>(TreeResourcePath);
            if (src == null)
            {
                Debug.LogWarning("[TreeOfLifeMaterialFixer] WebGL spawn-guard — no Tree-of-Life " +
                                 "at Resources/" + TreeResourcePath + "; centre plaza will be empty.");
                return null;
            }

            GameObject tree = Object.Instantiate(src);
            tree.name = "TreeOfLife(SpawnGuard)";
            tree.transform.position = Vector3.zero;
            tree.transform.rotation = Quaternion.Euler(TreeUprightEulerFallback);

            UprightFromBounds(tree, TreeUprightEulerFallback);
            ScaleToHeight(tree, CentrepieceTargetHeight);
            // Centre the VISIBLE bounds (not the transform/pivot) at origin on XZ and
            // seat the base at y=0 — an off-centre Tripo pivot otherwise flings the mesh
            // off-plaza even though transform.position == (0,0,0).
            CenterBoundsAtOrigin(tree);
            StripColliders(tree);

            Debug.Log("[TreeOfLifeMaterialFixer] WebGL spawn-guard — baked centrepiece was missing; " +
                      "spawned Tree-of-Life from Resources/" + TreeResourcePath + " at (0,0,0).");
            return tree;
        }

        // ── Spawn-guard placement helpers (bounds-derived, art-independent) ──────

        // Stand a lying FBX up by rotating its longest horizontal axis to vertical.
        // Same self-correcting logic Village2Generator uses; falls back to the supplied
        // euler when the bounds are too round to tell.
        private static void UprightFromBounds(GameObject go, Vector3 fallbackEuler)
        {
            if (go == null) return;
            if (!TryCombinedBounds(go, out Bounds b)) return;
            Vector3 s = b.size;
            float maxHoriz = Mathf.Max(s.x, s.z);
            if (maxHoriz > s.y * 1.05f)
            {
                if (s.x >= s.z) go.transform.Rotate(0f, 0f, -90f, Space.World);
                else            go.transform.Rotate(-90f, 0f, 0f, Space.World);
            }
            // else: keep the already-applied fallbackEuler.
        }

        // Uniform-scale the object so its combined renderer-bounds height equals targetH.
        private static void ScaleToHeight(GameObject go, float targetH)
        {
            if (go == null) return;
            if (!TryCombinedBounds(go, out Bounds b)) return;
            float h = b.size.y;
            if (h > 0.01f) go.transform.localScale *= (targetH / h);
        }

        // Centre the object's COMBINED RENDERER BOUNDS at world (0,0,0) on the XZ plane
        // and seat the bottom of those bounds at y=0. This is the displacement fix: a
        // Tripo mesh with an off-centre pivot × large scale renders far from its
        // transform.position, so moving the transform to origin is NOT enough — the
        // VISIBLE trunk must be the thing that lands at the plaza centre. We offset
        // transform.position by -(bounds.center.x, 0, bounds.center.z) so the bounds
        // centre moves to x=0,z=0 (keeping the height we computed), then shift Y so
        // bounds.min.y == 0 (base on the ground). Bounds are recomputed after the XZ
        // shift is conceptually irrelevant (a pure translation moves centre and min
        // together), so a single pass is exact.
        private static void CenterBoundsAtOrigin(GameObject go)
        {
            if (go == null) return;
            if (!TryCombinedBounds(go, out Bounds b)) return;
            Vector3 c = b.center;
            go.transform.position += new Vector3(-c.x, 0f, -c.z);
            // Re-seat the base at ground level (min.y unaffected by the XZ shift).
            go.transform.position += new Vector3(0f, -b.min.y, 0f);
        }

        // Decorative centrepiece — never wall off the plaza (HeartController owns the blocker).
        private static void StripColliders(GameObject go)
        {
            if (go == null) return;
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) Object.Destroy(c);
        }

        private static bool TryCombinedBounds(GameObject go, out Bounds bounds)
        {
            bounds = default;
            var rends = go.GetComponentsInChildren<Renderer>(true);
            bool have = false;
            foreach (var r in rends)
            {
                if (r == null) continue;
                if (!have) { bounds = r.bounds; have = true; }
                else bounds.Encapsulate(r.bounds);
            }
            return have;
        }

        // ---------------------------------------------------------------------
        // WO-311 — CANONICAL vs GENERIC centrepiece discrimination.
        // The real Tree of Life clones as "TreeOfLife(Clone)" (authored prefab) or
        // "TreeOfLife(SpawnGuard)" (this fixer's Resources spawn). The generic
        // polyperfect fallback the generator drops at origin clones as
        // "SM_Tree_Round(Clone)" / "SM_Tree_Oak" / "SM_Tree_Baobab". We must show the
        // FORMER at origin and delete the LATTER if it ever lands there.
        // ---------------------------------------------------------------------

        // The canonical Tree of Life only — NOT a generic SM_Tree_* polyperfect prop.
        private static bool NameIsCanonicalTree(string n)
        {
            if (string.IsNullOrEmpty(n)) return false;
            string lower = n.ToLowerInvariant();
            if (NameIsGenericTree(n)) return false;            // SM_Tree_* is never canonical
            return lower.Contains("treeoflife")
                || lower.Contains("tree_of_life")
                || lower.Contains("tree of life")
                || lower.Contains("world tree")
                || lower.Contains("life tree");
        }

        // A generic polyperfect tree prop (ring/decoration foliage), never the centrepiece.
        private static bool NameIsGenericTree(string n)
        {
            if (string.IsNullOrEmpty(n)) return false;
            string lower = n.ToLowerInvariant();
            return lower.StartsWith("sm_tree")            // SM_Tree_Round/Oak/Baobab(Clone)
                || lower.Contains("trees_a_large");       // KayKit hex decoration tree
        }

        // Remove (or disable) any GENERIC tree whose visible bounds sit inside the centre
        // plaza, so it can't be mistaken for or visually overlap the real Tree of Life.
        // Walks to the generic tree's own root and destroys the whole prop. Idempotent.
        private static void RemoveGenericTreesAtOrigin()
        {
            var all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            // Collect distinct generic roots first so destroying one doesn't invalidate the loop.
            var doomed = new System.Collections.Generic.HashSet<GameObject>();
            foreach (var r in all)
            {
                if (r == null) continue;
                string rootName = NameOfGenericTreeRoot(r.transform);
                if (rootName == null) continue;            // not part of a generic tree
                Vector3 p = r.bounds.center;
                if (new Vector2(p.x, p.z).sqrMagnitude > CentrepieceRadius * CentrepieceRadius)
                    continue;                              // generic ring tree, far from plaza — keep
                GameObject root = GenericTreeRoot(r.transform);
                if (root != null) doomed.Add(root);
            }
            foreach (var g in doomed)
            {
                if (g == null) continue;
                Debug.Log("[TreeOfLifeMaterialFixer] WO-311 — removed generic tree '" + g.name +
                          "' from the centre plaza (canonical Tree of Life owns origin).");
                Object.Destroy(g);
            }
        }

        // Highest ancestor that still reads as a generic tree (so we destroy the whole prop).
        private static GameObject GenericTreeRoot(Transform t)
        {
            Transform best = null;
            Transform cur = t;
            while (cur != null)
            {
                if (NameIsGenericTree(cur.name)) best = cur;
                cur = cur.parent;
            }
            return best != null ? best.gameObject : null;
        }

        // Name of the nearest generic-tree ancestor, or null if this transform isn't in one.
        private static string NameOfGenericTreeRoot(Transform t)
        {
            Transform cur = t;
            while (cur != null)
            {
                if (NameIsGenericTree(cur.name)) return cur.name;
                cur = cur.parent;
            }
            return null;
        }

        // The CANONICAL Tree of Life nearest world origin (inside the plaza first, then
        // anywhere). Distinct from FindCentrepieceTree, which would also accept a generic.
        private static GameObject FindCanonicalCentrepieceTree()
        {
            var all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            GameObject best = null;
            float bestDist = float.MaxValue;
            // Pass 1: canonical tree inside the centre plaza.
            foreach (var r in all)
            {
                if (r == null) continue;
                if (!NameIsCanonicalTree(NameOfTreeRoot(r.transform))) continue;
                Vector3 p = r.bounds.center;
                float d = new Vector2(p.x, p.z).sqrMagnitude;
                if (d <= CentrepieceRadius * CentrepieceRadius && d < bestDist)
                {
                    bestDist = d;
                    best = TreeRoot(r.transform);
                }
            }
            if (best != null) return best;
            // Pass 2: any canonical tree at all, nearest origin (covers a displaced pivot).
            foreach (var r in all)
            {
                if (r == null) continue;
                if (!NameIsCanonicalTree(NameOfTreeRoot(r.transform))) continue;
                Vector3 p = r.bounds.center;
                float d = new Vector2(p.x, p.z).sqrMagnitude;
                if (d < bestDist) { bestDist = d; best = TreeRoot(r.transform); }
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
