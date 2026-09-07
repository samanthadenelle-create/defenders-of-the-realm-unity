# WO-1583 RESULT - boot never signs; the signature is spent only on a purchase, a code, or a tap

**Status:** IMPLEMENTED - 2026-09-07, **uncommitted, awaiting gate**. No Unity run, no git (lane rule).
**Files:** `Core/Web3/BackendRequestSigner.cs`, `Wallet/WalletSkinBootstrap.cs`,
`Core/State/GameStateService.cs`, `Editor/Regression/BackendSaveAuthRegression.cs`,
`Editor/Regression/WalletConnectFailureAttributionRegression.cs`,
`Tests/EditMode/LoginSurfacePlatformTests.cs`, plus WO-1441's SUPERSEDED block and the numbering
banner (bumped 1583 -> 1584 in the same edit).
**Checked here:** brace balance + NUL scan clean on all six `.cs`. The `18/14` raw brace count in
`BackendSaveAuthRegression.cs` is pre-existing at HEAD (regex literals), proven with `git show HEAD:`.
Every new suite pin was executed against the edited files using the oracles' own Strip/Method logic -
all pass.

## The line the owner's next device log must show

```
[Flow:Wallet] boot never signs (ruling 2026-09-07)
```

One per auto-resume, in whichever of three forms applies - reused / RENEWED / **restored IDENTITY
without a signature** (the expected cold-boot form, followed by `[Flow:Sync] session absent, save
queued`).

**PASS = that line present in the boot window AND `MintSessionAsync why=explicit-connect` ABSENT from
it.** The mint line must reappear only after a purchase, a promo redeem, or a Connect tap, with
`[Flow:Sync] offline queue DRAINED` following within seconds.

## Boot path, before and after

- BEFORE: `WalletSkinBootstrap.TryAutoResumeAsync` -> `ConnectForLoginAsync()` ->
  `:375 MintSessionForExplicitConnectAsync` -> `MintSessionAsync` -> `SignMessageBase58`. A wallet
  sheet on every launch.
- AFTER: `WalletSkinBootstrap.cs:196` `ConnectForLoginAsync(explicitConnect: false)` -> `:410`
  `BackendRequestSigner.TryResumeSessionWithoutSigningAsync` (`BackendRequestSigner.cs:377`) - reuse,
  else renew when `expired`, else trace and return false. No `SignMessage` on the path.

## Mint sites that remain (all player-initiated)

1. `WalletSkinBootstrap.cs:280` - SKR corner Connect button.
2. `WalletSkinBootstrap.cs:408` - login-surface Connect Wallet tap (`explicitConnect: true`, registered
   as a lambda at `:73`).
3. `BackendRequestSigner.TryAttachAsync` -> `IsPurchaseRoute` -> `allowMint` - any `/api/purchases/*`.
4. `PromoCodeService.cs:168` - `allowInteractiveSessionMint: true` on redeem.

## Carry forward

The brief's premise that the client persists and restores the session token is **false at source**
(`BackendRequestSigner.cs:58-68` - memory-only, deliberately). A cold boot has nothing to restore or
renew, so a wallet holder has no cloud save until they buy, redeem, or tap Connect; saves queue offline
until then. Closing that without a boot sheet needs a sealed persisted token - a separate ruling.
`TryRenewSessionAsync`'s server half is still held behind WO-1446's `signed_at` migration.
Follow-up candidate: the per-call `!allowMint` Warn in `TryAttachSession` now fires once per sync
attempt all session - latch it like `ReportSaveAuthAborted` before it crowds the logcat ring.
