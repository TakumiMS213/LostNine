using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;

/// <summary>
/// 記憶の欠片1個を管理する。
/// 生成時に中央登場演出 → PortraitのRectTransformを中心としたOrbitアニメーション。
/// Hover時にラベル表示と拡大演出を行う。
/// </summary>
[RequireComponent(typeof(Image))]
public class MemoryFragment : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // ── 公開設定 ─────────────────────────────────────────
    [Header("Orbit 設定")]
    [Tooltip("Orbit の半径（px）")]
    [SerializeField] private float orbitRadius = 160f;

    [Tooltip("Orbit の角速度（度/秒）")]
    [SerializeField] private float orbitSpeed = 30f;

    [Tooltip("Orbit 開始角度 (度)")]
    [SerializeField] private float startAngle = 0f;

    [Tooltip("Y 軸に対する楕円比率（1=正円, <1=横長楕円）")]
    [Range(0.2f, 1f)]
    [SerializeField] private float orbitYRatio = 0.6f;

    [Header("Hover 設定")]
    [Tooltip("Hover 時の拡大倍率")]
    [SerializeField] private float hoverScale = 1.25f;

    [Tooltip("Hover 時に表示するキーワード名 TMP_Text（子オブジェクト）")]
    [SerializeField] private TMP_Text keywordLabel;

    [Header("登場演出")]
    [Tooltip("中央に表示する時間（秒）")]
    [SerializeField] private float centerHoldDuration = 1.5f;

    [Tooltip("中央登場のスケールアップ時間")]
    [SerializeField] private float centerAppearDuration = 0.4f;

    [Tooltip("Orbit開始への移動時間")]
    [SerializeField] private float moveToOrbitDuration = 0.8f;

    // ── プライベート ──────────────────────────────────────
    private RectTransform _rect;
    private Image _image;
    private Vector2 _orbitCenter;   // Portraitのアンカー座標
    private float _currentAngle;
    private bool _isOrbiting = false;
    private bool _isAppearing = false;
    private bool _isHovering = false;
    private string _keywordId;
    private Vector3 _baseScale;

    // ── 初期化 ───────────────────────────────────────────

    /// <summary>
    /// MemoryFragmentSystemから呼ばれる初期化。
    /// </summary>
    /// <param name="fragmentCanvasRect">この欠片が属するCanvasのRectTransform（座標変換に使用）</param>
    public void Initialize(RectTransform portraitRect, RectTransform fragmentCanvasRect, Sprite sprite, string keywordId, float startAngleDeg, System.Action onAppearComplete = null)
    {
        _rect  = GetComponent<RectTransform>();
        _image = GetComponent<Image>();

        _keywordId    = keywordId;
        _currentAngle = startAngleDeg;
        _baseScale    = Vector3.one;

        // スプライト設定
        if (sprite != null)
        {
            _image.sprite = sprite;
            _image.SetNativeSize();
        }

        // キーワードラベル初期化
        if (keywordLabel != null)
        {
            keywordLabel.text = keywordId;
            keywordLabel.gameObject.SetActive(false);
        }

        // ── FIX: Orbit中心をPortraitのワールド座標 → 欠片Canvasのローカル座標に変換 ──
        // portraitRect.anchoredPositionは親Canvas基準のため、異なるCanvas間では比較不可。
        // ワールド座標を経由してFragmentCanvas上のローカル座標に変換する。
        if (fragmentCanvasRect != null)
        {
            // PortraitのワールドポジションをFragmentCanvasのローカル座標に変換
            Vector3 portraitWorldPos = portraitRect.position;
            Camera cam = fragmentCanvasRect.GetComponentInParent<Canvas>()?.worldCamera;
            if (RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    fragmentCanvasRect,
                    RectTransformUtility.WorldToScreenPoint(cam, portraitWorldPos),
                    cam,
                    out Vector2 localPos))
            {
                _orbitCenter = localPos;
            }
            else
            {
                // フォールバック（変換失敗時）
                _orbitCenter = Vector2.zero;
                Debug.LogWarning("[MemoryFragment] Orbit center conversion failed. Using Vector2.zero.");
            }
        }
        else
        {
            // fragmentCanvasRect未指定時のフォールバック
            _orbitCenter = portraitRect.anchoredPosition;
        }

        // 最初は中央に表示、非表示から始める
        _rect.anchoredPosition = Vector2.zero;
        _rect.localScale       = Vector3.zero;
        _image.color           = new Color(1f, 1f, 1f, 0f);

        PlayAppearSequence(onAppearComplete);
    }

    // ── 登場演出 ─────────────────────────────────────────

    private void PlayAppearSequence(Action onComplete)
    {
        _isAppearing = true;

        var seq = DOTween.Sequence().SetLink(gameObject);

        // 1. 中央でスケールアップ + フェードイン
        seq.Append(_rect.DOScale(1.3f, centerAppearDuration).SetEase(Ease.OutBack));
        seq.Join(_image.DOFade(1f, centerAppearDuration * 0.5f));

        // 2. 中央に一定時間停留
        seq.AppendInterval(centerHoldDuration);

        // 3. 通常サイズに収縮
        seq.Append(_rect.DOScale(1f, 0.2f).SetEase(Ease.InQuad));

        // 4. Orbit開始位置へ移動
        seq.AppendCallback(() =>
        {
            Vector2 targetPos = CalcOrbitPosition(_currentAngle);
            _rect.DOAnchorPos(targetPos, moveToOrbitDuration).SetEase(Ease.InOutQuad)
                .SetLink(gameObject)
                .OnComplete(() =>
                {
                    _isAppearing = false;
                    _isOrbiting  = true;
                    onComplete?.Invoke();
                });
        });
    }

    // ── Update: Orbit ────────────────────────────────────

    private void Update()
    {
        if (!_isOrbiting || _isAppearing || _isHovering) return;

        _currentAngle += orbitSpeed * Time.deltaTime;
        if (_currentAngle >= 360f) _currentAngle -= 360f;

        _rect.anchoredPosition = CalcOrbitPosition(_currentAngle);
    }

    private Vector2 CalcOrbitPosition(float angleDeg)
    {
        float rad = angleDeg * Mathf.Deg2Rad;
        float x   = _orbitCenter.x + Mathf.Cos(rad) * orbitRadius;
        float y   = _orbitCenter.y + Mathf.Sin(rad) * orbitRadius * orbitYRatio;
        return new Vector2(x, y);
    }

    // ── Hover 演出 ───────────────────────────────────────

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (!_isOrbiting) return;
        _isHovering = true;
        _rect.DOScale(hoverScale, 0.15f).SetEase(Ease.OutBack).SetLink(gameObject);
        if (keywordLabel != null)
        {
            keywordLabel.gameObject.SetActive(true);
            keywordLabel.DOFade(1f, 0.1f).SetLink(gameObject);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _isHovering = false;
        _rect.DOScale(1f, 0.15f).SetEase(Ease.OutQuad).SetLink(gameObject);
        if (keywordLabel != null)
            keywordLabel.gameObject.SetActive(false);
    }

    // ── 外部から制御 ─────────────────────────────────────

    /// <summary>Presentationフェーズなどで非表示にする</summary>
    public void Hide()
    {
        _isOrbiting = false;
        var seq = DOTween.Sequence().SetLink(gameObject);
        seq.Append(_image.DOFade(0f, 0.4f));
        seq.Join(_rect.DOScale(0f, 0.4f).SetEase(Ease.InBack));
        seq.OnComplete(() => gameObject.SetActive(false));
    }

    /// <summary>軌道の開始角度を設定（生成時に呼ぶ）</summary>
    public void SetOrbitCenter(Vector2 center) => _orbitCenter = center;
}
