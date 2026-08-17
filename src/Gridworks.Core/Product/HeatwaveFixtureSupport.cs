using System.Globalization;
using System.Text.Json;

namespace Gridworks.Core.Product;

internal static class HeatwaveFixtureSupport
{
    internal const string SchemaVersion = "gridworks.product.heatwave.v1";
    private const string FixtureId = "HEATWAVE_MAINTENANCE_V1";
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
        "heatwave",
        "preventiveMaintenance",
    ];
    private static readonly string[] HeatwaveFields =
    [
        "id",
        "leadMinutes",
        "durationMinutes",
        "townDemandKw",
        "agedFactoryFeederId",
        "agedFactoryFeederHeatwaveRatingKw",
    ];
    private static readonly string[] MaintenanceFields =
        ["projectId", "targetAssetId", "costCashUnit", "buildMinutes"];

    internal static ProductFixture Read(JsonElement root)
    {
        ProductFixture cumulative = FactoryFixtureSupport.ReadCumulative(root, RootFields);
        JsonElement heatwave = root.GetProperty("heatwave");
        Exact(heatwave, HeatwaveFields, "$.heatwave");
        JsonElement maintenance = root.GetProperty("preventiveMaintenance");
        Exact(maintenance, MaintenanceFields, "$.preventiveMaintenance");
        return cumulative with
        {
            Heatwave = new ProductHeatwaveDefinition(
                Text(heatwave, "id", "$.heatwave"),
                Int32(heatwave.GetProperty("leadMinutes"), "$.heatwave.leadMinutes"),
                Int32(heatwave.GetProperty("durationMinutes"), "$.heatwave.durationMinutes"),
                Int64(heatwave.GetProperty("townDemandKw"), "$.heatwave.townDemandKw"),
                Text(heatwave, "agedFactoryFeederId", "$.heatwave"),
                Int64(
                    heatwave.GetProperty("agedFactoryFeederHeatwaveRatingKw"),
                    "$.heatwave.agedFactoryFeederHeatwaveRatingKw")),
            PreventiveMaintenance = new ProductPreventiveMaintenanceDefinition(
                Text(maintenance, "projectId", "$.preventiveMaintenance"),
                Text(maintenance, "targetAssetId", "$.preventiveMaintenance"),
                Int64(
                    maintenance.GetProperty("costCashUnit"),
                    "$.preventiveMaintenance.costCashUnit"),
                Int32(
                    maintenance.GetProperty("buildMinutes"),
                    "$.preventiveMaintenance.buildMinutes")),
        };
    }

    internal static void Validate(ProductFixture fixture)
    {
        if (fixture.SchemaVersion != SchemaVersion || fixture.FixtureId != FixtureId)
        {
            throw new ProductFixtureValidationException(
                "Heatwave Maintenance schemaVersion and fixtureId must match the supported pair.");
        }

        ProductFixture factoryShape = fixture with
        {
            SchemaVersion = FactoryFixtureSupport.SchemaVersion,
            FixtureId = "FACTORY_CAPACITY_V1",
            Heatwave = null,
            PreventiveMaintenance = null,
        };
        FactoryFixtureSupport.Validate(factoryShape);

        ProductHeatwaveDefinition heatwave = fixture.Heatwave
            ?? throw new ProductFixtureValidationException("heatwave cannot be null.");
        ProductPreventiveMaintenanceDefinition maintenance = fixture.PreventiveMaintenance
            ?? throw new ProductFixtureValidationException(
                "preventiveMaintenance cannot be null.");
        ProductFactory factory = fixture.Factory!;

        Positive(heatwave.LeadMinutes, "heatwave.leadMinutes");
        Positive(heatwave.DurationMinutes, "heatwave.durationMinutes");
        if (heatwave.TownDemandKw <= fixture.Town.DemandKw)
        {
            throw new ProductFixtureValidationException(
                "heatwave.townDemandKw must exceed normal town demand.");
        }
        if (heatwave.AgedFactoryFeederHeatwaveRatingKw <= 0 ||
            heatwave.AgedFactoryFeederHeatwaveRatingKw >= factory.FeederRatingKw)
        {
            throw new ProductFixtureValidationException(
                "Heatwave feeder rating must be positive and below its normal rating.");
        }
        NonBlank(heatwave.Id, "heatwave.id");
        NonBlank(heatwave.AgedFactoryFeederId, "heatwave.agedFactoryFeederId");
        NonBlank(maintenance.ProjectId, "preventiveMaintenance.projectId");
        if (maintenance.TargetAssetId != heatwave.AgedFactoryFeederId)
        {
            throw new ProductFixtureValidationException(
                "Preventive maintenance must target the aged factory feeder.");
        }
        Positive(maintenance.CostCashUnit, "preventiveMaintenance.costCashUnit");
        Positive(maintenance.BuildMinutes, "preventiveMaintenance.buildMinutes");
        if (maintenance.BuildMinutes > heatwave.LeadMinutes)
        {
            throw new ProductFixtureValidationException(
                "Preventive maintenance must finish within the heatwave lead time.");
        }

        HashSet<string> ids = BaseIds(fixture).ToHashSet(StringComparer.Ordinal);
        foreach (string id in
            new[] { heatwave.Id, heatwave.AgedFactoryFeederId, maintenance.ProjectId })
        {
            if (!ids.Add(id))
            {
                throw new ProductFixtureValidationException($"Stable ID '{id}' is duplicated.");
            }
        }

        ValidateArithmetic(fixture, heatwave, maintenance);
    }

    private static IEnumerable<string> BaseIds(ProductFixture fixture)
    {
        yield return fixture.ExistingSource.AssetId;
        yield return fixture.ExistingSource.TerminalId;
        yield return fixture.Town.Id;
        yield return fixture.SubstationProject.ProjectId;
        yield return fixture.SubstationProject.AssetId;
        yield return fixture.SubstationProject.TerminalId;
        yield return fixture.LineProject.ProjectId;
        yield return fixture.Hospital!.Id;
        yield return fixture.Hospital.PrimaryTerminalId;
        yield return fixture.Hospital.BackupTerminalId;
        foreach (ProductHospitalLineProjectDefinition line in fixture.HospitalLineProjects!)
        {
            yield return line.ProjectId;
        }
        yield return fixture.SpatialIncident!.Id;
        yield return fixture.Factory!.Id;
        yield return fixture.Factory.TerminalId;
        yield return fixture.GasPlantProject!.ProjectId;
        yield return fixture.GasPlantProject.AssetId;
        yield return fixture.GasPlantProject.TerminalId;
        foreach (ProductGasPlantSite site in fixture.GasPlantSites!)
        {
            yield return site.SiteId;
        }
        yield return fixture.PlantConnectionLineProject!.ProjectId;
    }

    private static void ValidateArithmetic(
        ProductFixture fixture,
        ProductHeatwaveDefinition heatwave,
        ProductPreventiveMaintenanceDefinition maintenance)
    {
        try
        {
            _ = checked((long)fixture.InitialMinute + heatwave.LeadMinutes + heatwave.DurationMinutes);
            _ = checked(fixture.Economy.InitialCash - maintenance.CostCashUnit);
            long[] demands =
                [fixture.Hospital!.DemandKw, heatwave.TownDemandKw, fixture.Factory!.DemandKw];
            long[] rates =
            [
                fixture.Economy.SaleRateCashUnitPerGWh,
                fixture.HospitalEconomy!.VariableGenerationCostCashUnitPerGWh,
                fixture.GasPlantProject!.VariableGenerationCostCashUnitPerGWh,
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
                long energy = checked(demand * heatwave.DurationMinutes);
                foreach (long rate in rates)
                {
                    long numerator = checked(energy * rate);
                    if (numerator % EnergyPerGWh != 0)
                    {
                        throw new ProductFixtureValidationException(
                            "Heatwave rate calculation must divide exactly into CashUnit.");
                    }
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
                "Heatwave Maintenance arithmetic exceeds the supported range.",
                exception);
        }
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
}
