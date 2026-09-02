// =============================================================================
// StorePiSkinCurrencyRegression - WO-1323.
// -----------------------------------------------------------------------------
// THE DEFECT THIS PINS, read off the owner's own felt-test in REAL Pi Browser
// (2026-09-02). She was signed in as Pi - the title chip read "Pi: samanthadenelle"
// and the session log read:
//
//     Currency skin resolved: 'pi' (auth=PiSdk, symbol=pi, identity=PiUid)
//
// and the Night Market quoted her "1022 SKR", "2555 SKR", "511 SKR",
// "BUY - 255 SKR", offered "Connect a wallet to see your balance", and showed a
// Mainnet/SKR readiness chip.
//
// SKR is Solana Mobile's governance token. This game has never minted it, never held
// it and does not own it - PackStore says so in its own header - and a Pi player
// cannot spend it. Quoting it at her is the same error that comment forbids, aimed at
// the wrong audience.
//
// ── WHAT THIS ORACLE PROVES, AND WHY EACH HALF IS NEEDED ─────────────────────
//   A. UNDER THE PI SKIN, NO SKR STRING IS REACHABLE IN THE STORE SURFACE. Every
//      site that can print an SKR figure or Solana wallet furniture is behind the
//      PiDisplay hinge, and that hinge is resolved from the SKIN (which demonstrably
//      resolved correctly in the field) rather than from the payment CHANNEL (which
//      demonstrably did not register).
//   B. UNDER THE SKR SKIN NOTHING CHANGES. Proved by the shape of every guard: each
//      one is an ADDED `PiDisplay` test in front of behaviour that is otherwise
//      byte-identical, and the SKR strings/paths themselves are asserted STILL PRESENT.
//      A "fix" that deleted the SKR path would fail this suite, not pass it.
//   C. THE CLIENT NEVER PRICES ANYTHING. No `pi` constant in packs.json, no second
//      HTTP client, and a refused/expired quote CLEARS the cached figure instead of
//      leaving the last one on the shelf.
//   D. THE EMPTY PI SHELF IS AN HONEST STATE, NOT A FLAG FLIP. hearth-spark is the
//      only Pi-quotable sku and it is storeVisible:false by WO-1069; this suite pins
//      that flag DOWN, so nobody can resolve an empty Pi shelf by reversing a pricing
//      ruling with a display change.
//
// Marker: STORE_PI_SKIN_OK / STORE_PI_SKIN_FAIL (unique repo-wide).
// =============================================================================

using System;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Wallet;

namespace DeNelle.Editor.Regression
{
    /// <summary>WO-1323: the Pi skin's store never speaks SKR, and the SKR skin is untouched.</summary>
    public static class StorePiSkinCurrencyRegression
    {
        public static void RunAll()
        {
            if (Run(out var reason)) Debug.Log("STORE_PI_SKIN_OK - " + reason);
            else Debug.LogError("STORE_PI_SKIN_FAIL: " + reason);
        }

        public static bool Run(out string reason)
        {
            var fail = new List<string>();
            int checks = 0;

            try
            {
                string root = Directory.GetParent(Application.dataPath).FullName;

                string storePath = Path.Combine(root, "Assets/_Modules/Wallet/PackStore.cs");
                string piPath = Path.Combine(root,
                    "Assets/_Modules/Core/Payments/Providers/Pi/PiBrowserPaymentProvider.cs");
                string canonAPath = Path.Combine(root, "Assets/Resources/Data/Canonical/canon-strings.json");
                string canonBPath = Path.Combine(root, "Assets/StreamingAssets/Data/Canonical/canon-strings.json");
                string packsAPath = Path.Combine(root, "Assets/Resources/Data/Canonical/packs.json");
                string packsBPath = Path.Combine(root, "Assets/StreamingAssets/Data/Canonical/packs.json");

                foreach (string p in new[] { storePath, piPath, canonAPath, canonBPath, packsAPath, packsBPath })
                    if (!File.Exists(p)) fail.Add("missing file " + p);
                if (fail.Count > 0)
                {
                    // An unreadable input is a FAIL, never a quiet green (WO-1138).
                    reason = string.Join("; ", fail);
                    return false;
                }

                string store = File.ReadAllText(storePath);
                string pi = File.ReadAllText(piPath);
                string canonA = File.ReadAllText(canonAPath);
                string canonB = File.ReadAllText(canonBPath);

                // =============================================================
                //  1. THE COPY
                // =============================================================
                if (!string.Equals(canonA, canonB, StringComparison.Ordinal))
                    fail.Add("the two canonical canon-strings.json copies differ");
                checks++;

                var canon = JObject.Parse(canonA);
                var seen = new HashSet<string>(StringComparer.Ordinal);
                string[] piKeys = StoreStrings.PiSkinKeys;   // the class's OWN list - never retyped here
                if (piKeys == null || piKeys.Length < 6)
                    fail.Add("StoreStrings.PiSkinKeys is missing or shrank below the six authored Pi keys");
                checks++;

                foreach (string key in piKeys ?? new string[0])
                {
                    string value = (string)canon[key];
                    if (string.IsNullOrWhiteSpace(value)) { fail.Add("missing Pi-skin key " + key); continue; }

                    // ASCII only - TMP renders anything else as tofu.
                    foreach (char c in value)
                        if (c > 127) { fail.Add(key + " is not ASCII-clean"); break; }

                    // ⛔ THE POINT OF THE WHOLE WORK ORDER: the Pi player's copy may not name SKR.
                    if (value.IndexOf("SKR", StringComparison.OrdinalIgnoreCase) >= 0)
                        fail.Add(key + " names SKR in copy shown to a Pi player: " + value);

                    if (!seen.Add(value))
                        fail.Add("two Pi-skin keys share the same sentence: " + value);
                    checks++;
                }

                // B: the SKR copy is STILL THERE, unchanged. A pass earned by deleting the SKR
                // strings would be the opposite of "the SKR skin is byte-for-byte unchanged".
                RequireCanon(canon, "storeBalanceValue", "SKR", fail, ref checks);
                RequireCanon(canon, "storeBalanceAfter", "SKR", fail, ref checks);
                RequireCanon(canon, "storeBalanceNoWallet", "Connect a wallet", fail, ref checks);

                // =============================================================
                //  2. THE HINGE IS THE SKIN, NOT THE CHANNEL
                // =============================================================
                Require(store, "private static bool PiDisplay => PiSkinActive || PiRailOwnsTheStore;",
                    "the Pi display hinge is gone or renamed", fail, ref checks);
                Require(store, "skin.AuthMode == SkinAuthMode.PiSdk",
                    "PiSkinActive no longer reads the resolved SKIN auth mode - the field session proved the " +
                    "payment CHANNEL is the signal that fails, so a channel-only hinge reopens this defect",
                    fail, ref checks);
                Require(store, "string.Equals(skin.SkinId, \"pi\", StringComparison.Ordinal)",
                    "PiSkinActive no longer pins skin id 'pi' - the generic 'wallet' skin also carries PiSdk " +
                    "auth and would be dragged into Pi wording", fail, ref checks);
                Require(store, "provider.Channel == PaymentChannel.PiBrowser",
                    "PiRailOwnsTheStore no longer asks who takes the money - who-is-looking and who-charges " +
                    "are two questions and both must survive", fail, ref checks);

                // =============================================================
                //  3. EVERY SKR SITE IS BEHIND THE HINGE
                // =============================================================
                // The header chip. Guarded BEFORE the switch, so no state added later can reach the
                // "  SKR" identity line at the tail of RenderBalanceLabel.
                RequireOrdered(store,
                    "private void RenderBalanceLabel()",
                    "if (PiDisplay)",
                    "switch (_balanceState)",
                    "RenderBalanceLabel does not test PiDisplay BEFORE its balance-state switch",
                    fail, ref checks);
                Require(store, "StoreStrings.Get(StoreStrings.KeyPiHeaderNotice)",
                    "the Pi header notice never replaces the SKR wallet chip", fail, ref checks);

                // ...and the SKR chip itself is still there for the SKR skin.
                Require(store, "_wallet.NetworkLabel + \"  SKR\"",
                    "the SKR wallet chip was DELETED rather than guarded - that breaks the SKR skin", fail, ref checks);

                // The async read that produces it.
                RequireOrdered(store,
                    "private async UniTaskVoid RefreshWalletMirror()",
                    "if (PiDisplay)",
                    "WalletEndpoints.SkrMint(_wallet.Network)",
                    "RefreshWalletMirror still reads the SKR mint under the Pi skin", fail, ref checks);

                // The balance-after promise ("Wallet after: {0} SKR").
                Require(store, "if (!PiDisplay && roomForBalance && _balanceState == BalanceState.Known",
                    "the balance-after preview is not excluded by name under the Pi skin", fail, ref checks);

                // The Solana network marker.
                Require(store, "if (!PiDisplay && roomForNetwork && _wallet != null && _wallet.Network == WalletNetwork.Devnet)",
                    "the DEVNET marker is still drawn under the Pi skin", fail, ref checks);
                Require(store, "DEVNET - TEST TOKEN",
                    "the DEVNET marker was DELETED rather than guarded - that breaks the SKR skin", fail, ref checks);

                // The Solana connect door.
                Require(store, "if (walletIsTheBlocker && PiDisplay)",
                    "a Pi player can still be sent into the Solana wallet-connect flow", fail, ref checks);
                Require(store, "StoreStrings.KeyPiWalletGate",
                    "the wallet-ceiling refusal has no Pi wording", fail, ref checks);
                Require(store, "PurchaseGate.FormatUsd(PurchaseGate.WalletRequiredAboveUsd)",
                    "the Pi wallet-gate sentence hardcodes its threshold instead of formatting the constant",
                    fail, ref checks);

                // No Pi rail -> no Buy control, and NO fallback to the Solana path.
                Require(store, "if (PiDisplay && !PiRailOwnsTheStore)",
                    "a Pi-skinned session with no Pi rail can still fall through to the Solana Buy path",
                    fail, ref checks);
                Require(store, "StoreStrings.KeyPiRailUnavailable",
                    "there is no worded refusal for a Pi session with no Pi rail", fail, ref checks);

                // The SKR price label survives, and is only reached when PiDisplay is false.
                RequireOrdered(store,
                    "private string StorePriceMajor(PackDef pack)",
                    "if (PiDisplay)",
                    "return pack != null ? pack.AmountLabel(_defaultCurrency) : string.Empty;",
                    "StorePriceMajor can still reach the SKR amount label under the Pi skin", fail, ref checks);
                RequireOrdered(store,
                    "private string StorePriceMinor(PackDef pack)",
                    "if (PiDisplay)",
                    "return pack != null ? pack.UsdApprox() : string.Empty;",
                    "StorePriceMinor is not Pi-aware", fail, ref checks);

                // The empty Pi shelf is SHOWN, not papered over.
                Require(store, "BuildPiShelfNoticeIfNothingIsBuyable();",
                    "the empty-Pi-shelf notice is never called from Render", fail, ref checks);
                Require(store, "StoreStrings.KeyPiShelfEmpty",
                    "there is no sentence for a shelf on which nothing is Pi-purchasable", fail, ref checks);

                // =============================================================
                //  4. THE CLIENT NEVER PRICES ANYTHING
                // =============================================================
                Require(store, "RefreshPiDisplayPrices();",
                    "the store never asks the server for the shelf's Pi figures", fail, ref checks);
                Require(store, "PaymentProviders.Current as IDisplayPriceRefresher",
                    "the Pi display-price refresh no longer goes through the provider seam", fail, ref checks);
                if (store.IndexOf("UnityWebRequest", StringComparison.Ordinal) >= 0)
                    fail.Add("PackStore opened its own HTTP client - the ONE Pi endpoint client is " +
                             "PiPaymentEndpoints and there must never be a second");
                checks++;

                Require(pi, "PiPaymentEndpoints.RequestQuoteAsync(sku, uid)",
                    "the Pi display refresh does not use the existing /api/pi/quote client", fail, ref checks);
                Require(pi, "if (_displayQuotes.Remove(sku)) changed = true;",
                    "a refused quote no longer CLEARS the cached shelf figure - the store would keep drawing " +
                    "a price the server just refused", fail, ref checks);
                Require(pi, "DisplayQuoteTtlSeconds",
                    "the shelf's Pi figure no longer expires, so a stale rate can sit on the shelf",
                    fail, ref checks);
                if (pi.IndexOf("UnityWebRequest", StringComparison.Ordinal) >= 0)
                    fail.Add("PiBrowserPaymentProvider opened a second HTTP client");
                checks++;
                // ⛔ The display refresh must NEVER raise a Pi consent sheet: it runs on store OPEN,
                // unattended. Read the method's OWN body (definition -> the next section rule) rather
                // than the whole file, so the legitimate purchase-time call cannot mask a new one here.
                checks++;
                string refreshBody = Between(pi,
                    "private async UniTaskVoid RefreshDisplayPricesAsync",
                    "// -----------------------------------------------------------------");
                if (refreshBody == null)
                    fail.Add("RefreshDisplayPricesAsync is gone - the shelf has no server price source");
                else if (refreshBody.IndexOf("EnsurePaymentsScope", StringComparison.Ordinal) >= 0)
                    fail.Add("the display-price refresh asks for the Pi payments scope - that puts a consent " +
                             "sheet in front of a player who only browsed");
                else if (refreshBody.IndexOf("PiPaymentEndpoints.RequestQuoteAsync", StringComparison.Ordinal) < 0)
                    fail.Add("the display-price refresh no longer asks the server for the price");

                // =============================================================
                //  5. NO AUTHORED PI PRICE, AND NO FLIPPED SHELF FLAG
                // =============================================================
                foreach (string packsPath in new[] { packsAPath, packsBPath })
                {
                    // packs.json is an OBJECT whose "packs" member holds the array -
                    // {"version":..,"packs":[...]}. Parsing the file as a JArray threw
                    // JsonReaderException("Current JsonReader item is not an array: StartObject")
                    // on the suite's FIRST real run, which Guard.Try caught and reported as an
                    // unlabelled failure - the whole suite never reached its OK/FAIL marker, so it
                    // was neither green nor legibly red. Read the member, and refuse loudly if the
                    // shape is not what we expect rather than throwing again.
                    var packsDoc = JObject.Parse(File.ReadAllText(packsPath));
                    var packs = packsDoc["packs"] as JArray;
                    if (packs == null)
                    {
                        fail.Add("packs.json at '" + packsPath + "' has no 'packs' array - the shape " +
                                 "this suite reads changed, and a shape change here silently stops " +
                                 "every assertion below from running");
                        continue;
                    }
                    bool sawHearthSpark = false;
                    foreach (var packToken in packs)
                    {
                        var pack = packToken as JObject;
                        if (pack == null) continue;
                        string sku = (string)pack["sku"];

                        var pricing = pack["pricing"] as JObject;
                        if (pricing != null && pricing["pi"] != null)
                            fail.Add("packs.json authored a static 'pi' price on '" + sku +
                                     "' - the Pi amount is a live server derivation (CoinGecko low_24h), " +
                                     "never a constant that drifts the moment the rate moves");

                        if (string.Equals(sku, "hearth-spark", StringComparison.Ordinal))
                        {
                            sawHearthSpark = true;
                            var visible = pack["storeVisible"];
                            if (visible != null && (bool)visible)
                                fail.Add("hearth-spark storeVisible was flipped TRUE - WO-1069 shelved it as " +
                                         "dominated by starters-hand, and an empty Pi shelf must not be " +
                                         "resolved by reversing a pricing ruling");
                        }
                    }
                    if (!sawHearthSpark)
                        fail.Add("hearth-spark is absent from " + Path.GetFileName(packsPath) +
                                 " - it is the only Pi-quotable sku");
                    // One assertion per FILE, not per pack: 28 rows must not inflate the count and
                    // let the source half of this suite rot behind a big number.
                    checks += 2;
                }

                if (checks < 30)
                    fail.Add("only " + checks + " assertions ran - the suite degenerated into a hollow pass");
            }
            catch (Exception e)
            {
                reason = "threw " + e.GetType().Name + ": " + e.Message;
                return false;
            }

            if (fail.Count > 0)
            {
                reason = fail.Count + " failure(s): " + string.Join(" | ", fail);
                return false;
            }

            reason = checks + " assertions: Pi skin reaches no SKR string in the store surface; " +
                     "prices come from /api/pi/quote and expire; SKR skin paths and copy intact; " +
                     "no authored 'pi' price and no flipped storeVisible.";
            return true;
        }

        // -----------------------------------------------------------------
        //  helpers
        // -----------------------------------------------------------------

        private static void Require(string body, string needle, string why, List<string> fail, ref int checks)
        {
            checks++;
            if (body.IndexOf(needle, StringComparison.Ordinal) < 0) fail.Add(why + " (missing: " + needle + ")");
        }

        private static void RequireCanon(JObject canon, string key, string needle, List<string> fail, ref int checks)
        {
            checks++;
            string value = (string)canon[key];
            if (string.IsNullOrWhiteSpace(value) || value.IndexOf(needle, StringComparison.Ordinal) < 0)
                fail.Add("canon key " + key + " no longer reads '" + needle +
                         "' - the SKR skin's own copy must be untouched by this work order");
        }

        /// <summary>
        /// Proves <paramref name="guard"/> appears between <paramref name="from"/> and
        /// <paramref name="guarded"/>. ORDER is the assertion: a PiDisplay test that sits AFTER the
        /// SKR line it is meant to prevent is not a guard, and a plain "both strings exist" check
        /// cannot tell the two apart.
        /// </summary>
        private static void RequireOrdered(string body, string from, string guard, string guarded,
                                           string why, List<string> fail, ref int checks)
        {
            checks++;
            int start = body.IndexOf(from, StringComparison.Ordinal);
            if (start < 0) { fail.Add(why + " (anchor missing: " + from + ")"); return; }
            int guardAt = body.IndexOf(guard, start, StringComparison.Ordinal);
            int guardedAt = body.IndexOf(guarded, start, StringComparison.Ordinal);
            if (guardedAt < 0) { fail.Add(why + " (guarded line missing: " + guarded + ")"); return; }
            if (guardAt < 0 || guardAt > guardedAt) fail.Add(why + " (guard '" + guard + "' does not precede it)");
        }

        /// <summary>
        /// The text from <paramref name="from"/> up to the next <paramref name="until"/>, or null if
        /// the opening anchor is gone. Used to read ONE method's body so a legitimate call elsewhere
        /// in the file cannot mask (or manufacture) a finding inside it.
        /// </summary>
        private static string Between(string body, string from, string until)
        {
            int start = body.IndexOf(from, StringComparison.Ordinal);
            if (start < 0) return null;
            int end = body.IndexOf(until, start, StringComparison.Ordinal);
            return end < 0 ? body.Substring(start) : body.Substring(start, end - start);
        }
    }
}
