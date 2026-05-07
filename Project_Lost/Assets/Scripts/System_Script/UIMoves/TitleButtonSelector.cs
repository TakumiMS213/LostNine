using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class TitleButtonSelector : MonoBehaviour
{
    [Header("ボタン (このセット専用)")]
    [SerializeField] private Button[] buttons;

    [Header("選択中の背景 (ボタンとindex対応)")]
    [SerializeField] private GameObject[] highlights;

    [Header("選択後のハイライト (ボタンとindex対応)")]
    [SerializeField] private GameObject[] selectedHighlights;

    [Space]
    [Header("Sound Settings")]
    [SerializeField] private AudioSource moveSound;
    [SerializeField] private AudioSource selectSound;
    [SerializeField] private float soundVolume = 1f;
    private bool isSelected = false; // 決定済みフラグ 決定後には移動無効化
    [SerializeField] private MultiUISwitcher uiSwitcher;

    private int index = 0;

    void Start()
    {
        UpdateHighlight();
        SelectThis();  // 初期フォーカス
        ClearSelectedHighlights();  // 選択ハイライトは初期状態では非表示
    }

    void Update()
    {
        // 現在 UI フォーカスがこのボタン群以外なら、入力無視
        if (!IsFocused()) return;

        // マウスホバー検出
        CheckMouseHover();

        // ↓ 移動（Down / S / ホイール下）
        if (!isSelected && (Input.GetKeyDown(KeyCode.DownArrow) || Input.GetKeyDown(KeyCode.S) || Input.GetAxis("Mouse ScrollWheel") < 0f))
        {
            index = (index + 1) % buttons.Length;
            ClearSelectedHighlights();
            UpdateHighlight();
            PlaySound(moveSound);
        }

        // ↑ 移動（Up / W / ホイール上）
        if (!isSelected && (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.W) || Input.GetAxis("Mouse ScrollWheel") > 0f))
        {
            index = (index - 1 + buttons.Length) % buttons.Length;
            ClearSelectedHighlights();
            UpdateHighlight();
            PlaySound(moveSound);
        }

        // 決定（Enter / F）
        if (!isSelected && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.F) || Input.GetKeyDown(KeyCode.Space)))
        {
            isSelected = true;
            ShowSelectedHighlight(index);
            buttons[index].onClick.Invoke();
            // SEはボタン側のOnClick()で PlaySelectSound() を呼ぶ
        }
        // キャンセル（Esc / G）
        if (isSelected && (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.G)))
        {
            if (Input.GetKeyDown(KeyCode.Escape))
            {
                SystemScript.EscapeQuitHandler.SuppressQuitForCurrentFrame();
            }

            Debug.Log("TitleButtonSelector: キャンセル入力検出");
            isSelected = false;
            ClearSelectedHighlights();
            UpdateHighlight();
            uiSwitcher.HidePanels(0);
        }
    }

    public void ResetSelection() // 外部から選択解除したいときに呼ぶ
    {
        isSelected = false;
        ClearSelectedHighlights();
        UpdateHighlight();
    }

    public void PlaySelectSound() // ボタンの OnClick() から呼び出す
    {
        PlaySound(selectSound);
    }

    private bool IsFocused()
    {
        var selected = EventSystem.current.currentSelectedGameObject;
        if (selected == null) return false;

        foreach (var b in buttons)
        {
            if (b.gameObject == selected) return true;
        }
        return false;
    }

    private void SelectThis()
    {
        EventSystem.current.SetSelectedGameObject(buttons[index].gameObject);
    }

    private void UpdateHighlight()
    {
        for (int i = 0; i < highlights.Length; i++)
        {
            highlights[i].SetActive(i == index);
        }
        SelectThis();
    }

    private void PlaySound(AudioSource source)
    {
        if (source == null || source.clip == null) return;
        source.PlayOneShot(source.clip, soundVolume);
    }

    private void ClearSelectedHighlights()
    {
        for (int i = 0; i < selectedHighlights.Length; i++)
        {
            if (selectedHighlights[i] != null)
            {
                selectedHighlights[i].SetActive(false);
            }
        }
    }

    private void ShowSelectedHighlight(int buttonIndex)
    {
        if (selectedHighlights.Length > buttonIndex && selectedHighlights[buttonIndex] != null)
        {
            selectedHighlights[buttonIndex].SetActive(true);
        }
    }

    private void CheckMouseHover()
    {
        if(isSelected) return;
        var pointerData = new PointerEventData(EventSystem.current)
        {
            position = Input.mousePosition
        };

        var raycastResults = new System.Collections.Generic.List<RaycastResult>();
        EventSystem.current.RaycastAll(pointerData, raycastResults);

        foreach (var result in raycastResults)
        {
            for (int i = 0; i < buttons.Length; i++)
            {
                if (result.gameObject == buttons[i].gameObject)
                {
                    if (index != i)
                    {
                        index = i;
                        ClearSelectedHighlights();
                        UpdateHighlight();
                        PlaySound(moveSound);
                    }
                    return;
                }
            }
        }
    }
}
