using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Portrait (立ち絵) へのドロップを受け付けるハンドラ。
/// Memorizerがドロップされた時にシークエンスを開始する。
/// </summary>
public class PortraitDropHandler : MonoBehaviour, IDropHandler
{
    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return;

        // ドロップされたオブジェクトが MemorizerDraggable を持っているか確認
        var draggable = eventData.pointerDrag.GetComponent<MemorizerDraggable>();
        if (draggable != null)
        {
            Debug.Log("[PortraitDropHandler] Memorizer dropped on Portrait.");
            
            // ComuStartandEndManager を探して ToggleComuforPortrait を呼ぶ
            var manager = FindObjectOfType<ComuStartandEndManager>();
            if (manager != null)
            {
                manager.ToggleComuforPortrait();
            }
            else
            {
                Debug.LogWarning("[PortraitDropHandler] ComuStartandEndManager not found via FindObjectOfType.");
                
                // フォールバック: GameFlowDirector を直接呼ぶ
                // しかしシークエンスIDの解決が必要なので、本来は Manager 経由が良い
                // ここではログを出すだけにする
            }
        }
    }
}
