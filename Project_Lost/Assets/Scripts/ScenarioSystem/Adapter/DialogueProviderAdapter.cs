using UnityEngine;
using TMPro;
using ScenarioSystem.Events;

namespace ScenarioSystem.Adapter
{
    /// <summary>
    /// 新シナリオシステムを IDialogueProvider として公開する。
    /// KeywordHandler がこのコンポーネントを参照することで、
    /// MessageWindowManager への直接依存を除去できる。
    /// 
    /// EventBus を購読して最新のテキスト状態を保持する。
    /// </summary>
    public class DialogueProviderAdapter : MonoBehaviour, IDialogueProvider
    {
        #region Serialized Fields

        [Header("Text Reference")]
        [Tooltip("セリフ表示用の TMP_Text（DialogueView と同じものを設定）。")]
        [SerializeField] private TMP_Text dialogueText;

        #endregion

        #region Private Fields

        private string _currentText = "";
        private bool _isWindowActive;
        private bool _isTyping;

        #endregion

        #region IDialogueProvider Implementation

        public TMP_Text DialogueText => dialogueText;
        public string CurrentText => _currentText;
        public bool IsWindowActive => _isWindowActive;
        public bool IsTyping => _isTyping;

        public void UpdateCurrentText(string newText)
        {
            _currentText = newText;
            if (dialogueText != null)
            {
                dialogueText.text = newText;
                dialogueText.ForceMeshUpdate();
            }
        }

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            ScenarioEventBus.OnDialogueRequested += HandleDialogue;
            ScenarioEventBus.OnTypingCompleted += HandleTypingCompleted;
            ScenarioEventBus.OnWindowVisibilityChanged += HandleWindowVisibility;
        }

        private void OnDisable()
        {
            ScenarioEventBus.OnDialogueRequested -= HandleDialogue;
            ScenarioEventBus.OnTypingCompleted -= HandleTypingCompleted;
            ScenarioEventBus.OnWindowVisibilityChanged -= HandleWindowVisibility;
        }

        #endregion

        #region Event Handlers

        private void HandleDialogue(DialogueEventData data)
        {
            _currentText = data.Text ?? "";
            _isTyping = true;
        }

        private void HandleTypingCompleted()
        {
            _isTyping = false;
        }

        private void HandleWindowVisibility(bool visible)
        {
            _isWindowActive = visible;
        }

        #endregion
    }
}
