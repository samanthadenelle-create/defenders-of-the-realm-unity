> ## RECONCILED 2026-08-08 - true status is NOT STARTED
> Audit `docs/reference/WO_TRUE_STATUS_2026-08-08.md`. Evidence: `HeartAuraController.cs:302-322`
> `StartGreenTreeAura` still calls `VFXManager.PlayKey` unconditionally, and zero commits mention 1002.
> The previous Status line read "READY TO IMPLEMENT" and was CORRECT - this WO was checked on
> 2026-08-08 and confirmed accurate, not skipped.

# WORK ORDER 1002 — Remove the yellow aura at the hub Heart of Elarion tree

**Status:** READY TO IMPLEMENT - NOT STARTED (verified accurate 2026-08-08) · **Silo:** Hub/VFX · **For:** CLAUDE CLI · **Date:** 2026-08-07
**PO:** Samantha (owner) · **Author:** UI seat · **UI-seat block:** 1000–1099
**Owner (felt-test 2026-08-07):** *"remove the yellow."* A big yellow glowing plume engulfs the base/roots of the Heart of Elarion tree in the hub. It's the same over-hot yellow class as the harvest-node plume (WO-890 subtlety ruling).

## 0. Grounded source
`Assets/_Modules/Village/Heart/HeartAuraController.cs` drives the Heart's aura:
- The WHITE `Aura_HeartPulse` swirl is **already withheld** on the hub centerpiece Heart (`_suppressWhiteSwirl` L126; withheld at L206-211) — the code comment (L110) notes the owner has **repeatedly asked** this stray heal VFX be gone.
- But a **separate tree-ambient loop** plays regardless: `PlayKey(TreeAuraKey)` FireFlies at the crown (L306-322). Something in this path (the tree aura, its recipe, or its procedural/gold fallback) is rendering as the **yellow plume at the tree base** — it is NOT subtle and it is NOT the withheld white swirl.

## 1. The fix
**On the HUB static-town Heart of Elarion tree, remove the yellow aura entirely.**
- Identify the VFX producing the yellow glow at the tree base (start with `TreeAuraKey`/`PlayKey` at L306-318 and any aura parented to the Heart anchor/roots; confirm at source which handle draws the yellow — don't guess).
- **On the hub centerpiece Heart, suppress it** the same way the white swirl is suppressed (extend the `_suppressWhiteSwirl` / hub-detection gate to cover the tree-base aura too, or stop that handle when `IsHubCenterpieceHeart`). The hub tree should carry **no glowing aura at its base** — it is a clean world-tree centerpiece.
- Do NOT remove auras from **combat/raid Hearts** — those keep their aura (the withhold rule is hub-only, per canon).
- If a subtle crown sparkle is wanted later that's a separate ask; for now, **kill the yellow** — nothing at the base.

## 2. Also (same class — verify)
The identical yellow plume appeared on a **harvest node** earlier (WO-890). If the two share a source (a common recipe / the gold procedural fallback rendering a flame), fix the root once and both go away. Either way both must obey the WO-890 subtlety ruling: **no yellow plume that swallows the object.**

## 3. Acceptance
- [ ] The hub Heart of Elarion tree has **NO yellow glow/plume** at its base or roots — clean tree.
- [ ] The white `Aura_HeartPulse` swirl stays withheld on the hub (unchanged).
- [ ] Combat/raid Hearts still show their aura (hub-only withhold).
- [ ] Headless-capture the hub with the tree — open the PNG, confirm no yellow.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK`.

## 4. RESULT
`WorkOrders/WORK_ORDER_1002_remove_hub_heart_tree_yellow_aura.RESULT.md` — before/after of the hub tree, and the identified source.
