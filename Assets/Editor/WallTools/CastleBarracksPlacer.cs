// =============================================================================
// CastleBarracksPlacer — drops the Military Barracks (the troop-training hub
// building, WO-453) into MainCastle_Hall, in the middle NEAR the hero spawn
// (owner 2026-06-14). Reproducible + idempotent (re-run repositions; delete the
// 'CastleBarracks' root to remove) — we don't hand-edit scenes.
//
// Prefab: polyperfect _M tier (CLAUDE.md §4 — always _M). Gitignored pack, so a
// missing prefab is a LogWarning, not a crash (re-import via Defenders/Art).
// Spawn anchor: the authored 'HeroStartPoint_PlayerSpawn' GameObject; the barracks
// sits a few metres to the side + toward centre so it's by the spawn but off the path.
//
// Batchmode: DeNelle.Editor.CastleBarracksPlacer.PlaceInCastle
// Menu:      Defenders/Castle/Place Barracks near Spawn
// =============================================================================
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using DeNelle.Core.Diagnostics; // FlowTrace — name the placed object + its collider bounds (CLAUDE.md S12)

namespace DeNelle.Editor
{
    public static class CastleBarracksPlacer
    {
        private const string CastleScene  = "Assets/Scenes/MainCastle_Hall.unity";
        private const string BarracksPrefab =
            "Assets/polyperfect/Low Poly Ultimate Pack/_M/Prefabs_M/Military_M/Military_Barracks.prefab";
        private const string SpawnAnchorName = "HeroStartPoint_PlayerSpawn";
        private const string RootName = "CastleBarracks";
        // Shrink factor (owner 2026-06-14: "shrink the size of the barracks"). The polyperfect
        // Military_Barracks ships large; 0.6 brings it to building scale near the spawn. The
        // owner can hand-nudge scale/position after — the runtime NPC injector keys off the
        // root NAME, so the Drillmaster follows wherever the barracks ends up.
        private const float BarracksScale = 0.6f;
        // Y-only stretch (owner 2026-06-23: "scale the barracks 1.5x on the Y axis only" —
        // taller, same footprint). Applied to localScale.y AFTER the uniform shrink and
        // BEFORE the bounds ground-seat, so the base re-seats on the floor automatically.
        private const float BarracksHeightScale = 1.5f;
        // Offset from the spawn: BESIDE + slightly BEHIND the spawn, OFF the central
        // spawn->Heart corridor. INVISIBLE-ITEM FIX (F8 flag, owner 2026-06-24): the old
        // (6,0,4) put the barracks DIRECTLY in the spawn(0,0,0)->Tree/Heart(0,0,12) walk
        // lane, where its full mesh-shaped MeshCollider body-blocked the hero next to the
        // tree (the "invisible item blocking me here" grey box in the F8 screenshot). The
        // "off the path" claim was wrong for this spawn->tree axis. Move it well to the
        // side (+16 X) and behind the spawn (-4 Z) so it is adjacent to the spawn but clear
        // of the corridor; the open band there has no storefront/wall (nearest is Jeweler at
        // (18.3,-35) and Forge at (-27.4,-7.45)).
        private static readonly Vector3 SpawnOffset = new Vector3(16f, 0f, -4f);

        [MenuItem("Defenders/Castle/Place Barracks near Spawn")]
        public static void PlaceInCastle()
        {
            var scene = EditorSceneManager.OpenScene(CastleScene, OpenSceneMode.Single);

            var prior = GameObject.Find(RootName);
            if (prior != null) Object.DestroyImmediate(prior);

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BarracksPrefab);
            if (prefab == null)
            {
                Debug.LogWarning($"[CastleBarracksPlacer] prefab missing at {BarracksPrefab} " +
                                 "(polyperfect pack not imported?) — nothing placed.");
                return;
            }

            // Anchor on the authored spawn marker; fall back to a sane spot near it.
            var anchor = GameObject.Find(SpawnAnchorName);
            Vector3 spawnPos = anchor != null ? anchor.transform.position : new Vector3(0f, 0f, -11f);
            Vector3 pos = spawnPos + SpawnOffset;

            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            if (go == null) go = Object.Instantiate(prefab);
            go.name = RootName;
            go.transform.localScale *= BarracksScale; // shrink to building scale (before bounds-seat)
            // Y-only stretch: taller, same X/Z footprint. Bounds ground-seat below re-corrects the base.
            var s = go.transform.localScale;
            go.transform.localScale = new Vector3(s.x, s.y * BarracksHeightScale, s.z);
            // Face roughly toward castle centre (so the door reads toward the plaza).
            var toCentre = new Vector3(-pos.x, 0f, -pos.z);
            if (toCentre.sqrMagnitude > 0.01f)
                go.transform.rotation = Quaternion.LookRotation(toCentre.normalized, Vector3.up);
            go.transform.position = pos;

            // Ground-seat: drop so the model's base sits at the spawn's floor height.
            var rends = go.GetComponentsInChildren<Renderer>(true);
            if (rends.Length > 0)
            {
                var b = rends[0].bounds;
                for (int k = 1; k < rends.Length; k++) b.Encapsulate(rends[k].bounds);
                go.transform.position += new Vector3(0f, spawnPos.y - b.min.y, 0f);
                Debug.Log($"[CastleBarracksPlacer] barracks world size after {BarracksScale}x scale = " +
                          $"{b.size.x:F1} x {b.size.y:F1} x {b.size.z:F1} m (W x H x D).");
            }

            // INVISIBLE-ITEM FIX instrumentation (CLAUDE.md S12): name the placed object + its
            // blocking collider bounds so the next run PROVES the barracks no longer overlaps the
            // spawn->Heart corridor (spawn at world (0,0,0), Heart/Tree at (0,0,12)). ASCII-only.
            var col = go.GetComponentInChildren<Collider>(true);
            if (col != null)
            {
                var cb = col.bounds;
                FlowTrace.Step("Hub", $"CastleBarracks placed at {go.transform.position} " +
                    $"colliderBounds center={cb.center} size={cb.size} " +
                    $"(corridor x[-3,3] z[-2,14] must be clear of this box).");
            }
            else
            {
                FlowTrace.Warn("Hub", $"CastleBarracks placed at {go.transform.position} but has NO collider " +
                    "(it cannot body-block, but verify it still renders).");
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log($"[CastleBarracksPlacer] placed '{RootName}' at {go.transform.position} " +
                      $"(spawn {spawnPos} + offset {SpawnOffset}) in {CastleScene}.");
        }
    }
}
