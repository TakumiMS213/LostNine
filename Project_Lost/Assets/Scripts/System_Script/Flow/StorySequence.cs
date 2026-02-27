using System.Collections.Generic;
using UnityEngine;

namespace System_Script.Flow
{
    /// <summary>
    /// Defines a sequence of steps to be executed by the GameFlowDirector.
    /// </summary>
    [CreateAssetMenu(fileName = "NewStorySequence", menuName = "Flow/Story Sequence")]
    public class StorySequence : ScriptableObject
    {
        [Tooltip("List of steps to execute in order.")]
        public List<FlowStep> steps = new List<FlowStep>();
    }
}
