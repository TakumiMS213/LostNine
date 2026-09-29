using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

/// <summary>
/// Singleton that manages game progress (chapter and phase).
/// </summary>
public class ProgressManager : MonoBehaviour
{
    public static ProgressManager Instance { get; private set; }

    private static readonly int PhaseCount = Enum.GetValues(typeof(GamePhase)).Length;

    [Header("Current Progress")]
    [SerializeField] private int _currentChapter = 1;
    [SerializeField] private GamePhase _currentPhase = GamePhase.Prologue;

    [Header("Keyword Progress")]
    [Tooltip("現在のキーワード獲得数")]
    [SerializeField] private int _currentKeywordProgress = 0;

    [Tooltip("シークエンス起動に必要なキーワード数")]
    [SerializeField] private int _keywordThreshold = 3;

    [Header("Memorizer")]
    [Tooltip("カチョウのチュートリアル完了後に有効になるメモライザー解放フラグ")]
    [SerializeField] private bool _isMemorizerUnlocked;

    [Tooltip("SHIFTで切り替えるメモライザー起動フラグ。フレームUIはこの値を参照する")]
    [SerializeField] private bool _isMemorizerActive;

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
    public bool IsMemorizerUnlocked => _isMemorizerUnlocked;
    public bool IsMemorizerActive => _isMemorizerActive;
    public string MainSceneName => mainSceneName;
    public string TitleSceneName => titleSceneName;
    public string StorySceneName => storySceneName;

    public event Action OnProgressChanged;

    /// <summary>
    /// キーワード獲得数がしきい値に達した時に発火するイベント
    /// </summary>
    public event Action OnKeywordThresholdReached;

    /// <summary>メモライザーの起動状態が変わった時に発火する。フレームUIの表示切替に使用できる。</summary>
    public event Action<bool> OnMemorizerStateChanged;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            RefreshMemorizerUnlockFromProgress();
            SceneManager.activeSceneChanged += HandleActiveSceneChanged;
            Debug.Log("[ProgressManager] Initialized and marked DontDestroyOnLoad.");
        }
        else
        {
            Debug.LogWarning("[ProgressManager] Duplicate instance detected. Destroying this gameObject.");
            Destroy(gameObject);
        }
    }

    private void Update()
    {
        if (!_isMemorizerUnlocked || SceneManager.GetActiveScene().name != mainSceneName)
            return;

        var keyboard = Keyboard.current;
        if (keyboard != null &&
            (keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame))
        {
            ToggleMemorizer();
        }
    }

    private void OnDestroy()
    {
        if (Instance != this)
            return;

        SceneManager.activeSceneChanged -= HandleActiveSceneChanged;
        Instance = null;
    }

    /// <summary>
    /// Sets the current chapter and phase.
    /// </summary>
    public void SetProgress(int chapter, GamePhase phase)
    {
        Debug.Log($"[ProgressManager] SetProgress: {_currentChapter}-{_currentPhase} -> {chapter}-{phase}");
        _currentChapter = chapter;
        _currentPhase = phase;
        RefreshMemorizerUnlockFromProgress();
        ResetKeywordProgress();
        OnProgressChanged?.Invoke();
    }

    /// <summary>
    /// Advances to the next phase within the current chapter.
    /// </summary>
    public void AdvancePhase()
    {
        int nextPhase = ((int)_currentPhase + 1) % PhaseCount;
        
        if (nextPhase == 0)
        {
            // 章が変わるときだけキーワード進捗をリセット
            _currentChapter++;
            ResetKeywordProgress();
        }
        // ※ 同じ章内のフェーズ進行ではキーワード進捗を維持する
        //   (Extraction→Tuning→Fixation→Presentation で AllKeywordsCollected を保つ)
        
        _currentPhase = (GamePhase)nextPhase;
        RefreshMemorizerUnlockFromProgress();
        OnProgressChanged?.Invoke();
    }

    /// <summary>
    /// Advances to the next chapter (starts at Prologue).
    /// </summary>
    public void AdvanceChapter()
    {
        _currentChapter++;
        _currentPhase = GamePhase.Prologue;
        RefreshMemorizerUnlockFromProgress();
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
        SetMemorizerUnlocked(_currentChapter > 1);
        _currentKeywordProgress = 0;
        OnProgressChanged?.Invoke();

        TransitionToScene(storySceneName);
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

        TransitionToScene(mainSceneName, simple: true);
    }

    /// <summary>解放済みの場合だけメモライザーの起動状態を変更する。</summary>
    public bool SetMemorizerActive(bool active)
    {
        if (active && !_isMemorizerUnlocked)
            return false;

        if (_isMemorizerActive == active)
            return true;

        _isMemorizerActive = active;
        Debug.Log($"[ProgressManager] Memorizer {(active ? "activated" : "deactivated")}.");
        OnMemorizerStateChanged?.Invoke(active);
        return true;
    }

    /// <summary>メモライザーの起動状態を反転する。</summary>
    public bool ToggleMemorizer()
    {
        return SetMemorizerActive(!_isMemorizerActive);
    }

    /// <summary>
    /// キーワードを1つ追加する。しきい値に達したらイベントを発火する。
    /// </summary>
    public bool AddKeyword(string keywordId)
    {
        if (!_extractedKeywords.Add(keywordId))
        {
            Debug.Log($"[ProgressManager] Keyword '{keywordId}' already extracted. Ignored.");
            return false;
        }

        _currentKeywordProgress++;
        Debug.Log($"[ProgressManager] Keyword '{keywordId}' added. Progress: {_currentKeywordProgress}/{_keywordThreshold}");

        // 閾値ジャスト到達時のみ発火（超過後の追加では発火しない）
        if (_currentKeywordProgress == _keywordThreshold)
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

        TransitionToScene(storySceneName);

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

        TransitionToScene(storySceneName);
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
        RefreshMemorizerUnlockFromProgress();
        ResetKeywordProgress();
        OnProgressChanged?.Invoke();
        Debug.Log($"[ProgressManager] Queued main scenario: {normalizedScenarioId}");

        TransitionToScene(mainSceneName);
    }

    public bool TryConsumeMainScenarioId(out string scenarioId)
    {
        scenarioId = _pendingMainScenarioId;
        _pendingMainScenarioId = null;
        return !string.IsNullOrEmpty(scenarioId);
    }

    private static void TransitionToScene(string sceneName, bool simple = false)
    {
        var transition = SceneTransition.Instance;
        if (transition == null)
        {
            SceneManager.LoadScene(sceneName);
            return;
        }

        if (simple)
            transition.TransitionToSimple(sceneName);
        else
            transition.TransitionTo(sceneName);
    }

    private void ApplyChapterFromScenarioId(string scenarioId)
    {
        if (!TryParseChapterFromScenarioId(scenarioId, out int chapter))
            return;

        _currentChapter = Mathf.Clamp(chapter, 1, _maxChapter);
        _currentPhase = GamePhase.Prologue;
        RefreshMemorizerUnlockFromProgress();
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

    private void RefreshMemorizerUnlockFromProgress()
    {
        if (_currentChapter > 1 || (_currentChapter == 1 && _currentPhase != GamePhase.Prologue))
            SetMemorizerUnlocked(true);
    }

    private void SetMemorizerUnlocked(bool unlocked)
    {
        _isMemorizerUnlocked = unlocked;
        if (!unlocked)
            SetMemorizerActive(false);
    }

    private void HandleActiveSceneChanged(Scene previous, Scene next)
    {
        if (next.name != mainSceneName)
            SetMemorizerActive(false);
    }

    /// <summary>
    /// チャプター選択シーンへ遷移する。
    /// Epilogue終了後に呼び出す想定。
    /// </summary>
    public void GoToChapterSelect()
    {
        Debug.Log($"[ProgressManager] GoToChapterSelect: Chapter {_currentChapter} complete.");
        TransitionToScene(chapterSelectSceneName, simple: true);
    }

    /// <summary>
    /// タイトルシーンへ遷移する。
    /// </summary>
    public void GoToTitle()
    {
        TransitionToScene(titleSceneName, simple: true);
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
