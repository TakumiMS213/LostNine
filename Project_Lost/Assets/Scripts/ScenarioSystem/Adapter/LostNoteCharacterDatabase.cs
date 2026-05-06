using System.Collections.Generic;
using ScenarioSystem.Model;
using UnityEngine;

namespace ScenarioSystem.Adapter
{
    [CreateAssetMenu(fileName = "LostNoteCharacterDatabase", menuName = "Scenario/Lost Note Character Database")]
    public class LostNoteCharacterDatabase : ScriptableObject
    {
        [SerializeField] private List<LostNoteCharacterData> characterDataList = new();

        private Dictionary<int, LostNoteCharacterData> _cache;

        public bool TryGetByChapter(int chapter, out LostNoteCharacterData data)
        {
            EnsureCache();

            if (_cache.TryGetValue(chapter, out data))
                return true;

            Debug.LogWarning($"[LostNoteCharacterDatabase] Character data for chapter '{chapter}' not found.");
            return false;
        }

        private void EnsureCache()
        {
            if (_cache != null)
                return;

            _cache = new Dictionary<int, LostNoteCharacterData>();
            if (characterDataList == null)
                return;

            foreach (var data in characterDataList)
            {
                if (data == null)
                    continue;

                if (_cache.ContainsKey(data.Chapter))
                {
                    Debug.LogWarning($"[LostNoteCharacterDatabase] Duplicate chapter '{data.Chapter}' ignored. First data is used.");
                    continue;
                }

                _cache.Add(data.Chapter, data);
            }
        }
    }
}
