using UnityEngine;

namespace ScenarioSystem.Model
{
    [CreateAssetMenu(fileName = "LostNoteCharacterData", menuName = "Scenario/Lost Note Character Data")]
    public class LostNoteCharacterData : ScriptableObject
    {
        [SerializeField] private int chapter = 1;
        [SerializeField] private Sprite characterSprite;
        [SerializeField] private string characterName;

        [TextArea(2, 6)]
        [SerializeField] private string characterDescription;

        public int Chapter => chapter;
        public Sprite CharacterSprite => characterSprite;
        public string CharacterName => characterName;
        public string CharacterDescription => characterDescription;
    }

    public readonly struct LostNoteCharacterState
    {
        public readonly Sprite CharacterSprite;
        public readonly string CharacterName;
        public readonly string CharacterDescription;

        public LostNoteCharacterState(Sprite characterSprite, string characterName, string characterDescription)
        {
            CharacterSprite = characterSprite;
            CharacterName = characterName;
            CharacterDescription = characterDescription;
        }
    }
}
