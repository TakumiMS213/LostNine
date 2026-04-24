using System;
using UnityEngine;
using ScenarioSystem.Model;
using ScenarioSystem.Model.Actions;
using ScenarioSystem.Runtime;

namespace ScenarioSystem.Presenter.Executors
{
    /// <summary>
    /// PortraitInteractableAction を実行する Executor。
    /// ComuStartandEndManager を探して状態を切り替える。
    /// </summary>
    public class PortraitInteractableActionExecutor : IActionExecutor
    {
        public string HandledActionType => "PortraitInteractable";

        public void Execute(ScenarioAction action, ScenarioRuntimeState state, Action onComplete)
        {
            if (action is not PortraitInteractableAction interactableAction)
            {
                Debug.LogWarning("[PortraitInteractableActionExecutor] Invalid action type.");
                onComplete?.Invoke();
                return;
            }

            var manager = UnityEngine.Object.FindFirstObjectByType<ComuStartandEndManager>();
            if (manager != null)
            {
                manager.SetPortraitInteractable(interactableAction.isInteractable, interactableAction.updateOverlay);
                Debug.Log($"[PortraitInteractableActionExecutor] Portrait interactable set to {interactableAction.isInteractable}");
            }
            else
            {
                Debug.LogWarning("[PortraitInteractableActionExecutor] ComuStartandEndManager not found in scene.");
            }

            // 即座に次のアクションへ進む
            onComplete?.Invoke();
        }
    }
}
