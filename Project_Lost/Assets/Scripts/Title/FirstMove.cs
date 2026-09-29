using UnityEngine;
using DG.Tweening;

public class FirstMove : MonoBehaviour
{
    private RectTransform rect;
    private Vector2 originalPos;
    public Vector2 OriginalPos => originalPos;

    [SerializeField] float duration = 0.4f;
    [SerializeField] float offsetY = -300f; // 下からどれだけオフセットして出すか
    [SerializeField] Ease ease = Ease.OutBack; // 動きのタイプ

    void Awake()
    {
        rect = GetComponent<RectTransform>();
        originalPos = rect.anchoredPosition;
    }

    void OnEnable()
    {
        Play();
    }

    public void Play()
    {
        if (rect == null) rect = GetComponent<RectTransform>();
        if (originalPos == Vector2.zero && rect != null) originalPos = rect.anchoredPosition; // Auto-init if needed

        rect.DOKill();
        rect.anchoredPosition = originalPos + new Vector2(0, offsetY); // 画面下へずらす
        rect.DOAnchorPos(originalPos, duration).SetEase(ease).SetLink(gameObject);
    }

    /// <summary>
    /// レイアウト変更後の登場先を更新する。
    /// </summary>
    public void SetDestination(Vector2 destination, bool replay)
    {
        if (rect == null) rect = GetComponent<RectTransform>();
        if (rect == null) return;

        rect.DOKill();
        originalPos = destination;

        if (replay && isActiveAndEnabled)
            Play();
        else
            rect.anchoredPosition = originalPos;
    }

    private void OnDisable()
    {
        if (rect != null)
            rect.DOKill();
    }
}
