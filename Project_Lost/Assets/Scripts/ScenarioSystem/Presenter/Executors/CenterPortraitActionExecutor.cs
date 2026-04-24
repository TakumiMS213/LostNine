using System;
using UnityEngine;
using ScenarioSystem.Model;
using ScenarioSystem.Model.Actions;
using ScenarioSystem.Runtime;
using ScenarioSystem.Events;

namespace ScenarioSystem.Presenter.Executors
{
    /// <summary>
    /// CenterPortraitAction を実行する Executor。
    /// EventBus 経由で CenterPortraitView に通知し、即座に完了する。
    /// </summary>
    public class CenterPortraitActionExecutor : IActionExecutor
    {
        public string HandledActionType => "CenterPortrait";

        public void Execute(ScenarioAction action, ScenarioRuntimeState state, Action onComplete)
        {
            if (action is CenterPortraitAction centerPortrait)
            {
                ScenarioEventBus.RaiseCenterPortraitChanged(centerPortrait.sprite);
            }
            else
            {
                Debug.LogWarning("[CenterPortraitActionExecutor] Invalid action type.");
            }

            onComplete?.Invoke();
        }
    }
}
