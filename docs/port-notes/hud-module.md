# HUD module — village in-game HUD (ARC-003)

The `DeNelle.HUD` module was an empty asmdef (architecture review item
ARC-003). This note records the first real component: the in-game village
HUD for the Week-4 "playable Wave 1" loop.

## Files

- `Assets/_Modules/HUD/VillageHud.uxml` — the HUD overlay layout.
- `Assets/_Modules/HUD/VillageHud.uss` — styling (matches `BattleHUD.uss` /
  `BuildMenu.uss` palette: dark Heart-Forest, violet Heart accent, sky-blue
  crystals, amber CTA, mint HP).
- `Assets/_Modules/HUD/VillageHudController.cs` — the `MonoBehaviour` driver,
  namespace `DeNelle.HUD`.

The asmdef was **not** changed — it already referenced only `DeNelle.Core`,
`DeNelle.Data`, `Unity.Localization`, `UniTask`. No change was needed.

## What the HUD shows

- **Heart HP bar** (top-left) — Elarion's vitals; tints amber at <=50% and red
  at <=25% of max.
- **Crystal counter** (top-centre) — the village currency.
- **Wave indicator** (top-right) — wave number + a between-wave countdown line
  that turns amber+bold under ~3s.
- **Hero ability bar** (bottom-centre) — four Q/W/E/R cells, each with a hotkey
  badge, a placeholder glyph, a cooldown sweep (vertical wipe standing in for a
  radial sweep until icon art lands) and a seconds-remaining numeral.
- **Mana bar** (bottom-left of the ability bar) — the hero mana pool.
- **Build button** (bottom-right) — opens the build menu.

## Module isolation (port spec Part 2)

The HUD is a **passive display**. It owns no gameplay state and never
references `DeNelle.Village` or any gameplay module — the asmdef stays at
`DeNelle.Core` + UI Toolkit. All data is **pushed in** through the controller's
public setters; the Build button raises a `UnityEvent` rather than calling
`BuildMenu` directly (the HUD cannot see that type).

## Public API surface

`VillageHudController` (MonoBehaviour, `[RequireComponent(typeof(UIDocument))]`):

| Member | Purpose |
| --- | --- |
| `SetHeartHp(float current, float max)` | Heart HP bar fill + label + tint. |
| `SetCrystals(int amount)` | Crystal counter (clamped >= 0). |
| `SetWave(int number, float countdown)` | Wave ordinal + Prepare-Phase timer (pass `countdown <= 0` while a wave is active to clear the line). |
| `SetAbilityCooldown(int slot, float remaining, float total)` | One Q/W/E/R cell (slot 0=Q, 1=W, 2=E, 3=R). |
| `SetMana(float current, float max)` | Hero mana bar. |
| `SetBuildButtonEnabled(bool)` | Greys / re-enables the Build button. |
| `BuildRequested` (`UnityEvent`) | Raised on Build-button click. |
| `IsBound` (`bool`) | True once the UXML is bound + cells built. |
| `AbilitySlotCount` (`const int = 4`) | Slot count. |

The four ability cells are built once at runtime into the `ability-bar`
container; the UXML supplies only the bar shell + corner panels.

## Integrator wiring (the HUD does NOT do this itself)

The village scene builder / `VillageController` owns every connection — the
`DeNelle.HUD` asmdef cannot see `DeNelle.Village`:

1. Add a `UIDocument` to the village HUD GameObject, source `VillageHud.uxml`,
   and add `VillageHudController` beside it.
2. **Build button → build menu:** `hud.BuildRequested.AddListener(buildMenu.Open);`
   (`BuildMenu.Open()` is parameterless — a direct `UnityEvent` listener).
3. Push data from the village sub-systems:
   - `HeartController` → `hud.SetHeartHp(heart.Hp, 100f);`
   - crystal balance → `hud.SetCrystals(GameStateService.Instance.State.Resources.Crystals);`
     (or subscribe to `GameStateService.ResourcesChanged`).
   - `WaveManager` → `hud.SetWave(wave.CurrentWaveId, wave.CountdownRemaining);`
     `WaveManager` also exposes `OnCountdownTick` / `OnWaveStarted` UnityEvents
     to refresh on change.
   - `HeroAbilities` → per slot `hud.SetAbilityCooldown((int)slot,
     hero.CooldownRemaining(slot), <ability cooldown>);` and
     `hud.SetMana(hero.Mana, hero.MaxMana);`. The full cooldown duration comes
     from the ability def (`AbilityCatalog.Find(class, slot).Cooldown`).

The HUD holds no timers — the integrator drives every setter.
