using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using ScenarioSystem.Events;
using ScenarioSystem.Model.Actions;

namespace ScenarioSystem.View
{
    /// <summary>
    /// 演出表示を担当する View。
    /// EventBus の OnEffectRequested を購読し、各種演出（Shake, Flash, SE, BGM等）を実行する。
    /// 旧 EffectManager の機能を EventBus 駆動に変換。
    /// </summary>
    public class EffectView : MonoBehaviour
    {
        #region Serialized Fields

        [Header("Overlays")]
        [SerializeField] private Image flashOverlay;
        [SerializeField] private Image fadeOverlay;
        [SerializeField] private Image centerImage;

        [Header("Audio Sources")]
        [SerializeField] private AudioSource seAudioSource;
        [SerializeField] private AudioSource bgmAudioSource;

        [Header("Flash Settings")]
        [SerializeField] private float flashInDuration = 0.08f;
        [SerializeField] private float flashOutDuration = 0.25f;
        [SerializeField] private Color defaultFlashColor = Color.white;

        #endregion

        #region Private Fields

        private Coroutine _shakeCoroutine;
        private Coroutine _flashCoroutine;
        private Coroutine _fadeCoroutine;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            InitOverlayAlpha(flashOverlay);
            InitOverlayAlpha(fadeOverlay);
        }

        private void OnEnable()
        {
            ScenarioEventBus.OnEffectRequested += HandleEffectRequested;
        }

        private void OnDisable()
        {
            ScenarioEventBus.OnEffectRequested -= HandleEffectRequested;
        }

        #endregion

        #region Event Handler

        private void HandleEffectRequested(EffectAction action)
        {
            switch (action.effectType)
            {
                case ScenarioEffectType.None:
                    break;
                case ScenarioEffectType.Shake:
                    PlayShake(action.floatParam > 0 ? action.floatParam : 0.5f, 0.2f);
                    break;
                case ScenarioEffectType.Flash:
                    PlayFlash(action.colorParam);
                    break;
                case ScenarioEffectType.FadeIn:
                    PlayFade(action.floatParam, action.colorParam, fadeIn: true);
                    break;
                case ScenarioEffectType.FadeOut:
                    PlayFade(action.floatParam, action.colorParam, fadeIn: false);
                    break;
                case ScenarioEffectType.PlaySE:
                    PlaySE(action.stringParam);
                    break;
                case ScenarioEffectType.PlayBGM:
                    PlayBGM(action.stringParam);
                    break;
                case ScenarioEffectType.StopBGM:
                    StopBGM();
                    break;
                case ScenarioEffectType.ShowImage:
                    ShowImage(action.stringParam, action.spriteParam, action.colorParam);
                    break;
                case ScenarioEffectType.HideImage:
                    HideImage();
                    break;
            }
        }

        #endregion

        #region Audio

        private void PlaySE(string clipName)
        {
            if (seAudioSource == null || string.IsNullOrEmpty(clipName)) return;
            var clip = Resources.Load<AudioClip>($"Audio/SE/{clipName}");
            if (clip != null) seAudioSource.PlayOneShot(clip);
            else Debug.LogWarning($"[EffectView] SE not found: Audio/SE/{clipName}");
        }

        private void PlayBGM(string clipName)
        {
            if (bgmAudioSource == null || string.IsNullOrEmpty(clipName)) return;
            var clip = Resources.Load<AudioClip>($"Audio/BGM/{clipName}");
            if (clip == null)
            {
                Debug.LogWarning($"[EffectView] BGM not found: Audio/BGM/{clipName}");
                return;
            }
            if (bgmAudioSource.clip == clip && bgmAudioSource.isPlaying) return;

            bgmAudioSource.clip = clip;
            bgmAudioSource.loop = true;
            bgmAudioSource.Play();
        }

        private void StopBGM() => bgmAudioSource?.Stop();

        #endregion

        #region Image

        private void ShowImage(string imageName, Sprite directSprite, Color tint)
        {
            if (centerImage == null) return;

            Sprite sprite = directSprite ?? Resources.Load<Sprite>($"Images/{imageName}");
            if (sprite == null)
            {
                Debug.LogWarning($"[EffectView] Image not found: {imageName}");
                return;
            }

            centerImage.sprite = sprite;
            centerImage.color = tint.a > 0 ? tint : Color.white;
            centerImage.SetNativeSize();
            centerImage.gameObject.SetActive(true);
        }

        private void HideImage() => centerImage?.gameObject.SetActive(false);

        #endregion

        #region Visual Effects

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
            Color c = color == Color.clear ? defaultFlashColor : color;

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
                if (camTransform == null) yield break;

                float x = Random.Range(-1f, 1f) * magnitude;
                float y = Random.Range(-1f, 1f) * magnitude;
                camTransform.localPosition = originalPos + new Vector3(x, y, 0f);
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (camTransform != null)
                camTransform.localPosition = originalPos;
        }

        #endregion

        #region Utility

        private static void InitOverlayAlpha(Image overlay)
        {
            if (overlay == null) return;
            var c = overlay.color;
            c.a = 0f;
            overlay.color = c;
        }

        #endregion
    }
}
