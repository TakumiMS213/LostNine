using UnityEngine;

namespace ScenarioSystem.Model.Actions
{
    [CreateAssetMenu(fileName = "LostNoteCharacterAction", menuName = "Scenario/Actions/Lost Note Character")]
    public class LostNoteCharacterAction : ScenarioAction
    {
        public override string ActionType => "LostNoteCharacter";

        [SerializeField] private string characterName;

        [TextArea(2, 6)]
        [SerializeField] private string characterDescription;

        public string CharacterName => characterName;
        public string CharacterDescription => characterDescription;
    }
}
