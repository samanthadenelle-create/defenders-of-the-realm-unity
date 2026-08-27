# PROD-007 — The axis-conversion pass corrected the WRONG FILE; five structures were double-corrected and lie down

**Status:** CLOSED 2026-08-27 — owner felt-tested PASS on APK 2026.08.27.343739 (village review).
>  PRIOR: **Status:** FIXED — AWAITING OWNER FELT-TEST TO CLOSE. Prior status: DONE (catalog rows applied in the working tree) — AWAITING OWNER FELT-VERIFY after re-bake; the missing ORACLE is filed separately as **PROD-008**, and one tower remains OPEN (see §6).
**Minted:** 2026-08-18 (docs seat) — PROD series, post-launch defect.
**Priority:** HIGH — buildings lying on their sides in the LIVE build.
**Silo:** Structure art / orientation. **Lane:** `structures-catalog.json` (dual copies). No scenes, no `.cs`.
**Provenance:** 2026-08-18 investigation of commit `f995c4706`.

---

## 1. What went wrong

Commit **`f995c4706`** — *"Tripo models arrive UPRIGHT — axis conversion baked at import, ten -90
offsets retired"* — set `bakeAxisConversion: 1` on ten structure FBXs and zeroed ten rows in
`Assets/OffsetForge/offsets.json`.

**For structures, those `offsets.json` rows are INERT.** Nothing resolves a structure id through
`AttachmentOffsetRegistry` — that registry is keyed by **hero/enemy attachment mesh ids**. So the
commit baked the correction into the mesh and then retired ten rows that were never applied to a
building in the first place.

The **LIVE** structure orientation channel is `entry.orientation` in `structures-catalog.json`,
applied at:

`Assets/_Modules/Village/Catalog/StructureFactory.cs:151-158`

```csharp
if (entry.orientation != null && entry.orientation.manual)
{
    ...
    visual.transform.localRotation = Quaternion.Euler(entry.orientation.Euler) * visual.transform.localRotation;
```

Those rows still carried `euler: [-90,0,0]`. **Baked mesh correction + legacy -90 both applied = the
building lies down.**

## 2. Fixed tonight (in tree)

Five rows zeroed to `[0,0,0]`, `manual:true` **KEPT** (the `tower_ground_archer` precedent — the flag
marks the row human-verified so no auto-baker re-tips it), each note dated and citing the superseded
commit:

| id | new euler | was |
|---|---|---|
| `forge` | `[0,0,0]` | `[-90,0,0]` |
| `workshop` | `[0,0,0]` | `[-90,0,0]` |
| `jeweler` | `[0,0,0]` | `[-90,0,0]` |
| `barracks` | `[0,0,0]` | `[-90,0,0]` |
| `tower_ballista` | `[0,0,0]` | `[-90,0,0]` |

Catalog `version` **22 → 23**. Dual copies verified byte-identical:

```
6079bb5d210b24e2f644e0412290e4d3  Assets/Resources/Data/Canonical/structures-catalog.json
6079bb5d210b24e2f644e0412290e4d3  Assets/StreamingAssets/Data/Canonical/structures-catalog.json
```

**NOT touched, correct as-is:** `armorer` (`manual:false` — the block never runs for it); Ballista
L2/L3 (the reskin path deliberately does not apply `entry.orientation` —
`StructureFactory.cs:384-390`: *"Tier models rely on their prefab-native orientation"*).

## 3. ⛔ EIGHT OTHER ROWS STILL CARRY A LIVE `[-90,0,0]` AND THEY ARE CORRECT — DO NOT "TIDY" THEM

Verified at source in `structures-catalog.json` this session, all `manual:true`, all with
`bakeAxisConversion: 0` on their FBX metas:

`pet-house` · `market` · `arcane-tower` · `collector_farm` · `collector_lumbermill` ·
`lumberyard` · `foundry` · `silo`

**Their -90s MUST STAY.** A future "let's finish retiring the -90s" pass would lay all eight down —
including **`collector_lumbermill`, the FTUE's first building**. The distinguishing fact is not the
euler value; it is **whether that FBX was axis-baked**. Check the meta, never the pattern.

## 4. Falsifiable prediction, on the record

After a re-bake, the five corrected structures should MEASURE world `bounds.size.y` equal to
`YHeightVariable * heightMul`:

- `forge` / `workshop` / `jeweler` / `barracks` → **4.00 m ±0.05**
- `tower_ballista` → **4.80 m ±0.05** (heightMul 1.2)

If a measured height comes back near the SHORT axis instead, the model is still lying down and the
correction did not take.

## 5. Acceptance criteria

1. Owner screenshot: the five structures stand upright in town. **Headless cannot judge this** — see §6.
2. The measured heights in §4 hold.
3. The eight rows in §3 are untouched and `collector_lumbermill` still stands.

## 6. OPEN — `Tower_Wooden_Watchtower_L3` is double-corrected by a DIFFERENT route

A `.prefab` variant overrides the FBX child to X **-90**, and its catalog row `tower_ground_archer`
sets `preservePrefabRotation: true` (`RepoProps.cs:329`; mirrored at
`Assets/_Modules/Village/Catalog/CatalogBootstrap.cs:203-210` with the comment *"WO-928 defect A"*),
so `VisualFactory` keeps the prefab rotation. **This is WO-928's tower regressing.**

**Deliberately NOT fixed tonight** — it touches the one sanctioned `preservePrefabRotation` opt-in, and
changing that seam blind is how the ArcaneSpire double-rotation trap was set in the first place. It
needs its own change, with its own measurement.

## 7. What NOT to touch

- `Assets/OffsetForge/offsets.json` — inert for structures; leave `f995c4706`'s zeroing alone.
- The `manual:true` flags on the corrected rows — zeroing the euler while clearing `manual` invites an
  auto-baker to re-author it.
- `ReskinForLevel` (`StructureFactory.cs:384-390`) — the tier-model carve-out is deliberate.
