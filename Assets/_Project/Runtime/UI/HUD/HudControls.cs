using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace WhoWhoo3D.UI.HUD;

/// <summary>Shared canvas controls in safe-area logical pixels, measured from each panel's top left.</summary>
internal static class HudControls
{
    public static readonly Color Surface = new Color32(23, 32, 40, 249);
    public static readonly Color Tile = new Color32(43, 57, 68, 255);
    public static readonly Color Mint = new Color32(156, 240, 213, 255);
    public static readonly Color Soft = new Color32(183, 201, 210, 255);
    public static readonly Color White = new Color32(243, 247, 248, 255);

    public static Canvas CreateCanvas(Transform owner, string name, out RectTransform safe)
    {
        var root = Node(owner, name);
        var canvas = root.gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 12;
        root.gameObject.AddComponent<GraphicRaycaster>();
        if (EventSystem.current == null)
        {
            var events = new GameObject("HUD input", typeof(EventSystem), typeof(InputSystemUIInputModule));
            events.transform.SetParent(owner, false);
        }
        safe = Node(root, "Safe area");
        safe.pivot = new Vector2(.5f, .5f);
        return canvas;
    }

    public static Vector2 Fit(Canvas canvas, RectTransform safe, float uiScale)
    {
        var area = Screen.safeArea;
        var viewport = new HudViewport(area, Screen.height, uiScale);
        canvas.scaleFactor = viewport.Scale;
        safe.anchorMin = new Vector2(area.xMin / Screen.width, area.yMin / Screen.height);
        safe.anchorMax = new Vector2(area.xMax / Screen.width, area.yMax / Screen.height);
        safe.offsetMin = safe.offsetMax = Vector2.zero;
        return viewport.Size;
    }

    public static void Navigate(Canvas canvas)
    {
        var keys = Keyboard.current;
        if (keys == null || !keys.tabKey.wasPressedThisFrame || EventSystem.current == null) return;
        var list = canvas.GetComponentsInChildren<Selectable>();
        var selected = EventSystem.current.currentSelectedGameObject;
        int index = System.Array.FindIndex(list, item => item.gameObject == selected);
        int direction = keys.shiftKey.isPressed ? -1 : 1;
        if (index < 0 && direction < 0) index = 0;
        for (int i = 0; i < list.Length; i++)
        {
            index = (index + direction + list.Length) % list.Length;
            if (!list[index].IsInteractable()) continue;
            list[index].Select();
            break;
        }
    }

    public static TMP_InputField Field(Transform parent, string name, int limit)
    {
        var bg = Panel(parent, name, Color.white);
        var field = bg.gameObject.AddComponent<TMP_InputField>();
        field.targetGraphic = bg;
        var colors = field.colors;
        colors.normalColor = Tile;
        colors.selectedColor = colors.highlightedColor = new Color32(65, 88, 97, 255);
        field.colors = colors;
        var viewport = Node(bg.transform, "Text area");
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.offsetMin = new Vector2(9, 0);
        viewport.offsetMax = new Vector2(-9, 0);
        viewport.gameObject.AddComponent<RectMask2D>();
        var value = Label(viewport, "Value", 16, White);
        value.richText = false;
        value.rectTransform.anchorMin = Vector2.zero;
        value.rectTransform.anchorMax = Vector2.one;
        value.rectTransform.offsetMin = value.rectTransform.offsetMax = Vector2.zero;
        field.textViewport = viewport;
        field.textComponent = value;
        field.richText = false;
        field.characterLimit = limit;
        field.customCaretColor = true;
        field.caretColor = Mint;
        return field;
    }

    public static RectTransform Node(Transform parent, string name)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
        return rect;
    }

    public static void Place(RectTransform rect, float x, float y, float width, float height)
    {
        rect.anchoredPosition = new Vector2(x, -y);
        rect.sizeDelta = new Vector2(width, height);
    }

    public static Image Panel(Transform parent, string name, Color color)
    {
        var image = Node(parent, name).gameObject.AddComponent<Image>();
        image.color = color;
        return image;
    }

    public static TextMeshProUGUI Label(Transform parent, string name, float size, Color color, bool bold = false)
    {
        var text = Node(parent, name).gameObject.AddComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.raycastTarget = false;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        return text;
    }

    internal sealed class Control
    {
        public readonly Button Button;
        public readonly TextMeshProUGUI Caption;
        private readonly bool _primary;
        public RectTransform Rect => (RectTransform)Button.transform;

        public Control(Transform parent, string name, UnityAction click, bool primary = false)
        {
            _primary = primary;
            var bg = Panel(parent, name, Color.white);
            Button = bg.gameObject.AddComponent<Button>();
            Button.targetGraphic = bg;
            var colors = Button.colors;
            colors.normalColor = primary ? Mint : Tile;
            colors.highlightedColor = colors.selectedColor = primary ? Color.white : new Color32(70, 105, 111, 255);
            colors.pressedColor = new Color32(106, 161, 148, 255);
            colors.disabledColor = new Color32(47, 56, 62, 255);
            colors.fadeDuration = 0f;
            Button.colors = colors;
            Button.onClick.AddListener(click);
            Caption = Label(bg.transform, "Caption", 15, primary ? Surface : White, true);
            Caption.alignment = TextAlignmentOptions.Center;
            var label = Caption.rectTransform;
            label.anchorMin = Vector2.zero;
            label.anchorMax = Vector2.one;
            label.offsetMin = new Vector2(2, 0);
            label.offsetMax = new Vector2(-2, 0);
            Caption.text = name;
        }

        private Image _mark;

        public void MarkSelected(bool selected)
        {
            if (_mark == null && selected)
            {
                _mark = Panel(Rect, "Selected", Mint);
                _mark.raycastTarget = false;
                _mark.rectTransform.anchorMin = Vector2.zero;
                _mark.rectTransform.anchorMax = Vector2.right;
                _mark.rectTransform.offsetMin = Vector2.zero;
                _mark.rectTransform.offsetMax = new Vector2(0, 3);
            }
            if (_mark != null) _mark.gameObject.SetActive(selected);
        }

        public void Show(bool visible, bool enabled)
        {
            Button.gameObject.SetActive(visible);
            Button.interactable = enabled;
            Caption.color = enabled ? (_primary ? Surface : White) : Soft;
        }
    }
}
