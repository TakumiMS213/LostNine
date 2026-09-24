using UnityEngine;

namespace Tuning.Data
{
    /// <summary>
    /// 調律ミニゲームのステージ個別設定
    /// </summary>
    [CreateAssetMenu(fileName = "TuningStageSettings", menuName = "Tuning/Stage Settings")]
    public class TuningStageSettings : ScriptableObject
    {
        // ─────────────────────────────────────────────
        //  ターゲット設定
        // ─────────────────────────────────────────────
        [Header("ターゲット設定")]

        [Tooltip("点がターゲットに入っているとみなす許容半径（大きいほどターゲットが広い）")]
        [Range(0.1f, 5f)]
        public float targetTolerance = 0.5f;

        [Tooltip("ターゲット領域が動くかどうか")]
        public bool isMovingTarget = false;

        [Tooltip("ターゲットが動く場合の速度倍率")]
        [Range(0.1f, 5f)]
        public float targetMoveSpeed = 1f;

        [Tooltip("動くターゲットの振れ幅（px）")]
        [Range(10f, 200f)]
        public float targetMoveAmplitude = 50f;

        [Tooltip("左ターゲットの初期位置（動くターゲット時は中心座標）")]
        public Vector2 leftTargetPosition = Vector2.zero;

        [Tooltip("右ターゲットの初期位置（動くターゲット時は中心座標）")]
        public Vector2 rightTargetPosition = Vector2.zero;

        // ─────────────────────────────────────────────
        //  操作設定
        // ─────────────────────────────────────────────
        [Header("操作設定")]

        [Tooltip("左側（WASD）の入力を反転するか")]
        public bool isInvertedLeft = false;

        [Tooltip("右側（IJKL）の入力を反転するか")]
        public bool isInvertedRight = false;

        [Tooltip("左右の干渉強度（値が高いほど相手の操作が自分に影響する）")]
        [Range(0f, 1f)]
        public float interferenceStrength = 0f;

        // ─────────────────────────────────────────────
        //  移動設定（左側）
        // ─────────────────────────────────────────────
        [Header("移動設定（左側）")]

        [Tooltip("左側の入力に対する移動力")]
        [Range(10f, 500f)]
        public float leftMoveForce = 100f;

        [Tooltip("左側の点の最大移動速度")]
        [Range(50f, 1000f)]
        public float leftMaxSpeed = 300f;

        // ─────────────────────────────────────────────
        //  移動設定（右側）
        // ─────────────────────────────────────────────
        [Header("移動設定（右側）")]

        [Tooltip("右側の入力に対する移動力")]
        [Range(10f, 500f)]
        public float rightMoveForce = 100f;

        [Tooltip("右側の点の最大移動速度")]
        [Range(50f, 1000f)]
        public float rightMaxSpeed = 300f;

        // ─────────────────────────────────────────────
        //  慣性設定
        // ─────────────────────────────────────────────
        [Header("慣性設定")]

        [Tooltip("通常時の摩擦係数（大きいほど止まりやすく、スライドしにくい）")]
        [Range(0.1f, 30f)]
        public float baseInertia = 5f;

        // ─────────────────────────────────────────────
        //  ペナルティ設定
        // ─────────────────────────────────────────────
        [Header("ペナルティ設定")]

        [Tooltip("NGゾーン滞在中の慣性増加速度（大きいほど急激に操作が重くなる）")]
        [Range(0f, 20f)]
        public float ngZonePenaltyRate = 2f;

        [Tooltip("NGゾーン外での慣性の回復速度（Lerpのt係数）")]
        [Range(0f, 10f)]
        public float penaltyRecoverySpeed = 1f;

        [Tooltip("ゲームオーバーになるまでのNGゾーン累積滞在秒数")]
        [Range(1f, 30f)]
        public float overheatThreshold = 5f;

        [Tooltip("NGゾーン外での過熱タイマーの自然回復速度（秒/秒）")]
        [Range(0f, 5f)]
        public float overheatCooldownRate = 0.5f;

        [Tooltip("ゲームオーバー後にリスタートするまでの待機秒数")]
        [Range(0f, 5f)]
        public float gameOverRestartDelay = 2f;

        // ─────────────────────────────────────────────
        //  NGゾーン設定
        // ─────────────────────────────────────────────
        [Header("NGゾーン設定")]

        [Tooltip("NGゾーン矩形のサイズ（幅, 高さ）（px）")]
        public Vector2 ngZoneSize = new Vector2(80f, 80f);

        [Tooltip("ターゲット・スタート地点・他NGゾーンとの最低距離（px）")]
        [Range(20f, 300f)]
        public float targetSafeMargin = 100f;

        // ─────────────────────────────────────────────
        //  安定度設定
        // ─────────────────────────────────────────────
        [Header("安定度設定")]

        [Tooltip("安定度ゲージが上昇するために必要な最低同期率")]
        [Range(0f, 1f)]
        public float syncThresholdForStability = 0.8f;

        [Tooltip("安定度ゲージの上昇倍率（大きいほど速くクリアできる）")]
        [Range(0.1f, 5f)]
        public float stabilityGainRate = 1f;

        [Tooltip("同期率が閾値を下回ったときの安定度減少速度（/秒）")]
        [Range(0f, 2f)]
        public float stabilityDecayRate = 0.2f;

        // ─────────────────────────────────────────────
        //  フィードバック設定
        // ─────────────────────────────────────────────
        [Header("フィードバック設定")]

        [Tooltip("「ターゲットに入った」とみなすSync閾値（エフェクト発火条件）")]
        [Range(0.5f, 1f)]
        public float inTargetFeedbackThreshold = 0.9f;
    }
}
