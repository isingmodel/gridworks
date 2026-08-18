using System;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using Gridworks.Core.Release.V2;

namespace Gridworks.Game;

internal sealed record CommercialProductData(
    CommercialWorldDefinition World,
    CommercialCoreSliceDefinition Slice,
    string WorldSha256,
    string SliceSha256);

internal static class CommercialProductResources
{
    public const string WorldResource =
        "Gridworks.Game.EmbeddedData.release-world-v2.json";
    public const string CoreSliceResource =
        "Gridworks.Game.EmbeddedData.commercial-core-slice-v1.json";

    public static CommercialProductData Load(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        byte[] worldBytes = Read(assembly, WorldResource);
        byte[] sliceBytes = Read(assembly, CoreSliceResource);
        CommercialWorldDefinition world = CommercialWorldLoader.Load(worldBytes);
        CommercialCoreSliceDefinition slice = CommercialCoreSliceLoader.Load(
            sliceBytes,
            world);
        return new CommercialProductData(
            world,
            slice,
            Sha256(worldBytes),
            Sha256(sliceBytes));
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
