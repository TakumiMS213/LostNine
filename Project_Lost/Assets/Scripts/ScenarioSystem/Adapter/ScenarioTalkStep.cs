using UnityEngine;
using ScenarioSystem.Model;
using ScenarioSystem.Presenter;

namespace System_Script.Flow
{
    /// <summary>
    /// 新シナリオシステム (ScenarioPresenter) 経由で ScenarioData を再生する FlowStep。
    /// 既存の TalkStep（旧 DialogueScenario + MessageWindowManager）と並行して使用可能。
    /// 完全移行後に TalkStep を置き換える。
    /// </summary>
    [CreateAssetMenu(fileName = "ScenarioTalkStep", menuName = "Flow/Steps/Scenario Talk Step")]
    public class ScenarioTalkStep : FlowStep
    {
        [Tooltip("再生するシナリオデータ（新システム）。")]
        public ScenarioData scenario;

        public override void Execute(GameFlowDirector director)
        {
            var presenter = FindObjectOfType<ScenarioPresenter>();

            if (presenter != null && scenario != null)
            {
                Debug.Log($"[ScenarioTalkStep] Starting scenario via new system: {scenario.name}");
                presenter.StartScenario(scenario, () =>
                {
                    director.NextStep();
                });
            }
            else
            {
                if (presenter == null)
                    Debug.LogWarning("[ScenarioTalkStep] ScenarioPresenter not found in scene.");
                if (scenario == null)
                    Debug.LogWarning("[ScenarioTalkStep] ScenarioData is null.");

                director.NextStep();
            }
        }
    }
}
