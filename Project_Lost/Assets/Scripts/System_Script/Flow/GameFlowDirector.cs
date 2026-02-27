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
        [Header("Default Settings")]
        [SerializeField] private StorySequence startingSequence;
        [SerializeField] private bool playOnStart = false;

        [Header("Context-Aware Overrides")]
        [Tooltip("If current progress matches an entry, that sequence will play instead of the default.")]
        [SerializeField] private List<SequenceOverride> overrideSequences = new List<SequenceOverride>();

        private StorySequence _currentSequence;
        private int _currentStepIndex = 0;
        private bool _isPlaying = false;

        private void Start()
        {
            if (playOnStart)
            {
                // check for overrides first
                var overrideSeq = GetOverrideSequence();
                if (overrideSeq != null)
                {
                     PlaySequence(overrideSeq);
                }
                else if (startingSequence != null)
                {
                    PlaySequence(startingSequence);
                }
            }
        }

        private void OnEnable()
        {
            if (ProgressManager.Instance != null)
            {
                ProgressManager.Instance.OnKeywordThresholdReached += OnKeywordThresholdReached;
            }
        }

        private void OnDisable()
        {
            if (ProgressManager.Instance != null)
            {
                ProgressManager.Instance.OnKeywordThresholdReached -= OnKeywordThresholdReached;
            }
        }

        /// <summary>
        /// キーワード獲得数がしきい値に達した時、現在の章のシークエンスを起動する。
        /// </summary>
        private void OnKeywordThresholdReached()
        {
            if (_isPlaying)
            {
                Debug.LogWarning("[GameFlowDirector] Sequence already playing, ignoring keyword threshold.");
                return;
            }

            var overrideSeq = GetOverrideSequence();
            if (overrideSeq != null)
            {
                Debug.Log($"[GameFlowDirector] Keyword threshold reached. Launching sequence for Chapter {ProgressManager.Instance.CurrentChapter}.");
                PlaySequence(overrideSeq);
            }
            else if (startingSequence != null)
            {
                Debug.Log("[GameFlowDirector] Keyword threshold reached. No override found, using default sequence.");
                PlaySequence(startingSequence);
            }
            else
            {
                Debug.LogWarning("[GameFlowDirector] Keyword threshold reached but no sequence found to play.");
            }
        }

        private StorySequence GetOverrideSequence()
        {
            if (ProgressManager.Instance == null) return null;
            
            int currentChapter = ProgressManager.Instance.CurrentChapter;
            GamePhase currentPhase = ProgressManager.Instance.CurrentPhase;

            // Debug log to check what we are looking for
            Debug.Log($"[GameFlowDirector] GetOverrideSequence: Checking for Ch{currentChapter} - {currentPhase}. Registered overrides: {overrideSequences.Count}");

            foreach (var mapping in overrideSequences)
            {
                if (mapping.targetChapter == currentChapter && mapping.targetPhase == currentPhase)
                {
                    Debug.Log($"[GameFlowDirector] Match found! Sequence: {mapping.sequence.name}");
                    return mapping.sequence;
                }
                else
                {
                    // Debug breakdown for failed matches (optional, maybe verbose but helpful now)
                    // Debug.Log($"[GameFlowDirector] Skip: Ch{mapping.targetChapter}-{mapping.targetPhase} != Current");
                }
            }
            
            Debug.Log($"[GameFlowDirector] No override found for Ch{currentChapter} - {currentPhase}.");
            return null;
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
