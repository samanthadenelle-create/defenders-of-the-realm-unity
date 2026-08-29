// =============================================================================
// FoundingGuideWolfBodyRegression [founding-guide-wolf]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core for MagentaGuard).
//
// WO-961 (owner ruling 2026-08-10, verbatim: "we should have Ice wolf" + "under
// pets"): the founding Echo guide finally gets a BODY, and it is the Simple Wolf
// pack the owner supplied. This suite pins the FOUR asset-side invariants that
// make that body actually reach the player, every one of which has already broken
// this project at least once:
//
//   (1) EXACTLY ONE asset answers Resources.Load<GameObject>("Pets/ice-wolf").
//       Resources.Load resolves by extension-less path, so a .fbx and a same-stem
//       .prefab in one folder is AMBIGUOUS - Unity picks one and which one is not
//       contractual. Resources/Structures already carries that live bug; this
//       suite makes sure Pets never grows a second one. It ALSO pins that the
//       shipped body is the WOLF and not the retired fox/coyote FBX that used to
//       occupy this path (Coyote_Mesh skinned to an AccuRig CC_Base human biped -
//       the wrong animal, unusable with quadruped clips; moved to
//       Assets/Art/Retired/Pets/ice-wolf-fox-legacy.fbx).
//
//   (2) THE CONTROLLER RESOLVES WHERE PetDeployer.WirePetAnimator LOOKS. That
//       probe order is per-species -> Pets/Pet -> Pets/PetIdle, and the first two
//       have been missing for the whole life of the pet system (which is exactly
//       why the WO-184 embedded-clip fallback exists). A controller that lands one
//       folder off is a silent T-pose, so the path is asserted by LOADING it, not
//       by reading the file listing - and the load path itself is pinned at source
//       so a refactor of WirePetAnimator cannot quietly move the goalposts.
//
//   (3) NO MAGENTA-CLASS MATERIAL on the body (QR-5.1, the single most-referenced
//       failure in this repo). The pack shipped "Simple Wolf.mat" on the BUILT-IN
//       Standard shader; built-in shaders are STRIPPED from a URP player build and
//       resolve to Hidden/InternalErrorShader = MAGENTA. It renders perfectly in
//       the editor, so ONLY a predicate test catches it before the build does.
//       The predicate used here is MagentaGuard.IsBrokenShader - the SINGLE
//       authority (deliberately public; see the note on its summary). This suite
//       does not re-implement it, because every local copy of that predicate in
//       this repo has drifted.
//
//   (4) THE CLIPS EXIST and are the ones the controller's states point at. A
//       controller whose motions are null binds fine and animates nothing - the
//       "bound-but-cannot-pose" case WirePetAnimator warns about separately.
//
// WHAT THIS CANNOT PROVE: that the wolf reads RIGHT on screen - scale, facing,
// grounding, whether it slides. Headless gates cannot see orientation (the 08-09
// lesson). That stays the owner's felt-verify / a device capture, per
// docs/TICKET_PIPELINE.md (PO closes, not CLI).
//
// Markers: FOUNDING_GUIDE_WOLF_OK / FOUNDING_GUIDE_WOLF_FAIL.
// Standalone: run-unity-method DeNelle.Editor.Regression.FoundingGuideWolfBodyRegression.RunAll
// Covenant contract Run(out reason) is DataRegression-shaped; wiring into
// DataRegression.RunAll is left to the committer (that file is lane-fenced).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using DeNelle.Core;

namespace DeNelle.Editor.Regression
{
    public static class FoundingGuideWolfBodyRegression
    {
        private const string Species        = "ice-wolf";
        private const string ResourcesDir   = "Assets/Resources/Pets";
        private const string BodyPrefab     = "Assets/Resources/Pets/ice-wolf.prefab";
        private const string ControllerPath = "Assets/Resources/Pets/ice-wolf.controller";
        private const string WolfFbx        = "Assets/Animals/Low Poly Animals/Simple Wolf/wolf.fbx";
        private const string RetiredFox     = "Assets/Art/Retired/Pets/ice-wolf-fox-legacy.fbx";
        private const string PetDeployerSrc = "Assets/_Modules/Pets/PetDeployer.cs";
        private const string TutorialFlowSrc = "Assets/_Modules/Village/Tutorial/V2/TutorialFlow.cs";
        private const string AnchorsSrc      = "Assets/_Modules/Village/Tutorial/V2/TutorialWorldAnchors.cs";
        // WO-1108 Lane B: the SINGLE owner of the Echo's appear/vanish/reappear transitions.
        // TutorialFlow no longer calls PetDeployer.SummonAt itself; it asks this owner, which
        // reaches the same SummonAt through the same EnsurePetDeployer self-heal.
        private const string PresenceSrc     = "Assets/_Modules/Village/World/Camps/EchoAutoDeployTrigger.cs";

        // The two clips the guide actually needs: idle at rest, run while leading.
        private const string IdleClip = "wolf_rig|idle2";
        private const string RunClip  = "wolf_rig|running";

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("FOUNDING_GUIDE_WOLF_OK - " + reason);
            else Debug.LogError("FOUNDING_GUIDE_WOLF_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            try
            {
                Case(failures, "one-body",   () => Case1_ExactlyOneBody(failures));
                Case(failures, "controller", () => Case2_ControllerResolves(failures));
                Case(failures, "no-magenta", () => Case3_NoMagentaClassMaterial(failures));
                Case(failures, "clips",      () => Case4_ClipsExist(failures));
                Case(failures, "guide-body", () => Case5_GuideBodyNotStewardFallback(failures));
                Case(failures, "device-spawn-look", () => Case6_DeviceSpawnAndLook(failures));
            }
            catch (Exception ex)
            {
                failures.Add("[suite] THREW " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "FOUNDING GUIDE WOLF OK - exactly one asset answers " +
                         "Resources.Load<GameObject>(\"Pets/" + Species + "\") (the Simple Wolf body; the " +
                         "fox/coyote FBX is retired out of Resources), a controller with a Speed-driven " +
                         "Idle<->Run pair resolves at Resources/Pets/" + Species + " where " +
                         "PetDeployer.WirePetAnimator probes first, every material on the body is a live " +
                         "URP shader (MagentaGuard.IsBrokenShader=false), both clips are present, the " +
                         "founding guide's body is summoned so world.guide resolves to a real Pet " +
                         "instead of the Sylas steward stand-in, and the roster DATA grant is untouched.";
                return true;
            }
            reason = "founding-guide-wolf FAIL x" + failures.Count + ": " + string.Join(" | ", failures);
            return false;
        }

        private static void Case(List<string> failures, string name, Action body)
        {
            try { body(); }
            catch (Exception ex) { failures.Add("[" + name + "] THREW " + ex.GetType().Name + ": " + ex.Message); }
        }

        // =====================================================================
        //  Case 1 - EXACTLY ONE asset answers Resources.Load<GameObject>("Pets/ice-wolf")
        // =====================================================================

        private static void Case1_ExactlyOneBody(List<string> failures)
        {
            if (!Directory.Exists(ResourcesDir))
            {
                failures.Add("[one-body] " + ResourcesDir + " does not exist - the pet Resources folder is gone.");
                return;
            }

            // Every asset in the folder whose EXTENSION-LESS name is the species id. That is the
            // exact key Resources.Load collides on - not the full filename.
            var collisions = Directory
                .GetFiles(ResourcesDir, Species + ".*", SearchOption.TopDirectoryOnly)
                .Select(p => p.Replace('\\', '/'))
                .Where(p => !p.EndsWith(".meta", StringComparison.OrdinalIgnoreCase))
                .Where(p => Path.GetFileNameWithoutExtension(p) == Species)
                .Where(p => typeof(GameObject).IsAssignableFrom(
                                AssetDatabase.GetMainAssetTypeAtPath(p) ?? typeof(object)))
                .ToList();

            if (collisions.Count != 1)
                failures.Add("[one-body] " + collisions.Count + " GameObject asset(s) answer " +
                             "Resources.Load<GameObject>(\"Pets/" + Species + "\") [" +
                             string.Join(", ", collisions) + "] - Resources.Load resolves by an " +
                             "extension-less path, so anything other than exactly ONE is ambiguous " +
                             "(the live Resources/Structures bug; do not reproduce it here).");

            if (collisions.Count == 1 && collisions[0] != BodyPrefab)
                failures.Add("[one-body] the single body answering \"Pets/" + Species + "\" is '" +
                             collisions[0] + "', expected '" + BodyPrefab + "' - WO-961 ships the " +
                             "Simple Wolf prefab, not the retired fox/coyote FBX.");

            var loaded = Resources.Load<GameObject>("Pets/" + Species);
            if (loaded == null)
                failures.Add("[one-body] Resources.Load<GameObject>(\"Pets/" + Species + "\") returned NULL - " +
                             "PetDeployer.TryLoadPetMesh would fall through to the billboard fallback and the " +
                             "FTUE's \"Follow {guide}\" beat would again have nothing to follow.");

            // The retired fox must be OUT of Resources (it is the wrong animal AND it is what
            // made the path ambiguous). Anywhere under Assets/Resources counts as a relapse.
            var strays = AssetDatabase.GetAllAssetPaths()
                .Where(p => p.StartsWith("Assets/Resources/", StringComparison.Ordinal))
                .Where(p => p.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                .Where(p => Path.GetFileNameWithoutExtension(p) == Species)
                .ToList();
            if (strays.Count > 0)
                failures.Add("[one-body] the fox/coyote FBX is back under Resources [" +
                             string.Join(", ", strays) + "] - it is a Coyote_Mesh on an AccuRig CC_Base " +
                             "HUMAN biped, cannot play the wolf's quadruped clips, and re-creates the " +
                             "Resources.Load ambiguity. It belongs at " + RetiredFox + ".");
        }

        // =====================================================================
        //  Case 2 - the controller resolves where WirePetAnimator actually probes
        // =====================================================================

        private static void Case2_ControllerResolves(List<string> failures)
        {
            var rac = Resources.Load<RuntimeAnimatorController>("Pets/" + Species);
            if (rac == null)
            {
                failures.Add("[controller] Resources.Load<RuntimeAnimatorController>(\"Pets/" + Species + "\") " +
                             "returned NULL - WirePetAnimator would fall through to Pets/Pet, Pets/PetIdle " +
                             "(both known-missing) and then the embedded-clip fallback, i.e. no idle<->run " +
                             "blend. Expected asset at " + ControllerPath + ".");
                return;
            }

            var ac = rac as AnimatorController;
            if (ac == null)
            {
                failures.Add("[controller] the asset at \"Pets/" + Species + "\" is a " + rac.GetType().Name +
                             ", not an AnimatorController - an override controller with no base animates nothing.");
                return;
            }

            if (!ac.parameters.Any(p => p.name == "Speed" && p.type == AnimatorControllerParameterType.Float))
                failures.Add("[controller] '" + ac.name + "' declares no float parameter 'Speed' - Pet.cs and " +
                             "PetAnimatorController drive locomotion through that exact name (both guard on " +
                             "its presence), so without it the wolf can only ever play its idle.");

            var layer = ac.layers != null && ac.layers.Length > 0 ? ac.layers[0] : null;
            if (layer == null || layer.stateMachine == null)
            {
                failures.Add("[controller] '" + ac.name + "' has no Base Layer state machine.");
                return;
            }

            var states = layer.stateMachine.states.Select(s => s.state).Where(s => s != null).ToList();
            var byName = states.ToDictionary(s => s.name, s => s);

            foreach (var required in new[] { "Idle", "Run" })
            {
                if (!byName.TryGetValue(required, out var st))
                {
                    failures.Add("[controller] '" + ac.name + "' has no '" + required + "' state - the guide " +
                                 "needs an idle at rest AND a run while leading (WO-961 acceptance #2).");
                    continue;
                }
                if (st.motion == null)
                    failures.Add("[controller] state '" + required + "' has a NULL motion - it binds cleanly " +
                                 "and animates nothing, which reads on screen as a bind-pose statue.");
            }

            if (layer.stateMachine.defaultState == null || layer.stateMachine.defaultState.name != "Idle")
                failures.Add("[controller] the default state is '" +
                             (layer.stateMachine.defaultState != null ? layer.stateMachine.defaultState.name : "<null>") +
                             "' - the wolf must settle into Idle at rest, not into a run or a fallen pose.");

            // The probe order itself, pinned at source: a refactor that drops the per-species lookup
            // would silently demote this controller to unreachable.
            string src = StripComments(File.ReadAllText(PetDeployerSrc));
            if (!Regex.IsMatch(src, @"Resources\.Load<RuntimeAnimatorController>\(\s*""Pets/""\s*\+\s*species\s*\)"))
                failures.Add("[controller] " + PetDeployerSrc + " no longer probes " +
                             "Resources.Load<RuntimeAnimatorController>(\"Pets/\" + species) - the per-species " +
                             "controller this ticket ships would never be found.");
            if (!Regex.IsMatch(src, @"Resources\.Load<GameObject>\(\s*""Pets/""\s*\+\s*def\.Species\s*\)"))
                failures.Add("[controller] " + PetDeployerSrc + " no longer loads the body via " +
                             "Resources.Load<GameObject>(\"Pets/\" + def.Species) - Case 1's whole invariant " +
                             "is about that exact key.");
        }

        // =====================================================================
        //  Case 3 - no magenta-class material anywhere on the body (QR-5.1)
        // =====================================================================

        private static void Case3_NoMagentaClassMaterial(List<string> failures)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BodyPrefab);
            if (prefab == null)
            {
                failures.Add("[no-magenta] no prefab at " + BodyPrefab + " - cannot inspect the body's materials.");
                return;
            }

            var renderers = prefab.GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0)
            {
                failures.Add("[no-magenta] the body at " + BodyPrefab + " has NO renderer - it would spawn invisible.");
                return;
            }

            int checkedSlots = 0;
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mats = r.sharedMaterials;
                if (mats == null || mats.Length == 0)
                {
                    failures.Add("[no-magenta] renderer '" + r.name + "' has no material slots.");
                    continue;
                }
                for (int i = 0; i < mats.Length; i++)
                {
                    var m = mats[i];
                    checkedSlots++;
                    if (m == null)
                    {
                        failures.Add("[no-magenta] renderer '" + r.name + "' slot " + i + " is NULL - under URP an " +
                                     "unassigned submesh draws with the engine default, i.e. MAGENTA.");
                        continue;
                    }
                    // THE single authority. Never re-implement this predicate (every local copy in
                    // this repo has drifted, notably by dropping the !isSupported / on-device branch).
                    if (MagentaGuard.IsBrokenShader(m.shader))
                    {
                        failures.Add("[no-magenta] material '" + m.name + "' on '" + r.name + "' slot " + i +
                                     " uses shader '" + (m.shader != null ? m.shader.name : "<null>") +
                                     "' which MagentaGuard.IsBrokenShader flags as magenta-class. A built-in " +
                                     "Standard/Legacy shader is STRIPPED from a URP player build and resolves " +
                                     "to Hidden/InternalErrorShader - the wolf would ship MAGENTA while looking " +
                                     "perfect in the editor. Convert it to Universal Render Pipeline/Lit " +
                                     "(_MainTex -> _BaseMap, keep _BumpMap/_OcclusionMap).");
                        continue;
                    }
                    if (m.shader == null || !m.shader.name.StartsWith("Universal Render Pipeline/", StringComparison.Ordinal))
                        failures.Add("[no-magenta] material '" + m.name + "' on '" + r.name + "' slot " + i +
                                     " is on shader '" + (m.shader != null ? m.shader.name : "<null>") +
                                     "' - not a URP shader. It may survive the strip today but it is not the " +
                                     "pipeline this project renders with.");
                    else if (m.HasProperty("_BaseMap") && m.GetTexture("_BaseMap") == null &&
                             m.HasProperty("_BaseColor") && m.GetColor("_BaseColor").a < 0.05f)
                        failures.Add("[no-magenta] material '" + m.name + "' has neither a _BaseMap nor an opaque " +
                                     "_BaseColor - a colourless lit surface reads as a white/lavender blob under " +
                                     "this project's ambient (the FLOOR-FIX class of defect).");
                }
            }

            if (checkedSlots == 0)
                failures.Add("[no-magenta] inspected 0 material slots - the check proved nothing.");
        }

        // =====================================================================
        //  Case 4 - the clips exist on the source FBX and are the ones bound
        // =====================================================================

        private static void Case4_ClipsExist(List<string> failures)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(WolfFbx) == null)
            {
                failures.Add("[clips] no model at " + WolfFbx + " - the Simple Wolf pack is not imported, so " +
                             "the body prefab's mesh, avatar and every clip dangle.");
                return;
            }

            var clips = AssetDatabase.LoadAllAssetsAtPath(WolfFbx)
                .OfType<AnimationClip>()
                .Where(c => c != null && !c.name.StartsWith("__preview__", StringComparison.Ordinal))
                .Select(c => c.name)
                .ToList();

            foreach (var required in new[] { IdleClip, RunClip })
                if (!clips.Contains(required))
                    failures.Add("[clips] " + WolfFbx + " carries no clip named '" + required + "' - present: [" +
                                 string.Join(", ", clips) + "]. The guide needs an idle at rest and a run while " +
                                 "leading; a controller pointing at a missing clip binds and animates nothing.");

            // And the controller must actually be pointing at THOSE clips, not at some other take.
            var ac = Resources.Load<RuntimeAnimatorController>("Pets/" + Species) as AnimatorController;
            if (ac == null || ac.layers == null || ac.layers.Length == 0 || ac.layers[0].stateMachine == null) return;

            var bound = ac.layers[0].stateMachine.states
                .Select(s => s.state)
                .Where(s => s != null && s.motion != null)
                .ToDictionary(s => s.name, s => s.motion.name);

            if (bound.TryGetValue("Idle", out string idleMotion) && idleMotion != IdleClip)
                failures.Add("[clips] the Idle state plays '" + idleMotion + "', expected '" + IdleClip + "'.");
            if (bound.TryGetValue("Run", out string runMotion) && runMotion != RunClip)
                failures.Add("[clips] the Run state plays '" + runMotion + "', expected '" + RunClip + "'.");
        }

        // =====================================================================
        //  Case 5 - the GUIDE resolves to a real Pet body, not the steward stand-in,
        //           and the roster DATA grant is untouched
        // =====================================================================

        // WHY THIS IS A SOURCE INVARIANT AND NOT A LIVE PROBE: proving it live means running the
        // FTUE's ARRIVE beat in a loaded hub scene with a GameStateService, a PetAcquisitionService
        // and a baked NavMesh - none of which exist in an editor batch, and a green tick over that
        // many nulls would be worth nothing. What CAN be pinned exactly is the wiring the owner's
        // F8 seq 2304 ("npc") proved was missing: the body is summoned at all, the resolver prefers
        // that body over the Sylas steward, the floating-spirit layer stays retired, and the data
        // grant the 07-17 ruling protects is still the untouched path it always was. The body
        // actually APPEARING is the owner's felt-verify.
        //
        // WO-1108 Lane B (2026-08-16) moved the SITE, not the invariants: the summon is now one hop
        // away, through the single appearance owner EchoWorldPresence.SummonEscortBody, which reaches
        // the same PetDeployer.SummonAt. Case (b) below follows that hop and asserts the SAME two
        // things at the new site -- the body is summoned, and the spawn is Guard-isolated so a
        // cosmetic failure can never take the roster grant down with it.
        private static void Case5_GuideBodyNotStewardFallback(List<string> failures)
        {
            string flow     = StripComments(File.ReadAllText(TutorialFlowSrc));
            string anchors  = StripComments(File.ReadAllText(AnchorsSrc));
            string presence = File.Exists(PresenceSrc)
                ? StripComments(File.ReadAllText(PresenceSrc)) : null;

            // -- (a) the species constant IS the body this ticket ships -------------------
            // The two halves of WO-961 are wired by a bare string on each side; nothing but this
            // check makes them agree. A constant pointing at a species with no Resources body is
            // exactly the state the FTUE shipped in (grant said 'aether-sprite', world said nothing).
            var m = Regex.Match(flow, @"StarterPetSpecies\s*=\s*""([^""]+)""");
            if (!m.Success)
                failures.Add("[guide-body] could not find the StarterPetSpecies constant in " + TutorialFlowSrc + ".");
            else if (m.Groups[1].Value != Species)
                failures.Add("[guide-body] StarterPetSpecies is '" + m.Groups[1].Value + "' but the body this " +
                             "ticket ships is '" + Species + "' (" + BodyPrefab + "). The grant would name a " +
                             "species whose body does not exist and the guide would have no world presence - " +
                             "the exact defect WO-961 fixes.");

            // -- (b) the body is actually summoned, guarded, before the roster grant ------
            //
            // RE-POINTED 2026-08-16 (WO-1108 Lane B), NOT relaxed. Both invariants below were
            // earned from owner F8 seq 2304 and BOTH still hold; only the SITE moved. Lane B made
            // EchoWorldPresence the single owner of the Echo's appear/vanish/reappear transitions,
            // so the grant now asks that owner (SummonEscortBody) instead of calling
            // PetDeployer.SummonAt inline. This case therefore follows the call one hop: the grant
            // must summon, the owner must actually reach SummonAt, and the spawn must still be
            // failure-isolated -- at whichever site now performs it.
            int summonAt = flow.IndexOf("EchoWorldPresence.SummonEscortBody(", StringComparison.Ordinal);
            if (summonAt < 0)
                failures.Add("[guide-body] " + TutorialFlowSrc + " no longer summons the guide's body " +
                             "(EchoWorldPresence.SummonEscortBody, the WO-1108 Lane B route to " +
                             "PetDeployer.SummonAt) - the founding guide has NO world body, so " +
                             "TutorialWorldAnchors falls through to the Sylas steward NPC and the beat tells " +
                             "the player to follow an unrelated townsperson (owner F8 seq 2304, message \"npc\").");

            if (presence == null)
                failures.Add("[guide-body] the appearance owner " + PresenceSrc + " is missing - the grant's " +
                             "SummonEscortBody call has nothing to reach and the guide gets no body.");
            else
            {
                // The one hop: the owner must really put a body in the world, not just exist.
                if (presence.IndexOf(".SummonAt(", StringComparison.Ordinal) < 0)
                    failures.Add("[guide-body] " + PresenceSrc + " no longer calls PetDeployer.SummonAt - " +
                                 "SummonEscortBody would return without a body and the beat would again point " +
                                 "at the steward stand-in (owner F8 seq 2304, message \"npc\").");

                // EVERY spawn site must be failure-isolated, not merely one of them: a failed visual
                // spawn must never throw out of the caller. In the grant's case that caller is the
                // roster grant itself - the body is cosmetic, the data grant is not.
                foreach (var statement in presence.Split(';'))
                {
                    if (statement.IndexOf(".SummonAt(", StringComparison.Ordinal) < 0) continue;
                    if (Regex.IsMatch(statement, @"Guard\.Try\s*(<[^>]*>)?\s*\(", RegexOptions.Singleline)) continue;
                    failures.Add("[guide-body] a PetDeployer.SummonAt call in " + PresenceSrc + " is not " +
                                 "Guard-wrapped - a failed visual spawn would throw out of the appearance " +
                                 "owner and, through SummonEscortBody, out of the starter-pet grant, taking " +
                                 "the roster entry down with it. The body is cosmetic; the data grant is not. " +
                                 "Offending statement: " +
                                 Regex.Replace(statement.Trim(), @"\s+", " "));
                }
            }

            // -- (c) the ROSTER DATA GRANT is untouched, and still runs AFTER the summon --
            if (!flow.Contains("state.StarterPetId ="))
                failures.Add("[guide-body] " + TutorialFlowSrc + " no longer records state.StarterPetId - that " +
                             "field is both the save's memory of the Echo AND the gate " +
                             "PetDeployer.HasChosenOrOwnedPet() checks, so dropping it silently disables the " +
                             "summon this case is about.");
            int acquire = flow.IndexOf("petSvc.Acquire(", StringComparison.Ordinal);
            if (acquire < 0)
                failures.Add("[guide-body] " + TutorialFlowSrc + " no longer calls " +
                             "PetAcquisitionService.Acquire - the roster grant (the half the 2026-07-17 " +
                             "portrait-card ruling protects) is gone.");
            else if (summonAt >= 0 && summonAt > acquire)
                failures.Add("[guide-body] the guide's body summon (EchoWorldPresence.SummonEscortBody) now " +
                             "runs AFTER petSvc.Acquire. The documented order is StarterPetId -> summon -> " +
                             "Acquire, so the slot redeploy sees the already-born body and never double-spawns " +
                             "the guide.");

            // -- (d) the floating-spirit layer stays retired on a quadruped ---------------
            //
            // WIDENED 2026-08-16 (WO-993), NOT relaxed. WO-993 DELETED EchoSpiritPresentation.cs
            // outright (owner: the guide is a grounded wolf that walks, not a floating spirit), so
            // the type no longer exists and this lint is now belt-and-braces against the FILE being
            // re-added. But the scan had a real GAP that Lane B opened the same day: it watched only
            // TutorialFlow, and the summon MOVED one hop to the appearance owner. A re-added
            // AddComponent<EchoSpiritPresentation>() at the new spawn site would have attached the
            // hover to the wolf with this case still green. Both sites are watched now.
            const string SpiritLayer = "EchoSpiritPresentation";
            const string SpiritWhy =
                " references " + SpiritLayer + ". That hover/yaw-drift/Aura_HeartPulse layer existed " +
                "to MASK the aether-sprite's missing idle; the ice wolf ships its own idle and run " +
                "clips, and a hovering quadruped is wrong (WO-961 scope, retired outright by WO-993). " +
                "Note this is the ECHO's use only - Aura_HeartPulse itself is NOT orphaned and the " +
                "Heart of Elarion keeps it.";
            if (flow.Contains(SpiritLayer))
                failures.Add("[guide-body] " + TutorialFlowSrc + SpiritWhy);
            if (presence != null && presence.Contains(SpiritLayer))
                failures.Add("[guide-body] " + PresenceSrc + SpiritWhy);

            // -- (e) the resolver probes the live body, and the steward link is GONE ------
            // HISTORY: this used to assert an ORDERING - Pet probed before the "Sylas" steward
            // stand-in, "both branches must exist, the steward is a legitimate fallback".
            // WO-971 (owner ruling 2026-08-10) DELETED the steward link, so the ordering
            // clause below is now vacuous by construction (steward < 0) and the stronger
            // "no second body link at all" invariant is enforced by OneGuideBodyRegression
            // [one-guide-body]. The Pet probe assertion still earns its place here.
            int petProbe = anchors.IndexOf("FindAnyObjectByType<DeNelle.Pets.Pet>", StringComparison.Ordinal);
            int steward  = anchors.IndexOf("\"Sylas\"", StringComparison.Ordinal);
            if (petProbe < 0)
                failures.Add("[guide-body] " + AnchorsSrc + " no longer probes for a live DeNelle.Pets.Pet - " +
                             "world.guide could never resolve to the guide's own body, only to a stand-in.");
            else if (steward >= 0 && petProbe > steward)
                failures.Add("[guide-body] " + AnchorsSrc + " probes the Sylas steward BEFORE the live Pet body, " +
                             "so the guide highlight would land on the steward NPC even once the wolf exists " +
                             "(owner F8 seq 2304). The live body must win.");
        }

        // Device regressions reported 2026-08-29: Aldwin spawned on top of the hero and
        // rebuilt as a flat white wolf. Pin the two player-only safeguards at their live sites.
        private static void Case6_DeviceSpawnAndLook(List<string> failures)
        {
            string presence = StripComments(File.ReadAllText(PresenceSrc));
            string deployer = StripComments(File.ReadAllText(PetDeployerSrc));

            if (!presence.Contains("ResolveSafeEscortSpawn(at)"))
                failures.Add("[device-spawn-look] escort summon no longer resolves a separated staging point before SummonAt; Aldwin can spawn on the hero again.");
            if (!presence.Contains("NavMesh.SamplePosition") ||
                !presence.Contains("EscortHeroSeparation") ||
                !presence.Contains("GameObject.FindGameObjectWithTag(\"Player\")"))
                failures.Add("[device-spawn-look] safe escort staging lost its Player-distance or NavMesh validity check.");

            if (!deployer.Contains("SetForcedSourceTexture(FindFirstAlbedo(visual))"))
                failures.Add("[device-spawn-look] ice-wolf no longer pins its authored source albedo before the Android material rebuild; it can degrade to flat white again.");

            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(BodyPrefab);
            var renderer = prefab != null ? prefab.GetComponentInChildren<Renderer>(true) : null;
            bool hasAlbedo = false;
            if (renderer != null)
                foreach (var mat in renderer.sharedMaterials)
                    if (mat != null && ((mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null) ||
                                        (mat.HasProperty("_MainTex") && mat.GetTexture("_MainTex") != null)))
                        { hasAlbedo = true; break; }
            if (!hasAlbedo)
                failures.Add("[device-spawn-look] shipped ice-wolf prefab has no authored albedo for SetForcedSourceTexture to preserve.");
        }

        /// <summary>Strip // line and /* */ block comments so a lint never matches doc text.</summary>
        private static string StripComments(string src)
        {
            src = Regex.Replace(src, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
            src = Regex.Replace(src, @"//[^\r\n]*", string.Empty);
            return src;
        }
    }
}
