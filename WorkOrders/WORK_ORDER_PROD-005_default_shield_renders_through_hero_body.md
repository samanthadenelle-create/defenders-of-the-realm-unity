# PROD-005 — The default shield renders THROUGH the hero's body, and the break survives a dungeon→town port

**Status:** READY TO IMPLEMENT
**Minted:** 2026-08-18 (docs seat) — PROD series, post-launch defect, jumps the dev-era backlog.
**Priority:** HIGH — it is the STARTER shield on the STARTER class, on the LIVE dApp Store build. Every
new knight sees it in the first minutes of play.
**Silo:** Gear / equip seating. **Lane:** gear catalog + Addressables. Does NOT touch scenes.
**Provenance:** owner, on the LIVE build. F8 words carried from the ancestor ticket:
*"the shield is now mid body"* (seq 2325) and *"broken shield carried back on exit"* (seq 2326).
**Ancestor:** **WO-994** — `WorkOrders/WORK_ORDER_994_shield_seat_stranded_against_wo970_align.md`.
⛔ **DO NOT rewrite, close or gut WO-994.** It holds four days of captured, trace-proven evidence and is
the diagnostic ancestor of this ticket. It is bannered as such and its body stays intact.

---

## 1. The symptom, in the player's words

The shield does not hang off the arm — it sits **inside the torso**. And the break is not confined to
one scene: leaving a dungeon and returning to town **carries the broken pose back with it**.

Owner pin recorded on WO-994: the seat is *good* in town during steady play and *good* inside the
dungeon; it is the **dungeon → town port** that breaks it, and the broken pose then persists.

## 2. Why (the RCA lives on WO-994 — this is the one-paragraph summary)

WO-970 (`af5e2e7d8`, 2026-08-10) fixed `AlignAxesYLongXNarrowZWide` so a weapon's long axis finally
reaches +Y. Captured proof, same mesh, same authored delta, before and after:

```
PRE-FIX  (WO-970 SS2)   NormalizeInto 'EquipmentProp_OffHand': aligned b1=(0.01, 0.002, 0.008)   X-long
POST-FIX (2026-08-14)   NormalizeInto 'EquipmentProp_OffHand': aligned b1=(0.002, 0.01, 0.008)   Y-long
```

The inner prop rotation moved ~90 degrees underneath a hand-dialled constant that never moved with it.
`shield_A` carries `rot=(-160,-180,-84)` in `Assets/Resources/OffsetForge/offsets.json`, **dialled
2026-07-07** — authored on top of the OLD align and never re-dialled. It is a **stranded constant**.

## 3. The approach in flight — REPLACE THE ASSET, do not re-dial the stranded offsets

**Verified at source this session, in the working tree (uncommitted):**

| Fact | Where | State |
|---|---|---|
| `knight_shield_starter` ("Squire's Heater") now carries `prefabPath: "gear/weapon/ShieldWithItemLogic"` + `loadVia: "addressable"` | `Assets/Resources/Data/Canonical/weapons.json:75-76` **and** `Assets/StreamingAssets/Data/Canonical/weapons.json:73-74` | **both dual copies carry the row** (+2 lines each) |
| A new editor tool published the address | `Assets/Editor/Catalog/SupercyanGearAddressableMarker.cs` | **untracked — needs to be committed** |
| The tool has been RUN and verified | `Assets/AddressableAssetsData/AssetGroups/Gear.asset:1914` → `m_Address: gear/weapon/ShieldWithItemLogic` | **present**; run reported *"Marked 1 prefab(s), 0 missing"* |
| The new prefab has NO authored offset row | `Assets/Resources/OffsetForge/offsets.json` | **zero rows match `ShieldWithItemLogic`** |

### Why replacing the asset is the RIGHT DOOR, not a dodge

The owner already ruled: **do not re-dial `shield_A`** — the town/dungeon steady pose feels right, and
re-dialling a global constant to fix one seam ruins the pose that works. Fix the seam instead.

A **NEW** prefab has **no `offsets.json` row at all** (verified above), so it seats from **DERIVED
geometry** per `docs/ARCHITECTURE_PRINCIPLES.md` §4 rather than from a 2026-07-07 constant that the
2026-08-10 align stranded. That removes the class of bug; it does not paper over one instance of it.

`EquipmentController.LoadsViaAddressable` already takes the addressable branch for any `prefabPath`
beginning `gear/` and seats NATIVE, and the address scheme is deliberately identical to the Blink
weapons scheme so **the catalog `prefabPath` IS the address**.

> ⚠ **The last address segment is the offset key.** `AttachmentOffsetRegistry` is keyed on it
> (`EquipmentController.VisualFromCatalog` derives `vis.mesh` from the last segment; `AttachOffHandProp`
> looks the offset up by that mesh name). **Renaming the address orphans any Offset Forge row the owner
> later dials for it.** Do not "tidy" the address into a prettier slug.

## 4. ⚠ THE OPEN RISK, STATED HONESTLY — derivation is NOT self-proving

Per `CANON_GROUND_TRUTH_2026-08-16.md` §4: derivation **did not save the bow**. The bow's held rotation
was **90 degrees wrong at the ATTACH SEAT** while the grip **POSITION measured correct**. A
measurement-based gate said "correct" about a weapon the player could see was wrong.

> ### HEADLESS GATES CANNOT SEE ORIENTATION.
> `COMPILE_GATE_OK` + `REGRESSION_OK` are necessary and **not sufficient** here. A green marker is not
> evidence that the shield hangs on the arm.

**Therefore acceptance REQUIRES a SCREENSHOT taken AFTER a dungeon → town port.** Not a marker. Not a
measured bounds line.

## 5. Acceptance criteria

1. A fresh knight equips the starter shield and it hangs on the **off-hand arm** — not intersecting the
   torso — in **town**.
2. Same, **inside a dungeon**.
3. **Enter a dungeon, then exit to town: the seat is unchanged.** This is the ancestor's whole bug.
4. **A screenshot of 3** is attached to the RESULT. A verbal "looks right" does not close this.
5. `gear/weapon/ShieldWithItemLogic` resolves on a **device build**, not only in the editor — it is an
   Addressables address on a live product, and an unresolved address is an invisible shield.
6. `offsets.json` still has **no** row for `ShieldWithItemLogic` at close, unless the owner deliberately
   dialled one. A row appearing by itself means something re-authored a constant.
7. `COMPILE_GATE_OK` + `REGRESSION_OK <n>/<n> suites` — necessary, not sufficient (see §4).
8. Owner felt-verify closes it (§13: PO closes, not CLI).

## 6. Files to edit

- `Assets/Resources/Data/Canonical/weapons.json` + `Assets/StreamingAssets/Data/Canonical/weapons.json`
  — the `knight_shield_starter` row (**already changed in the working tree; needs committing**).
- `Assets/Editor/Catalog/SupercyanGearAddressableMarker.cs` (+ `.meta`) — **untracked; commit it**, or
  the address cannot be republished on another clone.
- `Assets/AddressableAssetsData/AssetGroups/Gear.asset` — the published address (**already changed**).
- This WO's `**Status:**` line + a `.RESULT.md`, in the SAME commit as the work (RULES 67).

## 7. ⛔ What NOT to touch

- **Do NOT re-dial `shield_A`** in `offsets.json`. The owner ruled it. Its `rot=(-160,-180,-84)` stays
  as-is; the fix is the seam, not the dial.
- **Do NOT touch `AlignAxesYLongXNarrowZWide`.** WO-970 fixed it correctly. The stranded constants are
  downstream of that fix, not evidence against it.
- **Do NOT author a new `offsets.json` row for `ShieldWithItemLogic`** to "help it land". The absence of
  a row is the mechanism of this fix — adding one re-creates the exact bug class being retired.
- **Do NOT rename the Addressable address** (see the key-orphan warning in §3).
- **Do NOT rewrite, close or gut WO-994.** Diagnostic ancestor; body stays intact.
- No scene edits. No `.unity` files.

## 8. Loose end found while verifying — FLAGGED, NOT ACTIONED

`Assets/AddressableAssetsData/AssetGroups/Default Local Group.asset` has an **unrelated stray entry** in
the working tree:

```
+  - m_GUID: a677919e08ac6db43ac4cadb494efbf5
+    m_Address: Assets/Models/KayKit/KayKit Medieval Hexagon Pack 1.0.1/Assets/obj/units/neutral/shield.obj
```

It is **not** the Supercyan prefab, **not** on the `gear/` address scheme (it is a raw asset path), and it
sits in the **Default LOCAL group** — which **force-includes it into the APK**, cutting directly against
this morning's CDN size work (`785d6d91b`, APK 472.0 MB, enemies moved genuinely REMOTE).

**Recommendation: revert that hunk.** Flagged only — this ticket does not action it, and the seat that
owns the Addressables data should be the one to drop it.

## 9. Second loose end — the weapons.json dual copies are OUT OF SYNC (pre-existing)

`Assets/Resources/Data/Canonical/weapons.json` and `Assets/StreamingAssets/Data/Canonical/weapons.json`
**differ beyond this change**: the Resources copy carries `flavor` strings the StreamingAssets copy
lacks, and several buy costs diverge (e.g. the hatchet row reads wood 25 / iron 50 in Resources vs
wood 20 / food 20 / iron 20 in StreamingAssets). This drift **predates** this ticket and is not caused by
it — the shield row landed identically in both. Flagged for its own ticket; do not fold a catalog
reconciliation into a seating fix.
