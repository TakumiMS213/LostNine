using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Memorizer (記憶庫アイコン) をドラッグ可能にするコンポーネント。
/// </summary>
[RequireComponent(typeof(CanvasGroup))]
public class MemorizerDraggable : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private RectTransform _rectTransform;
    private CanvasGroup _canvasGroup;
    private Vector2 _originalPosition;
    private Transform _originalParent;
    private Canvas _canvas;

    private void Awake()
    {
        _rectTransform = GetComponent<RectTransform>();
        _canvasGroup = GetComponent<CanvasGroup>();
        _canvas = GetComponentInParent<Canvas>();
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        _originalPosition = _rectTransform.anchoredPosition;
        _originalParent = _rectTransform.parent;

        // ドロップ検知のためレイキャストを無効化
        _canvasGroup.blocksRaycasts = false;

        // 最前面に表示するために一時的に親を変更（必要なら）
        // _rectTransform.SetParent(_canvas.transform, true);
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (_canvas == null) return;
        
        // マウス位置に追従
        _rectTransform.anchoredPosition += eventData.delta / _canvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        _canvasGroup.blocksRaycasts = true;

        // ドロップが成功したかどうかは DropHandler 側で処理される
        // ここではドロップされなかった場合のみ元の位置に戻す
        // DropHandler で成功フラグを立てるか、あるいは単に常に戻すか
        
        // 常に元の位置に戻す（成功してもアイコン自体は元の場所に戻るのが一般的）
        _rectTransform.anchoredPosition = _originalPosition;
        // _rectTransform.SetParent(_originalParent, true);
    }
}
