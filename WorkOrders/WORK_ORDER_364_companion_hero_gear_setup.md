<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-364: Companion Gear Setup — Cosmetics, Forge Visit, Auto-Equip

**Status:** DONE — audit-verified as shipped (2026-08-21 backlog audit).
**Estimated Effort:** P1 (1–2 days)  
**Priority:** High (narrative + visual progression)  
**Lane:** Narrative/Quests

---

## Overview

Enhance companion introduction (WO-360) with hero gear customization:

1. **Companion Dialogue** — "Let me outfit you properly. You'll need armor and a weapon to face what's coming."
2. **Forge Visit Sequence** — Companion escorts hero to forge (optional travel, skip if already equipped)
3. **Gear Auto-Equip** — Companion selects armor + weapon, automatically equips on hero
4. **Visual Upgrade** — Hero appears in new armor immediately (cosmetic + stat boost)
5. **Narrative Tie** — "Now you look the part. Let's show them what we're made of."

**Why:** Companion introduction feels more impactful if hero visually upgrades. Stops at forge = world feels alive (NPCs use buildings). Auto-equip removes friction.

---

## Acceptance Criteria

- [ ] Companion dialogue includes gear offer: "Let me outfit you properly"
- [ ] Dialogue branches: "Stop at forge" or "Skip, I'm ready"
- [ ] If skip selected → Check hero already has weapon/armor (skip forge)
- [ ] Forge visit triggers auto-walk sequence (hero + companion to forge)
- [ ] At forge, companion interacts with NPC (dialogue: "This one needs proper gear")
- [ ] Equipment grants: 1 weapon + 1 armor piece (auto-selected by companion)
- [ ] Equipment equips automatically on hero (no manual selection)
- [ ] Hero model updates immediately (shows new armor visually)
- [ ] Companion comments on upgraded appearance: "Much better. You look ready now."
- [ ] After equip, auto-walk back to village center
- [ ] Conversation resumes (Echo intro + tutorial, then to outpost)
- [ ] Equipment persists in inventory (can unequip/swap later)

---

## Files to Create

### New Files
- `Assets/_Modules/Village/NPCs/CompanionGearSetup.cs` — Gear selection + equip logic
- `Assets/_Modules/Village/NPCs/ForgeVisitSequence.cs` — Walk-to-forge animation + NPC dialogue

### Existing Files (Modify)
- `Assets/Yarn/CompanionOutpostIntroduction.yarn` — Add gear dialogue branch
- `Assets/_Modules/Village/NPCs/CompanionOutpostIntro.cs` (WO-360) — Call gear setup if selected
- `Assets/_Modules/Village/Hero/HeroEquipment.cs` — Add public auto-equip method

---

## Design Spec

### Dialogue Flow

```yarn
title: CompanionOutpostIntroduction
---

COMPANION: Greetings! I've heard of your recent victories.

HERO: Who are you?

COMPANION: I am [Companion Name]. The darkness grows, and you'll need aid.
          But first—let me outfit you properly.
          
COMPANION: You'll need armor and a weapon to face what's coming.
          >> [Choice]

→ Option 1: "Let's stop at the forge."
→ Option 2: "I'm already equipped."

---

title: CompanionForgeVisit
---

[Hero + Companion walk to forge]

COMPANION: *calls to forge NPC*
          This one needs proper gear for the battles ahead.

FORGE_NPC: *nods* I have just the thing.
          *hands over armor + weapon*

COMPANION: *turning to hero*
          There. Now you look the part.
          That should serve you well.

[Equipment equips on hero]
[Hero model updates to show new armor]

COMPANION: Much better. You look ready now.
          Come, the outpost awaits.

[Walk back to village center]
[Echo intro continues...]

===
```

### Equipment Selection Logic

**Companion chooses gear based on hero's class/level:**

| Hero Class | Armor | Weapon | Notes |
|-----------|-------|--------|-------|
| Warrior | Iron Plate | Iron Sword | Balanced |
| Ranger | Leather Vest | Bow | Mobility |
| Mage | Cloth Robe | Staff | Light armor |
| Generic (no class) | Leather Armor | Sword | Safe default |

**Wave-aware:** If hero is past Wave 3 already, companion gives slightly better gear.

### Visual Feedback

**Armor Change:**
- Hero model swaps to new armor (mesh + material)
- Particle effect: shimmer/glow (hero gets upgraded)
- Sound cue: equip sound + success chime
- HUD notification: "+Iron Plate Armor" (brief popup)

**Weapon Change:**
- Hero weapon swaps visually
- First-person/third-person hand model updates
- Sound: weapon draw + whoosh

---

## Implementation Notes

### CompanionGearSetup.cs

```csharp
public sealed class CompanionGearSetup : MonoBehaviour
{
    [SerializeField] private List<EquipmentItem> _startingArmor;
    [SerializeField] private List<EquipmentItem> _startingWeapons;

    public void EquipHeroGear(HeroEquipment heroEquip, string heroClass = "Generic")
    {
        // Select gear based on class
        var armor = SelectArmorForClass(heroClass);
        var weapon = SelectWeaponForClass(heroClass);

        // Auto-equip
        heroEquip.EquipArmor(armor);
        heroEquip.EquipWeapon(weapon);

        // Visual feedback
        SpawnEquipEffect(heroEquip.transform.position);
        AudioService.PlayCue(AudioId.EquipArmor);
        ShowEquipNotification(armor, weapon);

        Debug.Log($"[CompanionSetup] Equipped {armor.name} + {weapon.name} on {heroClass}");
    }

    private EquipmentItem SelectArmorForClass(string heroClass)
    {
        return heroClass switch
        {
            "Warrior" => FindArmor("IronPlate"),
            "Ranger" => FindArmor("LeatherVest"),
            "Mage" => FindArmor("ClothRobe"),
            _ => FindArmor("LeatherArmor")  // Default
        };
    }

    private EquipmentItem SelectWeaponForClass(string heroClass)
    {
        return heroClass switch
        {
            "Warrior" => FindWeapon("IronSword"),
            "Ranger" => FindWeapon("Bow"),
            "Mage" => FindWeapon("Staff"),
            _ => FindWeapon("Sword")  // Default
        };
    }

    private void SpawnEquipEffect(Vector3 position)
    {
        var vfx = Instantiate(equipVFXPrefab, position, Quaternion.identity);
        Destroy(vfx, 1f);
    }
}
```

### ForgeVisitSequence.cs

```csharp
public sealed class ForgeVisitSequence : MonoBehaviour
{
    [SerializeField] private Transform _forgeLocation;
    [SerializeField] private float _walkSpeed = 5f;

    public async UniTask VisitForge(HeroLocomotion hero, GameObject companion)
    {
        // 1. Walk to forge
        await WalkToLocation(hero, _forgeLocation.position);
        await WalkToLocation(companion.GetComponent<Animator>(), _forgeLocation.position);

        // 2. Companion dialogue with forge NPC
        DialogueService.Play("CompanionForgeVisit");
        while (DialogueService.IsRunning)
            await UniTask.Delay(100);

        // 3. Auto-equip gear
        var gearSetup = GetComponent<CompanionGearSetup>();
        gearSetup.EquipHeroGear(hero.GetComponent<HeroEquipment>());

        // 4. Walk back to village center
        await WalkToLocation(hero, Vector3.zero);
        await WalkToLocation(companion.GetComponent<Animator>(), Vector3.zero);
    }

    private async UniTask WalkToLocation(Transform character, Vector3 target)
    {
        while (Vector3.Distance(character.position, target) > 0.5f)
        {
            var dir = (target - character.position).normalized;
            character.position += dir * _walkSpeed * Time.deltaTime;
            character.LookAt(target);
            await UniTask.Yield();
        }
    }
}
```

### Dialogue Branch Integration (CompanionOutpostIntro.cs)

```csharp
private async void PlayIntroduction()
{
    // ... existing companion spawn ...

    // Play intro dialogue
    DialogueService.Play("CompanionOutpostIntroduction");

    // Wait for dialogue + check choice
    while (DialogueService.IsRunning)
        await UniTask.Delay(100);

    // Check if player chose "Visit Forge"
    var choice = DialogueService.GetLastChoice();  // "forge" or "skip"
    
    if (choice == "forge")
    {
        var forgeVisit = companion.GetComponent<ForgeVisitSequence>();
        await forgeVisit.VisitForge(_hero, companion);
    }
    else if (!_hero.HasEquipment())
    {
        // Player chose "skip" but has no gear — auto-equip anyway (skip forge)
        var gearSetup = companion.GetComponent<CompanionGearSetup>();
        gearSetup.EquipHeroGear(_hero.GetComponent<HeroEquipment>());
    }

    // Continue to Echo intro
    DialogueService.Play("EchoDeployment");
}
```

### HeroEquipment Integration

```csharp
public class HeroEquipment : MonoBehaviour
{
    public void EquipArmor(EquipmentItem armor)
    {
        _currentArmor = armor;
        _armorModel.mesh = armor.mesh;
        _armorModel.material = armor.material;
        AddInventoryItem(armor);
    }

    public void EquipWeapon(EquipmentItem weapon)
    {
        _currentWeapon = weapon;
        _weaponModel.mesh = weapon.mesh;
        _weaponModel.material = weapon.material;
        _weaponModel.GetComponent<Collider>().enabled = true;
        AddInventoryItem(weapon);
    }

    public bool HasEquipment() => _currentArmor != null && _currentWeapon != null;
}
```

---

## Gear Options

### Starting Armor Set

| Name | Mesh | Defense Bonus | Class Match |
|------|------|---------------|-------------|
| Iron Plate | Heavy armor | +3 DEF | Warrior |
| Leather Vest | Light leather | +1 DEF | Ranger |
| Cloth Robe | Mage robes | +0 DEF, +2 MAG | Mage |
| Leather Armor | Generic leather | +1 DEF | Default |

### Starting Weapon Set

| Name | Damage | Type | Class Match |
|------|--------|------|-------------|
| Iron Sword | +5 DMG | Melee | Warrior |
| Bow | +3 DMG | Ranged | Ranger |
| Staff | +3 DMG | Magic | Mage |
| Sword | +4 DMG | Melee | Default |

**No stat advantage,** just visual differentiation + narrative flavor.

---

## Testing Checklist

- [ ] Companion dialogue includes gear offer ("Let me outfit you")
- [ ] Dialogue branches correctly (forge vs. skip)
- [ ] If skip: Check hero has gear, auto-equip if missing
- [ ] Walk-to-forge sequence works (hero + companion walk together)
- [ ] Forge NPC dialogue plays
- [ ] Equipment equips automatically on hero
- [ ] Hero model updates visually (armor shows)
- [ ] Weapon model updates visually
- [ ] Audio cues play (equip sound)
- [ ] HUD notification shows equipped items
- [ ] Walk back to village works
- [ ] Equipment persists in inventory
- [ ] Echo intro continues after gear sequence
- [ ] Works in WebGL build (dialogue via DialogueService)

---

## What NOT to Touch

- Combat damage (gear is cosmetic + story flavor, not stat-breaking)
- Hero class system (use existing class if defined)
- Forge NPC behavior (just quick dialogue, no crafting)
- Echo auto-deploy (happens after gear, separate WO-360 sequence)

---

## Dependencies

- **Depends on:** WO-360 (companion intro), Yarn Spinner, HeroEquipment
- **Unblocks:** Hero customization UI (later WO for gear swap)
- **Parallel:** None (quick 1–2 days)

---

## Narrative Polish

- Companion's voiceline: "You'll need armor and a weapon to face what's coming."
- Forge NPC: "I have just the thing." (acknowledges companion's request)
- Companion callback: "Much better. You look ready now." (validates upgrade)
- Creates story moment: hero goes from unprepared → geared up → ready for outpost

---

## Future Enhancements

- [ ] Hero customization menu (swap armor/weapon later)
- [ ] Cosmetic options (dye armor color)
- [ ] Companion gear recommendations (companion suggests upgrades based on enemy type)
- [ ] Transmogrification (cosmetic skins for gear)

---

## Acceptance Sign-Off

- [ ] Companion gear dialogue integrated into intro
- [ ] Forge visit sequence polished (animations + dialogue flow)
- [ ] Auto-equip works on hero model
- [ ] Equipment visually distinct per class
- [ ] Dialogue feels natural (companion guides hero)
- [ ] Works in WebGL build

> **AUDIT 2026-08-21 (agent fleet, read-only):** FIXED. Evidence: `CompanionGearSetup.cs:1-20` — wave-3 gear-up beat. Status was flipped from READY by the AUDIT, not by an implementation pass. The body below is left intact; if this call is wrong, the evidence cited here is what to challenge.
