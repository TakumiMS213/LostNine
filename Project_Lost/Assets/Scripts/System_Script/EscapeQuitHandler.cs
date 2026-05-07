using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace SystemScript
{
    public sealed class EscapeQuitHandler : MonoBehaviour
    {
        private static EscapeQuitHandler _instance;
        private static int _suppressedFrame = -1;

        public static void SuppressQuitForCurrentFrame()
        {
            _suppressedFrame = Time.frameCount;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Initialize()
        {
            if (_instance != null)
                return;

            var gameObject = new GameObject(nameof(EscapeQuitHandler));
            _instance = gameObject.AddComponent<EscapeQuitHandler>();
            DontDestroyOnLoad(gameObject);
        }

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }

            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void LateUpdate()
        {
            if (!Input.GetKeyDown(KeyCode.Escape))
                return;

            if (_suppressedFrame == Time.frameCount)
                return;

            QuitGame();
        }

        private static void QuitGame()
        {
#if UNITY_EDITOR
            EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
