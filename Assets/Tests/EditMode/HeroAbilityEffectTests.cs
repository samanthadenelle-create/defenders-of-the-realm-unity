// =============================================================================
// HeroAbilityEffectTests (EditMode) — WO-861 step 1.
// -----------------------------------------------------------------------------
// Locks the THREE new HeroAbilities.ResolveEffect shapes that WO-861 calls "the
// ONLY new combat code in the whole program", plus the A4 arrow-rider scoping gate:
//
//   drainshot — PINNED: the caster heals for the damage ACTUALLY DEALT (measured
//               across the target's own TakeDamage: post-resist AND clamped by the
//               target's remaining HP), never the ability's nominal damage number.
//   shield    — reduces incoming damage by the stated percent for the stated
//               duration, then EXPIRES. Routed through the SAME timed mitigation
//               window Warden's Grace writes (HeroAbilities.ApplyDamageShield) —
//               there is exactly ONE mitigation store, read via DamageTakenMultiplier.
//   manaweave — restores absolute mana over a window through the EXISTING WO3 mana
//               drip (RestoreManaOverTime -> _manaOverTimeRate/_manaOverTimeUntil),
//               and can never overfill the 0..10 pool.
//   ammoEffect rider — provably scoped to the RANGER's locked-Q basic attack; the
//               Knight and Mage basics can never carry it.
//
// LIFECYCLE: the project's EditMode pattern (new GameObject + AddComponent +
// DestroyImmediate in TearDown — see GearLoadoutUnequipTests). Update() never runs
// in EditMode, so every timed path is driven through the deterministic clock
// overloads (TickManaOverTime(now, dt) / DamageTakenMultiplierAt(now)) rather than
// Time.time advancing. No coroutine-backed branch (burn / poison DoT) is exercised
// here — coroutines do not run in EditMode; those need AutoPilot/PlayMode coverage.
//
// NO LogAssert.Expect is needed: none of the paths under test calls FlowTrace.Fail
// (which would Debug.LogError and fail the run). They emit Step/Warn/Once only,
// i.e. Debug.Log / Debug.LogWarning, neither of which fails a test.
// =============================================================================

using NUnit.Framework;
using UnityEngine;
using DeNelle.Core.Combat;
using DeNelle.Village;

namespace DeNelle.Tests.EditMode
{
    [TestFixture]
    public class HeroAbilityEffectTests
    {
        // A minimal IDamageable stand-in. `resist` models the target's own mitigation
        // (1.0 = takes full damage, 0.5 = halves it) so a test can prove the drain heal
        // is measured AFTER the target's math, not from the ability's nominal number.
        private sealed class FakeFoe : IDamageable
        {
            private float _hp;
            private readonly float _resist;

            public FakeFoe(float hp, float resist = 1f) { _hp = hp; _resist = resist; }

            public CombatFaction Faction => CombatFaction.Hostile;
            public Vector3 WorldPosition => Vector3.zero;
            public float Hp => _hp;
            public bool IsAlive => _hp > 0f;

            public int StatusCount;
            public StatusEffect LastStatus;
            public float LastStatusSeconds;

            public void TakeDamage(float amount, DamageElement element)
                => _hp = Mathf.Max(0f, _hp - amount * _resist);

            public void ApplyStatus(StatusEffect effect, float seconds)
            {
                StatusCount++;
                LastStatus = effect;
                LastStatusSeconds = seconds;
            }
        }

        private GameObject _go;
        private HeroAbilities _abilities;

        [SetUp]
        public void SetUp()
        {
            _go = new GameObject("HeroAbilities (test)");
            _abilities = _go.AddComponent<HeroAbilities>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_go != null) Object.DestroyImmediate(_go);
        }

        // =====================================================================
        //  drainshot — heal == damage DEALT (PINNED)
        // =====================================================================

        [Test]
        public void drainshot_heals_exactly_the_damage_dealt()
        {
            var foe = new FakeFoe(hp: 100f);

            float healed = _abilities.ApplyDrainshot(foe, 34f, DamageElement.Aether);

            Assert.That(healed, Is.EqualTo(34f).Within(0.0001f),
                "drainshot heal must equal the damage dealt");
            Assert.That(foe.Hp, Is.EqualTo(66f).Within(0.0001f),
                "the strike half must still land its damage");
        }

        [Test]
        public void drainshot_heal_is_clamped_to_remaining_hp_not_the_nominal_damage()
        {
            // 34 nominal damage into a 10 HP foe: only 10 HP is actually removed, so the
            // drain must heal 10. Healing 34 here is the exact bug WO-861 pins against.
            var foe = new FakeFoe(hp: 10f);

            float healed = _abilities.ApplyDrainshot(foe, 34f, DamageElement.Aether);

            Assert.That(healed, Is.EqualTo(10f).Within(0.0001f),
                "overkill must NOT heal the nominal 34 - only the 10 HP actually removed");
            Assert.That(healed, Is.Not.EqualTo(34f));
            Assert.That(foe.Hp, Is.EqualTo(0f));
        }

        [Test]
        public void drainshot_heal_is_post_mitigation_not_nominal()
        {
            // The target halves incoming damage. 34 nominal -> 17 actually dealt -> heal 17.
            var foe = new FakeFoe(hp: 100f, resist: 0.5f);

            float healed = _abilities.ApplyDrainshot(foe, 34f, DamageElement.Aether);

            Assert.That(healed, Is.EqualTo(17f).Within(0.0001f),
                "the heal must be measured AFTER the target's own mitigation");
            Assert.That(foe.Hp, Is.EqualTo(83f).Within(0.0001f));
        }

        [Test]
        public void drainshot_on_no_target_or_a_dead_target_heals_nothing()
        {
            Assert.That(_abilities.ApplyDrainshot(null, 34f, DamageElement.Aether), Is.EqualTo(0f),
                "no target -> no damage dealt -> no heal");

            var corpse = new FakeFoe(hp: 0f);
            Assert.That(_abilities.ApplyDrainshot(corpse, 34f, DamageElement.Aether), Is.EqualTo(0f),
                "a dead target deals no damage -> no heal");
        }

        [Test]
        public void measured_damage_helper_never_returns_negative()
        {
            // A target that somehow ends the call with MORE HP must not feed the drain.
            var foe = new FakeFoe(hp: 100f, resist: -1f);   // "heals" on hit

            float dealt = HeroAbilities.ApplyMeasuredDamage(foe, 34f, DamageElement.Aether);

            Assert.That(dealt, Is.EqualTo(0f), "damage dealt is clamped at 0 - never negative");
        }

        [Test]
        public void drainshot_heals_the_caster_by_exactly_the_dealt_amount()
        {
            // End-to-end through the real HeroHealth on the same rig. Skipped (not failed)
            // when the hero has no HP headroom for the heal to be observable.
            var hp = _go.AddComponent<HeroHealth>();
            float before = hp.Hp;
            float headroom = hp.MaxHp - before;
            Assume.That(headroom, Is.GreaterThanOrEqualTo(10f),
                "no HP headroom on this rig - the exact-heal assertion is unobservable");

            var foe = new FakeFoe(hp: 10f);
            float healed = _abilities.ApplyDrainshot(foe, 34f, DamageElement.Aether);

            Assert.That(healed, Is.EqualTo(10f).Within(0.0001f));
            Assert.That(hp.Hp - before, Is.EqualTo(10f).Within(0.001f),
                "the caster's HP must rise by exactly the damage dealt (10), not the nominal 34");
        }

        // =====================================================================
        //  shield — the Warden's Grace mitigation window, minus the heal
        // =====================================================================

        [Test]
        public void shield_is_off_by_default()
        {
            Assert.That(_abilities.DamageTakenMultiplierAt(Time.time), Is.EqualTo(1f),
                "with no shield up the hero takes unmitigated damage");
        }

        [Test]
        public void shield_reduces_incoming_damage_by_the_stated_pct_for_the_stated_duration_then_expires()
        {
            float t0 = Time.time;

            _abilities.ApplyDamageShield(40f, 4f);

            Assert.That(_abilities.DamageShieldUntil - t0, Is.EqualTo(4f).Within(0.01f),
                "the window must last the stated 4s");

            // -40% => a 100 damage hit lands as 60, for the whole window.
            Assert.That(100f * _abilities.DamageTakenMultiplierAt(t0 + 0.01f),
                Is.EqualTo(60f).Within(0.001f), "start of window: -40%");
            Assert.That(100f * _abilities.DamageTakenMultiplierAt(t0 + 2f),
                Is.EqualTo(60f).Within(0.001f), "mid window: -40%");
            Assert.That(100f * _abilities.DamageTakenMultiplierAt(t0 + 3.99f),
                Is.EqualTo(60f).Within(0.001f), "just before expiry: still -40%");

            // ... and then EXPIRES back to unmitigated.
            Assert.That(_abilities.DamageTakenMultiplierAt(t0 + 4.01f), Is.EqualTo(1f),
                "the shield must EXPIRE at the end of its window");
            Assert.That(_abilities.DamageTakenMultiplierAt(t0 + 60f), Is.EqualTo(1f));
        }

        [Test]
        public void shield_refresh_keeps_the_stronger_mitigation_and_the_later_expiry()
        {
            float t0 = Time.time;

            _abilities.ApplyDamageShield(40f, 4f);
            _abilities.ApplyDamageShield(10f, 1f);   // weaker AND shorter — must not win

            Assert.That(100f * _abilities.DamageTakenMultiplierAt(t0 + 0.5f),
                Is.EqualTo(60f).Within(0.001f), "a weaker re-cast must not weaken a live shield");
            Assert.That(_abilities.DamageShieldUntil - t0, Is.EqualTo(4f).Within(0.01f),
                "a shorter re-cast must not shorten a live shield");
        }

        [Test]
        public void shield_rejects_a_zero_or_negative_pct_or_duration()
        {
            float t0 = Time.time;

            _abilities.ApplyDamageShield(0f, 4f);
            _abilities.ApplyDamageShield(40f, 0f);
            _abilities.ApplyDamageShield(-40f, -4f);

            Assert.That(_abilities.DamageTakenMultiplierAt(t0 + 0.5f), Is.EqualTo(1f),
                "a malformed shield must not open a window");
        }

        [Test]
        public void shield_mitigation_is_capped_so_a_hero_can_never_be_fully_immune()
        {
            float t0 = Time.time;

            _abilities.ApplyDamageShield(500f, 4f);   // absurd authoring

            float mult = _abilities.DamageTakenMultiplierAt(t0 + 0.5f);
            Assert.That(mult, Is.GreaterThan(0f),
                "the mitigation window must never reach full immunity (invuln is a separate effect)");
        }

        // =====================================================================
        //  manaweave — the EXISTING mana-over-time drip
        // =====================================================================

        [Test]
        public void manaweave_opens_the_existing_drip_with_the_expected_rate_and_window()
        {
            float t0 = Time.time;

            _abilities.ApplyManaweave(5f, 3f);   // A4: "restore ~5 mana over 3s"

            Assert.That(_abilities.ManaOverTimeUntil - t0, Is.EqualTo(3f).Within(0.01f),
                "manaweave must open a 3s drip window");
            Assert.That(_abilities.ManaOverTimeRate, Is.EqualTo(5f / 3f).Within(0.001f),
                "rate must deliver 5 mana across the 3s window");
            Assert.That(_abilities.ManaOverTimeRate * 3f, Is.EqualTo(5f).Within(0.001f));
        }

        [Test]
        public void manaweave_delivers_the_expected_mana_over_the_expected_window()
        {
            float t0 = Time.time;
            float before = _abilities.Mana;
            Assume.That(_abilities.MaxMana - before, Is.GreaterThan(5.5f),
                "no mana headroom on this rig - the delivery assertion is unobservable");

            _abilities.ApplyManaweave(5f, 3f);

            // 30 deterministic 0.1s frames inside the window.
            for (int i = 0; i < 30; i++) _abilities.TickManaOverTime(t0 + i * 0.1f, 0.1f);

            Assert.That(_abilities.Mana - before, Is.EqualTo(5f).Within(0.05f),
                "the drip must deliver 5 mana across the 3s window");

            // Past the window the drip is inert.
            float atEnd = _abilities.Mana;
            _abilities.TickManaOverTime(t0 + 5f, 0.5f);
            Assert.That(_abilities.Mana, Is.EqualTo(atEnd).Within(0.0001f),
                "the drip must stop once the window closes");
        }

        [Test]
        public void manaweave_never_exceeds_the_mana_cap()
        {
            float t0 = Time.time;

            _abilities.ApplyManaweave(50f, 1f);   // five times the whole pool
            _abilities.TickManaOverTime(t0 + 0.5f, 1f);

            Assert.That(_abilities.Mana, Is.EqualTo(_abilities.MaxMana).Within(0.0001f),
                "mana must saturate at the cap");
            Assert.That(_abilities.Mana, Is.LessThanOrEqualTo(_abilities.MaxMana));
            Assert.That(_abilities.ManaOverTimeRate, Is.EqualTo(0f),
                "the drip must close the moment the pool caps (no lingering overfill)");
        }

        [Test]
        public void mana_drip_step_is_hard_capped_at_max()
        {
            // The pure step every drip caller runs — the cap lives here, so no caller can overfill.
            Assert.That(HeroAbilities.StepManaOverTime(9.5f, 10f, 50f, 1f), Is.EqualTo(10f),
                "a huge step must clamp to the cap, not to 59.5");
            Assert.That(HeroAbilities.StepManaOverTime(0f, 10f, 5f / 3f, 0.1f),
                Is.EqualTo(5f / 30f).Within(0.0001f), "an in-window step is rate * dt");
            Assert.That(HeroAbilities.StepManaOverTime(10f, 10f, 5f, 1f), Is.EqualTo(10f),
                "already full stays full");
        }

        [Test]
        public void manaweave_rejects_a_zero_or_negative_amount_or_window()
        {
            _abilities.ApplyManaweave(0f, 3f);
            _abilities.ApplyManaweave(5f, 0f);
            _abilities.ApplyManaweave(-5f, -3f);

            Assert.That(_abilities.ManaOverTimeRate, Is.EqualTo(0f),
                "a malformed manaweave must not open a drip");
        }

        // =====================================================================
        //  A4 arrow rider — scoping
        // =====================================================================

        [Test]
        public void arrow_rider_is_eligible_for_the_rangers_basic_attack()
        {
            // Positive control: without this, the "not eligible" tests below would pass
            // trivially on an always-false gate.
            var rangerQ = AbilityCatalog.Find("ranger", AbilitySlot.Q);
            Assume.That(rangerQ, Is.Not.Null, "abilities.json ranger loadout absent in this env");

            _abilities.SetHeroClass("ranger");

            Assert.That(_abilities.IsArrowRiderEligible(rangerQ), Is.True,
                "the Ranger's locked Q basic attack IS the arrow-using shot");
        }

        [Test]
        public void arrow_rider_does_not_fire_for_a_non_ranger_basic_attack()
        {
            var knightQ = AbilityCatalog.Find("knight", AbilitySlot.Q);
            var mageQ   = AbilityCatalog.Find("mage",   AbilitySlot.Q);
            Assume.That(knightQ, Is.Not.Null);
            Assume.That(mageQ,   Is.Not.Null);

            _abilities.SetHeroClass("knight");
            Assert.That(_abilities.IsArrowRiderEligible(knightQ), Is.False,
                "the Knight's basic attack must never carry an arrow rider");

            _abilities.SetHeroClass("mage");
            Assert.That(_abilities.IsArrowRiderEligible(mageQ), Is.False,
                "the Mage's basic attack must never carry an arrow rider");

            // ... and a Ranger holding someone else's def still doesn't qualify.
            _abilities.SetHeroClass("ranger");
            Assert.That(_abilities.IsArrowRiderEligible(knightQ), Is.False);
            Assert.That(_abilities.IsArrowRiderEligible(mageQ), Is.False);
        }

        [Test]
        public void arrow_rider_does_not_fire_for_the_rangers_non_basic_abilities()
        {
            _abilities.SetHeroClass("ranger");

            foreach (var slot in new[] { AbilitySlot.W, AbilitySlot.E, AbilitySlot.R })
            {
                var def = AbilityCatalog.Find("ranger", slot);
                Assume.That(def, Is.Not.Null);
                Assert.That(_abilities.IsArrowRiderEligible(def), Is.False,
                    $"the arrow rider must not ride the Ranger's {slot} slot");
            }
        }

        [Test]
        public void arrow_rider_is_never_eligible_for_a_null_ability()
        {
            _abilities.SetHeroClass("ranger");
            Assert.That(_abilities.IsArrowRiderEligible(null), Is.False);
        }

        [Test]
        public void ammo_rider_slow_reuses_the_existing_status_primitive()
        {
            // Only the non-coroutine branch is exercised: burn/poison start coroutines,
            // which do not run in EditMode (they need AutoPilot / PlayMode coverage).
            var foe = new FakeFoe(hp: 100f);

            _abilities.ApplyAmmoRider(foe, "frost", dps: 0f, seconds: 2.5f, slowPct: 35f);

            Assert.That(foe.StatusCount, Is.EqualTo(1), "the frost rider applies exactly one status");
            Assert.That(foe.LastStatus, Is.EqualTo(StatusEffect.Slow),
                "the frost rider must reuse the existing Slow primitive, not a new system");
            Assert.That(foe.LastStatusSeconds, Is.EqualTo(2.5f).Within(0.0001f));
        }

        [Test]
        public void ammo_rider_ignores_an_unknown_or_empty_effect_and_a_dead_target()
        {
            var foe = new FakeFoe(hp: 100f);
            _abilities.ApplyAmmoRider(foe, null,       0f, 2f, 0f);
            _abilities.ApplyAmmoRider(foe, "",         0f, 2f, 0f);
            _abilities.ApplyAmmoRider(foe, "sparkles", 0f, 2f, 0f);
            Assert.That(foe.StatusCount, Is.EqualTo(0), "an unknown ammoEffect must apply nothing");

            var corpse = new FakeFoe(hp: 0f);
            _abilities.ApplyAmmoRider(corpse, "frost", 0f, 2f, 0f);
            Assert.That(corpse.StatusCount, Is.EqualTo(0), "a dead target takes no rider");
        }
    }
}
