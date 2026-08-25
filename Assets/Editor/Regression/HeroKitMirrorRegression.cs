// =============================================================================
// HeroKitMirrorRegression — pins the hero-select CARD KIT to the SHIPPED KIT.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression   Namespace: DeNelle.Editor.Regression
// Marker  : HERO_KIT_MIRROR_OK / HERO_KIT_MIRROR_FAIL   (suite tag [hero-kit-mirror])
//
// THE DEFECT THIS EXISTS FOR (owner ruling 2026-08-25, "fix the mage kit ... update
// truth to match source"): HeroCatalog.cs is a HAND-MIRROR of abilities.json — the
// Onboarding assembly references DeNelle.Core + DeNelle.Data only, so it cannot read
// AbilityCatalog (DeNelle.Village) and the four ability NAMES are re-typed in C#.
// A hand-mirror with no oracle drifts, and it had:
//   mage   advertised  Q Arcane Bolt / F Frost Nova / E Healing Beacon / R Meteor Strike
//          ships       Q Fireball    / W Arcane Shell / E Drain        / R Poison Cloud
//   ranger advertised  W as "F", and E "Mending Salve" (a KNIGHT-SKILLS pool entry)
//          ships       W Snare Trap  / E Healing Shot
//   knight advertised  W "Shield Charge"; ships "Shield Bash"
// i.e. the class-select screen sold kits the game does not have, and taught a slot
// letter ("F") the player's bar does not contain.
//
// WHAT IT ASSERTS — all of it read from abilities.json AT TEST TIME through the REAL
// consumer (AbilityCatalog.GetLoadout), never from a hardcoded expected-name list. A
// hardcoded list would be a THIRD copy of the same fact, which is the very failure
// mode being fixed here, and it would go green on a kit rename that broke the screen.
//   1. every advertised slot letter is one of Q / W / E / R (nothing else is a slot);
//   2. a class with an authored kit advertises exactly its four abilities, in Q,W,E,R
//      order, with the slot key AND the display name matching abilities.json verbatim;
//   3. the card's signature AbilityName is one of that class's four shipped names
//      (advertising a name the class cannot cast is the mage bug, exactly);
//   4. a class with NO authored kit (the Cleric — deliberately not playable) advertises
//      NO skills. Inventing a kit for it in C# fails here rather than on screen.
//
// WHAT MAKES IT GO RED: re-type any ability display name or slot letter in
// HeroCatalog.cs that abilities.json does not carry (e.g. put "Frost Nova" back on the
// mage's bar, or write "F" for a slot); reorder the four entries away from Q,W,E,R;
// drop or add a bar entry; point a card's signature at a pool/legacy ability; author
// skills for the Cleric. Equally: rename an ability in abilities.json WITHOUT updating
// the mirror — the drift is caught from either side, which is the point.
// NO HOLLOW PASS: if the catalog cannot be read, or fewer than 3 classes end up
// actually pinned, the suite FAILS instead of quietly asserting nothing.
//
// WHY REFLECTION (and why that is not the banned kind): DeNelle.EditorRegression does
// not reference DeNelle.Onboarding, and this suite is deliberately NOT licensed to
// widen an .asmdef. CLAUDE.md §10 bans new reflection in BRIDGE SCRIPTS — runtime
// cross-module calls — not in an editor-only oracle whose whole job is to read another
// assembly's static data. A missing type/field FAILS loudly here; it never silently
// skips. If the asmdef ever gains the reference, swap the two Read* helpers for direct
// typed access and delete nothing else.
// =============================================================================

using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using DeNelle.Village;

namespace DeNelle.Editor.Regression
{
    public static class HeroKitMirrorRegression
    {
        /// <summary>The only legal ability-bar slot keys. "F" is a learnable-pool key, not a slot.</summary>
        private static readonly string[] LegalSlots = { "Q", "W", "E", "R" };

        public static bool Run(out string report)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            int classesPinned = 0;
            int slotsPinned = 0;

            var catalogType = FindType("DeNelle.Onboarding.HeroCatalog");
            if (catalogType == null)
            {
                report = "HERO_KIT_MIRROR_FAIL: type DeNelle.Onboarding.HeroCatalog not found in any loaded assembly";
                return false;
            }

            var heroesField = catalogType.GetField("Heroes", BindingFlags.Public | BindingFlags.Static);
            var heroes = heroesField?.GetValue(null) as Array;
            if (heroes == null || heroes.Length == 0)
            {
                report = "HERO_KIT_MIRROR_FAIL: HeroCatalog.Heroes is missing, not an array, or empty";
                return false;
            }

            AbilityCatalog.Reload();   // read abilities.json as it is on disk right now

            foreach (var card in heroes)
            {
                if (card == null) { failures.Add("null HeroCardInfo entry in HeroCatalog.Heroes"); continue; }

                var cardType = card.GetType();
                object heroEnum = ReadField(cardType, card, "Hero");
                string cls = heroEnum == null ? null : heroEnum.ToString().ToLowerInvariant();
                if (string.IsNullOrEmpty(cls)) { failures.Add("HeroCardInfo with unreadable Hero class"); continue; }

                string advertisedSignature = ReadField(cardType, card, "AbilityName") as string;
                var advertised = ReadField(cardType, card, "PrimarySkills") as Array;
                int advertisedCount = advertised == null ? 0 : advertised.Length;

                // ---- what the game actually ships for this class ------------------
                var shipped = AbilityCatalog.GetLoadout(cls);
                int shippedCount = shipped == null ? 0 : shipped.Count;

                // ---- (1) every advertised slot letter must be a real slot ---------
                var advertisedNames = new List<string>(advertisedCount);
                for (int i = 0; i < advertisedCount; i++)
                {
                    var skill = advertised.GetValue(i);
                    if (skill == null) { failures.Add($"{cls}: null HeroSkillInfo at index {i}"); continue; }
                    var st = skill.GetType();
                    string slot = ReadField(st, skill, "Slot") as string;
                    string name = ReadField(st, skill, "Name") as string;
                    advertisedNames.Add(name ?? string.Empty);

                    if (Array.IndexOf(LegalSlots, slot) < 0)
                        failures.Add($"{cls}: advertises slot '{slot}' for '{name}' - the bar is Q/W/E/R only " +
                                     "(an F/other letter is a learnable-pool key, not a slot the player has)");
                }

                // ---- (4) a class with no authored kit advertises nothing ----------
                if (shippedCount == 0)
                {
                    if (advertisedCount > 0)
                        failures.Add($"{cls}: abilities.json authors NO kit for this class, yet the card advertises " +
                                     $"{advertisedCount} skill(s) - a kit invented in C# is not a kit the game can cast");
                    else
                        notes.Add($"{cls}: no authored kit in abilities.json and no advertised skills (deliberately not playable)");
                    continue;
                }

                // ---- (2) slot-by-slot verbatim mirror -----------------------------
                if (advertisedCount != shippedCount)
                {
                    failures.Add($"{cls}: card advertises {advertisedCount} skill(s), abilities.json ships {shippedCount}");
                }
                else
                {
                    for (int i = 0; i < shippedCount; i++)
                    {
                        var def = shipped[i];
                        var skill = advertised.GetValue(i);
                        if (def == null || skill == null) continue;
                        var st = skill.GetType();
                        string slot = ReadField(st, skill, "Slot") as string;
                        string name = ReadField(st, skill, "Name") as string;

                        string wantSlot = (def.Key ?? def.Slot ?? string.Empty).ToUpperInvariant();
                        if (!string.Equals(slot, wantSlot, StringComparison.Ordinal))
                            failures.Add($"{cls}[{i}]: card slot '{slot}' != abilities.json slot '{wantSlot}' (id {def.Id})");
                        if (!string.Equals(name, def.Name, StringComparison.Ordinal))
                            failures.Add($"{cls} slot {wantSlot}: card name '{name}' != shipped name '{def.Name}' (id {def.Id}) " +
                                         "- the hero-select screen is advertising an ability the game does not have");
                        slotsPinned++;
                    }
                }

                // ---- (3) the signature must be one of the four this class casts ---
                var shippedNames = new List<string>(shippedCount);
                for (int i = 0; i < shippedCount; i++) if (shipped[i] != null) shippedNames.Add(shipped[i].Name);
                if (string.IsNullOrEmpty(advertisedSignature))
                {
                    failures.Add($"{cls}: card has no signature AbilityName");
                }
                else if (!shippedNames.Contains(advertisedSignature))
                {
                    failures.Add($"{cls}: signature ability '{advertisedSignature}' is not one of the shipped kit " +
                                 $"[{string.Join(", ", shippedNames.ToArray())}]");
                }
                else
                {
                    // Convention (not a hard rule): the signature is the W-slot ability.
                    string wName = shippedCount > 1 && shipped[1] != null ? shipped[1].Name : null;
                    if (wName != null && !string.Equals(advertisedSignature, wName, StringComparison.Ordinal))
                        notes.Add($"{cls}: signature '{advertisedSignature}' is a real kit ability but not the W-slot " +
                                  $"'{wName}' (mage/knight/ranger convention) - deliberate?");
                }

                classesPinned++;
            }

            // ---- no hollow pass --------------------------------------------------
            if (classesPinned < 3)
                failures.Add($"only {classesPinned} class(es) were actually pinned (expected at least 3: mage/knight/ranger) " +
                             "- the suite would be asserting nothing");
            if (slotsPinned < 12)
                failures.Add($"only {slotsPinned} slot(s) were compared (expected at least 12: 3 classes x Q/W/E/R)");

            var sb = new StringBuilder();
            if (failures.Count > 0)
            {
                sb.Append("HERO_KIT_MIRROR_FAIL: ").Append(failures.Count).Append(" mismatch(es) between HeroCatalog and abilities.json");
                foreach (var f in failures) sb.Append("\n  - ").Append(f);
                report = sb.ToString();
                return false;
            }

            sb.Append("HERO_KIT_MIRROR_OK: ").Append(classesPinned).Append(" class(es), ")
              .Append(slotsPinned).Append(" slot(s) mirror abilities.json verbatim; all slots are Q/W/E/R");
            foreach (var n in notes) sb.Append("\n  note: ").Append(n);
            report = sb.ToString();
            return true;
        }

        /// <summary>Finds a type by full name across every loaded assembly (Onboarding is not referenced).</summary>
        private static Type FindType(string fullName)
        {
            foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type t = null;
                try { t = asm.GetType(fullName, false); } catch { }
                if (t != null) return t;
            }
            return null;
        }

        /// <summary>Reads a public instance field by name; returns null when absent.</summary>
        private static object ReadField(Type type, object instance, string field)
        {
            var f = type.GetField(field, BindingFlags.Public | BindingFlags.Instance);
            return f == null ? null : f.GetValue(instance);
        }
    }
}
