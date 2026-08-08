> ## RECONCILED 2026-08-08 - true status is DONE
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: f359ece2; PortalRebuildRegression.cs is new in the tree.
> The previous Status line read "Status: READY TO IMPLEMENT" and was wrong; the board understated this.

# WORK ORDER 869 — Dungeon Portal: REBUILD (design + URP material + MagentaGuard widen + Ultimate VFX aura)

**Status:** DONE
**Author:** UI/QA triage (read-only, §13) — Claude UI
**Lane:** World/VFX + Art. **WO#:** UI-seat block; **869**=this.
**Source:** `docs/ui-review/2026-08-04-seeker/README.md` §5 + `08-portal-magenta.png` (Seeker).

---

## 1. Scope — this is a REBUILD, not a repair (owner ruling)
> Owner: *"we need that portal to look way better … the whole thing needs redone."*
The current arch is a **flat rectangular frame — no depth, no threshold, no sense it leads anywhere**, sitting alone
in open ground. It must read as a way **INTO somewhere**: a frame, an **active threshold surface**, and an **aura**
that makes it a landmark you can navigate toward — **visible and legible from across the field at 2340×1080**, not
just up close. **Do NOT shader-swap the material, watch it render, and close the ticket** — the portal design itself
is the deliverable.

## 2. The magenta is CONTEXT (why it looks that bad), not the deliverable
That specific magenta is **Unity's missing/incompatible-shader error colour** — the portal has NO working material
(rendering the error material), plus a **second set of broken materials** as the blue blocks inside the arch. Adding
aura on top would still leave a magenta frame. This is the project's known pink-material failure
(`CastleHubBuilder.cs:2233`, `CastleWallKitSpawner.cs:52`, `CastleBuilderTester.cs:263`, `EnsureShadersIncluded.cs:46`;
memory `never-inference-fix` — do NOT guess, the material is named below).
- **Candidate materials (Tripo import → non-URP shader is the likely cause):**
  `Assets/Resources/Structures/Materials/Portal_To_Dungeon_basecolor.mat` and
  `Assets/Art/TripoStructures/Materials/Portal_To_Dungeon_basecolor.mat`.
- Whatever art the rebuild lands, its material MUST be **URP-compatible** (reassign to URP/Lit or run
  `Defenders > Art > Fix Polyperfect URP Materials`-style fix) — else it ships magenta again. Fix the inner
  blue-block broken materials too.

## 3. ⚠ Widen `MagentaGuard.cs` — part of THIS fix, not a follow-up
`MagentaGuard.cs` EXISTS and did NOT catch this. Determine why (not run on this asset path / not in this scene /
scoped to a set the portal isn't in) and **widen it so this class cannot ship again**. A guard that misses the thing
it exists to catch buys false confidence — worse than none. This is a required deliverable of the WO.

## 4. Use the EXISTING aura VFX — author NONE
**Browse the owned VFX library FIRST — `docs/asset-inventory/04_vfx_spells_audio.md`** (Mirza Beig 564 prefabs incl.
portals; Spells Pack 466 incl. aura/portal-adjacent effects). We wire only ~38 of ~1,000 owned VFX — the portal
aura is already in there; nothing is authored.
The aura already ships: **`Mirza Beig / Particle Systems / Ultimate VFX / Prefabs / Loop/`** —
`pf_vfx-ult_demo_psys_loop_ghostPortal`, `…_ghostPortal2`, `…_portalBlue`, `…_portalBlueTutorial`, `…_portalOrange`.
**Use these; do NOT author new VFX.** Route through the existing **`VFXManager`** (pooled, quality-gated) — do NOT
instantiate particle prefabs directly — and respect the **WO-753 one-owner teardown** so a destroyed portal never
orphans its effect. Per the VFX-tagging rule (memory `vfx-map-owner-tags-no-creative-pick`), the owner tags which
portal prefab; CLI maps the key → hook **verbatim** (no creative substitution).

## 5. Order of work (it matters)
1. **Design the portal** (frame + active threshold + aura; reads as a landmark) — start here, NOT at the shader.
2. **URP-compatible material** (§2) — or it ships magenta regardless of design quality.
3. **Widen `MagentaGuard`** (§3) so this class can't ship again.
4. **Dress with the Ultimate VFX aura** (§4) via VFXManager.

## 6. Acceptance
- [ ] The portal reads as a way INTO somewhere (frame + active threshold + aura), legible across the field at
      2340×1080 on the Seeker — NOT a flat magenta rectangle.
- [ ] NO magenta anywhere on the portal (frame OR the inner blue blocks); material is URP-compatible.
- [ ] `MagentaGuard` is widened and now CATCHES this asset/path/scene (a test that would have failed on the old mat).
- [ ] Aura is an EXISTING Ultimate VFX prefab routed via `VFXManager`; destroyed portal tears down its effect (WO-753).
- [ ] `CompileGate` green; verified on-device (world-space — headless can't see it).

## 7. Do NOT
- Do NOT shader-swap-and-close (the rebuild is the deliverable). Do NOT author new VFX. Do NOT instantiate prefabs
  outside `VFXManager`. Do NOT guess the material cause — it's named. Do NOT leave `MagentaGuard` un-widened.
