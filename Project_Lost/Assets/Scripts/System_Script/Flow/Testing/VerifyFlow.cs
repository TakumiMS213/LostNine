using UnityEngine;
using System_Script.Flow;
using ScenarioSystem.Model;
using System.Collections;
using System.Collections.Generic;

namespace System_Script.Flow.Testing
{
    public class VerifyFlow : MonoBehaviour
    {
        [Header("Test Assets")]
        [SerializeField] private ScenarioData testScenario;
        
        private void Start()
        {
            StartCoroutine(RunTest());
        }

        private IEnumerator RunTest()
        {
            Debug.Log("=== Starting Flow System Verification ===");

            // 1. Create Director
            var directorObj = new GameObject("TestDirector");
            var director = directorObj.AddComponent<GameFlowDirector>();

            // 2. Create Sequence
            var sequence = ScriptableObject.CreateInstance<StorySequence>();
            sequence.name = "TestSequence";

            // 3. Create Steps
            // Step 1: Progress Update
            var progressStep = ScriptableObject.CreateInstance<ProgressStep>();
            progressStep.chapter = 99;
            progressStep.phase = GamePhase.Epilogue;
            progressStep.name = "Step1_Progress";
            sequence.steps.Add(progressStep);

            // Step 2: Talk (if scenario provided)
            if (testScenario != null)
            {
                var talkStep = ScriptableObject.CreateInstance<TalkStep>();
                talkStep.scenario = testScenario;
                talkStep.name = "Step2_Talk";
                sequence.steps.Add(talkStep);
            }

            // 4. Run
            director.PlaySequence(sequence);

            yield return new WaitForSeconds(1.0f);

            // 5. Verify Progress
            if (ProgressManager.Instance != null)
            {
                if (ProgressManager.Instance.CurrentChapter == 99 && ProgressManager.Instance.CurrentPhase == GamePhase.Epilogue)
                {
                    Debug.Log("PASS: Progress updated correctly.");
                }
                else
                {
                    Debug.LogError($"FAIL: Progress update failed. Current: {ProgressManager.Instance.CurrentChapter}-{ProgressManager.Instance.CurrentPhase}");
                }
            }
            else
            {
                 Debug.LogWarning("SKIP: ProgressManager not found in scene.");
            }

            Debug.Log("=== Verification Complete ===");
        }
    }
}
