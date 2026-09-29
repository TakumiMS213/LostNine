using System;
using System.Collections.Generic;
using UnityEngine;
using Tuning.Data;

namespace Tuning.Core
{
    /// <summary>
    /// Main controller for the Tuning (調律) minigame.
    /// Manages point movement, sync calculation, penalties, and stability.
    /// </summary>
    public class TuningManager : MonoBehaviour
    {
        #region Serialized Fields

        [Header("ステージ設定")]
        [Tooltip("章ごとのステージ設定（インデックス = 章番号 - 1）")]
        [SerializeField] private TuningStageSettings[] stageSettingsList;

        [Header("ポイント")]
        [Tooltip("プレイヤーが操作する左側の点（WASD操作）")]
        [SerializeField] private RectTransform leftPoint;

        [Tooltip("プレイヤーが操作する右側の点（IJKL操作）")]
        [SerializeField] private RectTransform rightPoint;

        [Tooltip("左側の点が目指すべきターゲット円")]
        [SerializeField] private RectTransform leftTarget;

        [Tooltip("右側の点が目指すべきターゲット円")]
        [SerializeField] private RectTransform rightTarget;

        [Header("UIレイアウト")]
        [Tooltip("操作する点の基準サイズに対する倍率")]
        [SerializeField, Min(0.1f)] private float pointSizeMultiplier = 1.5f;

        [Header("範囲設定")]
        [Tooltip("左側の点が移動できる背景エリア（この上に制限）")]
        [SerializeField] private RectTransform leftBoundsArea;

        [Tooltip("右側の点が移動できる背景エリア（この上に制限）")]
        [SerializeField] private RectTransform rightBoundsArea;

        [Tooltip("左側のNGゾーン（この上にいるとペナルティ、ランダム配置）")]
        [SerializeField] private RectTransform leftNgZoneArea;
        [Tooltip("左側の2つ目のNGゾーン")]
        [SerializeField] private RectTransform leftNgZoneArea2;

        [Tooltip("右側のNGゾーン（この上にいるとペナルティ、ランダム配置）")]
        [SerializeField] private RectTransform rightNgZoneArea;
        [Tooltip("右側の2つ目のNGゾーン")]
        [SerializeField] private RectTransform rightNgZoneArea2;

        [Header("コンポーネント")]
        [Tooltip("入力処理を行うTuningInputコンポーネント")]
        [SerializeField] private TuningInput input;

        [Tooltip("演出処理を行うTuningFeedbackコンポーネント")]
        [SerializeField] private TuningFeedback feedback;

        #endregion

        #region Events

        public event Action OnTuningSuccess;
        public event Action OnTuningGameOver;

        #endregion

        #region Private State

        private TuningStageSettings _currentSettings;
        private Vector2 _leftVelocity;
        private Vector2 _rightVelocity;
        private float _currentInertia;
        private float _overheatTimer;
        private float _stabilityGauge;
        private float _totalSync;
        private float _leftSync;
        private float _rightSync;
        private float _leftBlockProximity;
        private float _rightBlockProximity;
        private bool _isActive;
        private int _activeBlockCount = 2;

        private bool _layoutCaptured;
        private Vector2 _leftTwoBlockPosition;
        private Vector2 _rightTwoBlockPosition;
        private Vector2 _leftPointBaseSize;
        private Vector2 _rightPointBaseSize;
        private bool _layoutApplied;

        private bool _leftInTarget;
        private bool _rightInTarget;
        private readonly Dictionary<RectTransform, CanvasGroup> _ngZoneCanvasGroups = new(4);

        #endregion

        #region Properties

        public float TotalSync => _totalSync;
        public float StabilityGauge => _stabilityGauge;
        public float OverheatProgress => _currentSettings != null ? _overheatTimer / _currentSettings.overheatThreshold : 0f;
        public bool IsActive => _isActive;

        #endregion

        #region Unity Lifecycle

        private void Start()
        {
            Initialize();
        }

        private void Update()
        {
            if (!_isActive || _currentSettings == null) return;

            UpdatePointMovement();
            UpdateTargetMovement();
            UpdateSyncRate();
            UpdatePenalty();
            UpdateStability();

            feedback?.OnSyncUpdate(
                _totalSync,
                _stabilityGauge,
                _leftInTarget,
                _rightInTarget,
                _leftBlockProximity,
                _rightBlockProximity);
        }

        #endregion

        #region Public API

        public void Initialize()
        {
            if (stageSettingsList == null || stageSettingsList.Length == 0)
            {
                Debug.LogError("[TuningManager] No stage settings assigned!");
                return;
            }

            // ProgressManagerから現在の章を取得
            int chapter = ProgressManager.Instance != null ? ProgressManager.Instance.CurrentChapter : 1;
            int index = Mathf.Clamp(chapter - 1, 0, stageSettingsList.Length - 1);

            ApplySettings(stageSettingsList[index], $"Chapter {chapter}");
        }

        private void ApplySettings(TuningStageSettings settings, string sourceName)
        {
            if (settings == null)
            {
                Debug.LogError($"[TuningManager] Settings are missing for {sourceName}.");
                return;
            }

            _currentSettings = settings;

            CaptureInitialLayout();
            _activeBlockCount = Mathf.Clamp(_currentSettings.activeBlockCount, 1, 2);
            ApplyBlockLayout();

            // 状態をリセット
            _currentInertia = _currentSettings.baseInertia;
            _overheatTimer = 0f;
            _stabilityGauge = 0f;
            _leftVelocity = Vector2.zero;
            _rightVelocity = Vector2.zero;
            _leftInTarget = false;
            _rightInTarget = false;
            _leftBlockProximity = 0f;
            _rightBlockProximity = _activeBlockCount > 1 ? 0f : 1f;
            _isActive = true;

            // フィードバックのリセット
            feedback?.ResetFeedback();

            // ターゲット位置をランダム配置
            RandomizeTargetPlacement(leftTarget, leftBoundsArea, _currentSettings.leftTargetPosition, _currentSettings.targetSafeMargin);
            if (_activeBlockCount > 1)
                RandomizeTargetPlacement(rightTarget, rightBoundsArea, _currentSettings.rightTargetPosition, _currentSettings.targetSafeMargin);

            // NGゾーンをランダム配置（ターゲットとスタート地点を避ける）
            Vector2 leftStartPos = leftPoint != null ? leftPoint.anchoredPosition : Vector2.zero;
            Vector2 rightStartPos = rightPoint != null ? rightPoint.anchoredPosition : Vector2.zero;

            // 左側NGゾーン配置
            // 1つ目: ターゲットとスタート地点を避ける
            PlaceNgZoneRandomly(leftNgZoneArea, leftBoundsArea, leftTarget.anchoredPosition, leftStartPos,
                               _currentSettings.ngZoneSize, _currentSettings.targetSafeMargin);
            // 2つ目: ターゲット、スタート地点、そして1つ目のNGゾーンを避ける
            PlaceNgZoneRandomly(leftNgZoneArea2, leftBoundsArea, leftTarget.anchoredPosition, leftStartPos,
                               _currentSettings.ngZoneSize, _currentSettings.targetSafeMargin, leftNgZoneArea);

            // 右側NGゾーン配置
            if (_activeBlockCount > 1)
            {
                PlaceNgZoneRandomly(rightNgZoneArea, rightBoundsArea, rightTarget.anchoredPosition, rightStartPos,
                                   _currentSettings.ngZoneSize, _currentSettings.targetSafeMargin);
                PlaceNgZoneRandomly(rightNgZoneArea2, rightBoundsArea, rightTarget.anchoredPosition, rightStartPos,
                                   _currentSettings.ngZoneSize, _currentSettings.targetSafeMargin, rightNgZoneArea);
            }

            // 入力設定を適用
            input?.Configure(_currentSettings.isInvertedLeft, _currentSettings.isInvertedRight, _currentSettings.interferenceStrength);

            // NGゾーンの表示をリセット
            _ngZoneCanvasGroups.Clear();
            ResetNGZoneAlpha(leftNgZoneArea);
            ResetNGZoneAlpha(leftNgZoneArea2);
            ResetNGZoneAlpha(rightNgZoneArea);
            ResetNGZoneAlpha(rightNgZoneArea2);

            Debug.Log($"[TuningManager] Initialized with {sourceName}: Blocks={_activeBlockCount}, Inertia={_currentSettings.useInertia}");
        }

        private void CaptureInitialLayout()
        {
            if (_layoutCaptured) return;

            if (leftBoundsArea != null)
                _leftTwoBlockPosition = GetEntranceDestination(leftBoundsArea);
            if (rightBoundsArea != null)
                _rightTwoBlockPosition = GetEntranceDestination(rightBoundsArea);
            if (leftPoint != null)
                _leftPointBaseSize = leftPoint.sizeDelta;
            if (rightPoint != null)
                _rightPointBaseSize = rightPoint.sizeDelta;

            _layoutCaptured = true;
        }

        private static Vector2 GetEntranceDestination(RectTransform block)
        {
            FirstMove entrance = block.GetComponent<FirstMove>();
            return entrance != null ? entrance.OriginalPos : block.anchoredPosition;
        }

        private void ApplyBlockLayout()
        {
            bool usesRightBlock = _activeBlockCount > 1;
            bool isInitialLayout = !_layoutApplied;
            Vector2 centerPosition = (_leftTwoBlockPosition + _rightTwoBlockPosition) * 0.5f;

            if (leftBoundsArea != null)
            {
                leftBoundsArea.gameObject.SetActive(true);
                Vector2 destination = usesRightBlock ? _leftTwoBlockPosition : centerPosition;
                FirstMove entrance = leftBoundsArea.GetComponent<FirstMove>();

                if (!usesRightBlock && entrance != null)
                    entrance.SetDestination(destination, isInitialLayout);
                else if (!isInitialLayout && entrance != null)
                    entrance.SetDestination(destination, false);
                else if (entrance == null)
                    leftBoundsArea.anchoredPosition = destination;
            }

            if (rightBoundsArea != null)
            {
                FirstMove entrance = rightBoundsArea.GetComponent<FirstMove>();
                if (!usesRightBlock || !isInitialLayout)
                {
                    if (entrance != null)
                        entrance.SetDestination(_rightTwoBlockPosition, false);
                    else
                        rightBoundsArea.anchoredPosition = _rightTwoBlockPosition;
                }
                rightBoundsArea.gameObject.SetActive(usesRightBlock);
            }

            if (leftPoint != null)
                leftPoint.sizeDelta = _leftPointBaseSize * pointSizeMultiplier;
            if (rightPoint != null)
                rightPoint.sizeDelta = _rightPointBaseSize * pointSizeMultiplier;

            feedback?.ConfigureBlockLayout(
                _activeBlockCount,
                usesRightBlock ? _leftTwoBlockPosition : centerPosition,
                _rightTwoBlockPosition);

            _layoutApplied = true;
        }

        private void ResetNGZoneAlpha(RectTransform zone)
        {
            if (zone == null) return;
            GetNGZoneCanvasGroup(zone).alpha = 0f;
        }

        private CanvasGroup GetNGZoneCanvasGroup(RectTransform zone)
        {
            if (_ngZoneCanvasGroups.TryGetValue(zone, out var canvasGroup) && canvasGroup != null)
                return canvasGroup;

            canvasGroup = zone.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
                canvasGroup = zone.gameObject.AddComponent<CanvasGroup>();

            _ngZoneCanvasGroups[zone] = canvasGroup;
            return canvasGroup;
        }

        public void SetSettings(TuningStageSettings newSettings)
        {
            ApplySettings(newSettings, newSettings != null ? newSettings.name : "runtime settings");
        }

        public void SetActive(bool active) => _isActive = active;

        #endregion

        #region Movement

        private void UpdatePointMovement()
        {
            if (input == null) 
            {
                Debug.LogWarning("[TuningManager] Input is null!");
                return;
            }

            // Get combined input (direct + interference)
            Vector2 leftForceVec = (input.LeftInput + input.LeftInterference) * _currentSettings.leftMoveForce;
            Vector2 rightForceVec = (input.RightInput + input.RightInterference) * _currentSettings.rightMoveForce;

            if (_currentSettings.useInertia)
            {
                // 加速と摩擦によって、入力を離した後も速度が残る。
                _leftVelocity += leftForceVec * Time.deltaTime;
                _rightVelocity += rightForceVec * Time.deltaTime;
                _leftVelocity = Vector2.Lerp(_leftVelocity, Vector2.zero, _currentInertia * Time.deltaTime);
                _rightVelocity = Vector2.Lerp(_rightVelocity, Vector2.zero, _currentInertia * Time.deltaTime);
            }
            else
            {
                // 低難易度では入力をそのまま速度へ変換し、滑りを残さない。
                _leftVelocity = leftForceVec;
                _rightVelocity = rightForceVec;
            }

            // Clamp speed
            _leftVelocity = Vector2.ClampMagnitude(_leftVelocity, _currentSettings.leftMaxSpeed);
            _rightVelocity = Vector2.ClampMagnitude(_rightVelocity, _currentSettings.rightMaxSpeed);

            // Apply movement
            if (leftPoint != null && leftBoundsArea != null)
            {
                Vector2 newPos = leftPoint.anchoredPosition + _leftVelocity * Time.deltaTime;
                leftPoint.anchoredPosition = ClampToRectTransform(newPos, leftBoundsArea);
            }

            if (_activeBlockCount > 1 && rightPoint != null && rightBoundsArea != null)
            {
                Vector2 newPos = rightPoint.anchoredPosition + _rightVelocity * Time.deltaTime;
                rightPoint.anchoredPosition = ClampToRectTransform(newPos, rightBoundsArea);
            }
        }

        private void UpdateTargetMovement()
        {
            if (!_currentSettings.isMovingTarget) return;

            float time = Time.time * _currentSettings.targetMoveSpeed;

            if (leftTarget != null)
            {
                float amp = _currentSettings.targetMoveAmplitude;
                float x = Mathf.Sin(time) * amp + _currentSettings.leftTargetPosition.x;
                float y = Mathf.Cos(time * 0.7f) * amp + _currentSettings.leftTargetPosition.y;
                leftTarget.anchoredPosition = new Vector2(x, y);
            }

            if (_activeBlockCount > 1 && rightTarget != null)
            {
                float amp = _currentSettings.targetMoveAmplitude;
                float x = Mathf.Cos(time * 0.8f) * amp + _currentSettings.rightTargetPosition.x;
                float y = Mathf.Sin(time * 1.1f) * amp + _currentSettings.rightTargetPosition.y;
                rightTarget.anchoredPosition = new Vector2(x, y);
            }
        }

        private Vector2 ClampToRectTransform(Vector2 pos, RectTransform boundsRect)
        {
            if (boundsRect == null) return pos;

            // 親の rect を使用（点は親の子オブジェクトとして配置される想定）
            Rect bounds = boundsRect.rect;

            // pivot を考慮したローカル座標での範囲
            float minX = bounds.xMin;
            float maxX = bounds.xMax;
            float minY = bounds.yMin;
            float maxY = bounds.yMax;

            return new Vector2(
                Mathf.Clamp(pos.x, minX, maxX),
                Mathf.Clamp(pos.y, minY, maxY)
            );
        }

        #endregion

        #region Sync Calculation

        private void UpdateSyncRate()
        {
            _leftSync = CalculatePointSync(leftPoint, leftTarget);
            _rightSync = _activeBlockCount > 1 ? CalculatePointSync(rightPoint, rightTarget) : 1f;

            _leftBlockProximity = CalculateBlockProximity(leftPoint, leftTarget, leftBoundsArea);
            _rightBlockProximity = _activeBlockCount > 1
                ? CalculateBlockProximity(rightPoint, rightTarget, rightBoundsArea)
                : 1f;

            // 2ブロック時は両方、1ブロック時は左だけをクリア判定に使用する。
            _totalSync = _leftSync * _rightSync;

            // Check target entry for feedback
            bool leftNowInTarget = _leftSync > _currentSettings.inTargetFeedbackThreshold;
            bool rightNowInTarget = _activeBlockCount > 1
                && _rightSync > _currentSettings.inTargetFeedbackThreshold;

            if (leftNowInTarget && !_leftInTarget)
                feedback?.OnPointInTarget(0);
            if (rightNowInTarget && !_rightInTarget)
                feedback?.OnPointInTarget(1);

            _leftInTarget = leftNowInTarget;
            _rightInTarget = rightNowInTarget;
        }

        private float CalculatePointSync(RectTransform point, RectTransform target)
        {
            if (point == null || target == null) return 0f;

            float distance = Vector2.Distance(point.anchoredPosition, target.anchoredPosition);
            float sync = 1f - Mathf.Clamp01(distance / (_currentSettings.targetTolerance * 100f));
            return sync;
        }

        private static float CalculateBlockProximity(RectTransform point, RectTransform target, RectTransform boundsArea)
        {
            if (point == null || target == null || boundsArea == null) return 0f;

            Vector2 targetPosition = target.anchoredPosition;
            Rect bounds = boundsArea.rect;
            float maximumDistance = Mathf.Max(
                Vector2.Distance(targetPosition, new Vector2(bounds.xMin, bounds.yMin)),
                Vector2.Distance(targetPosition, new Vector2(bounds.xMin, bounds.yMax)),
                Vector2.Distance(targetPosition, new Vector2(bounds.xMax, bounds.yMin)),
                Vector2.Distance(targetPosition, new Vector2(bounds.xMax, bounds.yMax)));

            if (maximumDistance <= Mathf.Epsilon) return 1f;

            float distance = Vector2.Distance(point.anchoredPosition, targetPosition);
            return 1f - Mathf.Clamp01(distance / maximumDistance);
        }

        #endregion

        #region Penalty System

        private void UpdatePenalty()
        {
            // NGゾーンの中にいる場合にペナルティ
            bool inLeft1 = CheckAndVisualizeNGZone(leftNgZoneArea, leftPoint);
            bool inLeft2 = CheckAndVisualizeNGZone(leftNgZoneArea2, leftPoint);
            bool inRight1 = _activeBlockCount > 1 && CheckAndVisualizeNGZone(rightNgZoneArea, rightPoint);
            bool inRight2 = _activeBlockCount > 1 && CheckAndVisualizeNGZone(rightNgZoneArea2, rightPoint);

            bool isInNGZone = inLeft1 || inLeft2 || inRight1 || inRight2;

            if (isInNGZone)
            {
                if (_currentSettings.useInertia)
                    _currentInertia += _currentSettings.ngZonePenaltyRate * Time.deltaTime;
                _overheatTimer += Time.deltaTime;
            }
            else
            {
                if (_currentSettings.useInertia)
                    _currentInertia = Mathf.Lerp(_currentInertia, _currentSettings.baseInertia, _currentSettings.penaltyRecoverySpeed * Time.deltaTime);
                _overheatTimer = Mathf.Max(0f, _overheatTimer - Time.deltaTime * _currentSettings.overheatCooldownRate);
            }

            // タイマーUI更新
            feedback?.OnPenaltyUpdate(_overheatTimer, _currentSettings.overheatThreshold, isInNGZone, _stabilityGauge);

            if (_overheatTimer >= _currentSettings.overheatThreshold)
            {
                TriggerGameOver();
            }
        }

        private bool CheckAndVisualizeNGZone(RectTransform areaRect, RectTransform point)
        {
            if (areaRect == null || point == null) return false;

            bool isInside = IsPointInsideArea(point, areaRect);

            // アルファ値の制御 (CanvasGroupを使用)
            CanvasGroup cg = GetNGZoneCanvasGroup(areaRect);

            float targetAlpha = isInside ? 1f : 0f;
            cg.alpha = Mathf.Lerp(cg.alpha, targetAlpha, Time.deltaTime * 10f);

            return isInside;
        }

        private bool IsPointInsideArea(RectTransform point, RectTransform areaRect)
        {
            if (point == null || areaRect == null) return false;

            // NGゾーンのローカル座標でチェック
            Rect bounds = areaRect.rect;
            Vector2 ngZonePos = areaRect.anchoredPosition;
            Vector2 pointPos = point.anchoredPosition;

            // 点の位置がNGゾーンの範囲内かどうか
            float halfW = bounds.width * 0.5f;
            float halfH = bounds.height * 0.5f;
            
            return pointPos.x >= ngZonePos.x - halfW && pointPos.x <= ngZonePos.x + halfW &&
                   pointPos.y >= ngZonePos.y - halfH && pointPos.y <= ngZonePos.y + halfH;
        }

        private void PlaceNgZoneRandomly(RectTransform ngZone, RectTransform boundsArea, Vector2 targetPos, Vector2 startPointPos, Vector2 ngSize, float safeMargin, params RectTransform[] avoidRects)
        {
            if (ngZone == null || boundsArea == null) return;

            // NGゾーンのサイズを設定
            ngZone.sizeDelta = ngSize;

            Rect bounds = boundsArea.rect;
            int maxAttempts = 50;

            for (int i = 0; i < maxAttempts; i++)
            {
                // ランダムな位置を生成（NGゾーンがはみ出ないように）
                float halfW = ngSize.x * 0.5f;
                float halfH = ngSize.y * 0.5f;
                float x = UnityEngine.Random.Range(bounds.xMin + halfW, bounds.xMax - halfW);
                float y = UnityEngine.Random.Range(bounds.yMin + halfH, bounds.yMax - halfH);
                Vector2 candidatePos = new Vector2(x, y);

                // ターゲットおよびスタート地点との距離をチェック
                float distToTarget = Vector2.Distance(candidatePos, targetPos);
                float distToPoint = Vector2.Distance(candidatePos, startPointPos);

                bool isTooClose = distToTarget < safeMargin || distToPoint < safeMargin;

                // 他のNGゾーンとの重なりチェック
                if (!isTooClose && avoidRects != null)
                {
                    foreach (var avoidRect in avoidRects)
                    {
                        if (avoidRect == null) continue;
                        float distToOther = Vector2.Distance(candidatePos, avoidRect.anchoredPosition);
                        // 簡易的な距離チェック（必要に応じて矩形判定にするが、円形距離で十分な場合が多い）
                        if (distToOther < safeMargin)
                        {
                            isTooClose = true;
                            break;
                        }
                    }
                }

                if (!isTooClose)
                {
                    ngZone.anchoredPosition = candidatePos;
                    return;
                }
            }

            // 見つからなかった場合はターゲットとスタート地点から離れた位置に配置（簡易的にターゲットから離す）
            Vector2 awayDir = (Vector2.zero - targetPos).normalized;
            if (awayDir == Vector2.zero) awayDir = Vector2.right;
            ngZone.anchoredPosition = targetPos + awayDir * safeMargin;
        }

        private void RandomizeTargetPlacement(RectTransform target, RectTransform boundsArea, Vector2 defaultPos, float margin)
        {
            if (target == null || boundsArea == null) return;

            // デフォルト位置を基準にしたランダムオフセット、あるいは完全にランダム
            // ここではboundsArea内でランダムに配置する（ただし中央付近を避ける等のロジックが必要なら追加）
            
            Rect bounds = boundsArea.rect;
            // ターゲットがはみ出ないように少しマージンを取る
            float padding = 50f; 

            float x = UnityEngine.Random.Range(bounds.xMin + padding, bounds.xMax - padding);
            float y = UnityEngine.Random.Range(bounds.yMin + padding, bounds.yMax - padding);
            
            target.anchoredPosition = new Vector2(x, y);
        }

        #endregion

        #region Stability

        private void UpdateStability()
        {
            if (_totalSync >= _currentSettings.syncThresholdForStability)
            {
                _stabilityGauge += _totalSync * _currentSettings.stabilityGainRate * Time.deltaTime;
            }
            else
            {
                _stabilityGauge -= _currentSettings.stabilityDecayRate * Time.deltaTime;
            }

            _stabilityGauge = Mathf.Clamp01(_stabilityGauge);

            if (_stabilityGauge >= 1f)
            {
                TriggerSuccess();
            }
        }

        #endregion

        #region Game End

        private void TriggerSuccess()
        {
            _isActive = false;
            feedback?.OnSuccess();
            OnTuningSuccess?.Invoke();
            Debug.Log("[TuningManager] Tuning Success!");
        }

        private void TriggerGameOver()
        {
            _isActive = false;
            feedback?.OnGameOver();
            OnTuningGameOver?.Invoke();
            Debug.Log("[TuningManager] Game Over - Overheat!");
            StartCoroutine(RestartSequence());
        }

        private System.Collections.IEnumerator RestartSequence()
        {
            // ゲームオーバー後待機（SOで設定可能）
            yield return new WaitForSeconds(_currentSettings.gameOverRestartDelay);

            // 黒フェードアウト
            bool fadeDone = false;
            if (feedback != null)
            {
                feedback.FadeOut(0.5f, () => fadeDone = true);
                yield return new WaitUntil(() => fadeDone);
            }
            else
            {
                yield return new WaitForSeconds(0.5f);
            }

            // 再初期化（リスタート）
            Initialize();

            // 黒フェードイン
            feedback?.FadeIn(0.5f);
        }

        #endregion
    }
}
