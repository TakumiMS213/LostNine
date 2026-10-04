using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using ScenarioSystem.Model;
using ScenarioSystem.Model.Actions;
using System_Script.Flow;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ScenarioSystem.Editor.Csv
{
    /// <summary>CSVを編集元にした統合制作画面。変更は反映操作まで再生用アセットへ書き込まない。</summary>
    public sealed class ScenarioCsvWindow : EditorWindow
    {
        [SerializeField] private ScenarioCsvDraft draft;
        private ScenarioCsvDocument document { get => draft != null ? draft.Document : null; set { if (draft != null) draft.Document = value; } }
        [SerializeField] private string folder = ScenarioCsvService.DefaultFolder;
        [SerializeField] private string loadedFolder;
        [SerializeField] private string fingerprint;
        [SerializeField] private string savedDocument;
        [SerializeField] private string selectedOwner;
        [SerializeField] private string selectedRow;
        [SerializeField] private int tab;
        private string search = "";
        private string rowSearch = "";
        private string pasteText = "";
        private string status = "";
        private bool statusError;
        private bool showPaste;
        private Vector2 ownersScroll, rowsScroll, detailsScroll, errorsScroll;
        private readonly List<string> issues = new();
        private readonly Dictionary<string, Object> resolvedAssets = new();
        private readonly Dictionary<string, Type> resolvedTypes = new();
        private Type[] actionTypes = Array.Empty<Type>();
        private Type[] stepTypes = Array.Empty<Type>();
        private string[] sceneNames = Array.Empty<string>();
        private ScriptableObject preview;
        private SerializedObject previewFields;
        private string previewId;
        private bool previewAttempted;
        private double nextExternalCheck;
        private bool externalChange;
        private GUIStyle bodyStyle;

        [MenuItem("Scenario System/CSV Scenario Editor", false, 10)]
        public static void Open() => GetWindow<ScenarioCsvWindow>("シナリオ CSV");

        private void OnEnable()
        {
            if (draft == null) draft = CreateInstance<ScenarioCsvDraft>();
            // The serialized draft is embedded in the editor layout to survive domain reloads.
            // DontSaveInEditor would reject that persistence and trigger a native assertion.
            draft.hideFlags = HideFlags.HideInHierarchy | HideFlags.HideInInspector | HideFlags.DontSaveInBuild;
            CacheScenes();
            minSize = new Vector2(900, 580);
            saveChangesMessage = "シナリオCSVに未保存の変更があります。";
            actionTypes = TypeCache.GetTypesDerivedFrom<ScenarioAction>().Where(t => !t.IsAbstract && !t.ContainsGenericParameters).OrderBy(t => Label(t.Name)).ToArray();
            stepTypes = TypeCache.GetTypesDerivedFrom<FlowStep>().Where(t => !t.IsAbstract && !t.ContainsGenericParameters).OrderBy(t => Label(t.Name)).ToArray();
            Undo.undoRedoPerformed += OnUndoRedo;
            EditorApplication.projectChanged += OnProjectChanged;
            if (document == null && Directory.Exists(folder) && File.Exists(Path.Combine(folder, "scenarios.csv"))) LoadDocument(folder);
            else if (document != null) RefreshDirty();
        }

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            EditorApplication.projectChanged -= OnProjectChanged;
            DisposePreview();
        }

        private void OnInspectorUpdate()
        {
            if (document == null || EditorApplication.timeSinceStartup < nextExternalCheck) return;
            nextExternalCheck = EditorApplication.timeSinceStartup + 2;
            try { bool changed = Fingerprint(loadedFolder) != fingerprint; if (changed != externalChange) { externalChange = changed; Repaint(); } }
            catch (IOException) { /* 外部エディターが保存中の場合は次回確認する。 */ }
        }

        private void OnProjectChanged() { resolvedAssets.Clear(); CacheScenes(); Repaint(); }
        private void CacheScenes() => sceneNames = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => Path.GetFileNameWithoutExtension(s.path)).Distinct().ToArray();
        private void OnDestroy() { if (draft != null) DestroyImmediate(draft); }
        private void OnUndoRedo() { DisposePreview(); resolvedAssets.Clear(); RefreshDirty(); Repaint(); }
        public override void SaveChanges() { if (SaveDocument()) base.SaveChanges(); }
        public override void DiscardChanges() { hasUnsavedChanges = false; base.DiscardChanges(); }

        private void OnGUI()
        {
            DrawToolbar();
            if (!string.IsNullOrEmpty(status)) EditorGUILayout.HelpBox(status, statusError ? MessageType.Error : MessageType.Info);
            if (document == null)
            {
                EditorGUILayout.HelpBox("既存のシナリオ・演出・シークエンス・進行設定をCSVへ移行して編集します。既存CSVがある場合は読み込みます。", MessageType.Info);
                if (GUILayout.Button("既存データをCSVへ移行して開く", GUILayout.Height(32))) Run(() => AcceptDocument(ScenarioCsvService.ExportProject(folder), folder));
                return;
            }
            if (externalChange) EditorGUILayout.HelpBox("外部でCSVが変更されています。編集中の内容は保持されています。競合を避けるため、保存前に再読込してください。", MessageType.Warning);
            int nextTab = GUILayout.Toolbar(tab, new[] { "シナリオ本文・演出", "シークエンス", "章・フェーズの進行", "共通素材" });
            if (nextTab != tab) { tab = nextTab; selectedOwner = null; selectedRow = null; DisposePreview(); }
            if (tab == 3) DrawAssets();
            else
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    DrawOwners();
                    using (new EditorGUILayout.VerticalScope())
                    {
                        var owner = document.rows.FirstOrDefault(r => r.id == selectedOwner);
                        if (owner == null) EditorGUILayout.HelpBox("左の一覧から編集するデータを選択してください。", MessageType.Info);
                        else if (tab == 2) { detailsScroll = EditorGUILayout.BeginScrollView(detailsScroll); DrawMetadata(owner); DrawPreview(owner); EditorGUILayout.EndScrollView(); }
                        else DrawTimeline(owner);
                    }
                }
            }
            if (issues.Count > 0)
            {
                EditorGUILayout.LabelField("検証結果: " + issues.Count + " 件", EditorStyles.boldLabel);
                errorsScroll = EditorGUILayout.BeginScrollView(errorsScroll, GUILayout.MaxHeight(120));
                foreach (string issue in issues) EditorGUILayout.HelpBox(issue, MessageType.Error);
                EditorGUILayout.EndScrollView();
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                folder = EditorGUILayout.TextField(folder, EditorStyles.toolbarTextField, GUILayout.MinWidth(200));
                if (GUILayout.Button("読込", EditorStyles.toolbarButton, GUILayout.Width(45)) && CanReplaceDraft()) LoadDocument(folder);
                using (new EditorGUI.DisabledScope(document == null))
                {
                    if (GUILayout.Button("保存", EditorStyles.toolbarButton, GUILayout.Width(45))) SaveDocument();
                    if (GUILayout.Button("検証", EditorStyles.toolbarButton, GUILayout.Width(45))) ValidateDocument();
                    if (GUILayout.Button("保存して再生データに反映", EditorStyles.toolbarButton, GUILayout.Width(175))) ApplyDocument();
                    if (GUILayout.Button("元に戻す", EditorStyles.toolbarButton, GUILayout.Width(65))) Undo.PerformUndo();
                    if (GUILayout.Button("やり直す", EditorStyles.toolbarButton, GUILayout.Width(65))) Undo.PerformRedo();
                }
            }
            if (document != null) EditorGUILayout.LabelField((hasUnsavedChanges ? "● 未保存  " : "保存済み  ") + loadedFolder + "    CSV保存後、反映ボタンでゲームへ適用します。", EditorStyles.miniLabel);
        }

        private void DrawOwners()
        {
            using (new EditorGUILayout.VerticalScope(GUILayout.Width(235)))
            {
                search = EditorGUILayout.TextField("検索", search);
                string kind = tab == 0 ? "scenario" : tab == 1 ? "sequence" : "flow";
                ownersScroll = EditorGUILayout.BeginScrollView(ownersScroll);
                foreach (var row in document.rows)
                {
                    if (row.kind != kind || !Matches(row.name + " " + row.argument, search)) continue;
                    if (GUILayout.Toggle(row.id == selectedOwner, string.IsNullOrEmpty(row.name) ? row.id : row.name, "Button"))
                    {
                        if (selectedOwner != row.id) { selectedOwner = row.id; selectedRow = null; DisposePreview(); }
                    }
                }
                EditorGUILayout.EndScrollView();
                if (tab != 2 && GUILayout.Button(tab == 0 ? "＋ シナリオを作成" : "＋ シークエンスを作成"))
                {
                    string type = tab == 0 ? typeof(ScenarioData).FullName : typeof(StorySequence).FullName;
                    var row = ScenarioCsvService.NewRow(kind, type, "");
                    row.name = tab == 0 ? "新規シナリオ" : "新規シークエンス";
                    Change(() => document.rows.Add(row)); selectedOwner = row.id; selectedRow = null;
                }
            }
        }

        private void DrawMetadata(ScenarioCsvRow row)
        {
            EditText("表示名", row.name, value => row.name = value);
            if (row.kind != "scenario") return;
            EditText("シナリオID", row.argument, value => row.argument = value);
            EditBool("メインウィンドウを表示", row.window, value => row.window = value);
            EditBool("ループ再生", row.loop, value => row.loop = value);
            AssetField("終了後のシナリオ", row.next, typeof(ScenarioData), value => Change(() => row.next = value), true);
        }

        private void DrawTimeline(ScenarioCsvRow owner)
        {
            DrawMetadata(owner);
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                if (GUILayout.Button("＋ 行を挿入", EditorStyles.toolbarButton, GUILayout.Width(85))) ShowTypeMenu(owner, false);
                using (new EditorGUI.DisabledScope(Selected() == null))
                {
                    using (new EditorGUI.DisabledScope(Selected()?.type == "$missing"))
                        if (GUILayout.Button("複製", EditorStyles.toolbarButton, GUILayout.Width(45))) DuplicateRow();
                    if (GUILayout.Button("削除", EditorStyles.toolbarButton, GUILayout.Width(45))) RemoveRow();
                    if (GUILayout.Button("↑", EditorStyles.toolbarButton, GUILayout.Width(28))) MoveRow(-1);
                    if (GUILayout.Button("↓", EditorStyles.toolbarButton, GUILayout.Width(28))) MoveRow(1);
                }
                if (tab == 0) showPaste = GUILayout.Toggle(showPaste, "複数行貼り付け", EditorStyles.toolbarButton, GUILayout.Width(105));
                rowSearch = EditorGUILayout.TextField(rowSearch, EditorStyles.toolbarSearchField);
            }
            if (showPaste && tab == 0) DrawPaste(owner);
            using (new EditorGUILayout.HorizontalScope())
            {
                using (new EditorGUILayout.VerticalScope(GUILayout.Width(Mathf.Max(270, (position.width - 250) * .43f))))
                {
                    rowsScroll = EditorGUILayout.BeginScrollView(rowsScroll);
                    int number = 0;
                    foreach (var row in document.rows)
                    {
                        if (row.owner != owner.id) continue;
                        number++;
                        if (!Matches(row.speaker + " " + row.text + " " + row.name + " " + Label(ShortType(row.type)), rowSearch)) continue;
                        string summary = ShortType(row.type) == "DialogueAction" ? row.speaker + "  " + row.text : Label(ShortType(row.type)) + "  " + row.effect + " " + row.name;
                        summary = (summary ?? "").Replace('\r', ' ').Replace('\n', ' ');
                        if (summary.Length > 100) summary = summary.Substring(0, 100) + "…";
                        if (GUILayout.Toggle(selectedRow == row.id, number.ToString("D3") + "  " + summary, "Button") && selectedRow != row.id)
                        { selectedRow = row.id; DisposePreview(); }
                    }
                    EditorGUILayout.EndScrollView();
                }
                using (new EditorGUILayout.VerticalScope())
                {
                    detailsScroll = EditorGUILayout.BeginScrollView(detailsScroll);
                    var row = Selected();
                    if (row != null && row.owner == owner.id) DrawRow(row, owner);
                    else EditorGUILayout.HelpBox("行を選択すると、本文や演出を編集できます。挿入は選択行の直後に追加します。", MessageType.Info);
                    EditorGUILayout.EndScrollView();
                }
            }
        }

        private void DrawRow(ScenarioCsvRow row, ScenarioCsvRow owner)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(Label(ShortType(row.type)), EditorStyles.boldLabel);
                if (GUILayout.Button("種類を変更", GUILayout.Width(90))) ShowTypeMenu(owner, true);
            }
            switch (ShortType(row.type))
            {
                case "$missing":
                    EditorGUILayout.HelpBox("旧データの参照切れです。再生時はスキップされます。種類を変更して差し替えるか、不要なら行を削除してください。", MessageType.Warning);
                    break;
                case "DialogueAction":
                    EditText("話者", row.speaker, value => row.speaker = value);
                    EditorGUILayout.LabelField("本文（改行・TMPタグ対応）");
                    bodyStyle ??= new GUIStyle(EditorStyles.textArea) { wordWrap = true };
                    string text = EditorGUILayout.TextArea(row.text ?? "", bodyStyle, GUILayout.MinHeight(160));
                    if (text != (row.text ?? "")) Change(() => row.text = text);
                    AssetField("立ち絵", row.portrait, typeof(Sprite), value => Change(() => row.portrait = value));
                    EditEnum<PortraitPosition>("立ち絵の位置", row.position, value => row.position = value);
                    AssetField("背景", row.background, typeof(Sprite), value => Change(() => row.background = value));
                    AssetField("ボイス", row.voice, typeof(AudioClip), value => Change(() => row.voice = value));
                    EditNumber("文字送り（秒/文字）", row.speed, value => row.speed = value, true);
                    EditEnum<NameSlideDirection>("名前のスライド", row.slide, value => row.slide = value);
                    break;
                case "EffectAction": DrawEffect(row); break;
                case "OverlayAction": DrawOverlay(row); break;
                case "WaitAction": EditNumber("待機秒数", row.seconds, value => row.seconds = value); break;
                default: DrawPreview(row); break;
            }
        }


        private void DrawEffect(ScenarioCsvRow row)
        {
            EditEnum<ScenarioEffectType>("演出の種類", row.effect, value => { row.effect = value; row.voice = ""; row.argument = ""; });
            Enum.TryParse(row.effect, out ScenarioEffectType effect);
            if (effect == ScenarioEffectType.PlaySE || effect == ScenarioEffectType.PlayBGM)
            {
                string prefix = effect == ScenarioEffectType.PlaySE ? "Audio/SE/" : "Audio/BGM/";
                Rect rect = EditorGUILayout.GetControlRect();
                Rect button = EditorGUI.PrefixLabel(rect, new GUIContent("再生する音声"));
                string clipLabel = document.assets.FirstOrDefault(a => a.id == row.voice)?.label;
                if (GUI.Button(button, clipLabel ?? (string.IsNullOrEmpty(row.argument) ? "選択してください" : row.argument), EditorStyles.popup))
                {
                    var choices = new List<ScenarioCsvPicker.Entry> { new("", "なし") };
                    foreach (var asset in document.assets)
                        if (Resolve(asset.id) is AudioClip && (asset.resourcePath ?? "").StartsWith(prefix, StringComparison.Ordinal))
                            choices.Add(new ScenarioCsvPicker.Entry(asset.id, asset.label));
                    PopupWindow.Show(button, new ScenarioCsvPicker(choices, value => Change(() =>
                    {
                        row.voice = value;
                        string resource = document.assets.FirstOrDefault(a => a.id == value)?.resourcePath;
                        row.argument = !string.IsNullOrEmpty(resource) && resource.StartsWith(prefix, StringComparison.Ordinal) ? resource.Substring(prefix.Length) : "";
                    })));
                }
                EditorGUILayout.HelpBox("Resources/" + prefix + " にある登録済み音声から選択します。", MessageType.None);
            }
            if (effect == ScenarioEffectType.ShowImage)
            {
                AssetField("表示画像", row.sprite, typeof(Sprite), value => Change(() => row.sprite = value));
                if (string.IsNullOrEmpty(row.sprite) && !string.IsNullOrEmpty(row.argument)) EditorGUILayout.LabelField("既存のResources画像", row.argument);
            }
            if (effect == ScenarioEffectType.Shake || effect == ScenarioEffectType.FadeIn || effect == ScenarioEffectType.FadeOut)
                EditNumber("持続時間（秒）", row.seconds, value => row.seconds = value);
            if (effect == ScenarioEffectType.Flash || effect == ScenarioEffectType.FadeIn || effect == ScenarioEffectType.FadeOut || effect == ScenarioEffectType.ShowImage)
            {
                ColorUtility.TryParseHtmlString(row.color, out Color current);
                Color next = EditorGUILayout.ColorField("色", current);
                if (next != current) Change(() => row.color = "#" + ColorUtility.ToHtmlStringRGBA(next));
            }
        }

        private void DrawOverlay(ScenarioCsvRow row)
        {
            EditText("話者", row.speaker, value => row.speaker = value);
            EditorGUILayout.LabelField("本文");
            bodyStyle ??= new GUIStyle(EditorStyles.textArea) { wordWrap = true };
            string text = EditorGUILayout.TextArea(row.text ?? "", bodyStyle, GUILayout.MinHeight(140));
            if (text != (row.text ?? "")) Change(() => row.text = text);
            AssetField("立ち絵", row.portrait, typeof(Sprite), value => Change(() => row.portrait = value));
            EditEnum<PortraitPosition>("立ち絵の位置", row.position, value => row.position = value);
            EditNumber("表示秒数（0でクリック待ち）", row.seconds, value => row.seconds = value);
        }

        private void DrawPaste(ScenarioCsvRow owner)
        {
            EditorGUILayout.HelpBox("表計算ソフトの「話者・本文」の2列をコピーして貼り付けます。1列の場合は本文として扱います。引用符で囲まれたセル内改行にも対応します。", MessageType.Info);
            pasteText = EditorGUILayout.TextArea(pasteText, GUILayout.Height(65));
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("クリップボードを取得")) pasteText = EditorGUIUtility.systemCopyBuffer;
                using (new EditorGUI.DisabledScope(string.IsNullOrWhiteSpace(pasteText)))
                    if (GUILayout.Button("選択行の後に挿入")) Run(() =>
                    {
                        var records = ParseTsv(pasteText);
                        if (records.Any(r => r.Count > 2)) throw new FormatException("話者・本文の2列以内で貼り付けてください。");
                        Change(() =>
                        {
                            int index = InsertionIndex(owner);
                            foreach (var cells in records)
                            {
                                if (cells.All(string.IsNullOrEmpty)) continue;
                                var row = ScenarioCsvService.NewRow("action", typeof(DialogueAction).FullName, owner.id);
                                row.speaker = cells.Count > 1 ? cells[0] : "";
                                row.text = cells[cells.Count > 1 ? 1 : 0];
                                document.rows.Insert(index++, row); selectedRow = row.id;
                            }
                        });
                        pasteText = ""; showPaste = false;
                    });
            }
        }

        private void ShowTypeMenu(ScenarioCsvRow owner, bool replace)
        {
            var menu = new GenericMenu();
            foreach (Type type in owner.kind == "scenario" ? actionTypes : stepTypes)
            {
                var selectedType = type;
                menu.AddItem(new GUIContent(Label(type.Name)), false, () =>
                {
                    var previous = Selected();
                    var row = ScenarioCsvService.NewRow(owner.kind == "scenario" ? "action" : "step", selectedType.FullName, owner.id);
                    Change(() =>
                    {
                        if (replace && previous != null)
                        {
                            row.id = previous.id;
                            document.rows[document.rows.IndexOf(previous)] = row;
                        }
                        else document.rows.Insert(InsertionIndex(owner), row);
                        selectedRow = row.id;
                    });
                });
            }
            menu.ShowAsContext();
        }

        private int InsertionIndex(ScenarioCsvRow owner)
        {
            var selected = Selected();
            if (selected != null && selected.owner == owner.id) return document.rows.IndexOf(selected) + 1;
            int last = document.rows.FindLastIndex(r => r.owner == owner.id);
            return last >= 0 ? last + 1 : document.rows.IndexOf(owner) + 1;
        }

        private ScenarioCsvRow Selected() => document?.rows.FirstOrDefault(r => r.id == selectedRow);
        private void DuplicateRow()
        {
            var row = Selected(); if (row == null || row.type == "$missing") return;
            var copy = JsonUtility.FromJson<ScenarioCsvRow>(JsonUtility.ToJson(row));
            copy.id = ScenarioCsvService.NewRow(row.kind, row.type, row.owner).id;
            copy.source = "";
            Change(() => { document.rows.Insert(document.rows.IndexOf(row) + 1, copy); selectedRow = copy.id; });
        }
        private void RemoveRow()
        {
            var row = Selected(); if (row == null) return;
            Change(() => { document.rows.Remove(row); selectedRow = null; });
        }
        private void MoveRow(int offset)
        {
            var row = Selected(); if (row == null) return;
            var siblings = document.rows.Where(r => r.owner == row.owner).ToList();
            int target = siblings.IndexOf(row) + offset;
            if (target < 0 || target >= siblings.Count) return;
            Change(() => { int a = document.rows.IndexOf(row), b = document.rows.IndexOf(siblings[target]); document.rows[a] = siblings[target]; document.rows[b] = row; });
        }

        private void DrawPreview(ScenarioCsvRow row)
        {
            if (previewId != row.id || !previewAttempted)
            {
                DisposePreview(); previewId = row.id; previewAttempted = true;
                try { preview = ScenarioCsvService.GetPreview(row, document); if (preview != null) previewFields = new SerializedObject(preview); }
                catch (Exception e) { SetStatus(e.Message, true); }
            }
            if (preview == null || previewFields == null) { EditorGUILayout.HelpBox("この行の編集プレビューを生成できません。検証で参照や種類を確認してください。", MessageType.Warning); return; }
            EditorGUILayout.HelpBox("素材や遷移先は登録一覧から選択します。新しく作ったデータは保存・反映後に選択肢へ加わります。", MessageType.None);
            previewFields.Update();
            EditorGUI.BeginChangeCheck();
            var iterator = previewFields.GetIterator();
            bool enter = true;
            while (iterator.NextVisible(enter))
            {
                enter = false;
                if (iterator.name == "m_Script") continue;
                DrawProperty(iterator.Copy());
            }
            if (EditorGUI.EndChangeCheck())
            {
                Undo.RecordObject(draft, "CSV 項目を編集");
                previewFields.ApplyModifiedPropertiesWithoutUndo();
                ScenarioCsvService.CapturePreview(row, preview, document);
                Changed(false);
            }
        }

        private void DrawProperty(SerializedProperty property)
        {
            if (property.propertyType == SerializedPropertyType.String && (property.name == "sceneName" || property.name == "targetSceneName"))
            {
                var names = new[] { "なし" }.Concat(sceneNames).ToList();
                if (!string.IsNullOrEmpty(property.stringValue) && !names.Contains(property.stringValue)) names.Add(property.stringValue);
                int index = string.IsNullOrEmpty(property.stringValue) ? 0 : names.IndexOf(property.stringValue);
                int next = EditorGUILayout.Popup("遷移先シーン", index, names.ToArray());
                if (next != index) property.stringValue = next == 0 ? "" : names[next];
                return;
            }
            if (property.propertyType == SerializedPropertyType.Enum)
            {
                int next = EditorGUILayout.Popup(PropertyLabel(property.name, property.displayName), property.enumValueIndex, property.enumNames.Select(EnumLabel).ToArray());
                if (next != property.enumValueIndex) property.enumValueIndex = next;
                return;
            }

            if (property.propertyType == SerializedPropertyType.ObjectReference)
            {
                Type type = PropertyType(preview.GetType(), property.propertyPath) ?? typeof(Object);
                var current = property.objectReferenceValue;
                string currentId = current != null ? document.assets.FirstOrDefault(a => Resolve(a.id) == current)?.id : "";
                string propertyPath = property.propertyPath;
                AssetField(PropertyLabel(property.name, property.displayName), currentId, type, id =>
                {
                    if (preview == null || previewFields == null) return;
                    var targetRow = document.rows.FirstOrDefault(r => r.id == previewId);
                    if (targetRow == null) return;
                    Undo.RecordObject(draft, "CSV 参照を変更");
                    previewFields.Update();
                    var field = previewFields.FindProperty(propertyPath);
                    if (field == null) return;
                    field.objectReferenceValue = Resolve(id);
                    previewFields.ApplyModifiedPropertiesWithoutUndo();
                    ScenarioCsvService.CapturePreview(targetRow, preview, document); Changed(false);
                });
                return;
            }
            if (property.isArray && property.propertyType != SerializedPropertyType.String)
            {
                property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, PropertyLabel(property.name, property.displayName), true);
                if (!property.isExpanded) return;
                EditorGUI.indentLevel++;
                int count = Mathf.Max(0, EditorGUILayout.IntField("項目数", property.arraySize));
                if (count != property.arraySize) property.arraySize = count;
                for (int i = 0; i < property.arraySize; i++) DrawProperty(property.GetArrayElementAtIndex(i));
                EditorGUI.indentLevel--; return;
            }
            if (property.propertyType == SerializedPropertyType.Generic && property.hasVisibleChildren)
            {
                property.isExpanded = EditorGUILayout.Foldout(property.isExpanded, property.displayName, true);
                if (!property.isExpanded) return;
                var child = property.Copy(); var end = property.GetEndProperty();
                EditorGUI.indentLevel++;
                if (child.NextVisible(true)) do { DrawProperty(child.Copy()); } while (child.NextVisible(false) && !SerializedProperty.EqualContents(child, end));
                EditorGUI.indentLevel--; return;
            }
            if (property.propertyType == SerializedPropertyType.Float)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.PropertyField(property, new GUIContent(PropertyLabel(property.name, property.displayName)), true);
                    int choice = EditorGUILayout.Popup(0, new[] { "プリセット", "0", "0.1", "0.3", "0.5", "1", "2", "3" }, GUILayout.Width(85));
                    if (choice > 0) property.floatValue = new[] { 0f, .1f, .3f, .5f, 1f, 2f, 3f }[choice - 1];
                }
                return;
            }
            EditorGUILayout.PropertyField(property, new GUIContent(PropertyLabel(property.name, property.displayName)), true);
        }

        private static Type PropertyType(Type type, string propertyPath)
        {
            foreach (string part in propertyPath.Replace(".Array.data[", "[").Split('.'))
            {
                int bracket = part.IndexOf('[');
                string name = bracket >= 0 ? part.Substring(0, bracket) : part;
                FieldInfo field = null;
                for (Type at = type; at != null && field == null; at = at.BaseType) field = at.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (field == null) return null;
                type = field.FieldType;
                if (bracket >= 0) type = type.IsArray ? type.GetElementType() : type.IsGenericType ? type.GetGenericArguments()[0] : null;
                if (type == null) return null;
            }
            return type;
        }

        private void DrawAssets()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                search = EditorGUILayout.TextField("素材検索", search);
                var added = EditorGUILayout.ObjectField("一覧へ登録", null, typeof(Object), false);
                if (added != null) Change(() => ScenarioCsvSerialization.AddAsset(added, document));
            }
            EditorGUILayout.HelpBox("同じ登録IDを参照する全行へ差し替えが反映されます。素材の名前や画像・音声をこの一覧で管理します。", MessageType.Info);
            ownersScroll = EditorGUILayout.BeginScrollView(ownersScroll);
            foreach (var asset in document.assets)
            {
                if (!Matches(asset.label + " " + asset.type + " " + asset.resourcePath, search)) continue;
                using (new EditorGUILayout.HorizontalScope(EditorStyles.helpBox))
                {
                    string label = EditorGUILayout.TextField(asset.label ?? "", GUILayout.Width(230));
                    if (label != (asset.label ?? "")) Change(() => asset.label = label);
                    Object current = Resolve(asset.id);
                    Type expected = FindType(asset.type) ?? typeof(Object);
                    bool source = document.rows.Any(r => r.source == asset.id) || typeof(ScenarioData).IsAssignableFrom(expected) || typeof(ScenarioAction).IsAssignableFrom(expected) || typeof(StorySequence).IsAssignableFrom(expected) || typeof(FlowStep).IsAssignableFrom(expected) || expected.Name == "ScenarioFlowCatalog";
                    using (new EditorGUI.DisabledScope(source))
                    {
                        Object next = EditorGUILayout.ObjectField(current, expected, false);
                        if (next != current && next != null)
                        {
                            if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(next, out string guid, out long localId))
                                Change(() => { asset.guid = guid; asset.localId = localId.ToString(CultureInfo.InvariantCulture); asset.type = next.GetType().AssemblyQualifiedName; asset.resourcePath = ResourcePath(AssetDatabase.GetAssetPath(next)); });
                        }
                    }
                    GUILayout.Label(source ? "再生データ" : ShortType(asset.type), GUILayout.Width(110));
                    if (GUILayout.Button("表示", GUILayout.Width(45)) && current != null) EditorGUIUtility.PingObject(current);
                }
            }
            EditorGUILayout.EndScrollView();
        }

        private void AssetField(string label, string id, Type type, Action<string> selected, bool allowNewScenario = false)
        {
            string name = "なし";
            if (!string.IsNullOrEmpty(id))
            {
                name = document.assets.FirstOrDefault(a => a.id == id)?.label;
                if (name == null && id.StartsWith("row:", StringComparison.Ordinal)) name = document.rows.FirstOrDefault(r => r.id == id.Substring(4))?.name;
                if (name == null) name = "参照不明: " + id;
            }
            Rect rect = EditorGUILayout.GetControlRect();
            Rect button = EditorGUI.PrefixLabel(rect, new GUIContent(label));
            if (!GUI.Button(button, name, EditorStyles.popup)) return;
            var entries = new List<ScenarioCsvPicker.Entry> { new("", "なし") };
            foreach (var asset in document.assets)
            {
                Object value = Resolve(asset.id);
                if (value != null && type.IsInstanceOfType(value)) entries.Add(new ScenarioCsvPicker.Entry(asset.id, asset.label + "  [" + ShortType(asset.type) + "]"));
            }
            if (allowNewScenario)
                foreach (var row in document.rows.Where(r => r.kind == "scenario" && string.IsNullOrEmpty(r.source))) entries.Add(new ScenarioCsvPicker.Entry("row:" + row.id, row.name + "（新規）"));
            PopupWindow.Show(button, new ScenarioCsvPicker(entries, selected));
        }

        private Object Resolve(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (!resolvedAssets.TryGetValue(id, out Object value))
            {
                value = ScenarioCsvSerialization.ResolveAsset(id, document); resolvedAssets[id] = value;
            }
            return value;
        }
        private Type FindType(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (!resolvedTypes.TryGetValue(name, out Type type))
            { type = Type.GetType(name) ?? AppDomain.CurrentDomain.GetAssemblies().Select(a => a.GetType(name)).FirstOrDefault(t => t != null); resolvedTypes[name] = type; }
            return type;
        }

        private void EditText(string label, string value, Action<string> set)
        { string next = EditorGUILayout.TextField(label, value ?? ""); if (next != (value ?? "")) Change(() => set(next)); }
        private void EditBool(string label, string value, Action<string> set)
        { bool current = string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) || value == "1"; bool next = EditorGUILayout.Toggle(label, current); if (next != current) Change(() => set(next ? "true" : "false")); }
        private void EditEnum<T>(string label, string value, Action<string> set) where T : struct, Enum
        {
            Enum.TryParse(value, out T current);
            var values = (T[])Enum.GetValues(typeof(T));
            int index = Array.IndexOf(values, current);
            int choice = EditorGUILayout.Popup(label, Mathf.Max(0, index), values.Select(v => EnumLabel(v.ToString())).ToArray());
            T next = values[choice];
            if (!EqualityComparer<T>.Default.Equals(current, next)) Change(() => set(next.ToString()));
        }
        private void EditNumber(string label, string value, Action<string> set, bool speed = false)
        {
            float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float current);
            using (new EditorGUILayout.HorizontalScope())
            {
                float next = EditorGUILayout.FloatField(label, current);
                string[] names = speed ? new[] { "プリセット", "標準", "速い", "普通", "ゆっくり" } : new[] { "プリセット", "即時", "短い (0.3秒)", "普通 (1秒)", "長い (2秒)" };
                int choice = EditorGUILayout.Popup(0, names, GUILayout.Width(105));
                if (choice > 0) next = (speed ? new[] { 0f, .015f, .03f, .06f } : new[] { 0f, .3f, 1f, 2f })[choice - 1];
                if (next != current) Change(() => set(next.ToString("R", CultureInfo.InvariantCulture)));
            }
        }

        private void Change(Action action)
        { Undo.RecordObject(draft, "シナリオCSVを編集"); action(); Changed(true); }
        private void Changed(bool clearPreview)
        { hasUnsavedChanges = true; if (draft != null) EditorUtility.SetDirty(draft); issues.Clear(); resolvedAssets.Clear(); if (clearPreview) DisposePreview(); Repaint(); }
        private void RefreshDirty() => hasUnsavedChanges = document != null && JsonUtility.ToJson(document) != savedDocument;
        private void DisposePreview()
        { previewFields?.Dispose(); previewFields = null; if (preview != null) DestroyImmediate(preview); preview = null; previewId = null; previewAttempted = false; }
        private void SetStatus(string message, bool error = false) { status = message; statusError = error; Repaint(); }
        private void Run(Action action) { try { action(); } catch (Exception e) { RefreshDirty(); SetStatus(e.Message, true); Debug.LogException(e); } }

        private bool CanReplaceDraft()
        {
            if (!hasUnsavedChanges) return true;
            int choice = EditorUtility.DisplayDialogComplex("未保存の変更", "現在の編集内容を保存してから読み込みますか？", "保存して読込", "キャンセル", "変更を破棄して読込");
            return choice == 2 || choice == 0 && SaveDocument();
        }
        private void LoadDocument(string location) => Run(() => AcceptDocument(ScenarioCsvService.Load(location), location));
        private void AcceptDocument(ScenarioCsvDocument next, string location)
        {
            DisposePreview(); Undo.ClearUndo(draft); document = next; folder = loadedFolder = location;
            fingerprint = Fingerprint(location); savedDocument = JsonUtility.ToJson(document);
            hasUnsavedChanges = false; externalChange = false; resolvedAssets.Clear(); issues.Clear(); selectedOwner = null; selectedRow = null;
            SetStatus("CSVを読み込みました。");
        }
        private bool SaveDocument()
        {
            if (document == null) return false;
            try
            {
                if (Fingerprint(loadedFolder) != fingerprint)
                { SetStatus("外部CSVとの競合を検出したため保存を中止しました。現在の編集は保持しています。外部の変更を確認してから再読込してください。", true); return false; }
                ScenarioCsvService.Save(loadedFolder, document);
                fingerprint = Fingerprint(loadedFolder); savedDocument = JsonUtility.ToJson(document); hasUnsavedChanges = false; externalChange = false;
                SetStatus("CSVを保存しました。再生データへの適用は「保存して再生データに反映」を押してください。"); return true;
            }
            catch (Exception e) { SetStatus(e.Message, true); return false; }
        }
        private bool ValidateDocument()
        {
            if (document == null) return false;
            issues.Clear();
            try { issues.AddRange(ScenarioCsvService.Validate(document)); SetStatus(issues.Count == 0 ? "検証完了。問題は見つかりませんでした。" : "検証結果を確認してください。", issues.Count > 0); }
            catch (Exception e) { SetStatus(e.Message, true); return false; }
            return issues.Count == 0;
        }
        private void ApplyDocument()
        {
            if (!ValidateDocument() || !SaveDocument()) return;
            Run(() =>
            {
                ScenarioCsvService.Apply(loadedFolder, document);
                DisposePreview(); resolvedAssets.Clear(); fingerprint = Fingerprint(loadedFolder);
                savedDocument = JsonUtility.ToJson(document); hasUnsavedChanges = false;
                SetStatus("CSVを保存し、再生データへ反映しました。");
            });
        }

        private static string Fingerprint(string location)
        {
            if (string.IsNullOrEmpty(location) || !Directory.Exists(location)) return "";
            using var hash = SHA256.Create();
            var content = new StringBuilder();
            foreach (string file in Directory.GetFiles(location, "*.csv").OrderBy(f => f, StringComparer.Ordinal))
            { content.Append(Path.GetFileName(file)); content.Append('\0'); content.Append(File.ReadAllText(file)); content.Append('\0'); }
            return Convert.ToBase64String(hash.ComputeHash(Encoding.UTF8.GetBytes(content.ToString())));
        }
        private static string ResourcePath(string assetPath)
        { int start = assetPath.LastIndexOf("/Resources/", StringComparison.Ordinal); if (start < 0) return ""; string relative = assetPath.Substring(start + 11); return relative.Substring(0, relative.Length - Path.GetExtension(relative).Length); }
        private static bool Matches(string value, string query) => string.IsNullOrEmpty(query) || (value ?? "").IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
        private static string ShortType(string value) { if (string.IsNullOrEmpty(value)) return ""; int comma = value.IndexOf(','); if (comma >= 0) value = value.Substring(0, comma); return value.Substring(value.LastIndexOf('.') + 1); }

        internal static List<List<string>> ParseTsv(string text)
        {
            var rows = new List<List<string>>(); var row = new List<string>(); var cell = new StringBuilder(); bool quoted = false; bool closedQuote = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (closedQuote && c != '\t' && c != '\n' && c != '\r') throw new FormatException("閉じた引用符の後に区切り以外の文字があります。");
                if (c == '"')
                {
                    if (quoted && i + 1 < text.Length && text[i + 1] == '"') { cell.Append('"'); i++; }
                    else if (quoted) { quoted = false; closedQuote = true; }
                    else if (cell.Length == 0) quoted = true;
                    else cell.Append(c);
                }
                else if (!quoted && (c == '\t' || c == '\n' || c == '\r'))
                {
                    row.Add(cell.ToString()); cell.Clear(); closedQuote = false;
                    if (c != '\t') { rows.Add(row); row = new List<string>(); if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++; }
                }
                else cell.Append(c);
            }
            if (quoted) throw new FormatException("貼り付けデータの引用符が閉じられていません。");
            if (cell.Length > 0 || row.Count > 0 || closedQuote) { row.Add(cell.ToString()); rows.Add(row); }
            return rows;
        }


        private static string EnumLabel(string name) => name switch
        {
            "Center" => "中央", "Left" => "左", "Right" => "右", "Default" => "標準", "None" => "なし",
            "Shake" => "画面を揺らす", "Flash" => "フラッシュ", "FadeIn" => "フェードイン", "FadeOut" => "フェードアウト",
            "PlaySE" => "SE再生", "PlayBGM" => "BGM再生", "StopBGM" => "BGM停止", "ShowImage" => "画像表示", "HideImage" => "画像非表示",
            "AdvancePhase" => "次のフェーズ", "AdvanceChapter" => "次の章", "SetDirectly" => "直接指定",
            "Prologue" => "プロローグ", "Dialogue" => "対話", "Extraction" => "抽出", "Presentation" => "提示", "Epilogue" => "エピローグ",
            _ => ObjectNames.NicifyVariableName(name)
        };
        private static string PropertyLabel(string name, string fallback) => name switch
        {
            "startingSequence" => "開始シークエンス", "overrideSequences" => "章・フェーズ別シークエンス", "targetChapter" => "対象の章",
            "targetPhase" => "対象フェーズ", "sequence" => "シークエンス", "scenario" => "シナリオ", "nextSequence" => "移動先シークエンス",
            "nextScenario" => "移動先シナリオ", "choices" => "選択肢", "choiceText" => "選択肢の本文", "choiceId" => "選択肢ID",
            "actionType" => "操作", "sprite" => "画像", "logoSprite" => "ロゴ画像", "backgroundColor" => "背景色",
            "duration" => "秒数", "useChapterSelect" => "章選択へ移動", "useSceneTransition" => "シーン切替演出を使用", "useSimpleFade" => "黒フェードを使用",
            "backgroundFadeInDuration" => "背景の表示時間", "logoFadeInDuration" => "ロゴの表示時間", "logoDisplayDuration" => "ロゴの保持時間",
            "logoFadeOutDuration" => "ロゴの消去時間", "backgroundHoldDuration" => "背景の保持時間", "backgroundFadeOutDuration" => "背景の消去時間",
            "fadeOutBackgroundAfterLogo" => "終了後に背景を消去", "useNativeLogoSize" => "ロゴの元サイズを使用", "maxLogoSize" => "ロゴの最大サイズ",
            _ => fallback
        };

        private static string Label(string name) => name switch
        {
            "$missing" => "参照切れ（スキップ）",
            "DialogueAction" => "セリフ", "ChoiceAction" => "選択肢", "OverlayAction" => "オーバーレイ会話", "WaitAction" => "待機", "EffectAction" => "演出・音声",
            "ProgressUpdateAction" => "進行状態を更新", "ProgressScenarioAction" => "進行に対応するシナリオ", "SceneTransitionAction" => "シーン遷移",
            "ComuToggleAction" => "会話状態を切替", "ComuToggleInstantAction" => "会話状態を即時切替", "CenterPortraitAction" => "中央立ち絵",
            "PortraitInteractableAction" => "対話ボタンの許可", "PortraitGuidanceAction" => "立ち絵の誘導", "KeywordEnableAction" => "キーワードの操作許可",
            "DialogueStartButtonAction" => "対話開始ボタン", "LostNoteCharacterAction" => "ノート人物登録", "TitleLogoAction" => "タイトルロゴ",
            "TalkStep" => "シナリオ再生", "ScenarioTalkStep" => "シナリオ再生", "JumpSequenceStep" => "シークエンスへ移動", "ProgressStep" => "進行を更新",
            "SceneLoadStep" => "シーンを読込", "FadeInStep" => "フェードイン", "ObjectiveStep" => "目的を表示", "ChapterSelectStep" => "章選択",
            "TeichakuStep" => "定着", "ComuStartStep" => "対話開始", "ComuEndStep" => "対話終了", _ => ObjectNames.NicifyVariableName(name)
        };
    }

    internal sealed class ScenarioCsvPicker : PopupWindowContent
    {
        internal readonly struct Entry
        {
            internal readonly string Id, Label;
            internal Entry(string id, string label) { Id = id; Label = label; }
        }
        private readonly List<Entry> entries;
        private readonly Action<string> selected;
        private string search = "";
        private Vector2 scroll;
        internal ScenarioCsvPicker(List<Entry> entries, Action<string> selected) { this.entries = entries; this.selected = selected; }
        public override Vector2 GetWindowSize() => new(420, 360);
        public override void OnGUI(Rect rect)
        {
            GUI.SetNextControlName("AssetSearch");
            search = EditorGUILayout.TextField(search, EditorStyles.toolbarSearchField);
            if (Event.current.type == EventType.KeyDown && Event.current.keyCode == KeyCode.Escape) { editorWindow.Close(); return; }
            scroll = EditorGUILayout.BeginScrollView(scroll);
            foreach (var entry in entries)
            {
                if (!string.IsNullOrEmpty(search) && entry.Label.IndexOf(search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (!GUILayout.Button(entry.Label, EditorStyles.label)) continue;
                selected(entry.Id); editorWindow.Close(); GUIUtility.ExitGUI();
            }
            EditorGUILayout.EndScrollView();
        }
        public override void OnOpen() { editorWindow.Focus(); }
    }
}
