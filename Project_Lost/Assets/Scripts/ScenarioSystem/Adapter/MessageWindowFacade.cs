using System;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using MessageWindowSystem.Data;
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

        [Header("Legacy References")]
        [Tooltip("旧 ScenarioDatabase（ID でのシナリオ検索用）。")]
        [SerializeField] private MessageWindowSystem.Data.ScenarioDatabase legacyDatabase;

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

        /// <summary>旧 ScenarioDatabase（互換用）。</summary>
        public MessageWindowSystem.Data.ScenarioDatabase ScenarioDatabase => legacyDatabase;

        /// <summary>ウィンドウが表示中か。</summary>
        public bool IsWindowActive => _isWindowActive;

        /// <summary>タイピング中か。</summary>
        public bool IsTyping => _isTyping;

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

            // Typing state tracking
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
        /// 旧 DialogueScenario を変換せずにそのまま再生する（レガシー互換）。
        /// 旧 SO が渡された場合はログを出して警告する。
        /// 完全移行後はこのオーバーロードを削除する。
        /// </summary>
        public void StartScenario(DialogueScenario legacyScenario, Action onComplete = null)
        {
            Debug.LogWarning($"[MessageWindowFacade] Legacy DialogueScenario '{legacyScenario?.name}' was passed. " +
                             "Please migrate to ScenarioData. Falling back to legacy system.");

            // レガシーシステムへのフォールバック
            var legacyManager = MessageWindowSystem.Core.MessageWindowManager.Instance;
            if (legacyManager != null)
            {
                legacyManager.StartScenario(legacyScenario, onComplete);
            }
            else
            {
                Debug.LogError("[MessageWindowFacade] Legacy MessageWindowManager not found for fallback.");
                onComplete?.Invoke();
            }
        }

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
