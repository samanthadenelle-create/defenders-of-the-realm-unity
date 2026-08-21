<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-07-24
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-07-24) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 764 — Hub landmarks obey the Y-height normalization (WO-751 gap)

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Lane:** World/Visual (hub skinning). Scope: **SMALL-MODERATE** (one injector routed to the existing fit-to-height path + per-landmark height values + a visual verify).
**Owner intent (F8, verbatim):** *"We discussed all items being normalized by Y height of a preset ceiling correct?"* — i.e. the WO-751 normalization must apply to the Main_Castle_Overworld landmark buildings too, not just player-built structures.

---

## 0. RCA (code-verified 2026-07-24, read-only agent — NO edits yet)

WO-751/DEF-208 normalization = fit each item so its **bounds.Y == a preset ceiling**: `VisualFactory.Fit(go, target, largest:false)` (`VisualFactory.cs:260-266`), target from `StructureFactory.EffectiveVisualHeight` (`:60-65`) = `repo.visualHeight` if >0 else `DefaultVisualHeight = 4f` (`StructureFactory.cs:50`); tower/siege overrides 7 m/3 m in `structures-catalog.json`.

**Split in Main_Castle_Overworld (the bug):**
- **Player-built catalog structures → NORMALIZED.** They go through `StructureFactory.Create` (`:109-111`, sets `FitHeight`). ✔
- **Baked hub landmarks → NOT normalized.** Forge, farm/lumbermill, windmill, arcane tower, jeweler, store, barracks, colosseum are baked by `CastleHubBuilder` and re-skinned at runtime by **`HubStructureVisualInjector`**, which sizes them with **`SkinOptions.Structure(sizeM)`** → sets **`FitLargest`** (fit-to-LARGEST-dimension, the legacy path), NOT `FitHeight`. Hand-dialed `sizeM` = 7/12/7/8/16/… (`HubStructureVisualInjector.cs:66-76, 128-131`). And `FitLargest` WINS over `FitHeight` (`VisualFactory.cs:161`), so the WO-751 ceiling is never reached for these.

**Gap seam (exact):**
- `HubStructureVisualInjector.cs:287` — `TrySwap`: `SkinOptions.Structure(s.sizeM)`
- `HubStructureVisualInjector.cs:221` — `TryPlace`: `SkinOptions.Structure(p.sizeM)`

## 1. The model — one global base × a per-item multiplier (owner-locked 2026-07-24)

Owner directive (verbatim): *"the heights should be YHeight_Variable or whatever so that all buildings are default 1 height where its 1 * variable. the only things would be things like towers to be 1.25 * variable … since we build all with a script, it makes sense."*

So normalization becomes **centralized + deterministic**, replacing per-item absolute meters:

```
YHeight_Variable   = <base ceiling, one constant>   // = today's DefaultVisualHeight (4 m) as the starting value
heightMultiplier   = per-item, DEFAULT 1.0          // ALL buildings = 1.0
EffectiveHeight    = YHeight_Variable * heightMultiplier
```

- **All buildings → 1.0 × base** (uniform height — the whole point: consistent, script-built).
- **Towers → 1.25 × base** (the one deliberate exception the owner named).
- Change `YHeight_Variable` in ONE place → the entire town re-scales together.
- This SUPERSEDES the old per-item absolute `visualHeight` (7 m/3 m) overrides — they become multipliers (tower 1.25; siege = owner-confirm, likely <1.0). The hand-dialed hub `sizeM` values are DELETED.

### Implementation
1. Introduce `YHeight_Variable` as the single base constant (rename/repoint `StructureFactory.DefaultVisualHeight = 4f`, or a new tunable). Value = 4 m to start (owner-tunable).
2. Add a per-item `heightMultiplier` (default **1.0**) — reuse/repurpose `RepoProps.visualHeight` semantics as a MULTIPLIER, or add a clean `heightMul` field. Catalog authors only override it for exceptions (towers **1.25**).
3. `EffectiveVisualHeight` returns `YHeight_Variable * heightMultiplier` (`StructureFactory.cs:60-65`).
4. **Route the hub injector through this** — `HubStructureVisualInjector.cs:221` + `:287`: replace `SkinOptions.Structure(sizeM)` with the height-fit path (`FitHeight = EffectiveVisualHeight(...)`, **`FitLargest = 0`**). Delete the hand-dialed `sizeM` table (`:66-76, 128-131`). Now hub landmarks obey the same base×mult as everything else.
5. `FitLargest` must be CLEARED everywhere structures skin (else it wins over `FitHeight`, `VisualFactory.cs:161`).

## 2. Values (owner-locked model; base tunable)
- Base `YHeight_Variable` = **4 m** to start (one number, owner-tunable).
- Buildings (forge, jeweler, store, farm, lumbermill, windmill, barracks, colosseum, all hub landmarks) = **1.0×** → uniform. *(Colosseum included — owner said "all buildings default 1 height"; it is NOT special-cased tall unless owner later tags it a multiplier.)*
- Towers = **1.25×** = 5 m at base 4.
- Siege / any other exception = owner-tag a multiplier (default 1.0 if untagged).
- Per memory `headless-screenshot-verify-ui-before-build`: after wiring, run `UICaptureLaunch.RunCaptureHeadless` on Main_Castle_Overworld and OPEN the PNGs to confirm the town reads right before building. Owner eyes the base value.

## 3. Acceptance criteria
- [ ] Hub landmarks in Main_Castle_Overworld are sized by fit-to-HEIGHT (`FitHeight`/`EffectiveVisualHeight`), not `FitLargest(sizeM)`; `FitLargest` cleared on them.
- [ ] Player-built structures unchanged (already normalized).
- [ ] Each landmark reads at a sane preset height (stalls consistent; colosseum still a landmark) — verified by headless screenshot, not assumption.
- [ ] No regression to hub footprint/collision (the size drives visuals only; confirm nav/footprint unaffected).
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK`; screenshot-verify PNG opened + owner-approved before the build ships.

## 4. Notes
- Root cause = an un-migrated skinning path from before WO-751, not a broken normalization system. The system works; the hub injector never adopted it.
- Data source: read-only RCA 2026-07-24 (all file:line cited), per §12.
- `HarvestSite.cs:293` / `MineNodeVisual.cs:161` also still use `FitLargest` — decor nodes, out of scope here but flag for a follow if they read inconsistent too.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
