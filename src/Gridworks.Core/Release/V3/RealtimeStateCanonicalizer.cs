using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;
using Gridworks.Core.Release.V2;

namespace Gridworks.Core.Release.V3;

public static class RealtimeStateCanonicalizer
{
    public const string CanonicalSchemaVersion =
        "gridworks.realtime.canonical-state.v1";

    private const string WorldAuthoritySchemaVersion =
        "gridworks.realtime.world-authority.v1";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false,
        Converters =
        {
            new JsonStringEnumConverter(JsonNamingPolicy.CamelCase, allowIntegerValues: false),
        },
    };

    public static RealtimeStateAuthority AuthorityFor(
        RealtimeCampaignDefinition campaign,
        RealtimeWorldDefinition world)
    {
        ArgumentNullException.ThrowIfNull(campaign);
        ArgumentNullException.ThrowIfNull(world);
        return new RealtimeStateAuthority(
            campaign.SchemaVersion,
            campaign.CampaignId,
            CanonicalValueSha256(campaign),
            world.SchemaVersion,
            world.WorldId,
            CanonicalValueSha256(CanonicalWorldFor(world)));
    }

    public static byte[] CanonicalUtf8(RealtimeCampaignSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ValidateAuthority(snapshot.Authority);
        return JsonSerializer.SerializeToUtf8Bytes(
            new CanonicalStateEnvelope(
                CanonicalSchemaVersion,
                NormalizeSnapshot(snapshot)),
            Options);
    }

    public static string Sha256(RealtimeCampaignSnapshot snapshot) =>
        Convert.ToHexString(SHA256.HashData(CanonicalUtf8(snapshot))).ToLowerInvariant();

    /// <summary>
    /// Canonical equality boundary for snapshots. Use this instead of synthesized
    /// record equality when nested V2 contracts contain IReadOnlyList properties.
    /// </summary>
    public static bool StructuralEquals(
        RealtimeCampaignSnapshot? left,
        RealtimeCampaignSnapshot? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }
        if (left is null || right is null)
        {
            return false;
        }
        return CryptographicOperations.FixedTimeEquals(
            CanonicalUtf8(left),
            CanonicalUtf8(right));
    }

    /// <summary>
    /// Hash-code companion to <see cref="StructuralEquals(RealtimeCampaignSnapshot?,RealtimeCampaignSnapshot?)"/>.
    /// This is for in-process equality collections; canonical persistence uses Sha256.
    /// </summary>
    public static int StructuralHashCode(RealtimeCampaignSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        byte[] digest = SHA256.HashData(CanonicalUtf8(snapshot));
        return BinaryPrimitives.ReadInt32BigEndian(digest);
    }

    /// <summary>
    /// Central structural boundary for other serializable R1 value contracts, including
    /// values that contain nested list-bearing V2 records.
    /// </summary>
    public static bool StructuralValueEquals<T>(T? left, T? right)
    {
        if (ReferenceEquals(left, right))
        {
            return true;
        }
        if (left is null || right is null)
        {
            return false;
        }
        return CryptographicOperations.FixedTimeEquals(
            CanonicalValueUtf8(left),
            CanonicalValueUtf8(right));
    }

    public static int StructuralValueHashCode<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        byte[] digest = SHA256.HashData(CanonicalValueUtf8(value));
        return BinaryPrimitives.ReadInt32BigEndian(digest);
    }

    public static string CanonicalValueSha256<T>(T value)
    {
        ArgumentNullException.ThrowIfNull(value);
        return Convert.ToHexString(
                SHA256.HashData(CanonicalValueUtf8(value)))
            .ToLowerInvariant();
    }

    private static byte[] CanonicalValueUtf8<T>(T value) =>
        JsonSerializer.SerializeToUtf8Bytes(value, Options);

    private static RealtimeCampaignSnapshot NormalizeSnapshot(
        RealtimeCampaignSnapshot snapshot)
    {
        // These spatial collections are ID-keyed simulation facts. The V2 validator
        // accepts equivalent permutations and runtime lookup/output order is stable, so
        // keep incidental source ordering out of future-equivalence hashes. Terrain order
        // intentionally remains authored: overlapping terrain kinds determine the typed
        // first placement rejection. Schedules, polygon point order, transitions, and
        // outcome sequences likewise remain untouched because their order is meaningful.
        var world = snapshot.Construction.World;
        var construction = snapshot.Construction with
        {
            World = world with
            {
                NodeClasses = world.NodeClasses
                    .OrderBy(item => item.ClassId, StringComparer.Ordinal)
                    .ToArray(),
                LineClasses = world.LineClasses
                    .OrderBy(item => item.ClassId, StringComparer.Ordinal)
                    .ToArray(),
                RiskAreas = world.RiskAreas
                    .OrderBy(item => item.RiskAreaId, StringComparer.Ordinal)
                    .ToArray(),
                Nodes = world.Nodes
                    .OrderBy(item => item.NodeId, StringComparer.Ordinal)
                    .ToArray(),
                Edges = world.Edges
                    .OrderBy(item => item.EdgeId, StringComparer.Ordinal)
                    .ToArray(),
            },
        };
        return snapshot with { Construction = construction };
    }

    private static CanonicalRealtimeWorldAuthority CanonicalWorldFor(
        RealtimeWorldDefinition world)
    {
        CommercialWorldDefinition network = world.Network;
        var canonicalNetwork = new CanonicalCommercialWorldAuthority(
            network.SchemaVersion,
            network.WorldId,
            network.DisplayName,
            network.UnitsPerDesignUnit,
            network.Bounds,
            network.InitialCashUnit,
            network.NodeClasses
                .OrderBy(item => item.ClassId, StringComparer.Ordinal)
                .ToArray(),
            network.LineClasses
                .OrderBy(item => item.ClassId, StringComparer.Ordinal)
                .ToArray(),
            network.Terrain,
            network.RiskAreas
                .OrderBy(item => item.RiskAreaId, StringComparer.Ordinal)
                .ToArray(),
            network.Nodes
                .OrderBy(item => item.NodeId, StringComparer.Ordinal)
                .ToArray(),
            network.Edges
                .OrderBy(item => item.EdgeId, StringComparer.Ordinal)
                .ToArray(),
            network.Sources
                .OrderBy(item => item.SourceId, StringComparer.Ordinal)
                .ToArray(),
            network.Loads
                .OrderBy(item => item.LoadId, StringComparer.Ordinal)
                .ToArray());

        return new CanonicalRealtimeWorldAuthority(
            WorldAuthoritySchemaVersion,
            world.SchemaVersion,
            world.WorldId,
            canonicalNetwork,
            world.ThermalClasses
                .OrderBy(item => item.AssetKind)
                .ThenBy(item => item.ClassId, StringComparer.Ordinal)
                .ToArray());
    }

    private static void ValidateAuthority(RealtimeStateAuthority authority)
    {
        ArgumentNullException.ThrowIfNull(authority);
        RequireText(authority.CampaignSchemaVersion, nameof(authority.CampaignSchemaVersion));
        RequireText(authority.CampaignId, nameof(authority.CampaignId));
        RequireSha256(
            authority.CampaignDefinitionSha256,
            nameof(authority.CampaignDefinitionSha256));
        RequireText(authority.WorldSchemaVersion, nameof(authority.WorldSchemaVersion));
        RequireText(authority.WorldId, nameof(authority.WorldId));
        RequireSha256(
            authority.WorldDefinitionSha256,
            nameof(authority.WorldDefinitionSha256));
    }

    private static void RequireText(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || !string.Equals(value, value.Trim(),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"Canonical authority field '{field}' must be nonblank and trimmed.",
                field);
        }
    }

    private static void RequireSha256(string value, string field)
    {
        if (value is null || value.Length != 64 || value.Any(character =>
                character is not (>= '0' and <= '9') and
                not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                $"Canonical authority field '{field}' must be lowercase SHA-256 hex.",
                field);
        }
    }

    private sealed record CanonicalStateEnvelope(
        string CanonicalSchemaVersion,
        RealtimeCampaignSnapshot Snapshot);

    /// <summary>
    /// Authority serialization is independent of loader order for every validated,
    /// ID-keyed world collection. Ordered values inside an item, such as polygon point
    /// winding, remain authored semantic data and are intentionally not reordered.
    /// </summary>
    private sealed record CanonicalRealtimeWorldAuthority(
        string AuthoritySchemaVersion,
        string SchemaVersion,
        string WorldId,
        CanonicalCommercialWorldAuthority Network,
        IReadOnlyList<RealtimeThermalClassDefinition> ThermalClasses);

    private sealed record CanonicalCommercialWorldAuthority(
        string SchemaVersion,
        string WorldId,
        string DisplayName,
        int UnitsPerDesignUnit,
        MapBounds Bounds,
        long InitialCashUnit,
        IReadOnlyList<CommercialNodeClassDefinition> NodeClasses,
        IReadOnlyList<CommercialLineClassDefinition> LineClasses,
        IReadOnlyList<TerrainPolygonDefinition> Terrain,
        IReadOnlyList<SpatialRiskAreaDefinition> RiskAreas,
        IReadOnlyList<SpatialNodeDefinition> Nodes,
        IReadOnlyList<SpatialEdgeDefinition> Edges,
        IReadOnlyList<CommercialSourceDefinition> Sources,
        IReadOnlyList<CommercialLoadDefinition> Loads);
}
