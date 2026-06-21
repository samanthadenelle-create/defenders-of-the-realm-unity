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
                    if (_lit != null && IsGroundLike(r) && _floorSeen.Add(r.name))
                    {
                        var fm = mats.Length > 0 ? mats[0] : null;
                        string fsh = fm != null && fm.shader != null ? fm.shader.name : "";
                        bool nonLit = fm != null && fm.shader != null
                                      && fsh != "Universal Render Pipeline/Lit"
                                      && fsh.IndexOf("Particles", System.StringComparison.OrdinalIgnoreCase) < 0
                                      && fsh.IndexOf("Terrain", System.StringComparison.OrdinalIgnoreCase) < 0
                                      && fsh.IndexOf("Unlit", System.StringComparison.OrdinalIgnoreCase) < 0;
                        if (nonLit)
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

                if (recovered > 0 || hiddenStray > 0)
                    FlowTrace.Step("MagentaGuard",
                        $"sweep '{sceneName}': recovered {recovered} lost-shader material(s) -> URP/Lit, hid {hiddenStray} stray placeholder primitive(s).");
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
