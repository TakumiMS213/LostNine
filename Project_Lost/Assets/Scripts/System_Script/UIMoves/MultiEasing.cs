using System.Collections.Generic;
using UnityEngine;

namespace Main.UIMoves
{
    /// <summary>
    /// 複数オブジェクトの Play() を一括起動するユーティリティ。
    /// インスペクターからリストにオブジェクトを設定し、
    /// Play() を呼ぶと、リスト内の全オブジェクトの Play() を実行する。
    /// オプションで一定時間後に再度 Play() を実行するリピート機能あり。
    /// </summary>
    public class MultiEasing : MonoBehaviour
    {
        [Header("識別ラベル（検索用）")]
        [Tooltip("FindByLabel() で検索するためのラベル。例: FadeOut, FadeIn")]
        [SerializeField] private string label = "";

        [Header("Play() を実行する対象オブジェクト")]
        [SerializeField] private List<GameObject> targets = new List<GameObject>();

        [Space]
        [Header("リピート設定")]
        [SerializeField] private bool enableRepeat = false;
        [SerializeField] private float repeatDelay = 1.0f;
        [SerializeField] private int repeatCount = 1;

        [Space]
        [Header("SE設定")]
        [Tooltip("SE再生用AudioSource（未設定なら鳴らさない）")]
        [SerializeField] private AudioSource seSource;

        [Tooltip("Play() 実行時に再生するSE")]
        [SerializeField] private AudioClip seClip;

        [Space]
        [Header("完了待ち設定")]
        [Tooltip("Play() 開始から完了までの推定時間（秒）。IsPlaying 判定に使用")]
        [SerializeField] private float totalDuration = 0.5f;

        private int _remainingRepeats;
        private float _playEndTime;

        /// <summary>
        /// アニメーションが再生中かどうか。totalDuration に基づく推定。
        /// </summary>
        public bool IsPlaying => Time.time < _playEndTime;

        /// <summary>
        /// ラベルでシーン内の MultiEasing を検索する。
        /// </summary>
        public static MultiEasing FindByLabel(string searchLabel)
        {
            MultiEasing[] existingInstances = FindObjectsByType<MultiEasing>(FindObjectsSortMode.None);
            foreach (var me in existingInstances)
            {
                if (me.label == searchLabel) return me;
            }
            return null;
        }

        /// <summary>
        /// リストに登録された全オブジェクトの Play() を呼び出す。
        /// enableRepeat が有効なら、repeatDelay 秒ごとに repeatCount 回まで再実行する。
        /// </summary>
        public void Play()
        {
            // 外部からの呼び出し時にカウンタをリセット
            CancelInvoke(nameof(RepeatPlay));
            _remainingRepeats = repeatCount;

            // 完了時刻を計算（リピートも含む）
            float repeatTotal = enableRepeat ? repeatDelay * repeatCount : 0f;
            _playEndTime = Time.time + totalDuration + repeatTotal;

            ExecutePlay();

            if (enableRepeat && _remainingRepeats > 0)
            {
                Invoke(nameof(RepeatPlay), repeatDelay);
            }
        }

        private void ExecutePlay()
        {
            // SE再生
            if (seSource != null && seClip != null)
            {
                seSource.PlayOneShot(seClip);
            }

            foreach (var target in targets)
            {
                if (target == null) continue;
                target.SendMessage("Play", SendMessageOptions.DontRequireReceiver);
            }
        }

        private void RepeatPlay()
        {
            if (_remainingRepeats <= 0) return;

            _remainingRepeats--;
            ExecutePlay();

            if (_remainingRepeats > 0)
            {
                Invoke(nameof(RepeatPlay), repeatDelay);
            }
        }
    }
}
