using System;
using System.IO;
using System.Reflection;
using Gridworks.Core.Product;

namespace Gridworks.Game;

public static class ProductEmbeddedData
{
    private const string CampaignResourceName =
        "Gridworks.Game.EmbeddedData.product-campaign-v1.json";
    private const string HeatwaveResourceName =
        "Gridworks.Game.EmbeddedData.product-heatwave-v1.json";

    public static byte[] ReadCampaignBytes() => ReadRequiredResource(CampaignResourceName);

    public static byte[] ReadHeatwaveBytes() => ReadRequiredResource(HeatwaveResourceName);

    public static string ComputeSha256(ReadOnlySpan<byte> content) =>
        ProductContentHash.ComputeSha256(content);

    private static byte[] ReadRequiredResource(string resourceName)
    {
        Assembly assembly = typeof(ProductEmbeddedData).Assembly;
        using Stream stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException(
                $"Required embedded product data '{resourceName}' is missing.");
        using MemoryStream bytes = new();
        stream.CopyTo(bytes);
        return bytes.ToArray();
    }
}
