using System;
using System.Collections.Generic;
using UnityEngine;
using MessageWindowSystem.Core;
using ScenarioSystem.Events;

/// <summary>キーワードの発見・クリック・表示色を、会話の再生とは独立して保持する。</summary>
public class ClueManager : MonoBehaviour
{
    public static ClueManager Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("If true, keywords are clickable immediately without discovery.")]
    public bool clickableImmediately = false;

    private const string DiscoveredColor = "#FFFF00";
    private readonly HashSet<string> _discovered = new(StringComparer.Ordinal);
    private readonly HashSet<string> _clicked = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> _keywordColors = new(StringComparer.Ordinal);
    private ProgressManager _progressManager;
    private int _chapter = -1;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void OnEnable() => SubscribeProgressManager();

    private void Update()
    {
        // ProgressManagerが後から生成・再生成される場合にも追従する。
        if (_progressManager != ProgressManager.Instance)
            SubscribeProgressManager();
    }

    private void OnDisable() => UnsubscribeProgressManager();

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    /// <summary>新しいステージの開始時に明示的に呼ぶ。会話終了や操作無効化ではリセットしない。</summary>
    public void ResetForNewStage()
    {
        _discovered.Clear();
        _clicked.Clear();
        _keywordColors.Clear();
        ScenarioEventBus.RaiseKeywordColorsChanged();
    }

    public void DiscoverKeyword(string id)
    {
        id = id?.Trim();
        if (string.IsNullOrEmpty(id) || _discovered.Contains(id) || _clicked.Contains(id)) return;

        _discovered.Add(id);
        SetKeywordColor(id, DiscoveredColor);
        FindFirstObjectByType<KeywordHandler>()?.ShakeLinkVisual(id);
    }

    public void ProcessKeywordClick(string id)
    {
        id = id?.Trim();
        if (string.IsNullOrEmpty(id) || IsDiscovered(id)) return;

        if (!clickableImmediately && !_discovered.Contains(id))
        {
            DiscoverKeyword(id);
            return;
        }

        _clicked.Add(id);
        // 即時クリック設定でも、発見済み・抽出済みを同じ黄色で表示する。
        SetKeywordColor(id, DiscoveredColor);
    }

    public bool IsClicked(string id) => _clicked.Contains(id?.Trim());

    /// <summary>発見済み・抽出済みのどちらも再クリックの対象外。</summary>
    public bool IsDiscovered(string id)
    {
        id = id?.Trim();
        return _discovered.Contains(id) || _clicked.Contains(id);
    }

    public string ApplyKeywordColors(string text) => KeywordTextFormatter.ApplyColors(text, _keywordColors);

    public void SetKeywordColor(string id, string colorHex)
    {
        id = id?.Trim();
        if (string.IsNullOrEmpty(id)) return;
        if (string.IsNullOrEmpty(colorHex) || !ColorUtility.TryParseHtmlString(colorHex, out _))
        {
            Debug.LogWarning($"[ClueManager] Invalid keyword color: {colorHex}");
            return;
        }

        if (_keywordColors.TryGetValue(id, out var previous) && previous == colorHex) return;
        _keywordColors[id] = colorHex;
        ScenarioEventBus.RaiseKeywordColorsChanged();
    }

    public void ResetKeywordStatus(string id)
    {
        id = id?.Trim();
        if (string.IsNullOrEmpty(id)) return;
        _clicked.Remove(id);
        _discovered.Remove(id);
        if (_keywordColors.Remove(id))
            ScenarioEventBus.RaiseKeywordColorsChanged();
    }

    private void SubscribeProgressManager()
    {
        UnsubscribeProgressManager();
        _progressManager = ProgressManager.Instance;
        if (_progressManager == null) return;
        _progressManager.OnProgressChanged += HandleProgressChanged;
        HandleProgressChanged();
    }

    private void UnsubscribeProgressManager()
    {
        if (_progressManager != null)
            _progressManager.OnProgressChanged -= HandleProgressChanged;
        _progressManager = null;
    }

    private void HandleProgressChanged()
    {
        int chapter = _progressManager.CurrentChapter;
        bool changed = _chapter >= 0 && _chapter != chapter;
        _chapter = chapter;
        if (changed) ResetForNewStage();
    }
}
