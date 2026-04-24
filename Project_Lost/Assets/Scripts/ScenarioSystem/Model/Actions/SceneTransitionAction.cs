using UnityEngine;

namespace ScenarioSystem.Model.Actions
{
    /// <summary>
    /// シーン遷移を実行するアクション。
    /// シナリオの最後のアクションとして配置すること。
    /// （遷移後は現在のシーンが破棄されるため、以降のアクションは実行されない）
    /// </summary>
    [CreateAssetMenu(fileName = "SceneTransitionAction", menuName = "Scenario/Actions/Scene Transition Action")]
    public class SceneTransitionAction : ScenarioAction
    {
        public override string ActionType => "SceneTransition";

        [Tooltip("遷移先のシーン名。空の場合は ProgressManager の ChapterSelect シーンへ遷移する。")]
        public string targetSceneName;

        /// <summary>遷移先がチャプター選択シーンかどうか。</summary>
        [Tooltip("true の場合、ProgressManager.GoToChapterSelect() を使用する。")]
        public bool useChapterSelect = true;
    }
}
