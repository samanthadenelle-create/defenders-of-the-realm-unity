# WebGL Asset Review — mark what to REMOVE

**How to use:** put `KEEP` / `REMOVE` / `?` in the Decision column (or just tell me in chat). I'll turn
your calls into the cleanup for WO-191 Phase 0. `Assets/Resources/` = 537 MB raw → 181 MB in the build,
and it force-ships EVERYTHING here whether used or not.

---

## A. Obvious dead weight (my flag: REMOVE)
| Size | Asset | Why | Decision |
|---|---|---|---|
| **91 MB** | `Cosmetics/Pets/pet-aether-twilight.fbx` | **Zero references** anywhere in code/prefabs — orphan. | ____ |

## B. Duplicated textures (same image shipping 2–3×) — my flag: DEDUPE to one copy
These are the same texture in multiple folders (`.fbm` auto-extract + a `Textures/` copy + a top-level `Textures/` copy). Keep ONE, point materials at it, stop the FBX re-embedding.
| Size each | Duplicated asset (appears in these folders) | Decision |
|---|---|---|
| 15.7 MB ×3 | dragon basecolor — `Pets/flame-pup.fbm/`, `Pets/Textures/`, `Textures/flame-pup.png` | ____ |
| ~17–18 MB ×2 | archer basecolor — `Heroes/Ranger.fbm/`, `Heroes/Textures/` | ____ |
| 5.2 MB ×3 | knight/cc5 normal map — `Heroes/Knight.fbm/`, `Heroes/Textures/`, `CC5Hero/Tex/` | ____ |
| 9.7 MB ×3 | aether basecolor — `Pets/aether-sprite.fbm/`, `Pets/Textures/`, `Textures/aether-sprite.png` | ____ |
| 11.9 MB ×2 | ice-fox basecolor — `Pets/Textures/`, `Textures/ice-wolf.png` | ____ |

> The whole top-level **`Textures/` folder (85 MB)** looks like duplicate copies of model textures
> (Cathedral, Knight, flame-pup, ice-wolf, aether-sprite). Likely mostly removable — **verify per file.**

## C. Possibly-unused content (my flag: VERIFY with you)
| Size | Asset | Question | Decision |
|---|---|---|---|
| 21 MB | `CC5Hero/` (cc5hero.fbx 13M + tex) | Old CC5 character-pipeline experiment — is this hero actually used? | ____ |
| 25 MB | `Textures/Cathedral.png` | Is the cathedral texture used in the live scene, or leftover? | ____ |
| 2.8 MB | `Resources/Enemy/` (vs `Enemies/`) | Two enemy folders — is `Enemy/` a stale duplicate of `Enemies/`? | ____ |

## D. In-use content — KEEP, but compress (Phase 1, not removal)
These are real game assets; we shrink them (crunch textures, 2048→1024, mono audio) rather than delete:
| Size | Group | Note |
|---|---|---|
| 171 MB | `Pets/` (flame-pup, aether-sprite, ice-wolf + textures) | Are all 3 starter pets in the game? If one's cut, that's a removal. |
| 122 MB | `Heroes/` (wizard, knight, archer + textures) | All 3 hero classes used? |
| 23 MB | `Structures/` (PetHouse, tree_of_life, Portal) | Used in village. |
| 18 MB | `Enemies/` (Dragon + materials) | Used. |
| 5.5 MB | `PatriciaLight/` (Tower) | Defend-the-Tower asset. |

---

## Quick questions where your call removes whole chunks
1. **Both top-level `Textures/` (85 MB) and the `Cosmetics/` orphan (91 MB)** — safe to purge? That's ~140 MB raw right there.
2. **CC5Hero (21 MB)** — keep or cut?
3. **All 3 pets + all 3 heroes actually shipping in the game**, or are any leftover/unused?
4. Anything else you know you've cut that might still be in here.
