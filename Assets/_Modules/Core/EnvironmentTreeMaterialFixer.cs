// =============================================================================
// EnvironmentTreeMaterialFixer — WO-332 (P0) WebGL white-tree fix.
// -----------------------------------------------------------------------------
// SYMPTOM (owner playtest 2026-06-06, WebGL build): the environment trees render
// pure WHITE — the polyperfect nature-ring trees (SM_Tree_Round / _Oak / _Baobab)
// and the KayKit hex-pack decoration trees (trees_A_large / trees_B_medium /
// tree_single_A). The Tree of Life centrepiece is handled SEPARATELY by
// TreeOfLifeMaterialFixer; this component covers EVERYTHING ELSE that reads as a
// tree, anywhere in the scene (village ring + scatter + OuterWorld foliage).
//
// ROOT CAUSE
//   White = the tree material did NOT resolve to a lit URP material at runtime:
//     • polyperfect ships built-in/Standard materials (render white/pink under URP
//       unless the editor op "Defenders/Art/Fix Polyperfect URP Materials" was run
//       before the bake — and the pack is gitignored, so a fresh bake can miss it);
//     • the KayKit hex trees rely on the FBX importer's external-material remap to
//       the shared atlas (hexagons_medieval_URP.mat). That remap does not resolve
//       reliably at runtime, and the atlas .mat is NOT under a Resources/ folder,
//       so it cannot be Resources.Load'd in the WebGL player either;
//     • either way the renderer ends up with a null / default / Standard / no-albedo
//       slot, which URP draws as flat white.
//   The WebGL texture-shrink pass (1,516 → 512 Compressed) only changes texture
//   SIZE/format on already-assigned textures — it does not null a texture or change
//   a shader, so it is NOT the cause; this fixer is shrink-agnostic.
//
// FIX (WebGL-safe, runtime, robust — no editor-only step, no Resources dependency):
//   On every scene load, scan ALL tree renderers and, for each white/grey/default/
//   Standard/no-albedo slot, replace it with a real lit URP material chosen in order:
//     (a) borrow a GOOD (URP, textured) material another tree renderer in the scene
//         already uses — so the whole forest matches with zero asset deps;
//     (b) URP-convert the slot's OWN built-in material in place, carrying its texture
//         + colour (same conversion PolyperfectUrpFix does, but at runtime);
//     (c) a green foliage / brown trunk tint as the last resort — better a green tree
//         than a white one.
//   Resources.Load / Shader.Find / new Material are all WebGL-safe (no File I/O, no
//   scene-mesh-ref dependency). Idempotent: only touches white/grey slots, never an
//   art-authored (already-textured-URP) material.
//
// WEBGL-SAFETY (WO-331): an uncaught exception in a sceneLoaded handler HALTS the
// WebGL player. Every entry point below is wrapped in try/catch so a bad tree fix
// can never freeze the game.
//
// SCOPE NOTE: this file deliberately does NOT touch the Tree-of-Life centrepiece
// placement/spawn logic (owned by TreeOfLifeMaterialFixer). It only recolours
// material slots, and it SKIPS the canonical centrepiece (that fixer owns it).
// =============================================================================

using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core
{
    public static class EnvironmentTreeMaterialFixer
    {
        // Green foliage tint + brown trunk tint, applied only when nothing better
        // resolves. Heuristic: a renderer whose name reads "trunk/bark/wood/stem"
        // gets the trunk tint, everything else the foliage tint.
        private static readonly Color FoliageTint = new Color(0.30f, 0.50f, 0.26f);
        private static readonly Color TrunkTint   = new Color(0.42f, 0.30f, 0.18f);

        /// <summary>
        /// Registrar. Runs once at app start, then re-runs on EVERY scene load — the
        /// player boots into Title and reaches the village/world LATER, so a one-shot
        /// check would miss the foliage entirely. Idempotent per load.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Register()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
            // Also fix the scene already active at app start.
            SafeFixAllTrees();
        }

        private static void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene,
                                          UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            SafeFixAllTrees();
        }

        // WO-331: never let a tree fix throw out of a sceneLoaded handler (would halt WebGL).
        // U/T: route the catch through FlowTrace.Fail so a thrown tree fix lands in the break-log
        // (a swallowed exception here would otherwise silently leave white/magenta trees).
        private static void SafeFixAllTrees()
        {
            try { FixAllTrees(); }
            catch (System.Exception e)
            {
                FlowTrace.Fail("EnvTreeFix",
                    "tree fix threw (non-fatal, trees may stay white/grey): " +
                    e.GetType().Name + ": " + e.Message + "\n" + e.StackTrace);
            }
        }

        /// <summary>
        /// Find every tree renderer in the active scene(s) and repair any white/grey/
        /// default/Standard/no-albedo material slot. Public so a generator or test can
        /// call it directly.
        /// </summary>
        public static void FixAllTrees()
        {
            using var _t = FlowTrace.Enter("EnvTreeFix", "FixAllTrees");

            var all = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
            if (all == null || all.Length == 0) return;

            // First pass: find ONE good tree material to share across white trees so the
            // whole forest matches (cheapest + best-looking fix, zero asset deps).
            Material sharedGood = FindGoodTreeMaterial(all);

            // V: counters captured for the post-pass URP verify + the summary roll-up.
            int fixedSlots = 0;
            int treeRenderers = 0;
            int viaSibling = 0, viaUrpConvert = 0, viaTint = 0;
            var touched = new System.Collections.Generic.List<Renderer>();

            // G: guard EACH tree renderer independently — one bad tree (e.g. a destroyed/odd
            // renderer or a shader op that throws) logs via [Flow:EnvTreeFix] -> break-log and is
            // skipped, never aborting the rest of the forest (the whole point of TryEach here).
            Guard.TryEach("EnvTreeFix", "fix tree renderer", all, r =>
            {
                if (r == null) return;
                if (!IsTreeRenderer(r.transform)) return;
                // The canonical Tree of Life is owned by TreeOfLifeMaterialFixer — skip it
                // so the two fixers never fight over the centrepiece.
                if (IsCanonicalCentrepiece(r.transform)) return;
                treeRenderers++;

                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0)
                {
                    var m = sharedGood ?? TintMatFor(r.name);
                    r.sharedMaterial = m;
                    fixedSlots++;
                    touched.Add(r);
                    if (sharedGood != null) viaSibling++; else viaTint++;
                    return;
                }

                bool changed = false;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (!SlotNeedsFix(mats[i])) continue;

                    Material repl;
                    if (sharedGood != null)            { repl = sharedGood;          viaSibling++; }
                    else if (mats[i] != null)          { repl = UrpConvert(mats[i]); viaUrpConvert++; }
                    else                               { repl = TintMatFor(r.name);  viaTint++; }

                    mats[i] = repl;
                    changed = true;
                    fixedSlots++;
                }
                if (changed) { r.sharedMaterials = mats; touched.Add(r); }
            });

            // V: VERIFY every tree slot we touched ended on a URP shader (not Standard/Legacy/error)
            // and is no longer a flat-white untextured slot — otherwise the repair silently failed.
            VerifyTreesFixed(touched);

            if (fixedSlots > 0)
                FlowTrace.Step("EnvTreeFix", "WO-332 — fixed " + fixedSlots +
                          " white/grey tree slot(s) across " + treeRenderers + " tree renderer(s) " +
                          "(sibling=" + viaSibling + ", urp-convert=" + viaUrpConvert +
                          ", tint=" + viaTint + "). (Full pack fix: Defenders/Art/Fix Polyperfect URP Materials.)");
            else if (treeRenderers > 0)
                FlowTrace.Step("EnvTreeFix", "WO-332 — scanned " + treeRenderers +
                          " tree renderer(s); all already had good URP materials (no fix needed).");
        }

        // V (TGVRU): after the repair pass, assert every tree slot we replaced is now on a URP
        // shader AND no longer reads as a flat-white untextured surface (the WO-332 symptom). A
        // slot still failing SlotNeedsFix() means the chosen replacement (sibling/convert/tint)
        // did not actually resolve to a good material — FlowTrace.Fail so the run self-reports a
        // still-white tree instead of leaving the owner to spot it. The tint fallback always lands
        // (R: never silently white), so a fail here flags a real shader/material resolution gap.
        private static void VerifyTreesFixed(System.Collections.Generic.List<Renderer> touched)
        {
            if (touched == null || touched.Count == 0) return;
            int slots = 0, stillBad = 0;
            foreach (var r in touched)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null) continue;
                for (int i = 0; i < mats.Length; i++)
                {
                    slots++;
                    if (!SlotNeedsFix(mats[i])) continue;   // good URP, textured-or-tinted — pass
                    stillBad++;
                    string sn = (mats[i] != null && mats[i].shader != null) ? mats[i].shader.name : "<null>";
                    FlowTrace.Fail("EnvTreeFix",
                        "VERIFY FAILED on tree renderer '" + r.name + "' slot " + i + ": shader='" + sn +
                        "' still reads white/grey/non-URP after the fix — replacement material did not resolve " +
                        "(URP/Lit shader missing in build?). Tree may still render white.");
                }
            }
            if (stillBad == 0)
                FlowTrace.Step("EnvTreeFix",
                    "VERIFY OK — all " + slots + " repaired tree slot(s) now on a good URP material (no white/grey).");
        }

        // A good (URP, textured, non-default) material some tree in the scene already
        // uses — borrow it so white trees match the rest of the foliage with no deps.
        private static Material FindGoodTreeMaterial(Renderer[] all)
        {
            foreach (var r in all)
            {
                if (r == null) continue;
                if (!IsTreeRenderer(r.transform)) continue;
                var mats = r.sharedMaterials;
                if (mats == null) continue;
                foreach (var m in mats)
                    if (m != null && !SlotNeedsFix(m)) return m;   // good, textured, URP
            }
            return null;
        }

        // ── Tree identification ──────────────────────────────────────────────────
        // Matches the polyperfect ring trees, KayKit hex trees, and any "*tree*" prop.
        private static bool IsTreeRenderer(Transform t)
        {
            // EXCLUSION FIRST: the hero's equipment props (weapon / shield-offhand / bow)
            // are NOT trees. They can name-collide with the foliage matcher (e.g. a "branch"
            // token, or a tree-named parent bone they're parented under), which made this
            // tree fixer collect them and overwrite their good material to flat white. If
            // ANY transform in the parent chain reads as an equipment prop, it is never a
            // tree — bail out before the tree-name walk so the fix/verify never touch it.
            Transform e = t;
            while (e != null)
            {
                if (IsEquipmentProp(e.name)) return false;
                e = e.parent;
            }

            Transform cur = t;
            while (cur != null)
            {
                if (NameIsTree(cur.name)) return true;
                cur = cur.parent;
            }
            return false;
        }

        // Hero equipment props (weapon, off-hand shield, bow) — these must NEVER be treated
        // as trees nor have their authored material mutated by the tree fixer.
        private static bool IsEquipmentProp(string n)
        {
            if (string.IsNullOrEmpty(n)) return false;
            string lower = n.ToLowerInvariant();
            return lower.Contains("equipmentprop")
                || lower.Contains("bowprop")
                || lower.Contains("weaponprop")
                || lower.Contains("_weapon")
                || lower.Contains("offhand")
                || lower.Contains("shield");
        }

        private static bool NameIsTree(string n)
        {
            if (string.IsNullOrEmpty(n)) return false;
            string lower = n.ToLowerInvariant();
            // Generic tree/foliage words first…
            if (lower.Contains("tree")             // SM_Tree_*, trees_*, tree_single_*, TreeOfLife
                || lower.Contains("foliage")
                || lower.Contains("bush")
                || lower.Contains("shrub")
                || lower.Contains("plant")
                || lower.Contains("leaf")
                || lower.Contains("leaves")
                || lower.Contains("canopy"))
                return true;
            // …then the concrete polyperfect / KayKit species names. Polyperfect ships
            // trees whose CHILD mesh objects are often named by species only ("Beech",
            // "Conifer", …) with no "tree" token, so the parent-walk would miss the leaf
            // renderer's own name; match the species directly so every slot is covered.
            return lower.Contains("beech")  || lower.Contains("oak")    || lower.Contains("birch")
                || lower.Contains("maple")  || lower.Contains("conifer")|| lower.Contains("poplar")
                || lower.Contains("lime")   || lower.Contains("baobab") || lower.Contains("willow")
                || lower.Contains("pine")   || lower.Contains("palm")   || lower.Contains("spruce")
                || lower.Contains("cedar")  || lower.Contains("aspen");
        }

        // The canonical Tree of Life — owned by TreeOfLifeMaterialFixer. We skip it.
        private static bool IsCanonicalCentrepiece(Transform t)
        {
            Transform cur = t;
            while (cur != null)
            {
                string lower = cur.name.ToLowerInvariant();
                if (lower.Contains("treeoflife")
                    || lower.Contains("tree_of_life")
                    || lower.Contains("tree of life")
                    || lower.Contains("world tree")
                    || lower.Contains("life tree"))
                    return true;
                cur = cur.parent;
            }
            return false;
        }

        // ── Material repair (WebGL-safe) ──────────────────────────────────────────

        // A slot needs fixing if it is null, on a built-in/non-URP/error/legacy shader
        // (the white/grey fallback), OR resolves to a flat WHITE surface (the WO-332
        // symptom): no basecolor texture AND a white/near-white base colour. Matching on
        // the SHADER + RENDERED COLOUR (not just the object name) is the robustness the
        // WO asks for — a polyperfect tree baked without the editor URP-convert lands on
        // shader "Standard" (caught here) regardless of what its GameObject is named.
        private static bool SlotNeedsFix(Material m)
        {
            if (m == null) return true;
            string sn = m.shader != null ? m.shader.name : "";
            // Anything that is NOT a URP lit/unlit shader is a white/pink/grey risk under
            // URP: Standard, Legacy, the internal error shader, a stripped/empty shader,
            // or any leftover built-in surface shader.
            bool isUrp = sn.StartsWith("Universal Render Pipeline/")
                      || sn.StartsWith("URP/");
            if (!isUrp
                || sn == "Standard"
                || sn == "Standard (Specular setup)"
                || sn.StartsWith("Legacy Shaders/")
                || sn.Contains("InternalErrorShader")
                || string.IsNullOrEmpty(sn))
                return true;

            // URP shader: art-authored only if it has a real basecolor texture OR a
            // non-white tint. A URP/Lit slot with NO texture and a white base colour
            // renders as the flat-white tree the owner saw → fix it.
            Texture tex = null;
            if (m.HasProperty("_BaseMap")) tex = m.GetTexture("_BaseMap");
            if (tex == null && m.HasProperty("_MainTex")) tex = m.GetTexture("_MainTex");
            if (tex != null) return false;                 // textured URP = keep

            Color c = Color.white;
            if (m.HasProperty("_BaseColor")) c = m.GetColor("_BaseColor");
            else if (m.HasProperty("_Color")) c = m.GetColor("_Color");
            // Near-white untextured = the white-tree symptom → needs a tint.
            return c.r > 0.85f && c.g > 0.85f && c.b > 0.85f;
        }

        // Rebuild a built-in/grey material as URP/Lit carrying its texture + colour.
        private static Material UrpConvert(Material src)
        {
            Texture tex = null;
            if (src.HasProperty("_MainTex")) tex = src.GetTexture("_MainTex");
            if (tex == null && src.HasProperty("_BaseMap")) tex = src.GetTexture("_BaseMap");
            Color col = src.HasProperty("_Color") ? src.GetColor("_Color")
                      : src.HasProperty("_BaseColor") ? src.GetColor("_BaseColor")
                      : Color.white;
            return tex != null ? BuildLit(tex, col) : TintMat(FoliageTint);
        }

        private static Material BuildLit(Texture tex, Color col)
        {
            Shader lit = LitShader();
            if (lit == null) return TintMat(FoliageTint);
            var m = new Material(lit) { name = "EnvTree (runtime URP)" };
            if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
            if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Color")) m.SetColor("_Color", col);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            return m;
        }

        // Pick a trunk vs foliage tint from the renderer's name.
        private static Material TintMatFor(string rendererName)
        {
            string lower = string.IsNullOrEmpty(rendererName) ? "" : rendererName.ToLowerInvariant();
            bool trunk = lower.Contains("trunk") || lower.Contains("bark")
                      || lower.Contains("wood")  || lower.Contains("stem")
                      || lower.Contains("branch");
            return TintMat(trunk ? TrunkTint : FoliageTint);
        }

        private static Material TintMat(Color tint)
        {
            Shader lit = LitShader();
            if (lit == null)
            {
                // T/V: no URP/Lit (or even Standard) shader resolves — the LAST-RESORT tint can't be
                // built, so this tree stays white/magenta. Hard fail to the break-log (the fixer's
                // own shader is missing = every tree it touches is broken).
                FlowTrace.Fail("EnvTreeFix",
                    "no URP/Lit (or Standard) shader found — cannot build the tint fallback; tree stays white/magenta. " +
                    "URP pipeline asset missing or shaders stripped from the build.");
                return null;
            }
            var m = new Material(lit) { name = "EnvTree (runtime tint)" };
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", tint);
            if (m.HasProperty("_Color")) m.SetColor("_Color", tint);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.1f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0f);
            return m;
        }

        private static Shader LitShader()
        {
            return Shader.Find("Universal Render Pipeline/Lit")
                ?? Shader.Find("Universal Render Pipeline/Simple Lit")
                ?? Shader.Find("Standard");
        }
    }
}
