using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

namespace ScenarioSystem.Editor
{
    /// <summary>
    /// エディター上でのシーン切り替えを簡単にするための専用ウィンドウ。
    /// Assets/Scenes フォルダ内のシーンを一覧表示し、ワンクリックでロードと再生が行える。
    /// </summary>
    public class SceneSwitcherWindow : EditorWindow
    {
        private Vector2 _scrollPos;

        [MenuItem("Tools/Scene Switcher (シーン切り替え)", false, 100)]
        public static void ShowWindow()
        {
            var window = GetWindow<SceneSwitcherWindow>("Scene Switcher");
            window.minSize = new Vector2(300, 400);
            window.Show();
        }

        private void OnGUI()
        {
            GUILayout.Space(10);
            GUILayout.Label("Lost Nine - Scene Switcher", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Assets/Scenes フォルダ内のシーン一覧です。", MessageType.Info);
            GUILayout.Space(10);

            // Assets/Scenes フォルダ内のみを検索
            string[] searchFolders = { "Assets/Scenes" };
            string[] sceneGuids = AssetDatabase.FindAssets("t:Scene", searchFolders);

            if (sceneGuids.Length == 0)
            {
                GUILayout.Label("シーンが見つかりません。Assets/Scenes フォルダを確認してください。");
                return;
            }

            _scrollPos = EditorGUILayout.BeginScrollView(_scrollPos);

            foreach (string guid in sceneGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                string sceneName = Path.GetFileNameWithoutExtension(path);

                EditorGUILayout.BeginHorizontal("box");
                
                // シーン名表示
                GUILayout.Label(sceneName, GUILayout.ExpandWidth(true));

                // 開くボタン
                if (GUILayout.Button("Load", GUILayout.Width(80), GUILayout.Height(24)))
                {
                    if (Application.isPlaying)
                    {
                        Debug.LogWarning("実行中は Load ボタンを使用できません。Playモードを終了してください。");
                    }
                    else if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                    }
                }

                // 開いて即再生ボタン
                var originalColor = GUI.backgroundColor;
                GUI.backgroundColor = new Color(0.6f, 1f, 0.6f); // 薄い緑色
                if (GUILayout.Button("Play", GUILayout.Width(80), GUILayout.Height(24)))
                {
                    if (Application.isPlaying)
                    {
                        Debug.LogWarning("すでに実行中です！");
                    }
                    else if (EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    {
                        EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
                        EditorApplication.isPlaying = true;
                    }
                }
                GUI.backgroundColor = originalColor;

                EditorGUILayout.EndHorizontal();
            }

            EditorGUILayout.EndScrollView();
        }
    }
}
