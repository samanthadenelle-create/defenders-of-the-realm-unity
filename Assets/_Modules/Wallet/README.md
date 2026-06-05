# Wallet — `DeNelle.Wallet`

Monetization + crypto wallet layer (~70% built — **do NOT greenfield**, see
CLAUDE.md §8). Store scene-wiring currently DISABLED pending own PanelSettings.

## Files

- `PackStore`, `PackCatalog` — the existing store implementation
- `WalletService`, `WalletRegistry`, `WalletEndpoints` — wallet abstraction
- `SolanaWalletProvider`, `StubWalletProvider` — providers (stub for dev/tests)
- `CryptoPaymentManager`, `WalletConnectDialog`
- `Tests/` — wallet service/registry/stub provider tests

Related: `Web3/` module (Jupiter swap), `docs/monetization-v2-spec.md`,
`docs/wallets-of-record.md`, WO-72–80, WO-131.

> Maintenance: update this README when files are added/removed.
