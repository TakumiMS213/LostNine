using System;
using UnityEngine;
using ScenarioSystem.Model;
using ScenarioSystem.Model.Actions;
using ScenarioSystem.Runtime;

namespace ScenarioSystem.Presenter.Executors
{
    /// <summary>
    /// SceneTransitionAction を実行する Executor。
    /// SceneTransition（フェード付き）を使ってシーン遷移する。
    /// 遷移後は現在のシーンが破棄されるため、onComplete は呼ばない。
    /// </summary>
    public class SceneTransitionActionExecutor : IActionExecutor
    {
        public string HandledActionType => "SceneTransition";

        public void Execute(ScenarioAction action, ScenarioRuntimeState state, Action onComplete)
        {
            if (action is not SceneTransitionAction transitionAction)
            {
                Debug.LogWarning("[SceneTransitionActionExecutor] Invalid action type.");
                onComplete?.Invoke();
                return;
            }

            if (transitionAction.useChapterSelect)
            {
                // ProgressManager 経由でチャプター選択シーンへ
                if (ProgressManager.Instance != null)
                {
                    Debug.Log("[SceneTransitionActionExecutor] Transitioning to ChapterSelect.");
                    ProgressManager.Instance.GoToChapterSelect();
                }
                else
                {
                    Debug.LogWarning("[SceneTransitionActionExecutor] ProgressManager not found.");
                }
            }
            else if (!string.IsNullOrEmpty(transitionAction.targetSceneName))
            {
                // 任意のシーンへ遷移
                Debug.Log($"[SceneTransitionActionExecutor] Transitioning to: {transitionAction.targetSceneName}");
                if (SceneTransition.Instance != null)
                {
                    SceneTransition.Instance.TransitionTo(transitionAction.targetSceneName);
                }
                else
                {
                    UnityEngine.SceneManagement.SceneManager.LoadScene(transitionAction.targetSceneName);
                }
            }
            else
            {
                Debug.LogWarning("[SceneTransitionActionExecutor] No target scene specified.");
                onComplete?.Invoke();
            }

            // 注意: シーン遷移が成功した場合、onComplete は意図的に呼ばない。
            // 現在のシーンが破棄されるため、後続アクションの実行は不要。
        }
    }
}
