using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ScenarioSystem.Tests.Editor
{
    public class ScenarioCsvImportTests
    {
        private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        private string _folder;
        private object _document;
        private readonly List<Object> _assets = new();

        private static Type RuntimeType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
        private static Type CsvType(string name) => Type.GetType("ScenarioSystem.Editor.Csv." + name + ", Assembly-CSharp-Editor", true);
        private static object Call(string method, params object[] arguments)
            => CsvType("ScenarioCsvService").GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, arguments);
        private static object Serialization(string method, params object[] arguments)
            => CsvType("ScenarioCsvSerialization").GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, arguments);
        private static object Get(object target, string field) => target.GetType().GetField(field, Fields).GetValue(target);
        private static void Set(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
        private static IList Actions(Object scenario) => (IList)Get(scenario, "actions");
        private static IList Entries(Object action) => (IList)Get(action, "entries");
        private IList Rows => (IList)Get(_document, "rows");

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__ScenarioCsvImportTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", _folder.Substring("Assets/".Length));
            _document = Activator.CreateInstance(CsvType("ScenarioCsvDocument"));
        }

        [TearDown]
        public void TearDown()
        {
            AssetDatabase.DeleteAsset(_folder);
            _assets.Clear();
        }

        private Object CreateAsset(string type, string name)
        {
            var asset = ScriptableObject.CreateInstance(RuntimeType(type));
            asset.name = name;
            AssetDatabase.CreateAsset(asset, _folder + "/" + name + ".asset");
            _assets.Add(asset);
            return asset;
        }

        private Object Scenario(string name)
        {
            var scenario = CreateAsset("ScenarioSystem.Model.ScenarioData", name);
            Set(scenario, "scenarioId", "csv_test_" + name + "_" + Guid.NewGuid().ToString("N"));
            return scenario;
        }

        private Object Dialogue(string name, params string[] texts)
        {
            var action = CreateAsset("ScenarioSystem.Model.Actions.DialogueAction", name);
            foreach (string text in texts)
            {
                var entry = Activator.CreateInstance(RuntimeType("ScenarioSystem.Model.Actions.DialogueEntry"));
                Set(entry, "speakerName", "話者");
                Set(entry, "text", text);
                Set(entry, "typingSpeed", 0.03125f);
                Entries(action).Add(entry);
            }
            return action;
        }

        private Object Effect(string name, Color color)
        {
            var effect = CreateAsset("ScenarioSystem.Model.Actions.EffectAction", name);
            var field = effect.GetType().GetField("effectType");
            field.SetValue(effect, Enum.Parse(field.FieldType, "Flash"));
            Set(effect, "floatParam", 0.45f);
            Set(effect, "colorParam", color);
            return effect;
        }

        private void Persist()
        {
            foreach (var asset in _assets)
            {
                EditorUtility.SetDirty(asset);
                AssetDatabase.SaveAssetIfDirty(asset);
            }
        }

        private object ExportRow(string kind, Object source, string owner = "", string block = "")
        {
            var row = Call("NewRow", kind, source.GetType().FullName, owner);
            Call("CapturePreview", row, source, _document);
            Set(row, "source", Serialization("AddAsset", source, _document));
            if (source.GetType().Name == "DialogueAction") Set(row, "argument", block);
            Rows.Add(row);
            return row;
        }

        private object ExportScenario(Object scenario)
        {
            var parent = ExportRow("scenario", scenario);
            int index = 0;
            foreach (Object action in Actions(scenario))
            {
                index++;
                if (action.GetType().Name != "DialogueAction")
                {
                    ExportRow("action", action, (string)Get(parent, "id"));
                    continue;
                }
                foreach (object entry in Entries(action))
                {
                    var preview = Object.Instantiate(action);
                    try
                    {
                        preview.name = action.name;
                        Entries(preview).Clear();
                        Entries(preview).Add(entry);
                        var row = Call("NewRow", "action", action.GetType().FullName, (string)Get(parent, "id"));
                        Call("CapturePreview", row, preview, _document);
                        Set(row, "source", Serialization("AddAsset", action, _document));
                        Set(row, "argument", "block_" + index);
                        Rows.Add(row);
                    }
                    finally { Object.DestroyImmediate(preview); }
                }
            }
            return parent;
        }

        private void RoundTripCsv()
        {
            Call("Save", _folder, _document);
            _document = Call("Load", _folder);
        }

        private void Apply() => Call("Apply", _folder, _document);
        private static string Line(Object action, int index = 0) => (string)Get(Entries(action)[index], "text");
        private static string Address(Object asset)
        {
            Assert.That(AssetDatabase.TryGetGUIDAndLocalFileIdentifier(asset, out string guid, out long localId), Is.True);
            return guid + ":" + localId;
        }
        private Dictionary<string, byte[]> AssetBytes() => _assets.Select(AssetDatabase.GetAssetPath).Distinct()
            .ToDictionary(path => path, File.ReadAllBytes);
        private static void AssertBytes(Dictionary<string, byte[]> snapshot)
        {
            foreach (var pair in snapshot) Assert.That(File.ReadAllBytes(pair.Key), Is.EqualTo(pair.Value), pair.Key);
        }

        [Test]
        public void CsvRoundTripKeepsScenarioSequenceStepAndActionReferences()
        {
            var next = Scenario("Next");
            var scenario = Scenario("Root");
            var dialogue = Dialogue("Dialogue", "一行目, \"台詞\"\n二行目", "続き");
            var effect = Effect("Effect", Color.white);
            Actions(scenario).Add(dialogue);
            Actions(scenario).Add(effect);
            Set(scenario, "nextScenario", next);
            var sequence = CreateAsset("System_Script.Flow.StorySequence", "Sequence");
            var step = CreateAsset("System_Script.Flow.TalkStep", "Talk");
            Set(step, "scenario", scenario);
            ((IList)Get(sequence, "steps")).Add(step);
            Persist();
            var before = _assets.ToDictionary(asset => asset, Address);
            ExportScenario(scenario);
            ExportScenario(next);
            var sequenceRow = ExportRow("sequence", sequence);
            ExportRow("step", step, (string)Get(sequenceRow, "id"));
            RoundTripCsv();
            Apply();
            Assert.That(Actions(scenario)[0], Is.EqualTo(dialogue));
            Assert.That(Actions(scenario)[1], Is.EqualTo(effect));
            Assert.That(Entries(dialogue).Count, Is.EqualTo(2));
            Assert.That(Line(dialogue), Is.EqualTo("一行目, \"台詞\"\n二行目"));
            Assert.That(Get(scenario, "nextScenario"), Is.EqualTo(next));
            Assert.That(((IList)Get(sequence, "steps"))[0], Is.EqualTo(step));
            Assert.That(Get(step, "scenario"), Is.EqualTo(scenario));
            foreach (var pair in before) Assert.That(Address(pair.Key), Is.EqualTo(pair.Value));
        }

        [Test]
        public void InsertingEffectBetweenDialogueRowsPreservesOrderAndReapplyIdentity()
        {
            var scenario = Scenario("Root");
            var dialogue = Dialogue("Dialogue", "前", "後");
            Actions(scenario).Add(dialogue);
            Persist();
            var parent = ExportScenario(scenario);
            var effect = Call("NewRow", "action", "ScenarioSystem.Model.Actions.EffectAction", (string)Get(parent, "id"));
            Set(effect, "name", "InsertedFlash");
            Set(effect, "effect", "Flash");
            Rows.Insert(2, effect);
            RoundTripCsv();
            Apply();
            Assert.That(Actions(scenario).Count, Is.EqualTo(3));
            Assert.That(Line((Object)Actions(scenario)[0]), Is.EqualTo("前"));
            Assert.That(Actions(scenario)[1].GetType().Name, Is.EqualTo("EffectAction"));
            Assert.That(Line((Object)Actions(scenario)[2]), Is.EqualTo("後"));
            Assert.That(Entries(dialogue).Count, Is.EqualTo(2), "Splitting one source must not rewrite the shared original.");
            var addresses = Actions(scenario).Cast<Object>().Select(Address).ToArray();
            int count = AssetDatabase.FindAssets("", new[] { _folder + "/Generated" }).Length;
            Apply();
            Assert.That(Actions(scenario).Cast<Object>().Select(Address), Is.EqualTo(addresses));
            Assert.That(AssetDatabase.FindAssets("", new[] { _folder + "/Generated" }).Length, Is.EqualTo(count));
        }

        [Test]
        public void EditingOneUseOfSharedActionDoesNotChangeOtherScenarioOrOriginal()
        {
            var first = Scenario("First");
            var second = Scenario("Second");
            var shared = Dialogue("Shared", "共通台詞");
            Actions(first).Add(shared);
            Actions(second).Add(shared);
            Persist();
            var firstParent = ExportScenario(first);
            ExportScenario(second);
            Apply();
            Assert.That(Actions(first)[0], Is.EqualTo(shared));
            Assert.That(Actions(second)[0], Is.EqualTo(shared));
            var editedRow = Rows.Cast<object>().Single(row => (string)Get(row, "owner") == (string)Get(firstParent, "id"));
            Set(editedRow, "text", "一方だけ修正");
            Apply();
            var firstAction = (Object)Actions(first)[0];
            var secondAction = (Object)Actions(second)[0];
            Assert.That(firstAction, Is.Not.EqualTo(secondAction));
            Assert.That(Line(firstAction), Is.EqualTo("一方だけ修正"));
            Assert.That(Line(secondAction), Is.EqualTo("共通台詞"));
            Assert.That(Line(shared), Is.EqualTo("共通台詞"));
            var firstId = Address(firstAction);
            var secondId = Address(secondAction);
            Apply();
            Assert.That(Address((Object)Actions(first)[0]), Is.EqualTo(firstId));
            Assert.That(Address((Object)Actions(second)[0]), Is.EqualTo(secondId));
        }

        [Test]
        public void InvalidCsvLeavesAssetsAndPreviouslySavedCsvUnchanged()
        {
            var scenario = Scenario("Root");
            var effect = Effect("Effect", Color.white);
            Actions(scenario).Add(effect);
            Persist();
            ExportScenario(scenario);
            RoundTripCsv();
            var bytes = AssetBytes();
            var csv = File.ReadAllBytes(_folder + "/scenarios.csv");
            Set(Rows[1], "seconds", "not-a-number");
            var exception = Assert.Throws<TargetInvocationException>(Apply);
            Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
            AssertBytes(bytes);
            Assert.That(File.ReadAllBytes(_folder + "/scenarios.csv"), Is.EqualTo(csv));
            Assert.That(Actions(scenario)[0], Is.EqualTo(effect));
            Assert.That(Get(effect, "floatParam"), Is.EqualTo(0.45f));
        }

        [Test]
        public void ManifestWriteFailureRollsBackAssetsCsvAndDocument()
        {
            var scenario = Scenario("Root");
            var dialogue = Dialogue("Dialogue", "保存済み本文");
            Actions(scenario).Add(dialogue);
            Persist();
            var parent = ExportScenario(scenario);
            RoundTripCsv();
            var bytes = AssetBytes();
            var csv = File.ReadAllBytes(_folder + "/scenarios.csv");
            var assetsCsv = File.ReadAllBytes(_folder + "/assets.csv");
            Set(Rows[1], "text", "反映予定本文");
            var inserted = Call("NewRow", "action", "ScenarioSystem.Model.Actions.WaitAction", (string)Get(parent, "id"));
            Set(inserted, "seconds", "1.25");
            Rows.Add(inserted);
            string documentBefore = JsonUtility.ToJson(_document);
            Directory.CreateDirectory(_folder + "/bindings.json");
            var exception = Assert.Throws<TargetInvocationException>(Apply);
            Assert.That(exception.InnerException is IOException || exception.InnerException is UnauthorizedAccessException, Is.True, exception.InnerException?.ToString());
            AssertBytes(bytes);
            Assert.That(Line(dialogue), Is.EqualTo("保存済み本文"));
            Assert.That(Actions(scenario).Count, Is.EqualTo(1));
            Assert.That(File.ReadAllBytes(_folder + "/scenarios.csv"), Is.EqualTo(csv));
            Assert.That(File.ReadAllBytes(_folder + "/assets.csv"), Is.EqualTo(assetsCsv));
            Assert.That(JsonUtility.ToJson(_document), Is.EqualTo(documentBefore));
            Assert.That(Directory.GetFiles(_folder + "/Generated", "*.asset"), Is.Empty);
            Assert.That(File.Exists(_folder + "/bindings.json.writing"), Is.False);
        }

        [Test]
        public void CsvRoundTripPreservesFloatColorPrecisionIncludingHdr()
        {
            var scenario = Scenario("Root");
            var color = new Color(0.12345679f, 0.2345679f, 1.456789f, 0.34567893f);
            var effect = Effect("Effect", color);
            Actions(scenario).Add(effect);
            Persist();
            ExportScenario(scenario);
            RoundTripCsv();
            Apply();
            Assert.That((Color)Get((Object)Actions(scenario)[0], "colorParam"), Is.EqualTo(color));
        }


        [Test]
        public void MissingSequenceGuidSurvivesCsvApplyAndReapply()
        {
            var sequence = CreateAsset("System_Script.Flow.StorySequence", "MissingSequence");
            ((IList)Get(sequence, "steps")).Add(null);
            Persist();
            string path = AssetDatabase.GetAssetPath(sequence);
            string missingGuid = Guid.NewGuid().ToString("N");
            string referenceYaml = "{fileID: 11400000, guid: " + missingGuid + ", type: 2}";
            string yaml = File.ReadAllText(path);
            Assert.That(yaml, Does.Contain("- {fileID: 0}"));
            AssetDatabase.ReleaseCachedFileHandles();
            File.WriteAllText(path, yaml.Replace("- {fileID: 0}", "- " + referenceYaml));
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
            sequence = AssetDatabase.LoadMainAssetAtPath(path);
            Assert.That((Object)((IList)Get(sequence, "steps"))[0] == null, Is.True);
            var raw = System.Text.RegularExpressions.Regex.Match(EditorJsonUtility.ToJson(sequence),
                @"""steps""\s*:\s*\[\s*(\{[^}]+\})");
            Assert.That(raw.Success, Is.True);
            string referenceJson = raw.Groups[1].Value;
            Assert.That(referenceJson, Does.Contain(missingGuid));
            var parent = ExportRow("sequence", sequence);
            var row = Activator.CreateInstance(CsvType("ScenarioCsvRow"));
            Set(row, "id", Guid.NewGuid().ToString("N"));
            Set(row, "owner", Get(parent, "id"));
            Set(row, "kind", "step");
            Set(row, "type", "$missing");
            Set(row, "name", "参照切れ（スキップ）");
            Set(row, "data", referenceJson);
            Rows.Add(row);
            RoundTripCsv();
            string address = Address(sequence);
            Apply();
            Assert.That(File.ReadAllText(path), Does.Contain(referenceYaml));
            Assert.That(Address(sequence), Is.EqualTo(address));
            Assert.That((Object)((IList)Get(sequence, "steps"))[0] == null, Is.True);
            Apply();
            Assert.That(File.ReadAllText(path), Does.Contain(referenceYaml));
            Assert.That(((IList)Get(sequence, "steps")).Count, Is.EqualTo(1));
        }

        [Test]
        public void CustomAssetAliasesDoNotChangeManifestIdentityOnReapply()
        {
            var scenario = Scenario("Root");
            var dialogue = Dialogue("Dialogue", "別名付き素材参照");
            Actions(scenario).Add(dialogue);
            Persist();
            var parent = ExportScenario(scenario);
            var catalog = (IList)Get(_document, "assets");
            foreach (object asset in catalog)
            {
                string oldId = (string)Get(asset, "id");
                string alias = "custom_" + Get(asset, "label");
                Set(asset, "id", alias);
                foreach (object row in Rows)
                    if ((string)Get(row, "source") == oldId) Set(row, "source", alias);
            }
            var inserted = Call("NewRow", "action", "ScenarioSystem.Model.Actions.WaitAction", (string)Get(parent, "id"));
            Set(inserted, "seconds", "0.75");
            Rows.Add(inserted);
            Apply();
            var generated = (Object)Actions(scenario)[1];
            string generatedAddress = Address(generated);
            Assert.That(Actions(scenario)[0], Is.EqualTo(dialogue));
            foreach (object asset in catalog)
                if ((string)Get(asset, "id") == generatedAddress) Set(asset, "id", "custom_generated_wait");
            int count = Directory.GetFiles(_folder + "/Generated", "*.asset").Length;
            for (int i = 0; i < 3; i++)
            {
                Apply();
                Assert.That(Address((Object)Actions(scenario)[1]), Is.EqualTo(generatedAddress));
                Assert.That(Directory.GetFiles(_folder + "/Generated", "*.asset").Length, Is.EqualTo(count));
                Assert.That(File.ReadAllText(_folder + "/bindings.json"), Does.Contain(generatedAddress));
            }
        }

        [Test]
        public void DeletingPreviouslyAppliedParentIsRejectedWithoutChangingAssetsOrCsv()
        {
            var first = Scenario("First");
            var second = Scenario("Second");
            Actions(first).Add(Dialogue("FirstDialogue", "残す本文"));
            Actions(second).Add(Dialogue("SecondDialogue", "削除できない親"));
            Persist();
            ExportScenario(first);
            var removedParent = ExportScenario(second);
            Apply();
            string removedId = (string)Get(removedParent, "id");
            var bytes = AssetBytes();
            var scenariosCsv = File.ReadAllBytes(_folder + "/scenarios.csv");
            var manifest = File.ReadAllBytes(_folder + "/bindings.json");
            for (int i = Rows.Count - 1; i >= 0; i--)
                if ((string)Get(Rows[i], "id") == removedId || (string)Get(Rows[i], "owner") == removedId) Rows.RemoveAt(i);
            var exception = Assert.Throws<TargetInvocationException>(Apply);
            Assert.That(exception.InnerException, Is.TypeOf<InvalidOperationException>());
            Assert.That(exception.InnerException.Message, Does.Contain("親データ"));
            AssertBytes(bytes);
            Assert.That(File.ReadAllBytes(_folder + "/scenarios.csv"), Is.EqualTo(scenariosCsv));
            Assert.That(File.ReadAllBytes(_folder + "/bindings.json"), Is.EqualTo(manifest));
            Assert.That(Line((Object)Actions(second)[0]), Is.EqualTo("削除できない親"));
        }


        [Test]
        public void GeneratedAssetNameMatchesFilenameAndDisplayLabelChangesDoNotResaveIt()
        {
            var scenario = Scenario("Root");
            var dialogue = Dialogue("Dialogue", "既存の本文");
            Actions(scenario).Add(dialogue);
            Persist();
            var originalDialogueBytes = File.ReadAllBytes(AssetDatabase.GetAssetPath(dialogue));
            var parent = ExportScenario(scenario);
            var inserted = Call("NewRow", "action", "ScenarioSystem.Model.Actions.WaitAction", (string)Get(parent, "id"));
            Set(inserted, "name", "制作画面の待機ラベル");
            Set(inserted, "seconds", "1.25");
            Rows.Add(inserted);
            Apply();
            var generated = (Object)Actions(scenario)[1];
            string path = AssetDatabase.GetAssetPath(generated);
            string address = Address(generated);
            Assert.That(generated.name, Is.EqualTo(Path.GetFileNameWithoutExtension(path)));
            Assert.That(Get(inserted, "name"), Is.EqualTo("制作画面の待機ラベル"));
            var generatedBytes = File.ReadAllBytes(path);
            var allAssetBytes = AssetBytes();
            Set(inserted, "name", "表示ラベルだけ変更");
            Apply();
            Assert.That(Address((Object)Actions(scenario)[1]), Is.EqualTo(address));
            Assert.That(generated.name, Is.EqualTo(Path.GetFileNameWithoutExtension(path)));
            Assert.That(File.ReadAllBytes(path), Is.EqualTo(generatedBytes));
            Assert.That(EditorUtility.IsDirty(generated), Is.False);
            Assert.That(File.ReadAllBytes(AssetDatabase.GetAssetPath(dialogue)), Is.EqualTo(originalDialogueBytes));
            AssertBytes(allAssetBytes);
        }

        [Test]
        public void DifferentCsvDisplayLabelsKeepEqualSharedActionAndItsExistingName()
        {
            var first = Scenario("First");
            var second = Scenario("Second");
            var shared = Dialogue("Shared", "共通の本文");
            Actions(first).Add(shared);
            Actions(second).Add(shared);
            Persist();
            var firstParent = ExportScenario(first);
            var secondParent = ExportScenario(second);
            var rows = Rows.Cast<object>().Where(row => (string)Get(row, "kind") == "action").ToArray();
            Set(rows.Single(row => (string)Get(row, "owner") == (string)Get(firstParent, "id")), "name", "第1章用表示名");
            Set(rows.Single(row => (string)Get(row, "owner") == (string)Get(secondParent, "id")), "name", "第2章用表示名");
            var originalBytes = AssetBytes();
            Apply();
            Assert.That(Actions(first)[0], Is.EqualTo(shared));
            Assert.That(Actions(second)[0], Is.EqualTo(shared));
            Assert.That(shared.name, Is.EqualTo("Shared"));
            Assert.That(Directory.GetFiles(_folder + "/Generated", "*.asset"), Is.Empty);
            AssertBytes(originalBytes);
            Apply();
            Assert.That(Actions(first)[0], Is.EqualTo(shared));
            Assert.That(Actions(second)[0], Is.EqualTo(shared));
            AssertBytes(originalBytes);
        }

        [Test]
        public void ConsecutiveUsesOfSameDialogueRemainSeparateActionBlocks()
        {
            var scenario = Scenario("Root");
            var dialogue = Dialogue("Repeated", "同じ台詞");
            Actions(scenario).Add(dialogue);
            Actions(scenario).Add(dialogue);
            Persist();
            ExportScenario(scenario);
            RoundTripCsv();
            Apply();
            Assert.That(Actions(scenario).Count, Is.EqualTo(2));
            Assert.That(Actions(scenario)[0], Is.EqualTo(dialogue));
            Assert.That(Actions(scenario)[1], Is.EqualTo(dialogue));
            Assert.That(Entries(dialogue).Count, Is.EqualTo(1));
            Apply();
            Assert.That(Actions(scenario).Count, Is.EqualTo(2));
        }
    }
}
