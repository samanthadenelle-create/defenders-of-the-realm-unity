> ⚠ **HISTORICAL (dated record) — reads as live but predates current state.** Branch is now `wip/village2-and-f8-tickets` (nothing pushed); hero = single Tripo Knight (Blink junked 06-22). Kept for history. Live reality: `CANON_GROUND_TRUTH_2026-06-26.md`.

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

## 🌙 Did overnight (safe, gated, committed — `86bfa73a`)
- **Yarn "no node" — INSTRUMENTED, not blind-patched.** `DialogueCommandBridge.RunDeferred` (the single
  choke-point for every sync command) now `FlowTrace.Step`s the command name on each dispatch. **Next
  play:** reproduce the "no node" then check `Player.log` — the `[Flow:Yarn] dispatch command '<name>'`
  line right before the exception names the culprit. Strong suspects: `transition_to` /
  `enable_full_controls` (sync commands that END the dialogue → VM then Continues → throws). Most
  likely, though, the 04:24 capture was the **editor running pre-fix code** (Unity doesn't recompile
  during Play) — so first just confirm on a FRESH play whether it even still happens.

## 🛠️ PetHouse2 — SOLVED via a runtime injector (no bake needed)
**Update (2026-06-17 AM):** rather than rebuild the primary scene blind, the Echo Hollow stables is now
swapped to PetHouse2 at **runtime** by `EchoHollowVisualInjector` — the project's established no-scene-edit
pattern (CampSystem / StoryCompanionInjector). On every hub load it finds `EchoHollow_Pets_RoamingArea`,
hides the baked stables renderers (keeps the NPC point + roaming logic), and skins PetHouse2 in via
`VisualFactory.Skin(SkinOptions.Structure(7f))` — which **bounds-fits** the raw FBX to ~7 m AND **URP-fixes
the embedded Tripo materials** (so it won't render magenta, which a builder bake WOULD have, since
PetHouse2 imports with embedded materials). Idempotent + graceful (PetHouse2 missing → stables restored).
- **Just play on this PC** — Echo Hollow shows PetHouse2, no bake step. Eyeball: size (change `TargetSizeM`)
  and facing (Tripo FBXs often import +X; I can add a yaw to the skin if it faces wrong).
- If you ever want it truly baked into the scene instead, the builder swap is still an option — but the
  injector is cleaner (zero primary-scene risk, correct materials) and matches the rest of the hub injectors.

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
- **"doesnt follow"** flag (04:26) — companion/pet not following. **Instrumented overnight:**
  `PetHeroLeash` now warns ONCE if it can't resolve the hero within 5s (was a silent failure) — so the
  next capture says whether the PET even found the hero. (My companion changes don't touch movement, so
  a non-following *companion* would be a separate, pre-existing case — `StoryCompanion` already
  FlowTraces its leash; check `[Flow:Roster]` / its leash logs if it's a companion not the pet.)
- **"weapon placement feels off"** flag — matches the bow-90°/sword-grip note. Hero bow = HeroBowAttachment
  (loaded weapon pack, fine); **companion** bows use `EquipmentController.Bow()` (one-line gripEuler fix if off).
- **"fight much better"** (positive) — companion gear scaling (`_gearWeaponMult`) is landing.

## Notes
- Nothing here is pushed-and-forgotten — if a playtest flags something, it's a quick fix.
- The "pets makeover and wiring" + "arcane tower replaced" art-lane changes are yours/another
  session's — confirm they look right alongside the above.
