using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using ScenarioSystem.Adapter;

namespace MessageWindowSystem.Core
{
    /// <summary>
    /// Handles all keyword-related interactions: pointer detection, charge animation,
    /// link color manipulation, and keyword conversation requests.
    /// Attach to the same GameObject as (or as a child of) the dialogue text area.
    /// </summary>
    public class KeywordHandler : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
    {
        #region Serialized Fields

        [Header("Dialogue Provider")]
        [Tooltip("IDialogueProvider 実装。未設定時は旧 MessageWindowManager.Instance にフォールバック。")]
        [SerializeField] private MonoBehaviour dialogueProviderSource;

        [Header("Charge Settings")]
        [SerializeField] private float chargeDuration = 1.0f;

        [Header("Keyword Hover Cursor")]
        [Tooltip("キーワード上にカーソルが重なったときに表示するカーソル画像。")]
        [SerializeField] private Texture2D keywordHoverCursor;

        [Tooltip("ホバーカーソルのクリック位置オフセット（左上からのピクセル数）。")]
        [SerializeField] private Vector2 keywordHoverHotspot = Vector2.zero;

        [Header("Click Area")]
        [Tooltip("キーワードホバー中に raycastTarget を無効にする ClickArea の Graphic。")]
        [SerializeField] private Graphic clickAreaGraphic;

        #endregion

        #region Events

        /// <summary>Fired when a keyword is successfully charged and clicked.</summary>
        public event Action<string> OnKeywordClicked;

        /// <summary>Requests the manager to play a keyword scenario by ID.</summary>
        public event Action<string> OnKeywordScenarioRequested;

        // Removed OnKeywordInteractionComplete

        #endregion

        #region Public Properties

        public bool IsKeywordEnabled => _isKeywordEnabled;
        public bool IsCharging => _isCharging;

        #endregion

        #region Private Fields

        private IDialogueProvider _provider;
        private bool _isKeywordEnabled;
        private bool _isCharging;
        private bool _shouldBlockNext;
        private string _chargingLinkID;
        private Coroutine _chargeCoroutine;

        private const string DummyPrefix = "dummy_";
        private bool _isHoveringLink;

        #endregion

        #region Public Utility

        /// <summary>指定IDがダミーキーワードかどうかを判定する。</summary>
        public static bool IsDummyKeyword(string id) => !string.IsNullOrEmpty(id) && id.StartsWith(DummyPrefix);

        #endregion

        #region Public API

        /// <summary>Initializes keyword state for a new scenario.</summary>
        public void Initialize(bool enableKeywords)
        {
            _isKeywordEnabled = enableKeywords;
            CancelCharge();
            _shouldBlockNext = false;
        }

        /// <summary>Sets keyword enabled state at runtime.</summary>
        public void SetKeywordEnabled(bool enable) => _isKeywordEnabled = enable;

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            ResolveProvider();
        }

        private void Update()
        {
            UpdateCursorHover();
        }

        private void OnDisable()
        {
            // Restore default cursor when handler is disabled
            if (_isHoveringLink)
            {
                _isHoveringLink = false;
                CursorManager.Instance?.ResetToDefault();
            }
        }

        /// <summary>Checks if the text contains any TMP link tags.</summary>
        public bool HasKeywordsInText(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return Regex.IsMatch(text, @"<(?:a\s+href|link)\s*=\s*"".*?""\s*>", RegexOptions.Singleline);
        }

        /// <summary>Sets the color of a keyword link across all lines in the scenario.</summary>
        public void SetLinkColor(string id, string colorHex)
        {
            var provider = GetProvider();
            if (provider == null || provider.DialogueText == null || string.IsNullOrEmpty(id)) return;

            string currentText = provider.CurrentText;
            if (string.IsNullOrEmpty(currentText)) return;

            string pattern = BuildLinkPattern(id);
            if (!Regex.IsMatch(currentText, pattern)) return;

            string newText = Regex.Replace(currentText, pattern, m =>
            {
                string stripped = StripColorTags(m.Groups[1].Value);
                return $"<link=\"{id}\"><color={colorHex}>{stripped}</color></link>";
            }, RegexOptions.Singleline);

            if (currentText != newText)
            {
                provider.UpdateCurrentText(newText);
            }
        }

        /// <summary>Resets a keyword's visual and discovery state.</summary>
        public void ResetKeywordState(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            ClueManager.Instance?.ResetKeywordStatus(id);

            var provider = GetProvider();
            if (provider == null) return;

            string currentText = provider.CurrentText;
            if (string.IsNullOrEmpty(currentText)) return;

            string pattern = BuildLinkPattern(id);
            if (!Regex.IsMatch(currentText, pattern)) return;

            string newText = Regex.Replace(currentText, pattern, m =>
            {
                string stripped = StripColorTags(m.Groups[1].Value);
                return $"<link=\"{id}\">{stripped}</link>";
            }, RegexOptions.Singleline);

            if (currentText != newText)
            {
                provider.UpdateCurrentText(newText);
            }
        }

        /// <summary>Triggers a shake effect on the dialogue text.</summary>
        public void ShakeLinkVisual(string id)
        {
            var provider = GetProvider();
            provider?.DialogueText?.GetComponent<RectTransform>()?.DOShakeAnchorPos(0.35f, new Vector2(8f, 0f), 10, 90f);
        }

        #endregion

        #region Pointer Handlers

        public void OnPointerDown(PointerEventData eventData)
        {
            _shouldBlockNext = false;

            var provider = GetProvider();
            if (provider == null || !provider.IsWindowActive || provider.IsTyping || !_isKeywordEnabled)
                return;

            var dialogueText = provider.DialogueText;
            if (dialogueText == null) return;

            Camera uiCamera = GetUICamera(dialogueText);
            int linkIndex = TMP_TextUtilities.FindIntersectingLink(dialogueText, eventData.position, uiCamera);
            if (linkIndex == -1) return;

            _shouldBlockNext = true;
            _chargingLinkID = dialogueText.textInfo.linkInfo[linkIndex].GetLinkID().Trim('"');
            _isCharging = true;
            _chargeCoroutine = StartCoroutine(ChargeRoutine(dialogueText, linkIndex, _chargingLinkID));
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_isCharging) CancelCharge();
        }

        /// <summary>Returns true if a pointer down event should block the Next() call.</summary>
        public bool ConsumeBlockNext()
        {
            if (_shouldBlockNext)
            {
                _shouldBlockNext = false;
                return true;
            }
            return false;
        }

        #endregion

        #region Charge Logic

        private void CancelCharge()
        {
            _isCharging = false;
            if (_chargeCoroutine != null) StopCoroutine(_chargeCoroutine);

            if (!string.IsNullOrEmpty(_chargingLinkID))
            {
                var provider = GetProvider();
                provider?.DialogueText?.ForceMeshUpdate();
            }

            EffectManager.Instance?.StopChargeSE();
            _chargingLinkID = null;
        }

        private IEnumerator ChargeRoutine(TMP_Text dialogueText, int linkIndex, string linkID)
        {
            EffectManager.Instance?.PlayChargeSE();

            var linkInfo = dialogueText.textInfo.linkInfo[linkIndex];
            int startCharIdx = linkInfo.linkTextfirstCharacterIndex;
            int charCount = linkInfo.linkTextLength;

            var originalColors = new Color32[charCount];
            var originalVertices = new Vector3[charCount][];
            CacheCharacterData(dialogueText, startCharIdx, charCount, originalColors, originalVertices);

            Color32 targetColor = new Color32(255, 215, 0, 255);
            const float maxScale = 1.5f;

            float timer = 0f;
            while (timer < chargeDuration)
            {
                timer += Time.deltaTime;
                float progress = timer / chargeDuration;
                float scale = Mathf.Lerp(1f, maxScale, DOVirtual.EasedValue(0f, 1f, progress, Ease.OutQuad));
                ApplyChargeVisuals(dialogueText, startCharIdx, charCount, originalColors, originalVertices, targetColor, progress, scale);
                yield return null;
            }

            RestoreVertices(dialogueText, startCharIdx, charCount, originalVertices);

            _isCharging = false;
            EffectManager.Instance?.StopChargeSE();
            EffectManager.Instance?.PlayDevelopmentEffect();

            // Fire events
            OnKeywordClicked?.Invoke(linkID);
            ClueManager.Instance?.ProcessKeywordClick(linkID);

            bool thresholdReachedThisTime = false;
            if (!IsDummyKeyword(linkID))
            {
                var pm = ProgressManager.Instance;
                if (pm != null)
                {
                    int beforeProgress = pm.CurrentKeywordProgress;
                    pm.AddKeyword();
                    
                    // If the progress just hit the threshold, GameFlowDirector will take over the sequence.
                    // We must NOT play the individual keyword scenario, otherwise they collide.
                    if (pm.CurrentKeywordProgress >= pm.KeywordThreshold && beforeProgress < pm.KeywordThreshold)
                    {
                        thresholdReachedThisTime = true;
                    }
                }
            }
            else
            {
                Debug.Log($"[KeywordHandler] Dummy keyword '{linkID}' — Progress not incremented.");
            }

            // キーワード抽出完了後、ClickArea を再有効化
            if (clickAreaGraphic != null) clickAreaGraphic.raycastTarget = true;
            _isHoveringLink = false;

            // Only request individual scenario if the main sequence override didn't trigger
            if (!thresholdReachedThisTime)
            {
                OnKeywordScenarioRequested?.Invoke(linkID);
            }
        }

        #endregion

        #region Vertex Manipulation

        private static void CacheCharacterData(TMP_Text text, int start, int count, Color32[] colors, Vector3[][] vertices)
        {
            for (int i = 0; i < count; i++)
            {
                var charInfo = text.textInfo.characterInfo[start + i];
                if (!charInfo.isVisible) continue;

                var mesh = text.textInfo.meshInfo[charInfo.materialReferenceIndex];
                int vi = charInfo.vertexIndex;

                colors[i] = mesh.colors32[vi];
                vertices[i] = new[] { mesh.vertices[vi], mesh.vertices[vi + 1], mesh.vertices[vi + 2], mesh.vertices[vi + 3] };
            }
        }

        private static void ApplyChargeVisuals(TMP_Text text, int start, int count, Color32[] origColors, Vector3[][] origVerts, Color32 target, float progress, float scale)
        {
            for (int i = 0; i < count; i++)
            {
                var charInfo = text.textInfo.characterInfo[start + i];
                if (!charInfo.isVisible || origVerts[i] == null) continue;

                var mesh = text.textInfo.meshInfo[charInfo.materialReferenceIndex];
                int vi = charInfo.vertexIndex;

                var c = Color32.Lerp(origColors[i], target, progress);
                mesh.colors32[vi] = mesh.colors32[vi + 1] = mesh.colors32[vi + 2] = mesh.colors32[vi + 3] = c;

                Vector3 center = (origVerts[i][0] + origVerts[i][2]) / 2;
                for (int v = 0; v < 4; v++)
                    mesh.vertices[vi + v] = center + (origVerts[i][v] - center) * scale;
            }

            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Colors32 | TMP_VertexDataUpdateFlags.Vertices);
        }

        private static void RestoreVertices(TMP_Text text, int start, int count, Vector3[][] origVerts)
        {
            for (int i = 0; i < count; i++)
            {
                var charInfo = text.textInfo.characterInfo[start + i];
                if (!charInfo.isVisible || origVerts[i] == null) continue;

                var mesh = text.textInfo.meshInfo[charInfo.materialReferenceIndex];
                int vi = charInfo.vertexIndex;

                for (int v = 0; v < 4; v++)
                    mesh.vertices[vi + v] = origVerts[i][v];
            }
            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices);
        }

        #endregion

        #region Cursor Hover

        /// <summary>
        /// 毎フレーム、マウスが TMP リンクタグ上にあるかチェックし、
        /// CursorManager 経由でカーソル画像を切り替える。
        /// 当たり／ハズレ／ダミーに関係なく全リンクに反応する。
        /// </summary>
        private void UpdateCursorHover()
        {
            if (CursorManager.Instance == null || keywordHoverCursor == null) return;

            var provider = GetProvider();
            if (provider == null || !provider.IsWindowActive || provider.IsTyping)
            {
                if (_isHoveringLink)
                {
                    _isHoveringLink = false;
                    CursorManager.Instance.ResetToDefault();
                }
                return;
            }

            var dialogueText = provider.DialogueText;
            if (dialogueText == null)
            {
                if (_isHoveringLink)
                {
                    _isHoveringLink = false;
                    CursorManager.Instance.ResetToDefault();
                }
                return;
            }

            // Input System 経由でマウス位置を取得
            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Input.mousePosition;

            Camera uiCamera = GetUICamera(dialogueText);
            int linkIndex = TMP_TextUtilities.FindIntersectingLink(dialogueText, mousePos, uiCamera);

            if (linkIndex != -1)
            {
                if (!_isHoveringLink)
                {
                    _isHoveringLink = true;
                    CursorManager.Instance.SetCursor(keywordHoverCursor, keywordHoverHotspot);
                    if (clickAreaGraphic != null) clickAreaGraphic.raycastTarget = false;
                }
            }
            else
            {
                if (_isHoveringLink)
                {
                    _isHoveringLink = false;
                    CursorManager.Instance.ResetToDefault();
                    if (clickAreaGraphic != null) clickAreaGraphic.raycastTarget = true;
                }
            }
        }

        #endregion

        #region Utility

        /// <summary>
        /// IDialogueProvider を解決する。
        /// SerializedField から注入されていればそれを使い、
        /// なければ旧 MessageWindowManager.Instance にフォールバック。
        /// </summary>
        private IDialogueProvider GetProvider()
        {
            if (_provider != null) return _provider;
            ResolveProvider();
            return _provider;
        }

        private void ResolveProvider()
        {
            // 1. Inspector から注入された MonoBehaviour
            if (dialogueProviderSource != null && dialogueProviderSource is IDialogueProvider injected)
            {
                _provider = injected;
                return;
            }

            var adapter = FindFirstObjectByType<DialogueProviderAdapter>();
            if (adapter != null)
            {
                _provider = adapter;
                return;
            }

            // 3. フォールバック廃止
            Debug.LogError("[KeywordHandler] IDialogueProvider was not found. Legacy fallback has been removed.");
        }

        private static Camera GetUICamera(TMP_Text text)
        {
            var canvas = text.GetComponentInParent<Canvas>();
            return canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera ? canvas.worldCamera : null;
        }

        private static string BuildLinkPattern(string id) => $@"<(?:a\s+href|link)\s*=\s*""{Regex.Escape(id)}""\s*>(.*?)</(?:a|link)>";
        private static string StripColorTags(string content) => Regex.Replace(content, "</?color[^>]*>", "");

        #endregion

        #region Legacy Wrapper

        // Legacy wrapper removed

        #endregion
    }
}
