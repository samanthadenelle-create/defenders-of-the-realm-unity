# GROK MEMORY — load next session (Defenders / EoA)

**Purpose:** cold-start pointer so the next Grok session does not re-discover the arc.  
**Update in place** when program state changes.  
**Last session:** 2026-07-14 (detached worktree `~/.grok/worktrees/eoa/defenders`, same SHA as the main repo tip).

---

## Where to work

| Tree | Path | Notes |
|---|---|---|
| **Primary (prefer)** | the main repo checkout — **root is MACHINE-DEPENDENT** (`C:\eoa` on one box, `D:\eoa` on another; never hardcode it) | Branch `wip/village2-and-f8-tickets` — **Grok-03 pack copied here 2026-07-14 (uncommitted until sole committer stages)** |
| Grok worktree | `~/.grok/worktrees/eoa/defenders` | Detached HEAD; safe to `git worktree remove` **after** verifying the files landed in the main checkout |

**Next free WO: ⛔ DO NOT COPY A NUMBER FROM THIS FILE.** Open the **`CLI_LANES_WO_NUMBERS.md` banner**
— it is the SOLE authority and it moves several times a day. *(This line used to read "754", which by
2026-08-02 was ~99 numbers short — a fresh Grok session minting from it would collide immediately.)*
As of **2026-08-02** two **disjoint** blocks are in use, and each seat bumps ITS OWN banner row in the
SAME edit as the mint:

| Block | Owner | Read the banner for the live number |
|---|---|---|
| **main line** | CLI | 782–852 consumed as of 08-02 evening |
| **860–899 reserved** | UI seat (Grok/UI mints here) | 860/861/862 consumed as of 08-02 evening |

Five two-seat collisions struck on 2026-08-02 alone, all caused by minting without bumping the banner.
Collisions resolve **first-on-disk-and-referenced-wins**.

---

## Grok pack (this arc) — read order

| # | File | Role |
|---|---|---|
| 0 | `OVERNIGHT_ORDERS_GROK03_2026-07-14.md` | **CLI overnight authority** — must/stretch/park + morning template |
| 1 | `docs/UI/Grok-03-here-to-there-WO-program.md` | Program index + dependency graph |
| 2 | `docs/UI/Grok-02-Obsidian-UI-guidance.md` | Blink Obsidian tight lens |
| 3 | `docs/vfx/Grok-01-VFX-guidance.md` | Hovl towers / melee / spells |
| 4 | `docs/UI_BLINK_TEMPLATE_CANON.md` | Binding UI formula |
| 5 | `docs/HOVL_STUDIO_SME.md` | Hovl pack SME |

**WOs minted (READY):**  
**715** Hovl combat VFX · **716** pair-walk capture · **717** unstyled kill · **718** kit-law oracle · **719** Build HUD CoC · **720** founding FIX (needs owner PASS/FIX) · **721** HUD vitals · **722** expansion tail  

Specs: `WorkOrders/WORK_ORDER_71[5-9]_*.md`, `WORK_ORDER_72[0-2]_*.md`.

---

## Honest distance (don’t sugarcoat)

- Architecture (factory, RpgUi mirror, PlayKey catalog): **strong**  
- Felt Obsidian: **~half** (modals better than HUD/build)  
- Build chrome: **engine good, UI not CoC-simple**  
- Hovl: **pipes good**; towers miss travel; registry-empty `vfxKey` silences hero motion VFX  
- Pair-walk **unsigned** — night report: Windows exe was wiped  

---

## CLI overnight priority (if still open)

```
716 complete → 718 → 717 or 719 → stretch 715 B or C
720 PARKED until owner marks PAIRWALK_716
721/722 not required overnight
Push HELD unless owner said otherwise
```

---

## Hard rules that bit this session

1. **Registry-only motion VFX** — abilities.json cast keys suppressed; fill `motion-castings.json` `vfxKey`.  
2. **Never runtime-load `Assets/Blink/**`** — only `Resources/RpgUi`.  
3. **No UXML** in player builds.  
4. **Sole committer, explicit paths** — never `git add -A`.  
5. **Detached worktree:** uncommitted Grok files die if worktree removed without copy/commit to the main repo checkout.

---

## Owner preferences (this arc)

- Notion sync later — **git WOs authority** for overnight.  
- Clear expectations > vague backlog graze.  
- Keep it real on distance assessments.  
- Gold = accents only; fill contract on bars; colorblind-safe (text/shape, not hue alone).  

---

## Next Grok session opener (suggested)

> Load `docs/GROK_MEMORY.md`, then Grok-03 + overnight orders if still active. Continue program from morning report; don’t re-mint 715–722.

---

*Grok memory — not a substitute for CANON_GROUND_TRUTH or START_HERE; fast path for this program only.*
