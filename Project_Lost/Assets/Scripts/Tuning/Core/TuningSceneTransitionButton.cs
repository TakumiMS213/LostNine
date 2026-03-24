using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using DG.Tweening;
using Main.UIMoves;

namespace Tuning.Core
{
    /// <summary>
    /// Tuning成功後のシーン遷移ボタンに付けるスクリプト。
    /// OnClickイベントからTransitionToFixation()を呼ぶだけでOK。
    /// MultiEasing（ラベル "FadeOut"）が見つかればそれを使い、
    /// なければ自前のフェードアウト → シーンロードを行う。
    /// </summary>
    public class TuningSceneTransitionButton : MonoBehaviour
    {
        [Tooltip("遷移先のシーン名")]
        [SerializeField] private string targetSceneName = "Memorize_2";

        [Header("フェード設定")]
        [Tooltip("フェード用のオーバーレイImage（画面全体を覆う黒いImage）")]
        [SerializeField] private Image fadeOverlay;

        [Tooltip("フェードアウトの時間（秒）")]
        [SerializeField] private float fadeOutDuration = 0.5f;

        [Header("MultiEasing 設定")]
        [Tooltip("フェードアウト用 MultiEasing のラベル")]
        [SerializeField] private string fadeOutLabel = "FadeOut";

        private bool _isTransitioning;

        private void Start()
        {
            // 初期状態: 完全に透明
            if (fadeOverlay != null)
            {
                var c = fadeOverlay.color;
                c.a = 0f;
                fadeOverlay.color = c;
                fadeOverlay.raycastTarget = false;
            }
        }

        /// <summary>
        /// ボタンのOnClickイベントに設定するメソッド。
        /// フェーズをFixationに更新し、フェード付きで指定シーンへ遷移する。
        /// </summary>
        public void TransitionToFixation()
        {
            if (_isTransitioning) return;
            _isTransitioning = true;

            // フェーズを Fixation に更新
            if (ProgressManager.Instance != null)
                ProgressManager.Instance.SetProgress(ProgressManager.Instance.CurrentChapter, GamePhase.Fixation);

            // MultiEasing を検索してフェードアウト
            var fadeOutEasing = MultiEasing.FindByLabel(fadeOutLabel);
            if (fadeOutEasing != null)
            {
                if (fadeOverlay != null) fadeOverlay.raycastTarget = true;
                fadeOutEasing.Play();
                StartCoroutine(WaitAndLoadScene(fadeOutEasing));
            }
            else
            {
                // フォールバック: 直接 DOFade
                if (fadeOverlay != null)
                {
                    fadeOverlay.raycastTarget = true;
                    fadeOverlay.DOFade(1f, fadeOutDuration).OnComplete(() =>
                    {
                        Debug.Log($"[TuningSceneTransitionButton] Fade out complete. Loading {targetSceneName}");
                        SceneManager.LoadScene(targetSceneName);
                    });
                }
                else
                {
                    Debug.LogWarning("[TuningSceneTransitionButton] fadeOverlay is not assigned. Transitioning without fade.");
                    SceneManager.LoadScene(targetSceneName);
                }
            }
        }

        private System.Collections.IEnumerator WaitAndLoadScene(MultiEasing easing)
        {
            yield return new WaitWhile(() => easing != null && easing.IsPlaying);
            Debug.Log($"[TuningSceneTransitionButton] MultiEasing fade out complete. Loading {targetSceneName}");
            SceneManager.LoadScene(targetSceneName);
        }
    }
}
