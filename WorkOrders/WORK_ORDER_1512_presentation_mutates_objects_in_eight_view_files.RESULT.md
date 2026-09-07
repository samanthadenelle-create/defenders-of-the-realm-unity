# WO-1512 RESULT - two VMs extracted, the AdminOverlay grant narrowed off TESTER_BUILD, and the ticket's own evidence corrected

**Status:** THE TWO CONSEQUENTIAL VIEWS DONE - uncommitted in the working tree as of 2026-09-06 21:45, awaiting
the wave-two gate. **Tree contradicts the ticket:** its Status line still reads `READY TO IMPLEMENT` while the
work sits in the tree. (Status line not edited here - RESULT-only lane.)
**Commit:** none. Edit-only lane.
**Files:** `Village/BuildMode/ObsidianQueueVM.cs`, `Village/Troops/ArmyMusterVM.cs`, `Wallet/RedeemCodeVM.cs`
(all NEW, untracked); `Village/BuildMode/ObsidianQueueHud.cs`, `Village/Troops/ArmyMusterPanel.cs`,
`Wallet/RedeemCodePanel.cs`, `HUD/AdminOverlay.cs:235-263`,
`Assets/Editor/Regression/UiMvvmConformanceRegression.cs:135-152,276,312`.
**Gates:** none. `Builds/cg-quiet.log` `COMPILE_GATE_OK` is 20:04 and predates these edits;
`Builds/cg-aab.log` (20:54) is RED (42x `CS0103`, the Manage lane's half-written suites).

## 1. Correction to the ticket's evidence, recorded per CLAUDE.md sec.11B

The headline - `ObsidianQueueHud.cs:397,410,428` "SPENDS CURRENCY directly" - is **wrong at source**, and the lane
says so in `ObsidianQueueVM.cs:7-14`: the View never debited a wallet. `BuildTimerService.TryBuySlot` /
`TryInstantFinish` own the basket and always did (WO-911 Q1). The real breach is one level up and is still a
breach: the View resolved the service singleton, decided WHETHER a spend was on offer (price quote,
gold-vs-crystal currency, the `ff.rewardedadskip` gate), invoked the verb and interpreted the outcome. So the VM
owns the DECISION and the CALL - `QueueOffer`, `BuySlot`, `InstantFinish`, `WatchAdSkip` - and deliberately does
NOT re-home the debit, which would move it out of the service that correctly owns it. `ArmyMusterPanel` was the
plainer case: `private static readonly ArmyComposition s_composition` lived on the View and every command mutated
it in place; `ArmyMusterVM.cs` now holds it.

## 2. The AdminOverlay grant

The OUTER guard stays `#if DEVELOPMENT_BUILD || UNITY_EDITOR || TESTER_BUILD` (`:44,70,378,514,547`), but the
value-minting grants sit inside a narrowed INNER guard `#if DEVELOPMENT_BUILD || UNITY_EDITOR` (`:235`, `#endif`
at `:263`, a second at `:809`). The comment at `:238-248` states why the two must not be "simplified" into one:
the outer guard admits the Firebase tester APK.

## 3. Acceptance

- [x] The grant unreachable in a TESTER build - **by compile-stripping, source-proven** (sec.2). No
      build-with-the-symbol screenshot; the `#if` is the stronger proof and the suite says so
      (`UiMvvmConformanceRegression.cs:152`: "protection is compile-stripping, not this lint").
- [x] `ObsidianQueueHud` no longer decides or invokes the spend; `ObsidianQueueVM` does - sec.1.
- [x] A regression fails when a `*Panel`/`*Hud` file mutates economy state directly -
      `UiMvvmConformanceRegression.cs:276,312` source-lints every `*Panel.cs`/`*Hud.cs` under `Assets/_Modules`.
      RED proof unrun - the tree does not compile.
- [ ] `REGRESSION_OK n/n` on a fresh log - owed.

## 4. Not done, deliberately

Per sec.3 ("do not do all eight in one commit"), `CosmeticShopPanel.cs:198` is untouched and
`RedeemCodePanel.cs:290` has a VM but is third priority. No device capture applies - architecture lane, no
player-visible change.
