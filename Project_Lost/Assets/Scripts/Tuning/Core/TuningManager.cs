using System;
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
        private bool _isActive;

        private bool _leftInTarget;
        private bool _rightInTarget;

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

            feedback?.OnSyncUpdate(_leftSync, _rightSync, _totalSync, _stabilityGauge, _leftInTarget, _rightInTarget);
        }

        #endregion

        #region Public API

        public void Initialize()
        {
            // ProgressManagerから現在の章を取得
            int chapter = ProgressManager.Instance != null ? ProgressManager.Instance.CurrentChapter : 1;
            int index = Mathf.Clamp(chapter - 1, 0, stageSettingsList.Length - 1);
            
            if (stageSettingsList == null || stageSettingsList.Length == 0)
            {
                Debug.LogError("[TuningManager] No stage settings assigned!");
                return;
            }

            _currentSettings = stageSettingsList[index];
            if (_currentSettings == null) return;

            // 状態をリセット
            _currentInertia = _currentSettings.baseInertia;
            _overheatTimer = 0f;
            _stabilityGauge = 0f;
            _leftVelocity = Vector2.zero;
            _rightVelocity = Vector2.zero;
            _isActive = true;

            // フィードバックのリセット
            feedback?.ResetFeedback();

            // ターゲット位置をランダム配置
            RandomizeTargetPlacement(leftTarget, leftBoundsArea, _currentSettings.leftTargetPosition, _currentSettings.targetSafeMargin);
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
            PlaceNgZoneRandomly(rightNgZoneArea, rightBoundsArea, rightTarget.anchoredPosition, rightStartPos,
                               _currentSettings.ngZoneSize, _currentSettings.targetSafeMargin);
            PlaceNgZoneRandomly(rightNgZoneArea2, rightBoundsArea, rightTarget.anchoredPosition, rightStartPos,
                               _currentSettings.ngZoneSize, _currentSettings.targetSafeMargin, rightNgZoneArea);

            // 入力設定を適用
            input?.Configure(_currentSettings.isInvertedLeft, _currentSettings.isInvertedRight, _currentSettings.interferenceStrength);

            // NGゾーンの表示をリセット
            ResetNGZoneAlpha(leftNgZoneArea);
            ResetNGZoneAlpha(leftNgZoneArea2);
            ResetNGZoneAlpha(rightNgZoneArea);
            ResetNGZoneAlpha(rightNgZoneArea2);

            Debug.Log($"[TuningManager] Initialized for Chapter {chapter}");
        }

        private void ResetNGZoneAlpha(RectTransform zone)
        {
            if (zone == null) return;
            CanvasGroup cg = zone.GetComponent<CanvasGroup>();
            if (cg == null) cg = zone.gameObject.AddComponent<CanvasGroup>();
            cg.alpha = 0f;
        }

        public void SetSettings(TuningStageSettings newSettings)
        {
            _currentSettings = newSettings;
            Initialize();
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

            // Apply force with inertia (friction)
            _leftVelocity += leftForceVec * Time.deltaTime;
            _rightVelocity += rightForceVec * Time.deltaTime;

            // Apply friction
            _leftVelocity = Vector2.Lerp(_leftVelocity, Vector2.zero, _currentInertia * Time.deltaTime);
            _rightVelocity = Vector2.Lerp(_rightVelocity, Vector2.zero, _currentInertia * Time.deltaTime);

            // Clamp speed
            _leftVelocity = Vector2.ClampMagnitude(_leftVelocity, _currentSettings.leftMaxSpeed);
            _rightVelocity = Vector2.ClampMagnitude(_rightVelocity, _currentSettings.rightMaxSpeed);

            // Apply movement
            if (leftPoint != null && leftBoundsArea != null)
            {
                Vector2 newPos = leftPoint.anchoredPosition + _leftVelocity * Time.deltaTime;
                leftPoint.anchoredPosition = ClampToRectTransform(newPos, leftBoundsArea);
            }

            if (rightPoint != null && rightBoundsArea != null)
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
                float x = Mathf.Sin(time) * 50f + _currentSettings.leftTargetPosition.x;
                float y = Mathf.Cos(time * 0.7f) * 50f + _currentSettings.leftTargetPosition.y;
                leftTarget.anchoredPosition = new Vector2(x, y);
            }

            if (rightTarget != null)
            {
                float x = Mathf.Cos(time * 0.8f) * 50f + _currentSettings.rightTargetPosition.x;
                float y = Mathf.Sin(time * 1.1f) * 50f + _currentSettings.rightTargetPosition.y;
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
            _rightSync = CalculatePointSync(rightPoint, rightTarget);

            // Multiplicative: both must be good for high sync
            _totalSync = _leftSync * _rightSync;

            // Check target entry for feedback
            bool leftNowInTarget = _leftSync > 0.9f;
            bool rightNowInTarget = _rightSync > 0.9f;

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

        #endregion

        #region Penalty System

        private void UpdatePenalty()
        {
            // NGゾーンの中にいる場合にペナルティ
            bool inLeft1 = CheckAndVisualizeNGZone(leftNgZoneArea, leftPoint);
            bool inLeft2 = CheckAndVisualizeNGZone(leftNgZoneArea2, leftPoint);
            bool inRight1 = CheckAndVisualizeNGZone(rightNgZoneArea, rightPoint);
            bool inRight2 = CheckAndVisualizeNGZone(rightNgZoneArea2, rightPoint);

            bool isInNGZone = inLeft1 || inLeft2 || inRight1 || inRight2;

            if (isInNGZone)
            {
                _currentInertia += _currentSettings.ngZonePenaltyRate * Time.deltaTime;
                _overheatTimer += Time.deltaTime;
            }
            else
            {
                _currentInertia = Mathf.Lerp(_currentInertia, _currentSettings.baseInertia, _currentSettings.penaltyRecoverySpeed * Time.deltaTime);
                _overheatTimer = Mathf.Max(0f, _overheatTimer - Time.deltaTime * 0.5f);
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
            CanvasGroup cg = areaRect.GetComponent<CanvasGroup>();
            if (cg == null) cg = areaRect.gameObject.AddComponent<CanvasGroup>();

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
                _stabilityGauge += _totalSync * Time.deltaTime;
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
            // 2秒待機
            yield return new WaitForSeconds(2f);

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
