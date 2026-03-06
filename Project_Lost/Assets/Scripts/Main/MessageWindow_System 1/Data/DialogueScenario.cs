using System.Collections.Generic;
using UnityEngine;

namespace MessageWindowSystem.Data
{
    [CreateAssetMenu(fileName = "NewDialogueScenario", menuName = "MessageWindow/Dialogue Scenario")]
    public class DialogueScenario : ScriptableObject
    {
        [Tooltip("Unique ID for this scenario (optional, for lookup)")]
        public string scenarioId;

        [Tooltip("The next scenario to play automatically after this one ends.")]
        public DialogueScenario nextScenario;

        [Tooltip("Whether keywords are interactive in this scenario.")]
        public bool enableKeywords = true;

        [Tooltip("Whether the portrait can be clicked to toggle communication in this scenario.")]
        public bool enablePortraitClick = true;

        [Tooltip("If true, this scenario will restart from the beginning when it ends.")]
        public bool loopScenario = false;

        [Tooltip("If true, calls ComuStartandEndManager.ToggleComu() when this scenario ends.")]
        public bool toggleComuOnEnd = false;

        [Header("Progress Update")]
        [Tooltip("If true, updates ProgressManager when this scenario ends.")]
        public bool updateProgressOnEnd = false;

        [Tooltip("How to update the progress.")]
        public ProgressActionType progressAction = ProgressActionType.AdvancePhase;

        [Tooltip("Target chapter (only used if SetDirectly).")]
        public int targetChapter = 1;

        [Tooltip("Target phase (only used if SetDirectly).")]
        public GamePhase targetPhase = GamePhase.Dialogue;

        public List<DialogueLine> lines;
    }

    public enum ProgressActionType
    {
        AdvancePhase,
        AdvanceChapter,
        SetDirectly
    }
}
