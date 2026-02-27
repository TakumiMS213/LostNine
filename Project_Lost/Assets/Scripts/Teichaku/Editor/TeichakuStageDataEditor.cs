using UnityEngine;
using UnityEditor;
using Teichaku.Data;

namespace Teichaku.Editor
{
    /// <summary>
    /// TeichakuStageData のカスタムインスペクタ。
    /// グリッド状のボタンでタイルの ON/OFF を直感的に編集できるステージビルダー。
    /// </summary>
    [CustomEditor(typeof(TeichakuStageData))]
    public class TeichakuStageDataEditor : UnityEditor.Editor
    {
        private const float BUTTON_SIZE = 32f;
        private const float BUTTON_SPACING = 2f;

        public override void OnInspectorGUI()
        {
            TeichakuStageData data = (TeichakuStageData)target;

            EditorGUILayout.LabelField("ステージビルダー", EditorStyles.boldLabel);
            EditorGUILayout.Space(4);

            // グリッドサイズの編集
            EditorGUI.BeginChangeCheck();
            int newWidth = EditorGUILayout.IntSlider("横幅 (Width)", data.width, 1, 10);
            int newHeight = EditorGUILayout.IntSlider("高さ (Height)", data.height, 1, 10);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(data, "Resize Teichaku Grid");
                data.ResizeGrid(newWidth, newHeight);
                EditorUtility.SetDirty(data);
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("タイル配置（クリックでON/OFF切替）", EditorStyles.miniLabel);
            EditorGUILayout.Space(4);

            // 配列サイズの整合性チェック
            if (data.tileActive == null || data.tileActive.Length != data.width * data.height)
            {
                Undo.RecordObject(data, "Fix Teichaku Grid Array");
                data.ResizeGrid(data.width, data.height);
                EditorUtility.SetDirty(data);
            }

            // グリッドボタンの描画
            DrawGrid(data);

            EditorGUILayout.Space(8);

            // ステージ情報
            EditorGUILayout.LabelField($"アクティブタイル数: {data.ActiveTileCount}", EditorStyles.helpBox);

            // 一括操作
            EditorGUILayout.Space(4);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("全てON"))
            {
                Undo.RecordObject(data, "Enable All Tiles");
                for (int i = 0; i < data.tileActive.Length; i++)
                    data.tileActive[i] = true;
                EditorUtility.SetDirty(data);
            }
            if (GUILayout.Button("全てOFF"))
            {
                Undo.RecordObject(data, "Disable All Tiles");
                for (int i = 0; i < data.tileActive.Length; i++)
                    data.tileActive[i] = false;
                EditorUtility.SetDirty(data);
            }
            EditorGUILayout.EndHorizontal();
        }

        /// <summary>
        /// グリッドをボタン配列として描画する
        /// </summary>
        private void DrawGrid(TeichakuStageData data)
        {
            Color defaultBg = GUI.backgroundColor;

            for (int y = 0; y < data.height; y++)
            {
                EditorGUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();

                for (int x = 0; x < data.width; x++)
                {
                    int index = y * data.width + x;
                    bool isActive = data.tileActive[index];

                    // 色の設定（ON = 緑、 OFF = 灰色）
                    GUI.backgroundColor = isActive
                        ? new Color(0.3f, 0.9f, 0.5f)
                        : new Color(0.4f, 0.4f, 0.4f);

                    string label = isActive ? "■" : "□";

                    if (GUILayout.Button(label, GUILayout.Width(BUTTON_SIZE), GUILayout.Height(BUTTON_SIZE)))
                    {
                        Undo.RecordObject(data, "Toggle Teichaku Tile");
                        data.tileActive[index] = !isActive;
                        EditorUtility.SetDirty(data);
                    }
                }

                GUILayout.FlexibleSpace();
                EditorGUILayout.EndHorizontal();
            }

            GUI.backgroundColor = defaultBg;
        }
    }
}
