using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;   // NavMeshSurface / CollectObjects
using UnityEngine.SceneManagement;
using DeNelle.Core.World;   // RegionId (DeNelle.Core is referenced by the Editor asmdef)

namespace DeNelle.Editor
{
    // =============================================================================
    // GarrisonSceneBuilder — builds the dedicated enemy-GARRISON raid scene.
    // -----------------------------------------------------------------------------
    // Run from: Defenders > Scenes > Build Garrison Scene
    // Batchmode: DeNelle.Editor.GarrisonSceneBuilder.BuildGarrisonScene (via run-unity-method).
    //
    // This is the "USE" stage of the create -> use -> destroy enemy-outpost lifecycle:
    // a standalone Assets/Scenes/Garrison.unity the player ENTERS and RAIDS. The fort
    // (WOOD catalog fortification) + the boss-led garrison are NOT hand-placed — an
    // EnemyOutpost host self-realizes them on Play, exactly like RaidOutpostSystem does
    // in the open world. We just place + Configure() the host; it spawns the rest.
    //
    // WHAT IT CREATES (a blank single scene, fully lit + nav-baked):
    //   1. A large NavMesh-static ground plane + directional light + ambient/skybox.
    //   2. HeroStartPoint_PlayerSpawn — the entry transform (on the navmesh, south edge).
    //   3. EnemyOutpost host at centre, Configure(Goldfields, threat)'d like RaidOutpostSystem
    //      — on Play it realizes the wood fort + drops the boss + ~5-8 guards.
    //   4. A RETURN SceneTransitionTrigger (single-load back to OuterWorld) at the south
    //      exit, with a GENEROUS proximity radius (~16m) so it actually fires (the castle
    //      seam taught us a too-small radius never triggers at the navmesh edge).
    //   5. A baked NavMeshSurface (so the hero + garrison can path).
    //   6. Saves Assets/Scenes/Garrison.unity + adds it to EditorBuildSettings.scenes.
    //
    // CROSS-ASSEMBLY: the Editor asmdef references DeNelle.Core + Unity.AI.Navigation, but
    // NOT DeNelle.Village. So DeNelle.Village types (EnemyOutpost, SceneTransitionTrigger)
    // are added + configured via REFLECTION — the SAME pattern CastleHubBuilder uses for
    // SceneTransitionTrigger / StairwayBuilder. NavMeshSurface is referenced directly.
    //
    // No .unity hand-edits. Batchmode-safe (no EditorUtility dialogs). Idempotent-ish:
    // a re-run on an open Garrison scene clears the prior GarrisonRoot and rebuilds.
    // Canon: the village is Elarion (never Avalon). ASCII-only strings.
    // =============================================================================
    public static partial class GarrisonSceneBuilder
    {
        private const string MenuPath   = "Defenders/Scenes/Build Garrison Scene";
        private const string ScenePath  = "Assets/Scenes/Garrison.unity";
        private const string NavDataDir = "Assets/Scenes/Garrison";
        private const string RootName   = "GarrisonRoot";

        // Layout (world space; root at origin). The garrison fort sits at the centre;
        // the hero arrives at the south edge and the return seam is just south of that.
        private static readonly Vector3 OutpostCentre   = new Vector3(0f, 0f, 0f);
        private static readonly Vector3 HeroEntryPos     = new Vector3(0f, 0.1f, -34f);
        private static readonly Vector3 ReturnSeamPos     = new Vector3(0f, 1.5f, -42f);
        // Where the hero lands back in OuterWorld after returning (the castle-approach
        // road south of Village, matching CastleHubBuilder's OuterWorld target).
        private static readonly Vector3 OuterWorldReturnPos = new Vector3(0f, 0.5f, -80f);

        // Garrison sizing for this dedicated raid. RaidOutpostSystem threat-scales guards
        // (5 + threat/2, clamped 0..3 => 5..8). We pick a deliberate threat tier so the
        // scene reads as a meaty mid-tier raid rather than the easiest Goldfields edge.
        private const RegionId GarrisonRegion = RegionId.Goldfields;
        private const int      GarrisonThreat = 3;   // => ~6 guards + 1 boss

        [MenuItem(MenuPath)]
        public static void BuildGarrisonScene()
        {
            // 1. Fresh empty single scene (also covers re-runs cleanly).
            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            Log("New empty scene created.");

            // Idempotency safety: if a stray root survived, clear it.
            var prior = GameObject.Find(RootName);
            if (prior != null) Object.DestroyImmediate(prior);

            var root = new GameObject(RootName);
            root.transform.position = Vector3.zero;

            // 2. Lighting + environment (so the scene is lit, not black).
            BuildLightingAndEnvironment(root.transform);

            // 2b. Ground plane (large, walkable, NavMesh source via its MeshCollider).
            BuildGround(root.transform);

            // 3. Player ENTRY point (on the navmesh, facing the fort).
            var entry = new GameObject("HeroStartPoint_PlayerSpawn");
            entry.transform.SetParent(root.transform, false);
            entry.transform.position = HeroEntryPos;
            // Face north toward the garrison centre.
            entry.transform.rotation = Quaternion.LookRotation(
                (OutpostCentre - HeroEntryPos).normalized, Vector3.up);
            Log("HeroStartPoint_PlayerSpawn placed at " + HeroEntryPos + " facing the garrison.");

            // 4. EnemyOutpost host (self-spawns fort + boss-led garrison on Play).
            BuildOutpostHost(root.transform);

            // 5. RETURN seam back to OuterWorld.
            BuildReturnSeam(root.transform);

            // 6. Bake the NavMeshSurface so hero + garrison can path.
            BuildAndBakeNavMesh(root.transform);

            // 7. Save the scene + register it in Build Settings.
            SaveScene(scene);
            AddSceneToBuildSettings(ScenePath);

            Selection.activeGameObject = root;
            Log("=== Garrison scene built + baked + saved + in Build Settings. Play it to raid. ===");
        }

        // =====================================================================
        // Lighting + environment — a single directional sun + sane ambient so the
        // scene is lit headlessly (no scene-view-only lighting reliance).
        // =====================================================================
        private static void BuildLightingAndEnvironment(Transform parent)
        {
            // Directional "sun".
            var sunGo = new GameObject("Directional Light");
            sunGo.transform.SetParent(parent, false);
            sunGo.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            var sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.96f, 0.84f);
            sun.intensity = 1.1f;
            sun.shadows = LightShadows.Soft;
            RenderSettings.sun = sun;

            // Flat ambient so nothing is pitch black if no skybox material resolves.
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.45f, 0.47f, 0.52f);
            RenderSettings.fog = false;

            Log("Lighting + ambient configured (directional sun + flat ambient).");
        }

        // =====================================================================
        // Ground — a large Unity Plane (10x10m at scale 1). Scale 12 => 120x120m,
        // comfortably hosting the ~6x6 cell (CellSize 3m => 18m) fort, the garrison
        // ring, the hero entry at z -34 and the return seam at z -42. The plane's
        // MeshCollider is the NavMesh source (Use Geometry = Physics Colliders).
        // Marked NavMesh-static for editor clarity; the bake uses the collider.
        // =====================================================================
        private static void BuildGround(Transform parent)
        {
            var ground = GameObject.CreatePrimitive(PrimitiveType.Plane);
            ground.name = "GarrisonGround";
            ground.transform.SetParent(parent, false);
            ground.transform.position = Vector3.zero;
            ground.transform.localScale = new Vector3(12f, 1f, 12f);

            // Mark static (Navigation Static flag) so it reads as a deliberate nav source.
            GameObjectUtility.SetStaticEditorFlags(
                ground, StaticEditorFlags.NavigationStatic | StaticEditorFlags.BatchingStatic);

            // Tint a muted earthy ground so the fort/enemies read against it (URP-safe Lit).
            var mr = ground.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                if (shader != null)
                {
                    var mat = new Material(shader) { color = new Color(0.30f, 0.34f, 0.24f) };
                    mr.sharedMaterial = mat;
                }
            }

            Log("Ground plane (120x120m) placed, NavMesh-static, with a MeshCollider nav source.");
        }

        // =====================================================================
        // EnemyOutpost host — added + Configure()'d via reflection (DeNelle.Village is
        // NOT referenced by the Editor asmdef). Mirrors RaidOutpostSystem.SpawnNow:
        //   go.AddComponent<EnemyOutpost>(); _outpost.Configure(region, threat);
        // On Play, EnemyOutpost.Start() => BuildFortification() (auto-gen wood fort) +
        // SpawnGarrison() (1 boss + threat-scaled guards). We DON'T hand-place enemies.
        //
        // PERSISTENCE NOTE: EnemyOutpost derives its PlayerPrefs id from the region
        // ("raid_<Region>"). A cleared raid STAYS cleared on reload (the fort/garrison
        // won't re-raise). To re-raid during testing, clear PlayerPrefs key
        // "dotr-raid-cleared-raid_Goldfields" (or call PlayerPrefs.DeleteAll). Flagged
        // below so the owner can verify / tune.
        // =====================================================================
        private static void BuildOutpostHost(Transform parent)
        {
            var go = new GameObject("EnemyOutpost_" + GarrisonRegion);
            go.transform.SetParent(parent, false);
            go.transform.position = OutpostCentre;

            var outpostType = FindType("DeNelle.Village.World.Camps.EnemyOutpost");
            if (outpostType == null)
            {
                Warn("DeNelle.Village.World.Camps.EnemyOutpost not found (is it compiled?). " +
                     "Host GameObject placed WITHOUT the component — add EnemyOutpost + call " +
                     "Configure(Goldfields, " + GarrisonThreat + ") manually, or re-run after compile.");
                return;
            }

            var comp = go.AddComponent(outpostType);

            // Configure(RegionId region, int threat) — exactly RaidOutpostSystem's call.
            var configure = outpostType.GetMethod("Configure",
                BindingFlags.Public | BindingFlags.Instance,
                null, new[] { typeof(RegionId), typeof(int) }, null);
            if (configure != null)
            {
                configure.Invoke(comp, new object[] { GarrisonRegion, GarrisonThreat });
                Log("EnemyOutpost host placed + Configure(" + GarrisonRegion + ", threat " +
                    GarrisonThreat + ") — fort + boss-led garrison self-spawn on Play.");
            }
            else
            {
                Warn("EnemyOutpost.Configure(RegionId,int) not found via reflection — " +
                     "host added but NOT configured. Verify the signature.");
            }
        }

        // =====================================================================
        // RETURN seam — a SceneTransitionTrigger (added via reflection) that carries the
        // hero back to OuterWorld. SINGLE load (not additive): the garrison is a discrete
        // raid instance, so returning should fully unload it and restore the open world,
        // not stack Garrison + OuterWorld. A GENEROUS ProximityRadius (16m) so it fires at
        // the navmesh edge as the hero approaches the south exit (the castle seam proved a
        // small radius never triggers). A backing trigger box is added as the fallback.
        // =====================================================================
        private static void BuildReturnSeam(Transform parent)
        {
            var seamGo = new GameObject("ReturnToOuterWorld_Seam");
            seamGo.transform.SetParent(parent, false);
            seamGo.transform.position = ReturnSeamPos;

            // Fallback physics trigger (for movers that DO trip OnTriggerEnter).
            var box = seamGo.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(20f, 6f, 10f);

            var transType = FindType("DeNelle.Village.SceneTransitionTrigger");
            if (transType == null)
            {
                Warn("DeNelle.Village.SceneTransitionTrigger not found (is it compiled?). " +
                     "Return trigger collider added WITHOUT the behaviour — attach it manually " +
                     "or re-run after compile.");
                return;
            }

            var comp = seamGo.AddComponent(transType);

            SetField(transType, comp, "targetSceneName", "OuterWorld");
            SetField(transType, comp, "targetPosition", OuterWorldReturnPos);
            SetField(transType, comp, "loadAdditive", false);   // SINGLE: unload the raid, restore the world
            SetField(transType, comp, "ProximityRadius", 16f);  // generous so it fires at the navmesh edge

            Log("Return seam wired: SceneTransitionTrigger -> OuterWorld (single load) at " +
                OuterWorldReturnPos + ", proximity 16m, fallback trigger box.");
        }

        // =====================================================================
        // NavMesh — add a NavMeshSurface to the root and bake. Use Geometry = Physics
        // Colliders + Collect = All so the ground plane's MeshCollider is the source.
        // NavMeshSurface is in Unity.AI.Navigation (referenced by the Editor asmdef), so
        // it is used DIRECTLY here (no reflection needed, unlike DeNelle.Village types).
        // The baked data is persisted as an asset so it survives the scene save.
        // =====================================================================
        private static void BuildAndBakeNavMesh(Transform parent)
        {
            var surface = parent.GetComponent<NavMeshSurface>();
            if (surface == null) surface = parent.gameObject.AddComponent<NavMeshSurface>();

            surface.collectObjects = CollectObjects.All;
            surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
            surface.layerMask = ~0;   // all layers

            surface.BuildNavMesh();
            Log("NavMeshSurface baked (Physics Colliders, all layers).");

            // Persist the baked data so it survives the scene save (mirrors CastleHubBuilder).
            var data = surface.navMeshData;
            if (data != null)
            {
                if (!System.IO.Directory.Exists(NavDataDir))
                    AssetDatabase.CreateFolder("Assets/Scenes", "Garrison");
                string assetPath = NavDataDir + "/NavMesh-GarrisonSurface.asset";
                if (!AssetDatabase.Contains(data))
                {
                    var existing = AssetDatabase.LoadAssetAtPath<Object>(assetPath);
                    if (existing != null) AssetDatabase.DeleteAsset(assetPath);
                    AssetDatabase.CreateAsset(data, assetPath);
                    Log("NavMesh asset written -> " + assetPath);
                }
            }
            else
            {
                Warn("surface.navMeshData was null after bake — the bake produced nothing. " +
                     "Verify the ground plane MeshCollider is present.");
            }
        }

        // =====================================================================
        // Save the scene to disk.
        // =====================================================================
        private static void SaveScene(Scene scene)
        {
            // Ensure the Scenes folder exists (it does, but be safe for fresh checkouts).
            if (!AssetDatabase.IsValidFolder("Assets/Scenes"))
                AssetDatabase.CreateFolder("Assets", "Scenes");

            EditorSceneManager.MarkSceneDirty(scene);
            bool ok = EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            if (ok) Log("Saved scene -> " + ScenePath);
            else Err("SaveScene FAILED for " + ScenePath);
        }

        // =====================================================================
        // Register the scene in EditorBuildSettings so it loads at runtime (a scene
        // not in the build list fails SceneManager.LoadScene silently). Idempotent.
        // =====================================================================
        private static void AddSceneToBuildSettings(string path)
        {
            var scenes = new System.Collections.Generic.List<EditorBuildSettingsScene>(
                EditorBuildSettings.scenes);

            foreach (var s in scenes)
            {
                if (s.path == path)
                {
                    if (!s.enabled) { s.enabled = true; EditorBuildSettings.scenes = scenes.ToArray(); }
                    Log("Scene already in Build Settings (ensured enabled): " + path);
                    return;
                }
            }

            scenes.Add(new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
            Log("Added scene to Build Settings: " + path);
        }

        // =====================================================================
        // Reflection helpers (cross-assembly: DeNelle.Village not referenced here).
        // =====================================================================
        private static void SetField(System.Type type, Object comp, string fieldName, object value)
        {
            var f = type.GetField(fieldName, BindingFlags.Public | BindingFlags.Instance);
            if (f != null) f.SetValue(comp, value);
            else Warn("Field '" + fieldName + "' not found on " + type.Name + " — skipped.");
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

        private static void Log(string m)  => Debug.Log("[GarrisonSceneBuilder] " + m);
        private static void Warn(string m) => Debug.LogWarning("[GarrisonSceneBuilder] " + m);
        private static void Err(string m)  => Debug.LogError("[GarrisonSceneBuilder] " + m);
    }
}
