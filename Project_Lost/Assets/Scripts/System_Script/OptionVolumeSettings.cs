using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

namespace SystemScript
{
    public sealed class OptionVolumeSettings : MonoBehaviour
    {
        private const string BgmVolumeKey = "Option.BGMVolume";
        private const string SeVolumeKey = "Option.SEVolume";

        [SerializeField] private AudioMixer audioMixer;
        [SerializeField] private RectTransform panelParent;
        [SerializeField] private TMP_FontAsset labelFont;
        [SerializeField] private string bgmParameter = "BGMVolume";
        [SerializeField] private string seParameter = "SEVolume";
        [SerializeField] private float defaultBgmVolume = 0.5f;
        [SerializeField] private float defaultSeVolume = 0.5f;

        private Slider _bgmSlider;
        private Slider _seSlider;

        private void Awake()
        {
            BuildControls();

            float bgmVolume = PlayerPrefs.GetFloat(BgmVolumeKey, defaultBgmVolume);
            float seVolume = PlayerPrefs.GetFloat(SeVolumeKey, defaultSeVolume);
            SetSliderValue(_bgmSlider, bgmVolume);
            SetSliderValue(_seSlider, seVolume);
            ApplyBgmVolume(bgmVolume);
            ApplySeVolume(seVolume);
        }

        private void BuildControls()
        {
            if (panelParent == null || _bgmSlider != null || _seSlider != null)
                return;

            _bgmSlider = CreateVolumeRow("BGM", new Vector2(0f, 50f), ApplyBgmVolume);
            _seSlider = CreateVolumeRow("SE", new Vector2(0f, -50f), ApplySeVolume);
        }

        private Slider CreateVolumeRow(string label, Vector2 position, UnityEngine.Events.UnityAction<float> onChanged)
        {
            var rowObject = new GameObject($"{label}Volume", typeof(RectTransform));
            rowObject.transform.SetParent(panelParent, false);

            var rowRect = rowObject.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0.5f, 0.5f);
            rowRect.anchorMax = new Vector2(0.5f, 0.5f);
            rowRect.pivot = new Vector2(0.5f, 0.5f);
            rowRect.anchoredPosition = position;
            rowRect.sizeDelta = new Vector2(760f, 72f);

            CreateLabel(label, rowRect);
            return CreateSlider(rowRect, onChanged);
        }

        private void CreateLabel(string text, RectTransform parent)
        {
            var labelObject = new GameObject($"{text}Label", typeof(RectTransform), typeof(TextMeshProUGUI));
            labelObject.transform.SetParent(parent, false);

            var rect = labelObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0f, 0.5f);
            rect.anchorMax = new Vector2(0f, 0.5f);
            rect.pivot = new Vector2(0f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(140f, 64f);

            var label = labelObject.GetComponent<TextMeshProUGUI>();
            label.text = text;
            label.fontSize = 44f;
            label.alignment = TextAlignmentOptions.MidlineLeft;
            label.raycastTarget = false;
            if (labelFont != null)
            {
                label.font = labelFont;
            }
        }

        private Slider CreateSlider(RectTransform parent, UnityEngine.Events.UnityAction<float> onChanged)
        {
            var sliderObject = new GameObject("Slider", typeof(RectTransform), typeof(Slider));
            sliderObject.transform.SetParent(parent, false);

            var sliderRect = sliderObject.GetComponent<RectTransform>();
            sliderRect.anchorMin = new Vector2(0f, 0.5f);
            sliderRect.anchorMax = new Vector2(1f, 0.5f);
            sliderRect.pivot = new Vector2(0.5f, 0.5f);
            sliderRect.offsetMin = new Vector2(170f, -18f);
            sliderRect.offsetMax = new Vector2(0f, 18f);

            var background = CreateSliderImage("Background", sliderRect, new Color(0.12f, 0.1f, 0.14f, 0.8f));
            var fillArea = CreateChildRect("Fill Area", sliderRect);
            fillArea.offsetMin = new Vector2(8f, 0f);
            fillArea.offsetMax = new Vector2(-8f, 0f);

            var fill = CreateSliderImage("Fill", fillArea, new Color(0.52f, 0.45f, 0.74f, 1f));
            var handleArea = CreateChildRect("Handle Slide Area", sliderRect);
            handleArea.offsetMin = new Vector2(8f, 0f);
            handleArea.offsetMax = new Vector2(-8f, 0f);

            var handle = CreateSliderImage("Handle", handleArea, Color.white);
            handle.sizeDelta = new Vector2(28f, 44f);

            var slider = sliderObject.GetComponent<Slider>();
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.wholeNumbers = false;
            slider.targetGraphic = handle.GetComponent<Image>();
            slider.fillRect = fill;
            slider.handleRect = handle;
            slider.direction = Slider.Direction.LeftToRight;
            slider.onValueChanged.AddListener(onChanged);
            fill.GetComponent<Image>().raycastTarget = false;
            return slider;
        }

        private static RectTransform CreateChildRect(string name, RectTransform parent)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            var rect = child.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            return rect;
        }

        private static RectTransform CreateSliderImage(string name, RectTransform parent, Color color)
        {
            var imageObject = new GameObject(name, typeof(RectTransform), typeof(Image));
            imageObject.transform.SetParent(parent, false);

            var rect = imageObject.GetComponent<RectTransform>();
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var image = imageObject.GetComponent<Image>();
            image.color = color;
            return rect;
        }

        private void SetSliderValue(Slider slider, float value)
        {
            if (slider != null)
            {
                slider.SetValueWithoutNotify(Mathf.Clamp01(value));
            }
        }

        private void ApplyBgmVolume(float value)
        {
            ApplyVolume(bgmParameter, value);
            PlayerPrefs.SetFloat(BgmVolumeKey, Mathf.Clamp01(value));
        }

        private void ApplySeVolume(float value)
        {
            ApplyVolume(seParameter, value);
            PlayerPrefs.SetFloat(SeVolumeKey, Mathf.Clamp01(value));
        }

        private void ApplyVolume(string parameter, float value)
        {
            if (audioMixer == null || string.IsNullOrEmpty(parameter))
                return;

            audioMixer.SetFloat(parameter, LinearToDecibel(value));
        }

        private static float LinearToDecibel(float value)
        {
            return value <= 0.0001f ? -80f : Mathf.Log10(Mathf.Clamp01(value)) * 20f;
        }
    }
}
