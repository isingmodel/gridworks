using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;

namespace Gridworks.Game.Realtime.R2;

internal sealed record RealtimeSliceData(
    CommercialWorldDefinition BaseWorld,
    CommercialCampaignDefinition BaseCampaign,
    RealtimeWorldDefinition World,
    RealtimeCampaignDefinition Campaign,
    string BaseWorldSha256,
    string BaseCampaignSha256,
    string WorldSha256,
    string CampaignSha256);

internal static class RealtimeSliceResources
{
    internal const string BaseWorldResource =
        "Gridworks.Game.EmbeddedData.release-world-v2.json";
    internal const string BaseCampaignResource =
        "Gridworks.Game.EmbeddedData.release-campaign-v2.json";
    internal const string WorldResource =
        "Gridworks.Game.EmbeddedData.stage-r1-world-realtime-v3.json";
    internal const string CampaignResource =
        "Gridworks.Game.EmbeddedData.stage-r1-first-light-realtime-v3.json";

    internal static RealtimeSliceData Load(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        byte[] baseWorldBytes = Read(assembly, BaseWorldResource);
        byte[] baseCampaignBytes = Read(assembly, BaseCampaignResource);
        byte[] worldBytes = Read(assembly, WorldResource);
        byte[] campaignBytes = Read(assembly, CampaignResource);

        CommercialWorldDefinition baseWorld = CommercialWorldLoader.Load(baseWorldBytes);
        CommercialCampaignDefinition baseCampaign = CommercialCampaignLoader.Load(
            baseCampaignBytes,
            baseWorld);
        RealtimeWorldDefinition world = RealtimeWorldLoader.Load(worldBytes, baseWorld);
        RealtimeCampaignDefinition campaign = RealtimeCampaignLoader.Load(
            campaignBytes,
            baseCampaign,
            world);
        return new RealtimeSliceData(
            baseWorld,
            baseCampaign,
            world,
            campaign,
            Sha256(baseWorldBytes),
            Sha256(baseCampaignBytes),
            Sha256(worldBytes),
            Sha256(campaignBytes));
    }

    private static byte[] Read(Assembly assembly, string resourceName)
    {
        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Required realtime slice resource is unavailable: {resourceName}");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }

    private static string Sha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
}
