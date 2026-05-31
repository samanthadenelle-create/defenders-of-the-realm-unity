// =============================================================================
// WorldSceneLoader — additively loads the OuterWorld scene over the Village so
// the town + the surrounding regions form one continuous world at runtime
// (the owner's "two scenes existing together" model).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHY additive: Village.unity (castle/town, VillageSceneBuilder) and
// OuterWorld.unity (regions + mine nodes, OuterWorldBuilder) are SEPARATE scene
// files so the two builders never collide. At play, this loader brings OuterWorld
// in on top of Village — both run in one shared space, one physics/render world,
// player can't tell it's two scenes.
//
// Self-bootstrapping (AfterSceneLoad, like AudioBootstrap / WaveSystemBridge):
// when Village is the active scene and OuterWorld isn't already loaded, load it
// additively. No scene wiring, no manual call. Safe no-op in any other scene.
// Domain-reload-off safe via the s_done reset.
// =============================================================================
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    public static class WorldSceneLoader
    {
        private const string VillageSceneName    = "Village";
        private const string OuterWorldSceneName  = "OuterWorld";

        // BUILD FIX (WO-173/DEF-108): a one-shot AfterSceneLoad check FAILED in player
        // builds. AfterSceneLoad fires when the BOOT scene (Title) is active — not Village
        // — so the old `active.name != "Village"` early-return ran during Title, set its
        // guard, and OuterWorld was NEVER loaded once the player reached the village
        // (Title -> HeroSelect -> PetSelect -> Village). It only "worked" in the editor
        // because you press Play directly on Village. Fix: subscribe to sceneLoaded and
        // bring OuterWorld in WHENEVER the Village scene loads, in any flow.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetGuard() => SceneManager.sceneLoaded -= OnSceneLoaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Init()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;   // de-dupe across domain reloads
            SceneManager.sceneLoaded += OnSceneLoaded;
            Debug.Log("[WorldSceneLoader] DEBUG Init fired — subscribed to sceneLoaded. " +
                "Active scene now = '" + SceneManager.GetActiveScene().name + "'. Watching for '" +
                VillageSceneName + "'. (DEF-108 diagnostic)");
            // Handle the case where Village is already the active scene right now.
            TryLoadOuterWorld(SceneManager.GetActiveScene(), "Init-active");
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            Debug.Log("[WorldSceneLoader] DEBUG sceneLoaded event: '" + scene.name +
                "' (mode=" + mode + ").");
            if (scene.name == OuterWorldSceneName) DiagTerrain(scene);
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
                foreach (var L in UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
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
                        // (all-zero weights -> every layer contributes 0 -> terrain renders pure
                        // black). Repaint grass across the whole terrain at runtime so it renders.
                        // (Full biome variety to be restored with a bake-side persistence fix.)
                        int aw = td.alphamapWidth, ah = td.alphamapHeight, layers = td.alphamapLayers;
                        var fill = new float[ah, aw, layers];
                        for (int z = 0; z < ah; z++)
                            for (int x = 0; x < aw; x++)
                                fill[z, x, 0] = 1f;   // grass (layer 0) everywhere
                        td.SetAlphamaps(0, 0, fill);
                        Debug.Log("[WorldSceneLoader] TERRAINDIAG splat was EMPTY -> repainted grass at runtime; terrain should render now.");
                    }
                    else
                    {
                        Debug.Log("[WorldSceneLoader] TERRAINDIAG splat OK (center sum=" + sum + ").");
                    }
                }
                return;
            }
            Debug.Log("[WorldSceneLoader] TERRAINDIAG no Terrain found in OuterWorld!");
        }

        private static void TryLoadOuterWorld(Scene scene, string via)
        {
            // Only pull the outer world in when the Village scene is the one that loaded.
            if (scene.name != VillageSceneName)
            {
                Debug.Log("[WorldSceneLoader] DEBUG (" + via + ") skip — '" + scene.name +
                    "' != '" + VillageSceneName + "'.");
                return;
            }

            // Already loaded? (re-entry / editor play-twice) — don't double-load.
            if (SceneManager.GetSceneByName(OuterWorldSceneName).isLoaded)
            {
                Debug.Log("[WorldSceneLoader] DEBUG (" + via + ") OuterWorld already loaded — skip.");
                return;
            }

            // Guard: the scene must be in Build Settings to load by name.
            if (!Application.CanStreamedLevelBeLoaded(OuterWorldSceneName))
            {
                Debug.LogWarning("[WorldSceneLoader] (" + via + ") '" + OuterWorldSceneName +
                    "' is NOT in Build Settings — outer world not loaded. " +
                    "Add it (EnsureBuildSettings) so the regions/mine nodes appear.");
                return;
            }

            Debug.Log("[WorldSceneLoader] DEBUG (" + via + ") Village is active/loaded -> " +
                "loading '" + OuterWorldSceneName + "' ADDITIVELY now.");
            SceneManager.LoadScene(OuterWorldSceneName, LoadSceneMode.Additive);
            Debug.Log("[WorldSceneLoader] OuterWorld load call issued (additive over Village).");
        }
    }
}
