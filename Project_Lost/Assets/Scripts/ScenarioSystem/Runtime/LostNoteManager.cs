using System;
using System.Collections.Generic;
using ScenarioSystem.Adapter;
using UnityEngine;
using ScenarioSystem.Events;
using ScenarioSystem.Model;

namespace ScenarioSystem.Runtime
{
    public class LostNoteManager : MonoBehaviour
    {
        public static LostNoteManager Instance { get; private set; }

        private const int MaxMemoCount = 3;

        private readonly List<LostNoteMemo> _notes = new(MaxMemoCount);
        private readonly HashSet<string> _registeredKeywordIds = new(StringComparer.Ordinal);

        [SerializeField] private LostNoteCharacterDatabase characterDatabase;

        private global::ProgressManager _progressManager;
        private int _lastChapter = -1;
        private LostNoteCharacterState _characterState;

        public IReadOnlyList<LostNoteMemo> Notes => _notes;
        public LostNoteCharacterState CharacterState => _characterState;
        public event Action OnNotesChanged;
        public event Action OnCharacterChanged;
        public event Action OnContentUpdated;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                return;
            }

            Destroy(gameObject);
        }

        private void OnEnable()
        {
            ScenarioEventBus.OnLostNoteRequested += HandleLostNoteRequested;
            TrySubscribeProgressManager();
        }

        private void Update()
        {
            TrySubscribeProgressManager();
        }

        private void OnDisable()
        {
            ScenarioEventBus.OnLostNoteRequested -= HandleLostNoteRequested;
            UnsubscribeProgressManager();
        }

        public bool TryAddMemo(string keywordId, string title, string description)
        {
            string normalizedKeywordId = keywordId?.Trim();
            if (string.IsNullOrEmpty(normalizedKeywordId))
            {
                Debug.LogWarning("[LostNoteManager] Keyword ID is empty. Memo was not added.");
                return false;
            }

            if (string.IsNullOrWhiteSpace(title) && string.IsNullOrWhiteSpace(description))
            {
                Debug.LogWarning($"[LostNoteManager] LostNote text is empty for keyword '{normalizedKeywordId}'. Memo was not added.");
                return false;
            }

            if (_registeredKeywordIds.Contains(normalizedKeywordId))
                return false;

            if (_notes.Count >= MaxMemoCount)
            {
                Debug.LogWarning("[LostNoteManager] LostNote memo limit reached. Ignored additional memo.");
                return false;
            }

            _notes.Add(new LostNoteMemo(normalizedKeywordId, title, description));
            _registeredKeywordIds.Add(normalizedKeywordId);
            OnNotesChanged?.Invoke();
            OnContentUpdated?.Invoke();
            return true;
        }

        public void ClearNotes()
        {
            if (_notes.Count == 0)
                return;

            _notes.Clear();
            _registeredKeywordIds.Clear();
            OnNotesChanged?.Invoke();
        }

        public void UpdateCharacterText(string characterName, string characterDescription)
        {
            UpdateCharacter(_characterState.CharacterSprite, characterName, characterDescription);
        }

        public void UpdateCharacter(Sprite characterSprite, string characterName, string characterDescription)
        {
            string normalizedName = characterName ?? string.Empty;
            string normalizedDescription = characterDescription ?? string.Empty;
            if (_characterState.CharacterSprite == characterSprite
                && _characterState.CharacterName == normalizedName
                && _characterState.CharacterDescription == normalizedDescription)
            {
                return;
            }

            _characterState = new LostNoteCharacterState(
                characterSprite,
                normalizedName,
                normalizedDescription);

            OnCharacterChanged?.Invoke();
            OnContentUpdated?.Invoke();
        }

        private void HandleLostNoteRequested(LostNoteEventData data)
        {
            TryAddMemo(data.KeywordId, data.Title, data.Description);
        }

        private void HandleProgressChanged()
        {
            if (_progressManager == null)
                return;

            int currentChapter = _progressManager.CurrentChapter;
            if (_lastChapter >= 0 && currentChapter != _lastChapter)
                ResetForChapter(currentChapter);

            _lastChapter = currentChapter;
        }

        private void ResetForChapter(int chapter)
        {
            ClearNotes();
            LoadCharacterForChapter(chapter, true);
        }

        private void LoadCharacterForChapter(int chapter, bool notify)
        {
            var previousState = _characterState;

            if (characterDatabase != null && characterDatabase.TryGetByChapter(chapter, out var data))
            {
                _characterState = new LostNoteCharacterState(
                    data.CharacterSprite,
                    data.CharacterName,
                    data.CharacterDescription);
            }
            else
            {
                _characterState = new LostNoteCharacterState(null, string.Empty, string.Empty);
            }

            OnCharacterChanged?.Invoke();
            if (notify && !IsSameCharacterState(previousState, _characterState))
                OnContentUpdated?.Invoke();
        }

        private static bool IsSameCharacterState(LostNoteCharacterState left, LostNoteCharacterState right)
        {
            return left.CharacterSprite == right.CharacterSprite
                && left.CharacterName == right.CharacterName
                && left.CharacterDescription == right.CharacterDescription;
        }

        private void TrySubscribeProgressManager()
        {
            if (_progressManager == global::ProgressManager.Instance && _progressManager != null)
                return;

            UnsubscribeProgressManager();

            _progressManager = global::ProgressManager.Instance;
            if (_progressManager == null)
                return;

            _lastChapter = _progressManager.CurrentChapter;
            _progressManager.OnProgressChanged += HandleProgressChanged;
            LoadCharacterForChapter(_lastChapter, false);
        }

        private void UnsubscribeProgressManager()
        {
            if (_progressManager == null)
                return;

            _progressManager.OnProgressChanged -= HandleProgressChanged;
            _progressManager = null;
        }
    }
}
