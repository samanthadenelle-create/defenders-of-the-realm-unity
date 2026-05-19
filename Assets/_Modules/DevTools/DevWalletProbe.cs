// =============================================================================
// DevWalletProbe — a DEV-ONLY IWalletProvider with QA-settable mock balances.
// -----------------------------------------------------------------------------
// The shipped StubWalletProvider hard-codes generous fixed devnet balances
// (5 SOL / 250 USDC / 2000 SKR) — fine for a normal dev run, but QA sometimes
// needs to test the store's INSUFFICIENT-FUNDS path, or a specific balance for
// a pack-purchase test case (qa-test-plan TC-WAL-08..10). This provider lets
// the DevPanel's "MOCK wallet balance" actions set each rail's balance live.
//
// ── RELEASE-SAFE ─────────────────────────────────────────────────────────────
// Whole file is `#if DEVELOPMENT_BUILD || UNITY_EDITOR`; the DeNelle.DevTools
// asmdef also carries the matching define constraint. None of it ships.
//
// It is a thin wrapper: it delegates Connect / Disconnect / SendPayment to a
// real StubWalletProvider, and only OVERRIDES GetBalance with the QA-set
// numbers. The mock balances are static so the DevPanel can set them before
// any WalletService is even constructed (the panel and the wallet screen are
// usually different objects / scenes).
//
// To make the mocked numbers visible, the wallet-using screen must build its
// WalletService over a DevWalletProbe — see the integrator notes at the foot
// of DevPanelController.cs and docs/port-notes/dev-panel.md.
// =============================================================================

#if DEVELOPMENT_BUILD || UNITY_EDITOR

using Cysharp.Threading.Tasks;
using DeNelle.Wallet;
using UnityEngine;

namespace DeNelle.DevTools
{
    /// <summary>
    /// DEV-ONLY <see cref="IWalletProvider"/> that reports QA-settable mock
    /// balances. Delegates connect / pay to a real <see cref="StubWalletProvider"/>;
    /// overrides only <see cref="GetBalance"/>. Compiled out of release builds.
    /// </summary>
    public sealed class DevWalletProbe : IWalletProvider
    {
        // Static so the DevPanel can set balances before any WalletService /
        // probe instance exists. Seeded from the shipped stub's defaults.
        private static WalletBalance _mockBalance = new WalletBalance
        {
            Sol = 5d,
            Usdc = 250d,
            Skr = 2000d,
        };

        /// <summary>
        /// Sets the mock balance for one currency rail. Called by the DevPanel's
        /// "MOCK wallet balance" actions. Applies to every <see cref="DevWalletProbe"/>.
        /// </summary>
        public static void SetMockBalance(CurrencyKind currency, double amount)
        {
            amount = amount < 0d ? 0d : amount;
            switch (currency)
            {
                case CurrencyKind.Sol: _mockBalance.Sol = amount; break;
                case CurrencyKind.Usdc: _mockBalance.Usdc = amount; break;
                case CurrencyKind.Skr: _mockBalance.Skr = amount; break;
            }
            Debug.Log($"[DevWalletProbe] Mock {currency} balance set to {amount:0.###}.");
        }

        /// <summary>The current QA-set mock balance across all three rails.</summary>
        public static WalletBalance MockBalance => _mockBalance;

        // ── Delegated to a real stub for everything except GetBalance ────────
        private readonly StubWalletProvider _inner = new StubWalletProvider();

        /// <inheritdoc/>
        public string ProviderName => "Dev Wallet Probe (mock balances)";

        /// <inheritdoc/>
        public bool IsConnected => _inner.IsConnected;

        /// <inheritdoc/>
        public WalletAccount Account => _inner.Account;

        /// <inheritdoc/>
        public UniTask<WalletAccount> Connect(WalletNetwork network) => _inner.Connect(network);

        /// <inheritdoc/>
        public UniTask Disconnect() => _inner.Disconnect();

        /// <inheritdoc/>
        public async UniTask<WalletBalance> GetBalance(WalletNetwork network)
        {
            if (!_inner.IsConnected)
            {
                Debug.LogWarning("[DevWalletProbe] GetBalance with no wallet connected.");
                return default;
            }
            // A small simulated RPC round-trip, then the QA-set numbers.
            await UniTask.Delay(80);
            return _mockBalance;
        }

        /// <inheritdoc/>
        public UniTask<PaymentResult> SendPayment(
            string packSku, CurrencyKind currency, double amount, WalletNetwork network)
        {
            return _inner.SendPayment(packSku, currency, amount, network);
        }
    }
}

#endif // DEVELOPMENT_BUILD || UNITY_EDITOR
