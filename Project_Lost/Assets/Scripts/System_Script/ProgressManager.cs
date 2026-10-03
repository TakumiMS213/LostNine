using System;
using System.Collections.Generic;
using ScenarioSystem.Model;
using DG.Tweening;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Singleton that manages game progress (chapter and phase).
/// </summary>
[DefaultExecutionOrder(-100)]
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

    [Tooltip("SHIFTを押している間だけ有効になるメモライザー起動フラグ。フレームUIはこの値を参照する")]
    [SerializeField] private bool _isMemorizerActive;

    [Tooltip("メモライザー起動中に画面全体へ表示するフレーム画像")]
    [SerializeField] private Sprite memorizerFrameSprite;

    [SerializeField] private TMP_FontAsset memorizerStatusFont;
    [SerializeField, Min(1f)] private float memorizerFrameStartScale = 1.35f;
    [SerializeField, Min(0.01f)] private float memorizerFrameEnterDuration = 0.35f;
    [SerializeField, Min(0f)] private float memorizerStatusDuration = 0.5f;
    [SerializeField, Min(0f)] private float memorizerStatusJumpHeight = 28f;
    [SerializeField] private Color memorizerTintColor = new Color(0.35f, 0.25f, 0.75f, 0.12f);

    private GameObject _memorizerCanvas;
    private Image _memorizerFrame;
    private Image _memorizerTint;
    private Canvas _memorizerTintCanvas;
    private Material _memorizerTintMaterial;
    private TextMeshProUGUI _memorizerStatus;
    private Tween _memorizerFrameTween;
    private Sequence _memorizerStatusTween;
    private bool _hasApplicationFocus = true;

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
            RefreshMemorizerFrame();
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
        var keyboard = Keyboard.current;
        bool shiftHeld = keyboard != null
            && (keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed);
        SetMemorizerActive(_isMemorizerUnlocked && _hasApplicationFocus
            && SceneManager.GetActiveScene().name == mainSceneName && shiftHeld);
    }

    private void OnApplicationFocus(bool hasFocus)
    {
        _hasApplicationFocus = hasFocus;
        if (!hasFocus)
            SetMemorizerActive(false);
    }

    private void OnDisable()
    {
        if (Instance == this)
            SetMemorizerActive(false);
    }

    private void OnDestroy()
    {
        StopMemorizerTweens();
        if (_memorizerTintMaterial != null) Destroy(_memorizerTintMaterial);
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
        bool chapterChanged = _currentChapter != chapter;
        _currentChapter = chapter;
        _currentPhase = phase;
        RefreshMemorizerUnlockFromProgress();
        // 発見色・再クリック不可と同様、取得済みIDと個数も章の間は保持する。
        // LOAD/ニューゲームで同章をやり直す場合はStartFromChapterが明示的にリセットする。
        if (chapterChanged) ResetKeywordProgress();
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
        SetMemorizerActive(false);
        _pendingStoryScenarioId = null;
        _pendingMainScenarioId = null;
        ResetKeywordProgress();
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
        RefreshMemorizerFrame(true);
        Debug.Log($"[ProgressManager] Memorizer {(active ? "activated" : "deactivated")}.");
        OnMemorizerStateChanged?.Invoke(active);
        return true;
    }

    private void RefreshMemorizerFrame(bool animateAndNotify = false)
    {
        StopMemorizerTweens();
        RefreshMemorizerTintMasks();
        bool canDisplay = _isMemorizerUnlocked
            && SceneManager.GetActiveScene().name == mainSceneName
            && memorizerFrameSprite != null;

        if (!canDisplay || (!_isMemorizerActive && !animateAndNotify))
        {
            if (_memorizerCanvas != null)
                _memorizerCanvas.SetActive(false);
            if (_memorizerTintCanvas != null)
                _memorizerTintCanvas.gameObject.SetActive(false);
            return;
        }

        EnsureMemorizerUI();
        _memorizerCanvas.SetActive(true);
        _memorizerTintCanvas.worldCamera = Camera.main;
        _memorizerTintCanvas.gameObject.SetActive(_isMemorizerActive);
        _memorizerFrame.gameObject.SetActive(_isMemorizerActive);
        _memorizerTint.gameObject.SetActive(_isMemorizerActive);
        _memorizerTint.color = memorizerTintColor;
        _memorizerFrame.rectTransform.localScale = Vector3.one;
        _memorizerStatus.gameObject.SetActive(animateAndNotify);

        if (!animateAndNotify)
            return;

        if (_isMemorizerActive)
        {
            _memorizerFrame.rectTransform.localScale = Vector3.one * memorizerFrameStartScale;
            _memorizerFrameTween = _memorizerFrame.rectTransform.DOScale(1f, memorizerFrameEnterDuration)
                .SetEase(Ease.OutBack).SetUpdate(true).SetLink(gameObject);
        }

        _memorizerStatus.text = _isMemorizerActive ? "MEMORIZER ON" : "MEMORIZER OFF";
        _memorizerStatus.alpha = 1f;
        var statusRect = _memorizerStatus.rectTransform;
        // 連続切り替えでも、前のジャンプ位置を引き継がず同じ位置から跳ねる。
        statusRect.anchoredPosition = Vector2.zero;
        _memorizerStatusTween = DOTween.Sequence();
        _memorizerStatusTween.SetUpdate(true).SetLink(gameObject);
        float fadeDuration = Mathf.Min(0.15f, memorizerStatusDuration);
        float halfJumpDuration = (memorizerStatusDuration - fadeDuration) * 0.5f;
        _memorizerStatusTween.Append(statusRect.DOAnchorPosY(memorizerStatusJumpHeight, halfJumpDuration)
            .SetEase(Ease.OutQuad));
        _memorizerStatusTween.Append(statusRect.DOAnchorPosY(0f, halfJumpDuration)
            .SetEase(Ease.InQuad));
        _memorizerStatusTween.Append(_memorizerStatus.DOFade(0f, fadeDuration));
        _memorizerStatusTween.OnComplete(() =>
        {
            _memorizerStatus.gameObject.SetActive(false);
            if (!_isMemorizerActive)
                _memorizerCanvas.SetActive(false);
        });
    }

    private void EnsureMemorizerUI()
    {
        if (_memorizerCanvas != null)
            return;

        // ProgressManagerと一緒に保持する。描画専用なのでGraphicRaycasterは付けない。
        _memorizerCanvas = new GameObject("MemorizerFrameCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
        _memorizerCanvas.transform.SetParent(transform, false);
        var canvas = _memorizerCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // SceneTransitionのフェード（9999）より背面。
        var scaler = _memorizerCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        // 色フィルターはフレームとステータス文字の背面に置く。
        _memorizerTint = CreateMemorizerImage("MemorizerTint");
        // 立ち絵と同じカメラのステンシルを読み、最前面のOverlayフレームより先に描く。
        var tintRoot = new GameObject("MemorizerTintCanvas", typeof(RectTransform), typeof(Canvas));
        tintRoot.transform.SetParent(transform, false);
        _memorizerTintCanvas = tintRoot.GetComponent<Canvas>();
        _memorizerTintCanvas.renderMode = RenderMode.ScreenSpaceCamera;
        _memorizerTintCanvas.worldCamera = Camera.main;
        _memorizerTintCanvas.planeDistance = 1f;
        _memorizerTintCanvas.sortingOrder = 99;
        _memorizerTint.transform.SetParent(tintRoot.transform, false);
        _memorizerTint.rectTransform.offsetMin = Vector2.zero;
        _memorizerTint.rectTransform.offsetMax = Vector2.zero;
        _memorizerTintMaterial = new Material(_memorizerTint.defaultMaterial);
        _memorizerTintMaterial.SetInt("_Stencil", MemorizerTintMask.PortraitBit);
        _memorizerTintMaterial.SetInt("_StencilReadMask", MemorizerTintMask.PortraitBit);
        _memorizerTintMaterial.SetInt("_StencilWriteMask", 0);
        _memorizerTintMaterial.SetInt("_StencilComp", (int)UnityEngine.Rendering.CompareFunction.NotEqual);
        _memorizerTint.material = _memorizerTintMaterial;
        _memorizerFrame = CreateMemorizerImage("MemorizerFrame");
        _memorizerFrame.sprite = memorizerFrameSprite;

        var statusObject = new GameObject("MemorizerStatus", typeof(RectTransform), typeof(TextMeshProUGUI));
        statusObject.transform.SetParent(_memorizerCanvas.transform, false);
        _memorizerStatus = statusObject.GetComponent<TextMeshProUGUI>();
        _memorizerStatus.font = memorizerStatusFont;
        _memorizerStatus.alignment = TextAlignmentOptions.Center;
        _memorizerStatus.color = Color.white;
        _memorizerStatus.raycastTarget = false;
        _memorizerStatus.textWrappingMode = TextWrappingModes.NoWrap;
        _memorizerStatus.enableAutoSizing = true;
        _memorizerStatus.fontSizeMin = 12f;
        _memorizerStatus.fontSizeMax = 120f;
        var statusRect = _memorizerStatus.rectTransform;
        statusRect.anchorMin = new Vector2(1f / 3f, 0.95f);
        statusRect.anchorMax = new Vector2(2f / 3f, 0.95f);
        statusRect.pivot = new Vector2(0.5f, 1f);
        statusRect.sizeDelta = new Vector2(0f, 160f);
        statusRect.anchoredPosition = Vector2.zero;
        statusRect.localScale = Vector3.one * 0.5f;
    }

    private void RefreshMemorizerTintMasks()
    {
        // シーン内の非表示UIも登録し、会話開始後に表示されたUIにも同じ除外判定を使う。
        foreach (var graphic in FindObjectsByType<Graphic>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (graphic.gameObject.scene.name != mainSceneName) continue;
            if (_isMemorizerActive && !graphic.TryGetComponent<MemorizerTintMask>(out _))
                graphic.gameObject.AddComponent<MemorizerTintMask>();
            graphic.SetMaterialDirty();
        }
    }

    private Image CreateMemorizerImage(string objectName)
    {
        var imageObject = new GameObject(objectName, typeof(RectTransform), typeof(Image));
        imageObject.transform.SetParent(_memorizerCanvas.transform, false);
        var image = imageObject.GetComponent<Image>();
        image.raycastTarget = false;
        var rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return image;
    }

    private void StopMemorizerTweens()
    {
        // 連打やシーン退出時は古い完了処理も破棄し、最新の状態を優先する。
        _memorizerFrameTween?.Kill();
        _memorizerFrameTween = null;
        _memorizerStatusTween?.Kill();
        _memorizerStatusTween = null;
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
    public string GetScenarioKey() => ScenarioKey.ForPhase(_currentChapter, _currentPhase);

    /// <summary>
    /// Queues a scenario ID and moves to the Story scene.
    /// </summary>
    public void StartScenarioById(string scenarioId)
    {
        StartScenarioById(scenarioId, null);
    }

    public void StartScenarioById(string scenarioId, Action onComplete = null)
    {
        string normalizedScenarioId = ScenarioKey.Normalize(scenarioId);
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
        string normalizedScenarioId = ScenarioKey.Normalize(scenarioId);
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
        string normalizedScenarioId = ScenarioKey.Normalize(scenarioId);
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
        return ScenarioKey.TryGetChapter(scenarioId, out chapter);
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
        RefreshMemorizerFrame();
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
