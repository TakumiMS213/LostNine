using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using MessageWindowSystem.Core;

/// <summary>
/// 記憶の欠片システム。
/// ProgressManager.OnKeywordThresholdReached を購読し、
/// キーワード取得ごとに欠片を1つずつ生成・演出する。
///
/// 専用の Canvas（sortingOrder が MessageWindow より低い）に生成する想定。
/// </summary>
public class MemoryFragmentSystem : MonoBehaviour
{
    public static MemoryFragmentSystem Instance { get; private set; }

    // ── Inspector 設定 ────────────────────────────────────────────
    [Header("欠片スポーン設定")]
    [Tooltip("欠片を生成する専用Canvas（MWより低いsortingOrder）")]
    [SerializeField] private Canvas fragmentCanvas;

    [Tooltip("欠片プレハブ（MemoryFragmentがアタッチ済み）")]
    [SerializeField] private GameObject fragmentPrefab;

    [Tooltip("欠片に使用するスプライトのパターン（ランダム選択）")]
    [SerializeField] private Sprite[] fragmentSprites;

    [Tooltip("Portraitの RectTransform（Orbit 中心として使用）")]
    [SerializeField] private RectTransform portraitRect;

    [Header("Orbit 角度設定")]
    [Tooltip("欠片それぞれの初期Orbit角度（度）。インデックス0・1・2に対応")]
    [SerializeField] private float[] orbitStartAngles = { 90f, 210f, 330f };

    [Header("暗転オーバーレイ")]
    [Tooltip("キーワード取得時に背景を暗くする Image（全画面半透明黒）")]
    [SerializeField] private Image darkOverlay;

    [Tooltip("暗転の目標アルファ値")]
    [Range(0f, 1f)]
    [SerializeField] private float darkAlpha = 0.5f;

    [Tooltip("暗転にかかる時間（秒）")]
    [SerializeField] private float darkFadeDuration = 0.3f;

    [Tooltip("暗転が明けるまでの保持時間（秒）")]
    [SerializeField] private float darkHoldDuration = 2.0f;

    // ── プライベート ──────────────────────────────────────────────
    private readonly List<MemoryFragment> _fragments = new();
    private int _spawnCount = 0;     // 現在何個生成済みか

    // ── Unity Lifecycle ──────────────────────────────────────────

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }

        // 暗転オーバーレイ初期化
        if (darkOverlay != null)
        {
            var c = darkOverlay.color;
            c.a = 0f;
            darkOverlay.color = c;
            darkOverlay.gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        // シーン再読込時に古い参照が残るのを防止
        if (Instance == this) Instance = null;
    }

    private void OnEnable()
    {
        if (ProgressManager.Instance != null)
            ProgressManager.Instance.OnKeywordThresholdReached += OnKeywordThresholdReached;
    }

    private void OnDisable()
    {
        if (ProgressManager.Instance != null)
            ProgressManager.Instance.OnKeywordThresholdReached -= OnKeywordThresholdReached;
    }

    // ── キーワード取得イベント ────────────────────────────────────

    /// <summary>
    /// ProgressManager から「キーワードしきい値到達」が通知されたとき。
    /// ※ 3回のキーワードで3回呼ばれるのではなく1回（全Keywords達成時）。
    ///   個別追加は AddFragmentForKeyword(id) を直接呼ぶ。
    /// </summary>
    private void OnKeywordThresholdReached()
    {
        // 既に全欠片スポーン済みなら無視
        if (_spawnCount >= (fragmentSprites != null ? 3 : 0)) return;
        SpawnFragment($"Keyword_{_spawnCount}");
    }

    /// <summary>
    /// キーワード1個取得のたびに外部から呼ぶAPI（ClueManagerやKeywordClicked時など）。
    /// </summary>
    public void AddFragmentForKeyword(string keywordId)
    {
        if (_spawnCount >= 3) return;
        SpawnFragment(keywordId);
    }

    // ── 欠片生成 ─────────────────────────────────────────────────

    private void SpawnFragment(string keywordId)
    {
        if (fragmentPrefab == null || fragmentCanvas == null || portraitRect == null)
        {
            Debug.LogWarning("[MemoryFragmentSystem] 設定が不足しています。");
            return;
        }

        // スプライトをランダムに選択
        Sprite sprite = null;
        if (fragmentSprites != null && fragmentSprites.Length > 0)
            sprite = fragmentSprites[Random.Range(0, fragmentSprites.Length)];

        // 専用Canvas上にインスタンス化
        var go = Instantiate(fragmentPrefab, fragmentCanvas.transform);
        var fragment = go.GetComponent<MemoryFragment>();
        if (fragment == null)
        {
            Debug.LogError("[MemoryFragmentSystem] fragmentPrefab に MemoryFragment がありません。");
            Destroy(go);
            return;
        }

        RectTransform fragmentCanvasRect = fragmentCanvas.GetComponent<RectTransform>();

        // Orbit 開始角度（順番に割り当て、余ったらランダム）
        float angle = _spawnCount < orbitStartAngles.Length
            ? orbitStartAngles[_spawnCount]
            : Random.Range(0f, 360f);

        // 暗転演出 → 欠片登場
        StartCoroutine(SpawnWithDarkEffect(fragment, fragmentCanvasRect, sprite, keywordId, angle));
        _spawnCount++;
        _fragments.Add(fragment);
    }

    private IEnumerator SpawnWithDarkEffect(MemoryFragment fragment, RectTransform fragmentCanvasRect, Sprite sprite, string keywordId, float angle)
    {
        // 1. 背景ほんのり暗転
        if (darkOverlay != null)
        {
            darkOverlay.gameObject.SetActive(true);
            darkOverlay.DOFade(darkAlpha, darkFadeDuration).SetLink(gameObject);
        }

        yield return new WaitForSeconds(darkFadeDuration);

        // 2. 欠片の登場演出（内部でOrbitに移行）
        //    ※ PlayDevelopmentEffect はKeywordHandler側で既に呼ばれているためここでは呼ばない
        fragment.Initialize(portraitRect, fragmentCanvasRect, sprite, keywordId, angle, onAppearComplete: () =>
        {
            // 4. 登場後に暗転を戻す
            if (darkOverlay != null)
                darkOverlay.DOFade(0f, darkFadeDuration)
                    .SetLink(gameObject)
                    .OnComplete(() => darkOverlay.gameObject.SetActive(false));
        });

        yield return new WaitForSeconds(darkHoldDuration);
    }

    // ── 全欠片を隠す（Presentationフェーズ移行時） ────────────────

    /// <summary>全欠片をフェードアウトして非表示にする。</summary>
    public void HideAll()
    {
        foreach (var f in _fragments)
        {
            if (f != null) f.Hide();
        }
    }

    /// <summary>欠片をすべて破棄してリセット（次の章開始時など）。</summary>
    public void ClearAll()
    {
        foreach (var f in _fragments)
        {
            if (f != null) Destroy(f.gameObject);
        }
        _fragments.Clear();
        _spawnCount = 0;
    }
}
