# Session Handover — 2026-08-14 (CLI)

**Branch:** `wip/village2-and-f8-tickets` · **HEAD at handover:** `56c889c23` · **Working tree: clean, all pushed.**
**Frozen ledger** (CLAUDE.md §15) — do not rewrite this file. Supersede it by date.

---

## 1. Read these first

| Artifact | What it holds |
|---|---|
| `docs/OPEN_OWNER_QUESTIONS_2026-08-14.md` | **Every decision blocking work**, 16 sections, cited to `path:line`. Start here. |
| `docs/WEAPONS_DEEP_DIVE_2026-08-14.md` | **96 weapons, 24 obtainable.** Two live menu commands that destroy each other's output; the art-half and design-half are each unfinished in the opposite direction. Owner-commissioned; has a 7-step plan with steps 1–3 dispatchable immediately. |
| `CLI_LANES_WO_NUMBERS.md` banner | Numbering authority. **Next free: 996.** 984–995 minted today. |
| `BOARD.html` | Regenerate with `python tools/board_build.py` before any board read. |
| This file | State, in-flight work, and the methodology corrections. |

---

## 2. What shipped (17 commits, all pushed)

**Gameplay / data**
- Addressables **content-build seam** wired into all four `BuildPlayer` call sites — a player build can no longer ship a catalog nobody rebuilt. Portal moved to tracked `Assets/Art/Dungeon/Exit`, keyed `dungeon/exit/portal`.
- Dungeon exit **portal**: normalize → re-measure → re-seat (measured three-axis delta), hero-scale ×1.5, `EXIT` label removed per owner ruling.
- **Camera yaw pair zeroed.** `DungeonCameraRig._headingYawOffset` existed solely to cancel `FaceHeading`'s `Euler(0,-90,0)`. Both halves now 0.
- **VFX pool write-side fix** — the pools were *manufacturing* destroyed hosts, not merely returning them.
- **Cost-basket separation fully applied** (WO-947): six rows, all owner pins spent, `PendingPins` empty.
- **`tower_healer` retired** (WO-990) — never buildable. The `HealerTower` *behaviour* is kept and annotated as the WO-891 field-pattern reference; WO-991 is its future consumer.
- Enemy **model mapping**: only the Hollow branch honoured `enemies.json`; every other family let the code switch win. The ATB boss had been a violet capsule (retired `Dragon` mesh, null prefab, logged as *"expected fallback"*).
- **Interact button re-skinned** to the Obsidian kit (owner: *"the purple should go"*) — global button, so town changed too, with her confirmation.

**Verification infrastructure — the highest-leverage work of the day**
- **`run-unity-method.ps1`** now proves the log belongs to *this* run (`-ExpectMarker`, optional, backward compatible). Exit 8 with named reasons: `LOG_MISSING`, `LOG_STALE_FROM_EARLIER_RUN`, `LOG_TRUNCATED`, `MARKER_ABSENT`.
- **`tools/capture/headed-dungeon-capture.ps1`** now refuses wrong-scene / frozen-clock / stolen-focus / un-driven runs (exit 5/6/7/8). Shots stage to `%TEMP%` and publish only on success; failures land in `<dir>-INVALID`.
- **`hasSurface` replaced** — was `docOk || canvasOk`, two non-null checks gating its own failure. Now four separately-named, proven-falsifiable classes plus a mandatory named batchmode skip.
- New regressions: `[cost-basket]` (self-cleaning — the exemption list may only shrink), `[exit-beacon-layouts]`, `[vfx-pool-shape]`, `[ui-surface-probe]`, EndState capture cases.

---

## 3. In flight at handover

| Item | State |
|---|---|
| **Weapons deep-dive SME** | Running. Owner asked for weapons to be *"resolved"* — pipeline map, obtainable-vs-authored gap, seating state, dispatchable plan. Result lands in the task log, **not** the repo. Harvest it. |

Everything else is committed. No agent holds an uncommitted edit.

---

## 4. Backlog: verified, not guessed

Every band 1–1099 was swept by read-only agents under one rule: **a commit saying "WO-123 done" is a claim, not evidence** — open the file or return UNVERIFIED.

| Band | Result |
|---|---|
| 1–299 | 65 verified — **46 closeable (71%)**. 15 phantom, 15 obsolete, 16 superseded, 16 partial, **3 still ready** |
| 300–599 | 73 rows — 26 phantom, 15 partial, 9 still ready, 7 unverified, 5 obsolete, 4 superseded |
| 600–799 | 16 phantom, 18 partial, 5 superseded, **10 rescued as still-ready** |
| 800–899 | 2 phantom, 14 partial, 2 superseded, 3 still ready |
| **900+** | **ZERO phantoms.** Every named defect site still present in HEAD |
| First pass (78 mixed) | Only **2** genuinely still ready |

**The owner's instinct was right and the data proves it:** below 800 the backlog is sediment; at 900+ it is real work. 36 phantom statuses were flipped to DONE with citations (commit `b912583af`).

⚠ **A bulk legacy-close of pre-800 was started and STOPPED on owner correction** (*"i never said bulk close before 800, i said verify"*). **Zero files were modified** — verified. Do not resurrect it.

---

## 5. Corrections to canon made today

- **WO-966 / WO-985 are one subject.** `HeroBodySwapper.cs:263` is **shared surface** — the dungeon Keeper uses the same body and swapper. One edit, gated once, verified in both scenes. Six corrections banner-ed onto WO-966, including that its *"no capture needed"* claim is retracted and its title's "45 degrees" is a felt estimate (measured is ~94.5).
- **WO-970's "immune either way" sentence is FALSE** and is why the shield bug survived — see WO-994.
- **WO-271's acceptance criterion is now BACKWARDS.** `DialogueView.cs:190-197` deliberately sits above the HUD band by an owner-verified F8 fix. **Implementing WO-271 as written would re-break it.** Needs an explicit close.
- **WO-110's acceptance contradicts the WO-753 ruling** at `WallSegment.cs:468`.
- **`weapons.json` dual-copy asymmetry is BY DESIGN** (Resources 96 curated, StreamingAssets 431 library, via `GearCurationExporter`). Do not "fix" it.

---

## 6. Owner rulings captured today

- **Cost baskets:** magical = crystals+iron; healing is magical; jeweler is crafting; `tower_wall_wizard` is a mechanical ballista; **Cathedral of Magic is magical** (it is where magic upgrades live — the engine of progression, not a vendor).
- **Echoes are a FAUCET** — helpers, not physical items. No animation, no walk-to-node. Pet aura + pet progression + `EchoSpiritPresentation` retire (WO-993).
- ⛔ **BUT the Echo GUIDE is carved out** — *"we use the wolf fbx as the echo guide"*. **`PetHeroLeash` STAYS.** `BlankStartCensusRegression.cs:42-43`: *"the guide's only body is the wolf the founding arc summons"* — there is **no fallback NPC**, and `FoundingGuideWolfBodyRegression` guards it.
- **`tower_healer`: retire.** **`AuraController`: retire.**

---

## 7. Live defects with no owner yet

1. **Action bar shows 4 faces; canon says 6.** `Build · Bag · Quests · Manage` — **Talk and Raids missing.** Unknown whether dropped or flag-gated.
2. **HUD clips both screen edges** — settings gear sliced left, `Resources` chip cut right, vitals truncated to `s... 144`.
3. **`armor.json` dual-copy violation** — `armor_knight` 10× in Resources, **0× in StreamingAssets**. Android/WebGL read a different roster.
4. **Shield mis-seats on dungeon port** — fully diagnosed, WO-994.
5. **Dungeon boot self-evicts to town** — WO-995, nondeterministic (6 launches, some evicted).
6. **Seven classes ship and are never instantiated** — WO-992.
7. **Two data sets ship with zero readers** — `dungeon-kit.json`, `Resources/Data/Dungeons/*`.
8. **Synthetic WASD does not move the TOWN hero** (works in dungeon). Blocks headed town captures.

---

## 8. ⚠ Methodology — read this before trusting any report

Today produced **nine** instances of one failure: *a check written to describe the healthy case, with no thought given to what the broken case would print.*

- A gate wrapper exiting **0** on a script path that does not exist.
- A capture harness certifying **ten screenshots of a frozen town** as a dungeon proof.
- A regression suite holding **159/159 green** through a bake that reverted an owner ruling.
- An acceptance grep written against an **em-dashed string it could never match**.
- `hasSurface` — two non-null checks gating their own failure.
- A `"1x1 cells"` fallback identical to a real measurement.
- A facing instrument reading a **hard 0** in the exact scene it measures (`velMag` gated, and dungeons force `Velocity` to zero).
- **My own backlog query silently missing 212 of 458 READY tickets** (`**Status: READY**` with the colon inside the bold).
- A refusal path reporting exit 1 instead of its named code — *the refusal machinery had the defect it exists to prevent*.

**Two methods that actually worked, and should be standard:**
1. **GUID search, never name-grep.** Unity serialises script refs by GUID; a class-name grep across `.unity`/`.prefab` finds nothing and reads as "no problem". That is how seven classes sat unwired for ~2.5 months. WO-190 and WO-279 both flip verdict on this.
2. **Demonstrate acceptance by running it.** WO-984 and WO-988 pasted observed exit codes. Both found bugs *by running* that reading had missed.

**Agent reliability:** one sweep agent filed **three contradictory reports** — two claiming coverage it did not have, then an honest correction (*"I stated twice that delegated batches had come back when no agent had in fact reported"*). **Trust the conservative report.** ~30 tickets in 353–587 still need a re-run.

---

## 9. Where the orchestrator's own judgement was weakest

Recorded plainly so the next seat calibrates.

- **WO-975 (Blink):** an SME's *"~68 MB via LFS"* was relayed as fact and an owner decision framework built on it. The pack is **12.85 GB** — wrong by ~190×. When the real number arrived the question should have collapsed; instead it was re-asked with a corrected number. **Retracted in the questions doc.** Owner input needed: none.
- **Relayed "47 of 74 closeable" as fact** before the same agent's contradiction arrived.
- **Relayed "`tower_healer` is player-reachable"** as an economy warning when the data refuted it.

Pattern: the verification held where agents proved things at source; **synthesis on top of it was the weak layer.** Anything not cited to `path:line` should be treated as unverified.

---

## 10. Resume points

**Highest value first:**
1. **Harvest the weapons deep-dive** from the task log before it is lost.
2. **WO-995** — the dungeon-boot eviction. Until fixed, *every* dungeon acceptance capture is a lottery (WO-1007, WO-980, WO-983 all depend on it).
3. **WO-994** — shield re-dial. Code + trace halves are dispatchable; **the data half is the owner's** (Seating Editor). ⚠ An agent must **not** compute a compensating euler — that recreates the stranded pair one layer up.
4. **WO-966/985** — the hero yaw fix, as ONE coordinated edit. Fix the blind dungeon instrument first, or the proof is worthless.
5. **The 4-face action bar** — canon says 6; Raids is the whole offense loop's entry.
6. **`armor.json`** dual-copy — a shipping-platform defect.
7. Re-run the ~30 unverified tickets in 353–587.

**Owner-blocked, in `docs/OPEN_OWNER_QUESTIONS_2026-08-14.md`:** WO-716 (root-blocks three), WO-949, WO-802/804, WO-980, WO-910, WO-993, WO-991, WO-986, the VFX cluster, WO-271 (close it), WO-182 (prose-only?).
