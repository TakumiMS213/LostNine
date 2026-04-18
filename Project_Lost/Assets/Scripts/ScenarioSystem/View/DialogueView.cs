using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
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

        [Header("Skip Mode")]
        [SerializeField] private bool enableSkipMode = true;
        [SerializeField] private Key skipKey = Key.LeftCtrl;
        [SerializeField] private float skipTypingSpeed = 0.001f;

        #endregion

        #region Private Fields

        private Coroutine _typingCoroutine;
        private bool _isTyping;
        private string _currentFullText;

        #endregion

        #region Public Properties

        /// <summary>現在表示中の TMP_Text コンポーネント。</summary>
        public TMP_Text Text => dialogueText;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            ScenarioEventBus.OnDialogueRequested += HandleDialogue;
            ScenarioEventBus.OnWindowVisibilityChanged += HandleWindowVisibility;
        }

        private void OnDisable()
        {
            ScenarioEventBus.OnDialogueRequested -= HandleDialogue;
            ScenarioEventBus.OnWindowVisibilityChanged -= HandleWindowVisibility;
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
                float step = (enableSkipMode && Keyboard.current?[skipKey].isPressed == true)
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

        #endregion
    }
}
