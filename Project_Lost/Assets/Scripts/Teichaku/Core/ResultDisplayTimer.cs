using UnityEngine;

namespace Teichaku.Core
{
    /// <summary>クリア操作の押しっぱなしを除外し、次のクリックか制限時間で結果表示を終える。</summary>
    public sealed class ResultDisplayTimer
    {
        private readonly float _duration;
        private float _elapsed;
        private bool _canSkip;

        public bool IsComplete { get; private set; }

        public ResultDisplayTimer(float duration, bool pointerHeld)
        {
            _duration = Mathf.Max(0f, duration);
            _canSkip = !pointerHeld;
        }

        public void Advance(float deltaTime, bool pointerHeld, bool pointerDown)
        {
            if (IsComplete) return;
            _elapsed += Mathf.Max(0f, deltaTime);
            if (_canSkip && pointerDown) IsComplete = true;
            if (!pointerHeld) _canSkip = true;
            if (_elapsed >= _duration) IsComplete = true;
        }
    }
}
