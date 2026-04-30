using UnityEngine;

namespace ScenarioSystem.Model.Actions
{
    [CreateAssetMenu(fileName = "TitleLogoAction", menuName = "Scenario/Actions/Title Logo")]
    public class TitleLogoAction : ScenarioAction
    {
        public override string ActionType => "TitleLogo";

        [Header("Logo")]
        [Tooltip("Center logo sprite to display.")]
        public Sprite logoSprite;

        [Tooltip("If true, the logo Image uses the sprite's native size before applying max size.")]
        public bool useNativeLogoSize = true;

        [Tooltip("Maximum logo size in UI pixels. Set either value to 0 or less to skip clamping on that axis.")]
        public Vector2 maxLogoSize = new(900f, 360f);

        [Header("Background")]
        [Tooltip("Background overlay color. Alpha is used as the final opacity.")]
        public Color backgroundColor = Color.black;

        [Header("Timing")]
        [Min(0f)] public float backgroundFadeInDuration = 1.0f;
        [Min(0f)] public float logoFadeInDuration = 1.2f;
        [Min(0f)] public float logoDisplayDuration = 1.5f;
        [Min(0f)] public float logoFadeOutDuration = 0.8f;
        [Min(0f)] public float backgroundHoldDuration = 0.4f;
        [Min(0f)] public float backgroundFadeOutDuration = 1.0f;

        public float TotalDuration =>
            Mathf.Max(0f, backgroundFadeInDuration)
            + Mathf.Max(0f, logoFadeInDuration)
            + Mathf.Max(0f, logoDisplayDuration)
            + Mathf.Max(0f, logoFadeOutDuration)
            + Mathf.Max(0f, backgroundHoldDuration)
            + Mathf.Max(0f, backgroundFadeOutDuration);
    }
}
