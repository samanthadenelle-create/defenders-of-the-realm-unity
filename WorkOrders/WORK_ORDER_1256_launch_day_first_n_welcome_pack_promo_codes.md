# WORK ORDER 1256 - First Watch two-tier Welcome Pack and letter

**Status:** SUPERSEDED BY WO-1264 (implementation history retained) - 4pm live promo uses pack-free currency tiers; next APK contains the
hidden resource packs and letter. Store propagation, not APK creation, gates switching the live row
to pack SKUs.
**Lane:** Monetization/live ops plus the next Seeker APK. **NOT PROD.** This same WO owns the server tier, two hidden baked pack SKUs, the text-only launch letter, Vercel api deploy, and APK smoke. This ruling supersedes the earlier server-only/no-APK draft. Full Neon DB-driven packs catalog remains a follow-up.

## Binding product ruling (Samantha GO 2026-08-28)

### Critical 4pm safety split (Monetization correction, binding)

- Today's published Seeker APK does not contain the new SKUs. For the 4pm campaign, `FIRSTWATCH`
  must have `reward_pack_sku`, `tier1_pack_sku`, and `tier2_pack_sku` all NULL.
- Live rewards are crystals/coins only: redemptions 1-500 receive 500/500; later redemptions receive
  100/100. The same atomic ordinal and one-wallet rule apply.
- The hidden five-resource packs and welcome letter remain in the next APK. Do not point the live
  promo at those packs until dApp Store propagation proves that APK is what Seekers actually run.

1. **packs.json SKUs** `storeVisible=false` (both StreamingAssets and Resources mirrors; byte-identical):
   - `welcome-500`: 500 wood, 500 iron, 500 stone + crystals/coins. **PRIMARY (author this):** 500 crystals + 500 coins. **SAFER OVERRIDE (Monetization note only; do NOT author unless Sam changes the ask):** 250 crystals + 100 coins; wood/iron/stone stay 500.
   - `welcome-100`: 100 each wood / iron / stone / crystals / coins.
   - No cosmetics, no convenience, not for sale, not impulse, not on the shelf.
2. **Promo `FIRSTWATCH` two-tier via `reward_pack_sku` / tier:** first 500 successful distinct-wallet redemptions receive `welcome-500`; every later successful redemption receives `welcome-100` until expiry. Expiry **Sun 2026-08-30 23:59 Chicago/CDT** = `2026-08-31T04:59:00Z`. Kill `TEST10` (deactivate, never delete). Neon + `api/promo/redeem.js` + **Vercel**. One per proven wallet. Guests remain ineligible.
3. **In-build custom Welcome Letter (first session)** — `WELCOME TO THE WATCH` / `Welcome to the Watch`. Shown once at the first safe gameplay moment after onboarding. **No code on the letter.** Existing code-built Obsidian UI; no new art.
4. **APK build + smoke.**

**Must not:** PROD lane; payment flags (`RealmStorePurchase`, `WalletService.Pay`, `PurchaseGate`, IAP); logging codes (never print `FIRSTWATCH` / `TEST10` in logs, traces, F8, analytics, or the letter).

- Server message: `Welcome to the Watch.`
- JS MUST NOT special-case the campaign string; two-tier is column-driven. Map GO `reward_pack_sku` / tier onto generic columns (`tier1_pack_sku` / `tier1_limit` / `tier2_pack_sku` or equivalent). Existing `reward_pack_sku` is the fallback SKU if a second column is not used. Do not hardcode `welcome-500` / `welcome-100` in JS.
- Row crystals/coins MUST be 0 when a pack SKU is paid: `PromoCodeService.ApplyReward` applies pack contents AND JSON crystals/coins; non-zero would double-grant.
- One redemption per proven wallet (`UNIQUE (code, player_id)`). `per_player_limit` stays NULL (that column is a cross-code cap; setting it to 1 locks out anyone who already burned `TEST10`).

## Implementation

1. Add both SKUs identically to the StreamingAssets and Resources mirrors of `packs.json`. Unique pack `tier` numbers (next free after current max 26: `welcome-500` = 27, `welcome-100` = 28).
2. Add generic tier columns to `promo_codes`: `tier1_pack_sku`, `tier1_limit`, `tier2_pack_sku` (and/or reuse existing `reward_pack_sku` as tier2), and atomic `redemption_count`. Add `promo_redemptions.pack_sku` so the exact delivered tier is snapshotted for audit. Document ALTERs in `api/schema.sql`; do not put new columns in CREATE TABLE until they exist on live.
3. `api/promo/redeem.js` selects the tier generically, never by checking the campaign code. Ordinal increment and redemption insertion occur in one SQL statement. A duplicate/failed insert rolls back the counter increment. Old clients without `supportsPackRewards` are refused before the code is consumed. Do not log the code string.
4. Deploy api to **Vercel** after Neon ALTER and before (or with `active=FALSE` until) `FIRSTWATCH` is live. The new APK that contains both SKUs must be the Seeker binary before `active=TRUE`: current APK already sends `supportsPackRewards=true`, so a live pack SKU on an old catalog burns the one-shot and grants nothing.
5. The existing client receives `packSku`, resolves the baked pack through `PackCatalog.Find`, and grants through `PackStoreVM.ApplyPackContents` / the promised-purchase economy seam. Do not touch payment flags.
6. The launch letter waits for `Onboarded`, a gameplay scene, no active modal, and no battle lock. Persist seen-flag. Do not revive WO-1012 retired welcome cards. Do not reuse `WelcomeBackPopup`.

## Acceptance

- Both catalog mirrors parse and are byte-identical; both hidden packs contain the exact PRIMARY baskets; `storeVisible=false`.
- Redemptions 1-500 snapshot/return `welcome-500`; 501 onward snapshot/return `welcome-100`.
- Concurrent duplicate wallet requests grant once and do not consume an ordinal.
- Production migration runs before the API / Vercel deployment and campaign activation.
- `TEST10` is inactive and `FIRSTWATCH` has the exact expiry and tier configuration.
- A non-owner Seeker wallet on the **new** APK receives all five resources (500/500/500/500/500 on first 500) and cannot redeem twice.
- Letter opens once, never during onboarding/battle/another modal, and contains no campaign code.
- Unity compile/regressions pass and the Seeker APK is built and smoked.
- Payment flags unchanged. No PROD retarget. Code string never logged.

## Files in scope

- both canonical `packs.json` runtime mirrors
- `Assets/_Modules/Onboarding/FirstWatchWelcomeLetter.cs`
- `api/schema.sql`, the WO-1256 migration, and `api/promo/redeem.js`
- Vercel api deploy + Neon FIRSTWATCH row (`active=FALSE` until new APK)
- focused regressions/tests, Android APK, and this work order

Do not publish marketing posts from this lane. Do not build the DB-driven arbitrary-pack catalog under this ticket. Do not start a second Codex/CLI writer; this WO is already assigned.
