using System;
using System.Collections;
using UnityEngine;
using ScenarioSystem.Model;
using ScenarioSystem.Model.Actions;
using ScenarioSystem.Runtime;
using ScenarioSystem.Events;

namespace ScenarioSystem.Presenter.Executors
{
    /// <summary>
    /// OverlayAction を実行する Executor。
    /// 指定秒数待機するか、ユーザークリックを待つ。
    /// </summary>
    public class OverlayActionExecutor : IActionExecutor
    {
        public string HandledActionType => "Overlay";

        private readonly MonoBehaviour _coroutineHost;

        /// <summary>
        /// コルーチンを実行するための MonoBehaviour ホストを受け取る。
        /// </summary>
        public OverlayActionExecutor(MonoBehaviour coroutineHost)
        {
            _coroutineHost = coroutineHost;
        }

        public void Execute(ScenarioAction action, ScenarioRuntimeState state, Action onComplete)
        {
            if (action is not OverlayAction overlay)
            {
                Debug.LogWarning("[OverlayActionExecutor] Invalid action type.");
                onComplete?.Invoke();
                return;
            }

            // View 側にデータ送信
            ScenarioEventBus.RaiseOverlayRequested(new OverlayEventData(overlay.text, overlay.portrait, overlay.portraitPosition));

            if (overlay.displayDuration <= 0f)
            {
                // クリック待ち（Presenter に管理を委ねる）
                state.IsWaitingForInput = true;
                // 注意：クリックが来たタイミングで Presenter.Advance() が走り、
                // AdvanceToNextAction() 内で自動的に RaiseOverlayDismissed() が呼ばれます。
            }
            else
            {
                // 指定秒数後に自動で進む
                _coroutineHost.StartCoroutine(WaitAutoDismiss(overlay.displayDuration, state, onComplete));
            }
        }

        private IEnumerator WaitAutoDismiss(float duration, ScenarioRuntimeState state, Action onComplete)
        {
            yield return new WaitForSeconds(duration);
            
            // 自動ディスミスの場合はここでイベントを発火する
            ScenarioEventBus.RaiseOverlayDismissed();
            
            // 次へ進む
            onComplete?.Invoke();
        }
    }
}
