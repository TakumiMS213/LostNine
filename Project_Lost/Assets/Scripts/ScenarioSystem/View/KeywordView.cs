using UnityEngine;
using ScenarioSystem.Events;
using ScenarioSystem.Model;
using ScenarioSystem.Model.Actions;

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
        }

        private void OnDisable()
        {
            ScenarioEventBus.OnKeywordStateChanged -= HandleKeywordStateChanged;
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

        #endregion
    }
}
