using UnityEngine;

namespace System_Script.Flow
{
    /// <summary>
    /// Epilogue終了後にChapterSelectシーンへ遷移するFlowStep。
    /// </summary>
    [CreateAssetMenu(fileName = "ChapterSelectStep", menuName = "Flow/Steps/Chapter Select Step")]
    public class ChapterSelectStep : FlowStep
    {
        public override void Execute(GameFlowDirector director)
        {
            if (ProgressManager.Instance != null)
            {
                Debug.Log("[ChapterSelectStep] Transitioning to ChapterSelect scene.");
                ProgressManager.Instance.GoToChapterSelect();
            }
            else
            {
                Debug.LogWarning("[ChapterSelectStep] ProgressManager not found.");
                director.NextStep();
            }
        }
    }
}
