// =============================================================================
// HubStructureVisualInjector — runtime visual swap of baked castle-hub structures to
// lightweight Resources models (owner 2026-06-17), WITHOUT a scene rebuild.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// MainCastle_Hall bakes its 8 structures from polyperfect/Quaternius prefabs via
// CastleHubBuilder. As the owner authors LIGHTWEIGHT replacement models (tiny Tripo
// FBX, dropped into Assets/Resources/Structures/), this injector swaps them in at
// runtime — the project's no-scene-edit pattern (CampSystem / StoryCompanionInjector).
// On every hub load it finds each baked structure by name, hides its renderers
// (keeping the NPC interact point + colliders/logic), and skins the model in via
// VisualFactory.Skin, which BOUNDS-FITS the raw FBX to a target size, SEATS it on the
// ground, and URP-FIXES embedded Tripo materials (so it never renders magenta — which a
// CastleHubBuilder bake would, since it never fixes materials).
//
// TO REPLACE ANOTHER STRUCTURE: drop the model in Resources/Structures and add ONE row
// to Swaps below — { baked structure NAME, model path, target size (m), yaw° }. The
// baked names (from CastleHubBuilder) are:
//   Blacksmith_Weapons_Storefront · Lumbermill_Wood_Storefront · Windmill_Food_Storefront
//   EchoHollow_Pets_RoamingArea · Forge_Armor_Storefront · ArcaneTower_MagicUpgrades
//   Marketplace_Monetization
//
// Idempotent (a marker child guards re-swaps) + graceful (model missing → the baked
// visual is restored, nothing breaks).
// =============================================================================

using DeNelle.Core;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Re-skins baked hub structures with lightweight Resources models at runtime.</summary>
    public static class HubStructureVisualInjector
    {
        /// <summary>One structure re-skin. Add a row per lightweight model authored.</summary>
        private struct Swap
        {
            public string bakedName;   // the structure's baked GameObject name (CastleHubBuilder)
            public string modelPath;   // Resources path of the lightweight model
            public float  sizeM;       // fit the model's largest dimension to this many metres
            public float  yawDeg;      // Y rotation to correct a wrong-facing Tripo FBX (convention: 90)
            public float  pitchDeg;    // X rotation — when a model imports lying down (default 0)
            public float  rollDeg;     // Z rotation — rarely needed (default 0)
        }

        // ── THE SWAP TABLE — add a row per lightweight structure ──────────────────
        private static readonly Swap[] Swaps =
        {
            // CONVENTION (owner 2026-06-17): these are Tripo FBX exports — they import facing +X, so
            // ALL need yawDeg=90 to face the plaza, and their embedded materials are URP-fixed
            // automatically by SkinOptions.Structure (FixTripoMaterials). Keep new Tripo rows at yaw 90.
            // Trade convention: forge = WEAPONS (Blacksmith), armorer = ARMOR (Forge_Armor), store = Market.
            new Swap { bakedName = "EchoHollow_Pets_RoamingArea",   modelPath = "Structures/PetHouse2",    sizeM = 7f,  yawDeg = 90f },
            new Swap { bakedName = "ArcaneTower_MagicUpgrades",     modelPath = "Structures/arcane tower", sizeM = 12f, yawDeg = 90f },
            new Swap { bakedName = "Blacksmith_Weapons_Storefront", modelPath = "Structures/Forge",        sizeM = 7f,  yawDeg = 180f, pitchDeg = 90f },
            new Swap { bakedName = "Forge_Armor_Storefront",        modelPath = "Structures/armorer",      sizeM = 7f,  yawDeg = 180f, pitchDeg = 90f },
            new Swap { bakedName = "Marketplace_Monetization",      modelPath = "Structures/store",        sizeM = 8f,  yawDeg = 90f },
            new Swap { bakedName = "Lumbermill_Wood_Storefront",    modelPath = "Structures/lumbermill",   sizeM = 7f,  yawDeg = 0f,   pitchDeg = 90f },
            new Swap { bakedName = "Windmill_Food_Storefront",      modelPath = "Structures/farm",         sizeM = 8f,  yawDeg = 90f },
            // Castle barracks = the troop-TRAINING building (existing scene prefab "CastleBarracks");
            // visual swap only — its training function is already wired. Size/yaw owner-dialed.
            new Swap { bakedName = "CastleBarracks",                modelPath = "Structures/barracks",     sizeM = 8f,  yawDeg = 90f },
        };

        private const string MarkerPrefix = "LightSkin_";   // child added on swap (idempotency guard)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            if (HubScenes.IsHub(SceneManager.GetActiveScene().name)) ApplyAll();
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (HubScenes.IsHub(scene.name)) ApplyAll();
        }

        private static void ApplyAll()
        {
            for (int i = 0; i < Swaps.Length; i++) TrySwap(Swaps[i]);
        }

        private static void TrySwap(Swap s)
        {
            Transform target = FindByName(s.bakedName);
            if (target == null) return;                              // not in this scene
            string marker = MarkerPrefix + s.bakedName;
            if (target.Find(marker) != null) return;                // already swapped (idempotent)

            // Hide the baked visual (renderers only — NPC point + colliders/logic stay live).
            var bakedRenderers = target.GetComponentsInChildren<Renderer>(true);
            foreach (var r in bakedRenderers)
                if (r != null) r.enabled = false;

            // Skin the lightweight model in: bounds-fit + seat-on-ground + URP-fix Tripo materials.
            // LocalRotation (yaw) is applied BEFORE fit/seat so the fit measures it final-facing.
            var opts = SkinOptions.Structure(s.sizeM);
            opts.LocalRotation = Quaternion.Euler(s.pitchDeg, s.yawDeg, s.rollDeg);
            var vis = VisualFactory.Skin(target, s.modelPath, opts);
            if (vis == null)
            {
                // Model absent on this machine — restore the baked visual; nothing lost.
                foreach (var r in bakedRenderers)
                    if (r != null) r.enabled = true;
                Debug.LogWarning("[HubStructureVisualInjector] " + s.modelPath +
                                 " not found — kept the baked visual for " + s.bakedName + ".");
                return;
            }

            vis.name = marker;
            Debug.Log("[HubStructureVisualInjector] " + s.bakedName + " re-skinned to " + s.modelPath + ".");
        }

        // Name match across the loaded scene(s). Runs once per hub load (not per frame).
        private static Transform FindByName(string name)
        {
            foreach (var t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
                if (t != null && t.name == name) return t;
            return null;
        }
    }
}
