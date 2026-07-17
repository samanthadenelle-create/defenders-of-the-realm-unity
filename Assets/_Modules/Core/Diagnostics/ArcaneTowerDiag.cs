// =============================================================================
// ArcaneTowerDiag - DATA-first disambiguator for the arcane tower rendering WHITE
// on the tester device (CLAUDE.md section 12: instrument, do NOT guess).
// -----------------------------------------------------------------------------
// TWO competing hypotheses this NAMES from captured data instead of reasoning:
//   (a) MagentaGuard recovered the BAKED polyperfect LPUP renderers (M_*_LPUP) to
//       URP/Lit but the colour did not STICK / read as white in the built player,
//       and those baked renderers are what is VISIBLE (the re-skin never hid them).
//   (b) HubStructureVisualInjector RE-SKINNED the tower to Resources/Structures/
//       "arcane tower.fbx" (a Tripo model) and ITS material/forced-texture is the
//       white surface - not MagentaGuard at all.
//
// The disambiguator is WHICH renderer is VISIBLE (enabled + activeInHierarchy) and
// WHAT material sits on it. So ~2.5s after Main_Castle_Overworld loads (steady state,
// AFTER MagentaGuard's sweep AND after the injector's re-skin + TripoMaterialFixer's
// next-frame Start()), this walks EVERY renderer under any GameObject named
// "ArcaneTower_MagicUpgrades" and logs, per material slot, via FlowTrace.Fail (so it
// lands in the errors-only break-log.jsonl the device pulls): renderer path, enabled,
// activeInHierarchy, material name, shader.name, _BaseColor, _Color, _BaseMap!=null,
// _MainTex!=null.
//
// READ THE CAPTURE LIKE THIS:
//   - An ENABLED renderer whose material name is "M_*_LPUP*" (or "*_MagentaFix") and
//     shader "Universal Render Pipeline/Lit" with baseColor ~white + baseMap=False
//     => hypothesis (a): the baked LPUP tower is visible and recovered to white.
//   - An ENABLED renderer whose material name is NOT an LPUP swatch (a Tripo/embedded
//     name, under a "LightSkin_..." parent) => hypothesis (b): the re-skin is visible;
//     inspect its baseColor / baseMap to see why it reads white.
//
// Gated: only the target scene, only the target object, one-shot per scene load, cheap.
// Self-contained; remove this file once the tower is confirmed coloured on device.
// =============================================================================
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Core.Diagnostics
{
    public static class ArcaneTowerDiag
    {
        private const string TargetScene  = "Main_Castle_Overworld";
        private const string TargetObject = "ArcaneTower_MagicUpgrades";
        private static GameObject _host;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            if (_host == null)
            {
                _host = new GameObject("[ArcaneTowerDiag]");
                Object.DontDestroyOnLoad(_host);
                _host.AddComponent<Runner>();
            }
        }

        private sealed class Runner : MonoBehaviour
        {
            private void OnEnable()  { SceneManager.sceneLoaded += OnLoaded; }
            private void OnDisable() { SceneManager.sceneLoaded -= OnLoaded; }

            private void Start()
            {
                if (SceneManager.GetActiveScene().name == TargetScene) StartCoroutine(DumpAfter(2.5f));
            }

            private void OnLoaded(Scene s, LoadSceneMode m)
            {
                if (s.name == TargetScene) StartCoroutine(DumpAfter(2.5f));
            }

            // Let MagentaGuard's sceneLoaded sweep AND the injector re-skin (+ TripoMaterialFixer's
            // next-frame Start rebuild) all settle, so we capture the true STEADY-STATE visible tower.
            private IEnumerator DumpAfter(float delay)
            {
                yield return new WaitForSeconds(delay);
                Dump();
            }
        }

        private static void Dump()
        {
            var roots = new System.Collections.Generic.List<Transform>();
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (t != null && t.name == TargetObject) roots.Add(t);

            if (roots.Count == 0)
            {
                FlowTrace.Fail("ArcaneDiag",
                    "no GameObject named '" + TargetObject + "' found in scene '" + TargetScene +
                    "' at steady state (structure absent/renamed/deactivated) - cannot classify white-tower cause.");
                return;
            }

            FlowTrace.Fail("ArcaneDiag",
                "===== ARCANE TOWER RENDER DUMP (" + TargetScene + ", post-MagentaGuard + post-reskin) roots=" + roots.Count + " =====");

            int ri = 0;
            foreach (var root in roots)
            {
                var rends = root.GetComponentsInChildren<Renderer>(true);
                FlowTrace.Fail("ArcaneDiag",
                    "ROOT[" + (ri++) + "] '" + Path(root) + "' activeSelf=" + root.gameObject.activeSelf +
                    " renderers=" + (rends != null ? rends.Length : 0));
                if (rends == null) continue;

                int si = 0;
                foreach (var r in rends)
                {
                    if (r == null) continue;
                    bool visible = r.enabled && r.gameObject.activeInHierarchy;
                    var mats = r.sharedMaterials;
                    int slots = mats != null ? mats.Length : 0;
                    for (int i = 0; i < slots; i++)
                    {
                        var m = mats[i];
                        string mn  = m != null ? m.name : "<null-mat>";
                        string sh  = (m != null && m.shader != null) ? m.shader.name : "<null-shader>";
                        string bc  = ColorProp(m, "_BaseColor");
                        string col = ColorProp(m, "_Color");
                        bool baseMap = m != null && m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null;
                        bool mainTex = m != null && m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null;
                        // FlowTrace.Fail => lands in the errors-only break-log.jsonl the device pulls.
                        // VISIBLE=True is the row that names the cause; VISIBLE=False rows are the hidden layer.
                        FlowTrace.Fail("ArcaneDiag",
                            "SLOT[" + (si++) + "] rend='" + r.name + "' path='" + Path(r.transform) + "' VISIBLE=" + visible +
                            " (enabled=" + r.enabled + " activeInHier=" + r.gameObject.activeInHierarchy + ")" +
                            " mat='" + mn + "' shader='" + sh + "' baseColor=" + bc + " color=" + col +
                            " baseMap=" + baseMap + " mainTex=" + mainTex);
                    }
                }
            }

            FlowTrace.Fail("ArcaneDiag", "===== END ARCANE TOWER RENDER DUMP =====");

            // WO-719 gap-closer: the baked 'ArcaneTower_MagicUpgrades' above is DEACTIVATED
            // (standdown) and its LightSkin re-skin never spawns - so the VISIBLE white spire the
            // owner sees is a DIFFERENT GameObject the name-filtered dump never touches. It is the
            // BaseLayout replay of the 'arcane-tower' record, built by StructureFactory.Create and
            // named after entry.displayName ('Arcane Tower') / the clone ('arcane tower(Clone)').
            // Find + dump THAT object's live material so the next capture names why it reads white.
            DumpVisibleSpire(roots);
        }

        // Dump the ACTIVE visible arcane spire (StructureFactory replay / any re-skin) regardless of
        // its name. Two finders: (1) any VISIBLE renderer whose hierarchy path contains "arcane";
        // (2) as a backup, the nearest VISIBLE tall renderer to the baked tower's XZ. Logs each
        // candidate's material name/shader/_BaseColor/_BaseMap/_MainTex via FlowTrace.Fail (break-log),
        // plus whether a TripoMaterialFixer / ArcaneAura sits in its hierarchy. VISIBLE + _BaseMap=False
        // => the forced albedo never reached this material (load null / stomped / not-run). VISIBLE +
        // _BaseMap=True but still white => the bound texture itself, or lighting/aura, is the cause.
        private static void DumpVisibleSpire(System.Collections.Generic.List<Transform> bakedRoots)
        {
            // Reference XZ = the (deactivated) baked tower, if present - the replay sits near it.
            bool haveRef = false;
            Vector3 refPos = Vector3.zero;
            if (bakedRoots != null)
                foreach (var b in bakedRoots)
                    if (b != null) { refPos = b.position; haveRef = true; break; }

            var candidates = new System.Collections.Generic.List<Renderer>();
            Renderer nearestTall = null;
            float nearestDist = float.MaxValue;

            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r == null) continue;
                if (!(r.enabled && r.gameObject.activeInHierarchy)) continue;   // VISIBLE only
                string path = Path(r.transform).ToLowerInvariant();
                bool byName = path.Contains("arcane");
                if (byName && !candidates.Contains(r)) candidates.Add(r);

                if (haveRef)
                {
                    Bounds bb = r.bounds;
                    float dxz = new Vector2(bb.center.x - refPos.x, bb.center.z - refPos.z).magnitude;
                    // Tall (spire) + near the baked tower + not itself already name-matched.
                    if (dxz < 10f && bb.size.y >= 3f && bb.size.y >= bb.size.x && !byName && dxz < nearestDist)
                    {
                        nearestDist = dxz;
                        nearestTall = r;
                    }
                }
            }
            if (nearestTall != null && !candidates.Contains(nearestTall)) candidates.Add(nearestTall);

            if (candidates.Count == 0)
            {
                FlowTrace.Fail("ArcaneDiag",
                    "VISIBLE-SPIRE: no active renderer named '*arcane*' AND no tall visible renderer within 10m of the " +
                    "baked tower - the visible spire is either absent or an unexpected object (widen the search next pass).");
                return;
            }

            FlowTrace.Fail("ArcaneDiag",
                "===== VISIBLE SPIRE DUMP (active re-skin / BaseLayout replay) candidates=" + candidates.Count +
                " haveBakedRef=" + haveRef + " =====");

            int ci = 0;
            foreach (var r in candidates)
            {
                if (r == null) continue;
                if (ci >= 16) break;
                var top = Topmost(r.transform);
                bool hasFixer = HasComp(top.gameObject, "TripoMaterialFixer");
                bool hasAura  = HasComp(top.gameObject, "ArcaneAura");
                Bounds b = r.bounds;
                FlowTrace.Fail("ArcaneDiag",
                    "VIS[" + (ci++) + "] root='" + top.name + "' path='" + Path(r.transform) + "' pos=" + r.transform.position +
                    " boundsSize=(" + b.size.x.ToString("F1") + "," + b.size.y.ToString("F1") + "," + b.size.z.ToString("F1") + ")" +
                    " hasTripoFixer=" + hasFixer + " hasArcaneAura=" + hasAura);

                var mats = r.sharedMaterials;
                int slots = mats != null ? mats.Length : 0;
                for (int i = 0; i < slots && i < 8; i++)
                {
                    var m = mats[i];
                    string mn = m != null ? m.name : "<null-mat>";
                    string sh = (m != null && m.shader != null) ? m.shader.name : "<null-shader>";
                    bool baseMap = m != null && m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null;
                    bool mainTex = m != null && m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null;
                    string bmName = "-";
                    if (m != null && m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null) bmName = m.GetTexture("_BaseMap").name;
                    else if (m != null && m.HasProperty("_MainTex") && m.GetTexture("_MainTex") != null) bmName = m.GetTexture("_MainTex").name;
                    FlowTrace.Fail("ArcaneDiag",
                        "  VIS-SLOT[" + i + "] mat='" + mn + "' shader='" + sh + "' baseColor=" + ColorProp(m, "_BaseColor") +
                        " color=" + ColorProp(m, "_Color") + " baseMap=" + baseMap + " mainTex=" + mainTex + " texName='" + bmName + "'");
                }
            }

            FlowTrace.Fail("ArcaneDiag", "===== END VISIBLE SPIRE DUMP =====");
        }

        // Name-based component probe (no cross-assembly ref: ArcaneAura lives in DeNelle.Village,
        // which Core must not reference). One-shot, so the full component walk is fine.
        private static bool HasComp(GameObject go, string typeName)
        {
            if (go == null) return false;
            foreach (var c in go.GetComponentsInChildren<Component>(true))
                if (c != null && c.GetType().Name == typeName) return true;
            return false;
        }

        private static Transform Topmost(Transform t)
        {
            var top = t;
            while (top.parent != null) top = top.parent;
            return top;
        }

        private static string ColorProp(Material m, string prop)
        {
            if (m == null || !m.HasProperty(prop)) return "-";
            var c = m.GetColor(prop);
            return "(" + c.r.ToString("F2") + "," + c.g.ToString("F2") + "," + c.b.ToString("F2") + "," + c.a.ToString("F2") + ")";
        }

        private static string Path(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }
    }
}
