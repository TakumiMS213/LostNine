using UnityEngine;
using ScenarioSystem.Events;

namespace ScenarioSystem.Adapter
{
    /// <summary>
    /// 新シナリオシステム ↔ 既存 ClueManager の橋渡し。
    /// EventBus の OnKeywordClicked / OnKeywordStateChanged を購読し、
    /// 既存の ClueManager API を呼び出す。
    /// </summary>
    public class ClueAdapter : MonoBehaviour
    {
        #region Unity Lifecycle

        private void OnEnable()
        {
            ScenarioEventBus.OnKeywordClicked += HandleKeywordClicked;
            ScenarioEventBus.OnKeywordStateChanged += HandleKeywordStateChanged;
        }

        private void OnDisable()
        {
            ScenarioEventBus.OnKeywordClicked -= HandleKeywordClicked;
            ScenarioEventBus.OnKeywordStateChanged -= HandleKeywordStateChanged;
        }

        #endregion

        #region Event Handlers

        private void HandleKeywordClicked(string keywordId)
        {
            if (ClueManager.Instance == null)
            {
                Debug.LogWarning("[ClueAdapter] ClueManager.Instance is null.");
                return;
            }

            Debug.Log($"[ClueAdapter] Keyword clicked: {keywordId}");
            ClueManager.Instance.ProcessKeywordClick(keywordId);
        }

        private void HandleKeywordStateChanged(bool enabled)
        {
            Debug.Log($"[ClueAdapter] Keyword state changed: {(enabled ? "Enabled" : "Disabled")}");
            // 既存の KeywordHandler への通知は
            // MessageWindowManager 経由で行われていたが、
            // 新システムでは直接 KeywordHandler に通知する形に変更可能。
            // 現時点では ClueManager の ResetForNewStage() で対応。
            if (!enabled && ClueManager.Instance != null)
            {
                ClueManager.Instance.ResetForNewStage();
            }
        }

        #endregion
    }
}
