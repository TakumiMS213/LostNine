using System;
using UnityEngine;
using ScenarioSystem.Model;
using ScenarioSystem.Runtime;
using ScenarioSystem.Events;

namespace ScenarioSystem.Presenter.Executors
{
    /// <summary>
    /// ComuToggleAction を実行する Executor。
    /// EventBus 経由で ComuAdapter に通知し、即座に完了する。
    /// </summary>
    public class ComuToggleActionExecutor : IActionExecutor
    {
        public string HandledActionType => "ComuToggle";

        public void Execute(ScenarioAction action, ScenarioRuntimeState state, Action onComplete)
        {
            ScenarioEventBus.RaiseComuToggleRequested();
            onComplete?.Invoke();
        }
    }
}
