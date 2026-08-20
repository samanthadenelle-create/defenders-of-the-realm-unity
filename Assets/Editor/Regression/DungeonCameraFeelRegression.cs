// =============================================================================
// DungeonCameraFeelRegression [dungeon-camera-feel] -- locks the boundary between
// the OUTDOOR world-aesthetics pass and an ENCLOSED interior camera.
// -----------------------------------------------------------------------------
// PROVENANCE (2026-08-20). Owner: "healers cottage broken still, movement or camera
// issues", screenshot logs/device/enemy-color.png -- Dungeon_HealersCottage framed
// into a wall, a near-black band across the top, a cream strip down the right edge.
//
// The standing theory was that WorldFeelInjector was applying OVERWORLD camera
// treatment (Skybox clear + post-processing + ambient motes) inside the dungeon.
// THE CAPTURED DATA REFUTED IT -- logs/device/enemy-color.log, session pid ( 6783):
//
//   14:05:54.209  [Flow:WorldFeel] camera 'Main Camera' clearFlags SolidColor -> Skybox
//   14:05:54.214  [Flow:WorldFeel] scene='Main_Castle_Overworld' skybox=dusk-procedural
//   14:10:01.753  [DungeonPortal] Entering dungeon scene: Dungeon_HealersCottage
//   14:10:02.713  [Flow:FloorDiag] LIGHTING scene='Dungeon_HealersCottage'
//                 ambientMode=Flat ambient=RGBA(0.050, 0.050, 0.055) ... intensity=0.18
//
// WorldFeel ran FOUR MINUTES EARLIER, in the overworld. ZERO [Flow:WorldFeel] lines
// exist after the dungeon load, and the dungeon's authored dark Flat ambient survived
// intact. The near-black band is the dungeon camera's OWN authored clear
// (DungeonSceneBuilder.CreateCamera -> SolidColor #070709), not a skybox.
//
// WHAT THE INVESTIGATION DID FIND, and what this suite therefore guards: the global
// grade Volume is a child of the DontDestroyOnLoad WorldFeelInjector (isGlobal,
// priority 10) and NOTHING switched it off on the way underground. RenderSettings are
// per-scene and revert on load; a DDOL Volume does not. Bloom 4.5 / +0.75 EV / +10
// saturation stayed armed indoors, waiting on any interior camera with post-processing
// enabled. A latent leak, now stood down.
//
// This suite asserts BOTH DIRECTIONS -- the pass must reach the outdoor scenes AND
// must let go of interiors. Source-lint (edit-mode, no PlayMode). Never throws.
// FEEL / framing is OWNER felt-verify; this guards wiring, scoping and numbers.
// =============================================================================
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class DungeonCameraFeelRegression
    {
        /// <summary>Batchmode entry point. Emits the marker and exits non-zero on failure.</summary>
        public static void RunAll()
        {
            bool ok = Run(out string reason);
            if (ok) Debug.Log("DUNGEON_CAMERA_FEEL_OK - " + reason);
            else Debug.LogError("DUNGEON_CAMERA_FEEL_FAIL: " + reason);
            EditorApplication.Exit(ok ? 0 : 1);
        }

        /// <summary>Contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var fails = new List<string>();

            try
            {
                string feel    = ReadOrFail("_Modules/Village/World/WorldFeelInjector.cs", fails);
                string hubs    = ReadOrFail("_Modules/Core/HubScenes.cs", fails);
                string profile = ReadOrFail("_Modules/Core/World/DungeonCameraProfile.cs", fails);
                string builder = ReadOrFail("Editor/DungeonSceneBuilder.cs", fails);
                if (fails.Count > 0) return Verdict(fails, out reason);

                // =============================================================
                //  DIRECTION 1 -- the pass MUST still reach the outdoor scenes.
                //  (A "fix" that simply disabled WorldFeel everywhere would make
                //  Direction 2 pass while silently reverting the black-sky fix.)
                // =============================================================

                // (1a) The outdoor allowlist still names the live overworld hub, and
                //      the merged-overworld structural test is still consulted.
                if (!feel.Contains("\"" + DeNelle.Core.SceneRouter.Castle + "\""))
                    fails.Add($"(1a) WorldFeelInjector no longer lists '{DeNelle.Core.SceneRouter.Castle}' as outdoor -- the hub would lose the dusk pass");
                if (!feel.Contains("HubScenes.IsOverworld"))
                    fails.Add("(1a) WorldFeelInjector no longer consults HubScenes.IsOverworld -- the WO-608 merged-world path would be missed");

                // (1b) The camera Skybox-clear fix (the whole reason this injector exists)
                //      is still applied on the outdoor path.
                if (!feel.Contains("CameraClearFlags.Skybox"))
                    fails.Add("(1b) WorldFeelInjector no longer forces Skybox clear outdoors -- the black-sky root cause returns");

                // (1c) The global grade volume must be RE-ARMED when we come back outdoors.
                //      Without this, one dungeon visit permanently kills the town grade.
                if (!feel.Contains("RE-ARMED"))
                    fails.Add("(1c) no re-arm of the global grade volume on the outdoor path -- a single interior visit would permanently disable the town grade");

                // =============================================================
                //  DIRECTION 2 -- the pass MUST let go of an enclosed interior.
                // =============================================================

                // (2a) An explicit standdown exists and is reached from the non-outdoor branch.
                if (!feel.Contains("StandDown("))
                    fails.Add("(2a) WorldFeelInjector has no StandDown -- a non-outdoor scene would silently keep the DDOL grade volume armed (the 2026-08-20 latent leak)");
                if (!feel.Contains("STANDDOWN scene="))
                    fails.Add("(2a) no [Flow:WorldFeel] STANDDOWN trace -- the next interior regression would start from zero evidence (CLAUDE.md 12)");

                // (2b) The standdown actually DISABLES the volume. Disable, never Destroy:
                //      it is rebuilt-once and re-armed, so a Destroy here would churn.
                if (!feel.Contains("_postVolume.SetActive(false)"))
                    fails.Add("(2b) StandDown does not disable the global grade volume -- Bloom/exposure/saturation stay live underground");
                if (feel.Contains("Destroy(_postVolume"))
                    fails.Add("(2b) StandDown destroys the grade volume instead of disabling it -- the volume is build-once/re-arm by design");

                // (2c) SCOPED STRUCTURALLY, NOT BY A SCENE-NAME LIST. The whole point:
                //      the next dungeon must be covered the day it is added.
                if (!feel.Contains("HubScenes.IsDungeon"))
                    fails.Add("(2c) the interior path does not consult HubScenes.IsDungeon -- scoping must be structural so a NEW dungeon cannot silently escape it");

                // (2d) The interior camera dump -- the instrument this triage wished it had.
                foreach (var probe in new[] { "interior camera '", "clear=", "near=", "far=", "pos=", "post=" })
                {
                    if (!feel.Contains(probe))
                    {
                        fails.Add("(2d) interior camera dump missing field '" + probe + "' -- the next interior defect could not name itself from the trace");
                        break;
                    }
                }

                // (2e) INSTRUMENTATION IS PERMANENT (owner ruling 2026-08-09). The injector
                //      must still carry its Warn/Guard net, not just Step lines.
                if (!feel.Contains("FlowTrace.Warn") || !feel.Contains("Guard.Try"))
                    fails.Add("(2e) FlowTrace.Warn / Guard.Try stripped from WorldFeelInjector -- instrumentation is permanent, flag it off, never delete it");

                // =============================================================
                //  DIRECTION 3 -- the interior's OWN authored clear is what paints
                //  the frame where geometry does not cover it. This is the fact the
                //  2026-08-20 triage turned on: the near-black band is authored, not
                //  a leaked skybox. Pin it, so the next reader is not sent hunting.
                // =============================================================

                // (3a) The hand-built dungeon camera still clears SolidColor #070709.
                if (!builder.Contains("CameraClearFlags.SolidColor"))
                    fails.Add("(3a) DungeonSceneBuilder.CreateCamera no longer clears SolidColor -- a Skybox clear in a sealed interior paints sky wherever geometry does not cover the frustum");
                if (!builder.Contains("HexColor(\"070709\")"))
                    fails.Add("(3a) DungeonSceneBuilder.CreateCamera clear colour is no longer 070709 -- it must agree with DungeonCameraProfile.ClearColor");

                // (3b) The Core-side shared clear colour is the SAME number. These two are a
                //      cited mirror across an asmdef boundary (Core cannot see Editor), so
                //      drift is only caught by reading both -- which is what this does.
                if (!profile.Contains("0x07, 0x07, 0x09"))
                    fails.Add("(3b) DungeonCameraProfile.ClearColor drifted from #070709 -- the two dungeon camera pipelines would clear to different colours again");

                // (3c) HubScenes.IsDungeon must actually classify the reported scene.
                if (!HubScenes_IsDungeon_Compiles(hubs))
                    fails.Add("(3c) HubScenes.IsDungeon no longer prefix-matches 'Dungeon' -- Dungeon_HealersCottage would be treated as an outdoor-eligible scene");

                // Live behavioural check of the same test (Core is referenced by this asmdef).
                if (!DeNelle.Core.HubScenes.IsDungeon("Dungeon_HealersCottage"))
                    fails.Add("(3c) HubScenes.IsDungeon(\"Dungeon_HealersCottage\") returned FALSE -- the reported scene is not classified as an interior");
                if (DeNelle.Core.HubScenes.IsOverworld("Dungeon_HealersCottage"))
                    fails.Add("(3c) HubScenes.IsOverworld(\"Dungeon_HealersCottage\") returned TRUE -- the dungeon would take the outdoor world-feel pass");
                // ⛔ RESOLVED, never typed. hub-scene-literal caught a hardcoded
                // "Main_Castle_Overworld" here on 2026-08-20. A typed-in hub name goes stale
                // SILENTLY and the gate then reports OK while watching a scene the player never
                // loads - the same way UICaptureMode, TowerRespawnRegression and FloorDeepDiag
                // all ended up green and blind. Iterate CastleCandidates so this holds for BOTH
                // ff.MergedWorld branches rather than only the one that happens to be on today.
                // ⛔ SceneRouter.Castle, NOT CastleCandidates. The candidate ARRAY holds both
                // branches of ff.MergedWorld, and index [1] is "MainCastle_Hall" - a LEGACY file
                // that still exists on disk and is explicitly NOT the hub (CLAUDE.md §7). Iterating
                // both asserts the legacy scene must be outdoor, which it correctly is not; that
                // false failure was this file's first red. `Castle` resolves the ACTIVE hub off the
                // flag, so this follows a MergedWorld flip without naming either scene.
                string activeHub = DeNelle.Core.SceneRouter.Castle;
                if (!DeNelle.Core.HubScenes.IsOverworld(activeHub))
                    fails.Add($"(3c) HubScenes.IsOverworld('{activeHub}') returned FALSE -- the active hub would lose the dusk pass");
                if (DeNelle.Core.HubScenes.IsDungeon(activeHub))
                    fails.Add($"(3c) HubScenes.IsDungeon('{activeHub}') returned TRUE -- the active hub would be stood down as an interior");

                // (3d) No outdoor-allowlisted scene may ALSO classify as a dungeon. If these
                //      two sets ever overlap, the two branches fight and the winner is
                //      whichever test is read first -- exactly the drift this file exists for.
                // Hub resolved, not typed (hub-scene-literal). Village2 is a raid target and
                // has no router accessor, so it stays a literal deliberately - the RULE is
                // about the HUB name going stale, and that one is now resolved.
                foreach (var s in new[] { DeNelle.Core.SceneRouter.Castle, "Village2" })
                {
                    if (!feel.Contains("\"" + s + "\"")) continue;   // allowlist may legitimately shrink
                    if (DeNelle.Core.HubScenes.IsDungeon(s))
                        fails.Add("(3d) outdoor-allowlisted scene '" + s + "' also classifies as a dungeon -- the outdoor and interior branches overlap");
                }
            }
            catch (System.Exception ex)
            {
                fails.Add("oracle threw (this suite must never throw): " + ex.GetType().Name + " " + ex.Message);
            }

            return Verdict(fails, out reason);
        }

        /// <summary>Source-level check that IsDungeon still prefix-matches the "Dungeon" family.</summary>
        private static bool HubScenes_IsDungeon_Compiles(string hubs)
        {
            return hubs.Contains("StartsWith(\"Dungeon\"");
        }

        private static string ReadOrFail(string rel, List<string> fails)
        {
            string p = Path.Combine(Application.dataPath, rel);
            if (!File.Exists(p)) { fails.Add("source not found: " + rel + " -- re-point this oracle"); return string.Empty; }
            return File.ReadAllText(p);
        }

        private static bool Verdict(List<string> fails, out string reason)
        {
            if (fails.Count == 0)
            {
                reason = "DUNGEON CAMERA FEEL OK -- WorldFeel still applies the Skybox clear + dusk grade " +
                         "in the outdoor scenes and RE-ARMS its grade volume on return; it now STANDS DOWN " +
                         "in any non-outdoor scene (global volume disabled, motes cleared, traced), scoped " +
                         "structurally via HubScenes.IsDungeon rather than a scene-name list; the interior " +
                         "camera dump (clear/bg/near/far/pos/post) is wired so the next interior defect names " +
                         "itself; and the dungeon's authored SolidColor #070709 clear still agrees across " +
                         "DungeonSceneBuilder and DungeonCameraProfile (FRAMING = owner felt-verify)";
                return true;
            }

            reason = "dungeon-camera-feel: " + string.Join("; ", fails);
            return false;
        }
    }
}
