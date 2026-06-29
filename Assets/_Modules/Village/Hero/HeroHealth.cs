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

        // Gear v1: equipped armor (fractional damage reduction). Lazily resolved in TakeDamage.
        private GearLoadout _gear;

        // ── Contact-damage tuning (first-pass) ────────────────────────────────
        private const float EngageRadius   = 1.5f;  // enemy must be this close to strike
        private const float DamageInterval = 1.0f;  // seconds between contact ticks
        private const float DamagePerEnemy = 6f;    // FALLBACK only — used if an attacker's real
                                                    // ContactDamage is non-positive (mis-authored def).
        private const int   MaxEnemiesPerTick = 4;  // cap so a swarm can't one-shot

        private float _hp;
        private float _cooldown;
        private int   _enemyMask;
        private bool  _isDead;
        private readonly Collider[] _buf = new Collider[24];

        // ── v2 talent behavioural state (WO-566 effect interpreter) ───────────────
        // Reusable buffer of the enemies struck this contact tick — the reflect handler
        // bounces a share of the damage taken back to them. Sized to match _buf.
        private readonly Enemy[] _attackerBuf = new Enemy[24];

        // Last Stand capstone: an active low-HP defensive window (+DR +reflect) on cd.
        private bool  _lastStandActive;
        private float _lastStandUntil;     // Time.time the active window ends
        private float _lastStandReadyAt;   // Time.time the cooldown frees the next trigger
        private float _lastStandDr;        // extra fractional DR during the window
        private float _lastStandReflect;   // extra reflect fraction during the window

        // Eternal Aegis capstone: an auto-emergency full-invuln window on a long cd.
        // (Capstone exclusivity means a Knight holds Last Stand OR Eternal Aegis, never both.)
        private float _aegisReadyAt;       // Time.time the cooldown frees the next trigger
        private const float AegisAutoThreshold = 0.25f;  // auto-fires below this projected HP fraction

        // Legendary Resolve (shared): one cheat-death per run.
        private bool _revivedThisRun;

        /// <summary>True while the Last Stand window is live (auto-expires when the timer passes).</summary>
        private bool LastStandActive
        {
            get
            {
                if (_lastStandActive && Time.time >= _lastStandUntil) _lastStandActive = false;
                return _lastStandActive;
            }
        }

        // ── Respawn (DEF-102) ─────────────────────────────────────────────────
        // The hero is NOT the lose condition — the Heart is (a Heart breach
        // escalates to the ATB / Defend-the-Tower flow; there is no game-over
        // screen for the wave loop). So when the hero falls it enters a brief
        // "down" beat then RESPAWNS at its start point rather than reloading the
        // scene. Tunables are SerializeField so feel can be dialled in-editor.
        [Header("Death / Respawn (DEF-102)")]
        [Tooltip("Seconds the hero stays down (no control, death pose) before respawning.")]
        [SerializeField] private float _downSeconds = 1.75f;
        [Tooltip("Fraction of max HP restored on respawn (1 = full).")]
        [Range(0.1f, 1f)]
        [SerializeField] private float _respawnHpFraction = 1f;
        [Tooltip("Seconds of damage immunity after respawn so the hero isn't instantly re-killed.")]
        [SerializeField] private float _respawnInvulnSeconds = 1.5f;

        private Vector3 _spawnPosition;          // captured in Awake — respawn anchor
        private float   _invulnUntil;            // Time.time at which post-respawn invuln ends

        // WO-284/285: death/revive animation routes through the canonical ActorAnimator
        // driver (Dead bool latch + DeathDir). Guarded internally — a controller without
        // a Death state is a silent no-op, never the per-frame param-spam pitfall.
        private ActorAnimator _actor;

        // Cached siblings for death-stop + haptics. All optional — resolved in
        // Awake and only used through null-safe calls, so a hero missing any of
        // them simply skips that bit of feedback.
        private HeroLocomotion     _locomotion;
        private HeroAbilities      _abilities;
        private HeroImpactFeedback _impactFeedback;
        private PlayerAttackController _pac;   // perfect-parry source (same GameObject)

        // WO-543: equipped armor + accessories add a flat HP bonus folded into the EFFECTIVE max.
        // GearLoadout.GearHpBonus is the single source; resolved lazily + synced in Update so
        // equipping a +HP ring grows the bar and tops the hero up by the delta (and unequipping
        // shrinks it + clamps). 0 when no GearLoadout / no HP gear, so existing combat is unchanged.
        private int GearHpBonus => _gear != null ? _gear.GearHpBonus : 0;
        private int _appliedGearHpBonus;

        // v2 talents (WO talent-tree): Vitality / Elarion's Blessing fold a fractional max-HP
        // bonus into the effective max, the SAME way gear HP does (so the bar grows + the hero
        // tops up by the delta when a +HP node is learned mid-run, and clamps when respec'd).
        private string HeroClassOrDefault
        {
            get
            {
                if (_abilities == null) _abilities = GetComponent<HeroAbilities>();
                return _abilities != null ? _abilities.HeroClass : "knight";
            }
        }
        private int TalentHpBonus
        {
            get
            {
                float m = DeNelle.Village.Talents.HeroTalentModifiers.MaxHpMultiplier(HeroClassOrDefault);
                return Mathf.RoundToInt(_maxHp * Mathf.Max(0f, m - 1f));
            }
        }
        private int EffectiveBonus => GearHpBonus + TalentHpBonus;

        public float MaxHp    => _maxHp + EffectiveBonus;
        public float Hp       => _hp;
        public float Fraction => MaxHp > 0f ? Mathf.Clamp01(_hp / MaxHp) : 0f;
        public bool  IsAlive  => _hp > 0f;

        // ── WO-493 #5 / WO-497: HERO injured stance (the hero half; the ENEMY half is
        //    Enemy.DriveAnimator). Below the low-HP cutoff the hero reads "wounded":
        //    the Injured locomotion swap (ActorAnimator.SetInjured), a breathing red
        //    screen-edge vignette, a slight move slow, and an optional heartbeat cue.
        //    All flag-gated by FeatureFlags.HeroInjuredStance. ─────────────────────
        private const float InjuredFraction = 0.30f;  // enter injured below this HP fraction
        private bool  _injured;                        // current injured latch (set on threshold cross)
        private HeroInjuredVignette _vignette;         // optional edge vignette (resolved in Awake)
        private float _heartbeatCooldown;              // throttles the optional heartbeat cue
        private static AudioClip s_heartbeatClip;      // generated once, shared

        // Movement slow seam: a global multiplier the hero's locomotion can read to
        // ease the felt move speed while wounded. Defaults to 1 (no change). Kept as a
        // public static so HeroLocomotion can consume it WITHOUT a hard reference back
        // to HeroHealth (and so this WO touches only HeroHealth/vignette/factory).
        public static float MoveSpeedMultiplier { get; private set; } = 1f;
        private const float InjuredMoveScale = 0.85f;  // ~15% slower while wounded

        /// <summary>True while the hero is below the low-HP injured cutoff.</summary>
        public bool IsInjured => _injured;

        /// <summary>Fired whenever HP changes — args = (current, max).</summary>
        public event Action<float, float> OnHealthChanged;
        /// <summary>Fired once when HP reaches zero.</summary>
        public event Action OnDied;

        private void Awake()
        {
            Instance = this;
            // FIX 2: start at the EFFECTIVE max (base + gear + talent maxHpPct), not the bare
            // serialized base. With a talent like Vitality the effective max can be ~195 vs a
            // base 100, so seeding _hp from _maxHp made the hero spawn at 100/195 (~0.51 frac) —
            // the bar read half-empty. MaxHp is the same effective max the Fraction calc uses.
            _hp = MaxHp;
            _enemyMask = LayerMask.GetMask("Enemy");
            if (_enemyMask == 0) _enemyMask = ~0;   // "Enemy" layer missing — scan all

            _locomotion     = GetComponent<HeroLocomotion>();
            _abilities      = GetComponent<HeroAbilities>();
            _impactFeedback = GetComponent<HeroImpactFeedback>();
            if (!TryGetComponent(out _actor)) _actor = gameObject.AddComponent<ActorAnimator>();

            // WO-493 #5 / WO-497: the hero's low-HP screen-edge vignette. Self-attached so
            // it needs no prefab wiring (mirrors HeroHitReaction). Resolved up-front here.
            if (!TryGetComponent(out _vignette)) _vignette = gameObject.AddComponent<HeroInjuredVignette>();
            MoveSpeedMultiplier = 1f;   // start un-slowed every fresh hero

            // Capture the spawn point as the respawn anchor. Resolved later in
            // HandleDeath against the Heart if the recorded point is unsafe.
            _spawnPosition = transform.position;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        private void Start()
        {
            // Resolve gear up-front so the starting bar reflects any persisted HP gear, then
            // top the hero to the effective full so a +HP loadout doesn't read as "missing HP".
            if (_gear == null) _gear = GetComponent<GearLoadout>();
            _appliedGearHpBonus = EffectiveBonus;
            _hp = MaxHp;
            OnHealthChanged?.Invoke(_hp, MaxHp);
        }

        // WO-543: keep the effective max in sync with the equipped HP gear. On a bonus INCREASE
        // (equipped a +HP ring), top the hero up by the delta so the new HP is usable; on a
        // DECREASE (unequipped), clamp current HP to the smaller max. Cheap; runs each frame.
        private void SyncGearHp()
        {
            if (_gear == null) _gear = GetComponent<GearLoadout>();
            int now = EffectiveBonus;   // gear HP + talent HP folded together
            if (now == _appliedGearHpBonus) return;
            int delta = now - _appliedGearHpBonus;
            _appliedGearHpBonus = now;
            if (delta > 0) _hp += delta;           // grow with the new max
            _hp = Mathf.Min(_hp, MaxHp);           // clamp to the (possibly smaller) max
            OnHealthChanged?.Invoke(_hp, MaxHp);
        }

        // In Defend-the-Tower the hero is a safe turret on the stand — the TOWER is
        // what enemies attack, not the hero. Resolve once, then skip contact damage.
        private bool _modeChecked;
        private bool _safeTurretMode;

        private void Update()
        {
            SyncGearHp();   // WO-543: fold equipped HP gear into the effective max (top-up / clamp on change)
            if (_hp <= 0f) { UpdateInjuredState(); return; }

            // WO-493 #5 / WO-497: re-evaluate the wounded stance every frame off the single
            // HP-fraction source of truth. Cheap: the latch only flips the visuals/anim/slow
            // on an actual threshold CROSS; while injured it just pulses the optional heartbeat.
            UpdateInjuredState();

            if (!_modeChecked)
            {
                _modeChecked = true;
                _safeTurretMode = false;   // Defend-the-Tower mode removed — always normal village contact damage
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
                if (en != null && !en.IsDead)
                {
                    if (attackers < _attackerBuf.Length) _attackerBuf[attackers] = en;
                    attackers++;
                }
            }

            if (attackers > 0)
            {
                _cooldown = DamageInterval;
                // WO-419: the actual "enemy attacks hero" beat — an enemy entered the 1.5 m
                // engage ring and the hero self-applies the contact tick. Tracing it here (the
                // ground truth for the seam bug) shows in a headless run that OuterWorld enemies
                // now reach + damage the hero, not just path toward it. Throttled ~1/sec.
                DeNelle.Core.Diagnostics.FlowTrace.Throttle("EnemyAggro", "hero-hit", 1f,
                    $"hero struck by {attackers} adjacent enemy(s) within {EngageRadius:F2}m " +
                    $"(scene='{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}').");
                float hpBeforeTick = _hp;
                // WO-591 RCA: honour each adjacent enemy's REAL authored ContactDamage (from
                // enemies.json, post wave-scaling) instead of a single flat number — so berserker
                // (15) vs necromancer (18) vs walker (8) finally differ, and ApplyWaveScaling's
                // damageMult reaches the hero. Capped at MaxEnemiesPerTick so a swarm can't one-shot.
                int counted = Mathf.Min(attackers, Mathf.Min(MaxEnemiesPerTick, _attackerBuf.Length));
                float tickDamage = 0f;
                for (int i = 0; i < counted; i++)
                {
                    var atk = _attackerBuf[i];
                    float dmg = atk != null ? atk.ContactDamage : 0f;
                    tickDamage += dmg > 0f ? dmg : DamagePerEnemy; // fallback if a def authored 0
                }
                TakeDamage(tickDamage);
                // WO-566: v2 talent reflect (Retaliation Surge) + the Last Stand reflect portion
                // bounce a fraction of the damage ACTUALLY taken (post block/DR) back onto the
                // contact attackers. Identity (0) until a reflect node is learned.
                ApplyReflect(hpBeforeTick - _hp, Mathf.Min(attackers, _attackerBuf.Length));
            }
        }

        /// <summary>
        /// WO-566: bounce a fraction of the damage just taken back to the contact attackers
        /// (Retaliation Surge reflect + Last Stand reflect window). Data-driven — the fraction
        /// comes from <see cref="HeroTalentModifiers.ReflectFraction"/> (+ the active Last Stand
        /// reflect), so a hero with no reflect node reflects nothing. Split evenly across the
        /// enemies that struck this tick; each share routes through Enemy.TakeDamageFrom so the
        /// hit shows a number + flinches toward the hero.
        /// </summary>
        private void ApplyReflect(float damageTaken, int attackerCount)
        {
            if (damageTaken <= 0f || attackerCount <= 0) return;
            string heroClass = HeroClassOrDefault;
            float frac = DeNelle.Village.Talents.HeroTalentModifiers.ReflectFraction(heroClass);
            if (LastStandActive) frac += _lastStandReflect;
            if (frac <= 0f) return;
            float total = damageTaken * frac;
            if (total <= 0f) return;
            float share = total / attackerCount;
            int reflectedTo = 0;
            for (int i = 0; i < attackerCount; i++)
            {
                var en = _attackerBuf[i];
                _attackerBuf[i] = null;   // release the reference
                if (en == null || en.IsDead) continue;
                en.TakeDamageFrom(share, transform.position + Vector3.up * 1.0f);
                reflectedTo++;
            }
            if (reflectedTo > 0)
                DeNelle.Core.Diagnostics.FlowTrace.Throttle("HeroTalents", "reflect", 1f,
                    $"reflected {total:F0} dmg ({frac:P0} of {damageTaken:F0}) across {reflectedTo} attacker(s)" +
                    (LastStandActive ? " [Last Stand window]" : "") + ".");
        }

        /// <summary>Applies <paramref name="amount"/> damage; fires events; handles death.</summary>
        public void TakeDamage(float amount)
        {
            // WO-triage 2026-06-27 (HP-desync): owner saw stagger/limp + DEFEAT while the HUD read
            // 100/100. Log WHICH HeroHealth instance + scene actually takes damage — if this id/scene
            // differs from the one the HUD binds (the [Flow:HUD] HP line), the arena spawns a SECOND
            // hero and the overworld HUD stays bound to the untouched 100/100 body. Proves it from data.
            DeNelle.Core.Diagnostics.FlowTrace.Step("HeroHealth",
                $"TakeDamage id={GetInstanceID()} scene='{UnityEngine.SceneManagement.SceneManager.GetActiveScene().name}' " +
                $"amount={amount:F1} hpBefore={_hp:F1}/{_maxHp:F1} invuln={(Time.time < _invulnUntil)}");
            if (_hp <= 0f || amount <= 0f) return;
            // DEF-102: post-respawn grace — ignore damage during the invuln window
            // so a hero respawning into a lingering melee isn't instantly re-killed.
            if (Time.time < _invulnUntil) return;

            // Perfect parry — a hit landing inside the player's parry window is NEGATED and turned
            // into the riposte payoff (Knight block now; the caster's magical deflect reuses the
            // same OpenParryWindow seam). Same-GameObject lookup, lazily cached.
            if (_pac == null) _pac = GetComponent<PlayerAttackController>();
            if (_pac != null && _pac.TryConsumeParry())
            {
                _pac.OnParrySuccess(transform.position + Vector3.up);
                return;   // fully negate the parried hit
            }

            // Gear v1: equipped armor reduces incoming damage (fractional). Lazily-resolved;
            // graceful — no GearLoadout / no armor = no reduction, so combat is unchanged.
            if (_gear == null) _gear = GetComponent<GearLoadout>();
            if (_gear != null && _gear.ArmorDefense > 0f)
                amount *= (1f - _gear.ArmorDefense);

            // v2 talents (Knight V1): Guardian Stance can fully BLOCK a hit; Iron Resolve /
            // Resilience / defense nodes reduce the rest. Identity (no block, 0 DR) until a
            // defensive node is learned, so combat is unchanged at baseline.
            string heroClass = HeroClassOrDefault;

            // WO-566: arm the emergency low-HP capstones (Last Stand / Eternal Aegis) BEFORE the
            // hit lands so their window protects against this very blow. Capstone exclusivity
            // means at most one of these is ever owned at a time, so they never stack.
            UpdateEmergencyTalents(heroClass, amount);

            // An Eternal Aegis (or respawn) invuln window may have just opened above — re-honor it.
            if (Time.time < _invulnUntil) return;

            if (DeNelle.Village.Talents.HeroTalentModifiers.RollBlock(heroClass))
            {
                DeNelle.Core.Diagnostics.FlowTrace.Throttle("HeroTalents", "block", 1f,
                    "Guardian Stance blocked a hit (full negate).");
                VFXManager.Play(VFXType.Impact_Physical, transform.position + Vector3.up * 1.0f);
                return;
            }
            float talentDr = DeNelle.Village.Talents.HeroTalentModifiers.IncomingDamageReduction(heroClass);
            // WO-566: Last Stand folds an extra DR slice on top while its window is live.
            if (LastStandActive) talentDr += _lastStandDr;
            talentDr = Mathf.Clamp(talentDr, 0f, 0.95f);
            if (talentDr > 0f) amount *= (1f - talentDr);

            _hp = Mathf.Max(0f, _hp - amount);
            OnHealthChanged?.Invoke(_hp, MaxHp);

            // WO-566: Legendary Resolve (shared) — cheat death ONCE per run. When a hit would drop
            // the hero to 0 and the revive is still available, restore to a fraction of max HP with
            // a brief grace window instead of dying. Identity until the node is learned.
            if (_hp <= 0f && !_isDead && !_revivedThisRun
                && DeNelle.Village.Talents.HeroTalentModifiers.TryGetRevive(heroClass, out float reviveFrac))
            {
                _revivedThisRun = true;
                _hp = MaxHp * reviveFrac;
                _invulnUntil = Time.time + 1.5f;   // brief grace so the revive isn't instantly re-killed
                DeNelle.Core.Diagnostics.FlowTrace.Step("HeroTalents",
                    $"Legendary Resolve REVIVE: cheat death — restored to {reviveFrac:P0} HP (once per run).");
                VFXManager.Play(VFXType.Impact_Heal, transform.position + Vector3.up * 1.0f);
                OnHealthChanged?.Invoke(_hp, MaxHp);
                return;
            }

            // ── Combat feel (additive) ────────────────────────────────────────
            // VFXManager.Play and HitStopManager.DoImpact are static + null-safe,
            // so absent managers are a silent no-op. Contact ticks use the Light
            // tier (shake only, no time-freeze) so the 1 s cadence never stutters.
            VFXManager.Play(VFXType.Impact_Physical, transform.position + Vector3.up * 1.0f);
            _impactFeedback?.PlayHaptic(0.25f, 0.12f);
            GameSfx.PlayHeroHit();   // hero took a hit — audible grunt/impact (was silent)

            if (_hp <= 0f && !_isDead)
            {
                // Idempotent: _isDead guards re-entry so a swarm landing several
                // lethal ticks in one frame can't start multiple death coroutines.
                _isDead = true;
                Debug.Log("[HeroHealth] Hero defeated.");
                HitStopManager.DoImpact(HitTier.Heavy);   // one dramatic beat on death
                PlayDeathAnim();
                OnDeath?.Invoke();
                OnDied?.Invoke();   // legacy event kept for existing listeners
                StartCoroutine(HandleDeath());
            }
            else
            {
                HitStopManager.DoImpact(HitTier.Light);   // subtle shake per hit
            }
        }

        /// <summary>
        /// WO-566: arm the low-HP EMERGENCY capstones just before a hit resolves.
        /// <para>
        /// Last Stand — when the projected post-hit HP drops below its threshold and the
        /// cooldown is free, opens a window granting extra DR + reflect (consumed in TakeDamage
        /// / ApplyReflect). Eternal Aegis — an "active" capstone modelled in V1 as an AUTO
        /// emergency: below a small projected HP fraction it triggers a full-invuln window on a
        /// long cooldown (reuses the existing _invulnUntil grace). Both are data-driven (params
        /// from the node) and identity (no-op) until the respective capstone is learned.
        /// </para>
        /// OWNER-DECISION FLAG: Eternal Aegis is authored as a PLAYER-ACTIVATED active. V1 has no
        /// free hotkey / HUD button for a non-slot capstone (keyboard 1-4 are removed, mobile-first),
        /// so it auto-fires here. If the owner wants player-activation, call <see cref="ActivateInvuln"/>
        /// from a HUD button / bound input instead and drop the auto-trigger branch.
        /// </summary>
        private void UpdateEmergencyTalents(string heroClass, float incomingApprox)
        {
            float maxHp = MaxHp;
            if (maxHp <= 0f) return;
            float projectedFrac = Mathf.Clamp01((_hp - Mathf.Max(0f, incomingApprox)) / maxHp);

            // Last Stand
            if (!LastStandActive && Time.time >= _lastStandReadyAt
                && DeNelle.Village.Talents.HeroTalentModifiers.TryGetLastStand(
                       heroClass, out float lsTh, out float lsDr, out float lsRef, out float lsDur, out float lsCd)
                && projectedFrac < lsTh)
            {
                _lastStandActive  = true;
                _lastStandDr      = lsDr;
                _lastStandReflect = lsRef;
                _lastStandUntil   = Time.time + lsDur;
                _lastStandReadyAt = Time.time + lsCd;
                DeNelle.Core.Diagnostics.FlowTrace.Step("HeroTalents",
                    $"Last Stand TRIGGERED: -{lsDr:P0} dmg + reflect {lsRef:P0} for {lsDur:F0}s (cd {lsCd:F0}s).");
                VFXManager.Play(VFXType.Impact_ShockwaveRing, transform.position + Vector3.up * 1.0f);
            }

            // Eternal Aegis (auto-emergency invuln; capstone-exclusive with Last Stand)
            if (Time.time >= _aegisReadyAt
                && DeNelle.Village.Talents.HeroTalentModifiers.TryGetInvuln(heroClass, out float aeDur, out float aeCd)
                && projectedFrac < AegisAutoThreshold)
            {
                _aegisReadyAt = Time.time + aeCd;
                ActivateInvuln(aeDur);
                DeNelle.Core.Diagnostics.FlowTrace.Step("HeroTalents",
                    $"Eternal Aegis TRIGGERED: {aeDur:F0}s invulnerability (cd {aeCd:F0}s).");
                VFXManager.Play(VFXType.Impact_Heal, transform.position + Vector3.up * 1.0f);
            }
        }

        /// <summary>
        /// WO-566: open a damage-immunity window for <paramref name="seconds"/> (reuses the same
        /// _invulnUntil grace the respawn flow uses — every TakeDamage early-returns while it is
        /// live). Public so a future HUD button / bound input can drive Eternal Aegis as a
        /// player-activated active. Extends (never shortens) any existing window.
        /// </summary>
        public void ActivateInvuln(float seconds)
        {
            if (seconds <= 0f) return;
            _invulnUntil = Mathf.Max(_invulnUntil, Time.time + seconds);
        }

        /// <summary>WO-566: clear the per-run talent state — re-arms Legendary Resolve's one
        /// cheat-death and ends any lingering Last Stand window. Cooldowns (Last Stand / Eternal
        /// Aegis) are intentionally NOT reset here; they free on their own timers.</summary>
        private void ResetTalentRunState()
        {
            _revivedThisRun  = false;
            _lastStandActive = false;
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
            yield return new WaitForSeconds(Mathf.Max(0.1f, _downSeconds));

            // RAID-DEATH EVAC: dying in an enemy-owned base ends the raid — retreat
            // to the home hub (MainCastle_Hall) instead of respawning in place. The
            // hub load resets the hero fresh on the far side. Player-owned scenes
            // keep the normal in-place respawn below.
            if (DeNelle.Village.SceneOwnership.IsEnemyOwned)
            {
                Debug.Log("[HeroHealth] Hero down in enemy territory — raid ends, retreating to home hub.");
                DeNelle.Core.SceneRouter.GoCastle();
                yield break;
            }

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
            _hp       = MaxHp * Mathf.Clamp01(_respawnHpFraction <= 0f ? 1f : _respawnHpFraction);
            _cooldown = 0f;
            ResetTalentRunState();   // WO-566: a fresh life re-arms revive / clears Last Stand
            // DEF-102: short grace so the hero isn't re-killed the instant it lands
            // back in a melee. Consumed in TakeDamage.
            _invulnUntil = Time.time + Mathf.Max(0f, _respawnInvulnSeconds);
            // Clear any death pose so the revived hero animates normally again.
            ClearDeathAnim();
            if (_locomotion != null) _locomotion.enabled = true;
            if (_abilities  != null) _abilities.enabled  = true;
            OnHealthChanged?.Invoke(_hp, MaxHp);
            VFXManager.Play(VFXType.Impact_Heal, transform.position + Vector3.up * 1.0f);
            Debug.Log($"[HeroHealth] Hero respawned at {position} (hp={Mathf.CeilToInt(_hp)}, " +
                      $"invuln={_respawnInvulnSeconds:F1}s).");
        }

        // ── Death animation (WO-284/285, fully guarded) ───────────────────────
        // Latches the hero controller's Death state via the canonical Dead bool
        // (ActorAnimator). The death clip holds its last frame and never flickers
        // back to idle; Revive() clears it on respawn. Safe no-op on a controller
        // with no Death state.
        private void PlayDeathAnim() => _actor?.Die(DeathDirection.Fall);

        private void ClearDeathAnim() => _actor?.Revive();

        /// <summary>Heals up to max (for repair pads / potions / wave-clear).</summary>
        public void Heal(float amount)
        {
            if (amount <= 0f) return;
            _hp = Mathf.Min(MaxHp, _hp + amount);
            OnHealthChanged?.Invoke(_hp, MaxHp);
            UpdateInjuredState();   // T-HP fix (owner 2026-06-27): clear the limp/injured stance once healed back above the cutoff
            VFXManager.Play(VFXType.Impact_Heal, transform.position + Vector3.up * 1.0f);
        }

        /// <summary>
        /// Restores the hero to FULL HP (the "heal up between fights at home base" beat —
        /// called when the hero returns to town after an arena battle, win, flee, OR death).
        /// Works from ANY HP including 0: a hero that DIED in the arena must come back to town
        /// at full HP, not 0 (which one-shot it on the next fight). Also clears the death latch
        /// so contact damage + control resume, mirroring Respawn's revive (without moving the
        /// hero — the arena owns the town warp). Fires the HP-changed event the HUD/bar listen
        /// to + the heal VFX so the top-off reads on screen.
        /// </summary>
        public void RestoreToFull()
        {
            bool wasDown = _isDead || _hp <= 0f;
            _appliedGearHpBonus = EffectiveBonus;   // re-sync so SyncGearHp doesn't double-apply after a full restore
            _hp = MaxHp;
            ResetTalentRunState();   // WO-566: town return = a fresh run — re-arm revive / clear Last Stand
            // If the hero had gone down, clear the death state so it isn't stuck "dead" on the
            // town return (Respawn does this on its own path; we mirror it here without warping).
            if (wasDown)
            {
                _isDead = false;
                _cooldown = 0f;
                ClearDeathAnim();
                if (_locomotion != null) _locomotion.enabled = true;
                if (_abilities  != null) _abilities.enabled  = true;
            }
            OnHealthChanged?.Invoke(_hp, MaxHp);
            UpdateInjuredState();   // T-HP fix (owner 2026-06-27): a town-return restore to full must CLEAR the limp/injured stance carried out of the fight (was lingering -> "health full but still limping")
            VFXManager.Play(VFXType.Impact_Heal, transform.position + Vector3.up * 1.0f);
        }

        // ── WO-493 #5 / WO-497: HERO injured stance ───────────────────────────
        // Single source of truth for "wounded": HP fraction vs the cutoff. On a
        // threshold CROSS it drives the animator's Injured swap, toggles the red
        // edge vignette, and sets the move-speed multiplier; while injured it pulses
        // the optional heartbeat cue. All flag-gated (FeatureFlags.HeroInjuredStance);
        // when the flag is off the hero is forced healthy (no swap, full speed, dark
        // vignette) so the feature can be disabled cleanly without a rebuild.
        private void UpdateInjuredState()
        {
            bool flagOn = DeNelle.Core.FeatureFlags.HeroInjuredStance;
            // Injured only while alive + below the cutoff + the flag is on. A dead hero
            // is "not injured" — the Death anim/respawn owns that beat, not the limp.
            bool injured = flagOn && _hp > 0f && Fraction < InjuredFraction;

            if (injured != _injured)
            {
                _injured = injured;
                _actor?.SetInjured(injured);
                _vignette?.SetInjured(injured);
                MoveSpeedMultiplier = injured ? InjuredMoveScale : 1f;
                _heartbeatCooldown = 0f;   // let the first beat land promptly on entry
                Debug.Log($"[HeroHealth] Injured stance {(injured ? "ON" : "OFF")} " +
                          $"(hp={Mathf.CeilToInt(_hp)}/{Mathf.CeilToInt(_maxHp)}, frac={Fraction:F2}).");
            }

            // Optional heartbeat cue while wounded — paced ~1/sec, routed through the
            // audio service (null-safe). Generated once so it works with no audio asset.
            if (_injured)
            {
                _heartbeatCooldown -= Time.deltaTime;
                if (_heartbeatCooldown <= 0f)
                {
                    _heartbeatCooldown = 1.0f;
                    if (s_heartbeatClip == null) s_heartbeatClip = GenerateHeartbeat();
                    DeNelle.Core.CoreServices.Audio?.PlaySfx(s_heartbeatClip, 0.35f);
                }
            }
        }

        /// <summary>
        /// Builds a short two-thump "lub-dub" heartbeat clip procedurally so the cue
        /// works with no authored audio asset (mirrors GameSfx's generated SFX). One
        /// low sine burst, a gap, then a softer second burst.
        /// </summary>
        private static AudioClip GenerateHeartbeat()
        {
            const int rate = 44100;
            const float dur = 0.55f;
            int n = Mathf.CeilToInt(rate * dur);
            var data = new float[n];
            // Two thumps: lub at ~0.00s (loud), dub at ~0.18s (softer).
            AddThump(data, rate, 0.00f, 0.10f, 55f, 0.9f);
            AddThump(data, rate, 0.18f, 0.10f, 48f, 0.6f);
            var clip = AudioClip.Create("HeroHeartbeat", n, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static void AddThump(float[] data, int rate, float start, float len,
                                     float freq, float gain)
        {
            int s = Mathf.Clamp(Mathf.RoundToInt(start * rate), 0, data.Length - 1);
            int e = Mathf.Min(data.Length, s + Mathf.RoundToInt(len * rate));
            for (int i = s; i < e; i++)
            {
                float t = (i - s) / (float)rate;
                float env = Mathf.Exp(-t * 24f);   // fast percussive decay
                data[i] += Mathf.Sin(2f * Mathf.PI * freq * t) * env * gain;
            }
        }

        // ── IDamageableStructure ─────────────────────────────────────────────
        bool IDamageableStructure.IsAlive => IsAlive;
        void IDamageableStructure.ApplyContactDamage(float amount) => TakeDamage(amount);

        // ── IMGUI health bar (no UIDocument dependency) ───────────────────────
        private static Texture2D Px => Texture2D.whiteTexture;

        // Reference resolution the bar is laid out against. IMGUI draws in raw
        // pixels with no auto-scaling, so on a high-DPI / high-resolution player
        // build the same fixed Rect lands at the wrong size & place — which is how
        // this bar ended up "HUGE and floating mid-screen". We scale the whole pass
        // by GUI.matrix against this reference so the bar is a CONSISTENT compact
        // size and stays anchored top-left, just under the Heart bar, on any screen.
        private const float RefWidth  = 1920f;
        private const float RefHeight = 1080f;

        private void OnGUI()
        {
            // WO-411 #2 (duplicate hero bar): this legacy IMGUI bar is suppressed whenever the
            // real uGUI village HUD is present (VillageHudController registers CoreServices.Hud and
            // now owns hero vitals). It remains only as a FALLBACK for HUD-less scenes.
            if (DeNelle.Core.CoreServices.Hud != null) return;

            // Compact bar, anchored top-left under the top-left Heart HP bar.
            const float w = 200f, h = 18f, x = 24f, y = 92f;

            // Uniform reference-resolution scale (letterbox-safe: use the smaller
            // axis ratio so the bar never balloons on ultrawide / tall screens).
            float scale = Mathf.Min(Screen.width / RefWidth, Screen.height / RefHeight);
            if (scale <= 0f) scale = 1f;
            Matrix4x4 prevMatrix = GUI.matrix;
            GUI.matrix = Matrix4x4.TRS(Vector3.zero, Quaternion.identity,
                                       new Vector3(scale, scale, 1f));

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
                fontSize = 12,
                fontStyle = FontStyle.Bold,
                alignment = TextAnchor.MiddleCenter,
            };
            GUI.Label(new Rect(x, y, w, h),
                      $"Hero   {Mathf.CeilToInt(_hp)} / {Mathf.CeilToInt(MaxHp)}", style);

            GUI.color = Color.white;
            GUI.matrix = prevMatrix;
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

                // NOTE: the hero deliberately gets NO over-the-head FloatingHealthBar.
                // The hero's HP is already shown in the HUD (VillageHudController hero
                // HP bar) plus the top-left IMGUI readout (HeroHealth.OnGUI), so a
                // floating world-space bar over the player just rendered as a green
                // pill edge-on from the over-shoulder camera. FloatingHealthBar is for
                // enemies/units only (Enemy.EnsureHealthBar) — the hero is excluded
                // here on purpose. Do not re-add an Attach() call for the hero.
            }
        }
    }
}
