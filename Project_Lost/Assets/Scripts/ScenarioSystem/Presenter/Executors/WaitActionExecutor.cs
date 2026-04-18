using System;
using System.Collections;
using UnityEngine;
using ScenarioSystem.Model;
using ScenarioSystem.Model.Actions;
using ScenarioSystem.Runtime;

namespace ScenarioSystem.Presenter.Executors
{
    /// <summary>
    /// WaitAction を実行する Executor。
    /// 指定秒数待機してから完了する。
    /// MonoBehaviour の Coroutine を使用するため、ホスト参照が必要。
    /// </summary>
    public class WaitActionExecutor : IActionExecutor
    {
        public string HandledActionType => "Wait";

        private readonly MonoBehaviour _coroutineHost;

        /// <summary>
        /// コルーチンを実行するための MonoBehaviour ホストを受け取る。
        /// 通常は ScenarioPresenter 自身を渡す。
        /// </summary>
        public WaitActionExecutor(MonoBehaviour coroutineHost)
        {
            _coroutineHost = coroutineHost;
        }

        public void Execute(ScenarioAction action, ScenarioRuntimeState state, Action onComplete)
        {
            if (action is not WaitAction wait)
            {
                Debug.LogWarning("[WaitActionExecutor] Invalid action type.");
                onComplete?.Invoke();
                return;
            }

            if (wait.duration <= 0f)
            {
                onComplete?.Invoke();
                return;
            }

            _coroutineHost.StartCoroutine(WaitRoutine(wait.duration, onComplete));
        }

        private static IEnumerator WaitRoutine(float duration, Action onComplete)
        {
            yield return new WaitForSeconds(duration);
            onComplete?.Invoke();
        }
    }
}
