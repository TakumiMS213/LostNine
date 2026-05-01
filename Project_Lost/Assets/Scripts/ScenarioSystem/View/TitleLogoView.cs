using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using ScenarioSystem.Events;

namespace ScenarioSystem.View
{
    public class TitleLogoView : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private RectTransform titleRoot;
        [SerializeField] private Image backgroundImage;
        [SerializeField] private Image logoImage;

        private Coroutine _playRoutine;

        private void Awake()
        {
            EnsureUi();
            HideImmediate();
        }

        private void OnEnable()
        {
            ScenarioEventBus.OnTitleLogoRequested += HandleTitleLogoRequested;
            ScenarioEventBus.OnScenarioStarted += HandleScenarioStarted;
            ScenarioEventBus.OnScenarioEnded += HandleScenarioEnded;
        }

        private void OnDisable()
        {
            StopAndHide();
            ScenarioEventBus.OnTitleLogoRequested -= HandleTitleLogoRequested;
            ScenarioEventBus.OnScenarioStarted -= HandleScenarioStarted;
            ScenarioEventBus.OnScenarioEnded -= HandleScenarioEnded;
        }

        private void HandleTitleLogoRequested(TitleLogoEventData data)
        {
            EnsureUi();

            if (_playRoutine != null)
                StopCoroutine(_playRoutine);

            _playRoutine = StartCoroutine(PlayRoutine(data));
        }

        private void HandleScenarioStarted(ScenarioSystem.Model.ScenarioData _)
        {
            StopAndHide();
        }

        private void HandleScenarioEnded(ScenarioSystem.Model.ScenarioData _)
        {
            StopAndHide();
        }

        private void StopAndHide()
        {
            if (_playRoutine != null)
            {
                StopCoroutine(_playRoutine);
                _playRoutine = null;
            }

            HideImmediate();
        }

        private IEnumerator PlayRoutine(TitleLogoEventData data)
        {
            if (titleRoot == null || backgroundImage == null || logoImage == null)
                yield break;

            titleRoot.SetAsLastSibling();
            titleRoot.gameObject.SetActive(true);

            Color backgroundColor = data.BackgroundColor;
            float targetBackgroundAlpha = backgroundColor.a <= 0f ? 1f : backgroundColor.a;
            SetImageAlpha(backgroundImage, backgroundColor, 0f);

            logoImage.sprite = data.LogoSprite;
            logoImage.gameObject.SetActive(data.LogoSprite != null);
            SetImageAlpha(logoImage, Color.white, 0f);
            ApplyLogoSize(data);

            yield return FadeImage(backgroundImage, backgroundColor, 0f, targetBackgroundAlpha, data.BackgroundFadeInDuration);
            yield return FadeImage(logoImage, Color.white, 0f, 1f, data.LogoFadeInDuration);
            yield return Wait(data.LogoDisplayDuration);
            yield return FadeImage(logoImage, Color.white, 1f, 0f, data.LogoFadeOutDuration);
            logoImage.gameObject.SetActive(false);

            if (data.FadeOutBackgroundAfterLogo)
            {
                yield return Wait(data.BackgroundHoldDuration);
                yield return FadeImage(backgroundImage, backgroundColor, targetBackgroundAlpha, 0f, data.BackgroundFadeOutDuration);
                HideImmediate();
            }
            else
            {
                SetImageAlpha(backgroundImage, backgroundColor, targetBackgroundAlpha);
            }

            _playRoutine = null;
        }

        private void EnsureUi()
        {
            if (titleRoot != null && backgroundImage != null && logoImage != null)
                return;

            Canvas canvas = GetComponentInParent<Canvas>();
            if (canvas == null)
                canvas = FindFirstObjectByType<Canvas>();

            if (canvas == null)
            {
                Debug.LogWarning("[TitleLogoView] Canvas not found. Title logo action cannot be displayed.");
                return;
            }

            if (titleRoot == null)
            {
                var rootObj = new GameObject("TitleLogoRoot", typeof(RectTransform));
                titleRoot = rootObj.GetComponent<RectTransform>();
                titleRoot.SetParent(canvas.transform, false);
                StretchFull(titleRoot);
            }

            if (backgroundImage == null)
            {
                var backgroundObj = new GameObject("TitleLogoBackground", typeof(RectTransform), typeof(Image));
                var rect = backgroundObj.GetComponent<RectTransform>();
                rect.SetParent(titleRoot, false);
                StretchFull(rect);

                backgroundImage = backgroundObj.GetComponent<Image>();
                backgroundImage.raycastTarget = false;
            }

            if (logoImage == null)
            {
                var logoObj = new GameObject("TitleLogoImage", typeof(RectTransform), typeof(Image));
                var rect = logoObj.GetComponent<RectTransform>();
                rect.SetParent(titleRoot, false);
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(900f, 360f);

                logoImage = logoObj.GetComponent<Image>();
                logoImage.preserveAspect = true;
                logoImage.raycastTarget = false;
            }
        }

        private void ApplyLogoSize(TitleLogoEventData data)
        {
            if (logoImage == null || data.LogoSprite == null)
                return;

            RectTransform rect = logoImage.rectTransform;
            if (data.UseNativeLogoSize)
                logoImage.SetNativeSize();

            Vector2 size = rect.sizeDelta;
            Vector2 maxSize = data.MaxLogoSize;

            float scale = 1f;
            if (maxSize.x > 0f && size.x > maxSize.x)
                scale = Mathf.Min(scale, maxSize.x / size.x);
            if (maxSize.y > 0f && size.y > maxSize.y)
                scale = Mathf.Min(scale, maxSize.y / size.y);

            rect.sizeDelta = size * scale;
        }

        private void HideImmediate()
        {
            if (backgroundImage != null)
                SetImageAlpha(backgroundImage, Color.black, 0f);

            if (logoImage != null)
            {
                SetImageAlpha(logoImage, Color.white, 0f);
                logoImage.gameObject.SetActive(false);
            }

            if (titleRoot != null)
                titleRoot.gameObject.SetActive(false);
        }

        private static IEnumerator FadeImage(Image image, Color color, float from, float to, float duration)
        {
            if (image == null)
                yield break;

            duration = Mathf.Max(0f, duration);
            if (duration <= 0f)
            {
                SetImageAlpha(image, color, to);
                yield break;
            }

            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float alpha = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
                SetImageAlpha(image, color, alpha);
                yield return null;
            }

            SetImageAlpha(image, color, to);
        }

        private static IEnumerator Wait(float duration)
        {
            duration = Mathf.Max(0f, duration);
            if (duration > 0f)
                yield return new WaitForSeconds(duration);
        }

        private static void SetImageAlpha(Image image, Color color, float alpha)
        {
            if (image == null)
                return;

            image.color = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
        }

        private static void StretchFull(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
