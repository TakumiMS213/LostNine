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
            if (action is not DialogueAction dialogue)
            {
                Debug.LogWarning("[DialogueActionExecutor] Invalid action type.");
                onComplete?.Invoke();
                return;
            }

            // タイピング中フラグを立てる
            state.IsTyping = true;

            // View にデータを通知（View が実際のタイピング演出を担当）
            var eventData = DialogueEventData.FromAction(dialogue);
            ScenarioEventBus.RaiseDialogueRequested(eventData);

            // onComplete は呼ばない。
            // タイピング完了 → OnTypingCompleted イベント → Presenter が IsWaitingForInput = true に。
            // ユーザークリック → OnAdvanceRequested → Presenter が次のアクションへ。
            // つまりこの Executor は「fire and forget」で、進行制御は Presenter に委ねる。
        }
    }
}
