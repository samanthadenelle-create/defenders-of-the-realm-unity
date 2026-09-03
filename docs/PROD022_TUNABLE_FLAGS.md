# PROD-022 remote tunables — the flags you flip instead of rebuilding

**Owner ruling 2026-09-02, verbatim:**

> *"make the testing as robust as possible with as many solutions as possible... all we really have to
> do is just flip a flag and possibly redeploy"*

A WebGL rebuild costs about **thirty minutes**. PROD-022 is a P0 crash loop that reproduces inside
**Pi Browser on the owner's iPhone and nowhere else** — desktop Chrome ran the identical build for
62 minutes. So every candidate mitigation ships in **one** build, each behind its **own independent
flag**, and the bisect becomes flag flips against the database instead of half an hour per hypothesis.

---

## ⛔ The invariant that outranks everything else here

> **No row, no network, no server, no parse ⇒ TODAY'S BEHAVIOUR, EXACTLY.**

Every default in the table below is the value the shipping code hardcoded before PROD-022 touched it.
A player who is offline, whose fetch times out, who gets a 404, or who receives malformed JSON resolves
**every** knob to that default. The remote read is an **override**, never a dependency, and the fetch
never blocks or delays boot.

**An empty `client_tunables` table is the correct resting state, and it is what ships.**

---

## The flags

| # | Key | Kind | Default (= today) | ON / raised does | Hypothesis it tests |
|---|---|---|---|---|---|
| 1 | `pi.eagerStructureWarm` | bool | `0` (OFF) | Pi Browser runs the **full desktop warm pass** — await Addressables init, harvest keys, `DownloadDependenciesAsync`, load and retain all 35 structure prefabs — instead of the on-demand policy. | That on-demand streaming is itself the problem and eager residency is healthier on this webview. WO-PROD-022 forbids re-enabling eager residency *without proof*; this knob is how the proof gets gathered rather than assumed. |
| 2 | `pi.awaitInitBeforeFirstLoad` | bool | `0` (OFF) | Pi awaits `Addressables.InitializeAsync` **and harvests every registered key before the first on-demand load**. Requests raised meanwhile are queued, never dropped. Residency policy untouched — this is *not* the eager warm. Also holds `WhenSettled` until init lands. | **PRIME SUSPECT.** Today the Pi branch returns from `Boot()` without ever awaiting init and without harvesting keys, so the *first on-demand request is the first thing that touches the catalog*, and `State` is `Degraded` from frame one — making `IsSettled` **true immediately**, so a `WhenSettled` retry can fire before a single location exists. That is the shape of the observed `model not found` storm. |
| 3 | `pi.disableRemoteStructureArt` | bool | `0` (OFF) | Pi issues **no remote structure-art request at all**. Callers keep the path they already take when an asset is not resident: the baked twin or the visible pending-art proxy. | **THE BIG HAMMER**, decisive in *both* directions. If the crash loop **stops**, asset streaming is implicated beyond argument. If it **continues**, streaming is exonerated and the cause is elsewhere — worth just as much. Trades visual fidelity for a clean signal, on purpose. |
| 4 | `assets.maxConcurrentRequests` | int | `0` (= today) | Caps residency fetches in flight. `0` = today: Pi serialises through its own latch, desktop is unbounded. `1`+ installs an explicit shared queue with that ceiling on **every** host. | That several simultaneous multi-MB bundle downloads plus decompression blow a memory ceiling **outside the managed heap** — exactly how the captured sessions look, dying with `mem=247MB` flat and no exception. |
| 5 | `pi.requestTimeoutSeconds` | int | `20` | The `UnityWebRequest` timeout installed by the Pi Addressables `WebRequestOverride`. | That 20 s is the wrong bound. **Unchanged at 20 deliberately** — the root is not proven and picking a new constant would bake in a guess. It ships tunable so the number moves on *data*. |
| 6 | `assets.maxRequestAttempts` | int | `3` | Async fetch attempts one address gets before it is retired for the launch. | That the retry budget is mis-sized: too high and the retry storm is itself the load that kills the tab; too low and one transient stall costs a building its art for the session. |
| 7 | `visuals.missLogCap` | int | `3` | Full resolve-miss `Fail` lines `VisualFactory` emits per address before announcing its cap and dropping to a throttled line. **It never goes silent.** | That trace *volume* is a contributor — the observed final seconds were nothing but four addresses cycling, and every line is a remote trace POST from the suspect device. |
| 8 | `trace.assetVerbosity` | int | `2` (= today) | Narration level for `[Flow:StructureAssets]` and `[Flow:VisualFactory]`. `2` = today (every Step, including the `-> Skin(...)` / `<- Skin(...)` pair). `1` = lifecycle Steps only. `0` = no Steps. | Same volume hypothesis as #7 but separable: silences the *success* narration while leaving every failure line intact, so a quiet session can be compared against a loud one. |
| 9 | `combat.drainReturnPct` | int | `100` (= today) | Percent of the damage a **drainshot** ability actually deals that returns to the caster as healing. `100` = today (heal == damage dealt). Applies to **every** drainshot — `mage.siphon`, `mage.drain`, `ranger.healing-shot` — because `HeroAbilities.HealFromDrain` is the single owner of the drain heal. Clamped to `0..1000` at that consumer. | **Not a PROD-022 hypothesis — this is a BALANCE lever (WO-1306).** The owner ruled the mage's first talent point must buy a castable that *sustains* ("the blm needs to get some healing , like drain to stay balanced (early)"), then that its strength must move without a rebuild ("be smart, dont make it need a code change, make it tweakable from a db call"). It rides this rail rather than growing a second configuration mechanism. What it tests is whether the mage's early sustain is at the right level — a question only felt-testing answers, which is exactly why it must move in seconds rather than in a thirty-minute rebuild. |

**⛔ `Warn` and `Fail` are emitted at every verbosity level and cannot be turned off.** CLAUDE.md §12
is binding: instrumentation is permanent, and a failure line that stops being logged turns a logged
failure back into a silent one. Only the success narration is dimmable.

### Independence

Every flag is **independently togglable and independently meaningful**. None implies or requires
another. Where a mitigation needs a value as well as an arm (#4), the value **is** the arm — a
sentinel default of `0` means "today", so there is no second coupled flag. #9 is the same shape from
the other end: its sentinel is `100`, because the quantity it scales is a percentage of what already
happens, and `100%` *is* today.

**#9 is a BALANCE knob, not a PROD-022 mitigation, and that is deliberate.** The owner ruled
(2026-09-02) that balance must move without a rebuild too. It rides this rail because the rail already
exists and works; building a parallel one for balance would be the second bespoke configuration
mechanism this page's last section explains we do not want. The invariant at the top of the page binds
it exactly as it binds the other eight.

---

## Precedence — how these compose with the existing `ff.*` flags

```
LOCAL PlayerPrefs "ff.tun.<key>"    (a human at the device)
    beats  REMOTE database row      (the owner at the console)
        beats  BUILD DEFAULT        (what this build hardcodes = today)
```

`FeatureFlags.Get` already resolves PlayerPrefs-over-default for the `ff.*` family. This system inserts
the **remote** layer between those two and leaves `ff.*` untouched. The prefix is `ff.tun.` and
**not** plain `ff.` on purpose: a tunable key and a `FeatureFlags` name must never be able to collide
in one PlayerPrefs namespace.

---

## Flipping one — worked example (the prime suspect)

```powershell
# See what is set. An absent row means the knob is at the build's default.
tools\command-centre.ps1 -Tunables

# Arm the prime suspect.
tools\command-centre.ps1 -Tunables -Key pi.awaitInitBeforeFirstLoad -Value 1

# ...owner felt-tests in Pi Browser, reports back...

# Put it back. CLEAR, not -Value 0 - see below.
tools\command-centre.ps1 -Tunables -Key pi.awaitInitBeforeFirstLoad -Clear
```

The balance knob works identically. To halve the mage's early drain sustain:

```powershell
tools\command-centre.ps1 -Tunables -Key combat.drainReturnPct -Value 50

# ...owner felt-tests, reports back...

# Back to the shipped 100%. CLEAR, not -Value 100.
tools\command-centre.ps1 -Tunables -Key combat.drainReturnPct -Clear
```

It is **not** a boot-time knob — `HeroAbilities.DrainReturnPct` is resolved at the moment each drain
lands — so it reaches a running client on the ordinary ~40 s path below, mid-session, with no relaunch.

Judge by the **marker on a fresh log** (`Builds\client-tunables.log`), never the exit code:
`TUNABLES_LIST_OK` / `TUNABLES_SET_OK` / `TUNABLES_CLEAR_OK` / `TUNABLES_FAIL`.

### ⭐ `-Clear` is not `-Value 0`

Clearing **removes the override**, so the knob answers whatever the build hardcodes — which for
`pi.requestTimeoutSeconds` is **20**, not 0. It is the one-word way back to today's behaviour, and it
is a separate verb for exactly that reason.

### From the phone

The same two writes exist on `POST /api/admin/ops` as `tunable.set` / `tunable.clear`, behind the same
two secrets (`ADMIN_DASH_KEY` + `ADMIN_OPS_KEY`) every other ops write uses. The Command Center console
HTML has **not** been extended with buttons for them — the PowerShell surface above is primary.

### How long until it reaches a client

About **40 seconds** for a running client: 10 s edge cache + the 30 s client poll.

**Boot-time knobs (#1, #2, and #3 as it affects the first request) are read at frame zero from the
on-device cache**, so they take effect on the **next launch** of a client that has fetched the value at
least once. Since PROD-022's symptom is that the app relaunches every 30–60 s, that is usually the very
next relaunch. See "The cache" below.

---

## Reading a session's configuration out of the trace

Every session prints its whole configuration on one line, at boot and again whenever a payload changes
it. **Quote this line in any felt-test report** — a run whose configuration cannot be reconstructed
afterwards proves nothing.

Default build, nothing set (a `Step` line):

```
[Flow:Tunables] CONFIG (StructureContentWarmer.Boot): generation=1 tableProvenance=default rows=0 | pi.eagerStructureWarm=OFF  pi.awaitInitBeforeFirstLoad=OFF  pi.disableRemoteStructureArt=OFF  assets.maxConcurrentRequests=0  pi.requestTimeoutSeconds=20  assets.maxRequestAttempts=3  visuals.missLogCap=3  trace.assetVerbosity=2  combat.drainReturnPct=100 || EVERY knob is at its shipping default - this session is TODAY'S BEHAVIOUR, unchanged. Nothing was overridden by the database or by PlayerPrefs.
```

With the prime suspect armed (a `Warn` line — an overridden build is not the shipping build and must
not read as ordinary narration):

```
[Flow:Tunables] CONFIG (payload accepted, rows=1 unknown=0): generation=2 tableProvenance=remote rows=1 | pi.eagerStructureWarm=OFF  pi.awaitInitBeforeFirstLoad=ON(OVERRIDDEN, default OFF)  ... || 1 knob(s) are OVERRIDDEN. This session is NOT the shipping default configuration - quote this line in any felt-test report, because it is the only record of what produced the run.
```

Each knob additionally traces its own provenance once per distinct value:

```
[Flow:Tunables] KNOB pi.requestTimeoutSeconds = 20  provenance=default  (shipping default 20, generation=1). No database row and no local override - this is TODAY'S BEHAVIOUR, unchanged.
[Flow:Tunables] KNOB pi.awaitInitBeforeFirstLoad = ON  provenance=remote  (shipping default OFF, generation=2). This is an OVERRIDE of the shipping default.
```

`provenance` is one of `default` | `remote` | `remote-cached` | `local-playerprefs`. A reader never has
to infer whether a value came from the database.

---

## The cache — and why this diverges from `MaintenanceService`

`MaintenanceService` deliberately has **no** cache: a stale kill switch is a safety question, and the
owner ruled that an offline player falls back to "everything is open". **That ruling is about seals and
does not transfer here.**

The knobs that matter most to PROD-022 are read **during boot** (the Pi Addressables policy is decided
in `StructureContentWarmer.Boot`, at `AfterSceneLoad`). A value that only arrived after a network round
trip would be a launch too late, on every launch, forever. So `RemoteTunablesService` mirrors the last
accepted payload into `PlayerPrefs["tunables.cache.v1"]` and reads it back at **`BeforeSceneLoad`** —
which Unity guarantees runs before every `AfterSceneLoad` hook.

Safety properties of that cache, all of them load-bearing:

- It can only ever hold values that **came from** the database.
- A fresh payload **replaces it wholesale**, so it cannot resurrect a knob the owner cleared.
- A **404** (endpoint not deployed) **clears** it — an absent feature holds no knob.
- A corrupt cache is rejected by the same `Guard`-wrapped parse as a live payload, **discarded**, and
  every knob falls to its shipping default.

---

## Where this lives

| Layer | File |
|---|---|
| Registry / defaults / parse (**source of truth for defaults**) | `Assets/_Modules/Core/Ops/RemoteTunables.cs` |
| Transport, poll, cache | `Assets/_Modules/Core/Ops/RemoteTunablesService.cs` |
| Knobs 1–6, 8 consumed | `Assets/_Modules/Core/Addressables/StructureContentWarmer.cs` |
| Knobs 7–8 consumed | `Assets/_Modules/Village/VisualFactory.cs` |
| Knob 9 consumed | `Assets/_Modules/Village/Hero/HeroAbilities.cs` — `DrainReturnPct` / `HealFromDrain` |
| Table | `api/schema.sql` — `client_tunables` |
| Server read + validation + writer | `api/_lib/tunables.js` |
| Public GET | `api/client-tunables.js` |
| Phone write actions | `api/_lib/ops.js`, `api/admin/ops.js` (`tunable.set` / `tunable.clear`) |
| Operator CLI | `tools/client-tunables.mjs` |
| Operator surface | `tools/command-centre.ps1 -Tunables` |
| **Oracle** | `Assets/Editor/Regression/RemoteTunablesDefaultsRegression.cs` — `[tunable-defaults]`, registered in `DataRegression.RunAll` |

**If you change a default or add a knob, change `RemoteTunables.Registry`, the `TUNABLE_KEYS`
allowlist in `api/_lib/tunables.js`, this document, and `ExpectedDefaults` in
`RemoteTunablesDefaultsRegression.cs` in the same commit** — CLAUDE.md §15. You will not forget:
the `[tunable-defaults]` oracle pins all four against each other and reds naming which two disagree.

### What the oracle pins

The invariant at the top of this page — **no row / unreachable backend ⇒ today's behaviour, byte for
byte** — is the one every offline player depends on, and a break in it is *invisible*: nothing
crashes, the build simply stops behaving the way this page says it does. So it is asserted, not
assumed. `[tunable-defaults]` drives **seven** failure paths (no table · `readOk:false` · malformed
JSON · empty body · corrupt device cache · values the server would refuse · garbage after a good
payload) and re-asserts **all nine knobs on each one**. It also proves the real consumers still
answer `3`, `20` and `100` with no table, that all three clamps hold, that the key domain matches
across the three sources, that the fetch still cannot block boot, and that no `Warn`/`Fail` was ever routed
through the verbosity knob (CLAUDE.md §12). Zero network, zero database.

### Why a new table and not `maintenance_toggles`

Asked and answered. `maintenance_toggles` is PK-`CHECK`-constrained to exactly six area ids; its shape
is boolean + operator prose; and its six-id domain is source-linted three ways (the `MaintenanceArea`
enum, the `AREAS` array, the SQL `CHECK`) by `MaintenanceTogglesRegression`. Putting knobs there would
force that `CHECK` open — defeating the lint that exists to keep the six honest — and would overload
`closed`/`message` as a value field. Different domain, different shape, different failure semantics.

The **pattern** is reused end to end (public unauthenticated GET, 10 s edge cache, fail-to-safe-ground-
state, writes only through the two-key admin endpoint and one operator CLI, one `command-centre` switch,
marker-judged). Only the table is new. There is no second bespoke configuration mechanism.
