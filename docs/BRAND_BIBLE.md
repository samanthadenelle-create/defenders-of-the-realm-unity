# Brand Bible — Echoes of Elarion: Alerion's Awakening

> **Status: NAME LOCKING — pending owner confirm on exact spelling (see §1).** Captured 2026-05-30
> from owner rebrand directive. Supersedes the working title **"Defenders of the Realm"** everywhere.
> This is the source of truth for the creative rebrand sweep (WO-138).

## 1. The name
- **Title:** *Echoes of Elarion: Alerion's Awakening*
- **Short title / app name:** *Echoes of Elarion*
- **Spelling to confirm:** **"Echoes"** (standard) vs **"Echos"** (stylized drop-vowel, as first typed).
  Recommendation: **Echoes** — cleaner, still unique as a *phrase*, no misspell tax. Owner's call.
- **Why it's a good brand:** coined words → **trademark-ownable, zero SEO competition, no name clash**
  (owner's explicit goal: "a name not taken anywhere I can find"). Strong, ownable, searchable.

### The two names (canon — do NOT conflate)
- **Elarion** — the **realm / fallen city.** Already canon (CLAUDE.md §7); the Heart of Elarion sits at
  scene centre. Stays the in-world place name.
- **Alerion** — the **apex that awakens.** A heraldic eagle (wings displayed, no beak/talons) → maps
  onto our **apex dragon**. "Alerion's Awakening" = the dragon/ancient power stirring as the realm
  rebuilds. (One-letter-swap from Elarion is intentional kinship — realm vs the power that named it.)

## 2. The premise (owner, 2026-05-30)
**Elarion was the realm's great center of commerce — now decimated to ruin.** The player arrives at a
fallen, once-thriving city and **rebuilds it from the rubble** while **defending** the rising settlement
against the waves drawn by Alerion's awakening.

This premise *is* the game design — every pillar is justified by the fiction:
| Fiction | Mechanic it justifies |
|---|---|
| Ruined city you rebuild | Catalog + build-mode (place walls/towers/structures from the rubble) |
| Lost center of **commerce** | Resource/economy pillar, trade, monetization-as-prosperity |
| Waves drawn to the ruin | Defense loop (the F7 placement=role towers) |
| **Alerion awakening** | Escalation to the apex dragon boss; the title event |
| "Defenders of the **Realm**, not just the tower/city" | Scope ladder: tower → city → realm |

## 3. Tagline candidates (creative to refine)
- *Rebuild the fallen. Defend the realm. Wake the legend.*
- *From the ashes of Elarion, a defender rises.*
- *The city fell. The echoes remain.*

## 4. Tone
Stylized low-poly, mobile-first; hopeful-rebuild over grimdark — *ruin you restore*, not ruin you wallow
in. Heroic, mythic, warm dusk palette (matches the LastChanceLightingPreset skybox already in scene).

## 5. Rebrand sweep — surfaces (from repo grep of the old title)
**Technical strings (careful, brace/serialization-gated — build-connected session or CLI, NOT a loose agent):**
- `ProjectSettings/ProjectSettings.asset` → `productName: Defenders of the Realm` → new title
- Build output exe name `DefendersOfTheRealm.exe` (build scripts: `build-windows.ps1`,
  `build-webgl.ps1`, `DesktopBuild.cs`, `AndroidBuild.cs`, `install-apk-to-seeker.ps1`)
- Canonical strings: `Assets/StreamingAssets/Data/Canonical/canon-strings.json`, `en.json`,
  and `Assets/_Modules/Onboarding/CanonStrings.cs`
- In-game copy: `HelpMenu.cs`, `HeroSelectScreen.uxml`, `DevBootScene.cs`, intro/splash

**Narrative + marketing copy (creative lane):**
- `docs/narrative-bible.md`, `docs/NORTH_STAR.md`, `docs/whitepaper.md`, `docs/PI_PITCH.md`, store
  listings, README, pitch decks — rewrite around the ruin-of-commerce / awakening premise.

## 6. Sweep rules
- **Keep "Elarion" everywhere it already means the realm** — it was never the product title, it's canon.
- Replace only the **product title** "Defenders of the Realm" → "Echoes of Elarion: Alerion's Awakening"
  (short: "Echoes of Elarion").
- **Do not** rename the exe path/GUIDs casually mid-flight — coordinate the build-name change so build
  scripts + launch args + `Builds/` stay consistent (one atomic pass, then a verify build).
- Code/JSON edits via Write/Edit + brace/compile gate; ProjectSettings via careful single-field edit.
