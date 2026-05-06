using UnityEngine;
using MessageWindowSystem.Core;
using ScenarioSystem.Events;

namespace ScenarioSystem.Adapter
{
    public class LostNoteKeywordResolver : MonoBehaviour
    {
        [SerializeField] private KeywordHandler keywordHandler;
        [SerializeField] private KeyWordDatabase keyWordDatabase;

        private bool _databaseWarningLogged;
        private bool _handlerWarningLogged;

        private void OnEnable()
        {
            ResolveKeywordHandler();

            if (keywordHandler != null)
            {
                keywordHandler.OnKeywordExtracted += HandleKeywordExtracted;
            }
            else if (!_handlerWarningLogged)
            {
                Debug.LogWarning("[LostNoteKeywordResolver] KeywordHandler is not assigned and could not be resolved.");
                _handlerWarningLogged = true;
            }
        }

        private void OnDisable()
        {
            if (keywordHandler != null)
                keywordHandler.OnKeywordExtracted -= HandleKeywordExtracted;
        }

        private void HandleKeywordExtracted(string keywordId)
        {
            if (keyWordDatabase == null)
            {
                if (!_databaseWarningLogged)
                {
                    Debug.LogWarning("[LostNoteKeywordResolver] KeyWordDatabase is not assigned.");
                    _databaseWarningLogged = true;
                }

                return;
            }

            string normalizedKeywordId = keywordId?.Trim();
            if (!keyWordDatabase.TryGetById(normalizedKeywordId, out var note) || note == null)
                return;

            ScenarioEventBus.RaiseLostNoteRequested(normalizedKeywordId, note.Title, note.Description);
        }

        private void ResolveKeywordHandler()
        {
            if (keywordHandler != null)
                return;

            keywordHandler = FindFirstObjectByType<KeywordHandler>();
        }
    }
}
