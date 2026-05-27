// =============================================================================
// EconomyService — DEF-78 (Linear). Singleton resource manager referenced by
// TowerPlacementSystem.CanPlaceTower() and TowerUpgradeButton.OnUpgradeClicked().
// Implemented VERBATIM to the DEF-78 spec (MonoBehaviour singleton in
// DeNelle.Village, own Wood/Stone/Iron/Crystals fields, Wood-only CanAfford/Spend
// stub). The only addition is a self-bootstrap so Instance exists at runtime
// without hand-wiring it into the scene (matches the project's other singletons);
// it changes none of the spec'd API. Multi-resource overload is a future ticket.
// =============================================================================

using UnityEngine;
using UnityEngine.Events;

namespace DeNelle.Village
{
    public class EconomyService : MonoBehaviour
    {
        public static EconomyService Instance;

        [SerializeField] private int _wood     = 200;
        [SerializeField] private int _stone    = 150;
        [SerializeField] private int _iron     = 80;
        [SerializeField] private int _crystals = 50;

        public int Wood     => _wood;
        public int Stone    => _stone;
        public int Iron     => _iron;
        public int Crystals => _crystals;

        public UnityEvent OnResourcesChanged;

        // Self-install so Instance is never null at runtime (no scene authoring
        // required). No-op if a scene-placed EconomyService already exists.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject("EconomyService");
            DontDestroyOnLoad(go);
            go.AddComponent<EconomyService>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
        }

        private void OnDestroy() { if (Instance == this) Instance = null; }

        public bool CanAfford(int cost) => _wood >= cost; // expand to multi-resource later

        public void Spend(int cost)
        {
            if (!CanAfford(cost)) return; // guard — never go negative
            _wood -= cost;
            OnResourcesChanged?.Invoke();
        }
    }
}
