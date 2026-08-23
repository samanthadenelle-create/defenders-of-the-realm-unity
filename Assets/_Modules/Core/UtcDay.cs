// =============================================================================
// UtcDay — THE single UTC calendar-day key ("yyyy-MM-dd"), Core-visible.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core
//
// WHY THIS EXISTS (WO-1134): "once per UTC day" is a rule several systems enforce,
// and every one of them had grown its OWN private copy of the same one-liner. That is
// the duplicated-state failure CLAUDE.md §2/§5/§16 each record: five definitions of
// one truth, none of them reachable by the next feature that needs it, and any format
// or timezone correction has to be found in five places or it silently half-lands.
//
// This is deliberately NOT a clock abstraction. It is the DAY BUCKET only:
//   * UTC, never local — a local-day bucket moves under a travelling player and can
//     hand out a second daily reward on a flight. (Village/Harvest/TimeSource is the
//     server-anchored CLOCK for cooldown windows; that is a different axis and this
//     does not replace it.)
//   * "yyyy-MM-dd", invariant-sortable, and PERSISTED — the strings are already on
//     disk in live saves (GameState.DailyChestDayKey) and in PlayerPrefs, so THE
//     FORMAT IS A SAVE CONTRACT. Changing it silently invalidates every stored stamp,
//     which reads to the player as "my daily reset broke". Do not "improve" it.
//
// MIGRATED SO FAR: DailyChestController.TodayKey (repointed here in the same edit
// that introduced this file).
//
// ⚠ STILL TO MIGRATE — three independent UTC-day copies remain; repointing them was
// out of scope for WO-1134 (they are live monetization paths and a drive-by edit to
// any of them is a structural refactor smuggled into player-facing work):
//     BattlePassService.cs   ~:525
//     MonthlyCardService.cs  ~:92
//     AdGateService.cs       ~:131
// The LOCAL-day variants in DailyQuests.cs / DailyQuestGateBridge.cs are a DIFFERENT
// axis on purpose — do NOT fold those in here without an owner ruling.
//
// Pure + static: no Unity lifecycle, no scene, no save. ASCII-only.
// =============================================================================

using System;
using System.Globalization;

namespace DeNelle.Core
{
    /// <summary>
    /// The UTC calendar-day bucket key. One definition, so "once per day" means the
    /// same thing in every system that enforces it.
    /// </summary>
    public static class UtcDay
    {
        /// <summary>
        /// The persisted day-bucket format. A SAVE CONTRACT (stamps are already on disk) —
        /// and invariant-culture so a device locale can never emit a different string.
        /// </summary>
        public const string Format = "yyyy-MM-dd";

        /// <summary>Today's UTC day key, e.g. "2026-08-23".</summary>
        public static string Key() => Key(DateTime.UtcNow);

        /// <summary>
        /// The UTC day key for an explicit instant — the testable form, so a regression can
        /// assert "tomorrow" without waiting for midnight or mutating the machine clock.
        /// A non-UTC <paramref name="when"/> is converted first, never read as-is.
        /// </summary>
        public static string Key(DateTime when)
        {
            if (when.Kind == DateTimeKind.Local) when = when.ToUniversalTime();
            return when.ToString(Format, CultureInfo.InvariantCulture);
        }
    }
}
