using UnityEngine;
using UnityEngine.SceneManagement;
using Cysharp.Threading.Tasks;

namespace System_Script.Flow
{
    [CreateAssetMenu(fileName = "SceneLoadStep", menuName = "Flow/Steps/Scene Load Step")]
    public class SceneLoadStep : FlowStep
    {
        [Tooltip("Name of the scene to load.")]
        public string sceneName;
        [Tooltip("Use SceneTransition if one exists in the scene.")]
        public bool useSceneTransition = true;
        [Tooltip("Use the simple black fade instead of labeled MultiEasing transitions.")]
        public bool useSimpleFade;

        public override async void Execute(GameFlowDirector director)
        {
            if (!string.IsNullOrEmpty(sceneName))
            {
                if (useSceneTransition && global::SceneTransition.Instance != null)
                {
                    Debug.Log($"[SceneLoadStep] Transitioning to scene: {sceneName}");
                    if (useSimpleFade)
                    {
                        global::SceneTransition.Instance.TransitionToSimple(sceneName);
                    }
                    else
                    {
                        global::SceneTransition.Instance.TransitionTo(sceneName);
                    }

                    return;
                }

                Debug.Log($"[SceneLoadStep] Loading scene directly: {sceneName}");
                
                // Using SceneManager directly. could use UniTask's LoadSceneAsync for await support.
                await SceneManager.LoadSceneAsync(sceneName).ToUniTask();
                
                // Note: The director in the OLD scene will be destroyed here.
                // The director in the NEW scene will take over (via its own Start method).
            }
            else
            {
                Debug.LogWarning("[SceneLoadStep] Scene name is empty.");
                director.NextStep(); 
            }
        }
    }
}
