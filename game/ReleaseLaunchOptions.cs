using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using Gridworks.Core.Release;

namespace Gridworks.Game;

internal enum ReleaseCampaignSmokeLeg
{
    None,
    Save,
    Continue,
}

internal sealed record ReleaseLaunchOptions(
    bool Smoke,
    string SessionId,
    ReleasePoint? SmokeSubstation,
    ReleasePoint? SmokeLineStart,
    IReadOnlyList<ReleasePoint> SmokeLinePoints,
    ReleasePoint? SmokeLineEnd,
    ReleaseCampaignSmokeLeg CampaignSmokeLeg,
    string? StorageDirectory)
{
    public bool Automated => Smoke || CampaignSmokeLeg != ReleaseCampaignSmokeLeg.None;

    public static ReleaseLaunchOptions Parse(IReadOnlyList<string> args)
    {
        bool smoke = false;
        string sessionId = "release-local";
        ReleasePoint? substation = null;
        ReleasePoint? lineStart = null;
        ReleasePoint? lineEnd = null;
        var linePoints = new List<ReleasePoint>();
        ReleaseCampaignSmokeLeg campaignSmokeLeg = ReleaseCampaignSmokeLeg.None;
        string? storageDirectory = null;

        for (int index = 0; index < args.Count; index++)
        {
            string arg = args[index];
            switch (arg)
            {
                case "--release-smoke": smoke = true; break;
                case "--session-id": sessionId = Value(args, ref index, arg); break;
                case "--smoke-substation": substation = Point(Value(args, ref index, arg), arg); break;
                case "--smoke-line-start": lineStart = Point(Value(args, ref index, arg), arg); break;
                case "--smoke-line-point": linePoints.Add(Point(Value(args, ref index, arg), arg)); break;
                case "--smoke-line-end": lineEnd = Point(Value(args, ref index, arg), arg); break;
                case "--release-campaign-smoke":
                    campaignSmokeLeg = Value(args, ref index, arg) switch
                    {
                        "save" => ReleaseCampaignSmokeLeg.Save,
                        "continue" => ReleaseCampaignSmokeLeg.Continue,
                        _ => throw new ArgumentException(
                            "출시판 캠페인 확인 단계는 save 또는 continue여야 합니다."),
                    };
                    break;
                case "--storage-directory":
                    storageDirectory = Value(args, ref index, arg);
                    break;
                default: throw new ArgumentException($"지원하지 않는 출시판 실행 인자입니다: {arg}");
            }
        }

        if (smoke && (substation is null || lineStart is null || lineEnd is null || linePoints.Count == 0))
        {
            throw new ArgumentException("출시판 smoke에는 변전소, 선로 시작·중간·끝 좌표가 모두 필요합니다.");
        }
        if (!smoke && (substation is not null || lineStart is not null || lineEnd is not null || linePoints.Count != 0))
        {
            throw new ArgumentException("smoke 좌표는 --release-smoke와 함께 사용하세요.");
        }

        if (smoke && campaignSmokeLeg != ReleaseCampaignSmokeLeg.None)
        {
            throw new ArgumentException("두 출시판 확인 흐름을 한 실행에서 함께 사용할 수 없습니다.");
        }
        if (campaignSmokeLeg != ReleaseCampaignSmokeLeg.None)
        {
            if (string.IsNullOrWhiteSpace(storageDirectory))
            {
                throw new ArgumentException("캠페인 저장 확인에는 저장 폴더가 필요합니다.");
            }
            storageDirectory = Path.GetFullPath(storageDirectory);
        }
        else if (storageDirectory is not null)
        {
            throw new ArgumentException("저장 폴더는 캠페인 저장 확인에서만 지정할 수 있습니다.");
        }

        return new ReleaseLaunchOptions(
            smoke,
            sessionId,
            substation,
            lineStart,
            linePoints.ToArray(),
            lineEnd,
            campaignSmokeLeg,
            storageDirectory);
    }

    private static string Value(IReadOnlyList<string> args, ref int index, string option)
    {
        index++;
        if (index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
        {
            throw new ArgumentException($"{option} 뒤에 값이 필요합니다.");
        }
        return args[index];
    }

    private static ReleasePoint Point(string value, string option)
    {
        string[] parts = value.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length != 2 ||
            !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out int x) ||
            !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int y))
        {
            throw new ArgumentException($"{option} 좌표는 x,y 형식의 정수여야 합니다.");
        }
        return new ReleasePoint(x, y);
    }
}
