# WORK ORDER 1363 - Nothing crypto in the Play AAB: compile the literals out, de-crypto Arena

> ## ⛔ SCOPE REVERSED BY THE OWNER, 2026-09-04, BEFORE ANY WORK STARTED. READ THIS FIRST.
>
> Owner, verbatim: ***"the arena will go to the google play store, just needs to remove crypto"***.
>
> **ARENA IS NOT CUT. Arena SHIPS in the Play build.** An earlier ruling in the same session said
> "cut Arena from Play"; **that is superseded** and this file was rewritten before any agent picked
> it up. If you are reading a summary, a commit body, or a canon line that says Arena is compiled
> out of Play - ⛔ **that line is stale, and this block is the correction.**
>
> The job on Arena is therefore **de-crypto, not delete**: the mode, its loop, its UI routes and its
> save fields all stay. What leaves is SKR - the currency it wagers and every SKR literal in it.
> **The wager denomination is an OWNER DESIGN CALL and is recorded in §PART 3 below.**

**Status:** FIXED - implemented in 6979fb961 (2026-09-04), on the Seeker in build 2026.09.05.355872; RCA re-verified 2026-09-04 (see the appended block). Awaiting owner felt-test: open Title and the Store/Stake panels on the device and confirm no SKR/Solana copy is visible; the artifact-clean AAB box stays open on WO-1366 + WO-1377 (canon-strings.json "Solana" residue still goes RED, wo1367-aab.log:37493).
**Silo / Lane:** Release engineering / Play variant - `DeNelle.Core` UI + `DeNelle.Village/Arena`
**Type:** EXISTING system, incomplete exclusion
**Minted:** 2026-09-04 (CLI), on owner rulings
**Blocks:** the Google Play AAB (WO-1362 Gate B). Does NOT block the Seeker/dApp lane.

## THE RULINGS THIS IMPLEMENTS

Owner, 2026-09-04, verbatim: ***"Nothing Crypto goes in the aab build"***.

Follow-up ruling, same exchange, **superseding an earlier cut-Arena ruling from the same session**:
***"the arena will go to the google play store, just needs to remove crypto"***. **Arena SHIPS in the
Play build.** Nothing is compiled out; no UI route is removed; no save field is touched. The SKR
BRANDING leaves, the mode stays.

Follow-up ruling, same exchange: **no throwaway AAB.** Fix first, build once.

## THE PROVING LINE - read this before anything else

```
Builds/ui-reskin-final-google-play-aab-v2.log:38188   PLAY_ARTIFACT_CLEAN_OK
```

That marker was emitted on `Builds/Android/EchoesOfElarion-GooglePlay.aab` (514,062,537 bytes,
2026-09-01 07:29) - **the same artifact WO-1362 measured as carrying the USDC mint address
`EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v`, `solana` x74, `SKR` x35, `Jupiter` x12 and four SKR
marketing sentences.**

⛔ **The gate is not lying and it is not broken - it was built with a documented blind spot.**
Fixing the blind spot is **WO-1364**, and it is a SEPARATE ticket on purpose: this ticket removes the
contamination, that ticket makes it impossible to ship again unnoticed. **Neither is sufficient
alone.** Do 1364 first if you want the failure to be visible while you work.

## ⛔ THE STRUCTURAL FACT THAT DEFINES THE WHOLE JOB

**A runtime `#if` guard does NOT remove a string literal from the binary.**

The Play exclusion has two tiers and only one of them works:

- **Tier 1, assembly exclusion - GENUINELY CLEAN, do not touch it.**
  `Assets/_Modules/Wallet/DeNelle.Wallet.asmdef:22` and `Assets/_Modules/Web3/DeNelle.Web3.asmdef:17`
  carry `"!GOOGLE_PLAY"`; `Assets/_Modules/GooglePlay/DeNelle.GooglePlay.asmdef:18` is
  `["GOOGLE_PLAY"]`-constrained; the Solana SDK runtime is `!GOOGLE_PLAY`-constrained; the MWA
  androidlib is excluded by `Assets/Editor/MobileWalletAdapterPlayExclusion.cs:90`. **The merged dex
  proves this tier works** - 23.3 MB containing no `mobilewalletadapter`, no `solanamobile`, no
  `com/solana`.

- **Tier 2, `#if` guards inside assemblies that ALWAYS ship (`DeNelle.Core`, `DeNelle.Onboarding`,
  `DeNelle.Settings`) - THIS IS THE DEFECT.** The guards wrap *behaviour*, and the literals sit
  outside them, so they compile into `global-metadata.dat` regardless of whether the code can ever
  run. Canonical example: `Assets/_Modules/Core/UI/SkrShowcasePanel.cs:68` is a `#if GOOGLE_PLAY`
  early-return inside `Open()`, while the copy at `:77`, `:154` and `:172` is outside it.

A policy reviewer runs `strings` on the artifact. They do not run the game.

## THE WORK - three parts, and part 3 is the risky one

### PART 1 - compile the SKR/crypto literals OUT of the always-shipping assemblies

Each of these is a **C# literal reaching `global-metadata.dat`**. The fix is to place them inside
`#if !GOOGLE_PLAY` (or move them behind a `DAPP_STORE`-only type), NOT to blank them at runtime.

| File:line | Literal |
|---|---|
| `Assets/_Modules/Core/UI/SkrShowcasePanel.cs:77` | `"Non-custodial by design: you stake natively - we never take custody of your SKR."` |
| `Assets/_Modules/Core/UI/SkrShowcasePanel.cs:154` | `"Powered with SKR"` |
| `Assets/_Modules/Core/UI/SkrShowcasePanel.cs:172` | `"How SKR powers the realm"` |
| `Assets/_Modules/Core/UI/StakeRewardsPanel.cs:193` | `"Stake SKR natively to unlock your first reward."` |
| `Assets/_Modules/Onboarding/TitleController.cs:348, :353` | the `"Powered with SKR"` badge |

⚠ `TitleController.cs` has **`grep -c GOOGLE_PLAY` = 0** - it has no guard at all today, so this is
a new `#if`, not a widened one.

⚠ **Sweep, do not spot-fix.** The five rows above are the ones recon found; treat them as the
*shape*, not the inventory. Re-scan the whole of `DeNelle.Core`, `DeNelle.Onboarding` and
`DeNelle.Settings` for SKR / Solana / USDC / Jupiter / stake / wallet copy that sits outside a guard.

### PART 2 - the string CATALOGS the rewrite misses

Catalog exclusion is a **separate, non-define mechanism**: `Assets/Editor/GooglePlayContentExclusion.cs`
(quarantine list `:128-149`, mirror pairs `:151-156`, rewrite block `:203-273`).

**The rewrite is a hardcoded 3-key allowlist** (`:225`, `:226`, `:230`). These survive into the Play
build today, all in `Assets/Resources/Data/Canonical/` - i.e. **compiled into the player**:

- `canon-strings.json:197`, `:202-207` - `storeBalanceAfter`, `storeBalanceNoWallet`,
  `storeBalanceBoundAddress`, `storeBalanceBoundIdentity`, `storeBalanceChecking`,
  `storeBalanceUnavailable`, `storeBalanceValue` - every one literally ends in `" SKR"`.
- `en.json:141` - `heroSelect.subtitle: "powered by SKR"`
- `en.json:248` - `swap.title: "Swap to SKR"`
- `siege-stakes.json:2` - **not handled at all**
- `ad-placements.json:14`, `:76` - `"Crystals are the SKR on-ramp"` - **not handled at all**

⭐ **NEW DRIFT, found 2026-09-04 and not in WO-1362:** `canon-strings.json:231` `_storePiSkinNote`
was authored **2026-09-02** - *after* the Sep 1 AAB - and contains `"Solana Mobile's governance
token"`. It is **not** in the rewrite allowlist, and `solana` **is** in the gate's
`UserFacingContentTokens`, so the next Play build should trip `PLAY_ARTIFACT_DIRTY` on it.
**That is the good outcome** - it means the `.json` tier of the gate still works. Do not silence it;
add the key to the rewrite.

⛔ **A hardcoded per-key allowlist is duplicated state and it has already drifted once in three
days.** Prefer a rule the drift cannot outrun (e.g. reject-on-token at author time) over a longer
allowlist. If you extend the allowlist anyway, say why in the code.

### PART 3 - Arena: SPLIT OUT TO WO-1366. NOT THIS TICKET.

Owner ruled 2026-09-04, in three steps: Arena is **not** cut from Play (it ships) - the Play build
wagers **Crystals** - and ***"both to use same logic just different curency for wagers"***, i.e. ONE
Arena code path with the currency as the only per-channel difference.

That turned Arena from a string-purge into a **real balance change** touching `GameState.Crystals`,
the channel seam and the remote tunables rail. It is therefore **`WorkOrders/WORK_ORDER_1366_arena_wager_currency_per_channel.md`**,
file-disjoint from Parts 1-2 above so the two run in parallel.

⛔ **Do not touch `Assets/_Modules/Village/Arena/*` in this ticket.** Its SKR literals leave as a
consequence of WO-1366's currency abstraction, not by a find-and-replace here - a rename that left
the wager pointing at the stub would satisfy a grep and ship the wrong economy.

⚠ **The two tickets share one acceptance test** - the `global-metadata.dat` scan below. Neither can
claim a clean artifact alone; run it once, after both have landed.

## ACCEPTANCE

- [ ] ⛔ **Proven against the ARTIFACT, not the source.** Build a Play AAB, unzip it, and scan
      `base/assets/bin/Data/Managed/Metadata/global-metadata.dat` for `SKR`, `solana`, `usdc`,
      `jupiter`, and the USDC mint `EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v`. **Quote the counts
      before and after.** Source-level greps do not close this ticket - a source grep is exactly what
      `GooglePlayPackagingRegression` already does, and it certified the dirty artifact.
- [ ] ⛔ **Arena is still fully playable in the Play build** - enter, wager, win, lose, see the purse.
      A Play build where Arena is present but broken is a Google broken-functionality rejection,
      which is worse than the string. (Arena's own work is WO-1366; this box is the guard that this
      ticket did not break it.)
- [ ] The Seeker/dApp artifact is UNCHANGED - Arena still present, SKR wagering intact. Prove it:
      build both, diff the behaviour claim, do not assume the define did what you meant.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK n/n` on fresh logs, and `error CS` count zero
      (the marker alone is not sufficient - `Builds/rail-compile.log` precedent).
- [ ] WO-1364's widened gate passes on the resulting artifact. If 1364 has not landed, say so and do
      the scan by hand.

## WHAT NOT TO TOUCH

- ⛔ Tier 1. The asmdef constraints and the MWA/SDK exclusions are working. Leave them.
- ⛔ Do NOT compile Arena out, gate it behind `#if GOOGLE_PLAY`, or remove any route into it. That
      was the SUPERSEDED ruling. Arena ships on both channels.
- ⛔ Do not touch `Assets/_Modules/Village/Arena/*` at all - that is WO-1366's lane.
- ⛔ Do not "fix" the drift by deleting a persisted save field or by re-seeding a PlayerPrefs balance.
- ⛔ Do not cut a Play AAB as a throwaway measurement - owner ruled fix-first-build-once. The one
      build in the acceptance criteria above is the verification build, and it is the point of the
      ticket.

---
## RCA re-verified 2026-09-04 (QA read-only pass)
**Verdict:** SUPERSEDED
**Evidence:**
- Landed in `6979fb961 2026-09-04 WO-1363/1364: the Play crypto purge, the gate that can finally see it, and the ceiling neither can cross` (body: COMPILE_GATE_OK both variants, REGRESSION_OK 363/363; 24 token-bearing literals down to 2).
- The tree matches the commit, not this WO: `Assets/_Modules/Core/UI/SkrShowcasePanel.cs:46 #if !GOOGLE_PLAY` wraps the whole panel, `:277 #else // GOOGLE_PLAY` stub (the WO's `:68` early-return mechanism was recorded as wrong in the commit body). `StakeRewardsPanel.cs:57,67 #if GOOGLE_PLAY` per-channel consts. `TitleController.cs:337 #if !GOOGLE_PLAY` around the badge at `:356` (WO said 0 guards; now 2, at `:207` and `:337`).
- Part 2 replaced by a rule: `Assets/Editor/GooglePlayContentExclusion.cs:406 PLAY_NEUTRAL_UNMAPPED_TOKEN`; mirror pairs include `siege-stakes.json:161`, `ad-placements.json:162`; `storeBalance*` mapped `:198-204`, `heroSelect.subtitle` `:210`, `_storePiSkinNote` `:170`.
- Proven RED on a real artifact: `Builds/wo1367-aab.log:37493 PLAY_ARTIFACT_DIRTY: content:base/assets/Data/Canonical/canon-strings.json token:solana`, `:37507 PLAY_ARTIFACT_REJECTED` (Sep 4 09:20). The "clean AAB" acceptance box is therefore NOT green: `canon-strings.json:184,231` still carry "Solana" in `_nightMarketNote` / `_storePiSkinNote` (that file is also modified-uncommitted in the working tree).
- This WO's Status line (`:17`) still reads READY; no RESULT. Part 3 split to WO-1366 (READY); the ceiling is WO-1377 (BLOCKED on ruling).
**What changed since the RCA:** the purge landed; the artifact scan now goes RED on canon-strings.json; WO-1366 and WO-1377 hold the remaining crypto surface.
**Ready for a lane?** no - implementation is done; what remains is (a) flip Status + write RESULT, (b) the artifact-clean box, blocked by WO-1366 (Arena literals) and WO-1377 (identifiers). Files a lane would touch: this WO only, then the 1366/1377 lanes.
**Pins/rulings needed:** the WO-1377 owner ruling (rename vs save serialisation); WO-1366 is already ruled (Crystals on Play).
