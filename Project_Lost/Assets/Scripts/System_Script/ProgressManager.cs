using System;
using UnityEngine;

/// <summary>
/// Singleton that manages game progress (chapter and phase).
/// </summary>
public class ProgressManager : MonoBehaviour
{
    public static ProgressManager Instance { get; private set; }

    [Header("Current Progress")]
    [SerializeField] private int _currentChapter = 1;
    [SerializeField] private GamePhase _currentPhase = GamePhase.Prologue;

    [Header("Keyword Progress")]
    [Tooltip("現在のキーワード獲得数")]
    [SerializeField] private int _currentKeywordProgress = 0;

    [Tooltip("シークエンス起動に必要なキーワード数")]
    [SerializeField] private int _keywordThreshold = 3;

    public int CurrentChapter => _currentChapter;
    public GamePhase CurrentPhase => _currentPhase;
    public int CurrentKeywordProgress => _currentKeywordProgress;
    public int KeywordThreshold => _keywordThreshold;

    public event Action OnProgressChanged;

    /// <summary>
    /// キーワード獲得数がしきい値に達した時に発火するイベント
    /// </summary>
    public event Action OnKeywordThresholdReached;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Debug.Log("[ProgressManager] Initialized and marked DontDestroyOnLoad.");
        }
        else
        {
            Debug.LogWarning("[ProgressManager] Duplicate instance detected. Destroying this gameObject.");
            Destroy(gameObject);
        }
    }

    /// <summary>
    /// Sets the current chapter and phase.
    /// </summary>
    public void SetProgress(int chapter, GamePhase phase)
    {
        Debug.Log($"[ProgressManager] SetProgress: {_currentChapter}-{_currentPhase} -> {chapter}-{phase}");
        _currentChapter = chapter;
        _currentPhase = phase;
        ResetKeywordProgress();
        OnProgressChanged?.Invoke();
    }

    /// <summary>
    /// Advances to the next phase within the current chapter.
    /// </summary>
    public void AdvancePhase()
    {
        int phaseCount = Enum.GetValues(typeof(GamePhase)).Length;
        int nextPhase = ((int)_currentPhase + 1) % phaseCount;
        
        if (nextPhase == 0)
        {
            _currentChapter++;
        }
        
        // Debug.Log($"[ProgressManager] AdvancePhase: {_currentPhase} -> {(GamePhase)nextPhase} (Chapter {_currentChapter})");
        
        _currentPhase = (GamePhase)nextPhase;
        ResetKeywordProgress();
        OnProgressChanged?.Invoke();
    }

    /// <summary>
    /// Advances to the next chapter (starts at Prologue).
    /// </summary>
    public void AdvanceChapter()
    {
        _currentChapter++;
        _currentPhase = GamePhase.Prologue;
        ResetKeywordProgress();
        OnProgressChanged?.Invoke();
    }

    /// <summary>
    /// キーワードを1つ追加する。しきい値に達したらイベントを発火する。
    /// </summary>
    public void AddKeyword()
    {
        _currentKeywordProgress++;
        Debug.Log($"[ProgressManager] Keyword added. Progress: {_currentKeywordProgress}/{_keywordThreshold}");

        if (_currentKeywordProgress >= _keywordThreshold)
        {
            Debug.Log($"[ProgressManager] Keyword threshold reached! ({_currentKeywordProgress}/{_keywordThreshold})");
            OnKeywordThresholdReached?.Invoke();
        }
    }

    /// <summary>
    /// キーワード進捗をリセットする（章の切り替え時などに使用）。
    /// </summary>
    public void ResetKeywordProgress()
    {
        _currentKeywordProgress = 0;
    }

    /// <summary>
    /// Returns a string key for scenario lookup (e.g., "Ch1_Dialogue").
    /// </summary>
    public string GetScenarioKey() => $"Ch{_currentChapter}_{_currentPhase}";
}

/// <summary>
/// Game phases within each chapter.
/// </summary>
public enum GamePhase
{
    Prologue,     // プロローグ
    Dialogue,     // 対話
    Extraction,   // 抽出
    Tuning,       // 調律
    Fixation,     // 定着
    Presentation, // 提示
    Epilogue      // エピローグ
}
