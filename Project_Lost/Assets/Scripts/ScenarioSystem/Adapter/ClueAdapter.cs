using UnityEngine;
using ScenarioSystem.Events;

namespace ScenarioSystem.Adapter
{
    /// <summary>
    /// 新シナリオシステム ↔ 既存 ClueManager の橋渡し。
    /// EventBus の OnKeywordClicked を購読し、
    /// 既存の ClueManager API を呼び出す。
    /// </summary>
    public class ClueAdapter : MonoBehaviour
    {
        #region Unity Lifecycle

        private void OnEnable()
        {
            ScenarioEventBus.OnKeywordClicked += HandleKeywordClicked;
        }

        private void OnDisable()
        {
            ScenarioEventBus.OnKeywordClicked -= HandleKeywordClicked;
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

        #endregion
    }
}
