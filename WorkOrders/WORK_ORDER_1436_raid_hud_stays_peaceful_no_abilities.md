# WO-1436: P0 - the raid HUD stays in PEACEFUL posture, so the hero has no ability buttons and cannot fight

**Status:** READY TO IMPLEMENT - **P0, the raid core loop is unplayable.**
**Silo:** HUD context/posture + the raid scene bootstrap. Disjoint from WO-1435 (rail geometry) and from
the Manage 2000-block.
**Source:** owner felt-test 2026-09-06 on build **2026.09.06.358161**, verbatim:
> *"in the raid, i had no way to fight. No combast skills"*

**Evidence: 25 MB of device logcat captured to `logs/debug/raid-no-abilities-2026-09-06.log`** while the
session was still live. Every line below is quoted from it.

---

## 1. THE RAID RAN. THE HERO DIED IN 45 SECONDS.

```
[Flow:Raid] stars settled: 0 (cleared=False destruction=32 % elapsed=45s/180s underTime=True survival=100 %)
[Flow:Raid] raid scored: 0 star(s), 32% razed, 44.6s, cleared=False, deployed=10.
[Flow:Raid] hero death settle: partial loot for 32% razed.
```
Scene `RaidBase_raider_camp_small` loaded correctly (133 lines carry that scene name). **This is not the
WO-1109 pill-hero problem and it is not a missing scene.** The raid worked; the HUD did not.

## 2. THE ABILITIES WERE PUSHED. RULED OUT AT SOURCE - DO NOT RE-INVESTIGATE THIS.

```
12:58:57.512  [Flow:HudKit] VillageHudController bootstrapped the kit (scene 'RaidBase_raider_camp_small')
12:58:57.534  [HeroAbilitiesHudBridge] Pushed ability bar for class 'mage': Fireball, Arcane Shell, Drain, Poison Cloud
```
The push is **22 ms after** the raid scene's kit bootstrap, so it happened **inside the raid scene**. The
command bridge registered too:
```
[Flow:HudKit] command bridge registered (attack, block, cycleSelect, potions, assignable) for scene 'RaidBase_raider_camp_small'
```
**The loadout existed. The bridge existed. The player still had no buttons.**

## 2B. ⛔ CORRECTED 2026-09-06, MID-TICKET, BY A DEVICE SCREENSHOT. READ THIS BEFORE SECTION 3.

**The first root cause written below was WRONG and is corrected here rather than deleted, because the
mistake is instructive.** A second `adb screencap`, taken while the owner was mid-raid
(`1:58` remaining, `Razed 79%`, `SPIRE DOWN`), shows what the log could not:

**THE ABILITY FACES ARE RENDERING.** Visible along the bottom: **`CAST · BLOCK · Arcane Bolt · Mend ·
EMPTY · ITEM`**. They are **drawn UNDERNEATH the raid deploy bar** (`FOOTMAN` / `ARCHER` / `Rally ON` /
`RETREAT`), which paints over them. The player cannot reach them - which is indistinguishable, from the
chair, from not having them.

**AND THE ENTIRE TOWN HUD IS STILL LIVE INSIDE THE RAID.** Also on screen, over a battlefield:
`Thrain Lv 5` + portrait, the **compass**, the **wave timer**, the **FLAG** button, **`Echoes 4/6`**, and
a **`REPAIR ALL - Wood 263 Iron 133`** button in the middle of the screen. All of it washes over the raid's
own chrome (`SPIRE DOWN`, `Troops 8/10`, `Razed 79%`), which is why the top strip reads as semi-transparent
mush.

### THE ACTUAL DEFECT: entering a raid never tears down the town HUD. Two complete interfaces are live at
once, and the one on top is the one that cannot fight.

**This does NOT invalidate section 3 - it explains it.** The peaceful posture is exactly why the town
widgets stayed live. Posture is the CAUSE; the stacked bars and the unreachable ability row are the
SYMPTOM the player actually experiences. **Fix the posture and the teardown; verify by SCREENSHOT, because
this defect is invisible to the log** - the log truthfully reported that abilities were pushed, and they
were.

⚠ **Methodology note, worth more than this ticket:** the log said "abilities pushed" and that was TRUE. A
log-only reading concluded "no faces rendered", which was FALSE. **For a visual or spatial defect the
screenshot IS the primary evidence** - FlowTrace shows what the code believes, a screenshot shows what the
player sees (memory: `screenshots-are-primary-evidence-for-visual-defects`). The corrected conclusion cost
one capture.

## 3. THE POSTURE CAUSE - EVERY COMBAT INPUT IS FALSE INSIDE THE RAID

The same line, repeated for the whole raid:
```
[Flow:HUD] context inputs: wave=False battleLock=False pursuit=False inVillage=False
           modal=False buildMode=False scene='RaidBase_raider_camp_small' -> Overworld
```

**`wave`, `battleLock` and `pursuit` are ALL False in a raid scene.** Nothing tells the HUD that the
player is in combat, so the posture resolves to **Overworld** - the peaceful dock - and the bar renders
BUILD / TALK / HERO / JOURNEY / MANAGE instead of ability faces.

The bar was working exactly as designed. It was asked the wrong question.

⚠ **Do NOT "fix" this by force-showing abilities from the ability bridge.** That treats the symptom and
leaves the posture lying to every other consumer that reads it. **The raid scene must declare combat**;
the posture then does the right thing everywhere at once. Find the seam that sets these inputs
(`[Flow:HUD] context inputs` is emitted by the HUD context resolver - read it and follow its sources) and
make a raid scene set the combat input the same way a wave does.

## 4. THE OTHER FINDING IN THE SAME TRACE - the deploy modal freezes the world

```
[Flow:HeroOwner] WORLD CLOCK FROZEN: Time.timeScale=0.00 in scene 'RaidBase_raider_camp_small'.
                 The hero CANNOT move, turn or animate while this holds
```
Repeated once a second from 12:58:58 to 12:59:02 - **~4 seconds of a 180 s raid** spent frozen while
`modal=True`. The raid timer is a scored resource (`elapsed=45s/180s` feeds `underTime`). Determine
whether the raid clock runs during that freeze; if it does, the player is being charged for a modal.
**Report it either way - do not silently retune the timer.**

## 5. WHY NO ORACLE CAUGHT THIS

394+ suites are green and the raid HUD has no ability buttons. Every existing HUD oracle asks *"does the
bar render its faces correctly for a posture?"* - and it does. **None asks "is the posture RIGHT for the
scene the player is standing in?"** This is the same species as WO-1430's doorless panels: the parts all
work, the connection between them was never checked.

## 6. ACCEPTANCE

- [ ] Loading a raid scene resolves the HUD posture to a COMBAT posture, proven by a regression that
      **asserts the resolved posture for a raid scene**, not by a screenshot alone.
- [ ] **A new seam oracle: for every scene in the build list, the resolved HUD posture is asserted
      against that scene's declared kind.** This is the general form and it is what would have caught it.
      A raid scene resolving to a peaceful posture must FAIL the build.
- [ ] The ability faces are present and tappable in a raid, proven by a headless capture with the **PNG
      opened and looked at** (memory: `headless-screenshot-verify-ui-before-build`).
- [ ] Section 4 answered with data.
- [ ] `REGRESSION_OK n/n`.

---

## 7. IMPLEMENTATION RECORD — 2026-09-06 (edit-only lane; NOT gated, NOT committed)

**Status line deliberately NOT flipped** — the CLI lead holds the Unity lock and is sole committer
(CLAUDE.md §2/§11). This section records what was edited and what remains unproven.

### 7.1 How the posture is now declared
`DeNelle.Core.HubScenes.SceneDeclaresCombat(sceneName)` — **TODAY EXACTLY `IsRaid`**, and that
narrowness is the point. `HudContextEvaluator` feeds it into the `combat` input as `sceneCombat`,
alongside wave / battleLock / pursuit, so the posture resolves `Battle` → `hostile(activebattle)` for
the **whole** assault instead of flapping with pursuit pulses.

`IsRaid`, **not** `IsEnemyOwnedScene`: `Village2` carries `ownership:"Enemy"`, so keying combat off
enemy-ownership would drag the raid-target village into a permanent battle posture and overturn the
owner's 2026-07-05 "peaceful default on enemy ground" ruling. A `RaidBase_*` scene is reachable only
through BEGIN ASSAULT with troops committed and a scored clock. **The 07-05 ruling is refined, not
overturned**, and both headers now say so.

### 7.1b Two side-effects of touching shared code, both checked, both intended
- **`waveBlock` has TWO visibility writers.** `HudKitController.OnWave` (event-driven) also
  `SetActive`s it, so the new hub gate would have fought a wave-model event for a one-frame flash of
  a TOWN WAVE TIMER over a battlefield. Both writers now consult one cached answer
  (`RefreshHubGateCache`). `heartStatus` had no second writer, which is why its gate worked alone —
  generalising it without checking would have shipped a flicker.
- **`EchoUnlockFeedback.IsGameplayScene` has a second caller** (`:187`), a HARD hold on the founding-
  tale dialogue card. Enemy-owned ground now holds that card too. Intended: it is a quiet story beat
  and must not fire mid-assault. It is **held, not burned** — the existing pending-retry loop
  re-evaluates until the holds clear, so it lands when the player is back in town.

### 7.2 Section 4 — ANSWERED WITH DATA. The player is NOT charged for the modal freeze.
`RaidScoring.cs:715` is `_elapsed += Time.deltaTime` — **scaled** time, so at `timeScale=0` the raid
clock does not advance. Confirmed against the capture:

| measurement | value |
|---|---|
| `RAID START` | `12:58:58.335` |
| `raid scored` | `12:59:47.749` |
| wall-clock delta | **49.4 s** |
| reported `elapsed` | **44.6 s** |
| unbilled | **4.8 s** — matches the ~4 s `WORLD CLOCK FROZEN` window |

**No retune. Nothing to fix here.**

### 7.3 ⛔ THE P0 IS NOT CLOSED BY THIS CHANGE. A SECOND, MEASURED DEFECT SITS ON TOP OF IT.
The ability faces are unreachable because **the raid deploy bar paints over the action bar**, and
that is now proven from source, not inferred:

| surface | Y band (viewport) | canvas sortingOrder | file |
|---|---|---|---|
| kit `actionBar` (holds `combatDock`) | **0.015 – 0.150** | **4000** | `HudAreasHost.cs:135`, `:111` |
| raid deploy bar panel | **0.010 – 0.160** | **30000** | `RaidDeployController.cs:868`, `:865` |

Same band; the deploy canvas is 26 000 layers above. **The posture fix makes this WORSE from the
chair**, and that must not be a surprise at the gate: before it, the raid sat in `calm(explore)` most
of the time (posture flapped 7× in 49 s), so the deploy bar mostly covered a *peaceful* dock. After
it, `combatDock` is up for the whole raid — permanently underneath a permanently-present deploy bar.

**Not touched, on purpose.** The instruction "do not fix the overlap by nudging the deploy bar's Y"
is reasoned on the thing underneath being *town HUD that should not be there*. It is not — it is
`combatDock`, which is exactly what this ticket wants reachable. So the premise does not hold, and
per CLAUDE.md §11B.B the deviation is **raised, not taken**. There is also no clean seam today:
`RaidDeployController` is in `DeNelle.Village`, which may not reference `DeNelle.HUD` (§5), so the
raid HUD cannot ask the kit for a free band. **Owner ruling needed** — the principled fix is a Core
seam carrying a reserved bottom band that the HUD writes and the Village reads (the `CoreServices`
pattern already used everywhere), not a hardcoded Y on either side.

### 7.4 What could not be proven from this lane
No headless capture was taken: this is an edit-only lane and the lead holds the Unity lock. **The PNG
has not been opened, so ability reachability in a raid is NOT proven** — and per §2B it is precisely
the thing the log cannot answer. The `UI_CAPTURE_OK` harness cannot supply it either: it is
editor-only, never enters Play mode, and boots the Castle hub (`UICaptureMode.BootScene =>
SceneRouter.Castle`), shooting *panels*, not in-world HUD.

The path that can is the AutoPilot fleet, which runs Play mode, accepts a boot-scene override
(`--scene=<name>` / `AUTOPILOT_SCENE`, `AutoPilotDriver.cs:162-168`) and already writes PNGs via
`ScreenCapture.CaptureScreenshot` into `<persistentDataPath>/ui-shots`. Boot it at
`--scene=RaidBase_raider_camp_small` and open the frame.

---

## 8. §7.3 CLOSED — the deploy bar now stacks ABOVE the ability row (edit-only lane, 2026-09-06)

**OWNER RULING 2026-09-06:** the **ABILITY ROW owns the thumb position** at the bottom of the screen;
the raid deploy bar (FOOTMAN / ARCHER / Rally ON / RETREAT) **stacks above it**. Her reasoning is the
frequency argument — **casting is constant, deploying is occasional**, so the constant action belongs
under the thumb. §7.3's *"owner ruling needed"* is answered; the posture fix in §7.1 **stays as-is**.

### 8.1 The shared band lives in `DeNelle.Core.UI.HudLayoutBands`, and only there

| edge | value | authored at |
|---|---|---|
| `ThumbActionRowMinY` / `MaxY` | 0.015 / 0.150 | `Assets/_Modules/Core/UI/HudLayoutBands.cs` |
| `ThumbBandClearanceGap` | 0.010 | same file |
| `BottomOverlayFloorY` (derived) | **0.160** | same file |
| deploy bar band (derived) | **0.160 – 0.310** | `RaidDeployController.DeployBarBand` |
| deploy status band (derived) | **0.320 – 0.360** | `RaidDeployController.DeployStatusBand` |

**Why that file and not either surface.** `RaidDeployController` is `DeNelle.Village`, which may not
reference `DeNelle.HUD` (CLAUDE.md §5 — the one asmdef-enforced invariant; `AdminOverlay`'s reflection
exists *because* of it). So the raid HUD cannot ask the kit for a free band. `DeNelle.Core` is the only
assembly both sides already reference, and `HudLayoutBands` is the file that **already** solves this
exact problem for the shared `ToastZone` — reused, not invented. `HudAreasHost` now reads its ActionBar
mount's **Y** from it; the **X** edges stay on `HudAreasHost` because nothing outside `DeNelle.HUD`
needs them and `HudDockLayoutRegression.cs:373` pins their source text verbatim.

**A number typed into both files was never on the table:** that is the duplicated-state failure
CLAUDE.md documents four separate times (§2 stale WO block, §5 retired dependency table, §8 restated
`MaxVisibleFaces`, §16 copy-pasted R2 verify). Every one was correct the day it was written.

### 8.2 The oracle — `RaidHudThumbBandRegression` `[raid-thumb-band]`
`Assets/Editor/Regression/RaidHudThumbBandRegression.cs`, registered in `DataRegression.cs` beside
`[hud-ui-sme]`. It measures **from the authored anchors on both sides** (`HudAreasHost.ActionBarMinX/
MaxX` + `HudLayoutBands.ThumbActionRow*` vs `RaidDeployController.DeployBarBand`, the same accessor
`BuildHud` consumes), never from figures copied into the oracle. It answers §5's open question in its
general form: not *"does each surface lay itself out?"* — both always did — but *"can two surfaces in
assemblies that cannot see each other occupy the same pixels?"*

**RED PROOF (stated from HEAD's literals — NOT executed; this is an edit-only lane and the lead holds
the Unity lock).** Against the build the owner played, all four go red:
1. deploy bar 0.010–0.160 vs ability row 0.015–0.150 → `Intersects` **true** → `[buried-abilities]`.
2. `DeployBarBand.yMin` 0.010 < `BottomOverlayFloorY` 0.160 → `[thumb-floor]`.
3. `RaidDeployController.cs` held `new Vector2(0.98f, 0.16f)` / `new Vector2(0.02f, 0.01f)` → `[shared-seam]`.
4. `HudAreasHost.cs` held `new Vector2(ActionBarMaxX, 0.150f)` → `[shared-seam]`.

Cases 3–4 are the ones that outlive today's numbers: they fail the build if **either** seat ever
re-types the band as a local literal.

### 8.3 Two side-effects of moving the bar, both measured, both reported not fixed

- **`HudLayoutBands.ToastZone` (y 0.203–0.308, x 0.375–0.625) is now WHOLLY INSIDE the deploy bar's
  band** (it grazed the old bar by 0.002). Three consumers: `HubRepairAffordance` — **inert in a raid**,
  the §7 lane gated it out via `HubScenes.SuppressTownHud`; `HudKitController.ShowToast` and
  `BreakCaptureHarness`'s F8 `FLAGGED` acknowledgement — **both CAN fire in a raid scene, and would
  draw over the deploy bar for their 2–3.5 s lifetime.** Transient dev/feedback chrome over occasional
  chrome, so it is not the P0 — but it is a real new overlap and it is **not** proven harmless.
- **The movement stick is still covered, and my change moves WHICH HALF.** `MoveCluster` mount is
  x 0.010–0.270, y 0.030–0.330; `ElarionUiKit.BuildAnalogStick` centres a 236×236 reference-unit ring
  at the mount's midpoint, i.e. **x 0.085–0.195, y 0.058–0.302** at the owner's 2670×1200. The old bar
  (0.010–0.160) covered its lower ~42 %; the new bar (0.160–0.310) covers its upper ~58 %. **Covered
  before, covered after — larger now.** Not a defect this ticket created, and out of the ruling's
  scope, so it is raised rather than taken (CLAUDE.md §11B.B). **The clean fix is the same seam
  applied to the other axis:** move `MoveCluster`'s right edge into `HudLayoutBands` and start the
  deploy bar's x there (~0.28) instead of 0.02 — which needs a ruling, because `HudAreasHost
  .ActionBarMinX = 0.270f` shares that edge and its **source text is pinned** by
  `HudDockLayoutRegression.cs:373`, so it cannot simply be re-pointed at Core.

### 8.4 Not proven by this lane
No gate, no regression run, no capture — edit-only, per the lead's instruction. **Ability
reachability in a raid remains unproven by screenshot** (§7.4 still stands); the AutoPilot path named
there is still the one that can close it.
