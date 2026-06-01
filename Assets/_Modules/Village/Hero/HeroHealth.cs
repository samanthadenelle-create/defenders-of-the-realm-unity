// =============================================================================
// HeroHealth — the hero's HP, contact damage from nearby enemies, and a visible
// health bar. Restores the "hero can take damage + has a health bar" loop the
// owner asked for (DEF playtest 2026-05-28).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// DESIGN (deliberately self-contained + low-risk):
//   • The hero is transform-driven with a manual CapsuleCast and NO physical
//     collider (HeroLocomotion). Adding a collider would make the hero collide
//     with itself, so instead HeroHealth pulls damage IN: each interval it scans
//     for living enemies within EngageRadius (Enemy layer) and takes contact
//     damage. Combined with EnemyBrain's hero-engage targeting, enemies that
//     reach the hero now actually hurt it.
//   • The bar is drawn with IMGUI (OnGUI) — no UIDocument / PanelSettings / uGUI
//     dependency, so it always renders in player builds (UI-Toolkit HUDs have
//     repeatedly come up empty in this project).
//   • Self-bootstraps: a tiny persistent manager attaches HeroHealth to the hero
//     (the HeroAbilities GameObject) whenever a scene with a hero loads.
//
// Tuning constants are first-pass — tune for feel.
// =============================================================================

using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Combat;

namespace DeNelle.Village
{
    /// <summary>Hero hit points + contact-damage intake + an IMGUI health bar.</summary>
    [DisallowMultipleComponent]
    public sealed class HeroHealth : MonoBehaviour, IDamageableStructure
    {
        public static HeroHealth Instance { get; private set; }

        [SerializeField] private float _maxHp = 100f;

        // ── Contact-damage tuning (first-pass) ────────────────────────────────
        private const float EngageRadius   = 1.5f;  // enemy must be this close to strike
        private const float DamageInterval = 1.0f;  // seconds between contact ticks
        private const float DamagePerEnemy = 6f;    // damage per adjacent enemy per tick
        private const int   MaxEnemiesPerTick = 4;  // cap so a swarm can't one-shot

        private float _hp;
        private float _cooldown;
        private int   _enemyMask;
        private bool  _isDead;
        private readonly Collider[] _buf = new Collider[24];

        // ── Respawn ───────────────────────────────────────────────────────────
        // The hero is NOT the lose condition — the Heart is (a Heart breach
        // escalates to the ATB / Defend-the-Tower flow; there is no game-over
        // screen for the wave loop). So when the hero falls it RESPAWNS at its
        // start point after a short delay rather than reloading the scene.
        private const float RespawnDelaySeconds = 4f;
        private Vector3 _spawnPosition;          // captured in Awake — respawn anchor

        // Cached siblings for death-stop + haptics. All optional — resolved in
        // Awake and only used through null-safe calls, so a hero missing any of
        // them simply skips that bit of feedback.
        private HeroLocomotion     _locomotion;
        private HeroAbilities      _abilities;
        private HeroImpactFeedback _impactFeedback;

        public float MaxHp    => _maxHp;
        public float Hp       => _hp;
        public float Fraction => _maxHp > 0f ? Mathf.Clamp01(_hp / _maxHp) : 0f;
        public bool  IsAlive  => _hp > 0f;

        /// <summary>Fired whenever HP changes — args = (current, max).</summary>
        public event Action<float, float> OnHealthChanged;
        /// <summary>Fired once when HP reaches zero.</summary>
        public event Action OnDied;

        private void Awake()
        {
            Instance = this;
            _hp = _maxHp;
            _enemyMask = LayerMask.GetMask("Enemy");
            if (_enemyMask == 0) _enemyMask = ~0;   // "Enemy" layer missing — scan all

            _locomotion     = GetComponent<HeroLocomotion>();
            _abilities      = GetComponent<HeroAbilities>();
            _impactFeedback = GetComponent<HeroImpactFeedback>();

            // Capture the spawn point as the respawn anchor. Resolved later in
            // HandleDeath against the Heart if the recorded point is unsafe.
            _spawnPosition = transform.position;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Start() => OnHealthChanged?.Invoke(_hp, _maxHp);

        // In Defend-the-Tower the hero is a safe turret on the stand — the TOWER is
        // what enemies attack, not the hero. Resolve once, then skip contact damage.
        private bool _modeChecked;
        private bool _safeTurretMode;

        private void Update()
        {
            if (_hp <= 0f) return;

            if (!_modeChecked)
            {
                _modeChecked = true;
                _safeTurretMode = FindAnyObjectByType<DeNelle.Village.PatriciaLightController>() != null;
            }
            if (_safeTurretMode) return;   // enemies target the tower, not the hero

            _cooldown -= Time.deltaTime;
            if (_cooldown > 0f) return;

            Vector3 centre = transform.position + Vector3.up * 0.9f;
            // Use Collide (not Ignore): PatriciaLight ("Defend the Tower") spawns its
            // enemies with TRIGGER colliders, so an Ignore sweep finds nothing and the
            // hero never takes damage there. Collide matches the hero/pet attack sweeps.
            int n = Physics.OverlapSphereNonAlloc(centre, EngageRadius, _buf, _enemyMask,
                                                  QueryTriggerInteraction.Collide);
            int attackers = 0;
            for (int i = 0; i < n; i++)
            {
                var en = _buf[i] != null ? _buf[i].GetComponentInParent<Enemy>() : null;
                if (en != null && !en.IsDead) attackers++;
            }

            if (attackers > 0)
            {
                _cooldown = DamageInterval;
                TakeDamage(DamagePerEnemy * Mathf.Min(attackers, MaxEnemiesPerTick));
            }
        }

        /// <summary>Applies <paramref name="amount"/> damage; fires events; handles death.</summary>
        public void TakeDamage(float amount)
        {
            if (_hp <= 0f || amount <= 0f) return;
            _hp = Mathf.Max(0f, _hp - amount);
            OnHealthChanged?.Invoke(_hp, _maxHp);

            // ── Combat feel (additive) ────────────────────────────────────────
            // VFXManager.Play and HitStopManager.DoImpact are static + null-safe,
            // so absent managers are a silent no-op. Contact ticks use the Light
            // tier (shake only, no time-freeze) so the 1 s cadence never stutters.
            VFXManager.Play(VFXType.Impact_Physical, transform.position + Vector3.up * 1.0f);
            _impactFeedback?.PlayHaptic(0.25f, 0.12f);

            if (_hp <= 0f && !_isDead)
            {
                _isDead = true;
                Debug.Log("[HeroHealth] Hero defeated.");
                HitStopManager.DoImpact(HitTier.Heavy);   // one dramatic beat on death
                OnDeath?.Invoke();
                OnDied?.Invoke();   // legacy event kept for existing listeners
                StartCoroutine(HandleDeath());
            }
            else
            {
                HitStopManager.DoImpact(HitTier.Light);   // subtle shake per hit
            }
        }

        /// <summary>Event fired the moment the hero dies (before the coroutine delay).</summary>
        public event System.Action OnDeath;

        /// <summary>
        /// Death → timed respawn. Disables locomotion + abilities immediately so
        /// the hero is no longer controllable, holds a brief "down" beat, then
        /// revives the hero at its spawn point (near the Heart) at full HP.
        /// <para>
        /// DESIGN (DEF-102): the hero is NOT the lose condition — a Heart breach
        /// is what escalates the run (WaveManager.TriggerBreach → ATB / Defend-
        /// the-Tower). Reloading the scene on hero death would be jarring and
        /// design-wrong, so the hero respawns instead. The old reflection-driven
        /// GameOverUI path is removed: GameOverUI is not placed in the Village
        /// scene, so that path always fell through to a hard scene reload.
        /// </para>
        /// </summary>
        private IEnumerator HandleDeath()
        {
            // Disable control immediately so a dead hero can't be walked or cast.
            if (_locomotion != null) _locomotion.enabled = false;
            if (_abilities  != null) _abilities.enabled  = false;

            // Brief "down" beat. WaitForSeconds is scaled time, but the lethal
            // HitStop above restores Time.timeScale within ~0.1s, so this elapses.
            yield return new WaitForSeconds(RespawnDelaySeconds);

            // Respawn at the recorded spawn point, falling back to the Heart's
            // position if that point is no longer meaningful (e.g. it was captured
            // at origin before the hero had been placed in the scene).
            Vector3 target = _spawnPosition;
            if (target == Vector3.zero)
            {
                var heart = FindAnyObjectByType<HeartController>();
                if (heart != null)
                    target = heart.transform.position + heart.transform.forward * 4f;
            }
            Respawn(target);
        }

        /// <summary>
        /// Revives the hero at <paramref name="position"/> at full HP and restores
        /// control. Uses NavMeshAgent.Warp when the hero is agent-driven so the
        /// teleport isn't fought by the agent (HeroLocomotion drives a kinematic
        /// NavMeshAgent); also clears the death flag so contact damage resumes.
        /// </summary>
        public void Respawn(Vector3 position)
        {
            var agent = GetComponent<UnityEngine.AI.NavMeshAgent>();
            if (agent != null && agent.enabled && agent.isOnNavMesh)
                agent.Warp(position);
            else
                transform.position = position;

            _isDead   = false;
            _hp       = _maxHp;
            _cooldown = 0f;
            if (_locomotion != null) _locomotion.enabled = true;
            if (_abilities  != null) _abilities.enabled  = true;
            OnHealthChanged?.Invoke(_hp, _maxHp);
            VFXManager.Play(VFXType.Impact_Heal, transform.position + Vector3.up * 1.0f);
            Debug.Log("[HeroHealth] Hero respawned at the Heart.");
        }

        /// <summary>Heals up to max (for repair pads / potions / wave-clear).</summary>
        public void Heal(float amount)
        {
            if (amount <= 0f) return;
            _hp = Mathf.Min(_maxHp, _hp + amount);
            OnHealthChanged?.Invoke(_hp, _maxHp);
            VFXManager.Play(VFXType.Impact_Heal, transform.position + Vector3.up * 1.0f);
        }

        // ── IDamageableStructure ─────────────────────────────────────────────
        bool IDamageableStructure.IsAlive => IsAlive;
        void IDamageableStructure.ApplyContactDamage(float amount) => TakeDamage(amount);

        // ── IMGUI health bar (no UIDocument dependency) ───────────────────────
        private static Texture2D Px => Texture2D.whiteTexture;

        private void OnGUI()
        {
            const float w = 260f, h = 22f, x = 20f, y = 110f;

            // Backdrop + empty track.
            GUI.color = new Color(0f, 0f, 0f, 0.55f);
            GUI.DrawTexture(new Rect(x - 3f, y - 3f, w + 6f, h + 6f), Px);
            GUI.color = new Color(0.16f, 0.16f, 0.20f, 0.95f);
            GUI.DrawTexture(new Rect(x, y, w, h), Px);

            // Fill — red → green by fraction.
            float frac = Fraction;
            GUI.color = Color.Lerp(new Color(0.85f, 0.18f, 0.18f),
                                   new Color(0.30f, 0.85f, 0.40f), frac);
            GUI.DrawTexture(new Rect(x, y, w * frac, h), Px);

            // Label.
            GUI.color = Color.white;
            var style = new GUIStyle(GUI.skin.label)
            {
                fontSize = 13,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            GUI.Label(new Rect(x, y, w, h),
                      $"Hero   {Mathf.CeilToInt(_hp)} / {Mathf.CeilToInt(_maxHp)}", style);
            GUI.color = Color.white;
        }
    }

    /// <summary>
    /// Persistent bootstrap that attaches <see cref="HeroHealth"/> to the hero
    /// (the HeroAbilities GameObject) whenever a scene containing a hero is loaded.
    /// Polls briefly because the hero may spawn a frame or two after scene load.
    /// </summary>
    internal sealed class HeroHealthBootstrap : MonoBehaviour
    {
        private float _retry;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            var go = new GameObject("HeroHealthBootstrap");
            DontDestroyOnLoad(go);
            go.AddComponent<HeroHealthBootstrap>();
        }

        private void Update()
        {
            if (HeroHealth.Instance != null) return;   // already attached
            _retry -= Time.deltaTime;
            if (_retry > 0f) return;
            _retry = 0.5f;

            var hero = FindAnyObjectByType<HeroAbilities>();
            if (hero != null && hero.GetComponent<HeroHealth>() == null)
            {
                var health = hero.gameObject.AddComponent<HeroHealth>();
                // Combat feel: screen flash on damage + death slow-mo (additive).
                if (hero.GetComponent<HeroHitReaction>() == null)
                    hero.gameObject.AddComponent<HeroHitReaction>();

                // WO-178: floating world-space HP bar over the hero, matching the
                // enemy bars so the field reads as one themed combat-HUD set. The
                // top-left IMGUI bar (HeroHealth.OnGUI) stays as the screen readout;
                // this is the over-the-head bar. Always shown (hideAtFull:false) so
                // the player can always locate their hero in a melee.
                float headOffset = 2.2f;
                Renderer rend = hero.GetComponentInChildren<Renderer>();
                if (rend != null)
                {
                    float worldTop = rend.bounds.max.y - hero.transform.position.y;
                    if (worldTop > 0.1f) headOffset = worldTop + 0.4f;
                }
                FloatingHealthBar.Attach(
                    hero.gameObject,
                    fraction: () => health.Fraction,
                    isDead:   () => !health.IsAlive,
                    heightOffset: headOffset,
                    hideAtFull: false,
                    destroyOnDead: false);
            }
        }
    }
}
