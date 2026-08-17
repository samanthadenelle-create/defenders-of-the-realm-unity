// =============================================================================
// ForgeShelfClassKindRegression [forge-shelf-kind]
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core + DeNelle.Village).
// Markers: FORGE_SHELF_OK / FORGE_SHELF_FAIL.
//
// Pins the three defects docs/reference/WEAPON_CATALOG.md (2026-08-16) proved from
// the live catalog, all three of which are SILENT - compile-green, log-green, and
// only visible to a player standing at the shelf or looking in the bag:
//
//   1 [class-kind-shelf]  Every Forge shelf a playable class can stand at contains
//                         at least one weapon of THAT CLASS'S OWN KIND (knight ->
//                         sword, ranger -> bow, mage -> staff). Before the fix a
//                         level-1 Mage's shelf was `blink_shield1h_04` +
//                         `blink_shield1h_05` - two shields, no staff - because
//                         every job:"any" shield carries damageMult 1.0, ties the
//                         level-1 staves and wins the id-ordinal tiebreak; the
//                         Knight's sword band lost the same tie to axes at every
//                         tier. This case fails on a shelf that is empty, on a
//                         shelf with no class-kind row, and on ZERO shelves
//                         evaluated (a resolver that returns nothing must not read
//                         as a pass).
//
//   2 [weapon-icon-kind]  No weapon row resolves an icon of a DIFFERENT weapon
//                         kind. The founding case: `knight_shield_starter`
//                         ("Squire's Heater") painted a SWORD, because
//                         ItemIconCatalog.ForWeapon had no shield branch at all -
//                         the shield keywords lived only in ForArmor, which never
//                         sees a weapons.json row. A null sprite is always allowed
//                         (the caller paints the row's glyph: an honest blank beats
//                         another item's art). KnownIconKindDebt below is a NAMED,
//                         SHRINKING baseline of the mismatches that need AUTHORED
//                         iconPath rows or a new sheet, not a code fix - it is a
//                         ratchet: a NEW mismatch fails.
//
//   3 [forge-exclusion]   The Forge's excludeIdPrefixes matches the Armorer's, OR
//                         the Forge row carries an authored note saying why it does
//                         not. It does not (the 65 blink_ weapons were deliberately
//                         unhidden on 2026-08-14 after the WO-500 curve was
//                         ratified, while blink ARMOR stays junked), so the note is
//                         the artifact this case requires - an undocumented
//                         asymmetry is indistinguishable from the WO-860 miss it
//                         looks like. Also pins the two vendors.json copies
//                         byte-identical (modulo line endings).
//
// The oracle deliberately re-derives weapon KIND itself instead of calling the
// resolver's private helper, so it can DISAGREE with the code under test rather
// than inherit its bug.
//
// Standalone: run-unity-method.ps1
//   -Method DeNelle.Editor.Regression.ForgeShelfClassKindRegression.RunAll
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Village;
using DeNelle.Village.Hero;

namespace DeNelle.Editor.Regression
{
    public static class ForgeShelfClassKindRegression
    {
        private const string VendorsRes = "Assets/Resources/Data/Canonical/vendors.json";
        private const string VendorsSA = "Assets/StreamingAssets/Data/Canonical/vendors.json";
        private const string ForgeId = "forge";
        private const string ArmorerId = "armorer";
        private const string NoteField = "_excludeIdPrefixesNote";

        // Pinned roster: the suite must not depend on ff.knightonly / PlayableHeroes drift.
        private static readonly string[] Roster = { "knight", "ranger", "mage" };
        private static readonly int[] Levels = { 1, 3, 6, 10 };

        /// <summary>class -> the weapon kind that class actually wields (weapons.json ladders).</summary>
        private static readonly Dictionary<string, string> PrimaryKind =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                { "knight", "sword" },
                { "ranger", "bow" },
                { "mage",   "staff" },
            };

        // ---------------------------------------------------------------------
        //  CASE 2 baseline - mismatches that need AUTHORED ART, not a code change.
        //  SHRINK THIS LIST. Every entry is a row whose true kind has no sheet:
        //    * the four ranger_arrow_* rows are AMMO and paint a BOW (there is no
        //      arrow sheet, and ranger_arrow_plain is additionally the Ranger
        //      starter that StarterLoadout never seeds - the open WO-861 gap).
        //    * cleric_starter is a MACE and paints a sword (no mace/hammer sheet).
        //    * tripo_axe_a is an AXE and paints a sword ("axe" is routed into the
        //      sword sheet as the generic melee silhouette).
        //  The fix for all six is an authored iconPath on the row (the 76 tripo_*/
        //  blink_* rows already sidestep the mapper that way), which is a CONTENT
        //  decision for the owner - not something this lane invents.
        // ---------------------------------------------------------------------
        private static readonly HashSet<string> KnownIconKindDebt =
            new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "ranger_arrow_plain",
                "ranger_arrow_fire",
                "ranger_arrow_poison",
                "ranger_arrow_frost",
                "cleric_starter",
                "tripo_axe_a",
            };

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("FORGE_SHELF_OK - " + reason);
            else Debug.LogError("FORGE_SHELF_FAIL: " + reason);
        }

        /// <summary>Covenant contract (DataRegression-shaped). Never throws.</summary>
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();
            try
            {
                GearCatalog.Reload();
                VendorRegistry.Reload();
                Case1_ClassKindShelf(failures, notes);
                Case2_WeaponIconKind(failures, notes);
                Case3_ForgeExclusion(failures, notes);
            }
            catch (Exception ex)
            {
                failures.Add($"[forge-shelf-kind] unexpected {ex.GetType().Name}: {ex.Message}");
            }

            if (failures.Count > 0) { reason = string.Join(" | ", failures); return false; }
            reason = "forge shelf + weapon icons verified (" + string.Join("; ", notes) + ")";
            return true;
        }

        // =====================================================================
        //  CASE 1 - every class sees its own weapon kind on the shelf
        // =====================================================================
        private static void Case1_ClassKindShelf(List<string> failures, List<string> notes)
        {
            int shelvesEvaluated = 0;
            int promotedTiers = 0;

            foreach (var pair in PrimaryKind)
            {
                string job = pair.Key;
                string want = pair.Value;
                foreach (int level in Levels)
                {
                    var wares = VendorStockResolver.Resolve(ForgeId, job, level, Roster);
                    if (wares == null)
                    {
                        failures.Add($"[class-kind-shelf] Resolve('{ForgeId}', {job}, {level}) returned null");
                        continue;
                    }

                    var ids = new List<string>();
                    var kinds = new List<string>();
                    bool found = false;
                    foreach (var ware in wares)
                    {
                        if (ware.Kind != VendorWareKind.Weapon) continue;
                        var w = GearCatalog.FindWeapon(ware.Id);
                        string k = KindOf(w);
                        ids.Add(ware.Id);
                        kinds.Add(k.Length == 0 ? "?" : k);
                        if (string.Equals(k, want, StringComparison.OrdinalIgnoreCase)) found = true;
                    }

                    shelvesEvaluated++;

                    if (ids.Count == 0)
                    {
                        failures.Add($"[class-kind-shelf] the {ForgeId} shelf for a Lv{level} {job} is EMPTY - " +
                                     "a weapon vendor with no weapon on it cannot be tested and is not a pass");
                        continue;
                    }
                    if (!found)
                    {
                        failures.Add($"[class-kind-shelf] Lv{level} {job}: shelf is [{string.Join(",", ids)}] " +
                                     $"(kinds: {string.Join(",", kinds)}) - NOT ONE is a '{want}', the weapon " +
                                     $"a {job} actually wields. The per-level cap keeps the top rows by damageMult " +
                                     "DESC then id ORDINAL ASC; on a tie that ordinal hands the slots to shields/" +
                                     "axes and caps the class band out. Fix the SORT (the reserved class-kind slot " +
                                     "in VendorStockResolver.EmitCapped), never damageMult or rarity");
                    }
                    else if (level == 1)
                    {
                        promotedTiers++;
                    }
                }
            }

            if (shelvesEvaluated == 0)
                failures.Add("[class-kind-shelf] ZERO shelves evaluated - the vendor/catalog seam produced nothing " +
                             "to assert on, which is a FAIL, not a skip (this case exists to catch an empty shelf)");
            notes.Add($"shelves checked = {shelvesEvaluated}, Lv1 shelves carrying the class kind = {promotedTiers}");
        }

        // =====================================================================
        //  CASE 2 - a weapon never wears another kind's picture
        // =====================================================================
        private static void Case2_WeaponIconKind(List<string> failures, List<string> notes)
        {
            // Hollow-pass guard: if the sliced sheets are not indexed, EVERY row resolves
            // null and the case would "pass" while asserting nothing.
            var shieldSheet = Resources.LoadAll<Sprite>("ItemIcons/WRdWM");
            var swordSheet = Resources.LoadAll<Sprite>("ItemIcons/Ud37F");
            if (shieldSheet == null || shieldSheet.Length == 0 || swordSheet == null || swordSheet.Length == 0)
            {
                failures.Add("[weapon-icon-kind] the item-icon sheets are not indexed under Resources/ItemIcons " +
                             "(shield sheet 'WRdWM' / sword sheet 'Ud37F' resolved 0 sub-sprites) - every weapon " +
                             "would resolve null and this case would assert NOTHING. Run " +
                             "Defenders/Art/Slice Item Icons");
                return;
            }

            int checkedRows = 0, shieldRows = 0, unknownKind = 0, authored = 0;
            var debtSeen = new List<string>();

            foreach (var w in GearCatalog.AllWeapons())
            {
                if (w == null || string.IsNullOrEmpty(w.id)) continue;

                // An authored iconPath is the row's own answer and bypasses the mapper
                // entirely (ItemIconCatalog treats it as authoritative) - nothing to judge.
                if (!string.IsNullOrEmpty(w.iconPath) && Resources.Load<Sprite>(w.iconPath) != null)
                { authored++; continue; }

                string kind = KindOf(w);
                if (kind.Length == 0) { unknownKind++; continue; }   // no authored category, no inference

                var sprite = ItemIconCatalog.ForWeapon(w);
                checkedRows++;
                if (string.Equals(kind, "shield", StringComparison.OrdinalIgnoreCase)) shieldRows++;

                // A null sprite is ALWAYS honest - the caller paints the row's own glyph.
                if (sprite == null || string.IsNullOrEmpty(sprite.name)) continue;

                string family = SpriteFamily(sprite.name);
                string wantFamily = ExpectedFamily(kind);

                bool ok = wantFamily.Length > 0
                    ? string.Equals(family, wantFamily, StringComparison.OrdinalIgnoreCase)
                    : false;   // kinds with no sheet of their own: only a null sprite is honest

                if (ok) continue;

                if (KnownIconKindDebt.Contains(w.id)) { debtSeen.Add(w.id + ":" + family); continue; }

                failures.Add($"[weapon-icon-kind] '{w.id}' is a {kind} but its icon resolves to the " +
                             $"'{family}' art ('{sprite.name}') - the bag shows one weapon and the hero holds " +
                             "another. Route the kind in ItemIconCatalog.ForWeapon, or author an iconPath on the " +
                             "row; do NOT baseline it into KnownIconKindDebt");
            }

            if (checkedRows == 0)
                failures.Add("[weapon-icon-kind] ZERO unauthored weapon rows evaluated - the catalog seam gave this " +
                             "case nothing to judge, which is a FAIL, not a pass");
            if (shieldRows == 0)
                failures.Add("[weapon-icon-kind] not ONE shield row reached the icon resolver - the founding case " +
                             "(a shield painting a sword) can no longer be detected by this suite");

            notes.Add($"icons judged = {checkedRows} ({shieldRows} shield, {authored} authored-skip, " +
                      $"{unknownKind} uncategorised-skip), known art debt still open = {debtSeen.Count}" +
                      (debtSeen.Count > 0 ? " [" + string.Join(",", debtSeen) + "]" : ""));
        }

        // =====================================================================
        //  CASE 3 - the shelf exclusion asymmetry is documented, or it is drift
        // =====================================================================
        private static void Case3_ForgeExclusion(List<string> failures, List<string> notes)
        {
            if (!File.Exists(VendorsRes)) { failures.Add("[forge-exclusion] missing " + VendorsRes); return; }
            if (!File.Exists(VendorsSA)) { failures.Add("[forge-exclusion] missing " + VendorsSA); return; }

            string res = File.ReadAllText(VendorsRes);
            string sa = File.ReadAllText(VendorsSA);
            if (res.Replace("\r\n", "\n") != sa.Replace("\r\n", "\n"))
                failures.Add("[forge-exclusion] vendors.json DRIFT between Resources and StreamingAssets - " +
                             "editor and device would disagree about the weapon shelf");

            JObject root;
            try { root = JObject.Parse(res); }
            catch (Exception ex)
            {
                failures.Add($"[forge-exclusion] vendors.json failed to parse ({ex.GetType().Name}: {ex.Message})");
                return;
            }

            JObject forge = FindRow(root, ForgeId), armorer = FindRow(root, ArmorerId);
            if (forge == null) { failures.Add("[forge-exclusion] vendors.json has no '" + ForgeId + "' row"); return; }
            if (armorer == null) { failures.Add("[forge-exclusion] vendors.json has no '" + ArmorerId + "' row"); return; }

            string forgeList = Prefixes(forge), armorerList = Prefixes(armorer);
            bool same = string.Equals(forgeList, armorerList, StringComparison.OrdinalIgnoreCase);

            string note = (string)forge[NoteField];
            if (!same && string.IsNullOrEmpty(note))
                failures.Add($"[forge-exclusion] forge.excludeIdPrefixes = [{forgeList}] but " +
                             $"armorer.excludeIdPrefixes = [{armorerList}], and the forge row carries no " +
                             $"'{NoteField}'. An undocumented asymmetry is indistinguishable from a missed edit - " +
                             "the WO-860 acceptance asked for the blink_ exclusion on BOTH shelves. Either match " +
                             "the lists or author the note saying why they differ");
            if (same && !string.IsNullOrEmpty(note))
                failures.Add($"[forge-exclusion] the lists now MATCH ([{forgeList}]) but the forge still carries " +
                             $"'{NoteField}' explaining a difference that no longer exists - a stale note is worse " +
                             "than none; drop it in the same edit that matched the lists");

            // The loader must actually see the list - a note guarding a value the registry
            // never reads pins nothing.
            var def = VendorRegistry.Find(ForgeId);
            if (def == null)
                failures.Add("[forge-exclusion] VendorRegistry.Find('" + ForgeId + "') is null despite the JSON row");
            else
            {
                string parsed = def.ExcludeIdPrefixes == null ? "" : string.Join(",", def.ExcludeIdPrefixes);
                if (!string.Equals(parsed, forgeList, StringComparison.OrdinalIgnoreCase))
                    failures.Add($"[forge-exclusion] loader drift: JSON forge.excludeIdPrefixes = [{forgeList}] " +
                                 $"but VendorRegistry parsed [{parsed}]");
            }

            notes.Add($"forge exclude=[{forgeList}] armorer exclude=[{armorerList}] documented={!string.IsNullOrEmpty(note)}");
        }

        private static JObject FindRow(JObject root, string id)
        {
            if (root["vendors"] is JArray arr)
                foreach (var v in arr)
                    if (string.Equals((string)v["id"], id, StringComparison.OrdinalIgnoreCase)) return (JObject)v;
            return null;
        }

        private static string Prefixes(JObject row)
        {
            var parts = new List<string>();
            if (row["excludeIdPrefixes"] is JArray arr)
                foreach (var p in arr) parts.Add(((string)p) ?? "");
            return string.Join(",", parts);
        }

        // =====================================================================
        //  The oracle's OWN kind mapping - deliberately independent of the
        //  resolver's helper so the two can disagree.
        // =====================================================================
        private static string KindOf(WeaponDef w)
        {
            if (w == null) return string.Empty;
            if (!string.IsNullOrEmpty(w.category)) return w.category.Trim().ToLowerInvariant();

            string key = ((w.id ?? "") + " " + (w.name ?? "")).ToLowerInvariant();
            if (Contains(key, "bow")) return "bow";
            if (Contains(key, "staff", "scepter", "sceptre", "rod", "wand")) return "staff";
            if (Contains(key, "shield", "buckler", "targe", "heater")) return "shield";
            if (Contains(key, "sword", "blade", "longsword", "greatsword", "claymore")) return "sword";
            if (Contains(key, "axe", "hatchet")) return "axe";
            if (Contains(key, "hammer", "maul", "mace", "censer")) return "hammer";
            return string.Empty;
        }

        /// <summary>The sheet family a KIND may legitimately paint; empty = it has no sheet,
        /// so only a null sprite (the row's glyph) is honest for it.</summary>
        private static string ExpectedFamily(string kind)
        {
            switch (kind)
            {
                case "shield": return "shield";
                case "bow":    return "bow";
                case "sword":  return "sword";
                case "dagger": return "mat";
                default:       return string.Empty;   // staff / axe / hammer / arrow: no sheet
            }
        }

        private static string SpriteFamily(string spriteName)
        {
            int u = spriteName.IndexOf('_');
            return u > 0 ? spriteName.Substring(0, u).ToLowerInvariant() : spriteName.ToLowerInvariant();
        }

        private static bool Contains(string haystack, params string[] needles)
        {
            for (int i = 0; i < needles.Length; i++)
                if (haystack.IndexOf(needles[i], StringComparison.Ordinal) >= 0) return true;
            return false;
        }
    }
}
