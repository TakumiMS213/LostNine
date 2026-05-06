using System;
using ScenarioSystem.Model;
using ScenarioSystem.Model.Actions;
using ScenarioSystem.Runtime;
using UnityEngine;

namespace ScenarioSystem.Presenter.Executors
{
    public class LostNoteCharacterActionExecutor : IActionExecutor
    {
        public string HandledActionType => "LostNoteCharacter";

        public void Execute(ScenarioAction action, ScenarioRuntimeState state, Action onComplete)
        {
            if (action is not LostNoteCharacterAction characterAction)
            {
                Debug.LogWarning("[LostNoteCharacterActionExecutor] Invalid action type.");
                onComplete?.Invoke();
                return;
            }

            var manager = LostNoteManager.Instance;
            if (manager == null)
            {
                Debug.LogWarning("[LostNoteCharacterActionExecutor] LostNoteManager is not found.");
                onComplete?.Invoke();
                return;
            }

            manager.UpdateCharacterText(characterAction.CharacterName, characterAction.CharacterDescription);
            onComplete?.Invoke();
        }
    }
}
