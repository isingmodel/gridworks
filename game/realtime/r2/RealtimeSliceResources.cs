using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Gridworks.Core.Release.V2;
using Gridworks.Core.Release.V3;

namespace Gridworks.Game.Realtime.R2;

internal enum RealtimeSliceSourceRoute
{
    TechnicalCheckpointFixture,
    ReleaseFirstLight,
    ReleaseTutorialThroughSecondSource,
}

internal sealed record RealtimeSliceData(
    CommercialWorldDefinition BaseWorld,
    CommercialCampaignDefinition BaseCampaign,
    RealtimeWorldDefinition World,
    RealtimeCampaignDefinition Campaign,
    string BaseWorldSha256,
    string BaseCampaignSha256,
    string WorldSha256,
    string CampaignSha256,
    RealtimeSliceSourceRoute SourceRoute,
    string? CampaignOverlaySha256,
    string? FullComposedCampaignSha256);

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
    internal const string ReleaseWorldResource =
        "Gridworks.Game.EmbeddedData.release-world-v3.json";
    internal const string ReleaseCampaignOverlayResource =
        "Gridworks.Game.EmbeddedData.release-campaign-v3.json";

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
            Sha256(campaignBytes),
            RealtimeSliceSourceRoute.TechnicalCheckpointFixture,
            null,
            null);
    }

    internal static RealtimeSliceData LoadReleaseFirstLight(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        byte[] baseWorldBytes = Read(assembly, BaseWorldResource);
        byte[] baseCampaignBytes = Read(assembly, BaseCampaignResource);
        byte[] worldBytes = Read(assembly, ReleaseWorldResource);
        byte[] campaignOverlayBytes = Read(
            assembly,
            ReleaseCampaignOverlayResource);

        CommercialWorldDefinition baseWorld = CommercialWorldLoader.Load(baseWorldBytes);
        RealtimeWorldDefinition world = RealtimeWorldLoader.Load(worldBytes, baseWorld);
        RealtimeCampaignOverlayLoadResult loaded =
            RealtimeCampaignOverlayLoader.LoadFirstLight(
                baseCampaignBytes,
                campaignOverlayBytes,
                world);
        RealtimeCampaignOverlaySourceIdentity identity = loaded.SourceIdentity;
        if (!string.Equals(
                identity.BaseCampaignSha256,
                Sha256(baseCampaignBytes),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Release FIRST_LIGHT base-campaign source identity drifted.");
        }

        return new RealtimeSliceData(
            baseWorld,
            loaded.Campaign.Content,
            world,
            loaded.Campaign,
            Sha256(baseWorldBytes),
            identity.BaseCampaignSha256,
            Sha256(worldBytes),
            identity.SelectedComposedCampaignSha256,
            RealtimeSliceSourceRoute.ReleaseFirstLight,
            identity.RealtimeOverlaySha256,
            identity.FullComposedCampaignSha256);
    }

    internal static RealtimeSliceData LoadReleaseTutorialThroughSecondSource(
        Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        byte[] baseWorldBytes = Read(assembly, BaseWorldResource);
        byte[] baseCampaignBytes = Read(assembly, BaseCampaignResource);
        byte[] worldBytes = Read(assembly, ReleaseWorldResource);
        byte[] campaignOverlayBytes = Read(
            assembly,
            ReleaseCampaignOverlayResource);

        CommercialWorldDefinition baseWorld = CommercialWorldLoader.Load(baseWorldBytes);
        RealtimeWorldDefinition world = RealtimeWorldLoader.Load(worldBytes, baseWorld);
        RealtimeCampaignOverlayLoadResult loaded =
            RealtimeCampaignOverlayLoader.LoadPrefix(
                baseCampaignBytes,
                campaignOverlayBytes,
                world,
                chapterCount: 3);
        RealtimeCampaignOverlaySourceIdentity identity = loaded.SourceIdentity;
        if (!string.Equals(
                identity.BaseCampaignSha256,
                Sha256(baseCampaignBytes),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Release tutorial base-campaign source identity drifted.");
        }

        return new RealtimeSliceData(
            baseWorld,
            loaded.Campaign.Content,
            world,
            loaded.Campaign,
            Sha256(baseWorldBytes),
            identity.BaseCampaignSha256,
            Sha256(worldBytes),
            identity.SelectedComposedCampaignSha256,
            RealtimeSliceSourceRoute.ReleaseTutorialThroughSecondSource,
            identity.RealtimeOverlaySha256,
            identity.FullComposedCampaignSha256);
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
