# WORK ORDER 1264 — Launch Welcome Pack: 4pm-safe live promo + next-build packs and letter

**Status:** IMPLEMENTED / LIVE TRACK ACTIVE / NEXT APK BUILT
**Minted:** 2026-08-28 by Codex CLI from Samantha's unnumbered work order; banner bumped 1264 → 1265 in the same edit.
**Lane:** Monetization/live operations and next Seeker APK. Not PROD. Supersedes WO-1256 for release tracking; WO-1256 retains the implementation history.

## Track A — live for 4pm CT

- Public code `FIRSTWATCH`; one redemption per signed wallet.
- Atomic two-tier currency grant: ordinals 1–500 receive 500 crystals + 500 coins; later ordinals receive 100 + 100.
- Every success snapshots the signed wallet ID and exact `redemption_ordinal`, preserving the first-500 cohort for future rewards.
- Expires `2026-08-31T04:59:00Z` (Sun 11:59pm Chicago/CDT).
- Production row must keep `reward_pack_sku`, `tier1_pack_sku`, and `tier2_pack_sku` NULL while today's published APK is in use.
- `TEST10` inactive. Code is for Discord only, never X, logs, traces, analytics, or the welcome letter.
- Neon migration precedes Vercel deploy. Tier selection and redemption insert are one atomic SQL statement so duplicates cannot consume an ordinal.

### Delivered state

- Neon schema parity: 19 tables green.
- Vercel production deployment: `dpl_7Co7wg4R1EYLnnSR2Q8wZVzWijed`, aliased to `https://defenders-of-the-realm-v2.vercel.app`.
- Live row verified active with 500/500 → 100/100, all pack fields NULL, zero redemptions at activation, exact expiry.
- Non-consuming smoke verified the production route rejects a wallet-shaped invalid session with HTTP 401.
- Final value-grant smoke requires a real non-owner signed wallet; never fabricate or burn one from CLI.

## Track B — next APK / store propagation

- Both canonical `packs.json` mirrors contain hidden `welcome-500` and `welcome-100` SKUs (`storeVisible=false`, `promoGrantOnly=true`).
- `welcome-500`: 500 wood, iron, stone, crystals, and coins.
- `welcome-100`: 100 wood, iron, stone, crystals, and coins.
- First-session letter uses Creative's `welcome-letter-complete-v1.png`, with the approved
  “Welcome to the Watch” copy and native “Hold the Line” button; no promo code shown. The empty
  `welcome-letter-scroll-frame.png` and source copy are retained with the WO/build assets.
- Generic `redeem.js` pack-tier support remains available for the later operator cutover.
- Do not set live pack fields until dApp Store propagation proves Seekers are running this catalog.
- Fresh store-shaped APK: `Builds/Android/DefendersOfTheRealm.apk`; Android R2 parity verified for all 45 referenced objects.

## Acceptance and gates

- Focused Node tests green; Unity compile gate green; full regression `314/314`.
- Pack catalog mirrors byte-identical.
- Payment flags and purchase rails untouched.
- No code burns against absent SKUs.
- Future DB-driven arbitrary packs remain a separate follow-up (WO-1258).

## Changesets

- `b32856648` — next-build hidden packs, welcome letter, generic pack-tier support, APK.
- `c2f8e1d08` — production-safe currency-tier split and live deployment.
