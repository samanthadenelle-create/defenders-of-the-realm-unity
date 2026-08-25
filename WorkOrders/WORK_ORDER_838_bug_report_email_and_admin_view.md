> ⚠ **NUMBER COLLISION — this document does not own WO-838; `WORK_ORDER_838_raidbase_material_survivability.md` does.**
> Referred to hereafter as **WO-838-B (bug-report email + admin view)**.
> Flagged by the 2026-08-16 Sunday board-grooming pass (`python tools/board_build.py` → `DUPLICATE_WO_NUMBERS`);
> ownership decided by **first-on-disk** (`git log --follow --diff-filter=A`): the winner's file was created first.
> Banner only — nothing was renumbered or deleted.

> ## COLLIDED NUMBER (4th two-seat collision, 2026-08-02) + SUPERSEDED
> The number 838 belongs to WORK_ORDER_838_raidbase_material_survivability.md (committed). This spec is
> SUPERSEDED by WO-846 (bug-report attribution + f8-inbox watcher, IMPLEMENTED) per its own 08-02 note. Do not implement.

# WORK ORDER 838 — Bug-report email-on-submit + admin bug-report view (server-side, -v2)

**Status:** SUPERSEDED 2026-08-02 by **WO-846** — do NOT implement this. WO-846 already delivers the house-pattern
notification: a **bug-report watcher that pings the F8 inbox** (`logs/f8-inbox/`, the same surface CLI live-triages,
§14) — better than email (the report reaches the person who RCAs it, evidence attached, automatically). WO-846's
agent is also handling the `view=bugreports` gap in `api/admin/db.js` this WO predicted. Kept for the record only.
**Premise correction:** this WO wrongly said `api/` lives in a separate "-v2 Vercel project." It does NOT — `api/`
is **git-tracked IN THIS REPO** (`api/bug-report.js` etc., START_HERE §3 / KEY_FACTS). The DB write is confirmed at
`api/bug-report.js:125` (`INSERT INTO bug_reports (description, route, app_version, player_id, context)`).
**Author:** UI/QA triage (read-only RCA, §13) — Claude UI
**Lane:** Backend (`api/` — IN THIS REPO). Superseded by WO-846.
**Origin:** owner 2026-08-02 — wants to be **emailed when a bug report comes in** (instead of manually checking the
DB), and to confirm the DB write lands. Clarified: reports come from the **APK** build too (platform-agnostic).

---

## 1. Context (verified read-only, this repo)
- In-app **Report a Bug** (`BugReportView` → `BugReportVM.Submit`, `Assets/_Modules/HUD/BugReportVM.cs:97-141`) POSTs
  JSON to **`https://defenders-of-the-realm-v2.vercel.app/api/bug-report`** — the same -v2 host as `api/trace`.
  Payload (`:143-176`): `note, sceneName, sessionId, version, platform, piUid?(salted SHA-256), traceTail[], screenshotB64?`.
- **Platform-agnostic:** it's a `UnityWebRequest` POST, so it fires from the **APK/Seeker** build as well as web —
  NOT tied to the web-only `?trace=1` pump. APK bug reports go to `api/bug-report` → (server) Neon.
- **The DB write + any email are SERVER-side** in the -v2 project (`api/bug-report.js`), which is a SEPARATE repo/
  deploy — none of it is in this Unity tree. Today there is **no email** to the owner (the intended check-path is the
  `triage-web-issue` skill pulling from the DB via `api/admin/db`).

## 2. Build (both in the -v2 Vercel project)

### 2a. Email the owner on each new bug report (`api/bug-report.js`)
- After the report row is inserted into Neon, send an email to the owner with: `note`, `sceneName`, `platform`,
  `version`, `sessionId`, the salted `piUid` (if present), the row id, and the `traceTail` (last N lines inline or a
  link to view it). Screenshot: attach the JPEG OR include a link to it — implementer's call (attaching is simplest).
- **Non-blocking / fire-and-forget:** the client's 200 response and the DB insert MUST NOT depend on the email. Wrap
  the send in try/catch so a mail failure never fails the report (the app already treats non-200 as "retry/save
  local" — don't turn a mail hiccup into a lost report). Log mail failures server-side.
- **Provider:** use **Resend** (Vercel-native, simplest) via the Vercel Marketplace — when implementing, load the
  `marketplace` skill first to provision it (do NOT hardcode an SDK/provider before that). If the -v2 project already
  has a mail provider wired, reuse it.
- **Env (server-only, never committed):** `RESEND_API_KEY` (or the chosen provider's key) + `BUG_REPORT_TO_EMAIL`
  (the owner's address) — set in the Vercel project env, same pattern as `ADMIN_DASH_KEY` / `DATABASE_URL`.
- Subject suggestion: `🐛 EoA bug — <scene> (<platform> v<version>)`; body = the fields above. Rate-limit / de-dupe
  optional (a burst of retries from one session shouldn't spam — key on `sessionId` + a short window) — nice-to-have.

### 2b. Admin `view=bugreports` (`api/admin/db.js`) — so APK reports are triageable like traces
- The documented admin views are trace-focused (`traces`/`metrics`/`players`/`overview`). Add a **read-only**
  `view=bugreports` (GET, `X-Admin-Key` gated, hard `LIMIT`, default 20 / max 50) returning recent rows:
  `id, created_at, platform, version, sceneName, note, sessionId, piUid, hasScreenshot`. This lets the
  `triage-web-issue` skill (and the owner's db-viewer GUI) pull bug reports the same way it pulls web traces.
- Confirm `view=overview` lists the bug-report table row count + newest timestamp (the quick "is the write landing?"
  check the owner asked for). If the table isn't in `overview`, add it.

## 3. Acceptance criteria
- [ ] Submitting Report-a-Bug from the APK (and web) inserts a Neon row AND sends the owner an email with the note +
      trace tail + scene/platform/version (+ screenshot attached or linked).
- [ ] A mail-provider failure does NOT fail the report (client still gets 200; row still written; error logged).
- [ ] `GET /api/admin/db?view=bugreports` (with `X-Admin-Key`) returns recent reports; `view=overview` shows the
      bug-report table count + newest timestamp.
- [ ] No secret committed (provider key + owner email live in Vercel env only).

## 4. What NOT to touch
- Do NOT change the Unity client (`BugReportVM`/`BugReportView`) — the payload contract + endpoint are correct; this
  is purely server-side in -v2.
- Do NOT make the email path blocking or let it drop reports on failure.
- Do NOT expose the screenshot/PII beyond the owner's email + the key-gated admin view (salted `piUid` stays hashed).
- Do NOT touch `api/trace` / the web-trace pipeline.

## 5. Note (Unity-machine handoff)
This WO is implemented + deployed in the **-v2 Vercel project**, not the Windows Unity machine. No `CompileGate`/
fleet applies. Verify by submitting a test report from a build and confirming (a) the email arrives and (b) the row
appears via `view=bugreports`.
