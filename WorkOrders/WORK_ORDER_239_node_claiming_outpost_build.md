**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.

<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 239 — Kill → Claim → Build → Defend Loop (Node Claiming + Outpost System)

**Status: READY TO IMPLEMENT**
**Author:** UI + Owner (creative lane)
**WO Number:** 239
**Date:** 2026-06-02
**North Star rung:** Rung 4 — "Defend, explore, place your base"
**Supersedes:** earlier WO-239 draft

---

## Locked decisions (2026-06-02)

| Question | Decision |
|---|---|
| `resourcesInvested` tracking | Add `float resourcesInvested` to `ClaimableNode`. Increment on every build + upgrade. Raze returns `resourcesInvested * 0.60f`. |
| Kill count persistence | Yes — saved via `NodeStateService`. 3/6 on logout = 3/6 on return. |
| Camp repopulation after enemy raze | Yes — 30–90 min realtime (or on next region entry). Node becomes "abandoned" — re-clearable. |
| Claim without building | Yes — claiming auto-spawns **Master Outpost Hall** only. Farm/Watchtower are optional additions. |
| Max simultaneous claimed nodes | **3** (expandable later via tech tree). |
| Workers during raid | **Flee** — run to node edge, despawn. Reappear when raid ends. |
| Worker prefab | Use `Assets/Models/People` purchased pack (medieval tone). Not KayKit. |
| Building upgrade tuning | Placeholder JSON values for now — owner tunes after first pass. |
| Magic tech tree tie-in | **Separate** — economic/military upgrades now, Magic unlocks new building TYPES later. |
| Grom join trigger | **First return from OuterWorld** (not wave count). |
| "Return to Elarion" button | Only active if autosave exists. Otherwise: "No save — Return to Main Menu." |
| Hero death respawn | Show death screen → pause → player clicks [Rise Again] → respawn at village with partial resource loss. |
| GameState.SetMeta/GetMeta | **Needs adding.** Add `Dictionary<string,string> Meta` to `GameState` + thin accessors on `GameStateService`. |
| InteractionPrompt.Instance | Should exist — if null, bootstrap in Village scene startup. |
| Intro video format | **In-engine** (Timeline + Cinemachine) for first pass. |
| Voice | **AI-generated** first pass (ElevenLabs). Hire actors after loop feels good. |
| Heartwood asset | Still cathedral model. **Swap to regrowing tree needed** — new WO. |

---

## The loop this delivers

```
Kill enemies at camp  →  Camp clears  →  Press E to claim  →  Choose what to build
        ↓                                                              ↓
  Enemy.Died event                                        Building spawns at node
        ↓                                                              ↓
  Kill count tracked                                   Pets auto-harvest MineNode
        ↓                                                              ↓
  ClaimableNode.ClearCamp()                          TribeManager raids to take it back
```

**Key reuse — no new systems invented:**
- `Enemy.Died` event (line 873, `Enemy.cs`) — already fires on every kill, passes `Enemy` instance. Subscribe to it. Do NOT use `Physics.OverlapSphere` inside `Die()`.
- `TribeManager` (WO-160) — already raids any `IDamageableStructure`. Outpost just needs to implement it.
- `PetHarvester` (WO-229) — already harvests any `MineNode`. Outpost just needs one attached.
- `GameState` — already persisted. Node state goes here, not PlayerPrefs.
- `EconomyService.Grant(ResourceCost)` — already the resource bank. No new currency plumbing.

---

## Assembly map

| File | Assembly | Namespace |
|---|---|---|
| `ClaimableNode.cs` (new) | `DeNelle.Village` | `DeNelle.Village` |
| `NodeStateService.cs` (new) | `DeNelle.Village` | `DeNelle.Village` |
| `OutpostBuildPanel.cs` (new) | `DeNelle.Village` | `DeNelle.Village` |
| `Building.cs` (extend — outpost spawn path) | `DeNelle.Village` | `DeNelle.Village` |

---

## 1. `ClaimableNode.cs`

```csharp
// Assets/_Modules/Village/World/ClaimableNode.cs
using System;
using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Village
{
    public sealed class ClaimableNode : MonoBehaviour
    {
        [Header("Node settings")]
        public string nodeId       = "camp_01";       // unique — used for save key
        public string nodeName     = "Iron Camp";
        public int    killsRequired = 6;
        public float  claimRadius  = 12f;
        public ResourceNodeKind nodeResource = ResourceNodeKind.Iron;

        [Header("Runtime state")]
        public bool isCleared  = false;
        public bool isClaimed  = false;
        public int  currentKills = 0;

        public event Action<ClaimableNode> OnCleared;
        public event Action<ClaimableNode> OnClaimed;

        private readonly HashSet<Enemy> _registeredEnemies = new HashSet<Enemy>();
        private Transform _hero;
        private bool      _heroInRange;
        private OutpostBuildPanel _buildPanel;

        // ── called by RegionMobSpawner after spawning each enemy near this node ──
        public void RegisterEnemy(Enemy e)
        {
            if (e == null || isCleared || isClaimed) return;
            if (_registeredEnemies.Add(e))
                e.Died += HandleEnemyDied;
        }

        private void HandleEnemyDied(Enemy e)
        {
            e.Died -= HandleEnemyDied;
            _registeredEnemies.Remove(e);
            if (isCleared || isClaimed) return;
            currentKills++;
            NodeStateService.Instance?.Dirty(nodeId);
            if (currentKills >= killsRequired)
                ClearCamp();
        }

        private void ClearCamp()
        {
            isCleared = true;
            OnCleared?.Invoke(this);
            NodeStateService.Instance?.Dirty(nodeId);
            Debug.Log($"[ClaimableNode] {nodeName} cleared.");
        }

        private void Awake()
        {
            var h = GameObject.FindGameObjectWithTag("Player");
            if (h != null) _hero = h.transform;

            NodeStateService.Instance?.Register(this);
        }

        private void Update()
        {
            if (_hero == null || isClaimed) return;

            bool inRange = Vector3.Distance(transform.position, _hero.position) <= claimRadius;

            if (isCleared)
            {
                if (inRange && !_heroInRange)
                {
                    _heroInRange = true;
                    InteractionPrompt.Instance?.Show($"Press E — Claim {nodeName}");
                }
                else if (!inRange && _heroInRange)
                {
                    _heroInRange = false;
                    InteractionPrompt.Instance?.Hide();
                }

                if (_heroInRange && Input.GetKeyDown(KeyCode.E))
                    Claim();
            }
            else if (isClaimed && inRange && !_heroInRange)
            {
                // Already claimed — show build menu on E
                _heroInRange = true;
                InteractionPrompt.Instance?.Show($"Press E — Build at {nodeName}");
            }
            else if (isClaimed && !inRange && _heroInRange)
            {
                _heroInRange = false;
                InteractionPrompt.Instance?.Hide();
            }

            if (isClaimed && _heroInRange && Input.GetKeyDown(KeyCode.E))
                ShowBuildPanel();
        }

        public void Claim()
        {
            if (!isCleared || isClaimed) return;
            isClaimed = true;
            InteractionPrompt.Instance?.Hide();
            OnClaimed?.Invoke(this);
            NodeStateService.Instance?.Dirty(nodeId);
            Debug.Log($"[ClaimableNode] {nodeName} claimed.");
            ShowBuildPanel();
        }

        private void ShowBuildPanel()
        {
            if (_buildPanel == null)
            {
                var go  = new GameObject("OutpostBuildPanel");
                _buildPanel = go.AddComponent<OutpostBuildPanel>();
            }
            _buildPanel.Show(this);
        }
    }

    public enum ResourceNodeKind { Iron, Wood, Crystal, Food }
}
```

---

## 2. `NodeStateService.cs` (replaces NodeSaveManager — uses GameState)

Thin service that persists node state into `GameState` (the existing save path) rather than a
separate save file or PlayerPrefs.

```csharp
// Assets/_Modules/Village/World/NodeStateService.cs
using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.State;

namespace DeNelle.Village
{
    /// <summary>
    /// Tracks and persists ClaimableNode state via GameState.
    /// One DDOL singleton; no scene wiring needed.
    /// </summary>
    public sealed class NodeStateService : MonoBehaviour
    {
        public static NodeStateService Instance { get; private set; }

        private readonly Dictionary<string, ClaimableNode> _nodes  = new Dictionary<string, ClaimableNode>();
        private bool _dirty;

        private void Awake()
        {
            if (Instance != null) { Destroy(gameObject); return; }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance == null)
                new GameObject("NodeStateService").AddComponent<NodeStateService>();
        }

        public void Register(ClaimableNode node) => _nodes[node.nodeId] = node;

        public void Dirty(string nodeId)
        {
            _dirty = true;
            // Flush on next LateUpdate — batches rapid-fire changes
        }

        private void LateUpdate()
        {
            if (!_dirty) return;
            _dirty = false;
            Flush();
        }

        private void Flush()
        {
            // Write compact state per node into GameState string bag (extend GameState
            // with a Dictionary<string,string> NodeStates field if not present)
            foreach (var kv in _nodes)
            {
                var n   = kv.Value;
                var key = $"node_{n.nodeId}";
                // Simple CSV: cleared|claimed|kills
                var val = $"{(n.isCleared?1:0)}|{(n.isClaimed?1:0)}|{n.currentKills}";
                GameStateService.Instance?.SetMeta(key, val);
            }
            Debug.Log($"[NodeStateService] Flushed {_nodes.Count} node(s).");
        }

        public void RestoreAll()
        {
            foreach (var kv in _nodes)
            {
                var n   = kv.Value;
                var key = $"node_{n.nodeId}";
                var val = GameStateService.Instance?.GetMeta(key);
                if (string.IsNullOrEmpty(val)) continue;
                var parts = val.Split('|');
                if (parts.Length < 3) continue;
                n.isCleared    = parts[0] == "1";
                n.isClaimed    = parts[1] == "1";
                n.currentKills = int.TryParse(parts[2], out var k) ? k : 0;
            }
        }
    }
}
```

> **Note:** If `GameStateService.SetMeta/GetMeta` doesn't exist yet, add a
> `Dictionary<string,string> Meta` field to `GameState` and thin accessors. Alternatively,
> a flat `NodeStates` string in `GameState` works for MVP — just pick one and flag it.

---

## 3. `OutpostBuildPanel.cs` (code-built, no UXML)

```csharp
// Assets/_Modules/Village/World/OutpostBuildPanel.cs
using UnityEngine;
using UnityEngine.UI;

namespace DeNelle.Village
{
    public sealed class OutpostBuildPanel : MonoBehaviour
    {
        private static readonly string[] BuildOptions = { "Lumbermill", "Watchtower", "Farm" };

        private Canvas      _canvas;
        private GameObject  _panel;
        private ClaimableNode _node;

        private void Awake() { BuildCanvas(); Hide(); }

        public void Show(ClaimableNode node) { _node = node; _panel.SetActive(true); }
        public void Hide()                  { _panel?.SetActive(false); _node = null; }

        private void BuildCanvas()
        {
            _canvas             = gameObject.AddComponent<Canvas>();
            _canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 25;
            gameObject.AddComponent<CanvasScaler>();
            gameObject.AddComponent<GraphicRaycaster>();

            _panel = new GameObject("Panel", typeof(RectTransform));
            _panel.transform.SetParent(transform, false);
            _panel.AddComponent<Image>().color = new Color(0.05f, 0.05f, 0.12f, 0.94f);
            var pr = _panel.GetComponent<RectTransform>();
            pr.sizeDelta        = new Vector2(340f, 260f);
            pr.anchoredPosition = Vector2.zero;
            pr.anchorMin = pr.anchorMax = new Vector2(0.5f, 0.5f);

            MakeLabel("Node Claimed!", 20, new Vector2(0,  100));
            MakeLabel("Choose what to build:", 13, new Vector2(0, 72));

            float yPos = 35f;
            foreach (string type in BuildOptions)
            {
                string capturedType = type;
                MakeButton(type, new Vector2(0, yPos), () => OnBuild(capturedType));
                yPos -= 48f;
            }

            MakeButton("Leave for now", new Vector2(0, yPos - 4f), Hide);
        }

        private void OnBuild(string type)
        {
            if (_node == null) return;
            var pos = _node.transform.position + Vector3.up * 0.5f;

            // Reuse existing Building system (WO-237)
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.position   = pos;
            go.transform.localScale = new Vector3(2f, 3f, 2f);
            var b = go.AddComponent<Building>();
            b.buildingName = type;
            b.isUpgradable = true;

            // Attach a MineNode so pets can harvest
            var mine     = go.AddComponent<MineNode>();
            mine.Resource = _node.nodeResource == ResourceNodeKind.Iron    ? MineNode.ResourceKind.Iron
                          : _node.nodeResource == ResourceNodeKind.Wood     ? MineNode.ResourceKind.Wood
                          : _node.nodeResource == ResourceNodeKind.Crystal  ? MineNode.ResourceKind.AetherCrystal
                          :                                                    MineNode.ResourceKind.Food;

            NodeStateService.Instance?.Dirty(_node.nodeId);
            Debug.Log($"[OutpostBuildPanel] Built {type} at {_node.nodeName}");
            Hide();
        }

        private void MakeLabel(string text, int size, Vector2 pos)
        {
            var go = new GameObject("Label", typeof(RectTransform));
            go.transform.SetParent(_panel.transform, false);
            var t = go.AddComponent<Text>();
            t.text = text; t.fontSize = size; t.color = Color.white;
            t.alignment = TextAnchor.MiddleCenter;
            t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var r = go.GetComponent<RectTransform>();
            r.sizeDelta = new Vector2(320, 30);
            r.anchoredPosition = pos;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
        }

        private void MakeButton(string label, Vector2 pos, UnityEngine.Events.UnityAction cb)
        {
            var go = new GameObject(label, typeof(RectTransform));
            go.transform.SetParent(_panel.transform, false);
            go.AddComponent<Image>().color = new Color(0.15f, 0.35f, 0.55f);
            var btn = go.AddComponent<Button>();
            btn.onClick.AddListener(cb);
            var r = go.GetComponent<RectTransform>();
            r.sizeDelta = new Vector2(200, 36);
            r.anchoredPosition = pos;
            r.anchorMin = r.anchorMax = new Vector2(0.5f, 0.5f);
            MakeLabel(label, 13, Vector2.zero);
            go.GetComponentInChildren<Text>().transform.SetParent(go.transform, false);
        }
    }
}
```

---

## 4. Wire enemies to nodes — `RegionMobSpawner.cs`

After each spawn, register the enemy with its nearest `ClaimableNode`. One addition to existing
spawn method. Use event subscription — **do NOT use `Physics.OverlapSphere` inside `Enemy.Die()`**
(that breaks the enemy's death path and is expensive).

```csharp
// After enemy.Configure(...) in RegionMobSpawner — add:
float nearest = 32f;
ClaimableNode nearestNode = null;
foreach (var node in FindObjectsOfType<ClaimableNode>())
{
    float d = Vector3.Distance(spawnPos, node.transform.position);
    if (d < nearest) { nearest = d; nearestNode = node; }
}
nearestNode?.RegisterEnemy(enemy);
```

---

## 5. Raid — use `TribeManager`, not a new `RaidManager`

`TribeManager` (WO-160) already raids any `IDamageableStructure`. The `Building` MonoBehaviour
already implements `IDamageableStructure`. So once the outpost is built, it is automatically a
raid target — no new `RaidManager` needed.

If raid timing needs to be shorter for field nodes vs town waves, add a `NodeRaidInterval` field to
`TribeManager` and check it when the nearest `IDamageableStructure` is a field outpost (not the Heartwood).

---

## 6. Razing rules (locked in)

Two ways a node is lost. They feel completely different by design.

| | Player raze | Enemy raid raze |
|---|---|---|
| **Trigger** | Player chooses to abandon the outpost | Enemies destroy it (especially offline) |
| **Delay** | 45s (configurable) — player must stay near | Instant on HP reaching 0 |
| **Resource recovery** | 60% of invested resources returned | 0% — full loss |
| **Interruption** | Yes — leaving radius cancels it | No |
| **Stakes** | Can't panic-raze during an active raid | Coming back to a razed node is a real setback |

### Add to `ClaimableNode.cs`:

```csharp
// ── Razing (WO-239 final) ────────────────────────────────────────────────────
[Header("Razing")]
public float playerRazeDelay          = 45f;   // seconds
public float resourceRecoveryPercent  = 0.60f; // 60% back on player raze

private bool  _isRazing;
private float _razeStartTime;
private float _razeResourceValue;   // Wood/Iron/Crystal invested — set on build

public void StartPlayerRaze(float resourcesInvested)
{
    if (_isRazing || !isClaimed) return;
    _isRazing         = true;
    _razeStartTime    = Time.time;
    _razeResourceValue = resourcesInvested;
    Debug.Log($"[ClaimableNode] Razing {nodeName} — {playerRazeDelay}s remaining.");
    // ClaimableNode.Update() drives the countdown (below)
}

// Add to existing Update():
//   if (_isRazing)
//   {
//       if (Vector3.Distance(_hero.position, transform.position) > claimRadius)
//           CancelRaze();   // player walked away — cancelled
//       else if (Time.time - _razeStartTime >= playerRazeDelay)
//           FinishPlayerRaze();
//   }

private void FinishPlayerRaze()
{
    _isRazing = false;
    int recovered = Mathf.RoundToInt(_razeResourceValue * resourceRecoveryPercent);

    // Return resources via EconomyService (NOT HeroStats)
    var economy = FindObjectOfType<EconomyService>();
    economy?.Grant(ResourceCost.WoodOnly(recovered));   // adjust per nodeResource type

    DestroyNode();
    Debug.Log($"[ClaimableNode] {nodeName} razed by player — {recovered} resources recovered.");
}

private void CancelRaze()
{
    _isRazing = false;
    Debug.Log($"[ClaimableNode] Raze cancelled — player left the area.");
    InteractionPrompt.Instance?.Show("Raze cancelled.");
}

// Called by PlayerOutpost.OnDestroyed (enemy kill — IDamageableStructure event)
public void EnemyRaze()
{
    // 0% recovery — full loss
    DestroyNode();
    Debug.Log($"[ClaimableNode] {nodeName} destroyed by enemies. No resources recovered.");
}

private void DestroyNode()
{
    isClaimed  = false;
    isCleared  = false;
    currentKills = 0;
    NodeStateService.Instance?.Dirty(nodeId);
    // Destroy the outpost building if present
    foreach (var b in FindObjectsOfType<Building>())
        if (Vector3.Distance(b.transform.position, transform.position) < 3f)
            Destroy(b.gameObject);
}

public float GetRazeProgress() =>
    _isRazing ? Mathf.Clamp01((Time.time - _razeStartTime) / playerRazeDelay) : 0f;
// ─────────────────────────────────────────────────────────────────────────────
```

### Wire enemy raze via `IDamageableStructure.OnDestroyed`:

In `PlayerOutpost.cs`, in the `OnDestroyed` event handler, call back to the parent node:

```csharp
// In PlayerOutpost — after OnDestroyed fires:
var node = GetComponentInParent<ClaimableNode>()
        ?? FindObjectsOfType<ClaimableNode>()
               .OrderBy(n => Vector3.Distance(n.transform.position, transform.position))
               .FirstOrDefault();
node?.EnemyRaze();
```

### Raze confirmation UI (add to `OutpostBuildPanel`):

```csharp
// Add a "Raze Outpost" button to the build panel options.
// On click — show inline confirmation text, then start raze.
private void OnRazeConfirm()
{
    if (_node == null) return;
    _node.StartPlayerRaze(resourcesInvested: 150f); // pass actual build cost
    Hide();
    // Show progress bar — GetRazeProgress() polls 0→1 over 45s
}
```

**Progress bar:** code-built `Image` with `fillAmount = node.GetRazeProgress()` polled in `Update()`.
Display above the node in world space. Disappears on cancel or completion.

---

## 6. Player experience flow (the full interaction sequence)

```
1. Kill enemies around camp
        ↓ visual: camp fire goes out / enemies stop spawning
2. Walk within claimRadius (12m)
        ↓ prompt: "Press E — Claim Iron Camp"
3. Press E
        ↓ node turns cyan / "Iron Camp Claimed!" flash
        ↓ OutpostBuildPanel opens automatically after 0.6s
4. Choose what to build (Lumbermill / Watchtower / Farm)
        ↓ building spawns at node with MineNode attached
5. Walk up to the built building
        ↓ prompt: "Press E to Upgrade" (existing BuildingInteractable — WO-237)
6. Press E → BuildingUpgradePanel opens (existing — WO-237)
```

The hero controller needs two tracked references and one E-key handler:

```csharp
// In HeroLocomotion.cs or a dedicated InteractionController — add:

private ClaimableNode  _nearbyNode;
private Building       _nearbyBuilding;

// In trigger detection (OnTriggerEnter / proximity check):
//   if (other has ClaimableNode) → _nearbyNode = node
//   if (other has Building && isUpgradable) → _nearbyBuilding = building

private void Update()
{
    if (!Input.GetKeyDown(KeyCode.E)) return;

    if (_nearbyNode != null)
    {
        if (!_nearbyNode.isClaimed)
            _nearbyNode.Claim();           // claim → auto-shows build panel
        else
            _nearbyNode.ShowBuildPanel();  // already claimed → show build options
    }
    else if (_nearbyBuilding != null && _nearbyBuilding.isUpgradable)
    {
        // Re-use BuildingUpgradePanel from WO-237
        if (BuildingUpgradePanel.Instance == null)
            new GameObject("BuildingUpgradePanel").AddComponent<BuildingUpgradePanel>();
        BuildingUpgradePanel.Instance?.Show(_nearbyBuilding);
    }
}
```

**Priority order if both are in range:** node interaction takes priority over building upgrade.

---

## Acceptance criteria

- [ ] Player raze takes 45s, cancels if hero leaves radius, returns 60% resources via `EconomyService`
- [ ] Enemy raze (outpost HP → 0) returns 0% resources and resets the node to uncleared
- [ ] Raze progress bar visible during the 45s countdown
- [ ] Killing `killsRequired` enemies near a `ClaimableNode` fires `ClearCamp()`
- [ ] "Press E — Claim" prompt appears in claim radius after camp clears
- [ ] Claiming triggers cyan visual feedback + "Node Claimed!" message
- [ ] `OutpostBuildPanel` opens automatically 0.6s after claim
- [ ] Three build options (Lumbermill / Watchtower / Farm) displayed
- [ ] Selected building spawns at node position with a `MineNode` attached
- [ ] Pets harvest from the outpost's `MineNode` automatically (existing PetHarvester)
- [ ] Walking up to built building shows "Press E to Upgrade" (WO-237 path)
- [ ] `TribeManager` raids the outpost (it implements `IDamageableStructure`)
- [ ] Node state (cleared / claimed / kills) persists across sessions via `GameState`
- [ ] No `Physics.OverlapSphere` added to `Enemy.Die()` — uses `Enemy.Died` event only
- [ ] No UXML / UIDocument
- [ ] Brace balance passed on all new `.cs` files

---

## 9. Ambient workers — "little hammer people" (outpost feels alive)

When an outpost is claimed and built, small worker NPCs spawn around it, wander, and occasionally
play a work animation. Pure feel — no gameplay impact. They despawn on raze.

Worker count scales with outpost level (ties into WO-237 upgrade system):

| Outpost level | Workers |
|---|---|
| 1 | 1 |
| 2 | 2 |
| 3–4 | 3 |
| 5 (max) | 4 |

### `OutpostWorker.cs`

```csharp
// Assets/_Modules/Village/World/OutpostWorker.cs
using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace DeNelle.Village
{
    /// <summary>
    /// Ambient worker NPC. Wanders the outpost, plays a work animation occasionally.
    /// No gameplay logic — pure feel.
    /// </summary>
    public sealed class OutpostWorker : MonoBehaviour
    {
        [Header("Behaviour")]
        public float wanderRadius     = 6f;
        public float workPauseMin     = 3f;
        public float workPauseMax     = 7f;
        public float walkSpeed        = 1.4f;

        private Transform    _outpostCenter;
        private NavMeshAgent _agent;
        private Animator     _animator;    // optional — works without one

        public void AssignToOutpost(Transform center)
        {
            _outpostCenter = center;
            _agent         = GetComponent<NavMeshAgent>();
            _animator      = GetComponent<Animator>();

            if (_agent != null) _agent.speed = walkSpeed;

            StartCoroutine(WorkerLife());
        }

        private IEnumerator WorkerLife()
        {
            while (true)
            {
                // Pick a random point near the outpost
                Vector3 target = _outpostCenter.position
                               + new Vector3(Random.Range(-wanderRadius, wanderRadius), 0f,
                                             Random.Range(-wanderRadius, wanderRadius));

                // Move — use NavMesh if available, else lerp
                if (_agent != null && NavMesh.SamplePosition(target, out var hit, 4f, NavMesh.AllAreas))
                {
                    _agent.SetDestination(hit.position);
                    _animator?.SetBool("Walking", true);

                    while (_agent.pathPending || _agent.remainingDistance > 0.6f)
                        yield return null;

                    _animator?.SetBool("Walking", false);
                }
                else
                {
                    // Fallback: simple move towards
                    while (Vector3.Distance(transform.position, target) > 0.5f)
                    {
                        transform.position = Vector3.MoveTowards(
                            transform.position, target, walkSpeed * Time.deltaTime);
                        transform.LookAt(new Vector3(target.x, transform.position.y, target.z));
                        yield return null;
                    }
                }

                // Pause + work animation
                _animator?.SetTrigger("Work");  // "Hammer" / "Mine" / any work clip
                yield return new WaitForSeconds(Random.Range(workPauseMin, workPauseMax));
            }
        }
    }
}
```

### Add to `ClaimableNode.cs`:

```csharp
// ── Workers (WO-239) ─────────────────────────────────────────────────────────
[Header("Workers")]
public GameObject workerPrefab;   // assign low-poly villager prefab in Inspector

private readonly List<OutpostWorker> _workers = new List<OutpostWorker>();

private void SpawnWorkers(int count)
{
    foreach (var w in _workers)
        if (w != null) Destroy(w.gameObject);
    _workers.Clear();

    for (int i = 0; i < count; i++)
    {
        if (workerPrefab == null) break;
        var pos = transform.position
                + new Vector3(Random.Range(-4f, 4f), 0.15f, Random.Range(-4f, 4f));
        var go  = Instantiate(workerPrefab, pos, Quaternion.identity);
        var w   = go.GetComponent<OutpostWorker>() ?? go.AddComponent<OutpostWorker>();
        w.AssignToOutpost(transform);
        _workers.Add(w);
    }
}

// Call this from Claim() and from Building level-up callback:
public void RefreshWorkers(int outpostLevel)
{
    int count = outpostLevel switch { 1 => 1, 2 => 2, 3 => 3, 4 => 3, 5 => 4, _ => 1 };
    SpawnWorkers(count);
}

// Call from DestroyNode():
private void DespawnWorkers()
{
    foreach (var w in _workers)
        if (w != null) Destroy(w.gameObject);
    _workers.Clear();
}
// ─────────────────────────────────────────────────────────────────────────────
```

**Prefab note:** No worker prefab yet? Spawn a KayKit villager capsule with a `TextMesh` name tag —
same pattern as the existing ambient NPCs. Workers don't need a full rig for the first pass; a
wandering capsule with a bobbing Y-animation reads correctly at mobile scale.

---

## 10. Master Outpost Hall — auto-spawned on claim

Auto-spawns the moment a node is claimed. It is the visual anchor and storage hub of every outpost.
The player is NOT asked to build it — it appears automatically.

**Spec:**
- Name: `"Outpost Hall"`
- Is a `Building` with `isUpgradable = true`, `maxLevel = 3`
- JSON: `OutpostHallUpgrades.json`
- Visual: Medieval wooden longhouse + banner pole (use KayKit `building_farmstead` or `building_house` + a flag child object)
- Functions: primary resource storage + small global trickle

**`OutpostHallUpgrades.json`**
```json
{
  "upgrades": [
    {
      "id": "hall_storage",
      "title": "Expanded Stores",
      "description": "Increases resource storage cap by 200 across all types",
      "woodCost": 80, "stoneCost": 40, "ironCost": 0, "crystalCost": 0,
      "boosts": [{ "stat": "StorageCap", "value": 200, "type": "add" }]
    },
    {
      "id": "hall_trickle",
      "title": "Established Settlement",
      "description": "Generates +2 of each resource per minute passively",
      "woodCost": 100, "stoneCost": 60, "ironCost": 40, "crystalCost": 20,
      "boosts": [{ "stat": "ResourceTrickle", "value": 2, "type": "add" }]
    }
  ]
}
```

**Auto-spawn in `ClaimableNode.Claim()`:**
```csharp
// After claim — spawn Hall before showing build panel
var hallGo  = new GameObject("Outpost Hall");
hallGo.transform.position = transform.position + Vector3.up * 0.5f;
var hall    = hallGo.AddComponent<Building>();
hall.buildingName = "OutpostHall";
hall.isUpgradable = true;
AddInvestment(0f);   // Hall is free — investment starts at 0
```

---

## 11. Final consolidated `ClaimableNode.cs`

All decisions baked in. This replaces the earlier draft in Section 1.

```csharp
// Assets/_Modules/Village/World/ClaimableNode.cs
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace DeNelle.Village
{
    public sealed class ClaimableNode : MonoBehaviour
    {
        // ── Config ───────────────────────────────────────────────────────────
        [Header("Node")]
        public string nodeId          = "camp_01";
        public string nodeName        = "Iron Camp";
        public int    killsRequired   = 6;
        public float  claimRadius     = 12f;
        public ResourceNodeKind nodeResource = ResourceNodeKind.Iron;

        [Header("Razing")]
        public float playerRazeDelay         = 45f;
        public float resourceRecoveryPercent = 0.60f;

        // ── State ────────────────────────────────────────────────────────────
        [HideInInspector] public bool  isCleared    = false;
        [HideInInspector] public bool  isClaimed    = false;
        [HideInInspector] public int   currentKills = 0;
        [HideInInspector] public float resourcesInvested = 0f;

        public event Action<ClaimableNode> OnCleared;
        public event Action<ClaimableNode> OnClaimed;

        // ── Private ──────────────────────────────────────────────────────────
        private readonly HashSet<Enemy> _registered = new HashSet<Enemy>();
        private Transform _hero;
        private bool      _heroInRange;
        private bool      _isRazing;
        private float     _razeStartTime;
        private DateTime  _lastClearedTime = DateTime.MinValue;
        private readonly List<OutpostWorker> _workers = new List<OutpostWorker>();
        private OutpostBuildPanel _buildPanel;

        [Header("Workers")]
        public GameObject workerPrefab;   // Assets/Models/People character

        // ── Lifecycle ────────────────────────────────────────────────────────
        private void Awake()
        {
            var h = GameObject.FindGameObjectWithTag("Player");
            if (h) _hero = h.transform;
            NodeStateService.Instance?.Register(this);
        }

        private void Update()
        {
            HandleRazeProgress();
            HandleRepopulation();
            HandleProximityPrompt();
        }

        // ── Kill registration ────────────────────────────────────────────────
        public void RegisterEnemy(Enemy e)
        {
            if (e == null || isCleared || isClaimed) return;
            if (_registered.Add(e)) e.Died += OnEnemyDied;
        }

        private void OnEnemyDied(Enemy e)
        {
            e.Died -= OnEnemyDied;
            _registered.Remove(e);
            if (isCleared || isClaimed) return;
            currentKills++;
            NodeStateService.Instance?.Dirty(nodeId);
            if (currentKills >= killsRequired) ClearCamp();
        }

        private void ClearCamp()
        {
            isCleared = true;
            OnCleared?.Invoke(this);
            NodeStateService.Instance?.Dirty(nodeId);
        }

        // ── Claiming ─────────────────────────────────────────────────────────
        public void Claim()
        {
            if (!isCleared || isClaimed) return;
            isClaimed = true;
            InteractionPrompt.Instance?.Hide();
            SpawnHall();
            SpawnWorkers(1);
            OnClaimed?.Invoke(this);
            NodeStateService.Instance?.Dirty(nodeId);
            StartCoroutine(OpenBuildPanelDelayed(0.6f));
        }

        private IEnumerator OpenBuildPanelDelayed(float delay)
        {
            yield return new WaitForSeconds(delay);
            ShowBuildPanel();
        }

        private void ShowBuildPanel()
        {
            if (_buildPanel == null)
            {
                var go  = new GameObject("OutpostBuildPanel");
                _buildPanel = go.AddComponent<OutpostBuildPanel>();
            }
            _buildPanel.Show(this);
        }

        private void SpawnHall()
        {
            var go = new GameObject("Outpost Hall");
            go.transform.position = transform.position + Vector3.up * 0.5f;
            var b = go.AddComponent<Building>();
            b.buildingName = "OutpostHall";
            b.isUpgradable = true;
        }

        // ── Investment tracking ──────────────────────────────────────────────
        public void AddInvestment(float amount)
        {
            resourcesInvested += amount;
            NodeStateService.Instance?.Dirty(nodeId);
        }

        // ── Player raze ──────────────────────────────────────────────────────
        public void StartPlayerRaze()
        {
            if (_isRazing || !isClaimed) return;
            _isRazing      = true;
            _razeStartTime = Time.time;
        }

        private void HandleRazeProgress()
        {
            if (!_isRazing) return;
            if (_hero != null && Vector3.Distance(_hero.position, transform.position) > claimRadius)
            { CancelRaze(); return; }
            if (Time.time - _razeStartTime >= playerRazeDelay)
                FinishPlayerRaze();
        }

        private void FinishPlayerRaze()
        {
            _isRazing = false;
            int recovered = Mathf.RoundToInt(resourcesInvested * resourceRecoveryPercent);
            FindObjectOfType<EconomyService>()?.Grant(
                nodeResource == ResourceNodeKind.Iron    ? ResourceCost.IronOnly(recovered)    :
                nodeResource == ResourceNodeKind.Wood    ? ResourceCost.WoodOnly(recovered)    :
                nodeResource == ResourceNodeKind.Crystal ? ResourceCost.CrystalsOnly(recovered) :
                                                           ResourceCost.WoodOnly(recovered));
            DestroyNode(playerInitiated: true);
        }

        private void CancelRaze()
        {
            _isRazing = false;
            InteractionPrompt.Instance?.Show("Raze cancelled.");
        }

        public float GetRazeProgress() =>
            _isRazing ? Mathf.Clamp01((Time.time - _razeStartTime) / playerRazeDelay) : 0f;

        // ── Enemy raze ───────────────────────────────────────────────────────
        public void EnemyRaze()
        {
            // 0% recovery
            DestroyNode(playerInitiated: false);
        }

        private void DestroyNode(bool playerInitiated)
        {
            isClaimed  = false;
            isCleared  = false;
            currentKills = 0;
            resourcesInvested = 0f;
            _lastClearedTime = DateTime.UtcNow;
            DespawnWorkers();
            foreach (var b in FindObjectsOfType<Building>())
                if (Vector3.Distance(b.transform.position, transform.position) < 5f)
                    Destroy(b.gameObject);
            NodeStateService.Instance?.Dirty(nodeId);
        }

        // ── Repopulation ─────────────────────────────────────────────────────
        private void HandleRepopulation()
        {
            if (isClaimed || isCleared || _lastClearedTime == DateTime.MinValue) return;
            var hoursSince = (DateTime.UtcNow - _lastClearedTime).TotalHours;
            if (hoursSince >= 0.5)  // 30 min minimum
            {
                // Reset — enemies will respawn from RegionMobSpawner on next tick
                isCleared    = false;
                currentKills = 0;
                _lastClearedTime = DateTime.MinValue;
                NodeStateService.Instance?.Dirty(nodeId);
            }
        }

        // ── Proximity prompt ─────────────────────────────────────────────────
        private void HandleProximityPrompt()
        {
            if (_hero == null) return;
            bool inRange = Vector3.Distance(_hero.position, transform.position) <= claimRadius;

            if (inRange && !_heroInRange)
            {
                _heroInRange = true;
                if (isCleared && !isClaimed)
                    InteractionPrompt.Instance?.Show($"Press E — Claim {nodeName}");
                else if (isClaimed)
                    InteractionPrompt.Instance?.Show($"Press E — Build at {nodeName}");
            }
            else if (!inRange && _heroInRange)
            {
                _heroInRange = false;
                InteractionPrompt.Instance?.Hide();
            }

            if (_heroInRange && Input.GetKeyDown(KeyCode.E))
            {
                if (isCleared && !isClaimed) Claim();
                else if (isClaimed)          ShowBuildPanel();
            }
        }

        // ── Workers ──────────────────────────────────────────────────────────
        public void RefreshWorkers(int level)
        {
            int count = level switch { 1 => 1, 2 => 2, 3 => 3, 4 => 3, 5 => 4, _ => 1 };
            SpawnWorkers(count);
        }

        private void SpawnWorkers(int count)
        {
            DespawnWorkers();
            for (int i = 0; i < count; i++)
            {
                if (workerPrefab == null) break;
                var pos = transform.position
                        + new Vector3(UnityEngine.Random.Range(-4f, 4f), 0.15f,
                                      UnityEngine.Random.Range(-4f, 4f));
                var go  = Instantiate(workerPrefab, pos, Quaternion.identity);
                var w   = go.GetComponent<OutpostWorker>() ?? go.AddComponent<OutpostWorker>();
                w.AssignToOutpost(transform);
                _workers.Add(w);
            }
        }

        // Workers flee during raid — called by TribeManager raid start event
        public void FleeWorkers()   => _workers.ForEach(w => { if (w) Destroy(w.gameObject); });
        public void ReturnWorkers() => SpawnWorkers(_workers.Count > 0 ? _workers.Count : 1);

        private void DespawnWorkers()
        {
            _workers.ForEach(w => { if (w) Destroy(w.gameObject); });
            _workers.Clear();
        }
    }
}
```

---

## What NOT to touch

- `Enemy.cs` internals — subscribe to `Died` only
- `TribeManager.cs` — no changes; it already raids `IDamageableStructure` targets
- `PetHarvester.cs` — no changes; it already harvests `MineNode` objects
- `Village.unity` — do not hand-edit

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
