// =============================================================================
// OuterWorldCavePortalBuilder — places a FEW walk-in CAVE MOUTHS (outpost entrances)
// and a FEW KayKit-skinned DUNGEON PORTAL entries around the OuterWorld shore so the
// player can walk up to them. Originally WO-468 (single Village2 cave); reworked
// 2026-06-28 (owner: "create a few caves in the bake, we just don't wire them and
// flag them on till ready").
// -----------------------------------------------------------------------------
// Run from:  Defenders > World > Place OuterWorld Cave Portal
// Batchmode: -executeMethod DeNelle.Editor.OuterWorldCavePortalBuilder.PlaceCavePortal
//
// CANON: outposts/dungeons are entered by a placeable warp gate (cave skin = outpost,
// portal skin = dungeon) -> [future] loading zone -> resolver. The resolver/loading-zone
// DOES NOT EXIST YET, so this bake places the ENTRANCE GEOMETRY ONLY and FLAG-GATES the
// warp behavior OFF (FeatureFlags.OutpostCaves / FeatureFlags.DungeonPortals, both default
// OFF) so the shipping build is unaffected. We DO NOT wire Village2 (that stale direct-load
// is removed).
//
// WHAT IT BUILDS (idempotent, clear-then-build; sweeps prior CavePortal*/DungeonPortal*/*_Trigger):
//   * `CavePortal_{i}` GameObjects (a few shore spots) — a real walk-in mouth from the
//     polyperfect tunnel-entrance prefab (fallback: island-cave; final fallback: a tinted
//     primitive cube, CLAUDE.md §4 skip-safe).
//   * `DungeonPortal_{i}` GameObjects (distinct spots) — a KayKit door/arch/portal prefab if
//     the gitignored KayKit pack is present, else a dark archway primitive placeholder.
//   * A `PortalVFXController` on each (DeNelle.Village, self-sufficient, attached by reflection
//     because the Editor asmdef does NOT reference DeNelle.Village).
//   * A child `*_Trigger` ONLY when the matching flag is ON — and even then it is attached
//     INERT (no destination, component disabled) until the resolver slice wires a real target.
//     When the flag is OFF (default) NO trigger is attached: geometry-only, fully inert.
//
// NO .unity hand-edits — this opens, mutates, saves the scene through the editor
// scene API. Batchmode-safe (no EditorUtility dialogs). Canon: village is Elarion.
//
// Instrumented per CLAUDE.md §12 ([Flow:CavePortal] Step/Warn/Fail + Guard.Try
// around the prefab load / reflection). Loud FlowTrace.Fail if the
// SceneTransitionTrigger type cannot be resolved.
// =============================================================================

using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core;                 // FeatureFlags — flag-gated entrance behavior
using DeNelle.Core.Diagnostics;     // FlowTrace / Guard — CLAUDE.md §12

namespace DeNelle.Editor
{
    public static class OuterWorldCavePortalBuilder
    {
        private const string ScenePath = "Assets/Scenes/OuterWorld.unity";

        private const string PolyPrefabRoot =
            "Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/";

        // CAVE MOUTH = a real walk-in opening (owner 2026-06-28: "create a few caves").
        // Primary: the rail-hill tunnel ENTRANCE reads as a true cave mouth you walk into
        // (confirmed on disk). Fallback: the large island cave shell. Both skip-safe -> primitive.
        private const string CaveMouthPrefab =
            PolyPrefabRoot + "Tiles_M/Tunnels_M/Tile_Rail_Hill_Tunnel_Entrance.prefab";
        private const string CaveMouthFallbackPrefab =
            PolyPrefabRoot + "Terrains_M/Islands_M/Island_Cave_Large.prefab";

        // KayKit dungeon-portal art lives under this gitignored pack (may be ABSENT on a clean clone).
        private const string KayKitRoot = "Assets/Models/KayKit";

        private const string CaveNamePrefix    = "CavePortal";       // CavePortal_0, _1, _2
        private const string DungeonNamePrefix = "DungeonPortal";    // DungeonPortal_0, _1
        private const string TriggerSuffix     = "_Trigger";         // CavePortal_0_Trigger, etc.

        // A FEW cave mouths at sensible OuterWorld shore spots (data-ish, not copy-paste).
        // Index 0 keeps the existing terminus the player reaches just past the seam (~z-150);
        // +1/+2 spread to the east and west shore. (CavePos history: owner parked the 600m far cave.)
        private static readonly Vector3[] CavePositions =
        {
            new Vector3(  0f, 0f, -150f),   // centre shore — existing terminus
            new Vector3(130f, 0f, -110f),   // east shore
            new Vector3(-130f, 0f, -110f),  // west shore
        };

        // A FEW dungeon portal entries — DISTINCT spots from the caves (owner: test the same entry
        // theory with KayKit-skinned portals). Placed nearer the seam approach, offset off the cave line.
        private static readonly Vector3[] DungeonPositions =
        {
            new Vector3( 70f, 0f, -70f),    // east-inner
            new Vector3(-70f, 0f, -70f),    // west-inner
        };

        [MenuItem("Defenders/World/Place OuterWorld Cave Portal")]
        public static void PlaceCavePortal()
        {
            FlowTrace.Step("CavePortal", $"PlaceCavePortal START — opening '{ScenePath}'");

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
            if (!scene.IsValid())
            {
                FlowTrace.Fail("CavePortal", $"could not open scene '{ScenePath}' — ABORT.");
                return;
            }

            // Parent: an OuterWorld/Exterior root if one exists, else scene root (null parent).
            Transform parent = FindRoot(scene);
            FlowTrace.Step("CavePortal",
                $"parent root = {(parent != null ? parent.name : "<scene root>")}");

            // Idempotent: destroy any prior cave/dungeon entrance + stray triggers (sweep by prefix).
            DestroyExisting(parent, scene);

            // CANON: the entrance behavior (cave->outpost resolver, portal->dungeon resolver) DOES NOT
            // EXIST YET. So in THIS bake we place the GEOMETRY only and gate the warp behavior OFF (the
            // flags default OFF). When a flag is OFF the geometry is placed but NO destination trigger is
            // attached — the entrance is inert. The flags read at bake time (default OFF -> inert bake).
            bool cavesGated     = FeatureFlags.OutpostCaves;     // false by default -> behavior inert
            bool dungeonsGated  = FeatureFlags.DungeonPortals;   // false by default -> behavior inert
            FlowTrace.Step("CavePortal",
                $"behavior gates: OutpostCaves={cavesGated}, DungeonPortals={dungeonsGated} " +
                "(resolver/loading-zone not built yet — geometry-only when OFF).");

            // ── Cave mouths (outpost entrances) ─────────────────────────────────
            for (int i = 0; i < CavePositions.Length; i++)
            {
                var cave = BuildCaveMouth(parent, i);
                cave.transform.position = CavePositions[i];
                MarkStatic(cave);
                AddPortalVfx(cave);

                if (cavesGated)
                {
                    BuildEntranceTrigger(cave, CavePositions[i], "outpost");
                }
                else
                {
                    FlowTrace.Step("CavePortal",
                        $"cave '{cave.name}' @ {CavePositions[i]} placed, behavior gated OFF (outpostcaves) — no trigger.");
                }
            }

            // ── Dungeon portals (KayKit-skinned, distinct spots) ────────────────
            for (int i = 0; i < DungeonPositions.Length; i++)
            {
                var portal = BuildDungeonPortal(parent, i);
                portal.transform.position = DungeonPositions[i];
                MarkStatic(portal);
                AddPortalVfx(portal);

                if (dungeonsGated)
                {
                    BuildEntranceTrigger(portal, DungeonPositions[i], "dungeon");
                }
                else
                {
                    FlowTrace.Step("CavePortal",
                        $"dungeon portal '{portal.name}' @ {DungeonPositions[i]} placed, behavior gated OFF (dungeonportals) — no trigger.");
                }
            }

            // ── Save ────────────────────────────────────────────────────────────
            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene);
            AssetDatabase.SaveAssets();

            FlowTrace.Step("CavePortal",
                $"DONE — {CavePositions.Length} cave mouth(s) + {DungeonPositions.Length} dungeon portal(s) placed " +
                $"(behavior gated: outpostcaves={cavesGated}, dungeonportals={dungeonsGated}), scene-saved={saved}.");
        }

        // Parent the portal under a root that SURVIVES a terrain rebuild. CRITICAL (2026-06-20):
        // ExteriorTerrainBuilder.BuildExterior NUKES its "ExteriorRoot" — if the cave is parented
        // there, a later BuildExterior DESTROYS it (owner: "the portal didn't spawn, we were at the
        // end"). So we (a) NEVER use ExteriorRoot/ExteriorTerrainRoot, and (b) loop candidates OUTER
        // so OuterWorldRoot (which BuildExterior preserves) wins regardless of scene root order.
        private static Transform FindRoot(Scene scene)
        {
            string[] candidates = { "OuterWorldRoot", "OuterWorld" };   // BuildExterior-safe roots only
            foreach (var name in candidates)
                foreach (var go in scene.GetRootGameObjects())
                    if (go != null && go.name == name) return go.transform;
            return null; // scene root (also survives BuildExterior; only ExteriorRoot is nuked)
        }

        private static void DestroyExisting(Transform parent, Scene scene)
        {
            // Sweep the WHOLE scene for any prior cave/dungeon entrance object or stray trigger,
            // matched by NAME PREFIX (CavePortal*, DungeonPortal*, *_Trigger) so a re-run leaves
            // exactly the freshly placed set — no orphan triggers lingering on the path.
            int swept = 0;
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go == null) continue;
                foreach (var t in go.GetComponentsInChildren<Transform>(true))
                {
                    if (t == null || t.gameObject == null) continue;
                    var n = t.gameObject.name;
                    if (n != null &&
                        (n.StartsWith(CaveNamePrefix) || n.StartsWith(DungeonNamePrefix) || n.EndsWith(TriggerSuffix)))
                    {
                        Object.DestroyImmediate(t.gameObject);
                        swept++;
                    }
                }
            }
            if (swept > 0)
                FlowTrace.Step("CavePortal", $"swept {swept} stale cave/dungeon entrance object(s) (idempotent re-run).");
        }

        // Instantiate the cave mouth (walk-in opening). Skip-safe two-stage: primary tunnel-entrance
        // prefab -> island-cave fallback -> tinted primitive cube, so a pack-less clone still builds.
        private static GameObject BuildCaveMouth(Transform parent, int index)
        {
            GameObject prefab = Guard.Try("CavePortal", "LoadCaveMouthPrefab",
                () => AssetDatabase.LoadAssetAtPath<GameObject>(CaveMouthPrefab), null);
            string usedPath = CaveMouthPrefab;
            if (prefab == null)
            {
                prefab = Guard.Try("CavePortal", "LoadCaveMouthFallbackPrefab",
                    () => AssetDatabase.LoadAssetAtPath<GameObject>(CaveMouthFallbackPrefab), null);
                if (prefab != null) usedPath = CaveMouthFallbackPrefab;
            }

            GameObject cave;
            if (prefab != null)
            {
                cave = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                FlowTrace.Step("CavePortal", $"cave mouth[{index}] = polyperfect '{usedPath}'.");
                // The tunnel-entrance / island-cave meshes are near real-world scale — a modest bump
                // reads as a sizeable, walk-in mouth (the old x4 was tuned for the solid boulder).
                cave.transform.localScale = Vector3.one * 1.5f;
            }
            else
            {
                Debug.LogWarning("[CavePortal] cave-mouth prefab '" + CaveMouthPrefab + "' (and fallback '" +
                    CaveMouthFallbackPrefab + "') not found (polyperfect pack may not be imported) — using a primitive placeholder cube.");
                FlowTrace.Warn("CavePortal", $"cave-mouth[{index}] prefab missing — primitive placeholder cube stand-in.");
                cave = GameObject.CreatePrimitive(PrimitiveType.Cube);
                if (parent != null) cave.transform.SetParent(parent, false);
                cave.transform.localScale = new Vector3(6f, 5f, 4f); // sizeable cave-mouth read
                TintMesh(cave, new Color(0.20f, 0.19f, 0.18f));       // dark stone
            }

            cave.name = CaveNamePrefix + "_" + index;
            return cave;
        }

        // Instantiate a KayKit-skinned dungeon portal. KayKit is gitignored and may be ABSENT on a
        // clean clone — skip-safe: search the pack for a door/arch/portal prefab; if none, build a dark
        // archway primitive placeholder + LogWarning (NOT error) so the build always succeeds.
        private static GameObject BuildDungeonPortal(Transform parent, int index)
        {
            GameObject prefab = LoadKayKitPortalPrefab(out string usedPath);

            GameObject portal;
            if (prefab != null)
            {
                portal = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                FlowTrace.Step("CavePortal", $"dungeon portal[{index}] = KayKit '{usedPath}'.");
                portal.transform.localScale = Vector3.one * 1.5f;
            }
            else
            {
                Debug.LogWarning("[CavePortal] no KayKit dungeon-portal prefab found under '" + KayKitRoot +
                    "' (pack gitignored / not present) — using a dark archway primitive placeholder.");
                FlowTrace.Warn("CavePortal", $"dungeon portal[{index}] — KayKit absent; dark archway primitive stand-in.");
                portal = BuildArchwayPlaceholder(parent);
            }

            portal.name = DungeonNamePrefix + "_" + index;
            return portal;
        }

        // Look for a dungeon door/arch/portal/gate prefab inside the (optional) KayKit pack.
        private static GameObject LoadKayKitPortalPrefab(out string usedPath)
        {
            string found = null;
            usedPath = Guard.Try("CavePortal", "LoadKayKitPortalPrefab", () =>
            {
                if (!AssetDatabase.IsValidFolder(KayKitRoot)) return null;
                string[] keys = { "door", "arch", "portal", "gate", "entrance", "dungeon" };
                foreach (var k in keys)
                {
                    var guids = AssetDatabase.FindAssets(k + " t:Prefab", new[] { KayKitRoot });
                    if (guids != null && guids.Length > 0)
                        return AssetDatabase.GUIDToAssetPath(guids[0]);
                }
                return null;
            }, null);

            found = usedPath;
            if (string.IsNullOrEmpty(found)) return null;
            return AssetDatabase.LoadAssetAtPath<GameObject>(found);
        }

        // A simple dark archway (two posts + a lintel) so the dungeon-entry read survives a pack-less clone.
        private static GameObject BuildArchwayPlaceholder(Transform parent)
        {
            var root = new GameObject("Archway");
            if (parent != null) root.transform.SetParent(parent, false);
            var stone = new Color(0.12f, 0.11f, 0.13f); // near-black dungeon stone

            void Post(float x)
            {
                var p = GameObject.CreatePrimitive(PrimitiveType.Cube);
                p.transform.SetParent(root.transform, false);
                p.transform.localScale = new Vector3(1f, 5f, 1f);
                p.transform.localPosition = new Vector3(x, 2.5f, 0f);
                TintMesh(p, stone);
            }
            Post(-2f);
            Post(2f);
            var lintel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lintel.transform.SetParent(root.transform, false);
            lintel.transform.localScale = new Vector3(5f, 1f, 1f);
            lintel.transform.localPosition = new Vector3(0f, 5.5f, 0f);
            TintMesh(lintel, stone);

            return root;
        }

        // PortalVFXController self-bootstraps its glow/light/vortex on Start, so a
        // bare AddComponent is enough. Reflection because the Editor asmdef cannot
        // reference DeNelle.Village.
        private static void AddPortalVfx(GameObject cave)
        {
            var vfxType = FindType("DeNelle.Village.PortalVFXController");
            if (vfxType == null)
            {
                FlowTrace.Warn("CavePortal",
                    "DeNelle.Village.PortalVFXController not found — cave placed WITHOUT glow. Re-run after compile.");
                return;
            }
            Guard.Try("CavePortal", "AddPortalVFXController", () =>
            {
                if (cave.GetComponent(vfxType) == null) cave.AddComponent(vfxType);
                return true;
            }, false);
            FlowTrace.Step("CavePortal", "PortalVFXController attached (self-builds glow/light/vortex at runtime).");
        }

        // Child trigger — ONLY built when the matching behavior flag is ON. CANON: the loading-zone /
        // resolver that an entrance should warp into DOES NOT EXIST YET, so even when the flag is ON we
        // attach the SceneTransitionTrigger with NO destination (no targetSceneName) and DISABLED, so it
        // is inert until the resolver slice wires a real target. We DO NOT load Village2 — that stale
        // direct-load path is intentionally removed. When the flag is OFF this method is never called
        // (geometry-only bake). `kind` is "outpost" | "dungeon" for the prompt label / trace.
        private static void BuildEntranceTrigger(GameObject entrance, Vector3 entrancePos, string kind)
        {
            var trig = new GameObject(entrance.name + TriggerSuffix);
            // Parent under the entrance's PARENT (the UNSCALED root), NOT the scaled entrance — parenting
            // under a scaled object multiplies the local offset and floats the trigger off the navmesh
            // (fleet 2026-06-19 SEAM-OFF-MESH). Seat it at GROUND level on the approach path, ~16m on the
            // +Z (approach) side of the mouth, in unscaled world space.
            trig.transform.SetParent(entrance.transform.parent, false);
            trig.transform.position = new Vector3(entrancePos.x, 1f, entrancePos.z + 16f);

            var box = trig.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(12f, 6f, 8f);   // unscaled: a 12x6x8m volume on the path

            var transType = FindType("DeNelle.Village.SceneTransitionTrigger");
            if (transType == null)
            {
                FlowTrace.Warn("CavePortal",
                    "DeNelle.Village.SceneTransitionTrigger NOT FOUND — trigger collider added WITHOUT the " +
                    "behaviour. Re-run after compile.");
                return;
            }

            Guard.Try("CavePortal", "AttachInertSceneTransitionTrigger", () =>
            {
                var comp = trig.AddComponent(transType);
                // NO targetSceneName / targetPosition — the resolver/loading-zone is not built yet.
                // Disable the component so it can NEVER fire until the resolver slice wires a real target.
                var beh = comp as Behaviour;
                if (beh != null) beh.enabled = false;
                SetField(transType, comp, "ProximityRadius", 16f);
                SetField(transType, comp, "promptOverride",
                    kind == "dungeon" ? "Enter the dungeon" : "Enter the outpost");
                return true;
            }, false);

            FlowTrace.Step("CavePortal",
                $"entrance trigger '{trig.name}' ({kind}) attached but INERT — no destination, component disabled " +
                $"(resolver not built). Trigger world pos = {trig.transform.position}.");
        }

        // ── shared helpers (mirror EnemyStrongholdBuilder) ──────────────────────
        private static void MarkStatic(GameObject go)
        {
            GameObjectUtility.SetStaticEditorFlags(go,
                StaticEditorFlags.NavigationStatic | StaticEditorFlags.BatchingStatic |
                StaticEditorFlags.OccluderStatic | StaticEditorFlags.OccludeeStatic);
        }

        private static void TintMesh(GameObject go, Color c)
        {
            var mr = go.GetComponent<MeshRenderer>();
            if (mr == null) return;
            var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) return;
            var mat = new Material(shader);
            if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", c); else mat.color = c;
            mr.sharedMaterial = mat;
        }

        private static void SetField(System.Type type, Object comp, string fieldName, object value)
        {
            var f = type.GetField(fieldName,
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (f != null) f.SetValue(comp, value);
            else FlowTrace.Warn("CavePortal", "field '" + fieldName + "' not found on " + type.Name + " — skipped.");
        }

        private static System.Type FindType(string fullName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }
    }
}
