# Offset Forge — Asset Store Listing Kit

Everything you need to submit this as a first listing. Copy/paste the sections into the
Publisher Portal fields. Replace every `<...>` placeholder.

---

## Title
**Offset Forge — Visual Attachment Offset Tool**

## Short / summary (one line)
Load any model, rotate X/Y/Z by eye, copy the exact offset. Stop guessing weapon & prop alignment.

## Category
Tools ▸ Utilities  (secondary: Tools ▸ Modeling)

## Suggested price
**$9.99** for v1.0 (a focused single-purpose editor tool). You can launch at an introductory
**$4.99** for the first 2 weeks to gather early reviews, then raise. Free tools get installs but
no signal; a low paid price gets you real buyers + reviews, which is what the algorithm ranks on.

## Key features (bullet list for the listing)
- One-click Editor window — `Tools ▸ Offset Forge`. No setup.
- Drop in any model or prefab; orbit / zoom / pan a live preview.
- Rotation X/Y/Z + Position X/Y/Z with live, exact readouts.
- Copy a paste-ready `Quaternion.Euler(...)` / `Vector3` to your clipboard.
- Save per-model offsets to a flat, version-control-friendly JSON.
- Optional 5°/15° snap.
- 100% Editor-only — zero runtime footprint, never touches your assets.
- Optional dependency-free runtime loader included.

## Long description (listing body)
> Paste as-is; it's written to read well on the store page.

**Stop typing rotation values blind.**

Every Unity dev knows the loop: you attach a sword to a hand, it points the wrong way, so you
type `(90, 0, 0)`... nope... `(0, 90, 0)`... press Play... close... type again. Offset Forge ends it.

Open one Editor window, drop in your model, and orbit it like you would in Blender. Dial Rotation
and Position until it sits exactly right, and read the precise offset — no play mode, no rebuilds,
no math. Copy it straight into your code or Inspector, or save it to a clean JSON file you can
drive at runtime.

**Built for the AI era.** Coding assistants can write your attach code, but they can't *see* a
model to know it needs −45° on Z. Offset Forge is the one human step that makes the automated ones
work: you set the offset once, by eye; your scripts (and your AI) use the exact numbers forever.

- Editor-only — adds nothing to your build.
- Never modifies your models or prefabs.
- Works with any model format Unity imports.
- Unity 2021.3 LTS through Unity 6.

Includes full documentation and an optional, dependency-free runtime loader. Tiny tool, real
hour-saver.

## Keywords / tags
offset, alignment, attachment, weapon, socket, transform, rotation, editor tool, utility,
prefab, bone, mount, prop, workflow, productivity

## Required assets to prepare (Publisher Portal)
- [ ] **Key image** 1950×1300 (the hero image — show the window with a model + the offset readout)
- [ ] **Icon** 160×160
- [ ] **Card image** 420×280
- [ ] **Screenshots** (3–5): the window in use; the JSON output; a before/after of a misaligned vs
      aligned weapon
- [ ] Optional **promo video** (30–60s screen capture of the 60-second workflow) — boosts conversion a lot

---

## First-time submission checklist (the pipeline)
1. [ ] Create a **Publisher account** at publisher.unity.com (one-time; needs tax/payout info).
2. [ ] Confirm the package imports cleanly into a **blank** project (no stray dependencies, no errors).
       Offset Forge is self-contained under `Assets/OffsetForge/` — verify nothing references your game.
3. [ ] Make sure it's **Editor-only**: the asmdef must list `Editor` as the only platform. (Reviewers
       check that a "tool" doesn't bloat runtime builds.)
4. [ ] Include `Documentation/Documentation.txt`, `README.md`, `CHANGELOG.md` (done) and a `LICENSE`
       reference. (Asset Store sells under Unity's standard EULA by default — you don't need your own
       unless you want extended terms.)
5. [ ] Set the **minimum Unity version** to 2021.3 (or whatever you test).
6. [ ] Export via **Assets ▸ Export Package** (or submit through the Publisher's Unity uploader tool),
       selecting ONLY the `Assets/OffsetForge/` folder.
7. [ ] Fill the listing with the copy above + the images.
8. [ ] Submit. **Review typically takes a few business days to ~2 weeks** for a first submission.
       They may bounce it for small things (missing docs, runtime leakage, screenshot quality) — that's
       normal; fix and resubmit.
9. [ ] After it's live: ask a few people to leave honest reviews — early reviews drive ranking.

## Roadmap (deliberately NOT in v1 — keep the first listing tiny)
Parked ideas, only if v1 finds buyers — each would be its OWN listing, not bundled:
- Socket/bone preview (align against a real hand bone).
- Mesh decimation / LOD companion tool.
- Tripo / import-convention auto-fixer.
> Decision (owner, 2026-06-23): ship ONE clean tool first. No suite. "Small and perfect" beats
> "two half-things" for a first sale.
