using UnityEngine;
using Teichaku.Core;

namespace System_Script.Flow
{
    /// <summary>
    /// 定着ミニゲーム（一筆書きパズル）を実行するフローステップ。
    /// TeichakuManager のクリアイベントで次のステップに進む。
    /// </summary>
    [CreateAssetMenu(fileName = "TeichakuStep", menuName = "Flow/Steps/Teichaku Step")]
    public class TeichakuStep : FlowStep
    {
        public override async void Execute(GameFlowDirector director)
        {
            var manager = FindFirstObjectByType<TeichakuManager>();
            if (manager != null)
            {
                bool cleared = false;

                // クリアイベントにリスナーを登録
                System.Action onClear = null;
                onClear = () =>
                {
                    manager.OnTeichakuClear -= onClear;
                    cleared = true;
                };
                manager.OnTeichakuClear += onClear;

                // ゲームを有効化
                manager.SetActive(true);

                // クリアまで待機
                while (!cleared)
                {
                    await Cysharp.Threading.Tasks.UniTask.Yield();
                }

                director.NextStep();
            }
            else
            {
                Debug.LogWarning("[TeichakuStep] TeichakuManager not found in scene.");
                director.NextStep();
            }
        }
    }
}
