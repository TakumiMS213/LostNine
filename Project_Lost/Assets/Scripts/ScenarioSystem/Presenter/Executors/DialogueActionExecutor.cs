using System;
using UnityEngine;
using ScenarioSystem.Model;
using ScenarioSystem.Model.Actions;
using ScenarioSystem.Runtime;
using ScenarioSystem.Events;

namespace ScenarioSystem.Presenter.Executors
{
    /// <summary>
    /// DialogueAction を実行する Executor。
    /// EventBus 経由でテキスト表示を通知し、ユーザー入力待ち状態にする。
    /// </summary>
    public class DialogueActionExecutor : IActionExecutor
    {
        public string HandledActionType => "Dialogue";

        public void Execute(ScenarioAction action, ScenarioRuntimeState state, Action onComplete)
        {
            if (action is not DialogueAction dialogue || dialogue.entries == null || dialogue.entries.Count == 0)
            {
                Debug.LogWarning("[DialogueActionExecutor] Invalid action type or empty entries list.");
                onComplete?.Invoke();
                return;
            }

            // サブインデックスで現在のエントリを取得（境界チェック安全策）
            int index = state.CurrentSubActionIndex;
            if (index >= dialogue.entries.Count) index = dialogue.entries.Count - 1;
            if (index < 0) index = 0;

            var entry = dialogue.entries[index];

            // タイピング中フラグを立てる
            state.IsTyping = true;

            // View にデータを通知（View が実際のタイピング演出を担当）
            var eventData = DialogueEventData.FromEntry(entry);
            ScenarioEventBus.RaiseDialogueRequested(eventData);

            // onComplete は呼ばない。
            // タイピング完了 → OnTypingCompleted イベント → Presenter が IsWaitingForInput = true に。
            // ユーザークリック → OnAdvanceRequested → Presenter が次のアクションへ。
            // つまりこの Executor は「fire and forget」で、進行制御は Presenter に委ねる。
        }
    }
}
