// =============================================================================
// StoryCompanion — a per-hero story companion who FOLLOWS the hero and SPEAKS
// (WO-227 / DEF-119, scoped slice).
// -----------------------------------------------------------------------------
// One companion per playable hero: a trusted figure from that hero's story who
// trails the Keeper around the village at a small offset and periodically shows
// a speech bubble with their intro + contextual lines. This is the village's
// "story presence" without a cutscene — the deferred opening cutscene, tutorial
// step-gating, and per-companion unique models are OUT OF SCOPE here (they need
// WO-222). This component only FOLLOWS and TALKS.
//
// ── What it reuses (no new frameworks) ───────────────────────────────────────
//   • Speech       — TownsfolkBubble (this module's self-building world-space
//                    bubble; same class the ambient townsfolk use).
//   • Dialogue     — CompanionDialogue (per-hero line table, twin of
//                    TownsfolkDialogue).
//   • Follow       — the "carrot trailing the hero" leash pattern from
//                    DeNelle.Pets.PetHeroLeash, simplified: the companion trails
//                    a few metres BEHIND/BESIDE the hero, keeping an inner ring so
//                    it never blocks the hero or sits in the camera's centre spot.
//                    It uses a NavMeshAgent when one is present (so it paths the
//                    baked village NavMesh) and falls back to a plain lerp when
//                    there is no agent / no NavMesh — it never errors.
//   • Hero lookup  — name-based ("Hero ..."), the same fallback AmbientNPC and
//                    VillageNpcInjector use (the project defines no "Player" tag).
//
// ── Non-interference (hard requirement) ──────────────────────────────────────
//   • It is on the "Ignore Raycast" layer and carries NO collider that could
//     shove the hero, and its NavMeshAgent (if any) keeps a generous inner ring.
//   • It never touches combat, the pet, the hero's input, or the camera.
//   • Every cross-reference is null-guarded; a missing hero just parks it idle.
//
// Spawned by StoryCompanionInjector (self-bootstrapping DDOL, no scene edit).
// =============================================================================

using DeNelle.Core.State;
using UnityEngine;
using UnityEngine.AI;

namespace DeNelle.Village
{
    /// <summary>
    /// A story companion that trails the chosen hero around the village and
    /// speaks intro + contextual lines via a <see cref="TownsfolkBubble"/>.
    /// Spawned at runtime by <see cref="StoryCompanionInjector"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class StoryCompanion : MonoBehaviour
    {
        // ── Follow tuning (mirrors PetHeroLeash's "trail, don't block" intent) ─
        // Trail this far behind/beside the hero; never crowd closer than the
        // inner ring (so it stays out of the hero's path + camera centre).
        private const float TrailDistance = 3.2f;
        private const float InnerRing     = 2.2f;
        // Beyond this the companion hurries to catch up (sprint multiplier).
        private const float CatchUpRange  = 9f;
        private const float WalkSpeed     = 3.0f;
        private const float SprintSpeed   = 5.5f;
        // Side offset so it walks AT the hero's shoulder rather than dead behind
        // (reads as a companion, not a shadow). Sign flips per-instance via seed.
        private const float SideOffset    = 1.4f;

        // ── Speech tuning ────────────────────────────────────────────────────
        // The companion speaks its intro once when the scene settles, then cycles
        // a contextual line on this cadence while it has a hero to walk beside.
        private const float IntroDelay        = 2.0f;   // let the scene settle first
        private const float LineHold          = 5.5f;   // a line stays up this long
        private const float LineGap           = 9.0f;   // quiet gap between lines

        // ── Runtime ──────────────────────────────────────────────────────────
        private HeroClass _hero = HeroClass.Knight;
        private Transform _heroT;
        private NavMeshAgent _agent;
        private TownsfolkBubble _bubble;

        private float _resolveTimer;
        private float _speakTimer;
        private bool  _introSpoken;
        private bool  _bubbleUp;
        private int   _lineCursor;
        private float _sideSign = 1f;

        // ── Configuration ────────────────────────────────────────────────────

        /// <summary>
        /// Sets which hero's companion this is — drives the name + line pool.
        /// Called by the injector before <see cref="Start"/> runs.
        /// </summary>
        public void Configure(HeroClass hero)
        {
            _hero = hero;
        }

        /// <summary>Assigns the speech bubble (the injector wires this).</summary>
        public void SetBubble(TownsfolkBubble bubble)
        {
            _bubble = bubble;
        }

        /// <summary>Assigns the hero transform to trail (the injector wires this).</summary>
        public void SetHero(Transform hero)
        {
            _heroT = hero;
        }

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Start()
        {
            // Stable per-instance side (left/right shoulder) so it isn't dead-centre.
            _sideSign = (gameObject.GetInstanceID() & 1) == 0 ? 1f : -1f;

            _agent = GetComponent<NavMeshAgent>();
            if (_agent != null)
            {
                if (_agent.isOnNavMesh)
                {
                    _agent.speed = WalkSpeed;
                    _agent.angularSpeed = 360f;
                    _agent.acceleration = 16f;
                    _agent.stoppingDistance = 0.3f;
                    _agent.radius = Mathf.Min(_agent.radius, 0.35f);   // slim, won't shove
                    _agent.avoidancePriority = 60;                     // yields to the hero/pets
                }
                else
                {
                    // No NavMesh under us — disable the agent so it never warns,
                    // and we fall back to a plain lerp follow.
                    _agent.enabled = false;
                }
            }

            if (_bubble == null) _bubble = GetComponentInChildren<TownsfolkBubble>();
            _bubble?.Hide();

            if (_heroT == null) _heroT = ResolveHeroFallback();

            _speakTimer = IntroDelay;
        }

        private void Update()
        {
            ResolveHeroIfNeeded();
            UpdateFollow();
            UpdateSpeech();
        }

        // ── Hero resolution ──────────────────────────────────────────────────

        private void ResolveHeroIfNeeded()
        {
            if (_heroT != null) return;
            _resolveTimer -= Time.deltaTime;
            if (_resolveTimer <= 0f)
            {
                _resolveTimer = 1.0f;
                _heroT = ResolveHeroFallback();
            }
        }

        /// <summary>
        /// Name-based Keeper lookup (matches AmbientNPC / VillageNpcInjector): the
        /// village hero rig is named "Hero (...)"; the project defines no "Player"
        /// tag, so FindGameObjectWithTag would throw. Returns null when none yet.
        /// </summary>
        private static Transform ResolveHeroFallback()
        {
            foreach (var t in
                     UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t != null && t.name.StartsWith("Hero")) return t;
            }
            return null;
        }

        // ── Follow (trail-the-hero, never block) ─────────────────────────────

        /// <summary>
        /// Trails the hero at a shoulder offset: targets a point behind the hero's
        /// facing, nudged to one side, and stops once inside the inner ring so it
        /// never crowds or shoves the Keeper. Paths via NavMeshAgent when present,
        /// else lerps directly. Null-safe — parks idle with no hero.
        /// </summary>
        private void UpdateFollow()
        {
            if (_heroT == null)
            {
                if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
                    _agent.isStopped = true;
                return;
            }

            // Trail point: behind the hero along its facing, offset to one shoulder.
            Vector3 heroPos = _heroT.position;
            Vector3 back = -_heroT.forward; back.y = 0f;
            if (back.sqrMagnitude < 0.0004f) back = Vector3.back;
            back.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, back) * (SideOffset * _sideSign);
            Vector3 target = heroPos + back * TrailDistance + side;

            Vector3 self = transform.position;
            Vector3 flatToHero = heroPos - self; flatToHero.y = 0f;
            float distHero = flatToHero.magnitude;

            // Inside the inner ring → hold position (don't push into the hero).
            bool tooClose = distHero <= InnerRing;
            float speed = distHero > CatchUpRange ? SprintSpeed : WalkSpeed;

            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                _agent.speed = speed;
                if (tooClose)
                {
                    _agent.isStopped = true;
                    FaceHero();
                }
                else
                {
                    _agent.isStopped = false;
                    if (NavMesh.SamplePosition(target, out var hit, 3f, NavMesh.AllAreas))
                        _agent.SetDestination(hit.position);
                    else
                        _agent.SetDestination(target);
                }
            }
            else
            {
                // Plain lerp fallback (no NavMesh): glide toward the trail point,
                // keep our own Y so we don't sink/float.
                if (!tooClose)
                {
                    Vector3 flatTarget = new Vector3(target.x, self.y, target.z);
                    transform.position =
                        Vector3.MoveTowards(self, flatTarget, speed * Time.deltaTime);
                }
                FaceHero();
            }
        }

        /// <summary>Smoothly turns the companion to face the hero.</summary>
        private void FaceHero()
        {
            if (_heroT == null) return;
            Vector3 dir = _heroT.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0004f) return;
            Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation =
                Quaternion.Slerp(transform.rotation, target, Time.deltaTime * 6f);
        }

        // ── Speech (intro, then cycle contextual lines) ──────────────────────

        /// <summary>
        /// Speaks the intro line once the scene has settled, then alternates a
        /// held contextual line and a quiet gap. Only speaks while a hero exists;
        /// no-ops gracefully without a bubble.
        /// </summary>
        private void UpdateSpeech()
        {
            if (_bubble == null || _heroT == null) return;

            _speakTimer -= Time.deltaTime;
            if (_speakTimer > 0f) return;

            if (!_introSpoken)
            {
                _introSpoken = true;
                ShowLine(CompanionDialogue.IntroFor(_hero));
                return;
            }

            if (_bubbleUp)
            {
                // A line is currently up → hide it and start the quiet gap.
                _bubbleUp = false;
                _bubble.Hide();
                _speakTimer = LineGap;
            }
            else
            {
                // Quiet gap elapsed → show the next contextual line.
                ShowLine(CompanionDialogue.LineFor(_hero, _lineCursor));
                _lineCursor++;
            }
        }

        /// <summary>Shows one line and arms the hold timer.</summary>
        private void ShowLine(string line)
        {
            if (_bubble == null || string.IsNullOrEmpty(line)) { _speakTimer = LineGap; return; }
            _bubble.Show(CompanionDialogue.NameFor(_hero), line);
            _bubbleUp = true;
            _speakTimer = LineHold;
        }
    }
}
