// CastleWalkable.cs — drop a controllable, human-scale hero + follow camera + input
// into Assets/Scenes/CastleTest.unity so the owner can WALK THROUGH the castle at
// human scale. Owner-directed (2026-06-07): "I want to walk through the CastleTest
// scene at human scale."
//
// REUSES the proven Village playable wiring verbatim — no reinvented locomotion or
// camera. It calls Village2Playable's public, scene-agnostic builders directly:
//   - Village2Playable.AddSceneDefaultsToActiveScene() — Main Camera (VillageCamera +
//     SmartMobileCamera) + Directional Light + ambient/fog
//   - Village2Playable.ImportEventSystem()             — EventSystem (+ Input UI module)
//   - Village2Playable.ImportHero(root, heart)         — People Mage body (~2 m) +
//     HeroBodySwapper/HeroLocomotion/HeroAbilities/HeroAbilityInput, tagged "Player"
//   - Village2Playable.WireCameraTargetToHero(hero)    — smart camera _target -> hero
//
// Editor-only (DeNelle.Editor asmdef). Like Village2Playable it cannot reference
// Assembly-CSharp, so it leans on Village2Playable for the reflection-based component
// adds (build tooling exemption, same as VillageSceneBuilder).
//
// Run:
//   In-editor : Defenders/Sandbox/Make CastleTest Walkable
//   Batchmode : -executeMethod DeNelle.Editor.CastleWalkable.MakeWalkable

using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Editor
{
    public static class CastleWalkable
    {
        const string CastleScenePath = "Assets/Scenes/CastleTest.unity";

        [MenuItem("Defenders/Sandbox/Make CastleTest Walkable")]
        public static void MakeWalkable()
        {
            Log("=== Make CastleTest walkable START ===");

            // --- 1. Open CastleTest (Single) — or reuse it if it's already active. ---
            Scene scene = SceneManager.GetActiveScene();
            if (scene.path != CastleScenePath)
            {
                if (!File.Exists(CastleScenePath))
                {
                    Fail($"{CastleScenePath} does not exist — nothing to make walkable.");
                    return;
                }
                scene = EditorSceneManager.OpenScene(CastleScenePath, OpenSceneMode.Single);
            }
            Log($"Active scene '{scene.name}' ({scene.rootCount} roots).");

            // --- 2. Scene defaults: Main Camera (SmartMobileCamera) + Directional Light
            //        + ambient/fog. Idempotent (skips anything already present). ---
            Village2Playable.AddSceneDefaultsToActiveScene();

            // --- 3. EventSystem (+ new-Input-System UI module) so input routes. ---
            Village2Playable.ImportEventSystem();

            // --- 4. Build/import the SAME village hero (human-scale ~2 m) with its
            //        locomotion + abilities, tagged "Player". CastleTest has no Heart,
            //        so the Healing-Beacon target is null (ImportHero null-guards it). ---
            Transform parent = ResolveHeroParent(scene);
            GameObject hero = Village2Playable.ImportHero(parent, /*heart:*/ null);
            if (hero == null) { Fail("ImportHero returned null — hero not built."); return; }

            // --- 5. Spawn the hero in the courtyard, just inside the gate, ON the floor.
            //        Measure the REAL floor bounds (robust to whatever was built) rather
            //        than assume the default 8x3 layout: ground on floor-top, sit near
            //        the -Z (gate) edge one tile in, facing +Z toward the courtyard centre. ---
            PlaceHeroInCourtyard(scene, hero);

            // --- 6. Wire the smart follow camera's _target -> the hero. ---
            Village2Playable.WireCameraTargetToHero(hero);

            // --- 7. Save. ---
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene, CastleScenePath);

            Vector3 p = hero.transform.position;
            Log($"CASTLE_WALKABLE_OK heroAt=({p.x:F2}, {p.y:F2}, {p.z:F2})");
            Log("=== Make CastleTest walkable DONE — open CastleTest.unity and press Play (WASD + mouse) ===");
        }

        // The hero parent: reuse a top-level castle root if present, else parent to the
        // first scene root, else leave at scene root. ImportHero positions via the parent
        // (SetParent(..., false)), then PlaceHeroInCourtyard sets the final world position.
        static Transform ResolveHeroParent(Scene scene)
        {
            var roots = scene.GetRootGameObjects();
            foreach (var r in roots)
                if (r.name.Contains("Castle") || r.name.Contains("Courtyard"))
                    return r.transform;
            return roots.Length > 0 ? roots[0].transform : null;
        }

        // Grounds the hero on the real floor and seats it just inside the gate.
        // Spawn = (floorCentreX, floorTopY, floorMinZ + tile), facing +Z toward centre.
        // Falls back to the default-layout spawn (0, 0, -9) if no floor renderers found.
        static void PlaceHeroInCourtyard(Scene scene, GameObject hero)
        {
            const float defaultTile = 3f;

            if (TryFloorBounds(scene, out Bounds fb))
            {
                float tile = Mathf.Max(defaultTile, fb.size.x / 17f); // 8x3 layout = 17 tiles wide; clamp to >= 3
                float floorTopY = fb.max.y;                            // ground ON the floor surface
                float centreX = fb.center.x;
                float entryZ = fb.min.z + tile;                        // one tile in from the gate (-Z) edge
                // Keep the entry inside the courtyard span.
                entryZ = Mathf.Min(entryZ, fb.center.z);

                hero.transform.position = new Vector3(centreX, floorTopY, entryZ);
                hero.transform.rotation = Quaternion.Euler(0f, 0f, 0f); // face +Z toward the courtyard centre
                Log($"Hero placed on floor: bounds center={fb.center} size={fb.size} top={floorTopY:F3} " +
                    $"-> spawn=({centreX:F2}, {floorTopY:F2}, {entryZ:F2}), tile~{tile:F2}.");
            }
            else
            {
                hero.transform.position = new Vector3(0f, 0f, -9f);
                hero.transform.rotation = Quaternion.Euler(0f, 0f, 0f); // face +Z
                Warn("No floor renderers found — using fallback spawn (0, 0, -9) facing +Z.");
            }
        }

        // Union of renderer bounds for the castle FLOOR tiles (named "Floor"), so the
        // hero grounds on the real floor surface regardless of how the scene was built.
        static bool TryFloorBounds(Scene scene, out Bounds bounds)
        {
            bounds = default;
            bool have = false;
            foreach (var root in scene.GetRootGameObjects())
            {
                foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                {
                    if (r == null) continue;
                    if (!NameOrAncestorContains(r.transform, "Floor")) continue;
                    if (!have) { bounds = r.bounds; have = true; }
                    else bounds.Encapsulate(r.bounds);
                }
            }
            return have;
        }

        static bool NameOrAncestorContains(Transform t, string fragment)
        {
            for (Transform cur = t; cur != null; cur = cur.parent)
                if (cur.name.Contains(fragment)) return true;
            return false;
        }

        static void Log(string m)  => Debug.Log("[CastleWalkable] " + m);
        static void Warn(string m) => Debug.LogWarning("[CastleWalkable] " + m);
        static void Fail(string m) => Debug.LogError("[CastleWalkable] CASTLE_WALKABLE_FAIL " + m);
    }
}
