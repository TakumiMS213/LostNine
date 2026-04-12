using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;
using DG.Tweening;
using MessageWindowSystem.Data;

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

        [Header("Charge Settings")]
        [SerializeField] private float chargeDuration = 1.0f;

        #endregion

        #region Events

        /// <summary>Fired when a keyword is successfully charged and clicked.</summary>
        public event Action<string> OnKeywordClicked;

        /// <summary>Requests the manager to play a keyword scenario by ID.</summary>
        public event Action<string> OnKeywordScenarioRequested;

        /// <summary>Fired when keyword interaction is complete (for last-line waiting).</summary>
        public event Action OnKeywordInteractionComplete;

        #endregion

        #region Public Properties

        public bool IsKeywordEnabled => _isKeywordEnabled;
        public bool IsCharging => _isCharging;

        #endregion

        #region Private Fields

        private bool _isKeywordEnabled;
        private bool _isCharging;
        private bool _shouldBlockNext;
        private string _chargingLinkID;
        private Coroutine _chargeCoroutine;

        private const string DummyPrefix = "dummy_";

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

        /// <summary>Checks if the text contains any TMP link tags.</summary>
        public bool HasKeywordsInText(string text)
        {
            if (string.IsNullOrEmpty(text)) return false;
            return Regex.IsMatch(text, @"<(?:a\s+href|link)\s*=\s*"".*?""\s*>", RegexOptions.Singleline);
        }

        /// <summary>Sets the color of a keyword link across all lines in the scenario.</summary>
        public void SetLinkColor(string id, string colorHex)
        {
            var manager = MessageWindowManager.Instance;
            if (manager == null || manager.DialogueText == null || string.IsNullOrEmpty(id)) return;

            // Only modify the currently displayed line's visual
            var currentLine = manager.CurrentLine;
            if (currentLine == null || string.IsNullOrEmpty(currentLine.text)) return;

            string pattern = BuildLinkPattern(id);
            if (!Regex.IsMatch(currentLine.text, pattern)) return;

            string newText = Regex.Replace(currentLine.text, pattern, m =>
            {
                string stripped = StripColorTags(m.Groups[1].Value);
                return $"<link=\"{id}\"><color={colorHex}>{stripped}</color></link>";
            }, RegexOptions.Singleline);

            if (currentLine.text != newText)
            {
                currentLine.text = newText;
                manager.DialogueText.text = currentLine.text;
                manager.DialogueText.ForceMeshUpdate();
            }
        }

        /// <summary>Resets a keyword's visual and discovery state.</summary>
        public void ResetKeywordState(string id)
        {
            if (string.IsNullOrEmpty(id)) return;
            ClueManager.Instance?.ResetKeywordStatus(id);

            var manager = MessageWindowManager.Instance;
            if (manager == null) return;

            var currentLine = manager.CurrentLine;
            if (currentLine == null || string.IsNullOrEmpty(currentLine.text)) return;

            string pattern = BuildLinkPattern(id);
            if (!Regex.IsMatch(currentLine.text, pattern)) return;

            string newText = Regex.Replace(currentLine.text, pattern, m =>
            {
                string stripped = StripColorTags(m.Groups[1].Value);
                return $"<link=\"{id}\">{stripped}</link>";
            }, RegexOptions.Singleline);

            if (currentLine.text != newText)
            {
                currentLine.text = newText;
                manager.DialogueText.text = currentLine.text;
                manager.DialogueText.ForceMeshUpdate();
            }
        }

        /// <summary>Triggers a shake effect on the dialogue text.</summary>
        public void ShakeLinkVisual(string id)
        {
            var manager = MessageWindowManager.Instance;
            manager?.DialogueText?.GetComponent<RectTransform>()?.DOShakeAnchorPos(0.35f, new Vector2(8f, 0f), 10, 90f);
        }

        #endregion

        #region Pointer Handlers

        public void OnPointerDown(PointerEventData eventData)
        {
            _shouldBlockNext = false;

            var manager = MessageWindowManager.Instance;
            if (manager == null || !manager.IsWindowActive || manager.IsTyping || !_isKeywordEnabled)
                return;

            var dialogueText = manager.DialogueText;
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
                var manager = MessageWindowManager.Instance;
                manager?.DialogueText?.ForceMeshUpdate();
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

                // 記憶の欠片システムへキーワード取得を通知（キーワードごとに1個ずつ生成）
                MemoryFragmentSystem.Instance?.AddFragmentForKeyword(linkID);
            }
            else
            {
                Debug.Log($"[KeywordHandler] Dummy keyword '{linkID}' — Progress not incremented.");
            }

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

        #region Utility

        private static Camera GetUICamera(TMP_Text text)
        {
            var canvas = text.GetComponentInParent<Canvas>();
            return canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera ? canvas.worldCamera : null;
        }

        private static string BuildLinkPattern(string id) => $@"<(?:a\s+href|link)\s*=\s*""{Regex.Escape(id)}""\s*>(.*?)</(?:a|link)>";
        private static string StripColorTags(string content) => Regex.Replace(content, "</?color[^>]*>", "");

        #endregion
    }
}
