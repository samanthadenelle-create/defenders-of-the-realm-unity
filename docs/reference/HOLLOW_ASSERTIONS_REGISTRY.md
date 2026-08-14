# Hollow Assertions Registry

**A canonical registry of trace fields that cannot report failure.**
Created 2026-08-10. Audit scope: `Assets/_Modules/`, `Assets/Editor/`. Read-only sweep — no code changed.

Companion to `docs/INSTRUMENTATION_STANDARD.md` (the *method*) and CLAUDE.md §12 (the *rule*).
This document is the *failure catalogue*: the specific lines in this tree that look like evidence and are not.

---

## ⛔ Read this before you touch any line in the table

**The fix is NEVER to delete these lines.**

CLAUDE.md §12 is binding: *"NEVER STRIP FLOWTRACE. Instrumentation is PERMANENT."* A deleted hollow
trace becomes **no** trace, and the next regression in that system starts from zero evidence instead of
weak evidence. Deleting is strictly worse than leaving it.

The fix is always the same shape: **make the line assert something that can fail.** Replace the
non-falsifiable token with a measured quantity and a threshold. Keep the line, keep its position in the
flow, keep its `[Flow:<system>]` tag. Widen it, never remove it.

> Drift note, flagged during this audit: `docs/INSTRUMENTATION_STANDARD.md` §1.4 is still titled
> **"The strip path (clean it up later)"** and instructs a reader to *"mute/strip the `Step`
> breadcrumbs"* on graduation. That section predates the 2026-08-09 owner ruling in CLAUDE.md §12 and
> now contradicts it. It needs a banner or a rewrite — not in scope for this read-only audit, but
> logged here so the contradiction is not discovered the hard way.

---

## 1. The pattern

A **hollow assertion** is an instrumentation line whose success token is not falsifiable — there exists
no realistic broken state of the system under test that would make it print anything other than success.

It is **worse than no instrumentation**, because no-trace makes the next reader go look, while a hollow
trace actively steers them away from the broken thing. It also quietly defeats CLAUDE.md §12: the seat
that reads `x=ok` believes it *has* captured data, so it skips the instrument-first step it thinks it
already did. The rule is satisfied in letter and broken in substance.

### The five shapes

| # | Shape | Tell |
|---|---|---|
| **H1** | **Non-null reported as health** | `x != null ? "ok" : "..."` — the word `ok` implies *working*, the check proves *exists* |
| **H2** | **Constant by construction** | asserting on an `AddComponent` result, a field assigned two lines above, a `.material` getter that instantiates on read |
| **H3** | **Dead-by-design source** | the field is computed from a source that is zero/empty *by design* in the context the trace fires |
| **H4** | **Pre-settle read** | emitted before layout/resize/async-load lands, so it reports the pre-settle value forever |
| **H5** | **Success marker with no failure branch** | an unconditional "done/applied/hidden" after a sequence of `void` calls that each early-return on error |

**H3 is the subtlest and the most valuable to hunt.** It survives code review, because the line reads as
a real measurement of a real quantity. Only knowing the *ownership/lifecycle context* reveals that the
quantity is structurally zero there.

### What is NOT hollow (do not pad this registry)

- A genuine `FlowTrace.Fail` / `Warn` on a real error branch — including one that prints `x=ok` to mean
  *"this argument was not the problem"* (e.g. `TowerSwapService.cs:174-175`,
  `BuildingPerkService.cs:209-210`). That `ok` is exculpatory, not a health claim.
- A check honestly **labelled as wiring**, e.g. `BuildPaletteUI.cs:457`
  (`topBar={_topBarGo!=null} tray={_trayGo!=null}`) — booleans, named as references, claiming nothing
  about render. The sin is the *word* `ok` implying health where only wiring was proven.
- Dev-only harnesses that are not gates: `Assets/_Modules/Village/Buildings/TowerLoopDevHarness.cs:171`
  — **noted and excluded**.
- Classification returns where `"OK"` is one enumerated class among named failure classes
  (`MagentaGuard.cs:731`, `ClassifyMagenta` — M1/M2/M3/M5 are the real classes; `OK` is the residual).
- `CoreServices.cs:55 / :119 / :191` — reads as H1 but is honest: the null branch prints
  `"IVillageHud registered as NULL."` The two states are distinguishable in the log.

---

## 2. The three worked examples

These surfaced independently in one night, which is what motivated the sweep.

### WO-973 — `bubble=ok` (shape H1 + H4)

`Assets/_Modules/Dungeons/Wanderer/Bryn.cs:158`

```csharp
$"bubble={(_bubble != null ? "ok" : "MUTE")}).");
```

`_bubble` is `_bubbleBehaviour as IWandererBubble` (`Bryn.cs:137`). Non-null proves the interface cast
resolved — **the seam exists**. It proves nothing about the bubble the player sees. The shipped defect
was a bubble covering ~60% of the screen with its text clipped mid-word, and this line printed `ok`
through the whole of it. It survived until a human looked at a screenshot.

The H4 half compounds it: `WandererBubble.cs:124` sets `_resizePending = 3` inside `Show`, i.e. the
real panel size lands up to **three frames after** `Show` returns. Any size assertion taken at `Show`
time reads a pre-settle value forever — and in fact **no line anywhere traces the settled size**, which
is why nothing caught it.

### WO-968 §11b — `[Flow:GaitF] bodyErr=0.0` (shape H3 — the subtlest kind)

`Assets/_Modules/Village/Hero/HeroGaitForensics.cs:160`, printed at `:207`, written to CSV at `:195`

```csharp
float bodyErr = _body != null && velMag > 0.2f ? Mathf.DeltaAngle(heading, bodyYaw) : 0f;
```

`velMag` comes from `_loco.Velocity` (`HeroGaitForensics.cs:132-133`). `HeroLocomotion`'s own header
(`HeroLocomotion.cs:20-31`) states the ownership law: *"a live CharacterController on this rig means
this component writes NOTHING"* and *"Feeding a dead Velocity while another component moved the root is
what made the Keeper slide through a dungeon in a single idle clip."*

So in dungeons — where a foreign `CharacterController` owns the hero — `_loco.Velocity` is **zero by
design**, `velMag` never clears `0.2f`, and `bodyErr` is pinned at `0f`. `bodyErr=0.0` reads as *"the
body points exactly along travel — perfect"* while the probe is measuring nothing at all.

**`HeroGaitForensics` has zero ownership awareness** — no `Foreign`, `CharacterController`, `Owner` or
`measured` token appears anywhere in the file. Every velocity-gated column shares the defect:
`heading` (`:134`), `bodyErr` (`:160`), `skate` (`:186`), and the `headingJump` emit trigger (`:200`).
In a dungeon the whole instrument is inert and prints as green.

### WO-976 — `panelSettings=ok canvas=ok => hasSurface=` (shape H1, highest traffic) — ✅ **FIXED 2026-08-14**

> **✅ FIXED (WO-976 implemented 2026-08-14).** The single hollow emit is now three:
> (1) the original line **retokened to wiring language** — `surfaceWired=` instead of `hasSurface=`,
> with `present`/`<missing>` instead of `ok`, and a Fail that now names itself a **wiring** failure;
> (2) a new **MEASURED** verify, `AddressableUIManager.VerifyRendersMeasured` → the shared
> `DeNelle.Core.Diagnostics.UiSurfaceProbe`, which waits for layout to settle and then measures
> resolved rect px / resolved opacity / sorting order / viewport intersection, emitting
> `SURFACE_ZERO_SIZE`, `SURFACE_TRANSPARENT`, `SURFACE_OFFSCREEN` and `SURFACE_BEHIND` as **four
> separately-named Fails**; (3) a **mandatory named skip** for batchmode (no layout pass there, so an
> unguarded probe would emit spurious failures and invite someone to weaken the thresholds).
> Falsified by `Assets/Editor/Regression/UiSurfaceProbeRegression.cs` (`UI_SURFACE_PROBE_OK`), which
> drives a 0×0 / transparent / offscreen / buried surface through the classifier and asserts each one
> fails **on its own class** and that an unmeasurable surface is never counted as a pass. The
> description below is retained as the record of the defect.



`Assets/_Modules/Core/UI/AddressableUIManager.cs:234`

```csharp
$"panelSettings={(docOk ? "ok" : "<missing>")} canvas={(canvasOk ? "ok" : "<none>")} => hasSurface={hasSurface}");
```

`docOk` is `doc != null && doc.panelSettings != null` (`:226`); `canvasOk` is `canvas != null` (`:229`).
Both halves are pure non-null. `hasSurface` therefore means *"a surface object exists"*, not *"a surface
renders"* — and the `Fail` at `:238` is gated on `!hasSurface`, so the whole verify block goes quiet for
a panel that is zero-sized, fully transparent, positioned offscreen, or buried behind a higher sort
order. This is the shared UI surface resolver: every Addressable-loaded panel in the game passes
through it.

---

## 3. The registry

Ranked by **traffic × consequence**. Every line below was opened and read at source.

### HIGH — shared surfaces and the pre-ship gate

| file:line | the line | what it claims | what it actually proves | how it could lie | severity |
|---|---|---|---|---|---|
| ✅ **FIXED 2026-08-14 (WO-976)** — `Assets/_Modules/Core/UI/AddressableUIManager.cs:234` | `panelSettings={(docOk ? "ok" : "<missing>")} canvas={(canvasOk ? "ok" : "<none>")} => hasSurface={hasSurface}` | the loaded panel has a usable render surface | a `UIDocument` with a non-null `PanelSettings`, **or** a `Canvas` component, exists somewhere in the instance | a panel whose root `VisualElement` resolves to 0×0, whose `opacity` is 0, whose `Canvas.sortingOrder` puts it behind an opaque scrim, or which is positioned offscreen — all print `hasSurface=True` and suppress the `Fail` at `:238`. **The shared resolver for every Addressable panel: one hollow token blinds the whole UI silo.** | **CRITICAL** |
| `Assets/_Modules/DevTools/AutoPilotDriver.cs:5231` | `OpenEachHUDPanel: {opened}/{registered} registered panels verified open.` | N panels were verified open by the headless fleet | `PanelRouter.Open(id)` returned true and `PanelManager.AnyOpen` was true (`:5207-:5214`) — a **bookkeeping flag** | every panel in the game renders as a 0×0 or fully-transparent element and this still prints `12/12 registered panels verified open`. The word "verified" is doing work the code never did. This is a **pre-ship gate line** (CLAUDE.md §8) — a hollow token here launders itself into "headless-verified" in a hand-off. | **CRITICAL** |
| `Assets/_Modules/Core/UI/PanelRouter.cs:289` (predicate at `:268`, method `VerifyOpenedVisible` at `:266`) | `"PanelRouter: '" + id + "' opened and verified **visible**"` | the panel opened **and was verified visible** | `PanelManager.AnyOpen == true` — a **bookkeeping flag** written by whoever called `NotifyOpened`. The method is named `VerifyOpenedVisible` and its entire predicate is one dictionary read | a panel that registers with the modal arbiter but renders nothing — **the WO-465 "invisible-scrim" class this very method was written to catch** — sets `AnyOpen`, suppresses the `Fail` at `:284`, and prints `verified visible`. It also **corroborates** with `ScreenOpenWatchdog.cs:96` and `AutoPilotDriver.cs:5231`, which read the same flag: three independent-looking traces agreeing on a blank screen. The battle-lock carve-out at `:277-:281` is genuinely good work on the *other* branch; the pass branch is the hollow one. | **CRITICAL** |
| `Assets/_Modules/DevTools/AutoPilotDriver.cs:2632` | `AssertTouchVerbBarRenderable: PASS — touch verb bar is code-built uGUI; Cancel renders without any PanelSettings adoption.` | the Cancel button **renders** | a `UnityEngine.UI.Button` whose GameObject name contains "Cancel" exists in the probe hierarchy — found via `GetComponentsInChildren<Button>(true)` at `:2620`, where the `true` is **`includeInactive`** | an inactive, zero-sized, off-canvas or alpha-0 Cancel button satisfies `ugui == true` and prints `PASS — Cancel renders`. The assertion's own name is `…Renderable` and it never touches a rect, an alpha, or `activeInHierarchy`. Gate line; the failure it guards ("mobile web can't cancel") is exactly the failure it cannot see. | **HIGH** |
| `Assets/_Modules/HUD/Kit/HudKitController.cs:1970` | `"occupancy applied: posture " + … + " -> " + shown + " widgets live"` | N widgets are live on screen for this posture | that N keys were found in the `hud-areas.json` occupancy dictionary. Look at `:1949-:1952`: when `_host.Mount(area)` returns **null** the reparent is skipped, but `SetActive(true)` and `shown++` run anyway | a mount destroyed on scene swap, or a `HudArea` present in json but missing from `HudAreasHost.Add()`, leaves every widget parented to the off-layout widget **pool** transform. The player sees an empty HUD and the log reads `occupancy applied: hostile.activebattle -> 14 widgets live`. Fires on **every posture flip** — the highest-traffic trace in the HUD module. | **HIGH** |
| `Assets/_Modules/HUD/Kit/HudKitController.cs:1423` | `"action bar repacked: " + n + " face(s) centered"` | n action-bar faces were positioned and shown | `_barModel.Active.Count == n`. The loop body at `:1415` does `if (rt == null \|\| go == null) continue;` — a missing button is silently skipped and `n` prints regardless | if `RegisterBarButton` never ran for a face (build-order change, or a widget builder that threw into a `Guard.Try`), `_barButtonRects[idx]` is null, zero buttons activate, the bar is empty, and the line still says `7 face(s) centered`. Fires on every `ActiveButtonsChanged` — every posture change and every loadout push. The bottom action bar is the game's primary navigation. | **HIGH** |
| `Assets/_Modules/Village/Hero/HeroGaitForensics.cs:160` (emitted `:207`, CSV `:195`) | `bodyErr={bodyErr:F1}` | the felt angular error between the visible body and the direction of travel | `Mathf.DeltaAngle(...)` **only when `velMag > 0.2f`**; otherwise the literal `0f` | in any dungeon a foreign `CharacterController` owns the hero, so `_loco.Velocity` is dead by design (`HeroLocomotion.cs:20-31`), `velMag` stays 0, and `bodyErr=0.0` prints as a perfect score while the hero visibly slides sideways. **The file has no ownership awareness at all** — `heading` (`:134`), `skate` (`:186`) and the `headingJump` emit trigger (`:200`) are inert by the same mechanism. WO-968 §11b. | **HIGH** |
| `Assets/_Modules/HUD/Kit/HudAreasHost.cs:124` | `"HudAreasHost built: 9 area mounts on one canvas (scaffolding only)"` | 9 area mounts were built | **nothing — `9` is a string literal.** There are **11** `Add(HudArea, …)` calls immediately above it (`:95-:118`) | the number is **already wrong today**, which is the cleanest possible proof it tracks nothing. Delete half the `Add()` calls, or have `Add` throw on the third mount, and it still prints "9 area mounts" — while every subsequent `Mount(area)` returns null and feeds the `widgets live` overcount above. Once per HUD boot, every scene load. | **HIGH** |
| `Assets/_Modules/Village/Enemies/EnemyAnimatorFactory.cs:166` | `Load controller for {modelName}: {(ctrl == null ? "NULL" : "OK")}` | the enemy's animator controller resolved usably | `Resources.Load<RuntimeAnimatorController>` returned non-null | a controller asset that exists but has an **empty default state**, no clips bound, or the wrong rig's states prints `OK` — which is byte-for-byte the WO-436 Failure-B symptom the comment at `:163-166` says this line exists to discriminate ("the Animator idles in its empty default state → the NavMeshAgent slides the transform with no clip playing"). Fires on **every enemy spawn**, so it is the most-read line in the combat silo. | **HIGH** |
| `Assets/_Modules/Village/Waves/WaveFeedbackDirector.cs:321` | `$"[WaveFeedbackDirector] Installed (wave feedback active). hudBound={CoreServices.Hud != null}."` | the director is installed **and bound to a HUD** | nothing about the binding. `FindHud()` at `:325-:330` is a **stub whose entire body is `return null;`** — so `dir.Bind(wave, hud)` at `:319` is *always* called with `hud == null`. The `hudBound` token reads an **unrelated global** that some other system registered | `hudBound=True` prints on every wave-scene load, forever, while the director holds a null HUD and wave damage feedback never renders. **The trace is not merely unfalsifiable — it reports a different variable than the one it names.** No failure branch exists; the line fires unconditionally after `SetActive(true)`. | **HIGH** |
| `Assets/_Modules/Village/World/SceneLinkResolverHost.cs:192` | `$"ARRIVED {link.toScene} on-mesh @ {landing}."` | the hero arrived and is standing on navmesh in the new scene | that `landing` — the **intended** target computed at `:173-:180` — samples onto a navmesh (`:187`). It **never reads `loco.transform.position` after the warp** | `loco == null` emits a `Warn` at `:184` and then **execution continues**, so `ARRIVED … on-mesh` prints for a hero that was never moved; or `WarpTo` lands the hero elsewhere because the destination mesh is not yet online. Either way the trace certifies a pose nobody occupies. The honest sibling in the same feature is `SceneTransitionTrigger.cs:683`, which prints `requested {targetPosition}, hero now @ {playerTransform.position}` — **the achieved pose**. This is the hollow copy of a correct line that already exists in the tree. | **HIGH** |
| `Assets/_Modules/Village/Hero/HeroProgression.cs:269` | `"granted 2 starter skill points (first level-up)."` | two skill points were banked | that `_hasGrantedStarterPoints` was flipped — **at `:266`, before the grants**. The two `SkillSystem.Instance?.GrantSkillPoint()` calls at `:267-:268` are null-conditional and, **unlike the identical call twelve lines above at `:258`, are not wrapped in the try/catch that emits `FlowTrace.Fail`** | `SkillSystem.Instance` is null at first level-up → zero points granted → the latch is already true so it **never retries** → the trace says "granted 2". Permanent, unrecoverable, and it fires for **every player exactly once**. The same file demonstrates the correct handling two statements earlier, which makes this a pure omission. | **HIGH** |

### HIGH — the economy-grant class (reporting the *request* as if it were the *result*)

This is a distinct sub-pattern worth naming, because it recurs across four unrelated files and it costs
the player real currency. **The economy authority itself is honest.** `EconomyService.GrantInternal`
clamps `EarnedIncome` grants against the town bank cap (`Assets/_Modules/Village/EconomyService.cs:396`,
`:405-:412`) and its own trace at `:416` prints the **post-clamp** amount *and* the resulting wallet
total: `Grant +W{wood} +I{iron} -> GameState Wood={...} Iron={...}`. That is exactly right.

The defect is in the **callers**, which echo the amount they *asked for* and call it granted.

| file:line | the line | what it claims | what it actually proves | how it could lie | severity |
|---|---|---|---|---|---|
| `Assets/_Modules/Village/World/Camps/RaidVictoryController.cs:277` (and the fallback at `:286`) | `$"LOOT granted via EconomyService: +{loot.Crystals} crystals, +{loot.Food} food."` | the player received that loot | that the **requested** amounts were passed to `eco.Grant(loot)`. `Grant` routes to `GrantInternal(amount, BankGrantKind.EarnedIncome)` (`EconomyService.cs:364`) — explicitly the **clampable** kind | town bank at cap → the raid pays **0** → the trace reads `LOOT granted: +500 crystals, +300 food`. The player finishes a raid, receives nothing, and the log corroborates the payout. The `GameStateService` fallback at `:284-:285` has the same defect (`AddCrystals`/`AddFood` also clamp). | **HIGH** |
| `Assets/_Modules/Village/Quests/DailyQuestRewardBridge.cs:126` (also `:141`, `:153`, `:159`) | `$"DailyQuest '{q.TemplateId}' granted {reward.RewardCrystals} crystals"` | the daily paid out | that a catalog row contained that number. Every grant call is null-conditional with no return checked; `:158` even **discards** `GlimmerCurrencyService.Instance?.TryAddGlimmer(...)`'s bool. Worse, `q.ClaimedAtUnix` is latched at `:119`, **before** the grants | a quest completes during a scene swap where `GameStateService.Instance` is null → every `?.` no-ops → the quest is **latched as claimed** → four `granted N …` lines print and the player got nothing, permanently. `GrantRandomItem` at `:178-:194` in the same file is the honest sibling: it `Warn`s on every null. | **HIGH** |
| `Assets/_Modules/Village/World/Camps/ChallengeOutpostVictoryController.cs:147` | `$"reward granted: {ClearGold} gold + {ClearXp} XP."` | gold and XP were paid | **two compile-time constants.** Both calls above are null-conditional (`EconomyService.Instance?.AddCoins`, `HeroProgression.Instance?.AddXp`) | outpost cleared in a scene with no `HeroProgression` instance → XP silently discarded → log reads `reward granted: 250 gold + 400 XP`. A trace built entirely from `const` fields can never disagree with itself. | **MEDIUM** |
| `Assets/_Modules/Village/Population/PopulationService.cs:211-213` | `$"Echo slot {m.EchoSlot} UNLOCKED ({reason}) … slots now {s.PopulationEchoSlots}/{MaxEchoSlots}."` | an Echo slot unlocked and the Echo is available | `s.PopulationEchoSlots` — **the field assigned three lines above at `:208`**. Shape H2, a self-report | `EchoService.Instance` null at `:209` → no Echo granted; `GameStateService.Instance` null at `:210` → the slot is **not even persisted** → the trace still prints `Echo slot 2 UNLOCKED … slots now 2/N`. Echo unlocks are a core progression beat (CLAUDE.md §7); a silent miss here is felt and unrecoverable. | **MEDIUM** |

> **The generalisation:** whenever a value passes through a layer that may clamp, reject, or no-op it,
> the caller must trace the **returned/observed** amount, never the argument it sent. If the callee
> returns nothing, that is the bug to fix first — the trace is only reporting the API's own blindness.

### MEDIUM — real systems, narrower blast radius

| file:line | the line | what it claims | what it actually proves | how it could lie | severity |
|---|---|---|---|---|---|
| `Assets/_Modules/HUD/Kit/HudKitController.cs:1371` | `"models bound (vitals/economy/wave/world/abilities/cycle/target/cast)"` | all ten named models are bound and the HUD is live-data-driven | that `BindAll` reached its last statement. **Shape H5 — the method has no failure branch at all** (`:1358-:1370`); `_targetFrame.Bind(m.Target)` and `_castBar.Bind(m.Cast)` are unchecked | `CoreServices.HudModel` is a live object whose sub-models (`Target`, `Cast`) are null because their producers have not registered yet. Every `OnXxx()` handler opens with `if (v == null) return;`, so bars sit at zero and the target frame never populates — and the line still prints the full "bound" list, **naming by name the sub-systems that are dead**. | **MEDIUM** |
| `Assets/_Modules/HUD/Kit/HudKitController.cs:184` | `"kit assembled: " + kit._widgets.Count + " widgets, posture " + …` | the kit assembled N widgets | how many times `Register()` was called — effectively a compile-time constant. Worse: `Register` ends with `root.SetActive(false)`, so **every counted widget is inactive at the moment the line prints** | every widget builds as a blank rect (missing `Resources/RpgUi` art in a WebGL build) and the count is unchanged. A count that cannot move is not a measurement. | **MEDIUM** |
| `Assets/_Modules/Dungeons/DungeonController.cs:1394` | `"WO-770.3b: subscribed to BattleArena.OnBattleStaged/OnBattleEnded (real-time settle + combat-camera switch)."` | the dungeon settle hook is live | that the `+=` executed. **The only failure exit — `if (arena == null) return;` at `:1386` — is completely silent** (no `Warn`, no `Fail`) | `BattleArena.Instance` is not yet constructed when the dungeon loads → the method returns with **no output at all** → the run can never settle. The comment at `:1378` states the stakes: *"Without this hook nothing clears the combat lock, credits the boss, or ends the run on a loss."* A success marker whose failure path emits nothing means the trace channel contains no evidence either way — the reader sees the absence of a line and cannot tell it from a log they scrolled past. | **MEDIUM** |
| `Assets/_Modules/Dungeons/ComposedAmbushDirector.cs:45` | `$"armed tier={tier} hero='{(hero != null ? hero.name : "<null>")}'"` | the darkness-ambush director is armed | `tier` (always `1` from the only caller) and that a Player-tagged transform exists. **It omits the one field the whole feature hinges on: `_lantern`** | `_lantern == null` → `dark` is false forever at `:53` → `Update` returns at `:73` every tick and **no ambush can ever fire**. The trace prints `armed tier=1 hero='Player'` on every composed-dungeon entry while the feature is 100% inert. Companion defect at `:91`: `AMBUSH fired in darkness (#N)` is gated on `SpawnAmbushNearHero()`, which `return true`s at `:108` immediately after `spawner.SpawnGroup(...)` **without checking anything spawned** — so a zero-enemy spawn still prints "AMBUSH fired". | **MEDIUM** |
| `Assets/_Modules/Village/World/Camps/EnemyOutpost.cs:271` | `$"{OutpostId} garrison live at {transform.position} — combat begins when the hero approaches (~{GarrisonRing + 6}m)."` | per its own comment at `:267-:270`, confirmation that "the outpost actually materialised + is garrisoned (i.e. the hero has something to fight)" | that the `SpawnGarrisonStaggered()` coroutine returned. **No count, no alive check** | an outpost anchored off-navmesh yields `_aliveCount == 0`, which warns and `Clear()`s the outpost at `:459` — and then control falls out of the coroutine and `:271` prints `garrison live at …` for an outpost with **zero defenders**. The success line is emitted on the empty-garrison path. The fix is one token: print `_aliveCount`. | **MEDIUM** |
| `Assets/_Modules/Village/HubStructureVisualInjector.cs:635` | `$"fitted BoxCollider on '{host.name}' size={b.size} center={b.center} (ticket #10 — now solid)."` | its own comment calls this "proof the structure is solid" | the size of a box it set on the line above. **The real failure — `if (!have) return;` at `:619`, i.e. the visual has no renderable mesh — exits silently** | a structure whose Tripo visual failed to skin has no renderers, so no collider is ever created, the hero walks through the building — and the `Hub` channel contains "now solid" lines **only for the structures that were never broken.** A trace that is emitted exclusively on the success path is a survivorship filter, not evidence. | **MEDIUM** |
| `Assets/_Modules/Village/Arena/ArenaHeraldSpawner.cs:179` | `$"herald placed at {_heraldRoot.position} (proximity Interact live)"` | the Arena entrance is discoverable **and its Interact prompt works** | that `BuildHerald()` returned a transform. `HideHeraldVisual(_heraldRoot)` runs immediately above whenever `FeatureFlags.Colosseum` is on, and the "Interact live" half is armed by a **different method** this line never touches | Colosseum flag ON but `HubStructureVisualInjector` failed to place the colosseum model → the procedural monument is hidden, nothing replaces it, the player sees **empty ground with no Arena entrance** — and the trace says "herald placed … proximity Interact live". Two claims in one line, neither checked. | **MEDIUM** |
| `Assets/_Modules/Dungeons/ComposedDungeonBootstrap.cs:138` | `"ComposedAmbushDirector armed (slice 6 darkness ambush)"` | the ambush director is armed | that `host.AddComponent<ComposedAmbushDirector>()` returned non-null — **constant by construction (H2)** | the same inert-lantern scenario as `ComposedAmbushDirector.cs:45`; this is the **second corroborating line** for a dead feature. Note `:130` in the same method is honest — `lantern armed standalone: stones={stones.Count}` prints a count, so a zero-stone bake shows up. | **MEDIUM** |
| `Assets/_Modules/Core/UI/ElarionUiKitConformance.cs:430` | `"kit toast -> '" + message + "' tone=" + tone` | the toast carrying this text is on screen | that `ShowToast` ran to completion. The one branch that could report a problem is swallowed at `:426`: `if (parts.label != null) parts.label.text = message;` | `ToastCard` fails to build its label (missing TMP font in the build). Nothing is written anywhere, the player sees a blank card or nothing — and the log prints **the exact message text as if it had displayed**. This is the toast path for every kit toast in the game, including the bank-overflow one below. | **MEDIUM** |
| `Assets/_Modules/Core/UI/BankOverflowToastPresenter.cs:53` | `"BankOverflowToastPresenter attached -- clamped grants will surface on screen."` | a forward-looking guarantee: overflow losses **will surface on screen** | that `TownBankCapacity.Overflowed += OnOverflow` executed. `_attached` was set true on the line above; no failure branch exists | `OnOverflow` fires but hands off to `ElarionUiKit.ShowToast`, which needs a live canvas — in a scene with no UI root, or under the label failure above, nothing surfaces. **The player silently loses resources and the boot log promised the opposite.** A trace must never make a claim about the future; `[RuntimeInitializeOnLoadMethod]`, so it fires once every session. | **MEDIUM** |
| `Assets/_Modules/Core/Diagnostics/PrivacySensitiveUi.cs:84` | `$"privacy: hid {scope.Hidden.Count} identity-bearing root(s) for capture frame"` | the capture frame was scrubbed of identity-bearing UI | how many objects had previously opted in via `Register()`. **Shape H3** — `s_registered` is empty by design until something registers, and the trace fires on the capture path regardless | a wallet-address label was never wrapped in a `Register` call (new panel, or a refactor). `HideForCapture` iterates an empty set, the screenshot ships with the address visible, and the log reads `privacy: hid 0 identity-bearing root(s)` — which reads as *"the privacy step ran."* **A count of 0 here should be a `Warn`, not a `Step`.** Privacy consequence raises this above its traffic. | **MEDIUM** |
| `Assets/_Modules/Core/UI/HarvestPanelGate.cs:49` | `"HarvestPanelGate.RequestToggle — harvest panel toggle requested"` | the harvest button did its job | that the method was entered. It is logged **before** the invoke, and `ToggleRequested?.Invoke()` on a null event is a silent no-op | `EchoWorkforceHud` is not present in the current scene (dungeon, or a boot race). The player taps the harvest icon repeatedly, nothing opens, and the log shows one clean "toggle requested" per tap with **zero warnings**. The sibling `ObsidianQueueGate` already exposes a `HasSubscriber` property for exactly this reason; this gate has no equivalent and the trace does not consult the concept. | **MEDIUM** |
| `Assets/_Modules/Cosmetics/CosmeticApplier.cs:112` (and the duplicate at `:131`) | `ApplyCosmetic: applied '{def.DisplayName}' to '{gameObject.name}'.` | the cosmetic was applied | that `ApplyMaterial` / `ApplyPrefab` / `ApplyVfx` were **called** (`:108-110`) — all three return `void` and none is checked | `ApplyMaterial` early-returns with a `Fail` when there is no `MeshRenderer` (`:180-182`); the summary at `:112` prints `applied` regardless. Player equips a skin, nothing changes, log says applied. **Shape H5 in its purest form.** | **MEDIUM** |
| `Assets/_Modules/Cosmetics/CosmeticApplier.cs:189-197` | `// READ-BACK VERIFY: confirm the swap took` … `if (applied == null) { Fail } … Step("swapped to override material '{applied.name}'")` | a read-back verification that the material swap took | nothing. `meshRenderer.material` **instantiates and returns a non-null material on read** — the `applied == null` branch at `:191` is unreachable in practice | a swap to the wrong material, a material with a missing/pink shader, or a swap onto the wrong renderer all pass the "READ-BACK VERIFY". **Shape H2, and aggravated: the comment explicitly promises verification.** A line labelled `VERIFY` that cannot fail is the most misleading artifact in the file. | **MEDIUM** |
| `Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs:466` | `palette collapsed: all dock chrome hidden (no black wall)` | a **visual** outcome — no black wall on screen | that `Collapse` reached its end. The three `SetActive(false)` calls at `:459-:461` are each individually null-guarded, so zero of them may have run | if `_topBarGo`/`_trayGo`/`_crystalsRowGo` are null (never built this posture, or destroyed by a rebuild), nothing is hidden and the line still asserts "all dock chrome hidden (no black wall)" — naming the exact defect it cannot detect. Contrast `:457` one line earlier, which prints the three refs honestly as booleans and is **not** hollow. | **MEDIUM** |
| `Assets/_Modules/DevTools/AutoPilotDriver.cs:1769` | `AssertHeroHasAlbedo: PASS — 'WHITE HERO ROOT' did NOT fire this run (check ordered after the tint/texture binding; early-outs when either is bound).` | the hero is not white | that a *different* diagnostic did not raise. The parenthetical concedes the source check **early-outs** | a PASS asserted on the **absence of a signal from a check that admits it may not have run**. If `HeroBodySwapper`'s white-root check is skipped, refactored away, or its static flag is stale from a prior scene, `white == false` and this prints PASS. Note the sibling at `:1759` is **honest** — it compares `bound`/`total` counts. | **MEDIUM** |
| `Assets/_Modules/Core/Diagnostics/ScreenOpenWatchdog.cs:96` | `$"panel '{now}' opened"` | the named panel opened | `PanelManager.OpenPanelName` changed — i.e. someone called `NotifyOpened`. A bookkeeping write, not a render | borderline as a breadcrumb (it does state a true fact about arbiter state), but listed because it is the **third reader of the same flag**. When a panel registers-but-renders-nothing, this watchdog, `PanelRouter.cs:289` and `AutoPilotDriver.cs:5231` all agree the panel is up. Three corroborating traces from one hollow predicate is how a blank screen survives a capture review. Fix by naming what it measures — `panel '{now}' registered with the arbiter` — which costs nothing and stops the false corroboration. | **MEDIUM** |
| `Assets/_Modules/Dungeons/Wanderer/Bryn.cs:158` | `bubble={(_bubble != null ? "ok" : "MUTE")}` | Bryn's speech bubble works | the `as IWandererBubble` cast at `:137` returned non-null — the **seam exists** | WO-973, shipped: bubble covered ~60% of screen with clipped text; this printed `ok` throughout. Single NPC, hence MEDIUM not HIGH — but it is the canonical teaching case. | **MEDIUM** |
| `Assets/_Modules/Core/UI/ElarionUiKitObsidian.cs:1982` | `"BuildTargetFrame: composed ONE " + TargetPlatePx + "px plate (… HP band " + TargetHpBandPx + "px, ASCII hp readout)"` | a plate of a stated size **with an ASCII HP readout** | that the `Guard.Try` body reached its end. Both px numbers are `const`. The "ASCII hp readout" clause prints unconditionally, but the label is only created inside `if (h.hp != null && h.hp.valueLabel == null && h.hp.track != null)` at `:1971` | the prefab path yields `h.hp == null`, no value label is created, and enemy HP is conveyed by **bar colour alone** — the exact colourblind-accessibility defect the block exists to fix (memory `owner-colorblind-delegate-visual-creative`) — while the trace announces "ASCII hp readout". A trace that names an accessibility guarantee it did not verify is the highest-consequence variant of H5. | **MEDIUM** |
| `Assets/_Modules/Dungeons/Wanderer/WandererBubble.cs:124` | `_resizePending = 3;` (inside `Show`) | — (an **absence**, listed for the shape) | that the true panel size lands up to 3 frames after `Show` returns | this is the H4 generator: **any** size/extent assertion taken at `Show` time is structurally pre-settle. Today nothing traces the settled size at all, which is why WO-973 needed a human eye. The fix is additive: a post-settle `Step` when `_resizePending` hits 0 (`:87-90`) that prints the resolved `localScale` against a viewport-fraction ceiling. | **MEDIUM** |

### LOW — hollow tokens on otherwise honest lines

These lines carry real measurements *and* a hollow token. Fixing them is cheap; leaving them is survivable.

| file:line | the line | what it claims | what it actually proves | how it could lie | severity |
|---|---|---|---|---|---|
| ✅ **FIXED 2026-08-14 (WO-976)** — retokened to `heroRef={_hero.name}`, so a wrong-hero bind is now readable in the capture — `Assets/_Modules/HUD/Kit/HudCompassWidget.cs:529` | `(provider={(EnemyProvider != null ? "wired" : "NULL")}, hero={(_hero != null ? "ok" : "NULL")})` | the compass's hero reference is good | `_hero != null` | the wrong hero transform (a stale pre-reload instance, or a body proxy rather than the root) prints `hero=ok` and every bearing computed from it is wrong. Note `provider=wired` on the **same line** is the correct labelling — it says *wired*, and that is exactly what it proves. The two tokens next to each other are a ready-made before/after. | **LOW** |
| `Assets/_Modules/Core/UI/ElarionUiKit.cs:3317` | `"attack pill: inset icon well seated behind the glyph."` | the two well discs are **behind** the icon | that two `new GameObject(...)` succeeded — cannot fail. The load-bearing claim is the sibling ordering (`SetSiblingIndex(at)` at `:3308`, where `at` was captured **before** either insert), and nothing re-reads the final index | the comment at `:3313-:3315` concedes the prefab-bound icon "can be nested deeper than the constructed one" — so `at` may no longer correspond to the icon's position after two inserts, and a well draws **over** the glyph. The attack button shows a dark disc instead of a sword; the trace says "seated behind the glyph". | **LOW** |
| `Assets/_Modules/HUD/Kit/HudCompassWidget.cs:311` | `"heading strip built: full-width tape, fan={FovDegrees:0} deg, 8 cardinal ticks, gold diamond objective, apex-down centre caret."` | an itemised inventory of five built features | that the `Guard.Try` body ran. `FovDegrees` is a `const`; "8 cardinal ticks" is a string literal; the sprite and TMP assignments above are never null-checked | the TMP font or `EnemyPipSprite()` fails to resolve in a shipped WebGL build — caret and cardinal glyphs render as white boxes or nothing, the compass is a bare bar, and the line still itemises all five features as built. **An enumerated feature list built from literals is a marketing claim, not a measurement.** | **LOW** |
| `Assets/_Modules/Core/UI/ObsidianQueueGate.cs:42` | `"ObsidianQueueGate.RequestToggle — work-queue panel toggle requested"` | the work-queue toggle was delivered | that the method was entered; logged before a null-safe `?.Invoke()` | same shape as `HarvestPanelGate.cs:49`, and structurally worse — **`HasSubscriber` exists six lines above at `:37`** and the trace does not consult it. Ranked LOW only because `HudKitController.OnManageAction` guards the call at the one high-traffic site; any *other* caller gets a dead tap logged as a successful request. | **LOW** |
| ✅ **ANNOTATED 2026-08-14 (WO-976)** — kept deliberately advisory and now says so in the line itself (`ADVISORY, not a verify: addReturned=…`) so no reader mistakes it for coverage — `Assets/_Modules/Village/NPCs/CompanionGearSetup.cs:208` | `no GearLoadout on '{root.name}' — lazily added (result={(lo != null ? "ok" : "<null>")})` | the lazy `AddComponent<GearLoadout>` succeeded | nothing — `AddComponent` on a live GameObject does not return null. **Shape H2** | mitigated: the caller does `Fail` on a null loadout downstream, and the `FlowTrace.Try` wrapper at `:206` catches a throw. The token is decorative rather than dangerous. | **LOW** |
| `Assets/_Modules/Village/Hero/InventoryGrid.cs:40-41` | `store={(_store == null ? "NULL" : "ok")} inventorySource={(_store == null ? "NULL" : "store-ok")}` | the inventory data source is healthy | `_store != null`, **stated twice** — both tokens are the same predicate | a store present but empty-by-fault (catalog failed to hydrate) prints `store=ok inventorySource=store-ok`. Mitigated because the same line carries `ownedCounts={owned}` and `slots={slotCount}`, which are the real discriminators the comment at `:29-33` describes. Recommendation: drop the duplicate token, keep the counts. | **LOW** |
| `Assets/Editor/CraftPixPeopleBuilder.cs:402` | `"staged art verified: " + Bodies.Length + " FBX + the shared atlas under " + ArtDir + "."` | staged art was verified | `Bodies.Length` — the **compile-time length of a static array**, not the number of assets found on disk. The count can never disagree with the claim | editor-time only, and the missing-asset path does throw, so this is cosmetic H2 rather than a live lie. Listed for completeness and because the word **"verified"** is again attached to a constant. | **LOW** |
| `Assets/_Modules/Village/Hero/HeroLocomotion.cs:666-667` | `newInputKb={(Keyboard.current != null ? "OK" : "null")}, newInputGp={(Gamepad.current != null ? "OK" : "null")}` | input is working | a device is *present* | inverted hollowness: on the primary target (Android/Seeker) there is no keyboard **by design**, so this prints `newInputKb=null` — which reads as a defect on the platform where it is correct, and `OK` in the editor where it proves nothing about whether input is *reaching* the locomotion path. | **LOW** |

---

## 4. Honest counterexamples — copy these shapes

Nearly every hollow line in §3 has a correct sibling **somewhere in the same file or feature**. That is
the most encouraging finding of the sweep: the right pattern is already known here and already written
down in code. Use these as the template for every fix.

- **`Assets/_Modules/Pets/PetDeployer.cs:793-816` — `VerifyPetRenders`.** The single best assertion in
  the tree. It walks `MeshRenderer` **and** `SkinnedMeshRenderer`, counts only those with a non-null
  `sharedMesh`, prints the count on success and `Fail`s with a named consequence on zero: *"the pet will
  be invisible."* It measures a thing, states a threshold, and can fail. Every `x != null ? "ok"` in §3
  should become this.
- **`Assets/_Modules/Village/World/SceneTransitionTrigger.cs:683`** — `repositioned: requested
  {targetPosition}, hero now @ {playerTransform.position}`. **Requested *and* achieved, side by side.**
  This is the exact line `SceneLinkResolverHost.cs:192` should have been, and it already exists twenty
  files away.
- **`Assets/_Modules/Village/EconomyService.cs:416`** — `Grant +W{wood} +I{iron} -> GameState
  Wood={...} Iron={...} (kind={kind}, bankCap={applyBankCap})`. Post-clamp amount, resulting total, and
  the policy that governed it. The four callers in the economy table above should echo *this*, not their
  own request.
- **`Assets/_Modules/DevTools/AutoPilotDriver.cs:1692-1706` — `AssertCompassMarks`.** The reference
  implementation. It waits for an **active** widget (`:1675-1682`), then splits the two failure classes
  by name: `ActiveTickCount == 0` with a non-empty buffer is *built-but-invisible* (`Fail` at `:1698`),
  and a pip whose measured rect is below a stated **10×16 px visibility floor** is a sub-visible sliver
  (`Fail` at `:1703`). It measures a resolved size against a threshold and it can fail three different
  ways. This is what `AddressableUIManager:234` should look like.
- **`Assets/_Modules/DevTools/AutoPilotDriver.cs:1683-1685`** — when the layout half genuinely cannot be
  evaluated this posture, it emits a `Warn` reading *"layout half SKIPPED … Named skip, not silent."*
  **A named skip is not a hollow pass.** This is the correct treatment for the H3 dungeon case: the gait
  probe should say *"velocity-derived columns INERT — foreign mover owns the transform"*, not print `0.0`.
- **`Assets/_Modules/Dungeons/Wanderer/WandererBubble.cs:214-222`** — a real `Fail` on a real branch
  (no unlit shader resolved → the panel would render magenta). Named consequence, falsifiable condition.
- **`Assets/_Modules/Village/BuildMode/BuildPaletteUI.cs:745`** — `rows-added: built={built.built}
  failed={built.failed}`. Two counts from a `Guard.TryEach`. It can report partial failure, which is the
  whole point.

---

## 5. Honest scope statement

The sweep read `Assets/_Modules/` and `Assets/Editor/` and returned **44 entries** — 16 HIGH/CRITICAL,
20 MEDIUM, 8 LOW. Every line was opened at source; nothing here is grep-only.

**That is more than expected, but the codebase is still mostly clean, and both halves of that are real
findings.** Hundreds of candidate lines were opened and *rejected* because they were honest. The
prevailing idiom in this tree is genuinely good, and the hollow lines are a minority that deviated from
it in two specific ways.

Clean areas, verified and named so nobody re-sweeps them:

- **`Assets/_Modules/Onboarding/`** — clean. `LoginPanelController.HandleOutcome` and
  `LoginViewModel.NoteAccessGranted` sit on real `outcome.Success` branches with populated failure legs.
- **`Assets/_Modules/Pets/`** — clean, and the source of the tree's best assertion (§4).
- **`Assets/_Modules/Core/`** outside `Core/UI` — clean. `CoreServices.cs:55/119/191` explicitly print
  `"… registered as NULL."`
- **`Core/UI/HudAreasConfig.cs`**, **`Core/UI/QueueRailView.cs`**, **`HUD/DialogueView.cs`**,
  **`HUD/HelpMenu.cs`** — all print raw measured values (including zeros) rather than verdicts.
  `HelpMenu.RefreshWell` explicitly **refuses to claim a count off an unresolved rect** — a deliberate
  anti-H4 guard already in the tree.
- **Verified not-hollow after reading:** `Village/Buildings/Tower.cs:438`,
  `Village/Buildings/TowerConstruction.cs:129`, `Village/Catalog/StructureFactory.cs:227`,
  `Village/Hero/EquipmentPanel.cs:1244` (carries `ProbeRenderedContent` as a decisive draw check),
  `Village/Waves/WaveManager.cs:959`, `Core/UI/PanelRouter.cs:277-281` (the battle-lock carve-out).
- **Excluded as dev-only harness:** `Village/Buildings/TowerLoopDevHarness.cs`,
  `Village/Dev/ResourceDevTool.cs`, `Village/Diagnostics/PlayerBot.cs`,
  `Village/Diagnostics/CastleNavTopologyDiag.cs`, `Core/Dev/FlagCaptureButton.cs`, and the
  `Core/Diagnostics/` dump harnesses (`ArcaneTowerDiag`, `FloorDeepDiag`, `UICaptureMode`).
- **Almost no shape-H4 (pre-settle) findings.** The UITK surface here is dev-only, and the uGUI layout
  traces that could have qualified force a canvas update or bail on an unresolved rect first. The
  `WandererBubble` `_resizePending` case appears to be close to unique — worth knowing, since it is the
  shape the prompt expected to recur.

Two structural reasons the honest majority stays honest, both worth preserving:

1. **The dominant convention in this tree is already honest.** The prevailing idiom is
   `x != null ? x.Name : "<null>"` — it prints the *identity* on success and an unambiguous `<null>` on
   failure, so both states are distinguishable in a capture. That is a genuinely good habit and it is
   why the sweep is short. The hollow cases are the minority that substituted the **word `ok`** for the
   identity.
2. **The `Fail`/`Warn` branches are overwhelmingly real.** Dozens of candidate lines were opened and
   excluded because their `ok` token was exculpatory inside a genuine error report
   (`TowerSwapService.cs:174-175`, `BuildingPerkService.cs:209-210`, `DungeonBakerChecks.cs:326`,
   `AddressableUIManager.cs:175`).

**The concentration matters more than the count.** The 44 entries are not spread evenly — they fall into
three clusters, and each cluster has a single root cause:

1. **Visual/spatial claims from non-visual predicates** (~20 entries). Does it render, is it visible, is
   it the right size, is it behind the glyph. This is precisely the class where FlowTrace shows what the
   code *believes* and only a screenshot shows what the player *sees* (memory
   `screenshots-are-primary-evidence-for-visual-defects`). **Four separate traces
   (`PanelRouter.cs:289`, `ScreenOpenWatchdog.cs:96`, `AutoPilotDriver.cs:5231`, and
   `AddressableUIManager.cs:234`) read variations of one bookkeeping flag and then corroborate each
   other.** That is how a blank screen survives a capture review: it does not look like one unverified
   claim, it looks like four independent confirmations.
2. **Grant/payout claims echoing the request** (~6 entries, §3 economy table). The authority clamps
   honestly; the callers re-print their own argument. Player-visible currency loss with a log that
   contradicts it.
3. **"Armed / installed / subscribed" claims on the success path only** (~8 entries). The failure exit
   is a bare `return` with no trace (`DungeonController.cs:1386`,
   `HubStructureVisualInjector.cs:619`). These are the hardest to notice, because the evidence of
   failure is an *absence* of a line — indistinguishable from a log you scrolled past.

Cluster 3 deserves the §12 emphasis: **a silent early-return next to a success `Step` is the generator
of hollow assertions.** Fix the silent return and the success line stops being able to lie.

---

## 6. Proposed standing rule for `docs/INSTRUMENTATION_STANDARD.md`

To be added as **§2.7 — Falsifiability** (after the authoring checklist), pending owner ruling.

> ### A trace field that cannot report failure is a bug, not a nicety.
>
> Before you write a success token, answer: **what realistic broken state makes this print something
> else?** If you cannot name one, you have not written instrumentation — you have written a comment
> that costs a string allocation and misleads the next reader. Delete-and-move-on is not the fix
> (§12: instrumentation is permanent); **widen the assertion until it can fail.**
>
> **Assert the measured, not the derived:**
>
> | Instead of… | assert… |
> |---|---|
> | `panel != null ? "ok"` | the **resolved rect** against a floor — `w×h px >= 10×16`, and `>0` after layout |
> | `canvas != null ? "ok"` | resolved `opacity`, `sortingOrder` vs. the topmost scrim, and whether the rect intersects the viewport |
> | a wiring seam being non-null | say **`wired`**, never `ok`. `ok` is a health word; reserve it for health |
> | an `AddComponent` / freshly-assigned field | nothing — the check is unreachable. Assert the **effect** instead (did the component do its job) |
> | a value read at `Show()` / build time | the **post-settle** value: trace on the frame the pending counter drains, not the frame it is armed |
> | `x.Velocity` where another component may own the transform | the **measured** delta (`Δposition / Δt`), which is mover-agnostic — see `HeroLocomotion.ResolveAnimatorFeed` (`HeroLocomotion.cs:388-393`) for the pattern already in tree |
> | an unconditional "done/applied/hidden" after a sequence of `void` calls | make each step return a result and print the **tally** — `built=N failed=M`, the `Guard.TryEach` shape |
> | a PASS on the absence of another check's signal | assert the check **ran** first, then assert its verdict. An unrun check is a **named skip** (`Warn`), never a pass |
> | the amount you **requested** from a granter | the amount **credited** and the **resulting total** — see `EconomyService.cs:416`. If the API returns nothing, fix the API first; the trace is only reporting its blindness |
> | a target position you **intended** to move to | the position the object **actually occupies afterwards** — print both, as `SceneTransitionTrigger.cs:683` does |
> | a `const`, a string literal, or an array's compile-time `.Length` | a runtime count of what was found. A number that cannot move is not a measurement |
>
> **Two rules that generate the rest:**
>
> 1. **Every silent early-return beside a success `Step` is a hollow-assertion factory.** If a method can
>    exit without tracing, its terminal success line is unfalsifiable by construction — the reader cannot
>    distinguish "it failed early" from "I scrolled past it". Trace the early exit (`Warn` naming the
>    consequence) and the success line becomes honest for free. This is the cheapest fix in the catalogue
>    and it closes ~8 of the 44 entries.
> 2. **Report the variable you name.** `WaveFeedbackDirector.cs:321` prints `hudBound={CoreServices.Hud
>    != null}` while the HUD it actually bound is a hardcoded `null` from a stub. Before shipping a
>    token, trace the identifier back to what the code under test actually used.
>
> **The context rule (shape H3, the one that gets past review):** before trusting a derived quantity,
> ask *"is this source zero or empty **by design** in the context where this trace fires?"* If a value
> can be structurally inert in some scene, posture, or ownership regime, the trace must either read the
> ownership and say **INERT**, or read a source that is valid in every regime. A probe that silently
> reports its own inertia as a perfect score is the most expensive failure in this catalogue.
>
> **The screenshot corollary:** for any assertion about a **visual** outcome — renders, visible, sized,
> positioned, hidden — a trace field is supporting evidence, never proof. FlowTrace shows what the code
> believes; the screenshot shows what the player sees. Pair every visual assertion with a
> `RunCaptureHeadless` PNG and open it (CLAUDE.md §8, `UI_CAPTURE_OK`).

---

## 7. Maintenance

Per the project's registry convention (memory `audit-outputs-as-known-dictionaries`), this file is a
**durable dictionary**, not a dated report.

- **When you fix an entry:** leave the row, strike the line, and add the replacement `file:line` + what
  it now measures. The registry's value is the *catalogue of shapes*, and a fixed entry teaches better
  than a deleted one.
- **When you find a new one:** add it, ranked, with a concrete lying scenario. An entry without a named
  concrete failure that prints `ok` is speculation and does not belong here.
- **When you write new instrumentation:** run the §6 falsifiability question before the success token
  goes in. This registry exists to stop being appended to.
