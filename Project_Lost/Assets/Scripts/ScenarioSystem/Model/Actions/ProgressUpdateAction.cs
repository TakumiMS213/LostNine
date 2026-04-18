using UnityEngine;

namespace ScenarioSystem.Model.Actions
{
    /// <summary>
    /// ゲーム進行状態を更新するアクション。
    /// 旧 DialogueScenario.updateProgressOnEnd の副作用を明示的なアクションとして分離。
    /// </summary>
    [CreateAssetMenu(fileName = "ProgressUpdateAction", menuName = "Scenario/Actions/Progress Update")]
    public class ProgressUpdateAction : ScenarioAction
    {
        public override string ActionType => "ProgressUpdate";

        [Tooltip("進行更新の種類")]
        public ScenarioProgressActionType actionType = ScenarioProgressActionType.AdvancePhase;

        [Tooltip("直接指定時のターゲットチャプター")]
        public int targetChapter = 1;

        [Tooltip("直接指定時のターゲットフェーズ")]
        public GamePhase targetPhase = GamePhase.Dialogue;
    }

    /// <summary>
    /// 進行更新の種類。旧 ProgressActionType の再定義。
    /// </summary>
    public enum ScenarioProgressActionType
    {
        AdvancePhase,
        AdvanceChapter,
        SetDirectly
    }
}
