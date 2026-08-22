using System.Buffers.Binary;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Gridworks.Core.Release.V2;

namespace Gridworks.CommercialChecks;

internal static class Program
{
    public static int Main(string[] args)
    {
        try
        {
            if (args.Length == 1 && args[0] == "--story-manifest")
            {
                CommercialStoryPartHarness storyParts = LoadStoryPartHarness();
                WriteJson(Console.OpenStandardOutput(), storyParts.SerializeManifest());
                return 0;
            }
            if (args.Length == 2 && args[0] == "--story-part")
            {
                CommercialStoryPartHarness storyParts = LoadStoryPartHarness();
                WriteJson(
                    Console.OpenStandardOutput(),
                    storyParts.Serialize(storyParts.Select(args[1])));
                return 0;
            }
            if (args.Any(arg => arg is "--story-part" or "--story-manifest") ||
                (args.Length == 1 && args[0].StartsWith("--", StringComparison.Ordinal)))
            {
                throw new ArgumentException(
                    "usage: Gridworks.CommercialChecks [release-world-v2-json] | " +
                    "--story-part SELECTOR | --story-manifest");
            }

            string fixturePath = ResolveFixturePath(args);
            return new CommercialChecks(fixturePath).Run();
        }
        catch (CommercialStoryPartSelectionException exception)
        {
            WriteJson(
                Console.OpenStandardError(),
                CommercialStoryPartHarness.SerializeError(exception));
            return 2;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"FAIL startup: {exception.Message}");
            Console.Error.WriteLine(exception);
            return 1;
        }
    }

    private static CommercialStoryPartHarness LoadStoryPartHarness()
    {
        string worldPath = ResolveFixturePath(Array.Empty<string>());
        CommercialWorldDefinition world = CommercialWorldLoader.Load(
            File.ReadAllBytes(worldPath));
        string campaignPath = Path.Combine(
            Path.GetDirectoryName(worldPath)!,
            "release-campaign-v2.json");
        CommercialCampaignDefinition campaign = CommercialCampaignLoader.Load(
            File.ReadAllBytes(campaignPath),
            world);
        return new CommercialStoryPartHarness(campaign);
    }

    private static void WriteJson(Stream stream, byte[] json)
    {
        stream.Write(json);
        stream.WriteByte((byte)'\n');
        stream.Flush();
    }

    private static string ResolveFixturePath(string[] args)
    {
        if (args.Length > 1)
        {
            throw new ArgumentException(
                "usage: Gridworks.CommercialChecks [release-world-v2-json] | " +
                "--story-part SELECTOR | --story-manifest");
        }

        string path = args.Length == 1
            ? args[0]
            : Path.Combine(
                Environment.CurrentDirectory,
                "data",
                "release-world-v2.json");
        path = Path.GetFullPath(path);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("Commercial world v2 fixture not found.", path);
        }
        return path;
    }
}

internal sealed class CommercialChecks
{
    private const string SourceClassId = "CHECK_SOURCE";
    private const string LoadClassId = "CHECK_LOAD";
    private const string PoleClassId = "CHECK_POLE";
    private const string SubstationClassId = "CHECK_SUBSTATION";
    private const string LineClassId = "CHECK_LINE";

    private readonly byte[] _fixtureBytes;
    private readonly string _fixtureJson;
    private readonly SpatialWorldDefinition _fixture;
    private readonly byte[] _commercialBytes;
    private readonly string _commercialJson;
    private readonly CommercialWorldDefinition _commercialWorld;
    private readonly byte[] _coreBytes;
    private readonly string _coreJson;
    private readonly CommercialCoreSliceDefinition _coreSlice;
    private readonly byte[] _campaignBytes;
    private readonly string _campaignJson;
    private readonly CommercialCampaignDefinition _campaign;
    private readonly CommercialStoryPartHarness _storyParts;
    private readonly string _repositoryDirectory;
    private int _assertionCount;

    public CommercialChecks(string fixturePath)
    {
        _repositoryDirectory = Path.GetFullPath(Path.Combine(
            Path.GetDirectoryName(fixturePath)!,
            ".."));
        _commercialBytes = File.ReadAllBytes(fixturePath);
        _commercialJson = Encoding.UTF8.GetString(_commercialBytes);
        _commercialWorld = CommercialWorldLoader.Load(_commercialBytes);
        string corePath = Path.Combine(
            Path.GetDirectoryName(fixturePath)!,
            "commercial-core-slice-v1.json");
        _coreBytes = File.ReadAllBytes(corePath);
        _coreJson = Encoding.UTF8.GetString(_coreBytes);
        _coreSlice = CommercialCoreLoader.Load(_coreBytes, _commercialWorld);
        string campaignPath = Path.Combine(
            Path.GetDirectoryName(fixturePath)!,
            "release-campaign-v2.json");
        _campaignBytes = File.ReadAllBytes(campaignPath);
        _campaignJson = Encoding.UTF8.GetString(_campaignBytes);
        _campaign = CommercialCampaignLoader.Load(_campaignBytes, _commercialWorld);
        _storyParts = new CommercialStoryPartHarness(_campaign);
        string spatialFixturePath = Path.Combine(
            Path.GetDirectoryName(fixturePath)!,
            "commercial-free-placement-slice-v1.json");
        _fixtureBytes = File.ReadAllBytes(spatialFixturePath);
        _fixtureJson = Encoding.UTF8.GetString(_fixtureBytes);
        _fixture = SpatialWorldLoader.Load(_fixtureBytes);
    }

    public int Run()
    {
        (string Name, Action Body)[] suites =
        [
            ("strict-spatial-loader", CheckStrictSpatialLoader),
            ("integer-geometry-and-tangency", CheckIntegerGeometryAndTangency),
            ("node-placement-and-risk", CheckNodePlacementAndRisk),
            ("line-geometry-and-risk", CheckLineGeometryAndRisk),
            ("construction-lifecycle-quote-atomicity", CheckConstructionLifecycle),
            ("rejected-invariance-and-determinism", CheckRejectedInvarianceAndDeterminism),
            ("crossing-nonconnection-and-replay", CheckCrossingNonConnectionAndReplay),
            ("strict-commercial-world-loader", CheckStrictCommercialWorldLoader),
            ("thermal-boundaries-and-route-order", CheckThermalBoundariesAndRouteOrder),
            ("thermal-shared-permissions-and-bottleneck", CheckThermalSharedPermissionsAndBottleneck),
            ("thermal-protection-cooling-and-determinism", CheckThermalProtectionCoolingAndDeterminism),
            ("thermal-review-regressions", CheckThermalReviewRegressions),
            ("strict-commercial-core-loader", CheckStrictCommercialCoreLoader),
            ("commercial-core-flow-designs-and-facts", CheckCommercialCoreFlowDesignsAndFacts),
            ("commercial-core-choice-deadline-and-atomicity", CheckCommercialCoreChoiceDeadlineAndAtomicity),
            ("commercial-core-rollback-and-fresh-replay", CheckCommercialCoreRollbackAndFreshReplay),
            ("commercial-core-save-v3", CheckCommercialCoreSaveV3),
            ("commercial-settings-v3-migration-and-atomicity", CheckCommercialSettingsV3),
            ("strict-commercial-campaign-loader", CheckStrictCommercialCampaignLoader),
            ("commercial-authored-copy-contract", CheckCommercialAuthoredCopy),
            ("commercial-story-part-harness", CheckCommercialStoryPartHarness),
            ("commercial-campaign-first-four-carry-save", CheckCommercialCampaignFirstFourCarrySave),
            ("commercial-campaign-final-eight-epilogue", CheckCommercialCampaignFinalEightEpilogue),
            ("commercial-map-discrete-art-contract", CheckCommercialMapDiscreteArt),
        ];

        List<string> failures = [];
        foreach ((string name, Action body) in suites)
        {
            try
            {
                body();
                Console.WriteLine($"PASS {name}");
            }
            catch (Exception exception)
            {
                failures.Add($"{name}: {exception.Message}");
                Console.Error.WriteLine($"FAIL {name}: {exception.Message}");
            }
        }

        if (failures.Count != 0)
        {
            Console.Error.WriteLine(
                $"Gridworks Commercial checks: FAIL ({failures.Count}/{suites.Length} suites)");
            return 1;
        }

        Console.WriteLine(
            $"Gridworks Commercial checks: PASS ({suites.Length} suites, {_assertionCount} assertions)");
        return 0;
    }

    private void CheckCommercialMapDiscreteArt()
    {
        string gameDirectory = Path.Combine(_repositoryDirectory, "game");
        string artDirectory = Path.Combine(gameDirectory, "art", "commercial");
        string scene = string.Join(
            '\n',
            new[]
            {
                "CommercialMapView.tscn",
                "CommercialMain.tscn",
                "CommercialTaskPanel.tscn",
                "ReleaseTheme.tres",
            }.Select(fileName => File.ReadAllText(Path.Combine(gameDirectory, fileName))));
        string renderer = File.ReadAllText(Path.Combine(gameDirectory, "CommercialMapView.cs"));
        string transform = File.ReadAllText(Path.Combine(gameDirectory, "CommercialMapTransform.cs"));
        string timeline = File.ReadAllText(Path.Combine(gameDirectory, "CommercialEventTimeline.cs"));
        string promptRecordPath = Path.Combine(
            artDirectory,
            "commercial-map-assets-v1.prompts.md");
        string promptRecord = File.ReadAllText(promptRecordPath);

        (string Property, string RelativePath, bool RequiresAlpha)[] assets =
        [
            ("GroundAsphaltTile", "tiles/ground-asphalt-v1.png", false),
            ("GroundScrubTile", "tiles/ground-scrub-v1.png", false),
            ("GroundConcreteTile", "tiles/ground-concrete-v1.png", false),
            ("GroundGravelTile", "tiles/ground-gravel-v1.png", false),
            ("RiverWaterTile", "tiles/river-water-v1.png", false),
        ];

        Equal(5, assets.Length, "retained discrete terrain material count");
        foreach ((string property, string relativePath, bool requiresAlpha) in assets)
        {
            string filePath = Path.Combine(artDirectory, relativePath);
            Check(File.Exists(filePath), $"missing discrete art: {relativePath}");
            Check(File.Exists($"{filePath}.import"), $"missing Godot import: {relativePath}");
            Check(File.ReadAllText($"{filePath}.import").Contains(
                    "mipmaps/generate=true",
                    StringComparison.Ordinal),
                $"discrete art lacks downscale mipmaps: {relativePath}");
            string resourcePath = $"res://art/commercial/{relativePath}";
            Check(scene.Contains(resourcePath, StringComparison.Ordinal),
                $"scene does not bind {resourcePath}");
            Check(scene.Contains($"{property} = ExtResource", StringComparison.Ordinal),
                $"scene does not assign {property}");
            Check(promptRecord.Contains(relativePath, StringComparison.Ordinal),
                $"prompt record does not name {relativePath}");
            string hash = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(filePath)))
                .ToLowerInvariant();
            Check(promptRecord.Contains(hash, StringComparison.Ordinal),
                $"prompt record hash is stale for {relativePath}");

            PngInfo png = ReadPng(filePath, requiresAlpha);
            Check(png.Width >= 1024 && png.Height >= 1024,
                $"discrete art is below source resolution: {relativePath}");
            Equal((byte)8, png.BitDepth, $"8-bit PNG: {relativePath}");
            if (requiresAlpha)
            {
                Equal((byte)6, png.ColorType, $"RGBA object PNG: {relativePath}");
                Check(png.TransparentFraction >= 0.35d,
                    $"object lacks transparent isolation: {relativePath}");
                Check(png.CornerAlphas.All(alpha => alpha <= 1),
                    $"object corners are not transparent: {relativePath}");
            }
            else
            {
                Check(png.ColorType is 2 or 6,
                    $"tile PNG has unsupported color type: {relativePath}");
            }
        }

        // G.2's composite parcel/cluster checks remain below as a historical
        // record, but the active G.3 Step 1 gate is deliberately authoritative.
        // The helper returns runtime-derived truth rather than a compile-time
        // constant, so the archived branch remains type-checked but never counts.
        if (CheckG3AtomicStepOne(
                gameDirectory,
                artDirectory,
                scene,
                renderer,
                transform,
                timeline))
        {
            return;
        }

        string g3ResidentialPath = Path.Combine(
            artDirectory,
            "g3/objects/residential-cluster-a.png");
        string g3PromptRecord = File.ReadAllText(Path.Combine(
            artDirectory,
            "g3-assets.prompts.md"));
        Check(File.Exists(g3ResidentialPath), "missing G.3 residential cluster A");
        Check(scene.Contains(
                "res://art/commercial/g3/objects/residential-cluster-a.png",
                StringComparison.Ordinal) &&
            scene.Contains("ResidentialClusterASprite = ExtResource", StringComparison.Ordinal),
            "G.3 residential cluster A is not runtime-bound");
        string g3ResidentialHash = Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(g3ResidentialPath))).ToLowerInvariant();
        Check(g3PromptRecord.Contains(g3ResidentialHash, StringComparison.Ordinal),
            "G.3 residential cluster A provenance hash is stale");
        PngInfo g3Residential = ReadPng(g3ResidentialPath, true);
        Equal((byte)6, g3Residential.ColorType, "G.3 residential cluster A RGBA");
        Check(g3Residential.TransparentFraction >= 0.35d &&
            g3Residential.CornerAlphas.All(alpha => alpha <= 1),
            "G.3 residential cluster A lacks isolated transparent alpha");

        string g3IndustryPath = Path.Combine(
            artDirectory,
            "g3/objects/industrial-warehouse-a.png");
        Check(File.Exists(g3IndustryPath), "missing G.3 industrial warehouse A");
        Check(scene.Contains(
                "res://art/commercial/g3/objects/industrial-warehouse-a.png",
                StringComparison.Ordinal) &&
            scene.Contains("IndustrialWarehouseASprite = ExtResource", StringComparison.Ordinal),
            "G.3 industrial warehouse A is not runtime-bound");
        string g3IndustryHash = Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(g3IndustryPath))).ToLowerInvariant();
        Check(g3PromptRecord.Contains(g3IndustryHash, StringComparison.Ordinal),
            "G.3 industrial warehouse A provenance hash is stale");
        PngInfo g3Industry = ReadPng(g3IndustryPath, true);
        Equal((byte)6, g3Industry.ColorType, "G.3 industrial warehouse A RGBA");
        Check(g3Industry.TransparentFraction >= 0.35d &&
            g3Industry.CornerAlphas.All(alpha => alpha <= 1),
            "G.3 industrial warehouse A lacks isolated transparent alpha");

        string g3CityParcelPath = Path.Combine(
            artDirectory,
            "g3/tiles/dense-city-parcel-a.png");
        Check(File.Exists(g3CityParcelPath), "missing G.3 dense city parcel A");
        Check(scene.Contains(
                "res://art/commercial/g3/tiles/dense-city-parcel-a.png",
                StringComparison.Ordinal) &&
            scene.Contains("DenseCityParcelASprite = ExtResource", StringComparison.Ordinal),
            "G.3 dense city parcel A is not runtime-bound");
        string g3CityParcelHash = Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(g3CityParcelPath))).ToLowerInvariant();
        Check(g3PromptRecord.Contains(g3CityParcelHash, StringComparison.Ordinal),
            "G.3 dense city parcel A provenance hash is stale");
        PngInfo g3CityParcel = ReadPng(g3CityParcelPath, true);
        Equal((byte)6, g3CityParcel.ColorType, "G.3 dense city parcel A RGBA");
        Check(g3CityParcel.TransparentFraction >= 0.35d &&
            g3CityParcel.CornerAlphas.All(alpha => alpha <= 1),
            "G.3 dense city parcel A lacks isolated transparent alpha");

        string g3RubblePath = Path.Combine(
            artDirectory,
            "g3/objects/central-rubble-service-corridor-a.png");
        Check(File.Exists(g3RubblePath), "missing G.3 central rubble corridor A");
        Check(File.Exists($"{g3RubblePath}.import") &&
            File.ReadAllText($"{g3RubblePath}.import").Contains(
                "mipmaps/generate=true",
                StringComparison.Ordinal),
            "G.3 central rubble corridor A lacks a mipmapped Godot import");
        Check(scene.Contains(
                "res://art/commercial/g3/objects/central-rubble-service-corridor-a.png",
                StringComparison.Ordinal) &&
            scene.Contains("CentralRubbleServiceCorridorASprite = ExtResource", StringComparison.Ordinal) &&
            renderer.Contains("DrawCentralRubbleCorridor", StringComparison.Ordinal),
            "G.3 central rubble corridor A is not individually runtime-bound");
        string g3RubbleHash = Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(g3RubblePath))).ToLowerInvariant();
        Check(g3PromptRecord.Contains(g3RubbleHash, StringComparison.Ordinal),
            "G.3 central rubble corridor A provenance hash is stale");
        PngInfo g3Rubble = ReadPng(g3RubblePath, true);
        Equal((byte)6, g3Rubble.ColorType, "G.3 central rubble corridor A RGBA");
        Check(g3Rubble.TransparentFraction >= 0.35d &&
            g3Rubble.CornerAlphas.All(alpha => alpha <= 1),
            "G.3 central rubble corridor A lacks isolated transparent alpha");

        string g3RiverWaterPath = Path.Combine(
            artDirectory,
            "g3/tiles/river-water-surface-a.png");
        Check(File.Exists(g3RiverWaterPath), "missing G.3 river water surface A");
        Check(File.Exists($"{g3RiverWaterPath}.import") &&
            File.ReadAllText($"{g3RiverWaterPath}.import").Contains(
                "mipmaps/generate=true",
                StringComparison.Ordinal),
            "G.3 river water surface A lacks a mipmapped Godot import");
        Check(scene.Contains(
                "res://art/commercial/g3/tiles/river-water-surface-a.png",
                StringComparison.Ordinal) &&
            scene.Contains("G3RiverWaterSurfaceTile = ExtResource", StringComparison.Ordinal) &&
            renderer.Contains(
                "G3RiverWaterSurfaceTile ?? RiverWaterTile",
                StringComparison.Ordinal),
            "G.3 river water surface A is not bound to the authoritative river polygon");
        string g3RiverWaterHash = Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(g3RiverWaterPath))).ToLowerInvariant();
        Check(g3PromptRecord.Contains(g3RiverWaterHash, StringComparison.Ordinal),
            "G.3 river water surface A provenance hash is stale");
        PngInfo g3RiverWater = ReadPng(g3RiverWaterPath, false);
        Check(g3RiverWater.ColorType is 2 or 6,
            "G.3 river water surface A has unsupported PNG color type");

        string g3RiverBankRockPath = Path.Combine(
            artDirectory,
            "g3/objects/river-bank-rock-segment-a.png");
        Check(File.Exists(g3RiverBankRockPath), "missing G.3 river bank rock segment A");
        Check(File.Exists($"{g3RiverBankRockPath}.import") &&
            File.ReadAllText($"{g3RiverBankRockPath}.import").Contains(
                "mipmaps/generate=true",
                StringComparison.Ordinal),
            "G.3 river bank rock segment A lacks a mipmapped Godot import");
        Check(scene.Contains(
                "res://art/commercial/g3/objects/river-bank-rock-segment-a.png",
                StringComparison.Ordinal) &&
            scene.Contains("G3RiverBankRockSegmentASprite = ExtResource", StringComparison.Ordinal) &&
            renderer.Contains("G3RiverBankRockSegmentASprite ?? RiverBankRubbleASprite", StringComparison.Ordinal),
            "G.3 river bank rock segment A is not individually bound to the river banks");
        string g3RiverBankRockHash = Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(g3RiverBankRockPath))).ToLowerInvariant();
        Check(g3PromptRecord.Contains(g3RiverBankRockHash, StringComparison.Ordinal),
            "G.3 river bank rock segment A provenance hash is stale");
        PngInfo g3RiverBankRock = ReadPng(g3RiverBankRockPath, true);
        Equal((byte)6, g3RiverBankRock.ColorType, "G.3 river bank rock segment A RGBA");
        Check(g3RiverBankRock.TransparentFraction >= 0.35d &&
            g3RiverBankRock.CornerAlphas.All(alpha => alpha <= 1),
            "G.3 river bank rock segment A lacks isolated transparent alpha");

        foreach ((string relativePath, string property) in new[]
                 {
                     ("g3/objects/river-bank-inner-bend-a.png", "G3RiverBankInnerBendASprite"),
                     ("g3/objects/river-bank-outer-bend-a.png", "G3RiverBankOuterBendASprite"),
                 })
        {
            string bendPath = Path.Combine(artDirectory, relativePath);
            Check(File.Exists(bendPath), $"missing G.3 river bank bend: {relativePath}");
            Check(File.Exists($"{bendPath}.import") &&
                File.ReadAllText($"{bendPath}.import").Contains(
                    "mipmaps/generate=true",
                    StringComparison.Ordinal),
                $"G.3 river bank bend lacks a mipmapped Godot import: {relativePath}");
            Check(scene.Contains($"res://art/commercial/{relativePath}", StringComparison.Ordinal) &&
                scene.Contains($"{property} = ExtResource", StringComparison.Ordinal) &&
                renderer.Contains(property, StringComparison.Ordinal),
                $"G.3 river bank bend is not individually runtime-bound: {relativePath}");
            string bendHash = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(bendPath))).ToLowerInvariant();
            Check(g3PromptRecord.Contains(bendHash, StringComparison.Ordinal),
                $"G.3 river bank bend provenance hash is stale: {relativePath}");
            PngInfo bendPng = ReadPng(bendPath, true);
            Equal((byte)6, bendPng.ColorType, $"G.3 river bank bend RGBA: {relativePath}");
            Check(bendPng.TransparentFraction >= 0.35d &&
                bendPng.CornerAlphas.All(alpha => alpha <= 1),
                $"G.3 river bank bend lacks isolated transparent alpha: {relativePath}");
        }

        string g3ServiceYardPath = Path.Combine(
            artDirectory,
            "g3/objects/industrial-service-yard-b.png");
        Check(File.Exists(g3ServiceYardPath), "missing G.3 industrial service yard B");
        Check(File.Exists($"{g3ServiceYardPath}.import") &&
            File.ReadAllText($"{g3ServiceYardPath}.import").Contains(
                "mipmaps/generate=true",
                StringComparison.Ordinal),
            "G.3 industrial service yard B lacks a mipmapped Godot import");
        Check(scene.Contains(
                "res://art/commercial/g3/objects/industrial-service-yard-b.png",
                StringComparison.Ordinal) &&
            scene.Contains("IndustrialServiceYardBSprite = ExtResource", StringComparison.Ordinal) &&
            renderer.Contains("DrawIndustrialServiceYards", StringComparison.Ordinal),
            "G.3 industrial service yard B is not individually runtime-bound");
        string g3ServiceYardHash = Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(g3ServiceYardPath))).ToLowerInvariant();
        Check(g3PromptRecord.Contains(g3ServiceYardHash, StringComparison.Ordinal),
            "G.3 industrial service yard B provenance hash is stale");
        PngInfo g3ServiceYard = ReadPng(g3ServiceYardPath, true);
        Equal((byte)6, g3ServiceYard.ColorType, "G.3 industrial service yard B RGBA");
        Check(g3ServiceYard.TransparentFraction >= 0.35d &&
            g3ServiceYard.CornerAlphas.All(alpha => alpha <= 1),
            "G.3 industrial service yard B lacks isolated transparent alpha");

        string g3ResidentialBPath = Path.Combine(
            artDirectory,
            "g3/objects/residential-cluster-b.png");
        Check(File.Exists(g3ResidentialBPath), "missing G.3 residential cluster B");
        Check(File.Exists($"{g3ResidentialBPath}.import") &&
            File.ReadAllText($"{g3ResidentialBPath}.import").Contains(
                "mipmaps/generate=true",
                StringComparison.Ordinal),
            "G.3 residential cluster B lacks a mipmapped Godot import");
        Check(scene.Contains(
                "res://art/commercial/g3/objects/residential-cluster-b.png",
                StringComparison.Ordinal) &&
            scene.Contains("ResidentialClusterBSprite = ExtResource", StringComparison.Ordinal) &&
            renderer.Contains("ResidentialClusterBSprite", StringComparison.Ordinal),
            "G.3 residential cluster B is not individually runtime-bound");
        string g3ResidentialBHash = Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(g3ResidentialBPath))).ToLowerInvariant();
        Check(g3PromptRecord.Contains(g3ResidentialBHash, StringComparison.Ordinal),
            "G.3 residential cluster B provenance hash is stale");
        PngInfo g3ResidentialB = ReadPng(g3ResidentialBPath, true);
        Equal((byte)6, g3ResidentialB.ColorType, "G.3 residential cluster B RGBA");
        Check(g3ResidentialB.TransparentFraction >= 0.35d &&
            g3ResidentialB.CornerAlphas.All(alpha => alpha <= 1),
            "G.3 residential cluster B lacks isolated transparent alpha");

        string g3ResidentialCPath = Path.Combine(
            artDirectory,
            "g3/objects/irregular-residential-parcel-c.png");
        Check(File.Exists(g3ResidentialCPath), "missing G.3 irregular residential parcel C");
        Check(File.Exists($"{g3ResidentialCPath}.import") &&
            File.ReadAllText($"{g3ResidentialCPath}.import").Contains(
                "mipmaps/generate=true",
                StringComparison.Ordinal),
            "G.3 irregular residential parcel C lacks a mipmapped Godot import");
        Check(scene.Contains(
                "res://art/commercial/g3/objects/irregular-residential-parcel-c.png",
                StringComparison.Ordinal) &&
            scene.Contains(
                "IrregularResidentialParcelCSprite = ExtResource",
                StringComparison.Ordinal) &&
            renderer.Contains("IrregularResidentialParcelCSprite", StringComparison.Ordinal),
            "G.3 irregular residential parcel C is not individually runtime-bound");
        string g3ResidentialCHash = Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(g3ResidentialCPath))).ToLowerInvariant();
        Check(g3PromptRecord.Contains(g3ResidentialCHash, StringComparison.Ordinal),
            "G.3 irregular residential parcel C provenance hash is stale");
        PngInfo g3ResidentialC = ReadPng(g3ResidentialCPath, true);
        Equal((byte)6, g3ResidentialC.ColorType, "G.3 irregular residential parcel C RGBA");
        Check(g3ResidentialC.TransparentFraction >= 0.30d &&
            g3ResidentialC.CornerAlphas.All(alpha => alpha <= 1),
            "G.3 irregular residential parcel C lacks isolated transparent alpha");

        string g3ResidentialDPath = Path.Combine(
            artDirectory,
            "g3/objects/dense-residential-neighborhood-d.png");
        Check(File.Exists(g3ResidentialDPath),
            "missing G.3 dense residential neighborhood D");
        Check(File.Exists($"{g3ResidentialDPath}.import") &&
            File.ReadAllText($"{g3ResidentialDPath}.import").Contains(
                "mipmaps/generate=true",
                StringComparison.Ordinal),
            "G.3 dense residential neighborhood D lacks a mipmapped Godot import");
        Check(scene.Contains(
                "res://art/commercial/g3/objects/dense-residential-neighborhood-d.png",
                StringComparison.Ordinal) &&
            scene.Contains(
                "DenseResidentialNeighborhoodDSprite = ExtResource",
                StringComparison.Ordinal) &&
            renderer.Contains("DenseResidentialNeighborhoodDSprite", StringComparison.Ordinal),
            "G.3 dense residential neighborhood D is not individually runtime-bound");
        string g3ResidentialDHash = Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(g3ResidentialDPath))).ToLowerInvariant();
        Check(g3PromptRecord.Contains(g3ResidentialDHash, StringComparison.Ordinal),
            "G.3 dense residential neighborhood D provenance hash is stale");
        PngInfo g3ResidentialD = ReadPng(g3ResidentialDPath, true);
        Equal((byte)6, g3ResidentialD.ColorType,
            "G.3 dense residential neighborhood D RGBA");
        Check(g3ResidentialD.TransparentFraction >= 0.30d &&
            g3ResidentialD.CornerAlphas.All(alpha => alpha <= 1),
            "G.3 dense residential neighborhood D lacks isolated transparent alpha");

        string g3IndustrialCPath = Path.Combine(
            artDirectory,
            "g3/objects/industrial-rubble-service-yard-c.png");
        Check(File.Exists(g3IndustrialCPath),
            "missing G.3 industrial rubble service yard C");
        Check(File.Exists($"{g3IndustrialCPath}.import") &&
            File.ReadAllText($"{g3IndustrialCPath}.import").Contains(
                "mipmaps/generate=true",
                StringComparison.Ordinal),
            "G.3 industrial rubble service yard C lacks a mipmapped Godot import");
        Check(scene.Contains(
                "res://art/commercial/g3/objects/industrial-rubble-service-yard-c.png",
                StringComparison.Ordinal) &&
            scene.Contains(
                "IndustrialRubbleServiceYardCSprite = ExtResource",
                StringComparison.Ordinal) &&
            renderer.Contains("DrawIndustrialRubbleServiceYards", StringComparison.Ordinal),
            "G.3 industrial rubble service yard C is not individually runtime-bound");
        string g3IndustrialCHash = Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(g3IndustrialCPath))).ToLowerInvariant();
        Check(g3PromptRecord.Contains(g3IndustrialCHash, StringComparison.Ordinal),
            "G.3 industrial rubble service yard C provenance hash is stale");
        PngInfo g3IndustrialC = ReadPng(g3IndustrialCPath, true);
        Equal((byte)6, g3IndustrialC.ColorType,
            "G.3 industrial rubble service yard C RGBA");
        Check(g3IndustrialC.TransparentFraction >= 0.30d &&
            g3IndustrialC.CornerAlphas.All(alpha => alpha <= 1),
            "G.3 industrial rubble service yard C lacks isolated transparent alpha");

        foreach ((string relativePath, string property, string drawMarker, string label) in new[]
                 {
                     (
                         "g3/objects/irregular-riverside-neighborhood-e.png",
                         "IrregularRiversideNeighborhoodESprite",
                         "IrregularRiversideNeighborhoodESprite",
                         "irregular riverside neighborhood E"),
                     (
                         "g3/objects/industrial-salvage-boiler-yard-d.png",
                         "IndustrialSalvageBoilerYardDSprite",
                         "DrawIndustrialRubbleServiceYards",
                         "industrial salvage boiler yard D"),
                     (
                         "g3/objects/rubble-utility-corridor-b.png",
                         "RubbleUtilityCorridorBSprite",
                         "DrawRubbleUtilityCorridor",
                         "rubble utility corridor B"),
                     (
                         "g3/objects/compact-utility-hamlet-f.png",
                         "CompactUtilityHamletFSprite",
                         "CompactUtilityHamletFSprite",
                         "compact utility hamlet F"),
                     (
                         "g3/objects/dense-roadside-residential-g.png",
                         "DenseRoadsideResidentialGSprite",
                         "DenseRoadsideResidentialGSprite",
                         "dense roadside residential G"),
                     (
                         "g3/objects/scrap-industrial-micro-block-e.png",
                         "ScrapIndustrialMicroBlockESprite",
                         "ScrapIndustrialMicroBlockESprite",
                         "scrap industrial micro-block E"),
                     (
                         "g3/objects/industrial-road-bridge-a.png",
                         "IndustrialRoadBridgeASprite",
                         "DrawRiverBridgeDeck",
                         "industrial road bridge A"),
                     (
                         "g3/objects/industrial-road-bridge-b.png",
                         "IndustrialRoadBridgeBSprite",
                         "IndustrialRoadBridgeBSprite",
                         "industrial road bridge B"),
                 })
        {
            string assetPath = Path.Combine(artDirectory, relativePath);
            Check(File.Exists(assetPath), $"missing G.3 {label}");
            Check(File.Exists($"{assetPath}.import") &&
                File.ReadAllText($"{assetPath}.import").Contains(
                    "mipmaps/generate=true",
                    StringComparison.Ordinal),
                $"G.3 {label} lacks a mipmapped Godot import");
            Check(scene.Contains($"res://art/commercial/{relativePath}", StringComparison.Ordinal) &&
                scene.Contains($"{property} = ExtResource", StringComparison.Ordinal) &&
                renderer.Contains(drawMarker, StringComparison.Ordinal),
                $"G.3 {label} is not individually runtime-bound");
            string assetHash = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(assetPath))).ToLowerInvariant();
            Check(g3PromptRecord.Contains(assetHash, StringComparison.Ordinal),
                $"G.3 {label} provenance hash is stale");
            PngInfo assetPng = ReadPng(assetPath, true);
            Equal((byte)6, assetPng.ColorType, $"G.3 {label} RGBA");
            Check(assetPng.TransparentFraction >= 0.30d &&
                assetPng.CornerAlphas.All(alpha => alpha <= 1),
                $"G.3 {label} lacks isolated transparent alpha");
        }

        string g3GroundRubblePath = Path.Combine(
            artDirectory,
            "g3/tiles/ground-rubble-mix-b.png");
        Check(File.Exists(g3GroundRubblePath), "missing G.3 ground rubble mix B");
        Check(File.Exists($"{g3GroundRubblePath}.import") &&
            File.ReadAllText($"{g3GroundRubblePath}.import").Contains(
                "mipmaps/generate=true",
                StringComparison.Ordinal),
            "G.3 ground rubble mix B lacks a mipmapped Godot import");
        Check(scene.Contains(
                "res://art/commercial/g3/tiles/ground-rubble-mix-b.png",
                StringComparison.Ordinal) &&
            scene.Contains("G3GroundRubbleMixBTile = ExtResource", StringComparison.Ordinal) &&
            renderer.Contains("G3GroundRubbleMixBTile", StringComparison.Ordinal),
            "G.3 ground rubble mix B is not individually runtime-bound");
        string g3GroundRubbleHash = Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(g3GroundRubblePath))).ToLowerInvariant();
        Check(g3PromptRecord.Contains(g3GroundRubbleHash, StringComparison.Ordinal),
            "G.3 ground rubble mix B provenance hash is stale");
        PngInfo g3GroundRubble = ReadPng(g3GroundRubblePath, false);
        Equal((byte)2, g3GroundRubble.ColorType, "G.3 ground rubble mix B RGB");

        string g3GroundReliefPath = Path.Combine(
            artDirectory,
            "g3/tiles/ground-rubble-relief-c.png");
        Check(File.Exists(g3GroundReliefPath), "missing G.3 ground rubble relief C");
        Check(File.Exists($"{g3GroundReliefPath}.import") &&
            File.ReadAllText($"{g3GroundReliefPath}.import").Contains(
                "mipmaps/generate=true",
                StringComparison.Ordinal),
            "G.3 ground rubble relief C lacks a mipmapped Godot import");
        Check(scene.Contains(
                "res://art/commercial/g3/tiles/ground-rubble-relief-c.png",
                StringComparison.Ordinal) &&
            scene.Contains("G3GroundRubbleReliefCTile = ExtResource", StringComparison.Ordinal) &&
            renderer.Contains("G3GroundRubbleReliefCTile ??", StringComparison.Ordinal),
            "G.3 ground rubble relief C is not bound as the primary ground material");
        string g3GroundReliefHash = Convert.ToHexString(
            SHA256.HashData(File.ReadAllBytes(g3GroundReliefPath))).ToLowerInvariant();
        Check(g3PromptRecord.Contains(g3GroundReliefHash, StringComparison.Ordinal),
            "G.3 ground rubble relief C provenance hash is stale");
        PngInfo g3GroundRelief = ReadPng(g3GroundReliefPath, false);
        Equal((byte)2, g3GroundRelief.ColorType, "G.3 ground rubble relief C RGB");

        foreach ((string relativePath, string property, string drawMarker, string label,
                     double minimumTransparency) in new[]
                 {
                     (
                         "g3/objects/continuous-worker-city-parcel-h.png",
                         "ContinuousWorkerCityParcelHSprite",
                         "ContinuousWorkerCityParcelHSprite ?? DenseCityParcelASprite",
                         "continuous worker city parcel H",
                         0.45d),
                     (
                         "g3/objects/continuous-worker-city-parcel-i.png",
                         "ContinuousWorkerCityParcelISprite",
                         "ContinuousWorkerCityParcelISprite ?? parcelTexture",
                         "continuous worker city parcel I",
                         0.45d),
                     (
                         "g3/objects/tall-thermal-power-station-b.png",
                         "TallThermalPowerStationBSprite",
                         "TallThermalPowerStationBSprite ?? IndustrialPowerDistrictASprite",
                         "tall thermal power station B",
                         0.50d),
                     (
                         "g3/objects/chunky-switching-substation-b.png",
                         "ChunkySwitchingSubstationBSprite",
                         "ChunkySwitchingSubstationBSprite ?? SubstationSprite",
                         "chunky switching substation B",
                         0.50d),
                     (
                         "g3/objects/river-current-reflection-a.png",
                         "RiverCurrentReflectionASprite",
                         "DrawRiverReflections",
                         "river current reflection A",
                         0.85d),
                 })
        {
            string assetPath = Path.Combine(artDirectory, relativePath);
            Check(File.Exists(assetPath), $"missing G.3 {label}");
            Check(File.Exists($"{assetPath}.import") &&
                File.ReadAllText($"{assetPath}.import").Contains(
                    "mipmaps/generate=true",
                    StringComparison.Ordinal),
                $"G.3 {label} lacks a mipmapped Godot import");
            Check(scene.Contains($"res://art/commercial/{relativePath}", StringComparison.Ordinal) &&
                scene.Contains($"{property} = ExtResource", StringComparison.Ordinal) &&
                renderer.Contains(drawMarker, StringComparison.Ordinal),
                $"G.3 {label} is not individually runtime-bound");
            string assetHash = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(assetPath))).ToLowerInvariant();
            Check(g3PromptRecord.Contains(assetHash, StringComparison.Ordinal),
                $"G.3 {label} provenance hash is stale");
            PngInfo assetPng = ReadPng(assetPath, true);
            Equal((byte)6, assetPng.ColorType, $"G.3 {label} RGBA");
            Check(assetPng.TransparentFraction >= minimumTransparency &&
                assetPng.CornerAlphas.All(alpha => alpha <= 1),
                $"G.3 {label} lacks isolated transparent alpha");
        }

        Check(
            renderer.Contains("IndividualArtAssetCount", StringComparison.Ordinal) &&
            renderer.Contains("G3GroundRubbleReliefCTile", StringComparison.Ordinal) &&
            renderer.Contains("ContinuousWorkerCityParcelHSprite", StringComparison.Ordinal) &&
            renderer.Contains("ContinuousWorkerCityParcelISprite", StringComparison.Ordinal) &&
            renderer.Contains("TallThermalPowerStationBSprite", StringComparison.Ordinal) &&
            renderer.Contains("ChunkySwitchingSubstationBSprite", StringComparison.Ordinal) &&
            renderer.Contains("RiverCurrentReflectionASprite", StringComparison.Ordinal),
            "G.3 exact 48-asset runtime inventory is incomplete");

        Check(!scene.Contains(
                "res://art/commercial/g3/tiles/dense-neighborhood-district-b.png",
                StringComparison.Ordinal),
            "superseded square neighborhood tile remains runtime-bound");

        string oldPlatePath = Path.Combine(gameDirectory, "art", "commercial-city-plate-v1.png");
        Check(!File.Exists(oldPlatePath), "whole-map city plate still exists");
        Check(!scene.Contains("commercial-city-plate", StringComparison.Ordinal),
            "scene still references whole-map city plate");
        Check(!renderer.Contains("CityPlate", StringComparison.Ordinal),
            "renderer still exposes whole-map city plate");
        foreach (string mapping in new[]
                 {
                     "CHEONGRYU_RIVER",
                     "EAST_RESIDENTIAL_BLOCK",
                     "HOSPITAL_BLOCK",
                     "EAST_RESIDENTIAL_TERMINAL",
                     "HOSPITAL_TERMINAL",
                     "WATER_TERMINAL",
                     "INDUSTRY_TERMINAL",
                     "STANDARD_POLE",
                     "DraftPoleSprite",
                     "DraftNodeSprite",
                     "DrawDraftSprite",
                     "CurrentDraftSpriteClassId",
                     "AuthoredFoundation",
                     "GroundTileWorldUnit = 400",
                     "DrawProjectedWorldCircle",
                     "DrawRiverTerrain",
                     "DrawRiverReflections",
                     "DrawDistrictObjects",
                 })
        {
            Check(renderer.Contains(mapping, StringComparison.Ordinal),
                $"renderer is missing discrete art mapping: {mapping}");
        }

        Check(
            transform.Contains("(deltaX - deltaY) * ScaleX", StringComparison.Ordinal) &&
            transform.Contains("(deltaX + deltaY) * ScaleY", StringComparison.Ordinal) &&
            transform.Contains("ScaleY => ScaleX * 0.5d", StringComparison.Ordinal),
            "commercial map transform is not the fixed 2:1 isometric projection");
        Check(
            timeline.Contains("650f * _uiScale", StringComparison.Ordinal) &&
            timeline.Contains("plateLeft", StringComparison.Ordinal),
            "event timeline is not a compact centered independent plate");
        Check(
            renderer.Contains("DrawBuildRail", StringComparison.Ordinal) &&
            renderer.Contains("BuildRailActionRequested", StringComparison.Ordinal),
            "functional individual-asset build rail is not wired into the map");
        Check(
            renderer.Contains("SmoothOpenPolyline", StringComparison.Ordinal) &&
            renderer.Contains("DrawCityFabric", StringComparison.Ordinal) &&
            renderer.Contains("DrawRoadFabric", StringComparison.Ordinal),
            "dense city fabric and spline river presentation are not wired");

        TerrainPolygonDefinition river = _commercialWorld.Spatial.Terrain.Single(area =>
            area.TerrainId == "CHEONGRYU_RIVER");
        Check(river.Polygon.Count >= 12,
            "commercial river lacks independent points for a winding two-bank channel");
        MapPoint[] firstBank = river.Polygon.Take(river.Polygon.Count / 2).ToArray();
        int previousDirection = 0;
        int bends = 0;
        for (int index = 1; index < firstBank.Length; index++)
        {
            int direction = Math.Sign(firstBank[index].XUnit - firstBank[index - 1].XUnit);
            if (direction != 0 && previousDirection != 0 && direction != previousDirection)
            {
                bends++;
            }
            if (direction != 0)
            {
                previousDirection = direction;
            }
        }
        Check(bends >= 3, "commercial river bank has fewer than three authored bends");
        Check(_commercialWorld.Spatial.Terrain.Any(area =>
                area.TerrainId == "WEST_INDUSTRIAL_BLOCK" &&
                area.Kind == TerrainKind.Building),
            "west industrial visual mass lacks an authoritative building footprint");
    }

    private bool CheckG3AtomicStepOne(
        string gameDirectory,
        string artDirectory,
        string scene,
        string renderer,
        string transform,
        string timeline)
    {
        string ledger = File.ReadAllText(Path.Combine(artDirectory, "g3-assets.prompts.md"));
        string sourceDirectory = Path.Combine(
            _repositoryDirectory,
            "playtests",
            "commercial-2d",
            "g3-runtime-sources");

        (string Property, string RelativePath, string SourceName, string RunId)[] atomicCityAssets =
        [
            ("AtomicWorkerHouseASprite", "g3/atomic/worker-house-a.png",
                "atomic-worker-house-a-source.png", "exec-348f1a4d-b730-4ad3-9d06-e9b780840d67"),
            ("AtomicWorkerHouseBSprite", "g3/atomic/worker-house-b.png",
                "atomic-worker-house-b-source.png", "exec-06607a7d-af88-4666-84c1-6615bbf5c16c"),
            ("AtomicWorkerHouseCSprite", "g3/atomic/worker-house-c.png",
                "atomic-worker-house-c-source.png", "exec-bfda1dea-09bd-4f19-924b-1c5e244d2ad4"),
            ("AtomicRowShopASprite", "g3/atomic/row-shop-a.png",
                "atomic-row-shop-a-source.png", "exec-1c3cda36-0c41-4cc4-b799-9142a49b498f"),
            ("AtomicWorkshopASprite", "g3/atomic/workshop-a.png",
                "atomic-workshop-a-source.png", "exec-254a88ba-cfd8-4a7e-ae7e-a52c2c313309"),
            ("AtomicSmallWarehouseASprite", "g3/atomic/small-warehouse-a.png",
                "atomic-small-warehouse-a-source.png", "exec-002d0386-4823-4e0c-a3f0-fbc39c77785a"),
            ("AtomicHospitalMainASprite", "g3/atomic/hospital-main-a.png",
                "atomic-hospital-main-a-source.png", "exec-279112df-8da1-43c9-9a93-71d6c98d3c52"),
            ("AtomicHospitalServiceASprite", "g3/atomic/hospital-service-a.png",
                "atomic-hospital-service-a-source.png", "exec-7789efea-c361-4bad-ba20-2906b6035670"),
            ("AtomicPumpHouseASprite", "g3/atomic/pump-house-a.png",
                "atomic-pump-house-a-source.png", "exec-5807d9c6-e49b-4924-b695-b6dacfc1d681"),
            ("AtomicWaterTankASprite", "g3/atomic/water-tank-a.png",
                "atomic-water-tank-a-source.png", "exec-14bf3ed7-f268-41d7-ac6b-e89ab87ab765"),
            ("AtomicRetainingWallASprite", "g3/atomic/retaining-wall-a.png",
                "atomic-retaining-wall-a-source.png", "exec-7ac6b279-f844-40b6-ae3f-9a2422cd8043"),
            ("AtomicStreetLampASprite", "g3/atomic/street-lamp-a.png",
                "atomic-street-lamp-a-source.png", "exec-a0b090cb-b450-441d-9e15-24394a61ec27"),
        ];
        Equal(12, atomicCityAssets.Length, "Step 1 atomic city asset count");

        (string Property, string RelativePath, string SourceName, string RunId)[] atomicRoadAssets =
        [
            ("RoadStraightNorthWestSouthEastATile", "g3/roads/road-straight-nw-se-a.png",
                "atomic-road-straight-nw-se-a-source.png", "exec-0d5eddd4-7250-499e-8063-9572ab44b149"),
            ("RoadStraightNorthEastSouthWestATile", "g3/roads/road-straight-ne-sw-a.png",
                "atomic-road-straight-ne-sw-a-source.png", "exec-7339f677-be4e-4d01-814d-b4fa56dbb026"),
            ("RoadCornerNorthEastATile", "g3/roads/road-corner-n-e-a.png",
                "atomic-road-corner-n-e-a-source.png", "exec-431fc6a3-8172-4a37-8ca7-8af23278866f"),
            ("RoadTJunctionATile", "g3/roads/road-t-junction-a.png",
                "atomic-road-t-junction-a-source.png", "exec-e269ef5f-1a1d-48c4-96fd-ec4560724de2"),
            ("RoadCrossJunctionATile", "g3/roads/road-cross-junction-a.png",
                "atomic-road-cross-junction-a-source.png", "exec-2f632ddb-d92a-4f75-8e97-70a451b8d53a"),
            ("ServiceYardATile", "g3/roads/service-yard-tile-a.png",
                "atomic-service-yard-tile-a-source.png", "exec-64bdce3b-b5ae-4aca-83ed-0c5c65a5c234"),
        ];
        Equal(6, atomicRoadAssets.Length, "Step 1 atomic road asset count");

        (string Property, string RelativePath, string SourceName, string RunId, bool RequiresAlpha)[]
            atomicRiverAssets =
        [
            ("RiverWaterNeutralBTile", "g3/river/river-water-neutral-b.png",
                "river-water-neutral-b-source.png", "exec-e2902ef2-0d4b-40d5-9198-9459848058cd", false),
            ("RiverWaterHeatATile", "g3/river/river-water-heat-a.png",
                "river-water-heat-a-source.png", "exec-0bbd1fe2-6d39-4589-b142-6bd7f0cbb64b", false),
            ("RiverWaterFloodATile", "g3/river/river-water-flood-a.png",
                "river-water-flood-a-source.png", "exec-c0a4f649-0a55-40ca-93a2-01d0be7befbb", false),
            ("RiverBankLeftStraightASprite", "g3/river/river-bank-left-straight-a.png",
                "river-bank-left-straight-a-source.png", "exec-1a53642a-0344-49ce-8f1f-65e464d8ceb0", true),
            ("RiverBankRightStraightASprite", "g3/river/river-bank-right-straight-a.png",
                "river-bank-right-straight-a-source.png", "exec-85c008ed-521f-42df-9965-345057973ce9", true),
            ("RiverBankLeftInnerASprite", "g3/river/river-bank-left-inner-a.png",
                "river-bank-left-inner-a-source.png", "exec-8447db00-7d32-494c-8b2f-aac537fb4960", true),
            ("RiverBankLeftOuterASprite", "g3/river/river-bank-left-outer-a.png",
                "river-bank-left-outer-a-source.png", "exec-d60c0365-8ce9-47c3-b3b4-65372b0cc556", true),
            ("RiverBankRightInnerASprite", "g3/river/river-bank-right-inner-a.png",
                "river-bank-right-inner-a-source.png", "exec-2669c1aa-5bf0-4137-af7d-5d4ebb7bea1a", true),
            ("RiverBankRightOuterASprite", "g3/river/river-bank-right-outer-a.png",
                "river-bank-right-outer-a-source.png", "exec-d21488ab-a180-4627-be80-bf8b49326b5a", true),
            ("RiverBridgeAbutmentASprite", "g3/river/river-bridge-abutment-a.png",
                "river-bridge-abutment-a-source.png", "exec-9b9a3087-d3d6-4c95-98d7-6f7c0a1645f5", true),
            ("RiverRockSoilTransitionASprite", "g3/river/river-rock-soil-transition-a.png",
                "river-rock-soil-transition-a-source.png", "exec-02db16b8-2c39-4648-a702-1e48a8f7c8f4", true),
            ("RiverFloodRippleASprite", "g3/river/river-flood-ripple-a.png",
                "river-flood-ripple-a-source.png", "exec-39d06c57-7026-4209-b07a-5fee07e76021", true),
            ("RiverBankConiferASprite", "g3/river/river-bank-conifer-a.png",
                "river-bank-conifer-a-source.png", "exec-ad2bc6df-f6ac-4de6-9409-e7ce590348b0", true),
            ("RiverBankScrubASprite", "g3/river/river-bank-scrub-a.png",
                "river-bank-scrub-a-source.png", "exec-1e6ad975-9e07-45e0-9cf5-902e45e3eac4", true),
            ("RiverBankOutcropASprite", "g3/river/river-bank-outcrop-a.png",
                "river-bank-outcrop-a-source.png", "exec-c3000101-0616-4fb7-bc3b-773d5d9d5bf6", true),
        ];
        Equal(15, atomicRiverAssets.Length, "Step 2 atomic river asset count");

        (string Property, string RelativePath, string SourceName, string RunId)[] atomicGridAssets =
        [
            ("AtomicPlantMainHallASprite", "g3/grid/plant-main-hall-a.png",
                "atomic-plant-main-hall-a-source.png", "exec-ff08804a-3196-4794-9c4a-fbe8fc021601"),
            ("AtomicPlantSmokestackASprite", "g3/grid/plant-smokestack-a.png",
                "atomic-plant-smokestack-a-source.png", "exec-822fe640-c211-45a2-adc2-1e1f1949c4f5"),
            ("AtomicPlantTurbineHallASprite", "g3/grid/plant-turbine-hall-a.png",
                "atomic-plant-turbine-hall-a-source.png", "exec-7e1e7885-b6af-4891-bd70-e9ff3fc92830"),
            ("AtomicSwitchyardBreakerBayASprite", "g3/grid/switchyard-breaker-bay-a.png",
                "atomic-switchyard-breaker-bay-a-source.png", "exec-751318e5-a19c-4fc8-811d-c0490c20e401"),
            ("AtomicSubstationTransformerASprite", "g3/grid/substation-transformer-a.png",
                "atomic-substation-transformer-a-source.png", "exec-525b7168-42d9-4c04-a538-4afe281489c6"),
            ("AtomicStandardPoleASprite", "g3/grid/pole-standard-a.png",
                "atomic-pole-standard-a-source.png", "exec-f0fe7a22-7c24-402d-9494-ad80b442073a"),
            ("AtomicReinforcedPoleASprite", "g3/grid/pole-reinforced-a.png",
                "atomic-pole-reinforced-a-source.png", "exec-3db19133-0dbf-42ea-b16b-8f6cf89648d4"),
            ("AtomicBridgeFoundationASprite", "g3/grid/bridge-foundation-a.png",
                "atomic-bridge-foundation-a-source.png", "exec-9775ee23-b362-488f-8d0e-2ab8df147252"),
        ];
        Equal(8, atomicGridAssets.Length, "Step 3 atomic grid/facility asset count");

        foreach ((string property, string relativePath, string sourceName, string runId) in
                 atomicCityAssets.Concat(atomicRoadAssets))
        {
            string runtimePath = Path.Combine(artDirectory, relativePath);
            string sourcePath = Path.Combine(sourceDirectory, sourceName);
            Check(File.Exists(runtimePath), $"missing Step 1 atomic runtime art: {relativePath}");
            Check(File.Exists(sourcePath), $"missing Step 1 preserved source: {sourceName}");
            Check(File.Exists($"{runtimePath}.import") &&
                File.ReadAllText($"{runtimePath}.import").Contains(
                    "mipmaps/generate=true",
                    StringComparison.Ordinal),
                $"Step 1 atomic art lacks mipmapped import: {relativePath}");
            Check(scene.Contains($"res://art/commercial/{relativePath}", StringComparison.Ordinal) &&
                scene.Contains($"{property} = ExtResource", StringComparison.Ordinal) &&
                renderer.Contains(property, StringComparison.Ordinal),
                $"Step 1 atomic art is not runtime-bound: {relativePath}");
            string runtimeHash = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(runtimePath))).ToLowerInvariant();
            string sourceHash = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(sourcePath))).ToLowerInvariant();
            Check(ledger.Contains(relativePath, StringComparison.Ordinal) &&
                ledger.Contains(sourceName, StringComparison.Ordinal) &&
                ledger.Contains(runId, StringComparison.Ordinal) &&
                ledger.Contains(runtimeHash, StringComparison.Ordinal) &&
                ledger.Contains(sourceHash, StringComparison.Ordinal),
                $"Step 1 provenance is incomplete or stale: {relativePath}");
            PngInfo png = ReadPng(runtimePath, true);
            Equal((byte)6, png.ColorType, $"Step 1 atomic RGBA: {relativePath}");
            Check(Math.Max(png.Width, png.Height) >= 1024 &&
                Math.Min(png.Width, png.Height) >= 512,
                $"Step 1 atomic art below source resolution: {relativePath}");
            Check(png.TransparentFraction >= 0.35d &&
                png.CornerAlphas.All(alpha => alpha <= 1),
                $"Step 1 atomic art lacks isolated alpha: {relativePath}");
        }

        foreach ((string property, string relativePath, string sourceName, string runId,
                     bool requiresAlpha) in atomicRiverAssets)
        {
            string runtimePath = Path.Combine(artDirectory, relativePath);
            string sourcePath = Path.Combine(sourceDirectory, sourceName);
            Check(File.Exists(runtimePath), $"missing Step 2 atomic river art: {relativePath}");
            Check(File.Exists(sourcePath), $"missing Step 2 preserved source: {sourceName}");
            Check(File.Exists($"{runtimePath}.import") &&
                File.ReadAllText($"{runtimePath}.import").Contains(
                    "mipmaps/generate=true",
                    StringComparison.Ordinal),
                $"Step 2 river art lacks mipmapped import: {relativePath}");
            Check(scene.Contains($"res://art/commercial/{relativePath}", StringComparison.Ordinal) &&
                scene.Contains($"{property} = ExtResource", StringComparison.Ordinal) &&
                renderer.Contains(property, StringComparison.Ordinal),
                $"Step 2 river art is not runtime-bound: {relativePath}");
            string runtimeHash = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(runtimePath))).ToLowerInvariant();
            string sourceHash = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(sourcePath))).ToLowerInvariant();
            Check(ledger.Contains(relativePath, StringComparison.Ordinal) &&
                ledger.Contains(sourceName, StringComparison.Ordinal) &&
                ledger.Contains(runId, StringComparison.Ordinal) &&
                ledger.Contains(runtimeHash, StringComparison.Ordinal) &&
                ledger.Contains(sourceHash, StringComparison.Ordinal),
                $"Step 2 provenance is incomplete or stale: {relativePath}");
            PngInfo png = ReadPng(runtimePath, requiresAlpha);
            Check(Math.Max(png.Width, png.Height) >= 1024 &&
                Math.Min(png.Width, png.Height) >= 512,
                $"Step 2 river art below source resolution: {relativePath}");
            if (requiresAlpha)
            {
                Equal((byte)6, png.ColorType, $"Step 2 river RGBA: {relativePath}");
                Check(png.TransparentFraction >= 0.35d &&
                    png.CornerAlphas.All(alpha => alpha <= 1),
                    $"Step 2 river object lacks isolated alpha: {relativePath}");
            }
            else
            {
                Check(png.ColorType is 2 or 6,
                    $"Step 2 water tile PNG has unsupported color type: {relativePath}");
                Equal(2, png.Width / Math.Max(1, png.Height),
                    $"Step 2 water tile is not 2:1: {relativePath}");
            }
        }

        foreach ((string property, string relativePath, string sourceName, string runId) in
                 atomicGridAssets)
        {
            string runtimePath = Path.Combine(artDirectory, relativePath);
            string sourcePath = Path.Combine(sourceDirectory, sourceName);
            Check(File.Exists(runtimePath), $"missing Step 3 atomic grid art: {relativePath}");
            Check(File.Exists(sourcePath), $"missing Step 3 preserved source: {sourceName}");
            Check(File.Exists($"{runtimePath}.import") &&
                File.ReadAllText($"{runtimePath}.import").Contains(
                    "mipmaps/generate=true",
                    StringComparison.Ordinal),
                $"Step 3 grid art lacks mipmapped import: {relativePath}");
            Check(scene.Contains($"res://art/commercial/{relativePath}", StringComparison.Ordinal) &&
                scene.Contains($"{property} = ExtResource", StringComparison.Ordinal) &&
                renderer.Contains(property, StringComparison.Ordinal),
                $"Step 3 grid art is not runtime-bound: {relativePath}");
            string runtimeHash = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(runtimePath))).ToLowerInvariant();
            string sourceHash = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(sourcePath))).ToLowerInvariant();
            Check(ledger.Contains(relativePath, StringComparison.Ordinal) &&
                ledger.Contains(sourceName, StringComparison.Ordinal) &&
                ledger.Contains(runId, StringComparison.Ordinal) &&
                ledger.Contains(runtimeHash, StringComparison.Ordinal) &&
                ledger.Contains(sourceHash, StringComparison.Ordinal),
                $"Step 3 provenance is incomplete or stale: {relativePath}");
            PngInfo png = ReadPng(runtimePath, true);
            Equal((byte)6, png.ColorType, $"Step 3 atomic grid RGBA: {relativePath}");
            bool alphaTrimmedPole = property is
                "AtomicStandardPoleASprite" or "AtomicReinforcedPoleASprite";
            Check(alphaTrimmedPole
                    ? Math.Max(png.Width, png.Height) >= 350 &&
                      Math.Min(png.Width, png.Height) >= 180
                    : Math.Max(png.Width, png.Height) >= 1024 &&
                      Math.Min(png.Width, png.Height) >= 512,
                $"Step 3 grid art lacks source or alpha-trimmed runtime resolution: {relativePath}");
            PngInfo sourcePng = ReadPng(sourcePath, true);
            Check(Math.Max(sourcePng.Width, sourcePng.Height) >= 1024 &&
                Math.Min(sourcePng.Width, sourcePng.Height) >= 512,
                $"Step 3 preserved source is below generation resolution: {sourceName}");
            Check(png.TransparentFraction >= 0.30d &&
                png.CornerAlphas.All(alpha => alpha <= 1),
                $"Step 3 grid art lacks isolated transparent alpha: {relativePath}");
        }

        foreach (string forbidden in new[]
                 {
                     "district",
                     "parcel",
                     "cluster",
                     "neighborhood",
                     "hamlet",
                     "city-plate",
                 })
        {
            Check(!scene.Contains(forbidden, StringComparison.OrdinalIgnoreCase),
                $"forbidden composite city raster remains runtime-bound: {forbidden}");
        }
        foreach (string retiredCityBlock in new[]
                 {
                     "residential-block-v1.png",
                     "hospital-block-v1.png",
                 })
        {
            Check(!scene.Contains(retiredCityBlock, StringComparison.Ordinal),
                $"retired baked city block remains scene-bound: {retiredCityBlock}");
        }

        string g3Directory = Path.Combine(artDirectory, "g3");
        string[] packagedG3Pngs = Directory.GetFiles(
            g3Directory,
            "*.png",
            SearchOption.AllDirectories);
        Equal(57, packagedG3Pngs.Length,
            "Step 4 package-eligible G.3 PNG count");
        foreach (string packagedG3Png in packagedG3Pngs)
        {
            string relativePath = Path.GetRelativePath(artDirectory, packagedG3Png)
                .Replace(Path.DirectorySeparatorChar, '/');
            Check(scene.Contains($"res://art/commercial/{relativePath}", StringComparison.Ordinal),
                $"unbound G.3 PNG remains package-eligible: {relativePath}");
        }

        Equal(338,
            renderer.Split("new(AtomicCitySpriteKind.", StringSplitOptions.None).Length - 1,
            "Step 1 atomic city instance records");
        Equal(112,
            renderer.Split("new(AtomicRoadSpriteKind.", StringSplitOptions.None).Length - 1,
            "Step 1 atomic road instance records");
        Equal(24,
            renderer.Split("new(AtomicRiverEnvironmentKind.", StringSplitOptions.None).Length - 1,
            "Step 2 atomic river environment instance records");
        Check(renderer.Contains("DrawAtomicRoadTiles();", StringComparison.Ordinal) &&
            renderer.Contains("DrawAtomicCity();", StringComparison.Ordinal) &&
            renderer.Contains("OrderBy(item => ToCanvas", StringComparison.Ordinal) &&
            renderer.Contains("AtomicWorldInstanceCount", StringComparison.Ordinal),
            "Step 1 atomic placement/depth renderer is incomplete");
        Check(renderer.Contains("AtomicHospitalMainASprite, 174f", StringComparison.Ordinal) &&
            renderer.Contains("AtomicPumpHouseASprite, 118f", StringComparison.Ordinal) &&
            renderer.Contains("AtomicSmallWarehouseASprite, 124f", StringComparison.Ordinal),
            "dedicated load terminals do not use atomic single-building sprites");

        (string Property, string RelativePath)[] retainedG3Assets =
        [
            ("G3GroundRubbleMixBTile", "g3/tiles/ground-rubble-mix-b.png"),
            ("G3GroundRubbleReliefCTile", "g3/tiles/ground-rubble-relief-c.png"),
            ("G3RiverWaterSurfaceTile", "g3/tiles/river-water-surface-a.png"),
            ("G3RiverBankRockSegmentASprite", "g3/objects/river-bank-rock-segment-a.png"),
            ("G3RiverBankInnerBendASprite", "g3/objects/river-bank-inner-bend-a.png"),
            ("G3RiverBankOuterBendASprite", "g3/objects/river-bank-outer-bend-a.png"),
            ("RiverCurrentReflectionASprite", "g3/objects/river-current-reflection-a.png"),
            ("IndustrialRoadBridgeASprite", "g3/objects/industrial-road-bridge-a.png"),
            ("IndustrialRoadBridgeBSprite", "g3/objects/industrial-road-bridge-b.png"),
        ];
        foreach ((string property, string relativePath) in retainedG3Assets)
        {
            string runtimePath = Path.Combine(artDirectory, relativePath);
            Check(File.Exists(runtimePath) &&
                scene.Contains($"res://art/commercial/{relativePath}", StringComparison.Ordinal) &&
                scene.Contains($"{property} = ExtResource", StringComparison.Ordinal),
                $"retained G.3 runtime asset is missing: {relativePath}");
            string runtimeHash = Convert.ToHexString(
                SHA256.HashData(File.ReadAllBytes(runtimePath))).ToLowerInvariant();
            Check(ledger.Contains(runtimeHash, StringComparison.Ordinal),
                $"retained G.3 provenance hash is stale: {relativePath}");
        }

        Equal(64,
            scene.Split("[ext_resource type=\"Texture2D\"", StringSplitOptions.None).Length - 1,
            "Step 4 runtime texture resource count including UI chrome");
        Check(renderer.Contains("IndividualArtAssetCount", StringComparison.Ordinal) &&
            renderer.Contains("AtomicCityAssetCount", StringComparison.Ordinal) &&
            renderer.Contains("AtomicRoadTileAssetCount", StringComparison.Ordinal) &&
            renderer.Contains("AtomicGridAssetCount", StringComparison.Ordinal),
            "Step 3 exact runtime asset inventory is not exposed");
        Equal(9,
            renderer.Split("new(AtomicSourcePartKind.", StringSplitOptions.None).Length - 1,
            "Step 3 atomic source-part placement records across source and southern works");
        Check(renderer.Contains("DrawAtomicSourcePlant", StringComparison.Ordinal) &&
            renderer.Contains("OrderBy(item => ToCanvas(item.Point).Y)", StringComparison.Ordinal) &&
            renderer.Contains("SpatialNodeKind.SourceTerminal => (AtomicPlantMainHallASprite", StringComparison.Ordinal) &&
            renderer.Contains("SpatialNodeKind.Substation =>", StringComparison.Ordinal) &&
            renderer.Contains("AtomicSubstationTransformerASprite", StringComparison.Ordinal) &&
            renderer.Contains("AtomicBridgeFoundationASprite", StringComparison.Ordinal) &&
            !scene.Contains("tall-thermal-power-station", StringComparison.Ordinal) &&
            !scene.Contains("chunky-switching-substation", StringComparison.Ordinal),
            "Step 3 atomic facility placement or composite retirement is incomplete");
        Check(renderer.Contains("RiverWaterHeatATile ?? texture", StringComparison.Ordinal) &&
            renderer.Contains("RiverWaterFloodATile ?? texture", StringComparison.Ordinal) &&
            renderer.Contains("RiverBankLeftStraightASprite", StringComparison.Ordinal) &&
            renderer.Contains("RiverBankRightOuterASprite", StringComparison.Ordinal) &&
            renderer.Contains("RiverBridgeAbutmentASprite", StringComparison.Ordinal) &&
            renderer.Contains("RiverFloodRippleASprite", StringComparison.Ordinal),
            "Step 2 river state/directional object wiring is incomplete");
        Check(renderer.Contains("new(1211, 2000)", StringComparison.Ordinal) &&
            renderer.Contains("new(1310, 500)", StringComparison.Ordinal) &&
            renderer.Contains("new CoreMapPoint(1330, 500)", StringComparison.Ordinal) &&
            renderer.Contains("new CoreMapPoint(1480, 1500)", StringComparison.Ordinal),
            "Step 2 river does not span the authoritative corridor and bridge foundations");
        int riverBankObjectDraw = renderer.IndexOf(
            "DrawRiverBankObjects(outerLeft", StringComparison.Ordinal);
        int riverWaterDraw = renderer.IndexOf(
            "DrawRiverSurfaceSegments(", StringComparison.Ordinal);
        Check(riverBankObjectDraw >= 0 && riverWaterDraw >= 0 &&
            riverBankObjectDraw < riverWaterDraw,
            "Step 2 bank objects must composite below water to avoid dark channel intrusion");
        Check(transform.Contains("(deltaX - deltaY) * ScaleX", StringComparison.Ordinal) &&
            transform.Contains("(deltaX + deltaY) * ScaleY", StringComparison.Ordinal) &&
            transform.Contains("ScaleY => ScaleX * 0.5d", StringComparison.Ordinal),
            "commercial map transform is not fixed 2:1 isometric");
        Check(timeline.Contains("650f * _uiScale", StringComparison.Ordinal) &&
            timeline.Contains("plateLeft", StringComparison.Ordinal),
            "event timeline is not a compact independent plate");

        TerrainPolygonDefinition river = _commercialWorld.Spatial.Terrain.Single(area =>
            area.TerrainId == "CHEONGRYU_RIVER");
        Check(river.Polygon.Count >= 12,
            "commercial river lacks independent points for a winding two-bank channel");
        MapPoint[] firstBank = river.Polygon.Take(river.Polygon.Count / 2).ToArray();
        int previousDirection = 0;
        int bends = 0;
        for (int index = 1; index < firstBank.Length; index++)
        {
            int direction = Math.Sign(firstBank[index].XUnit - firstBank[index - 1].XUnit);
            if (direction != 0 && previousDirection != 0 && direction != previousDirection)
            {
                bends++;
            }
            if (direction != 0)
            {
                previousDirection = direction;
            }
        }
        Check(bends >= 3, "commercial river bank has fewer than three authored bends");
        return true;
    }

    private void CheckStrictSpatialLoader()
    {
        SpatialWorldDefinition fromText = SpatialWorldLoader.Load(_fixtureJson);
        SpatialWorldDefinition fromBytes = SpatialWorldLoader.Load(_fixtureBytes);
        Equal(_fixture.WorldId, fromText.WorldId, "text loader world ID");
        Equal(_fixture.WorldId, fromBytes.WorldId, "UTF-8 loader world ID");
        Equal(100, _fixture.UnitsPerDesignUnit, "fixed-point units per design unit");
        Equal(6, _fixture.Nodes.Count, "authored fixture node count");
        JsonObject serviceRoot = JsonNode.Parse(_fixtureJson)!.AsObject();
        Object(JsonArrayProperty(serviceRoot, "nodeClasses")[4]!)["serviceRadiusUnit"] = 800;
        SpatialWorldDefinition serviceWorld = SpatialWorldLoader.Load(serviceRoot.ToJsonString());
        Equal(800, serviceWorld.NodeClasses.Single(item =>
            item.ClassId == "SMALL_SUBSTATION").ServiceRadiusUnit,
            "strict loader substation service radius");

        string trimmed = _fixtureJson.TrimStart();
        ExpectLoaderRejected(
            "duplicate JSON property",
            $"{{\"schemaVersion\":\"duplicate\",{trimmed[1..]}");
        ExpectLoaderRejected("invalid UTF-8", [0xff, 0xfe, 0xfd]);
        ExpectLoaderRejected("unknown root field", root => root["unexpected"] = true);
        ExpectLoaderRejected("missing required field", root => root.Remove("worldId"));
        ExpectLoaderRejected(
            "wrong fixed-point scale",
            root => root["unitsPerDesignUnit"] = 10);
        ExpectLoaderRejected(
            "future node capacity field",
            root => Object(JsonArrayProperty(root, "nodeClasses")[0]!)["capacityKw"] = 5000);
        ExpectLoaderRejected(
            "future line rating field",
            root => Object(JsonArrayProperty(root, "lineClasses")[0]!)["ratingKw"] = 2500);
        ExpectLoaderRejected(
            "service radius on non-substation",
            root => Object(JsonArrayProperty(root, "nodeClasses")[0]!)["serviceRadiusUnit"] = 800);
        ExpectLoaderRejected(
            "negative substation service radius",
            root => Object(JsonArrayProperty(root, "nodeClasses")[4]!)["serviceRadiusUnit"] = -1);
        ExpectLoaderRejected(
            "duplicate node identifier",
            root => JsonArrayProperty(root, "nodes").Add(
                JsonArrayProperty(root, "nodes")[0]!.DeepClone()));
        ExpectLoaderRejected(
            "self-intersecting risk polygon",
            root => Object(JsonArrayProperty(root, "riskAreas")[0]!)["polygon"] = new JsonArray(
                PointJson(100, 100),
                PointJson(700, 100),
                PointJson(100, 500),
                PointJson(600, 700)));
        ExpectLoaderRejected(
            "adjacent polygon edge retrace",
            root => Object(JsonArrayProperty(root, "riskAreas")[0]!)["polygon"] = new JsonArray(
                PointJson(100, 100),
                PointJson(700, 100),
                PointJson(400, 100),
                PointJson(400, 700),
                PointJson(100, 700)));
        ExpectLoaderRejected(
            "zero-edge risk polygon",
            root =>
            {
                JsonArray polygon = JsonArrayProperty(
                    Object(JsonArrayProperty(root, "riskAreas")[0]!),
                    "polygon");
                polygon[1] = polygon[0]!.DeepClone();
            });
        ExpectLoaderRejected(
            "authored edge through building",
            root =>
            {
                JsonArrayProperty(root, "nodes").Add(NodeJson(
                    "BUILDING_END",
                    "LOAD_TERMINAL",
                    3150,
                    800,
                    authoredFoundation: true));
                JsonArrayProperty(root, "edges").Add(EdgeJson(
                    "BUILDING_EDGE",
                    "REINFORCED_LINE",
                    "EAST_RESIDENTIAL_TERMINAL",
                    "BUILDING_END"));
            });
        ExpectLoaderRejected(
            "authored edge through third-node footprint",
            root =>
            {
                JsonArrayProperty(root, "nodes").Add(NodeJson(
                    "THIRD_NODE_END",
                    "LOAD_TERMINAL",
                    300,
                    1150,
                    authoredFoundation: false));
                JsonArrayProperty(root, "edges").Add(EdgeJson(
                    "THIRD_NODE_EDGE",
                    "STANDARD_LINE",
                    "WEST_SOURCE",
                    "THIRD_NODE_END"));
            });
    }

    private void CheckIntegerGeometryAndTangency()
    {
        Equal(5L, FixedGeometry.CeilDistance(new MapPoint(0, 0), new MapPoint(3, 4)),
            "3-4-5 integer distance");
        Equal(2L, FixedGeometry.CeilDistance(new MapPoint(0, 0), new MapPoint(1, 1)),
            "irrational distance rounds upward");
        Equal(5L, FixedGeometry.CeilSquareRoot(17), "integer square-root ceiling");

        var bounds = new MapBounds(0, 0, 100, 100);
        Check(FixedGeometry.CircleWithinBounds(new MapPoint(10, 10), 10, bounds),
            "boundary-tangent footprint must remain inside");
        Check(!FixedGeometry.CircleWithinBounds(new MapPoint(9, 10), 10, bounds),
            "footprint crossing bounds was accepted");

        MapPoint[] square =
        [
            new(20, 20),
            new(80, 20),
            new(80, 80),
            new(20, 80),
        ];
        Check(FixedGeometry.ContainsPointInclusive(new MapPoint(20, 50), square),
            "polygon boundary must be inclusive");
        Check(FixedGeometry.CircleIntersectsPolygon(new MapPoint(10, 50), 10, square),
            "circle-polygon tangency must count as intersection");
        Check(FixedGeometry.SegmentTouchesCircle(
                new MapPoint(0, 0),
                new MapPoint(100, 0),
                new MapPoint(50, 10),
                10),
            "segment-circle tangency must count as contact");
        Check(!FixedGeometry.SegmentTouchesCircle(
                new MapPoint(int.MinValue, int.MinValue),
                new MapPoint(int.MaxValue, int.MinValue),
                new MapPoint(0, int.MaxValue),
                int.MaxValue),
            "extreme segment-circle comparison overflowed or reported a false contact");
        Check(FixedGeometry.SegmentsIntersectInclusive(
                new MapPoint(0, 50),
                new MapPoint(100, 50),
                new MapPoint(50, 0),
                new MapPoint(50, 100)),
            "noncollinear crossing was missed");
        Check(FixedGeometry.CollinearPositiveOverlap(
                new MapPoint(0, 0),
                new MapPoint(100, 0),
                new MapPoint(50, 0),
                new MapPoint(150, 0)),
            "positive collinear overlap was missed");
        Check(!FixedGeometry.CollinearPositiveOverlap(
                new MapPoint(0, 0),
                new MapPoint(100, 0),
                new MapPoint(100, 0),
                new MapPoint(150, 0)),
            "endpoint-only contact became positive overlap");
    }

    private void CheckNodePlacementAndRisk()
    {
        TerrainPolygonDefinition water = Terrain("WATER", TerrainKind.Water, 300, 300, 500, 500);
        TerrainPolygonDefinition building = Terrain("BUILDING", TerrainKind.Building, 700, 700, 900, 900);
        SpatialRiskAreaDefinition riskZ = Risk("Z_RISK", 1100, 1100, 1300, 1300);
        SpatialRiskAreaDefinition riskA = Risk("A_RISK", 1050, 1050, 1350, 1350);
        SpatialWorldDefinition world = World(
            [Node("A", 100, 100)],
            terrain: [water, building],
            risks: [riskZ, riskA]);
        SpatialWorldLoader.Validate(world);

        Check(PlacementValidator.PreviewNodePlacement(
                world,
                SubstationClassId,
                new MapPoint(1500, 1500)).Accepted,
            "safe node placement was rejected");
        Error(
            ConstructionError.OutsideBounds,
            PlacementValidator.PreviewNodePlacement(
                world,
                SubstationClassId,
                new MapPoint(10, 1500)),
            "full footprint map bounds");
        Error(
            ConstructionError.WaterFootprint,
            PlacementValidator.PreviewNodePlacement(
                world,
                SubstationClassId,
                new MapPoint(400, 400)),
            "water footprint");
        Error(
            ConstructionError.BuildingFootprint,
            PlacementValidator.PreviewNodePlacement(
                world,
                SubstationClassId,
                new MapPoint(800, 800)),
            "building footprint");
        Error(
            ConstructionError.PositionOccupied,
            PlacementValidator.PreviewNodePlacement(
                world,
                SubstationClassId,
                new MapPoint(130, 100)),
            "node-footprint tangency");

        NodePlacementPreview risky = PlacementValidator.PreviewNodePlacement(
            world,
            SubstationClassId,
            new MapPoint(1200, 1200));
        Check(risky.Accepted, "risk area incorrectly blocked placement");
        SequenceEqual(["A_RISK", "Z_RISK"], risky.RiskAreaIds,
            "risk identifiers must be stable and sorted");
        ExpectThrows<NotSupportedException>(
            () => ((IList<string>)risky.RiskAreaIds).Add("MUTATION"),
            "risk preview collection must be immutable");

        SpatialWorldDefinition edgeWorld = World(
            [Node("A", 100, 200), Node("B", 700, 200, LoadClassId)],
            [Edge("E", "A", "B")]);
        SpatialWorldLoader.Validate(edgeWorld);
        Error(
            ConstructionError.ExistingLineTouch,
            PlacementValidator.PreviewNodePlacement(
                edgeWorld,
                SubstationClassId,
                new MapPoint(400, 220)),
            "new footprint touching existing line body");
    }

    private void CheckLineGeometryAndRisk()
    {
        SpatialWorldDefinition thirdNodeWorld = World(
        [
            Node("A", 100, 100),
            Node("B", 500, 100, LoadClassId),
            Node("C", 300, 110, PoleClassId),
            Node("D", 800, 100, LoadClassId),
        ]);
        SpatialWorldLoader.Validate(thirdNodeWorld);
        LineDraftSnapshot thirdNodeDraft = Draft("A");
        Error(
            ConstructionError.ZeroLengthSegment,
            PlacementValidator.PreviewLinePoint(
                thirdNodeWorld,
                thirdNodeDraft,
                new MapPoint(100, 100)),
            "positive intermediate segment length");
        Error(
            ConstructionError.ThirdNodeTouch,
            PlacementValidator.PreviewLineFinish(thirdNodeWorld, thirdNodeDraft, "B"),
            "third-node footprint tangency");
        Error(
            ConstructionError.SpanTooLong,
            PlacementValidator.PreviewLineFinish(thirdNodeWorld, thirdNodeDraft, "D"),
            "maximum span");
        Error(
            ConstructionError.SameEndpoint,
            PlacementValidator.PreviewLineFinish(thirdNodeWorld, thirdNodeDraft, "A"),
            "explicit same endpoint");

        SpatialWorldDefinition duplicateWorld = World(
            [Node("A", 100, 100), Node("B", 500, 100, LoadClassId)],
            [Edge("E", "A", "B")]);
        SpatialWorldLoader.Validate(duplicateWorld);
        Error(
            ConstructionError.DuplicateSegment,
            PlacementValidator.PreviewLineFinish(duplicateWorld, Draft("B"), "A"),
            "unordered duplicate endpoints");

        SpatialWorldDefinition overlapWorld = World(
        [
            Node("A", 100, 100),
            Node("B", 700, 100, LoadClassId),
            Node("C", 300, 100, PoleClassId),
            Node("D", 500, 100, PoleClassId),
        ],
        [Edge("EXISTING", "C", "D")]);
        SpatialWorldLoader.Validate(overlapWorld);
        Error(
            ConstructionError.CollinearOverlap,
            PlacementValidator.PreviewLineFinish(overlapWorld, Draft("A"), "B"),
            "positive collinear overlap");

        SpatialWorldDefinition buildingWorld = World(
            [Node("A", 100, 500), Node("B", 700, 500, LoadClassId)],
            terrain: [Terrain("BLOCK", TerrainKind.Building, 300, 400, 500, 600)]);
        SpatialWorldLoader.Validate(buildingWorld);
        Error(
            ConstructionError.BuildingCrossing,
            PlacementValidator.PreviewLineFinish(buildingWorld, Draft("A"), "B"),
            "building crossing");

        SpatialWorldDefinition waterWorld = World(
            [Node("A", 100, 500), Node("B", 700, 500, LoadClassId)],
            terrain: [Terrain("RIVER", TerrainKind.Water, 300, 400, 500, 600)]);
        SpatialWorldLoader.Validate(waterWorld);
        Check(PlacementValidator.PreviewLineFinish(waterWorld, Draft("A"), "B").Accepted,
            "line crossing water was rejected");

        SpatialWorldDefinition riskWorld = World(
            [Node("A", 800, 1200), Node("B", 1400, 1200, LoadClassId)],
            risks:
            [
                Risk("Z_RISK", 1000, 1100, 1300, 1300),
                Risk("A_RISK", 1050, 1050, 1250, 1350),
            ]);
        SpatialWorldLoader.Validate(riskWorld);
        LineFinishPreview riskPreview = PlacementValidator.PreviewLineFinish(
            riskWorld,
            Draft("A"),
            "B");
        Check(riskPreview.Accepted, "risk area incorrectly blocked line");
        SequenceEqual(["A_RISK", "Z_RISK"], riskPreview.RiskAreaIds,
            "line risk identifiers must be stable and sorted");

        SpatialWorldDefinition draftContactWorld = World(
        [
            Node("A", 100, 100),
            Node("B", 100, 900, LoadClassId),
        ]);
        var draftContact = new ConstructionSession(draftContactWorld);
        Accepted(draftContact.StartLineDraft("A", LineClassId, PoleClassId),
            "start draft-contact path");
        Accepted(draftContact.AddLinePoint(new MapPoint(500, 100)),
            "draft-contact first point");
        Accepted(draftContact.AddLinePoint(new MapPoint(500, 500)),
            "draft-contact second point");
        Error(
            ConstructionError.ThirdNodeTouch,
            draftContact.PreviewLinePoint(new MapPoint(300, 110)),
            "new pole footprint touching non-adjacent draft segment");
        string beforeBadPoint = JsonSerializer.Serialize(draftContact.GetSnapshot());
        ConstructionCommandResult badPoint = draftContact.AddLinePoint(new MapPoint(300, 110));
        Check(!badPoint.Accepted && badPoint.Error == ConstructionError.ThirdNodeTouch,
            "draft-segment contact preview/command mismatch");
        Equal(beforeBadPoint, JsonSerializer.Serialize(draftContact.GetSnapshot()),
            "rejected draft-segment contact changed the draft");
        Accepted(draftContact.AddLinePoint(new MapPoint(100, 500)),
            "draft-contact third point");
        string beforeMove = JsonSerializer.Serialize(draftContact.GetSnapshot());
        LinePointMovePreview badMove = draftContact.PreviewMoveLinePoint(
            1,
            new MapPoint(300, 110));
        Check(!badMove.Accepted && badMove.Error == ConstructionError.ThirdNodeTouch,
            "moved pole contact must validate the whole candidate path");
        ConstructionCommandResult rejectedMove = draftContact.MoveLinePoint(
            1,
            new MapPoint(300, 110));
        Check(!rejectedMove.Accepted && rejectedMove.Error == badMove.Error,
            "move preview/command error mismatch");
        Equal(beforeMove, JsonSerializer.Serialize(draftContact.GetSnapshot()),
            "rejected pole move changed the draft");
    }

    private void CheckConstructionLifecycle()
    {
        SpatialWorldDefinition nodeWorld = World([Node("A", 100, 100)]);
        var nodeSession = new ConstructionSession(nodeWorld);
        Accepted(nodeSession.SetNodeDraft(SubstationClassId, new MapPoint(500, 500)),
            "set node draft");
        Accepted(nodeSession.SetNodeDraft(SubstationClassId, new MapPoint(600, 500)),
            "move node draft");
        Equal(1, nodeSession.GetSnapshot().World.Nodes.Count,
            "drafting must not create a node");
        Accepted(nodeSession.CancelNodeDraft(), "cancel node draft");
        Equal(ConstructionPhase.Ready, nodeSession.GetSnapshot().Phase,
            "node cancel phase");

        Accepted(nodeSession.SetNodeDraft(SubstationClassId, new MapPoint(600, 500)),
            "set final node draft");
        ConstructionQuote nodeQuote = nodeSession.PreviewNodeOrder();
        Quote(nodeQuote, 100, 10, 10, "node quote");
        Accepted(nodeSession.OrderNode(), "order node");
        ConstructionSnapshot nodeOrdered = nodeSession.GetSnapshot();
        Equal(ConstructionPhase.NodeBuilding, nodeOrdered.Phase, "node building phase");
        Check(!nodeOrdered.World.Nodes.Single(node => node.NodeId == "PLAYER_SUBSTATION_1").Commissioned,
            "ordered node commissioned before completion");
        Accepted(nodeSession.AdvanceToConstructionCompletion(), "complete node");
        ConstructionSnapshot nodeComplete = nodeSession.GetSnapshot();
        Equal(10L, nodeComplete.Minute, "node completion minute");
        Check(nodeComplete.World.Nodes.Single(node => node.NodeId == "PLAYER_SUBSTATION_1").Commissioned,
            "completed node remains uncommissioned");

        SpatialWorldDefinition lineWorld = World(
            [Node("A", 100, 100), Node("B", 700, 100, LoadClassId)],
            risks: [Risk("WORK_RISK", 350, 50, 450, 150)]);
        var lineSession = new ConstructionSession(lineWorld);
        Accepted(lineSession.StartLineDraft("A", LineClassId, PoleClassId),
            "start explicit-node line draft");
        LinePointPreview pointPreview = lineSession.PreviewLinePoint(new MapPoint(400, 100));
        Check(pointPreview.Accepted, "valid intermediate preview rejected");
        SequenceEqual(["WORK_RISK"], pointPreview.RiskAreaIds,
            "intermediate risk exposure");
        Accepted(lineSession.AddLinePoint(new MapPoint(400, 100)), "add intermediate point");
        Accepted(lineSession.FinishLineDraft("B"), "finish at explicit node");
        LinePointMovePreview movePreview = lineSession.PreviewMoveLinePoint(
            0,
            new MapPoint(400, 120));
        Check(movePreview.Accepted, "valid completed-draft pole move was rejected");
        Equal(301L, movePreview.PreviousSegmentLengthUnit,
            "moved pole previous segment length");
        Equal(301L, movePreview.NextSegmentLengthUnit,
            "moved pole next segment length");
        Accepted(lineSession.MoveLinePoint(0, new MapPoint(400, 120)),
            "move pole before order");
        Equal(new MapPoint(400, 120), lineSession.GetSnapshot().LineDraft!.IntermediatePoints[0],
            "moved pole coordinate");
        Accepted(lineSession.MoveLinePoint(0, new MapPoint(400, 100)),
            "restore pole before undo checks");
        Accepted(lineSession.UndoLinePoint(), "undo explicit end");
        Error(ConstructionError.DraftIncomplete, lineSession.PreviewLineOrder(),
            "unfinished draft quote");
        Accepted(lineSession.UndoLinePoint(), "undo intermediate point");
        Equal(0, lineSession.GetSnapshot().LineDraft!.IntermediatePoints.Count,
            "intermediate undo count");
        ConstructionCommandResult emptyUndo = lineSession.UndoLinePoint();
        Check(!emptyUndo.Accepted && emptyUndo.Error == ConstructionError.NothingToUndo,
            "empty line undo must be typed rejection");
        Accepted(lineSession.CancelLineDraft(), "cancel line draft");

        Accepted(lineSession.StartLineDraft("A", LineClassId, PoleClassId),
            "restart line draft");
        Accepted(lineSession.AddLinePoint(new MapPoint(400, 100)),
            "add final intermediate point");
        Accepted(lineSession.FinishLineDraft("B"), "finish final line draft");
        ConstructionQuote lineQuote = lineSession.PreviewLineOrder();
        Quote(lineQuote, 80, 15, 15, "segmented line quote");
        SequenceEqual(["WORK_RISK"], lineQuote.RiskAreaIds, "line quote risk exposure");
        Accepted(lineSession.OrderLine(), "order line");
        ConstructionSnapshot ordered = lineSession.GetSnapshot();
        Equal(ConstructionPhase.LineBuilding, ordered.Phase, "line building phase");
        Equal(3, ordered.World.Nodes.Count, "atomic order node count");
        Equal(2, ordered.World.Edges.Count, "atomic order edge count");
        Check(ordered.ActiveConstruction!.NodeIds.All(id =>
                !ordered.World.Nodes.Single(node => node.NodeId == id).Commissioned),
            "ordered poles must all remain uncommissioned");
        Check(ordered.ActiveConstruction.EdgeIds.All(id =>
                !ordered.World.Edges.Single(edge => edge.EdgeId == id).Commissioned),
            "ordered edges must all remain uncommissioned");
        SequenceEqual(["WORK_RISK"], ordered.ActiveConstruction.RiskAreaIds,
            "ordered risk exposure");

        Accepted(lineSession.AdvanceToConstructionCompletion(), "atomic line completion");
        ConstructionSnapshot complete = lineSession.GetSnapshot();
        Equal(15L, complete.Minute, "line completion minute");
        Equal(ConstructionPhase.Ready, complete.Phase, "line completion phase");
        Check(complete.World.Nodes.Where(node => node.NodeId.StartsWith("PLAYER_", StringComparison.Ordinal))
                .All(node => node.Commissioned),
            "line completion left a player node uncommissioned");
        Check(complete.World.Edges.All(edge => edge.Commissioned),
            "line completion left an edge uncommissioned");

        SpatialWorldDefinition shortSegmentsWorld = World(
            [Node("A", 100, 100), Node("B", 200, 100, LoadClassId)]);
        var shortSegments = new ConstructionSession(shortSegmentsWorld);
        Accepted(shortSegments.StartLineDraft("A", LineClassId, PoleClassId),
            "start short-segment quote");
        Accepted(shortSegments.AddLinePoint(new MapPoint(150, 100)),
            "add short-segment pole");
        Accepted(shortSegments.FinishLineDraft("B"), "finish short-segment line");
        Quote(shortSegments.PreviewLineOrder(), 55, 5, 5,
            "path-level design-unit rounding");
    }

    private void CheckRejectedInvarianceAndDeterminism()
    {
        SpatialWorldDefinition world = World(
            [Node("A", 100, 100), Node("B", 500, 300, LoadClassId)]);
        var session = new ConstructionSession(world);
        AssertRejectedPreserves(
            session,
            () => session.SetNodeDraft(SubstationClassId, new MapPoint(5, 5)),
            ConstructionError.OutsideBounds,
            "rejected node set");

        Accepted(session.StartLineDraft("A", LineClassId, PoleClassId), "start invariant draft");
        AssertRejectedPreserves(
            session,
            () => session.MoveLinePoint(0, new MapPoint(200, 200)),
            ConstructionError.InvalidPointIndex,
            "invalid pole index");
        LinePointPreview preview = session.PreviewLinePoint(new MapPoint(100, 100));
        AssertRejectedPreserves(
            session,
            () => session.AddLinePoint(new MapPoint(100, 100)),
            preview.Error!.Value,
            "preview/command zero-length parity");
        AssertRejectedPreserves(
            session,
            () => session.FinishLineDraft("MISSING"),
            ConstructionError.EndpointNotFound,
            "unknown explicit endpoint");
        AssertRejectedPreserves(
            session,
            session.OrderLine,
            ConstructionError.DraftIncomplete,
            "incomplete line order");

        string first = ExecuteReplay(world);
        string second = ExecuteReplay(world);
        Equal(first, second, "identical command replay must be deterministic");
    }

    private void CheckCrossingNonConnectionAndReplay()
    {
        SpatialWorldDefinition crossingWorld = World(
        [
            Node("A", 100, 500),
            Node("B", 700, 500, LoadClassId),
            Node("C", 400, 200),
            Node("D", 400, 800, LoadClassId),
        ],
        [Edge("VERTICAL", "C", "D")]);
        SpatialWorldLoader.Validate(crossingWorld);

        var session = new ConstructionSession(crossingWorld);
        Accepted(session.StartLineDraft("A", LineClassId, PoleClassId),
            "start crossing line");
        Check(session.PreviewLineFinish("B").Accepted,
            "noncollinear line crossing was rejected");
        Accepted(session.FinishLineDraft("B"), "finish crossing line");
        Accepted(session.OrderLine(), "order crossing line");
        Accepted(session.AdvanceToConstructionCompletion(), "complete crossing line");
        SpatialWorldDefinition completed = session.GetSnapshot().World;
        Equal(4, completed.Nodes.Count, "crossing created an implicit node");
        Check(!completed.Nodes.Any(node => node.Position == new MapPoint(400, 500)),
            "crossing intersection became a node");
        Check(!Reachable(completed, "A", "C"),
            "crossing lines became electrically connected");

        SpatialWorldDefinition replayWorld = World(
            [Node("A", 100, 100), Node("B", 500, 300, LoadClassId)]);
        ConstructionSnapshot replay = ExecuteReplaySnapshot(replayWorld);
        SpatialNodeDefinition pole = replay.World.Nodes.Single(node =>
            node.NodeId == "PLAYER_POLE_1");
        Equal(new MapPoint(250, 200), pole.Position,
            "replay must preserve exact fixed-point coordinates");
        Equal("A", replay.World.Edges.Single(edge => edge.EdgeId == "PLAYER_EDGE_1").FromNodeId,
            "replay first edge start identifier");
        Equal("PLAYER_POLE_1",
            replay.World.Edges.Single(edge => edge.EdgeId == "PLAYER_EDGE_1").ToNodeId,
            "replay first edge end identifier");
    }

    private void CheckStrictCommercialWorldLoader()
    {
        CommercialWorldDefinition fromText = CommercialWorldLoader.Load(_commercialJson);
        CommercialWorldDefinition fromBytes = CommercialWorldLoader.Load(_commercialBytes);
        Equal(_commercialWorld.WorldId, fromText.WorldId, "commercial text loader world ID");
        Equal(_commercialWorld.WorldId, fromBytes.WorldId, "commercial byte loader world ID");
        Equal(2, _commercialWorld.GenerationSources.Count, "commercial source count");
        Check(_commercialWorld.Spatial.Edges.Count > 0, "final world must contain an initial network");
        Check(CommercialWorldLoader.ThermalAssetIds(_commercialWorld).Contains("NORTH_SUBSTATION"),
            "substation thermal asset missing from final world");

        string trimmed = _commercialJson.TrimStart();
        ExpectCommercialRejected(
            "duplicate commercial JSON property",
            $"{{\"schemaVersion\":\"duplicate\",{trimmed[1..]}");
        ExpectCommercialRejected("unknown commercial root field", root => root["unexpected"] = true);
        ExpectCommercialRejected("missing commercial world ID", root => root.Remove("worldId"));
        ExpectCommercialRejected(
            "mismatched spatial world ID",
            root => Object(root["spatial"]!)["worldId"] = "OTHER_WORLD");
        ExpectCommercialRejected(
            "zero continuous limit",
            root => Object(JsonArrayProperty(root, "thermalNodeClasses")[0]!)["continuousLimitKw"] = 0);
        ExpectCommercialRejected(
            "continuous above emergency",
            root =>
            {
                JsonObject thermalClass = Object(JsonArrayProperty(root, "thermalLineClasses")[0]!);
                thermalClass["continuousLimitKw"] = 9000;
                thermalClass["emergencyLimitKw"] = 8000;
            });
        ExpectCommercialRejected(
            "thermal source terminal class",
            root => Object(JsonArrayProperty(root, "thermalNodeClasses")[0]!)["classId"] =
                "SOURCE_TERMINAL");
        ExpectCommercialRejected(
            "duplicate source authored order",
            root => Object(JsonArrayProperty(root, "generationSources")[1]!)["authoredOrder"] = 0);
    }

    private void CheckStrictCommercialCoreLoader()
    {
        CommercialCoreSliceDefinition fromText = CommercialCoreLoader.Load(
            _coreJson,
            _commercialWorld);
        CommercialCoreSliceDefinition fromBytes = CommercialCoreLoader.Load(
            _coreBytes,
            _commercialWorld);
        Equal(_coreSlice.SliceId, fromText.SliceId, "core slice text loader ID");
        Equal(_coreSlice.SliceId, fromBytes.SliceId, "core slice byte loader ID");
        Equal(2, _coreSlice.Chapters.Count, "Stage-D exact chapter count");
        Equal(2, _coreSlice.Chapters[1].DecisionWindows.Count, "core decision-window count");

        string trimmed = _coreJson.TrimStart();
        ExpectCoreRejected(
            "duplicate core JSON property",
            $"{{\"schemaVersion\":\"duplicate\",{trimmed[1..]}");
        ExpectCoreRejected("unknown core root field", root => root["future"] = true);
        ExpectCoreRejected("missing core slice ID", root => root.Remove("sliceId"));
        ExpectCoreRejected(
            "wrong core world ID",
            root => root["worldId"] = "OTHER_WORLD");
        ExpectCoreRejected(
            "future campaign placeholder chapter",
            root => JsonArrayProperty(root, "chapters").Add(
                JsonArrayProperty(root, "chapters")[1]!.DeepClone()));
        ExpectCoreRejected(
            "unknown seed node",
            root => JsonArrayProperty(
                Object(JsonArrayProperty(root, "chapters")[0]!),
                "seedNodeIds").Add("UNKNOWN_NODE"));
        ExpectCoreRejected(
            "seed edge endpoint omitted",
            root => JsonArrayProperty(
                Object(JsonArrayProperty(root, "chapters")[1]!),
                "seedNodeIds").RemoveAt(0));
        ExpectCoreRejected(
            "zero window allowance",
            root => Object(JsonArrayProperty(
                Object(JsonArrayProperty(root, "chapters")[0]!),
                "decisionWindows")[0]!)["buildMinutesAllowance"] = 0);
        ExpectCoreRejected(
            "unknown next phase",
            root => Object(JsonArrayProperty(
                Object(JsonArrayProperty(root, "chapters")[1]!),
                "decisionWindows")[0]!)["nextPhaseId"] = "UNKNOWN_PHASE");
        ExpectCoreRejected(
            "integer chapter kind",
            root => Object(JsonArrayProperty(root, "chapters")[0]!)["kind"] = 0);
        ExpectCoreRejected(
            "integer phase policy",
            root => Object(JsonArrayProperty(
                Object(JsonArrayProperty(root, "chapters")[0]!),
                "operatingPhases")[0]!)["policy"] = 0);
        ExpectCoreRejected(
            "promise without deferred result",
            root => Object(JsonArrayProperty(root, "chapters")[1]!)["deferredResult"] = null);
        ExpectCoreRejected(
            "unknown phase risk area",
            root => JsonArrayProperty(
                Object(JsonArrayProperty(
                    Object(JsonArrayProperty(root, "chapters")[1]!),
                    "operatingPhases")[0]!),
                "activeRiskAreaIds").Add("UNKNOWN_RISK"));
        ExpectCoreRejected(
            "load on source terminal",
            root => Object(JsonArrayProperty(
                Object(JsonArrayProperty(
                    Object(JsonArrayProperty(root, "chapters")[0]!),
                    "operatingPhases")[0]!),
                    "loads")[0]!)["nodeId"] = "WEST_SOURCE");
        ExpectCoreRejected(
            "duplicate load ID across phases",
            root => Object(JsonArrayProperty(
                Object(JsonArrayProperty(
                    Object(JsonArrayProperty(root, "chapters")[1]!),
                    "operatingPhases")[1]!),
                    "loads")[0]!)["loadId"] = "HOSPITAL_DUTY");
    }

    private void CheckStrictCommercialCampaignLoader()
    {
        CommercialCampaignDefinition fromText = CommercialCampaignLoader.Load(
            _campaignJson,
            _commercialWorld);
        CommercialCampaignDefinition fromBytes = CommercialCampaignLoader.Load(
            _campaignBytes,
            _commercialWorld);
        Equal(_campaign.CampaignId, fromText.CampaignId, "campaign text loader ID");
        Equal(_campaign.CampaignId, fromBytes.CampaignId, "campaign byte loader ID");
        Equal(8, _campaign.Chapters.Count, "final exact chapter count");
        Check(_campaign.Chapters.Select(item => item.ChapterId).SequenceEqual(
                new[]
                {
                    "FIRST_LIGHT", "SECOND_HEART", "SECOND_SOURCE", "NORTH_BANK_PROMISE",
                    "WHOSE_MARGIN", "BEFORE_WATER_REACHES", "SHUT_DOWN_TO_KEEP", "LONGEST_NIGHT",
                },
                StringComparer.Ordinal),
            "final authored chapter order");
        Check(_campaign.Chapters.Take(4).SelectMany(item => item.OperatingPhases).All(phase =>
                phase.Policy == ThermalIntervalPolicy.ContinuousOnly),
            "Stage-E opened emergency thermal permission before mission five");
        Check(_campaign.Chapters[4].OperatingPhases.Any(phase =>
                phase.Policy == ThermalIntervalPolicy.SafetyEmergencyAllowed),
            "mission five did not open the authored emergency permission boundary");
        Check(_campaign.Chapters[5].OperatingPhases.Single().Policy ==
                ThermalIntervalPolicy.SafetyEmergencyAllowed &&
            _campaign.Chapters[5].OperatingPhases.Single().Loads.Any(load =>
                load.NamedEmergencyDuty),
            "mission six did not recombine the established named-emergency permission");

        string trimmed = _campaignJson.TrimStart();
        ExpectCampaignRejected(
            "duplicate campaign JSON property",
            $"{{\"schemaVersion\":\"duplicate\",{trimmed[1..]}");
        ExpectCampaignRejected("unknown campaign root field", root => root["future"] = true);
        ExpectCampaignRejected("missing campaign ID", root => root.Remove("campaignId"));
        ExpectCampaignRejected("missing campaign epilogue", root => root.Remove("epilogue"));
        ExpectCampaignRejected(
            "wrong campaign world ID",
            root => root["worldId"] = "OTHER_WORLD");
        ExpectCampaignRejected(
            "future mission placeholder",
            root => JsonArrayProperty(root, "chapters").Add(
                JsonArrayProperty(root, "chapters")[3]!.DeepClone()));
        ExpectCampaignRejected(
            "later cash reset",
            root => Object(JsonArrayProperty(root, "chapters")[1]!)["seedCashUnit"] = 1);
        ExpectCampaignRejected(
            "emergency permission before mission five",
            root => Object(JsonArrayProperty(
                Object(JsonArrayProperty(root, "chapters")[2]!),
                "operatingPhases")[0]!)["policy"] = "SafetyEmergencyAllowed");
        ExpectCampaignRejected(
            "mission five without emergency permission",
            root => Object(JsonArrayProperty(
                Object(JsonArrayProperty(root, "chapters")[4]!),
                "operatingPhases")[0]!)["policy"] = "ContinuousOnly");
        ExpectCampaignRejected(
            "long-gap reset at opening",
            root => Object(JsonArrayProperty(root, "chapters")[0]!)[
                "resetThermalMemoryAtStart"] = true);
        ExpectCampaignRejected(
            "multiple long-gap resets",
            root => Object(JsonArrayProperty(root, "chapters")[5]!)[
                "resetThermalMemoryAtStart"] = true);
        ExpectCampaignRejected(
            "missing authored long-gap reset",
            root => Object(JsonArrayProperty(root, "chapters")[6]!)[
                "resetThermalMemoryAtStart"] = false);
        ExpectCampaignRejected(
            "direct terminal service allowed",
            root => Object(JsonArrayProperty(
                Object(JsonArrayProperty(
                    Object(JsonArrayProperty(root, "chapters")[0]!),
                    "operatingPhases")[0]!),
                "loads")[0]!)["requireSubstationPath"] = false);
    }

    private void CheckCommercialAuthoredCopy()
    {
        string contextPath = Path.Combine(
            _repositoryDirectory,
            "tools",
            "commercial-ux",
            "text-plan-context.json");
        string contextJson = File.ReadAllText(contextPath);
        using JsonDocument contextDocument = JsonDocument.Parse(contextJson);
        JsonElement[] contextChapters = contextDocument.RootElement
            .GetProperty("chapters")
            .EnumerateArray()
            .ToArray();
        Equal(8, contextChapters.Length, "text-plan authored chapter count");

        string ContextField(string chapterId, string fieldName)
        {
            JsonElement chapter = contextChapters.Single(item =>
                item.GetProperty("chapterId").GetString() == chapterId);
            return chapter.GetProperty(fieldName).GetString()!;
        }

        var campaignCopy = new List<string>();
        void AddStory(CommercialStoryCard? story)
        {
            if (story is null)
            {
                return;
            }

            campaignCopy.Add(story.Speaker);
            campaignCopy.Add(story.Title);
            campaignCopy.Add(story.Body);
        }

        foreach (CommercialCoreChapter chapter in _campaign.Chapters)
        {
            campaignCopy.Add(chapter.DisplayName);
            AddStory(chapter.Briefing);
            campaignCopy.Add(chapter.Objective);
            if (chapter.Promise is not null)
            {
                campaignCopy.Add(chapter.Promise.DisplayName);
            }
            foreach (CommercialCoreDecisionWindow window in chapter.DecisionWindows)
            {
                AddStory(window.Story);
            }
            foreach (CommercialCoreOperatingPhase phase in chapter.OperatingPhases)
            {
                campaignCopy.Add(phase.DisplayName);
                campaignCopy.AddRange(phase.Loads.Select(load => load.DisplayName));
            }
            AddStory(chapter.StandardResult);
            AddStory(chapter.KeptResult);
            AddStory(chapter.DeferredResult);
        }
        AddStory(_campaign.Epilogue);

        string authoredCopy = string.Join('\n', campaignCopy) + '\n' + contextJson;
        string[] forbiddenPhrases =
        [
            "draft",
            "reset",
            "장간 시간경과",
            "작성된 정비 시간",
            "사용불가",
        ];
        foreach (string phrase in forbiddenPhrases)
        {
            Check(authoredCopy.IndexOf(phrase, StringComparison.OrdinalIgnoreCase) < 0,
                $"authored copy still contains forbidden phrase '{phrase}'");
        }

        CommercialCoreChapter firstLight = _campaign.Chapters.Single(chapter =>
            chapter.ChapterId == "FIRST_LIGHT");
        Check(firstLight.Briefing.Body.Contains("건설·조작 순서", StringComparison.Ordinal) &&
            firstLight.Briefing.Body.Contains("단계별로 안내", StringComparison.Ordinal),
            "first-light briefing did not introduce staged construction/control guidance");
        string firstLightLearning = ContextField("FIRST_LIGHT", "learningIntent");
        Check(firstLightLearning.Contains("건설·조작 순서 안내", StringComparison.Ordinal),
            "first-light text plan omitted construction/control sequence guidance");
        Check(ContextField("FIRST_LIGHT", "choiceIntent")
                .Contains("작성 중 계획", StringComparison.Ordinal),
            "first-light text plan omitted the Korean in-progress-plan term");

        CommercialCoreChapter secondHeart = _campaign.Chapters.Single(chapter =>
            chapter.ChapterId == "SECOND_HEART");
        Check(secondHeart.Briefing.Body.Contains("건설·조작 순서는 되풀이하지 않고",
                StringComparison.Ordinal) &&
            secondHeart.Briefing.Body.Contains("차단시험", StringComparison.Ordinal) &&
            secondHeart.Briefing.Body.Contains("경로를 확인하는 안내만", StringComparison.Ordinal),
            "second-heart briefing did not retain route-test-only guidance");
        string secondHeartLearning = ContextField("SECOND_HEART", "learningIntent");
        Check(secondHeartLearning.Contains("건설·조작 순서를 되풀이하지 않고",
                StringComparison.Ordinal) &&
            secondHeartLearning.Contains("경로 시험 안내만 유지", StringComparison.Ordinal),
            "second-heart text plan did not stage the route-test-only guidance");

        CommercialCoreChapter secondSource = _campaign.Chapters.Single(chapter =>
            chapter.ChapterId == "SECOND_SOURCE");
        string tutorialHandoff = secondSource.StandardResult.Body;
        Check(tutorialHandoff.Contains("인수시험", StringComparison.Ordinal) &&
            tutorialHandoff.Contains("건설·조작 순서", StringComparison.Ordinal) &&
            tutorialHandoff.Contains("경로 시험 안내", StringComparison.Ordinal) &&
            tutorialHandoff.Contains("여기서 끝납니다", StringComparison.Ordinal),
            "second-source result did not identify the guidance that ends after acceptance");
        Check(tutorialHandoff.Contains("각 장의 목표", StringComparison.Ordinal) &&
            tutorialHandoff.Contains("필수 공급 의무", StringComparison.Ordinal) &&
            tutorialHandoff.Contains("도시 약속", StringComparison.Ordinal) &&
            tutorialHandoff.Contains("다음 경계 예보", StringComparison.Ordinal) &&
            tutorialHandoff.Contains("계속 표시", StringComparison.Ordinal),
            "second-source result did not identify the information retained after tutorial handoff");
        string secondSourceLearning = ContextField("SECOND_SOURCE", "learningIntent");
        Check(secondSourceLearning.Contains("건설·조작 순서와 경로 시험 안내가 끝나지만",
                StringComparison.Ordinal) &&
            secondSourceLearning.Contains("장별 목표", StringComparison.Ordinal) &&
            secondSourceLearning.Contains("필수 공급 의무", StringComparison.Ordinal) &&
            secondSourceLearning.Contains("다음 경계 예보", StringComparison.Ordinal),
            "second-source text plan did not preserve the staged tutorial handoff facts");

        CommercialCoreChapter flood = _campaign.Chapters.Single(chapter =>
            chapter.ChapterId == "BEFORE_WATER_REACHES");
        string floodWindow = flood.DecisionWindows.Single(window =>
            window.WindowId == "FLOOD_BYPASS_BUILD").Story!.Body;
        Check(floodWindow.Contains("사용 불가", StringComparison.Ordinal),
            "flood window omitted the spaced Korean unavailable label");

        CommercialCoreChapter maintenance = _campaign.Chapters.Single(chapter =>
            chapter.ChapterId == "SHUT_DOWN_TO_KEEP");
        Check(maintenance.Briefing.Body.Contains("3주가 지나", StringComparison.Ordinal),
            "maintenance briefing omitted the natural long-gap wording");
        string maintenanceWindow = maintenance.DecisionWindows.Single(window =>
            window.WindowId == "MAINTENANCE_BYPASS_BUILD").Story!.Body;
        Check(maintenanceWindow.Contains("예정된 정비 시간", StringComparison.Ordinal),
            "maintenance window omitted the scheduled-maintenance wording");
        string maintenanceLearning = ContextField("SHUT_DOWN_TO_KEEP", "learningIntent");
        Check(maintenanceLearning.Contains("초기화", StringComparison.Ordinal) &&
            maintenanceLearning.Contains("복귀", StringComparison.Ordinal),
            "maintenance text plan omitted the Korean reset/return terms");

        CommercialCoreChapter finale = _campaign.Chapters.Single(chapter =>
            chapter.ChapterId == "LONGEST_NIGHT");
        CommercialStoryCard? stormStory = finale.DecisionWindows.Single(window =>
            window.WindowId == "LAST_STORM_APPROVAL").Story;
        Check(stormStory is not null, "last-storm approval story is missing");
        string finalePreResult = finale.Briefing.Body + '\n' + stormStory!.Body;
        Check(finalePreResult.Contains("첫 폭염 경계", StringComparison.Ordinal) &&
            finalePreResult.Contains("2.6 MW", StringComparison.Ordinal) &&
            finalePreResult.Contains("지킬지", StringComparison.Ordinal) &&
            finalePreResult.Contains("미룰지", StringComparison.Ordinal),
            "finale pre-result story did not introduce the 2.6 MW keep/defer promise choice");
        Check(finale.Objective.Contains("첫 폭염 경계", StringComparison.Ordinal) &&
            finale.Objective.Contains("2.6 MW", StringComparison.Ordinal) &&
            finale.Objective.Contains("지킬지 미룰지", StringComparison.Ordinal),
            "finale objective did not preserve the timed 2.6 MW promise decision");
        string finaleChoice = ContextField("LONGEST_NIGHT", "choiceIntent");
        Check(finaleChoice.Contains("첫 폭염 경계", StringComparison.Ordinal) &&
            finaleChoice.Contains("2.6 MW", StringComparison.Ordinal) &&
            finaleChoice.Contains("지킬지 미룰지", StringComparison.Ordinal),
            "finale text plan omitted the timed 2.6 MW promise decision");

        Check(finale.Promise is not null, "finale promise is missing");
        CommercialCorePromise finalePromise = finale.Promise!;
        CommercialCoreOperatingPhase[] promiseLoadPhases = finale.OperatingPhases
            .Where(phase => phase.Loads.Any(load => load.LoadId == finalePromise.LoadId))
            .ToArray();
        SequenceEqual(
            new[] { "LAST_HEAT" },
            promiseLoadPhases.Select(phase => phase.PhaseId).ToArray(),
            "finale promise load phase placement");
        CommercialCoreLoadBundle finalePromiseLoad = promiseLoadPhases.Single().Loads.Single(load =>
            load.LoadId == finalePromise.LoadId);
        Equal(2600L, finalePromiseLoad.DemandKw, "finale promise load demand");
    }

    private void CheckCommercialStoryPartHarness()
    {
        IReadOnlyList<CommercialStoryPart> parts = _storyParts.Parts;
        string[] expectedSelectors =
        [
            "FIRST_LIGHT/briefing",
            "FIRST_LIGHT/result/standard",
            "SECOND_HEART/briefing",
            "SECOND_HEART/result/standard",
            "SECOND_SOURCE/briefing",
            "SECOND_SOURCE/window/SECOND_SOURCE_BUILD",
            "SECOND_SOURCE/result/standard",
            "NORTH_BANK_PROMISE/briefing",
            "NORTH_BANK_PROMISE/result/keep",
            "NORTH_BANK_PROMISE/result/defer",
            "WHOSE_MARGIN/briefing",
            "WHOSE_MARGIN/window/AFTER_HEAT_SAFETY",
            "WHOSE_MARGIN/result/keep",
            "WHOSE_MARGIN/result/defer",
            "BEFORE_WATER_REACHES/briefing",
            "BEFORE_WATER_REACHES/window/FLOOD_BYPASS_BUILD",
            "BEFORE_WATER_REACHES/result/standard",
            "SHUT_DOWN_TO_KEEP/briefing",
            "SHUT_DOWN_TO_KEEP/window/MAINTENANCE_BYPASS_BUILD",
            "SHUT_DOWN_TO_KEEP/result/keep",
            "SHUT_DOWN_TO_KEEP/result/defer",
            "LONGEST_NIGHT/briefing",
            "LONGEST_NIGHT/window/LAST_STORM_APPROVAL",
            "LONGEST_NIGHT/result/keep",
            "LONGEST_NIGHT/result/defer",
            "campaign/epilogue",
        ];

        Equal(26, parts.Count, "story-part reachable selector count");
        SequenceEqual(
            expectedSelectors,
            parts.Select(part => part.Selector).ToArray(),
            "story-part canonical authored order");
        Equal(
            parts.Count,
            parts.Select(part => part.Selector).Distinct(StringComparer.Ordinal).Count(),
            "story-part selector uniqueness");
        Equal(8, parts.Count(part => part.Kind == CommercialStoryPartKind.Briefing),
            "story-part briefing count");
        Equal(5, parts.Count(part => part.Kind == CommercialStoryPartKind.Window),
            "story-part non-null window count");
        Equal(12, parts.Count(part => part.Kind == CommercialStoryPartKind.Result),
            "story-part reachable result count");
        Equal(1, parts.Count(part => part.Kind == CommercialStoryPartKind.Epilogue),
            "story-part epilogue count");
        Equal(4, parts.Count(part => part.Kind == CommercialStoryPartKind.Result &&
                part.RequiredPromiseBranch is null),
            "story-part standard no-promise result count");
        Equal(4, parts.Count(part => part.RequiredPromiseBranch == PromiseDecision.Keep),
            "story-part keep result count");
        Equal(4, parts.Count(part => part.RequiredPromiseBranch == PromiseDecision.Defer),
            "story-part defer result count");
        Check(parts.All(part => part.Reachable),
            "story-part manifest included a selector not marked reachable");

        foreach (CommercialStoryPart part in parts)
        {
            CommercialStoryCard authorityStory;
            if (part.Kind == CommercialStoryPartKind.Epilogue)
            {
                authorityStory = _campaign.Epilogue;
            }
            else
            {
                CommercialCoreChapter chapter = _campaign.Chapters.Single(item =>
                    item.ChapterId == part.ChapterId);
                authorityStory = part.Kind switch
                {
                    CommercialStoryPartKind.Briefing => chapter.Briefing,
                    CommercialStoryPartKind.Window => chapter.DecisionWindows.Single(item =>
                        item.WindowId == part.WindowId).Story!,
                    CommercialStoryPartKind.Result when
                        part.RequiredPromiseBranch == PromiseDecision.Keep => chapter.KeptResult!,
                    CommercialStoryPartKind.Result when
                        part.RequiredPromiseBranch == PromiseDecision.Defer => chapter.DeferredResult!,
                    CommercialStoryPartKind.Result => chapter.StandardResult,
                    _ => throw new InvalidOperationException(
                        $"Unhandled story part kind {part.Kind}."),
                };
            }
            Check(ReferenceEquals(authorityStory, part.Story),
                $"story-part '{part.Selector}' copied or replaced its campaign story reference");
            Check(ReferenceEquals(part, _storyParts.Select(part.Selector)),
                $"story-part '{part.Selector}' selection did not return its manifest entry");
        }

        byte[] manifestBytes = _storyParts.SerializeManifest();
        Check(manifestBytes.SequenceEqual(_storyParts.SerializeManifest()),
            "story-part manifest bytes changed between identical serializations");
        var freshHarness = new CommercialStoryPartHarness(
            CommercialCampaignLoader.Load(_campaignBytes, _commercialWorld));
        Check(manifestBytes.SequenceEqual(freshHarness.SerializeManifest()),
            "story-part manifest bytes depended on campaign object identity");
        for (int index = 0; index < parts.Count; index++)
        {
            byte[] selectedBytes = _storyParts.Serialize(parts[index]);
            Check(selectedBytes.SequenceEqual(
                    _storyParts.Serialize(_storyParts.Select(parts[index].Selector))),
                $"story-part '{parts[index].Selector}' bytes changed between selections");
            Check(selectedBytes.SequenceEqual(freshHarness.Serialize(
                    freshHarness.Select(parts[index].Selector))),
                $"story-part '{parts[index].Selector}' bytes depended on campaign object identity");
        }

        using (JsonDocument manifest = JsonDocument.Parse(manifestBytes))
        {
            JsonElement root = manifest.RootElement;
            SequenceEqual(
                new[] { "schemaVersion", "campaignId", "count", "parts" },
                root.EnumerateObject().Select(property => property.Name).ToArray(),
                "story manifest fixed root property order");
            Equal(CommercialStoryPartHarness.ManifestSchemaVersion,
                root.GetProperty("schemaVersion").GetString(),
                "story manifest schema version");
            Equal(_campaign.CampaignId, root.GetProperty("campaignId").GetString(),
                "story manifest campaign ID");
            Equal(26, root.GetProperty("count").GetInt32(), "story manifest JSON count");
            JsonElement[] serializedParts = root.GetProperty("parts").EnumerateArray().ToArray();
            Equal(parts.Count, serializedParts.Length, "story manifest serialized part count");
            for (int index = 0; index < serializedParts.Length; index++)
            {
                JsonElement serializedPart = serializedParts[index];
                SequenceEqual(
                    new[]
                    {
                        "schemaVersion", "campaignId", "selector", "kind", "chapterId",
                        "windowId", "reachable", "requiredPromiseBranch", "story",
                    },
                    serializedPart.EnumerateObject().Select(property => property.Name).ToArray(),
                    $"story-part '{parts[index].Selector}' fixed property order");
                SequenceEqual(
                    new[] { "speaker", "title", "body" },
                    serializedPart.GetProperty("story").EnumerateObject()
                        .Select(property => property.Name).ToArray(),
                    $"story-part '{parts[index].Selector}' fixed story property order");
                Equal(CommercialStoryPartHarness.OutputSchemaVersion,
                    serializedPart.GetProperty("schemaVersion").GetString(),
                    $"story-part '{parts[index].Selector}' output schema version");
                Equal(_campaign.CampaignId,
                    serializedPart.GetProperty("campaignId").GetString(),
                    $"story-part '{parts[index].Selector}' output campaign ID");
                Equal(parts[index].Selector, serializedPart.GetProperty("selector").GetString(),
                    $"story-part '{parts[index].Selector}' serialized selector");
                string expectedKind = parts[index].Kind switch
                {
                    CommercialStoryPartKind.Briefing => "briefing",
                    CommercialStoryPartKind.Window => "window",
                    CommercialStoryPartKind.Result => "result",
                    CommercialStoryPartKind.Epilogue => "epilogue",
                    _ => throw new InvalidOperationException(
                        $"Unhandled story part kind {parts[index].Kind}."),
                };
                Equal(expectedKind, serializedPart.GetProperty("kind").GetString(),
                    $"story-part '{parts[index].Selector}' serialized kind");
                Equal(parts[index].ChapterId,
                    serializedPart.GetProperty("chapterId").GetString(),
                    $"story-part '{parts[index].Selector}' serialized chapter ID");
                Equal(parts[index].WindowId,
                    serializedPart.GetProperty("windowId").GetString(),
                    $"story-part '{parts[index].Selector}' serialized window ID");
                Check(serializedPart.GetProperty("reachable").GetBoolean(),
                    $"story-part '{parts[index].Selector}' serialized as unreachable");
                string? expectedBranch = parts[index].RequiredPromiseBranch switch
                {
                    null => null,
                    PromiseDecision.Keep => "keep",
                    PromiseDecision.Defer => "defer",
                    _ => throw new InvalidOperationException(
                        $"Unhandled promise branch {parts[index].RequiredPromiseBranch}."),
                };
                Equal(expectedBranch,
                    serializedPart.GetProperty("requiredPromiseBranch").GetString(),
                    $"story-part '{parts[index].Selector}' serialized promise branch");
                JsonElement story = serializedPart.GetProperty("story");
                Equal(parts[index].Story.Speaker, story.GetProperty("speaker").GetString(),
                    $"story-part '{parts[index].Selector}' serialized speaker");
                Equal(parts[index].Story.Title, story.GetProperty("title").GetString(),
                    $"story-part '{parts[index].Selector}' serialized title");
                Equal(parts[index].Story.Body, story.GetProperty("body").GetString(),
                    $"story-part '{parts[index].Selector}' serialized body");
            }
        }

        List<string> unreachableSelectors = [];
        foreach (CommercialCoreChapter chapter in _campaign.Chapters)
        {
            unreachableSelectors.AddRange(chapter.DecisionWindows
                .Where(window => window.Story is null)
                .Select(window => $"{chapter.ChapterId}/window/{window.WindowId}"));
            if (chapter.Promise is null)
            {
                unreachableSelectors.Add($"{chapter.ChapterId}/result/keep");
                unreachableSelectors.Add($"{chapter.ChapterId}/result/defer");
            }
            else
            {
                unreachableSelectors.Add($"{chapter.ChapterId}/result/standard");
            }
        }
        Equal(17, unreachableSelectors.Count,
            "story-part known but unreachable negative truth-table count");
        Equal(unreachableSelectors.Count,
            unreachableSelectors.Distinct(StringComparer.Ordinal).Count(),
            "story-part unreachable negative truth-table uniqueness");
        foreach (string selector in unreachableSelectors)
        {
            ExpectStoryPartRejected(
                selector,
                CommercialStoryPartErrorCode.UnreachableStoryPart,
                $"known unreachable story part '{selector}'");
        }

        ExpectStoryPartRejected(
            "UNKNOWN_CHAPTER/briefing",
            CommercialStoryPartErrorCode.UnknownChapter,
            "unknown story-part chapter");
        ExpectStoryPartRejected(
            "first_light/briefing",
            CommercialStoryPartErrorCode.UnknownChapter,
            "case-mismatched story-part chapter");
        ExpectStoryPartRejected(
            "FIRST_LIGHT/window/UNKNOWN_WINDOW",
            CommercialStoryPartErrorCode.UnreachableStoryPart,
            "unknown story-part window");
        ExpectStoryPartRejected(
            "SECOND_SOURCE/window/second_source_build",
            CommercialStoryPartErrorCode.UnreachableStoryPart,
            "case-mismatched story-part window");
        string[] invalidSelectors =
        [
            "",
            "FIRST_LIGHT",
            "FIRST_LIGHT/Briefing",
            "FIRST_LIGHT/window",
            "FIRST_LIGHT/window/",
            "FIRST_LIGHT/result/KEEP",
            "FIRST_LIGHT/result/other",
            "campaign/Epilogue",
            "campaign/epilogue/extra",
            "FIRST_LIGHT//briefing",
        ];
        foreach (string selector in invalidSelectors)
        {
            ExpectStoryPartRejected(
                selector,
                CommercialStoryPartErrorCode.InvalidSelector,
                $"invalid story-part grammar '{selector}'");
        }

        CommercialStoryPartSelectionException error = CaptureStoryPartFailure(
            "NORTH_BANK_PROMISE/result/standard",
            CommercialStoryPartErrorCode.UnreachableStoryPart,
            "promise standard-result typed failure");
        byte[] errorBytes = CommercialStoryPartHarness.SerializeError(error);
        Check(errorBytes.SequenceEqual(CommercialStoryPartHarness.SerializeError(error)),
            "story-part error bytes changed between identical serializations");
        using JsonDocument errorDocument = JsonDocument.Parse(errorBytes);
        JsonElement errorRoot = errorDocument.RootElement;
        SequenceEqual(
            new[] { "schemaVersion", "selector", "errorCode", "message" },
            errorRoot.EnumerateObject().Select(property => property.Name).ToArray(),
            "story-part error fixed property order");
        Equal(CommercialStoryPartHarness.ErrorSchemaVersion,
            errorRoot.GetProperty("schemaVersion").GetString(),
            "story-part error schema version");
        Equal("UNREACHABLE_STORY_PART", errorRoot.GetProperty("errorCode").GetString(),
            "story-part typed error code serialization");
    }

    private void CheckCommercialCampaignFirstFourCarrySave()
    {
        var direct = new CommercialCoreRun(_commercialWorld, _campaign);
        BuildCampaignLine(
            direct,
            "WEST_SOURCE",
            "EAST_RESIDENTIAL_TERMINAL",
            [
                new MapPoint(650, 700),
                new MapPoint(1030, 500),
                new MapPoint(1560, 500),
                new MapPoint(2000, 600),
                new MapPoint(2400, 700),
            ],
            "direct no-substation route");
        CommercialDecisionPreview directPreview = direct.PreviewDecisionWindow();
        Check(!directPreview.Accepted &&
            directPreview.Error == CommercialCoreError.SafetyDutyFailed &&
            directPreview.SupplyFailure == ThermalSupplyFailure.NoPath,
            "a direct source-to-load line bypassed the required distribution substation");

        CommercialCoreRun keep = CompleteCampaignFirstThree();
        CommercialCoreSnapshot fourthStart = keep.GetSnapshot();
        Equal("NORTH_BANK_PROMISE", fourthStart.Chapter.ChapterId,
            "campaign fourth mission transition");
        int carriedEdgeCount = fourthStart.Construction.World.Edges.Count;
        CoreAccepted(keep.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Keep)), "keep north-bank promise");
        CommercialDecisionPreview missingWater = keep.PreviewDecisionWindow();
        Check(!missingWater.Accepted &&
            missingWater.Error == CommercialCoreError.SafetyDutyFailed &&
            missingWater.FailedDemandId == "WATER_SAFETY_DUTY",
            "fourth mission approved without its new water safety branch");
        BuildCampaignLine(
            keep,
            "PLAYER_SUBSTATION_1",
            "WATER_TERMINAL",
            Array.Empty<MapPoint>(),
            "keep water branch");
        CommercialDecisionPreview keepPreview = keep.PreviewDecisionWindow();
        Check(keepPreview.Accepted, "keep first-four preview failed");
        Check(keepPreview.PhaseResults.SelectMany(item => item.Assets).All(item =>
                item.CurrentState != ThermalOperatingState.Emergency),
            "first four missions used emergency thermal permission");
        CommercialCoreCommandResult keepFinish = keep.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow));
        CoreAccepted(keepFinish, "keep first-four completion");
        Check(!keepFinish.Snapshot.CampaignComplete &&
            keepFinish.Snapshot.ChapterResults.Count == 4 &&
            keepFinish.Snapshot.Chapter.ChapterId == "WHOSE_MARGIN",
            "Stage-E checkpoint did not retain four factual results before mission five");
        Check(keepFinish.Snapshot.Construction.World.Edges.Count > carriedEdgeCount &&
            keepFinish.Snapshot.Construction.World.Edges.Any(item => item.EdgeId == "PLAYER_EDGE_1"),
            "campaign did not carry the player-built first-light network through mission four");
        CommercialChapterResultRecord keptResult = keepFinish.CompletedChapter!;
        Check(keptResult.PromiseDecision == PromiseDecision.Keep &&
            keptResult.DemandFacts.Single(item =>
                item.DemandId == "NORTH_BANK_PROMISE_LOAD").Supplied,
            "kept north-bank promise was not recorded from actual supply");
        Check(ReferenceEquals(
                _storyParts.Select("NORTH_BANK_PROMISE/result/keep").Story,
                keptResult.Story),
            "north-bank keep command witness did not reach its keep story selector");

        CommercialCampaignSaveV3 save = CommercialCampaignSaveCodec.Create(
            _commercialWorld,
            _commercialBytes,
            _campaign,
            _campaignBytes,
            keep.GetCommands());
        CommercialCampaignSaveV3 decoded = CommercialCampaignSaveCodec.Deserialize(
            CommercialCampaignSaveCodec.Serialize(save));
        CommercialCoreRun restored = CommercialCampaignSaveCodec.Restore(
            decoded,
            _commercialWorld,
            _commercialBytes,
            _campaign,
            _campaignBytes);
        Equal(JsonSerializer.Serialize(keep.GetSnapshot()),
            JsonSerializer.Serialize(restored.GetSnapshot()),
            "first-four save fresh restore state equality");
        Equal(_campaign.CampaignId, decoded.CampaignId, "campaign save authority ID");
        string saveJson = Encoding.UTF8.GetString(CommercialCampaignSaveCodec.Serialize(save));
        string duplicateCampaignId = saveJson.Replace(
            "\"campaignId\":",
            "\"campaignId\": \"DUPLICATE\", \"campaignId\":",
            StringComparison.Ordinal);
        ExpectThrows<CommercialCorePersistenceException>(
            () => CommercialCampaignSaveCodec.Deserialize(
                Encoding.UTF8.GetBytes(duplicateCampaignId)),
            "duplicate campaign save property");
        CommercialCoreCampaignSave stageDSave = CommercialCoreSaveCodec.Create(
            _commercialWorld,
            _commercialBytes,
            _coreSlice,
            _coreBytes,
            Array.Empty<CommercialCoreCommand>());
        ExpectThrows<CommercialCorePersistenceException>(
            () => CommercialCampaignSaveCodec.Deserialize(
                CommercialCoreSaveCodec.Serialize(stageDSave)),
            "Stage-D development save must be incompatible with final campaign authority");

        string directory = Path.Combine(
            Path.GetTempPath(),
            $"gridworks-commercial-campaign-save-{Guid.NewGuid():N}");
        string savePath = Path.Combine(directory, CommercialCampaignPersistenceStore.SaveFileName);
        try
        {
            CommercialCampaignPersistenceStore.Save(savePath, save);
            CommercialCampaignSaveLoadResult loaded = CommercialCampaignPersistenceStore.Load(savePath);
            Check(loaded.Status == CommercialCoreDocumentLoadStatus.Loaded && loaded.Save is not null,
                "campaign atomic store did not load its committed save");
            Equal(saveJson, Encoding.UTF8.GetString(File.ReadAllBytes(savePath)),
                "campaign atomic store bytes");
            string preservedPath = CommercialCampaignPersistenceStore.PreserveIncompatible(savePath);
            Check(!File.Exists(savePath) && File.Exists(preservedPath),
                "incompatible campaign save was not moved aside in the same directory");
            Equal(saveJson, Encoding.UTF8.GetString(File.ReadAllBytes(preservedPath)),
                "preserved incompatible campaign save bytes");
            CommercialCampaignPersistenceStore.Save(savePath, save);
            Check(CommercialCampaignPersistenceStore.Load(savePath).Status ==
                    CommercialCoreDocumentLoadStatus.Loaded && File.Exists(preservedPath),
                "new campaign save did not coexist with preserved incompatible bytes");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        CommercialCoreRun restart = CompleteCampaignFirstThree();
        string fourthStartJson = JsonSerializer.Serialize(restart.GetSnapshot());
        BuildCampaignLine(
            restart,
            "PLAYER_SUBSTATION_1",
            "WATER_TERMINAL",
            Array.Empty<MapPoint>(),
            "restart water branch");
        CoreAccepted(restart.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Keep)), "restart promise choice");
        CoreAccepted(restart.RestartChapter(), "campaign fourth mission restart");
        Equal(fourthStartJson, JsonSerializer.Serialize(restart.GetSnapshot()),
            "campaign chapter restart changed earlier network/results/cash");

        CommercialCoreRun deferred = CompleteCampaignFirstThree();
        BuildCampaignLine(
            deferred,
            "PLAYER_SUBSTATION_2",
            "WATER_TERMINAL",
            Array.Empty<MapPoint>(),
            "deferred shared water branch");
        CoreAccepted(deferred.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Defer)), "defer north-bank promise");
        CommercialCoreCommandResult deferredFinish = deferred.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow));
        CoreAccepted(deferredFinish, "deferred first-four completion");
        CommercialResultDemandFact deferredPromise = deferredFinish.CompletedChapter!.DemandFacts.Single(
            item => item.DemandId == "NORTH_BANK_PROMISE_LOAD");
        Check(deferredPromise.Deferred && !deferredPromise.Supplied,
            "deferred north-bank promise entered supply allocation");
        Check(deferredFinish.CompletedChapter.PromiseDecision == PromiseDecision.Defer,
            "deferred result omitted explicit promise choice");
        Check(ReferenceEquals(
                _storyParts.Select("NORTH_BANK_PROMISE/result/defer").Story,
                deferredFinish.CompletedChapter.Story),
            "north-bank defer command witness did not reach its defer story selector");

        CommercialCoreRun keepSouth = CompleteCampaignFirstThree();
        BuildCampaignLine(
            keepSouth,
            "PLAYER_SUBSTATION_2",
            "WATER_TERMINAL",
            Array.Empty<MapPoint>(),
            "keep south water branch");
        CoreAccepted(keepSouth.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Keep)), "keep south promise choice");
        CommercialCoreCommandResult keepSouthFinish = keepSouth.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow));
        CoreAccepted(keepSouthFinish, "keep south first-four completion");
        CommercialResultDemandFact northWater = keptResult.DemandFacts.Single(item =>
            item.DemandId == "WATER_SAFETY_DUTY");
        CommercialResultDemandFact southWater = keepSouthFinish.CompletedChapter!.DemandFacts.Single(item =>
            item.DemandId == "WATER_SAFETY_DUTY");
        Check(northWater.Supplied && southWater.Supplied &&
            !northWater.PathEdgeIds.SequenceEqual(southWater.PathEdgeIds, StringComparer.Ordinal),
            "two valid fourth-mission prototypes did not retain distinct actual water paths");

        CommercialWorldDefinition constrainedWorld = _commercialWorld with
        {
            ThermalNodeClasses = _commercialWorld.ThermalNodeClasses.Select(item => item with
            {
                ContinuousLimitKw = Math.Min(item.ContinuousLimitKw, 1500),
            }).ToArray(),
            ThermalLineClasses = _commercialWorld.ThermalLineClasses.Select(item => item with
            {
                ContinuousLimitKw = Math.Min(item.ContinuousLimitKw, 1500),
            }).ToArray(),
        };
        CommercialWorldLoader.Validate(constrainedWorld);
        CommercialCoreRun noEarlyEmergency = CompleteCampaignFirstThree(constrainedWorld);
        BuildCampaignLine(
            noEarlyEmergency,
            "PLAYER_SUBSTATION_1",
            "WATER_TERMINAL",
            Array.Empty<MapPoint>(),
            "continuous-only water branch");
        CoreAccepted(noEarlyEmergency.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Keep)), "continuous-only keep choice");
        CommercialDecisionPreview noEarlyEmergencyPreview =
            noEarlyEmergency.PreviewDecisionWindow();
        Check(!noEarlyEmergencyPreview.Accepted &&
            noEarlyEmergencyPreview.Error == CommercialCoreError.KeptPromiseFailed &&
            noEarlyEmergencyPreview.FailedDemandId == "NORTH_BANK_PROMISE_LOAD" &&
            noEarlyEmergencyPreview.SupplyFailure == ThermalSupplyFailure.ContinuousPermission,
            "mission four used emergency capacity for a kept promise before the mission-five unlock");
        CoreAccepted(noEarlyEmergency.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Defer)), "continuous-only defer recovery");
        Check(noEarlyEmergency.PreviewDecisionWindow().Accepted,
            "mission-four continuous-only boundary could not recover by deferring its optional promise");
    }

    private void CheckCommercialCampaignFinalEightEpilogue()
    {
        CommercialCoreRun run = CompleteCampaignFirstFour();
        CommercialCoreSnapshot fifthStart = run.GetSnapshot();
        Equal("WHOSE_MARGIN", fifthStart.Chapter.ChapterId, "mission-five start");
        Check(fifthStart.Construction.World.Nodes.Single(item =>
                item.NodeId == "INDUSTRY_TERMINAL").Commissioned,
            "industry terminal did not activate at mission five");
        IReadOnlyList<CommercialCoreCommand> fifthStartCommands = run.GetCommands().ToArray();
        string fifthStartJson = JsonSerializer.Serialize(fifthStart);
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Keep)), "mission-five keep choice before construction");
        CommercialDecisionPreview missingIndustry = run.PreviewDecisionWindow();
        Check(!missingIndustry.Accepted &&
            missingIndustry.Error == CommercialCoreError.KeptPromiseFailed &&
            missingIndustry.FailedDemandId == "INDUSTRY_MARGIN_PROMISE" &&
            missingIndustry.SupplyFailure == ThermalSupplyFailure.NoPath,
            "mission five approved a kept industry promise without service construction");
        CoreAccepted(run.RestartChapter(), "mission-five missing-service recovery restart");
        Equal(fifthStartJson, JsonSerializer.Serialize(run.GetSnapshot()),
            "mission-five failure restart did not restore the exact chapter start");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Keep)), "mission-five keep choice after restart");

        BuildMissionFiveIndustryService(run, "STANDARD_LINE");
        CommercialDecisionPreview hot = run.PreviewDecisionWindow();
        Check(hot.Accepted && hot.PhaseResults[0].Demands.Single(item =>
                item.DemandId == "INDUSTRY_MARGIN_PROMISE").EmergencyAssetIds.Count > 0,
            $"mission-five standard prototype failed: {hot.Error}/{hot.FailedDemandId}/" +
            $"{hot.SupplyFailure}/{hot.FirstBottleneckAssetId}");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow)), "mission-five hot approval");
        Check(run.GetSnapshot().ThermalMemory.Any(item => item.ProtectiveOutage),
            "mission-five emergency use did not create protective memory");
        CommercialDecisionPreview morning = run.PreviewDecisionWindow();
        Check(morning.Accepted && morning.PhaseResults[0].Assets.Any(item =>
                item.CurrentState == ThermalOperatingState.ProtectiveOutage),
            "mission-five next safety projection omitted protective outage");
        CommercialCoreCommandResult fifthFinish = run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow));
        CoreAccepted(fifthFinish, "mission-five morning approval");
        Check(fifthFinish.CompletedChapter!.EmergencyAssetIds.Count > 0 &&
            fifthFinish.CompletedChapter.ProtectiveOutageAssetIds.Count > 0,
            "mission-five factual result omitted emergency/protective assets");
        Check(ReferenceEquals(
                _storyParts.Select("WHOSE_MARGIN/result/keep").Story,
                fifthFinish.CompletedChapter.Story),
            "whose-margin keep command witness did not reach its keep story selector");

        CommercialCoreRun reinforcedMargin = CommercialCoreRun.Restore(
            _commercialWorld,
            _campaign,
            fifthStartCommands);
        BuildMissionFiveIndustryService(reinforcedMargin, "REINFORCED_LINE");
        CoreAccepted(reinforcedMargin.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Keep)), "reinforced mission-five keep choice");
        CommercialDecisionPreview reinforcedHot = reinforcedMargin.PreviewDecisionWindow();
        Check(reinforcedHot.Accepted && reinforcedHot.PhaseResults.SelectMany(item => item.Assets)
                .All(item => item.CurrentState != ThermalOperatingState.Emergency),
            "mission-five reinforced prototype did not stay inside continuous limits");
        CoreAccepted(reinforcedMargin.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow)),
            "reinforced mission-five hot approval");
        CoreAccepted(reinforcedMargin.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow)),
            "reinforced mission-five morning approval");
        CommercialChapterResultRecord reinforcedMarginResult =
            reinforcedMargin.GetSnapshot().ChapterResults[^1];
        Check(fifthFinish.CompletedChapter.RemainingCashUnit >
                reinforcedMarginResult.RemainingCashUnit &&
            hot.ProjectedMinute < reinforcedHot.ProjectedMinute &&
            fifthFinish.CompletedChapter.EmergencyAssetIds.Count > 0 &&
            reinforcedMarginResult.EmergencyAssetIds.Count == 0,
            "mission-five standard/reinforced prototypes lost their cash-time versus thermal-state tradeoff");
        CompleteCampaignFromMissionSix(reinforcedMargin, "mission-five reinforced prototype");
        Equal("BEFORE_WATER_REACHES", run.GetSnapshot().Chapter.ChapterId,
            "mission-six transition");

        IReadOnlyList<CommercialCoreCommand> sixthStartCommands = run.GetCommands().ToArray();
        string sixthStartJson = JsonSerializer.Serialize(run.GetSnapshot());

        CommercialDecisionPreview noFloodBypass = run.PreviewDecisionWindow();
        Check(!noFloodBypass.Accepted &&
            noFloodBypass.Error == CommercialCoreError.SafetyDutyFailed,
            "mission six did not require additional flood-surviving capacity");
        CoreAccepted(run.RestartChapter(), "mission-six missing-bypass recovery restart");
        Equal(sixthStartJson, JsonSerializer.Serialize(run.GetSnapshot()),
            "mission-six failure restart did not restore the exact chapter start");
        HashSet<string> sixthStartEdgeIds = run.GetSnapshot().Construction.World.Edges
            .Select(item => item.EdgeId)
            .ToHashSet(StringComparer.Ordinal);
        BuildCampaignLine(
            run,
            "WATER_TERMINAL",
            "PLAYER_SUBSTATION_3",
            Array.Empty<MapPoint>(),
            "standard flood high-ground bypass",
            lineClassId: "STANDARD_LINE",
            poleClassId: "STANDARD_POLE");
        string floodBypassEdgeId = run.GetSnapshot().Construction.World.Edges
            .Select(item => item.EdgeId)
            .Single(item => !sixthStartEdgeIds.Contains(item));
        CommercialDecisionPreview flood = run.PreviewDecisionWindow();
        Check(flood.Accepted && flood.PhaseResults[0].Demands
                .Where(item => item.DemandId is "FLOOD_WATER_DUTY" or "FLOOD_HOSPITAL_DUTY")
                .All(item => item.Supplied) &&
            flood.PhaseResults[0].Demands.Single(item =>
                item.DemandId == "FLOOD_HOSPITAL_DUTY").EmergencyAssetIds.Count > 0,
            $"mission-six bypass failed: {flood.Error}/{flood.FailedDemandId}/" +
            $"{flood.SupplyFailure}/{flood.FirstBottleneckAssetId}; emergency=" +
            string.Join(",", flood.PhaseResults[0].Demands.Single(item =>
                item.DemandId == "FLOOD_HOSPITAL_DUTY").EmergencyAssetIds) + "; path=" +
            string.Join(",", flood.PhaseResults[0].Demands.Single(item =>
                item.DemandId == "FLOOD_HOSPITAL_DUTY").PathEdgeIds) + "; demands=" +
            string.Join(";", flood.PhaseResults[0].Demands.Select(item =>
                $"{item.DemandId}:{string.Join(',', item.PathEdgeIds)}:{item.MinimumRemainingLimitKw}")));
        Check(flood.PhaseResults[0].Demands
                .Where(item => item.DemandId is "FLOOD_WATER_DUTY" or "FLOOD_HOSPITAL_DUTY")
                .Any(item => item.PathEdgeIds.Contains(floodBypassEdgeId, StringComparer.Ordinal)),
            "mission-six accepted construction was not used by either hard flood duty");

        CommercialCoreRun reinforcedFlood = CommercialCoreRun.Restore(
            _commercialWorld,
            _campaign,
            sixthStartCommands);
        BuildCampaignLine(
            reinforcedFlood,
            "WATER_TERMINAL",
            "PLAYER_SUBSTATION_3",
            Array.Empty<MapPoint>(),
            "reinforced flood high-ground bypass");
        CommercialDecisionPreview reinforcedFloodPreview = reinforcedFlood.PreviewDecisionWindow();
        Check(reinforcedFloodPreview.Accepted &&
            reinforcedFloodPreview.PhaseResults[0].Demands.Single(item =>
                item.DemandId == "FLOOD_HOSPITAL_DUTY").EmergencyAssetIds.Count == 0,
            $"mission-six reinforced prototype failed: {reinforcedFloodPreview.Error}/" +
            $"{reinforcedFloodPreview.FailedDemandId}/{reinforcedFloodPreview.SupplyFailure}/" +
            $"{reinforcedFloodPreview.FirstBottleneckAssetId}");
        ThermalDemandResult waterPrototype = flood.PhaseResults[0].Demands.Single(item =>
            item.DemandId == "FLOOD_WATER_DUTY");
        ThermalDemandResult reinforcedWater = reinforcedFloodPreview.PhaseResults[0].Demands.Single(item =>
            item.DemandId == "FLOOD_WATER_DUTY");
        Check(flood.ProjectedCashUnit > reinforcedFloodPreview.ProjectedCashUnit &&
            flood.ProjectedMinute < reinforcedFloodPreview.ProjectedMinute &&
            waterPrototype.MinimumRemainingLimitKw < reinforcedWater.MinimumRemainingLimitKw,
            "mission-six standard/reinforced prototypes lost their cash-time versus flood-margin tradeoff: " +
            $"cash={flood.ProjectedCashUnit}/{reinforcedFloodPreview.ProjectedCashUnit}, " +
            $"minute={flood.ProjectedMinute}/{reinforcedFloodPreview.ProjectedMinute}, " +
            $"water-margin={waterPrototype.MinimumRemainingLimitKw}/" +
            $"{reinforcedWater.MinimumRemainingLimitKw}");
        CoreAccepted(reinforcedFlood.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow)),
            "mission-six reinforced prototype approval");
        CompleteCampaignFromMissionSeven(reinforcedFlood, "mission-six reinforced prototype");
        CommercialCoreCommandResult sixthFinish = run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow));
        CoreAccepted(sixthFinish, "mission-six flood approval");
        Equal("SHUT_DOWN_TO_KEEP", run.GetSnapshot().Chapter.ChapterId,
            "mission-seven transition");
        Check(sixthFinish.CompletedChapter!.EmergencyAssetIds.Count > 0 &&
            run.GetSnapshot().ThermalMemory.All(item => !item.ProtectiveOutage),
            "authored long gap did not clear the protection created by mission-six emergency use");
        Check(ReferenceEquals(
                _storyParts.Select("BEFORE_WATER_REACHES/result/standard").Story,
                sixthFinish.CompletedChapter.Story),
            "flood command witness did not reach its standard story selector");
        IReadOnlyList<CommercialCoreCommand> seventhStartCommands = run.GetCommands().ToArray();
        string seventhStart = JsonSerializer.Serialize(run.GetSnapshot());

        CommercialCoreRun overdueMaintenance = CommercialCoreRun.Restore(
            _commercialWorld,
            _campaign,
            seventhStartCommands);
        BuildCampaignSubstation(
            overdueMaintenance,
            new MapPoint(160, 160),
            "mission-seven overdue substation one");
        BuildCampaignSubstation(
            overdueMaintenance,
            new MapPoint(420, 160),
            "mission-seven overdue substation two");
        BuildCampaignSubstation(
            overdueMaintenance,
            new MapPoint(680, 160),
            "mission-seven overdue substation three");
        BuildCampaignSubstation(
            overdueMaintenance,
            new MapPoint(2600, 200),
            "mission-seven overdue substation four");
        CoreAccepted(overdueMaintenance.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Defer)), "mission-seven overdue defer choice");
        CommercialDecisionPreview overdueMaintenancePreview =
            overdueMaintenance.PreviewDecisionWindow();
        Check(!overdueMaintenancePreview.Accepted &&
            overdueMaintenancePreview.Error == CommercialCoreError.DeadlineExceeded &&
            overdueMaintenancePreview.ProjectedMinute >
                overdueMaintenance.GetSnapshot().Chapter.DeadlineMinute,
            "mission seven did not reject a project beyond its authored deadline");
        CoreAccepted(overdueMaintenance.RestartChapter(), "mission-seven deadline recovery restart");
        Equal(seventhStart, JsonSerializer.Serialize(overdueMaintenance.GetSnapshot()),
            "mission-seven deadline recovery did not restore the exact chapter start");

        HashSet<string> seventhStartEdgeIds = run.GetSnapshot().Construction.World.Edges
            .Select(item => item.EdgeId)
            .ToHashSet(StringComparer.Ordinal);
        BuildCampaignLine(
            run,
            "PLAYER_SUBSTATION_2",
            "PLAYER_POLE_15",
            Array.Empty<MapPoint>(),
            "planned-outage substation tie");
        string maintenanceTieEdgeId = run.GetSnapshot().Construction.World.Edges
            .Select(item => item.EdgeId)
            .Single(item => !seventhStartEdgeIds.Contains(item));
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Keep)), "mission-seven keep choice");
        CommercialDecisionPreview maintenance = run.PreviewDecisionWindow();
        Check(maintenance.Accepted && maintenance.PhaseResults[0].Demands.All(item => item.Supplied),
            $"mission-seven keep prototype failed: {maintenance.Error}/" +
            $"{maintenance.FailedDemandId}/{maintenance.SupplyFailure}/" +
            $"{maintenance.FirstBottleneckAssetId}");
        Check(maintenance.PhaseResults[0].Demands.Any(item =>
                item.PathEdgeIds.Contains(maintenanceTieEdgeId, StringComparer.Ordinal)),
            "mission-seven reinforced tie was not used by an accepted obligation path");
        CommercialCoreCommandResult maintenanceFinish = run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow));
        CoreAccepted(maintenanceFinish, "mission-seven maintenance approval");
        Check(ReferenceEquals(
                _storyParts.Select("SHUT_DOWN_TO_KEEP/result/keep").Story,
                maintenanceFinish.CompletedChapter!.Story),
            "maintenance keep command witness did not reach its keep story selector");

        CommercialCoreRun standardMaintenance = CommercialCoreRun.Restore(
            _commercialWorld,
            _campaign,
            seventhStartCommands);
        BuildCampaignLine(
            standardMaintenance,
            "PLAYER_SUBSTATION_2",
            "PLAYER_POLE_15",
            Array.Empty<MapPoint>(),
            "standard planned-outage substation tie",
            lineClassId: "STANDARD_LINE",
            poleClassId: "STANDARD_POLE");
        CoreAccepted(standardMaintenance.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Keep)), "standard mission-seven keep choice");
        CommercialDecisionPreview standardMaintenancePreview =
            standardMaintenance.PreviewDecisionWindow();
        Check(standardMaintenancePreview.Accepted,
            $"mission-seven standard prototype failed: {standardMaintenancePreview.Error}/" +
            $"{standardMaintenancePreview.FailedDemandId}/" +
            $"{standardMaintenancePreview.SupplyFailure}/" +
            $"{standardMaintenancePreview.FirstBottleneckAssetId}");
        Check(standardMaintenancePreview.PhaseResults[0].Demands.Any(item =>
                item.PathEdgeIds.Contains(maintenanceTieEdgeId, StringComparer.Ordinal)),
            "mission-seven standard tie was not used by an accepted obligation path");
        CommercialCoreCommandResult standardMaintenanceFinish =
            standardMaintenance.Apply(new CommercialCoreCommand(
                CommercialCoreCommandKind.ApproveDecisionWindow));
        CoreAccepted(standardMaintenanceFinish, "mission-seven standard maintenance approval");
        bool reinforcedMaintenanceMargin = standardMaintenancePreview.PhaseResults[0].Demands.Any(
            standardDemand =>
            {
                ThermalDemandResult reinforcedDemand = maintenance.PhaseResults[0].Demands.Single(item =>
                    item.DemandId == standardDemand.DemandId);
                return reinforcedDemand.MinimumRemainingLimitKw >
                    standardDemand.MinimumRemainingLimitKw;
            });
        Check(maintenanceFinish.CompletedChapter!.PromiseDecision == PromiseDecision.Keep &&
            standardMaintenanceFinish.CompletedChapter!.PromiseDecision == PromiseDecision.Keep &&
            maintenance.ProjectedCashUnit < standardMaintenancePreview.ProjectedCashUnit &&
            maintenance.ProjectedMinute > standardMaintenancePreview.ProjectedMinute &&
            reinforcedMaintenanceMargin,
            "mission-seven standard/reinforced prototypes lost their cash-time versus thermal-margin tradeoff");
        CompleteCampaignFromMissionEight(
            standardMaintenance,
            "mission-seven standard prototype");
        Equal("LONGEST_NIGHT", run.GetSnapshot().Chapter.ChapterId,
            "mission-eight transition");
        IReadOnlyList<CommercialCoreCommand> eighthStartCommands = run.GetCommands().ToArray();
        string eighthStartSnapshot = JsonSerializer.Serialize(run.GetSnapshot());

        CommercialCoreRun overdueNight = CommercialCoreRun.Restore(
            _commercialWorld,
            _campaign,
            eighthStartCommands);
        BuildCampaignSubstation(
            overdueNight,
            new MapPoint(160, 160),
            "mission-eight overdue substation one");
        BuildCampaignSubstation(
            overdueNight,
            new MapPoint(420, 160),
            "mission-eight overdue substation two");
        BuildCampaignSubstation(
            overdueNight,
            new MapPoint(680, 160),
            "mission-eight overdue substation three");
        BuildCampaignSubstation(
            overdueNight,
            new MapPoint(2600, 200),
            "mission-eight overdue substation four");
        BuildCampaignSubstation(
            overdueNight,
            new MapPoint(160, 1880),
            "mission-eight overdue substation five");
        CoreAccepted(overdueNight.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Defer)), "mission-eight overdue defer choice");
        CommercialDecisionPreview overdueNightPreview = overdueNight.PreviewDecisionWindow();
        Check(!overdueNightPreview.Accepted &&
            overdueNightPreview.Error == CommercialCoreError.DeadlineExceeded &&
            overdueNightPreview.ProjectedMinute > overdueNight.GetSnapshot().Chapter.DeadlineMinute,
            "mission eight did not reject a project beyond its authored deadline");
        CoreAccepted(overdueNight.RestartChapter(), "mission-eight deadline recovery restart");
        Equal(eighthStartSnapshot, JsonSerializer.Serialize(overdueNight.GetSnapshot()),
            "mission-eight deadline recovery did not restore the exact chapter start");

        HashSet<string> eighthStartEdgeIds = run.GetSnapshot().Construction.World.Edges
            .Select(item => item.EdgeId)
            .ToHashSet(StringComparer.Ordinal);
        BuildCampaignLine(
            run,
            "HOSPITAL_TERMINAL",
            "PLAYER_POLE_15",
            Array.Empty<MapPoint>(),
            "last-night hospital cross-tie");
        string lastNightTieEdgeId = run.GetSnapshot().Construction.World.Edges
            .Select(item => item.EdgeId)
            .Single(item => !eighthStartEdgeIds.Contains(item));
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Defer)), "mission-eight defer choice");
        CommercialDecisionPreview lastHeat = run.PreviewDecisionWindow();
        Check(lastHeat.Accepted, $"mission-eight heat preview failed: {lastHeat.Error}/" +
            $"{lastHeat.FailedDemandId}/{lastHeat.SupplyFailure}/{lastHeat.FirstBottleneckAssetId}");
        Check(lastHeat.PhaseResults[0].Demands.Any(item =>
                item.PathEdgeIds.Contains(lastNightTieEdgeId, StringComparer.Ordinal)),
            "mission-eight cross-tie was not used by an accepted obligation path");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow)), "mission-eight heat approval");
        CommercialDecisionPreview lastStorm = run.PreviewDecisionWindow();
        Check(lastStorm.Accepted && lastStorm.PhaseResults[0].Demands.All(item => item.Supplied),
            $"mission-eight storm preview failed: {lastStorm.Error}/" +
            $"{lastStorm.FailedDemandId}/{lastStorm.SupplyFailure}/" +
            $"{lastStorm.FirstBottleneckAssetId}");
        CommercialCoreCommandResult final = run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow));
        CoreAccepted(final, "mission-eight final approval");

        CommercialCoreRun keptNight = CommercialCoreRun.Restore(
            _commercialWorld,
            _campaign,
            eighthStartCommands);
        BuildCampaignLine(
            keptNight,
            "HOSPITAL_TERMINAL",
            "PLAYER_POLE_15",
            Array.Empty<MapPoint>(),
            "kept last-night hospital cross-tie");
        CoreAccepted(keptNight.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Keep)), "mission-eight keep choice");
        CommercialDecisionPreview keptLastHeat = keptNight.PreviewDecisionWindow();
        Check(keptLastHeat.Accepted,
            $"mission-eight keep prototype heat failed: {keptLastHeat.Error}/" +
            $"{keptLastHeat.FailedDemandId}/{keptLastHeat.SupplyFailure}/" +
            $"{keptLastHeat.FirstBottleneckAssetId}");
        CoreAccepted(keptNight.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow)),
            "mission-eight kept heat approval");
        CommercialDecisionPreview keptLastStorm = keptNight.PreviewDecisionWindow();
        Check(keptLastStorm.Accepted,
            $"mission-eight keep prototype storm failed: {keptLastStorm.Error}/" +
            $"{keptLastStorm.FailedDemandId}/{keptLastStorm.SupplyFailure}/" +
            $"{keptLastStorm.FirstBottleneckAssetId}");
        CommercialCoreCommandResult keptFinal = keptNight.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow));
        CoreAccepted(keptFinal, "mission-eight kept final approval");
        Check(ReferenceEquals(
                _storyParts.Select("LONGEST_NIGHT/result/defer").Story,
                final.CompletedChapter!.Story) &&
            ReferenceEquals(
                _storyParts.Select("LONGEST_NIGHT/result/keep").Story,
                keptFinal.CompletedChapter!.Story),
            "last-night command witnesses did not reach their defer/keep story selectors");
        Check(final.CompletedChapter!.PromiseDecision == PromiseDecision.Defer &&
            keptFinal.CompletedChapter!.PromiseDecision == PromiseDecision.Keep &&
            final.CompletedChapter.EmergencyAssetIds.Count <
                keptFinal.CompletedChapter.EmergencyAssetIds.Count &&
            final.CompletedChapter.DemandFacts.All(item =>
                item.DemandId != "LAST_NIGHT_INDUSTRY_PROMISE" || item.Deferred) &&
            keptFinal.CompletedChapter.DemandFacts.Any(item =>
                item.DemandId == "LAST_NIGHT_INDUSTRY_PROMISE" && !item.Deferred && item.Supplied),
            "mission-eight valid prototypes did not preserve deferred versus supplied promise facts");
        CommercialCoreSnapshot complete = run.GetSnapshot();
        Check(complete.CampaignComplete && complete.ChapterResults.Count == 8 &&
            complete.ChapterStartCommandCounts.Count == 8 &&
            complete.ChapterStartCommandCounts.Zip(
                    complete.ChapterStartCommandCounts.Skip(1),
                    (first, second) => first < second)
                .All(item => item),
            "final campaign did not reach eight results with selectable chapter starts");
        Equal("청류시 전력망 운영 인계", _campaign.Epilogue.Title,
            "authored campaign epilogue");

        CommercialCampaignSaveV3 completedSave = CommercialCampaignSaveCodec.Create(
            _commercialWorld,
            _commercialBytes,
            _campaign,
            _campaignBytes,
            run.GetCommands());
        CommercialCoreRun completedRestore = CommercialCampaignSaveCodec.Restore(
            CommercialCampaignSaveCodec.Deserialize(
                CommercialCampaignSaveCodec.Serialize(completedSave)),
            _commercialWorld,
            _commercialBytes,
            _campaign,
            _campaignBytes);
        Equal(JsonSerializer.Serialize(complete),
            JsonSerializer.Serialize(completedRestore.GetSnapshot()),
            "completed campaign save fresh restore equality");
        int eighthStart = complete.ChapterStartCommandCounts[7];
        CommercialCoreRun selectedEighth = CommercialCoreRun.Restore(
            _commercialWorld,
            _campaign,
            run.GetCommands().Take(eighthStart).ToArray());
        Check(!selectedEighth.GetSnapshot().CampaignComplete &&
            selectedEighth.GetSnapshot().Chapter.ChapterId == "LONGEST_NIGHT" &&
            selectedEighth.GetSnapshot().ChapterResults.Count == 7,
            "completed chapter selection did not restore the exact eighth-chapter start state");
    }

    private void CheckCommercialCoreFlowDesignsAndFacts()
    {
        CommercialCoreRun standard = NewCoreRun();
        CompletePrelude(standard);
        CoreAccepted(standard.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Keep)), "standard keep promise");
        CommercialDecisionPreview standardDraft = CompleteIndustryDraft(
            standard,
            "STANDARD_LINE",
            "STANDARD_POLE");
        Equal(10L, standardDraft.ProjectedMinute, "standard draft projected minute");
        Check(standardDraft.Accepted,
            $"standard draft preview failed: {standardDraft.Error}/" +
            $"{standardDraft.FailedDemandId}/{standardDraft.SupplyFailure}/" +
            $"{standardDraft.FirstBottleneckAssetId}");
        ThermalDemandResult standardPromise = standardDraft.PhaseResults[0].Demands.Single(item =>
            item.DemandId == "INDUSTRY_PROMISE");
        Check(standardPromise.Supplied, "standard prototype did not supply the promise");
        Check(standardPromise.EmergencyAssetIds.Contains("PLAYER_EDGE_1"),
            "standard prototype did not use its conductor emergency limit");

        CoreAccepted(standard.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.OrderLine)), "standard line order");
        CoreAccepted(standard.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.AdvanceConstruction)), "standard line completion");
        CommercialDecisionPreview standardCommittedPreview = standard.PreviewDecisionWindow();
        Equal(
            JsonSerializer.Serialize(standardDraft),
            JsonSerializer.Serialize(standardCommittedPreview),
            "complete-draft preview equals commissioned preview");
        CommercialCoreCommandResult hotApproval = standard.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow));
        CoreAccepted(hotApproval, "standard hot-window approval");
        Equal(
            JsonSerializer.Serialize(standardCommittedPreview),
            JsonSerializer.Serialize(hotApproval.DecisionPreview),
            "public hot preview equals approval result");
        Check(hotApproval.Snapshot.ThermalMemory.Single(item =>
                item.AssetId == "PLAYER_EDGE_1").ProtectiveOutage,
            "standard emergency line did not enter next-phase protective memory");
        CommercialCoreCommandResult frozenChoice = standard.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Defer));
        Check(!frozenChoice.Accepted && frozenChoice.Error == CommercialCoreError.WrongPhase,
            "promise changed after its operating result was committed");
        CommercialDecisionPreview safetyPreview = standard.PreviewDecisionWindow();
        Check(safetyPreview.Accepted, "standard prototype broke next safety duties");
        CommercialCoreCommandResult standardFinish = standard.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow));
        CoreAccepted(standardFinish, "standard next-safety approval");
        Equal(JsonSerializer.Serialize(safetyPreview),
            JsonSerializer.Serialize(standardFinish.DecisionPreview),
            "public next-safety preview equals approval result");
        Check(standardFinish.Snapshot.CampaignComplete, "standard prototype did not complete the slice");
        CommercialChapterResultRecord standardResult = standardFinish.CompletedChapter!;
        Equal(PromiseDecision.Keep, standardResult.PromiseDecision, "standard result promise decision");
        CommercialResultDemandFact standardFact = standardResult.DemandFacts.Single(item =>
            item.DemandId == "INDUSTRY_PROMISE");
        Equal(CommercialCoreObligationKind.CityPromise, standardFact.ObligationKind,
            "standard result obligation fact");
        Check(standardFact.Supplied && standardFact.PathNodeIds.Count > 1 &&
            standardFact.PathEdgeIds.Contains("PLAYER_EDGE_1") &&
            standardFact.SourceNodeId is not null,
            "standard result omitted actual source/path facts");
        Check(standardResult.EmergencyAssetIds.Contains("PLAYER_EDGE_1") &&
            standardResult.ProtectiveOutageAssetIds.Contains("PLAYER_EDGE_1"),
            "standard result omitted emergency/protective facts");

        CommercialCoreRun reinforced = NewCoreRun();
        CompletePrelude(reinforced);
        CoreAccepted(reinforced.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Keep)), "reinforced keep promise");
        CommercialDecisionPreview reinforcedDraft = CompleteIndustryDraft(
            reinforced,
            "REINFORCED_LINE",
            "REINFORCED_POLE");
        Check(reinforcedDraft.Accepted, "reinforced draft preview failed");
        Equal(15L, reinforcedDraft.ProjectedMinute, "reinforced draft projected minute");
        Check(reinforcedDraft.ProjectedCashUnit < standardDraft.ProjectedCashUnit,
            "reinforced prototype was not more expensive");
        Check(reinforcedDraft.PhaseResults[0].Demands.Single(item =>
                item.DemandId == "INDUSTRY_PROMISE").EmergencyAssetIds.Count == 0,
            "reinforced prototype did not remain continuous: " + string.Join(",",
                reinforcedDraft.PhaseResults[0].Assets
                    .Where(item => item.UseKw > 0)
                    .Select(item => $"{item.AssetId}={item.UseKw}/{item.ContinuousLimitKw}")));
        CoreAccepted(reinforced.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.OrderLine)), "reinforced line order");
        CoreAccepted(reinforced.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.AdvanceConstruction)), "reinforced line completion");
        CoreAccepted(reinforced.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow)), "reinforced hot approval");
        CommercialCoreCommandResult reinforcedFinish = reinforced.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow));
        CoreAccepted(reinforcedFinish, "reinforced safety approval");
        Check(reinforcedFinish.CompletedChapter!.EmergencyAssetIds.Count == 0,
            "reinforced result reported emergency operation");
    }

    private void CheckCommercialCoreChoiceDeadlineAndAtomicity()
    {
        CommercialCoreRun missingPromise = NewCoreRun();
        CompletePrelude(missingPromise);
        string beforeChoice = JsonSerializer.Serialize(missingPromise.GetSnapshot());
        CommercialDecisionPreview choiceRequired = missingPromise.PreviewDecisionWindow();
        Check(!choiceRequired.Accepted &&
            choiceRequired.Error == CommercialCoreError.PromiseDecisionRequired,
            "core preview did not require an explicit promise decision");
        Equal(beforeChoice, JsonSerializer.Serialize(missingPromise.GetSnapshot()),
            "rejected missing-choice preview mutated state");

        CommercialCoreRun deferred = NewCoreRun();
        CompletePrelude(deferred);
        long cashBeforeChoice = deferred.GetSnapshot().CashUnit;
        CoreAccepted(deferred.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Defer)), "defer promise");
        Equal(cashBeforeChoice, deferred.GetSnapshot().CashUnit,
            "promise choice changed the authored grant");
        CommercialDecisionPreview deferPreview = deferred.PreviewDecisionWindow();
        Check(deferPreview.Accepted, "deferred promise blocked required progress");
        ThermalDemandResult deferredDemand = deferPreview.PhaseResults[0].Demands.Single(item =>
            item.DemandId == "INDUSTRY_PROMISE");
        Check(deferredDemand.Deferred && !deferredDemand.Supplied,
            "deferred promise was not excluded from supply candidates");
        CoreAccepted(deferred.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow)), "deferred hot approval");
        CommercialCoreCommandResult deferFinish = deferred.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow));
        CoreAccepted(deferFinish, "deferred safety approval");
        Equal(PromiseDecision.Defer, deferFinish.CompletedChapter!.PromiseDecision,
            "deferred result choice");

        CommercialCoreRun exactDeadline = NewCoreRun();
        CompletePrelude(exactDeadline);
        CoreAccepted(exactDeadline.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Defer)), "deadline defer promise");
        CompleteIndustryDraft(exactDeadline, "STANDARD_LINE", "STANDARD_POLE");
        CoreAccepted(exactDeadline.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.OrderLine)), "deadline first line order");
        CoreAccepted(exactDeadline.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.AdvanceConstruction)), "deadline first line completion");
        CoreAccepted(exactDeadline.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.StartLineDraft,
            StartNodeId: "NORTH_SUBSTATION",
            LineClassId: "STANDARD_LINE",
            PoleClassId: "STANDARD_POLE")), "deadline second line start");
        CoreAccepted(exactDeadline.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.FinishLineDraft,
            EndNodeId: "EAST_RESIDENTIAL_TERMINAL")), "deadline second line finish");
        CoreAccepted(exactDeadline.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.OrderLine)), "deadline second line order");
        CoreAccepted(exactDeadline.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.AdvanceConstruction)), "deadline second line completion");
        Equal(20L, exactDeadline.GetSnapshot().Construction.Minute, "exact deadline minute");
        Check(exactDeadline.PreviewDecisionWindow().Accepted,
            "exact authored deadline was rejected");

        CommercialCoreRun overdue = NewCoreRun();
        CompletePrelude(overdue);
        CoreAccepted(overdue.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Keep)), "overdue keep promise");
        CoreAccepted(overdue.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.StartLineDraft,
            StartNodeId: "WATER_TERMINAL",
            LineClassId: "STANDARD_LINE",
            PoleClassId: "STANDARD_POLE")), "overdue line start");
        CoreAccepted(overdue.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.AddLinePoint,
            Position: new MapPoint(2850, 1100))), "overdue line point");
        CoreAccepted(overdue.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.FinishLineDraft,
            EndNodeId: "INDUSTRY_TERMINAL")), "overdue line finish");
        CommercialDecisionPreview overdueDraft = overdue.PreviewDecisionWindow();
        Check(!overdueDraft.Accepted && overdueDraft.Error == CommercialCoreError.DeadlineExceeded &&
            overdueDraft.ProjectedMinute > 20,
            "complete draft beyond deadline was not rejected");
        CoreAccepted(overdue.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.OrderLine)), "overdue line order");
        CoreAccepted(overdue.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.AdvanceConstruction)), "overdue line completion");
        string overdueBeforeApproval = JsonSerializer.Serialize(overdue.GetSnapshot());
        CommercialCoreCommandResult overdueApproval = overdue.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow));
        Check(!overdueApproval.Accepted && overdueApproval.Error == CommercialCoreError.DeadlineExceeded,
            "commissioned project beyond deadline was approved");
        Equal(overdueBeforeApproval, JsonSerializer.Serialize(overdue.GetSnapshot()),
            "deadline rejection mutated campaign state");

        CommercialCoreSliceDefinition bottleneckSlice = CoreSliceWithIndustryDemand(3100);
        CommercialCoreRun bottleneck = new(_commercialWorld, bottleneckSlice);
        CompletePrelude(bottleneck);
        CoreAccepted(bottleneck.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Keep)), "bottleneck keep promise");
        CompleteIndustryDraft(bottleneck, "STANDARD_LINE", "STANDARD_POLE");
        CommercialDecisionPreview bottleneckPreview = bottleneck.PreviewDecisionWindow();
        Check(!bottleneckPreview.Accepted &&
            bottleneckPreview.Error == CommercialCoreError.KeptPromiseFailed &&
            bottleneckPreview.SupplyFailure == ThermalSupplyFailure.EmergencyLimit &&
            bottleneckPreview.FirstBottleneckAssetId == "PLAYER_EDGE_1",
            $"representative thermal bottleneck was not identified: " +
            $"{bottleneckPreview.Error}/{bottleneckPreview.SupplyFailure}/" +
            $"{bottleneckPreview.FirstBottleneckAssetId}");

        CommercialCoreRun invalid = NewCoreRun();
        string invalidBefore = JsonSerializer.Serialize(invalid.GetSnapshot());
        CommercialCoreCommandResult ignoredField = invalid.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.AddLinePoint,
            Position: new MapPoint(1, 1),
            EndNodeId: "EXTRA"));
        Check(!ignoredField.Accepted && ignoredField.Error == CommercialCoreError.InvalidCommand,
            "command with ignored extra field was accepted");
        Equal(invalidBefore, JsonSerializer.Serialize(invalid.GetSnapshot()),
            "invalid command shape mutated state");
        CommercialCoreCommandResult invalidPromise = invalid.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: (PromiseDecision)999));
        Check(!invalidPromise.Accepted && invalidPromise.Error == CommercialCoreError.InvalidCommand,
            "undefined promise decision enum was accepted");
    }

    private void CheckCommercialCoreRollbackAndFreshReplay()
    {
        CommercialCoreRun run = NewCoreRun();
        CompletePrelude(run);
        string chapterStart = JsonSerializer.Serialize(run.GetSnapshot());
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Keep)), "rollback keep promise");
        string beforeProject = JsonSerializer.Serialize(run.GetSnapshot());
        CompleteIndustryDraft(run, "STANDARD_LINE", "STANDARD_POLE");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.OrderLine)), "rollback project order");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.AdvanceConstruction)), "rollback project completion");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow)), "rollback hot approval");
        Check(run.GetSnapshot().DecisionWindowIndex == 1 &&
            run.GetSnapshot().ThermalMemory.Count > 0,
            "rollback setup did not advance phase and thermal state");
        CoreAccepted(run.RollbackRecentProject(), "recent project rollback");
        Equal(beforeProject, JsonSerializer.Serialize(run.GetSnapshot()),
            "recent rollback did not restore coordinates/cash/time/phases/promise/thermal state");

        CommercialCoreRun fresh = CommercialCoreRun.Restore(
            _commercialWorld,
            _coreSlice,
            run.GetCommands());
        Equal(JsonSerializer.Serialize(run.GetSnapshot()), JsonSerializer.Serialize(fresh.GetSnapshot()),
            "fresh replay after rollback snapshot");
        Equal(JsonSerializer.Serialize(run.GetCommands()), JsonSerializer.Serialize(fresh.GetCommands()),
            "fresh replay after rollback journal");

        CompleteIndustryDraft(run, "REINFORCED_LINE", "REINFORCED_POLE");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.CancelLineDraft)), "cancel replayed draft");
        CoreAccepted(run.RestartChapter(), "chapter restart");
        Equal(chapterStart, JsonSerializer.Serialize(run.GetSnapshot()),
            "chapter restart did not restore its journal prefix");
    }

    private void CheckCommercialCoreSaveV3()
    {
        CommercialCoreRun run = NewCoreRun();
        CompletePrelude(run);
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Keep)), "save keep promise");
        CompleteIndustryDraft(run, "REINFORCED_LINE", "REINFORCED_POLE");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.OrderLine)), "save line order");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.AdvanceConstruction)), "save line completion");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow)), "save hot approval");

        CommercialCoreCampaignSave save = CommercialCoreSaveCodec.Create(
            _commercialWorld,
            _commercialBytes,
            _coreSlice,
            _coreBytes,
            run.GetCommands());
        byte[] serialized = CommercialCoreSaveCodec.Serialize(save);
        CommercialCoreCampaignSave decoded = CommercialCoreSaveCodec.Deserialize(serialized);
        CommercialCoreRun restored = CommercialCoreSaveCodec.Restore(
            decoded,
            _commercialWorld,
            _commercialBytes,
            _coreSlice,
            _coreBytes);
        Equal(JsonSerializer.Serialize(run.GetSnapshot()), JsonSerializer.Serialize(restored.GetSnapshot()),
            "save to fresh restore state equality");
        Equal(CommercialCoreSaveCodec.ComputeSha256(_commercialBytes), decoded.WorldSha256,
            "save world content hash");
        Equal(CommercialCoreSaveCodec.ComputeSha256(_coreBytes), decoded.SliceSha256,
            "save core-slice content hash");

        string duplicate = Encoding.UTF8.GetString(serialized).Replace(
            "\"sliceId\":",
            "\"sliceId\": \"DUPLICATE\", \"sliceId\":",
            StringComparison.Ordinal);
        ExpectThrows<CommercialCorePersistenceException>(
            () => CommercialCoreSaveCodec.Deserialize(Encoding.UTF8.GetBytes(duplicate)),
            "duplicate save property");
        string unknown = Encoding.UTF8.GetString(serialized).Replace(
            "\"commands\":",
            "\"future\": true, \"commands\":",
            StringComparison.Ordinal);
        ExpectThrows<CommercialCorePersistenceException>(
            () => CommercialCoreSaveCodec.Deserialize(Encoding.UTF8.GetBytes(unknown)),
            "unknown save property");
        JsonObject nullCommands = JsonNode.Parse(serialized)!.AsObject();
        nullCommands["commands"] = null;
        ExpectThrows<CommercialCorePersistenceException>(
            () => CommercialCoreSaveCodec.Deserialize(
                Encoding.UTF8.GetBytes(nullCommands.ToJsonString())),
            "null save command list");
        ExpectThrows<CommercialCorePersistenceException>(
            () => CommercialCoreSaveCodec.Restore(
                decoded,
                _commercialWorld,
                [.. _commercialBytes, (byte)0],
                _coreSlice,
                _coreBytes),
            "save world hash mismatch");

        string directory = Path.Combine(
            Path.GetTempPath(),
            $"gridworks-commercial-save-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, CommercialCorePersistenceStore.SaveFileName);
        try
        {
            Equal(CommercialCoreDocumentLoadStatus.Missing,
                CommercialCorePersistenceStore.Load(path).Status,
                "missing commercial save status");
            CommercialCorePersistenceStore.Save(path, save);
            CommercialCoreSaveLoadResult loaded = CommercialCorePersistenceStore.Load(path);
            Equal(CommercialCoreDocumentLoadStatus.Loaded, loaded.Status,
                "stored commercial save status");
            Check(loaded.Save is not null && !File.Exists(path + ".tmp"),
                "atomic commercial save left no load or temporary file");
            File.WriteAllText(path, "{invalid", Encoding.UTF8);
            Equal(CommercialCoreDocumentLoadStatus.Invalid,
                CommercialCorePersistenceStore.Load(path).Status,
                "invalid commercial save status");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private void CheckCommercialSettingsV3()
    {
        const string v2 = """
            {
              "schemaVersion": "gridworks.settings.v2",
              "windowMode": "fullscreen",
              "uiScalePercent": 125,
              "showControlHelp": false,
              "masterVolumePercent": 75,
              "ambientVolumePercent": 50,
              "sfxVolumePercent": 25
            }
            """;
        CommercialSettingsV3 migrated = CommercialSettingsCodec.Deserialize(
            Encoding.UTF8.GetBytes(v2),
            out bool migratedFromV2);
        Check(migratedFromV2, "settings v2 was not reported as migrated");
        Equal(CommercialSettingsV3.SupportedSchemaVersion, migrated.SchemaVersion,
            "migrated settings schema");
        Equal(CommercialWindowMode.Fullscreen, migrated.WindowMode,
            "migrated window mode");
        Equal(125, migrated.UiScalePercent, "migrated UI scale");
        Check(!migrated.ShowControlHelp, "migrated control-help value changed");
        Equal(75, migrated.MasterVolumePercent, "migrated Master volume");
        Equal(50, migrated.AmbientVolumePercent, "migrated Ambient volume");
        Equal(25, migrated.SfxVolumePercent, "migrated SFX volume");
        Check(!migrated.ReduceMotion, "v2 migration did not default ReduceMotion off");

        byte[] v3Bytes = CommercialSettingsCodec.Serialize(migrated);
        string v3 = Encoding.UTF8.GetString(v3Bytes);
        Check(v3.Contains("\"schemaVersion\": \"gridworks.settings.v3\"", StringComparison.Ordinal),
            "settings writer did not emit v3");
        Check(v3.Contains("\"reduceMotion\": false", StringComparison.Ordinal),
            "settings writer omitted ReduceMotion");
        CommercialSettingsV3 roundTrip = CommercialSettingsCodec.Deserialize(
            v3Bytes,
            out bool roundTripMigrated);
        Check(!roundTripMigrated && roundTrip == migrated,
            "settings v3 round trip changed values");

        ExpectThrows<CommercialCorePersistenceException>(
            () => CommercialSettingsCodec.Deserialize(
                Encoding.UTF8.GetBytes(v2.Replace(
                    "\"uiScalePercent\": 125,",
                    "\"uiScalePercent\": 125, \"uiScalePercent\": 100,",
                    StringComparison.Ordinal)),
                out _),
            "duplicate settings property");
        ExpectThrows<CommercialCorePersistenceException>(
            () => CommercialSettingsCodec.Deserialize(
                Encoding.UTF8.GetBytes(v2.Replace(
                    "\"showControlHelp\": false,",
                    "\"future\": true, \"showControlHelp\": false,",
                    StringComparison.Ordinal)),
                out _),
            "unknown settings property");
        ExpectThrows<CommercialCorePersistenceException>(
            () => CommercialSettingsCodec.Deserialize(
                Encoding.UTF8.GetBytes(v2.Replace(
                    "  \"sfxVolumePercent\": 25\n",
                    string.Empty,
                    StringComparison.Ordinal).Replace(
                    "\"ambientVolumePercent\": 50,",
                    "\"ambientVolumePercent\": 50",
                    StringComparison.Ordinal)),
                out _),
            "missing settings v2 field");
        ExpectThrows<CommercialCorePersistenceException>(
            () => CommercialSettingsCodec.Deserialize(
                Encoding.UTF8.GetBytes(v3.Replace(
                    ",\n  \"reduceMotion\": false",
                    string.Empty,
                    StringComparison.Ordinal)),
                out _),
            "missing ReduceMotion in settings v3");
        ExpectThrows<CommercialCorePersistenceException>(
            () => CommercialSettingsCodec.Serialize(migrated with { UiScalePercent = 110 }),
            "invalid settings UI scale");

        string directory = Path.Combine(
            Path.GetTempPath(),
            $"gridworks-commercial-settings-{Guid.NewGuid():N}");
        string path = Path.Combine(directory, CommercialSettingsPersistenceStore.SettingsFileName);
        try
        {
            Equal(CommercialCoreDocumentLoadStatus.Missing,
                CommercialSettingsPersistenceStore.Load(path).Status,
                "missing settings status");
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(path, Encoding.UTF8.GetBytes(v2));
            CommercialSettingsLoadResult loaded = CommercialSettingsPersistenceStore.Load(path);
            Equal(CommercialCoreDocumentLoadStatus.Loaded, loaded.Status,
                "migrated settings load status");
            Check(loaded.MigratedFromV2, "settings store did not report migration");
            Check(File.ReadAllText(path, Encoding.UTF8).Contains(
                    "gridworks.settings.v3",
                    StringComparison.Ordinal),
                "settings store did not atomically replace v2 with v3");
            Check(!File.Exists(path + ".tmp"), "settings migration left a temporary file");

            CommercialSettingsV3 changed = loaded.Settings with
            {
                ReduceMotion = true,
                WindowMode = CommercialWindowMode.Windowed,
            };
            CommercialSettingsPersistenceStore.Save(path, changed);
            CommercialSettingsLoadResult reloaded = CommercialSettingsPersistenceStore.Load(path);
            Check(reloaded.Status == CommercialCoreDocumentLoadStatus.Loaded &&
                  reloaded.Settings == changed &&
                  !reloaded.MigratedFromV2 &&
                  !File.Exists(path + ".tmp"),
                "atomic settings v3 save did not round trip cleanly");
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, recursive: true);
            }
        }
    }

    private void CheckThermalBoundariesAndRouteOrder()
    {
        CommercialWorldDefinition boundaryWorld = ThermalWorld(
        [
            Node("S", 100, 100),
            Node("A", 300, 100, PoleClassId),
            Node("L", 500, 100, LoadClassId),
        ],
        [Edge("E1", "S", "A"), Edge("E2", "A", "L")],
        continuous: 100,
        emergency: 150);

        ThermalIntervalResult exactContinuous = EvaluateOne(
            boundaryWorld,
            Interval("P", Demand("D", "L", 100, ThermalObligationKind.SafetyDuty)));
        Check(exactContinuous.Demands[0].Supplied, "exact continuous load was rejected");
        Check(exactContinuous.Assets.All(item => item.CurrentState == ThermalOperatingState.Continuous),
            "exact continuous load entered emergency state");

        ThermalIntervalResult aboveContinuous = EvaluateOne(
            boundaryWorld,
            Interval("P", Demand(
                "D",
                "L",
                101,
                ThermalObligationKind.CityPromise,
                emergencyApproved: true)));
        Check(aboveContinuous.Demands[0].Supplied, "approved load above continuous was rejected");
        Check(aboveContinuous.Assets.Any(item => item.CurrentState == ThermalOperatingState.Emergency),
            "load above continuous did not enter emergency state");

        ThermalIntervalResult exactEmergency = EvaluateOne(
            boundaryWorld,
            Interval("P", Demand(
                "D",
                "L",
                150,
                ThermalObligationKind.CityPromise,
                emergencyApproved: true)));
        Check(exactEmergency.Demands[0].Supplied, "exact emergency load was rejected");
        ThermalIntervalResult overEmergency = EvaluateOne(
            boundaryWorld,
            Interval("P", Demand(
                "D",
                "L",
                151,
                ThermalObligationKind.CityPromise,
                emergencyApproved: true)));
        Check(!overEmergency.Demands[0].Supplied &&
            overEmergency.Demands[0].Failure == ThermalSupplyFailure.EmergencyLimit,
            "load above emergency did not return the typed limit failure");

        CommercialWorldDefinition routeWorld = ThermalWorld(
        [
            Node("S", 100, 500),
            Node("SHORT_POLE", 300, 500, PoleClassId),
            Node("LONG_POLE_1", 100, 800, PoleClassId),
            Node("LONG_POLE_2", 400, 800, PoleClassId),
            Node("L", 700, 500, LoadClassId),
        ],
        [
            Edge("SHORT_1", "S", "SHORT_POLE"),
            Edge("SHORT_2", "SHORT_POLE", "L"),
            Edge("LONG_1", "S", "LONG_POLE_1"),
            Edge("LONG_2", "LONG_POLE_1", "LONG_POLE_2"),
            Edge("LONG_3", "LONG_POLE_2", "L"),
        ],
        continuous: 500,
        emergency: 700);
        ThermalIntervalDefinition routeInterval = Interval(
            "ROUTE",
            Demand("D", "L", 200, ThermalObligationKind.CityPromise, emergencyApproved: true),
            overrides: [new ThermalLimitOverride("SHORT_2", 100, 500)]);
        ThermalDemandResult route = EvaluateOne(routeWorld, routeInterval).Demands[0];
        SequenceEqual(["LONG_1", "LONG_2", "LONG_3"], route.PathEdgeIds,
            "long continuous route must outrank short emergency route");

        CommercialWorldDefinition sourceOrderWorld = ThermalWorld(
        [
            Node("S0", 100, 50),
            Node("S1", 100, 150),
            Node("H", 300, 100, PoleClassId),
            Node("L", 500, 100, LoadClassId),
        ],
        [Edge("S0_H", "S0", "H"), Edge("S1_H", "S1", "H"), Edge("H_L", "H", "L")],
        continuous: 500,
        emergency: 700);
        Equal("S0", EvaluateOne(
                sourceOrderWorld,
                Interval("SOURCE_ORDER", Demand("D", "L", 100, ThermalObligationKind.SafetyDuty)))
            .Demands[0].SourceNodeId,
            "authored source order tie-break");
    }

    private void CheckThermalSharedPermissionsAndBottleneck()
    {
        CommercialWorldDefinition world = SharedThermalWorld();
        ThermalLimitOverride hubLimit = new("HUB", 300, 400);
        ThermalDemandDefinition first = Demand(
            "SAFETY",
            "L1",
            180,
            ThermalObligationKind.SafetyDuty);

        ThermalIntervalResult operatingRejected = EvaluateOne(
            world,
            Interval(
                "OPERATING",
                first,
                Demand("RECORD", "L2", 180, ThermalObligationKind.OperatingRecord),
                overrides: [hubLimit]));
        ThermalDemandResult rejected = operatingRejected.Demands[1];
        Check(!rejected.Supplied && rejected.Failure == ThermalSupplyFailure.ContinuousPermission,
            "operating record incorrectly used emergency capacity");
        Equal("HUB", rejected.FirstBottleneckAssetId,
            "shared connector must be the first typed bottleneck");

        ThermalIntervalResult promiseApproved = EvaluateOne(
            world,
            Interval(
                "PROMISE",
                first,
                Demand(
                    "PROMISE_LOAD",
                    "L2",
                    180,
                    ThermalObligationKind.CityPromise,
                    emergencyApproved: true),
                overrides: [hubLimit]));
        Check(promiseApproved.Demands[1].Supplied, "approved promise did not use emergency capacity");
        ThermalAssetResult hub = promiseApproved.Assets.Single(item => item.AssetId == "HUB");
        Equal(360L, hub.UseKw, "shared connector aggregate use");
        Equal(ThermalOperatingState.Emergency, hub.CurrentState, "shared connector emergency state");
        Equal(ThermalOperatingState.ProtectiveOutage, hub.NextState,
            "shared connector next protective state");

        ThermalIntervalResult promiseUnapproved = EvaluateOne(
            world,
            Interval(
                "PROMISE_UNAPPROVED",
                Demand("P", "L1", 350, ThermalObligationKind.CityPromise),
                overrides: [hubLimit]));
        Check(!promiseUnapproved.Demands[0].Supplied &&
            promiseUnapproved.Demands[0].Failure == ThermalSupplyFailure.ContinuousPermission,
            "unapproved promise used emergency capacity");

        ThermalIntervalResult ordinarySafety = EvaluateOne(
            world,
            Interval(
                "ORDINARY_SAFETY",
                Demand("S", "L1", 350, ThermalObligationKind.SafetyDuty),
                policy: ThermalIntervalPolicy.SafetyEmergencyAllowed,
                overrides: [hubLimit]));
        Check(!ordinarySafety.Demands[0].Supplied,
            "ordinary safety duty incorrectly used named-emergency permission");

        ThermalIntervalResult namedSafety = EvaluateOne(
            world,
            Interval(
                "NAMED_SAFETY",
                Demand(
                    "S",
                    "L1",
                    350,
                    ThermalObligationKind.SafetyDuty,
                    namedEmergency: true),
                policy: ThermalIntervalPolicy.SafetyEmergencyAllowed,
                overrides: [hubLimit]));
        Check(namedSafety.Demands[0].Supplied, "named emergency safety duty was rejected");

        ThermalSequenceResult futureGuard = ThermalEvaluator.Evaluate(
            world,
            new ThermalSequenceRequest(
            [
                Interval(
                    "NAMED",
                    Demand(
                        "S1",
                        "L1",
                        350,
                        ThermalObligationKind.SafetyDuty,
                        namedEmergency: true),
                    policy: ThermalIntervalPolicy.SafetyEmergencyAllowed,
                    overrides: [hubLimit]),
                Interval(
                    "PUBLIC_NEXT",
                    Demand("S2", "L1", 200, ThermalObligationKind.SafetyDuty),
                    overrides: [hubLimit]),
            ],
            Array.Empty<ThermalAssetMemory>()));
        Check(!futureGuard.Intervals[0].Demands[0].Supplied &&
            futureGuard.Intervals[0].Demands[0].Failure == ThermalSupplyFailure.FutureSafetyDuty,
            "named emergency use broke a published next safety duty");

        ThermalIntervalResult deferred = EvaluateOne(
            world,
            Interval(
                "DEFERRED",
                Demand(
                    "P",
                    "L1",
                    350,
                    ThermalObligationKind.CityPromise,
                    included: false,
                    emergencyApproved: true),
                overrides: [hubLimit]));
        Check(deferred.Demands[0].Deferred && !deferred.Demands[0].Supplied,
            "deferred promise remained an active demand candidate");
    }

    private void CheckThermalProtectionCoolingAndDeterminism()
    {
        CommercialWorldDefinition world = SharedThermalWorld();
        ThermalLimitOverride hubLimit = new("HUB", 300, 400);
        ThermalSequenceRequest request = new(
        [
            Interval(
                "HOT",
                Demand(
                    "PROMISE",
                    "L1",
                    350,
                    ThermalObligationKind.CityPromise,
                    emergencyApproved: true),
                overrides: [hubLimit]),
            Interval("COOL", overrides: [hubLimit]),
            Interval(
                "RETURN",
                Demand("SAFETY", "L1", 200, ThermalObligationKind.SafetyDuty),
                overrides: [hubLimit]),
        ],
        Array.Empty<ThermalAssetMemory>());

        ThermalSequenceResult first = ThermalEvaluator.Evaluate(world, request);
        ThermalAssetResult hotHub = first.Intervals[0].Assets.Single(item => item.AssetId == "HUB");
        ThermalAssetResult coolingHub = first.Intervals[1].Assets.Single(item => item.AssetId == "HUB");
        ThermalAssetResult returnedHub = first.Intervals[2].Assets.Single(item => item.AssetId == "HUB");
        Equal(ThermalOperatingState.Emergency, hotHub.CurrentState, "hot phase state");
        Equal(ThermalOperatingState.ProtectiveOutage, coolingHub.CurrentState,
            "next full phase protective outage");
        Equal(0L, coolingHub.UseKw, "protective phase must remain unloaded");
        Equal(ThermalOperatingState.Continuous, returnedHub.CurrentState,
            "asset did not return after one unloaded cooling phase");
        Check(first.Intervals[2].Demands[0].Supplied, "cooled asset did not resume supply");

        string evaluated = JsonSerializer.Serialize(first);
        string repeated = JsonSerializer.Serialize(ThermalEvaluator.Evaluate(world, request));
        string preview = JsonSerializer.Serialize(ThermalEvaluator.Preview(world, request));
        Equal(evaluated, repeated, "thermal sequence repeat determinism");
        Equal(evaluated, preview, "thermal preview/evaluation value equality");

        ExpectThrows<ThermalEvaluationException>(
            () => ThermalEvaluator.Evaluate(
                world,
                new ThermalSequenceRequest(
                [
                    Interval(
                        "BAD_OVERRIDE",
                        overrides: [new ThermalLimitOverride("HUB", 401, 501)]),
                ],
                Array.Empty<ThermalAssetMemory>())),
            "thermal override must only lower the class limits");
    }

    private void CheckThermalReviewRegressions()
    {
        CommercialWorldDefinition shared = SharedThermalWorld();
        ExpectThrows<ThermalEvaluationException>(
            () => EvaluateOne(
                shared,
                Interval(
                    "DEFERRED_SAFETY",
                    Demand(
                        "S",
                        "L1",
                        100,
                        ThermalObligationKind.SafetyDuty,
                        included: false))),
            "mandatory safety duty cannot be deferred");
        ExpectThrows<ThermalEvaluationException>(
            () => EvaluateOne(
                shared,
                Interval(
                    "DEFERRED_RECORD",
                    Demand(
                        "R",
                        "L1",
                        100,
                        ThermalObligationKind.OperatingRecord,
                        included: false))),
            "operating record cannot be deferred");

        ThermalIntervalResult unavailableEndpoint = EvaluateOne(
            shared,
            Interval(
                "UNAVAILABLE_ENDPOINT",
                Demand("S", "L1", 100, ThermalObligationKind.SafetyDuty),
                unavailable: ["L1"]));
        Check(!unavailableEndpoint.Demands[0].Supplied &&
            unavailableEndpoint.Demands[0].Failure == ThermalSupplyFailure.UnavailableAsset &&
            unavailableEndpoint.Demands[0].FirstBottleneckAssetId == "L1",
            "unavailable nonthermal load endpoint still received supply");

        CommercialWorldDefinition transitWorld = ThermalWorld(
        [
            Node("S", 100, 100),
            Node("TRANSIT_LOAD", 300, 100, LoadClassId),
            Node("L", 500, 100, LoadClassId),
        ],
        [Edge("E1", "S", "TRANSIT_LOAD"), Edge("E2", "TRANSIT_LOAD", "L")],
        continuous: 500,
        emergency: 700);
        ThermalDemandResult unavailableTransit = EvaluateOne(
            transitWorld,
            Interval(
                "UNAVAILABLE_TRANSIT",
                Demand("D", "L", 100, ThermalObligationKind.SafetyDuty),
                unavailable: ["TRANSIT_LOAD"]))
            .Demands[0];
        Check(!unavailableTransit.Supplied &&
            unavailableTransit.FirstBottleneckAssetId == "TRANSIT_LOAD",
            "unavailable nonthermal transit endpoint still carried supply");

        CommercialWorldDefinition outsideServiceArea = ThermalWorld(
        [
            Node("S", 100, 400),
            Node("SUB", 500, 400, SubstationClassId),
            Node("P", 900, 400, PoleClassId),
            Node("L", 1300, 400, LoadClassId),
        ],
        [
            Edge("S_SUB", "S", "SUB"),
            Edge("SUB_P", "SUB", "P"),
            Edge("P_L", "P", "L"),
        ],
        continuous: 500,
        emergency: 700);
        ThermalDemandResult outsideServiceResult = EvaluateOne(
            outsideServiceArea,
            Interval(
                "OUTSIDE_SERVICE_AREA",
                Demand("D", "L", 100, ThermalObligationKind.SafetyDuty) with
                {
                    RequireSubstationPath = true,
                }))
            .Demands[0];
        Check(!outsideServiceResult.Supplied &&
            outsideServiceResult.Failure == ThermalSupplyFailure.NoPath,
            "a path through a distant substation served a load outside its service area");

        CommercialWorldDefinition manyPaths = DiamondThermalWorld(17);
        ThermalDemandResult routed = EvaluateOne(
            manyPaths,
            Interval(
                "MANY_PATHS",
                Demand("D", "L", 1, ThermalObligationKind.SafetyDuty)))
            .Demands[0];
        Check(routed.Supplied && routed.PathEdgeIds.Count == 34,
            "all-simple-path evaluation aborted above the former 100,000-path limit");
    }

    private CommercialCoreRun NewCoreRun() => new(_commercialWorld, _coreSlice);

    private CommercialCoreRun CompleteCampaignFirstThree(
        CommercialWorldDefinition? world = null)
    {
        var run = new CommercialCoreRun(world ?? _commercialWorld, _campaign);
        SpatialNodeDefinition reservedSource = run.GetSnapshot().Construction.World.Nodes.Single(item =>
            item.NodeId == "WEST_AUXILIARY");
        Check(reservedSource.Reserved && !reservedSource.Commissioned &&
            run.PreviewLineStart(
                "WEST_AUXILIARY",
                "REINFORCED_LINE",
                "REINFORCED_POLE").Error == ConstructionError.EndpointNotCommissioned,
            "future second source was not footprint-reserved and electrically locked");
        BuildCampaignSubstation(run, new MapPoint(2200, 750), "first-light substation");
        BuildCampaignLine(
            run,
            "WEST_SOURCE",
            "PLAYER_SUBSTATION_1",
            [
                new MapPoint(650, 700),
                new MapPoint(950, 500),
                new MapPoint(1545, 450),
                new MapPoint(1900, 600),
            ],
            "first-light feeder",
            poleClassId: "STANDARD_POLE");
        BuildCampaignLine(
            run,
            "PLAYER_SUBSTATION_1",
            "EAST_RESIDENTIAL_TERMINAL",
            Array.Empty<MapPoint>(),
            "first-light service line",
            poleClassId: "STANDARD_POLE");
        CommercialCoreCommandResult first = run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow));
        CoreAccepted(first, "first-light approval");
        Check(ReferenceEquals(
                _storyParts.Select("FIRST_LIGHT/result/standard").Story,
                first.CompletedChapter!.Story),
            "first-light command witness did not reach its standard story selector");
        Equal("SECOND_HEART", first.Snapshot.Chapter.ChapterId,
            "first-light chapter transition");
        Check(first.Snapshot.Construction.World.Edges.Any(item => item.EdgeId == "PLAYER_EDGE_1"),
            "first-light network did not carry into second-heart");

        BuildCampaignLine(
            run,
            "PLAYER_SUBSTATION_1",
            "HOSPITAL_TERMINAL",
            [new MapPoint(2000, 1100)],
            "second-heart north route");
        CommercialDecisionPreview singleCorridor = run.PreviewDecisionWindow();
        Check(!singleCorridor.Accepted &&
            singleCorridor.Error == CommercialCoreError.SafetyDutyFailed &&
            singleCorridor.FailedDemandId == "HOSPITAL_NORTH_TEST",
            "second-heart accepted one corridor against both cutover tests");
        BuildCampaignSubstation(run, new MapPoint(2100, 1450), "second-heart south substation");
        BuildCampaignLine(
            run,
            "WEST_SOURCE",
            "PLAYER_SUBSTATION_2",
            [
                new MapPoint(550, 1150),
                new MapPoint(950, 1450),
                new MapPoint(1170, 1750),
                new MapPoint(1760, 1750),
                new MapPoint(2050, 1650),
            ],
            "second-heart south feeder");
        BuildCampaignLine(
            run,
            "PLAYER_SUBSTATION_2",
            "HOSPITAL_TERMINAL",
            Array.Empty<MapPoint>(),
            "second-heart south service line");
        CommercialDecisionPreview heartPreview = run.PreviewDecisionWindow();
        Check(heartPreview.Accepted, $"second-heart preview failed: {heartPreview.Error}/" +
            $"{heartPreview.FailedDemandId}/{heartPreview.SupplyFailure}/" +
            $"{heartPreview.FirstBottleneckAssetId}");
        ThermalDemandResult northTest = heartPreview.PhaseResults[0].Demands[0];
        ThermalDemandResult floodTest = heartPreview.PhaseResults[1].Demands[0];
        Check(northTest.Supplied && floodTest.Supplied &&
            !northTest.PathEdgeIds.SequenceEqual(floodTest.PathEdgeIds, StringComparer.Ordinal),
            "second-heart tests did not select two surviving spatial corridors");
        CommercialCoreCommandResult second = run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow));
        CoreAccepted(second, "second-heart approval");
        Check(ReferenceEquals(
                _storyParts.Select("SECOND_HEART/result/standard").Story,
                second.CompletedChapter!.Story),
            "second-heart command witness did not reach its standard story selector");
        Equal("SECOND_SOURCE", second.Snapshot.Chapter.ChapterId,
            "second-heart chapter transition");
        SpatialNodeDefinition activatedSource = second.Snapshot.Construction.World.Nodes.Single(item =>
            item.NodeId == "WEST_AUXILIARY");
        Check(activatedSource.Commissioned && !activatedSource.Reserved,
            "second source did not activate at its authored mission boundary");
        CommercialDecisionPreview disconnectedSource = run.PreviewDecisionWindow();
        Check(!disconnectedSource.Accepted &&
            disconnectedSource.Error == CommercialCoreError.SafetyDutyFailed,
            "second-source mission approved before the new source joined the carried network");

        BuildCampaignLine(
            run,
            "WEST_AUXILIARY",
            "PLAYER_POLE_1",
            Array.Empty<MapPoint>(),
            "second-source residential tie");
        BuildCampaignLine(
            run,
            "WEST_AUXILIARY",
            "PLAYER_POLE_6",
            [new MapPoint(900, 1200)],
            "second-source hospital tie");
        CommercialDecisionPreview sourcePreview = run.PreviewDecisionWindow();
        Check(sourcePreview.Accepted, $"second-source preview failed: {sourcePreview.Error}/" +
            $"{sourcePreview.FailedDemandId}/{sourcePreview.FirstBottleneckAssetId}");
        Check(sourcePreview.PhaseResults[0].Demands.All(item =>
                item.Supplied && item.SourceNodeId == "WEST_AUXILIARY"),
            "second-source acceptance test did not use the available authored source");
        CommercialCoreCommandResult third = run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow));
        CoreAccepted(third, "second-source approval");
        Check(ReferenceEquals(
                _storyParts.Select("SECOND_SOURCE/result/standard").Story,
                third.CompletedChapter!.Story),
            "second-source command witness did not reach its standard story selector");
        Equal("NORTH_BANK_PROMISE", third.Snapshot.Chapter.ChapterId,
            "second-source chapter transition");
        return run;
    }

    private CommercialCoreRun CompleteCampaignFirstFour()
    {
        CommercialCoreRun run = CompleteCampaignFirstThree();
        BuildCampaignLine(
            run,
            "PLAYER_SUBSTATION_1",
            "WATER_TERMINAL",
            Array.Empty<MapPoint>(),
            "fourth-mission water branch");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Keep)), "fourth-mission keep choice");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow)), "fourth-mission approval");
        Equal("WHOSE_MARGIN", run.GetSnapshot().Chapter.ChapterId,
            "first-four checkpoint transition");
        return run;
    }

    private void CompleteCampaignFromMissionSix(CommercialCoreRun run, string label)
    {
        Equal("BEFORE_WATER_REACHES", run.GetSnapshot().Chapter.ChapterId,
            $"{label} mission-six start");
        BuildCampaignLine(
            run,
            "WATER_TERMINAL",
            "PLAYER_SUBSTATION_3",
            Array.Empty<MapPoint>(),
            $"{label} flood bypass");
        CommercialDecisionPreview flood = run.PreviewDecisionWindow();
        Check(flood.Accepted,
            $"{label} future mission-six softlock: {flood.Error}/{flood.FailedDemandId}/" +
            $"{flood.SupplyFailure}/{flood.FirstBottleneckAssetId}");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow)),
            $"{label} mission-six approval");
        CompleteCampaignFromMissionSeven(run, label);
    }

    private void CompleteCampaignFromMissionSeven(CommercialCoreRun run, string label)
    {
        Equal("SHUT_DOWN_TO_KEEP", run.GetSnapshot().Chapter.ChapterId,
            $"{label} mission-seven start");
        BuildCampaignLine(
            run,
            "PLAYER_SUBSTATION_2",
            "PLAYER_POLE_15",
            Array.Empty<MapPoint>(),
            $"{label} maintenance tie");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Keep)),
            $"{label} mission-seven keep choice");
        CommercialDecisionPreview maintenance = run.PreviewDecisionWindow();
        Check(maintenance.Accepted,
            $"{label} future mission-seven softlock: {maintenance.Error}/" +
            $"{maintenance.FailedDemandId}/{maintenance.SupplyFailure}/" +
            $"{maintenance.FirstBottleneckAssetId}");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow)),
            $"{label} mission-seven approval");
        CompleteCampaignFromMissionEight(run, label);
    }

    private void CompleteCampaignFromMissionEight(CommercialCoreRun run, string label)
    {
        Equal("LONGEST_NIGHT", run.GetSnapshot().Chapter.ChapterId,
            $"{label} mission-eight start");
        BuildCampaignLine(
            run,
            "HOSPITAL_TERMINAL",
            "PLAYER_POLE_15",
            Array.Empty<MapPoint>(),
            $"{label} last-night hospital tie");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetPromiseDecision,
            PromiseDecision: PromiseDecision.Defer)),
            $"{label} mission-eight defer choice");
        CommercialDecisionPreview heat = run.PreviewDecisionWindow();
        Check(heat.Accepted,
            $"{label} future mission-eight heat softlock: {heat.Error}/{heat.FailedDemandId}/" +
            $"{heat.SupplyFailure}/{heat.FirstBottleneckAssetId}");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow)),
            $"{label} mission-eight heat approval");
        CommercialDecisionPreview storm = run.PreviewDecisionWindow();
        Check(storm.Accepted,
            $"{label} future mission-eight storm softlock: {storm.Error}/{storm.FailedDemandId}/" +
            $"{storm.SupplyFailure}/{storm.FirstBottleneckAssetId}");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow)),
            $"{label} mission-eight storm approval");
        Check(run.GetSnapshot().CampaignComplete && run.GetSnapshot().ChapterResults.Count == 8,
            $"{label} did not reach the epilogue-ready completed state");
    }

    private void BuildMissionFiveIndustryService(
        CommercialCoreRun run,
        string serviceLineClassId)
    {
        BuildCampaignSubstation(run, new MapPoint(2700, 1150), "industry service substation");
        BuildCampaignLine(
            run,
            "WEST_AUXILIARY",
            "PLAYER_SUBSTATION_3",
            [
                new MapPoint(900, 700),
                new MapPoint(1050, 1050),
                new MapPoint(1650, 1050),
                new MapPoint(2100, 1050),
                new MapPoint(2600, 1100),
            ],
            "industry reinforced feeder");
        BuildCampaignLine(
            run,
            "PLAYER_SUBSTATION_3",
            "INDUSTRY_TERMINAL",
            Array.Empty<MapPoint>(),
            "industry service line",
            lineClassId: serviceLineClassId,
            poleClassId: serviceLineClassId == "STANDARD_LINE"
                ? "STANDARD_POLE"
                : "REINFORCED_POLE");
    }

    private void BuildCampaignSubstation(
        CommercialCoreRun run,
        MapPoint position,
        string label)
    {
        BuildCampaignNode(run, position, "SMALL_SUBSTATION", label);
    }

    private void BuildCampaignNode(
        CommercialCoreRun run,
        MapPoint position,
        string nodeClassId,
        string label)
    {
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.SetNodeDraft,
            Position: position,
            NodeClassId: nodeClassId)), $"{label} draft");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.OrderNode)), $"{label} order");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.AdvanceConstruction)), $"{label} completion");
    }

    private void BuildCampaignLine(
        CommercialCoreRun run,
        string startNodeId,
        string endNodeId,
        IReadOnlyList<MapPoint> points,
        string label,
        string lineClassId = "REINFORCED_LINE",
        string poleClassId = "REINFORCED_POLE")
    {
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.StartLineDraft,
            StartNodeId: startNodeId,
            LineClassId: lineClassId,
            PoleClassId: poleClassId)), $"{label} start");
        foreach (MapPoint point in points)
        {
            CoreAccepted(run.Apply(new CommercialCoreCommand(
                CommercialCoreCommandKind.AddLinePoint,
                Position: point)), $"{label} point {point}");
        }
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.FinishLineDraft,
            EndNodeId: endNodeId)), $"{label} finish");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.OrderLine)), $"{label} order");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.AdvanceConstruction)), $"{label} completion");
    }

    private void CompletePrelude(CommercialCoreRun run)
    {
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.StartLineDraft,
            StartNodeId: "WEST_SOURCE",
            LineClassId: "REINFORCED_LINE",
            PoleClassId: "STANDARD_POLE")), "prelude line start");
        foreach (MapPoint point in new[]
        {
            new MapPoint(650, 700),
            new MapPoint(1030, 500),
            new MapPoint(1560, 500),
            new MapPoint(2000, 600),
            new MapPoint(2400, 700),
        })
        {
            CoreAccepted(run.Apply(new CommercialCoreCommand(
                CommercialCoreCommandKind.AddLinePoint,
                Position: point)), "prelude line point");
        }
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.FinishLineDraft,
            EndNodeId: "EAST_RESIDENTIAL_TERMINAL")), "prelude line finish");
        CommercialDecisionPreview draftPreview = run.PreviewDecisionWindow();
        Check(draftPreview.Accepted, "prelude complete-draft preview failed");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.OrderLine)), "prelude line order");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.AdvanceConstruction)), "prelude line completion");
        CommercialDecisionPreview commissioned = run.PreviewDecisionWindow();
        Check(commissioned.Accepted, "commissioned prelude preview failed");
        Equal(JsonSerializer.Serialize(draftPreview), JsonSerializer.Serialize(commissioned),
            "prelude complete-draft preview equals commissioned preview");
        CommercialCoreCommandResult approval = run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.ApproveDecisionWindow));
        CoreAccepted(approval, "prelude approval");
        Equal(JsonSerializer.Serialize(commissioned),
            JsonSerializer.Serialize(approval.DecisionPreview),
            "public prelude preview equals approval result");
        Equal("WHOSE_MARGIN", approval.Snapshot.Chapter.ChapterId,
            "prelude did not transition to the commercial core");
    }

    private CommercialDecisionPreview CompleteIndustryDraft(
        CommercialCoreRun run,
        string lineClassId,
        string poleClassId)
    {
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.StartLineDraft,
            StartNodeId: "WATER_TERMINAL",
            LineClassId: lineClassId,
            PoleClassId: poleClassId)), "industry line start");
        CoreAccepted(run.Apply(new CommercialCoreCommand(
            CommercialCoreCommandKind.FinishLineDraft,
            EndNodeId: "INDUSTRY_TERMINAL")), "industry line finish");
        return run.PreviewDecisionWindow();
    }

    private CommercialCoreSliceDefinition CoreSliceWithIndustryDemand(long demandKw)
    {
        CommercialCoreChapter chapter = _coreSlice.Chapters[1];
        CommercialCoreOperatingPhase hot = chapter.OperatingPhases[0];
        CommercialCoreOperatingPhase changedHot = hot with
        {
            Loads = hot.Loads.Select(load => load.LoadId == "INDUSTRY_PROMISE"
                ? load with { DemandKw = demandKw }
                : load).ToArray(),
        };
        CommercialCoreChapter changedChapter = chapter with
        {
            OperatingPhases = chapter.OperatingPhases.Select(phase =>
                phase.PhaseId == changedHot.PhaseId ? changedHot : phase).ToArray(),
        };
        CommercialCoreSliceDefinition changed = _coreSlice with
        {
            Chapters = [_coreSlice.Chapters[0], changedChapter],
        };
        CommercialCoreLoader.Validate(changed, _commercialWorld);
        return changed;
    }

    private void CoreAccepted(CommercialCoreCommandResult result, string label)
    {
        Check(result.Accepted, $"{label}: rejected with {result.Error}/{result.ConstructionError}");
        Check(result.Error is null && result.ConstructionError is null,
            $"{label}: accepted result contained an error");
    }

    private string ExecuteReplay(SpatialWorldDefinition world) =>
        JsonSerializer.Serialize(ExecuteReplaySnapshot(world));

    private ConstructionSnapshot ExecuteReplaySnapshot(SpatialWorldDefinition world)
    {
        var session = new ConstructionSession(world);
        Accepted(session.StartLineDraft("A", LineClassId, PoleClassId), "replay start");
        Accepted(session.AddLinePoint(new MapPoint(250, 200)), "replay add point");
        Accepted(session.FinishLineDraft("B"), "replay finish");
        ConstructionQuote quote = session.PreviewLineOrder();
        Quote(quote, 75, 13, 13, "replay quote");
        Accepted(session.OrderLine(), "replay order");
        Accepted(session.AdvanceToConstructionCompletion(), "replay completion");
        return session.GetSnapshot();
    }

    private void AssertRejectedPreserves(
        ConstructionSession session,
        Func<ConstructionCommandResult> command,
        ConstructionError expected,
        string label)
    {
        string before = JsonSerializer.Serialize(session.GetSnapshot());
        ConstructionCommandResult result = command();
        Check(!result.Accepted, $"{label}: command was accepted");
        Equal(expected, result.Error, $"{label}: typed error");
        Equal(before, JsonSerializer.Serialize(result.Snapshot),
            $"{label}: returned snapshot changed");
        Equal(before, JsonSerializer.Serialize(session.GetSnapshot()),
            $"{label}: session state changed");
    }

    private static SpatialWorldDefinition World(
        IReadOnlyList<SpatialNodeDefinition> nodes,
        IReadOnlyList<SpatialEdgeDefinition>? edges = null,
        IReadOnlyList<TerrainPolygonDefinition>? terrain = null,
        IReadOnlyList<SpatialRiskAreaDefinition>? risks = null) =>
        new(
            SpatialWorldLoader.SupportedSchemaVersion,
            "COMMERCIAL_CHECK_WORLD",
            "상용 검사 세계",
            100,
            new MapBounds(0, 0, 2000, 2000),
            10000,
            NodeClasses(),
            LineClasses(),
            terrain ?? Array.Empty<TerrainPolygonDefinition>(),
            risks ?? Array.Empty<SpatialRiskAreaDefinition>(),
            nodes,
            edges ?? Array.Empty<SpatialEdgeDefinition>());

    private static CommercialWorldDefinition ThermalWorld(
        IReadOnlyList<SpatialNodeDefinition> nodes,
        IReadOnlyList<SpatialEdgeDefinition> edges,
        long continuous,
        long emergency)
    {
        SpatialWorldDefinition spatial = World(nodes, edges);
        GenerationSourceDefinition[] sources = nodes
            .Where(item => item.ClassId == SourceClassId)
            .OrderBy(item => item.NodeId, StringComparer.Ordinal)
            .Select((item, index) => new GenerationSourceDefinition(item.NodeId, 1000, index))
            .ToArray();
        CommercialWorldDefinition world = new(
            CommercialWorldLoader.SupportedSchemaVersion,
            spatial.WorldId,
            spatial.DisplayName,
            spatial,
            [
                new ThermalNodeClassDefinition(PoleClassId, continuous, emergency),
                new ThermalNodeClassDefinition(SubstationClassId, continuous, emergency),
            ],
            [new ThermalLineClassDefinition(LineClassId, continuous, emergency)],
            sources);
        CommercialWorldLoader.Validate(world);
        return world;
    }

    private static CommercialWorldDefinition SharedThermalWorld() => ThermalWorld(
    [
        Node("S", 100, 100),
        Node("HUB", 300, 100, PoleClassId),
        Node("L1", 500, 50, LoadClassId),
        Node("L2", 500, 150, LoadClassId),
    ],
    [
        Edge("SOURCE_HUB", "S", "HUB"),
        Edge("HUB_L1", "HUB", "L1"),
        Edge("HUB_L2", "HUB", "L2"),
    ],
    continuous: 400,
    emergency: 500);

    private static CommercialWorldDefinition DiamondThermalWorld(int diamondCount)
    {
        var nodes = new List<SpatialNodeDefinition>
        {
            Node("S", 100, 1000),
            Node("L", 100 + (diamondCount * 100), 1000, LoadClassId),
        };
        var edges = new List<SpatialEdgeDefinition>();
        for (int index = 0; index < diamondCount; index++)
        {
            string left = index == 0 ? "S" : $"J{index:D2}";
            string right = index + 1 == diamondCount ? "L" : $"J{index + 1:D2}";
            string top = $"T{index:D2}";
            string bottom = $"B{index:D2}";
            int branchX = 150 + (index * 100);
            nodes.Add(Node(top, branchX, 900, PoleClassId));
            nodes.Add(Node(bottom, branchX, 1100, PoleClassId));
            if (index + 1 < diamondCount)
            {
                nodes.Add(Node(right, 200 + (index * 100), 1000, PoleClassId));
            }
            edges.Add(Edge($"D{index:D2}_A_TOP", left, top));
            edges.Add(Edge($"D{index:D2}_B_BOTTOM", left, bottom));
            edges.Add(Edge($"D{index:D2}_C_TOP", top, right));
            edges.Add(Edge($"D{index:D2}_D_BOTTOM", bottom, right));
        }
        return ThermalWorld(nodes, edges, continuous: 500, emergency: 700);
    }

    private static ThermalDemandDefinition Demand(
        string id,
        string nodeId,
        long demandKw,
        ThermalObligationKind obligation,
        bool included = true,
        bool emergencyApproved = false,
        bool namedEmergency = false) => new(
            id,
            id,
            nodeId,
            demandKw,
            obligation,
            included,
            emergencyApproved,
            namedEmergency);

    private static ThermalIntervalDefinition Interval(
        string id,
        ThermalDemandDefinition? first = null,
        ThermalDemandDefinition? second = null,
        ThermalIntervalPolicy policy = ThermalIntervalPolicy.ContinuousOnly,
        IReadOnlyList<string>? unavailable = null,
        IReadOnlyList<ThermalLimitOverride>? overrides = null) => new(
            id,
            id,
            policy,
            new[] { first, second }.Where(item => item is not null).Cast<ThermalDemandDefinition>().ToArray(),
            unavailable ?? Array.Empty<string>(),
            overrides ?? Array.Empty<ThermalLimitOverride>());

    private static ThermalIntervalResult EvaluateOne(
        CommercialWorldDefinition world,
        ThermalIntervalDefinition interval) => ThermalEvaluator.Evaluate(
            world,
            new ThermalSequenceRequest([interval], Array.Empty<ThermalAssetMemory>()))
        .Intervals[0];

    private static IReadOnlyList<SpatialNodeClassDefinition> NodeClasses() =>
    [
        new(SourceClassId, "검사 발전 접속점", SpatialNodeKind.SourceTerminal,
            10, 6, 0, 0),
        new(LoadClassId, "검사 부하 접속점", SpatialNodeKind.DedicatedLoadTerminal,
            10, 6, 0, 0),
        new(PoleClassId, "검사 전신주", SpatialNodeKind.Pole,
            10, 4, 50, 3),
        new(SubstationClassId, "검사 변전소", SpatialNodeKind.Substation,
            20, 4, 100, 10, 600),
    ];

    private static IReadOnlyList<SpatialLineClassDefinition> LineClasses() =>
    [
        new(LineClassId, "검사 선로", 600, 5, 2),
    ];

    private static SpatialNodeDefinition Node(
        string id,
        int x,
        int y,
        string classId = SourceClassId) =>
        new(id, classId, id, new MapPoint(x, y), true, false);

    private static SpatialEdgeDefinition Edge(string id, string from, string to) =>
        new(id, LineClassId, from, to, true);

    private static LineDraftSnapshot Draft(string start) =>
        new(start, LineClassId, PoleClassId, Array.Empty<MapPoint>(), null);

    private static TerrainPolygonDefinition Terrain(
        string id,
        TerrainKind kind,
        int minX,
        int minY,
        int maxX,
        int maxY) =>
        new(id, id, kind, Rectangle(minX, minY, maxX, maxY));

    private static SpatialRiskAreaDefinition Risk(
        string id,
        int minX,
        int minY,
        int maxX,
        int maxY) =>
        new(id, id, Rectangle(minX, minY, maxX, maxY));

    private static IReadOnlyList<MapPoint> Rectangle(
        int minX,
        int minY,
        int maxX,
        int maxY) =>
    [
        new(minX, minY),
        new(maxX, minY),
        new(maxX, maxY),
        new(minX, maxY),
    ];

    private static bool Reachable(SpatialWorldDefinition world, string start, string target)
    {
        var reached = new HashSet<string>(StringComparer.Ordinal) { start };
        var pending = new Queue<string>();
        pending.Enqueue(start);
        while (pending.Count != 0)
        {
            string current = pending.Dequeue();
            foreach (SpatialEdgeDefinition edge in world.Edges.Where(edge =>
                         edge.FromNodeId == current || edge.ToNodeId == current))
            {
                string next = edge.FromNodeId == current ? edge.ToNodeId : edge.FromNodeId;
                if (reached.Add(next))
                {
                    pending.Enqueue(next);
                }
            }
        }
        return reached.Contains(target);
    }

    private readonly record struct PngInfo(
        int Width,
        int Height,
        byte BitDepth,
        byte ColorType,
        double TransparentFraction,
        IReadOnlyList<byte> CornerAlphas);

    private static PngInfo ReadPng(string path, bool decodeAlpha)
    {
        using FileStream input = File.OpenRead(path);
        Span<byte> signature = stackalloc byte[8];
        input.ReadExactly(signature);
        ReadOnlySpan<byte> expectedSignature =
            [137, 80, 78, 71, 13, 10, 26, 10];
        if (!signature.SequenceEqual(expectedSignature))
        {
            throw new InvalidOperationException($"invalid PNG signature: {path}");
        }

        int width = 0;
        int height = 0;
        byte bitDepth = 0;
        byte colorType = 0;
        byte interlace = 0;
        using var compressed = new MemoryStream();
        byte[] lengthBytes = new byte[4];
        byte[] typeBytes = new byte[4];
        while (input.Position < input.Length)
        {
            input.ReadExactly(lengthBytes);
            int length = checked((int)BinaryPrimitives.ReadUInt32BigEndian(lengthBytes));
            input.ReadExactly(typeBytes);
            string type = Encoding.ASCII.GetString(typeBytes);
            byte[] data = new byte[length];
            input.ReadExactly(data);
            input.Position = checked(input.Position + 4);
            if (type == "IHDR")
            {
                if (data.Length != 13)
                {
                    throw new InvalidOperationException($"invalid PNG IHDR: {path}");
                }
                width = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(0, 4)));
                height = checked((int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(4, 4)));
                bitDepth = data[8];
                colorType = data[9];
                interlace = data[12];
            }
            else if (type == "IDAT" && decodeAlpha)
            {
                compressed.Write(data);
            }
            else if (type == "IEND")
            {
                break;
            }
        }

        if (width <= 0 || height <= 0 || interlace != 0)
        {
            throw new InvalidOperationException($"unsupported PNG layout: {path}");
        }
        if (!decodeAlpha)
        {
            return new PngInfo(width, height, bitDepth, colorType, 0d, Array.Empty<byte>());
        }
        if (bitDepth != 8 || colorType != 6)
        {
            return new PngInfo(width, height, bitDepth, colorType, 0d, Array.Empty<byte>());
        }

        compressed.Position = 0;
        using var decompressor = new ZLibStream(compressed, CompressionMode.Decompress);
        using var rawBuffer = new MemoryStream();
        decompressor.CopyTo(rawBuffer);
        byte[] raw = rawBuffer.ToArray();
        const int bytesPerPixel = 4;
        int stride = checked(width * bytesPerPixel);
        int expectedLength = checked(height * (stride + 1));
        if (raw.Length != expectedLength)
        {
            throw new InvalidOperationException($"unexpected PNG scanline length: {path}");
        }

        byte[] pixels = new byte[checked(height * stride)];
        int source = 0;
        for (int y = 0; y < height; y++)
        {
            byte filter = raw[source++];
            int rowStart = y * stride;
            int previousRowStart = rowStart - stride;
            for (int x = 0; x < stride; x++)
            {
                byte encoded = raw[source++];
                byte left = x >= bytesPerPixel ? pixels[rowStart + x - bytesPerPixel] : (byte)0;
                byte up = y > 0 ? pixels[previousRowStart + x] : (byte)0;
                byte upLeft = y > 0 && x >= bytesPerPixel
                    ? pixels[previousRowStart + x - bytesPerPixel]
                    : (byte)0;
                int predictor = filter switch
                {
                    0 => 0,
                    1 => left,
                    2 => up,
                    3 => (left + up) / 2,
                    4 => Paeth(left, up, upLeft),
                    _ => throw new InvalidOperationException($"unsupported PNG filter: {path}"),
                };
                pixels[rowStart + x] = unchecked((byte)(encoded + predictor));
            }
        }

        long transparent = 0;
        for (int offset = 3; offset < pixels.Length; offset += bytesPerPixel)
        {
            if (pixels[offset] == 0)
            {
                transparent++;
            }
        }
        byte[] corners =
        [
            pixels[3],
            pixels[((width - 1) * bytesPerPixel) + 3],
            pixels[((height - 1) * stride) + 3],
            pixels[((height * stride) - bytesPerPixel) + 3],
        ];
        return new PngInfo(
            width,
            height,
            bitDepth,
            colorType,
            (double)transparent / (width * (long)height),
            corners);
    }

    private static int Paeth(byte left, byte up, byte upLeft)
    {
        int prediction = left + up - upLeft;
        int leftDistance = Math.Abs(prediction - left);
        int upDistance = Math.Abs(prediction - up);
        int upperLeftDistance = Math.Abs(prediction - upLeft);
        return leftDistance <= upDistance && leftDistance <= upperLeftDistance
            ? left
            : upDistance <= upperLeftDistance
                ? up
                : upLeft;
    }

    private void ExpectLoaderRejected(string label, Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(_fixtureJson)!.AsObject();
        mutate(root);
        ExpectLoaderRejected(label, root.ToJsonString());
    }

    private void ExpectLoaderRejected(string label, string json) =>
        ExpectThrows<SpatialWorldValidationException>(
            () => SpatialWorldLoader.Load(json),
            label);

    private void ExpectLoaderRejected(string label, byte[] bytes) =>
        ExpectThrows<SpatialWorldValidationException>(
            () => SpatialWorldLoader.Load(bytes),
            label);

    private void ExpectCommercialRejected(string label, Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(_commercialJson)!.AsObject();
        mutate(root);
        ExpectCommercialRejected(label, root.ToJsonString());
    }

    private void ExpectCommercialRejected(string label, string json) =>
        ExpectThrows<CommercialWorldValidationException>(
            () => CommercialWorldLoader.Load(json),
            label);

    private void ExpectCoreRejected(string label, Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(_coreJson)!.AsObject();
        mutate(root);
        ExpectCoreRejected(label, root.ToJsonString());
    }

    private void ExpectCoreRejected(string label, string json) =>
        ExpectThrows<CommercialCoreValidationException>(
            () => CommercialCoreLoader.Load(json, _commercialWorld),
            label);

    private void ExpectCampaignRejected(string label, Action<JsonObject> mutate)
    {
        JsonObject root = JsonNode.Parse(_campaignJson)!.AsObject();
        mutate(root);
        ExpectCampaignRejected(label, root.ToJsonString());
    }

    private void ExpectCampaignRejected(string label, string json) =>
        ExpectThrows<CommercialCampaignValidationException>(
            () => CommercialCampaignLoader.Load(json, _commercialWorld),
            label);

    private void ExpectStoryPartRejected(
        string selector,
        CommercialStoryPartErrorCode expectedError,
        string label) =>
        _ = CaptureStoryPartFailure(selector, expectedError, label);

    private CommercialStoryPartSelectionException CaptureStoryPartFailure(
        string selector,
        CommercialStoryPartErrorCode expectedError,
        string label)
    {
        try
        {
            _storyParts.Select(selector);
        }
        catch (CommercialStoryPartSelectionException exception)
        {
            Equal(expectedError, exception.ErrorCode, $"{label}: typed error");
            Equal(selector, exception.Selector, $"{label}: preserved selector");
            return exception;
        }
        throw new InvalidOperationException(
            $"{label}: expected {nameof(CommercialStoryPartSelectionException)}");
    }

    private void ExpectThrows<T>(Action body, string label)
        where T : Exception
    {
        try
        {
            body();
        }
        catch (T)
        {
            _assertionCount++;
            return;
        }
        throw new InvalidOperationException($"{label}: expected {typeof(T).Name}");
    }

    private static JsonObject Object(JsonNode node) => node.AsObject();

    private static JsonArray JsonArrayProperty(JsonObject parent, string property) =>
        parent[property]!.AsArray();

    private static JsonObject PointJson(int x, int y) =>
        new() { ["xUnit"] = x, ["yUnit"] = y };

    private static JsonObject NodeJson(
        string id,
        string classId,
        int x,
        int y,
        bool authoredFoundation) =>
        new()
        {
            ["nodeId"] = id,
            ["classId"] = classId,
            ["displayName"] = id,
            ["position"] = PointJson(x, y),
            ["commissioned"] = true,
            ["authoredFoundation"] = authoredFoundation,
        };

    private static JsonObject EdgeJson(
        string id,
        string classId,
        string from,
        string to) =>
        new()
        {
            ["edgeId"] = id,
            ["lineClassId"] = classId,
            ["fromNodeId"] = from,
            ["toNodeId"] = to,
            ["commissioned"] = true,
        };

    private void Error(ConstructionError expected, NodePlacementPreview actual, string label)
    {
        Check(!actual.Accepted, $"{label}: preview was accepted");
        Equal(expected, actual.Error, $"{label}: error");
    }

    private void Error(ConstructionError expected, LinePointPreview actual, string label)
    {
        Check(!actual.Accepted, $"{label}: preview was accepted");
        Equal(expected, actual.Error, $"{label}: error");
    }

    private void Error(ConstructionError expected, LineFinishPreview actual, string label)
    {
        Check(!actual.Accepted, $"{label}: preview was accepted");
        Equal(expected, actual.Error, $"{label}: error");
    }

    private void Error(ConstructionError expected, ConstructionQuote actual, string label)
    {
        Check(!actual.Accepted, $"{label}: quote was accepted");
        Equal(expected, actual.Error, $"{label}: error");
    }

    private void Accepted(ConstructionCommandResult result, string label)
    {
        Check(result.Accepted, $"{label}: rejected with {result.Error}");
        Check(result.Error is null, $"{label}: accepted result has an error");
    }

    private void Quote(
        ConstructionQuote quote,
        long cost,
        long minutes,
        long completion,
        string label)
    {
        Check(quote.Accepted, $"{label}: rejected with {quote.Error}");
        Equal(cost, quote.CostCashUnit, $"{label}: cost");
        Equal(minutes, quote.BuildMinutes, $"{label}: build minutes");
        Equal(completion, quote.CompletionMinute, $"{label}: completion minute");
    }

    private void SequenceEqual<T>(
        IReadOnlyList<T> expected,
        IReadOnlyList<T> actual,
        string label)
    {
        Check(expected.SequenceEqual(actual),
            $"{label}: expected [{string.Join(", ", expected)}], " +
            $"actual [{string.Join(", ", actual)}]");
    }

    private void Equal<T>(T expected, T actual, string label)
    {
        Check(EqualityComparer<T>.Default.Equals(expected, actual),
            $"{label}: expected {expected}, actual {actual}");
    }

    private void Check(bool condition, string message)
    {
        _assertionCount++;
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
