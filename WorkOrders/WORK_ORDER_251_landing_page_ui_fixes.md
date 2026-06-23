# WORK ORDER 251 — Landing Page UI Fixes (Dragon Art + Overlaps + Z-order)
**Status: READY TO IMPLEMENT**
**WO:** 251 | **Lane:** HUD (parallel safe — no scene edits)
**Closes:** DEF-134, DEF-144, DEF-145

---
## DEF-134 — Dragon art missing from landing page

Dragon image/sprite is missing from the top of the hero-select landing page. Black gap visible.

**Fix:** In the hero-select screen builder (MainMenu or HeroSelectController):
1. Find the dragon image slot — `Image` component or `Sprite` field at top of screen
2. Ensure it is assigned and loaded: `Resources.Load<Sprite>("UI/Dragon")` or assign in Inspector
3. If asset exists but fails to load: check the asset is in a `Resources/` folder and path is correct
4. Asset must be ≤2MB and load within 3s

**Acceptance criteria:**
- [ ] Dragon renders at top of hero-select page in WebGL
- [ ] No black gap at any viewport 375px–428px
- [ ] Asset ≤2MB
- [ ] Confirmed on Chrome mobile

---
## DEF-144 — Connect Wallet button overlaps title text

Connect Wallet button and Skip button overlap title/subtitle on mobile.

**Fix:** In hero-select/landing page layout code:
1. Add ≥8px margin below subtitle before Connect Wallet button
2. Skip button must be positioned so it does NOT overlap Connect Wallet
3. Test at minimum viewport width 375px

**Acceptance criteria:**
- [ ] "Connect Wallet" button renders below subtitle with ≥8px margin
- [ ] Skip button does not overlap Connect Wallet at any width 375px–428px
- [ ] Full button text "Connect Wallet" is never truncated
- [ ] Confirmed on Chrome mobile

---
## DEF-145 — Intro lore text overlaps title (z-order / animation sequencing)

Intro narration text and title/subtitle are rendering simultaneously — z-index collision.

**Fix:** In the intro animation sequence:
1. Ensure narration text `CanvasGroup.alpha` is 0 before title begins rendering
2. Check Canvas `sortingOrder` — narration panel must be on a lower order than title
3. Add a sequence: narration fades to 0 → 0.3s gap → title fades in

```csharp
// Correct sequencing:
yield return StartCoroutine(FadeOut(_narrationGroup, 0.5f));
yield return new WaitForSeconds(0.3f);
yield return StartCoroutine(FadeIn(_titleGroup, 0.5f));
```

**Acceptance criteria:**
- [ ] Narration text is at alpha 0 before title/subtitle become visible — no simultaneous render
- [ ] Canvas SortOrder separates the two layers (title > narration)
- [ ] Confirmed on Chrome mobile at 375px width

---
## What NOT to touch
- `Village.unity` — do not hand-edit
- No UXML / UIDocument
- Brace balance check passed on all modified `.cs` files
