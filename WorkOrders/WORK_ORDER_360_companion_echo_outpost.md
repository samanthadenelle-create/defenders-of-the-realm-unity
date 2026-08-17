<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-360: Companion Introduction & Echo Auto-Deploy at Outpost Encounters

**Status:** READY TO IMPLEMENT  
**Estimated Effort:** P1–P2 (2–4 days)  
**Priority:** High (world exploration + pet onboarding)  
**Lane:** Narrative/Quests

---

## Overview

Introduce a new companion (dialogue + character spawn) after Wave 3 or on first outpost encounter. Simultaneously auto-deploy Echo (the pet) to aid the player with a contextual mini-tutorial:

1. **Companion Introduction** — A new story character meets the player after Wave 3 or at first world outpost location (triggers dialogue)
2. **Echo Auto-Deploy** — Pet summons automatically when entering an outpost encounter zone
3. **Pet Mini-Tutorial** — Quick visual + dialogue explaining Echo's role (healing, buffs, exploration aid)
4. **Visual Narrative Tie** — Companion + Echo meeting reinforces player agency (player can now explore with pet support)

**Why:** Wave 3 is a natural story beat. Outpost encounters are progression gates. Echo deployment bridges the gap between pet adoption (WO-XXX) and combat utility. Companion + Echo together signal "you're ready for the world."

---

## Acceptance Criteria

- [ ] New companion character spawns after Wave 3 or on first outpost entry (whichever comes first)
- [ ] Companion dialogue plays (Yarn node: `CompanionOutpostIntroduction`)
- [ ] Echo auto-deploys when entering outpost combat zone (appears near player)
- [ ] Echo mini-tutorial plays (visual indicator + dialogue: "Echo will aid you in battle")
- [ ] Tutorial highlights Echo's ability (heal, buff, or role-specific action)
- [ ] Player can dismiss tutorial (ESC or tap "Got it")
- [ ] Echo remains deployed for outpost battle duration
- [ ] On outpost victory, companion reacts (dialogue or gesture)
- [ ] Echo persists into exploration phase (doesn't despawn)
- [ ] Triggers only once (flag persists in save file)
- [ ] Works in WebGL build (dialogue via DialogueService)

---

## Files to Create

### New Files
- `Assets/_Modules/Village/NPCs/CompanionOutpostIntro.cs` — Trigger logic for companion spawn
- `Assets/_Modules/Village/Pets/EchoAutoDeployTrigger.cs` — Echo summon on outpost entry
- `Assets/_Modules/Village/Pets/EchoTutorialUI.cs` — Mini-tutorial overlay
- `Assets/Yarn/CompanionOutpostIntroduction.yarn` — Dialogue script

### Existing Files (Modify Minimally)
- `Assets/_Modules/Village/World/Camps/EnemyOutpost.cs` — Add entry event trigger
- `Assets/_Modules/Village/Waves/WaveManager.cs` — Post-Wave-3 completion hook
- `Assets/_Modules/Village/Pets/PetController.cs` — Echo auto-deploy method

---

## Design Spec

### Trigger Conditions

**Option A: Post-Wave 3 (Preferred)**
```csharp
// In WaveManager, after Wave 3 victory
if (WaveNumber == 3 && !GameState.CompanionIntroduced)
{
    CompanionOutpostIntro.TriggerIntroduction();
}
```

**Option B: First Outpost Entry (Fallback)**
```csharp
// In EnemyOutpost.OnPlayerEnter()
if (!GameState.CompanionIntroduced && 
    !GameState.HasVisitedAnyOutpost)
{
    CompanionOutpostIntro.TriggerIntroduction();
}
```

**Use Option A** (cleaner narrative flow: story beats, not location).

### Companion Introduction Flow

1. **Hero returns to village** after Wave 3 victory
2. **Companion spawns** at village center or gate (animation: walk/run in from edge)
3. **Dialogue plays:** "Greetings, brave one. I've heard of your victory. I offer my aid..."
4. **Echo appears** (particle effect: summon puff)
5. **Companion introduction dialogue concludes,** companion remains in village (follows player or stands at gate)
6. **Tutorial overlay appears:** "Meet Echo, a spirit companion. Echo can [heal/buff/scout]. Try summoning Echo in battle!"

### Companion Character Specs

**Name:** (Player or story choice — suggest "Mira" or "Kael")  
**Role:** Support/Guidance character (lore: scholar, mage, or seasoned warrior)  
**Visuals:** Distinct silhouette from hero/party members  
**Behavior:** Walks with player in village, visible in dialogue, stands near Heart during wave intro  
**Dialogue Node:** `CompanionOutpostIntroduction` (branching: first meeting → hero reaction → Echo intro)

### Echo Auto-Deploy Sequence

**Trigger:** Player enters outpost combat zone  
**Sequence:**
1. Outpost enemy wave spawns
2. Hero camera pans to show outpost
3. **Echo summoning effect** plays (golden light, summon circle on ground)
4. **Echo spawns** at hero's side (pet model + particle effects)
5. **Mini-tutorial UI appears** (bottom-left, 3-second duration or dismissible):
   ```
   "Echo has arrived!
    Echo will [heal you when low HP / grant +10% ATK / scout ahead]
    Tap to dismiss or press ESC"
   ```
6. **Hero + Echo enter combat** (normal outpost battle flow)

### Echo Mini-Tutorial UI

**Position:** Bottom-left corner (safe area + below HUD)  
**Design:** Semi-transparent stone panel with Echo icon  
**Content:**
```
🐾 Echo Deployed
Echo will [role-specific ability] during this battle.
Tap anywhere or press ESC to dismiss.
```

**Duration:** 3s (auto-dismiss) or tap to skip  
**Animation:** Slide in from left, fade out on dismiss  
**No blocking:** Player can move/attack during tutorial (non-intrusive)

### Yarn Dialogue Structure

```yarn
title: CompanionOutpostIntroduction
---
// Companion meets hero
COMPANION: Greetings! I've heard of your recent victories.

HERO: Who are you?

COMPANION: I am [Companion Name], scholar of the ancient ways.
          The darkness grows, and you'll need aid beyond your own strength.

COMPANION: Behold—Echo, a spirit bound to your cause.
          >> [Echo appears with summon effect]

ECHO: *ethereal chime*

COMPANION: Echo can [heal your wounds / strengthen your resolve].
          Call upon Echo in battle, and watch as the tide turns.

COMPANION: The outpost ahead holds threats unknown.
          Go now, with Echo at your side.

===
```

**Branching (optional):**
- If hero has specific pet class (Healer/Buffer/Scout) → customize Echo's role description
- If multiple companions exist → Companion comments on Echo's nature

---

## Implementation Notes

### CompanionOutpostIntro.cs

```csharp
public sealed class CompanionOutpostIntro : MonoBehaviour
{
    [SerializeField] private string _companionPrefabPath = "NPCs/CompanionMira";
    [SerializeField] private Vector3 _spawnPosition = Vector3.zero;
    [SerializeField] private float _spawnAnimationDuration = 1.5f;

    public static void TriggerIntroduction()
    {
        if (GameStateService.Instance?.State?.CompanionIntroduced ?? false)
            return;  // Already triggered

        var instance = FindObjectOfType<CompanionOutpostIntro>();
        if (instance != null)
            instance.PlayIntroduction();
    }

    private async UniTask PlayIntroduction()
    {
        // 1. Spawn companion
        var companion = SpawnCompanion(_spawnPosition);

        // 2. Play walk-in animation
        await companion.WalkTo(_spawnPosition, _spawnAnimationDuration);

        // 3. Start dialogue
        DialogueService.Play("CompanionOutpostIntroduction");

        // 4. Wait for dialogue to complete
        while (DialogueService.IsRunning)
            await UniTask.Delay(100);

        // 5. Mark as introduced
        GameStateService.Instance.MarkCompanionIntroduced();
        GameStateService.Instance.Save();

        // 6. Keep companion in scene (follows player or stands idle)
        companion.SetFollowBehavior(FollowBehavior.Idle);
    }

    private GameObject SpawnCompanion(Vector3 pos)
    {
        var prefab = Resources.Load<GameObject>(_companionPrefabPath);
        return Instantiate(prefab, pos, Quaternion.identity);
    }
}
```

### EchoAutoDeployTrigger.cs

```csharp
public sealed class EchoAutoDeployTrigger : MonoBehaviour
{
    private void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Player")) return;
        if (!GameStateService.Instance.State.CompanionIntroduced) return;

        // Deploy Echo
        var petController = PetController.Instance;
        if (petController != null)
        {
            petController.SummonEcho(other.transform.position + Vector3.right);
            
            // Show mini-tutorial if first deployment in outpost
            if (!GameStateService.Instance.State.EchoOutpostTutorialShown)
            {
                ShowEchoTutorial();
                GameStateService.Instance.MarkEchoTutorialShown();
            }
        }
    }

    private void ShowEchoTutorial()
    {
        // Instantiate tutorial UI
        var tutorialUI = Instantiate(Resources.Load<GameObject>("UI/EchoTutorial"));
        tutorialUI.GetComponent<EchoTutorialUI>().Show(3f);  // 3s duration
    }
}
```

### PetController.SummonEcho()

```csharp
public void SummonEcho(Vector3 position)
{
    // Spawn Echo at position with summon effect
    var echo = Instantiate(echoPrefab, position, Quaternion.identity);
    
    // Play summon VFX (golden circle, light burst)
    var vfx = Instantiate(summonVFXPrefab, position, Quaternion.identity);
    Destroy(vfx, 1f);
    
    // Play summon audio
    AudioService.PlayCue(AudioId.EchoSummon, position);
    
    // Set Echo as active combat pet
    _activePet = echo;
    echo.SetTarget(_hero.transform);
}
```

---

## Integration Checklist

- [ ] WaveManager.OnWaveComplete() → Check Wave == 3 → TriggerIntroduction()
- [ ] EnemyOutpost adds trigger zone → OnPlayerEnter() → EchoAutoDeployTrigger
- [ ] GameState has flags: `CompanionIntroduced`, `EchoOutpostTutorialShown`
- [ ] Yarn node `CompanionOutpostIntroduction` compiled in DefendersDialogue.yarnproject
- [ ] Echo summon VFX/audio ready (AudioId.EchoSummon)
- [ ] Companion prefab exists (NPC model + animator)
- [ ] Tutorial UI prefab in Resources/UI/EchoTutorial

---

## Testing Checklist

- [ ] Companion spawns after Wave 3 victory
- [ ] Companion dialogue plays without errors
- [ ] Echo auto-deploys on outpost entry
- [ ] Echo tutorial UI appears and dismisses correctly
- [ ] Echo remains active during outpost battle
- [ ] Companion flag persists across save/load
- [ ] No duplicate triggers (flag prevents re-triggering)
- [ ] Works in WebGL build
- [ ] Dialogue advances with click/tap
- [ ] No GC allocation during deploy

---

## What NOT to Touch

- Pet combat mechanics (Echo's healing/buff already implemented, just trigger deployment)
- Wave progression (WaveManager changes are minimal, just one hook)
- Outpost enemy balance (no changes to combat)
- Story scope (one companion, one Echo intro)

---

## Dependencies

- **Depends on:** Yarn Spinner (dialogue), PetController (Echo), WaveManager, EnemyOutpost
- **Unblocks:** Deeper companion storyline (WO-future: companion romance/questline)
- **Parallel:** None (2–4 days, can run solo)

---

## Narrative Hooks

**Post-Introduction possibilities:**
- Companion comments on player's strategy after each wave
- Echo reaction to major events (victory, defeat, upgrade unlocked)
- Companion offers side quests (explore X location, defeat Y enemies)
- Echo develops personality over time (unlocked in future WO)

---

## Acceptance Sign-Off

- [ ] Companion + Echo introduction sequence complete and narrative-coherent
- [ ] Echo auto-deploys reliably on outpost entry
- [ ] Tutorial teaches player Echo's role without blocking gameplay
- [ ] Flags prevent duplicate triggers
- [ ] Works in WebGL build with DialogueService
