using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using ScenarioSystem.Adapter;
using ScenarioSystem.Model;
using ScenarioSystem.Model.Actions;
using ScenarioSystem.Model.Validation;
using System_Script.Flow;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ScenarioSystem.Editor.Csv
{
    public static partial class ScenarioCsvService
    {
        [Serializable] internal sealed class Binding { public string key; public string asset; }
        [Serializable] internal sealed class Manifest
        {
            public int version = 1;
            public string fingerprint;
            public List<Binding> bindings = new();
        }

        internal static Manifest ReadManifest(string folder)
        {
            string path = folder + "/bindings.json";
            return File.Exists(path) ? JsonUtility.FromJson<Manifest>(File.ReadAllText(path)) ?? new Manifest() : new Manifest();
        }

        private sealed class Node
        {
            public ScenarioCsvRow row;
            public ScriptableObject draft;
            public ScriptableObject target;
            public bool changed;
            public readonly List<string> rowIds = new();
        }

        private sealed class Stage : IDisposable
        {
            public readonly List<Node> nodes = new();
            public readonly Dictionary<string, Node> parents = new(StringComparer.Ordinal);
            public readonly List<string> errors = new();
            public void Dispose()
            {
                foreach (var node in nodes) if (node.draft != null) Object.DestroyImmediate(node.draft);
            }
        }

        public static List<string> Validate(ScenarioCsvDocument document)
        {
            using var stage = BuildStage(document);
            return new List<string>(stage.errors);
        }

        private static Stage BuildStage(ScenarioCsvDocument document)
        {
            var stage = new Stage();
            if (document?.rows == null || document.assets == null)
            {
                stage.errors.Add("CSVドキュメントが空です。"); return stage;
            }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var asset in document.assets)
            {
                if (string.IsNullOrWhiteSpace(asset.id) || !ids.Add(asset.id))
                    stage.errors.Add("素材IDが空または重複しています: " + asset.id);
                try { ScenarioCsvSerialization.ResolveAsset(asset.id, document); }
                catch (Exception e) { stage.errors.Add("素材 " + asset.label + ": " + e.Message); }
            }
            ids.Clear();
            for (int i = 0; i < document.rows.Count; i++)
            {
                var row = document.rows[i];
                try
                {
                    if (row == null || string.IsNullOrWhiteSpace(row.id) || !ids.Add(row.id))
                        throw new FormatException("行IDが空または重複しています。");
                    RowType(row);
                    if (!string.IsNullOrEmpty(row.source))
                    {
                        var original = ScenarioCsvSerialization.ResolveAsset(row.source, document);
                        if (original == null || original.GetType() != RowType(row))
                            throw new FormatException("元アセットと行の型が一致しません。型変更時はsourceを空にしてください。");
                    }
                    if (row.kind == "scenario" || row.kind == "sequence" || row.kind == "flow")
                    {
                        if (!string.IsNullOrEmpty(row.owner)) throw new FormatException("親データにはownerを指定できません。");
                        if (!string.IsNullOrEmpty(row.source) && stage.parents.Values.Any(n => n.row.source == row.source))
                            throw new FormatException("同じ元アセットを複数の親行で定義しています。");
                        var node = new Node { row = row, draft = ScriptableObject.CreateInstance(RowType(row)) };
                        node.rowIds.Add(row.id);
                        stage.nodes.Add(node);
                        stage.parents.Add(row.id, node);
                    }
                }
                catch (Exception e) { stage.errors.Add("CSV行 " + (i + 3) + " [" + row?.name + "]: " + e.Message); }
            }
            if (stage.errors.Count != 0) return stage;
            Object Resolve(string key)
            {
                if (key.StartsWith("row:", StringComparison.Ordinal))
                    return stage.parents.TryGetValue(key.Substring(4), out var parent) ? parent.draft : null;
                return stage.parents.Values.FirstOrDefault(n => n.row.source == key)?.draft;
            }
            foreach (var parent in stage.parents.Values)
            {
                try
                {
                    var populated = Preview(parent.row, document, Resolve);
                    try { EditorUtility.CopySerialized(populated, parent.draft); }
                    finally { Object.DestroyImmediate(populated); }
                }
                catch (Exception e) { stage.errors.Add(parent.row.name + ": " + e.Message); }
            }
            foreach (var row in document.rows.Where(r => r.kind == "action" || r.kind == "step"))
            {
                if (!stage.parents.TryGetValue(row.owner, out var parent) ||
                    (row.kind == "action" ? parent.row.kind != "scenario" : parent.row.kind != "sequence"))
                    stage.errors.Add("行 " + row.id + ": 対応する親シナリオ/シークエンスがありません。");
            }
            if (stage.errors.Count != 0) return stage;
            foreach (var parent in stage.parents.Values)
            {
                Node previous = null;
                foreach (var row in document.rows.Where(r => r.owner == parent.row.id))
                {
                    try
                    {
                        var draft = Preview(row, document, Resolve);
                        if (draft == null && parent.draft is StorySequence missingOwner)
                        {
                            missingOwner.steps.Add(null); previous = null; continue;
                        }
                        bool append = draft is DialogueAction && previous?.draft is DialogueAction &&
                            previous.row.source == row.source && previous.row.argument == row.argument && !string.IsNullOrEmpty(row.source);
                        if (append)
                        {
                            ((DialogueAction)previous.draft).entries.AddRange(((DialogueAction)draft).entries);
                            previous.rowIds.Add(row.id);
                            Object.DestroyImmediate(draft);
                        }
                        else
                        {
                            previous = new Node { row = row, draft = draft };
                            previous.rowIds.Add(row.id);
                            stage.nodes.Add(previous);
                            if (parent.draft is ScenarioData scenario) scenario.actions.Add((ScenarioAction)draft);
                            else if (parent.draft is StorySequence sequence) sequence.steps.Add((FlowStep)draft);
                        }
                    }
                    catch (Exception e) { stage.errors.Add("行 " + row.id + " [" + row.name + "]: " + e.Message); }
                }
            }
            foreach (var parent in stage.parents.Values.Where(n => n.draft is StorySequence))
            {
                var steps = document.rows.Where(r => r.owner == parent.row.id).ToList();
                if (!steps.Any(r => r.type == "$missing" && !string.IsNullOrEmpty(r.data))) continue;
                try
                {
                    var json = Newtonsoft.Json.Linq.JObject.Parse(EditorJsonUtility.ToJson(parent.draft));
                    for (int i = 0; i < steps.Count; i++)
                    {
                        if (steps[i].type != "$missing" || string.IsNullOrEmpty(steps[i].data)) continue;
                        var reference = Newtonsoft.Json.Linq.JObject.Parse(steps[i].data);
                        bool missingGuid = reference.Count == 3 && reference["guid"] != null && reference["fileID"] != null && reference["type"] != null;
                        bool nullReference = reference.Count == 1 && ((int?)reference["instanceID"] == 0 || (int?)reference["fileID"] == 0);
                        if (!missingGuid && !nullReference) throw new FormatException("参照切れ行の保存形式が不正です。");
                        json["MonoBehaviour"]["steps"][i] = reference;
                    }
                    EditorJsonUtility.FromJsonOverwrite(json.ToString(), parent.draft);
                }
                catch (Exception e) { stage.errors.Add(parent.row.name + ": " + e.Message); }
            }
            if (stage.errors.Count != 0) return stage;
            try
            {
                var scenarioNodes = stage.parents.Values.Where(n => n.draft is ScenarioData).ToList();
                var byAsset = scenarioNodes.Where(n => !string.IsNullOrEmpty(n.row.source)).ToDictionary(
                    n => PersistentId(ScenarioCsvSerialization.ResolveAsset(n.row.source, document)), n => (ScenarioData)n.draft);
                var covered = new HashSet<ScenarioData>();
                void Check(IEnumerable<ScenarioData> roots)
                {
                    var selected = roots.Where(root => root != null).Distinct().ToArray();
                    foreach (var scenario in ScenarioGraph.Collect(selected)) covered.Add(scenario);
                    foreach (var issue in ScenarioDataValidator.Validate(selected))
                        if (issue.Severity == ScenarioDataIssueSeverity.Error)
                            stage.errors.Add((issue.Context != null ? issue.Context.name + ": " : "") + issue.Code + " " + issue.Message);
                }
                foreach (var database in FindAll<ScenarioDataDatabase>())
                {
                    var roots = database.allScenarios.Where(root => root != null && byAsset.ContainsKey(PersistentId(root)))
                        .Select(root => byAsset[PersistentId(root)]).ToList();
                    if (roots.Count == 0) continue;
                    roots.AddRange(scenarioNodes.Where(n => string.IsNullOrEmpty(n.row.source)).Select(n => (ScenarioData)n.draft));
                    Check(roots);
                }
                Check(scenarioNodes.Select(n => (ScenarioData)n.draft).Where(scenario => !covered.Contains(scenario)));
                foreach (var node in stage.nodes)
                    ValidateExtra(node.draft, node.row, document, stage.errors);
            }
            catch (Exception e) { stage.errors.Add("データ検証: " + e.Message); }
            return stage;
        }

        private static void ValidateExtra(ScriptableObject value, ScenarioCsvRow row, ScenarioCsvDocument document, List<string> errors)
        {
            if (value is EffectAction effect && (effect.effectType == ScenarioEffectType.PlaySE || effect.effectType == ScenarioEffectType.PlayBGM))
            {
                string path = "Audio/" + (effect.effectType == ScenarioEffectType.PlaySE ? "SE/" : "BGM/") + effect.stringParam;
                if (string.IsNullOrWhiteSpace(effect.stringParam) || Resources.Load<AudioClip>(path) == null)
                    errors.Add(row.name + ": 音声が見つかりません: Resources/" + path);
            }
            if (value is ScenarioFlowCatalog catalog)
            {
                var keys = new HashSet<string>();
                foreach (var entry in catalog.Overrides)
                {
                    if (entry == null || entry.Sequence == null || entry.TargetChapter < 1 || !Enum.IsDefined(typeof(GamePhase), entry.TargetPhase))
                        errors.Add(row.name + ": 章別進行の指定が不正です。");
                    else if (!keys.Add(entry.TargetChapter + "/" + entry.TargetPhase))
                        errors.Add(row.name + ": 章・フェーズの進行指定が重複しています。");
                }
            }
            if (value is System_Script.Flow.JumpSequenceStep jump && jump.nextSequence == null)
                errors.Add(row.name + ": 移動先シークエンスがありません。");
            // Scene and flow step validation uses serialized field names shared by the existing step types.
            using var serialized = new SerializedObject(value);
            var scene = serialized.FindProperty("sceneName");
            if (scene != null && string.IsNullOrWhiteSpace(scene.stringValue))
                errors.Add(row.name + ": 移動先シーンが空です。");
        }

        public static void Apply(string folder, ScenarioCsvDocument document)
        {
            CheckFolder(folder);
            if (Importing) throw new InvalidOperationException("CSVを反映中です。");
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("再生を停止してからCSVを反映してください。");
            using var stage = BuildStage(document);
            if (stage.errors.Count != 0) throw new InvalidOperationException(string.Join("\n", stage.errors));
            var manifest = ReadManifest(folder);
            foreach (var binding in manifest.bindings)
            {
                var previous = ResolvePersistent(binding.asset);
                if ((previous is ScenarioData || previous is StorySequence || previous is ScenarioFlowCatalog)
                    && !stage.parents.ContainsKey(binding.key))
                    throw new InvalidOperationException("反映済みの親データはCSVから削除できません。参照を確認し、空のまま保持してください: " + previous.name);
            }
            var canonicalSources = stage.nodes.ToDictionary(n => n.row.id, n => string.IsNullOrEmpty(n.row.source) ? "" : PersistentId(ScenarioCsvSerialization.ResolveAsset(n.row.source, document)));
            var oldBindings = manifest.bindings.ToDictionary(b => b.key, b => b.asset, StringComparer.Ordinal);
            var backups = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);
            var originals = new Dictionary<ScriptableObject, string>();
            var createdPaths = new List<string>();
            var newObjects = new List<ScriptableObject>();
            var docBackup = JsonUtility.ToJson(document);
            var oldManifestBytes = File.Exists(folder + "/bindings.json") ? File.ReadAllBytes(folder + "/bindings.json") : null;
            foreach (string file in new[] { folder + "/scenarios.csv", folder + "/assets.csv" })
                if (File.Exists(file)) backups[file] = File.ReadAllBytes(file);
            Importing = true;
            try
            {
                Directory.CreateDirectory(folder + "/Generated");
                AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
                var sourceGroups = stage.nodes.Where(n => !string.IsNullOrEmpty(n.row.source)).GroupBy(n => canonicalSources[n.row.id])
                    .ToDictionary(g => g.Key, g => g.ToList(), StringComparer.Ordinal);
                // CSV display labels do not change Unity asset identity or shared content.
                var comparable = stage.nodes.ToDictionary(node => node, node => ComparableContent(node.draft));
                foreach (var node in stage.nodes)
                {
                    if (oldBindings.TryGetValue(node.row.id, out string bound))
                        node.target = ResolvePersistent(bound) as ScriptableObject;
                    if (node.target != null && node.target.GetType() != node.draft.GetType()) node.target = null;
                    // Reuse original shared objects only when all occurrences still agree.
                    if (node.target == null && !string.IsNullOrEmpty(node.row.source))
                    {
                        var peers = sourceGroups[canonicalSources[node.row.id]];
                        string data = comparable[node];
                        if (peers.All(peer => comparable[peer] == data))
                            node.target = ScenarioCsvSerialization.ResolveAsset(node.row.source, document) as ScriptableObject;
                    }
                    // An earlier import may have bound several rows to one shared source.
                    // If their edits diverged, keep that source unchanged and create local copies.
                    if (node.target != null)
                    {
                        string targetId = PersistentId(node.target);
                        var sameTarget = stage.nodes.Where(peer =>
                            (oldBindings.TryGetValue(peer.row.id, out string b) ? b : canonicalSources[peer.row.id]) == targetId).ToList();
                        if (sameTarget.Any(peer => comparable[peer] != comparable[node]))
                            node.target = null;
                    }
                }
                foreach (var node in stage.nodes)
                {
                    if (node.target == null)
                    {
                        node.target = ScriptableObject.CreateInstance(node.draft.GetType());
                        newObjects.Add(node.target);
                        string path = folder + "/Generated/" + StableId(node.row.id) + ".asset";
                        // Never overwrite an asset not recorded in this import's manifest.
                        if (File.Exists(path)) path = AssetDatabase.GenerateUniqueAssetPath(path);
                        node.target.name = Path.GetFileNameWithoutExtension(path);
                        AssetDatabase.CreateAsset(node.target, path);
                        createdPaths.Add(path);
                    }
                    else
                    {
                        string path = AssetDatabase.GetAssetPath(node.target);
                        if (!backups.ContainsKey(path) && File.Exists(path)) backups.Add(path, File.ReadAllBytes(path));
                        if (!originals.ContainsKey(node.target)) originals.Add(node.target, EditorJsonUtility.ToJson(node.target));
                    }
                }
                var targets = stage.nodes.ToDictionary(n => (Object)n.draft, n => (Object)n.target);
                foreach (var node in stage.nodes) Remap(node.draft, targets);
                foreach (var node in stage.nodes)
                {
                    string targetPath = AssetDatabase.GetAssetPath(node.target);
                    node.draft.name = targetPath.StartsWith(folder + "/Generated/", StringComparison.Ordinal)
                        && AssetDatabase.IsMainAsset(node.target)
                        ? Path.GetFileNameWithoutExtension(targetPath) : node.target.name;
                    if (EditorJsonUtility.ToJson(node.draft) != EditorJsonUtility.ToJson(node.target))
                    {
                        EditorUtility.CopySerialized(node.draft, node.target);
                        node.target.hideFlags = HideFlags.None;
                        EditorUtility.SetDirty(node.target);
                        node.changed = true;
                    }
                }
                // Register new scenario roots so normal ID lookup sees author-created scenarios.
                var sourceIds = new HashSet<string>(stage.parents.Values.Where(n => n.draft is ScenarioData).Select(n => canonicalSources[n.row.id]));
                var databases = FindAll<ScenarioDataDatabase>().Where(db =>
                    db.allScenarios.Any(scenario => scenario != null && sourceIds.Contains(PersistentId(scenario)))
                    || (folder == DefaultFolder && AssetDatabase.GetAssetPath(db) == "Assets/ScriptableObjects/ProgressSystemDatabase/ScenarioDataDatabase.asset")).ToList();
                var changedDatabases = new List<ScenarioDataDatabase>();
                foreach (var database in databases)
                {
                    var newScenarios = stage.parents.Values.Where(n => n.target is ScenarioData && string.IsNullOrEmpty(n.row.source))
                        .Select(n => (ScenarioData)n.target).ToList();
                    if (newScenarios.Count == 0) continue;
                    string path = AssetDatabase.GetAssetPath(database);
                    if (!backups.ContainsKey(path)) backups.Add(path, File.ReadAllBytes(path));
                    originals.TryAdd(database, EditorJsonUtility.ToJson(database));
                    foreach (var scenario in newScenarios) if (!database.allScenarios.Contains(scenario)) database.allScenarios.Add(scenario);
                    EditorUtility.SetDirty(database);
                    changedDatabases.Add(database);
                }
                foreach (var node in stage.nodes.Where(n => n.changed)) AssetDatabase.SaveAssetIfDirty(node.target);
                foreach (var database in changedDatabases) AssetDatabase.SaveAssetIfDirty(database);
                foreach (var database in databases) database.InvalidateCache();
                manifest.bindings.Clear();
                foreach (var node in stage.nodes)
                {
                    string asset = ScenarioCsvSerialization.AddAsset(node.target, document);
                    if (newObjects.Contains(node.target) && !string.IsNullOrWhiteSpace(node.row.name))
                        document.assets.First(entry => entry.id == asset).label = node.row.name;
                    manifest.bindings.Add(new Binding { key = node.row.id, asset = PersistentId(node.target) });
                    // New parent roots get a stable asset address for typed dropdowns.
                    if (stage.parents.ContainsKey(node.row.id) && string.IsNullOrEmpty(node.row.source)) node.row.source = asset;
                }
                Save(folder, document);
                manifest.fingerprint = Fingerprint(folder);
                WriteChanged(folder + "/bindings.json", JsonUtility.ToJson(manifest, true));
                AssetDatabase.ImportAsset(folder + "/bindings.json");
                foreach (var scenario in stage.nodes.Select(n => n.target).OfType<ScenarioData>()) scenario.NotifyDataChanged();
                ScenarioAuthoringService.Invalidate();
                _managed = null;
            }
            catch
            {
                foreach (var entry in originals)
                    if (entry.Key != null) EditorJsonUtility.FromJsonOverwrite(entry.Value, entry.Key);
                foreach (var entry in backups) File.WriteAllBytes(entry.Key, entry.Value);
                foreach (string path in createdPaths)
                    if (path.StartsWith(folder + "/Generated/", StringComparison.Ordinal)) AssetDatabase.DeleteAsset(path);
                JsonUtility.FromJsonOverwrite(docBackup, document);
                if (oldManifestBytes != null) File.WriteAllBytes(folder + "/bindings.json", oldManifestBytes);
                AssetDatabase.Refresh();
                throw;
            }
            finally { Importing = false; }
        }

        private static string ComparableContent(ScriptableObject value)
        {
            var json = Newtonsoft.Json.Linq.JObject.Parse(EditorJsonUtility.ToJson(value));
            if (json["MonoBehaviour"] is Newtonsoft.Json.Linq.JObject fields) fields.Remove("m_Name");
            return json.ToString(Newtonsoft.Json.Formatting.None);
        }

        private static void Remap(Object value, Dictionary<Object, Object> targets)
        {
            using var serialized = new SerializedObject(value);
            var property = serialized.GetIterator();
            while (property.Next(true))
                if (property.propertyType == SerializedPropertyType.ObjectReference &&
                    property.objectReferenceValue != null && targets.TryGetValue(property.objectReferenceValue, out var mapped))
                    property.objectReferenceValue = mapped;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static Object ResolvePersistent(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            int colon = id.LastIndexOf(':');
            if (colon < 1 || !long.TryParse(id.Substring(colon + 1), out long fileId)) return null;
            string path = AssetDatabase.GUIDToAssetPath(id.Substring(0, colon));
            return AssetDatabase.LoadAllAssetsAtPath(path).FirstOrDefault(asset =>
                asset != null && AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string _, out long localId) && localId == fileId);
        }

        private static string PersistentId(Object value)
        {
            if (value == null || !AssetDatabase.TryGetGUIDAndLocalFileIdentifier(value, out string guid, out long id)) return "";
            return guid + ":" + id.ToString(Invariant);
        }
    }
}
