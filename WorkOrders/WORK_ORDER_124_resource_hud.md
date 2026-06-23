# WORK ORDER 124 — Resource HUD: Show the Four Harvest Resources Tick Up

**Status:** READY TO IMPLEMENT
**Date:** 2026-05-30 (Fri)
**Priority:** High — the WO-117 worker auto-collect demo needs the player to **SEE** Wood / Food / Crystal / Ore bank in real time. Today only crystals render; the other three harvest payouts are invisible.
**Scope:** Small + self-contained. Additive interface method on Core + one code-built panel on the existing `VillageHudController`. No new assembly, no scene file, no UXML.
**Lanes:** design (owner + UI) · HUD/Core code (CLI).
**Depends on:** none hard. **Soft-ties (do not block on):** WO-117 (provides `ResourceType` enum in `DeNelle.Core` + banks the four payouts), WO-115 (offline welcome-back can reuse this readout for the "while you were away" line).

---

## Why (the gap)

WO-117's worker layer banks four harvest payouts:

| Node | Banks to (existing `GameState`) |
|---|---|
| Wood | `GameState.Wood` |
| Food | `GameState.Resources.Food` |
| Crystal | `GameState.AetherCrystals` (and/or `Resources.Crystals`) |
| Ore / Stone-Mine | `GameState.Stone` |

But the village HUD only displays **crystals** (`SetCrystals(int)`). When a worker fills a Wood node and banks it, the wallet changes with **zero on-screen feedback** — the harvest verb feels dead. The Warcraft "watch the number climb" payoff is missing for 3 of the 4 resources.

**This WO makes all four visible**, plus an optional small node/worker-status readout so the player can glance at "Wood node 60%" without finding the node in-world.

---

## Reconciliation — what already exists (read before writing; build-up, not rebuild)

I read `IVillageHud.cs`, `VillageHudController.cs`, WO-117, CLAUDE.md §5, and PIPELINE_STATE.md §8 before writing this.

| Need | Exists? | Where / note |
|---|---|---|
| `ResourceType` enum (Core) | **provided by WO-117** | `Assets/_Modules/Core/ResourceType.cs` — `enum ResourceType { Wood, Food, Crystal, Ore }`. **Do NOT redefine it** — if WO-117 hasn't landed yet, this WO's interface method references it, so WO-117's enum file is the one shared dependency. See §"Ordering". |
| Crystal counter on the HUD | **BUILT** | `VillageHudController.SetCrystals(int)` already renders the crystal count from a bound `_crystalCount` label. **Reuse / sit beside it — do not replace it.** |
| HUD interface (Core) | **BUILT** | `Assets/_Modules/Core/HUD/IVillageHud.cs` exposes `SetWave / SetCountdown / SetHeartHp / SetCrystals / SetAttackDirections / SetWaveImminent / ShowWaveClearBanner / HideWaveClearBanner / ShowRepairPrompt`. **No Wood/Food/Ore method exists** — this WO adds one, additively. |
| Code-built absolute-positioned HUD panel precedent | **BUILT** | `VillageHudController` already builds panels in C# at runtime (`BuildStartWaveButton`, `BuildSkillsButton`, `BuildCompassRose`, `MoveManaPanelToTopLeft`) — absolute-positioned `VisualElement` trees, styled in code, parented to `_root`. **Mirror this exactly for the resource bar.** |
| Cross-module push seam | **BUILT** | Village pushes HUD data via `CoreServices.Hud?.<setter>(...)` with `?.` (CLAUDE.md §6). The HUD never references Village. |

**So the new work is: ONE additive interface method + ONE code-built resource bar on the existing controller + the existing-pattern node-status readout. No new currency, no new assembly, no scene edit, no UXML.**

---

## Architecture (respect — CLAUDE.md §5/§6)

- **Interface lives in `DeNelle.Core.HUD`** (`IVillageHud`). The `ResourceType` enum lives in `DeNelle.Core` (WO-117) so both Core's interface and Village's pusher see it.
- **Implementation lives in `DeNelle.HUD`** (`VillageHudController`). **Passive display only — never references `DeNelle.Village`.**
- **Village pushes values** via `CoreServices.Hud?.SetResource(...)` with the null-conditional `?.`. The HUD reads nothing back.
- **No UXML** — the bar is built in C# (PIPELINE_STATE.md §8: UXML does not render in builds).

---

## 1. Interface addition — `IVillageHud` (DESIGN-ONLY illustrative code)

**Recommended (the enum form — scales to all 4 without 4 setters):**

```csharp
// Assets/_Modules/Core/HUD/IVillageHud.cs  (additive — existing members unchanged)
using DeNelle.Core;   // for ResourceType (WO-117)

namespace DeNelle.Core.HUD
{
    public interface IVillageHud
    {
        // ... existing members unchanged ...
        void SetCrystals(int amount);            // KEEP — do not remove; Crystal may route here too

        /// <summary>Pushes the current wallet total for one harvest resource to the HUD bar.
        /// Called by the harvest/economy layer on change. Passive display — clamps &lt;0 to 0.</summary>
        void SetResource(ResourceType type, int amount);

        /// <summary>Optional node/worker status line, e.g. "Wood node 60%" or "idle".
        /// percent 0..1; null/empty label clears the readout. Safe to no-op if unbound.</summary>
        void SetNodeStatus(ResourceType type, float fillPercent, string workerState);
    }
}
```

**Decision for owner:** prefer the **enum `SetResource`** form over explicit `SetWood/SetFood/SetStone` — one method covers all four and any future node type (Iron exists in `GameState` if a 5th is ever added) with no interface churn. Recommend **keeping `SetCrystals(int)` too** (it already exists and is wired) — Crystal can route through *either* `SetCrystals` *or* `SetResource(ResourceType.Crystal, n)`; have the controller treat them as the same cell so there's one crystal number, not two. (Owner: confirm Crystal banks to `AetherCrystals` per WO-117 §1.)

> **Interface-vs-controller note (flag for CLI):** the concrete `VillageHudController` currently carries **richer signatures than the `IVillageHud` interface** (e.g. it has `SetHeartHp(float,float)` while the interface declares `SetHeartHp(float)`), and several Village→HUD calls go through a reflection bridge rather than the interface. Keep that pattern: add `SetResource` / `SetNodeStatus` to **both** the interface **and** the controller. If the controller is reached by reflection in the village wiring (mirroring `SetAbilityCooldown`/`SetMana`), expose the new methods as plain `public` so the same bridge path can call them.

---

## 2. VillageHudController — code-built resource bar (DESIGN-ONLY illustrative code)

A small horizontal bar of four resource cells (icon glyph + count), built once at bind time and parented to `_root`, mirroring `BuildStartWaveButton()`/`BuildSkillsButton()`. **No UXML, no `.uss` dependency** — style in code so it renders in a build.

```csharp
// Inside VillageHudController (DeNelle.HUD) — illustrative shape, not final code.

// One cell per resource: a glyph + a count label.
private struct ResourceCell { public Label Count; public Label Icon; }
private readonly System.Collections.Generic.Dictionary<DeNelle.Core.ResourceType, ResourceCell> _resourceCells = new();
private VisualElement _resourceBar;
private Label _nodeStatus;

// Built once in BindElements(), after the existing Build*Button() calls.
private void BuildResourceBar()
{
    if (_root == null) return;
    if (_resourceBar != null) { _resourceBar.RemoveFromHierarchy(); _resourceBar = null; }

    _resourceBar = new VisualElement { name = "resource-bar" };
    _resourceBar.pickingMode = PickingMode.Ignore;            // glanceable, not interactive
    var rs = _resourceBar.style;
    rs.position = Position.Absolute;
    rs.top = 14f; rs.right = 16f;                             // top-right strip (clear of heart/mana top-left, wave top-centre)
    rs.flexDirection = FlexDirection.Row;
    // ... translucent dark pill bg, rounded corners, padding (mirror BuildStartWaveButton styling) ...
    _root.Add(_resourceBar);

    // Glyphs are placeholder stand-ins until icon art lands (matches the ability-glyph convention).
    AddResourceCell(DeNelle.Core.ResourceType.Wood,    "🪵"); // Wood  -> GameState.Wood
    AddResourceCell(DeNelle.Core.ResourceType.Food,    "🍖"); // Food  -> Resources.Food
    AddResourceCell(DeNelle.Core.ResourceType.Crystal, "◆");  // Crystal -> AetherCrystals (shares the SetCrystals cell)
    AddResourceCell(DeNelle.Core.ResourceType.Ore,     "⛏");  // Ore   -> GameState.Stone

    // Optional one-line node/worker status under the bar (e.g. "Wood node 60% • collecting").
    _nodeStatus = new Label(string.Empty) { name = "resource-node-status" };
    _nodeStatus.pickingMode = PickingMode.Ignore;
    // ... small dim text, absolute under the bar ...
    _root.Add(_nodeStatus);
}

public void SetResource(DeNelle.Core.ResourceType type, int amount)
{
    // Crystal funnels to the SAME cell SetCrystals drives, so there's one crystal number.
    if (type == DeNelle.Core.ResourceType.Crystal) { SetCrystals(amount); }
    if (_resourceCells.TryGetValue(type, out var cell) && cell.Count != null)
        cell.Count.text = Mathf.Max(0, amount).ToString();
}

public void SetNodeStatus(DeNelle.Core.ResourceType type, float fillPercent, string workerState)
{
    if (_nodeStatus == null) return;
    if (string.IsNullOrEmpty(workerState)) { _nodeStatus.text = string.Empty; return; }
    int pct = Mathf.RoundToInt(Mathf.Clamp01(fillPercent) * 100f);
    _nodeStatus.text = $"{type} node {pct}% • {workerState}";   // e.g. "Wood node 60% • collecting"
}
```

**Layout guidance:** top-right strip. The HUD is already crowded top-left (heart + mana + Skills) and top-centre (wave timer + START WAVE + compass), so the resource bar sits **top-right** to avoid overlap. Cells read **icon + count** left-to-right; `pickingMode = Ignore` so it never swallows clicks (the EnsureHudReachable lesson). Optional `_nodeStatus` line is dim/small directly under the bar; empty string hides it. A brief count-up flash/tint on change is a nice-to-have (owner's call), not required for acceptance.

---

## 3. Wiring — who pushes the values (keep HUD passive)

The **harvest/economy layer pushes on change** (HUD never polls, never reads Village):

- **On bank / wallet change** — WO-117's `HarvestService.BankNode(...)` (or whichever code writes the `GameState` field) calls, after the write:
  ```csharp
  CoreServices.Hud?.SetResource(ResourceType.Wood, GameState.Wood);   // and Food / Crystal / Ore
  ```
  If an `EconomyService` / `GameStateService.ResourcesChanged` event already exists, subscribe a single pusher to it that refreshes all four cells on any wallet change (preferred — one subscription, no scattered call sites). **Reconcile with the existing crystal push** in the integrator notes (`hud.SetCrystals(...Resources.Crystals)`): replace/augment it so all four flow through the same on-change path.
- **On node fill tick (optional status line)** — while a node is `Collecting`, push `CoreServices.Hud?.SetNodeStatus(node.Data.resourceType, node.FillPercent, "collecting")` (throttle to ~2–4 Hz, not every frame). On bank/idle, push an empty `workerState` to clear it. This reads WO-117's read-only `ResourceNode.FillPercent` + `Worker.State` — **do not duplicate that state in the HUD.**

All cross-module calls use `?.` (CLAUDE.md §6). The push is from **Village → Core seam → HUD**; the HUD asmdef stays Core-only.

---

## Ordering / dependency note

This WO's interface method **references `ResourceType`** (WO-117, `DeNelle.Core`). Two clean options for CLI:

1. **Land after WO-117's Phase-1 enum** (cleanest) — the enum file already exists; just add the methods.
2. **Land alongside WO-117** — if building in parallel, WO-117 owns creating `Assets/_Modules/Core/ResourceType.cs`; this WO only *consumes* it. **Do not have both WOs create the enum** — WO-117 is the owner of that file.

The bar + interface method are otherwise independent and can be built/brace-checked on their own.

---

## Files to Create / Edit

| File | Action | Note |
|---|---|---|
| `Assets/_Modules/Core/HUD/IVillageHud.cs` | **Edit** | Add `SetResource(ResourceType, int)` + `SetNodeStatus(ResourceType, float, string)`. Add `using DeNelle.Core;`. Keep existing `SetCrystals(int)`. |
| `Assets/_Modules/HUD/VillageHudController.cs` | **Edit** | Add `BuildResourceBar()` (called in `BindElements()` after the existing `Build*Button()` calls), the four-cell dictionary, `SetResource`, `SetNodeStatus`. Crystal cell funnels through existing `SetCrystals`. All code-built (no UXML). |
| `Assets/_Modules/Core/ResourceType.cs` | **Reference only — DO NOT create here** | Owned by WO-117. This WO consumes the enum. |
| WO-117 `HarvestService` / economy pusher | **Edit (WO-117 lane / integrator)** | After banking a haul (or on `ResourcesChanged`), call `CoreServices.Hud?.SetResource(...)` for the four resources; optional `SetNodeStatus` while collecting. Village→Core seam only. |

---

## Acceptance Criteria

- [ ] `IVillageHud` declares `SetResource(ResourceType, int)` (and `SetNodeStatus(ResourceType, float, string)`); existing members (incl. `SetCrystals`) unchanged and unbroken — **additive only**
- [ ] `using DeNelle.Core;` present in `IVillageHud.cs` (for `ResourceType`); does not create a Core→Village or Core→HUD-impl dependency
- [ ] `VillageHudController` builds a **code-built** resource bar (no UXML/`.uxml`/`UIDocument` source asset) showing **icon + count for all four** resources (Wood, Food, Crystal, Ore)
- [ ] The bar is parented to `_root`, absolute-positioned **top-right** (clear of the heart/mana/wave/compass clusters), `pickingMode = Ignore` so it never blocks clicks
- [ ] `SetResource` updates the matching cell's count; **Crystal funnels to the same cell as `SetCrystals`** (one crystal number, not two); negatives clamp to 0
- [ ] Optional `SetNodeStatus` renders a one-line readout (e.g. `"Wood node 60% • collecting"`); empty/null `workerState` clears it
- [ ] The harvest/economy layer pushes values via `CoreServices.Hud?.SetResource(...)` (with `?.`) on bank / wallet change — verified Wood/Food/Crystal/Ore visibly tick up when a node banks
- [ ] `VillageHudController` (DeNelle.HUD) references **DeNelle.Core only** — no `DeNelle.Village` reference introduced
- [ ] Brace balance passes on every `.cs` touched; no `System.Reflection` introduced in these scripts (existing reflection bridge unchanged); no `.unity` scene hand-edited; no bake fired

---

## Do NOT touch

- **Do NOT create `Assets/_Modules/Core/ResourceType.cs`** — WO-117 owns that file. Consume the enum; don't redefine it.
- **Do NOT remove or repurpose `SetCrystals(int)`** — it's already wired; the new bar sits beside / funnels through it.
- **Do NOT build any UI in UXML** — code-built only (PIPELINE_STATE.md §8: UXML doesn't render in builds).
- **Do NOT make `DeNelle.HUD` (`VillageHudController`) reference `DeNelle.Village`** — values are pushed in through `CoreServices.Hud?` setters; the HUD reads nothing back. (CLAUDE.md §5: "HUD → Core only", "passive display only, never references Village".)
- **Do NOT duplicate `GameState` wallet state or WO-117 node/worker state in the HUD** — the HUD only displays the numbers pushed to it; `FillPercent`/`Worker.State` are read off WO-117's seam by the pusher, not re-derived here.
- **Do NOT hand-edit `Village.unity` or fire any bake** (CLAUDE.md §3) — this WO is code-only; the bar is built at runtime in `BindElements()`.
- **Do NOT give the resource bar an interactive `pickingMode`** — it's a glanceable readout; a Position pickingMode would risk swallowing HUD clicks (the EnsureHudReachable history).

---

🤖 Spec'd by the design lane (UI). Reconciled against `IVillageHud.cs` (no Wood/Food/Ore method today — only `SetCrystals`), `VillageHudController.cs` (already code-builds absolute-positioned panels — `BuildStartWaveButton`/`BuildSkillsButton`/`BuildCompassRose` — mirror that for the bar; passive, Core-only), WO-117 (provides the `ResourceType` enum + banks the four payouts), WO-115 (can reuse the readout), CLAUDE.md §5/§6 (HUD→Core only, push via `CoreServices.Hud?`), and PIPELINE_STATE.md §8 (no UXML in builds). Additive interface method + one code-built bar — no new currency, no new assembly, no scene file, no bake. Markdown work order only — no `.cs` touched.
