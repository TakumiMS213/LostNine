using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ScenarioSystem.Tests.Editor
{
    public sealed class ScenarioCsvWindowTests
    {
        private const BindingFlags PrivateInstance = BindingFlags.Instance | BindingFlags.NonPublic;
        private static Type CsvType(string name) => Type.GetType("ScenarioSystem.Editor.Csv." + name + ", Assembly-CSharp-Editor", true);
        private static List<List<string>> Parse(string text) => (List<List<string>>)CsvType("ScenarioCsvWindow")
            .GetMethod("ParseTsv", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, new object[] { text });
        private static object Invoke(EditorWindow window, string method, params object[] args) => window.GetType().GetMethod(method, PrivateInstance).Invoke(window, args);
        private static object Field(EditorWindow window, string name) => window.GetType().GetField(name, PrivateInstance).GetValue(window);
        private static void Set(object target, string field, string value) => target.GetType().GetField(field).SetValue(target, value);
        private static string FirstText(EditorWindow window)
        {
            var document = window.GetType().GetProperty("document", PrivateInstance).GetValue(window);
            var rows = (IList)document.GetType().GetField("rows").GetValue(document);
            return (string)rows[0].GetType().GetField("text").GetValue(rows[0]);
        }

        [Test]
        public void SpreadsheetPastePreservesQuotedMultilineTabsAndEscapedQuotes()
        {
            var rows = Parse("話者\t\"一行目\r\n二行目\t\"\"引用\"\"\"\r\n次の人\t本文\r\n");
            Assert.That(rows.Count, Is.EqualTo(2));
            Assert.That(rows[0], Is.EqualTo(new[] { "話者", "一行目\r\n二行目\t\"引用\"" }));
            Assert.That(rows[1], Is.EqualTo(new[] { "次の人", "本文" }));
        }

        [Test]
        public void PasteSupportsSingleColumnAndEmptyTextWithoutAddingATrailingRow()
        {
            var rows = Parse("本文のみ\n話者\t\"\"\n");
            Assert.That(rows.Count, Is.EqualTo(2));
            Assert.That(rows[0], Is.EqualTo(new[] { "本文のみ" }));
            Assert.That(rows[1], Is.EqualTo(new[] { "話者", "" }));
            Assert.That(Parse(""), Is.Empty);
            Assert.That(Parse("\"\"")[0], Is.EqualTo(new[] { "" }));
        }

        [TestCase("話者\t\"閉じていない本文")]
        [TestCase("話者\t\"閉じた本文\"余分な文字")]
        public void MalformedQuotedPasteIsRejectedInsteadOfChangingTheText(string text)
        {
            var exception = Assert.Throws<TargetInvocationException>(() => Parse(text));
            Assert.That(exception.InnerException, Is.InstanceOf<FormatException>());
        }

        [Test]
        public void UndoAfterSaveRestoresDraftButNeverRewindsTheDiskConflictBaseline()
        {
            string folder = "Assets/__ScenarioCsvWindowTest_" + Guid.NewGuid().ToString("N");
            AssetDatabase.CreateFolder("Assets", folder.Substring("Assets/".Length));
            var window = (EditorWindow)ScriptableObject.CreateInstance(CsvType("ScenarioCsvWindow"));
            Object draft = null;
            try
            {
                var document = Activator.CreateInstance(CsvType("ScenarioCsvDocument"));
                var row = Activator.CreateInstance(CsvType("ScenarioCsvRow"));
                Set(row, "id", "draft-test"); Set(row, "text", "変更前");
                ((IList)document.GetType().GetField("rows").GetValue(document)).Add(row);
                Invoke(window, "AcceptDocument", document, folder);
                draft = (Object)Field(window, "draft");
                Undo.IncrementCurrentGroup();
                Invoke(window, "Change", (Action)(() => Set(row, "text", "保存した本文\n次の行")));
                Undo.FlushUndoRecordObjects();
                Assert.That((bool)Invoke(window, "SaveDocument"), Is.True);
                string fingerprint = (string)Field(window, "fingerprint");
                string savedDocument = (string)Field(window, "savedDocument");
                string savedFile = File.ReadAllText(folder + "/scenarios.csv");
                Assert.That(window.hasUnsavedChanges, Is.False);

                Undo.PerformUndo();
                Assert.That(FirstText(window), Is.EqualTo("変更前"));
                Assert.That(window.hasUnsavedChanges, Is.True);
                Assert.That(Field(window, "fingerprint"), Is.EqualTo(fingerprint));
                Assert.That(Field(window, "savedDocument"), Is.EqualTo(savedDocument));
                Assert.That(File.ReadAllText(folder + "/scenarios.csv"), Is.EqualTo(savedFile));

                Undo.PerformRedo();
                Assert.That(FirstText(window), Is.EqualTo("保存した本文\n次の行"));
                Assert.That(window.hasUnsavedChanges, Is.False);
                Assert.That(Field(window, "fingerprint"), Is.EqualTo(fingerprint));
            }
            finally
            {
                if (draft != null) Undo.ClearUndo(draft);
                if (window != null) { window.DiscardChanges(); Object.DestroyImmediate(window); }
                AssetDatabase.DeleteAsset(folder);
            }
        }

        [Test]
        public void EditorLayoutSerializationRestoresUnsavedDraftAndItsSavedBaseline()
        {
            string layoutPath = "Temp/ScenarioCsvWindow_" + Guid.NewGuid().ToString("N") + ".wlt";
            Directory.CreateDirectory("Temp");
            var window = (EditorWindow)ScriptableObject.CreateInstance(CsvType("ScenarioCsvWindow"));
            Object draft = null;
            Object[] restored = Array.Empty<Object>();
            try
            {
                var document = Activator.CreateInstance(CsvType("ScenarioCsvDocument"));
                var row = Activator.CreateInstance(CsvType("ScenarioCsvRow"));
                Set(row, "id", "layout-draft"); Set(row, "text", "保存済み本文");
                ((IList)document.GetType().GetField("rows").GetValue(document)).Add(row);
                Invoke(window, "AcceptDocument", document, "Assets/__ScenarioCsvWindowLayoutTest");
                string savedDocument = (string)Field(window, "savedDocument");
                string fingerprint = (string)Field(window, "fingerprint");
                Invoke(window, "Change", (Action)(() => Set(row, "text", "未保存の本文\n引用符\"と日本語")));
                draft = (Object)Field(window, "draft");
                Undo.FlushUndoRecordObjects();
                Undo.ClearUndo(draft);

                // Use the same native object serialization as Unity's editor layouts.
                // A DontSaveInEditor draft produces a native assertion here.
                UnityEditorInternal.InternalEditorUtility.SaveToSerializedFileAndForget(new Object[] { window, draft }, layoutPath, true);
                window.DiscardChanges();
                Object.DestroyImmediate(window);
                window = null;
                draft = null;

                restored = UnityEditorInternal.InternalEditorUtility.LoadSerializedFileAndForget(layoutPath);
                EditorWindow loadedWindow = null;
                foreach (var value in restored)
                    if (value != null && value.GetType() == CsvType("ScenarioCsvWindow")) loadedWindow = (EditorWindow)value;
                Assert.That(loadedWindow, Is.Not.Null);
                Assert.That((Object)Field(loadedWindow, "draft"), Is.Not.Null);
                Assert.That(FirstText(loadedWindow), Is.EqualTo("未保存の本文\n引用符\"と日本語"));
                Assert.That(loadedWindow.hasUnsavedChanges, Is.True);
                Assert.That(Field(loadedWindow, "savedDocument"), Is.EqualTo(savedDocument));
                Assert.That(Field(loadedWindow, "fingerprint"), Is.EqualTo(fingerprint));
                UnityEngine.TestTools.LogAssert.NoUnexpectedReceived();
            }
            finally
            {
                if (draft != null) Undo.ClearUndo(draft);
                if (window != null) { window.DiscardChanges(); Object.DestroyImmediate(window); }
                foreach (var value in restored)
                {
                    if (value == null) continue;
                    if (value is EditorWindow loadedWindow) loadedWindow.DiscardChanges();
                    Object.DestroyImmediate(value);
                }
                if (File.Exists(layoutPath)) File.Delete(layoutPath);
            }
        }
    }
}
