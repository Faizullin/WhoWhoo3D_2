using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using WhoWhoo3D.Domain.Match;
using WhoWhoo3D.Gameplay.Abilities;
using WhoWhoo3D.Gameplay.Game;
using WhoWhoo3D.Gameplay.Player;
using static WhoWhoo3D.UI.HUD.HudControls;

namespace WhoWhoo3D.UI.HUD;

internal sealed class AbilityGuide
{
    private readonly RectTransform _backdrop, _panel;
    private readonly TextMeshProUGUI _heading, _rule;
    private readonly CanvasGroup _fade;
    private readonly Control _close, _motion, _textSize;
    private readonly RectTransform[] _rows = new RectTransform[AbilityPresentation.Cards.Length];
    private readonly TextMeshProUGUI[] _names = new TextMeshProUGUI[AbilityPresentation.Cards.Length];
    private readonly TextMeshProUGUI[] _copy = new TextMeshProUGUI[AbilityPresentation.Cards.Length];
    private readonly UnityEngine.Events.UnityAction _dismiss;
    public bool IsOpen => _backdrop.gameObject.activeSelf;

    public AbilityGuide(RectTransform canvas, RectTransform safe, UnityEngine.Events.UnityAction dismiss)
    {
        _dismiss = dismiss;
        _backdrop = Panel(canvas, "Guide backdrop", new Color32(8, 15, 22, 182)).rectTransform;
        _backdrop.anchorMin = Vector2.zero;
        _backdrop.anchorMax = Vector2.one;
        _backdrop.offsetMin = _backdrop.offsetMax = Vector2.zero;
        _panel = Panel(safe, "Ability guide", Surface).rectTransform;
        // The guide and its safe-area parent render over the full-screen input shield.
        safe.SetAsLastSibling();
        _fade = _panel.gameObject.AddComponent<CanvasGroup>();
        _heading = Label(_panel, "Guide heading", 22, White, true);
        _close = new Control(_panel, "Close guide", dismiss);
        _close.Caption.text = "Close";
        for (int i = 0; i < _rows.Length; i++)
        {
            var card = AbilityPresentation.Cards[i];
            _rows[i] = Panel(_panel, card.Name, Tile).rectTransform;
            var glyph = AbilityGlyph.Create(_rows[i], card.Action, card.Color);
            Place(glyph.rectTransform, 12, 3, 23, 23);
            _names[i] = Label(_rows[i], "Name and charges", 16, card.Color, true);
            _copy[i] = Label(_rows[i], "Description", 14, White);
            _copy[i].textWrappingMode = TextWrappingModes.Normal;
            _copy[i].text = card.Description;
        }
        _rule = Label(_panel, "When to play", 14, Soft);
        _rule.textWrappingMode = TextWrappingModes.Normal;
        _motion = new Control(_panel, "Reduce motion", () => PresentationSettings.ReducedMotion = !PresentationSettings.ReducedMotion);
        _textSize = new Control(_panel, "Large text", () => PresentationSettings.LargeText = !PresentationSettings.LargeText);
        Close();
    }

    public void Open()
    {
        _backdrop.gameObject.SetActive(true);
        _panel.gameObject.SetActive(true);
        _fade.alpha = PresentationSettings.ReducedMotion ? 1 : .45f;
        _close.Button.Select();
    }

    public void Close()
    {
        _backdrop.gameObject.SetActive(false);
        _panel.gameObject.SetActive(false);
    }

    public void Refresh(Vector2 size, PlayerState seat, bool supply)
    {
        if (UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame == true)
        {
            _dismiss();
            return;
        }
        if (EventSystem.current != null && (EventSystem.current.currentSelectedGameObject == null ||
            !EventSystem.current.currentSelectedGameObject.transform.IsChildOf(_panel))) _close.Button.Select();
        Navigate(_panel);
        _fade.alpha = PresentationSettings.ReducedMotion ? 1 : Mathf.MoveTowards(_fade.alpha, 1, Time.unscaledDeltaTime * 6);
        _heading.text = $"{seat.Id} inventory";
        _rule.text = supply
            ? $"Confirm on your turn to spend one charge and your action. After {AbilityInventory.SupplyThreshold} ordinary claims, refill one empty card for free, once per match."
            : "Confirm on your turn to spend one charge and your action. Cancel keeps both. Fixed charges do not refill.";
        _textSize.Caption.text = PresentationSettings.LargeText ? "Large text   On" : "Large text   Off";
        _motion.Caption.text = PresentationSettings.ReducedMotion ? "Reduce motion   On" : "Reduce motion   Off";
        float w = Mathf.Min(size.x - 20, 526);
        float inner = w - 24;
        Place(_heading.rectTransform, 14, 8, inner - 88, 34);
        Place(_close.Rect, w - 82, 8, 70, 32);
        float y = 48;
        for (int i = 0; i < _rows.Length; i++)
        {
            var card = AbilityPresentation.Cards[i];
            bool available = supply || card.Action != MatchAction.Beacon;
            _names[i].text = available ? $"{card.Name}   ×{(seat.Inventory.Has(card.Action) ? 1 : 0)}" : $"{card.Name}   ·   Supply only";
            float indent = w < 410 ? 12 : 46;
            float copyWidth = inner - indent - 12;
            float copyHeight = Mathf.Ceil(_copy[i].GetPreferredValues(_copy[i].text, copyWidth, 1000).y) + 3;
            Place(_rows[i], 12, y, inner, 29 + copyHeight);
            Place(_names[i].rectTransform, 46, 3, inner - 58, 21);
            Place(_copy[i].rectTransform, indent, 26, copyWidth, copyHeight);
            y += 34 + copyHeight;
        }
        float lines = Mathf.Ceil(_rule.GetPreferredValues(_rule.text, inner, 1000).y) + 4;
        Place(_rule.rectTransform, 12, y, inner, lines);
        float half = (inner - 6) / 2;
        Place(_motion.Rect, 12, y + lines + 4, half, 34);
        Place(_textSize.Rect, 18 + half, y + lines + 4, half, 34);
        float h = y + lines + 46;
        Place(_panel, (size.x - w) / 2, (size.y - h) / 2, w, h);
    }
}
