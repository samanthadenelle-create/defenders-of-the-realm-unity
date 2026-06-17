# Morning walkthrough — review these (session of 2026-06-16, pushed)

11 commits on `feat/tower-core-loop`, all `COMPILE_GATE_OK`, pushed (`3cf63df3..7e6ce088`).
Full detail in `docs/SESSION_SUMMARY_2026-06-16_overnight.md`. Walk these in-Play:

## ✅ Verify in Play
1. **Per-member equip** (the big one) — open the equip panel: a target row (hero + companions)
   sits above the WEAPONS/ARMOR tabs. Play as Grom, pick **Sylas** → list shows only Ranger-usable
   gear → assign the bow → it persists, scales his attacks, and shows on his body. Armor obeys
   **light = Ranger/Mage, heavy = Knight/Cleric**.
2. **Blink sword** — `knight_starter` now equips the Blink `Sword1h_01`. **Eyeball grip/orientation**
   in-hand (native prefab; nudge the `knight_starter` gripEuler if off).
3. **Camp → base → harvest** — clear a camp in OuterWorld → it becomes a CoC **square double-wood-wall**
   base with a courtyard → 4 renewable harvest nodes ([F] to extract) appear inside. **Eyeball node
   placement** (math-derived offsets — may overlap the OutpostHub; easy nudge). No more silent-fail
   "defend" stage.
4. **Yarn** — OpenUpgrade / OpenCraft / OpenEquip / OpenArena / OpenRumorBoard no longer throw
   "no node" / wedge the dialogue.
5. **Companion clone** — pick Ranger/Cleric/Knight and reload a few times: the **"second one is me"**
   clone is gone (party count unchanged, just no duplicate body).
6. **Art renders** — harvest node models (wood/iron/food/crystal), **PetHouse2** as Echo Hollow, and
   the lightweight arcane tower all render right (watch orientation on the Tripo exports).
7. **Town team UI** — party frames are a compact strip on the **left** now (was a wide block).

## ⚖️ One decision for you
- **`WORK_ORDER_430`** — weapons/armor "seed JSON → DB + pull from DB." The DB/endpoint half lives in
  the **separate Vercel backend repo** (a WebGL build can't hit a DB directly, only REST). My rec:
  **seed the DB now, keep local JSON authoritative for the demo** until the endpoint is proven
  fast/cached. Pick the path and I'll wire the client half.

## 🧹 Done as housekeeping (FYI)
- Deleted the dead **7.5 MB `PetHouse.fbx`** chain (Echo Hollow uses the ~80 KB PetHouse2 now).
- Harvest models committed to LFS so a clone/CI has them. (`Resources/Structures` stays gitignored
  like the polyperfect/Quaternius packs — PetHouse2 lives there, local like the art packs.)

## 🔎 From your F8 fast-play (2026-06-17 ~02:50–04:26) — triage in the morning
- **Echo Hollow still shows the STABLES** — confirmed a **bake** issue (you called it). `CityManifest.json:113`
  hard-places PetHouse as `polyperfect/.../Stables_Medieval.prefab`. The catalog repoint to PetHouse2
  only affects runtime/build-mode placement, not the baked town instance. **Fix:** repoint that manifest
  line to PetHouse2 (needs a `prefabKind` that loads `Resources/Structures/PetHouse2`, not the polyperfect
  kind) + **rebuild the scene with the editor CLOSED**. PREP PENDING.
- **Yarn "no node" STILL throwing** — 3× this session (02:52, 02:53, 04:24), *after* the sibling fix
  (`8ed0bce9`). So either the editor hadn't recompiled the fix, or it's a **different** Yarn command than
  the 5 converted (OpenShop/Upgrade/Craft/Equip/Arena/RumorBoard). break-log doesn't name the command —
  check Player.log around those UTC stamps for the `[DialogueCommandBridge] <command>` line just before it.
- **"doesnt follow"** flag (04:26) — companion/pet not following. May relate to tonight's companion changes.
- **"weapon placement feels off"** flag — matches the bow-90°/sword-grip note. Hero bow = HeroBowAttachment
  (loaded weapon pack, fine); **companion** bows use `EquipmentController.Bow()` (one-line gripEuler fix if off).
- **"fight much better"** (positive) — companion gear scaling (`_gearWeaponMult`) is landing.

## Notes
- Nothing here is pushed-and-forgotten — if a playtest flags something, it's a quick fix.
- The "pets makeover and wiring" + "arcane tower replaced" art-lane changes are yours/another
  session's — confirm they look right alongside the above.
