# CANON STALENESS AUDIT — 2026-06-28 (READ-ONLY)

> Scope: the load-bearing canon set vs. current reality. Sourced from `git log` (HEAD
> `ace37cc4`), working tree, `SaveSchema.cs`, the combat-pivot canon, and WO-584
> (dungeon/outpost/arena consolidation). **No files were edited** — this is the finding
> list per CLAUDE.md §15 (banner-don't-rewrite for dated ledgers; same-breath fix for the
> live set). Fixes routed to CLI as the single committer.

## The headline drift
The live anchor `CANON_GROUND_TRUTH_2026-06-26.md` is itself **~30 commits stale**: it
pins HEAD `8aa24c32`, but HEAD is now `ace37cc4`. The whole WO-560→584 arc (arena
telegraph/VFX, Knight talent interpreter, equip→visual MPB tint, intro video, title
rebrand, audio wiring, UI Blink master-frame template, wave-loop-in-hub, dungeon/outpost/
arena consolidation spec) landed AFTER the anchor. **A fresh `CANON_GROUND_TRUTH_2026-06-28.md`
should be minted and the 06-26 one bannered SUPERSEDED.**

---

## Stale-doc findings (claim → correction)

### 1. CANON_GROUND_TRUTH_2026-06-26.md
- **L14/L15 `HEAD: 8aa24c32`** → WRONG. HEAD is `ace37cc4`. Mint a 06-28 anchor; supersede this one.
- **L51 "Echo workforce ... save v25"** → WRONG. `SaveSchema.CurrentVersion = 27` (v26 = ring/amulet
  WO-543, v27 = wall-mounted seating). Correct to **save v27**.
- **L64–68 "Queued / captured"** has no entry for **WO-584 dungeon/outpost/arena consolidation** (the
  one-combat-space primitive: RegionGate warp → resolver → Arena skin → ownership flip; ATB dungeon
  flagged off `ff.atbdungeon`). Add it as the current combat-space direction.
- Missing the **title canon** (WO-570: "Echoes of Elarion" title under "Defenders of the Realm" series,
  tagline "Hold the line") and the **UI Blink master-frame template canon** (BINDING, `docs/UI_BLINK_TEMPLATE_CANON.md`).

### 2. SESSION_CANON_LOADER.md
- **L46 "save v25"** → WRONG, now **v27** (same as above).
- **L32–47 "Current State (2026-06-26)"** → re-date to 06-28; add the WO-584 combat-space consolidation
  and the title canon. Combat bullet still frames the model as "overworld BattleArena + separate flat ATB"
  — true today, but WO-584 is now folding the **dungeon** off ATB onto the Arena; note the in-flight pivot.
- **L55 Key Files** lists `docs/BLINK_UI.md` but NOT the newer BINDING `docs/UI_BLINK_TEMPLATE_CANON.md`.
  Add it.

### 3. PIPELINE_STATE.md
- **"CURRENT STATE — 2026-06-26" block: "save v25"** → **v27**.
- Same block: **"WO numbering: next free = 430"** → STALE. WO spec files now run through **584**
  (WO-584 = dungeon/outpost/arena consolidation, READY TO IMPLEMENT). Re-state next-free from the
  numbering authority, not 430.
- Block has no mention of the **dungeon→Arena consolidation / `ff.atbdungeon`** or the **title rebrand** —
  add to the 06-26 (→ 06-28) block.
- **L64 (historical 06-09 block): "highest WO is now 383; next free WO = 384"** — frozen historical text,
  leave as-is (already under a SUPERSEDED block); flagged only so it isn't mistaken for current.

### 4. CLAUDE.md
- **§2 "Next free WO = 430 (through 429 used ...)"** → STALE. WOs exist through **584**. Correct the
  next-free pointer (defer to `MASTER_PIPELINES_BACKLOG` / `CLI_LANES_WO_NUMBERS`, but the literal "430"
  is wrong by ~150 WOs).

### 5. docs/HANDOVER.md
- **L2 read-order preamble: "★ the SESSION HANDOVER block immediately below (2026-06-19)"** → the block
  below is dated **2026-06-26**, not 06-19. Fix the date reference.
- **"SESSION HANDOVER — 2026-06-26" block: "save v25"** → **v27**; HEAD `8aa24c32` → `ace37cc4`. Add the
  WO-560→584 arc (UI Blink frames, title rebrand, dungeon/Arena consolidation spec). Re-date to 06-28.
- **"OPEN CREATIVE FORK: Cathedral Spire vs living world-Tree"** still listed unresolved — confirm/close
  against the owner ruling captured in memory `canon-maintenance-wo520` (living world-Tree won). If
  resolved, retire the fork line.

### 6. docs/MASTER_CATALOG.md
- Top already carries a STALE banner (good). But the **INDEX TABLE rows still read "Player hero (Blaise +
  class bodies)"** and "party-of-4" framing in the per-area section files (`docs/MASTER_CATALOG/village-hero.md`),
  which lack their own banner. Add a one-line STALE flag to the section file head (hero = single Tripo
  "Grom", no party, no Blink rig) so the section isn't read in isolation without the correction.

### 7. docs/ARCHITECTURE.md (last touched 06-13 — predates the entire single-Knight pivot)
- **L95 combat flow: "Breach (Village2/dungeon) ► GoBattle(BattleParams) ► ATBBattle ► returns to
  ReturnScene"** → STALE. This is the flat-ATB-dungeon path that **WO-584 replaces** (dungeon now routes
  to the real-time `BattleArena`; ATB behind `ff.atbdungeon` OFF). Doc never mentions `BattleArena`,
  single-Knight, or overworld real-time combat. Add a `STALE:` banner on the combat-flow section pointing
  to `docs/COMBAT_PIVOT_NORTHSTAR.md` + WO-584.

### 8. docs/COMBAT_PIVOT_NORTHSTAR.md (06-22 — foundational, mostly green)
- Still names **ATB** as the V1 combat (L193) without noting the 06-28 consolidation that moves the
  dungeon off ATB onto the Arena (WO-584). Add a forward-pointer to WO-584 so the northstar and the
  newest WO don't read as conflicting.

---

## Cross-cutting fact corrections (apply wherever they appear)
- **Save version: every load-bearing doc says `v25` → reality is `v27`** (SaveSchema.cs:30).
- **HEAD `8aa24c32` → `ace37cc4`** in every doc that pins it.
- **"Next free WO = 430" → WOs run through 584** (CLAUDE.md §2, PIPELINE_STATE/anchor 06-26 block).
- **Echo workforce design drift (note, not strictly doc-stale):** docs + code = "1–4 echoes (≤4)";
  memory `echo-workforce-drag-drop` = cap 5 (3 organic + 2 flex). Code is authoritative today (≤4); the
  cap-5 design is not yet built. Flag so the next echo WO reconciles the two, doesn't assume 5 shipped.
