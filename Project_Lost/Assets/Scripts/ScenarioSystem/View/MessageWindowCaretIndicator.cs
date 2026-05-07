using UnityEngine;

namespace ScenarioSystem.View
{
    public class MessageWindowCaretIndicator : MonoBehaviour
    {
        [SerializeField] private float amplitude = 8f;
        [SerializeField] private float speed = 1.8f;

        private RectTransform _rectTransform;
        private Vector2 _baseAnchoredPosition;

        private void Awake()
        {
            _rectTransform = transform as RectTransform;
            if (_rectTransform != null)
                _baseAnchoredPosition = _rectTransform.anchoredPosition;
        }

        private void OnEnable()
        {
            if (_rectTransform == null)
                _rectTransform = transform as RectTransform;

            if (_rectTransform != null)
                _baseAnchoredPosition = _rectTransform.anchoredPosition;
        }

        private void Update()
        {
            if (_rectTransform == null) return;

            float offset = Mathf.Sin(Time.unscaledTime * speed * Mathf.PI * 2f) * amplitude;
            _rectTransform.anchoredPosition = _baseAnchoredPosition + new Vector2(0f, offset);
        }
    }
}
