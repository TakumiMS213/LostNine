using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;
using Main.UIMoves;

/// <summary>
/// フェードイン・アウト付きシーン遷移ユーティリティ。
/// DontDestroyOnLoad で保持される。
/// フェード演出はシーン内の MultiEasing（ラベル "FadeOut" / "FadeIn"）を検索して実行する。
/// MultiEasing が見つからない場合はフォールバックとして直接 DOFade を使用する。
/// </summary>
public class SceneTransition : MonoBehaviour
{
    public static SceneTransition Instance { get; private set; }

    [Header("Fade Settings")]
    [SerializeField] private Image fadeOverlay;
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private Color fadeColor = Color.black;

    [Header("MultiEasing Labels")]
    [Tooltip("フェードアウト用 MultiEasing のラベル")]
    [SerializeField] private string fadeOutLabel = "FadeOut";
    [Tooltip("フェードイン用 MultiEasing のラベル")]
    [SerializeField] private string fadeInLabel = "FadeIn";

    private bool _isTransitioning;
    private bool _useSimpleFade;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            EnsureFadeOverlay();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// fadeOverlay が未設定または参照切れの場合に、
    /// DontDestroyOnLoad 配下に Canvas + Image を自動生成する。
    /// </summary>
    private void EnsureFadeOverlay()
    {
        if (fadeOverlay != null)
        {
            // 既に有効な参照がある場合は初期化のみ
            fadeOverlay.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
            fadeOverlay.raycastTarget = false;
            return;
        }

        // Canvas を生成
        var canvasObj = new GameObject("FadeOverlayCanvas");
        canvasObj.transform.SetParent(transform);

        var canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999; // 最前面に表示

        var scaler = canvasObj.AddComponent<UnityEngine.UI.CanvasScaler>();
        scaler.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        canvasObj.AddComponent<UnityEngine.UI.GraphicRaycaster>();

        // Image を生成
        var imageObj = new GameObject("FadeOverlay");
        imageObj.transform.SetParent(canvasObj.transform, false);

        var rect = imageObj.AddComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;

        fadeOverlay = imageObj.AddComponent<Image>();
        fadeOverlay.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
        fadeOverlay.raycastTarget = false;

        Debug.Log("[SceneTransition] FadeOverlay を自動生成しました。");
    }

    /// <summary>
    /// フェードアウト → シーンロード → フェードイン
    /// </summary>
    public void TransitionTo(string sceneName)
    {
        if (_isTransitioning) return;
        _useSimpleFade = false;
        StartCoroutine(TransitionCoroutine(sceneName));
    }

    /// <summary>
    /// フェードアウト → シーンロード → フェードイン (コールバック付き)
    /// </summary>
    public void TransitionTo(string sceneName, System.Action onSceneLoaded)
    {
        if (_isTransitioning) return;
        _useSimpleFade = false;
        StartCoroutine(TransitionCoroutine(sceneName, onSceneLoaded));
    }

    /// <summary>
    /// シンプルな黒フェードでシーン遷移する。
    /// MultiEasing（FilmCanvas等）を使わず、常に黒画面フェードを使用する。
    /// </summary>
    public void TransitionToSimple(string sceneName, System.Action onSceneLoaded = null)
    {
        if (_isTransitioning) return;
        _useSimpleFade = true;
        StartCoroutine(TransitionCoroutine(sceneName, onSceneLoaded));
    }

    /// <summary>
    /// フェードインのみ実行（シーン開始時に外部から呼ぶ場合）
    /// </summary>
    public void FadeIn()
    {
        var fadeInEasing = MultiEasing.FindByLabel(fadeInLabel);
        if (fadeInEasing != null)
        {
            if (fadeOverlay != null) fadeOverlay.raycastTarget = true;
            fadeInEasing.Play();
            StartCoroutine(WaitAndDisableRaycast(fadeInEasing));
        }
        else
        {
            // フォールバック: 直接 DOFade
            FadeInFallback();
        }
    }

    private IEnumerator WaitAndDisableRaycast(MultiEasing easing)
    {
        yield return new WaitWhile(() => easing != null && easing.IsPlaying);
        if (fadeOverlay != null) fadeOverlay.raycastTarget = false;
    }

    private IEnumerator TransitionCoroutine(string sceneName, System.Action onSceneLoaded = null)
    {
        _isTransitioning = true;

        // 遷移直前に fadeOverlay が消失していないか確認・再生成
        EnsureFadeOverlay();

        // フェードアウト

        if (!_useSimpleFade)
        {
            var fadeOutEasing = MultiEasing.FindByLabel(fadeOutLabel);
            if (fadeOutEasing != null)
            {
                if (fadeOverlay != null) fadeOverlay.raycastTarget = true;
                fadeOutEasing.Play();
                yield return new WaitWhile(() => fadeOutEasing != null && fadeOutEasing.IsPlaying);
            }
            else
            {
                yield return FadeOutFallbackCoroutine();
            }
        }
        else
        {
            yield return FadeOutFallbackCoroutine();
        }

        // シーンロード
        var asyncOp = SceneManager.LoadSceneAsync(sceneName);
        while (asyncOp != null && !asyncOp.isDone)
            yield return null;

        onSceneLoaded?.Invoke();

        // 1フレーム待って新シーンの初期化を待つ
        yield return null;

        // フェードイン
        if (!_useSimpleFade)
        {
            var fadeInEasing = MultiEasing.FindByLabel(fadeInLabel);
            if (fadeInEasing != null)
            {
                // Fallback用フェード画像が残っている場合は、MultiEasingと被らないよう即座に隠す
                if (fadeOverlay != null)
                {
                    fadeOverlay.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
                    fadeOverlay.raycastTarget = false;
                    fadeOverlay.gameObject.SetActive(false);
                }

                fadeInEasing.Play();
                yield return new WaitWhile(() => fadeInEasing != null && fadeInEasing.IsPlaying);
            }
            else
            {
                yield return FadeInFallbackCoroutine();
            }
        }
        else
        {
            yield return FadeInFallbackCoroutine();
        }

        _isTransitioning = false;
    }

    #region Fallback (MultiEasing が見つからない場合)

    private void FadeInFallback()
    {
        if (fadeOverlay == null) return;
        fadeOverlay.raycastTarget = true;
        fadeOverlay.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 1f);
        fadeOverlay.DOFade(0f, fadeDuration).OnComplete(() =>
        {
            fadeOverlay.raycastTarget = false;
        });
    }

    private IEnumerator FadeOutFallbackCoroutine()
    {
        if (fadeOverlay == null) yield break;

        fadeOverlay.gameObject.SetActive(true);
        var canvas = fadeOverlay.canvas;
        if (canvas != null) canvas.gameObject.SetActive(true);

        fadeOverlay.raycastTarget = true;
        fadeOverlay.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
        var tween = fadeOverlay.DOFade(1f, fadeDuration).SetUpdate(true);
        yield return tween.WaitForCompletion();
    }

    private IEnumerator FadeInFallbackCoroutine()
    {
        if (fadeOverlay == null) yield break;

        fadeOverlay.gameObject.SetActive(true);
        var canvas = fadeOverlay.canvas;
        if (canvas != null) canvas.gameObject.SetActive(true);

        fadeOverlay.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 1f);
        var tween = fadeOverlay.DOFade(0f, fadeDuration).SetUpdate(true);
        yield return tween.WaitForCompletion();
        fadeOverlay.raycastTarget = false;
    }

    #endregion
}
