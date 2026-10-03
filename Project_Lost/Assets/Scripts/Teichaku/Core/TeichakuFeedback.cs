using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using DG.Tweening;
using Teichaku.Data;
using ScenarioSystem.Model;

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
        [SerializeField] private List<ChapterClearResult> chapterClearResults = new();
        [SerializeField] private Vector2 resultImageSize = new Vector2(750f, 500f);
        [SerializeField] private Vector2 resultTextOffset = new Vector2(0f, -330f);
        [SerializeField] private float resultTextFontSize = 80f;
        [SerializeField] private TMP_FontAsset resultTextFont;
        [SerializeField] private Color resultTextColor = Color.white;
        [SerializeField] private string resultHeaderText = "特定したなくしもの";
        [SerializeField] private Vector2 resultHeaderOffset = new Vector2(0f, 330f);
        [SerializeField] private float resultHeaderFontSizeMultiplier = 1.5f;

        [Tooltip("クリア画像の表示時間（秒）")]
        [SerializeField] private float clearImageDisplayDuration = 5f;

        [Tooltip("クリア画像のフェードイン時間（秒）")]
        [SerializeField] private float clearImageFadeInDuration = 0.3f;

        [Tooltip("クリア画像のフェードアウト時間（秒）")]
        [SerializeField] private float clearImageFadeOutDuration = 0.3f;

        [Header("結果の後光")]
        [SerializeField] private Color resultHaloColor = new Color(1f, 0.84f, 0.35f, 1f);
        [SerializeField] private Vector2 resultHaloSize = new Vector2(1050f, 780f);

        private CanvasGroup _resultGroup;
        private ResultHaloGraphic _resultHalo;
        private Coroutine _clearRoutine;
        private bool _isShowingClear;
        private TeichakuStageData _resultStageData;
        private Vector2 _gridInitialPos;
        private Image _resultImage;
        private TextMeshProUGUI _resultText;
        private TextMeshProUGUI _resultHeaderText;

        [Serializable]
        private class ChapterClearResult
        {
            public int chapter;
            public Sprite image;
            public string title;
        }

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
            EnsureResultViews();
            SetResultAlpha(0f);
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
            if (_isShowingClear) return;
            _isShowingClear = true;
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
            _clearRoutine = StartCoroutine(ClearSequence());
        }

        /// <summary>
        /// クリア演出シーケンス：画像表示 → シーン遷移
        /// </summary>
        private IEnumerator ClearSequence()
        {
            EnsureResultViews();
            var result = ResolveClearResult();

            if (result != null)
            {
                _resultImage.sprite = result.image;
                _resultImage.enabled = result.image != null;
                _resultHalo.gameObject.SetActive(result.image != null);
                _resultHeaderText.text = resultHeaderText;
                _resultText.text = result.title ?? string.Empty;
                SetResultAlpha(0f);

                _resultGroup.DOFade(1f, clearImageFadeInDuration).SetUpdate(true);
                _resultHalo.rectTransform.localRotation = Quaternion.identity;
                _resultHalo.rectTransform.localScale = Vector3.one;
                _resultHalo.rectTransform.DORotate(new Vector3(0f, 0f, 18f),
                    clearImageFadeInDuration + clearImageDisplayDuration).SetEase(Ease.Linear).SetUpdate(true);

                // フェード中もクリックでスキップ可能。クリア時のクリック・押しっぱなしは消費しない。
                var timer = new ResultDisplayTimer(clearImageFadeInDuration + clearImageDisplayDuration,
                    Input.GetMouseButton(0));
                yield return null;
                while (!timer.IsComplete)
                {
                    timer.Advance(Time.unscaledDeltaTime, Input.GetMouseButton(0), Input.GetMouseButtonDown(0));
                    if (!timer.IsComplete) yield return null;
                }

                _resultGroup.DOKill();
                _resultGroup.DOFade(0f, clearImageFadeOutDuration).SetUpdate(true);
                yield return new WaitForSecondsRealtime(clearImageFadeOutDuration);
                _resultHalo.rectTransform.DOKill();
                Debug.Log("[TeichakuFeedback] Clear result sequence completed.");
            }

            // ProgressをPresentationフェーズに変換
            var pm = ProgressManager.Instance;
            if (pm == null)
            {
                Debug.LogWarning("[TeichakuFeedback] ProgressManager not found.");
                _clearRoutine = null;
                yield break;
            }

            pm.SetProgress(pm.CurrentChapter, GamePhase.Presentation);

            // PresentationはStoryシーンで再生する
            string scenarioId = ScenarioKey.ForPhase(pm.CurrentChapter, GamePhase.Presentation);
            pm.StartProgressScenarioInStory(scenarioId);
            _clearRoutine = null;
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
            StopResultDisplay();
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

        public void SetResultStageData(TeichakuStageData stageData)
        {
            StopResultDisplay();
            _resultStageData = stageData;
        }

        private ChapterClearResult ResolveClearResult()
        {
            int chapter = ProgressManager.Instance != null ? ProgressManager.Instance.CurrentChapter : 0;
            var fallback = chapterClearResults?.Find(result => result != null && result.chapter == chapter);
            if (_resultStageData == null || (_resultStageData.lostThingImage == null
                && string.IsNullOrWhiteSpace(_resultStageData.lostThingName))) return fallback;
            return new ChapterClearResult
            {
                chapter = chapter,
                image = _resultStageData.lostThingImage != null ? _resultStageData.lostThingImage : fallback?.image,
                title = !string.IsNullOrWhiteSpace(_resultStageData.lostThingName)
                    ? _resultStageData.lostThingName : fallback?.title
            };
        }

        private void EnsureResultViews()
        {
            if (_resultGroup != null) return;
            Transform parent = clearImage != null && clearImage.transform.parent != null
                ? clearImage.transform.parent : transform;
            var root = new GameObject("ClearResult", typeof(RectTransform), typeof(CanvasGroup));
            root.layer = parent.gameObject.layer;
            root.transform.SetParent(parent, false);
            var rootRect = (RectTransform)root.transform;
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = rootRect.offsetMax = Vector2.zero;
            _resultGroup = root.GetComponent<CanvasGroup>();
            _resultGroup.interactable = false;
            _resultGroup.blocksRaycasts = false;
            _resultGroup.alpha = 0f;

            // 盤面を落ち着かせ、後光と結果画像を見やすくする。
            var backdrop = NewResultGraphic<Image>("ClearResultBackdrop", Vector2.zero, Vector2.zero);
            backdrop.rectTransform.anchorMin = Vector2.zero;
            backdrop.rectTransform.anchorMax = Vector2.one;
            backdrop.rectTransform.offsetMin = backdrop.rectTransform.offsetMax = Vector2.zero;
            backdrop.color = new Color(0f, 0f, 0f, 0.75f);

            // 作成順に、後光 → 画像 → 文字を重ねる。
            _resultHalo = NewResultGraphic<ResultHaloGraphic>("ClearResultHalo", resultHaloSize, Vector2.zero);
            _resultHalo.color = resultHaloColor;
            _resultImage = NewResultGraphic<Image>("ClearResultImage", resultImageSize, Vector2.zero);
            _resultImage.preserveAspect = true;
            _resultText = NewResultText("ClearResultText", new Vector2(1200f, 160f), resultTextOffset, resultTextFontSize);
            _resultHeaderText = NewResultText("ClearResultHeaderText", new Vector2(1400f, 180f),
                resultHeaderOffset, resultTextFontSize * resultHeaderFontSizeMultiplier);
            var hint = NewResultText("ClearResultSkipHint", new Vector2(360f, 60f),
                new Vector2(0f, -45f), resultTextFontSize * 0.35f);
            hint.rectTransform.anchorMin = hint.rectTransform.anchorMax = new Vector2(1f, 1f);
            hint.rectTransform.pivot = new Vector2(1f, 0.5f);
            hint.rectTransform.anchoredPosition = new Vector2(-35f, -45f);
            hint.text = "クリックでスキップ";
        }

        private T NewResultGraphic<T>(string objectName, Vector2 size, Vector2 position) where T : Graphic
        {
            var view = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(T));
            view.layer = _resultGroup.gameObject.layer;
            view.transform.SetParent(_resultGroup.transform, false);
            var rect = (RectTransform)view.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            T graphic = view.GetComponent<T>();
            graphic.raycastTarget = false;
            return graphic;
        }

        private TextMeshProUGUI NewResultText(string objectName, Vector2 size, Vector2 position, float fontSize)
        {
            var text = NewResultGraphic<TextMeshProUGUI>(objectName, size, position);
            text.alignment = TextAlignmentOptions.Center;
            text.fontSize = fontSize;
            if (resultTextFont != null) text.font = resultTextFont;
            text.color = resultTextColor;
            return text;
        }

        private void SetResultAlpha(float alpha)
        {
            if (_resultGroup != null) _resultGroup.alpha = alpha;
        }

        private void StopResultDisplay()
        {
            if (_clearRoutine != null) StopCoroutine(_clearRoutine);
            _clearRoutine = null;
            _isShowingClear = false;
            if (_resultGroup != null) _resultGroup.DOKill();
            if (_resultHalo != null) _resultHalo.rectTransform.DOKill();
            SetResultAlpha(0f);
        }

        private void OnDisable()
        {
            StopResultDisplay();
        }
    }
}
