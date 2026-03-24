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

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 初期状態: 完全に透明
            if (fadeOverlay != null)
            {
                fadeOverlay.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
                fadeOverlay.raycastTarget = false;
            }
        }
        else
        {
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// フェードアウト → シーンロード → フェードイン
    /// </summary>
    public void TransitionTo(string sceneName)
    {
        if (_isTransitioning) return;
        StartCoroutine(TransitionCoroutine(sceneName));
    }

    /// <summary>
    /// フェードアウト → シーンロード → フェードイン (コールバック付き)
    /// </summary>
    public void TransitionTo(string sceneName, System.Action onSceneLoaded)
    {
        if (_isTransitioning) return;
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

        // フェードアウト（MultiEasing 経由）
        var fadeOutEasing = MultiEasing.FindByLabel(fadeOutLabel);
        if (fadeOutEasing != null)
        {
            if (fadeOverlay != null) fadeOverlay.raycastTarget = true;
            fadeOutEasing.Play();
            yield return new WaitWhile(() => fadeOutEasing != null && fadeOutEasing.IsPlaying);
        }
        else
        {
            // フォールバック: 直接 DOFade
            yield return FadeOutFallbackCoroutine();
        }

        // シーンロード
        var asyncOp = SceneManager.LoadSceneAsync(sceneName);
        while (asyncOp != null && !asyncOp.isDone)
            yield return null;

        onSceneLoaded?.Invoke();

        // 1フレーム待って新シーンの初期化を待つ
        yield return null;

        // フェードイン（新シーン内の MultiEasing を検索）
        var fadeInEasing = MultiEasing.FindByLabel(fadeInLabel);
        if (fadeInEasing != null)
        {
            fadeInEasing.Play();
            yield return new WaitWhile(() => fadeInEasing != null && fadeInEasing.IsPlaying);
            if (fadeOverlay != null) fadeOverlay.raycastTarget = false;
        }
        else
        {
            // フォールバック: 直接 DOFade
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
        fadeOverlay.raycastTarget = true;
        fadeOverlay.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);
        var tween = fadeOverlay.DOFade(1f, fadeDuration);
        yield return tween.WaitForCompletion();
    }

    private IEnumerator FadeInFallbackCoroutine()
    {
        if (fadeOverlay == null) yield break;
        fadeOverlay.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 1f);
        var tween = fadeOverlay.DOFade(0f, fadeDuration);
        yield return tween.WaitForCompletion();
        fadeOverlay.raycastTarget = false;
    }

    #endregion
}
