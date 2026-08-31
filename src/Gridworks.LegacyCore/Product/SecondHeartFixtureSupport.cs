using System.Globalization;
using System.Text.Json;

namespace Gridworks.Core.Product;

internal static class SecondHeartFixtureSupport
{
    internal const string SchemaVersion = "gridworks.product.second-heart.v1";
    private const string FixtureId = "SECOND_HEART_V1";
    private const long EnergyPerGWh = 60_000_000;

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
        "hospital",
        "hospitalLineProjects",
        "spatialIncident",
        "hospitalEconomy",
    ];
    private static readonly string[] UnitFields =
        ["position", "power", "energy", "time", "cash", "rate"];
    private static readonly string[] BoundsFields = ["minX", "maxX", "minY", "maxY"];
    private static readonly string[] PointFields = ["x", "y"];
    private static readonly string[] EconomyFields = ["initialCash", "saleRateCashUnitPerGWh"];
    private static readonly string[] SourceFields =
        ["assetId", "terminalId", "position", "capacityKw"];
    private static readonly string[] TownFields = ["id", "position", "demandKw", "priority"];
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
    private static readonly string[] HospitalFields =
    [
        "id",
        "position",
        "demandKw",
        "priority",
        "primaryTerminalId",
        "backupTerminalId",
        "upsMinutes",
        "dieselMinutes",
    ];
    private static readonly string[] HospitalLineFields =
    [
        "projectId",
        "fromTerminalId",
        "toTerminalId",
        "routePriority",
        "ratingKw",
        "maxSpanGridUnit",
        "supportCostCashUnit",
        "spanCostCashUnit",
        "supportBuildMinutes",
        "spanBuildMinutes",
    ];
    private static readonly string[] IncidentFields =
        ["id", "riskRect", "leadMinutes", "durationMinutes"];
    private static readonly string[] HospitalEconomyFields =
    [
        "variableGenerationCostCashUnitPerGWh",
        "unservedCompensationCashUnitPerGWh",
        "lostSalesCashUnitPerGWh",
    ];

    internal static ProductFixture Read(JsonElement root)
    {
        return ReadCumulative(root, RootFields);
    }

    internal static ProductFixture ReadCumulative(
        JsonElement root,
        IReadOnlyCollection<string> rootFields)
    {
        Exact(root, rootFields, "$");
        ProductHospital hospital = ReadHospital(root.GetProperty("hospital"), "$.hospital");
        return new ProductFixture(
            Text(root, "schemaVersion", "$"),
            Text(root, "fixtureId", "$"),
            Text(root, "displayName", "$"),
            ReadUnits(root.GetProperty("units"), "$.units"),
            ReadBounds(root.GetProperty("mapBounds"), "$.mapBounds"),
            ReadPoints(root.GetProperty("blockedCells"), "$.blockedCells"),
            Int32(root.GetProperty("initialMinute"), "$.initialMinute"),
            Int32(root.GetProperty("settlementMinutes"), "$.settlementMinutes"),
            ReadEconomy(root.GetProperty("economy"), "$.economy"),
            ReadSource(root.GetProperty("existingSource"), "$.existingSource"),
            ReadTown(root.GetProperty("town"), "$.town"),
            ReadSubstation(root.GetProperty("substationProject"), "$.substationProject"),
            ReadLine(root.GetProperty("lineProject"), "$.lineProject"),
            hospital,
            ReadHospitalLines(root.GetProperty("hospitalLineProjects"), "$.hospitalLineProjects"),
            ReadIncident(root.GetProperty("spatialIncident"), "$.spatialIncident"),
            ReadHospitalEconomy(root.GetProperty("hospitalEconomy"), "$.hospitalEconomy"));
    }

    internal static void Validate(ProductFixture fixture)
    {
        if (fixture.SchemaVersion != SchemaVersion || fixture.FixtureId != FixtureId)
        {
            throw new ProductFixtureValidationException(
                "Second Heart schemaVersion and fixtureId must match the supported pair.");
        }

        ProductFixture firstLightShape = fixture with
        {
            SchemaVersion = "gridworks.product.first-light.v1",
            FixtureId = "FIRST_LIGHT_V1",
            Town = fixture.Town with { Priority = 0 },
            Hospital = null,
            HospitalLineProjects = null,
            SpatialIncident = null,
            HospitalEconomy = null,
        };
        ProductFixtureLoader.Validate(firstLightShape);

        ProductHospital hospital = fixture.Hospital
            ?? throw new ProductFixtureValidationException("hospital cannot be null.");
        IReadOnlyList<ProductHospitalLineProjectDefinition> lines = fixture.HospitalLineProjects
            ?? throw new ProductFixtureValidationException("hospitalLineProjects cannot be null.");
        ProductSpatialIncident incident = fixture.SpatialIncident
            ?? throw new ProductFixtureValidationException("spatialIncident cannot be null.");
        ProductHospitalEconomy economy = fixture.HospitalEconomy
            ?? throw new ProductFixtureValidationException("hospitalEconomy cannot be null.");

        if (!ProductFixtureLoader.Contains(fixture.MapBounds, hospital.Position) ||
            hospital.Position == fixture.ExistingSource.Position ||
            hospital.Position == fixture.Town.Position ||
            fixture.BlockedCells.Contains(hospital.Position))
        {
            throw new ProductFixtureValidationException(
                "Hospital position must be a distinct, buildable map cell.");
        }
        Positive(hospital.DemandKw, "hospital.demandKw");
        Positive(hospital.UpsMinutes, "hospital.upsMinutes");
        Positive(hospital.DieselMinutes, "hospital.dieselMinutes");
        if (hospital.Priority < 0 || fixture.Town.Priority < 0 ||
            hospital.Priority >= fixture.Town.Priority)
        {
            throw new ProductFixtureValidationException(
                "Hospital must have a smaller non-negative priority than town.");
        }
        if (lines.Count != 2 || lines.Any(line => line is null))
        {
            throw new ProductFixtureValidationException(
                "hospitalLineProjects must contain exactly two projects.");
        }

        ProductHospitalLineProjectDefinition[] primary = lines
            .Where(line => line.ToTerminalId == hospital.PrimaryTerminalId).ToArray();
        ProductHospitalLineProjectDefinition[] backup = lines
            .Where(line => line.ToTerminalId == hospital.BackupTerminalId).ToArray();
        if (primary.Length != 1 || backup.Length != 1 ||
            primary[0].ProjectId == backup[0].ProjectId)
        {
            throw new ProductFixtureValidationException(
                "Hospital lines must reference each hospital terminal exactly once.");
        }
        if (primary[0].RoutePriority < 0 || backup[0].RoutePriority < 0 ||
            primary[0].RoutePriority == backup[0].RoutePriority)
        {
            throw new ProductFixtureValidationException(
                "Hospital route priorities must be distinct and non-negative.");
        }
        foreach (ProductHospitalLineProjectDefinition line in lines)
        {
            NonBlank(line.ProjectId, "hospital line projectId");
            if (line.FromTerminalId != fixture.ExistingSource.TerminalId)
            {
                throw new ProductFixtureValidationException(
                    "Hospital lines must start at the existing source terminal.");
            }
            Positive(line.RatingKw, "hospital line ratingKw");
            Positive(line.MaxSpanGridUnit, "hospital line maxSpanGridUnit");
            Positive(line.SupportCostCashUnit, "hospital line supportCostCashUnit");
            Positive(line.SpanCostCashUnit, "hospital line spanCostCashUnit");
            Positive(line.SupportBuildMinutes, "hospital line supportBuildMinutes");
            Positive(line.SpanBuildMinutes, "hospital line spanBuildMinutes");
        }

        UniqueIds(
        [
            fixture.ExistingSource.AssetId,
            fixture.ExistingSource.TerminalId,
            fixture.Town.Id,
            fixture.SubstationProject.ProjectId,
            fixture.SubstationProject.AssetId,
            fixture.SubstationProject.TerminalId,
            fixture.LineProject.ProjectId,
            hospital.Id,
            hospital.PrimaryTerminalId,
            hospital.BackupTerminalId,
            primary[0].ProjectId,
            backup[0].ProjectId,
            incident.Id,
        ]);

        ProductRiskRect rect = incident.RiskRect
            ?? throw new ProductFixtureValidationException("spatialIncident.riskRect cannot be null.");
        if (rect.MinX >= rect.MaxX || rect.MinY >= rect.MaxY ||
            rect.MinX < fixture.MapBounds.MinX || rect.MaxX > fixture.MapBounds.MaxX ||
            rect.MinY < fixture.MapBounds.MinY || rect.MaxY > fixture.MapBounds.MaxY)
        {
            throw new ProductFixtureValidationException(
                "riskRect must be strictly increasing and inside map bounds.");
        }
        Positive(incident.LeadMinutes, "spatialIncident.leadMinutes");
        Positive(incident.DurationMinutes, "spatialIncident.durationMinutes");
        Positive(economy.VariableGenerationCostCashUnitPerGWh, "generation rate");
        Positive(economy.UnservedCompensationCashUnitPerGWh, "compensation rate");
        Positive(economy.LostSalesCashUnitPerGWh, "lost sales rate");

        try
        {
            long totalDemand = checked(hospital.DemandKw + fixture.Town.DemandKw);
            long maximumEventEnergy = checked(totalDemand * incident.DurationMinutes);
            ExactCash(maximumEventEnergy, fixture.Economy.SaleRateCashUnitPerGWh);
            ExactCash(maximumEventEnergy, economy.VariableGenerationCostCashUnitPerGWh);
            ExactCash(maximumEventEnergy, economy.UnservedCompensationCashUnitPerGWh);
            ExactCash(maximumEventEnergy, economy.LostSalesCashUnitPerGWh);
            foreach (ProductHospitalLineProjectDefinition line in lines)
            {
                _ = checked((long)line.MaxSpanGridUnit * line.MaxSpanGridUnit);
            }
            _ = checked((long)fixture.InitialMinute + incident.LeadMinutes + incident.DurationMinutes);
        }
        catch (OverflowException exception)
        {
            throw new ProductFixtureValidationException(
                "Second Heart arithmetic exceeds the supported range.",
                exception);
        }
    }

    private static void ExactCash(long energy, long rate)
    {
        long numerator = checked(energy * rate);
        if (numerator % EnergyPerGWh != 0)
        {
            throw new ProductFixtureValidationException(
                "Second Heart rate calculation must divide exactly into CashUnit.");
        }
    }

    private static ProductUnits ReadUnits(JsonElement value, string path)
    {
        Exact(value, UnitFields, path);
        return new ProductUnits(
            Text(value, "position", path), Text(value, "power", path),
            Text(value, "energy", path), Text(value, "time", path),
            Text(value, "cash", path), Text(value, "rate", path));
    }

    private static ProductMapBounds ReadBounds(JsonElement value, string path)
    {
        Exact(value, BoundsFields, path);
        return new ProductMapBounds(
            Int32(value.GetProperty("minX"), $"{path}.minX"),
            Int32(value.GetProperty("maxX"), $"{path}.maxX"),
            Int32(value.GetProperty("minY"), $"{path}.minY"),
            Int32(value.GetProperty("maxY"), $"{path}.maxY"));
    }

    private static ProductEconomy ReadEconomy(JsonElement value, string path)
    {
        Exact(value, EconomyFields, path);
        return new ProductEconomy(
            Int64(value.GetProperty("initialCash"), $"{path}.initialCash"),
            Int64(value.GetProperty("saleRateCashUnitPerGWh"), $"{path}.saleRateCashUnitPerGWh"));
    }

    private static ProductExistingSource ReadSource(JsonElement value, string path)
    {
        Exact(value, SourceFields, path);
        return new ProductExistingSource(
            Text(value, "assetId", path), Text(value, "terminalId", path),
            ReadPoint(value.GetProperty("position"), $"{path}.position"),
            Int64(value.GetProperty("capacityKw"), $"{path}.capacityKw"));
    }

    private static ProductTown ReadTown(JsonElement value, string path)
    {
        Exact(value, TownFields, path);
        return new ProductTown(
            Text(value, "id", path), ReadPoint(value.GetProperty("position"), $"{path}.position"),
            Int64(value.GetProperty("demandKw"), $"{path}.demandKw"),
            Int32(value.GetProperty("priority"), $"{path}.priority"));
    }

    private static ProductSubstationProjectDefinition ReadSubstation(JsonElement value, string path)
    {
        Exact(value, SubstationFields, path);
        return new ProductSubstationProjectDefinition(
            Text(value, "projectId", path), Text(value, "assetId", path),
            Text(value, "terminalId", path),
            Int64(value.GetProperty("capacityKw"), $"{path}.capacityKw"),
            Int32(value.GetProperty("serviceRadiusGridUnit"), $"{path}.serviceRadiusGridUnit"),
            Int64(value.GetProperty("costCashUnit"), $"{path}.costCashUnit"),
            Int32(value.GetProperty("buildMinutes"), $"{path}.buildMinutes"));
    }

    private static ProductLineProjectDefinition ReadLine(JsonElement value, string path)
    {
        Exact(value, LineFields, path);
        return new ProductLineProjectDefinition(
            Text(value, "projectId", path), Text(value, "fromTerminalId", path),
            Text(value, "toTerminalId", path),
            Int64(value.GetProperty("ratingKw"), $"{path}.ratingKw"),
            Int32(value.GetProperty("maxSpanGridUnit"), $"{path}.maxSpanGridUnit"),
            Int64(value.GetProperty("supportCostCashUnit"), $"{path}.supportCostCashUnit"),
            Int64(value.GetProperty("spanCostCashUnit"), $"{path}.spanCostCashUnit"),
            Int32(value.GetProperty("supportBuildMinutes"), $"{path}.supportBuildMinutes"),
            Int32(value.GetProperty("spanBuildMinutes"), $"{path}.spanBuildMinutes"));
    }

    private static ProductHospital ReadHospital(JsonElement value, string path)
    {
        Exact(value, HospitalFields, path);
        return new ProductHospital(
            Text(value, "id", path), ReadPoint(value.GetProperty("position"), $"{path}.position"),
            Int64(value.GetProperty("demandKw"), $"{path}.demandKw"),
            Int32(value.GetProperty("priority"), $"{path}.priority"),
            Text(value, "primaryTerminalId", path), Text(value, "backupTerminalId", path),
            Int32(value.GetProperty("upsMinutes"), $"{path}.upsMinutes"),
            Int32(value.GetProperty("dieselMinutes"), $"{path}.dieselMinutes"));
    }

    private static IReadOnlyList<ProductHospitalLineProjectDefinition> ReadHospitalLines(
        JsonElement value,
        string path)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new ProductFixtureValidationException($"{path} must be an array.");
        }
        List<ProductHospitalLineProjectDefinition> result = [];
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string itemPath = $"{path}[{index++}]";
            Exact(item, HospitalLineFields, itemPath);
            result.Add(new ProductHospitalLineProjectDefinition(
                Text(item, "projectId", itemPath), Text(item, "fromTerminalId", itemPath),
                Text(item, "toTerminalId", itemPath),
                Int32(item.GetProperty("routePriority"), $"{itemPath}.routePriority"),
                Int64(item.GetProperty("ratingKw"), $"{itemPath}.ratingKw"),
                Int32(item.GetProperty("maxSpanGridUnit"), $"{itemPath}.maxSpanGridUnit"),
                Int64(item.GetProperty("supportCostCashUnit"), $"{itemPath}.supportCostCashUnit"),
                Int64(item.GetProperty("spanCostCashUnit"), $"{itemPath}.spanCostCashUnit"),
                Int32(item.GetProperty("supportBuildMinutes"), $"{itemPath}.supportBuildMinutes"),
                Int32(item.GetProperty("spanBuildMinutes"), $"{itemPath}.spanBuildMinutes")));
        }
        return Array.AsReadOnly(result.ToArray());
    }

    private static ProductSpatialIncident ReadIncident(JsonElement value, string path)
    {
        Exact(value, IncidentFields, path);
        JsonElement rect = value.GetProperty("riskRect");
        Exact(rect, BoundsFields, $"{path}.riskRect");
        return new ProductSpatialIncident(
            Text(value, "id", path),
            new ProductRiskRect(
                Int32(rect.GetProperty("minX"), $"{path}.riskRect.minX"),
                Int32(rect.GetProperty("maxX"), $"{path}.riskRect.maxX"),
                Int32(rect.GetProperty("minY"), $"{path}.riskRect.minY"),
                Int32(rect.GetProperty("maxY"), $"{path}.riskRect.maxY")),
            Int32(value.GetProperty("leadMinutes"), $"{path}.leadMinutes"),
            Int32(value.GetProperty("durationMinutes"), $"{path}.durationMinutes"));
    }

    private static ProductHospitalEconomy ReadHospitalEconomy(JsonElement value, string path)
    {
        Exact(value, HospitalEconomyFields, path);
        return new ProductHospitalEconomy(
            Int64(value.GetProperty("variableGenerationCostCashUnitPerGWh"),
                $"{path}.variableGenerationCostCashUnitPerGWh"),
            Int64(value.GetProperty("unservedCompensationCashUnitPerGWh"),
                $"{path}.unservedCompensationCashUnitPerGWh"),
            Int64(value.GetProperty("lostSalesCashUnitPerGWh"),
                $"{path}.lostSalesCashUnitPerGWh"));
    }

    private static IReadOnlyList<ProductPoint> ReadPoints(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new ProductFixtureValidationException($"{path} must be an array.");
        }
        List<ProductPoint> result = [];
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            result.Add(ReadPoint(item, $"{path}[{index++}]"));
        }
        return Array.AsReadOnly(result.ToArray());
    }

    private static ProductPoint ReadPoint(JsonElement value, string path)
    {
        Exact(value, PointFields, path);
        return new ProductPoint(
            Int32(value.GetProperty("x"), $"{path}.x"),
            Int32(value.GetProperty("y"), $"{path}.y"));
    }

    private static string Text(JsonElement parent, string name, string path)
    {
        JsonElement value = parent.GetProperty(name);
        if (value.ValueKind != JsonValueKind.String || value.GetString() is not string result)
        {
            throw new ProductFixtureValidationException($"{path}.{name} must be a string.");
        }
        return result;
    }

    private static int Int32(JsonElement value, string path)
    {
        string token = IntegerToken(value, path);
        if (!int.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int result))
        {
            throw new ProductFixtureValidationException($"{path} must be a 32-bit integer.");
        }
        return result;
    }

    private static long Int64(JsonElement value, string path)
    {
        string token = IntegerToken(value, path);
        if (!long.TryParse(token, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long result))
        {
            throw new ProductFixtureValidationException($"{path} must be a 64-bit integer.");
        }
        return result;
    }

    private static string IntegerToken(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Number)
        {
            throw new ProductFixtureValidationException($"{path} must be an integer.");
        }
        string token = value.GetRawText();
        int start = token.Length > 0 && token[0] == '-' ? 1 : 0;
        if (start == token.Length || token[start..].Any(character => character is < '0' or > '9'))
        {
            throw new ProductFixtureValidationException($"{path} must be an integer token.");
        }
        return token;
    }

    private static void Exact(JsonElement value, IReadOnlyCollection<string> fields, string path)
    {
        if (value.ValueKind != JsonValueKind.Object)
        {
            throw new ProductFixtureValidationException($"{path} must be an object.");
        }
        HashSet<string> remaining = new(fields, StringComparer.Ordinal);
        foreach (JsonProperty property in value.EnumerateObject())
        {
            if (!remaining.Remove(property.Name))
            {
                throw new ProductFixtureValidationException(
                    $"Unknown or duplicate property '{property.Name}' at {path}.");
            }
        }
        if (remaining.Count != 0)
        {
            throw new ProductFixtureValidationException($"Missing property at {path}.");
        }
    }

    private static void Positive(long value, string label)
    {
        if (value <= 0)
        {
            throw new ProductFixtureValidationException($"{label} must be positive.");
        }
    }

    private static void NonBlank(string value, string label)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ProductFixtureValidationException($"{label} cannot be blank.");
        }
    }

    private static void UniqueIds(IEnumerable<string> values)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (string id in values)
        {
            NonBlank(id, "stable ID");
            if (!ids.Add(id))
            {
                throw new ProductFixtureValidationException($"Stable ID '{id}' is duplicated.");
            }
        }
    }
}
