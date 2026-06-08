// =============================================================================
// VillageHudBootstrap — WO-334 (P0): guarantee the battle/gameplay HUD exists.
// -----------------------------------------------------------------------------
// ROOT CAUSE this fixes:
//   VillageHudController (the sleek code-built combat HUD) is CORRECT, but it is
//   only ADDED as a scene component by the editor scene-builders for the VILLAGE
//   scenes (VillageSceneBuilder / WallRepairSceneSetup). The real-time battle
//   scenes — "Defend the Tower" (PatriciaLightMode), the ATB battle (ATBBattle),
//   the Arena and the Dungeon scenes — are loaded via a FULL SceneManager.LoadScene
//   (SceneRouter.GoPatriciaLight / GoBattle / GoDungeon), which UNLOADS the village
//   scene and its authored HUD. Those battle scenes never author a HUD of their
//   own (ATBBattle even uses a UXML HUD, which §8 says does NOT render in builds).
//   Net result: in DTT / Arena / ATB / Dungeon there is NO VillageHudController in
//   the loaded scenes → nothing builds the HUD → BattleHudVisibilityManager finds
//   no HUD group to fade in → the battle HUD never appears.
//
// THE FIX (build-safe, code-built, asmdef-clean):
//   A RuntimeInitializeOnLoadMethod(AfterSceneLoad) bootstrap (mirroring
//   BattleHudVisibilityManager.Bootstrap) that, on EVERY scene load, ENSURES
//   exactly one VillageHudController exists in the active gameplay scenes. If the
//   village scene already authored one, FindObjectOfType finds it and we do
//   nothing (idempotent). If a battle scene has none, we spawn a self-contained
//   host GameObject and add the controller — its own Start() then builds the
//   full code uGUI HUD and registers via CoreServices.RegisterHud.
//
//   We DELIBERATELY do NOT spawn the HUD on the pure front-end / menu screens
//   (Title, HeroSelect, PetSelect, Intro, Store, GameOver) — those own their own
//   UI and must stay clean. Everything else (village + every combat/dungeon scene)
//   gets the HUD, so any NEW battle/dungeon scene is covered automatically without
//   touching its scene-builder.
//
//   We do NOT DontDestroyOnLoad the host: the controller's own context-detection
//   (village vs world / wave-active) is per-loaded-scene, and the village scene
//   authors its own instance. A persistent HUD would collide with the authored one
//   and could drift across unrelated menu scenes. Per-scene-ensure keeps exactly
//   one live HUD wherever gameplay is happening.
//
// ASMDEF: DeNelle.HUD → DeNelle.Core only. No Village / BattleATB references.
// WEBGL-SAFE: the controller's Build() is already try/catch-wrapped; this
//   bootstrap only constructs a GameObject + AddComponent (cannot throw in normal
//   operation) and is itself guarded.
// =============================================================================

using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.HUD
{
    /// <summary>
    /// Static, scene-driven guarantor that a <see cref="VillageHudController"/>
    /// exists in every gameplay scene (village AND the full-load battle/dungeon
    /// scenes), so the code-built combat HUD always appears. WO-334.
    /// </summary>
    public static class VillageHudBootstrap
    {
        // Menu / front-end scenes that own their own UI and must NOT get the
        // gameplay HUD. Matched case-insensitively against the active scene name.
        // Everything NOT in this set is treated as a gameplay scene.
        private static readonly string[] MenuScenes =
        {
            "Title",
            "HeroSelect",
            "PetSelect",
            "Intro",
            "IntroFlow",
            "Store",
            "PackStore",
            "Boot",
            "Bootstrap",
            "MainMenu",
            "GameOver",
        };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Install()
        {
            // Re-arm on every scene load, plus handle the very first loaded scene.
            SceneManager.sceneLoaded -= OnSceneLoaded;
            SceneManager.sceneLoaded += OnSceneLoaded;
            EnsureHudForScene(SceneManager.GetActiveScene());
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EnsureHudForScene(scene);
        }

        private static void EnsureHudForScene(Scene scene)
        {
            try
            {
                // Skip pure menu / front-end screens (they own their UI).
                if (IsMenuScene(scene.name)) return;

                // Idempotent: if any loaded scene already has a HUD (e.g. the
                // village scene authored one), do nothing.
                if (Object.FindObjectOfType<VillageHudController>() != null) return;

                var go = new GameObject("VillageHUD (bootstrapped)");
                // Parent to the just-loaded scene so it unloads with that scene
                // (no DontDestroyOnLoad — see header rationale).
                if (scene.IsValid() && scene.isLoaded)
                    SceneManager.MoveGameObjectToScene(go, scene);
                go.AddComponent<VillageHudController>();
                Debug.Log($"[VillageHudBootstrap] WO-334: spawned VillageHudController for scene '{scene.name}' (none authored).");
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[VillageHudBootstrap] EnsureHudForScene failed: " + e.Message);
            }
        }

        private static bool IsMenuScene(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            for (int i = 0; i < MenuScenes.Length; i++)
            {
                if (string.Equals(sceneName, MenuScenes[i], System.StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }
    }
}
