using UnityEngine;
using WhoWhoo3D.Domain.Match;

namespace WhoWhoo3D.UI.HUD;

/// <summary>Shared labels, glyph colors and guide copy. The gameplay inventory owns charges and legality.</summary>
internal sealed class AbilityPresentation
{
    public readonly MatchAction Action;
    public readonly Color Color;
    public readonly string Description;
    public string Name => Action.ToString();

    private AbilityPresentation(MatchAction action, Color32 color, string description)
    {
        Action = action;
        Color = color;
        Description = description;
    }

    public static readonly AbilityPresentation[] Cards =
    {
        new(MatchAction.Relay, new Color32(254, 188, 118, 255),
            "Give up one of your tiles to take two white tiles on the face across its edge. A full face starts a wave."),
        new(MatchAction.Freeze, new Color32(139, 221, 250, 255),
            "Freeze one cube through the next opponent’s turn. It blocks direct claims and Relay arrivals. Waves still pass through."),
        new(MatchAction.Beacon, new Color32(211, 174, 253, 255),
            "Mark two white tiles on one face. At the start of your next turn, claim both if both are still legal.")
    };
}
