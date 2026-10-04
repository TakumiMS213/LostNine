using System.Collections.Generic;
using UnityEngine;

namespace System_Script.Flow
{
    /// <summary>
    /// Manually maps a progress state (Chapter/Phase) to a specific sequence.
    /// </summary>
    [System.Serializable]
    public class SequenceOverride
    {
        public int targetChapter;
        // Assuming GamePhase is defined globally or imported. 
        // If namespaces are an issue, might need full qualification or just int.
        public GamePhase targetPhase;
        public StorySequence sequence;
    }

    /// <summary>
    /// Manages the execution of a StorySequence.
    /// </summary>
    public class GameFlowDirector : MonoBehaviour
    {
        [Header("Scenario Authoring")]
        [SerializeField] private ScenarioFlowCatalog flowCatalog;

        [Header("Default Settings")]
        [SerializeField] private StorySequence startingSequence;
        [SerializeField] private bool playOnStart = false;

        [Header("Context-Aware Overrides")]
        [Tooltip("If current progress matches an entry, that sequence will play instead of the default.")]
        [SerializeField] private List<SequenceOverride> overrideSequences = new List<SequenceOverride>();

        private StorySequence _currentSequence;
        private int _currentStepIndex = 0;
        private bool _isPlaying = false;
        private ProgressManager _progressManager;
        private bool _keywordCompletionPending;
        private int _keywordCompletionChapter = -1;

        private StorySequence StartingSequence => flowCatalog != null ? flowCatalog.StartingSequence : startingSequence;

        private void Start()
        {
            SubscribeProgressManager();
            if (playOnStart)
            {
                // check for overrides first
                var overrideSeq = GetOverrideSequence();
                if (overrideSeq != null)
                {
                     PlaySequence(overrideSeq);
                }
                else if (StartingSequence != null)
                {
                    PlaySequence(StartingSequence);
                }
            }
        }

        private void OnEnable()
        {
            SubscribeProgressManager();
        }

        private void OnDisable()
        {
            UnsubscribeProgressManager();
            _keywordCompletionPending = false;
        }

        private void SubscribeProgressManager()
        {
            if (_progressManager == ProgressManager.Instance && _progressManager != null) return;
            UnsubscribeProgressManager();
            _progressManager = ProgressManager.Instance;
            if (_progressManager == null) return;
            _progressManager.OnKeywordThresholdReached += OnKeywordThresholdReached;
            _progressManager.OnProgressChanged += HandleProgressChanged;
            HandleProgressChanged();
        }

        private void UnsubscribeProgressManager()
        {
            if (_progressManager != null)
            {
                _progressManager.OnKeywordThresholdReached -= OnKeywordThresholdReached;
                _progressManager.OnProgressChanged -= HandleProgressChanged;
            }
            _progressManager = null;
        }

        private void HandleProgressChanged()
        {
            if (_progressManager == null) return;
            // 同じ章をLOADし直した場合も、リセット後は完了演出をもう一度再生する。
            if (!_progressManager.AllKeywordsCollected)
                _keywordCompletionChapter = -1;
            _keywordCompletionPending = _progressManager.AllKeywordsCollected
                && _progressManager.CurrentPhase == GamePhase.Extraction
                && _keywordCompletionChapter != _progressManager.CurrentChapter;
        }

        private void LateUpdate()
        {
            if (_progressManager != ProgressManager.Instance) SubscribeProgressManager();
            if (!_keywordCompletionPending || _progressManager == null || _isPlaying) return;
            if (!_progressManager.AllKeywordsCollected || _progressManager.CurrentPhase != GamePhase.Extraction)
            {
                _keywordCompletionPending = false;
                return;
            }

            // 進行更新のイベント配信・そのアクションの完了処理が終わってから開始する。
            // Dialogue中に集め終えていても、その会話はExtraction到達まで維持する。
            _keywordCompletionPending = false;
            var sequence = GetOverrideSequence();
            if (sequence == null) return;
            _keywordCompletionChapter = _progressManager.CurrentChapter;
            PlaySequence(sequence);
        }

        /// <summary>
        /// キーワード獲得数がしきい値に達した時に呼ばれる。
        /// overrideSequences に明示的な登録がある場合のみシーケンスを起動する。
        ///
        /// ※ startingSequence へのフォールバックは行わない。
        ///   Extractionフェーズ等でoverrideが未登録の場合、GFDは何もしない。
        ///   （欠片演出とD&D解放は MemoryFragmentSystem / DragToSceneItem が担当）
        /// </summary>
        private void OnKeywordThresholdReached()
        {
            HandleProgressChanged();
        }

        private StorySequence GetOverrideSequence()
        {
            if (ProgressManager.Instance == null) return null;
            
            int currentChapter = ProgressManager.Instance.CurrentChapter;
            GamePhase currentPhase = ProgressManager.Instance.CurrentPhase;

            if (flowCatalog != null)
                return flowCatalog.TryGetOverride(currentChapter, currentPhase, out var sequence) ? sequence : null;
            if (overrideSequences == null) return null;

            // Debug log to check what we are looking for
            Debug.Log($"[GameFlowDirector] GetOverrideSequence: Checking for Ch{currentChapter} - {currentPhase}. Registered overrides: {overrideSequences.Count}");

            foreach (var mapping in overrideSequences)
            {
                if (mapping != null && mapping.targetChapter == currentChapter && mapping.targetPhase == currentPhase)
                {
                    Debug.Log($"[GameFlowDirector] Match found! Sequence: {(mapping.sequence != null ? mapping.sequence.name : "<none>")}");
                    return mapping.sequence;
                }
            }
            
            Debug.Log($"[GameFlowDirector] No override found for Ch{currentChapter} - {currentPhase}.");
            return null;
        }

        /// <summary>
        /// 現在の Chapter / Phase に対応するシーケンスを探して再生する。
        /// overrideSequences から一致するものを返し、なければ startingSequence を使う。
        /// MainSceneFlowController から呼ぶことで、9章すべての分岐をここ1か所で管理できる。
        /// </summary>
        /// <returns>対応するシーケンスが見つかり再生を開始した場合 true</returns>
        public bool PlaySequenceForCurrentProgress()
        {
            var seq = GetOverrideSequence();
            if (seq != null)
            {
                PlaySequence(seq);
                return true;
            }
            if (StartingSequence != null)
            {
                Debug.Log("[GameFlowDirector] PlaySequenceForCurrentProgress: using startingSequence fallback.");
                PlaySequence(StartingSequence);
                return true;
            }
            Debug.LogWarning("[GameFlowDirector] PlaySequenceForCurrentProgress: No sequence found.");
            return false;
        }

        /// <summary>
        /// Starts playing a new sequence from the beginning.
        /// </summary>
        public void PlaySequence(StorySequence sequence)
        {
            if (sequence == null) return;

            _currentSequence = sequence;
            _currentStepIndex = 0;
            _isPlaying = true;

            Debug.Log($"[GameFlowDirector] Starting Sequence: {sequence.name}");
            ExecuteCurrentStep();
        }

        /// <summary>
        /// Advances to the next step in the sequence.
        /// </summary>
        public void NextStep()
        {
            if (!_isPlaying) return;

            _currentStepIndex++;
            ExecuteCurrentStep();
        }

        private void ExecuteCurrentStep()
        {
            if (_currentSequence == null || _currentSequence.steps == null)
            {
                EndSequence();
                return;
            }

            if (_currentStepIndex >= _currentSequence.steps.Count)
            {
                EndSequence();
                return;
            }

            var step = _currentSequence.steps[_currentStepIndex];
            if (step != null)
            {
                Debug.Log($"[GameFlowDirector] Executing Step {_currentStepIndex}: {step.name} ({step.GetType().Name})");
                step.Execute(this);
            }
            else
            {
                Debug.LogWarning($"[GameFlowDirector] Step at index {_currentStepIndex} is null. Skipping.");
                NextStep();
            }
        }

        private void EndSequence()
        {
            Debug.Log($"[GameFlowDirector] Sequence Completed: {_currentSequence?.name}");
            _isPlaying = false;
            _currentSequence = null;
        }
    }
}
