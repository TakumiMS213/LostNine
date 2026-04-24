using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using ScenarioSystem.Model;
using ScenarioSystem.Presenter;
using ScenarioSystem.Events;

namespace ScenarioSystem.Adapter
{
    /// <summary>
    /// 旧 MessageWindowManager の公開 API を維持しながら、
    /// 内部的に新 ScenarioPresenter へ委譲するファサード。
    /// 
    /// 既存の ComuStartandEndManager / TalkStep / KeywordHandler 等からの呼び出しを
    /// そのまま受け取り、新システムに転送する。
    /// 完全移行後はこのクラスを削除し、ScenarioPresenter を直接使用する。
    /// </summary>
    public class MessageWindowFacade : MonoBehaviour
    {
        #region Singleton

        public static MessageWindowFacade Instance { get; private set; }

        #endregion

        #region Serialized Fields

        [Header("New System")]
        [SerializeField] private ScenarioPresenter presenter;

        [Header("New Database (ScenarioData)")]
        [Tooltip("新 ScenarioData 用データベース。ID 検索に使用。")]
        [SerializeField] private ScenarioDataDatabase scenarioDataDatabase;

        // legacyDatabase removed

        [Header("UI References (read-only exposure)")]
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField] private GameObject windowRoot;

        #endregion

        #region Private Fields

        private readonly List<(string speaker, string text)> _log = new();
        private bool _isWindowActive;
        private bool _isTyping;

        #endregion

        #region Public Properties (旧 MWM 互換)

        /// <summary>DialogueText（KeywordHandler が参照）。</summary>
        public TMP_Text DialogueText => dialogueText;

        // ScenarioDatabase removed

        /// <summary>ウィンドウが表示中か。</summary>
        public bool IsWindowActive => _isWindowActive;

        /// <summary>タイピング中か。</summary>
        public bool IsTyping => _isTyping;

        /// <summary>シナリオ再生中か。</summary>
        public bool IsPlaying => presenter != null && presenter.IsPlaying;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }
        }

        private void OnEnable()
        {
            ScenarioEventBus.OnWindowVisibilityChanged += HandleWindowVisibility;
            ScenarioEventBus.OnDialogueRequested += HandleDialogueForLog;
            ScenarioEventBus.OnDialogueRequested += _ => _isTyping = true;
            ScenarioEventBus.OnTypingCompleted += () => _isTyping = false;
        }

        private void OnDisable()
        {
            ScenarioEventBus.OnWindowVisibilityChanged -= HandleWindowVisibility;
            ScenarioEventBus.OnDialogueRequested -= HandleDialogueForLog;
        }

        #endregion

        #region Public API (旧 MWM 互換)

        /// <summary>
        /// 新 ScenarioData を再生する。
        /// </summary>
        public void StartScenario(ScenarioData scenario, Action onComplete = null)
        {
            if (presenter == null)
            {
                Debug.LogError("[MessageWindowFacade] ScenarioPresenter is not assigned.");
                onComplete?.Invoke();
                return;
            }

            presenter.StartScenario(scenario, onComplete);
        }

        /// <summary>
        /// シナリオ ID で検索して再生する。
        /// 新 DB → 旧 DB の順で検索し、見つかったものを再生する。
        /// </summary>
        public void StartScenarioById(string scenarioId, Action onComplete = null)
        {
            if (string.IsNullOrEmpty(scenarioId))
            {
                Debug.LogWarning("[MessageWindowFacade] Empty scenario ID.");
                onComplete?.Invoke();
                return;
            }

            // 1. 新 ScenarioDataDatabase で検索
            if (scenarioDataDatabase != null)
            {
                var newScenario = scenarioDataDatabase.GetById(scenarioId);
                if (newScenario != null)
                {
                    StartScenario(newScenario, onComplete);
                    return;
                }
            }

            // Legacy fallback removed

            Debug.LogWarning($"[MessageWindowFacade] Scenario '{scenarioId}' not found in any database.");
            onComplete?.Invoke();
        }

        // Legacy DialogueScenario fallback removed

        /// <summary>テキスト送り（旧 Next() 互換）。</summary>
        public void Next()
        {
            ScenarioEventBus.RaiseAdvanceRequested();
        }

        /// <summary>会話ログを取得する。</summary>
        public IReadOnlyList<(string speaker, string text)> GetLog() => _log;

        #endregion

        #region Event Handlers

        private void HandleWindowVisibility(bool visible)
        {
            _isWindowActive = visible;
        }

        private void HandleDialogueForLog(DialogueEventData data)
        {
            _log.Add((data.SpeakerName ?? string.Empty, data.Text ?? string.Empty));
        }

        #endregion
    }
}

