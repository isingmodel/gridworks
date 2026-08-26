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
    string CampaignSha256,
    RealtimeNativeRoute? NativeRoute,
    string? CampaignOverlaySha256,
    string? FullComposedCampaignSha256)
{
    internal RealtimeCampaignSourceIdentity RequireSaveSourceIdentity()
    {
        RealtimeNativeRoute route = RealtimeNativeRouteCatalog.RequireSupported(
            NativeRoute ?? throw new InvalidOperationException(
                "Only a canonical native route may be saved."));
        return new RealtimeCampaignSourceIdentity(
            route.LaunchArgument,
            BaseWorldSha256,
            BaseCampaignSha256,
            WorldSha256,
            CampaignOverlaySha256 ?? throw new InvalidOperationException(
                "A native save requires the realtime overlay identity."),
            CampaignSha256,
            FullComposedCampaignSha256 ?? throw new InvalidOperationException(
                "A native save requires the full composed campaign identity."));
    }
}

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

    internal static RealtimeSliceData LoadTechnicalFixture(Assembly assembly)
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
            null,
            null,
            null);
    }

    internal static RealtimeSliceData LoadNativeRelease(
        Assembly assembly,
        RealtimeNativeRoute route)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        route = RealtimeNativeRouteCatalog.RequireSupported(route);
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
                route.SelectedChapterCount);
        RealtimeCampaignOverlaySourceIdentity identity = loaded.SourceIdentity;
        if (!string.Equals(
                identity.BaseCampaignSha256,
                Sha256(baseCampaignBytes),
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Release '{route.EndChapterId}' base-campaign source identity drifted.");
        }
        if (identity.SelectedChapterCount != route.SelectedChapterCount ||
            loaded.Campaign.Chapters.Count != route.SelectedChapterCount ||
            !string.Equals(
                loaded.Campaign.Chapters[^1].Content.ChapterId,
                route.EndChapterId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Release route '{route.LaunchArgument}' selected an unexpected prefix.");
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
            route,
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
