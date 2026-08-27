# WORK ORDER 950 — Drillmaster (+ teach toast) appears on a blank-town save with NO barracks

**Status:** CLOSED 2026-08-27 — owner felt-tested PASS on APK 2026.08.27.343739 (village review).

*(Board note 2026-08-24: bucket corrected DONE/IMPLEMENTED → **FIXED**. Nothing about the work changed — §13 reserves DONE/closing for the PO, and this line's own text says the owner's felt-verify is still owed, so the row belongs in the felt-test queue, not the closed pile.)*
**Minted:** 2026-08-10 (CLI seat, main line — banner bumped 950 → 951 in the same edit)
**Silo:** Village/NPCs (BarracksNpcInjector) + the singleton/blank-town gate seam
**Origin:** owner felt-report 2026-08-10 (*"i have a drillmaster but have not placed the barracks yet.
concern?"*) — RCA'd live from Player.log the same minute.

---

## 1. Proving lines (captured, §12 satisfied)

| Line | Meaning |
|---|---|
| `[Flow:Singleton] EnforceAll: swept 12 singleton catalog row(s) (9 authoring baked twins) - surfaced=0 suppressed=0 alreadyDown=9 (blank-town gate).` | The WO-834 gate is ACTIVE and healthy on this save — zero baked twins surfaced |
| `[Flow:Harvest] existence gate ... everBuilt=[<empty>]` | `everBuiltStructureIds` is EMPTY — a genuine blank founding, barracks never built |
| `[Flow:Village] BarracksNpcInjector: placed the drillmaster NPC at the Barracks.` (Inject, from OnSceneLoaded) | The injector found an ACTIVE `CastleBarracks` root and seated the NPC anyway |
| `[Flow:UI] kit toast -> 'Elarion needs soldiers. The drillmaster at the Barracks trains them.'` + `[Flow:Barracks] WO-813 once-teach fired (barracks_intro marked seen).` | The one-shot teach burned, pointing the player at a building they never built |

Contradicts the SHIPPED WO-813 rule: *"Drillmaster only spawns if CastleBarracks (or future placed
barracks) exists — injectors no-op if missing"* — intended to mean a LEGITIMATELY-present barracks.

## 2. The gap (verified at source, `BarracksNpcInjector.cs`)

- The 1 Hz live-unlock poll path DOES check the blank-town gate:
  `if (!StructureSingleton.MayBakedTwinSurface(StructureId)) return;` (~:124).
- The **OnSceneLoaded → Inject() path does NOT** — it gates on `BarracksUnlock.IsUnlocked`
  (ff.barracks + Onboarded, ~:152) and then finds `CastleBarracks` by name. Two candidate mechanisms
  for the root being active (pin which with one captured ordering line before fixing):
  (a) the baked `CastleBarracks` fixture is not among the 9 singleton-swept baked twins, so nothing
  ever deactivates it on a blank save; (b) Inject raced EnforceAll on scene load (found it active,
  sweep deactivated it after — leaving an orphaned NPC at a hidden building).

## 2b. ⚠ SECOND HALF, same session (F8 seq 2267, 2026-08-10 11:00) — the PHANTOM FOOTPRINT

Owner, standing at **(20.9, -4.4)** (`[Flow:Zone] GetZone(x=20.9,z=-4.4)`): *"feels like a building is
here... my guess is it reserved the footprint for the barracks since its near the npc."* **Confirmed:**

- `CastleBarracksPlacer.cs:47` authors the baked barracks at anchor + offset **(16, 0, -4)** — she is
  standing directly beside it.
- `HubStructureVisualInjector.cs:402`: the suppression path *"Hide the baked visual (renderers only —
  NPC point + colliders/logic stay live)"* — so a gate-suppressed baked barracks keeps its SOLID
  colliders: an invisible building that blocks movement. The same file documents this hazard class
  itself at `:431-433` ("a phantom wall where nothing is visible") and already disables baked
  non-trigger colliders on that OTHER path — the discipline exists, the suppression path just does
  not apply it.

## 3. Fix shape

1. Add the SAME `MayBakedTwinSurface("barracks")` early-return to the Inject() scene-load path when
   the found root is the BAKED twin (a PLACED barracks — SingletonResolved reseat — stays exempt).
2. Reconcile ownership: if `CastleBarracks` is not a swept singleton row, either add it to the
   authority or document why the injector-side gate is the single guard. ONE owner per concern.
3. The once-teach must not burn while the gate is closed (barracks_intro seen-flag only sets when
   the drillmaster legitimately seats). ⚠ The owner's CURRENT save has already burned it — decide:
   reset tolerance (a dev-only unseen flip) or accept for this save; note in RESULT.
4. **Phantom footprint:** when a baked twin is gate-suppressed (blank town), the suppression must
   ALSO disable its non-trigger colliders + any nav obstacle — mirror the `:431-433` discipline the
   same file already applies on its skin path (keep trigger colliders only if the NPC point is
   legitimately live, which on a suppressed blank-town twin it is NOT). Restore them on surfacing.
5. Regression: blank-town fixture (everBuilt empty, Onboarded true, ff.barracks on) → injector
   refuses, no NPC, no toast, AND the suppressed twin has zero enabled non-trigger colliders;
   placed-barracks fixture → seats + colliders live. Follow an existing NPC/singleton suite's pattern.

## 4. What NOT to touch

`BarracksUnlock` semantics · the SingletonResolved placed-barracks reseat (placed wins) · WO-822
teach content · the WO-834 gate itself.
