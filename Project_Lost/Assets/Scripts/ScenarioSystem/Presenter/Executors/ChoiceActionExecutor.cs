using System;
using UnityEngine;
using ScenarioSystem.Model;
using ScenarioSystem.Model.Actions;
using ScenarioSystem.Runtime;
using ScenarioSystem.Events;

namespace ScenarioSystem.Presenter.Executors
{
    /// <summary>
    /// ChoiceAction を実行する Executor。
    /// EventBus 経由で選択肢表示を通知し、選択待ち状態にする。
    /// </summary>
    public class ChoiceActionExecutor : IActionExecutor
    {
        public string HandledActionType => "Choice";

        public void Execute(ScenarioAction action, ScenarioRuntimeState state, Action onComplete)
        {
            if (action is not ChoiceAction choice)
            {
                Debug.LogWarning("[ChoiceActionExecutor] Invalid action type.");
                onComplete?.Invoke();
                return;
            }

            if (choice.choices == null || choice.choices.Count == 0)
            {
                Debug.LogWarning("[ChoiceActionExecutor] No choices defined. Skipping.");
                onComplete?.Invoke();
                return;
            }

            // 選択待ちフラグを立てる
            state.IsWaitingForChoice = true;

            // View に選択肢表示を通知
            ScenarioEventBus.RaiseChoicesRequested(choice.choices);

            // onComplete は呼ばない。
            // ユーザーが選択 → OnChoiceSelected → Presenter が HandleChoiceSelected で処理。
        }
    }
}
