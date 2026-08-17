using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Godot;

namespace Gridworks.Game;

internal sealed record ProductLaunchOptions(
    string SessionId,
    string DiagnosticPath,
    bool Smoke,
    IReadOnlyList<FirstLightGridPoint> SmokeSubstations,
    IReadOnlyList<FirstLightGridPoint> SmokeSupports,
    IReadOnlyList<FirstLightGridPoint> SmokePrimarySupports,
    IReadOnlyList<FirstLightGridPoint> SmokeBackupSupports,
    FirstLightGridPoint? SmokePlant,
    IReadOnlyList<FirstLightGridPoint> SmokePlantSupports)
{
    public static ProductLaunchOptions Parse(IReadOnlyList<string> arguments)
    {
        string? sessionId = null;
        string? diagnosticPath = null;
        bool smoke = false;
        var substations = new List<FirstLightGridPoint>();
        var supports = new List<FirstLightGridPoint>();
        var primarySupports = new List<FirstLightGridPoint>();
        var backupSupports = new List<FirstLightGridPoint>();
        FirstLightGridPoint? plant = null;
        var plantSupports = new List<FirstLightGridPoint>();

        for (int index = 0; index < arguments.Count; index++)
        {
            switch (arguments[index])
            {
                case "--session-id":
                    if (sessionId is not null)
                    {
                        throw new ArgumentException("--session-id may be provided only once.");
                    }
                    sessionId = RequiredValue(arguments, ref index, "--session-id");
                    break;
                case "--diagnostic-log":
                    if (diagnosticPath is not null)
                    {
                        throw new ArgumentException("--diagnostic-log may be provided only once.");
                    }
                    diagnosticPath = RequiredValue(arguments, ref index, "--diagnostic-log");
                    break;
                case "--smoke":
                    if (smoke)
                    {
                        throw new ArgumentException("--smoke may be provided only once.");
                    }
                    smoke = true;
                    break;
                case "--smoke-substation":
                    substations.Add(ParsePoint(
                        RequiredValue(arguments, ref index, "--smoke-substation"),
                        "--smoke-substation"));
                    break;
                case "--smoke-support":
                    supports.Add(ParsePoint(
                        RequiredValue(arguments, ref index, "--smoke-support"),
                        "--smoke-support"));
                    break;
                case "--smoke-primary-support":
                    primarySupports.Add(ParsePoint(
                        RequiredValue(arguments, ref index, "--smoke-primary-support"),
                        "--smoke-primary-support"));
                    break;
                case "--smoke-backup-support":
                    backupSupports.Add(ParsePoint(
                        RequiredValue(arguments, ref index, "--smoke-backup-support"),
                        "--smoke-backup-support"));
                    break;
                case "--smoke-plant":
                    if (plant.HasValue)
                    {
                        throw new ArgumentException("--smoke-plant may be provided only once.");
                    }
                    plant = ParsePoint(
                        RequiredValue(arguments, ref index, "--smoke-plant"),
                        "--smoke-plant");
                    break;
                case "--smoke-plant-support":
                    plantSupports.Add(ParsePoint(
                        RequiredValue(arguments, ref index, "--smoke-plant-support"),
                        "--smoke-plant-support"));
                    break;
                default:
                    throw new ArgumentException(
                        $"Unknown product game argument: {arguments[index]}");
            }
        }

        sessionId ??= "LOCAL-FACTORY-CAPACITY";
        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("--session-id cannot be empty.");
        }

        if (smoke)
        {
            if (substations.Count != 2)
            {
                throw new ArgumentException(
                    "--smoke requires exactly two --smoke-substation x,y values.");
            }
            if (supports.Count == 0)
            {
                throw new ArgumentException(
                    "--smoke requires at least one --smoke-support x,y value.");
            }
            if (primarySupports.Count == 0)
            {
                throw new ArgumentException(
                    "--smoke requires at least one --smoke-primary-support x,y value.");
            }
            if (backupSupports.Count == 0)
            {
                throw new ArgumentException(
                    "--smoke requires at least one --smoke-backup-support x,y value.");
            }
            if (!plant.HasValue)
            {
                throw new ArgumentException(
                    "--smoke requires exactly one --smoke-plant x,y value.");
            }
            if (plantSupports.Count == 0)
            {
                throw new ArgumentException(
                    "--smoke requires at least one --smoke-plant-support x,y value.");
            }
        }
        else if (substations.Count != 0 ||
                 supports.Count != 0 ||
                 primarySupports.Count != 0 ||
                 backupSupports.Count != 0 ||
                 plant.HasValue ||
                 plantSupports.Count != 0)
        {
            throw new ArgumentException(
                "Smoke coordinates are valid only when --smoke is present.");
        }

        diagnosticPath ??= ProjectSettings.GlobalizePath(
            $"user://product-factory-local-{System.Environment.ProcessId}.jsonl");
        return new ProductLaunchOptions(
            sessionId,
            Path.GetFullPath(diagnosticPath),
            smoke,
            substations.AsReadOnly(),
            supports.AsReadOnly(),
            primarySupports.AsReadOnly(),
            backupSupports.AsReadOnly(),
            plant,
            plantSupports.AsReadOnly());
    }

    private static FirstLightGridPoint ParsePoint(string value, string option)
    {
        string[] fields = value.Split(',', StringSplitOptions.None);
        if (fields.Length != 2 ||
            !int.TryParse(
                fields[0],
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out int x) ||
            !int.TryParse(
                fields[1],
                NumberStyles.AllowLeadingSign,
                CultureInfo.InvariantCulture,
                out int y))
        {
            throw new ArgumentException($"{option} must be exactly x,y using two integers.");
        }
        return new FirstLightGridPoint(x, y);
    }

    private static string RequiredValue(
        IReadOnlyList<string> arguments,
        ref int index,
        string option)
    {
        if (index + 1 >= arguments.Count ||
            arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"{option} requires a value.");
        }
        index++;
        return arguments[index];
    }
}
