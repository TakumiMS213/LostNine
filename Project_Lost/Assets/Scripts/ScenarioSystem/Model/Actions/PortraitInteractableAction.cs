using UnityEngine;

namespace ScenarioSystem.Model.Actions
{
    /// <summary>
    /// ポートレート（キャラクター）のクリック可能/不可状態を切り替えるアクション。
    /// ComuStartandEndManager.SetPortraitInteractable() を呼び出す。
    /// </summary>
    [CreateAssetMenu(fileName = "PortraitInteractableAction", menuName = "Scenario/Actions/Portrait Interactable Action")]
    public class PortraitInteractableAction : ScenarioAction
    {
        public override string ActionType => "PortraitInteractable";

        [Tooltip("true: クリック可能にする / false: クリック不可にする")]
        public bool isInteractable = true;

        [Tooltip("true: クリック不可時にバツ印等のオーバーレイUIの表示状態も更新する")]
        public bool updateOverlay = true;
    }
}
