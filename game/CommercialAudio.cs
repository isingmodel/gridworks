using System;
using Godot;

namespace Gridworks.Game;

internal enum CommercialAudioCue
{
    Construction,
    Energize,
    ProtectiveStop,
}

/// <summary>
/// Keeps the commercial scene's live-only audio boundary explicit while reusing the
/// deterministic generated sound set. Save restore, journal replay and rendering never call it.
/// </summary>
internal sealed partial class CommercialAudio : Node
{
    private ProductAudio _generatedAudio = null!;

    public override void _Ready()
    {
        _generatedAudio = new ProductAudio();
        AddChild(_generatedAudio);
    }

    public void PlayLive(CommercialAudioCue cue)
    {
        _generatedAudio.Play(cue switch
        {
            CommercialAudioCue.Construction => ProductAudioCue.Breaker,
            CommercialAudioCue.Energize => ProductAudioCue.Energize,
            CommercialAudioCue.ProtectiveStop => ProductAudioCue.Outage,
            _ => throw new ArgumentOutOfRangeException(nameof(cue)),
        });
    }
}
