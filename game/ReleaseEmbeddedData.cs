using System;
using System.IO;
using System.Reflection;

namespace Gridworks.Game;

internal static class ReleaseEmbeddedData
{
    private const string WorldResource =
        "Gridworks.Game.EmbeddedData.release-world-v1.json";
    private const string CampaignResource =
        "Gridworks.Game.EmbeddedData.release-campaign-v1.json";

    public static byte[] ReadWorldBytes() => ReadRequired(WorldResource);

    public static byte[] ReadCampaignBytes() => ReadRequired(CampaignResource);

    private static byte[] ReadRequired(string resourceName)
    {
        using Stream stream = Assembly.GetExecutingAssembly()
            .GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException("출시판 데이터를 열 수 없습니다.");
        using var memory = new MemoryStream();
        stream.CopyTo(memory);
        return memory.ToArray();
    }
}
