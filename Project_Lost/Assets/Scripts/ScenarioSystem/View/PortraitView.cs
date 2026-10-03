using System;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using ScenarioSystem.Events;
using ScenarioSystem.Model.Actions;

namespace ScenarioSystem.View
{
    /// <summary>
    /// ポートレート表示を担当する View。
    /// EventBus の OnDialogueRequested を購読し、ポートレート画像・位置・ジャンプ演出を行う。
    /// ゴーストポートレート（前の話者の残像）も管理する。
    /// </summary>
    public class PortraitView : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Portrait")]
        [SerializeField] private Image portraitImage;
        [SerializeField] private RectTransform portraitLeftAnchor;
        [SerializeField] private RectTransform portraitCenterAnchor;
        [SerializeField] private RectTransform portraitRightAnchor;

        [Header("Ghost Portrait")]
        [SerializeField] private Image ghostPortraitImage;
        [SerializeField] private bool enableGhostPortrait = true;
        [SerializeField, Range(0f, 1f)] private float ghostPortraitAlpha = 0.3f;

        [Header("Animation")]
        [SerializeField] private bool jumpOnText = true;
        [SerializeField] private float jumpHeight = 50f;
        [SerializeField] private float jumpDuration = 0.3f;
        [SerializeField] private Ease jumpEase = Ease.OutBounce;

        #endregion

        #region Private Fields

        private string _previousSpeakerName;
        private Sprite _previousPortrait;
        private PortraitPosition _previousPortraitPosition;
        private Sequence _jumpTween;
        private Vector2 _jumpOrigin;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            MemorizerTintMask.MarkPortrait(portraitImage);
            MemorizerTintMask.MarkPortrait(ghostPortraitImage);
        }

        private void OnEnable()
        {
            ScenarioEventBus.OnDialogueRequested += HandleDialogue;
            ScenarioEventBus.OnScenarioEnded += HandleScenarioEnded;
            ScenarioEventBus.OnWindowVisibilityChanged += HandleWindowVisibilityChanged;
        }

        private void OnDisable()
        {
            StopJump();
            ScenarioEventBus.OnDialogueRequested -= HandleDialogue;
            ScenarioEventBus.OnScenarioEnded -= HandleScenarioEnded;
            ScenarioEventBus.OnWindowVisibilityChanged -= HandleWindowVisibilityChanged;
        }

        #endregion

        #region Event Handlers

        private void HandleDialogue(DialogueEventData data)
        {
            UpdateGhostPortrait(data);
            UpdatePortrait(data);

            _previousSpeakerName = data.SpeakerName;
            _previousPortrait = data.Portrait;
            _previousPortraitPosition = data.PortraitPosition;
        }

        private void HandleScenarioEnded(Model.ScenarioData _)
        {
            StopJump();
            HideGhostPortrait();
            _previousSpeakerName = null;
            _previousPortrait = null;
        }

        private void HandleWindowVisibilityChanged(bool visible)
        {
            if (!visible)
            {
                StopJump();
                // メッセージウィンドウが閉じた時、ポートレートを Center に移動する
                if (portraitImage != null && portraitImage.gameObject.activeSelf)
                {
                    SetPortraitPosition(portraitImage.rectTransform, PortraitPosition.Center);
                }
            }
        }

        #endregion

        #region Portrait Logic

        private void UpdatePortrait(DialogueEventData data)
        {
            if (portraitImage == null) return;
            StopJump();

            if (data.Portrait == null)
            {
                SetImageAlpha(portraitImage, 0f);
                return;
            }

            portraitImage.sprite = data.Portrait;
            portraitImage.gameObject.SetActive(true);
            SetImageAlpha(portraitImage, 1f);
            SetPortraitPosition(portraitImage.rectTransform, data.PortraitPosition);

            if (jumpOnText) PlayJump();
        }

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

        private void PlayJump()
        {
            var rect = portraitImage.GetComponent<RectTransform>();
            if (rect == null) return;

            _jumpOrigin = rect.anchoredPosition;
            var target = _jumpOrigin + Vector2.up * jumpHeight;

            _jumpTween = DOTween.Sequence().SetLink(gameObject);
            _jumpTween.Append(rect.DOAnchorPos(target, jumpDuration * 0.5f).SetEase(Ease.OutQuad));
            _jumpTween.Append(rect.DOAnchorPos(_jumpOrigin, jumpDuration * 0.5f).SetEase(jumpEase));
        }

        private void StopJump()
        {
            if (_jumpTween == null) return;
            _jumpTween.Kill();
            _jumpTween = null;
            if (portraitImage != null)
                portraitImage.rectTransform.anchoredPosition = _jumpOrigin;
        }

        private static void SetImageAlpha(Image image, float alpha)
        {
            if (image == null) return;

            var color = image.color;
            color.a = Mathf.Clamp01(alpha);
            image.color = color;
        }

        #endregion

        #region Ghost Portrait

        private void UpdateGhostPortrait(DialogueEventData data)
        {
            if (ghostPortraitImage == null || !enableGhostPortrait)
            {
                HideGhostPortrait();
                return;
            }

            if (data.Portrait == null)
            {
                HideGhostPortrait();
                return;
            }

            // 新たに中央（Center）で喋る場合は、既存のゴーストを強制的に消す
            if (data.PortraitPosition == PortraitPosition.Center)
            {
                HideGhostPortrait();
                return;
            }

            bool hasPrevious = _previousPortrait != null;
            bool speakerChanged = !string.Equals(data.SpeakerName, _previousSpeakerName, StringComparison.Ordinal);
            bool positionChanged = data.PortraitPosition != _previousPortraitPosition;

            if (speakerChanged || positionChanged)
            {
                if (hasPrevious)
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
            // 話者も位置も変わらない（連続して同じキャラが喋る）場合は、
            // 新しいゴーストを生成せず、また現在のゴーストも消さずに維持する。
        }

        private void HideGhostPortrait()
        {
            if (ghostPortraitImage != null)
                ghostPortraitImage.gameObject.SetActive(false);
        }

        #endregion
    }
}
