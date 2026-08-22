// =============================================================================
// ArenaReturnMusicRegression [arena-return-music] -- WO-517
// Pins the additive arena return to the position-aware WorldMusicDirector.
// =============================================================================
using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace DeNelle.Editor.Regression
{
    public static class ArenaReturnMusicRegression
    {
        public static bool Run(out string reason)
        {
            try
            {
                var failures = new List<string>();
                string arena = Read("Assets/_Modules/Village/Arena/BattleArena.cs");
                string director = Read("Assets/_Modules/Village/World/WorldMusicDirector.cs");

                Require(arena, "RestoreAmbientContext();", failures,
                    "normal delayed restore no longer delegates to the context authority");
                Require(arena, "restore ambient context after abandon", failures,
                    "abandon/flee path no longer uses the context-aware restore");
                Require(arena, "WorldMusicDirector.Instance", failures,
                    "arena does not resolve the shared position-aware music director");
                Require(arena, "director.ReapplyCurrentContext()", failures,
                    "arena resolves the director but never asks it to re-evaluate");
                Require(arena, "WaitForSecondsRealtime(Mathf.Max(0f, seconds))", failures,
                    "result cue delay was removed; victory/defeat sting would be cut short");

                int hardcoded = Count(arena, "CoreServices.Audio?.PlayMusic(MusicTrack.Overworld)");
                if (hardcoded != 0)
                    failures.Add("BattleArena still contains " + hardcoded +
                                 " hardcoded Overworld ambient restore(s)");

                Require(director, "public bool ReapplyCurrentContext()", failures,
                    "WorldMusicDirector exposes no explicit additive-return re-evaluation seam");
                Require(director, "ZoneManager.GetZone(hero.position)", failures,
                    "music context is no longer derived from the hero's real zone");
                Require(director, "MusicTrack.Village", failures,
                    "town/village return track is absent from the shared authority");
                Require(director, "MusicTrack.Overworld", failures,
                    "outer-world return track is absent from the shared authority");

                if (failures.Count > 0)
                {
                    reason = "arena-return-music: " + failures.Count + " failure(s): " +
                             string.Join(" | ", failures);
                    return false;
                }

                reason = "arena-return-music: resolve + abandon preserve the result beat, then " +
                         "re-evaluate Village/Overworld from the hero's actual return zone";
                return true;
            }
            catch (Exception ex)
            {
                reason = "arena-return-music: oracle threw " + ex.GetType().Name + ": " + ex.Message;
                return false;
            }
        }

        public static void RunStandalone()
        {
            if (Run(out string reason)) Debug.Log("ARENA_RETURN_MUSIC_OK - " + reason);
            else Debug.LogError("ARENA_RETURN_MUSIC_FAIL - " + reason);
        }

        private static string Read(string relative)
        {
            return File.ReadAllText(Path.Combine(Application.dataPath, "..", relative));
        }

        private static void Require(string source, string token, List<string> failures, string message)
        {
            if (source.IndexOf(token, StringComparison.Ordinal) < 0) failures.Add(message);
        }

        private static int Count(string source, string token)
        {
            int count = 0;
            int at = 0;
            while ((at = source.IndexOf(token, at, StringComparison.Ordinal)) >= 0)
            {
                count++;
                at += token.Length;
            }
            return count;
        }
    }
}
