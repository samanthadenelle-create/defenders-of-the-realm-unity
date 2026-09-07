# WO-1420: the wallet's silent reauthorize was REFUSED in 0.1s and the game reported a 30s TIMEOUT

**Status:** FIXED - ON THE SEEKER 2026.09.07.358574 - landed in `32659c0f6` (see RESULT); minted 2026-09-06 00:55 (CLI) from F8 device capture seq 4683 on build 2026.09.06.357453
**Silo:** Wallet (DeNelle.Wallet) - boot auto-resume + Connect error attribution
**Source capture:** `logs/f8-inbox/capture-device-20260906-004946-seq4683.md` (kind=error, scene=Title, device SM02G4061955851)
**Player-felt:** none visible - the title screen fell back to the `Connect Wallet` corner button as designed. The defect is
a WRONG diagnosis in the trace and in `LastConnectError`, which will mis-steer every future triage of this seam.

## 1. What the device log proves (logcat, 2026-09-06 00:49:31, all one process 14788)
```
31.423  [Flow:Wallet] auto-resume: sealed session present - attempting a SILENT reconnect at boot
31.425  [Flow:Wallet] -> Connect (provider=Solana Wallet, Mainnet)
31.448  [Flow:Wallet] MWA session found for CHKK...sfkC - attempting SILENT reauthorize (no prompt expected).
31.490  [Flow:Wallet] MWA handlers visible: com.solanamobile.wallet, ag.jup.jupiter.android, app.backpack.mobile.standalone, app.phantom, com.solflare.mobile
31.491  [Flow:Wallet] MWA package resolved=com.solanamobile.wallet reason=chain rank 1.
31.491  [Flow:Wallet] MWA association -> package=com.solanamobile.wallet
31.538  [Flow:Wallet] MWA wallet closed its one-shot association endpoint; not reconnecting to the retired port.   (thread 15249)
31.707  [Flow:Wallet] Corner auth button updated: 'Connect Wallet' (interactable=True).
31.822  Cysharp.Threading.Tasks.<Timeout>d__45`1:MoveNext()
31.823  [Flow:Wallet] Connect TIMED OUT after 30s (no wallet app installed, or the handshake was never answered) - staying disconnected.   (FlowTrace.Fail -> F8 error)
```
- Connect to Fail = **0.4 s**. The message asserts **30 s**. The wallet app IS installed (five handlers listed) and it DID
  answer - it closed the association endpoint at 31.538, 47 ms after the association opened.
- The 09-03 handover recorded the same path SUCCEEDING (`auto-resume: sealed session present` -> silent reauthorize
  ~3.3 s -> `auto-resume SUCCEEDED`, commit `6e9f86cc3`). Tonight the wallet refused instead. The refusal itself is the
  wallet app's behaviour (Seed Vault wallet on a phone unlocked seconds earlier, game launched via adb, first boot after
  the 357453 install) - NOT proven from here which of those matters. That is the open question in section 4.

## 2. The defect, at source
- `Assets/_Modules/Wallet/WalletService.cs:473-474` wraps `_provider.Connect(Network)` in
  `.Timeout(TimeSpan.FromSeconds(ConnectTimeoutSeconds), ...)`.
- `:509-518` `catch (TimeoutException)` sets `LastConnectError = "Your wallet did not respond in 30 seconds..."` and
  `FlowTrace.Fail("Connect TIMED OUT after 30s ...")` for EVERY `TimeoutException`, including one raised INSIDE the
  provider/MWA layer long before the 30 s deadline. The catch cannot tell "our deadline expired" from "the SDK threw a
  TimeoutException as its refusal shape". Tonight it was the second, and the trace, the F8 capture and the player-facing
  `LastConnectError` all reported the first.
- The comment at `:470-472` ("On expiry this throws TimeoutException, which lands in the catch below") documents the
  intended path only. The stack in the capture shows `UniTask.Timeout` -> `TrySetException` -> `WhenAnyPromise`, i.e. the
  inner task completed with an exception and Timeout re-surfaced it; the 30 s delay promise did not win the race.

## 3. Fix (spec)
1. Measure the elapsed time around the awaited Connect (a `Stopwatch` or `Time.realtimeSinceStartup` delta). In the
   `TimeoutException` catch, branch: elapsed >= `ConnectTimeoutSeconds` - keep today's copy; elapsed well under it -
   `FlowTrace.Fail("Wallet", $"Connect REFUSED by the wallet after {elapsed:F1}s (TimeoutException from the provider,
   not our {ConnectTimeoutSeconds}s deadline): {ex.Message}")` and `LastConnectError = "Your wallet refused the
   connection. Open your wallet app and try again."`. Keep both copies in canon-strings if the surface reads
   `LastConnectError` (check `LoginWalletBridge` / `WalletSkinBootstrap`).
2. Carry the `MWA wallet closed its one-shot association endpoint` fact into the same Fail line when it fired during this
   Connect (the association layer already logs it on thread 15249 - expose a last-close reason on the provider, read it in
   the catch). One line should name the cause; a triage should not need to correlate two threads by timestamp.
3. Regression (RED first): a fake provider whose `Connect` throws `TimeoutException` immediately must produce the REFUSED
   line, not the TIMED OUT line; a fake provider that never completes must produce TIMED OUT at the deadline (use a
   short injected `ConnectTimeoutSeconds`).
4. Do NOT change the 30 s deadline, the 180 s signing hold (WO-1360, owner call), the auto-resume path, or the fallback
   to the corner button - all of those worked tonight.

## 4. Open question for the owner / next felt-test
- On the SAME phone and build, tap `Connect Wallet` once: does the Seed Vault wallet answer? If yes, the silent
  reauthorize refusal is a first-boot/just-unlocked artefact of tonight and only section 3 applies. If it refuses again,
  the sealed session for CHKK...sfkC is stale on the wallet side and the game should DROP the seal after a refusal
  (today it keeps it, so every boot will replay the refusal) - that becomes item 5 of section 3.

## 5. What NOT to touch
- `TargetedLocalAssociationScenario` / the MWA transport - the refusal is logged correctly there.
- `PurchaseGate`, `RealmStorePurchase`, any pay path (owner: money attached).

Provenance: CLI seat, from the harvested capture and the device logcat read this session (CLAUDE.md sections 12 and 14).
