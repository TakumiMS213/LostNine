using System;
using UnityEngine;
using TMPro;
using DG.Tweening;
using ScenarioSystem.Events;
using ScenarioSystem.Model.Actions;

namespace ScenarioSystem.View
{
    /// <summary>
    /// 話者名の表示・スライドアニメーションを担当する View。
    /// EventBus の OnDialogueRequested を購読し、話者名が変わった時にスライド演出を行う。
    /// </summary>
    public class SpeakerNameView : MonoBehaviour
    {
        #region Serialized Fields

        [Header("UI Reference")]
        [SerializeField] private TMP_Text speakerNameText;

        [Header("Slide Animation")]
        [SerializeField] private bool animateName = true;
        [SerializeField] private float slideDistance = 600f;
        [SerializeField] private float slideDuration = 0.35f;
        [SerializeField] private Ease slideEase = Ease.OutCubic;

        #endregion

        #region Private Fields

        private Vector2 _originalAnchored;
        private string _previousSpeakerName;
        private bool _originalCaptured;
        private Tween _slideTween;

        #endregion

        #region Setup

        /// <summary>レイアウト変更後の位置を、以降の話者名スライドの着地点にする。</summary>
        public void SetRestingPosition(Vector2 position)
        {
            if (speakerNameText == null) return;
            _originalAnchored = position;
            _originalCaptured = true;
            var rect = speakerNameText.rectTransform;
            StopSlide();
            rect.anchoredPosition = position;
        }

        public void Configure(TMP_Text text, bool animate = true)
        {
            speakerNameText = text;
            animateName = animate;
            if (speakerNameText == null) return;

            _originalAnchored = speakerNameText.rectTransform.anchoredPosition;
            _originalCaptured = true;
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (speakerNameText != null)
            {
                _originalAnchored = speakerNameText.rectTransform.anchoredPosition;
                _originalCaptured = true;
            }
        }

        private void OnEnable()
        {
            ScenarioEventBus.OnDialogueRequested += HandleDialogue;
            ScenarioEventBus.OnScenarioEnded += HandleScenarioEnded;
        }

        private void OnDisable()
        {
            StopSlide();
            ScenarioEventBus.OnDialogueRequested -= HandleDialogue;
            ScenarioEventBus.OnScenarioEnded -= HandleScenarioEnded;
        }

        #endregion

        #region Event Handlers

        private void HandleDialogue(DialogueEventData data)
        {
            if (speakerNameText == null) return;

            speakerNameText.text = data.SpeakerName ?? string.Empty;

            if (animateName)
                AnimateSpeakerName(data.SpeakerName, data.NameSlideDirection);

            _previousSpeakerName = data.SpeakerName;
        }

        private void HandleScenarioEnded(Model.ScenarioData _)
        {
            StopSlide();
            _previousSpeakerName = null;
        }

        #endregion

        #region Animation

        private void AnimateSpeakerName(string newName, NameSlideDirection direction)
        {
            if (speakerNameText == null) return;
            if (string.Equals(newName, _previousSpeakerName, StringComparison.Ordinal)) return;

            var rt = speakerNameText.rectTransform;
            if (!_originalCaptured)
            {
                _originalAnchored = rt.anchoredPosition;
                _originalCaptured = true;
            }

            bool fromRight = direction switch
            {
                NameSlideDirection.Right => true,
                _ => false
            };

            float dir = fromRight ? 1f : -1f;
            StopSlide();
            rt.anchoredPosition = _originalAnchored + new Vector2(dir * slideDistance, 0f);

            _slideTween = rt.DOAnchorPos(_originalAnchored, slideDuration).SetEase(slideEase).SetLink(gameObject);
        }

        private void StopSlide()
        {
            _slideTween?.Kill();
            _slideTween = null;
            if (speakerNameText != null && _originalCaptured)
                speakerNameText.rectTransform.anchoredPosition = _originalAnchored;
        }

        #endregion
    }
}
