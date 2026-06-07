using UnityEngine;

namespace DeNelle.Sandbox
{
    /// <summary>
    /// Multi-level "Elden Ring style" deep dungeon generator (Grok-authored, reproduced).
    /// Builds <see cref="levels"/> stacked levels, each dropped -7 units below the one above,
    /// using KayKit dungeon prefabs for structure and Apocalypse_M pieces for debris/details.
    ///
    /// Reproduced faithfully from the Grok source with two robustness additions:
    ///   1. Null-guards on EVERY Instantiate so a missing prefab logs a warning instead of
    ///      throwing (per CLAUDE.md §4 — missing prefab = LogWarning, not error).
    ///   2. A per-level <see cref="SeatLevelPieces"/> grounding pass (the float-fix) that
    ///      measures each piece's renderer bounds and seats its base on the level's floor
    ///      (heightOffset), mirroring SeatAllPieces in EnemyOutpostBuilder.
    ///
    /// Productionization note: only LEVEL 0 (the top, heightOffset 0) is made walkable by the
    /// tester. Multi-level NavMesh descent across the -7 drops needs an OffMeshLink / multi-
    /// surface pass and should be lifted into a real DungeonSceneBuilder. See the tester.
    /// </summary>
    [ExecuteInEditMode]
    public class DeepDungeonBuilder : MonoBehaviour
    {
        [Header("=== Dungeon Configuration ===")]
        public int width = 16;
        public int depth = 12;
        public int levels = 3;                    // How deep (like Elden Ring)
        public float unitSize = 2f;

        [Header("=== KayKit Dungeon Prefabs ===")]
        public GameObject floorTile;
        public GameObject wallStraight;
        public GameObject wallCorner;
        public GameObject archway;
        public GameObject stairsDown;
        public GameObject pillar;

        [Header("=== Apocalypse_M Details ===")]
        public GameObject barrier;
        public GameObject damagedBarrier;
        public GameObject barricadeWindow;
        public GameObject[] debrisPrefabs;        // Barrels, Blood, Broken Beds, etc.

        [Header("=== Resources (Scarce deeper down) ===")]
        public GameObject healthItem;
        public GameObject magicItem;

        // The per-level container currently being filled. All level pieces are parented here
        // so SeatLevelPieces can ground exactly that level's pieces on the level's floor.
        private Transform _currentLevelRoot;

        [ContextMenu("Build Full Deep Dungeon")]
        public void BuildDeepDungeon()
        {
            ClearExisting();

            for (int level = 0; level < levels; level++)
            {
                float height = level * -7f; // Drop 7 units per level
                BuildLevel(level, height);
            }

            // Random/contextual encounters wired to OUR real ATB battle, plus one
            // guaranteed boss encounter at the deepest level.
            AddEncounters();

            Debug.Log($"✅ {levels}-Level Deep Dungeon Built! (Elden Ring Style)");
        }

        void BuildLevel(int level, float heightOffset)
        {
            // Per-level container so the seat pass grounds exactly this level's pieces. The
            // container itself sits at the origin; pieces keep their world heightOffset.
            var levelGo = new GameObject($"Level_{level}");
            levelGo.transform.SetParent(transform, false);
            levelGo.transform.localPosition = Vector3.zero;
            _currentLevelRoot = levelGo.transform;

            // Floor
            for (int x = 0; x < width; x++)
            for (int z = 0; z < depth; z++)
            {
                Vector3 pos = new Vector3(x * unitSize, heightOffset, z * unitSize);
                Spawn(floorTile, pos, Quaternion.identity, "floorTile");
            }

            BuildOuterWalls(heightOffset, level);
            BuildInternalStructure(level, heightOffset);
            AddApocalypticDetails(level, heightOffset);
            PlaceResources(level, heightOffset);

            if (level < levels - 1)
                PlaceStairsToNextLevel(heightOffset);

            // FLOAT-FIX: seat this level's pieces on the level's floor (heightOffset) by
            // measuring renderer bounds, so varying prefab pivots don't leave them floating.
            SeatLevelPieces(heightOffset);
        }

        void BuildOuterWalls(float height, int level)
        {
            // More damaged on deeper levels
            float damageChance = 0.3f + (level * 0.2f);

            for (int x = 0; x < width; x++)
            {
                PlaceWall(x, 0, height, damageChance);
                PlaceWall(x, depth - 1, height, damageChance);
            }
            for (int z = 0; z < depth; z++)
            {
                PlaceWall(0, z, height, damageChance);
                PlaceWall(width - 1, z, height, damageChance);
            }
        }

        void PlaceWall(int x, int z, float height, float damageChance)
        {
            Vector3 pos = new Vector3(x * unitSize, height + 2.5f, z * unitSize);
            GameObject prefab = (Random.value < damageChance && damagedBarrier != null) ? damagedBarrier : barrier;

            GameObject wall = Spawn(prefab, pos, Quaternion.identity, "wall (barrier/damagedBarrier)");
            if (wall == null) return; // missing prefab — skip the crenellation that rides on it

            if (x == 0 || x == width - 1)
                wall.transform.rotation = Quaternion.Euler(0, 90, 0);

            // Add crenellations on upper levels
            if (barricadeWindow != null && Random.value < 0.6f)
            {
                Vector3 crenPos = pos + new Vector3(0, 2f, 0);
                Spawn(barricadeWindow, crenPos, wall.transform.rotation, "barricadeWindow");
            }
        }

        void BuildInternalStructure(int level, float height)
        {
            if (level == 0) // Upper level - more open
            {
                CreateRoom(3, 3, 7, 6, height);
                CreateRoom(10, 4, 13, 8, height);
            }
            else if (level == 1) // Middle - more complex
            {
                CreateRoom(2, 2, 6, 5, height);
                CreateRoom(9, 3, 14, 7, height);
                CreateRoom(4, 8, 11, 10, height);
            }
            else // Deep level - dangerous and cramped
            {
                CreateRoom(3, 3, 13, 9, height);
            }
        }

        void CreateRoom(int startX, int startZ, int endX, int endZ, float height)
        {
            for (int x = startX; x <= endX; x++)
            for (int z = startZ; z <= endZ; z++)
            {
                if (x == startX || x == endX || z == startZ || z == endZ)
                    PlaceWall(x, z, height, 0.4f);
            }
        }

        void PlaceStairsToNextLevel(float currentHeight)
        {
            if (stairsDown == null) return;
            Vector3 pos = new Vector3(width * unitSize * 0.5f, currentHeight + 0.1f, depth * unitSize * 0.5f);
            Spawn(stairsDown, pos, Quaternion.Euler(0, 45, 0), "stairsDown");
        }

        void AddApocalypticDetails(int level, float height)
        {
            if (debrisPrefabs == null || debrisPrefabs.Length == 0) return;

            int amount = 40 + level * 15; // More debris deeper down

            for (int i = 0; i < amount; i++)
            {
                int x = Random.Range(1, width - 1);
                int z = Random.Range(1, depth - 1);
                Vector3 pos = new Vector3(x * unitSize + Random.Range(-0.8f, 0.8f), height + 0.3f, z * unitSize + Random.Range(-0.8f, 0.8f));

                GameObject prefab = debrisPrefabs[Random.Range(0, debrisPrefabs.Length)];
                var obj = Spawn(prefab, pos,
                                Quaternion.Euler(0, Random.Range(0, 360), Random.Range(-30f, 30f)), "debrisPrefabs");
                if (obj != null)
                    obj.transform.localScale *= Random.Range(0.7f, 1.6f);
            }
        }

        void PlaceResources(int level, float height)
        {
            int amount = Mathf.Max(8 - level * 2, 2); // Scarcer deeper

            for (int i = 0; i < amount; i++)
            {
                int x = Random.Range(3, width - 3);
                int z = Random.Range(3, depth - 3);
                Vector3 pos = new Vector3(x * unitSize, height + 1f, z * unitSize);

                if (Random.value < 0.6f && healthItem != null)
                    Spawn(healthItem, pos, Quaternion.identity, "healthItem");
                else if (magicItem != null)
                    Spawn(magicItem, pos, Quaternion.identity, "magicItem");
            }
        }

        // ================================================================
        // ENCOUNTERS — random/contextual + one boss, wired to OUR real ATB
        // ================================================================
        // Places (8 + levels*4) EncounterTrigger boxes at interior floor positions,
        // more/deeper levels = more triggers and a harder enemy family/larger
        // roster (EncounterEnemySet scales by level). One GUARANTEED boss encounter
        // is placed in a room on the deepest level (encounterChance = 1). Each
        // trigger fires OUR ATB battle via SceneRouter.GoBattle (see EncounterTrigger).
        // Null-safe: a level with no floor footprint is simply skipped.
        [Header("=== Encounters ===")]
        [Tooltip("Enemy family ids per depth tier (index 0 = top). Deeper levels pick " +
                 "the heavier family; the last entry is reused for levels beyond it.")]
        public string[] encounterFamiliesByDepth = new[] { "skeleton", "bruiser", "necromancer" };

        [Tooltip("Family id for the guaranteed boss at the deepest level.")]
        public string bossFamilyId = "hollow-king";

        [Tooltip("Half-extent (metres) of each cubic encounter trigger box (~3m box).")]
        public float encounterTriggerSize = 1.5f;

        public void AddEncounters()
        {
            int total = 8 + Mathf.Max(0, levels) * 4;
            int deepest = Mathf.Max(0, levels - 1);

            // Distribute the triggers across levels, weighted toward deeper levels
            // (deeper = more dangerous). Level L gets a share proportional to (L+1).
            int weightSum = 0;
            for (int l = 0; l < levels; l++) weightSum += (l + 1);
            if (weightSum <= 0) weightSum = 1;

            var encRoot = new GameObject("Encounters");
            encRoot.transform.SetParent(transform, false);

            int placed = 0;
            for (int level = 0; level < levels; level++)
            {
                float height = level * -7f;
                int share = Mathf.Max(1, Mathf.RoundToInt(total * ((level + 1f) / weightSum)));
                if (level == levels - 1) share = Mathf.Max(share, total - placed); // mop up the remainder deep

                for (int i = 0; i < share && placed < total; i++)
                {
                    // Interior floor position (inside the outer walls).
                    int x = Random.Range(2, Mathf.Max(3, width - 2));
                    int z = Random.Range(2, Mathf.Max(3, depth - 2));
                    Vector3 pos = new Vector3(x * unitSize, height + encounterTriggerSize, z * unitSize);
                    string family = FamilyForDepth(level);
                    CreateEncounterBox(encRoot.transform, pos, level, family, 0.5f, isBoss: false);
                    placed++;
                }
            }

            // ONE guaranteed BOSS encounter, centred in a room at the deepest level.
            {
                float bossHeight = deepest * -7f;
                Vector3 bossPos = new Vector3(
                    width * unitSize * 0.5f,
                    bossHeight + encounterTriggerSize,
                    depth * unitSize * 0.5f);
                CreateEncounterBox(encRoot.transform, bossPos, deepest,
                    string.IsNullOrEmpty(bossFamilyId) ? "hollow-king" : bossFamilyId,
                    1f, isBoss: true);
            }

            Debug.Log($"[DeepDungeonBuilder] Placed {placed} random encounter(s) + 1 boss " +
                      $"across {levels} level(s), all wired to ATB via SceneRouter.GoBattle.");
        }

        // Pick the enemy family for a depth tier; clamps to the last configured entry.
        string FamilyForDepth(int level)
        {
            if (encounterFamiliesByDepth == null || encounterFamiliesByDepth.Length == 0)
                return "skeleton";
            int idx = Mathf.Clamp(level, 0, encounterFamiliesByDepth.Length - 1);
            string f = encounterFamiliesByDepth[idx];
            return string.IsNullOrEmpty(f) ? "skeleton" : f;
        }

        // Spawn one trigger-box GameObject with a BoxCollider (isTrigger) + an
        // EncounterTrigger configured for this level/family. Null-safe.
        void CreateEncounterBox(Transform parent, Vector3 pos, int level, string family,
                                float chance, bool isBoss)
        {
            if (parent == null) parent = transform;

            var go = new GameObject(isBoss ? $"BossEncounter_L{level}" : $"Encounter_L{level}");
            go.transform.SetParent(parent, false);
            go.transform.position = pos;

            float s = Mathf.Max(0.5f, encounterTriggerSize);
            var box = go.AddComponent<BoxCollider>();
            box.isTrigger = true;
            box.size = new Vector3(s * 2f, s * 2f, s * 2f); // ~3m cube at default 1.5 half-extent

            var trig = go.AddComponent<EncounterTrigger>();
            trig.encounterChance = isBoss ? 1f : Mathf.Clamp01(chance);
            trig.enemySet = new EncounterEnemySet
            {
                level = level,
                familyId = family,
                isBoss = isBoss,
            };
        }

        void ClearExisting()
        {
            while (transform.childCount > 0)
                DestroyImmediate(transform.GetChild(0).gameObject);
        }

        // ================================================================
        // SEAT PASS — fix floating pieces on a level's floor (pivot-agnostic)
        // ================================================================
        // For each piece on the current level, measure its combined world-space renderer
        // bounds and shift it on Y so its BASE sits at this level's floor (heightOffset).
        // This removes the float caused by varying prefab pivots without touching placement
        // logic. Debris are intentionally lifted slightly (height + 0.3) by placement; the
        // seat grounds them to the floor — acceptable for clutter. Mirrors SeatAllPieces in
        // EnemyOutpostBuilder. Walls are placed at height + 2.5; seating to the floor keeps
        // their base on the floor regardless of pivot.
        void SeatLevelPieces(float heightOffset)
        {
            if (_currentLevelRoot == null) return;

            for (int i = 0; i < _currentLevelRoot.childCount; i++)
            {
                Transform piece = _currentLevelRoot.GetChild(i);
                if (piece == null) continue;

                var renderers = piece.GetComponentsInChildren<Renderer>();
                if (renderers == null || renderers.Length == 0) continue;

                // Combined world-space renderer bounds for this piece.
                Bounds b = renderers[0].bounds;
                for (int r = 1; r < renderers.Length; r++)
                    b.Encapsulate(renderers[r].bounds);

                // Shift on Y so bounds.min.y == this level's floor.
                float delta = heightOffset - b.min.y;
                if (Mathf.Abs(delta) > 0.0001f)
                {
                    Vector3 p = piece.position;
                    p.y += delta;
                    piece.position = p;
                }
            }
        }

        // ================================================================
        // SPAWN HELPER — null-guarded Instantiate
        // ================================================================
        // Instantiate under the current level root. If the prefab is null, log a warning
        // (per CLAUDE.md §4: a missing pack prefab is a warning, not an error) and return
        // null so callers can skip dependent placement.
        GameObject Spawn(GameObject prefab, Vector3 pos, Quaternion rot, string label)
        {
            if (prefab == null)
            {
                Debug.LogWarning($"[DeepDungeonBuilder] Skipped '{label}' — prefab is null (pack not imported?).");
                return null;
            }
            Transform parent = _currentLevelRoot != null ? _currentLevelRoot : transform;
            return Instantiate(prefab, pos, rot, parent);
        }
    }
}
