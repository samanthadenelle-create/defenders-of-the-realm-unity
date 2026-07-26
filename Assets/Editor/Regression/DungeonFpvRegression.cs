// =============================================================================
// DungeonFpvRegression [dungeon-fpv] — locks the dungeon FIRST-PERSON camera
// (2026-07-26; an architect chose FPV traversal over raising the ~4u ceiling, and
// the owner wants it). RUNTIME-ONLY feature — no re-bake, reversible via
// ff.dungeonfpv=0. PlayMode camera feel is hard to unit-test, so this is a
// SOURCE-LINT that gates the five wiring seams so a later edit can't silently gut
// the FPV rig, its controls, or the combat-camera switch:
//   1. ff.dungeonfpv DEFAULTS ON (Get("dungeonfpv", defaultOn: true)).
//   2. DungeonCameraRig carries the FPV look-accumulator (yaw+pitch) with a pitch
//      clamp, hides the hero body (ShadowsOnly), and exposes SetCombatFraming(bool);
//      the look layer runs in LateUpdate decoupled from the movement heading.
//   3. DungeonHero feeds the shared VirtualJoystick into movement AND gates
//      tap-to-move OFF while FPV is active (a tap is "look", not "walk").
//   4. DungeonController wires SetCombatFraming on battle STAGED (OTS for the fight)
//      and battle ENDED (restore FPV traversal).
//   5. BattleArena raises OnBattleStaged (fired in BeginEncounter) — the start signal.
// FEEL (motion sickness / control) is OWNER felt-verify — this only guards wiring.
// Source-lint (edit-mode, no PlayMode). Wired into DataRegression.RunAll. Never throws.
// =============================================================================
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

namespace DeNelle.Editor
{
    public static class DungeonFpvRegression
    {
        public static bool Run(out string reason)
        {
            var fails = new List<string>();

            string flags = ReadOrFail("_Modules/Core/FeatureFlags.cs", fails);
            string rig   = ReadOrFail("_Modules/Dungeons/DungeonCameraRig.cs", fails);
            string hero  = ReadOrFail("_Modules/Dungeons/DungeonHero.cs", fails);
            string ctrl  = ReadOrFail("_Modules/Dungeons/DungeonController.cs", fails);
            string arena = ReadOrFail("_Modules/Village/Arena/BattleArena.cs", fails);
            if (fails.Count > 0) { return Verdict(fails, out reason); }

            // (1) ff.dungeonfpv defaults ON (reversible via PlayerPrefs ff.dungeonfpv=0).
            if (!Regex.IsMatch(flags, @"DungeonFpv\s*=>\s*Get\(""dungeonfpv"",\s*defaultOn:\s*true\)"))
                fails.Add("FeatureFlags.DungeonFpv is not defaulted ON (expected Get(\"dungeonfpv\", defaultOn: true))");

            // (2) DungeonCameraRig — look accumulator + pitch clamp + body-hide + SetCombatFraming.
            if (!rig.Contains("_lookYaw") || !rig.Contains("_lookPitch"))
                fails.Add("DungeonCameraRig has no FPV look accumulator (_lookYaw/_lookPitch)");
            if (!Regex.IsMatch(rig, @"Mathf\.Clamp\(\s*_lookPitch") || !rig.Contains("_fpvPitchClamp"))
                fails.Add("DungeonCameraRig does not pitch-clamp the FPV look (_fpvPitchClamp)");
            if (!rig.Contains("SampleLookDelta"))
                fails.Add("DungeonCameraRig has no independent look sampler (SampleLookDelta — right-half drag / mouse delta)");
            if (!Regex.IsMatch(rig, @"void\s+LateUpdate"))
                fails.Add("DungeonCameraRig drives no LateUpdate look layer (heading-decoupled free-look)");
            if (!rig.Contains("HideHeroBody") || !rig.Contains("ShadowsOnly"))
                fails.Add("DungeonCameraRig does not hide the hero body on FPV bind (HideHeroBody + ShadowsOnly)");
            if (!Regex.IsMatch(rig, @"public\s+void\s+SetCombatFraming\s*\(\s*bool"))
                fails.Add("DungeonCameraRig has no public SetCombatFraming(bool) combat-framing override");

            // (3) DungeonHero — virtual joystick fed into movement + tap-to-move FPV gate.
            if (!hero.Contains("SampleJoystickMove") || !hero.Contains("VirtualJoystick.Move"))
                fails.Add("DungeonHero does not feed the shared VirtualJoystick into movement (SampleJoystickMove)");
            if (!Regex.IsMatch(hero, @"if\s*\(\s*!FeatureFlags\.DungeonFpv\s*\)\s*\n\s*TrySampleTap"))
                fails.Add("DungeonHero does not gate tap-to-move OFF in FPV (expected 'if (!FeatureFlags.DungeonFpv) TrySampleTap()')");

            // (4) DungeonController — combat-camera switch on battle staged/ended.
            if (!ctrl.Contains("OnBattleStaged += OnRealtimeBattleStaged"))
                fails.Add("DungeonController never subscribes BattleArena.OnBattleStaged (no combat-camera switch on fight start)");
            if (!ctrl.Contains("OnBattleStaged -= OnRealtimeBattleStaged"))
                fails.Add("DungeonController never unsubscribes BattleArena.OnBattleStaged (event leak)");
            if (!Regex.IsMatch(ctrl, @"OnRealtimeBattleStaged[\s\S]{0,300}SetCombatFraming\(true\)"))
                fails.Add("DungeonController does not SetCombatFraming(true) on battle staged (OTS for the fight)");
            if (!Regex.IsMatch(ctrl, @"OnRealtimeBattleEnded[\s\S]{0,300}SetCombatFraming\(false\)"))
                fails.Add("DungeonController does not SetCombatFraming(false) on battle ended (restore FPV traversal)");

            // (5) BattleArena — the battle-STARTED signal (event route).
            if (!Regex.IsMatch(arena, @"event\s+Action<EncounterParams>\s+OnBattleStaged"))
                fails.Add("BattleArena has no OnBattleStaged event (the battle-started signal)");
            if (!arena.Contains("OnBattleStaged?.Invoke("))
                fails.Add("BattleArena never fires OnBattleStaged in BeginEncounter");

            return Verdict(fails, out reason);
        }

        private static string ReadOrFail(string rel, List<string> fails)
        {
            string p = Path.Combine(Application.dataPath, rel);
            if (!File.Exists(p)) { fails.Add("source not found: " + rel + " — re-point this oracle"); return string.Empty; }
            return File.ReadAllText(p);
        }

        private static bool Verdict(List<string> fails, out string reason)
        {
            if (fails.Count == 0)
            {
                Debug.Log("DUNGEON_FPV_OK");
                reason = "DUNGEON FPV OK — ff default ON, look+clamp+body-hide+SetCombatFraming, joystick+tap-gate, combat-camera switch, OnBattleStaged (FEEL = owner felt-verify)";
                return true;
            }
            reason = "dungeon-fpv: " + string.Join("; ", fails);
            Debug.LogError("DUNGEON_FPV_FAIL: " + reason);
            return false;
        }
    }
}
