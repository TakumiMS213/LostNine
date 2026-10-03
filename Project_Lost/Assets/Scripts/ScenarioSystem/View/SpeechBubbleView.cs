using System;
using ScenarioSystem.Events;
using UnityEngine;
using UnityEngine.UI;

namespace ScenarioSystem.View
{
    /// <summary>非会話時の吹き出しを話者へ向ける。子の本文・クリック領域は反転しない。</summary>
    [RequireComponent(typeof(Image))]
    public sealed class SpeechBubbleView : BaseMeshEffect
    {
        [SerializeField] private string playerSpeakerName = "ヒイラギ";

        private bool _isPlayerSpeaking;
        private bool _isInCommunication;

        protected override void OnEnable()
        {
            base.OnEnable();
            ScenarioEventBus.OnDialogueRequested += HandleDialogue;
        }

        protected override void OnDisable()
        {
            ScenarioEventBus.OnDialogueRequested -= HandleDialogue;
            base.OnDisable();
        }

        public void SetConversationMode(bool isInCommunication)
        {
            _isInCommunication = isInCommunication;
            graphic.SetVerticesDirty();
        }

        private void HandleDialogue(DialogueEventData data)
        {
            _isPlayerSpeaking = string.Equals(data.SpeakerName?.Trim(), playerSpeakerName,
                StringComparison.Ordinal);
            graphic.SetVerticesDirty();
        }

        public override void ModifyMesh(VertexHelper vertices)
        {
            // 元画像の突起は下向き。カウンター奥の話者のときだけ画像を上下反転。
            if (!IsActive() || _isInCommunication || _isPlayerSpeaking) return;

            float centerY = graphic.rectTransform.rect.center.y;
            var vertex = new UIVertex();
            for (int i = 0; i < vertices.currentVertCount; i++)
            {
                vertices.PopulateUIVertex(ref vertex, i);
                vertex.position.y = 2f * centerY - vertex.position.y;
                vertices.SetUIVertex(vertex, i);
            }
        }
    }
}
