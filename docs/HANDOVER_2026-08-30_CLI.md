# HANDOVER — 2026-08-30 CLI session

**Branch:** `wip/village2-and-f8-tickets` · **Device build on the Seeker:** `2026.08.30.348233`

---

## 1. START HERE — the process, in order

1. **Read `SESSION_STATE_2026-08-30_CLI.md`** (repo root). It records the in-flight agents,
   the owner rulings, and my own corrected errors.
2. **`git status`** — the tree is DIRTY (~420 files) and most of it is NOT mine. Other seats
   have uncommitted code for the card-collections program (`api/catalog/`, `api/showcase/`,
   `api/entitlements.js`, the 11 new regression suites), plus `Assets/EnemyContent/*.meta`,
   `Assets/Scenes/Village2.unity` and the Canonical JSON mirrors. **Their WORK ORDERS are
   committed; their CODE is not.** Do not blind-add.
3. **Gate before trusting anything in the tree:**
   `powershell -File .un-unity-method.ps1 -Method DeNelle.Editor.CompileGate.Run -LogName compile-gate.log -ExpectMarker COMPILE_GATE_OK -TimeoutMin 45`
   then `DeNelle.Editor.DataRegression.RunAll` with `-ExpectMarker REGRESSION_OK`.
   **Judge by the MARKER on a FRESH log, never the exit code** — the runners exit 0 on refusals.
   Expect `329/330`; the one red is `BUILD_COLLECTION_PLAYER_FAIL`, a known ORACLE bug (a source
   grep asserting `||` against shipped `&&`), not a product defect.
4. **Commit BY EXPLICIT PATH.** I broke this once today (commit `459b8edd` swept in ~30 files of
   another seat's work) and disclosed it in the message. Do not repeat it.
5. **Build/ship only through the sanctioned scripts** (§16): `overnight-apk-build.ps1` →
   `tools/r2-ship.ps1` → `install-apk-to-seeker.ps1 -Build:$false`. Never raw `adb install`.
6. **Push** — the `pre-push` hook checks the Unity pin, schema parity, and that
   `Builds/r2-parity.log` POSTDATES everything under `ServerData/`. If it blocks, the fix is to
   run the sanctioned chain again, never a flag.

---

## 2. WHAT LANDED TODAY (all gated, all pushed)

| Area | Outcome |
|---|---|
| **Hero death shake** | ROOT-CAUSED and fixed. Owner confirmed working on device. |
| **Harvest node on its side** | Fixed — Food was missing from `authoredPose`. |
| **Weapon mesh readability** | 400 of 417 gear meshes had Read/Write OFF; all fixed, guarded, `.meta` tracked. |
| **Dead Solana tower-swap** | Deleted (owner ruling PIN-3). |
| **`DeNelle.Commerce` extraction** | `DeNelle.Village` no longer references `DeNelle.Wallet`. |
| **MWA plugin Play exclusion** | Works; Seeker APK PROVEN to still carry `solana-wallet` + `mwa/` classes. |
| **Play billing settlement** | `ConfigureSettlement` had NO CALLER — the store could take money and grant nothing. Now composed, fail-closed. |
| **Google identity server rail** | `/api/auth/google-session` committed, DORMANT, Seeker byte-identical. |
| **Command Center** | Phone-first, audit-first. IMPLEMENTED, not DONE — needs a phone-width capture. |

### The two most useful lessons from today
- **The death shake survived three fixes because they all pinned the ROOT TRANSFORM.** The root
  was never what moved — two death clips were crossfading into each other ~10x/sec via an
  unconditioned AnyState fallback. Proven by parsing the shipped `.controller`.
- **Every proof that failed us was editor-side.** PROD-019 and the shield were closed on an
  Inspector row and a source-grep regression. The three things that actually broke problems open
  today were all DEVICE captures. Prefer a device capture over any editor-green.

---

## 3. THE FOUR AGENTS THAT WERE RUNNING (their edits may be in the tree, UNGATED)

1. **Play readiness SME audit** — read-only. Owner directive: audit everything blocking a Play
   release, then systematically close it out.
2. **UniTask migration scoping** — read-only. Asked the question that may reframe the AAB problem:
   removing `com.solana.unity_sdk` removes it from the **Seeker** build too. "Remove the package"
   may be the wrong frame; alternatives are a pre-build manifest swap, a separate Play project, or
   changing the gate's expectations.
3. **Battle quiescence softlock** — edits. Arena WIN leaves `timeScale` at **0.04** and battle-lock
   HELD by `PursuitBattleProbe.Probe` + `BattleArena.<Awake>b__84_0`. Player-facing softlock,
   REGRESSION of the 2026-08-20 leaked hit-stop. Brief says the failure must SELF-HEAL, not just log.
4. **Two orphaned screens** — edits. **No path to the SKILL TREE** (owner: "a huge issue") and no
   path to the **defensive/building upgrade screen**. Hypothesis to prove: CLAUDE.md §7's re-point
   of the `Upgrade` bar face to the unified Manage screen orphaned both. Door-level regressions
   required, modelled on `ManageTroopsTrainDoorRegression`.

**If their edits are in the tree, gate them before committing. If the tree is clean, that work is lost and needs re-running.**

---

## 4. GOOGLE PLAY — where it actually stands

`PLAY_SOURCE_ISOLATION_OK` (all four conditions, from four failures this morning) but
**`PLAY_ARTIFACT_DIRTY`. DO NOT UPLOAD THE AAB.**

| Blocker | Status |
|---|---|
| `com.solana.unity_sdk` UPM package | compiles Solana into `global-metadata.dat`/`libil2cpp.so`; blocked on UniTask |
| Two unsatisfiable gate tokens | `phantom` matches `java.lang.ref.PhantomReference`; `mwa/` matches base64 in ad-SDK strings. **The gate has NO passing state until these are made precise.** Duplicated in `tools/android/assert-google-play-aab-clean.ps1:18` — change both or they drift |
| No entitlement writer in a Play build | `PackStoreVM.ApplyPackContents` is in `DeNelle.Wallet`, which compiles OUT |
| Client identity | server rail done and dormant; nothing on the device calls it |
| Account deletion URL | already ticketed as **WO-1270** — do not re-mint |

---

## 5. OWNER DECISIONS OUTSTANDING

1. **The two gate tokens** — make them precise, or the gate stays permanently red and gets ignored.
2. **`ANALYTICS_EXCLUDED_PLAYER_IDS` is UNSET** — her own play counts as player retention in every
   Command Center figure.
3. **UniTask/AAB approach** — pending the scoping agent.
4. **Command Center felt-verify** — phone-width capture.

---

## 6. HANDS OFF

⛔ **The shield.** `Assets/Resources/OffsetForge/offsets.json` is at HEAD with the owner's
hand-dialled values (`pos -0.103, 0.164, -0.238` / `rot 1.915, -48.302, -127.941` / `scale 0.71`).
She dialled and validated them through movement cycles. **I overwrote them twice today and was
wrong both times.** Do not touch without an explicit request.

---

# APPENDIX - OPEN ITEMS AND EXPECTATIONS TO CLOSE (added 2026-08-30, end of session)

Every row states what DONE looks like. Nothing here is "have a look at it".

## A. IN FLIGHT WHEN THE SESSION ENDED

| What | State | Expectation to close |
|---|---|---|
| Play readiness SME audit | COMPLETE, recorded at `docs/GOOGLE_PLAY_READINESS_AUDIT_2026-08-30.md` | Work its Wave 0/1/2/3 order. Start #1. |
| UniTask scoping | COMPLETE, verdict in WO-1282 | Do option (d) embed+constrain, ~1 day. NOT the swap. |
| Battle quiescence | FIXED, gated, pushed | Owner plays an arena win then immediately starts another fight; no FAIL, a `BATTLE_QUIESCENCE_SUPERSEDED` line instead. |
| Skill tree door | FIXED, gated, pushed | Bag shows SEVEN tabs, Skills opens the talent tree, and a UI capture confirms labels are not squeezed. |
| Defensive upgrade | NOT A DEFECT - correctly gated | Owner places one tower/wall and sees Defense appear. Discoverability tracked as WO-1285. |

## B. BLOCKS A PLAY RELEASE - in priority order

| # | Item | Expectation to close |
|---|---|---|
| 1 | **AAB is 493 MiB vs Play 200 MB** | Split Application Binary -> PAD install-time pack; an AAB the Console ACCEPTS. Own lane, own headless verify. **If only one thing starts, start this.** |
| 3 | SKR skin forced on every Android build | `CurrencySkinResolver` no longer forces `skr` on a GOOGLE_PLAY build; no `$SKR` anywhere in a Play run. |
| 4 | Unconditional Wallet section in Settings | Absent from a Play build. |
| 2 | Artifact crypto-dirty | `PLAY_ARTIFACT_CLEAN_OK` on a fresh log - AFTER the token list is BOTH widened and narrowed in one edit. |
| 5 | Crypto-framed Privacy/Terms | Play-variant copy with no wallet/on-chain framing, linked from the Play build. |
| 9 | No in-app account deletion | An in-app route, shipped in the SAME release as sign-in. |

## C. BLOCKS MONETISATION - one ordered chain, not parallel lanes

`#8 identity` -> `#6 entitlement writer` -> `#7 store UI + restore` -> `#10 promo`.
Close = a licensed test purchase on Play completes and GRANTS, survives reinstall via restore,
and the ledger shows one settlement for one token.

## D. OWNER DECISIONS - engineering is blocked or wasted without these

| Decision | Why it matters | Expectation |
|---|---|---|
| **Closed testing: 12 testers x 14 CONTINUOUS days?** | Applies if the Play account is personal and post-13-Nov-2023. It is a TWO-WEEK CALENDAR dependency gating everything. | Check the Console TODAY. If it applies, start the clock the day an installable AAB exists. |
| **#11 package id / Play App Signing** | Play re-signs with a Google key on the SAME id as the live dApp-Store build - they become mutually un-installable. | Decide BEFORE the first Play install exists. Cheap now, expensive after. |
| Gate token list | Two tokens can never pass; the list is also blind to SKR/wallet/usdc/nft/stake. | Widen AND narrow in one edit, both files (s16 drift). |
| `ANALYTICS_EXCLUDED_PLAYER_IDS` | Unset - the owner own play counts as player retention. | Set it, or every Command Center retention figure is measuring her. |
| WO-1285 design questions | Empty-state card or placed-only; deep-link or list. | Answer, then implement. |
| Command Center felt-verify | IMPLEMENTED, not DONE. | Phone-width capture of the deployed page. |

## E. RECORDED, NOT FIXED - do not rediscover these

- `ClaimableCamp.cs:553` calls `VerifyNodeRenders` SYNCHRONOUSLY but the visual builds in
  `MineNodeVisual.Start()` - it can only ever see the PRE-BUILD state. That is why a node lying on
  its side passed a check whose whole job is verifying the node.
- WO-1260 excluded OS-suspended time from hold age, so a legitimately-open pause menu past 180s
  trips the watchdog and RESUMES THE WORLD UNDERNEATH AN OPEN MENU.
- `StakeRewardsFallbackData.g.cs` bakes `stake-rewards.json` into C# - moving the JSON achieves
  nothing. Any file-moving exclusion misses this class of leak.
- No `session_end` event, so Command Center average-online-time is an ESTIMATE and says so.
- `packs.store_visible` exists in Neon and NOTHING reads it - a push-SKU button would flip a value
  no player can see.
- `TownBankCapRegression.cs:335` hardcodes `PackStoreVM.cs` path and DEGRADES TO A SKIP if it
  moves - amend it in the same commit as any move.

## F. HANDS OFF

The shield. `offsets.json` is at HEAD with the owner hand-dialled, movement-cycle-validated values.
I overwrote them twice today and was wrong both times. Do not touch without an explicit request.
