# WO-596 — Player-facing Bug Report (the F8 harness, skinned for players)

**Status:** READY TO IMPLEMENT
**Lane:** 4 (UI/HUD) + 7 (backend endpoint) — UI and API slices are file-disjoint
**Origin:** owner design conversation 2026-07-02 ("thinking out loud" → decided shape)
**Supersedes:** the HelpMenu "Report a bug" stub (owner: "a create-in-10-seconds and never look at")

## Why
The current stub (`Assets/_Modules/HUD/HelpMenu.cs:31-330`) POSTs to the **retired** Vercel domain
`defenders-of-the-realm.vercel.app/api/bug-report` (live project is `defenders-of-the-realm-v2`) — reports
go nowhere — with a mailto-to-personal-gmail fallback. Meanwhile the capture side already exists and is
proven (F8 `BreakCaptureHarness`: clean-frame screenshot + note + jsonl) and the transport rail already
exists and is proven (WebTrace → `api/trace` → Vercel logs → Neon). This WO is a skin + a plug, not a system.

## Decided design (owner-ratified 2026-07-02)
- **The submit button IS the consent.** NO extra "do you want to send?" dialog (kills report volume),
  NO silent collection (kills trust). The form SHOWS what will be sent.
- Form contents when opened from Settings:
  1. **Screenshot thumbnail** — captured on form open, clean frame (reuse the F8 capture path:
     screenshot BEFORE the form draws, same trick as `BreakCaptureHarness.OnFlag`). Untickable
     ("include screenshot" toggle, default ON) for the rare shy reporter.
  2. **Note field** — one multi-line text box, placeholder "What went wrong?"
  3. One quiet disclosure line: *"Includes recent game logs to help us fix it."* (not a checkbox —
     the logs always go; the line is the honesty.)
  4. **"Send report"** button (single CTA).
- On submit, auto-attach: recent FlowTrace tail (the in-memory ring / last N `[Flow:*]` lines), scene
  name, session id, app version, platform, Pi uid if signed in. Players can't judge diagnostic value —
  we take that burden.
- Confirmation = toast ("Report sent — thank you, defender."). No modal.
- **Privacy: identity is covered on submit (owner directive 2026-07-02).**
  - The capture frame HIDES identity-bearing UI before the screenshot and restores it after
    (the same one-frame trick BreakCaptureHarness uses for the F8 note box): the Pi sign-in
    button/username readout, chat panel, and any surface showing a player name. Mechanism:
    a small registry (`PrivacySensitiveUi.Register(GameObject)`) that identity-displaying
    widgets opt into; capture toggles registered objects off for the frame. No image
    post-processing — hidden at the source, can't miss.
  - Payload: `piUid` is sent as a salted hash (server can correlate repeat reporters;
    a raw uid never leaves the client in a bug report). Username is never sent.

## Implementation
1. **UI (Lane 4):** replace the HelpMenu stub flow with an Obsidian-conformant form — built via
   `ElarionUiKit.BuildObsidianPanel` drop-zones per `docs/UI_BLINK_TEMPLATE_CANON.md` (master frame,
   same canvas discipline, kit font, shared Close, no restyling). MVVM: `BugReportVM` holds note text /
   screenshot toggle / submit state; the View binds only.
2. **Capture (reuse):** factor the F8 clean-frame screenshot + recent-trace harvest in
   `BreakCaptureHarness` into a callable (`CaptureForReport()` returning png bytes + trace tail) —
   REUSE, do not duplicate the capture code in the HUD assembly (Core service, HUD calls via seam).
3. **Transport (Lane 7):** new `api/bug-report` endpoint on the LIVE Vercel project
   (`defenders-of-the-realm-v2`), same shape as `api/trace` (which it sits beside): accepts JSON
   {note, sceneName, sessionId, version, platform, piUid?, traceTail[], screenshotB64?} → writes to
   Neon (`bug_reports` table) + echoes a signal line to Vercel logs (readable without DATABASE_URL,
   same pattern as WebTrace). Screenshot size-capped (e.g. JPEG re-encode ≤ 300KB) — no unbounded blobs.
4. **Retire the stub:** remove the mailto fallback + the dead-domain constant; local
   `persistentDataPath/BugReports` copy may stay as offline fallback (send-on-next-boot optional,
   slice 2).

## Acceptance criteria
- [ ] Settings → Report a bug opens an Obsidian master-frame form showing thumbnail + note + disclosure line
- [ ] Submit POSTs to the live `-v2` endpoint; row lands in Neon; signal line visible in Vercel logs
- [ ] Screenshot toggle OFF → report sends without image
- [ ] Trace tail present in the payload (verify a `[Flow:*]` line round-trips)
- [ ] No new capture code duplicated from BreakCaptureHarness (reuse via the factored callable)
- [ ] Stub mailto + old-domain constant removed
- [ ] Works in WebGL (the primary audience is the Pi focus group)

## Do NOT touch
- The F8 break-log flow itself (owner's own capture path stays as-is)
- `api/trace` (sit beside it, don't overload it)
- PackStore / monetization surfaces

## Notes
- This makes every web player an F8 reporter — the break-log queue fills from the focus group.
- UI slice implementation is CLI-seat work per `ui-work-cli-owns-docs-first-screenshot-compare`
  (screenshot-compare against the Blink template before calling it done).
