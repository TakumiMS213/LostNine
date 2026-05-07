using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ScenarioSystem.Adapter;
using ScenarioSystem.Events;
using ScenarioSystem.Presenter;
using ScenarioSystem.Presenter.Executors;
using ScenarioSystem.View;

namespace System_Script
{
    /// <summary>
    /// Builds a small runtime-only scenario window for the Memorize scene and
    /// auto-plays the current chapter's tuning scenario.
    /// </summary>
    public class MemorizeOverlayMessageWindow : MonoBehaviour
    {
        [Header("Scenario")]
        [SerializeField] private ScenarioDataDatabase scenarioDataDatabase;
        [SerializeField] private string scenarioIdFormat = "Ch{0}_Tuning";
        [SerializeField] private bool playOnStart = true;
        [SerializeField] private float delaySeconds;
        [SerializeField] private int maxWaitFrames = 10;

        [Header("Window")]
        [SerializeField] private float typingSpeed = 0.05f;
        [SerializeField] private TMP_FontAsset fontAsset;
        [SerializeField] private Sprite advanceIndicatorSprite;
        [SerializeField] private Vector2 advanceIndicatorSize = new Vector2(32f, 32f);
        [SerializeField] private Vector2 advanceIndicatorOffset = new Vector2(-44f, 34f);
        [SerializeField] private int sortingOrder = 100;

        private ScenarioPresenter _presenter;
        private GameObject _windowRoot;
        private TMP_Text _speakerNameText;
        private TMP_Text _messageText;

        private void Awake()
        {
            BuildWindow();
        }

        private void Start()
        {
            if (playOnStart)
                StartCoroutine(WaitAndPlay());
        }

        private void OnEnable()
        {
            ScenarioEventBus.OnOverlayRequested += HandleOverlayRequested;
            ScenarioEventBus.OnOverlayDismissed += HandleOverlayDismissed;
            ScenarioEventBus.OnWindowVisibilityChanged += HandleWindowVisibilityChanged;
        }

        private void OnDisable()
        {
            ScenarioEventBus.OnOverlayRequested -= HandleOverlayRequested;
            ScenarioEventBus.OnOverlayDismissed -= HandleOverlayDismissed;
            ScenarioEventBus.OnWindowVisibilityChanged -= HandleWindowVisibilityChanged;
        }

        private void BuildWindow()
        {
            var canvasObject = new GameObject("OverlayMessageWindow");
            canvasObject.transform.SetParent(transform, false);

            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;

            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;

            canvasObject.AddComponent<GraphicRaycaster>();

            var windowRoot = new GameObject("WindowRoot");
            windowRoot.transform.SetParent(canvasObject.transform, false);
            var rootRect = windowRoot.AddComponent<RectTransform>();
            rootRect.anchorMin = Vector2.zero;
            rootRect.anchorMax = Vector2.one;
            rootRect.offsetMin = Vector2.zero;
            rootRect.offsetMax = Vector2.zero;
            windowRoot.SetActive(false);
            _windowRoot = windowRoot;

            var blocker = CreateImage("ClickBlocker", windowRoot.transform, new Color(0f, 0f, 0f, 0f));
            Stretch(blocker.rectTransform);
            var button = blocker.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;

            var panel = CreateImage("MessagePanel", windowRoot.transform, new Color(0f, 0f, 0f, 0.68f));
            panel.raycastTarget = false;
            var panelRect = panel.rectTransform;
            panelRect.anchorMin = new Vector2(0.08f, 0.05f);
            panelRect.anchorMax = new Vector2(0.92f, 0.26f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;

            var speakerText = CreateText("SpeakerName", panel.transform, 34, TextAlignmentOptions.Left);
            _speakerNameText = speakerText;
            var speakerRect = speakerText.rectTransform;
            speakerRect.anchorMin = new Vector2(0.04f, 0.68f);
            speakerRect.anchorMax = new Vector2(0.96f, 0.94f);
            speakerRect.offsetMin = Vector2.zero;
            speakerRect.offsetMax = Vector2.zero;

            var dialogueText = CreateText("DialogueText", panel.transform, 42, TextAlignmentOptions.TopLeft);
            _messageText = dialogueText;
            var dialogueRect = dialogueText.rectTransform;
            dialogueRect.anchorMin = new Vector2(0.04f, 0.12f);
            dialogueRect.anchorMax = new Vector2(0.96f, 0.66f);
            dialogueRect.offsetMin = Vector2.zero;
            dialogueRect.offsetMax = Vector2.zero;

            CreateAdvanceIndicator(panel.transform);

            var dialogueView = canvasObject.AddComponent<DialogueView>();
            dialogueView.Configure(dialogueText, windowRoot, typingSpeed);
            button.onClick.AddListener(dialogueView.OnUserInput);

            var speakerView = canvasObject.AddComponent<SpeakerNameView>();
            speakerView.Configure(speakerText);

            _presenter = gameObject.AddComponent<ScenarioPresenter>();
            RegisterExecutors(_presenter);
        }

        private IEnumerator WaitAndPlay()
        {
            if (delaySeconds > 0f)
                yield return new WaitForSeconds(delaySeconds);

            var waited = 0;
            while (ProgressManager.Instance == null && waited < maxWaitFrames)
            {
                waited++;
                yield return null;
            }

            yield return null;
            PlayCurrentChapterScenario();
        }

        private void PlayCurrentChapterScenario()
        {
            if (ProgressManager.Instance == null)
            {
                Debug.LogWarning("[MemorizeOverlayMessageWindow] ProgressManager not found.");
                return;
            }

            if (scenarioDataDatabase == null)
            {
                Debug.LogWarning("[MemorizeOverlayMessageWindow] ScenarioDataDatabase is not assigned.");
                return;
            }

            var scenarioId = string.Format(scenarioIdFormat, ProgressManager.Instance.CurrentChapter);
            var scenario = scenarioDataDatabase.GetById(scenarioId);
            if (scenario == null)
            {
                Debug.LogWarning($"[MemorizeOverlayMessageWindow] Scenario '{scenarioId}' not found.");
                return;
            }

            Debug.Log($"[MemorizeOverlayMessageWindow] Auto-playing scenario: {scenarioId}");
            _presenter.StartScenario(scenario);
        }

        private void HandleOverlayRequested(OverlayEventData data)
        {
            if (_windowRoot != null)
                _windowRoot.SetActive(true);

            if (_speakerNameText != null)
                _speakerNameText.text = data.SpeakerName ?? string.Empty;

            if (_messageText != null)
                _messageText.text = data.Text ?? string.Empty;
        }

        private void HandleOverlayDismissed()
        {
            if (_speakerNameText != null)
                _speakerNameText.text = string.Empty;

            if (_messageText != null)
                _messageText.text = string.Empty;
        }

        private void HandleWindowVisibilityChanged(bool visible)
        {
            if (!visible && _windowRoot != null)
                _windowRoot.SetActive(false);
        }

        private void RegisterExecutors(ScenarioPresenter presenter)
        {
            presenter.RegisterExecutor(new DialogueActionExecutor());
            presenter.RegisterExecutor(new EffectActionExecutor());
            presenter.RegisterExecutor(new ChoiceActionExecutor());
            presenter.RegisterExecutor(new WaitActionExecutor(presenter));
            presenter.RegisterExecutor(new ProgressUpdateActionExecutor());
            presenter.RegisterExecutor(new OverlayActionExecutor(presenter));
            presenter.RegisterExecutor(new ProgressScenarioActionExecutor(presenter, scenarioDataDatabase));
            presenter.RegisterExecutor(new SceneTransitionActionExecutor());
        }

        private static Image CreateImage(string name, Transform parent, Color color)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var rect = obj.AddComponent<RectTransform>();
            rect.localScale = Vector3.one;
            var image = obj.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private TMP_Text CreateText(string name, Transform parent, float fontSize, TextAlignmentOptions alignment)
        {
            var obj = new GameObject(name);
            obj.transform.SetParent(parent, false);
            var text = obj.AddComponent<TextMeshProUGUI>();
            text.fontSize = fontSize;
            text.color = Color.white;
            text.alignment = alignment;
            text.enableWordWrapping = true;
            text.raycastTarget = false;
            if (fontAsset != null)
                text.font = fontAsset;
            return text;
        }

        private void CreateAdvanceIndicator(Transform parent)
        {
            if (advanceIndicatorSprite == null)
                return;

            var obj = new GameObject("AdvanceIndicator");
            obj.transform.SetParent(parent, false);

            var rect = obj.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 0f);
            rect.anchorMax = new Vector2(1f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = advanceIndicatorSize;
            rect.anchoredPosition = advanceIndicatorOffset;

            var image = obj.AddComponent<Image>();
            image.sprite = advanceIndicatorSprite;
            image.raycastTarget = false;
            image.preserveAspect = true;

            obj.AddComponent<MessageWindowCaretIndicator>();
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
