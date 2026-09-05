// =============================================================================
// BaseLayoutRoundTripRegression - a MULTI-RECORD BaseLayout survives the REAL
// save -> reload -> migrate cycle (WO-1361, headless DATA oracle).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression. Data + logic only - no scene, no
// PlacementGrid, no StructureFactory. Runs in the editor batchmode harness.
//
// WHY THIS EXISTS. The owner's device (2026-09-03) logged
//   [Flow:BaseLayout] Enter build mode CENSUS: live PlacedStructure(s) in
//   scene=9, loader.Loaded=9, persisted BaseLayout=17
// (emitter: Assets/_Modules/Village/BuildMode/BuildModeController.cs:513-523),
// and the archived device logs of 08-19/08-20 read "0 live / 0 loaded / 8
// persisted". The coverage audit of 2026-09-04 found that NO registered suite
// asserts a MULTI-record BaseLayout survives the full persistence cycle:
//   - CoreSaveRegression [G] (:479-520) and [H] (:582-612) round-trip ONE record;
//   - BuildEconomyRegression.CheckBaseLayoutReplay (:592-640) validates three
//     synthetic records against the registry + grid and never touches the save
//     path at all.
// This oracle sits at the persistence seam - the only places a BaseLayout LIST
// is REPLACED wholesale:
//   SaveMigrator.MigrateToV14        (SaveMigrator.cs:306-310)  null -> empty seed
//   SaveMigrator.MigrateToV36        (SaveMigrator.cs:630-661)  READS the list only
//   GameStateService.SeedBlankFoundingOnMissingSave (GameStateService.cs:436-447)
//   GameStateService.Snapshot        (GameStateService.cs:582)  copies the list out
//   GameStateService.ApplyPersisted  (GameStateService.cs:706)
//       `if (p.BaseLayout != null) s.BaseLayout = p.BaseLayout;`
//   GameStateService.ResetToNewGame  (GameStateService.cs:1250) `= new List<>()`
// If a record can vanish BETWEEN the save blob and the in-memory GameState, this
// suite names it by (itemId, cell). If this suite is green and the census still
// reads live << persisted, the loss is DOWNSTREAM of the save layer - in
// BaseLayoutLoader / StructureFactory / the scene - and the hunt moves there.
//
// CASES
//   1 [count-survives]        17 real-catalog records (12 distinct ids: walls,
//                             a gate, towers, collectors, storefronts, a mine),
//                             varied cell/yaw/level/worldY/wallMounted, through
//                             the REAL GameStateService.Save() (signed envelope
//                             via a swapped in-memory ISaveProvider) -> service
//                             death -> fresh service Awake/Load() -> migrate ->
//                             validate -> ApplyPersisted. Count == 17 and every
//                             (itemId, cell) pair is present with identical
//                             level/yaw/yawOffset/worldY/wallMounted. Also
//                             splits WRITE loss from READ loss by counting the
//                             records inside the stored blob itself.
//   2 [migrator-from-oldest]  the same 17 serialised at storeVersion 14 - the
//                             version that INTRODUCED baseLayout (SaveSchema.cs
//                             changelog "v14 - baseLayout (WO-108)";
//                             SaveMigrator.MigrateToV14 is the seed step) -
//                             run through SaveMigrator.MigrateForImport to
//                             CurrentVersion (pure) AND through the real Load()
//                             (envelope). 17 survive both.
//   3 [null-and-empty-distinct] an EMPTY list on disk loads as a non-null empty
//                             list; an ABSENT key (and a literal null) loads as
//                             the GameState initializer (non-null, empty),
//                             because ApplyPersisted only assigns when
//                             p.BaseLayout != null (GameStateService.cs:706) and
//                             MigrateForImport SKIPS the chain at CurrentVersion
//                             (SaveMigrator.cs:119-121) so MigrateToV14 never
//                             seeds it. Then the shallow-merge contract on a LIVE
//                             service: an absent/null field must NOT clobber a
//                             populated in-memory list (17 stay 17), while an
//                             empty list on disk IS authoritative (17 -> 0, by
//                             contract - the player has no structures).
//                             SEAM FINDING (recorded, asserted as current
//                             behaviour, not red): GameStateService.Load() on a
//                             provider with NO KEY or an EMPTY value calls
//                             SeedBlankFoundingOnMissingSave (:436-447), which
//                             assigns `_state.BaseLayout = new List<>()` onto
//                             WHATEVER _state is live. That is the one path
//                             where a MISSING save discards a populated
//                             in-memory list. Its only callers in shipped code
//                             are Awake (state is fresh anyway) and AutoPilot's
//                             explicit reload (DevTools/AutoPilotDriver.cs:5017,
//                             5063); no gameplay code calls Load() on a live
//                             service. It is WO-1250 sanctioned behaviour and
//                             is pinned here so a NEW caller of Load() that
//                             races a PlayerPrefs wipe is a known shape.
//   4 [new-game-clears]       ResetToNewGame (GameStateService.cs:1250) clears
//                             BaseLayout (sanctioned, asserted behaviourally),
//                             AND a comment-stripped, string-stripped source
//                             lint over GameStateService.cs counts every
//                             `BaseLayout = new` and names its enclosing
//                             method. Sanctioned set: ResetToNewGame (public)
//                             and SeedBlankFoundingOnMissingSave (private,
//                             WO-1250). Any occurrence inside a different
//                             PUBLIC method is a failure.
//
// Global-state discipline: swaps GameStateService.Provider to an in-memory
// provider and restores it; stashes and restores the developer's live
// GameStateService._instance (reflection, the HireReinforcementsRegression
// pattern) so a live editor singleton is never commandeered and never lost;
// stashes and restores the developer's `dotr-save` PlayerPrefs slot (belt and
// braces - the swapped provider never touches PlayerPrefs, but ResetToNewGame
// also clears equip/progression/harvest prefs, the same cost CoreSaveRegression
// [H] already pays in the same DataRegression pass); DestroyImmediate on every
// created object in finally. Hydrates CatalogRegistry from
// structures-catalog.json when it is empty (the BuildEconomyRegression parse,
// verbatim) and leaves it hydrated, as that suite does.
//
// Every case FAILS - never skips - when its fixture cannot be built, and each
// case runs under Guard.Try with a throw recorded as a failure, so there is no
// hollow pass. Markers: BASELAYOUT_ROUNDTRIP_OK / BASELAYOUT_ROUNDTRIP_FAIL.
//
// Wire into DataRegression.RunAll (one line - orchestrator):
//   if (!BaseLayoutRoundTripRegression.Run(out var blrtReason)) failures.Add(blrtReason); else log.AppendLine("[baselayout-roundtrip] " + blrtReason);
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using UnityEngine;
using DeNelle.Core;
using DeNelle.Core.Catalog;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;

namespace DeNelle.Editor.Regression
{
    public static class BaseLayoutRoundTripRegression
    {
        private const string Sys = "BaseLayoutRoundTrip";
        private const string CatalogRelPath = "Data/Canonical/structures-catalog.json";
        private const string ServiceSourceRelPath = "_Modules/Core/State/GameStateService.cs";
        private const int ExpectedRecordCount = 17;

        /// <summary>
        /// The version that INTRODUCED baseLayout. Read at source: SaveSchema.cs:41
        /// changelog tail "v14 - baseLayout (WO-108)" and SaveMigrator.MigrateToV14
        /// (SaveMigrator.cs:306-310) seeds `baseLayout ?? []`. Case 2 reflects the
        /// step method by name so a renumbering fails loudly instead of testing the
        /// wrong chain.
        /// </summary>
        private const int OldestBaseLayoutVersion = 14;

        private sealed class InMemorySaveProvider : ISaveProvider
        {
            public readonly Dictionary<string, string> Store = new Dictionary<string, string>();
            public bool Exists(string slot) => Store.ContainsKey(slot);
            public string Read(string slot) => Store.TryGetValue(slot, out var v) ? v : string.Empty;
            public void Write(string slot, string json) => Store[slot] = json;
            public void Delete(string slot) => Store.Remove(slot);
        }

        private sealed class StructuresFile
        {
            [JsonProperty("version")] public int Version;
            [JsonProperty("entries")] public List<CatalogEntry> Entries = new List<CatalogEntry>();
        }

        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- BASELAYOUT ROUND-TRIP (WO-1361): 17 records through the REAL save -> reload -> migrate cycle ---");
            log.AppendLine($"  SaveSchema.CurrentVersion={SaveSchema.CurrentVersion} oldest-baseLayout-version={OldestBaseLayoutVersion}");

            var created = new List<GameObject>();
            var priorProvider = GameStateService.Provider;
            var priorInstance = GameStateService.Instance;
            bool hadSaveKey = PlayerPrefs.HasKey(SaveSchema.PlayerPrefsKey);
            string savedSlot = hadSaveKey ? PlayerPrefs.GetString(SaveSchema.PlayerPrefsKey) : null;
            var memory = new InMemorySaveProvider();

            try
            {
                GameStateService.Provider = memory;
                // A live singleton would make every AddComponent+Awake a no-op duplicate
                // (Awake: `_instance != null && _instance != this` -> return). Park it for
                // the duration; restored in finally whatever happens.
                if (priorInstance != null)
                {
                    SetInstance(null);
                    log.AppendLine("  parked a live GameStateService.Instance for the duration (restored in finally)");
                }

                RunCase(1, "count-survives", failures, log, () => Case1CountSurvives(memory, created, failures, log));
                RunCase(2, "migrator-from-oldest-supported", failures, log, () => Case2MigratorFromOldest(memory, created, failures, log));
                RunCase(3, "null-and-empty-are-distinct", failures, log, () => Case3NullAndEmptyDistinct(memory, created, failures, log));
                RunCase(4, "new-game-clears-everything-else-not", failures, log, () => Case4NewGameClears(memory, created, failures, log));
            }
            catch (Exception ex)
            {
                failures.Add($"suite threw outside a case: {ex.GetType().Name}: {ex.Message}");
            }
            finally
            {
                foreach (var go in created) if (go != null) UnityEngine.Object.DestroyImmediate(go);
                GameStateService.Provider = priorProvider;
                if (hadSaveKey) PlayerPrefs.SetString(SaveSchema.PlayerPrefsKey, savedSlot);
                else if (PlayerPrefs.HasKey(SaveSchema.PlayerPrefsKey)) PlayerPrefs.DeleteKey(SaveSchema.PlayerPrefsKey);
                SetInstance(priorInstance);
            }

            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "BASELAYOUT_ROUNDTRIP_OK");
                reason = $"BASELAYOUT ROUND-TRIP OK - {ExpectedRecordCount} records survive save->reload (v{SaveSchema.CurrentVersion}) and migrate (v{OldestBaseLayoutVersion}->v{SaveSchema.CurrentVersion}); null/empty distinct; ResetToNewGame is the only public clearer";
                return true;
            }
            reason = "baselayout-roundtrip: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "BASELAYOUT_ROUNDTRIP_FAIL: " + reason);
            return false;
        }

        private static void RunCase(int n, string name, List<string> failures, StringBuilder log, Action body)
        {
            log.AppendLine($"[case {n}] {name}");
            int before = failures.Count;
            // Not Guard.Try: that logs the MESSAGE only, and on 2026-09-04 the first red this
            // oracle ever produced ("GameStateService has been destroyed") could not be placed
            // without the stack. A case that throws is named with its full stack, then counted.
            bool completed;
            try { body(); completed = true; }
            catch (Exception ex)
            {
                completed = false;
                FlowTrace.Fail(Sys, $"case {n} {name} FAILED: {ex}");
            }
            if (!completed)
                failures.Add($"[case {n}] {name} THREW (see [Flow:{Sys}] Fail line) - the case did not run to its assertions");
            log.AppendLine(failures.Count == before ? $"  case {n} holds" : $"  case {n} FAILED ({failures.Count - before} finding(s))");
        }

        // =====================================================================
        //  Case 1 - 17 records through the REAL Save() / Awake-Load() path
        // =====================================================================
        private static void Case1CountSurvives(InMemorySaveProvider memory, List<GameObject> created,
            List<string> failures, StringBuilder log)
        {
            int baseline = failures.Count;
            if (!EnsureCatalog(failures, log)) return;   // failure already recorded

            var fixture = BuildFixture();
            if (fixture.Count != ExpectedRecordCount)
            {
                failures.Add($"[case 1] fixture builder produced {fixture.Count} records, expected {ExpectedRecordCount} - the oracle's own fixture is wrong");
                return;
            }
            var distinct = new HashSet<string>(StringComparer.Ordinal);
            foreach (var r in fixture)
            {
                distinct.Add(r.itemId);
                if (CatalogRegistry.Get(r.itemId) == null)
                    failures.Add($"[case 1] fixture id '{r.itemId}' does not resolve in CatalogRegistry - the game could never persist it; re-point the fixture at a live id");
            }
            if (distinct.Count < 8)
                failures.Add($"[case 1] fixture carries only {distinct.Count} distinct ids, the oracle requires >= 8");
            if (failures.Count > baseline) return;
            log.AppendLine($"  fixture: {fixture.Count} records / {distinct.Count} distinct ids, all resolve in CatalogRegistry ({CatalogRegistry.Count} registered)");

            memory.Store.Clear();

            // -- Lifetime 1: fresh boot (no key -> blank founding), seed, Save(). --
            var svc1 = BootService("BaseLayoutRT_Svc1", created, failures, "case 1 lifetime-1");
            if (svc1 == null) return;
            svc1.State.BaseLayout = new List<PlacedStructureData>(fixture);

            var snap = svc1.Snapshot();
            if (snap == null || snap.BaseLayout == null || snap.BaseLayout.Count != ExpectedRecordCount)
                failures.Add($"[case 1] Snapshot() carried {(snap?.BaseLayout == null ? "null" : snap.BaseLayout.Count.ToString())} records out of the SO, expected {ExpectedRecordCount} (GameStateService.cs:582)");

            svc1.Save();

            string stored = memory.Read(SaveSchema.PlayerPrefsKey);
            if (string.IsNullOrEmpty(stored))
            {
                failures.Add("[case 1] Save() wrote NOTHING to the provider - the write seam is dead");
                return;
            }
            string json = SaveSchema.TryExtractSigned(stored, out bool sigPresent, out bool sigValid);
            if (!sigPresent || !sigValid)
                failures.Add($"[case 1] Save() wrote an unsigned/invalid blob (present={sigPresent} valid={sigValid}) - Load() would REJECT it and the town would be gone");

            // Split WRITE loss from READ loss: count the records inside the blob itself.
            SaveSchema.SaveFile onDisk = null;
            try { onDisk = JsonConvert.DeserializeObject<SaveSchema.SaveFile>(json, SaveSchema.JsonSettings); }
            catch (Exception ex) { failures.Add($"[case 1] stored blob does not parse: {ex.GetType().Name}: {ex.Message}"); }
            if (onDisk != null)
            {
                if (onDisk.StoreVersion != SaveSchema.CurrentVersion)
                    failures.Add($"[case 1] stored blob storeVersion={onDisk.StoreVersion}, expected {SaveSchema.CurrentVersion}");
                int diskCount = onDisk.State?.BaseLayout?.Count ?? -1;
                if (diskCount != ExpectedRecordCount)
                    failures.Add($"[case 1] WRITE LOSS: the stored blob holds {diskCount} baseLayout records, expected {ExpectedRecordCount} - records vanished BEFORE reaching the provider (Snapshot/serialize)");
                else
                    log.AppendLine($"  stored blob: signed, storeVersion={onDisk.StoreVersion}, {diskCount} records on the wire");
            }

            // Capture the GameObject BEFORE DestroyImmediate: touching svc1.gameObject afterwards is
            // a MissingReferenceException (the first red this oracle ever produced, 2026-09-04).
            var go1 = svc1.gameObject;
            UnityEngine.Object.DestroyImmediate(go1);   // OnDestroy clears the singleton
            created.Remove(go1);

            // -- Lifetime 2: fresh service Awake -> Load() -> migrate -> validate -> ApplyPersisted. --
            var svc2 = BootService("BaseLayoutRT_Svc2", created, failures, "case 1 lifetime-2");
            if (svc2 == null) return;
            CompareLayouts(fixture, svc2.State.BaseLayout, "[case 1] READ LOSS after fresh Load()", failures);
            if (failures.Count == baseline)
                log.AppendLine($"  lifetime-1 -> lifetime-2: all {ExpectedRecordCount} records present with identical level/yaw/yawOffset/worldY/wallMounted");

            var go2 = svc2.gameObject;
            UnityEngine.Object.DestroyImmediate(go2);
            created.Remove(go2);
        }

        // =====================================================================
        //  Case 2 - the same 17 at storeVersion 14 through the migrator chain
        // =====================================================================
        private static void Case2MigratorFromOldest(InMemorySaveProvider memory, List<GameObject> created,
            List<string> failures, StringBuilder log)
        {
            var step = typeof(SaveMigrator).GetMethod("MigrateToV" + OldestBaseLayoutVersion,
                BindingFlags.NonPublic | BindingFlags.Static);
            if (step == null)
            {
                failures.Add($"[case 2] SaveMigrator.MigrateToV{OldestBaseLayoutVersion} not found by reflection - the baseLayout seed step moved; re-read SaveMigrator and re-point OldestBaseLayoutVersion");
                return;
            }
            if (OldestBaseLayoutVersion >= SaveSchema.CurrentVersion)
            {
                failures.Add($"[case 2] OldestBaseLayoutVersion ({OldestBaseLayoutVersion}) is not below CurrentVersion ({SaveSchema.CurrentVersion}) - nothing would migrate");
                return;
            }

            var fixture = BuildFixture();

            // (a) PURE: MigrateForImport from v14 to CurrentVersion.
            var pure = new SaveSchema.PersistedState { BaseLayout = new List<PlacedStructureData>(fixture) };
            var result = SaveMigrator.MigrateForImport(pure, OldestBaseLayoutVersion);
            if (result == null || !result.Ok || result.Data == null)
            {
                failures.Add($"[case 2] MigrateForImport(v{OldestBaseLayoutVersion}) REFUSED: {(result == null ? "null result" : result.Reason)}");
                return;
            }
            CompareLayouts(fixture, result.Data.BaseLayout, $"[case 2a] pure migrate v{OldestBaseLayoutVersion}->v{SaveSchema.CurrentVersion}", failures);

            // The v36 step reads the list to seed everBuiltStructureIds - prove it READ all of them.
            if (result.Data.EverBuiltStructureIds == null)
                failures.Add("[case 2a] MigrateToV36 did not seed everBuiltStructureIds from a populated baseLayout");
            else
            {
                foreach (var r in fixture)
                {
                    bool found = false;
                    foreach (var id in result.Data.EverBuiltStructureIds)
                        if (string.Equals(id, r.itemId, StringComparison.OrdinalIgnoreCase)) { found = true; break; }
                    if (!found) failures.Add($"[case 2a] everBuiltStructureIds is missing '{r.itemId}' after migration - MigrateToV36 skipped a baseLayout record");
                }
            }

            // (a2) the seed contract at the oldest version: a NULL list migrates to a non-null EMPTY one.
            var nullState = new SaveSchema.PersistedState { BaseLayout = null };
            var nullResult = SaveMigrator.MigrateForImport(nullState, OldestBaseLayoutVersion - 1);
            if (nullResult == null || !nullResult.Ok || nullResult.Data == null)
                failures.Add($"[case 2a2] MigrateForImport(v{OldestBaseLayoutVersion - 1}) with a null baseLayout REFUSED");
            else if (nullResult.Data.BaseLayout == null || nullResult.Data.BaseLayout.Count != 0)
                failures.Add($"[case 2a2] MigrateToV{OldestBaseLayoutVersion} did not seed a null baseLayout to an EMPTY list (got {(nullResult.Data.BaseLayout == null ? "null" : nullResult.Data.BaseLayout.Count.ToString())})");

            // (b) REAL: a v14 envelope on the provider, loaded by a fresh service.
            memory.Store.Clear();
            var envelopeState = new SaveSchema.PersistedState { BaseLayout = new List<PlacedStructureData>(fixture) };
            WriteEnvelope(memory, OldestBaseLayoutVersion, envelopeState, null);
            var svc = BootService("BaseLayoutRT_SvcV14", created, failures, "case 2b v14 load");
            if (svc == null) return;
            if (svc.State.SchemaVersion != SaveSchema.CurrentVersion)
                failures.Add($"[case 2b] loaded state SchemaVersion={svc.State.SchemaVersion}, expected {SaveSchema.CurrentVersion} - Load() did not run the migrate/apply path (GameStateService.cs:405)");
            CompareLayouts(fixture, svc.State.BaseLayout, $"[case 2b] real Load() of a v{OldestBaseLayoutVersion} envelope", failures);
            var goSvc = svc.gameObject;
            UnityEngine.Object.DestroyImmediate(goSvc);
            created.Remove(goSvc);

            log.AppendLine($"  v{OldestBaseLayoutVersion} -> v{SaveSchema.CurrentVersion}: pure chain + real Load() both carry {ExpectedRecordCount} records; null seeds to empty");
        }

        // =====================================================================
        //  Case 3 - EMPTY list vs ABSENT/NULL field, and the shallow-merge contract
        // =====================================================================
        private static void Case3NullAndEmptyDistinct(InMemorySaveProvider memory, List<GameObject> created,
            List<string> failures, StringBuilder log)
        {
            // (a) EMPTY list on disk -> non-null, Count 0.
            memory.Store.Clear();
            WriteEnvelope(memory, SaveSchema.CurrentVersion,
                new SaveSchema.PersistedState { BaseLayout = new List<PlacedStructureData>() }, null);
            var svcEmpty = BootService("BaseLayoutRT_SvcEmpty", created, failures, "case 3a empty-list load");
            if (svcEmpty == null) return;
            if (svcEmpty.State.BaseLayout == null)
                failures.Add("[case 3a] an EMPTY baseLayout on disk loaded as NULL - every downstream .Count would throw");
            else if (svcEmpty.State.BaseLayout.Count != 0)
                failures.Add($"[case 3a] an EMPTY baseLayout on disk loaded with {svcEmpty.State.BaseLayout.Count} records - something SEEDED structures into an empty town");
            var goEmpty = svcEmpty.gameObject;
            UnityEngine.Object.DestroyImmediate(goEmpty);
            created.Remove(goEmpty);

            // (b) ABSENT key on disk -> the GameState initializer (non-null, empty):
            //     MigrateForImport skips the chain at CurrentVersion (SaveMigrator.cs:119-121)
            //     so MigrateToV14 never runs, p.BaseLayout stays null, and
            //     ApplyPersisted (:706) leaves the fresh SO's `new List<>()` in place.
            memory.Store.Clear();
            WriteEnvelope(memory, SaveSchema.CurrentVersion, new SaveSchema.PersistedState { BaseLayout = null }, "baseLayout");
            string absentJson = SaveSchema.TryExtractSigned(memory.Read(SaveSchema.PlayerPrefsKey), out _, out _);
            if (absentJson.IndexOf("\"baseLayout\"", StringComparison.Ordinal) >= 0)
            {
                failures.Add("[case 3b] fixture error: the 'absent' envelope still carries a baseLayout key");
                return;
            }
            var svcAbsent = BootService("BaseLayoutRT_SvcAbsent", created, failures, "case 3b absent-key load");
            if (svcAbsent == null) return;
            if (svcAbsent.State.BaseLayout == null)
                failures.Add("[case 3b] an ABSENT baseLayout key loaded as NULL - the GameState initializer (GameState.cs:287) was discarded");
            else if (svcAbsent.State.BaseLayout.Count != 0)
                failures.Add($"[case 3b] an ABSENT baseLayout key loaded with {svcAbsent.State.BaseLayout.Count} records on a fresh service");

            // (c) Load() on a LIVE service is a REPLACE, not a shallow merge - the event it raises
            //     is literally StateReplaced. The :706 null-guard is per-field onto the FRESH
            //     state object, so an ABSENT baseLayout on a current-version envelope loads as
            //     that object's initializer: non-null and EMPTY. The first run of this case
            //     (2026-09-04) asserted a merge contract the code never had and went red with
            //     17 records "clobbered"; that was the oracle's assumption, not a defect. The
            //     envelope is authoritative: the game never writes a partial one
            //     (NullValueHandling.Include), so "absent" is a pre-v14 shape and the migrator's
            //     empty seed IS the sanctioned meaning. Pinned so nobody re-derives it.
            var fixture = BuildFixture();
            svcAbsent.State.BaseLayout = new List<PlacedStructureData>(fixture);
            bool loadedAbsent = svcAbsent.Load();
            if (!loadedAbsent)
                failures.Add("[case 3c] Load() over the absent-key envelope returned false - the envelope was REJECTED; it used to be accepted as an empty town");
            if (svcAbsent.State.BaseLayout == null)
                failures.Add("[case 3c] Load() over an ABSENT baseLayout key left the live list NULL - the fresh-state initializer was discarded");
            else if (svcAbsent.State.BaseLayout.Count != 0)
                failures.Add($"[case 3c] Load() over an ABSENT baseLayout key left {svcAbsent.State.BaseLayout.Count} records on the live service - Load() is documented as a REPLACE (StateReplaced); a merge here would be a new, unruled contract");
            else
                log.AppendLine("  case 3c: absent key on a live service -> Load() REPLACED the state; live list is the fresh initializer (0), as documented");

            // (c2) the same with a literal `"baseLayout":null` on the wire.
            memory.Store.Clear();
            WriteEnvelope(memory, SaveSchema.CurrentVersion, new SaveSchema.PersistedState { BaseLayout = null }, null);
            string nullJson = SaveSchema.TryExtractSigned(memory.Read(SaveSchema.PlayerPrefsKey), out _, out _);
            if (nullJson.IndexOf("\"baseLayout\":null", StringComparison.Ordinal) < 0)
                failures.Add("[case 3c2] fixture error: expected a literal \"baseLayout\":null on the wire (SaveSchema.JsonSettings NullValueHandling.Include)");
            svcAbsent.State.BaseLayout = new List<PlacedStructureData>(fixture);
            bool loadedNull = svcAbsent.Load();
            // Recorded, not asserted: whether a literal-null envelope is REJECTED (live list kept
            // because nothing replaced it) or accepted. The first run kept all 17; the return
            // value was not captured, so the reason is stated here on the next run, not guessed.
            log.AppendLine($"  case 3c2: literal \"baseLayout\":null envelope -> Load() returned {loadedNull}; live list now {svcAbsent.State.BaseLayout?.Count.ToString() ?? "null"}");
            CompareLayouts(fixture, svcAbsent.State.BaseLayout, "[case 3c2] literal NULL CLOBBERED a populated in-memory list on Load()", failures);

            // (d) an EMPTY list on disk IS authoritative on a live service - by contract the
            //     disk says the player has no structures. Asserted so the semantics are pinned,
            //     not guessed.
            memory.Store.Clear();
            WriteEnvelope(memory, SaveSchema.CurrentVersion,
                new SaveSchema.PersistedState { BaseLayout = new List<PlacedStructureData>() }, null);
            svcAbsent.State.BaseLayout = new List<PlacedStructureData>(fixture);
            svcAbsent.Load();
            if (svcAbsent.State.BaseLayout == null)
                failures.Add("[case 3d] empty-list Load() over a populated service left BaseLayout NULL");
            else if (svcAbsent.State.BaseLayout.Count != 0)
                failures.Add($"[case 3d] an EMPTY list on disk did not replace the in-memory list (still {svcAbsent.State.BaseLayout.Count}) - disk is no longer authoritative; ApplyPersisted (:706) changed shape");

            // (e) SEAM FINDING - a MISSING key (not a missing field) on a live service:
            //     Load() -> SeedBlankFoundingOnMissingSave (:436-447) assigns a NEW empty
            //     list onto the live _state. Pinned as CURRENT behaviour (WO-1250) and named.
            memory.Store.Clear();
            svcAbsent.State.BaseLayout = new List<PlacedStructureData>(fixture);
            bool loadedMissing = svcAbsent.Load();
            int afterMissing = svcAbsent.State.BaseLayout?.Count ?? -1;
            if (loadedMissing)
                failures.Add("[case 3e] Load() with NO save key returned true - it claimed to load a save that does not exist");
            if (afterMissing != 0)
                failures.Add($"[case 3e] Load() with NO save key left {afterMissing} in-memory records; the documented WO-1250 SeedBlankFoundingOnMissingSave path (GameStateService.cs:436-447) blanks BaseLayout - the seam changed shape, re-read it and re-pin");
            else
                log.AppendLine("  seam finding pinned: Load() over a MISSING key blanks a populated live list via SeedBlankFoundingOnMissingSave (WO-1250; only Awake + AutoPilot call Load())");

            var goAbsent = svcAbsent.gameObject;
            UnityEngine.Object.DestroyImmediate(goAbsent);
            created.Remove(goAbsent);

            if (failures.Count == 0)
                log.AppendLine("  empty -> 0 (non-null); absent/null -> initializer (non-null, 0); absent/null never clobbers a live list; empty on disk is authoritative");
        }

        // =====================================================================
        //  Case 4 - ResetToNewGame clears (sanctioned); nothing else public does
        // =====================================================================
        private static void Case4NewGameClears(InMemorySaveProvider memory, List<GameObject> created,
            List<string> failures, StringBuilder log)
        {
            // Behavioural half.
            memory.Store.Clear();
            var svc = BootService("BaseLayoutRT_SvcReset", created, failures, "case 4 reset");
            if (svc == null) return;
            svc.State.BaseLayout = new List<PlacedStructureData>(BuildFixture());
            svc.ResetToNewGame();
            if (svc.State.BaseLayout == null)
                failures.Add("[case 4] ResetToNewGame left BaseLayout NULL - it must be a fresh EMPTY list (GameStateService.cs:1250)");
            else if (svc.State.BaseLayout.Count != 0)
                failures.Add($"[case 4] ResetToNewGame left {svc.State.BaseLayout.Count} records - New Game must start on the blank template (WO-707)");
            var goSvc = svc.gameObject;
            UnityEngine.Object.DestroyImmediate(goSvc);
            created.Remove(goSvc);

            // Source-lint half.
            string path = Path.Combine(Application.dataPath, ServiceSourceRelPath);
            if (!File.Exists(path))
            {
                failures.Add($"[case 4] source lint: {ServiceSourceRelPath} not found under Assets/ - the service moved; re-point ServiceSourceRelPath");
                return;
            }
            string[] rawLines = File.ReadAllLines(path);
            string[] lines = StripCommentsAndStrings(rawLines);

            var sanctioned = new Dictionary<string, bool>(StringComparer.Ordinal)
            {
                { "ResetToNewGame", true },                    // public, the ONE sanctioned public clearer (:1250)
                { "SeedBlankFoundingOnMissingSave", false },   // private, WO-1250 blank founding on a MISSING save (:441)
            };
            var assignRx = new Regex(@"\bBaseLayout\s*=\s*new\b");
            int hits = 0;
            bool sawReset = false;
            for (int i = 0; i < lines.Length; i++)
            {
                if (!assignRx.IsMatch(lines[i])) continue;
                hits++;
                string method = EnclosingMethod(lines, i, out bool isPublic);
                log.AppendLine($"  lint: line {i + 1} `{rawLines[i].Trim()}` in {(isPublic ? "public" : "non-public")} {method}()");
                if (method == "ResetToNewGame") sawReset = true;
                if (sanctioned.TryGetValue(method, out bool expectPublic))
                {
                    if (expectPublic != isPublic)
                        failures.Add($"[case 4] lint: {method} is now {(isPublic ? "public" : "non-public")} (expected {(expectPublic ? "public" : "non-public")}) - the sanctioned clearer set changed; re-read and re-pin");
                    continue;
                }
                if (isPublic)
                    failures.Add($"[case 4] lint: PUBLIC {method}() assigns `BaseLayout = new` at GameStateService.cs:{i + 1} - a second public clearer of the player's town (only ResetToNewGame may)");
                else
                    failures.Add($"[case 4] lint: non-public {method}() assigns `BaseLayout = new` at GameStateService.cs:{i + 1} - not in the sanctioned set (ResetToNewGame, SeedBlankFoundingOnMissingSave); name it here or remove it");
            }
            if (hits == 0)
                failures.Add("[case 4] lint: found ZERO `BaseLayout = new` in GameStateService.cs - the lint regex or the stripper is broken (ResetToNewGame:1250 must match)");
            if (!sawReset)
                failures.Add("[case 4] lint: ResetToNewGame no longer assigns `BaseLayout = new` - the sanctioned clearer moved; re-read GameStateService.ResetToNewGame");
            log.AppendLine($"  lint: {hits} `BaseLayout = new` assignment(s) in GameStateService.cs, all in the sanctioned set");
        }

        // =====================================================================
        //  Fixture - 17 real-catalog records, 12 distinct ids
        // =====================================================================
        // Ids read from Assets/Resources/Data/Canonical/structures-catalog.json
        // (2026-09-04): walls/gate (wall_wood, wall_stone, gate_stone), towers
        // (tower_ground_archer, tower_ballista), collectors (collector_lumbermill,
        // collector_farm), storefronts/stations (market, forge, pet-house,
        // lumberyard), a resource node (mine_crystal). Levels stay within the ids'
        // catalog ceilings (wall_wood maxLevel 2 per BuildEconomyRegression:604).
        private static List<PlacedStructureData> BuildFixture()
        {
            return new List<PlacedStructureData>
            {
                new PlacedStructureData("wall_wood",            2,  2, 0, 1),
                new PlacedStructureData("wall_wood",            3,  2, 0, 2),
                new PlacedStructureData("wall_wood",            4,  2, 1, 1, yawOffset: 15f),
                new PlacedStructureData("wall_wood",           -5, -5, 2, 2),
                new PlacedStructureData("wall_stone",           6,  2, 0, 1),
                new PlacedStructureData("wall_stone",           7,  2, 3, 2, worldY: 0.5f),
                new PlacedStructureData("gate_stone",           8,  2, 1, 1, yawOffset: 45f),
                new PlacedStructureData("tower_ground_archer",  3,  5, 2, 1),
                new PlacedStructureData("tower_ground_archer",  7,  5, 3, 3, yawOffset: 30f, worldY: 2.5f, wallMounted: true),
                new PlacedStructureData("tower_ballista",      -3,  9, 0, 2, worldY: 2.5f, wallMounted: true),
                new PlacedStructureData("collector_lumbermill", 12, 4, 1, 1),
                new PlacedStructureData("collector_farm",       12, 8, 0, 2),
                new PlacedStructureData("market",               0, 12, 2, 1),
                new PlacedStructureData("forge",               -8, 12, 1, 1, yawOffset: 60f),
                new PlacedStructureData("pet-house",           -8, -8, 3, 1),
                new PlacedStructureData("lumberyard",           14, -2, 0, 3),
                new PlacedStructureData("mine_crystal",         -14, 14, 2, 1, worldY: 1.25f),
            };
        }

        // =====================================================================
        //  Helpers
        // =====================================================================

        /// <summary>
        /// Every expected (itemId, cellX, cellZ) must be present in <paramref name="actual"/>
        /// with identical level/yawSteps/yawOffset/worldY/wallMounted, and the counts must
        /// match. Missing/changed records are named by id + cell; extras are named too.
        /// </summary>
        private static void CompareLayouts(List<PlacedStructureData> expected, List<PlacedStructureData> actual,
            string tag, List<string> failures)
        {
            if (actual == null)
            {
                failures.Add($"{tag}: BaseLayout is NULL (expected {expected.Count} records)");
                return;
            }
            var missing = new List<string>();
            var changed = new List<string>();
            var matched = new bool[actual.Count];
            foreach (var e in expected)
            {
                int idx = -1;
                for (int i = 0; i < actual.Count; i++)
                {
                    if (matched[i]) continue;
                    var a = actual[i];
                    if (string.Equals(a.itemId, e.itemId, StringComparison.Ordinal) && a.cellX == e.cellX && a.cellZ == e.cellZ)
                    { idx = i; break; }
                }
                if (idx < 0) { missing.Add($"{e.itemId}@({e.cellX},{e.cellZ})"); continue; }
                matched[idx] = true;
                var got = actual[idx];
                var diffs = new List<string>();
                if (got.level != e.level) diffs.Add($"level {e.level}->{got.level}");
                if (got.yawSteps != e.yawSteps) diffs.Add($"yawSteps {e.yawSteps}->{got.yawSteps}");
                if (Math.Abs(got.yawOffset - e.yawOffset) > 0.001f) diffs.Add($"yawOffset {e.yawOffset}->{got.yawOffset}");
                if (Math.Abs(got.worldY - e.worldY) > 0.001f) diffs.Add($"worldY {e.worldY}->{got.worldY}");
                if (got.wallMounted != e.wallMounted) diffs.Add($"wallMounted {e.wallMounted}->{got.wallMounted}");
                if (diffs.Count > 0) changed.Add($"{e.itemId}@({e.cellX},{e.cellZ}) [{string.Join(", ", diffs)}]");
            }
            var extras = new List<string>();
            for (int i = 0; i < actual.Count; i++)
                if (!matched[i]) extras.Add($"{actual[i].itemId}@({actual[i].cellX},{actual[i].cellZ})");

            if (actual.Count != expected.Count)
                failures.Add($"{tag}: count {actual.Count}, expected {expected.Count}");
            if (missing.Count > 0)
                failures.Add($"{tag}: MISSING {missing.Count} record(s): {string.Join(", ", missing)}");
            if (changed.Count > 0)
                failures.Add($"{tag}: CHANGED {changed.Count} record(s): {string.Join(", ", changed)}");
            if (extras.Count > 0)
                failures.Add($"{tag}: {extras.Count} UNEXPECTED record(s): {string.Join(", ", extras)}");
        }

        /// <summary>
        /// Serialises a SaveFile envelope at <paramref name="storeVersion"/> through
        /// SaveSchema.JsonSettings, optionally REMOVES one key from the state object
        /// (to fabricate a truly ABSENT field), signs it with SaveSchema.EmbedSignature
        /// and writes it to the provider under SaveSchema.PlayerPrefsKey - the exact
        /// shape GameStateService.Save() produces (GameStateService.cs:463-480).
        /// </summary>
        private static void WriteEnvelope(InMemorySaveProvider memory, int storeVersion,
            SaveSchema.PersistedState state, string removeStateKey)
        {
            var file = new SaveSchema.SaveFile
            {
                Format = SaveSchema.FileFormat,
                StoreVersion = storeVersion,
                ExportedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                Wallet = null,
                State = state,
            };
            string json = JsonConvert.SerializeObject(file, SaveSchema.JsonSettings);
            if (!string.IsNullOrEmpty(removeStateKey))
            {
                var root = JObject.Parse(json);
                var st = root["state"] as JObject;
                if (st != null) st.Remove(removeStateKey);
                json = root.ToString(Formatting.None);
            }
            memory.Write(SaveSchema.PlayerPrefsKey, SaveSchema.EmbedSignature(json));
        }

        /// <summary>
        /// Edit-mode AddComponent does not run Awake; invoke it by reflection (the
        /// CoreSaveRegression [H] pattern). Awake: sets _instance, creates the SO,
        /// then Load() (GameStateService.cs:238-253). Returns null AND records a
        /// failure when the service cannot be booted - never a silent skip.
        /// </summary>
        private static GameStateService BootService(string name, List<GameObject> created,
            List<string> failures, string why)
        {
            var awake = typeof(GameStateService).GetMethod("Awake", BindingFlags.NonPublic | BindingFlags.Instance);
            if (awake == null)
            {
                failures.Add($"[{why}] could not reflect GameStateService.Awake - the lifecycle seam moved; re-point this oracle");
                return null;
            }
            if (GameStateService.Instance != null)
            {
                failures.Add($"[{why}] a GameStateService.Instance is already live ({GameStateService.Instance.name}) - Awake would no-op as a duplicate; the previous case did not tear down");
                return null;
            }
            var go = new GameObject(name);
            created.Add(go);
            var svc = go.AddComponent<GameStateService>();
            awake.Invoke(svc, null);
            if (svc.State == null)
            {
                failures.Add($"[{why}] service has no State after Awake");
                return null;
            }
            if (!ReferenceEquals(GameStateService.Instance, svc))
            {
                failures.Add($"[{why}] Awake did not install this service as Instance");
                return null;
            }
            return svc;
        }

        private static void SetInstance(GameStateService value)
        {
            var f = typeof(GameStateService).GetField("_instance", BindingFlags.NonPublic | BindingFlags.Static);
            if (f != null) f.SetValue(null, value);
        }

        /// <summary>
        /// Hydrates CatalogRegistry from structures-catalog.json when empty - the
        /// BuildEconomyRegression.ParseCatalog parse verbatim (StringEnumConverter +
        /// ignore null/missing), registered the way CatalogBootstrap does. Returns
        /// false (with a failure recorded) when the file cannot be read or parsed.
        /// </summary>
        private static bool EnsureCatalog(List<string> failures, StringBuilder log)
        {
            if (CatalogRegistry.Count > 0) return true;

            string json = CanonicalJson.Read(CatalogRelPath);
            if (string.IsNullOrEmpty(json))
            {
                failures.Add($"[case 1] {CatalogRelPath} unreadable (CanonicalJson.Read returned empty) - cannot prove the fixture ids are real");
                return false;
            }
            StructuresFile file;
            try
            {
                var settings = new JsonSerializerSettings
                {
                    Converters = { new StringEnumConverter() },
                    NullValueHandling = NullValueHandling.Ignore,
                    MissingMemberHandling = MissingMemberHandling.Ignore,
                };
                file = JsonConvert.DeserializeObject<StructuresFile>(json, settings);
            }
            catch (Exception ex)
            {
                failures.Add($"[case 1] structures-catalog.json failed to parse: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
            if (file == null || file.Entries == null || file.Entries.Count == 0)
            {
                failures.Add("[case 1] structures-catalog.json deserialized to 0 entries");
                return false;
            }
            int registered = 0;
            foreach (var e in file.Entries)
                if (e != null && !string.IsNullOrEmpty(e.id) && CatalogRegistry.Get(e.id) == null)
                { CatalogRegistry.Register(e); registered++; }
            log.AppendLine($"  hydrated CatalogRegistry from {CatalogRelPath}: {registered} row(s) registered");
            return CatalogRegistry.Count > 0;
        }

        /// <summary>
        /// Blanks // and /* */ comments and the contents of string literals (keeps
        /// line count so reported line numbers match the file). Verbatim and
        /// interpolated strings are handled well enough for a lint that only needs
        /// `BaseLayout = new` to be invisible inside a FlowTrace message.
        /// </summary>
        private static string[] StripCommentsAndStrings(string[] lines)
        {
            var output = new string[lines.Length];
            bool inBlock = false;
            for (int i = 0; i < lines.Length; i++)
            {
                string s = lines[i];
                var sb = new StringBuilder(s.Length);
                int j = 0;
                while (j < s.Length)
                {
                    if (inBlock)
                    {
                        int end = s.IndexOf("*/", j, StringComparison.Ordinal);
                        if (end < 0) { j = s.Length; break; }
                        j = end + 2;
                        inBlock = false;
                        continue;
                    }
                    char c = s[j];
                    if (c == '/' && j + 1 < s.Length && s[j + 1] == '/') break;
                    if (c == '/' && j + 1 < s.Length && s[j + 1] == '*') { inBlock = true; j += 2; continue; }
                    if (c == '"')
                    {
                        bool verbatim = j > 0 && (s[j - 1] == '@' || (j > 1 && s[j - 1] == '$' && s[j - 2] == '@') || (j > 1 && s[j - 1] == '@' && s[j - 2] == '$'));
                        sb.Append('"');
                        j++;
                        while (j < s.Length)
                        {
                            if (s[j] == '"')
                            {
                                if (verbatim && j + 1 < s.Length && s[j + 1] == '"') { j += 2; continue; }
                                break;
                            }
                            if (!verbatim && s[j] == '\\') { j += 2; continue; }
                            j++;
                        }
                        sb.Append('"');
                        j++;
                        continue;
                    }
                    if (c == '\'' && j + 2 < s.Length)
                    {
                        // char literal: skip '\x' or 'x'
                        int close = s.IndexOf('\'', j + (s[j + 1] == '\\' ? 3 : 2));
                        j = close < 0 ? s.Length : close + 1;
                        sb.Append("' '");
                        continue;
                    }
                    sb.Append(c);
                    j++;
                }
                output[i] = sb.ToString();
            }
            return output;
        }

        private static readonly Regex MethodDeclRx = new Regex(
            @"^\s*(public|private|internal|protected)\s+(?:static\s+|override\s+|virtual\s+|async\s+)*[\w<>\[\],\.\s]+?\s+(\w+)\s*\(",
            RegexOptions.Compiled);

        /// <summary>Walks up from <paramref name="lineIndex"/> to the nearest method declaration.</summary>
        private static string EnclosingMethod(string[] lines, int lineIndex, out bool isPublic)
        {
            for (int i = lineIndex; i >= 0; i--)
            {
                var m = MethodDeclRx.Match(lines[i]);
                if (!m.Success) continue;
                string name = m.Groups[2].Value;
                if (name == "if" || name == "for" || name == "foreach" || name == "while" || name == "switch" || name == "using" || name == "catch") continue;
                isPublic = m.Groups[1].Value == "public";
                return name;
            }
            isPublic = false;
            return "<no enclosing method found>";
        }
    }
}
