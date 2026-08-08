> ## RECONCILED 2026-08-08 - true status is DONE
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: 55d4df9b; ConstructionWorker.cs 538 lines new plus BuilderWorkerWork.controller.
> The previous Status line read "Status: READY TO IMPLEMENT" and was wrong; the board understated this.

# WORK ORDER 871 — Worker animation during build / upgrade timers

**Status:** DONE
**Author:** UI/QA triage (read-only, §13) — Claude UI
**Lane:** World/VFX + Animation. **WO#:** UI-seat block; **871**=this.
**Origin:** owner 2026-08-04 — *"is it possible to have worker animation play during upgrades or building timers?"*
**Answer: yes — the lifecycle hook already exists.**

---

## 1. Feasibility (confirmed from code)
`Assets/_Modules/Village/BuildMode/UnderConstructionVisual.cs` is THE per-building "is this under construction" hook:
- Attached to any building/structure with a live timer via `Attach` / `AttachToBuildingId` (city, resource, tower,
  placement seams — `:70-141`).
- While `BuildTimerService.IsBuilding(key)`: it dims the structure, silences combat, floats a world-space countdown,
  **and already holds a persistent VFX loop** — `_upgradeLoop = VFXManager.PlayKey("UpgradeVisual_Aura", …, parent)`
  (`:190`), the owner-tagged circling orb.
- On `JobCompleted` / the `Update` self-heal / `OnDestroy`: `Reveal()` restores the structure, `StopUpgradeLoop()`
  drops the loop, and the component removes itself (`:332-416`) — a proven WO-753-style one-owner teardown.

So a **worker animation is a parallel addition to this exact component** — spawn a builder while the timer runs, tear
it down the same way the aura already is.

## 2. The build
Add a "worker/builder" to `UnderConstructionVisual`'s lifecycle:
- **In `Bind()`** (alongside `_upgradeLoop`): spawn/enable ONE builder NPC positioned beside/in front of the
  structure, facing it, playing a **work/hammer animation loop** for the duration of the timer.
- **In `Reveal()` and `OnDestroy()`** (alongside `StopUpgradeLoop`): despawn the worker — mirror the `_upgradeLoop`
  handle pattern so it is impossible to orphan (WO-753 one-owner teardown). A cancelled / moved / torn-down build
  removes its worker.
- **Reuse the KayKit NPC body + animator** (WO-818 structure NPC models + the WO-833 `KayKitNpcIdle` retarget
  pattern) — a generic **builder**, NOT an Echo (Echoes are portrait-card spirits, never 3D — keep them distinct).
- **Animation:** a "work"/"hammer" loop clip. **Browse the owned anim library first — `docs/asset-inventory/`
  (`04_vfx_spells_audio.md` Action = 401 Mixamo Humanoid clips; `01_kaykit.md` KayKit Character Animations 1.1;
  Supercyan ~51 combat anims).** Confirm a work/hammer clip exists there; if not, the owner TAGS the
  work-animation key (same owner-tags-the-key/CLI-maps-verbatim rule as the VFX, memory
  `vfx-map-owner-tags-no-creative-pick`) — do NOT creative-pick or author a new clip.

## 3. Performance / lightweight (keep it cheap)
- **Bounded:** only buildings actively under construction carry a worker — usually few. One worker per building.
- **Pool the worker body** (don't Instantiate/Destroy per build); despawn (return to pool) on `Reveal`/`OnDestroy`.
- No per-frame allocation; the worker just loops its clip + faces the building. It rides the same opt-in-by-attachment
  gate as the scaffold, so baked/enemy/finished structures never spawn one.

## 4. Acceptance
- [ ] While a building/tower has a live build or upgrade timer, a builder NPC stands at it playing a work animation;
      it despawns the instant the timer completes (or the build is cancelled/moved).
- [ ] No orphaned worker on completion/cancel/scene-teardown (mirror the `_upgradeLoop` handle discipline).
- [ ] The worker is a KayKit builder (pooled), NOT an Echo; animation is an existing/owner-tagged clip (none authored).
- [ ] Bounded + pooled (no per-build Instantiate/Destroy churn); `CompileGate` green; verified on-device.

## 5. Do NOT
- Do NOT author a new animation clip — use an existing / owner-tagged one.
- Do NOT make the worker an Echo (Echoes stay 2D portrait spirits).
- Do NOT Instantiate/Destroy per build (pool it); do NOT leave a worker orphaned on teardown (WO-753).
- Do NOT reinvent the "is under construction" state — reuse `UnderConstructionVisual` / `BuildTimerService.IsBuilding`.
