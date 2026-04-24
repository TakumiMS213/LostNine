using System;
using UnityEngine;
using ScenarioSystem.Model;
using ScenarioSystem.Model.Actions;
using ScenarioSystem.Runtime;
using ScenarioSystem.Adapter;

namespace ScenarioSystem.Presenter.Executors
{
    /// <summary>
    /// ProgressScenarioAction を実行する Executor。
    /// ProgressManager から現在のキー（例: "Ch1_Dialogue"）を取得し、
    /// ScenarioDataDatabase から検索して ScenarioPresenter でチェーン再生する。
    /// </summary>
    public class ProgressScenarioActionExecutor : IActionExecutor
    {
        public string HandledActionType => "ProgressScenario";

        private readonly ScenarioPresenter _presenter;
        private readonly ScenarioDataDatabase _database;

        public ProgressScenarioActionExecutor(ScenarioPresenter presenter, ScenarioDataDatabase database)
        {
            _presenter = presenter;
            _database = database;
        }

        public void Execute(ScenarioAction action, ScenarioRuntimeState state, Action onComplete)
        {
            if (ProgressManager.Instance == null)
            {
                Debug.LogWarning("[ProgressScenarioActionExecutor] ProgressManager not found. Skipping.");
                onComplete?.Invoke();
                return;
            }

            if (_database == null)
            {
                Debug.LogWarning("[ProgressScenarioActionExecutor] ScenarioDataDatabase not assigned. Skipping.");
                onComplete?.Invoke();
                return;
            }

            string key = ProgressManager.Instance.GetScenarioKey();
            var nextScenario = _database.GetById(key);

            if (nextScenario != null)
            {
                Debug.Log($"[ProgressScenarioActionExecutor] Chaining to progress scenario: {key} ({nextScenario.name})");
                
                // 現在のシナリオを中断し、Progress に応じたシナリオにチェーンする。
                // onComplete をリレーすることで、元の呼び出し元（FlowStep 等）のコールバックが保持される。
                _presenter.StartScenario(nextScenario, state.OnComplete);
                
                // ※ StartScenario がステートをリセットするため、ここで onComplete を呼ぶ必要はない
            }
            else
            {
                Debug.LogWarning($"[ProgressScenarioActionExecutor] Scenario '{key}' not found in database. Proceeding to next action.");
                onComplete?.Invoke();
            }
        }
    }
}
