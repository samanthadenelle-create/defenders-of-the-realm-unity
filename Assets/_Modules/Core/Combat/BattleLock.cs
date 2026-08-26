// =============================================================================
// BattleLock — single source of truth for "is an ATB/Arena battle active now?"
// (WO-437 input/state discipline).
// -----------------------------------------------------------------------------
// THE PROBLEM (WO-437): the base loop had no input/state discipline. Gameplay
// panels (shop, crafting, talents, etc.) could open mid-battle and global hotkeys
// popped panels regardless of context. To enforce the battle-lock we need ONE
// reliable, assembly-neutral predicate that any system can read:
//   - PanelManager (Core) gates panel opens against it.
//   - Panel bootstraps (HUD/Village) gate their dev hotkeys against it.
//   - The Yarn command bridges (Village) no-op panel verbs when it is true.
//
// THE FIX: a thin static in DeNelle.Core (referenced by every gameplay module).
// The battle-owning code lives in assemblies that reference Core (DeNelle.BattleATB
// for ATB combat, DeNelle.Village for Arena raids), so Core CANNOT reference back.
// Instead each battle owner REGISTERS a predicate (Func<bool>) here; IsInBattle()
// returns true if ANY registered predicate reports active. Mirrors the CoreServices
// register/unregister pattern. Pure static state, reset on domain reload.
//
//   ATBCombatManager  registers  () => IsActive          (combat session running)
//   ArenaMode         registers  () => RaidInProgress     (raid in flight)
//
// NOTE: named "BattleLock" (not "BattleState") deliberately — DeNelle.BattleATB.Engine
// already owns a per-battle snapshot type called BattleState, and BattleController
// imports both namespaces. A distinct name avoids the CS0104 ambiguity.
// =============================================================================

using System;
using System.Collections.Generic;

namespace DeNelle.Core.Combat
{
    /// <summary>
    /// Assembly-neutral battle-active predicate (the WO-437 battle-lock). Battle
    /// owners register a probe (a <see cref="Func{Boolean}"/>) when they come alive
    /// and unregister on teardown; <see cref="IsInBattle"/> is true while ANY
    /// registered probe reports a battle in progress. Safe to call from any
    /// assembly that references DeNelle.Core.
    /// </summary>
    public static class BattleLock
    {
        private static readonly List<Func<bool>> _probes = new List<Func<bool>>();

        // WO-1233 ATTRIBUTION. ⚠ THE REGISTRATION CONTRACT IS UNCHANGED — RegisterProbe and
        // UnregisterProbe keep their exact one-argument signatures and their exact semantics, and
        // NO call site was touched, because other systems read this contract. The label is DERIVED
        // from the delegate itself (see DescribeDelegate), so attribution costs the callers nothing.
        //
        // WHY: on 2026-08-26 the quiescence gate reported "battle-lock: still HELD" NINE times and
        // could not say WHO held it, because this list is an anonymous OR over N owners. Nine
        // captures, zero attribution, and a whole ticket spent deducing the holder from a HUD line
        // in a neighbouring log. A lock with N writers and no name for any of them is the bug that
        // made the other bug expensive.
        private static readonly List<string> _labels = new List<string>();

        /// <summary>
        /// Register a battle-active probe. The probe must return TRUE only while a
        /// battle (ATB combat session or Arena raid) is actively in progress, FALSE
        /// in hub / explore. Idempotent: the same delegate is added once.
        /// </summary>
        public static void RegisterProbe(Func<bool> probe)
        {
            if (probe == null) return;
            if (_probes.Contains(probe)) return;
            _probes.Add(probe);
            _labels.Add(DescribeDelegate(probe));
        }

        /// <summary>Remove a previously registered probe. Null-safe.</summary>
        public static void UnregisterProbe(Func<bool> probe)
        {
            if (probe == null) return;
            int i = _probes.IndexOf(probe);
            if (i < 0) return;
            _probes.RemoveAt(i);
            if (i < _labels.Count) _labels.RemoveAt(i);
        }

        /// <summary>
        /// Names of the probes currently reporting a battle in progress, comma-separated, or
        /// "none". This is the line that turns "the lock is stuck" into "the lock is held by
        /// PursuitBattleProbe" without a second capture. A probe that throws is skipped, exactly
        /// as <see cref="IsInBattle"/> skips it, so the two can never disagree.
        /// </summary>
        public static string DescribeHolders()
        {
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < _probes.Count; i++)
            {
                var probe = _probes[i];
                if (probe == null) continue;
                bool held = false;
                try { held = probe.Invoke(); }
                catch { /* mirrors IsInBattle: a faulty probe is not a holder */ }
                if (!held) continue;
                if (sb.Length > 0) sb.Append(", ");
                sb.Append(i < _labels.Count ? _labels[i] : "unnamed-probe");
            }
            return sb.Length == 0 ? "none" : sb.ToString();
        }

        /// <summary>Every registered probe's label, held or not (boot/diagnostic read).</summary>
        public static string DescribeAll()
        {
            if (_labels.Count == 0) return "none registered";
            return string.Join(", ", _labels);
        }

        /// <summary>Number of registered probes (regression + diagnostics).</summary>
        public static int ProbeCount => _probes.Count;

        /// <summary>
        /// Derive a human label from the delegate. Lambdas compile into a nested closure type
        /// (<c>Owner+&lt;&gt;c__DisplayClass12_0</c>), so walk out to the real declaring type —
        /// that is what turns an anonymous entry into "BattleArena.&lt;Awake&gt;b__0".
        /// </summary>
        private static string DescribeDelegate(Func<bool> probe)
        {
            try
            {
                var method = probe.Method;
                var type = method.DeclaringType;
                while (type != null && type.DeclaringType != null &&
                       (type.Name.IndexOf("c__", StringComparison.Ordinal) >= 0 ||
                        type.Name.IndexOf('<') >= 0))
                    type = type.DeclaringType;
                return (type != null ? type.Name : "unknown") + "." + method.Name;
            }
            catch { return "unnamed-probe"; }
        }

        /// <summary>
        /// TRUE while any registered battle is active (ATB combat or Arena raid),
        /// FALSE in hub / explore. A probe that throws is treated as "not in battle"
        /// (and skipped) so one bad owner can't wedge the whole gate.
        /// </summary>
        public static bool IsInBattle()
        {
            for (int i = 0; i < _probes.Count; i++)
            {
                var probe = _probes[i];
                if (probe == null) continue;
                try { if (probe.Invoke()) return true; }
                catch { /* a faulty probe never blocks input — treat as not-in-battle */ }
            }
            return false;
        }
    }
}
