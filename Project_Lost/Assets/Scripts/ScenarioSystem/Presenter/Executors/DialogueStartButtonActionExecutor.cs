using System;
using UnityEngine;
using ScenarioSystem.Model;
using ScenarioSystem.Model.Actions;
using ScenarioSystem.Runtime;

namespace ScenarioSystem.Presenter.Executors
{
    public class DialogueStartButtonActionExecutor : IActionExecutor
    {
        public string HandledActionType => "DialogueStartButton";

        public void Execute(ScenarioAction action, ScenarioRuntimeState state, Action onComplete)
        {
            if (action is not DialogueStartButtonAction buttonAction)
            {
                Debug.LogWarning("[DialogueStartButtonActionExecutor] Invalid action type.");
                onComplete?.Invoke();
                return;
            }

            var manager = UnityEngine.Object.FindFirstObjectByType<ComuStartandEndManager>();
            if (manager != null)
                manager.SetDialogueStartButtonVisible(buttonAction.visible);
            else
                Debug.LogWarning("[DialogueStartButtonActionExecutor] ComuStartandEndManager not found in scene.");

            onComplete?.Invoke();
        }
    }
}
