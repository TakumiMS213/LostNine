using System;
using ScenarioSystem.Model;

namespace ScenarioSystem.Runtime
{
    /// <summary>
    /// 1つのシナリオ実行セッションの状態を保持するクラス。
    /// ScriptableObject には一切触れず、Presenter がこの状態を操作する。
    /// SO の不変性を保証するための分離層。
    /// </summary>
    public class ScenarioRuntimeState
    {
        /// <summary>現在再生中のシナリオデータ（読み取りのみ）。</summary>
        public ScenarioData CurrentScenario { get; set; }

        /// <summary>現在実行中のアクションインデックス。</summary>
        public int CurrentActionIndex { get; set; }

        /// <summary>シナリオが再生中かどうか。</summary>
        public bool IsPlaying { get; set; }

        /// <summary>ユーザー入力待ち状態か。</summary>
        public bool IsWaitingForInput { get; set; }

        /// <summary>テキストタイピング中か。</summary>
        public bool IsTyping { get; set; }

        /// <summary>選択肢の入力待ち状態か。</summary>
        public bool IsWaitingForChoice { get; set; }

        /// <summary>シナリオ完了時のコールバック。</summary>
        public Action OnComplete { get; set; }

        /// <summary>
        /// 現在のアクションを取得する（境界チェック付き）。
        /// </summary>
        public ScenarioAction CurrentAction =>
            CurrentScenario != null
            && CurrentScenario.actions != null
            && CurrentActionIndex < CurrentScenario.actions.Count
                ? CurrentScenario.actions[CurrentActionIndex]
                : null;

        /// <summary>
        /// 残りアクションがあるか。
        /// </summary>
        public bool HasNextAction =>
            CurrentScenario != null
            && CurrentScenario.actions != null
            && CurrentActionIndex + 1 < CurrentScenario.actions.Count;

        /// <summary>
        /// 状態を初期化する。
        /// </summary>
        public void Reset()
        {
            CurrentScenario = null;
            CurrentActionIndex = 0;
            IsPlaying = false;
            IsWaitingForInput = false;
            IsTyping = false;
            IsWaitingForChoice = false;
            OnComplete = null;
        }
    }
}
