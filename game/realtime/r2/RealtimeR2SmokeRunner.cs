#if DEBUG
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using Gridworks.Core.Release.V3;

namespace Gridworks.Game.Realtime.R2;

/// <summary>
/// Dedicated headless entry for deterministic controller evidence. It runs the
/// complete registry by default, or one exact case, without constructing the UI
/// harness. A terminal save is published only after the complete registry passes.
/// </summary>
internal sealed partial class RealtimeR2SmokeRunner : Control
{
    private const string CaseArgumentPrefix = "--case=";
    private const string CompletedSaveOutputArgumentPrefix =
        "--completed-save-output=";

    private sealed record RunRequest(
        string? ExactCaseName,
        string? CompletedSaveOutputPath);

    public override void _Ready()
    {
        int exitCode;
        try
        {
            RunRequest request = ParseRequest(OS.GetCmdlineUserArgs());
            var failures = new List<string>();
            RealtimeR2SmokeResult result = RealtimeR2Smoke.Validate(
                failures,
                request.ExactCaseName);
            if (failures.Count != 0)
            {
                foreach (string failure in failures.Distinct(StringComparer.Ordinal))
                {
                    GD.PushError($"REALTIME_R2_CONTROLLER_SMOKE_FAIL {failure}");
                }
                exitCode = 1;
            }
            else
            {
                RealtimeCampaignSave? completedSave = result.CompletedProductSave;
                if (request.CompletedSaveOutputPath is not null)
                {
                    if (completedSave is null)
                    {
                        throw new InvalidOperationException(
                            "The all-controller run did not return its validated " +
                            "completed product save.");
                    }
                    ValidateAbsentOutputPath(request.CompletedSaveOutputPath);
                    RealtimeCampaignSaveStore.Save(
                        request.CompletedSaveOutputPath,
                        completedSave);
                }
                PrintPass(request, result.ExecutedCaseCount, completedSave);
                exitCode = 0;
            }
        }
        catch (ArgumentException exception)
        {
            GD.PushError(
                $"REALTIME_R2_CONTROLLER_SMOKE_ARGUMENT_FAIL {exception.Message}");
            exitCode = 2;
        }
        catch (Exception exception)
        {
            GD.PushError(
                "REALTIME_R2_CONTROLLER_SMOKE_FAIL " +
                $"{exception.GetType().Name}: {exception.Message}");
            exitCode = 1;
        }
        ScheduleQuit(exitCode);
    }

    private static RunRequest ParseRequest(string[] arguments)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (arguments.Length == 0)
        {
            return new RunRequest(null, null);
        }
        if (arguments.Length != 1)
        {
            throw new ArgumentException(
                "Controller smoke accepts no user arguments, exactly one " +
                $"{CaseArgumentPrefix}<NAME>, or exactly one " +
                $"{CompletedSaveOutputArgumentPrefix}<ABSOLUTE_PATH>.");
        }

        string argument = arguments[0];
        if (argument.StartsWith(CaseArgumentPrefix, StringComparison.Ordinal))
        {
            string caseName = argument[CaseArgumentPrefix.Length..];
            if (!RealtimeR2Smoke.CaseNames.Contains(
                    caseName,
                    StringComparer.Ordinal))
            {
                throw new ArgumentException(
                    $"Unknown R2 smoke case '{caseName}'; known cases: " +
                    $"{string.Join(", ", RealtimeR2Smoke.CaseNames)}.");
            }
            return new RunRequest(caseName, null);
        }
        if (argument.StartsWith(
                CompletedSaveOutputArgumentPrefix,
                StringComparison.Ordinal))
        {
            string path = argument[CompletedSaveOutputArgumentPrefix.Length..];
            ValidateAbsentOutputPath(path);
            return new RunRequest(null, path);
        }
        throw new ArgumentException(
            "Controller smoke accepts no user arguments, exactly one " +
            $"{CaseArgumentPrefix}<NAME>, or exactly one " +
            $"{CompletedSaveOutputArgumentPrefix}<ABSOLUTE_PATH>.");
    }

    private static void ValidateAbsentOutputPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) ||
            !Path.IsPathFullyQualified(path) ||
            Path.GetDirectoryName(path) is null ||
            string.IsNullOrEmpty(Path.GetFileName(path)))
        {
            throw new ArgumentException(
                "The completed-save output must be an absolute file path.");
        }
        if (File.Exists(path) || Directory.Exists(path))
        {
            throw new ArgumentException(
                "The completed-save output path must start absent.");
        }
    }

    private static void PrintPass(
        RunRequest request,
        int executedCaseCount,
        RealtimeCampaignSave? completedSave)
    {
        string mode = request.ExactCaseName is null ? "all" : "case";
        string selection = request.ExactCaseName ?? "all";
        if (completedSave is null)
        {
            GD.Print(string.Join(' ',
                "REALTIME_R2_CONTROLLER_SMOKE_PASS",
                $"mode={mode}",
                $"selection={selection}",
                $"executedCount={executedCaseCount}",
                "terminal=not-run"));
            return;
        }

        int closedStoryCount = completedSave.ClosedStoryCount ??
            throw new InvalidOperationException(
                "The validated current terminal save omitted its story cursor.");
        GD.Print(string.Join(' ',
            "REALTIME_R2_CONTROLLER_SMOKE_PASS",
            $"mode={mode}",
            $"selection={selection}",
            $"executedCount={executedCaseCount}",
            $"route={completedSave.Source.RouteId}",
            $"minute={completedSave.SavedMinute}",
            $"hash={completedSave.CanonicalStateSha256}",
            $"commandCount={completedSave.Commands.Count}",
            $"closedStoryCount={closedStoryCount}"));
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
}
#endif
