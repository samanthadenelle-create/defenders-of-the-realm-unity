# WO-1441 RESULT - the client now mints the wallet session at connect; proof on device is still owed

**Status:** FIXED - ON THE SEEKER `2026.09.07.358574` (installed 2026-09-06 19:20, package version read
back from the device). Awaiting the owner's felt-verify and a post-fix device capture for acceptance 2-4.
**Commit:** `32659c0f6` (2026-09-06 16:51). The client fix was bundled under the title
`feat(manage,build): ...` and the WO Status was not flipped in that commit - this RESULT closes that
gap (CLAUDE.md §2 same-commit rule, missed).
**Files:** `Assets/_Modules/Core/Web3/BackendRequestSigner.cs` (+237), `Assets/_Modules/Wallet/WalletSkinBootstrap.cs`,
`Assets/_Modules/Wallet/WalletService.cs`, `Assets/_Modules/Wallet/NightMarketSharedCardSession.cs`,
`Assets/_Modules/Core/State/GameStateService.cs` (+47), suite `Assets/Editor/Regression/WalletConnectFailureAttributionRegression.cs`.
**Gates on fresh logs postdating the commit:** `COMPILE_GATE_OK` (`Builds/cg-final.log` 18:48),
`REGRESSION_OK 414/414` (`Builds/reg-final2.log` 18:50), `APK_OK` + `R2_PARITY_OK objects=271` (19:19).

---

## 1. Root cause, from the captured device log (read-only diagnosis, 2026-09-06 19:15-19:20)

The log `logs/debug/wallet-session-2026-09-06.log` is from a PRE-FIX build: its message text
`authed call has no live session; waiting without SignMessage.` matches `32659c0f6^:BackendRequestSigner.cs:357-359`,
not HEAD's rewritten text.

**§4.1 - what creates the session, and did it ever run: it never ran.**
`TryAttachSession`'s "live session" is three in-memory statics (`_sessionToken/_sessionWallet/_sessionExpiresUtc`,
pre-fix `BackendRequestSigner.cs:71-73`), read by `SessionUsable` (`:79-82`); `SessionGapWhy` (`:88-94`) returns
`missing` on an empty token. The only writer is `MintSessionAsync` (`:503-508`). Its opening `FlowTrace.Step`
(`:427-431`) fires BEFORE any network call, and the string `MintSessionAsync` occurs **0 times across all five
2026-09-06 device captures (~91 MB)** while `[Flow:Wallet]` is provably enabled on that device. The boot window is
in the sibling capture, same pid 7170:

```
12:50:06.956 [Flow:Wallet] Connect OK - CHKK...sfkC (Solana Wallet).
12:50:06.960 [Flow:Wallet] session warm-up deferred - first authenticated action will mint; boot/connect never signs.
12:50:11.556 [Flow:Wallet] authed call has no live session ... why=missing scene=Title ... /api/game/save
```
(`logs/debug/raid-no-abilities-2026-09-06.log:3055/3147/5299`). The warm-up line's promise was false since WO-1157:
save passes `allowMint:false` (`:237`), so nothing minted. `MintSessionForExplicitConnectAsync` existed with zero call sites.

**§4.2 - the browser round trip: there is no round trip.** `NightMarketSharedCardSession.OpenBrowser()` is a Unity
modal (`NightMarketSharedCardSession.cs:88-98` -> `FocusedModalHost.OpenUnderExistingPanel`). Both 13:25 log lines are
stack frames under `[Flow:Pause] WorldHold ACQUIRE 'focused-card-modal'` from `PackStore:OnEnable`
(`wallet-session-2026-09-06.log:35520-35530`, `41491-41500`). Nothing left the app. The WO's §2 premise is void.

**§4.3 - establishment paths (pre-fix): neither was the Night Market.** `allowMint = purchaseRoute ||
allowInteractiveSessionMint` (`:237`); only completing a purchase (`/api/purchases/*`) or a promo redeem
(`PromoCodeService.cs:169`) minted. A wallet holder who never bought or redeemed had **no cloud save from first
launch** - a missing lifecycle, not a bug in one screen.

**§4.4 - WO-1420: two distinct defects.** WO-1420 is a misattribution in `WalletService`'s `TimeoutException` catch
during a REFUSED reauthorize (00:49 boot). In the 12:50 boot the reauthorize SUCCEEDED (`Connect OK`, 2855.8 ms) and
the session was still missing, so WO-1441 is independent of and downstream from connect. Note for WO-1420 §3.2:
`MWA wallet closed its one-shot association endpoint` appears four times (12:50:04.742-06.256) on the SUCCESS path,
so it is not by itself a refusal marker.

## 2. What HEAD does (verified at source)

- `WalletSkinBootstrap.cs:254,375` - both connect paths call `MintSessionForExplicitConnectAsync`; `WarmUpSessionAsync` deleted.
- `BackendRequestSigner.cs:542` - `TryRenewSessionAsync` added (its server half is the api renewal cap, a separate
  uncommitted lane; see WO-1440 RESULT §7c for the `auth_sessions.signed_at` landmine).
- `WalletService.cs:525-556` - WO-1420's elapsed-time REFUSED / TIMED-OUT branch.
- §5 of the WO held: save still passes `allowMint:false`; nothing fails open; `FlushOfflineQueue`
  (`GameStateService.cs:2771-2834`) deletes `SyncQueueKey` only after a successful upload, re-queues on failure,
  and now emits permanent `offline queue DRAINED` / `drain FAILED` FlowTrace lines.

## 3. Acceptance

- [x] Root cause proven from a captured trace, quoted (§1).
- [ ] A wallet holder gets a live session without opening the Night Market - **on device, not yet captured**.
      Expected marker on the post-fix build: `MintSessionAsync why=explicit-connect ... held` at boot.
- [ ] A cloud save SUCCEEDS - needs a captured `/api/game/save` 2xx on build 358574.
- [ ] The queued offline deltas DRAIN - needs `offline queue DRAINED` captured; live depth reached **112**.
- [x] WO-1420 answered: separate cause, with evidence (§1, §4.4).
- [x] Instrumentation on the mint/drain path is permanent.
- [x] `REGRESSION_OK 414/414`.

Session life past 15 minutes depends on `TryRenewSessionAsync` plus the `signed_at` column on the live Neon DB,
which is the owner-run repair (`tools/run-schema-repair.mjs`) - not this ticket's client scope.
