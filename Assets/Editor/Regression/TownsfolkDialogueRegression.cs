// =============================================================================
// TownsfolkDialogueRegression — headless "real object in -> assert" gate for the
// ambient townsfolk dialogue table (Assets/_Modules/Village/NPCs/TownsfolkDialogue).
// -----------------------------------------------------------------------------
// The SECOND first-ever oracle for the village-npcs area. Pure data/logic (NO
// PlayMode, NO GameObjects) so it runs inside DataRegression.RunAll. Mirrors the
// MonetizationCovenantRegression contract: public static bool Run(out string reason);
// true = pass + summary, false = fail + detail. Never throws.
//
// INVARIANTS ASSERTED (the ambient-NPC flavour table AmbientNPC speaks through):
//   1. ArchetypeCount == 9 — the enum is STABLE 0..8 (Trader..Farmer). AmbientNPC
//      serializes Archetype BY VALUE, so a renumber/drop silently repoints saved NPCs.
//   2. NAME COVERAGE — NameFor(Farmer) (the highest enum value, 8) must NOT fall to
//      the "Villager" fallback, which it does when the _names array is too short. This
//      is the concrete "index-out-of-coverage" regression guard for the display-name table.
//   3. Every archetype: NameFor non-empty; PoolFor non-null AND non-empty; and every
//      line in the pool is non-empty/non-whitespace (no blank bubble line).
//   4. LineFor(archetype, index) never returns null across a wide index range
//      (incl. negative + past-length) — the modulo "never throws / never null" contract.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Text;
using DeNelle.Village;

namespace DeNelle.Editor
{
    /// <summary>
    /// Data/logic regression for TownsfolkDialogue: stable archetype count, full
    /// name coverage, non-empty pools/lines, and the never-null LineFor contract.
    /// Returns true (summary) / false (detail).
    /// </summary>
    public static class TownsfolkDialogueRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();

            // --- 1) stable archetype count (0..8 = 9) ---
            int count = TownsfolkDialogue.ArchetypeCount;
            if (count != 9)
                failures.Add($"ArchetypeCount is {count}, expected 9 (Trader..Farmer, 0..8) — the enum was renumbered/extended; " +
                             "AmbientNPC serializes Archetype by value, so saved NPCs would repoint.");

            // --- 2) name coverage: the top archetype must not hit the 'Villager' fallback ---
            // NameFor returns "Villager" as its out-of-range fallback; Farmer (8) SHOULD map to
            // "Goodman Harrow". If the _names array is short, NameFor(Farmer) == "Villager" — this
            // catches exactly that coverage regression without hard-coding the exact string.
            string farmerName = TownsfolkDialogue.NameFor(TownsfolkDialogue.Archetype.Farmer);
            if (farmerName == "Villager")
                failures.Add("NameFor(Farmer) fell back to 'Villager' — the display-name array does not cover the full archetype range.");

            // --- 3) per-archetype: name non-empty, pool non-empty, every line non-empty ---
            foreach (TownsfolkDialogue.Archetype arch in Enum.GetValues(typeof(TownsfolkDialogue.Archetype)))
            {
                string name = TownsfolkDialogue.NameFor(arch);
                if (string.IsNullOrWhiteSpace(name))
                    failures.Add($"NameFor({arch}) is blank — the speech-bubble attribution would be empty.");

                string[] pool = TownsfolkDialogue.PoolFor(arch);
                if (pool == null || pool.Length == 0)
                {
                    failures.Add($"PoolFor({arch}) is empty — that archetype has no ambient line to speak.");
                    continue;
                }
                for (int i = 0; i < pool.Length; i++)
                    if (string.IsNullOrWhiteSpace(pool[i]))
                        failures.Add($"PoolFor({arch})[{i}] is blank — a bubble would show an empty line.");
            }

            // --- 4) LineFor never null across a wide index range (modulo contract) ---
            foreach (TownsfolkDialogue.Archetype arch in Enum.GetValues(typeof(TownsfolkDialogue.Archetype)))
            {
                for (int i = -3; i <= 12; i++)
                {
                    string line = TownsfolkDialogue.LineFor(arch, i);
                    if (line == null)
                    {
                        failures.Add($"LineFor({arch}, {i}) returned null — modulo/never-throws contract broken.");
                        break;
                    }
                }
            }

            if (failures.Count == 0)
            {
                reason = $"TOWNSFOLK DIALOGUE OK — {count} archetypes: names cover the full range, " +
                         "every pool non-empty with no blank lines, LineFor never null.";
                return true;
            }

            var sb = new StringBuilder();
            sb.Append($"TOWNSFOLK DIALOGUE FAIL: {failures.Count} issue(s):");
            foreach (var f in failures) sb.Append("\n  - ").Append(f);
            reason = sb.ToString();
            return false;
        }
    }
}
