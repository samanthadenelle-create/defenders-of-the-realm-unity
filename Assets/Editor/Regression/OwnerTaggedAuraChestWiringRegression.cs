// =============================================================================
// OwnerTaggedAuraChestWiringRegression [owner-aura-chest] - WO-1346 + WO-1347.
// -----------------------------------------------------------------------------
// Pins THREE owner-tagged VFX keys to the code that consumes them, so a later
// refactor cannot quietly detach an effect the owner asked for by name:
//
//   ArcaneTower_Aura        -> Fog/Fog_electric.prefab            (WO-1346)
//   Treasure_Aura           -> Loot/Loot_iddle.prefab             (WO-1347)
//   DailyChestCollect_Aura  -> Backlight_resources/backlight_coin (WO-1347)
//
// WHAT IT ASSERTS, AND WHY EACH ONE EARNS ITS PLACE:
//
//  (a) THE MAPPING IS HERS. Each key's prefabPath in Assets/Editor/VfxManualPicks.json
//      still points at the prefab she tagged, and that prefab still EXISTS at that
//      path on disk. 'Loot_iddle' is the pack's own typo for idle - a well-meant
//      "correction" in either the JSON or a code path is a silent load failure, and
//      this is what catches it.
//
//  (b) THE CONSUMER IS STILL WIRED. Each key is read by the file that owns its
//      lifecycle, by literal. A key with no consumer is a tag she made that never
//      reaches the screen, which is indistinguishable from a subtle effect.
//
//  (c) THE ARCANE AURA IS GATED ON BUILT STATE, from STATE and not from the build
//      event. Her spec is "arcane tower vfx (after built) softly". An event-only
//      wiring passes a felt-test once and then vanishes on every relaunch until the
//      player builds another tower - the single most likely way to get this wrong,
//      so it is asserted rather than trusted.
//
//  (d) THE SOFT DEFAULT IS SUBDUED AND THE NO-ROW INVARIANT HOLDS. With no database
//      row, no network and the knob not yet registered, ArcaneTowerAuraTuning must
//      answer EXACTLY its built-in default - and that default must be under 100% (she
//      asked for soft) and over 0% (an invisible aura reads as a broken one).
//
//  (e) THE CHEST SHIMMER IS GATED ON THE UNOPENED STATE. A shimmer still playing over
//      an emptied chest reads as a bug and invites a second tap.
//
// Source-lint + one pure runtime read (edit-mode, no PlayMode) - mirrors
// VfxAuraDifferentiationRegression. NEVER throws: a missing file is a listed fail.
//
// REGISTER IT (one line, in DeNelle.Editor.DataRegression.RunAll, next to the other
// vfx suites) - deliberately NOT added here because three other agents are editing
// that same registry in this tree right now:
//
//   DeNelle.Core.Diagnostics.Guard.Try("Regression", "owner-aura-chest suite", () => { if (!OwnerTaggedAuraChestWiringRegression.Run(out var r)) failures.Add(r); else log.AppendLine("[owner-aura-chest] " + r); });
//
// ASCII only.
// =============================================================================
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class OwnerTaggedAuraChestWiringRegression
    {
        // Her three keys and the prefab each one is tagged to, as the owner authored them.
        private const string TowerKey = "ArcaneTower_Aura";
        private const string TowerPrefab = "Assets/Lana Studio/Casual RPG VFX/Prefabs/Fog/Fog_electric.prefab";

        private const string ChestKey = "Treasure_Aura";
        private const string ChestPrefab = "Assets/Lana Studio/Casual RPG VFX/Prefabs/Loot/Loot_iddle.prefab";

        private const string DailyKey = "DailyChestCollect_Aura";
        private const string DailyPrefab = "Assets/Lana Studio/Casual RPG VFX/Prefabs/Backlight_resources/backlight_coin.prefab";

        public static bool Run(out string reason)
        {
            string assets = Application.dataPath;
            string root = Directory.GetParent(assets)?.FullName ?? assets;

            string picks = Path.Combine(assets, "Editor/VfxManualPicks.json");
            string tower = Path.Combine(assets, "_Modules/Village/Buildings/ArcaneTower.cs");
            string aura = Path.Combine(assets, "_Modules/Village/Vfx/ArcaneAura.cs");
            string tuning = Path.Combine(assets, "_Modules/Village/Vfx/ArcaneTowerAuraTuning.cs");
            string cache = Path.Combine(assets, "_Modules/Dungeons/DungeonTreasureCache.cs");
            string daily = Path.Combine(assets, "_Modules/Village/Monetization/DailyChestController.cs");
            string burst = Path.Combine(assets, "_Modules/Village/Vfx/CollectBurstVfx.cs");

            var fails = new List<string>();

            string picksTxt = Read(picks, "VfxManualPicks.json", fails);
            string towerTxt = Read(tower, "ArcaneTower.cs", fails);
            string auraTxt = Read(aura, "ArcaneAura.cs", fails);
            string tuningTxt = Read(tuning, "ArcaneTowerAuraTuning.cs", fails);
            string cacheTxt = Read(cache, "DungeonTreasureCache.cs", fails);
            string dailyTxt = Read(daily, "DailyChestController.cs", fails);
            string burstTxt = Read(burst, "CollectBurstVfx.cs", fails);

            // -- (a) her key -> prefab mapping, and the prefab exists on disk ---------
            RequireTag(picksTxt, TowerKey, TowerPrefab, root, fails);
            RequireTag(picksTxt, ChestKey, ChestPrefab, root, fails);
            RequireTag(picksTxt, DailyKey, DailyPrefab, root, fails);

            // The arcane aura is an AMBIENT LOOP and she tagged it isLoop:true. If that ever
            // flips, the tower's aura becomes a burst and the "(after built)" gate is holding
            // nothing - so the flag is pinned for this key only. (The other two keys are
            // deliberately NOT pinned on isLoop: both are authored false and both conflicts are
            // REPORTED to the owner rather than corrected, per the no-pick rule.)
            if (picksTxt.Length > 0 && !IsLoopTrue(picksTxt, TowerKey))
                fails.Add("VfxManualPicks row '" + TowerKey + "' is no longer isLoop:true - the " +
                          "Arcane Tower's ambient aura would become a one-shot burst and the " +
                          "after-built gate would be holding nothing.");

            // -- (b)+(c) the tower consumer, wired by literal AND gated on built state -
            if (towerTxt.Length > 0)
            {
                if (!Regex.IsMatch(towerTxt, @"ArcaneAura\.Ensure\(\s*gameObject\s*,\s*""" + TowerKey + @""""))
                    fails.Add("ArcaneTower.cs no longer passes the owner's key \"" + TowerKey +
                              "\" to ArcaneAura.Ensure(gameObject, ...) as a LITERAL - the tower has " +
                              "lost her tagged aura (or moved it behind a const, which also breaks " +
                              "VfxAuraDifferentiationRegression's source-lint).");
                if (!towerTxt.Contains("requireBuilt: true"))
                    fails.Add("ArcaneTower.cs no longer opts into the built-state gate " +
                              "(requireBuilt: true) - her spec is 'after built' and an ungated aura " +
                              "plays on the scaffold and in the Obsidian queue.");
                if (!towerTxt.Contains("ArcaneTowerAuraTuning.SoftEmissionMul()"))
                    fails.Add("ArcaneTower.cs no longer reads the 'softly' intensity from " +
                              "ArcaneTowerAuraTuning - the dial she asked to be tweakable from a db " +
                              "call has been replaced by a constant.");
            }

            if (auraTxt.Length > 0)
            {
                if (!auraTxt.Contains("UnderConstructionVisual.IsUnderConstruction(gameObject)"))
                    fails.Add("ArcaneAura.cs no longer consults " +
                              "UnderConstructionVisual.IsUnderConstruction - the built-state gate is " +
                              "gone, so the aura is back to playing during construction.");
                // STATE, not the event: the gate must be asked inside StartAura, which is what
                // runs on a cold load for an ALREADY-BUILT tower. A gate that only lives in an
                // event handler passes a felt-test once and vanishes on every relaunch.
                if (!Regex.IsMatch(auraTxt,
                        @"private void StartAura\(\)[\s\S]{0,2000}?UnderConstructionVisual\.IsUnderConstruction"))
                    fails.Add("ArcaneAura.StartAura no longer asks the built-state question itself - " +
                              "the gate has become event-driven, which loses the RELOAD case (an " +
                              "already-built tower would stay dark until the player builds another).");
                if (!auraTxt.Contains("SetEmissionScale"))
                    fails.Add("ArcaneAura.cs no longer applies the 'softly' dial to the spawned " +
                              "INSTANCE via VfxLoopModulator.SetEmissionScale.");
            }

            // -- (d) subdued default, and the no-row invariant, read at runtime -------
            if (tuningTxt.Length > 0)
            {
                int def = DeNelle.Village.ArcaneTowerAuraTuning.SoftEmissionDefaultPct;
                if (def <= 0 || def >= 100)
                    fails.Add("ArcaneTowerAuraTuning.SoftEmissionDefaultPct is " + def + "% - it must " +
                              "be strictly between 0 and 100. She asked for 'softly' (so under 100), " +
                              "and 0 would be an INVISIBLE aura, which reads as broken rather than soft.");

                // THE NO-ROW INVARIANT. Nothing has been fetched in an edit-mode suite, so this is
                // the offline / 404 / empty-table / unregistered-key path exactly.
                float mul = DeNelle.Village.ArcaneTowerAuraTuning.SoftEmissionMul();
                float expected = def / 100f;
                if (Mathf.Abs(mul - expected) > 0.0001f)
                    fails.Add("NO-ROW INVARIANT BROKEN: with no database row and the knob unregistered, " +
                              "ArcaneTowerAuraTuning.SoftEmissionMul() answered " + mul + " instead of the " +
                              "built-in default " + expected + ". An offline player must get the shipping " +
                              "softness exactly.");
            }

            // -- (b)+(e) the dungeon chest: her key, gated on the UNOPENED state ------
            if (cacheTxt.Length > 0)
            {
                if (!Regex.IsMatch(cacheTxt, @"ShimmerKey\s*=\s*""" + ChestKey + @""""))
                    fails.Add("DungeonTreasureCache.cs no longer names the owner's key \"" + ChestKey +
                              "\" - the in-world treasure chest has lost her idle shimmer.");
                if (!cacheTxt.Contains("StopShimmer(\"cache opened\")"))
                    fails.Add("DungeonTreasureCache.Open no longer stops the idle shimmer - a shimmer " +
                              "over an opened chest reads as a bug and invites a second tap.");
                if (!cacheTxt.Contains("StopShimmer(\"cache claimed\")"))
                    fails.Add("DungeonTreasureCache.Claim no longer stops the idle shimmer.");
                if (!Regex.IsMatch(cacheTxt, @"private void StartShimmer[\s\S]{0,400}?if \(_opened\) return;"))
                    fails.Add("DungeonTreasureCache.StartShimmer no longer refuses to spawn on an " +
                              "OPENED chest - the shimmer's lifetime is meant to be owned by the " +
                              "chest's unopened state.");
            }

            // -- (b) the daily chest collect burst, and its world-space placement -----
            if (dailyTxt.Length > 0 &&
                !Regex.IsMatch(dailyTxt, @"CollectBurstVfx\.Raise\(\s*""" + DailyKey + @""""))
                fails.Add("DailyChestController.cs no longer raises the owner's key \"" + DailyKey +
                          "\" on the claim path - the daily chest collect flourish is gone.");

            if (burstTxt.Length > 0)
            {
                // The whole point of this helper: a world-space composite must NOT be parented
                // into the overlay Canvas, and a UI beat has no object to own a loop's lifetime.
                if (!burstTxt.Contains("Lifetime"))
                    fails.Add("CollectBurstVfx no longer time-bounds the burst - a key retagged " +
                              "isLoop:true would strand an orphan loop in front of the camera with " +
                              "nobody holding a handle.");
                if (!Regex.IsMatch(burstTxt, @"PlayKey\([\s\S]{0,200}?null,\s*\r?\n?\s*null,\s*0f,\s*Lifetime\)"))
                    fails.Add("CollectBurstVfx no longer spawns UNPARENTED with an explicit lifetime - " +
                              "parented into the modal it would be destroyed by Close(), and parented " +
                              "into the overlay Canvas it would render at the wrong scale or not at all.");
            }

            if (fails.Count > 0)
            {
                reason = "[owner-aura-chest] " + fails.Count + " failure(s): " + string.Join(" | ", fails);
                return false;
            }

            reason = "3 owner-tagged keys pinned to their consumers: " + TowerKey + " (built-state " +
                     "gated + soft default " + DeNelle.Village.ArcaneTowerAuraTuning.SoftEmissionDefaultPct +
                     "% with the no-row invariant proven), " + ChestKey + " (gated on the chest's " +
                     "unopened state), " + DailyKey + " (unparented world-space burst, time-bounded). " +
                     "All three prefabs present at the owner's authored paths.";
            return true;
        }

        // -- helpers -----------------------------------------------------------------

        private static string Read(string path, string label, List<string> fails)
        {
            if (!File.Exists(path)) { fails.Add(label + " missing (" + path + ")"); return ""; }
            string txt = null;
            try { txt = File.ReadAllText(path); }
            catch (IOException e) { fails.Add(label + " unreadable: " + e.Message); }
            return txt ?? "";
        }

        /// <summary>Assert the owner's key -> prefabPath row is intact AND the prefab is on disk.</summary>
        private static void RequireTag(string picksTxt, string key, string prefabPath,
                                       string repoRoot, List<string> fails)
        {
            if (picksTxt.Length == 0) return;

            var m = Regex.Match(picksTxt,
                "\"key\"\\s*:\\s*\"" + Regex.Escape(key) + "\"\\s*,\\s*\"prefabPath\"\\s*:\\s*\"([^\"]+)\"");
            if (!m.Success)
            {
                fails.Add("VfxManualPicks.json has no row for the owner-tagged key '" + key + "'.");
                return;
            }

            string actual = m.Groups[1].Value.Replace('\\', '/');
            if (!string.Equals(actual, prefabPath, System.StringComparison.Ordinal))
                fails.Add("owner tag '" + key + "' now points at '" + actual + "' but this wiring was " +
                          "built against '" + prefabPath + "'. The tag is HERS - re-point the consumer, " +
                          "never the tag.");

            string onDisk = Path.Combine(repoRoot, actual.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(onDisk))
                fails.Add("owner tag '" + key + "' points at '" + actual + "' but no such prefab exists " +
                          "on disk. (Note 'Loot_iddle' is the pack's own spelling - correcting it to " +
                          "'idle' is exactly this failure.)");
        }

        private static bool IsLoopTrue(string picksTxt, string key)
        {
            var m = Regex.Match(picksTxt,
                "\"key\"\\s*:\\s*\"" + Regex.Escape(key) + "\"[\\s\\S]{0,400}?\"isLoop\"\\s*:\\s*(true|false)");
            return m.Success && m.Groups[1].Value == "true";
        }

    }
}
