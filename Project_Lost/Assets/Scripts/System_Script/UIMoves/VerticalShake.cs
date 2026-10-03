using UnityEngine;
using DG.Tweening;

public class VerticalShake : MonoBehaviour
{
    [SerializeField] private float moveAmount = 10f; // 上下の移動量
    [SerializeField] private float duration = 0.6f;  // 片道にかかる時間
    [SerializeField] private Ease ease = Ease.InOutSine;

    private Vector3 originalPos;
    private Tween _tween;

    private void OnEnable()
    {
        originalPos = transform.localPosition;

        _tween = transform
            .DOLocalMoveY(originalPos.y + moveAmount, duration)
            .SetEase(ease)
            .SetLink(gameObject)
            .SetLoops(-1, LoopType.Yoyo); // 往復
    }

    private void OnDisable()
    {
        _tween?.Kill();
        _tween = null;
        transform.localPosition = originalPos;
    }
}
