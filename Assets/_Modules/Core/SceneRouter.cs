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
        public const string Village = "Village";
        /// <summary>The ATB Last-Stand battle scene (React global AtbBattleHost).</summary>
        public const string ATBBattle = "ATBBattle";

        /// <summary>The Week-1 starter dungeon scene name.</summary>
        public const string DungeonHealersCottage = "Dungeon_HealersCottage";

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

        /// <summary>Go to the Village scene (React `/village`).</summary>
        public static void GoVillage() => LoadScene(Village);

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
