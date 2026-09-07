using WhoWhoo3D.Domain.Board;

namespace WhoWhoo3D.Domain.Wave;

/// <summary>
/// Why a slot changed hands. Presentation depends on this: only <see cref="Expanded"/>
/// tiles actually travel across an edge, so only they get a flying tile.
///
/// This is recorded rather than inferred. An earlier version worked out the origin of a
/// change by looking up the slot's partner, which silently became wrong the moment
/// conquest started taking whole faces — sibling slots have partners too, and flights were
/// drawn from unrelated parts of the board.
/// </summary>
public enum SlotChangeCause
{
    /// <summary>A player claimed a white tile on their turn. No travel.</summary>
    Claimed,

    /// <summary>Taken across the shared edge by an expanding face. This one flies.</summary>
    Expanded,

    /// <summary>
    /// Taken because a *different* slot on the same face was conquered. It changes hands
    /// where it stands — nothing crosses an edge, so nothing should be drawn travelling.
    /// </summary>
    Absorbed,

    /// <summary>Released to white by the face that spent it expanding. No travel.</summary>
    Released,
}

/// <summary>
/// One slot changing hands. PlayerId.None means the tile went back to white.
///
/// <para><b>Origin</b> — for an <see cref="SlotChangeCause.Expanded"/> change, the slot the
/// tile travelled from. Recorded rather than derived: an arrival landing on a slot the
/// attacker already holds is redirected to a free slot on the same face, so the destination
/// is not necessarily the origin's partner.</para>
/// </summary>
public readonly record struct SlotStateChange(
    SlotId Slot,
    PlayerId Before,
    PlayerId After,
    SlotChangeCause Cause,
    SlotId? Origin = null)
{
    public override string ToString() =>
        Origin.HasValue
            ? $"{Slot}: {Before} → {After} ({Cause} from {Origin.Value})"
            : $"{Slot}: {Before} → {After} ({Cause})";
}
