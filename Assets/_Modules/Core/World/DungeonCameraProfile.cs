// =============================================================================
// DungeonCameraProfile — the shared seat + clear colour for a dungeon camera (WO-920).
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Core   Namespace: DeNelle.Core.World
//
// WHY THIS FILE EXISTS — THERE ARE TWO DUNGEON CAMERA PIPELINES, NOT ONE
// ----------------------------------------------------------------------
// WO-920 is written as if the dungeon camera is DeNelle.Dungeons.DungeonCameraRig.
// Verified at source 2026-08-07: that is true for exactly TWO scenes, and they are
// not the ones this quality effort is looking at.
//
//   (A) HAND-BUILT — Dungeon_HealersCottage, Dungeon_FolksGranary.
//       DungeonSceneBuilder.CreateCamera (L2061-2093) BAKES a "Main Camera" with
//       clearFlags=SolidColor / background #070709 + a CinemachineBrain, and the scene
//       carries a CinemachineCamera + DungeonCameraRig + DungeonController. THIS is the
//       rig WO-920 describes (FPV default, ThirdPersonFollow.AvoidObstacles, combat
//       framing swap). It already clears to near-black and always has.
//
//   (B) COMPOSED (Assets/Scenes/DungeonCompose/dg_*.unity, RoomForge/DungeonBaker)
//       and the hand-coded KayKitChallengeOutpost.
//       NEITHER BAKES A CAMERA AT ALL — grepped both .unity files for the Camera class
//       id (!u!20): zero hits. DungeonBaker L230-237 and KayKitChallengeOutpostBuilder
//       L77-78 each say so explicitly and defer to "the runtime rig". That rig is
//       HeroControlEnsurer L283-295: it creates "GameplayCamera (ensured)" and attaches
//       DeNelle.Village.SmartMobileCamera — the VILLAGE third-person camera, running
//       underground. DungeonCameraRig and DungeonController never load in these scenes
//       (HeroControlEnsurer L256 even early-returns its whole camera takeover when a
//       DungeonCameraRig IS present — in pipeline B it never is).
//
// So a "stationary dungeon camera" has to be implemented on BOTH rigs, and the numbers
// must agree or the two pipelines drift apart again. They live here once, in Core, which
// both DeNelle.Village and DeNelle.Dungeons may reference (Village must never reference
// Dungeons — that is a circular asmdef; HeroControlEnsurer L253-255 documents the same).
//
// THE NUMBERS ARE NOT INVENTED. Sources, read at HEAD 2026-08-07:
//   ClearColor #070709   — Assets/Editor/DungeonSceneBuilder.cs L2067
//                          (cam.backgroundColor = HexColor("070709")).
//   CeilingHeightRef 4.0 — Assets/_Modules/Dungeons/RoomForge/RoomForgeCanon.cs L59
//                          (public const float WallHeight = 4f).
//   CellSizeRef 10.0     — RoomForgeCanon.cs L45 (public const float Cell = 10f).
//   Seat 1.9 / 3.2 / 1.5 — WO-920 §3 Phase A.5 (shoulder Y 1.8-2.0, distance 3-3.5),
//                          resolved against the two RoomForgeCanon values above.
//
// ⚠ THE ONE DRIFT RISK, NAMED. RoomForgeCanon lives in DeNelle.Dungeons, which Core and
// Village CANNOT reference, so CeilingHeightRef/CellSizeRef are a CITED MIRROR, not a
// live read — precisely the copied-oracle failure RoomForgeCanon's own header (L13-17)
// warns about. It is not left on trust: DungeonFpvRegression case (6) reads BOTH files
// as text and FAILS if the mirror drifts, or if the seat stops clearing the ceiling.
// =============================================================================

using UnityEngine;

namespace DeNelle.Core.World
{
    /// <summary>
    /// Shared camera presentation for dungeon scenes (see <see cref="HubScenes.IsDungeon"/>).
    /// Pure constants — no behaviour, no allocation.
    /// </summary>
    public static class DungeonCameraProfile
    {
        /// <summary>
        /// Camera clear colour for a dungeon. WO-919 nulled <c>RenderSettings.skybox</c> in every
        /// composed dungeon (DungeonBaker L238) — and with a null skybox,
        /// <see cref="CameraClearFlags.Skybox"/> falls back to clearing with
        /// <c>Camera.backgroundColor</c>, whose Unity default is #314D79 BLUE. The runtime
        /// "GameplayCamera (ensured)" sets NEITHER field, so pipeline (B) still cleared to
        /// daylight blue behind every enclosed room. Value taken from the hand-built dungeon's
        /// long-proven background (DungeonSceneBuilder L2067) so both pipelines match.
        /// <para>FELT-TEST NOTE: the composed fog colour is #0a0a10 (DungeonBaker L221), a hair
        /// lighter than this. Both read as black. If the far end of a corridor ever shows a seam
        /// against the clear, move THIS to the fog value — the fog is the tuned WO-1000 number.</para>
        /// </summary>
        public static readonly Color ClearColor = (Color)new Color32(0x07, 0x07, 0x09, 0xFF);

        /// <summary>
        /// CITED MIRROR of RoomForgeCanon.WallHeight (4 m) — interior clear height of a composed
        /// room. The camera must sit clearly BELOW this or it beds into the WO-919 ceiling slab.
        /// See the header's drift warning; guarded by DungeonFpvRegression case (6).
        /// </summary>
        public const float CeilingHeightRef = 4f;

        /// <summary>CITED MIRROR of RoomForgeCanon.Cell (10 m) — room footprint, the budget
        /// <see cref="CameraDistance"/> has to fit inside. See the header's drift warning.</summary>
        public const float CellSizeRef = 10f;

        /// <summary>
        /// Height of the dungeon camera above the hero's feet. WO-920 §3 Phase A.5 asks for a
        /// 1.8-2.0 shoulder seat that stays clearly under the enclosed ceiling; 1.9 leaves 2.1 m
        /// of headroom under <see cref="CeilingHeightRef"/>. (SmartMobileCamera's village default
        /// is 2.6 — authored for open sky, not for a 4 m lid.)
        /// </summary>
        public const float CameraHeight = 1.9f;

        /// <summary>
        /// How far BEHIND the hero the dungeon camera sits. WO-920 §3 Phase A.5 asks for ~3-3.5.
        /// The village default is 4.5, which inside a <see cref="CellSizeRef"/> room puts the seat
        /// in the wall behind the hero for much of a corridor — the direct cause of the constant
        /// occlusion pull-in / wall-fade that reads as "bounce".
        /// </summary>
        public const float CameraDistance = 3.2f;

        /// <summary>
        /// Look-at height above the hero's feet. MUST be below <see cref="CameraHeight"/> or the
        /// camera tilts UP into the ceiling. The village default (2.5, ABOVE its 2.6 seat) is
        /// near-horizontal by design, because outdoors there is sky worth seeing; 1.5 tips the
        /// view gently DOWN so the floor ahead reads instead.
        /// </summary>
        public const float LookAtHeight = 1.5f;

        /// <summary>
        /// Vertical arm for the Cinemachine ThirdPersonFollow path (pipeline A only) — the small
        /// lift above the shoulder pivot that tips the view down. WO-920 §3 Phase A.5 caps it at
        /// 0.35. <see cref="CameraHeight"/> + this is the pipeline-A camera height, so the pair
        /// must still clear <see cref="CeilingHeightRef"/> (1.9 + 0.35 = 2.25, well under 4).
        /// </summary>
        public const float VerticalArmLength = 0.35f;

        // ═════════════════════════════════════════════════════════════════════
        //  WO-958 — tight-room stability (owner F8 seq 2289: "the camera is
        //  fighting me hard in here ... its auto rotating and it needs to keep
        //  more focus to the room as well as my direction").
        //  Every knob below is dungeon-context ONLY (SmartMobileCamera reads them
        //  solely while its WO-920 dungeon profile is active); town camera never
        //  sees them. Tune HERE — never at the call site.
        // ═════════════════════════════════════════════════════════════════════

        // ── (1) Input owns yaw: the damped facing-recenter ───────────────────
        // The "auto rotating" she felt is SmartMobileCamera's facing-recenter: it
        // is suspended while steering, so every PAUSE in a small room swung the
        // seat behind her facing 0.4 s later at up to 220 deg/s (village tuning).
        // In a dungeon her input owns yaw; the recenter survives only as a slow
        // idle drift (turning it fully off world-locks the seat behind corridor
        // walls — the exact WO-385 failure the recenter exists to prevent).

        /// <summary>
        /// Master switch for the idle facing-recenter in dungeons. TRUE = the damped
        /// drift below; set FALSE to hand yaw to input alone (accepting the WO-385
        /// world-locked-seat trade on corridor turns). Owner-tunable.
        /// </summary>
        public const bool FacingRecenterEnabled = true;

        /// <summary>Seconds of no camera-drag AND no steering before the recenter may drift
        /// (village 0.4 — the pause-then-swing she reported). Long = her input owns yaw.</summary>
        public const float FacingRecenterDelay = 1.25f;

        /// <summary>Hard cap (deg/s) on the recenter swing (village 220 — the whip).</summary>
        public const float FacingRecenterMaxSpeed = 70f;

        /// <summary>Recenter stiffness, 1/sec (village 4). Step = angleError * this, capped
        /// above; ~1.4 reads as a lazy drift that never fights a deliberate look.</summary>
        public const float FacingRecenterStiffness = 1.4f;

        // ── (2) Room-aware framing: the small-room seat ──────────────────────

        /// <summary>
        /// A room whose NARROW footprint extent (min of world X/Z size) is at or under
        /// this is SMALL and gets the tighter seat below. One RoomForge cell is
        /// <see cref="CellSizeRef"/> = 10 m, so 12 catches every 1-cell-wide room and
        /// corridor while a 2x2 (20 m) hall keeps the standard seat.
        /// </summary>
        public const float SmallRoomMaxExtent = 12f;

        /// <summary>Boom length in a SMALL room (standard <see cref="CameraDistance"/> 3.2).
        /// 2.4 keeps the seat off the wall behind the hero in a 10 m room.</summary>
        public const float SmallRoomCameraDistance = 2.4f;

        /// <summary>Seat height in a SMALL room (standard <see cref="CameraHeight"/> 1.9).
        /// Slightly higher over the SAME <see cref="LookAtHeight"/> = the raised pitch the
        /// WO asks for — the floor of the room fills the frame instead of the far wall.
        /// 2.15 + the 20 deg pitch cap still clears the 4 m ceiling (max ~2.9 m).</summary>
        public const float SmallRoomCameraHeight = 2.15f;

        /// <summary>SmoothDamp time (s) easing the seat between the standard and small-room
        /// framing on a room change. Transition, never a snap.</summary>
        public const float RoomSeatSmoothTime = 0.55f;

        /// <summary>Slack (m) the CURRENT room keeps in its containment test so skirting a
        /// doorway edge doesn't flap the room id (and the seat target) every frame.</summary>
        public const float RoomStickySlack = 0.35f;

        // ── (3) Facing focus + ceiling/pitch safety ──────────────────────────

        /// <summary>
        /// Metres the look-at point biases toward the hero's FACING (never velocity) in a
        /// dungeon, so the frame leads where she is pointed. Routed through the existing
        /// lead-point SmoothDamp, so a quick spin moves the aim under a metre, eased —
        /// facing focus without whipping. 0 disables.
        /// </summary>
        public const float FacingLookAhead = 0.8f;

        /// <summary>Dungeon pitch band floor (deg; negative = look up). Village floor is -10;
        /// under a 4 m lid there is no sky worth tilting up for.</summary>
        public const float PanPitchMin = -5f;

        /// <summary>Dungeon pitch band ceiling (deg). Village cap is 35, which rotates the
        /// standard seat to ~3.4 m — a bed-in against the WO-919 ceiling slab. 20 keeps the
        /// rotated seat at ~2.9 m worst case, clear of the 4 m lid.</summary>
        public const float PanPitchMax = 20f;

        /// <summary>Hard backstop: the camera never rises above heroFeetY +
        /// (<see cref="CeilingHeightRef"/> - this), so the lens stays under the ceiling slab
        /// even mid-transition or on geometry the pitch clamp cannot know about. The clamp is
        /// continuous (a min), so engaging it eases — never a pop.</summary>
        public const float CeilingClearance = 0.5f;

        /// <summary>Interval (s) of the [Flow:Camera] dungeon heartbeat Throttle line
        /// (boom / yaw source / room id+size / ceiling clamps) — WO-958's evidence feed.</summary>
        public const float TraceEverySeconds = 2f;
    }
}
