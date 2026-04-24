using UnityEngine;
using ScenarioSystem.Model;

namespace MessageWindowSystem.Testing
{
    public class MessageWindowTester : MonoBehaviour
    {
        [Header("Test Settings")]
        public bool playOnStart = true;
        public ScenarioData testScenario;
        public bool enableKeywords = false;

        private void Start()
        {
            if (playOnStart)
            {
                if (testScenario != null)
                {
                    ScenarioSystem.Adapter.MessageWindowFacade.Instance.StartScenario(testScenario);
                }
                else
                {
                    Debug.LogWarning("[MessageWindowTester] Please assign a ScenarioData to Test Scenario.");
                }
            }
        }
    }
}
