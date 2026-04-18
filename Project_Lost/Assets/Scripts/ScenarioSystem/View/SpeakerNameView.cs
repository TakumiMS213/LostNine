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
            rt.anchoredPosition = _originalAnchored + new Vector2(dir * slideDistance, 0f);

            rt.DOKill();
            rt.DOAnchorPos(_originalAnchored, slideDuration).SetEase(slideEase);
        }

        #endregion
    }
}
