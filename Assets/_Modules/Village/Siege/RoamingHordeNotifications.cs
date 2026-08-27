// =============================================================================
// RoamingHordeNotifications — earned lookout intel on the phone.
// -----------------------------------------------------------------------------
// Presentation only. It observes SiegeScheduler's cadence, schedules one local
// notification, and never spawns, partitions, tunes, or resolves an encounter.
// Away time still banks pressure; nothing is damaged while the player is absent.
// =============================================================================

using System;
using System.Collections;
using DeNelle.Core.Catalog;
using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
#if UNITY_EDITOR || UNITY_ANDROID || UNITY_IOS
using Unity.Notifications;
#endif
using UnityEngine;

namespace DeNelle.Village
{
    [DisallowMultipleComponent]
    public sealed class RoamingHordeNotifications : MonoBehaviour
    {
        private const int NotificationId = 1179001;
        private const string ChannelId = "lookout_reports";
        private const string PermissionAskedKey = "eoa.lookout.notifications.permission-asked.v1";

        /// <summary>
        /// Live lookout catalog id — the wooden watchtower / archer tower. Display
        /// names are not keys (WO-1184). A catalog row that authors
        /// <see cref="StructureRole.Lookout"/> also counts.
        /// </summary>
        public const string LookoutCatalogId = "tower_ground_archer";

        private static int s_cachedLevel;
        private static float s_cachedAt = -999f;
        private const float CacheSeconds = 1f;

#if UNITY_EDITOR || UNITY_ANDROID || UNITY_IOS
        private NotificationsPermissionRequest _permission;
#endif
        private SiegeScheduler _scheduler;

        public static void Attach(SiegeScheduler scheduler)
        {
            if (scheduler == null || scheduler.GetComponent<RoamingHordeNotifications>() != null) return;
            scheduler.gameObject.AddComponent<RoamingHordeNotifications>();
        }

        private void Awake()
        {
            _scheduler = GetComponent<SiegeScheduler>();
#if UNITY_EDITOR || UNITY_ANDROID || UNITY_IOS
            var args = NotificationCenterArgs.Default;
            args.AndroidChannelId = ChannelId;
            args.AndroidChannelName = "Lookout reports";
            args.AndroidChannelDescription = "Lookout notices about hordes approaching your town.";
            args.PresentationOptions = NotificationPresentation.Alert |
                                       NotificationPresentation.Sound |
                                       NotificationPresentation.Vibrate;
            NotificationCenter.Initialize(args);
#endif
        }

        private void Start()
        {
#if UNITY_EDITOR || UNITY_ANDROID || UNITY_IOS
            // Ask in context, after the player has earned a lookout — never on first boot.
            if (BestLookoutLevel() > 0 && PlayerPrefs.GetInt(PermissionAskedKey, 0) == 0)
            {
                PlayerPrefs.SetInt(PermissionAskedKey, 1);
                PlayerPrefs.Save();
                _permission = NotificationCenter.RequestPermission();
                StartCoroutine(ReleasePermissionRequest());
            }
#endif
        }

#if UNITY_EDITOR || UNITY_ANDROID || UNITY_IOS
        private IEnumerator ReleasePermissionRequest()
        {
            while (_permission != null && _permission.Status == NotificationsPermissionStatus.RequestPending)
                yield return null;
            _permission = null;
        }
#endif

        private void OnApplicationPause(bool paused)
        {
            if (paused) Schedule();
            else Cancel();
        }

        private void OnApplicationFocus(bool focused)
        {
            if (focused) Cancel();
        }

        private void OnApplicationQuit() => Schedule();

        public void Schedule()
        {
#if UNITY_EDITOR || UNITY_ANDROID || UNITY_IOS
            Cancel();
            var state = GameStateService.Instance?.State;
            int lookoutLevel = BestLookoutLevel();
            if (_scheduler == null || state == null || !state.Onboarded || lookoutLevel <= 0) return;
            if (!SiegeClock.TryGetDueIn(state, _scheduler.SiegeIntervalMs, out TimeSpan dueIn)) return;

            // The lookout warns before arrival. Higher tiers buy more preparation time.
            TimeSpan lead = TimeSpan.FromMinutes(lookoutLevel >= 3 ? 60 : lookoutLevel >= 2 ? 30 : 15);
            TimeSpan fireIn = dueIn - lead;
            if (fireIn < TimeSpan.FromSeconds(5)) fireIn = TimeSpan.FromSeconds(5);

            int waveId = Mathf.Max(1, (WaveManager.Instance?.CurrentWaveId ?? 0) + 1);
            if (WaveManager.Instance != null &&
                WaveManager.Instance.TryDescribeUpcomingWave(out int describedWaveId, out _, out _))
                waveId = describedWaveId;
            string size = lookoutLevel >= 3 ? DescribeForceSize(waveId) + " " : string.Empty;
            string timing = FriendlyDuration(dueIn);
            var notification = new Notification
            {
                Identifier = NotificationId,
                Title = "Lookout report",
                Text = $"{size}Horde approaching. Expected at the town in {timing}. Return to defend live.",
                Data = $"roaming-horde:{waveId}",
                ShowInForeground = false
            };

            NotificationCenter.ScheduleNotification(notification,
                new NotificationIntervalSchedule(fireIn));
            FlowTrace.Step("Siege", $"lookout phone report scheduled wave={waveId} fireIn={fireIn.TotalMinutes:F1}m dueIn={dueIn.TotalMinutes:F1}m intel=L{lookoutLevel}");
#endif
        }

        public static void Cancel()
        {
#if UNITY_EDITOR || UNITY_ANDROID || UNITY_IOS
            NotificationCenter.CancelScheduledNotification(NotificationId);
            NotificationCenter.CancelDeliveredNotification(NotificationId);
#endif
        }

        public static string DescribeForceSize(int waveId)
        {
            WaveManager manager = WaveManager.Instance;
            if (manager == null || !manager.TryDescribeUpcomingWave(out int describedId,
                    out bool authoredHeavy, out EnemyCatalog catalog))
                return "An unknown force.";
            int targetWave = waveId > 0 ? waveId : describedId;
            EnemyWaveComposition composition = WaveCompositionBuilder.Build(Mathf.Max(1, targetWave), authoredHeavy, catalog);
            int count = composition?.TotalCount ?? 0;
            if (count <= 6) return "A small raiding party.";
            if (count <= 14) return "A warband.";
            return "A large horde.";
        }

        public static string FriendlyDuration(TimeSpan duration)
        {
            if (duration.TotalMinutes < 2) return "about a minute";
            if (duration.TotalHours < 1) return $"about {Math.Max(5, (int)Math.Round(duration.TotalMinutes / 5.0) * 5)} minutes";
            int hours = Math.Max(1, (int)Math.Round(duration.TotalHours));
            return hours == 1 ? "about an hour" : $"about {hours} hours";
        }

        /// <summary>
        /// True when <paramref name="catalogId"/> is a lookout: the stable
        /// <see cref="LookoutCatalogId"/>, or a catalog row whose role is
        /// <see cref="StructureRole.Lookout"/>. Never matches a display-name
        /// substring.
        /// </summary>
        public static bool IsLookoutCatalogId(string catalogId)
        {
            if (string.IsNullOrEmpty(catalogId)) return false;
            if (string.Equals(catalogId, LookoutCatalogId, StringComparison.OrdinalIgnoreCase))
                return true;
            string role = StructureRoles.RoleOf(catalogId);
            return !string.IsNullOrEmpty(role) &&
                   string.Equals(role, StructureRole.Lookout, StringComparison.OrdinalIgnoreCase);
        }

        public static int BestLookoutLevel()
        {
            if (Time.unscaledTime - s_cachedAt < CacheSeconds) return s_cachedLevel;
            s_cachedLevel = ComputeBestLookoutLevel();
            s_cachedAt = Time.unscaledTime;
            return s_cachedLevel;
        }

        private static int ComputeBestLookoutLevel()
        {
            int best = 0;

            // Persisted player layout — the pause/phone path still has this when
            // the player backgrounds the app. Enemy garrison turrets are not in it.
            var layout = GameStateService.Instance?.State?.BaseLayout;
            if (layout != null)
            {
                for (int i = 0; i < layout.Count; i++)
                {
                    var rec = layout[i];
                    if (!IsLookoutCatalogId(rec.itemId)) continue;
                    best = Mathf.Max(best, rec.level);
                }
            }

            // Live placed markers cover an upgrade this session that is not yet
            // committed to BaseLayout.
            PlacedStructure[] placed = FindObjectsByType<PlacedStructure>();
            if (placed != null)
            {
                for (int i = 0; i < placed.Length; i++)
                {
                    PlacedStructure p = placed[i];
                    if (p == null || !IsLookoutCatalogId(p.itemId)) continue;
                    best = Mathf.Max(best, p.level);
                }
            }

            return best;
        }
    }
}
