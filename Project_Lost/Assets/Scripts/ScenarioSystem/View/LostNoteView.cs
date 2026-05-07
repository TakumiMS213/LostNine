using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ScenarioSystem.Runtime;

namespace ScenarioSystem.View
{
    public class LostNoteView : MonoBehaviour
    {
        private const int MaxMemoCount = 3;
        private const float TitleFontSize = 87f;
        private const float DescriptionFontSize = 70f;

        [Header("UI References")]
        [SerializeField] private TMP_Text[] titleTexts = new TMP_Text[MaxMemoCount];
        [SerializeField] private TMP_Text[] descriptionTexts = new TMP_Text[MaxMemoCount];
        [SerializeField] private Image characterImage;
        [SerializeField] private TMP_Text characterNameText;
        [SerializeField] private TMP_Text characterDescriptionText;

        [Header("Update Notice")]
        [SerializeField] private TMP_Text updateNoticeText;
        [SerializeField] private AudioSource updateSeSource;
        [SerializeField] private AudioClip updateSeClip;
        [SerializeField] private float updateNoticeSeconds = 1.5f;

        private readonly bool[] _titleWarningLogged = new bool[MaxMemoCount];
        private readonly bool[] _descriptionWarningLogged = new bool[MaxMemoCount];
        private LostNoteManager _subscribedManager;
        private Coroutine _noticeCoroutine;
        private bool _managerWarningLogged;
        private bool _characterImageWarningLogged;
        private bool _characterNameWarningLogged;
        private bool _characterDescriptionWarningLogged;
        private bool _updateNoticeWarningLogged;

        private void Awake()
        {
            ApplyTextSettings();
            ClearMemo();
            HideUpdateNotice();
        }

        private void OnEnable()
        {
            TrySubscribeManager(true);
            RenderNotes();
            RenderCharacter();
        }

        private void Start()
        {
            TrySubscribeManager(false);
            RenderNotes();
            RenderCharacter();
        }

        private void Update()
        {
            TrySubscribeManager(false);
        }

        private void OnDisable()
        {
            StopNoticeCoroutine();
            UnsubscribeManager();
        }

        private void OnValidate()
        {
            ApplyTextSettings();
            HideUpdateNotice();
        }

        public void ClearMemo()
        {
            ClearTexts(titleTexts);
            ClearTexts(descriptionTexts);
        }

        private void RenderNotes()
        {
            ApplyTextSettings();
            ClearMemo();

            var manager = _subscribedManager != null ? _subscribedManager : LostNoteManager.Instance;
            if (manager == null)
                return;

            var notes = manager.Notes;
            int count = Mathf.Min(notes.Count, MaxMemoCount);
            for (int i = 0; i < count; i++)
            {
                SetSlotText(titleTexts, i, notes[i].Title, _titleWarningLogged, "titleTexts");
                SetSlotText(descriptionTexts, i, notes[i].Description, _descriptionWarningLogged, "descriptionTexts");
            }
        }

        private void RenderCharacter()
        {
            var manager = _subscribedManager != null ? _subscribedManager : LostNoteManager.Instance;
            if (manager == null)
                return;

            var state = manager.CharacterState;
            if (characterImage != null)
            {
                characterImage.sprite = state.CharacterSprite;
                characterImage.enabled = state.CharacterSprite != null;
            }
            else if (!_characterImageWarningLogged)
            {
                Debug.LogWarning("[LostNoteView] characterImage is not assigned.");
                _characterImageWarningLogged = true;
            }

            SetText(characterNameText, state.CharacterName, ref _characterNameWarningLogged, "characterNameText");
            SetText(characterDescriptionText, state.CharacterDescription, ref _characterDescriptionWarningLogged, "characterDescriptionText");
        }

        private void PlayUpdateNotice()
        {
            PlayUpdateSe();

            if (updateNoticeText == null)
            {
                if (!_updateNoticeWarningLogged)
                {
                    Debug.LogWarning("[LostNoteView] updateNoticeText is not assigned.");
                    _updateNoticeWarningLogged = true;
                }

                return;
            }

            StopNoticeCoroutine();
            // updateNoticeText.text = "\u30ce\u30fc\u30c8\u304c\u66f4\u65b0\u3055\u308c\u307e\3057\u305f\uff01";
            updateNoticeText.text = "  ノートが更新されました！";
            updateNoticeText.gameObject.SetActive(true);
            _noticeCoroutine = StartCoroutine(HideUpdateNoticeAfterDelay());
        }

        private void PlayUpdateSe()
        {
            if (updateSeSource == null)
                return;

            if (updateSeClip != null)
            {
                updateSeSource.PlayOneShot(updateSeClip);
                return;
            }

            if (updateSeSource.clip != null)
                updateSeSource.Play();
        }

        private IEnumerator HideUpdateNoticeAfterDelay()
        {
            yield return new WaitForSeconds(updateNoticeSeconds);
            HideUpdateNotice();
            _noticeCoroutine = null;
        }

        private void StopNoticeCoroutine()
        {
            if (_noticeCoroutine == null)
                return;

            StopCoroutine(_noticeCoroutine);
            _noticeCoroutine = null;
        }

        private void HideUpdateNotice()
        {
            if (updateNoticeText != null)
            {
                updateNoticeText.text = string.Empty;
                updateNoticeText.gameObject.SetActive(false);
            }
        }

        private static void ClearTexts(TMP_Text[] texts)
        {
            if (texts == null)
                return;

            foreach (var text in texts)
            {
                if (text != null)
                    text.text = string.Empty;
            }
        }

        private static void SetSlotText(TMP_Text[] texts, int index, string value, bool[] warningLogged, string fieldName)
        {
            if (texts == null || index < 0 || index >= texts.Length || texts[index] == null)
            {
                if (warningLogged != null && index >= 0 && index < warningLogged.Length && !warningLogged[index])
                {
                    Debug.LogWarning($"[LostNoteView] {fieldName}[{index}] is not assigned.");
                    warningLogged[index] = true;
                }

                return;
            }

            texts[index].text = value ?? string.Empty;
        }

        private static void SetText(TMP_Text text, string value, ref bool warningLogged, string fieldName)
        {
            if (text == null)
            {
                if (!warningLogged)
                {
                    Debug.LogWarning($"[LostNoteView] {fieldName} is not assigned.");
                    warningLogged = true;
                }

                return;
            }

            text.text = value ?? string.Empty;
        }

        private void ApplyTextSettings()
        {
            ApplyTextSettings(titleTexts, TitleFontSize, false);
            ApplyTextSettings(descriptionTexts, DescriptionFontSize, true);
        }

        private static void ApplyTextSettings(TMP_Text[] texts, float fontSize, bool enableAutoSizing)
        {
            if (texts == null)
                return;

            foreach (var text in texts)
            {
                if (text == null)
                    continue;

                text.enableAutoSizing = enableAutoSizing;
                text.fontSize = fontSize;
                text.textWrappingMode = TextWrappingModes.Normal;
            }
        }

        private void TrySubscribeManager(bool logWarning)
        {
            var manager = LostNoteManager.Instance;
            if (_subscribedManager == manager && _subscribedManager != null)
                return;

            UnsubscribeManager();

            if (manager == null)
            {
                if (logWarning && !_managerWarningLogged)
                {
                    Debug.LogWarning("[LostNoteView] LostNoteManager is not found.");
                    _managerWarningLogged = true;
                }

                return;
            }

            _subscribedManager = manager;
            _subscribedManager.OnNotesChanged += RenderNotes;
            _subscribedManager.OnCharacterChanged += RenderCharacter;
            _subscribedManager.OnContentUpdated += PlayUpdateNotice;
            RenderNotes();
            RenderCharacter();
        }

        private void UnsubscribeManager()
        {
            if (_subscribedManager == null)
                return;

            _subscribedManager.OnNotesChanged -= RenderNotes;
            _subscribedManager.OnCharacterChanged -= RenderCharacter;
            _subscribedManager.OnContentUpdated -= PlayUpdateNotice;
            _subscribedManager = null;
        }
    }
}
