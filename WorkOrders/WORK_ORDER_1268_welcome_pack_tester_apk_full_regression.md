# WORK ORDER 1268 — Welcome Pack tester APK + full regression

**Status:** IMPLEMENTED — TESTER APK BUILT, REGRESSION GREEN, SIDELOADED
**Minted:** 2026-08-28 by Codex CLI from Samantha's unnumbered tester-build request; banner bumped 1268 → 1269 in the same edit.
**Lane:** Seeker tester release validation. Not PROD. Child validation lane of WO-1264.

## Scope

1. Run the authoritative full `DeNelle.Editor.DataRegression.RunAll` gate; never substitute focused tests.
2. Build a fresh release-signed tester APK through `overnight-apk-build.ps1 -Tester`, including the WO-1264 hidden `welcome-500` / `welcome-100` packs, Welcome Letter, and generic promo-pack client support.
3. Require schema parity and Android R2 catalog parity from the sanctioned build chain.
4. Report exact APK path, version name/code, regression verdict, blockers, and Seeker sideload safety.
5. Fix the exact on-device blocker found during the pre-build smoke: a player-pressed Redeem may
   mint a missing/expired wallet auth session and retry safely; passive/background calls remain silent.
6. Author the Welcome Letter CTA above the 112 px touch minimum instead of relying on runtime clamping.
7. Latest owner direction: present the letter once only after a confirmed `FIRSTWATCH` redemption,
   after the redeem panel closes, with an explicit `Close` button; failed attempts never trigger it.

## Safety locks

- Production `FIRSTWATCH` remains currency-only; do not update any live `reward_pack_sku` or tier pack SKU.
- `TESTER_BUILD` may expose deliberate tester tools but may not bypass production wallet, onboarding, promo, or payment behavior.
- Do not distribute or submit a build whose regression, signing, freshness, schema, or R2 parity proof is absent.
- A real successful promo redemption requires a non-owner signed wallet. Refused unsigned attempts must not consume the code.

## Device observation before build

- Existing store-shaped build `345292` launched on Seeker `SM02G4061955851` without an app crash.
- Welcome Letter runtime marker emitted and campaign code remained withheld.
- Promo attempts without a live signed wallet session were safely refused as `identity-proof-refused`; no grant/code consumption was observed.
- The letter CTA triggered the touch oracle clamp (authored height 104.1 px, grown to 112 px); record this in the final risk verdict.

## Acceptance

- [x] `REGRESSION_OK 314/314 suites` from fresh `Builds/data-regression-wo1268-final2.log`.
- [x] Fresh tester APK produced after the regression run.
- [x] APK built through `overnight-apk-build.ps1 -Tester` with `TESTER_BUILD`, increasing code
  `345316`, stable in-place install, and no production-behavior bypass.
- [x] `SCHEMA_PARITY_OK`, `APK_OK`, `R2_PARITY_OK 45 object(s)`, and `APK_DONE` are present.
- [x] Safe for Seeker tester sideload; installed in place on `SM02G4061955851` and launched with
  no app fault. Wallet-holder approval remains required for the successful signed redeem smoke.

## Result — 2026-08-28

- APK: `D:\eoa\Builds\Android\DefendersOfTheRealm.apk`
- Version: `2026.08.28.345316` (`versionCode=345316`), 577,178,989 bytes (~550 MB).
- Full regression: **PASS**, 314/314 green, 0 red, 0 skipped.
- Focused First Watch source checks: 3/3 green.
- Schema: 19/19 tables; Android remote content: 45/45 objects.
- Exact-device launch: process remained alive, no fatal/app exception, and the thank-you letter did
  not interrupt boot. It now queues only after confirmed `FIRSTWATCH` success and shows after the
  redeem panel closes with an authored-size `Close` button.
- Redeem auth: a player-pressed Redeem can mint a missing/expired wallet session once; failed
  handshakes abort before sending or consuming the code. Background calls remain silent.
- Live safety: no production promo mutation was made. `FIRSTWATCH` remains currency-only with all
  pack SKU fields NULL until the dApp Store build is confirmed propagated.
