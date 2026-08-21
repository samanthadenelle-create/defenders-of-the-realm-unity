<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-23
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-23) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK_ORDER_492 — V2 enemy cross-seam NavMesh traversal (Grok-guided)

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Relates:** memory [[v2-enemy-seam-navmesh-traversal]], `RuntimeRegionGate.cs`, `SceneTransitionTrigger.cs`.
**Why V2:** the V1 workaround is "castle = safe / reps spawn+roam OuterWorld-only / chase stalls at the
seam" (OverworldEncounterSpawner). This WO makes enemies actually path ACROSS the seam so that becomes a
design choice, not a limitation. **Owner: investigate with the bots after this is picked up — instrument +
headless-verify per §12, don't ship on Grok's snippet alone; it's a starting hypothesis to PROVE.**

## Grok's diagnosis (2026-06-23)
"Enemies losing tracking is a classic cross-scene NavMesh issue. NavMeshAgents lose their path when the
destination scene's navmesh isn't fully connected at runtime." Hero slides across (HeroLinkCrossing warp) +
walks back fine; the AGENTS are the gap.

## Grok's proposed fix (starting point — VERIFY before trusting)
**1. Strengthen the AI `NavMeshLink` in `RuntimeRegionGate.BuildAiLink(float width)`** — wider for groups,
explicitly bidirectional/walkable, `UpdateLink()`:
```csharp
private void BuildAiLink(float width)
{
    if (_aiLinkBuilt) return;
    Guard.Try("RuntimeSeam", "build AI NavMeshLink", () =>
    {
        var linkGo = new GameObject("RuntimeSeam_AiNavLink");
        linkGo.transform.SetParent(transform, false);
        var link = linkGo.AddComponent<NavMeshLink>();
        link.startPoint    = new Vector3(_gatePos.x, _gatePos.y + 0.5f, _thresholdZ);
        link.endPoint      = _landing;
        link.width         = Mathf.Max(width, 8f);   // wider for enemy groups
        link.bidirectional = true;
        link.area          = 0;                      // Walkable
        link.costModifier  = 1f;
        link.UpdateLink();
        _aiLinkBuilt = true;
        FlowTrace.Step("RuntimeSeam", $"AI NavMeshLink built: {link.startPoint} <-> {link.endPoint} (width={link.width})");
    });
}
```
**2. Add a re-path helper** (kick agents to re-path when the seam topology changes):
```csharp
public static void RepathAgentsNearSeam(Vector3 seamPosition, float radius = 30f)
{
    var agents = FindObjectsByType<NavMeshAgent>(FindObjectsSortMode.None);
    foreach (var agent in agents)
        if (Vector3.Distance(agent.transform.position, seamPosition) < radius)
            agent.ResetPath();   // + re-issue destination if a global target system exists
}
```
**3. Call `RuntimeRegionGate.RepathAgentsNearSeam(thresholdPosition)` from `SceneTransitionTrigger`
   after a cross (both directions).**

## Bot investigation plan (when picked up)
- INSTRUMENT first: log each near-seam agent's `pathStatus` (Complete/Partial/Invalid) + `hasPath` +
  `isOnNavMesh` before/after the link build + the re-path call — PROVE the link connects the islands
  (the current bake is `CollectObjects.Children`; confirm the link's start/end land on baked surfaces).
- Headless-verify with the fleet: an oracle that places an agent on the OuterWorld side, targets the
  castle side, asserts `PathComplete` across the seam (mirrors the existing RUNTIME_SEAM_NAV_OK hero check).
- Watch for: ASCII-only logs, brace gate, §12 (data before edit). Don't lift the V1 "castle = safe" gate
  in OverworldEncounterSpawner until the cross-seam path is PROVEN reliable headless.
- Confirm `_aiLinkBuilt` / existing `BuildAiLink` (RuntimeRegionGate ~527) isn't already doing a weaker
  version — extend, don't duplicate.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict.
