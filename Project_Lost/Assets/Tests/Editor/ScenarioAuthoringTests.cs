using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public class ScenarioAuthoringTests
{
    private string _folder;
    private int _assetNumber;
    private static Type RuntimeType(string name) => Type.GetType("ScenarioSystem.Model." + name + ", Assembly-CSharp", true);
    private static Type ServiceType => Type.GetType("ScenarioSystem.Editor.ScenarioAuthoringService, Assembly-CSharp-Editor", true);
    private static object Call(string name, params object[] arguments) => ServiceType.GetMethod(name, BindingFlags.Public | BindingFlags.Static).Invoke(null, arguments);
    private static IList Actions(Object scenario) => (IList)scenario.GetType().GetField("actions").GetValue(scenario);
    private static IList ValidateInEditor(string method, object value)
        => (IList)Type.GetType("ScenarioSystem.Editor.ScenarioValidationGUI, Assembly-CSharp-Editor", true)
            .GetMethod(method, BindingFlags.Public | BindingFlags.Static).Invoke(null, new[] { value });
    private static bool HasIssue(IList issues, string code)
        => issues.Cast<object>().Any(issue => (string)issue.GetType().GetProperty("Code").GetValue(issue) == code);

    [SetUp]
    public void SetUp()
    {
        _folder = "Assets/__ScenarioAuthoringTest_" + Guid.NewGuid().ToString("N");
        AssetDatabase.CreateFolder("Assets", _folder.Substring("Assets/".Length));
        Undo.IncrementCurrentGroup();
        Call("Invalidate");
    }

    [TearDown]
    public void TearDown()
    {
        foreach (string guid in AssetDatabase.FindAssets("", new[] { _folder }))
            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GUIDToAssetPath(guid)))
                if (asset != null) Undo.ClearUndo(asset);
        AssetDatabase.DeleteAsset(_folder);
        Call("Invalidate");
        Undo.IncrementCurrentGroup();
    }

    private Object CreateAsset(string type)
    {
        var asset = ScriptableObject.CreateInstance(RuntimeType(type));
        asset.name = type.Replace('.', '_') + ++_assetNumber;
        AssetDatabase.CreateAsset(asset, _folder + "/" + asset.name + ".asset");
        return asset;
    }

    private static Object ReimportFirstAction(Object scenario)
    {
        string path = AssetDatabase.GetAssetPath(scenario);
        AssetDatabase.SaveAssetIfDirty(scenario);
        AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
        var reloaded = AssetDatabase.LoadAssetAtPath(path, scenario.GetType());
        var restored = (Object)Actions(reloaded)[0];
        Assert.That(restored != null && AssetDatabase.IsSubAsset(restored), Is.True);
        Assert.That(AssetDatabase.GetAssetPath(restored), Is.EqualTo(path));
        return restored;
    }

    [Test]
    public void NewActionIsStoredInsideItsScenarioAndSurvivesSaving()
    {
        var scenario = CreateAsset("ScenarioData");
        var action = (Object)Call("AddAction", scenario, RuntimeType("Actions.WaitAction"));
        AssetDatabase.SaveAssetIfDirty(scenario);
        Assert.That(Actions(scenario).Count, Is.EqualTo(1));
        Assert.That(Actions(scenario)[0], Is.SameAs(action));
        Assert.That(AssetDatabase.IsSubAsset(action), Is.True);
        Assert.That(AssetDatabase.GetAssetPath(action), Is.EqualTo(AssetDatabase.GetAssetPath(scenario)));
        Assert.That(AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(scenario)), Does.Contain(action));
    }

    [Test]
    public void AddingActionSupportsUndoAndRedoIncludingSubassetOwnership()
    {
        var scenario = CreateAsset("ScenarioData");
        Call("AddAction", scenario, RuntimeType("Actions.WaitAction"));
        Undo.FlushUndoRecordObjects();
        Undo.PerformUndo();
        Assert.That(Actions(scenario), Is.Empty);
        Undo.PerformRedo();
        Assert.That(Actions(scenario).Count, Is.EqualTo(1));
        var restored = (Object)Actions(scenario)[0];
        Assert.That(restored != null, Is.True);
        Assert.That(AssetDatabase.IsSubAsset(restored), Is.True, "Redo must restore the asset's ownership, not only its object reference.");
        Assert.That(AssetDatabase.GetAssetPath(restored), Is.EqualTo(AssetDatabase.GetAssetPath(scenario)));
        Undo.PerformUndo();
        Assert.That(Actions(scenario), Is.Empty);
        Undo.PerformRedo();
        Assert.That(Actions(scenario).Count, Is.EqualTo(1));
        ReimportFirstAction(scenario);
    }

    [Test]
    public void DuplicatingSharedActionCopiesItsValuesWithoutChangingOtherScenarios()
    {
        var first = CreateAsset("ScenarioData");
        var second = CreateAsset("ScenarioData");
        var shared = CreateAsset("Actions.WaitAction");
        shared.GetType().GetField("duration").SetValue(shared, 2.75f);
        Actions(first).Add(shared);
        Actions(second).Add(shared);
        var copy = (Object)Call("DuplicateAction", first, 0);
        Assert.That(copy, Is.Not.SameAs(shared));
        Assert.That(copy.GetType().GetField("duration").GetValue(copy), Is.EqualTo(2.75f));
        Assert.That(Actions(first)[0], Is.SameAs(copy));
        Assert.That(Actions(second)[0], Is.SameAs(shared));
        Assert.That(AssetDatabase.IsMainAsset(shared), Is.True);
        Assert.That(AssetDatabase.IsSubAsset(copy), Is.True);
        Undo.FlushUndoRecordObjects();
        Undo.PerformUndo();
        Assert.That(Actions(first)[0], Is.SameAs(shared));
        Assert.That(Actions(second)[0], Is.SameAs(shared));
        Undo.PerformRedo();
        var restored = (Object)Actions(first)[0];
        Assert.That(restored != null, Is.True);
        Assert.That(AssetDatabase.IsSubAsset(restored), Is.True);
        Assert.That(restored.GetType().GetField("duration").GetValue(restored), Is.EqualTo(2.75f));
        var reimported = ReimportFirstAction(first);
        Assert.That(reimported.GetType().GetField("duration").GetValue(reimported), Is.EqualTo(2.75f));
    }

    [TestCase(false)]
    [TestCase(true)]
    public void RemovingReferenceKeepsOwnedAndExternalAssetsAndIsUndoable(bool owned)
    {
        var scenario = CreateAsset("ScenarioData");
        var action = owned ? (Object)Call("AddAction", scenario, RuntimeType("Actions.WaitAction"))
            : CreateAsset("Actions.WaitAction");
        if (!owned) Actions(scenario).Add(action);
        string path = AssetDatabase.GetAssetPath(action);
        Undo.IncrementCurrentGroup();
        Call("RemoveActionReference", scenario, 0);
        Undo.FlushUndoRecordObjects();
        Assert.That(Actions(scenario), Is.Empty);
        Assert.That(action != null && AssetDatabase.Contains(action), Is.True);
        Assert.That(AssetDatabase.GetAssetPath(action), Is.EqualTo(path));
        Undo.PerformUndo();
        Assert.That(Actions(scenario)[0], Is.SameAs(action));
        Undo.PerformRedo();
        Assert.That(Actions(scenario), Is.Empty);
        Assert.That(action != null && AssetDatabase.Contains(action), Is.True);
    }

    [Test]
    public void SharedReferenceCountCountsScenariosAndUpdatesAfterDuplicationAndUndo()
    {
        var first = CreateAsset("ScenarioData");
        var second = CreateAsset("ScenarioData");
        var shared = CreateAsset("Actions.WaitAction");
        Actions(first).Add(shared);
        Actions(first).Add(shared);
        Actions(second).Add(shared);
        Call("Invalidate");
        Assert.That(Call("ReferenceCount", shared), Is.EqualTo(2), "Repeated use inside one scenario is one referencing scenario.");
        Call("DuplicateAction", second, 0);
        Assert.That(Call("ReferenceCount", shared), Is.EqualTo(1));
        Undo.FlushUndoRecordObjects();
        Undo.PerformUndo();
        Assert.That(Call("ReferenceCount", shared), Is.EqualTo(2));
    }

    [Test]
    public void InvalidActionTypeDoesNotChangeScenarioOrCreateAssets()
    {
        var scenario = CreateAsset("ScenarioData");
        var error = Assert.Throws<TargetInvocationException>(() => Call("AddAction", scenario, typeof(GameObject)));
        Assert.That(error.InnerException, Is.TypeOf<ArgumentException>());
        Assert.That(Actions(scenario), Is.Empty);
        Assert.That(AssetDatabase.LoadAllAssetsAtPath(AssetDatabase.GetAssetPath(scenario)).Count(asset => asset != null), Is.EqualTo(1));
    }

    private Object CreateDatabase()
    {
        var type = Type.GetType("ScenarioSystem.Adapter.ScenarioDataDatabase, Assembly-CSharp", true);
        var database = ScriptableObject.CreateInstance(type);
        AssetDatabase.CreateAsset(database, _folder + "/Database" + ++_assetNumber + ".asset");
        return database;
    }

    [Test]
    public void DatabaseValidationReportsMissingAndRepeatedRegistrationsWithoutChangingThem()
    {
        var scenario = CreateAsset("ScenarioData");
        var database = CreateDatabase();
        var registrations = (IList)database.GetType().GetField("allScenarios").GetValue(database);
        registrations.Add(scenario);
        registrations.Add(null);
        registrations.Add(scenario);
        Call("Invalidate");
        var issues = ValidateInEditor("ValidateDatabase", database);
        Assert.That(HasIssue(issues, "NULL_REGISTRATION"), Is.True);
        Assert.That(HasIssue(issues, "REPEATED_REGISTRATION"), Is.True);
        Assert.That(registrations.Count, Is.EqualTo(3));
        Assert.That(registrations[1], Is.Null);
    }

    [Test]
    public void SelectedScenarioValidationResolvesKeywordTargetsRegisteredInProjectDatabases()
    {
        var source = CreateAsset("ScenarioData");
        var destination = CreateAsset("ScenarioData");
        string keywordId = "authoring_test_" + Guid.NewGuid().ToString("N");
        destination.GetType().GetField("scenarioId").SetValue(destination, keywordId);
        var dialogue = (Object)Call("AddAction", source, RuntimeType("Actions.DialogueAction"));
        var entry = Activator.CreateInstance(RuntimeType("Actions.DialogueEntry"));
        entry.GetType().GetField("text").SetValue(entry, "<link=\"" + keywordId + "\">単語</link>");
        ((IList)dialogue.GetType().GetField("entries").GetValue(dialogue)).Add(entry);
        var selection = Array.CreateInstance(RuntimeType("ScenarioData"), 1);
        selection.SetValue(source, 0);
        var database = CreateDatabase();
        var registrations = (IList)database.GetType().GetField("allScenarios").GetValue(database);
        registrations.Add(source);
        Call("Invalidate");
        var before = ValidateInEditor("ValidateSelection", selection);
        Assert.That(HasIssue(before, "MISSING_KEYWORD_SCENARIO"), Is.True);
        registrations.Add(destination);
        Call("Invalidate");
        var after = ValidateInEditor("ValidateSelection", selection);
        Assert.That(HasIssue(after, "MISSING_KEYWORD_SCENARIO"), Is.False);
        Assert.That(Actions(source)[0], Is.SameAs(dialogue), "Validation must not mutate scenario data.");
    }

    [Test]
    public void IndependentDatabasesDoNotShareIdsOrKeywordResolution()
    {
        var first = CreateAsset("ScenarioData");
        var second = CreateAsset("ScenarioData");
        var detail = CreateAsset("ScenarioData");
        string sharedId = "isolated_root_" + Guid.NewGuid().ToString("N");
        string detailId = "isolated_detail_" + Guid.NewGuid().ToString("N");
        first.GetType().GetField("scenarioId").SetValue(first, sharedId);
        second.GetType().GetField("scenarioId").SetValue(second, sharedId);
        detail.GetType().GetField("scenarioId").SetValue(detail, detailId);
        var dialogue = (Object)Call("AddAction", first, RuntimeType("Actions.DialogueAction"));
        var entry = Activator.CreateInstance(RuntimeType("Actions.DialogueEntry"));
        entry.GetType().GetField("text").SetValue(entry, "<link=\"" + detailId + "\">単語</link>");
        ((IList)dialogue.GetType().GetField("entries").GetValue(dialogue)).Add(entry);
        var firstDatabase = CreateDatabase();
        var secondDatabase = CreateDatabase();
        ((IList)firstDatabase.GetType().GetField("allScenarios").GetValue(firstDatabase)).Add(first);
        var secondRegistrations = (IList)secondDatabase.GetType().GetField("allScenarios").GetValue(secondDatabase);
        secondRegistrations.Add(second);
        secondRegistrations.Add(detail);
        Call("Invalidate");
        var firstIssues = ValidateInEditor("ValidateDatabase", firstDatabase);
        Assert.That(HasIssue(firstIssues, "DUPLICATE_ID"), Is.False);
        Assert.That(HasIssue(firstIssues, "MISSING_KEYWORD_SCENARIO"), Is.True,
            "A keyword target in another database is unavailable to this database.");
        Assert.That(HasIssue(ValidateInEditor("ValidateDatabase", secondDatabase), "DUPLICATE_ID"), Is.False);
        var selection = Array.CreateInstance(RuntimeType("ScenarioData"), 1);
        selection.SetValue(first, 0);
        var selectedIssues = ValidateInEditor("ValidateSelection", selection);
        Assert.That(HasIssue(selectedIssues, "DUPLICATE_ID"), Is.False);
        Assert.That(HasIssue(selectedIssues, "MISSING_KEYWORD_SCENARIO"), Is.True);
    }
}
