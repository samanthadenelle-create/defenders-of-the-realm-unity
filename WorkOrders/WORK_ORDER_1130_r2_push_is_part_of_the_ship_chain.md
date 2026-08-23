# WORK ORDER 1130 — The R2 push is part of the ship chain, not a step a human remembers

**Status:** FIXED — AWAITING OWNER FELT-TEST TO CLOSE. Prior status: DONE — implemented in the working tree 2026-08-20 (`tools/r2-ship.ps1` new; `morning-ship-chain.ps1`, `overnight-apk-build.ps1`, `install-apk-to-seeker.ps1` rewired to call it). ⚠ NOT YET COMMITTED at the time this WO was minted — the sole committer stages it; and ⚠ ONE BYPASS REMAINS OPEN (§5).
**Minted:** 2026-08-20 (CLI seat, main line — banner bumped 1130 → 1131 in this SAME edit)
**Silo:** Build/ship tooling (`tools/`, root `*.ps1`) — touches no game code, no scene, no catalog
**Priority:** HIGH — this failure costs whole owner play sessions and is invisible on device
**Provenance:** owner, 2026-08-20, after playing a build in which every enemy rendered as a tinted capsule: *"wire the r2 push into the ship chain."*
**Class:** process defect made permanent in tooling. Not a code bug — nothing in `Assets/` is wrong.

---

## 1. The failure this exists to make impossible

Enemy and structure ART is served **remotely from R2**. It is **not in the APK**, and there is **no
local fallback** — the CDN migration deleted `Assets/Resources/Enemies` and
`Assets/Resources/Structures` (the same fact PROD-012 is built on). So an APK whose bundles were
never uploaded:

- installs perfectly,
- launches perfectly,
- and shows **tinted capsules** where the enemies should be and placeholders where the buildings
  should be — **with no error on screen.**

⚠ **Bundle names are CONTENT-HASHED.** Every content build produces new filenames, so **every build
needs its own push**. A push from a previous build can never cover this one. That single sentence is
the whole trap: the bucket looks full, the previous build works, and the new one is broken.

## 2. Incident history — this is the THIRD occurrence

| Date | What shipped | How it was caught |
|---|---|---|
| **2026-08-18** | an APK sat ready to install whose enemy bundle had never been uploaded | **by hand.** Commit `16e22dba3` conceded in its own body: *"NO GATE COULD HAVE CAUGHT THIS."* That admission is what minted **PROD-011**. |
| **2026-08-19** | a real Android APK carrying `StandaloneWindows64` content, every other marker green | **WO-1124** (`11d166fe9`) — the wrong-target half. |
| **2026-08-20** | the owner played a build where **every enemy was a capsule** | the **device** named it in one line, after two wrong causes had been proposed (a duplicated `[BuildTarget]` token, then a stale content build): `RemoteProviderException : Unable to load asset bundle from : https://pub-….r2.dev/Android/enemy_art_assets_enemyfam-hollow_….bundle` / `UnityWebRequest result : ProtocolError : HTTP/1.1 404 Not Found`. |

**Root cause on 08-20 was NOT code.** Re-running the Addressables grouper and re-packing enemy
content re-hashed every bundle; the new bundles were never pushed. Fixed by
`python tools/r2_sync.py --push ServerData` then `--verify-catalog ServerData/Android` →
**`R2_PARITY_OK 42 object(s) verified`**.

> ### The lesson, stated once
> PROD-011 already built the gate. The gate **worked** — it verified, failed, and printed
> `FIX: python tools\r2_sync.py --push ServerData` for a human to run. On 08-20 that second command
> is exactly the one that got skipped. **A gate whose remedy is "someone remembers to run another
> command" is not a gate.** The remedy has to be inside the chain.

## 3. What was implemented

### 3.1 `tools/r2-ship.ps1` — new, and the ONE place the rules live

`push → verify → judge by the MARKER`, with three switches:

| Invocation | Behaviour |
|---|---|
| `powershell -File tools\r2-ship.ps1` | push + verify, **BLOCKS** on failure (exit 16) |
| `… -WarnOnly` | push + verify, warns and continues (exit 0) |
| `… -VerifyOnly` | prove only, upload nothing |

It carries, in exactly one file, the three rules that had been getting re-learned:

- ⛔ **Push the PARENT, always.** `--push ServerData/Android` **flattens** the keys to the bucket
  root where the game never looks, and reports `R2_PUSH_OK` while uploading 103 objects nobody can
  read — observed 2026-08-20. Verify, however, needs the **explicit** target
  (`--verify-catalog ServerData/Android`) because `ServerData/` holds both `Android` and
  `StandaloneWindows64` and the tool refuses to guess (the WO-1124 / PROD-011 edge). The asymmetry
  is real, which is why it is hard-coded exactly once.
- ⛔ **Judge by the MARKER on a FRESH log, never the exit code** — this repo's runners exit 0 on
  refusals and FAILs (memory `gates-report-success-without-proving-it`). The script deletes
  `Builds/r2-parity.log` before verifying so a stale log can never read as a pass.
- A push that *throws* is not fatal on its own; **the verify is the authority** on whether the
  bucket holds what this build needs.

### 3.2 The three callers, rewired

- **`morning-ship-chain.ps1`** step `2b/4` — was verify-only plus a printed FIX line for a human.
  Now calls `tools\r2-ship.ps1` and `Die`s on non-zero. **Blocks distribution.**
- **`overnight-apk-build.ps1`** — had its OWN copy of push+verify. Delegates now.
- **`install-apk-to-seeker.ps1`** — was verify-only/warn. Now **pushes**, with `-WarnOnly`
  **deliberately and only here**: sideloading a knowingly-offline or experimental build from this
  script is legitimate, so a mismatch must not block the install — but the owner must never be
  surprised by it.

### 3.3 Why it had to become ONE file

The push+verify pair was **copy-pasted** into `overnight-apk-build.ps1` and
`morning-ship-chain.ps1`, and the two copies **had already drifted**: overnight pushed *then*
verified; morning **only verified** and told a human to go push. Same fact in three files is the
duplicated-state class that already cost this project a WO number block (CLAUDE.md §2) and a
dependency table (§5); here it costs play sessions.

## 4. Acceptance

- [x] `tools/r2-ship.ps1` exists and is the only place the `--push` / `--verify-catalog` arguments are spelled.
- [x] All three ship scripts call it; no script carries its own copy of the pair.
- [x] The distribution chains **block** (`Die … 16`); only the sideload path warns.
- [x] Stale-log defence: the parity log is removed before each verify.
- [ ] **Not yet proven end-to-end on a real chain run** — the scripts were rewired after the
      08-20 manual recovery, so the next `morning-ship-chain.ps1` run is the first real exercise.
      Judge it by `R2_PARITY_OK <n> object(s) verified` in `Builds/r2-parity.log`, never by exit code.
- [ ] Commit (sole committer).

## 5. ⚠ WHAT REMAINS OPEN — the raw `adb install` bypass

**A human who runs `adb install -r <apk>` directly touches none of these three scripts and is
therefore still ungated.** That path is real: it is in the operator's muscle memory and in the
`adb`-path note (the Unity Hub `platform-tools` full path). Nothing in this change can see it.

Options — none implemented, none ruled on:

1. Accept it, and write it down as the known hole (this section is that record).
2. Make the APK itself refuse to boot against a bucket that does not hold its catalog's objects —
   i.e. move the check **into the client** as a first-run content probe. That overlaps PROD-010's
   first-run content signal and PROD-012's is-internet-required decision, and must not be invented
   here.
3. A build stamp recording the last-pushed catalog hash next to the APK, so installing an unpushed
   build is at least **loud** on the console.

**Also still true and NOT solved by this WO:** the failure is silent **to the player**. This change
makes it impossible for *us* to ship it; it does not make it impossible for a player to experience
if the bucket is ever emptied or the CDN is unreachable. That half is PROD-010 / PROD-012 territory.

## 6. What NOT to touch

`tools/r2_sync.py` itself (PROD-011 owns it — including the docstring at `:21` that still teaches
the wrong `--push ServerData/Android` form) · the Addressables group assets · any `Assets/` code ·
the `m_Timeout: 0` decision (PROD-011 §3 ruled it finished, not missing).
