# Talent Tree — Custom Node-Graph (Path B) Build Spec — 2026-06-28

> Implementation spec for CLI. Grounded in the verified seam (see
> `BLINK_OBSIDIAN_UI_UNDERSTANDING_2026-06-28.md`). Owner locked the 4 forks (below).
> Presentation + a plan/confirm layer only — the 68-node data model + effect handlers are UNCHANGED.

## Locked decisions (owner, 2026-06-28)
1. **Single-purchase, graph look** — keep the authored 68-node design (buy a node once). No
   multi-rank, no data/effects/currency rework. Render it as a node-graph.
2. **Authored positions + edges in JSON** — the graph layout is data-driven.
3. **Plan → CONFIRM** — tap stages a pending build with a live cost preview; commit on CONFIRM; cancel discards.
4. **No pagination yet** — one scrollable canvas (Knight + Shared) for V1; pages added with future heroes.

## North star
Replace the flat tiered grid in `HeroSkillTreePanelMvvm` with the Obsidian dark node-graph: a framed
`Talent_Tree_Panel` window, `Talent_Border` node plates with real icons, connector lines drawn along
prerequisite edges, node-state styling (locked/unlockable/staged/owned/capstone), a staged-plan flow
with a CONFIRM/Cancel + cost preview, on a scrollable canvas. VM stays dumb; data drives the layout.

---

## 1. Data — `hero-talents.json` (additive, non-breaking)

Add OPTIONAL per-node layout fields; absence = fall back to tier/slot auto-position (so the file stays
valid mid-migration and other heroes work before they're authored):

```
node: { ...existing (id,name,tier,slot,cost,kind,iconPath,effect,prerequisites)...,
        "x": <float 0..1>,   // canvas-relative X (0=left,1=right)
        "y": <float 0..1>,   // canvas-relative Y (0=top,1=bottom)
        "edges": ["<nodeId>", ...]   // OPTIONAL extra connectors beyond prerequisites
}
```
- **Connectors = `prerequisites` by default** (child→parent edge). `edges` is only for cosmetic links
  that aren't strict prereqs (likely unused for Knight V1).
- Author `x/y` for the 20 Knight nodes (3 branches × tiers, organic spread like the reference) + the 8
  Shared nodes (a band below/beside). Dual-copy Resources + StreamingAssets; re-validate via
  DataRegression + the existing `CheckCraftingChain`-style guard (add a talent layout guard: every
  edge/prereq id resolves; every node has x/y in 0..1 or none).
- **No change** to cost/effect/prereq semantics. `WisdomCurrencyService`/`HeroTalentCatalog.CanUnlock`
  untouched for validation.

## 2. VM — `HeroSkillTreeVM` (add a PENDING/plan layer; keep it pure)

Today: `Unlock(id)` spends immediately via `WisdomCurrencyService.Unlock`. Add a staged layer:
- New state: `_pending` (HashSet<string>) — nodes staged this session, not yet committed.
- New reads (for the View): `IsPending(id)`, `PendingCost` (sum of staged node costs), `CanCommit`
  (pending set is non-empty, prereqs satisfied counting owned+pending, capstone-exclusive across
  owned+pending, total cost ≤ Wisdom).
- New commands: `Stage(id)` / `Unstage(id)` (toggle into `_pending` with the SAME validation as
  CanUnlock but treating pending as tentatively-owned), `Commit()` (unlock every pending node in
  dependency order via `WisdomCurrencyService.Unlock`, then clear pending), `CancelPlan()` (clear pending).
- `SkillNodeVM` gains `IsPending` + an `X`/`Y` (carry the layout) + the resolved icon already present
  via `IconPath`. Node display state becomes: Owned > Pending > Unlockable > Locked (+ capstone flag).
- Validation reuses `HeroTalentCatalog.CanUnlock` against an "effective owned" = owned ∪ pending so a
  tier-2 can be staged in the same plan as its tier-1. Capstone exclusivity checked across owned ∪ pending.
- Still NO Unity types in the VM (dumb-View rule preserved).

## 3. Service — `WisdomCurrencyService` (minimal/none)
- Prefer NO change: the VM commits by calling existing `Unlock(id)` per node in dependency order.
- Only if needed: add `bool CanUnlockBatch(IEnumerable<string>)` convenience — optional.

## 4. View — `HeroSkillTreePanelMvvm` (the real work; code-built uGUI, no UXML)
- **Window**: `ElarionUiKit.PanelFramed(..., packSpriteName: "panel_talent")` (new mirrored frame; falls
  back to `PanelWindowDark` if absent — null-safe).
- **Scroll canvas**: host nodes in a `ScrollRect` content rect sized to the node bounds (single page).
- **Connector lines**: for each node, for each prereq/edge, draw a line from parent.(x,y) to child.(x,y).
  V1 = a thin rotated 9-slice `Image` (procedural or a mirrored line sprite) tinted by state
  (owned/pending = warm gold glow; locked = dim). Curved edges are a later polish; straight glowing
  segments match the reference closely enough for V1. Draw connectors BEHIND nodes (sibling order).
- **Node plate**: `slot_talent` (mirrored `Talent_Border`) via `ElarionUiKit` slot/Apply, state-tinted:
  Owned (green/full), Unlockable (gold rim), Pending (gold pulse/distinct tint), Locked (desaturated,
  show `LockReason`), Capstone (distinct border, e.g. `Talent_Border_6`). Falls back to today's colour
  plate when art absent.
- **Node icon**: `ConceptIconResolver.Resolve(node.Id or ability id)` centred in the plate (Knight ids
  already mapped in concept-icons.json); letter-glyph fallback on null.
- **Node tap**: `_vm.Stage/Unstage(id)` (NOT immediate unlock). Visual: staged nodes get the pending look.
- **Footer**: `CONFIRM` button (`ButtonConfirm`/green) enabled when `vm.CanCommit`, shows
  `Spend X Wisdom`; `Cancel` (clears plan); keep `Close`. CONFIRM → `vm.Commit()`.
- **Wallet header**: `Wisdom <remaining>  (−<pendingCost> pending)`; re-render on `Changed`.
- **Two-state contract**: must look right with `ff.blinkchrome` ON and OFF (existing rule).

## 5. Art to mirror (extend `BlinkUiImporter.BuildTable()`, then run `Defenders > Art > Import Blink UI Pack`)
| Obsidian source | canonical id | role | border |
|---|---|---|---|
| `Slots_Obsidian/Talent_Border_1..6.png` | `slot_talent_1..6` (+ `slot_talent`=1) | slot | 24 |
| `Panels_Obsidian/Talent_Tree_Panel.png` | `panel_talent` | panel | 48 |
| `Decoration_Obsidian/TalentTree_Decoration_1/2.png` | `deco_talent_1/2` | panel/decoration | 0 |
| (connector) check kit for a line sprite; else procedural | `line_talent` (opt) | — | — |
- Read each sprite's baked 9-slice border from its meta if it differs (vendor publishes no table).
- All null-safe: until mirrored, the View keeps colour-plate/glyph fallback — never blanks.

## 6. Phasing (each phase compiles + gates green before the next; commit local, owner felt-verifies)
1. **Art mirror** — add the talent entries to `BlinkUiImporter`, run import, commit the new
   `Resources/RpgUi/**` PNGs + meta. (No behavior change.)
2. **Data** — add `x/y` (+ optional `edges`) to the 28 Knight+Shared nodes in both JSON copies; add a
   DataRegression talent-layout guard. (No behavior change; auto-layout still works without x/y.)
3. **VM plan layer** — add pending/Stage/Unstage/Commit/CancelPlan + CanCommit + PendingCost; unit-safe.
4. **View graph** — connectors + `slot_talent` plates + icons + scroll + plan/CONFIRM/Cancel; both
   chrome states. This is the bulk.
5. **Polish** — capstone frame, pending pulse, decoration flourishes, curved connectors (optional).

## 7. Verify
- Headless: CompileGate + DataRegression (layout guard) green.
- Felt-test on a **clean `dotr-talents-v1`** (wipe the stale all-owned save) so locked/stage/confirm/
  spend + single-capstone are actually observable.
- Both `ff.blinkchrome` states render correctly.

## 8. Non-goals (V1)
- No multi-rank nodes (single-purchase). No pagination. No Ranger/Mage authoring (data stored, inert).
- Don't touch `TalentTreePanel.cs` (deprecated UIToolkit). Don't change effect handlers or costs.
- No UXML. No inspector drag-drop. VM stays pure.

## 9. Files in scope
- `Assets/Resources/Data/Canonical/hero-talents.json` (+ StreamingAssets copy) — layout fields.
- `Assets/_Modules/Village/Talents/HeroSkillTreeVM.cs` — pending/plan layer.
- `Assets/_Modules/Village/Talents/HeroSkillTreePanelMvvm.cs` — the graph view.
- `Assets/Editor/BlinkUiImporter.cs` — talent art entries.
- `Assets/Editor/Regression/DataRegression.cs` — talent-layout guard.
- (maybe) `Assets/_Modules/Village/Talents/WisdomCurrencyService.cs` — optional batch helper only.
- New mirrored sprites under `Assets/Resources/RpgUi/**`.
