using System;
using UnityEngine;
using ScenarioSystem.Model;
using ScenarioSystem.Model.Actions;
using ScenarioSystem.Runtime;
using ScenarioSystem.Events;

namespace ScenarioSystem.Presenter.Executors
{
    /// <summary>
    /// ProgressUpdateAction を実行する Executor。
    /// EventBus 経由で ProgressAdapter に通知し、即座に完了する。
    /// </summary>
    public class ProgressUpdateActionExecutor : IActionExecutor
    {
        public string HandledActionType => "ProgressUpdate";

        public void Execute(ScenarioAction action, ScenarioRuntimeState state, Action onComplete)
        {
            if (action is not ProgressUpdateAction progressAction)
            {
                Debug.LogWarning("[ProgressUpdateActionExecutor] Invalid action type.");
                onComplete?.Invoke();
                return;
            }

            ScenarioEventBus.RaiseProgressUpdateRequested(progressAction);
            onComplete?.Invoke();
        }
    }
}
