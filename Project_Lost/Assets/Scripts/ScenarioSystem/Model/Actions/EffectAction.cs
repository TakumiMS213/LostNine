using UnityEngine;

namespace ScenarioSystem.Model.Actions
{
    /// <summary>
    /// 演出アクション。画面効果、SE/BGM 再生などを定義する。
    /// 旧 ScreenEffectData + EffectType enum の switch 文を置き換える。
    /// 将来的にサブクラス化して個別の演出SOに分離することも可能。
    /// </summary>
    [CreateAssetMenu(fileName = "EffectAction", menuName = "Scenario/Actions/Effect")]
    public class EffectAction : ScenarioAction
    {
        public override string ActionType => "Effect";

        [Tooltip("実行する演出の種類")]
        public ScenarioEffectType effectType = ScenarioEffectType.None;

        [Tooltip("演出の持続時間や強度")]
        public float floatParam;

        [Tooltip("SE/BGM のクリップ名やその他文字列パラメータ")]
        public string stringParam;

        [Tooltip("表示するスプライト（ShowImage用）")]
        public Sprite spriteParam;

        [Tooltip("フラッシュ/フェードの色")]
        public Color colorParam = Color.white;
    }

    /// <summary>
    /// 演出の種類。旧 EffectType の再定義。
    /// </summary>
    public enum ScenarioEffectType
    {
        None = 0,
        Shake,
        Flash,
        FadeIn,
        FadeOut,
        PlaySE,
        PlayBGM,
        StopBGM,
        ShowImage,
        HideImage
    }
}
