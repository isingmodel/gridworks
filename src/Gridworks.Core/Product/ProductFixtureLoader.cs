using System.Text;
using System.Text.Json;

namespace Gridworks.Core.Product;

public static class ProductFixtureLoader
{
    private const string SupportedSchemaVersion = "gridworks.product.first-light.v1";
    private const string SupportedFixtureId = "FIRST_LIGHT_V1";

    private static readonly string[] RootFields =
    [
        "schemaVersion",
        "fixtureId",
        "displayName",
        "units",
        "mapBounds",
        "blockedCells",
        "initialMinute",
        "settlementMinutes",
        "economy",
        "existingSource",
        "town",
        "substationProject",
        "lineProject",
    ];

    private static readonly string[] UnitFields =
        ["position", "power", "energy", "time", "cash", "rate"];
    private static readonly string[] BoundsFields = ["minX", "maxX", "minY", "maxY"];
    private static readonly string[] PointFields = ["x", "y"];
    private static readonly string[] EconomyFields = ["initialCash", "saleRateCashUnitPerGWh"];
    private static readonly string[] SourceFields =
        ["assetId", "terminalId", "position", "capacityKw"];
    private static readonly string[] TownFields = ["id", "position", "demandKw"];
    private static readonly string[] SubstationFields =
    [
        "projectId",
        "assetId",
        "terminalId",
        "capacityKw",
        "serviceRadiusGridUnit",
        "costCashUnit",
        "buildMinutes",
    ];
    private static readonly string[] LineFields =
    [
        "projectId",
        "fromTerminalId",
        "toTerminalId",
        "ratingKw",
        "maxSpanGridUnit",
        "supportCostCashUnit",
        "spanCostCashUnit",
        "supportBuildMinutes",
        "spanBuildMinutes",
    ];

    public static ProductFixture Load(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Load(Encoding.UTF8.GetBytes(json));
    }

    public static ProductFixture Load(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(
                utf8Json.ToArray(),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 24,
                });

            JsonElement root = document.RootElement;
            EnsureNoDuplicateProperties(root, "$");
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("schemaVersion", out JsonElement heatwaveSchemaElement) &&
                heatwaveSchemaElement.ValueKind == JsonValueKind.String &&
                string.Equals(
                    heatwaveSchemaElement.GetString(),
                    HeatwaveFixtureSupport.SchemaVersion,
                    StringComparison.Ordinal))
            {
                ProductFixture heatwave = HeatwaveFixtureSupport.Read(root);
                Validate(heatwave);
                return heatwave;
            }
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("schemaVersion", out JsonElement factorySchemaElement) &&
                factorySchemaElement.ValueKind == JsonValueKind.String &&
                string.Equals(
                    factorySchemaElement.GetString(),
                    FactoryFixtureSupport.SchemaVersion,
                    StringComparison.Ordinal))
            {
                ProductFixture factory = FactoryFixtureSupport.Read(root);
                Validate(factory);
                return factory;
            }
            if (root.ValueKind == JsonValueKind.Object &&
                root.TryGetProperty("schemaVersion", out JsonElement schemaElement) &&
                schemaElement.ValueKind == JsonValueKind.String &&
                string.Equals(
                    schemaElement.GetString(),
                    SecondHeartFixtureSupport.SchemaVersion,
                    StringComparison.Ordinal))
            {
                ProductFixture secondHeart = SecondHeartFixtureSupport.Read(root);
                Validate(secondHeart);
                return secondHeart;
            }
            EnsureExactObject(root, RootFields, "$");

            ProductFixture fixture = new(
                ReadString(root.GetProperty("schemaVersion"), "$.schemaVersion"),
                ReadString(root.GetProperty("fixtureId"), "$.fixtureId"),
                ReadString(root.GetProperty("displayName"), "$.displayName"),
                ReadUnits(root.GetProperty("units"), "$.units"),
                ReadBounds(root.GetProperty("mapBounds"), "$.mapBounds"),
                ReadPointArray(root.GetProperty("blockedCells"), "$.blockedCells"),
                ReadInt32(root.GetProperty("initialMinute"), "$.initialMinute"),
                ReadInt32(root.GetProperty("settlementMinutes"), "$.settlementMinutes"),
                ReadEconomy(root.GetProperty("economy"), "$.economy"),
                ReadSource(root.GetProperty("existingSource"), "$.existingSource"),
                ReadTown(root.GetProperty("town"), "$.town"),
                ReadSubstation(
                    root.GetProperty("substationProject"),
                    "$.substationProject"),
                ReadLine(root.GetProperty("lineProject"), "$.lineProject"));

            Validate(fixture);
            return fixture;
        }
        catch (ProductFixtureValidationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or NotSupportedException or OverflowException or
            InvalidOperationException or KeyNotFoundException)
        {
            throw new ProductFixtureValidationException(
                "First Light product fixture JSON is invalid.",
                exception);
        }
    }

    internal static void Validate(ProductFixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        if (string.Equals(
                fixture.SchemaVersion,
                HeatwaveFixtureSupport.SchemaVersion,
                StringComparison.Ordinal))
        {
            HeatwaveFixtureSupport.Validate(fixture);
            return;
        }
        if (string.Equals(
                fixture.SchemaVersion,
                FactoryFixtureSupport.SchemaVersion,
                StringComparison.Ordinal))
        {
            FactoryFixtureSupport.Validate(fixture);
            return;
        }
        if (string.Equals(
                fixture.SchemaVersion,
                SecondHeartFixtureSupport.SchemaVersion,
                StringComparison.Ordinal))
        {
            SecondHeartFixtureSupport.Validate(fixture);
            return;
        }
        ArgumentNullException.ThrowIfNull(fixture.Units);
        ArgumentNullException.ThrowIfNull(fixture.MapBounds);
        ArgumentNullException.ThrowIfNull(fixture.BlockedCells);
        ArgumentNullException.ThrowIfNull(fixture.Economy);
        ArgumentNullException.ThrowIfNull(fixture.ExistingSource);
        ArgumentNullException.ThrowIfNull(fixture.Town);
        ArgumentNullException.ThrowIfNull(fixture.SubstationProject);
        ArgumentNullException.ThrowIfNull(fixture.LineProject);

        if (!string.Equals(fixture.SchemaVersion, SupportedSchemaVersion, StringComparison.Ordinal))
        {
            throw new ProductFixtureValidationException(
                $"Unsupported product schemaVersion '{fixture.SchemaVersion}'.");
        }
        if (!string.Equals(fixture.FixtureId, SupportedFixtureId, StringComparison.Ordinal))
        {
            throw new ProductFixtureValidationException(
                $"Unsupported product fixtureId '{fixture.FixtureId}'.");
        }
        RequireNonBlank(fixture.DisplayName, "displayName");

        ProductUnits units = fixture.Units;
        if (!string.Equals(units.Position, "GridUnit", StringComparison.Ordinal) ||
            !string.Equals(units.Power, "kW", StringComparison.Ordinal) ||
            !string.Equals(units.Energy, "kWMinute", StringComparison.Ordinal) ||
            !string.Equals(units.Time, "GameMinute", StringComparison.Ordinal) ||
            !string.Equals(units.Cash, "CashUnit", StringComparison.Ordinal) ||
            !string.Equals(units.Rate, "CashUnitPerGWh", StringComparison.Ordinal))
        {
            throw new ProductFixtureValidationException("Product fixture units are unsupported.");
        }

        ProductMapBounds bounds = fixture.MapBounds;
        if (bounds.MinX >= bounds.MaxX || bounds.MinY >= bounds.MaxY)
        {
            throw new ProductFixtureValidationException(
                "Product map bounds must be strictly increasing on both axes.");
        }

        ProductPoint sourcePosition = fixture.ExistingSource.Position
            ?? throw new ProductFixtureValidationException("existingSource.position cannot be null.");
        ProductPoint townPosition = fixture.Town.Position
            ?? throw new ProductFixtureValidationException("town.position cannot be null.");
        if (!Contains(bounds, sourcePosition) || !Contains(bounds, townPosition))
        {
            throw new ProductFixtureValidationException(
                "Existing source and town positions must be inside map bounds.");
        }
        if (sourcePosition == townPosition)
        {
            throw new ProductFixtureValidationException(
                "Existing source and town positions must be distinct.");
        }

        HashSet<ProductPoint> blocked = new();
        foreach (ProductPoint? point in fixture.BlockedCells)
        {
            if (point is null)
            {
                throw new ProductFixtureValidationException("blockedCells cannot contain null.");
            }
            if (!Contains(bounds, point))
            {
                throw new ProductFixtureValidationException(
                    $"Blocked cell ({point.X},{point.Y}) is outside map bounds.");
            }
            if (!blocked.Add(point))
            {
                throw new ProductFixtureValidationException(
                    $"Blocked cell ({point.X},{point.Y}) is duplicated.");
            }
            if (point == sourcePosition || point == townPosition)
            {
                throw new ProductFixtureValidationException(
                    "Blocked cells cannot overlap the source or town.");
            }
        }

        string[] definedIds =
        [
            fixture.ExistingSource.AssetId,
            fixture.ExistingSource.TerminalId,
            fixture.Town.Id,
            fixture.SubstationProject.ProjectId,
            fixture.SubstationProject.AssetId,
            fixture.SubstationProject.TerminalId,
            fixture.LineProject.ProjectId,
        ];
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (string id in definedIds)
        {
            RequireNonBlank(id, "stable ID");
            if (!ids.Add(id))
            {
                throw new ProductFixtureValidationException($"Stable ID '{id}' is duplicated.");
            }
        }

        RequireNonBlank(fixture.LineProject.FromTerminalId, "lineProject.fromTerminalId");
        RequireNonBlank(fixture.LineProject.ToTerminalId, "lineProject.toTerminalId");
        if (!string.Equals(
                fixture.LineProject.FromTerminalId,
                fixture.ExistingSource.TerminalId,
                StringComparison.Ordinal) ||
            !string.Equals(
                fixture.LineProject.ToTerminalId,
                fixture.SubstationProject.TerminalId,
                StringComparison.Ordinal))
        {
            throw new ProductFixtureValidationException(
                "Line terminal references must match the source and substation terminals.");
        }

        if (fixture.InitialMinute < 0 || fixture.Economy.InitialCash < 0)
        {
            throw new ProductFixtureValidationException(
                "initialMinute and initialCash must be non-negative.");
        }
        RequirePositive(fixture.SettlementMinutes, "settlementMinutes");
        RequirePositive(fixture.Economy.SaleRateCashUnitPerGWh, "saleRateCashUnitPerGWh");
        RequirePositive(fixture.ExistingSource.CapacityKw, "existingSource.capacityKw");
        RequirePositive(fixture.Town.DemandKw, "town.demandKw");
        RequirePositive(fixture.SubstationProject.CapacityKw, "substationProject.capacityKw");
        RequirePositive(
            fixture.SubstationProject.ServiceRadiusGridUnit,
            "substationProject.serviceRadiusGridUnit");
        RequirePositive(fixture.SubstationProject.CostCashUnit, "substationProject.costCashUnit");
        RequirePositive(fixture.SubstationProject.BuildMinutes, "substationProject.buildMinutes");
        RequirePositive(fixture.LineProject.RatingKw, "lineProject.ratingKw");
        RequirePositive(fixture.LineProject.MaxSpanGridUnit, "lineProject.maxSpanGridUnit");
        RequirePositive(
            fixture.LineProject.SupportCostCashUnit,
            "lineProject.supportCostCashUnit");
        RequirePositive(fixture.LineProject.SpanCostCashUnit, "lineProject.spanCostCashUnit");
        RequirePositive(
            fixture.LineProject.SupportBuildMinutes,
            "lineProject.supportBuildMinutes");
        RequirePositive(fixture.LineProject.SpanBuildMinutes, "lineProject.spanBuildMinutes");

        ValidateArithmetic(fixture);
    }

    internal static bool Contains(ProductMapBounds bounds, ProductPoint point) =>
        point.X >= bounds.MinX && point.X <= bounds.MaxX &&
        point.Y >= bounds.MinY && point.Y <= bounds.MaxY;

    internal static long DistanceSquared(ProductPoint from, ProductPoint to)
    {
        long dx = (long)to.X - from.X;
        long dy = (long)to.Y - from.Y;
        return checked(checked(dx * dx) + checked(dy * dy));
    }

    private static void ValidateArithmetic(ProductFixture fixture)
    {
        try
        {
            ProductMapBounds bounds = fixture.MapBounds;
            long width = (long)bounds.MaxX - bounds.MinX;
            long height = (long)bounds.MaxY - bounds.MinY;
            _ = checked(checked(width * width) + checked(height * height));
            _ = checked(
                (long)fixture.SubstationProject.ServiceRadiusGridUnit *
                fixture.SubstationProject.ServiceRadiusGridUnit);
            _ = checked(
                (long)fixture.LineProject.MaxSpanGridUnit *
                fixture.LineProject.MaxSpanGridUnit);

            long cellCount = checked(checked(width + 1) * checked(height + 1));
            if (cellCount > int.MaxValue)
            {
                throw new ProductFixtureValidationException(
                    "Product map contains more cells than a runtime support list can address.");
            }

            long maximumSupports = cellCount;
            long maximumSpans = checked(maximumSupports + 1);
            long maximumLineCost = checked(
                checked(maximumSupports * fixture.LineProject.SupportCostCashUnit) +
                checked(maximumSpans * fixture.LineProject.SpanCostCashUnit));
            long maximumLineBuildMinutes = checked(
                checked(maximumSupports * fixture.LineProject.SupportBuildMinutes) +
                checked(maximumSpans * fixture.LineProject.SpanBuildMinutes));
            _ = maximumLineCost;
            _ = checked(
                checked((long)fixture.InitialMinute + fixture.SubstationProject.BuildMinutes) +
                checked(maximumLineBuildMinutes + fixture.SettlementMinutes));

            long potentialEnergy = checked(
                fixture.Town.DemandKw * fixture.SettlementMinutes);
            long potentialRevenueNumerator = checked(
                potentialEnergy * fixture.Economy.SaleRateCashUnitPerGWh);
            const long revenueDenominator = 60_000_000;
            if (potentialRevenueNumerator % revenueDenominator != 0)
            {
                throw new ProductFixtureValidationException(
                    "Potential settlement revenue must divide exactly into CashUnit.");
            }
            long potentialRevenue = potentialRevenueNumerator / revenueDenominator;
            _ = checked(fixture.Economy.InitialCash + potentialRevenue);
        }
        catch (ProductFixtureValidationException)
        {
            throw;
        }
        catch (OverflowException exception)
        {
            throw new ProductFixtureValidationException(
                "Product fixture arithmetic exceeds the supported 64-bit range.",
                exception);
        }
    }

    private static ProductUnits ReadUnits(JsonElement element, string path)
    {
        EnsureExactObject(element, UnitFields, path);
        return new ProductUnits(
            ReadString(element.GetProperty("position"), $"{path}.position"),
            ReadString(element.GetProperty("power"), $"{path}.power"),
            ReadString(element.GetProperty("energy"), $"{path}.energy"),
            ReadString(element.GetProperty("time"), $"{path}.time"),
            ReadString(element.GetProperty("cash"), $"{path}.cash"),
            ReadString(element.GetProperty("rate"), $"{path}.rate"));
    }

    private static ProductMapBounds ReadBounds(JsonElement element, string path)
    {
        EnsureExactObject(element, BoundsFields, path);
        return new ProductMapBounds(
            ReadInt32(element.GetProperty("minX"), $"{path}.minX"),
            ReadInt32(element.GetProperty("maxX"), $"{path}.maxX"),
            ReadInt32(element.GetProperty("minY"), $"{path}.minY"),
            ReadInt32(element.GetProperty("maxY"), $"{path}.maxY"));
    }

    private static ProductEconomy ReadEconomy(JsonElement element, string path)
    {
        EnsureExactObject(element, EconomyFields, path);
        return new ProductEconomy(
            ReadInt64(element.GetProperty("initialCash"), $"{path}.initialCash"),
            ReadInt64(
                element.GetProperty("saleRateCashUnitPerGWh"),
                $"{path}.saleRateCashUnitPerGWh"));
    }

    private static ProductExistingSource ReadSource(JsonElement element, string path)
    {
        EnsureExactObject(element, SourceFields, path);
        return new ProductExistingSource(
            ReadString(element.GetProperty("assetId"), $"{path}.assetId"),
            ReadString(element.GetProperty("terminalId"), $"{path}.terminalId"),
            ReadPoint(element.GetProperty("position"), $"{path}.position"),
            ReadInt64(element.GetProperty("capacityKw"), $"{path}.capacityKw"));
    }

    private static ProductTown ReadTown(JsonElement element, string path)
    {
        EnsureExactObject(element, TownFields, path);
        return new ProductTown(
            ReadString(element.GetProperty("id"), $"{path}.id"),
            ReadPoint(element.GetProperty("position"), $"{path}.position"),
            ReadInt64(element.GetProperty("demandKw"), $"{path}.demandKw"));
    }

    private static ProductSubstationProjectDefinition ReadSubstation(
        JsonElement element,
        string path)
    {
        EnsureExactObject(element, SubstationFields, path);
        return new ProductSubstationProjectDefinition(
            ReadString(element.GetProperty("projectId"), $"{path}.projectId"),
            ReadString(element.GetProperty("assetId"), $"{path}.assetId"),
            ReadString(element.GetProperty("terminalId"), $"{path}.terminalId"),
            ReadInt64(element.GetProperty("capacityKw"), $"{path}.capacityKw"),
            ReadInt32(
                element.GetProperty("serviceRadiusGridUnit"),
                $"{path}.serviceRadiusGridUnit"),
            ReadInt64(element.GetProperty("costCashUnit"), $"{path}.costCashUnit"),
            ReadInt32(element.GetProperty("buildMinutes"), $"{path}.buildMinutes"));
    }

    private static ProductLineProjectDefinition ReadLine(JsonElement element, string path)
    {
        EnsureExactObject(element, LineFields, path);
        return new ProductLineProjectDefinition(
            ReadString(element.GetProperty("projectId"), $"{path}.projectId"),
            ReadString(element.GetProperty("fromTerminalId"), $"{path}.fromTerminalId"),
            ReadString(element.GetProperty("toTerminalId"), $"{path}.toTerminalId"),
            ReadInt64(element.GetProperty("ratingKw"), $"{path}.ratingKw"),
            ReadInt32(element.GetProperty("maxSpanGridUnit"), $"{path}.maxSpanGridUnit"),
            ReadInt64(
                element.GetProperty("supportCostCashUnit"),
                $"{path}.supportCostCashUnit"),
            ReadInt64(element.GetProperty("spanCostCashUnit"), $"{path}.spanCostCashUnit"),
            ReadInt32(
                element.GetProperty("supportBuildMinutes"),
                $"{path}.supportBuildMinutes"),
            ReadInt32(
                element.GetProperty("spanBuildMinutes"),
                $"{path}.spanBuildMinutes"));
    }

    private static IReadOnlyList<ProductPoint> ReadPointArray(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Array)
        {
            throw new ProductFixtureValidationException($"{path} must be an array.");
        }

        List<ProductPoint> points = [];
        int index = 0;
        foreach (JsonElement item in element.EnumerateArray())
        {
            points.Add(ReadPoint(item, $"{path}[{index}]"));
            index++;
        }
        return Array.AsReadOnly(points.ToArray());
    }

    private static ProductPoint ReadPoint(JsonElement element, string path)
    {
        EnsureExactObject(element, PointFields, path);
        return new ProductPoint(
            ReadInt32(element.GetProperty("x"), $"{path}.x"),
            ReadInt32(element.GetProperty("y"), $"{path}.y"));
    }

    private static string ReadString(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new ProductFixtureValidationException($"{path} must be a string.");
        }
        return element.GetString()
            ?? throw new ProductFixtureValidationException($"{path} cannot be null.");
    }

    private static int ReadInt32(JsonElement element, string path)
    {
        string token = IntegerToken(element, path);
        if (!int.TryParse(
                token,
                System.Globalization.NumberStyles.AllowLeadingSign,
                System.Globalization.CultureInfo.InvariantCulture,
                out int value))
        {
            throw new ProductFixtureValidationException($"{path} must be a 32-bit integer.");
        }
        return value;
    }

    private static long ReadInt64(JsonElement element, string path)
    {
        string token = IntegerToken(element, path);
        if (!long.TryParse(
                token,
                System.Globalization.NumberStyles.AllowLeadingSign,
                System.Globalization.CultureInfo.InvariantCulture,
                out long value))
        {
            throw new ProductFixtureValidationException($"{path} must be a 64-bit integer.");
        }
        return value;
    }

    private static string IntegerToken(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Number)
        {
            throw new ProductFixtureValidationException($"{path} must be an integer.");
        }
        string token = element.GetRawText();
        int index = token.Length > 0 && token[0] == '-' ? 1 : 0;
        if (index == token.Length)
        {
            throw new ProductFixtureValidationException($"{path} must be an integer.");
        }
        for (; index < token.Length; index++)
        {
            if (token[index] is < '0' or > '9')
            {
                throw new ProductFixtureValidationException($"{path} must be an integer token.");
            }
        }
        return token;
    }

    private static void RequireNonBlank(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ProductFixtureValidationException($"{label} cannot be blank.");
        }
    }

    private static void RequirePositive(long value, string label)
    {
        if (value <= 0)
        {
            throw new ProductFixtureValidationException($"{label} must be positive.");
        }
    }

    private static void EnsureExactObject(
        JsonElement element,
        IReadOnlyCollection<string> expectedFields,
        string path)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new ProductFixtureValidationException($"{path} must be an object.");
        }

        HashSet<string> remaining = new(expectedFields, StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (!remaining.Remove(property.Name))
            {
                throw new ProductFixtureValidationException(
                    $"Unknown or duplicate property '{property.Name}' at {path}.");
            }
        }
        if (remaining.Count != 0)
        {
            throw new ProductFixtureValidationException(
                $"Missing property '{remaining.OrderBy(value => value, StringComparer.Ordinal).First()}' at {path}.");
        }
    }

    private static void EnsureNoDuplicateProperties(JsonElement element, string path)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            HashSet<string> names = new(StringComparer.Ordinal);
            foreach (JsonProperty property in element.EnumerateObject())
            {
                if (!names.Add(property.Name))
                {
                    throw new ProductFixtureValidationException(
                        $"Duplicate JSON property '{property.Name}' at {path}.");
                }
                EnsureNoDuplicateProperties(property.Value, $"{path}.{property.Name}");
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            int index = 0;
            foreach (JsonElement item in element.EnumerateArray())
            {
                EnsureNoDuplicateProperties(item, $"{path}[{index}]");
                index++;
            }
        }
    }
}
