# Open Owner Questions — 2026-08-14

**Purpose:** every decision currently blocking work, gathered in one place for review.
**Source:** the full-backlog verification sweep of 2026-08-14 (bands 300–1099 verified against HEAD by
opening implementation files, not by trusting commit messages or RESULT files).

**How to read this:** each question states what is blocked, what the code actually says today, and the
options. Nothing here is a request for engineering direction — these are all calls only the owner can
make. Where there is a recommendation it is labelled as such and is not load-bearing.

> ⚠ **A caution about this document's own provenance.** Today produced repeated instances of confident
> reports that turned out to under-report or overstate: a gate wrapper that exited 0 without running, a
> capture harness that certified ten screenshots of the wrong scene, a regression suite that stayed
> 159/159 green through a reverted owner ruling, a backlog query that silently missed 212 of 458 READY
> tickets, and one sweep agent that filed two contradictory coverage claims. Facts below are cited to
> `path:line` where they were read at source. **Anything not cited should be treated as unverified.**

---

## 1. WO-975 — NOT A QUESTION. Retracted 2026-08-14.

> ⚠ **This was wrongly presented to the owner as a decision. It is engineering work with one correct
> answer, and it should never have consumed her attention.**
>
> **What went wrong:** an SME estimate of *"~68 MB via LFS"* was relayed as fact, and an owner decision
> framework (is the repo private? does the Asset Store EULA permit committing the packs?) was built on
> top of it. The pack is **12.85 GB** — the estimate was wrong by ~190×. When the real number arrived,
> the question should have collapsed; instead it was re-asked with a corrected number attached.
>
> **The correct answer, which needs no ruling:** you do not commit a 12.85 GB vendor art pack — that is
> source `.png`/`.tga`/`.psd`, not shipping assets. The licence question is irrelevant because the size
> question already settles it.
>
> **The actual work:** make the Gear Addressables group follow the curation that already exists in this
> tree (`GearCurationExporter` + `GearCurationPicks.json` already do exactly this for `weapons.json`:
> a 431-entry library curated down to a 96-entry runtime set), then promote only the ~91 reachable
> prefabs' art at shipping resolution to a tracked path. Fix `GearCatalogGenerator.BlinkGearSource`'s
> missing curation filter so the group cannot re-inflate to 427.
>
> **Owner input needed: NONE.** Retained below only as the record of what was measured.

**Context (kept for the numbers, not as a question):**

**What is actually true (measured 2026-08-14):**

```
Assets/Blink   4,579 files   12,851 MB
  .png  2,201 files   8,067 MB
  .tga    160 files   4,178 MB
  .fbx    760 files     420 MB
  .psd     18 files     136 MB
```

- `Gear.asset` holds **427 entries**; roughly **91 are reachable** from catalog data, ~335 are dormant.
- The dormant ones are **not dead** — `GearCatalogGenerator.BlinkGearSource` re-derives rows from every
  prefab it finds **with no curation filter**, so re-running the generator makes them live again.
- ⚠ **An earlier estimate in this project put the promote at "~68 MB via LFS". That was wrong by ~190×.**
  Any plan resting on that number should be re-examined.

**The question:** how should the gear art be made survivable on a fresh clone?

| Option | Cost | Notes |
|---|---|---|
| **A. Curate the group, promote only what ships** *(recommended)* | small | The tree ALREADY does this for weapons: `GearCurationExporter` + `GearCurationPicks.json` produce a curated Resources set (96) from a library (431). Make the Gear group follow the same curation, then promote only the ~91 reachable prefabs' art — shipping-resolution textures, **not** `.tga`/`.psd` sources. |
| **B. Commit the pack** | **12.85 GB** | Off the table on size alone, before the Asset Store licence question is even reached. |
| **C. Commit nothing; hard-fail the build on dangling entries** | ~zero | Loud and honest, but leaves gear art unavailable on a fresh clone. Viable as an interim. |

**Sub-question if A:** should the generator's lack of a curation filter be fixed (so the group can never
re-inflate), or is a one-time curation acceptable?

---

## 2. WO-716 → 720 / 722 — the pair-walk sheet is blank

**Blocked:** three tickets, at the root.

`PAIRWALK_716.md:26` records that the `-Graphics` fleet run was **skipped** (a Vercel preview was
substituted). No `panel_*.png` exists, and **all 18 PASS/FIX cells are blank** — so WO-720 and WO-722
have no work list to execute against.

WO-720's defect is confirmed live and independent of the sheet: `FoundingChoiceController.cs:168` still
opens frameless — the "flat box teaching moment" in the founding flow.

**The question:** re-run WO-716's capture step, or rule the live-preview walk sufficient and let 720/722
proceed from spot findings?

---

## 3. WO-949 — should dying cost anything?

**Blocked:** the death-UX one-shot (D3).

**Discovery the ticket asked for and nobody had recorded:** **dying in town currently costs nothing.**
`HeroHealth.cs:878-884` — there is no debit on that path.

**The question:** does death carry a price, or does the one-shot teach *"you keep everything but lose
the run"*? Deliverables D1 and D2 have already landed; only this beat is open.

---

## 4. WO-802 / WO-804 — which raid model is canon?

**Blocked:** raid stakes, casualties, and the star-tier HUD split.

- **802:** the WO specifies a **star-tiered casualty table**. The shipped model has casualties emerge
  from wounding (`RaidDeployController.cs:532-570`, `ArmyStorage.cs:280-333`). **They contradict.**
- Also open in 802: the **retreat/defeat exit has no summary panel** — loot is granted with only a
  FlowTrace line (`RaidDeployController.cs:480-505`). A player who retreats sees nothing.
- **804:** scopes 1+2 shipped under WO-853 §7 as a **50/30/20** blend
  (`RaidScoring.cs:487-516`). What remains is that the HUD and victory screen never split
  **Defenders %** vs **Structures %**.

**The questions:** (a) casualty table or emergent wounding — which is canon? (b) is 50/30/20 the answer?
If yes, 804 shrinks to a HUD copy split.

---

## 5. WO-980 — dungeon readability

**Blocked:** the entire ticket; no engineering has started.

Verbatim from its §3: *"Walking through a dungeon, can you tell where you are going and where the walls
are — or does the screen read as a bright blur with your hero as a dark shape?"*

**The question:** answer that, from play. The answer decides close-as-intended vs one of four named fixes.

> ⚠ Note: an acceptance capture for this cannot currently be trusted — the headed harness certified ten
> screenshots of the **town with a frozen clock** as a dungeon proof on 2026-08-14 (WO-988 fixes it).

---

## 6. WO-910 — 31 dead talent nodes

**Blocked:** ranger/mage talent consumers.

`TalentStrategyRegression.cs:235+` keeps all 31 dead-node ids intact; `HeroTalentModifiers.cs:373-375`
exposes stats with **no consumer**.

**The questions:**
1. Approve the cheap stat spine first?
2. Which **single connected column per tree** becomes the traversable spine to a tier-4 capstone?
3. Do the 5 `unlockAbility` stubs become their own tickets, or get hidden?

---

## 7. WO-993 — does a faucet Echo bind to a node?

**Blocked:** the harvest tick design (the retirement work itself is unblocked).

Following the descope (*"we are not doing the animation, faucet only"*), removing movement removes the
**trigger**, not the banking. The Manage picker assigns a **resource**, not a node — which points at "no
node binding" — **but that is an inference, not a ruling.**

Constraint either way: banking must keep going through `MineNode.TryAutoExtract`. No second economy path
— that was WO-229's whole condition.

**The question:** does a faucet Echo still bind to a specific MineNode, or credit its assigned resource
with no node at all?

---

## 8. WO-991 — the Healing Caravan: six design forks

**Blocked:** the whole spec (mobile caravan + unlockable heal field for the Tree of Life and nearby troops).

1. **How does it move?** Player-commanded relocation, patrol between set points, or follow-the-hero?
2. **Can it heal while moving, or only parked?** This decides whether slowness is a *repositioning* cost
   or an *uptime* cost — very different balance levers.
3. **How slow is "very slow"** relative to hero walk? A comparison, not a number.
4. **What unlocks the heal field?** Perk, building tier, Echo, research line? (The arcane-wellspring perk
   already gates the caravan itself.)
5. **Does it heal structures, troops, or both?** Decides whether it reads as a repair vehicle or a medic.
6. **Does mobility interact with the placement grid?** A moving structure that claims grid cells is a
   different problem from one that does not.

---

## 9. WO-986 — thin-structure footprints

**Blocked:** filed as SPEC; deliberately not implemented.

`PlacementGrid.cs:235-238` computes one scalar and returns `new Vector2Int(cells, cells)` — it **squares**
the claim. `StructureFactory.cs:693` collapses the mesh with `Max(size.x, size.z)`, discarding depth.
Walls were fixed by feeding a different **metric**, not by fixing the squaring — so every other thin
structure still over-claims on its narrow axis.

A real fix threads a non-square `(x,z)` through the grid, occupancy, the yaw-inflation path, **and every
saved layout's occupancy replay** — i.e. it touches every placeable structure and existing player saves.

**The question:** does the over-claim hurt in play anywhere other than walls? If only walls, WO-972
already bought the whole benefit and this should stay unimplemented on purpose.

---

## 10. VFX cluster — WO-870 / 874 / 875 / 884 / 885 / 887

**Blocked:** five tickets, all on owner input rather than engineering.

- **870** — `ArcaneTower.cs:83-86` holds `BoltCastVfx` / `BoltImpactExtraVfx` **empty**: no Aether art has
  been tagged. *(Per standing rule, VFX keys are the owner's to tag; CLI maps them verbatim and never
  substitutes.)*
- **875** — **a directive reversal, not engineering debt.** `HeroAbilities.cs:1887`
  `RegistryOnlyMotionVfx = true` is still gating. Someone must rule whether that directive stands.
- **884 / 885** — the **VFX facade** (`Vfx.On(...)`) was specified and never built; every consumer it
  existed to serve has since shipped around it (`TowerCombat.cs:376` calls `VFXManager.Play` directly;
  repo-wide **0 hits** for `Vfx.On(`). 885's "LOCKED contract" is therefore **void in practice**.
  **The question: is the facade still wanted, or do we retire the clause?**
- **874** — elite VFX: wire or kill.
- **887** — the surface taxonomy: no `SurfaceType` / `HitSurface` enum exists.

---

## 11. Flag flips — WO-512, WO-369, WO-731

Each is code-complete behind a flag that is off.

- **WO-512 lock-on camera** — `FeatureFlags.cs:369` `lockon` default **OFF**. Needs a nausea felt-test,
  plus a ruling on whether auto-lock-on-engage stays removed.
- **WO-369 / WO-731** — `ff.arena` (`FeatureFlags.cs:33`) and `ff.colosseum` (`:736`): flip on, or retire.
  WO-731 also needs PO sign-off and re-scopes to roughly a 2-hour close.

---

## 12. Platform / cost calls — WO-767, WO-768, WO-755

- **WO-767 texture caps** — one dict edit in `TextureShrinkAudit.cs`, then Apply + rebuild. The
  *decision* is fidelity vs download size.
- **WO-768 thin-client streaming** — `AddressableAssetSettings.asset:20 m_BuildRemoteCatalog: 0`, still a
  fat client. Multi-week plus CDN spend. Commit to Phase 0/1, or take the AAB + Play Asset Delivery
  interim.
- **WO-755 §8 pack pricing** — blocks the pack catalog, and transitively **WO-756** (sales banner and
  campaign tooling, which is scene-free owner tooling that would let you run a sale without an engineer).

---

## 13. Board / process calls

- **WO-937** — 16 rows say `PARTIAL —` / `UNVERIFIED —`, which are absent from the board's `bucket_of`.
  Add them as canonical keywords, or reword the 16 status lines?
- **WO-1018** — confirm the watermark stays canonical over `acked` (the library kept it deliberately for
  back-compat).
- **WO-817** — its banner **forbids the code its own oracle now requires**
  (`HudKitController.cs:1765-1782` IS the door; `ObsidianQueueRegression.cs:297-316` was inverted to
  require it). A doc correction, but it needs the owner's word because the banner records a prior ruling.
- **WO-438 / WO-440** — each number holds **multiple genuinely different specs** that all landed
  separately. The board cannot represent that. Renumbering is an owner call.
- **⚠ Status-line format** — **212 of 458 READY work orders** write `**Status: READY…**` with the colon
  *inside* the bold markers. `BOARD.html` handles both; ad-hoc greps do not, and under-reported all day.
  Worth standardising the format so the two can never disagree again.

---

## 14. Scope calls on old tickets

- **WO-353–357** — these specify `env(safe-area-inset-*)`, WCAG AA, 44×44 px targets, `<600px`
  breakpoints: **web/CSS idioms**. The batch reads as auto-generated against a web app. Bulk-close, or
  rewrite against Unity?
- **WO-355** — is **portrait** still a target orientation? The Build HUD is authored landscape-only
  (2670×1200). A "no" makes this OBSOLETE.
- **WO-595** — `Resources/Data/dungeon-kit.json` is authored, ships in every build, and is **read by zero
  code**; RoomForge won instead. Is the KayKit kit dead or deferred?
- **WO-362** — the WO-783 smart-composition authority ruling.
- **WO-587** — the SP-linkage question (default = workforce-only).
- **WO-837** — silo vs "Quarry" naming.
- **WO-800 / WO-806** — 800: do defense towers route into the unified focus panel, plus the copy matrix.
  806: acceptance #1 is literally an owner signature on a mock that was never produced; **all** the
  engineering is already in HEAD.
- **WO-765** — needs an owner **placement session** (lay out the default town), not a decision.

---

## 15. Defects found incidentally — need an owner only for prioritisation

These are not decisions; they are real defects surfaced by the sweep that currently have no ticket owner.

1. **Action bar shows FOUR faces, canon says SIX.** Observed in a live screenshot: `Build · Bag · Quests ·
   Manage`. **Talk and Raids are missing.** Raids is the entry to the whole CoC offense loop. Unknown
   whether they are dropped or flag-gated.
2. **HUD clips on both screen edges** at the observed aspect — settings gear sliced at left, `Resources`
   chip cut at right, hero vitals truncated to `s... 144`. Matches WO-779's finding that
   `BossHealthBar.cs:180` and `BuildPlaceButton.cs:59` still hardcode `(1920f, 1080f)`.
3. **`armor.json` dual-copy divergence** — Resources **v2 / 24 entries**, StreamingAssets **v1 / 30**.
   Resources wins in editor, so this is invisible there; **Android and WebGL read a different roster.**
4. **Shield mis-seats when porting from a dungeon** (owner-reported, recurring — "still the same
   problem"). Under diagnosis.
5. **Seven classes ship in every build and are never instantiated** (WO-992). Owner dispositions given
   for most; `TorchFireController` and `AuraController` are recommended DELETE with evidence.
6. **Two orphaned data sets ship and are read by no code**: `Resources/Data/dungeon-kit.json` and the four
   JSONs under `Resources/Data/Dungeons/`.

---

## 16. From the WO-1–299 band (65 verified; 46 closeable)

### ⚠ 16.1 WO-271 — ACT ON THIS ONE. Its acceptance criterion is now BACKWARDS.

WO-271 asks that dialogue stop overlapping the HUD. `DialogueView.cs:190-197` now deliberately sits at
**sorting order 4800, ABOVE the 4000 HUD band** — placed there by an owner-verified F8 fix.

**Anyone implementing WO-271 as written would re-break a fix you already approved.** It needs an
explicit close, not a quiet one, so the inversion is recorded.

### 16.2 WO-110 — its acceptance contradicts a later ruling

WO-110's acceptance says *"Repair restores HP after breach."* That contradicts the WO-753 ruling
enforced at `WallSegment.cs:468` (destroyed = build fresh at full cost, no repair path). **Must be
re-scoped before anyone builds it.**

### 16.3 WO-144 — the ticket's own fork blocks it

Crystal grade is **classification-only** today. Persist per-grade crystals (`CrystalLedger`, a SaveSchema
round-trip, `AddCrystals(int, CrystalGrade)`) — which is a schema bump — or keep it session-only?

### 16.4 WO-112 — the wards

`TryBeginRelight` has **zero callers**, so a ward can never be lit; visuals are disabled at
`WardStone.cs:148`; the HUD sinks are empty; and the wards were sited in **deleted OuterWorld space**.
⚠ It also gates WO-115 (offline harvest), which currently accrues from workers/settlements/pets instead.

**Still wanted on `Main_Castle_Overworld`, or close both?**

### 16.5 WO-182 — the Avalon purge is a migration, not a find-and-replace

**242 hits across 106 files.** Prose is safe to change. But the `"avalon"` **id** in
`realm-map.json:22` is **load-bearing for saves and three regression suites** — renaming it is a data
migration with the same shape as WO-989's `tower_wall_wizard` rename.

**Confirm: prose-only?** (Recommended. The id can stay ugly and correct.)

### 16.6 WO-280 B7 — does levelling read as getting stronger?

Levelling grants **Wisdom / skill points, not flat stats**, by design. Confirm that reads as "getting
stronger" to a player, or B7 stays open.

### 16.7 WO-121 / WO-129 — both spend Unity time against a backend that was never deployed

`LeaderboardService.cs:6-10` says so directly. WO-121 needs 6 instrumentation events (only
`session_start` fires today); WO-129 needs a profile/username service, social link, and a real
`RemoteLeaderboardSource` (there is only a local stub). **Worth doing before the backend exists?**

### 16.8 Two method catches worth generalising

- **WO-190's own status line asserted its FBX was never imported.** It is on disk
  (`Assets/Resources/Enemies/Orc_Necromancer.fbx`) and wired in four places. A status-line read would
  have kept a finished ticket open.
- **WO-279's fixes genuinely were never applied — and the verdict is still OBSOLETE**, because a GUID
  check proved the script is attached to nothing. A grep read would have kept dead work alive.

**Both would have gone the other way on a status-line or name-grep read.** This is the strongest
argument for the GUID method and for treating every status line as a claim.

---

## Appendix — what "verified" means in this document

Every band from 300 to 1099 was swept by read-only agents under one rule: **a commit message saying
"WO-123 done" is a claim, not evidence** — open the implementation file or return UNVERIFIED.

Headline result: **in the 900+ band there were ZERO phantoms** — every named defect site is still
literally present in HEAD. Below 800 the picture inverts: of 78 tickets verified in the first pass, only
**two** were genuinely still ready. The rest were finished long ago, superseded, or partially shipped.

That is the empirical basis for the owner's instinct that *"anything before 600 would be very old"* and
*"i wouldnt rely on anything before 800 with much truth."* The data agrees with her.

⚠ **Coverage caveat:** the 300–599 band's agent filed two contradictory reports — one claiming full
coverage, one admitting roughly 30 tickets were never verified because six of its own sub-agents failed
to launch. **The conservative report is the one being trusted.** Those ~30 need a re-run.
