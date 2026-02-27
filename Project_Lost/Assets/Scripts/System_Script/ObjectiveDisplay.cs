using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

/// <summary>
/// Displays the current objective text based on ProgressManager state.
/// Monitors progress changes and updates automatically.
/// </summary>
public class ObjectiveDisplay : MonoBehaviour
{
    public static ObjectiveDisplay Instance { get; private set; }

    // シーン側の ObjectiveTextBinder から登録される
    private TMP_Text _objectiveText;

    [Header("フェーズごとのデフォルト目標")]
    [Tooltip("フェーズごとに固定のデフォルト目標テキスト。全章共通。")]
    [SerializeField] private List<PhaseDefault> phaseDefaults = new();

    [Header("章ごとのオーバーライド（特殊な場合のみ）")]
    [Tooltip("章×フェーズの組み合わせで上書きしたい特殊なケース。")]
    [SerializeField] private List<ObjectiveEntry> overrides = new();

    [Header("Fallback")]
    [Tooltip("Text to display when no matching objective is found.")]
    [SerializeField] private string fallbackText = "---";

    [Header("Manual Mode")]
    [Tooltip("If true, ignores ProgressManager updates and only changes via SetObjectiveText.")]
    [SerializeField] private bool manualMode = false;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else { Destroy(gameObject); return; }
    }

    private void Start()
    {
        // Subscribe to progress changes
        if (ProgressManager.Instance != null)
        {
            ProgressManager.Instance.OnProgressChanged += UpdateObjectiveText;
            // Initial update if not manual
            if (!manualMode) UpdateObjectiveText();
        }
        else
        {
            Debug.LogWarning("[ObjectiveDisplay] ProgressManager.Instance is null.");
        }
    }

    private void OnDestroy()
    {
        // Unsubscribe to prevent memory leaks
        if (ProgressManager.Instance != null)
        {
            ProgressManager.Instance.OnProgressChanged -= UpdateObjectiveText;
        }
    }

    /// <summary>
    /// シーン側のTMP_Textを登録する（ObjectiveTextBinderから呼ばれる）。
    /// </summary>
    public void RegisterText(TMP_Text text)
    {
        _objectiveText = text;
        Debug.Log("[ObjectiveDisplay] TMP_Text registered.");
        if (!manualMode) UpdateObjectiveText();
    }

    /// <summary>
    /// シーン側のTMP_Textの登録を解除する。
    /// </summary>
    public void UnregisterText(TMP_Text text)
    {
        if (_objectiveText == text)
        {
            _objectiveText = null;
            Debug.Log("[ObjectiveDisplay] TMP_Text unregistered.");
        }
    }

    /// <summary>
    /// Manually sets the objective text (for ObjectiveStep or other special cases).
    /// </summary>
    public void SetObjectiveText(string text)
    {
        if (_objectiveText != null)
        {
            _objectiveText.text = text;
        }
    }

    /// <summary>
    /// Updates the objective text based on current progress.
    /// </summary>
    public void UpdateObjectiveText()
    {
        if (manualMode) return;
        if (_objectiveText == null || ProgressManager.Instance == null) return;

        int chapter = ProgressManager.Instance.CurrentChapter;
        GamePhase phase = ProgressManager.Instance.CurrentPhase;

        string text = GetObjectiveText(chapter, phase);
        _objectiveText.text = text;
    }

    private string GetObjectiveText(int chapter, GamePhase phase)
    {
        // 1) 章×フェーズのオーバーライドを探す（特殊な場合）
        foreach (var entry in overrides)
        {
            if (entry.chapter == chapter && entry.phase == phase)
            {
                return entry.objectiveText;
            }
        }

        // 2) フェーズごとのデフォルト目標を探す（通常はこちらが使われる）
        foreach (var pd in phaseDefaults)
        {
            if (pd.phase == phase)
            {
                return pd.objectiveText;
            }
        }

        // 3) フォールバック
        return fallbackText;
    }

    /// <summary>
    /// フェーズごとのデフォルト目標テキスト。全章共通。
    /// </summary>
    [Serializable]
    public class PhaseDefault
    {
        [Tooltip("対象フェーズ")]
        public GamePhase phase = GamePhase.Prologue;

        [TextArea(2, 5)]
        [Tooltip("このフェーズのデフォルト目標テキスト")]
        public string objectiveText = "";
    }

    /// <summary>
    /// 章×フェーズの特殊なオーバーライド用エントリ。
    /// </summary>
    [Serializable]
    public class ObjectiveEntry
    {
        [Tooltip("Target chapter number.")]
        public int chapter = 1;

        [Tooltip("Target game phase.")]
        public GamePhase phase = GamePhase.Prologue;

        [TextArea(2, 5)]
        [Tooltip("Objective text to display.")]
        public string objectiveText = "";
    }
}

