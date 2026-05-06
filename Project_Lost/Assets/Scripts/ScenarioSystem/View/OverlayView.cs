using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ScenarioSystem.Events;
using ScenarioSystem.Model.Actions;

namespace ScenarioSystem.View
{
    /// <summary>
    /// オーバーレイメッセージウィンドウの表示を担当する View。
    /// EventBus 経由で OverlayRequested / OverlayDismissed を受け取る。
    /// </summary>
    public class OverlayView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private GameObject overlayRoot;
        [SerializeField] private TMP_Text speakerNameText;
        [SerializeField] private TMP_Text overlayText;
        [SerializeField] private Image portraitImage;

        [Header("Portrait Anchors")]
        [SerializeField] private RectTransform portraitLeftAnchor;
        [SerializeField] private RectTransform portraitCenterAnchor;
        [SerializeField] private RectTransform portraitRightAnchor;

        [Header("Ghost Portrait")]
        [SerializeField] private Image ghostPortraitImage;
        [SerializeField] private bool enableGhostPortrait = true;
        [SerializeField, Range(0f, 1f)] private float ghostPortraitAlpha = 0.3f;

        // ゴースト管理用ステート
        private Sprite _previousPortrait;
        private PortraitPosition _previousPortraitPosition;

        private void Awake()
        {
            if (overlayRoot != null) overlayRoot.SetActive(false);
        }

        private void OnEnable()
        {
            ScenarioEventBus.OnOverlayRequested += HandleOverlayRequested;
            ScenarioEventBus.OnOverlayDismissed += HandleOverlayDismissed;
            ScenarioEventBus.OnWindowVisibilityChanged += HandleWindowVisibilityChanged;
        }

        private void OnDisable()
        {
            ScenarioEventBus.OnOverlayRequested -= HandleOverlayRequested;
            ScenarioEventBus.OnOverlayDismissed -= HandleOverlayDismissed;
            ScenarioEventBus.OnWindowVisibilityChanged -= HandleWindowVisibilityChanged;
        }

        private void HandleOverlayRequested(OverlayEventData data)
        {
            if (overlayRoot != null) overlayRoot.SetActive(true);
            if (speakerNameText != null) speakerNameText.text = data.SpeakerName ?? string.Empty;
            if (overlayText != null) overlayText.text = data.Text;
            
            // ゴーストの更新
            UpdateGhostPortrait(data);

            if (portraitImage != null)
            {
                if (data.Portrait != null)
                {
                    portraitImage.sprite = data.Portrait;
                    portraitImage.gameObject.SetActive(true);
                    SetPortraitPosition(portraitImage.rectTransform, data.PortraitPosition);
                }
                else
                {
                    portraitImage.gameObject.SetActive(false);
                }
            }

            // 現在の状態を記録
            _previousPortrait = data.Portrait;
            _previousPortraitPosition = data.PortraitPosition;
        }

        private void HandleOverlayDismissed()
        {
            if (overlayRoot != null) overlayRoot.SetActive(false);
            if (speakerNameText != null) speakerNameText.text = string.Empty;
            ClearGhostState();
        }

        private void HandleWindowVisibilityChanged(bool visible)
        {
            // シナリオが完全に終了してメインウィンドウが消えた時、念のためオーバーレイも確実に消す（フェイルセーフ）
            if (!visible)
            {
                if (overlayRoot != null) overlayRoot.SetActive(false);
                if (speakerNameText != null) speakerNameText.text = string.Empty;
                ClearGhostState();
            }
        }

        /// <summary>
        /// ユーザーのクリック（タップ）でシナリオを先に進める場合に使用します。
        /// Canvas内の ClickArea (Button) の OnClickイベントに紐付けてください。
        /// </summary>
        public void OnUserInput()
        {
            // Presenterに対して「次へ進む」リクエストを送信する
            ScenarioEventBus.RaiseAdvanceRequested();
        }

        #region Position and Ghost Logic

        private void SetPortraitPosition(RectTransform rect, PortraitPosition position)
        {
            RectTransform anchor = position switch
            {
                PortraitPosition.Left => portraitLeftAnchor,
                PortraitPosition.Right => portraitRightAnchor,
                _ => portraitCenterAnchor
            };

            if (anchor != null)
                rect.anchoredPosition = anchor.anchoredPosition;
        }

        private void UpdateGhostPortrait(OverlayEventData data)
        {
            if (ghostPortraitImage == null || !enableGhostPortrait)
            {
                HideGhostPortrait();
                return;
            }

            bool hasPrevious = _previousPortrait != null;
            // 話者名がないため、「画像が違う」または「位置が違う」場合にゴースト化する
            bool portraitChanged = data.Portrait != _previousPortrait;
            bool positionChanged = data.PortraitPosition != _previousPortraitPosition;

            if (hasPrevious && data.Portrait != null && (portraitChanged || positionChanged))
            {
                ghostPortraitImage.sprite = _previousPortrait;
                ghostPortraitImage.gameObject.SetActive(true);
                SetPortraitPosition(ghostPortraitImage.rectTransform, _previousPortraitPosition);

                var color = ghostPortraitImage.color;
                color.a = ghostPortraitAlpha;
                ghostPortraitImage.color = color;
            }
            else
            {
                HideGhostPortrait();
            }
        }

        private void HideGhostPortrait()
        {
            if (ghostPortraitImage != null)
                ghostPortraitImage.gameObject.SetActive(false);
        }

        private void ClearGhostState()
        {
            HideGhostPortrait();
            _previousPortrait = null;
        }

        #endregion
    }
}
