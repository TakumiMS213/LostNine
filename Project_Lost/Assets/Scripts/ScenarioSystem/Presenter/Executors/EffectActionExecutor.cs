using System;
using UnityEngine;
using ScenarioSystem.Model;
using ScenarioSystem.Model.Actions;
using ScenarioSystem.Runtime;
using ScenarioSystem.Events;

namespace ScenarioSystem.Presenter.Executors
{
    /// <summary>
    /// EffectAction を実行する Executor。
    /// EventBus 経由で演出実行を通知し、即座に完了する。
    /// </summary>
    public class EffectActionExecutor : IActionExecutor
    {
        public string HandledActionType => "Effect";

        public void Execute(ScenarioAction action, ScenarioRuntimeState state, Action onComplete)
        {
            if (action is not EffectAction effect)
            {
                Debug.LogWarning("[EffectActionExecutor] Invalid action type.");
                onComplete?.Invoke();
                return;
            }

            // View（EffectView）に演出実行を通知
            ScenarioEventBus.RaiseEffectRequested(effect);

            // 演出は即座に完了扱い（演出自体の時間は View が管理）
            onComplete?.Invoke();
        }
    }
}
