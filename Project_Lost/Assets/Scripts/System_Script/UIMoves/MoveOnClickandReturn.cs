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

    private Sequence _animation;
    private bool _animationPending;
    private bool _initialized;

    private void Awake() => EnsureInitialized();

    private void EnsureInitialized()
    {
        if (_initialized) return;
        _rect = GetComponent<RectTransform>();
        if (_rect == null) return;
        _originalAnchoredPosition = _rect.anchoredPosition;
        _originalSizeDelta = _rect.sizeDelta;
        _originalScale = transform.localScale;
        _originalRotation = transform.localEulerAngles;
        _initialized = true;
    }

    public void Play()
    {
        EnsureInitialized();
        if (_rect == null) return;

        StopAnimation();
        isMoved = !isMoved;
        var sequence = MoveWithEasing.MoveToAnchored(
            gameObject,
            isMoved ? targetAnchoredPosition : _originalAnchoredPosition,
            new MoveWithEasing.MoveOptions
            {
                duration = duration,
                ease = ease,
                shakeOnComplete = shakeOnComplete,
                shakeStrength = shakeStrength,
                shakeDuration = shakeDuration,
                endAlpha = endAlpha,
                fadeDuration = fadeDuration
            });

        // 位置と並行して動く変形も同じ所有範囲にまとめる。
        if (enableScale)
            sequence.Insert(0f, transform.DOScale(isMoved ? targetScale : _originalScale, scaleDuration).SetEase(scaleEase));
        if (enableSize)
            sequence.Insert(0f, _rect.DOSizeDelta(isMoved ? targetSizeDelta : _originalSizeDelta, sizeDuration).SetEase(sizeEase));
        if (enableRotation)
            sequence.Insert(0f, transform.DORotate(isMoved ? targetRotation : _originalRotation, rotateDuration).SetEase(rotateEase));

        _animation = sequence;
        _animationPending = true;
        sequence.OnComplete(() => _animationPending = false);
        sequence.OnKill(() =>
        {
            if (_animation == sequence) _animation = null;
        });
    }

    /// <summary>進行中の演出を中止し、指定された最終形状へ即座に確定する。</summary>
    public void SetToTarget() => SetState(true);

    public void SetToOriginal() => SetState(false);

    private void SetState(bool moved)
    {
        EnsureInitialized();
        if (_rect == null) return;

        StopAnimation();
        // 外部の入場アニメーションも同じRectTransformへの書き戻しを止める。
        DOTween.Kill(_rect);
        isMoved = moved;
        _rect.anchoredPosition = moved ? targetAnchoredPosition : _originalAnchoredPosition;
        if (enableScale) transform.localScale = moved ? targetScale : _originalScale;
        if (enableSize) _rect.sizeDelta = moved ? targetSizeDelta : _originalSizeDelta;
        if (enableRotation) transform.localEulerAngles = moved ? targetRotation : _originalRotation;
        if (moved && TryGetComponent<CanvasGroup>(out var canvasGroup))
            canvasGroup.alpha = endAlpha;
    }

    private void StopAnimation()
    {
        _animationPending = false;
        _animation?.Kill();
        _animation = null;
    }

    private void OnDisable()
    {
        // 会話切替では演出中にウィンドウを隠す。再表示時に中途半端な形状を残さない。
        if (_animationPending) SetState(isMoved);
        else StopAnimation();
    }

    private void OnDestroy() => StopAnimation();
}
