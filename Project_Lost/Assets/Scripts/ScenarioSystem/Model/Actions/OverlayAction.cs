using UnityEngine;

namespace ScenarioSystem.Model.Actions
{
    /// <summary>
    /// オーバーレイ（強制割り込み）メッセージを表示するアクション。
    /// </summary>
    [CreateAssetMenu(fileName = "OverlayAction", menuName = "Scenario/Actions/Overlay")]
    public class OverlayAction : ScenarioAction
    {
        public override string ActionType => "Overlay";

        [TextArea(3, 10)]
        [Tooltip("オーバーレイとして表示するテキスト。")]
        public string text;

        [Tooltip("表示するポートレート画像（任意）")]
        public Sprite portrait;

        [Tooltip("ポートレートの表示位置")]
        public PortraitPosition portraitPosition;

        [Tooltip("自動で閉じるまでの秒数。0以下の場合はユーザーがクリックするまで待機します。")]
        public float displayDuration = 0f;
    }
}
