# WORK ORDER 824 — CoC + Warcraft **player enjoyment** program (master sequence + PO fun bar)

**Status: READY — PROGRAM / DISPATCH AUTHORITY (not a single code dump)**  
**Minted:** 2026-08-01 (CLI / Grok — owner asked after CoC/WC3 enjoyment review)  
**Silo:** Program (PM + Claude design lanes + CLI implement in linked WOs only)  
**Program hub:** also update `docs/WC3_COC_EXPERIENCE_ANALYSIS.md` §3 dispatch when this lands  

---

## 0. What this WO is (and is not)

| This WO **is** | This WO **is not** |
|----------------|--------------------|
| The **player-enjoyment** north star for CoC + WC3 feel | A re-implementation of 817/774/etc. in one file |
| Binding **ship order** so Claude/CLI pull the right child WO | Replacing the queue engine or raid spine |
| The **PO felt bar** (“done enough to enjoy”) | Hardening / bugfix (that is **823**) |
| Gap fills that had **no** child WO | Async PvP “someone hit my base” (V2, out) |

### Already minted — do **not** re-spec here; **pull the child file**

| Child | Owns enjoyment beat |
|-------|---------------------|
| **817** | Production glance: icon + bar + channel strips (CoC workers + WC3 queue grammar). Folds 798/801/816. |
| **822** | Barracks teach: marker + coach + Train-3 + first-raid tip (not toast). |
| **774** | Raid loadout + deploy ring + naming (CoC Attack fantasy). |
| **809** | War readiness / power score on Raids screen. |
| **800** | One building focus card (Level \| Skills \| active job). |
| **805** | Construction / upgrade world feedback parity (scaffold trust). |
| **799** | Cancel + refund **engine**; cancel **UI** after 817 rows exist. |
| **821** | Building perks timed on Research channel (Lab feel). |
| **806** | Barracks progression spine UX (one army mindspace). |
| **807** | Troop upgrade power readability (+HP/+DPS/ability scream). |
| **802–804** | Raid stakes, session comfort (**803**), structure stars language. |
| **818** | KayKit unique NPC per structure (identity spice). |
| **811 / 815** | Echo gather/repair + harvest affinity (city autonomy). |
| **808** | Hero gear levels — **code shipped**; PO felt only. |
| **812 / 819 / 820** | Barracks placeable, singleton, full-army gate — **code shipped**; PO felt. |

### Sibling hardening (not enjoyment)

| WO | Role |
|----|------|
| **823** | `ArmyReadiness`, founding-card soft deadline, over-queue tests, RESULT hygiene. **Do before 822** if 822 reuses readiness. Does **not** make the game feel like CoC. |

---

## 1. Player north star (binding)

> One hero you control · city work autonomous · raid = **train → army → teleport → deploy → watch**.  
> Steal **grammar** from CoC + WC3 (icons, bars, queues, attack door, power cue) — keep **Elarion** art.  
> Never “Obsidian” in player copy. Never a second global FIFO queue.

### Three feelings that must land (P0)

1. **“Something is cooking”** — glance without a text novel → **817**  
2. **“My army is ready / I know how to fill it”** — teach + train + gate → **822** + shipped **820**  
3. **“I’m invading and it matters”** — loadout + deploy ring + readiness → **774** + **809**

---

## 2. PO “done enough to enjoy” bar (close program when all YES)

A new-ish player, without external help, can:

- [ ] **See** build/train/research progress as **icon + bar** (not only text) — **817** Phase 1–2 min  
- [ ] **Find** the drillmaster / Barracks after onboarding (marker + coach) — **822**  
- [ ] **Train** a first army with a soft goal (Train N) — **822**  
- [ ] **Tap Raids** and understand full-army / redirect to train — **820** shipped + **822** tip  
- [ ] **Deploy** troops with readable loadout + deploy language — **774**  
- [ ] **Feel stronger** after a troop L or gear improve (numbers or clear delta) — **807** + **808** felt  
- [ ] **Trust** a building under construction / upgrade in the world — **805** (or 817 scaffold phase)

Optional for “deep CoC joy” (P1–P2, not block first pride loop):

- [ ] Cancel a queued job with refund chrome — **799** UI after 817  
- [ ] Perk research times out on Research channel — **821**  
- [ ] Single readiness/power number on Raids — **809**  
- [ ] One unified building card — **800**  
- [ ] Stars/loot/casualty drama — **802–804**

---

## 3. Binding dispatch order (player joy path)

Implement **in this order** unless owner re-prioritizes. Parallel only when silos are file-disjoint and do not fight the same HUD surface.

```
WAVE 0 — trust (short)
  823 Phase A (ArmyReadiness)  → then C/B/D as capacity allows
  PO felt: 819 singleton, 820 army gate, 808 gear, 810 board (already on origin)

WAVE 1 — city “cooking” (highest joy lever)
  817 Phase 0 design sign-off (if still needed) → Phase 1–2 icon+bar glance
  → Phase 3 multi-channel (was 801) → later scaffold / cancel chrome

WAVE 2 — army discovery
  822 barracks teach v2 (depends on 823 A if it consumes ArmyReadiness)

WAVE 3 — attack fantasy
  774 loadout + deploy ring + naming
  809 war readiness score (can lean partial after 774; gear optional)

WAVE 4 — city management depth
  800 building card unify (design → CLI)
  805 construction feedback
  821 timed perk research
  799 cancel UI (engine may already land; chrome after 817 rows)

WAVE 5 — army ladder readability
  806 barracks spine UX (design → CLI)
  807 troop power readability

WAVE 6 — raid stakes + spice
  803 session comfort (after 774)
  802 casualties/loot stakes
  804 stars language (owner go)
  818 KayKit NPCs finish
  811/815 Echo autonomy
```

**Not parallel:** 817 phases fighting each other on the same chip; 803 vs 774 same deploy path; 806 heavy rewrite vs 807 on same Barracks panel without coordination.

---

## 4. Gaps this program adds (no prior dedicated WO)

These were named in the enjoyment review but **not** owned by 823 or a single child. Implement as **sub-tasks under this WO** or tiny follow-ons — do not invent parallel mega-specs.

### 4.1 Soft first-raid rule (PO ruling required)

**Problem:** Full army cap (10) may block the first proud raid after Train-3.  
**Default until owner rules:** keep **820** hard full-army (queued counts).  
**If owner rules SOFT:**

- First raid only: Ready if `DeployableSlots + QueuedSlots >= FirstRaidMin` (e.g. **3**) OR full cap — pick one rule, document in RESULT.  
- After first successful raid return: full **820** rule forever.  
- Must go through **`ArmyReadiness`** (823); never fork `RaidSelectionScreen` math again.  
- Seen key e.g. `first_raid_completed` / existing raid stats.

**Acceptance (only if SOFT ruled):** Train-3 path can open Raids without waiting for 10; second+ raids require full.

### 4.2 “Work” empty / busy teaching (copy only)

**Problem:** Players ask “what is Work?”  
**Do:** One-time coach or empty-state line on Work panel: *“Builders, training, and research in progress.”* ASCII, Elarion not Avalon.  
**Do not:** Rename to Obsidian; do not second panel.  
**Hook:** First open of Work / ObsidianQueueGate when idle + once `work_panel_intro` SeenTutorials via `MarkTutorialSeen`.  
**Can ship inside 817 Phase 1** if cheaper — if so, check off here and note in 817 RESULT.

### 4.3 Program hub truth pass (docs only)

Update `docs/WC3_COC_EXPERIENCE_ANALYSIS.md`:

- §1 feel-gap table points at **817** (already)  
- §2 status: 808/812/819/820 **IMPLEMENTED** (awaiting PO) where true  
- §3 dispatch replaced by **this WO’s Wave 0–6**  
- Link **822, 823, 824** in the index table  

### 4.4 Out of program (do not add)

- Async defense of player base (V2)  
- Full WC3 micro of raid troops  
- Cloning CoC/WC3 UI skins  
- Second queue engine  
- Toast-only teach (forbidden; 822 only)

---

## 5. Roles

| Role | Does |
|------|------|
| **Owner (PO)** | Rules 4.1 soft first-raid Y/N; signs 817/800/806 image pairs when required; **closes** fun bar checkboxes in §2 after felt play |
| **Claude** | Design packs only where child WO says UI seat; **never** `.cs`; may draft copy for 4.2 |
| **CLI** | Implements **one child WO at a time** per Wave; gates; sole committer; does not merge Wave 3 into Wave 1 |

### Paste boot (Claude)

```text
Read WORK_ORDER_824_coc_wc3_player_enjoyment_program.md (dispatch only).
Then open ONE child WO from the current Wave (e.g. 817 or 822).
Obey that child file. Do not implement the whole program in one PR.
Hardening = 823. Teach = 822. Queue look = 817. Deploy = 774.
```

### Paste boot (CLI implementer)

```text
Current Wave from WO-824. Implement the assigned child WO file only.
After ship: update child status + short RESULT; tick the matching §2 bar item only if PO felt.
```

---

## 6. Acceptance for **this** program WO (meta)

- [ ] CLI_LANES banner lists 824 and next free  
- [ ] Hub doc §3 points at Wave 0–6 (task 4.3)  
- [ ] Owner has ruled 4.1 soft first-raid YES/NO (record under §4.1)  
- [ ] Child WOs not duplicated; Claude pulled children not a mega-diff  
- [ ] PO fun bar (§2) is the close criteria for “CoC/WC3 enjoyment pass” — not “all WOs through 815”

---

## 7. One-line truth

**823** = make the gates trustworthy.  
**824** = make the game *feel* like CoC production + WC3 queues + CoC attack — by sequencing **817 → 822 → 774 → 809 → …** and closing the PO fun bar.
