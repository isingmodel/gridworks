using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

const string WorldResource = "Gridworks.Game.EmbeddedData.release-world-v2.json";
const string CampaignResource = "Gridworks.Game.EmbeddedData.release-campaign-v2.json";
const string BuildIdentityResource =
    "Gridworks.Game.EmbeddedData.commercial-build-identity-v1.json";
const string BuildIdentitySchema = "gridworks.commercial-build-identity.v1";

try
{
    if (args.Length == 1 && args[0] == "selftest")
    {
        RunNegativeProbes();
        Console.WriteLine("PACKAGE_AUDIT_NEGATIVE_PROBES_PASS");
        return 0;
    }

    if (args.Length == 2 && args[0] == "pck")
    {
        string pckPath = RequireFile(args[1], "Godot PCK");
        PckAuditResult result = ValidateCommercialPck(pckPath);
        Console.WriteLine(
            $"PACKAGE_AUDIT_PCK_PASS format={result.FormatVersion} " +
            $"godot={result.GodotVersion} entries={result.EntryCount} " +
            $"sha256={result.Sha256}");
        return 0;
    }

    if (args.Length == 2 && args[0] == "core")
    {
        string corePath = RequireFile(args[1], "core assembly");
        ValidateNoEmbeddedResources(corePath);
        Console.WriteLine(
            $"PACKAGE_AUDIT_CORE_PASS resources=0 local_paths=0 forbidden_markers=0 " +
            $"sha256={Sha256File(corePath)}");
        return 0;
    }

    bool assemblyMode = args.Length == 7 && args[0] == "assembly";
    bool appMode = args.Length == 6 && args[0] == "app";
    if (!assemblyMode && !appMode)
    {
        throw new ArgumentException(
            "usage: Gridworks.PackageAudit assembly <Gridworks.Game.dll> " +
            "<release-world-v2.json> <release-campaign-v2.json> " +
            "<source-commit> <product-version> <i386|x86_64|arm64> | " +
            "Gridworks.PackageAudit app <Gridworks.app> " +
            "<release-world-v2.json> <release-campaign-v2.json> " +
            "<source-commit> <product-version> | " +
            "Gridworks.PackageAudit pck <Gridworks.pck> | " +
            "Gridworks.PackageAudit core <Gridworks.Core.dll> | " +
            "Gridworks.PackageAudit selftest");
    }

    string targetPath = Path.GetFullPath(args[1]);
    string worldPath = RequireFile(args[2], "world fixture");
    string campaignPath = RequireFile(args[3], "campaign fixture");
    string sourceCommit = RequireLowerHex(args[4], 40, "source commit");
    string productVersion = RequireProductVersion(args[5]);
    string worldSha256 = Sha256File(worldPath);
    string campaignSha256 = Sha256File(campaignPath);
    var identity = new BuildIdentityExpectation(
        sourceCommit,
        productVersion,
        worldSha256,
        campaignSha256);

    if (assemblyMode)
    {
        Machine expectedMachine = ParseMachine(args[6]);
        AssemblyAuditResult result = ValidateManagedAssembly(
            RequireFile(targetPath, "managed assembly"),
            identity,
            expectedMachine);
        Console.WriteLine(
            $"assembly={Path.GetFileName(targetPath)} " +
            $"machine={MachineLabel(result.Machine)} " +
            $"sha256={result.Sha256} resources=3");
    }
    else
    {
        ValidateApp(targetPath, identity);
    }

    Console.WriteLine(
        $"PACKAGE_AUDIT_PASS source_commit={sourceCommit} " +
        $"version={productVersion} world={worldSha256} campaign={campaignSha256}");
    return 0;
}
catch (Exception exception)
{
    Console.Error.WriteLine($"PACKAGE_AUDIT_FAIL {exception.Message}");
    return 1;
}

static void ValidateApp(string appPath, BuildIdentityExpectation identity)
{
    if (!Directory.Exists(appPath) ||
        !appPath.EndsWith(".app", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidDataException($"app bundle not found: {appPath}");
    }

    string resourcesPath = Path.Combine(appPath, "Contents", "Resources");
    if (!Directory.Exists(resourcesPath))
    {
        throw new InvalidDataException($"app resources not found: {resourcesPath}");
    }

    string[] pdbFiles = Directory.GetFiles(
            appPath,
            "*",
            SearchOption.AllDirectories)
        .Where(path => string.Equals(
            Path.GetExtension(path),
            ".pdb",
            StringComparison.OrdinalIgnoreCase))
        .ToArray();
    if (pdbFiles.Length != 0)
    {
        throw new InvalidDataException(
            $"debug symbol is packaged: {Path.GetRelativePath(appPath, pdbFiles[0])}");
    }

    var ridPayloads = new[]
    {
        new RidPayload(
            "arm64",
            Machine.Arm64,
            Path.Combine(resourcesPath, "data_Gridworks.Game_macos_arm64")),
        new RidPayload(
            "x86_64",
            Machine.Amd64,
            Path.Combine(resourcesPath, "data_Gridworks.Game_macos_x86_64")),
    };
    string[] expectedGameAssemblies = ridPayloads
        .Select(payload => Path.Combine(payload.Directory, "Gridworks.Game.dll"))
        .Order(StringComparer.Ordinal)
        .ToArray();
    string[] expectedCoreAssemblies = ridPayloads
        .Select(payload => Path.Combine(payload.Directory, "Gridworks.Core.dll"))
        .Order(StringComparer.Ordinal)
        .ToArray();
    RequireExactPaths(
        Directory.GetFiles(resourcesPath, "Gridworks.Game.dll", SearchOption.AllDirectories),
        expectedGameAssemblies,
        "RID-specific Game assemblies");
    RequireExactPaths(
        Directory.GetFiles(resourcesPath, "Gridworks.Core.dll", SearchOption.AllDirectories),
        expectedCoreAssemblies,
        "RID-specific Core assemblies");

    foreach (RidPayload payload in ridPayloads)
    {
        string gameAssembly = Path.Combine(payload.Directory, "Gridworks.Game.dll");
        string coreAssembly = Path.Combine(payload.Directory, "Gridworks.Core.dll");
        AssemblyAuditResult game = ValidateManagedAssembly(
            gameAssembly,
            identity,
            payload.GameMachine);
        ValidateNoEmbeddedResources(coreAssembly);
        Console.WriteLine(
            $"rid={payload.Name} game_machine={MachineLabel(game.Machine)} " +
            $"game_sha256={game.Sha256} core_sha256={Sha256File(coreAssembly)}");
    }

    string[] pckFiles = Directory.GetFiles(
        appPath,
        "*.pck",
        SearchOption.AllDirectories);
    string expectedPck = Path.Combine(resourcesPath, "Gridworks.pck");
    RequireExactPaths(pckFiles, [expectedPck], "standalone Godot PCK");
    PckAuditResult pck = ValidateCommercialPck(expectedPck);
    Console.WriteLine(
        $"pck=Gridworks.pck format={pck.FormatVersion} " +
        $"godot={pck.GodotVersion} entries={pck.EntryCount} sha256={pck.Sha256}");

    string[] forbiddenLooseFiles = Directory.GetFiles(
            resourcesPath,
            "*",
            SearchOption.AllDirectories)
        .Where(path => IsForbiddenLooseFile(Path.GetFileName(path)))
        .Order(StringComparer.Ordinal)
        .ToArray();
    if (forbiddenLooseFiles.Length != 0)
    {
        throw new InvalidDataException(
            "prototype or v1 file is packaged: " +
            Path.GetRelativePath(appPath, forbiddenLooseFiles[0]));
    }
}

static void RequireExactPaths(
    IEnumerable<string> actualPaths,
    IEnumerable<string> expectedPaths,
    string label)
{
    string[] actual = actualPaths
        .Select(Path.GetFullPath)
        .Order(StringComparer.Ordinal)
        .ToArray();
    string[] expected = expectedPaths
        .Select(Path.GetFullPath)
        .Order(StringComparer.Ordinal)
        .ToArray();
    if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
    {
        throw new InvalidDataException(
            $"unexpected {label}: actual=[{string.Join(",", actual)}] " +
            $"expected=[{string.Join(",", expected)}]");
    }
    foreach (string path in expected)
    {
        RequireFile(path, label);
    }
}

static AssemblyAuditResult ValidateManagedAssembly(
    string assemblyPath,
    BuildIdentityExpectation identity,
    Machine expectedMachine)
{
    byte[] assemblyBytes = File.ReadAllBytes(assemblyPath);
    using var stream = new MemoryStream(assemblyBytes, writable: false);
    using var peReader = new PEReader(stream, PEStreamOptions.LeaveOpen);
    Machine actualMachine = peReader.PEHeaders.CoffHeader.Machine;
    if (actualMachine != expectedMachine)
    {
        throw new InvalidDataException(
            $"PE Machine mismatch in {assemblyPath}: " +
            $"actual={MachineLabel(actualMachine)} expected={MachineLabel(expectedMachine)}");
    }
    if (!peReader.HasMetadata)
    {
        throw new InvalidDataException($"managed metadata not found: {assemblyPath}");
    }

    MetadataReader metadata = peReader.GetMetadataReader();
    var resources = new Dictionary<string, byte[]>(StringComparer.Ordinal);
    foreach (ManifestResourceHandle handle in metadata.ManifestResources)
    {
        ManifestResource manifestResource = metadata.GetManifestResource(handle);
        string name = metadata.GetString(manifestResource.Name);
        if (!manifestResource.Implementation.IsNil)
        {
            throw new InvalidDataException(
                $"linked manifest resource is not allowed in {assemblyPath}: {name}");
        }
        if (!resources.TryAdd(name, ReadEmbeddedResource(peReader, manifestResource, name)))
        {
            throw new InvalidDataException(
                $"duplicate embedded resource in {assemblyPath}: {name}");
        }
    }

    string[] expectedResources =
        [BuildIdentityResource, CampaignResource, WorldResource];
    string[] actualResources = resources.Keys.Order(StringComparer.Ordinal).ToArray();
    if (!actualResources.SequenceEqual(expectedResources, StringComparer.Ordinal))
    {
        throw new InvalidDataException(
            $"unexpected embedded resources in {assemblyPath}: " +
            (actualResources.Length == 0 ? "none" : string.Join(",", actualResources)));
    }

    string worldSha256 = Sha256(resources[WorldResource]);
    string campaignSha256 = Sha256(resources[CampaignResource]);
    if (!string.Equals(worldSha256, identity.WorldSha256, StringComparison.Ordinal) ||
        !string.Equals(campaignSha256, identity.CampaignSha256, StringComparison.Ordinal))
    {
        throw new InvalidDataException(
            $"embedded data hash mismatch in {assemblyPath}: " +
            $"world={worldSha256} campaign={campaignSha256}");
    }
    ValidateBuildIdentity(resources[BuildIdentityResource], identity, assemblyPath);
    RequireNoLocalPath(assemblyPath);
    RequireNoForbiddenGameMarkers(assemblyPath);

    return new AssemblyAuditResult(actualMachine, Sha256(assemblyBytes));
}

static byte[] ReadEmbeddedResource(
    PEReader peReader,
    ManifestResource resource,
    string resourceName)
{
    CorHeader corHeader = peReader.PEHeaders.CorHeader
        ?? throw new InvalidDataException("managed PE has no CLR header");
    PEMemoryBlock resourceBlock = peReader.GetSectionData(
        corHeader.ResourcesDirectory.RelativeVirtualAddress);
    int offset = checked((int)resource.Offset);
    if (offset < 0 || offset > resourceBlock.Length - sizeof(int))
    {
        throw new InvalidDataException(
            $"embedded resource offset is invalid: {resourceName}");
    }
    BlobReader reader = resourceBlock.GetReader(offset, resourceBlock.Length - offset);
    int length = reader.ReadInt32();
    if (length < 0 || length > reader.RemainingBytes)
    {
        throw new InvalidDataException(
            $"embedded resource length is invalid: {resourceName}");
    }
    return reader.ReadBytes(length);
}

static void ValidateNoEmbeddedResources(string assemblyPath)
{
    using FileStream stream = File.OpenRead(assemblyPath);
    using var peReader = new PEReader(stream, PEStreamOptions.LeaveOpen);
    if (!peReader.HasMetadata)
    {
        throw new InvalidDataException($"managed metadata not found: {assemblyPath}");
    }
    MetadataReader metadata = peReader.GetMetadataReader();
    string[] resources = metadata.ManifestResources
        .Select(handle => metadata.GetString(metadata.GetManifestResource(handle).Name))
        .Order(StringComparer.Ordinal)
        .ToArray();
    if (resources.Length != 0)
    {
        throw new InvalidDataException(
            $"core assembly unexpectedly embeds resources in {assemblyPath}: " +
            string.Join(",", resources));
    }
    RequireNoLocalPath(assemblyPath);
    RequireNoForbiddenGameMarkers(assemblyPath);
}

static void ValidateBuildIdentity(
    byte[] bytes,
    BuildIdentityExpectation expected,
    string assemblyPath)
{
    if (bytes.AsSpan().StartsWith(new byte[] { 0xef, 0xbb, 0xbf }))
    {
        throw new InvalidDataException(
            $"build identity must be UTF-8 without BOM in {assemblyPath}");
    }
    using JsonDocument document = JsonDocument.Parse(
        bytes,
        new JsonDocumentOptions
        {
            AllowTrailingCommas = false,
            CommentHandling = JsonCommentHandling.Disallow,
            MaxDepth = 4,
        });
    JsonElement root = document.RootElement;
    if (root.ValueKind != JsonValueKind.Object)
    {
        throw new InvalidDataException(
            $"build identity root must be an object in {assemblyPath}");
    }

    string[] expectedNames =
    [
        "schemaVersion",
        "sourceCommit",
        "productVersion",
        "configuration",
        "worldSha256",
        "campaignSha256",
    ];
    JsonProperty[] properties = root.EnumerateObject().ToArray();
    string[] actualNames = properties.Select(property => property.Name).ToArray();
    if (properties.Length != expectedNames.Length ||
        !actualNames.SequenceEqual(expectedNames, StringComparer.Ordinal) ||
        actualNames.Distinct(StringComparer.Ordinal).Count() != expectedNames.Length)
    {
        throw new InvalidDataException(
            $"build identity has unknown, missing, reordered, or duplicate properties " +
            $"in {assemblyPath}: [{string.Join(",", actualNames)}]");
    }

    string schemaVersion = JsonString(properties[0], assemblyPath);
    string sourceCommit = JsonString(properties[1], assemblyPath);
    string productVersion = JsonString(properties[2], assemblyPath);
    string configuration = JsonString(properties[3], assemblyPath);
    string worldSha256 = JsonString(properties[4], assemblyPath);
    string campaignSha256 = JsonString(properties[5], assemblyPath);
    if (schemaVersion != BuildIdentitySchema ||
        sourceCommit != expected.SourceCommit ||
        productVersion != expected.ProductVersion ||
        configuration != "ExportRelease" ||
        worldSha256 != expected.WorldSha256 ||
        campaignSha256 != expected.CampaignSha256)
    {
        throw new InvalidDataException(
            $"build identity value mismatch in {assemblyPath}: " +
            $"schemaVersion={schemaVersion} sourceCommit={sourceCommit} " +
            $"productVersion={productVersion} configuration={configuration} " +
            $"worldSha256={worldSha256} campaignSha256={campaignSha256}");
    }
}

static string JsonString(JsonProperty property, string assemblyPath)
{
    if (property.Value.ValueKind != JsonValueKind.String)
    {
        throw new InvalidDataException(
            $"build identity property must be a string in {assemblyPath}: {property.Name}");
    }
    return property.Value.GetString()
        ?? throw new InvalidDataException(
            $"build identity property is null in {assemblyPath}: {property.Name}");
}

static PckAuditResult ValidateCommercialPck(string pckPath)
{
    const uint PckMagic = 0x43504447;
    const uint PckFormatV4 = 4;
    const uint RelativeFileBaseFlag = 1u << 1;
    var strictUtf8 = new UTF8Encoding(
        encoderShouldEmitUTF8Identifier: false,
        throwOnInvalidBytes: true);

    using FileStream stream = File.OpenRead(pckPath);
    using var reader = new BinaryReader(stream, strictUtf8, leaveOpen: true);
    if (stream.Length < 108)
    {
        throw new InvalidDataException("Gridworks.pck is shorter than a PCK v4 header.");
    }
    uint magic = reader.ReadUInt32();
    uint formatVersion = reader.ReadUInt32();
    uint godotMajor = reader.ReadUInt32();
    uint godotMinor = reader.ReadUInt32();
    uint godotPatch = reader.ReadUInt32();
    uint flags = reader.ReadUInt32();
    ulong fileBase = reader.ReadUInt64();
    ulong directoryOffset = reader.ReadUInt64();
    if (magic != PckMagic || formatVersion != PckFormatV4)
    {
        throw new InvalidDataException(
            $"Gridworks.pck must be standalone Godot PCK v4: " +
            $"magic=0x{magic:x8} format={formatVersion}");
    }
    if (godotMajor != 4 || godotMinor != 7 || godotPatch != 1)
    {
        throw new InvalidDataException(
            $"Gridworks.pck was not authored by Godot 4.7.1: " +
            $"{godotMajor}.{godotMinor}.{godotPatch}");
    }
    if (flags != RelativeFileBaseFlag)
    {
        throw new InvalidDataException(
            $"Gridworks.pck must use only the relative-file-base flag: 0x{flags:x8}");
    }
    for (int index = 0; index < 16; index++)
    {
        if (reader.ReadUInt32() != 0)
        {
            throw new InvalidDataException(
                $"Gridworks.pck reserved header word {index} is nonzero.");
        }
    }
    ulong length = checked((ulong)stream.Length);
    if (fileBase < checked((ulong)stream.Position) ||
        fileBase > directoryOffset ||
        directoryOffset > length - sizeof(uint))
    {
        throw new InvalidDataException(
            $"Gridworks.pck base or directory offset is invalid: " +
            $"fileBase={fileBase} directoryOffset={directoryOffset} length={length}");
    }

    stream.Position = checked((long)directoryOffset);
    uint fileCount = reader.ReadUInt32();
    if (fileCount == 0 || fileCount > 100_000)
    {
        throw new InvalidDataException(
            $"Gridworks.pck file count is invalid: {fileCount}");
    }
    var entries = new Dictionary<string, PckEntry>(StringComparer.Ordinal);
    for (uint index = 0; index < fileCount; index++)
    {
        uint storedPathLength = reader.ReadUInt32();
        if (storedPathLength == 0 ||
            storedPathLength > 1_048_576 ||
            storedPathLength % 4 != 0)
        {
            throw new InvalidDataException(
                $"Gridworks.pck entry {index} has invalid path storage length " +
                $"{storedPathLength}.");
        }
        byte[] storedPath = ReadExact(
            reader,
            checked((int)storedPathLength),
            $"PCK path {index}");
        int nulIndex = Array.IndexOf(storedPath, (byte)0);
        int pathLength = nulIndex < 0 ? storedPath.Length : nulIndex;
        int expectedStoredPathLength = checked((pathLength + 3) / 4 * 4);
        if (expectedStoredPathLength != storedPath.Length)
        {
            throw new InvalidDataException(
                $"Gridworks.pck entry {index} has noncanonical path padding length.");
        }
        if (nulIndex >= 0 && storedPath.AsSpan(nulIndex).IndexOfAnyExcept((byte)0) >= 0)
        {
            throw new InvalidDataException(
                $"Gridworks.pck entry {index} has nonzero path padding.");
        }
        string path = strictUtf8.GetString(storedPath, 0, pathLength);
        ValidatePckPath(path, index);

        ulong relativeOffset = reader.ReadUInt64();
        ulong size = reader.ReadUInt64();
        byte[] md5 = ReadExact(reader, 16, $"PCK MD5 {path}");
        uint entryFlags = reader.ReadUInt32();
        if (entryFlags != 0)
        {
            throw new InvalidDataException(
                $"Gridworks.pck entry must be plain and present: " +
                $"path={path} flags=0x{entryFlags:x8}");
        }
        ulong absoluteOffset = checked(fileBase + relativeOffset);
        ulong endOffset = checked(absoluteOffset + size);
        if (absoluteOffset < fileBase || endOffset > directoryOffset)
        {
            throw new InvalidDataException(
                $"Gridworks.pck entry range is outside the payload: " +
                $"path={path} offset={absoluteOffset} size={size}");
        }
        if (!entries.TryAdd(path, new PckEntry(path, absoluteOffset, size, md5)))
        {
            throw new InvalidDataException(
                $"Gridworks.pck contains a duplicate path: {path}");
        }
    }
    if (checked((ulong)stream.Position) != length)
    {
        throw new InvalidDataException(
            $"Gridworks.pck directory does not end at EOF: " +
            $"position={stream.Position} length={length}");
    }

    PckEntry[] orderedByOffset = entries.Values
        .OrderBy(entry => entry.Offset)
        .ThenBy(entry => entry.Path, StringComparer.Ordinal)
        .ToArray();
    ulong previousEnd = fileBase;
    foreach (PckEntry entry in orderedByOffset)
    {
        if (entry.Offset < previousEnd)
        {
            throw new InvalidDataException(
                $"Gridworks.pck payload entries overlap at {entry.Path}.");
        }
        previousEnd = checked(entry.Offset + entry.Size);
        byte[] actualMd5 = HashEntry(stream, entry, HashAlgorithmName.MD5);
        if (!actualMd5.AsSpan().SequenceEqual(entry.Md5))
        {
            throw new InvalidDataException(
                $"Gridworks.pck entry MD5 mismatch: {entry.Path}");
        }
    }

    RequireCommercialRemap(stream, entries, "CommercialMain.tscn.remap",
        ".godot/exported/", "-CommercialMain.scn");
    RequireCommercialRemap(stream, entries, "CommercialTheme.tres.remap",
        ".godot/exported/", "-CommercialTheme.res");
    RequireCommercialRemap(stream, entries, "default_bus_layout.tres.remap",
        ".godot/exported/", "-default_bus_layout.res");
    foreach (string portrait in new[]
             {
                 "kang_minho.png",
                 "lee_doyoon.png",
                 "park_jihyeon.png",
                 "yoon_seojin.png",
             })
    {
        RequireCommercialImport(
            stream,
            entries,
            $"assets/commercial/portraits/{portrait}",
            $".godot/imported/{portrait}-",
            ".ctex");
    }

    EncodedMarkerMatch? forbiddenMarker = FirstEncodedMarker(
        pckPath,
        [
            "res://ReleaseMain.tscn",
            "res://ReleaseAudio.tscn",
            "res://ReleaseMapView.tscn",
            "res://ReleaseShellOverlay.tscn",
            "res://ReleaseTaskPanel.tscn",
            "res://ReleaseTheme.tres",
            "res://ProductMain.tscn",
            "res://ProductAudio.tscn",
            "res://ProductShellOverlay.tscn",
            "res://FirstLightMapView.tscn",
            "res://FirstLightTaskPanel.tscn",
            "res://Main.tscn",
            "res://Scope1Main.tscn",
            "res://Scope1PlacementMapView.tscn",
            "release-world-v1.json",
            "release-campaign-v1.json",
            "product-campaign-v1.json",
            "product-first-light-v1.json",
            "product-second-heart-v1.json",
            "product-factory-v1.json",
            "product-heatwave-v1.json",
            "commercial-core-slice-v1.json",
            "commercial-free-placement-slice-v1.json",
            "scope-0b-v1.json",
            "scope-1-v1.json",
        ]);
    if (forbiddenMarker is not null)
    {
        throw new InvalidDataException(
            $"prototype or v1 marker is packaged in Gridworks.pck: " +
            $"{forbiddenMarker.Marker} ({forbiddenMarker.EncodingName})");
    }
    RequireNoPckDebugMarkers(pckPath);
    RequireNoLocalPath(pckPath);

    return new PckAuditResult(
        formatVersion,
        $"{godotMajor}.{godotMinor}.{godotPatch}",
        checked((int)fileCount),
        Sha256File(pckPath));
}

static void ValidatePckPath(string path, uint index)
{
    if (string.IsNullOrWhiteSpace(path) ||
        path.StartsWith("/", StringComparison.Ordinal) ||
        path.StartsWith("res://", StringComparison.Ordinal) ||
        path.Contains('\\') ||
        path.Split('/').Any(segment =>
            segment.Length == 0 || segment is "." or ".."))
    {
        throw new InvalidDataException(
            $"Gridworks.pck entry {index} has a noncanonical path: {path}");
    }
    string fileName = Path.GetFileName(path);
    if (Path.GetExtension(fileName).Equals(".pdb", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidDataException(
            $"debug symbol path is packaged in Gridworks.pck: {path}");
    }
    if (path.Contains("CommercialMain.Smoke.cs", StringComparison.OrdinalIgnoreCase))
    {
        throw new InvalidDataException(
            $"smoke source path is packaged in Gridworks.pck: {path}");
    }
    if (fileName.Equals("Main.tscn.remap", StringComparison.Ordinal) ||
        path.Contains("ReleaseMain", StringComparison.Ordinal) ||
        path.Contains("ReleaseAudio", StringComparison.Ordinal) ||
        path.Contains("ReleaseMapView", StringComparison.Ordinal) ||
        path.Contains("ReleaseShellOverlay", StringComparison.Ordinal) ||
        path.Contains("ReleaseTaskPanel", StringComparison.Ordinal) ||
        path.Contains("ReleaseTheme", StringComparison.Ordinal) ||
        path.Contains("ProductMain", StringComparison.Ordinal) ||
        path.Contains("ProductAudio", StringComparison.Ordinal) ||
        path.Contains("ProductShellOverlay", StringComparison.Ordinal) ||
        path.Contains("FirstLightMapView", StringComparison.Ordinal) ||
        path.Contains("FirstLightTaskPanel", StringComparison.Ordinal) ||
        path.Contains("Scope1Main", StringComparison.Ordinal) ||
        path.Contains("Scope1Placement", StringComparison.Ordinal) ||
        IsForbiddenLooseFile(fileName))
    {
        throw new InvalidDataException(
            $"prototype or v1 path is packaged in Gridworks.pck: {path}");
    }
}

static void RequireCommercialRemap(
    FileStream stream,
    IReadOnlyDictionary<string, PckEntry> entries,
    string remapPath,
    string targetPrefix,
    string targetSuffix)
{
    if (!entries.TryGetValue(remapPath, out PckEntry? remap))
    {
        throw new InvalidDataException(
            $"required PCK remap is missing: {remapPath}");
    }
    byte[] bytes = ReadEntry(stream, remap, maximumLength: 32_768);
    string text = new UTF8Encoding(false, true).GetString(bytes);
    string[] targetLines = text
        .Replace("\r\n", "\n", StringComparison.Ordinal)
        .Split('\n')
        .Where(line => line.StartsWith("path=\"", StringComparison.Ordinal))
        .ToArray();
    if (targetLines.Length != 1 || !targetLines[0].EndsWith('"'))
    {
        throw new InvalidDataException(
            $"required PCK remap has no single target: {remapPath}");
    }
    string target = targetLines[0][6..^1];
    if (!target.StartsWith("res://", StringComparison.Ordinal))
    {
        throw new InvalidDataException(
            $"required PCK remap target is not a resource path: {remapPath}");
    }
    string packedTarget = target[6..];
    if (!packedTarget.StartsWith(targetPrefix, StringComparison.Ordinal) ||
        !packedTarget.EndsWith(targetSuffix, StringComparison.Ordinal) ||
        !entries.ContainsKey(packedTarget))
    {
        throw new InvalidDataException(
            $"required PCK remap target is missing or unexpected: " +
            $"{remapPath} -> {target}");
    }
}

static void RequireCommercialImport(
    FileStream stream,
    IReadOnlyDictionary<string, PckEntry> entries,
    string sourcePath,
    string targetPrefix,
    string targetSuffix)
{
    string importPath = sourcePath + ".import";
    if (!entries.TryGetValue(importPath, out PckEntry? import))
    {
        throw new InvalidDataException(
            $"required PCK import metadata is missing: {importPath}");
    }
    byte[] bytes = ReadEntry(stream, import, maximumLength: 32_768);
    string text = new UTF8Encoding(false, true).GetString(bytes);
    string[] lines = text
        .Replace("\r\n", "\n", StringComparison.Ordinal)
        .Split('\n');
    if (!lines.Contains("importer=\"texture\"", StringComparer.Ordinal) ||
        !lines.Contains("type=\"CompressedTexture2D\"", StringComparer.Ordinal))
    {
        throw new InvalidDataException(
            $"required PCK texture import shape is invalid: {importPath}");
    }
    string[] targetLines = lines
        .Where(line => line.StartsWith("path=\"", StringComparison.Ordinal))
        .ToArray();
    if (targetLines.Length != 1 || !targetLines[0].EndsWith('"'))
    {
        throw new InvalidDataException(
            $"required PCK import has no single target: {importPath}");
    }
    string target = targetLines[0][6..^1];
    string expectedSource = $"source_file=\"res://{sourcePath}\"";
    if (!lines.Contains(expectedSource, StringComparer.Ordinal))
    {
        throw new InvalidDataException(
            $"required PCK import source is incorrect: {importPath}");
    }
    string expectedDestination = $"dest_files=[\"{target}\"]";
    if (!lines.Contains(expectedDestination, StringComparer.Ordinal))
    {
        throw new InvalidDataException(
            $"required PCK import destination is incorrect: {importPath}");
    }
    if (!target.StartsWith("res://", StringComparison.Ordinal))
    {
        throw new InvalidDataException(
            $"required PCK import target is not a resource path: {importPath}");
    }
    string packedTarget = target[6..];
    if (!packedTarget.StartsWith(targetPrefix, StringComparison.Ordinal) ||
        !packedTarget.EndsWith(targetSuffix, StringComparison.Ordinal) ||
        !entries.ContainsKey(packedTarget))
    {
        throw new InvalidDataException(
            $"required PCK import target is missing or unexpected: " +
            $"{importPath} -> {target}");
    }
}

static byte[] ReadEntry(FileStream stream, PckEntry entry, int maximumLength)
{
    if (entry.Size > checked((ulong)maximumLength))
    {
        throw new InvalidDataException(
            $"PCK entry is unexpectedly large: {entry.Path} size={entry.Size}");
    }
    stream.Position = checked((long)entry.Offset);
    byte[] bytes = new byte[checked((int)entry.Size)];
    stream.ReadExactly(bytes);
    return bytes;
}

static byte[] HashEntry(
    FileStream stream,
    PckEntry entry,
    HashAlgorithmName algorithm)
{
    using IncrementalHash hash = IncrementalHash.CreateHash(algorithm);
    stream.Position = checked((long)entry.Offset);
    ulong remaining = entry.Size;
    byte[] buffer = new byte[64 * 1024];
    while (remaining != 0)
    {
        int requested = checked((int)Math.Min((ulong)buffer.Length, remaining));
        int read = stream.Read(buffer, 0, requested);
        if (read == 0)
        {
            throw new EndOfStreamException(
                $"PCK entry ended early while hashing: {entry.Path}");
        }
        hash.AppendData(buffer, 0, read);
        remaining -= checked((ulong)read);
    }
    return hash.GetHashAndReset();
}

static byte[] ReadExact(BinaryReader reader, int count, string label)
{
    byte[] bytes = reader.ReadBytes(count);
    if (bytes.Length != count)
    {
        throw new EndOfStreamException($"{label} ended early.");
    }
    return bytes;
}

static void RequireNoForbiddenGameMarkers(string assemblyPath)
{
    EncodedMarkerMatch? marker = FirstEncodedMarker(
        assemblyPath,
        [
            "--commercial-placement-smoke",
            "--commercial-thermal-smoke",
            "--commercial-stage-g-layout-smoke",
            "--commercial-campaign-smoke=",
            "--commercial-smoke-save-path=",
            "COMMERCIAL_PLACEMENT_SMOKE_PASS",
            "COMMERCIAL_THERMAL_SMOKE_PASS",
            "COMMERCIAL_STAGE_G_LAYOUT_SMOKE_PASS",
            "COMMERCIAL_CAMPAIGN_SMOKE_PASS",
            "COMMERCIAL_PACKAGE_EVIDENCE",
            "Gridworks.Game.EmbeddedData.release-world-v1.json",
            "Gridworks.Game.EmbeddedData.release-campaign-v1.json",
            "Gridworks.Game.EmbeddedData.product-campaign-v1.json",
            "Gridworks.Game.EmbeddedData.product-heatwave-v1.json",
            "Gridworks.Game.EmbeddedData.commercial-free-placement-slice-v1.json",
            "get_SfxVoiceCount",
            "get_StoryContinueButton",
            "get_InfoViewportMinimumHeight",
            "get_ChapterReplayOptionCount",
            "get_SelectedCandidateSummary",
            "get_WeatherAnimationPhase",
            "get_HighlightedLimitingAssetId",
            "get_HighlightedEdgeIds",
            "ViewportPointForWorld",
            "_placementOutcomePresentationCount",
        ]);
    if (marker is not null)
    {
        throw new InvalidDataException(
            $"debug witness or v1 logical-name marker '{marker.Marker}' " +
            $"is present as {marker.EncodingName}: {assemblyPath}");
    }
}

static void RequireNoLocalPath(string assemblyPath)
{
    string[] markers = ["/Users/", "/home/", "/private/tmp/", "C:\\Users\\"];
    EncodedMarkerMatch? marker = FirstEncodedMarker(assemblyPath, markers);
    if (marker is not null)
    {
        throw new InvalidDataException(
            $"project assembly exposes a local absolute path '{marker.Marker}' " +
            $"as {marker.EncodingName}: {assemblyPath}");
    }
}

static void RequireNoPckDebugMarkers(string pckPath)
{
    EncodedMarkerMatch? marker = FirstEncodedMarker(
        pckPath,
        [
            "DEBUG",
            "CommercialMain.Smoke.cs",
            "--commercial-placement-smoke",
            "--commercial-thermal-smoke",
            "--commercial-stage-g-layout-smoke",
            "--commercial-campaign-smoke=",
            "--commercial-smoke-save-path=",
            "COMMERCIAL_PLACEMENT_SMOKE_PASS",
            "COMMERCIAL_THERMAL_SMOKE_PASS",
            "COMMERCIAL_STAGE_G_LAYOUT_SMOKE_PASS",
            "COMMERCIAL_CAMPAIGN_SMOKE_PASS",
            "COMMERCIAL_PACKAGE_EVIDENCE",
        ]);
    if (marker is not null)
    {
        throw new InvalidDataException(
            $"debug or smoke marker is packaged in Gridworks.pck: " +
            $"{marker.Marker} ({marker.EncodingName})");
    }
}

static EncodedMarkerMatch? FirstEncodedMarker(
    string filePath,
    IReadOnlyList<string> markers)
{
    EncodedMarkerPattern[] patterns = markers
        .SelectMany(marker => new[]
        {
            new EncodedMarkerPattern(
                marker,
                "ASCII",
                Encoding.ASCII.GetBytes(marker)),
            new EncodedMarkerPattern(
                marker,
                "UTF-16LE",
                Encoding.Unicode.GetBytes(marker)),
        })
        .ToArray();
    int overlap = patterns.Max(pattern => pattern.Bytes.Length) - 1;
    byte[] buffer = new byte[checked(64 * 1024 + overlap)];
    int retained = 0;
    using FileStream stream = File.OpenRead(filePath);
    while (true)
    {
        int read = stream.Read(buffer, retained, buffer.Length - retained);
        int available = retained + read;
        ReadOnlySpan<byte> window = buffer.AsSpan(0, available);
        foreach (EncodedMarkerPattern pattern in patterns)
        {
            if (window.IndexOf(pattern.Bytes) >= 0)
            {
                return new EncodedMarkerMatch(pattern.Marker, pattern.EncodingName);
            }
        }
        if (read == 0)
        {
            return null;
        }
        retained = Math.Min(overlap, available);
        buffer.AsSpan(available - retained, retained).CopyTo(buffer);
    }
}

static void RunNegativeProbes()
{
    string directory = Path.Combine(
        Path.GetTempPath(),
        $"gridworks-package-audit-negative-{Guid.NewGuid():N}");
    Directory.CreateDirectory(directory);
    try
    {
        string probePath = Path.Combine(directory, "probe.bin");
        File.WriteAllBytes(
            probePath,
            Encoding.ASCII.GetBytes("prefix /Users/package-audit suffix"));
        ExpectInvalidData(
            () => RequireNoLocalPath(probePath),
            "ASCII local path");

        File.WriteAllBytes(
            probePath,
            Encoding.Unicode.GetBytes("prefix C:\\Users\\package-audit suffix"));
        ExpectInvalidData(
            () => RequireNoLocalPath(probePath),
            "UTF-16LE local path");

        File.WriteAllBytes(
            probePath,
            Encoding.ASCII.GetBytes("--commercial-placement-smoke"));
        ExpectInvalidData(
            () => RequireNoForbiddenGameMarkers(probePath),
            "ASCII PE smoke marker");

        File.WriteAllBytes(
            probePath,
            Encoding.Unicode.GetBytes("COMMERCIAL_CAMPAIGN_SMOKE_PASS"));
        ExpectInvalidData(
            () => RequireNoForbiddenGameMarkers(probePath),
            "UTF-16LE PE smoke marker");

        File.WriteAllBytes(
            probePath,
            Encoding.ASCII.GetBytes("get_InfoViewportMinimumHeight"));
        ExpectInvalidData(
            () => RequireNoForbiddenGameMarkers(probePath),
            "ASCII PE smoke witness symbol");

        File.WriteAllBytes(probePath, Encoding.ASCII.GetBytes("DEBUG"));
        ExpectInvalidData(
            () => RequireNoPckDebugMarkers(probePath),
            "ASCII PCK debug marker");

        File.WriteAllBytes(
            probePath,
            Encoding.Unicode.GetBytes("--commercial-thermal-smoke"));
        ExpectInvalidData(
            () => RequireNoPckDebugMarkers(probePath),
            "UTF-16LE PCK smoke marker");

        ExpectInvalidData(
            () => ValidatePckPath("symbols/Game.PdB", 0),
            "case-insensitive PCK PDB path");
        ExpectInvalidData(
            () => ValidatePckPath("scripts/commercialmain.sMoKe.cS", 0),
            "case-insensitive PCK smoke source path");

        const string SplitMarker = "SPLIT_MARKER";
        byte[] splitMarkerBytes = Encoding.Unicode.GetBytes(SplitMarker);
        int firstWindowLength = checked(64 * 1024 + splitMarkerBytes.Length - 1);
        int splitMarkerStart = checked(firstWindowLength - 2);
        byte[] splitPayload = new byte[checked(splitMarkerStart + splitMarkerBytes.Length)];
        Array.Fill(splitPayload, (byte)'x');
        splitMarkerBytes.CopyTo(splitPayload, splitMarkerStart);
        File.WriteAllBytes(probePath, splitPayload);
        EncodedMarkerMatch? splitMatch = FirstEncodedMarker(probePath, [SplitMarker]);
        if (splitMatch is null || splitMatch.EncodingName != "UTF-16LE")
        {
            throw new InvalidDataException(
                "chunk-boundary UTF-16LE marker was not detected.");
        }

        File.WriteAllBytes(probePath, Encoding.ASCII.GetBytes("release-safe"));
        if (FirstEncodedMarker(probePath, ["DEBUG", "/Users/"]) is not null)
        {
            throw new InvalidDataException(
                "negative-probe clean payload produced a marker false positive.");
        }
    }
    finally
    {
        Directory.Delete(directory, recursive: true);
    }
}

static void ExpectInvalidData(Action action, string label)
{
    try
    {
        action();
    }
    catch (InvalidDataException)
    {
        return;
    }
    throw new InvalidDataException($"negative probe was not rejected: {label}");
}

static bool IsForbiddenLooseFile(string fileName)
{
    string normalized = fileName.ToLowerInvariant();
    return normalized is
        "releasemain.tscn" or
        "releaseaudio.tscn" or
        "releasemapview.tscn" or
        "releaseshelloverlay.tscn" or
        "releasetaskpanel.tscn" or
        "releasetheme.tres" or
        "productmain.tscn" or
        "productaudio.tscn" or
        "productshelloverlay.tscn" or
        "firstlightmapview.tscn" or
        "firstlighttaskpanel.tscn" or
        "main.tscn" or
        "scope1main.tscn" or
        "scope1placementmapview.tscn" or
        "release-world-v1.json" or
        "release-campaign-v1.json" or
        "product-campaign-v1.json" or
        "product-first-light-v1.json" or
        "product-second-heart-v1.json" or
        "product-factory-v1.json" or
        "product-heatwave-v1.json" or
        "commercial-core-slice-v1.json" or
        "commercial-free-placement-slice-v1.json" or
        "scope-0b-v1.json" or
        "scope-1-v1.json";
}

static Machine ParseMachine(string value) => value switch
{
    "i386" => Machine.I386,
    "x86_64" => Machine.Amd64,
    "arm64" => Machine.Arm64,
    _ => throw new ArgumentException($"unsupported expected PE Machine: {value}"),
};

static string MachineLabel(Machine machine) => machine switch
{
    Machine.I386 => "i386",
    Machine.Amd64 => "x86_64",
    Machine.Arm64 => "arm64",
    _ => $"0x{checked((ushort)machine):x4}",
};

static string RequireLowerHex(string value, int exactLength, string label)
{
    if (value.Length != exactLength ||
        value.Any(character =>
            !((character >= '0' && character <= '9') ||
              (character >= 'a' && character <= 'f'))))
    {
        throw new ArgumentException(
            $"{label} must be exactly {exactLength} lowercase hexadecimal characters.");
    }
    return value;
}

static string RequireProductVersion(string value)
{
    if (value != "1.0.0")
    {
        throw new ArgumentException(
            $"commercial internal candidate version must be 1.0.0: {value}");
    }
    return value;
}

static string RequireFile(string path, string label)
{
    string absolutePath = Path.GetFullPath(path);
    if (!File.Exists(absolutePath))
    {
        throw new FileNotFoundException($"{label} not found", absolutePath);
    }
    return absolutePath;
}

static string Sha256File(string path)
{
    using FileStream stream = File.OpenRead(path);
    return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
}

static string Sha256(ReadOnlySpan<byte> bytes) =>
    Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();

internal sealed record BuildIdentityExpectation(
    string SourceCommit,
    string ProductVersion,
    string WorldSha256,
    string CampaignSha256);

internal sealed record AssemblyAuditResult(Machine Machine, string Sha256);

internal sealed record RidPayload(string Name, Machine GameMachine, string Directory);

internal sealed record PckEntry(
    string Path,
    ulong Offset,
    ulong Size,
    byte[] Md5);

internal sealed record PckAuditResult(
    uint FormatVersion,
    string GodotVersion,
    int EntryCount,
    string Sha256);

internal sealed record EncodedMarkerPattern(
    string Marker,
    string EncodingName,
    byte[] Bytes);

internal sealed record EncodedMarkerMatch(
    string Marker,
    string EncodingName);
