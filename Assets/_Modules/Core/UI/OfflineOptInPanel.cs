// =============================================================================
// OfflineOptInPanel — PROD-010. The opt-in prompt for offline mode, and the
// progress it shows while the one-time CDN pull runs.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core (Core/UI). Code-built uGUI on the Obsidian kit — no UXML
// (UXML does not render in builds, learned the hard way).
//
// OWNER SPEC 2026-08-19: opt IN to offline mode; on yes, a first-time CDN pull of
// everything needed; that pull needs Wi-Fi; afterwards the game falls back to local
// when there is no network.
//
// ⛔ THE SIZE IS SHOWN, ALWAYS, BEFORE ANYTHING DOWNLOADS.
// This is ~88 MB. An earlier plan for this ticket promised the player "10 seconds",
// which was only ever true while PROD-009 was going to shrink the download; the owner
// retired PROD-009 ("PROD 10 kills 10 and 09"), so the honest figure is the whole set:
// roughly 141 s at 5 Mbps and 471 s at 1.5 Mbps. A prompt that hides the number and
// then holds someone for eight minutes has lied to them. The number comes from
// GetDownloadSizeAsync - MEASURED, never typed - so it stays true when content changes.
//
// ASCII-only strings: non-ASCII renders as tofu in TMP.
// Never meaning by colour alone: every state is worded, not just tinted (the owner is
// red/green colourblind).
// =============================================================================

using System;
using System.Collections;
using UnityEngine;
using TMPro;
using DeNelle.Core.Diagnostics;

namespace DeNelle.Core.UI
{
    /// <summary>PROD-010 opt-in prompt + download progress. One at a time.</summary>
    public sealed class OfflineOptInPanel : MonoBehaviour
    {
        private static OfflineOptInPanel s_active;

        private ElarionUiKit.ObsidianModal _modal;
        private TextMeshProUGUI _body;
        private TextMeshProUGUI _progress;
        private bool _downloading;

        // MODAL ARBITER. This builds a 31010-band modal with a scrim, so without a handle
        // PanelManager.AnyOpen stays FALSE while the panel covers the screen: the world
        // interact button stays live underneath, the Android back button has nothing to
        // close, and BattleLock cannot reject it. ONE handle per panel lifetime.
        private PanelHandle _panelHandle;

        private void Awake()
        {
            _panelHandle = PanelManager.Register("Play Offline", DeclineAndClose, IsShowing);
        }

        /// <summary>Arbiter visibility probe (NotifyOpened verifies with it).</summary>
        private bool IsShowing() => _modal != null && _modal.canvas != null && _modal.canvas.activeInHierarchy;

        /// <summary>
        /// Show the prompt. No-op when one is already up, when the player has already
        /// pulled for this build, or when there is nothing to download.
        /// </summary>
        public static void Show()
        {
            if (s_active != null) return;

            if (OfflineContentService.PulledForThisBuild)
            {
                FlowTrace.Step("OfflineContent", "opt-in prompt skipped - already pulled for this build");
                return;
            }

            var go = new GameObject("OfflineOptInPanel");
            var p = go.AddComponent<OfflineOptInPanel>();   // Awake registers the arbiter handle
            DontDestroyOnLoad(go);
            p.Build();

            // The arbiter CAN REJECT (battle-lock) - and on rejection it has already invoked
            // our close hook and torn this panel down. Never force-show over that: an 88 MB
            // download prompt on top of a live battle is the worst possible moment for one.
            if (!PanelManager.NotifyOpened(p._panelHandle))
            {
                FlowTrace.Warn("OfflineContent",
                    "opt-in prompt REJECTED by the modal arbiter (battle-lock) - not shown.");
                return;
            }

            s_active = p;
        }

        private void Build()
        {
            _modal = ElarionUiKit.BuildObsidianModal(
                "OfflineOptInUI", "Play Offline",
                new Vector2(0.10f, 0.24f), new Vector2(0.90f, 0.76f),
                onClose: DeclineAndClose, sortingOrder: 31010);

            var content = _modal.chrome.content.transform;

            _body = ElarionUiKit.Label(content,
                "Download everything now so the game works without a connection?\n\n" +
                "Checking download size...",
                0.46f, 0.86f, ElarionUi.Parchment, 34, TextAlignmentOptions.Top);

            _progress = ElarionUiKit.Label(content, "",
                0.34f, 0.45f, ElarionUi.ParchmentDim, 28, TextAlignmentOptions.Center);

            ElarionUiKit.Button(content, "Download Now", ElarionUiKit.ButtonKind.Gold,
                new Vector2(0.06f, 0.10f), new Vector2(0.48f, 0.26f), OnDownload);

            ElarionUiKit.Button(content, "Not Now", ElarionUiKit.ButtonKind.Quiet,
                new Vector2(0.52f, 0.10f), new Vector2(0.94f, 0.26f), DeclineAndClose);

            StartCoroutine(ShowSize());
        }

        private IEnumerator ShowSize()
        {
            yield return OfflineContentService.GetDownloadSize(bytes =>
            {
                if (_body == null) return;

                if (bytes <= 0)
                {
                    // Everything is already cached. Do not offer a download that would do
                    // nothing - record the state and get out of the player's way.
                    FlowTrace.Step("OfflineContent", "size = 0 - already fully cached; marking offline-ready");
                    OfflineContentService.SetOptedIn(true);
                    _body.text = "Everything is already downloaded. This game will work without a connection.";
                    _progress.text = "";
                    return;
                }

                float mb = bytes / (1024f * 1024f);
                // State the minutes, not just the megabytes: megabytes mean nothing to most
                // people and the whole point is that they are not surprised by the wait.
                string estimate = EstimateText(bytes);
                _body.text =
                    "Download everything now so the game works without a connection?\n\n" +
                    $"Size: {mb:F0} MB\n{estimate}\n\n" +
                    "You need Wi-Fi for this one-time download. After it finishes the game " +
                    "opens without a connection.";
            });
        }

        /// <summary>Honest range, both ends. One number would read as a promise.</summary>
        private static string EstimateText(long bytes)
        {
            float bits = bytes * 8f;
            int fast = Mathf.RoundToInt(bits / 5_000_000f);    // 5 Mbps
            int slow = Mathf.RoundToInt(bits / 1_500_000f);    // 1.5 Mbps
            string F(int s) => s >= 90 ? $"{Mathf.RoundToInt(s / 60f)} min" : $"{s} sec";
            return $"About {F(fast)} on a fast connection, up to {F(slow)} on a slow one.";
        }

        private void OnDownload()
        {
            if (_downloading) return;
            _downloading = true;
            _body.text = "Downloading content. Keep this screen open.";
            StartCoroutine(RunDownload());
        }

        private IEnumerator RunDownload()
        {
            yield return OfflineContentService.DownloadAllForOffline(
                pct => { if (_progress != null) _progress.text = $"{Mathf.RoundToInt(pct * 100f)}%"; },
                ok =>
                {
                    _downloading = false;
                    if (_body == null) return;
                    if (ok)
                    {
                        _body.text = "Done. This game now works without a connection.";
                        _progress.text = "";
                    }
                    else
                    {
                        // Never claim success on a partial pull - the service deliberately
                        // does not stamp it, and the message must match that truth.
                        _body.text = "The download did not finish. You can try again any time; " +
                                     "the game still works normally with a connection.";
                        _progress.text = "";
                    }
                });
        }

        private void DeclineAndClose()
        {
            if (_downloading) return;   // do not let a stray tap abandon a live download
            OfflineContentService.SetOptedIn(false);
            Close();
        }

        private void Close()
        {
            PanelManager.NotifyClosed(_panelHandle);   // no-op if the arbiter already swapped us out
            if (_modal != null && _modal.canvas != null) Destroy(_modal.canvas);
            if (s_active == this) s_active = null;
            Destroy(gameObject);
        }

        private void OnDestroy()
        {
            if (s_active == this) s_active = null;
        }
    }
}
