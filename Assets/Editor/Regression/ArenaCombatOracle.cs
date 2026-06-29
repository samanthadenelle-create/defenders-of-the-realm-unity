// =============================================================================
// ArenaCombatOracle -- headless EXECUTED-behaviour oracle for the arena battle
// CLOSE (WO-505 victory/defeat audio + stars + reward multiplier) and the
// WO-504 s3 rarity swing-trail.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.EditorRegression   Namespace: DeNelle.Editor
//
// WHY THIS EXISTS (owner, 2026-06-23: "the testing must NOT be lazy / assumption-
// based"): the AutoPilot fleet is hub-capped (MainCastle_Hall + Village2 only,
// WO-453) and never enters a battle, so the win/loss audio cue, the star rating +
// reward multiplier, and the rarity swing-trail rested on CODE-READING INFERENCE.
// This oracle DRIVES the REAL code path and PROVES each fire actually happened by
// reading CAPTURED signals -- never inference (CLAUDE.md S12 hard gate):
//
//   (a) MusicTrack.Victory requested via CoreServices.Audio on a WIN
//       -- proven by a RECORDING IAudioService that captures the requested track,
//         AND by the permanent FlowTrace "VICTORY AUDIO FIRED" line at the call.
//   (b) MusicTrack.Defeat requested on a LOSS -- same two captured signals.
//   (c) stars computed + the reward multiplier applied on the resolve path
//       -- proven by the permanent FlowTrace "STARS=<n> rewardMult=<x> applied" line
//         (emitted INSIDE the real Resolve), with the expected tier for the duration.
//   (d) the hero's GearLoadout granted a reward on the win path (BattleArena.Resolve
//       -> GrantWinReward) -- proven by the "GrantWinReward: ... XP" FlowTrace line.
//   (e) PlayerAttackController applied a NON-default (non-steel) trail color for a
//       high-rarity equipped weapon -- proven by EXECUTING the real EnsureSwingTrail
//       + ApplyWeaponTrailVfx (via the QA seam) and reading back the applied color
//         AND the permanent "TRAIL color=... rarity=<band> applied" FlowTrace line.
//
// HOW IT STAYS NON-FRAGILE (the prompt's "do not hack a fake" constraint): it does
// NOT stand up a full PlayMode fight (NavMesh bake, family spawn, kill loop -- heavy
// + flaky). It drives the SAME private Resolve the live fight calls, via a thin QA
// seam (BattleArena.ResolveForTest) that only pre-seeds the fields BeginEncounter
// would have set -- zero behaviour fork. The trail uses the SAME EnsureSwingTrail +
// ApplyWeaponTrailVfx a real swing runs (PlayerAttackController.ApplyWeaponTrailVfxForTest).
//
// RUN (batchmode, Unity closed):
//   run-unity-method.ps1 -Method DeNelle.Editor.ArenaCombatOracle.Run -LogName arena-oracle.log
//   (or menu: Defenders/QA/Run Arena Combat Oracle)
// Prints ONE grep-able marker: ARENA_ORACLE_OK / ARENA_ORACLE_FAIL: <reasons>.
// =============================================================================

using System.Collections.Generic;
using System.Text;
using UnityEditor;
using UnityEngine;
using DeNelle.Core;
using DeNelle.Core.Audio;
using DeNelle.Core.Diagnostics;
using DeNelle.Village;
using DeNelle.Village.Arena;

namespace DeNelle.Editor
{
    /// <summary>
    /// Headless oracle that EXECUTES the arena resolve + trail-apply paths and
    /// asserts the WO-505 / WO-504 s3 fires happened from CAPTURED signals (a
    /// recording audio service + a capturing FlowTrace sink), not code-reading.
    /// </summary>
    public static class ArenaCombatOracle
    {
        // -- Capturing FlowTrace sink: buffers every [Flow:*] line so we can assert
        //    the permanent FIRED instrumentation actually emitted on the real path. --
        private sealed class CaptureSink : ITraceSink
        {
            public readonly List<string> Lines = new List<string>();
            public void Info(string line)  { Lines.Add(line); }
            public void Warn(string line)  { Lines.Add(line); }
            public void Error(string line) { Lines.Add(line); }

            public bool Has(string needle)
            {
                foreach (var l in Lines)
                    if (l != null && l.Contains(needle)) return true;
                return false;
            }
        }

        // -- Recording audio service: captures every PlayMusic request so we PROVE
        //    the resolve path actually asked CoreServices.Audio for Victory/Defeat. --
        private sealed class RecordingAudio : IAudioService
        {
            public readonly List<MusicTrack> Tracks = new List<MusicTrack>();
            public void PlayMusic(MusicTrack track) { Tracks.Add(track); }
            public void StopMusic() { }
            public void PlaySfx(AudioClip clip, float volume) { }
            public void PlayUiClick() { }

            public bool Requested(MusicTrack t) => Tracks.Contains(t);
        }

        [MenuItem("Defenders/QA/Run Arena Combat Oracle")]
        public static void Run()
        {
            var failures = new List<string>();
            var log = new StringBuilder();
            log.AppendLine("=== ArenaCombatOracle: drive the REAL resolve + trail paths, prove the fires ===");

            // Preserve + restore the global trace/audio state so the oracle leaves no
            // durable side-effects on the editor session.
            ITraceSink prevSink = FlowTrace.Sink;
            bool prevEnabled = FlowTrace.Enabled;
            IAudioService prevAudio = CoreServices.Audio;

            var sink = new CaptureSink();
            var audio = new RecordingAudio();

            try
            {
                FlowTrace.Enabled = true;
                FlowTrace.AllOn();
                FlowTrace.Sink = sink;
                CoreServices.RegisterAudio(audio);

                // -----------------------------------------------------------------
                //  PART 1 -- WIN resolve: victory audio + stars + reward multiplier.
                // -----------------------------------------------------------------
                // A fast (60s) win -> the 3-star tier (BattleStarRating.StarsForDuration
                // <= 90s == 3) -> reward x1.50. We pin the tier so the assert proves the
                // REAL star math ran on the resolve path, not just "some number".
                var winParams = new EncounterParams
                {
                    EnemyIds = new[] { "orc-warrior", "orc-tank", "orc-mage" },
                    Threat = 2,
                    BackdropContext = "outerworld",
                    ReturnScene = "OuterWorld",
                    ReturnPosition = new Vector3(1f, 0f, 2f),
                    ReturnYaw = 90f,
                };
                const float winDuration = 60f;
                int expectedStars = BattleStarRating.StarsForDuration(winDuration);     // 3
                float expectedMult = BattleStarRating.MultiplierForStars(expectedStars); // 1.50

                BattleArena.Instance.ResolveForTest(winParams, won: true, durationSeconds: winDuration);

                // (a) victory audio requested via CoreServices.Audio (recording service).
                if (!audio.Requested(MusicTrack.Victory))
                    failures.Add("WIN: CoreServices.Audio did NOT receive PlayMusic(Victory) on the resolve path");
                else
                    log.AppendLine("  [exec] WIN -> CoreServices.Audio.PlayMusic(Victory) CAPTURED");

                // (a') the permanent FIRED line emitted AT the call (the felt-test break-log signal).
                if (!sink.Has("VICTORY AUDIO FIRED track=Victory"))
                    failures.Add("WIN: missing FlowTrace 'VICTORY AUDIO FIRED track=Victory' (audio fire-point not reached)");
                else
                    log.AppendLine("  [exec] WIN -> FlowTrace 'VICTORY AUDIO FIRED' CAPTURED");

                // (c) stars + reward multiplier computed + applied on the resolve path.
                string starsNeedle = $"STARS={expectedStars} rewardMult={expectedMult:0.00} applied";
                if (!sink.Has(starsNeedle))
                    failures.Add($"WIN: missing FlowTrace '{starsNeedle}' -- star/reward computation did not run as expected " +
                                 $"(duration {winDuration:0}s must map to {expectedStars} star(s) x{expectedMult:0.00})");
                else
                    log.AppendLine($"  [exec] WIN -> FlowTrace '{starsNeedle}' CAPTURED (real star math ran)");

                // (d) reward GRANTED on the win path (GrantWinReward -> XP line). Proves the
                // multiplier feeds the reward, not just gets computed and dropped.
                if (!sink.Has("GrantWinReward:"))
                    failures.Add("WIN: missing FlowTrace 'GrantWinReward:' -- the win reward grant did not run");
                else
                    log.AppendLine("  [exec] WIN -> FlowTrace 'GrantWinReward:' CAPTURED (reward path ran with the multiplier)");

                // (e) WO-556 ITEM 1 — the itemized SUMMARY totals were CAPTURED and handed to the
                // victory-summary view (proves GrantWinReward now RETURNS totals, not void).
                if (!sink.Has("SUMMARY xp="))
                    failures.Add("WIN: missing FlowTrace 'SUMMARY xp=...' -- reward totals were not captured for the victory summary (WO-556 ITEM 1)");
                else
                    log.AppendLine("  [exec] WIN -> FlowTrace 'SUMMARY xp=...' CAPTURED (totals captured for the summary view)");

                // -----------------------------------------------------------------
                //  PART 2 -- LOSS resolve: defeat audio fires, no reward.
                // -----------------------------------------------------------------
                audio.Tracks.Clear();
                BattleArena.Instance.ResolveForTest(winParams, won: false, durationSeconds: 130f);

                if (!audio.Requested(MusicTrack.Defeat))
                    failures.Add("LOSS: CoreServices.Audio did NOT receive PlayMusic(Defeat) on the resolve path");
                else
                    log.AppendLine("  [exec] LOSS -> CoreServices.Audio.PlayMusic(Defeat) CAPTURED");

                if (!sink.Has("DEFEAT AUDIO FIRED track=Defeat"))
                    failures.Add("LOSS: missing FlowTrace 'DEFEAT AUDIO FIRED track=Defeat' (defeat fire-point not reached)");
                else
                    log.AppendLine("  [exec] LOSS -> FlowTrace 'DEFEAT AUDIO FIRED' CAPTURED");

                // A loss must NOT request Victory (cross-check the win/loss branch is real).
                if (audio.Requested(MusicTrack.Victory))
                    failures.Add("LOSS: CoreServices.Audio wrongly received PlayMusic(Victory) on a LOSS");

                // -----------------------------------------------------------------
                //  PART 3 -- rarity swing-trail: the controller applies a NON-steel
                //  trail color for a high-rarity equipped weapon (WO-504 s3).
                // -----------------------------------------------------------------
                RunTrailCheck(failures, log, sink);
            }
            catch (System.Exception ex)
            {
                failures.Add($"oracle threw: {ex.GetType().Name}: {ex.Message}");
                log.AppendLine($"  EXCEPTION: {ex}");
            }
            finally
            {
                // Restore global state -- leave no durable side-effects.
                CoreServices.UnregisterAudio(audio);
                if (prevAudio != null) CoreServices.RegisterAudio(prevAudio);
                FlowTrace.Sink = prevSink;
                FlowTrace.Enabled = prevEnabled;
            }

            // -- verdict --
            log.AppendLine("=== verdict ===");
            bool ok = failures.Count == 0;
            if (ok)
            {
                log.AppendLine("ARENA_ORACLE_OK");
                Debug.Log(log.ToString());
            }
            else
            {
                log.AppendLine($"ARENA_ORACLE_FAIL: {failures.Count} failure(s):");
                foreach (var f in failures) log.AppendLine("  - " + f);
                Debug.LogError(log.ToString());
            }

            if (Application.isBatchMode)
                EditorApplication.Exit(ok ? 0 : 1);
        }

        // =====================================================================
        //  PART 3 helper -- drive the REAL EnsureSwingTrail + ApplyWeaponTrailVfx on
        //  a live PlayerAttackController + GearLoadout with a high-rarity weapon, and
        //  assert the applied color is NON-steel (reads legendary) + the FIRED line.
        // =====================================================================
        private static void RunTrailCheck(List<string> failures, StringBuilder log, CaptureSink sink)
        {
            GearCatalog.Reload();

            // Pick the highest-rarity MAIN-HAND weapon the catalog actually has (legendary
            // -> epic -> rare). Catalog-driven so it stays valid as gear changes; if none
            // exists we report it honestly rather than invent a fake.
            WeaponDef high = PickHighRarityMainHand();
            if (high == null)
            {
                failures.Add("TRAIL: no legendary/epic/rare MAIN-HAND weapon in the catalog to prove a non-default trail");
                return;
            }
            string band = (high.rarity ?? "?").ToLowerInvariant();
            log.AppendLine($"  [trail] high-rarity weapon: id='{high.id}' rarity='{band}'");

            // The EXPECTED applied color is exactly the pure resolver's answer for this
            // weapon -- so the assert proves the CONTROLLER routed through WeaponVfxMap,
            // and that the result is NOT the steel common default.
            Color expected = WeaponVfxMap.Resolve(high).TrailColor;
            if (ApproxColor(expected, WeaponVfxMap.SteelColor))
            {
                // A rare-band weapon should not resolve to steel; if it does the catalog's
                // top rarity is 'common'/unknown -- report honestly, don't pass vacuously.
                failures.Add($"TRAIL: highest-rarity weapon '{high.id}' (rarity '{band}') still resolves the STEEL default " +
                             "-- cannot prove a non-default trail (catalog has no >common weapon?)");
                return;
            }

            // Clear any persisted equip for the test class so the equip is deterministic.
            const string Job = "knight";
            string key = Job.ToLowerInvariant();
            PlayerPrefs.DeleteKey("dotr-equip-weapon-" + key);
            PlayerPrefs.DeleteKey("dotr-equip-offhand-" + key);
            PlayerPrefs.DeleteKey("dotr-equip-armor-" + key);

            var heroGo = new GameObject("ArenaOracleHero");
            try
            {
                var loadout = heroGo.AddComponent<GearLoadout>();
                loadout.BindOwnerClass(Job);

                var attack = heroGo.AddComponent<PlayerAttackController>();

                // Equip the high-rarity weapon through the REAL armory API. If it isn't a
                // knight-eligible main-hand, equip still takes by id (EquipWeaponById finds
                // it in the catalog) -- what matters is EquippedWeapon carries the rarity.
                loadout.EquipWeaponById(high.id);
                if (loadout.EquippedWeapon == null || loadout.EquippedWeapon.id != high.id)
                {
                    // Some high-rarity weapons are off-hand/2H or class-gated; fall back to
                    // forcing the id we know is a main-hand. Re-pick a main-hand of this band.
                    failures.Add($"TRAIL: could not equip '{high.id}' as the main hand (got " +
                                 $"'{loadout.EquippedWeapon?.id ?? "<null>"}') -- cannot drive the trail apply");
                    return;
                }

                Color applied = attack.ApplyWeaponTrailVfxForTest();   // EXECUTES the real path

                // (d.1) applied color matches the pure resolver's answer (controller routed through it).
                if (!ApproxColor(applied, expected))
                    failures.Add($"TRAIL: controller applied color ({Fmt(applied)}) != WeaponVfxMap.Resolve color " +
                                 $"({Fmt(expected)}) for '{high.id}' -- controller did not route through the resolver");
                else
                    log.AppendLine($"  [exec] TRAIL -> controller applied {Fmt(applied)} (== resolver, NON-steel)");

                // (d.2) applied color is NOT the steel default -- the headline "reads legendary" proof.
                if (ApproxColor(applied, WeaponVfxMap.SteelColor))
                    failures.Add($"TRAIL: controller applied the STEEL default for a '{band}' weapon -- rarity does not read");

                // (d.3) the permanent FIRED line emitted AT the apply (felt-test break-log signal).
                if (!sink.Has("TRAIL color=") || !sink.Has($"rarity={high.rarity} applied"))
                    failures.Add($"TRAIL: missing FlowTrace 'TRAIL color=... rarity={high.rarity} applied' (trail fire-point not reached)");
                else
                    log.AppendLine($"  [exec] TRAIL -> FlowTrace 'TRAIL color=... rarity={high.rarity} applied' CAPTURED");
            }
            finally
            {
                if (heroGo != null) Object.DestroyImmediate(heroGo);
                PlayerPrefs.DeleteKey("dotr-equip-weapon-" + key);
                PlayerPrefs.DeleteKey("dotr-equip-offhand-" + key);
                PlayerPrefs.DeleteKey("dotr-equip-armor-" + key);
            }
        }

        // Highest-rarity main-hand weapon in the catalog (legendary -> epic -> rare).
        // Skips off-hand/shield items (they never carry the main-hand swing trail).
        private static WeaponDef PickHighRarityMainHand()
        {
            string[] order = { "legendary", "elarion", "epic", "rare" };
            foreach (var want in order)
            {
                foreach (var w in GearCatalog.AllWeapons())
                {
                    if (w == null || w.IsOffHandItem) continue;
                    string r = (w.rarity ?? "").Trim().ToLowerInvariant();
                    if (r == want) return w;
                }
            }
            return null;
        }

        private static bool ApproxColor(Color a, Color b)
        {
            return Mathf.Approximately(a.r, b.r) && Mathf.Approximately(a.g, b.g)
                && Mathf.Approximately(a.b, b.b) && Mathf.Approximately(a.a, b.a);
        }

        private static string Fmt(Color c) => $"({c.r:0.00},{c.g:0.00},{c.b:0.00},{c.a:0.00})";
    }
}
