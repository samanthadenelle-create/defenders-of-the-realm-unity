# WORK ORDER 07 — RESULT

**Executed:** 2026-05-24 (evening) under Standing Authority #35 + WO-025
**Outcome:** Ability *logic* is sound and fully wired in the scene. Found + fixed two real defects (HUD never reflected mana/cooldown; placeholder VFX would render magenta in URP). Build clean. Runtime key-press eyes-on is the remaining tick (build-side gate — see §5).
**Editor:** Unity 6000.4.8f1

---

## 1. Q/W/E/R → ability mapping (mage / Blaise — the v2 foundation class)

Abilities are **content**, loaded from `Assets/StreamingAssets/Data/Canonical/abilities.json` via the static `AbilityCatalog` (no inspector assignment to break — the WO's "AbilityCatalog asset not assigned" failure mode does not apply here).

> **Hotkeys are `1 / 2 / 3 / 4`, not literal Q/W/E/R** — `W` is reserved by `HeroLocomotion` (move forward). The HUD labels Q/W/E/R are slot mnemonics (`HeroAbilityInput.cs`). Gamepad: South/East/West/North.

| Slot | Key | Ability | Effect | Cooldown | Mana | Damage/Heal | Range | Colour |
|---|---|---|---|---|---|---|---|---|
| Q | 1 | **Arcane Bolt** | strike (nearest enemy) | 0.5 s | **0** | 16 | 13 | `#b388ff` violet |
| W | 2 | **Frost Nova** | aoe blast + 1.4 s freeze | 12 s | 3 | 26 | 5.2 | `#7dd3fc` cyan |
| E | 3 | **Healing Beacon** | heal the Heart (no enemy dmg) | 16 s | 4 | +48 HP | 5 | `#ffd27a` amber |
| R | 4 | **Meteor Strike** | meteor blast on nearest cluster | 45 s | 7 | 80 | 6 | `#ff7043` orange |

All four slots are bound. **By design:** Q costs 0 mana (spammable primary), so casting Q will *not* move the mana bar — that's correct, not a bug. Max mana 10, regen 0.9/s.

---

## 2. Fire-path trace (per ability) — the logic is correct

`HeroAbilityInput.Update()` reads `digit1..4` → `HeroAbilities.TryCast(slot)`:
1. `AbilityCatalog.Find(heroClass, slot)` → def (from JSON).
2. Gate: `cooldownRemaining[slot] > 0 || mana < manaCost` → bail.
3. `cooldownRemaining[slot] = def.Cooldown; mana -= def.ManaCost`.
4. Animator `Cast` trigger fired (null-guarded).
5. `ResolveEffect`: Heal → `HeartController.SetHp(+heal)`; Strike/Snare → `NearestHostile.TakeDamage` (+Slow on snare); Aoe/Cleave → `Blast` around caster; Meteor → `Blast` on nearest cluster. Targets resolved via `Physics.OverlapSphere` → `IDamageable` on the Enemy layer.
6. `SpawnVfx` → particle burst tinted to the ability colour.

**Scene wiring (Village.unity, hero rig) — all correct:**
- `HeroAbilityInput` ✓, `HeroAbilities` ✓ (`_heroClass: mage`, `_heart` wired ✓, `_enemyMask` = Enemy layer (bit 256) ✓, `_maxMana: 10`), `HeroAbilitiesHudBridge` ✓ (`_hud` wired ✓).
- `_castVfxPrefab: {fileID: 0}` (null) → all abilities use the runtime-built particle burst (see fix §3.2).

So **(a) abilities fire, (c) effects apply, and cooldown/mana are tracked internally** — the ability runtime itself was not broken.

---

## 3. Defects found + fixed

### 3.1 — HUD never reflected mana / cooldown (AC2 bug) — FIXED
`VillageHudController.SetMana(...)` and `SetAbilityCooldown(...)` exist but had **no runtime caller anywhere** in the codebase (verified by full-repo grep — only the method definitions + the integrator-note comments). `HeroAbilitiesHudBridge` only forwarded HUD *clicks* into `TryCast`; it never pushed state *back*. Net effect: casting an ability correctly spent mana and started a cooldown **internally**, but the HUD mana bar stayed frozen at its UXML default (10/10) and the ability cooldown sweeps never animated.

**Fix** (`HeroAbilitiesHudBridge.cs`, additive): the bridge now resolves `SetMana(float,float)` + `SetAbilityCooldown(int,float,float)` by reflection (same asmdef-isolation seam it already uses for `AbilityRequested`) and pushes them every frame in `Update()` — `SetMana(hero.Mana, hero.MaxMana)` and, per slot, `SetAbilityCooldown(slot, hero.CooldownRemaining(slot), def.Cooldown)`. No balance values touched.

### 3.2 — Placeholder VFX would render magenta in URP (AC3 risk) — FIXED
With `_castVfxPrefab` null, `HeroAbilities.BuildBuiltInBurst()` creates a `ParticleSystem` via `AddComponent`, which ships with the **legacy built-in particle material** — URP renders that as a magenta/invalid burst (same missing-shader class as the WO-05 pets).

**Fix** (`HeroAbilities.cs`, additive): `BuildBuiltInBurst` now assigns the renderer a `Universal Render Pipeline/Particles/Unlit` material (fallback `Sprites/Default`), **only when `Shader.Find` resolves a shader that is actually present in the build** — so it can never trade the default for a stripped (magenta) shader. The ability tint still drives the burst colour via `startColor` (both shaders honour vertex colour).

---

## 4. Verification performed

- ✅ Static trace of all four fire paths + scene wiring (§1–§2).
- ✅ Full-repo grep proving the HUD push was absent (root cause of AC2).
- ✅ Headless build after the fixes: `[DesktopBuild] SUCCEEDED — 559 MB`, **0 compile errors**, no warnings/errors in either edited file → both fixes compile and ship (AC4).

---

## 5. Remaining (could not verify autonomously — build-side gate)

The *runtime* confirmations AC1/AC2/AC3 ask for — press 1/2/3/4 and watch the mana bar drop, the cooldown sweep animate, and the VFX burst appear — require either Editor playmode or reaching the Village in the player (Title→HeroSelect→PetSelect→Village, not cleanly automatable headlessly; the batchmode playmode capture was set aside per owner direction). The fixes are validated by static correctness + a clean build.

- **Owner 2-minute confirm (editor):** open `Village.unity`, Play, press `2` (Frost Nova) — mana drops 10→7, the W slot sweep animates over 12 s, a cyan burst plays; press `3` (Healing Beacon) — mana 7→3, Heart HP rises, amber burst; press `1` (Arcane Bolt) repeatedly — fires every 0.5 s, no mana change (correct, it's free). Screenshots → `docs/wo07-abilities-before.png` / `docs/wo07-abilities-after.png`.

---

## 6. Related systemic finding (out of WO-07 scope — flagged)

The missing-HUD-push problem is broader than abilities. Runtime push status of each `VillageHudController` readout:

| Readout | Pushed at runtime? | By |
|---|---|---|
| Wave / countdown | ✅ | `WaveHudBridge` |
| Mana | ✅ **(now — this WO)** | `HeroAbilitiesHudBridge` |
| Ability cooldowns | ✅ **(now — this WO)** | `HeroAbilitiesHudBridge` |
| **Heart HP** | ❌ only the DevPanel pushes it | — |
| **Crystals** | ❌ no caller | — |

So in normal gameplay the **Heart HP bar and crystal counter likely stay static** (chrome renders — WO-06 confirmed the HUD *renders* — but these two readouts get no live data). Recommend a small consolidated `VillageHudPump` (or a `HeartHudBridge` + crystal push) — natural to fold into **WO-10 (MVP smoke test)** or a dedicated HUD-data-binding fix. Not done here to keep WO-07 focused on abilities (additive-only, no scope creep).

---

## 7. Suggested next step (the upcoming "basic attack + VFX" WO)

- The Q primary (Arcane Bolt, free, 0.5 s cd, strike on nearest enemy via `IDamageable`) is effectively the **basic attack** already — the new WO can build its VFX/feel on top of this proven path rather than new combat plumbing.
- **Blocker to clear first:** there must be an enemy with an `IDamageable` on the Enemy layer in range for strike/aoe/meteor to land. Wire a test/training dummy (or confirm Wave-1 spawns reach the hero) so abilities have something to hit during verification.
- Replace the placeholder particle burst with real per-element VFX prefabs (assign `_castVfxPrefab`, or per-ability prefabs) — fire/ice/arcane/heal themed; wrap any imported VFX in `TripoMaterialFixer` per the WO-05 pattern if they import non-URP.
