# Design — the generic dungeon mapper (component-driven rooms)

**Owner idea, 2026-08-07:** *"leave things like that with four open doors, one on each of the four sides, and then just have a toggle … that allows us to toggle east, west, north, and south if we wanna remove the doorway. So it just really becomes a wall."* … *"make it as generic as possible to make as many combinations as possible just by adjusting the component settings."*

**Status:** design, not implemented · **From:** CLI seat · **For:** UI/Grok to turn into a WO

---

## 0. The headline: this is mostly CONSOLIDATION, not new machinery

Both halves of the idea already exist in the tree. Measured from `rooms-catalog.json`:

**Eleven of the seventeen kit rooms are the SAME 1×1 shell with different door counts.**

| doors | rooms |
|---|---|
| 1 | `DeadEnd`, `LoreShrine`, `RewardVault`, `SecretAlcove` |
| 2 | `Entrance`, `EntryHall`, `Straight`, `TurnLeft`, `TurnRight`, `ChokePoint`, `SideBranch` |
| 3 | `TJunction` |
| 4 | **`Intersection`** ← already the four-door room |

The remaining six are genuinely different: `CombatChamber` and `BossKeep` are 2×2, and the four stair rooms carry vertical sockets.

**And the toggle already runs on every bake.** `DungeonBakerChecks.SealSocket` closes any socket the composer did not mate — a doorway that becomes a wall. That is the owner's toggle, operating implicitly.

**So the proposal is not "build a feature." It is: make the toggle EXPLICIT and AUTHORABLE, then delete the eleven prefabs that only differ by which doors are open.**

---

## 1. The one real gap — the seal does not look like a wall

`DungeonBakerChecks.SealSocket` (`:250-266`) does:

```csharp
var wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
wall.name = $"Seal_{s.id}";
wall.transform.SetParent(s.transform, false);
wall.transform.localPosition = Vector3.forward * 0.15f + Vector3.up * (RoomForgeCanon.WallHeight * 0.5f);
wall.transform.localScale = new Vector3(s.halfWidth * 2f, RoomForgeCanon.WallHeight, 0.35f);
```

Name, parent, transform — **and no material.** The seal renders as the URP default, so a sealed doorway reads as a lighter patch, not as wall.

**This is why the idea feels like it needs new machinery when it does not.** Give the seal the room's wall material and it stops being a plug and becomes a wall. That single change is the difference between "the toggle exists" and "the toggle is usable."

⚠ **Assembly constraint:** `DungeonBakerChecks` is in the runtime `DeNelle.Dungeons` assembly and **cannot** reference `RoomForgeMaterials` (editor). Pass a `Material` **in** as an optional parameter — the same shape already proposed for the `CreatePlaceholderRoom` floor. Do not reach across.

---

## 2. The proposed shape

### 2.1 One shell, one component

Replace the eleven door-variants with **one 1×1 shell prefab carrying four door sockets** (`n_door_01`, `e_door_01`, `s_door_01`, `w_door_01`) plus a `RoomDoorMask` component:

```csharp
[System.Flags] public enum RoomSide { None = 0, N = 1, E = 2, S = 4, W = 8, All = 15 }

public sealed class RoomDoorMask : MonoBehaviour
{
    [SerializeField] private RoomSide _openSides = RoomSide.All;
}
```

**Applied at BAKE time, before `BuildNavMesh()`.** Not at runtime — the navmesh has to be built against the final walls, and a runtime toggle would bake a mesh through a door that is about to close.

### 2.2 The composer already knows the answer

`GraphDungeonComposer` mates the sockets the graph's `edges` name and leaves the rest unmated; `SealAndReport` then seals them. **So the mask can be DERIVED from the topology and needs no authoring in the common case** — the graph already says which sides are used.

The explicit mask earns its keep for the cases topology cannot express:
- a dead end you want to *keep* looking like a dead end even though a door could mate there
- a secret door (there is already `RoomSocket.isSecret` → `SEALED_SECRET`)
- a deliberately asymmetric room for pacing

**Derived by default, overridable by hand.** That is the split that keeps it generic without making every room a config exercise.

### 2.3 Archetype must stop riding on the prefab

**This is the part that will bite if it is skipped.** Today the prefab name carries meaning beyond geometry: `RoomPrefabMeta.archetype` distinguishes combat / lore / reward, and `DungeonBaker.LintPacing` reads those archetypes to check pacing targets (`combat=38% (target 60%)`, `lore`, `reward`).

Collapse eleven prefabs into one and **every room becomes the same archetype and the pacing linter goes blind.**

So archetype has to move to the **graph node**, where it already half-lives (`encounter`, `chests` are node properties). The prefab supplies geometry; the node supplies role. That is the right separation anyway — `RewardVault` being a distinct prefab is an accident of how the kit grew, not a design decision.

### 2.4 Footprint as a parameter, not a prefab

`CombatChamber` and `BossKeep` are 2×2. The same shell generator should take a footprint, so `1×1` and `2×2` are one code path with a parameter rather than two hand-maintained prefab families. `RoomForgeCanon.Cell` already makes the maths trivial.

---

## 3. What this buys — and the honest cost

**Buys:**
- **11 prefabs → 1.** The owner's stated pain is hand-editing rooms; hand-editing one shell instead of eleven is the whole win.
- Any shell change (trim, materials, ceiling detail) propagates to every configuration **automatically**. Today it needs eleven edits, and the eleventh is the one you forget.
- The composer gets more freedom: any room can serve any topology, so a graph is no longer constrained by which door-variants happen to exist.
- Fewer oracle surfaces. `[room-shell]` currently walks every prefab; one shell is one assertion.

**Costs, stated plainly:**
- **Archetype migration is not optional** (§2.3) and touches the graphs, the layout emit, and `LintPacing`.
- The eleven prefabs are referenced by **`prefab:` name in every graph JSON**. Deleting them without a migration breaks every dungeon. Either keep the names as aliases (the `LoadRoomPrefab` alias added for stair connectors is the exact precedent) or rewrite the graphs — and the alias is much cheaper.
- **`[room-shell]` and the layout fixtures pin the current 17-room count.** They must be updated in the same change, not after.

---

## 4. Suggested order (each step independently shippable)

1. **Give `SealSocket` a material** (§1). Small, self-contained, and it is the thing that makes a sealed door read as a wall. **Do this first — it improves today's dungeons whether or not the rest ever happens.**
2. **Move archetype from prefab to graph node** (§2.3), keeping `RoomPrefabMeta.archetype` as the fallback. No visual change; unblocks everything after.
3. **Add `RoomDoorMask`**, derived from topology, applied at bake before `BuildNavMesh()`.
4. **Generate one shell** at a given footprint + mask; keep the eleven names as `LoadRoomPrefab` aliases so no graph changes.
5. **Retire the eleven prefabs** once the aliases have baked clean across all dungeons.

**Do not do 4 before 2.** Collapsing prefabs while archetype still rides on the prefab name is how the pacing linter goes quietly green on a dungeon that is 100% combat.

---

## 5. Not in scope, deliberately

- The 2×2 rooms and the stair connectors stay separate families for now. Stairs carry vertical sockets and an ownership rule (`_Up` owns the flight, `_Down` the landing) that has nothing to do with door masks.
- This is a **quality/consolidation** change, not a player-facing one. Per the owner's own call tonight — *"get functional before we skin"* — it should not jump the multi-level traversal defect, which is still `PathPartial` on four of five dungeons.
