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
using DeNelle.Core.Diagnostics;

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
    /// RETURN-POINT (return-point feature): the scene + hero pose the player left
    /// when a battle launched, so the ATB round-trip lands back exactly where they
    /// fought from — not the Village2 default. Stashed by
    /// <see cref="SceneRouter.GoBattle"/> on a STATIC (it must survive the ATB scene's
    /// Single load tearing the whole world down — same handoff lifetime as
    /// <see cref="SceneRouter.PendingBattle"/>), read by BattleController to choose the
    /// return scene, and consumed once on the far-side load to warp the hero home.
    /// </summary>
    [Serializable]
    public sealed class ReturnPoint
    {
        /// <summary>Active scene name the battle was launched from.</summary>
        public string Scene;
        /// <summary>Hero world position at launch (skipped/zero if no hero was found).</summary>
        public Vector3 Position;
        /// <summary>Hero heading (Y euler) at launch, for restoring facing.</summary>
        public float Yaw;
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
        /// <summary>
        /// The Central Castle Hub (MainCastle_Hall or Main_Castle_Overworld) — the player's home base and the
        /// new first stop after onboarding. Built by
        /// <c>DeNelle.Editor.CastleHubBuilder.BuildCastleHub</c>. The player arrives
        /// here, then travels out to <see cref="Village"/> for the tower-defense loop.
        /// <para>
        /// WO-608: flag-aware. When <c>ff.MergedWorld</c> is ON the home hub is the
        /// single merged <c>Main_Castle_Overworld</c> scene (castle + outer world in one
        /// continuous navmesh, no additive stream / seam warp); when OFF this stays the
        /// legacy two-scene <c>MainCastle_Hall</c>. A property, not
        /// a const, so it can flip at runtime — verified nothing uses it in a const/case/
        /// attribute context (only GoCastle's fade-load + DevPanel JumpScene, both runtime).
        /// </para>
        /// </summary>
        public static string Castle => DeNelle.Core.FeatureFlags.MergedWorld
            ? "Main_Castle_Overworld"
            : "MainCastle_Hall";
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

        // ── Raid bases (WO-453 Step 4 — the troop DEPLOY/RALLY/RETREAT target) ──
        // Config-generated enemy garrisons baked to RaidBase_<id>.unity. The player
        // sails out from the castle hub to ASSAULT one with their trained army; the
        // raid HUD (RaidDeployController) deploys troops, and a loss/retreat evacs
        // home via GoCastle. These three first-playable raid scenes are registered
        // in EditorBuildSettings so IsSceneRegistered passes for GoRaid.
        /// <summary>Small raider camp — the easiest first-playable raid target.</summary>
        public const string RaidBaseRaiderCampSmall  = "RaidBase_raider_camp_small";
        /// <summary>A fortified garrison — mid-tier raid target.</summary>
        public const string RaidBaseFortifiedGarrison = "RaidBase_fortified_garrison";
        /// <summary>A mage enclave — the toughest of the three first-playable raids.</summary>
        public const string RaidBaseMageEnclave       = "RaidBase_mage_enclave";

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
            FlowTrace.Step("SceneRouter", $"LoadScene(sync) name='{sceneName ?? "<null>"}'");
            if (!IsSceneRegistered(sceneName))
            {
                FlowTrace.Fail("SceneRouter", $"LoadScene ABORTED: '{sceneName ?? "<null>"}' not in Build Settings — hero stranded on current scene.");
                Debug.LogError($"[SceneRouter] Scene '{sceneName}' is not in Build Settings — load aborted.");
                return;
            }
            // WO1: local save before synchronous transitions (no await available here;
            // the background delta-sync timer will push the backend diff shortly after).
            DeNelle.Core.State.GameStateService.Instance?.Save();
            FlowTrace.Step("SceneRouter", $"LoadScene committing SceneManager.LoadScene('{sceneName}')");
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
            FlowTrace.Step("SceneRouter", $"LoadSceneWithFade name='{sceneName ?? "<null>"}' fade={fadeSeconds:F2}s");
            if (!IsSceneRegistered(sceneName))
            {
                FlowTrace.Fail("SceneRouter", $"LoadSceneWithFade ABORTED: '{sceneName ?? "<null>"}' not in Build Settings — hero stranded on current scene.");
                Debug.LogError($"[SceneRouter] Scene '{sceneName}' is not in Build Settings — load aborted.");
                return;
            }

            // WO1: flush local + backend before the scene tears down. Runs during
            // the fade-out so there is no perceptible delay added to the transition.
            // WO-769 FIX (device data 2026-07-26): the backend flush must NEVER abort
            // navigation. A signed-in player's save-sync POSTs to Neon /api/game/save;
            // if that 401s (Neon not yet verifying the Firebase token) or the network
            // faults, the thrown UnityWebRequestException used to propagate out of this
            // .Forget()'d task and SILENTLY strand the player on the front-end scene
            // (LoadSceneAsync below never ran). The local save already persisted; a
            // failed cloud sync re-queues offline — it must not block the scene load.
            var svc = DeNelle.Core.State.GameStateService.Instance;
            if (svc != null)
            {
                try { await svc.SaveBeforeSceneChange(); }
                catch (System.Exception e)
                {
                    FlowTrace.Warn("SceneRouter",
                        $"SaveBeforeSceneChange failed — loading '{sceneName}' anyway (save re-queues): {e.Message}");
                }
            }

            if (Fader != null)
                await Fader.FadeOut(fadeSeconds);

            // RETURN-POINT (return-point feature): if a battle stashed a return point, arm a
            // one-shot sceneLoaded handler BEFORE the load so the hero is warped back to where
            // they fought the instant the destination scene is active. Self-clearing.
            ArmReturnPointRestore();

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
        //  RETURN-POINT restore (return-point feature)
        // =====================================================================

        /// <summary>
        /// RETURN-POINT (return-point feature): if <see cref="Return"/> is set, subscribes a
        /// ONE-SHOT <see cref="SceneManager.sceneLoaded"/> handler that finds the hero in the
        /// freshly-loaded scene and warps it to the stashed pose (reusing HeroLocomotion.WarpTo
        /// — disables the agent, moves, re-warps onto the NavMesh, raises OnTeleported for the
        /// follow camera), then clears <see cref="Return"/> and unsubscribes. Null-guarded; a
        /// no-op when nothing was stashed.
        /// </summary>
        private static void ArmReturnPointRestore()
        {
            if (Return == null) { FlowTrace.Step("SceneRouter", "ArmReturnPointRestore: no Return stashed — nothing to arm."); return; }

            // Audit P1 fix (return-point double-subscribe): if GoBattle arms again
            // while a prior load is still pending, a second handler would subscribe —
            // handler#1 clears Return, handler#2 then sees null and skips the warp
            // (wrong scene on return). Always detach before attaching so exactly one
            // handler is ever active.
            FlowTrace.Step("SceneRouter", $"ArmReturnPointRestore: arming one-shot sceneLoaded handler for return scene '{Return.Scene ?? "<null>"}' (detach-before-attach de-dupe).");
            SceneManager.sceneLoaded -= OnReturnSceneLoaded;
            // Capture once; the handler runs after the load completes.
            SceneManager.sceneLoaded += OnReturnSceneLoaded;
        }

        private static void OnReturnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            FlowTrace.Step("SceneRouter", $"OnReturnSceneLoaded fired for scene '{scene.name}' (mode={mode}).");
            // One-shot: always detach, even on an early-out / failure path.
            SceneManager.sceneLoaded -= OnReturnSceneLoaded;

            ReturnPoint rp = Return;
            Return = null;            // consume regardless of outcome
            if (rp == null)
            {
                // DOUBLE-SUBSCRIBE STRAND: a prior handler already consumed Return (set it null).
                // This second handler now has nothing to warp with — the hero stays where the
                // load dropped them instead of returning to the launch pose.
                FlowTrace.Fail("SceneRouter", $"OnReturnSceneLoaded: Return already consumed (null) on scene '{scene.name}' — double-subscribe STRAND, hero NOT warped home.");
                return;
            }

            try
            {
                GameObject hero = null;
                try { hero = GameObject.FindWithTag("Player"); }
                catch (Exception e) { FlowTrace.Warn("SceneRouter", $"RestoreReturnPoint FindWithTag('Player') threw (tag may be undefined): {e.GetType().Name}: {e.Message}"); hero = null; }

                var loco = (hero != null) ? FindHeroLocomotionOn(hero) : FindHeroLocomotion();
                if (loco == null)
                {
                    FlowTrace.Fail("SceneRouter", $"Return-point restore: no HeroLocomotion in '{scene.name}' — warp SKIPPED, hero not repositioned to launch pose {rp.Position}.");
                    Debug.LogWarning("[SceneRouter] Return-point restore: no HeroLocomotion found — warp skipped.");
                    return;
                }

                Quaternion rot = Quaternion.Euler(0f, rp.Yaw, 0f);

                // Reuse HeroLocomotion.WarpTo(Vector3, Quaternion?) via reflection (no
                // Core→Village reference). Signature verified: it disables the agent, moves,
                // re-warps onto the NavMesh and raises OnTeleported for the camera.
                var warp = loco.GetType().GetMethod(
                    "WarpTo",
                    new[] { typeof(Vector3), typeof(Quaternion?) });
                if (warp != null)
                {
                    warp.Invoke(loco, new object[] { rp.Position, (Quaternion?)rot });
                    FlowTrace.Step("SceneRouter", $"Return-point restored hero via WarpTo to {rp.Position} yaw={rp.Yaw:F0} in '{scene.name}'.");
                    Debug.Log($"[SceneRouter] Return-point restored hero to {rp.Position} in '{scene.name}'.");
                }
                else
                {
                    // Last-ditch fallback: plain transform move so the player at least lands home.
                    FlowTrace.Warn("SceneRouter", $"Return-point: HeroLocomotion.WarpTo(Vector3,Quaternion?) not found via reflection — using transform fallback (no NavMesh re-warp/camera event) to {rp.Position}.");
                    loco.transform.SetPositionAndRotation(rp.Position, rot);
                    Debug.LogWarning("[SceneRouter] Return-point: WarpTo not found — used transform fallback.");
                }
            }
            catch (System.Exception e)
            {
                FlowTrace.Fail("SceneRouter", $"Return-point restore threw on '{scene.name}': {e.GetType().Name}: {e.Message} — hero NOT returned to launch pose.");
                Debug.LogWarning("[SceneRouter] Return-point restore threw (non-fatal): " + e.Message);
            }
        }

        /// <summary>
        /// RETURN-POINT helper: finds the active <c>HeroLocomotion</c> by type name via
        /// reflection so DeNelle.Core never references DeNelle.Village. Returns the component
        /// as a <see cref="MonoBehaviour"/>, or null if none is present.
        /// </summary>
        private static MonoBehaviour FindHeroLocomotion()
        {
            try
            {
                var t = System.Type.GetType("DeNelle.Village.HeroLocomotion, DeNelle.Village");
                if (t == null) return null;
                var found = UnityEngine.Object.FindAnyObjectByType(t);
                return found as MonoBehaviour;
            }
            catch (Exception e) { FlowTrace.Warn("SceneRouter", $"FindHeroLocomotion reflected lookup threw: {e.GetType().Name}: {e.Message}"); return null; }
        }

        /// <summary>RETURN-POINT helper: the HeroLocomotion on a specific hero GameObject (or null).</summary>
        private static MonoBehaviour FindHeroLocomotionOn(GameObject go)
        {
            try
            {
                var t = System.Type.GetType("DeNelle.Village.HeroLocomotion, DeNelle.Village");
                if (t == null) return null;
                return go.GetComponent(t) as MonoBehaviour;
            }
            catch (Exception e) { FlowTrace.Warn("SceneRouter", $"FindHeroLocomotionOn reflected GetComponent threw: {e.GetType().Name}: {e.Message}"); return null; }
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
        /// Go to the Central Castle Hub (<see cref="Castle"/>) — the home base players
        /// land in after onboarding and on every session resume. Fire-and-forget with
        /// a fade, mirroring <see cref="GoVillage"/>. The castle is lighter than the
        /// village, so it uses the standard fade load rather than the village overlay.
        /// From the castle the player travels out to the village TD loop.
        /// </summary>
        public static void GoCastle()
        {
            var castle = Castle;
            FlowTrace.Step("SceneRouter", $"GoCastle -> '{castle}' (MergedWorld={DeNelle.Core.FeatureFlags.MergedWorld}).");
            LoadSceneWithFade(castle).Forget();
        }

        /// <summary>
        /// Go to a RAID BASE scene (WO-453 Step 4) — the troop DEPLOY/RALLY/RETREAT
        /// target the player assaults from the castle hub. Fire-and-forget with a fade,
        /// mirroring <see cref="GoCastle"/>; the raid scene's RaidGarrisonSpawner marks
        /// it enemy-owned on the far side and the RaidDeployController self-installs.
        /// <para>
        /// SHARED CONTRACT: this exact signature — <c>GoRaid(string sceneName)</c> — is
        /// what the raid-entry UI calls; pass one of the <c>RaidBase*</c> consts. An
        /// unregistered scene name is rejected by <see cref="LoadSceneWithFade"/> (logs,
        /// no load), so a bad name never silently strands the player.
        /// </para>
        /// </summary>
        public static void GoRaid(string sceneName) => LoadSceneWithFade(sceneName).Forget();

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
            FlowTrace.Step("SceneRouter", $"LoadVillageWithLoader -> '{Village}' (overlay load).");
            if (!IsSceneRegistered(Village))
            {
                FlowTrace.Fail("SceneRouter", $"LoadVillageWithLoader ABORTED: '{Village}' not in Build Settings — village load aborted.");
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
                var tree = UnityEngine.Object.FindAnyObjectByType<DeNelle.Core.TreeOfLifeMaterialFixer>();
                if (tree != null)
                {
                    tree.transform.position = new Vector3(0f, -0.25f, 0f);
                    FlowTrace.Step("SceneRouter", $"Tree of Life planted at {tree.transform.position} (roots underground).");
                    Debug.Log($"[SceneRouter] Tree of Life planted at {tree.transform.position} (roots underground).");
                }
                else
                {
                    FlowTrace.Warn("SceneRouter", "Tree of Life (TreeOfLifeMaterialFixer) not found on village load — root-seat skipped.");
                    Debug.LogWarning("[SceneRouter] Tree of Life (TreeOfLifeMaterialFixer) not found on village load — root-seat skipped.");
                }
            }
            catch (System.Exception e)
            {
                FlowTrace.Fail("SceneRouter", $"Tree root-seat threw: {e.GetType().Name}: {e.Message}");
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
            FlowTrace.Step("SceneRouter", $"GoBattle wave={p?.Wave ?? -1} breachedIds={p?.BreachedIds?.Length ?? 0} -> ATBBattle");
            PendingBattle = p;

            // RETURN-POINT (return-point feature): stash the scene + hero pose we are
            // leaving BEFORE the ATB scene loads Single and tears this world down, so the
            // round-trip returns to where the player fought instead of the Village2 default.
            StashReturnPoint(p);

            return LoadSceneWithFade(ATBBattle);
        }

        /// <summary>The last <see cref="BattleParams"/> handed to <see cref="GoBattle"/>.</summary>
        public static BattleParams PendingBattle { get; private set; }

        /// <summary>
        /// RETURN-POINT (return-point feature): the scene + hero pose to restore after a
        /// battle resolves. Set by <see cref="GoBattle"/>, read by BattleController to pick
        /// the return scene, and consumed once by <see cref="RestoreReturnPointOnLoad"/>.
        /// </summary>
        public static ReturnPoint Return { get; set; }

        /// <summary>
        /// RETURN-POINT (return-point feature): captures the active scene name and the hero's
        /// current world pose into <see cref="Return"/>, and pins
        /// <see cref="BattleParams.ReturnScene"/> to that scene so the Village2 default can
        /// never win on the far side. Fully null-guarded — if no hero is found the position is
        /// skipped but the scene is still recorded. SceneRouter lives in DeNelle.Core, which
        /// must not reference DeNelle.Village, so the hero is reached by the "Player" tag and
        /// (fallback) by reflection — never a direct HeroLocomotion type reference.
        /// </summary>
        private static void StashReturnPoint(BattleParams p)
        {
            string activeScene = SceneManager.GetActiveScene().name;
            FlowTrace.Step("SceneRouter", $"StashReturnPoint from active scene '{activeScene}'.");
            var rp = new ReturnPoint { Scene = activeScene };

            try
            {
                GameObject hero = null;
                try { hero = GameObject.FindWithTag("Player"); }
                catch (Exception e) { FlowTrace.Warn("SceneRouter", $"StashReturnPoint FindWithTag('Player') threw (tag may be undefined in some scenes): {e.GetType().Name}: {e.Message}"); hero = null; }

                if (hero == null)
                {
                    // Fallback: locate the HeroLocomotion host by type name (reflection — no
                    // Core→Village asmdef reference).
                    FlowTrace.Warn("SceneRouter", "StashReturnPoint: no 'Player'-tagged hero — falling back to reflected HeroLocomotion lookup.");
                    var loco = FindHeroLocomotion();
                    if (loco != null) hero = loco.gameObject;
                }

                if (hero != null)
                {
                    rp.Position = hero.transform.position;
                    rp.Yaw = hero.transform.eulerAngles.y;
                    FlowTrace.Step("SceneRouter", $"StashReturnPoint captured hero pose pos={rp.Position} yaw={rp.Yaw:F0}.");
                }
                else
                {
                    FlowTrace.Warn("SceneRouter", $"StashReturnPoint: no hero found in '{activeScene}' — scene stashed, position defaults to {rp.Position}.");
                    Debug.LogWarning("[SceneRouter] Return-point: no hero found — scene stashed, position skipped.");
                }
            }
            catch (System.Exception e)
            {
                FlowTrace.Fail("SceneRouter", $"StashReturnPoint threw on '{activeScene}': {e.GetType().Name}: {e.Message} — return pose may be wrong on the round-trip.");
                Debug.LogWarning("[SceneRouter] Return-point stash threw (non-fatal): " + e.Message);
            }

            Return = rp;

            // Pin the return scene so BattleParams' Village2 default can never win.
            if (p != null) p.ReturnScene = activeScene;
        }

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
