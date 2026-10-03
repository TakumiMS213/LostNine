using System;
using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using ScenarioSystem.Adapter;
using ScenarioSystem.Events;

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
        [Tooltip("IDialogueProvider 実装。未設定時はシーン内の DialogueProviderAdapter を使用。")]
        [SerializeField] private MonoBehaviour dialogueProviderSource;

        [Header("Charge Settings")]
        [SerializeField] private float chargeDuration = 1.0f;

        [Header("Memorizer Keyword Shake")]
        [Tooltip("メモライザー起動中の文字の揺れ幅（テキストのローカル座標）。")]
        [SerializeField, Min(0f)] private float memorizerShakeStrength = 1.25f;
        [SerializeField, Min(0f)] private float memorizerShakeFrequency = 24f;

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

        /// <summary>Fired only when a keyword is newly extracted.</summary>
        public event Action<string> OnKeywordExtracted;

        /// <summary>Requests the manager to play a keyword scenario by ID.</summary>
        public event Action<string, Action> OnKeywordScenarioRequested;

        // Removed OnKeywordInteractionComplete

        #endregion

        #region Public Properties

        public bool IsKeywordEnabled => _isKeywordEnabled;
        public bool IsKeywordExtractionAvailable => _isKeywordEnabled && IsMemorizerActive;
        public bool IsCharging => _isCharging;

        #endregion

        #region Private Fields

        private IDialogueProvider _provider;
        private bool _isKeywordEnabled;
        private bool _isCharging;
        private bool _shouldBlockNext;
        private string _chargingLinkID;
        private Coroutine _chargeCoroutine;
        private int _chargingLinkIndex = -1;
        private float _chargeProgress;
        private TMP_Text _animatedText;
        private TMP_MeshInfo[] _baseMeshInfo;
        private TMP_Text _chargingText;
        private string _chargingTextSource;
        private Tween _discoveryShakeTween;
        private RectTransform _discoveryShakeTarget;
        private Vector2 _discoveryShakeOrigin;

        private const string DummyPrefix = "dummy_";
        private bool _isHoveringLink;

        #endregion

        #region Public Utility

        /// <summary>指定IDがダミーキーワードかどうかを判定する。</summary>
        public static bool IsDummyKeyword(string id) => !string.IsNullOrEmpty(id) && id.StartsWith(DummyPrefix);

        private static bool IsMemorizerActive => ProgressManager.Instance != null
            && ProgressManager.Instance.IsMemorizerActive;

        private bool CanInteractWithKeyword(string id) => IsKeywordExtractionAvailable
            && !string.IsNullOrWhiteSpace(id)
            && (ClueManager.Instance == null || !ClueManager.Instance.IsDiscovered(id));

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
        public void SetKeywordEnabled(bool enable)
        {
            _isKeywordEnabled = enable;
            if (!enable)
                CancelCharge();
        }

        #endregion

        #region Unity Lifecycle

        private void Awake()
        {
            ResolveProvider();
        }

        private void OnEnable()
        {
            ScenarioEventBus.OnDialogueRequested += HandleDialogueChanged;
            ScenarioEventBus.OnWindowVisibilityChanged += HandleWindowVisibility;
            ScenarioEventBus.OnScenarioStarted += HandleScenarioBoundary;
            ScenarioEventBus.OnScenarioEnded += HandleScenarioBoundary;
        }

        private void Update()
        {
            if (_isCharging && !CanContinueCharge())
                CancelCharge();

            UpdateCursorHover();
        }

        private void LateUpdate()
        {
            UpdateKeywordVisuals();
        }

        private void OnDisable()
        {
            ScenarioEventBus.OnDialogueRequested -= HandleDialogueChanged;
            ScenarioEventBus.OnWindowVisibilityChanged -= HandleWindowVisibility;
            ScenarioEventBus.OnScenarioStarted -= HandleScenarioBoundary;
            ScenarioEventBus.OnScenarioEnded -= HandleScenarioBoundary;
            CancelCharge();
            StopDiscoveryShake();
            ReleaseAnimatedText();
            SetKeywordHover(false);
        }

        /// <summary>Checks if the text contains any TMP link tags.</summary>
        public bool HasKeywordsInText(string text)
        {
            return KeywordTextFormatter.ContainsKeywords(text);
        }

        /// <summary>表示中かどうかにかかわらず、IDごとの表示色を保存する。</summary>
        public void SetLinkColor(string id, string colorHex)
        {
            if (ClueManager.Instance != null)
                ClueManager.Instance.SetKeywordColor(id, colorHex);
            else
                Debug.LogWarning("[KeywordHandler] ClueManager is required to retain keyword colors.");
        }

        /// <summary>状態と色をリセットする。表示は元の会話テキストから再生成される。</summary>
        public void ResetKeywordState(string id)
        {
            ClueManager.Instance?.ResetKeywordStatus(id);
        }

        /// <summary>Triggers a shake effect on the dialogue text.</summary>
        public void ShakeLinkVisual(string id)
        {
            StopDiscoveryShake();
            var provider = GetProvider();
            var text = provider?.DialogueText;
            if (text == null) return;

            _discoveryShakeTarget = text.rectTransform;
            _discoveryShakeOrigin = _discoveryShakeTarget.anchoredPosition;
            _discoveryShakeTween = _discoveryShakeTarget.DOShakeAnchorPos(0.35f, new Vector2(8f, 0f), 10, 90f)
                .SetLink(text.gameObject)
                .OnKill(RestoreDiscoveryShake);
        }

        private void StopDiscoveryShake()
        {
            var tween = _discoveryShakeTween;
            _discoveryShakeTween = null;
            tween?.Kill();
            RestoreDiscoveryShake();
        }

        private void RestoreDiscoveryShake()
        {
            if (_discoveryShakeTarget != null)
                _discoveryShakeTarget.anchoredPosition = _discoveryShakeOrigin;
            _discoveryShakeTarget = null;
            _discoveryShakeTween = null;
        }

        #endregion

        #region Pointer Handlers

        public void OnPointerDown(PointerEventData eventData)
        {
            _shouldBlockNext = false;

            var provider = GetProvider();
            if (provider == null || !provider.IsWindowActive || provider.IsTyping || !IsKeywordExtractionAvailable)
                return;

            var dialogueText = provider.DialogueText;
            if (dialogueText == null) return;

            Camera uiCamera = GetUICamera(dialogueText);
            int linkIndex = TMP_TextUtilities.FindIntersectingLink(dialogueText, eventData.position, uiCamera);
            if (linkIndex == -1) return;

            string linkID = dialogueText.textInfo.linkInfo[linkIndex].GetLinkID().Trim('"');
            if (_isCharging || !CanInteractWithKeyword(linkID)) return;

            _shouldBlockNext = true;
            _chargingLinkID = linkID;
            _chargingText = dialogueText;
            _chargingTextSource = dialogueText.text;
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
            _chargeCoroutine = null;
            _chargingLinkIndex = -1;
            _chargeProgress = 0f;

            if (!string.IsNullOrEmpty(_chargingLinkID))
            {
                // シーン破棄時にはProviderやTMPが先に破棄されている場合がある。
                // cleanup中にProviderを再検索せず、Unityのnull判定で生存確認する。
                var text = _chargingText;
                if (text == null && IsProviderAlive(_provider)) text = _provider.DialogueText;
                if (text != null) text.ForceMeshUpdate();
            }

            var effects = EffectManager.Instance;
            if (effects != null) effects.StopChargeSE();
            _chargingLinkID = null;
            _chargingText = null;
            _chargingTextSource = null;
        }

        private bool CanContinueCharge()
        {
            var provider = GetProvider();
            return IsKeywordExtractionAvailable && provider != null && provider.IsWindowActive
                && !provider.IsTyping && _chargingText != null && _chargingText.isActiveAndEnabled
                && provider.DialogueText == _chargingText && _chargingText.text == _chargingTextSource
                && CanInteractWithKeyword(_chargingLinkID);
        }

        private void HandleDialogueChanged(DialogueEventData _) => CancelInteraction();

        private void HandleScenarioBoundary(ScenarioSystem.Model.ScenarioData _) => CancelInteraction();

        private void HandleWindowVisibility(bool visible)
        {
            if (!visible) CancelInteraction();
        }

        private void CancelInteraction()
        {
            CancelCharge();
            StopDiscoveryShake();
            SetKeywordHover(false);
        }

        private IEnumerator ChargeRoutine(TMP_Text dialogueText, int linkIndex, string linkID)
        {
            EffectManager.Instance?.PlayChargeSE();
            _chargingLinkIndex = linkIndex;
            _chargeProgress = 0f;

            float timer = 0f;
            while (timer < chargeDuration)
            {
                timer += Time.deltaTime;
                _chargeProgress = Mathf.Clamp01(timer / chargeDuration);
                yield return null;
            }

            _isCharging = false;
            _chargeCoroutine = null;
            _chargingLinkID = null;
            _chargingText = null;
            _chargingTextSource = null;
            _chargingLinkIndex = -1;
            _chargeProgress = 0f;
            EffectManager.Instance?.StopChargeSE();

            // 長押し中に別経路で発見された場合も、取得処理を繰り返さない。
            if (!CanInteractWithKeyword(linkID))
            {
                dialogueText.ForceMeshUpdate();
                SetKeywordHover(false);
                yield break;
            }

            // 通知先から再入しても再クリックされないよう、先に状態を確定する。
            ClueManager.Instance?.ProcessKeywordClick(linkID);
            EffectManager.Instance?.PlayDevelopmentEffect();

            // Fire events
            OnKeywordClicked?.Invoke(linkID);

            bool shouldAddProgressAfterScenario = false;
            if (!IsDummyKeyword(linkID))
            {
                shouldAddProgressAfterScenario = ProgressManager.Instance != null;

                // 記憶の欠片システムへキーワード取得を通知（キーワードごとに1個ずつ生成）
                MemoryFragmentSystem.Instance?.AddFragmentForKeyword(linkID);
            }
            else
            {
                Debug.Log($"[KeywordHandler] Dummy keyword '{linkID}' — Progress not incremented.");
            }

            // キーワード抽出完了後、ClickArea を再有効化・カーソルをリセット
            SetKeywordHover(false);

            if (shouldAddProgressAfterScenario)
            {
                var handler = OnKeywordScenarioRequested;
                if (handler != null)
                {
                    handler.Invoke(linkID, () =>
                    {
                        if (TryAddKeywordProgress(linkID))
                            OnKeywordExtracted?.Invoke(linkID);
                    });
                }
                else if (TryAddKeywordProgress(linkID))
                {
                    OnKeywordExtracted?.Invoke(linkID);
                }
            }
            else
            {
                OnKeywordScenarioRequested?.Invoke(linkID, null);
            }
        }

        private bool TryAddKeywordProgress(string linkID)
        {
            var pm = ProgressManager.Instance;
            return pm != null && pm.AddKeyword(linkID);
        }

        #endregion

        #region Vertex Manipulation

        private void UpdateKeywordVisuals()
        {
            var provider = GetProvider();
            var text = provider?.DialogueText;
            if (!IsMemorizerActive || provider == null || !provider.IsWindowActive
                || text == null || !text.isActiveAndEnabled)
            {
                ReleaseAnimatedText();
                return;
            }

            if (_animatedText != text)
            {
                ReleaseAnimatedText();
                _animatedText = text;
                text.OnPreRenderText += HandleTextPreRender;
                text.ForceMeshUpdate();
            }
            else if (text.havePropertiesChanged)
                text.ForceMeshUpdate();

            if (_baseMeshInfo == null || text.textInfo.linkCount == 0)
                return;

            // 常にTMPが生成した元の位置から描画し、揺れ・拡大の累積を防ぐ。
            for (int i = 0; i < text.textInfo.meshInfo.Length; i++)
            {
                var mesh = text.textInfo.meshInfo[i];
                Array.Copy(_baseMeshInfo[i].vertices, mesh.vertices, mesh.vertexCount);
                Array.Copy(_baseMeshInfo[i].colors32, mesh.colors32, mesh.vertexCount);
            }
            ApplyKeywordVisuals(text.textInfo);
            text.UpdateVertexData(TMP_VertexDataUpdateFlags.Vertices | TMP_VertexDataUpdateFlags.Colors32);
        }

        private void HandleTextPreRender(TMP_TextInfo textInfo)
        {
            // タイピング・色変更・文章の差し替え・レイアウト更新にも追従する。
            // TMPの再利用バッファなので、毎フレームの配列生成は不要。
            _baseMeshInfo = textInfo.CopyMeshInfoVertexData();
            ApplyKeywordVisuals(textInfo);
        }

        private void ApplyKeywordVisuals(TMP_TextInfo textInfo)
        {
            if (!IsMemorizerActive || _provider == null || !_provider.IsWindowActive)
                return;

            float time = Time.unscaledTime * memorizerShakeFrequency * Mathf.PI * 2f;
            float chargeScale = Mathf.Lerp(1f, 1.5f,
                DOVirtual.EasedValue(0f, 1f, _chargeProgress, Ease.OutQuad));
            var chargeColor = new Color32(255, 215, 0, 255);
            for (int linkIndex = 0; linkIndex < textInfo.linkCount; linkIndex++)
            {
                var link = textInfo.linkInfo[linkIndex];
                bool charging = _isCharging && linkIndex == _chargingLinkIndex;
                int end = Mathf.Min(link.linkTextfirstCharacterIndex + link.linkTextLength,
                    Mathf.Min(textInfo.characterCount, _animatedText.maxVisibleCharacters));
                for (int i = link.linkTextfirstCharacterIndex; i < end; i++)
                {
                    var character = textInfo.characterInfo[i];
                    if (!character.isVisible) continue;

                    var mesh = textInfo.meshInfo[character.materialReferenceIndex];
                    int vertex = character.vertexIndex;
                    Vector3 center = (mesh.vertices[vertex] + mesh.vertices[vertex + 2]) * 0.5f;
                    Vector3 offset = new Vector3(Mathf.Sin(time + i * 2.4f),
                        Mathf.Sin(time * 1.17f + i * 1.3f), 0f) * memorizerShakeStrength;
                    Color32 color = Color32.Lerp(mesh.colors32[vertex], chargeColor, _chargeProgress);
                    for (int v = 0; v < 4; v++)
                    {
                        if (charging)
                        {
                            mesh.vertices[vertex + v] = center + (mesh.vertices[vertex + v] - center) * chargeScale;
                            mesh.colors32[vertex + v] = color;
                        }
                        mesh.vertices[vertex + v] += offset;
                    }
                }
            }
        }

        private void ReleaseAnimatedText()
        {
            if (_animatedText != null)
            {
                _animatedText.OnPreRenderText -= HandleTextPreRender;
                _animatedText.ForceMeshUpdate(true);
            }
            _animatedText = null;
            _baseMeshInfo = null;
        }

        #endregion

        #region Cursor Hover

        /// <summary>
        /// 毎フレーム、マウスが TMP リンクタグ上にあるかチェックし、
        /// CursorManager 経由でカーソル画像を切り替える。
        /// 未発見で操作可能なリンクに反応する。
        /// </summary>
        private void UpdateCursorHover()
        {
            var provider = GetProvider();
            if (provider == null || !provider.IsWindowActive || provider.IsTyping || !IsKeywordExtractionAvailable)
            {
                SetKeywordHover(false);
                return;
            }

            var dialogueText = provider.DialogueText;
            if (dialogueText == null)
            {
                SetKeywordHover(false);
                return;
            }

            // Input System 経由でマウス位置を取得
            Vector2 mousePos = Mouse.current != null ? Mouse.current.position.ReadValue() : Input.mousePosition;

            Camera uiCamera = GetUICamera(dialogueText);
            int linkIndex = TMP_TextUtilities.FindIntersectingLink(dialogueText, mousePos, uiCamera);

            bool canHover = linkIndex != -1
                && CanInteractWithKeyword(dialogueText.textInfo.linkInfo[linkIndex].GetLinkID().Trim('"'));
            SetKeywordHover(canHover);
        }

        private void SetKeywordHover(bool hovering)
        {
            if (clickAreaGraphic != null) clickAreaGraphic.raycastTarget = !hovering;
            if (_isHoveringLink == hovering) return;

            _isHoveringLink = hovering;
            var cursor = CursorManager.Instance;
            if (cursor == null) return;
            if (hovering && keywordHoverCursor != null)
            {
                cursor.SetCursor(keywordHoverCursor, keywordHoverHotspot);
            }
            else
            {
                cursor.ResetToDefault();
            }
        }

        #endregion

        #region Utility

        /// <summary>
        /// IDialogueProvider を解決する。
        /// SerializedField から注入されていればそれを使い、
        /// なければシーン内の DialogueProviderAdapter を使用する。
        /// </summary>
        private IDialogueProvider GetProvider()
        {
            if (IsProviderAlive(_provider)) return _provider;
            _provider = null;
            ResolveProvider();
            return _provider;
        }

        private static bool IsProviderAlive(IDialogueProvider provider) => provider != null
            && (!(provider is UnityEngine.Object unityObject) || unityObject != null);

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

        #endregion

    }
}
