# WORK ORDER 1363 - Nothing crypto in the Play AAB: compile the literals out, cut Arena

**Status:** READY TO IMPLEMENT
**Silo / Lane:** Release engineering / Play variant - `DeNelle.Core` UI + `DeNelle.Village/Arena`
**Type:** EXISTING system, incomplete exclusion
**Minted:** 2026-09-04 (CLI), on owner rulings
**Blocks:** the Google Play AAB (WO-1362 Gate B). Does NOT block the Seeker/dApp lane.

## THE RULINGS THIS IMPLEMENTS

Owner, 2026-09-04, verbatim: ***"Nothing Crypto goes in the aab build"***.

Follow-up ruling, same exchange: **Arena is CUT from Play builds** - compiled out, with every UI
route that points at it removed. The Play build does not have the mode. The soft-currency Arena
variant is explicitly **NOT** being built. The Seeker/dApp build keeps SKR wagering unchanged.

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

### PART 3 - cut Arena from the Play build. THIS IS THE RISK.

Current state (READ, `grep -c GOOGLE_PLAY`): `Assets/_Modules/Village/Arena/ArenaMode.cs` = **0** ·
`ArenaVM.cs` = **0** · `Assets/_Modules/Core/State/GameState.cs` = **0** ·
`Assets/_Modules/Village/DeNelle.Village.asmdef:35` = `"defineConstraints": []`.

**The SKR wager loop compiles into and runs in a Play build today.** The literals include
`ArenaMode.cs:163`, `:164` (`"cannot afford {0} SKR wager"`), `:167`, `:193`, `:382`, `:431`
(`"forfeiting staked {1} SKR (no refund)"`), `:435`, `:452`, and `ArenaVM.cs:191` (the `"SKR"`
currency label on every opponent row).

⛔ **THE DANGER IS NOT THE REMOVAL, IT IS THE DOORS.** A Play build with Arena compiled out but a
button, quest, dialogue verb, HUD card, realm-map pin or panel route still pointing at it is a
**Google broken-functionality rejection** - a worse outcome than the crypto string this was meant to
remove. Find every entrance before you cut:

- `DialogueCommandBridge` has an `OpenArena` verb (`docs/MASTER_CATALOG.md` §2c) - dialogue is a data
  path, so grep the `.json` dialogue content too, not just C#.
- `PanelRouter` / `PanelId` - is there an Arena panel id, and does anything route to it?
- `ArenaPaletteVM`, `arenaDefense` (persisted since save v19) and `ArenaProgress` (v34) are **save
  fields**. ⛔ **Do NOT remove a persisted field.** Read-migrate so a save written by the Seeker build
  still LOADS in a Play build (ordinary defensive deserialisation - CLAUDE.md §5 additive rule).
- The realm map, quests, the Journey/Manage screens, the FTUE.

⚠ `Assets/_Modules/Village/DeNelle.Village.asmdef` **cannot** simply take a `!GOOGLE_PLAY`
constraint - it is the ~275-file main gameplay assembly. The cut has to be `#if` inside it, or an
Arena sub-assembly extracted first. **Extracting a sub-assembly is the structurally right answer and
it is a bigger change - name the choice explicitly rather than defaulting to whichever is quicker**
(ARCHITECTURE_PRINCIPLES §3: do not smuggle a structural refactor into this, and do not pick easy and
present it as the answer).

## ACCEPTANCE

- [ ] ⛔ **Proven against the ARTIFACT, not the source.** Build a Play AAB, unzip it, and scan
      `base/assets/bin/Data/Managed/Metadata/global-metadata.dat` for `SKR`, `solana`, `usdc`,
      `jupiter`, and the USDC mint `EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v`. **Quote the counts
      before and after.** Source-level greps do not close this ticket - a source grep is exactly what
      `GooglePlayPackagingRegression` already does, and it certified the dirty artifact.
- [ ] Zero Arena strings in the artifact, AND zero reachable Arena entry points in a Play build -
      enumerate the doors you found and state how each was closed.
- [ ] A save written by the Seeker build LOADS in a Play build with no data loss (arenaDefense,
      ArenaProgress read-migrated, not dropped).
- [ ] The Seeker/dApp artifact is UNCHANGED - Arena still present, SKR wagering intact. Prove it:
      build both, diff the behaviour claim, do not assume the define did what you meant.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK n/n` on fresh logs, and `error CS` count zero
      (the marker alone is not sufficient - `Builds/rail-compile.log` precedent).
- [ ] WO-1364's widened gate passes on the resulting artifact. If 1364 has not landed, say so and do
      the scan by hand.

## WHAT NOT TO TOUCH

- ⛔ Tier 1. The asmdef constraints and the MWA/SDK exclusions are working. Leave them.
- ⛔ The Seeker/dApp lane's Arena, wagering, currency skin or copy. This is a Play-variant ticket.
- ⛔ Do not "fix" the drift by deleting a persisted save field.
- ⛔ Do not cut a Play AAB as a throwaway measurement - owner ruled fix-first-build-once. The one
      build in the acceptance criteria above is the verification build, and it is the point of the
      ticket.
