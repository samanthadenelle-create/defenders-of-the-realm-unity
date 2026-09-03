# WORK ORDER 1294 — Blink Skill Identity, Three-Slot Hot-Swap, and Troop Portraits

**Status:** FIXED (data + oracle landed 2026-09-02; NOT closed - needs the owner felt test and the
section-9 screenshots, which headless cannot capture)  
**Owner ruling:** 2026-09-01  
**Program:** Complete medieval UI reskin  
**Priority:** High — player-facing identity/readability

## 1. Outcome

Give every player-facing combat skill one stable visual identity from the Blink icon library and use
that same identity in the talent tree, assignment UI, three-slot hot-swap rail, and combat HUD. Import
the owner's separate nine-icon troop portrait set for Barracks/Manage troop surfaces. Do not use spell
icons as troop portraits and do not use generic sword/combat fallbacks where authored art exists.

## 2. Approved art sources

### Skills, talents, and hot-swap

- Source: `Assets/Blink/Art/Icons`.
- The existing `BlinkIconImporter`, `RpgUiCatalog` roles, and data-driven `concept-icons.json` mapping
  are the required pipeline. Extend them; do not create a competing loader.
- Blink class families are semantic pools, not automatic class assignments. Choose by readable
  silhouette and actual ability meaning:
  - Guardian/Paladin: guard, block, shield, taunt, defensive talents.
  - Ranger/Hunter: bow attacks, volleys, traps, pursuit and repositioning.
  - Priest/Paladin: healing, recovery, support and cleansing.
  - Arcanist/Pyromancer/Electromancer: bolts, fireballs, wards and caster talents.
  - Warrior/Dragonknight: commits, cleaves, spear attacks and melee capstones.
- Never distinguish state or meaning by hue alone. The owner is red/green colourblind; silhouette,
  border, iconography, label and state mark must carry meaning.

### Troops

- Owner archive: `C:\Users\Elden\Downloads\Elarion_Troop_Icons.zip`.
- It contains exactly these canonical filenames/IDs:
  `troop-footman`, `troop-archer`, `troop-spearman`, `troop-field-cleric`,
  `troop-shieldguard`, `troop-outrider`, `troop-catapult`, `troop-battlemage`, and
  `troop-echo-legionnaire`.
- The supplied 3x3 mobile preview is the approved portrait family: character/weapon silhouette inside
  a matching red-and-antique-gold circular medallion.
- Import as UI Sprites with transparency, no nine-slice, mobile-appropriate max size and compression.
- Route by canonical troop ID through the existing runtime catalog. Preserve role-glyph/letter fallback
  only for a genuinely missing asset.

## 3. Locked skill-tree presentation

- Landscape-mobile progression reads **left to right**: basic/default nodes on the left, advanced and
  capstone nodes toward the right. Tracks may separate vertically.
- The entire visible frontier must fit without opening at a scroll offset or clipping upper ranks.
- Nodes use Blink ability art inside the shared medieval circular medallion/node frame.
- Keep node title and rank/lock requirement readable. Art never replaces the text requirement.
- Connectors remain behind nodes and must terminate at node edges, not cross labels or icons.
- Locked, available, purchased and assigned states remain visible and distinct by frame/overlay plus
  text or symbol. Never use colour alone.
- The bottom assignment rail contains exactly **three** hot-swap slots. Remove the obsolete fourth
  slot from layout, persistence normalization, tests, fixtures and evidence.
- The default/basic skill row must be immediately visible when the screen opens.

## 4. One-icon identity contract

For every assignable skill, the authoritative concept/ability ID resolves one icon and that icon is
used consistently in:

1. talent-tree node;
2. selected-node/detail presentation;
3. assignment picker;
4. hot-swap slot;
5. peaceful/pre-combat loadout summary where present;
6. combat HUD button;
7. cooldown/disabled overlay state.

Do not copy sprite choices into individual views. Views ask the shared resolver/catalog by the
authoritative ID. A mapping change must update all consumers without prefab edits.

## 5. Hot-swap behavior

- Three distinct slots; assignment is deterministic and persists per hero/class under the existing
  loadout ownership model.
- Assigning a skill updates the rail immediately and does not silently duplicate it into two slots
  unless the authoritative rules explicitly permit duplicates.
- Selecting an occupied slot communicates `SELECTED`/equipped state through the shared pressed or
  selected treatment; never render a blank button face.
- Locked/passive/unassignable nodes cannot enter a slot and state the reason.
- During combat, the three buttons preserve stable anchors and display cooldown, unavailable, pressed
  and ready states as overlays on the same icon.
- Skill activation continues to use authoritative ability data, costs and cooldowns. This work order
  changes presentation/mapping, not combat balance.

## 6. Architecture and files to inspect

- `Assets/Editor/BlinkIconImporter.cs`
- `Assets/_Modules/Core/UI/RpgUiCatalog.cs`
- `Assets/Resources/Data/Canonical/concept-icons.json` and its StreamingAssets mirror
- `Assets/_Modules/Village/Talents/HeroSkillTreePanelMvvm.cs`
- `Assets/_Modules/Village/Talents/HeroSkillTreeVM.cs`
- `Assets/_Modules/Village/Hero/AssignableSkillBar.cs`
- HUD model/view consumers resolving skill icons
- `Assets/_Modules/Village/Hero/TroopTrainingPanel.cs`
- both canonical `troops.json` copies

Preserve MVVM boundaries: data/VM exposes IDs and state; views own layout and overlays; the catalog
owns sprite resolution. Do not hard-code gameplay counts, cooldowns, unlocks or costs into views.

## 7. Migration sequence

1. Inventory all assignable active skills and current resolved icon keys.
2. Produce a mobile-size contact sheet grouped by hero/class and reject ambiguous duplicates.
3. Complete the canonical concept-to-Blink map; keep both canonical data copies byte-identical.
4. Import the nine troop portraits and map each canonical troop ID.
5. Finish the horizontal tree composition and three-slot rail.
6. Verify the same skill icon propagates through tree, assignment and live combat HUD.
7. Run compile, data/icon integrity, layout, interaction and persistence regressions.
8. Capture all required screen states at supported landscape ratios and iterate against the approved
   medieval references until the SME review passes.

## 8. Acceptance criteria

- All nine troop types show their supplied unique portrait in Barracks and Manage troop surfaces.
- No reachable trainable troop with supplied art shows the generic sword, crossed-weapons or letter
  fallback.
- Every assignable active skill has one stable Blink icon across tree, assignment and combat HUD.
- No two simultaneously visible skills use the same icon unless they are the same authoritative skill.
- The tree reads left-to-right, opens with the default row visible, and has no clipped nodes or labels.
- Exactly three hot-swap slots render, persist and activate; no fourth slot survives in runtime UI,
  fixtures or regression expectations.
- Locked, available, purchased, selected, assigned, cooldown and unavailable states remain legible in
  colour and greyscale.
- Touch targets meet the project standard (minimum 48 logical pixels), overlays block click-through,
  and rapid taps cannot duplicate assignments or casts.
- No sprite is loaded from `AssetDatabase` or arbitrary disk IO at runtime; Windows, Android and WebGL
  use the committed runtime catalog.
- Fresh asserted compile and relevant regressions pass, followed by screenshots at all supported
  landscape ratios and a live Windows/mobile felt test.

## 9. Required evidence

- Before/after Blink contact sheet with ability ID and selected sprite key.
- Three supported-ratio screenshots of the complete tree and three-slot rail.
- Tree → assignment → combat capture proving one-icon identity for at least one skill per hero class.
- Locked, selected, assigned, cooldown and unavailable state captures.
- Barracks and Manage screenshots showing all nine troop portraits, including locked tiers.
- Exact files added/modified and fresh compile/regression logs.

## 10. Supersession

This work order supersedes only stale **presentation/count** statements in older WOs that say the
assignable hot-swap rail has four slots (including WO-614-era wording and the current UI-reskin ledger).
It does not retire their authoritative ability, unlock, animation, cooldown, persistence or combat
contracts. The Complete UI Reskin requirement of three distinct skill slots plus Item remains binding.
