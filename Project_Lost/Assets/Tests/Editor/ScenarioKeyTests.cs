using System;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using UnityEditor;

public class ScenarioKeyTests
{
    private static Type GameType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private static object KeyCall(string method, params object[] args) =>
        GameType("ScenarioSystem.Model.ScenarioKey").GetMethod(method).Invoke(null, args);

    [TestCase(null, "")]
    [TestCase("", "")]
    [TestCase(" \t\r\n", "")]
    [TestCase(" Ch2_Dialogue \n", "Ch2_Dialogue")]
    [TestCase("Data1", "Data1")]
    [TestCase(" Ch2_loop ", "Ch2_loop")]
    [TestCase(" ch2_LOOP ", "ch2_LOOP")]
    public void NormalizationOnlyRemovesSurroundingWhitespace(string input, string expected)
    {
        Assert.That(KeyCall("Normalize", input), Is.EqualTo(expected));
    }

    [TestCase("Prologue", "Ch2_Prologue")]
    [TestCase("Dialogue", "Ch2_Dialogue")]
    [TestCase("Extraction", "Ch2_Extraction")]
    [TestCase("Tuning", "Ch2_Tuning")]
    [TestCase("Fixation", "Ch2_Fixation")]
    [TestCase("Presentation", "Ch2_Presentation")]
    [TestCase("Epilogue", "Ch2_Epilogue")]
    public void PhaseKeysAndConversationStartKeepTheirExistingBehavior(string phaseName, string expected)
    {
        object phase = Enum.Parse(GameType("GamePhase"), phaseName);
        Assert.That(KeyCall("ForPhase", 2, phase), Is.EqualTo(expected));

        object startInfo = GameType("Communication.ComuLogic").GetMethod("ResolveScenarioId").Invoke(null, new[] { (object)2, phase });
        Assert.That(startInfo.GetType().GetField("ScenarioId").GetValue(startInfo), Is.EqualTo(expected));
        Assert.That(startInfo.GetType().GetField("EnableKeywords").GetValue(startInfo), Is.EqualTo(phaseName == "Extraction"));
    }

    [TestCase("Story", "Ch12_Story")]
    [TestCase("Loop", "Ch12_loop")]
    [TestCase("DialogueStart", "Ch12_DialogueStart")]
    public void PurposeKeysKeepTheirExistingCapitalization(string purposeName, string expected)
    {
        object purpose = Enum.Parse(GameType("ScenarioSystem.Model.ScenarioPurpose"), purposeName);
        Assert.That(KeyCall("ForPurpose", 12, purpose), Is.EqualTo(expected));
        if (purposeName == "Loop")
            Assert.That(GameType("Communication.ComuLogic").GetMethod("ResolveEndScenarioId").Invoke(null, new object[] { 12 }), Is.EqualTo(expected));
    }

    [TestCase("Ch1_Story", 1)]
    [TestCase("Ch2_DialogueStart", 2)]
    [TestCase("Ch12_loop", 12)]
    [TestCase("Ch002_Extraction", 2)]
    [TestCase(" Ch2_Presentation_Truth \n", 2)]
    [TestCase("Ch2147483647_Story", int.MaxValue)]
    public void ChapterParsingAcceptsCompleteChapterKeys(string input, int expected)
    {
        AssertParse(input, true, expected);
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" \t\n")]
    [TestCase("Data1")]
    [TestCase("Ch2")]
    [TestCase("Ch2_")]
    [TestCase("Ch2_ \t")]
    [TestCase("Ch2Story")]
    [TestCase("Ch2Other_Story")]
    [TestCase("Ch_Story")]
    [TestCase("Ch0_Story")]
    [TestCase("Ch-2_Story")]
    [TestCase("Ch+2_Story")]
    [TestCase("ch2_Story")]
    [TestCase("CH2_Story")]
    [TestCase("Ch２_Story")]
    [TestCase("Ch٢_Story")]
    [TestCase("Ch2147483648_Story")]
    [TestCase("Ch999999999999999999999999999999_Story")]
    public void InvalidOrUnrelatedKeysCannotChangeTheCurrentChapter(string input)
    {
        AssertParse(input, false, 0);
    }

    private static void AssertParse(string input, bool expectedSuccess, int expectedChapter)
    {
        var arguments = new object[] { input, 99 };
        Assert.That(KeyCall("TryGetChapter", arguments), Is.EqualTo(expectedSuccess));
        Assert.That(arguments[1], Is.EqualTo(expectedChapter));

        // ProgressManager の既存呼び出し口も同じ規約で判定する。
        var wrapper = GameType("ProgressManager").GetMethod("TryParseChapterFromScenarioId", BindingFlags.Static | BindingFlags.NonPublic);
        arguments[1] = 99;
        Assert.That(wrapper.Invoke(null, arguments), Is.EqualTo(expectedSuccess));
        Assert.That(arguments[1], Is.EqualTo(expectedChapter));
    }

    [Test]
    public void ExistingAssetIdsKeepTheirIdentityAndChapterAssociation()
    {
        Type scenarioType = GameType("ScenarioSystem.Model.ScenarioData");
        FieldInfo idField = scenarioType.GetField("scenarioId");
        string[] guids = AssetDatabase.FindAssets("t:ScenarioData", new[] { "Assets/ScriptableObjects/ProgressSystemDatabase/ScenarioData" });
        Assert.That(guids, Is.Not.Empty, "実データを含む互換性チェックが必要。");

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            var scenario = AssetDatabase.LoadAssetAtPath(path, scenarioType);
            string id = (string)idField.GetValue(scenario);
            Assert.That(KeyCall("Normalize", id), Is.EqualTo(id ?? string.Empty), path);

            Match match = Regex.Match(id ?? string.Empty, "^Ch([0-9]+)_.+$");
            var arguments = new object[] { id, 0 };
            Assert.That(KeyCall("TryGetChapter", arguments), Is.EqualTo(match.Success), path);
            if (match.Success)
                Assert.That(arguments[1], Is.EqualTo(int.Parse(match.Groups[1].Value)), path);
            Assert.That(idField.GetValue(scenario), Is.EqualTo(id), "参照・検証時に保存済みIDを書き換えない: " + path);
        }
    }
}
