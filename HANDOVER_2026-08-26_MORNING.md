# Morning handover - 2026-08-26

**Branch:** `wip/village2-and-f8-tickets`, everything committed and pushed.
**Last commit at handover:** `0082dcc99` plus the release-train commits below it.
**Gates, marker-asserted on fresh logs:** `COMPILE_GATE_OK` + `REGRESSION_OK 285/285 suites`.

---

## 1. WHAT NEEDS YOU - nothing else is blocked on anything but these

### a. The Mill is a design question, not a wording one
`structures-catalog.json:720` still reads *"Grinds grain into Food."* Food is retired.
**What does a gristmill produce now?** Until that is answered the string cannot be fixed, because
every candidate answer invents game design.

### b. Three prose strings are yours
Two `guide-content.json` tips explain food as a concept ("the wheat and apples the Heart wills into
being") and one `quests.json` objective is written around it. A noun swap does not survive them.
They are tracked as a DATED, RATCHETED baseline in `RetiredVocabularyRegression` - new leaks still
FAIL, and the suite's success string NAMES these three every run, so green never reads as clean.
Author the copy, delete the rows, and the suite proves itself.

### c. WO-1082 palette - 4a or 4b
Open since yesterday's RCA. Recommendation stands: **4a, storage containers first** - it matches the
evidence and does not reverse WO-963.

### d. `MAINNET_SALES_ENABLED` is still unproven
Your wallet is waved through BEFORE that switch is consulted, so your own store working proves
nothing about a stranger's. Only a price-list call from a wallet that is not yours settles it, and it
cannot be done from your device.

### e. PROD-016 art question (small)
`Resources/Harvest/stone.fbx` exists and is now wired. Nothing owed unless you want different art.

---

## 2. THE RELEASE TRAIN

| step | state |
|---|---|
| **Windows exe** | ✅ BUILT - `Builds/Windows/`, 1.9 GB with a 1.8 GB data payload (the "0.6 MB" in the success line is the launcher stub only - verified separately) |
| **Seeker APK** | ✅ BUILT + INSTALLED - `2026.08.26.341419`, three-way stamp match (APK / catalog / device), `R2_PUSH_OK` + `R2_PARITY_OK 43` |
| **Firebase distribution** | ⛔ BLOCKED, CORRECTLY - `distribute-android.ps1` requires `SCHEMA_PARITY_OK`, which requires `DATABASE_URL`. `.env.local` holds the literal placeholder `"[SENSITIVE]"`, not a connection string. **Run it from your credentialed shell:** `.\distribute-android.ps1 -Groups testers -Notes "..."` (add `-Build` only if you want a fresh APK; the current one is already built and installed). ⛔ Do not bypass the gate - it exists so a build cannot be distributed against an unverified database. |
| **WebGL -> Vercel** | RUNNING when this was written. Detached; markers land in `Builds/overnight-chain-status.txt` and the URL in `Builds/vercel-deploy.txt`. **PREVIEW only, never `--prod`** - promotion stays yours. |

### The dApp Store submission is ready whenever you are
Four pre-ship markers all green on fresh logs: `COMPILE_GATE_OK`, `REGRESSION_OK 285/285`,
`UI_CAPTURE_OK 89` / `FIDELITY_OK 65` (PNGs opened), `R2_PARITY_OK` matching the installed APK's
catalog. Release notes drafted in `publishing/config.yaml`; both 08-22 blockers formally cleared with
on-chain proof in `publishing/SUBMISSION_BLOCKERS_CLEARED_2026-08-25.md`.
It is an UPDATE to the existing listing, run with your wallet - no agent can sign it.

---

## 3. WHAT LANDED OVERNIGHT

**WO-1206 - the retired-vocabulary detector.** Its FIRST run found **twelve** player-visible Food
leaks, not the two found by hand: the tutorial guide, a quest objective, the end-of-battle spoils
row, the Echo affinity label, the "+N" harvest pop, and the world node's own model route. **Eight
fixed at source**, four are the prose above. The retirement list is DATA, dual-copy and versioned;
the detector scopes itself by SYNTAX so frozen persistence vocabulary is never flagged.

**Also landed:** WO-1159 (treasury verification now wired into the ship command, always `--multisig`;
proven-wrong config BLOCKS, RPC failure warns and needs explicit acknowledgement) · WO-1100 (net -47
lines, removing a false debt baseline) · R3 (`WallSegment` delegated to one toughness authority: +3/-5
in production, +24 in the oracle) · WO-935 Phase 0 inventory · WO-1209 Phase A instrumentation ·
WO-1208 collector lifecycle, with its RESULT filed.

**One oracle re-pointed, not weakened:** `EchoResourcePickerRegression` asserted the literal "Food";
it now DERIVES the label from `EchoRosterCatalog.TargetLabel`, keeping full strength.

---

## 4. BOUNCED, WITH REASONS - do not re-hand these as-is

- **WO-1211** (sign-on-every-launch) - REJECTED AT THE GATE: it removed the prompt by WEAKENING
  fail-closed auth (`[real-wallet-gate]` + guests bypassing shared proof). Backed out, not committed.
  Its SHAPE was right (+11/-158 deleting the second signing authority) and its oracle is preserved at
  `WorkOrders/preserved/` so the rework starts from good work. **Still the highest-value ticket open.**
- **WO-823 Phase E** - needs `Core/State` files the WO-1211 rework owns. Sequence:
  **WO-1211 rework -> WO-1212 -> WO-823.**
- **PROD-014(c)** - needs its fence widened to `HubRepairAffordance.cs`. Your multi-shortfall ruling
  is already recorded verbatim on the ticket, so it no longer needs a decision, only a fence.

---

## 5. NEW TICKETS FROM LAST NIGHT

**WO-1212 (P0 precondition)** - there are TWO Stone balances and only one is the player's.
`Resources.Food` is what the HUD shows and every cost spends; `GameState.Stone` is a second persisted,
server-guarded balance seeded 20 at new game, displayed nowhere and spent by nothing. RULED: retire
`GameState.Stone` and **DISCARD** its value - do not migrate or sum, because it holds only a seed and
dev top-ups, and summing would hand every save a free +20. Keep `stone` as an inbound wire alias.

**Owed a ticket (not yet minted):** `VFXCatalog.asset` carries rows at ordinals **94 and 95** while
`VFXType` ends at **93**. Harmless today, dangerous tomorrow: the enum serialises BY ORDINAL and
appends only, so the next VFX type added silently inherits an orphaned row's prefab and loop flag.

---

## 6. TWO ERRORS OF MINE, RECORDED

1. **I overwrote a tracked asset.** You said a `stone.fbx` existed; I said twice that it did not,
   because my search truncated at ten results. I then copied the KayKit one over the real one.
   `git checkout` restored it byte-exact and nothing was lost - but I asserted absence from a search
   I had not verified was complete.
2. **My own WO-1211 ticket told the implementer to PERSIST the bearer token.** That was wrong -
   in-memory-only is a deliberate security decision. The dev seat raised the conflict instead of
   obeying it; the paragraph is now marked superseded.

Both are the same shape as the bugs hunted all night: a confident statement that outlived its truth.
