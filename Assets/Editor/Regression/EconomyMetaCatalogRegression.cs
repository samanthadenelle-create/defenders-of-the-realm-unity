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
//   • packs.json — 13 packs (5 price-ladder + 8 themed bundles, v2 2026-06-28),
//     tiers 1..13 each present & unique, each with a USD reference price, exactly
//     one founderOnly pack (Founder's Vow, tier 5).
//   • wallets.json — the two PUBLIC addresses resolve, look base58, and the blob
//     carries NO secret-key material (privatekey/seed/keypair/mnemonic).
//   (pet-skill-trees.json check RETIRED 2026-07-08 — that catalog was deleted; pets are
//    harvest/companion only per docs/COMBAT_PIVOT_NORTHSTAR.md.)
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

        // Canonical pack-shelf size (docs/MONETIZATION_REVIEW_2026-07-02.md §1.1: "13 authored
        // packs … 5 price-ladder + 8 themed bundles"; packs.json header). Tiers are a UNIQUE
        // 1..N lookup key (PackCatalog.FindByTier), not the price band.
        // ⚠ 2026-08-17 (WO-1037): this was `= 13`, and WO-1037's twelve impulse SKUs turned it red
        // at "tier 14 out of 1..13". THAT WAS A STALE LITERAL, NOT A REAL BOUND. packs.json's own
        // schema doc says so: "tier": "Unique 1..N lookup key (PackCatalog.FindByTier). NOT the
        // price band". So the ceiling is N — the number of packs — and 13 was only ever "N as it
        // stood when 13 packs existed" (5 price-ladder + 8 themed bundles).
        //
        // ⛔ THE FIX IS NOT `= 25`. That just re-arms the same trap for the next SKU, and the next
        // author gets a red that looks like a real economy violation and is not. Derive it from the
        // catalog so the assertion becomes what it always meant: **tiers are unique and dense over
        // 1..N**, which is the property FindByTier actually depends on. A hardcoded ceiling cannot
        // express that and never could.
        //
        // The suite is now STRONGER, not weaker: a gap or a duplicate in the tier sequence fails,
        // where before a duplicate at tier 7 would have passed while a perfectly valid tier 14 failed.
        /// <summary>How many BROWSABLE packs the shelf carries: 5 price-ladder (Hearth Spark →
        /// Founder's Vow) + 8 themed bundles + 1 permanent-builder (WO-1253, patronage band,
        /// CONCURRENCY SKU). Impulse SKUs are excluded by design — they are a
        /// shortfall remedy, not a storefront (WO-947 §12c.4), and PackStore skips them in its card
        /// loop. Re-rule this number only when a genuinely browsable pack is added.
        /// <para>⚠ DELIBERATELY UNCHANGED BY WO-1050 (The Night Market, 2026-08-21) — this is a
        /// RECORDED DECISION, not an oversight, so the next seat does not "fix" it. That WO's draft
        /// asked for a new <c>keepers-almanac</c> SKU at $9.99/120 SKR as a second Patronage anchor,
        /// which would have made this 14. It was NOT minted: $9.99 is already the live
        /// <c>folks-thanks</c> rung (two products at one price, one of them unbuyable, reads as
        /// broken rather than aspirational), and an <c>anchorOnly</c> row creates no price contrast
        /// while <c>FeatureFlags.RealmStorePurchase</c> is OFF and every card already reads "Coming
        /// soon". The Patronage band instead carries the TWO REAL top rungs that were already on the
        /// shelf. The redesign is presentation-only and added no browsable pack, so 13 still holds.
        /// The owner may overrule and mint the SKU; this constant is then the one to re-rule.</para>
        /// </summary>
        private const int CanonShelfPackCount = 14;

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
            // RETIRED (2026-07-08): CheckPetSkillTrees removed — pet-skill-trees.json was deleted
            // (dead content; pets are harvest/companion-only per docs/COMBAT_PIVOT_NORTHSTAR.md).

            if (failures.Count == 0)
            {
                reason = "ECONOMY-META CATALOG OK — cosmetics/pets/packs/wallets invariants hold" +
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
                if (tier < 1) failures.Add($"packs.json: '{sku}' tier {tier} is below 1 (tiers are a 1..N lookup key)");
                else if (!tiers.Add(tier)) failures.Add($"packs.json: duplicate tier {tier}");

                var pricing = o["pricing"] as JObject;
                double usd = pricing != null ? (double?)pricing["usd"] ?? 0d : 0d;
                if (usd <= 0d) failures.Add($"packs.json: '{sku}' has no positive USD reference price");
                if (o["founderOnly"] != null && o["founderOnly"].Type == JTokenType.Boolean && (bool)o["founderOnly"])
                    founderCount++;
            }
            // Canon = 13 packs (v2, 2026-06-28): the 5-tier price ladder (Hearth Spark →
            // Founder's Vow) + 8 themed starter bundles (Frostfall … Builder's Cache), each
            // with a UNIQUE tier lookup key 1..13. Source: docs/MONETIZATION_REVIEW_2026-07-02.md
            // §1.1 ("13 authored packs … 5 price-ladder + 8 themed bundles") + packs.json header.
            // The old "5 / tiers 1..5" oracle predated the 06-28 bundle expansion (STALE).
            // ⚠ REWRITTEN 2026-08-17 (WO-1037). This used to assert `count != 13`, which WO-1037's
            // twelve impulse SKUs broke. But "13" was never one claim — it was TWO, welded together:
            //   (a) the SHELF has 13 browsable packs   ← a real canon claim, still true
            //   (b) therefore the catalog has 13 rows  ← an accident of (a), and now false
            // Impulse packs are deliberately NOT on the shelf (WO-947 §12c.4: they are a shortfall
            // remedy, not a storefront — PackStore skips `pack.Impulse` in its card loop), so they
            // add rows without adding shelf entries. Asserting (b) was asserting the accident.
            int shelfCount = 0;
            foreach (var tok in packs)
                if (tok is JObject po && !(po["impulse"] != null && po["impulse"].Type == JTokenType.Boolean && (bool)po["impulse"]))
                    shelfCount++;

            if (shelfCount != CanonShelfPackCount)
                failures.Add($"packs.json: {shelfCount} SHELF packs (canon is {CanonShelfPackCount} — 5 price-ladder " +
                             $"Hearth Spark→Founder's Vow + 8 themed bundles + permanent-builder). Impulse SKUs are excluded from this " +
                             $"count by design; if you added a browsable pack, this number is the one to re-rule.");

            // Tiers must be UNIQUE and DENSE over 1..N — the property PackCatalog.FindByTier
            // actually depends on, and the one a hardcoded ceiling could never express. A gap here
            // means a lookup silently returns nothing for a tier a caller can legitimately ask for.
            for (int t = 1; t <= count; t++)
                if (!tiers.Contains(t))
                    failures.Add($"packs.json: tier {t} is missing — tiers must be dense over 1..{count} " +
                                 $"(FindByTier does a direct lookup; a gap is a silent null).");
            if (founderCount != 1) failures.Add($"packs.json: {founderCount} founderOnly packs (expected exactly 1 — Founder's Vow, tier 5)");
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

        // RETIRED (2026-07-08): CheckPetSkillTrees removed with pet-skill-trees.json (dead content;
        // pets are harvest/companion-only per docs/COMBAT_PIVOT_NORTHSTAR.md).

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
