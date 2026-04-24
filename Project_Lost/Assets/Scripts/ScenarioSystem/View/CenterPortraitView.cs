using UnityEngine;
using UnityEngine.UI;
using ScenarioSystem.Events;
using ScenarioSystem.Model.Actions;

namespace ScenarioSystem.View
{
    /// <summary>
    /// 画面中央に常時配置するポートレートの表示を担当する View。
    /// メッセージウィンドウ側の PortraitView とは独立して動作するが、
    /// 既存 Portrait が Center 位置にいる場合は自動的に非表示になる。
    /// </summary>
    public class CenterPortraitView : MonoBehaviour
    {
        [Header("UI Reference")]
        [Tooltip("画面中央に配置する Image コンポーネント")]
        [SerializeField] private Image portraitImage;

        private Sprite _currentSprite;
        private bool _isMainPortraitAtCenter;

        private void OnEnable()
        {
            ScenarioEventBus.OnCenterPortraitChanged += HandleCenterPortraitChanged;
            ScenarioEventBus.OnDialogueRequested += HandleDialogueRequested;
            ScenarioEventBus.OnWindowVisibilityChanged += HandleWindowVisibilityChanged;
        }

        private void OnDisable()
        {
            ScenarioEventBus.OnCenterPortraitChanged -= HandleCenterPortraitChanged;
            ScenarioEventBus.OnDialogueRequested -= HandleDialogueRequested;
            ScenarioEventBus.OnWindowVisibilityChanged -= HandleWindowVisibilityChanged;
        }

        private void HandleCenterPortraitChanged(Sprite sprite)
        {
            _currentSprite = sprite;
            RefreshVisibility();
        }

        private void HandleDialogueRequested(DialogueEventData data)
        {
            // 既存 Portrait が Center にいるかどうかを追跡する
            _isMainPortraitAtCenter = (data.Portrait != null && data.PortraitPosition == PortraitPosition.Center);
            RefreshVisibility();
        }

        private void HandleWindowVisibilityChanged(bool visible)
        {
            if (!visible)
            {
                // ウィンドウが閉じた = 既存 Portrait は Center に戻る
                // → CenterPortrait は競合するので非表示を維持
                _isMainPortraitAtCenter = true;
                RefreshVisibility();
            }
        }

        /// <summary>
        /// 表示条件: スプライトが設定されていて、かつ既存 Portrait が Center にいない時のみ表示
        /// </summary>
        private void RefreshVisibility()
        {
            if (portraitImage == null) return;

            bool shouldShow = _currentSprite != null && !_isMainPortraitAtCenter;

            if (shouldShow)
            {
                portraitImage.sprite = _currentSprite;
                portraitImage.gameObject.SetActive(true);
            }
            else
            {
                portraitImage.gameObject.SetActive(false);
            }
        }
    }
}
