# WORK ORDER 1224 - The storage containers render at ~2x town scale, and five structures ship placeholder art

**Status:** FIXED 2026-08-27 - gated `COMPILE_GATE_OK` + `REGRESSION_OK 303/303 suites` (Builds/w3-c, Builds/w3-r). AWAITING OWNER FELT-VERIFY to close.
**Silo:** Catalog data / art
**Origin:** Owner felt-test in Build mode, Seeker build `2026.08.26.341419`, 2026-08-26.
Owner verbatim: *"and crystal mine is a wrong image"* -> *"its the well"* -> *"the three are the
storage containers which are still twice as large"*.

---

## Slice A - the containers are ~2x town scale

**PROOF.** Device capture `tmp/screen-111126.png`: the green **Lumberyard** placement ghost is an
open timber rack roughly twice the footprint of the neighbouring house and hut.

Read from the catalog this session:

| id | heightMul | prefab |
|---|---|---|
| `tower_ground_archer` | 1.2 | `Structures/Tower_Wooden_Watchtower` |
| `collector_farm` | 1.4 | `Structures/farm` |
| **`lumberyard`** | **(absent)** | `Structures/GenericContainer` |
| **`foundry`** | **(absent)** | `Structures/GenericContainer` |
| **`silo`** | **(absent)** | `Structures/GenericContainer` |

⚠ **AN ABSENT `heightMul` IS NOT "NO NORMALISATION" — IT IS 1.0.** Read at source,
`StructureFactory.EffectiveVisualHeight:73-74`:
```csharp
float mult = entry?.repo != null ? entry.repo.heightMul : 1f;
if (mult <= 0f) mult = 1f;   // guard a zero/unset/negative authored multiplier -> uniform base
```
So the containers ARE fit to the 4 m base, exactly like every house. **Do not "fix" this by adding
a normalisation call — one already runs.**

**The real mechanism** is the caveat `RepoProps.heightMul` states in its own doc:
> *"a spindly silhouette reads SMALLER than a boxy one at the same number… **equal heightMul does
> NOT mean equal apparent size**."*
> *"HEIGHT AND FOOTPRINT ARE ONE NUMBER, by design. The fit is a UNIFORM scale, so this multiplier
> moves the base footprint by the same factor."*

The fit is to **height**, applied **uniformly**. `GenericContainer` is a wide open lattice; forcing
it to stand 4 m tall scales its width by the same factor, so its footprint balloons. This is
`collector_farm`'s problem in reverse — the farm's windmill blades inflate its Y bounds so it needed
**1.4** to put its body back on the line; a wide rack needs a multiplier **below 1.0** for the same
reason. ⛔ **`collector_farm`'s 1.4 is a compensation, not an outlier — never "normalise" it to 1.0.**

The authored `placement.footprint: 2.38` does not save them: the doc states the real estate is
measured off the **height-fitted model** (`MeasureUprightFootprintMetres`), and the authored number
is only the prefab-missing fallback.

### The number is the OWNER'S, by precedent

`collector_farm`'s 1.4 is recorded as *"the owner felt-report compensation"* — dialled by eye against
the town, because bounds maths cannot say what reads right. Same here.

**Owner said "twice as large", so start at `heightMul: 0.5` on all three** and let her judge it on
the next device build. One row each, one number.

⭐ **SAFE DIRECTION, and this matters:** the same doc warns that **RAISING** a multiplier grows the
grid claim (`ceil(measured / 3 m)`) and can make an existing saved town reload with **OVERLAPPING
claims** — but **lowering only shrinks a claim, which is overlap-safe.** We are lowering. State the
before/after cell claim in the RESULT anyway; that is the standing rule for any change here.

⚠ All three share ONE prefab, so all three move together unless authored separately. If the owner
wants them to differ in size later, that needs three prefabs, not three multipliers.

## Slice B - five structures ship placeholder art ⛔ BLOCKED ON THE OWNER

Audited across all 28 catalog entries this session — **24 distinct prefab paths for 28 entries**:

| prefab | used by |
|---|---|
| `Structures/GenericContainer` | `lumberyard`, `foundry`, `silo` |
| `Structures/Well` | `mine_crystal`, `healing_caravan` |
| *(none authored)* | `repair_default` |

So the **Crystal Mine literally renders the Well prefab** — that is the owner's report, and it is one
line of data:
```json
"mine_crystal": { "displayName": "Crystal Mine",
                  "visualPrefabPath": "Structures/Well",
                  "upgradeVisualPath": null }
```
`mine_crystal` also has **no `upgradeVisualPath`**, so all three of its levels are visually identical
even once the art is right. `healing_caravan` shares the same prefab.

⛔ **DO NOT INVENT PREFAB NAMES.** `Assets/Resources/Structures/` shows **0 prefabs** on the CLI
machine — it is gitignored, only four models are tracked (`ArcaneSpire_1/2/3`, `WizardTower_1`), and
the rest arrive by manual LAN copy from `Kayden-Laptop`. The art inventory cannot be seen from here,
and **the art pick is the owner's** in both senses.

**Blocked until the owner supplies:** a prefab path for the crystal mine, one for the healing
caravan, and whether the three storage buildings should keep one shared look or get their own. If no
art exists, this slice belongs in the art queue, not the code queue.

## Acceptance

1. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` on fresh logs, counts off the marker.
2. Catalog **version bumped**, BOTH copies written. ⚠ Verify by PARSE + row equality, not by byte
   equality — `Resources` is compact and `StreamingAssets` is pretty-printed, so these two are
   legitimately different sizes. (The CLI got this wrong once on `armor.json` this same session.)
3. ⭐ **A DEVICE SCREENSHOT in Build mode** showing a Lumberyard ghost beside a house, opened and
   looked at. Headless gates cannot see scale — `bb6dc010` laid a whole town on its side with every
   marker green.
4. The RESULT states the before/after grid cell claim for each of the three containers.
5. Owner felt-verifies the size and CLOSES. She rules the number; the CLI does not.

## What NOT to touch

- ⛔ `collector_farm`'s 1.4 (a bounds compensation, not an outlier).
- ⛔ `StructureFactory.YHeightVariable = 4f` — the ONE global base. Changing it re-scales the whole
  town.
- ⛔ Wall rows. Walls are deliberately unauthored for save compat; a narrower wall segment opens
  **pathable gaps** in already-placed runs.
- ⛔ `repo.visualHeight` — dead for runtime placement since WO-764, authored zero times.
- ⛔ Any prefab path, pending Slice B's owner input.

---

## OWNER RULING 2026-08-26 - Slice B stays art-blocked; it does NOT hold Slice A hostage

Owner verbatim: *"Keep it art-blocked, but do not block Slice A or functional work behind it. Art
dependency should not drag finished implementation back into limbo."*

**Slice A (`heightMul: 0.5` on the three storage containers) is LANDED and STAYS.** It is the fix for
the owner's own felt-test report that containers rendered at twice the size of neighbouring houses.

STOP: do NOT revert Slice A because Slice B lacks art, and do NOT hold this ticket's functional work
behind the art dependency.

WARNING: Slice A moved the structure family median 4.32 m -> 3.78 m, which moved the cadence oracle's
2x band to 7.56 m and made `barracks` (unchanged at 7.64 m) read as an outlier. Tracked in
**WO-1239** - that is a CONSEQUENCE of this ruling, not a defect in it.