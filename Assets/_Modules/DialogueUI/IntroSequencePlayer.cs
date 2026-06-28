// =============================================================================
// IntroSequencePlayer — the ~30s skippable image-slate opening (WO-561).
// -----------------------------------------------------------------------------
// YARN REMOVED (WO-557): the old 9-screen Yarn cinematic is gone. This is a tight,
// code-built uGUI image-slate sequence on OUR own presentation kit (ElarionUiKit
// black+gold) — NO Yarn, NO UXML. Five beats (~5.5s each ≈ 30s) tell the Hollow /
// dimming-Heart hook and the Knight's call to reclaim the light, then it hands off
// to hero select.
//
// SKIPPABLE: a full-screen tap advances to the next slate (impatient players click
// straight through); a visible "Skip" button (and any keyboard key) ends the intro
// immediately and jumps to the next boot step. "Design it well, but fast for people
// who want to move right in" (owner).
//
// Decoupled trigger: registers itself on Core's IntroLauncher.Play at startup so the
// Title screen's "Play Intro" button (DeNelle.Onboarding) fires it WITHOUT a hard
// reference to this assembly — exactly as before.
//
// ART: each slate loads Resources.Load<Sprite>(image). The owner generates the five
// slates per docs/ART/INTRO_IMAGE_SLATES.md and drops them at those Resources paths.
// A missing sprite degrades to caption-on-black (LogWarning, never a hard fault).
// =============================================================================

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DeNelle.Core;
using DeNelle.Core.UI;
using DeNelle.Core.Audio;

namespace DeNelle.DialogueUI
{
    /// <summary>Registers + launches the skippable image-slate intro.</summary>
    public static class IntroSequencePlayer
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Register()
        {
            DeNelle.Core.IntroLauncher.Play = Play;
        }

        /// <summary>Play the cinematic intro from the first slate. Spawns a self-contained,
        /// DontDestroyOnLoad driver so it survives the hand-off frame to hero select.</summary>
        public static void Play()
        {
            // If one is somehow already running, don't double-spawn.
            if (UnityEngine.Object.FindObjectOfType<IntroSequenceDriver>() != null) return;

            var go = new GameObject("IntroSequence");
            UnityEngine.Object.DontDestroyOnLoad(go);
            go.AddComponent<IntroSequenceDriver>();
            Debug.Log("[IntroSequencePlayer] Playing image-slate intro (Yarn-free).");
        }
    }

    /// <summary>One image slate: a background sprite, a caption, and how long it holds.</summary>
    internal struct IntroSlate
    {
        public string Image;     // Resources path (no extension) to the slate sprite
        public string Caption;   // evocative narration line
        public float Hold;       // seconds to hold before auto-advancing
        public bool TitleCard;   // last slate overlays the game title

        public IntroSlate(string image, string caption, float hold, bool titleCard = false)
        { Image = image; Caption = caption; Hold = hold; TitleCard = titleCard; }
    }

    /// <summary>Drives the slate sequence: builds the uGUI overlay, runs the timeline,
    /// handles tap/key/Skip, then routes to hero select. Code-built, no UXML.</summary>
    [DisallowMultipleComponent]
    internal sealed class IntroSequenceDriver : MonoBehaviour
    {
        // The five beats (~5.5s each + dips ≈ 30s). Captions are canon (see STORY_BIBLE_POLISH.md).
        private static readonly IntroSlate[] Slates =
        {
            new IntroSlate("Intro/intro-heart-ablaze",
                "Once, the Heart of Elarion blazed — a world-tree whose light was the breath of all living things.", 5.5f),
            new IntroSlate("Intro/intro-dimming",
                "Then came the Dimming: a grief older than memory, and the Heart's light began to fail.", 5.5f),
            new IntroSlate("Intro/intro-hollow-ones",
                "The Hollow Ones rose — not monsters, but the broken, drawn to the last warmth they could feel.", 5.5f),
            new IntroSlate("Intro/intro-knight-call",
                "One answered. A knight, Grom, sworn to carry a single ember back into the dark.", 5.5f),
            new IntroSlate("Intro/intro-reclaim",
                "Drive back the dark. Let the Heart grow. Reclaim the light of Elarion.", 6f, titleCard: true),
        };

        private const float DipSeconds = 0.35f;   // dip-to-black transition between slates

        private GameObject _canvas;
        private Image _slateImage;
        private TextMeshProUGUI _caption;
        private TextMeshProUGUI _title;
        private Image _dip;                        // top black overlay used for dip transitions
        private int _index = -1;
        private bool _ending;
        private Coroutine _run;

        private void Start()
        {
            Build();
            CoreServices.Audio?.PlayMusic(MusicTrack.Title);
            _run = StartCoroutine(RunSequence());
        }

        private void Update()
        {
            // Any keyboard key ends the intro (skip straight in). Tap/Skip are uGUI buttons.
            var kb = UnityEngine.InputSystem.Keyboard.current;
            if (kb != null && kb.anyKey.wasPressedThisFrame) EndIntro();
        }

        // ── Build the overlay (ElarionUiKit black+gold) ───────────────────────
        private void Build()
        {
            _canvas = ElarionUiKit.BuildModalCanvas("IntroCanvas", sortingOrder: 6000);
            _canvas.transform.SetParent(transform, false);
            Transform root = _canvas.transform;

            // Black backdrop (also catches no-image slates).
            var bg = NewImage(root, "Backdrop", Color.black);
            Stretch(bg.rectTransform, 0f, 0f, 1f, 1f);

            // The slate sprite, full-screen.
            _slateImage = NewImage(root, "Slate", Color.white);
            Stretch(_slateImage.rectTransform, 0f, 0f, 1f, 1f);
            _slateImage.preserveAspect = false;
            _slateImage.enabled = false;   // shown once a sprite is assigned

            // Full-screen tap target: advances to the NEXT slate (click straight through).
            var tap = ElarionUiKit.Button(root, "", ElarionUiKit.ButtonKind.Quiet,
                Vector2.zero, Vector2.one, AdvanceSlate);
            var tapImg = tap.GetComponent<Image>();
            if (tapImg != null) tapImg.color = new Color(0f, 0f, 0f, 0f);   // invisible, still raycasts

            // Caption band: a translucent black strip with a thin gold rule on top.
            var band = NewImage(root, "CaptionBand", new Color(0.02f, 0.02f, 0.025f, 0.72f));
            Stretch(band.rectTransform, 0.06f, 0.07f, 0.94f, 0.24f);
            band.raycastTarget = false;
            var rule = NewImage(band.transform, "GoldRule", ElarionUi.Gold);
            var rr = rule.rectTransform;
            rr.anchorMin = new Vector2(0f, 1f); rr.anchorMax = new Vector2(1f, 1f);
            rr.offsetMin = new Vector2(0f, -3f); rr.offsetMax = new Vector2(0f, 0f);
            rule.raycastTarget = false;

            _caption = ElarionUiKit.Label(band.transform, "", 0.10f, 0.92f,
                new Color(0.93f, 0.90f, 0.82f, 1f), 40, TextAlignmentOptions.Center,
                0.05f, 0.95f, spacing: 0.5f);

            // Title card (last slate only).
            _title = ElarionUiKit.Label(root, "", 0.40f, 0.62f, ElarionUi.Gold, 92,
                TextAlignmentOptions.Center, 0.05f, 0.95f, spacing: 2f, bold: true);
            _title.text = "";
            var sub = ElarionUiKit.Label(root, "", 0.34f, 0.40f,
                new Color(0.93f, 0.90f, 0.82f, 1f), 40, TextAlignmentOptions.Center);
            sub.name = "Subtitle";
            sub.text = "";
            _subtitle = sub;

            // Visible Skip button (top-right) — ends the intro immediately.
            ElarionUiKit.Button(root, "Skip  ›", ElarionUiKit.ButtonKind.Gold,
                new Vector2(0.74f, 0.92f), new Vector2(0.96f, 0.975f), EndIntro);

            // Dip overlay on top of everything — starts opaque so the first slate fades in.
            _dip = NewImage(root, "Dip", Color.black);
            Stretch(_dip.rectTransform, 0f, 0f, 1f, 1f);
            _dip.raycastTarget = false;
            SetAlpha(_dip, 1f);
        }

        private TextMeshProUGUI _subtitle;

        // ── Timeline ──────────────────────────────────────────────────────────
        private IEnumerator RunSequence()
        {
            for (int i = 0; i < Slates.Length; i++)
            {
                if (_ending) yield break;
                ShowSlate(i);
                yield return FadeDip(1f, 0f, DipSeconds);   // fade FROM black into the slate
                float t = 0f;
                while (t < Slates[i].Hold && !_ending && _index == i)
                { t += Time.deltaTime; yield return null; }
                if (_ending) yield break;
                if (_index == i && i < Slates.Length - 1)
                    yield return FadeDip(0f, 1f, DipSeconds); // dip to black before the next
            }
            EndIntro();
        }

        private void ShowSlate(int i)
        {
            _index = i;
            var s = Slates[i];

            var sprite = Resources.Load<Sprite>(s.Image);
            if (sprite != null) { _slateImage.sprite = sprite; _slateImage.enabled = true; }
            else
            {
                _slateImage.enabled = false;
                Debug.LogWarning($"[IntroSequence] slate sprite '{s.Image}' not found — caption-on-black " +
                                 "(generate art per docs/ART/INTRO_IMAGE_SLATES.md).");
            }

            _caption.text = s.Caption;
            if (s.TitleCard)
            {
                _title.text = "DEFENDERS OF THE REALM";
                if (_subtitle != null) _subtitle.text = "Echoes of Elarion";
            }
        }

        // Skip the CURRENT slate (tap) — jump the timeline forward one beat.
        private void AdvanceSlate()
        {
            if (_ending) return;
            if (_index >= Slates.Length - 1) { EndIntro(); return; }
            // Bumping the index makes the active hold loop exit; restart the timeline at the next slate.
            int next = _index + 1;
            if (_run != null) StopCoroutine(_run);
            _run = StartCoroutine(JumpTo(next));
        }

        private IEnumerator JumpTo(int next)
        {
            yield return FadeDip(0f, 1f, 0.18f);   // quick dip
            for (int i = next; i < Slates.Length; i++)
            {
                if (_ending) yield break;
                ShowSlate(i);
                yield return FadeDip(1f, 0f, DipSeconds);
                float t = 0f;
                while (t < Slates[i].Hold && !_ending && _index == i)
                { t += Time.deltaTime; yield return null; }
                if (_ending) yield break;
                if (_index == i && i < Slates.Length - 1)
                    yield return FadeDip(0f, 1f, DipSeconds);
            }
            EndIntro();
        }

        private IEnumerator FadeDip(float from, float to, float seconds)
        {
            float t = 0f;
            SetAlpha(_dip, from);
            while (t < seconds && !_ending)
            { t += Time.deltaTime; SetAlpha(_dip, Mathf.Lerp(from, to, t / seconds)); yield return null; }
            SetAlpha(_dip, to);
        }

        // ── End / hand-off ────────────────────────────────────────────────────
        private void EndIntro()
        {
            if (_ending) return;
            _ending = true;
            if (_run != null) StopCoroutine(_run);
            // Black out instantly so the scene swap is never seen mid-fade, then route.
            if (_dip != null) SetAlpha(_dip, 1f);
            Debug.Log("[IntroSequence] Intro complete — routing to hero select.");
            SceneRouter.GoHeroSelect();
            Destroy(gameObject);
        }

        // ── uGUI helpers ──────────────────────────────────────────────────────
        private static Image NewImage(Transform parent, string name, Color color)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(parent, false);
            var img = go.GetComponent<Image>();
            img.color = color;
            return img;
        }

        private static void Stretch(RectTransform r, float x0, float y0, float x1, float y1)
        {
            r.anchorMin = new Vector2(x0, y0); r.anchorMax = new Vector2(x1, y1);
            r.offsetMin = Vector2.zero; r.offsetMax = Vector2.zero;
        }

        private static void SetAlpha(Image img, float a)
        {
            if (img == null) return;
            var c = img.color; c.a = a; img.color = c;
        }
    }
}
