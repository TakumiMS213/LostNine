using UnityEngine;

namespace ScenarioSystem.Editor.Csv
{
    internal sealed class ScenarioCsvDraft : ScriptableObject
    {
        [SerializeField] private ScenarioCsvDocument document;
        internal ScenarioCsvDocument Document { get => document; set => document = value; }
    }

}
