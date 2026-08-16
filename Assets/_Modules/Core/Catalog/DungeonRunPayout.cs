// =============================================================================
// DungeonRunPayout — carries the POLISH SCORE of each un-polished rough stone from
// the dungeon that produced it to the Jeweler's bench (WO-1041 / WO-1042).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.Catalog
//
// THE PROBLEM THIS SOLVES: the rough stone is one material id in the larder, so the stone
// itself cannot remember how well the run that produced it went. The grade is earned in
// the dungeon scene and spent in the town scene, minutes or days later, across a scene
// load and possibly an app restart. Something has to carry it.
//
// WHY PLAYERPREFS AND NOT A SAVE-SCHEMA FIELD: a schema bump is a migration, and this is a
// small, self-healing, non-authoritative hint — the AUTHORITATIVE record of what the player
// owns is the larder count, which is already persisted properly. If this list is ever lost
// or out of step, the stone is still there and still polishes; it just rolls on the floor
// row, which still pays a real gem. Nothing is destroyed and nothing is duplicated. The
// project already uses PlayerPrefs for exactly this class of non-authoritative state
// (FeatureFlags' ff.* keys). ⚠ If a future WO bumps the save schema for another reason,
// MOVING this into GameState is a welcome cleanup — the surface is two methods.
//
// FIFO, deliberately: stones are indistinguishable in the larder, so the player cannot
// choose which one to hand over. Paying out oldest-first is the only order that is honest
// about that, and it means a great run's grade is never silently spent on a later stone.
// =============================================================================

using System.Collections.Generic;
using UnityEngine;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.Catalog
{
    /// <summary>
    /// The FIFO of polish scores for rough stones the player is carrying but has not yet
    /// polished. Written when a run pays out; read (and consumed) when a polish is queued.
    /// </summary>
    public static class DungeonRunPayout
    {
        private const string Sys = "JewelPolish";
        private const string PrefKey = "dungeon.pendingpolishscores";

        /// <summary>
        /// Push the score of a freshly granted rough stone. Setting this ENQUEUES a score; it is a
        /// property purely so the grant site reads as a single assignment.
        /// </summary>
        public static int LastPolishScore
        {
            set => Push(value);
            get
            {
                var q = Load();
                return q.Count > 0 ? q[0] : 0;
            }
        }

        /// <summary>Record the polish score of one newly granted rough stone.</summary>
        public static void Push(int score)
        {
            var q = Load();
            q.Add(Mathf.Clamp(score, 0, DungeonRunGrade.MaxStars));
            Save(q);
            FlowTrace.Step(Sys, $"pending polish scores: pushed {score} (now {q.Count} stone(s) waiting).");
        }

        /// <summary>
        /// Take the oldest pending score, removing it. Returns 0 when none is recorded — which is the
        /// FLOOR row, not a refusal: a stone whose grade was lost still polishes into a real gem.
        /// </summary>
        public static int Pop()
        {
            var q = Load();
            if (q.Count == 0)
            {
                FlowTrace.Warn(Sys, "no pending polish score recorded for this stone - rolling on the " +
                                    "floor row (score 0). The stone still pays; only the grade bonus is lost.");
                return 0;
            }
            int score = q[0];
            q.RemoveAt(0);
            Save(q);
            return score;
        }

        /// <summary>How many rough stones have a recorded grade waiting. Diagnostics + regression.</summary>
        public static int PendingCount => Load().Count;

        // ── The per-stone ROLL CAP (owner ruling 2026-08-16: "no more than five roles") ──

        /// <summary>
        /// The base ceiling on how many times ONE stone may be rolled — the first polish plus its
        /// re-polishes. Owner: "no more than five roles... you have four more chances."
        /// </summary>
        public const int BaseRollCap = 5;

        private const string RollsKey = "dungeon.polishrollsused";

        /// <summary>
        /// The effective cap for this player: the base plus any ATTEMPT bonus (staking grants
        /// attempts, never odds — see IPolishBonusProvider). Zero bonus for everyone by default and
        /// on Play-store builds, because the flag is off.
        /// </summary>
        public static int RollCap => BaseRollCap + Mathf.Max(0, PolishBonuses.RollCapDelta);

        /// <summary>Rolls already spent on the stone currently at the head of the FIFO.</summary>
        public static int RollsUsed => Mathf.Max(0, PlayerPrefs.GetInt(RollsKey, 0));

        /// <summary>
        /// How many rolls remain for the current stone.
        /// <para>
        /// ⚠ THIS IS A CEILING, NOT A PROMISE, AND THE COPY MUST SAY SO. A stone can SHATTER on any
        /// re-roll, so a player with "4 chances left" may get none of them. Player-facing text must
        /// read "up to N" — never "N chances", which would imply a guarantee the shatter contradicts.
        /// </para>
        /// </summary>
        public static int RollsLeft => Mathf.Max(0, RollCap - RollsUsed);

        /// <summary>Record one roll spent on the current stone.</summary>
        public static void NoteRollSpent()
        {
            PlayerPrefs.SetInt(RollsKey, RollsUsed + 1);
            PlayerPrefs.Save();
            FlowTrace.Step(Sys, $"roll spent: {RollsUsed}/{RollCap} used, up to {RollsLeft} left.");
        }

        /// <summary>
        /// Reset the roll counter — the stone's life ended (it shattered, or its gem was spent in a
        /// recipe), so the next stone starts fresh.
        /// </summary>
        public static void ResetRolls()
        {
            PlayerPrefs.DeleteKey(RollsKey);
            PlayerPrefs.Save();
        }

        /// <summary>Player-facing ASCII counter. Reads as a CEILING, per <see cref="RollsLeft"/>.</summary>
        public static string RollsLeftLabel()
        {
            int left = RollsLeft;
            if (left <= 0) return "No polish attempts left for this stone.";
            return left == 1 ? "Up to 1 more chance." : "Up to " + left + " more chances.";
        }

        /// <summary>Drop every pending score (test/regression hygiene).</summary>
        public static void Clear()
        {
            PlayerPrefs.DeleteKey(PrefKey);
            PlayerPrefs.Save();
        }

        private static List<int> Load()
        {
            var list = new List<int>();
            string raw = PlayerPrefs.GetString(PrefKey, "");
            if (string.IsNullOrEmpty(raw)) return list;
            foreach (var part in raw.Split(','))
            {
                if (int.TryParse(part, out int v))
                    list.Add(Mathf.Clamp(v, 0, DungeonRunGrade.MaxStars));
            }
            return list;
        }

        private static void Save(List<int> q)
        {
            PlayerPrefs.SetString(PrefKey, string.Join(",", q));
            PlayerPrefs.Save();
        }
    }
}
