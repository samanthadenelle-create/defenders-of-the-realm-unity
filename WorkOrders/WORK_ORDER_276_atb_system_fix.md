**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-276: Fix & Implement FF-Style ATB System

**Status: READY TO IMPLEMENT**

**Date:** 2026-06-01  
**Priority:** 🔴 CRITICAL (blocking battle feature)  
**Owner:** CLI  
**Blocks:** Combat feel (WO-217–219), game progression  
**Depends On:** BattleController exists in scene  
**Time Estimate:** 3–4 hours

---

## The Critical Blocking Issue

**Current state:** BattleController._runtimeState is null → battle never starts → "No ATBRuntimeState assigned" error

**Immediate fix (5 minutes):**
1. Open `Scenes/ATBBattle.unity`
2. Select the BattleController GameObject
3. In Inspector, find field `_runtimeState`
4. Drag `Assets/_Modules/BattleATB/Generated/ATBRuntimeState.asset` into that field
5. Save scene

**This single fix should stop the null reference error and let battle run.**

---

## Complete FF-Style ATB System

### Core Concept

- Characters have **ATB bars** that fill over time (like Final Fantasy)
- When bar is full → character is "Ready"
- Player picks action (Attack, Skill) → character animates + deals damage → bar resets
- Enemies auto-act when ready (no input delay)
- Simple, turn-based feel, very retro-FF

---

## A) ATBUnit.cs (Base Class)

Create: **Assets/Scripts/Combat/ATB/ATBUnit.cs**

```csharp
using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Base class for any unit in ATB combat (hero, enemy).
/// Handles ATB bar filling, readiness, and basic actions.
/// </summary>
public class ATBUnit : MonoBehaviour
{
    [Header("=== ATB Settings ===")]
    public float atbSpeed = 1.2f;           // How fast the bar fills (tune for difficulty)
    public float maxATB = 100f;             // When bar reaches this, unit is ready

    [Header("=== References ===")]
    public Image atbBar;                    // UI bar display
    public Animator animator;               // For attack/hit animations
    public Text healthDisplay;              // Optional: show current HP

    [Header("=== Health ===")]
    public float maxHealth = 100f;
    private float currentHealth;

    // State
    public bool IsPlayerSide { get; set; }
    public bool IsReady => currentATB >= maxATB;
    public bool IsAlive => currentHealth > 0;

    private float currentATB = 0f;
    private bool isActing = false;

    private void Start()
    {
        currentHealth = maxHealth;
        UpdateHealthDisplay();
    }

    /// <summary>
    /// Called every frame to fill the ATB bar.
    /// </summary>
    public void UpdateATB(float deltaTime)
    {
        // Don't fill if already acting or ready
        if (isActing || IsReady) return;

        currentATB += atbSpeed * deltaTime * 30f; // 30 is tuning factor for feel
        
        if (currentATB > maxATB)
            currentATB = maxATB;

        // Update UI
        if (atbBar != null)
            atbBar.fillAmount = currentATB / maxATB;
    }

    /// <summary>
    /// Lock the ATB bar while action is playing.
    /// </summary>
    public void StartAction()
    {
        isActing = true;
        currentATB = 0f;
        
        if (atbBar != null)
            atbBar.fillAmount = 0;
    }

    /// <summary>
    /// Unlock ATB bar, allow it to fill again.
    /// </summary>
    public void FinishAction()
    {
        isActing = false;
    }

    /// <summary>
    /// Example: Perform a basic attack on a target.
    /// </summary>
    public void PerformAttack(ATBUnit target)
    {
        if (target == null || !target.IsAlive) return;

        StartAction();

        // Play attack animation
        if (animator != null)
            animator.SetTrigger("Attack");

        // Play sound
        if (GameManager.Instance?.AudioManager != null)
            GameManager.Instance.AudioManager.PlaySFX("attack_sword");

        // Deal damage after animation delay (match your animation length)
        StartCoroutine(DealDamageAfterDelay(target, 35f, 1.2f));
    }

    private IEnumerator DealDamageAfterDelay(ATBUnit target, float damage, float delay)
    {
        yield return new WaitForSeconds(delay);
        target.TakeDamage(damage);
        FinishAction();
    }

    /// <summary>
    /// Take damage and potentially die.
    /// </summary>
    public void TakeDamage(float amount)
    {
        currentHealth -= amount;

        // Play hit animation
        if (animator != null)
            animator.SetTrigger("Hit");

        UpdateHealthDisplay();

        Debug.Log($"{name} took {amount} damage! (HP: {currentHealth}/{maxHealth})");

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (animator != null)
            animator.SetTrigger("Death");

        // Disable this unit
        GetComponent<Collider>().enabled = false;
        enabled = false;
    }

    private void UpdateHealthDisplay()
    {
        if (healthDisplay != null)
            healthDisplay.text = $"HP: {currentHealth}/{maxHealth}";
    }

    public float GetHealthPercent() => currentHealth / maxHealth;
}
```

---

## B) Improved BattleController.cs

Create / Replace: **Assets/Scripts/Combat/BattleController.cs**

```csharp
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Main ATB battle orchestrator.
/// Manages turn order, action selection, and battle flow.
/// </summary>
public class BattleController : MonoBehaviour
{
    [Header("=== Core Data ===")]
    [SerializeField] private ATBRuntimeState _runtimeState;   // ← CRITICAL: Must be assigned!

    [Header("=== Scene References ===")]
    [SerializeField] private Transform playerPartyParent;
    [SerializeField] private Transform enemyPartyParent;

    [Header("=== UI References ===")]
    [SerializeField] private Canvas battleCanvas;
    [SerializeField] private Button attackButton;
    [SerializeField] private Button skillButton;
    [SerializeField] private Button defendButton;
    [SerializeField] private Text battleLogText;

    // State
    private List<ATBUnit> heroes = new List<ATBUnit>();
    private List<ATBUnit> enemies = new List<ATBUnit>();
    private ATBUnit currentActingUnit;
    private bool battleActive = false;
    private bool awaitingPlayerInput = false;

    private void Start()
    {
        // CRITICAL: Check if _runtimeState is assigned
        if (_runtimeState == null)
        {
            Debug.LogError("[BattleController] ❌ CRITICAL: No ATBRuntimeState assigned in Inspector!");
            Debug.LogError("   → Open ATBBattle.unity");
            Debug.LogError("   → Select BattleController GameObject");
            Debug.LogError("   → Drag ATBRuntimeState.asset into the _runtimeState field");
            return;
        }

        InitializeBattle();
    }

    private void InitializeBattle()
    {
        SpawnParty();
        BindUIButtons();
        battleActive = true;
        
        Debug.Log("✅ ATB Battle Started — Classic FF Style");
        Log("Battle Start! Heroes vs Enemies");
    }

    private void Update()
    {
        if (!battleActive) return;

        // Update all units' ATB bars
        foreach (var unit in GetAllUnits())
        {
            if (unit.IsAlive)
                unit.UpdateATB(Time.deltaTime);
        }

        // Check if any unit is ready to act
        if (currentActingUnit == null && !awaitingPlayerInput)
        {
            ATBUnit readyUnit = GetAllUnits()
                .Where(u => u.IsReady && u.IsAlive)
                .OrderByDescending(u => u.IsPlayerSide) // Heroes first
                .FirstOrDefault();

            if (readyUnit != null)
                StartUnitTurn(readyUnit);
        }

        // Check if battle is over
        if (heroes.All(h => !h.IsAlive))
        {
            BattleOver(false); // Enemies won
        }
        else if (enemies.All(e => !e.IsAlive))
        {
            BattleOver(true); // Heroes won
        }
    }

    private void StartUnitTurn(ATBUnit unit)
    {
        currentActingUnit = unit;
        Log($"{unit.name} is ready!");

        if (unit.IsPlayerSide)
        {
            // Show action menu for player
            awaitingPlayerInput = true;
            ShowActionMenu(unit);
        }
        else
        {
            // Auto-act for enemy (with slight delay for drama)
            StartCoroutine(EnemyAutoAttack(unit));
        }
    }

    private void ShowActionMenu(ATBUnit hero)
    {
        // Highlight the current hero
        Log($"{hero.name}'s turn! Choose action:");

        // Enable buttons
        attackButton.interactable = true;
        skillButton.interactable = true;
        defendButton.interactable = true;

        // Store reference for button callbacks
        CurrentActor = hero;
    }

    public ATBUnit CurrentActor { get; private set; }

    // Button callbacks
    public void OnAttackButtonPressed()
    {
        if (!awaitingPlayerInput || CurrentActor == null) return;

        ATBUnit target = enemies.Where(e => e.IsAlive).FirstOrDefault();
        if (target != null)
        {
            CurrentActor.PerformAttack(target);
            awaitingPlayerInput = false;
            StartCoroutine(WaitForActionToFinish());
        }
    }

    public void OnSkillButtonPressed()
    {
        // TODO: Implement skill menu
        Log("[Skill] Not yet implemented");
    }

    public void OnDefendButtonPressed()
    {
        // TODO: Implement defend mechanic
        Log($"{CurrentActor.name} takes a defensive stance!");
        awaitingPlayerInput = false;
        EndCurrentTurn();
    }

    private IEnumerator WaitForActionToFinish()
    {
        yield return new WaitForSeconds(2f); // Wait for animation + damage
        EndCurrentTurn();
    }

    private IEnumerator EnemyAutoAttack(ATBUnit enemy)
    {
        yield return new WaitForSeconds(0.8f); // Dramatic pause

        ATBUnit target = heroes.Where(h => h.IsAlive).FirstOrDefault();
        if (target != null)
        {
            Log($"{enemy.name} attacks {target.name}!");
            enemy.PerformAttack(target);
        }

        yield return new WaitForSeconds(2f); // Wait for animation
        EndCurrentTurn();
    }

    private void EndCurrentTurn()
    {
        currentActingUnit = null;
    }

    private void BattleOver(bool heroesWon)
    {
        battleActive = false;
        
        if (heroesWon)
        {
            Log("✅ Victory! You defeated the enemies!");
            StartCoroutine(ReturnToVillage());
        }
        else
        {
            Log("❌ Defeat! Your party was vanquished...");
            StartCoroutine(ShowDefeatScreen());
        }
    }

    private IEnumerator ReturnToVillage()
    {
        yield return new WaitForSeconds(3f);
        GameManager.Instance?.TransitionToVillage();
    }

    private IEnumerator ShowDefeatScreen()
    {
        yield return new WaitForSeconds(3f);
        // TODO: Show game over screen
    }

    private void SpawnParty()
    {
        // Find all ATBUnits in the scene
        var allUnits = FindObjectsByType<ATBUnit>(FindObjectsSortMode.None);

        heroes = allUnits.Where(u => u.IsPlayerSide).ToList();
        enemies = allUnits.Where(u => !u.IsPlayerSide).ToList();

        Debug.Log($"✅ Battle parties spawned: {heroes.Count} heroes, {enemies.Count} enemies");
    }

    private void BindUIButtons()
    {
        if (attackButton != null)
            attackButton.onClick.AddListener(OnAttackButtonPressed);
        if (skillButton != null)
            skillButton.onClick.AddListener(OnSkillButtonPressed);
        if (defendButton != null)
            defendButton.onClick.AddListener(OnDefendButtonPressed);
    }

    private List<ATBUnit> GetAllUnits()
    {
        return heroes.Concat(enemies).Where(u => u != null).ToList();
    }

    private void Log(string message)
    {
        Debug.Log($"[Battle] {message}");
        if (battleLogText != null)
            battleLogText.text = message;
    }
}
```

---

## Integration Steps

### Step 1: Immediate Fix (5 min)
1. Open `Scenes/ATBBattle.unity`
2. Find BattleController GameObject in Hierarchy
3. In Inspector, find `_runtimeState` field
4. Drag `Assets/_Modules/BattleATB/Generated/ATBRuntimeState.asset` into field
5. **Save scene**

### Step 2: Add Scripts (10 min)
1. Create `Assets/Scripts/Combat/ATB/` folder
2. Add ATBUnit.cs to this folder
3. Replace BattleController.cs in `Assets/Scripts/Combat/`

### Step 3: Scene Setup (20 min)
1. In ATBBattle.unity:
   - Select each hero/enemy capsule
   - Add ATBUnit.cs component
   - Assign Animator, atbBar (UI Image), healthDisplay (Text)
   - Set IsPlayerSide (true for heroes, false for enemies)
2. Select BattleController
   - Assign playerPartyParent (parent of all hero capsules)
   - Assign enemyPartyParent (parent of all enemy capsules)
   - Assign battleCanvas and buttons
3. **Save scene**

### Step 4: Test (30 min)
1. Play the ATBBattle scene
2. Watch ATB bars fill
3. Click "Attack" when a hero is ready
4. Watch enemy auto-act
5. Verify battle completes

---

## Acceptance Criteria

- [ ] _runtimeState assigned in BattleController
- [ ] ATBUnit.cs compiles (no errors)
- [ ] BattleController.cs compiles (no errors)
- [ ] ATB bars visible in scene (UI shows)
- [ ] ATB bars fill over time (enemies + heroes)
- [ ] Hero can click "Attack" button when ready
- [ ] Attack animation plays + damage dealt
- [ ] Enemy auto-acts when ready
- [ ] Battle ends when one side is defeated
- [ ] No null reference errors in console

---

## Tuning Parameters (After It Works)

Once battle runs, tune these for feel:

- **ATBUnit.atbSpeed** — Higher = faster turns (try 1.2 for classic FF feel)
- **maxATB** — Higher = longer bars (try 100 for UI clarity)
- **Animation delays** — Match your actual animation lengths

---

## Known Limitations (OK for MVP)

- ❌ Only "Attack" action works (Skills/Defend are TODO)
- ❌ No target selection UI (just attacks random alive enemy)
- ❌ No damage calculation (flat 35 damage per attack)
- ❌ No status effects (poison, sleep, etc.)

**These can be added later. MVP is: bars fill → units act → damage applied → battle ends.**

---

## Commit Message

`"WO-276: implement FF-style ATB system — ATB bars, turn queue, hero actions, enemy AI"`

---

**Estimate:** 3–4 hours (most time is UI wiring, not code)

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
