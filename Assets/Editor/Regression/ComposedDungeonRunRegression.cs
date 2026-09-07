// =============================================================================
// ComposedDungeonRunRegression (WO-1112) -- the composed (dg_*) dungeon is a REAL
// run: the hero has abilities, its keys and locks are visible, its lantern has a
// meter and an honest burn, and clearing it PAYS.
// -----------------------------------------------------------------------------
// FOUR DEFECTS THIS PINS, all player-visible, all found by the 2026-08-16 cross-silo
// sweep, and every one of them SILENT -- which is why they survived nightly play:
//
//   A5  NO ABILITIES. DungeonBaker.PopulateForPlay bakes the Keeper with
//       HeroLocomotion + HeroBodySwapper and nothing else. HeroAbilityInput is
//       [RequireComponent(HeroAbilities)] so it never attaches, AssignableSkillBar's
//       ability ref stays null, and the HUD bridge never binds. Q/W/E/R did nothing,
//       with ZERO trace lines. Root: DungeonPortal loaded the scene with no
//       hero-carry hook. Fixed by reusing WO-1109's carry (SceneRouter's ONE
//       CarryHeroAcrossSingleLoad, armed through LoadSceneWithFade's beforeLoad).
//
//   A6  INVISIBLE KEYS AND LOCKS. Both are baked with no Renderer anywhere, so an
//       invisible key gated a floor and a run could hard-stall with nothing on
//       screen. Fixed with a runtime lit-primitive body (no re-bake needed).
//
//   A7  NO OIL METER, AND THE OIL REALLY DRAINS. DungeonHudController.SetLantern had
//       one production caller (DungeonController), which is in NO dg_* scene. Meanwhile
//       100 oil at 1.6/s emptied in 62.5s with darkness latching ~53s: a minute in, the
//       player was permanently dark with the ambush multiplier on and no meter.
//
//   A8  A CLEARED RUN PAID NOTHING. GrantRunPayout -- whose own doc says "EVERY
//       COMPLETED RUN PAYS" -- was private and reachable only from the cottage
//       ExitToVillage. Since DungeonRunPayout.LastPolishScore is written nowhere else,
//       JewelPolishService scored every composed run 0 and the rough-stone economy was
//       inert in exactly the dungeons that get played.
//
// WHY A SOURCE-STRUCTURAL ORACLE: no harness loads a dg_* scene in play mode today,
// so there is nothing to assert against at runtime. Every invariant below is decidable
// from the real .cs / .json text on disk -- the same idiom SceneRoutingRegression and
// RaidHeroCarryRegression use. It therefore proves the SHIPPED source.
//
// ⚠ COMMENTS AND STRING LITERALS ARE STRIPPED BEFORE EVERY MATCH. A source-lint that
// matches raw text passes on a mention in a comment or an error message -- i.e. it can
// be satisfied by the very sentence describing the bug. Every assertion here runs
// against CODE ONLY. (This file's own header names all four defects in prose, which is
// exactly the text that would produce a false pass without the stripper.)
//
// ⚠ NO HOLLOW PASSES. Every case that scans for targets FAILS when it finds none. A
// case that quietly proves nothing is worse than an absent case, because it reports
// green.
//
// Orchestrator (DataRegression.RunAll) registers it covenant-style:
//   if (!ComposedDungeonRunRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[composed-dungeon-run] " + r);
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class ComposedDungeonRunRegression
    {
        // ── Files under test (Assets-relative) ──────────────────────────────────
        private const string RouterRel   = "_Modules/Core/SceneRouter.cs";
        private const string PortalRel   = "_Modules/Village/Buildings/DungeonPortal.cs";
        private const string EnsurerRel  = "_Modules/Village/Hero/HeroControlEnsurer.cs";
        private const string HubScenesRel = "_Modules/Core/HubScenes.cs";
        private const string KeyRel      = "_Modules/Dungeons/ComposedKeyPickup.cs";
        private const string LockRel     = "_Modules/Dungeons/ComposedLockedPort.cs";
        private const string VisualsRel  = "_Modules/Dungeons/ComposedPropVisuals.cs";
        private const string HostRel     = "_Modules/Dungeons/ComposedDungeonHost.cs";
        private const string BootRel     = "_Modules/Dungeons/ComposedDungeonBootstrap.cs";
        private const string ExitRel     = "_Modules/Dungeons/DungeonExitInteractable.cs";
        private const string DungeonCtlRel = "_Modules/Dungeons/DungeonController.cs";
        private const string SeatRel     = "_Modules/Dungeons/DungeonHeroSeat.cs";
        private const string LanternRel  = "_Modules/Dungeons/Lantern.cs";
        private const string BalanceResRel    = "Resources/Data/Canonical/dungeon-balance.json";
        private const string BalanceStreamRel = "StreamingAssets/Data/Canonical/dungeon-balance.json";

        // The burn the owner ruled against ("triple that at minimum"): 100 oil / 1.6 per sec.
        private const float OldSecondsToEmpty = 62.5f;
        private const float MinimumBurnMultiple = 3f;

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- COMPOSED DUNGEON RUN (WO-1112: abilities carried, keys/locks visible, oil meter installed, exit pays) ---");

            string assetsRoot = Application.dataPath;
            var src = new Dictionary<string, string>();

            Case(failures, "hero-abilities", () => Case1_HeroCarriesAbilities(assetsRoot, src, failures, log));
            Case(failures, "prop-renderers", () => Case2_KeysAndLocksAreVisible(assetsRoot, src, failures, log));
            Case(failures, "oil-meter",      () => Case3_ComposedInstallsTheOilHud(assetsRoot, src, failures, log));
            Case(failures, "exit-pays",      () => Case4_ComposedExitReachesThePayout(assetsRoot, src, failures, log));
            Case(failures, "lantern-burn",   () => Case5_LanternBurnIsAuthoredData(assetsRoot, src, failures, log));
            Case(failures, "arrival-seat",   () => Case6_ComposedArrivalIsProven(assetsRoot, src, failures, log));

            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "COMPOSED_DUNGEON_RUN_OK");
                reason = "COMPOSED DUNGEON RUN OK - 6/6 cases pass (the town hero with its abilities is carried into dg_* via the " +
                         "ONE WO-1109 carry, keys and locks build a visible body at runtime, the composed host installs the code-built " +
                         "oil HUD, the composed exit reaches the single GrantRunPayout authority and writes LastPolishScore, and the " +
                         "lantern burn is authored in dungeon-balance.json at >=3x the old 62.5s, and the composed " +
                         "arrival pose is PROVEN one frame after load by the one shared DungeonHeroSeat authority)";
                return true;
            }

            reason = "composed-dungeon-run: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "COMPOSED_DUNGEON_RUN_FAIL: " + reason);
            return false;
        }

        /// <summary>Standalone batch entry.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("COMPOSED_DUNGEON_RUN_OK - " + reason);
            else Debug.LogError("COMPOSED_DUNGEON_RUN_FAIL: " + reason);
        }

        // =====================================================================
        //  Case 1 (A5) -- a composed dungeon hero carries abilities
        // =====================================================================
        private static void Case1_HeroCarriesAbilities(string root, Dictionary<string, string> src,
                                                       List<string> failures, StringBuilder log)
        {
            string router = Load(root, RouterRel, src, failures);
            string portal = Load(root, PortalRel, src, failures);
            string ensurer = Load(root, EnsurerRel, src, failures);
            string hub = Load(root, HubScenesRel, src, failures);
            if (router == null || portal == null || ensurer == null || hub == null) return;

            // (a) The composed-only scene test exists. It is what keeps the carry OFF the
            //     hand-built pipeline, whose DungeonController owns its baked hero by
            //     serialized reference - carrying there would null those refs.
            if (hub.IndexOf("bool IsComposedDungeon", StringComparison.Ordinal) < 0)
                failures.Add("[hero-abilities] HubScenes.IsComposedDungeon is GONE - the composed-only gate the dungeon hero carry rides on no longer exists, so either every dungeon carries a hero (breaking the cottage's serialized hero refs) or none does (Q/W/E/R dead again in dg_*)");
            else
                log.AppendLine("OK: HubScenes.IsComposedDungeon exists (the composed-only carry gate)");

            // (b) GoDungeonScene arms the ONE carry, gated to composed, via the beforeLoad hook.
            if (TryExtractMethodBody(router, "void GoDungeonScene(string sceneName)", out string goBody))
            {
                bool arms = goBody.IndexOf("CarryHeroAcrossSingleLoad", StringComparison.Ordinal) >= 0;
                bool hooked = goBody.IndexOf("beforeLoad", StringComparison.Ordinal) >= 0;
                bool gated = goBody.IndexOf("IsComposedDungeon", StringComparison.Ordinal) >= 0;

                if (!arms)
                    failures.Add("[hero-abilities] SceneRouter.GoDungeonScene no longer calls CarryHeroAcrossSingleLoad - the town hero is destroyed with the town on the Single load and the composed dungeon falls back to the baker's bare rig, which has NO HeroAbilities. Q/W/E/R go dead again, silently");
                if (!hooked)
                    failures.Add("[hero-abilities] SceneRouter.GoDungeonScene arms the carry INLINE instead of as LoadSceneWithFade's beforeLoad hook - an aborted load then leaves the hero detached and DontDestroyOnLoad'd in a town that never unloads, and the fade leaves the player driving a detached hero for hundreds of ms");
                if (!gated)
                    failures.Add("[hero-abilities] SceneRouter.GoDungeonScene no longer gates the carry on IsComposedDungeon - a HAND-BUILT Dungeon_* scene would now get a second hero, DedupeHeroes would destroy the baked one, and that scene's DungeonController would be left holding null serialized hero references");
                if (arms && hooked && gated)
                    log.AppendLine("OK: GoDungeonScene arms the ONE carry (CarryHeroAcrossSingleLoad) through the beforeLoad hook, gated to composed dungeons");

                // No SECOND carry mechanism: the WO explicitly forbade writing one.
                if (goBody.IndexOf("DontDestroyOnLoad", StringComparison.Ordinal) >= 0)
                    failures.Add("[hero-abilities] SceneRouter.GoDungeonScene calls DontDestroyOnLoad DIRECTLY - that is a SECOND carry mechanism beside CarryHeroAcrossSingleLoad. The detach-before-DDOL rule (DDOL-ing the hub root once dragged WaveManager, HeartController and the Tree of Life into the destination) lives in that one helper; a parallel copy will not have it");
                else
                    log.AppendLine("OK: GoDungeonScene owns no second carry mechanism (it delegates to the shared helper)");
            }
            else
            {
                failures.Add("[hero-abilities] SceneRouter.GoDungeonScene(string) is GONE - the composed dungeon hero carry has no implementation, so the dungeon hero is the baker's bare rig with no HeroAbilities");
            }

            // (c) The portal actually USES it. This is the call site the whole defect traced to.
            if (portal.IndexOf("SceneRouter.GoDungeonScene", StringComparison.Ordinal) < 0)
                failures.Add("[hero-abilities] DungeonPortal no longer calls SceneRouter.GoDungeonScene - it is the entry point for every dungeon the player walks into, so routing around it means no carry is ever armed no matter what the router offers");
            else
                log.AppendLine("OK: DungeonPortal enters through SceneRouter.GoDungeonScene (the carry is actually armed)");

            // (d) The RECEIVING half: the DDOL leak guard must cover composed scenes too.
            //     FindObjectsByType RETURNS DDOL objects, so without this the carried hero is
            //     "found", the re-home never runs, and the hero lives in DDOL for the session.
            if (TryExtractMethodBody(ensurer, "private void Ensure()", out string ensureBody))
            {
                bool composedRehome = ensureBody.IndexOf("IsComposedDungeon", StringComparison.Ordinal) >= 0;
                bool recovers = ensureBody.IndexOf("TryRecoverCarriedHero", StringComparison.Ordinal) >= 0;
                if (!composedRehome || !recovers)
                    failures.Add($"[hero-abilities] HeroControlEnsurer.Ensure no longer re-homes a carried hero on COMPOSED dungeon entry (IsComposedDungeon={composedRehome}, TryRecoverCarriedHero={recovers}). FindObjectsByType returns DontDestroyOnLoad objects, so the carried hero is 'found', the re-home is skipped, and it both keeps its TOWN world pose (arriving outside the dungeon shell) and leaks in DDOL across every later Single load");
                else
                    log.AppendLine("OK: Ensure() re-homes the carried hero on composed dungeon entry (no DDOL leak, hero is seated)");
            }
            else
            {
                failures.Add("[hero-abilities] could not locate HeroControlEnsurer.Ensure() - the receiving half of the carry is unverifiable");
            }

            // (e) A composed dungeon bakes NO HeroStartPoint_PlayerSpawn, so the seat must come
            //     from the hero the dedupe displaced. Without it the carried hero keeps its town
            //     coordinates and arrives outside the dungeon.
            //     WO-1131 widened DedupeHeroes' return from a bare Vector3? to a result that
            //     ALSO carries the survivor, so this no longer matches on the old signature.
            //     The seat half is what this case is about; the survivor half is measured for
            //     real (by reflection + a driven fixture) in HeroDedupeSurvivorRegression.
            if (TryExtractMethodBody(ensurer, "HeroDedupeResult DedupeHeroes()", out string dedupeBody)
                && dedupeBody.IndexOf("displacedSeat", StringComparison.Ordinal) >= 0)
                log.AppendLine("OK: DedupeHeroes returns the displaced seat (the composed dungeon's only entry position)");
            else
                failures.Add("[hero-abilities] HeroControlEnsurer.DedupeHeroes no longer returns the displaced seat. A composed dungeon bakes no HeroStartPoint_PlayerSpawn marker, so the destroyed baked hero's position is the ONLY record of where that scene wanted a hero - without it the carried hero keeps its TOWN world pose and arrives outside the dungeon shell, in the dark, unable to reach the exit");
        }

        // =====================================================================
        //  Case 2 (A6) -- keys and locks have a renderer
        // =====================================================================
        private static void Case2_KeysAndLocksAreVisible(string root, Dictionary<string, string> src,
                                                         List<string> failures, StringBuilder log)
        {
            string key = Load(root, KeyRel, src, failures);
            string lok = Load(root, LockRel, src, failures);
            string vis = Load(root, VisualsRel, src, failures);
            if (key == null || lok == null || vis == null) return;

            if (key.IndexOf("ComposedPropVisuals.BuildKey", StringComparison.Ordinal) < 0)
                failures.Add("[prop-renderers] ComposedKeyPickup no longer builds a visual body. DungeonBaker.PlaceComposeKeys bakes a bare GameObject + trigger collider with NO Renderer, so the key is INVISIBLE in a player build - and a floor is gated behind it, so the run hard-stalls with nothing on screen to explain it");
            else
                log.AppendLine("OK: ComposedKeyPickup builds a visible body at runtime");

            if (lok.IndexOf("ComposedPropVisuals.BuildLock", StringComparison.Ordinal) < 0)
                failures.Add("[prop-renderers] ComposedLockedPort no longer builds a visual body - the baked lock has no Renderer, so the barrier the player must read as 'you need a key' is invisible and all they get is a floating prompt attached to nothing");
            else
                log.AppendLine("OK: ComposedLockedPort builds a visible body at runtime");

            // The builder must actually MAKE geometry. Counting CreatePrimitive across the file
            // would be the WRONG assertion (and this oracle failed on it once, correctly): every
            // part funnels through one Prim() helper, so the file-wide count is 1 no matter how
            // many parts exist. Assert per-BODY instead — the key must be several parts or its
            // silhouette does not read as a key, which is the colourblind-safe carrier here.
            if (CountOccurrences(vis, "CreatePrimitive") < 1)
                failures.Add("[prop-renderers] ComposedPropVisuals no longer calls CreatePrimitive - it builds no geometry at all, so every caller above lands in a no-op and the props stay invisible");
            else
                log.AppendLine("OK: ComposedPropVisuals actually instantiates primitives");

            if (TryExtractMethodBody(vis, "void BuildKey(GameObject host", out string keyBody))
            {
                int parts = CountOccurrences(keyBody, "Prim(body");
                if (parts < 3)
                    failures.Add($"[prop-renderers] the key body is down to {parts} part(s) - a key is identified by its SILHOUETTE (ring + shaft + bit teeth), which is what makes it readable without relying on its brass tint. Fewer parts reads as an anonymous blob in a dark room");
                else
                    log.AppendLine($"OK: the key body is built from {parts} parts (ring/shaft/teeth silhouette)");

                if (keyBody.IndexOf("ComposedPropSpin", StringComparison.Ordinal) < 0)
                    failures.Add("[prop-renderers] the key no longer spins/bobs - MOTION is the colourblind-safe half of 'that is a pickup'. Without it the key relies on hue alone to stand out from dungeon dressing");
                else
                    log.AppendLine("OK: the key spins and bobs (motion carries 'pickup', not colour)");
            }
            else
            {
                failures.Add("[prop-renderers] ComposedPropVisuals.BuildKey is GONE - the invisible-key ship-blocker has no fix");
            }

            if (TryExtractMethodBody(vis, "void BuildLock(GameObject host", out string lockBody))
            {
                int parts = CountOccurrences(lockBody, "Prim(body");
                if (parts < 2)
                    failures.Add($"[prop-renderers] the lock body is down to {parts} part(s) - the keyhole plus its bar is what makes a lock read as 'this needs the key you are carrying' rather than as a door someone shut");
                else
                    log.AppendLine($"OK: the lock body adds {parts} lock parts (keyhole + bar) on top of the door");

                // WO-1588: ONE DOOR BUILDER. The locked port used to hang its OWN flat cube -
                // 1.6 x 2.1 x 0.16, the exact "moving wall" silhouette WO-1568 removed from
                // CommonDungeonDoor - and the owner photographed it as a white slab with a
                // floating yellow blob (F8 seq 4699). A second door builder is the defect, so
                // pin the seam, not the shape.
                if (lockBody.IndexOf("CommonDungeonDoor.BuildDoorVisual", StringComparison.Ordinal) < 0)
                    failures.Add("[prop-renderers] BuildLock no longer routes through CommonDungeonDoor.BuildDoorVisual - the locked port is building a SECOND door, which is how it ended up as a flat slab while every other door in the dungeon had a frame, a lintel and a real leaf (WO-1588)");
                else
                    log.AppendLine("OK: the locked port is built by the ONE door seam (CommonDungeonDoor.BuildDoorVisual)");

                if (lockBody.IndexOf("\"Plate\"", StringComparison.Ordinal) >= 0)
                    failures.Add("[prop-renderers] the retired flat 'Plate' cube is back in BuildLock - that primitive IS the white slab of WO-1588");

                // A teleport port seated in open floor must not leave a collider behind: the leaf
                // blocker BuildDoorVisual attaches is right for a wall gap and wrong here, and no
                // NavMesh knows about it.
                if (lockBody.IndexOf("door.Blocker", StringComparison.Ordinal) < 0)
                    failures.Add("[prop-renderers] BuildLock no longer strips the door leaf's blocker collider - the locked port is a teleport at a room seat, not a wall gap, so that collider is an unbaked solid box standing in open floor");
                else
                    log.AppendLine("OK: BuildLock strips the door leaf blocker (a port never blocks the floor)");
            }
            else
            {
                failures.Add("[prop-renderers] ComposedPropVisuals.BuildLock is GONE - the invisible lock has no fix");
            }

            // WO-1588: the strip moved into a DestroyNow(col) helper, because DungeonSceneCapture
            // now drives BuildLock in EDIT mode and a plain Object.Destroy there logs an error and
            // leaves the collider alive. Accept either spelling - the assertion is "the collider is
            // stripped", never "one particular API is called".
            if (vis.IndexOf("Destroy(col)", StringComparison.Ordinal) < 0 &&
                vis.IndexOf("DestroyNow(col)", StringComparison.Ordinal) < 0)
                failures.Add("[prop-renderers] ComposedPropVisuals no longer strips the primitive colliders. CreatePrimitive attaches one by default: on the lock plate it would physically block the hero, and on the key it would shadow the SphereCollider the pickup fires from - a visible key that can no longer be picked up is strictly worse than an invisible one");
            else
                log.AppendLine("OK: ComposedPropVisuals strips every primitive collider (decoration never blocks or shadows a trigger)");

            // Idempotency: a future bake-time art pass must win over the runtime body.
            if (vis.IndexOf("HasBody", StringComparison.Ordinal) < 0)
                failures.Add("[prop-renderers] ComposedPropVisuals lost its HasBody guard - bodies would stack on re-entry and a future baked Renderer would be double-drawn instead of taking precedence");
            else
                log.AppendLine("OK: ComposedPropVisuals is idempotent and yields to a baked Renderer (HasBody)");
        }

        // =====================================================================
        //  Case 3 (A7) -- the composed pipeline installs the lantern HUD
        // =====================================================================
        private static void Case3_ComposedInstallsTheOilHud(string root, Dictionary<string, string> src,
                                                            List<string> failures, StringBuilder log)
        {
            string host = Load(root, HostRel, src, failures);
            string boot = Load(root, BootRel, src, failures);
            if (host == null || boot == null) return;

            bool addsHud = host.IndexOf("AddComponent<DungeonHudController>", StringComparison.Ordinal) >= 0;
            bool binds = host.IndexOf("SetLantern", StringComparison.Ordinal) >= 0;
            if (!addsHud || !binds)
                failures.Add($"[oil-meter] ComposedDungeonHost no longer installs and binds the oil HUD (AddComponent<DungeonHudController>={addsHud}, SetLantern={binds}). DungeonHudController.SetLantern's only other production caller is DungeonController, which is in NO dg_* scene, so the composed player watches an invisible flask drain to empty and then plays the rest of the run in the dark with the ambush multiplier on");
            else
                log.AppendLine("OK: ComposedDungeonHost installs DungeonHudController and pushes the lantern through SetLantern");

            // The HUD must be the CODE-BUILT one. CLAUDE.md sec.8: UXML does not render in
            // player builds; DungeonHudController was rebuilt code-first for exactly this.
            if (host.IndexOf("UIDocument", StringComparison.Ordinal) >= 0
                || host.IndexOf("VisualElement", StringComparison.Ordinal) >= 0)
                failures.Add("[oil-meter] ComposedDungeonHost references UIDocument/VisualElement - a UXML path was resurrected for the composed oil meter. UXML DOES NOT RENDER IN PLAYER BUILDS (CLAUDE.md sec.8); that is precisely why DungeonHudController was rebuilt code-first, and a UXML meter would come up blank exactly where it matters");
            else
                log.AppendLine("OK: no UXML path in the composed HUD install (code-built uGUI only)");

            // The bootstrap must hand its run state to a live owner, or nothing downstream
            // (HUD, ambush, payout) has anything to read.
            if (boot.IndexOf("AddComponent<ComposedDungeonHost>", StringComparison.Ordinal) < 0)
                failures.Add("[oil-meter] ComposedDungeonBootstrap no longer installs ComposedDungeonHost - the DungeonRuntimeState goes back to dying in a local variable, which takes the oil HUD, the ambush wiring AND the exit payout down with it");
            else
                log.AppendLine("OK: ComposedDungeonBootstrap installs the ComposedDungeonHost owner");

            // The one-frame defer: on the load frame BOTH the carried hero and the baker's
            // doomed rig answer the Player tag (Destroy resolves at end of frame).
            if (host.IndexOf("yield return null", StringComparison.Ordinal) < 0)
                failures.Add("[oil-meter] ComposedDungeonHost no longer defers the hero pillars by a frame. On the load frame the carried hero AND the baker's rig both answer FindGameObjectWithTag(Player) - Unity's Destroy is deferred to end of frame - so the lantern, the oil meter and the ambush director can all be attached to the rig that is about to be destroyed, and they would fail silently");
            else
                log.AppendLine("OK: the hero pillars arm one frame after load (hero resolution is unambiguous)");
        }

        // =====================================================================
        //  Case 4 (A8) -- a composed exit reaches the ONE payout authority
        // =====================================================================
        private static void Case4_ComposedExitReachesThePayout(string root, Dictionary<string, string> src,
                                                               List<string> failures, StringBuilder log)
        {
            string exit = Load(root, ExitRel, src, failures);
            string ctl = Load(root, DungeonCtlRel, src, failures);
            if (exit == null || ctl == null) return;

            // The authority must be reachable from outside DungeonController.
            if (ctl.IndexOf("public static void GrantRunPayout", StringComparison.Ordinal) < 0)
                failures.Add("[exit-pays] DungeonController.GrantRunPayout is no longer 'public static' - it is reachable only from the cottage-pipeline ExitToVillage again, and DungeonController is in NO dg_* scene, so a cleared composed dungeon pays nothing");
            else
                log.AppendLine("OK: GrantRunPayout is a callable authority (public static)");

            if (exit.IndexOf("DungeonController.GrantRunPayout", StringComparison.Ordinal) < 0)
                failures.Add("[exit-pays] DungeonExitInteractable no longer calls DungeonController.GrantRunPayout - the composed exit falls through to the Castle load and pays NOTHING, and since DungeonRunPayout.LastPolishScore is written nowhere else, JewelPolishService scores every composed run 0 and the whole rough-stone economy is inert in exactly the dungeons that get played");
            else
                log.AppendLine("OK: the composed exit routes through DungeonController.GrantRunPayout");

            // ONE AUTHORITY. Exactly one site in the whole project may WRITE the polish score;
            // a second one is the duplicate-authority bug this WO was explicitly told to avoid.
            var writers = new List<string>();
            int scanned = 0;
            foreach (var cs in Directory.GetFiles(Path.Combine(root, "_Modules"), "*.cs", SearchOption.AllDirectories))
            {
                scanned++;
                string s;
                try { s = StripCommentsAndStrings(File.ReadAllText(cs)); }
                catch { continue; }
                if (s.IndexOf("LastPolishScore =", StringComparison.Ordinal) >= 0)
                    writers.Add(Path.GetFileName(cs));
            }
            if (scanned == 0)
            {
                // NO HOLLOW PASS: a scan that examined nothing proved nothing.
                failures.Add("[exit-pays] the LastPolishScore authority scan found NO .cs files under Assets/_Modules - this case proved nothing and must not be read as a pass");
            }
            else if (writers.Count == 0)
            {
                failures.Add($"[exit-pays] NOTHING under Assets/_Modules writes DungeonRunPayout.LastPolishScore ({scanned} files scanned) - no run of either pipeline records a polish grade, so JewelPolishService scores every stone 0 and the grade half of the economy is dead");
            }
            else if (writers.Count > 1)
            {
                failures.Add($"[exit-pays] {writers.Count} sites write DungeonRunPayout.LastPolishScore ({string.Join(", ", writers)}) - the payout was DUPLICATED rather than shared. Two payout authorities drift, then double-pay or disagree on the grade; WO-1112 required the composed exit to reach the SAME GrantRunPayout, not to copy it");
            }
            else
            {
                log.AppendLine($"OK: exactly ONE site writes LastPolishScore ({writers[0]}) across {scanned} module files");
            }
        }

        // =====================================================================
        //  Case 5 -- the lantern burn is authored data, and tripled
        // =====================================================================
        private static void Case5_LanternBurnIsAuthoredData(string root, Dictionary<string, string> src,
                                                            List<string> failures, StringBuilder log)
        {
            string lantern = Load(root, LanternRel, src, failures);
            if (lantern != null && lantern.IndexOf("DungeonLanternBalance", StringComparison.Ordinal) < 0)
                failures.Add("[lantern-burn] Lantern no longer reads DungeonLanternBalance - the oil tuning falls back to the [SerializeField] defaults, i.e. a HIDDEN CODE DEFAULT nobody can re-tune. That is the same defect shape as the silent 6x storage-repair issue, and here it decides whether the player spends the run in the dark");
            else if (lantern != null)
                log.AppendLine("OK: Lantern takes its oil tuning from dungeon-balance.json");

            string resPath = Path.Combine(root, BalanceResRel);
            string streamPath = Path.Combine(root, BalanceStreamRel);
            bool resExists = File.Exists(resPath);
            bool streamExists = File.Exists(streamPath);
            if (!resExists || !streamExists)
            {
                failures.Add($"[lantern-burn] dungeon-balance.json is missing a copy (Resources={resExists}, StreamingAssets={streamExists}). The Resources copy is the one a WebGL/player build reads; without BOTH, the authored burn silently reverts to the code default on some platform");
                return;
            }

            string resText, streamText;
            try
            {
                resText = File.ReadAllText(resPath);
                streamText = File.ReadAllText(streamPath);
            }
            catch (Exception e)
            {
                failures.Add($"[lantern-burn] could not read dungeon-balance.json ({e.GetType().Name}) - the authored burn is unverifiable");
                return;
            }

            if (!string.Equals(resText, streamText, StringComparison.Ordinal))
                failures.Add("[lantern-burn] the dungeon-balance.json Resources/StreamingAssets dual-copy is NOT identical - the editor and the player build would run different lantern burns, which makes every felt-test of the dark unrepeatable");
            else
                log.AppendLine("OK: dungeon-balance.json dual-copy is identical");

            float maxOil, drain;
            try
            {
                var o = JObject.Parse(resText);
                var lan = o["lantern"];
                if (lan == null)
                {
                    failures.Add("[lantern-burn] dungeon-balance.json has no 'lantern' block - the burn is unauthored and the code default silently decides it");
                    return;
                }
                maxOil = lan["maxOil"] != null ? (float)lan["maxOil"] : -1f;
                drain = lan["oilDrainPerSec"] != null ? (float)lan["oilDrainPerSec"] : -1f;
            }
            catch (Exception e)
            {
                failures.Add($"[lantern-burn] dungeon-balance.json does not parse ({e.GetType().Name}: {e.Message}) - the loader would fall back to the code default with only a Warn");
                return;
            }

            if (maxOil <= 0f || drain <= 0f)
            {
                failures.Add($"[lantern-burn] dungeon-balance.json authors an unusable lantern (maxOil={maxOil}, oilDrainPerSec={drain}) - both must be > 0 or the flask either never drains or empties instantly");
                return;
            }

            float seconds = maxOil / drain;
            float required = OldSecondsToEmpty * MinimumBurnMultiple;
            if (seconds < required)
                failures.Add($"[lantern-burn] the authored lantern burns for {seconds:F0}s, below the {required:F0}s floor (owner ruling 2026-08-16: 'make the lanterns last triple that at minimum'; the old burn was {OldSecondsToEmpty:F1}s). Below this the player is back in permanent darkness about a minute into every run");
            else
                log.AppendLine($"OK: authored lantern burn = {seconds:F0}s to empty ({seconds / OldSecondsToEmpty:F1}x the old {OldSecondsToEmpty:F1}s), darkness latch at ~{seconds * 0.88f:F0}s");
        }

        // =====================================================================
        //  Case 6 (WO-1222) -- the composed arrival pose is PROVEN, not assumed
        // =====================================================================
        // THE DEFECT: entering the composed Healer’s Cottage gave the owner a BLACK SCREEN with
        // a working joystick (Seeker build 2026.08.26.341419). The scene was healthy -- 7 enemies,
        // 60 fps, nothing threw. The hero was at (5000, 0, 4991), which is BattleArena’s staged
        // hero stance to the centimetre (ArenaCentre + (0, 0, -ArenaHalfDepth + 9)); the camera
        // was honestly following a hero standing ~7km away in an arena staging area.
        //
        // THE STRUCTURAL CAUSE: the two dungeon pipelines were asymmetric. The hand-built one
        // teleports its Keeper every Begin() (DungeonController.PlaceHero). The composed one has
        // no DungeonController, therefore no PlaceHero, therefore NOTHING that ever checked where
        // the carried hero ended up -- and the hero root is DontDestroyOnLoad, so other DDOL
        // systems (BattleArena above all) can write that transform with nobody watching.
        //
        // WHAT THIS PINS is the OUTCOME contract, not a call order: both pipelines run the SAME
        // authority, and that authority tests the arena coordinate explicitly. A second, parallel
        // placement path in the composed host would satisfy a naive "does it seat the hero" lint
        // and would drift from the hand-built one exactly as these two already did.
        private static void Case6_ComposedArrivalIsProven(string root, Dictionary<string, string> src,
                                                          List<string> failures, StringBuilder log)
        {
            string seat = Load(root, SeatRel, src, failures);
            string host = Load(root, HostRel, src, failures);
            string ctl  = Load(root, DungeonCtlRel, src, failures);
            if (seat == null || host == null || ctl == null) return;

            // (a) The authority exists and is genuinely shared (public, not a host-private helper).
            if (seat.IndexOf("class DungeonHeroSeat", StringComparison.Ordinal) < 0)
                failures.Add("[arrival-seat] DungeonHeroSeat is GONE - there is no shared placement authority, so the composed pipeline is back to having NO arrival check at all, and a hero parked at the arena stance renders a black screen at 60fps through every headless gate");
            else
                log.AppendLine("OK: DungeonHeroSeat exists (the one placement authority for both dungeon pipelines)");

            // (b) It tests the arena coordinate BY NAME. A generic "is the hero near its seat"
            //     check would still pass a hero standing 7km away in a scene that bakes no seat;
            //     the arena test is what makes that case unambiguous and nameable in the trace.
            if (seat.IndexOf("IsArenaPosition", StringComparison.Ordinal) < 0)
                failures.Add("[arrival-seat] DungeonHeroSeat no longer tests BattleArena.IsArenaPosition. That coordinate is the ONE pose no dungeon layout can produce and only BattleArena.WarpHero can write - dropping the test turns the exact 2026-08-26 black screen back into an unexplained 'the hero is somewhere odd'");
            else
                log.AppendLine("OK: DungeonHeroSeat names the arena coordinate explicitly (IsArenaPosition)");

            // (c) It STANDS DOWN for a live staged battle. Dungeons legitimately stage arena
            //     fights (EncounterTrigger -> BeginEncounter); teleporting the player out of one
            //     would be a worse defect than the one being fixed.
            if (seat.IndexOf("AnyBattleInProgress", StringComparison.Ordinal) < 0)
                failures.Add("[arrival-seat] DungeonHeroSeat no longer checks for a live staged battle before correcting the pose. A dungeon CAN stage a real arena encounter, and yanking the hero out of a live fight is a worse defect than the arrival bug this net exists for");
            else
                log.AppendLine("OK: DungeonHeroSeat stands down while a staged battle owns the hero");

            // (d) THE COMPOSED PIPELINE ACTUALLY CALLS IT. This is the missing half -- the whole
            //     defect is that the composed path had no assertion, however good the authority is.
            if (TryExtractMethodBody(host, "void ArmHeroPillars()", out string armBody))
            {
                if (armBody.IndexOf("DungeonHeroSeat", StringComparison.Ordinal) < 0)
                    failures.Add("[arrival-seat] ComposedDungeonHost.ArmHeroPillars does NOT run the DungeonHeroSeat arrival check. That method is the composed pipeline's one hero-resolution point (deliberately one frame after load, once the duplicate-hero destroy has resolved) - without the check here nothing in the composed path ever proves where the player arrived, which is exactly how a 60fps black screen shipped");
                else
                    log.AppendLine("OK: ComposedDungeonHost.ArmHeroPillars proves the arrival through DungeonHeroSeat");
            }
            else
            {
                failures.Add("[arrival-seat] ComposedDungeonHost.ArmHeroPillars() is GONE - the composed pipeline's hero-resolution point cannot be located, so its arrival check is unverifiable");
            }

            // (e) The HAND-BUILT pipeline runs the SAME authority. Two placement paths that do not
            //     share code are how these two drifted apart in the first place.
            if (TryExtractMethodBody(ctl, "void PlaceHero(Vector3 spawnPos)", out string placeBody))
            {
                if (placeBody.IndexOf("DungeonHeroSeat", StringComparison.Ordinal) < 0)
                    failures.Add("[arrival-seat] DungeonController.PlaceHero no longer routes through DungeonHeroSeat. The hand-built and composed dungeons are then placing heroes with two independent copies of the same logic - the exact asymmetry that left the composed path with no assertion for its whole life");
                else
                    log.AppendLine("OK: DungeonController.PlaceHero runs the same shared authority (one placement owner, both pipelines)");
            }
            else
            {
                failures.Add("[arrival-seat] DungeonController.PlaceHero(Vector3) is GONE - the hand-built pipeline's placement cannot be located");
            }
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add($"[{name}] THREW {ex.GetType().Name}: {ex.Message}"); }
        }

        /// <summary>Reads a source file and returns it with comments AND string literals stripped.</summary>
        private static string Load(string assetsRoot, string rel, Dictionary<string, string> cache, List<string> failures)
        {
            if (cache.TryGetValue(rel, out var hit)) return hit;
            string path = Path.Combine(assetsRoot, rel);
            if (!File.Exists(path))
            {
                failures.Add($"WO-1112: '{rel}' is MISSING - the invariant it carries is unverifiable, which is a failure and not a skip");
                cache[rel] = null;
                return null;
            }
            try
            {
                string s = StripCommentsAndStrings(File.ReadAllText(path));
                cache[rel] = s;
                return s;
            }
            catch (Exception e)
            {
                failures.Add($"WO-1112: could not read '{rel}' ({e.GetType().Name}: {e.Message})");
                cache[rel] = null;
                return null;
            }
        }

        /// <summary>
        /// Removes line comments, block comments, char literals and string literals (including
        /// verbatim and interpolated forms) from C# source, replacing each with a single space so
        /// token boundaries survive.
        /// <para>
        /// ⚠ THIS IS THE POINT OF THE WHOLE FILE. A raw-text source-lint is satisfied by a MENTION
        /// - a comment describing the bug, or an error message naming the call it is asserting -
        /// so the oracle passes on prose while the code is broken. Every match above therefore
        /// runs on CODE ONLY. Brace chars come from code points (123/125) so this file's own
        /// brace balance stays clean under the CLAUDE.md sec.1 gate.
        /// </para>
        /// </summary>
        public static string StripCommentsAndStrings(string source)
        {
            if (string.IsNullOrEmpty(source)) return string.Empty;
            var sb = new StringBuilder(source.Length);
            int i = 0;
            int n = source.Length;
            while (i < n)
            {
                char c = source[i];

                // Line comment
                if (c == '/' && i + 1 < n && source[i + 1] == '/')
                {
                    while (i < n && source[i] != '\n') i++;
                    sb.Append(' ');
                    continue;
                }
                // Block comment
                if (c == '/' && i + 1 < n && source[i + 1] == '*')
                {
                    i += 2;
                    while (i + 1 < n && !(source[i] == '*' && source[i + 1] == '/')) i++;
                    i = Math.Min(n, i + 2);
                    sb.Append(' ');
                    continue;
                }
                // Verbatim string: @"..."  ("" is an escaped quote)
                if (c == '@' && i + 1 < n && source[i + 1] == '"')
                {
                    i += 2;
                    while (i < n)
                    {
                        if (source[i] == '"')
                        {
                            if (i + 1 < n && source[i + 1] == '"') { i += 2; continue; }
                            i++;
                            break;
                        }
                        i++;
                    }
                    sb.Append(' ');
                    continue;
                }
                // Interpolated verbatim: $@"..." or @$"..."
                if ((c == '$' && i + 2 < n && source[i + 1] == '@' && source[i + 2] == '"')
                    || (c == '@' && i + 2 < n && source[i + 1] == '$' && source[i + 2] == '"'))
                {
                    i += 3;
                    while (i < n)
                    {
                        if (source[i] == '"')
                        {
                            if (i + 1 < n && source[i + 1] == '"') { i += 2; continue; }
                            i++;
                            break;
                        }
                        i++;
                    }
                    sb.Append(' ');
                    continue;
                }
                // Regular or interpolated string: "..." / $"..."
                if (c == '"' || (c == '$' && i + 1 < n && source[i + 1] == '"'))
                {
                    i += (c == '$') ? 2 : 1;
                    while (i < n)
                    {
                        if (source[i] == '\\') { i += 2; continue; }
                        if (source[i] == '"') { i++; break; }
                        if (source[i] == '\n') break;   // unterminated; do not run away
                        i++;
                    }
                    sb.Append(' ');
                    continue;
                }
                // Char literal
                if (c == '\'')
                {
                    i++;
                    while (i < n)
                    {
                        if (source[i] == '\\') { i += 2; continue; }
                        if (source[i] == '\'') { i++; break; }
                        if (source[i] == '\n') break;
                        i++;
                    }
                    sb.Append(' ');
                    continue;
                }

                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }

        private static int CountOccurrences(string haystack, string needle)
        {
            if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle)) return 0;
            int count = 0, idx = 0;
            while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
            {
                count++;
                idx += needle.Length;
            }
            return count;
        }

        // Extracts the balanced-brace body (including the outer braces) of the first method whose
        // signature contains signatureNeedle. Runs on ALREADY-STRIPPED source, so no brace inside
        // a string or comment can throw the depth count off. Brace chars come from code points
        // (123='{', 125='}') so this file's own brace balance stays clean under sec.1.
        private static bool TryExtractMethodBody(string source, string signatureNeedle, out string body)
        {
            body = null;
            if (string.IsNullOrEmpty(source)) return false;
            char openBrace = (char)123;
            char closeBrace = (char)125;
            int sig = source.IndexOf(signatureNeedle, StringComparison.Ordinal);
            if (sig < 0) return false;
            int open = source.IndexOf(openBrace, sig);
            if (open < 0) return false;
            int depth = 0;
            for (int i = open; i < source.Length; i++)
            {
                char c = source[i];
                if (c == openBrace) depth++;
                else if (c == closeBrace)
                {
                    depth--;
                    if (depth == 0) { body = source.Substring(open, i - open + 1); return true; }
                }
            }
            return false;
        }
    }
}
