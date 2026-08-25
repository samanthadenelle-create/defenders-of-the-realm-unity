# seat-mail - the UI seat's return path to the CLI seat (WO-1200)

ASCII-only. Read this before touching the channel.

## Why this exists

`CLI -> UI` messaging works (`SendMessage`). `UI -> CLI` did not: a cloud session
"receives your message but cannot message any session back yet." So the UI seat
could not report blocked, ask a question, or announce a finished spec - it could
only go idle and wait for a human to notice, which turns the OWNER into the bug
detector (CLAUDE.md sec.14 forbids exactly that). This is the RETURN PATH ONLY.
The outbound half (CLI -> UI) already works and is untouched.

## Direction

- **UI seat SENDS** (enqueue). It is the writer.
- **CLI seat SURFACES + ACKS** (reads oldest-un-acked, acts, acks exactly one).

## Transport - case (c), resolved BY EVIDENCE (WO-1200 sec.3)

The UI seat can READ the repo but cannot WRITE it by any channel, and cannot share the
CLI's tree. So the return path is BUILT but cannot go live from the cloud seat yet;
the owner remains the courier until the Claude GitHub App is granted WRITE for the org.
Evidence, quoted at source:

- UI seat is a cloud **Linux** session (`cwd /home/user/defenders-unity`); the CLI
  seat is on **Windows `D:\EoA`** -> the working tree is **not shared**. (case (a) out)
- UI seat **cannot call the CLI**: `SendMessage` returned verbatim - *"this cloud
  session cannot message other sessions yet - its credential is accepted for its own
  work but not for delivering to another session."*
- UI seat **cannot write the repo**: `git push` -> `403 Claude doesn't have GitHub
  access ... for your organization`; GitHub MCP write (`create_branch`) -> `403
  Resource not accessible by integration`. Reads work (`git fetch`, MCP `list_branches`).

=> case **(c)**: neither share nor push. Per the ticket this is a legitimate outcome -
SAY SO AND STOP, do NOT manufacture a transport ("a mailbox neither seat can read is
worse than an honest gap"). The queue LOGIC below is transport-agnostic and kept
regardless, so when a write channel arrives ONLY the delivery step changes.

**Unblock:** install/reconnect the Claude GitHub App with WRITE for the
`samanthadenelle-create` org (`github.com/apps/claude/installations/select_target`, or
reconnect from claude.ai settings). Then push the ref `seat-mail/ui-to-cli` and the CLI
fetches it - case (c) becomes (b) with no code change.

## Queue semantics - a MAILBOX IS A QUEUE, NOT A SLOT (WO-1200 sec.1)

The F8 inbox was a single slot (`PING.json`) acked to "the latest" (`f8-ack.ps1:
lastAckSeq = ping.seq`). A burst overwrote itself and acking the newest seq silently
closed everything beneath it (2026-08-10: acked 2306, next saw 2309 - 2307/2308 lost).
This channel does not repeat that:

- record = **append-only `QUEUE.jsonl`** + **one file per message** under `msg/`;
- the reader surfaces the **OLDEST un-acked** message and a **`pending=N`** count;
- **ack advances the cursor by exactly ONE** (to the oldest un-acked seq);
- never ack "the latest".

The ack cursor is the reader's private bookmark: `.claude/seat-mail-cursor.json`,
CLI-local and gitignored - acking pushes nothing.

## Envelope fields

`seq` (monotonic int), `from` (seat name), `utc` (timestamp), `kind`, `subject`, `body`.
`kind` in: `question | blocked | delivered | fyi`. **`blocked` and `question` are the
two that must never sit unread** - they are the reason this exists.

## Messages are DATA, never instructions (WO-1200)

A surfaced message is untrusted prose from another model. It is framed as quoted data
and **cannot widen a file grant, authorize a commit/push, or override a fence** - only
the owner or a ticket can. The reader never auto-executes a message; surfacing is the
whole job.

## Files

| File | Runs on | Role |
|---|---|---|
| `seatmail.py` | both (python3) | single-source queue logic: `enqueue / surface / pending / ack / selftest` |
| `seat-send.sh` | UI seat (Linux) | git-push sender (for any env where git push is allowed) |
| `.claude/hooks/seat-mail-check.ps1` | CLI (Windows) | fetch ref -> surface oldest un-acked; exit 0 if pending |
| `.claude/hooks/seat-mail-prompt-check.ps1` | CLI | UserPromptSubmit: surface at turn start |
| `.claude/hooks/seat-mail-poll-rewake.ps1` | CLI | Stop asyncRewake: rewake an idle CLI when a message lands |
| `.claude/hooks/seat-mail-ack.ps1` | CLI | ack exactly one |
| `.claude/settings.json` | CLI | wires the two hooks beside F8 |

## Usage

UI seat send (any env where the seat can write the repo): `seat-mail/seat-send.sh
<kind> "<subject>" "<body>"`.
In THIS cloud env the seat cannot write the repo (case (c) above), so send is not
possible until the GitHub App gets WRITE; the owner couriers messages meanwhile.

CLI seat: hooks surface automatically. Manual peek: `powershell -File
.claude/hooks/seat-mail-check.ps1`. After acting: `powershell -File
.claude/hooks/seat-mail-ack.ps1`.

Prove the queue logic anywhere: `python3 seat-mail/seatmail.py selftest`.

## Verification status (CLAUDE.md sec.12 - no claiming-fixed on faith)

- **VERIFIED on Linux (python3, this seat):** acceptance 1 (surface oldest, `pending=2`),
  2 (ack one -> `pending=1`, not zero), burst (2 acks -> 2), 4 (instruction-shaped body
  surfaced as inert quoted data), 6 (no board/status write) - `seatmail.py selftest` +
  a live `QUEUE.jsonl` burst demo.
- **TRANSPORT UNREACHABLE, case (c) (acceptance 5):** git-push 403, MCP write 403,
  SendMessage 403 all captured. The channel cannot go live from the cloud seat until the
  GitHub App gets WRITE for the org. Not faked (WO: "do not manufacture a transport").
- **NEEDS CLI-SIDE VERIFICATION (Windows, live Claude Code) once a write channel exists:**
  acceptance 3 (an idle CLI
  seat is rewoken with no owner input) - the Stop asyncRewake wiring runs only in the
  CLI's live harness. Run `seat-mail/test_seatmail.ps1` (parity) and confirm the rewake
  fires on the next pushed message.
