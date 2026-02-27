using UnityEngine;

namespace System_Script.Flow
{
    [CreateAssetMenu(fileName = "JumpSequenceStep", menuName = "Flow/Steps/Jump Sequence Step")]
    public class JumpSequenceStep : FlowStep
    {
        [Tooltip("The next sequence to play.")]
        public StorySequence nextSequence;

        public override void Execute(GameFlowDirector director)
        {
            if (nextSequence != null)
            {
                Debug.Log($"[JumpSequenceStep] Jumping to sequence: {nextSequence.name}");
                director.PlaySequence(nextSequence);
            }
            else
            {
                Debug.LogWarning("[JumpSequenceStep] Next sequence is null. Ending current sequence.");
                director.NextStep(); // Effectively ends if this was the last step
            }
        }
    }
}
