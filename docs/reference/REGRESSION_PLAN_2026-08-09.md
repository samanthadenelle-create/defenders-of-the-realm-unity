# REGRESSION PLAN - 2026-08-09

**Status: KNOWN DICTIONARY (durable registry, not a one-off report).** Companion to
`docs/reference/AUDIT_2026-08-09.md`, which is the finding registry; this is the *closure* registry.
It **supersedes and expands** that audit's §6 "Proposed oracles, in leverage order" list.

**Scope:** what to build, in what order, and what each thing can honestly claim. It authors no code.

**Method / provenance.** Every assertion below was read at source in this session against the working
tree at branch `wip/village2-and-f8-tickets`. Files opened: `AUDIT_2026-08-09.md` (full),
`Assets/Editor/Regression/VfxResourceSelfContainmentRegression.cs` (full), `DataWebRegression.cs`
(full), `RegressionMarkerRegression.cs` (full), `UiMvvmConformanceRegression.cs` (full),
`DataRegression.cs` (`:240-284`, `:285-665`, `:1268-1280`), `HudUiRegression.cs` (`:85-250`,
`:362-456`, `:519-633`), `SessionRegression.cs` (`:60-80`), `CombatAtbRegression.cs` (`:515-560`),
`SceneRoutingRegression.cs` (grep), `docs/INSTRUMENTATION_STANDARD.md` (`:150-240`),
`docs/reference/REGRESSION_COVERAGE_MATRIX.md` (`:1-205`), plus disk sweeps of
`Assets/Resources/Data/Canonical/`, `Assets/StreamingAssets/Data/Canonical/`, `Assets/Resources/VFX/`
and `Builds/ui-capture/`. **Where I could not open a thing at source I say so in the row.**

**Not read on purpose:** `RaidDeployController.cs`, `RaidScoring.cs`, `RaidGarrisonSpawner.cs`,
`TroopFactory.cs`, `SceneRouter.cs`, `scene-configs.json`, `troops.json`. Another session is mid-edit
in this shared tree; any line number I quoted from them would be stale before it was read. **This is
why F39 (no raid probe) is deliberately parked in §6 rather than specified here.**

⚠ **Counts in `docs/reference/REGRESSION_COVERAGE_MATRIX.md` are stale and must never be quoted.** Its
per-finding PROPOSED-ASSERTIONS column is still canonical and is reused below by row id (ECON-*, CS-*,
EW-*, BLIND-*). Where a matrix row already names the right assertion, this plan points at it rather
than reinventing it.

---

## 1. THE GOVERNING PRINCIPLE - convert EXISTENCE assertions into CONSUMPTION assertions

The audit's §0 "THE PATTERN" is the spine of this plan, so it is restated as the design rule every
oracle below is measured against:

> **Every gate audited asserts that a thing EXISTS. Almost none assert it is CONSUMED at the site that
> needs it.** Canon named the same shape independently three days earlier, about a different system:
> *"the gate proves the part that was never broken."*

An existence assertion answers *"is the part in the box?"*. A consumption assertion answers *"is the
part bolted to the thing that turns?"*. The second question is strictly stronger and it is the one the
defects in the audit actually live under:

| The gate proves the part exists | The consumption question it never asks |
|---|---|
| `RarityGlyph` renders in the shipped font (`HudUiRegression.cs:427-448`) | which panels **call** it - `EquipmentPanel` does not (F33) |
| `SafeAreaInset` places a corner correctly (`HudUiRegression.cs:551-625`) | which mounts **apply** it - 9 HUD zones do not (F74) |
| `DifficultyMath.*` computes five multipliers | which systems **read** them - 3 of 5 have no reader (F17) |
| Resources/VFX **prefabs** are self-contained (`VfxResourceSelfContainmentRegression.cs:205`) | whether the **catalogs** that resolve them are (F1/F2/F38) |
| A `version` field is **present** and cross-copy equal (`DataWebRegression.cs:352-398`) | whether a content change **moves** it (F22) |
| A View **references** a ViewModel (`UiMvvmConformanceRegression.cs:190-198`) | whether it **stopped** reading state - the file-level limitation it documents at `:25-29` |

**Why this retires a class rather than N symptoms.** `RarityGlyph` is the proof: a font-verified,
regression-pinned, ASCII, colourblind-safe rarity channel existed the entire time and the one panel
that needed it never called it. **No fix was ever required - only a gate that asks "who consumes
this?"** Fix F33 alone and the next panel built repeats it. Add a consumption assertion and every
future panel is covered by construction. The same inversion is what makes O1 close three findings for
one edit and O5 close four.

**Operational form of the rule, for anyone authoring from this document.** Per
`INSTRUMENTATION_STANDARD.md:178` - *real object in -> assert real response -> one marker* - a
consumption assertion is written as one of exactly three shapes:

1. **Producer-to-consumer join.** Enumerate producers from the artefact that owns them (a catalog, a
   public API surface, an enum), then require >= 1 non-oracle reader for each. Editor/regression code
   must be excluded from the reader set or the oracle proves itself. (Closes F17, F32, F61.)
2. **Site coverage.** Enumerate the sites that need the behaviour (all nine `HudAreasHost.Add` calls,
   all pooled-VFX `SetParent` owners, all synthesized stat builders) and require each one to route
   through the single authority. (Closes F74a, F11/F12/F36, F18/F19.)
3. **Change-implies-effect ratchet.** Store a derived fingerprint of the input; require a declared
   output to move when the fingerprint moves. (Closes F22.)

Anything that cannot be written as one of those three is an existence assertion wearing a costume.

---

## 2. LEVERAGE-ORDERED ORACLE TABLE

**Ordering is findings-closed-per-unit-effort, NOT severity.** Effort scale, stated so the ranking is
auditable:

- **S** - an edit inside an existing suite; roughly < 100 lines; no new registration, no new marker.
- **M** - a new suite or a substantial rework; new marker + a `DataRegression.RunAll` registration line
  above the END FENCE (`DataRegression.cs:611`); 100-300 lines.
- **L** - new harness capability that does not exist in the repo at all (a runner, a capture target, a
  play-mode arm). Carries its own marker and its own gate stage.

| # | Tag | Closes | The assertion, in one sentence | File | Size |
|---|---|---|---|---|---|
| **O1** | `[vfx-self-contained]` | **F1, F2, F38** | Every **asset** under `Assets/Resources/VFX/` - ScriptableObject catalogs included, not only prefabs - has zero recursive dependencies resolving into a gitignored art root. | `VfxResourceSelfContainmentRegression.cs` | **S** |
| **O2** | `[regression-marker]` + verdict | **G1** *(and the denominator of all 130)* | The count of registration call-sites parsed out of `RunAll`'s body between the two fences EQUALS the runtime `suitesTotal`, so a suite that throws inside `Guard.Try` cannot silently leave the denominator. | `RegressionMarkerRegression.cs` + `DataRegression.cs:613-615` | **S** |
| **O3** | `[data-web]` | **F22, F23** *(surfaces F61)* | Drift and version checks iterate the **union** of both canonical roots, and a catalog whose normalized content hash moved since the pinned baseline must also have moved its `version`. | `DataWebRegression.cs` | **S/M** |
| **O4** | `[enemy-stat-divergence]` | **F18, F19, F46, F47** | **Every** synthesized enemy-stat builder in the tree resolves the same id to the same `{hp,dmg,xp,name}` as `enemies.json`, and a builder that fails to resolve by reflection FAILS instead of skipping. | `CombatAtbRegression.cs:524` (widen) | **M** |
| **O5** | `[money-surface-gate]` | **F10, F73** *(hardens F8, F9)* | Every source site that reaches a payment provider or renders a real-money price CTA references a `FeatureFlags` gate in the same file. | new suite | **S/M** |
| **O6** | `[feature-flag-defaults]` | **F14, F60** *(+ the doc-lie class incl. F78's shape)* | Under cleared PlayerPrefs every `FeatureFlags` getter returns its declared `defaultOn`, **and** each getter's own doc-summary prose ("Default OFF" / "ships OFF" / "ON") agrees with that value. | new suite (matrix **BLIND-4-F3**) | **M** |
| **O7** | `[vfx-parent-lint]` | **F11, F12, F36** | No pooled-VFX return path calls `SetParent` without the `activeInHierarchy` guard its own twin already carries at `VFXManager.Hovl.cs:397-398`. | new suite | **M** |
| **O8** | `[api-contract]` | **F5, F6, F7, F26, F64, F65, F66** | Every handler under `api/**` exports `config` **before** `module.exports`, answers only 200/400/500, and refuses an unauthenticated grant/claim. | new harness, outside Unity | **L** |
| **O9** | `[asset-identity]` | **F41** | `structures-catalog.json`'s `visualPrefabPath` resolves to a **`.prefab`**, not merely to non-null - no id may have both a `.fbx` and a same-stem `.prefab` under `Resources/Structures/`. | `DataRegression.cs:1273` (extend) | **S** |
| **O10** | `[uxml-scene-fence]` | **F3, F4, F37** | No scene enabled in `EditorBuildSettings` carries an **enabled** `UIDocument` with a non-zero `sourceAsset` unless its owning controller has a runtime-disable call. ⚠ **BLOCKED - see §6.** | new suite (idiom: `SceneRoutingRegression.cs:65`, `:217`) | **M** |
| **O11** | `[hud-safe-area]` | **F74(a) only** | Every one of the nine `HudAreasHost` zone mounts routes through a stretch-mode `SafeAreaInset` applier, table-verified against the same cutout cases `HudUiRegression.cs:541-549` already uses. | `HudUiRegression.cs` (new CHECK 7) | **M** |
| **O12** | `[catalog-key-resolves]` | **F58** | Every `VillageStrings` key referenced in code **resolves** in `en.json` - non-empty `displayName` is not enough (`DataRegression.cs:1693` is the hole that let `763d1a60` ship). | `DataRegression.cs` (extend) | **S** |
| **O13** | `[roomforge-dualcopy]` | **F24** | The dual-copy sweep enumerates **every** `dg_*` layout on disk instead of the hardcoded 3-file list at `RoomForgeRegression.cs:162`. | `RoomForgeRegression.cs` | **S** |
| **O14** | `[ad-covenant]` | **F28** | `Walk()` tests a `JProperty`'s **Name** as well as its Value, so `{"economy":{"crystals":700}}` - the sibling files' own shape - trips `[no-premium-grant]`. | `AdPlacementCovenantRegression.cs:302-327` | **S** |
| **O15** | `[audio-mixer-exposed]` | **F34** | Every parameter `AudioService` calls `SetFloat` on is present in `GameAudioMixer.mixer`'s `m_ExposedParameters`, and no `SetFloat` return is discarded. | new suite | **S** |
| **O16** | `[dead-catalog]` | **F61** | Every catalog in `DataWebRegression`'s pin lists (`:121-129`) has >= 1 reader **outside** `Assets/Editor/`. A gate mirroring a file nothing loads is mirroring debt. | `DataWebRegression.cs` | **S** |
| **O17** | `[skr-usd-parity]` | **F29** | The SKR and USD rails price the same bundle within a declared tolerance; today they diverge 2.876x and `MonetizationCovenantRegression.cs:93-100` loads both files without comparing. | `MonetizationCovenantRegression.cs` | **S** |
| **O18** | `[accessory-persist]` | **F13** | Seed **every** `PersistedState` property non-default and assert it survives `Save`/`Load` - which subsumes the two-field `equippedRingId`/`equippedAmuletId` hole rather than pinning it. | matrix **CS-3** | **M** |
| **O19** | `[hud-touch-band]` | **F15 (measurement only)** | The action-bar face width derived from the live `HudAreasHost` zone and `BarSlotW` is recorded and may never DECREASE - it pins 78.66 px so it cannot silently get worse. **This is not a fix.** | `HudActionBarRegression.cs` | **S** |
| **O20** | `[hudkit-capture]` | **F76** *(precondition for F15/F74b)* | The capture harness renders at least one **persistent HUD** target; today all 47 PNGs are modal panels across 15 stems and `HudKit` appears nowhere in `UICaptureLaunch.cs`. | `UICaptureLaunch.cs` + harness | **L** |
| **O21** | `[f8-inbox-freshness]` | **F77** | `ACK.json`'s `lastAckSeq` is within N of `PING.json`'s seq, and the capture backlog is under a declared ceiling. | new suite | **S** |
| **O22** | `[echo-lane-consumers]` | **F32** | Each `EchoLaneBonuses` field has >= 1 production reader outside `Assets/Editor/` - the same producer-to-consumer join as O4 and matrix **ECHO-1**. | `EchoSpecializationRegression.cs` | **S** |

**Not in this table, deliberately:** F16, F40, F42, F43, F48, F49, F53, F54, F69, F70 and most P2/P3
behavioural rows. Per audit §3b they are **behavioural inference from source - CANDIDATE ONLY, not an
RCA**. Writing an oracle against an undiagnosed candidate pins the guess, not the defect. Those earn
instrumentation first (§12), then an oracle against the captured proving line.

---

## 3. THE TOP THREE - verified at source, with the trap in each

The audit nominates F2, F23 and G1 as the three highest-leverage changes because each blinds a whole
class. **I verified all three at source. All three stand. Two of the three carry a framing correction.**

### O1 - `[vfx-self-contained]`: prefabs only -> every asset (closes F1, F2, F38)

**Verified.** `VfxPrefabPaths()` (`VfxResourceSelfContainmentRegression.cs:155-167`) queries
`AssetDatabase.FindAssets("t:Prefab", new[] { VfxRoot.TrimEnd('/') })` and filters to
`.EndsWith(".prefab")`. `RunCore` iterates exactly that list at `:205`. The two ScriptableObject
catalogs are on disk inside that same root - `Assets/Resources/VFX/HovlVfxCatalog.asset` and
`Assets/Resources/VFX/VFXCatalog.asset` - and carry **110** and **68** distinct GUID references
respectively. `Assets/Hovl Studio/` exists on this machine and is gitignored at `.gitignore:272`
(the suite's own `GitignoredArtRoots` cites `:218` for the same root - a line-number drift in a comment,
not a functional defect, since the match is by path prefix).

**The assertion.** Rename `VfxPrefabPaths()` -> `VfxAssetPaths()` and have it return prefabs **and**
ScriptableObjects under `VfxRoot`. `PackDependenciesOf` (`:136-152`) needs no change at all - it is
already asset-path-generic and `AssetDatabase.GetDependencies(path, true)` follows a ScriptableObject's
object references the same way it follows a prefab's.

**The trap, and it is the reason this row is not a one-liner.** The suite's existing empty-set branch
(`:206-216`) is exemplary - *"NOT a hollow pass: finding no prefabs is itself the failure"*. **The
extension must carry the same discipline or it inherits the G1 shape.** If `FindAssets("t:ScriptableObject", ...)`
returns nothing - a plausible outcome, because `t:` type filtering behaviour on ScriptableObject
subclasses is not something I can verify without running the editor - the suite would iterate the same
prefab set it always did and report green with a slightly different message. **Therefore: assert by
NAME that both known catalogs are in the returned set**, and fail loudly if either is absent. That
converts a query that silently under-matches into a red. Enumerating `*.asset` off disk under
`VfxRoot` is the more robust alternative and is preferred if the `t:` query proves flaky.

**Expected verdict on arrival: RED**, naming ~103 leaked references. That is correct and is the point.
The mirror tool `DeNelle.Editor.VfxResourceArtMirror` (which the suite header at `:33-39` names as
calling into these same members rather than re-deriving them) is the fix path; **check whether it also
walks prefabs only before assuming it can remediate the catalogs.** I did not open it.

### O2 - `[regression-marker]`: pin the suite count (closes G1)

**Verified.** `DataRegression.cs:613-615` computes `suitesGreen = CountOracleTagLines(log) - suiteTagLinesBefore`,
`suitesRed = failures.Count - suiteFailuresBefore`, `suitesTotal = suitesGreen + suitesRed`, and
`:637` prints `REGRESSION_OK {suitesGreen}/{suitesTotal} suites`. Both terms are **derived from what
reported**. `Guard.Try` swallows a throw. A suite that throws appends no `[tag]` line and adds no
failure, so it leaves **both** terms and the marker still reads `n/n`. There is no pinned expected
count anywhere in the file. G1 is exactly right.

**The assertion, and this is where the audit's framing needs sharpening.** The audit says "pin an
EXPECTED SUITE COUNT". Taken literally that means a constant - **which is G8's anti-pattern**
(`SESSION_GUARDS_OK 6/6 checks`, verified as a bare literal at `SessionRegression.cs:71`). Do not
write a constant.

**Derive both sides.** `RegressionMarkerRegression` already extracts the brace-matched body of
`RunAll` (`ExtractRunAllBody`, `:420-438`) and already strips comments (`StripLineComments`, `:390-417`).
Add one public helper there that counts registration call-sites **between the START FENCE
(`DataRegression.cs:270`) and the END FENCE (`:611`)**, then have the verdict block compare that
source-derived number against the runtime-derived `suitesTotal` and fail on inequality.

**I measured it.** Non-comment `.Run(out` call-sites between the fences: **130**. The last green
marker read `REGRESSION_OK 130/130 suites`. **Source-derived and runtime-derived agree exactly today,
so this oracle lands GREEN and is safe to write first.** That is the strongest argument for doing it
first: it is the cheapest oracle on the list, it goes green on arrival, and until it exists every other
oracle in this document can vanish from the denominator without anyone noticing.

**Dependency note.** O2 is ranked #2 by ratio but is a **precondition for the trustworthiness of every
other row**. If only one thing is built, build this.

### O3 - `[data-web]`: iterate the union, and ratchet the version (closes F22, F23)

**Verified, and F23 is precisely right but slightly under-stated.** `CheckDualCopyDrift` iterates
`CanonicalJsonFiles(streamingRoot)` at `:208`; `CheckVersionFields` iterates
`CanonicalJsonFiles(streamingRoot)` at `:356`. Both are StreamingAssets-only. `CheckAllParse`
(`:332`) **does** walk both roots, so the parse arm is already union-shaped - the audit did not claim
otherwise, but a reader skimming F23 could over-generalize. The subdirectory-mirror sweep
(`:311`) is StreamingAssets-only by design and correctly so; it asserts a mirror direction.

**I measured the exposure.** 80 canonical `.json` files on each side. **Resources-only: exactly three**
- `ad-creatives.json`, `ad-placements.json`, `widget-params.json`. StreamingAssets-only: three -
`battle_monthly_packs.sample.json`, `skr_staking.json`, `skr_store.json`, all three already excluded by
`IsNonDualCopyByDesign` (`:101-105`). So union iteration adds exactly **three** files to the version
check and nothing to the drift check (a Resources-only file has no twin to drift against). The audit's
named example, `widget-params.json`, is confirmed: it exists only at
`Assets/Resources/Data/Canonical/widget-params.json` and carries no `version` field, and it is
invisible to the gate today. **The red on arrival is three rows, which is tractable in one sitting.**

Note the interaction with **O16 / F61**: `ad-creatives.json` is one of the three, and F61 records it as
having zero readers in any `.cs`. Do not add a `version` field to a dead catalog reflexively - run O16
first and delete or de-pin it if it is genuinely dead.

**The version-bump arm (F22) is the harder half and needs its own design.** "A change bumps it" is not
decidable from the file alone - it needs a stored baseline. Two shapes are possible:

- **(a) In-repo hash baseline (recommended).** A committed JSON of `{file: {hash, version}}` using the
  suite's existing `Normalize()` (`:569-574`, BOM-strip + CRLF->LF) so the hash is EOL-agnostic. On
  each run: if `hash` moved and `version` did not, FAIL. Same ratchet discipline as
  `RegressionMarkerRegression`'s `KnownHollowPassFiles`. **Cost: the baseline file must be updated in
  the same commit as any legitimate catalog change**, which is exactly the behaviour F22 wants to force.
- **(b) Git-history derivation.** Compare the working copy against `HEAD~1`. Rejected: it makes the
  suite depend on git state, it breaks in a clean checkout and on a squash, and it cannot run in the
  same batch as the change it is guarding.

**Trap in (a):** the baseline must key on the **normalized** hash, not the raw bytes, or every CRLF
round-trip through a Windows editor reads as a content change and the gate cries wolf until someone
allowlists it into uselessness. The suite already has the exact normalizer for this at `:569`.

---

## 4. WHAT CANNOT BE GATED HEADLESSLY - and what proves it instead

`INSTRUMENTATION_STANDARD.md:197-202` draws the line: **headless `DataRegression` owns anything
decidable from data + logic** (catalog mapping, capability composition, service resolution, save
round-trip, pricing); **play-mode / owner F8 owns anything needing the running scene, physics,
rendering, input, or subjective judgment.** Audit §3b adds the harder constraint: **exactly one finding
in the entire document (F75) is pixel-verified.** No oracle on this list can promote a static or
structural finding to a proven one.

| Finding | Why no headless oracle can ever close it | The alternative proof |
|---|---|---|
| **F15** - action-bar face 78.66 px vs a 112 px floor | The arithmetic over authored constants is sound (audit §3b classes it as such), but *whether a 5 mm target is mistappable in a row of six* is a **subjective/input judgment**. `Screen.*` never moves in batchmode - confessed at `UICaptureLaunch.cs:543-547`. | **O19 pins the measurement, not the verdict.** A device shot at the real DPI plus owner felt-test decides. Note the audit's own ruling: this is a **DECISION, not a task** - the 6-face bar cannot satisfy 112 px inside its current zone by any lever, and WO-911's 7->6 already *improved* it by 12 px. **Do not "fix" it from this document.** |
| **F74(b)** - "...therefore the HUD renders under the gesture bar" | Rendering class. Batchmode has no device cutout and `Screen.safeArea` is inert. | **A device screencap.** Nothing else. **O11 closes F74(a) only** - "the nine zones make no safe-area call" - which is a structural absence and is legitimately headless. Per audit §3b, a RESULT file that conflates (a) with (b) is the failure. |
| **F33** - colour-only meaning (build ghost, health bar, rarity, crit floaters, the dungeon `.uss` green/amber pairs) | Colour perception is subjective and is item 7 on the audit's "what a headless gate structurally cannot see" list. | **Partly headless after all, and this is the leverage:** the *rarity* row is not a perception question, it is a **consumption** question - `ElarionUiKit.RarityGlyph` exists, is pinned at `HudUiRegression.cs:427-448`, and `HeroInventoryController` already calls it while `EquipmentPanel.cs:381` does not. A producer-to-consumer join over `RarityGlyph` callers is decidable from source and closes the rarity row. **The rest (hue-only ghost, unlabelled health bar, `.uss` state classes) needs eyes.** |
| **F75** - `DailyQuestHud` mis-anchored body panel | Layout defect found by opening a PNG. `UI_CAPTURE_OK` proves a panel *rendered*, never that it *looks right*. | Already the one pixel-verified finding. It needs **instrumentation, not an oracle** - audit §3b explicitly says the cause is undiagnosed and per §12 it earns a trace, not a guess. An oracle written now would pin a symptom. |
| **F76** - zero persistent-HUD captures | Not a limit of headless gating; a **missing target**. Verified: 47 PNGs across 15 distinct panel stems, all modal; `HudKit` does not appear in `UICaptureLaunch.cs`. | **O20 - a HudKit capture target.** This is the precondition for ever obtaining pixels on F15 and F74(b). Until it exists, the pixels that would confirm or kill those two findings **do not exist**. |
| **Class-wide** | Orientation / world transform (nothing in the 130 reads an up-axis - the `70a86c17` class), skinned-mesh pose (the run itself logs `pose-verify skipped (headless/culled)` for five enemy models), coroutine presentation, and materials resolvable only on this machine. | The AutoPilot fleet / play-mode arm, and for the last one, **O1** - which is precisely why extending it matters. |

**One structural consequence worth stating plainly.** G4 records that **PlayMode tests have never
produced an artifact** - no `tests-PlayMode.xml` exists anywhere, ever, and the 6 `[UnityTest]` PlayMode
tests are entirely unevidenced. So "move it to play-mode" is not currently a real destination. **Any
row above whose alternative proof is "play-mode" is, today, a row whose alternative proof is "the
owner's eyes."** Building a play-mode arm that emits an artifact is itself an L-sized prerequisite and
is not costed in this plan.

---

## 5. ANTI-PATTERNS - every one of these is a trap THIS harness already fell into

Cited from the audit's own gate findings, each verified at source in this session. An oracle that
reproduces any of these is worse than no oracle, because it reads as proof.

1. **A hardcoded count in a marker string.** `SessionRegression.cs:71` prints
   `SESSION_GUARDS_OK 6/6 checks` as a bare literal (verified). Six void `Check*` methods run above it;
   if one silently no-ops, the marker still says 6/6. **Rule: a number in a marker is DERIVED from what
   ran, or it is not in the marker.** This is the exact anti-pattern O2 must not become.

2. **Tautological substring checks that do not strip comments.** G9: `DungeonExitReachableRegression.cs:26-48`
   asserts a substring exists in a `.cs` file without stripping comments, so a commented-out or
   dead-branch call satisfies it. `DungeonReturnSceneRegression.cs:31-38` *does* strip and is the
   pattern to copy; `RegressionMarkerRegression.StripLineComments` (`:390-417`) is the in-repo
   implementation - **call it, do not write a second one.**

3. **A hollow-pass shape the ratchet cannot see.** G5: `FindHollowPassLines`
   (`RegressionMarkerRegression.cs:468-489`) fires only on `== null` / `IsNullOrEmpty` /
   `!File.Exists` / `!Directory.Exists` inside a 4-line window (verified at `:480-486`). The live
   pattern in four suites is a **bool helper** - `if (!InstallState(...)) { reason = "needs fleet"; return true; }`
   - which is invisible to it, so the suite reports "6 baselined / 0 new": a false all-clear.
   **A new oracle must not answer OK out of any guard, in any shape.** If the ratchet cannot see your
   shape, that is not permission.

4. **A suite that catches its own exception and returns true.** G6: `HudUiRegression.cs:228-234`
   (verified) catches everything from all six CHECKs and returns `true` with a `Debug.LogWarning`,
   under the rationale *"a broken oracle must never masquerade as a broken game"*. That rationale is
   defensible for a diagnostic and indefensible for a gate: one throw in CHECK 1 silently discards
   CHECKs 2-6 and the marker stays green. **Note the shape difference from G1** - this one still
   appends its `[hud-ui-sme]` tag line, so the *denominator* is unaffected; it is the *content* that
   evaporates. O2 does not catch it. `VfxResourceSelfContainmentRegression.Run` (`:182-194`) shows the
   correct shape: catch, log `*_FAIL` via `Debug.LogError`, **return false.**

5. **A skip-on-reflection-miss.** Not in the audit's G list; found this session and it belongs here.
   `CombatAtbRegression.cs:555-558`: if either synthesized-stat source fails to resolve by reflection,
   the suite logs `"could not resolve both sources - skipped"` and **returns without a failure**. A
   rename turns it green-and-empty - audit §0 item 5, made concrete. **O4 must FAIL on a reflection
   miss, not skip.**

6. **A baseline entry that can never resolve.** G10: the Obsidian baseline pins `PauseHudBootstrap.cs`,
   annotated in-tree as a dead never-instantiated class, so it permanently inflates the "15 tracked"
   figure. **A ratchet whose baseline contains unresolvable entries reports progress it has not made.**

7. **An allowlist that guts the gate it protects - the UXML fence trap (audit §3a).** The audit is
   right that `HudUiRegression`'s `UiDocumentBaseline` **sanctions** the offenders rather than merely
   missing them. **Two corrections to the framing, both material to O10:**
   - **It permits 22 surfaces, not 12** (counted at `:99-123`). The audit names 12; the other ten are
     `ArenaDefensePaletteUI`, `TowerUpgradeButton`, `LevelUpSkillPopupBootstrap`, `LevelUpSkillPopup`,
     `WelcomeBackPopup`, `TalentTreePanel`, `SeatingEditorOverlay`, `TowerPlacementRotateMenu`,
     `TutorialHudOverlay`, `PetIntroduction`.
   - **The fence bans the wrong thing.** `CheckUiDocumentFence` (`:366-404`) matches
     `UiDocumentSmells` (`:195-200`): `AddComponent<UIDocument>`, `new GameObject(...typeof(UIDocument))`,
     `RequireComponent(typeof(UIDocument))`. It fences **UIDocument CONSTRUCTION**. But the failure
     canon documents is **UXML SOURCING** - and `PetSelectController.cs:9-11` says so in as many
     words: a code-built UIDocument on `rootVisualElement` is the pattern that *works*. So the
     baseline lumps the safe shape and the broken shape into one allowlist and cannot distinguish
     them. **O10 must fence on a non-zero `sourceAsset` / `visualTreeAsset` binding, not on UIDocument
     construction**, or it inherits the same conflation at scene level.
   - The audit's recommended shape is right and should be kept: **fail only where an enabled
     `UIDocument` has a non-zero `sourceAsset` AND its owning controller has no runtime-disable call;
     allowlist exactly two, each with a falsifiable reason.** A blanket allowlist of all four red
     scenes guts the gate's entire value, which is distinguishing "serialized but killed at runtime"
     (Title, HeroSelect) from "serialized and LIVE" (PetSelect, HealersCottage).

8. **Latent, not yet fired, but load-bearing: the tag counter is newline-sensitive.**
   `CountOracleTagLines` (`DataRegression.cs:656-665`) counts every line in the accumulated log that
   begins with `[`. A suite whose `reason` string contains an embedded newline followed by `[` would
   inflate `suitesGreen`. No suite does this today - `HudUiRegression`'s multi-line FAIL reason starts
   its continuation lines with a space (`:246-247`) - so this is a fragility, **not** a live defect, and
   I am labelling it as such rather than as a finding. **O2 makes it detectable** the moment it fires,
   because the source-derived count would no longer match.

---

## 6. SEQUENCING - what must NOT be written yet, and why

**O10 `[uxml-scene-fence]` - build the SCAN, do not write the ALLOWLIST.**
The scan itself is unblocked and cheap: the in-repo idiom already exists two files over -
`SceneRoutingRegression.cs:65` iterates `EditorBuildSettings.scenes` and `:217`/`:236` do plain
`File.ReadAllText` over source. A `.unity` is YAML text; joining `*.uxml.meta` GUIDs against enabled
scene files needs **no Editor session and no scene open**, which matters because audit F37's sequencing
note is correct: **scene surgery needs the Unity Editor OPEN while gates and builds require it CLOSED
(`START_HERE.md:32-33`) - those cannot run in one breath.** A text-join scan sidesteps that entirely.

**But its allowlist depends on an unresolved owner decision.** Audit §3a puts `Dungeon_HealersCottage`
(build index 4, two enabled UIDocuments, both binding by UXML element name) at a **three-way owner
choice**: (a) accept the panels are dead in player builds, (b) delete, (c) convert to uGUI. The audit
measures (c) as cheap - `DungeonHudVM.cs` (146 lines) and `DungeonCraftVM.cs` (149 lines) already exist
beside the controllers, total binding surface is 12 elements, and `DungeonToastView` / `LoreReadingModal`
are the uGUI precedent in the same folder. **Each of the three outcomes produces a different allowlist
row**, so any entry written today is stale on arrival, and a stale allowlist entry is anti-pattern #6.
Worse, the tempting shortcut - allowlist all four red scenes to get to green - is anti-pattern #7 and
destroys the gate.

**Also unresolved and upstream of the same file:** the §3a premise is explicitly **UNPROVEN in the
strict sense** - a strong documentary and code-shape inference, not a device capture. **One player-build
entry into Healer's Cottage settles it**, and per §12 that is what it earns: a build check, not a
conclusion.

**Sequence:** land the scan in report-only mode (name every enabled `UIDocument` with a non-zero
`sourceAsset` and whether its controller disables it) -> take the build check -> get the owner ruling ->
*then* write two allowlist rows with falsifiable reasons and flip it to hard-fail. This is the same
seed flow `UiMvvmConformanceRegression.cs:37-40` documents for its own baseline, and it worked there.

**O11 and O19 must be decided together, and neither should be written as a fix.**
Audit §3a-adjacent note under F74: `EdgeMarginPx` (verified as `44f` at `SafeAreaInset.cs:58`) is
applied **on top of** `Screen.safeArea`, and the HUD zones already carry their own fractional insets.
Applying safe area **plus** 44 px double-insets them, and the audit's arithmetic is that the ActionBar
zone drops from 496.8 to 408.8 ref px, taking the face from 78.66 to ~64.7 px - **14 px worse than the
F15 gap.** So O11 as an oracle (assert the zones route through an applier) is safe to write; **O11 as a
fix is coupled to the F15 relayout ruling and cannot be made independently.** O19 pins the current
number so the coupling stays visible. Write both as *measurements*, not as corrections.

Additional blocker for O11 that must be known before starting: **there is no applier to call.**
`SafeAreaInset` exposes exactly one, `ApplyTopRight` (`:112`), which corner-pins a fixed-size box and
would **collapse** a stretch-anchored fraction rect. What exists and should be reused is the pure static
math - `LeftInset`/`RightInset`/`TopInset`/`BottomInset` (verified at `:63-74`) - which is headlessly
assertable with no live `Screen` and is already the basis of the CHECK 5 oracle. Writing O11's assertion
before that applier exists means asserting against a function nobody wrote.

**O8 `[api-contract]` is sequenced by §5 of the audit, not by effort.** Canon calls promoting `api/` to
prod "the single highest-value action on the board"; the audit inverts that - **prod running OLD `api/`
code is currently what protects you**, and F5/F6/F7 are live in the repo copy. So O8's oracle and the
F5/F6/F7 fixes go together, **before** any promotion, and O8 must not be used as a green light.

**F39 (no raid probe in `AutoPilotDriver`) is deliberately absent from §2.** The raid silo is mid-edit
by another session in this shared tree. Specifying assertions against files being rewritten produces
line numbers that are wrong before they are read. **Re-cost F39 once that lane lands and the tree is
quiet.** Note also that audit F71 records the three Raid suites **are** already wired into
`DataRegression.RunAll` (`:315`, `:429`, `:481` - I confirmed `:315` `[raid-scoring]`, `:429`
`[raid-deploy-ui]`, `:481` `[raid-arena-shape]` at source) **despite their own file headers claiming
otherwise** - so anyone picking up F39 should not "fix" a wiring problem that does not exist.

---

## 7. CORRECTIONS TO THE AUDIT

Recorded because the audit is a committed known dictionary and other agents execute from it. Each was
verified at source this session.

| # | Audit says | Verified reality | Does the finding survive? |
|---|---|---|---|
| C1 | §3a: the `.cs` fence "already explicitly permits **12** UIDocument-using surfaces" | `HudUiRegression.cs:99-123` holds **22** entries | **Yes, strengthened.** Ten more sanctioned surfaces than recorded. |
| C2 | §3a / F3: framed as "the fence is a source fence over `.cs`; it never opens a `.unity`" | True, **and** the fence bans **UIDocument construction** (`UiDocumentSmells`, `:195-200`), never UXML **sourcing** - so it cannot distinguish the safe code-built shape (`PetSelectController.cs:9-11`) from the broken UXML-sourced one | **Yes, sharpened.** This changes what O10 must assert. |
| C3 | F18 coverage: "the divergence oracle guards **`WildlandsRoster`** only" | `CheckSynthesizedStatDivergence` (`CombatAtbRegression.cs:524-560`) compares **`RegionMobSpawner.BuildRoamerDef`** and **`GarrisonStatBlocks.BuildTypedDef`**. `WildlandsRoster` is not one of them. | **Yes.** The substance (TribeManager and WardTetherService are not compared, and `enemies.json` is not the reference) is correct; only the named sources are wrong. Matrix **EW-2** already states it correctly: "compares only 2 of 7 sites". |
| C4 | F23: "**Drift + version** checks iterate the StreamingAssets root ONLY" | Correct for drift (`:208`) and version (`:356`). **`CheckAllParse` (`:332`) already walks both roots.** | **Yes.** Stated so nobody widens the parse arm twice. |
| C5 | §6: "Pin an **expected suite count**" | Taken literally this is G8's own anti-pattern. The count must be **derived on both sides** - source-parsed vs runtime-derived. | **Yes, but the implementation must not be a constant.** See O2. |
| C6 | F1: "100 of 110 distinct GUIDs" in `HovlVfxCatalog.asset` | The **110** distinct GUIDs is confirmed by direct count. The **100-that-leak** split I could **not** verify without `AssetDatabase` - it requires a running editor. | **Undetermined in detail, unchanged in substance.** The gate is blind to the file either way (O1). **Do not quote "100 of 110" as verified until O1 has run.** |
| C7 | §6 leverage order lists 9 oracles | Two closures the audit's own findings support are missing from it: the **all-builders divergence** widening (F18/F19/F46/F47 - four findings for one M edit) and the **feature-flag defaults + doc-prose agreement** walk (F14/F60, already specified as matrix BLIND-4-F3). | Added as **O4** and **O6**. |

---

## 8. WHAT THIS PLAN DOES NOT PROVE

In the spirit of audit §3b, and binding on anyone writing a RESULT file against it.

1. **No oracle in this document proves a defect exists.** Each proves a *gate is blind to a class*.
   The audit is explicit: it is **a ranked CANDIDATE LIST that tells you where to instrument, not a set
   of diagnosed root causes**, and **exactly one finding (F75) carries a captured proving line**. An
   oracle written against a candidate pins the candidate. Where a row above closes a *behavioural*
   finding, it closes the finding's **structural half** only.

2. **No oracle here has been run.** I authored no code, ran no gate, no build, no Unity method and no
   git write. Every "expected verdict on arrival" (O1 RED, O2 GREEN, O3 three-row RED) is a
   **prediction from disk contents**, not an observed result. Treat the first real run as the evidence
   and correct this document if it disagrees.

3. **Sizes are estimates, not measurements.** S/M/L is judgment from reading the target files. The one
   number I did measure - 130 registration call-sites between the fences, matching the last green
   marker's 130/130 - is a fact; the effort column is not.

4. **The leverage ordering is an argument, not a proof.** "Findings-closed-per-unit-effort" divides an
   integer I counted by an effort estimate I guessed. Reasonable people would swap adjacent rows. The
   only orderings I would defend hard are: **O2 is a precondition for trusting anything else**, and
   **O1 has the best ratio on the list.**

5. **Nothing here closes the rendering, physics, input or perception class.** §4 is the honest boundary.
   F15's felt verdict, F74(b), F33's non-rarity rows and F75's cause are outside every oracle proposed
   above, and **F76 means the pixels that would settle F15 and F74(b) do not currently exist.**

6. **This plan does not cover PlayMode.** G4 records that PlayMode has **never** produced an artifact.
   Any row whose alternative proof is "play-mode" is, today, a row whose alternative proof is the
   owner's eyes. Building a PlayMode arm that emits an artifact is an uncosted prerequisite.

7. **The `api/**` surface (O8) is outside the `REGRESSION_OK` marker entirely** and would need its own
   marker, its own gate stage, and RULE 3 ownership in `RegressionMarkerRegression` (whose gate-script
   scan covers only `tools/` and `.claude/skills` today - G7 - so four root-level scripts already sit
   outside it). Adding O8 without adding its marker to that scan repeats G7.

8. **I did not read the raid silo.** F39 is uncosted by design (§6). Any assertion about the raid loop's
   coverage in this document would have been written against files under active edit.

---

## 9. PROVENANCE

Authored 2026-08-09 against branch `wip/village2-and-f8-tickets` in a shared working tree.
Source of truth: `docs/reference/AUDIT_2026-08-09.md` (78 findings, 72 uncovered).
Reuses the per-finding PROPOSED-ASSERTIONS column of `docs/reference/REGRESSION_COVERAGE_MATRIX.md`
(rows CS-3, EW-1/EW-2, ECHO-1, BLIND-4-F3) - **never its counts or its verdict, which are stale.**
No code was written, no gate or build was run, no git write was performed.
Where a claim could not be verified at source it is labelled as such (C6) rather than asserted.
