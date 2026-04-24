using UnityEngine;
using ScenarioSystem.Events;
using ScenarioSystem.Model;
using ScenarioSystem.Model.Actions;
using ScenarioSystem.Adapter;

namespace ScenarioSystem.View
{
    /// <summary>
    /// キーワード機能を新シナリオシステムと連携させる View。
    /// EventBus の OnKeywordStateChanged を購読し、既存の KeywordHandler を制御する。
    /// また、キーワードクリック時にシナリオ再生を要求する。
    /// </summary>
    public class KeywordView : MonoBehaviour
    {
        #region Serialized Fields

        [Header("References")]
        [Tooltip("既存の KeywordHandler への参照。")]
        [SerializeField] private MessageWindowSystem.Core.KeywordHandler keywordHandler;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            ScenarioEventBus.OnKeywordStateChanged += HandleKeywordStateChanged;

            // KeywordHandler のシナリオ再生リクエストを購読
            if (keywordHandler != null)
                keywordHandler.OnKeywordScenarioRequested += HandleKeywordScenarioRequested;
        }

        private void OnDisable()
        {
            ScenarioEventBus.OnKeywordStateChanged -= HandleKeywordStateChanged;

            if (keywordHandler != null)
                keywordHandler.OnKeywordScenarioRequested -= HandleKeywordScenarioRequested;
        }

        #endregion

        #region Event Handlers

        private void HandleKeywordStateChanged(bool enabled)
        {
            if (keywordHandler != null)
            {
                keywordHandler.SetKeywordEnabled(enabled);
                Debug.Log($"[KeywordView] Keywords {(enabled ? "enabled" : "disabled")}");
            }
        }

        /// <summary>
        /// キーワード抽出後、linkID と同名のシナリオを DB から検索して再生する。
        /// </summary>
        private void HandleKeywordScenarioRequested(string keywordId)
        {
            if (string.IsNullOrEmpty(keywordId)) return;

            // ダミーキーワードはシナリオを持たない
            if (MessageWindowSystem.Core.KeywordHandler.IsDummyKeyword(keywordId))
            {
                Debug.Log($"[KeywordView] Dummy keyword '{keywordId}' — no scenario to play.");
                return;
            }

            Debug.Log($"[KeywordView] Playing keyword scenario: {keywordId}");
            MessageWindowFacade.Instance?.StartScenarioById(keywordId);
        }

        #endregion
    }
}
