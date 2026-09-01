using DeNelle.Core.HUD;
using UnityEngine;
using UnityEngine.EventSystems;

namespace DeNelle.HUD.Kit
{
    /// <summary>Mobile hold gesture for the combat Block medallion. Every exit path releases.</summary>
    [DisallowMultipleComponent]
    public sealed class HudBlockPressRelay : MonoBehaviour, IPointerDownHandler, IPointerUpHandler,
        IPointerExitHandler, ICancelHandler
    {
        private bool _held;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_held) return;
            _held = true;
            HudCommands.Block(true);
        }

        public void OnPointerUp(PointerEventData eventData) => Release();
        public void OnPointerExit(PointerEventData eventData) => Release();
        public void OnCancel(BaseEventData eventData) => Release();
        private void OnDisable() => Release();
        private void OnDestroy() => Release();

        private void Release()
        {
            if (!_held) return;
            _held = false;
            HudCommands.Block(false);
        }
    }
}
