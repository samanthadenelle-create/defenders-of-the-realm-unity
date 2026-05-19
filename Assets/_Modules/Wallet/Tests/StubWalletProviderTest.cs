// =============================================================================
// Wallet — StubWalletProvider tests (EditMode)
// -----------------------------------------------------------------------------
// qa-test-plan.md TC-WAL-01 / TC-WAL-06: the wallet module must compile and run
// over the devnet StubWalletProvider with the Solana Unity SDK absent (bug-log
// BUG-010). This fixture backtests the stub's connect / balance / pay flow IN
// ISOLATION — the stub IS the test double for the not-yet-resolved SDK, so the
// whole wallet rail is exercisable today.
//
// ASYNC IN EDITMODE: every IWalletProvider method returns UniTask, and the stub
// uses UniTask.Delay to simulate network timing. UniTask.Delay needs the player
// loop pumped, which a plain [Test] does not do — so these are [UnityTest]
// methods returning IEnumerator, driven via UniTask.ToCoroutine(). The Unity
// Test Framework runs an EditMode [UnityTest]'s IEnumerator on the editor update
// loop, so the delays resolve. No PlayMode build is required.
// =============================================================================

using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using DeNelle.Wallet;

namespace DeNelle.Wallet.Tests
{
    [TestFixture]
    public class StubWalletProviderTest
    {
        // =====================================================================
        //  Initial state — before any connect
        // =====================================================================

        [Test]
        public void a_fresh_stub_is_disconnected_with_no_account()
        {
            var stub = new StubWalletProvider();
            Assert.That(stub.IsConnected, Is.False, "a fresh stub must not be connected.");
            Assert.That(stub.Account.IsValid, Is.False, "a fresh stub exposes no account.");
            Assert.That(stub.ProviderName, Is.Not.Null.And.Not.Empty);
        }

        [UnityTest]
        public IEnumerator get_balance_before_connect_returns_a_zero_balance() => UniTask.ToCoroutine(
            async () =>
            {
                var stub = new StubWalletProvider();
                // Documented behaviour: GetBalance with no wallet logs a warning
                // and returns default — it must not throw.
                LogAssert.Expect(UnityEngine.LogType.Warning,
                    "[StubWalletProvider] GetBalance with no wallet connected.");
                var balance = await stub.GetBalance(WalletNetwork.Devnet);
                Assert.That(balance.Sol, Is.EqualTo(0d));
                Assert.That(balance.Usdc, Is.EqualTo(0d));
                Assert.That(balance.Skr, Is.EqualTo(0d));
            });

        [UnityTest]
        public IEnumerator pay_before_connect_fails_with_no_wallet_connected() => UniTask.ToCoroutine(
            async () =>
            {
                var stub = new StubWalletProvider();
                var result = await stub.SendPayment(
                    "hearth-spark", CurrencyKind.Sol, 0.05, WalletNetwork.Devnet);
                Assert.That(result.Ok, Is.False, "a pay with no wallet must fail.");
                Assert.That(result.Error, Does.Contain("connected"));
            });

        // =====================================================================
        //  Connect
        // =====================================================================

        [UnityTest]
        public IEnumerator connect_yields_a_valid_devnet_account() => UniTask.ToCoroutine(
            async () =>
            {
                var stub = new StubWalletProvider();
                var account = await stub.Connect(WalletNetwork.Devnet);

                Assert.That(stub.IsConnected, Is.True, "Connect must flip IsConnected true.");
                Assert.That(account.IsValid, Is.True, "Connect must yield a valid account.");
                Assert.That(account.Address, Is.Not.Null.And.Not.Empty);
                Assert.That(account.Address.Length, Is.EqualTo(44),
                    "a devnet-shaped base58 address is 44 chars.");
                Assert.That(stub.Account.Address, Is.EqualTo(account.Address),
                    "the stub must expose the same account it returned.");
            });

        [UnityTest]
        public IEnumerator short_address_renders_the_compact_form() => UniTask.ToCoroutine(
            async () =>
            {
                var stub = new StubWalletProvider();
                var account = await stub.Connect(WalletNetwork.Devnet);
                Assert.That(account.ShortAddress, Does.Contain("…"),
                    "ShortAddress must collapse the middle of the address.");
            });

        [UnityTest]
        public IEnumerator disconnect_clears_the_connection_and_account() => UniTask.ToCoroutine(
            async () =>
            {
                var stub = new StubWalletProvider();
                await stub.Connect(WalletNetwork.Devnet);
                await stub.Disconnect();

                Assert.That(stub.IsConnected, Is.False, "Disconnect must clear IsConnected.");
                Assert.That(stub.Account.IsValid, Is.False, "Disconnect must drop the account.");
            });

        [UnityTest]
        public IEnumerator disconnect_when_already_disconnected_is_safe() => UniTask.ToCoroutine(
            async () =>
            {
                var stub = new StubWalletProvider();
                await stub.Disconnect(); // never connected — must not throw
                Assert.That(stub.IsConnected, Is.False);
            });

        // =====================================================================
        //  GetBalance — the devnet mock balances
        // =====================================================================

        [UnityTest]
        public IEnumerator connected_balance_is_generous_on_every_rail() => UniTask.ToCoroutine(
            async () =>
            {
                var stub = new StubWalletProvider();
                await stub.Connect(WalletNetwork.Devnet);
                var balance = await stub.GetBalance(WalletNetwork.Devnet);

                // Generous on purpose so all five packs are purchasable.
                Assert.That(balance.Sol, Is.GreaterThan(0d), "mock SOL balance > 0.");
                Assert.That(balance.Usdc, Is.GreaterThan(0d), "mock USDC balance > 0.");
                Assert.That(balance.Skr, Is.GreaterThan(0d), "mock SKR balance > 0.");
            });

        [UnityTest]
        public IEnumerator wallet_balance_for_returns_the_per_rail_amount() => UniTask.ToCoroutine(
            async () =>
            {
                var stub = new StubWalletProvider();
                await stub.Connect(WalletNetwork.Devnet);
                var balance = await stub.GetBalance(WalletNetwork.Devnet);

                Assert.That(balance.For(CurrencyKind.Sol), Is.EqualTo(balance.Sol));
                Assert.That(balance.For(CurrencyKind.Usdc), Is.EqualTo(balance.Usdc));
                Assert.That(balance.For(CurrencyKind.Skr), Is.EqualTo(balance.Skr));
            });

        // =====================================================================
        //  SendPayment — the simulated devnet transfer
        // =====================================================================

        [UnityTest]
        public IEnumerator a_successful_payment_returns_a_tx_signature() => UniTask.ToCoroutine(
            async () =>
            {
                var stub = new StubWalletProvider();
                await stub.Connect(WalletNetwork.Devnet);

                var result = await stub.SendPayment(
                    "hearth-spark", CurrencyKind.Sol, 0.05, WalletNetwork.Devnet);

                Assert.That(result.Ok, Is.True, "a funded payment must succeed.");
                Assert.That(result.TxSignature, Is.Not.Null.And.Not.Empty,
                    "a confirmed devnet tx must carry a signature.");
                Assert.That(result.TxSignature.Length, Is.EqualTo(88),
                    "a Solana tx signature is ~88 base58 chars.");
                Assert.That(result.PackSku, Is.EqualTo("hearth-spark"));
                Assert.That(result.Currency, Is.EqualTo(CurrencyKind.Sol));
                Assert.That(result.AmountNative, Is.EqualTo(0.05));
            });

        [UnityTest]
        public IEnumerator a_payment_debits_the_mock_balance() => UniTask.ToCoroutine(
            async () =>
            {
                var stub = new StubWalletProvider();
                await stub.Connect(WalletNetwork.Devnet);

                var before = (await stub.GetBalance(WalletNetwork.Devnet)).Sol;
                await stub.SendPayment("hearth-spark", CurrencyKind.Sol, 0.05, WalletNetwork.Devnet);
                var after = (await stub.GetBalance(WalletNetwork.Devnet)).Sol;

                Assert.That(after, Is.EqualTo(before - 0.05).Within(1e-9),
                    "a successful payment must debit the mock SOL balance.");
            });

        [UnityTest]
        public IEnumerator an_overpriced_payment_fails_with_insufficient_funds() => UniTask.ToCoroutine(
            async () =>
            {
                var stub = new StubWalletProvider();
                await stub.Connect(WalletNetwork.Devnet);

                // Far beyond the generous mock SOL balance.
                var result = await stub.SendPayment(
                    "founders-vow", CurrencyKind.Sol, 999999d, WalletNetwork.Devnet);

                Assert.That(result.Ok, Is.False, "an unaffordable payment must fail.");
                Assert.That(result.Error, Does.Contain("Insufficient"));
                Assert.That(result.TxSignature, Is.Null.Or.Empty,
                    "a failed payment carries no tx signature.");
            });

        [UnityTest]
        public IEnumerator a_failed_payment_does_not_debit_the_balance() => UniTask.ToCoroutine(
            async () =>
            {
                var stub = new StubWalletProvider();
                await stub.Connect(WalletNetwork.Devnet);

                var before = (await stub.GetBalance(WalletNetwork.Devnet)).Sol;
                await stub.SendPayment("founders-vow", CurrencyKind.Sol, 999999d, WalletNetwork.Devnet);
                var after = (await stub.GetBalance(WalletNetwork.Devnet)).Sol;

                Assert.That(after, Is.EqualTo(before),
                    "a rejected payment must leave the balance untouched.");
            });

        [UnityTest]
        public IEnumerator each_currency_rail_debits_its_own_balance() => UniTask.ToCoroutine(
            async () =>
            {
                var stub = new StubWalletProvider();
                await stub.Connect(WalletNetwork.Devnet);

                var start = await stub.GetBalance(WalletNetwork.Devnet);
                await stub.SendPayment("lanternlight", CurrencyKind.Usdc, 10d, WalletNetwork.Devnet);
                var afterUsdc = await stub.GetBalance(WalletNetwork.Devnet);

                Assert.That(afterUsdc.Usdc, Is.EqualTo(start.Usdc - 10d).Within(1e-9),
                    "a USDC payment debits USDC.");
                Assert.That(afterUsdc.Sol, Is.EqualTo(start.Sol),
                    "a USDC payment must not touch the SOL balance.");
                Assert.That(afterUsdc.Skr, Is.EqualTo(start.Skr),
                    "a USDC payment must not touch the SKR balance.");
            });

        [UnityTest]
        public IEnumerator reconnect_resets_the_mock_balance_to_starting_values() => UniTask.ToCoroutine(
            async () =>
            {
                var stub = new StubWalletProvider();
                await stub.Connect(WalletNetwork.Devnet);
                await stub.SendPayment("hearth-spark", CurrencyKind.Sol, 1d, WalletNetwork.Devnet);
                var spent = (await stub.GetBalance(WalletNetwork.Devnet)).Sol;

                await stub.Disconnect();
                await stub.Connect(WalletNetwork.Devnet); // fresh handshake
                var fresh = (await stub.GetBalance(WalletNetwork.Devnet)).Sol;

                Assert.That(fresh, Is.GreaterThan(spent),
                    "a re-connect must reset the mock balance to the generous starting value.");
            });
    }
}
