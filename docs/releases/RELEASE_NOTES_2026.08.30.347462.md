# Release Notes — Echoes of Elarion `2026.08.30.347462`

**Artifact:** `DefendersOfTheRealm.apk` (501 MB) · staged at `Builds/Distribution/(Solana)/`
**Target:** Solana Seeker / dApp Store rail (`DAPP_STORE`)
**Package:** `com.denellestudios.echoesofelarion`
**Version:** name `2026.08.30.347462`, code `347462`
**Build:** IL2CPP, ARM64, minSdk 26, targetSdk 36, 28 scenes
**Signing:** RELEASE — `dotr-release.keystore`, alias `dotr` (stable signature; testers update in place)
**Built:** 2026-08-30 02:02–02:08 · **Installed to Seeker `SM02G4061955851`** 02:09:22

---

## Ship gates — all green, judged by marker on fresh logs

| Gate | Result |
|---|---|
| `SCHEMA_PARITY_OK` | 38 table(s) verified against `api/schema.sql` |
| `COMPILE_GATE_OK` | fresh log, 31.3 s after run start |
| `DataRegression` | 328/329 suites green — see the one red below |
| `APK_OK` | 02:08:25, 501 MB |
| `R2_PARITY_OK` | **54 object(s)** — presence + size + content for every remote object this catalog names |
| Device install | `Performing Streamed Install` → `Success` |

The R2 parity proof is the one that matters most: enemy and structure art is served from the CDN,
not packed in the APK, and bundle names are content-hashed — so **this** build needed **its own**
push. 54 objects verified against **this** catalog.

### The one red, and why it did not block

```
BUILD_COLLECTION_PLAYER_FAIL: progression-locked collection entries are not
hidden-before/unhidden-after authoritative unlock
```

**False alarm against working code.** `BuildCollectionPlayerRegression.cs:46` is a source-text grep
asserting the literal `string.IsNullOrEmpty(lockReason) ||`. The shipped gate at
`BuildCollectionBrowser.cs:261` phrases the same logic negatively —
`!string.IsNullOrEmpty(lockReason) && !ProgressionUnlocks.IsUnlocked(itemId)` → `return false`.
De Morgan-identical; the grep failed on `&&` vs `||`, not on behaviour. The suite is
`#if UNITY_EDITOR`, so no player-reachable defect sits behind it.

---

## What actually landed

### Committed since the previous APK (`4329e3d1c`, 2026-08-29 18:55)

| Commit | Change |
|---|---|
| `df027b0bb` | Weaponsmith and Armorer build-card portraits were swapped — fixed |
| `ac40ab578` | PROD-019: sheathed shield pushed clear of the cape for rear cameras |
| `74d9e6546` | PROD-019: knight heater locked to the Offset Forge seat on `Socket_Shield` |
| `0fb7055cc` | WO-961: `SetForcedSourceTexture` on `TripoMaterialFixer` — the ice-wolf albedo pin |
| `c8d63db0b` | WO-961 closed, owner felt-verified |

**Note on `0fb7055cc`:** the previous session pushed `PetDeployer`'s *call* to
`SetForcedSourceTexture` (`4329e3d1c`) without the *method*. Origin briefly carried a call to an API
that did not exist there. That is now closed — both halves are on
`wip/village2-and-f8-tickets`.

### ⚠ Also compiled in: uncommitted working-tree work

**This artifact is not reproducible from any commit.** Beyond the five commits above, the build
includes roughly **90 modified `.cs` files and 30 untracked new ones** from other seats, none of
which are committed. By file, that work covers:

- database-driven **card collections** (`CardCollectionCatalog`, `CardCollectionRemoteService`,
  `CardPresentationModel`, `BuildCollectionBrowser`)
- **Night Market** shared-card session and art (`NightMarketSharedCardSession`, `NightMarketArt`)
- **jeweler discovery FTUE** (`JewelerDiscoveryFtue`)
- **rewarded progression** and SKU entitlements (`RewardedProgression`)
- **Town Showcase** visit panel and community voting
- **harvest overflow** modal, **focused modal host**, build first-use guide
- 11 new regression suites covering the above

Those file names are read off the working tree; their *behaviour* has not been verified
commit-by-commit in this ship. **Before this artifact — or anything built from this tree — goes to
a store or to external testers, that work should be committed**, so there is a revision to roll back
to and rebuild from.

---

## Known issues carried into this build

**Founding tutorial steps are watchdog-skipping on device.** Six distinct steps reported
`STEP-STUCK` on the Seeker across 08-29/08-30: `founding_walk`, `founding_ack`, `founding_timers`,
`founding_stores`, `founding_defense`, `founding_defend`. Newest, seq 3766, device time
2026-08-30T06:55:38Z:

```
[Flow:Tutorial] STEP-STUCK :: founding_defend — no 'wave.tutorial_band_repelled' after 120s in-step
(bound 120s; ff.tutorialv2 on; builderOpenedThisStep=False, coachBeats=3)
[WO-1036 clock: played-and-charged 120s, wall 120s, excluded 0s, discarded suspend gap 0s]
RESCUED via watchdog and recorded as SKIPPED - the step was NOT completed, its outro is suppressed
```

Grants still apply, so no player is left half-granted. Several of these coincide with a
`STUCK WORLD HOLD: 'pause-menu' outstanding for 10863s`, i.e. the app sitting paused — which would
explain idle timeouts. But `founding_defend` reports `excluded 0s` and `discarded suspend gap 0s`,
meaning 120 s of genuinely played time with the tutorial band never repelled. **Unresolved: needs an
owner felt-test to say whether a real new player's FTUE auto-skips.**

---

## Not in this build

- **No Google Play artifact.** `BuildGooglePlayAab()` refuses at `GooglePlayPackagingGate`
  Gate 0 — `DeNelle.Wallet`/`DeNelle.Web3` lack `!GOOGLE_PLAY` constraints, `DeNelle.Village` still
  references Wallet, and the MWA androidlib is packaged unconditionally. Tracked as **WO-1282**
  (AAB-only per owner ruling 2026-08-30). The Seeker APK must **not** be uploaded to Play — it
  carries the Solana SDK and Mobile Wallet Adapter that Gate 0 exists to keep out.
- Monetization remains dormant: no server flag enabled, no endpoint deployed, no purchase path live.

---

## Companion artifacts built the same night

### `2026.08.30.347495` — TESTER build (Firebase App Distribution)

A **second, separate APK** built with `overnight-apk-build.ps1 -Tester`, i.e. the `TESTER_BUILD`
scripting define. Per the owner ruling of 2026-08-24, App Distribution gets the tester-shaped
artifact so the one-tap F8 FLAG capture chip is present; the store-shaped APK above deliberately
lacks it. Both carry identical content and the same `R2_PARITY_OK 54 object(s)`.

- Version `2026.08.30.347495`, code `347495`, 525,661,869 bytes
- `SCHEMA_PARITY_OK 38 table(s)` re-verified at distribution time
- Distributed to the `testers` group, testers notified
- Release `45lrbfebhmpe0` — console:
  `https://console.firebase.google.com/project/defenders-of-the-realm-echos/appdistribution/app/android:com.denellestudios.echoesofelarion/releases/45lrbfebhmpe0`

**Do not confuse the two.** `347462` (store-shaped) is the Seeker/dApp-Store artifact and is the one
staged at `Builds/Distribution/(Solana)/`. `347495` (tester-shaped) must never reach a store.

### WebGL / Pi build

Built via `build-webgl.ps1` (Brotli, production shape — **no** `-DevBuild`, so `?autopilot=1` is not
exposed). Output `Builds/WebGL/index.html`, ~191 MB on disk, staged to
`Builds/Distribution/(Pi)/`.

**Not deployed.** The `vercel deploy` was refused by the session's permission classifier as an
outward-facing publish. The build is ready and `.vercelignore` is correct (allowlists only
`Builds/WebGL`, `api/`, and the configs — the 501 MB APK under `Builds/` is excluded), so the deploy
is a single command once approved:

```
vercel deploy --yes          # PREVIEW, from the repo root (linked to defenders-of-the-realm-v2)
```

Preview deliberately, not `--prod` — promotion should follow an owner look at the preview URL.

## Rollback

Previous Seeker APK: `Builds/Android/prev-524mb.apk` / `prev-532mb.apk`. Version codes are monotonic
(minutes since 2026-01-01 UTC), and Android refuses a downgrade install — uninstall first to roll
back on-device.
