// =============================================================================
// AmbientNPC — an ambient townsperson of Avalon village (Workstream D).
// -----------------------------------------------------------------------------
// One ambient villager: a KayKit civilian model that either WANDERS the village
// on the baked NavMesh or stands IDLE at an authored spot, and shows an
// engage-on-approach word bubble when the Keeper draws near.
//
// This is the village twin of the dungeon's Bryn — the proximity / hysteresis /
// line-pick pattern is lifted from Bryn.cs, re-homed in DeNelle.Village so it
// carries no DeNelle.Dungeons dependency (module isolation). The speech bubble
// itself is TownsfolkBubble (this module's twin of WandererBubble).
//
// ── Behaviour ──
//   • Wanderers pick a random NavMesh point inside a roam radius of their home
//     anchor, walk there via a NavMeshAgent, pause, then pick another. If no
//     NavMesh is present the agent is simply disabled and the villager stands
//     idle — it never errors.
//   • Idlers stay put and gently sway / turn, like Bryn.
//   • When the Keeper is within speakRadius a TownsfolkBubble fades in with the
//     next line from this villager's TownsfolkDialogue pool; it hides again past
//     a hysteresis margin so an edge-loitering Keeper does not flicker it.
//   • While speaking, a wanderer halts and faces the Keeper — it "engages."
//
// The scene builder (VillageSceneBuilder) adds this by reflection, sets the
// serialized fields through SerializedObject, and calls Configure(...). The
// Keeper transform is handed in via SetHero — the NPC never reaches into the
// scene for it.
//
// Legacy Input Manager only — this component takes no input at all; it just
// watches a transform. (No UnityEngine.InputSystem anywhere.)
// =============================================================================

using UnityEngine;
using UnityEngine.AI;

namespace DeNelle.Village
{
    /// <summary>
    /// An ambient village townsperson — wanders or idles, and speaks a word
    /// bubble when the Keeper approaches. Built into the Village scene by
    /// <c>VillageSceneBuilder</c>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AmbientNPC : MonoBehaviour
    {
        [Header("Identity")]
        [Tooltip("Which dialogue archetype this villager speaks as (drives the " +
                 "line pool + the bubble's display name).")]
        [SerializeField] private TownsfolkDialogue.Archetype _archetype =
            TownsfolkDialogue.Archetype.Villager;

        [Header("Movement")]
        [Tooltip("When true the villager roams the NavMesh; when false it stands " +
                 "idle at its home anchor.")]
        [SerializeField] private bool _wander = true;

        [Tooltip("Roam radius (world units) around the home anchor a wanderer " +
                 "picks its destinations within.")]
        [SerializeField] private float _roamRadius = 7f;

        [Tooltip("Walk speed of a wandering villager (world units / second).")]
        [SerializeField] private float _walkSpeed = 1.6f;

        [Tooltip("Seconds a wanderer pauses on reaching a destination before " +
                 "choosing the next one.")]
        [SerializeField] private float _pauseSeconds = 2.5f;

        [Header("Proximity speech")]
        [Tooltip("World-unit distance within which the villager speaks to the Keeper.")]
        [SerializeField] private float _speakRadius = 5.5f;

        [Tooltip("Hysteresis margin added to the speak radius for the fade-OUT " +
                 "threshold — stops the bubble flickering at the edge.")]
        [SerializeField] private float _speakHysteresis = 1.5f;

        [Header("Idle motion")]
        [Tooltip("Idle Y-sway frequency (Hz) — gentle, lifelike.")]
        [SerializeField] private float _swayHz = 0.45f;

        [Tooltip("Idle Y-sway amplitude (world units).")]
        [SerializeField] private float _swayAmplitude = 0.04f;

        [Tooltip("When true the idle sway is disabled (reduced motion).")]
        [SerializeField] private bool _reducedMotion;

        [Header("Wiring")]
        [Tooltip("The world-space speech bubble. Assigned by the scene builder.")]
        [SerializeField] private TownsfolkBubble _bubble;

        // ── Runtime ──────────────────────────────────────────────────────────

        private NavMeshAgent _agent;
        private Transform _hero;
        private Vector3 _homeAnchor;
        private float _baseY;
        private float _pauseTimer;
        private bool _hasNavMesh;
        private int _lineCursor;
        private float _seedPhase;   // per-NPC sway phase so a crowd does not pulse in sync

        /// <summary>True while the Keeper is close enough that this villager is speaking.</summary>
        public bool Speaking { get; private set; }

        // ── Configuration ────────────────────────────────────────────────────

        /// <summary>
        /// Configures the villager from the scene builder. <paramref name="homeAnchor"/>
        /// is the world point a wanderer roams around (and an idler stands at);
        /// it defaults to the current transform position when not set explicitly.
        /// Called after the component is added + its fields are wired.
        /// </summary>
        public void Configure(TownsfolkDialogue.Archetype archetype, bool wander,
                              Vector3 homeAnchor)
        {
            _archetype = archetype;
            _wander = wander;
            _homeAnchor = homeAnchor;
        }

        /// <summary>
        /// Sets the Keeper transform this villager watches for the proximity
        /// check. The scene builder / a runtime caller assigns this — the NPC
        /// never searches the scene itself.
        /// </summary>
        public void SetHero(Transform hero)
        {
            _hero = hero;
        }

        /// <summary>Assigns the speech bubble (the scene builder uses this when wiring by reflection).</summary>
        public void SetBubble(TownsfolkBubble bubble)
        {
            _bubble = bubble;
        }

        /// <summary>Sets the reduced-motion preference — disables the idle sway.</summary>
        public void SetReducedMotion(bool reduced)
        {
            _reducedMotion = reduced;
        }

        // ── Lifecycle ────────────────────────────────────────────────────────

        private void Start()
        {
            if (_homeAnchor == Vector3.zero) _homeAnchor = transform.position;
            _baseY = transform.position.y;
            _seedPhase = Random.value * Mathf.PI * 2f;
            _lineCursor = Random.Range(0, 64);   // varied opening line per villager

            // Resolve the Keeper if the builder did not hand one over — a tagged
            // "Player" GameObject, else the Hero rig the village builder names.
            if (_hero == null) _hero = ResolveHeroFallback();

            _agent = GetComponent<NavMeshAgent>();
            _hasNavMesh = _wander && _agent != null && _agent.isOnNavMesh;

            if (_agent != null)
            {
                if (_hasNavMesh)
                {
                    _agent.speed = _walkSpeed;
                    _agent.angularSpeed = 240f;
                    _agent.acceleration = 12f;
                    _agent.stoppingDistance = 0.2f;
                    PickNewDestination();
                }
                else
                {
                    // No NavMesh (or an idle villager) — disable the agent so it
                    // never warns or drifts; the villager stands its ground.
                    _agent.enabled = false;
                }
            }

            _bubble?.Hide();
        }

        private void Update()
        {
            UpdateProximity();
            UpdateRoaming();
            UpdateIdleMotion();
        }

        // ── Proximity speech ─────────────────────────────────────────────────

        /// <summary>
        /// Drives the speaking state from the Keeper's distance with asymmetric
        /// thresholds (enter at <see cref="_speakRadius"/>, leave at the wider
        /// hysteresis radius). On a fresh entry into range the bubble shows the
        /// next line from this villager's pool.
        /// </summary>
        private void UpdateProximity()
        {
            if (_hero == null)
            {
                if (Speaking) { Speaking = false; _bubble?.Hide(); }
                return;
            }

            Vector3 a = transform.position; a.y = 0f;
            Vector3 b = _hero.position;     b.y = 0f;
            float dist = (a - b).magnitude;

            if (!Speaking && dist <= _speakRadius)
            {
                Speaking = true;
                string name = TownsfolkDialogue.NameFor(_archetype);
                string line = TownsfolkDialogue.LineFor(_archetype, _lineCursor);
                _lineCursor++;   // next approach steps to the next line
                _bubble?.Show(name, line);
            }
            else if (Speaking && dist > _speakRadius + _speakHysteresis)
            {
                Speaking = false;
                _bubble?.Hide();
            }
        }

        // ── Roaming ──────────────────────────────────────────────────────────

        /// <summary>
        /// Steps a wandering villager: while speaking it halts and turns to face
        /// the Keeper (it "engages"); otherwise it walks to its current
        /// destination, pauses on arrival, then picks a new one.
        /// </summary>
        private void UpdateRoaming()
        {
            if (!_hasNavMesh || _agent == null || !_agent.enabled) return;

            // Engage: a speaking villager stops and faces the Keeper.
            if (Speaking)
            {
                _agent.isStopped = true;
                FaceHero();
                return;
            }
            _agent.isStopped = false;

            // Arrived at the destination — pause, then roam again.
            if (!_agent.pathPending &&
                _agent.remainingDistance <= _agent.stoppingDistance + 0.05f)
            {
                _pauseTimer -= Time.deltaTime;
                if (_pauseTimer <= 0f) PickNewDestination();
            }
        }

        /// <summary>
        /// Picks a fresh random NavMesh destination within the roam radius of the
        /// home anchor and sends the agent there, then arms the arrival pause.
        /// </summary>
        private void PickNewDestination()
        {
            if (_agent == null || !_agent.enabled || !_agent.isOnNavMesh) return;

            for (int attempt = 0; attempt < 6; attempt++)
            {
                Vector2 disc = Random.insideUnitCircle * _roamRadius;
                Vector3 candidate = _homeAnchor + new Vector3(disc.x, 0f, disc.y);
                if (NavMesh.SamplePosition(candidate, out NavMeshHit hit, 3f, NavMesh.AllAreas))
                {
                    _agent.SetDestination(hit.position);
                    break;
                }
            }
            _pauseTimer = _pauseSeconds + Random.Range(-0.5f, 1.5f);
        }

        /// <summary>Smoothly turns the villager to face the Keeper while engaged.</summary>
        private void FaceHero()
        {
            if (_hero == null) return;
            Vector3 dir = _hero.position - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.0004f) return;
            Quaternion target = Quaternion.LookRotation(dir, Vector3.up);
            transform.rotation =
                Quaternion.Slerp(transform.rotation, target, Time.deltaTime * 6f);
        }

        // ── Idle motion ──────────────────────────────────────────────────────

        /// <summary>
        /// Gives a non-wandering (or NavMesh-less) villager a gentle Y-sway so it
        /// reads as alive rather than frozen. A wanderer actually moving on the
        /// NavMesh is left to the agent; the sway only applies when stationary.
        /// </summary>
        private void UpdateIdleMotion()
        {
            // A villager the agent is actively driving owns its own transform.
            if (_hasNavMesh && _agent != null && _agent.enabled &&
                !_agent.isStopped && _agent.velocity.sqrMagnitude > 0.02f)
            {
                _baseY = transform.position.y;
                return;
            }
            if (_reducedMotion) return;

            float y = _baseY + Mathf.Sin(Time.time * _swayHz * 2f * Mathf.PI + _seedPhase)
                               * _swayAmplitude;
            Vector3 p = transform.position;
            transform.position = new Vector3(p.x, y, p.z);

            // An idle villager that is not roaming also faces the Keeper when
            // they are near, so engaging feels deliberate.
            if (Speaking && !(_hasNavMesh && _agent != null && _agent.enabled))
                FaceHero();
        }

        // ── Helpers ──────────────────────────────────────────────────────────

        /// <summary>
        /// Last-resort Keeper lookup when the builder / controller did not call
        /// SetHero — a GameObject whose name starts with "Hero" (the village
        /// builder names the hero rig "Hero (Blaise)"). Returns null when none
        /// exists; the villager simply stays silent until a hero is assigned.
        ///
        /// <para>Deliberately name-based, not tag-based: the project's
        /// TagManager defines no "Player" tag, and FindGameObjectWithTag throws
        /// on an undefined tag.</para>
        /// </summary>
        private Transform ResolveHeroFallback()
        {
            foreach (var t in
                     UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
            {
                if (t != null && t.name.StartsWith("Hero")) return t;
            }
            return null;
        }
    }
}
