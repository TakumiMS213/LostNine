using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using UnityEngine.SceneManagement;

namespace Tuning.Core
{
    /// <summary>
    /// 調律ミニゲームの演出処理
    /// </summary>
    public class TuningFeedback : MonoBehaviour
    {
        [Header("オーディオ")]
        [Tooltip("BGMを再生するAudioSource")]
        [SerializeField] private AudioSource bgmSource;

        [Tooltip("同期率に連動するローパスフィルター")]
        [SerializeField] private AudioLowPassFilter lowPassFilter;

        [Tooltip("最低カットオフ周波数（同期率0%時）")]
        [SerializeField] private float minCutoff = 500f;

        [Tooltip("最高カットオフ周波数（同期率100%時）")]
        [SerializeField] private float maxCutoff = 22000f;

        [Header("ビジュアル")]
        [Tooltip("フラッシュ演出用オーバーレイ画像")]
        [SerializeField] private Image flashOverlay;

        [Tooltip("フェード演出用オーバーレイ画像（暗転用）")]
        [SerializeField] private Image fadeOverlay;

        [Tooltip("左側の点のビジュアル（シェイク演出用）")]
        [SerializeField] private RectTransform leftPointVisual;

        [Tooltip("右側の点のビジュアル（シェイク演出用）")]
        [SerializeField] private RectTransform rightPointVisual;

        [Tooltip("安定度が低い時の追加シェイク最大強度（不安定なほど揺れる）")]
        [SerializeField] private float maxStabilityShake = 5f;

        [Tooltip("シェイク位置を初期位置に戻そうとする強さ")]
        [SerializeField] private float centeringSpeed = 5f;

        [Tooltip("ノイズ演出用オーバーレイ（同期率が低いと表示）")]
        [SerializeField] private CanvasGroup noiseOverlay;

        [SerializeField] private Vector2 _leftInitPos;
        [SerializeField] private Vector2 _rightInitPos;

        [Header("UI")]
        [Tooltip("ゲームオーバーまでの残り時間を表示するバー（Xスケールで変動）")]
        [SerializeField] private RectTransform overheatTimerBar;

        [Tooltip("バーの色を変更するためのImage（警告用）")]
        [SerializeField] private Image overheatTimerImage;

        [Header("ビジュアル（波形）")]
        [Tooltip("左側の波形ビジュアライザー")]
        [SerializeField] private Tuning.Visuals.WaveformVisualizer leftWaveform;

        [Tooltip("右側の波形ビジュアライザー")]
        [SerializeField] private Tuning.Visuals.WaveformVisualizer rightWaveform;

        [Tooltip("最小周波数（同期率0%時）")]
        [SerializeField] private float minWaveFreq = 2f;

        [Tooltip("最大周波数（同期率100%時）")]
        [SerializeField] private float maxWaveFreq = 15f;

        [Tooltip("最小振幅（安定度0%時）")]
        [SerializeField] private float minWaveAmp = 20f;

        [Tooltip("最大振幅（安定度100%時）")]
        [SerializeField] private float maxWaveAmp = 60f;

        [Tooltip("通常時の線の太さ")]
        [SerializeField] private float baseThickness = 2f;

        [Tooltip("ターゲットロック時の線の太さ加算値（1つにつき）")]
        [SerializeField] private float thicknessBoostPerLock = 3f;

        [Tooltip("NGゾーン滞在時のノイズビジュアライザー")]
        [SerializeField] private Tuning.Visuals.NoiseVisualizer ngNoise;

        [Header("ターゲット点滅演出")]
        [Tooltip("左ターゲットの CanvasGroup（点滅対象）")]
        [SerializeField] private CanvasGroup leftTargetGroup;

        [Tooltip("右ターゲットの CanvasGroup（点滅対象）")]
        [SerializeField] private CanvasGroup rightTargetGroup;

        [Tooltip("点滅が開始されるまでのターゲット外滞在時間（秒）")]
        [SerializeField] private float blinkStartDelay = 3f;

        [Tooltip("点滅が最大速度に達するまでの時間（秒）")]
        [SerializeField] private float blinkRampDuration = 5f;

        [Tooltip("最大点滅速度（Hz）")]
        [SerializeField] private float blinkMaxFrequency = 6f;

        [Tooltip("最小点滅速度（Hz、遅い段階）")]
        [SerializeField] private float blinkMinFrequency = 1f;

        [Tooltip("点滅の最小透明度（0=完全消灯, 0.3=薄く残る）")]
        [Range(0f, 1f)]
        [SerializeField] private float blinkMinAlpha = 0.15f;

        // ── 点滅の内部状態 ──
        private float _leftOutOfTargetTime  = 0f;
        private float _rightOutOfTargetTime = 0f;

        [Header("成功演出")]
        [Tooltip("成功時に再生するSE")]
        [SerializeField] private AudioClip successSE;

        [Tooltip("成功時に動かすオブジェクト（MoveOnClickandReturnを持つ）")]
        [SerializeField] private MoveOnClickandReturn successMoveVisual;
        [SerializeField] private MoveOnClickandReturn ToNextStepVisual;

        [Tooltip("SE再生用AudioSource")]
        [SerializeField] private AudioSource seSource;

        private float _targetCutoff;

        private void Awake()
        {
            if (flashOverlay != null)
            {
                var c = flashOverlay.color;
                c.a = 0f;
                flashOverlay.color = c;
            }

            if (leftWaveform != null) leftWaveform.PhaseOffset = 0f;
            if (rightWaveform != null) rightWaveform.PhaseOffset = Mathf.PI;
        }

        private void Start()
        {
            //if (leftPointVisual != null) _leftInitPos = leftPointVisual.anchoredPosition;
            //if (rightPointVisual != null) _rightInitPos = rightPointVisual.anchoredPosition;
        }

        /// <summary>
        /// 同期率、安定度、ロック状態に基づいて演出を更新
        /// </summary>
        /// <summary>
        /// 同期率、安定度、ロック状態に基づいて演出を更新
        /// </summary>
        public void OnSyncUpdate(float leftSync, float rightSync, float totalSync, float stability, bool leftLocked, bool rightLocked)
        {
            if (lowPassFilter == null) return;

            _targetCutoff = Mathf.Lerp(minCutoff, maxCutoff, totalSync);
            lowPassFilter.cutoffFrequency = Mathf.Lerp(lowPassFilter.cutoffFrequency, _targetCutoff, Time.deltaTime * 5f);

            if (noiseOverlay != null)
                noiseOverlay.alpha = 1f - totalSync;

            UpdateWaveform(leftWaveform, leftSync, stability, leftLocked);
            UpdateWaveform(rightWaveform, rightSync, stability, rightLocked);

            ApplyShake(totalSync, stability);
            UpdateTargetBlink(leftLocked, rightLocked);
        }

        private void UpdateWaveform(Tuning.Visuals.WaveformVisualizer wave, float sync, float stability, bool isLocked)
        {
            if (wave == null) return;

            // 同期率 -> 周波数（細かい波へ）
            wave.Frequency = Mathf.Lerp(minWaveFreq, maxWaveFreq, sync);
            // 安定度 -> 振幅（大きな波へ）
            wave.Amplitude = Mathf.Lerp(minWaveAmp, maxWaveAmp, stability);
            
            // ターゲットロック -> 線の太さ
            float targetThickness = baseThickness + (isLocked ? thicknessBoostPerLock : 0f);
            wave.Thickness = Mathf.Lerp(wave.Thickness, targetThickness, Time.deltaTime * 10f);
        }

        /// <summary>
        /// ターゲット外の累積時間に応じてターゲット円を点滅させる
        /// </summary>
        private void UpdateTargetBlink(bool leftLocked, bool rightLocked)
        {
            UpdateSingleTargetBlink(leftTargetGroup,  leftLocked,  ref _leftOutOfTargetTime);
            UpdateSingleTargetBlink(rightTargetGroup, rightLocked, ref _rightOutOfTargetTime);
        }

        private void UpdateSingleTargetBlink(CanvasGroup group, bool isLocked, ref float outTimer)
        {
            if (group == null) return;

            if (isLocked)
            {
                // ターゲット内：タイマーリセット + 非表示に戻す
                outTimer = 0f;
                group.alpha = Mathf.Lerp(group.alpha, 0f, Time.deltaTime * 5f);
                return;
            }

            // ターゲット外：累積カウント
            outTimer += Time.deltaTime;

            if (outTimer < blinkStartDelay)
            {
                // ヒント表示前：完全非表示
                group.alpha = 0f;
                return;
            }

            // blinkStartDelay 〜 blinkStartDelay+blinkRampDuration で周波数が上昇
            float rampT     = Mathf.Clamp01((outTimer - blinkStartDelay) / blinkRampDuration);
            float frequency = Mathf.Lerp(blinkMinFrequency, blinkMaxFrequency, rampT);

            // Sin波で点滅（0〜1 に正規化）
            float sinVal    = (Mathf.Sin(Time.time * frequency * Mathf.PI * 2f) + 1f) * 0.5f;
            group.alpha     = Mathf.Lerp(blinkMinAlpha, 1f, sinVal);
        }


        /// <summary>
        /// ペナルティ状態（オーバーヒートタイマー）の更新
        /// </summary>
        public void OnPenaltyUpdate(float currentTimer, float threshold, bool isInNGZone, float stability)
        {
            if (overheatTimerBar != null)
            {
                // 残り時間の割合 (1.0 -> 0.0)
                float ratio = Mathf.Clamp01(1f - (currentTimer / threshold));
                Vector3 scale = overheatTimerBar.localScale;
                scale.x = ratio;
                overheatTimerBar.localScale = scale;

                // 残り時間が少なくなったら赤くする（残り約20%以下など）
                if (overheatTimerImage != null)
                {
                    if (ratio < 0.3f)
                    {
                        overheatTimerImage.color = Color.Lerp(Color.red, Color.white, Mathf.PingPong(Time.time * 10f, 1f));
                    }
                    else
                    {
                        overheatTimerImage.color = Color.white;
                    }
                }
            }

            // NGゾーンのノイズ演出
            // 安定度がMAX(1.0)になったら消える
            if (ngNoise != null)
            {
                bool showNoise = isInNGZone && stability < 0.99f;
                ngNoise.IsEffectActive = showNoise;
            }
        }

        /// <summary>
        /// 片方の点がターゲットに入った時
        /// </summary>
        public void OnPointInTarget(int side)
        {
            if (flashOverlay != null)
            {
                flashOverlay.DOKill();
                flashOverlay.DOFade(0.3f, 0.05f).OnComplete(() => flashOverlay.DOFade(0f, 0.15f));
            }
        }

        /// <summary>
        /// 調律成功時
        /// </summary>
        public void OnSuccess()
        {
            if (flashOverlay != null)
            {
                flashOverlay.DOKill();
                flashOverlay.DOFade(1f, 0.1f).OnComplete(() => flashOverlay.DOFade(0f, 0.5f));
            }

            if (seSource != null && successSE != null)
                seSource.PlayOneShot(successSE);

            if (bgmSource != null)
            {
                bgmSource.DOFade(0f, 0.5f).OnComplete(() => bgmSource.Stop());
            }

            if (MessageWindowSystem.Core.EffectManager.Instance != null)
            {
                MessageWindowSystem.Core.EffectManager.Instance.StopBGM();
            }

            if (lowPassFilter != null)
                lowPassFilter.cutoffFrequency = maxCutoff;

            if (noiseOverlay != null)
                noiseOverlay.DOFade(0f, 0.5f);

            if (overheatTimerBar != null)
                overheatTimerBar.gameObject.SetActive(false);

            // 波形を最大状態にする
            if (leftWaveform != null)
            {
                leftWaveform.Frequency = maxWaveFreq;
                leftWaveform.Amplitude = maxWaveAmp;
                leftWaveform.PhaseOffset = 0f;
                leftWaveform.ResetWave();
            }
            if (rightWaveform != null)
            {
                rightWaveform.Frequency = maxWaveFreq;
                rightWaveform.Amplitude = maxWaveAmp;
                rightWaveform.PhaseOffset = Mathf.PI;
                rightWaveform.ResetWave();
            }

            // 成功演出アニメーション（MoveOnClickandReturn）
            if (successMoveVisual != null)
            {
                successMoveVisual.Play();
                // 2秒後にもう一度再生（元の位置に戻る等の動作を想定）
                DOVirtual.DelayedCall(2f, () => 
                {
                    if (successMoveVisual != null) successMoveVisual.Play();
                });
            }

            // 次のステップへの遷移ボタンを表示（直接シーン遷移せず、ボタンで遷移させる）
            DOVirtual.DelayedCall(3f, () => 
            {
                if (ToNextStepVisual != null)
                {
                    ToNextStepVisual.Play();
                    Debug.Log("[TuningFeedback] Scene transition button displayed.");
                }
                else
                {
                    Debug.LogWarning("[TuningFeedback] ToNextStepVisual is not assigned. Falling back to direct transition.");
                    // フォールバック: ボタンが未設定の場合は直接遷移
                    if (ProgressManager.Instance != null)
                        ProgressManager.Instance.SetProgress(ProgressManager.Instance.CurrentChapter, GamePhase.Fixation);

                    if (SceneTransition.Instance != null)
                        SceneTransition.Instance.TransitionTo("Memorize_2");
                    else
                        SceneManager.LoadScene("Memorize_2");
                }
            }).SetLink(gameObject);
        }

        /// <summary>
        /// ゲームオーバー時（オーバーヒート）
        /// </summary>
        public void OnGameOver()
        {
            if (flashOverlay != null)
            {
                flashOverlay.color = new Color(0f, 0f, 0f, 0f);
                flashOverlay.DOFade(0.8f, 0.2f);
            }
        }

        public void FadeOut(float duration, System.Action onComplete)
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

        public void FadeIn(float duration)
        {
            if (fadeOverlay != null)
            {
                fadeOverlay.color = new Color(0f, 0f, 0f, 1f);
                fadeOverlay.DOFade(0f, duration);
            }
        }

        private void ApplyShake(float sync, float stability)
        {
            // 同期率が低い時のシェイク + 安定度が低い時のシェイク
            // 安定度が高まるにつれてシェイクが収まっていく
            float baseShake = (1f - sync) * 3f;
            float instabilityShake = (1f - stability) * maxStabilityShake;
            
            float totalShake = baseShake + instabilityShake;

            if (leftPointVisual != null)
            {
                Vector2 currentPos = leftPointVisual.anchoredPosition;
                Vector2 random = Random.insideUnitCircle * totalShake;
                
                // 初期位置に戻ろうとする力（ばねのような挙動）
                Vector2 restoringForce = (_leftInitPos - currentPos) * centeringSpeed;

                Vector2 move = (random + restoringForce) * Time.deltaTime * 30f;
                leftPointVisual.anchoredPosition += move;
            }

            if (rightPointVisual != null)
            {
                Vector2 currentPos = rightPointVisual.anchoredPosition;
                Vector2 random = Random.insideUnitCircle * totalShake;

                // 初期位置に戻ろうとする力
                Vector2 restoringForce = (_rightInitPos - currentPos) * centeringSpeed;

                Vector2 move = (random + restoringForce) * Time.deltaTime * 30f;
                rightPointVisual.anchoredPosition += move;
            }
        }
        public void ResetFeedback()
        {
            if (ngNoise != null)
                ngNoise.IsEffectActive = false;
            
            if (overheatTimerBar != null)
            {
                overheatTimerBar.localScale = new Vector3(1f, 1f, 1f);
                overheatTimerBar.gameObject.SetActive(true);
            }

            if (overheatTimerImage != null)
                overheatTimerImage.color = Color.white;

            if (noiseOverlay != null)
                noiseOverlay.alpha = 1f; // Initial state (low sync)

            // 位相オフセットをリセット（確実に適用するためここで再設定）
            if (leftWaveform != null) leftWaveform.PhaseOffset = 0f;
            if (rightWaveform != null) rightWaveform.PhaseOffset = Mathf.PI;

            if (flashOverlay != null)
            {
                flashOverlay.DOKill();
                var c = flashOverlay.color;
                c.a = 0f;
                flashOverlay.color = c;
            }

            // ターゲット点滅をリセット（普段は非表示）
            _leftOutOfTargetTime  = 0f;
            _rightOutOfTargetTime = 0f;
            if (leftTargetGroup  != null) leftTargetGroup.alpha  = 0f;
            if (rightTargetGroup != null) rightTargetGroup.alpha = 0f;
        }
    }
}
