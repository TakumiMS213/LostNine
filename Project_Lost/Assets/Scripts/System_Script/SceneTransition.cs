using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;

/// <summary>
/// フェードイン・アウト付きシーン遷移ユーティリティ。
/// DontDestroyOnLoad で保持される。
/// </summary>
public class SceneTransition : MonoBehaviour
{
    public static SceneTransition Instance { get; private set; }

    [Header("Fade Settings")]
    [SerializeField] private Image fadeOverlay;
    [SerializeField] private float fadeDuration = 0.5f;
    [SerializeField] private Color fadeColor = Color.black;

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
        if (fadeOverlay == null) return;
        fadeOverlay.raycastTarget = true;
        fadeOverlay.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 1f);
        fadeOverlay.DOFade(0f, fadeDuration).OnComplete(() =>
        {
            fadeOverlay.raycastTarget = false;
        });
    }

    private IEnumerator TransitionCoroutine(string sceneName, System.Action onSceneLoaded = null)
    {
        _isTransitioning = true;

        // フェードアウト
        if (fadeOverlay != null)
        {
            fadeOverlay.raycastTarget = true;
            fadeOverlay.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 0f);

            var tween = fadeOverlay.DOFade(1f, fadeDuration);
            yield return tween.WaitForCompletion();
        }

        // シーンロード
        var asyncOp = SceneManager.LoadSceneAsync(sceneName);
        while (asyncOp != null && !asyncOp.isDone)
            yield return null;

        onSceneLoaded?.Invoke();

        // 1フレーム待って新シーンの初期化を待つ
        yield return null;

        // フェードイン
        if (fadeOverlay != null)
        {
            fadeOverlay.color = new Color(fadeColor.r, fadeColor.g, fadeColor.b, 1f);
            var tween = fadeOverlay.DOFade(0f, fadeDuration);
            yield return tween.WaitForCompletion();
            fadeOverlay.raycastTarget = false;
        }

        _isTransitioning = false;
    }
}
