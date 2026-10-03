using System.Collections.Generic;
using UnityEngine;

namespace ScenarioSystem.Model
{
    /// <summary>
    /// シナリオ1本分のデータを定義する ScriptableObject。
    /// アクションの順序リストと、連鎖・ループ設定を保持する編集用データ。
    /// 再生位置などの実行状態は ScenarioRuntimeState が担当し、このSOには書き戻さない。
    /// </summary>
    [CreateAssetMenu(fileName = "NewScenarioData", menuName = "Scenario/Scenario Data")]
    public class ScenarioData : ScriptableObject
    {
        [Tooltip("ID検索するシナリオの一意ID。直接参照だけで再生するシナリオは空欄でも可。")]
        public string scenarioId;

        [Tooltip("メインウィンドウを表示するか（オーバーレイ専用の場合は false にする）")]
        public bool showMainWindow = true;

        [Tooltip("実行するアクションの順序リスト")]
        public List<ScenarioAction> actions = new();

        [Tooltip("終了後に自動再生するシナリオ（null = なし）")]
        public ScenarioData nextScenario;

        [Tooltip("ループ再生するか")]
        public bool loop = false;

        private void OnValidate() => ScenarioDataRevision.Invalidate();

        /// <summary>コードから編集した場合の通知。Inspector編集はOnValidateが通知する。</summary>
        public void NotifyDataChanged() => ScenarioDataRevision.Invalidate();
    }
}
