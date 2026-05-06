using System.Collections.Generic;
using UnityEngine;
using MessageWindowSystem.Core;

/// <summary>
/// Manages keyword discovery and click state for the dialogue system.
/// </summary>
public class ClueManager : MonoBehaviour
{
    public static ClueManager Instance { get; private set; }

    [Header("Settings")]
    [Tooltip("If true, keywords are clickable immediately without discovery.")]
    public bool clickableImmediately = false;

    private readonly HashSet<string> _discovered = new();
    private readonly HashSet<string> _clicked = new();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    /// <summary>
    /// Resets all keyword states (for stage transitions).
    /// </summary>
    public void ResetForNewStage()
    {
        _discovered.Clear();
        _clicked.Clear();
    }

    /// <summary>
    /// Marks a keyword as discovered (first encounter).
    /// </summary>
    public void DiscoverKeyword(string id)
    {
        if (string.IsNullOrEmpty(id) || _discovered.Contains(id) || _clicked.Contains(id)) return;

        _discovered.Add(id);

        // Use KeywordHandler for visual feedback
        var handler = FindKeywordHandler();
        handler?.SetLinkColor(id, "#FFFF00");
        handler?.ShakeLinkVisual(id);
    }

    /// <summary>
    /// Processes a keyword click (confirms discovery and triggers effects).
    /// </summary>
    public void ProcessKeywordClick(string id)
    {
        if (string.IsNullOrEmpty(id)) return;

        bool canClick = clickableImmediately || _discovered.Contains(id);

        if (!canClick)
        {
            DiscoverKeyword(id);
            return;
        }

        if (!_clicked.Contains(id))
        {
            _clicked.Add(id);
            // NOTE: Color persistence is disabled. No greying out of clicked keywords.
        }

        // Keyword conversation is now handled via OnKeywordScenarioRequested event in KeywordHandler
    }

    public bool IsClicked(string id) => _clicked.Contains(id);

    public void ResetKeywordStatus(string id)
    {
        _clicked.Remove(id);
        _discovered.Remove(id);
    }

    /// <summary>
    /// Finds the KeywordHandler in the scene.
    /// </summary>
    private static KeywordHandler FindKeywordHandler()
    {
        // Prefer getting it through the manager if available
        return FindFirstObjectByType<KeywordHandler>();
    }
}
