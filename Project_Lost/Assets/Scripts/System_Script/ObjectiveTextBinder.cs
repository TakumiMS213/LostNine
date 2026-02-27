using UnityEngine;
using TMPro;

/// <summary>
/// シーン上のTMP_Textに貼り付けるコンポーネント。
/// 自身をObjectiveDisplayに登録し、シーン破棄時に自動で解除する。
/// </summary>
[RequireComponent(typeof(TMP_Text))]
public class ObjectiveTextBinder : MonoBehaviour
{
    private TMP_Text _text;

    private void Awake()
    {
        _text = GetComponent<TMP_Text>();
    }

    private void OnEnable()
    {
        if (ObjectiveDisplay.Instance != null)
        {
            ObjectiveDisplay.Instance.RegisterText(_text);
        }
    }

    private void OnDisable()
    {
        if (ObjectiveDisplay.Instance != null)
        {
            ObjectiveDisplay.Instance.UnregisterText(_text);
        }
    }
}
