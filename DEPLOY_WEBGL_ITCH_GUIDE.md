# Deploy the Web Build to itch.io — Step-by-Step (for Samantha)

**Goal:** get a public, click-to-play web link for Defenders of the Realm, on any device, in ~30–60 min.
**Why itch.io (not Vercel first):** itch handles the 186 MB build natively with no server/header config;
Vercel likely rejects the single 181 MB data file. itch = fastest path to a shareable link.

**Prereq:** CLI has produced a fresh build (WO-190) at `Builds\WebGL\` from the current green tree, so the
link shows the fixed world, not the old void build. Confirm that's done first.

---

## Step 1 — Zip the build correctly (this is the part people get wrong)
The zip must have **`index.html` at the TOP LEVEL of the zip**, not inside a folder.

1. Open `C:\Users\Kayden-Laptop\Documents\defenders-unity\Builds\WebGL\`.
2. Select the **contents** — `index.html`, `Build`, `TemplateData`, `StreamingAssets` (and `vercel.json` is
   harmless to include or leave out). Do **not** select the `WebGL` folder itself.
3. Right-click → Send to → Compressed (zipped) folder. Name it `defenders-web.zip`.
   - ✅ Correct: opening the zip shows `index.html` immediately.
   - ❌ Wrong: opening the zip shows a single `WebGL/` folder — itch won't find the game. Re-zip from inside the folder.

## Step 2 — Make an itch.io account / project
1. Go to itch.io, sign in (or create a free account).
2. Top-right menu → **Upload new project** (or itch.io/game/new).
3. Title: `Defenders of the Realm`. Set a project URL.

## Step 3 — Configure it as a playable web build
1. **Kind of project:** choose **HTML**.
2. Under **Uploads** → **Upload files** → select `defenders-web.zip`.
3. After it uploads, tick the checkbox **"This file will be played in the browser."**
4. **Embed options / Viewport dimensions:** set a size — e.g. **960 × 600** (or 1280 × 720). Tick
   **"Fullscreen button"** and **"Mobile friendly"** (so it works on phones).
5. **Frame options:** "Embed in page" with "Manually set size" is the safe default.

## Step 4 — Visibility + save
1. Set **Visibility & access** to **Draft** (only you) or **Restricted** (you + people with the link) while
   testing — flip to **Public** when you're happy.
2. Click **Save**.
3. Click **View page** → press the **Run game** button. It downloads ~186 MB on first load (be patient on
   the first launch), then boots into the title/village.

## Step 5 — First-load sanity check (do this before sharing widely)
- Does it load past the Unity loading bar to the game? (If it hangs, note the browser console error.)
- Terrain renders — world is there, not a black void.
- Keyboard/mouse work; on mobile, touch works.
- Audio starts after first click/tap (browsers block autoplay until a gesture — expected).

Once it plays, the itch page URL is your shareable link. Send the **Restricted** link to testers, or go Public.

---

## Known limits of this link (all expected, not bugs)
- **Big first download (~186 MB)** — slow on the first cold load; cached after. Slimming this is a later task
  (texture compression / Addressables streaming, ~3–6 days).
- **No in-app purchases** (crypto/Stripe are off on web by design).
- **No cross-device save / leaderboards** (backend features, deferred).
- Build is offline-first, so single-player play works fully without any of the above.

## If you'd rather use Vercel later (the long-term home)
The repo already has `vercel.json` + `.vercelignore` configured. The catch: the 181 MB `WebGL.data.br` likely
exceeds Vercel's per-file limit. If it rejects, the fix is to host the `Build/` folder on object storage
(Cloudflare R2 / S3 + CDN) and repoint the loader — a ~0.5–1 day job. itch first, Vercel when it matters.
