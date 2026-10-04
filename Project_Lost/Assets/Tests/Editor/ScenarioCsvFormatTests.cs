using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ScenarioSystem.Tests.Editor
{
    public sealed class ScenarioCsvFormatTests
    {
        private static Type CsvType(string name) => Type.GetType("ScenarioSystem.Editor.Csv." + name + ", Assembly-CSharp-Editor", true);
        private static object Call(string method, params object[] args) => CsvType("ScenarioCsvFormat").GetMethod(method).Invoke(null, args);
        private static IList List(string type) => (IList)Activator.CreateInstance(typeof(System.Collections.Generic.List<>).MakeGenericType(CsvType(type)));
        private static void Set(object target, string field, string value) => target.GetType().GetField(field).SetValue(target, value);
        private static string Get(object target, string field) => (string)target.GetType().GetField(field).GetValue(target);
        private static void Reject(Action action)
        {
            var exception = Assert.Throws<TargetInvocationException>(() => action());
            Assert.That(exception.InnerException, Is.InstanceOf<FormatException>());
        }

        [Test]
        public void RowsRoundTripEveryColumnWithMultilineQuotesJapaneseAndWhitespace()
        {
            var row = Activator.CreateInstance(CsvType("ScenarioCsvRow"));
            foreach (var field in row.GetType().GetFields()) field.SetValue(row, "  " + field.Name + ",日本語\r\n次行\n<link=\"001\">台詞</link>  ");
            var rows = List("ScenarioCsvRow");
            rows.Add(row);
            string csv = (string)Call("WriteRows", rows);
            Assert.That(csv.StartsWith("\uFEFFscenario-csv,1\r\n"), Is.True);
            var restored = (IList)Call("ReadRows", csv);
            Assert.That(restored.Count, Is.EqualTo(1));
            foreach (var field in row.GetType().GetFields()) Assert.That(field.GetValue(restored[0]), Is.EqualTo(field.GetValue(row)), field.Name);
            Assert.That(Call("WriteRows", restored), Is.EqualTo(csv));
        }

        [Test]
        public void EmptyDocumentAndTrailingEmptyCellsRoundTripWithoutPhantomRows()
        {
            var rows = List("ScenarioCsvRow");
            Assert.That(((IList)Call("ReadRows", Call("WriteRows", rows))).Count, Is.Zero);
            var row = Activator.CreateInstance(CsvType("ScenarioCsvRow"));
            Set(row, "text", "001");
            rows.Add(row);
            var restored = (IList)Call("ReadRows", Call("WriteRows", rows));
            Assert.That(restored.Count, Is.EqualTo(1));
            Assert.That(Get(restored[0], "text"), Is.EqualTo("001"));
            Assert.That(Get(restored[0], "data"), Is.Empty);
        }

        [Test]
        public void AssetCatalogPreservesStableIdsLabelsAndSubassetLocalIds()
        {
            var asset = Activator.CreateInstance(CsvType("ScenarioCsvAsset"));
            foreach (var field in asset.GetType().GetFields()) field.SetValue(asset, field.Name + ":001,\"日本語\"");
            var assets = List("ScenarioCsvAsset");
            assets.Add(asset);
            var restored = (IList)Call("ReadAssets", Call("WriteAssets", assets));
            foreach (var field in asset.GetType().GetFields()) Assert.That(field.GetValue(restored[0]), Is.EqualTo(field.GetValue(asset)), field.Name);
        }

        [TestCase("\"unterminated")]
        [TestCase("\"closed\"trailing")]
        [TestCase("unquoted\"quote")]
        [TestCase("bad\rnewline")]
        public void MalformedCsvCannotSilentlyDiscardCells(string suffix)
        {
            string header = (string)Call("WriteRows", List("ScenarioCsvRow"));
            Reject(() => Call("ReadRows", header + suffix));
        }

        [Test]
        public void UnknownVersionColumnsAndWrongColumnCountAreRejected()
        {
            string csv = (string)Call("WriteRows", List("ScenarioCsvRow"));
            Reject(() => Call("ReadRows", csv.Replace("scenario-csv,1", "scenario-csv,2")));
            Reject(() => Call("ReadRows", csv.Replace(",data\r\n", ",data,unknown\r\n")));
            Reject(() => Call("ReadRows", csv.Replace("id,owner,", "owner,id,")));
            Reject(() => Call("ReadRows", csv + "one,two\r\n"));
        }

        [Test]
        public void SpreadsheetPaddingAfterVersionIsAcceptedButMetadataContentIsNotDiscarded()
        {
            string rows = (string)Call("WriteRows", List("ScenarioCsvRow"));
            string paddedRows = rows.Replace("scenario-csv,1\r\n", "scenario-csv,1" + new string(',', 21) + "\r\n");
            Assert.That(((IList)Call("ReadRows", paddedRows)).Count, Is.Zero);
            string assets = (string)Call("WriteAssets", List("ScenarioCsvAsset"));
            Assert.That(((IList)Call("ReadAssets", assets.Replace("scenario-assets,1\r\n", "scenario-assets,1,,,,\r\n"))).Count, Is.Zero);
            Reject(() => Call("ReadRows", rows.Replace("scenario-csv,1\r\n", "scenario-csv,1,unknown\r\n")));
        }

        [Test]
        public void Utf8BomIsOptionalAndUnixRecordTerminatorsAreAccepted()
        {
            var rows = List("ScenarioCsvRow");
            var row = Activator.CreateInstance(CsvType("ScenarioCsvRow"));
            Set(row, "text", "行1\r\n行2");
            rows.Add(row);
            string csv = (string)Call("WriteRows", rows);
            Assert.That(Get(((IList)Call("ReadRows", csv.Substring(1)))[0], "text"), Is.EqualTo("行1\r\n行2"));
            Assert.That(((IList)Call("ReadRows", csv.Substring(1).Replace("\r\n", "\n"))).Count, Is.EqualTo(1));
        }
    }

    public sealed class ScenarioCsvSerializationTests
    {
        private string _folder;
        private readonly System.Collections.Generic.List<Object> _temporary = new();
        private static Type CsvType(string name) => Type.GetType("ScenarioSystem.Editor.Csv." + name + ", Assembly-CSharp-Editor", true);
        private static object Call(string method, params object[] args) => CsvType("ScenarioCsvSerialization").GetMethod(method).Invoke(null, args);
        private static object Document() => Activator.CreateInstance(CsvType("ScenarioCsvDocument"));
        private static IList Assets(object document) => (IList)document.GetType().GetField("assets").GetValue(document);
        private static void Set(object target, string field, string value) => target.GetType().GetField(field).SetValue(target, value);
        private static string Get(object target, string field) => (string)target.GetType().GetField(field).GetValue(target);
        private ScriptableObject Create(string name)
        {
            var value = ScriptableObject.CreateInstance(Type.GetType("ScenarioSystem.Model." + name + ", Assembly-CSharp", true));
            _temporary.Add(value);
            return value;
        }
        private string Capture(Object value, object document, params string[] excluded) => (string)Call("Capture", value, document, excluded);
        private static void Apply(string data, Object target, object document) => Call("Apply", data, target, document, null);

        [SetUp]
        public void SetUp()
        {
            _folder = "Assets/__ScenarioCsvSerialization_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", _folder.Substring("Assets/".Length));
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var value in _temporary) if (value != null && !AssetDatabase.Contains(value)) Object.DestroyImmediate(value);
            _temporary.Clear();
            AssetDatabase.DeleteAsset(_folder);
        }

        [Test]
        public void ColorVectorAndScalarsRoundTripAndExcludedFieldsKeepTheirDefaults()
        {
            var source = Create("Actions.TitleLogoAction");
            var target = Create("Actions.TitleLogoAction");
            using (var serialized = new SerializedObject(source))
            {
                serialized.FindProperty("maxLogoSize").vector2Value = new Vector2(124.75f, 908.25f);
                serialized.FindProperty("backgroundColor").colorValue = new Color(.1f, .2f, .3f, .4f);
                serialized.FindProperty("logoDisplayDuration").floatValue = 4.25f;
                serialized.FindProperty("useNativeLogoSize").boolValue = false;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var document = Document();
            string payload = Capture(source, document, "logoDisplayDuration");
            Apply(payload, target, document);
            Assert.That(Capture(target, document, "logoDisplayDuration"), Is.EqualTo(payload));
            using var targetFields = new SerializedObject(target);
            Assert.That(targetFields.FindProperty("logoDisplayDuration").floatValue, Is.EqualTo(1.5f));
            Assert.That(payload, Does.Not.Contain("instanceID"));
        }

        [Test]
        public void PrivateSerializedFieldsRoundTrip()
        {
            var source = Create("Actions.LostNoteCharacterAction");
            var target = Create("Actions.LostNoteCharacterAction");
            using (var serialized = new SerializedObject(source))
            {
                serialized.FindProperty("characterName").stringValue = "人物,一\r\n二";
                serialized.FindProperty("characterDescription").stringValue = "<link=\"key\">本文</link>";
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var document = Document();
            string payload = Capture(source, document);
            Apply(payload, target, document);
            Assert.That(Capture(target, document), Is.EqualTo(payload));
        }

        [Test]
        public void NestedChoiceArraysAndPersistentReferencesRoundTripThroughCatalog()
        {
            var destination = Create("ScenarioData");
            AssetDatabase.CreateAsset(destination, _folder + "/destination.asset");
            var source = Create("Actions.ChoiceAction");
            var target = Create("Actions.ChoiceAction");
            using (var serialized = new SerializedObject(source))
            {
                var choices = serialized.FindProperty("choices");
                choices.arraySize = 2;
                var first = choices.GetArrayElementAtIndex(0);
                first.FindPropertyRelative("choiceText").stringValue = "行く";
                first.FindPropertyRelative("choiceId").stringValue = "001";
                first.FindPropertyRelative("nextScenario").objectReferenceValue = destination;
                var second = choices.GetArrayElementAtIndex(1);
                second.FindPropertyRelative("choiceText").stringValue = "終える";
                second.FindPropertyRelative("nextScenario").objectReferenceValue = null;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var document = Document();
            string payload = Capture(source, document);
            Assert.That(Assets(document).Count, Is.EqualTo(1));
            Apply(payload, target, document);
            Assert.That(Capture(target, document), Is.EqualTo(payload));
            using var fields = new SerializedObject(target);
            Assert.That(fields.FindProperty("choices.Array.data[0].nextScenario").objectReferenceValue, Is.SameAs(destination));
            Assert.That(fields.FindProperty("choices.Array.data[1].nextScenario").objectReferenceValue, Is.Null);
        }

        [Test]
        public void ReferenceAndStringLeavesExcludeNativeChildrenAndRespectTemporaryResolver()
        {
            var original = Create("ScenarioData");
            AssetDatabase.CreateAsset(original, _folder + "/original.asset");
            var replacement = Create("ScenarioData");
            var source = Create("Actions.ChoiceAction");
            var target = Create("Actions.ChoiceAction");
            using (var serialized = new SerializedObject(source))
            {
                serialized.FindProperty("choices").arraySize = 1;
                serialized.FindProperty("choices.Array.data[0].choiceText").stringValue = "本文\r\n日本語";
                serialized.FindProperty("choices.Array.data[0].nextScenario").objectReferenceValue = original;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            var document = Document();
            string payload = Capture(source, document);
            Assert.That(payload, Does.Not.Contain(".m_FileID"));
            Assert.That(payload, Does.Not.Contain(".m_PathID"));
            Assert.That(payload, Does.Not.Contain(".m_InstanceID"));
            Assert.That(payload, Does.Not.Contain(".choiceText.Array"));
            Assert.That(payload, Does.Not.Contain(".choiceId.Array"));
            string id = Get(Assets(document)[0], "id");
            Call("Apply", payload, target, document, new Func<string, Object>(key => key == id ? replacement : null));
            using (var restored = new SerializedObject(target))
            {
                Assert.That(restored.FindProperty("choices.Array.data[0].nextScenario").objectReferenceValue, Is.SameAs(replacement));
                Assert.That(restored.FindProperty("choices.Array.data[0].choiceText").stringValue, Is.EqualTo("本文\r\n日本語"));
            }
            foreach (string nativePath in new[] {
                "choices.Array.data[0].nextScenario.m_FileID",
                "choices.Array.data[0].nextScenario.m_PathID",
                "choices.Array.data[0].choiceText.Array.size" })
            {
                string injected = payload.Substring(0, payload.Length - 2) + ",{\"path\":\"" + nativePath + "\",\"kind\":\"Integer\",\"value\":0}]}";
                var exception = Assert.Throws<TargetInvocationException>(() => Apply(injected, target, document));
                Assert.That(exception.InnerException, Is.InstanceOf<FormatException>());
                using var unchanged = new SerializedObject(target);
                Assert.That(unchanged.FindProperty("choices.Array.data[0].nextScenario").objectReferenceValue, Is.SameAs(replacement));
            }
        }

        [Test]
        public void CatalogReplacementKeepsItsIdAndAddAssetUsesCurrentGuidAndLocalId()
        {
            var original = Create("ScenarioData");
            var replacement = Create("ScenarioData");
            AssetDatabase.CreateAsset(original, _folder + "/first.asset");
            AssetDatabase.CreateAsset(replacement, _folder + "/second.asset");
            var document = Document();
            string id = (string)Call("AddAsset", original, document);
            Assert.That(Call("AddAsset", original, document), Is.EqualTo(id));
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(replacement, out string guid, out long localId);
            Set(Assets(document)[0], "guid", guid);
            Set(Assets(document)[0], "localId", localId.ToString(System.Globalization.CultureInfo.InvariantCulture));
            Assert.That(Call("ResolveAsset", id, document), Is.SameAs(replacement));
            Assert.That(Call("AddAsset", replacement, document), Is.EqualTo(id));
            Assert.That(Call("AddAsset", original, document), Is.Not.EqualTo(id));
        }

        [Test]
        public void InvalidPayloadDoesNotPartiallyApplyFieldsAndMissingReferencesFail()
        {
            var source = Create("Actions.WaitAction");
            var target = Create("Actions.WaitAction");
            var document = Document();
            string payload = Capture(source, document);
            Assert.Throws<TargetInvocationException>(() => Apply(payload.Replace("duration", "unknownField"), target, document));
            Assert.Throws<TargetInvocationException>(() => Apply(payload.Replace("Float", "String"), target, document));
            Assert.Throws<TargetInvocationException>(() => Call("ResolveAsset", "missing-id", document));
            Assert.Throws<TargetInvocationException>(() => Call("AddAsset", source, document));
            using var fields = new SerializedObject(target);
            Assert.That(fields.FindProperty("duration").floatValue, Is.EqualTo(1f));
        }
    }

    public sealed class ScenarioCsvFileWriteTests
    {
        private string _folder;
        private string _path;
        private static void Write(string path, string text)
            => Type.GetType("ScenarioSystem.Editor.Csv.ScenarioCsvService, Assembly-CSharp-Editor", true)
                .GetMethod("WriteChanged", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { path, text });

        [SetUp]
        public void SetUp()
        {
            _folder = Path.GetFullPath(Path.Combine("Temp", "ScenarioCsvWrite_" + Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(_folder);
            _path = Path.Combine(_folder, "scenarios.csv");
        }

        [TearDown]
        public void TearDown()
        {
            if (File.Exists(_path + ".writing")) File.Delete(_path + ".writing");
            if (File.Exists(_path)) File.Delete(_path);
            if (Directory.Exists(_folder)) Directory.Delete(_folder);
        }

        [Test]
        public void SavingIdenticalBomBytesKeepsTheExistingFileTimestamp()
        {
            const string content = "\uFEFFscenario-csv,1\r\n本文\r\n";
            Write(_path, content);
            File.SetLastWriteTimeUtc(_path, new DateTime(2020, 1, 2, 3, 4, 6, DateTimeKind.Utc));
            DateTime before = File.GetLastWriteTimeUtc(_path);
            Write(_path, content);
            Assert.That(File.GetLastWriteTimeUtc(_path), Is.EqualTo(before));
            Assert.That(File.ReadAllBytes(_path), Is.EqualTo(new System.Text.UTF8Encoding(false).GetBytes(content)));
            Assert.That(File.Exists(_path + ".writing"), Is.False);
        }

        [Test]
        public void AtomicSaveSucceedsWhenWindowsDeleteShareLockIsReleasedShortly()
        {
            if (Application.platform != RuntimePlatform.WindowsEditor) Assert.Ignore("Windows file sharing semantics");
            const string original = "\uFEFForiginal\r\n";
            const string updated = "\uFEFFupdated,日本語\r\n";
            Write(_path, original);
            var locked = new FileStream(_path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            using var started = new System.Threading.ManualResetEventSlim();
            var release = new System.Threading.Thread(() =>
            {
                started.Set();
                System.Threading.Thread.Sleep(75);
                locked.Dispose();
            });
            try
            {
                release.Start();
                Assert.That(started.Wait(5000), Is.True);
                Write(_path, updated);
                Assert.That(File.ReadAllBytes(_path), Is.EqualTo(new System.Text.UTF8Encoding(false).GetBytes(updated)));
                Assert.That(File.Exists(_path + ".writing"), Is.False);
            }
            finally
            {
                release.Join(5000);
                locked.Dispose();
            }
        }
    }

}
