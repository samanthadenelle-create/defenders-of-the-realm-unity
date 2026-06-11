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

            // Vendor / station UI verbs (WO-106/109/291/304) — formerly on the now-dead
            // NPCCommandBridge. Consolidated here onto the SINGLE live runner so each Yarn
            // action name is registered exactly ONCE project-wide (the YarnSpinner source
            // generator throws on a duplicate name across IActionRegistration methods).
            Reg("OpenShop",       (Action<string>)CmdOpenShop);
            Reg("OpenUpgrade",    (Action<string>)CmdOpenUpgrade);
            Reg("OpenCraft",      (Action<string>)CmdOpenCraft);
            Reg("OpenEquip",      (Action)CmdOpenEquip);
            Reg("OpenArena",      (Action)CmdOpenArena);
            Reg("OpenRumorBoard", (Action)CmdOpenRumorBoard);
            Reg("LearnRecipe",    (Action<string>)CmdLearnRecipe);

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

        private void Reg(string name, Delegate handler) =>
            ((IActionRegistration)_runner).AddCommandHandler(name, handler);

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
            var vs = _runner != null ? _runner.VariableStorage : null;
            if (vs == null) return;

            // The Yarn command arg arrives as the LITERAL "$structureId" (a bare command-arg doesn't
            // interpolate in this Yarn build) — so use the real id PlayStructure stashed. This was a
            // long-standing bug: structure_status never saw a real id → resource buildings never
            // resolved (no Upgrade line, wrong shop gate). Fixed via DialogueService.CurrentStructureId.
            string structureId = DialogueService.CurrentStructureId;

            // NOTE: $structureName is seeded by DialogueService.PlayStructure from the
            // building's sign label (DisplayLabel) and intentionally NOT overwritten here,
            // so the dialogue title matches the world sign. We only fill yield/cost/level.
            var def = Prog.ResourceBuildingProgression.Find(structureId);

            // WO-413: resource/production buildings (farm/lumbermill/forge) ARE the upgradable ones →
            // they show Upgrade/Talk/Leave, never a Buy/Sell shop. Non-resource (def==null) can shop.
            vs.SetValue("$structureCanShop", def == null);
            if (def == null)
            {
                vs.SetValue("$structureLevel", 0f);
                vs.SetValue("$structureYield", 0f);
                vs.SetValue("$upgradeCostText", "");
                vs.SetValue("$structureMaxed", true);   // non-resource building → no upgrade option
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

            deployer.DeployChosen(species);
            Debug.Log($"[DialogueCommandBridge] spawn_named_pet '{species}' → deployed via PetDeployer.");
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

        // ── Vendor / station UI verbs (consolidated from the dead NPCCommandBridge) ──
        // Each opens the relevant code-built panel, self-healing a host if none exists
        // (the panels build their own Canvas). Behaviour identical to the old bridge.
        private void CmdOpenShop(string vendor)
        {
            Debug.Log($"[DialogueCommandBridge] OpenShop: {vendor}");
            var panel = FindObjectOfType<DeNelle.Village.Hero.ShopPanel>();
            if (panel == null)
                panel = new GameObject("ShopPanelHost").AddComponent<DeNelle.Village.Hero.ShopPanel>();
            panel.Open(vendor);
        }

        private void CmdOpenUpgrade(string stationType)
        {
            Debug.Log($"[DialogueCommandBridge] OpenUpgrade: {stationType}");
            var panel = FindObjectOfType<DeNelle.Village.Buildings.Progression.BuildingUpgradePanel>();
            if (panel == null)
                panel = new GameObject("BuildingUpgradePanelHost").AddComponent<DeNelle.Village.Buildings.Progression.BuildingUpgradePanel>();
            if (!string.IsNullOrEmpty(stationType)) panel.OpenFocused(stationType);
            else panel.Open();
        }

        private void CmdOpenCraft(string context)
        {
            Debug.Log($"[DialogueCommandBridge] OpenCraft: {context}");
            var panel = FindObjectOfType<DeNelle.Village.Crafting.VillageCraftingPanel>();
            if (panel == null)
                panel = new GameObject("VillageCraftingPanelHost").AddComponent<DeNelle.Village.Crafting.VillageCraftingPanel>();
            panel.Open();
        }

        private void CmdOpenEquip()
        {
            Debug.Log("[DialogueCommandBridge] OpenEquip");
            var panel = FindObjectOfType<DeNelle.Village.Hero.EquipmentPanel>();
            if (panel == null)
                panel = new GameObject("EquipmentPanelHost").AddComponent<DeNelle.Village.Hero.EquipmentPanel>();
            panel.Open();
        }

        private void CmdOpenArena()
        {
            Debug.Log("[DialogueCommandBridge] OpenArena");
            var panel = FindObjectOfType<DeNelle.Village.Arena.ArenaPanel>();
            if (panel == null)
                panel = new GameObject("ArenaPanelHost").AddComponent<DeNelle.Village.Arena.ArenaPanel>();
            panel.Open();
        }

        private void CmdOpenRumorBoard()
        {
            Debug.Log("[DialogueCommandBridge] OpenRumorBoard");
            var panel = FindObjectOfType<DeNelle.Village.Hero.RumorBoardPanel>();
            if (panel == null)
                panel = new GameObject("RumorBoardPanelHost").AddComponent<DeNelle.Village.Hero.RumorBoardPanel>();
            panel.Open();
        }

        private void CmdLearnRecipe(string recipeId)
            => Debug.Log($"[DialogueCommandBridge] Learned recipe: {recipeId}");

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
