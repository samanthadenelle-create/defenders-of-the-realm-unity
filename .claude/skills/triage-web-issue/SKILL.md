---
name: triage-web-issue
description: Triage a live web/mobile build issue from anywhere (PHONE included) by pulling the real web-trace data out of the database, RCA-ing it, and writing a Work Order. Use when the owner says "triage", "triage this", "debug the web build", "pull the trace", "what broke on my phone", "what happened in the web build", "read the web logs", "create a work order for", "make a WO for", or describes a game bug she saw while playing the deployed web build. Turns a plain-words symptom into a proving-line-backed WO left READY for the next Windows/Unity machine.
---

# Triage a live web issue -> RCA -> Work Order (phone / async runbook)

This is the standing knowledge that lets ANY Claude Code session (a phone session with no
Unity, no vercel CLI, no repo checkout even) do the full front half of the ticket pipeline:
**pull the live web-trace from the database, find the proving line, classify, RCA, and write
the Work Order.** The code fix + build stay on the Windows machine (see THE UNITY LIMIT).

The owner plays the deployed web build in Pi Browser (or any mobile browser); when the game is
active it streams its whole log pump (`?trace=1` on the URL, or the `ff.webtrace` flag) ->
`POST /api/trace` -> Neon table `analytics_events` (`event_name='web_trace'`). Errors are caught
QUIETLY for the player (owner law: no giant JSON failure screen) and are loud ONLY in the db.
So the db IS the flight recorder. This skill reads it.

Pipeline law this follows: `docs/TICKET_PIPELINE.md` (QA -> CLI -> PO) + `CLAUDE.md` §12
(instrument, don't guess: no fix claim without a captured line) + §13 (classify NEW-FEATURE vs
EXISTING first).

---

## STEP 0 - get the admin key into the session

Every read below is authenticated by the header `X-Admin-Key`, which must match the Vercel env
var `ADMIN_DASH_KEY` (owner set it in the Vercel project env). The value is SENSITIVE and is
**never committed / never written into any file in this repo.**

- The owner pastes the key value into the chat when starting a triage session, OR
- It is already exported in the shell as `$ADMIN_DASH_KEY`.

If you do not have it, ask the owner for it in one line and stop. Do not proceed keyless (the
endpoint returns `400 Unauthorized` and you will misread that as "no data").

In the commands below, `$ADMIN_DASH_KEY` is a placeholder for the pasted value. Prod host is
`defenders-of-the-realm-v2.vercel.app`.

---

## STEP 1 - pull the trace data (read paths, portable-first)

Try them in this order. #1 works from a phone with nothing but the key; #2/#3 need tools a
phone session usually lacks.

### 1. PRIMARY - HTTPS GET the admin db endpoint (works anywhere with the key)

Backed by `api/admin/db.js` (GET only, read-only SELECTs with hard LIMITs). Three views matter:

**(a) 7-day error rollup - START HERE to see if/when something broke:**
```bash
curl -s -H "X-Admin-Key: $ADMIN_DASH_KEY" \
  "https://defenders-of-the-realm-v2.vercel.app/api/admin/db?view=metrics"
```
Returns `per_day`, `per_event_per_day`, and `trace_error_lines_per_day` (count of lines matching
exception/nullreference/softlock/error/fail per day). A spike on the day the owner played = your
lead.

**(b) List recent trace sessions - to pick which session to read:**
```bash
curl -s -H "X-Admin-Key: $ADMIN_DASH_KEY" \
  "https://defenders-of-the-realm-v2.vercel.app/api/admin/db?view=traces"
```
Returns the latest `web_trace` sessions (session id, build, batch count, total lines, latest
timestamp). Pick the session whose timestamp matches when the owner hit the bug (she will often
say "just now" / "a few minutes ago").

**(c) Read ONE session's actual lines - where the proving line lives:**
```bash
curl -s -H "X-Admin-Key: $ADMIN_DASH_KEY" \
  "https://defenders-of-the-realm-v2.vercel.app/api/admin/db?view=traces&session=<SESSION_ID>"
```
Returns each batch's `lines` array verbatim - the FlowTrace steps + Unity errors/exceptions from
that play session. This is the captured data you RCA from.

Optional: `view=overview` (per-table row counts + newest timestamp) to confirm the sink is live,
`view=players` / `view=players&player=<id>` for save-state questions. Add `&limit=N` to traces
(default 20, max 50) or metrics.

Tip for phones: pipe into `jq` if present (`| jq '.rows[].lines[]'` to print lines flat). If no
`jq`, the raw JSON is still readable - scan for `error:` / `Exception` / `[sig]` substrings.

### 2. FALLBACK - Vercel MCP runtime logs (if the Vercel MCP is connected)

`api/trace.js` also ECHOES every signal line to the Vercel runtime logs prefixed `  [sig] `
(the echo path, so traces are readable without the sensitive `DATABASE_URL`). If the Vercel MCP
tools are available, call `get_runtime_logs` for the prod deployment and filter to lines
containing `[sig]` or `error`. Same data, different pipe.

### 3. FALLBACK - vercel CLI (only if it is installed and logged in)

```bash
vercel logs <deployment-url-or-id> | grep -E "\[sig\]|error|Exception"
```
Grabs the same `[sig]` echo. A phone session almost never has this - prefer #1.

---

## STEP 2 - triage the pulled data (the QA half)

Given the owner's plain-words symptom + the lines you pulled:

**(a) Find + quote the proving line(s).** Scan the pulled lines for the error/`[sig]`/exception
that matches the symptom. Quote it VERBATIM with its source (session id + timestamp). This is the
"RCA proof SHOWN by data" rule - no narrative-only RCA (`docs/TICKET_PIPELINE.md` rule 0). If no
line matches the symptom, say so plainly: either the trace flag was off that session, or it is a
render/feel issue the db cannot see (those need the owner's F8 / eyes, not this skill).

**(b) Classify NEW-FEATURE vs EXISTING (mandatory gate, §13).**
- **EXISTING** (was built, now broken) -> continue to RCA below and write a fix WO.
- **NEW-FEATURE** (never built) -> this is NOT a bug fix. Write the WO as a spec routed to the
  dev silo and say so; do not fabricate an RCA for something that never existed.

**(c) RCA (for EXISTING).** From the captured line, name the most likely root and the file(s)
that own it. Static reading LOCATES candidates; the captured line is what PROVES the class of
failure. Where you cannot fully prove root without the Windows machine, list the candidate(s) and
mark the exact proving step the CLI must run (§12 - candidates only until instrumented).

**(d) Name the silo / lane.** Map to a `CLI_LANES_WO_NUMBERS.md` lane (e.g. Lane 9 VFX/Audio,
Lane 10 Build/Deploy/Perf, Lane 2 Combat/AI, Lane 4 UI/HUD, Lane 5 World). This tells the next
machine session where it slots.

---

## STEP 3 - write the Work Order (the deliverable)

1. **Mint the number.** Open `CLI_LANES_WO_NUMBERS.md`, read the banner's **next free WO**
   (read it OFF THE BANNER - never restate it here; a copied number is exactly what goes stale), and **bump it in the SAME edit** (change the banner's "next free" value to the next
   number and note what you minted, per the file's own rule). Never mint from filesystem max.

2. **Write `WorkOrders/WORK_ORDER_NNN_short_name.md`** in the house format (match WO-678 / WO-682
   as templates). Required sections:
   - Title line + **Status: READY TO IMPLEMENT** + Lane + **Type: EXISTING** (or NEW-FEATURE).
   - **Symptom** - the owner's plain-words report + when/where (web, mobile/desktop, date).
   - **RCA - proven from the db** - the VERBATIM proving line(s) with source (session id +
     timestamp), then one sentence each on what they prove. This section is non-negotiable.
   - **Root candidates** (if root not fully proven) - static candidates, each with the exact
     proving step the CLI must run before editing (§12).
   - **The fix** - bounded steps, files to edit.
   - **Acceptance** - checkboxes incl. a proving-line-quoted RESULT + `COMPILE_GATE_OK` + a
     preview WebGL build for the owner's mobile felt-pass (PO closes).
   - **What NOT to touch** - guardrails (e.g. production stays untouched; the working timeout
     guards stay).

3. **Notion mirror (if Notion tools are available in this session).** Also add a row to the
   "Work Orders" DB (data source `5f66b263-c732-4075-b94a-f5f4de9f8087`) so the live board stays
   in sync (`NOTION_SOURCE_OF_TRUTH.md`). If Notion tools are NOT available (typical phone
   session), note in your reply "Notion row pending - add on next tooled session."

4. **Do NOT commit.** Writing the WO file is enough; the sole committer (CLI on Windows)
   reconciles it into the tree by explicit path (`CLAUDE.md` §11).

---

## THE UNITY LIMIT (state this plainly in your handoff)

A phone / cloud / async session CAN: pull the trace, find the proving line, classify, RCA, and
write the WO. It CANNOT: fix `.cs` code, run `CompileGate` (brace/NUL gate), run the AutoPilot
fleet, or build the WebGL/Windows player - all of that needs the **Windows Unity machine** (Unity
editor closed for batchmode; §0 mount-sync rule; §1 brace gate). So the WO is left **READY** for
the next machine session (or the owner runs the local build when back at the PC). Say this in the
reply so the owner knows exactly what remains and where it happens.

---

## WORKED EXAMPLE (the real WO-682 case)

**Owner (on her phone):** "the sword swings have no sound and it stutters."

1. **Pull.** `view=metrics` -> a bump in `trace_error_lines_per_day` for today. `view=traces`
   -> pick the recent session. `view=traces&session=wt-b085deef5b6` -> read the lines.

2. **Proving line (verbatim, with source):**
   ```
   [Main_Castle_Overworld] error: Loading FSB failed for audio clip "SwordSwing".
   ```
   session `wt-b085deef5b6`, 22:48:27 UTC; same batch carried `[Flow:Perf] LOW fps=6 ms=167.9`,
   and a later session `wt-370cb605d41` (23:05:04) carried `[Flow:Perf] LOW fps=0 ms=4000.0`
   (a 4s stall). The error repeats across sessions and pairs with the stutter the owner felt.

3. **Classify:** EXISTING - the audio + capture pipe were built; a clip decode fails on WebGL.
   Not a new feature.

4. **RCA:** the FSB (audio bank) decode for `SwordSwing` fails under WebGL -> no swing SFX (the
   "no sound") and the failed load / error surface stalls the frame (the "stutter"). Candidate
   owners: the audio import settings for that clip + the WebGL build's error-surface (a
   Development WebGL build paints the full-screen overlay - the demo-killer). Silo: Lane 9
   VFX/Audio + Lane 10 Build/Deploy/Perf.

5. **WO:** mint the next number, bump the banner, write
   `WorkOrders/WORK_ORDER_NNN_web_quiet_error_surface.md` with Symptom / RCA-with-the-line-above /
   fix / acceptance / what-not-to-touch. Left READY for the Windows machine. (This is exactly how
   WO-682 was created.)

---

## Reference
- `KEY_FACTS.md` (Backend/web + Process) - the web-debug read path, db-viewer, WO numbering.
- `api/trace.js` (the sink + `[sig]` echo) / `api/admin/db.js` (the read endpoint, views).
- `docs/TICKET_PIPELINE.md` (QA->CLI->PO; rule 0 = proof shown by data).
- `CLAUDE.md` §12 (instrument-don't-guess) / §13 (ticket pipeline) / §11 (sole committer).
- `CLI_LANES_WO_NUMBERS.md` (WO numbering banner - mint + bump here).
- `tools/db-viewer/index.html` - the owner's double-click GUI over the same endpoint.
