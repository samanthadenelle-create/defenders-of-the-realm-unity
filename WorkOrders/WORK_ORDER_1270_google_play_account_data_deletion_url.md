# WORK ORDER 1270 — Google Play account and data deletion URL

**Status:** DONE — implemented, tested, and deployed 2026-08-28.
**Minted:** 2026-08-28 by Codex CLI from the owner's unnumbered Google Play intake request; banner bumped 1270 → 1271 in the same edit.
**Lane:** Public website / privacy compliance. No gameplay, payment, production-data, or Unity changes.

## Goal

Publish a stable public URL suitable for Google Play's **Delete account URL** field. The page must
name **Echoes of Elarion** and **DeNelle Studios**, prominently explain how a user requests deletion,
and distinguish account deletion from deletion of data associated with guest play.

## Required content

1. A clear **Request account and data deletion** heading above the fold.
2. Exact request steps using `support.eoa@icloud.com`, including which identifier the player may
   provide (Google-account email/identifier once supported, wallet address for other distributions,
   guest/report/session reference where available). Never request passwords, wallet signatures,
   seed phrases, private keys, purchase tokens, or full payment details.
3. State what is deleted: account/binding record where one exists, cloud saves, gameplay analytics
   associated with the supplied identifier, diagnostics, and bug reports.
4. State what may be retained and why: minimum records required for legal, fraud-prevention,
   chargeback, purchase/entitlement, security, and audit obligations; give a retention description
   without inventing a fixed period unsupported by current policy or schema.
5. Explain local device data is removed through Android app storage/uninstall and is not remotely
   erased by the web request.
6. Offer a request for deletion of some or all associated data without requiring account deletion.
7. Link to the privacy policy and support email. No wallet/SKR/Pi marketing on this compliance page.

## Route and implementation

- Preferred route: `/delete-account` on the existing public website.
- Page must work as a direct navigation and on mobile, carry existing site security headers/style,
  and require no login or JavaScript to read the instructions.
- Add route/header configuration only if needed by the existing static-host structure.
- Do not deploy until the root seat verifies the page and confirms the production URL.

## Acceptance

- `site/delete-account.html` (or an equivalently stable static route) contains every required item.
- No secret or sensitive credential is requested.
- Automated test pins the page title, app/developer identity, support address, deletion scope,
  retention statement, local-data instruction, partial-deletion option, and privacy link.
- Existing website tests remain green and `board_build` reports `BOARD_CHECK_OK`.
- Root reports the exact URL for Play Console only after deployment/route verification.

## Must not

- Do not claim Google Sign-In/account creation is already live; it is planned, not shipped.
- Do not claim every record is deleted when legally/security-required records may need retention.
- Do not build the account-deletion backend under this page ticket.
- Do not deploy, commit, or push from an agent seat.

## Result

- Production URL: `https://echoes-of-elarion.vercel.app/delete-account`
- Live verification: HTTP 200 and required identity/support/deletion content present.
- Focused page tests: 5/5 passed.
- Full Node suite: 198/198 passed.
- Deployment: Vercel production `dpl_GrWwoRoi5zrswnWqNqDBLUcjx9kR`, aliased to the URL above.
