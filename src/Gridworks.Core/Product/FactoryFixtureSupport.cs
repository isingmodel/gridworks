using System.Globalization;
using System.Text.Json;

namespace Gridworks.Core.Product;

internal static class FactoryFixtureSupport
{
    internal const string SchemaVersion = "gridworks.product.factory.v1";
    private const string FixtureId = "FACTORY_CAPACITY_V1";
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
        "factorySettlementMinutes",
        "factory",
        "gasPlantProject",
        "gasPlantSites",
        "plantConnectionLineProject",
    ];
    private static readonly string[] PointFields = ["x", "y"];
    private static readonly string[] FactoryFields =
        ["id", "terminalId", "position", "demandKw", "priority", "feederRatingKw"];
    private static readonly string[] GasPlantFields =
    [
        "projectId",
        "assetId",
        "terminalId",
        "capacityKw",
        "baseCostCashUnit",
        "buildMinutes",
        "variableGenerationCostCashUnitPerGWh",
    ];
    private static readonly string[] SiteFields = ["siteId", "position", "siteCostCashUnit"];
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

    internal static ProductFixture Read(JsonElement root)
    {
        ProductFixture cumulative = SecondHeartFixtureSupport.ReadCumulative(root, RootFields);
        return cumulative with
        {
            FactorySettlementMinutes = Int32(
                root.GetProperty("factorySettlementMinutes"),
                "$.factorySettlementMinutes"),
            Factory = ReadFactory(root.GetProperty("factory"), "$.factory"),
            GasPlantProject = ReadGasPlant(
                root.GetProperty("gasPlantProject"),
                "$.gasPlantProject"),
            GasPlantSites = ReadSites(root.GetProperty("gasPlantSites"), "$.gasPlantSites"),
            PlantConnectionLineProject = ReadLine(
                root.GetProperty("plantConnectionLineProject"),
                "$.plantConnectionLineProject"),
        };
    }

    internal static void Validate(ProductFixture fixture)
    {
        if (fixture.SchemaVersion != SchemaVersion || fixture.FixtureId != FixtureId)
        {
            throw new ProductFixtureValidationException(
                "Factory Capacity schemaVersion and fixtureId must match the supported pair.");
        }

        ProductFixture secondHeartShape = fixture with
        {
            SchemaVersion = SecondHeartFixtureSupport.SchemaVersion,
            FixtureId = "SECOND_HEART_V1",
            FactorySettlementMinutes = null,
            Factory = null,
            GasPlantProject = null,
            GasPlantSites = null,
            PlantConnectionLineProject = null,
        };
        SecondHeartFixtureSupport.Validate(secondHeartShape);

        int settlementMinutes = fixture.FactorySettlementMinutes
            ?? throw new ProductFixtureValidationException(
                "factorySettlementMinutes cannot be null.");
        ProductFactory factory = fixture.Factory
            ?? throw new ProductFixtureValidationException("factory cannot be null.");
        ProductGasPlantProjectDefinition plant = fixture.GasPlantProject
            ?? throw new ProductFixtureValidationException("gasPlantProject cannot be null.");
        IReadOnlyList<ProductGasPlantSite> sites = fixture.GasPlantSites
            ?? throw new ProductFixtureValidationException("gasPlantSites cannot be null.");
        ProductLineProjectDefinition connection = fixture.PlantConnectionLineProject
            ?? throw new ProductFixtureValidationException(
                "plantConnectionLineProject cannot be null.");

        Positive(settlementMinutes, "factorySettlementMinutes");
        ValidateFactoryPosition(fixture, factory);
        Positive(factory.DemandKw, "factory.demandKw");
        Positive(factory.FeederRatingKw, "factory.feederRatingKw");
        if (factory.Priority <= fixture.Town.Priority)
        {
            throw new ProductFixtureValidationException(
                "Factory priority must be greater than town priority.");
        }

        Positive(plant.CapacityKw, "gasPlantProject.capacityKw");
        Positive(plant.BaseCostCashUnit, "gasPlantProject.baseCostCashUnit");
        Positive(plant.BuildMinutes, "gasPlantProject.buildMinutes");
        Positive(
            plant.VariableGenerationCostCashUnitPerGWh,
            "gasPlantProject.variableGenerationCostCashUnitPerGWh");

        if (sites.Count != 2 || sites.Any(site => site is null))
        {
            throw new ProductFixtureValidationException(
                "gasPlantSites must contain exactly two sites.");
        }
        HashSet<ProductPoint> sitePositions = [];
        foreach (ProductGasPlantSite site in sites)
        {
            NonBlank(site.SiteId, "gas plant siteId");
            Positive(site.SiteCostCashUnit, "gas plant siteCostCashUnit");
            if (!ProductFixtureLoader.Contains(fixture.MapBounds, site.Position) ||
                fixture.BlockedCells.Contains(site.Position) ||
                site.Position == fixture.ExistingSource.Position ||
                site.Position == fixture.Town.Position ||
                site.Position == fixture.Hospital!.Position ||
                site.Position == factory.Position)
            {
                throw new ProductFixtureValidationException(
                    "Each gas plant site must be a distinct, empty map cell.");
            }
            if (!sitePositions.Add(site.Position))
            {
                throw new ProductFixtureValidationException(
                    "Gas plant site positions must be unique.");
            }
        }

        if (connection.FromTerminalId != plant.TerminalId ||
            connection.ToTerminalId != fixture.ExistingSource.TerminalId)
        {
            throw new ProductFixtureValidationException(
                "Plant connection must run from the gas plant terminal to the existing source terminal.");
        }
        Positive(connection.RatingKw, "plant connection ratingKw");
        Positive(connection.MaxSpanGridUnit, "plant connection maxSpanGridUnit");
        Positive(connection.SupportCostCashUnit, "plant connection supportCostCashUnit");
        Positive(connection.SpanCostCashUnit, "plant connection spanCostCashUnit");
        Positive(connection.SupportBuildMinutes, "plant connection supportBuildMinutes");
        Positive(connection.SpanBuildMinutes, "plant connection spanBuildMinutes");

        UniqueIds(
        [
            fixture.ExistingSource.AssetId,
            fixture.ExistingSource.TerminalId,
            fixture.Town.Id,
            fixture.SubstationProject.ProjectId,
            fixture.SubstationProject.AssetId,
            fixture.SubstationProject.TerminalId,
            fixture.LineProject.ProjectId,
            fixture.Hospital!.Id,
            fixture.Hospital.PrimaryTerminalId,
            fixture.Hospital.BackupTerminalId,
            .. fixture.HospitalLineProjects!.Select(line => line.ProjectId),
            fixture.SpatialIncident!.Id,
            factory.Id,
            factory.TerminalId,
            plant.ProjectId,
            plant.AssetId,
            plant.TerminalId,
            .. sites.Select(site => site.SiteId),
            connection.ProjectId,
        ]);

        ValidateArithmetic(fixture, factory, plant, sites, connection, settlementMinutes);
    }

    private static void ValidateFactoryPosition(ProductFixture fixture, ProductFactory factory)
    {
        if (!ProductFixtureLoader.Contains(fixture.MapBounds, factory.Position) ||
            fixture.BlockedCells.Contains(factory.Position) ||
            factory.Position == fixture.ExistingSource.Position ||
            factory.Position == fixture.Town.Position ||
            factory.Position == fixture.Hospital!.Position)
        {
            throw new ProductFixtureValidationException(
                "Factory position must be a distinct, buildable map cell.");
        }
    }

    private static void ValidateArithmetic(
        ProductFixture fixture,
        ProductFactory factory,
        ProductGasPlantProjectDefinition plant,
        IReadOnlyList<ProductGasPlantSite> sites,
        ProductLineProjectDefinition connection,
        int settlementMinutes)
    {
        try
        {
            long width = (long)fixture.MapBounds.MaxX - fixture.MapBounds.MinX;
            long height = (long)fixture.MapBounds.MaxY - fixture.MapBounds.MinY;
            long cellCount = checked(checked(width + 1) * checked(height + 1));
            long maximumSpans = checked(cellCount + 1);
            long maximumConnectionCost = checked(
                checked(cellCount * connection.SupportCostCashUnit) +
                checked(maximumSpans * connection.SpanCostCashUnit));
            long maximumConnectionMinutes = checked(
                checked(cellCount * connection.SupportBuildMinutes) +
                checked(maximumSpans * connection.SpanBuildMinutes));
            _ = checked((long)connection.MaxSpanGridUnit * connection.MaxSpanGridUnit);
            _ = checked(plant.BaseCostCashUnit + sites.Max(site => site.SiteCostCashUnit));
            _ = checked(fixture.Economy.InitialCash - maximumConnectionCost);
            _ = checked(
                checked((long)fixture.InitialMinute + plant.BuildMinutes) +
                checked(maximumConnectionMinutes + settlementMinutes));

            long[] demands =
                [fixture.Hospital!.DemandKw, fixture.Town.DemandKw, factory.DemandKw];
            long[] rates =
            [
                fixture.Economy.SaleRateCashUnitPerGWh,
                fixture.HospitalEconomy!.VariableGenerationCostCashUnitPerGWh,
                plant.VariableGenerationCostCashUnitPerGWh,
                fixture.HospitalEconomy.UnservedCompensationCashUnitPerGWh,
                fixture.HospitalEconomy.LostSalesCashUnitPerGWh,
            ];
            for (int mask = 0; mask < 1 << demands.Length; mask++)
            {
                long demand = 0;
                for (int index = 0; index < demands.Length; index++)
                {
                    if ((mask & 1 << index) != 0)
                    {
                        demand = checked(demand + demands[index]);
                    }
                }
                long energy = checked(demand * settlementMinutes);
                foreach (long rate in rates)
                {
                    ExactCash(energy, rate);
                }
            }
        }
        catch (ProductFixtureValidationException)
        {
            throw;
        }
        catch (OverflowException exception)
        {
            throw new ProductFixtureValidationException(
                "Factory Capacity arithmetic exceeds the supported range.",
                exception);
        }
    }

    private static ProductFactory ReadFactory(JsonElement value, string path)
    {
        Exact(value, FactoryFields, path);
        return new ProductFactory(
            Text(value, "id", path), Text(value, "terminalId", path),
            ReadPoint(value.GetProperty("position"), $"{path}.position"),
            Int64(value.GetProperty("demandKw"), $"{path}.demandKw"),
            Int32(value.GetProperty("priority"), $"{path}.priority"),
            Int64(value.GetProperty("feederRatingKw"), $"{path}.feederRatingKw"));
    }

    private static ProductGasPlantProjectDefinition ReadGasPlant(JsonElement value, string path)
    {
        Exact(value, GasPlantFields, path);
        return new ProductGasPlantProjectDefinition(
            Text(value, "projectId", path), Text(value, "assetId", path),
            Text(value, "terminalId", path),
            Int64(value.GetProperty("capacityKw"), $"{path}.capacityKw"),
            Int64(value.GetProperty("baseCostCashUnit"), $"{path}.baseCostCashUnit"),
            Int32(value.GetProperty("buildMinutes"), $"{path}.buildMinutes"),
            Int64(
                value.GetProperty("variableGenerationCostCashUnitPerGWh"),
                $"{path}.variableGenerationCostCashUnitPerGWh"));
    }

    private static IReadOnlyList<ProductGasPlantSite> ReadSites(JsonElement value, string path)
    {
        if (value.ValueKind != JsonValueKind.Array)
        {
            throw new ProductFixtureValidationException($"{path} must be an array.");
        }
        List<ProductGasPlantSite> sites = [];
        int index = 0;
        foreach (JsonElement item in value.EnumerateArray())
        {
            string itemPath = $"{path}[{index++}]";
            Exact(item, SiteFields, itemPath);
            sites.Add(new ProductGasPlantSite(
                Text(item, "siteId", itemPath),
                ReadPoint(item.GetProperty("position"), $"{itemPath}.position"),
                Int64(item.GetProperty("siteCostCashUnit"), $"{itemPath}.siteCostCashUnit")));
        }
        return Array.AsReadOnly(sites.ToArray());
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

    private static void ExactCash(long energyKwMinute, long rateCashUnitPerGWh)
    {
        long numerator = checked(energyKwMinute * rateCashUnitPerGWh);
        if (numerator % EnergyPerGWh != 0)
        {
            throw new ProductFixtureValidationException(
                "Factory Capacity rate calculation must divide exactly into CashUnit.");
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
