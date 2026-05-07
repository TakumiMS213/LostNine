using UnityEngine;
using UnityEngine.InputSystem;

public class UISwitcher : MonoBehaviour
{
    [System.Serializable]
    public class PanelData
    {
        public GameObject panel;
        [Tooltip("If true, this panel will close when Escape is pressed.")]
        public bool hideOnEsc = true;
    }

    [SerializeField] private PanelData[] _panels;

    private void Update()
    {
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame)
        {
            HideEscPanels();
        }
    }

    // 表示ボタンに割り当てる
    public void ShowPanel(int index)
    {
        if (_panels != null && index >= 0 && index < _panels.Length)
        {
            if (_panels[index].panel != null)
                _panels[index].panel.SetActive(true);
        }
    }

    // 非表示ボタンに割り当てる
    public void HidePanel(int index)
    {
        if (_panels != null && index >= 0 && index < _panels.Length)
        {
            if (_panels[index].panel != null)
                _panels[index].panel.SetActive(false);
        }
    }

    public void HideAllPanels()
    {
        if (_panels == null) return;
        for (int i = 0; i < _panels.Length; i++)
        {
            HidePanel(i);
        }
    }

    private void HideEscPanels()
    {
        if (_panels == null) return;
        for (int i = 0; i < _panels.Length; i++)
        {
            // Only hide if the panel is registered, active, AND hideOnEsc is enabled
            if (_panels[i].panel != null && _panels[i].panel.activeSelf && _panels[i].hideOnEsc)
            {
                SystemScript.EscapeQuitHandler.SuppressQuitForCurrentFrame();
                _panels[i].panel.SetActive(false);
            }
        }
    }
}
