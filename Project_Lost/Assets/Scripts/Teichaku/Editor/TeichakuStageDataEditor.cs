using UnityEngine;
using UnityEditor;
using Teichaku.Data;

namespace Teichaku.Editor
{
    [CustomEditor(typeof(TeichakuStageData))]
    public class TeichakuStageDataEditor : UnityEditor.Editor
    {
        private StageSolutionStatus? _status;
        private Vector2Int[] _solution;
        private bool _showSolution;
        private bool _showReference = true;

        private void OnEnable() => Undo.undoRedoPerformed += InvalidateSolution;
        private void OnDisable() => Undo.undoRedoPerformed -= InvalidateSolution;
        private void InvalidateSolution() { _status = null; _solution = null; Repaint(); }

        public override void OnInspectorGUI()
        {
            var data = (TeichakuStageData)target;
            serializedObject.Update();
            EditorGUILayout.LabelField("なくしものと盤面", EditorStyles.boldLabel);
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lostThingImage"), new GUIContent("下絵・結果画像"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("lostThingName"), new GUIContent("なくしものの名前"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("preferredTileSize"), new GUIContent("マスの希望サイズ"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("tileSpacing"), new GUIContent("マスの間隔"));
            serializedObject.ApplyModifiedProperties();
            EditorGUILayout.HelpBox("章ごとにデータを作成してTeichakuManagerへ登録します。後半ほどマス数や曲がり道・分岐を増やして調整できます。大きな盤面は自動で画面内に収まります。", MessageType.Info);

            EditorGUI.BeginChangeCheck();
            int width = EditorGUILayout.IntSlider("横幅", data.width, 1, 20);
            int height = EditorGUILayout.IntSlider("高さ", data.height, 1, 20);
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(data, "Resize Teichaku Grid");
                data.ResizeGrid(width, height);
                Changed(data);
            }
            if (data.tileActive == null || data.tileActive.Length != data.width * data.height)
            {
                Undo.RecordObject(data, "Repair Teichaku Grid");
                data.ResizeGrid(data.width, data.height);
                Changed(data);
            }

            _showReference = EditorGUILayout.Toggle("画像を下絵に表示", _showReference);
            EditorGUILayout.LabelField("マスをクリックして形を編集", EditorStyles.miniBoldLabel);
            DrawGrid(data);
            EditorGUILayout.LabelField($"有効マス：{data.ActiveTileCount}個");
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("全てON")) Fill(data, true);
                if (GUILayout.Button("全てOFF")) Fill(data, false);
                if (GUILayout.Button("解けるか確認"))
                {
                    _status = TeichakuStageSolver.Solve(data, out _solution);
                    _showSolution = _status == StageSolutionStatus.Solved;
                }
            }
            if (_status == StageSolutionStatus.Solved)
            {
                EditorGUILayout.HelpBox("全マスを一度ずつ通る解答が見つかりました。数字は通過順です。", MessageType.Info);
                _showSolution = EditorGUILayout.Toggle("解答の通過順を表示", _showSolution);
            }
            else if (_status == StageSolutionStatus.Unsolvable)
                EditorGUILayout.HelpBox("この配置は一筆書きでクリアできません。マスの配置を調整してください。", MessageType.Error);
            else if (_status == StageSolutionStatus.SearchLimitReached)
                EditorGUILayout.HelpBox("探索の上限に達したため未判定です。解答がないという意味ではありません。盤面を簡略化するか、手動で解答を確認してください。", MessageType.Warning);
        }

        private void Changed(TeichakuStageData data)
        {
            EditorUtility.SetDirty(data);
            InvalidateSolution();
        }

        private void Fill(TeichakuStageData data, bool active)
        {
            Undo.RecordObject(data, "Fill Teichaku Grid");
            for (int i = 0; i < data.tileActive.Length; i++) data.tileActive[i] = active;
            Changed(data);
        }

        private void DrawGrid(TeichakuStageData data)
        {
            float cell = Mathf.Clamp((EditorGUIUtility.currentViewWidth - 48f) / data.width, 10f, 32f);
            Rect area = GUILayoutUtility.GetRect(data.width * cell, data.height * cell, GUILayout.ExpandWidth(false));
            EditorGUI.DrawRect(area, new Color(0.12f, 0.12f, 0.12f));
            if (_showReference && data.lostThingImage != null)
            {
                Sprite sprite = data.lostThingImage;
                Rect crop = sprite.rect;
                float scale = Mathf.Min(area.width / crop.width, area.height / crop.height);
                Rect image = new Rect(area.center - crop.size * scale * 0.5f, crop.size * scale);
                Rect uv = new Rect(crop.x / sprite.texture.width, crop.y / sprite.texture.height,
                    crop.width / sprite.texture.width, crop.height / sprite.texture.height);
                GUI.DrawTextureWithTexCoords(image, sprite.texture, uv, true);
            }
            for (int y = 0; y < data.height; y++)
            {
                for (int x = 0; x < data.width; x++)
                {
                    Rect rect = new Rect(area.x + x * cell, area.y + y * cell, cell - 1f, cell - 1f);
                    bool active = data.IsTileActive(x, y);
                    EditorGUI.DrawRect(rect, active ? new Color(0.15f, 0.9f, 0.45f, 0.55f) : new Color(0f, 0f, 0f, 0.2f));
                    if (GUI.Button(rect, GUIContent.none, GUIStyle.none))
                    {
                        Undo.RecordObject(data, "Toggle Teichaku Tile");
                        data.tileActive[y * data.width + x] = !active;
                        Changed(data);
                    }
                    if (_showSolution && _solution != null)
                    {
                        int order = System.Array.IndexOf(_solution, new Vector2Int(x, y));
                        if (order >= 0) GUI.Label(rect, (order + 1).ToString(), EditorStyles.centeredGreyMiniLabel);
                    }
                }
            }
        }
    }
}
