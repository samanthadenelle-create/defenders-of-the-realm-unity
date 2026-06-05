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
using DeNelle.Core.State;
using DeNelle.Pets;
using UnityEngine;
using Yarn.Unity;

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
            Reg("show_pet_name_prompt", (Action)CmdShowPetNamePrompt);
            Reg("show_pet_role_choice", (Action)CmdShowPetRoleChoice);
            Reg("send_pet_to_harvest",  (Action)CmdSendPetToHarvest);

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
            // No string→clip table yet; a UI blip gives audible punctuation without
            // mis-mapping. (Real SFX mapping is a follow-up, not a soft-lock risk.)
            CoreServices.Audio?.PlayUiClick();
        }

        private void CmdPlayMusic(string id)
        {
            // Intentionally inert: the village music track is already playing and a
            // mismatched swap would be worse than leaving it. Logged for visibility.
            Debug.Log($"[DialogueCommandBridge] play_music '{id}' — keeping current village track.");
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
            var deployer = FindObjectOfType<PetDeployer>();
            if (deployer != null) deployer.DeployStarterPets();
            else Debug.Log("[DialogueCommandBridge] spawn_starting_pet — no PetDeployer in scene.");
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
