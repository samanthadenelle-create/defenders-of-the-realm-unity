// =============================================================================
// SceneRouter — the Unity analog of React-Router's <Routes> / useNavigate
// -----------------------------------------------------------------------------
// Port of the App.tsx route table (spec §3.3). A static class. Per the v2
// port-spec Part 3, the four canonical Unity scenes are Title, Village,
// Dungeon_HealersCottage and ATBBattle. Week 1 ships Title and Village; the
// dungeon/battle entry points are stubbed for Weeks 2+.
//
// INTRO FLOW (owner-acceptance-checklist "Intro & first-run flow"): the path
// from the Title scene into the world runs through two onboarding select
// screens —
//   Title (studio bumper) -> HeroSelect -> PetSelect -> Village
// HeroSelect and PetSelect are their own scenes (DeNelle.Onboarding). The
// Title "Start" button calls GoHeroSelect; HeroSelect's confirm calls
// GoPetSelect; PetSelect's confirm calls GoVillage. A returning player whose
// save already has a hero + starter pet may skip straight to the village —
// each select controller checks GameState and self-skips (see those files).
//
//   LoadScene          — thin synchronous SceneManager.LoadScene wrapper.
//   LoadSceneWithFade  — async UniTask: fade a black overlay → LoadSceneAsync →
//                        fade back. Never `async void` (Part-3 mandate).
//
// Scene-transition MUSIC (the App.tsx AudioBootstrap) is intentionally NOT here
// — an Audio/Core director listens for scene loads (Village owns village BGM,
// every other scene gets the `title` track). Keeping that out of the router
// preserves module separation: the router loads scenes, the audio director
// reacts.
// =============================================================================

using System;
using Cysharp.Threading.Tasks;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Core
{
    /// <summary>Hand-off parameters for the ATB battle scene (detailed in the Week-2 spec).</summary>
    [Serializable]
    public sealed class BattleParams
    {
        /// <summary>The wave that breached the town — victory loot scales off this.</summary>
        public int Wave;
        /// <summary>3D-layer ids of the enemies that breached and started the battle.</summary>
        public string[] BreachedIds = Array.Empty<string>();
        /// <summary>Engine unit-ids of the pets that fought (for the pity reward).</summary>
        public string[] ParticipatingPetIds = Array.Empty<string>();

        /// <summary>
        /// The scene the battle returns to once the result settles (BUG-008 fix).
        /// A village breach leaves this at the default (<see cref="SceneRouter.Village"/>);
        /// a dungeon encounter sets it to <see cref="SceneRouter.DungeonHealersCottage"/>
        /// so the ATB round-trip lands back in the dungeon, not the village.
        /// Empty / null also resolves to the village default.
        /// </summary>
        public string ReturnScene = SceneRouter.Village;
    }

    /// <summary>
    /// Hand-off parameters for the "Defend the Tower" (Patricia Light) scene.
    /// Stashed on <see cref="SceneRouter.PendingPatriciaLight"/> by
    /// <see cref="SceneRouter.GoPatriciaLight"/> and read by the scene's
    /// <c>PatriciaLightController</c> on the far side — exactly like
    /// <see cref="BattleParams"/> / <see cref="SceneRouter.PendingBattle"/>.
    /// </summary>
    [Serializable]
    public sealed class PatriciaLightParams
    {
        /// <summary>The wave that breached — difficulty / reward scale off this.</summary>
        public int Wave;

        /// <summary>The scene the mode returns to once it resolves (default: Village).</summary>
        public string ReturnScene = SceneRouter.Village;
    }

    /// <summary>Static scene-navigation surface — the React route table's port.</summary>
    public static class SceneRouter
    {
        // ── Canonical scene names (v2 port-spec Part 3) ──────────────────────
        /// <summary>The landing / onboarding scene (React `/`).</summary>
        public const string Title = "Title";
        /// <summary>
        /// The hero-select screen — pick Mage / Knight / Ranger. Shown once,
        /// between the Title scene's studio bumper and the Village, on the
        /// first-run intro path. See <see cref="GoHeroSelect"/>.
        /// </summary>
        public const string HeroSelect = "HeroSelect";
        /// <summary>
        /// The pet-select screen — pick one of the three starter Wardens.
        /// Shown right after <see cref="HeroSelect"/> on the intro path.
        /// </summary>
        public const string PetSelect = "PetSelect";
        /// <summary>The village tower-defense scene (React `/village`).</summary>
        public const string Village = "Village2";
        /// <summary>The ATB Last-Stand battle scene (React global AtbBattleHost).</summary>
        public const string ATBBattle = "ATBBattle";

        /// <summary>
        /// The "Defend the Tower" real-time shooter scene (WO-47 "Patricia Light").
        /// The breach-time alternative to <see cref="ATBBattle"/>: a third-person
        /// tower-defense stand where the hero fires from the spire while pets
        /// attack or repair. Built by the editor scene-builder
        /// <c>DeNelle.Editor.PatriciaLightSceneBuilder.BuildScene</c> and routed via
        /// <see cref="GoPatriciaLight"/>.
        /// </summary>
        public const string PatriciaLight = "PatriciaLightMode";

        /// <summary>The Week-1 starter dungeon scene name.</summary>
        public const string DungeonHealersCottage   = "Dungeon_HealersCottage";
        public const string DungeonFolksGranary     = "Dungeon_FolksGranary";
        public const string DungeonSunkenBellTower  = "Dungeon_SunkenBellTower";
        public const string DungeonWolfwardensVigil = "Dungeon_WolfwardensVigil";
        public const string DungeonFrostStair       = "Dungeon_FrostStair";
        public const string DungeonGlassCathedral   = "Dungeon_GlassCathedral";
        public const string DungeonApothecarysVault = "Dungeon_ApothecarysVault";

        /// <summary>The default fade duration, in seconds, for <see cref="LoadSceneWithFade"/>.</summary>
        public const float DefaultFadeSeconds = 0.4f;

        /// <summary>
        /// The dungeon scene name for a dungeon id — React `/dungeon/:id`.
        /// Week 1 ships <c>Dungeon_HealersCottage</c>.
        /// </summary>
        public static string Dungeon(string dungeonId) => $"Dungeon_{dungeonId}";

        /// <summary>
        /// Optional full-screen fade overlay used by <see cref="LoadSceneWithFade"/>.
        /// Wired up by the Core bootstrap (a DontDestroyOnLoad canvas). When null,
        /// <see cref="LoadSceneWithFade"/> falls back to a plain async load.
        /// </summary>
        public static ISceneFader Fader { get; set; }

        // =====================================================================
        //  Synchronous load
        // =====================================================================

        /// <summary>
        /// Loads a scene synchronously. Unity scene names are compile-time, so an
        /// unknown name is a programmer error — this logs an error rather than
        /// silently failing (there is no React-style catch-all 404).
        /// </summary>
        public static void LoadScene(string sceneName)
        {
            if (!IsSceneRegistered(sceneName))
            {
                Debug.LogError($"[SceneRouter] Scene '{sceneName}' is not in Build Settings — load aborted.");
                return;
            }
            // WO1: local save before synchronous transitions (no await available here;
            // the background delta-sync timer will push the backend diff shortly after).
            DeNelle.Core.State.GameStateService.Instance?.Save();
            SceneManager.LoadScene(sceneName);
        }

        // =====================================================================
        //  Async load with fade
        // =====================================================================

        /// <summary>
        /// Fades a full-screen black overlay in, loads <paramref name="sceneName"/>
        /// asynchronously, then fades back out. Returns a <see cref="UniTask"/> —
        /// never <c>async void</c> (Part-3 mandate).
        /// </summary>
        public static async UniTask LoadSceneWithFade(string sceneName, float fadeSeconds = DefaultFadeSeconds)
        {
            if (!IsSceneRegistered(sceneName))
            {
                Debug.LogError($"[SceneRouter] Scene '{sceneName}' is not in Build Settings — load aborted.");
                return;
            }

            // WO1: flush local + backend before the scene tears down. Runs during
            // the fade-out so there is no perceptible delay added to the transition.
            var svc = DeNelle.Core.State.GameStateService.Instance;
            if (svc != null)
                await svc.SaveBeforeSceneChange();

            if (Fader != null)
                await Fader.FadeOut(fadeSeconds);

            var op = SceneManager.LoadSceneAsync(sceneName);
            if (op != null)
            {
                op.allowSceneActivation = true;
                await op.ToUniTask();
            }

            if (Fader != null)
                await Fader.FadeIn(fadeSeconds);
        }

        // =====================================================================
        //  Typed entry points mirroring the React routes
        // =====================================================================

        /// <summary>Go to the Title scene (React `/`).</summary>
        public static void GoTitle() => LoadScene(Title);

        /// <summary>
        /// Go to the hero-select screen — the first step of the intro flow after
        /// the Title scene's studio bumper. The Title "Start" button routes here.
        /// </summary>
        public static void GoHeroSelect() => LoadScene(HeroSelect);

        /// <summary>
        /// Go to the pet-select screen — the second intro-flow step, entered from
        /// hero-select once the player confirms a hero.
        /// </summary>
        public static void GoPetSelect() => LoadScene(PetSelect);

        /// <summary>
        /// Go to the Village scene (React `/village`). Routes through
        /// <see cref="LoadVillageWithLoader"/> so the 3-6s village load shows a
        /// code-built loading overlay instead of a black stall. Fire-and-forget —
        /// the overlay + async load run on their own; callers don't await.
        /// </summary>
        public static void GoVillage() => LoadVillageWithLoader().Forget();

        /// <summary>
        /// Loads the Village scene ASYNCHRONOUSLY behind a full-screen, code-built
        /// loading overlay ("Loading Elarion…" + spinner + progress + rotating lore).
        /// Fixes the black-screen stall: the synchronous SceneManager.LoadScene gave
        /// the engine no chance to render feedback during the multi-second load;
        /// LoadSceneAsync yields each frame so the overlay animates while the village
        /// streams in, then the overlay tears down once the scene is active.
        ///
        /// The overlay is uGUI (no UXML, no PanelSettings) so it renders in player
        /// builds — see <see cref="UI.VillageLoadOverlay"/>. Returns a UniTask; never
        /// async void (Part-3 mandate).
        /// </summary>
        public static async UniTask LoadVillageWithLoader()
        {
            if (!IsSceneRegistered(Village))
            {
                Debug.LogError($"[SceneRouter] Scene '{Village}' is not in Build Settings — load aborted.");
                return;
            }

            // Persist the save before tearing the old scene down (same contract as
            // LoadScene). No await needed — the background delta-sync pushes shortly.
            DeNelle.Core.State.GameStateService.Instance?.Save();

            // Put the loader up FIRST so the player sees feedback immediately, before
            // the (heavy) scene load begins.
            var overlay = UI.VillageLoadOverlay.Show();

            // A frame so the overlay actually paints before we start the load.
            await UniTask.Yield();

            var op = SceneManager.LoadSceneAsync(Village);
            if (op != null)
            {
                // Hold activation until ~90% so the bar fills smoothly, then flip it.
                op.allowSceneActivation = false;
                while (!op.isDone)
                {
                    // Unity caps progress at 0.9 until allowSceneActivation flips true.
                    float p = Mathf.Clamp01(op.progress / 0.9f);
                    overlay?.SetProgress(p);

                    if (op.progress >= 0.9f)
                    {
                        overlay?.SetProgress(1f);
                        op.allowSceneActivation = true;
                    }
                    await UniTask.Yield();
                }
            }

            // A couple of frames after activation so the new scene's first frame is up
            // (NavMesh/HUD/hero settling) before we pull the overlay — avoids a flash
            // of an un-lit village.
            await UniTask.DelayFrame(2);

            // LAST word on village load: plant the decorative Tree of Life at a fixed
            // (0, -0.25, 0) so its roots sit just under the ground (owner-requested).
            // This runs AFTER every Awake/Start (incl. SeatOnGroundOnStart), so it is
            // the final position. NB: this is the *decorative* centrepiece (it carries
            // the Core-side TreeOfLifeMaterialFixer) — NOT the gameplay Heart anchor,
            // which stays at (0,0,0) for spawns/camera/grid and the regression gate.
            SeatTreeOfLifeRootsUnderground();

            overlay?.HideAndDestroy();
        }

        /// <summary>
        /// Forces the decorative Tree of Life centrepiece to (0, -0.25, 0) so its roots
        /// read as planted under the ground. Found via its Core-side
        /// <c>TreeOfLifeMaterialFixer</c> (no Core→Village reference). Non-fatal.
        /// </summary>
        private static void SeatTreeOfLifeRootsUnderground()
        {
            try
            {
                var tree = UnityEngine.Object.FindFirstObjectByType<DeNelle.Core.TreeOfLifeMaterialFixer>();
                if (tree != null)
                {
                    tree.transform.position = new Vector3(0f, -0.25f, 0f);
                    Debug.Log($"[SceneRouter] Tree of Life planted at {tree.transform.position} (roots underground).");
                }
                else
                {
                    Debug.LogWarning("[SceneRouter] Tree of Life (TreeOfLifeMaterialFixer) not found on village load — root-seat skipped.");
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning("[SceneRouter] Tree root-seat threw (non-fatal): " + e.Message);
            }
        }

        /// <summary>
        /// Go to a dungeon scene with a fade (React `/dungeon/:id`). Week 1 ships
        /// <c>Dungeon_HealersCottage</c>.
        /// </summary>
        public static UniTask GoDungeon(string dungeonId) => LoadSceneWithFade(Dungeon(dungeonId));

        /// <summary>
        /// Go to the ATB battle scene with a fade, handing off <paramref name="p"/>.
        /// The hand-off mechanism (runtime SO / static field) is detailed in the
        /// Week-2 BattleATB spec; this is the signature stub.
        /// </summary>
        public static UniTask GoBattle(BattleParams p)
        {
            PendingBattle = p;
            return LoadSceneWithFade(ATBBattle);
        }

        /// <summary>The last <see cref="BattleParams"/> handed to <see cref="GoBattle"/>.</summary>
        public static BattleParams PendingBattle { get; private set; }

        /// <summary>
        /// Go to the "Defend the Tower" (Patricia Light) shooter scene with a
        /// fade, stashing <paramref name="p"/> on <see cref="PendingPatriciaLight"/>.
        /// Mirrors <see cref="GoBattle"/>: the scene's PatriciaLightController reads
        /// the pending params on the far side and, on resolve, returns via
        /// <see cref="GoVillage"/>. Fire-and-forget — never await in a hot path.
        /// </summary>
        public static UniTask GoPatriciaLight(PatriciaLightParams p)
        {
            PendingPatriciaLight = p;
            return LoadSceneWithFade(PatriciaLight);
        }

        /// <summary>The last <see cref="PatriciaLightParams"/> handed to <see cref="GoPatriciaLight"/>.</summary>
        public static PatriciaLightParams PendingPatriciaLight { get; private set; }

        // ── Helpers ──────────────────────────────────────────────────────────
        private static bool IsSceneRegistered(string sceneName)
        {
            if (string.IsNullOrEmpty(sceneName)) return false;
            return Application.CanStreamedLevelBeLoaded(sceneName);
        }
    }

    /// <summary>
    /// A full-screen fade overlay — implemented by the Core bootstrap. Kept as an
    /// interface so <see cref="SceneRouter"/> stays free of any UI dependency.
    /// </summary>
    public interface ISceneFader
    {
        /// <summary>Fade the overlay to fully opaque (black) over <paramref name="seconds"/>.</summary>
        UniTask FadeOut(float seconds);

        /// <summary>Fade the overlay to fully transparent over <paramref name="seconds"/>.</summary>
        UniTask FadeIn(float seconds);
    }
}
