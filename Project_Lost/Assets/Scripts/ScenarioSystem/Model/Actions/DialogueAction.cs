using UnityEngine;

namespace ScenarioSystem.Model.Actions
{
    /// <summary>
    /// テキスト表示アクション。話者名、セリフ、ポートレート等のデータを保持する。
    /// 旧 DialogueLine に相当。
    /// </summary>
    [CreateAssetMenu(fileName = "DialogueAction", menuName = "Scenario/Actions/Dialogue")]
    public class DialogueAction : ScenarioAction
    {
        public override string ActionType => "Dialogue";

        [Tooltip("話者名")]
        public string speakerName;

        [TextArea(3, 10)]
        [Tooltip("セリフテキスト。TMPタグ対応。")]
        public string text;

        [Tooltip("表示するポートレート画像")]
        public Sprite portrait;

        [Tooltip("ポートレートの表示位置")]
        public PortraitPosition portraitPosition = PortraitPosition.Center;

        [Tooltip("タイピング速度（秒/文字）。0 = デフォルト使用")]
        public float typingSpeed = 0f;

        [Tooltip("話者名のスライド方向")]
        public NameSlideDirection nameSlideDirection = NameSlideDirection.Default;

        [Tooltip("ボイスクリップ")]
        public AudioClip voiceClip;

        [Tooltip("背景スチル画像（CG等）")]
        public Sprite backgroundImage;
    }

    /// <summary>ポートレート表示位置。</summary>
    public enum PortraitPosition
    {
        Center = 0,
        Left,
        Right
    }

    /// <summary>話者名スライド方向。</summary>
    public enum NameSlideDirection
    {
        Default,
        Left,
        Right
    }
}
