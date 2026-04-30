using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using Communication;
using MessageWindowSystem.Core;
using MessageWindowSystem.Testing;
using TMPro;

/// <summary>
/// Manages communication start/end UI transitions.
/// Portrait click behavior is differentiated by current GamePhase.
/// 判定ロジックは ComuLogic に委譲し、本クラスは UI 操作に専念する。
///
/// Portrait の OnClick にバインドされていた形状変更処理（MoveOnClickandReturn.Play 群、
/// スプライト変更、フォントサイズ変更）を本クラスに集約。
/// シーン上の OnClick は ToggleComuforPortrait() の呼び出しのみで動作する。
/// </summary>
public class ComuStartandEndManager : MonoBehaviour
{
    [SerializeField] private GameObject comuStartUI;
    [SerializeField] private GameObject comuEndUI;
    [SerializeField] private GameObject desk;
    [SerializeField] private GameObject messageWindow;
    [SerializeField] private GameObject messageWindowBackGround;
    [SerializeField] private GameObject NamePlate;
    [SerializeField] private GameObject NamePlateBackGround;
    [SerializeField] private Image fadeFrame;
    [SerializeField] private GameObject Memorizer;
    [SerializeField] private GameObject LostNote;
    [SerializeField] private GameObject ToggleEffect;
    [SerializeField] private GameObject ObjectiveDisplay;

    [SerializeField] private GameObject Portrait;
    [Tooltip("Overlay displayed when portrait is unclickable in scenario")]
    [SerializeField] private GameObject unclickableOverlay;
    [Tooltip("SE played when clicking portrait while it's unclickable")]
    [SerializeField] private AudioClip unclickableSE;
    
    [SerializeField] private MessageWindowIndexStarter messageWindowIndexStarter;

    [Header("Scenario IDs")]
    [Tooltip("If true, uses ProgressManager to generate scenario IDs automatically.")]
    [SerializeField] private bool useProgressBasedId = true;
    [Tooltip("Manual start ID (used if useProgressBasedId is false).")]
    [SerializeField] private string startScenarioId;
    [Tooltip("Manual end ID (used if useProgressBasedId is false).")]
    [SerializeField] private string endScenarioId;

    [Header("Shape Animators (元 Portrait OnClick バインド)")]
    [Tooltip("Portrait クリック時にトグルする MoveOnClickandReturn の一覧。\nシーン上の OnClick に直接バインドされていたものをここに設定する。")]
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
    [SerializeField] private float comuFontSize = 70f;

    private readonly ComuLogic _logic = new ComuLogic();

    /// <summary>通常モード（探索モード）時のフォントサイズ。Awake で記録する。</summary>
    private float _originalFontSize;
    /// <summary>通常モード時のスプライト。Awake で記録する。</summary>
    private Sprite _originalSprite;

    public bool IsInCommunication => _logic.IsInCommunication;

    private void Awake()
    {
        // 初期値を記録（End 時の復帰用）
        if (fontSizeTarget != null)
            _originalFontSize = fontSizeTarget.fontSize;
        if (portraitSpriteTarget != null)
            _originalSprite = portraitSpriteTarget.sprite;
    }

    public void ComuStart(string scenarioId) => StartComuFlow(scenarioId).Forget();
    public void ComuEnd(string scenarioId) => EndComuFlow(scenarioId).Forget();

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

    /// <summary>
    /// Portrait クリック時のメイン処理。
    /// ComuLogic に判定を委譲し、結果に応じた UI 操作を行う。
    /// allowAnimation = true の場合、形状変更アニメーション（MoveOnClickandReturn.Play）も実行する。
    /// allowAnimation = false の場合、形状を即座に変更する。
    /// </summary>
    public void ToggleComuforPortrait(bool allowAnimation = true)
    {
        var result = _logic.JudgeToggle();

        switch (result)
        {
            case ComuLogic.ToggleResult.Blocked:
                return;

            case ComuLogic.ToggleResult.PlayUnclickableSE:
                if (unclickableSE != null)
                    EffectManager.Instance?.PlaySE(unclickableSE);
                return;

            case ComuLogic.ToggleResult.EndCommunication:
                string endId = GetEndScenarioId();
                // 形状変更（探索モードへ）
                ApplyShapeToggle(allowAnimation);
                if (allowAnimation)
                    ComuEnd(endId);
                else
                    ComuEndTask(endId, allowAnimation: false).Forget();
                _logic.IsInCommunication = false;
                return;

            case ComuLogic.ToggleResult.StartCommunication:
                var pm = ProgressManager.Instance;
                string scenarioId;
                if (pm == null)
                {
                    Debug.LogWarning("[ComuManager] ProgressManager not found. Using fallback ID.");
                    scenarioId = startScenarioId;
                }
                else
                {
                    var info = ComuLogic.ResolveScenarioId(pm.CurrentChapter, pm.CurrentPhase);
                    Debug.Log($"[ComuManager] ToggleComuforPortrait: Phase={pm.CurrentPhase}, Scenario={info.ScenarioId}, Keywords={info.EnableKeywords}");
                    scenarioId = info.ScenarioId;
                }

                // 形状変更（コミュニケーションモードへ）
                ApplyShapeToggle(allowAnimation);
                if (allowAnimation)
                    ComuStart(scenarioId);
                else
                    ComuStartTask(scenarioId, allowAnimation: false).Forget();
                _logic.IsInCommunication = true;
                return;
        }
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
            _logic.IsInCommunication = false;
        }
        else
        {
            ComuStart(startId);
            _logic.IsInCommunication = true;
        }
    }

    private string GetEndScenarioId()
    {
        if (useProgressBasedId && ProgressManager.Instance != null)
            return ComuLogic.ResolveEndScenarioId(ProgressManager.Instance.CurrentChapter);
        return endScenarioId;
    }

    #region Shape Toggle

    /// <summary>
    /// shapeAnimators の全 MoveOnClickandReturn をトグルし、
    /// スプライト・フォントサイズも切り替える。
    /// allowAnimation = true: Play() でアニメーション付きトグル。
    /// allowAnimation = false: SetToTarget() / SetToOriginal() で即座にトグル。
    /// </summary>
    private void ApplyShapeToggle(bool allowAnimation)
    {
        if (shapeAnimators != null)
        {
            foreach (var animator in shapeAnimators)
            {
                if (animator == null) continue;

                if (allowAnimation)
                {
                    animator.Play();
                }
                else
                {
                    // Play() のトグル動作を再現: 未移動→ターゲット、移動済→元に戻す
                    if (!animator.isMoved)
                        animator.SetToTarget();
                    else
                        animator.SetToOriginal();
                }
            }
        }

        // スプライト切り替え
        if (portraitSpriteTarget != null && comuSprite != null)
        {
            // IsInCommunication はまだ更新前なので、false = これから Start（comuSprite を設定）
            if (!_logic.IsInCommunication)
                portraitSpriteTarget.sprite = comuSprite;
            else
                portraitSpriteTarget.sprite = _originalSprite;
        }

        // フォントサイズ切り替え
        if (fontSizeTarget != null)
        {
            if (!_logic.IsInCommunication)
                fontSizeTarget.fontSize = comuFontSize;
            else
                fontSizeTarget.fontSize = _originalFontSize;
        }
    }

    #endregion

    #region UniTask API (for FlowSteps)

    public async UniTask ComuStartTask(string scenarioId, bool allowAnimation = true)
    {
        if (allowAnimation)
        {
            await StartComuFlow(scenarioId);
        }
        else
        {
            SetPortraitInteractable(false);
            if (unclickableOverlay != null) unclickableOverlay.SetActive(false);
            
            // アニメーション中の DOTween を停止し、即座に最終状態へ
            SetDeskInstant();

            fadeFrame.gameObject.SetActive(false);
            // fadeFrame の MoveOnClickandReturn 状態もリセット
            if (fadeFrame.TryGetComponent<MoveOnClickandReturn>(out var fadeAnim))
                fadeAnim.SetToOriginal();

            NamePlate.SetActive(true);
            messageWindow.SetActive(true);
            messageWindowBackGround.SetActive(true);
            NamePlateBackGround.SetActive(true);
            ObjectiveDisplay.SetActive(true);
            Memorizer.SetActive(false); 
            LostNote.SetActive(false);
            ToggleEffect.SetActive(false);
            if (Portrait != null) Portrait.SetActive(true);
            
            SetPortraitInteractable(true);

            if (!string.IsNullOrEmpty(scenarioId))
                messageWindowIndexStarter.StartScenarioById(scenarioId);
        }
    }

    public async UniTask ComuEndTask(string scenarioId, bool allowAnimation = true)
    {
        if (allowAnimation)
        {
            await EndComuFlow(scenarioId);
        }
        else
        {
            SetPortraitInteractable(false);
            if (unclickableOverlay != null) unclickableOverlay.SetActive(false);
            
            // アニメーション中の DOTween を停止し、即座に最終状態へ
            SetDeskInstant();

            NamePlate.SetActive(false);
            messageWindow.SetActive(false);
            messageWindowBackGround.SetActive(false);
            NamePlateBackGround.SetActive(false);
            ObjectiveDisplay.SetActive(true);
            fadeFrame.gameObject.SetActive(false);
            // fadeFrame の MoveOnClickandReturn 状態もリセット
            if (fadeFrame.TryGetComponent<MoveOnClickandReturn>(out var fadeAnim))
                fadeAnim.SetToOriginal();

            ToggleEffect.SetActive(false);
            if (Portrait != null) Portrait.SetActive(true);
            
            Memorizer.SetActive(true);
            LostNote.SetActive(true);
            
            SetPortraitInteractable(true);

            if (!string.IsNullOrEmpty(scenarioId))
                messageWindowIndexStarter.StartScenarioById(scenarioId);
        }
    }

    #endregion

    #region Animation Flows

    private async UniTask StartComuFlow(string Startid)
    {
        _logic.IsAnimating = true;
        SetPortraitInteractable(false);
        if (unclickableOverlay != null) unclickableOverlay.SetActive(false);

        var fadeAnim = fadeFrame.GetComponent<MoveOnClickandReturn>();
        fadeFrame.gameObject.SetActive(true);
        NamePlate.SetActive(false);
        messageWindow.SetActive(false);
        messageWindowBackGround.SetActive(false);
        NamePlateBackGround.SetActive(false);
        Memorizer.SetActive(false);
        LostNote.SetActive(false);
        ObjectiveDisplay.SetActive(false);
        ToggleEffect.SetActive(true);

        PlayDeskAnimation();
        fadeAnim.Play();

        await UniTask.Delay(1000);

        var startPanel = comuStartUI.GetComponent<MoveOnClickandReturn>();
        startPanel.Play();

        await UniTask.Delay(1500);

        startPanel.Play();
        PlayDeskAnimation();

        await UniTask.Delay(500);

        NamePlate.SetActive(true);
        messageWindow.SetActive(true);
        messageWindowBackGround.SetActive(true);
        NamePlateBackGround.SetActive(true);
        ObjectiveDisplay.SetActive(true);
        ToggleEffect.SetActive(false);
        if (Portrait != null) Portrait.SetActive(true);

        fadeFrame.gameObject.SetActive(false);

        _logic.IsAnimating = false;
        SetPortraitInteractable(true);

        if (!string.IsNullOrEmpty(Startid))
            messageWindowIndexStarter.StartScenarioById(Startid);
    }

    private async UniTask EndComuFlow(string Endid)
    {
        _logic.IsAnimating = true;
        SetPortraitInteractable(false);
        if (unclickableOverlay != null) unclickableOverlay.SetActive(false);

        var endPanel = comuEndUI.GetComponent<MoveOnClickandReturn>();
        var fadeAnim = fadeFrame.GetComponent<MoveOnClickandReturn>();
        endPanel.Play();
        NamePlate.SetActive(false);
        messageWindow.SetActive(false);
        messageWindowBackGround.SetActive(false);
        NamePlateBackGround.SetActive(false);
        ObjectiveDisplay.SetActive(false);
        fadeFrame.gameObject.SetActive(true);
        ToggleEffect.SetActive(true);
        fadeAnim.Play();

        await UniTask.Delay(1500);

        endPanel.Play();

        await UniTask.Delay(500);
        NamePlate.SetActive(true);
        messageWindow.SetActive(true);
        messageWindow.GetComponent<TMP_Text>().fontSize = 45;
        messageWindowBackGround.SetActive(true);
        NamePlateBackGround.SetActive(true);
        fadeFrame.gameObject.SetActive(false);
        ObjectiveDisplay.SetActive(true);
        Memorizer.SetActive(true);
        LostNote.SetActive(true);
        ToggleEffect.SetActive(false);
        if (Portrait != null) Portrait.SetActive(true);

        _logic.IsAnimating = false;
        SetPortraitInteractable(true);

        if (!string.IsNullOrEmpty(Endid))
            messageWindowIndexStarter.StartScenarioById(Endid);
    }

    #endregion

    #region Utility

    private void PlayDeskAnimation()
    {
        if (desk.TryGetComponent<MoveOnClickandReturn>(out var move)) move.Play();
        else if (desk.TryGetComponent<FirstMove>(out var first)) first.Play();
    }

    /// <summary>
    /// desk の MoveOnClickandReturn をアニメーションなしで元の位置に設定する。
    /// アニメーション版では Play() が2回呼ばれて往復するため、最終的に元の位置に戻る。
    /// </summary>
    private void SetDeskInstant()
    {
        if (desk.TryGetComponent<MoveOnClickandReturn>(out var move))
        {
            if (move.isMoved)
                move.SetToOriginal();
        }
    }

    public void SetPortraitInteractable(bool interactable, bool updateOverlay = false)
    {
        _logic.IsPortraitInteractable = interactable;

        // Button自体は常にinteractableにしてクリック入力を受け付ける。ToggleComuforPortrait内で弾く。
        if (Portrait != null && Portrait.TryGetComponent<Button>(out var btn))
            btn.interactable = true;

        if (updateOverlay && unclickableOverlay != null)
        {
            unclickableOverlay.SetActive(!interactable);
        }
    }

    #endregion
}
