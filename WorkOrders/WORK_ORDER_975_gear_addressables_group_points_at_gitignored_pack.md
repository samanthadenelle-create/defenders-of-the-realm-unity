# WORK ORDER 975 — The `Gear` Addressables group points at a gitignored art pack

**Status:** DONE — owner-confirmed fixed and verified 2026-08-19.
**Lane:** Build path / Addressables / asset pipeline
**Minted:** 2026-08-10 (CLI), from the same architect verification as WO-974, ordered by the owner:
*"make sure that addressables are implemented as supposed to be. Have an architect read and verify."*

---

## 0. OWNER RULING 2026-08-15 (closes “track Blink?”)

> **B — do not want Blink as real ship content.** It is **only a placeholder.**
> **Armor is never changed on the 3D character** — it holds **damage/defense stats** and is
> **visualized in menu screens as 2D only** (icons), not as skinned 3D body packs.

### What this means for this ticket

| Was on the table | Ruling |
|------------------|--------|
| **A.** Promote ~426 Blink prefabs into git / tracked Addressables | **NO** — do not commit Blink; do not treat it as the gear pipeline |
| **B.** Keep Blink local-only + loud fail if missing | **Yes for residual placeholder keys**, but the **product goal is to stop needing Blink** for armor at all |
| Armor Addressable 3D body swap | **OUT OF SCOPE / JUNKED** (aligns with `FeatureFlags` Blink-armor pivot: armor is stats + 2D) |

### Correct end state (implementation target)

1. **Armor rows** = id, stats, costs, **2D `iconPath`** (Resources or tracked icons). No player path requires `Assets/Blink/...` armor prefabs. Armor is **never** a 3D body swap.
2. **Weapons are placed items** (owner 2026-08-15, same breath): real **3D props** seated on the hero
   (hand / back sockets via `EquipmentController`). They are **not** 2D-only like armor. Ship path for
   weapon meshes must be **tracked / build-reachable** (Resources props, curated Addressables, or
   mirror) — **not** “import whole Blink pack,” but also not “icons only.”
3. **Gear.asset** should not assert a hollow player when Blink is absent for **armor** — drop armor
   Blink entries over time. For **weapons**, fence only keys the catalog actually uses for 3D attach.
4. Hard fence must not force “import all of Blink.” Prefer: fence **canonical catalog keys that declare
   Addressable/3D load**, not every leftover Gear.asset GUID.
5. Clean-clone / CI: green without `/Assets/Blink/` for armor + menu; weapons resolve from **non-Blink
   or curated tracked** mesh paths.

Remaining work = **decouple armor (2D+stats) from Blink**, **keep weapons as placed 3D** on a shippable
mesh path — not promote the full Blink pack.

---

## 1. The defect

`Assets/AddressableAssetsData/AssetGroups/Gear.asset` is **git-TRACKED** (confirmed via
`git ls-files`) and holds **426 entries**. Sampled GUIDs resolve to:

- `0101e680…` → `Assets/Blink/Art/Weapons/LowPoly/MegaWeaponPack1/_Prefabs_MWP1/Crossbows/Crossbow2h_06.prefab`
- `03ebd051…` → `Assets/Blink/Art/Characters/LowPoly/Humans_LowPoly/ArmorPacks/Prefabs/LandWarrior_HumanMale.prefab`

And `git check-ignore -v` returns:

```
.gitignore:350:/Assets/Blink/
```

**The group is tracked. Its content is not.**

## 2. Why this is worse than the `polyperfect` / `KayKit` case

Those packs are also gitignored, and the project handles it: a missing prefab logs a
`Debug.LogWarning` (CLAUDE.md §4), the world looks black on a fresh clone, and everyone knows why.

This is different. **A tracked group asset ASSERTS that 426 assets exist.** It looks authoritative to
every tool and every reader. On a fresh clone or a CI runner:

1. All 426 entries dangle.
2. The content build emits an empty/degenerate `gear_assets_all_*.bundle`.
3. Every one of these fails its `LoadAssetAsync`:
   - `EquipmentController.cs:744`
   - `HeroArmorVisual.cs:199`
   - `HeroBodySwapper.cs:148`
4. Result in the player: **no weapons, no armour, no hero body.**

No warning names the cause. The build is green.

## 3. The existing net is soft, not a fence

`Assets/Editor/Regression/DataRegression.cs:2642-2657` (`AddressableKeyExists`) probes that gear
addresses resolve — but it **returns `false` on throw with only a `LogWarning`.** That is a signal,
not a gate; the suite does not hard-fail on it. Anyone reading "the regression covers Addressables"
would draw the wrong conclusion, which is exactly the false-green pattern this project keeps paying
for.

## 4. Fix (do both)

**A. Make the content real.** Promote the ~426 referenced prefabs into a tracked location. The
precedent already exists in this repo: `.gitignore:150-176` negates `Resources/Structures` out of a
broader ignore. Follow that pattern rather than inventing a new one. If the pack's licence forbids
committing it, say so on this WO and pivot to (B)-only plus a loud boot-time warning.

**B. Turn the soft signal into a hard fence.** A regression that **FAILS** when
`Gear.asset` entry count ≠ resolvable-GUID count. That single assertion converts a silent,
far-from-cause runtime failure into a gate failure at check-in.

## 5. Acceptance criteria

- [ ] Either the referenced prefabs are tracked, or this WO records the licence reason they cannot be
      and the boot path warns loudly and specifically.
- [ ] A regression hard-fails on any dangling `Gear.asset` entry, and is registered in
      `DataRegression.cs` (committer adds the registration — that file is lane-fenced).
- [ ] The `AddressableKeyExists` soft-warning path is either promoted to a failure or documented
      in-place as deliberately advisory, so no future reader mistakes it for coverage.
- [ ] Proven by a clean-clone (or `Assets/Blink` temporarily renamed) build that now fails at the
      gate instead of shipping a hollow bundle.
- [ ] Brace balance + 0 NUL bytes on every `.cs` touched (§1, §0).

## 6. What NOT to do

- Do **not** "fix" this by removing entries from `Gear.asset` to make the count match. That ships the
  same hollow player and destroys the record of what gear is supposed to exist.
- Do **not** move gear loading back to `Resources/`. The audit confirmed Addressables and `Resources/`
  are currently **disjoint** (Addressables owns gear/armor/skins/hero textures; `Resources/` owns
  canonical JSON + hero art), and that disjointness is worth keeping — it is the reason the
  `Resources/Data/Canonical` dual-copy law is not being violated.

## 7. Related

- **WO-974** — the content build has no seam (rides an uncommitted Editor Preference). That WO is the
  *seam*; this one is the *content*. Both were found in the same audit and either alone can produce a
  green build that ships a hollow player.
- Asset-pipeline context: big art packs are gitignored and transferred by zip (see the project's asset
  pipeline notes); `Assets/Blink/` had simply never been reconciled with the fact that a group
  referencing it is tracked.
