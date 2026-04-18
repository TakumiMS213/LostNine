using System;
using ScenarioSystem.Model;
using ScenarioSystem.Runtime;

namespace ScenarioSystem.Presenter
{
    /// <summary>
    /// 特定の ScenarioAction 型を実行する責務を持つインターフェース。
    /// 新しい Action 種別ごとに Executor を追加すれば OCP を満たす。
    /// 既存の読み込み・ディスパッチロジックを修正する必要がない。
    /// </summary>
    public interface IActionExecutor
    {
        /// <summary>
        /// この Executor が処理できる ActionType 文字列を返す。
        /// ScenarioAction.ActionType と一致させること。
        /// </summary>
        string HandledActionType { get; }

        /// <summary>
        /// アクションを実行する。
        /// 完了時に onComplete を呼び出すこと（非同期処理でも必ず呼ぶ）。
        /// </summary>
        /// <param name="action">実行するアクション（型に応じてキャスト）。</param>
        /// <param name="state">現在のランタイム状態（読み書き可能）。</param>
        /// <param name="onComplete">アクション完了時に呼ぶコールバック。</param>
        void Execute(ScenarioAction action, ScenarioRuntimeState state, Action onComplete);
    }
}
