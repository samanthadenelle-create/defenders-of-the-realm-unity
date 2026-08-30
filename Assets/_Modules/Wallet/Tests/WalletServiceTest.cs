// =============================================================================
// Wallet — WalletService tests (EditMode)
// -----------------------------------------------------------------------------
// qa-test-plan.md TC-WAL-01 / TC-WAL-03 / TC-WAL-04 / TC-WAL-06: the app-facing
// WalletService must default to the owner-ruled production Mainnet, route connect / balance / pay through
// its IWalletProvider seam, and surface clean failures.
//
// BACKTEST-WITH-STUBS: WalletService depends only on the IWalletProvider
// interface. Two test doubles drive it here:
//   - StubWalletProvider — the shipped devnet mock (also the SDK stand-in).
//   - FakeWalletProvider — a SYNCHRONOUS, fully-controllable double (no
//     UniTask.Delay) so the service's own branch logic (no-wallet guard, null
//     pack, zero-price guard, status events) is tested deterministically with a
//     plain [Test], independent of any timing.
// =============================================================================

using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using DeNelle.Wallet;

namespace DeNelle.Wallet.Tests
{
    /// <summary>
    /// A synchronous, fully-controllable <see cref="IWalletProvider"/> test
    /// double. Every method completes immediately (no <c>UniTask.Delay</c>), so
    /// the awaiting <see cref="WalletService"/> code resolves on the same frame
    /// and a plain <c>[Test]</c> can drive it.
    /// </summary>
    internal sealed class FakeWalletProvider : IWalletProvider
    {
        public string ProviderName => "Fake Provider";
        public bool IsConnected { get; private set; }
        public WalletAccount Account { get; private set; }

        /// <summary>When true, <see cref="Connect"/> resolves to an invalid account.</summary>
        public bool ConnectFails;

        /// <summary>The balance <see cref="GetBalance"/> reports.</summary>
        public WalletBalance Balance = new WalletBalance { Sol = 3d, Usdc = 100d, Skr = 500d };

        /// <summary>When true, <see cref="SendPayment"/> resolves to a failure.</summary>
        public bool PaymentFails;

        public UniTask<WalletAccount> Connect(WalletNetwork network)
        {
            if (ConnectFails)
            {
                IsConnected = false;
                Account = default;
                return UniTask.FromResult<WalletAccount>(default);
            }
            Account = new WalletAccount { Address = "FakeAddr0000000000000000000000000000000000000", WalletName = ProviderName };
            IsConnected = true;
            return UniTask.FromResult(Account);
        }

        public UniTask Disconnect()
        {
            IsConnected = false;
            Account = default;
            return UniTask.CompletedTask;
        }

        public UniTask<WalletBalance> GetBalance(WalletNetwork network)
        {
            return UniTask.FromResult(Balance);
        }

        public UniTask<PaymentResult> SendPayment(
            string packSku, CurrencyKind currency, double amount, WalletNetwork network)
        {
            return UniTask.FromResult(PaymentFails
                ? PaymentResult.Failure(packSku, currency, "Fake provider forced failure.")
                : PaymentResult.Success(packSku, currency, amount, "FAKE-SIGNATURE"));
        }

        /// <summary>When true, the fake reports it can sign and returns FAKE-MSG-SIG.</summary>
        public bool CanSign { get; set; }

        public bool CanSignMessages => CanSign;

        public UniTask<string> SignMessageBase58(string utf8Message)
        {
            return UniTask.FromResult(CanSign ? "FAKE-MSG-SIG" : null);
        }
    }

    [TestFixture]
    public class WalletServiceTest
    {
        /// <summary>Builds a minimal PackDef priced on all three rails.</summary>
        private static PackDef MakePack(string sku = "hearth-spark") => new PackDef
        {
            Sku = sku,
            Tier = 1,
            Name = "Hearth Spark",
            Pricing = new PackPricing { Usd = 4.99, Usdc = 4.99, Sol = 0.05, Skr = 60 },
            Contents = new PackContents(),
        };

        // =====================================================================
        //  Network — devnet by default, mainnet owner-gated  (TC-WAL-03/04)
        // =====================================================================

        [Test]
        public void default_network_constant_is_mainnet()
        {
            Assert.That(WalletService.DefaultNetwork, Is.EqualTo(WalletNetwork.Mainnet),
                "WO-1159 records Mainnet sales as live and permanently moved the production default.");
        }

        [Test]
        public void a_new_service_starts_on_mainnet()
        {
            var service = new WalletService(new FakeWalletProvider());
            Assert.That(service.Network, Is.EqualTo(WalletNetwork.Mainnet));
            Assert.That(service.NetworkLabel, Is.EqualTo("Mainnet"));
        }

        [Test]
        public void selecting_mainnet_logs_the_owner_gated_warning()
        {
            var service = new WalletService(new FakeWalletProvider());
            // Mainnet is owner-gated — SetNetwork must warn but still honour it
            // (an owner may flip it deliberately; the agent never does).
            LogAssert.Expect(UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex("Mainnet selected"));
            service.SetNetwork(WalletNetwork.Mainnet);
            Assert.That(service.Network, Is.EqualTo(WalletNetwork.Mainnet));
            Assert.That(service.NetworkLabel, Is.EqualTo("Mainnet"));
        }

        // =====================================================================
        //  Initial state
        // =====================================================================

        [Test]
        public void a_new_service_is_disconnected()
        {
            var service = new WalletService(new FakeWalletProvider());
            Assert.That(service.Status, Is.EqualTo(WalletStatus.Disconnected));
            Assert.That(service.IsConnected, Is.False);
        }

        [Test]
        public void a_null_provider_falls_back_to_the_stub()
        {
            // The IWalletProvider ctor coalesces a null provider to the stub —
            // the service must always have a live provider.
            var service = new WalletService((IWalletProvider)null);
            Assert.That(service.ProviderName, Is.Not.Null.And.Not.Empty);
        }

        // =====================================================================
        //  Connect — through the synchronous fake
        // =====================================================================

        [Test]
        public void connect_moves_the_service_to_connected_and_raises_the_event()
        {
            var service = new WalletService(new FakeWalletProvider());
            var seen = new System.Collections.Generic.List<WalletStatus>();
            service.StatusChanged += seen.Add;

            var account = service.Connect().GetAwaiter().GetResult();

            Assert.That(account.IsValid, Is.True);
            Assert.That(service.IsConnected, Is.True);
            Assert.That(service.Status, Is.EqualTo(WalletStatus.Connected));
            Assert.That(seen, Does.Contain(WalletStatus.Connecting),
                "a Connecting status must be raised during the handshake.");
            Assert.That(seen, Does.Contain(WalletStatus.Connected));
        }

        [Test]
        public void a_failed_connect_returns_the_service_to_disconnected()
        {
            var service = new WalletService(new FakeWalletProvider { ConnectFails = true });
            var account = service.Connect().GetAwaiter().GetResult();

            Assert.That(account.IsValid, Is.False);
            Assert.That(service.Status, Is.EqualTo(WalletStatus.Disconnected),
                "a connect that yields no account must end Disconnected.");
        }

        [Test]
        public void disconnect_returns_the_service_to_disconnected()
        {
            var service = new WalletService(new FakeWalletProvider());
            service.Connect().GetAwaiter().GetResult();
            service.Disconnect().GetAwaiter().GetResult();
            Assert.That(service.Status, Is.EqualTo(WalletStatus.Disconnected));
            Assert.That(service.IsConnected, Is.False);
        }

        // =====================================================================
        //  GetBalance
        // =====================================================================

        [Test]
        public void get_balance_before_connect_warns_and_returns_zero()
        {
            var service = new WalletService(new FakeWalletProvider());
            LogAssert.Expect(UnityEngine.LogType.Warning,
                new System.Text.RegularExpressions.Regex("GetBalance called with no wallet"));
            var balance = service.GetBalance().GetAwaiter().GetResult();
            Assert.That(balance.Sol, Is.EqualTo(0d));
        }

        [Test]
        public void get_balance_after_connect_returns_the_provider_balance()
        {
            var fake = new FakeWalletProvider
            {
                Balance = new WalletBalance { Sol = 7d, Usdc = 42d, Skr = 999d },
            };
            var service = new WalletService(fake);
            service.Connect().GetAwaiter().GetResult();

            var balance = service.GetBalance().GetAwaiter().GetResult();
            Assert.That(balance.Sol, Is.EqualTo(7d));
            Assert.That(balance.Usdc, Is.EqualTo(42d));
            Assert.That(balance.Skr, Is.EqualTo(999d));
        }

        // =====================================================================
        //  Pay — guards + the happy path
        // =====================================================================

        [Test]
        public void pay_with_a_null_pack_fails_cleanly()
        {
            var service = new WalletService(new FakeWalletProvider());
            service.Connect().GetAwaiter().GetResult();
            // The null-pack guard logs its abort proof via FlowTrace.Fail (Debug.LogError).
            LogAssert.Expect(UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex("pack definition is null"));
            var result = service.Pay(null, CurrencyKind.Sol).GetAwaiter().GetResult();
            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Does.Contain("null"));
        }

        [Test]
        public void pay_before_connect_fails_with_no_wallet()
        {
            var service = new WalletService(new FakeWalletProvider());
            var result = service.Pay(MakePack(), CurrencyKind.Sol).GetAwaiter().GetResult();
            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Does.Contain("No wallet connected"));
        }

        [Test]
        public void pay_for_a_rail_with_no_price_fails()
        {
            var service = new WalletService(new FakeWalletProvider());
            service.Connect().GetAwaiter().GetResult();
            // A pack with a zero SOL price — Pay must reject the unpriced rail.
            var unpriced = new PackDef
            {
                Sku = "no-sol-pack",
                Pricing = new PackPricing { Usd = 1, Usdc = 1, Sol = 0, Skr = 1 },
                Contents = new PackContents(),
            };
            // The unpriced-rail guard logs its abort proof via FlowTrace.Fail (Debug.LogError).
            LogAssert.Expect(UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex("no price for"));
            var result = service.Pay(unpriced, CurrencyKind.Sol).GetAwaiter().GetResult();
            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Does.Contain("no price"));
        }

        [Test]
        public void a_funded_pay_succeeds_and_echoes_the_pack_and_rail()
        {
            // WO-931: the payment seam now requires a real signing provider
            // (IsRealSigningWallet) — a paying fake must attest CanSign.
            var service = new WalletService(new FakeWalletProvider { CanSign = true });
            service.Connect().GetAwaiter().GetResult();

            var result = service.Pay(MakePack(), CurrencyKind.Usdc).GetAwaiter().GetResult();

            Assert.That(result.Ok, Is.True);
            Assert.That(result.PackSku, Is.EqualTo("hearth-spark"));
            Assert.That(result.Currency, Is.EqualTo(CurrencyKind.Usdc));
            Assert.That(result.AmountNative, Is.EqualTo(4.99));
            Assert.That(result.TxSignature, Is.EqualTo("FAKE-SIGNATURE"));
        }

        [Test]
        public void a_provider_payment_failure_surfaces_through_pay()
        {
            // WO-931: CanSign = true so the failure comes from the PROVIDER,
            // not from the new signing-attestation refusal at the seam.
            var service = new WalletService(new FakeWalletProvider { PaymentFails = true, CanSign = true });
            service.Connect().GetAwaiter().GetResult();
            // The provider-failure guard logs its proof via FlowTrace.Fail (Debug.LogError).
            LogAssert.Expect(UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex("FAILED at provider"));
            var result = service.Pay(MakePack(), CurrencyKind.Sol).GetAwaiter().GetResult();
            Assert.That(result.Ok, Is.False,
                "a provider-level payment failure must bubble up as a failed PaymentResult.");
        }

        [Test]
        public void pay_refuses_a_connected_provider_that_cannot_sign()
        {
            // WO-931 belt check: a CONNECTED provider that holds no signing key
            // (CanSignMessages false — e.g. the dev-only DevWalletProbe, which
            // delegates SendPayment to an inner stub) must be refused at the
            // seam, loudly, before the provider is ever asked to "pay".
            var service = new WalletService(new FakeWalletProvider()); // CanSign defaults false
            service.Connect().GetAwaiter().GetResult();
            LogAssert.Expect(UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex("REFUSED"));
            var result = service.Pay(MakePack(), CurrencyKind.Sol).GetAwaiter().GetResult();
            Assert.That(result.Ok, Is.False,
                "a non-signing provider must never settle a payment (WO-931).");
            Assert.That(result.Error, Is.EqualTo(WalletService.NonSigningPaymentRefusalReason));
            Assert.That(result.TxSignature, Is.Null.Or.Empty,
                "a refused payment must carry no transaction signature.");
        }

        [Test]
        public void payflat_refuses_a_connected_provider_that_cannot_sign()
        {
            // WO-931: PayFlat shares the same seam — it reaches the same
            // provider SendPayment and was previously gated by nothing at all.
            var service = new WalletService(new FakeWalletProvider()); // CanSign defaults false
            service.Connect().GetAwaiter().GetResult();
            LogAssert.Expect(UnityEngine.LogType.Error,
                new System.Text.RegularExpressions.Regex("REFUSED"));
            var result = service.PayFlat("test-flat-tx", CurrencyKind.Sol, 0.01).GetAwaiter().GetResult();
            Assert.That(result.Ok, Is.False);
            Assert.That(result.Error, Is.EqualTo(WalletService.NonSigningPaymentRefusalReason));
        }

        // =====================================================================
        //  End-to-end over the SHIPPED StubWalletProvider  (TC-WAL-06)
        // =====================================================================

        [UnityTest]
        public IEnumerator full_connect_balance_flow_over_the_real_stub_and_pay_refuses() => UniTask.ToCoroutine(
            async () =>
            {
                // Create(useStub:true) forces the devnet stub even if the SDK
                // were present — the canonical offline / EditMode path.
                //
                // WO-931 CONTRACT CHANGE (2026-08-10): connect / balance /
                // identity plumbing still run end-to-end over the stub, but a
                // stub PAYMENT now REFUSES at the WalletService.Pay seam. This
                // test used to assert the stub purchase CONFIRMED — that was the
                // free-grant hole itself (fabricated signature → PackStore grant
                // → fake purchase_completed analytics), so it now asserts the
                // refusal. A re-appearing green "stub purchase confirms" is a
                // regression, not a feature.
                var service = WalletService.Create(useStub: true);

                var account = await service.Connect();
                Assert.That(service.IsConnected, Is.True);
                Assert.That(account.IsValid, Is.True);

                var balance = await service.GetBalance();
                Assert.That(balance.Sol, Is.GreaterThan(0d));

                // The refusal is LOUD (FlowTrace.Fail → Debug.LogError), §12.
                LogAssert.Expect(UnityEngine.LogType.Error,
                    new System.Text.RegularExpressions.Regex("REFUSED: stub provider cannot sign"));
                var result = await service.Pay(MakePack(), CurrencyKind.Sol);
                Assert.That(result.Ok, Is.False,
                    "WO-931: a stub-backed payment must REFUSE — a confirming stub purchase is the free-grant hole.");
                Assert.That(result.Error, Is.EqualTo(WalletService.StubPaymentRefusalReason));
                Assert.That(result.TxSignature, Is.Null.Or.Empty,
                    "no fabricated signature may leave the seam.");

                // PayFlat shares the seam (it was gated by nothing before WO-931).
                LogAssert.Expect(UnityEngine.LogType.Error,
                    new System.Text.RegularExpressions.Regex("REFUSED: stub provider cannot sign"));
                var flat = await service.PayFlat("wo931-flat", CurrencyKind.Sol, 0.01);
                Assert.That(flat.Ok, Is.False);
                Assert.That(flat.Error, Is.EqualTo(WalletService.StubPaymentRefusalReason));

                await service.Disconnect();
                Assert.That(service.IsConnected, Is.False);
            });
    }
}
