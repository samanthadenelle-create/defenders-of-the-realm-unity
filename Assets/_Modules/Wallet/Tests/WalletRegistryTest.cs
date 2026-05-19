// =============================================================================
// Wallet — WalletRegistry / wallets.json tests (EditMode)
// -----------------------------------------------------------------------------
// qa-test-plan.md TC-WAL-05 / TC-WAL-11: wallets.json must hold ONLY public
// base58 addresses (zero private keys / seed phrases — spec Part 10) and the
// Rewards Distributor address must be available for the transparency display.
//
// WalletRegistry reads StreamingAssets/Data/Canonical/wallets.json synchronously.
// =============================================================================

using System.IO;
using NUnit.Framework;
using UnityEngine;
using DeNelle.Wallet;

namespace DeNelle.Wallet.Tests
{
    [TestFixture]
    public class WalletRegistryTest
    {
        [SetUp]
        public void SetUp()
        {
            WalletRegistry.Reload();
        }

        // =====================================================================
        //  Public addresses present
        // =====================================================================

        [Test]
        public void rewards_distributor_address_is_available_for_transparency()
        {
            // TC-WAL-11 — the title/settings transparency label reads this.
            var addr = WalletRegistry.RewardsDistributorAddress;
            Assert.That(string.IsNullOrEmpty(addr), Is.False,
                "the Rewards Distributor address must be available.");
            Assert.That(addr.Length, Is.InRange(32, 44),
                "a Solana base58 address is 32–44 chars.");
        }

        [Test]
        public void devnet_purchase_recipient_address_is_available()
        {
            var addr = WalletRegistry.DevnetPurchaseRecipientAddress;
            Assert.That(string.IsNullOrEmpty(addr), Is.False);
            Assert.That(addr.Length, Is.InRange(32, 44));
        }

        [Test]
        public void both_registry_entries_are_flagged_public()
        {
            Assert.That(WalletRegistry.RewardsDistributor.Public, Is.True,
                "wallets.json entries must be flagged public.");
            Assert.That(WalletRegistry.DevnetPurchaseRecipient.Public, Is.True);
        }

        [Test]
        public void registry_addresses_use_only_base58_characters()
        {
            // base58 (Bitcoin/Solana) — no 0, O, I, l. A leaked key would not
            // necessarily violate this, but a corrupt/non-address string would.
            const string base58 = "123456789ABCDEFGHJKLMNPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz";
            foreach (var addr in new[]
            {
                WalletRegistry.RewardsDistributorAddress,
                WalletRegistry.DevnetPurchaseRecipientAddress,
            })
            {
                foreach (var ch in addr)
                    Assert.That(base58.IndexOf(ch), Is.GreaterThanOrEqualTo(0),
                        $"address '{addr}' has non-base58 char '{ch}'.");
            }
        }

        // =====================================================================
        //  No secrets — wallets.json must hold zero private-key material
        // =====================================================================

        [Test]
        public void wallets_json_contains_no_private_key_material()
        {
            // TC-WAL-05 / spec Part 10 — scan the raw file for any field name or
            // phrase that would indicate a private key / seed phrase leaked in.
            var full = Path.Combine(
                Application.streamingAssetsPath, "Data/Canonical/wallets.json");
            Assert.That(File.Exists(full), Is.True, "wallets.json must exist.");

            var text = File.ReadAllText(full).ToLowerInvariant();
            foreach (var forbidden in new[]
            {
                "privatekey", "private_key", "secretkey", "secret_key",
                "seedphrase", "seed_phrase", "mnemonic", "keypair", "signer",
            })
            {
                Assert.That(text.Contains(forbidden), Is.False,
                    $"wallets.json must NEVER contain '{forbidden}' — public addresses only (spec Part 10).");
            }
        }

        [Test]
        public void rewards_distributor_is_not_the_devnet_purchase_recipient()
        {
            // Two distinct wallets of record — a single address would be a
            // copy-paste error in wallets.json.
            Assert.That(WalletRegistry.RewardsDistributorAddress,
                Is.Not.EqualTo(WalletRegistry.DevnetPurchaseRecipientAddress),
                "the Rewards Distributor and the devnet recipient must be distinct wallets.");
        }
    }
}
