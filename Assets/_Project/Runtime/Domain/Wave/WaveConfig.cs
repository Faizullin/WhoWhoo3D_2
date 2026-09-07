using System;

namespace WhoWhoo3D.Domain.Wave;

/// <summary>
/// Wave tuning.
///
/// There is no Capacity any more: a face expands when every slot it actually has is
/// claimed by one player, so a two-slot face fires on two and a four-slot face on four.
///
/// <para><b>ConquestTakesFace</b> — when true, taking a slot also takes every other
/// player's slot on that face, so the whole face changes hands at once and a face is never
/// contested. When false, only the targeted slot flips, contested faces become possible,
/// and the click rule must fall back to majority ownership.</para>
/// </summary>
public sealed record WaveConfig(int MaxChainSteps, bool ConquestTakesFace)
{
    public static readonly WaveConfig Default = new(MaxChainSteps: 8, ConquestTakesFace: true);

    public void Validate()
    {
        if (MaxChainSteps <= 0)
            throw new ArgumentOutOfRangeException(nameof(MaxChainSteps), "MaxChainSteps must be positive.");
    }
}
