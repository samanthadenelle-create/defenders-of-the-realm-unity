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
using UnityEngine;
using UnityEngine.SceneManagement;

namespace DeNelle.Village
{
    /// <summary>Hero hit points + contact-damage intake + an IMGUI health bar.</summary>
    [DisallowMultipleComponent]
    public sealed class HeroHealth : MonoBehaviour
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
        private readonly Collider[] _buf = new Collider[24];

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
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Start() => OnHealthChanged?.Invoke(_hp, _maxHp);

        private void Update()
        {
            if (_hp <= 0f) return;

            _cooldown -= Time.deltaTime;
            if (_cooldown > 0f) return;

            Vector3 centre = transform.position + Vector3.up * 0.9f;
            int n = Physics.OverlapSphereNonAlloc(centre, EngageRadius, _buf, _enemyMask,
                                                  QueryTriggerInteraction.Ignore);
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
            if (_hp <= 0f)
            {
                Debug.Log("[HeroHealth] Hero defeated.");
                OnDied?.Invoke();   // loss-flow handler hooks here (WO46 P11)
            }
        }

        /// <summary>Heals up to max (for repair pads / potions / wave-clear).</summary>
        public void Heal(float amount)
        {
            if (amount <= 0f) return;
            _hp = Mathf.Min(_maxHp, _hp + amount);
            OnHealthChanged?.Invoke(_hp, _maxHp);
        }

        // ── IMGUI health bar (no UIDocument dependency) ───────────────────────
        private static Texture2D Px => Texture2D.whiteTexture;

        private void OnGUI()
        {
            const float w = 260f, h = 22f, x = 20f, y = 64f;

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
                hero.gameObject.AddComponent<HeroHealth>();
        }
    }
}
