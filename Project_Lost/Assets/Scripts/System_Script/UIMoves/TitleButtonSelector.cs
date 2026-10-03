using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

public class TitleButtonSelector : MonoBehaviour
{
    [Header("ボタン (このセット専用)")]
    [SerializeField] private Button[] buttons;
    [Header("選択中の背景 (ボタンとindex対応)")]
    [SerializeField] private GameObject[] highlights;
    [Header("選択後のハイライト (ボタンとindex対応)")]
    [SerializeField] private GameObject[] selectedHighlights;
    [Header("Sound Settings")]
    [SerializeField] private AudioSource moveSound;
    [SerializeField] private AudioSource selectSound;
    [SerializeField] private float soundVolume = 1f;
    [SerializeField] private UISwitcher uiSwitcher;

    private int index;
    private UnityAction[] _clickHandlers;
    private Vector2 _lastPointerPosition;
    private readonly List<RaycastResult> _raycastResults = new();
    private PointerEventData _pointerData;

    private void Awake()
    {
        if (buttons == null) return;
        _clickHandlers = new UnityAction[buttons.Length];
        for (int i = 0; i < buttons.Length; i++)
        {
            if (buttons[i] == null) continue;
            int buttonIndex = i;
            _clickHandlers[i] = () => HandleClick(buttonIndex);
            // 矢印・W/S・決定はEventSystemに任せ、サブパネルへ勝手に移動しない。
            buttons[i].navigation = new Navigation
            {
                mode = Navigation.Mode.Explicit,
                selectOnUp = buttons[FindNextIndex(i, -1)],
                selectOnDown = buttons[FindNextIndex(i, 1)]
            };
        }
        DisableHighlightRaycasts(highlights);
        DisableHighlightRaycasts(selectedHighlights);
    }

    private void OnEnable()
    {
        if (_clickHandlers != null)
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i] != null && _clickHandlers[i] != null)
                    buttons[i].onClick.AddListener(_clickHandlers[i]);
        if (uiSwitcher != null) uiSwitcher.OnPanelClosed += ResetSelection;
        _lastPointerPosition = Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
        ResetSelection();
    }

    private void OnDisable()
    {
        if (_clickHandlers != null)
            for (int i = 0; i < buttons.Length; i++)
                if (buttons[i] != null && _clickHandlers[i] != null)
                    buttons[i].onClick.RemoveListener(_clickHandlers[i]);
        if (uiSwitcher != null) uiSwitcher.OnPanelClosed -= ResetSelection;
    }

    private void LateUpdate()
    {
        var eventSystem = EventSystem.current;
        if (eventSystem == null || buttons == null || buttons.Length == 0) return;

        // フォーカスが別のUIへ移っていても、マウスでタイトルへ戻れる。
        // 静止したマウスは、キーボードで選んだ項目を上書きしない。
        var mouse = Mouse.current;
        if (mouse != null)
        {
            Vector2 position = mouse.position.ReadValue();
            if (position != _lastPointerPosition || mouse.leftButton.wasPressedThisFrame)
                CheckMouseHover(eventSystem, position);
            _lastPointerPosition = position;
        }

        var selected = eventSystem.currentSelectedGameObject;
        if (selected == null || !selected.activeInHierarchy)
        {
            SelectThis();
            selected = eventSystem.currentSelectedGameObject;
        }
        int focusedIndex = FindButtonIndex(selected);
        if (focusedIndex >= 0) SetIndex(focusedIndex);

        var keyboard = Keyboard.current;
        if (keyboard != null && keyboard.gKey.wasPressedThisFrame && uiSwitcher != null)
        {
            uiSwitcher.HideAllPanels();
            ResetSelection();
            return;
        }
        // スライダーやロード先を操作している間は、そちらのキーボード操作を優先する。
        if (focusedIndex < 0 || !CanSelect(index)) return;

        float scroll = mouse != null ? mouse.scroll.ReadValue().y : 0f;
        if (scroll != 0f)
        {
            SetIndex(FindNextIndex(index, scroll > 0f ? -1 : 1));
            SelectThis();
        }

        // 標準Submitと重複させず、従来のF/Spaceも決定キーとして維持する。
        var module = eventSystem.currentInputModule as InputSystemUIInputModule;
        bool submitted = module != null && module.submit != null
            && module.submit.action != null && module.submit.action.WasPerformedThisFrame();
        if (!submitted && keyboard != null &&
            (keyboard.fKey.wasPressedThisFrame || keyboard.spaceKey.wasPressedThisFrame))
            buttons[index].onClick.Invoke();
    }

    public void ResetSelection()
    {
        if (buttons == null || buttons.Length == 0) return;
        if (!CanSelect(index)) index = FindNextIndex(index, 1);
        ClearSelectedHighlights();
        UpdateHighlight();
        SelectThis();
    }

    public void PlaySelectSound() => PlaySound(selectSound);

    private void HandleClick(int buttonIndex)
    {
        SetIndex(buttonIndex);
        ClearSelectedHighlights();
        if (selectedHighlights != null && buttonIndex < selectedHighlights.Length
            && selectedHighlights[buttonIndex] != null)
            selectedHighlights[buttonIndex].SetActive(true);
    }

    private void SetIndex(int nextIndex)
    {
        if (index == nextIndex || !CanSelect(nextIndex)) return;
        index = nextIndex;
        ClearSelectedHighlights();
        UpdateHighlight();
        PlaySound(moveSound);
    }

    private bool CanSelect(int i) => i >= 0 && i < buttons.Length && buttons[i] != null
        && buttons[i].IsActive() && buttons[i].IsInteractable();

    private int FindNextIndex(int start, int direction)
    {
        for (int step = 1; step <= buttons.Length; step++)
        {
            int candidate = (start + direction * step + buttons.Length) % buttons.Length;
            if (CanSelect(candidate)) return candidate;
        }
        return start;
    }

    private int FindButtonIndex(GameObject target)
    {
        if (target == null) return -1;
        var button = target.GetComponentInParent<Button>();
        for (int i = 0; i < buttons.Length; i++)
            if (button != null && buttons[i] == button) return i;
        return -1;
    }

    private void SelectThis()
    {
        if (EventSystem.current != null && CanSelect(index))
            EventSystem.current.SetSelectedGameObject(buttons[index].gameObject);
    }

    private void UpdateHighlight()
    {
        if (highlights == null) return;
        for (int i = 0; i < highlights.Length; i++)
            if (highlights[i] != null) highlights[i].SetActive(i == index);
    }

    private void ClearSelectedHighlights()
    {
        if (selectedHighlights == null) return;
        foreach (var highlight in selectedHighlights)
            if (highlight != null) highlight.SetActive(false);
    }

    private void CheckMouseHover(EventSystem eventSystem, Vector2 position)
    {
        _pointerData ??= new PointerEventData(eventSystem);
        _pointerData.position = position;
        _raycastResults.Clear();
        eventSystem.RaycastAll(_pointerData, _raycastResults);
        if (_raycastResults.Count == 0) return;

        // 最前面だけを見る。パネル越しに背後のボタンを選択しない。
        int hoveredIndex = FindButtonIndex(_raycastResults[0].gameObject);
        if (hoveredIndex < 0 || !CanSelect(hoveredIndex)) return;
        SetIndex(hoveredIndex);
        SelectThis();
    }

    private static void DisableHighlightRaycasts(GameObject[] objects)
    {
        if (objects == null) return;
        foreach (var obj in objects)
            if (obj != null)
                foreach (var graphic in obj.GetComponentsInChildren<Graphic>(true))
                    graphic.raycastTarget = false;
    }

    private void PlaySound(AudioSource source)
    {
        if (source != null && source.clip != null)
            source.PlayOneShot(source.clip, soundVolume);
    }
}
