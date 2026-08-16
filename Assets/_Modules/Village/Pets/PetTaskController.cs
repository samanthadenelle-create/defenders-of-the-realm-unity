// =============================================================================
// PetTaskController (owner felt-test 2026-07-17: "the pet should engage me and
// have some dialogue to determine what it should do (Harvest/Repair)").
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// WHY THIS LIVES IN DeNelle.Village (not DeNelle.Pets):
//   It routes the player's choice to systems that live in Village — PetHarvester
//   (DeNelle.Pets, referenceable) for the gather task, and WallRepairController
//   (DeNelle.Village) for the repair task — and it opens the code-built dialogue
//   via DeNelle.Core.Dialogue.DialogueService. DeNelle.Pets cannot reference
//   DeNelle.Village, so (exactly like PetContextualBehaviour) this engagement
//   behaviour cannot live in the Pets assembly. It is attached to each deployed
//   Pet by the self-installing PetTaskInstaller below (mirrors how
//   PetHarvestBootstrap adds Village-side pet behaviour without a scene edit).
//
// WHAT IT DOES (builds ON the existing systems, no greenfield):
//   1. ENGAGE   — when the Keeper comes near the pet (proximity, with a leave/return
//                 re-arm) OR taps on/near the pet, the pet greets them and asks what
//                 to do. Shown through the SAME code-built DialogueView (no UXML, no
//                 YarnSpinner) via DialogueService.PlayDef(code-built def).
//   2. CHOOSE   — a two-option prompt (colourblind-safe TEXT labels, never colour):
//                   * "Gather resources"  -> Harvest task
//                   * "Repair structures" -> Repair task
//                 Each option's node fires the "pet_task" verb (DialogueCommandSink),
//                 which calls back into the engaging controller's SetTask.
//   3. PERFORM  — Harvest: enables the pet's existing PetHarvester (the autonomous
//                 gather loop PetDeployer already attaches). Repair: disables the
//                 harvester and, on a scan tick, drives the EXISTING repair backend
//                 (WallRepairController.RepairAll — the same worst-first, spend-through-
//                 the-construction-economy path HubRepairAffordance uses) so damaged
//                 structures get mended while the pet is assigned to repair.
//
// FLOW NOTE (found while instrumenting, §12): Pet.Update returns at its ff.petcombat
// gate (default OFF) BEFORE its MoveToward(_homePost), so a Defend pet does not
// self-drive toward HomePost today. The Repair task therefore does NOT depend on pet
// locomotion — it mends from the assignment (a companion "tend" action), reusing the
// complete RepairAll backend. See the report's flagged item.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Dialogue;
using DeNelle.Core.Diagnostics;
using DeNelle.Pets;
using CoreCost = DeNelle.Core.Catalog.ResourceCost;

namespace DeNelle.Village
{
    /// <summary>The task the player can assign the pet through the engagement prompt.</summary>
    public enum PetTask
    {
        /// <summary>Gather resources (the pet's existing PetHarvester loop).</summary>
        Harvest = 0,
        /// <summary>Mend damaged structures (the WallRepairController RepairAll backend).</summary>
        Repair = 1,
    }

    /// <summary>
    /// Drives the pet's "engage the Keeper and ask what to do" interaction and routes
    /// the chosen task (Harvest / Repair) to the existing pet-gather / structure-repair
    /// systems. One per deployed <see cref="Pet"/> (added by <see cref="PetTaskInstaller"/>).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class PetTaskController : MonoBehaviour
    {
        // ── Engagement tuning ────────────────────────────────────────────────
        private const float EngageRadius = 5f;      // Keeper this close -> the pet greets
        private const float RearmRadius = 10f;      // must leave past this before it greets again
        private const float MinAutoInterval = 20f;  // hard floor between auto-greets (not spammy)
        private const float TapScreenRadiusPx = 110f; // tap within this many px of the pet also engages
        private const float HeroResolveInterval = 0.5f;

        // ── Repair tuning ────────────────────────────────────────────────────
        private const float RepairScanInterval = 1.5f;

        // ── Runtime ──────────────────────────────────────────────────────────
        private Pet _pet;
        private PetHarvester _harvester;
        private Transform _hero;
        private WallRepairController _repair;

        private PetTask _task = PetTask.Harvest;   // default = gather (matches the deploy-time PetHarvester)
        private bool _armed = true;                 // proximity greet re-arms after the Keeper leaves
        private float _heroResolveTimer;
        private float _lastAutoEngage = -999f;
        private float _nextRepairScan;

        // The controller that opened the CURRENT engagement prompt — the "pet_task" verb
        // (fired when the player picks an option) applies the choice back to THIS pet.
        private static PetTaskController s_engaging;

        /// <summary>The task the pet is currently assigned.</summary>
        public PetTask Task => _task;

        private void Awake()
        {
            _pet = GetComponent<Pet>();
            _harvester = GetComponent<PetHarvester>();
        }

        private void Update()
        {
            if (_pet == null || !_pet.IsAlive) return;

            ResolveHero();

            // Never greet during a battle or while a conversation already owns the screen.
            bool busy = DeNelle.Core.Dialogue.DialogueService.IsRunning || DialogueService.IsRunning || DeNelle.Core.Combat.BattleLock.IsInBattle();

            if (!busy) TickEngagement();

            if (_task == PetTask.Repair) TickRepair();
        }

        // =====================================================================
        //  Engagement — proximity (with leave/return re-arm) + tap on/near the pet
        // =====================================================================

        private void TickEngagement()
        {
            if (_hero == null) return;

            float dist = Vector3.Distance(transform.position, _hero.position);

            // Re-arm once the Keeper has walked away, so the greeting is a "come near"
            // event rather than a per-frame pop.
            if (dist > RearmRadius) _armed = true;

            // Deliberate tap on/near the pet always offers the prompt (subject to the
            // busy gate above) — the player can re-assign the pet whenever they like.
            if (TapNearPetThisFrame())
            {
                Engage("tap");
                return;
            }

            // Auto-greet on approach: armed + close + not too soon since the last auto-greet.
            if (_armed && dist <= EngageRadius && Time.time - _lastAutoEngage >= MinAutoInterval)
            {
                _armed = false;
                _lastAutoEngage = Time.time;
                Engage("proximity");
            }
        }

        private void Engage(string trigger)
        {
            if (!DeNelle.Core.FeatureFlags.CustomDialogue)
            {
                FlowTrace.Warn("Pet",
                    "engage skipped — ff.customdialogue OFF, so no DialogueView can render the prompt.");
                return;
            }

            FlowTrace.Step("Pet",
                $"engage ({trigger}) pet '{_pet.PetId}' -> opening Harvest/Repair prompt (current task {_task}).");

            s_engaging = this;
            DeNelle.Core.Dialogue.DialogueService.PlayDef(BuildEngageDef(_pet != null ? _pet.Species : null));
        }

        // =====================================================================
        //  The code-built two-choice dialogue (no catalog id, no UXML, no Yarn)
        // =====================================================================

        /// <summary>
        /// The code-built Harvest/Repair engagement prompt for an Echo of the given species.
        /// STATIC + PUBLIC (WO-1030) so the UI capture harness shoots the EXACT def the game
        /// plays -- one builder, no drifting fixture copy. Speaker resolves via SpeakerName
        /// (species -> "Frost"/"Ember"/"Aether"), which the dialogues.json speakers block now
        /// carries records for (portrait + affiliation).
        /// </summary>
        public static DialogueDef BuildEngageDef(string species)
        {
            string speaker = SpeakerName(species);
            var def = new DialogueDef { Id = "pet_engage", StartNode = "root" };

            def.Nodes.Add(new DialogueNode
            {
                Id = "root",
                Lines = new List<DialogueLine>
                {
                    new DialogueLine { Speaker = speaker, Text = "Keeper, I'm at your side. What should I tend to?" },
                },
                Options = new List<DialogueOption>
                {
                    new DialogueOption { Text = "Gather resources",  Goto = "do_harvest" },
                    new DialogueOption { Text = "Repair structures", Goto = "do_repair" },
                },
            });

            def.Nodes.Add(new DialogueNode
            {
                Id = "do_harvest",
                Lines = new List<DialogueLine>
                {
                    new DialogueLine { Speaker = speaker, Text = "On it - I'll gather what I can find." },
                },
                Commands = new List<DialogueCommand>
                {
                    new DialogueCommand { Verb = "pet_task", Args = new List<string> { "harvest" } },
                },
            });

            def.Nodes.Add(new DialogueNode
            {
                Id = "do_repair",
                Lines = new List<DialogueLine>
                {
                    new DialogueLine { Speaker = speaker, Text = "I'll mend the walls and buildings." },
                },
                Commands = new List<DialogueCommand>
                {
                    new DialogueCommand { Verb = "pet_task", Args = new List<string> { "repair" } },
                },
            });

            return def;
        }

        private static string SpeakerName(string species)
        {
            switch ((species ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "aether-sprite": return "Aether";
                case "flame-pup":     return "Ember";
                case "ice-wolf":      return "Frost";
                default:              return "Your Echo";
            }
        }

        // =====================================================================
        //  Choice routing — the "pet_task" verb (DialogueCommandSink) lands here
        // =====================================================================

        /// <summary>
        /// Applies the player's dialogue choice to the pet that opened the current
        /// engagement prompt. Called by the DialogueCommandSink "pet_task" verb.
        /// Falls back to the single deployed controller if the handoff was lost.
        /// </summary>
        public static void ApplyEngagementChoice(string mode)
        {
            var target = s_engaging;
            if (target == null) target = FindAnyObjectByType<PetTaskController>();
            s_engaging = null;

            if (target == null)
            {
                FlowTrace.Warn("Pet", $"pet_task '{mode}' — no PetTaskController to apply the choice to.");
                return;
            }
            target.SetTask(ParseTask(mode));
        }

        private static PetTask ParseTask(string mode)
        {
            return string.Equals((mode ?? string.Empty).Trim(), "repair", System.StringComparison.OrdinalIgnoreCase)
                ? PetTask.Repair : PetTask.Harvest;
        }

        /// <summary>Assigns the pet's task and switches the backing loop (Harvest vs Repair).</summary>
        public void SetTask(PetTask task)
        {
            _task = task;
            FlowTrace.Step("Pet", $"choice -> {task} for pet '{(_pet != null ? _pet.PetId : "<null>")}'.");

            if (_harvester == null) _harvester = GetComponent<PetHarvester>();

            if (task == PetTask.Harvest)
            {
                // Hand back to the existing autonomous gather loop.
                Guard.Try("Pet", "enable harvest task", () =>
                {
                    if (_harvester != null) _harvester.enabled = true;
                });
                FlowTrace.Step("Pet", $"harvest task active — pet '{PetId()}' will gather via PetHarvester.");
            }
            else
            {
                // Stop gathering so the two loops don't fight; repair runs from TickRepair.
                Guard.Try("Pet", "disable harvest for repair task", () =>
                {
                    if (_harvester != null) _harvester.enabled = false;
                });
                _nextRepairScan = 0f;   // let the first repair pass run immediately
                FlowTrace.Step("Pet", $"repair task active — pet '{PetId()}' will mend structures via WallRepairController.");
            }
        }

        private string PetId() => _pet != null ? _pet.PetId : "<null>";

        // =====================================================================
        //  Repair task — drive the EXISTING RepairAll backend (no new repair system)
        // =====================================================================

        private void TickRepair()
        {
            if (Time.time < _nextRepairScan) return;
            _nextRepairScan = Time.time + RepairScanInterval;

            // Don't mend mid-assault (RepairAll's own callers gate on wave phase too).
            if (DeNelle.Core.Combat.BattleLock.IsInBattle()) return;

            var repair = EnsureRepair();
            if (repair == null) return;

            CoreCost cost = repair.RepairAllCost();
            if (WallRepairController.MaterialsZero(cost))
            {
                FlowTrace.Throttle("Pet", "repair-clean-" + PetId(), 5f,
                    $"repair task: nothing damaged — pet '{PetId()}' idle.");
                return;
            }

            if (!repair.CanAffordMaterials(cost))
            {
                FlowTrace.Throttle("Pet", "repair-short-" + PetId(), 5f,
                    $"repair task: cannot afford {WallRepairController.DescribeMaterials(cost)} — " +
                    "waiting for materials (go farm).");
                return;
            }

            Guard.Try("Pet", "pet repair pass (RepairAll)", () =>
            {
                var r = repair.RepairAll();
                FlowTrace.Step("Pet",
                    $"repair task pass by '{PetId()}': repaired={r.repairedCount} " +
                    $"spent={WallRepairController.DescribeMaterials(r.spent)} remaining={r.remainingDamaged}.");
            });
        }

        /// <summary>
        /// Resolves the shared repair backend: reuses an existing WallRepairController
        /// (a wave scene / HubRepairAffordance installs one) or creates a LOGIC-ONLY,
        /// disabled controller purely to price + apply RepairAll — never a second
        /// repair system (mirrors HubRepairAffordance.EnsureRepair).
        /// </summary>
        private WallRepairController EnsureRepair()
        {
            if (_repair != null) return _repair;
            _repair = FindAnyObjectByType<WallRepairController>();
            if (_repair == null)
            {
                var go = new GameObject("WallRepair_PetTaskEngine");
                _repair = go.AddComponent<WallRepairController>();
                _repair.enabled = false;   // logic-only: we call RepairAllCost / RepairAll directly
                FlowTrace.Step("Pet", "pet repair task self-installed a logic-only WallRepairController.");
            }
            return _repair;
        }

        // =====================================================================
        //  Input / hero resolution (legacy Input Manager — project constraint)
        // =====================================================================

        private void ResolveHero()
        {
            if (_hero != null) return;
            _heroResolveTimer -= Time.deltaTime;
            if (_heroResolveTimer > 0f) return;
            _heroResolveTimer = HeroResolveInterval;

            var loco = FindAnyObjectByType<HeroLocomotion>();
            if (loco != null) { _hero = loco.transform; return; }
            var tagged = GameObject.FindWithTag("Player");
            if (tagged != null) _hero = tagged.transform;
        }

        // True when the player tapped ON or NEAR the pet this frame (screen-space distance,
        // so no collider is needed on the collider-less pet billboard/mesh).
        private bool TapNearPetThisFrame()
        {
            Vector2 screen;
            if (Input.touchCount > 0)
            {
                Touch t = Input.GetTouch(0);
                if (t.phase != TouchPhase.Began) return false;
                screen = t.position;
            }
            else if (Input.GetMouseButtonDown(0))
            {
                screen = Input.mousePosition;
            }
            else
            {
                return false;
            }

            var cam = Camera.main;
            if (cam == null) return false;

            Vector3 petScreen = cam.WorldToScreenPoint(transform.position + Vector3.up);
            if (petScreen.z <= 0f) return false;   // pet is behind the camera

            float px = Vector2.Distance(new Vector2(petScreen.x, petScreen.y), screen);
            return px <= TapScreenRadiusPx;
        }
    }

    /// <summary>
    /// Self-installing host that attaches a <see cref="PetTaskController"/> to every
    /// deployed <see cref="Pet"/> (pets can spawn after scene load via the Echo Hollow /
    /// tutorial, so it polls on a light interval). Mirrors PetHarvestBootstrap: code-built,
    /// runtime, DDOL — no scene edit, no Pets-asmdef change.
    /// </summary>
    public sealed class PetTaskInstaller : MonoBehaviour
    {
        private static PetTaskInstaller _instance;
        private float _timer;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (_instance != null) return;
            var go = new GameObject("PetTaskInstaller");
            _instance = go.AddComponent<PetTaskInstaller>();
            Object.DontDestroyOnLoad(go);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Update()
        {
            _timer -= Time.deltaTime;
            if (_timer > 0f) return;
            _timer = 1f;

            var pets = FindObjectsByType<Pet>(FindObjectsSortMode.None);
            if (pets == null) return;
            foreach (var pet in pets)
            {
                if (pet == null) continue;
                if (pet.GetComponent<PetTaskController>() == null)
                {
                    pet.gameObject.AddComponent<PetTaskController>();
                    FlowTrace.Step("Pet", $"attached PetTaskController to pet '{pet.PetId}' (engagement enabled).");
                }
            }
        }
    }
}
