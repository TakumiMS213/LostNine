using System;
using System.Collections.Generic;
using UnityEngine;

namespace ScenarioSystem.Model.Actions
{
    /// <summary>
    /// 1行のセリフごとのデータを保持する構造体。
    /// </summary>
    [Serializable]
    public struct DialogueEntry
    {
        [Tooltip("話者名")]
        public string speakerName;

        [TextArea(3, 10)]
        [Tooltip("セリフテキスト。TMPタグ対応。")]
        public string text;

        [Tooltip("表示するポートレート画像")]
        public Sprite portrait;

        [Tooltip("ポートレートの表示位置")]
        public PortraitPosition portraitPosition;

        [Tooltip("タイピング速度（秒/文字）。0 = デフォルト使用")]
        public float typingSpeed;

        [Tooltip("話者名のスライド方向")]
        public NameSlideDirection nameSlideDirection;

        [Tooltip("ボイスクリップ")]
        public AudioClip voiceClip;

        [Tooltip("背景スチル画像（CG等）")]
        public Sprite backgroundImage;
    }

    /// <summary>
    /// テキスト表示アクション。
    /// 複数のセリフ（DialogueEntry）を1つのアクションで連続表示可能。
    /// </summary>
    [CreateAssetMenu(fileName = "DialogueAction", menuName = "Scenario/Actions/Dialogue")]
    public class DialogueAction : ScenarioAction, IMultiStepAction
    {
        public override string ActionType => "Dialogue";

        [Tooltip("セリフのリスト")]
        public List<DialogueEntry> entries = new();

        public int StepCount => entries.Count;
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
