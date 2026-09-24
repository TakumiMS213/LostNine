using UnityEngine;
using Main.UIMoves;

namespace System_Script.Flow
{
    /// <summary>
    /// シーン内の MultiEasing（"FadeIn" ラベル）を使ってフェードイン演出を実行するフローステップ。
    /// SceneTransition.FadeIn() 経由で実行される。
    /// フェードイン完了後に次のステップに進む。
    /// </summary>
    [CreateAssetMenu(fileName = "FadeInStep", menuName = "Flow/Steps/Fade In Step")]
    public class FadeInStep : FlowStep
    {
        public override async void Execute(GameFlowDirector director)
        {
            if (SceneTransition.Instance != null)
            {
                SceneTransition.Instance.FadeIn();
                Debug.Log("[FadeInStep] FadeIn started via SceneTransition.");

                // MultiEasing の FadeIn が完了するまで待機
                var fadeInEasing = MultiEasing.FindByLabel("FadeIn");
                if (fadeInEasing != null)
                {
                    while (fadeInEasing != null && fadeInEasing.IsPlaying)
                    {
                        await Cysharp.Threading.Tasks.UniTask.Yield();
                    }
                    Debug.Log("[FadeInStep] FadeIn completed.");
                }
            }
            else
            {
                Debug.LogWarning("[FadeInStep] SceneTransition not found.");
            }

            director.NextStep();
        }
    }
}
