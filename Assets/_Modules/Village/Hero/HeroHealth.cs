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

        /// <summary>Stops locomotion/abilities immediately, then shows GameOver UI
        /// (or reloads the scene as a fallback) after a short dramatic pause.</summary>
        private IEnumerator HandleDeath()
        {
            // Disable locomotion and abilities immediately
            if (_locomotion != null) _locomotion.enabled = false;
            if (_abilities  != null) _abilities.enabled  = false;

            // Brief pause for death feel
            yield return new WaitForSeconds(1.5f);

            // Try the game-over UI first. GameOverUI lives in the default
            // Assembly-CSharp (no asmdef, global namespace), which this
            // DeNelle.Village asmdef cannot reference — so we resolve it by type
            // name across loaded assemblies and drive it via reflection (the
            // project's cross-assembly bridge pattern). Falls back to a reload.
            var gameOver = FindGameOverUi();
            if (gameOver != null)
            {
                gameOver.gameObject.SetActive(true);
                var show = gameOver.GetType().GetMethod("Show",
                    System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
                show?.Invoke(gameOver, null);
            }
            else
            {
                // Fallback: reload the scene
                UnityEngine.SceneManagement.SceneManager.LoadScene(
                    UnityEngine.SceneManagement.SceneManager.GetActiveScene().name);
            }
        }

        /// <summary>
        /// Finds the GameOverUI MonoBehaviour by type name. It compiles into the
        /// default Assembly-CSharp (unreachable from this asmdef), so it is located
        /// reflectively rather than referenced directly. Returns null if absent.
        /// </summary>
        private static MonoBehaviour FindGameOverUi()
        {
            System.Type t = null;
            foreach (var asm in System.AppDomain.CurrentDomain.GetAssemblies())
            {
                t = asm.GetType("GameOverUI");
                if (t != null) break;
            }
            if (t == null) return null;
            var found = UnityEngine.Object.FindObjectsByType(
                t, FindObjectsInactive.Include, FindObjectsSortMode.None);
            return (found != null && found.Length > 0) ? found[0] as MonoBehaviour : null;
        }

        /// <summary>Revives the hero at <paramref name="position"/> (future use).</summary>
        public void Respawn(Vector3 position)
        {
            _isDead = false;
            _hp = _maxHp;
            transform.position = position;
            if (_locomotion != null) _locomotion.enabled = true;
            if (_abilities  != null) _abilities.enabled  = true;
            OnHealthChanged?.Invoke(_hp, _maxHp);
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
                hero.gameObject.AddComponent<HeroHealth>();
                // Combat feel: screen flash on damage + death slow-mo (additive).
                if (hero.GetComponent<HeroHitReaction>() == null)
                    hero.gameObject.AddComponent<HeroHitReaction>();
            }
        }
    }
}
