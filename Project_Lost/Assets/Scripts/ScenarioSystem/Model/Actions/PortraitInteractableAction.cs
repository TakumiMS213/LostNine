using UnityEngine;

namespace ScenarioSystem.Model.Actions
{
    /// <summary>
    /// 対話ボタンの操作許可を切り替える。旧Portraitクリック許可のアセットと互換。
    /// ComuStartandEndManager.SetPortraitInteractable() を呼び出す。
    /// </summary>
    [CreateAssetMenu(fileName = "PortraitInteractableAction", menuName = "Scenario/Actions/Portrait Interactable Action")]
    public class PortraitInteractableAction : ScenarioAction
    {
        public override string ActionType => "PortraitInteractable";

        [Tooltip("true: 対話ボタンの操作を許可する / false: 操作を禁止する。表示指定はDialogueStartButtonActionで行う。")]
        public bool isInteractable = true;

        [Tooltip("true: クリック不可時にバツ印等のオーバーレイUIの表示状態も更新する")]
        public bool updateOverlay = true;
    }
}
