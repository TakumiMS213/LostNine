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
        private int _executionVersion;
        private DialogueEventData _lastDialogueData;
        private bool _hasDialogueData;
        private int _dispatchDepth;
        private Action _pendingScenarioChange;

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
            ScenarioEventBus.OnDialogueRequested += HandleDialogueRequested;
        }

        private void OnDisable()
        {
            ScenarioEventBus.OnAdvanceRequested -= HandleAdvanceRequested;
            ScenarioEventBus.OnTypingCompleted -= HandleTypingCompleted;
            ScenarioEventBus.OnChoiceSelected -= HandleChoiceSelected;
            ScenarioEventBus.OnDialogueRequested -= HandleDialogueRequested;
            InvalidateExecution();
            _state.Reset();
            _hasDialogueData = false;
            _pendingScenarioChange = null;
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

            if (_dispatchDepth > 0)
            {
                DeferScenarioChange(() => StartScenario(scenario, onComplete));
                return;
            }

            // 前のシナリオが再生中なら停止
            if (_state.IsPlaying)
            {
                Debug.LogWarning($"[ScenarioPresenter] Interrupting current scenario to start: {scenario.name}");
            }

            bool dismissOverlay = _state.IsPlaying && _state.CurrentAction is Model.Actions.OverlayAction;
            InvalidateExecution();
            _state.Reset();
            _state.CurrentScenario = scenario;
            _state.IsPlaying = true;
            _state.OnComplete = onComplete;

            Debug.Log($"[ScenarioPresenter] StartScenario: {scenario.name} (ID: {scenario.scenarioId})");

            int version = _executionVersion;
            if (dismissOverlay)
                Dispatch(ScenarioEventBus.RaiseOverlayDismissed);
            if (version != _executionVersion) return;

            Dispatch(() => ScenarioEventBus.RaiseScenarioStarted(scenario));
            if (version != _executionVersion) return;
            Dispatch(() => ScenarioEventBus.RaiseWindowVisibilityChanged(scenario.showMainWindow));
            if (version != _executionVersion) return;

            ExecuteCurrentAction();
        }

        /// <summary>
        /// キーワードなどの補足シナリオを再生し、終了後に元の行へ戻る。
        /// 現在の行より前の進行更新や演出を再実行せず、元の完了通知も維持する。
        /// </summary>
        public void PlayTemporaryScenario(ScenarioData scenario, Action onComplete = null)
        {
            if (scenario == null)
            {
                onComplete?.Invoke();
                return;
            }

            if (_dispatchDepth > 0)
            {
                DeferScenarioChange(() => PlayTemporaryScenario(scenario, onComplete));
                return;
            }

            if (!_state.IsPlaying)
            {
                StartScenario(scenario, onComplete);
                return;
            }

            var suspendedScenario = _state.CurrentScenario;
            int suspendedIndex = _state.CurrentActionIndex;
            int suspendedSubIndex = _state.CurrentSubActionIndex;
            var suspendedCompletion = _state.OnComplete;
            bool hadDialogue = _hasDialogueData;
            var suspendedDialogue = _lastDialogueData;

            StartScenario(scenario, () =>
            {
                int version = _executionVersion;
                onComplete?.Invoke();
                // 完了通知が別の進行を開始した場合、その進行を優先する。
                if (_state.IsPlaying || _pendingScenarioChange != null
                    || version != _executionVersion || suspendedScenario == null)
                    return;

                InvalidateExecution();
                _state.CurrentScenario = suspendedScenario;
                _state.CurrentActionIndex = suspendedIndex;
                _state.CurrentSubActionIndex = suspendedSubIndex;
                _state.OnComplete = suspendedCompletion;
                _state.IsPlaying = true;

                version = _executionVersion;
                Dispatch(() => ScenarioEventBus.RaiseScenarioStarted(suspendedScenario));
                if (version != _executionVersion) return;
                Dispatch(() => ScenarioEventBus.RaiseWindowVisibilityChanged(suspendedScenario.showMainWindow));
                if (version != _executionVersion) return;
                if (hadDialogue)
                {
                    // 元の本文・話者・立ち絵・背景を全文で復元する。
                    // 同じ行のタイプ演出とログ追加を繰り返さない。
                    bool resumeDialogue = _state.CurrentAction is Model.Actions.DialogueAction;
                    _state.IsTyping = resumeDialogue;
                    Dispatch(() => ScenarioEventBus.RaiseDialogueRequested(suspendedDialogue.WithInstantDisplay()));
                    if (version != _executionVersion) return;
                    // Viewの購読順によらずProviderのtyping状態を確定する。
                    Dispatch(ScenarioEventBus.RaiseTypingCompleted);
                    if (version != _executionVersion) return;
                    if (resumeDialogue) return;
                }
                ExecuteCurrentAction();
            });
        }

        /// <summary>会話を中止する。完了扱いにせず、後続の進行は実行しない。</summary>
        public void StopScenario()
        {
            if (_dispatchDepth > 0)
            {
                DeferScenarioChange(StopScenario);
                return;
            }

            var stoppedScenario = _state.CurrentScenario;
            bool dismissOverlay = _state.CurrentAction is Model.Actions.OverlayAction;
            InvalidateExecution();
            _state.Reset();
            _hasDialogueData = false;
            Dispatch(() =>
            {
                if (dismissOverlay)
                    ScenarioEventBus.RaiseOverlayDismissed();
                ScenarioEventBus.RaiseWindowVisibilityChanged(false);
                if (stoppedScenario != null)
                    ScenarioEventBus.RaiseScenarioEnded(stoppedScenario);
            });
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
            if (!_state.IsPlaying) return;

            var action = _state.CurrentAction;

            if (action == null)
            {
                EndScenario();
                return;
            }

            int version = ++_executionVersion;
            _state.IsTyping = false;
            _state.IsWaitingForInput = false;
            _state.IsWaitingForChoice = false;
            bool completed = false;
            void Complete()
            {
                // 差替え前の待機・演出が後から完了しても、新しい会話を進めない。
                if (completed || !_state.IsPlaying || version != _executionVersion) return;
                completed = true;
                OnActionComplete();
            }

            if (_executors.TryGetValue(action.ActionType, out var executor))
            {
                Debug.Log($"[ScenarioPresenter] Executing [{_state.CurrentActionIndex}]: {action.name} ({action.ActionType})");
                Dispatch(() => executor.Execute(action, _state, Complete));
            }
            else
            {
                Debug.LogWarning($"[ScenarioPresenter] No executor found for ActionType: {action.ActionType}. Skipping.");
                Complete();
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
            if (!_state.IsPlaying) return;
            int version = ++_executionVersion;
            if (_state.CurrentAction is Model.IMultiStepAction multiStep 
                && _state.CurrentSubActionIndex < multiStep.StepCount - 1)
            {
                _state.CurrentSubActionIndex++;
                ExecuteCurrentAction();
                return;
            }

            if (_state.CurrentAction is Model.Actions.OverlayAction)
            {
                Dispatch(ScenarioEventBus.RaiseOverlayDismissed);
                if (version != _executionVersion) return;
            }

            _state.CurrentSubActionIndex = 0;
            _state.CurrentActionIndex++;
            ExecuteCurrentAction();
        }

        private void EndScenario()
        {
            if (!_state.IsPlaying) return;
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
            InvalidateExecution();
            _state.Reset();
            _hasDialogueData = false;

            Dispatch(() =>
            {
                if (completedScenario?.showMainWindow == true)
                    ScenarioEventBus.RaiseWindowVisibilityChanged(false);
                ScenarioEventBus.RaiseScenarioEnded(completedScenario);
            });

            onComplete?.Invoke();
        }

        private void InvalidateExecution()
        {
            _executionVersion++;
            StopAllCoroutines();
        }

        private void DeferScenarioChange(Action change)
        {
            InvalidateExecution();
            _pendingScenarioChange = change;
        }

        private void Dispatch(Action notification)
        {
            _dispatchDepth++;
            try
            {
                notification();
            }
            finally
            {
                _dispatchDepth--;
                if (_dispatchDepth == 0)
                {
                    // 古いStarted/Endedの全購読者が処理を終えてから差し替える。
                    // 先に新しいUIを描くと、残りの古い通知がそれを消してしまう。
                    var pendingChange = _pendingScenarioChange;
                    _pendingScenarioChange = null;
                    pendingChange?.Invoke();
                }
            }
        }

        #endregion

        #region Event Handlers (View → Presenter)

        private void HandleAdvanceRequested()
        {
            Advance();
        }

        private void HandleDialogueRequested(DialogueEventData data)
        {
            if (!_state.IsPlaying) return;
            _lastDialogueData = data;
            _hasDialogueData = true;
        }

        private void HandleTypingCompleted()
        {
            if (!_state.IsPlaying || !_state.IsTyping
                || _state.CurrentAction is not Model.Actions.DialogueAction) return;

            _state.IsTyping = false;
            _state.IsWaitingForInput = true;
        }

        private void HandleChoiceSelected(int index)
        {
            if (!_state.IsPlaying || !_state.IsWaitingForChoice) return;

            // ChoiceAction から選択肢データを取得
            if (_state.CurrentAction is Model.Actions.ChoiceAction choiceAction
                && index >= 0
                && choiceAction.choices != null
                && index < choiceAction.choices.Count
                && choiceAction.choices[index] != null)
            {
                _state.IsWaitingForChoice = false;
                var selectedChoice = choiceAction.choices[index];
                Debug.Log($"[ScenarioPresenter] Choice selected [{index}]: {selectedChoice.choiceText}");

                if (selectedChoice.nextScenario != null)
                {
                    StartScenario(selectedChoice.nextScenario, _state.OnComplete);
                    return;
                }
            }
            else
            {
                // 古いボタンや不正なインデックスで会話を終了させない。
                return;
            }

            // 遷移先がない場合はシナリオ終了
            _state.CurrentActionIndex = _state.CurrentScenario.actions.Count; // 末尾に飛ばす
            EndScenario();
        }

        #endregion
    }
}
