using UnityEditor;
using UnityEngine;
using Tuning.Data;

/// <summary>
/// TuningStageSettings 用カスタムInspector
/// セクションをカラー分けし、難易度プレビューを表示する
/// </summary>
[CustomEditor(typeof(TuningStageSettings))]
public class TuningStageSettingsEditor : Editor
{
    // ── Foldout 状態 ──────────────────────────────────
    private bool _foldTarget    = true;
    private bool _foldControl   = true;
    private bool _foldMoveLeft  = true;
    private bool _foldMoveRight = true;
    private bool _foldInertia   = true;
    private bool _foldPenalty   = true;
    private bool _foldNgZone    = true;
    private bool _foldStability = true;
    private bool _foldFeedback  = true;
    private bool _foldPreview   = true;

    // ── セクションカラー ──────────────────────────────
    private static readonly Color ColTarget    = new Color(0.30f, 0.65f, 1.00f, 0.18f);
    private static readonly Color ColControl   = new Color(0.45f, 1.00f, 0.60f, 0.18f);
    private static readonly Color ColMove      = new Color(0.20f, 0.85f, 0.75f, 0.18f);
    private static readonly Color ColInertia   = new Color(0.90f, 0.80f, 0.30f, 0.18f);
    private static readonly Color ColPenalty   = new Color(1.00f, 0.35f, 0.30f, 0.20f);
    private static readonly Color ColNgZone    = new Color(1.00f, 0.55f, 0.15f, 0.18f);
    private static readonly Color ColStability = new Color(0.55f, 0.35f, 1.00f, 0.18f);
    private static readonly Color ColFeedback  = new Color(0.90f, 0.45f, 1.00f, 0.18f);
    private static readonly Color ColPreview   = new Color(0.20f, 0.20f, 0.20f, 0.30f);

    // ── SerializedProperty キャッシュ ─────────────────
    // Target
    private SerializedProperty _targetTolerance;
    private SerializedProperty _isMovingTarget;
    private SerializedProperty _targetMoveSpeed;
    private SerializedProperty _targetMoveAmplitude;
    private SerializedProperty _leftTargetPosition;
    private SerializedProperty _rightTargetPosition;
    // Control
    private SerializedProperty _isInvertedLeft;
    private SerializedProperty _isInvertedRight;
    private SerializedProperty _interferenceStrength;
    // Move L
    private SerializedProperty _leftMoveForce;
    private SerializedProperty _leftMaxSpeed;
    // Move R
    private SerializedProperty _rightMoveForce;
    private SerializedProperty _rightMaxSpeed;
    // Inertia
    private SerializedProperty _baseInertia;
    // Penalty
    private SerializedProperty _ngZonePenaltyRate;
    private SerializedProperty _penaltyRecoverySpeed;
    private SerializedProperty _overheatThreshold;
    private SerializedProperty _overheatCooldownRate;
    private SerializedProperty _gameOverRestartDelay;
    // NGZone
    private SerializedProperty _ngZoneSize;
    private SerializedProperty _targetSafeMargin;
    // Stability
    private SerializedProperty _syncThresholdForStability;
    private SerializedProperty _stabilityGainRate;
    private SerializedProperty _stabilityDecayRate;
    // Feedback
    private SerializedProperty _inTargetFeedbackThreshold;

    private void OnEnable()
    {
        _targetTolerance             = serializedObject.FindProperty("targetTolerance");
        _isMovingTarget              = serializedObject.FindProperty("isMovingTarget");
        _targetMoveSpeed             = serializedObject.FindProperty("targetMoveSpeed");
        _targetMoveAmplitude         = serializedObject.FindProperty("targetMoveAmplitude");
        _leftTargetPosition          = serializedObject.FindProperty("leftTargetPosition");
        _rightTargetPosition         = serializedObject.FindProperty("rightTargetPosition");

        _isInvertedLeft              = serializedObject.FindProperty("isInvertedLeft");
        _isInvertedRight             = serializedObject.FindProperty("isInvertedRight");
        _interferenceStrength        = serializedObject.FindProperty("interferenceStrength");

        _leftMoveForce               = serializedObject.FindProperty("leftMoveForce");
        _leftMaxSpeed                = serializedObject.FindProperty("leftMaxSpeed");
        _rightMoveForce              = serializedObject.FindProperty("rightMoveForce");
        _rightMaxSpeed               = serializedObject.FindProperty("rightMaxSpeed");

        _baseInertia                 = serializedObject.FindProperty("baseInertia");

        _ngZonePenaltyRate           = serializedObject.FindProperty("ngZonePenaltyRate");
        _penaltyRecoverySpeed        = serializedObject.FindProperty("penaltyRecoverySpeed");
        _overheatThreshold           = serializedObject.FindProperty("overheatThreshold");
        _overheatCooldownRate        = serializedObject.FindProperty("overheatCooldownRate");
        _gameOverRestartDelay        = serializedObject.FindProperty("gameOverRestartDelay");

        _ngZoneSize                  = serializedObject.FindProperty("ngZoneSize");
        _targetSafeMargin            = serializedObject.FindProperty("targetSafeMargin");

        _syncThresholdForStability   = serializedObject.FindProperty("syncThresholdForStability");
        _stabilityGainRate           = serializedObject.FindProperty("stabilityGainRate");
        _stabilityDecayRate          = serializedObject.FindProperty("stabilityDecayRate");

        _inTargetFeedbackThreshold   = serializedObject.FindProperty("inTargetFeedbackThreshold");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        var s = (TuningStageSettings)target;
        EditorGUILayout.Space(4);

        DrawSection("🎯  ターゲット設定",    ColTarget,    ref _foldTarget,    DrawTarget);
        DrawSection("🕹️  操作設定",         ColControl,   ref _foldControl,   DrawControl);
        DrawSection("⬅️  移動設定（左側）",   ColMove,      ref _foldMoveLeft,  DrawMoveLeft);
        DrawSection("➡️  移動設定（右側）",   ColMove,      ref _foldMoveRight, DrawMoveRight);
        DrawSection("🏋️  慣性設定",          ColInertia,   ref _foldInertia,   DrawInertia);
        DrawSection("⚠️  ペナルティ設定",    ColPenalty,   ref _foldPenalty,   DrawPenalty);
        DrawSection("🚫  NGゾーン設定",      ColNgZone,    ref _foldNgZone,    DrawNgZone);
        DrawSection("📊  安定度設定",        ColStability, ref _foldStability, DrawStability);
        DrawSection("✨  フィードバック設定", ColFeedback,  ref _foldFeedback,  DrawFeedback);

        EditorGUILayout.Space(6);
        DrawDifficultyPreview(s);

        serializedObject.ApplyModifiedProperties();
    }

    // ─────────────────────────────────────────────────
    //  セクション描画ヘルパー
    // ─────────────────────────────────────────────────
    private delegate void DrawContents();

    private void DrawSection(string title, Color bgColor, ref bool foldout, DrawContents drawContents)
    {
        var bgStyle = new GUIStyle(GUI.skin.box)
        {
            padding = new RectOffset(10, 10, 6, 6),
            margin  = new RectOffset(0, 0, 2, 2)
        };

        // 背景を塗る
        var rect = EditorGUILayout.BeginVertical(bgStyle);
        EditorGUI.DrawRect(rect, bgColor);

        // フォールドアウトヘッダー
        var headerStyle = new GUIStyle(EditorStyles.foldout)
        {
            fontStyle = FontStyle.Bold,
            fontSize  = 12
        };
        foldout = EditorGUILayout.Foldout(foldout, title, true, headerStyle);

        if (foldout)
        {
            EditorGUILayout.Space(2);
            EditorGUI.indentLevel++;
            drawContents();
            EditorGUI.indentLevel--;
            EditorGUILayout.Space(2);
        }

        EditorGUILayout.EndVertical();
        EditorGUILayout.Space(2);
    }

    // ─────────────────────────────────────────────────
    //  各セクションの中身
    // ─────────────────────────────────────────────────
    private void DrawTarget()
    {
        EditorGUILayout.PropertyField(_targetTolerance);
        EditorGUILayout.PropertyField(_isMovingTarget);
        if (_isMovingTarget.boolValue)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(_targetMoveSpeed);
            EditorGUILayout.PropertyField(_targetMoveAmplitude);
            EditorGUILayout.PropertyField(_leftTargetPosition);
            EditorGUILayout.PropertyField(_rightTargetPosition);
            EditorGUI.indentLevel--;
        }
    }

    private void DrawControl()
    {
        // 反転フラグを横並びで表示
        EditorGUILayout.BeginHorizontal();
        GUILayout.Label("反転", GUILayout.Width(40));
        GUILayout.Label("Left", GUILayout.Width(35));
        _isInvertedLeft.boolValue  = EditorGUILayout.Toggle(_isInvertedLeft.boolValue,  GUILayout.Width(20));
        GUILayout.Label("Right", GUILayout.Width(40));
        _isInvertedRight.boolValue = EditorGUILayout.Toggle(_isInvertedRight.boolValue, GUILayout.Width(20));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.PropertyField(_interferenceStrength);
        DrawBar(_interferenceStrength.floatValue, 0f, 1f, Color.Lerp(Color.green, Color.red, _interferenceStrength.floatValue));
    }

    private void DrawMoveLeft()
    {
        EditorGUILayout.PropertyField(_leftMoveForce);
        DrawBar(_leftMoveForce.floatValue, 10f, 500f, new Color(0.2f, 0.8f, 0.6f));
        EditorGUILayout.PropertyField(_leftMaxSpeed);
        DrawBar(_leftMaxSpeed.floatValue, 50f, 1000f, new Color(0.2f, 0.6f, 1f));
    }

    private void DrawMoveRight()
    {
        EditorGUILayout.PropertyField(_rightMoveForce);
        DrawBar(_rightMoveForce.floatValue, 10f, 500f, new Color(0.2f, 0.8f, 0.6f));
        EditorGUILayout.PropertyField(_rightMaxSpeed);
        DrawBar(_rightMaxSpeed.floatValue, 50f, 1000f, new Color(0.2f, 0.6f, 1f));
    }

    private void DrawInertia()
    {
        EditorGUILayout.PropertyField(_baseInertia);
        string label = _baseInertia.floatValue < 5f  ? "　スライド寄り" :
                       _baseInertia.floatValue < 15f ? "　標準" : "　ピタ止まり";
        DrawBar(_baseInertia.floatValue, 0.1f, 30f, Color.Lerp(new Color(0.3f, 0.8f, 1f), new Color(1f, 0.6f, 0.1f), (_baseInertia.floatValue - 0.1f) / 29.9f), label);
    }

    private void DrawPenalty()
    {
        EditorGUILayout.PropertyField(_ngZonePenaltyRate);
        EditorGUILayout.PropertyField(_penaltyRecoverySpeed);
        EditorGUILayout.Space(4);
        EditorGUILayout.PropertyField(_overheatThreshold);
        DrawBar(_overheatThreshold.floatValue, 1f, 30f, Color.Lerp(Color.red, Color.green, (_overheatThreshold.floatValue - 1f) / 29f), "　制限時間");
        EditorGUILayout.PropertyField(_overheatCooldownRate);
        EditorGUILayout.PropertyField(_gameOverRestartDelay);
    }

    private void DrawNgZone()
    {
        EditorGUILayout.PropertyField(_ngZoneSize);
        EditorGUILayout.PropertyField(_targetSafeMargin);
    }

    private void DrawStability()
    {
        EditorGUILayout.PropertyField(_syncThresholdForStability);
        DrawBar(_syncThresholdForStability.floatValue, 0f, 1f, Color.Lerp(Color.green, Color.red, _syncThresholdForStability.floatValue), "　要求精度");
        EditorGUILayout.PropertyField(_stabilityGainRate);
        EditorGUILayout.PropertyField(_stabilityDecayRate);
    }

    private void DrawFeedback()
    {
        EditorGUILayout.PropertyField(_inTargetFeedbackThreshold);
        DrawBar(_inTargetFeedbackThreshold.floatValue, 0.5f, 1f, new Color(0.9f, 0.4f, 1f));
    }

    // ─────────────────────────────────────────────────
    //  バー描画ヘルパー
    // ─────────────────────────────────────────────────
    private void DrawBar(float value, float min, float max, Color color, string suffix = "")
    {
        float t = Mathf.Clamp01((value - min) / (max - min));
        var barRect = GUILayoutUtility.GetRect(0, 6, GUILayout.ExpandWidth(true));

        // 背景
        EditorGUI.DrawRect(barRect, new Color(0.15f, 0.15f, 0.15f, 0.5f));
        // 塗り
        var fill = new Rect(barRect.x, barRect.y, barRect.width * t, barRect.height);
        EditorGUI.DrawRect(fill, color);

        if (!string.IsNullOrEmpty(suffix))
        {
            var labelRect = new Rect(barRect.xMax + 4, barRect.y - 1, 120, 14);
            GUI.Label(labelRect, suffix, EditorStyles.miniLabel);
        }

        EditorGUILayout.Space(2);
    }

    // ─────────────────────────────────────────────────
    //  難易度プレビュー
    // ─────────────────────────────────────────────────
    private void DrawDifficultyPreview(TuningStageSettings s)
    {
        var rect = EditorGUILayout.BeginVertical(GUILayout.MinHeight(10));
        EditorGUI.DrawRect(rect, ColPreview);

        _foldPreview = EditorGUILayout.Foldout(_foldPreview, "🧩  難易度プレビュー（概算）", true,
            new GUIStyle(EditorStyles.foldout) { fontStyle = FontStyle.Bold, fontSize = 12 });

        if (_foldPreview)
        {
            EditorGUILayout.Space(4);

            // ① 精度要求：targetTolerance が小さいほど難しい
            float precision = 1f - Mathf.Clamp01((s.targetTolerance - 0.1f) / 4.9f);
            DrawPreviewBar("精度要求",    precision);

            // ② スピード感：moveForce / maxSpeed の平均で算出
            float avgForce = (s.leftMoveForce + s.rightMoveForce) * 0.5f;
            float speedScore = Mathf.Clamp01((avgForce - 10f) / 490f);
            DrawPreviewBar("スピード感",  speedScore);

            // ③ 操作難度：inertia(低=滑る=難) + interference
            float inertiaHard = 1f - Mathf.Clamp01((s.baseInertia - 0.1f) / 29.9f);
            float controlHard = (inertiaHard + s.interferenceStrength) * 0.5f;
            DrawPreviewBar("操作難度",    controlHard);

            // ④ 制限の厳しさ：overheatThreshold が短いほど厳しい
            float pressure = 1f - Mathf.Clamp01((s.overheatThreshold - 1f) / 29f);
            DrawPreviewBar("制限の厳しさ", pressure);

            // ⑤ クリアしやすさ：stabilityGainRate が高く、decayRate が低いほど楽
            float clearEase = Mathf.Clamp01(s.stabilityGainRate / 5f)
                            * (1f - Mathf.Clamp01(s.stabilityDecayRate / 2f));
            DrawPreviewBar("クリア速度",  clearEase);

            EditorGUILayout.Space(4);

            // 総合スコア（0-100）
            float overall = (precision + controlHard + pressure + (1f - clearEase)) / 4f * 100f;
            EditorGUILayout.Space(2);
            var overallStyle = new GUIStyle(EditorStyles.boldLabel) { alignment = TextAnchor.MiddleCenter, fontSize = 13 };
            string rank = overall < 25f ? "★☆☆☆  やさしい" :
                          overall < 50f ? "★★☆☆  ふつう"   :
                          overall < 75f ? "★★★☆  むずかしい" :
                                          "★★★★  超上級";
            EditorGUILayout.LabelField($"総合難易度:  {overall:F0} / 100　{rank}", overallStyle);
            EditorGUILayout.Space(4);
        }

        EditorGUILayout.EndVertical();
    }

    private void DrawPreviewBar(string label, float value)
    {
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField(label, GUILayout.Width(100));

        var barRect = GUILayoutUtility.GetRect(0, 14, GUILayout.ExpandWidth(true));
        EditorGUI.DrawRect(barRect, new Color(0.15f, 0.15f, 0.15f, 0.6f));

        Color barColor = Color.Lerp(new Color(0.2f, 0.8f, 0.4f), new Color(1f, 0.25f, 0.2f), value);
        var fill = new Rect(barRect.x, barRect.y, barRect.width * value, barRect.height);
        EditorGUI.DrawRect(fill, barColor);

        EditorGUILayout.LabelField($"{value * 100f:F0}%", GUILayout.Width(38));
        EditorGUILayout.EndHorizontal();
        EditorGUILayout.Space(1);
    }
}
