using Gridworks.Core.Release.V2;

namespace Gridworks.Game;

internal enum CommercialPresentationMode
{
    Briefing,
    WindowStory,
    Operations,
    ResumeOrientation,
    Result,
    Epilogue,
}

internal sealed record CommercialFrozenResultPresentation(
    CommercialCoreSnapshot CoreSnapshot,
    ConstructionSnapshot Construction,
    long CashUnit,
    ThermalSequenceResult ThermalProjection,
    int ThermalProjectionIndex,
    string SelectedThermalAssetId,
    string? SelectedDemandId,
    CommercialChapterResultRecord Result)
{
    public bool IsFinalChapter => CoreSnapshot.ChapterIndex + 1 == CoreSnapshot.ChapterCount;
}
