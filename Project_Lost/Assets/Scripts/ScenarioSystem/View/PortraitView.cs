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

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            ScenarioEventBus.OnDialogueRequested += HandleDialogue;
            ScenarioEventBus.OnScenarioEnded += HandleScenarioEnded;
        }

        private void OnDisable()
        {
            ScenarioEventBus.OnDialogueRequested -= HandleDialogue;
            ScenarioEventBus.OnScenarioEnded -= HandleScenarioEnded;
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
            HideGhostPortrait();
            _previousSpeakerName = null;
            _previousPortrait = null;
        }

        #endregion

        #region Portrait Logic

        private void UpdatePortrait(DialogueEventData data)
        {
            if (portraitImage == null) return;

            if (data.Portrait != null)
            {
                portraitImage.sprite = data.Portrait;
                portraitImage.gameObject.SetActive(true);
                SetPortraitPosition(portraitImage.rectTransform, data.PortraitPosition);

                if (jumpOnText) PlayJump();
            }
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

            rect.DOKill();
            var original = rect.anchoredPosition;
            var target = original + Vector2.up * jumpHeight;

            var seq = DOTween.Sequence();
            seq.Append(rect.DOAnchorPos(target, jumpDuration * 0.5f).SetEase(Ease.OutQuad));
            seq.Append(rect.DOAnchorPos(original, jumpDuration * 0.5f).SetEase(jumpEase));
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

            bool hasPrevious = _previousPortrait != null;
            bool speakerChanged = !string.Equals(data.SpeakerName, _previousSpeakerName, StringComparison.Ordinal);
            bool positionChanged = data.PortraitPosition != _previousPortraitPosition;

            if (hasPrevious && data.Portrait != null && (speakerChanged || positionChanged))
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

        #endregion
    }
}
