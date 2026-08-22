#if DEBUG
using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace Gridworks.Game.Realtime.R2;

internal sealed partial class RealtimeSliceCheckpointRunner : Control
{
    private const string ArgumentPrefix = "--checkpoint=";
    private const string SliceScenePath =
        "res://realtime/r2/RealtimeSliceMain.tscn";

    public override void _Ready() => _ = RunAsync();

    private async Task RunAsync()
    {
        int exitCode = 1;
        try
        {
            string checkpointId = ParseCheckpointId(OS.GetCmdlineUserArgs());
            PackedScene packed = ResourceLoader.Load<PackedScene>(SliceScenePath) ??
                throw new InvalidOperationException(
                    $"Unable to load actual slice scene '{SliceScenePath}'.");
            RealtimeSliceMain slice = packed.Instantiate<RealtimeSliceMain>();
            AddChild(slice);
            await SettleFrames(2);

            RealtimeSliceCheckpointFact checkpoint =
                slice.EnterTargetedLiveCheckpoint(checkpointId);
            await SettleFrames(2);
            PrintReady(checkpoint);

            RealtimeSliceCheckpointSegmentResult segment =
                slice.RunTargetedLiveCheckpointSegment(checkpoint);
            await SettleFrames(2);
            RealtimeSliceCheckpointEvidence evidence =
                slice.CompleteTargetedLiveCheckpoint(segment);
            GD.Print(string.Join(' ',
                evidence.EvidenceLabel,
                $"startMinute={evidence.StartMinute}",
                $"startHash={evidence.StartCanonicalStateSha256}",
                $"replayHash={evidence.CommandReplaySha256}",
                $"endMinute={evidence.EndMinute}",
                $"endHash={evidence.EndCanonicalStateSha256}",
                $"presentationRevision={evidence.EndPresentationRevision}",
                $"renderedAssets={evidence.RenderedAssetCount}",
                $"hudClock={Escape(evidence.HudClockText)}"));
            exitCode = 0;
        }
        catch (ArgumentException exception)
        {
            GD.PushError($"TARGETED_LIVE_CHECKPOINT_ARGUMENT_FAIL {exception.Message}");
            exitCode = 2;
        }
        catch (Exception exception)
        {
            GD.PushError(
                $"TARGETED_LIVE_CHECKPOINT_FAIL {exception.GetType().Name}: " +
                exception.Message);
            exitCode = 1;
        }
        ScheduleQuit(exitCode);
    }

    private static string ParseCheckpointId(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        string[] checkpointArguments = arguments.Where(argument =>
                argument.StartsWith(ArgumentPrefix, StringComparison.Ordinal))
            .ToArray();
        if (arguments.Length != 1 || checkpointArguments.Length != 1)
        {
            throw new ArgumentException(
                $"Exactly one {ArgumentPrefix}<ID> user argument is required; " +
                $"known IDs: {string.Join(", ", RealtimeSliceCheckpointIds.All)}.");
        }
        string id = checkpointArguments[0][ArgumentPrefix.Length..];
        if (!RealtimeSliceCheckpointIds.IsKnown(id))
        {
            throw new ArgumentException(
                $"Unknown checkpoint '{id}'; known IDs: " +
                $"{string.Join(", ", RealtimeSliceCheckpointIds.All)}.");
        }
        return id;
    }

    private static void PrintReady(RealtimeSliceCheckpointFact checkpoint)
    {
        string construction = checkpoint.ActiveConstruction is null
            ? "none"
            : string.Join(',',
                checkpoint.ActiveConstruction.Kind,
                $"due={checkpoint.ActiveConstruction.CompletionMinute}",
                $"nodes={string.Join('+', checkpoint.ActiveConstruction.NodeIds)}",
                $"edges={string.Join('+', checkpoint.ActiveConstruction.EdgeIds)}");
        GD.Print(string.Join(' ',
            "TARGETED_LIVE_CHECKPOINT_READY",
            $"id={checkpoint.CheckpointId}",
            $"campaignSchema={checkpoint.Fixture.RealtimeCampaignSchemaVersion}",
            $"campaignId={checkpoint.Fixture.RealtimeCampaignId}",
            $"campaignSourceHash={checkpoint.Fixture.RealtimeCampaignSourceSha256}",
            $"campaignDefinitionHash={checkpoint.Fixture.RealtimeCampaignDefinitionSha256}",
            $"worldSchema={checkpoint.Fixture.RealtimeWorldSchemaVersion}",
            $"worldId={checkpoint.Fixture.RealtimeWorldId}",
            $"worldSourceHash={checkpoint.Fixture.RealtimeWorldSourceSha256}",
            $"worldDefinitionHash={checkpoint.Fixture.RealtimeWorldDefinitionSha256}",
            $"replaySchema={checkpoint.CommandReplaySchemaId}",
            $"replayHash={checkpoint.CommandReplaySha256}",
            $"commandCount={checkpoint.CommandCount}",
            $"startMinute={checkpoint.StartMinute}",
            $"startHash={checkpoint.StartCanonicalStateSha256}",
            $"expectedEndHash={checkpoint.ExpectedEndCanonicalStateSha256}",
            $"construction={construction}",
            $"activeEvents={checkpoint.ActiveEvents.Count}",
            $"activeDuty={(checkpoint.ActiveDuty is null ? "none" : checkpoint.ActiveDuty.EventId)}",
            $"thermalAssets={checkpoint.Thermal.Count}",
            $"selection={(checkpoint.ExpectedSelectionId ?? "none")}",
            $"anchor={(checkpoint.ExpectedTimelineAnchorMinute?.ToString() ?? "none")}",
            $"surface={checkpoint.ExpectedSurface}",
            $"tool={checkpoint.ExpectedTool}",
            $"simulation={checkpoint.ExpectedSimulation}",
            $"allowedInput={checkpoint.AllowedNextInput}",
            $"allowedFrames={checkpoint.AllowedFrameCount}/{checkpoint.AllowedFramesPerSecond}"));
    }

    private async Task SettleFrames(int count)
    {
        for (int index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private void ScheduleQuit(int exitCode)
    {
        SceneTree tree = GetTree();
        int remainingFrames = 2;
        void DrainAndQuit()
        {
            remainingFrames--;
            if (remainingFrames > 0)
            {
                return;
            }
            tree.ProcessFrame -= DrainAndQuit;
            tree.Quit(exitCode);
        }
        tree.ProcessFrame += DrainAndQuit;
    }

    private static string Escape(string value) =>
        value.Replace(' ', '_');
}
#endif
