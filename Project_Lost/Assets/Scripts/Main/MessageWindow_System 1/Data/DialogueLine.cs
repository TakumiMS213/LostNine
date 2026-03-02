using System.Collections.Generic;
using UnityEngine;

namespace MessageWindowSystem.Data
{
    [System.Serializable]
    public class DialogueLine
    {
        [Tooltip("Name of the speaker")]
        public string speakerName;
        
        [Tooltip("Portrait to display")]
        public Sprite portrait;
        
        [TextArea(3, 10)]
        [Tooltip("Dialogue text. Supports TMP tags.")]
        public string text;
        
        [Tooltip("List of effects to trigger when this line is displayed.")]
        public List<ScreenEffectData> effects;
        
        [Tooltip("Typing speed for this line (seconds per character). 0 = use default from Manager")]
        public float typingSpeed = 0f;

        [Tooltip("Direction the speaker name should slide in from for this line. Default = use Manager setting.")]
        public NameSlideDirection nameSlideDirection = NameSlideDirection.Default;

        [Tooltip("Audio clip to play when this line is displayed (Voice)")]
        public AudioClip voiceClip;

        [Tooltip("Choices to present after this line (if any).")]
        public List<ChoiceData> choices;

        [Tooltip("Portrait display position (Left, Center, Right). Default = Center.")]
        public PortraitPosition portraitPosition;
    }

    /// <summary>
    /// Portrait display position on the message window.
    /// </summary>
    public enum PortraitPosition
    {
        Center = 0,
        Left,
        Right
    }

    [System.Serializable]
    public struct ScreenEffectData
    {
        public EffectType effectType;
        
        [Tooltip("Duration or Intensity (depending on effect)")]
        public float floatParam;
        
        [Tooltip("Name of SE/BGM or other string parameter")]
        public string stringParam;
        
        [Tooltip("Sprite to display (for ShowImage)")]
        public Sprite spriteParam;
        
        [Tooltip("Color for Flash/Fade")]
        public Color colorParam;
    }

    public enum NameSlideDirection
    {
        Default,
        Left,
        Right
    }
}
