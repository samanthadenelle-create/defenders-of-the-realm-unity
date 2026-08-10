# WORK ORDER 951 — Echo Hollow repurposed: tap it → the Echoes popup opens. Simple and easy.

**Status:** DONE (implemented + gated 2026-08-10; RESULT filed; owner felt-verify owed)
**Minted:** 2026-08-10 (CLI seat, main line — banner bumped 951 → 952 in the same edit)
**Silo:** Village (Hollow interactable routing) + HUD panel routing — small, one-verb change
**Origin:** owner F8 seq 2266 (*"Given how we do the Echos on levels does this still fit? If it
doesnt fit, should we remove echo hollow? repurpose? make a pet store, so they can buy skins?"*) +
her confirmation of the repurpose direction, verbatim: *"so then when they go to the store they open
the echos pop up on right? Simple and easy."*

---

## 1. The ruling (CONFIRMED core)

The Echo Hollow (`pet-house`) is NOT removed and NOT a skins store. It is the Echoes' building:
**interacting with the Hollow (tap the building / talk to its keeper NPC) opens the existing Echo
roster popup** — the same Echoes screen the HUD reaches. One tap, no new UI. The keeper NPC's Talk
routes there instead of (or in addition to — check what its dialogue does today) its current menu.

## 2. Implementation notes

- Reuse the existing panel: the Echoes roster (HUD "Echoes"; PanelManager-registered). Route via the
  existing `DialogueCommandBridge` vendor-verb pattern or `BuildingInteractable` action — whichever
  the Hollow's interact already uses; do not invent a new open path. `PanelManager` single-modal
  discipline applies.
- The tutorial's BUILD ONE beat (WO-1012 arc) builds the Hollow — with this ruling the pet-Echo
  guide's utility line gains a natural pointer ("build me a home"); coordinate copy with the WO-1012
  lane, do not fork it.
- FlowTrace on the open route; ASCII; MinTouch unchanged (existing surfaces).

## 3. RECOMMENDED extensions — NOT pinned, owner may adopt later

(From the 2026-08-10 design discussion; each is its own small follow-up if adopted.)
1. **Capacity job:** Echo slots 2+ require the Hollow to exist (buildings house things — the CoC
   grammar); Hollow upgrades could raise the roster cap.
2. **Awakening stage:** newly level-unlocked Echoes wake AT the Hollow; the per-Echo teaching
   conversation stages there.
3. **Skins counter (much later, monetization-gated):** the keeper sells Echo cosmetics via the
   existing Glimmer/cosmetics rails — a layer on the building, never its identity.

## 4. What NOT to touch

The Echo lanes/assignment logic (WO-811 lane, uncommitted) · the roster panel internals · pet deploy
logic · the WO-1012 tutorial files (pipeline in flight — coordinate, don't edit).
