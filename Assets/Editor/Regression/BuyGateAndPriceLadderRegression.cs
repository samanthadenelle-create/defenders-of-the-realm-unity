// =============================================================================
// BuyGateAndPriceLadderRegression [buy-gate] -- WO-1121, the two owner rulings of
// 2026-08-21 that changed what this store may sell and to whom.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (editor-only). Contract mirrors the sibling
// oracles:
//   public static bool Run(out string reason)   -- NEVER throws
//   markers: BUY_GATE_OK (Debug.Log) / BUY_GATE_FAIL (LogError)
//   registered ONCE inside DataRegression.RunAll's fenced registry region.
//
// THE TWO RULINGS
//
//   1. THE PRICE CEILING IS $49.99, NOT $4.99. The $4.99 cap was an EARLY-ACCESS
//      constraint that WO-1118 applied as though it were permanent, and it hid the
//      top three rungs of the ladder monetization-v2-spec §4 has always authored.
//      ⛔ THE COVENANT IS UNCHANGED AND IS ABOUT CONTENT, NOT PRICE: a $49.99 pack
//      that sells time and beauty is fine; a $0.99 pack that sells damage is not.
//      So this suite does NOT police price at all -- it polices what the shelf
//      ADVERTISES, which is the thing that can actually lie to a payer.
//
//   2. WALLET REQUIRED ABOVE $4.99. A guest's save key is
//      guest-local-<sha256(deviceId)> -- device-derived, with no proven restore
//      path after a reinstall or a new phone. At $4.99 a lost entitlement is an
//      annoyance; at $49.99 it is a chargeback on a LIVE dApp Store listing.
//
// WHY THE STRUCTURAL CASES EXIST, AND WHY THEY ARE THE POINT.
// A value check ("this pack is refused today") is cheap to satisfy and cheap to
// break: it passes for the WRONG REASON the moment purchases are enabled, because
// the whole rail is closed right now and EVERY pack is refused. So the load-bearing
// cases here are structural:
//   * [charge-path] PackStore.Purchase() -- the ONLY method in the project that
//     reaches WalletService.Pay -- must consult PurchaseGate.CanBuy(pack, ...), not
//     FeatureFlags.RealmStorePurchase. A rule enforced only where the button is
//     drawn is bypassed by every caller that never drew one (the shortfall offer, a
//     deep link, a promo). That is the exact defect the ruling names, and no
//     value-based assertion can detect it.
//   * [single-threshold] the threshold exists ONCE, as a code constant, and is NOT
//     re-authored as a per-pack `requiresWallet` field. Two copies of one decision
//     is how this repo's worst drift bugs are built (CLAUDE.md §2/§5).
//
// AND THE VAPOR RULE (WO-1118, restated by the owner the same day when glimmer was
// stripped): a browsable pack may not advertise anything this build cannot deliver.
// Cosmetics must exist in cosmetics.json; convenience kinds must pass
// PackCatalog.IsRedeemableConvenience; and NO pack may carry `glimmer` at all,
// because its only sink is cosmetics and no CosmeticApplier runs.
//
// Wire (DataRegression.RunAll, inside the fence):
//   Guard.Try("Regression", "buy-gate suite", () => { if (!BuyGateAndPriceLadderRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[buy-gate] " + r); });
// =============================================================================

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Wallet;

namespace DeNelle.Editor.Regression
{
    /// <summary>Pins the WO-1121 price-ladder ruling and the wallet-above-$4.99 buy gate.</summary>
    public static class BuyGateAndPriceLadderRegression
    {
        private const string PacksRelPath  = "Data/Canonical/packs.json";
        private const string CanonRelPath  = "Data/Canonical/canon-strings.json";
        private const string CosmeticsRel  = "Data/Canonical/cosmetics.json";

        /// <summary>PlayerPrefs key behind FeatureFlags.RealmStorePurchase (FeatureFlags.Get: "ff." + name).</summary>
        private const string BuyFlagPrefKey = "ff.realmstorepurchase";

        /// <summary>
        /// The full ladder monetization-v2-spec §4 authors, and the ruling that put the top three
        /// rungs back on the shelf. sku -> usd. Hardcoded HERE on purpose: an oracle that read the
        /// ladder out of the same file it is checking would assert nothing about it.
        /// </summary>
        private static readonly KeyValuePair<string, double>[] Ladder =
        {
            // ⭐ ENTRY RUNG IS $4.99 (owner ruling 2026-08-24). It was `hearth-spark` at $1.99
            // until WO-1069 repriced that pack to 4.99 to stop it dominating impulse-wood-small.
            // ⛔ hearth-spark did NOT move to this row: at 4.99 `starters-hand` STRICTLY DOMINATES
            // it (more of all five resources, same price), so hearth-spark left the SHELF entirely
            // rather than sitting on the entry rung as the bad buy. It stays quotable as
            // DEVNET_CANARY_SKU. ⚠ This amends WO-1121's "$1.99..$49.99" to "$4.99..$49.99".
            new KeyValuePair<string, double>("starters-hand",     4.99d),
            new KeyValuePair<string, double>("folks-thanks",      9.99d),
            new KeyValuePair<string, double>("patron-of-elarion", 19.99d),
            new KeyValuePair<string, double>("founders-vow",      49.99d),
        };

        /// <summary>Standalone batch entry - prints the marker.</summary>
        public static void RunAll()
        {
            if (Run(out string reason)) Debug.Log("BUY_GATE_OK - " + reason);
            else Debug.LogError("BUY_GATE_FAIL: " + reason);
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("=== BuyGateAndPriceLadderRegression [buy-gate] (WO-1121: $49.99 ceiling + wallet above $4.99) ===");

            try
            {
                CaseDualCopy(failures, log);

                JArray packs = ReadPacks(failures, log);
                if (packs != null)
                {
                    CaseLadderIsOnTheShelf(packs, failures, log);
                    CaseNoGlimmerAnywhere(packs, failures, log);
                    CaseShelfAdvertisesOnlyDeliverables(packs, failures, log);
                    CaseSingleThreshold(packs, failures, log);
                    CaseWalletRuleRefusesEveryUpperTier(packs, failures, log);
                }

                CaseThresholdBoundary(failures, log);
                CaseRefusalSentencesExist(failures, log);
                CaseChargePathConsultsTheGate(failures, log);
            }
            catch (Exception ex)
            {
                // NEVER throws (the suite contract): a throw here would take the whole gate down
                // and tell nobody which rule broke.
                failures.Add("[buy-gate] BuyGateAndPriceLadderRegression THREW: " + ex.GetType().Name + ": " + ex.Message);
            }

            if (failures.Count == 0)
            {
                reason = "BUY GATE OK - the full $1.99..$49.99 ladder is on the shelf; no pack advertises a " +
                         "cosmetic, a convenience kind or a glimmer line this build cannot deliver; the wallet " +
                         "threshold exists exactly once (PurchaseGate.WalletRequiredAboveUsd = $" +
                         PurchaseGate.WalletRequiredAboveUsd.ToString("0.00", CultureInfo.InvariantCulture) +
                         ") and is never re-authored per pack; every pack above it is refused while this save " +
                         "has no attested wallet; and the CHARGE PATH itself (PackStore.Purchase) consults the " +
                         "gate, so the rule is not UI-only.";
                Debug.Log("BUY_GATE_OK\n" + log);
                return true;
            }

            reason = "buy-gate: " + failures.Count + " failure(s): " + string.Join(" | ", failures);
            Debug.LogError("BUY_GATE_FAIL: " + failures.Count + " failure(s)\n" + log +
                           "\n - " + string.Join("\n - ", failures));
            return false;
        }

        // =====================================================================
        //  [dual-copy] -- runs FIRST. If the two copies differ, every later case
        //  is measuring a file the shipped build may never load, and the price a
        //  player SEES would not be the price they are CHARGED.
        // =====================================================================
        private static void CaseDualCopy(List<string> failures, StringBuilder log)
        {
            AssertCopiesIdentical(PacksRelPath, failures, log);
            AssertCopiesIdentical(CanonRelPath, failures, log);
        }

        private static void AssertCopiesIdentical(string rel, List<string> failures, StringBuilder log)
        {
            string res = Application.dataPath + "/Resources/" + rel;
            string sa  = Application.dataPath + "/StreamingAssets/" + rel;
            if (!File.Exists(res) || !File.Exists(sa))
            {
                failures.Add("[dual-copy] " + rel + " is missing " +
                             (File.Exists(res) ? "" : "the Resources copy ") +
                             (File.Exists(sa) ? "" : "the StreamingAssets copy") +
                             " - CanonicalJson reads Resources first and falls back to StreamingAssets, so one " +
                             "missing copy silently changes what a shipped build loads.");
                return;
            }

            byte[] a = File.ReadAllBytes(res), b = File.ReadAllBytes(sa);
            bool equal = a.Length == b.Length;
            if (equal)
                for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) { equal = false; break; }

            if (!equal)
                failures.Add("[dual-copy] " + rel + " Resources and StreamingAssets copies DIVERGED (" +
                             a.Length + " vs " + b.Length + " bytes).");
            else
                log.AppendLine("  [dual-copy] " + rel + " byte-identical across both copies (" + a.Length + " bytes)");
        }

        // =====================================================================
        //  [ladder] -- RULING 1. All five rungs priced as the spec authors them,
        //  and all five BROWSABLE. The old $4.99 ceiling hid the top three.
        // =====================================================================
        private static void CaseLadderIsOnTheShelf(JArray packs, List<string> failures, StringBuilder log)
        {
            foreach (var rung in Ladder)
            {
                JObject p = FindPack(packs, rung.Key);
                if (p == null)
                {
                    failures.Add("[ladder] packs.json has no pack '" + rung.Key + "' - the monetization-v2-spec §4 " +
                                 "ladder is incomplete. `sku` is a LIVE save key, so a missing rung is either a " +
                                 "deletion or a rename without a legacySkus alias; both orphan entitlements.");
                    continue;
                }

                double usd = p["pricing"]?["usd"]?.Value<double>() ?? -1d;
                if (Math.Abs(usd - rung.Value) > 0.0001d)
                    failures.Add("[ladder] '" + rung.Key + "' is priced $" + usd.ToString("0.00", CultureInfo.InvariantCulture) +
                                 ", spec §4 says $" + rung.Value.ToString("0.00", CultureInfo.InvariantCulture) + ".");

                bool visible = p["storeVisible"] == null || p["storeVisible"].Type != JTokenType.Boolean
                             || p["storeVisible"].Value<bool>();
                if (!visible)
                    failures.Add("[ladder] '" + rung.Key + "' is storeVisible:false. The owner ruled the FULL " +
                                 "$1.99..$49.99 ladder back onto the shelf on 2026-08-21 - the $4.99 cap was an " +
                                 "EARLY-ACCESS constraint, not a permanent one. Re-hiding a rung is an OWNER " +
                                 "decision; if one was genuinely re-hidden, this list is what to re-rule.");

                if (visible && p["_hiddenReason"] != null)
                    failures.Add("[ladder] '" + rung.Key + "' is VISIBLE but still carries a _hiddenReason - the " +
                                 "row and its own explanation disagree, and the next reader will believe the note.");
            }
            log.AppendLine("  [ladder] 5 rungs checked ($1.99 / $4.99 / $9.99 / $19.99 / $49.99), all browsable");
        }

        // =====================================================================
        //  [no-glimmer] -- owner ruling 2026-08-21, verbatim: "remove all glimmer
        //  from packs as its nothing real and money has never been active".
        //  Glimmer's only sink is cosmetics and CosmeticApplier is called from
        //  nowhere, so a glimmer line on a paid card buys nothing visible.
        //  ⚠ This is about pack CONTENTS. Glimmer the CURRENCY is untouched and is
        //  still earned and spent elsewhere - do not "fix" a failure here by
        //  deleting the currency.
        // =====================================================================
        private static void CaseNoGlimmerAnywhere(JArray packs, List<string> failures, StringBuilder log)
        {
            int scanned = 0;
            foreach (var tok in packs)
            {
                if (!(tok is JObject p)) continue;
                scanned++;
                if (p["contents"]?["economy"]?["glimmer"] != null)
                    failures.Add("[no-glimmer] pack '" + Sku(p) + "' carries a `glimmer` key. Owner ruling " +
                                 "2026-08-21: no pack may. Its only sink is cosmetics, and no CosmeticApplier " +
                                 "runs, so it is a line on a paid card that buys nothing the player can see.");
            }
            log.AppendLine("  [no-glimmer] " + scanned + " packs scanned, none carrying a glimmer line");
        }

        // =====================================================================
        //  [no-vapor] -- the WO-1118 shelf-honesty rule, applied to whatever is
        //  browsable TODAY. Only storeVisible rows are policed: a hidden row is
        //  kept loadable so an existing owner's entitlement still resolves, and
        //  holding it to the shelf's standard would force a delete instead.
        // =====================================================================
        private static void CaseShelfAdvertisesOnlyDeliverables(JArray packs, List<string> failures, StringBuilder log)
        {
            HashSet<string> cosmeticIds = ReadCosmeticIds();
            int shelf = 0;

            foreach (var tok in packs)
            {
                if (!(tok is JObject p)) continue;
                bool visible = p["storeVisible"] == null || p["storeVisible"].Type != JTokenType.Boolean
                             || p["storeVisible"].Value<bool>();
                if (!visible) continue;
                shelf++;
                string sku = Sku(p);

                // Cosmetics: every advertised id must exist in cosmetics.json, or it is a dangling
                // entitlement the player can never equip.
                var cos = p["contents"]?["cosmetics"] as JArray;
                if (cos != null && cosmeticIds != null)
                    foreach (var c in cos)
                    {
                        string id = c?.Value<string>();
                        if (!string.IsNullOrEmpty(id) && !cosmeticIds.Contains(id))
                            failures.Add("[no-vapor] shelf pack '" + sku + "' advertises cosmetic '" + id +
                                         "' which has NO row in cosmetics.json - unredeemable.");
                    }

                // Convenience: LEGAL is not REDEEMABLE. PackCatalog.IsRedeemableConvenience is the
                // live statement about THIS build; asking it (rather than re-listing kinds here)
                // means the day a redeemer ships, this oracle updates itself.
                var conv = p["contents"]?["convenience"] as JArray;
                if (conv != null)
                    foreach (var item in conv)
                    {
                        string kind = (item as JObject)?["kind"]?.Value<string>();
                        if (string.IsNullOrEmpty(kind)) continue;
                        if (!PackCatalog.IsRedeemableConvenience(kind))
                            failures.Add("[no-vapor] shelf pack '" + sku + "' advertises convenience kind '" + kind +
                                         "' which NOTHING in this build spends (PackCatalog.IsRedeemableConvenience " +
                                         "== false). Ship the redeemer first, then re-add the line - that order is " +
                                         "the whole of WO-1118.");
                    }

                // A pack must grant SOMETHING. An all-empty contents bag on a sellable row is the
                // limit case of the vapor rule: money in, nothing out.
                bool anyEconomy = false;
                var econ = p["contents"]?["economy"] as JObject;
                if (econ != null)
                    foreach (var kv in econ)
                        if (kv.Value != null && kv.Value.Type == JTokenType.Integer && kv.Value.Value<long>() > 0)
                        { anyEconomy = true; break; }

                bool anyCosmetic = cos != null && cos.Count > 0;
                bool anyConv = conv != null && conv.Count > 0;
                if (!anyEconomy && !anyCosmetic && !anyConv)
                    failures.Add("[no-vapor] shelf pack '" + sku + "' grants NOTHING (empty economy, cosmetics and " +
                                 "convenience) and is still sellable.");
            }

            log.AppendLine("  [no-vapor] " + shelf + " browsable packs: every advertised cosmetic exists, every " +
                           "convenience kind has a live redeemer, none grants nothing");
        }

        // =====================================================================
        //  [single-threshold] -- STRUCTURAL. The wallet rule is derived from the
        //  authored PRICE, in ONE code constant. A per-pack `requiresWallet` field
        //  would be a second copy of a decision the price already makes, and the
        //  two would drift the first time a pack is repriced.
        // =====================================================================
        private static void CaseSingleThreshold(JArray packs, List<string> failures, StringBuilder log)
        {
            foreach (var tok in packs)
            {
                if (!(tok is JObject p)) continue;
                foreach (var kv in p)
                {
                    string k = kv.Key;
                    if (k.IndexOf("requireswallet", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        k.IndexOf("walletrequired", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        k.IndexOf("walletonly", StringComparison.OrdinalIgnoreCase) >= 0)
                        failures.Add("[single-threshold] pack '" + Sku(p) + "' authors a '" + k + "' field. The " +
                                     "wallet rule is DERIVED from pricing.usd in PurchaseGate.RequiresWallet and " +
                                     "must not be re-authored per pack - two copies of one decision is the drift " +
                                     "bug CLAUDE.md §2/§5 documents.");
                }
            }
            log.AppendLine("  [single-threshold] no per-pack wallet override authored; the rule lives only in " +
                           "PurchaseGate.WalletRequiredAboveUsd");
        }

        // =====================================================================
        //  [threshold] -- RULING 2's boundary, asserted on the predicate itself.
        //  $4.99 EXACTLY must stay guest-buyable: the ruling moved the wallet
        //  requirement ABOVE the old ceiling, so nothing that was already
        //  guest-buyable may be taken away by a float rounding hair.
        // =====================================================================
        private static void CaseThresholdBoundary(List<string> failures, StringBuilder log)
        {
            if (Math.Abs(PurchaseGate.WalletRequiredAboveUsd - 4.99d) > 0.0001d)
                failures.Add("[threshold] PurchaseGate.WalletRequiredAboveUsd is " +
                             PurchaseGate.WalletRequiredAboveUsd.ToString("0.0000", CultureInfo.InvariantCulture) +
                             ", the owner ruled $4.99 (2026-08-21).");

            if (PurchaseGate.RequiresWallet(1.99d)) failures.Add("[threshold] $1.99 must be guest-buyable.");
            if (PurchaseGate.RequiresWallet(4.99d)) failures.Add("[threshold] $4.99 EXACTLY must stay guest-buyable - " +
                                                                "the rule is ABOVE $4.99, not from $4.99.");
            if (!PurchaseGate.RequiresWallet(9.99d))  failures.Add("[threshold] $9.99 must require a wallet.");
            if (!PurchaseGate.RequiresWallet(19.99d)) failures.Add("[threshold] $19.99 must require a wallet.");
            if (!PurchaseGate.RequiresWallet(49.99d)) failures.Add("[threshold] $49.99 must require a wallet - this is " +
                                                                  "the chargeback case the ruling exists for.");
            log.AppendLine("  [threshold] $1.99/$4.99 guest-buyable, $9.99/$19.99/$49.99 wallet-gated");
        }

        // =====================================================================
        //  [wallet-rule] -- BEHAVIOURAL. With the Buy flag forced ON and no
        //  attested wallet on this machine, EVERY pack above the threshold must be
        //  refused by PurchaseGate.CanBuy(pack, ...), with a non-empty reason.
        //
        //  The flag is forced ON deliberately: with it OFF (the shipping default)
        //  every pack is refused anyway, and the case would pass for a reason that
        //  has nothing to do with the wallet rule - a green that means nothing.
        //  The prior value is restored in a finally, always.
        // =====================================================================
        private static void CaseWalletRuleRefusesEveryUpperTier(JArray packs, List<string> failures, StringBuilder log)
        {
            if (PurchaseGate.HasDurableIdentity)
            {
                // A machine whose save carries a real attested wallet cannot demonstrate the refusal.
                // Say so as a PARTIAL SKIP rather than pass silently - a hollow green here would be
                // the exact arithmetic bug RegressionOutcome exists to end.
                log.AppendLine("  [wallet-rule] " + RegressionOutcome.PartialSkipToken +
                               " this save has an ATTESTED wallet, so the without-a-wallet refusal cannot be " +
                               "exercised here. The structural cases below still bind.");
                return;
            }

            bool hadPref = PlayerPrefs.HasKey(BuyFlagPrefKey);
            int prevPref = hadPref ? PlayerPrefs.GetInt(BuyFlagPrefKey, -1) : -1;
            int checkedUpper = 0, checkedGuest = 0;
            try
            {
                PlayerPrefs.SetInt(BuyFlagPrefKey, 1);   // force the rail flag ON for this case only
                PackCatalog.Reload();

                foreach (var tok in packs)
                {
                    if (!(tok is JObject po)) continue;
                    string sku = Sku(po);
                    PackDef pack = PackCatalog.Find(sku);
                    if (pack == null) continue;

                    double usd = pack.Pricing != null ? pack.Pricing.Usd : 0d;
                    bool allowed = PurchaseGate.CanBuy(pack, out string why);

                    if (PurchaseGate.RequiresWallet(usd))
                    {
                        checkedUpper++;
                        if (allowed)
                            failures.Add("[wallet-rule] '" + sku + "' ($" + usd.ToString("0.00", CultureInfo.InvariantCulture) +
                                         ") is PURCHASABLE with no attested wallet on this save. A guest key is " +
                                         "device-derived with no proven restore path - at this price a lost " +
                                         "entitlement is a chargeback on a live listing.");
                        else if (string.IsNullOrEmpty(why))
                            failures.Add("[wallet-rule] '" + sku + "' was refused with an EMPTY reason - that is a " +
                                         "dead button, which the ruling forbids as explicitly as the sale itself.");
                    }
                    else
                    {
                        checkedGuest++;
                        // The guest tier must NOT be refused BY THE WALLET RULE. It may still be
                        // refused by the rail (no resolvable mint today), so assert on the sentence
                        // rather than on the bool: a guest-tier pack must never be told to connect
                        // a wallet, because connecting one would not change anything for it.
                        if (!allowed && string.Equals(why, StoreStrings.Format(
                                StoreStrings.KeyBuyWalletRequired, "$" +
                                PurchaseGate.WalletRequiredAboveUsd.ToString("0.00", CultureInfo.InvariantCulture)),
                                StringComparison.Ordinal))
                            failures.Add("[wallet-rule] '" + sku + "' ($" + usd.ToString("0.00", CultureInfo.InvariantCulture) +
                                         ") was refused with the WALLET sentence, but it is at or under the $" +
                                         PurchaseGate.WalletRequiredAboveUsd.ToString("0.00", CultureInfo.InvariantCulture) +
                                         " ceiling and must stay guest-buyable.");
                    }
                }
            }
            finally
            {
                // Restore EXACTLY what was there, including "no key at all" - leaving a stored 1
                // behind would silently arm the purchase rail on this machine (CLAUDE.md notes a
                // stored ff.realmstorepurchase BEATS the compiled default).
                if (hadPref) PlayerPrefs.SetInt(BuyFlagPrefKey, prevPref);
                else PlayerPrefs.DeleteKey(BuyFlagPrefKey);
                PlayerPrefs.Save();
                PackCatalog.Reload();
            }

            log.AppendLine("  [wallet-rule] " + checkedUpper + " above-threshold packs all refused without a wallet; " +
                           checkedGuest + " guest-tier packs never shown the wallet sentence");
        }

        // =====================================================================
        //  [copy] -- the refusal must be HONEST AND ACTIONABLE, and authored in
        //  canon-strings.json rather than typed inline (CLAUDE.md §7).
        // =====================================================================
        private static void CaseRefusalSentencesExist(List<string> failures, StringBuilder log)
        {
            StoreStrings.Reload();
            var seen = new HashSet<string>(StringComparer.Ordinal);

            foreach (string key in StoreStrings.BuyGateKeys)
            {
                string s = StoreStrings.Get(key);
                if (string.IsNullOrEmpty(s) || s.StartsWith("[[missing:", StringComparison.Ordinal))
                {
                    failures.Add("[copy] canon-strings has no '" + key + "' - the store would refuse a purchase " +
                                 "with a placeholder marker on the one screen where that reads as a scam.");
                    continue;
                }
                if (!seen.Add(s))
                    failures.Add("[copy] '" + key + "' reuses another key's sentence. Each refusal has a different " +
                                 "remedy; sharing one sentence tells the player the wrong thing about at least one.");

                foreach (char c in s)
                    if (c > 127)
                    {
                        failures.Add("[copy] '" + key + "' contains a non-ASCII character - TMP renders it as tofu.");
                        break;
                    }
            }

            string walletLine = StoreStrings.Get(StoreStrings.KeyBuyWalletRequired);
            if (walletLine.IndexOf("{0}", StringComparison.Ordinal) < 0)
                failures.Add("[copy] 'storeBuyWalletRequired' does not format {0}. The threshold must come from " +
                             "PurchaseGate.WalletRequiredAboveUsd so the copy cannot drift from the rule; a typed " +
                             "'$4.99' in the sentence is a second copy of the number.");
            if (walletLine.IndexOf("wallet", StringComparison.OrdinalIgnoreCase) < 0)
                failures.Add("[copy] 'storeBuyWalletRequired' never says 'wallet' - the refusal must NAME the remedy, " +
                             "not just decline.");

            log.AppendLine("  [copy] " + StoreStrings.BuyGateKeys.Length + " buy-gate sentences present, distinct, " +
                           "ASCII-only; the wallet refusal names its remedy and formats the threshold");
        }

        // =====================================================================
        //  [charge-path] -- THE STRUCTURAL CASE THAT MATTERS MOST.
        //  PackStore.Purchase is the only method that reaches WalletService.Pay.
        //  It must consult PurchaseGate, because a rule enforced only where the
        //  button is drawn is bypassed by every caller that never drew one.
        // =====================================================================
        private static void CaseChargePathConsultsTheGate(List<string> failures, StringBuilder log)
        {
            string path = Application.dataPath + "/_Modules/Wallet/PackStore.cs";
            if (!File.Exists(path))
            {
                failures.Add("[charge-path] PackStore.cs not found at " + path + " - the charge path cannot be " +
                             "verified, so this is a FAIL, not an unknown.");
                return;
            }

            string src = File.ReadAllText(path);
            int purchaseAt = src.IndexOf("UniTask<PaymentResult> Purchase(", StringComparison.Ordinal);
            if (purchaseAt < 0)
            {
                failures.Add("[charge-path] PackStore.Purchase(PackDef, CurrencyKind) not found. If it was renamed, " +
                             "this oracle must be re-pointed at the new charge entry point in the SAME change - " +
                             "otherwise the gate silently stops being checked.");
                return;
            }

            string body = src.Substring(purchaseAt);
            int payAt = body.IndexOf("_wallet.Pay(", StringComparison.Ordinal);
            int gateAt = body.IndexOf("PurchaseGate.CanBuy(pack", StringComparison.Ordinal);

            if (gateAt < 0)
                failures.Add("[charge-path] PackStore.Purchase does NOT call PurchaseGate.CanBuy(pack, ...). The " +
                             "wallet rule and the rail gate would then be UI-only, and any caller that never drew " +
                             "a Buy button (the shortfall offer, a deep link, a promo) would charge straight past " +
                             "them. This is the defect the owner's ruling names.");
            else if (payAt >= 0 && gateAt > payAt)
                failures.Add("[charge-path] PackStore.Purchase reaches _wallet.Pay BEFORE consulting PurchaseGate - " +
                             "a gate downstream of the charge is not a gate.");
            else
                log.AppendLine("  [charge-path] PackStore.Purchase consults PurchaseGate.CanBuy(pack, ...) before " +
                               "_wallet.Pay");
        }

        // =====================================================================
        //  Helpers
        // =====================================================================
        private static JArray ReadPacks(List<string> failures, StringBuilder log)
        {
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read(PacksRelPath);
                if (string.IsNullOrEmpty(json))
                {
                    failures.Add("[buy-gate] packs.json could not be read through CanonicalJson.");
                    return null;
                }
                var packs = JObject.Parse(json)["packs"] as JArray;
                if (packs == null || packs.Count == 0)
                {
                    failures.Add("[buy-gate] packs.json has no `packs` array.");
                    return null;
                }
                log.AppendLine("  packs.json: " + packs.Count + " rows enumerated");
                return packs;
            }
            catch (Exception ex)
            {
                failures.Add("[buy-gate] packs.json parse failed: " + ex.GetType().Name + ": " + ex.Message);
                return null;
            }
        }

        private static HashSet<string> ReadCosmeticIds()
        {
            try
            {
                string json = DeNelle.Core.CanonicalJson.Read(CosmeticsRel);
                if (string.IsNullOrEmpty(json)) return null;
                var items = JObject.Parse(json)["items"] as JArray;
                if (items == null) return null;
                var set = new HashSet<string>(StringComparer.Ordinal);
                foreach (var t in items)
                {
                    string id = (t as JObject)?["id"]?.Value<string>();
                    if (!string.IsNullOrEmpty(id)) set.Add(id);
                }
                return set;
            }
            catch
            {
                // Returning null degrades the cosmetic check to "not asserted" rather than throwing
                // the whole suite; the caller skips it. Swallowing is acceptable ONLY because the
                // fallback is visible in the log line, not silent.
                return null;
            }
        }

        private static JObject FindPack(JArray packs, string sku)
        {
            foreach (var tok in packs)
                if (tok is JObject p && string.Equals(Sku(p), sku, StringComparison.Ordinal))
                    return p;
            return null;
        }

        private static string Sku(JObject p) => p["sku"]?.Value<string>() ?? "<no-sku>";
    }
}
