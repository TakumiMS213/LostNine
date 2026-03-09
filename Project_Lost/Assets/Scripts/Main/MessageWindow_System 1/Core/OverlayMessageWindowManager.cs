using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using DG.Tweening;
using MessageWindowSystem.Data;

namespace MessageWindowSystem.Core
{
    /// <summary>
    /// Independent message window manager for playing overlay/interrupt scenarios 
    /// (e.g., tutorials, inner thoughts). 
    /// Blocks clicks to the main MessageWindow automatically while active.
    /// Does not integrate with ProgressManager or ComuStartandEndManager.
    /// </summary>
    public class OverlayMessageWindowManager : MonoBehaviour
    {
        #region Serialized Fields

        [Header("UI References")]
        [SerializeField] private TMP_Text speakerNameText;
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField] private GameObject windowRoot;

        [Header("Background Still")]
        [Tooltip("Image component behind the message window for displaying CGs/stills.")]
        [SerializeField] private Image backgroundStillImage;
        [Tooltip("Objects to disable when a background still is active (e.g., text box, speaker name bg).")]
        [SerializeField] private GameObject[] objectsToHideOnStill;

        [Header("Click Blocker")]
        [Tooltip("An invisible or tinted UI Image spanning the entire screen to block clicks from reaching the main window.")]
        [SerializeField] private GameObject clickBlockerRoot;

        [Header("Portrait")]
        [SerializeField] private Image portraitImage;
        [SerializeField] private RectTransform portraitLeftAnchor;
        [SerializeField] private RectTransform portraitCenterAnchor;
        [SerializeField] private RectTransform portraitRightAnchor;

        [Header("Ghost Portrait")]
        [SerializeField] private Image ghostPortraitImage;
        [SerializeField] private bool enableGhostPortrait = true;
        [SerializeField, Range(0f, 1f)] private float ghostPortraitAlpha = 0.3f;

        [Header("Typing Settings")]
        [SerializeField] private float typingSpeed = 0.05f;

        [Header("Skip Mode")]
        [SerializeField] private bool enableSkipMode = true;
        [SerializeField] private Key skipKey = Key.LeftCtrl;
        [SerializeField] private float skipTypingSpeed = 0.001f;

        [Header("Name Slide Animation")]
        [SerializeField] private bool animateName = true;
        [SerializeField] private float nameSlideDistance = 600f;
        [SerializeField] private float nameSlideDuration = 0.35f;
        [SerializeField] private Ease nameSlideEase = Ease.OutCubic;

        [Header("Portrait Animation")]
        [SerializeField] private bool portraitJumpOnText = true;
        [SerializeField] private float portraitJumpHeight = 50f;
        [SerializeField] private float portraitJumpDuration = 0.3f;
        [SerializeField] private Ease portraitJumpEase = Ease.OutBounce;

        [Header("Choice Buttons (Optional for Overlay)")]
        [SerializeField] private Button[] choiceButtons;
        [SerializeField] private TMP_Text[] choiceButtonTexts;

        #endregion

        #region Public Properties

        public static OverlayMessageWindowManager Instance { get; private set; }

        public bool IsWindowActive => _isWindowActive;
        public bool IsTyping => _isTyping;

        #endregion

        #region Private Fields

        private readonly Queue<DialogueLine> _linesQueue = new();

        private DialogueScenario _currentScenarioData;
        private DialogueLine _currentLine;

        private Coroutine _typingCoroutine;

        private Vector2 _nameOriginalAnchored;
        private string _previousSpeakerName;
        private Sprite _previousPortrait;
        private PortraitPosition _previousPortraitPosition;

        private bool _isTyping;
        private bool _isWindowActive;
        private bool _isWaitingForChoice;
        private bool _nameOriginalCaptured;

        private Action _onScenarioComplete;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }

            if (windowRoot) windowRoot.SetActive(false);
            if (clickBlockerRoot) clickBlockerRoot.SetActive(false);

            if (speakerNameText != null)
            {
                _nameOriginalAnchored = speakerNameText.rectTransform.anchoredPosition;
                _nameOriginalCaptured = true;
            }

            HideAllChoices();
            HideGhostPortrait();
        }

        #endregion

        #region Public API

        /// <summary>Starts playing an overlay scenario.</summary>
        public void StartScenario(DialogueScenario scenario, Action onComplete = null)
        {
            if (scenario == null)
            {
                onComplete?.Invoke();
                return;
            }

            _isWaitingForChoice = false;

            HideAllChoices();

            _linesQueue.Clear();
            foreach (var line in scenario.lines)
                _linesQueue.Enqueue(line);

            _currentScenarioData = scenario;
            _onScenarioComplete = onComplete;

            if (clickBlockerRoot) clickBlockerRoot.SetActive(true);
            if (windowRoot) windowRoot.SetActive(true);
            _isWindowActive = true;

            Debug.Log($"[OverlayMWM] StartScenario: {scenario.name} (ID: {scenario.scenarioId})");
            DisplayNextLine();
        }

        /// <summary>Advances to the next line or completes typing. Call this from an invisible screen-sized button on the Overlay Canvas.</summary>
        public void Next()
        {
            if (!_isWindowActive) return;
            if (_isWaitingForChoice) return;

            SkipOrInteract();
        }

        #endregion

        #region Dialogue Flow

        private void SkipOrInteract()
        {
            if (_isTyping)
            {
                if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
                _isTyping = false;
                dialogueText.text = _currentLine.text;
                dialogueText.maxVisibleCharacters = _currentLine.text.Length;

                if (_currentLine?.choices != null && _currentLine.choices.Count > 0)
                    ShowChoices(_currentLine.choices);
                return;
            }

            DisplayNextLine();
        }

        private void DisplayNextLine()
        {
            if (_linesQueue.Count == 0)
            {
                if (_currentScenarioData != null && _currentScenarioData.loopScenario)
                {
                    StartScenario(_currentScenarioData, _onScenarioComplete);
                    return;
                }

                EndScenario();
                return;
            }

            _currentLine = _linesQueue.Dequeue();
            string speakerName = _currentLine.speakerName ?? string.Empty;

            if (speakerNameText) speakerNameText.text = speakerName;

            // Update Background Still Image & Toggle Objects
            if (backgroundStillImage != null)
            {
                bool hasStill = _currentLine.backgroundImage != null;
                if (hasStill)
                {
                    backgroundStillImage.sprite = _currentLine.backgroundImage;
                    backgroundStillImage.gameObject.SetActive(true);
                }
                else
                {
                    backgroundStillImage.gameObject.SetActive(false);
                }

                if (objectsToHideOnStill != null)
                {
                    foreach (var obj in objectsToHideOnStill)
                    {
                        if (obj != null) obj.SetActive(!hasStill);
                    }
                }
            }

            UpdatePortrait();
            PlayEffects();
            AnimateSpeakerName(speakerName);

            _previousSpeakerName = speakerName;
            _previousPortrait = _currentLine.portrait;
            _previousPortraitPosition = _currentLine.portraitPosition;

            if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
            float speed = _currentLine.typingSpeed > 0 ? _currentLine.typingSpeed : typingSpeed;
            _typingCoroutine = StartCoroutine(TypeText(_currentLine.text, speed));
        }

        private void EndScenario()
        {
            // Chain to next scenario if set
            if (_currentScenarioData?.nextScenario != null)
            {
                Debug.Log($"[OverlayMWM] Chaining to next scenario: {_currentScenarioData.nextScenario.name}");
                StartScenario(_currentScenarioData.nextScenario, _onScenarioComplete);
                return;
            }

            // Close window
            _isWindowActive = false;
            if (windowRoot) windowRoot.SetActive(false);
            if (clickBlockerRoot) clickBlockerRoot.SetActive(false);
            if (backgroundStillImage) backgroundStillImage.gameObject.SetActive(false);

            // Re-enable hidden objects when scenario ends
            if (objectsToHideOnStill != null)
            {
                foreach (var obj in objectsToHideOnStill)
                {
                    if (obj != null) obj.SetActive(true);
                }
            }

            HideGhostPortrait();

            // Fire completion callback
            _onScenarioComplete?.Invoke();
            _onScenarioComplete = null;
        }

        #endregion

        #region Typing

        private IEnumerator TypeText(string text, float speed)
        {
            _isTyping = true;
            dialogueText.text = text;
            dialogueText.maxVisibleCharacters = 0;
            dialogueText.ForceMeshUpdate();

            int total = dialogueText.textInfo.characterCount;
            for (int i = 0; i <= total; i++)
            {
                float step = (enableSkipMode && Keyboard.current?[skipKey].isPressed == true) ? skipTypingSpeed : speed;
                dialogueText.maxVisibleCharacters = i;
                yield return new WaitForSeconds(step);
            }

            _isTyping = false;

            if (_currentLine?.choices != null && _currentLine.choices.Count > 0)
            {
                ShowChoices(_currentLine.choices);
            }
        }

        #endregion

        #region Choices

        private void ShowChoices(List<ChoiceData> choices)
        {
            if (choiceButtons == null || choiceButtons.Length == 0) return;

            _isWaitingForChoice = true;

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (i < choices.Count)
                {
                    choiceButtons[i].gameObject.SetActive(true);
                    if (choiceButtonTexts != null && i < choiceButtonTexts.Length)
                        choiceButtonTexts[i].text = choices[i].choiceText;

                    int index = i;
                    choiceButtons[i].onClick.RemoveAllListeners();
                    choiceButtons[i].onClick.AddListener(() => OnChoiceSelected(index));
                }
                else
                {
                    choiceButtons[i].gameObject.SetActive(false);
                }
            }
        }

        public void OnChoiceSelected(int index)
        {
            if (!_isWaitingForChoice || _currentLine?.choices == null) return;
            if (index < 0 || index >= _currentLine.choices.Count) return;

            var choice = _currentLine.choices[index];
            _isWaitingForChoice = false;
            HideAllChoices();

            if (choice.nextScenario != null)
            {
                StartScenario(choice.nextScenario, _onScenarioComplete);
            }
            else
            {
                _linesQueue.Clear();
                EndScenario();
            }
        }

        private void HideAllChoices()
        {
            if (choiceButtons == null) return;
            foreach (var btn in choiceButtons)
                btn?.gameObject.SetActive(false);
        }

        #endregion

        #region Portrait

        private void UpdatePortrait()
        {
            if (portraitImage == null) return;

            UpdateGhostPortrait();

            if (_currentLine.portrait != null)
            {
                portraitImage.sprite = _currentLine.portrait;
                portraitImage.gameObject.SetActive(true);
                SetPortraitPosition(portraitImage.rectTransform, _currentLine.portraitPosition);

                if (portraitJumpOnText) PlayPortraitJump();
            }
            else
            {
                portraitImage.gameObject.SetActive(false);
            }
        }

        private void SetPortraitPosition(RectTransform portraitRect, PortraitPosition position)
        {
            RectTransform anchor = position switch
            {
                PortraitPosition.Left => portraitLeftAnchor,
                PortraitPosition.Right => portraitRightAnchor,
                _ => portraitCenterAnchor
            };

            if (anchor != null)
            {
                portraitRect.anchoredPosition = anchor.anchoredPosition;
            }
        }

        private void UpdateGhostPortrait()
        {
            if (ghostPortraitImage == null || !enableGhostPortrait)
            {
                HideGhostPortrait();
                return;
            }

            bool hasPreviousPortrait = _previousPortrait != null;
            bool speakerChanged = !string.Equals(_currentLine.speakerName, _previousSpeakerName, StringComparison.Ordinal);
            bool positionChanged = _currentLine.portraitPosition != _previousPortraitPosition;

            if (hasPreviousPortrait && _currentLine.portrait != null && (speakerChanged || positionChanged))
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

        private void PlayPortraitJump()
        {
            var rect = portraitImage.GetComponent<RectTransform>();
            if (rect == null) return;

            rect.DOKill();
            var original = rect.anchoredPosition;
            var jumpTarget = original + Vector2.up * portraitJumpHeight;

            var seq = DOTween.Sequence();
            seq.Append(rect.DOAnchorPos(jumpTarget, portraitJumpDuration * 0.5f).SetEase(Ease.OutQuad));
            seq.Append(rect.DOAnchorPos(original, portraitJumpDuration * 0.5f).SetEase(portraitJumpEase));
        }

        #endregion

        #region Speaker Name Animation

        private void AnimateSpeakerName(string newName)
        {
            if (!animateName || speakerNameText == null) return;
            if (string.Equals(newName, _previousSpeakerName, StringComparison.Ordinal)) return;

            var rt = speakerNameText.rectTransform;
            if (!_nameOriginalCaptured) { _nameOriginalAnchored = rt.anchoredPosition; _nameOriginalCaptured = true; }

            bool fromRight = _currentLine.nameSlideDirection switch
            {
                NameSlideDirection.Left => false,
                NameSlideDirection.Right => true,
                _ => false
            };

            float dir = fromRight ? 1f : -1f;
            rt.anchoredPosition = _nameOriginalAnchored + new Vector2(dir * nameSlideDistance, 0f);

            rt.DOKill();
            rt.DOAnchorPos(_nameOriginalAnchored, nameSlideDuration).SetEase(nameSlideEase);
        }

        #endregion

        #region Effects

        private void PlayEffects()
        {
            if (_currentLine.effects == null || EffectManager.Instance == null) return;
            foreach (var effect in _currentLine.effects)
                EffectManager.Instance.PlayEffect(effect);
        }

        #endregion
    }
}
