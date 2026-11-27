using TMPro;
using UnityEngine;
using UnityEngine.Events;
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

        public void Show(bool visible, bool enabled)
        {
            Button.gameObject.SetActive(visible);
            Button.interactable = enabled;
            Caption.color = enabled ? (_primary ? Surface : White) : Soft;
        }
    }
}
