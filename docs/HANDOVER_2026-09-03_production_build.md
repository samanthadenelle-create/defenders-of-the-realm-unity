# HANDOVER - 2026-09-03 - THE PRODUCTION-CANDIDATE BUILD

> **Read `CANON_GROUND_TRUTH_2026-09-03.md` (repo root) first.** This file is the session narrative;
> that file is the anchor. Where they disagree, the anchor wins.
>
> ⛔ **Sourced per CLAUDE.md §11B** (landed today, commit `f1104a5fd`). Every claim below names a
> commit, a marker, a log line or a file:line. Where something could not be proven from this repo it
> says **unverified** in those words.

---

## THE ONE THING TO KNOW

**`2026.09.04.354315` is the production candidate.** It is on her Seeker, it is gate-green on fresh
logs, and its content bundles are pushed and parity-verified. The submission paperwork is the part
that is not finished - not the build.

| Fact | Value | Source |
|---|---|---|
| versionName | `2026.09.04.354315` | `ProjectSettings/ProjectSettings.asset:148` |
| versionCode | `354315` | `ProjectSettings/ProjectSettings.asset:177` |
| Branch | `feat/synty-art-retheme`, pushed | `git status` / `git log` |
| HEAD at handover | `0a15744c9` | `git rev-parse --short HEAD` |
| Updates live release | `2026.08.17.328845` | `publishing/SUBMIT_CHECKLIST.md:41` |
| Distance from live | 756 commits, ~230 player-facing | commit `0a15744c9` body |

**On-device:** the lead reports `adb shell dumpsys` returned versionCode `354315`, lastUpdateTime
`2026-09-03 20:21:27`. ⚠ **That dumpsys output is not captured in this repo** - it is the running
seat's report. The in-repo corroboration is `Builds/r2-push.log` (mtime 20:21) naming
`Android/catalog_2026.09.04.354315.bin`: the push that accompanied the install carries this
version's catalog, so the bytes on the CDN and the bytes on the phone are the same generation.

### Gates - markers on fresh logs (§8: judge the marker, never the exit code)

```
COMPILE_GATE_OK :: scripts compiled clean                                Builds/compile-gate.log  20:10
REGRESSION_OK 358/358 suites -- 358 green, 0 red, 0 skipped              Builds/regression.log    20:13
R2_PUSH_OK 2 uploaded (0.1 MB), 757 unchanged                            Builds/r2-push.log       20:21
R2_PARITY_OK targets=Android,StandaloneWindows64,WebGL objects=266       Builds/r2-parity.log     20:21
```

All four logs are dated 2026-09-03 and postdate the tree. ⭐ **`R2_PUSH_OK` naming this build's own
catalog is the §16 proof** - bundle names are content-hashed, so a push from a previous build can
never cover this one, and the bucket looking full proves nothing.

---

## WHAT SHIPPED (read the commit bodies - they are honest about what did not)

Do not summarise this from ticket titles. Each commit body states its own boundaries, its own
deliberate omissions and, in three cases tonight, its own mistake.

- **WO-1352 - structures show wear from the first hit** (`dabfeecf2`, follow-up `a055da803`).
  ⭐ The follow-up found the tuning complaint was a **bug**: the ramp interpolates on
  `t = (step-1)/(steps-1)`, which is zero at step 1 by construction, and the gloss ramp's step-1
  endpoint was a hardcoded `1f` - the second channel was mathematically incapable of moving at step
  1. No re-tune could have fixed it; it needed a new field (`scuffGlossStep1`,
  `damage-states.json` v3 -> v4).
- **WO-1353 - the world clock gets ONE owner** (`ecb1a1a5e`). Measured defect:
  `[Flow:HeroOwner] ... timeScale=0.28` in open town, no battle, no modal. ⛔ The CLI read past that
  line and built a frame-budget story off `fps=40`, asserting it twice. Owner: *"Why did you guess"*.
  That is the banned inference-fix and it cost two rounds.
- **WO-1354 - the Close the Gap rail rebuilds when prices arrive** (`d836d2f15`).
- **WO-1355 / WO-1356 - the board Submit loop** (`2220d79a3`, `56fed789c`, `695a5c92b`,
  `759063f3f`). She taps Submit on `BOARD.html` over `file://`, a drop file lands in Downloads, and
  an ordinary `python tools/board_build.py` ingests it, closes what she passed and bounces what she
  failed **carrying her note verbatim into the ticket**. First real run: closed 40, bounced 3.
  ⭐ Root cause of the confusion that started it was two sources of truth for one fact - the page
  counted marks from browser storage while the close pass read the file on disk.
- **WO-1357 - the Journey Raids card locks and says which barracks problem it is** (`e63494ed8`,
  art follow-up `3921a487e`). Reuses the existing `PostureSignals.RaidCapable` predicate; ⛔ no
  second barracks check was written, and the oracle fails the build if `PlayerDeckWorkspace` ever
  grows one.
- **WO-1359 - her action bar emblems** (`a17dfa126`, kit split `1fb837410`). Sliced from the sheet's
  own alpha and keyed by caption, so a slot cannot be handed another face's emblem.
- **WO-1360 - a player-owned pause has no ceiling** (`3e6ae4274`). Fixes a regression WO-1353
  shipped hours earlier: the watchdog force-released a legitimate eight-minute pause with the PAUSED
  menu open on screen (F8 seq 4679, `OVERRAN by 327.3s`).
- **WO-1343 follow-up** (`7437942c6`) - the two held VFX seats bound to the keys **she** retagged.
- Earlier in the day: FTUE talent beat (`c06475c1d`), one producer per label (`d041ed68c`), retreat
  no longer locks the battle (`99b574392`), the guide's pathfinding (`bdfce98dc`), the Night Market
  HUD door (`bcecb5991`), the privacy and terms pages the store rejected us over (`13711c14b`).
- **Release notes for everything since the live store build** (`0a15744c9`) - 501 chars, ASCII
  verified, every claim sourced to a commit in the file. ⛔ `publishing/config.yaml` was **not**
  edited; she pastes the approved wording.

### Did NOT ship, on the record

- **Hero decimation to 50k is REVERTED and PARKED**, not cancelled (`47aae2d8d` -> `e07e1b860`) -
  the Mage deformed in motion. ⚠ A T-pose render, a bone count, a byte-identical `.meta` and a clean
  `COMPILE_GATE_OK` are all compatible with a broken character. **`COMPILE_GATE_OK` does not cover
  rig errors.**
- **The "smaller download" listing claim was deliberately left out.** The two figures in the tree
  disagree (80 MB in `d706b430b` vs the 09-03 draft's 49 MB) and could not be reconciled without a
  build. Commit `0a15744c9` says so explicitly. **Do not resurrect it from a doc - measure it.**
- Excluded from the listing text per §11B ("do not describe a feature the reviewer cannot reach"):
  the remote catalog seam, clan chat, the Map tab, Command Center and kill switches, Google Play
  billing, and the premium half of the battle pass.

---

## OPEN ITEMS - ranked by cost

### 1. ⛔ SAVE DATA LOSS. NO TICKET EXISTS. THIS IS THE BIGGEST KNOWN DEFECT.

Reported by the lead from tonight's capture:

```
[Flow:BaseLayout] Enter build mode CENSUS: live PlacedStructure(s) in scene=9,
  loader.Loaded=9, persisted BaseLayout=17
```

Nine live, seventeen persisted - **eight structures gone**, and the trace itself names it as an
earlier vanish. The emitter is `Assets/_Modules/Village/BuildMode/BuildModeController.cs:513-523`;
its own trailing sentence reads *"live << persisted = structures already gone before this build
session (F8-39 vanish happened earlier)"*.

⚠ **Tonight's raw capture is not on disk** (no `logs/f8-inbox/*.md`, and no `Builds/*.log` carries
the line) - the quoted numbers are the lead's, this session, **unverified from the repo**. What IS
in the repo and proves the defect is not new:

```
$ grep -h "Enter build mode CENSUS" logs/device/*.log
08-19 20:01:42.629  ... in scene=0, loader.Loaded=0, persisted BaseLayout=8
08-20 09:04:54.090  ... in scene=0, loader.Loaded=0, persisted BaseLayout=8
```

Zero of eight, twice, a fortnight ago. Never worked.

**Why destruction does not explain it:** WO-1357's commit (`e63494ed8`) establishes that
`Destructible.NotifyBroken` frees the footprint, calls `BaseLayoutLoader.Forget` and **drops the
persisted BaseLayout record**. A legitimate destruction therefore lowers `persisted`. `persisted`
*exceeding* `live` is the opposite of the sanctioned path.

**Next session should open the ticket and instrument first (§12).** The census already tells you the
vanish happened BEFORE build-mode entry, so the instrumented window is the preceding session -
death, scene load, or save write - not the build session you can see.

### 2. ⛔ THE 180s HOLD CEILING STILL APPLIES TO WALLET TRANSACTIONS. OWNER CALL, MONEY ATTACHED.

Recorded in WO-1360 / commit `3e6ae4274`, flagged rather than changed:

> The signing leg sends the player out to a wallet app and is user-paced; a first-time install or a
> 2FA detour can exceed three minutes. If it fires, the world thaws under a live payment and drops
> the player into a running battle mid-signature - a manufactured route into "paid but not granted".

The mechanism to fix it exists already: `HoldKind { BoundedBeat, PlayerOwned }` and
`AcquirePlayerOwned`, shipped tonight, with the ceiling deliberately kept as the **default** so a
future author cannot get it wrong by omission. Seven holds were converted; the signing leg was not,
because splitting user-paced signing from code-owned settlement is a design decision.

⚠ **This ceiling already broke something once tonight** - the eight-minute pause, F8 seq 4679. Same
ceiling, different victim, and the payment victim costs money.

### 3. THE SIGNING CERTIFICATE CANNOT BE PROVEN TO MATCH THE LIVE RELEASE.

`publishing/SUBMIT_CHECKLIST.md:101`, deliberately unticked: the LIVE release's certificate sha256
was never captured, so there is nothing to compare against. What is true: the APK is signed by
`dotr-release.keystore`, the keystore configured in `ProjectSettings.asset`
(`androidUseCustomKeystore: 1`, alias `dotr`).

**The cheap close:** install the candidate over the live store build on a device. Android refuses an
update signed by a different key, so a successful in-place update **is** the proof. Record the live
value once observed so it is never PENDING again.

### 4. THE VFX CASTER CAN STILL AUTHOR A TAG SHE DID NOT MAKE.

`VfxCasterWindow.TagSelected` (`Assets/Editor/VfxCasterWindow.cs:1223`) reads the key from a
never-cleared TextField and the prefab from the live selection - two persistent fields never
captured together - then overwrites an existing key with no diff, no warning and no confirmation.
Four bad tags in one hour; one was attributed to her for a choice she did not make.

**The four tags are fixed** (she retagged, `7437942c6` bound the seats). **The tool is not.** Root
cause known, unfixed, will recur on the next batch. ⛔ Standing rule: she tags, the CLI maps
verbatim - the CLI never picks a prefab.

### 5. TWO SILENT SERVER-SIDE 400s. NEITHER TICKETED.

From her Vercel log export, recorded in commit `9d0294c5e`:

- `/api/entitlements` - 53 of 66 calls 400, every guest player rejected. `isProvenValueId`
  (`wallet-auth.js:145`) admits only wallet or play ids while the comment at `:152` describes a rule
  that allows guests. ⛔ **Do not fix by widening that predicate** - it is the membership rule for
  the proven-identity rail, and widening admits unproven ids to grant-bearing paths.
- `/api/catalog/collection` - 11 of 11 fail, all build-carousel collections. `readCollection` throws
  a `CatalogError` mapped to 400. *Possibly* the same family as PROD-020 - stated as a hypothesis,
  unproven.

Both fail silently: the client renders its empty state and nothing reaches the screen.

### 6. TEXTURES AT 98.9 MB - THE LARGEST UNADDRESSED PAYLOAD BLOCK.

`docs/MESH_DECIMATION_PROCESS.md:277`;
`WorkOrders/WORK_ORDER_1314_webgl_remote_payload_against_a_512mb_heap.md:310` gives the category
table. It is 7,000+ already-shrunk files, so there is no single asset to fix. ⚠ Parked behind the
owner's 2026-09-02 ruling that the APK is the lane.

---

## TWO CORRECTIONS LANDED WITH THIS HANDOVER

1. **`CLAUDE.md` §7 - `MaxVisibleFaces` is 4, not 6.**
   `Assets/_Modules/Core/HudModel/HudActionBarModel.cs:121`; pinned by
   `HudLabelFitRegression.cs:266-269` (Case 0) and `SessionShapeRegression.cs:232`. **The code and
   the suites are the authority.** ⚠ §7 already carried a correction banner about that same line
   from 2026-08-26 - it was corrected once and drifted again within eight days. **That pattern is
   the finding**, not the number: a hand-maintained count tracking a live constant is duplicated
   state and fails the same way every time.
2. **`publishing/SUBMIT_CHECKLIST.md` Gate A is marked STALE.** It was filled in tonight against APK
   `2026.09.04.354266`; the shipped build is `354315`. Values were **not** re-derived - the lead
   re-records against whichever APK actually ships.

---

## THE HABIT THIS SESSION ADDED - CLAUDE.md §11B

Owner, verbatim: *"we never Guess, always must be proven to be true. Documention is the same is must
be proven followed not gone off script without explicit permission"* (commit `f1104a5fd`).

Two halves, both hard rules:

- **A claim without evidence is not a claim.** Every factual statement traces to something read or
  measured this session. *"Probably / should be / I believe"* are admissions of a guess; the honest
  sentence is *"I have not proven this"* plus what would prove it.
  ⭐ Forged twice in one evening, and both times a **real** measurement was used to support a
  conclusion it did not support: `fps=40` became a frame-budget theory while `timeScale=0.28` sat
  unread in the same output; and a GET against a POST-only endpoint returned 400 and was reported as
  a server defect. **Measuring something is not the same as measuring the right thing.**
- **Follow the documented procedure and prove you followed it.** Reading the top and improvising the
  rest is going off script. Deviation needs her explicit permission in advance.
- ⚠ It does **not** license bouncing solvable problems back. Fixing it yourself and reporting after
  is required. What is forbidden is claiming without proof, or skipping a written procedure without
  asking.

---

## RESUME POINTS FOR THE NEXT SESSION

1. **Open a ticket for the save data loss and instrument the preceding session** (item 1). Nothing
   else on the list costs the player their town.
2. **Put the 180s-vs-wallet question to the owner** (item 2). It is a one-word ruling and the
   mechanism is already in the tree.
3. **Close the certificate gap the cheap way** (item 3) - install over the live store build.
4. **Re-record Gate A against the APK that actually ships** and clear the STALE banner.
5. Ticket the two API 400s (item 5) and the VFX Caster tool defect (item 4).
