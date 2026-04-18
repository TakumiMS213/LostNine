using System.Collections.Generic;
using UnityEngine;

namespace ScenarioSystem.Model.Actions
{
    /// <summary>
    /// 選択肢表示アクション。選択肢のリストと、各選択肢の遷移先を定義する。
    /// 旧 ChoiceData のリストに相当。
    /// </summary>
    [CreateAssetMenu(fileName = "ChoiceAction", menuName = "Scenario/Actions/Choice")]
    public class ChoiceAction : ScenarioAction
    {
        public override string ActionType => "Choice";

        [Tooltip("表示する選択肢のリスト")]
        public List<ChoiceEntry> choices = new();
    }

    /// <summary>
    /// 1つの選択肢の定義。表示テキストと遷移先を保持する。
    /// </summary>
    [System.Serializable]
    public class ChoiceEntry
    {
        [Tooltip("選択肢ボタンに表示するテキスト")]
        public string choiceText;

        [Tooltip("選択時に遷移するシナリオ（null = 現シナリオ終了）")]
        public ScenarioData nextScenario;

        [Tooltip("選択肢の一意ID（ロジックフック用）")]
        public string choiceId;
    }
}
