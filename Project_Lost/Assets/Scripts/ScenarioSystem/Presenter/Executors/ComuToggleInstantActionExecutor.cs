using System;
using UnityEngine;
using ScenarioSystem.Model;
using ScenarioSystem.Runtime;
using ScenarioSystem.Events;

namespace ScenarioSystem.Presenter.Executors
{
    /// <summary>
    /// ComuToggleInstantAction を実行する Executor。
    /// EventBus 経由で Adapter に通知し、アニメーションなしで
    /// Portrait OnClick と同等の処理を即座に実行する。
    /// </summary>
    public class ComuToggleInstantActionExecutor : IActionExecutor
    {
        public string HandledActionType => "ComuToggleInstant";

        public void Execute(ScenarioAction action, ScenarioRuntimeState state, Action onComplete)
        {
            ScenarioEventBus.RaiseComuToggleInstantRequested();
            onComplete?.Invoke();
        }
    }
}
