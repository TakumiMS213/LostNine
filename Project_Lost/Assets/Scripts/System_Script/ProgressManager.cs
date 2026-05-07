using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

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

    private HashSet<string> _extractedKeywords = new HashSet<string>();
    private string _pendingStoryScenarioId;
    private string _pendingMainScenarioId;

    [Header("Chapter Settings")]
    [Tooltip("最大チャプター数")]
    [SerializeField] private int _maxChapter = 9;

    [Header("Scene Settings")]
    [Tooltip("メインゲームシーンの名前")]
    [SerializeField] private string mainSceneName = "Main";

    [Tooltip("チャプター選択シーンの名前")]
    [SerializeField] private string chapterSelectSceneName = "ChapterSelect";

    [Tooltip("タイトルシーンの名前")]
    [SerializeField] private string titleSceneName = "Title";

    [Tooltip("ストーリーシーンの名前")]
    [SerializeField] private string storySceneName = "Story";

    public int CurrentChapter => _currentChapter;
    public GamePhase CurrentPhase => _currentPhase;
    public int CurrentKeywordProgress => _currentKeywordProgress;
    public int KeywordThreshold => _keywordThreshold;
    public int MaxChapter => _maxChapter;
    public bool IsLastChapter => _currentChapter >= _maxChapter;
    public bool AllKeywordsCollected => _currentKeywordProgress >= _keywordThreshold;
    public string MainSceneName => mainSceneName;
    public string TitleSceneName => titleSceneName;
    public string StorySceneName => storySceneName;

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
    /// 指定したチャプターのプロローグから開始する。
    /// Progressを強制的にオーバーライドし、メインシーンをロードする。
    /// タイトル画面のチャプター選択ボタンから呼び出す想定。
    /// </summary>
    /// <param name="chapter">開始するチャプター番号</param>
    public void StartFromChapter(int chapter)
    {
        Debug.Log($"[ProgressManager] StartFromChapter: Overriding progress to Chapter {chapter}, Prologue");
        _currentChapter = Mathf.Clamp(chapter, 1, _maxChapter);
        _currentPhase = GamePhase.Prologue;
        _currentKeywordProgress = 0;
        OnProgressChanged?.Invoke();

        if (SceneTransition.Instance != null)
            SceneTransition.Instance.TransitionTo(storySceneName);
        else
            SceneManager.LoadScene(storySceneName);
    }

    /// <summary>
    /// ニューゲーム。チャプター1のプロローグから開始する。
    /// </summary>
    public void NewGame()
    {
        StartFromChapter(1);
    }

    public void LoadGame()
    {
        _pendingStoryScenarioId = null;

        if (SceneTransition.Instance != null)
            SceneTransition.Instance.TransitionToSimple(mainSceneName);
        else
            SceneManager.LoadScene(mainSceneName);
    }

    /// <summary>
    /// キーワードを1つ追加する。しきい値に達したらイベントを発火する。
    /// </summary>
    public bool AddKeyword(string keywordId)
    {
        if (_extractedKeywords.Contains(keywordId))
        {
            Debug.Log($"[ProgressManager] Keyword '{keywordId}' already extracted. Ignored.");
            return false;
        }

        _extractedKeywords.Add(keywordId);
        _currentKeywordProgress++;
        Debug.Log($"[ProgressManager] Keyword '{keywordId}' added. Progress: {_currentKeywordProgress}/{_keywordThreshold}");

        if (_currentKeywordProgress >= _keywordThreshold)
        {
            Debug.Log($"[ProgressManager] Keyword threshold reached! ({_currentKeywordProgress}/{_keywordThreshold})");
            OnKeywordThresholdReached?.Invoke();
        }
        return true;
    }

    /// <summary>
    /// キーワード進捗をリセットする（章の切り替え時などに使用）。
    /// </summary>
    public void ResetKeywordProgress()
    {
        _currentKeywordProgress = 0;
        _extractedKeywords.Clear();
    }

    /// <summary>
    /// Returns a string key for scenario lookup (e.g., "Ch1_Dialogue").
    /// </summary>
    public string GetScenarioKey() => $"Ch{_currentChapter}_{_currentPhase}";

    /// <summary>
    /// Queues a scenario ID and moves to the Story scene.
    /// </summary>
    public void StartScenarioById(string scenarioId)
    {
        StartScenarioById(scenarioId, null);
    }

    public void StartScenarioById(string scenarioId, Action onComplete = null)
    {
        string normalizedScenarioId = scenarioId?.Trim();
        if (string.IsNullOrEmpty(normalizedScenarioId))
        {
            Debug.LogWarning("[ProgressManager] Scenario ID is empty.");
            onComplete?.Invoke();
            return;
        }

        _pendingStoryScenarioId = normalizedScenarioId;
        ApplyChapterFromScenarioId(normalizedScenarioId);
        Debug.Log($"[ProgressManager] Queued story scenario: {normalizedScenarioId}");

        if (SceneTransition.Instance != null)
            SceneTransition.Instance.TransitionTo(storySceneName);
        else
            SceneManager.LoadScene(storySceneName);

        onComplete?.Invoke();
    }

    public bool TryConsumeStoryScenarioId(out string scenarioId)
    {
        scenarioId = _pendingStoryScenarioId;
        _pendingStoryScenarioId = null;
        return !string.IsNullOrEmpty(scenarioId);
    }

    public void StartProgressScenarioInStory(string scenarioId)
    {
        string normalizedScenarioId = scenarioId?.Trim();
        if (string.IsNullOrEmpty(normalizedScenarioId))
        {
            Debug.LogWarning("[ProgressManager] Progress scenario ID is empty.");
            return;
        }

        _pendingMainScenarioId = null;
        _pendingStoryScenarioId = normalizedScenarioId;
        Debug.Log($"[ProgressManager] Queued progress scenario in Story: {normalizedScenarioId}");

        if (SceneTransition.Instance != null)
            SceneTransition.Instance.TransitionTo(storySceneName);
        else
            SceneManager.LoadScene(storySceneName);
    }

    public void StartScenarioFromMainById(string scenarioId)
    {
        string normalizedScenarioId = scenarioId?.Trim();
        if (string.IsNullOrEmpty(normalizedScenarioId))
        {
            Debug.LogWarning("[ProgressManager] Main scenario ID is empty.");
            return;
        }

        _pendingStoryScenarioId = null;
        _pendingMainScenarioId = normalizedScenarioId;
        ApplyChapterFromScenarioId(normalizedScenarioId);
        _currentPhase = GamePhase.Dialogue;
        ResetKeywordProgress();
        OnProgressChanged?.Invoke();
        Debug.Log($"[ProgressManager] Queued main scenario: {normalizedScenarioId}");

        if (SceneTransition.Instance != null)
            SceneTransition.Instance.TransitionTo(mainSceneName);
        else
            SceneManager.LoadScene(mainSceneName);
    }

    public bool TryConsumeMainScenarioId(out string scenarioId)
    {
        scenarioId = _pendingMainScenarioId;
        _pendingMainScenarioId = null;
        return !string.IsNullOrEmpty(scenarioId);
    }

    private void ApplyChapterFromScenarioId(string scenarioId)
    {
        if (!TryParseChapterFromScenarioId(scenarioId, out int chapter))
            return;

        _currentChapter = Mathf.Clamp(chapter, 1, _maxChapter);
        _currentPhase = GamePhase.Prologue;
        ResetKeywordProgress();
        OnProgressChanged?.Invoke();
    }

    private static bool TryParseChapterFromScenarioId(string scenarioId, out int chapter)
    {
        chapter = 0;
        if (string.IsNullOrEmpty(scenarioId) || scenarioId.Length < 3 || scenarioId[0] != 'C' || scenarioId[1] != 'h')
            return false;

        int index = 2;
        while (index < scenarioId.Length && char.IsDigit(scenarioId[index]))
        {
            chapter = chapter * 10 + (scenarioId[index] - '0');
            index++;
        }

        return chapter > 0;
    }

    /// <summary>
    /// チャプター選択シーンへ遷移する。
    /// Epilogue終了後に呼び出す想定。
    /// </summary>
    public void GoToChapterSelect()
    {
        Debug.Log($"[ProgressManager] GoToChapterSelect: Chapter {_currentChapter} complete.");
        if (SceneTransition.Instance != null)
            SceneTransition.Instance.TransitionToSimple(chapterSelectSceneName);
        else
            SceneManager.LoadScene(chapterSelectSceneName);
    }

    /// <summary>
    /// タイトルシーンへ遷移する。
    /// </summary>
    public void GoToTitle()
    {
        if (SceneTransition.Instance != null)
            SceneTransition.Instance.TransitionToSimple(titleSceneName);
        else
            SceneManager.LoadScene(titleSceneName);
    }
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
