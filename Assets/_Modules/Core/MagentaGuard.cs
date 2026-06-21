// =============================================================================
// MagentaGuard — global runtime safety net for "pink/magenta" renderers (TKT-1).
// -----------------------------------------------------------------------------
// SYMPTOM (owner playtest, recurring): ground / objects render PINK/MAGENTA in the
// BUILT player but look fine in the editor. CAUSE: a material on a Built-in/Standard/
// Legacy shader — or a Shader Graph not referenced by a serialized material in a built
// scene — is STRIPPED from the URP player build. At runtime its shader resolves to
// Hidden/InternalErrorShader = magenta. This only happens in a BUILD (the editor never
// strips), so an editor-only scan finds nothing (the autopilot magenta probe ran in the
// editor-built player here and reported ZERO — the strip can't reproduce in-editor).
//
// WHY A GLOBAL GUARD (not yet another per-object fixer): the project already has ~8
// TARGETED fixers (Tripo / Polyperfect / Tree / Portal / Worker / HeroArmor …). Each
// only covers ITS own objects, so a NEW magenta source (e.g. a procedurally-instantiated
// Quaternius floor) slips through until someone hunts it object-by-object. This catches
// ANY magenta renderer in ANY scene at load, recovers it, AND logs exactly what it caught
// (hierarchy path / scene / material / dead shader) — so the offender self-identifies in
// the break-log instead of costing a guess-and-rebuild cycle (§12: instrument, don't guess).
//
// WHAT IT DOES (asset-independent, idempotent, WebGL-safe):
//   On every scene load, scan active Renderers' sharedMaterials; any material whose shader
//   is null / Hidden/InternalErrorShader / Standard / Legacy / Specular setup is swapped
//   IN PLACE to URP/Lit, carrying base colour + main texture + emission. Valid URP / Unlit
//   / Particles / UI shaders are left untouched. Each unique material is processed once and
//   already-URP materials are skipped, so repeated loads are no-ops.
//
// Mirrors GroundZFightFixer's lifecycle: [RuntimeInitializeOnLoadMethod(AfterSceneLoad)]
// + SceneManager.sceneLoaded re-arm (the player boots into Title and reaches gameplay
// scenes later), every entry point wrapped in try/catch (an uncaught sceneLoaded
// exception halts the WebGL player).
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core
{
    public static class MagentaGuard
    {
        private static Shader _lit;
        private static Shader _terrainLit;
        private static bool _hooked;
        private static readonly HashSet<string> _floorSeen = new HashSet<string>();

        // A large, flat, ground-like renderer (by name or by footprint) — the candidates for a
        // "pink floor". Used only to NAME them in the diagnostic, never to auto-mutate (a terrain
        // shader is valid; we fix the named cause, not blindly swap the floor).
        private static bool IsGroundLike(Renderer r)
        {
            if (r == null) return false;
            string n = r.name.ToLowerInvariant();
            if (n.Contains("floor") || n.Contains("ground") || n.Contains("terrain") ||
                n.Contains("plaza") || n.Contains("courtyard")) return true;
            string mesh = MeshName(r).ToLowerInvariant();
            if (mesh.Contains("floor") || mesh.Contains("ground") || mesh.Contains("plane")) return true;
            var b = r.bounds; // big footprint, thin in Y = a floor slab
            return b.size.x > 8f && b.size.z > 8f && b.size.y < 2f;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            if (_hooked) return;
            _hooked = true;
            SceneManager.sceneLoaded += OnSceneLoaded;
            // sceneLoaded does NOT fire for the scene already active at boot — sweep it now.
            Sweep("boot");
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode) => Sweep(scene.name);

        private static void Sweep(string sceneName)
        {
            try
            {
                if (_lit == null) _lit = Shader.Find("Universal Render Pipeline/Lit");
                if (_lit == null)
                {
                    FlowTrace.Warn("MagentaGuard", "no URP/Lit shader found — cannot recover magenta materials.");
                    return;
                }

                var renderers = Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None);
                if (renderers == null || renderers.Length == 0) return;

                var seen = new HashSet<Material>();
                int recovered = 0, hiddenStray = 0;
                foreach (var r in renderers)
                {
                    if (r == null) continue;
                    var mats = r.sharedMaterials;
                    if (mats == null) continue;

                    // FLOOR FIX (TKT-1, owner "still pink floor"): the Raid PROVED the lavender floor is
                    // the Quaternius courtyard (material 'MI_WoodTrim', shader 'Shader Graphs/M_BaseMaterial')
                    // rendering an UNTEXTURED white surface the scene lighting tints lavender — its albedo
                    // didn't survive the build. NOT bright magenta, so the broken-shader recovery skipped it.
                    // So: a GROUND-LIKE renderer on a NON-URP-Lit, non-particles/terrain shader → retarget to
                    // URP/Lit, carrying any albedo texture we can find (Quaternius uses '_Base_Color'); if none
                    // survived, set a warm wood/stone base so it's NEVER lavender. Mutates the SHARED material
                    // once (all tiles fixed); idempotent (re-runs see URP/Lit and skip). Textured floors are
                    // left alone. Quiet: a single Step, not an error per tile.
                    if (_lit != null && IsGroundLike(r))
                    {
                        // PROVEN (RCA 2026-06-21): the colorless RE-CHECK below must NOT sit behind the
                        // `_floorSeen.Add` name-dedup — `_floorSeen` is process-static, so a name consumed on
                        // an EARLIER sweep made `Add` return false on the sweep where the tile is actually
                        // colorless, and the branch NEVER fired (no FLOOR-FIX(colorless) log, tiles stayed
                        // (0,0,0,0)). The dedup is only correct for the one-time nonLit IDENTITY swap; the
                        // colorless fix tests a mutable STATE and must run every sweep (self-idempotent:
                        // once repainted opaque, colorless=false -> no-op).
                        bool firstName = _floorSeen.Add(r.name);
                        var fm = mats.Length > 0 ? mats[0] : null;
                        string fsh = fm != null && fm.shader != null ? fm.shader.name : "";
                        bool nonLit = fm != null && fm.shader != null
                                      && fsh != "Universal Render Pipeline/Lit"
                                      && fsh.IndexOf("Particles", System.StringComparison.OrdinalIgnoreCase) < 0
                                      && fsh.IndexOf("Terrain", System.StringComparison.OrdinalIgnoreCase) < 0
                                      && fsh.IndexOf("Unlit", System.StringComparison.OrdinalIgnoreCase) < 0;
                        if (firstName && nonLit)
                        {
                            Texture tex = null;
                            foreach (var prop in new[] { "_BaseMap", "_MainTex", "_Base_Color", "_BaseColorMap", "_Albedo", "_AlbedoMap" })
                                if (fm.HasProperty(prop) && fm.GetTexture(prop) != null) { tex = fm.GetTexture(prop); break; }
                            fm.shader = _lit;
                            if (tex != null && fm.HasProperty("_BaseMap")) fm.SetTexture("_BaseMap", tex);
                            else if (fm.HasProperty("_BaseColor")) fm.SetColor("_BaseColor", new Color(0.42f, 0.34f, 0.24f, 1f)); // warm wood — never lavender
                            FlowTrace.Step("MagentaGuard",
                                $"FLOOR-FIX '{fm.name}' (was '{fsh}', scene '{r.gameObject.scene.name}') -> URP/Lit, albedoTex={(tex != null)} — lavender floor corrected.");
                        }
                        else if (fm != null && fsh == "Universal Render Pipeline/Lit")
                        {
                            // PROVEN (RCA 2026-06-21, floordiag-probe2.log): the visible castle floor is the
                            // CourtyardFloor_* tiles — URP/Lit, no _BaseMap. Their material is FBX-EMBEDDED
                            // (Floor_WoodDark.fbx, externalObjects:{}), so fm.HasProperty("_BaseColor") is FALSE.
                            // The PRIOR fix guarded the whole branch on HasProperty("_BaseColor") -> it short-
                            // circuited and NEVER fired (zero FLOOR-FIX lines in the full log) — THIS is why every
                            // floor fix (incl. mine) missed it. Under the lavender Trilight ambient a colorless lit
                            // tile renders pink. Fix: treat colorless OR property-missing as "needs paint", and
                            // ASSIGN A FRESH URP/Lit material to the renderer (mutating the embedded SHARED material
                            // may not stick in a built player). Self-idempotent: once repainted opaque, colorless=false.
                            bool hasTex = fm.HasProperty("_BaseMap") && fm.GetTexture("_BaseMap") != null;
                            bool hasBC  = fm.HasProperty("_BaseColor");
                            Color bc    = hasBC ? fm.GetColor("_BaseColor") : Color.clear;
                            bool colorless = !hasBC || bc.a < 0.05f || (bc.r + bc.g + bc.b) < 0.05f;
                            if (!hasTex && colorless)
                            {
                                // §12 proof line: logs the disambiguator (hasBC) at fix time — captured to the full
                                // Player.log (Step lands there; the break-log is errors-only — see BreakCaptureHarness).
                                var fresh = new Material(_lit) { name = fm.name + "_FloorFix" };
                                fresh.SetColor("_BaseColor", new Color(0.42f, 0.34f, 0.24f, 1f));   // opaque warm wood/stone
                                if (fresh.HasProperty("_Surface")) fresh.SetFloat("_Surface", 0f);  // URP: 0 = Opaque
                                if (fresh.HasProperty("_ZWrite"))  fresh.SetFloat("_ZWrite", 1f);
                                fresh.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Geometry;
                                var sm = r.sharedMaterials;
                                if (sm != null && sm.Length > 0) { sm[0] = fresh; r.sharedMaterials = sm; }
                                FlowTrace.Step("MagentaGuard",
                                    $"FLOOR-FIX(colorless URP/Lit) '{fm.name}' (scene '{r.gameObject.scene.name}') hasBaseColorProp={hasBC} was {bc} -> FRESH opaque warm wood (pink floor corrected).");
                            }
                        }
                    }

                    // First broken material on this renderer (and its dead shader name for the log).
                    Material brokenMat = null;
                    foreach (var m in mats)
                        if (m != null && IsBrokenShader(m.shader)) { brokenMat = m; break; }
                    if (brokenMat == null) continue;
                    string deadShader = brokenMat.shader != null ? brokenMat.shader.name : "<null>";

                    // A magenta BUILT-IN PRIMITIVE (Capsule/Cube/Sphere…) is never intended art — it is a
                    // stray placeholder pill (CastleSpawnMarkerHider exists for exactly these). Real art
                    // would carry a valid URP material. So HIDE the primitive (matches the hider's intent)
                    // rather than recolour it to a grey pill that still litters the scene.
                    if (IsPrimitivePlaceholder(r))
                    {
                        r.enabled = false;
                        hiddenStray++;
                        FlowTrace.Fail("MagentaGuard",
                            $"hid stray MAGENTA placeholder '{HierarchyPath(r.transform)}' (scene '{r.gameObject.scene.name}') " +
                            $"mesh='{MeshName(r)}' material '{brokenMat.name}' dead-shader '{deadShader}' — built-in primitive, not real art.");
                        continue;
                    }

                    // Real art that merely lost its shader in the build: recover each unique broken
                    // material on it to URP/Lit, carrying colour/albedo/emission.
                    foreach (var m in mats)
                    {
                        if (m == null || !IsBrokenShader(m.shader)) continue;
                        if (!seen.Add(m)) continue;            // each unique material once
                        string dead = m.shader != null ? m.shader.name : "<null>";
                        RecoverMaterial(m);
                        recovered++;
                        // FAIL (not Step): a magenta in the shipped player IS a break — surface it in the
                        // F8 break-log with the EXACT object so the source can be fixed at root too.
                        FlowTrace.Fail("MagentaGuard",
                            $"recovered MAGENTA renderer '{HierarchyPath(r.transform)}' (scene '{r.gameObject.scene.name}') " +
                            $"material '{m.name}' dead-shader '{dead}' -> URP/Lit (pink killed at runtime; fix at source).");
                    }
                }

                // TERRAIN PASS (owner F8 2026-06-21 "Pink Floor", ran-from-exe): MainCastle_Hall's VISIBLE
                // floor is the additively-loaded OuterWorld Terrain (the wood courtyard tiles are dropped to
                // Y=-0.5 by GroundZFightFixer and hidden). A Terrain is NOT a Renderer, so the loop above
                // never sees it AND the IsGroundLike path excludes "Terrain" shaders — so a stripped URP
                // Terrain/Lit shader (-> InternalError = pink) is structurally unreachable by the code above.
                // Recover it here. GUARDED: only acts when the terrain's shader is genuinely broken, so if the
                // pink is from something else (lighting), this is a safe no-op. SOURCE fix is to pin
                // "Universal Render Pipeline/Terrain/Lit" in the build (ExteriorTerrainBuilder.EnsureTerrainShaderIncluded).
                // ── FLOOR-DIAG (owner F8 2026-06-21 "still pink, PERSISTENT") ─────────────────
                // The narrow shader-broken fix didn't resolve it, so DUMP the full ground state on
                // every hub/OuterWorld load — the next F8 names the exact pink surface + cause. All
                // [Flow:FloorDiag] Fail lines land in break-log.jsonl. REMOVE once root-caused.
                // Causes this distinguishes: (a) terrain shader stripped (-> InternalError), (b) terrain
                // LAYER missing its diffuse texture (magenta even with a valid shader — the modified
                // Exterior_*.terrainlayer are suspects), (c) null terrain material, (d) a violet/tinted
                // scene light making a white floor read lavender, (e) the visible floor is a Renderer not
                // the Terrain. Keeps the broken-shader auto-recovery.
                int terrainFixed = 0;
                if (_terrainLit == null) _terrainLit = Shader.Find("Universal Render Pipeline/Terrain/Lit");
                var terrains = Object.FindObjectsByType<Terrain>(FindObjectsSortMode.None);
                FlowTrace.Step("FloorDiag", $"sweep '{sceneName}': {(terrains != null ? terrains.Length : 0)} Terrain(s); URP-Terrain-Lit-found={_terrainLit != null}.");
                if (terrains != null)
                {
                    foreach (var t in terrains)
                    {
                        if (t == null) continue;
                        var tm = t.materialTemplate;
                        string sh = (tm != null && tm.shader != null) ? tm.shader.name : "<null-mat-or-shader>";
                        string layers = "<no terrainData>";
                        var td = t.terrainData;
                        if (td != null && td.terrainLayers != null)
                        {
                            var lb = new System.Text.StringBuilder();
                            for (int li = 0; li < td.terrainLayers.Length; li++)
                            {
                                var L = td.terrainLayers[li];
                                string dt = (L != null && L.diffuseTexture != null) ? L.diffuseTexture.name : "<NULL-DIFFUSE>";
                                lb.Append("[" + li + "]" + (L != null ? L.name : "<null>") + ":" + dt + " ");
                            }
                            layers = td.terrainLayers.Length + " layer(s): " + lb;
                        }
                        FlowTrace.Step("FloorDiag",
                            "TERRAIN '" + t.name + "' scene='" + t.gameObject.scene.name + "' pos=" + t.transform.position +
                            " mat='" + (tm != null ? tm.name : "<NULL>") + "' shader='" + sh + "' broken=" + (tm != null && IsBrokenShader(tm.shader)) + " " + layers);
                        if (tm != null && IsBrokenShader(tm.shader) && _terrainLit != null)
                        {
                            tm.shader = _terrainLit; terrainFixed++;
                            FlowTrace.Warn("FloorDiag", "-> recovered TERRAIN '" + t.name + "' broken shader -> URP Terrain/Lit.");
                        }
                    }
                }
                // Scene lighting — a violet/tinted ambient or sun makes a plain floor read lavender.
                var sun = RenderSettings.sun;
                FlowTrace.Step("FloorDiag",
                    "LIGHTING scene='" + sceneName + "' ambientMode=" + RenderSettings.ambientMode + " ambient=" + RenderSettings.ambientLight +
                    " sun=" + (sun != null ? (sun.name + " color=" + sun.color + " intensity=" + sun.intensity) : "<none>"));
                // Ground-like Renderers (in case the visible floor is a Renderer, not the Terrain).
                int gdump = 0;
                foreach (var r in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
                {
                    if (r == null || !IsGroundLike(r) || gdump >= 12) continue;
                    var m0 = (r.sharedMaterials != null && r.sharedMaterials.Length > 0) ? r.sharedMaterials[0] : null;
                    string gsh = (m0 != null && m0.shader != null) ? m0.shader.name : "<null>";
                    Color gc = (m0 != null && m0.HasProperty("_BaseColor")) ? m0.GetColor("_BaseColor")
                             : (m0 != null && m0.HasProperty("_Color") ? m0.color : Color.clear);
                    FlowTrace.Step("FloorDiag", "GROUND '" + HierarchyPath(r.transform) + "' scene='" + r.gameObject.scene.name +
                        "' shader='" + gsh + "' baseColor=" + gc + " size=" + r.bounds.size);
                    gdump++;
                }

                if (recovered > 0 || hiddenStray > 0 || terrainFixed > 0)
                    FlowTrace.Step("MagentaGuard",
                        $"sweep '{sceneName}': recovered {recovered} lost-shader material(s) -> URP/Lit, hid {hiddenStray} stray placeholder primitive(s), fixed {terrainFixed} terrain(s).");
            }
            catch (System.Exception e)
            {
                FlowTrace.Fail("MagentaGuard", $"sweep '{sceneName}' threw: {e.Message}");
            }
        }

        // A shader that renders MAGENTA (or grey/wrong) under URP, or is missing entirely. Valid
        // URP / Unlit / Particles / UI / Skybox shaders are NOT broken and are left untouched.
        private static bool IsBrokenShader(Shader sh)
        {
            if (sh == null) return true;
            string sn = sh.name;
            if (string.IsNullOrEmpty(sn)) return true;
            return sn == "Standard"
                || sn == "Standard (Specular setup)"
                || sn.StartsWith("Legacy Shaders/")
                || sn.Contains("InternalError")
                || sn.Contains("Hidden/InternalError");
        }

        // Swap to URP/Lit IN PLACE, carrying authored colour + albedo + emission so the recovered
        // surface reads as close to intended as the lost shader's base properties allow.
        private static void RecoverMaterial(Material m)
        {
            Color col = m.HasProperty("_Color") ? m.color : Color.white;
            Texture tex = m.HasProperty("_MainTex") ? m.mainTexture : null;
            Color emis = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;

            m.shader = _lit;

            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", col);
            if (m.HasProperty("_Color")) m.color = col;
            if (tex != null)
            {
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", tex);
                if (m.HasProperty("_MainTex")) m.mainTexture = tex;
            }
            if (emis != Color.black && m.HasProperty("_EmissionColor"))
            {
                m.SetColor("_EmissionColor", emis);
                m.EnableKeyword("_EMISSION");
            }
        }

        // A renderer whose mesh is one of Unity's BUILT-IN PRIMITIVES (the meshes
        // GameObject.CreatePrimitive produces). A magenta one is a stray placeholder, not art.
        private static bool IsPrimitivePlaceholder(Renderer r)
        {
            string mesh = MeshName(r);
            return mesh == "Capsule" || mesh == "Cube" || mesh == "Sphere"
                || mesh == "Cylinder" || mesh == "Plane" || mesh == "Quad";
        }

        private static string MeshName(Renderer r)
        {
            if (r is SkinnedMeshRenderer smr && smr.sharedMesh != null) return smr.sharedMesh.name;
            var mf = r.GetComponent<MeshFilter>();
            return mf != null && mf.sharedMesh != null ? mf.sharedMesh.name : "<none>";
        }

        private static string HierarchyPath(Transform t)
        {
            var sb = new System.Text.StringBuilder(t.name);
            for (var p = t.parent; p != null; p = p.parent) sb.Insert(0, p.name + "/");
            return sb.ToString();
        }
    }
}
