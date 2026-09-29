using UnityEngine;

[System.Serializable]
public class PanelGroup
{
    public GameObject[] panels;
}

public class MultiUISwitcher : MonoBehaviour
{
    [Header("パネルのグループ (インスペクター編集可能)")]
    [SerializeField] private PanelGroup[] panelGroups;

    public void ShowPanels(int groupIndex)
    {
        SetPanelsActive(groupIndex, true);
    }

    public void HidePanels(int groupIndex)
    {
        SetPanelsActive(groupIndex, false);
    }

    private void SetPanelsActive(int groupIndex, bool active)
    {
        if (!IsValidGroup(groupIndex)) return;

        foreach (var panel in panelGroups[groupIndex].panels)
        {
            if (panel != null)
                panel.SetActive(active);
        }
    }

    private bool IsValidGroup(int index)
    {
        return panelGroups != null && index >= 0 && index < panelGroups.Length;
    }
}
