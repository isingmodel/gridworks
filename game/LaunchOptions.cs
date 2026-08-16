using System;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace Gridworks.Game;

internal sealed record LaunchOptions(string SessionId, string Variant, string DiagnosticPath, bool Smoke)
{
    public static LaunchOptions Parse(IReadOnlyList<string> arguments)
    {
        string? sessionId = null;
        string? variant = null;
        string? diagnosticPath = null;
        var smoke = false;

        for (var index = 0; index < arguments.Count; index++)
        {
            switch (arguments[index])
            {
                case "--session-id":
                    sessionId = RequiredValue(arguments, ref index, "--session-id");
                    break;
                case "--variant":
                    variant = RequiredValue(arguments, ref index, "--variant");
                    break;
                case "--diagnostic-log":
                    diagnosticPath = RequiredValue(arguments, ref index, "--diagnostic-log");
                    break;
                case "--smoke":
                    smoke = true;
                    break;
                default:
                    throw new ArgumentException($"Unknown game argument: {arguments[index]}");
            }
        }

        sessionId ??= "LOCAL";
        variant ??= "ab";
        if (variant is not ("ab" or "ba"))
        {
            throw new ArgumentException("--variant must be exactly 'ab' or 'ba'.");
        }

        if (string.IsNullOrWhiteSpace(sessionId))
        {
            throw new ArgumentException("--session-id cannot be empty.");
        }

        diagnosticPath ??= ProjectSettings.GlobalizePath("user://scope-0b-local.jsonl");
        diagnosticPath = Path.GetFullPath(diagnosticPath);
        return new LaunchOptions(sessionId, variant, diagnosticPath, smoke);
    }

    private static string RequiredValue(IReadOnlyList<string> arguments, ref int index, string option)
    {
        if (index + 1 >= arguments.Count || arguments[index + 1].StartsWith("--", StringComparison.Ordinal))
        {
            throw new ArgumentException($"{option} requires a value.");
        }

        index++;
        return arguments[index];
    }
}
