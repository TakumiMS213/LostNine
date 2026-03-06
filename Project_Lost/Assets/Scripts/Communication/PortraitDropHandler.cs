using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// Portrait (立ち絵) へのドロップを受け付けるハンドラ。
/// 
/// - Extractionフェーズかつ全キーワード取得済み: Memorizer D&D → Tuning(Memorize)シーンへ遷移
/// - それ以外: 通常のPortraitクリック処理（ToggleComuforPortrait）
/// </summary>
public class PortraitDropHandler : MonoBehaviour, IDropHandler
{
    [Tooltip("Tuning用シーンの名前")]
    [SerializeField] private string tuningSceneName = "Memorize";

    public void OnDrop(PointerEventData eventData)
    {
        if (eventData.pointerDrag == null) return;

        // ドロップされたオブジェクトが MemorizerDraggable を持っているか確認
        var draggable = eventData.pointerDrag.GetComponent<MemorizerDraggable>();
        if (draggable == null) return;

        var pm = ProgressManager.Instance;
        if (pm == null)
        {
            Debug.LogWarning("[PortraitDropHandler] ProgressManager not found.");
            return;
        }

        // Extractionフェーズかつ全キーワード取得済みの場合のみTuningへ遷移
        if (pm.CurrentPhase == GamePhase.Extraction && pm.AllKeywordsCollected)
        {
            Debug.Log($"[PortraitDropHandler] All keywords collected. Transitioning to Tuning scene: {tuningSceneName}");

            // フェーズをTuningに更新
            pm.SetProgress(pm.CurrentChapter, GamePhase.Tuning);

            // フェード付きでシーン遷移
            if (SceneTransition.Instance != null)
                SceneTransition.Instance.TransitionTo(tuningSceneName);
            else
                UnityEngine.SceneManagement.SceneManager.LoadScene(tuningSceneName);
        }
        else if (pm.CurrentPhase == GamePhase.Extraction && !pm.AllKeywordsCollected)
        {
            Debug.Log($"[PortraitDropHandler] Keywords not complete ({pm.CurrentKeywordProgress}/{pm.KeywordThreshold}). Drop rejected.");
            // キーワードが足りない場合は何もしない（MemorizerDraggableが元の位置に戻る）
        }
        else
        {
            Debug.Log("[PortraitDropHandler] Not in Extraction phase. Drop ignored.");
        }
    }
}
