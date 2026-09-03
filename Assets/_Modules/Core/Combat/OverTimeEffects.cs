// =============================================================================
// OverTimeEffects - THE ONE over-time engine (WO-1330).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Combat
//
// -----------------------------------------------------------------------------
// WHY THIS FILE EXISTS. Owner ruling 2026-09-02, verbatim:
//     "we have a ton of blink art and spells, have creative match a DoT would be
//      nice" / "or a regen for knight" / "lots of room to interprut"
// and, correcting the CLI's first reading of the ticket:
//     "it doesnt but it wouldnt be too challenging"
//
// -----------------------------------------------------------------------------
// WHAT WAS ACTUALLY TRUE AT SOURCE, because it is neither what the CLI first
// reported NOR quite what the correction assumed - and the difference is the
// whole design:
//
//   * The CLI's first read said "the DoT already exists" and pointed at
//     DeNelle.BattleATB. WRONG in the way that matters: BattleATB is the
//     SUPERSEDED turn-based engine and the shipping game cannot cast into it.
//
//   * The owner's correction said the mechanic "doesnt" exist. Very nearly
//     right, and right about the thing that counts - but not literally: the
//     LIVE real-time path (HeroAbilities.ResolveEffect) already dispatches
//     "dot" and "healOverTime". What it did NOT have was ONE mechanism. It had
//     THREE unrelated ad-hoc tick loops:
//         1. HeroAbilities.BurnDoT      - a coroutine, hardcoded 1s tick
//         2. HeroAbilities.PoisonDoT    - a SECOND coroutine, hardcoded 1s tick,
//                                         byte-for-byte the same loop
//         3. the _hpOverTime window     - a per-FRAME continuous drip in Update
//     ...none of them tunable, and no mage ability able to reach any of them.
//
//   * Assets/_Modules/Core/Combat/CombatStatusTracker.cs IS live (EnemyDamageable
//     and HeroCombatStatus each own one) - but it is a HUD TIMER BAG. It stores
//     when a status ENDS. It has no magnitude, no tick and no sink, so it can
//     record that a foe is burning and can never make the burn hurt. It is the
//     right home for the ROW and was never a candidate for the TICK. This file
//     does not replace it; the two are used together, exactly as before.
//
// So this file is the ONE mechanism the ticket asked for. Loops 1 and 2 above
// now run on it (behaviour preserved tick-for-tick, see PULSE ARITHMETIC), and
// the two new abilities are the same mechanism with the sign flipped.
//
// -----------------------------------------------------------------------------
// WHY IT IS PURE, AND WHY THAT IS NOT DECORATION.
// No MonoBehaviour, no coroutine, no UnityEngine.Time - the clock is a parameter
// (Advance(now)). That is copied deliberately from the precedent already in this
// repo: HeroAbilities.TickManaOverTime was extracted for exactly this reason,
// with the comment "so the drip is unit-testable with an explicit clock (EditMode
// never runs Update)". An over-time effect whose ticking cannot be OBSERVED by a
// gate is an over-time effect nobody can prove ticks - and CLAUDE.md section 12
// forbids shipping on that basis. OverTimeEffectRegression drives this type with
// a fake clock and counts the pulses.
//
// -----------------------------------------------------------------------------
// ONE MECHANISM, BOTH SIGNS. The engine is generic over the target type so that
// the SAME code serves "damage a foe" and "heal the hero" without a second
// implementation - two closed generic types, one body. Magnitude is always a
// POSITIVE quantity and the direction travels in OverTimeKind, so no call site
// can ever heal by passing a negative damage (the classic sign bug).
//
// ASCII only. FlowTrace tag "OverTime". Never strip it (CLAUDE.md section 12).
// =============================================================================

using System;
using System.Collections.Generic;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.Ops;

namespace DeNelle.Core.Combat
{
    /// <summary>Which way an over-time pulse moves a health bar.</summary>
    public enum OverTimeKind
    {
        /// <summary>The pulse REMOVES health from the target (a DoT).</summary>
        Damage = 0,

        /// <summary>The pulse RESTORES health to the target (a regen / HoT).</summary>
        Heal = 1,
    }

    /// <summary>
    /// The balance rail for every over-time effect (WO-1330).
    /// <para>
    /// ⛔ EVERY VALUE HERE IS ON THE PROD-022 TUNABLE RAIL and NOT a hardcoded
    /// constant, per the standing rule of 2026-09-02 and the owner's ruling that
    /// produced it ("be smart, dont make it need a code change, make it tweakable
    /// from a db call"). Tick magnitude, tick interval and duration are the three
    /// levers the ticket named, and they are THREE SHARED knobs rather than six
    /// per-ability ones because the quantity is genuinely the same concept on both
    /// signs - see the "prefer ONE shared knob" line in the work order.
    /// </para>
    /// <para>
    /// ⭐ EVERY DEFAULT IS TODAY'S BEHAVIOUR. 1000 ms is precisely the
    /// <c>const float tick = 1f</c> that BurnDoT and PoisonDoT each hardcoded, and
    /// the two percent knobs are identity at 100. An empty client_tunables table,
    /// a 404, an offline player and a malformed row therefore all produce the exact
    /// numbers that shipped before this file existed.
    /// </para>
    /// </summary>
    public static class OverTimeTuning
    {
        /// <summary>FlowTrace system tag for the whole over-time lane.</summary>
        public const string Sys = "OverTime";

        /// <summary>Milliseconds between pulses. 1000 = today (the old <c>const float tick = 1f</c>).</summary>
        public const int TickMsDefault = 1000;

        /// <summary>Percent scale on every pulse's magnitude. 100 = today.</summary>
        public const int MagnitudePctDefault = 100;

        /// <summary>Percent scale on every effect's duration. 100 = today.</summary>
        public const int DurationPctDefault = 100;

        // ---------------------------------------------------------------------
        // CLAMPS. AUTHORED FRESH for this ticket and flagged as such in the
        // RESULT - no precedent existed for these three. They mirror the shape of
        // the WO-1306 drain clamp (0..1000) and exist because each has a value
        // that would BREAK the engine rather than merely mis-balance it:
        //   * a tick of 0 or less is a divide-by-zero and an unbounded pulse
        //     count in one frame;
        //   * a tick above the ceiling silently never fires, which is
        //     indistinguishable from a broken ability;
        //   * a negative percent would flip a DoT into a heal and a regen into
        //     damage, which is the one outcome the OverTimeKind design exists to
        //     make impossible.
        // A clamped value is TRACED, never silently swallowed.
        // ---------------------------------------------------------------------

        /// <summary>Fastest permitted pulse cadence, milliseconds.</summary>
        public const int TickMsMin = 50;

        /// <summary>Slowest permitted pulse cadence, milliseconds.</summary>
        public const int TickMsMax = 60000;

        /// <summary>Floor for both percent knobs (0 = the effect does nothing, which is legal).</summary>
        public const int PctMin = 0;

        /// <summary>Ceiling for both percent knobs (10x).</summary>
        public const int PctMax = 1000;

        /// <summary>Resolved seconds between pulses. Never 0, never negative.</summary>
        public static float TickSeconds
            => ClampInt(RemoteTunables.Int(RemoteTunables.KeyCombatOverTimeTickMs),
                        TickMsMin, TickMsMax, RemoteTunables.KeyCombatOverTimeTickMs) / 1000f;

        /// <summary>Resolved magnitude multiplier. 1.0 = today.</summary>
        public static float MagnitudeScale
            => ClampInt(RemoteTunables.Int(RemoteTunables.KeyCombatOverTimeMagnitudePct),
                        PctMin, PctMax, RemoteTunables.KeyCombatOverTimeMagnitudePct) / 100f;

        /// <summary>Resolved duration multiplier. 1.0 = today.</summary>
        public static float DurationScale
            => ClampInt(RemoteTunables.Int(RemoteTunables.KeyCombatOverTimeDurationPct),
                        PctMin, PctMax, RemoteTunables.KeyCombatOverTimeDurationPct) / 100f;

        private static int ClampInt(int raw, int lo, int hi, string key)
        {
            if (raw < lo)
            {
                FlowTrace.Throttle(Sys, "clamp-lo:" + key, 30f,
                    "tunable '" + key + "' resolved to " + raw + ", which is below the floor " + lo +
                    " - CLAMPED to " + lo + ". Nothing is broken; fix the row when convenient.");
                return lo;
            }
            if (raw > hi)
            {
                FlowTrace.Throttle(Sys, "clamp-hi:" + key, 30f,
                    "tunable '" + key + "' resolved to " + raw + ", which is above the ceiling " + hi +
                    " - CLAMPED to " + hi + ". Nothing is broken; fix the row when convenient.");
                return hi;
            }
            return raw;
        }
    }

    /// <summary>
    /// One over-time effect in flight, as reported to the caller's sink on each pulse.
    /// </summary>
    /// <typeparam name="TTarget">Whatever the caller applies the effect to.</typeparam>
    public readonly struct OverTimePulse<TTarget> where TTarget : class
    {
        /// <summary>Who the pulse lands on.</summary>
        public readonly TTarget Target;

        /// <summary>Stable effect id, e.g. "burn" or "knight.ironblood". Trace + HUD key.</summary>
        public readonly string Id;

        /// <summary>How much health this single pulse moves. ALWAYS POSITIVE - direction lives in <see cref="Kind"/>.</summary>
        public readonly float Amount;

        /// <summary>Damage or Heal.</summary>
        public readonly OverTimeKind Kind;

        /// <summary>1-based index of this pulse within the effect.</summary>
        public readonly int Index;

        /// <summary>How many pulses the effect will deliver in total.</summary>
        public readonly int TotalPulses;

        /// <summary>True on the last pulse of the effect.</summary>
        public bool IsFinal => Index >= TotalPulses;

        internal OverTimePulse(TTarget target, string id, float amount, OverTimeKind kind, int index, int total)
        {
            Target = target;
            Id = id ?? string.Empty;
            Amount = amount;
            Kind = kind;
            Index = index;
            TotalPulses = total;
        }
    }

    /// <summary>
    /// THE over-time engine. Applies, ticks and expires health-over-time effects of
    /// EITHER sign against an explicit clock.
    /// <para>
    /// It owns TIMING AND ARITHMETIC ONLY. It never touches a health bar itself -
    /// the caller supplies a sink and remains the single owner of its own damage /
    /// heal seam (HeroAbilities keeps <c>IDamageable.TakeDamage</c> and
    /// <c>HeroHealth.RegenTick</c>; nothing here bypasses mitigation, attribution
    /// or the death check). It likewise never touches the HUD:
    /// <see cref="CombatStatusTracker"/> still owns the buff/debuff ROW and is
    /// applied by the caller exactly as before.
    /// </para>
    /// <para>
    /// Not thread-safe; drive it from the caller's Update.
    /// </para>
    /// </summary>
    /// <typeparam name="TTarget">Foe type, hero-health type - one body serves both.</typeparam>
    public sealed class OverTimeEngine<TTarget> where TTarget : class
    {
        // ---------------------------------------------------------------------
        //  PULSE ARITHMETIC - and it is a DELIBERATE REPRODUCTION, not a redesign.
        //
        //  The loop this replaces was, verbatim:
        //
        //      float elapsed = 0f;  const float tick = 1f;
        //      while (elapsed < duration) {
        //          yield return new WaitForSeconds(tick);
        //          elapsed += tick;
        //          if (target == null || !target.IsAlive) yield break;
        //          target.TakeDamage(dps * tick, element);
        //      }
        //
        //  Three properties of it are load-bearing and are preserved EXACTLY:
        //   1. The FIRST pulse lands one full interval AFTER application, never on
        //      the cast frame. (An ability that dealt its impact damage AND a burn
        //      pulse on the same frame would silently gain a tick of damage.)
        //   2. The pulse count is CEIL(duration / interval), not floor and not
        //      round: the old loop's test ran BEFORE the increment, so a 4.5s
        //      duration at a 1s tick delivered FIVE pulses and slightly
        //      over-delivered. Rounding that "cleanly" to 4 would be a stealth
        //      nerf to two shipped abilities, so ceil it stays.
        //   3. Magnitude per pulse is perSecond * interval, so total delivery is
        //      INVARIANT under the tick knob: halving the interval doubles the
        //      pulse count and halves each pulse. Moving the cadence is a FEEL
        //      lever, never a damage lever - which is what makes it safe to hand
        //      to the owner.
        // ---------------------------------------------------------------------

        private sealed class Entry
        {
            public TTarget Target;
            public string Id;
            public float AmountPerPulse;
            public float IntervalSeconds;
            public float NextAt;
            public int Delivered;
            public int TotalPulses;
            public OverTimeKind Kind;
        }

        private readonly List<Entry> _entries = new List<Entry>(8);

        // ---------------------------------------------------------------------
        //  LIVENESS IS A CONSTRUCTOR ARGUMENT, NOT AN OPTIONAL PARAMETER.
        //
        //  It was `Advance(now, onPulse, Func<TTarget,bool> isAlive = null)` for
        //  exactly one afternoon, and OverTimeEffectRegression's [death] case shot
        //  it: an Advance called WITHOUT the argument ticked a corpse (3 pulses
        //  where 2 were due) and leaked the dead entry (ActiveCount 1, not 0).
        //
        //  The two shipping call sites in HeroAbilities.TickOverTimeEffects both
        //  passed it and were correct. THAT IS THE POINT - the hazard was never the
        //  code that exists, it was the NEXT call site, written by a seat who did
        //  not know the argument was load-bearing and got a compiler that did not
        //  care. "A DoT must never hit a corpse" is an invariant of the ENGINE, so
        //  it is now impossible to construct one without saying how to test it: no
        //  default, no null, no overload that omits it. Forgetting was made
        //  IMPOSSIBLE rather than merely REMEMBERED, which is why the oracle was
        //  fixed at the engine and not weakened at the assertion.
        // ---------------------------------------------------------------------
        private readonly Func<TTarget, bool> _isAlive;

        /// <summary>
        /// The ONLY constructor. <paramref name="isAlive"/> is REQUIRED - see the block
        /// above. Pass the same null-tolerant test the call site uses everywhere else,
        /// e.g. <c>t =&gt; t != null &amp;&amp; t.IsAlive</c>.
        /// </summary>
        /// <exception cref="ArgumentNullException">If no liveness test is supplied.</exception>
        public OverTimeEngine(Func<TTarget, bool> isAlive)
        {
            if (isAlive == null)
                throw new ArgumentNullException(nameof(isAlive),
                    "OverTimeEngine requires a liveness test at construction. Without one the " +
                    "engine ticks corpses and leaks an entry per dead target - see the [death] " +
                    "case in OverTimeEffectRegression. Pass 't => t != null && t.IsAlive'.");
            _isAlive = isAlive;
        }

        /// <summary>Effects currently in flight.</summary>
        public int ActiveCount => _entries.Count;

        /// <summary>Pulses this engine has delivered since construction. Diagnostics only.</summary>
        public int PulsesDelivered { get; private set; }

        /// <summary>
        /// How many pulses an effect of <paramref name="durationSeconds"/> delivers at
        /// <paramref name="intervalSeconds"/>. CEIL, for the reason set out above.
        /// Exposed so an oracle can assert the count without reaching into the engine.
        /// </summary>
        public static int PulseCountFor(float durationSeconds, float intervalSeconds)
        {
            if (durationSeconds <= 0f || intervalSeconds <= 0f) return 0;
            // Epsilon so an exact multiple (4.0 / 1.0) is 4 and not 5 on float slop.
            double raw = durationSeconds / (double)intervalSeconds;
            int n = (int)Math.Ceiling(raw - 1e-4);
            return n < 1 ? 1 : n;
        }

        /// <summary>
        /// Start an over-time effect on <paramref name="target"/>.
        /// <para>
        /// The three tunables are resolved HERE, at application time, and then frozen
        /// onto the entry. That is the same property WO-1306 gave the drain knob: it is
        /// not a boot-time value, so a flip reaches a running client on the ordinary
        /// ~40s path and takes effect on the next cast - while an effect ALREADY in
        /// flight keeps the cadence it was cast with, so nothing in flight ever changes
        /// shape underneath the player.
        /// </para>
        /// </summary>
        /// <param name="target">The thing being burned or mended. Null is a no-op.</param>
        /// <param name="id">Stable id for trace + HUD. Never null-checked away silently.</param>
        /// <param name="perSecond">Magnitude PER SECOND, positive. Direction is <paramref name="kind"/>.</param>
        /// <param name="durationSeconds">How long the effect runs, before the duration knob.</param>
        /// <param name="kind">Damage or Heal.</param>
        /// <param name="now">The caller's clock reading.</param>
        /// <param name="maxStacks">
        /// Concurrent copies of this id allowed on this target. The default of 0 means
        /// UNLIMITED, which is what the shipped BurnDoT did (every cast started another
        /// coroutine) - so folding that loop in here changed nothing.
        /// </param>
        /// <returns>Pulses this effect will deliver, or 0 if it was refused.</returns>
        public int Apply(TTarget target, string id, float perSecond, float durationSeconds,
                         OverTimeKind kind, float now, int maxStacks = 0)
        {
            if (target == null) return 0;

            if (perSecond <= 0f || durationSeconds <= 0f)
            {
                // Not an exception - authored data can legitimately be zero while a
                // designer is mid-tune. But it is never SILENT (CLAUDE.md section 12):
                // "the effect did nothing" and "the effect was never applied" must not
                // look the same in a capture.
                FlowTrace.Throttle(OverTimeTuning.Sys, "inert:" + id, 5f,
                    "over-time effect '" + id + "' was applied with perSecond=" + perSecond.ToString("0.##") +
                    " duration=" + durationSeconds.ToString("0.##") + "s - nothing will tick. This is the " +
                    "authored data, not a fault: check the ability row.");
                return 0;
            }

            float interval = OverTimeTuning.TickSeconds;
            float duration = durationSeconds * OverTimeTuning.DurationScale;
            float magnitude = perSecond * OverTimeTuning.MagnitudeScale;

            if (duration <= 0f || magnitude <= 0f)
            {
                FlowTrace.Throttle(OverTimeTuning.Sys, "scaled-inert:" + id, 5f,
                    "over-time effect '" + id + "' was scaled to nothing by the tunable rail " +
                    "(magnitudeScale=" + OverTimeTuning.MagnitudeScale.ToString("0.##") +
                    " durationScale=" + OverTimeTuning.DurationScale.ToString("0.##") + "). " +
                    "A knob is at 0 - clear the row to restore the shipping default.");
                return 0;
            }

            if (maxStacks > 0 && CountOf(target, id) >= maxStacks)
            {
                FlowTrace.Throttle(OverTimeTuning.Sys, "capped:" + id, 1f,
                    "over-time effect '" + id + "' is already at its cap of " + maxStacks +
                    " stack(s) on this target - the new application adds nothing and refreshes nothing.");
                return 0;
            }

            int pulses = PulseCountFor(duration, interval);
            _entries.Add(new Entry
            {
                Target = target,
                Id = id ?? string.Empty,
                AmountPerPulse = magnitude * interval,
                IntervalSeconds = interval,
                NextAt = now + interval,      // property (1): never on the cast frame
                Delivered = 0,
                TotalPulses = pulses,
                Kind = kind,
            });

            FlowTrace.Step(OverTimeTuning.Sys,
                (kind == OverTimeKind.Heal ? "HEAL" : "DAMAGE") + "-over-time '" + id + "' applied: " +
                magnitude.ToString("0.##") + "/s for " + duration.ToString("0.##") + "s = " +
                pulses + " pulse(s) of " + (magnitude * interval).ToString("0.##") +
                " every " + interval.ToString("0.###") + "s (total " +
                (magnitude * interval * pulses).ToString("0.##") + ").");

            return pulses;
        }

        /// <summary>Concurrent effects with this id on this target.</summary>
        public int CountOf(TTarget target, string id)
        {
            int n = 0;
            for (int i = 0; i < _entries.Count; i++)
                if (ReferenceEquals(_entries[i].Target, target) &&
                    string.Equals(_entries[i].Id, id, StringComparison.Ordinal)) n++;
            return n;
        }

        /// <summary>Drop every effect on <paramref name="target"/>. Returns how many were dropped.</summary>
        public int CancelAll(TTarget target)
        {
            int removed = 0;
            for (int i = _entries.Count - 1; i >= 0; i--)
                if (ReferenceEquals(_entries[i].Target, target)) { _entries.RemoveAt(i); removed++; }
            return removed;
        }

        /// <summary>Drop everything. Used at scene teardown and by the oracle between cases.</summary>
        public void Clear() => _entries.Clear();

        /// <summary>
        /// Advance the clock to <paramref name="now"/> and deliver every pulse that has
        /// come due, oldest first.
        /// </summary>
        /// <param name="now">The caller's clock reading. Monotonic.</param>
        /// <param name="onPulse">Sink. The caller's own damage / heal seam.</param>
        /// <returns>Pulses delivered by this call.</returns>
        /// <remarks>
        /// The liveness test given at construction is consulted UNCONDITIONALLY: an entry
        /// whose target fails it is dropped WITHOUT pulsing - reproducing the shipped loop's
        /// <c>if (!target.IsAlive) yield break;</c> so a DoT can never hit a corpse - and it
        /// is re-checked between pulses of the same frame, because a pulse can kill its own
        /// target. There is deliberately no way for a caller to skip it.
        /// </remarks>
        public int Advance(float now, Action<OverTimePulse<TTarget>> onPulse)
        {
            if (_entries.Count == 0) return 0;
            int fired = 0;

            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                var e = _entries[i];

                if (e.Target == null || !_isAlive(e.Target))
                {
                    _entries.RemoveAt(i);
                    continue;
                }

                // A long frame (or a resumed session) can owe several pulses at once.
                // Bounded by TotalPulses by construction, so this can never spin.
                while (e.Delivered < e.TotalPulses && now >= e.NextAt)
                {
                    e.Delivered++;
                    e.NextAt += e.IntervalSeconds;
                    fired++;
                    PulsesDelivered++;

                    if (onPulse != null)
                    {
                        var pulse = new OverTimePulse<TTarget>(
                            e.Target, e.Id, e.AmountPerPulse, e.Kind, e.Delivered, e.TotalPulses);
                        // Guarded: one throwing sink must never strand every OTHER effect
                        // in the engine mid-tick (CLAUDE.md section 12, no silent failures).
                        Guard.Try(OverTimeTuning.Sys, "pulse:" + e.Id, () => onPulse(pulse));
                    }

                    // Re-check liveness between pulses of the SAME frame: the pulse we
                    // just delivered may itself have killed the target.
                    if (!_isAlive(e.Target)) { e.Delivered = e.TotalPulses; break; }
                }

                if (e.Delivered >= e.TotalPulses)
                {
                    FlowTrace.Step(OverTimeTuning.Sys,
                        "over-time effect '" + e.Id + "' EXPIRED after " + e.Delivered + "/" +
                        e.TotalPulses + " pulse(s).");
                    _entries.RemoveAt(i);
                }
            }

            return fired;
        }
    }
}
