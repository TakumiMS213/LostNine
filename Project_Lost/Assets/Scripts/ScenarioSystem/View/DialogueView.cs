using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using TMPro;
using ScenarioSystem.Events;
using ScenarioSystem.Model.Actions;

namespace ScenarioSystem.View
{
    /// <summary>
    /// テキスト表示を担当する View。
    /// EventBus の OnDialogueRequested を購読し、タイピング演出を行う。
    /// タイピング完了時に OnTypingCompleted を発火する。
    /// ユーザークリック時に OnAdvanceRequested を発火する。
    /// </summary>
    public class DialogueView : MonoBehaviour
    {
        #region Serialized Fields

        [Header("UI References")]
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField] private GameObject windowRoot;

        [Header("Typing Settings")]
        [SerializeField] private float defaultTypingSpeed = 0.05f;

        [Header("Advance Indicator")]
        [SerializeField] private Sprite advanceIndicatorSprite;
        [SerializeField] private RectTransform advanceIndicatorParent;
        [SerializeField] private Vector2 advanceIndicatorSize = new Vector2(32f, 32f);
        [SerializeField] private Vector2 advanceIndicatorOffset = new Vector2(-44f, 34f);

        [Header("Skip Mode")]
        [SerializeField] private bool enableSkipMode = true;
        [SerializeField] private Key skipKey = Key.LeftCtrl;
        [SerializeField] private float skipTypingSpeed = 0.001f;
        [SerializeField] private bool enableAutoAdvance = true;
        [SerializeField, Min(0.01f)] private float autoAdvanceInterval = 0.06f;
        [SerializeField, Min(0f)] private float autoAdvanceInitialDelay = 0.08f;

        [Header("Keyword Integration")]
        [Tooltip("キーワードクリック時に advance をブロックするための参照。")]
        [SerializeField] private MessageWindowSystem.Core.KeywordHandler keywordHandler;

        #endregion

        #region Private Fields

        private Coroutine _typingCoroutine;
        private bool _isTyping;
        private string _currentFullText;
        private bool _wasSkipHeld;
        private float _nextAutoAdvanceTime;
        private GameObject _advanceIndicatorObject;

        #endregion

        #region Public Properties

        /// <summary>現在表示中の TMP_Text コンポーネント。</summary>
        public TMP_Text Text => dialogueText;

        #endregion

        #region Setup

        public void Configure(TMP_Text text, GameObject root, float typingSpeed = 0.05f)
        {
            dialogueText = text;
            windowRoot = root;
            defaultTypingSpeed = typingSpeed;
            keywordHandler = null;
            EnsureAdvanceIndicator();
        }

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            ScenarioEventBus.OnDialogueRequested += HandleDialogue;
            ScenarioEventBus.OnWindowVisibilityChanged += HandleWindowVisibility;
            SceneManager.sceneLoaded += HandleSceneLoaded;
        }

        private void Start()
        {
            EnsureAdvanceIndicator();
        }

        private void OnDisable()
        {
            ScenarioEventBus.OnDialogueRequested -= HandleDialogue;
            ScenarioEventBus.OnWindowVisibilityChanged -= HandleWindowVisibility;
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            ResetAutoAdvanceState();
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
                ResetAutoAdvanceState();
        }

        private void OnApplicationPause(bool pauseStatus)
        {
            if (pauseStatus)
                ResetAutoAdvanceState();
        }

        private void Update()
        {
            if (!enableSkipMode || !enableAutoAdvance)
                return;

            bool skipHeld = IsSkipHeld();
            if (!skipHeld)
            {
                _wasSkipHeld = false;
                return;
            }

            if (!_wasSkipHeld)
            {
                _wasSkipHeld = true;
                _nextAutoAdvanceTime = Time.unscaledTime + autoAdvanceInitialDelay;
                return;
            }

            if (Time.unscaledTime < _nextAutoAdvanceTime)
                return;

            _nextAutoAdvanceTime = Time.unscaledTime + autoAdvanceInterval;

            if (windowRoot == null || windowRoot.activeInHierarchy)
                OnUserInput();
        }

        #endregion

        #region Public API

        /// <summary>
        /// ユーザーのクリック/タップ入力を受け取る。
        /// UI Button の OnClick 等から呼び出す。
        /// </summary>
        public void OnUserInput()
        {
            if (_isTyping)
            {
                // タイピング中ならスキップ（全文表示）
                SkipTyping();
            }
            else
            {
                // キーワードクリック中なら advance をブロック
                if (keywordHandler != null && keywordHandler.ConsumeBlockNext())
                    return;

                // タイピング完了済みなら次へ進むリクエスト
                ScenarioEventBus.RaiseAdvanceRequested();
            }
        }

        #endregion

        #region Event Handlers

        private void HandleDialogue(DialogueEventData data)
        {
            if (dialogueText == null) return;

            _currentFullText = data.Text;
            float speed = data.TypingSpeed > 0 ? data.TypingSpeed : defaultTypingSpeed;

            if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
            _typingCoroutine = StartCoroutine(TypeText(_currentFullText, speed));
        }

        private void HandleWindowVisibility(bool visible)
        {
            if (windowRoot != null)
                windowRoot.SetActive(visible);

            if (!visible)
                ResetAutoAdvanceState();
        }

        private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            ResetAutoAdvanceState();
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
                float step = (enableSkipMode && IsSkipHeld())
                    ? skipTypingSpeed
                    : speed;

                dialogueText.maxVisibleCharacters = i;
                yield return new WaitForSeconds(step);
            }

            FinishTyping();
        }

        private void SkipTyping()
        {
            if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);

            if (dialogueText != null && _currentFullText != null)
            {
                dialogueText.text = _currentFullText;
                dialogueText.maxVisibleCharacters = _currentFullText.Length;
            }

            FinishTyping();
        }

        private void FinishTyping()
        {
            _isTyping = false;
            _typingCoroutine = null;
            ScenarioEventBus.RaiseTypingCompleted();
        }

        private bool IsSkipHeld()
        {
            var keyboard = Keyboard.current;
            if (keyboard == null)
                return false;

            if (IsControlSkipKey())
            {
                bool inputSystemHeld = keyboard.leftCtrlKey.isPressed || keyboard.rightCtrlKey.isPressed;
#if ENABLE_LEGACY_INPUT_MANAGER
                bool legacyHeld = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
                return inputSystemHeld && legacyHeld;
#else
                return inputSystemHeld;
#endif
            }

            var keyControl = keyboard[skipKey];
            return keyControl != null && keyControl.isPressed;
        }

        private bool IsControlSkipKey()
        {
            return skipKey == Key.LeftCtrl || skipKey == Key.RightCtrl;
        }

        private void ResetAutoAdvanceState()
        {
            _wasSkipHeld = false;
            _nextAutoAdvanceTime = 0f;
        }

        private void EnsureAdvanceIndicator()
        {
            var parent = advanceIndicatorParent != null
                ? advanceIndicatorParent
                : windowRoot != null
                    ? windowRoot.transform as RectTransform
                    : null;

            if (advanceIndicatorSprite == null || parent == null || _advanceIndicatorObject != null)
                return;

            _advanceIndicatorObject = new GameObject("AdvanceIndicator");
            _advanceIndicatorObject.transform.SetParent(parent, false);

            var rect = _advanceIndicatorObject.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = advanceIndicatorSize;
            rect.anchoredPosition = advanceIndicatorOffset;

            var image = _advanceIndicatorObject.AddComponent<Image>();
            image.sprite = advanceIndicatorSprite;
            image.raycastTarget = false;
            image.preserveAspect = true;

            _advanceIndicatorObject.AddComponent<MessageWindowCaretIndicator>();
        }

        #endregion
    }
}
