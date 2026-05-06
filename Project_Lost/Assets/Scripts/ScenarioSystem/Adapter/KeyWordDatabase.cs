using System;
using System.Collections.Generic;
using UnityEngine;
using ScenarioSystem.Model;

namespace ScenarioSystem.Adapter
{
    [CreateAssetMenu(fileName = "KeyWordDatabase", menuName = "Scenario/KeyWord Database")]
    public class KeyWordDatabase : ScriptableObject
    {
        [Tooltip("LostNote data resolved by keyword ID.")]
        [SerializeField] private List<LostNoteData> allLostNotes = new();

        private Dictionary<string, LostNoteData> _map;

        private void OnEnable()
        {
            BuildMap();
        }

        public void BuildMap()
        {
            _map = new Dictionary<string, LostNoteData>(StringComparer.Ordinal);

            foreach (var note in allLostNotes)
            {
                if (note == null)
                    continue;

                string keywordId = note.KeywordId?.Trim();
                if (string.IsNullOrEmpty(keywordId))
                    continue;

                if (!_map.ContainsKey(keywordId))
                {
                    _map.Add(keywordId, note);
                }
                else
                {
                    Debug.LogWarning($"[KeyWordDatabase] Duplicate ID: {keywordId} in {note.name}");
                }
            }
        }

        public LostNoteData GetById(string id)
        {
            if (_map == null) BuildMap();

            string keywordId = id?.Trim();
            if (!string.IsNullOrEmpty(keywordId) && _map.TryGetValue(keywordId, out var note))
                return note;

            Debug.LogWarning($"[KeyWordDatabase] LostNoteData '{id}' not found.");
            return null;
        }

        public bool TryGetById(string id, out LostNoteData note)
        {
            if (_map == null) BuildMap();

            note = null;
            string keywordId = id?.Trim();
            if (!string.IsNullOrEmpty(keywordId) && _map.TryGetValue(keywordId, out note))
                return true;

            Debug.LogWarning($"[KeyWordDatabase] LostNoteData '{id}' not found.");
            return false;
        }
    }
}
