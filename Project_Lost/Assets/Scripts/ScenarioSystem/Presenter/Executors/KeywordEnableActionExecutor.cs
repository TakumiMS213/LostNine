using System;
using UnityEngine;
using ScenarioSystem.Model;
using ScenarioSystem.Model.Actions;
using ScenarioSystem.Runtime;
using ScenarioSystem.Events;

namespace ScenarioSystem.Presenter.Executors
{
    /// <summary>
    /// KeywordEnableAction を実行する Executor。
    /// EventBus 経由で View/Adapter にキーワード状態変更を通知する。
    /// </summary>
    public class KeywordEnableActionExecutor : IActionExecutor
    {
        public string HandledActionType => "KeywordEnable";

        public void Execute(ScenarioAction action, ScenarioRuntimeState state, Action onComplete)
        {
            if (action is not KeywordEnableAction keywordAction)
            {
                Debug.LogWarning("[KeywordEnableActionExecutor] Invalid action type.");
                onComplete?.Invoke();
                return;
            }

            ScenarioEventBus.RaiseKeywordStateChanged(keywordAction.enable);
            onComplete?.Invoke();
        }
    }
}
