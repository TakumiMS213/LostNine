using System;
using TMPro;
using ScenarioSystem.Events;
using UnityEngine;
using UnityEngine.UI;

namespace ScenarioSystem.View
{
    /// <summary>非会話時の吹き出しを話者へ向ける。子の本文・クリック領域は反転しない。</summary>
    [RequireComponent(typeof(Image))]
    public sealed class SpeechBubbleView : BaseMeshEffect
    {
        [SerializeField] private string playerSpeakerName = "ヒイラギ";

        [Header("Story準拠の対話ウィンドウ")]
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField] private Sprite conversationSprite;
        [SerializeField] private Vector2 conversationWindowPosition = new Vector2(0f, -375f);
        [SerializeField] private Vector2 conversationWindowSize = new Vector2(1920f, 340f);
        [SerializeField] private Color conversationWindowColor = new Color(0.16078432f, 0.16078432f, 0.16078432f, 0.8f);
        [SerializeField] private Vector2 conversationTextPosition = new Vector2(-11.17f, 0.81f);
        [SerializeField] private Vector3 conversationTextScale = new Vector3(0.95f, 0.95f, 1f);
        [SerializeField] private float conversationFontSize = 63f;

        private Image _windowImage;
        private RectTransform _windowRect;
        private RectTransform _textRect;
        private Vector2 _originalWindowPosition;
        private Vector2 _originalWindowSize;
        private Sprite _originalWindowSprite;
        private Color _originalWindowColor;
        private Vector2 _originalTextPosition;
        private Vector2 _originalTextSize;
        private Vector3 _originalTextScale;
        private Color _originalTextColor;
        private Vector4 _originalTextMargin;
        private float _originalFontSize;
        private bool _layoutCaptured;

        private bool _isPlayerSpeaking;
        private bool _isInCommunication;

        protected override void Awake()
        {
            base.Awake();
            CaptureLayout();
        }

        protected override void OnEnable()
        {
            base.OnEnable();
            CaptureLayout();
            ApplyWindowLayout();
            ScenarioEventBus.OnDialogueRequested += HandleDialogue;
        }

        protected override void OnDisable()
        {
            ScenarioEventBus.OnDialogueRequested -= HandleDialogue;
            base.OnDisable();
        }

        public void SetConversationMode(bool isInCommunication)
        {
            CaptureLayout();
            _isInCommunication = isInCommunication;
            ApplyWindowLayout();
            if (_windowImage != null) _windowImage.SetVerticesDirty();
        }

        private void CaptureLayout()
        {
            if (_layoutCaptured) return;
            _windowImage = graphic as Image;
            if (_windowImage == null) return;
            _windowRect = _windowImage.rectTransform;
            if (_windowRect == null) return;

            _originalWindowPosition = _windowRect.anchoredPosition;
            _originalWindowSize = _windowRect.sizeDelta;
            _originalWindowSprite = _windowImage.sprite;
            _originalWindowColor = _windowImage.color;
            if (dialogueText != null)
            {
                _textRect = dialogueText.rectTransform;
                if (_textRect != null)
                {
                    _originalTextPosition = _textRect.anchoredPosition;
                    _originalTextSize = _textRect.sizeDelta;
                    _originalTextScale = _textRect.localScale;
                }
                _originalTextColor = dialogueText.color;
                _originalTextMargin = dialogueText.margin;
                _originalFontSize = dialogueText.fontSize;
            }
            _layoutCaptured = true;
        }

        private void ApplyWindowLayout()
        {
            if (!_layoutCaptured || _windowRect == null || _windowImage == null) return;
            _windowRect.anchoredPosition = _isInCommunication ? conversationWindowPosition : _originalWindowPosition;
            _windowRect.sizeDelta = _isInCommunication ? conversationWindowSize : _originalWindowSize;
            _windowImage.sprite = _isInCommunication && conversationSprite != null ? conversationSprite : _originalWindowSprite;
            _windowImage.color = _isInCommunication ? conversationWindowColor : _originalWindowColor;
            if (_textRect != null)
            {
                _textRect.anchoredPosition = _isInCommunication ? conversationTextPosition : _originalTextPosition;
                _textRect.sizeDelta = _isInCommunication ? conversationWindowSize : _originalTextSize;
                _textRect.localScale = _isInCommunication ? conversationTextScale : _originalTextScale;
            }
            if (dialogueText != null)
            {
                dialogueText.color = _isInCommunication ? Color.white : _originalTextColor;
                dialogueText.margin = _isInCommunication ? Vector4.zero : _originalTextMargin;
                dialogueText.fontSize = _isInCommunication ? conversationFontSize : _originalFontSize;
            }
        }

        private void HandleDialogue(DialogueEventData data)
        {
            _isPlayerSpeaking = string.Equals(data.SpeakerName?.Trim(), playerSpeakerName,
                StringComparison.Ordinal);
            if (_windowImage != null) _windowImage.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vertices)
        {
            // 元画像の突起は下向き。カウンター奥の話者のときだけ画像を上下反転。
            if (!IsActive() || _windowImage == null || _windowRect == null
                || _isInCommunication || _isPlayerSpeaking) return;

            float centerY = _windowRect.rect.center.y;
            var vertex = new UIVertex();
            for (int i = 0; i < vertices.currentVertCount; i++)
            {
                vertices.PopulateUIVertex(ref vertex, i);
                vertex.position.y = 2f * centerY - vertex.position.y;
                vertices.SetUIVertex(vertex, i);
            }
        }
    }
}
