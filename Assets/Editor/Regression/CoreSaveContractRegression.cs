// =============================================================================
// CoreSaveContractRegression — the save version-triple + migrate/round-trip contract.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression (references DeNelle.Core). Headless, no-scene.
// The save layer's #1 silent-corruption risk is VERSION DRIFT: three places declare
// "the current schema version" and they must agree, or an old save either skips a
// migration step (data loss) or is falsely rejected as "from a newer version":
//   1. SaveSchema.CurrentVersion         — the authoritative constant.
//   2. GameState.SchemaVersion (default) — the in-memory state's stamp (must == #1).
//   3. SaveMigrator's TOP migration step — the highest target version the chain can
//      migrate TO (read by reflection from the private Steps table). Per the migrator's
//      own convention (MigrateToV27 is a documented no-op STEP kept "to keep the version
//      chain explicit + unit-testable"), the newest bump carries a Steps entry, so the
//      top step key must equal CurrentVersion. A bump with no matching step = real drift.
//
// Plus a real migrate + JSON round-trip: migrate a v1 payload up to current (must seed
// the additive fields), serialize/deserialize through SaveSchema.JsonSettings, validate
// it, and confirm MigrateForImport REJECTS a version newer than this build.
//
// Restores no global state (constructs only a throwaway GameState ScriptableObject,
// DestroyImmediate in finally; touches no PlayerPrefs).
//
// Wire into the suite from DataRegression.RunAll (one line):
//   if (!CoreSaveContractRegression.Run(out var coreSaveReason)) failures.Add(coreSaveReason); else log.AppendLine("[core-save] " + coreSaveReason);
// =============================================================================
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using UnityEngine;
using DeNelle.Core.State;

namespace DeNelle.Editor
{
    public static class CoreSaveContractRegression
    {
        public static bool Run(out string reason)
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("--- CORE SAVE CONTRACT (version triple + migrate/round-trip) ---");

            GameState throwaway = null;
            try
            {
                int current = SaveSchema.CurrentVersion;
                log.AppendLine($"SaveSchema.CurrentVersion = {current}");

                // (1) GameState.SchemaVersion default must equal CurrentVersion.
                throwaway = ScriptableObject.CreateInstance<GameState>();
                int stateVersion = throwaway.SchemaVersion;
                log.AppendLine($"GameState.SchemaVersion (fresh) = {stateVersion}");
                if (stateVersion != current)
                    failures.Add($"version drift: GameState.SchemaVersion ({stateVersion}) != SaveSchema.CurrentVersion ({current})");

                // (2) SaveMigrator's TOP migration step (reflection on the private Steps table).
                int migratorTop = ReadMigratorTopStep(out string reflectErr);
                if (reflectErr != null)
                {
                    // The seam moved — fail loud rather than pass a vacuous check.
                    failures.Add(reflectErr);
                }
                else
                {
                    log.AppendLine($"SaveMigrator top step target = {migratorTop}");
                    if (migratorTop != current)
                        failures.Add($"version drift: SaveMigrator top step ({migratorTop}) != SaveSchema.CurrentVersion ({current}) " +
                                     "— a schema bump landed without a matching migration Steps entry (or a step targets a stale version)");
                }

                // (3) Migrate a v1 payload to current: additive fields must be seeded.
                var migrated = SaveMigrator.Migrate(new SaveSchema.PersistedState(), 1);
                if (migrated == null)
                    failures.Add("SaveMigrator.Migrate(v1) returned null");
                else
                {
                    if (!migrated.Resources.HasValue) failures.Add("migrate v1->current did not seed resources (v2 step)");
                    if (migrated.Quests == null) failures.Add("migrate v1->current did not seed quests (v6 step)");
                    if (migrated.Zones == null || migrated.Zones.Count == 0) failures.Add("migrate v1->current did not seed zone graph (v17 step)");
                }

                // (4) JSON round-trip through the REAL save settings, then validate.
                if (migrated != null)
                {
                    string json = Newtonsoft.Json.JsonConvert.SerializeObject(migrated, SaveSchema.JsonSettings);
                    var back = Newtonsoft.Json.JsonConvert.DeserializeObject<SaveSchema.PersistedState>(json, SaveSchema.JsonSettings);
                    if (back == null)
                        failures.Add("save round-trip deserialized to null");
                    else
                    {
                        var vr = SaveSchema.Validate(back);
                        if (!vr.Ok)
                            failures.Add($"migrated+round-tripped save FAILED validation: field '{vr.FieldPath}' ({vr.Reason})");
                    }
                }

                // (5) A save from a NEWER version than this build must be REJECTED.
                var future = SaveMigrator.MigrateForImport(new SaveSchema.PersistedState(), current + 1);
                if (future == null || future.Ok)
                    failures.Add($"MigrateForImport accepted a future version ({current + 1}) — should reject as newer-than-build");

                // (6) An equal-version import is a no-op pass-through (not rejected).
                var same = SaveMigrator.MigrateForImport(new SaveSchema.PersistedState(), current);
                if (same == null || !same.Ok)
                    failures.Add($"MigrateForImport rejected the CURRENT version ({current}) — should be a no-op pass");
            }
            finally
            {
                if (throwaway != null) Object.DestroyImmediate(throwaway);
            }

            if (failures.Count == 0)
            {
                Debug.Log(log.ToString() + "CORE_SAVE_OK");
                reason = $"CORE SAVE CONTRACT OK — version triple aligned at {SaveSchema.CurrentVersion} + migrate/round-trip/version-gate hold";
                return true;
            }
            reason = "core-save: " + string.Join("; ", failures);
            Debug.LogError(log.ToString() + "CORE_SAVE_FAIL: " + reason);
            return false;
        }

        /// <summary>
        /// Reads the highest TARGET version in SaveMigrator's private static Steps table
        /// (SortedDictionary&lt;int, Func&lt;...&gt;&gt;) by reflection — the real chain the
        /// runtime applies, not a re-derivation. Sets <paramref name="err"/> (and returns -1)
        /// if the seam was renamed/removed so the oracle fails loud instead of vacuously passing.
        /// </summary>
        private static int ReadMigratorTopStep(out string err)
        {
            err = null;
            var field = typeof(SaveMigrator).GetField("Steps", BindingFlags.NonPublic | BindingFlags.Static);
            if (field == null)
            {
                err = "could not read SaveMigrator.Steps (private migration table) by reflection — the migrator seam was renamed/removed; re-point this oracle";
                return -1;
            }
            var dict = field.GetValue(null) as IDictionary;
            if (dict == null || dict.Count == 0)
            {
                err = "SaveMigrator.Steps is null/empty via reflection — the migration chain has no steps (seam moved)";
                return -1;
            }
            int top = int.MinValue;
            foreach (var key in dict.Keys)
                if (key is int k && k > top) top = k;
            if (top == int.MinValue)
            {
                err = "SaveMigrator.Steps keys were not int — the migration table shape changed";
                return -1;
            }
            return top;
        }
    }
}
