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

        /// <summary>
        /// Register a battle-active probe. The probe must return TRUE only while a
        /// battle (ATB combat session or Arena raid) is actively in progress, FALSE
        /// in hub / explore. Idempotent: the same delegate is added once.
        /// </summary>
        public static void RegisterProbe(Func<bool> probe)
        {
            if (probe == null) return;
            if (!_probes.Contains(probe)) _probes.Add(probe);
        }

        /// <summary>Remove a previously registered probe. Null-safe.</summary>
        public static void UnregisterProbe(Func<bool> probe)
        {
            if (probe == null) return;
            _probes.Remove(probe);
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
