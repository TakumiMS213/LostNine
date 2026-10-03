using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public class TeichakuTests
{
    private const BindingFlags Fields = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static Type GameType(string name) => Type.GetType(name + ", Assembly-CSharp", true);
    private static void Set(object target, string field, object value) => target.GetType().GetField(field, Fields).SetValue(target, value);
    private static object Call(object target, string method, params object[] args) => target.GetType().GetMethod(method, Fields).Invoke(target, args);
    private static bool Complete(object timer) => (bool)timer.GetType().GetProperty("IsComplete").GetValue(timer);
    private static object Timer(bool held) => Activator.CreateInstance(GameType("Teichaku.Core.ResultDisplayTimer"), 5f, held);

    [Test]
    public void ResultWaitsFiveSecondsWithoutClick()
    {
        object timer = Timer(false);
        Call(timer, "Advance", 4.99f, false, false);
        Assert.That(Complete(timer), Is.False);
        Call(timer, "Advance", 0.02f, false, false);
        Assert.That(Complete(timer), Is.True);
    }

    [Test]
    public void ClearDragMustBeReleasedBeforeClickCanSkip()
    {
        object timer = Timer(true);
        Call(timer, "Advance", 0.1f, true, true);
        Assert.That(Complete(timer), Is.False, "Clear click must not skip");
        Call(timer, "Advance", 0.5f, true, false);
        Call(timer, "Advance", 0.1f, false, false);
        Assert.That(Complete(timer), Is.False, "Releasing the clear drag must not skip");
        Call(timer, "Advance", 0.1f, true, true);
        Assert.That(Complete(timer), Is.True);
    }

    [Test]
    public void FreshResultClickSkipsBeforeTimeout()
    {
        object timer = Timer(false);
        Call(timer, "Advance", 0.1f, true, true);
        Assert.That(Complete(timer), Is.True);
    }

    [Test]
    public void LeafBoardHasACompleteSolutionAndFitsTheDisplay()
    {
        var data = AssetDatabase.LoadMainAssetAtPath("Assets/ScriptableObjects/TeichakuData/Ch2_Leaf.asset");
        Vector2Int[] path = Solve(data, "Solved");
        Assert.That(path.Length, Is.EqualTo(28));
        Assert.That(new System.Collections.Generic.HashSet<Vector2Int>(path).Count, Is.EqualTo(path.Length));
        for (int i = 1; i < path.Length; i++)
            Assert.That(Mathf.Abs(path[i].x - path[i - 1].x) + Mathf.Abs(path[i].y - path[i - 1].y), Is.EqualTo(1));

        Scene scene = EditorSceneManager.NewPreviewScene();
        try
        {
            var manager = NewObject(scene, "Manager").AddComponent(GameType("Teichaku.Core.TeichakuManager"));
            var grid = (RectTransform)NewObject(scene, "Grid", typeof(RectTransform)).transform;
            var tileObject = NewObject(scene, "Tile", typeof(RectTransform), typeof(UnityEngine.UI.Image));
            var tile = tileObject.AddComponent(GameType("Teichaku.Core.TeichakuTile"));
            Set(manager, "gridParent", grid);
            Set(manager, "tilePrefab", tile);
            Set(manager, "maxBoardSize", new Vector2(1000f, 650f));
            Call(manager, "SetStageData", data);
            Assert.That(grid.childCount, Is.EqualTo(28));
            Assert.That(grid.sizeDelta.x, Is.LessThanOrEqualTo(1000.01f));
            Assert.That(grid.sizeDelta.y, Is.LessThanOrEqualTo(650.01f));
            bool cleared = false;
            manager.GetType().GetEvent("OnTeichakuClear").AddEventHandler(manager, new Action(() => cleared = true));
            for (int i = 0; i < path.Length; i++)
            {
                Component next = null;
                foreach (Transform child in grid)
                {
                    var t = child.GetComponent(GameType("Teichaku.Core.TeichakuTile"));
                    if ((int)t.GetType().GetProperty("GridX").GetValue(t) == path[i].x &&
                        (int)t.GetType().GetProperty("GridY").GetValue(t) == path[i].y) next = t;
                }
                Assert.That(next, Is.Not.Null);
                Call(manager, i == 0 ? "OnTilePointerDown" : "OnTilePointerEnter", next);
                if (i < path.Length - 1) Assert.That(cleared, Is.False);
            }
            Assert.That(cleared, Is.True);
            Assert.That((bool)manager.GetType().GetProperty("IsActive").GetValue(manager), Is.False);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); }
    }

    [Test]
    public void SolverRejectsDisconnectedOrBranchedImpossibleShapes()
    {
        var data = ScriptableObject.CreateInstance(GameType("Teichaku.Data.TeichakuStageData"));
        try
        {
            Set(data, "width", 3); Set(data, "height", 3);
            Set(data, "tileActive", new[] {true, false, false, false, false, false, false, false, true});
            Solve(data, "Unsolvable");
            Set(data, "tileActive", new[] {false, true, false, true, true, true, false, true, false});
            Solve(data, "Unsolvable");
        }
        finally { UnityEngine.Object.DestroyImmediate(data); }
    }

    [Test]
    public void SingleTileShapeCanClearOnInitialPress()
    {
        Scene scene = EditorSceneManager.NewPreviewScene();
        var data = ScriptableObject.CreateInstance(GameType("Teichaku.Data.TeichakuStageData"));
        try
        {
            Set(data, "width", 1); Set(data, "height", 1); Set(data, "tileActive", new[] {true});
            var manager = NewObject(scene, "Manager").AddComponent(GameType("Teichaku.Core.TeichakuManager"));
            var grid = (RectTransform)NewObject(scene, "Grid", typeof(RectTransform)).transform;
            var tile = NewObject(scene, "Tile", typeof(RectTransform), typeof(UnityEngine.UI.Image))
                .AddComponent(GameType("Teichaku.Core.TeichakuTile"));
            Set(manager, "gridParent", grid); Set(manager, "tilePrefab", tile);
            Call(manager, "SetStageData", data);
            bool cleared = false;
            manager.GetType().GetEvent("OnTeichakuClear").AddEventHandler(manager, new Action(() => cleared = true));
            Call(manager, "OnTilePointerDown", grid.GetChild(0).GetComponent(GameType("Teichaku.Core.TeichakuTile")));
            Assert.That(cleared, Is.True);
        }
        finally { EditorSceneManager.ClosePreviewScene(scene); UnityEngine.Object.DestroyImmediate(data); }
    }

    private static Vector2Int[] Solve(object data, string expected)
    {
        var solver = Type.GetType("Teichaku.Editor.TeichakuStageSolver, Assembly-CSharp-Editor", true);
        object[] args = { data, null, 200000 };
        object result = solver.GetMethod("Solve").Invoke(null, args);
        Assert.That(result.ToString(), Is.EqualTo(expected));
        return (Vector2Int[])args[1];
    }

    private static GameObject NewObject(Scene scene, string name, params Type[] components)
    {
        var result = new GameObject(name, components);
        SceneManager.MoveGameObjectToScene(result, scene);
        return result;
    }
}
