> **SUPERSEDED 2026-09-06 by `CANON_GROUND_TRUTH_2026-09-06.md`.** Frozen, not rewritten (CLAUDE.md section 15). Read the newer anchor for current state.

# CANON GROUND TRUTH - 2026-09-03 (the production-candidate build)

**This supersedes `CANON_GROUND_TRUTH_2026-09-02.md`.** Keep exactly ONE current; supersede by date
(CLAUDE.md §15). Every session and every agent checks docs against THIS file.

> ⛔ **EVERY CLAIM ON THIS PAGE IS SOURCED, per CLAUDE.md §11B** (added today, commit `f1104a5fd`:
> *"we never Guess, always must be proven to be true"*). Where a claim could not be proven from this
> repo, the page says so in those words rather than tidying it into a fact. Read the source, never
> the summary.

---

## 1. THE BUILD - `2026.09.04.354315` IS THE PRODUCTION CANDIDATE

**It is installed on her Seeker and it is the build the store submission is about.**

| Fact | Value | Where it was read |
|---|---|---|
| `bundleVersion` | `2026.09.04.354315` | `ProjectSettings/ProjectSettings.asset:148` |
| `AndroidBundleVersionCode` | `354315` | `ProjectSettings/ProjectSettings.asset:177` |
| Branch | `feat/synty-art-retheme`, pushed | `git status` / `git log` - never a doc |
| HEAD at write time | `0a15744c9` | `git rev-parse --short HEAD` |
| Live store release it updates | `2026.08.17.328845` | commit `0a15744c9` body; `publishing/SUBMIT_CHECKLIST.md:41` |
| Commits between live and this | 756, ~230 player-facing | commit `0a15744c9` body |

**On-device confirmation:** the lead reports `adb shell dumpsys` returned versionCode `354315`,
lastUpdateTime `2026-09-03 20:21:27`. ⚠ That dumpsys output is **not captured in the repo** - it is
reported by the seat that ran it. The corroborating in-repo evidence that it is the same build:
`Builds/r2-push.log` (20:21) names `Android/catalog_2026.09.04.354315.bin`, i.e. the push that
accompanied the install carries **this** version's catalog.

### The four gates, read off MARKERS on FRESH logs (§8 - never an exit code)

| Marker | Log | Log mtime |
|---|---|---|
| `COMPILE_GATE_OK :: scripts compiled clean` | `Builds/compile-gate.log` | 2026-09-03 20:10 |
| `REGRESSION_OK 358/358 suites -- 358 green, 0 red, 0 skipped` | `Builds/regression.log` | 2026-09-03 20:13 |
| `R2_PUSH_OK 2 uploaded (0.1 MB), 757 unchanged` | `Builds/r2-push.log` | 2026-09-03 20:21 |
| `R2_PARITY_OK targets=Android,StandaloneWindows64,WebGL objects=266` | `Builds/r2-parity.log` | 2026-09-03 20:21 |

⭐ **The push log proving the push is THIS build's push is the whole point of §16.** Bundle names are
content-hashed; a push from a previous build can never cover this one. `Builds/r2-push.log` naming
`catalog_2026.09.04.354315.bin` is that proof, and it is why the line is quoted here rather than
paraphrased.

---

## 2. TWO CORRECTIONS LANDED TONIGHT - both measured, neither softened

### 2a. ⛔ `HudActionBarModel.MaxVisibleFaces` IS **4**, NOT 6. CLAUDE.md §7 WAS WRONG AGAIN.

- Code: `Assets/_Modules/Core/HudModel/HudActionBarModel.cs:121` - `public const int MaxVisibleFaces = 4;`
- Oracle: `Assets/Editor/Regression/HudLabelFitRegression.cs:266-269`
  (`Case0_BoxesStillAuthored`) **FAILS** if it is not 4, with the message
  *"adaptive peaceful HUD is locked to Build/Hero/Journey/Manage"*.
- A second suite pins it too: `Assets/Editor/Regression/SessionShapeRegression.cs:232`.
- The deriving consumer: `Assets/_Modules/HUD/Kit/HudKitController.cs:199-202` computes the slot
  width from the constant, so the View's geometry follows the code, not the doc.
- First flagged in commit `a17dfa126` body: *"HudActionBarModel.MaxVisibleFaces is 4, and a
  regression case FAILS if it is not 4. CLAUDE.md §7 says 6. The doc is stale, not the code."*

**THE AUTHORITY IS THE CONSTANT AND THE SUITE, NEVER THIS PAGE AND NEVER §7.**

⚠ **AND THIS IS THE SECOND TIME THAT SAME LINE HAS GONE STALE.** §7 already carries a correction
banner dated 2026-08-26 about the face count - which cost a felt-test report and an RCA when the
owner correctly saw five faces and the CLI opened a defect against working code. It was corrected
once and drifted again within eight days. **The pattern, not the number, is the finding:** a
hand-maintained count in canon tracking a live constant is duplicated state, and it fails the same
way every time (CLAUDE.md §2's stale WO block, §5's retired dependency table, §16's copy-pasted R2
verify). The fix applied to §7 is the same fix those took - **point at the constant, stop restating
the number.**

### 2b. ⛔ `publishing/SUBMIT_CHECKLIST.md` GATE A RECORDS THE WRONG APK.

Gate A was filled in tonight (commit `f1104a5fd`) with measured values against APK
`2026.09.04.354266`. **The shipped build is `354315`.** Every identity field in that block - APK
sha256, versionName, versionCode, source commit, APK path and size - therefore describes a file that
is not the one going to the store.

**Marked STALE in place with the reason; the values were NOT re-derived.** The lead re-records
against whichever APK actually ships. This is the copied-state trap §11B exists for, and it should
be visible rather than quietly patched.

---

## 3. THE OPEN ITEMS, RANKED BY COST

### 1. ⛔ SAVE DATA LOSS - PLAYER PROGRESS DISAPPEARING. NO TICKET EXISTS.

Reported by the lead from tonight's capture:

```
[Flow:BaseLayout] Enter build mode CENSUS: live PlacedStructure(s) in scene=9,
  loader.Loaded=9, persisted BaseLayout=17
```

**Nine structures live, seventeen persisted - eight gone.** The emitter is
`Assets/_Modules/Village/BuildMode/BuildModeController.cs:513-523`, and it exists precisely to
catch this: its own trailing sentence reads *"live << persisted = structures already gone before
this build session (F8-39 vanish happened earlier)"*. So the instrument names the shape AND tells
you the vanish predates the build session you are looking at.

⚠ **I could not find tonight's raw capture on disk** (no `logs/f8-inbox/*.md` and no
`Builds/*.log` carries the line). The quoted numbers are the lead's, this session. What IS in the
repo and confirms the defect is **not new**:

```
$ grep -h "Enter build mode CENSUS" logs/device/*.log
08-19 20:01:42.629  ... live PlacedStructure(s) in scene=0, loader.Loaded=0, persisted BaseLayout=8
08-20 09:04:54.090  ... live PlacedStructure(s) in scene=0, loader.Loaded=0, persisted BaseLayout=8
```

Two captures a fortnight ago with **zero of eight** live. Same shape, worse ratio, and it was never
worked. **This is the biggest known defect in the game and it has no ticket.** Everything else on
this list is cosmetic or operational by comparison; this one loses the player's town.

Note the interaction with WO-1357 (`e63494ed8`): `Destructible.NotifyBroken` deliberately DROPS the
persisted `BaseLayout` record when a structure is destroyed - so a legitimate destruction lowers
`persisted`, it does not raise it. `persisted` **exceeding** `live` is therefore the opposite of the
sanctioned path and cannot be explained by destruction.

### 2. ✅ RULED 2026-09-04 - THE 180s CEILING STAYS ON WALLET SIGNING. NO LONGER OPEN.

Owner, verbatim: ***"180 stays on wallet"***. `PackStore.cs:3075` keeps `WorldHold.Acquire`; the hold
is NOT split and NOT converted to `AcquirePlayerOwned`. **No code changed - the ruling was to leave
it.** This is an accepted, documented exposure, not a claim the exposure is absent: everything below
stays true, and `NotifyApplicationPause` (WO-1260) remains the mitigation covering the common
backgrounded case. ⛔ Do not re-open it from the argument below - that argument was read and ruled
against. An OBSERVED occurrence in a capture would be new evidence and a new ticket.
Recorded in `WorkOrders/WORK_ORDER_1360_a_user_pause_has_no_ceiling.md` §4.

*(The original open-call text is kept below, unrewritten, per CLAUDE.md §15.)*

### 2b. (was open) THE 180s HOLD CEILING STILL APPLIES TO WALLET TRANSACTIONS. MONEY IS ATTACHED.

Flagged, not changed, in commit `3e6ae4274` (WO-1360) - quoting the commit body verbatim:

> the 180s transaction ceiling. The signing leg sends the player out to a wallet app and is
> user-paced; a first-time install or a 2FA detour can exceed three minutes. If it fires, the world
> thaws under a live payment and drops the player into a running battle mid-signature - a
> manufactured route into "paid but not granted". Arguably two holds in sequence (user-owned
> signing, code-owned settlement). **That is an owner call.**

The mechanism this would use if ruled on already exists: WO-1360 introduced
`HoldKind { BoundedBeat, PlayerOwned }` and `AcquirePlayerOwned`, converting seven holds
(pause-menu, game-over, wave-results, combat-item-picker, bug-report-form, f8-note-capture,
vfx-parade-curation). **The ceiling stays the default by design** - unbounded must be asked for by
name. The signing leg was not converted because splitting user-paced signing from code-owned
settlement is a design decision, not a mechanical one.

⚠ **This ceiling has already broken something once, tonight.** WO-1353's watchdog force-released a
legitimate eight-minute pause with the PAUSED menu open on screen - F8 seq 4679,
`OVERRAN by 327.3s`. Same ceiling, different victim. A ceiling firing under a live payment is the
same bug with money on it.

### 3. THE SIGNING CERTIFICATE CANNOT BE PROVEN TO MATCH THE LIVE RELEASE.

`publishing/SUBMIT_CHECKLIST.md:101`, left deliberately unticked: the LIVE release's certificate
sha256 **was never captured**, so there is nothing to compare against. What is true and recorded:
the APK is signed by `dotr-release.keystore`, the keystore configured in `ProjectSettings.asset`
(`androidUseCustomKeystore: 1`, alias `dotr`).

**The cheap close, already written into the checklist:** install the candidate over the LIVE store
build on a device. Android refuses an update signed by a different key, so a **successful in-place
update IS the proof**. No new tooling needed.

### 4. THE VFX CASTER CAN STILL AUTHOR A TAG SHE DID NOT MAKE. ROOT CAUSE KNOWN, UNFIXED.

`VfxCasterWindow.TagSelected` (`Assets/Editor/VfxCasterWindow.cs:1223`) reads the key from a
never-cleared TextField and the prefab from the live selection - two persistent fields never
captured together - then overwrites an existing key with **no diff, no warning, no confirmation**
(recorded in `CANON_GROUND_TRUTH_2026-09-02.md`). Four bad tags in one hour, one of them attributed
to her for a choice she did not make.

The four bad tags **were retagged by her** and the seats bound in commit `7437942c6`
(`atfootprintoftree_Aura`, `atfootprintoftree_Impact` deleted, `EliteDeath_Impact`,
`BossDeath_Impact`). **The tags are fixed; the tool that produced them is not.** It will do it again
to the next batch. ⛔ Standing rule unchanged: she tags, the CLI maps verbatim - the CLI never picks
a prefab.

### 5. TWO SILENT SERVER-SIDE 400s. NEITHER TICKETED.

Found in her Vercel log export (3,253 rows) and recorded in commit `9d0294c5e`:

- `/api/entitlements` rejects **every guest player** - 53 of 66 calls 400.
  `isProvenValueId` (`api/.../wallet-auth.js:145`) admits only wallet or play ids, while the code's
  own comment at `:152` documents `PLAYER_ID_BAD_SHAPE` as *"neither a base58 wallet nor a
  guest-local id"* - a comment describing a rule the function does not implement.
  ⛔ **Do NOT fix by widening that predicate.** It is the membership rule for the proven-identity
  rail; a guest is proven by nothing, which is why it is excluded. Widening admits unproven ids to
  every consumer, including grant-bearing paths.
- `/api/catalog/collection` fails **11 of 11**, every one a build-carousel collection
  (`build-defenses`, `build-protection`). `readCollection` throws a `CatalogError` mapped to 400.
  Possibly the same family as PROD-020 - **unproven**, stated as the hypothesis it is.

Both fail silently: the client asks, gets a 400, renders its empty state. Her log export was the
only place either was visible.

### 6. TEXTURES AT 98.9 MB REMAIN THE LARGEST UNADDRESSED PAYLOAD BLOCK.

`docs/MESH_DECIMATION_PROCESS.md:277`; the same figure at
`WorkOrders/WORK_ORDER_1314_webgl_remote_payload_against_a_512mb_heap.md:310` in the category table
(`Textures 98.9 mb 33.5% / Meshes 68.7 / Sounds 48.6 / Total 295.1 mb`). WO-1314:385 notes it is
*"7,000+ already-shrunk files, not a"* single win - i.e. there is no one asset to fix. ⚠ **Parked
behind the owner's 2026-09-02 ruling that the APK is the lane and Pi/WebGL is quiet.**

---

## 4. WHAT SHIPPED TONIGHT (sourced to commits, not to ticket titles)

Read the bodies - they are long and they state what did NOT ship as clearly as what did.

- `dabfeecf2` + `a055da803` **WO-1352** structures show wear from the first point of damage. ⭐ The
  follow-up found it was a **bug, not a bad number**: the gloss ramp interpolates on
  `t = (step-1)/(steps-1)`, zero at step 1 by construction, so the second channel was
  *mathematically incapable of moving* at step 1. `damage-states.json` v3 -> v4 adds
  `scuffGlossStep1`.
- `e63494ed8` + `3921a487e` **WO-1357** the Journey Raids card locks and says WHICH barracks problem
  it is, reusing the existing `PostureSignals.RaidCapable` predicate rather than writing a second
  barracks check. Two boundaries deliberately left and flagged (a barracks under construction still
  unlocks raids; a WO-819 baked twin stays capable - ⛔ do not "fix" the latter to `IsPlayerBuilt`,
  it would lock raids on a pre-handover Default-Town save).
- `a17dfa126` + `1fb837410` **WO-1359** her action bar emblems, sliced from the sheet's own alpha and
  keyed by caption, so a slot physically cannot be handed another face's emblem.
  `PresentAuthoredEmblem` makes the kit step back only when authored art answered.
- `3e6ae4274` **WO-1360** a player-owned pause has no ceiling. Fixes a regression shipped hours
  earlier by WO-1353.
- `ecb1a1a5e` **WO-1353** the world clock gets ONE owner; every slow pairs with a return. Measured
  defect: `timeScale=0.28` in open town, no battle, no modal.
- `d836d2f15` **WO-1354** the Close the Gap rail rebuilds when prices arrive.
- `56fed789c` + `695a5c92b` + `759063f3f` **WO-1355/1356** the board Submit loop: she taps Submit,
  one command ingests, closes and bounces. First real run closed 40 and bounced 3 with her notes
  carried verbatim into the tickets.
- `7437942c6` **WO-1343 follow-up** the two held VFX seats bound to the keys she retagged.
- `c06475c1d` / `d041ed68c` / `99b574392` / `bdfce98dc` / `bcecb5991` FTUE talent beat, one producer
  per label, retreat no longer locks the battle, the guide's pathfinding, the Night Market HUD door.
- `13711c14b` the privacy and terms pages the store rejected us over, restored.
- `0a15744c9` release notes for everything since the live store build, plus the `354266 -> 354315`
  bundle bump.

**Did NOT ship, on the record:** hero decimation to 50k is **reverted and parked**
(`47aae2d8d` -> `e07e1b860`, the Mage deformed in motion). The "smaller download" claim was
**deliberately left out** of the listing text because the two figures in the tree disagree (80 MB vs
49 MB) and could not be reconciled without a build - commit `0a15744c9` says so in as many words.
⚠ **Do not resurrect that claim from a doc; measure it.**

---

## 5. STATE - read every number from its source, never from this page

| Fact | Authority |
|---|---|
| Branch / push state | `git status`, `git log` |
| Save schema version | `SaveSchema.CurrentVersion` (`Assets/_Modules/Core/State/SaveSchema.cs`) |
| Suite count | the `REGRESSION_OK` marker on a fresh log |
| Action bar visible faces | `HudActionBarModel.MaxVisibleFaces` + `HudLabelFitRegression` Case 0 |
| Next free WO number | the `CLI_LANES_WO_NUMBERS.md` banner rows |
| Board / ticket status | `BOARD.html`, regenerated by `python tools/board_build.py` |
| Web surfaces + project ids | `tools/web-ship.ps1 -ListSurfaces` |
| Assembly dependencies | the `.asmdef` files themselves |
| Gate results | the MARKER on a FRESH log, never a doc, never an exit code |

**Still true from the 09-02 anchor, carried forward rather than restated:** the Android APK is the
priority lane and Pi/WebGL is parked (owner, 2026-09-02); the pay path IS activated (owner,
2026-08-23) so an economy removal is no longer a clean purge; a balance value is a tunable by
default; four Vercel projects serve this game and two are public production. Read
`CANON_GROUND_TRUTH_2026-09-02.md` for the detail on each - this page does not duplicate it.

---

## 6. THE LESSON OF THE NIGHT

**A number copied into a doc is a defect waiting for a date.** Three of tonight's findings are the
same failure wearing different clothes: §7's face count (corrected 08-26, wrong again by 09-03),
Gate A's APK identity (correct for 40 minutes), and the anchor pointer at the top of `KEY_FACTS.md`
(stale three times, and its own footnote says so). The cure is never a better copy. It is deleting
the copy and pointing at the thing.
