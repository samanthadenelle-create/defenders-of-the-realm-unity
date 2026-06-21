// =============================================================================
// FloorDeepDiag — DEEP root diagnostic for the recurring MainCastle_Hall "pink
// floor" (owner: "we need deeper root testing on it").
//
// The material baseColor is PROVEN warm (0.42,0.34,0.24) at Play, yet the owner
// still sees pink in Play. So the pink is NOT the base color — it must be lighting,
// emission, a surface ON TOP, fog, or post-processing TINTING what the camera sees.
// MagentaGuard's FloorDiag only dumps named ground renderers' baseColor; this dumps
// the FULL render context so the captured data NAMES the real cause:
//   - EVERY renderer overlapping the floor footprint BY GEOMETRY (not by name) —
//     catches a second floor layer / decal / overlay MagentaGuard never matched.
//   - ALL colour-ish material channels per surface (_BaseColor, _Color, _EmissionColor
//     + emission keyword, _TintColor, _BaseMap presence, shader, renderQueue, Y).
//   - ALL lights (type/colour/intensity/enabled) — a tinted light reads as a tint.
//   - Ambient (mode/colour/intensity), fog (colour/density), camera clear+background.
//   - Any post-processing Volume (URP) — colour grading is a prime suspect.
//
// Runs ONE second after MainCastle_Hall loads (so MagentaGuard's repaint applies
// first — we capture the POST-fix render state). Every line is FlowTrace.Step
// ([Flow:FloorDeep]) -> lands in the full Player.log (NOT the errors-only break-log).
// Self-contained; remove this file once the root is found.
// =============================================================================
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Core.Diagnostics
{
    public static class FloorDeepDiag
    {
        private const string TargetScene = "MainCastle_Hall";
        private static GameObject _host;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            if (_host == null)
            {
                _host = new GameObject("[FloorDeepDiag]");
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
                if (SceneManager.GetActiveScene().name == TargetScene) StartCoroutine(DumpAfter(1f));
            }
            private void OnLoaded(Scene s, LoadSceneMode m)
            {
                if (s.name == TargetScene) StartCoroutine(DumpAfter(1f));
            }

            private IEnumerator DumpAfter(float delay)
            {
                yield return new WaitForSeconds(delay);   // let MagentaGuard's sceneLoaded repaint run first
                Dump();
            }
        }

        private static void Dump()
        {
            FlowTrace.Step("FloorDeep", "===== DEEP FLOOR DUMP (MainCastle_Hall, post-MagentaGuard) =====");

            // --- Camera + ambient + fog ------------------------------------------
            var cam = Camera.main;
            FlowTrace.Step("FloorDeep", cam != null
                ? $"CAMERA '{cam.name}' clear={cam.clearFlags} bg={cam.backgroundColor} pos={cam.transform.position}"
                : "CAMERA: <none>");
            FlowTrace.Step("FloorDeep",
                $"AMBIENT mode={RenderSettings.ambientMode} sky={RenderSettings.ambientSkyColor} " +
                $"equator={RenderSettings.ambientEquatorColor} ground={RenderSettings.ambientGroundColor} " +
                $"flat={RenderSettings.ambientLight} intensity={RenderSettings.ambientIntensity}");
            FlowTrace.Step("FloorDeep",
                $"FOG on={RenderSettings.fog} mode={RenderSettings.fogMode} color={RenderSettings.fogColor} " +
                $"density={RenderSettings.fogDensity} start={RenderSettings.fogStartDistance} end={RenderSettings.fogEndDistance}");

            // --- All lights (with POSITION — to prove which sit over the floor) ---
            int li = 0;
            var activeLights = new System.Collections.Generic.List<Light>();
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (l == null) continue;
                bool on = l.enabled && l.gameObject.activeInHierarchy && l.intensity > 0.01f;
                if (on) activeLights.Add(l);
                FlowTrace.Step("FloorDeep",
                    $"LIGHT[{li++}] '{l.name}' type={l.type} on={on} color={l.color} intensity={l.intensity} " +
                    $"range={l.range} pos={l.transform.position}");
            }

            // --- Every renderer overlapping the floor footprint BY GEOMETRY -------
            // (low + flat + within 40m of origin) — regardless of name, so a second
            // floor layer / decal / overlay that MagentaGuard's name match missed shows up.
            int gi = 0;
            foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (r == null) continue;
                var b = r.bounds;
                bool lowFlat = b.center.y < 1.6f && b.size.y < 1.2f;
                string rn = r.name.ToLowerInvariant();
                bool floorish = (b.size.x * b.size.z) > 3f || rn.Contains("floor") || rn.Contains("courtyard") || rn.Contains("plaza");
                bool nearCenter = new Vector2(b.center.x, b.center.z).magnitude < 45f;
                if (!(lowFlat && floorish && nearCenter)) continue;
                if (gi >= 40) { FlowTrace.Step("FloorDeep", "...(floor-overlap dump truncated at 40)"); break; }
                // nearest ON light over this floor patch + its colour (proves a purple PortalLight is lighting it)
                Light nearest = null; float nd = 9999f;
                foreach (var al in activeLights)
                {
                    float d = Vector3.Distance(al.transform.position, b.center);
                    if (al.type == LightType.Point && d > al.range) continue;
                    if (d < nd) { nd = d; nearest = al; }
                }
                string litBy = nearest != null ? $"{nearest.name} color={nearest.color} d={nd:F1}" : "<none in range>";

                var m = r.sharedMaterial;
                string sh = (m != null && m.shader != null) ? m.shader.name : "<null>";
                string bc = ColorProp(m, "_BaseColor");
                string col = ColorProp(m, "_Color");
                string emi = ColorProp(m, "_EmissionColor");
                bool emiOn = m != null && m.IsKeywordEnabled("_EMISSION");
                string tint = ColorProp(m, "_TintColor");
                bool hasMap = m != null && m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") != null;
                FlowTrace.Step("FloorDeep",
                    $"FLOOROBJ[{gi++}] '{Path(r.transform)}' shader='{sh}' enabled={r.enabled} " +
                    $"y={b.center.y:F2} size=({b.size.x:F1},{b.size.y:F2},{b.size.z:F1}) Q={(m != null ? m.renderQueue : -1)} " +
                    $"baseColor={bc} color={col} emission={emi}(on={emiOn}) tint={tint} baseMap={hasMap} LITBY=[{litBy}]");
            }
            FlowTrace.Step("FloorDeep", $"floor-overlap renderers found: {gi}");

            // --- Post-processing volumes (URP) by reflection (color grading suspect) --
            int vi = 0;
            foreach (var mb in Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None))
            {
                if (mb == null) continue;
                var tn = mb.GetType().Name;
                if (tn == "Volume")
                {
                    FlowTrace.Step("FloorDeep", $"VOLUME[{vi++}] '{mb.name}' enabled={mb.enabled && mb.gameObject.activeInHierarchy} (post-processing — check color grading/tint)");
                }
            }
            FlowTrace.Step("FloorDeep", $"post-process Volumes found: {vi}");
            FlowTrace.Step("FloorDeep", "===== END DEEP FLOOR DUMP =====");
        }

        private static string ColorProp(Material m, string prop)
        {
            if (m == null || !m.HasProperty(prop)) return "-";
            var c = m.GetColor(prop);
            return $"({c.r:F2},{c.g:F2},{c.b:F2},{c.a:F2})";
        }

        private static string Path(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }
    }
}
