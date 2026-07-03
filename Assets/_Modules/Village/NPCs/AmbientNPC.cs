// =============================================================================
// AmbientNPC — an ambient townsperson of Elarion village (Workstream D).
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
        [SerializeField] private Vector3 _homeAnchor;   // builder-set; serialized so it survives the scene save
        private float _baseY;
        private float _pauseTimer;
        private bool _hasNavMesh;
        private int _lineCursor;
        private float _seedPhase;   // per-NPC sway phase so a crowd does not pulse in sync

        /// <summary>True while the Keeper is close enough that this villager is speaking.</summary>
        public bool Speaking { get; private set; }

        // ── Animator (DEF-91: purchased NPC model pack — walk / idle / talk clips) ─
        private Animator _animator;
        private static readonly int SpeedHash   = Animator.StringToHash("Speed");
        private static readonly int TalkingHash = Animator.StringToHash("IsTalking");
        // WO-163: cached once at init — whether THIS NPC's controller actually
        // declares the param. Driving an absent param logs an error EVERY frame
        // (3,351-error spam). Guard the SetFloat/SetBool with these.
        private bool _hasSpeedParam;
        private bool _hasTalkingParam;

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

            // WO-29: when this villager's KayKit civilian model is absent (the
            // Models packs are gitignored, so a clone / this machine may not have
            // them) the builder leaves a default-white placeholder primitive — the
            // "white pill" the owner saw. Tint any such untinted body by archetype
            // at runtime so a missing model still reads as a person, never a blank
            // capsule. Skips real textured meshes (only recolours the white default).
            EnsureBodyTinted();

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

            // Grab the Animator from the mesh child (if the NPC model pack prefab is present).
            // Null-safe: locomotion and speech still work without it.
            _animator = GetComponentInChildren<Animator>();

            // WO-163: cache which params the controller actually has, so UpdateAnimator
            // never drives an absent param (was spamming 3,351 errors/run). A controller
            // with no runtimeAnimatorController has no parameters → both stay false.
            if (_animator != null && _animator.runtimeAnimatorController != null)
            {
                foreach (var p in _animator.parameters)
                {
                    if (p.nameHash == SpeedHash)   _hasSpeedParam   = true;
                    if (p.nameHash == TalkingHash) _hasTalkingParam = true;
                }
            }
        }

        private void Update()
        {
            UpdateProximity();
            UpdateRoaming();
            UpdateIdleMotion();
            UpdateAnimator();
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
            // A villager with a live NavMeshAgent lets the agent own its
            // transform — writing position here (even while the agent is paused
            // between roam legs) fights the agent and jitters. Only true idlers
            // (no agent, or a disabled one) get the sway.
            if (_agent != null && _agent.enabled)
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

        // ── Animator driver (DEF-91) ─────────────────────────────────────────

        /// <summary>
        /// Drives the NPC model pack Animator's Speed and IsTalking parameters
        /// from live agent velocity and the Speaking flag. No-ops gracefully when
        /// no Animator is present (placeholder primitives have none).
        /// </summary>
        private void UpdateAnimator()
        {
            if (_animator == null) return;

            // Speed: agent velocity magnitude when moving, 0 when idle/stopped.
            // WO-163: only drive params the controller actually declares.
            if (_hasSpeedParam)
            {
                float speed = 0f;
                if (_agent != null && _agent.enabled && !_agent.isStopped)
                    speed = _agent.velocity.magnitude;
                _animator.SetFloat(SpeedHash, speed, 0.08f, Time.deltaTime);
            }

            // IsTalking: mirrors the Speaking state so the talk clip plays while
            // the bubble is visible.
            if (_hasTalkingParam)
                _animator.SetBool(TalkingHash, Speaking);
        }

        // ── Body tint (WO-29 white-pill safety-net) ──────────────────────────

        /// <summary>
        /// Recolours any default-white / untextured placeholder body this villager
        /// carries (a primitive left when the KayKit model is missing) with an
        /// archetype tint, so it never renders as a blank white capsule. Real
        /// textured meshes are left untouched — only Unity's built-in
        /// "Default-Material" (or a null material) is replaced.
        /// </summary>
        private void EnsureBodyTinted()
        {
            var renderers = GetComponentsInChildren<Renderer>(true);
            if (renderers == null || renderers.Length == 0) return;

            Material tinted = null;   // built lazily, shared across any placeholder parts
            foreach (var r in renderers)
            {
                if (r == null) continue;
                var mat = r.sharedMaterial;
                bool isDefault = mat == null ||
                                 mat.name.StartsWith("Default-Material") ||
                                 mat.name.StartsWith("Lit");   // bare URP/Lit instance
                if (!isDefault) continue;

                if (tinted == null)
                {
                    var shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
                    if (shader == null) return;
                    tinted = new Material(shader) { name = "AmbientNPC_" + _archetype };
                    Color c = ArchetypeTint(_archetype);
                    if (tinted.HasProperty("_BaseColor")) tinted.SetColor("_BaseColor", c);
                    if (tinted.HasProperty("_Color")) tinted.SetColor("_Color", c);
                }
                r.sharedMaterial = tinted;
            }
        }

        /// <summary>Warm, distinguishable tint per townsfolk archetype (WO-29).</summary>
        private static Color ArchetypeTint(TownsfolkDialogue.Archetype a)
        {
            switch (a)
            {
                case TownsfolkDialogue.Archetype.Trader:        return Hex("c2925a"); // amber
                case TownsfolkDialogue.Archetype.Guard:         return Hex("8a6b5a"); // earthy brown
                case TownsfolkDialogue.Archetype.Child:         return Hex("7fa8c9"); // soft blue
                case TownsfolkDialogue.Archetype.Elder:         return Hex("a09890"); // grey-white
                // WO-116 wardens — each reads at a glance even as a placeholder body.
                case TownsfolkDialogue.Archetype.Blacksmith:    return Hex("5a5048"); // soot/iron grey
                case TownsfolkDialogue.Archetype.Quartermaster: return Hex("9c7b3f"); // ledger-brown ochre
                case TownsfolkDialogue.Archetype.Archmage:      return Hex("8a6fb0"); // ward-violet
                case TownsfolkDialogue.Archetype.Farmer:        return Hex("8a9a52"); // field green
                default:                                        return Hex("c2a882"); // Villager warm tan
            }
        }

        private static Color Hex(string rrggbb)
        {
            return ColorUtility.TryParseHtmlString("#" + rrggbb, out var c) ? c : Color.white;
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
                     UnityEngine.Object.FindObjectsByType<Transform>())
            {
                if (t != null && t.name.StartsWith("Hero")) return t;
            }
            return null;
        }
    }
}
