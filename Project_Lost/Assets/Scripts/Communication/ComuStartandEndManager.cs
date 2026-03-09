using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using MessageWindowSystem.Core;
using MessageWindowSystem.Testing;
using TMPro;

/// <summary>
/// Manages communication start/end UI transitions.
/// Portrait click behavior is differentiated by current GamePhase.
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

    private bool _isAnimating = false;
    private bool _isPortraitInteractable = true;
    private bool _isInCommunication = false;

    public bool IsInCommunication => _isInCommunication;

    public void ComuStart(string scenarioId) => StartComuFlow(scenarioId).Forget();
    public void ComuEnd(string scenarioId) => EndComuFlow(scenarioId).Forget();

    /// <summary>
    /// Wrapper for ToggleComuforPortrait. Can be called from Button.onClick.
    /// </summary>
    public void ToggleComu()
    {
        ToggleComuforPortrait();
    }

    /// <summary>
    /// Portrait クリック時のメイン処理。
    /// 現在の GamePhase に応じてシナリオIDを生成し、適切な処理を分岐する。
    /// 
    /// - Dialogue:     Ch{N}_Dialogue を再生
    /// - Extraction:   Ch{N}_Extraction を再生（キーワード有効、繰り返し閲覧可）
    /// - Presentation: Ch{N}_Presentation を再生
    /// - その他:       GetScenarioKey() で汎用対応
    /// </summary>
    public void ToggleComuforPortrait()
    {
        if (_isAnimating) return;

        // もしPortraitがクリック不可状態ならSEを鳴らして処理を中断
        if (!_isPortraitInteractable)
        {
            if (unclickableSE != null)
            {
                // EffectManagerへの依存があるため、Sceneに存在すれば鳴らす
                if (EffectManager.Instance != null)
                {
                    EffectManager.Instance.PlaySE(unclickableSE);
                }
            }
            return;
        }

        if (_isInCommunication)
        {
            // 対話終了 → 探索モードへ戻る
            string endId = GetEndScenarioId();
            ComuEnd(endId);
            _isInCommunication = false;
            return;
        }

        // ---- 対話開始: フェーズに応じた処理 ----
        var pm = ProgressManager.Instance;
        if (pm == null)
        {
            Debug.LogWarning("[ComuManager] ProgressManager not found. Using fallback ID.");
            ComuStart(startScenarioId);
            _isInCommunication = true;
            return;
        }

        int chapter = pm.CurrentChapter;
        GamePhase phase = pm.CurrentPhase;
        string scenarioId;
        bool enableKeywords = false;

        switch (phase)
        {
            case GamePhase.Dialogue:
                scenarioId = $"Ch{chapter}_Dialogue";
                break;

            case GamePhase.Extraction:
                scenarioId = $"Ch{chapter}_Extraction";
                enableKeywords = true;
                break;

            case GamePhase.Presentation:
                scenarioId = $"Ch{chapter}_Presentation";
                break;

            default:
                // 汎用: 現在のフェーズ名でシナリオIDを生成
                scenarioId = pm.GetScenarioKey();
                break;
        }

        Debug.Log($"[ComuManager] ToggleComuforPortrait: Phase={phase}, Scenario={scenarioId}, Keywords={enableKeywords}");

        // キーワード設定を反映
        if (MessageWindowManager.Instance != null)
        {
            // KeywordHandler will be initialized when StartScenario is called
            // but we can pre-set keyword enablement
        }

        ComuStart(scenarioId);
        _isInCommunication = true;
    }

    /// <summary>
    /// Overload with explicit IDs (for script calls).
    /// </summary>
    public void ToggleComu(string startId, string endId)
    {
        if (_isAnimating) return;

        if (_isInCommunication)
        {
            ComuEnd(endId);
            _isInCommunication = false;
        }
        else
        {
            ComuStart(startId);
            _isInCommunication = true;
        }
    }

    private string GetEndScenarioId()
    {
        if (useProgressBasedId && ProgressManager.Instance != null)
            return $"Ch{ProgressManager.Instance.CurrentChapter}_loop";
        return endScenarioId;
    }

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
            
            fadeFrame.gameObject.SetActive(false);
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
            
            NamePlate.SetActive(false);
            messageWindow.SetActive(false);
            messageWindowBackGround.SetActive(false);
            NamePlateBackGround.SetActive(false);
            ObjectiveDisplay.SetActive(true);
            fadeFrame.gameObject.SetActive(false);
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
        _isAnimating = true;
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

        _isAnimating = false;
        SetPortraitInteractable(true);

        if (!string.IsNullOrEmpty(Startid))
            messageWindowIndexStarter.StartScenarioById(Startid);
    }

    private async UniTask EndComuFlow(string Endid)
    {
        _isAnimating = true;
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

        _isAnimating = false;
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

    public void SetPortraitInteractable(bool interactable, bool updateOverlay = false)
    {
        _isPortraitInteractable = interactable;

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
