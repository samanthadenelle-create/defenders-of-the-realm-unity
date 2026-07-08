// =============================================================================
// WorldSceneLoader — DEPRECATED: OuterWorld scene removed (WO-608 MergedWorld)
// =============================================================================
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// With MergedWorld ON, the castle + overworld are merged into Main_Castle_Overworld.
// OuterWorld no longer exists. This loader is kept for compatibility but does nothing.
// All world loading is now handled by HubScenes.IsOverworld() and the merged scene.
// =============================================================================
using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    public static class WorldSceneLoader
    {
        private const string VillageSceneName    = "Village2";
        // OuterWorld scene removed (WO-608 MergedWorld) — use HubScenes.IsOverworld() instead

        // Hub scenes (Village2 / MainCastle_Hall / CastleHub*) now come from the ONE shared
        // source DeNelle.Core.HubScenes — the same list VillageHudController reads, so the HUD's
        // town context + this loader can never drift again (WO-411 root cause A).

        // BUILD FIX (WO-173/DEF-108): a one-shot AfterSceneLoad check FAILED in player
        // builds. AfterSceneLoad fires when the BOOT scene (Title) is active — not Village
        // — so the old `active.name != "Village"` early-return ran during Title, set its
        // guard, and the overworld was NEVER loaded once the player reached the village
        // (Title -> HeroSelect -> PetSelect -> Village). It only "worked" in the editor
        // because you press Play directly on Village. Fix: subscribe to sceneLoaded and
        // bring the overworld in WHENEVER the Village scene loads, in any flow.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetGuard() => SceneManager.sceneLoaded -= OnSceneLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;   // de-dupe across domain reloads
            SceneManager.sceneLoaded += OnSceneLoaded;
            FlowTrace.Step("World", $"Init (AfterSceneLoad): subscribed to sceneLoaded; active scene='{SceneManager.GetActiveScene().name}'.");
            Debug.Log("[WorldSceneLoader] DEBUG Init fired — subscribed to sceneLoaded. " +
                "Active scene now = '" + SceneManager.GetActiveScene().name + "'. Watching for '" +
                VillageSceneName + "'. (DEF-108 diagnostic)");
            // Handle the case where Village is already the active scene right now.
            TryLoadOuterWorld(SceneManager.GetActiveScene(), "Init-active");
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            FlowTrace.Step("World", $"sceneLoaded '{scene.name}' (mode={mode}).");
            Debug.Log("[WorldSceneLoader] DEBUG sceneLoaded event: '" + scene.name +
                "' (mode=" + mode + ").");
            if (DeNelle.Core.HubScenes.IsOverworld(scene.name))
            {
                FlowTrace.Step("World", $"'{scene.name}' is overworld — running terrain diagnostics.");
                DiagTerrain(scene);
            }
            TryLoadOuterWorld(scene, "sceneLoaded");
        }

        // DEF-108 diagnostic: when OuterWorld loads, log the terrain's actual RUNTIME state
        // so we can see why it renders black (stripped shader -> InternalErrorShader, no
        // layers, drawHeightmap off, wrong position, or inactive). Remove once resolved.
        private static void DiagTerrain(Scene scene)
        {
            foreach (var root in scene.GetRootGameObjects())
            {
                var t = root.GetComponentInChildren<Terrain>(true);
                if (t == null) continue;
                var mt = t.materialTemplate;
                var td = t.terrainData;
                Debug.Log("[WorldSceneLoader] TERRAINDIAG '" + t.name +
                    "' active=" + t.gameObject.activeInHierarchy + " enabled=" + t.enabled +
                    " drawHeightmap=" + t.drawHeightmap + " layer=" + t.gameObject.layer +
                    " pos=" + t.transform.position +
                    " material='" + (mt != null ? mt.name : "NULL") +
                    "' shader='" + (mt != null && mt.shader != null ? mt.shader.name : "NULL") +
                    "' terrainLayers=" + (td != null && td.terrainLayers != null ? td.terrainLayers.Length : -1) +
                    " size=" + (td != null ? td.size.ToString() : "?"));
                if (td != null && td.terrainLayers != null)
                    for (int i = 0; i < td.terrainLayers.Length; i++)
                        Debug.Log("[WorldSceneLoader] TERRAINDIAG   layer" + i + "='" +
                            (td.terrainLayers[i] != null ? td.terrainLayers[i].name : "NULL") +
                            "' diffuse=" + (td.terrainLayers[i] != null && td.terrainLayers[i].diffuseTexture != null ? "set" : "NULL"));
                if (t.drawInstanced) t.drawInstanced = false;   // ruled out, keep off anyway
                // DEF-108 probe: terrain is correct (shader/layers/pos) yet black -> LIGHTING + SPLAT.
                Debug.Log("[WorldSceneLoader] TERRAINDIAG ambientMode=" + RenderSettings.ambientMode +
                    " ambientLight=" + RenderSettings.ambientLight + " ambientIntensity=" + RenderSettings.ambientIntensity +
                    " skybox='" + (RenderSettings.skybox != null ? RenderSettings.skybox.name : "NULL") + "'");
                foreach (var L in UnityEngine.Object.FindObjectsByType<Light>())
                    Debug.Log("[WorldSceneLoader] TERRAINDIAG light '" + L.name + "' type=" + L.type +
                        " intensity=" + L.intensity + " on=" + (L.enabled && L.gameObject.activeInHierarchy) +
                        " scene='" + L.gameObject.scene.name + "'");
                if (td != null && td.alphamapLayers > 0)
                {
                    var c = td.GetAlphamaps(td.alphamapWidth / 2, td.alphamapHeight / 2, 1, 1);
                    float sum = 0f;
                    for (int k = 0; k < td.alphamapLayers; k++) sum += c[0, 0, k];
                    if (sum < 0.01f)
                    {
                        // DEF-108 FIX: the baked splatmap did NOT persist into the player build
                        // (all-zero weights -> terrain renders pure black). Repaint at runtime.
                        // Per-quadrant BIOMES so the 4 elemental regions read as distinct ground:
                        //   E Goldfields=grass(0) / W Stoneback=stone(1) / S Mirewood=mud(2) /
                        //   N Ashwood=dead-ash(4). Weighted by how far into each quadrant the point
                        //   sits, normalized -> smooth blends at the seams. (Full slope/height biome
                        //   variety + scenery to be restored via the bake-side persistence fix + WO-142.)
                        int aw = td.alphamapWidth, ah = td.alphamapHeight, layers = td.alphamapLayers;
                        var fill = new float[ah, aw, layers];
                        for (int z = 0; z < ah; z++)
                        {
                            // WO-468 Phase 1: terrain enlarged 300 -> 1000 (edge ±500).
                            float worldZ = (ah <= 1) ? 0f : (z / (float)(ah - 1)) * 1000f - 500f;
                            for (int x = 0; x < aw; x++)
                            {
                                float worldX = (aw <= 1) ? 0f : (x / (float)(aw - 1)) * 1000f - 500f;
                                float wGrass = Mathf.Max(0f, worldX);    // east  -> Goldfields
                                float wStone = Mathf.Max(0f, -worldX);   // west  -> Stoneback
                                float wDead  = Mathf.Max(0f, worldZ);    // north -> Ashwood
                                float wMud   = Mathf.Max(0f, -worldZ);   // south -> Mirewood
                                float total  = wGrass + wStone + wDead + wMud;
                                if (total < 0.001f) { fill[z, x, 0] = 1f; continue; }  // village centre -> grass
                                fill[z, x, 0] = wGrass / total;
                                if (layers > 1) fill[z, x, 1] = wStone / total;
                                if (layers > 2) fill[z, x, 2] = wMud / total;
                                if (layers > 4) fill[z, x, 4] = wDead / total;
                            }
                        }
                        td.SetAlphamaps(0, 0, fill);
                        Debug.Log("[WorldSceneLoader] TERRAINDIAG splat was EMPTY -> repainted PER-QUADRANT biomes (E grass / W stone / S mud / N dead-ash) so the 4 regions read distinct.");
                    }
                    else
                    {
                        Debug.Log("[WorldSceneLoader] TERRAINDIAG splat OK (center sum=" + sum + ").");
                    }
                }
                // DEF-108 bump probe: actual terrain surface Y at the village + just outside,
                // vs the village floor (hero spawns ~Y=0.03). Reveals the step/lip at the edge.
                float baseY = t.transform.position.y;
                int[] xs = { 0, 30, 42, 50, 70 };
                foreach (int d in xs)
                    Debug.Log("[WorldSceneLoader] TERRAINDIAG surfaceY @x=" + d + " -> " +
                        (baseY + t.SampleHeight(new Vector3(d, 0f, 0f))).ToString("0.000") +
                        "  @z=" + d + " -> " +
                        (baseY + t.SampleHeight(new Vector3(0f, 0f, d))).ToString("0.000"));
                return;
            }
            FlowTrace.Warn("World", $"DiagTerrain: no Terrain found in overworld scene '{scene.name}' — nothing to diagnose.");
            Debug.Log("[WorldSceneLoader] TERRAINDIAG no Terrain found in OuterWorld!");
        }

        private const string MergedWorldSceneName = "Main_Castle_Overworld";

        private static void TryLoadOuterWorld(Scene scene, string via)
        {
            // WO-608: OuterWorld scene has been DELETED. All world content is now in Main_Castle_Overworld.
            // This method is kept for compatibility but does nothing. The merged single scene ALREADY
            // contains all overworld content in-scene (welded into one continuous navmesh by WorldMergeBuilder).
            // No additive loading is needed or possible.
            FlowTrace.Step("World", $"TryLoadOuterWorld({via}): DEPRECATED no-op — OuterWorld removed (WO-608 MergedWorld); content is in-scene ('{MergedWorldSceneName}'), no additive stream.");
            Debug.Log("[WorldSceneLoader] (" + via + ") DEPRECATED: OuterWorld scene removed (WO-608 MergedWorld). " +
                "All world content is in Main_Castle_Overworld. No streaming loader needed.");
        }
    }
}
