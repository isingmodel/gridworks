#if DEBUG
using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;

namespace Gridworks.Game.Realtime.R2;

/// <summary>
/// Non-score native observation host. It prepares one frozen technical
/// checkpoint, then waits for the player's real production HUD/keyboard input.
/// It never presses a control or injects a simulation frame on the actor's behalf.
/// </summary>
internal sealed partial class RealtimeInteractiveCheckpointHost : Control
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
            slice.ArmInteractiveTargetedLiveCheckpoint(checkpoint);
            PrintReady(checkpoint);

            RealtimeSliceCheckpointEvidence? evidence = null;
            while (!slice.TryCompleteInteractiveTargetedLiveCheckpoint(out evidence))
            {
                await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            }
            if (evidence is null)
            {
                throw new InvalidOperationException(
                    "Interactive checkpoint completed without renderer evidence.");
            }
            GD.Print(string.Join(' ',
                $"TARGETED_LIVE_CHECKPOINT_PASS:{evidence.CheckpointId}",
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

        await SettleFrames(2);
        GetTree().Quit(exitCode);
    }

    internal static string ParseCheckpointId(string[] arguments)
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

    private static void PrintReady(RealtimeSliceCheckpointFact checkpoint) =>
        GD.Print(string.Join(' ',
            "TARGETED_LIVE_CHECKPOINT_READY",
            $"id={checkpoint.CheckpointId}",
            $"startMinute={checkpoint.StartMinute}",
            $"startHash={checkpoint.StartCanonicalStateSha256}",
            $"expectedEndHash={checkpoint.ExpectedEndCanonicalStateSha256}",
            $"allowedInput={checkpoint.AllowedNextInput}",
            $"allowedAdvanceMinutes={checkpoint.AllowedAdvanceMinutes}",
            "actorInput=PRODUCTION_MOUSE_OR_KEYBOARD_ONLY"));

    private async Task SettleFrames(int count)
    {
        for (int index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private static string Escape(string value) =>
        value.Replace(' ', '_');
}
#endif
