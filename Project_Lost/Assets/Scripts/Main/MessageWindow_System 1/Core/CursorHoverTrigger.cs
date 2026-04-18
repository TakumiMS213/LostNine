using UnityEngine;
using UnityEngine.EventSystems;

namespace MessageWindowSystem.Core
{
    /// <summary>
    /// 任意の UI オブジェクトにアタッチするカーソル変更トリガー。
    /// ポインターが乗ったときに、設定されたカーソル画像を CursorManager に送る。
    /// 離れたときはデフォルトカーソルに戻す。
    /// </summary>
    [RequireComponent(typeof(RectTransform))]
    public class CursorHoverTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        #region Serialized Fields

        [Header("Hover Cursor")]
        [Tooltip("このオブジェクトにカーソルが合ったとき表示するカーソル画像。")]
        [SerializeField] private Texture2D hoverCursor;

        [Tooltip("ホバーカーソルのクリック位置オフセット（左上からのピクセル数）。")]
        [SerializeField] private Vector2 hoverHotspot = Vector2.zero;

        #endregion

        #region Private Fields

        private bool _isHovering;

        #endregion

        #region Pointer Handlers

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (hoverCursor == null || CursorManager.Instance == null) return;

            _isHovering = true;
            CursorManager.Instance.SetCursor(hoverCursor, hoverHotspot);
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (!_isHovering || CursorManager.Instance == null) return;

            _isHovering = false;
            CursorManager.Instance.ResetToDefault();
        }

        #endregion

        #region Unity Lifecycle

        private void OnDisable()
        {
            // オブジェクト無効化時にホバー中ならカーソルを戻す
            if (_isHovering)
            {
                _isHovering = false;
                CursorManager.Instance?.ResetToDefault();
            }
        }

        #endregion
    }
}
