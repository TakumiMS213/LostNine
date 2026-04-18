#if UNITY_EDITOR
using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using TMPro;
using ScenarioSystem.Presenter;
using ScenarioSystem.Runtime;
using ScenarioSystem.View;
using ScenarioSystem.Adapter;
using MessageWindowSystem.Core;

/// <summary>
/// メニューからワンクリックで Main シーンにシナリオシステムのヒエラルキーを自動構築するエディタースクリプト。
/// 既存の MessageWindowManager / EffectManager の SerializedField を読み取り、
/// 新システムの各 View に自動結線する。
/// 
/// 使い方:
///   Unity メニュー → Scenario System → Setup Main Scene
/// </summary>
public static class ScenarioSystemSetup
{
    // ═══════════════════════════════════════════
    //  Menu Commands
    // ═══════════════════════════════════════════

    [MenuItem("Scenario System/Setup Main Scene", false, 0)]
    public static void SetupMainScene()
    {
        // ── 前提チェック ──
        var existingMWM = Object.FindObjectOfType<MessageWindowManager>();
        if (existingMWM == null)
        {
            EditorUtility.DisplayDialog("Error",
                "MessageWindowManager が見つかりません。\nMain シーンを開いた状態で実行してください。",
                "OK");
            return;
        }

        if (GameObject.Find("ScenarioSystem") != null || GameObject.Find("Views") != null)
        {
            if (!EditorUtility.DisplayDialog("確認",
                "ScenarioSystem / Views が既に存在します。削除して再作成しますか？",
                "再作成", "キャンセル"))
                return;

            DestroyImmediate("ScenarioSystem");
            DestroyImmediate("Views");
            DestroyImmediate("ScenarioAudio");
        }

        // ── 構築開始 ──
        Undo.SetCurrentGroupName("Setup Scenario System");
        int undoGroup = Undo.GetCurrentGroup();

        var systemRoot = BuildScenarioSystem(existingMWM);
        var audioRoot = BuildScenarioAudio();
        var viewsRoot = BuildViews();

        AutoWireAll(systemRoot, viewsRoot, audioRoot, existingMWM);

        Undo.CollapseUndoOperations(undoGroup);

        // ── 結果レポート ──
        var report = GenerateReport(systemRoot, viewsRoot);
        Debug.Log($"[ScenarioSystemSetup] ✅ Main シーンセットアップ完了\n{report}");

        EditorUtility.DisplayDialog("セットアップ完了",
            "ScenarioSystem・Views・ScenarioAudio を作成し、既存 UI への結線が完了しました。\n\n" +
            "【手動確認が必要な項目】\n" +
            "1. ClickArea の Button → OnClick に DialogueView.OnUserInput を設定\n" +
            "2. Inspector で各 View の結線を目視確認\n" +
            "3. ScenarioBootstrap の autoPlay = false を確認（本番用）\n\n" +
            "Console に詳細レポートを出力しました。",
            "OK");
    }

    [MenuItem("Scenario System/Setup Test Scene (Minimal)", false, 1)]
    public static void SetupTestScene()
    {
        if (GameObject.Find("ScenarioSystem") != null)
        {
            if (!EditorUtility.DisplayDialog("確認",
                "既存のセットアップを削除して再作成しますか？", "再作成", "キャンセル"))
                return;

            DestroyImmediate("ScenarioSystem");
            DestroyImmediate("Views");
            DestroyImmediate("ScenarioAudio");
        }

        Undo.SetCurrentGroupName("Setup Test Scenario System");
        int undoGroup = Undo.GetCurrentGroup();

        // ── Canvas ──
        var canvas = Object.FindObjectOfType<Canvas>();
        if (canvas == null)
        {
            var canvasObj = CreateAndRegister("Canvas");
            canvas = canvasObj.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObj.AddComponent<CanvasScaler>();
            canvasObj.AddComponent<GraphicRaycaster>();
        }

        // ── EventSystem ──
        if (Object.FindObjectOfType<UnityEngine.EventSystems.EventSystem>() == null)
        {
            var esObj = CreateAndRegister("EventSystem");
            esObj.AddComponent<UnityEngine.EventSystems.EventSystem>();
            esObj.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
        }

        // ── MessageWindow ──
        var messageWindow = CreateUIChild("MessageWindow", canvas.transform);
        SetAnchorStretchBottom(messageWindow.GetComponent<RectTransform>(), 250f);

        var bg = CreateUIChild("Background", messageWindow.transform);
        var bgImage = bg.AddComponent<Image>();
        bgImage.color = new Color(0, 0, 0, 0.7f);
        StretchFull(bg.GetComponent<RectTransform>());

        var speakerNameTextObj = CreateUIChild("SpeakerNameText", messageWindow.transform);
        var speakerNameText = speakerNameTextObj.AddComponent<TextMeshProUGUI>();
        speakerNameText.fontSize = 30;
        speakerNameText.color = Color.white;
        speakerNameText.text = "";
        var snRT = speakerNameTextObj.GetComponent<RectTransform>();
        snRT.anchorMin = new Vector2(0, 1);
        snRT.anchorMax = new Vector2(1, 1);
        snRT.pivot = new Vector2(0.5f, 1);
        snRT.anchoredPosition = new Vector2(0, -10);
        snRT.sizeDelta = new Vector2(0, 40);

        var dialogueTextObj = CreateUIChild("DialogueText", messageWindow.transform);
        var dialogueText = dialogueTextObj.AddComponent<TextMeshProUGUI>();
        dialogueText.fontSize = 24;
        dialogueText.color = Color.white;
        dialogueText.text = "";
        var dtRT = dialogueTextObj.GetComponent<RectTransform>();
        dtRT.anchorMin = Vector2.zero;
        dtRT.anchorMax = Vector2.one;
        dtRT.offsetMin = new Vector2(20, 20);
        dtRT.offsetMax = new Vector2(-20, -55);

        // ── ClickArea ──
        var clickArea = CreateUIChild("ClickArea", canvas.transform);
        var clickImg = clickArea.AddComponent<Image>();
        clickImg.color = new Color(0, 0, 0, 0);
        clickImg.raycastTarget = true;
        var clickBtn = clickArea.AddComponent<Button>();
        clickBtn.transition = Selectable.Transition.None;
        var nav = clickBtn.navigation;
        nav.mode = UnityEngine.UI.Navigation.Mode.None;
        clickBtn.navigation = nav;
        StretchFull(clickArea.GetComponent<RectTransform>());
        clickArea.transform.SetAsLastSibling();

        // ── ScenarioSystem ──
        var systemRoot = CreateAndRegister("ScenarioSystem");
        var presenter = systemRoot.AddComponent<ScenarioPresenter>();
        var bootstrap = systemRoot.AddComponent<ScenarioBootstrap>();
        SetField(bootstrap, "autoPlay", true);
        SetField(bootstrap, "presenter", presenter);

        // ── Views (minimal) ──
        var viewsRoot = CreateAndRegister("Views");

        var dvObj = CreateChild("DialogueViewObj", viewsRoot);
        var dv = dvObj.AddComponent<DialogueView>();

        var snvObj = CreateChild("SpeakerNameViewObj", viewsRoot);
        var snv = snvObj.AddComponent<SpeakerNameView>();

        // ── Auto-wire ──
        SetField(dv, "dialogueText", dialogueText);
        SetField(dv, "windowRoot", messageWindow);
        SetField(snv, "speakerNameText", speakerNameText);

        // ── ClickArea → DialogueView.OnUserInput ──
        UnityEditor.Events.UnityEventTools.AddVoidPersistentListener(
            clickBtn.onClick, new UnityEngine.Events.UnityAction(dv.OnUserInput));

        Undo.CollapseUndoOperations(undoGroup);

        Debug.Log("[ScenarioSystemSetup] ✅ テストシーンセットアップ完了");
        EditorUtility.DisplayDialog("セットアップ完了",
            "テストシーン用の最小構成を作成しました。\n\n" +
            "【次のステップ】\n" +
            "1. Create → Scenario → Actions → Dialogue でアクション SO を作成\n" +
            "2. Create → Scenario → Scenario Data でシナリオ SO を作成\n" +
            "3. ScenarioBootstrap の Test Scenario にドラッグ\n" +
            "4. Play で動作確認",
            "OK");
    }

    [MenuItem("Scenario System/Remove All Setup Objects", false, 100)]
    public static void RemoveSetup()
    {
        if (!EditorUtility.DisplayDialog("確認",
            "ScenarioSystem / Views / ScenarioAudio を削除しますか？\n（既存の UI には影響しません）",
            "削除", "キャンセル"))
            return;

        DestroyImmediate("ScenarioSystem");
        DestroyImmediate("Views");
        DestroyImmediate("ScenarioAudio");
        Debug.Log("[ScenarioSystemSetup] 削除完了");
    }

    [MenuItem("Scenario System/Validate Wiring", false, 50)]
    public static void ValidateWiring()
    {
        var views = new System.Type[]
        {
            typeof(DialogueView), typeof(SpeakerNameView), typeof(PortraitView),
            typeof(ChoiceView), typeof(EffectView), typeof(BackgroundStillView),
            typeof(KeywordView), typeof(DialogueLogView)
        };

        int warnings = 0;
        foreach (var viewType in views)
        {
            var obj = Object.FindObjectOfType(viewType) as Component;
            if (obj == null)
            {
                Debug.LogWarning($"[Validate] ❌ {viewType.Name} がシーン内に見つかりません");
                warnings++;
                continue;
            }

            var so = new SerializedObject(obj);
            var iter = so.GetIterator();
            while (iter.NextVisible(true))
            {
                if (iter.propertyType == SerializedPropertyType.ObjectReference
                    && iter.objectReferenceValue == null
                    && iter.name != "m_Script"
                    && !iter.name.StartsWith("m_"))
                {
                    Debug.LogWarning($"[Validate] ⚠️ {viewType.Name}.{iter.name} が未設定です");
                    warnings++;
                }
            }
        }

        var presenter = Object.FindObjectOfType<ScenarioPresenter>();
        if (presenter == null)
        {
            Debug.LogWarning("[Validate] ❌ ScenarioPresenter がシーン内に見つかりません");
            warnings++;
        }

        if (warnings == 0)
            Debug.Log("[Validate] ✅ 全ての結線が正常です");
        else
            Debug.LogWarning($"[Validate] {warnings} 件の警告があります。Inspector で確認してください。");
    }

    // ═══════════════════════════════════════════
    //  Build: ScenarioSystem
    // ═══════════════════════════════════════════

    private static GameObject BuildScenarioSystem(MessageWindowManager existingMWM)
    {
        var root = CreateAndRegister("ScenarioSystem");

        // Core
        var presenter = root.AddComponent<ScenarioPresenter>();
        var bootstrap = root.AddComponent<ScenarioBootstrap>();
        SetField(bootstrap, "presenter", presenter);
        SetField(bootstrap, "autoPlay", false); // 本番は false

        // Adapters
        root.AddComponent<ProgressAdapter>();
        root.AddComponent<ClueAdapter>();

        // ComuAdapterExtended → 既存 ComuManager
        var existingComu = Object.FindObjectOfType<ComuStartandEndManager>();
        var comuAdapter = root.AddComponent<ComuAdapterExtended>();
        if (existingComu != null)
            SetField(comuAdapter, "comuManager", existingComu);

        // MessageWindowFacade
        var facade = root.AddComponent<MessageWindowFacade>();
        SetField(facade, "presenter", presenter);

        // Facade に旧 MWM の参照をコピー
        var mwmSO = new SerializedObject(existingMWM);
        CopyField(mwmSO, "dialogueText", facade, "dialogueText");
        CopyField(mwmSO, "windowRoot", facade, "windowRoot");
        CopyField(mwmSO, "scenarioDatabase", facade, "legacyDatabase");

        return root;
    }

    // ═══════════════════════════════════════════
    //  Build: ScenarioAudio
    // ═══════════════════════════════════════════

    private static GameObject BuildScenarioAudio()
    {
        // 既存の EffectManager の AudioSource を確認
        var existingEM = Object.FindObjectOfType<EffectManager>();
        if (existingEM != null)
        {
            var emSO = new SerializedObject(existingEM);
            var existingSE = emSO.FindProperty("seAudioSource").objectReferenceValue;
            var existingBGM = emSO.FindProperty("bgmAudioSource").objectReferenceValue;

            // 既存の AudioSource がある場合は新規作成しない
            if (existingSE != null && existingBGM != null)
                return null;
        }

        var root = CreateAndRegister("ScenarioAudio");

        var seObj = CreateChild("AudioSource_SE", root);
        seObj.AddComponent<AudioSource>();

        var bgmObj = CreateChild("AudioSource_BGM", root);
        var bgmSource = bgmObj.AddComponent<AudioSource>();
        bgmSource.loop = true;

        return root;
    }

    // ═══════════════════════════════════════════
    //  Build: Views
    // ═══════════════════════════════════════════

    private static GameObject BuildViews()
    {
        var root = CreateAndRegister("Views");

        CreateChild("DialogueViewObj", root).AddComponent<DialogueView>();
        CreateChild("SpeakerNameViewObj", root).AddComponent<SpeakerNameView>();
        CreateChild("PortraitViewObj", root).AddComponent<PortraitView>();
        CreateChild("ChoiceViewObj", root).AddComponent<ChoiceView>();
        CreateChild("EffectViewObj", root).AddComponent<EffectView>();
        CreateChild("BackgroundStillViewObj", root).AddComponent<BackgroundStillView>();
        CreateChild("KeywordViewObj", root).AddComponent<KeywordView>();
        CreateChild("DialogueLogViewObj", root).AddComponent<DialogueLogView>();

        return root;
    }

    // ═══════════════════════════════════════════
    //  Auto-Wire All
    // ═══════════════════════════════════════════

    private static void AutoWireAll(
        GameObject systemRoot, GameObject viewsRoot,
        GameObject audioRoot, MessageWindowManager existingMWM)
    {
        var mwmSO = new SerializedObject(existingMWM);

        // ── DialogueView ──
        var dialogueView = viewsRoot.GetComponentInChildren<DialogueView>();
        CopyField(mwmSO, "dialogueText", dialogueView, "dialogueText");
        CopyField(mwmSO, "windowRoot", dialogueView, "windowRoot");
        // typingSpeed 等のパラメータもコピー
        CopyFloatField(mwmSO, "typingSpeed", dialogueView, "defaultTypingSpeed");

        // ── SpeakerNameView ──
        var speakerNameView = viewsRoot.GetComponentInChildren<SpeakerNameView>();
        CopyField(mwmSO, "speakerNameText", speakerNameView, "speakerNameText");
        CopyFloatField(mwmSO, "nameSlideDistance", speakerNameView, "slideDistance");
        CopyFloatField(mwmSO, "nameSlideDuration", speakerNameView, "slideDuration");

        // ── PortraitView ──
        var portraitView = viewsRoot.GetComponentInChildren<PortraitView>();
        CopyField(mwmSO, "portraitImage", portraitView, "portraitImage");
        CopyField(mwmSO, "ghostPortraitImage", portraitView, "ghostPortraitImage");
        CopyField(mwmSO, "portraitLeftAnchor", portraitView, "portraitLeftAnchor");
        CopyField(mwmSO, "portraitCenterAnchor", portraitView, "portraitCenterAnchor");
        CopyField(mwmSO, "portraitRightAnchor", portraitView, "portraitRightAnchor");
        CopyFloatField(mwmSO, "portraitJumpHeight", portraitView, "jumpHeight");
        CopyFloatField(mwmSO, "portraitJumpDuration", portraitView, "jumpDuration");

        // ── ChoiceView ──
        var choiceView = viewsRoot.GetComponentInChildren<ChoiceView>();
        CopyArrayField(mwmSO, "choiceButtons", choiceView, "choiceButtons");
        CopyArrayField(mwmSO, "choiceButtonTexts", choiceView, "choiceButtonTexts");

        // ── EffectView ──
        var effectView = viewsRoot.GetComponentInChildren<EffectView>();
        var existingEM = Object.FindObjectOfType<EffectManager>();
        if (existingEM != null)
        {
            var emSO = new SerializedObject(existingEM);
            CopyField(emSO, "flashOverlay", effectView, "flashOverlay");
            CopyField(emSO, "fadeOverlay", effectView, "fadeOverlay");
            CopyField(emSO, "centerImage", effectView, "centerImage");

            // AudioSource: 既存があれば既存を、なければ新規を使用
            var existingSE = emSO.FindProperty("seAudioSource").objectReferenceValue;
            var existingBGM = emSO.FindProperty("bgmAudioSource").objectReferenceValue;

            if (existingSE != null)
                SetField(effectView, "seAudioSource", existingSE);
            else if (audioRoot != null)
                SetField(effectView, "seAudioSource",
                    audioRoot.transform.Find("AudioSource_SE")?.GetComponent<AudioSource>());

            if (existingBGM != null)
                SetField(effectView, "bgmAudioSource", existingBGM);
            else if (audioRoot != null)
                SetField(effectView, "bgmAudioSource",
                    audioRoot.transform.Find("AudioSource_BGM")?.GetComponent<AudioSource>());
        }

        // ── BackgroundStillView ──
        var bgStillView = viewsRoot.GetComponentInChildren<BackgroundStillView>();
        CopyField(mwmSO, "backgroundStillImage", bgStillView, "backgroundStillImage");
        CopyArrayField(mwmSO, "objectsToHideOnStill", bgStillView, "objectsToHideOnStill");

        // ── KeywordView ──
        var existingKH = Object.FindObjectOfType<KeywordHandler>();
        if (existingKH != null)
        {
            var keywordView = viewsRoot.GetComponentInChildren<KeywordView>();
            SetField(keywordView, "keywordHandler", existingKH);
        }

        // ── DialogueLogView ──
        // フィールドなし、結線不要

        Debug.Log("[ScenarioSystemSetup] Auto-wire: 全結線完了");
    }

    // ═══════════════════════════════════════════
    //  Report
    // ═══════════════════════════════════════════

    private static string GenerateReport(GameObject systemRoot, GameObject viewsRoot)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("── 作成されたオブジェクト ──");
        sb.AppendLine($"  ScenarioSystem: {CountComponents(systemRoot)} コンポーネント");
        sb.AppendLine($"  Views: {viewsRoot.transform.childCount} View オブジェクト");

        sb.AppendLine("\n── ScenarioSystem コンポーネント ──");
        foreach (var c in systemRoot.GetComponents<Component>())
        {
            if (c is Transform) continue;
            sb.AppendLine($"  ✅ {c.GetType().Name}");
        }

        sb.AppendLine("\n── Views コンポーネント ──");
        foreach (Transform child in viewsRoot.transform)
        {
            var comp = child.GetComponent<Component>();
            var comps = child.GetComponents<Component>();
            foreach (var c in comps)
            {
                if (c is Transform) continue;
                sb.AppendLine($"  ✅ {c.GetType().Name} ({child.name})");
            }
        }

        sb.AppendLine("\n── 手動確認が必要 ──");
        sb.AppendLine("  ⚠️ ClickArea Button → OnClick → DialogueView.OnUserInput");
        sb.AppendLine("  ⚠️ 各 View の Inspector で結線先を目視確認");

        return sb.ToString();
    }

    // ═══════════════════════════════════════════
    //  Utility: Object Creation
    // ═══════════════════════════════════════════

    private static GameObject CreateAndRegister(string name)
    {
        var obj = new GameObject(name);
        Undo.RegisterCreatedObjectUndo(obj, $"Create {name}");
        return obj;
    }

    private static GameObject CreateChild(string name, GameObject parent)
    {
        var obj = new GameObject(name);
        obj.transform.SetParent(parent.transform, false);
        return obj;
    }

    private static GameObject CreateUIChild(string name, Transform parent)
    {
        var obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }

    // ═══════════════════════════════════════════
    //  Utility: Field Wiring
    // ═══════════════════════════════════════════

    private static void SetField(Object target, string fieldName, Object value)
    {
        if (target == null) return;
        var so = new SerializedObject(target);
        var prop = so.FindProperty(fieldName);
        if (prop != null && prop.propertyType == SerializedPropertyType.ObjectReference)
        {
            prop.objectReferenceValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
        else if (prop == null)
        {
            Debug.LogWarning($"[Wire] Field '{fieldName}' not found on {target.GetType().Name}");
        }
    }

    private static void SetField(Object target, string fieldName, bool value)
    {
        if (target == null) return;
        var so = new SerializedObject(target);
        var prop = so.FindProperty(fieldName);
        if (prop != null && prop.propertyType == SerializedPropertyType.Boolean)
        {
            prop.boolValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void CopyField(SerializedObject source, string srcField,
                                    Object target, string dstField)
    {
        if (target == null) return;
        var srcProp = source.FindProperty(srcField);
        if (srcProp == null || srcProp.objectReferenceValue == null) return;

        SetField(target, dstField, srcProp.objectReferenceValue);
    }

    private static void CopyFloatField(SerializedObject source, string srcField,
                                        Object target, string dstField)
    {
        if (target == null) return;
        var srcProp = source.FindProperty(srcField);
        if (srcProp == null) return;

        var so = new SerializedObject(target);
        var dstProp = so.FindProperty(dstField);
        if (dstProp != null && dstProp.propertyType == SerializedPropertyType.Float)
        {
            dstProp.floatValue = srcProp.floatValue;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    private static void CopyArrayField(SerializedObject source, string srcField,
                                        Object target, string dstField)
    {
        if (target == null) return;
        var srcArray = source.FindProperty(srcField);
        if (srcArray == null || !srcArray.isArray) return;

        var so = new SerializedObject(target);
        var dstArray = so.FindProperty(dstField);
        if (dstArray == null || !dstArray.isArray) return;

        dstArray.arraySize = srcArray.arraySize;
        for (int i = 0; i < srcArray.arraySize; i++)
        {
            dstArray.GetArrayElementAtIndex(i).objectReferenceValue =
                srcArray.GetArrayElementAtIndex(i).objectReferenceValue;
        }
        so.ApplyModifiedPropertiesWithoutUndo();
    }

    // ═══════════════════════════════════════════
    //  Utility: RectTransform
    // ═══════════════════════════════════════════

    private static void StretchFull(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static void SetAnchorStretchBottom(RectTransform rt, float height)
    {
        rt.anchorMin = new Vector2(0, 0);
        rt.anchorMax = new Vector2(1, 0);
        rt.pivot = new Vector2(0.5f, 0);
        rt.anchoredPosition = Vector2.zero;
        rt.sizeDelta = new Vector2(0, height);
    }

    // ═══════════════════════════════════════════
    //  Utility: Cleanup
    // ═══════════════════════════════════════════

    private static void DestroyImmediate(string name)
    {
        var obj = GameObject.Find(name);
        if (obj != null) Undo.DestroyObjectImmediate(obj);
    }

    private static int CountComponents(GameObject obj)
    {
        int count = 0;
        foreach (var c in obj.GetComponents<Component>())
        {
            if (c is Transform) continue;
            count++;
        }
        return count;
    }
}
#endif
