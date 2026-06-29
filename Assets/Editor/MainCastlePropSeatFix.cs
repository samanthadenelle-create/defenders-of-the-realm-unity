// =============================================================================
// MainCastlePropSeatFix — durable fix for the MainCastle_Hall "floating props"
// CLASS bug, applied WITHOUT hand-editing the scene YAML (CLAUDE.md §3).
//
// PROVEN ROOT (data RCA): the courtyard floor is dropped to y=-0.5 at runtime
// (GroundZFightFixer.TargetY=-0.5; MainCastleFloorFix seats the big floor at -0.55)
// but the hand-placed decorative props were authored when the floor sat at ~y=0.
// So the Well (y=-0.18), Anvil (-0.34), StorefrontCrate x4 (0), the storefronts
// (Forge/Jeweler at 0), Dungeon_Stairs_Stone, CornerTower_South (0.04),
// ArcaneTower_MagicUpgrades (0.05), Marketplace_Monetization (-0.06), etc. all
// HOVER above the -0.5 floor. Tree_Of_Life already self-corrects because it carries
// SeatOnGroundOnStart (Player.log "[SeatOnGround] Tree final position") — proof the
// component works. This pass attaches that SAME proven component to the whole float
// class so each prop self-seats by its live renderer bounds at Play, robust to any
// future floor-Y change or art swap. No new seating system is invented.
//
// SeatOnGroundOnStart config: groundY fallback = -0.5 (the courtyard floor Y),
// raycastGround = true. (Note: because every prop's transform.root is CastleHubRoot,
// SeatOnGroundOnStart's self-skip ignores the floor collider under that root, so the
// raycast harmlessly falls through to the -0.5 fallback — i.e. props seat to exactly
// the floor Y, deterministically.)
//
// INCLUSION RULE (name allow-list + outermost-only de-dup): walk the entire
// CastleHubRoot subtree; a node is seated iff its name matches a decorative-prop
// allow-list (exact/prefix) AND it has a Renderer in its subtree AND none of its
// ancestors already matched (we seat only the OUTERMOST matching prop so a parent
// storefront and its child crate are never double-seated — the parent seats the whole
// group as one unit by combined bounds). A defensive exclude guard also drops any
// floor/nav/NPC/tree/light/camera/trigger node. Idempotent: skips props that already
// have the component.
//
// Run (EDITOR CLOSED, batchmode):
//   -executeMethod DeNelle.Editor.MainCastlePropSeatFix.Run
// =============================================================================
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Editor
{
    public static class MainCastlePropSeatFix
    {
        private const string ScenePath = "Assets/Scenes/MainCastle_Hall.unity";
        private const string RootName  = "CastleHubRoot";
        private const float  FloorY    = -0.5f;   // courtyard floor Y (GroundZFightFixer.TargetY)
        private const string SeatTypeName = "DeNelle.Village.SeatOnGroundOnStart";

        // Decorative-prop allow-list (case-insensitive; matched by exact name OR prefix).
        // Drawn directly from the data RCA's floating class. Storefronts are seated at
        // their ROOT (outermost), which carries the whole building + its child anvil/crate.
        private static readonly string[] IncludePrefixes =
        {
            "Well",
            "Anvil",
            "StorefrontCrate",
            "StorefrontVine",
            "VineDecor",
            "Dungeon_Stairs",
            "CornerTower",
            "ArcaneTower_MagicUpgrades",
            "Blacksmith_Weapons_Storefront",
            "Lumbermill_Wood_Storefront",
            "Windmill_Food_Storefront",
            "Forge_Armor_Storefront",
            "Jeweler_Gems_Storefront",
            "Marketplace_Monetization",
        };

        // Defensive exclude guard — never seat structural/system nodes even if a future
        // allow-list entry overlaps them. (Floors, navmesh, NPC bodies which self-seat via
        // NpcGroundSeat, the Tree which already has the component, lights, cameras, triggers.)
        private static readonly string[] ExcludeContains =
        {
            "Floor", "Nav", "Plaza", "NPC", "Npc", "Vendor", "Companion", "Townsfolk",
            "Tree_Of_Life", "Light", "Camera", "Trigger", "Gate", "Connector", "Wall",
            "HeroStart", "Spawn", "Heart", "Ambience",
        };

        [MenuItem("Defenders/Castle/Seat Floating Props (SeatOnGroundOnStart)")]
        public static void Run()
        {
            Log("=== MainCastle prop-seat fix START ===");

            var seatType = FindType(SeatTypeName);
            if (seatType == null)
            {
                Err($"Could not resolve type '{SeatTypeName}' — is DeNelle.Village compiled? Aborting.");
                return;
            }

            var scene = EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);

            var root = FindRoot(scene, RootName);
            if (root == null)
            {
                Err($"'{RootName}' not found in '{ScenePath}'. Nothing seated.");
                return;
            }

            // Collect every node in the subtree that matches the allow-list, is visible, and
            // is not excluded. Then keep only the OUTERMOST match per branch (skip a node if
            // any ancestor up to the root also matched) so we never double-seat nested props.
            var allTransforms = root.GetComponentsInChildren<Transform>(true);
            var matched = new HashSet<Transform>();
            foreach (var t in allTransforms)
            {
                if (t == root.transform) continue;
                if (!IsIncluded(t.name)) continue;
                if (IsExcluded(t.name)) continue;
                if (t.GetComponentInChildren<Renderer>(true) == null) continue; // visible only
                matched.Add(t);
            }

            var outermost = new List<Transform>();
            foreach (var t in matched)
            {
                if (HasMatchedAncestor(t, root.transform, matched)) continue;
                outermost.Add(t);
            }

            int seated = 0, already = 0;
            var seatedNames = new List<string>();
            foreach (var t in outermost)
            {
                if (t.GetComponent(seatType) != null) { already++; continue; }

                var comp = t.gameObject.AddComponent(seatType);
                if (comp == null)
                {
                    Warn($"AddComponent(SeatOnGroundOnStart) returned null on '{t.name}' — skipped.");
                    continue;
                }

                // Configure serialized fields: groundY fallback = -0.5, raycastGround = true.
                var so = new SerializedObject(comp);
                var pGroundY  = so.FindProperty("_groundY");
                var pRaycast  = so.FindProperty("_raycastGround");
                if (pGroundY != null) pGroundY.floatValue = FloorY;
                if (pRaycast != null) pRaycast.boolValue  = true;
                so.ApplyModifiedPropertiesWithoutUndo();

                seated++;
                seatedNames.Add(t.name);
            }

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();

            Log($"Seated {seated} prop(s) (already had component: {already}). Saved={saved}.");
            if (seatedNames.Count > 0)
                Log("Newly seated: " + string.Join(", ", seatedNames));
            Log("=== MainCastle prop-seat fix DONE — open MainCastle_Hall + Play; props drop onto the -0.5 floor ===");
        }

        // True if name equals or starts with any allow-list prefix (case-insensitive).
        private static bool IsIncluded(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            foreach (var p in IncludePrefixes)
                if (name.StartsWith(p, System.StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        // Defensive: never seat a structural/system node (overrides include on overlap).
        private static bool IsExcluded(string name)
        {
            if (string.IsNullOrEmpty(name)) return true;
            // Storefronts legitimately contain "Storefront"; allow them through even though
            // their child NPC bodies are excluded by name. Only block on the structural tokens.
            foreach (var x in ExcludeContains)
                if (name.IndexOf(x, System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    // Allow the storefront ROOTS (they end with "_Storefront" / are upgrade towers /
                    // marketplace) — those are in the include list and are the props we WANT to seat.
                    if (IsIncluded(name) && !LooksLikeNpcOrSystem(name)) return false;
                    return true;
                }
            return false;
        }

        // A name that is BOTH in the allow-list and clearly an NPC/system node (defensive).
        private static bool LooksLikeNpcOrSystem(string name)
        {
            return name.IndexOf("NPC", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Npc", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Vendor", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Companion", System.StringComparison.OrdinalIgnoreCase) >= 0
                || name.IndexOf("Townsfolk", System.StringComparison.OrdinalIgnoreCase) >= 0;
        }

        // True if any ancestor (up to and including the search root) is also in the matched set.
        private static bool HasMatchedAncestor(Transform t, Transform root, HashSet<Transform> matched)
        {
            for (var p = t.parent; p != null && p != root; p = p.parent)
                if (matched.Contains(p)) return true;
            return false;
        }

        // Find the named root (including inactive) among the scene's root objects.
        private static GameObject FindRoot(Scene scene, string name)
        {
            foreach (var go in scene.GetRootGameObjects())
            {
                if (go.name == name) return go;
                var t = go.transform.Find(name);
                if (t != null) return t.gameObject;
            }
            return GameObject.Find(name);
        }

        // Cross-assembly type lookup (same pattern as CastleHubBuilder.FindType).
        private static System.Type FindType(string fullName)
        {
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        private static void Log(string m)  => Debug.Log("[MainCastlePropSeatFix] " + m);
        private static void Warn(string m) => Debug.LogWarning("[MainCastlePropSeatFix] " + m);
        private static void Err(string m)  => Debug.LogError("[MainCastlePropSeatFix] " + m);
    }
}
