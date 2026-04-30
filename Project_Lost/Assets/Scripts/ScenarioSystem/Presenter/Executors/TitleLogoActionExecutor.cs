using System;
using System.Collections;
using UnityEngine;
using ScenarioSystem.Events;
using ScenarioSystem.Model;
using ScenarioSystem.Model.Actions;
using ScenarioSystem.Runtime;

namespace ScenarioSystem.Presenter.Executors
{
    public class TitleLogoActionExecutor : IActionExecutor
    {
        public string HandledActionType => "TitleLogo";

        private readonly MonoBehaviour _coroutineHost;

        public TitleLogoActionExecutor(MonoBehaviour coroutineHost)
        {
            _coroutineHost = coroutineHost;
        }

        public void Execute(ScenarioAction action, ScenarioRuntimeState state, Action onComplete)
        {
            if (action is not TitleLogoAction titleLogo)
            {
                Debug.LogWarning("[TitleLogoActionExecutor] Invalid action type.");
                onComplete?.Invoke();
                return;
            }

            ScenarioEventBus.RaiseTitleLogoRequested(TitleLogoEventData.FromAction(titleLogo));

            float totalDuration = titleLogo.TotalDuration;
            if (totalDuration <= 0f || _coroutineHost == null)
            {
                onComplete?.Invoke();
                return;
            }

            _coroutineHost.StartCoroutine(WaitForTitleLogo(totalDuration, onComplete));
        }

        private static IEnumerator WaitForTitleLogo(float duration, Action onComplete)
        {
            yield return new WaitForSeconds(duration);
            yield return null;
            onComplete?.Invoke();
        }
    }
}
