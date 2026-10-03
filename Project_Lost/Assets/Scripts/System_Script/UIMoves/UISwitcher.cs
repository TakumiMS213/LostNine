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
    public event System.Action OnPanelClosed;

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
        SetPanelActive(index, true);
    }

    // 非表示ボタンに割り当てる
    public void HidePanel(int index)
    {
        SetPanelActive(index, false);
    }

    private void SetPanelActive(int index, bool active)
    {
        if (_panels == null || index < 0 || index >= _panels.Length) return;

        var panel = _panels[index].panel;
        if (panel != null)
        {
            bool wasActive = panel.activeSelf;
            panel.SetActive(active);
            if (wasActive && !active) OnPanelClosed?.Invoke();
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
                SetPanelActive(i, false);
            }
        }
    }
}
