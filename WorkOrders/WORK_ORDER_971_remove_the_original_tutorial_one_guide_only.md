# WORK ORDER 971 — Remove the original tutorial: ONE tutorial, ONE guide

**Status:** FIXED — shipped `17cf8736` ("feat(tutorial): WO-971"); owner felt-verify still PENDING (see the Owner-verify line below). RESULT file still owed (not fabricated).

*(Board note 2026-08-24: bucket corrected DONE/IMPLEMENTED → **FIXED**. Nothing about the work changed — §13 reserves DONE/closing for the PO, and this line's own text says the owner's felt-verify is still owed, so the row belongs in the felt-test queue, not the closed pile.)*
**Silo:** Tutorial / FTUE
**Minted:** 2026-08-10 (banner bumped 971 → 972 in the SAME edit as this mint)
**Supersedes in part:** WO-1014 (its "gate the stand-in" approach), WO-702 (the Sylas steward body)
**Owner-verify:** PENDING — PO felt-verifies + closes (§13). Headless/source checks are done; only she can judge the courtyard.

---

## 1. The ruling (verbatim, binding)

> *"why are two tutorials active?"*
> *"remove the original"*
> *"only the new wolf one stays"*
> *"i want a agent to triage it completely"*
> *"from data"*

— owner, 2026-08-10, on the 20:42 build.

Read literally, as she asked: the original is **REMOVED**. Not disabled, not flag-gated, not
suppressed-but-present. Exactly ONE tutorial exists in the tree when this WO is done.

---

## 2. Triage — FROM DATA (§12: static reading LOCATES, capture CONCLUDES)

### 2a. The proving lines (PROVEN-BY-CAPTURE)

Her live `Player.log` (`%USERPROFILE%\AppData\LocalLow\DeNelle\Echoes of Elarion\Player.log`,
the 20:42 build). Four lines are the whole bug:

```
13425  [Flow:SylasSteward]   Sylas steward spawned at (2.00, 0.08, 3.00) (near the Heart, facing the tree) - founding beats have a body.
21933  [Flow:Tutorial] step 'founding_greet' grant.starterPet - guide BODY summoned ('ice-wolf') at (2.00, 0.06, 3.00)
23125  [Flow:Tutorial] FocusMask resolved highlightId=world.guide target=Sylas       style=Focus rect=(867,316,120,120)
25438  [Flow:Tutorial] FocusMask resolved highlightId=world.guide target=Pet_ice-wolf style=Focus rect=(867,318,120,120)
```

**Two guide bodies two centimetres apart, and the ONE `world.guide` spotlight alternating
between them.** Line 23125 is the decisive one: it resolves to `Sylas` at t **after** the wolf
already existed (21933). That is not a fallback, it is a second guide.

Her screenshot `flag_20260811-014345_00.png` (20:43) confirms it visually: the gold spotlight
ring with the chevron sits on a **bearded peasant NPC in an orange tunic**, with the **white
wolf standing inside the same ring**, while the objective strip reads **"Follow Aldwin to the
gate"**. The peasant is the steward — its body fell back to `NPCs/NPC_Peasant_Tob`
(log 13389: `steward body OK from 'NPCs/NPC_Peasant_Tob' (6 enabled renderer(s))`), because
`NPCs/NPC_Ranger_Scout` did not resolve.

This is exactly her 2307 wording — *"both NPC and echo"* — and 2320, *"the wolf doesnt move and
the npc"*.

### 2b. What the two are, and who arms each

| | **THE ORIGINAL (removed)** | **THE NEW WOLF ONE (kept)** |
|---|---|---|
| Flow | `TutorialDirector` (legacy FTUE) | `TutorialFlow` (`ftue_v2`, the founding arc) |
| Guide body | `SylasStewardInjector` → a humanoid NPC named `Sylas` | `Pet_ice-wolf`, summoned by `TutorialFlow.ApplyStarterPetGrant` |
| Entry point | `[RuntimeInitializeOnLoadMethod(AfterSceneLoad)] Bootstrap()` | `TutorialFlow.Bootstrap` / `OnAnySceneLoaded` → `TryArm` |
| Content | 9 legacy `tut_*` Sylas dialogues | `{guide}`-token dialogue, guide identity `Aldwin, the Ice Echo` |

**Both halves of "the original" armed, but by different routes — this is the part that had to
come from data, not from reading:**

* `TutorialDirector` — **PROVEN-BY-CAPTURE it does NOT reach the hub.** Log 2153:
  `[Flow:Tutorial] Awake self-destruct — scene 'Title' is not a hub`, then nothing. Its
  `RuntimeInitializeOnLoadMethod` fires once per app run, at `Title`, where `Awake` destroys it;
  nothing rebuilds it. It was additionally flag-gated (`if (FeatureFlags.TutorialV2) { …
  Destroy; }`, its own comment: *"Deleted only in WO-T5 after the flip is verified"*).
  So it was **dormant-but-present** — the exact state the ruling forbids.
* `SylasStewardInjector` — **PROVEN-BY-CAPTURE it DOES arm, every hub load.** Log 13297→13440:
  `[Flow:SylasSteward] -> Inject` … `Sylas steward spawned` … `<- Inject (28.8ms)`.
  **This is the half the owner actually saw.** WO-1014 retired the nine legacy `tut_*`
  DIALOGUE ids (the script half) — correctly — but the injector spawns independently of any
  dialogue, which is precisely why she still saw two after the script was clean.

### 2c. Why WO-1014's gate did not hold (READ-AT-SOURCE + PROVEN-BY-CAPTURE)

WO-1014 kept the stand-in and gated it in both directions: `Inject()` refuses to seat when a
guide body exists, and a 0.25 Hz `Update()` watch destroys the holder once one appears.
That code was committed and clean in the tree (`git status` clean on
`SylasStewardInjector.cs`), i.e. it was in her 20:42 build.

**It never fired.** Its own stand-down trace —
`"the founding guide now HAS a world body (WO-961) - standing the steward stand-in DOWN"` —
occurs **ZERO times** in her log (`grep -c` → `0`), while the steward is still on screen at
t=127 s. The gate is unproven at runtime *and* the owner overruled the approach itself. A
fallback that can be on screen at the same time as the real guide is not a fallback.

**Note on a concurrent report:** another lane reported the stand-in as "suppressed, single
authority in place, steward proven to be purely the founding-arc stand-in". The first two
claims describe the WO-1014 code, which is real but **did not execute** in her session (above).
The third claim I **confirm** at source and extend: the steward's only roles were the guide
anchor's link 2 and the AutoPilot probe below — both removed here.

---

## 3. What was REMOVED

| File | Why |
|---|---|
| `Assets/_Modules/Village/NPCs/SylasStewardInjector.cs` (+`.meta`) | The second guide BODY the owner saw. Contained `SylasStewardInjector` + `SylasStewardInteractable`. |
| `Assets/_Modules/Village/Tutorial/TutorialDirector.cs` (+`.meta`) | The legacy FTUE FLOW — "the original". Was flag-dormant, which the ruling forbids. |
| `Assets/_Modules/Village/Tutorial/PetIntroduction.cs` (+`.meta`) | Existed **only** to support the director (sole consumer, verified by grep). |
| `Assets/Tests/EditMode/TutorialDirectorHubGateTest.cs` (+`.meta`) | Existed only to assert the deleted director's hub gate. |

Plus, edited:

* `TutorialWorldAnchors.ResolveGuide` — **link 2 (the stand-in body link) deleted.** The chain
  is now: live pet-Echo body → the Heart → a safe town anchor. Removal note left in place
  naming the capture lines, so no future reader re-adds it.
* `AutoPilotDriver` — the probe `AssertStewardSurvivesNewGame` **existed only to guarantee the
  steward respawned**; it asserted the *opposite* of the ruling and would have gone GREEN on the
  build she rejected. **Reversed, not merely deleted** → `AssertExactlyOneGuideBody` (below).
  Also: `AssertFoundingArc` link 1 accepted `"Sylas"`/`"CompanionIntroducer"` as a valid guide
  body — same alibi problem — now only a `Pet_*` root counts.

### 3a. Carve-outs — VERIFIED BEFORE DELETING, as required

**(a) Sylas the CHARACTER is untouched.** PROVEN: `"Sylas"` is carried in
`Assets/_Modules/Core/State/HeroCanonNames.cs`; `en.json` binds `hero.ranger = Sylas`;
`abilities.json` carries his WO-861 A2 hero kit; `dialogues.json` keeps his non-tutorial
`SylasFirstMeeting` companion beat and his `Portraits/Sylas` speaker row.
**Only his role as a tutorial guide BODY is gone.** `OneGuideBodyRegression` Case 5 pins this
so a later cleanup cannot mistake this WO for a licence to delete the person.

**(b) Everything the founding arc still references SURVIVES.** Grepped before every deletion.
The legacy tutorial folder is *not* wholly legacy — the surviving arc and live non-tutorial
systems consume it:

* `TutorialFlow.cs:1550` adds a `TutorialWaveSpawner`
* `DialogueCommandSink` adds `TutorialAutoWalk` + `TutorialHudOverlay`
* `ElaraWaveThreeJoin` + `StoryCompanionInjector` call `CompanionSpawner.CompanionClassFor`
* `ElaraWaveThreeJoin` + `SylasFirstMeeting` use `TutorialDialogue`

Deleting the folder wholesale would have been an orphaned-reference outage that **compiles**
and blanks at runtime. `OneGuideBodyRegression` Case 6 pins these as must-survive, so the suite
now guards **both** failure directions.

---

## 4. Proof nothing references what was deleted

```
$ grep -rn "TutorialDirector|SylasStewardInjector|SylasStewardInteractable|PetIntroduction" \
      --include=*.cs Assets/   # comments + string literals excluded
  NONE - code-clean

$ for f in <the 4 deleted files>; do test -e "$f" ...
  gone: Assets/_Modules/Village/NPCs/SylasStewardInjector.cs
  gone: Assets/_Modules/Village/Tutorial/TutorialDirector.cs
  gone: Assets/_Modules/Village/Tutorial/PetIntroduction.cs
  gone: Assets/Tests/EditMode/TutorialDirectorHubGateTest.cs

$ grep -rl <the 4 deleted .meta GUIDs> Assets/Scenes Assets/Resources Assets/_Modules Assets/Prefabs
  NO scene/prefab/asset references the deleted script GUIDs
```

GUIDs checked: `8abcbe81423d0d044a31cbfba9ac9839` (steward),
`2439e2ca171cedb4cb92d9ace7bc3d07` (director), `17a378ae993f8314992414641c3b981f` (pet intro),
`27a849fdfb14d3c4f87f14f4ea44a7e9` (hub-gate test).

Dependent suites/tests repaired rather than left dangling:
`OneGuideBodyRegression` (rewritten), `CastleCompanionIntroducerTest`,
`BuildMenuRealEconomyRegression`, `HudUiRegression`, `FoundingGuideWolfBodyRegression`,
`BlankStartCensusRegression`, `TownsfolkBodyPoolRegression`.

Brace + NUL gate (§1 / WO-434) run on every touched `.cs`: **all balanced, zero NUL bytes**,
and a tree-wide `.cs` NUL sweep is clean.

---

## 5. The regression — fails if a second tutorial or guide ever arms again

`Assets/Editor/Regression/OneGuideBodyRegression.cs` `[one-guide-body]`, markers
`ONE_GUIDE_BODY_OK` / `ONE_GUIDE_BODY_FAIL`. Class name + entry points UNCHANGED, so **no
`DataRegression.cs` edit is required** (that file is lane-fenced).

| Case | Fails when |
|---|---|
| `single-authority` | `LiveGuideBody()` / `HasLiveGuideBody` gone, or the live-Pet lookup is duplicated |
| `no-standin-link` | `ResolveGuide` looks for `"Sylas"` or `"CompanionIntroducer"` again, or loses its Heart fallback |
| `original-removed` | **any of the 4 deleted files comes back** |
| `one-flow` | **more than one file drives tutorial steps**, or any file can seat a `"Sylas"` body, or the survivor stops being the wolf arc |
| `character-kept` | the canon hero NAME or `SylasFirstMeeting` was deleted (carve-out (a) guard) |
| `shared-kept` | any must-survive shared file was deleted (carve-out (b) guard) |

Every case was **dry-run against the real tree** before landing and all pass:
seaters = 0, step-drivers = exactly 1 (`V2/TutorialFlow.cs`), Pet lookups = 1,
`ResolveGuide` has no `"Sylas"`/`"CompanionIntroducer"` and keeps `HeartController`,
`HeroCanonNames.cs` still carries the name, `TutorialFlow` still references `ice-wolf`.

**Runtime counterpart:** `AutoPilotDriver.AssertExactlyOneGuideBody` — fresh save + hub reload,
then an **8-second watch window** (not a single sample — the WO-1014 failure was precisely that
the stand-in seats on hub load and the wolf arrives a beat later), asserting no
`Sylas`/`CompanionIntroducer` body seats, at most one `Pet_*` root exists, and the guide anchor
resolves to a pet body or honestly to the Heart.

---

## 6. Also reported (owner named it in the same breath, 2320) — NOT fixed here

> *"the vfx yes thing on the tree"*

**Identified, out of this WO's scope, needs its own ticket.** It is **not** either Heart aura —
both report `WITHHELD` in her log (`Aura_HeartPulse`, and `TreeofLifeAura_Aura` withheld by
`AmbientAuraPolicy` per WO-1002/WO-890), and `HubAmbientVfxInjector` reports `tree aura=0`.

**It is `Poi_NodeAura` → Hovl prefab `Magic circle sun loop`** — a gold ground disc whose
`Trails` sub-system throws vertical orange/gold streaks upward. Fired 43× in the flagged
session, re-spawning every tick because its catalog row is `IsLoop: 0`.

* Spawned by `PoiCalloutSystem.EnsureNodeAura` — `Assets/_Modules/Village/Vfx/PoiCalloutSystem.cs:239` (key const `:63`)
* Catalog row: `Assets/Resources/VFX/HovlVfxCatalog.asset:128-134`
* Prefab: `Assets/Hovl Studio/Magic circles/Prefabs/Loop version/Magic circle sun loop.prefab`
* **Root cause:** it is the POI callout for an *invisible logical* object — the DDOL fallback
  `Collector_lumbermill`, parented to `ResourceCollectorHost` and never positioned, so it sits at
  **world (0,0,0)**. The Heart anchor is `(0,0,12)`: same X, 12 m nearer the camera, so it reads
  as a plume at the tree roots. `AmbientAuraPolicy` misses it because that policy gates by **key
  string** (`TreeofLifeAura_Aura`) only — `PoiCalloutSystem.cs:226` even says *"this gate is
  dormant BY VALUE"*.
* Suggested fix (for the new ticket): position/suppress the DDOL fallback collector's
  `PoiBeacon`, **or** make `AmbientAuraPolicy` gate by resolved **prefab** rather than key so
  aliases cannot slip through. Latent bypass worth closing at the same time: `PP_FireFlies`
  aliases the rejected `FireFlies.prefab` under a different key (zero call sites today).

Her other two 2320 complaints — *"the wolf doesnt move"* (a separate lane just fixed
`PetHeroLeash`/`PetHarvester`, untouched here) and the repeated
`STEP-STUCK :: founding_walk — no 'hero.reached:guide_gate' after 120s` (2308/2318/2321) — are
**not** addressed by this WO. The step-stuck is plausibly downstream of the wolf not leading,
and should be re-measured after her next felt-test rather than theorised now.

---

## 7. What NOT to touch (honoured)

`Pet.cs`, `PetHarvester.cs`, `PetHeroLeash.cs`, `HeroLocomotion.cs`, `DungeonHero.cs`,
`DungeonCameraRig.cs`, `DataRegression.cs`. No Unity run, no gate, no git, no commit — the
orchestrator gates and commits. No FlowTrace stripped (§12): the removed traces went with their
deleted files, and `[Flow:Tutorial]` instrumentation is untouched and extended.
