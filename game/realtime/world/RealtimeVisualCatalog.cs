using System;
using System.Collections.Generic;
using Godot;

namespace Gridworks.Game.Realtime;

internal enum RealtimeVisualKind
{
    StandardPole,
    ReinforcedPole,
    SmallSubstation,
    LargeSubstation,
    SourceTerminal,
    LoadTerminal,
    Hospital,
    Waterworks,
    Factory,
    Residential,
}

internal sealed record RealtimeVisualSpec(
    RealtimeVisualKind Kind,
    string ResourcePath,
    Vector2 OperatingSize,
    Vector2 ConstructionSize,
    Vector2 GroundAnchor,
    Vector2 LabelAnchor,
    float SelectionRadius,
    Vector2 PhaseAnchorA,
    Vector2 PhaseAnchorB,
    Vector2 PhaseAnchorC);

internal static class RealtimeVisualCatalog
{
    private static readonly IReadOnlyDictionary<string, RealtimeVisualSpec> Specs =
        new Dictionary<string, RealtimeVisualSpec>(StringComparer.Ordinal)
        {
            ["STANDARD_POLE"] = Equipment(
                RealtimeVisualKind.StandardPole,
                "res://assets/realtime/equipment/standard_pole_v1.png",
                new Vector2(42, 76),
                26),
            ["REINFORCED_POLE"] = Equipment(
                RealtimeVisualKind.ReinforcedPole,
                "res://assets/realtime/equipment/reinforced_pole_v1.png",
                new Vector2(56, 92),
                32),
            ["SMALL_SUBSTATION"] = Compound(
                RealtimeVisualKind.SmallSubstation,
                "res://assets/realtime/equipment/small_substation_v1.png",
                new Vector2(116, 116),
                58),
            ["LARGE_SUBSTATION"] = Compound(
                RealtimeVisualKind.LargeSubstation,
                "res://assets/realtime/equipment/large_substation_v1.png",
                new Vector2(156, 156),
                78),
            ["SOURCE_TERMINAL"] = Compound(
                RealtimeVisualKind.SourceTerminal,
                "res://assets/realtime/equipment/source_terminal_v1.png",
                new Vector2(172, 172),
                82),
            ["LOAD_TERMINAL"] = new RealtimeVisualSpec(
                RealtimeVisualKind.LoadTerminal,
                string.Empty,
                new Vector2(36, 42),
                new Vector2(48, 56),
                new Vector2(0.5f, 0.78f),
                new Vector2(0.5f, 0f),
                24,
                new Vector2(0.28f, 0.26f),
                new Vector2(0.5f, 0.19f),
                new Vector2(0.72f, 0.26f)),
        };

    private static readonly IReadOnlyDictionary<RealtimeVisualKind, string> FacilityPaths =
        new Dictionary<RealtimeVisualKind, string>
        {
            [RealtimeVisualKind.Hospital] =
                "res://assets/realtime/facilities/hospital_v1.png",
            [RealtimeVisualKind.Waterworks] =
                "res://assets/realtime/facilities/waterworks_v1.png",
            [RealtimeVisualKind.Factory] =
                "res://assets/realtime/facilities/factory_v1.png",
            [RealtimeVisualKind.Residential] =
                "res://assets/realtime/facilities/residential_v1.png",
        };

    public static RealtimeVisualSpec Resolve(string classId) =>
        Specs.TryGetValue(classId, out RealtimeVisualSpec? spec)
            ? spec
            : throw new InvalidOperationException(
                $"No realtime visual spec exists for node class '{classId}'.");

    public static string FacilityResource(RealtimeVisualKind kind) =>
        FacilityPaths.TryGetValue(kind, out string? path)
            ? path
            : throw new InvalidOperationException($"No facility resource exists for {kind}.");

    public static RealtimeVisualKind FacilityFor(string displayName)
    {
        if (displayName.Contains("의료", StringComparison.Ordinal))
        {
            return RealtimeVisualKind.Hospital;
        }
        if (displayName.Contains("정수", StringComparison.Ordinal))
        {
            return RealtimeVisualKind.Waterworks;
        }
        if (displayName.Contains("산업", StringComparison.Ordinal) ||
            displayName.Contains("공장", StringComparison.Ordinal))
        {
            return RealtimeVisualKind.Factory;
        }
        return RealtimeVisualKind.Residential;
    }

    private static RealtimeVisualSpec Equipment(
        RealtimeVisualKind kind,
        string path,
        Vector2 operatingSize,
        float selectionRadius) => new(
            kind,
            path,
            operatingSize,
            operatingSize * 1.14f,
            new Vector2(0.5f, 0.88f),
            new Vector2(0.5f, 0.02f),
            selectionRadius,
            new Vector2(0.28f, 0.16f),
            new Vector2(0.5f, 0.12f),
            new Vector2(0.72f, 0.16f));

    private static RealtimeVisualSpec Compound(
        RealtimeVisualKind kind,
        string path,
        Vector2 operatingSize,
        float selectionRadius) => new(
            kind,
            path,
            operatingSize,
            operatingSize * 1.08f,
            new Vector2(0.5f, 0.72f),
            new Vector2(0.5f, 0.02f),
            selectionRadius,
            new Vector2(0.35f, 0.28f),
            new Vector2(0.5f, 0.22f),
            new Vector2(0.65f, 0.28f));
}
