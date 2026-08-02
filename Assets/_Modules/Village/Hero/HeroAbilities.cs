// =============================================================================
// HeroAbilities — Blaise's Q/W/E/R combat block (Week-4).
// -----------------------------------------------------------------------------
// Port spec Part 3 / Part 5 Week 4: src/modules/village/hero/castAbility.ts +
// heroAbilities.ts -> HeroAbilities.cs. Reads ability tuning from abilities.json
// via AbilityCatalog; resolves casts against the village's IDamageable enemies;
// uses Unity built-in ParticleSystems as placeholder VFX (the React shockwave
// ring + combatFx — final art is later).
//
//   Q  Arcane Bolt    — strike: single hit on the nearest enemy in range.
//   W  Frost Nova     — aoe:    blast around the hero, freezes what it catches.
//   E  Healing Beacon — heal:   restores Heart HP, no enemy damage.
//   R  Meteor Strike  — meteor: blast centred on the nearest enemy cluster.
//
// PORT NOTE — castAbility.ts mutated a CombatState struct directly. The Unity
// port has no CombatState yet (the village combat registry is a later week), so
// HeroAbilities owns its own mana pool + per-slot cooldown timers and discovers
// enemies via Physics.OverlapSphere -> IDamageable. The cast math (cooldown /
// mana gate, nearest-in-range, blast-radius hit test) is line-equivalent to
// castAbility.ts.
//
// MODULE ISOLATION (port spec Part 2): this file is in DeNelle.Village, so it
// CAN see DeNelle.Village.Enemy directly — but it deliberately talks to enemies
// only through DeNelle.Core.Combat.IDamageable, the same seam the Pets module
// uses. That keeps the cast code independent of the concrete Enemy component.
// =============================================================================

using System.Collections.Generic;
using DeNelle.Core.Combat;
using DeNelle.Core.Diagnostics; // WO3: FlowTrace self-reporting for the mana-over-time potion
using DeNelle.Core.State;     // WO-36: GameState backstop for hero class self-resolve
using DeNelle.Village.Talents; // WO-36: talent -> ability-stat multipliers
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// Blaise's hero ability block — the Mage's Q/W/E/R kit. Holds the mana
    /// pool + per-slot cooldown timers, resolves a queued cast each frame, and
    /// spawns placeholder particle VFX. Lives on the hero rig; wired by
    /// <see cref="VillageController"/> (the integrator hands it the Heart ref).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class HeroAbilities : MonoBehaviour
    {
        [Header("Hero class")]
        [Tooltip("Hero class id read from abilities.json. v2 foundation = mage (Blaise).")]
        [SerializeField] private string _heroClass = AbilityCatalog.DefaultClass;

        [Header("Mana")]
        [Tooltip("Max mana pool. The React combat state used 0-10 (heroAbilities.ts).")]
        [SerializeField] private float _maxMana = 10f;

        [Tooltip("Mana regenerated per second. React: 0.9/s * manaRegenMul (Aether pet perk).")]
        [SerializeField] private float _manaRegenPerSecond = 0.9f;

        [Tooltip("Mana-regen multiplier — raised by the Aether Sprite's Mana Tide perk.")]
        [SerializeField, Min(0.1f)] private float _manaRegenMultiplier = 1f;

        [Header("Scene refs (wired by VillageController / integrator)")]
        [Tooltip("Elarion — the Heart. Healing Beacon (E) restores its HP.")]
        [SerializeField] private HeartController _heart;

        [Tooltip("Layers an ability hit-test sweeps for IDamageable targets. Set to the Enemy layer.")]
        [SerializeField] private LayerMask _enemyMask = ~0;

        [Header("Placeholder VFX")]
        [Tooltip("Optional — a ParticleSystem spawned at the cast point. " +
                 "When null, HeroAbilities builds a built-in particle burst at runtime.")]
        [SerializeField] private ParticleSystem _castVfxPrefab;

        [Tooltip("Enemy collision radius added to ability range — matches ENEMY_HIT_R in castAbility.ts.")]
        [SerializeField] private float _enemyHitRadius = 0.85f;

        // --- runtime state (the React CombatState fields HeroAbilities owns) ---
        private float _mana;

        // WO3: mana-over-time potion (Mana Draught). The potion adds mana GRADUALLY
        // (e.g. +3%/s of max for 10s = +30% total), not an instant chunk. We hold a
        // per-second rate + an expiry time; Update() drips it in until the window ends.
        // Re-entrant: a second potion REFRESHES the window + ADDS the remaining target
        // onto the rate (simple, correct — never double-counts already-delivered mana).
        private float _manaOverTimeRate;   // mana per second added while active (absolute units)
        private float _manaOverTimeUntil;  // Time.time at which the drip stops

        // WO-614 hook 2 (Oathmend healOverTime): HP drip mirroring the mana-over-time pattern
        // above. Ticks HeroHealth.RegenTick (silent, no per-frame VFX strobe) each frame while
        // the window is open. Cached HeroHealth is resolved lazily + survives the body swap.
        private float _hpOverTimeRate;     // HP per second healed while active
        private float _hpOverTimeUntil;    // Time.time at which the drip stops
        private HeroHealth _heroHealth;    // cached hero HP (WO-614 hooks 2 & 3)

        private readonly float[] _cooldownRemaining = new float[4]; // indexed by AbilitySlot

        // WO-574: per-ability cooldowns for the player-assignable EXTRA skill bar
        // (AssignableSkillBar, bottom-middle HUD). The 4 Q/W/E/R slots use the fixed
        // array above; an assigned hot-swap skill is keyed by its abilityId here, so a
        // 5th+ talent skill is castable in battle without growing the slot enum. Ticks
        // down in Update; cleared on expiry. _extraKeys is a reusable scratch list so the
        // tick never allocates and never mutates the dictionary while enumerating it.
        private readonly Dictionary<string, float> _extraCooldown =
            new Dictionary<string, float>(System.StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _extraKeys = new List<string>();

        // Reusable overlap buffer — avoids per-cast GC (Physics.OverlapSphereNonAlloc).
        private readonly Collider[] _overlap = new Collider[64];

        // ── F8 "movement interrupts casting" — interruptible cast WIND-UP ──────────
        // Casts were INSTANT (TryCast committed in one frame), so there was no window to
        // interrupt. def.CastSeconds > 0 now opens a wind-up: mana/cooldown are charged up
        // front (so spamming can't double-charge), the effect commits only AFTER the wind-up,
        // and feeding move input during it CANCELS + refunds. _casting guards against a second
        // cast starting mid-wind-up (we IGNORE the new cast — safer than cancel-and-restart).
        private bool _casting;
        private Coroutine _castRoutine;
        // A tiny post-cancel lockout so a self-interrupt doesn't instantly re-fire on the same
        // key-hold frame (anti-flicker). Time.time until which TryCast/TryCastExtra are refused.
        private float _castLockoutUntil;
        private const float CastCancelLockout = 0.2f;

        // ── Animation ─────────────────────────────────────────────────────────
        // The hero rig's Animator (Hero.controller, built by the AnimatorSetup
        // editor script; assigned to the hero prefab by the integrator — see
        // docs/port-notes/animation-setup.md) plays the Q/W/E/R cast animation.
        // HeroAbilities fires the Cast trigger whenever an ability resolves.
        // Null-guarded so the cast logic runs without a rig.
        private Animator _animator;

        /// <summary>Animator <c>Cast</c> trigger hash — matches AnimatorSetup.cs.</summary>
        private static readonly int AnimCast = Animator.StringToHash("Cast");

        // WO-163: cached "Cast" param presence for the currently-resolved Animator.
        // HeroBodySwapper assigns a fresh runtimeAnimatorController at runtime, so
        // re-scan whenever the resolved Animator changes (same pattern as
        // HeroLocomotion.RefreshParamCache). Driving an absent param logs an error.
        private bool _hasCastParam;
        private Animator _paramCheckedAnimator;

        /// <summary>Current mana, 0..<see cref="MaxMana"/>.</summary>
        public float Mana => _mana;

        /// <summary>Max mana pool.</summary>
        public float MaxMana => _maxMana;

        /// <summary>Hero class id (drives the abilities.json lookup).</summary>
        public string HeroClass => _heroClass;

        /// <summary>Mana-regen multiplier — bumped by the Aether Sprite's Mana Tide perk.</summary>
        public float ManaRegenMultiplier
        {
            get => _manaRegenMultiplier;
            set => _manaRegenMultiplier = Mathf.Max(0.1f, value);
        }

        /// <summary>Seconds of cooldown remaining on a slot — 0 means ready.</summary>
        public float CooldownRemaining(AbilitySlot slot) => _cooldownRemaining[(int)slot];

        // Loadout indirection (Knight skill-tree spine): a HeroLoadout on this rig may
        // equip a skill-tree ability into W/E/R. Resolve(slot) returns that equipped
        // def (looked up by id) when one is set, else the class's stock def. Cached +
        // re-resolved only while null so it survives the body swap. With NO loadout
        // component / nothing equipped, Resolve == AbilityCatalog.Find — identity.
        private HeroLoadout _loadout;

        /// <summary>
        /// The AbilityDef for <paramref name="slot"/>: the loadout-equipped ability
        /// (by id) when one is set on W/E/R, otherwise the class's stock Q/W/E/R def.
        /// Q is the locked basic attack and always resolves to the class def. Behaviour
        /// is identical to <c>AbilityCatalog.Find(_heroClass, slot)</c> when no loadout
        /// is present — the chooser is purely additive.
        /// </summary>
        private AbilityDef Resolve(AbilitySlot slot)
        {
            if (_loadout == null) _loadout = GetComponent<HeroLoadout>();
            if (_loadout != null && slot != AbilitySlot.Q)
            {
                string id = _loadout.AbilityIdForSlot(slot);
                if (!string.IsNullOrEmpty(id))
                {
                    var equipped = AbilityCatalog.FindById(id);
                    if (equipped != null) return equipped;
                }
            }
            return AbilityCatalog.Find(_heroClass, slot);
        }

        /// <summary>
        /// PUBLIC facade over <see cref="Resolve"/> — returns EXACTLY the AbilityDef this hero
        /// will CAST for <paramref name="slot"/>: the loadout-equipped ability (by id) on W/E/R
        /// when one is set, else this component's class stock def, resolved through THIS hero's
        /// own <see cref="HeroLoadout"/> + <c>_heroClass</c> (the same lookup <see cref="TryCast"/>
        /// uses). The HUD ability medallions resolve their ICON through this so the icon shown is
        /// always the ability actually cast — one source of truth. Previously the icon re-derived
        /// class + loadout through a DIFFERENT lookup (HeroLoadoutAccess.Current + a hardcoded
        /// class) that could disagree with the cast (E showed a taunt icon while E cast a heal).
        /// Null only when the class has no def for the slot at all.
        /// </summary>
        public AbilityDef ResolvedDef(AbilitySlot slot) => Resolve(slot);

        /// <summary>0..1 cooldown fill for the HUD — 1 = ready, 0 = just cast.</summary>
        public float CooldownFraction(AbilitySlot slot)
        {
            var def = Resolve(slot);
            if (def == null || def.Cooldown <= 0f) return 1f;
            return 1f - Mathf.Clamp01(_cooldownRemaining[(int)slot] / def.Cooldown);
        }

        /// <summary>True when the slot is off cooldown AND there is enough mana to cast it.</summary>
        public bool CanCast(AbilitySlot slot)
        {
            var def = Resolve(slot);
            if (def == null) return false;
            return _cooldownRemaining[(int)slot] <= 0f && _mana >= def.ManaCost;
        }

        /// <summary>Wires the Heart reference (the integrator calls this from VillageController).</summary>
        public void SetHeart(HeartController heart) => _heart = heart;

        /// <summary>
        /// Sets the hero class id that drives the abilities.json lookup (WO-36).
        /// The field defaults to "mage" and was never reassigned, so a Knight or
        /// Ranger cast the Mage loadout. HeroBodySwapper calls this after swapping
        /// in the real class body so each hero casts its own kit. AbilityCatalog
        /// normalises the id, but lower-case it here to be safe.
        /// </summary>
        /// <summary>
        /// WO-581: explicit animator injection — <see cref="HeroBodySwapper"/> calls this DIRECTLY
        /// after a body swap (no reflection) so the Cast trigger drives the LIVE swapped rig.
        /// Re-scans the controller for the "Cast" param (a swap rebinds the animator). Replaces
        /// the brittle name-based reflection write that silently wrote 0 components.
        /// </summary>
        public void SetAnimator(Animator anim)
        {
            if (anim == null) return;
            _animator = anim;
            _paramCheckedAnimator = anim;
            _hasCastParam = false;
            if (anim.runtimeAnimatorController != null)
            {
                foreach (var p in anim.parameters)
                    if (p.nameHash == AnimCast) { _hasCastParam = true; break; }
            }
        }

        public void SetHeroClass(string slug)
        {
            if (string.IsNullOrWhiteSpace(slug)) return;
            _heroClass = slug.Trim().ToLowerInvariant();
            Debug.Log($"[HeroAbilities] Hero class set to '{_heroClass}' (abilities will resolve from this loadout).");
        }

        private void Awake()
        {
            _mana = _maxMana;
            // The Animator sits on the KayKit hero mesh child of the hero rig.
            _animator = GetComponentInChildren<Animator>();

            // WO-36 (Bug 1 backstop): self-resolve hero class from GameState so the
            // correct Q/W/E/R loadout is active even in test scenes where
            // HeroBodySwapper is absent. HeroBodySwapper.Start() calls SetHeroClass()
            // directly in the normal village flow (runs after Awake), so this only
            // matters for stand-alone test scenes and editor play without a full rig.
            var svc = GameStateService.Instance;
            if (svc != null)
            {
                var opt = svc.State?.HeroClass.ToNullable();
                if (opt.HasValue)
                {
                    _heroClass = opt.Value switch
                    {
                        // Fully-qualified: unqualified 'HeroClass' shadows to a string in this scope.
                        DeNelle.Core.State.HeroClass.Knight => "knight",
                        DeNelle.Core.State.HeroClass.Ranger => "ranger",
                        DeNelle.Core.State.HeroClass.Mage   => "mage",
                        // WO-226: the Cleric is a caster — reuse the Mage loadout.
                        DeNelle.Core.State.HeroClass.Cleric => "mage",
                        _                                   => AbilityCatalog.DefaultClass,
                    };
                    Debug.Log($"[HeroAbilities] Awake backstop: resolved class '{ _heroClass}' from GameState.");
                }
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // ── mana regen + cooldown ticks (heroAbilities.ts lines 122-126) ──
            // WO-676 G3 (wire-or-hide): Aether Bond (shared.n5, manaRegen) — fold the talent
            // bonus into the per-second regen. Same registry read HeroHealth.RegenTick uses
            // (HeroTalentModifiers, clamped accessor), resolved with THIS component's class
            // (_heroClass is the class source HeroHealth.HeroClassOrDefault itself reads).
            // Identity (×1) until the node is learned. Multiplicative with the Aether Sprite's
            // _manaRegenMultiplier, mirroring how gear and talent HP bonuses stack.
            float talentManaRegen = DeNelle.Village.Talents.HeroTalentModifiers.ManaRegenBonus(_heroClass);
            if (talentManaRegen > 0f)
                DeNelle.Core.Diagnostics.FlowTrace.Once("HeroTalents", "manaRegen",
                    $"Aether Bond applied: +{talentManaRegen:P0} mana regen (shared.n5).");
            _mana = Mathf.Min(_maxMana,
                _mana + _manaRegenPerSecond * dt * _manaRegenMultiplier * (1f + talentManaRegen));

            // WO3: Mana Draught / WO-861 Manaweave — drip the over-time mana restore while the
            // window is open. Extracted to TickManaOverTime so the drip is unit-testable with an
            // explicit clock (EditMode never runs Update). Math is byte-identical to the inline
            // block it replaced.
            TickManaOverTime(Time.time, dt);

            // WO-614 hook 2: Oathmend HP-over-time drip. RegenTick is silent (no per-frame heal
            // VFX) and no-ops when full/dead, so the drip is safe to run every frame.
            if (Time.time < _hpOverTimeUntil && _hpOverTimeRate > 0f)
            {
                if (_heroHealth == null) _heroHealth = TryGetComponent<HeroHealth>(out var hh) ? hh : HeroHealth.Instance;
                _heroHealth?.RegenTick(_hpOverTimeRate * dt);
            }

            for (int i = 0; i < _cooldownRemaining.Length; i++)
                _cooldownRemaining[i] = Mathf.Max(0f, _cooldownRemaining[i] - dt);

            // WO-574: tick the assignable EXTRA-skill cooldowns (keyed by abilityId).
            if (_extraCooldown.Count > 0)
            {
                _extraKeys.Clear();
                _extraKeys.AddRange(_extraCooldown.Keys);
                for (int i = 0; i < _extraKeys.Count; i++)
                {
                    string k = _extraKeys[i];
                    float v = _extraCooldown[k] - dt;
                    if (v <= 0f) _extraCooldown.Remove(k);
                    else _extraCooldown[k] = v;
                }
            }
        }

        /// <summary>WO-574: seconds of cooldown left on an assigned EXTRA-bar skill (0 = ready).</summary>
        public float ExtraCooldownRemaining(string abilityId)
        {
            return !string.IsNullOrEmpty(abilityId) && _extraCooldown.TryGetValue(abilityId, out var v) ? v : 0f;
        }

        // ── WO-861: the mana-over-time drip, factored out of Update ─────────────────────
        // The drip is the SINGLE mana-over-time mechanism in the game (WO3 Mana Draught and
        // WO-861 Manaweave both feed it through RestoreManaOverTime). Splitting the per-frame
        // step out of Update() lets EditMode drive it with an explicit clock — Update never
        // runs in an EditMode test, so an in-Update drip is untestable by construction.

        /// <summary>Mana-per-second the over-time drip is currently delivering (0 = no drip).</summary>
        public float ManaOverTimeRate => _manaOverTimeRate;

        /// <summary><see cref="Time.time"/> at which the over-time drip window closes.</summary>
        public float ManaOverTimeUntil => _manaOverTimeUntil;

        /// <summary>
        /// One frame of the mana-over-time drip at <paramref name="now"/> with step
        /// <paramref name="dt"/>. Called every frame by <see cref="Update"/> with
        /// (Time.time, Time.deltaTime); called directly by unit tests with a deterministic clock.
        /// Closes the window (and clears the HUD marker) the moment mana reaches the cap.
        /// </summary>
        public void TickManaOverTime(float now, float dt)
        {
            if (now >= _manaOverTimeUntil || _manaOverTimeRate <= 0f || dt <= 0f) return;
            _mana = StepManaOverTime(_mana, _maxMana, _manaOverTimeRate, dt);
            if (_mana >= _maxMana)
            {
                _manaOverTimeRate = 0f;
                _manaOverTimeUntil = 0f;
                HeroCombatStatus.GetOrAdd(gameObject)?.ClearNamed("mana-draught");
            }
        }

        /// <summary>
        /// PURE per-frame step of the mana drip: mana + rate*dt, HARD-CAPPED at
        /// <paramref name="maxMana"/>. The cap lives here so every caller (Update, tests,
        /// any future drip source) is provably unable to overfill the pool.
        /// </summary>
        public static float StepManaOverTime(float mana, float maxMana, float ratePerSecond, float dt)
            => Mathf.Min(maxMana, mana + ratePerSecond * dt);

        /// <summary>
        /// WO3 (Mana Draught): restore mana GRADUALLY — <paramref name="totalPct"/> percent
        /// of max mana spread evenly over <paramref name="seconds"/> (owner spec: "+3%/s till
        /// 30% recovered" = totalPct 30, seconds 10). Data-driven from consumables.json. The
        /// drip runs in Update(). Re-entrant: a second draught REFRESHES the window and carries
        /// any UNDELIVERED mana from the prior draught into the new rate, so it never
        /// double-counts already-restored mana.
        /// </summary>
        public void RestoreManaOverTime(float totalPct, float seconds)
        {
            Guard.Try("HeroAbilities", "RestoreManaOverTime", () =>
            {
                if (totalPct <= 0f || seconds <= 0f)
                {
                    FlowTrace.Warn("HeroAbilities", $"RestoreManaOverTime ignored (pct={totalPct}, secs={seconds}).");
                    return;
                }

                // Carry forward whatever the in-flight draught hasn't yet delivered.
                float carry = 0f;
                if (Time.time < _manaOverTimeUntil && _manaOverTimeRate > 0f)
                    carry = _manaOverTimeRate * (_manaOverTimeUntil - Time.time);

                float target = _maxMana * (totalPct / 100f) + carry;
                _manaOverTimeRate  = target / seconds;
                _manaOverTimeUntil = Time.time + seconds;
                HeroCombatStatus.GetOrAdd(gameObject)?.ApplyNamed("mana-draught", "Mana", seconds, isBuff: true);

                FlowTrace.Step("HeroAbilities",
                    $"Mana Draught: +{totalPct}% ({target:0.0} mana) over {seconds}s -> {_manaOverTimeRate:0.00}/s (mana {_mana:0.0}/{_maxMana:0.0}).");
            });
        }

        /// <summary>
        /// SAFE-ZONE full recovery (owner 2026-06-29): restore mana to FULL instantly. Called when the
        /// hero enters a safe zone (Castle/Town/Base — see <see cref="DeNelle.Village.SafeZoneRecovery"/>),
        /// the only place full passive recovery happens under the survival rule (ff.noautoheal). Clears any
        /// in-flight Mana Draught drip so it can't keep ticking past full. Self-reporting.
        /// </summary>
        public void RestoreManaToFull()
        {
            _mana = _maxMana;
            _manaOverTimeRate  = 0f;   // a full restore ends any in-flight draught drip
            _manaOverTimeUntil = 0f;
            HeroCombatStatus.GetOrAdd(gameObject)?.ClearNamed("mana-draught");
            FlowTrace.Step("HeroAbilities", $"SAFE-ZONE mana restore: mana -> FULL ({_maxMana:0.0}).");
        }

        /// <summary>
        /// Attempts to cast the Q/W/E/R ability in <paramref name="slot"/>.
        /// Returns true when the cast fired (cooldown + mana satisfied), false
        /// when it was on cooldown / out of mana / unknown. Mirrors the
        /// cooldown+mana gate at the top of castAbility.ts.
        /// </summary>
        public bool TryCast(AbilitySlot slot)
        {
            var def = Resolve(slot);
            if (def == null)
            {
                Debug.LogWarning($"[HeroAbilities] No ability for {_heroClass}/{slot} in abilities.json.");
                return false;
            }

            // F8 wind-up: ignore a new cast while one is winding up (safer than cancel-and-restart)
            // and during the brief post-cancel anti-flicker lockout.
            if (_casting || Time.time < _castLockoutUntil)
                return false;

            // castAbility.ts: `if (combat.cd[kind] > 0 || combat.mana < def.mana) return null;`
            if (_cooldownRemaining[(int)slot] > 0f || _mana < def.ManaCost)
                return false;

            // WO-36 (talent -> stat): unlocked skill-tree talents shave cooldowns
            // class-wide via CdReduction. CooldownMultiplier returns 1f when the
            // hero has no cooldown talents unlocked, preserving the JSON baseline.
            float scaledCooldown = def.Cooldown * HeroTalentModifiers.CooldownMultiplier(_heroClass);
            _cooldownRemaining[(int)slot] = scaledCooldown;
            _mana -= def.ManaCost;

            // WO-97 Bug 3: drive the per-slot cooldown fill overlay on the ability
            // button. AbilityCooldownUI lives on the button GameObject; the ability
            // HeroAbilities lives on the hero rig, so we broadcast to any registered
            // listener via the HudBridge rather than a direct GetComponent. As a
            // belt-and-braces fallback also check this GO (in case someone collocates).
            GetComponent<AbilityCooldownUI>()?.StartCooldown(scaledCooldown);

            // PER-SPELL ANIMATION: pass the slot as the cast variant (q/w/e/r → 1..4).
            // F8 "movement interrupts casting": spells with an authored wind-up (CastSeconds > 0)
            // start an interruptible routine that commits CastResolved only after the wind-up;
            // basics/melee (CastSeconds <= 0) stay INSTANT + uninterruptible (backward-compatible).
            if (def.CastSeconds > 0f)
            {
                _casting = true;
                FlowTrace.Step("HeroAbility",
                    $"cast-start slot={slot} '{def.Name}' windup={def.CastSeconds:0.00}s (interruptible).");
                _castRoutine = StartCoroutine(CastRoutine(def, (int)slot + 1, (int)slot, null, scaledCooldown));
            }
            else
            {
                CastResolved(def, (int)slot + 1);
            }
            return true;
        }

        /// <summary>
        /// WO-574: cast an assigned EXTRA-bar skill (the player-assignable hot-swap bar,
        /// <see cref="AssignableSkillBar"/>) by its <paramref name="abilityId"/>. The 4-slot
        /// Q/W/E/R engine could never fire a 5th+ talent skill — that left every hot-swap
        /// assignment cosmetic ("nothing happens"). This resolves the def from the catalog,
        /// gates on a per-id cooldown + mana, then runs the SAME cast core (anim + facing +
        /// effect) the Q/W/E/R path uses, so an assigned skill is genuinely usable in battle.
        /// Returns true when the cast fired. Additive — Q/W/E/R behaviour is unchanged.
        /// </summary>
        public bool TryCastExtra(string abilityId)
        {
            if (string.IsNullOrEmpty(abilityId)) return false;
            var def = AbilityCatalog.FindById(abilityId);
            if (def == null)
            {
                Debug.LogWarning($"[HeroAbilities] Extra skill '{abilityId}' not found in abilities.json.");
                return false;
            }
            // F8 wind-up: ignore a new cast while one is winding up + during the anti-flicker lockout.
            if (_casting || Time.time < _castLockoutUntil) return false;
            if (ExtraCooldownRemaining(abilityId) > 0f || _mana < def.ManaCost) return false;

            float scaledCooldown = def.Cooldown * HeroTalentModifiers.CooldownMultiplier(_heroClass);
            _extraCooldown[abilityId] = scaledCooldown;
            _mana -= def.ManaCost;

            // F8 "movement interrupts casting": route spells with a wind-up (CastSeconds > 0) through the
            // interruptible routine; instant skills (CastSeconds <= 0) commit immediately as before.
            // PER-ABILITY CAST ANIMATION (fix): the extra bar previously always passed variant 0 (the
            // generic cast), so an assigned hot-swap skill never played ITS animation. Resolve the extra
            // skill's own cast variant from its def (explicit castAnim > effect shape); the extra bar has
            // no Q/W/E/R slot, so generic (0) is the terminal fallback.
            int extraVariant = ResolveAnimVariant(def, 0);
            if (def.CastSeconds > 0f)
            {
                _casting = true;
                DeNelle.Core.Diagnostics.FlowTrace.Step("HeroAbility",
                    $"cast-start extra '{abilityId}' windup={def.CastSeconds:0.00}s variant={extraVariant} (interruptible).");
                _castRoutine = StartCoroutine(CastRoutine(def, extraVariant, -1, abilityId, scaledCooldown));
            }
            else
            {
                CastResolved(def, extraVariant);
                DeNelle.Core.Diagnostics.FlowTrace.Step("Hero", "extra-skill cast " + abilityId + " FIRED (variant " + extraVariant + ")");
            }
            return true;
        }

        /// <summary>
        /// F8 "movement interrupts casting" — the interruptible cast WIND-UP. The caller has already
        /// gated + charged mana/cooldown; this waits <c>def.CastSeconds</c> while polling
        /// <see cref="HeroLocomotion.WantsToMove"/> each frame. Moving CANCELS the cast (no effect) and
        /// REFUNDS near-full: mana back + the just-charged cooldown reset (slot array or extra-bar id),
        /// plus a tiny anti-flicker lockout so a self-interrupt feels responsive, not punishing. Only when
        /// the wind-up completes uninterrupted do we invoke the unchanged <see cref="CastResolved"/> so
        /// the VFX/anim/effect ordering is byte-identical to the instant path.
        /// </summary>
        /// <param name="castVariant">Per-variant cast clip (1..4 = Q/W/E/R; 0 = generic/extra bar).</param>
        /// <param name="slot">Q/W/E/R slot index whose cooldown to refund on cancel; -1 for extra-bar casts.</param>
        /// <param name="extraId">Extra-bar ability id whose cooldown to remove on cancel; null for slot casts.</param>
        /// <param name="chargedCooldown">The cooldown value charged up front (unused on commit; documents intent).</param>
        private System.Collections.IEnumerator CastRoutine(AbilityDef def, int castVariant, int slot, string extraId, float chargedCooldown)
        {
            float elapsed = 0f;
            while (elapsed < def.CastSeconds)
            {
                if (HeroLocomotion.WantsToMove)
                {
                    // CANCEL — refund mana + reset the just-charged cooldown, apply the anti-flicker lockout.
                    _mana = Mathf.Min(_maxMana, _mana + def.ManaCost);
                    if (extraId != null) _extraCooldown.Remove(extraId);
                    else if (slot >= 0 && slot < _cooldownRemaining.Length) _cooldownRemaining[slot] = 0f;
                    _casting = false;
                    _castRoutine = null;
                    _castLockoutUntil = Time.time + CastCancelLockout;
                    FlowTrace.Step("HeroAbility",
                        $"cast-interrupted '{def.Name}' at {elapsed:0.00}/{def.CastSeconds:0.00}s (moved) — mana+cd refunded.");
                    yield break;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }

            // Wind-up completed — commit the effect through the UNCHANGED cast core.
            _casting = false;
            _castRoutine = null;
            FlowTrace.Step("HeroAbility", $"cast-committed '{def.Name}' after {def.CastSeconds:0.00}s wind-up.");
            CastResolved(def, castVariant);
        }

        /// <summary>
        /// F8 external cancel hook (stun / knockback parity) — abort any in-flight cast wind-up WITHOUT
        /// refunding (an external interrupt is a real loss, unlike the player's own move-cancel). No-op
        /// when nothing is casting. Optional; the move-interrupt path is self-contained in CastRoutine.
        /// </summary>
        public void CancelCast()
        {
            if (!_casting) return;
            if (_castRoutine != null) StopCoroutine(_castRoutine);
            _castRoutine = null;
            _casting = false;
            _castLockoutUntil = Time.time + CastCancelLockout;
            FlowTrace.Step("HeroAbility", "cast-cancelled (external interrupt) — no refund.");
        }

        /// <summary>
        /// WO-574: the shared cast core (anim trigger + timing bonus + face-target + effect)
        /// invoked once the caller has passed its cooldown/mana gate and charged its own
        /// cooldown store. <paramref name="castVariant"/> selects the per-variant cast clip
        /// (1..4 = Q/W/E/R; 0 = generic). Extracted from <see cref="TryCast"/> so the
        /// EXTRA-bar path (<see cref="TryCastExtra"/>) reuses the exact same resolution.
        /// </summary>
        private void CastResolved(AbilityDef def, int castVariant)
        {
            // Play the hero's cast animation in sync with the ability resolving.
            // Self-heal the reference: Awake() caches it before HeroBodySwapper
            // swaps the real FBX body in, so the Awake cache is stale/null.
            // HeroBodySwapper re-caches this via reflection after the swap, but
            // re-resolve here too as a backstop (only while null).
            if (_animator == null)
            {
                var bodyT = transform.Find("HeroBody");
                if (bodyT != null) _animator = bodyT.GetComponentInChildren<Animator>();
            }
            // WO-163: (re)scan the resolved controller for the "Cast" param so we
            // never drive an absent param (a controller swap rebinds the animator).
            if (_animator != null && _animator != _paramCheckedAnimator)
            {
                _paramCheckedAnimator = _animator;
                _hasCastParam = false;
                if (_animator.runtimeAnimatorController != null)
                {
                    foreach (var p in _animator.parameters)
                        if (p.nameHash == AnimCast) { _hasCastParam = true; break; }
                }
            }
            if (_animator != null && _hasCastParam) _animator.SetTrigger(AnimCast);

            // Core fix: also drive the guarded ActorAnimator (IActorAnimator) so the
            // DTT Patricia hero (and village) get the Cast/Attack states from the
            // HeroAnimatorFactory controllers (upper-body layer + base). The legacy
            // direct _animator is kept for any direct listeners; ActorAnimator
            // re-resolves on body swap and guards missing params.
            var actor = GetComponent<ActorAnimator>();
            if (actor == null) actor = GetComponentInChildren<ActorAnimator>(true);
            // Class-specific attack/cast per factory controllers: Knight uses melee Attack
            // clip/stance, Ranger (aim), Mage/Cleric (cast). Call both; per-class controller
            // maps the right one (or no-op if param missing).
            // PER-SPELL ANIMATION: pass the slot as the cast variant (q/w/e/r → 1..4) so each
            // ability can play its own cast clip via the HeroAnimatorFactory's per-variant Cast
            // states. Variant 0 stays the generic cast for any caller using the no-arg overload.
            // A controller without CastVariant / per-variant states falls back to the default
            // Cast state, so this is fully backward-compatible (castVariant is the caller's arg).
            // F8-48: a HEAL must never drive the melee attack trigger — on the Knight the
            // attack trigger is a sword swing, so a committed Mend read as "does an attack".
            // Heals play only the cast side (owner melee/caster rule).
            string fxRaw = (def.Effect ?? string.Empty).Trim().ToLowerInvariant();
            // WO-750: gracebuff (Warden's Grace) is a support cast — like a heal, it must NOT
            // drive the melee attack trigger (F8-48: a support cast can't read as a sword swing).
            // WO-861: shield (Arcane Shell) + manaweave are SELF-CAST support, same rule as
            // gracebuff — they must not drive the melee attack trigger. drainshot is offensive
            // and deliberately stays out of this list (it swings/shoots).
            bool isHealCast = def.EffectEnum == AbilityEffect.Heal || fxRaw == "healovertime" || fxRaw == "gracebuff"
                              || fxRaw == "shield" || fxRaw == "manaweave";
            if (!isHealCast) actor?.PlayAttack(0);
            // PER-ABILITY CAST ANIMATION (fix "swapped/equipped ability plays the wrong cast clip"):
            // the animation was selected by the pressed SLOT (castVariant == slot+1), so an ability
            // equipped into a slot played that slot's STOCK clip while the EFFECT was the equipped
            // ability's. Derive the cast-clip variant from the RESOLVED ability instead — its explicit
            // castAnim key > its effect SHAPE's canonical keyword > the pressed slot as last-resort —
            // so the animation matches what the ability actually DOES, not which button fired it. The
            // slot the caller passed is the fallback, so a stock loadout is unchanged where the effect
            // maps back to its own slot clip. VFX keeps the caller's (slot) keyword — unchanged.
            int animVariant = ResolveAnimVariant(def, castVariant);
            actor?.PlayCast(animVariant);

            Vector3 origin = transform.position;

            // DEF-47: register the cast with the timing-bonus tracker. The returned
            // multiplier (1.00–1.50× depending on chain depth) is stored and applied
            // to outgoing damage inside ResolveEffect. Heals are unaffected.
            _pendingTimingBonus = AttackTimingBonus.NotifyCast(origin);

            // WO-423: for OFFENSIVE abilities, turn to face the foe BEFORE resolving the
            // effect so the cast/projectile reads as aimed at the target instead of the
            // hero's last move direction. Self / non-targeted abilities (Heal) skip this.
            // Null-guarded: no locomotion or no resolvable foe leaves facing untouched.
            FaceCastTarget(def, origin);

            // WO-VFX-003 / owner directive 2026-07-12: the CAST beat — in registry-only mode
            // the variant's motion-castings row is the ONLY VFX source (owner picks); the
            // abilities.json VfxCast default is suppressed. Single choke point for all actives.
            // The keyword is remembered for the PROJECTILE/IMPACT phases of this same cast
            // (phase bundle: vfxKey=start, vfxProjectile=travel, vfxImpact=end).
            _currentCastKeyword = castVariant >= 0 && castVariant < CastVariantKeyword.Length
                ? CastVariantKeyword[castVariant] : null;
            PlayCastVfxKey(def, origin, castVariant);

            ResolveEffect(def, origin);
        }

        // =====================================================================
        //  Effect resolution — line-equivalent to castAbility.ts.
        // =====================================================================

        // Cached level-progression on the hero (added at runtime by
        // ProgressionManager). Its DamageMultiplier scales ability damage on top
        // of the talent multiplier; resolved lazily so it survives the body swap.
        private HeroProgression _progression;

        // DEF (combat feel): lazily-attached ranged projectile launcher (arrow / spell orb).
        private RangedAttackVFX _rangedVfx;

        // Gear v1: lazily-attached equipped-gear loadout — its WeaponMult joins the damage chain.
        private GearLoadout _gear;

        // WO-423: cached locomotion (the sole rotation writer) so offensive casts can yaw-slew
        // the hero to face the foe before the effect resolves. Lives on the hero root alongside
        // this component; resolved lazily and only while null (survives the body swap).
        private HeroLocomotion _loco;

        // DEF-47: timing bonus captured by TryCast() from AttackTimingBonus,
        // consumed by the next ResolveEffect() damage call, then reset to 1.
        private float _pendingTimingBonus = 1f;

        // ── Defend-the-Tower aim overrides (null in village → behaviour unchanged) ──
        /// <summary>When set, offensive abilities resolve from this world point (the
        /// turret player's crosshair / aim target) instead of the hero's feet — so a
        /// stationary hero's spells reach the distant enemies. PatriciaLightController
        /// sets it per cast; village mode leaves it null.</summary>
        public Vector3? AimPointOverride;
        /// <summary>When set by HeroTargetIndicator, single-target offensive abilities hit
        /// THIS exact reticle-locked foe instead of re-searching via an OverlapSphere — so
        /// the hero damages precisely what the ring shows (the registry target, the same one
        /// companions hit), even if that enemy's collider isn't found by a physics sweep.
        /// Null → fall back to NearestHostile.</summary>
        public IDamageable LockedTarget;
        /// <summary>When set, Heal effects route here (repair the tower) instead of
        /// healing the caster. Returns true when it handled the heal. Null = heal hero.</summary>
        public System.Func<float, bool> HealHandler;

        /// <summary>
        /// WO-423: turn the hero to face the OFFENSIVE ability's foe/impact point before the
        /// effect resolves, so a stationary caster's spell/projectile reads as aimed at the
        /// target instead of the last move direction. Self / non-targeted (Heal) abilities are
        /// skipped. Resolves the same point the effect will use: the explicit aim override
        /// (DTT crosshair), else the reticle-locked target in reach, else the nearest hostile,
        /// else the live boss. Null-guarded — no locomotion or no resolvable foe = no facing.
        /// </summary>
        private void FaceCastTarget(AbilityDef def, Vector3 origin)
        {
            if (def.EffectEnum == AbilityEffect.Heal) return;   // self/non-targeted — don't turn
            // WO-750: Warden's Grace (gracebuff) is self-cast support — don't yaw toward a foe.
            // WO-861: shield + manaweave are self-cast too (they parse to EffectEnum.Strike by
            // default, so without this they would have yawed the hero at a random foe).
            switch ((def.Effect ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "gracebuff":
                case "shield":
                case "manaweave":
                    return;
            }

            if (_loco == null) _loco = GetComponent<HeroLocomotion>();
            if (_loco == null) return;

            Vector3 atk = AimPointOverride ?? origin;

            // Find the world point the effect targets (mirrors ResolveEffect's resolution).
            Vector3? facePoint = AimPointOverride;   // DTT: face the crosshair directly
            if (facePoint == null)
            {
                // Audit P2 (hero-combat): blast abilities resolve their CENTRE through
                // ResolveBlastCentre/CastReach in ResolveEffect — face the SAME point so the
                // hero turns toward where the blast actually lands, not the raw def.Range gate.
                switch (def.EffectEnum)
                {
                    case AbilityEffect.Aoe:
                    case AbilityEffect.Cleave:
                    case AbilityEffect.Meteor:
                        facePoint = ResolveBlastCentre(atk, origin);
                        break;
                    default:   // Strike / Snare — single-target reach gate (unchanged)
                    {
                        float maxR = def.Range + _enemyHitRadius;
                        // KNIGHT-SNIPE FIX (RCA 2026-06-13): reach gate is hero-relative (origin),
                        // not `atk` (the auto-target's own pos) — mirrors the cast-path fix so the
                        // hero FACES the same in-reach foe it will actually hit.
                        var foe = InReach(LockedTarget, origin, maxR) ? LockedTarget : NearestHostile(origin, maxR);
                        if (foe == null) foe = LiveBoss();
                        if (foe != null) facePoint = foe.WorldPosition;
                        break;
                    }
                }
            }

            if (facePoint.HasValue) _loco.FaceToward(facePoint.Value);
        }

        private void ResolveEffect(AbilityDef def, Vector3 origin)
        {
            // WO-494: Knight arena kit adds three NEW effect SHAPES — dash / knockback /
            // taunt — that the AbilityEffect enum (AbilityCatalog) doesn't carry yet, so
            // they parse to EffectEnum.Strike by default. Branch on the raw effect string
            // FIRST and resolve them here, reusing the existing Strike/Cleave damage + the
            // StatusEffect CC primitives. Unknown strings fall through to the enum switch
            // below, so this is fully additive / flag-safe (no behaviour change for mage/
            // ranger or any ability whose effect is one of the six canonical shapes).
            string rawEffect = (def.Effect ?? string.Empty).Trim().ToLowerInvariant();
            // F8-48: the Heal branch was trace-silent — every commit now names its id,
            // effect string, enum branch and damage so a mis-dispatch is one grep away.
            FlowTrace.Step("HeroAbility",
                $"resolve '{def.Id}' effect='{rawEffect}' enum={def.EffectEnum} dmg={def.Damage}");
            switch (rawEffect)
            {
                case "dash":         ResolveDash(def, origin);         return;
                case "knockback":    ResolveKnockback(def, origin);    return;
                case "taunt":        ResolveTaunt(def, origin);        return;
                case "blink":        ResolveBlink(def, origin);        return;
                case "dot":          ResolveDot(def, origin);          return;   // WO-614 hook 1
                case "healovertime": ResolveHealOverTime(def, origin); return;   // WO-614 hook 2
                case "invuln":       ResolveInvuln(def, origin);       return;   // WO-614 hook 3
                case "gracebuff":    ResolveWardensGrace(def, origin); return;   // WO-750 Warden's Grace
                // ── WO-861 (Sylas + Thrain) — the ONLY new combat code in that program. Each of
                //    the three routes through an EXISTING mechanism, never a parallel one:
                //      shield    -> the Warden's Grace timed mitigation window (ApplyDamageShield)
                //      manaweave -> the WO3 mana-over-time drip (RestoreManaOverTime)
                //      drainshot -> the Strike branch (ResolveStrikeLike) + heal == damage DEALT
                case "shield":       ResolveShield(def, origin);       return;   // WO-861 A4
                case "manaweave":    ResolveManaweave(def, origin);    return;   // WO-861 A4
                case "drainshot":    ResolveDrainshot(def, origin);    return;   // WO-861 A4 (PINNED)
            }

            DamageElement element = ElementOf(def);

            // WO-36 (talent -> stat): scale outgoing enemy damage by the hero's
            // unlocked DamageBonus talents (class-wide). DamageMultiplier returns
            // 1f when nothing is unlocked, so the abilities.json baseline holds
            // until the player learns a damage node. NOTE: the Heal case below
            // deliberately uses raw def.Damage (heal amount), not this scalar.
            // The level-progression multiplier stacks on top (1f until level 2+).
            if (_progression == null) _progression = GetComponent<HeroProgression>();
            float levelMult = _progression != null ? _progression.DamageMultiplier : 1f;
            // DEF-47: apply the chain timing bonus captured in TryCast.
            // _pendingTimingBonus is 1.00× when no chain is active, up to 1.50×
            // at chain 4+. Reset to 1f so a missed follow-up can't carry over.
            float dmg = def.Damage * HeroTalentModifiers.DamageMultiplier(_heroClass) * levelMult * _pendingTimingBonus * WeaponMult();
            _pendingTimingBonus = 1f;

            // DTT: offensive abilities resolve from the player's aim point (crosshair
            // target) so a stationary turret hero's spells reach the distant enemies.
            // Null override (village) keeps the original hero-centred behaviour.
            Vector3 atk = AimPointOverride ?? origin;

            switch (def.EffectEnum)
            {
                case AbilityEffect.Heal:
                {
                    // WO-220: the Heal branch never routes through SpawnVfx (where every
                    // other ability fires its cast SFX), so the heal cast was silent.
                    // Play the class-flavoured cast sting here directly so every ability
                    // beat — offensive AND heal — has a sound. The bridge null-guards
                    // CoreServices.Audio internally.
                    AbilityAudioBridge.PlayForClassAndKind(_heroClass, def.EffectEnum);

                    // DTT routes the heal to repair the TOWER (HealHandler). Otherwise
                    // it heals the CASTER — executive call 2026-05-28: "heal hero is
                    // correct — cannot heal a tree." def.Damage carries the amount.
                    // v2 talents: Mending Oath boosts the heal amount (modifyAbility stat=heal).
                    float healAmount = def.Damage * HeroTalentModifiers.HealAmountMultiplier(_heroClass);
                    if (HealHandler == null || !HealHandler(healAmount))
                    {
                        var heroHp = GetComponent<HeroHealth>();
                        if (heroHp == null) heroHp = HeroHealth.Instance;
                        if (heroHp != null) heroHp.Heal(healAmount);
                        // FULL-PREFAB heal read (owner 2026-07-24): Heal_Cast burst + a short
                        // Heal_Aura loop on the hero, both through the ONE VFXManager pool (PlayKey).
                        // Replaces the placeholder enum burst. Missing key throttled-no-ops (no throw).
                        VFXManager.PlayKey("Heal_Cast", origin + Vector3.up * 1.2f, Quaternion.identity, transform);
                        var healAura = VFXManager.PlayKey("Heal_Aura", transform.position + Vector3.up * 0.1f,
                            Quaternion.identity, transform);
                        if (healAura != null) StartCoroutine(StopHandleAfter(healAura, 1.5f));
                    }
                    // WO-VFX-003: a brief Hovl heal aura on the hero (reads by shape+motion, not hue —
                    // owner is colorblind). Instant heals get a short 1.5s glow.
                    PlayResidualLoop(def, transform, 1.5f, origin + Vector3.up * 1.2f);
                    break;
                }

                case AbilityEffect.Strike:
                case AbilityEffect.Snare:
                    // WO-861: the body moved VERBATIM into ResolveStrikeLike so `drainshot` can run
                    // the SAME strike (no copy of the targeting/projectile/damage path) and receive
                    // the damage ACTUALLY dealt through the onDealt callback. onDealt == null here,
                    // so the stock Strike/Snare behaviour is unchanged.
                    ResolveStrikeLike(def, origin, atk, dmg, element, null);
                    break;

                case AbilityEffect.Aoe:
                case AbilityEffect.Cleave:
                {
                    // WO-398 follow-up: cap the blast CENTRE to the caster's cast reach so a
                    // melee class (knight Bulwark Slam / Lantern Charge) can't centre its slam
                    // on the 45m auto-reticle target — it now lands on a nearby foe / itself.
                    // Ranged classes have a 45m reach so this is a no-op for them (aim in range).
                    Vector3 centre = ResolveBlastCentre(atk, origin);
                    Blast(centre, def.Range, dmg, element, def.Freeze);
                    SpawnVfx(centre, def, def.Range);
                    // WO-VFX-003: Hovl impact/slam at the blast centre (Cleave/Aoe land instantly).
                    PlayImpactVfxKey(def, centre);
                    break;
                }

                case AbilityEffect.Meteor:
                {
                    // blast centred on the nearest enemy cluster to the aim point — WO-398
                    // follow-up: bounded by the caster's cast reach (no-op at 45m for ranged
                    // casters like the Mage; caps any melee meteor to its short reach).
                    var foe = NearestHostile(atk, CastReach());
                    // WO-125 Bug 1: Meteor's 1000u sweep already encloses the orbiting
                    // dragon (it's a layer-8 IDamageable with a collider), so this is a
                    // belt-and-braces fallback for the rare case the sweep misses it.
                    if (foe == null && AimPointOverride == null)
                        foe = LiveBoss();
                    // WO-398 follow-up: when no foe is in reach, fall back to the reach-capped
                    // centre (self for melee, the in-reach aim for ranged) — never the raw 45m aim.
                    Vector3 target = foe != null ? foe.WorldPosition : ResolveBlastCentre(atk, origin);
                    // DEF (combat feel): hurl a visible orb to the target, then EXPLODE on arrival
                    // (blast + impact VFX), so the ultimate reads as a meteor streaking in and
                    // landing rather than an instant area-pop. Same proven projectile pattern as
                    // Strike/Snare; the RangedAttackVFX cast-burst covers the cast beat.
                    var meteorDef = def;   // WO-VFX-003: capture for the impact-key play on landing
                    LaunchProjectile(target, () =>
                    {
                        Blast(target, def.Range, dmg, element, 0f);
                        SpawnVfx(target, def, def.Range);
                        // WO-VFX-003: Hovl impact/explosion where the meteor lands.
                        PlayImpactVfxKey(meteorDef, target);
                    }, def.VfxProjectile, def.UnityColor);
                    break;
                }
            }
        }

        /// <summary>
        /// The single-target STRIKE/SNARE resolution — target acquisition, the visible projectile,
        /// the landing damage + status/venom riders + juice. Extracted VERBATIM from
        /// <see cref="ResolveEffect"/>'s Strike/Snare case (WO-861) so the new <c>drainshot</c>
        /// effect runs the EXACT same strike instead of a second copy of it.
        /// <para>
        /// <paramref name="onDealt"/> (WO-861, PINNED) is invoked on the projectile's ARRIVAL with
        /// the damage ACTUALLY dealt — <c>hpBefore - hpAfter</c> measured across the target's own
        /// <c>TakeDamage</c>, i.e. AFTER the target's resists/mitigation and CLAMPED by its
        /// remaining HP. It is not the nominal <c>def.Damage</c> and not the pre-mitigation
        /// computed <paramref name="dmg"/>. Null (the stock Strike/Snare path) = no callback.
        /// </para>
        /// </summary>
        private void ResolveStrikeLike(AbilityDef def, Vector3 origin, Vector3 atk, float dmg,
                                       DamageElement element, System.Action<float> onDealt)
        {
                    // Prefer the reticle's locked target (registry — exactly what the ring
                    // shows + companions hit); fall back to the OverlapSphere search. This
                    // fixes "ring locks but my hits do 0" — the physics sweep was finding a
                    // different/no enemy than the registry-locked one.
                    // WO-398: the locked target comes from HeroTargetIndicator's 45m acquire
                    // ring, so accepting it unconditionally let melee slots (knight Shield
                    // Bash, def.Range ~3.4) hit-scan at ranged distances. Gate the locked
                    // target by the ability's own reach (def.Range + hit radius); if it's out
                    // of reach, re-resolve to the nearest hostile actually IN range — which
                    // may be null (no target in reach -> no damage). dcd76ba fixed the
                    // COMPANION knight; this closes the player-hero path. Ranged abilities are
                    // unchanged: their def.Range already covers the locked target's distance.
                    float maxR = def.Range + _enemyHitRadius;
                    // KNIGHT-SNIPE FIX (RCA 2026-06-13): gate reach from the HERO (origin),
                    // NOT from `atk`. `atk = AimPointOverride ?? origin`, and HeroTargetIndicator
                    // writes AimPointOverride = the auto-target's OWN world pos every scan (out to
                    // 45m), so InReach(target, atk) was measuring the target's distance from
                    // itself (~0) → always passed → the 3.4m Shield Bash connected at 45m. Reach
                    // must be hero-relative. Ranged classes are unaffected (their large def.Range
                    // still covers the reticle target); AoE/Cleave/Meteor keep using `atk` for
                    // blast PLACEMENT below (that path was already correct).
                    var foe = InReach(LockedTarget, origin, maxR)
                        ? LockedTarget
                        : NearestHostile(origin, maxR);
                    // WO-125 Bug 1: the apex dragon orbits at altitude ~22-34u — far
                    // outside a short-slot sweep (Q ~13u) — so an OverlapSphere from the
                    // hero's feet can never reach it. In village mode (no aim override),
                    // when no ground enemy is in reach, let the single-target offensive
                    // slots punch up at the live boss so the airborne apex is hittable
                    // (not only during a low swoop). Ground targeting is untouched: a
                    // reachable ground enemy is still preferred. Resolved through the
                    // Core IDamageable seam via WaveManager — no concrete/HUD ref added.
                    if (foe == null && AimPointOverride == null)
                        foe = LiveBoss();
                    // §12 outgoing-attack trace (2026-06-30 "0 damage in dungeon"): PROVE the ability's
                    // target resolution — did it find a hostile in reach, and (owner's hypothesis) what
                    // FACTION is it? AsHostile below only accepts CombatFaction.Hostile.
                    if (foe == null)
                        DeNelle.Core.Diagnostics.FlowTrace.Step("HeroAbility",
                            $"{def.EffectEnum} cast: NO hostile target in reach (maxR={maxR:F1}m origin={origin}) — 0 damage this cast.");
                    else
                        DeNelle.Core.Diagnostics.FlowTrace.Step("HeroAbility",
                            $"{def.EffectEnum} cast: target='{(foe as MonoBehaviour)?.name}' faction={foe.Faction} " +
                            $"dist={(foe.WorldPosition - origin).magnitude:F1}m computedDmg={dmg:F1} (launching projectile).");
                    if (foe != null)
                    {
                        // DEF (combat feel): LAUNCH a visible projectile (Ranger arrow / Mage
                        // orb) and land the damage WHEN it arrives — "seeing the arrow/spell go
                        // is fun; click-button-instant-FX is sad." Capture the payload for the
                        // arrival closure; the enemy's TakeDamage fires all the impact juice
                        // (red flash + damage number + hit-stop) at the moment of connection.
                        var hitFoe = foe;
                        float hitDmg = dmg;
                        var hitEl = element;
                        bool snare = def.EffectEnum == AbilityEffect.Snare;
                        var hitDef = def;   // WO-VFX-003: capture for the impact-key play on arrival
                        // WO-676 Venombrand: read the poison rider ONCE at cast (the same
                        // HeroTalentModifiers read seam as Emberbrand's burn proc — see
                        // PlayerAttackController's ForEachOnHitProc) and capture it for the
                        // arrival closure. Identity (false) until the talent is owned; the
                        // node's effect.ability CSV names which abilities carry it
                        // (Thunderbolt + Throwing Spear), so this is a no-op for every other cast.
                        bool venom = HeroTalentModifiers.TryGetAbilityDotRider(
                            _heroClass, def.Id, "poison",
                            out float venomDps, out float venomSecs, out int venomStacks);
                        // WO-861 A4 ARROW RIDER — resolved ONCE at cast, and ONLY when this cast is
                        // the Ranger's arrow-using basic (see IsArrowRiderEligible). Captured into
                        // the arrival closure so the rider lands with the arrow, not at cast time.
                        // (declared up front, not as inline `out var`s — the closure below captures
                        // them, and an out-variable born inside a short-circuited && is not
                        // definitely assigned at the point the lambda is created.)
                        string ammoFx = null;
                        float ammoDps = 0f, ammoSecs = 0f, ammoSlowPct = 0f;
                        bool arrowRider = IsArrowRiderEligible(def)
                            && TryResolveAmmoRider(out ammoFx, out ammoDps, out ammoSecs, out ammoSlowPct);
                        LaunchProjectile(foe.WorldPosition, () =>
                        {
                            if (hitFoe == null || !hitFoe.IsAlive) return;
                            // Ticket #61: hero-dealt -> combo/streak/RAMPAGE eligible.
                            (hitFoe as DeNelle.Core.Combat.IHeroDamageMarkable)?.MarkNextHitFromHero();
                            // §12: PROVE the ability lands as a hero-dealt hit (dealtByHero=True).
                            DeNelle.Core.Diagnostics.FlowTrace.Step("HeroAbility",
                                $"ability hit '{(hitFoe as MonoBehaviour)?.name}' faction={hitFoe.Faction} " +
                                $"dealtByHero=True amount={hitDmg:F1}.");
                            // WO-861 (PINNED): measure the damage ACTUALLY dealt across the target's
                            // own TakeDamage (post-resist, clamped by its remaining HP) so drainshot
                            // can heal that exact number. Identity for every other caller.
                            float dealt = ApplyMeasuredDamage(hitFoe, hitDmg, hitEl);
                            DeNelle.Core.Combat.DamageAttribution.Record(hitFoe, HeroProgression.Id, hitDmg);
                            if (snare) hitFoe.ApplyStatus(StatusEffect.Slow, 2.5f); // castAbility.ts snare
                            // WO-676 Venombrand: venom in the wound — stack-capped poison DoT.
                            if (venom) ApplyPoisonRider(hitFoe, venomDps, venomSecs, venomStacks);
                            // WO-861 A4: the equipped arrow's on-hit rider (Ranger basic only).
                            if (arrowRider) ApplyAmmoRider(hitFoe, ammoFx, ammoDps, ammoSecs, ammoSlowPct);
                            ReportRumble(hitDmg);   // WO-497: rumble on the projectile CONNECTING
                            // WO-VFX-003: Hovl impact key at the connection point (element-tinted).
                            PlayImpactVfxKey(hitDef, hitFoe.WorldPosition);
                            onDealt?.Invoke(dealt);   // WO-861 drainshot: heal == damage DEALT
                        }, def.VfxProjectile, def.UnityColor);
                    }
                    else
                    {
                        // No target in reach — the strike deals nothing, so a drainshot heals
                        // nothing. Report 0 rather than leaving the caller hanging (a silent
                        // no-callback would read as "the heal never ran", §12 no-silent-failures).
                        onDealt?.Invoke(0f);
                    }
                    // Cast beat only (origin SFX/VFX). Impact juice now comes from the projectile
                    // ARRIVAL (enemy TakeDamage), so no target hint -> no premature impact flash.
                    SpawnVfx(atk, def, 1.6f, null);
        }

        // =====================================================================
        //  WO-861 — Sylas + Thrain: shield / manaweave / drainshot + the arrow rider.
        //  These are the ONLY new combat effects in the WO-861 program. Every one of
        //  them is a THIN wrapper over an existing mechanism:
        //    • shield    -> the timed damage-mitigation window Warden's Grace declared
        //                   (ApplyDamageShield) — one store, one reader, no second system.
        //    • manaweave -> RestoreManaOverTime, the WO3 Mana Draught drip.
        //    • drainshot -> ResolveStrikeLike (the real Strike branch) + a heal equal to
        //                   the damage ACTUALLY dealt (post-mitigation, HP-clamped).
        // =====================================================================

        // ── The ONE timed incoming-damage mitigation window ────────────────────────────
        // Written by BOTH Warden's Grace (WO-750, whose -20% DR was authored but never had a
        // store) and the mage's Arcane Shell (`shield`). Read through DamageTakenMultiplier.
        //
        // ⚠ CONSUMER GAP (blunt, WO-861): HeroHealth.TakeDamage does NOT read this yet — its
        // mitigation chain is gear armor -> talent block -> talent DR (+ Last Stand) -> invuln,
        // none of which an ability can write. Until ONE line lands in HeroHealth.TakeDamage
        //   `amount *= _abilities != null ? _abilities.DamageTakenMultiplier : 1f;`
        // BOTH Warden's Grace's -20% and Arcane Shell's -40% are INERT in game. That file is
        // another lane's; this is the producer half only. Do not mark `shield` shipped without it.
        private float _damageTakenMult = 1f;   // 0..1 incoming-damage multiplier while the window is open
        private float _damageShieldUntil;      // Time.time at which the mitigation window closes

        /// <summary>
        /// The incoming-damage MULTIPLIER the hero should currently take (1 = unmitigated,
        /// 0.6 = -40%). Single public read of the one timed mitigation window; a damage
        /// consumer multiplies incoming damage by this.
        /// </summary>
        public float DamageTakenMultiplier => DamageTakenMultiplierAt(Time.time);

        /// <summary>Deterministic form of <see cref="DamageTakenMultiplier"/> — the multiplier
        /// at an explicit clock value. Used by unit tests (EditMode has no advancing clock).</summary>
        public float DamageTakenMultiplierAt(float now)
            => now < _damageShieldUntil ? Mathf.Clamp(_damageTakenMult, MinDamageTakenMult, 1f) : 1f;

        /// <summary><see cref="Time.time"/> at which the mitigation window expires (0 = none).</summary>
        public float DamageShieldUntil => _damageShieldUntil;

        private const float MinDamageTakenMult = 0.05f;   // never mitigate more than 95%
        private const float ShieldDefaultReductionPct = 40f;  // A1 Arcane Shell: -40% (fallback if def.Damage is 0)
        private const float ShieldDefaultSeconds      = 4f;   // A1 Arcane Shell: 4s     (fallback if def.Seconds is 0)

        /// <summary>
        /// Open (or refresh) the timed incoming-damage mitigation window by
        /// <paramref name="reductionPct"/> percent for <paramref name="seconds"/>.
        /// REFRESH SEMANTICS mirror Warden's Grace's shared HoT window: keep the STRONGER
        /// mitigation and the LATER expiry, so a weak re-cast can never shorten or weaken a
        /// stronger shield already up.
        /// </summary>
        public void ApplyDamageShield(float reductionPct, float seconds,
                                      string statusId = "damage-shield", string statusLabel = "Ward")
        {
            if (reductionPct <= 0f || seconds <= 0f)
            {
                FlowTrace.Warn("HeroAbility",
                    $"ApplyDamageShield ignored (pct={reductionPct}, secs={seconds}).");
                return;
            }
            float mult = Mathf.Clamp(1f - reductionPct / 100f, MinDamageTakenMult, 1f);
            if (Time.time < _damageShieldUntil) mult = Mathf.Min(mult, _damageTakenMult);  // stronger wins
            _damageTakenMult   = mult;
            _damageShieldUntil = Mathf.Max(_damageShieldUntil, Time.time + seconds);
            HeroCombatStatus.GetOrAdd(gameObject)?.ApplyNamed(statusId, statusLabel, seconds, isBuff: true);
            FlowTrace.Step("HeroAbility",
                $"damage shield '{statusId}': incoming x{_damageTakenMult:0.00} (-{(1f - _damageTakenMult):P0}) " +
                $"for {seconds:0.#}s. NOTE: inert until HeroHealth.TakeDamage reads DamageTakenMultiplier.");
        }

        /// <summary>
        /// WO-861 — "shield" (Thrain's Arcane Shell): reduce incoming damage by
        /// <c>def.damage</c> PERCENT for <c>def.seconds</c>. Routes through
        /// <see cref="ApplyDamageShield"/>, the SAME window Warden's Grace writes — this is
        /// Warden's Grace's mitigation minus the heal, not a second mitigation system.
        /// (def.Damage is read as a PERCENT here, the same convention Warden's Grace uses for
        /// its heal percent — see <see cref="ResolveWardensGrace"/>.)
        /// </summary>
        private void ResolveShield(AbilityDef def, Vector3 origin)
        {
            float pct  = def.Damage  > 0f ? def.Damage  : ShieldDefaultReductionPct;
            float secs = def.Seconds > 0f ? def.Seconds : ShieldDefaultSeconds;
            ApplyDamageShield(pct, secs, "arcane-shell", "Shell");
            // Support cast beat: the class heal sting (no NEW VFX key — owner tags ability VFX
            // and CLI maps key->hook verbatim; this hook is deliberately left untagged).
            AbilityAudioBridge.PlayForClassAndKind(_heroClass, AbilityEffect.Heal);
            FlowTrace.Step("HeroAbility",
                $"shield '{def.Id ?? def.Name}': -{pct:0}% incoming for {secs:0.#}s (Warden's Grace mitigation path).");
        }

        // ── Manaweave: the EXISTING WO3 mana drip, nothing else ───────────────────────
        private const float ManaweaveDefaultMana    = 5f;   // A4: "restore ~5 mana over 3s"
        private const float ManaweaveDefaultSeconds = 3f;

        /// <summary>
        /// WO-861 — restore <paramref name="manaAmount"/> ABSOLUTE mana over
        /// <paramref name="seconds"/>. Converts to the percent-of-max the existing WO3 drip
        /// speaks and calls <see cref="RestoreManaOverTime"/> — the one and only mana-over-time
        /// mechanism (<c>_manaOverTimeRate</c> / <c>_manaOverTimeUntil</c>, ticked in
        /// <see cref="TickManaOverTime"/>). No second drip is introduced.
        /// </summary>
        public void ApplyManaweave(float manaAmount, float seconds)
        {
            if (manaAmount <= 0f || seconds <= 0f)
            {
                FlowTrace.Warn("HeroAbility", $"ApplyManaweave ignored (mana={manaAmount}, secs={seconds}).");
                return;
            }
            float pct = _maxMana > 0f ? 100f * manaAmount / _maxMana : 0f;
            RestoreManaOverTime(pct, seconds);   // <- the EXISTING drip; re-entrancy/carry handled there
            FlowTrace.Step("HeroAbility",
                $"manaweave: +{manaAmount:0.0} mana over {seconds:0.#}s (= {pct:0}% of a {_maxMana:0} pool) " +
                "via the existing mana-over-time drip.");
        }

        /// <summary>
        /// WO-861 — "manaweave" (Thrain's mana-recovery active). <c>def.damage</c> carries the
        /// ABSOLUTE mana restored, <c>def.seconds</c> the window.
        /// </summary>
        private void ResolveManaweave(AbilityDef def, Vector3 origin)
        {
            float mana = def.Damage  > 0f ? def.Damage  : ManaweaveDefaultMana;
            float secs = def.Seconds > 0f ? def.Seconds : ManaweaveDefaultSeconds;
            ApplyManaweave(mana, secs);
            AbilityAudioBridge.PlayForClassAndKind(_heroClass, AbilityEffect.Heal);
        }

        // ── Drainshot: heal == damage DEALT (PINNED) ──────────────────────────────────

        /// <summary>
        /// Deals <paramref name="amount"/> to <paramref name="foe"/> and returns the damage
        /// ACTUALLY DEALT — <c>Hp</c> before minus <c>Hp</c> after, measured across the target's
        /// own <see cref="IDamageable.TakeDamage"/>. That value is post-resist / post-mitigation
        /// AND clamped by the target's remaining HP (a 34-damage hit on a 10 HP foe deals 10),
        /// which is exactly the number WO-861 pins the drainshot heal to. Returns 0 for a null
        /// or dead target. Never negative (a target that HEALS on hit cannot feed the drain).
        /// </summary>
        public static float ApplyMeasuredDamage(IDamageable foe, float amount, DamageElement element)
        {
            if (foe == null || !foe.IsAlive || amount <= 0f) return 0f;
            float before = foe.Hp;
            foe.TakeDamage(amount, element);
            return Mathf.Max(0f, before - foe.Hp);
        }

        /// <summary>
        /// WO-861 drainshot core (also the unit-test entry): deal <paramref name="amount"/> to
        /// <paramref name="foe"/>, capture the damage ACTUALLY dealt, and heal the caster by
        /// EXACTLY that. Returns the amount healed (== the damage dealt).
        /// </summary>
        public float ApplyDrainshot(IDamageable foe, float amount, DamageElement element)
        {
            float dealt = ApplyMeasuredDamage(foe, amount, element);
            HealFromDrain(null, dealt, amount);
            return dealt;
        }

        /// <summary>
        /// The drain heal. <paramref name="dealt"/> is the damage ACTUALLY dealt;
        /// <paramref name="nominal"/> is only logged so a mis-capture is one grep away.
        /// The heal is deliberately NOT scaled by <c>HealAmountMultiplier</c>: WO-861 pins
        /// "heal = damage dealt", and a class-wide heal talent would break that identity.
        /// </summary>
        private void HealFromDrain(AbilityDef def, float dealt, float nominal)
        {
            string id = def != null ? (def.Id ?? def.Name ?? "drainshot") : "drainshot";
            if (dealt <= 0f)
            {
                FlowTrace.Step("HeroAbility",
                    $"drainshot '{id}': 0 damage dealt (no target / already dead) -> no heal.");
                return;
            }
            if (_heroHealth == null) _heroHealth = TryGetComponent<HeroHealth>(out var hh) ? hh : HeroHealth.Instance;
            _heroHealth?.Heal(dealt);
            FlowTrace.Step("HeroAbility",
                $"drainshot '{id}': dealt {dealt:0.0} (nominal {nominal:0.0}) -> healed caster {dealt:0.0} " +
                "(heal == damage DEALT, post-mitigation + HP-clamped).");
        }

        /// <summary>
        /// WO-861 — "drainshot" (Sylas's Healing Shot): runs the EXISTING Strike resolution
        /// (<see cref="ResolveStrikeLike"/> — same targeting, same projectile, same damage
        /// chain) and heals the caster for the damage that shot actually landed.
        /// </summary>
        private void ResolveDrainshot(AbilityDef def, Vector3 origin)
        {
            float dmg = DamageFor(def);
            ResolveStrikeLike(def, origin, AimPointOverride ?? origin, dmg, ElementOf(def),
                              dealt => HealFromDrain(def, dealt, dmg));
        }

        // ── WO-861 A4 — the ARROW RIDER (`ammoEffect`) hook ───────────────────────────

        /// <summary>
        /// SCOPING GATE (WO-861 acceptance: "arrow riders affect ONLY the Ranger's shot").
        /// True only when (a) this hero's class is the RANGER and (b) the ability being resolved
        /// is the class's LOCKED Q basic attack. The Knight's and Mage's basic attacks fail (a),
        /// and the Ranger's own W/E/R fail (b), so an arrow rider provably cannot ride anything
        /// but the arrow-using basic. Q is never loadout-swappable (see <see cref="Resolve"/>),
        /// so the identity/id comparison against the class Q def is exact.
        /// </summary>
        public bool IsArrowRiderEligible(AbilityDef def)
        {
            if (def == null) return false;
            if (!string.Equals(_heroClass, "ranger", System.StringComparison.OrdinalIgnoreCase)) return false;
            var q = AbilityCatalog.Find(_heroClass, AbilitySlot.Q);
            if (q == null) return false;
            if (ReferenceEquals(q, def)) return true;
            return !string.IsNullOrEmpty(def.Id) &&
                   string.Equals(def.Id, q.Id, System.StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Applies ONE equipped-arrow on-hit rider to a struck foe, reusing the EXISTING
        /// primitives only — <see cref="BurnDoT"/> + <see cref="StatusEffect.Burn"/> for burn,
        /// <see cref="ApplyPoisonRider"/> (stack-capped) for poison, <see cref="StatusEffect.Slow"/>
        /// for frost. No new status system.
        /// <para>
        /// KNOWN LIMIT: <paramref name="slowPct"/> has NO consumer — <c>StatusEffect.Slow</c>
        /// carries a duration but no magnitude, so Rimeshot's authored "-35%" cannot be honoured
        /// without an enemy-side change. It is logged, not silently dropped.
        /// </para>
        /// </summary>
        public void ApplyAmmoRider(IDamageable foe, string ammoEffect, float dps, float seconds, float slowPct)
        {
            if (foe == null || !foe.IsAlive || string.IsNullOrEmpty(ammoEffect)) return;
            bool applied = false;
            switch (ammoEffect.Trim().ToLowerInvariant())
            {
                case "burn":
                case "fire":
                    if (dps <= 0f || seconds <= 0f) return;
                    foe.ApplyStatus(StatusEffect.Burn, seconds);
                    StartCoroutine(BurnDoT(foe, dps, seconds));
                    applied = true;
                    break;
                case "poison":
                case "venom":
                    ApplyPoisonRider(foe, dps, seconds, AmmoPoisonStacks);
                    applied = true;
                    break;
                case "slow":
                case "frost":
                case "snare":
                    if (seconds <= 0f) return;
                    foe.ApplyStatus(StatusEffect.Slow, seconds);
                    applied = true;
                    if (slowPct > 0f)
                        FlowTrace.Once("HeroAbility", "ammo-slowpct",
                            $"arrow rider slow magnitude ({slowPct:0}%) has NO consumer - StatusEffect.Slow " +
                            "is duration-only. Honouring duration; magnitude needs an enemy-side seam.");
                    break;
                default:
                    FlowTrace.Once("HeroAbility", "ammo-unknown:" + ammoEffect,
                        $"arrow rider '{ammoEffect}' is not a known ammoEffect (burn|poison|slow) - ignored.");
                    break;
            }
            if (applied)
                FlowTrace.Step("HeroAbility",
                    $"arrow rider '{ammoEffect}' applied ({dps:0.#} dps / {seconds:0.#}s) to " +
                    $"'{(foe as MonoBehaviour)?.name}'.");
        }

        private const int AmmoPoisonStacks = 2;   // A2 Venomtip: "poison (dot x2)"

        /// <summary>
        /// Resolves the EQUIPPED arrow's on-hit rider. This is the SINGLE reader of the
        /// <c>ammoEffect</c> / <c>ammoDps</c> / <c>ammoSeconds</c> / <c>ammoSlowPct</c> weapon
        /// fields WO-861 A4 specifies.
        /// <para>
        /// ⚠ DATA GAP (WO-861, blunt): <see cref="WeaponDef"/> does NOT declare those four fields
        /// yet, and both <c>GearCatalog.cs</c> (where WeaponDef lives) and <c>weapons.json</c> are
        /// owned by another lane. Until they land this returns false and NO rider is ever applied —
        /// the hook, the scoping gate and the appliers above are complete and tested, the data is
        /// not. See the WO-861 report for the exact remaining data work.
        /// </para>
        /// </summary>
        private bool TryResolveAmmoRider(out string effect, out float dps, out float seconds, out float slowPct)
        {
            effect = null; dps = 0f; seconds = 0f; slowPct = 0f;
            if (_gear == null) TryGetComponent(out _gear);
            var w = _gear != null ? _gear.EquippedWeapon : null;
            if (w == null) return false;
            FlowTrace.Once("HeroAbility", "ammo-fields-missing",
                $"arrow rider: equipped '{w.id}' carries no ammoEffect/ammoDps/ammoSeconds/ammoSlowPct " +
                "(WeaponDef does not declare them yet) - no rider applied. WO-861 A4 data work is OWED.");
            return false;
        }

        // =====================================================================
        //  WO-494 — Knight arena kit: dash / knockback / taunt effect shapes.
        //  Built on the existing seams (NearestHostile, Blast, IDamageable CC,
        //  HeroLocomotion.WarpTo, HeroHealth.Heal). All null-guarded + additive.
        // =====================================================================

        /// <summary>
        /// WO-494 Heroic Leap — gap-closer. Dash to the nearest in-range hostile (or the
        /// reticle-locked foe), land a single-target hit, and apply a brief Freeze (stun)
        /// when dashing INTO a backline target (modelled as: the hit foe is interrupted).
        /// Reuses NearestHostile + HeroLocomotion.WarpTo; no new movement system.
        /// </summary>
        private void ResolveDash(AbilityDef def, Vector3 origin)
        {
            float dmg = DamageFor(def);
            float maxR = def.Range + _enemyHitRadius;
            var foe = InReach(LockedTarget, origin, maxR) ? LockedTarget : NearestHostile(origin, maxR);
            if (foe == null && AimPointOverride == null) foe = LiveBoss();

            if (foe != null)
            {
                // Dash to just short of the foe (keep melee spacing), then strike.
                Vector3 fp = foe.WorldPosition;
                Vector3 dir = fp - origin; dir.y = 0f;
                Vector3 landing = dir.sqrMagnitude > 0.01f
                    ? fp - dir.normalized * Mathf.Min(1.6f, dir.magnitude)
                    : origin;
                if (_loco == null) _loco = GetComponent<HeroLocomotion>();
                _loco?.WarpTo(landing);

                // Ticket #61: hero-dealt -> combo/streak/RAMPAGE eligible.
                (foe as DeNelle.Core.Combat.IHeroDamageMarkable)?.MarkNextHitFromHero();
                foe.TakeDamage(dmg, DamageElement.Aether);
                DeNelle.Core.Combat.DamageAttribution.Record(foe, HeroProgression.Id, dmg);
                // Bonus vs backline: a stun/interrupt on the dashed-into target (Freeze = stun).
                foe.ApplyStatus(StatusEffect.Freeze, 1.0f);
                ReportRumble(dmg);
                // WO-VFX-003: Hovl melee impact where the leap connects.
                PlayImpactVfxKey(def, fp);
            }
            SpawnVfx(origin, def, 1.6f, foe?.WorldPosition);
        }

        /// <summary>
        /// Universal Dash (2026-06-29) — a short cooldown-gated blink/dodge any class can learn
        /// from the Shared Universal talent pool. Warps the hero <c>def.Range</c> metres along its
        /// current facing via <see cref="HeroLocomotion.WarpTo"/> (the same warp the Knight's
        /// Heroic Leap uses), with NO target required, so it reads as an escape/reposition rather
        /// than a gap-closer. No damage; the per-cast cooldown is charged by the caller (TryCast /
        /// TryCastExtra). V1 cooldown-only — no stamina (deferred to V2). Null-guarded + additive.
        /// </summary>
        private void ResolveBlink(AbilityDef def, Vector3 origin)
        {
            if (_loco == null) _loco = GetComponent<HeroLocomotion>();
            Vector3 dir = transform.forward; dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = Vector3.forward;
            float dist = def.Range > 0f ? def.Range : 6f;
            Vector3 landing = origin + dir.normalized * dist;
            _loco?.WarpTo(landing);
            SpawnVfx(landing, def, 1.2f, null);
            DeNelle.Core.Diagnostics.FlowTrace.Step("Hero", "universal blink " + (def.Id ?? "dash") + " -> " + landing);
        }

        /// <summary>
        /// WO-494 Shield Bash — knockback cone + brief slow; interrupts a caster (Freeze)
        /// and breaks a tank's guard. Reuses Blast for the cone damage; pushes survivors
        /// out via a Slow + an away-impulse on any rigidbody, and Freeze to model the
        /// cast interrupt. Cone is approximated as a forward half-radius blast.
        /// </summary>
        private void ResolveKnockback(AbilityDef def, Vector3 origin)
        {
            float dmg = DamageFor(def);
            Vector3 fwd = transform.forward;
            Vector3 centre = origin + fwd * (def.Range * 0.5f);
            float r = def.Range + _enemyHitRadius;
            int count = Physics.OverlapSphereNonAlloc(centre, r, _overlap, _enemyMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var target = AsHostile(_overlap[i]);
                if (target == null) continue;
                // Ticket #61: hero-dealt -> combo/streak/RAMPAGE eligible.
                (target as DeNelle.Core.Combat.IHeroDamageMarkable)?.MarkNextHitFromHero();
                target.TakeDamage(dmg, DamageElement.None);   // physical knockback
                DeNelle.Core.Combat.DamageAttribution.Record(target, HeroProgression.Id, dmg);
                target.ApplyStatus(StatusEffect.Slow, 1.5f);   // brief slow
                target.ApplyStatus(StatusEffect.Freeze, 0.4f); // interrupt a cast / break guard
                // Physical knockback impulse on any rigidbody (null-safe; many enemies are kinematic).
                var rb = _overlap[i].attachedRigidbody;
                if (rb != null && !rb.isKinematic)
                {
                    Vector3 push = target.WorldPosition - origin; push.y = 0f;
                    if (push.sqrMagnitude > 0.001f)
                        rb.AddForce(push.normalized * 6f, ForceMode.VelocityChange);
                }
            }
            ReportRumble(dmg);
            SpawnVfx(centre, def, def.Range);
            // WO-VFX-003: Hovl impact/knockback burst at the cone centre.
            PlayImpactVfxKey(def, centre);
        }

        /// <summary>
        /// WO-494 Defender's Call — taunt zone + temp shield. There is no Taunt status
        /// primitive (StatusEffect = Slow/Freeze/Burn), so this models the "hold them on
        /// me" intent by Slowing every nearby hostile (they stick to the Knight) and
        /// grants the Knight a temp shield via a small self-heal. The AI-side taunt
        /// re-target is owned by the enemy brain (separate system) — additive + safe here.
        /// </summary>
        private void ResolveTaunt(AbilityDef def, Vector3 origin)
        {
            float r = def.Range + _enemyHitRadius;
            // WO-676 Holy Retribution: taunted enemies BURN. Read the taunt-burn rider ONCE
            // per cast (the node's effect.ability names "knight.wardens-roar", so Defender's
            // Call and any damage-0 taunt stay untouched); each held foe below then gets the
            // exact Emberbrand burn shape — StatusEffect.Burn tell + the BurnDoT ticker
            // (mirrors ResolveDot's landing branch). Identity until the capstone is owned.
            bool retribution = HeroTalentModifiers.TryGetAbilityDotRider(
                _heroClass, def.Id, null,
                out float burnDps, out float burnSecs, out _);
            int burned = 0;
            int count = Physics.OverlapSphereNonAlloc(origin, r, _overlap, _enemyMask, QueryTriggerInteraction.Collide);
            for (int i = 0; i < count; i++)
            {
                var target = AsHostile(_overlap[i]);
                if (target == null) continue;
                // Hold nearby foes on the Knight for the zone duration (def.Freeze = seconds).
                target.ApplyStatus(StatusEffect.Slow, Mathf.Max(2f, def.Freeze));
                // WO-614: a taunt that authors damage (Warden's Roar = 10) also strikes each
                // held foe. Defender's Call authors damage 0 -> no-op, so this is additive.
                if (def.Damage > 0f)
                {
                    (target as DeNelle.Core.Combat.IHeroDamageMarkable)?.MarkNextHitFromHero();
                    target.TakeDamage(def.Damage, DamageElement.None);
                    DeNelle.Core.Combat.DamageAttribution.Record(target, HeroProgression.Id, def.Damage);
                }
                if (retribution)
                {
                    target.ApplyStatus(StatusEffect.Burn, burnSecs);
                    StartCoroutine(BurnDoT(target, burnDps, burnSecs));
                    burned++;
                }
            }
            if (retribution && burned > 0)
                DeNelle.Core.Diagnostics.FlowTrace.Step("HeroTalents",
                    $"Holy Retribution: taunt-burn applied to {burned} foe(s) — {burnDps:F0} dps for {burnSecs:F0}s ({def.Id}).");
            // Temp shield = a small self-heal (the temp-shield system is separate; this is the cheap stand-in).
            var heroHp = TryGetComponent<HeroHealth>(out var hh) ? hh : HeroHealth.Instance;
            heroHp?.Heal(20f);
            ReportRumble(20f);
            SpawnVfx(origin, def, def.Range);
            // WO-VFX-003: the roar burst (impact key) + a held taunt aura on the Knight for the
            // zone duration (def.Freeze), so the "hold them on me" beat reads visually.
            PlayImpactVfxKey(def, origin + Vector3.up * 1.0f);
            PlayResidualLoop(def, transform, Mathf.Max(2f, def.Freeze), origin + Vector3.up * 1.0f);
        }

        // =====================================================================
        //  WO-614 — Knight solo re-spec effect shapes: dot / healOverTime / invuln.
        //  Built on the existing seams (LaunchProjectile, StatusEffect.Burn, the
        //  TowerCombat burn-DoT pattern, HeroHealth.RegenTick / ActivateInvuln).
        //  All null-guarded + additive; unknown effect strings fall through to the
        //  enum switch, so mage/ranger and the six canonical shapes are unchanged.
        // =====================================================================

        /// <summary>
        /// WO-614 hook 1 — "dot" (Emberbrand Throw): a ranged strike (reusing the LaunchProjectile
        /// path) that lands its initial damage AND applies <see cref="StatusEffect.Burn"/> plus a
        /// ticking burn DoT for <c>def.DotSeconds</c> at <c>def.DotDamage</c> dps. Mirrors the
        /// Strike branch's target resolution + the TowerCombat burn-DoT coroutine.
        /// </summary>
        private void ResolveDot(AbilityDef def, Vector3 origin)
        {
            float dmg = DamageFor(def);
            float maxR = def.Range + _enemyHitRadius;
            var foe = InReach(LockedTarget, origin, maxR) ? LockedTarget : NearestHostile(origin, maxR);
            if (foe == null && AimPointOverride == null) foe = LiveBoss();

            float burnDps  = def.DotDamage;
            float burnSecs = def.DotSeconds > 0f ? def.DotSeconds : 4f;
            if (foe != null)
            {
                var hitFoe = foe;
                float hitDmg = dmg;
                var hitDef = def;   // WO-VFX-003: capture for impact + residual on arrival
                LaunchProjectile(foe.WorldPosition, () =>
                {
                    if (hitFoe == null || !hitFoe.IsAlive) return;
                    (hitFoe as DeNelle.Core.Combat.IHeroDamageMarkable)?.MarkNextHitFromHero();
                    hitFoe.TakeDamage(hitDmg, DamageElement.Flame);
                    DeNelle.Core.Combat.DamageAttribution.Record(hitFoe, HeroProgression.Id, hitDmg);
                    // WO-VFX-003: Hovl impact where the brand lands.
                    PlayImpactVfxKey(hitDef, hitFoe.WorldPosition);
                    if (burnDps > 0f)
                    {
                        hitFoe.ApplyStatus(StatusEffect.Burn, burnSecs);
                        StartCoroutine(BurnDoT(hitFoe, burnDps, burnSecs));
                        // WO-VFX-003: the residual burn LOOP on the foe for the DoT duration.
                        PlayResidualLoop(hitDef, (hitFoe as MonoBehaviour)?.transform, burnSecs, hitFoe.WorldPosition);
                    }
                    ReportRumble(hitDmg);
                }, def.VfxProjectile, def.UnityColor);
            }
            SpawnVfx(AimPointOverride ?? origin, def, 1.6f, foe?.WorldPosition);
        }

        /// <summary>
        /// WO-614: ticks <paramref name="dps"/> Flame damage per second on <paramref name="target"/>
        /// for <paramref name="duration"/> seconds; stops early if the target dies. Same shape as
        /// TowerCombat's EternalEmber burn DoT.
        /// </summary>
        private System.Collections.IEnumerator BurnDoT(IDamageable target, float dps, float duration)
        {
            float elapsed = 0f;
            const float tick = 1f;
            while (elapsed < duration)
            {
                yield return new WaitForSeconds(tick);
                elapsed += tick;
                if (target == null || !target.IsAlive) yield break;
                target.TakeDamage(dps * tick, DamageElement.Flame);
            }
        }

        // ── WO-676 Venombrand — the poison rider on Thunderbolt / Throwing Spear ──
        // Per-foe stack ledger: each entry is the expiry time of one live poison stack.
        // A hit past the cap (effect.targets, Venombrand = 2) refreshes nothing and adds
        // nothing — "stacks to 2" exactly. The DoT itself mirrors BurnDoT tick-for-tick
        // but deals PLAIN (None-element) damage and deliberately does NOT ApplyStatus
        // Burn — poison must not wear the fire tell. Distinct tell = the VFX follow-up
        // (green DRIP trail + drip icon; never color-only — owner is colorblind).
        private readonly System.Collections.Generic.Dictionary<IDamageable, System.Collections.Generic.List<float>>
            _venomStacks = new System.Collections.Generic.Dictionary<IDamageable, System.Collections.Generic.List<float>>();

        /// <summary>WO-676: applies one stack of Venombrand poison to <paramref name="foe"/>,
        /// capped at <paramref name="maxStacks"/> concurrent stacks per foe.</summary>
        private void ApplyPoisonRider(IDamageable foe, float dps, float duration, int maxStacks)
        {
            if (foe == null || !foe.IsAlive || dps <= 0f || duration <= 0f) return;

            if (!_venomStacks.TryGetValue(foe, out var stamps))
            {
                // Opportunistic ledger sweep: drop foes whose stacks have all lapsed
                // (dead or long-since-cured) so the dictionary never grows unbounded.
                if (_venomStacks.Count >= 32) PruneVenomLedger();
                stamps = new System.Collections.Generic.List<float>(2);
                _venomStacks[foe] = stamps;
            }
            stamps.RemoveAll(t => t <= Time.time);
            if (stamps.Count >= Mathf.Max(1, maxStacks))
            {
                FlowTrace.Throttle("HeroTalents", "venom-capped", 1f,
                    $"Venombrand: foe already at {stamps.Count} poison stack(s) — cap holds, no new stack.");
                return;
            }
            stamps.Add(Time.time + duration);
            StartCoroutine(PoisonDoT(foe, dps, duration));
            FlowTrace.Step("HeroTalents",
                $"Venombrand: poison stack {stamps.Count} applied — {dps:F0} dps for {duration:F0}s.");
        }

        /// <summary>WO-676: one Venombrand stack — mirrors <see cref="BurnDoT"/> tick-for-tick
        /// (1s ticks, stops on death) with plain None-element damage (poison, not fire).</summary>
        private System.Collections.IEnumerator PoisonDoT(IDamageable target, float dps, float duration)
        {
            float elapsed = 0f;
            const float tick = 1f;
            while (elapsed < duration)
            {
                yield return new WaitForSeconds(tick);
                elapsed += tick;
                if (target == null || !target.IsAlive) yield break;
                target.TakeDamage(dps * tick, DamageElement.None);
            }
        }

        /// <summary>Drops venom-ledger entries whose stacks have all expired.</summary>
        private void PruneVenomLedger()
        {
            var stale = new System.Collections.Generic.List<IDamageable>();
            foreach (var kv in _venomStacks)
            {
                bool alive = false;
                foreach (var t in kv.Value)
                    if (t > Time.time) { alive = true; break; }
                if (!alive) stale.Add(kv.Key);
            }
            foreach (var key in stale) _venomStacks.Remove(key);
        }

        /// <summary>
        /// WO-614 hook 2 — "healOverTime" (Oathmend): opens an HP drip window that heals
        /// <c>def.Damage</c> HP/s (scaled by heal talents) for <c>def.Seconds</c> seconds. The
        /// drip runs in Update() via HeroHealth.RegenTick (silent, no per-frame VFX). Fires one
        /// heal cast beat here (SFX + a single Cast_Heal burst) so the cast reads immediately.
        /// </summary>
        private void ResolveHealOverTime(AbilityDef def, Vector3 origin)
        {
            float perSec = def.Damage * HeroTalentModifiers.HealAmountMultiplier(_heroClass);
            float secs   = def.Seconds > 0f ? def.Seconds : 5f;
            _hpOverTimeRate  = perSec;
            _hpOverTimeUntil = Time.time + secs;
            AbilityAudioBridge.PlayForClassAndKind(_heroClass, AbilityEffect.Heal);
            VFXManager.Play(VFXType.Cast_Heal, origin + Vector3.up * 1.2f);
            // WO-VFX-003: the residual heal-over-time aura on the hero for the drip window.
            PlayResidualLoop(def, transform, secs, origin + Vector3.up * 1.2f);
            DeNelle.Core.Diagnostics.FlowTrace.Step("HeroAbility",
                $"healOverTime {def.Id}: {perSec:0.0} HP/s for {secs:0}s ({perSec * secs:0} total).");
        }

        /// <summary>
        /// WO-614 hook 3 — "invuln" (Eternal Aegis, owner: HOTBAR-CASTABLE): grants the hero a
        /// full damage-immunity window for <c>def.Seconds</c> via the existing
        /// <see cref="HeroHealth.ActivateInvuln"/> seam (the same _invulnUntil grace respawn uses).
        /// </summary>
        private void ResolveInvuln(AbilityDef def, Vector3 origin)
        {
            float secs = def.Seconds > 0f ? def.Seconds : 8f;
            if (_heroHealth == null) _heroHealth = TryGetComponent<HeroHealth>(out var hh) ? hh : HeroHealth.Instance;
            _heroHealth?.ActivateInvuln(secs);
            VFXManager.Play(VFXType.Impact_Heal, origin + Vector3.up * 1.0f);
            SpawnVfx(origin, def, Mathf.Max(1.5f, def.Range));
            // WO-VFX-003: the residual shield-bubble LOOP on the hero for the immunity window.
            PlayResidualLoop(def, transform, secs, origin + Vector3.up * 1.0f);
            DeNelle.Core.Diagnostics.FlowTrace.Step("HeroAbility",
                $"invuln {def.Id}: {secs:0}s full immunity granted (HeroHealth.ActivateInvuln).");
        }

        // ── WO-750 Warden's Grace tuning (owner redesign 2026-07-19) ──────────────────
        // Hybrid support: a big % heal + a Defense-scaled bonus, then an 8s Grace Shield
        // (HoT drip + a -20% incoming-damage window). Percent-of-max-HP so it scales with
        // the hero's real max, not a flat number. The heal % is authored in abilities.json
        // (knight.e "damage" read as a PERCENT); the shield duration is the def "seconds".
        private const float GraceHealPct         = 0.25f;  // 25% max HP base heal (fallback if def.Damage is 0)
        private const float GraceDefenseHealBonus = 0.50f; // bonus heal = 50% of max HP scaled by Defense frac (0..0.70 -> up to +35%)
        private const float GraceShieldSeconds   = 8f;     // Grace Shield duration (fallback if def.Seconds is 0)
        private const float GraceHotPct          = 0.05f;  // HoT heals 5% max HP ...
        private const float GraceHotTick         = 2f;     // ... every 2s (drip runs via the shared _hpOverTime window)
        private const float GraceDamageReduction = 0.20f;  // -20% incoming damage (see report: needs a HeroHealth mitigation seam)

        /// <summary>
        /// WO-750 — "gracebuff" (Warden's Grace): the E-slot REDESIGN (was the taunt "Defender's
        /// Call"). Instantly heals the caster for <see cref="GraceHealPct"/> of max HP (authored in
        /// def.Damage as a percent) PLUS a bonus scaled by the Knight's Defense (gear ArmorDefense),
        /// then opens the GRACE SHIELD for def.Seconds: an HP-over-time drip (<see cref="GraceHotPct"/>
        /// of max HP per <see cref="GraceHotTick"/>s) through the existing shared _hpOverTime window,
        /// and a HUD buff marker. The -<see cref="GraceDamageReduction"/> incoming-damage mitigation
        /// needs a small HeroHealth.TakeDamage seam (out of this file's lane — see the WO-750 report);
        /// the heal + Defense bonus + HoT + marker are all self-contained here. Reuses the pooled
        /// VFXManager heal burst (single call — no new double-stack) and the class heal audio sting;
        /// the radiant cast VFX rides the shared castHeal registry row (Heal_Cast). Self-targeted:
        /// never drives the melee attack (F8-48) and does not face a foe.
        /// </summary>
        private void ResolveWardensGrace(AbilityDef def, Vector3 origin)
        {
            if (_heroHealth == null) _heroHealth = TryGetComponent<HeroHealth>(out var hh) ? hh : HeroHealth.Instance;
            float maxHp = _heroHealth != null ? _heroHealth.MaxHp : 100f;

            // Defense (gear ArmorDefense, 0..0.70) scales a bonus heal on top of the flat % heal.
            if (_gear == null) _gear = GetComponent<GearLoadout>();
            float defense = _gear != null ? _gear.ArmorDefense : 0f;

            float healPct   = def.Damage > 0f ? def.Damage / 100f : GraceHealPct;
            float baseHeal  = maxHp * healPct * HeroTalentModifiers.HealAmountMultiplier(_heroClass);
            float bonusHeal = maxHp * GraceDefenseHealBonus * defense;
            float heal      = baseHeal + bonusHeal;
            _heroHealth?.Heal(heal);

            // Grace Shield window: HoT drip (GraceHotPct max HP per GraceHotTick s) for def.Seconds.
            // Reuse the shared _hpOverTime drip (Update ticks HeroHealth.RegenTick) — refresh, never
            // shorten a longer live drip.
            float secs     = def.Seconds > 0f ? def.Seconds : GraceShieldSeconds;
            float hotPerSec = maxHp * GraceHotPct / Mathf.Max(0.5f, GraceHotTick);
            _hpOverTimeRate  = Mathf.Max(_hpOverTimeRate, hotPerSec);
            _hpOverTimeUntil = Mathf.Max(_hpOverTimeUntil, Time.time + secs);

            // WO-861: the -GraceDamageReduction incoming-damage half of the Grace Shield now
            // WRITES the one timed mitigation window (ApplyDamageShield) — the SAME store the
            // mage's `shield` effect uses. This is what makes `shield` "Warden's Grace minus the
            // heal" rather than a second mitigation system. ApplyDamageShield also raises the
            // HUD buff marker for the window (it replaced the standalone ApplyNamed call here).
            ApplyDamageShield(GraceDamageReduction * 100f, secs, "grace-shield", "Grace");

            // Soft heal chime + the FULL-PREFAB Hovl heal read (owner 2026-07-24: use the full
            // multi-layer prefabs, not flattened keys). Heal_Cast = a radiant cast burst at chest
            // height; Heal_Aura = the soft healing aura loop parented to the hero for the Grace
            // shield window (auto-stopped after `secs`). Both route through the ONE VFXManager pool
            // (PlayKey) — a missing key throttled-no-ops (no throw), so this is ship-safe. Replaces
            // the placeholder enum burst so the E heal is unmistakably visible on the hero.
            AbilityAudioBridge.PlayForClassAndKind(_heroClass, AbilityEffect.Heal);
            VFXManager.PlayKey("Heal_Cast", origin + Vector3.up * 1.2f, Quaternion.identity, transform);
            var graceAura = VFXManager.PlayKey("Heal_Aura", transform.position + Vector3.up * 0.1f,
                Quaternion.identity, transform);
            if (graceAura != null) StartCoroutine(StopHandleAfter(graceAura, secs));

            ReportRumble(heal);
            FlowTrace.Step("HeroAbility",
                $"Warden's Grace {def.Id}: heal {heal:0} ({baseHeal:0}+{bonusHeal:0} def={defense:P0}) + " +
                $"Grace Shield {secs:0}s (HoT {hotPerSec:0.0}/s; -{GraceDamageReduction:P0} DR now stored in the " +
                "shared mitigation window - still INERT until HeroHealth.TakeDamage reads DamageTakenMultiplier).");
        }

        /// <summary>WO-494: the full damage chain (talent x level x timing x weapon) for a def's base damage.</summary>
        private float DamageFor(AbilityDef def)
        {
            if (_progression == null) _progression = GetComponent<HeroProgression>();
            float levelMult = _progression != null ? _progression.DamageMultiplier : 1f;
            float dmg = def.Damage * HeroTalentModifiers.DamageMultiplier(_heroClass) * levelMult * _pendingTimingBonus * WeaponMult();
            _pendingTimingBonus = 1f;
            return dmg;
        }

        /// <summary>
        /// WO-497 cheap wire: rumble on LANDING a hit (the existing HeroImpactFeedback.PlayHaptic
        /// only fired when the hero TAKES damage). Scales intensity by the damage dealt. Null-safe:
        /// resolved lazily, no-op without the component / a gamepad. Capped so a 600-dmg ult doesn't
        /// max the motor for the whole duration.
        /// </summary>
        private HeroImpactFeedback _impactFeedback;
        private void ReportRumble(float damage)
        {
            if (_impactFeedback == null) _impactFeedback = GetComponent<HeroImpactFeedback>();
            if (_impactFeedback == null) return;
            float intensity = Mathf.Clamp(0.15f + damage * 0.004f, 0.15f, 0.6f);
            _impactFeedback.PlayHaptic(intensity, 0.10f);
        }

        /// <summary>
        /// DEF (combat feel): launches a VISIBLE projectile (Ranger arrow / Mage-or-other
        /// spell orb) toward <paramref name="target"/> and invokes <paramref name="onArrive"/>
        /// when it lands — so ranged hits read as the shot travelling + connecting rather than
        /// an instant hit-scan ("seeing the arrow/spell go is fun"). Lazily attaches
        /// <see cref="RangedAttackVFX"/> so it works on every hero without a builder change.
        /// </summary>
        private void LaunchProjectile(Vector3 target, System.Action onArrive,
                                      string projectileKey = null, Color? tint = null)
        {
            // Owner directive 2026-07-12 (registry-only motion VFX): the abilities.json travel
            // key is suppressed; the OWNER's phase bundle supplies it instead — the current
            // cast keyword's row vfxProjectile (start = muzzle, end = target, same flight
            // timing/damage path). No row / empty field = invisible travel, by design.
            if (RegistryOnlyMotionVfx)
                projectileKey = TryGetBundleField(_currentCastKeyword, r => r.vfxProjectile);
            if (_rangedVfx == null)
            {
                if (!TryGetComponent(out _rangedVfx)) _rangedVfx = gameObject.AddComponent<RangedAttackVFX>();
            }
            // WO-VFX-RANGED: mage/ranger fly a Hovl travel FX (def.VfxProjectile) muzzle→target and
            // recolour by the ability accent. impactKey=null here — the onArrive closures already fire
            // the Hovl impact via PlayImpactVfxKey, and a non-null travel key suppresses the old
            // SpawnImpact inside RangedAttackVFX (no double impact).
            if (_heroClass == "ranger")      _rangedVfx.FireArrow(target, onArrive, projectileKey, null, tint);
            else if (_heroClass == "knight")
            {
                // WO-VFX-003: the Knight resolves melee-INSTANT (no RangedAttackVFX travelling
                // body). For its THROWN skill-tree actives (Thunderbolt / Throwing Spear /
                // Emberbrand Throw / Snare Arrow) fly a COSMETIC Hovl projectile muzzle→target so
                // the throw reads as a shot travelling; damage still lands instantly via onArrive,
                // so gameplay timing is UNCHANGED (additive visual only). No key = old behaviour.
                if (!string.IsNullOrEmpty(projectileKey))
                    StartCoroutine(FlyCosmeticProjectile(projectileKey, ProjectileMuzzle(), target, tint));
                onArrive?.Invoke();
            }
            else                             _rangedVfx.FireSpellOrb(target, onArrive, projectileKey, null, tint);
        }

        // ── WO-VFX-003: Hovl skill-tree VFX helpers (string-key path, VFXManager.PlayKey) ──
        // Data-driven from AbilityDef.VfxCast/VfxProjectile/VfxImpact/VfxResidual (abilities.json).
        // Every call is null/empty-safe: an unset key or an unauthored catalog row no-ops (throttled
        // log in VFXManager), so this is safe to ship before the HovlVfxCatalog rows are authored.

        // ── OWNER-AUTHORED VFX ONLY (owner directive 2026-07-12, overnight) ──────────
        // "turn off the vfx on motion till i select individually": abilities.json Vfx*
        // defaults are SUPPRESSED at every choke below; the ONLY motion-VFX authority is
        // the owner's Motion Caster registry (motion-castings.json rows, manual:true),
        // resolved per cast via ActionBundleCatalog. abilities.json data stays intact —
        // flip this const to false to restore the data-driven defaults.
        private const bool RegistryOnlyMotionVfx = true;

        // Cast variant -> registry keyword. MUST mirror HeroAnimatorFactory.ResolveSpellCastClips
        // ([1] q → skill1, [2] w → skill2, [3] e → castHeal; [4] r has no registry keyword yet —
        // null = silent until the vocabulary grows a row for it). Variant 0 = generic cast.
        private static readonly string[] CastVariantKeyword = { "cast", "skill1", "skill2", "castHeal", null };

        // ── PER-ABILITY CAST ANIMATION (fix "swapped ability plays the wrong cast clip") ──────
        // The cast animation is chosen at runtime ONLY by the CastVariant int handed to
        // ActorAnimator.PlayCast — the baked controller carries one state per variant (0 generic
        // Cast, 1 Cast_q/skill1, 2 Cast_w/skill2, 3 Cast_e/castHeal, 4 Cast_r) whose CLIP was
        // registry-resolved at build time through MotionCastings' closed keyword vocabulary (the
        // ActionKeywords skill1/skill2/castHeal seam). There is NO runtime clip swap (arch §3), so
        // the fix is to pick the VARIANT from the RESOLVED ability rather than the pressed slot.
        // Priority mirrors the shared keyword seam: explicit per-ability anim key
        // (AbilityDef.CastAnim) > the effect SHAPE's canonical keyword > the pressed slot's own clip
        // as a last-resort fallback (so nothing regresses where a matching state/clip is missing).

        /// <summary>Resolves the CastVariant int for the RESOLVED ability: explicit
        /// <see cref="AbilityDef.CastAnim"/> key > effect-shape keyword > the pressed slot fallback.</summary>
        private static int ResolveAnimVariant(AbilityDef def, int slotFallbackVariant)
        {
            if (def == null) return slotFallbackVariant;
            // 1) explicit ability anim key (abilities.json "castAnim") wins.
            int v = VariantForAnimKey(def.CastAnim);
            if (v >= 0) return v;
            // 2) else the effect SHAPE's canonical anim keyword.
            v = VariantForAnimKey(AnimKeyForEffect(def));
            if (v >= 0) return v;
            // 3) last resort: the pressed slot's own clip (no regression where a clip is missing).
            return slotFallbackVariant;
        }

        /// <summary>Maps a resolved ability's effect SHAPE to its canonical cast-anim keyword. Shapes
        /// with no dedicated rig clip yet (dash/knockback/taunt/blink) return an INTENT keyword that
        /// <see cref="VariantForAnimKey"/> leaves unmapped, so the caller keeps the slot clip until art
        /// imports a leap/slam/shout/blink clip (see the content/mocap WO).</summary>
        private static string AnimKeyForEffect(AbilityDef def)
        {
            string fx = (def.Effect ?? string.Empty).Trim().ToLowerInvariant();
            switch (fx)
            {
                case "heal":         return "castHeal";               // instant heal/ward -> Cast_e
                case "gracebuff":    return "castHeal";               // WO-750 Warden's Grace -> Cast_e (Mage Spell Cast 5)
                case "strike":
                case "snare":        return "skill1";                 // single-target strike -> Cast_q
                case "aoe":
                case "cleave":
                case "meteor":       return "skill2";                 // area/sweep -> Cast_w
                case "dot":
                case "healovertime": return "cast";                  // channel -> generic Cast
                // WO-861: the three new shapes reuse the EXISTING baked cast variants —
                // self-buff/support reads as the heal cast; the drain shot is a single-target
                // strike, so it reads as skill1 (the same clip Quick Shot plays).
                case "shield":
                case "manaweave":    return "castHeal";              // self-cast support -> Cast_e
                case "drainshot":    return "skill1";                // single-target strike -> Cast_q
                // Shapes with no dedicated clip yet (content/mocap WO): intent keywords that
                // fall through VariantForAnimKey to the slot-clip fallback.
                case "dash":         return "leap";
                case "knockback":    return "slam";
                case "taunt":        return "shout";
                case "blink":        return "blink";
                default:             return "cast";
            }
        }

        /// <summary>Maps a cast-anim keyword (explicit or effect-derived) to the CastVariant int whose
        /// baked state carries that clip. Accepts the canonical ActionKeywords cast/skill vocabulary,
        /// the abstract category aliases, and the raw q/w/e/r slot letters. Returns -1 for an unknown /
        /// not-yet-built keyword (leap/slam/shout/blink) so the caller falls back to the pressed slot.</summary>
        private static int VariantForAnimKey(string key)
        {
            switch ((key ?? string.Empty).Trim().ToLowerInvariant())
            {
                case "cast":
                case "castchannel":
                case "generic":
                    return 0;
                case "attack":
                case "strike":
                case "skill1":
                case "q":
                    return 1;
                case "area-cast":
                case "areacast":
                case "skill2":
                case "w":
                    return 2;
                case "cast-heal":
                case "castheal":
                case "heal":
                case "e":
                    return 3;
                case "heavy":
                case "ult":
                case "r":
                    return 4;
                default:
                    return -1;   // leap/slam/shout/blink/unknown -> keep the pressed slot's clip
            }
        }

        /// <summary>The registry casting target for this hero. Combat pivot canon = single
        /// Knight north star; revisit when a second playable class ships.</summary>
        private const string RegistryTarget = "knight";

        /// <summary>The world point a Knight's thrown projectile spawns from (chest height, slightly ahead).</summary>
        private Vector3 ProjectileMuzzle() => transform.position + Vector3.up * 1.2f + transform.forward * 0.6f;

        /// <summary>
        /// CAST-beat VFX. Registry-only mode (owner directive): resolve the cast variant's
        /// keyword to the owner's motion-castings row and fire ITS vfxKey (honoring vfxDelay);
        /// no row / empty key = deliberately silent. Legacy mode: the abilities.json VfxCast key.
        /// </summary>
        private void PlayCastVfxKey(AbilityDef def, Vector3 origin, int castVariant)
        {
            if (RegistryOnlyMotionVfx)
            {
                string keyword = castVariant >= 0 && castVariant < CastVariantKeyword.Length
                    ? CastVariantKeyword[castVariant] : null;
                if (string.IsNullOrEmpty(keyword)) return;
                if (!ActionBundleCatalog.TryGetRow(RegistryTarget, keyword, out var row) ||
                    row == null || string.IsNullOrEmpty(row.vfxKey))
                {
                    DeNelle.Core.Diagnostics.FlowTrace.Once("Vfx", $"no-row-{keyword}",
                        $"cast '{keyword}': no owner vfx row — silent by design (registry-only motion VFX).");
                    return;
                }
                StartCoroutine(FireRegistryCastVfx(row));
                return;
            }
            if (def == null || string.IsNullOrEmpty(def.VfxCast)) return;
            VFXManager.PlayKey(def.VfxCast, origin + Vector3.up * 1.2f, transform.rotation, null, def.UnityColor);
        }

        /// <summary>Fire an owner bundle row's vfxKey after its authored vfxDelay, at the
        /// hero's FIRE-TIME position (chest height) so a delayed key tracks the cast.</summary>
        private System.Collections.IEnumerator FireRegistryCastVfx(ActionBundleRow row)
        {
            if (row.vfxDelay > 0f) yield return new WaitForSeconds(row.vfxDelay);
            DeNelle.Core.Diagnostics.FlowTrace.Step("Vfx",
                $"owner bundle vfx '{row.vfxKey}' fired (delay {row.vfxDelay:0.00}s, registry-only mode).");
            VFXManager.PlayKey(row.vfxKey, transform.position + Vector3.up * 1.2f,
                transform.rotation, null, null);
        }

        /// <summary>Play the IMPACT (end-point) VFX at a hit / blast point. Registry-only
        /// mode: the owner bundle row's vfxImpact for the current cast keyword, ORIENTED
        /// along the caster→impact direction (WO-678 item 4 — no more identity-rotation
        /// landings). Legacy mode: the abilities.json VfxImpact key.</summary>
        private void PlayImpactVfxKey(AbilityDef def, Vector3 at)
        {
            if (RegistryOnlyMotionVfx)
            {
                // Impact-phase AUDIO (owner sound drops 2026-07-12): the row's sfxImpact
                // names a Resources/Sfx clip, played at the landing through the mixer seam
                // (ActionBundlePlayer.PlaySfx convention). Independent of vfxImpact — either
                // phase half can be authored alone.
                string sfx = TryGetBundleField(_currentCastKeyword, r => r.sfxImpact);
                if (!string.IsNullOrEmpty(sfx))
                {
                    var clip = Resources.Load<AudioClip>("Sfx/" + sfx);
                    if (clip != null) DeNelle.Core.CoreServices.Audio?.PlaySfx(clip, 0.9f);
                    else DeNelle.Core.Diagnostics.FlowTrace.Once("Vfx", "sfximpact-missing:" + sfx,
                        $"sfxImpact '{sfx}' has no clip at Resources/Sfx/{sfx} — silent landing.");
                }

                string key = TryGetBundleField(_currentCastKeyword, r => r.vfxImpact);
                if (string.IsNullOrEmpty(key)) return;   // phase unpicked — silent by design
                Vector3 dir = at - transform.position;
                dir.y = 0f;   // landings read best yawed toward travel, not pitched into the ground
                Quaternion rot = dir.sqrMagnitude > 0.01f
                    ? Quaternion.LookRotation(dir.normalized) : Quaternion.identity;
                VFXManager.PlayKey(key, at, rot, null, null);
                return;
            }
            if (def == null || string.IsNullOrEmpty(def.VfxImpact)) return;
            VFXManager.PlayKey(def.VfxImpact, at, Quaternion.identity, null, def.UnityColor);
        }

        // Phase-bundle state: the registry keyword of the cast currently resolving —
        // set in CastAbility, consumed by the projectile/impact phases of that cast.
        private string _currentCastKeyword;

        /// <summary>Resolve one field off the current keyword's owner bundle row;
        /// null when there is no keyword / no row / empty field (silent phase).</summary>
        private string TryGetBundleField(string keyword, System.Func<ActionBundleRow, string> pick)
        {
            if (string.IsNullOrEmpty(keyword)) return null;
            if (!ActionBundleCatalog.TryGetRow(RegistryTarget, keyword, out var row) || row == null)
                return null;
            string v = pick(row);
            return string.IsNullOrEmpty(v) ? null : v;
        }

        /// <summary>
        /// Play the ability's Hovl RESIDUAL LOOP (DoT/HoT/aura/shield) parented to <paramref name="target"/>
        /// (the hero or the struck foe), auto-stopping after <paramref name="seconds"/>. Tinted by the accent.
        /// </summary>
        private void PlayResidualLoop(AbilityDef def, Transform target, float seconds, Vector3 fallbackPos)
        {
            if (RegistryOnlyMotionVfx) return;   // owner directive 2026-07-12: defaults off
            if (def == null || string.IsNullOrEmpty(def.VfxResidual)) return;
            Vector3 pos = target != null ? target.position : fallbackPos;
            var h = VFXManager.PlayKey(def.VfxResidual, pos, Quaternion.identity, target, def.UnityColor);
            if (h != null && seconds > 0f) StartCoroutine(StopHandleAfter(h, seconds));
        }

        private System.Collections.IEnumerator StopHandleAfter(VFXHandle h, float seconds)
        {
            yield return new WaitForSeconds(seconds);
            h?.Stop();
        }

        /// <summary>
        /// Fly a COSMETIC Hovl projectile (loop key) from <paramref name="from"/> to <paramref name="to"/> over
        /// a short flight, then stop it. Visual only — no damage (the caller resolves damage on its own timing).
        /// Used for the Knight's thrown skills, which otherwise resolve melee-instant with no travelling body.
        /// </summary>
        private System.Collections.IEnumerator FlyCosmeticProjectile(string key, Vector3 from, Vector3 to, Color? tint)
        {
            var proxy = new GameObject("[HovlProjProxy]");
            proxy.transform.position = from;
            Vector3 delta = to - from;
            // F8 2026-07-11 "spell cast on a 60 degree angle not flat" — the muzzle sits
            // chest-high (+1.2) while the target is at the enemy's base, so at close range the
            // full-3D delta pitches the launch ROTATION steeply downward. Flatten Y for the
            // ROTATION ONLY (position/travel below stays full-3D so it still reaches the
            // target); fall back to the unflattened vector when the horizontal component is
            // near-zero (target directly above/below).
            Vector3 flat = new Vector3(delta.x, 0f, delta.z);
            Vector3 aim  = flat.sqrMagnitude >= 0.0001f ? flat : delta;
            Quaternion rot = aim.sqrMagnitude > 0.01f ? Quaternion.LookRotation(aim.normalized) : Quaternion.identity;
            var handle = VFXManager.PlayKey(key, from, rot, null, tint, 0f, 0f, proxy.transform);

            const float speed = 26f;
            float travel = Vector3.Distance(from, to) / speed;
            float t = 0f;
            while (t < travel)
            {
                t += Time.deltaTime;
                if (proxy != null) proxy.transform.position = Vector3.Lerp(from, to, Mathf.Clamp01(t / travel));
                yield return null;
            }
            handle?.StopSoft();   // WO-VFX #3: let the cosmetic projectile trail finish, not pop
            if (proxy != null) Destroy(proxy);
        }

        /// <summary>
        /// Gear v1: the equipped weapon's damage multiplier (1.0 when none / no catalog).
        /// Lazily attaches <see cref="GearLoadout"/> so every hero gets gear with no builder
        /// change; graceful — a missing catalog leaves the multiplier at 1.0.
        /// </summary>
        private float WeaponMult()
        {
            if (_gear == null)
            {
                if (!TryGetComponent(out _gear)) _gear = gameObject.AddComponent<GearLoadout>();
            }
            return _gear.WeaponMult;
        }

        /// <summary>
        /// Damages every hostile <see cref="IDamageable"/> within
        /// <paramref name="radius"/> + the enemy hit radius of
        /// <paramref name="centre"/>. Mirrors the local <c>blast()</c> closure
        /// in castAbility.ts.
        /// </summary>
        private void Blast(Vector3 centre, float radius, float damage, DamageElement element, float freezeSeconds)
        {
            float r = radius + _enemyHitRadius;
            int count = Physics.OverlapSphereNonAlloc(centre, r, _overlap, _enemyMask, QueryTriggerInteraction.Collide);
            bool anyHit = false;
            for (int i = 0; i < count; i++)
            {
                var target = AsHostile(_overlap[i]);
                if (target == null) continue;
                // OverlapSphere is centre-distance; castAbility.ts hypot()'s the
                // same way, so no extra precision pass is needed.
                // Ticket #61: hero-dealt -> combo/streak/RAMPAGE eligible.
                (target as DeNelle.Core.Combat.IHeroDamageMarkable)?.MarkNextHitFromHero();
                target.TakeDamage(damage, element);
                DeNelle.Core.Combat.DamageAttribution.Record(target, HeroProgression.Id, damage);
                if (freezeSeconds > 0f)
                    target.ApplyStatus(StatusEffect.Freeze, freezeSeconds);
                anyHit = true;
            }
            if (anyHit) ReportRumble(damage);   // WO-497: rumble on a connecting blast
        }

        /// <summary>
        /// WO-398: true when <paramref name="target"/> is alive AND within
        /// <paramref name="maxRange"/> of <paramref name="origin"/>. Used to gate the
        /// 45m reticle-locked target against a melee ability's own reach so melee slots
        /// can't hit-scan distant enemies.
        /// </summary>
        private static bool InReach(IDamageable target, Vector3 origin, float maxRange)
        {
            if (target == null || !target.IsAlive) return false;
            return (target.WorldPosition - origin).sqrMagnitude <= maxRange * maxRange;
        }

        // ── WO-398 follow-up: gate AoE/Cleave/Meteor blast CENTRE by cast reach ──
        // The snipe WO-398 missed: HeroTargetIndicator feeds AimPointOverride the 45m
        // auto-reticle target (Defend-the-Tower's real crosshair setter is gone — removed
        // 2026-06-09 — so AimPointOverride is ALWAYS the reticle in the live game). The
        // Strike/Snare branch was gated by InReach, but Aoe/Cleave/Meteor blasted directly
        // on that 45m point, so the Knight's Bulwark Slam (W, cleave) / Lantern Charge (R)
        // landed their blast around an enemy 45m away — a melee "slam" sniping across the
        // map. These helpers cap the blast CENTRE to the caster's cast reach, mirroring the
        // WO-398 InReach pattern, so a melee class blasts only nearby foes while ranged
        // classes keep their long reach (a bow/staff is MEANT to reach the locked target).

        /// <summary>
        /// The max distance from the hero this class may CENTRE an AoE/Cleave/Meteor blast.
        /// Melee classes (knight) are capped to the equipped weapon's melee reach (the same
        /// reach the basic swing uses) + the enemy hit radius, so a slam can't snipe; ranged
        /// classes (mage/ranger) keep the full reticle acquire reach so their area spells
        /// still reach the locked foe across the field. Graceful: no gear → a melee default.
        /// </summary>
        private float CastReach()
        {
            bool melee = _heroClass == "knight" || _heroClass == "cleric";
            if (!melee) return RangedCastReach;   // ranged/caster: unchanged long reach

            if (_gear == null) TryGetComponent(out _gear);
            var w = _gear != null ? _gear.EquippedWeapon : null;
            float reach = (w != null && w.reach > 0f) ? w.reach : MeleeDefaultReach;
            return reach + _enemyHitRadius;
        }
        private const float MeleeDefaultReach = 3.4f;   // matches the knight's starter melee reach
        private const float RangedCastReach   = 45f;    // HeroTargetIndicator's reticle acquire range

        /// <summary>
        /// The world point an AoE/Cleave/Meteor blast should be centred on, capped to
        /// <see cref="CastReach"/> of the hero. If the aim point is within reach, blast there;
        /// otherwise snap to the nearest hostile actually in reach; otherwise fall back to the
        /// hero's own position (a self-centred blast) rather than the distant aim — so a melee
        /// class never lands an area hit 45m away. Ranged classes have a 45m reach so their
        /// aim point is virtually always in range and this is a no-op for them.
        /// </summary>
        private Vector3 ResolveBlastCentre(Vector3 atk, Vector3 origin)
        {
            float reach = CastReach();
            if ((atk - origin).sqrMagnitude <= reach * reach) return atk;   // aim is in reach
            var foe = NearestHostile(origin, reach);
            return foe != null ? foe.WorldPosition : origin;                // snap, else self
        }

        /// <summary>
        /// The nearest living hostile <see cref="IDamageable"/> within
        /// <paramref name="maxRange"/> of <paramref name="origin"/>, or null.
        /// Mirrors the local <c>nearest()</c> closure in castAbility.ts.
        /// </summary>
        private IDamageable NearestHostile(Vector3 origin, float maxRange)
        {
            float sweep = maxRange == float.MaxValue ? 1000f : maxRange;
            int count = Physics.OverlapSphereNonAlloc(origin, sweep, _overlap, _enemyMask, QueryTriggerInteraction.Collide);
            IDamageable best = null;
            float bestSqr = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                var target = AsHostile(_overlap[i]);
                if (target == null) continue;
                float sqr = (target.WorldPosition - origin).sqrMagnitude;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = target;
                }
            }
            return best;
        }

        /// <summary>Resolves a collider to a living hostile target, or null.</summary>
        private static IDamageable AsHostile(Collider col)
        {
            if (col == null) return null;
            var dmg = col.GetComponentInParent<IDamageable>();
            // §12 (2026-06-30): the owner's hypothesis was a wrong TEAM/FACTION flag. This trace
            // PROVES per-candidate WHY a collider is/ isn't a valid hero target — no IDamageable,
            // dead, or a non-Hostile faction. Throttled+keyed so it names the excluded object once/sec.
            if (dmg == null)
                return null;   // not a damageable at all (wall/prop) — silent, expected
            if (!dmg.IsAlive || dmg.Faction != CombatFaction.Hostile)
            {
                DeNelle.Core.Diagnostics.FlowTrace.Throttle("HeroAbility", "ashostile-reject-" + col.transform.root.name, 1f,
                    $"AsHostile REJECTED '{col.transform.root.name}': alive={dmg.IsAlive} faction={dmg.Faction} " +
                    "(needs alive + faction=Hostile). If a dungeon enemy shows faction!=Hostile, THAT is a faction bug.");
                return null;
            }
            return dmg;
        }

        // WO-125 Bug 1: cached WaveManager so the hero can reach the apex boss (which
        // lives in WaveManager._liveApexBoss, NOT in the OverlapSphere-reachable enemy
        // roster). Resolved lazily and re-resolved only while null — survives a body swap.
        private WaveManager _wave;

        /// <summary>
        /// The live apex boss as a hostile <see cref="IDamageable"/>, or null when no
        /// boss is up / it is dead. Lets the short-range offensive slots punch up at an
        /// airborne boss they could never sweep. Talks only to the Core seam.
        /// </summary>
        private IDamageable LiveBoss()
        {
            if (_wave == null)
            {
                var found = FindObjectsByType<WaveManager>();
                _wave = found.Length > 0 ? found[0] : null;
            }
            var boss = _wave?.LiveApexBoss;
            if (boss != null && boss.IsAlive && ((IDamageable)boss).Faction == CombatFaction.Hostile)
                return boss;
            return null;
        }

        private static DamageElement ElementOf(AbilityDef def)
        {
            // Map the Mage kit's effect colours to elements for resist math.
            switch (def.EffectEnum)
            {
                case AbilityEffect.Aoe: return DamageElement.Ice;     // Frost Nova
                case AbilityEffect.Meteor: return DamageElement.Flame; // Meteor Strike
                default: return DamageElement.Aether;                  // Arcane Bolt / Beacon
            }
        }

        // =====================================================================
        //  Placeholder VFX — Unity built-in particles (port spec Week 4).
        // =====================================================================

        // WO-35: real per-ability VFX via AbilityVfxKit (was a single tinted dot
        // burst — the "random dots"). An authored _castVfxPrefab still overrides.
        // targetHint = the foe / impact point (drives the strike tracer + meteor fall).
        private static bool HasAuthoredHovlVfx(AbilityDef def) =>
            def != null && (
                !string.IsNullOrEmpty(def.VfxCast) ||
                !string.IsNullOrEmpty(def.VfxProjectile) ||
                !string.IsNullOrEmpty(def.VfxImpact) ||
                !string.IsNullOrEmpty(def.VfxResidual));

        private void SpawnVfx(Vector3 at, AbilityDef def, float radius, Vector3? targetHint = null)
        {
            AbilityAudioBridge.PlayForClassAndKind(_heroClass, def.EffectEnum);   // class-flavoured SFX (WO-37)
            if (_castVfxPrefab != null)
            {
                ParticleSystem ps = Instantiate(_castVfxPrefab, at, Quaternion.identity);
                ps.Play();
                // bug-triage P2: startLifetimeMultiplier is the curve multiplier, not seconds —
                // use the actual max lifetime (matches VFXManager.DetectDuration) so longer
                // prefab effects aren't destroyed before their particles finish.
                float life = ps.main.duration + ps.main.startLifetime.constantMax;
                Destroy(ps.gameObject, life + 0.5f);
                return;
            }

            // F8 2026-07-11 "ability bar casts random vfx": abilities with authored
            // vfxCast/vfxProjectile/vfxImpact/vfxResidual keys already fire curated Hovl
            // beats via PlayCastVfxKey / PlayImpactVfxKey / PlayResidualLoop / FlyCosmeticProjectile.
            // The legacy procedural stack keys ONLY on EffectEnum (dash/knockback/taunt all read as
            // Strike) and stacks wrong generic bursts on top — skip it when data owns the visuals.
            if (HasAuthoredHovlVfx(def))
            {
                DeNelle.Core.Diagnostics.FlowTrace.Throttle("HeroAbility", $"vfx-hovl:{def.Id}", 1f,
                    $"SpawnVfx skipped procedural — '{def.Id}' uses authored Hovl keys.");
                return;
            }

            // WO-195: add a distinct element-coded CAST flash so each spell reads
            // as its own element (fire / frost / arcane / holy) at the cast beat.
            // SpellVfxFactory resolves (effect, class, accent) -> a Cast_* VFXType
            // and plays it through the canonical VFXManager (pooled prefab when
            // wired, procedural fallback otherwise). Additive — the main per-class
            // effect below is unchanged.
            SpellVfxFactory.PlayCast(def.EffectEnum, _heroClass, def.UnityColor, at);

            // DEF-VFX-01: route through VFXManager so prefab-based art swaps require
            // no code changes. Falls back to procedural if no prefab is wired.
            AbilityVfxKit.PlayHeroAbility(def.EffectEnum, def.UnityColor, at,
                                          Mathf.Max(0.6f, radius), targetHint ?? at, _heroClass);
        }

        /// <summary>
        /// Builds a one-shot Unity built-in particle burst — the Week-4
        /// placeholder for the React ability shockwave ring (heroAbilities.ts).
        /// Final VFX art lands later.
        /// </summary>
        private static ParticleSystem BuildBuiltInBurst(Vector3 at)
        {
            var go = new GameObject("AbilityVFX_Placeholder");
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();

            var main = ps.main;
            main.duration = 0.45f;                     // matches the React ring's 0.45s life
            main.loop = false;
            main.startLifetime = 0.45f;
            main.startSpeed = 6f;
            main.startSize = 0.35f;
            main.maxParticles = 64;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.stopAction = ParticleSystemStopAction.None;

            var emission = ps.emission;
            emission.enabled = true;
            emission.rateOverTime = 0f;
            emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 48) });

            var shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 0.2f;

            // A runtime-added ParticleSystem ships with the legacy built-in
            // particle material, which URP renders as a magenta/invalid burst
            // (same missing-shader class as the pets in WO-05). Swap in a URP
            // unlit particle material so the placeholder is actually visible.
            // Only replace when a known shader resolves in THIS build (Shader.Find
            // returns null for a stripped shader) so we never trade the default
            // for a missing (magenta) one. Vertex colour is honoured by both
            // shaders, so TintAndSize's startColor still drives the ability hue.
            var psr = go.GetComponent<ParticleSystemRenderer>();
            if (psr != null)
            {
                Shader sh = Shader.Find("Universal Render Pipeline/Particles/Unlit")
                         ?? Shader.Find("Sprites/Default");
                if (sh != null) psr.material = new Material(sh);
            }

            return ps;
        }
    }
}
