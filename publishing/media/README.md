# Listing media — drop your files here

Every file below is referenced by `publishing/config.yaml`. **Use these exact
filenames** and they wire up with no config edit.

## Where the numbers come from

Every dimension marked **VERIFIED** was read out of the dApp Store CLI's own
validation source, not from a blog or from memory:

- `@solana-mobile/dapp-store-cli@0.15.0` →
  `src/config/PublishDetails.ts` (functions `checkIconDimensions`,
  `checkScreenshotDimensions`, `checkBannerDimensions`,
  `checkFeatureGraphicDimensions`, `checkVideoDimensions`, `checkImageExtension`,
  `checkVideoExtension`) — line numbers cited per row.
- `@solana-mobile/dapp-store-publishing-tools@1.0.0` →
  `src/schemas/releaseJsonMetadata.json` for the string length caps.

Anything the published docs do not state is marked **UNVERIFIED** rather than
guessed. In particular: Solana Mobile's current *web* Publisher Portal does not
publish its asset specs anywhere in `docs.solanamobile.com` (checked
`/dapp-store/submit-new-app`, `/dapp-store/publishing-cli`, `/dapp-store/intro`,
`/dapp-store/publisher-policy` and the `llms.txt` index this session). The specs
below are the CLI's, which is the only authoritative machine-checked source that
exists. **Treat them as the target; the portal form may show its own limits —
follow the form if it disagrees.**

---

## Required files

| File | Purpose | Dimensions | Format | Status |
|---|---|---|---|---|
| `icon-512.png` | App icon **and** release icon | **exactly 512 x 512** (square, VERIFIED — `PublishDetails.ts:288`; error text at `:218` is *"Icons must be 512px by 512px."*) | `.png`, `.jpg`, `.jpeg` or `.webp` (VERIFIED — `checkImageExtension`, `PublishDetails.ts:242-250`) | ☐ TODO — owner |
| `banner-1200x600.png` | Store banner | **exactly 1200 x 600** (VERIFIED — `PublishDetails.ts:300`; the CLI hard-fails with *"Please specify banner image of size 1200x600"* at `:129` if it is missing entirely) | same four formats | ☐ TODO — owner |
| `screenshot-01.png` | Screenshot 1 | **at least 1080 px in BOTH width and height** (VERIFIED — `PublishDetails.ts:294`) | same four formats (VERIFIED — `:147`) | ☐ TODO — owner |
| `screenshot-02.png` | Screenshot 2 | same | same | ☐ TODO — owner |
| `screenshot-03.png` | Screenshot 3 | same | same | ☐ TODO — owner |
| `screenshot-04.png` | Screenshot 4 | same | same | ☐ TODO — owner |

### How many screenshots
**At least 4 screenshots *and/or* videos combined.** VERIFIED —
`PublishDetails.ts:179`: `if (screenshots.length + videos.length < 4) throw ...
"At least 4 screenshots or videos are required for publishing a new release."`
There is **no documented maximum** — UNVERIFIED. Four is the floor; more is fine.

### Screenshot aspect ratio
**There is no aspect-ratio check.** VERIFIED — the only screenshot rule in the
CLI is the ≥1080 px floor on each axis. So:

- `1920 x 1080` landscape — **passes** ✅ (this is the natural one: the game is
  landscape-only)
- `1080 x 1920` portrait — passes ✅
- `1080 x 1080` square — passes ✅
- `1920 x 900` — **FAILS** ❌ (height < 1080)
- `1280 x 720` — **FAILS** ❌ (both axes short)

**Recommended: 1920 x 1080 PNG, captured on the Seeker.** Grab them with
`adb exec-out screencap -p > screenshot-01.png` (adb lives at
`C:\Program Files\Unity\Hub\Editor\6000.4.8f1\Editor\Data\PlaybackEngines\AndroidPlayer\SDK\platform-tools\adb.exe`).
Check the Seeker's native capture resolution is ≥1080 on both axes before
relying on it — if the device captures at e.g. 1080x2400 portrait that still
passes, but a landscape game captured portrait will *look* wrong in the listing.

---

## Optional files

| File | Purpose | Dimensions | Format | Notes |
|---|---|---|---|---|
| `feature-graphic-1200x1200.png` | Feature graphic | **exactly 1200 x 1200** (VERIFIED — `PublishDetails.ts:306`) | png/jpg/jpeg/webp | The `featureGraphic` entry is **commented out** in `config.yaml`. Uncomment it *only after* the file exists — an entry pointing at a missing file is a hard error (`PublishDetails.ts:144`). |
| `trailer.mp4` | Promo video | **at least 720 px in both width and height** (VERIFIED — `PublishDetails.ts:312`) | **`.mp4` only** (VERIFIED — `checkVideoExtension`, `:252-257`) | Also commented out in `config.yaml`. A video counts toward the 4-asset minimum. Max length / max file size: **UNVERIFIED**. |

---

## Text limits (for reference — these live in `config.yaml`, not here)

| Field | Limit | Source |
|---|---|---|
| `release.catalog.<locale>.name` | 32 chars | `releaseJsonMetadata.json` → `name.maxLength: 32`. *"Echoes of Elarion"* = 17 ✅ |
| `short_description` | **50** per the JSON schema, but **30** per the CLI's own pre-check | schema `localized_resources.short_description.maxLength: 50` **vs** `PublishDetails.ts:278` (`desc.length > 30`) |
| `long_description` | no limit found | `releaseJsonMetadata.json` — no `maxLength` |
| `new_in_version` | no limit found | as above |

⚠ **Known conflict:** the canon subtitle *"Echoes of a Forgotten Civilization"*
is **34 characters** — passes the 50-char schema, fails the CLI's 30-char check
by 4. The current web portal's limit is UNVERIFIED. It is left verbatim in
`config.yaml`; the owner rules on whether to shorten it. Do not trim it silently
— it is canon (`CLAUDE.md` §7).

---

## The APK is NOT media, but read this anyway

`config.yaml` points `release.files[install]` at
`../Builds/Android/DefendersOfTheRealm.apk`.

⚠ **The APK sitting there right now is the TESTER build** — 572,202,298 bytes,
built 2026-08-08 14:41, i.e. *before* `FeatureFlags.RealmStorePurchase` was gated
off. **It must be rebuilt** (`Defenders/Build/Android APK (Seeker)`) after that
flag change lands, and re-verified, before any release is created. Publishing
mints an on-chain release NFT pointing at that exact binary — undoing it is
expensive and slow.

Note also: that APK is **~546 MiB**. Permanent-storage upload cost (ArDrive /
Turbo / Arweave) scales with size and is **not** covered by the ~0.2 SOL figure
the docs quote for transaction fees. Budget for it separately — the exact rate is
**UNVERIFIED**. Any dApp Store maximum APK size is also **UNVERIFIED**.
