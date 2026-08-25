# WORK ORDER 1192 — Rumor Board redesign: the slab shows the REWARD (DESIGN)

- **Status:** DESIGN SPEC — READY (opens with two OWNER DECISION GATES; see §6)
- **Lane:** 4 (UI legibility / conformance)
- **Author:** UI seat, on `claude/ui-spacing-layout-review-bqas0h` (2026-08-25)
- **Relationship to the CLI ticket:** the CLI seat owns `WorkOrders/WORK_ORDER_1192_rumor_board_redesign.md`. This `.DESIGN.md` supplies the redesign the owner re-scoped to the UI seat — merge it into that ticket.
- **Branch of record for citations:** `origin/wip/village2-and-f8-tickets` (near-current CLI tree). Reward data below read from `wip`'s `Assets/Resources/Data/Canonical/quests.json`.
- **Evidence note (§12):** the capture `docs/ui-captures/RumorBoard_2670x1200.png` is a git-LFS object and this cloud container has no `git-lfs`, so I could not view the pixels. The layout findings in §4 are the CLI seat's direct read of that capture, relayed verbatim; the reward design in §3/§5 is grounded in the actual `quests.json` reward schema, which I did read.
- **Deliverable:** design spec + image briefs (§7) for CLI implementation. The UI seat does not write `.cs`.

---

## 1. The design pivot (owner, 2026-08-25 — supersedes the illustration plan)

> "instead of that, why not use that image place to highlight the rewards of completing it — isn't that really what denotes what quest you want to take?"

Recorded in `FOUNDATIONAL_RULINGS.md` (above §12), **superseding §11 in place**: the parchment slab shows the quest's **REWARD**, not an illustration. This RETIRES the quest-art program (the 24 briefs in `docs/QUEST_IMAGE_BRIEFS.md` keep value only as a giver/synopsis/outcome inventory, not as art work orders) and turns the slab into the **showcase surface for the WO-1195 / §13 reward-grammar fix**: reward = **icon + quantity**, via the same `ElarionUiKit.CurrencyChip`. This is the SAME fix, not a second one.

It also resolves the layout finding (§4): the panel's dead bottom ~40% now has an obvious purpose.

## 2. Grounding — the reward schema (from `wip` `quests.json`)

Rewards are authored **per STAGE**: `"reward": { "crystals": N, "food": N, "magic": N, "grantItemId": "..." }`, plus `"grantsKeystone": bool`. Quest ordering/gating is `"requiresQuestId": "<prior quest id>"`. There are **24 quests**. Worked examples:

- `forgemasters_act2` final stage → `crystals:100, food:60` (**resource** reward).
- `forgemaster.first-commission` stage 2 → `magic:10, grantItemId:"knight_iron"` (**item + resource**).
- `forgemasters_act3` final stage → `crystals:150, magic:50, grantsKeystone:true` (**mixed + keystone**).
- `forgemasters_act4` → `crystals:300, magic:100, grantsKeystone:true` (**keystone**).
- `forgemasters_act1` "Honest Steel" → single stage, **all-zero reward, no item, no keystone** = **NO AUTHORED REWARD**. Its payoff is that `forgemasters_act2` has `requiresQuestId:"forgemasters_act1"` → the real grant is **"Unlocks: The Old Fire (Act 2)."**

## 3. The five reward shapes the slab MUST render

Design ALL of these; do not design only the clean all-numeric case.

1. **Resource** — one `CurrencyChip` per non-zero of `{crystals, food, magic}`: icon + compact quantity.
2. **Item** (`grantItemId`) — item icon + the item's **display name** (a name, not a quantity). Resolve the icon + name from the item catalog, not from the raw id (`knight_iron` → "Iron Longsword" + its sprite).
3. **Keystone** (`grantsKeystone:true`) — a distinct **keystone** mark (shape-distinct, e.g. a faceted seal) + label "Keystone" (or the keystone's name if one exists). It is a milestone, visually weightier than a resource chip.
4. **Progression-only** (all-zero, no item, no keystone) — render **"Unlocks: <title of the quest whose `requiresQuestId` is this quest>"**. Derive it from the chain; never invent. See §6 gate 2.
5. **Mixed** — any combination above in one row (e.g. `crystals 150` + `magic 50` + keystone). A wrapping row of chips + the item/keystone/unlock element; must not clip (the §4 clipping bug).

**Which stage's reward is "the" reward?** The completion payoff = the **terminal (last) stage's** reward. See §6 gate 1 (terminal vs. aggregate).

## 4. Layout findings to fix (CLI's read of the 2670x1200 capture — I could not view it)

The panel already has the room; it is not using it. Bottom ~40% is empty black while text truncates in three places:
- **Overlap:** the subtitle "The talk of Elarion. Accept what calls to you." is painted THROUGH the second quest row (readable text behind text). Not a crop — a z/rect overlap. Fix the layout rects so the subtitle owns its own band.
- **Mid-word clip, no scroll affordance:** objective body clips at "...have begun to sin" with no indication there is more. Add a scroll affordance or size the body to content.
- **Ellipsis title:** detail title ends "...Archive of O...". Give the title its own line height / wrap.
- **Under-fill:** two quests shown in a list area that could hold ~six — use the space (and the freed bottom 40% takes the reward slab).
- **"* All" tab** carries a literal asterisk prefix — a marker leaked into player copy. Strip it (verify at source).

## 5. The reward slab — layout

Place the slab in the reclaimed lower region of the detail column (right stage). Same obsidian-on-parchment kit as the rest of the panel.
- **Header:** "REWARD" (ASCII).
- **Body:** the terminal reward rendered by shape (§3): resource chips (icon+qty) wrap left-to-right; an item shows icon + name; a keystone shows its mark + label; progression-only shows "Unlocks: <next>".
- **Reuse `ElarionUiKit.CurrencyChip`** for the resource chips (icon resolves via `concept-icons.json`; the WO-1195 spec establishes this). Do NOT hand-roll a reward widget (trips `[ui-obsidian]`).
- **Colourblind:** chips/marks separate by **shape/silhouette**, never hue (owner is red/green colourblind — `FOUNDATIONAL_RULINGS §4`). Must survive a greyscale check. Keep a text label beside each icon (as the HUD `BuildResourceChips` does) so identity never rests on colour.
- The slab is a **read-out, not a control** — no `MinTouchPx` growth; `LayoutOracle` allow-list unchanged (no waiver).

## 6. ⛔ OWNER DECISION GATES

1. **Terminal vs. aggregate reward.** Show only the **final stage's** reward (the completion payoff — UI-seat recommendation, matches "rewards of completing it"), or **sum across stages**? Recommendation: terminal — it is what "completing it" grants and avoids double-counting mid-stage drips.
2. **The empty-reward quest** (`forgemasters_act1`, and any other all-zero quest). The slab may **never render blank**. Pick:
   - **(a)** render the derived grant — **"Unlocks: The Old Fire (Act 2)"** (UI-seat recommendation; a progression payoff is a legitimate, often better, reward than "90 food"), or
   - **(b)** author an explicit reward for it in `quests.json` (CLI dev-lane change, not UI).
   Either way, **any quest whose payoff is progression must SAY so.** The spec (§3.4) and mocks (§7) show BOTH a resource-reward and a progression-only quest.

## 7. Image-generation briefs (for the owner to render)

Dark obsidian detail column (`#050506`, thin gold rim), parchment-cream text (`#EAD9B0`). Reward icons distinguished by **shape**, greyscale-legible. Render the full detail column: title, role/blurb, objective, then the **REWARD slab** in the lower region.

- **Brief A — resource + item reward:** REWARD slab shows `[crystal] 150   [magic-sigil] 50   [sword icon] Iron Longsword`. Caption: "Reward = icon + quantity (item = icon + name)."
- **Brief B — progression-only:** same panel, REWARD slab reads `Unlocks: The Old Fire (Act 2)` with a small chain/seal glyph. Caption: "Progression is a reward — never blank."
- **Brief C — keystone / mixed:** `[crystal] 300   [magic-sigil] 100   [keystone seal] Keystone`. Caption: "Keystone milestone reads heavier than a resource chip."
- **Brief D — before/after of the panel:** TOP = the current capture's problems (subtitle overlapping row 2, clipped objective, ellipsis title, empty bottom). BOTTOM = fixed rects + six quests visible + the reward slab filling the lower region. Caption: "The space was always there."

## 8. Acceptance criteria

- [ ] **AC-1 — reward is icon+quantity.** No quest reward renders as a bare letter/number; resources use `CurrencyChip`; item = icon+name; keystone = mark+label. Proving line: `[Flow:UI]` step logging each quest's resolved reward shape + rendered elements.
- [ ] **AC-2 — never blank.** Every quest's slab renders a non-empty reward; an all-zero/no-item/no-keystone quest renders its derived "Unlocks: <next>" (or an authored reward per gate 2). Test with `forgemasters_act1` explicitly.
- [ ] **AC-3 — all five shapes.** Resource, item, keystone, progression-only, and mixed each render correctly (test one quest of each from §2).
- [ ] **AC-4 — colourblind-safe.** Reward glyphs distinguishable in greyscale at slab size; a text label accompanies each icon. (If the resource/magic icons differ mainly by hue, that is a finding to escalate.)
- [ ] **AC-5 — layout fixes.** Subtitle no longer overlaps a quest row; objective body does not clip mid-word without a scroll affordance; detail title no longer ellipsizes; the list shows more than two quests; the "* All" tab asterisk is gone.
- [ ] **AC-6 — read-out not control.** Slab elements keep `raycastTarget=false`, no `MinTouchPx` growth, `LayoutOracle` allow-list unchanged.

## 9. Dependencies / flags for the owner + CLI

- ⚠ **`magic` has no concept-icon.** The WO-1195 dossier found `concept-icons.json` carries gold/wood/iron/food/crystal but **no `magic`/`wisdom`** entry, yet quest rewards grant `magic`. Author a `magic` concept-icon (shape-distinct, greyscale-legible) or the magic chip falls back to a text tag. Owner ruling / CLI data task.
- **Item id → display name + icon** must resolve from the item catalog (e.g. `knight_iron` → "Iron Longsword"); confirm the catalog path with the CLI. No second icon registry.
- The relic example the CLI cited (`Relic Drowned Ledger`) is the item shape (§3.2); its id lives in whichever quest grants it — resolve via the same item catalog.

## 10. What NOT to do

- ⛔ Do NOT spec a reusable art slot or brief quest illustrations — the illustration plan (§11) is retired for the quest board.
- ⛔ Do NOT brief anything off the two quest titles visible in the capture — the CLI reports they are **harness fixtures**, not shipping data.
- ⛔ Do NOT edit `quests.json` reward data (UI seat writes no code/data); gate-2(b) is a CLI dev-lane change if the owner picks it.
- Do not treat this reward row as already in breach of §13 (§13 is scoped to build-screen costs); reusing the chip here is proposed as an **extension** of §13 for the owner to bless, not something §13 already decides.
