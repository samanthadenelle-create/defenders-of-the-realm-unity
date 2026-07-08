// =============================================================================
// CompanionRosterRegression — headless "real object in -> assert" gate for the
// village-npcs companion roster (the recruited party-of-4 data + logic).
// -----------------------------------------------------------------------------
// FIRST oracle coverage for Assets/_Modules/Village/NPCs/ (the area had ZERO before).
// Pure data/logic — NO PlayMode, NO GameObjects — so it runs inside the headless
// DataRegression.RunAll batch gate. Mirrors the MonetizationCovenantRegression
// contract exactly: public static bool Run(out string reason); true = pass + a
// one-line summary, false = fail + the offending detail. Never throws.
//
// INVARIANTS ASSERTED (the roster canon in CLAUDE.md §7 + memory
// combat-pivot / tripo-roster-knight-orcs-first):
//   1. Companion NEVER mirrors the hero — CompanionSpawner.CompanionClassFor(player)
//      != player for every class (the "comrade from another walk of life" rule).
//   2. The mapping is a BIJECTION over the 4 classes (4 distinct companion classes),
//      so a full party can never hold two of one class via the map.
//   3. CompanionDialogue.NameFor is non-empty AND unique across the 4 companions
//      (the speech-bubble attribution + HUD party label source).
//   4. CompanionDialogue.IntroFor is non-empty for every class (first spoken line).
//   5. CompanionDialogue.LineFor never returns null across a wide index range
//      (incl. negative + past-length) — the modulo contract that "never throws".
//   6. CompanionGearSetup.GrantFor returns a complete grant (weapon id/label +
//      armor id/label all non-empty, weapon id != armor id) for every class — the
//      wave-3 gear-up beat's data (WO-364).
// =============================================================================

using System.Collections.Generic;
using System.Text;
using DeNelle.Core.State;
using DeNelle.Village;

namespace DeNelle.Editor
{
    /// <summary>
    /// Data/logic regression for the companion roster: mapping (companion != hero,
    /// bijective), per-class dialogue names/intros, and the gear-up grant table.
    /// Real static game code in, asserted out. Returns true (summary) / false (detail).
    /// </summary>
    public static class CompanionRosterRegression
    {
        // The playable / companion classes in play (V1 roster). HeroClass may carry more
        // members, but these four are the canon companion set (Grom/Sylas/Thrain/Elara).
        private static readonly HeroClass[] Classes =
        {
            HeroClass.Knight, HeroClass.Ranger, HeroClass.Mage, HeroClass.Cleric,
        };

        public static bool Run(out string reason)
        {
            var failures = new List<string>();

            // --- 1 + 2) mapping: companion != hero, and the 4 map to 4 distinct classes ---
            var mapped = new HashSet<HeroClass>();
            foreach (var player in Classes)
            {
                HeroClass companion = CompanionSpawner.CompanionClassFor(player);
                if (companion == player)
                    failures.Add($"CompanionClassFor({player}) returned the SAME class — companion must never mirror the hero.");
                mapped.Add(companion);
            }
            if (mapped.Count != Classes.Length)
                failures.Add($"CompanionClassFor is not a bijection over the {Classes.Length} classes " +
                             $"(only {mapped.Count} distinct companion classes) — a full party could hold a duplicate class.");

            // --- 3) NameFor non-empty + unique across the roster ---
            var names = new Dictionary<string, HeroClass>();
            foreach (var cls in Classes)
            {
                string name = CompanionDialogue.NameFor(cls);
                if (string.IsNullOrWhiteSpace(name))
                {
                    failures.Add($"CompanionDialogue.NameFor({cls}) is blank — HUD party label + bubble attribution would be empty.");
                    continue;
                }
                if (names.TryGetValue(name, out var other))
                    failures.Add($"CompanionDialogue.NameFor collision: {cls} and {other} both return '{name}'.");
                else
                    names[name] = cls;
            }

            // --- 4) IntroFor non-empty for every class ---
            foreach (var cls in Classes)
            {
                string intro = CompanionDialogue.IntroFor(cls);
                if (string.IsNullOrWhiteSpace(intro))
                    failures.Add($"CompanionDialogue.IntroFor({cls}) is blank — the companion's first line would be empty.");
            }

            // --- 5) LineFor never null across a wide index range (modulo contract) ---
            foreach (var cls in Classes)
            {
                for (int i = -3; i <= 12; i++)
                {
                    string line = CompanionDialogue.LineFor(cls, i);
                    if (line == null)
                    {
                        failures.Add($"CompanionDialogue.LineFor({cls}, {i}) returned null — modulo/never-throws contract broken.");
                        break;
                    }
                }
            }

            // --- 6) GrantFor complete for every class ---
            foreach (var cls in Classes)
            {
                CompanionGearSetup.GearGrant g = CompanionGearSetup.GrantFor(cls);
                if (string.IsNullOrWhiteSpace(g.WeaponId))
                    failures.Add($"CompanionGearSetup.GrantFor({cls}).WeaponId is blank — gear-up beat can't equip a weapon.");
                if (string.IsNullOrWhiteSpace(g.ArmorId))
                    failures.Add($"CompanionGearSetup.GrantFor({cls}).ArmorId is blank — gear-up beat can't equip armor.");
                if (string.IsNullOrWhiteSpace(g.WeaponLabel))
                    failures.Add($"CompanionGearSetup.GrantFor({cls}).WeaponLabel is blank — gear toast/dialogue would show nothing.");
                if (string.IsNullOrWhiteSpace(g.ArmorLabel))
                    failures.Add($"CompanionGearSetup.GrantFor({cls}).ArmorLabel is blank — gear toast/dialogue would show nothing.");
                if (!string.IsNullOrEmpty(g.WeaponId) && g.WeaponId == g.ArmorId)
                    failures.Add($"CompanionGearSetup.GrantFor({cls}) uses the same id '{g.WeaponId}' for weapon AND armor — a mapping slip.");
            }

            if (failures.Count == 0)
            {
                reason = $"COMPANION ROSTER OK — {Classes.Length} classes: mapping is bijective + never mirrors the hero, " +
                         $"names unique, intros/lines non-null, gear grants complete.";
                return true;
            }

            var sb = new StringBuilder();
            sb.Append($"COMPANION ROSTER FAIL: {failures.Count} issue(s):");
            foreach (var f in failures) sb.Append("\n  - ").Append(f);
            reason = sb.ToString();
            return false;
        }
    }
}
