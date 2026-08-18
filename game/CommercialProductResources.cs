using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Gridworks.Core.Release.V2;

namespace Gridworks.Game;

internal sealed record CommercialProductData(
    CommercialWorldDefinition World,
    CommercialCampaignDefinition Campaign,
    string WorldSha256,
    string CampaignSha256);

internal static class CommercialProductResources
{
    public const string WorldResource =
        "Gridworks.Game.EmbeddedData.release-world-v2.json";
    public const string CampaignResource =
        "Gridworks.Game.EmbeddedData.release-campaign-v2.json";

    public static CommercialProductData Load(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        byte[] worldBytes = Read(assembly, WorldResource);
        byte[] campaignBytes = Read(assembly, CampaignResource);
        CommercialWorldDefinition world = CommercialWorldLoader.Load(worldBytes);
        CommercialCampaignDefinition campaign = CommercialCampaignLoader.Load(
            campaignBytes,
            world);
        return new CommercialProductData(
            world,
            campaign,
            Sha256(worldBytes),
            Sha256(campaignBytes));
    }

    private static string Sha256(ReadOnlySpan<byte> bytes) =>
        Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

    private static byte[] Read(Assembly assembly, string resourceName)
    {
        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"필수 상용 게임 데이터를 열 수 없습니다: {resourceName}");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
