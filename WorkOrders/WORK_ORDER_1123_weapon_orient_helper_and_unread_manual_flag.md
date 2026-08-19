# WORK ORDER 1123 — The orient canon's central deliverable was never built, and its canon flag is read by nothing

**Status:** READY TO IMPLEMENT (⚠ step 1 is a measurement — §4)
**Minted:** 2026-08-19 (CLI seat) — banner bumped 1123 → 1124 in the SAME edit
**Lane:** Hero gear orientation. `Assets/_Modules/Core/Geometry/` + `Assets/_Modules/Village/Hero/`.
No scenes, no bake, no catalog *values* changed.
**Silo:** Structural / holistic. **This is leverage, not a feature** (`ARCHITECTURE_PRINCIPLES.md` §3) —
it earns its own ticket precisely so it is never smuggled into a player-facing seating fix.
**Priority:** MEDIUM. Nothing here is store-blocking. It is the reason seating defects keep recurring.
**Provenance:** gear-seat coverage audit, 2026-08-19 (CLI seat), run against the four weapon families at
the owner's request. Findings 6 and 10 of that audit.

---

## 1. THE TWO DEFECTS, BOTH PROVEN FROM FILES

### 1.1 `WeaponOrientHelper` DOES NOT EXIST

`docs/ARCHITECTURE_PRINCIPLES.md` §4 is binding law and names it directly:

> "This is already the law for structures (`CatalogOrientationBaker`) and the bow
> (`HeroBowAttachment.NormalizeInto`); **it MUST generalize to every weapon + armor via
> `WeaponOrientHelper`, applied at equip + adjustable in dev builds through our DevOrient tooling.**"

`docs/WEAPON_ARMOR_ORIENT_LOGIC.md` devotes a whole section to it — "The system — `WeaponOrientHelper`",
steps 1-5 — and opens by naming the failure it exists to end: *"weapons got slapped onto the hand at
identity."*

**It was never written.** `find Assets -name "WeaponOrientHelper*"` returns nothing. What exists is
`Assets/_Modules/Core/Geometry/WeaponBoundsOrient.cs`, whose entire public surface is `NormalizeInto` /
`ComputeBowHeldRotation` / `TryAspectRatio` — **bow-specific, not generalized.**

The consequence is measurable, not theoretical. Per family, on a missing offset row:

| family | what actually happens on a miss | cite |
|---|---|---|
| bow (hero + companion, drawn AND sheathed) | **DERIVED** — `ComputeBowHeldRotation` | `WeaponBoundsOrient.cs:362`; `HeroBowAttachment.cs:281-284`; `EquipmentController.cs:1099-1110`, `:2242-2245` |
| melee (sword/staff/axe/hammer/wand/dagger), drawn | `ComputeMeleeGripRotation` + a per-family hand-typed nudge | `EquipmentController.cs:1077`, `:2660-2672` |
| melee, sheathed | `ComputeSheathRotation` — derived baldric | `:2246`, `:2582-2596` |
| **shield, drawn (native)** | **IDENTITY** ∘ 180° yaw. No derivation of any kind. | `:1906-1917`, `:1954` |
| **shield, sheathed** | **hand-typed `_sheatheOffHandLocalEuler = (0, 90, 192)`** ∘ 180° yaw | `:2283`, `:397` |

The shield rows are the exact construct the canon bans. `EquipmentController.cs:397`'s own comment
concedes the euler has *"no relationship to geometry OR the chest-bone axes."* Meanwhile
`WEAPON_ARMOR_ORIENT_LOGIC.md` mandates, per archetype: **sword** longest→+Y blade-up, hilt at the
widening cross-guard, grip below it, never blade-in-hand; **shield** flat face forward, centre→hand;
**staff** longest vertical, grip lower third; **bow** — `NormalizeInto` is named as *"the weapon seed to
generalize."* The seed exists. Nothing was grown from it.

### 1.2 `manual: true` IS AUTHORED 81 TIMES AND READ ZERO TIMES

`ARCHITECTURE_PRINCIPLES.md` §4: *"A `manual=true` correction is **canon and is NEVER overwritten** by
the auto pass."* `WEAPON_ARMOR_ORIENT_LOGIC.md:45-46,61-62` repeats it twice.

**81 of the 96 rows in `Assets/Resources/Data/Canonical/weapons.json` carry `manual: true`.**
`WeaponDef` (`Assets/_Modules/Village/Hero/GearCatalog.cs:59+`) **does not declare the field**, and no
consumer of it exists anywhere in the gear path. The flag is inert.

It is honoured for STRUCTURES (`CatalogOrientationBaker.cs:60-61`, `StructureFactory.cs:151`,
`GhostPreview.cs:120` — three readers), which is what makes the gear side's silence a genuine asymmetry
rather than a feature that was never designed.

**Why this is dangerous rather than merely untidy:** the flag reads as protection to anyone author­ing
gear. A seat that sets `manual: true` on a weapon row today believes it has locked that row against an
automatic pass. It has not. The first auto-orient pass over weapons — which §1.1 is asking someone to
build — would silently overwrite all 81.

> ⛔ **THESE TWO DEFECTS MUST BE FIXED IN THE SAME CHANGE, IN THIS ORDER: honour the flag FIRST, then
> build the deriver.** Building `WeaponOrientHelper` while `manual` is still unread is precisely how the
> owner's dialled poses get erased — the structure side already paid this bill once (the 2026-08-18
> axis-bake pass zeroed corrections it believed were redundant, and the town lay down).

---

## 2. WHAT THIS IS NOT

- **Not a fix for the default shield.** That is its own live defect (unauthored in both poses,
  `ShieldWithItemLogic`) and is being handled separately by an owner dial in the Seating Editor. This WO
  must not re-dial, overwrite, or "derive over" any pose the owner authors there.
- **Not a licence to touch `shield_A` / `shield_A@sheathed`** (`offsets.json:4`, `:244`). Those are
  owner-dialled, `fullOverride`, still live for `tripo_shield_a`, and are the last known-good shield
  poses on record (`Builds/wipe-test3-logcat.txt:115665`, `:113699`). **Reference them; never re-dial them.**
- **Not a bow change.** The bow's derived path is felt-verified by the owner (2026-08-19) and is the
  MODEL to generalize from, not a thing to modify.

---

## 3. ACCEPTANCE CRITERIA

1. **`manual` is read.** `WeaponDef` declares it; any automatic orientation pass over weapons/armor skips
   a `manual: true` row untouched. Prove it by running the pass twice over a dialled row and diffing —
   zero delta.
2. **`WeaponOrientHelper` exists** in `DeNelle.Core` (so `Village`, `Pets` and `Dungeons` can all read it
   across the asmdef boundary) and implements the per-archetype rules from
   `docs/WEAPON_ARMOR_ORIENT_LOGIC.md` — derived from **mesh bounds + asset name**, never a typed Euler.
3. **Shield gains a real derivation** for both poses — "flat face forward, centre→hand" — replacing
   identity (drawn) and `(0, 90, 192)` (sheathed). The hand-typed constants stay in the code as the
   documented fallback; they are not deleted (§12: instrumentation and fallbacks are never stripped).
4. **An authored row still wins.** Precedence is: owner offset row → `manual` → derived → archetype
   default. Assert that order with a test, in that order.
5. **The blind regression is fixed.** `AttachmentOffsetRegression.cs:103,122` asserts `shield_A` /
   `shield_A@sheathed` — the LEGACY mesh — so it passes green while the live default shield is broken.
   It must assert the mesh key the **starter loadout actually equips**
   (`GearLoadout.cs:85` → `knight_shield_starter` → `ShieldWithItemLogic`), derived from the loadout at
   test time rather than hard-coded, or it will go blind again at the next swap.
6. **Every assertion added can FAIL.** State, per assertion, what broken state makes it print differently.
7. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` (read the count off the marker, never a doc).

---

## 4. ⛔ STEP 1 IS A MEASUREMENT, NOT AN EDIT (CLAUDE.md §12)

Headless gates cannot see orientation. Before any deriver is written:

1. Capture the CURRENT seat of one weapon per family — sword, shield, staff, bow — in BOTH poses, with
   the measured rotation and the mesh bounds that produced it. The `[Flow:Offset]` /
   `[Flow:Equip] BowOrient` / `TraceBowSeatMeasured` seams already emit this; no new instrumentation is
   needed to read the before-state.
2. **Screenshot each one.** A derived value can be arithmetically perfect and land wrong one transform up
   the chain — that is exactly how the bow's held rotation shipped 90° out
   (`docs/ARCHITECTURE.md:155-159`). For anything the player sees pointed a direction, the screenshot is
   the evidence, not the gate.
3. Make a **falsifiable prediction** of each post-fix rotation before running it, and diff prediction
   against measurement afterwards.

**Known runtime gaps this measurement must fill** (from the 08-19 audit — all currently UNPROVEN):
`kind=Bow` has never appeared in any log for real gear; `ComputeBowHeldRotation` is proven only against a
synthetic `SynthBow` probe; **no staff has ever been captured on `SheatheSocket_Back`**; and
`Builds/dr-night3.log:67373` reports `'ranger_starter' -> Resources 'Heroes/Props/Weapons/bow_A'
MEASURES NOTHING [AllInactive]`, which needs explaining before any bounds-derived helper trusts that path.

---

## 5. FILES

| file | why |
|---|---|
| `Assets/_Modules/Core/Geometry/WeaponBoundsOrient.cs` | the seed — `NormalizeInto`, `ComputeBowHeldRotation`, `TryAspectRatio` |
| `Assets/_Modules/Core/Geometry/WeaponOrientHelper.cs` | **NEW** — the generalization |
| `Assets/_Modules/Village/Hero/GearCatalog.cs` | `WeaponDef` must declare + expose `manual` |
| `Assets/_Modules/Village/Hero/EquipmentController.cs` | `:397`, `:1906-1917`, `:1954`, `:2283`, `:2660-2672` — the miss paths |
| `Assets/Editor/Regression/AttachmentOffsetRegression.cs` | `:103`, `:122` — the blind assertions |
| `docs/WEAPON_ARMOR_ORIENT_LOGIC.md` | BINDING canon — read in full first; update in the same commit (§15) |

**Do NOT touch:** `Assets/OffsetForge/offsets.json` values (owner-dialled); `HeroBowAttachment`'s derived
path (felt-verified); any structure orientation channel — different lane, different ticket.

---

## 6. WHY IT IS WORTH DOING AT ALL

Every gear-seating defect this project has shipped has the same shape: a value that was hand-typed once,
for one mesh, that no longer matches the mesh actually equipped — and no instrument that can see it.
`shield_A@sheathed` is authored and never read. `ShieldWithItemLogic@sheathed` is read and never authored.
The guard asserts the first and is blind to the second. **74 of 80 equippable mesh keys have no authored
row in either pose**, so today the answer for most of the wardrobe is whatever the fallback happens to do.

A deriver does not make the owner's dials unnecessary — it makes them **rare, and permanent**: the
geometry gets it close, she corrects what feels wrong, and `manual` protects that correction forever.
That is the loop `ARCHITECTURE_PRINCIPLES.md` §4 describes and the one the gear path has never had.
