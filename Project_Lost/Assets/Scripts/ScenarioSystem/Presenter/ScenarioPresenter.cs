using System;
using System.Collections.Generic;
using UnityEngine;
using ScenarioSystem.Model;
using ScenarioSystem.Runtime;
using ScenarioSystem.Events;

namespace ScenarioSystem.Presenter
{
    /// <summary>
    /// シナリオ進行のメインコントローラー。
    /// 登録された IActionExecutor を使ってアクションを順次実行し、
    /// ScenarioEventBus 経由で View / Adapter に通知する。
    /// 自身は UI を直接操作しない。
    /// </summary>
    public class ScenarioPresenter : MonoBehaviour
    {
        #region Private Fields

        private readonly ScenarioRuntimeState _state = new();
        private readonly Dictionary<string, IActionExecutor> _executors = new();

        #endregion

        #region Public Properties

        /// <summary>現在のランタイム状態（読み取り専用公開）。</summary>
        public ScenarioRuntimeState State => _state;

        /// <summary>シナリオが再生中か。</summary>
        public bool IsPlaying => _state.IsPlaying;

        #endregion

        #region Unity Lifecycle

        private void OnEnable()
        {
            // View からの逆通知を購読
            ScenarioEventBus.OnAdvanceRequested += HandleAdvanceRequested;
            ScenarioEventBus.OnTypingCompleted += HandleTypingCompleted;
            ScenarioEventBus.OnChoiceSelected += HandleChoiceSelected;
        }

        private void OnDisable()
        {
            ScenarioEventBus.OnAdvanceRequested -= HandleAdvanceRequested;
            ScenarioEventBus.OnTypingCompleted -= HandleTypingCompleted;
            ScenarioEventBus.OnChoiceSelected -= HandleChoiceSelected;
        }

        #endregion

        #region Public API

        /// <summary>
        /// IActionExecutor を登録する。
        /// 同じ ActionType が既に登録されている場合は上書きする。
        /// </summary>
        public void RegisterExecutor(IActionExecutor executor)
        {
            if (executor == null) return;

            _executors[executor.HandledActionType] = executor;
            Debug.Log($"[ScenarioPresenter] Executor registered: {executor.HandledActionType}");
        }

        /// <summary>
        /// 複数の IActionExecutor を一括登録する。
        /// </summary>
        public void RegisterExecutors(IEnumerable<IActionExecutor> executors)
        {
            foreach (var executor in executors)
                RegisterExecutor(executor);
        }

        /// <summary>
        /// シナリオを開始する。
        /// </summary>
        /// <param name="scenario">再生するシナリオデータ。</param>
        /// <param name="onComplete">シナリオ完了時のコールバック（オプション）。</param>
        public void StartScenario(ScenarioData scenario, Action onComplete = null)
        {
            if (scenario == null)
            {
                Debug.LogWarning("[ScenarioPresenter] StartScenario called with null scenario.");
                onComplete?.Invoke();
                return;
            }

            // 前のシナリオが再生中なら停止
            if (_state.IsPlaying)
            {
                Debug.LogWarning($"[ScenarioPresenter] Interrupting current scenario to start: {scenario.name}");
            }

            _state.Reset();
            _state.CurrentScenario = scenario;
            _state.IsPlaying = true;
            _state.OnComplete = onComplete;

            Debug.Log($"[ScenarioPresenter] StartScenario: {scenario.name} (ID: {scenario.scenarioId})");

            ScenarioEventBus.RaiseScenarioStarted(scenario);
            ScenarioEventBus.RaiseWindowVisibilityChanged(true);

            ExecuteCurrentAction();
        }

        /// <summary>
        /// ユーザー入力で次のアクションへ進む。
        /// </summary>
        public void Advance()
        {
            if (!_state.IsPlaying) return;

            // タイピング中ならスキップ
            if (_state.IsTyping)
            {
                // View 側でスキップ処理 → OnTypingCompleted が発火される
                return;
            }

            // 選択肢待ちなら無視
            if (_state.IsWaitingForChoice) return;

            // 入力待ち状態なら次へ進む
            if (_state.IsWaitingForInput)
            {
                _state.IsWaitingForInput = false;
                AdvanceToNextAction();
            }
        }

        #endregion

        #region Execution Logic

        private void ExecuteCurrentAction()
        {
            var action = _state.CurrentAction;

            if (action == null)
            {
                EndScenario();
                return;
            }

            if (_executors.TryGetValue(action.ActionType, out var executor))
            {
                Debug.Log($"[ScenarioPresenter] Executing [{_state.CurrentActionIndex}]: {action.name} ({action.ActionType})");
                executor.Execute(action, _state, OnActionComplete);
            }
            else
            {
                Debug.LogWarning($"[ScenarioPresenter] No executor found for ActionType: {action.ActionType}. Skipping.");
                OnActionComplete();
            }
        }

        private void OnActionComplete()
        {
            // Dialogue 系のアクションでは IsWaitingForInput = true にして
            // ユーザー入力を待つ。その場合はここでは次に進まない。
            if (_state.IsWaitingForInput || _state.IsWaitingForChoice)
                return;

            AdvanceToNextAction();
        }

        private void AdvanceToNextAction()
        {
            _state.CurrentActionIndex++;
            ExecuteCurrentAction();
        }

        private void EndScenario()
        {
            var completedScenario = _state.CurrentScenario;

            Debug.Log($"[ScenarioPresenter] Scenario ended: {completedScenario?.name}");

            // チェーン先がある場合は連続再生
            if (completedScenario?.loop == true)
            {
                StartScenario(completedScenario, _state.OnComplete);
                return;
            }

            if (completedScenario?.nextScenario != null)
            {
                Debug.Log($"[ScenarioPresenter] Chaining to: {completedScenario.nextScenario.name}");
                StartScenario(completedScenario.nextScenario, _state.OnComplete);
                return;
            }

            // 完全終了
            var onComplete = _state.OnComplete;

            ScenarioEventBus.RaiseWindowVisibilityChanged(false);
            ScenarioEventBus.RaiseScenarioEnded(completedScenario);

            _state.Reset();

            onComplete?.Invoke();
        }

        #endregion

        #region Event Handlers (View → Presenter)

        private void HandleAdvanceRequested()
        {
            Advance();
        }

        private void HandleTypingCompleted()
        {
            if (!_state.IsPlaying) return;

            _state.IsTyping = false;
            _state.IsWaitingForInput = true;
        }

        private void HandleChoiceSelected(int index)
        {
            if (!_state.IsPlaying || !_state.IsWaitingForChoice) return;

            _state.IsWaitingForChoice = false;

            // ChoiceAction から選択肢データを取得
            if (_state.CurrentAction is Model.Actions.ChoiceAction choiceAction
                && index >= 0
                && index < choiceAction.choices.Count)
            {
                var selectedChoice = choiceAction.choices[index];
                Debug.Log($"[ScenarioPresenter] Choice selected [{index}]: {selectedChoice.choiceText}");

                if (selectedChoice.nextScenario != null)
                {
                    StartScenario(selectedChoice.nextScenario, _state.OnComplete);
                    return;
                }
            }

            // 遷移先がない場合はシナリオ終了
            _state.CurrentActionIndex = _state.CurrentScenario.actions.Count; // 末尾に飛ばす
            EndScenario();
        }

        #endregion
    }
}
