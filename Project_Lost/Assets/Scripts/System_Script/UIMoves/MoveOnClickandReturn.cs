using UnityEngine;
using Main.UIMoves;
using DG.Tweening;

public class MoveOnClickandReturn : MonoBehaviour //MoveWithEasingをクリックで実行
{
    [Header("移動先")]
    [SerializeField] private Vector2 targetAnchoredPosition;

    private RectTransform _rect;
    private Vector2 _originalAnchoredPosition;
    private Vector3 _originalScale;
    private Vector2 _originalSizeDelta;

    [Header("アニメオプション")]
    [SerializeField] private float duration = 0.6f;
    [SerializeField] private DG.Tweening.Ease ease = DG.Tweening.Ease.OutBack;

    [SerializeField] private bool shakeOnComplete = false;
    [SerializeField] private float shakeStrength = 10f;
    [SerializeField] private float shakeDuration = 0.25f;

    [SerializeField] private float endAlpha = 1f;
    [SerializeField] private float fadeDuration = 0.2f;
    public bool isMoved = false;

    [Space]
    [Header("Scale Settings")]
    [SerializeField] private bool enableScale = false;
    [SerializeField] private Vector3 targetScale = Vector3.one;
    [SerializeField] private float scaleDuration = 0.6f;
    [SerializeField] private DG.Tweening.Ease scaleEase = DG.Tweening.Ease.OutBack;

    [Space]
    [Header("Size Settings")]
    [SerializeField] private bool enableSize = false;
    [SerializeField] private Vector2 targetSizeDelta = Vector2.zero;
    [SerializeField] private float sizeDuration = 0.6f;
    [SerializeField] private DG.Tweening.Ease sizeEase = DG.Tweening.Ease.OutBack;

    [Space]
    [Header("Rotation Settings")]
    [SerializeField] private bool enableRotation = false;
    [SerializeField] private Vector3 targetRotation = Vector3.zero;
    [SerializeField] private float rotateDuration = 0.6f;
    [SerializeField] private DG.Tweening.Ease rotateEase = DG.Tweening.Ease.OutBack;

    private Vector3 _originalRotation;

    void Awake()
    {
        _rect = GetComponent<RectTransform>();
        if (_rect != null)
        {
            _originalAnchoredPosition = _rect.anchoredPosition;
            _originalSizeDelta = _rect.sizeDelta;
        }
        _originalScale = transform.localScale;
        _originalRotation = transform.localEulerAngles;
    }

    public void Play()
    {
        // Toggle behavior: 未移動 -> 指定位置へ移動, 移動済み -> 初期位置へ戻す
        if (_rect == null) _rect = GetComponent<RectTransform>();
        if (_rect == null) return;

        if (!isMoved)
        {
            Debug.Log("MoveOnClick Play() -> Move to target");
            MoveWithEasing.MoveToAnchored(
                gameObject,
                targetAnchoredPosition,
                new MoveWithEasing.MoveOptions
                {
                    duration = duration,
                    ease = ease,
                    shakeOnComplete = shakeOnComplete,
                    shakeStrength = shakeStrength,
                    shakeDuration = shakeDuration,
                    endAlpha = endAlpha,
                    fadeDuration = fadeDuration
                }
            );
            if (enableScale)
            {
                transform.DOScale(targetScale, scaleDuration).SetEase(scaleEase);
            }
            if (enableSize)
            {
                _rect.DOSizeDelta(targetSizeDelta, sizeDuration).SetEase(sizeEase);
            }
            if (enableRotation)
            {
                transform.DORotate(targetRotation, rotateDuration).SetEase(rotateEase);
            }
            isMoved = true;
        }
        else
        {
            Debug.Log("MoveOnClick Play() -> Return to original");
            MoveWithEasing.MoveToAnchored(
                gameObject,
                _originalAnchoredPosition,
                new MoveWithEasing.MoveOptions
                {
                    duration = duration,
                    ease = ease,
                    shakeOnComplete = shakeOnComplete,
                    shakeStrength = shakeStrength,
                    shakeDuration = shakeDuration,
                    endAlpha = endAlpha,
                    fadeDuration = fadeDuration
                }
            );
            if (enableScale)
            {
                transform.DOScale(_originalScale, scaleDuration).SetEase(scaleEase);
            }
            if (enableSize)
            {
                _rect.DOSizeDelta(_originalSizeDelta, sizeDuration).SetEase(sizeEase);
            }
            if (enableRotation)
            {
                transform.DORotate(_originalRotation, rotateDuration).SetEase(rotateEase);
            }
            isMoved = false;
        }
    }

    /// <summary>
    /// アニメーションなしで即座にターゲット位置・スケール・サイズ・回転に設定する。
    /// Play() のアニメーションを0秒で適用した場合と同じ最終状態になる。
    /// </summary>
    public void SetToTarget()
    {
        if (_rect == null) _rect = GetComponent<RectTransform>();
        if (_rect == null) return;

        DOTween.Kill(_rect);
        DOTween.Kill(transform);

        _rect.anchoredPosition = targetAnchoredPosition;
        if (enableScale) transform.localScale = targetScale;
        if (enableSize) _rect.sizeDelta = targetSizeDelta;
        if (enableRotation) transform.localEulerAngles = targetRotation;

        // alpha を合わせる
        var cg = GetComponent<CanvasGroup>();
        if (cg != null) cg.alpha = endAlpha;

        isMoved = true;
    }

    /// <summary>
    /// アニメーションなしで即座に元の位置・スケール・サイズ・回転に戻す。
    /// </summary>
    public void SetToOriginal()
    {
        if (_rect == null) _rect = GetComponent<RectTransform>();
        if (_rect == null) return;

        DOTween.Kill(_rect);
        DOTween.Kill(transform);

        _rect.anchoredPosition = _originalAnchoredPosition;
        if (enableScale) transform.localScale = _originalScale;
        if (enableSize) _rect.sizeDelta = _originalSizeDelta;
        if (enableRotation) transform.localEulerAngles = _originalRotation;

        isMoved = false;
    }
}

