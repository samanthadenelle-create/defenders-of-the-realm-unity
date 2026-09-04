# WORK ORDER 1377 - The crypto tokens that survive every string guard: IL2CPP ships TYPE NAMES

**Status:** READY TO IMPLEMENT - ⛔ **BLOCKED on an owner ruling** (§4, the save-serialisation risk)
**Silo / Lane:** Core assembly boundaries - `Assets/_Modules/Core/Web3/` -> `DeNelle.Web3`
**Type:** EXISTING architecture, Play-variant blocker
**Minted:** 2026-09-04 (CLI), surfaced by the WO-1363 purge
**Blocks:** the Google Play AAB passing its own artifact scan once WO-1364 widens the gate.

## THE FINDING - and it is the CEILING on WO-1363

WO-1363 moved **24 shipping token-bearing string literals down to 2**. It cannot get to zero, and no
amount of further `#if` work will, because:

⛔ **IL2CPP's `global-metadata.dat` carries TYPE AND MEMBER NAMES, not just string literals.**

A `#if` around a string removes the string. It does nothing to an identifier. Still shipping in
`DeNelle.Core`, which is in every build:

| Identifier | Home |
|---|---|
| `IJupiterService` | `Core/Web3/IJupiterService.cs` |
| `CoreServices.Jupiter` / `RegisterJupiter` / `UnregisterJupiter` | `Core/CoreServices.cs:154,157,172` |
| `FeatureFlags.JupiterSwap` | `Core/FeatureFlags.cs` |
| `SwapToken.USDC` | `Core/Web3/` |
| `PaymentChannel.SolanaDappStore` | `Core/Payments/` |
| `SkinAuthMode.SolanaWallet` | `Core/Platform/` |

**So the artifact scan WILL still find `solana`, `jupiter` and `usdc` after WO-1363 lands.** That is
not a purge failure - it is a different defect with a different fix, and conflating them will send
someone hunting for string literals that are not there.

## THE FIX SHAPE

Move `Assets/_Modules/Core/Web3/*` into the **already `!GOOGLE_PLAY`-constrained** `DeNelle.Web3`
assembly (`DeNelle.Web3.asmdef:17`), so the types are not compiled into the Play variant at all -
the same **Tier 1 assembly exclusion** that WO-1362 proved genuinely works (the merged dex contains
no MWA, no Solana). Then rename the two offending enum members.

⚠ This is CROSS-ASSEMBLY. `CoreServices` is referenced by everything; removing `Jupiter` from it in
the Play variant means every caller needs the same guard or the seam needs re-shaping.

## §4. ⛔ THE OWNER RULING THIS IS BLOCKED ON - a rename can be DATA LOSS

**`PaymentChannel` and `SkinAuthMode` may be save-serialised.** A grep of `Assets/_Modules/Core/State/`
found no obvious persistence and no `[JsonProperty]` on them - **but that is NOT PROVEN safe.**

⛔ **If either is stored by NAME in a save, renaming the member silently breaks every existing
player's save on read.** If stored by ORDINAL, renaming is safe but REORDERING is not.

**Establish which, at source, before touching either** - and if they are persisted, this needs a
read-migration and probably a schema bump, which makes it a much larger ticket than the asmdef move.

⚠ **The cheaper alternative worth putting to the owner:** if the identifiers cannot be moved safely,
is a Play artifact that contains the *type name* `IJupiterService` - with no reachable code, no
string copy and no UI - actually a policy problem? **That is a judgement call about what a reviewer
would object to, and it is hers.** A dead type name in a metadata blob is a very different thing from
"Powered with SKR" on a screen. ⛔ Do not assume either answer.

## ACCEPTANCE

- [ ] Established at source whether `PaymentChannel` / `SkinAuthMode` are persisted, and how (name vs
      ordinal). Quote the evidence.
- [ ] Owner has ruled §4 - either the move happens, or the residual identifiers are accepted with a
      recorded reason.
- [ ] If moved: the Play variant compiles (`-ExtraScriptingDefines GOOGLE_PLAY`) AND the dApp variant
      compiles. ⭐ Both, every time - the purge already proved a Play-only change can pass the default
      compile and still be wrong.
- [ ] ⛔ **Proven against the ARTIFACT**: scan `global-metadata.dat` for `solana`/`jupiter`/`usdc` and
      quote the before/after counts. A source grep does not close this - a source grep is exactly what
      certified the dirty build.
- [ ] No existing save fails to load. Prove it with a real save round-trip, not a code read.

## WHAT NOT TO TOUCH

- ⛔ Do not rename an enum member before answering §4. That is the data-loss path.
- ⛔ Do not delete `IJupiterService` - Jupiter swap is a real dApp-lane feature, only absent on Play.
- ⛔ Do not widen `DeNelle.Web3`'s constraint - `!GOOGLE_PLAY` is correct and is what makes this work.
