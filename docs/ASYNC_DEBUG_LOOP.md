# ASYNC_DEBUG_LOOP.md - the phone-drop -> cloud-triage loop

> Owner drops a message from her phone at the office; a scheduled Claude routine
> checks the Notion "CLI Inbox", debugs from LIVE web-trace data, triages per the
> ticket pipeline, mints a Work Order if needed, writes the answer back, and flips
> the row's Status. This doc is the runnable spec for that loop.
>
> Sibling doc: `docs/WEB_SELF_HEAL_LOOP_PLAN.md` - the browser-bot self-heal loop
> (Seeker/Chrome drives the deployed build, harvests console/network, ~60% built).
> That one GENERATES web-trace data by driving the app; THIS one CONSUMES the owner's
> dropped messages + the already-flowing web traces. They are complementary.

Created 2026-07-12. ASCII-only. Keep in sync with KEY_FACTS.md (Backend/web section).

---

## 0. THE PIECES (verified 2026-07-12)

- **Notion "CLI Inbox"** (phone drop-box, created this session):
  - URL: https://app.notion.com/p/011d90a405634b4681851a9bb1c8c73a
  - Database id: `011d90a4-0563-4b46-8185-1a9bb1c8c73a`
  - Data source id (API/MCP writes): `5ccde93a-d6e6-4e16-96a0-ae0b8b301400`
  - Parent: *Defenders of the Realm - Pipelines (Source of Truth)* page
    `378bf190-c689-81d0-b63f-e44b5661fa8f` (same parent as the Work Orders board)
  - Properties: **Title** (title = the message) | **Status** (select: New /
    Triaged / WO-Created / Answered / Wont-Do) | **Type** (select: Bug / Question /
    Idea / Task) | **Reply** (text - the CLI writes its answer here) |
    **Created** (created_time)
- **Notion "Work Orders" board**: db `f3115f05...`, data source
  `5f66b263-c732-4075-b94a-f5f4de9f8087`. Props: Title, WO (number), Lane, Status,
  Priority, Source, Depends On, Notes. See `NOTION_SOURCE_OF_TRUTH.md`.
- **Web-trace pipeline (LIVE)**: WebGL build with `?trace=1` streams FlowTrace +
  Unity errors -> `POST /api/trace` -> Neon `analytics_events`
  (`event_name='web_trace'`). CLI read path = the `[sig]` echo in Vercel **runtime
  logs** (`DATABASE_URL` is sensitive/unpullable). See `api/trace.js`.
- **db-viewer** (secondary read): `tools/db-viewer/index.html` + `api/admin/db.js`,
  key-gated by `ADMIN_DASH_KEY` (Vercel env). A Traces read view.
- **WO numbering**: mint from the `CLI_LANES_WO_NUMBERS.md` banner. **Next free: READ IT OFF THE BANNER** (read it OFF THE BANNER - never restate it here; a copied number is exactly what goes stale)
  (as of 2026-07-12); bump the banner in the same edit.

---

## 1. INBOX CHECK - read New rows

Query the CLI Inbox for `Status = 'New'` (MCP `notion-query-data-sources`, SQL mode):

```
SELECT url, Title, Type, Created
FROM "collection://5ccde93a-d6e6-4e16-96a0-ae0b8b301400"
WHERE Status = 'New'
ORDER BY datetime(Created) ASC
```

Each row = one dropped message. `url` is the page id you write Reply/Status back to.
No New rows -> the loop still runs step 2 (proactive web-data sweep) so real errors
surface even when the owner dropped nothing.

---

## 2. WEB-DATA CHECK - pull recent web-trace signal from Vercel runtime logs

The PROVEN read path (DATABASE_URL unpullable). Use the Vercel MCP:
`mcp__plugin_vercel_vercel__get_runtime_logs` (or `vercel logs <deployment-url>`)
against the current preview deployment. Grep for the per-line echo `  [sig] ` that
`api/trace.js` writes.

`api/trace.js` decides what is a signal line with EXACTLY this regex (line 64) -
use the SAME filter when classifying so cloud triage matches the sink:

```
/\[Flow:Pi\]|PiInit|PiAuth|Signing in|timed out|Exception|threw|softlock|NullReference|Fail|\berror\b|SeekerBootstrap|tier=|device=|\[Flow:Perf\]|\bfps=|\bF8\b|BreakCapture|flagged|\[Flow:Build\]|placement|TowerPlacement/i
```

Classify each `[sig]` line into an error-class:
- **FSB/decode / asset** -> `Exception|threw|NullReference` around load/decode/streaming.
- **Exceptions** -> `Exception|threw|NullReference|Fail`.
- **Softlocks** -> `softlock|flagged|F8|BreakCapture` (F8 harness fired).
- **LOW-fps / perf** -> `[Flow:Perf]|fps=` with a low number (perf regression).
- **Pi auth** -> `[Flow:Pi]|PiInit|PiAuth|Signing in|timed out`.
- **Build/placement** -> `[Flow:Build]|placement|TowerPlacement`.
- **Device/tier context** -> `device=|tier=|SeekerBootstrap` (attach to the above,
  not a class on its own).

Note: the AudioMixer boot "warning" noise is deliberately EXCLUDED by the sink -
don't treat its absence as missing data.

Get the current preview URL from `Builds/webgl-chain-status.txt` (`DEPLOY_URL`) or
`mcp__plugin_vercel_vercel__list_deployments`. Secondary: db-viewer via `api/admin/db.js`
with `ADMIN_DASH_KEY` if runtime-log retention has rolled the window.

---

## 3. TRIAGE - per docs/TICKET_PIPELINE.md (section 13)

For each inbox row AND each notable error-class from step 2:

1. **Classify NEW-FEATURE vs EXISTING** first (the section-13 gate).
   - **NEW** (behavior not built yet) -> do NOT RCA-fix the unbuilt. Route as a
     SPEC/WO to the PO. Reply on the inbox row that it's queued as a feature spec.
   - **EXISTING** (a built thing is misbehaving) -> RCA.
2. **EXISTING -> RCA from the CAPTURED line** (section 12 hard gate: no edit without
   captured data). CITE the verbatim `[sig] ...` line + its source (deployment +
   timestamp) as the proof. Static code-reading only LOCATES candidates; the trace
   line PINPOINTS. If no trace line proves it, the correct output is "need a capture"
   (ask the owner to reproduce with `?trace=1`, or drive it via the browser-bot loop),
   NOT a guessed fix.
3. **Ambiguous / no repro / no captured line** -> reply asking for detail; leave
   Status New or set Triaged with a question in Reply. Never work blind.

---

## 4. WO CREATION - mint + file + Notion row

When triage says a WO is warranted (EXISTING bug with RCA, or a NEW-FEATURE spec):

1. **Mint the number** from the `CLI_LANES_WO_NUMBERS.md` banner (next free: read it off the banner).
   Bump the banner's "next free" in the SAME edit (N -> N+1). Never mint from the
   filesystem max (collisions on record: 677, 678).
2. **Write** `WorkOrders/WORK_ORDER_NNN_short_name.md` with:
   - **Symptom** (the dropped message and/or the `[sig]` line, verbatim).
   - **RCA** (the captured proof line + why it fails) OR **Spec** (for NEW features).
   - **Steps / files to edit**, acceptance criteria, and what NOT to touch.
   - **Status: READY TO IMPLEMENT** (see the Unity limit, section 6 - a cloud run
     leaves code-fix WOs READY for the owner's machine; it does not build).
3. **Create the matching Work Orders Notion row** (data source
   `5f66b263-c732-4075-b94a-f5f4de9f8087`) via `notion-create-pages`:
   - Title `WO-NNN - short name`, WO = NNN, Lane (pick from the 13 lanes), Status
     `Ready` (fix ready to build) or `Spec` (new feature), Priority P0-P3,
     Source `This session`, Notes = one-line RCA + inbox link.

---

## 5. REPLY - close the inbox loop

Write back to the inbox row (`notion-update-page` on the row `url` from step 1):
- **Reply** (text): the answer, or the WO link
  `WorkOrders/WORK_ORDER_NNN_*.md` + the Notion WO row URL.
- **Status** ->
  - `Answered` - a Question answered, no WO needed.
  - `WO-Created` - a WO was minted (Reply carries the WO link).
  - `Triaged` - understood + needs owner input / a repro capture (Reply says what).
  - `Wont-Do` - out of scope / rejected (Reply says why).

Every row that started New must end in one of those four - never leave it New after
a run that read it.

---

## 6. THE UNITY LIMIT (state it plainly)

A CLOUD routine can do steps 1-5 end to end: read the inbox, read live web-trace
data, RCA, mint + write WO files, create/update Notion rows, reply. It **CANNOT**
run the Unity CompileGate or produce a build - those need the Unity editor open on
the owner's Windows machine (`DeNelle.Editor.CompileGate.Run` + the WebGL/desktop
batchmode). So:

- **Cloud run** = triage/RCA/WO/Notion only. Any code-fix WO is left **Status:
  READY TO IMPLEMENT** for the owner's machine to pick up, gate, build, deploy.
- **Local run** (Claude Code on the Windows box, machine left on) = can ALSO run the
  gate + build + deploy chain (`webgl-vercel-overnight.ps1`), i.e. steps 1-6 of the
  full fix. Promotion + `git push` stay the owner's call (per CLAUDE.md sole-committer
  + push-on-owner-OK rules).
- Never claim "fixed" from a cloud run - a WO written is not a WO built (section 12
  / the ten-year-old test: only the owner's hands prove feel).

---

## 7. ACTIVATION - turn it on with /schedule

The loop runs as a scheduled cloud routine (cron). It consumes credits while
running, so the OWNER flips it on/off - it is not on by default.

- **Create it:** invoke the `/schedule` skill -> create a routine on a cron cadence.
- **Recommended cadence:** every 45 min, 08:00-17:00 local (office hours), weekdays.
  Cron: `*/45 8-16 * * 1-5` (08:00-16:45; adjust to the machine timezone).
- **Stop it:** `/schedule` -> disable/delete the routine when the owner is back at
  her machine or done for the day.
- **Local alternative:** if the Windows machine stays on with Claude Code, run the
  same prompt on the `/loop` or a local schedule so it can ALSO gate+build (section 6).

### The exact prompt the routine runs (steps 1-5, compact):

```
You are the async CLI-Inbox checker for the Defenders of the Realm / Echoes of
Elarion project. Run this loop once, then stop:

1. INBOX: Query the Notion "CLI Inbox" data source
   collection://5ccde93a-d6e6-4e16-96a0-ae0b8b301400 for rows where Status='New'
   (notion-query-data-sources, SQL). Keep each row's url, Title, Type.

2. WEB DATA: Pull recent runtime logs for the current preview deployment
   (mcp Vercel get_runtime_logs; deployment URL from Builds/webgl-chain-status.txt
   DEPLOY_URL or list_deployments). Grep for '  [sig] ' echo lines. Classify each by
   this exact api/trace.js regex - FSB/decode, exceptions, softlocks(flagged/F8),
   LOW-fps, Pi-auth, build/placement:
   /\[Flow:Pi\]|PiInit|PiAuth|Signing in|timed out|Exception|threw|softlock|NullReference|Fail|\berror\b|SeekerBootstrap|tier=|device=|\[Flow:Perf\]|\bfps=|\bF8\b|BreakCapture|flagged|\[Flow:Build\]|placement|TowerPlacement/i

3. TRIAGE (docs/TICKET_PIPELINE.md sec 13): for each New row + each notable error,
   classify NEW-FEATURE vs EXISTING. EXISTING -> RCA from the captured [sig] line,
   CITE it verbatim as proof (never guess - section 12 hard gate). NEW -> route as a
   spec. Ambiguous/no-repro -> ask for detail, do not work blind.

4. WO (only if warranted): mint the next-free number from the CLI_LANES_WO_NUMBERS.md
   banner and bump it in the same edit; write WorkOrders/WORK_ORDER_NNN_short.md with
   Symptom + RCA(cited line)/Spec + steps + acceptance, Status READY TO IMPLEMENT;
   create the matching Work Orders Notion row (data source
   5f66b263-c732-4075-b94a-f5f4de9f8087) Title 'WO-NNN - short', Status Ready or Spec.

5. REPLY: notion-update-page each inbox row -> write Reply (answer or WO link) and set
   Status to Answered / WO-Created / Triaged / Wont-Do. No row read stays New.

LIMIT: you are a cloud run - you CANNOT run the Unity CompileGate or build. Leave
code-fix WOs as READY for the owner's Windows machine; never claim 'fixed'. Report a
one-line summary per inbox row + per WO minted.
```

---

## Cross-references

- `docs/WEB_SELF_HEAL_LOOP_PLAN.md` - the browser-bot self-heal sibling (~60% built).
- `docs/TICKET_PIPELINE.md` - the QA -> CLI -> PO role-separated pipeline (section 13).
- `CLAUDE.md` sections 12/13/14 - instrument-first, ticket pipeline, F8 live-triage.
- `NOTION_SOURCE_OF_TRUTH.md` - Work Orders board conventions + numbering authority.
- `KEY_FACTS.md` (Backend/web) - the WebTrace read path + db-viewer, kept in sync.
- `api/trace.js` - the sink + the exact signal regex quoted above.
- `CLI_LANES_WO_NUMBERS.md` - the WO numbering banner - THE SOLE AUTHORITY for the next free number.
