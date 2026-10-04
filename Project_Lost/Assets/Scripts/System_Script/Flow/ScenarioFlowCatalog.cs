using System;
using System.Collections.Generic;
using UnityEngine;

namespace System_Script.Flow
{
    [Serializable]
    public sealed class ScenarioFlowEntry
    {
        [SerializeField] private int targetChapter = 1;
        [SerializeField] private GamePhase targetPhase;
        [SerializeField] private StorySequence sequence;

        public int TargetChapter => targetChapter;
        public GamePhase TargetPhase => targetPhase;
        public StorySequence Sequence => sequence;
    }

    /// <summary>シーンから独立した、章・フェーズ別のシーケンス登録。</summary>
    [CreateAssetMenu(fileName = "ScenarioFlowCatalog", menuName = "Scenario/Flow Catalog")]
    public sealed class ScenarioFlowCatalog : ScriptableObject
    {
        [SerializeField] private StorySequence startingSequence;
        [SerializeField] private List<ScenarioFlowEntry> overrideSequences = new();

        public StorySequence StartingSequence => startingSequence;
        public IReadOnlyList<ScenarioFlowEntry> Overrides => overrideSequences;

        /// <summary>既存の登録順を維持し、最初に一致する設定を返す。</summary>
        public bool TryGetOverride(int chapter, GamePhase phase, out StorySequence sequence)
        {
            sequence = null;
            if (overrideSequences == null) return false;
            foreach (var entry in overrideSequences)
            {
                if (entry == null || entry.TargetChapter != chapter || entry.TargetPhase != phase) continue;
                sequence = entry.Sequence;
                return sequence != null;
            }
            return false;
        }
    }
}
