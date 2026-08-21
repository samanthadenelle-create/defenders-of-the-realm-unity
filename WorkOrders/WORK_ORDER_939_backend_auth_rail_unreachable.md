# WORK ORDER 939 — The backend auth rail is compiled OFF in every shipped build

**Status:** DONE — owner-confirmed 2026-08-21.
**Minted:** 2026-08-09 (number from the `CLI_LANES_WO_NUMBERS.md` banner; banner bumped 939 → 940 in the SAME edit as this mint)
**Lane:** Monetization/Backend (CLAUDE.md §9 — fully isolated; no gameplay, no scene, no UI)
**Owner ruling 2026-08-09:** **OVERNIGHT, NOT A HOTFIX.** Verbatim: *"c1 can be worked overnight"*,
*"as it doesnt impact us"* — there is no live player base, so the exposure is real but theoretical.
Do not rush this, and do not let it regress offline play to close it faster.
**Provenance:** repo-wide security audit, 2026-08-09. Every line below was re-verified at source by the
orchestrator before minting — this WO asserts nothing it did not open.

---

## 1. The finding (all four proving lines verified 2026-08-09)

The project has a correct, server-side-sound wallet auth rail. **The client never uses it.**

| # | Proving line | Verified |
|---|---|---|
| 1 | `BACKEND_AUTH_ENFORCED` appears on **no platform row** in `ProjectSettings/ProjectSettings.asset` | `grep` returns EMPTY across the whole file |
| 2 | `Assets/_Modules/Core/State/BackendAuthConfig.cs:58` — `#if BACKEND_AUTH_ENFORCED` | the enforced branch is therefore **compiled out of every build** |
| 3 | `Assets/_Modules/Core/State/GameStateService.cs:1469` | no auth headers are attached to the save request |
| 4 | `Assets/_Modules/Core/State/GameStateService.cs:1572-1574` — `private const string GuestIdSalt = "dotr-guest-id:v1:9f3c7a";` then `Sha256Hex(deviceId + GuestIdSalt)` | the salt is a **literal in the shipped binary** |

**The consequence, in order:** because (1) and (2) hold, wallet-authenticated saving cannot work in a
shipped build — a wallet player would be 401'd — so real cloud saves fall through to the **guest rail**.
The guest rail's identity is `Sha256(deviceId + GuestIdSalt)`. Both inputs are recoverable: the salt is
in the APK, and `deviceId` is not a secret. **Anyone who derives another player's guest id can read AND
overwrite that player's entire save.**

This is a client-side gap, not a server-side one. Do not "fix" the backend; it is already correct.

---

## 2. Instrument FIRST — this is a §12 hard gate

**No code edit until captured data proves the request shape.** The whole finding is about what the
client does or does not put on the wire, so a trace settles it in one read and a code-read cannot.

1. Add `FlowTrace.Step("BackendAuth", ...)` around the save request build in `GameStateService`:
   which rail was chosen (wallet vs guest), whether auth headers were attached, and the response code.
2. Run headless and capture. Confirm from the trace — not from reading `#if` — that a build with no
   define takes the guest rail and sends no headers.
3. Only then change anything. Attach the captured lines to the RESULT file.

---

## 3. Acceptance criteria

- [ ] A `[Flow:BackendAuth]` trace exists showing rail choice + header presence + response code, and the
      "before" capture is quoted in the RESULT.
- [ ] `BACKEND_AUTH_ENFORCED` is defined for the **shipping platform rows** in `ProjectSettings.asset`
      (Android and Win64 at minimum), and a headless run proves `BackendAuthConfig`'s enforced branch is
      now live.
- [ ] A wallet-bound save **round-trips** against the real endpoint with auth headers attached —
      captured request + 2xx response in the RESULT. **This is the criterion that actually matters;**
      flipping the define without proving the round-trip just converts silent-insecure into
      silent-broken.
- [ ] **Offline / no-wallet play still saves.** `BackendAuthConfig` is documented as "defaults OFF
      (offline-safe)" and that property must survive. A player with no wallet must not lose local saving.
      Prove it headlessly.
- [ ] The guest rail no longer derives identity from a binary-resident secret. Preferred: the **server**
      issues/derives the guest identity and the client stores the returned handle. Acceptable interim:
      guest saves are accepted only for genuinely anonymous local play and are never treated as
      authoritative cloud state for a wallet-bound account.
- [ ] A regression pins the gap so it cannot silently reopen: assert that the shipping platform rows
      carry `BACKEND_AUTH_ENFORCED`. A source/settings lint is adequate here and mirrors the existing
      `WalletProviderSelectionRegression` pattern.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` — read the counts off the markers, never
      restate them. **Never trust the exit code, and never the marker alone: grep the log for
      `error CS` too** (`SUNDAY_HOUSEKEEPING.md` §3).

---

## 4. What NOT to touch

- **The serverless handlers under `api/`.** The audit found the server-side crypto correct; this is a
  client gap. Changing both ends at once makes a failed round-trip undiagnosable.
- Save schema / `SaveMigrator`. This WO changes **transport auth**, not persisted shape. No version bump.
- The real-money purchase gate (`ff.realmstorepurchase`) and anything under `PackStore` / `WalletService`
  payment paths — separate findings, separate lane, and re-gating them OFF was a deliberate owner ruling
  (commit 576601e3).
- Any UI. No scene files.

---

## 5. Sequencing note for the overnight seat

Flip the define **last**, after the round-trip is proven against the live endpoint. The failure mode to
avoid is enabling enforcement, discovering the endpoint rejects the client's header format, and leaving
the tree in a state where **nothing** saves. If the round-trip cannot be proven overnight, stop at the
instrumentation + the regression, write down exactly which request the server rejected, and leave the
define OFF — a documented, still-insecure state is strictly better than a silently broken save path.

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `no BACKEND_AUTH_ENFORCED; GameStateService.cs:1647 salt literal` — auth rail compiled out. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **OWNER RULING 2026-08-21 (verbal, this session):** Owner: 939 is done. ⚠ The 2026-08-21 audit read this as OPEN - STILL VALID (evidence above). Owner review supersedes it; the audit line is kept so the evidence survives a reopen.
