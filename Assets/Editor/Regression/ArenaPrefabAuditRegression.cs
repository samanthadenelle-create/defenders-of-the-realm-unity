// =============================================================================
// ArenaPrefabAuditRegression (F8-37 "arena pole") -- EDITOR ASSET AUDIT oracle.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Editor (editor-only)   Namespace: DeNelle.Editor
//
// THE TICKET (F8-37, screenshots flag_05 / flag_02): a giant UNTEXTURED cylinder
// ("pole") appears in the BattleArena. Prior instrumentation RCA proved the arena
// RUNTIME code (BattleArena.BuildArena, ArenaBiomeDressing) creates ONLY Plane/Quad
// primitives -- NO Cylinder/Capsule anywhere -- so an oversized untextured column can
// only come from the LOADED landscape PREFAB (Resources/Arena/ForestClearingArena,
// authored by ArenaPrefabBuilder) or a stray mesh inside it.
//
// This oracle finds it WITHOUT playing or rendering: it Resources.Loads the arena
// landscape prefab(s), walks every MeshFilter/Renderer, and FAILS on
//   (a) a Cylinder/Capsule/oversized-primitive mesh, or a tall "pole" column, and
//   (b) any renderer whose material is null / Default-Material / InternalErrorShader
//       / carries no _BaseMap|_MainTex texture (the magenta/untextured class the
//       ArenaPrefabBuilder comment explicitly warns a serialized `new Material()`
//       produces).
// Runs in SECONDS. Deterministic. Self-contained. Editor-only asset reads.
//
// Contract mirrors MonetizationCovenantRegression.Run(out string reason):
//   true  = pass  (reason = one-line summary)
//   false = fail  (reason = exact offending object path + mesh + material + size)
//
// Orchestrator (DataRegression.RunAll) registers it covenant-style:
//   if (!ArenaPrefabAuditRegression.Run(out var arenaReason)) failures.Add(arenaReason); else log.AppendLine("[arena-prefab] " + arenaReason);
// =============================================================================

using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class ArenaPrefabAuditRegression
    {
        // The arena landscape prefab BattleArena.BuildArena actually loads (line ~473).
        // There is currently ONE landscape prefab; extra Resources-path names are audited
        // too if they exist (future biome variants) so this gate covers whatever ships.
        private static readonly string[] ArenaPrefabResourcePaths =
        {
            "Arena/ForestClearingArena",
        };

        // A vertical column ("pole"): tall in Y, with a small footprint relative to its
        // height. The real arena content is FLAT (ground Planes) or short props (trees/rocks,
        // <~8m). Anything >12m tall AND narrower than 0.6x its height reads as a pole.
        private const float PoleMinHeight   = 12f;
        private const float PoleFootprintFrac = 0.6f;
        // A builtin primitive (Cube/Cylinder/Capsule/Sphere) is legit only when small. Above
        // this any-axis world size it's an errant blocker mesh. (Ground Planes are exempt --
        // a ground plane is legitimately huge in X/Z but never tall.)
        private const float PrimitiveMaxSize = 15f;

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var summary  = new StringBuilder();
            int prefabsAudited = 0;
            int renderersAudited = 0;

            foreach (var resPath in ArenaPrefabResourcePaths)
            {
                var prefab = Resources.Load<GameObject>(resPath);
                if (prefab == null)
                {
                    // The primary landscape prefab is missing -> runtime degrades to the plain
                    // ground fallback (BattleArena.BuildFallbackFloor). That is itself a finding.
                    if (resPath == ArenaPrefabResourcePaths[0])
                        failures.Add($"arena landscape prefab MISSING at Resources/{resPath} -> runtime falls back to plain ground (no authored landscape).");
                    continue;
                }

                prefabsAudited++;
                // Instantiate once so world-space bounds (full transform hierarchy scale) and
                // runtime material state read accurately. Torn down in finally -> no leak.
                GameObject instance = null;
                try
                {
                    instance = Object.Instantiate(prefab);
                    instance.name = "__ArenaAudit_" + prefab.name;
                    // Keep it inert/off-screen; no play, no render.
                    instance.transform.position = new Vector3(0f, -10000f, 0f);

                    AuditHierarchy(instance, resPath, prefab.name, failures, ref renderersAudited);
                }
                finally
                {
                    if (instance != null) Object.DestroyImmediate(instance);
                }
            }

            if (prefabsAudited == 0 && failures.Count == 0)
            {
                // No prefab found AND not the primary path (defensive) -- report loudly.
                reason = "arena prefab audit: NO arena landscape prefab resolved under Resources/Arena/ (nothing to audit).";
                return false;
            }

            if (failures.Count > 0)
            {
                var sb = new StringBuilder();
                sb.Append($"arena prefab audit FAILED ({failures.Count} finding(s), {renderersAudited} renderer(s) across {prefabsAudited} prefab(s)): ");
                sb.Append(string.Join(" | ", failures));
                reason = sb.ToString();
                return false;
            }

            reason = $"arena prefab audit OK: audited {renderersAudited} renderer(s) in {prefabsAudited} prefab(s) ({string.Join(", ", ArenaPrefabResourcePaths)}); all textured, no oversized primitive / pole.";
            return true;
        }

        // Walk every MeshFilter + Renderer under the instantiated prefab and flag pole/untextured.
        private static void AuditHierarchy(GameObject instance, string resPath, string prefabName, List<string> failures, ref int renderersAudited)
        {
            // --- geometry: pole / oversized-primitive detection -----------------
            var filters = instance.GetComponentsInChildren<MeshFilter>(true);
            foreach (var mf in filters)
            {
                if (mf == null) continue;
                var mesh = mf.sharedMesh;
                if (mesh == null)
                {
                    failures.Add($"[{prefabName}] '{PathOf(mf.transform, instance.transform)}' has a MeshFilter with NULL sharedMesh (invisible/broken geo).");
                    continue;
                }

                // World-space size = mesh local bounds transformed by the full hierarchy matrix.
                Vector3 worldSize = WorldBoundsSize(mf.transform, mesh);
                string meshName = mesh.name ?? "<unnamed>";
                bool isPlane = meshName.Contains("Plane") || NameSuggestsGround(mf.transform);

                // (1) Cylinder/Capsule builtin -- the PRIME suspect shape for the pole.
                if (meshName.Contains("Cylinder") || meshName.Contains("Capsule"))
                {
                    failures.Add($"[{prefabName}] POLE SUSPECT: mesh '{meshName}' at '{PathOf(mf.transform, instance.transform)}' worldSize={Fmt(worldSize)} (a Cylinder/Capsule primitive has no place in the arena landscape -- untextured column class).");
                    continue;
                }

                // (2) Any tall vertical column, whatever the mesh -- the pole shape.
                float footprint = Mathf.Max(worldSize.x, worldSize.z);
                if (!isPlane && worldSize.y > PoleMinHeight && footprint < worldSize.y * PoleFootprintFrac)
                {
                    failures.Add($"[{prefabName}] POLE SHAPE: '{PathOf(mf.transform, instance.transform)}' mesh '{meshName}' is a tall column worldSize={Fmt(worldSize)} (height {worldSize.y:0.#}m >> footprint {footprint:0.#}m).");
                    continue;
                }

                // (3) An oversized builtin primitive (Cube/Sphere) sitting in the scene.
                if ((meshName == "Cube" || meshName.Contains("Sphere")) &&
                    (worldSize.x > PrimitiveMaxSize || worldSize.y > PrimitiveMaxSize || worldSize.z > PrimitiveMaxSize))
                {
                    failures.Add($"[{prefabName}] OVERSIZED PRIMITIVE: '{PathOf(mf.transform, instance.transform)}' mesh '{meshName}' worldSize={Fmt(worldSize)} (>{PrimitiveMaxSize}m -- errant blocker mesh).");
                    continue;
                }
            }

            // --- material: untextured / magenta detection -----------------------
            var renderers = instance.GetComponentsInChildren<Renderer>(true);
            foreach (var r in renderers)
            {
                if (r == null) continue;
                // Lights/probes have no mesh material; only surface renderers matter.
                if (!(r is MeshRenderer) && !(r is SkinnedMeshRenderer)) continue;
                renderersAudited++;

                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0)
                {
                    failures.Add($"[{prefabName}] '{PathOf(r.transform, instance.transform)}' renderer has NO materials (renders magenta/untextured).");
                    continue;
                }

                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    string path = PathOf(r.transform, instance.transform);
                    string slot = mats.Length > 1 ? $"[slot {i}]" : "";

                    if (m == null)
                    {
                        failures.Add($"[{prefabName}] '{path}'{slot} sharedMaterial is NULL (serialized {{fileID: 0}} -> URP renders MAGENTA -- the exact ArenaPrefabBuilder `new Material()` warning).");
                        continue;
                    }

                    string matName = m.name ?? "<unnamed>";
                    var shader = m.shader;
                    string shaderName = shader != null ? shader.name : "<null shader>";

                    if (shader == null)
                    {
                        failures.Add($"[{prefabName}] '{path}'{slot} material '{matName}' has a NULL shader (renders magenta).");
                        continue;
                    }
                    if (shaderName.Contains("InternalErrorShader") || shaderName.Contains("Hidden/InternalError"))
                    {
                        failures.Add($"[{prefabName}] '{path}'{slot} material '{matName}' uses {shaderName} (broken/missing shader -> magenta).");
                        continue;
                    }
                    if (matName == "Default-Material" || matName == "Default-Diffuse" ||
                        matName == "Default-Line"     || matName == "Default-ParticleSystem" ||
                        matName == "Default-Terrain-Standard")
                    {
                        failures.Add($"[{prefabName}] '{path}'{slot} uses builtin '{matName}' (untextured default surface -- authored art missing).");
                        continue;
                    }

                    // No _BaseMap AND no _MainTex texture bound -> untextured surface (the class
                    // the builder comment warns about). Only assert when the shader actually
                    // EXPOSES a base-texture slot (a color-only shader legitimately has none).
                    bool hasBaseMapProp = m.HasProperty("_BaseMap");
                    bool hasMainTexProp = m.HasProperty("_MainTex");
                    if (hasBaseMapProp || hasMainTexProp)
                    {
                        Texture baseTex = hasBaseMapProp ? m.GetTexture("_BaseMap") : null;
                        Texture mainTex = hasMainTexProp ? m.GetTexture("_MainTex") : null;
                        if (baseTex == null && mainTex == null)
                        {
                            failures.Add($"[{prefabName}] '{path}'{slot} material '{matName}' ({shaderName}) binds NO base texture (_BaseMap/_MainTex both null -> flat/untextured surface).");
                            continue;
                        }
                    }
                }
            }
        }

        // ---- helpers -------------------------------------------------------------

        // World-space AABB size of a mesh under the given transform (full hierarchy scale).
        private static Vector3 WorldBoundsSize(Transform t, Mesh mesh)
        {
            Bounds local = mesh.bounds;
            Vector3 c = local.center;
            Vector3 e = local.extents;
            var m = t.localToWorldMatrix;
            var min = new Vector3(float.MaxValue, float.MaxValue, float.MaxValue);
            var max = new Vector3(float.MinValue, float.MinValue, float.MinValue);
            for (int xi = -1; xi <= 1; xi += 2)
            for (int yi = -1; yi <= 1; yi += 2)
            for (int zi = -1; zi <= 1; zi += 2)
            {
                Vector3 corner = c + new Vector3(e.x * xi, e.y * yi, e.z * zi);
                Vector3 w = m.MultiplyPoint3x4(corner);
                min = Vector3.Min(min, w);
                max = Vector3.Max(max, w);
            }
            return max - min;
        }

        private static bool NameSuggestsGround(Transform t)
        {
            string n = t.name.ToLowerInvariant();
            return n.Contains("ground") || n.Contains("floor") || n.Contains("landscape") || n.Contains("terrain");
        }

        // Hierarchy path from the prefab root (excludes the audit-instance root name).
        private static string PathOf(Transform t, Transform root)
        {
            var stack = new List<string>();
            var cur = t;
            while (cur != null && cur != root)
            {
                stack.Add(cur.name);
                cur = cur.parent;
            }
            stack.Reverse();
            return stack.Count == 0 ? "<root>" : string.Join("/", stack);
        }

        private static string Fmt(Vector3 v) => $"({v.x:0.#} x {v.y:0.#} x {v.z:0.#})m";
    }
}
