using UnityEngine;

namespace ScenarioSystem.Model
{
    /// <summary>
    /// シナリオの1ステップを表す抽象アクション。
    /// SOとして定義し、ScenarioData のアクションリストに格納する。
    /// 実行ロジックは持たない — IActionExecutor が解釈・実行する。
    /// </summary>
    public abstract class ScenarioAction : ScriptableObject
    {
        /// <summary>アクションの種別を示す識別子。Executor のディスパッチに使用する。</summary>
        public abstract string ActionType { get; }

        protected virtual void OnValidate() => ScenarioDataRevision.Invalidate();

        /// <summary>選択肢の遷移先などをコードから編集した場合に呼ぶ。</summary>
        public void NotifyDataChanged() => ScenarioDataRevision.Invalidate();
    }
}
