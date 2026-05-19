# Core EditMode test fix — `_state` is null in `GameStateService.Reset()`

**Status:** Root-cause analysis + fix for the Core-module EditMode failure
(43 passed / 16 failed → 59 passed).

---

## 1. Symptom

A Core EditMode run reported **43 passed / 16 failed**:

- The **43 passing** tests live in `SaveMigratorTest` + `SaveSchemaValidateTest`.
  They exercise the *static* classes `SaveMigrator` / `SaveSchema` — they never
  build a `GameStateService` instance.
- **All 16 failing** tests live in `SaveLoadRoundTripTest` + `ResetCarveOutTest`.
  They are exactly the tests that call `TestSupport.SpawnService`.
- Every failure is the Unity Test Framework's
  `Unhandled log message: '[Exception] NullReferenceException...'` — the framework
  fails a test when an unexpected exception is *logged*.
- The stack trace points into `GameStateService.Reset()` at
  `s.Pets = new List<PetData>();` (just after `var s = _state;`) — i.e. the
  private `[SerializeField] private GameState _state` field is **null** when
  `Reset()` runs.

The split is the tell: *every test that builds a service fails; every test that
does not, passes.* The bug is in service construction, not in the save logic.

---

## 2. Why the obvious explanation is wrong

`TestSupport.SpawnService` does, in order:

1. `ResetSingleton()` — clears the static `_instance`.
2. `new GameObject(...)` then `go.SetActive(false)` — build it **inactive**.
3. `go.AddComponent<GameStateService>()`.
4. `state = ScriptableObject.CreateInstance<GameState>()`.
5. `SetPrivateField(service, "_state", state)` — reflection.
6. `SetPrivateField(service, "_loadOnAwake", false)` — reflection.
7. `go.SetActive(true)`.

The reflection in steps 5–6 *does* write the managed C# fields. So a naive read
says `_state` must be non-null. The custom `JsonConverter`, the `Application.isPlaying`
guards and the `ResetSingleton()` call were all tried and none of them fixed it —
because none of them addresses the actual mechanism.

Note also: a `GameState` `ScriptableObject.CreateInstance` cannot throw — none of
its field initializers (`List<>` literals, `SerializableDict` ctors, struct
`Starter`/`Empty` factories) run any code that can fail. So `_state` is genuinely
*assigned* in step 5; it only becomes null *later*.

---

## 3. Root cause — the inactive→active serialization sync clobbers the
reflection-injected `[SerializeField]` fields

`SpawnService` injects the private fields **while the GameObject is inactive**
(step 5–6) and only **then** activates it (step 7). That ordering is the bug.

`_state` and `_loadOnAwake` are both `[SerializeField]` fields. A Unity
`MonoBehaviour` is a split object: a native (C++) object that owns the
**serialized** field data, plus the managed (C#) object the reflection writes to.
When `AddComponent` runs on the inactive GameObject, the native side is created
with the serialized fields at their *defaults* — `_state` = a null object
reference, `_loadOnAwake` = `true` (its field initializer).

Reflection in steps 5–6 writes **only the managed C# fields**. It does not touch
the native serialized data.

When `go.SetActive(true)` runs (step 7), Unity transitions the component through
its activation / "awake from load" path, which performs a **serialization sync:
the native serialized field data is deserialized back onto the managed object.**
That sync **overwrites** the reflection-set managed values with the native
defaults:

- `_state` → back to **null**.
- `_loadOnAwake` → back to **true**.

So by the time the test calls `_service.Reset()` (or `Load()`), `_state` is null
again — `var s = _state; s.Pets = …` throws `NullReferenceException`, the Test
Framework sees the logged exception, and the test fails.

This also explains why `Awake`-related fixes did nothing. EditMode tests run in
**edit mode**, where Unity does **not** invoke `Awake`/`OnEnable` for a normal
(non-`[ExecuteAlways]`) `MonoBehaviour` — so the `if (_state == null) _state =
CreateInstance<GameState>()` line in `Awake` never runs to "rescue" the field.
The harness was silently depending on a serialization ordering that does not hold:
**managed-only writes to `[SerializeField]` fields made while a GameObject is
inactive do not survive the activation serialization sync.**

The 43 static-class tests pass because they never build a `GameStateService`,
so they never hit the inactive→active sync.

---

## 4. The fix

Production code (`GameStateService` / `GameState`) is **correct** — in a real
build `Awake` runs, sees `_state == null`, and self-heals; and a real scene asset
serializes `_state` natively so the sync restores the *intended* value. Hardening
`Reset()`/`Load()` against a null `_state` would only paper over a harness that
constructs the service wrongly, and would mask genuine "service never initialised"
bugs in production. So the fix belongs in the **test harness**.

`TestSupport.SpawnService` is changed to inject the private fields **after**
`go.SetActive(true)` — i.e. after the activation serialization sync has run, so
nothing clobbers the managed writes afterwards. Because EditMode never calls
`Awake`, activating first and injecting second is safe: no service code runs
between activation and injection, so `_loadOnAwake` is never observed before it
is set, and nothing reads `_state` before it is injected.

The GameObject no longer needs to be built inactive at all (its only purpose was
to gate `Awake`, which EditMode never calls anyway), but it is kept active-from-
construction for clarity and the injection is done immediately after.

This makes all 16 service tests construct a service whose `_state` is a live,
non-null `GameState` SO and whose `_loadOnAwake` is `false` — so `Reset()`,
`Load()`, `Save()` and the mutators all operate on a real state object. Combined
with the still-passing 43 static tests, the run is **59 / 59**.
