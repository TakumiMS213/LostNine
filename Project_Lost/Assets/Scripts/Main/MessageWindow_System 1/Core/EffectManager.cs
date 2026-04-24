using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;

namespace MessageWindowSystem.Core
{
    /// <summary>
    /// Manages visual and audio effects for the dialogue system.
    /// </summary>
    public class EffectManager : MonoBehaviour
    {
        #region Singleton

        public static EffectManager Instance { get; private set; }

        #endregion

        #region Serialized Fields

        [Header("Overlays")]
        [SerializeField] private Image flashOverlay;
        [SerializeField] private Image fadeOverlay;
        [SerializeField] private Image centerImage;

        [Header("Audio Sources")]
        [SerializeField] private AudioSource seAudioSource;
        [SerializeField] private AudioSource bgmAudioSource;

        [Header("Keyword Development")]
        [SerializeField] private AudioClip chargeSE;
        [SerializeField] private AudioClip developCompleteSE;
        [SerializeField] private float developShakeStrength = 3f;
        [SerializeField] private float developShakeDuration = 0.5f;

        [Header("Flash Settings")]
        [SerializeField] private float flashInDuration = 0.08f;
        [SerializeField] private float flashOutDuration = 0.25f;
        [SerializeField] private Color flashColor = Color.white;

        #endregion

        #region Private Fields

        private Coroutine _shakeCoroutine;
        private Coroutine _flashCoroutine;
        private Coroutine _fadeCoroutine;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }

            InitOverlayAlpha(flashOverlay);
            InitOverlayAlpha(fadeOverlay);
        }

        private static void InitOverlayAlpha(Image overlay)
        {
            if (overlay == null) return;
            var c = overlay.color;
            c.a = 0f;
            overlay.color = c;
        }

        #endregion

        #region Public Effect API

        // Removed PlayEffect(ScreenEffectData)

        public void PlayChargeSE()
        {
            if (seAudioSource == null || chargeSE == null) return;
            seAudioSource.clip = chargeSE;
            seAudioSource.loop = true;
            seAudioSource.Play();
        }

        public void StopChargeSE()
        {
            if (seAudioSource == null || seAudioSource.clip != chargeSE) return;
            seAudioSource.Stop();
            seAudioSource.loop = false;
            seAudioSource.clip = null;
        }

        public void PlayDevelopmentEffect(Action onComplete = null)
        {
            if (developCompleteSE != null)
                seAudioSource?.PlayOneShot(developCompleteSE);

            var seq = DOTween.Sequence();
            seq.AppendCallback(() => PlayShake(developShakeDuration, developShakeStrength));

            if (flashOverlay != null)
            {
                flashOverlay.color = new Color(flashColor.r, flashColor.g, flashColor.b, 0f);
                seq.Join(flashOverlay.DOFade(1f, 0.1f));
                seq.Append(flashOverlay.DOFade(0f, 0.5f));
            }

            seq.OnComplete(() => onComplete?.Invoke());
        }

        #endregion

        #region Audio Methods

        private void PlaySE(string clipName)
        {
            if (seAudioSource == null || string.IsNullOrEmpty(clipName)) return;
            var clip = Resources.Load<AudioClip>($"Audio/SE/{clipName}");
            if (clip != null) seAudioSource.PlayOneShot(clip);
            else Debug.LogWarning($"[EffectManager] SE not found: Audio/SE/{clipName}");
        }

        public void PlaySE(AudioClip clip)
        {
            if (seAudioSource == null || clip == null) return;
            seAudioSource.PlayOneShot(clip);
        }

        private void PlayBGM(string clipName)
        {
            if (bgmAudioSource == null || string.IsNullOrEmpty(clipName)) return;
            var clip = Resources.Load<AudioClip>($"Audio/BGM/{clipName}");
            if (clip == null)
            {
                Debug.LogWarning($"[EffectManager] BGM not found: Audio/BGM/{clipName}");
                return;
            }
            if (bgmAudioSource.clip == clip && bgmAudioSource.isPlaying) return;

            bgmAudioSource.clip = clip;
            bgmAudioSource.loop = true;
            bgmAudioSource.Play();
        }

        public void StopBGM() => bgmAudioSource?.Stop();

        #endregion

        #region Image Methods

        private void ShowImage(string imageName, Sprite directSprite, Color tint)
        {
            if (centerImage == null) return;

            Sprite sprite = directSprite ?? Resources.Load<Sprite>($"Images/{imageName}");
            if (sprite == null)
            {
                Debug.LogWarning($"[EffectManager] Image not found: {imageName}");
                return;
            }

            centerImage.sprite = sprite;
            centerImage.color = tint.a > 0 ? tint : Color.white;
            centerImage.SetNativeSize();
            centerImage.gameObject.SetActive(true);
        }

        private void HideImage() => centerImage?.gameObject.SetActive(false);

        #endregion

        #region Visual Effect Methods

        private void PlayShake(float duration, float magnitude)
        {
            if (_shakeCoroutine != null) StopCoroutine(_shakeCoroutine);
            _shakeCoroutine = StartCoroutine(ShakeCamera(duration, magnitude));
        }

        private void PlayFlash(Color color)
        {
            if (_flashCoroutine != null) StopCoroutine(_flashCoroutine);
            _flashCoroutine = StartCoroutine(FlashEffect(color));
        }

        private void PlayFade(float duration, Color color, bool fadeIn)
        {
            if (_fadeCoroutine != null) StopCoroutine(_fadeCoroutine);
            _fadeCoroutine = StartCoroutine(FadeEffect(duration, color, fadeIn));
        }

        private IEnumerator FlashEffect(Color color)
        {
            if (flashOverlay == null) yield break;

            Color c = color == Color.clear ? flashColor : color;

            yield return LerpAlpha(flashOverlay, c, 0f, 1f, flashInDuration);
            yield return LerpAlpha(flashOverlay, c, 1f, 0f, flashOutDuration);
        }

        private IEnumerator FadeEffect(float duration, Color color, bool fadeIn)
        {
            if (fadeOverlay == null) yield break;

            Color targetColor = color == Color.clear ? Color.black : color;
            float startAlpha = fadeOverlay.color.a;
            float endAlpha = fadeIn ? 0f : 1f;

            if (!fadeIn) fadeOverlay.color = new Color(targetColor.r, targetColor.g, targetColor.b, startAlpha);

            yield return LerpAlpha(fadeOverlay, targetColor, startAlpha, endAlpha, duration);
        }

        private static IEnumerator LerpAlpha(Image img, Color baseColor, float from, float to, float duration)
        {
            if (duration <= 0f)
            {
                img.color = new Color(baseColor.r, baseColor.g, baseColor.b, to);
                yield break;
            }

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                img.color = new Color(baseColor.r, baseColor.g, baseColor.b, Mathf.Lerp(from, to, t / duration));
                yield return null;
            }
            img.color = new Color(baseColor.r, baseColor.g, baseColor.b, to);
        }

        private static IEnumerator ShakeCamera(float duration, float magnitude)
        {
            var cam = Camera.main;
            if (cam == null) yield break;

            var camTransform = cam.transform;
            Vector3 originalPos = camTransform.localPosition;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                // Re-check camera validity each frame
                if (camTransform == null) yield break;

                float x = UnityEngine.Random.Range(-1f, 1f) * magnitude;
                float y = UnityEngine.Random.Range(-1f, 1f) * magnitude;
                camTransform.localPosition = originalPos + new Vector3(x, y, 0f);
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (camTransform != null)
                camTransform.localPosition = originalPos;
        }

        #endregion
    }
}