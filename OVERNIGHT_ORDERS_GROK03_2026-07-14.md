# OVERNIGHT ORDERS — Grok-03 Here→There (for CLI)

**Owner:** Samantha · **Issued:** 2026-07-14 (late) · **Audience:** overnight CLI only  
**Notion:** owner will sync later — **git WorkOrders are the authority tonight**  
**Program index:** `docs/UI/Grok-03-here-to-there-WO-program.md`  
**Guidance:** `docs/UI/Grok-02-Obsidian-UI-guidance.md` · `docs/vfx/Grok-01-VFX-guidance.md`  
**Canon boot:** `START_HERE.md` → `CANON_GROUND_TRUTH_2026-07-13.md` → this file → the WO you execute  

---

## 0. Mission (one paragraph)

Move the game from **hybrid/unstyled UI + tool-strip build chrome + half-wired Hovl** toward **demo-ready Obsidian + CoC-simple Build HUD + real combat VFX**. Do **not** invent a new UI system. Do **not** run the full 07-03 HUD demolition. Follow the minted WOs **716→722 + 715** in the order below. **Push only if the owner already authorized a push; default = commit local, push HELD.**

---

## 1. Non-negotiables (read once, obey all night)

| # | Rule |
|---|---|
| 1 | **Instrument / verify before claiming fixed.** Gates: `COMPILE_GATE_OK` + brace/NUL on every `.cs` touched. |
| 2 | **No UXML/UIDocument for new UI.** Code-built uGUI + `ElarionUiKit` only. |
| 3 | **No hand-edit `.unity` scenes.** No bash redirects into `.cs`. Windows path Write/Edit only. |
| 4 | **Unity editor CLOSED** for batchmode bake/build/gate. |
| 5 | **Sole committer:** explicit paths only — **never** `git add -A`. One committer. |
| 6 | **Push HELD** unless owner already said push. Preview deploy only if explicitly in a WO (tonight: **no prod**). |
| 7 | **Sprite-first fallbacks.** Never blank a panel if art missing. Never reference `Assets/Blink/**` at runtime. |
| 8 | **ASCII TMP only.** No color-only meaning (owner colorblind). |
| 9 | **Do not mint new WO numbers** for this arc — use 715–722. Next free is **723**. |
| 10 | **Stop after two failed fix attempts** on the same issue → write `logs/debug/` note, park, continue other WO. |

---

## 2. Success for *this overnight* (realistic)

### Must ship before morning (minimum viable overnight)

| Done means | WO |
|---|---|
| Windows **exe exists** and boots | **716** partial |
| `UI_REVIEW/INDEX.html` (or panel PNGs) on disk for critical screens | **716** |
| `UI_REVIEW/PAIRWALK_716.md` ready for owner PASS/FIX | **716** |
| At least **one** of: kit-law oracle green **or** unstyled fixes on 2+ known panels **or** Build HUD scaffold landed | **718** and/or **717** and/or **719** A |

### Stretch (if time / gates green)

| Done means | WO |
|---|---|
| Unstyled-class fixes on founding + shop + build chrome | **717** |
| `KIT_LAW_OK` oracle in Regression | **718** |
| Build HUD: single place-intent bar (no dual rotate) + wallet chips | **719** B |
| Hovl: tower travel key attached OR knight `vfxKey` rows for attack1–3 | **715** slice B or C |

### Explicitly NOT required by morning

- Full WO-715 all slices  
- WO-720 (needs owner PASS/FIX from 716 — **blocked on owner**)  
- WO-721 / 722  
- Notion updates  
- Owner felt-pass  
- Push / prod  

---

## 3. Execution order (strict)

```
STEP 1  →  WO-716  Capture + pair-walk gate     [P0, unblocks truth]
STEP 2  →  WO-718  Kit-law oracle               [parallel-safe after 716 starts]
STEP 3  →  WO-717  Unstyled-class kill          [use known offenders if 716 shots late]
STEP 4  →  WO-719  Build HUD CoC                [after 717/718 if possible; else after 716]
STEP 5  →  WO-715  Hovl VFX                     [parallel lane if file-disjoint from 719]
PARK    →  WO-720 until owner fills PAIRWALK PASS/FIX
PARK    →  WO-721 / 722 until Wave A+B green or owner says otherwise
```

**If overnight is short (pick one path):**  
**Path A (recommended):** 716 complete → 718 → start 717 on Title/Shop/Build.  
**Path B (build-demo bias):** 716 complete → 719 core (one canvas + merge rotate) → 718.  
**Path C (combat juice bias):** 716 complete → 715 Slice B (tower travel) only.

---

## 4. Per-WO: explicit CLI instructions

### WO-716 — Capture + pair-walk gate  
**Spec:** `WorkOrders/WORK_ORDER_716_capture_pairwalk_gate.md`  
**Status target by morning:** RESULT written; owner can open INDEX without CLI present.

```
1. Confirm Unity editor is NOT running.
2. Rebuild Windows:
   powershell -ExecutionPolicy Bypass -Command "Remove-Item -Recurse -Force 'Builds\Windows' -ErrorAction SilentlyContinue; .\build-windows.ps1"
3. On SUCCESS exe:
   .\run-autopilot-fleet.ps1 -Count 1 -SeedStart 9500 -TimeoutMin 12 -Graphics
   (If Graphics flag unsupported, use the project's documented graphics capture path from run-defenders skill — DO NOT invent; read skill.)
4. .\build-ui-review.ps1  → expect UI_REVIEW/INDEX.html
5. Create UI_REVIEW/PAIRWALK_716.md with columns:
   Screen | Capture path | PASS/FIX | Owner notes
   Pre-fill rows: Title, HeroSelect, FoundingDialogue, BuildPalette_Town, BuildPalette_Defenses,
   UpgradePanel, Shop, SettingsPause, WaveReport, CombatVitals
6. Write WorkOrders/WORK_ORDER_716_capture_pairwalk_gate.RESULT.md
   - exe timestamp
   - capture paths
   - anything blank/failed (honest)
7. Commit (if green): explicit paths only — build scripts none; docs + UI_REVIEW + RESULT.
   Message: "docs/gate(716): pair-walk capture baseline for Grok-03"
```

**STOP if:** build fails twice → log errors to `logs/debug/OVERNIGHT_716_BUILD_FAIL.md`, do **not** burn the night on build; switch to **718** (no exe required).

**Owner morning action:** open INDEX + mark PASS/FIX. CLI does **not** mark PASS for the owner.

---

### WO-718 — Kit-law regression oracle  
**Spec:** `WorkOrders/WORK_ORDER_718_kit_law_regression.md`  
**Status target:** `KIT_LAW_OK` or report with hits + allowlist.

```
1. Add Assets/Editor/Regression/ kit-law check (name clearly, e.g. KitLawRegression).
2. Scan consumer UI code for:
   - Image.Type.Filled outside ElarionUiKit* / allowlisted bar helpers
   - Allowlist: ElarionUiKit*.cs, RpgUiCatalog.cs, known bar helpers
3. Print KIT_LAW_OK / KIT_LAW_FAIL with file:line.
4. Wire into existing DataRegression or document how to run standalone via run-unity-method.
5. RESULT with sample output.
6. Commit explicit paths: "test(718): kit-law regression oracle"
```

**Do not** fix every hit tonight unless a hit blocks compile. Oracle first.

---

### WO-717 — Unstyled-class kill  
**Spec:** `WorkOrders/WORK_ORDER_717_unstyled_class_kill.md`  
**Status target:** ≥2 demo-critical surfaces use real frames; no mask-fill on those.

```
1. Grep for BuildObsidianPanel / BuildObsidianModal without frameName.
2. Grep for solid fill / ObsidianFill full-bleed that may mask frames (Shop/Inventory history).
3. Fix priority order: Title → Shop/PartyShop → Build palette chrome → Settings.
4. Prefer frameName: RpgUiCatalog.Frame* per Grok-02 table.
5. FlowTrace once per open: [Flow:UiChrome] frame=… nullSprite=…
6. COMPILE_GATE_OK + brace/NUL.
7. RESULT lists screens fixed.
8. Commit: "ui(717): kill unstyled/mask-fill on <screens>"
```

**Do not** restyle every panel in the game. Cap overnight at **demo-critical** list unless trivial.

---

### WO-719 — Dedicated Build HUD  
**Spec:** `WorkOrders/WORK_ORDER_719_dedicated_build_hud_coc.md`  
**Status target (overnight stretch):** one intent bar for place (no dual rotate); wallet chips if easy.

```
1. SME-read BuildPaletteUI, BuildPlaceButton, LeanTouchBuildDriver, BuildSelectionUI, BuildModeController enter/exit.
2. FIRST vertical slice ONLY if time-limited:
   a) Single parent canvas for place intents OR delete duplicate Rotate from one of PlaceButton vs TouchBar.
   b) BuildWalletRow (or CurrencyChip strip) for wood/iron/food — not "Crystals: N" alone.
3. Do NOT rewrite PlacementGrid / StructureFactory / place cost math.
4. FlowTrace [Flow:BuildHud] state=…
5. Touch path: PLACE button must still work (web/mobile law).
6. COMPILE_GATE_OK. Commit: "ui(719): build HUD <what landed>"
```

**If conflict with 715 files:** 719 owns BuildMode UI; 715 owns VFX keys/call sites — stay file-disjoint.

---

### WO-715 — Hovl combat VFX  
**Spec:** `WorkOrders/WORK_ORDER_715_hovl_towers_melee_spell_vfx.md`  
**Guidance:** `docs/vfx/Grok-01-VFX-guidance.md`  
**Status target (overnight stretch):** Slice B **or** Slice C, not both if tired.

```
PREFERRED overnight slice — B (towers):
1. TowerCombat: TravelKeyFor(element) + attach Hovl loop to projectile (mirror RangedAttackVFX.PlayHovlTravel).
2. Soft-stop on impact (default Stop).
3. FlowTrace cast/travel/impact keys.
4. COMPILE_GATE_OK. Commit: "vfx(715B): tower Hovl travel triplet"

ALTERNATE — C (melee registry):
1. motion-castings.json knight attack1/2/3/heavy → vfxKey Melee_Slash / Melee_Impact, manual:true, vfxDelay ~0.18.
2. Dual-copy Resources if runtime reads Resources.
3. Commit data + note registry-only path.

DO NOT: edit Assets/Hovl Studio prefabs; re-tune bloom (already WO-689); HS_ProjectileMover.
```

---

### WO-720 / 721 / 722 — PARKED for overnight

| WO | Why parked |
|---|---|
| **720** | Needs owner PASS/FIX on PAIRWALK_716 — **do not invent PASSes** |
| **721** | After Wave A/B; vitals only when not thrashing build/UI |
| **722** | Breadth only after demo path green |

**Morning CLI after owner marks pair-walk:** start **720** on FIX rows only.

---

## 5. Parallelization map (if multiple agents)

| Agent | Owns | Must not touch |
|---|---|---|
| A | 716 build/capture/docs | gameplay systems |
| B | 718 regression | BuildMode UI files |
| C | 717 panel consumers | Hovl generator / motion-castings |
| D | 719 BuildMode UI | VillageHudController vitals |
| E | 715 VFX / motion-castings / TowerCombat travel | BuildPaletteUI layout |

Orchestrator: **one batch CompileGate** after lanes merge; **commits by explicit path per lane**.

---

## 6. Morning handoff template (CLI fills this)

Paste into `MORNING_BRIEF` or chat:

```
## Overnight Grok-03 report
- HEAD: <sha>
- Branch: <branch>
- Commits landed (local): <list>
- Push: HELD / pushed (if authorized)

### WO-716
- Exe: YES/NO  path/timestamp:
- UI_REVIEW/INDEX.html: YES/NO
- PAIRWALK_716.md: YES/NO
- Blockers:

### WO-718
- KIT_LAW_OK / FAIL hits count:
- Commit:

### WO-717
- Screens fixed:
- Commit:

### WO-719
- Landed slice:
- Dual-rotate gone? YES/NO
- Wallet chips? YES/NO
- Commit:

### WO-715
- Slice B/C/none:
- Commit:

### Owner actions required
1. Mark UI_REVIEW/PAIRWALK_716.md PASS/FIX
2. Felt-pass: <what>
3. Authorize push? 

### Recommended morning CLI next
- WO-720 from FIX list / continue 719 / 715
```

---

## 7. Commit hygiene tonight

- Message format: `ui(716): …` / `test(718): …` / `vfx(715B): …` / `docs(…): …`  
- Stage **explicit paths only**  
- Include RESULT files in the same commit as the work  
- If dirty tree had unrelated deletions (asset packs) — **do not** stage them  
- Update RESULT status line: `IMPLEMENTED (overnight)` + what remains  

---

## 8. Definition of “stop and sleep the gate”

Stop coding and write the morning report when:

1. **716** baseline is done **or** build is twice-failed and logged, **and**  
2. At least **one** of 718 / 717 / 719 / 715 made a verified commit, **or** 2 hours with no gate-green progress on one issue (escalate, switch lane).

Do **not** thrash 719 layout and 715 and 717 in one uncommitted soup.

---

## 9. File checklist (open these, not the whole repo)

| Priority | Path |
|---|---|
| 1 | `OVERNIGHT_ORDERS_GROK03_2026-07-14.md` (this file) |
| 2 | `docs/UI/Grok-03-here-to-there-WO-program.md` |
| 3 | Active WO under `WorkOrders/WORK_ORDER_71x_*.md` |
| 4 | `docs/UI/Grok-02-Obsidian-UI-guidance.md` or `docs/vfx/Grok-01-VFX-guidance.md` |
| 5 | `PREFLIGHT_GATE.md` Gate A before first edit; Gate C before done |
| 6 | `.claude/skills/run-defenders/SKILL.md` for build/fleet commands |

---

## 10. Owner one-liner for CLI session start

> Execute `OVERNIGHT_ORDERS_GROK03_2026-07-14.md`. Priority: **716 complete**, then **718**, then **717** or **719** per Path A/B. **720 parked** until I mark pair-walk. Push held. Report with the morning template.

---

*End of overnight orders. Notion sync is owner’s later job — do not block on it.*
