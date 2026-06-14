// =============================================================================
// InteractableSign — a small, ALWAYS-VISIBLE world-space "what is this?" sign that
// floats above every interactable (shop / upgrade / talk / pet / spell vendor) so
// the player can read its TYPE from a distance, BEFORE walking up.
// -----------------------------------------------------------------------------
// Assembly: DeNelle.Village   Namespace: DeNelle.Village
//
// Ticket T-034 (recurring owner ask, 2026-06-13): "must add some symbol like
// suggested earlier — cannot tell what this interactable is — would prefer a sign
// with a symbol like most games." The proximity "[ Tap / F ] Interact" prompt
// (BuildingInteractable.ShowPrompt / MobileInteractButton) only appears once you
// are RIGHT next to a structure and only says "Interact" — it does not tell you a
// shop from an upgrade from a quest-giver. This sign is the at-a-distance affordance.
//
// DISTINCT from the proximity prompt:
//   • the proximity prompt is a chat-bubble that says "Interact" and only shows in
//     range — it is the ACTION affordance.
//   • this sign is a small framed ICON that is ALWAYS up — it is the IDENTITY
//     affordance. The two never fight: the sign sits higher and never says "Interact".
//
// LOOK (town HUD language — dark glass + gold):
//   a tiny framed plate built from sprites: a gold rim quad behind a dark-glass
//   backdrop quad, with the type icon centred on top. All three are SpriteRenderers
//   on one billboarded root, so the whole sign turns to face the camera as a unit.
//
// ART: reuses the EXISTING RpgUiCatalog bronze RPG-icon set (Resources/RpgUi/icons)
//   — no new art is invented. When the pack is not imported (icon sprite is null)
//   the sign degrades to a coloured plate with NO icon rather than throwing, exactly
//   like every other RpgUiCatalog caller (sprite-first, null-tolerant).
//
// PERF: zero per-frame allocation. The icon plate is built ONCE in Build(); the only
//   per-frame work is a single transform.rotation assignment in LateUpdate (the
//   billboard), which is alloc-free. Materials/sprites are shared. No FindObject in
//   Update. Heavy enough work (sprite load) is cached by RpgUiCatalog.
//
// Village -> Core only (RpgUiCatalog lives in DeNelle.Core.UI). No reflection.
// =============================================================================

using DeNelle.Core.UI;
using UnityEngine;

namespace DeNelle.Village
{
    /// <summary>
    /// A small billboarded "type sign" floating above an interactable. Attach via the
    /// static <see cref="ForBuilding"/> / <see cref="ForStructureId"/> helpers, which
    /// resolve the interactable's TYPE to an icon from <see cref="SignKind"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class InteractableSign : MonoBehaviour
    {
        // How high above the interactable's origin the sign floats. Sits ABOVE the
        // proximity bubble (ProximityHeightAboveBuilding = 3.2) so the two read as two
        // layers (identity on top, action below) instead of overlapping.
        private const float DefaultHeight = 4.2f;

        // Plate sizing (world units). Small + readable; tuned to the bronze icon art.
        private const float PlateWidth  = 0.95f;
        private const float PlateHeight = 0.95f;
        private const float RimPad      = 0.12f;   // gold rim extends this far past the glass
        private const float IconInset   = 0.18f;   // icon sits this far inside the glass

        // Town-HUD palette (dark glass + gold), matching the proximity bubble's gold rim.
        private static readonly Color GlassColor = new Color(0.06f, 0.05f, 0.10f, 0.92f); // dark glass
        private static readonly Color GoldColor  = new Color(1f, 0.78f, 0.32f, 1f);       // bright gold rim

        private Camera _camera;

        // ── Type → icon table (the single source of truth for T-034) ──────────────
        // Maps an interactable's TYPE to an icon role+name in RpgUiCatalog. If a type
        // has no obvious pack icon it falls back to a sensible default + we FLAG it in
        // the comment so the owner can swap in dedicated art later.
        public enum SignKind
        {
            Shop,     // a Buy/Sell vendor (market / jeweler)            → chest/bag icon
            Upgrade,  // a resource / forge upgrade building             → shield icon (FLAG: no anvil)
            Smith,    // the FORGE (weapon smith)                        → sword icon (owner)
            Talk,     // a pure talk / quest-giver NPC                   → speech/person icon
            Pet,      // the pet / companion house                      → heart icon
            Spell,    // the arcane tower (spell upgrades)              → quest/scroll icon (FLAG: no rune)
            Generic   // unknown — never blank, always a readable mark   → quest icon (FLAG: generic)
        }

        /// <summary>Resolves the icon SPRITE NAME (within RpgUiCatalog's "icons" role) for a kind.
        /// FLAGGED entries are approximations — no dedicated pack art exists for them yet.</summary>
        private static string IconNameFor(SignKind kind)
        {
            switch (kind)
            {
                case SignKind.Shop:    return RpgUiCatalog.IconInventory; // chest/bag = "buy/sell goods"
                case SignKind.Upgrade: return RpgUiCatalog.IconShield;    // FLAG: shield stands in for anvil/up-arrow
                case SignKind.Smith:   return RpgUiCatalog.IconSword;     // forge = weapon smith → sword (owner)
                case SignKind.Talk:    return RpgUiCatalog.IconTalk;      // person/speech = "talk"
                case SignKind.Pet:     return RpgUiCatalog.IconHeart;     // heart = companion
                case SignKind.Spell:   return RpgUiCatalog.IconQuest;     // FLAG: scroll stands in for a spell/rune
                default:               return RpgUiCatalog.IconQuest;     // FLAG: generic fallback (never blank)
            }
        }

        // ── Structure-id → kind (mirrors BuildingInteractable.StructureHookIdFor ids
        //    + CastleVendorNpcInjector structure ids) ─────────────────────────────
        // shop vs upgrade is a CAPABILITY (ARCHITECTURE §2b): keep this aligned with
        // BuildingCatalog.IsShoppable / IsUpgradable. We classify by id here because
        // both interactables already key their dialogue off the same id, and several
        // (forge) are BOTH — we pick the player's PRIMARY read for the single sign.
        private static SignKind KindForStructureId(string structureId)
        {
            string id = (structureId ?? string.Empty).ToLowerInvariant();
            if (id.Length == 0) return SignKind.Generic;
            if (id.Contains("pet"))                              return SignKind.Pet;        // pet-house / echo hollow
            if (id.Contains("arcane"))                           return SignKind.Spell;      // arcane-tower
            if (id.Contains("market") || id.Contains("jewel"))   return SignKind.Shop;       // market / jeweler
            // Forge = weapon smith → sword sign (owner). Armorer stays Upgrade (shield = armor).
            if (id.Contains("forge"))                            return SignKind.Smith;
            if (id.Contains("armorer"))                          return SignKind.Upgrade;
            if (id.Contains("lumbermill") || id.Contains("farm") ||
                id.Contains("mine")   || id.Contains("windmill")) return SignKind.Upgrade;   // resource upgrade
            if (id.Contains("workshop"))                         return SignKind.Upgrade;    // crafting bench
            return SignKind.Talk; // any other named vendor reads as a talk/quest-giver
        }

        /// <summary>Classifies a <see cref="BuildingType"/> for buildings authored without an id.</summary>
        private static SignKind KindForBuildingType(BuildingType type)
        {
            switch (type)
            {
                case BuildingType.PetHouse:    return SignKind.Pet;
                case BuildingType.ArcaneTower: return SignKind.Spell;
                case BuildingType.Workshop:    return SignKind.Upgrade;
                case BuildingType.CrystalMine: return SignKind.Upgrade;   // resource → shield
                case BuildingType.Farm:        return SignKind.Upgrade;   // resource → shield
                case BuildingType.Lumbermill:  return SignKind.Upgrade;   // resource → shield
                case BuildingType.Forge:       return SignKind.Smith;     // forge → sword
                case BuildingType.Armorer:     return SignKind.Upgrade;   // armorer → shield (armor)
                default:                        return SignKind.Generic;
            }
        }

        // ── Attach helpers ────────────────────────────────────────────────────────
        /// <summary>
        /// Attaches a type sign above a building interactable. Classifies by the
        /// building's structure id first (shop/upgrade/pet/spell), then its enum type.
        /// No-op if the building already has a sign. Returns the sign (or null on fail).
        /// </summary>
        public static InteractableSign ForBuilding(Building building, string structureId)
        {
            if (building == null) return null;
            SignKind kind = !string.IsNullOrEmpty(structureId)
                ? KindForStructureId(structureId)
                : KindForBuildingType(building.Type);
            return Attach(building.gameObject, kind, DefaultHeight);
        }

        /// <summary>
        /// Attaches a type sign above a structure keyed only by its id (the castle
        /// vendor NPCs have no Building component). No-op if one already exists.
        /// </summary>
        public static InteractableSign ForStructureId(GameObject host, string structureId, float height)
        {
            if (host == null) return null;
            return Attach(host, KindForStructureId(structureId), height);
        }

        /// <summary>Core attach: spawns the billboarded plate once. Idempotent per host.</summary>
        private static InteractableSign Attach(GameObject host, SignKind kind, float height)
        {
            if (host == null) return null;
            // Idempotent: one sign per host (a re-inject / re-load must not stack signs).
            var existing = host.GetComponentInChildren<InteractableSign>();
            if (existing != null) return existing;

            var go = new GameObject("InteractableSign");
            go.transform.SetParent(host.transform, false);
            go.transform.localPosition = Vector3.up * height;
            // Neutralize the host's lossy scale so the plate is the SAME world size on
            // every interactable (vendor bodies are rescaled to hero height; buildings
            // are scale-1). Counter the parent's lossy scale into local scale.
            Vector3 parentScale = host.transform.lossyScale;
            go.transform.localScale = new Vector3(
                parentScale.x > 0.0001f ? 1f / parentScale.x : 1f,
                parentScale.y > 0.0001f ? 1f / parentScale.y : 1f,
                parentScale.z > 0.0001f ? 1f / parentScale.z : 1f);
            var sign = go.AddComponent<InteractableSign>();
            sign.Build(kind);
            return sign;
        }

        // ── Build (once) ──────────────────────────────────────────────────────────
        private void Build(SignKind kind)
        {
            _camera = Camera.main;

            // Gold rim (back-most, slightly larger) → dark glass → icon (front-most).
            BuildQuad("Rim",   PlateWidth + RimPad, PlateHeight + RimPad, GoldColor,  0,  null);
            BuildQuad("Glass", PlateWidth,          PlateHeight,          GlassColor, 1,  null);

            Sprite icon = RpgUiCatalog.Get(RpgUiCatalog.RoleIcons, IconNameFor(kind));
            if (icon != null)
            {
                BuildQuad("Icon",
                    PlateWidth  - IconInset * 2f,
                    PlateHeight - IconInset * 2f,
                    Color.white, 2, icon);
            }
            else
            {
                // Pack not imported — degrade to a coloured plate (no icon) rather than
                // throw. Same null-tolerant contract every RpgUiCatalog caller honours.
                Debug.Log($"[InteractableSign] icon '{IconNameFor(kind)}' for {kind} not in pack — " +
                          "plate shown without icon (run Defenders/Art/Import RPG UI Pack).");
            }
        }

        /// <summary>
        /// Builds one layer of the sign as a SpriteRenderer (so transparency + sorting
        /// are free and no custom shader/material is allocated per instance — the sprite
        /// supplies the material). A null sprite paints a solid coloured quad via a 1x1
        /// white sprite tinted by <paramref name="color"/>.
        /// </summary>
        private void BuildQuad(string layerName, float w, float h, Color color, int order, Sprite sprite)
        {
            var go = new GameObject(layerName);
            go.transform.SetParent(transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, -0.001f * order); // toward camera by layer
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = sprite != null ? sprite : SolidSprite();
            sr.color = color;
            sr.sortingOrder = order;
            sr.drawMode = SpriteDrawMode.Sliced; // size by transform scale, not pixels
            sr.size = new Vector2(w, h);
        }

        // ── Billboard (the only per-frame work; alloc-free) ───────────────────────
        private void LateUpdate()
        {
            if (_camera == null) { _camera = Camera.main; if (_camera == null) return; }
            // Face the camera as a flat plate (look along the camera's forward so the
            // sign stays upright + readable, not tilted toward the camera position).
            transform.rotation = _camera.transform.rotation;
        }

        // ── Shared 1x1 white sprite for solid-colour quads (built once, cached) ───
        private static Sprite _solid;
        private static Sprite SolidSprite()
        {
            if (_solid != null) return _solid;
            var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            tex.SetPixel(0, 0, Color.white);
            tex.Apply();
            tex.wrapMode = TextureWrapMode.Clamp;
            _solid = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            _solid.name = "InteractableSign_Solid";
            return _solid;
        }
    }
}
