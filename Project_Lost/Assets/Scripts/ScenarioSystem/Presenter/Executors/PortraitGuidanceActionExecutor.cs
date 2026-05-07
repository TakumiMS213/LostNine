using System;
using UnityEngine;
using ScenarioSystem.Model;
using ScenarioSystem.Model.Actions;
using ScenarioSystem.Runtime;

namespace ScenarioSystem.Presenter.Executors
{
    public class PortraitGuidanceActionExecutor : IActionExecutor
    {
        public string HandledActionType => "PortraitGuidance";

        public void Execute(ScenarioAction action, ScenarioRuntimeState state, Action onComplete)
        {
            if (action is not PortraitGuidanceAction guidanceAction)
            {
                Debug.LogWarning("[PortraitGuidanceActionExecutor] Invalid action type.");
                onComplete?.Invoke();
                return;
            }

            var manager = UnityEngine.Object.FindFirstObjectByType<ComuStartandEndManager>();
            if (manager != null)
            {
                manager.SetPortraitGuidanceVisible(guidanceAction.Visible);
            }
            else
            {
                Debug.LogWarning("[PortraitGuidanceActionExecutor] ComuStartandEndManager not found in scene.");
            }

            onComplete?.Invoke();
        }
    }
}
