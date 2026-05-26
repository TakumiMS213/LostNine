using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using DG.Tweening;

/// <summary>
/// カメラ状のスプライトオブジェクトにアタッチするD&Dスクリプト。
/// ProgressManager.AllKeywordsCollected が true のときのみ有効。
/// Portraitの RectTransform の範囲内にドロップすると調律シーンへ遷移する。
/// </summary>
[RequireComponent(typeof(RectTransform))]
public class DragToSceneItem : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler, IPointerEnterHandler, IPointerExitHandler
{
    [Header("ドロップ先")]
    [Tooltip("ドロップ判定を行うPortraitの RectTransform")]
    [SerializeField] private RectTransform portraitDropTarget;

    [Header("遷移先")]
    [Tooltip("調律シーン名")]
    [SerializeField] private string tuningSceneName = "Memorize";

    [Header("無効時の演出")]
    [Tooltip("キーワード未達成時にドラッグしようとした場合の揺れ演出（強さ）")]
    [SerializeField] private float rejectShakeStrength = 5f;

    [Tooltip("揺れの持続時間")]
    [SerializeField] private float rejectShakeDuration = 0.3f;

    [Header("ホバー演出")]
    [SerializeField] private float hoverScaleMultiplier = 1.1f;

    // ── プライベート ──────────────────────────────────────────────
    private RectTransform _rect;
    private Canvas _rootCanvas;
    private Vector2 _originalPosition;
    private bool _isDragging = false;
    private bool _wasKeywordsCollected = false;

    private void Awake()
    {
        _rect = GetComponent<RectTransform>();
        _rootCanvas = GetComponentInParent<Canvas>();
        if (_rootCanvas != null && !_rootCanvas.isRootCanvas)
            _rootCanvas = _rootCanvas.rootCanvas;
    }

    private void Start()
    {
        _originalPosition = _rect.anchoredPosition;
        RefreshActivationState();
    }

    private void OnEnable()
    {
        if (ProgressManager.Instance != null)
            ProgressManager.Instance.OnKeywordThresholdReached += RefreshActivationState;
    }

    private void OnDisable()
    {
        if (ProgressManager.Instance != null)
            ProgressManager.Instance.OnKeywordThresholdReached -= RefreshActivationState;
    }

    private void RefreshActivationState()
    {
        bool collected = ProgressManager.Instance != null && ProgressManager.Instance.AllKeywordsCollected;
        if (collected && !_wasKeywordsCollected)
            PlayActivationEffect();
        _wasKeywordsCollected = collected;
    }

    private void PlayActivationEffect()
    {
        _rect.DOKill();
        var seq = DOTween.Sequence().SetLink(gameObject);
        seq.Append(_rect.DOScale(1.3f, 0.15f).SetEase(Ease.OutQuad));
        seq.Append(_rect.DOScale(1.0f, 0.2f).SetEase(Ease.InOutBounce));
        seq.SetLoops(3, LoopType.Restart);
    }

    // ── Hover ─────────────────────────────────────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!IsKeywordsCollected()) return;
        _rect.DOKill();
        _rect.DOScale(hoverScaleMultiplier, 0.12f).SetEase(Ease.OutBack).SetLink(gameObject);
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (_isDragging) return;
        _rect.DOKill();
        _rect.DOScale(1f, 0.12f).SetEase(Ease.OutQuad).SetLink(gameObject);
    }

    // ── Drag ──────────────────────────────────────────────────────

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (!IsKeywordsCollected())
        {
            PlayRejectEffect();
            return;
        }
        _isDragging = true;
        _originalPosition = _rect.anchoredPosition;
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;
        float scaleFactor = _rootCanvas != null ? _rootCanvas.scaleFactor : 1f;
        _rect.anchoredPosition += eventData.delta / scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (!_isDragging) return;
        _isDragging = false;

        // ── FIX: RectTransformUtility でPortraitのローカル座標に変換して判定 ──
        if (IsDroppedOnPortrait(eventData))
        {
            OnSuccessfulDrop();
        }
        else
        {
            _rect.DOAnchorPos(_originalPosition, 0.3f).SetEase(Ease.OutBack).SetLink(gameObject);
            _rect.DOScale(1f, 0.12f).SetEase(Ease.OutQuad).SetLink(gameObject);
        }
    }

    // ── 成功処理 ──────────────────────────────────────────────────

    private void OnSuccessfulDrop()
    {
        Debug.Log("[DragToSceneItem] Drop on Portrait. Transitioning to Tuning scene.");

        if (ProgressManager.Instance != null)
        {
            int chapter = ProgressManager.Instance.CurrentChapter;
            ProgressManager.Instance.SetProgress(chapter, GamePhase.Tuning);
        }

        if (SceneTransition.Instance != null)
            SceneTransition.Instance.TransitionTo(tuningSceneName);
        else
        {
            Debug.LogWarning("[DragToSceneItem] SceneTransition not found. Loading directly.");
            UnityEngine.SceneManagement.SceneManager.LoadScene(tuningSceneName);
        }
    }

    // ── 判定ヘルパー ──────────────────────────────────────────────

    private bool IsKeywordsCollected()
        => ProgressManager.Instance != null && ProgressManager.Instance.AllKeywordsCollected;

    /// <summary>
    /// ドロップ位置がPortraitのRect内かどうかを、スクリーン座標→PortraitローカルUI座標に変換して判定する。
    /// anchoredPositionを直接比較するより確実（親Canvasが異なる場合も安全）。
    /// </summary>
    private bool IsDroppedOnPortrait(PointerEventData eventData)
    {
        if (portraitDropTarget == null) return false;

        // eventData.position はスクリーン座標
        Camera cam = eventData.pressEventCamera;
        bool hit = RectTransformUtility.ScreenPointToLocalPointInRectangle(
            portraitDropTarget,
            eventData.position,
            cam,
            out Vector2 localPoint);

        if (!hit) return false;
        return portraitDropTarget.rect.Contains(localPoint);
    }

    private void PlayRejectEffect()
    {
        _rect.DOKill();
        _rect.DOShakeAnchorPos(rejectShakeDuration, rejectShakeStrength, 20, 90f, false)
             .SetLink(gameObject);
    }
}
