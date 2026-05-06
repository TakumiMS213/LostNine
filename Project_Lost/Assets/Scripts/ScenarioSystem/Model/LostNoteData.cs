using UnityEngine;

namespace ScenarioSystem.Model
{
    [CreateAssetMenu(fileName = "LostNoteData", menuName = "Scenario/Lost Note Data")]
    public class LostNoteData : ScriptableObject
    {
        [Tooltip("Keyword ID used to resolve this note.")]
        [SerializeField] private string keywordId;

        [TextArea(1, 2)]
        [SerializeField] private string title;

        [TextArea(2, 6)]
        [SerializeField] private string description;

        public string KeywordId => keywordId;
        public string Title => title;
        public string Description => description;
    }

    public readonly struct LostNoteMemo
    {
        public readonly string KeywordId;
        public readonly string Title;
        public readonly string Description;

        public LostNoteMemo(string keywordId, string title, string description)
        {
            KeywordId = keywordId;
            Title = title;
            Description = description;
        }
    }
}
