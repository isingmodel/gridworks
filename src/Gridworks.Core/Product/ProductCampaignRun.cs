namespace Gridworks.Core.Product;

public sealed class ProductCampaignRun
{
    private const int MaximumCommandCount = 10_000;

    private readonly ProductCampaignDefinition _definition;
    private readonly ProductFixture _fixture;
    private readonly string _campaignRootSha256;
    private readonly string _fixtureSha256;
    private readonly List<ProductCampaignCommand> _commands = [];
    private ProductSession _session;
    private int _currentChapterIndex;
    private int _chapterStartCommandCount;

    public ProductCampaignRun(
        ProductCampaignDefinition definition,
        ProductFixture fixture,
        string campaignRootSha256,
        string fixtureSha256)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(fixture);
        ProductCampaignLoader.Validate(definition);
        ProductFixtureLoader.Validate(fixture);
        if (!string.Equals(
                fixture.SchemaVersion,
                HeatwaveFixtureSupport.SchemaVersion,
                StringComparison.Ordinal))
        {
            throw new ProductCampaignValidationException(
                "The current campaign requires the Heatwave Maintenance scenario fixture.");
        }
        ValidateSha256(campaignRootSha256, nameof(campaignRootSha256));
        ValidateSha256(fixtureSha256, nameof(fixtureSha256));

        _definition = definition with
        {
            Chapters = Array.AsReadOnly(definition.Chapters.ToArray()),
        };
        _fixture = fixture;
        _campaignRootSha256 = campaignRootSha256;
        _fixtureSha256 = fixtureSha256;
        _session = new ProductSession(fixture);
    }

    public string CurrentChapterId =>
        _definition.Chapters[_currentChapterIndex].ChapterId;

    public int ChapterStartCommandCount => _chapterStartCommandCount;

    public int CommandCount => _commands.Count;

    public ProductSnapshot GetSnapshot() => _session.GetSnapshot();

    public ProductSubstationPlacementPreview PreviewSubstationPlacement(ProductPoint position) =>
        _session.PreviewSubstationPlacement(position);

    public ProductPlantPlacementPreview PreviewPlantPlacement(ProductPoint position) =>
        _session.PreviewPlantPlacement(position);

    public ProductLineSupportPreview PreviewLineSupport(ProductPoint position) =>
        _session.PreviewLineSupport(position);

    public ProductOrderPreview PreviewSubstationOrder() =>
        _session.PreviewSubstationOrder();

    public ProductOrderPreview PreviewLineOrder() =>
        _session.PreviewLineOrder();

    public ProductReliabilitySnapshot PreviewReliability() =>
        _session.PreviewReliability();

    public ProductOrderPreview PreviewPlantOrder() =>
        _session.PreviewPlantOrder();

    public ProductOrderPreview PreviewPreventiveMaintenanceOrder() =>
        _session.PreviewPreventiveMaintenanceOrder();

    public ProductCommandResult Execute(ProductCampaignCommand command)
    {
        ArgumentNullException.ThrowIfNull(command);
        ValidateCommandShape(command);
        if (_commands.Count >= MaximumCommandCount)
        {
            throw new InvalidOperationException(
                $"Campaign command count cannot exceed {MaximumCommandCount}.");
        }

        ProductCommandResult result = Dispatch(command);
        if (result.Accepted)
        {
            _commands.Add(command);
            UpdateChapterBoundary(result.Snapshot);
        }
        return result;
    }

    public ProductCommandResult RestartChapter()
    {
        ProductCampaignCommand[] prefix = _commands
            .Take(_chapterStartCommandCount)
            .ToArray();
        Replay(prefix);
        return new ProductCommandResult(true, null, _session.GetSnapshot());
    }

    public ProductCampaignSave CaptureSave() => new(
        ProductCampaignSave.SupportedSchemaVersion,
        _definition.CampaignId,
        _campaignRootSha256,
        _fixture.FixtureId,
        _fixtureSha256,
        Array.AsReadOnly(_commands.ToArray()));

    public static ProductCampaignRun Restore(
        ProductCampaignDefinition definition,
        ProductFixture fixture,
        string campaignRootSha256,
        string fixtureSha256,
        ProductCampaignSave save)
    {
        ArgumentNullException.ThrowIfNull(definition);
        ArgumentNullException.ThrowIfNull(fixture);
        ArgumentNullException.ThrowIfNull(save);
        ArgumentNullException.ThrowIfNull(save.Commands);
        if (!string.Equals(
                save.SchemaVersion,
                ProductCampaignSave.SupportedSchemaVersion,
                StringComparison.Ordinal))
        {
            throw new ProductPersistenceValidationException(
                $"Unsupported campaign save schemaVersion '{save.SchemaVersion}'.");
        }
        if (!string.Equals(save.CampaignId, definition.CampaignId, StringComparison.Ordinal) ||
            !string.Equals(save.CampaignRootSha256, campaignRootSha256, StringComparison.Ordinal) ||
            !string.Equals(save.FixtureId, fixture.FixtureId, StringComparison.Ordinal) ||
            !string.Equals(save.FixtureSha256, fixtureSha256, StringComparison.Ordinal))
        {
            throw new ProductPersistenceValidationException(
                "Campaign save identity does not match the current campaign and fixture.");
        }
        if (save.Commands.Count > MaximumCommandCount)
        {
            throw new ProductPersistenceValidationException(
                $"Campaign save contains more than {MaximumCommandCount} commands.");
        }

        ProductCampaignRun run = new(
            definition,
            fixture,
            campaignRootSha256,
            fixtureSha256);
        run.Replay(save.Commands);
        return run;
    }

    private void Replay(IReadOnlyList<ProductCampaignCommand> commands)
    {
        _session = new ProductSession(_fixture);
        _commands.Clear();
        _currentChapterIndex = 0;
        _chapterStartCommandCount = 0;
        for (int index = 0; index < commands.Count; index++)
        {
            ProductCampaignCommand command = commands[index]
                ?? throw new ProductPersistenceValidationException(
                    $"Campaign command {index} cannot be null.");
            try
            {
                ProductCommandResult result = Execute(command);
                if (!result.Accepted)
                {
                    throw new ProductPersistenceValidationException(
                        $"Campaign command {index} was rejected with {result.Error}.");
                }
            }
            catch (ProductPersistenceValidationException)
            {
                throw;
            }
            catch (Exception exception) when (
                exception is ArgumentException or InvalidOperationException or OverflowException)
            {
                throw new ProductPersistenceValidationException(
                    $"Campaign command {index} could not be replayed.",
                    exception);
            }
        }
    }

    private ProductCommandResult Dispatch(ProductCampaignCommand command) => command.Kind switch
    {
        ProductCampaignCommandKind.SetSubstationDraft =>
            _session.SetSubstationDraft(command.Position!),
        ProductCampaignCommandKind.CancelSubstationDraft =>
            _session.CancelSubstationDraft(),
        ProductCampaignCommandKind.AddLineSupport =>
            _session.AddLineSupport(command.Position!),
        ProductCampaignCommandKind.UndoLineSupport =>
            _session.UndoLineSupport(),
        ProductCampaignCommandKind.CancelLineDraft =>
            _session.CancelLineDraft(),
        ProductCampaignCommandKind.OrderSubstation =>
            _session.OrderSubstation(),
        ProductCampaignCommandKind.OrderLine =>
            _session.OrderLine(),
        ProductCampaignCommandKind.AdvanceToConstructionCompletion =>
            _session.AdvanceToConstructionCompletion(),
        ProductCampaignCommandKind.AdvanceToSettlement =>
            _session.AdvanceToSettlement(),
        ProductCampaignCommandKind.AdvanceToIncident =>
            _session.AdvanceToIncident(),
        ProductCampaignCommandKind.AdvanceToRecoveryAndSettlement =>
            _session.AdvanceToRecoveryAndSettlement(),
        ProductCampaignCommandKind.SetPlantDraft =>
            _session.SetPlantDraft(command.Position!),
        ProductCampaignCommandKind.CancelPlantDraft =>
            _session.CancelPlantDraft(),
        ProductCampaignCommandKind.OrderPlant =>
            _session.OrderPlant(),
        ProductCampaignCommandKind.AdvanceToFactorySettlement =>
            _session.AdvanceToFactorySettlement(),
        ProductCampaignCommandKind.OrderPreventiveMaintenance =>
            _session.OrderPreventiveMaintenance(),
        ProductCampaignCommandKind.SkipPreventiveMaintenance =>
            _session.SkipPreventiveMaintenance(),
        ProductCampaignCommandKind.AdvanceToHeatwave =>
            _session.AdvanceToHeatwave(),
        ProductCampaignCommandKind.AdvanceToHeatwaveSettlement =>
            _session.AdvanceToHeatwaveSettlement(),
        _ => throw new ArgumentOutOfRangeException(nameof(command)),
    };

    private void UpdateChapterBoundary(ProductSnapshot snapshot)
    {
        if (_currentChapterIndex == 0 && snapshot.Phase == ProductPhase.PrimaryPlanning)
        {
            _currentChapterIndex = 1;
            _chapterStartCommandCount = _commands.Count;
        }
        else if (_currentChapterIndex == 1 && snapshot.Phase == ProductPhase.PlantPlanning)
        {
            _currentChapterIndex = 2;
            _chapterStartCommandCount = _commands.Count;
        }
    }

    private static void ValidateCommandShape(ProductCampaignCommand command)
    {
        bool requiresPosition = command.Kind is
            ProductCampaignCommandKind.SetSubstationDraft or
            ProductCampaignCommandKind.AddLineSupport or
            ProductCampaignCommandKind.SetPlantDraft;
        if (requiresPosition != (command.Position is not null))
        {
            throw new ArgumentException(
                requiresPosition
                    ? $"{command.Kind} requires one position."
                    : $"{command.Kind} does not accept a position.",
                nameof(command));
        }
        if (!Enum.IsDefined(command.Kind))
        {
            throw new ArgumentOutOfRangeException(nameof(command));
        }
    }

    private static void ValidateSha256(string value, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(value, parameterName);
        if (value.Length != 64 || value.Any(character =>
                character is not (>= '0' and <= '9') and
                not (>= 'a' and <= 'f')))
        {
            throw new ArgumentException(
                "SHA-256 must be 64 lowercase hexadecimal characters.",
                parameterName);
        }
    }
}
