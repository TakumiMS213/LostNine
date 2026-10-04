using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using ScenarioSystem.Adapter;
using ScenarioSystem.Model;
using ScenarioSystem.Model.Actions;
using System_Script.Flow;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ScenarioSystem.Editor.Csv
{
    /// <summary>CSV is the authoring source; existing SOs remain the runtime format.</summary>
    public static partial class ScenarioCsvService
    {
        public const string DefaultFolder = "Assets/ScenarioCSV";
        public const string DefaultFlowPath = DefaultFolder + "/Generated/ScenarioFlowCatalog.asset";
        internal static bool Importing { get; private set; }
        private static readonly CultureInfo Invariant = CultureInfo.InvariantCulture;
        private static HashSet<string> _managed;
        private static readonly string[] ScenarioFields = { "scenarioId", "showMainWindow", "actions", "nextScenario", "loop" };
        private static readonly string[] DialogueFields = { "entries" };
        private static readonly string[] EffectFields = { "effectType", "floatParam", "stringParam", "spriteParam", "colorParam" };
        private static readonly string[] OverlayFields = { "speakerName", "text", "portrait", "portraitPosition", "displayDuration" };

        public static ScenarioCsvDocument Load(string folder)
        {
            CheckFolder(folder);
            return new ScenarioCsvDocument
            {
                rows = ScenarioCsvFormat.ReadRows(File.ReadAllText(folder + "/scenarios.csv", Encoding.UTF8)),
                assets = ScenarioCsvFormat.ReadAssets(File.ReadAllText(folder + "/assets.csv", Encoding.UTF8))
            };
        }

        public static void Save(string folder, ScenarioCsvDocument document)
        {
            CheckFolder(folder);
            if (document == null) throw new ArgumentNullException(nameof(document));
            string assetText = ScenarioCsvFormat.WriteAssets(document.assets);
            string rowText = ScenarioCsvFormat.WriteRows(document.rows);
            Directory.CreateDirectory(folder);
            string assetsPath = folder + "/assets.csv", rowsPath = folder + "/scenarios.csv";
            byte[] oldAssets = File.Exists(assetsPath) ? File.ReadAllBytes(assetsPath) : null;
            byte[] oldRows = File.Exists(rowsPath) ? File.ReadAllBytes(rowsPath) : null;
            try
            {
                WriteChanged(assetsPath, assetText);
                WriteChanged(rowsPath, rowText);
            }
            catch
            {
                AssetDatabase.ReleaseCachedFileHandles();
                if (oldAssets != null) File.WriteAllBytes(assetsPath, oldAssets);
                else if (File.Exists(assetsPath)) File.Delete(assetsPath);
                if (oldRows != null) File.WriteAllBytes(rowsPath, oldRows);
                else if (File.Exists(rowsPath)) File.Delete(rowsPath);
                throw;
            }
            AssetDatabase.ImportAsset(assetsPath);
            AssetDatabase.ImportAsset(rowsPath);
            _managed = null;
        }

        internal static void WriteChanged(string path, string text)
        {
            // Compare the bytes that will be saved: StreamReader strips the BOM from text reads.
            byte[] bytes = new UTF8Encoding(false).GetBytes(text);
            if (File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(bytes)) return;
            // The editor writes both CSVs before asking Unity to import either one.
            string temporary = path + ".writing";
            try
            {
                File.WriteAllBytes(temporary, bytes);
                if (File.Exists(path))
                {
                    AssetDatabase.ReleaseCachedFileHandles();
                    for (int retry = 0; ; retry++)
                    {
                        try { File.Replace(temporary, path, null); break; }
                        catch (IOException error) when (retry < 3 && IsTransientFileLock(error))
                        {
                            // Windows imports/indexers can briefly retain handles without delete sharing.
                            // Keep atomic replacement, retry only known lock failures, then let rollback run.
                            AssetDatabase.ReleaseCachedFileHandles();
                            System.Threading.Thread.Sleep(50 * (retry + 1));
                        }
                    }
                }
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }

        private static bool IsTransientFileLock(IOException error)
        {
            int code = error.HResult & 0xFFFF;
            return code == 32 || code == 33 || code == 1175;
        }

        internal static void CheckFolder(string folder)
        {
            if (string.IsNullOrWhiteSpace(folder)) throw new ArgumentException("CSVフォルダを指定してください。");
            string assets = Path.GetFullPath("Assets") + Path.DirectorySeparatorChar;
            string full = Path.GetFullPath(folder);
            if (!full.StartsWith(assets, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("CSVはプロジェクトのAssets内に保存してください。");
        }

        public static string Fingerprint(string folder)
        {
            using var sha = SHA256.Create();
            string contents = File.ReadAllText(folder + "/scenarios.csv") + "\0" + File.ReadAllText(folder + "/assets.csv");
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(contents))).Replace("-", "");
        }

        public static ScenarioCsvRow NewRow(string kind, string type, string owner)
        {
            var row = new ScenarioCsvRow { id = Guid.NewGuid().ToString("N"), kind = kind, type = type, owner = owner ?? "" };
            var temp = ScriptableObject.CreateInstance(RowType(row));
            try
            {
                temp.name = type.Split('.').Last();
                if (temp is DialogueAction dialogue) dialogue.entries.Add(new DialogueEntry());
                CapturePreview(row, temp, new ScenarioCsvDocument());
                return row;
            }
            finally { Object.DestroyImmediate(temp); }
        }

        internal static Type RowType(ScenarioCsvRow row)
        {
            if (row.kind == "step" && row.type == "$missing" && string.IsNullOrEmpty(row.source)) return null;
            Type type = Type.GetType(row.type + ", Assembly-CSharp", false);
            bool valid = type != null && !type.IsAbstract && (row.kind switch
            {
                "scenario" => type == typeof(ScenarioData),
                "action" => typeof(ScenarioAction).IsAssignableFrom(type),
                "sequence" => type == typeof(StorySequence),
                "step" => typeof(FlowStep).IsAssignableFrom(type),
                "flow" => type == typeof(ScenarioFlowCatalog),
                _ => false
            });
            if (!valid) throw new FormatException("未対応の行種別または型: " + row.kind + " / " + row.type);
            return type;
        }

        public static ScriptableObject GetPreview(ScenarioCsvRow row, ScenarioCsvDocument document)
            => Preview(row, document, null);

        private static ScriptableObject Preview(ScenarioCsvRow row, ScenarioCsvDocument document, Func<string, Object> resolve)
        {
            var rowType = RowType(row);
            if (rowType == null) return null;
            var value = ScriptableObject.CreateInstance(rowType);
            try
            {
                ScenarioCsvSerialization.Apply(row.data, value, document, resolve);
                value.name = row.name;
                Object Asset(string key)
                {
                    if (string.IsNullOrEmpty(key)) return null;
                    var mapped = resolve?.Invoke(key);
                    if (mapped != null) return mapped;
                    if (key.StartsWith("row:", StringComparison.Ordinal))
                    {
                        var destination = document.rows.FirstOrDefault(r => r.id == key.Substring(4));
                        if (destination == null) throw new FormatException("遷移先の行がありません: " + key);
                        return string.IsNullOrEmpty(destination.source) ? null : ScenarioCsvSerialization.ResolveAsset(destination.source, document);
                    }
                    return ScenarioCsvSerialization.ResolveAsset(key, document);
                }
                T Ref<T>(string key) where T : Object
                {
                    var asset = Asset(key);
                    if (asset != null && asset is not T) throw new FormatException("素材の型が異なります: " + key + " → " + typeof(T).Name);
                    return asset as T;
                }
                switch (value)
                {
                    case ScenarioData scenario:
                        scenario.scenarioId = row.argument;
                        scenario.showMainWindow = Bool(row.window, true);
                        scenario.loop = Bool(row.loop, false);
                        scenario.nextScenario = Ref<ScenarioData>(row.next);
                        scenario.actions.Clear();
                        break;
                    case DialogueAction dialogue:
                        dialogue.entries = new List<DialogueEntry> { new DialogueEntry {
                            speakerName = row.speaker, text = row.text,
                            portrait = Ref<Sprite>(row.portrait), backgroundImage = Ref<Sprite>(row.background),
                            voiceClip = Ref<AudioClip>(row.voice), portraitPosition = EnumValue<PortraitPosition>(row.position),
                            typingSpeed = Number(row.speed), nameSlideDirection = EnumValue<NameSlideDirection>(row.slide)
                        }};
                        break;
                    case EffectAction effect:
                        effect.effectType = EnumValue<ScenarioEffectType>(row.effect);
                        effect.floatParam = Number(row.seconds);
                        effect.stringParam = row.argument;
                        if (!string.IsNullOrEmpty(row.voice) && (effect.effectType == ScenarioEffectType.PlaySE || effect.effectType == ScenarioEffectType.PlayBGM))
                        {
                            var clip = Ref<AudioClip>(row.voice);
                            string prefix = "/Resources/Audio/" + (effect.effectType == ScenarioEffectType.PlaySE ? "SE/" : "BGM/");
                            string path = AssetDatabase.GetAssetPath(clip);
                            int start = path.LastIndexOf(prefix, StringComparison.Ordinal);
                            if (start < 0) throw new FormatException("音声は" + prefix + "以下に配置してください: " + path);
                            string relative = path.Substring(start + prefix.Length);
                            effect.stringParam = relative.Substring(0, relative.Length - Path.GetExtension(relative).Length);
                        }
                        effect.spriteParam = Ref<Sprite>(row.sprite);
                        if (!string.IsNullOrEmpty(row.color))
                        {
                            if (!ColorUtility.TryParseHtmlString(row.color, out var color)) throw new FormatException("色の指定が不正です: " + row.color);
                            if (!string.Equals("#" + ColorUtility.ToHtmlStringRGBA(effect.colorParam), row.color, StringComparison.OrdinalIgnoreCase))
                                effect.colorParam = color;
                        }
                        break;
                    case OverlayAction overlay:
                        overlay.speakerName = row.speaker; overlay.text = row.text;
                        overlay.portrait = Ref<Sprite>(row.portrait);
                        overlay.portraitPosition = EnumValue<PortraitPosition>(row.position);
                        overlay.displayDuration = Number(row.seconds);
                        break;
                    case WaitAction wait: wait.duration = Number(row.seconds); break;
                    case StorySequence sequence: sequence.steps.Clear(); break;
                }
                return value;
            }
            catch { Object.DestroyImmediate(value); throw; }
        }

        public static void CapturePreview(ScenarioCsvRow row, ScriptableObject value, ScenarioCsvDocument document)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            row.type = value.GetType().FullName;
            row.name = value.name;
            string Add(Object asset) => ScenarioCsvSerialization.AddAsset(asset, document);
            switch (value)
            {
                case ScenarioData scenario:
                    row.argument = scenario.scenarioId;
                    row.window = scenario.showMainWindow ? "true" : "false";
                    row.loop = scenario.loop ? "true" : "false";
                    row.next = Add(scenario.nextScenario);
                    row.data = ScenarioCsvSerialization.Capture(value, document, ScenarioFields);
                    break;
                case DialogueAction dialogue:
                    if (dialogue.entries.Count != 1) throw new InvalidOperationException("CSVの会話プレビューは1行ずつ編集します。");
                    var entry = dialogue.entries[0];
                    row.speaker = entry.speakerName; row.text = entry.text;
                    row.portrait = Add(entry.portrait); row.background = Add(entry.backgroundImage); row.voice = Add(entry.voiceClip);
                    row.position = entry.portraitPosition.ToString(); row.speed = entry.typingSpeed.ToString("R", Invariant);
                    row.slide = entry.nameSlideDirection.ToString();
                    row.data = ScenarioCsvSerialization.Capture(value, document, DialogueFields);
                    break;
                case EffectAction effect:
                    row.effect = effect.effectType.ToString(); row.seconds = effect.floatParam.ToString("R", Invariant);
                    row.argument = effect.stringParam; row.sprite = Add(effect.spriteParam);
                    row.voice = "";
                    if (effect.effectType == ScenarioEffectType.PlaySE || effect.effectType == ScenarioEffectType.PlayBGM)
                        row.voice = Add(Resources.Load<AudioClip>("Audio/" + (effect.effectType == ScenarioEffectType.PlaySE ? "SE/" : "BGM/") + effect.stringParam));
                    // Preserve float color precision in the portable payload. HTML is for normal CSV authoring.
                    row.color = "#" + ColorUtility.ToHtmlStringRGBA(effect.colorParam);
                    row.data = ScenarioCsvSerialization.Capture(value, document, EffectFields.Where(f => f != "colorParam").ToArray());
                    break;
                case OverlayAction overlay:
                    row.speaker = overlay.speakerName; row.text = overlay.text; row.portrait = Add(overlay.portrait);
                    row.position = overlay.portraitPosition.ToString(); row.seconds = overlay.displayDuration.ToString("R", Invariant);
                    row.data = ScenarioCsvSerialization.Capture(value, document, OverlayFields);
                    break;
                case WaitAction wait:
                    row.seconds = wait.duration.ToString("R", Invariant);
                    row.data = ScenarioCsvSerialization.Capture(value, document, "duration");
                    break;
                case StorySequence:
                    row.data = ScenarioCsvSerialization.Capture(value, document, "steps");
                    break;
                default:
                    row.data = ScenarioCsvSerialization.Capture(value, document);
                    break;
            }
        }

        private static float Number(string value)
        {
            if (string.IsNullOrEmpty(value)) return 0;
            if (!float.TryParse(value, NumberStyles.Float, Invariant, out float parsed) || float.IsNaN(parsed) || float.IsInfinity(parsed))
                throw new FormatException("数値が不正です: " + value);
            return parsed;
        }

        private static bool Bool(string value, bool fallback)
        {
            if (string.IsNullOrEmpty(value)) return fallback;
            if (value == "1") return true;
            if (value == "0") return false;
            if (bool.TryParse(value, out bool parsed)) return parsed;
            throw new FormatException("真偽値が不正です: " + value);
        }

        private static T EnumValue<T>(string value) where T : struct, Enum
        {
            if (string.IsNullOrEmpty(value)) return default;
            if (Enum.TryParse(value, false, out T parsed) && Enum.IsDefined(typeof(T), parsed)) return parsed;
            throw new FormatException(typeof(T).Name + " の選択値が不正です: " + value);
        }

        public static ScenarioCsvDocument ExportProject(string folder)
        {
            CheckFolder(folder);
            if (File.Exists(folder + "/scenarios.csv") || File.Exists(folder + "/assets.csv"))
                throw new InvalidOperationException("既存のCSVがあります。再移行で上書きせず、CSVを開いてください。");
            if (folder == DefaultFolder) ScenarioCsvFlowBridge.MigrateMainScene(DefaultFlowPath);
            var document = new ScenarioCsvDocument();
            foreach (var scenario in FindAll<ScenarioData>())
            {
                var parent = ExportRoot("scenario", scenario, document);
                int index = 0;
                int block = 0;
                foreach (var action in scenario.actions)
                {
                    block++;
                    if (action == null) throw new InvalidOperationException(scenario.name + " に未設定アクションがあります。");
                    if (action is DialogueAction dialogue)
                    {
                        if (dialogue.entries.Count == 0) throw new InvalidOperationException(dialogue.name + " の本文が空です。");
                        foreach (var entry in dialogue.entries)
                        {
                            var preview = Object.Instantiate(dialogue);
                            try
                            {
                                preview.name = dialogue.name;
                                preview.entries = new List<DialogueEntry> { entry };
                                ExportChild("action", parent.id, action, preview, index++, document);
                                document.rows.Last().argument = "block_" + block;
                            }
                            finally { Object.DestroyImmediate(preview); }
                        }
                    }
                    else ExportChild("action", parent.id, action, action, index++, document);
                }
            }
            foreach (var sequence in FindAll<StorySequence>())
            {
                var parent = ExportRoot("sequence", sequence, document);
                int index = 0;
                foreach (var step in sequence.steps)
                {
                    if (step == null)
                    {
                        var raw = Newtonsoft.Json.Linq.JObject.Parse(EditorJsonUtility.ToJson(sequence))["MonoBehaviour"]["steps"][index];
                        document.rows.Add(new ScenarioCsvRow { id = StableId(parent.id + "/" + index++), owner = parent.id, kind = "step", type = "$missing", name = "参照切れ（スキップ）", data = raw.ToString(Newtonsoft.Json.Formatting.None) });
                        Debug.LogWarning("[Scenario CSV] " + sequence.name + " の参照切れをスキップ行として保持します。");
                    }
                    else ExportChild("step", parent.id, step, step, index++, document);
                }
            }
            foreach (var flow in FindAll<ScenarioFlowCatalog>()) ExportRoot("flow", flow, document);
            foreach (var clip in FindAll<AudioClip>())
            {
                string path = AssetDatabase.GetAssetPath(clip);
                if (path.Contains("/Resources/Audio/SE/") || path.Contains("/Resources/Audio/BGM/"))
                    ScenarioCsvSerialization.AddAsset(clip, document);
            }
            Save(folder, document);
            return document;
        }

        private static ScenarioCsvRow ExportRoot(string kind, ScriptableObject value, ScenarioCsvDocument document)
        {
            string source = ScenarioCsvSerialization.AddAsset(value, document);
            var row = new ScenarioCsvRow { id = StableId(source), kind = kind, source = source };
            CapturePreview(row, value, document);
            document.rows.Add(row);
            return row;
        }

        private static void ExportChild(string kind, string owner, Object source, ScriptableObject value, int index, ScenarioCsvDocument document)
        {
            var row = new ScenarioCsvRow {
                id = StableId(owner + "/" + index), owner = owner, kind = kind,
                source = ScenarioCsvSerialization.AddAsset(source, document)
            };
            CapturePreview(row, value, document);
            document.rows.Add(row);
        }

        private static IEnumerable<T> FindAll<T>() where T : Object
            => AssetDatabase.FindAssets("t:" + typeof(T).Name).Select(AssetDatabase.GUIDToAssetPath).Distinct()
                .Where(path => !path.StartsWith("Assets/__", StringComparison.Ordinal))
                .SelectMany(AssetDatabase.LoadAllAssetsAtPath).OfType<T>().Distinct()
                .OrderBy(AssetDatabase.GetAssetPath, StringComparer.Ordinal);

        private static string StableId(string value)
        {
            using var sha = SHA256.Create();
            return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", "").Substring(0, 32).ToLowerInvariant();
        }

        public static bool IsManaged(Object value)
        {
            if (value == null || !AssetDatabase.Contains(value)) return false;
            if (_managed == null)
            {
                _managed = new HashSet<string>(StringComparer.Ordinal);
                if (!File.Exists(DefaultFolder + "/scenarios.csv")) return false;
                try
                {
                    var document = Load(DefaultFolder);
                    foreach (var row in document.rows)
                        if (!string.IsNullOrEmpty(row.source)) _managed.Add(PersistentId(ScenarioCsvSerialization.ResolveAsset(row.source, document)));
                    foreach (var binding in ReadManifest(DefaultFolder).bindings) _managed.Add(binding.asset);
                }
                catch { return false; }
            }
            return AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long fileId)
                && _managed.Contains(guid + ":" + fileId.ToString(Invariant));
        }
    }
}
