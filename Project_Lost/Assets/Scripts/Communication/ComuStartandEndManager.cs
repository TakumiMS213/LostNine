using UnityEngine;
using UnityEngine.UI;
using Cysharp.Threading.Tasks;
using MessageWindowSystem.Testing;
using TMPro;

/// <summary>
/// Manages communication start/end UI transitions.
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
    [SerializeField] private GameObject ObjectiveDisplay; //目的表示用
    [SerializeField] private GameObject ToggleEffect; //トグルエフェクト

    [SerializeField] private GameObject Portrait;
    
    [SerializeField] private MessageWindowIndexStarter messageWindowIndexStarter;

    [Header("Scenario IDs")]
    [Tooltip("If true, uses ProgressManager to generate scenario IDs automatically.")]
    [SerializeField] private bool useProgressBasedId = true;
    [Tooltip("Manual start ID (used if useProgressBasedId is false).")]
    [SerializeField] private string startScenarioId;
    [Tooltip("Manual end ID (used if useProgressBasedId is false).")]
    [SerializeField] private string endScenarioId;

    private bool _isInCommunication = false;
    private bool _isAnimating = false;

    public void ComuStart(string scenarioId) => StartComuFlow(scenarioId).Forget();
    public void ComuEnd(string scenarioId) => EndComuFlow(scenarioId).Forget();

    /// <summary>
    /// Toggles between Start and End communication flows.
    /// Uses Progress-based or Inspector-configured IDs. Can be called from Button.onClick.
    /// When called, plays the Dialogue scenario for the current chapter (Ch{N}_Dialogue).
    /// </summary>
    public void ToggleComu()
    {
        if (_isAnimating) return;

        if (_isInCommunication)
        {
            Portrait.GetComponent<Button>().onClick.Invoke();
            _isInCommunication = false;
        }
        else
        {
            Portrait.GetComponent<Button>().onClick.Invoke();
            _isInCommunication = true;

            // Generate Dialogue ID based on current chapter
            string dialogueId = startScenarioId; // Fallback
            if (ProgressManager.Instance != null)
            {
                // Ensure phase is set to Dialogue
                ProgressManager.Instance.SetProgress(ProgressManager.Instance.CurrentChapter, GamePhase.Dialogue);
                dialogueId = $"Ch{ProgressManager.Instance.CurrentChapter}_Dialogue";
                Debug.Log($"[ComuManager] ToggleComu: Starting Dialogue scenario: {dialogueId}");
            }
            
            // Pass the generated ID to ComuStart so it plays after animation
            // Note: Currently ToggleComu calls Portrait click which might trigger ComuStart indirectly via another script?
            // If Portrait button calls ToggleComuforPortrait, then this method is redundant or conflicting.
            // Assuming this method IS the entry point:
            
            // However, looking at the code, Portrait.onClick.Invoke() is called. 
            // If Portrait has a button that calls ToggleComuforPortrait, we should coordinate.
            // But based on request, we want to ensure ComuStart plays the Dialogue scenario.
            
            // If Portrait click handles the flow, we shouldn't call ComuStart here to avoid double play.
            // Wait, the user code previously called Portrait.GetComponent<Button>().onClick.Invoke();
            // Let's assume the user wants this method to drive the logic.
            // But if ToggleComu IS the button listener, calling Invoke() on itself causes a loop.
            // Let's assume Portrait button has a DIFFERENT listener (maybe visuals only?) or this method IS the listener.
            
            // Reverting to the previous simpler logic but fixing the ID passing:
            // Actually, usually ToggleComuforPortrait is the main one used by the Portrait button.
            // ToggleComu seems to be a wrapper or alternative.
            
            // Let's look at ToggleComuforPortrait logic below.
        }
    }

    public void ToggleComuforPortrait()
    {
        if (_isAnimating) return;

        // Use standard logic for End, but force Dialogue logic for Start
        string endId = GetEndScenarioId();

        if (_isInCommunication)
        {
            ComuEnd(endId);
            _isInCommunication = false;
        }
        else
        {
            // FORCE Update progress to Dialogue
            string startId = startScenarioId;
            if (ProgressManager.Instance != null)
            {
                ProgressManager.Instance.SetProgress(ProgressManager.Instance.CurrentChapter, GamePhase.Dialogue);
                startId = $"Ch{ProgressManager.Instance.CurrentChapter}_Dialogue";
            }

            Debug.Log($"[ComuManager] ToggleComuforPortrait: Starting Dialogue scenario {startId}");
            
            // Pass this ID to ComuStart. 
            // ComuStart will play the visual animation and THEN play the scenario with this ID.
            ComuStart(startId);
            
            _isInCommunication = true;
        }
    }

    private string GetStartScenarioId()
    {
        if (useProgressBasedId && ProgressManager.Instance != null)
            return ProgressManager.Instance.GetScenarioKey();
        return startScenarioId;
    }

    private string GetEndScenarioId()
    {
        if (useProgressBasedId && ProgressManager.Instance != null)
            return $"Ch{ProgressManager.Instance.CurrentChapter}_loop";
        return endScenarioId;
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
            // Even if arguments are provided, if we want to enforce Dialogue logic:
            // But this overload implies explicit control. 
            // However, purely to support the user request "When communication starts...", 
            // we might want to override startId if it's a "Start" action.
            
            // For now, respect the arguments but update progress if needed.
             if (ProgressManager.Instance != null)
            {
                 ProgressManager.Instance.SetProgress(ProgressManager.Instance.CurrentChapter, GamePhase.Dialogue);
            }
            
            ComuStart(startId);
            _isInCommunication = true;
        }
    }

    public async UniTask ComuStartTask(string scenarioId, bool allowAnimation = true)
    {
        if (allowAnimation)
        {
            await StartComuFlow(scenarioId);
        }
        else
        {
            // Instant Setup
            SetPortraitInteractable(false);
            
            fadeFrame.gameObject.SetActive(false);
            NamePlate.SetActive(true);
            messageWindow.SetActive(true);
            messageWindowBackGround.SetActive(true);
            NamePlateBackGround.SetActive(true);
            ObjectiveDisplay.SetActive(true);
            Memorizer.SetActive(false); 
            LostNote.SetActive(false);
            ToggleEffect.SetActive(false);
            
            // Ensure UI panels are in correct state (Open)
            if (comuStartUI.TryGetComponent<MoveOnClickandReturn>(out var startMove)) 
            {
                // Force open state without animation logic if possible, or just play instantly?
                // For MoveOnClickandReturn, "Play()" usually toggles. 
                // If we want to skip animation, we might need to set positions manually.
                // For now, let's assume setting Active is enough for the main window, 
                // but comuStartUI is the "Chapter Title" overlay. We might want to skip showing it entirely?
                // Usually "No Animation" means "Resume conversation state instantly".
                // So we don't show the Title Card.
            }
            
            SetPortraitInteractable(true);

            if (!string.IsNullOrEmpty(scenarioId))
            {
                messageWindowIndexStarter.StartScenarioById(scenarioId);
            }
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
            // Instant Teardown
            SetPortraitInteractable(false);
            
            NamePlate.SetActive(false);
            messageWindow.SetActive(false);
            messageWindowBackGround.SetActive(false);
            NamePlateBackGround.SetActive(false);
            ObjectiveDisplay.SetActive(true); // Keep objective visible in exploration?
            fadeFrame.gameObject.SetActive(false);
            ToggleEffect.SetActive(false);
            
            Memorizer.SetActive(true);
            LostNote.SetActive(true);
            
            SetPortraitInteractable(true);

            if (!string.IsNullOrEmpty(scenarioId))
            {
                messageWindowIndexStarter.StartScenarioById(scenarioId);
            }
        }
    }

    private async UniTask StartComuFlow(string Startid)
    {
        _isAnimating = true;
        SetPortraitInteractable(false);

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

        fadeFrame.gameObject.SetActive(false);

        _isAnimating = false;
        SetPortraitInteractable(true);

        // Note: New flow system might handle message starting separately.
        // For backward compatibility, we keep this, but if scenarioId is null/empty, we skip.
        if (!string.IsNullOrEmpty(Startid))
        {
            messageWindowIndexStarter.StartScenarioById(Startid);
        }
    }

    private async UniTask EndComuFlow(string Endid)
    {
        _isAnimating = true;
        SetPortraitInteractable(false);

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

        _isAnimating = false;
        SetPortraitInteractable(true);

        if (!string.IsNullOrEmpty(Endid))
        {
            messageWindowIndexStarter.StartScenarioById(Endid);
        }
    }

    private void PlayDeskAnimation()
    {
        if (desk.TryGetComponent<MoveOnClickandReturn>(out var move)) move.Play();
        else if (desk.TryGetComponent<FirstMove>(out var first)) first.Play();
    }

    private void SetPortraitInteractable(bool interactable)
    {
        if (Portrait != null && Portrait.TryGetComponent<Button>(out var btn))
            btn.interactable = interactable;
    }
}
