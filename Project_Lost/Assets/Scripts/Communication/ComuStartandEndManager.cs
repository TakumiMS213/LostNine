using System;
using System.Threading;
using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using Communication;
using MessageWindowSystem.Core;
using MessageWindowSystem.Testing;
using ScenarioSystem.View;
using ScenarioSystem.Adapter;
using TMPro;

/// <summary>
/// Manages communication start/end UI transitions.
/// シナリオで表示する対話ボタンから、現在の GamePhase に応じた会話を開始する。
/// 判定ロジックは ComuLogic に委譲し、本クラスは UI 操作に専念する。
///
/// Portrait の OnClick にバインドされていた形状変更処理（MoveOnClickandReturn.Play 群、
/// スプライト変更、フォントサイズ変更）を本クラスに集約。
/// 対話ボタンの OnClick は ToggleComuFromButton() に接続する。
/// </summary>
public class ComuStartandEndManager : MonoBehaviour
{
    [SerializeField] private GameObject comuStartUI;
    [SerializeField] private GameObject comuEndUI;
    [SerializeField] private GameObject desk;
    [SerializeField] private GameObject messageWindow;
    [SerializeField] private GameObject messageWindowBackGround;
    [SerializeField] private SpeechBubbleView speechBubble;
    [SerializeField] private SpeakerNameView speakerNameView;
    [SerializeField] private Vector2 comuSpeakerNamePosition = new Vector2(40f, 20f);
    [SerializeField] private Color comuWindowColor = new Color(0.16078432f, 0.16078432f, 0.16078432f, 0.8f);
    [SerializeField] private GameObject NamePlate;
    [SerializeField] private GameObject NamePlateBackGround;
    [SerializeField] private Image fadeFrame;
    [SerializeField] private GameObject Memorizer;
    [SerializeField] private GameObject LostNote;
    [SerializeField] private GameObject ToggleEffect;
    [SerializeField] private GameObject ObjectiveDisplay;

    [Header("Background Animations")]
    [Tooltip("会話終了時に Play() を起動するテキスト背景 GameObject")]
    [SerializeField] private GameObject backGround_Text;
    [Tooltip("会話終了時に Play() を起動するスピーカー名背景 GameObject")]
    [SerializeField] private GameObject backGround_SpeakerName;

    [SerializeField] private GameObject Portrait;

    [Header("Dialogue Button")]
    [Tooltip("Portrait の前面に配置する、シナリオから表示を指定する対話ボタン。")]
    [SerializeField] private Button dialogueStartButton;
    [SerializeField] private TMP_Text dialogueStartButtonLabel;

    [Tooltip("Overlay displayed when portrait is unclickable in scenario")]
    [SerializeField] private GameObject unclickableOverlay;
    [Tooltip("SE played when clicking portrait while it's unclickable")]
    [SerializeField] private AudioClip unclickableSE;

    [Header("Portrait Guidance")]
    [SerializeField] private Sprite portraitGuidanceSprite;
    [SerializeField] private Vector2 portraitGuidanceSize = new Vector2(216f, 216f);
    [SerializeField] private Vector2 portraitGuidanceOffset = new Vector2(0f, -40f);
    [SerializeField] private Color portraitGuidanceColor = Color.yellow;
    
    [SerializeField] private MessageWindowIndexStarter messageWindowIndexStarter;

    [Header("Scenario IDs")]
    [Tooltip("If true, uses ProgressManager to generate scenario IDs automatically.")]
    [SerializeField] private bool useProgressBasedId = true;
    [Tooltip("Manual start ID (used if useProgressBasedId is false).")]
    [SerializeField] private string startScenarioId;
    [Tooltip("Manual end ID (used if useProgressBasedId is false).")]
    [SerializeField] private string endScenarioId;

    [Header("Shape Animators (元 Portrait OnClick バインド)")]
    [Tooltip("会話状態の切り替え時に動かす MoveOnClickandReturn の一覧。")]
    [SerializeField] private MoveOnClickandReturn[] shapeAnimators;

    [Header("Portrait Sprite")]
    [Tooltip("コミュニケーション切り替え時にスプライトを変更する Image。")]
    [SerializeField] private Image portraitSpriteTarget;
    [Tooltip("コミュニケーション開始時のスプライト。")]
    [SerializeField] private Sprite comuSprite;

    [Header("Font Size")]
    [Tooltip("コミュニケーション切り替え時にフォントサイズを変更する TMP_Text。")]
    [SerializeField] private TMP_Text fontSizeTarget;
    [Tooltip("コミュニケーション開始時のフォントサイズ。")]
    [SerializeField] private float comuFontSize = 63f;

    private readonly ComuLogic _logic = new ComuLogic();

    /// <summary>通常モード（探索モード）時のフォントサイズ。Awake で記録する。</summary>
    private float _originalFontSize;
    private Vector4 _originalTextMargin;
    private Color _originalTextColor;
    /// <summary>通常モード時のスプライト。Awake で記録する。</summary>
    private Sprite _originalSprite;
    private Color _originalWindowColor;
    private Vector2 _originalSpeakerNamePosition;
    private GameObject _portraitGuidanceObject;
    private CancellationTokenSource _transitionCancellation;
    private int _transitionVersion;
    private bool _dialogueStartButtonRequested;

    public bool IsInCommunication => _logic.IsInCommunication;

    private void Awake()
    {
        // 初期値を記録（End 時の復帰用）
        if (fontSizeTarget != null)
        {
            _originalFontSize = fontSizeTarget.fontSize;
            _originalTextMargin = fontSizeTarget.margin;
            _originalTextColor = fontSizeTarget.color;
        }
        if (portraitSpriteTarget != null)
        {
            _originalSprite = portraitSpriteTarget.sprite;
            _originalWindowColor = portraitSpriteTarget.color;
        }
        if (NamePlate != null && NamePlate.transform is RectTransform nameRect)
            _originalSpeakerNamePosition = nameRect.anchoredPosition;

        RefreshDialogueStartButton();
    }

    // ── Unity Lifecycle ──────────────────────────────────────────

    private bool _subscribedToThreshold = false;

    private void OnEnable()
    {
        SubscribeThresholdEvent();
        RefreshDialogueStartButton();
    }

    private void Start()
    {
        // OnEnable 時に ProgressManager.Instance がまだ null だった場合のフォールバック
        SubscribeThresholdEvent();

        // シーン再読込時にすでに達成済みなら即有効化
        if (ProgressManager.Instance != null && ProgressManager.Instance.AllKeywordsCollected)
            ActivateMemorizer();
    }

    private void OnDisable()
    {
        _transitionVersion++;
        _transitionCancellation?.Cancel();
        RefreshDialogueStartButton();
        if (ProgressManager.Instance != null && _subscribedToThreshold)
        {
            ProgressManager.Instance.OnKeywordThresholdReached -= ActivateMemorizer;
            _subscribedToThreshold = false;
        }
    }

    private void SubscribeThresholdEvent()
    {
        if (_subscribedToThreshold) return;
        if (ProgressManager.Instance == null) return;

        ProgressManager.Instance.OnKeywordThresholdReached += ActivateMemorizer;
        _subscribedToThreshold = true;
    }

    /// <summary>キーワードを3つ発見した時点でメモライザーを有効化する。</summary>
    private void ActivateMemorizer()
    {
        if (Memorizer != null)
        {
            Debug.Log("[ComuStartandEndManager] ActivateMemorizer: Memorizer.SetActive(true)");
            Memorizer.SetActive(true);
        }
    }

    public void ComuStart(string scenarioId) => ComuStartTask(scenarioId).Forget();
    public void ComuEnd(string scenarioId) => ComuEndTask(scenarioId).Forget();

    /// <summary>表示要求と操作ロックは別々に保持し、シナリオ終了時に非表示指定を上書きしない。</summary>
    public void SetDialogueStartButtonVisible(bool visible)
    {
        _dialogueStartButtonRequested = visible;
        RefreshDialogueStartButton();
    }

    /// <summary>Portrait 本体のクリックではなく、対話ボタンだけが受け付ける入力。</summary>
    public void ToggleComuFromButton()
    {
        if (!_dialogueStartButtonRequested || !isActiveAndEnabled
            || dialogueStartButton == null || !dialogueStartButton.isActiveAndEnabled
            || !dialogueStartButton.IsInteractable())
            return;

        ToggleComuInternal(true, ignorePortraitLock: false);
    }

    private void RefreshDialogueStartButton()
    {
        if (dialogueStartButton == null) return;

        bool visible = _dialogueStartButtonRequested && isActiveAndEnabled
            && _logic.IsPortraitInteractable && !_logic.IsAnimating;
        dialogueStartButton.interactable = visible;
        dialogueStartButton.gameObject.SetActive(visible);
        if (dialogueStartButtonLabel != null)
            dialogueStartButtonLabel.text = IsInCommunication ? "対話終了" : "対話開始";
    }

    /// <summary>
    /// Wrapper for ToggleComuforPortrait. Can be called from Button.onClick.
    /// アニメーション付きでトグルする。
    /// </summary>
    public void ToggleComu()
    {
        ToggleComuforPortrait(true);
    }

    /// <summary>
    /// アニメーションなしで即座にトグルする。Action やコードから呼び出す。
    /// </summary>
    public void ToggleComuInstant()
    {
        ToggleComuforPortrait(false);
    }

    public void ToggleComuFromScenario(bool allowAnimation = true)
    {
        ToggleComuInternal(allowAnimation, ignorePortraitLock: true);
    }

    /// <summary>
    /// Portrait クリック時のメイン処理。
    /// ComuLogic に判定を委譲し、結果に応じた UI 操作を行う。
    /// allowAnimation = true の場合、形状変更アニメーション（MoveOnClickandReturn.Play）も実行する。
    /// allowAnimation = false の場合、形状を即座に変更する。
    /// </summary>
    public void ToggleComuforPortrait(bool allowAnimation = true)
    {
        ToggleComuInternal(allowAnimation, ignorePortraitLock: false);
    }

    private void ToggleComuInternal(bool allowAnimation, bool ignorePortraitLock)
    {
        var result = ignorePortraitLock
            ? JudgeToggleIgnoringPortraitLock()
            : _logic.JudgeToggle();

        switch (result)
        {
            case ComuLogic.ToggleResult.Blocked:
                return;

            case ComuLogic.ToggleResult.PlayUnclickableSE:
                if (unclickableSE != null)
                    EffectManager.Instance?.PlaySE(unclickableSE);
                return;

            case ComuLogic.ToggleResult.EndCommunication:
                EndCommunicationFromPortrait(allowAnimation).Forget();
                return;

            case ComuLogic.ToggleResult.StartCommunication:
                ComuStartTask(GetStartScenarioId(), allowAnimation).Forget();
                return;
        }
    }

    private ComuLogic.ToggleResult JudgeToggleIgnoringPortraitLock()
    {
        if (_logic.IsAnimating)
            return ComuLogic.ToggleResult.Blocked;

        return _logic.IsInCommunication
            ? ComuLogic.ToggleResult.EndCommunication
            : ComuLogic.ToggleResult.StartCommunication;
    }

    /// <summary>
    /// Overload with explicit IDs (for script calls).
    /// </summary>
    public void ToggleComu(string startId, string endId)
    {
        if (_logic.IsAnimating) return;

        if (_logic.IsInCommunication)
        {
            ComuEnd(endId);
        }
        else
        {
            ComuStart(startId);
        }
    }

    private string GetStartScenarioId()
    {
        if (useProgressBasedId && ProgressManager.Instance != null)
            return ComuLogic.ResolveScenarioId(ProgressManager.Instance.CurrentChapter,
                ProgressManager.Instance.CurrentPhase).ScenarioId;
        return startScenarioId;
    }

    private async UniTask EndCommunicationFromPortrait(bool allowAnimation)
    {
        string scenarioId = GetEndScenarioId();
        var transition = ComuEndTask(scenarioId, allowAnimation);
        int version = _transitionVersion;
        await transition;
        if (this != null && isActiveAndEnabled && version == _transitionVersion && string.IsNullOrEmpty(scenarioId))
            MessageWindowFacade.Instance?.StopScenario();
    }

    private string GetEndScenarioId()
    {
        if (useProgressBasedId && ProgressManager.Instance != null)
        {
            string id = ComuLogic.ResolveEndScenarioId(ProgressManager.Instance.CurrentChapter);
            return MessageWindowFacade.Instance != null && MessageWindowFacade.Instance.HasScenario(id)
                ? id : null;
        }
        return endScenarioId;
    }

    #region Shape Toggle

    /// <summary>
    /// UI形状を指定された会話状態へ確定する。
    /// アニメーション時も現在位置を確認し、同じ状態への再適用で反転させない。
    /// </summary>
    private void ApplyShapeState(bool isInCommunication, bool allowAnimation)
    {
        if (shapeAnimators != null)
        {
            foreach (var animator in shapeAnimators)
            {
                if (animator == null) continue;

                if (allowAnimation)
                {
                    if (animator.isMoved != isInCommunication)
                        animator.Play();
                }
                else if (isInCommunication)
                    animator.SetToTarget();
                else
                    animator.SetToOriginal();
            }
        }

        if (portraitSpriteTarget != null && comuSprite != null)
            portraitSpriteTarget.sprite = isInCommunication ? comuSprite : _originalSprite;

        if (speechBubble != null)
        {
            speechBubble.SetConversationMode(isInCommunication);
            if (portraitSpriteTarget != null)
                portraitSpriteTarget.color = isInCommunication ? comuWindowColor : _originalWindowColor;
            if (fontSizeTarget != null)
            {
                fontSizeTarget.margin = isInCommunication ? Vector4.zero : _originalTextMargin;
                fontSizeTarget.color = isInCommunication ? Color.white : _originalTextColor;
            }
        }

        if (speakerNameView != null)
            speakerNameView.SetRestingPosition(isInCommunication
                ? comuSpeakerNamePosition : _originalSpeakerNamePosition);

        if (fontSizeTarget != null)
            fontSizeTarget.fontSize = isInCommunication ? comuFontSize : _originalFontSize;
    }

    #endregion

    #region UniTask API (for FlowSteps)

    public UniTask ComuStartTask(string scenarioId, bool allowAnimation = true)
        => SetCommunicationState(true, scenarioId, allowAnimation);

    public UniTask ComuEndTask(string scenarioId, bool allowAnimation = true)
        => SetCommunicationState(false, scenarioId, allowAnimation);

    private async UniTask SetCommunicationState(bool inCommunication, string scenarioId, bool allowAnimation)
    {
        // 全入口で状態と形状を同時に更新し、連打・FlowStepとの競合を防ぐ。
        if (_logic.IsAnimating) return;

        _transitionVersion++;
        _logic.IsAnimating = true;
        _logic.IsInCommunication = inCommunication;
        RefreshDialogueStartButton();
        var cancellation = new CancellationTokenSource();
        _transitionCancellation = cancellation;
        bool completed = false;
        try
        {
            ApplyShapeState(inCommunication, allowAnimation);
            if (unclickableOverlay != null) unclickableOverlay.SetActive(false);

            if (allowAnimation)
            {
                if (inCommunication) await StartComuFlow(cancellation.Token);
                else await EndComuFlow(cancellation.Token);
            }

            cancellation.Token.ThrowIfCancellationRequested();
            ApplyCompletedTransitionUI(inCommunication);
            if (!inCommunication && allowAnimation)
            {
                PlayWithChildren(backGround_Text);
                PlayWithChildren(backGround_SpeakerName);
            }
            completed = true;
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            // シーン離脱・無効化後に古いUIやシナリオを操作しない。
            if (this != null)
            {
                ApplyShapeState(inCommunication, false);
                ApplyCompletedTransitionUI(inCommunication);
            }
        }
        finally
        {
            _logic.IsAnimating = false;
            _transitionCancellation = null;
            cancellation.Dispose();
            RefreshDialogueStartButton();
        }

        if (!completed) return;

        // アニメーションのロックだけを解除する。シナリオ側の操作禁止指定は保持する。
        SetPortraitInteractable(_logic.IsPortraitInteractable, true);
        if (!string.IsNullOrEmpty(scenarioId) && messageWindowIndexStarter != null)
            messageWindowIndexStarter.StartScenarioById(scenarioId);
    }

    private void ApplyCompletedTransitionUI(bool inCommunication)
    {
        SetDeskInstant();
        ResetAnimation(comuStartUI);
        ResetAnimation(comuEndUI);
        if (fadeFrame != null)
        {
            ResetAnimation(fadeFrame.gameObject);
            fadeFrame.gameObject.SetActive(false);
        }
        SetActive(NamePlate, true);
        SetActive(messageWindow, true);
        SetActive(messageWindowBackGround, true);
        SetActive(NamePlateBackGround, true);
        SetActive(ObjectiveDisplay, true);
        SetActive(ToggleEffect, false);
        SetActive(Portrait, true);
        if (!inCommunication)
        {
            SetActive(Memorizer, true);
            SetActive(LostNote, true);
        }
    }

    #endregion

    #region Animation Flows

    private async UniTask StartComuFlow(CancellationToken cancellationToken)
    {
        SetActive(fadeFrame != null ? fadeFrame.gameObject : null, true);
        SetTransitionContentVisible(false);
        SetActive(ToggleEffect, true);
        PlayDeskAnimation();
        PlayAnimation(fadeFrame != null ? fadeFrame.gameObject : null);

        await UniTask.Delay(1000, cancellationToken: cancellationToken);
        PlayAnimation(comuStartUI);
        await UniTask.Delay(1500, cancellationToken: cancellationToken);
        PlayAnimation(comuStartUI);
        PlayDeskAnimation();
        await UniTask.Delay(500, cancellationToken: cancellationToken);
    }

    private async UniTask EndComuFlow(CancellationToken cancellationToken)
    {
        PlayAnimation(comuEndUI);
        SetTransitionContentVisible(false);
        SetActive(fadeFrame != null ? fadeFrame.gameObject : null, true);
        SetActive(ToggleEffect, true);
        PlayAnimation(fadeFrame != null ? fadeFrame.gameObject : null);

        await UniTask.Delay(1500, cancellationToken: cancellationToken);
        PlayAnimation(comuEndUI);
        await UniTask.Delay(500, cancellationToken: cancellationToken);
    }

    private void SetTransitionContentVisible(bool visible)
    {
        SetActive(NamePlate, visible);
        SetActive(messageWindow, visible);
        SetActive(messageWindowBackGround, visible);
        SetActive(NamePlateBackGround, visible);
        SetActive(ObjectiveDisplay, visible);
    }

    #endregion

    #region Utility

    private void PlayDeskAnimation()
    {
        if (desk == null) return;
        if (desk.TryGetComponent<MoveOnClickandReturn>(out var move)) move.Play();
        else if (desk.TryGetComponent<FirstMove>(out var first)) first.Play();
    }

    /// <summary>
    /// desk の MoveOnClickandReturn をアニメーションなしで元の位置に設定する。
    /// アニメーション版では Play() が2回呼ばれて往復するため、最終的に元の位置に戻る。
    /// </summary>
    private void SetDeskInstant()
    {
        if (desk == null) return;

        if (desk.TryGetComponent<MoveOnClickandReturn>(out var move))
        {
            if (move.isMoved)
                move.SetToOriginal();
        }
    }

    private static void PlayWithChildren(GameObject root)
    {
        if (root == null) return;
        foreach (var animator in root.GetComponentsInChildren<MoveOnClickandReturn>(true))
            animator.Play();
    }

    private static void SetActive(GameObject target, bool active)
    {
        if (target != null) target.SetActive(active);
    }

    private static void PlayAnimation(GameObject target)
    {
        if (target != null && target.TryGetComponent<MoveOnClickandReturn>(out var move))
            move.Play();
    }

    private static void ResetAnimation(GameObject target)
    {
        if (target != null && target.TryGetComponent<MoveOnClickandReturn>(out var move))
            move.SetToOriginal();
    }

    public void SetPortraitInteractable(bool interactable, bool updateOverlay = false)
    {
        _logic.IsPortraitInteractable = interactable;

        RefreshDialogueStartButton();

        if (updateOverlay && unclickableOverlay != null)
        {
            unclickableOverlay.SetActive(!interactable);
        }

    }

    public void SetPortraitGuidanceVisible(bool isVisible)
    {
        EnsurePortraitGuidance();

        if (_portraitGuidanceObject != null)
            _portraitGuidanceObject.SetActive(isVisible);
    }

    private void EnsurePortraitGuidance()
    {
        if (_portraitGuidanceObject != null || Portrait == null || portraitGuidanceSprite == null)
            return;

        var portraitRect = Portrait.transform as RectTransform;
        if (portraitRect == null)
            return;

        _portraitGuidanceObject = new GameObject("PortraitGuidance", typeof(RectTransform), typeof(Image), typeof(MessageWindowCaretIndicator));
        _portraitGuidanceObject.transform.SetParent(portraitRect, false);

        var rect = _portraitGuidanceObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 1f);
        rect.anchorMax = new Vector2(0.5f, 1f);
        rect.pivot = new Vector2(0.5f, 1f);
        rect.anchoredPosition = portraitGuidanceOffset;
        rect.sizeDelta = portraitGuidanceSize;

        var image = _portraitGuidanceObject.GetComponent<Image>();
        image.sprite = portraitGuidanceSprite;
        image.color = portraitGuidanceColor;
        image.raycastTarget = false;
        _portraitGuidanceObject.SetActive(false);
    }

    #endregion
}
