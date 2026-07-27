// =============================================================================
// Checkpoint — a heal-and-save checkpoint shrine (Week 6).
// -----------------------------------------------------------------------------
// Port spec Part 3 row (encounters group):
//   src/modules/dungeons/encounters/ -> EncounterTrigger.cs + ... ; checkpoints
//   heal + save.
// Port spec Part 5 Week 6: "Checkpoints: certain rooms heal hero + save
// progress. Implemented as Checkpoint.cs MonoBehaviour at fixed world positions."
//
// C# port of src/modules/dungeons/3d/interactables/CheckpointShrine3D.tsx. A
// stone pedestal topped with a pulsing violet crystal — the checkpoint shrine.
// When the Keeper walks within its activation radius for the FIRST time in a
// run, the shrine:
//   1. Heals the Keeper + active pets to full.
//   2. Saves a checkpoint — the run's respawn point on death.
//   3. Settles the crystal's pulse from violet to steady gold.
//   4. Raises a one-shot bible-voice toast event.
//
// ── Post-fight reset ──
// v1 fully restores HP/MP after EVERY ATB fight, so the shrine's heal is
// wired-but-redundant in v1 — it still FUNCTIONS as a save / respawn point and
// a questline marker. When the checkpoint system fully lands (v1.1+) the heal
// becomes load-bearing. The shrine raises Activated either way; the heal/save
// wiring listens off that signal.
//
// The toast copy is CANON bible-voice — verbatim from the layout JSON (which
// mirrors docs/dungeon-3d-healers-cottage-design.md). This MonoBehaviour never
// authors or paraphrases it.
// =============================================================================

using DeNelle.Core.Diagnostics;
using UnityEngine;
using UnityEngine.Events;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// A checkpoint shrine — heals + saves the first time the Keeper reaches it
    /// in a run. Port of the React <c>CheckpointShrine3D</c>. The Healer's
    /// Cottage has three: the Entrance Room, the hearth, the Apothecary door.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class Checkpoint : MonoBehaviour
    {
        // ── Crystal tones (CheckpointShrine3D — verbatim) ────────────────────

        /// <summary>Violet crystal tone while the checkpoint is unvisited.</summary>
        private static readonly Color Violet = new Color(0.608f, 0.424f, 1f);    // #9b6cff

        /// <summary>Gold tone the crystal settles to once the checkpoint is saved.</summary>
        private static readonly Color Gold = new Color(1f, 0.812f, 0.420f);      // #ffcf6b

        [Header("State")]
        [Tooltip("The runtime run state — records the checkpoint as reached.")]
        [SerializeField] private DungeonRuntimeState _runtimeState;

        [Header("Scene refs")]
        [Tooltip("The floating crystal mesh renderer — recolours violet -> gold on save.")]
        [SerializeField] private Renderer _crystalRenderer;

        [Tooltip("The crystal's point light — a 'follow the light' cue from afar.")]
        [SerializeField] private Light _crystalLight;

        [Tooltip("The crystal transform — pulse-scaled while unvisited.")]
        [SerializeField] private Transform _crystal;

        [Header("Crystal animation")]
        [Tooltip("Pulse rate of the unvisited crystal (radians/sec sine input).")]
        [SerializeField] private float _pulseSpeed = 2f;

        [Tooltip("Pulse depth — the unvisited crystal scales +/- this fraction.")]
        [SerializeField] private float _pulseDepth = 0.08f;

        [Tooltip("When true, the crystal pulse is pinned flat (reduced motion).")]
        [SerializeField] private bool _reducedMotion;

        [Header("Events")]
        [Tooltip("Raised ONCE, the first time this shrine activates in a run — " +
                 "the heal/save layer wires the actual heal + checkpoint save off this.")]
        public UnityEvent<string> Activated = new UnityEvent<string>();

        [Tooltip("Raised with the canon bible-voice toast string on first activation.")]
        public UnityEvent<string> ToastRequested = new UnityEvent<string>();

        // ── Runtime data (handed in via Configure) ───────────────────────────

        private DungeonCheckpoint _def;
        private Transform _hero;

        /// <summary>True once this shrine has been activated this run.</summary>
        public bool Visited { get; private set; }

        /// <summary>The checkpoint id — e.g. <c>checkpoint-hearth</c>.</summary>
        public string CheckpointId => _def?.id ?? string.Empty;

        // ── Configuration ────────────────────────────────────────────────────

        /// <summary>
        /// Wires the checkpoint to its authored definition + the run state and
        /// positions the shrine at the layout's coordinates. Called by the
        /// dungeon scene builder against the layout JSON.
        /// </summary>
        public void Configure(
            DungeonCheckpoint def,
            DungeonRuntimeState runtimeState,
            Transform hero)
        {
            _def = def;
            _runtimeState = runtimeState;
            _hero = hero;

            // VERIFY: a null def means this shrine has no id / position — it can never
            // activate and would sit inert at the origin. Self-report instead of silently
            // shipping a dead checkpoint.
            if (_def == null)
            {
                FlowTrace.Warn("Dungeon",
                    $"Checkpoint.Configure on '{name}': null definition — the shrine has no id/position " +
                    "and will never activate.");
                return;
            }

            transform.position = _def.position.ToWorld();

            // VERIFY the render actor is wired — a checkpoint with no crystal renderer + no
            // light is the "follow the light" cue rendered invisible. Warn so a bare shrine
            // self-reports rather than reading blank to the player.
            if (_crystalRenderer == null && _crystalLight == null)
                FlowTrace.Warn("Dungeon",
                    $"Checkpoint.Configure '{_def.id}' on '{name}': no crystal renderer AND no light — " +
                    "the shrine will be invisible (no violet/gold cue). Check the scene wiring.");

            // A checkpoint already reached on a resumed run shows as saved.
            Visited = runtimeState != null && runtimeState.HasReachedCheckpoint(_def.id);
            ApplyCrystalTone();
            FlowTrace.Step("Dungeon",
                $"Checkpoint.Configure '{_def.id}' at {transform.position} (visited={Visited}).");
        }

        // ── Per-frame ────────────────────────────────────────────────────────

        private void Update()
        {
            if (_def == null || _hero == null) return;

            if (!Visited)
            {
                CheckProximity();
                PulseCrystal();
            }
            else
            {
                // A saved shrine holds its scale and rotates gently.
                if (_crystal != null && !_reducedMotion)
                    _crystal.Rotate(0f, 0.24f, 0f);
            }
        }

        /// <summary>
        /// Activates the shrine the first time the Keeper enters its radius —
        /// records the reach on the run state, raises <see cref="Activated"/> +
        /// <see cref="ToastRequested"/>, and settles the crystal to gold.
        /// </summary>
        private void CheckProximity()
        {
            Vector3 a = transform.position; a.y = 0f;
            Vector3 b = _hero.position; b.y = 0f;
            float r = _def.radius;
            if ((a - b).sqrMagnitude > r * r) return;

            // First reach this run — ReachCheckpoint returns false on a repeat.
            bool firstReach = _runtimeState != null && _runtimeState.ReachCheckpoint(_def.id);
            if (!firstReach && _runtimeState != null) return;

            Visited = true;

            // The shrine's two jobs (port spec Part 5 Week 6: "certain rooms heal
            // hero + save progress"):
            //   1. HEAL — restore the Keeper to full HP/MP. HealHeroToFull works
            //      off the vitals snapshot the scene loop keeps on the run state.
            //      v1's ATB engine also fully resets party HP/MP after every
            //      fight, so the heal is belt-and-braces in v1 — it becomes
            //      load-bearing the moment a non-reset combat build ships.
            //   2. SAVE — ReachCheckpoint above already recorded this shrine on
            //      the run state as the run's respawn point.
            // WO-775 heal-loop close: HealHeroToFull only mutates the run-state SO
            // snapshot — nothing re-read it, so checkpoint heals were COSMETIC. Push
            // the restored full HP/mana back onto the LIVE hero rig so the shrine's
            // heal is real. Resolve the live components off the hero rig with
            // TryGetComponent (NOT `?? ` — the NoNullCoalesceOnGetComponent lint fails
            // the gate) and restore them to full; null-guarded (mid body-swap the
            // component may not be on the rig, so HeroHealth.Instance is the HP fallback).
            bool healed = _runtimeState != null && _runtimeState.HealHeroToFull();
            if (healed)
            {
                DeNelle.Village.HeroHealth heroHealth = null;
                if (_hero != null && _hero.TryGetComponent<DeNelle.Village.HeroHealth>(out var hh))
                    heroHealth = hh;
                if (heroHealth == null) heroHealth = DeNelle.Village.HeroHealth.Instance;
                if (heroHealth != null) heroHealth.RestoreToFull();

                if (_hero != null && _hero.TryGetComponent<DeNelle.Village.HeroAbilities>(out var ha))
                    ha.RestoreManaToFull();
            }

            Activated.Invoke(_def.id);
            if (!string.IsNullOrEmpty(_def.toast))
                ToastRequested.Invoke(_def.toast);
            ApplyCrystalTone();
        }

        /// <summary>Pulse-scales + spins the unvisited crystal — frozen under reduced motion.</summary>
        private void PulseCrystal()
        {
            if (_crystal == null) return;
            if (_reducedMotion)
            {
                _crystal.localScale = Vector3.one;
                return;
            }
            float s = 1f + Mathf.Sin(Time.time * _pulseSpeed) * _pulseDepth;
            _crystal.localScale = new Vector3(s, s, s);
            _crystal.Rotate(0f, 0.72f, 0f);
        }

        /// <summary>Applies the violet (unvisited) or gold (saved) crystal tone.</summary>
        private void ApplyCrystalTone()
        {
            Color tone = Visited ? Gold : Violet;

            if (_crystalRenderer != null && _crystalRenderer.material != null)
            {
                var mat = _crystalRenderer.material;
                mat.color = tone;
                if (mat.HasProperty("_EmissionColor"))
                {
                    mat.EnableKeyword("_EMISSION");
                    mat.SetColor("_EmissionColor", tone * (Visited ? 1.4f : 2.2f));
                }
            }

            if (_crystalLight != null)
            {
                _crystalLight.color = tone;
                _crystalLight.intensity = Visited ? 1.6f : 2.4f;
            }
        }

        /// <summary>Sets the reduced-motion preference — pins the crystal pulse flat.</summary>
        public void SetReducedMotion(bool reduced)
        {
            _reducedMotion = reduced;
        }

        private void OnDrawGizmosSelected()
        {
            if (_def == null) return;
            Gizmos.color = new Color(0.6f, 0.42f, 1f, 0.4f);
            Gizmos.DrawWireSphere(_def.position.ToWorld(), _def.radius);
        }
    }
}
