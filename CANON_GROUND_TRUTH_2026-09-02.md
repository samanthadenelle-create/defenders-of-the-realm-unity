# CANON GROUND TRUTH — 2026-09-02 (the night the docs stopped lying)

**This supersedes `CANON_GROUND_TRUTH_2026-08-23.md`.** Keep exactly ONE current; supersede by date.
Every session and every agent checks docs against THIS file (CLAUDE.md §15).

> ⚠ **The 08-23 anchor was never wired into the loader.** `SESSION_CANON_LOADER.md` and
> `docs/HANDOVER.md` both still pointed at **08-21** while 08-23 sat on disk unreferenced. That is the
> third time this pointer has gone stale. **Re-stamp the loader banner AND the HANDOVER block in the
> SAME change as any new anchor** — an anchor nothing points at is not an anchor.

---

## THE HEADLINE — two owner rulings that change how every seat works

### 1. The Android APK is the priority. Pi is PARKED, not cancelled.

> *"we have spent most of today triaging and trying to get Pi to work, but have made almost no
> progress"* → *"so we need to shift back to the apk. thats the real vision so that needs to be the
> priority"*

**The evidence behind it, so nobody re-litigates:** of the 27 commits made before that ruling on
2026-09-02, exactly **ONE** was gameplay (`d45608080`, the wave-clear toast) — and it landed nine
minutes AFTER that morning's APK was cut, so it was not even in the artifact. Everything else was Pi,
WebGL, or docs about Pi. A full day of triage produced no player-facing progress.

PROD-022 drops from P0-active to a **quiet read-only triage lane**. The Pi/WebGL ticket cluster
carries PARKED banners. Pi resumes on her word.

### 2. A balance value is a TUNABLE, not a constant. The default answer is YES.

> *"be smart, dont make it need a code change, make it tweakable from a db call"* — followed by
> ***"i have been screaming this for months."***

**Read the second sentence as the actual defect.** The idea was never rejected; it kept being agreed
to in conversation and never written to disk. That is the failure this canon named on 08-23: *a
ruling recorded but not applied is indistinguishable from no ruling.* It is now standing canon in
`KEY_FACTS.md`, so no seat needs telling again.

---

## ⛔ THE DISCOVERY THAT EXPLAINS THE "MONTHS OF SCREAMING", AND IT IS NOT WHAT ANYONE ASSUMED

> ## "DATA-DRIVEN" IN THIS REPO DOES **NOT** MEAN "TUNABLE WITHOUT A REBUILD".

`LocalJsonCatalogSource` resolves `Resources.Load<TextAsset>` **FIRST on every platform**, and
`Assets/Resources/` is **compiled into the player**. Therefore:

- Editing any of the 71 canonical JSONs still costs a **full build** (~10 min APK / ~30 min WebGL).
- Editing the `StreamingAssets` twin **changes nothing at all**.
- **Five canonical files advertise in their own authoring notes that the owner "retunes with NO
  recompile."** Literally true (no C# recompiles) and false in the only sense she experiences.

**Every past attempt to fix this by moving numbers into JSON was working on the wrong axis.** That is
why it never took, through repeated asking, for months.

**The fix already existed and was assigned nowhere.** `CanonicalJson.Source` is a settable
`ICatalogSource`, documented in its own comments as a one-line swap. WO-1331 connected it —
**flag-gated OFF** (`FeatureFlags.RemoteCatalogs`, key `catalogremote`, `defaultOn:false`), scoped to
five allowlisted catalogs, with fall-through proven on every failure mode. Suite: `[catalog-seam]`.

---

## STATE — read every number from its source, never from this page

- **Branch:** `feat/synty-art-retheme`. **NOT PUSHED.** Count with
  `git rev-list --count origin/<branch>..HEAD`. ⚠ Every doc naming `wip/village2-and-f8-tickets` as
  the live branch is STALE.
- **HEAD / commits-ahead:** `git log -1` / `git rev-list`.
- **Save schema:** read `SaveSchema.CurrentVersion` (`Assets/_Modules/Core/State/SaveSchema.cs`).
  ⚠ `CLAUDE.md` §8, `docs/MASTER_CATALOG.md` and `docs/ARCHITECTURE.md` all still say **v38**; the
  const has moved on. **Never quote the number — quote the const.**
- **Gates, off fresh logs, markers asserted:** `COMPILE_GATE_OK` (`Builds/gate-batch4.log`) ·
  `REGRESSION_OK <n>/<n> suites` (`Builds/reg-batch4.log`) — 0 red, 0 skipped, 0 `error CS`, 0 hollow
  passes · `CATALOG_FALLBACK_GEN_OK` · `SCHEMA_PARITY_OK` → `APK_OK` → `R2_PARITY_OK
  targets=Android,StandaloneWindows64,WebGL` → `APK_DONE`.
- **Device:** a Seeker APK is installed and verified by reading `versionName` **off the device**, not
  from the installer's own say-so.
- **Board:** derived — `python tools/board_build.py` → `BOARD_CHECK_OK`.
- **WO numbers:** the `CLI_LANES_WO_NUMBERS.md` banner, sole authority.

---

## THE TUNABLES RAIL — the shape to know before touching any balance value

Contract: **`docs/PROD022_TUNABLE_FLAGS.md`**. Operator surface:
`tools\command-centre.ps1 -Tunables`, and now a **Balance tab in the Command Center** (WO-1328),
grouped Skills / Tiers / Spells / Misc.

```
LOCAL PlayerPrefs "ff.tun.<key>"   beats   REMOTE database row   beats   BUILD DEFAULT
```

> ### ⛔ THE INVARIANT THAT OUTRANKS THE FEATURE
> **No row, no network, no server, no parse ⇒ TODAY'S BEHAVIOUR, EXACTLY.**
> The remote read is an OVERRIDE, never a dependency, and never blocks or delays boot. An empty
> `client_tunables` table is the correct resting state and is what ships.

**⚠ IT IS SIX SOURCES NOW, NOT FOUR.** That paragraph said *four* all evening, and the Balance tab
added two more mid-evening. A seat following the four-source rule literally would have shipped a knob
**the owner's console cannot see** — a lever that exists, works, and is invisible to the one person
who needs it. Read the enumerated list in the rail doc; **do not restate the count here.** Two of the
six are human-written, and those are the two that rot.

**⭐ `Clear` is not `set 0`.** Clearing removes the override so the knob answers the build default;
zero may mean something entirely different (`pi.requestTimeoutSeconds` defaults to 20). It is the
easiest way to break a live game from a phone.

**⛔ SERVER-AUTHORITATIVE VALUES ARE PERMANENTLY OFF THIS RAIL** — prices, entitlements, grants,
base-unit amounts, token decimals, quote TTL (`api/_lib/purchase-catalog.js`). The client does no
pricing arithmetic by design and `/verify` runs AFTER settlement, so a client-side override there is
money gone with nothing granted. The Balance tab enforces this on the **shape** of the manifest: a
knob key matching `price|sku|entitle|grant|usd|payout|refund|cost|purchase|wallet` FAILS the suite.

---

## OWNER RULINGS, 2026-09-02 — implement these, do not re-litigate them

| Ruling | Detail |
|---|---|
| **Drain returns 60%** | *"keep drain at 60% for now"*. A **deliberate departure** from "default == today's behaviour": it is a ruled balance value, not a bug fix. Flagged in four places so nobody "corrects" it back to 100. |
| **Sustain buys time, it does not win fights** | *"drain should help stave off not run the show"*. Governs ALL sustain. A player using drain well survives a fight they would otherwise lose; they must never stand still and win by attrition. |
| **"Syphon Essence"** | Her spelling, a **Y**. Replaces both display names. The id `mage.siphon` is a LIVE SAVE KEY and was NOT renamed. |
| **Flat coat for the wolf** | The zero-art-work option, which also brings all three build targets into agreement. |
| **Alduin and Aldwin stand as authored** | See below. No rename. |
| **The marquee fire spell belongs to the MAGE** | Blocked on `RegistryTarget` being hardcoded `"knight"` (WO-1329). |
| **Balance editing belongs in the Command Center** | *"so you dont need to be a rocket scientist"* — a JSON-driven UI, grouped Skills/Tiers/Spells/Misc. |

---

## ⛔ FIVE FACTS THAT COST REAL TIME TONIGHT. Read them before you repeat them.

1. **`Assets/Spells Pack/` is GITIGNORED** (`.gitignore:430`). A prefab edit there cannot be
   committed or reviewed, never reaches another machine, and **dies at the next re-import — while
   still changing what the local build produces.** Any fix that goes in that way looks done and
   travels nowhere. Fix at the spawn owner (`VFXManager`) instead.
2. **`Assets/Blink`: no VFX, but a LARGE ICON LIBRARY that the game already uses.**
   A prefab census found 777 character/armour/weapon/UI prefabs and **zero VFX** (two of its four
   bundles are README files of unclaimed Asset Store links), so the spells are elsewhere — the real
   VFX warehouses are Lana Studio, Spells Pack, Hovl, Mirza Beig and `Resources/VFX`.
   > ### ⚠ BUT THAT CENSUS WAS THE WRONG QUESTION, and it put a misleading line in canon.
   > Owner, 2026-09-02: *"look for icons in blink. theres 4000"*. She is right, and a **PREFAB** count
   > is structurally blind to an icon library because **icons are TEXTURES, not prefabs**.
   > Measured: **2,379 image files**, of which **608 sit under `Assets/Blink/Art/Icons`**, organised
   > by class and archetype (`Assassin`, `Elementalist`, `HolyDarkness`, `Symbiose`, `Warrior`), plus
   > 70 in `Icons_Obsidian` and 17 in `Free_Blink_Icons`. Each is `spriteMode: 1` — one icon per file,
   > nothing to slice.
   > ⭐ **AND THE GAME IS ALREADY WIRED TO IT.** `concept-icons.json` resolves ids like `Arcanist17`,
   > `Priest5`, `Rogue4`, `Deathknight11` — and Blink carries exactly 23 icons under each of those
   > names. So the three abilities logged as OWNER-TAG DEBT (`mage.siphon`, `mage.wither`,
   > `knight.ironblood`) are **not blocked on tooling**. They are blocked on the owner naming an icon
   > from a library that is already plumbed in.
   > **The lesson is the repo's own:** search by the TOKEN, never by the asset type you expect. A
   > census that counts one kind of thing will confidently report the absence of another.
3. **`Core/State/ServerConfig.cs` is a DEAD SECOND MECHANISM.** Eleven fields fully wired
   client-side, absorbed at `GameStateService.cs` and consumed at `WaveManager.cs` — but
   `api/game/load.js` has **never emitted a `config` key**, so none of it has ever been settable.
   Retire it or fold its keys onto the rail. **Do NOT build the missing server half** — that is a
   second configuration mechanism, the disease this repo keeps paying for.
4. **`heart.json` and `towers.json` have NO RUNTIME READER AT ALL** — only a regression asserting
   they are *served*. Shipped Heart is **100 HP with 2 HP/s regen**; the authored files say **160 HP
   with zero regen**. The game has been ignoring reviewed, authored balance data.
   > ### ⛔ OWNER RULING 2026-09-02: **LEAVE THE HEART ALONE FOR NOW.**
   > *"and leave the heart alone for now"*. The shipped **100 HP with 2 HP/s regen STANDS.** Do NOT
   > wire these files, and do NOT "correct" the live values toward the authored ones. Wiring them
   > would change live balance in two directions at once — a 60% HP increase AND the loss of regen —
   > on a build real players are on. **The divergence is now KNOWN AND RULED, not an oversight waiting
   > to be tidied.** A future seat that discovers `heart.json` is unread must read this line before
   > acting on it. Re-opening it is hers alone.
5. **`Alduin` and `Aldwin` are TWO SEPARATELY-AUTHORED CHARACTERS**, one letter apart, with ZERO
   crossover across 43 files: **Alduin the Mournful** (the Necromancer boss) and **Aldwin, the Ice
   Echo** (Echo #1, the founding companion wolf). Two suites exist to forbid conflating them, one of
   which says so in words. **The mistake has now been minted TWICE, in OPPOSITE directions**
   (WO-881 Alduin→Aldwin, corrected 2026-08-05; WO-1332 the reverse, closed by the owner with no
   action). The guard now pins the five previously-unguarded files per line, on both twins.

---

## THE WOLF — the premise was BACKWARDS, and only measurement showed it

The owner observed the wolf looked right in Pi and grey in the APK and exe. True, and the conclusion
inverts:

```
[parity]   wolf_color   present=Android,Windows   ABSENT=WebGL
PAYLOAD_TEXTURE_PARITY_FAIL 26 map(s) diverge across 3 payload(s)
```

Pi looks correct **because the coat map never shipped there** — `TripoMaterialFixer` falls back to
the species tint and paints a clean pale body. The exe and APK bind the real map (measured **mean
saturation 0.091** — genuinely a grey coat) and multiply the pale tint into it. **The healthy-looking
target was the degraded one.**

The ticket's own leading candidate died to a measurement: `wolf_color.png.meta` is `overridden: 0` on
**both** Android and Standalone, so **0 of the Android texture pass's 65 overrides apply**.

**Blast radius recorded, not fixed: 26 base-colour maps diverge.** 22 absent from WebGL (including
the flame-pup — the same failure one species over) and **FOUR absent from WINDOWS**: `Orc_Mage`,
`Orc_Tank`, `Orc_Warrior`, `TreeofLife` basecolors. The owner ruled on the wolf alone; **the other 25
stay open.** `tools/payload-texture-parity.py` proved itself RED before being trusted and named
`wolf_color` unprompted; it is deliberately NOT in the regression suite (red on the real tree today).

---

## LESSONS OF THE NIGHT — the ones worth carrying

**A measurement can be arithmetically correct and mean nothing.** "34 Alduin vs 30 Aldwin" was a real
count of a boundary that does not exist. Before normalising ANY name, establish that the variants
refer to the same entity.

**A brace check does not catch a missing semicolon.** Two seats merged prose into one string
concatenation and dropped the `+` between literals. Every brace check passed; the compiler did not.
Judge a `.cs` edit by the COMPILE GATE, never by a brace count.

**Weakening an oracle to fit the code is the forbidden move.** `[death]` went red because the
over-time engine's liveness test was an OPTIONAL argument — the shipped caller passed it, but the
oracle exercised what a future caller forgetting it produces: a DoT ticking a corpse. The fix made
forgetting **impossible** (a required constructor argument that throws on null), rather than teaching
the test to forget too.

**Verify a lane against git before dispatching it.** WO-1303/1307 were dispatched against work
already in HEAD (`95b75cf75`). The board said READY; the tree said done. **The tree wins.**

**A gate that reports success without proving it is worse than no gate** — it actively asserts the bug
is absent and work proceeds on that assertion. Two detectors were fixed tonight that fired on every
healthy case; one sweep found a SECOND offender the ticket never mentioned.

---

## OWED

1. **Owner felt-test** of the installed APK: the gate walk (animator), the talent tree layout, the Bag
   peek strip, a scene transition with music, and the founding tutorial.
2. **`vercel deploy --prod`** — the Command Center Balance tab is built and **dark** until then
   (`vercel.json` sets `git.deploymentEnabled:false`, so pushing does not deploy). Owner's call.
3. **Heart HP** — 100→160 and regen removed, or leave as shipped. Owner's call.
4. **Art tags:** `mage.siphon`, `mage.wither`, `knight.ironblood` all render the crossed-swords
   default and are logged as OWNER-TAG DEBT rather than silently wrong. Both WO-1330 VFX keys are
   held EMPTY by the same ruling.
5. **PROD-021** — candidate close; the multi-target verify defect is fixed, but nobody has proven the
   marker is WITHHELD when a target is missing. **A gate never seen red is not evidence.**
6. **WO-1333** — the tofu sweep is deliberately PART-DONE (66 files swept, 57 still carry an em dash,
   many of them correct leave-alones). It needs a fresh lane with a key-path classifier, not a blind
   finish.
7. **PROD-022** — the `pageshow persisted=` discriminator is committed but **NOT DEPLOYED**; it needs
   a WebGL build to reach the device. Parked until the owner asks.
