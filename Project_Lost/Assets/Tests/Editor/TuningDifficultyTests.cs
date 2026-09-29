using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TuningDifficultyTests
{
    private const BindingFlags InstanceFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    private const BindingFlags StaticFlags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;

    private static Type GameType(string name) => Type.GetType(name + ", Assembly-CSharp", true);

    private static void Set(object target, string name, object value) =>
        target.GetType().GetField(name, InstanceFlags).SetValue(target, value);

    private static object Call(object target, string name, params object[] args) =>
        target.GetType().GetMethod(name, InstanceFlags).Invoke(target, args);

    [Test]
    public void StageOneUsesOneBlockWithoutInertia()
    {
        var stageOne = AssetDatabase.LoadMainAssetAtPath(
            "Assets/ScriptableObjects/Tuning_Settings/Stage_1.asset");
        var serialized = new SerializedObject(stageOne);

        Assert.That(serialized.FindProperty("activeBlockCount").intValue, Is.EqualTo(1));
        Assert.That(serialized.FindProperty("useInertia").boolValue, Is.False);
    }

    [Test]
    public void HigherStagesUseTwoBlocksWithInertia()
    {
        for (int stage = 2; stage <= 9; stage++)
        {
            var asset = AssetDatabase.LoadMainAssetAtPath(
                $"Assets/ScriptableObjects/Tuning_Settings/Stage_{stage}.asset");
            var serialized = new SerializedObject(asset);

            Assert.That(serialized.FindProperty("activeBlockCount").intValue, Is.EqualTo(2), $"Stage {stage}");
            Assert.That(serialized.FindProperty("useInertia").boolValue, Is.True, $"Stage {stage}");
        }
    }

    [Test]
    public void OneBlockCentersLeftPanelAndScalesPointsThenTwoBlocksRestoreLayout()
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var managerObject = NewObject(scene, "Manager");
            var manager = managerObject.AddComponent(GameType("Tuning.Core.TuningManager"));
            var leftPanel = NewRect(scene, "LeftPanel", new Vector2(-620f, -200f));
            var rightPanel = NewRect(scene, "RightPanel", new Vector2(620f, -200f));
            var leftPoint = NewRect(scene, "LeftPoint", Vector2.zero, leftPanel);
            var rightPoint = NewRect(scene, "RightPoint", Vector2.zero, rightPanel);
            leftPoint.sizeDelta = rightPoint.sizeDelta = new Vector2(25f, 25f);

            Set(manager, "leftBoundsArea", leftPanel);
            Set(manager, "rightBoundsArea", rightPanel);
            Set(manager, "leftPoint", leftPoint);
            Set(manager, "rightPoint", rightPoint);
            Set(manager, "pointSizeMultiplier", 1.5f);
            Call(manager, "CaptureInitialLayout");

            Set(manager, "_activeBlockCount", 1);
            Call(manager, "ApplyBlockLayout");
            Assert.That(leftPanel.anchoredPosition, Is.EqualTo(new Vector2(0f, -200f)));
            Assert.That(rightPanel.gameObject.activeSelf, Is.False);
            Assert.That(leftPoint.sizeDelta, Is.EqualTo(new Vector2(37.5f, 37.5f)));

            Set(manager, "_activeBlockCount", 2);
            Call(manager, "ApplyBlockLayout");
            Assert.That(leftPanel.anchoredPosition, Is.EqualTo(new Vector2(-620f, -200f)));
            Assert.That(rightPanel.anchoredPosition, Is.EqualTo(new Vector2(620f, -200f)));
            Assert.That(rightPanel.gameObject.activeSelf, Is.True);
            Assert.That(rightPoint.sizeDelta, Is.EqualTo(new Vector2(37.5f, 37.5f)));
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [Test]
    public void BlockProximityChangesAcrossTheWholePanel()
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            RectTransform bounds = NewRect(scene, "Bounds", Vector2.zero);
            bounds.sizeDelta = new Vector2(500f, 500f);
            RectTransform target = NewRect(scene, "Target", new Vector2(100f, 50f), bounds);
            RectTransform point = NewRect(scene, "Point", new Vector2(-200f, -200f), bounds);
            MethodInfo calculate = GameType("Tuning.Core.TuningManager")
                .GetMethod("CalculateBlockProximity", StaticFlags);

            float far = (float)calculate.Invoke(null, new object[] { point, target, bounds });
            point.anchoredPosition = new Vector2(0f, 0f);
            float near = (float)calculate.Invoke(null, new object[] { point, target, bounds });
            point.anchoredPosition = target.anchoredPosition;
            float exact = (float)calculate.Invoke(null, new object[] { point, target, bounds });

            Assert.That(far, Is.InRange(0f, 1f));
            Assert.That(near, Is.GreaterThan(far));
            Assert.That(exact, Is.EqualTo(1f).Within(0.0001f));
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [Test]
    public void OneBlockClearCalculationIgnoresTheUnusedRightPoint()
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var managerObject = NewObject(scene, "Manager");
            var manager = managerObject.AddComponent(GameType("Tuning.Core.TuningManager"));
            RectTransform leftBounds = NewRect(scene, "LeftBounds", Vector2.zero);
            leftBounds.sizeDelta = new Vector2(500f, 500f);
            RectTransform rightBounds = NewRect(scene, "RightBounds", Vector2.zero);
            rightBounds.sizeDelta = new Vector2(500f, 500f);
            RectTransform leftPoint = NewRect(scene, "LeftPoint", new Vector2(50f, 50f), leftBounds);
            RectTransform leftTarget = NewRect(scene, "LeftTarget", leftPoint.anchoredPosition, leftBounds);
            RectTransform rightPoint = NewRect(scene, "RightPoint", new Vector2(-250f, -250f), rightBounds);
            RectTransform rightTarget = NewRect(scene, "RightTarget", new Vector2(250f, 250f), rightBounds);
            var settings = AssetDatabase.LoadMainAssetAtPath(
                "Assets/ScriptableObjects/Tuning_Settings/Stage_1.asset");

            Set(manager, "_currentSettings", settings);
            Set(manager, "_activeBlockCount", 1);
            Set(manager, "leftPoint", leftPoint);
            Set(manager, "leftTarget", leftTarget);
            Set(manager, "leftBoundsArea", leftBounds);
            Set(manager, "rightPoint", rightPoint);
            Set(manager, "rightTarget", rightTarget);
            Set(manager, "rightBoundsArea", rightBounds);

            Call(manager, "UpdateSyncRate");

            float totalSync = (float)manager.GetType().GetProperty("TotalSync").GetValue(manager);
            Assert.That(totalSync, Is.EqualTo(1f).Within(0.0001f));
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    [Test]
    public void ReferenceWaveStaysVisibleAndBothModesAlignOnlyWhenComplete()
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var feedback = NewObject(scene, "Feedback")
                .AddComponent(GameType("Tuning.Core.TuningFeedback"));
            Component redWave = NewWave(scene, "RedWave");
            Component blueWave = NewWave(scene, "BlueWave");
            Set(feedback, "leftWaveform", redWave);
            Set(feedback, "rightWaveform", blueWave);
            Set(feedback, "minWaveFreq", 100f);
            Set(feedback, "maxWaveFreq", 10f);
            Set(feedback, "minWaveAmp", 300f);
            Set(feedback, "maxWaveAmp", 60f);
            Set(feedback, "unmatchedPhaseOffset", Mathf.PI);

            Call(feedback, "ConfigureBlockLayout", 1, Vector2.zero, Vector2.zero);
            Call(feedback, "ResetFeedback");
            Assert.That(blueWave.gameObject.activeSelf, Is.True);
            AssertWave(blueWave, 10f, 60f, 0f);
            AssertWave(redWave, 100f, 300f, Mathf.PI);

            Call(feedback, "OnSyncUpdate", 1f, 1f, true, false, 0.9f, 1f);
            AssertWave(redWave, 10f, 60f, 0f);

            Call(feedback, "ConfigureBlockLayout", 2, Vector2.zero, Vector2.zero);
            Call(feedback, "ResetFeedback");
            Call(feedback, "OnSyncUpdate", 0.5f, 0.5f, true, false, 1f, 0.4f);
            Assert.That(GetFloat(redWave, "Frequency"), Is.Not.EqualTo(GetFloat(blueWave, "Frequency")));

            Call(feedback, "OnSyncUpdate", 1f, 1f, true, true, 0.9f, 0.9f);
            AssertWave(redWave, 10f, 60f, 0f);
            AssertWave(blueWave, 10f, 60f, 0f);
        }
        finally
        {
            EditorSceneManager.ClosePreviewScene(scene);
        }
    }

    private static Component NewWave(Scene scene, string name)
    {
        var gameObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer));
        SceneManager.MoveGameObjectToScene(gameObject, scene);
        return gameObject.AddComponent(GameType("Tuning.Visuals.WaveformVisualizer"));
    }

    private static float GetFloat(Component component, string property) =>
        (float)component.GetType().GetProperty(property).GetValue(component);

    private static void AssertWave(Component wave, float frequency, float amplitude, float phase)
    {
        Assert.That(GetFloat(wave, "Frequency"), Is.EqualTo(frequency).Within(0.0001f));
        Assert.That(GetFloat(wave, "Amplitude"), Is.EqualTo(amplitude).Within(0.0001f));
        Assert.That(GetFloat(wave, "PhaseOffset"), Is.EqualTo(phase).Within(0.0001f));
    }

    private static GameObject NewObject(Scene scene, string name)
    {
        var gameObject = new GameObject(name);
        SceneManager.MoveGameObjectToScene(gameObject, scene);
        return gameObject;
    }

    private static RectTransform NewRect(
        Scene scene,
        string name,
        Vector2 position,
        RectTransform parent = null)
    {
        var gameObject = new GameObject(name, typeof(RectTransform));
        SceneManager.MoveGameObjectToScene(gameObject, scene);
        var rect = (RectTransform)gameObject.transform;
        if (parent != null)
            rect.SetParent(parent, false);
        rect.anchoredPosition = position;
        return rect;
    }
}
