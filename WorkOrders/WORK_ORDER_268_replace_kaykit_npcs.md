<!-- era-sweep-2026-08-17 -->
> ### ⚠ AGED 2026-08-17 — still READY, but unverified since 2026-06-22
> The 2026-08-17 era sweep found **no evidence** that this WO's subject was deleted or superseded, so its **Status stays READY** and nothing else was changed. It is simply OLD (git first-add 2026-06-22) and has not been re-verified against current canon (`CANON_GROUND_TRUTH_*.md`, CLAUDE.md §7). **Re-verify before pulling it.**

# WO-268: Replace KayKit placeholder NPCs with purchased character pack
**Linear:** [DEF-91](https://linear.app/defenders-of-the-realm/issue/DEF-91/replace-kaykit-placeholder-npcs-with-purchased-character-pack)
**Lane:** World/Environment
**Status:** READY TO IMPLEMENT
**Priority:** High

## Acceptance Criteria
- [ ] All 4 NPC archetypes visible in Village scene with correct mesh + textures (no pink/white materials)
- [ ] Wandering NPCs play Walk animation when moving, blend to Idle when stopped
- [ ] Talking NPCs play Talk animation when TownsfolkBubble is visible
- [ ] Blacksmith plays Forging loop at the forge post
- [ ] NavMesh pathing unaffected (no agent errors in console)
- [ ] No compilation errors

## Files to Edit
- `Assets/_Modules/Village/NPCs/AmbientNPC.cs` — add Animator integration
- New prefabs under `Assets/_Modules/Village/NPCs/Prefabs/`
- New materials under `Assets/_Modules/Village/NPCs/Materials/`
- New animator controllers under `Assets/_Modules/Village/NPCs/Animators/`
- Character FBX import settings in `Assets/Models/People/`

## Do NOT Touch
- Village.unity (never hand-edit)
- TownsfolkBubble, TownsfolkDialogue, TownsfolkController — only AmbientNPC gets Animator wiring
- Files outside World/Environment lane

## Dependencies
- Character pack already imported at `Assets/Models/People`
- Requires Village rebake for scene wiring (Phase 3)
- VSB is serialization bottleneck — coordinate
