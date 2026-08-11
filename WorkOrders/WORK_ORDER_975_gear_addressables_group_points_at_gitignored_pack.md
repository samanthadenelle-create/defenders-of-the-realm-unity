# WORK ORDER 975 — The `Gear` Addressables group points at a gitignored art pack

**Status:** READY TO IMPLEMENT
**Lane:** Build path / Addressables / asset pipeline
**Minted:** 2026-08-10 (CLI), from the same architect verification as WO-974, ordered by the owner:
*"make sure that addressables are implemented as supposed to be. Have an architect read and verify."*

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
