using UnityEngine;
using ScenarioSystem.Adapter;

namespace MessageWindowSystem.Testing
{
    public class MessageWindowIndexStarter : MonoBehaviour
    {
        [Header("Settings")]
        public bool playOnStart = true;
        
        // Legacy scenarioDatabase removed

        [Tooltip("ID of the scenario to start (if Play On Start is true)")]
        public string startScenarioId;

        // public bool enableKeywords = false; // Removed as keywords are always enabled

        private void Start()
        {
            if (playOnStart)
            {
                StartScenarioById(startScenarioId);
            }
        }

        public void StartScenarioById(string scenarioId)
        {
            var facade = MessageWindowFacade.Instance;
            if (facade != null)
            {
                facade.StartScenarioById(scenarioId);
            }
            else
            {
                Debug.LogWarning("[MessageWindowIndexStarter] MessageWindowFacade.Instance not found.");
            }
        }

        // Keep this for legacy support or manual testing via index if needed, but better to use ID.
        // Or we can create a simple test method.
        [ContextMenu("Test Start Scenario")]
        public void TestStart()
        {
            StartScenarioById(startScenarioId);
        }
    }
}
