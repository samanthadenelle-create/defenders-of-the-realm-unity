# WORK ORDER 909 — Activate Mage + Ranger in character selection (re-enable + verify)

> ## ⚠ LANDED 2026-08-05 (`9a0ff548` + `d0c7b8fd`) — AND THIS WO'S STATED PREMISE WAS **REFUTED**
> - **`Ranger.fbx.tripo-extracted` is NOT a parked mesh.** It is a **125-byte PLAIN TEXT SENTINEL** written
>   by `TripoAssetPostprocessor`. **There is nothing to un-park.** Knight's sentinel sits beside a live
>   `Knight.fbx`, which proves the marker never blocked an import. **Nobody should spend another cycle on
>   it.** The source comments repeating the premise were fixed in `d0c7b8fd`.
> - **What the body work actually was: a latent P0.** Ranger and Mage have **no FBX at all**; both fell
>   through to a **Blink base body**, and **`Assets/Blink` is GITIGNORED**. On a fresh clone the terminal
>   fallback logged a failure and **returned without instantiating anything**, after `Start` had already
>   destroyed the placeholder — **an INVISIBLE HERO, not a Knight-degrade.** Both bail-outs now build a
>   tracked **KayKit** body.
> - **Identity shipped too:** the nameplate had printed the CLASS word and the inventory medallion
>   hardcoded Grom's face, so **all four heroes wore the Knight's portrait**.
>   ⚠ **STILL OPEN — Grom and Elara** carry Thrain's exact portrait-import defect (imported as a plain
>   texture, so `Load<Sprite>` returns null and they fall to the blurrier RawImage path). **Grom is the
>   default hero.** Flagged, not fixed.
> - ⚠ **§4's "stale PlayerPref" gotcha is NOT what was observed.** The mechanism is real
>   (`FeatureFlags.cs:689-695` — a stored `ff.knightonly=1` wins over the new default), but the proven
>   reason a returning save never shows Ranger or Mage is that **the hero-select screen SELF-SKIPS**:
>   `HeroSelectController.OnEnable` (`:123-131`) calls `SceneRouter.GoCastle()` when
>   `IsIntroComplete()` (`:156-161`, `svc.State.HeroClass != HeroClassOpt.None`), and
>   `_skipWhenIntroComplete` defaults **true** (`:85-89`). `TitleController.cs:411-431` routes **Continue**
>   straight to the castle with no hero-select at all, and `:385-395` clears the persisted class on
>   **Play Intro** precisely because of this.
>   **>> TESTING RANGER OR MAGE REQUIRES NEW GAME / PLAY INTRO. Continue will never show the carousel. <<**
>   The pref migration §4 asks for was **never implemented**, so verify on a cleared-prefs profile before
>   ruling that gotcha closed.
> - ⚠ **The follow-on finding is WO-910 (READY FOR OWNER RULING): both unlocked classes have effectively
>   no talent tree** — Ranger 1 usable node of 20, Mage 5 of 20, both tier-4 capstone rows dead, 31 dead
>   nodes total.
> - Full ledger: `docs/reference/SESSION_INDEX_2026-08-06.md` §6.3, §6.4, §8.

**Status:** LANDED (see the banner above) — original header read: READY TO IMPLEMENT
**Silo:** Hero / character-select / onboarding
**PO:** Samantha (owner)
**For:** CLAUDE CLI (sole committer, headless-verifier)
**Authored:** 2026-08-05
**Parent spec:** `WorkOrders/WORK_ORDER_861_playable_characters_program.md` (this WO is the **verification + finish pass** on 861, not a fresh build)

---

## 0. One-paragraph mission

Make **Mage (Thrain)** and **Ranger (Sylas)** genuinely selectable **and playable** from
the hero-select screen, ending on the owner felt-confirming that each spawns with a real
class body + its own Q/W/E/R kit. The gate is already open in code
(`FeatureFlags.KnightOnly` default-OFF, committed `9a0ff548`) and WO-861 landed the kits,
loadout keys, portraits, copy and rename — so this is a **re-enable + verify** order whose
job is to (a) prove it works on a clean profile, and (b) close the **one real open risk:
the Mage/Ranger body mesh resolution** (the legacy `Resources/Heroes/{Mage,Ranger}.fbx` are
parked `.tripo-extracted`, so the class must render from the Blink base body or the live
KayKit body — never a bodyless placeholder).

**Owner design steer (2026-08-05):** *"Mage should obviously live heavily in that realm."*
Mage is the **magic showcase** — its spell-heavy kit (fireball / arcane / frost-nova /
meteor) is the primary place the new common VFX facade's **elemental casting** should read
rich. Treat Mage's cast/impact VFX quality as an acceptance concern, not just "it fires."

---

## 1. Current state (verified from source — do NOT re-derive)

| Fact | Source |
|------|--------|
| Gate `FeatureFlags.KnightOnly` default **OFF** | `Assets/_Modules/Core/FeatureFlags.cs:67` (committed `9a0ff548`) |
| Playable roster = `{ Knight, Ranger, Mage }` (Cleric excluded — no authored kit) | `Assets/_Modules/Core/State/PlayableHeroes.cs:52-57` |
| Hero enum `{ Mage, Knight, Ranger, Cleric }` | `Assets/_Modules/Core/State/Enums.cs:47-53` |
| Select screen locks non-playable via `PlayableHeroes.IsPlayable` | `Assets/_Modules/Onboarding/HeroSelectController.cs:737` |
| Confirm coerces only when `!IsPlayable` (no more force-Knight) | `Assets/_Modules/Core/State/GameStateService.cs:757-771` |
| Vendor shelf follows roster | `Assets/_Modules/Village/.../VendorStockResolver.cs:123-124` |
| Mage + Ranger ability kits authored (Q/W/E/R + skill pools) | `Assets/Resources/Data/Canonical/abilities.json` (mage.*, ranger.*) |
| Per-class HUD loadout key present | `Assets/_Modules/.../HeroLoadout.cs:39-60` (`dotr-loadout-<class>-v1`) |
| Animator controllers present | `Assets/Resources/Heroes/{Mage,Ranger,Knight}.controller` |
| Portraits + card copy present | `HeroPortraits/{Thrain,Sylas}.jpg`; `en.json` `hero.{mage,ranger}.*` |
| Garran→Grom rename done + guarded | `hero-talents.json`; `GlossaryRegression.cs:76` |

**Net:** the plumbing is ~95% landed. This WO is a re-enable + verify + body-mesh finish.

---

## 2. The ONE real open risk — body mesh (make this the headline test)

- `HeroBodySwapper.SlugFor` maps Mage→`"Mage"`, Ranger→`"Ranger"` and assumes live
  `Resources/Heroes/Mage.fbx` / `Ranger.fbx` — but **both are parked `.tripo-extracted`
  on disk (not loadable FBX).** (`Assets/_Modules/.../HeroBodySwapper.cs:1537-1551`.)
- Non-Knight heroes normally build from the shared **Blink base body** (Addressables
  `hero/base/HumanMale`, `HeroBodySwapper.cs:46`), falling back to
  `BuildLegacyResourcesBody` → `HeroAssetLoader.LoadHeroPrefab(slug)` only if the
  addressable fails. Since the parked FBX can't load, **if the Blink addressable is absent
  on the target build, Mage/Ranger would degrade to a bodyless/placeholder hero.**
- Live KayKit bodies DO exist at `Assets/Resources/NPCs/KayKit/{Mage,Ranger}.fbx` (+
  textures). WO-861 Phase 1/2 explicitly offered "point `HeroBodySwapper` at the KayKit
  body" as the fix.

**Decision for CLI (pick the one that renders a real body on the actual build target):**
1. Confirm the Blink base body loads for non-Knight on the target build → done; OR
2. Point the legacy fallback at the live KayKit body (`NPCs/KayKit/{slug}.fbx`); OR
3. Un-park the `.tripo-extracted` FBX into a real `Resources/Heroes/{slug}.fbx`.

Prefer (1) if it already works; else (2) as the lowest-risk committed fix.

---

## 3. Acceptance criteria (felt + engineering)

**Felt (owner closes):**
- [ ] Hero-select shows Mage + Ranger as **selectable** (no "Coming soon" / LOCKED scrim); "Enter Elarion" confirm enabled for each.
- [ ] Select Mage → spawn in Castle → **a real Mage body renders** (not bodyless/Wizard placeholder), staff/robe reads as a mage.
- [ ] Select Ranger → spawn → **real Ranger body** with **bow auto-attached** (`HeroBodySwapper` `cls==Ranger` → `HeroBowAttachment.AttachTo`).
- [ ] Each hero's **own Q/W/E/R** populates the combat HUD (not the Knight bar) and casts.
- [ ] **Mage magic showcase:** fireball / arcane / frost-nova / meteor cast + impact VFX read **rich and elemental** (this is where `Mage lives heavily in the realm` — the common VFX facade's elemental casting should shine here), not the flat procedural fallback.
- [ ] Knight unchanged.

**Engineering (CLI headless-verifies):**
- [ ] Clean profile: clear the stale `ff.knightonly` PlayerPref (see §4) → roster = `{ Knight, Ranger, Mage }`.
- [ ] `HeroBodySwapper` resolves a real body for Mage + Ranger on the **build target** (not just editor).
- [ ] New effects `shield` / `manaweave` / `drainshot` resolve in `HeroAbilities` (WO-861 A-series).
- [ ] Non-Knight selection **persists** through save/load (no coercion to Grom) — `GameStateService.cs:757-771`.
- [ ] `COMPILE_GATE_OK` + `REGRESSION_OK` green; `GlossaryRegression` (no "Garran") still passes.

---

## 4. Known gotcha — stale PlayerPref

`FeatureFlags.Get("knightonly", defaultOn:false)` reads `PlayerPrefs "ff.knightonly"` FIRST.
A device/save carrying a stale `ff.knightonly=1` (from the V1 solo-Knight era) would still
show only Knight despite the new default.
- Verify on a **cleared-prefs / fresh profile.**
- **Decide + implement:** a one-time migration that clears/normalizes the stale
  `ff.knightonly` pref on load, so existing installs get the unlock without a manual reset.
  (Low-risk, isolated; recommended to include so shipped players aren't stranded on Knight.)

---

## 5. Files in scope

| File | Why |
|------|-----|
| `Assets/_Modules/Core/FeatureFlags.cs` | Gate (already OFF) + optional stale-pref migration (§4) |
| `Assets/_Modules/.../HeroBodySwapper.cs` | The body-mesh finish (§2) — the real work of this WO |
| `Assets/_Modules/Core/State/PlayableHeroes.cs` | Roster source of truth — verify, don't change |
| `Assets/_Modules/Onboarding/HeroSelectController.cs` | Select-screen lock logic — verify only |
| `Assets/_Modules/Core/State/GameStateService.cs` | Persistence / no-coercion — verify only |
| `Assets/Resources/Data/Canonical/abilities.json` | Kits — verify Mage/Ranger fire, don't rewrite |

## 6. Do NOT
- Re-author the kits, portraits, copy or rename — WO-861 landed them; this is verify + body finish.
- Enable Cleric (no authored kit — deliberately excluded from the roster).
- Change the Knight path.
- Hand-edit `.unity`/`.prefab` YAML (§0/§3) — body-mesh changes go through code or an editor script.

## 7. RESULT
Write `WorkOrders/WORK_ORDER_909_activate_mage_ranger_character_select.RESULT.md` on completion:
the body-mesh decision taken (Blink base / KayKit / un-park), the stale-pref migration
outcome, headless verify markers, and the felt-test screenshots for Mage + Ranger spawns.
