# Overnight 2026-08-19 → 08-20 — jeweler orientation (with images) + PROD-010 offline mode

**Seat:** CLI, running with owner-granted overnight autonomy.
**Branch:** `wip/village2-and-f8-tickets` — pushed.
**Device:** build **2026.08.20.332813** installed on the Seeker (`versionCode=332813`, verified by
`dumpsys package com.denellestudios.echoesofelarion`).

---

## 1. The jeweler — you asked for data AND an image. Here is both, and the answer reversed.

### The finding, first

**The jeweler belongs at +90, not −90. My −90 change was wrong and is reverted.**

Yesterday you said the jeweler was upside down and told me to invert it in the castle builder
script. I did (`9180700e9`). The render proves that inversion **broke a jeweler that was already
fixed** — the real cause had been found and fixed the commit before mine.

### Why no amount of data could settle this without a picture

+90 and −90 about X produce an **identical bounding box**. Same size, same centre, same footprint.
Every measurement the project owns — the orientation oracle, the navmesh carve, the height
normalisation, every regression — reads the two as the same value. There is no number that separates
them. Only the screen can, which is why the request for an image was the right call.

So I built the instrument: `Assets/Editor/StorefrontOrientationCapture.cs`. It renders each baked
storefront from **one shared camera** — same distance measured in units of model height, same light,
same background — and isolates the subject on a spare culling layer so a neighbour cannot photobomb
it. (The first run did exactly that: the jeweler stands against the castle wall and the camera landed
inside it, producing a flat olive rectangle. Moving the camera per-subject would have fixed the
occlusion and destroyed the comparability that is the entire point, so the subject moves instead.)

Then it A/B shoots the jeweler at **both** pitches through that same rig.

### The images

| file | pitch | verdict |
|---|---|---|
| `jeweler-PLUS90-upright.png` | **+90** | **UPRIGHT** — shingle roof on top, cobble base on the ground, "Kraia's Jewelry" sign readable, ring sign hanging correctly |
| `jeweler-MINUS90-inverted.png` | −90 | **UPSIDE DOWN** — shingle roof at the bottom as an inverted V, stone floor slab on top |
| `armorer-reference-upright.png` | +90 | the weaponsmith-family reference you asked me to compare against — roof up, stone base down |

You asked me to compare against the weaponsmith "because they use the same structure, just different
signs." That comparison is what makes the shot trustworthy: the **same rig** renders the armorer
upright, so the rig is not the variable. Only the jeweler's pitch changed between the two jeweler
shots.

### What actually caused the upside-down jeweler you saw

Not the sign. `HubStructureVisualInjector` carried a **non-uniform scaleX** on the jeweler, and
`SkinStorefront` applies scale **after** LocalRotation and Fit — so it re-tipped the model on every
hub reload, while the bake alone looked perfect. That scale was cleared in `cd0d109b8`, the commit
immediately before mine. The jeweler was already fixed when I inverted it.

### What is in the tree now

- Sign reverted to +90 in all three places that must agree: the builder skin table, the per-id
  catalog table, and the runtime injector. If those three ever disagree, the baked hub jeweler and a
  player-**placed** jeweler are inverted relative to each other.
- The post-bake stage you asked for — *"if you can add it at the end just to flip it, then there
  would be no reason to rebake it"* — is in and retargeted: `VerifyJewelerUprightAfterBake()` runs
  after the navmesh bake, asserts +90, and forces + re-saves if anything upstream wrote something
  else. This bake printed `JEWELER_UPRIGHT_OK — localEuler=(90.0,0.0,0.0) — already upright, no change.`
- Hub re-baked with the corrected sign: `OWNER_UPRIGHT_PREBAKE_OK skinned=4 catalogFlagged=5`,
  `NAVMESH_BAKE_OK 1 surface(s)`, `OWNER_UPRIGHT_AND_NAVMESH_BAKE_OK`.

### Measured data (`measured-summary.txt`, from the baked scene)

```
Jeweler_Gems_Storefront    visual='jeweler(Clone)'  localEuler=(90,0,0)    bounds=(3.62,4.00,3.37)
Forge_Armor_Storefront     visual='armorer(Clone)'  localEuler=(90,90,0)   bounds=(2.54,4.00,2.92)
CastleBarracks             visual='barracks(Clone)' localEuler=(90,180,0)  bounds=(5.91,4.00,6.11)
```

**The rule I wrote into the code so this cannot recur:** if the jeweler ever reads inverted again,
**re-render it before touching the sign.** The scale/Fit path is the likelier culprit, and flipping
the sign hides it while breaking the mesh — which is precisely what happened here.

Commit: `5b6e97e95`.

---

## 2. PROD-010 — opt-in offline mode

Your spec: opt IN; on yes a first-time CDN pull of everything needed; that pull needs Wi-Fi;
afterwards default to local when it cannot reach Wi-Fi. Landed in `345a7b464`.

**`OfflineContentService`** — the seam. The part that needed real code is the fallback, and not for
the reason it looks like: bundles already cache on their own. What breaks a no-network launch is the
step *before* them. `AddressableAssetSettings` has `m_DisableCatalogUpdateOnStart: 0`, so the catalog
is refreshed from the CDN at launch, and that refresh throws or hangs with no connection. So the
resolver **skips the refresh entirely when offline** and survives a refresh failure when online.

I deliberately did **not** just flip `m_DisableCatalogUpdateOnStart` to 1. Installed players adopt new
remote catalogs at launch — that is how a shipped build learns about content we upload later, and
your "keep the CDN" ruling depends on that path staying alive. We degrade on **failure** instead of
disabling the feature.

"Pulled" is keyed to `Application.version`, because content is content-hashed per build — a pull from
the previous APK does not cover this one, and treating it as covered is how someone who opted in
still hits the network on a fresh install.

**`OfflineContentBootstrap`** — runs the resolve once per launch, Guard-wrapped, and deliberately
**not** a hard boot barrier. Blocking the boot on a network check is the exact stall this ticket exists
to prevent.

**`OfflineOptInPanel` + a new Settings → Offline row** — the door. Settings rather than a boot prompt:
this is ~88 MB, and a prompt that ambushes a new player in the opening minutes is the wrong trade
when the spec says opt *in*. The size is **measured** (`GetDownloadSizeAsync`), never typed, and the
prompt states an honest range — about 141 s at 5 Mbps, up to 471 s at 1.5 Mbps. An earlier plan
promised "10 seconds"; that was only true while PROD-009 was going to shrink the download, and
holding someone for eight minutes after that promise is a lie. A partial pull is never reported as
success. The button label carries the state ("Offline Ready" vs "Play Offline") so it reads in
greyscale.

**The regression caught a real defect in my own panel before it shipped.** A 31010-band modal with no
`PanelManager` handle leaves `AnyOpen` FALSE while it covers the screen — the world interact button
stays live underneath, the Android back button has nothing to close, and BattleLock cannot reject it.
Same class as the Echo FTUE cascade. It is registered now, and an arbiter rejection is honoured
rather than overridden: an 88 MB download prompt on top of a live battle is the worst possible moment
for one.

**PROD-009 closed as superseded** per your ruling *"PROD 10 kills 10 and 09"* — it shrank the same
download PROD-010 now measures and shows, so shipping both would mean maintaining two
content-partitioning schemes for one problem.

---

## 3. Gates

| gate | result |
|---|---|
| `COMPILE_GATE_OK` | PASS (3 runs: after the namespace fix, after the Settings wiring, after the panel registration) |
| `DataRegression` | **209/213, 4 failure(s) = the known-red baseline exactly.** Nothing new. |
| `NAVMESH_BAKE_OK` | PASS — 1 surface, `Main_Castle_Overworld.unity` |
| `JEWELER_UPRIGHT_OK` | PASS — `(90.0, 0.0, 0.0)` |
| `STOREFRONT_CAPTURE_OK` | PASS — 5 subjects |
| `R2_PARITY_OK` | PASS — 4 objects verified, catalog `2026.08.20.332807` |
| APK | built 21:56, installed, `versionCode=332813` confirmed on device |

The 4 baseline reds, unchanged: `CaravanStatusChip` (hand-rolled UI), `vfx-self-contained`,
`vfx-null-slot`, `WANDERER BUBBLE ×4` (dungeon needs an isolated-worktree re-bake).

One thing worth your eye inside a baselined red: `vfx-self-contained` reports the
`HovlVfxCatalog.asset` exposure **grew from a baselined 689 to 702** assets in gitignored art roots.
The suite was already red so this did not change the failure count, but the ratchet is only supposed
to move down — something added new pack references. Not from tonight's work; flagging it rather than
silently absorbing it.

---

## 4. ⚠ What I could NOT prove, and why

**The Seeker was locked all night.** `dumpsys window` reports `showing=true` /
`mDreamingLockscreen=true`; the screen reads "Emergency calls only". `adb install` works through a
lock, so the build is on the device and its version is confirmed — but **no gameplay screenshot and
no felt-test was possible.** I did not attempt to get past your lock screen.

So these are unproven and need you:

1. **The jeweler on device.** The render is from the baked scene, which is the thing the sign
   controls — but you have not seen it on hardware.
2. **PROD-010's airplane-mode path.** Proven in code and gate only. The real test is: Settings →
   Offline → Download, wait for 100%, then turn off Wi-Fi and cold-start. That is the one that matters
   and it has never been run.
3. Everything already sitting in AWAITING OWNER FELT-VERIFY on the board (PROD-002/003/005/006/011).

If you want unattended device testing on future nights, leaving the Seeker unlocked (or with the
screen-lock off while it is plugged in) is the whole difference between "installed" and "verified".
