// =============================================================================
// SupportFieldStructure - the GENERAL support/offensive-field structure. One class,
// stats off the catalog row, and TWO TAGS (an element and an effect) that re-skin it.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// ## WHAT WO-891 ACTUALLY ASKED FOR
//
// "A new structure = stats + two tags." The Healer is the first instance of the
// pattern, not a bespoke building: it copies range / fireRate / magnitude off
// entry.repo exactly the way DefenseTower does, runs a radius tick, and takes its
// whole VISUAL identity from an ELEMENT TAG. Adding a Slow-field, a damage-aura or a
// buffer is a new behaviorId, a new case in StructureFactory.AttachBehaviorImpl, and a
// different tag on this same component - and, on the VFX side, ZERO new code: the
// element table below already resolves all four, and every one of those VFXTypes is
// already catalogued today (verified at source in VFXCatalogGenerator.Map).
//
// HONEST LIMIT, stated rather than glossed: the VFX half is genuinely tag-only, but a
// Slow-field's SLOW is gameplay and would be a new arm of ResolveTick's effect switch.
// Only Heal is implemented today. The shape is there; the claim is not overstated.
//
// ## THE THREE BEATS (registry section 6f)
//
//   1. idle FIELD aura  - a held Family-A loop, the structure's resting identity.
//   2. per-tick CAST    - a burst at the structure that TELEGRAPHS AS CASTING. The
//                         structure visibly winds up, and that wind-up IS the warning;
//                         the effect lands CastLeadSeconds later. Registry section 1
//                         beat 3: "you watch it cast, and that is the warning." A tick
//                         that healed on the same frame it flashed would read as an
//                         instant silent heal, which is the thing WO-891 rules out.
//   3. CONTACT burst    - a burst on each unit the tick actually affected.
// Beats 2 and 3 are Family B one-shots: they cost no loop slot no matter how fast the
// structure ticks. Only beat 1 is a loop.
//
// ## LOOP DISCIPLINE (the part that costs a P0 when it is skipped)
//
// A loop played fire-and-forget permanently consumes one of VFXManager's 20 global
// slots. Six captured sessions showed that cap saturated. So:
//   * ONE handle field. There is no second field a second field-aura could live in.
//   * EVERY exit stops it: losing the nearest-first budget permit, OnDisable,
//     OnDestroy, sceneUnloaded, and a handle whose pooled instance died under us.
//   * NO WATCHDOG IS NEEDED, deliberately: this component is its own driver (it holds
//     the loop from its own Update), unlike the PUSH-driven HeroHpStateAura where a
//     silent driver is a leak. If this component stops updating it is because it was
//     disabled or destroyed - and both of those stop the loop.
//   * A STATIC BUDGET caps concurrent field auras (see MaxFieldAuras). A town that
//     places six healers must not spend six of the twenty slots on ambience.
// Worst case, one instance holds ONE loop; worst case across the game, MaxFieldAuras.
//
// POOLED-INSTANCE CONTAMINATION: VFXManager.ReturnToPool resets nothing it did not
// itself change, so every start re-baselines the pooled instance's modulation through
// VfxLoopModulator before seating its own scale - otherwise the next user of that pool
// slot inherits ours.
//
// LANDSCAPE PHONE (2670x1200): the field is seated LOW and WIDE on the ground plane
// rather than as a column. A tall effect spends the scarce axis and crops.
//
// COLOURBLIND (owner is red/green): the read is SHAPE + RHYTHM - a wide ground field
// that visibly pulses on a fixed cadence, then units flash - never a tint. The cast
// beat is a TIMING channel, which has no hue at all.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using DeNelle.Core.Catalog;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Village
{
    /// <summary>
    /// A structure that applies a support effect to allies inside a radius on a tick,
    /// presented as continuous casting. Element-tagged: the tag re-skins every beat.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class SupportFieldStructure : MonoBehaviour
    {
        /// <summary>
        /// The wheel element this structure is tagged with. It selects the field aura and
        /// the impact recipe and NOTHING else - that is the whole point of the pattern.
        /// Values and their mappings are verbatim from VFX_CREATIVE_PICKS_REGISTRY section 6f.
        /// </summary>
        public enum FieldElement
        {
            /// <summary>Healer. Rising shape. Aura_Healer + Impact_Heal.</summary>
            Holy,
            /// <summary>Slow-field. Angular outward drift. Aura_Ice + Impact_Ice.</summary>
            Ice,
            /// <summary>Buffer. Symmetric radial. Aura_EnemyCaster + Impact_Aether.</summary>
            Arcane,
            /// <summary>Damage-aura. Roiling miasma. Aura_Necromancer + Impact_ExplosionAether.</summary>
            Shadow,
        }

        /// <summary>What the tick DOES. Separate from the element on purpose: the element
        /// is presentation, the effect is gameplay, and conflating them is what would make
        /// "one more tag" a lie.</summary>
        public enum FieldEffect
        {
            /// <summary>Restore HP to allies in radius. The only arm implemented today.</summary>
            Heal,
        }

        // =====================================================================
        //  Stats - copied off entry.repo by StructureFactory, like DefenseTower
        // =====================================================================

        [Tooltip("Effect radius in world units (repo.range).")]
        public float Range = 8f;

        [Tooltip("Ticks per second (repo.fireRate). A support structure ticks slowly.")]
        public float FireRate = 0.5f;

        [Tooltip("Magnitude applied per tick per unit (repo.damage - a support structure's " +
                 "output shares the row's magnitude field rather than adding a parallel one).")]
        public float AmountPerTick = 6f;

        [Tooltip("Presentation tag. Re-skins every beat; changes no gameplay.")]
        public FieldElement Element = FieldElement.Holy;

        [Tooltip("Gameplay tag. Selects what the radius tick does.")]
        public FieldEffect Effect = FieldEffect.Heal;

        // =====================================================================
        //  TUNABLE BONES
        // =====================================================================

        /// <summary>
        /// How long the visible CAST leads the effect. This is the telegraph: long enough
        /// to read as a wind-up on a phone, short enough that the heal still feels caused
        /// by it. Always clamped below half the tick interval so a fast structure can
        /// never have two casts in flight.
        /// </summary>
        private const float CastLeadSeconds = 0.35f;

        /// <summary>
        /// Concurrent field auras allowed across the whole game, out of VFXManager's 20
        /// global slots. Nearest-first. Mirrors HarvestAura's budget for the same reason,
        /// and is deliberately a SEPARATE budget: hard-capping each domain is what makes
        /// the shared 20 predictable instead of first-come-first-served.
        /// </summary>
        private const int MaxFieldAuras = 3;

        private const float ArbitrateInterval = 0.5f;

        /// <summary>Beyond this the field aura is not worth a global slot.</summary>
        private const float MaxVisibleDistance = 55f;

        /// <summary>Retry throttle after a refused start (loop cap / quality gate / no manager).</summary>
        private const float StartRetrySeconds = 0.5f;

        /// <summary>
        /// The field is a GROUND-PLANE read, so it is seated at the structure's base and
        /// widened rather than raised. Landscape phone: a column crops, a disc does not.
        /// </summary>
        private const float FieldScaleMul = 1.35f;
        private const float FieldSeatHeight = 0.15f;

        /// <summary>Fallback cast height when the structure has no measurable renderer bounds.</summary>
        private const float DefaultCastHeight = 2.2f;

        /// <summary>Contact bursts are seated on the unit's chest, not its feet.</summary>
        private const float ContactHeight = 1.0f;

        /// <summary>Reused overlap buffer - a per-tick allocation on a structure that ticks
        /// forever is a slow leak, and this scan runs for every support structure in town.</summary>
        private const int OverlapBufferSize = 32;

        // =====================================================================
        //  Shared budget
        // =====================================================================

        private static readonly List<SupportFieldStructure> s_live   = new List<SupportFieldStructure>(8);
        private static readonly List<SupportFieldStructure> s_ranked = new List<SupportFieldStructure>(8);
        private static float s_nextArbitrate;

        /// <summary>Instances holding a pooled field loop right now (headless verification).</summary>
        public static int HoldingCount
        {
            get
            {
                int n = 0;
                for (int i = 0; i < s_live.Count; i++)
                    if (s_live[i] != null && s_live[i].IsHoldingField) n++;
                return n;
            }
        }

        // =====================================================================
        //  Instance state - ONE handle. There is deliberately no second field.
        // =====================================================================

        private VFXHandle _field;
        private bool      _permitted;
        private float     _nextStartAttempt;

        private float _tickTimer;
        private bool  _castInFlight;
        private float _castResolveAt;
        private float _castHeight = DefaultCastHeight;

        private readonly Collider[] _overlap = new Collider[OverlapBufferSize];

        /// <summary>True while a real pooled field loop is held.</summary>
        public bool IsHoldingField => _field != null && _field.IsAlive;

        /// <summary>Seconds between ticks, from <see cref="FireRate"/>. Never zero.</summary>
        public float TickInterval => 1f / Mathf.Max(0.05f, FireRate);

        // =====================================================================
        //  Catalog wiring
        // =====================================================================

        /// <summary>
        /// Copy stats off the catalog row, exactly the way DefenseTower's case does.
        /// Every field only overrides its default when the row authored something > 0, so
        /// a sparse row still produces a sensible structure instead of a dead one.
        /// Null-safe - a missing repo leaves the defaults intact.
        /// </summary>
        public void Configure(CatalogEntry entry, FieldElement element, FieldEffect effect)
        {
            Element = element;
            Effect  = effect;

            var r = entry != null ? entry.repo : null;
            if (r == null) return;
            if (r.range    > 0f) Range         = r.range;
            if (r.fireRate > 0f) FireRate      = r.fireRate;
            if (r.damage   > 0f) AmountPerTick = r.damage;

            FlowTrace.Step("Structure",
                $"SupportFieldStructure '{(entry != null ? entry.id : name)}' configured: " +
                $"element={Element} effect={Effect} range={Range:0.#} fireRate={FireRate:0.##}/s " +
                $"amount={AmountPerTick:0.#} -> aura '{AuraTypeFor(Element)}', impact '{ImpactTypeFor(Element)}'.");
        }

        // =====================================================================
        //  Lifecycle - every one of these is an EXIT PATH for the held loop.
        // =====================================================================

        private void OnEnable()
        {
            if (!s_live.Contains(this)) s_live.Add(this);
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            _tickTimer = TickInterval;   // do not fire a cast on the frame it is placed
        }

        private void OnDisable()
        {
            SceneManager.sceneUnloaded -= OnSceneUnloaded;
            s_live.Remove(this);
            _castInFlight = false;
            StopField(immediate: true, "OnDisable");
        }

        private void OnDestroy()
        {
            s_live.Remove(this);
            StopField(immediate: true, "OnDestroy");
        }

        private void OnSceneUnloaded(Scene _) => StopField(immediate: true, "sceneUnloaded");

        private void Start()
        {
            // Cast from the top of the structure so the wind-up reads as the BUILDING
            // doing it. Measured off the live renderer bounds (structures do not move),
            // with the flat fallback when there is nothing to measure.
            var rends = GetComponentsInChildren<Renderer>(true);
            bool have = false;
            Bounds b = default;
            for (int i = 0; i < rends.Length; i++)
            {
                var rr = rends[i];
                if (rr == null || rr is ParticleSystemRenderer) continue;
                if (!have) { b = rr.bounds; have = true; }
                else b.Encapsulate(rr.bounds);
            }
            if (have) _castHeight = Mathf.Clamp(b.max.y - transform.position.y + 0.3f, 0.5f, 8f);
        }

        private void Update()
        {
            Guard.Try("Structure", "support field tick", TickBeats);
            ArbitrateIfDue();
            ApplyField();
        }

        // =====================================================================
        //  Beats 2 + 3 - cast, then effect
        // =====================================================================

        private void TickBeats()
        {
            float interval = TickInterval;

            // Resolve a cast that is already in flight BEFORE arming the next one, so a
            // structure can never stack two.
            if (_castInFlight && Time.time >= _castResolveAt)
            {
                _castInFlight = false;
                ResolveTick();
            }

            _tickTimer -= Time.deltaTime;
            if (_tickTimer > 0f) return;
            _tickTimer = interval;

            if (_castInFlight) return;   // defensive: never two casts in flight

            // BEAT 2 - the visible wind-up AT the structure. Family B: no loop slot, no
            // handle, no stop path needed. Its whole job is that you SEE the building
            // cast before anything happens to anyone.
            VFXManager.Play(ImpactTypeFor(Element), transform.position + Vector3.up * _castHeight);

            _castInFlight  = true;
            // Clamped under half the interval so the cast always resolves inside its own
            // tick even if the row authored a fast fireRate.
            _castResolveAt = Time.time + Mathf.Min(CastLeadSeconds, interval * 0.5f);
        }

        /// <summary>
        /// The tick lands: apply the effect to every ally in radius and flash each one.
        /// Effect is chosen by the GAMEPLAY tag; the VFX by the ELEMENT tag. A second
        /// effect kind adds an arm here and needs no VFX code at all.
        /// </summary>
        private void ResolveTick()
        {
            int hit = Physics.OverlapSphereNonAlloc(
                transform.position, Range, _overlap, ~0, QueryTriggerInteraction.Ignore);
            if (hit >= OverlapBufferSize)
                FlowTrace.Throttle("Structure", "supportfield-overlap", 10f,
                    $"support field '{name}': overlap buffer full ({OverlapBufferSize}) - some units in " +
                    "radius were not served this tick. Raise OverlapBufferSize or shrink Range.");

            VFXType impact = ImpactTypeFor(Element);
            int served = 0;

            for (int i = 0; i < hit; i++)
            {
                var col = _overlap[i];
                if (col == null) continue;

                switch (Effect)
                {
                    case FieldEffect.Heal:
                        if (TryHeal(col, impact)) served++;
                        break;
                }
            }

            if (served > 0)
                FlowTrace.Throttle("Structure", "supportfield-tick", 2f,
                    $"support field '{name}' ({Element}/{Effect}): cast resolved, {served} unit(s) served " +
                    $"for {AmountPerTick:0.#} within {Range:0.#}m.");
        }

        /// <summary>
        /// Heal one collider's owner if it is a wounded, living ally. Returns true when
        /// something was actually restored - a full-HP ally is skipped so the field does
        /// not strobe contact bursts over a healthy party.
        /// <para>
        /// The HERO deliberately gets NO contact burst from here: HeroHealth.Heal already
        /// fires Impact_Heal itself (verified at source, HeroHealth.cs), so playing one
        /// here would double the burst on the one unit the player is looking at.
        /// </para>
        /// </summary>
        private bool TryHeal(Collider col, VFXType impact)
        {
            var hero = col.GetComponentInParent<HeroHealth>();
            if (hero != null)
            {
                if (hero.Hp <= 0f || hero.Fraction >= 1f) return false;
                hero.Heal(AmountPerTick);   // plays its own Impact_Heal - see the summary
                return true;
            }

            var troop = col.GetComponentInParent<TroopController>();
            if (troop != null)
            {
                if (!troop.IsAlive || troop.Hp >= troop.MaxHp) return false;
                troop.Heal(AmountPerTick);
                PlayContact(impact, troop.transform);
                return true;
            }

            var companion = col.GetComponentInParent<StoryCompanion>();
            if (companion != null)
            {
                if (!companion.IsAlive || companion.Hp >= companion.MaxHp) return false;
                companion.Heal(AmountPerTick);
                PlayContact(impact, companion.transform);
                return true;
            }

            return false;
        }

        /// <summary>BEAT 3 - the contact flash on a unit. Family B; costs no loop slot.</summary>
        private static void PlayContact(VFXType impact, Transform unit)
        {
            if (unit == null) return;
            VFXManager.Play(impact, unit.position + Vector3.up * ContactHeight);
        }

        // =====================================================================
        //  Beat 1 - the held field aura + its budget
        // =====================================================================

        private static void ArbitrateIfDue()
        {
            if (Time.time < s_nextArbitrate) return;
            s_nextArbitrate = Time.time + ArbitrateInterval;

            var cam = Camera.main;
            Vector3 eye = cam != null ? cam.transform.position : Vector3.zero;
            float maxSqr = MaxVisibleDistance * MaxVisibleDistance;

            s_ranked.Clear();
            for (int i = 0; i < s_live.Count; i++)
            {
                var s = s_live[i];
                if (s == null) continue;
                s._permitted = false;
                if (cam != null && (s.transform.position - eye).sqrMagnitude > maxSqr) continue;
                s_ranked.Add(s);
            }

            if (s_ranked.Count > 1)
                s_ranked.Sort((x, y) =>
                    (x.transform.position - eye).sqrMagnitude
                        .CompareTo((y.transform.position - eye).sqrMagnitude));

            int grant = Mathf.Min(s_ranked.Count, MaxFieldAuras);
            for (int i = 0; i < grant; i++) s_ranked[i]._permitted = true;

            if (s_ranked.Count > MaxFieldAuras)
                FlowTrace.Throttle("Structure", "supportfield-budget", 5f,
                    $"support field aura budget: {s_ranked.Count} structures in view, cap={MaxFieldAuras} " +
                    "(nearest kept). The gameplay tick is UNAFFECTED - only the idle field visual yields.");
        }

        private void ApplyField()
        {
            // A handle whose pooled instance died under us reads as not-alive: drop it so
            // we can re-acquire rather than sit on a corpse.
            if (_field != null && !_field.IsAlive) _field = null;

            if (!_permitted)
            {
                StopField(immediate: false, "budget permit lost");
                return;
            }
            if (_field != null) return;
            if (Time.time < _nextStartAttempt) return;

            var mgr = VFXManager.Instance;
            if (mgr == null) { _nextStartAttempt = Time.time + StartRetrySeconds; return; }

            VFXType type = AuraTypeFor(Element);
            _field = mgr.PlayAura(type, transform);
            if (_field == null)
            {
                _nextStartAttempt = Time.time + StartRetrySeconds;
                FlowTrace.Throttle("Structure", "supportfield-refused", 2f,
                    $"support field '{name}': PlayAura('{type}') REFUSED (global loop cap or quality gate) " +
                    "even though the field budget granted a permit. The tick still heals; retrying the visual.");
                return;
            }

            _field.SetPosition(transform.position + Vector3.up * FieldSeatHeight);

            // Re-baseline the pooled instance BEFORE seating our own scale - whatever its
            // previous owner modulated must not be inherited by this field.
            var mod = _field.Modulator;
            if (mod != null)
            {
                mod.SetEmissionScale(1f);
                mod.SetSimulationSpeed(1f);
                mod.SetScaleMul(FieldScaleMul);
            }

            FlowTrace.Step("Structure",
                $"support field '{name}' ({Element}): HELD '{type}' (one slot; " +
                $"{HoldingCount}/{MaxFieldAuras} field auras live, scaleMul={FieldScaleMul:0.00}).");
        }

        /// <summary>Stop and release THE held field loop. Idempotent; safe with nothing held.</summary>
        private void StopField(bool immediate, string reason)
        {
            if (_field == null) return;
            _field.Stop(immediate);   // Stop restores the instance's modulation before pooling
            _field = null;
            FlowTrace.Step("Structure",
                $"support field '{name}': released field loop (reason={reason}, immediate={immediate}) - slot returned.");
        }

        // =====================================================================
        //  THE ELEMENT TABLE - the whole "re-skin with one tag" claim, in one place
        // =====================================================================
        //
        // Verbatim from VFX_CREATIVE_PICKS_REGISTRY section 6f: "Healer=Holy .
        // Slow-field=Ice (Aura_Ice+Impact_Ice) . Damage-aura=Shadow (Aura_Necromancer+
        // Impact_ExplosionAether) . Buffer=Arcane (Aura_EnemyCaster+Impact_Aether)."
        // These are TRANSCRIBED owner-ratified picks, not choices made here.
        //
        // EVERY ONE of these eight VFXTypes already has a committed catalog row today
        // (verified at source in Assets/Editor/VFXCatalogGenerator.cs), so a second
        // element variant needs no new art, no builder row and no code in this file.
        // MEASURED for the Holy pair, off the real assets:
        //   Aura_Healer  -> Lana Regeneration/Regeneration_health_loop: 6 layers, ALL
        //                   looping, rateOverTime 15/25/7/1/5/5 -> CONTINUOUS, IsLoop=true
        //                   is correct. No repoint was needed and none was made.
        //   Impact_Heal  -> Lana Range_attack/Hit_heart: 6 layers, ALL non-looping,
        //                   rateOverTime 0 on every one -> BURST, IsLoop=false correct.
        // The family of each pair therefore MATCHES the beat it serves: the field is a
        // held loop, the cast and contact are fire-and-forget one-shots.

        private static VFXType AuraTypeFor(FieldElement e)
        {
            switch (e)
            {
                case FieldElement.Ice:    return VFXType.Aura_Ice;
                case FieldElement.Arcane: return VFXType.Aura_EnemyCaster;
                case FieldElement.Shadow: return VFXType.Aura_Necromancer;
                default:                  return VFXType.Aura_Healer;   // Holy
            }
        }

        private static VFXType ImpactTypeFor(FieldElement e)
        {
            switch (e)
            {
                case FieldElement.Ice:    return VFXType.Impact_Ice;
                case FieldElement.Arcane: return VFXType.Impact_Aether;
                case FieldElement.Shadow: return VFXType.Impact_ExplosionAether;
                default:                  return VFXType.Impact_Heal;   // Holy
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.86f, 0.45f, 0.3f);
            Gizmos.DrawWireSphere(transform.position, Range);
        }
#endif
    }
}
