<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WORK ORDER 156 — Authoritative Camera Rig: Pivot-Over-Walls + Occlusion Fade

**Status:** CLOSED — owner range sweep 2026-08-21 (WO 0-800): completed or immaterial.
**Lane:** Camera / UI — code, **parallel-safe** (no `VillageSceneBuilder`/village-freeze conflict). Runs now.
**Linear:** DEF-112. **Priority:** P1 — hero disappears behind tall walls; 3 competing camera controllers fight.

## Problem
Camera pitched at horizon, hero off-screen behind the 8–10m curtain walls. Three controllers currently
fight (HeroCinemachineRig priority 100 vs SmartMobileCamera vs others). Fix = ONE authoritative rig that
(a) pivots/lifts over walls (primary) and (b) ghosts only the occluding wall segments (fallback).

## Approach (decided, reviewed)
- **Single rig**, one camera→hero raycast/frame feeds BOTH the lift and the fade (no duplicate casts, can't disagree).
- **Pivot is primary:** when a wall occludes the hero, raise camera height (above default) so it sees over the parapet.
- **Occlusion fade is fallback:** fade ONLY the wall renderers the camera→hero ray hits; restore when clear.
- **Mobile-correct:** `RaycastNonAlloc` + cached collections (no per-frame GC), `MaterialPropertyBlock` (no material instancing), `sharedMaterial` reads.

## Material setup (URP) — walls
- Shader **Universal Render Pipeline / Lit**, **Surface Type = Transparent**, **Blending = Alpha**, **Alpha Clipping = Off**.
- Walls on a dedicated **`Wall` layer** (the rig's `wallLayer` mask).

## Final script — `CameraRigController.cs` (attach to Main Camera)
```csharp
using UnityEngine;
using System.Collections.Generic;

public class CameraRigController : MonoBehaviour
{
    [Header("Target & Follow")]
    public Transform target;                         // The Hero
    public Vector3 defaultOffset = new Vector3(0, 18f, -22f);
    public float smoothSpeed = 8f;

    [Header("Wall Avoidance")]
    public LayerMask wallLayer;
    public float raisedHeight = 32f;                 // > default so it lifts OVER 8-10m walls
    public float wallCheckDistance = 40f;
    public float fadeAlpha = 0.25f;
    public float fadeSpeed = 12f;

    private Vector3 currentOffset;
    private Camera cam;
    private readonly RaycastHit[] hitBuffer = new RaycastHit[16];
    private readonly Dictionary<Renderer, float> currentAlphas = new();
    private readonly HashSet<Renderer> hitRenderers = new();   // cached (no per-frame alloc)
    private readonly List<Renderer> toRemove = new();          // cached
    private MaterialPropertyBlock propBlock;

    private void Awake()
    {
        cam = GetComponent<Camera>();
        currentOffset = defaultOffset;
        propBlock = new MaterialPropertyBlock();
    }

    public void SetTarget(Transform newTarget) => target = newTarget;

    private void LateUpdate()
    {
        if (target == null) return;

        Vector3 direction = target.position - transform.position;
        float distance = direction.magnitude;
        int hitCount = Physics.RaycastNonAlloc(transform.position, direction, hitBuffer, distance, wallLayer);
        bool wallBlocking = hitCount > 0;

        hitRenderers.Clear();
        for (int i = 0; i < hitCount; i++)
        {
            Renderer rend = hitBuffer[i].collider.GetComponentInParent<Renderer>();
            if (rend != null && rend.sharedMaterial != null)
            {
                hitRenderers.Add(rend);
                if (!currentAlphas.ContainsKey(rend)) currentAlphas[rend] = 1f; // start opaque, lerp down
            }
        }

        float targetHeight = wallBlocking ? raisedHeight : defaultOffset.y;
        currentOffset.y = Mathf.Lerp(currentOffset.y, targetHeight, Time.deltaTime * smoothSpeed * 1.5f);

        Vector3 desiredPosition = target.position + currentOffset;
        transform.position = Vector3.Lerp(transform.position, desiredPosition, smoothSpeed * Time.deltaTime);
        transform.LookAt(target.position + Vector3.up * 2.5f);

        UpdateWallFades();
    }

    private void UpdateWallFades()
    {
        toRemove.Clear();
        foreach (var kvp in currentAlphas)
        {
            Renderer rend = kvp.Key;
            float target = hitRenderers.Contains(rend) ? fadeAlpha : 1f;
            float newAlpha = Mathf.MoveTowards(kvp.Value, target, Time.deltaTime * fadeSpeed);

            rend.GetPropertyBlock(propBlock);
            Color color = rend.sharedMaterial.GetColor("_BaseColor");
            color.a = newAlpha;
            propBlock.SetColor("_BaseColor", color);
            rend.SetPropertyBlock(propBlock);

            if (Mathf.Approximately(newAlpha, 1f)) toRemove.Add(rend);
            else currentAlphas[rend] = newAlpha;
        }
        foreach (var r in toRemove) currentAlphas.Remove(r);
    }
}
```

## Integration checklist (CLI/agent)
- [ ] **Remove/disable the 3 existing camera controllers** (HeroCinemachineRig, SmartMobileCamera, any others) — this rig is authoritative. Only ONE active camera driver.
- [ ] Walls → `Wall` layer; assign `wallLayer` mask on the rig.
- [ ] Wall material → URP/Lit **Transparent/Alpha** (so the `_BaseColor` alpha actually fades).
- [ ] **Wire `SetTarget(hero)` on hero spawn** (else it no-ops). Coordinate with the hero spawn/locomotion.
- [ ] Tune `raisedHeight` to the real wall height in-editor.

## Watch (playtest, minor)
- Gentle camera *bob* possible (lift→unblock→drop→reblock). Smoothing damps it; if visible, add ~0.3s hysteresis on the lift.
- `GetColor("_BaseColor")` used (bulletproof vs `.color`).

## Gate
Brace check; compile green; commit `feat: implement WO-156 — authoritative camera rig + wall occlusion fade`. No bake (runtime script; camera lane).

> **AUDIT 2026-08-21 (agent fleet, read-only):** OPEN — STILL VALID. Evidence: `no CameraRigController.cs` — pivot+occlusion rig unbuilt. Status left at READY deliberately: this work is real and unbuilt. Verified against HEAD 2f0b97bb5, not against the ticket's own claims.

> **OWNER RULING 2026-08-21 (verbal, this session):** CLOSED by an explicit owner sweep of the WHOLE 0-800 RANGE: "I've eyeballed them many times, and all of them are already completed. or immaterial." This is a RANGE close on the owner's direct review, not a per-ticket verdict. ⚠ NOTE FOR ANYONE REOPENING: the 2026-08-21 read-only audit had classified this one OPEN - STILL VALID, with the evidence cited above. The owner's review supersedes that call (owner statements are ground truth). The audit line is left in place deliberately, so if this work turns out to be needed, the evidence for it is still here rather than erased.
