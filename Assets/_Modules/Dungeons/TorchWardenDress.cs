// =============================================================================
// TorchWardenDress — WO-711 owner order (2026-07-13, verbatim): "SKIN THE PILL
// IN HEALERS COTTAGE AS A NPC" + refinement "INTERACTION IS TEACHING TO USE A
// TORCH IN THE DARK".
// -----------------------------------------------------------------------------
// THE PILL: the Healer's Cottage entrance placeholder is Bryn's capsule stand-in
// ("BrynBody", built by Assets/Editor/DungeonSceneBuilder.BuildBryn ~line 1241)
// in the Garden Approach entry room at the layout's bryn position (-31, 0, -2).
// This runtime dresser (NO scene hand-edit, CLAUDE.md paragraph 3):
//   1. HIDES the pill's MeshRenderer (the GameObject + Bryn's bubble anchor stay),
//   2. spawns a Resources/NPCs People-pack body at Bryn's spot, height-normalized
//      + bounds-seated, facing the entrance path (the layout's hero spawn point),
//   3. attaches a proximity Talk (TalkPromptRegistry — the established castle-NPC
//      idiom) that plays dialogues.json row "dun_torch_warden": Bryn teaching the
//      torch/light need. The copy teaches the BUILT dark mechanic (the lantern
//      burns oil; oil stones refill it) — it never promises a button that does
//      not exist (the AtbInventory.Torches USE path is unbuilt as of 2026-07-13).
//   4. On the Talk's first completion, grants 1 torch into AtbInventory.Torches,
//      one-shot behind a SeenTutorials key (the CastleCompanionIntroducerInjector
//      idiom) — so the consumable is in hand the day the use mechanic lands.
//
// The warden gates NOTHING (WO-711 "the DARK does the gating"); every step is
// Guard.Try'd and a failure logs + leaves the pill visible — never a broken run.
// Assembly: DeNelle.Dungeons (references Core + Village, so TalkPromptRegistry /
// MobileInteractButton / AmbientNPC and the Core dialogue stack are all in reach).
// =============================================================================

using DeNelle.Core.Diagnostics;
using DeNelle.Core.State;
using DeNelle.Village;
using UnityEngine;
using UnityEngine.AI;
using CoreDialogue = DeNelle.Core.Dialogue;

namespace DeNelle.Dungeons
{
    /// <summary>
    /// Dresses the Healer's Cottage entrance pill (Bryn's capsule stand-in) as a
    /// real NPC body with a Talk that teaches the torch/light need (WO-711).
    /// Called by <see cref="DungeonController"/> during scene setup, right after
    /// <c>ConfigureBryn</c>. Purely additive; a failure logs and leaves the pill.
    /// </summary>
    public static class TorchWardenDresser
    {
        /// <summary>The scene builder's capsule stand-in child under the "Bryn" root.</summary>
        private const string PillName = "BrynBody";

        /// <summary>The spawned body's name — greppable in a hierarchy dump.</summary>
        private const string BodyName = "TorchWarden_Body (runtime)";

        // People-pack bodies present under Assets/Resources/NPCs (verified 2026-07-13).
        // Bryn is the wandering woman at the threshold — Mevina first, Tob fallback.
        private const string PrimaryBody  = "NPCs/NPC_Peasant_Mevina";
        private const string FallbackBody = "NPCs/NPC_Peasant_Tob";

        /// <summary>The teaching conversation row in dialogues.json (both copies).</summary>
        public const string DialogueId = "dun_torch_warden";

        /// <summary>
        /// Hides the entrance pill and stands a real body in its place, with the
        /// torch-teaching Talk attached. Never throws (every risky op is guarded);
        /// on any failure the pill stays visible and the dungeon runs untouched.
        /// </summary>
        public static void Dress(Bryn bryn, DungeonLayout layout, Transform hero)
        {
            using var _flow = FlowTrace.Enter("Dungeon", "DressEntranceNpc (torch warden)");

            if (bryn == null)
            {
                FlowTrace.Warn("Dungeon",
                    "DressEntranceNpc: no Bryn wired on the controller - entrance pill left " +
                    "as-is (nothing to dress).");
                return;
            }

            // Idempotent: a re-entry (ATB round-trip reload re-runs EnterDungeon in the
            // same scene instance) must not stack bodies.
            Transform existing = bryn.transform.Find(BodyName);
            if (existing != null)
            {
                FlowTrace.Step("Dungeon", "DressEntranceNpc: already dressed - skipping.");
                return;
            }

            // Locate the pill — the builder's capsule child. Missing is non-fatal: the
            // body still spawns at Bryn's root (there is just nothing to hide).
            // BUG (Bryn-is-a-pill): when the baked BrynBody is a KayKit body, its renderer is a
            // SkinnedMeshRenderer on a CHILD — GetComponent<MeshRenderer>() came back null, so the
            // pill/baked body was NEVER hidden (Bryn-body + Mevina overlapped). Broaden to every
            // Renderer (MeshRenderer AND SkinnedMeshRenderer) under the pill, inactive included.
            Transform pill = bryn.transform.Find(PillName);
            Renderer[] pillRenderers = pill != null ? pill.GetComponentsInChildren<Renderer>(true) : null;
            if (pill == null)
                FlowTrace.Warn("Dungeon",
                    $"DressEntranceNpc: pill child '{PillName}' not found under '{bryn.name}' - " +
                    "spawning the body at Bryn's root anyway (nothing to hide).");

            GameObject body = null;
            Guard.Try("Dungeon", "spawn torch-warden body",
                () => body = SpawnBody(bryn.transform, layout));
            if (body == null)
            {
                FlowTrace.Fail("Dungeon",
                    "DressEntranceNpc: body spawn failed - pill left VISIBLE so the NPC spot " +
                    "still reads (degrade, never blank).");
                return;
            }

            // Only hide the pill once a real body verifiably renders (SpawnBody returns
            // null when no enabled renderer came up) — never trade a pill for nothing.
            // Disable ALL of the pill's renderers (MeshRenderer + any child SkinnedMeshRenderer).
            bool pillHidden = false;
            if (pillRenderers != null)
                foreach (var pr in pillRenderers)
                    if (pr != null) { pr.enabled = false; pillHidden = true; }

            Guard.Try("Dungeon", "attach torch-warden Talk", () =>
            {
                var interact = body.AddComponent<TorchWardenInteractable>();
                interact.SetHero(hero);
            });

            FlowTrace.Step("Dungeon",
                $"DressEntranceNpc: pill hidden={(pillHidden ? "yes" : "n/a")} " +
                $"({(pillRenderers != null ? pillRenderers.Length : 0)} renderer(s)), body " +
                $"'{BodyName}' up at {body.transform.position}, Talk plays '{DialogueId}'.");
        }

        // ── Body (the SylasStewardInjector / CastleVendorNpcInjector idiom) ──────

        private static GameObject SpawnBody(Transform brynRoot, DungeonLayout layout)
        {
            GameObject prefab = null;
            string usedKey = null;
            foreach (var key in new[] { PrimaryBody, FallbackBody })
            {
                FlowTrace.Try("Dungeon", $"Resources.Load '{key}'",
                    () => { if (prefab == null) prefab = Resources.Load<GameObject>(key); });
                if (prefab != null) { usedKey = key; break; }
            }
            if (prefab == null)
            {
                FlowTrace.Warn("Dungeon",
                    "DressEntranceNpc: no People-pack body under Resources/NPCs (pack not " +
                    "imported?) - pill stays.");
                return null;
            }

            // Stand at Bryn's spot, facing the entrance path — the layout's hero spawn
            // point (the Keeper walks in from there), falling back to Bryn's authored yaw.
            Vector3 groundPos = brynRoot.position;
            Quaternion rot = brynRoot.rotation;
            if (layout?.spawn != null)
            {
                Vector3 toSpawn = layout.spawn.position.ToWorld() - groundPos;
                toSpawn.y = 0f;
                if (toSpawn.sqrMagnitude > 0.01f)
                    rot = Quaternion.LookRotation(toSpawn.normalized, Vector3.up);
            }

            GameObject go = null;
            FlowTrace.Try("Dungeon", "Instantiate torch-warden body",
                () => go = Object.Instantiate(prefab, groundPos, rot, brynRoot));
            if (go == null)
            {
                FlowTrace.Fail("Dungeon",
                    $"DressEntranceNpc: Instantiate returned null for '{usedKey}' - pill stays.");
                return null;
            }
            go.name = BodyName;

            NormalizeToHeroHeight(go);
            SeatToGround(go, groundPos.y);

            // STATIC body: no wander, no agent (the vendor/steward idiom). The dungeon
            // has no townsfolk systems; AmbientNPC configured wander:false stands still.
            var npc = go.GetComponent<AmbientNPC>();
            if (npc != null) npc.Configure(TownsfolkDialogue.Archetype.Villager, /*wander*/ false, groundPos);
            var agent = go.GetComponent<NavMeshAgent>();
            if (agent != null) agent.enabled = false;

            // Render-verify (§12): a body with no enabled renderer is an invisible NPC —
            // report it and hand back null so the caller keeps the pill visible instead.
            int enabled = 0;
            foreach (var r in go.GetComponentsInChildren<Renderer>(true))
                if (r != null && r.enabled) enabled++;
            if (enabled <= 0)
            {
                FlowTrace.Fail("Dungeon",
                    $"DressEntranceNpc: body from '{usedKey}' has NO enabled renderer - " +
                    "dropping it, pill stays visible.");
                Object.Destroy(go);
                return null;
            }

            FlowTrace.Step("Dungeon",
                $"DressEntranceNpc: body OK from '{usedKey}' ({enabled} enabled renderer(s)).");
            return go;
        }

        /// <summary>Scale the pack body to ~hero height (the shared injector idiom).</summary>
        private static void NormalizeToHeroHeight(GameObject go)
        {
            float scale = 1f;
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length > 0)
            {
                Bounds b = rends[0].bounds;
                for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
                if (b.size.y > 0.01f) scale = 1.95f / b.size.y;
            }
            if (scale > 0.01f && !Mathf.Approximately(scale, 1f))
            {
                go.transform.localScale *= scale;
                var bubbleRoot = go.transform.Find("BubbleRoot");
                if (bubbleRoot != null) bubbleRoot.localScale = Vector3.one / Mathf.Max(0.01f, scale);
            }
        }

        /// <summary>
        /// Seats the body's renderer-bounds bottom on the dungeon floor. Inline
        /// (the shared NpcGroundSeat helper is internal to DeNelle.Village) — the
        /// cottage entry floor is flat authored geometry at the Bryn root's Y, so
        /// a bounds-to-groundY drop is exact here; no raycast needed.
        /// </summary>
        private static void SeatToGround(GameObject go, float groundY)
        {
            var rends = go.GetComponentsInChildren<Renderer>();
            if (rends.Length == 0) return;
            Bounds b = rends[0].bounds;
            for (int i = 1; i < rends.Length; i++) b.Encapsulate(rends[i].bounds);
            float delta = groundY - b.min.y;
            if (Mathf.Abs(delta) > 0.001f)
                go.transform.position += Vector3.up * delta;
        }
    }

    // =========================================================================
    // TorchWardenInteractable — the proximity Talk (mirrors SylasStewardInteractable:
    // TalkPromptRegistry register/deregister on range, suppressed during dialogue /
    // build mode). Talk plays "dun_torch_warden"; the FIRST completion grants one
    // torch into AtbInventory.Torches behind a one-shot SeenTutorials key. The
    // warden never gates anything - Talk is a teach, the DARK does the gating.
    // =========================================================================
    [DisallowMultipleComponent]
    public sealed class TorchWardenInteractable : MonoBehaviour
    {
        private const float ActivateRadius = 6f;

        /// <summary>One-shot grant key in GameState.SeenTutorials (persisted).</summary>
        private const string GrantSeenKey = "dun_torch_warden_torch";

        private Transform _hero;

        /// <summary>The dresser hands over the controller's hero rig (no tag guessing).</summary>
        public void SetHero(Transform hero) => _hero = hero;

        private void OnEnable()
        {
            // Grant rides the dialogue END (the "first completion" beat), not the press.
            CoreDialogue.DialogueService.EndedWithId += OnDialogueEnded;
        }

        private void OnDisable()
        {
            CoreDialogue.DialogueService.EndedWithId -= OnDialogueEnded;
            TalkPromptRegistry.Deregister(transform);
        }

        private void Update()
        {
            if (_hero == null) { ResolveHero(); if (_hero == null) return; }

            // Build mode / open dialogue: drop the prompt (same rules as every NPC).
            if (MobileInteractButton.Suppressed || CoreDialogue.DialogueService.IsRunning)
            {
                TalkPromptRegistry.Deregister(transform);
                return;
            }

            float distSqr = (_hero.position - transform.position).sqrMagnitude;
            if (distSqr <= ActivateRadius * ActivateRadius)
                TalkPromptRegistry.Register(transform, Interact);
            else
                TalkPromptRegistry.Deregister(transform);
        }

        private void Interact()
        {
            if (CoreDialogue.DialogueService.Play(TorchWardenDresser.DialogueId))
                FlowTrace.Step("Dungeon",
                    $"TorchWarden Talk: playing '{TorchWardenDresser.DialogueId}'.");
            else
                FlowTrace.Warn("Dungeon",
                    $"TorchWarden Talk: Play('{TorchWardenDresser.DialogueId}') returned false - " +
                    "row missing from dialogues.json?");
        }

        private void OnDialogueEnded(string dialogueId)
        {
            if (dialogueId != TorchWardenDresser.DialogueId) return;
            Guard.Try("Dungeon", "torch warden one-shot grant", GrantTorchOnce);
        }

        /// <summary>
        /// Grants 1 torch into the persisted AtbInventory, once per save. The torch
        /// USE mechanic is unbuilt (2026-07-13: Torches exists only in the save
        /// schema; the dungeon dark runs on the Lantern oil mechanic) - the grant
        /// pre-seeds the consumable so it is in hand the day the use path lands.
        /// MarkTutorialSeen persists the key AND the inventory bump in one Save().
        /// </summary>
        private static void GrantTorchOnce()
        {
            var svc = GameStateService.Instance;
            var state = svc != null ? svc.State : null;
            if (state == null)
            {
                FlowTrace.Warn("Dungeon",
                    "TorchWarden grant: no GameStateService/state live - grant skipped " +
                    "(teach still delivered).");
                return;
            }
            if (state.SeenTutorials != null &&
                state.SeenTutorials.TryGetValue(GrantSeenKey, out bool seen) && seen)
            {
                FlowTrace.Step("Dungeon", "TorchWarden grant: already granted (one-shot) - skip.");
                return;
            }

            state.Inventory.Torches += 1;
            svc.MarkTutorialSeen(GrantSeenKey);   // one-shot key + Save() (persists both)
            FlowTrace.Step("Dungeon",
                $"TorchWarden grant: +1 torch (Torches={state.Inventory.Torches}), " +
                $"one-shot key '{GrantSeenKey}' set.");
        }

        private void ResolveHero()
        {
            var tagged = GameObject.FindWithTag("Player");
            if (tagged != null) _hero = tagged.transform;
        }
    }
}
