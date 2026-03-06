using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.InputSystem;
using TMPro;
using MessageWindowSystem.Data;

namespace MessageWindowSystem.Core
{
    /// <summary>
    /// Lightweight message window for tutorials and cutscenes.
    /// No Progress integration, no keyword system.
    /// Focused on simple scenario playback from ScenarioDatabase.
    /// </summary>
    public class SimpleMessageWindow : MonoBehaviour
    {
        #region Serialized Fields

        [Header("UI References")]
        [SerializeField] private TMP_Text speakerNameText;
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField] private Image portraitImage;
        [SerializeField] private GameObject windowRoot;

        [Header("Portrait Anchors")]
        [SerializeField] private RectTransform portraitLeftAnchor;
        [SerializeField] private RectTransform portraitCenterAnchor;
        [SerializeField] private RectTransform portraitRightAnchor;

        [Header("Typing Settings")]
        [SerializeField] private float typingSpeed = 0.05f;

        [Header("Database")]
        [SerializeField] private ScenarioDatabase scenarioDatabase;

        #endregion

        #region Public Properties

        public static SimpleMessageWindow Instance { get; private set; }
        public bool IsActive => _isActive;

        #endregion

        #region Private Fields

        private readonly Queue<DialogueLine> _linesQueue = new();
        private DialogueLine _currentLine;
        private Coroutine _typingCoroutine;
        private Action _onComplete;
        private bool _isTyping;
        private bool _isActive;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }

            if (windowRoot) windowRoot.SetActive(false);
        }

        #endregion

        #region Public API

        /// <summary>Play a scenario directly.</summary>
        public void PlayScenario(DialogueScenario scenario, Action onComplete = null)
        {
            if (scenario == null)
            {
                onComplete?.Invoke();
                return;
            }

            _linesQueue.Clear();
            foreach (var line in scenario.lines)
                _linesQueue.Enqueue(line);

            _onComplete = onComplete;

            if (windowRoot) windowRoot.SetActive(true);
            _isActive = true;

            DisplayNextLine();
        }

        /// <summary>Play a scenario by ID from the database.</summary>
        public void PlayScenarioById(string scenarioId, Action onComplete = null)
        {
            if (scenarioDatabase == null)
            {
                Debug.LogWarning("[SimpleMessageWindow] ScenarioDatabase is not assigned.");
                onComplete?.Invoke();
                return;
            }

            var scenario = scenarioDatabase.GetScenarioById(scenarioId);
            if (scenario == null)
            {
                Debug.LogWarning($"[SimpleMessageWindow] Scenario '{scenarioId}' not found.");
                onComplete?.Invoke();
                return;
            }

            PlayScenario(scenario, onComplete);
        }

        /// <summary>Advance to next line or complete typing.</summary>
        public void Next()
        {
            if (!_isActive) return;

            if (_isTyping)
            {
                // Skip typing, show full text
                if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
                _isTyping = false;
                dialogueText.text = _currentLine.text;
                dialogueText.maxVisibleCharacters = _currentLine.text.Length;
                return;
            }

            DisplayNextLine();
        }

        /// <summary>Skip everything and close immediately.</summary>
        public void Skip()
        {
            if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
            _isTyping = false;
            _linesQueue.Clear();
            CloseWindow();
        }

        #endregion

        #region Internal Flow

        private void DisplayNextLine()
        {
            if (_linesQueue.Count == 0)
            {
                CloseWindow();
                return;
            }

            _currentLine = _linesQueue.Dequeue();

            if (speakerNameText)
                speakerNameText.text = _currentLine.speakerName ?? string.Empty;

            UpdatePortrait();

            // Play effects if available
            if (_currentLine.effects != null && EffectManager.Instance != null)
            {
                foreach (var effect in _currentLine.effects)
                    EffectManager.Instance.PlayEffect(effect);
            }

            if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
            float speed = _currentLine.typingSpeed > 0 ? _currentLine.typingSpeed : typingSpeed;
            _typingCoroutine = StartCoroutine(TypeText(_currentLine.text, speed));
        }

        private IEnumerator TypeText(string text, float speed)
        {
            _isTyping = true;
            dialogueText.text = text;
            dialogueText.maxVisibleCharacters = 0;
            dialogueText.ForceMeshUpdate();

            int total = dialogueText.textInfo.characterCount;
            for (int i = 0; i <= total; i++)
            {
                dialogueText.maxVisibleCharacters = i;
                yield return new WaitForSeconds(speed);
            }

            _isTyping = false;
        }

        private void UpdatePortrait()
        {
            if (portraitImage == null) return;

            if (_currentLine.portrait != null)
            {
                portraitImage.sprite = _currentLine.portrait;
                portraitImage.gameObject.SetActive(true);

                RectTransform anchor = _currentLine.portraitPosition switch
                {
                    PortraitPosition.Left => portraitLeftAnchor,
                    PortraitPosition.Right => portraitRightAnchor,
                    _ => portraitCenterAnchor
                };

                if (anchor != null)
                    portraitImage.rectTransform.anchoredPosition = anchor.anchoredPosition;
            }
        }

        private void CloseWindow()
        {
            _isActive = false;
            _isTyping = false;

            if (windowRoot) windowRoot.SetActive(false);

            _onComplete?.Invoke();
            _onComplete = null;
        }

        #endregion
    }
}
