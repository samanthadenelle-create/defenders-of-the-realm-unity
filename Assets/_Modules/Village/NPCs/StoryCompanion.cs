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

using System.Collections.Generic;
using DeNelle.Core.State;
using DeNelle.Core.Combat;
using DeNelle.Core.Diagnostics;
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
    public sealed class StoryCompanion : MonoBehaviour, IDamageableStructure
    {
        // ── Health (WO: companion stakes — mirrors Pet.cs's HP model) ─────────
        // Companions are now mortal: they take contact damage through the SAME
        // IDamageableStructure seam the enemy contact-attack lane already probes
        // (HeroHealth / Tower / Heart implement it too). At 0 HP the companion
        // falls — it deactivates (stops following + fighting + speaking) and is
        // not revived this session; a fresh body respawns on village re-enter
        // (the injector rebuilds roster members each Village load). The party
        // frame reads MaxHp/Hp so the bar is real, not a placeholder.
        private const float DefaultMaxHp = 120f;   // a touch tankier than a pet, weaker than the hero
        [SerializeField] private float _maxHp = DefaultMaxHp;
        private float _hp = DefaultMaxHp;
        private bool _fallen;

        // ── WO-403: live roster registry (no whole-scene FindObjectsByType) ───
        // The HUD party column previously polled FindObjectsByType<StoryCompanion>
        // every 0.5s (PartyHudBridge) — a whole-scene scan that ran in EVERY scene
        // (DDOL bridge), a suspect in the OuterWorld CPU/GC leak. Companions now
        // self-register here on enable, so the party feed is an O(1) registry read
        // ordered by join time (lowest instance id first), with zero scanning.
        private static readonly System.Collections.Generic.List<StoryCompanion> _registry
            = new System.Collections.Generic.List<StoryCompanion>();

        /// <summary>The live companions, ordered by join (lowest instance id first).</summary>
        public static System.Collections.Generic.IReadOnlyList<StoryCompanion> Active => _registry;

        /// <summary>Current HP (party-frame bar reads this).</summary>
        public float Hp => _hp;

        /// <summary>Max HP at this companion's tier (party-frame bar reads this).</summary>
        public float MaxHp => _maxHp;

        /// <summary>True while the companion is up and fighting (HP &gt; 0, not fallen).</summary>
        public bool IsAlive => !_fallen && _hp > 0f;

        // ── IDamageableStructure (lets the enemy contact-attack lane hurt us) ──
        // Enemy.ProbeForStructure / EnemyBrain.TryAttack resolve their target via
        // GetComponentInParent<IDamageableStructure>(); implementing it here is the
        // "damageable wrapper" — an enemy that closes to contact with the companion
        // can now chip it down. No separate component needed; the slim non-trigger
        // hitbox collider the injector attaches (Default layer, since ProbeForStructure
        // uses QueryTriggerInteraction.Ignore) is what lets that probe find us.
        bool IDamageableStructure.IsAlive => IsAlive;

        void IDamageableStructure.ApplyContactDamage(float amount) => TakeDamage(amount);

        /// <summary>
        /// WO-1439 — a story companion walks with the player. Constant Friendly, so enemies
        /// keep attacking it and no defender ever mistakes it for one of its own.
        /// </summary>
        CombatFaction IDamageableStructure.Faction => CombatFaction.Friendly;

        /// <summary>
        /// Applies <paramref name="amount"/> damage. At 0 HP the companion FALLS:
        /// it stops following / fighting / speaking and is deactivated (no revive
        /// this session — a fresh body respawns on the next Village entry). Simple
        /// by design (mirrors Pet.TakeDamage); no death VFX / ragdoll yet.
        /// </summary>
        public void TakeDamage(float amount)
        {
            if (_fallen || _hp <= 0f) return;
            amount = Mathf.Max(0f, amount);
            // Knight Bulwark: while the damage-soak is up the tank takes reduced damage
            // (the other half of the taunt — it pulls the hits AND survives them).
            if (Time.time < _bulwarkUntil) amount *= (1f - _bulwarkReduction);
            _hp = Mathf.Max(0f, _hp - amount);
            if (_hp <= 0f) Fall();
        }

        /// <summary>Heals the companion, clamped to max HP (for a future Cleric/heal kit).</summary>
        public void Heal(float amount)
        {
            if (_fallen) return;
            _hp = Mathf.Min(_maxHp, _hp + Mathf.Max(0f, amount));
        }

        /// <summary>The companion fell at 0 HP — hide it (no revive this session).</summary>
        private void Fall()
        {
            if (_fallen) return;
            _fallen = true;
            FlowTrace.Warn("Companion", $"{DisplayName} ({_hero}) FELL at 0 HP -> deactivating (teardown beat)");
            _bubble?.Hide();
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh) _agent.isStopped = true;
            // Deactivate the body so it stops updating + targeting. PartyHudBridge
            // hides its frame (no live StoryCompanion in that slot). The injector
            // respawns a fresh companion for this roster member on the next Village load.
            Debug.Log($"[StoryCompanion] {DisplayName} fell at 0 HP — deactivating until village re-enter.");
            gameObject.SetActive(false);
        }

        // ── Follow tuning (mirrors PetHeroLeash's "trail, don't block" intent) ─
        // Trail this far behind/beside the hero; never crowd closer than the
        // inner ring (so it stays out of the hero's path + camera centre).
        private const float TrailDistance = 3.2f;
        private const float InnerRing     = 2.2f;
        // Beyond this the companion hurries to catch up (sprint multiplier).
        private const float CatchUpRange  = 9f;
        private const float WalkSpeed     = 3.0f;
        private const float SprintSpeed   = 5.5f;
        // WO-301: beyond this the companion has stranded (e.g. across the village →
        // OuterWorld seam, where the agent can't path to the off-navmesh hero) — it
        // teleports to the hero's shoulder so it never gets left behind. Models the
        // pet/PetHeroLeash transform-follow (no NavMesh dependency).
        private const float TeleportRange = 28f;
        // How far from the hero we still trust the NavMeshAgent to path. Past this
        // (or when the hero is off the baked mesh) we drop to the plain lerp follow.
        private const float AgentReachRange = 18f;
        // Side offset so it walks AT the hero's shoulder rather than dead behind
        // (reads as a companion, not a shadow). Sign flips per-instance via seed.
        private const float SideOffset    = 1.4f;

        // ── Speech tuning ────────────────────────────────────────────────────
        // The companion speaks its intro once when the scene settles, then cycles
        // a contextual line on this cadence while it has a hero to walk beside.
        private const float IntroDelay        = 2.0f;   // let the scene settle first
        private const float LineHold          = 5.5f;   // a line stays up this long
        private const float LineGap           = 9.0f;   // quiet gap between lines

        // ── Combat (2026-06-02: companions FIGHT) ────────────────────────────
        // The companion engages the nearest hostile from the shared TargetManager
        // registry — moves into range, faces it, and fires a class projectile on a
        // cooldown (support damage; weaker than the hero). When no hostile is near it
        // reverts to trailing the hero. Tethered to the hero so it never chases off
        // across the map. The registry is the SAME list the hero's reticle reads.
        private const float EngageRange   = 16f;   // start fighting a hostile within this of the companion
        private const float AttackRange   = 12f;   // ranged kit (Mage/Cleric/Ranger): hold + shoot from here
        // WO-398: the Knight is a MELEE tank — he must close to weapon reach and strike,
        // never snipe from the 12 m ranged hold like the casters. A single shared
        // AttackRange (12 m) + the projectile-for-everyone FireAt() made the Knight deal
        // damage at range (the reported bug). MeleeAttackRange gates the Knight's engage/
        // strike to weapon reach so he only damages enemies he's actually next to.
        private const float MeleeAttackRange = 2.4f; // Knight weapon reach (close + strike here)
        private const float AttackCooldown = 1.1f;
        private const float AttackDamage  = 14f;   // support DPS — chips, doesn't carry
        private const float LeashFromHero = 22f;   // don't engage past this from the hero (stay with the party)
        private float _attackTimer;
        private RangedAttackVFX _ranged;

        // ── Class ability (Tier-2 party teamwork) ────────────────────────────
        // Each companion class has ONE signature ability that AUTO-FIRES on its own
        // cooldown whenever the situation is valid — no hotkey/UI yet (Tier-3). This
        // is what turns four identical basic-attackers into a TEAM: a Cleric sustains
        // the party, a Knight tanks (taunt + soak), a Ranger bursts, a Mage AoEs.
        // Tunables are SerializeField so feel can be dialled in the inspector.
        [Header("Class ability (Tier-2)")]
        [Tooltip("Seconds between class-ability casts (per companion class).")]
        [SerializeField] private float _abilityCooldown = 6f;
        [Tooltip("Range the ability operates in (heal-ally search / AoE radius / engage gate).")]
        [SerializeField] private float _abilityRange = 14f;

        [Header("Cleric — Mend (heal most-wounded ally)")]
        [Tooltip("Fraction of the target's MAX HP restored per heal.")]
        [SerializeField, Range(0.05f, 1f)] private float _healFraction = 0.30f;

        [Header("Knight — Taunt + Bulwark")]
        [Tooltip("Seconds taunted enemies stay fixed on the Knight.")]
        [SerializeField] private float _tauntSeconds = 4f;
        [Tooltip("Incoming-damage reduction while Bulwark is up (0.5 = take half).")]
        [SerializeField, Range(0f, 0.9f)] private float _bulwarkReduction = 0.5f;
        [Tooltip("Seconds the Knight's Bulwark damage-soak lasts after each taunt.")]
        [SerializeField] private float _bulwarkSeconds = 4f;

        [Header("Ranger — Multishot")]
        [Tooltip("Arrows fired in the Multishot burst.")]
        [SerializeField, Min(2)] private int _multishotArrows = 3;
        [Tooltip("Damage per Multishot arrow.")]
        [SerializeField] private float _multishotDamagePerArrow = 12f;

        [Header("Mage — Arcane Burst (AoE)")]
        [Tooltip("Damage dealt to every enemy in the burst radius.")]
        [SerializeField] private float _mageBurstDamage = 26f;

        // Gear weapon multiplier (owner 2026-06-16): the companion's GearLoadout pushes its
        // equipped-weapon damageMult here so the companion's attacks scale with assigned gear
        // (the companion has no HeroAbilities damage chain). 1 = no weapon bonus.
        private float _gearWeaponMult = 1f;

        /// <summary>Set the equipped-weapon damage multiplier (driven by this companion's
        /// GearLoadout on every gear change). Floored so it can never zero out damage.</summary>
        public void SetGearWeaponMult(float mult) => _gearWeaponMult = Mathf.Max(0.1f, mult);

        // Ability runtime.
        private float _abilityTimer;            // counts down to the next cast
        private float _bulwarkUntil;            // Time.time while the Knight soak is active
        private static readonly List<Enemy> s_aoeBuf = new List<Enemy>(32); // shared AoE scratch

        // WO-410 (perf): the Cleric's heal scan (FindMostWoundedAlly) runs EVERY FRAME
        // while idle — when nobody is wounded the cooldown is NOT consumed, so it re-fires
        // each Update. It used to FindObjectsByType<StoryCompanion>() + <Pet>() (two array
        // allocs) every one of those frames. Cache the rosters and only re-scan the scene
        // for membership on a throttle; HP is still read live off the cached refs each call.
        private StoryCompanion[] _alliesCache;
        private DeNelle.Pets.Pet[] _petsCache;
        private float _allyCacheTimer;
        private const float AllyCacheInterval = 0.5f;

        // ── Runtime ──────────────────────────────────────────────────────────
        private HeroClass _hero = HeroClass.Knight;
        private Transform _heroT;
        private NavMeshAgent _agent;
        private TownsfolkBubble _bubble;

        // Animator locomotion (same "Speed" blend the hero/AmbientNPC use). Guarded
        // by _hasSpeedParam so we never spam errors on a controller without it.
        private Animator _animator;
        private static readonly int SpeedHash = Animator.StringToHash("Speed");
        private bool _hasSpeedParam;
        private Vector3 _lastPos;

        private float _resolveTimer;
        private float _speakTimer;
        private bool  _introSpoken;
        private bool  _bubbleUp;
        private int   _lineCursor;
        private float _sideSign = 1f;

        // WO-277: while the FTUE owns the narrative the companion's ambient chatter
        // must stay quiet (the tutorial drives the same bubble with scripted lines).
        // Suppressing only the AUTO speech leaves the companion still FOLLOWING +
        // FIGHTING; the tutorial calls SetSpeechSuppressed(false) when it completes.
        private bool _speechSuppressed;

        // ── Configuration ────────────────────────────────────────────────────

        /// <summary>
        /// Sets which hero's companion this is — drives the name + line pool.
        /// Called by the injector before <see cref="Start"/> runs.
        /// </summary>
        public void Configure(HeroClass hero)
        {
            _hero = hero;
        }

        /// <summary>Which hero's companion this is (drives the party-frame label).</summary>
        public HeroClass Hero => _hero;

        /// <summary>The companion's display name, for the HUD party frame.</summary>
        public string DisplayName => CompanionDialogue.NameFor(_hero);

        /// <summary>Assigns the speech bubble (the injector wires this).</summary>
        public void SetBubble(TownsfolkBubble bubble)
        {
            _bubble = bubble;
        }

        /// <summary>The companion's speech bubble, so the FTUE can drive it with scripted lines.</summary>
        public TownsfolkBubble Bubble => _bubble;

        /// <summary>
        /// WO-277 — suppress (or restore) the companion's AMBIENT auto-chatter while
        /// the tutorial scripts the dialogue. Follow + combat are unaffected. While
        /// suppressed the companion hides any line it was showing so it doesn't fight
        /// the tutorial's bubble.
        /// </summary>
        public void SetSpeechSuppressed(bool suppressed)
        {
            _speechSuppressed = suppressed;
            if (suppressed && _bubbleUp)
            {
                _bubbleUp = false;
                _bubble?.Hide();
            }
        }

        /// <summary>Assigns the hero transform to trail (the injector wires this).</summary>
        public void SetHero(Transform hero)
        {
            _heroT = hero;
        }

        // ── Lifecycle ────────────────────────────────────────────────────────

        // WO-403: keep the live roster registry current so the HUD party column can
        // read it without a per-tick FindObjectsByType scan. Inserted in instance-id
        // order so the party slots stay stable (join order = lowest id first).
        private void OnEnable()
        {
            if (_registry.Contains(this)) return;
            int id = GetInstanceID();
            int pos = _registry.Count;
            while (pos > 0 && _registry[pos - 1].GetInstanceID() > id) pos--;
            _registry.Insert(pos, this);
        }

        private void OnDisable()
        {
            _registry.Remove(this);
        }

        private void Start()
        {
            // Stable per-instance side (left/right shoulder) so it isn't dead-centre.
            _sideSign = (gameObject.GetInstanceID() & 1) == 0 ? 1f : -1f;

            // Init HP from the (possibly inspector-tuned) max so a fresh body is full.
            if (_maxHp <= 0f) _maxHp = DefaultMaxHp;
            _hp = _maxHp;
            _fallen = false;

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

            // Cache the mesh Animator + whether its controller declares "Speed".
            _animator = GetComponentInChildren<Animator>();
            if (_animator != null && _animator.runtimeAnimatorController != null)
                foreach (var p in _animator.parameters)
                    if (p.nameHash == SpeedHash) { _hasSpeedParam = true; break; }
            _lastPos = transform.position;

            if (_heroT == null) _heroT = ResolveHeroFallback();

            _speakTimer = IntroDelay;
            // Stagger the first cast so a party doesn't fire every ability on frame 1.
            _abilityTimer = _abilityCooldown * 0.5f;

            FlowTrace.Step("Companion", $"Start {DisplayName} ({_hero}): heroResolved={_heroT != null} " +
                $"agent={(_agent != null ? (_agent.enabled ? "on-navmesh" : "disabled/off-navmesh") : "none")} " +
                $"bubble={(_bubble != null)} pos={transform.position}");
        }

        private void Update()
        {
            ResolveHeroIfNeeded();

            // Tier-2 class ability ticks on its own cooldown, INDEPENDENT of the basic
            // attack. The Cleric's Mend can fire while merely following (it targets a
            // wounded ally, not an enemy); the dps/tank abilities gate on a valid foe
            // inside TryClassAbility. Returns true when it consumed this frame's action
            // (e.g. the Cleric paused to heal) so we skip the basic-attack/follow step.
            bool abilityActed = TickClassAbility();

            if (!abilityActed)
            {
                // Fight a nearby hostile if there is one; otherwise trail the hero.
                if (!UpdateCombat())
                    UpdateFollow();
            }

            UpdateSpeech();
            DriveAnimator();
        }

        // ── Class ability dispatch (Tier-2) ──────────────────────────────────

        /// <summary>
        /// Counts the ability cooldown down and, when ready, attempts this companion's
        /// signature CLASS ability (by <see cref="HeroClass"/>). Returns true only when
        /// the Cleric paused its turn to heal (so the caller skips the basic step that
        /// frame); the dps/tank abilities fire alongside the normal combat loop and
        /// return false so the companion still positions/basic-attacks.
        /// </summary>
        private bool TickClassAbility()
        {
            if (_fallen) return false;
            _abilityTimer -= Time.deltaTime;
            if (_abilityTimer > 0f) return false;

            switch (_hero)
            {
                case HeroClass.Cleric: return TryClericMend();   // heal — may pause to act
                case HeroClass.Knight: TryKnightTaunt();   break; // tank — fires with combat
                case HeroClass.Ranger: TryRangerMultishot(); break; // dps burst
                case HeroClass.Mage:   TryMageBurst();     break; // dps/control AoE
            }
            return false;
        }

        /// <summary>
        /// CLERIC — Mend. Finds the most-wounded ALLY (hero + other companions + pet)
        /// within <see cref="_abilityRange"/> and heals it a chunk of its MAX HP. Only
        /// fires when someone is actually wounded (else the cooldown is NOT consumed, so
        /// it heals the instant an ally drops). Plays the existing heal VFX on the target.
        /// Returns true when it healed (the Cleric holds position + faces the patient that
        /// frame instead of basic-attacking). This sustain is what makes the party a team.
        /// </summary>
        private bool TryClericMend()
        {
            if (!FindMostWoundedAlly(out Vector3 healPos, out float healAmount, out System.Action applyHeal))
                return false;   // nobody hurt — don't burn the cooldown, keep basic-attacking

            applyHeal();
            _abilityTimer = _abilityCooldown;
            FlowTrace.Once("Companion", $"mend-{GetInstanceID()}", $"{DisplayName} Cleric Mend fired (heal={healAmount:F0}) — team sustain reached.");

            // Reuse the hero/respawn heal VFX (Impact_Heal green sparkle). Null-safe static.
            VFXManager.Play(VFXType.Impact_Heal, healPos + Vector3.up);
            GameSfx.PlayHeroHit();   // placeholder cue — no dedicated heal SFX yet (FLAGGED)

            // Hold + face the patient for the beat (don't path away mid-heal).
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh) _agent.isStopped = true;
            FaceWorld(healPos);
            return true;
        }

        /// <summary>
        /// Scans all allies — hero (HeroHealth), the other StoryCompanions, the pet(s)
        /// — within <see cref="_abilityRange"/> and picks the one with the LOWEST HP
        /// fraction that is below max (the most-wounded). Out-params return where to play
        /// the VFX, the heal amount used (for logging/feel), and a closure that applies
        /// the clamped heal to that specific ally. Returns false when nobody is wounded
        /// in range (so the Cleric doesn't over-heal or waste the cast).
        /// </summary>
        private bool FindMostWoundedAlly(out Vector3 pos, out float amount, out System.Action apply)
        {
            pos = Vector3.zero; amount = 0f; apply = null;
            float rangeSqr = _abilityRange * _abilityRange;
            float worstFrac = 0.999f;   // must be BELOW max to qualify (don't over-heal)
            Vector3 self = transform.position;

            // — Hero —
            var hero = HeroHealth.Instance;
            if (hero != null && hero.IsAlive && (hero.transform.position - self).sqrMagnitude <= rangeSqr)
            {
                float frac = hero.MaxHp > 0f ? hero.Hp / hero.MaxHp : 1f;
                if (frac < worstFrac)
                {
                    worstFrac = frac;
                    Vector3 hp = hero.transform.position; float heal = hero.MaxHp * _healFraction;
                    pos = hp; amount = heal; apply = () => hero.Heal(heal);
                }
            }

            // WO-410: refresh the cached rosters only on a throttle (membership changes
            // rarely), not every frame. The per-frame array allocs were the GC source.
            _allyCacheTimer -= Time.deltaTime;
            if (_alliesCache == null || _petsCache == null || _allyCacheTimer <= 0f)
            {
                _allyCacheTimer = AllyCacheInterval;
                _alliesCache = FindObjectsByType<StoryCompanion>();
                _petsCache   = FindObjectsByType<DeNelle.Pets.Pet>();
            }

            // — Other companions (including a wounded self) —
            var companions = _alliesCache;
            foreach (var c in companions)
            {
                if (c == null || !c.IsAlive) continue;
                if ((c.transform.position - self).sqrMagnitude > rangeSqr) continue;
                float frac = c.MaxHp > 0f ? c.Hp / c.MaxHp : 1f;
                if (frac < worstFrac)
                {
                    worstFrac = frac;
                    StoryCompanion target = c; float heal = c.MaxHp * _healFraction;
                    pos = c.transform.position; amount = heal; apply = () => target.Heal(heal);
                }
            }

            // — Pet(s) —
            var pets = _petsCache;
            foreach (var p in pets)
            {
                if (p == null || !p.IsAlive) continue;
                if ((p.transform.position - self).sqrMagnitude > rangeSqr) continue;
                float frac = p.MaxHp > 0f ? p.Hp / p.MaxHp : 1f;
                if (frac < worstFrac)
                {
                    worstFrac = frac;
                    DeNelle.Pets.Pet target = p; float heal = p.MaxHp * _healFraction;
                    pos = p.transform.position; amount = heal; apply = () => target.Heal(heal);
                }
            }

            return apply != null;
        }

        /// <summary>
        /// KNIGHT — Taunt + Bulwark. Pulls every enemy in <see cref="_abilityRange"/>
        /// onto the Knight (EnemyBrain.TauntTo) and raises a damage-soak (_bulwarkUntil),
        /// so the tank both grabs aggro and survives it. Only fires when at least one
        /// enemy is in range (else keeps the cooldown ready). No new VFX — reuses the
        /// existing combat feel; a dedicated shout/shield VFX is a later polish (FLAGGED).
        /// </summary>
        private void TryKnightTaunt()
        {
            var tm = TargetManager.Instance;
            if (tm == null) return;
            tm.CollectInRange(transform.position, _abilityRange, s_aoeBuf);
            if (s_aoeBuf.Count == 0) return;   // nothing to taunt — save the cooldown

            for (int i = 0; i < s_aoeBuf.Count; i++)
            {
                var brain = s_aoeBuf[i] != null ? s_aoeBuf[i].GetComponent<EnemyBrain>() : null;
                if (brain != null) brain.TauntTo(transform, _tauntSeconds);
            }
            _bulwarkUntil = Time.time + _bulwarkSeconds;
            _abilityTimer = _abilityCooldown;
            FlowTrace.Once("Companion", $"taunt-{GetInstanceID()}", $"{DisplayName} Knight Taunt+Bulwark fired (taunted {s_aoeBuf.Count}) — team tank reached.");
            // Reuse the shockwave-ring impact as a stand-in "ground slam / shout" tell.
            VFXManager.Play(VFXType.Impact_ShockwaveRing, transform.position);
        }

        /// <summary>
        /// RANGER — Multishot. Fires several arrows at the nearest enemy in one burst for
        /// strong front-loaded damage. Reuses RangedAttackVFX.FireArrow (the same arrow the
        /// basic attack uses). Only fires when a foe is in range.
        /// </summary>
        private void TryRangerMultishot()
        {
            var tm = TargetManager.Instance;
            if (tm == null) return;
            Enemy foe = tm.GetClosestTarget(transform.position, _abilityRange);
            if (foe == null || !foe.IsAlive) return;

            // GetComponent returns a Unity fake-null on miss, so `??` does NOT fall
            // through; use TryGetComponent so a real component is always assigned.
            if (_ranged == null && !TryGetComponent(out _ranged))
                _ranged = gameObject.AddComponent<RangedAttackVFX>();

            var dmg = foe.GetComponent<EnemyDamageable>() as IDamageable;
            Vector3 baseTarget = foe.transform.position + Vector3.up;
            for (int i = 0; i < _multishotArrows; i++)
            {
                // Slight horizontal spread so the volley reads as several arrows.
                Vector3 spread = new Vector3((i - (_multishotArrows - 1) * 0.5f) * 0.5f, 0f, 0f);
                float perArrow = _multishotDamagePerArrow * _gearWeaponMult;
                System.Action onArrive = () =>
                {
                    if (dmg != null && dmg.IsAlive) dmg.TakeDamage(perArrow, DamageElement.None);
                };
                _ranged.FireArrow(baseTarget + spread, onArrive);
            }
            FaceWorld(foe.transform.position);
            _abilityTimer = _abilityCooldown;
            FlowTrace.Once("Companion", $"multishot-{GetInstanceID()}", $"{DisplayName} Ranger Multishot fired ({_multishotArrows} arrows) — team burst reached.");
        }

        /// <summary>
        /// MAGE — Arcane Burst. Damages EVERY enemy within <see cref="_abilityRange"/> of
        /// the nearest foe (an AoE around the cluster centre), with an Aether impact at
        /// each. Only fires when enemies are present. Reuses the Aether impact VFX.
        /// </summary>
        private void TryMageBurst()
        {
            var tm = TargetManager.Instance;
            if (tm == null) return;
            Enemy nearest = tm.GetClosestTarget(transform.position, _abilityRange);
            if (nearest == null || !nearest.IsAlive) return;

            // Burst centred on the nearest foe; radius = a slice of the ability range so
            // it rewards clustered enemies (the Mage's "control" identity).
            Vector3 centre = nearest.transform.position;
            float burstRadius = _abilityRange * 0.5f;
            tm.CollectInRange(centre, burstRadius, s_aoeBuf);

            for (int i = 0; i < s_aoeBuf.Count; i++)
            {
                var e = s_aoeBuf[i];
                if (e == null || !e.IsAlive) continue;
                var dmg = e.GetComponent<EnemyDamageable>() as IDamageable;
                if (dmg != null && dmg.IsAlive) dmg.TakeDamage(_mageBurstDamage * _gearWeaponMult, DamageElement.Aether);
                VFXManager.Play(VFXType.Impact_Aether, e.transform.position + Vector3.up);
            }
            // Centre detonation so the AoE reads even when it whiffs the edges.
            VFXManager.Play(VFXType.Impact_ExplosionAether, centre + Vector3.up * 0.5f);
            FaceWorld(centre);
            _abilityTimer = _abilityCooldown;
            FlowTrace.Once("Companion", $"burst-{GetInstanceID()}", $"{DisplayName} Mage Arcane Burst fired ({s_aoeBuf.Count} in radius) — team AoE reached.");
        }

        /// <summary>Smoothly turns the companion to face a world point (heal/cast tell).</summary>
        private void FaceWorld(Vector3 worldPos)
        {
            Vector3 dir = worldPos - transform.position; dir.y = 0f;
            if (dir.sqrMagnitude < 0.0004f) return;
            transform.rotation = Quaternion.Slerp(
                transform.rotation, Quaternion.LookRotation(dir, Vector3.up), Time.deltaTime * 8f);
        }

        /// <summary>WO-455: re-point the locomotion animator at a specific body's animator after the
        /// visible body changed. HeroArmorVisual swaps the companion's visible body for an armored
        /// instance (and hides the base); without re-pointing here the companion keeps driving the
        /// now-hidden base animator, so the visible armored body never gets the Speed blend and
        /// T-poses (the owner-reported "changed gear, still T-pose"). Null → re-resolve from children.</summary>
        public void SetActiveAnimator(Animator a)
        {
            if (a == null) { RebindAnimator(); return; }
            _animator = a;
            _hasSpeedParam = false;
            if (a.runtimeAnimatorController != null)
                foreach (var p in a.parameters)
                    if (p.nameHash == SpeedHash) { _hasSpeedParam = true; break; }
        }

        /// <summary>Re-resolve the locomotion animator from the current child hierarchy — used after
        /// the armor body is removed and the base body restored, so we don't keep a dangling ref.</summary>
        public void RebindAnimator()
        {
            _animator = GetComponentInChildren<Animator>();
            _hasSpeedParam = false;
            if (_animator != null && _animator.runtimeAnimatorController != null)
                foreach (var p in _animator.parameters)
                    if (p.nameHash == SpeedHash) { _hasSpeedParam = true; break; }
        }

        /// <summary>
        /// Feeds the locomotion blend tree: agent velocity when pathing, else the
        /// per-frame transform delta (lerp fallback). 0 → Idle, higher → Walk/Run.
        /// Guarded so it never touches a controller that lacks the param.
        /// </summary>
        private void DriveAnimator()
        {
            if (_animator == null || !_hasSpeedParam) return;

            float speed;
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh && !_agent.isStopped)
                speed = _agent.velocity.magnitude;
            else
                speed = Time.deltaTime > 0f
                    ? (transform.position - _lastPos).magnitude / Time.deltaTime
                    : 0f;
            _lastPos = transform.position;

            _animator.SetFloat(SpeedHash, speed, 0.08f, Time.deltaTime);
        }

        // ── Combat (engage the shared registry's nearest hostile) ────────────

        /// <summary>
        /// If a living hostile is within <see cref="EngageRange"/> (and the companion
        /// is still near the hero), move into <see cref="AttackRange"/>, face it, and
        /// fire a class projectile on cooldown. Returns true while engaged (so the
        /// caller skips the follow step). Uses the shared <see cref="TargetManager"/>
        /// registry — the same source the hero's reticle reads.
        /// </summary>
        private bool UpdateCombat()
        {
            var tm = TargetManager.Instance;
            if (tm == null) return false;

            // Stay with the party: only engage when the companion is near the hero, so
            // it never abandons the Keeper to chase across the field.
            if (_heroT != null)
            {
                Vector3 toHero = _heroT.position - transform.position; toHero.y = 0f;
                if (toHero.sqrMagnitude > LeashFromHero * LeashFromHero) return false;
            }

            Enemy foe = tm.GetClosestTarget(transform.position, EngageRange);
            if (foe == null || !foe.IsAlive) return false;

            Vector3 tp = foe.transform.position;
            Vector3 flat = tp - transform.position; flat.y = 0f;
            float dist = flat.magnitude;

            // WO-398: melee classes (Knight tank) hold + strike at weapon reach; ranged
            // classes (Mage/Cleric/Ranger) hold + shoot from the 12 m ranged distance. One
            // effective range governs BOTH the approach (close until inside it) and the
            // damage gate below, so the Knight closes to his target and only hits in reach.
            float effectiveRange = (_hero == HeroClass.Knight) ? MeleeAttackRange : AttackRange;

            // Close to attack range; hold once inside it.
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
            {
                if (dist > effectiveRange) { _agent.isStopped = false; _agent.SetDestination(tp); }
                else _agent.isStopped = true;
            }

            // Face the foe.
            if (flat.sqrMagnitude > 0.0004f)
                transform.rotation = Quaternion.Slerp(
                    transform.rotation, Quaternion.LookRotation(flat, Vector3.up), Time.deltaTime * 8f);

            // Strike on cooldown when in range.
            _attackTimer -= Time.deltaTime;
            if (dist <= effectiveRange && _attackTimer <= 0f)
            {
                _attackTimer = AttackCooldown;
                FireAt(foe);
            }
            return true;
        }

        /// <summary>Strikes the foe; damages it via the Core IDamageable seam (so it gets
        /// the normal hit feedback). WO-398: the Knight is MELEE — he resolves damage
        /// instantly on the in-reach foe with NO projectile (a tank doesn't snipe). Only the
        /// ranged classes launch a travelling projectile (Ranger arrow / Mage-Cleric orb),
        /// mirroring the player-hero rule in HeroAbilities.LaunchProjectile.</summary>
        private void FireAt(Enemy foe)
        {
            var dmg = foe.GetComponent<EnemyDamageable>() as IDamageable;
            System.Action onArrive = () =>
            {
                if (dmg != null && dmg.IsAlive) dmg.TakeDamage(AttackDamage * _gearWeaponMult, DamageElement.None);
            };

            // WO-398: Knight melee — no projectile; the foe is already in weapon reach
            // (UpdateCombat gated this call to MeleeAttackRange), so apply the hit now.
            if (_hero == HeroClass.Knight) { onArrive(); return; }

            // GetComponent returns a Unity fake-null on miss, so `??` does NOT fall
            // through; use TryGetComponent so a real component is always assigned.
            if (_ranged == null && !TryGetComponent(out _ranged))
                _ranged = gameObject.AddComponent<RangedAttackVFX>();

            Vector3 target = foe.transform.position + Vector3.up;
            if (_hero == HeroClass.Ranger) _ranged.FireArrow(target, onArrive);
            else                           _ranged.FireSpellOrb(target, onArrive);
        }

        // ── Hero resolution ──────────────────────────────────────────────────

        private void ResolveHeroIfNeeded()
        {
            if (_heroT != null) return;
            // KNOWN SUSPECT (OuterWorld additive seam): the hero rig is torn down/rebuilt
            // and FindAnyObjectByType<HeroLocomotion>() momentarily returns null, so the
            // companion strands with _heroT == null. Throttle a Warn while we have no hero
            // so a capture shows HOW LONG it stays stranded; Once on re-acquire names the fix.
            FlowTrace.Throttle("Companion", $"hero-null-{GetInstanceID()}", 1f,
                $"{DisplayName} ({_hero}) has NO hero target — re-resolving (~1Hz). If this repeats forever the hero never re-resolved across the seam.");
            _resolveTimer -= Time.deltaTime;
            if (_resolveTimer <= 0f)
            {
                _resolveTimer = 1.0f;
                _heroT = ResolveHeroFallback();
                if (_heroT != null)
                    FlowTrace.Step("Companion", $"{DisplayName} ({_hero}) RE-ACQUIRED hero target ({_heroT.name}) — follow resumes");
            }
        }

        /// <summary>
        /// Resolves the Keeper. WO-438 (FIX 1): robust hero resolution mirroring
        /// the old ShopPanel.FindActiveHeroGO's priority (that panel was deleted 2026-09-06,
        /// WO-1430; the ORDER below is the surviving statement of it) — tag "Player" (the hero IS tagged
        /// Player, set in HeroControlEnsurer.Ensure, CLAUDE.md §7 / WO-450) → the
        /// typed HeroLocomotion lookup → a name-prefix sweep ("Hero ("). The earlier
        /// comment that "Player" was undeclared is stale; FindWithTag returns null
        /// (not a throw) when nothing carries it, so trying it first is safe and
        /// catches the case where HeroLocomotion hasn't spun up yet on first scan.
        /// The name sweep is only reached when the cheap lookups both miss, so the
        /// per-frame GC concern (WO-410) stays bounded. The caller caches the result
        /// in _heroT and only re-resolves (~1Hz) while it is null, so this keeps
        /// retrying until the hero exists — the companion then engages instead of
        /// stranding at its spawn.
        /// </summary>
        private static Transform ResolveHeroFallback()
        {
            var byTag = GameObject.FindWithTag("Player");
            if (byTag != null) return byTag.transform;

            var hero = UnityEngine.Object.FindAnyObjectByType<HeroLocomotion>();
            if (hero != null) return hero.transform;

            foreach (var t in UnityEngine.Object.FindObjectsByType<Transform>())
            {
                if (t != null && t.name.StartsWith("Hero (")) return t;
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

            // WO-301 — CATCH-UP TELEPORT: when the companion has been left far behind
            // (typically the village → OuterWorld seam, where the agent can't path to
            // the off-navmesh hero), snap to the hero's shoulder so it never strands.
            if (distHero > TeleportRange)
            {
                FlowTrace.Throttle("Companion", $"teleport-{GetInstanceID()}", 1f,
                    $"{DisplayName} ({_hero}) stranded {distHero:F0}m from hero (> {TeleportRange}m) — catch-up WarpTo (likely the OuterWorld seam)");
                WarpTo(target, self.y);
                FaceHero();
                return;
            }

            // WO-301 — choose the locomotion mode DYNAMICALLY each frame (not just once
            // at Start). Trust the NavMeshAgent only while we AND the hero are on the
            // baked mesh and within reach; otherwise fall through to the plain lerp so
            // the companion keeps following across the seam (mirrors PetHeroLeash, which
            // has no NavMesh dependency). This is the fix for the agent-locked-at-Start
            // stranding: the agent was kept enabled in the village even after the hero
            // walked off-navmesh into OuterWorld.
            bool useAgent =
                _agent != null && _agent.enabled && _agent.isOnNavMesh &&
                distHero <= AgentReachRange &&
                NavMesh.SamplePosition(heroPos, out _, 2.5f, NavMesh.AllAreas);

            if (useAgent)
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
                // Plain lerp fallback (off-navmesh / out of agent reach): glide toward
                // the trail point, keeping our own Y so we don't sink/float. We pause
                // the agent so it doesn't fight the transform move while we drive it
                // manually (it resumes automatically when reachability returns).
                if (_agent != null && _agent.enabled && _agent.isOnNavMesh)
                    _agent.isStopped = true;

                if (!tooClose)
                {
                    Vector3 flatTarget = new Vector3(target.x, self.y, target.z);
                    transform.position =
                        Vector3.MoveTowards(self, flatTarget, speed * Time.deltaTime);
                }
                FaceHero();
            }
        }

        /// <summary>
        /// WO-301 — snap the companion to <paramref name="target"/> (catch-up after
        /// stranding). Warps the NavMeshAgent when it can land on the mesh near the
        /// target (keeps it agent-valid), else moves the transform directly so the
        /// teleport still works off-navmesh (across the seam). Keeps <paramref
        /// name="keepY"/> so it doesn't sink/float when there's no mesh under it.
        /// </summary>
        private void WarpTo(Vector3 target, float keepY)
        {
            if (_agent != null && _agent.enabled && _agent.isOnNavMesh &&
                NavMesh.SamplePosition(target, out var hit, 4f, NavMesh.AllAreas))
            {
                _agent.Warp(hit.position);
            }
            else
            {
                transform.position = new Vector3(target.x, keepY, target.z);
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
            // WO-277: stay quiet while the tutorial owns the dialogue.
            if (_speechSuppressed) return;

            _speakTimer -= Time.deltaTime;
            if (_speakTimer > 0f) return;

            if (!_introSpoken)
            {
                _introSpoken = true;
                FlowTrace.Step("Companion", $"{DisplayName} ({_hero}) speaks INTRO line (speech beat reached)");
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
