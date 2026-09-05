# WORK ORDER 1364 - Make the Play artifact gate able to SEE a dirty artifact

**Status:** FIXED - implemented in 6979fb961 + 61d19a23b (2026-09-04), on the Seeker in build 2026.09.05.355872; RCA re-verified 2026-09-04 (see the appended block). Awaiting owner felt-test: none on the device - this is a gate; the proof is the RED on a real AAB (Builds/wo1367-aab.log:37493 PLAY_ARTIFACT_DIRTY / :37507 PLAY_ARTIFACT_REJECTED). The green-artifact proof waits on WO-1366 + WO-1377.
**Silo / Lane:** Release engineering / gates - `Assets/Editor/Regression/` + `tools/android/`
**Type:** EXISTING gate, structural blind spot
**Minted:** 2026-09-04 (CLI)
**Pairs with:** WO-1363 (the purge). **Do 1364 FIRST if you want the contamination visible while you
remove it.** Neither ticket is sufficient alone.

## THE ONE-LINE STATEMENT OF THE DEFECT

**The gate that certifies the Play artifact crypto-clean is structurally incapable of seeing the
crypto, and a regression case actively FAILS THE BUILD if you make it stricter.**

## THE PROVING LINE

```
Builds/ui-reskin-final-google-play-aab-v2.log:38188   PLAY_ARTIFACT_CLEAN_OK
```

Emitted on `Builds/Android/EchoesOfElarion-GooglePlay.aab` - the artifact WO-1362 measured as
carrying `solana` x74, `SKR` x35, `Jupiter` x12, and the USDC mint
`EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v`.

This is the **hollow-pass class** (CLAUDE.md §12; `docs/reference/HOLLOW_ASSERTIONS_REGISTRY.md`;
MASTER_CATALOG ledger P1 item 12) in its most expensive form. Quoting that ledger: *a gate that
reports success without proving it does not merely miss a bug - it **actively asserts the bug is
absent**, and work proceeds on that strength.* A human read the marker and shipped.

## THE MECHANISM - read all four parts before changing anything

### 1. Two token tiers
`Assets/Editor/Regression/GooglePlayPackagingGate.cs`:
- `UserFacingContentTokens` (`:20-30`) - strict. Includes `solana`, `jupiter`, `jup.ag`, `usdc`,
  `blockchain`, `crypto`, `web3`, `$skr`, `spend $skr`, the SKR mint.
- `OpaqueExecutableTokens` (`:35-45`) - **deliberately drops** `solana`, `jupiter`, `usdc`,
  `blockchain`, `crypto`, `web3`, `$skr`. The rationale is stated openly in the comment at `:32-34`:
  avoid matching `System.Security.Cryptography` and ad-SDK strings.

### 2. The routing sends the binary to the weak tier
`IsUserFacingContentEntry` (`:167-179`) returns true **only** for `base/assets/data/canonical/*` or
`.json/.txt/.html/.xml/.uxml`. So
`base/assets/bin/Data/Managed/Metadata/global-metadata.dat` - the IL2CPP metadata blob where every
C# string literal lands - is scanned with the weak tier.

### 3. Two gaps beyond the tier split
- ⛔ **The USDC mint `EPjFWdd5AufqSSqeM2qN1xzybapC8G4wEGGkZwyTDt1v` is in NEITHER list.** Only the
  SKR mints (`SKRbvo6...`, `3BwWSA...`) are covered.
- ⛔ **There is no bare `skr` token in either tier** - only `$skr` and `spend $skr`. So
  `"Powered with SKR"`, `"How SKR powers the realm"`, `"Stake SKR natively..."` and every Arena
  wager sentence match nothing, in either tier.

### 4. ⛔ AND THE SOURCE REGRESSION ENFORCES THE BLIND SPOT
`Assets/Editor/Regression/GooglePlayPackagingRegression.cs:105-129` pins the tier routing **in the
wrong direction**:
- `:112-114` **fails** if the opaque tier *does* reject `crypto` / `web3`
- `:115-116` **fails** if the opaque tier *does* reject `com.solana.unity_sdk`

**So widening the gate turns the suite RED until this oracle moves with it.** That is not a bug in
the oracle - it is a pin protecting a deliberate decision, and the decision is now overruled by the
owner's *"Nothing Crypto goes in the aab build"*. ⛔ **Re-point the pin, do not delete it**
(the WO-1159 precedent: a ruling moved, so the pin was re-pointed and made STRICTER, never softened
or removed). Leaving it deleted means nothing pins the tier routing at all.

⚠ Also note what `GooglePlayPackagingRegression` **is**: a ~70-case **source-text grep** oracle -
`Require(text, token, ...)` at `:225-228` is literally `if (!text.Contains(token))`. Its own header
(`:10-11`) says *"does not build an AAB."* **It cannot ever catch a dirty artifact**, by design.
Do not mistake its green for artifact evidence.

## THE WORK

1. **Widen `OpaqueExecutableTokens` toward the strict list**, with a **documented, justified
   allowlist** for the genuine false positives the comment at `:32-34` names
   (`System.Security.Cryptography`, `com.solana.unity_sdk` type names if the SDK is legitimately
   absent-but-referenced, ad-SDK strings). ⛔ **Do not narrow anything without widening in the same
   edit.** Budget a day of shaking out real false positives - that is expected, not a setback.
2. **Add the USDC mint and a bare `skr` token** to the appropriate tier(s).
3. **Re-point `GooglePlayPackagingRegression.cs:105-129`** to pin the NEW intended routing, so the
   suite proves the widened behaviour instead of the old blind spot.
4. **Both copies in ONE edit.** `Assets/Editor/Regression/GooglePlayPackagingGate.cs` and
   `tools/android/assert-google-play-aab-clean.ps1` (`:16-26`, `:27-35`) are byte-identical today
   and must stay so. ⚠ They are duplicated state that has drifted before (WO-1362 records the drift
   as previously closed). **Consider generating one from the other rather than maintaining a second
   copy** - CLAUDE.md §2/§5/§16 are all the same lesson.
5. **Close the rewrite-allowlist drift channel.** `Assets/Editor/GooglePlayContentExclusion.cs:203-273`
   rewrites a **hardcoded 3-key allowlist**; `canon-strings.json:231` `_storePiSkinNote` was authored
   2026-09-02 with `"Solana Mobile's governance token"` and is not in it. A longer allowlist just
   defers the next drift.

## ACCEPTANCE - prove RED first

- [ ] ⛔ **Proven RED before green.** Run the widened gate against the CURRENT dirty AAB on disk
      (`Builds/Android/EchoesOfElarion-GooglePlay.aab`, 514,062,537 bytes, 2026-09-01) and show it
      emits **`PLAY_ARTIFACT_DIRTY`**, naming the entries. **A gate that has never been seen red on a
      known-dirty artifact proves nothing** (WO-1138). Quote the output.
- [ ] Then show it emits `PLAY_ARTIFACT_CLEAN_OK` on a WO-1363-purged artifact.
- [ ] `REGRESSION_OK n/n` with the re-pointed oracle - and state the suite count moved or did not,
      read off the marker, never restated from a doc.
- [ ] The false-positive allowlist is documented **with the reason for each entry**, in code.
- [ ] Both token copies verified identical after the edit (diff them; do not eyeball).

## WHAT NOT TO TOUCH

- ⛔ Do not delete `GooglePlayPackagingRegression.cs:105-129`. Re-point it.
- ⛔ Do not weaken `UserFacingContentTokens` to make a false positive go away - allowlist the
      specific string, and say why.
- ⛔ Do not treat a green `GooglePlayPackagingRegression` as artifact evidence. It greps source.

---
## RCA re-verified 2026-09-04 (QA read-only pass)
**Verdict:** SUPERSEDED
**Evidence:**
- Landed in `6979fb961` (gate + regression) and `61d19a23b 2026-09-04 WO-1364: the PS1 artifact scanner kept in lockstep with the C# gate` (233-line rewrite of `tools/android/assert-google-play-aab-clean.ps1`).
- `Assets/Editor/Regression/GooglePlayPackagingGate.cs:50-62`: a single `ForbiddenTokens` array now carries `solana`, `jupiter`, `usdc`, `blockchain`, `crypto`, `web3`, bare `skr` (`:54`) and the USDC mint `EPjFWdd5...` (`:62`); `:77 ShortTokensRequiringTextContext = {"skr","$skr","usdc","web3"}`; `FalsePositiveAllowlist` with per-entry reasons (`:100-115`, e.g. `cryptoconvert`, `aarch64_crypto`, `libcrypto`). The `UserFacingContentTokens` / `OpaqueExecutableTokens` tiers this WO cites at `:20-45` no longer exist; the header `:17-44` records why.
- `GooglePlayPackagingRegression.cs:123-150`: the pin is RE-POINTED - `mustPolice` includes `crypto, web3, solana, jupiter, usdc, blockchain, skr` and both mints, failing with "this is the WO-1364 blind spot returning" if the opaque path drops any. `:106-108` now REJECTS `$userFacingTokens = @(` / `$opaqueTokens = @(` in the ps1; `Require` still `if (!text.Contains(token))` at `:325`.
- The ps1 is no longer a second copy: `tools/android/assert-google-play-aab-clean.ps1:71,73` parse `ForbiddenTokens` / `FalsePositiveAllowlist` out of the C# file at run time.
- RED proven on a real build: `Builds/wo1367-aab.log:37493 PLAY_ARTIFACT_DIRTY` / `:37507 PLAY_ARTIFACT_REJECTED` (AAB 472,637,397 bytes, Sep 4 09:17; the WO's 514,062,537-byte Sep 1 AAB is gone). The "CLEAN_OK on a purged artifact" box is not yet met (blocked by 1366/1377).
- This WO's Status line (`:3`) still reads READY; no RESULT.
**What changed since the RCA:** every cited line moved - the two-tier design was deleted, the regression pin inverted, the ps1 made derived; the gate has since gone RED on a real AAB, which is the point of the ticket.
**Ready for a lane?** no - done; needs Status flip + RESULT + board regen. Files a lane would touch: this WO only.
**Pins/rulings needed:** none for 1364 itself; the green-artifact proof waits on WO-1366 and the WO-1377 ruling.
