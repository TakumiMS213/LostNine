using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.SceneManagement;

namespace Teichaku.Core
{
    /// <summary>
    /// 定着ミニゲームの演出処理。
    /// 失敗時：画面シェイク
    /// クリア時：画面フラッシュ
    /// </summary>
    public class TeichakuFeedback : MonoBehaviour
    {
        [Header("グリッドコンテナ（シェイク対象）")]
        [Tooltip("タイルが配置されている親RectTransform（シェイク演出で揺らす）")]
        [SerializeField] private RectTransform gridContainer;

        [Header("フラッシュ演出")]
        [Tooltip("フラッシュ演出用オーバーレイ画像（白い全画面Image）")]
        [SerializeField] private Image flashOverlay;

        [Tooltip("フラッシュの最大アルファ値")]
        [SerializeField] private float flashMaxAlpha = 0.8f;

        [Tooltip("フラッシュのフェードイン時間")]
        [SerializeField] private float flashFadeInDuration = 0.05f;

        [Tooltip("フラッシュのフェードアウト時間")]
        [SerializeField] private float flashFadeOutDuration = 0.4f;

        [Header("シェイク演出")]
        [Tooltip("シェイクの強さ（ピクセル）")]
        [SerializeField] private float shakeStrength = 15f;

        [Tooltip("シェイクの持続時間")]
        [SerializeField] private float shakeDuration = 0.4f;

        [Tooltip("シェイクの振動回数")]
        [SerializeField] private int shakeVibrato = 20;

        [Header("タイル訪問演出")]
        [Tooltip("タイル訪問時のスケールパンチ強度")]
        [SerializeField] private float tilePunchScale = 0.15f;

        [Tooltip("タイル訪問時のパンチ持続時間")]
        [SerializeField] private float tilePunchDuration = 0.2f;

        [Header("フェード演出")]
        [Tooltip("フェード演出用オーバーレイ（黒い全画面Image）")]
        [SerializeField] private Image fadeOverlay;

        [Header("オーディオ")]
        [Tooltip("SE再生用AudioSource")]
        [SerializeField] private AudioSource seSource;

        [Tooltip("タイルなぞり時のSE")]
        [SerializeField] private AudioClip tileSE;

        [Tooltip("クリア時のSE")]
        [SerializeField] private AudioClip clearSE;

        [Tooltip("失敗時のSE")]
        [SerializeField] private AudioClip failSE;

        [Header("クリア演出画像")]
        [Tooltip("クリア時に表示する演出画像（UI Image）")]
        [SerializeField] private Image clearImage;

        [Tooltip("クリア画像の表示時間（秒）")]
        [SerializeField] private float clearImageDisplayDuration = 2f;

        [Tooltip("クリア画像のフェードイン時間（秒）")]
        [SerializeField] private float clearImageFadeInDuration = 0.3f;

        [Tooltip("クリア画像のフェードアウト時間（秒）")]
        [SerializeField] private float clearImageFadeOutDuration = 0.3f;

        private Vector2 _gridInitialPos;

        private void Awake()
        {
            // フラッシュオーバーレイの初期化
            if (flashOverlay != null)
            {
                var c = flashOverlay.color;
                c.a = 0f;
                flashOverlay.color = c;
            }

            // グリッドの初期位置を記憶
            if (gridContainer != null)
            {
                _gridInitialPos = gridContainer.anchoredPosition;
            }

            // クリア演出画像の初期化（非表示）
            if (clearImage != null)
            {
                var ci = clearImage.color;
                ci.a = 0f;
                clearImage.color = ci;
            }
        }

        /// <summary>
        /// タイルがなぞられた時の演出
        /// </summary>
        public void OnTileVisited(TeichakuTile tile)
        {
            if (tile != null)
            {
                // タイルのスケールパンチ
                RectTransform rt = tile.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.DOKill();
                    rt.localScale = Vector3.one;
                    rt.DOPunchScale(Vector3.one * tilePunchScale, tilePunchDuration, 1, 0f);
                }
            }

            // SE再生
            if (seSource != null && tileSE != null)
            {
                seSource.PlayOneShot(tileSE);
            }
        }

        /// <summary>
        /// クリア時の演出（フラッシュ）
        /// </summary>
        public void OnClear()
        {
            // 画面フラッシュ
            if (flashOverlay != null)
            {
                flashOverlay.DOKill();
                flashOverlay.color = new Color(
                    flashOverlay.color.r,
                    flashOverlay.color.g,
                    flashOverlay.color.b,
                    0f
                );
                flashOverlay
                    .DOFade(flashMaxAlpha, flashFadeInDuration)
                    .OnComplete(() => flashOverlay.DOFade(0f, flashFadeOutDuration));
            }

            // SE再生
            if (seSource != null && clearSE != null)
            {
                seSource.PlayOneShot(clearSE);
            }

            Debug.Log("[TeichakuFeedback] Clear flash played.");

            // クリア演出画像の表示→シーン遷移
            StartCoroutine(ClearSequence());
        }

        /// <summary>
        /// クリア演出シーケンス：画像表示 → シーン遷移
        /// </summary>
        private IEnumerator ClearSequence()
        {
            // クリア演出画像が設定されている場合、表示する
            if (clearImage != null)
            {
                // フェードイン
                clearImage.DOFade(1f, clearImageFadeInDuration);
                yield return new WaitForSeconds(clearImageFadeInDuration);

                // 一定時間表示
                yield return new WaitForSeconds(clearImageDisplayDuration);

                // フェードアウト
                clearImage.DOFade(0f, clearImageFadeOutDuration);
                yield return new WaitForSeconds(clearImageFadeOutDuration);

                Debug.Log("[TeichakuFeedback] Clear image sequence completed.");
            }

            // ProgressをPresentationフェーズに変換
            if (ProgressManager.Instance != null)
                ProgressManager.Instance.SetProgress(
                    ProgressManager.Instance.CurrentChapter, GamePhase.Presentation);

            // フェード付きでMainシーンへ戻る
            if (SceneTransition.Instance != null)
                SceneTransition.Instance.TransitionTo("Main");
            else
                SceneManager.LoadScene("Main");
        }

        /// <summary>
        /// 失敗時の演出（シェイク）
        /// </summary>
        public void OnFail()
        {
            // 画面シェイク（グリッドコンテナを揺らす）
            if (gridContainer != null)
            {
                gridContainer.DOKill();
                gridContainer.anchoredPosition = _gridInitialPos;
                gridContainer
                    .DOShakeAnchorPos(shakeDuration, shakeStrength, shakeVibrato, 90f, false, true)
                    .OnComplete(() => gridContainer.anchoredPosition = _gridInitialPos);
            }

            // SE再生
            if (seSource != null && failSE != null)
            {
                seSource.PlayOneShot(failSE);
            }

            Debug.Log("[TeichakuFeedback] Fail shake played.");
        }

        /// <summary>
        /// フェードアウト演出
        /// </summary>
        public void FadeOut(float duration, Action onComplete)
        {
            if (fadeOverlay != null)
            {
                fadeOverlay.color = new Color(0f, 0f, 0f, 0f);
                fadeOverlay.DOFade(1f, duration).OnComplete(() => onComplete?.Invoke());
            }
            else
            {
                onComplete?.Invoke();
            }
        }

        /// <summary>
        /// フェードイン演出
        /// </summary>
        public void FadeIn(float duration)
        {
            if (fadeOverlay != null)
            {
                fadeOverlay.color = new Color(0f, 0f, 0f, 1f);
                fadeOverlay.DOFade(0f, duration);
            }
        }

        /// <summary>
        /// 演出状態をリセットする
        /// </summary>
        public void ResetFeedback()
        {
            if (flashOverlay != null)
            {
                flashOverlay.DOKill();
                var c = flashOverlay.color;
                c.a = 0f;
                flashOverlay.color = c;
            }

            if (gridContainer != null)
            {
                gridContainer.DOKill();
                gridContainer.anchoredPosition = _gridInitialPos;
            }
        }
    }
}
