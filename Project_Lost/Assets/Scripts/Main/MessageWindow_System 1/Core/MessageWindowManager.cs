using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using TMPro;
using DG.Tweening;
using MessageWindowSystem.Data;
using Main.UIMoves;

namespace MessageWindowSystem.Core
{
    /// <summary>
    /// Manages the dialogue window, keyword interactions, and typing effects.
    /// </summary>
    public class MessageWindowManager : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        #region Serialized Fields

        [Header("UI References")]
        [SerializeField] private TMP_Text speakerNameText;
        [SerializeField] private TMP_Text dialogueText;
        [SerializeField] private Image portraitImage;
        [SerializeField] private GameObject windowRoot;

        [Header("Typing Settings")]
        [SerializeField] private float typingSpeed = 0.05f;

        [Header("Name Slide Animation")]
        [SerializeField] private bool animateName = true;
        [SerializeField] private float nameSlideDistance = 600f;
        [SerializeField] private float nameSlideDuration = 0.35f;
        [SerializeField] private Ease nameSlideEase = Ease.OutCubic;

        [Header("Portrait Animation")]
        [SerializeField] private bool portraitJumpOnText = true;
        [SerializeField] private float portraitJumpHeight = 50f;
        [SerializeField] private float portraitJumpDuration = 0.3f;
        [SerializeField] private Ease portraitJumpEase = Ease.OutBounce;

        [Header("Skip Mode")]
        [SerializeField] private bool enableSkipMode = true;
        [SerializeField] private Key skipKey = Key.LeftCtrl;
        [SerializeField] private float skipTypingSpeed = 0.001f;

        [Header("Keyword Charge")]
        [SerializeField] private float chargeDuration = 1.0f;
        [SerializeField] private bool _isKeywordEnabled = true;

        [Header("Choice Buttons")]
        [Tooltip("Pre-attached choice buttons (max 4 typically).")]
        [SerializeField] private Button[] choiceButtons;
        [SerializeField] private TMP_Text[] choiceButtonTexts;

        [Header("Database")]
        [SerializeField] private ScenarioDatabase scenarioDatabase;

        [Header("Debug")]
        [SerializeField] private bool showDebugGUI = true;

        #endregion

        #region Public Properties

        public static MessageWindowManager Instance { get; private set; }
        public bool IsKeywordEnabled => _isKeywordEnabled;
        public event Action<string> OnKeywordClicked;

        #endregion

        #region Private Fields

        private readonly List<(string speaker, string text)> _log = new();
        private readonly Queue<DialogueLine> _linesQueue = new();
        
        private DialogueScenario _currentScenarioData;
        private DialogueLine _currentLine;
        
        private Coroutine _typingCoroutine;
        private Coroutine _chargeCoroutine;
        
        private Vector2 _nameOriginalAnchored;
        private string _previousSpeakerName;
        private string _chargingLinkID;
        
        private bool _isTyping;
        private bool _isWindowActive;
        private bool _isCharging;
        private bool _shouldBlockNext;
        private bool _nameOriginalCaptured;
        private bool _isWaitingForChoice;
        private Action _onScenarioComplete;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else { Destroy(gameObject); return; }

            if (windowRoot) windowRoot.SetActive(false);

            if (speakerNameText != null)
            {
                _nameOriginalAnchored = speakerNameText.rectTransform.anchoredPosition;
                _nameOriginalCaptured = true;
            }

            HideAllChoices();
        }

        private void OnGUI()
        {
            if (!showDebugGUI) return;

            GUI.color = Color.black;
            GUILayout.BeginArea(new Rect(10, 10, 400, 200), GUI.skin.box);
            GUI.color = Color.white;
            
            GUILayout.Label($"<b>[Debug Info]</b>");
            if (ProgressManager.Instance != null)
            {
                GUILayout.Label($"Chapter: {ProgressManager.Instance.CurrentChapter} | Phase: {ProgressManager.Instance.CurrentPhase}");
            }
            else
            {
                GUILayout.Label("ProgressManager: NULL");
            }

            if (_currentScenarioData != null)
            {
                GUILayout.Label($"Scenario: {_currentScenarioData.name} ({_currentScenarioData.scenarioId})");
                GUILayout.Label($"UpdateProgress: {_currentScenarioData.updateProgressOnEnd}");
                GUILayout.Label($"Action: {_currentScenarioData.progressAction}");
                GUILayout.Label($"ToggleComu: {_currentScenarioData.toggleComuOnEnd}");
            }
            else
            {
                GUILayout.Label("Scenario: None");
            }
            GUILayout.EndArea();
        }

        #endregion

        #region Public API

        public void SetKeywordEnabled(bool enable) => _isKeywordEnabled = enable;

        public void StartScenario(DialogueScenario scenario, Action onComplete = null)
        {
            if (scenario == null)
            {
                onComplete?.Invoke();
                return;
            }

            // Reset choice state
            _isWaitingForChoice = false;
            HideAllChoices();

            _isKeywordEnabled = scenario.enableKeywords;
            BakePersistentColors(scenario);

            _linesQueue.Clear();
            foreach (var line in scenario.lines)
                _linesQueue.Enqueue(line);

            _currentScenarioData = scenario;
            _onScenarioComplete = onComplete; // Store callback

            if (windowRoot) windowRoot.SetActive(true);
            _isWindowActive = true;
            
            Debug.Log($"[MWM] StartScenario: {scenario.name} (ID: {scenario.scenarioId})");
            Debug.Log($"[MWM] Settings -> updateProgressOnEnd: {scenario.updateProgressOnEnd}, Action: {scenario.progressAction}");

            DisplayNextLine();
        }

        public void StartScenario(DialogueScenario scenario, bool enableKeywords, Action onComplete = null)
        {
            SetKeywordEnabled(enableKeywords);
            StartScenario(scenario, onComplete);
        }

        public void Next()
        {
            if (_isWaitingForChoice) return;
            if (_shouldBlockNext)
            {
                _shouldBlockNext = false;
                return;
            }
            SkipOrInteract();
        }

        public IReadOnlyList<(string speaker, string text)> GetLog() => _log;

        #endregion

        #region Keyword Link Methods

        public void SetLinkColor(string id, string colorHex)
        {
            if (dialogueText == null || string.IsNullOrEmpty(id) || _currentScenarioData == null) return;

            bool currentLineChanged = false;
            string pattern = BuildLinkPattern(id);

            foreach (var line in _currentScenarioData.lines)
            {
                if (string.IsNullOrEmpty(line.text) || !Regex.IsMatch(line.text, pattern)) continue;

                string newText = Regex.Replace(line.text, pattern, m =>
                {
                    string stripped = StripColorTags(m.Groups[1].Value);
                    return $"<a href=\"{id}\"><color={colorHex}>{stripped}</color></a>";
                }, RegexOptions.Singleline);

                if (line.text != newText)
                {
                    line.text = newText;
                    if (line == _currentLine) currentLineChanged = true;
                }
            }

            if (currentLineChanged) RefreshDialogueText();
        }

        public void ResetKeywordState(string id)
        {
            if (string.IsNullOrEmpty(id)) return;

            ClueManager.Instance?.ResetKeywordStatus(id);

            if (_currentScenarioData == null) return;

            bool currentLineChanged = false;
            string pattern = BuildLinkPattern(id);

            foreach (var line in _currentScenarioData.lines)
            {
                if (string.IsNullOrEmpty(line.text) || !Regex.IsMatch(line.text, pattern)) continue;

                string newText = Regex.Replace(line.text, pattern, m =>
                {
                    string stripped = StripColorTags(m.Groups[1].Value);
                    return $"<a href=\"{id}\">{stripped}</a>";
                }, RegexOptions.Singleline);

                if (line.text != newText)
                {
                    line.text = newText;
                    if (line == _currentLine) currentLineChanged = true;
                }
            }

            if (currentLineChanged) RefreshDialogueText();
        }

        public void ShakeLinkVisual(string id)
        {
            dialogueText?.GetComponent<RectTransform>()?.DOShakeAnchorPos(0.35f, new Vector2(8f, 0f), 10, 90f);
        }

        public void StartKeywordConversation(string id)
        {
            var ds = Resources.Load<DialogueScenario>($"KeywordConversations/{id}");
            if (ds != null) 
            {
                // Pause current flow? Ideally, we should stack scenarios.
                // For now, simple fire-and-forget or nested call.
                // Keyword conversations are usually interruptions.
                // We might want to resume the previous one?
                // Let's just play it.
                StartScenario(ds, () => 
                {
                    // Maybe return to previous? 
                    // For now, just allow it to end.
                });
            }
        }

        #endregion

        #region Pointer Handlers (Keyword Charge)

        public void OnPointerDown(PointerEventData eventData)
        {
            _shouldBlockNext = false;

            // Debug: Check which condition blocks keyword interaction
            if (!_isWindowActive)
            {
                Debug.Log("[MWM] OnPointerDown blocked: _isWindowActive is false");
                return;
            }
            if (_isTyping)
            {
                Debug.Log("[MWM] OnPointerDown blocked: _isTyping is true");
                return;
            }
            if (!_isKeywordEnabled)
            {
                Debug.Log("[MWM] OnPointerDown blocked: _isKeywordEnabled is false");
                return;
            }

            Camera uiCamera = GetUICamera();
            int linkIndex = TMP_TextUtilities.FindIntersectingLink(dialogueText, eventData.position, uiCamera);

            if (linkIndex == -1)
            {
                Debug.Log($"[MWM] OnPointerDown: No link found at position {eventData.position}");
                return;
            }

            Debug.Log($"[MWM] OnPointerDown: Link found! Index={linkIndex}");
            _shouldBlockNext = true;
            _chargingLinkID = dialogueText.textInfo.linkInfo[linkIndex].GetLinkID();
            _isCharging = true;
            _chargeCoroutine = StartCoroutine(ChargeRoutine(linkIndex, _chargingLinkID));
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_isCharging) CancelCharge();
        }

        private void CancelCharge()
        {
            _isCharging = false;
            if (_chargeCoroutine != null) StopCoroutine(_chargeCoroutine);

            if (!string.IsNullOrEmpty(_chargingLinkID))
                dialogueText.ForceMeshUpdate();

            EffectManager.Instance?.StopChargeSE();
            _chargingLinkID = null;
        }

        private IEnumerator ChargeRoutine(int linkIndex, string linkID)
        {
            EffectManager.Instance?.PlayChargeSE();

            var linkInfo = dialogueText.textInfo.linkInfo[linkIndex];
            int startCharIdx = linkInfo.linkTextfirstCharacterIndex;
            int charCount = linkInfo.linkTextLength;

            var originalColors = new Color32[charCount];
            var originalVertices = new Vector3[charCount][];

            CacheCharacterData(startCharIdx, charCount, originalColors, originalVertices);

            Color32 targetColor = new Color32(255, 215, 0, 255);
            const float maxScale = 1.5f;

            float timer = 0f;
            while (timer < chargeDuration)
            {
                timer += Time.deltaTime;
                float progress = timer / chargeDuration;
                float easedProgress = DOVirtual.EasedValue(0f, 1f, progress, Ease.OutQuad);
                float scale = Mathf.Lerp(1f, maxScale, easedProgress);

                ApplyChargeVisuals(startCharIdx, charCount, originalColors, originalVertices, targetColor, progress, scale);
                yield return null;
            }

            RestoreVertices(startCharIdx, charCount, originalVertices);

            _isCharging = false;
            EffectManager.Instance?.StopChargeSE();
            EffectManager.Instance?.PlayDevelopmentEffect();

            OnKeywordClicked?.Invoke(linkID);
            ClueManager.Instance?.ProcessKeywordClick(linkID);
            ProgressManager.Instance?.AddKeyword();

            // Try to play corresponding scenario from database
            if (scenarioDatabase != null)
            {
                var scenario = scenarioDatabase.GetScenarioById(linkID);
                if (scenario != null)
                {
                    Debug.Log($"[MWM] Found scenario for keyword '{linkID}'. Playing.");
                    StartScenario(scenario);
                }
            }
        }

        #endregion

        #region Dialogue Flow

        private void SkipOrInteract()
        {
            if (_isCharging) return;

            if (_isTyping)
            {
                if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
                _isTyping = false;
                dialogueText.text = _currentLine.text;
                dialogueText.maxVisibleCharacters = _currentLine.text.Length;

                // Show choices if this line has any (same logic as end of TypeText)
                if (_currentLine?.choices != null && _currentLine.choices.Count > 0)
                {
                    ShowChoices(_currentLine.choices);
                }
                return;
            }

            DisplayNextLine();
        }

#region Dialogue Flow

private void DisplayNextLine()
{
    if (_linesQueue.Count == 0)
    {
        // ループ判定：同一シナリオを繰り返す設定なら再開
        if (_currentScenarioData != null && _currentScenarioData.loopScenario)
        {
            StartScenario(_currentScenarioData, _onScenarioComplete);
            return;
        }

        // それ以外（終了、または次のシナリオへ）はすべて EndScenario に集約
        EndScenario();
        return;
    }

    _currentLine = _linesQueue.Dequeue();
    string speakerName = _currentLine.speakerName ?? string.Empty;

    if (speakerNameText) speakerNameText.text = speakerName;
    _log.Add((speakerName, _currentLine.text));

    UpdatePortrait();
    PlayEffects();
    AnimateSpeakerName(speakerName);

    _previousSpeakerName = speakerName;

    if (_typingCoroutine != null) StopCoroutine(_typingCoroutine);
    float speed = _currentLine.typingSpeed > 0 ? _currentLine.typingSpeed : typingSpeed;
    _typingCoroutine = StartCoroutine(TypeText(_currentLine.text, speed));
}

        private void EndScenario()
        {
            // 1. 進行状況の更新（フェーズ移行など）を、次のシナリオへ行く前に実行
            HandleScenarioCompletionActions();

            // 2. 次のシナリオチェーンがあるか確認
            if (_currentScenarioData?.nextScenario != null)
            {
                Debug.Log($"[MWM] Transitioning to next scenario: {_currentScenarioData.nextScenario.name}");
                // 次のシナリオを開始（ウィンドウは閉じない）
                StartScenario(_currentScenarioData.nextScenario, _onScenarioComplete);
                return;
            }

            // 3. チェーンがない場合はウィンドウを閉じる
            _isWindowActive = false;
            if (windowRoot) windowRoot.SetActive(false);

            // 4. 全行程終了のコールバックを実行
            _onScenarioComplete?.Invoke();
            _onScenarioComplete = null;
        }

        #endregion

        #region Choice Methods

        public void OnChoiceSelected(int index)
        {
            if (!_isWaitingForChoice || _currentLine?.choices == null) return;
            if (index < 0 || index >= _currentLine.choices.Count) return;

            var choice = _currentLine.choices[index];
            _isWaitingForChoice = false;
            HideAllChoices();

            // ★ 選択肢を選んだ際も、現在のシナリオとしての完了アクション（フェーズ移行等）を先に処理
            HandleScenarioCompletionActions();

            if (choice.nextScenario != null)
            {
                // 選択肢によって指定された次のシナリオへ移行
                StartScenario(choice.nextScenario, _onScenarioComplete);
            }
            else
            {
                // 飛び先がない場合は、現在のキューをクリアして終了処理（EndScenario）へ
                _linesQueue.Clear();
                EndScenario();
            }
        }

        #endregion

        private void HandleScenarioCompletionActions()
        {
            if (_currentScenarioData == null) return;

            // Handle Communication Toggle
            if (_currentScenarioData.toggleComuOnEnd)
            {
                var comuManager = FindObjectOfType<ComuStartandEndManager>();
                if (comuManager != null)
                {
                    Debug.Log("[MWM] toggleComuOnEnd: Calling ToggleComu().");
                    comuManager.ToggleComu();
                }
                else
                {
                    Debug.LogWarning("[MWM] toggleComuOnEnd is set but ComuStartandEndManager not found.");
                }
            }

            // Handle Progress Update
            if (_currentScenarioData.updateProgressOnEnd && ProgressManager.Instance != null)
            {
                Debug.Log($"[MWM] updateProgressOnEnd: Executing {_currentScenarioData.progressAction}");
                switch (_currentScenarioData.progressAction)
                {
                    case ProgressActionType.AdvancePhase:
                        ProgressManager.Instance.AdvancePhase(); 
                        break;
                    case ProgressActionType.AdvanceChapter:
                        ProgressManager.Instance.AdvanceChapter();
                        break;
                    case ProgressActionType.SetDirectly:
                        ProgressManager.Instance.SetProgress(_currentScenarioData.targetChapter, _currentScenarioData.targetPhase);
                        break;
                }
            }
        }

        private IEnumerator TypeText(string text, float speed)
        {
            _isTyping = true;
            dialogueText.text = text;
            dialogueText.maxVisibleCharacters = 0;
            dialogueText.ForceMeshUpdate();

            int total = dialogueText.textInfo.characterCount;
            for (int i = 0; i <= total; i++)
            {
                float step = (enableSkipMode && Keyboard.current?[skipKey].isPressed == true) ? skipTypingSpeed : speed;
                dialogueText.maxVisibleCharacters = i;
                yield return new WaitForSeconds(step);
            }

            _isTyping = false;

            // Show choices if this line has any
            if (_currentLine?.choices != null && _currentLine.choices.Count > 0)
            {
                Debug.Log($"[MWM] Showing {_currentLine.choices.Count} choices");
                ShowChoices(_currentLine.choices);
            }
            else
            {
                Debug.Log($"[MWM] No choices for this line. choices={_currentLine?.choices}, count={_currentLine?.choices?.Count ?? 0}");
            }
        }

        #endregion

        #region Choice Methods

        private void ShowChoices(List<ChoiceData> choices)
        {
            _isWaitingForChoice = true;

            for (int i = 0; i < choiceButtons.Length; i++)
            {
                if (i < choices.Count)
                {
                    choiceButtons[i].gameObject.SetActive(true);
                    if (choiceButtonTexts != null && i < choiceButtonTexts.Length)
                        choiceButtonTexts[i].text = choices[i].choiceText;

                    int index = i;
                    choiceButtons[i].onClick.RemoveAllListeners();
                    choiceButtons[i].onClick.AddListener(() => OnChoiceSelected(index));
                }
                else
                {
                    choiceButtons[i].gameObject.SetActive(false);
                }
            }
        }

        private void HideAllChoices()
        {
            if (choiceButtons == null) return;
            foreach (var btn in choiceButtons)
                btn?.gameObject.SetActive(false);
        }

        #endregion

        #region Helper Methods

        private void BakePersistentColors(DialogueScenario scenario)
        {
            if (ClueManager.Instance == null || scenario == null) return;

            foreach (var line in scenario.lines)
            {
                if (string.IsNullOrEmpty(line.text)) continue;

                line.text = Regex.Replace(line.text, @"<a\s+href\s*=\s*""(.*?)""\s*>(.*?)</a>", m =>
                {
                    string id = m.Groups[1].Value;
                    string stripped = StripColorTags(m.Groups[2].Value);
                    string colorTag = ClueManager.Instance.IsClicked(id) ? "<color=#888888>" : "";
                    string closeTag = ClueManager.Instance.IsClicked(id) ? "</color>" : "";
                    return $"<a href=\"{id}\">{colorTag}{stripped}{closeTag}</a>";
                }, RegexOptions.Singleline);
            }
        }

        private void UpdatePortrait()
        {
            if (portraitImage == null) return;

            if (_currentLine.portrait != null)
            {
                portraitImage.sprite = _currentLine.portrait;
                portraitImage.gameObject.SetActive(true);
                if (portraitJumpOnText) PlayPortraitJump();
            }
            else
            {
                portraitImage.gameObject.SetActive(false);
            }
        }

        private void PlayEffects()
        {
            if (_currentLine.effects == null || EffectManager.Instance == null) return;
            foreach (var effect in _currentLine.effects)
                EffectManager.Instance.PlayEffect(effect);
        }

        private void AnimateSpeakerName(string newName)
        {
            if (!animateName || speakerNameText == null) return;
            if (string.Equals(newName, _previousSpeakerName, StringComparison.Ordinal)) return;

            var rt = speakerNameText.rectTransform;
            if (!_nameOriginalCaptured) { _nameOriginalAnchored = rt.anchoredPosition; _nameOriginalCaptured = true; }

            bool fromRight = _currentLine.nameSlideDirection switch
            {
                NameSlideDirection.Left => false,
                NameSlideDirection.Right => true,
                _ => false
            };

            float dir = fromRight ? 1f : -1f;
            rt.anchoredPosition = _nameOriginalAnchored + new Vector2(dir * nameSlideDistance, 0f);

            MoveWithEasing.MoveToAnchored(speakerNameText.gameObject, _nameOriginalAnchored, new MoveWithEasing.MoveOptions
            {
                duration = nameSlideDuration,
                ease = nameSlideEase,
                shakeOnComplete = false,
                endAlpha = 1f
            });
        }

        private void PlayPortraitJump()
        {
            var rect = portraitImage.GetComponent<RectTransform>();
            if (rect == null) return;

            var original = rect.anchoredPosition;
            var jumpTarget = original + Vector2.up * portraitJumpHeight;

            rect.DOAnchorPos(jumpTarget, portraitJumpDuration * 0.5f).SetEase(Ease.OutQuad);
            rect.DOAnchorPos(original, portraitJumpDuration * 0.5f).SetEase(portraitJumpEase).SetDelay(portraitJumpDuration * 0.5f);
        }

        private Camera GetUICamera()
        {
            var canvas = dialogueText.GetComponentInParent<Canvas>();
            return canvas.renderMode == RenderMode.ScreenSpaceCamera ? canvas.worldCamera : null;
        }

        private void RefreshDialogueText()
        {
            dialogueText.text = _currentLine.text;
            dialogueText.ForceMeshUpdate();
        }

        private static string BuildLinkPattern(string id) => $@"<a\s+href\s*=\s*""{Regex.Escape(id)}""\s*>(.*?)</a>";
        private static string StripColorTags(string content) => Regex.Replace(content, "</?color[^>]*>", "");

        private void CacheCharacterData(int start, int count, Color32[] colors, Vector3[][] vertices)
        {
            for (int i = 0; i < count; i++)
            {
                var charInfo = dialogueText.textInfo.characterInfo[start + i];
                var mesh = dialogueText.textInfo.meshInfo[charInfo.materialReferenceIndex];
                int vi = charInfo.vertexIndex;

                colors[i] = mesh.colors32[vi];
                vertices[i] = new[] { mesh.vertices[vi], mesh.vertices[vi + 1], mesh.vertices[vi + 2], mesh.vertices[vi + 3] };
            }
        }

        private void ApplyChargeVisuals(int start, int count, Color32[] origColors, Vector3[][] origVerts, Color32 target, float progress, float scale)
        {
            for (int i = 0; i < count; i++)
            {
                var charInfo = dialogueText.textInfo.characterInfo[start + i];
                if (!charInfo.isVisible) continue;

                var mesh = dialogueText.textInfo.meshInfo[charInfo.materialReferenceIndex];
                int vi = charInfo.vertexIndex;

                var c = Color32.Lerp(origColors[i], target, progress);
                mesh.colors32[vi] = mesh.colors32[vi + 1] = mesh.colors32[vi + 2] = mesh.colors32[vi + 3] = c;

                Vector3 center = (origVerts[i][0] + origVerts[i][2]) / 2;
                for (int v = 0; v < 4; v++)
                    mesh.vertices[vi + v] = center + (origVerts[i][v] - center) * scale;
            }

            dialogueText.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32 | TMP_VertexDataUpdateFlags.Vertices);
        }

        private void RestoreVertices(int start, int count, Vector3[][] origVerts)
        {
            for (int i = 0; i < count; i++)
            {
                var charInfo = dialogueText.textInfo.characterInfo[start + i];
                if (!charInfo.isVisible) continue;

                var mesh = dialogueText.textInfo.meshInfo[charInfo.materialReferenceIndex];
                int vi = charInfo.vertexIndex;

                for (int v = 0; v < 4; v++)
                    mesh.vertices[vi + v] = origVerts[i][v];
            }
            dialogueText.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }

        #endregion
    }
}