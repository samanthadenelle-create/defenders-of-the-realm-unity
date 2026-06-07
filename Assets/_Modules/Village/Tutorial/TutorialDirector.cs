// =============================================================================
// TutorialDirector — the FTUE master sequencer (WO-277 / DEF-222).
// -----------------------------------------------------------------------------
// Drives the seven-scene first-time onboarding as one UniTask coroutine:
//   1. Arrival + Meeting     — companion (a DIFFERENT class) runs up + introduces.
//   2. Village Tour + Tower   — auto-walk tour; build the FIRST tower FREE at a gate.
//   3. Second Gate + Combat   — "not enough resources"; horn; small wave at THIS gate.
//   4. Post-Battle Supplies   — grant exactly 3× a Level-1 tower cost.
//   5. Daily Quests Callout    — dialogue-only narration (no quest panel exists yet).
//   6. Pet Introduction        — name the pet; choose Defend or Gather.
//   7. Freedom                — full control; companion follows; objective set.
//
// ── RECONCILIATION with the existing onboarding scaffold ─────────────────────
// The project already had OnboardingFlow (DeNelle.Onboarding) — a passive, 6-beat
// coach-mark overlay gated on GameState.Onboarded and wired into gameplay by
// OnboardingIntegrator via reflection. WO-277 is a far richer, NARRATIVE first-run
// experience and the WO names DeNelle.Village as its home (where it can drive the
// hero / waves / pets / towers DIRECTLY — no reflection). So TutorialDirector is
// the new first-run surface and it RECONCILES, not duplicates:
//   • It is gated on the SAME flag (GameState.Onboarded) so it only runs first-time
//     and is skippable on every subsequent playthrough — exactly like OnboardingFlow.
//   • While it runs it SUPPRESSES the legacy OnboardingFlow coach-marks (calls its
//     Skip) so the two never overlap.
//   • On completion it does the SAME handoff OnboardingIntegrator does:
//     GameStateService.FinishOnboarding() (sets Onboarded=true + Save) then kicks
//     the wave loop via WaveManager.BeginLoop() — so the cold-open replay bug stays
//     fixed and Wave 1 starts the moment the tutorial ends.
//
// ── Self-bootstrap (no scene edit) ────────────────────────────────────────────
// Self-injects via [RuntimeInitializeOnLoadMethod] like StoryCompanionInjector —
// it does NOT require baking into Village.unity (which is off-limits). It only runs
// in the Village scene, only on a first run (or when ForceRun dev-flag is set).
//
// async UniTask throughout — never async void (port-spec Part 3 mandate).
// Every cross-module/singleton call is null-guarded; off the village scene (no
// hero / no wave manager) the director simply no-ops.
// =============================================================================

using System;
using Cysharp.Threading.Tasks;
using DeNelle.Core.Data;
using DeNelle.Core.State;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>
    /// Orchestrates the seven-scene first-time tutorial. Self-bootstrapping +
    /// gated on <see cref="GameState.Onboarded"/>; on completion marks onboarding
    /// done and hands off to the wave loop the same way the existing
    /// OnboardingIntegrator does.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class TutorialDirector : MonoBehaviour
    {
        // ── Dev / test switches ──────────────────────────────────────────────
        /// <summary>
        /// Dev override — force the tutorial to run even when the save says
        /// onboarded (so it's testable without wiping the save). Set true from a
        /// dev menu / static initializer before the Village scene loads.
        /// </summary>
        public static bool ForceRun;

        /// <summary>Dev override — skip the tutorial entirely (still marks onboarded + kicks waves).</summary>
        public static bool ForceSkip;

        private const string TargetScene = "Village2";
        private static bool s_ranThisSession;   // belt-and-braces: one run per session

        // 3× a Level-1 tower cost — the post-battle supply grant (WO-277 economy).
        // Read from the tutorial tower's own cost so "free 1st + grant-for-3-more"
        // stays internally consistent (the tutorial tower carries TutorialTowerCost).
        private const int TutorialTowerCost = 50;   // a representative Level-1 cost
        private const int PostBattleGrantTowers = 3;

        // Small first wave (WO: "2-3 enemies").
        private const int TutorialWaveCount = 3;

        // ── Sub-components (added at runtime) ────────────────────────────────
        private CompanionSpawner _companionSpawner;
        private TutorialDialogue _dialogue;
        private TutorialAutoWalk _autoWalk;
        private TutorialWaveSpawner _waveSpawner;
        private PetIntroduction _petIntro;

        // ── Scene refs ───────────────────────────────────────────────────────
        private HeroLocomotion _hero;
        private WaveManager _wave;
        private bool _running;
        private bool _towerPlaced;

        // =====================================================================
        //  Bootstrap
        // =====================================================================

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (FindObjectOfType<TutorialDirector>() != null) return;
            var go = new GameObject("TutorialDirector");
            go.AddComponent<TutorialDirector>();
        }

        private void Awake()
        {
            // Only ever needed in the Village scene; clean up elsewhere.
            if (SceneManager.GetActiveScene().name != TargetScene)
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            if (!ShouldRun)
            {
                // Returning player (or dev-skip) — nothing to teach. If a first-run
                // skip was forced, still mark onboarded + kick the loop so the game
                // proceeds; otherwise leave the (already-running) loop alone.
                if (ForceSkip && IsFirstRun())
                    SkipToGameplay();
                Destroy(gameObject);
                return;
            }

            Run().Forget();
        }

        /// <summary>True when the FTUE should play this session.</summary>
        private bool ShouldRun
        {
            get
            {
                if (s_ranThisSession) return false;
                if (ForceSkip) return false;
                if (ForceRun) return true;
                return IsFirstRun();
            }
        }

        /// <summary>First run = a brand-new save (Onboarded == false). No Core ⇒ not first-run.</summary>
        private static bool IsFirstRun()
        {
            var svc = GameStateService.Instance;
            if (svc == null || svc.State == null) return false;
            return !svc.State.Onboarded;
        }

        // =====================================================================
        //  The seven-scene sequence
        // =====================================================================

        private async UniTask Run()
        {
            s_ranThisSession = true;
            _running = true;

            // Suppress the legacy OnboardingFlow coach-marks FIRST (before they can
            // show), so the two first-run surfaces never overlap. The wave loop is
            // already held closed by WaveManager's own first-run gate (Onboarded
            // == false), so Wave 1 won't start until FinishTutorial kicks it.
            SuppressLegacyCoachMarks();

            // Let the village settle (hero rig, NavMesh, HUD, companion bootstrap).
            await UniTask.Delay(TimeSpan.FromSeconds(1.25f));

            ResolveSceneRefs();

            // ─────────────────────────────────────────────────────────────────
            // DEF-263 — NPC INTRO DIALOG REMOVED.
            // The hardcoded seven-scene companion-meeting / tour / "they're here!"
            // dialogue no longer fires on village entry. The owner directive is a
            // streamlined hero→pet→village front-of-game with NO scripted NPC talk
            // on arrival. The scene methods (SceneArrivalAndMeeting … SceneFreedom)
            // and their sub-components are kept below for reference but are NO
            // LONGER CALLED — the lead's Yarn narrative (DEF-251 / CompanionMeeting.
            // yarn) replaces them via the progression-start hook.
            //
            // ↓↓↓ PROGRESSION-START HOOK ↓↓↓
            // This is the SINGLE first-village-entry call site where progression /
            // narrative kicks off. It does nothing yet — wire the Yarn narrative
            // (CompanionMeeting.yarn) HERE. Removing the NPC dialog above does NOT
            // orphan progression start: this hook is exactly where it now begins.
            OnVillageProgressionStart();
            // ↑↑↑ PROGRESSION-START HOOK ↑↑↑

            // Yarn owns the FTUE→gameplay handoff now: CompanionMeetingTrigger hosts the
            // CompanionMeeting narrative and its <<enable_full_controls>> calls
            // FinishOnboarding (opening the wave loop) at the END of the dialogue. We must
            // NOT FinishTutorial inline here — that would start the REAL wave loop on top
            // of the dialogue's own scripted tutorial wave. FALLBACK ONLY: if no narrative
            // hosted (prefab/pack absent), finish so a dialogue-less build still proceeds.
            await UniTask.Delay(TimeSpan.FromSeconds(0.75f));
            if (!CompanionMeetingTrigger.Hosted)
            {
                Debug.Log("[TutorialDirector] No FTUE narrative hosted — finishing tutorial inline (fallback).");
                FinishTutorial();
            }
        }

        /// <summary>
        /// DEF-263 PROGRESSION-START HOOK — the single, canonical first-village-entry
        /// call site for kicking off the opening narrative + first objective.
        ///
        /// It is intentionally EMPTY today: the streamlined onboarding (DEF-263) drops
        /// the old hardcoded NPC intro dialogue, and the replacement is a Yarn-driven
        /// narrative (DEF-251, CompanionMeeting.yarn) that the lead wires in next. Put
        /// that kick-off RIGHT HERE — e.g. start the Yarn dialogue runner for the
        /// companion-meeting node and/or set the first HUD objective — so progression
        /// start is never orphaned by removing the old dialog.
        ///
        /// Fires exactly once per first run (ShouldRun gates Run()), on the village
        /// scene only, after the scene has settled. Do NOT block the FinishTutorial
        /// handoff below from here unless the narrative explicitly needs to hold the
        /// wave loop — if it does, move the FinishTutorial() call into the narrative's
        /// completion callback instead of leaving it inline in Run().
        /// </summary>
        private void OnVillageProgressionStart()
        {
            // DEF-251: kick off the Yarn companion-meeting narrative. The
            // Resources/Dialogue/DialogueSystem prefab (paid ClassicRPG add-on, wired by
            // 'Defenders > Yarn > Setup Dialogue System') carries a DialogueRunner set to
            // autoStart the "CompanionMeeting" node from our compiled YarnProject — so
            // simply instantiating it plays the intro dialogue + choices. No Yarn types
            // referenced here (keeps DeNelle.Village free of a Yarn asmdef dependency).
            // DEF-265: CompanionMeetingTrigger is now the SOLE host of the Yarn
            // CompanionMeeting narrative — it self-bootstraps on every village load AND
            // installs the FTUE command bridge (DialogueCommandBridge) onto the runner
            // before it starts. Instantiating Dialogue/DialogueSystem HERE too would
            // double-host the dialogue, so this hook no longer loads the prefab. It
            // remains the canonical progression-start beat.
            Debug.Log("[TutorialDirector] Progression start — Yarn narrative hosted by CompanionMeetingTrigger.");
        }

        // ── Scene 1: Arrival + Meeting ───────────────────────────────────────
        private async UniTask SceneArrivalAndMeeting()
        {
            // Spawn the companion (a DIFFERENT class) and quiet its ambient chatter.
            _companionSpawner?.Spawn();
            BindCompanionBubble();

            string name = _companionSpawner != null ? _companionSpawner.CompanionShortName : "your guide";
            string speaker = _companionSpawner != null ? _companionSpawner.CompanionName : "Companion";

            _dialogue?.Say(speaker,
                "You made it. I wasn't sure anyone else would come.",
                "The enemy's been pushing closer every night. They've breached the outer walls twice this week.",
                $"Elarion needs defenders. I'm {name} — I'll show you what we're working with.");
            await WaitForDialogue();
        }

        // ── Scene 2: Village Tour + First Tower ──────────────────────────────
        private async UniTask SceneTourAndFirstTower()
        {
            string speaker = SpeakerName();

            // Auto-walk tour: lead the hero toward the nearest gate, narrating.
            WaveSpawnPoint gate = NearestSpawnPoint();
            if (gate != null && _autoWalk != null)
            {
                _dialogue?.Say(speaker,
                    "Walk with me. That's the Forge — where steel is mended.",
                    "The Arcane Tower, and the Echo Hollow beyond it. Your Echoes rest there.");
                await WaitForDialogue();

                _autoWalk.WalkTo(gate.transform.position);
                await UniTask.WaitUntil(() => _autoWalk.HasArrived);
            }

            _dialogue?.Say(speaker,
                "See that gate? Last attack came through there. We need a watchtower.",
                "Tap BUILD to place your first tower — this one's on me.");
            await WaitForDialogue();

            // FREE first tower (prepaid = true → no cost). Advance when it's placed.
            _towerPlaced = false;
            StartFreeTowerPlacement();
            await UniTask.WaitUntil(() => _towerPlaced);

            _dialogue?.Say(speaker,
                "Good. That'll slow them down. Let's check the other entrances.");
            await WaitForDialogue();
        }

        // ── Scene 3: Second Gate — Resource Wall + First Combat ──────────────
        private async UniTask SceneSecondGateAndCombat()
        {
            string speaker = SpeakerName();

            // Auto-walk to a second gate (different from the first if possible).
            WaveSpawnPoint second = SecondSpawnPoint();
            if (second != null && _autoWalk != null)
            {
                _autoWalk.WalkTo(second.transform.position);
                await UniTask.WaitUntil(() => _autoWalk.HasArrived);
            }

            _dialogue?.Say(speaker,
                "We should fortify this one too... Not enough resources.");
            await WaitForDialogue();

            // HORN — enemies spawn AT THIS GATE (the one we're standing at).
            _dialogue?.Say(speaker, "They're here! Defend this gate — NOW!");
            await WaitForDialogue();

            WaveSpawnPoint combatGate = second != null ? second : NearestSpawnPoint();
            if (_waveSpawner != null && combatGate != null)
            {
                await _waveSpawner.SpawnAt(combatGate, TutorialWaveCount);
                // Companion fights alongside (StoryCompanion's combat loop is live).
                await UniTask.WaitUntil(() => _waveSpawner.IsCleared);
            }
        }

        // ── Scene 4: Post-Battle — Supplies ──────────────────────────────────
        private async UniTask ScenePostBattleSupplies()
        {
            string speaker = SpeakerName();

            _dialogue?.Say(speaker,
                "Thanks for the help pushing them back.",
                "We better hurry to get some supplies. Here — take these to get started.");
            await WaitForDialogue();

            // Grant EXACTLY 3× a Level-1 tower cost (WO-277 economy table).
            GrantPostBattleSupplies();

            _dialogue?.Say(speaker,
                "That should be enough to put up a watchtower at each entrance. Get them built before the next attack.");
            await WaitForDialogue();
        }

        // ── Scene 5: Daily Quests Callout (dialogue-only) ────────────────────
        private async UniTask SceneQuestsCallout()
        {
            string speaker = SpeakerName();

            // NOTE (WO-277): no DailyQuestPanel exists yet — Scene 5 is narration
            // only. When a quest panel lands, pulse/glow it here.
            // TODO(WO-277 follow-up): DailyQuestPanel.Instance?.Pulse();
            _dialogue?.Say(speaker,
                "See those tasks on the side? Complete them for extra rewards. Resources, experience, sometimes rarer things.",
                "Keep an eye on them — they change, and they're worth doing.");
            await WaitForDialogue();
        }

        // ── Scene 6: Pet Introduction ────────────────────────────────────────
        private async UniTask ScenePetIntroduction()
        {
            string speaker = SpeakerName();

            _dialogue?.Say(speaker,
                "Well, looks like you've got a friend already. One of the Echoes — it's chosen you.");
            await WaitForDialogue();

            // Name prompt + Defend/Gather choice (code-built UI).
            if (_petIntro != null)
            {
                _petIntro.Begin();
                await UniTask.WaitUntil(() => _petIntro.IsComplete);

                string petName = _petIntro.PetName;
                if (_petIntro.ChoseGather)
                    _dialogue?.Say(speaker,
                        $"Smart. {petName} will bring back what they find. Keep your eyes open out there.");
                else
                    _dialogue?.Say(speaker,
                        $"{petName} will hold the line. But we need more resources — you should explore beyond the gates.");
                await WaitForDialogue();
            }
        }

        // ── Scene 7: Freedom ─────────────────────────────────────────────────
        private async UniTask SceneFreedom()
        {
            string speaker = SpeakerName();

            _dialogue?.Say(speaker,
                "North, South, East, West — they can come from any direction. Fortify all four.");
            await WaitForDialogue();

            // HUD objective: "Build towers at the remaining 3 gates (0/3)".
            // NOTE (WO-277): no dedicated objective HUD surface exists yet; emit the
            // objective so a HUD listener can show it. Logged for now; wire a real
            // objective ticker when one lands.
            // TODO(WO-277 follow-up): VillageHud.SetObjective("Build towers at the remaining 3 gates (0/3)");
            Debug.Log("[TutorialDirector] Objective set: Build towers at the remaining 3 gates (0/3).");
        }

        // =====================================================================
        //  Finish — mark onboarded, restore control, kick the wave loop
        // =====================================================================

        private void FinishTutorial()
        {
            _running = false;

            // Hand control back to the player + restore the companion's ambient voice.
            _autoWalk?.Stop();
            _hero?.ClearAutoWalk();
            RestoreCompanionSpeech();
            _companionSpawner?.ClearOverride();   // companion stays the mapped (different) class — WO-280, never a hero clone

            SkipToGameplay();
        }

        /// <summary>
        /// Marks onboarding complete (sets Onboarded + Save — the SAME persistence
        /// OnboardingIntegrator relies on, which also stops the cold-open replay) and
        /// kicks the wave loop, exactly the way the existing integrator hands off.
        /// </summary>
        private void SkipToGameplay()
        {
            GameStateService.Instance?.FinishOnboarding();

            // Kick Wave 1 — BeginLoop is the loop's entry the integrator uses.
            if (_wave == null) _wave = FindObjectOfType<WaveManager>();
            _wave?.BeginLoop().Forget();

            Debug.Log("[TutorialDirector] Tutorial complete — Onboarded persisted, wave loop kicked.");
            Destroy(gameObject, 0.5f);
        }

        // =====================================================================
        //  Setup helpers
        // =====================================================================

        private void ResolveSceneRefs()
        {
            _hero = FindObjectOfType<HeroLocomotion>();
            _wave = FindObjectOfType<WaveManager>();
        }

        private void BuildSubComponents()
        {
            _companionSpawner = gameObject.AddComponent<CompanionSpawner>();
            _dialogue         = gameObject.AddComponent<TutorialDialogue>();
            _autoWalk         = gameObject.AddComponent<TutorialAutoWalk>();
            _waveSpawner      = gameObject.AddComponent<TutorialWaveSpawner>();
            _petIntro         = gameObject.AddComponent<PetIntroduction>();

            _autoWalk.SetHero(_hero);
            _waveSpawner.SetWaveManager(_wave);
        }

        // The FTUE dialogue is shown on the companion's own world-space bubble so it
        // reads as the companion speaking. Falls back to no bubble (timed advance).
        private void BindCompanionBubble()
        {
            var injector = StoryCompanionInjector.Instance;
            var companion = injector != null ? injector.Companion : null;
            if (companion != null)
            {
                companion.SetSpeechSuppressed(true);   // we own the dialogue now
                _dialogue?.SetBubble(companion.Bubble);
            }
        }

        private void SuppressLegacyCoachMarks()
        {
            // Reconcile: skip the legacy OnboardingFlow coach-mark overlay (a
            // DeNelle.Onboarding type the Village asmdef can't reference) so the two
            // first-run surfaces never overlap. Resolved + skipped by reflection —
            // a no-op when the Onboarding module isn't present. (Pre-existing
            // reflection pattern; no NEW reflection into a bridge script.)
            try
            {
                Type flowType = ResolveType("DeNelle.Onboarding.OnboardingFlow");
                if (flowType == null) return;
                var flow = FindObjectOfType(flowType);
                if (flow == null) return;
                // Disable the legacy coach-mark overlay so it never shows alongside
                // the FTUE — the SAME Onboarded flag still gets set by FinishTutorial,
                // so OnboardingFlow's persistence responsibility is preserved.
                if (flow is Behaviour b) b.enabled = false;
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[TutorialDirector] Could not suppress legacy OnboardingFlow ({ex.Message}) — continuing.");
            }
        }

        private void RestoreCompanionSpeech()
        {
            var injector = StoryCompanionInjector.Instance;
            injector?.Companion?.SetSpeechSuppressed(false);
        }

        // ── Tower placement (free first tower) ───────────────────────────────

        private void StartFreeTowerPlacement()
        {
            // Ensure the placement system exists (it self-bootstraps in some scenes,
            // but the FTUE shouldn't depend on a dev harness).
            if (TowerPlacementSystem.Instance == null)
                new GameObject("TowerPlacementSystem").AddComponent<TowerPlacementSystem>();

            var tps = TowerPlacementSystem.Instance;
            if (tps == null) { _towerPlaced = true; return; }   // can't place — don't wedge

            tps.OnTowerPlaced -= OnTutorialTowerPlaced;
            tps.OnTowerPlaced += OnTutorialTowerPlaced;

            TowerData tower = ResolveTutorialTower();
            tps.StartPlacing(tower, prepaid: true);   // FREE — no cost charged
        }

        private void OnTutorialTowerPlaced(TowerData data)
        {
            if (TowerPlacementSystem.Instance != null)
                TowerPlacementSystem.Instance.OnTowerPlaced -= OnTutorialTowerPlaced;
            _towerPlaced = true;
        }

        // Prefer the seeded DevTower (real model); fall back to a code-built free,
        // skill-gate-free tower so the FTUE never depends on editor seeding.
        private static TowerData ResolveTutorialTower()
        {
            var seeded = Resources.Load<TowerData>("Towers/DevTower");
            if (seeded != null) return seeded;

            var d = ScriptableObject.CreateInstance<TowerData>();
            d.towerName = "Watchtower";
            d.cost = TutorialTowerCost;     // representative Level-1 cost
            d.buildTime = 3f;
            d.requiredSkill = new SkillRequirement { type = SkillType.None, minLevel = 0 };
            d.upgrades = new TowerUpgrade[3];
            for (int i = 0; i < 3; i++)
                d.upgrades[i] = new TowerUpgrade
                {
                    visualPrefab = null,
                    ability      = SpecialAbility.None,
                    range        = 9f + i * 2f,
                    damage       = 7f + i * 3f,
                    upgradeCost  = 0,
                };
            return d;
        }

        // ── Economy grant ─────────────────────────────────────────────────────

        private void GrantPostBattleSupplies()
        {
            int amount = TutorialTowerCost * PostBattleGrantTowers;   // 3× Level-1 cost

            // EconomyService.Grant routes crystals through GameState.Resources.Crystals —
            // the SINGLE build-spend pool the BuildMenu/HUD show and TowerPlacementSystem
            // charges from. So granting crystals here = "enough for 3 more towers".
            var econ = EconomyService.Instance;
            if (econ != null)
            {
                econ.Grant(crystals: amount);
            }
            else
            {
                // No EconomyService in this scene — grant straight to the store.
                GameStateService.Instance?.AddCrystals(amount);
            }

            Debug.Log($"[TutorialDirector] Granted {amount} crystals " +
                      $"(= {PostBattleGrantTowers}× the {TutorialTowerCost}-crystal Level-1 tower cost).");
        }

        // ── Spawn-point selection (gate = the WaveSpawnPoint nearest the hero) ──

        private WaveSpawnPoint NearestSpawnPoint()
        {
            Vector3 from = _hero != null ? _hero.transform.position : transform.position;
            var points = FindObjectsByType<WaveSpawnPoint>(FindObjectsSortMode.None);
            WaveSpawnPoint best = null;
            float bestSqr = float.MaxValue;
            foreach (var p in points)
            {
                if (p == null) continue;
                float sqr = (p.transform.position - from).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; best = p; }
            }
            return best;
        }

        // A different gate from the nearest one (the next-nearest), so Scene 3 walks
        // the hero to a SECOND entrance. Falls back to the nearest if only one exists.
        private WaveSpawnPoint SecondSpawnPoint()
        {
            Vector3 from = _hero != null ? _hero.transform.position : transform.position;
            var points = FindObjectsByType<WaveSpawnPoint>(FindObjectsSortMode.None);
            WaveSpawnPoint nearest = NearestSpawnPoint();
            WaveSpawnPoint second = null;
            float bestSqr = float.MaxValue;
            foreach (var p in points)
            {
                if (p == null || p == nearest) continue;
                float sqr = (p.transform.position - from).sqrMagnitude;
                if (sqr < bestSqr) { bestSqr = sqr; second = p; }
            }
            return second != null ? second : nearest;
        }

        // ── Dialogue await ───────────────────────────────────────────────────

        private async UniTask WaitForDialogue()
        {
            if (_dialogue == null) return;
            // A frame so the just-enqueued lines register, then wait for the queue
            // to drain (tap-to-advance / auto-failsafe).
            await UniTask.Yield();
            await UniTask.WaitUntil(() => _dialogue.IsIdle);
        }

        private string SpeakerName() =>
            _companionSpawner != null ? _companionSpawner.CompanionName : "Companion";

        private static Type ResolveType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                var t = asm.GetType(fullName, false);
                if (t != null) return t;
            }
            return null;
        }

        private void OnDestroy()
        {
            if (TowerPlacementSystem.Instance != null)
                TowerPlacementSystem.Instance.OnTowerPlaced -= OnTutorialTowerPlaced;
        }
    }
}
