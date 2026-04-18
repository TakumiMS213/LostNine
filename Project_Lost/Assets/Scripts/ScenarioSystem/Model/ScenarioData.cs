using System.Collections.Generic;
using UnityEngine;

namespace ScenarioSystem.Model
{
    /// <summary>
    /// シナリオ1本分のデータを定義する ScriptableObject。
    /// アクションの順序リストと、連鎖・ループ設定のみを保持する。
    /// 実行時の状態は ScenarioRuntimeState が担当するため、このSO は完全に不変。
    /// </summary>
    [CreateAssetMenu(fileName = "NewScenarioData", menuName = "Scenario/Scenario Data")]
    public class ScenarioData : ScriptableObject
    {
        [Tooltip("一意のシナリオID（検索・参照用）")]
        public string scenarioId;

        [Tooltip("実行するアクションの順序リスト")]
        public List<ScenarioAction> actions = new();

        [Tooltip("終了後に自動再生するシナリオ（null = なし）")]
        public ScenarioData nextScenario;

        [Tooltip("ループ再生するか")]
        public bool loop = false;
    }
}
