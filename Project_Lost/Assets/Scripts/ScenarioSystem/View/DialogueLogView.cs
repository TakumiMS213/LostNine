using System.Collections.Generic;
using UnityEngine;
using ScenarioSystem.Model;
using ScenarioSystem.Presenter;
using ScenarioSystem.Events;

namespace ScenarioSystem.View
{
    /// <summary>
    /// 会話ログを管理する View。
    /// EventBus の OnDialogueRequested を購読し、全セリフを記録する。
    /// </summary>
    public class DialogueLogView : MonoBehaviour
    {
        #region Private Fields

        private readonly List<(string speaker, string text)> _log = new();

        #endregion

        #region Public Properties

        /// <summary>記録された会話ログ。</summary>
        public IReadOnlyList<(string speaker, string text)> Log => _log;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            ScenarioEventBus.OnDialogueRequested += HandleDialogue;
            ScenarioEventBus.OnScenarioStarted += HandleScenarioStarted;
        }

        private void OnDisable()
        {
            ScenarioEventBus.OnDialogueRequested -= HandleDialogue;
            ScenarioEventBus.OnScenarioStarted -= HandleScenarioStarted;
        }

        #endregion

        #region Event Handlers

        private void HandleDialogue(DialogueEventData data)
        {
            _log.Add((data.SpeakerName ?? string.Empty, data.Text ?? string.Empty));
        }

        private void HandleScenarioStarted(ScenarioData scenario)
        {
            // シナリオ開始時にログをクリアするかどうかはゲーム仕様に依る
            // デフォルトではクリアしない（累積ログ）
        }

        #endregion

        #region Public API

        /// <summary>ログをクリアする。</summary>
        public void ClearLog()
        {
            _log.Clear();
        }

        #endregion
    }
}
