using UnityEngine;

namespace System_Script.UIMoves
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(RectTransform))]
    public sealed class ContinuousUiRotation : MonoBehaviour
    {
        [Tooltip("1秒あたりの回転角度。負の値で時計回りに回転します。") ]
        [SerializeField] private float degreesPerSecond = -12f;

        private RectTransform _rect;

        private void Awake()
        {
            _rect = transform as RectTransform;
        }

        private void Update()
        {
            if (_rect == null || degreesPerSecond == 0f) return;
            _rect.Rotate(0f, 0f, degreesPerSecond * Time.deltaTime, Space.Self);
        }
    }
}
