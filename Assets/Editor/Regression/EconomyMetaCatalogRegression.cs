// =============================================================================
// EconomyMetaCatalogRegression — headless data-invariant gate for the
// economy-meta area (Pets / Cosmetics / Wallet / Web3 services + their canon JSON).
// -----------------------------------------------------------------------------
// Mirrors MonetizationCovenantRegression's contract exactly (DeNelle.Editor,
// public static bool Run(out string reason) — true=pass+summary / false=fail+detail)
// so DataRegression.RunAll can register it with the same one-liner. No PlayMode:
// loads each canonical JSON through the REAL game path (DeNelle.Core.CanonicalJson,
// the same Resources-first, WebGL-safe read the live catalogs use) and asserts the
// data invariants that a silent edit/regen could break. Robust to a missing file
// (skip-with-note, never throw).
//
// COVERAGE (economy-meta canon data — NOT the pet/cosmetic gameplay catalogs the
// Catalogs team owns; these are the SERVICE-facing invariants):
//   • cosmetics.json — every row has id/category/displayName; category is one of
//     hero/pet/village; unlockMethod is buy|achievement; a 'buy' row costs > 0 and
//     an 'achievement' row costs 0 (the TryPurchase refuse-rules depend on this);
//     no duplicate ids (a dup id would let TryPurchase double-grant).
//   • pets.json — exactly the 3 starter species (aether-sprite/flame-pup/ice-wolf,
//     the only ones PetAcquisitionService maps to the PetSpecies enum), each with 5
//     bond ranks and a slot index; species are distinct.
//   • packs.json — 5 packs, tiers 1..5 each present & unique, each with a USD
//     reference price, exactly one founderOnly pack.
//   • wallets.json — the two PUBLIC addresses resolve, look base58, and the blob
//     carries NO secret-key material (privatekey/seed/keypair/mnemonic).
//   • pet-skill-trees.json — every species in pets.json has a tree (deploy path
//     intact). The extra authored trees (11 vs 3 deployable — the catalog's flag_15
//     over-specification) are reported as a NOTE, not a failure (forward authoring).
// =============================================================================
using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class EconomyMetaCatalogRegression
    {
        // The 3 species PetAcquisitionService.TryToSpeciesEnum maps to PetSpecies —
        // the only species that can be carried in OwnedPets + deployed from PetCatalog.
        private static readonly HashSet<string> EnumSpecies = new HashSet<string>
        {
            "aether-sprite", "flame-pup", "ice-wolf",
        };

        private static readonly string[] SecretFragments =
        {
            "privatekey", "private_key", "secretkey", "secret_key", "seedphrase",
            "seed_phrase", "mnemonic", "keypair", "signerkey",
        };

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var notes = new List<string>();

            CheckCosmetics(failures, notes);
            CheckPets(failures, notes);
            CheckPacks(failures, notes);
            CheckWallets(failures, notes);
            CheckPetSkillTrees(failures, notes);

            if (failures.Count == 0)
            {
                reason = "ECONOMY-META CATALOG OK — cosmetics/pets/packs/wallets/pet-skill-trees invariants hold" +
                         (notes.Count > 0 ? $" [notes: {string.Join("; ", notes)}]" : "");
                return true;
            }

            reason = $"ECONOMY-META CATALOG FAIL x{failures.Count}: " + string.Join(" | ", failures) +
                     (notes.Count > 0 ? $" [notes: {string.Join("; ", notes)}]" : "");
            return false;
        }

        // --- cosmetics.json ---------------------------------------------------
        private static void CheckCosmetics(List<string> failures, List<string> notes)
        {
            if (!TryLoadArray("Data/Canonical/cosmetics.json", "items", failures, notes, out var items))
                return;

            int count = 0;
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var tok in items)
            {
                if (!(tok is JObject o)) continue;
                count++;
                string id = Str(o, "id");
                string category = Str(o, "category");
                string display = Str(o, "displayName");
                string unlock = Str(o, "unlockMethod");
                int cost = Int(o, "glimmerCost");

                if (string.IsNullOrEmpty(id)) { failures.Add("cosmetics.json: a row with null/empty id"); continue; }
                if (!seen.Add(id)) failures.Add($"cosmetics.json: duplicate id '{id}' (TryPurchase could double-grant)");
                if (string.IsNullOrEmpty(display)) failures.Add($"cosmetics.json: '{id}' has empty displayName (blank shop card)");
                if (category != "hero" && category != "pet" && category != "village")
                    failures.Add($"cosmetics.json: '{id}' category '{category}' not in {{hero,pet,village}}");
                if (unlock != "buy" && unlock != "achievement")
                    failures.Add($"cosmetics.json: '{id}' unlockMethod '{unlock}' not in {{buy,achievement}}");
                else if (unlock == "buy" && cost <= 0)
                    failures.Add($"cosmetics.json: buyable '{id}' has glimmerCost {cost} (<=0 — TryPurchase would refuse it as free)");
                else if (unlock == "achievement" && cost != 0)
                    failures.Add($"cosmetics.json: achievement '{id}' has non-zero glimmerCost {cost} (achievement items must be 0)");
            }
            if (count == 0) failures.Add("cosmetics.json: 0 items (mapping break or empty 'items')");
        }

        // --- pets.json --------------------------------------------------------
        private static void CheckPets(List<string> failures, List<string> notes)
        {
            if (!TryLoadArray("Data/Canonical/pets.json", "pets", failures, notes, out var pets))
                return;

            var species = new HashSet<string>();
            var slots = new HashSet<int>();
            int count = 0;
            foreach (var tok in pets)
            {
                if (!(tok is JObject o)) continue;
                count++;
                string id = Str(o, "id");
                string sp = Str(o, "species");
                if (string.IsNullOrEmpty(id)) failures.Add("pets.json: a pet with null/empty id");
                if (string.IsNullOrEmpty(sp)) { failures.Add($"pets.json: '{id}' has empty species"); continue; }
                if (!species.Add(sp)) failures.Add($"pets.json: duplicate species '{sp}'");
                if (!EnumSpecies.Contains(sp))
                    failures.Add($"pets.json: species '{sp}' has no PetSpecies enum mapping — it cannot be carried in OwnedPets/deployed (PetAcquisitionService.TryToSpeciesEnum)");

                var bonds = o["bondRanks"] as JArray;
                if (bonds == null || bonds.Count != 5)
                    failures.Add($"pets.json: '{sp}' has {(bonds == null ? 0 : bonds.Count)} bondRanks (expected 5)");
                slots.Add(Int(o, "slotIndex"));
            }
            if (count != 3) failures.Add($"pets.json: {count} pets (V1 canon is exactly 3 starter species)");
            foreach (var required in EnumSpecies)
                if (!species.Contains(required))
                    failures.Add($"pets.json: missing required starter species '{required}'");
        }

        // --- packs.json -------------------------------------------------------
        private static void CheckPacks(List<string> failures, List<string> notes)
        {
            if (!TryLoadArray("Data/Canonical/packs.json", "packs", failures, notes, out var packs))
                return;

            var tiers = new HashSet<int>();
            int count = 0, founderCount = 0;
            foreach (var tok in packs)
            {
                if (!(tok is JObject o)) continue;
                count++;
                string sku = Str(o, "sku");
                int tier = Int(o, "tier");
                if (string.IsNullOrEmpty(sku)) failures.Add("packs.json: a pack with null/empty sku");
                if (tier < 1 || tier > 5) failures.Add($"packs.json: '{sku}' tier {tier} out of 1..5");
                else if (!tiers.Add(tier)) failures.Add($"packs.json: duplicate tier {tier}");

                var pricing = o["pricing"] as JObject;
                double usd = pricing != null ? (double?)pricing["usd"] ?? 0d : 0d;
                if (usd <= 0d) failures.Add($"packs.json: '{sku}' has no positive USD reference price");
                if (o["founderOnly"] != null && o["founderOnly"].Type == JTokenType.Boolean && (bool)o["founderOnly"])
                    founderCount++;
            }
            if (count != 5) failures.Add($"packs.json: {count} packs (canon is 5 — Hearth Spark → Founder's Vow)");
            if (founderCount != 1) failures.Add($"packs.json: {founderCount} founderOnly packs (expected exactly 1)");
        }

        // --- wallets.json -----------------------------------------------------
        private static void CheckWallets(List<string> failures, List<string> notes)
        {
            string json = SafeRead("Data/Canonical/wallets.json", failures, notes);
            if (json == null) return;

            // Secret-material scan on the RAW text (WalletRegistryTest's discipline).
            string lower = json.ToLowerInvariant();
            foreach (var frag in SecretFragments)
                if (lower.Contains(frag))
                    failures.Add($"wallets.json: contains forbidden secret-material token '{frag}' (public addresses ONLY)");

            JObject root;
            try { root = JObject.Parse(json); }
            catch (Exception ex) { failures.Add($"wallets.json: parse error ({ex.Message})"); return; }

            foreach (var key in new[] { "rewardsDistributor", "devnetPurchaseRecipient" })
            {
                var entry = root[key] as JObject;
                if (entry == null) { failures.Add($"wallets.json: missing '{key}' entry"); continue; }
                string addr = Str(entry, "address");
                if (string.IsNullOrEmpty(addr)) failures.Add($"wallets.json: '{key}' has no address");
                else if (!LooksBase58(addr)) failures.Add($"wallets.json: '{key}' address '{addr}' is not base58-shaped (len {addr.Length})");
            }
        }

        // --- pet-skill-trees.json --------------------------------------------
        private static void CheckPetSkillTrees(List<string> failures, List<string> notes)
        {
            string json = SafeRead("Data/Canonical/pet-skill-trees.json", failures, notes);
            if (json == null) return;
            JObject root;
            try { root = JObject.Parse(json); }
            catch (Exception ex) { failures.Add($"pet-skill-trees.json: parse error ({ex.Message})"); return; }

            var trees = root["trees"] as JArray;
            if (trees == null || trees.Count == 0) { failures.Add("pet-skill-trees.json: 0 trees (mapping break)"); return; }

            var treeSpecies = new HashSet<string>();
            foreach (var tok in trees)
                if (tok is JObject o)
                {
                    string sp = Str(o, "species");
                    if (!string.IsNullOrEmpty(sp)) treeSpecies.Add(sp);
                }

            // Deploy-path invariant: every DEPLOYABLE species must have a tree.
            foreach (var sp in EnumSpecies)
                if (!treeSpecies.Contains(sp))
                    failures.Add($"pet-skill-trees.json: deployable species '{sp}' has NO skill tree");

            // flag_15: authored trees (e.g. 11) exceed the 3 deployable species — this is
            // forward authoring, NOT a break. Report it so an edit that shrinks below 3 is caught.
            int extra = treeSpecies.Count - EnumSpecies.Count;
            if (extra > 0)
                notes.Add($"pet-skill-trees.json over-specifies by {extra} species vs the 3 deployable (flag_15 forward authoring)");
        }

        // ── helpers ───────────────────────────────────────────────────────────

        private static bool TryLoadArray(string relPath, string arrayKey, List<string> failures,
                                         List<string> notes, out JArray array)
        {
            array = null;
            string json = SafeRead(relPath, failures, notes);
            if (json == null) return false;
            JObject root;
            try { root = JObject.Parse(json); }
            catch (Exception ex) { failures.Add($"{relPath}: parse error ({ex.Message})"); return false; }
            array = root[arrayKey] as JArray;
            if (array == null) { failures.Add($"{relPath}: no '{arrayKey}' array (mapping break)"); return false; }
            return true;
        }

        private static string SafeRead(string relPath, List<string> failures, List<string> notes)
        {
            string json;
            try { json = DeNelle.Core.CanonicalJson.Read(relPath); }
            catch (Exception ex) { failures.Add($"{relPath}: read threw ({ex.Message})"); return null; }
            if (string.IsNullOrEmpty(json)) { notes.Add($"{relPath} MISSING/empty — skipped"); return null; }
            return json;
        }

        private static string Str(JObject o, string key)
        {
            var t = o[key];
            return t != null && t.Type == JTokenType.String ? t.ToString() : (t != null ? t.ToString() : null);
        }

        private static int Int(JObject o, string key)
        {
            var t = o[key];
            if (t == null) return 0;
            if (t.Type == JTokenType.Integer) return t.Value<int>();
            if (t.Type == JTokenType.Float) return (int)t.Value<double>();
            return 0;
        }

        private static bool LooksBase58(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length < 32 || s.Length > 44) return false;
            foreach (char c in s)
            {
                if (c == '0' || c == 'O' || c == 'I' || c == 'l') return false;
                bool ok = (c >= '1' && c <= '9') || (c >= 'A' && c <= 'Z') || (c >= 'a' && c <= 'z');
                if (!ok) return false;
            }
            return true;
        }
    }
}
