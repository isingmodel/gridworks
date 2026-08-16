using System.Collections.ObjectModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Gridworks.Core;

public static class FixtureLoader
{
    private const string SupportedSchemaVersion = "gridworks.scope0b.fixture.v1";
    private const string SupportedFixtureId = "S0B-FIXTURE-v1";

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        PropertyNameCaseInsensitive = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        AllowTrailingCommas = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        NumberHandling = JsonNumberHandling.Strict,
        MaxDepth = 64,
    };

    public static LoadedFixture Load(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        return Load(Encoding.UTF8.GetBytes(json));
    }

    public static LoadedFixture Load(ReadOnlySpan<byte> utf8Json)
    {
        try
        {
            using JsonDocument document = JsonDocument.Parse(
                utf8Json.ToArray(),
                new JsonDocumentOptions
                {
                    AllowTrailingCommas = false,
                    CommentHandling = JsonCommentHandling.Disallow,
                    MaxDepth = 64,
                });

            EnsureNoDuplicateProperties(document.RootElement, "$" );
            RawFixture raw = document.RootElement.Deserialize<RawFixture>(SerializerOptions)
                ?? throw new FixtureValidationException("Fixture root cannot be null.");

            ScenarioDefinition scenario = MapScenario(raw);
            PresentationDefinition presentation = MapPresentation(raw.Presentation);
            FixtureOracle oracle = MapOracle(raw.VerificationOnly);
            FixtureValidator.Validate(scenario, presentation, oracle);
            return new LoadedFixture(scenario, presentation, oracle);
        }
        catch (FixtureValidationException)
        {
            throw;
        }
        catch (Exception exception) when (
            exception is JsonException or NotSupportedException or OverflowException or NullReferenceException)
        {
            throw new FixtureValidationException("Fixture JSON is invalid.", exception);
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
                    throw new FixtureValidationException(
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

    private static ScenarioDefinition MapScenario(RawFixture raw)
    {
        if (!string.Equals(raw.SchemaVersion, SupportedSchemaVersion, StringComparison.Ordinal))
        {
            throw new FixtureValidationException(
                $"Unsupported schemaVersion '{raw.SchemaVersion}'.");
        }

        if (!string.Equals(raw.FixtureId, SupportedFixtureId, StringComparison.Ordinal))
        {
            throw new FixtureValidationException(
                $"Unsupported fixtureId '{raw.FixtureId}'.");
        }

        return new ScenarioDefinition(
            raw.SchemaVersion,
            raw.FixtureId,
            raw.DisplayName,
            new UnitsDefinition(
                raw.Units.Position,
                raw.Units.Power,
                raw.Units.Energy,
                raw.Units.Time,
                raw.Units.Cash,
                raw.Units.Rate),
            new CalendarDefinition(raw.Calendar.OriginLabel, raw.Calendar.MinutesPerDay),
            new EconomyDefinition(
                raw.Economy.InitialCash,
                raw.Economy.SaleRate,
                raw.Economy.GasVariableRate,
                raw.Economy.TownOutageRate,
                raw.Economy.HospitalOutageRate),
            Freeze(raw.Nodes.Select(MapNode)),
            Freeze(raw.Edges.Select(MapEdge)),
            Freeze(raw.Projects.Select(project => new ProjectDefinition(
                project.Id,
                project.EdgeId,
                project.CostCashUnit,
                project.AllowedOrderMinute,
                project.BuildMinutes))),
            Freeze(raw.Loads.Select(load => new LoadDefinition(
                load.NodeId,
                load.NoticeMinute,
                load.ActiveMinute,
                load.OutageRateKey))),
            Freeze(raw.Requirements.Select(requirement => new RequirementDefinition(
                requirement.Id,
                requirement.DeadlineMinute,
                Freeze(requirement.SatisfiedByAnyCommissionedEdgeId)))),
            Freeze(raw.PermittedSupplyPaths.Select(path => new SupplyPathDefinition(
                path.Id,
                path.LoadNodeId,
                ParsePathRole(path.Role),
                path.RequiredCommissionedEdgeId,
                Freeze(path.EdgeIds)))),
            Freeze(raw.EvaluationCases.Select(evaluationCase => new EvaluationCaseDefinition(
                evaluationCase.Id,
                ParseSelectorType(evaluationCase.SelectorType),
                evaluationCase.SelectorValue))),
            Freeze(raw.Events.Select(gameEvent => new EventDefinition(
                gameEvent.Id,
                gameEvent.StartMinute,
                gameEvent.EndMinute,
                gameEvent.EvaluationCaseId))),
            new HospitalInternalPowerDefinition(
                raw.HospitalInternalPower.LoadNodeId,
                raw.HospitalInternalPower.RatedPowerKw,
                Freeze(raw.HospitalInternalPower.Stages.Select(stage =>
                    new InternalPowerStageDefinition(stage.Id, stage.EnergyKwMinute)))),
            Freeze(raw.Milestones.Select(milestone =>
                new MilestoneDefinition(milestone.Minute, milestone.Label))));
    }

    private static NodeDefinition MapNode(RawNode node) => new(
        node.Id,
        ParseNodeKind(node.Kind),
        MapPosition(node.Position),
        node.MaxOutputKw,
        node.InitialOnline,
        node.InitialCommissioned,
        node.DemandKw,
        node.Priority is null ? null : ParseLoadPriority(node.Priority),
        node.ServiceSubstationId);

    private static EdgeDefinition MapEdge(RawEdge edge) => new(
        edge.Id,
        edge.FromNodeId,
        edge.ToNodeId,
        edge.RatingKw,
        edge.ElectricalContingencyId,
        edge.SpatialRiskGroup,
        ParseConstructionState(edge.InitialConstructionState));

    private static PresentationDefinition MapPresentation(RawPresentation raw) => new(
        new MapBoundsDefinition(raw.MapBounds.Width, raw.MapBounds.Height),
        Freeze(raw.ServiceAreas.Select(area => new ServiceAreaDefinition(
            area.SubstationId,
            area.Shape,
            MapPosition(area.Center),
            area.RadiusX,
            area.RadiusY))),
        Freeze(raw.RiskAreas.Select(area => new RiskAreaDefinition(
            area.Id,
            area.SpatialRiskGroup,
            Freeze(area.Polygon.Select(MapPosition))))),
        Freeze(raw.EdgePolylines.Select(polyline => new EdgePolylineDefinition(
            polyline.EdgeId,
            Freeze(polyline.Points.Select(MapPosition))))),
        Freeze(raw.LayoutVariants.Select(variant => new LayoutVariantDefinition(
            variant.Id,
            Freeze(variant.CorridorProjectOrder)))));

    private static FixtureOracle MapOracle(RawOracle raw) => new(
        new TopologyOracle(
            raw.Topology.NodeCount,
            raw.Topology.EdgeCount,
            raw.Topology.NormalDemandKw,
            raw.Topology.SharedTrunkRatingKw,
            raw.Topology.GeneratorRatingKw,
            raw.Topology.InitialTownServiceEligible,
            raw.Topology.InitialTownUtilityPathAvailable,
            raw.Topology.InitialHospitalUtilityPathAvailable),
        Freeze(raw.EvaluationOutcomes.Select(outcome => new EvaluationOutcomeOracle(
            ParseEvaluationDesign(outcome.Design),
            outcome.CaseId,
            FreezeSorted(outcome.RemovedEdgeIds),
            outcome.TownUtilityDelivered,
            outcome.HospitalUtilityDelivered,
            outcome.TownPathId,
            outcome.HospitalPathId))),
        new InternalPowerOracle(
            raw.InternalPower.UpsDurationMinutes,
            raw.InternalPower.DieselDurationMinutes,
            raw.InternalPower.TotalDurationMinutes,
            raw.InternalPower.RiverEventUsedKwMinute,
            raw.InternalPower.RiverEventRemainingKwMinute,
            raw.InternalPower.RiverEventHospitalP0UnservedKwMinute,
            raw.InternalPower.NorthEventUsedKwMinute,
            raw.InternalPower.NorthEventRemainingKwMinute,
            raw.InternalPower.NorthEventHospitalP0UnservedKwMinute),
        Freeze(raw.CommonBoundaryStates.Select(MapBoundaryOracle)),
        Freeze(raw.RouteBoundaryStates.Select(route => new RouteBoundaryOracle(
            ParseCorridorDesign(route.Design),
            Freeze(route.States.Select(MapBoundaryOracle))))),
        new CashOracle(
            raw.Cash.PreChoiceCash,
            raw.Cash.NormalPostChoiceNetCash,
            MapEventCashOracle(raw.Cash.RiverEvent),
            MapEventCashOracle(raw.Cash.NorthEvent)));

    private static BoundaryOracle MapBoundaryOracle(RawBoundaryState raw) =>
        new(raw.Id, MapSnapshot(raw));

    private static PublicSnapshot MapSnapshot(RawBoundaryState raw) => new(
        raw.Minute,
        raw.Cash,
        ParseProjectState(raw.TownProjectState),
        ParseProjectState(raw.CorridorProjectState),
        raw.SelectedCorridor is null ? null : ParseCorridorDesign(raw.SelectedCorridor),
        FreezeSorted(raw.CommissionedEdgeIds),
        FreezeSorted(raw.EventRemovedEdgeIds),
        FreezeSorted(raw.ActiveLoadIds),
        FreezeSorted(raw.UtilityPathByLoad),
        ParseInternalPowerStage(raw.HospitalInternalStage),
        raw.HospitalInternalRemainingKwMinute,
        MapSettlement(raw.Interval),
        MapSettlement(raw.Cumulative),
        raw.IsComplete);

    private static Settlement MapSettlement(RawSettlement raw) => new(
        raw.RevenueCashUnit,
        raw.GasCostCashUnit,
        raw.CompensationCashUnit,
        raw.LostSalesCashUnit,
        FreezeSorted(raw.UtilityDeliveredKwMinuteByLoad),
        FreezeSorted(raw.UtilityUnservedKwMinuteByLoad),
        raw.GasInjectionKwMinute,
        raw.HospitalInternalUsedKwMinute,
        raw.HospitalP0UnservedKwMinute);

    private static EventCashOracle MapEventCashOracle(RawEventCashOracle raw) => new(
        raw.UtilityDeliveredKwMinute,
        raw.TownUtilityUnservedKwMinute,
        raw.HospitalUtilityUnservedKwMinute,
        raw.RevenueCashUnit,
        raw.LostSalesCashUnit,
        raw.CompensationCashUnit,
        raw.GasCostCashUnit,
        raw.EventCashDelta,
        raw.EndingCash);

    private static Position MapPosition(RawPosition position) => new(position.X, position.Y);

    private static NodeKind ParseNodeKind(string value) => value switch
    {
        "generator" => NodeKind.Generator,
        "bus" => NodeKind.Bus,
        "substation" => NodeKind.Substation,
        "load" => NodeKind.Load,
        _ => throw InvalidEnum("node kind", value),
    };

    private static LoadPriority ParseLoadPriority(string value) => value switch
    {
        "P0" => LoadPriority.P0,
        "P2" => LoadPriority.P2,
        _ => throw InvalidEnum("load priority", value),
    };

    private static ConstructionState ParseConstructionState(string value) => value switch
    {
        "not_ordered" => ConstructionState.NotOrdered,
        "commissioned" => ConstructionState.Commissioned,
        _ => throw InvalidEnum("construction state", value),
    };

    private static ProjectState ParseProjectState(string value) => value switch
    {
        "not_ordered" => ProjectState.NotOrdered,
        "building" => ProjectState.Building,
        "commissioned" => ProjectState.Commissioned,
        _ => throw InvalidEnum("project state", value),
    };

    private static PathRole ParsePathRole(string value) => value switch
    {
        "primary" => PathRole.Primary,
        "backup" => PathRole.Backup,
        _ => throw InvalidEnum("path role", value),
    };

    private static SelectorType ParseSelectorType(string value) => value switch
    {
        "electricalContingencyId" => SelectorType.ElectricalContingencyId,
        "spatialRiskGroup" => SelectorType.SpatialRiskGroup,
        _ => throw InvalidEnum("selector type", value),
    };

    private static CorridorDesign ParseCorridorDesign(string value) => value switch
    {
        "RIVER_PARALLEL" => CorridorDesign.RiverParallel,
        "NORTH_DETOUR" => CorridorDesign.NorthDetour,
        _ => throw InvalidEnum("corridor design", value),
    };

    private static EvaluationDesign ParseEvaluationDesign(string value) => value switch
    {
        "NO_BUILD" => EvaluationDesign.NoBuild,
        "RIVER_PARALLEL" => EvaluationDesign.RiverParallel,
        "NORTH_DETOUR" => EvaluationDesign.NorthDetour,
        _ => throw InvalidEnum("evaluation design", value),
    };

    private static InternalPowerStage ParseInternalPowerStage(string value) => value switch
    {
        "none" => InternalPowerStage.None,
        "ups" => InternalPowerStage.Ups,
        "diesel" => InternalPowerStage.Diesel,
        _ => throw InvalidEnum("internal-power stage", value),
    };

    private static FixtureValidationException InvalidEnum(string field, string value) =>
        new($"Unknown {field} value '{value}'.");

    private static IReadOnlyList<T> Freeze<T>(IEnumerable<T> source) =>
        Array.AsReadOnly(source.ToArray());

    private static IReadOnlyList<string> FreezeSorted(IEnumerable<string> source) =>
        Array.AsReadOnly(source.OrderBy(value => value, StringComparer.Ordinal).ToArray());

    private static IReadOnlyDictionary<string, TValue> FreezeSorted<TValue>(
        IEnumerable<KeyValuePair<string, TValue>> source)
    {
        SortedDictionary<string, TValue> sorted = new(StringComparer.Ordinal);
        foreach ((string key, TValue value) in source)
        {
            if (!sorted.TryAdd(key, value))
            {
                throw new FixtureValidationException($"Duplicate dictionary key '{key}'.");
            }
        }

        return new ReadOnlyDictionary<string, TValue>(sorted);
    }
}
