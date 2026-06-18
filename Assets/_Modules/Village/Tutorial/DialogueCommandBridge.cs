// =============================================================================
// DialogueCommandBridge — registers EVERY custom Yarn command CompanionMeeting.yarn
// calls and DELEGATES each to the real game systems (DEF-265 follow-up).
// -----------------------------------------------------------------------------
// The FTUE dialogue auto-runs on village entry (CompanionMeetingTrigger hosts the
// DialogueSystem prefab). Its ~30 custom commands (camera_focus, start_autowalk,
// spawn_wave_at_nearest, grant_resources_for_towers, wait_for_event, …) had NO C#
// handlers, so the dialogue errored + soft-locked the village ("WebGL crash on
// village load"). This bridge wires them — for REAL, into the systems that already
// exist:
//   movement/tour   → TutorialAutoWalk (HeroLocomotion.SetAutoWalk)
//   scripted wave    → TutorialWaveSpawner (WaveManager.SpawnEnemyForExternalMode)
//   pet name/role    → PetIntroduction (code-built prompt → GameState.PetName + Pet.Mode)
//   camera           → SmartMobileCamera (SetTarget / Shake)
//   resources        → GameStateService.AddCrystals
//   objective/hint/UI→ TutorialHudOverlay
//   gameplay waits    → DialogueEventBus (tower_placed) + spawner/pet completion
//
// Yarn's blocking model: a handler returning IEnumerator pauses the dialogue until
// the coroutine finishes — so start_autowalk-arrivals, the scripted wave, the pet
// prompt, and player actions (place a tower) genuinely GATE the narrative. Every
// wait has a safety timeout so a never-completed beat can never hard-hang the FTUE.
//
// Installed by CompanionMeetingTrigger BEFORE the runner autostarts (registration
// must precede DialogueRunner.Start). Sub-systems + scene refs are resolved LAZILY
// (at command time) so init order never matters. All cross-calls null-guarded.
// =============================================================================

using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using DeNelle.Core;
using DeNelle.Core.Combat;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Quests;
using DeNelle.Core.State;
using DeNelle.Pets;
using UnityEngine;
using Yarn.Unity;
using Prog = DeNelle.Village.Buildings.Progression;

namespace DeNelle.Village
{
    /// <summary>
    /// Registers + services all Yarn FTUE commands by delegating to the existing
    /// Tutorial* / camera / economy / pet systems. One per hosted DialogueRunner.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DialogueCommandBridge : MonoBehaviour
    {
        // Crystals granted per "tower" by grant_resources_for_towers N.
        private const int TowerCrystalCost = 50;
        // How long a camera_focus/glance holds before easing back to the hero.
        private const float CameraFocusHold = 2.6f;
        // Safety: a wait_for_event that never resolves un-sticks after this long.
        private const float EventWaitTimeout = 120f;

        private DialogueRunner _runner;

        private TutorialAutoWalk _autoWalk;
        private TutorialWaveSpawner _waveSpawner;
        private PetIntroduction _petIntro;
        private TutorialHudOverlay _overlay;

        private bool _petBegun;
        private bool _towerSubscribed;
        private Coroutine _cameraRestore;

        // ── Install ──────────────────────────────────────────────────────────

        /// <summary>
        /// Register every command on <paramref name="runner"/> and seed the
        /// $companionName variable. Call BEFORE the runner starts dialogue.
        /// </summary>
        public void Install(DialogueRunner runner)
        {
            if (runner == null) { Debug.LogWarning("[DialogueCommandBridge] null runner — nothing wired."); return; }
            _runner = runner;

            RegisterCommands();

            // Seed $companionName now AND on dialogue start (program-load may reset
            // declared variables to their defaults — the start hook wins).
            SeedCompanionName();
            _runner.onDialogueStart?.AddListener(SeedCompanionName);

            // WO-438 (owner 2026-06-17): open the Inn rumor board from C# on node-start
            // instead of a yarn `<<OpenRumorBoard>>` command. The yarn command opened the
            // board as the FIRST statement of the `RumorBoard` node, racing the VM's
            // Continue() into Brom's narration → the "No node has been selected" no-node
            // fault on the clean fleet. Driving it from the node-start hook keeps Brom's
            // rumor narration intact (the node runs clean, no orphaned Continue) while the
            // board still surfaces the moment the rumor node begins.
            _runner.onNodeStart?.AddListener(OnNodeStartedOpenPanels);

            Debug.Log("[DialogueCommandBridge] Installed — FTUE commands wired to live systems.");
        }

        private void RegisterCommands()
        {
            // Camera
            Reg("camera_focus",          (Action<string>)CmdCameraFocus);
            Reg("camera_glance",         (Action<string>)CmdCameraFocus);
            Reg("camera_shake",          (Action<float>)CmdCameraShake);
            Reg("camera_show_all_gates", (Action)CmdCameraShowAllGates);
            Reg("camera_return_to_hero", (Action)CmdCameraReturnToHero);

            // Audio
            Reg("play_sfx",   (Action<string>)CmdPlaySfx);
            Reg("play_music", (Action<string>)CmdPlayMusic);

            // Structure interaction (the parameterized building hook — one node, $structureId)
            Reg("portrait",          (Action<string>)CmdPortrait);
            Reg("structure_status",  (Action<string>)CmdStructureStatus);
            Reg("structure_upgrade", (Action<string>)CmdStructureUpgrade);
            Reg("TryUpgradeBuilding", (Action<string, int>)CmdTryUpgradeBuilding);   // WO-430 city upgrades
            Reg("structure_talk",    (Action<string>)CmdStructureTalk);

            // Movement / control
            Reg("start_autowalk",      (Action<string>)CmdStartAutowalk);
            Reg("stop_autowalk",       (Action)CmdStopAutowalk);
            Reg("enable_player_input", (Action)CmdEnablePlayerInput);
            Reg("enable_full_controls",(Action)CmdEnableFullControls);

            // HUD objective / hint / highlight
            Reg("set_hud_objective", (Action<string, int, int>)CmdSetHudObjective);
            Reg("set_hud_hint",      (Action<string>)CmdSetHudHint);
            Reg("highlight_ui",      (Action<string>)CmdHighlightUi);
            Reg("unhighlight_ui",    (Action<string>)CmdUnhighlightUi);

            // Combat / economy
            Reg("spawn_wave_at_nearest",      (Action<int>)CmdSpawnWaveAtNearest);
            Reg("grant_resources_for_towers", (Action<int>)CmdGrantResourcesForTowers);

            // Pets
            Reg("spawn_starting_pet",   (Action)CmdSpawnStartingPet);
            Reg("spawn_named_pet",      (Action<string>)CmdSpawnNamedPet);
            Reg("show_pet_name_prompt", (Action)CmdShowPetNamePrompt);
            Reg("show_pet_role_choice", (Action)CmdShowPetRoleChoice);
            Reg("send_pet_to_harvest",  (Action)CmdSendPetToHarvest);

            // Quests (WO-290 — general story-quest verbs → QuestService)
            Reg("StartQuest",    (Action<string>)CmdStartQuest);
            Reg("AdvanceQuest",  (Action<string>)CmdAdvanceQuest);
            Reg("CompleteQuest", (Action<string>)CmdCompleteQuest);
            Reg("SetQuestFlag",  (Action<string, string>)CmdSetQuestFlag);
            Reg("GiveKeystone",  (Action<string>)CmdGiveKeystone);
            // WO-238 — companion recruit verb: enrol a companion class into the persisted
            // party roster (the proven join API, same as ElaraWaveThreeJoin). Fires
            // PlayerChanged → StoryCompanionInjector spawns the body that follows + fights.
            Reg("RecruitCompanion", (Action<string>)CmdRecruitCompanion);
            // Yarn functions for branching on keystones (<<if HasKeystone("x")>>).
            ((IActionRegistration)_runner).AddFunction("HasKeystone",  (Func<string, bool>)FnHasKeystone);
            ((IActionRegistration)_runner).AddFunction("KeystoneCount", (Func<int>)FnKeystoneCount);
            // WO-291 — quest-state READ functions for stage-aware vendor Talk branching
            // against the live QuestService ledger (survives save/reload).
            ((IActionRegistration)_runner).AddFunction("IsQuestActive",   (Func<string, bool>)FnIsQuestActive);
            ((IActionRegistration)_runner).AddFunction("IsQuestComplete", (Func<string, bool>)FnIsQuestComplete);
            // Pet-house gate: lets the Echo Hollow node branch on whether the player
            // already attuned (owns) a given echo species, so the unlock option is
            // hidden / replaced by an "already bonded" line on re-visit instead of
            // re-firing the grant. Mirrors the keystone/quest read-function pattern.
            ((IActionRegistration)_runner).AddFunction("pet_owned", (Func<string, bool>)FnPetOwned);
            // A7 re-select fix: the Echo Hollow gates its WHOLE selection on this. The design
            // grants one Echo deploy slot to start (PetAcquisitionService.DefaultMaxSlots == 1);
            // a returning player who already bonded an Echo must NOT be able to attune a SECOND
            // species until a free slot opens (Fenn's questline raises MaxSlots). True when the
            // player owns at least one Echo AND has no free deploy slot — the node then shows a
            // "you already have <pet>" line and offers no fresh selection options.
            ((IActionRegistration)_runner).AddFunction("pet_select_closed", (Func<bool>)FnPetSelectClosed);
            // Display name of the Echo the player already bonded — for the "already have <pet>" line.
            ((IActionRegistration)_runner).AddFunction("owned_pet_name", (Func<string>)FnOwnedPetName);

            // Vendor / station UI verbs (WO-106/109/291/304) — formerly on the now-dead
            // NPCCommandBridge. Consolidated here onto the SINGLE live runner so each Yarn
            // action name is registered exactly ONCE project-wide (the YarnSpinner source
            // generator throws on a duplicate name across IActionRegistration methods).
            Reg("OpenShop",       (Func<string, IEnumerator>)CmdOpenShop);
            Reg("OpenUpgrade",    (Func<string, IEnumerator>)CmdOpenUpgrade);
            Reg("OpenCraft",      (Func<string, IEnumerator>)CmdOpenCraft);
            Reg("OpenEquip",      (Func<IEnumerator>)CmdOpenEquip);
            Reg("OpenArena",      (Func<IEnumerator>)CmdOpenArena);
            Reg("OpenRumorBoard", (Func<IEnumerator>)CmdOpenRumorBoard);
            Reg("LearnRecipe",    (Action<string>)CmdLearnRecipe);

            // Barracks troop-training (WO-453) — the Barracks_MainMenu Yarn node fires
            // <<ShowTrainingUI>> to open the code-built training panel and (optionally)
            // <<StartTraining troopId qty>> to train directly. Both delegate to the
            // single TroopDialogueCommands home (which wires the ArmyStorage seams to
            // TroopCatalog + EconomyService). Registered ONCE here on the shared runner.
            Reg("ShowTrainingUI", (Action)CmdShowTrainingUI);
            Reg("StartTraining",  (Action<string, int>)CmdStartTraining);

            // Misc / meta
            Reg("save_game",            (Action)CmdSaveGame);

            // World-NPC commands (other dialogue nodes) — registered so they never
            // error; lightweight delegations / safe acknowledgements.
            Reg("spawn_npc",            (Action<string, string, string>)CmdSpawnNpc);
            Reg("move_npc",             (Action<string, string, string>)CmdMoveNpc);
            Reg("grant_pet",            (Action<string, string>)CmdGrantPet);
            Reg("grant_elder_blessing", (Action)CmdGrantElderBlessing);
            Reg("transition_to",        (Action<string>)CmdTransitionTo);

            // Blocking — pauses the dialogue until the real gameplay beat resolves.
            Reg("wait_for_event", (Func<string, IEnumerator>)CmdWaitForEvent);
        }

        // ROOT-CAUSE FIX (T-031 / T-019 — Yarn v3 SignalContentComplete re-entrancy):
        // -------------------------------------------------------------------------
        // Yarn v3's DialogueRunner.OnCommandReceivedAsync handles a SYNCHRONOUS command
        // (a plain Action handler that returns inline) by calling Dialogue.SignalContentComplete()
        // and THEN Dialogue.Continue() on the SAME call stack (DialogueRunner.cs ~L703/746).
        // That inline Continue() pumps the VM straight into the NEXT piece of content — and if
        // that next content is ANOTHER synchronous command (two back-to-back <<cmd>> lines, which
        // ~50 of our nodes have: every vendor "AdvanceQuest then GiveKeystone", the FTUE, the saga,
        // the soul awakenings, …), it dispatches command #2 nested INSIDE command #1's dispatch.
        // When the inner delivery advances the VM, its _executionState leaves DeliveringContent,
        // so the OUTER frame's completion path then calls SignalContentComplete() while the VM is
        // no longer dispatching a command → InvalidOperationException
        // ("SignalContentComplete can only be called when a command is being dispatched"),
        // which ABORTS the running dialogue (Talk dies; re-entering restarts the node but the
        // option stack is corrupted → "breaks other options"). The earlier folded-portrait patch
        // only fixed ONE site (StructureMenu node entry); the hazard is project-wide.
        //
        // The fix routes EVERY non-blocking command through a one-frame coroutine. The VM then sees
        // the command as ASYNC (CommandDispatchResult.Task is not IsCompletedSuccessfully on the same
        // frame — WaitForCoroutine completes via a YarnTaskCompletionSource next frame), so it takes
        // the `await dispatchResult.Task` branch instead of the inline SignalContentComplete+Continue.
        // The VM resumes on a CLEAN stack next frame — never nesting one command dispatch inside
        // another — so SignalContentComplete can never fire out of context. This makes the contract
        // safe for ALL current and FUTURE two-command nodes without editing every .yarn file.
        //
        // wait_for_event is registered DIRECTLY (it already returns IEnumerator and gates for real).
        // The original handler's work still runs synchronously within the first coroutine step, so
        // command ordering and side effects (set portrait, advance quest, open panel) are unchanged.

        // Register a command, wrapping plain Action-family handlers so the VM dispatches them
        // asynchronously (one frame) — eliminating the inline command-dispatch re-entrancy.
        private void Reg(string name, Delegate handler)
        {
            // IEnumerator/Task/Coroutine-returning handlers are already async to the VM — pass through.
            Type ret = handler.Method.ReturnType;
            bool alreadyAsync =
                typeof(IEnumerator).IsAssignableFrom(ret) ||
                typeof(Coroutine).IsAssignableFrom(ret) ||
                typeof(System.Threading.Tasks.Task).IsAssignableFrom(ret) ||
                ret == typeof(Cysharp.Threading.Tasks.UniTask);

            if (alreadyAsync)
            {
                ((IActionRegistration)_runner).AddCommandHandler(name, handler);
                return;
            }

            RegisterDeferred(name, handler);
        }

        // Wrap a void/Action-family handler into a Func<…, IEnumerator> of the SAME arity so the
        // Yarn source-generator/dispatcher sees the correct parameter count, while the VM treats it
        // as an async (coroutine) command. Args flow through to the original handler via DynamicInvoke
        // (commands fire only on dialogue beats, never per-frame — the reflection cost is irrelevant).
        private void RegisterDeferred(string name, Delegate inner)
        {
            int argc = inner.Method.GetParameters().Length;
            var reg = (IActionRegistration)_runner;
            switch (argc)
            {
                case 0:
                    reg.AddCommandHandler(name, (Func<IEnumerator>)(() => RunDeferred(inner)));
                    break;
                case 1:
                    reg.AddCommandHandler(name, (Func<string, IEnumerator>)((a) => RunDeferred(inner, a)));
                    break;
                case 2:
                    reg.AddCommandHandler(name, (Func<string, string, IEnumerator>)((a, b) => RunDeferred(inner, a, b)));
                    break;
                case 3:
                    reg.AddCommandHandler(name, (Func<string, string, string, IEnumerator>)((a, b, c) => RunDeferred(inner, a, b, c)));
                    break;
                default:
                    // Unsupported arity — register directly (synchronous) rather than drop the command.
                    Debug.LogWarning($"[DialogueCommandBridge] '{name}' has {argc} params — registered synchronously (no deferral wrapper for this arity).");
                    reg.AddCommandHandler(name, inner);
                    break;
            }
        }

        // The shared deferral coroutine: run the original handler THIS frame (preserving ordering and
        // side effects), then yield a frame so the VM's completion runs on a fresh stack. Args arrive
        // as strings from Yarn; DynamicInvoke coerces them to the handler's parameter types (int/float/
        // string) exactly as the direct registration would have.
        private IEnumerator RunDeferred(Delegate inner, params object[] rawArgs)
        {
            // INSTRUMENT (§12): name every deferred (sync) command as it dispatches. A captured
            // "DialogueException: No node has been selected" fires AFTER a command that ENDS/stops
            // the dialogue, when the VM then tries to Continue() — so the command logged immediately
            // before that exception in Player.log is the culprit. The 6 Open* + wait_for_event
            // commands bypass this path (registered async), so any remaining offender is a sync one
            // that stops the dialogue (suspects: transition_to, enable_full_controls). Cheap: fires
            // only on dialogue beats, never per-frame.
            FlowTrace.Step("Yarn", $"dispatch command '{inner.Method.Name}'");

            object[] args = CoerceArgs(inner, rawArgs);
            try { inner.DynamicInvoke(args); }
            catch (Exception ex)
            {
                Debug.LogError($"[DialogueCommandBridge] command '{inner.Method.Name}' threw — dialogue continues. " + ex);
            }
            yield return null;
        }

        // Convert the string args Yarn passes into the parameter types the original handler declares
        // (Yarn itself would do this for a directly-registered typed handler; we do it here because the
        // wrapper receives strings). Unparseable values fall back to default so a beat never hard-faults.
        private static object[] CoerceArgs(Delegate inner, object[] rawArgs)
        {
            var ps = inner.Method.GetParameters();
            var outArgs = new object[ps.Length];
            for (int i = 0; i < ps.Length; i++)
            {
                Type t = ps[i].ParameterType;
                string s = (rawArgs != null && i < rawArgs.Length) ? rawArgs[i] as string : null;
                if (t == typeof(string)) { outArgs[i] = s; }
                else if (t == typeof(int)) { outArgs[i] = int.TryParse(s, out int iv) ? iv : 0; }
                else if (t == typeof(float)) { outArgs[i] = float.TryParse(s, out float fv) ? fv : 0f; }
                else if (t == typeof(bool)) { outArgs[i] = bool.TryParse(s, out bool bv) && bv; }
                else { outArgs[i] = s; }
            }
            return outArgs;
        }

        // ── Camera ───────────────────────────────────────────────────────────

        private void CmdCameraFocus(string targetName)
        {
            Transform t = ResolveTransform(targetName);
            if (t == null) { Debug.Log($"[DialogueCommandBridge] camera_focus '{targetName}' unresolved — skipping."); return; }

            SmartMobileCamera.Instance?.SetTarget(t);
            if (_cameraRestore != null) StopCoroutine(_cameraRestore);
            _cameraRestore = StartCoroutine(RestoreCameraAfter(CameraFocusHold));
        }

        private void CmdCameraShake(float intensity)
        {
            SmartMobileCamera.Instance?.Shake(Mathf.Clamp(intensity, 0.05f, 0.6f), 0.45f);
        }

        private void CmdCameraShowAllGates()
        {
            // SmartMobileCamera exposes no "frame everything"; a soft cue keeps the
            // beat readable without a jarring fly-out we can't smoothly recover.
            SmartMobileCamera.Instance?.Shake(0.08f, 0.3f);
        }

        private void CmdCameraReturnToHero()
        {
            if (_cameraRestore != null) { StopCoroutine(_cameraRestore); _cameraRestore = null; }
            RestoreCameraToHero();
        }

        private IEnumerator RestoreCameraAfter(float hold)
        {
            yield return new WaitForSeconds(hold);
            RestoreCameraToHero();
            _cameraRestore = null;
        }

        private void RestoreCameraToHero()
        {
            Transform hero = ResolveTransform("hero");
            if (hero != null) SmartMobileCamera.Instance?.SetTarget(hero);
        }

        // ── Audio ────────────────────────────────────────────────────────────

        private void CmdPlaySfx(string id)
        {
            // Map the named Yarn SFX to real cues where we have them; fall back to a
            // UI blip so an unmapped name is audible punctuation, never a soft-lock.
            switch (id)
            {
                case "horn_warning":
                    GameSfx.PlayLookoutHorn();   // the FTUE previews the real lookout horn
                    break;
                default:
                    CoreServices.Audio?.PlayUiClick();
                    break;
            }
        }

        private void CmdPlayMusic(string id)
        {
            // Intentionally inert: the village music track is already playing and a
            // mismatched swap would be worse than leaving it. Logged for visibility.
            Debug.Log($"[DialogueCommandBridge] play_music '{id}' — keeping current village track.");
        }

        // ── Structure interaction (parameterized building hook) ──────────────
        // <<portrait Portraits/{$structureId}>> — show the building's NPC portrait.
        private void CmdPortrait(string path) => DeNelle.Core.DialoguePortrait.Forced = path;

        // <<structure_status $structureId>> — seed the Yarn vars the StructureMenu node
        // reads, scoped to THIS building's own domain data (so it can never show
        // out-of-domain options). Resource buildings (farm/lumbermill/forge) get a live
        // upgrade line; others (market/pet-house) fall back to name-only (no upgrade).
        private void CmdStructureStatus(string _argUnused)
        {
            // The Yarn command arg arrives as the LITERAL "$structureId" (a bare command-arg doesn't
            // interpolate in this Yarn build) — so use the real id PlayStructure stashed. This was a
            // long-standing bug: structure_status never saw a real id → resource buildings never
            // resolved (no Upgrade line, wrong shop gate). Fixed via DialogueService.CurrentStructureId.
            string structureId = DialogueService.CurrentStructureId;

            // Force the speaker portrait HERE (folded in from the old <<portrait Portraits/{$structureId}>>
            // entry command), UNCONDITIONALLY — before the variable-storage guard — so it matches the
            // original <<portrait>> behaviour. StructureMenu used to fire TWO back-to-back synchronous
            // commands at node entry; under Yarn v3 the first command's OnCommandReceivedAsync calls
            // Dialogue.Continue(), which pumps the VM into the second command inline, and the nested
            // SignalContentComplete throws "can only be called when a command is being dispatched" —
            // aborting Talk entirely. Folding the portrait into this single command keeps the prologue
            // to ONE command so it can't re-enter itself. (PetHouse keeps its own single <<portrait>>.)
            if (!string.IsNullOrEmpty(structureId))
                DeNelle.Core.DialoguePortrait.Forced = "Portraits/" + structureId;

            var vs = _runner != null ? _runner.VariableStorage : null;
            if (vs == null) return;

            // WO-413 capability model — read the building's DECLARED caps from the catalog
            // (DeNelle.Village.BuildingCatalog, same assembly). The menu gates Buy/Sell on IsShoppable,
            // the Upgrade option on IsUpgradable, and (Phase 2) routes the Upgrade flow by UpgradeType
            // (resource / gear / spells). Forge = shop + upgrade; arcane = upgrade(spells); market = shop.
            var caps = DeNelle.Village.BuildingCatalog.Find(structureId);
            // WO-413: a building that is NOT in the catalog (id mismatch) must NEVER fall through
            // to a Buy/Sell shop — shop is opt-IN only (explicit IsShoppable on a real entry). A
            // city-upgrade building (in BuildingTierCatalog) is upgradable even with no shop entry,
            // so it still surfaces its Upgrade option rather than collapsing to Talk/Leave.
            bool isCityUpgrade  = BuildingTierCatalog.IsUpgradable(structureId);
            bool canShop        = caps != null && caps.IsShoppable;                  // opt-in only
            bool canUpgrade     = (caps != null && caps.IsUpgradable) || isCityUpgrade;
            vs.SetValue("$structureCanShop",    canShop);
            vs.SetValue("$structureCanUpgrade", canUpgrade);
            vs.SetValue("$upgradeType",         caps != null && caps.UpgradeType != null ? caps.UpgradeType : "");
            // WO-430 — seed the city-upgrade gate var ($<id>_Level) from the saved tier so the
            // *_UpgradeMenu node gates correctly on open (CmdTryUpgradeBuilding updates it after a buy).
            vs.SetValue(LevelVarName(structureId), (float)ModifierService.TierOf(structureId));
            // WO-430 — is this a city-upgrade building? StructureMenu routes its Upgrade option to
            // BuildingUpgradeRouter (the tier tree) when true, and hides the legacy upgrade option.
            vs.SetValue("$isCityUpgrade", isCityUpgrade);

            // §12 / WO-413: capture the ACTUAL menu gating per building so a "wrongly offers shop"
            // report is decided by data, not theory. catalog-miss => caps null (shop forced false).
            FlowTrace.Step("Village", $"StructureMenu gates for '{structureId}': " +
                $"caps={(caps != null ? "hit" : "MISS")} shop={canShop} upgrade={canUpgrade} " +
                $"cityUpgrade={isCityUpgrade} upgradeType='{(caps != null ? caps.UpgradeType : "")}'");

            // NOTE: $structureName is seeded by DialogueService.PlayStructure from the building's sign
            // label and intentionally NOT overwritten here. Resource-upgrade buildings (farm/lumbermill/
            // forge) get a live level/cost/maxed line below; other upgradables (arcane → spells) have no
            // resource cost here — Phase 2 opens their own flow — so they are never "maxed".
            var def = Prog.ResourceBuildingProgression.Find(structureId);
            if (def == null)
            {
                vs.SetValue("$structureLevel", 0f);
                vs.SetValue("$structureYield", 0f);
                vs.SetValue("$upgradeCostText", "");
                vs.SetValue("$structureMaxed", false);   // $structureCanUpgrade now gates the option
                return;
            }

            int level = Prog.ResourceBuildingState.GetLevel(structureId);
            bool maxed = Prog.ResourceBuildingState.IsMaxLevel(structureId);
            var lvlDef = Prog.ResourceBuildingState.CurrentDef(structureId);

            vs.SetValue("$structureLevel", level);
            vs.SetValue("$structureYield", Prog.ResourceBuildingState.CurrentYield(structureId));
            vs.SetValue("$upgradeCostText", maxed || lvlDef == null ? "" : FormatCost(lvlDef.UpgradeCost, lvlDef.MagicCost));
            vs.SetValue("$structureMaxed", maxed);
        }

        // <<structure_upgrade $structureId>> — spend + level up via the existing economy
        // backend (ResourceBuildingState/ResourceLedger). Sets $upgradeResult for the node.
        private void CmdStructureUpgrade(string structureId)
        {
            var vs = _runner != null ? _runner.VariableStorage : null;
            if (vs == null) return;

            var result = Prog.ResourceBuildingState.TryUpgrade(structureId);
            string msg;
            switch (result)
            {
                case Prog.UpgradeResult.Upgraded:
                    msg = $"Upgraded to Level {Prog.ResourceBuildingState.GetLevel(structureId)}.";
                    break;
                case Prog.UpgradeResult.Insufficient: msg = "You can't afford that yet."; break;
                case Prog.UpgradeResult.MaxLevel:     msg = "This is already at its highest level."; break;
                case Prog.UpgradeResult.NeedMagic:    msg = "That tier needs Magic to unlock."; break;
                default:                          msg = "Nothing to upgrade here."; break;
            }
            vs.SetValue("$upgradeResult", msg);
        }

        // WO-430 — tier-targeted CITY upgrade. Yarn: <<TryUpgradeBuilding "arcane-tower" 2>>.
        // Buys the NEXT tier (targetTier must equal current+1) when affordable: spends the
        // catalog cost via EconomyService, writes GameState.BuildingTiers, saves, recomputes
        // the active GameModifiers, and updates the Yarn $<id>_Level gate var + $lastUpgradeOk.
        // No-op (ok=false) if it's not the next tier or the player can't afford it.
        private void CmdTryUpgradeBuilding(string buildingId, int targetTier)
        {
            var vs = _runner != null ? _runner.VariableStorage : null;

            // Shared execute (extracted): same spend -> write tier -> save -> recompute the
            // command used to inline. The MVVM upgrade panel calls the SAME path so they
            // can never diverge. The Yarn-var bookkeeping below stays here (Yarn-only).
            bool ok = Prog.BuildingUpgradeService.TryUpgrade(buildingId, targetTier);

            int level = ModifierService.TierOf(buildingId);
            if (vs != null)
            {
                vs.SetValue(LevelVarName(buildingId), (float)level);
                vs.SetValue("$lastUpgradeOk", ok);
            }
            FlowTrace.Step("UI", $"TryUpgradeBuilding('{buildingId}', {targetTier}) -> {(ok ? "OK level " + level : "no-op")}");
        }

        // The Yarn level-gate var for a building: "$<id>_Level" with hyphens -> underscores
        // (creative convention — $arcane_tower_Level / $armorer_Level / …).
        private static string LevelVarName(string buildingId)
            => "$" + (buildingId ?? string.Empty).Replace("-", "_") + "_Level";

        // <<structure_talk $structureId>> — narrative/questline seed (stub for now).
        private void CmdStructureTalk(string structureId)
            => Debug.Log($"[DialogueCommandBridge] structure_talk '{structureId}' — questline hook (stub).");

        private static string Titleize(string id)
        {
            if (string.IsNullOrEmpty(id)) return "Building";
            id = id.Replace('-', ' ').Replace('_', ' ');
            return char.ToUpper(id[0]) + (id.Length > 1 ? id.Substring(1) : "");
        }

        private static string FormatCost(System.Collections.Generic.IReadOnlyList<Prog.ResourceCost> costs, int magic)
        {
            var parts = new System.Collections.Generic.List<string>();
            if (costs != null)
                foreach (var c in costs)
                    parts.Add($"{c.Amount} {Prog.ResourceBuildingProgression.LabelFor(c.Resource)}");
            if (magic > 0) parts.Add($"{magic} Magic");
            return parts.Count > 0 ? string.Join(", ", parts) : "free";
        }

        // ── Movement / control ───────────────────────────────────────────────

        private void CmdStartAutowalk(string destName)
        {
            EnsureAutoWalk();
            Vector3? pos = ResolvePosition(destName);
            if (pos.HasValue) _autoWalk.WalkTo(pos.Value);
            else Debug.Log($"[DialogueCommandBridge] start_autowalk '{destName}' unresolved — staying put.");
        }

        private void CmdStopAutowalk() => _autoWalk?.Stop();

        private void CmdEnablePlayerInput() => _autoWalk?.Stop();

        private void CmdEnableFullControls()
        {
            _autoWalk?.Stop();
            // Yarn owns the FTUE→gameplay handoff: finish onboarding (opens the wave
            // loop) exactly once, at the END of the narrative — not mid-dialogue.
            var svc = GameStateService.Instance;
            if (svc != null && svc.State != null && !svc.State.Onboarded)
                svc.FinishOnboarding();
        }

        // ── HUD ──────────────────────────────────────────────────────────────

        private void CmdSetHudObjective(string title, int current, int max)
        {
            EnsureOverlay();
            _overlay.SetObjective(title, current, max);
        }

        private void CmdSetHudHint(string text)
        {
            EnsureOverlay();
            _overlay.SetHint(text);
        }

        private void CmdHighlightUi(string element)
        {
            EnsureOverlay();
            _overlay.Highlight(element, true);
        }

        private void CmdUnhighlightUi(string element)
        {
            EnsureOverlay();
            _overlay.Highlight(element, false);
        }

        // ── Combat / economy ─────────────────────────────────────────────────

        private void CmdSpawnWaveAtNearest(int count)
        {
            EnsureWaveSpawner();
            WaveSpawnPoint sp = NearestSpawnPoint();
            if (sp == null) { Debug.LogWarning("[DialogueCommandBridge] spawn_wave_at_nearest — no WaveSpawnPoint found."); return; }
            _waveSpawner.SpawnAt(sp, Mathf.Max(1, count)).Forget();
        }

        private void CmdGrantResourcesForTowers(int count)
        {
            int crystals = Mathf.Max(0, count) * TowerCrystalCost;
            GameStateService.Instance?.AddCrystals(crystals);
            Debug.Log($"[DialogueCommandBridge] grant_resources_for_towers {count} → +{crystals} crystals.");
        }

        // ── Pets ─────────────────────────────────────────────────────────────

        private void CmdSpawnStartingPet()
        {
            var deployer = EnsurePetDeployer();
            if (deployer != null) deployer.DeployStarterPets();
            else Debug.LogWarning("[DialogueCommandBridge] spawn_starting_pet — could not create a PetDeployer.");
        }

        // <<spawn_named_pet "species">> — pet-house "name + select your pet" flow.
        // Deploys the CHOSEN species via the self-healed deployer (records the pick
        // to GameState.StarterPetId so it persists). The deploy-once guard makes a
        // repeat call a safe no-op, so re-visiting the pet house never respawns.
        //
        // OWNERSHIP GATE: the Echo Hollow Yarn node already hides the unlock option
        // for an owned species via <<if not pet_owned(...)>> — but we ALSO gate here
        // so the grant can never double-fire even if the node is reached another way.
        // Acquire() is the idempotent ownership funnel (PetAcquisitionService): the
        // FIRST attune records the species in GameState.Pets + OwnedPets and returns
        // true; any repeat is a no-op (false). Either way we still call DeployChosen
        // so the chosen Warden is the live starter + is (re)deployed at the Heart.
        // TODO: free-text pet name UI — Yarn can't capture text, so the player picks
        // a species (and gets the catalog name) for now; a code-built name prompt
        // (cf. PetIntroduction) would write GameState.PetName before DeployChosen.
        private void CmdSpawnNamedPet(string species)
        {
            // PET-GRANT HARDENING (owner 2026-06-13): a Yarn command bridge must NEVER
            // throw — an exception here propagates into the YarnSpinner runner and can
            // halt the whole dialogue (the player is then stuck at the pet-house with a
            // dead conversation). So: (1) reject an empty/whitespace arg with a log+skip
            // (a bad <<spawn_named_pet>> call must not silently deploy nothing), and
            // (2) wrap the grant+deploy in a try/catch so any downstream surprise
            // (catalog miss, null GameState, deployer build failure) degrades to a
            // warning instead of breaking the dialogue.
            if (string.IsNullOrWhiteSpace(species))
            {
                Debug.LogWarning("[DialogueCommandBridge] spawn_named_pet — empty/blank species arg; " +
                                 "skipping (nothing deployed). Pass a species id, e.g. <<spawn_named_pet \"ice-wolf\">>.");
                return;
            }

            try
            {
                var deployer = EnsurePetDeployer();
                if (deployer == null)
                {
                    Debug.LogWarning("[DialogueCommandBridge] spawn_named_pet — could not create a PetDeployer.");
                    return;
                }

                // Record ownership exactly once (idempotent). Acquire is the canonical
                // grant primitive; PetAcquisitionService bootstraps itself on load, but
                // null-guard in case it isn't present yet.
                var acq = DeNelle.Pets.PetAcquisitionService.Instance;
                if (acq != null)
                {
                    bool firstTime = acq.Acquire(species, DeNelle.Pets.PetAcquisitionSource.Gift);
                    if (!firstTime)
                        Debug.Log($"[DialogueCommandBridge] spawn_named_pet '{species}' — already owned; not re-granting.");
                }
                else
                {
                    // No acquisition service yet (not bootstrapped). DeployChosen still
                    // records StarterPetId so the chosen pet persists + deploys — but the
                    // canonical roster (Pets/OwnedPets) won't be written until the service
                    // exists. Surface the gap rather than hiding it.
                    Debug.LogWarning("[DialogueCommandBridge] spawn_named_pet — PetAcquisitionService " +
                                     "not present; deploying via StarterPetId only (roster not recorded).");
                }

                deployer.DeployChosen(species);
                Debug.Log($"[DialogueCommandBridge] spawn_named_pet '{species}' → deployed via PetDeployer.");
            }
            catch (System.Exception e)
            {
                // Never let the exception escape into the Yarn runner.
                Debug.LogWarning($"[DialogueCommandBridge] spawn_named_pet '{species}' failed: {e.Message}");
            }
        }

        // Self-heal (GAP 1): Village2 ships no PetDeployer + no runtime bootstrap, so
        // FindObjectOfType returned null and the spawn no-op'd. Reuse the existing one
        // if present; otherwise build it exactly like PatriciaLightController.SpawnPets
        // — new GameObject("PetDeployer") + SetHeartPosition(Heart/origin) + SetEnemyMask
        // (project "Enemy" layer). The deployer's own deploy-once guard keeps repeat
        // FTUE calls (spawn_starting_pet fires at start AND end) safe.
        private PetDeployer EnsurePetDeployer()
        {
            var deployer = FindObjectOfType<PetDeployer>();
            if (deployer != null) return deployer;

            var go = new GameObject("PetDeployer");
            deployer = go.AddComponent<PetDeployer>();

            // Heart position = centre of the deploy ring. Use the live HeartController
            // if the village has one; otherwise the scene origin (Heart sits at 0,0,0).
            Vector3 heartPos = Vector3.zero;
            var heart = FindObjectOfType<HeartController>();
            if (heart != null) heartPos = heart.transform.position;
            deployer.SetHeartPosition(heartPos);

            // Enemy LayerMask — project layer "Enemy" (mirrors PatriciaLight + VillageSceneBuilder).
            int enemyLayer = LayerMask.NameToLayer("Enemy");
            deployer.SetEnemyMask(enemyLayer >= 0 ? (1 << enemyLayer) : ~0);

            // Per-species bond ranks from the save (indexed [aether, flame, ice]).
            var svc = GameStateService.Instance;
            if (svc != null && svc.State != null && svc.State.PetBonds != null)
            {
                var b = svc.State.PetBonds;
                int aether = b.Count > 0 ? b[0] : 0;
                int flame  = b.Count > 1 ? b[1] : 0;
                int ice    = b.Count > 2 ? b[2] : 0;
                deployer.SetBondRanks(aether, flame, ice);
            }

            Debug.Log($"[DialogueCommandBridge] Self-healed a PetDeployer (heart={heartPos}).");
            return deployer;
        }

        private void CmdShowPetNamePrompt()
        {
            EnsurePetIntro();
            if (_petBegun) return;
            _petBegun = true;
            _petIntro.Begin();
        }

        private void CmdShowPetRoleChoice()
        {
            // PetIntroduction.Begin() already prompts BOTH name + role in one card,
            // so this only ensures the prompt is up if name-prompt was skipped.
            EnsurePetIntro();
            if (_petBegun) return;
            _petBegun = true;
            _petIntro.Begin();
        }

        private void CmdSendPetToHarvest()
        {
            var pet = FindObjectOfType<Pet>();
            if (pet != null) pet.Mode = PetMode.Idle; // Idle ⇒ PetHarvester auto-gathers
        }

        // ── Misc / meta ──────────────────────────────────────────────────────

        private void CmdSaveGame() => GameStateService.Instance?.Save();

        // ── Quests (WO-290) ──────────────────────────────────────────────────
        private void CmdStartQuest(string id)        => QuestService.Instance?.StartQuest(id);
        private void CmdAdvanceQuest(string id)       => QuestService.Instance?.AdvanceQuest(id);
        private void CmdCompleteQuest(string id)      => QuestService.Instance?.CompleteQuest(id);
        private void CmdSetQuestFlag(string id, string flag) => QuestService.Instance?.SetFlag(id, flag);
        private void CmdGiveKeystone(string name)     => QuestService.Instance?.GiveKeystone(name);

        // WO-238 — <<command: RecruitCompanion Ranger>>: enrol the companion class into
        // the persisted party roster via the same join API the wave-3/return joins use.
        // AddToParty is idempotent (a member already in the party is a no-op) and fires
        // PlayerChanged, which StoryCompanionInjector listens to → spawns the companion
        // body (follows + fights) and PartyHudBridge grows the party frame. Null-guarded.
        private void CmdRecruitCompanion(string companionClass)
        {
            if (string.IsNullOrEmpty(companionClass)) return;
            FlowTrace.Step("Roster", $"RecruitCompanion command fired (Yarn) for '{companionClass}' caller=CmdRecruitCompanion -> AddToParty");
            GameStateService.Instance?.AddToParty(companionClass);
            Debug.Log($"[DialogueCommandBridge] RecruitCompanion '{companionClass}' → AddToParty.");
        }
        private static bool FnHasKeystone(string name) => QuestService.Instance != null && QuestService.Instance.HasKeystone(name);
        private static int FnKeystoneCount() => QuestService.Instance != null ? QuestService.Instance.KeystoneCount : 0;

        // WO-291 — read the live quest ledger for stage-aware vendor Talk branching.
        private static bool FnIsQuestActive(string id) =>
            QuestService.Instance != null && QuestService.Instance.IsActive(id);
        private static bool FnIsQuestComplete(string id) =>
            QuestService.Instance != null && QuestService.Instance.IsCompleted(id);

        // Pet-house gate: true when the player has already attuned (owns) this echo
        // species — the Echo Hollow node hides its unlock option with
        // <<if not pet_owned("ice-wolf")>>. Reads the canonical roster ownership
        // (PetAcquisitionService.Owns → GameState.Pets/OwnedPets) AND falls back to
        // GameState.StarterPetId so a pre-roster save (where the pet-house flow only
        // recorded the chosen starter id) still reads its starter as owned.
        private static bool FnPetOwned(string species)
        {
            if (string.IsNullOrEmpty(species)) return false;

            var acq = DeNelle.Pets.PetAcquisitionService.Instance;
            if (acq != null && acq.Owns(species)) return true;

            // StarterPetId fallback — the pet-house pick persists as the catalog id
            // ("pet-<species>"); resolve it back to a species and compare.
            var svc = GameStateService.Instance;
            string starterId = svc != null && svc.State != null ? svc.State.StarterPetId : null;
            if (string.IsNullOrEmpty(starterId)) return false;
            var def = DeNelle.Pets.PetCatalog.Find(starterId);
            return def != null && !string.IsNullOrEmpty(def.Species) &&
                   string.Equals(def.Species, species.Trim(), StringComparison.OrdinalIgnoreCase);
        }

        // A7: returns the species id of the Echo the player already owns, or null. Checks the
        // catalog species against PetAcquisitionService.Owns (canonical roster) and falls back to
        // the StarterPetId (legacy/intro saves). First match wins — the design grants one Echo.
        private static string FnOwnedSpecies()
        {
            var acq = DeNelle.Pets.PetAcquisitionService.Instance;
            if (acq != null)
                foreach (var def in DeNelle.Pets.PetCatalog.Pets)
                    if (def != null && !string.IsNullOrEmpty(def.Species) && acq.Owns(def.Species))
                        return def.Species;

            var svc = GameStateService.Instance;
            string starterId = svc != null && svc.State != null ? svc.State.StarterPetId : null;
            if (string.IsNullOrEmpty(starterId)) return null;
            var sDef = DeNelle.Pets.PetCatalog.Find(starterId);
            return sDef != null && !string.IsNullOrEmpty(sDef.Species) ? sDef.Species : null;
        }

        // A7 whole-node gate: TRUE when the player already owns an Echo AND has no free deploy slot,
        // so the Echo Hollow must NOT offer a second attune (re-select). The design starts with one
        // slot (DefaultMaxSlots); Fenn's questline raises MaxSlots, which re-opens selection. With no
        // PetAcquisitionService instance we fall back to "owns any" so selection still closes after a
        // pick (a fresh second pick can't be granted into a single slot anyway).
        private static bool FnPetSelectClosed()
        {
            if (string.IsNullOrEmpty(FnOwnedSpecies())) return false; // owns nothing → selection open
            var acq = DeNelle.Pets.PetAcquisitionService.Instance;
            if (acq == null) return true;                              // owns one, no service → closed
            return acq.FilledSlotCount >= acq.MaxSlots;                // closed only when no slot is free
        }

        // A7: the display name of the Echo the player already bonded, for the "already have <pet>"
        // line. Falls back to the species id, then a generic word, so the line is never blank.
        private static string FnOwnedPetName()
        {
            string species = FnOwnedSpecies();
            if (string.IsNullOrEmpty(species)) return "your Echo";
            var def = DeNelle.Pets.PetCatalog.FindBySpecies(species);
            return def != null && !string.IsNullOrEmpty(def.Name) ? def.Name : species;
        }

        // ── Vendor / station UI verbs (consolidated from the dead NPCCommandBridge) ──
        // Each opens the relevant code-built panel, self-healing a host if none exists
        // (the panels build their own Canvas). Behaviour identical to the old bridge.
        // WO-437: an NPC/Yarn verb must NOT pop a gameplay panel mid-battle. Each
        // panel-open verb checks this first and no-ops (skips the find-or-spawn-and-open)
        // while an ATB/Arena battle is active. The dialogue's own <<stop>> still ends the
        // conversation cleanly; we just don't surface the panel.
        private static bool PanelOpenBlockedByBattle(string verb)
        {
            if (!BattleLock.IsInBattle()) return false;
            FlowTrace.Warn("Input", "battle-lock: Yarn verb '" + verb + "' skipped (in battle)");
            return true;
        }

        private IEnumerator CmdOpenShop(string vendor)
        {
            if (PanelOpenBlockedByBattle("OpenShop")) yield break;

            // Yarn bare command-arg trap: the StructureMenu node fires <<OpenShop $structureId>>,
            // but a bare command-arg does NOT interpolate in this Yarn build — `vendor` arrives as
            // the LITERAL "$structureId" (the shop then read that as the vendor and showed generic
            // placeholder text, e.g. the Oathkeeper/structure storefront). Same trap CmdStructureStatus
            // already handles. The hardcoded NPC vendors pass a REAL quoted string ("armorer",
            // "forge", …) which must be preserved — so only fall back to the C#-stashed structure id
            // when the arg is an uninterpolated Yarn var ("$…") or empty.
            if (string.IsNullOrEmpty(vendor) || vendor.StartsWith("$"))
                vendor = DialogueService.CurrentStructureId;

            Debug.Log($"[DialogueCommandBridge] OpenShop: {vendor}");

            // PartyShop flag (docs/STORE_EQUIP_SPEC.md): when ON, route a WEAPON/ARMOR vendor to the
            // native code-built MVVM PartyShopPanelMvvm (party selector + tap-filter + unified single-
            // tap buy/equip/sell + real item images + buff deltas) via PanelRouter, SUPPRESSING the
            // legacy ShopPanel so the two never double-open. General-goods (potion-only) vendors keep
            // the legacy path (PartyShop is gear-only). Flag OFF -> the legacy ShopPanel below, unchanged.
            if (DeNelle.Core.FeatureFlags.PartyShop)
            {
                var kinds = DeNelle.Village.VendorStockContract.AllowedFor((vendor ?? "").ToLowerInvariant());
                bool gearVendor = (kinds & (DeNelle.Village.GearKind.Weapon | DeNelle.Village.GearKind.Armor)) != 0;
                if (gearVendor && DeNelle.Core.UI.PanelRouter.Open(DeNelle.Core.UI.PanelId.PartyShop, vendor))
                    yield break;   // PartyShop opened — do NOT also open the legacy ShopPanel.
            }

            var panel = FindObjectOfType<DeNelle.Village.Hero.ShopPanel>();
            if (panel == null)
                panel = new GameObject("ShopPanelHost").AddComponent<DeNelle.Village.Hero.ShopPanel>();
            // FIX B: pass the storefront's own sign LABEL (seeded by PlayStructure) so the panel
            // header reads the vendor's display name, not just the id-derived title — prevents the
            // "wrong storefront title" class of bug. Null/empty label safely falls back to the id.
            panel.Open(vendor, DialogueService.CurrentStructureName);
            // PERMANENT FIX (YarnSpinner-documented; memory yarn-no-node-stop-after-panel-command):
            // open the panel and RETURN. Do NOT call DialogueService.Stop() here — calling the runner's
            // Stop() from INSIDE an awaited command races its post-command Dialogue.Continue() and throws
            // "No node has been selected" (this exact bug, 42x/9 sessions). The conversation is now ended
            // cleanly by the built-in <<stop>> that follows <<OpenShop>> in every .yarn vendor node, which
            // completes via the normal onDialogueComplete path (presenter/portrait teardown included).
            yield return null;   // yield only so Reg registers this as an async command
        }

        // The 5 panel-open siblings: open the panel + return. Same permanent fix as CmdOpenShop — the
        // .yarn node's <<stop>> after the verb ends the dialogue; the command must NOT call Stop().
        private IEnumerator CmdOpenUpgrade(string stationType)
        {
            if (PanelOpenBlockedByBattle("OpenUpgrade")) yield break;
            Debug.Log($"[DialogueCommandBridge] OpenUpgrade: {stationType}");
            var panel = FindObjectOfType<DeNelle.Village.Buildings.Progression.BuildingUpgradePanel>();
            if (panel == null)
                panel = new GameObject("BuildingUpgradePanelHost").AddComponent<DeNelle.Village.Buildings.Progression.BuildingUpgradePanel>();
            if (!string.IsNullOrEmpty(stationType)) panel.OpenFocused(stationType);
            else panel.Open();
            yield return null;   // panel opens; the node's <<stop>> ends the dialogue (no Stop() here)
        }

        private IEnumerator CmdOpenCraft(string context)
        {
            if (PanelOpenBlockedByBattle("OpenCraft")) yield break;
            Debug.Log($"[DialogueCommandBridge] OpenCraft: {context}");
            var panel = FindObjectOfType<DeNelle.Village.Crafting.VillageCraftingPanel>();
            if (panel == null)
                panel = new GameObject("VillageCraftingPanelHost").AddComponent<DeNelle.Village.Crafting.VillageCraftingPanel>();
            panel.Open();
            yield return null;   // panel opens; the node's <<stop>> ends the dialogue (no Stop() here)
        }

        private IEnumerator CmdOpenEquip()
        {
            if (PanelOpenBlockedByBattle("OpenEquip")) yield break;
            Debug.Log("[DialogueCommandBridge] OpenEquip");
            var panel = FindObjectOfType<DeNelle.Village.Hero.EquipmentPanel>();
            if (panel == null)
                panel = new GameObject("EquipmentPanelHost").AddComponent<DeNelle.Village.Hero.EquipmentPanel>();
            panel.Open();
            yield return null;   // panel opens; the node's <<stop>> ends the dialogue (no Stop() here)
        }

        private IEnumerator CmdOpenArena()
        {
            if (PanelOpenBlockedByBattle("OpenArena")) yield break;
            Debug.Log("[DialogueCommandBridge] OpenArena");
            var panel = FindObjectOfType<DeNelle.Village.Arena.ArenaPanel>();
            if (panel == null)
                panel = new GameObject("ArenaPanelHost").AddComponent<DeNelle.Village.Arena.ArenaPanel>();
            panel.Open();
            yield return null;   // panel opens; the node's <<stop>> ends the dialogue (no Stop() here)
        }

        private IEnumerator CmdOpenRumorBoard()
        {
            OpenRumorBoard();
            yield return null;   // panel opens; the node's <<stop>> ends the dialogue (no Stop() here)
        }

        /// <summary>Find-or-spawn + open the rumor board. Shared by the (legacy) yarn
        /// command and the WO-438 node-start hook so both drive the same surface.</summary>
        private void OpenRumorBoard()
        {
            if (PanelOpenBlockedByBattle("OpenRumorBoard")) return;
            Debug.Log("[DialogueCommandBridge] OpenRumorBoard");
            var panel = FindObjectOfType<DeNelle.Village.Hero.RumorBoardPanel>();
            if (panel == null)
                panel = new GameObject("RumorBoardPanelHost").AddComponent<DeNelle.Village.Hero.RumorBoardPanel>();
            panel.Open();
        }

        // WO-438: node-start hook (registered on _runner.onNodeStart). Opens the rumor
        // board the moment the `RumorBoard` node begins — replacing the in-node yarn
        // `<<OpenRumorBoard>>` command that raced the VM into Brom's narration. Brom's
        // rumor lines now run clean (no orphaned Continue / no-node fault).
        private void OnNodeStartedOpenPanels(string nodeName)
        {
            if (string.Equals(nodeName, "RumorBoard", StringComparison.Ordinal))
                OpenRumorBoard();
        }

        private void CmdLearnRecipe(string recipeId)
            => Debug.Log($"[DialogueCommandBridge] Learned recipe: {recipeId}");

        // Barracks troop-training (WO-453). Both delegate to the single
        // TroopDialogueCommands home so the army-seam wiring lives in one place.
        private void CmdShowTrainingUI()
        {
            if (PanelOpenBlockedByBattle("ShowTrainingUI")) return;
            TroopDialogueCommands.ShowTrainingUI();
        }
        private void CmdStartTraining(string troopId, int qty) => TroopDialogueCommands.StartTraining(troopId, qty);

        private void CmdSpawnNpc(string who, string atWord, string where) =>
            Debug.Log($"[DialogueCommandBridge] spawn_npc '{who}' at '{where}' (companion auto-spawns; ambient NPCs are a follow-up).");

        private void CmdMoveNpc(string who, string toWord, string where) =>
            Debug.Log($"[DialogueCommandBridge] move_npc '{who}' to '{where}'.");

        private void CmdGrantPet(string species, string petName) =>
            Debug.Log($"[DialogueCommandBridge] grant_pet '{species}' \"{petName}\" (starter pet flows through spawn_starting_pet + PetIntroduction).");

        private void CmdGrantElderBlessing() =>
            Debug.Log("[DialogueCommandBridge] grant_elder_blessing.");

        private void CmdTransitionTo(string scene) =>
            Debug.Log($"[DialogueCommandBridge] transition_to '{scene}' — not transitioning from the village FTUE.");

        // ── Blocking wait ────────────────────────────────────────────────────

        private IEnumerator CmdWaitForEvent(string evt)
        {
            if (string.IsNullOrEmpty(evt)) yield break;
            DialogueEventBus.Clear(evt);
            float deadline = Time.time + EventWaitTimeout;

            while (Time.time < deadline)
            {
                if (evt.Equals("wave_cleared", StringComparison.OrdinalIgnoreCase))
                {
                    if (_waveSpawner != null && _waveSpawner.IsCleared) break;
                }
                else if (evt.Equals("pet_named", StringComparison.OrdinalIgnoreCase) ||
                         evt.Equals("pet_role_chosen", StringComparison.OrdinalIgnoreCase))
                {
                    if (_petIntro != null && _petIntro.IsComplete) { SeedPetName(); break; }
                }
                else
                {
                    if (DialogueEventBus.HasFired(evt)) break;
                }
                yield return null;
            }

            if (Time.time >= deadline)
                Debug.LogWarning($"[DialogueCommandBridge] wait_for_event '{evt}' timed out ({EventWaitTimeout}s) — continuing so the FTUE never wedges.");

            DialogueEventBus.Clear(evt);
        }

        // ── Yarn variables ───────────────────────────────────────────────────

        private void SeedCompanionName()
        {
            if (_runner == null || _runner.VariableStorage == null) return;
            _runner.VariableStorage.SetValue("$companionName", CompanionShortName());
        }

        private void SeedPetName()
        {
            if (_runner == null || _runner.VariableStorage == null || _petIntro == null) return;
            string name = _petIntro.PetName;
            if (!string.IsNullOrEmpty(name))
                _runner.VariableStorage.SetValue("$petName", name);
        }

        private static string CompanionShortName()
        {
            HeroClass player = HeroClass.Knight;
            var svc = GameStateService.Instance;
            if (svc != null && svc.State != null)
                player = svc.State.HeroClass.ToNullable() ?? HeroClass.Knight;

            switch (CompanionSpawner.CompanionClassFor(player))
            {
                case HeroClass.Knight: return "Grom";
                case HeroClass.Ranger: return "Sylas";
                case HeroClass.Mage:   return "Thrain";
                case HeroClass.Cleric: return "Elara";
                default:               return "your guide";
            }
        }

        // ── Lazy sub-system creation ─────────────────────────────────────────

        private void EnsureAutoWalk()
        {
            if (_autoWalk == null) _autoWalk = gameObject.AddComponent<TutorialAutoWalk>();
            var hero = FindObjectOfType<HeroLocomotion>();
            if (hero != null) _autoWalk.SetHero(hero);
        }

        private void EnsureWaveSpawner()
        {
            if (_waveSpawner == null) _waveSpawner = gameObject.AddComponent<TutorialWaveSpawner>();
            var wave = FindObjectOfType<WaveManager>();
            if (wave != null) _waveSpawner.SetWaveManager(wave);
        }

        private void EnsurePetIntro()
        {
            if (_petIntro == null) _petIntro = gameObject.AddComponent<PetIntroduction>();
        }

        private void EnsureOverlay()
        {
            if (_overlay == null) _overlay = gameObject.AddComponent<TutorialHudOverlay>();
        }

        // ── Resolution ───────────────────────────────────────────────────────

        // Resolve a Yarn target name to a live Transform (camera focus / glance).
        private Transform ResolveTransform(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            string n = name.ToLowerInvariant();

            switch (n)
            {
                case "companion":
                    return StoryCompanionInjector.Instance != null
                        ? StoryCompanionInjector.Instance.CompanionTransform : null;
                case "pet":
                {
                    var pet = FindObjectOfType<Pet>();
                    return pet != null ? pet.transform : null;
                }
                case "hero":
                case "player":
                    return HeroTransform();
                case "village_tour":
                {
                    var heart = FindObjectOfType<HeartController>();
                    return heart != null ? heart.transform : HeroTransform();
                }
            }

            if (n.StartsWith("gate_"))
            {
                if (int.TryParse(n.Substring("gate_".Length), out int oneBased))
                {
                    var sp = SpawnPointForGate(oneBased - 1);
                    if (sp != null) return sp.transform;
                }
            }

            // Buildings (forge / arcane_tower / pet_house / market): best-effort
            // by GameObject name; unresolved → null (caller logs + skips).
            var go = GameObject.Find(name) ?? GameObject.Find(name.Replace('_', ' '));
            return go != null ? go.transform : null;
        }

        private Vector3? ResolvePosition(string name)
        {
            Transform t = ResolveTransform(name);
            return t != null ? t.position : (Vector3?)null;
        }

        private Transform HeroTransform()
        {
            var loco = FindObjectOfType<HeroLocomotion>();
            if (loco != null) return loco.transform;
            var tagged = GameObject.FindWithTag("Player");
            return tagged != null ? tagged.transform : null;
        }

        private static WaveSpawnPoint SpawnPointForGate(int gateIndex)
        {
            var pts = FindObjectsByType<WaveSpawnPoint>(FindObjectsSortMode.None);
            if (pts == null || pts.Length == 0) return null;
            foreach (var p in pts)
                if (p != null && p.GateIndex == gateIndex) return p;
            // Fall back to the Nth point if indices don't line up.
            int idx = Mathf.Clamp(gateIndex, 0, pts.Length - 1);
            return pts[idx];
        }

        private WaveSpawnPoint NearestSpawnPoint()
        {
            var pts = FindObjectsByType<WaveSpawnPoint>(FindObjectsSortMode.None);
            if (pts == null || pts.Length == 0) return null;

            Transform hero = HeroTransform();
            Vector3 from = hero != null ? hero.position : Vector3.zero;

            WaveSpawnPoint best = null;
            float bestSq = float.MaxValue;
            foreach (var p in pts)
            {
                if (p == null) continue;
                float d = (p.transform.position - from).sqrMagnitude;
                if (d < bestSq) { bestSq = d; best = p; }
            }
            return best;
        }

        // ── Tower-placement event bridge ─────────────────────────────────────

        private void Update()
        {
            // Subscribe to tower placement once the system exists, then forward it to
            // the bus so <<wait_for_event tower_placed>> resolves on the real action.
            if (!_towerSubscribed)
            {
                var tps = TowerPlacementSystem.Instance;
                if (tps != null)
                {
                    tps.OnTowerPlaced += _ => DialogueEventBus.Raise("tower_placed");
                    _towerSubscribed = true;
                }
            }
        }
    }
}
