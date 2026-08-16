using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Gridworks.Core;

public static class SnapshotJson
{
    public static string Serialize(PublicSnapshot snapshot) =>
        Encoding.UTF8.GetString(SerializeToUtf8Bytes(snapshot));

    public static byte[] SerializeToUtf8Bytes(PublicSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        using MemoryStream stream = new();
        using (Utf8JsonWriter writer = new(stream, new JsonWriterOptions
               {
                   Indented = false,
                   SkipValidation = false,
               }))
        {
            WriteSnapshot(writer, snapshot);
        }
        return stream.ToArray();
    }

    public static string Sha256Hex(PublicSnapshot snapshot) =>
        Convert.ToHexString(SHA256.HashData(SerializeToUtf8Bytes(snapshot))).ToLowerInvariant();

    private static void WriteSnapshot(Utf8JsonWriter writer, PublicSnapshot snapshot)
    {
        writer.WriteStartObject();
        writer.WriteNumber("minute", snapshot.Minute);
        writer.WriteNumber("cash", snapshot.Cash);
        writer.WriteString("townProjectState", ProjectStateValue(snapshot.TownProjectState));
        writer.WriteString("corridorProjectState", ProjectStateValue(snapshot.CorridorProjectState));
        if (snapshot.SelectedCorridor.HasValue)
        {
            writer.WriteString("selectedCorridor", CorridorValue(snapshot.SelectedCorridor.Value));
        }
        else
        {
            writer.WriteNull("selectedCorridor");
        }
        WriteStringArray(writer, "commissionedEdgeIds", snapshot.CommissionedEdgeIds);
        WriteStringArray(writer, "eventRemovedEdgeIds", snapshot.EventRemovedEdgeIds);
        WriteStringArray(writer, "activeLoadIds", snapshot.ActiveLoadIds);

        writer.WritePropertyName("utilityPathByLoad");
        writer.WriteStartObject();
        foreach ((string loadId, string? pathId) in snapshot.UtilityPathByLoad
                     .OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            if (pathId is null)
            {
                writer.WriteNull(loadId);
            }
            else
            {
                writer.WriteString(loadId, pathId);
            }
        }
        writer.WriteEndObject();

        writer.WriteString(
            "hospitalInternalStage",
            InternalPowerStageValue(snapshot.HospitalInternalStage));
        writer.WriteNumber(
            "hospitalInternalRemainingKwMinute",
            snapshot.HospitalInternalRemainingKwMinute);
        writer.WritePropertyName("interval");
        WriteSettlement(writer, snapshot.Interval);
        writer.WritePropertyName("cumulative");
        WriteSettlement(writer, snapshot.Cumulative);
        writer.WriteBoolean("isComplete", snapshot.IsComplete);
        writer.WriteEndObject();
    }

    private static void WriteSettlement(Utf8JsonWriter writer, Settlement settlement)
    {
        writer.WriteStartObject();
        writer.WriteNumber("revenueCashUnit", settlement.RevenueCashUnit);
        writer.WriteNumber("gasCostCashUnit", settlement.GasCostCashUnit);
        writer.WriteNumber("compensationCashUnit", settlement.CompensationCashUnit);
        writer.WriteNumber("lostSalesCashUnit", settlement.LostSalesCashUnit);
        WriteLongMap(
            writer,
            "utilityDeliveredKwMinuteByLoad",
            settlement.UtilityDeliveredKwMinuteByLoad);
        WriteLongMap(
            writer,
            "utilityUnservedKwMinuteByLoad",
            settlement.UtilityUnservedKwMinuteByLoad);
        writer.WriteNumber("gasInjectionKwMinute", settlement.GasInjectionKwMinute);
        writer.WriteNumber(
            "hospitalInternalUsedKwMinute",
            settlement.HospitalInternalUsedKwMinute);
        writer.WriteNumber(
            "hospitalP0UnservedKwMinute",
            settlement.HospitalP0UnservedKwMinute);
        writer.WriteEndObject();
    }

    private static void WriteStringArray(
        Utf8JsonWriter writer,
        string propertyName,
        IEnumerable<string> values)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartArray();
        foreach (string value in values.OrderBy(value => value, StringComparer.Ordinal))
        {
            writer.WriteStringValue(value);
        }
        writer.WriteEndArray();
    }

    private static void WriteLongMap(
        Utf8JsonWriter writer,
        string propertyName,
        IReadOnlyDictionary<string, long> values)
    {
        writer.WritePropertyName(propertyName);
        writer.WriteStartObject();
        foreach ((string key, long value) in values.OrderBy(
                     pair => pair.Key,
                     StringComparer.Ordinal))
        {
            writer.WriteNumber(key, value);
        }
        writer.WriteEndObject();
    }

    private static string ProjectStateValue(ProjectState state) => state switch
    {
        ProjectState.NotOrdered => "not_ordered",
        ProjectState.Building => "building",
        ProjectState.Commissioned => "commissioned",
        _ => throw new ArgumentOutOfRangeException(nameof(state), state, null),
    };

    private static string CorridorValue(CorridorDesign design) => design switch
    {
        CorridorDesign.RiverParallel => "RIVER_PARALLEL",
        CorridorDesign.NorthDetour => "NORTH_DETOUR",
        _ => throw new ArgumentOutOfRangeException(nameof(design), design, null),
    };

    private static string InternalPowerStageValue(InternalPowerStage stage) => stage switch
    {
        InternalPowerStage.None => "none",
        InternalPowerStage.Ups => "ups",
        InternalPowerStage.Diesel => "diesel",
        _ => throw new ArgumentOutOfRangeException(nameof(stage), stage, null),
    };
}
