using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Gridworks.Core.Product;
using Godot;

namespace Gridworks.Game;

public sealed partial class ProductMain : Control
{
    private ProductLaunchOptions _options = null!;
    private ProductDiagnosticLog? _diagnostic;
    private ProductFixture _fixture = null!;
    private ProductHospital _hospitalFixture = null!;
    private ProductSpatialIncident _incidentFixture = null!;
    private ProductFactory _factoryFixture = null!;
    private ProductGasPlantProjectDefinition _plantFixture = null!;
    private ProductHeatwaveDefinition _heatwaveFixture = null!;
    private ProductPreventiveMaintenanceDefinition _maintenanceFixture = null!;
    private ProductCampaignDefinition _campaign = null!;
    private ProductCampaignRun _run = null!;
    private ProductCampaignRun? _continuation;
    private ProductSettings _settings = ProductSettings.Default;
    private ProductSnapshot _snapshot = null!;
    private FirstLightPointerPreview? _pointerPreview;
    private string _fixtureHash = string.Empty;
    private string _campaignHash = string.Empty;
    private string _buildHash = string.Empty;
    private string _campaignSavePath = string.Empty;
    private string _settingsPath = string.Empty;
    private string _titleStatus = string.Empty;
    private string _lastError = string.Empty;
    private bool _persistenceEnabled;
    private bool _finalLogged;

    private Label _phaseLabel = null!;
    private Label _timeLabel = null!;
    private Label _cashLabel = null!;
    private Label _demandLabel = null!;
    private FirstLightMapView _mapView = null!;
    private FirstLightTaskPanel _taskPanel = null!;
    private Button _menuButton = null!;
    private ProductShellOverlay _shell = null!;

    public override void _Ready()
    {
        try
        {
            GetWindow().Title = "Gridworks — 열돔 아래: 폭염과 정비";
            _options = ProductLaunchOptions.Parse(OS.GetCmdlineUserArgs());

            string dataDirectory = Path.GetFullPath(Path.Combine(
                ProjectSettings.GlobalizePath("res://"),
                "..",
                "data"));
            string campaignPath = Path.Combine(dataDirectory, "product-campaign-v1.json");
            byte[] campaignBytes = File.ReadAllBytes(campaignPath);
            _campaignHash = ProductContentHash.ComputeSha256(campaignBytes);
            _campaign = ProductCampaignLoader.Load(campaignBytes);
            string fixturePath = Path.GetFullPath(Path.Combine(
                dataDirectory,
                _campaign.ScenarioFixture));
            byte[] fixtureBytes = File.ReadAllBytes(fixturePath);
            _fixtureHash = ProductContentHash.ComputeSha256(fixtureBytes);
            _buildHash = ComputeBuildHash();
            _fixture = ProductFixtureLoader.Load(fixtureBytes);
            _hospitalFixture = _fixture.Hospital
                ?? throw new InvalidOperationException("Second Heart fixture is missing the hospital.");
            _incidentFixture = _fixture.SpatialIncident
                ?? throw new InvalidOperationException("Factory fixture is missing the spatial incident.");
            _factoryFixture = _fixture.Factory
                ?? throw new InvalidOperationException("Factory fixture is missing the factory.");
            _plantFixture = _fixture.GasPlantProject
                ?? throw new InvalidOperationException("Factory fixture is missing the gas plant.");
            _heatwaveFixture = _fixture.Heatwave
                ?? throw new InvalidOperationException("Heatwave fixture is missing the heatwave.");
            _maintenanceFixture = _fixture.PreventiveMaintenance
                ?? throw new InvalidOperationException("Heatwave fixture is missing preventive maintenance.");
            _run = new ProductCampaignRun(
                _campaign,
                _fixture,
                _campaignHash,
                _fixtureHash);
            _snapshot = _run.GetSnapshot();

            _persistenceEnabled = !_options.Smoke;
            ConfigurePersistencePaths();
            if (_persistenceEnabled)
            {
                LoadPersistentState();
            }

            BindScene();
            ApplySettings(_settings);
            _diagnostic = new ProductDiagnosticLog(_options.DiagnosticPath, _options.SessionId);
            Render();
            _diagnostic.WriteReady(new
            {
                buildHash = _buildHash,
                campaignHash = _campaignHash,
                fixtureHash = _fixtureHash,
                chapterId = _run.CurrentChapterId,
                phase = Machine(_snapshot.Phase),
            });

            if (_options.Smoke)
            {
                _shell.HideShell();
                CallDeferred(nameof(RunSmoke));
            }
            else
            {
                _shell.ShowTitle(_continuation is not null, _titleStatus);
                if (_options.ShellSmokeLeg != ProductShellSmokeLeg.None)
                {
                    CallDeferred(nameof(RunShellSmoke));
                }
            }
        }
        catch (Exception exception)
        {
            GD.PushError($"Heatwave Maintenance startup failed: {exception}");
            ShowFatalError(exception.Message);
            if (_options?.Automated == true ||
                OS.GetCmdlineUserArgs().Contains("--smoke") ||
                OS.GetCmdlineUserArgs().Contains("--shell-smoke"))
            {
                GetTree().Quit(1);
            }
        }
    }

    public override void _ExitTree()
    {
        _diagnostic?.Dispose();
        _diagnostic = null;
    }

    private void BindScene()
    {
        _phaseLabel = GetNode<Label>("%PhaseLabel");
        _timeLabel = GetNode<Label>("%TimeLabel");
        _cashLabel = GetNode<Label>("%CashLabel");
        _demandLabel = GetNode<Label>("%DemandLabel");
        _mapView = GetNode<FirstLightMapView>("%FirstLightMapView");
        _taskPanel = GetNode<FirstLightTaskPanel>("%FirstLightTaskPanel");
        _menuButton = GetNode<Button>("%MenuButton");
        _shell = GetNode<ProductShellOverlay>("%ProductShellOverlay");

        _mapView.PointerChanged += OnPointerChanged;
        _mapView.PointRequested += OnPointRequested;
        _taskPanel.CancelDraftRequested += OnCancelDraftRequested;
        _taskPanel.UndoRequested += OnUndoRequested;
        _taskPanel.OrderRequested += OnOrderRequested;
        _taskPanel.AdvanceRequested += OnAdvanceRequested;
        _taskPanel.SettleRequested += OnMilestoneRequested;
        _menuButton.Pressed += OnPauseRequested;
        _shell.PauseRequested += OnPauseRequested;
        _shell.NewGameRequested += OnNewGameRequested;
        _shell.ContinueRequested += OnContinueRequested;
        _shell.ResumeRequested += _shell.HideShell;
        _shell.SaveAndQuitRequested += OnSaveAndQuitRequested;
        _shell.RestartChapterRequested += OnRestartChapterRequested;
        _shell.FullscreenChanged += OnFullscreenChanged;
        _shell.UiScalePercentChanged += OnUiScalePercentChanged;
        _shell.ControlHelpChanged += OnControlHelpChanged;
        _shell.GameplayFocusRequested += OnGameplayFocusRequested;
    }

    private void OnGameplayFocusRequested() =>
        _mapView.CallDeferred(Control.MethodName.GrabFocus);

    private void OnPauseRequested() =>
        _shell.ShowPause(CurrentChapterDisplayName());

    private void OnNewGameRequested()
    {
        _continuation = null;
        _run = new ProductCampaignRun(
            _campaign,
            _fixture,
            _campaignHash,
            _fixtureHash);
        _snapshot = _run.GetSnapshot();
        _pointerPreview = null;
        _lastError = string.Empty;
        _finalLogged = false;
        TrySaveCampaign(out _lastError);
        Render();
        _diagnostic?.WriteLifecycle("NEW_GAME", CampaignDiagnosticPayload());
        EnterGameplay();
    }

    private void OnContinueRequested()
    {
        if (_continuation is null)
        {
            _shell.ShowTitle(false, "이어할 수 있는 유효한 저장이 없습니다.");
            return;
        }

        _run = _continuation;
        _continuation = null;
        _snapshot = _run.GetSnapshot();
        _pointerPreview = null;
        _lastError = string.Empty;
        _finalLogged = false;
        Render();
        _diagnostic?.WriteLifecycle("CONTINUE", CampaignDiagnosticPayload());
        EnterGameplay();
    }

    private void OnSaveAndQuitRequested()
    {
        if (!TrySaveCampaign(out string error))
        {
            _lastError = error;
            _shell.ShowPauseError(error);
            return;
        }

        _diagnostic?.WriteLifecycle("SAVE_AND_QUIT", CampaignDiagnosticPayload());
        if (_options.ShellSmokeLeg == ProductShellSmokeLeg.Save)
        {
            RequireSaveLegFinalState();
            GD.Print(
                $"PRODUCT_CAMPAIGN_SAVE_LEG_PASS session={_options.SessionId} chapter={_run.CurrentChapterId} commandCount={_run.CommandCount}");
        }
        GetTree().Quit(0);
    }

    private void OnRestartChapterRequested()
    {
        _finalLogged = false;
        ApplyCommand("RESTART_CHAPTER", _run.RestartChapter());
        _shell.HideShell();
    }

    private void OnFullscreenChanged(bool fullscreen)
    {
        _settings = _settings with
        {
            WindowMode = fullscreen
                ? ProductWindowMode.Fullscreen
                : ProductWindowMode.Windowed,
        };
        ApplySettings(_settings);
        SaveSettings();
    }

    private void OnUiScalePercentChanged(int uiScalePercent)
    {
        _settings = _settings with { UiScalePercent = uiScalePercent };
        ApplySettings(_settings);
        SaveSettings();
    }

    private void OnControlHelpChanged(bool enabled)
    {
        _settings = _settings with { ShowControlHelp = enabled };
        ApplySettings(_settings);
        SaveSettings();
    }

    private void ApplySettings(ProductSettings settings)
    {
        GetWindow().Mode = settings.WindowMode == ProductWindowMode.Fullscreen
            ? Window.ModeEnum.Fullscreen
            : Window.ModeEnum.Windowed;
        GetWindow().ContentScaleFactor = settings.UiScalePercent / 100f;
        _shell.SetSettings(
            settings.WindowMode == ProductWindowMode.Fullscreen,
            settings.UiScalePercent,
            settings.ShowControlHelp);
    }

    private void ConfigurePersistencePaths()
    {
        string storageDirectory = Path.GetFullPath(
            _options.StorageDirectory ?? ProjectSettings.GlobalizePath("user://"));
        _campaignSavePath = Path.Combine(
            storageDirectory,
            ProductPersistenceStore.CampaignSaveFileName);
        _settingsPath = Path.Combine(
            storageDirectory,
            ProductPersistenceStore.SettingsFileName);
    }

    private void LoadPersistentState()
    {
        var notices = new List<string>();
        ProductSettingsLoadResult settingsResult =
            ProductPersistenceStore.LoadSettings(_settingsPath);
        _settings = settingsResult.Settings;
        if (settingsResult.Status == ProductDocumentLoadStatus.Invalid)
        {
            notices.Add("화면 설정을 읽지 못해 기본값으로 시작합니다.");
        }

        ProductCampaignSaveLoadResult saveResult =
            ProductPersistenceStore.LoadCampaignSave(_campaignSavePath);
        if (saveResult.Status == ProductDocumentLoadStatus.Invalid)
        {
            notices.Add("저장 파일이 손상되어 이어하기를 사용할 수 없습니다.");
        }
        else if (saveResult.Status == ProductDocumentLoadStatus.Loaded)
        {
            try
            {
                _continuation = ProductCampaignRun.Restore(
                    _campaign,
                    _fixture,
                    _campaignHash,
                    _fixtureHash,
                    saveResult.Save
                        ?? throw new ProductPersistenceValidationException(
                            "Loaded campaign save has no payload."));
            }
            catch (Exception exception) when (
                exception is ProductPersistenceValidationException or
                ArgumentException or InvalidOperationException or OverflowException)
            {
                _continuation = null;
                notices.Add("현재 캠페인과 맞지 않는 저장이라 이어하기를 사용할 수 없습니다.");
            }
        }
        _titleStatus = string.Join('\n', notices);
    }

    private void EnterGameplay()
    {
        if (!string.IsNullOrEmpty(_lastError))
        {
            _shell.ShowPause(CurrentChapterDisplayName(), _lastError);
            return;
        }
        if (_settings.ShowControlHelp)
        {
            _shell.ShowControlHelpBeforeGameplay();
        }
        else
        {
            _shell.HideShell();
        }
    }

    private bool TrySaveCampaign(out string error)
    {
        error = string.Empty;
        if (!_persistenceEnabled)
        {
            return true;
        }
        try
        {
            ProductPersistenceStore.SaveCampaign(
                _campaignSavePath,
                _run.CaptureSave());
            return true;
        }
        catch (Exception exception)
        {
            error = $"캠페인을 저장하지 못했습니다. {exception.Message}";
            GD.PushWarning(error);
            return false;
        }
    }

    private void SaveSettings()
    {
        if (!_persistenceEnabled)
        {
            return;
        }
        try
        {
            ProductPersistenceStore.SaveSettings(_settingsPath, _settings);
        }
        catch (Exception exception)
        {
            string error = $"화면 설정을 저장하지 못했습니다. {exception.Message}";
            GD.PushWarning(error);
            _shell.ShowPersistenceError(error);
        }
    }

    private object CampaignDiagnosticPayload() => new
    {
        campaignId = _campaign.CampaignId,
        chapterId = _run.CurrentChapterId,
        chapterStartCommandCount = _run.ChapterStartCommandCount,
        commandCount = _run.CommandCount,
        phase = Machine(_snapshot.Phase),
        minute = _snapshot.Minute,
        cash = _snapshot.Cash,
    };

    private string CurrentChapterDisplayName() =>
        _campaign.Chapters.Single(chapter =>
            string.Equals(chapter.ChapterId, _run.CurrentChapterId, StringComparison.Ordinal))
        .DisplayName;

    private void OnPointerChanged(FirstLightGridPoint? point)
    {
        if (!point.HasValue)
        {
            _pointerPreview = null;
            Render();
            return;
        }

        ProductPoint productPoint = ToProduct(point.Value);
        _pointerPreview = _snapshot.Phase switch
        {
            ProductPhase.SubstationPlanning => ToMapPreview(
                _run.PreviewSubstationPlacement(productPoint)),
            ProductPhase.PlantPlanning => ToMapPreview(
                _run.PreviewPlantPlacement(productPoint)),
            _ when IsLinePlanning(_snapshot.Phase) =>
                ToMapPreview(_run.PreviewLineSupport(productPoint)),
            _ => null,
        };
        Render();
    }

    private void OnPointRequested(FirstLightGridPoint point)
    {
        ProductCampaignCommand? command = _snapshot.Phase switch
        {
            ProductPhase.SubstationPlanning => new(
                ProductCampaignCommandKind.SetSubstationDraft,
                ToProduct(point)),
            ProductPhase.PlantPlanning => new(
                ProductCampaignCommandKind.SetPlantDraft,
                ToProduct(point)),
            _ when IsLinePlanning(_snapshot.Phase) => new(
                ProductCampaignCommandKind.AddLineSupport,
                ToProduct(point)),
            _ => null,
        };
        if (command is null)
        {
            return;
        }

        string commandName = _snapshot.Phase switch
        {
            ProductPhase.SubstationPlanning => "SET_SUBSTATION_DRAFT",
            ProductPhase.PlantPlanning => "SET_PLANT_DRAFT",
            _ => "ADD_LINE_SUPPORT",
        };
        ApplyCommand(commandName, _run.Execute(command));
    }

    private void OnCancelDraftRequested()
    {
        ProductCampaignCommand? command = _snapshot.Phase switch
        {
            ProductPhase.SubstationPlanning => new(
                ProductCampaignCommandKind.CancelSubstationDraft),
            ProductPhase.PlantPlanning => new(
                ProductCampaignCommandKind.CancelPlantDraft),
            _ when IsLinePlanning(_snapshot.Phase) => new(
                ProductCampaignCommandKind.CancelLineDraft),
            _ => null,
        };
        if (command is null)
        {
            return;
        }
        string commandName = _snapshot.Phase switch
        {
            ProductPhase.SubstationPlanning => "CANCEL_SUBSTATION_DRAFT",
            ProductPhase.PlantPlanning => "CANCEL_PLANT_DRAFT",
            _ => "CANCEL_LINE_DRAFT",
        };
        ApplyCommand(commandName, _run.Execute(command));
    }

    private void OnUndoRequested() =>
        ApplyCommand(
            "UNDO_LINE_SUPPORT",
            _run.Execute(new ProductCampaignCommand(
                ProductCampaignCommandKind.UndoLineSupport)));

    private void OnOrderRequested()
    {
        ProductCampaignCommand? command = _snapshot.Phase switch
        {
            ProductPhase.SubstationPlanning => new(
                ProductCampaignCommandKind.OrderSubstation),
            ProductPhase.PlantPlanning => new(ProductCampaignCommandKind.OrderPlant),
            ProductPhase.MaintenanceDecision => new(
                ProductCampaignCommandKind.OrderPreventiveMaintenance),
            _ when IsLinePlanning(_snapshot.Phase) => new(
                ProductCampaignCommandKind.OrderLine),
            _ => null,
        };
        if (command is null)
        {
            return;
        }
        string commandName = _snapshot.Phase switch
        {
            ProductPhase.SubstationPlanning => "ORDER_SUBSTATION",
            ProductPhase.PlantPlanning => "ORDER_PLANT",
            ProductPhase.MaintenanceDecision => "ORDER_PREVENTIVE_MAINTENANCE",
            _ => "ORDER_LINE",
        };
        ApplyCommand(commandName, _run.Execute(command));
    }

    private void OnAdvanceRequested() =>
        ApplyCommand(
            "ADVANCE_TO_CONSTRUCTION_COMPLETION",
            _run.Execute(new ProductCampaignCommand(
                ProductCampaignCommandKind.AdvanceToConstructionCompletion)));

    private void OnMilestoneRequested()
    {
        (string Name, ProductCampaignCommand Command)? command = _snapshot.Phase switch
        {
            ProductPhase.SettlementReady =>
                ("ADVANCE_TO_SETTLEMENT", new(ProductCampaignCommandKind.AdvanceToSettlement)),
            ProductPhase.IncidentReady =>
                ("ADVANCE_TO_INCIDENT", new(ProductCampaignCommandKind.AdvanceToIncident)),
            ProductPhase.IncidentActive =>
                ("ADVANCE_TO_RECOVERY_AND_SETTLEMENT", new(
                    ProductCampaignCommandKind.AdvanceToRecoveryAndSettlement)),
            ProductPhase.FactorySettlementReady =>
                ("ADVANCE_TO_FACTORY_SETTLEMENT", new(
                    ProductCampaignCommandKind.AdvanceToFactorySettlement)),
            ProductPhase.MaintenanceDecision =>
                ("SKIP_PREVENTIVE_MAINTENANCE", new(
                    ProductCampaignCommandKind.SkipPreventiveMaintenance)),
            ProductPhase.HeatwaveReady =>
                ("ADVANCE_TO_HEATWAVE", new(
                    ProductCampaignCommandKind.AdvanceToHeatwave)),
            ProductPhase.HeatwaveActive =>
                ("ADVANCE_TO_HEATWAVE_SETTLEMENT", new(
                    ProductCampaignCommandKind.AdvanceToHeatwaveSettlement)),
            _ => null,
        };
        if (command.HasValue)
        {
            ApplyCommand(command.Value.Name, _run.Execute(command.Value.Command));
        }
    }

    private void ApplyCommand(string commandName, ProductCommandResult result)
    {
        _snapshot = result.Snapshot;
        _pointerPreview = null;
        _lastError = result.Accepted
            ? TrySaveCampaign(out string saveError) ? string.Empty : saveError
            : ErrorText(result.Error);
        _diagnostic?.WriteCommand(result.Accepted, new
        {
            commandName,
            chapterId = _run.CurrentChapterId,
            chapterStartCommandCount = _run.ChapterStartCommandCount,
            commandCount = _run.CommandCount,
            errorCode = result.Error.HasValue ? Machine(result.Error.Value) : null,
            phase = Machine(_snapshot.Phase),
            activeProjectId = ActiveProjectId(_snapshot),
            supportCount = ActiveSupports(_snapshot).Count,
            factory = _snapshot.Factory is ProductFactorySnapshot factory
                ? new
                {
                    selectedSiteId = factory.SelectedSiteId,
                    plantOnlineMinute = factory.PlantGridConnected
                        ? factory.ConnectionLine.CompletionMinute
                        : null,
                    factoryDeliveredKw = factory.FactoryDeliveredKw,
                    existingSourceDispatchKw = factory.ExistingSourceDispatchKw,
                    gasPlantDispatchKw = factory.GasPlantDispatchKw,
                }
                : null,
            heatwave = _snapshot.Heatwave is ProductHeatwaveSnapshot heatwave
                ? new
                {
                    maintenanceChoice = Machine(heatwave.MaintenanceChoice),
                    maintenanceProjectState = Machine(heatwave.MaintenanceProjectState),
                    maintenanceCompletionMinute = heatwave.MaintenanceCompletionMinute,
                    eventId = heatwave.Id,
                    eventActive = heatwave.Active,
                    startMinute = heatwave.StartMinute,
                    recoveryMinute = heatwave.RecoveryMinute,
                    townDemandKw = heatwave.CurrentTownDemandKw,
                    effectiveFactoryFeederRatingKw = heatwave.CurrentFactoryFeederRatingKw,
                    agedFactoryFeederUnavailable =
                        heatwave.AgedFactoryFeederCurrentlyUnavailable,
                    delivery = new
                    {
                        hospitalKw = heatwave.HospitalDeliveredKw,
                        townKw = heatwave.TownDeliveredKw,
                        factoryKw = heatwave.FactoryDeliveredKw,
                    },
                    dispatch = new
                    {
                        existingSourceKw = heatwave.ExistingSourceDispatchKw,
                        gasPlantKw = heatwave.GasPlantDispatchKw,
                    },
                    settlement = new
                    {
                        completed = heatwave.Settlement.Completed,
                        cashChangeCashUnit = heatwave.Settlement.CashChangeCashUnit,
                    },
                }
                : null,
        });
        Render();
    }

    private void Render()
    {
        _snapshot = _run.GetSnapshot();
        ProductHospitalSnapshot hospital = HospitalSnapshot();
        ProductFactorySnapshot factory = FactorySnapshot();
        ProductHeatwaveSnapshot heatwave = HeatwaveSnapshot();
        string phaseText = CurrentPhaseText(hospital, factory, heatwave);
        _phaseLabel.Text = phaseText;
        _phaseLabel.AccessibilityName = $"현재 단계 {phaseText}";
        _timeLabel.Text = $"시각 {_snapshot.Minute.ToString("N0", CultureInfo.InvariantCulture)}분";
        _cashLabel.Text = $"현금 {CashText(_snapshot.Cash)}";
        long hospitalUtility = DisplayHospitalUtility(hospital, factory, heatwave);
        long townUtility = DisplayTownUtility(hospital, factory, heatwave);
        long townDemand = DisplayTownDemand(heatwave);
        long factoryUtility = DisplayFactoryUtility(factory, heatwave);
        _demandLabel.Text =
            $"마을 {PowerText(townUtility)} / {PowerText(townDemand)} · " +
            $"병원 {PowerText(hospitalUtility)} / {PowerText(_hospitalFixture.DemandKw)} · " +
            $"공장 {PowerText(factoryUtility)} / {PowerText(_factoryFixture.DemandKw)}";

        _mapView.SetModel(BuildMapModel(hospital, factory, heatwave));
        _taskPanel.SetModel(BuildPanelModel(hospital, factory, heatwave));

        if (_snapshot.Phase == ProductPhase.Complete && !_finalLogged)
        {
            ProductHospitalSettlementSnapshot ledger = hospital.Settlement;
            ProductFactorySettlementSnapshot factoryLedger = factory.Settlement;
            ProductHeatwaveSettlementSnapshot heatwaveLedger = heatwave.Settlement;
            if (heatwaveLedger.Completed)
            {
                _diagnostic?.WriteFinal(new
                {
                    outcome = Machine(_snapshot.Outcome),
                    hardConditions = new
                    {
                        singleLineRemoval = ledger.SingleLineRemovalConditionMet,
                        spatialIncidentUtility = ledger.SpatialIncidentUtilityConditionMet,
                        hospitalP0 = ledger.HospitalP0ConditionMet,
                        allLoadsFullySupplied = heatwaveLedger.AllLoadsFullySupplied,
                    },
                    maintenance = new
                    {
                        choice = Machine(heatwave.MaintenanceChoice),
                        projectState = Machine(heatwave.MaintenanceProjectState),
                        completionMinute = heatwave.MaintenanceCompletionMinute,
                        costCashUnit = heatwave.MaintenanceChoice == ProductMaintenanceChoice.Ordered
                            ? _maintenanceFixture.CostCashUnit
                            : 0,
                    },
                    heatwaveEvent = new
                    {
                        eventId = heatwave.Id,
                        startMinute = heatwave.StartMinute,
                        recoveryMinute = heatwave.RecoveryMinute,
                        townDemandKw = heatwave.ForecastTownDemandKw,
                        effectiveFactoryFeederRatingKw =
                            heatwave.ForecastFactoryFeederRatingKw,
                        agedFactoryFeederId = _heatwaveFixture.AgedFactoryFeederId,
                        agedFactoryFeederUnavailableDuringEvent =
                            heatwave.AgedFactoryFeederUnavailableDuringEvent,
                    },
                    delivery = new
                    {
                        hospitalKw = heatwave.HospitalDeliveredKw,
                        hospitalSourceAssetId = heatwave.HospitalSourceAssetId,
                        townKw = heatwave.TownDeliveredKw,
                        townSourceAssetId = heatwave.TownSourceAssetId,
                        factoryKw = heatwave.FactoryDeliveredKw,
                        factorySourceAssetId = heatwave.FactorySourceAssetId,
                    },
                    dispatch = new
                    {
                        existingSourceKw = heatwave.ExistingSourceDispatchKw,
                        gasPlantKw = heatwave.GasPlantDispatchKw,
                    },
                    energy = new
                    {
                        hospitalDeliveredKwMinute = heatwaveLedger.HospitalDeliveredEnergyKwMinute,
                        townDeliveredKwMinute = heatwaveLedger.TownDeliveredEnergyKwMinute,
                        factoryDeliveredKwMinute = heatwaveLedger.FactoryDeliveredEnergyKwMinute,
                        existingSourceGenerationKwMinute =
                            heatwaveLedger.ExistingSourceGenerationEnergyKwMinute,
                        gasPlantGenerationKwMinute =
                            heatwaveLedger.GasPlantGenerationEnergyKwMinute,
                        utilityUnservedKwMinute = heatwaveLedger.UtilityUnservedEnergyKwMinute,
                    },
                    cash = new
                    {
                        revenueCashUnit = heatwaveLedger.UtilityRevenueCashUnit,
                        existingSourceGenerationCostCashUnit =
                            heatwaveLedger.ExistingSourceGenerationCostCashUnit,
                        gasPlantGenerationCostCashUnit =
                            heatwaveLedger.GasPlantGenerationCostCashUnit,
                        compensationCashUnit = heatwaveLedger.UnservedCompensationCashUnit,
                        lostSalesCashUnit = heatwaveLedger.LostSalesCashUnit,
                        changeCashUnit = heatwaveLedger.CashChangeCashUnit,
                        endingCashUnit = _snapshot.Cash,
                    },
                });
            }
            else if (factoryLedger.Completed)
            {
                _diagnostic?.WriteFinal(new
                {
                    outcome = Machine(_snapshot.Outcome),
                    hardConditions = new
                    {
                        singleLineRemoval = ledger.SingleLineRemovalConditionMet,
                        spatialIncidentUtility = ledger.SpatialIncidentUtilityConditionMet,
                        hospitalP0 = ledger.HospitalP0ConditionMet,
                        allLoadsFullySupplied = factoryLedger.AllLoadsFullySupplied,
                    },
                    plant = new
                    {
                        selectedSiteId = factory.SelectedSiteId,
                        onlineMinute = factory.ConnectionLine.CompletionMinute,
                        gridConnected = factory.PlantGridConnected,
                    },
                    delivery = new
                    {
                        hospitalKw = factory.HospitalDeliveredKw,
                        hospitalSourceAssetId = factory.HospitalSourceAssetId,
                        townKw = factory.TownDeliveredKw,
                        townSourceAssetId = factory.TownSourceAssetId,
                        factoryKw = factory.FactoryDeliveredKw,
                        factorySourceAssetId = factory.FactorySourceAssetId,
                    },
                    dispatch = new
                    {
                        existingSourceKw = factory.ExistingSourceDispatchKw,
                        gasPlantKw = factory.GasPlantDispatchKw,
                    },
                    energy = new
                    {
                        hospitalDeliveredKwMinute = factoryLedger.HospitalDeliveredEnergyKwMinute,
                        townDeliveredKwMinute = factoryLedger.TownDeliveredEnergyKwMinute,
                        factoryDeliveredKwMinute = factoryLedger.FactoryDeliveredEnergyKwMinute,
                        existingSourceGenerationKwMinute = factoryLedger.ExistingSourceGenerationEnergyKwMinute,
                        gasPlantGenerationKwMinute = factoryLedger.GasPlantGenerationEnergyKwMinute,
                        utilityUnservedKwMinute = factoryLedger.UtilityUnservedEnergyKwMinute,
                    },
                    cash = new
                    {
                        revenueCashUnit = factoryLedger.UtilityRevenueCashUnit,
                        existingSourceGenerationCostCashUnit =
                            factoryLedger.ExistingSourceGenerationCostCashUnit,
                        gasPlantGenerationCostCashUnit = factoryLedger.GasPlantGenerationCostCashUnit,
                        compensationCashUnit = factoryLedger.UnservedCompensationCashUnit,
                        lostSalesCashUnit = factoryLedger.LostSalesCashUnit,
                        changeCashUnit = factoryLedger.CashChangeCashUnit,
                        endingCashUnit = _snapshot.Cash,
                    },
                });
            }
            else if (!ledger.Completed)
            {
                _diagnostic?.WriteFinal(new
                {
                    outcome = Machine(_snapshot.Outcome),
                    firstLight = new
                    {
                        supplyFailure = Machine(_snapshot.SupplyFailure),
                        deliveredEnergyKwMinute = _snapshot.Settlement.DeliveredEnergyKwMinute,
                        revenueCashUnit = _snapshot.Settlement.RevenueCashUnit,
                        endingCashUnit = _snapshot.Cash,
                    },
                });
            }
            else
            {
                _diagnostic?.WriteFinal(new
                {
                    outcome = Machine(_snapshot.Outcome),
                    hardConditions = new
                    {
                        singleLineRemoval = ledger.SingleLineRemovalConditionMet,
                        spatialIncidentUtility = ledger.SpatialIncidentUtilityConditionMet,
                        hospitalP0 = ledger.HospitalP0ConditionMet,
                    },
                    removedProjectIds = hospital.Incident.UnavailableProjectIds,
                    utility = new
                    {
                        hospitalKw = hospital.Incident.HospitalUtilityKw,
                        townKw = hospital.Incident.TownUtilityKw,
                        hospitalP0DeliveredKw = hospital.HospitalP0DeliveredKw,
                    },
                    energy = new
                    {
                        hospitalUtilityKwMinute = ledger.HospitalUtilityEnergyKwMinute,
                        townUtilityKwMinute = ledger.TownUtilityEnergyKwMinute,
                        generationKwMinute = ledger.UtilityGenerationEnergyKwMinute,
                        utilityUnservedKwMinute = ledger.UtilityUnservedEnergyKwMinute,
                        upsKwMinute = ledger.UpsEnergyKwMinute,
                        dieselKwMinute = ledger.DieselEnergyKwMinute,
                        hospitalP0UnservedKwMinute = ledger.HospitalP0UnservedEnergyKwMinute,
                    },
                    cash = new
                    {
                        revenueCashUnit = ledger.UtilityRevenueCashUnit,
                        generationCostCashUnit = ledger.GenerationCostCashUnit,
                        compensationCashUnit = ledger.UnservedCompensationCashUnit,
                        lostSalesCashUnit = ledger.LostSalesCashUnit,
                        changeCashUnit = ledger.CashChangeCashUnit,
                        endingCashUnit = _snapshot.Cash,
                    },
                });
            }
            _finalLogged = true;
        }
    }

    private FirstLightMapModel BuildMapModel(
        ProductHospitalSnapshot hospital,
        ProductFactorySnapshot factory,
        ProductHeatwaveSnapshot heatwave)
    {
        FirstLightTargetPreview? targetPreview = null;
        if (IsLinePlanning(_snapshot.Phase))
        {
            ProductOrderPreview order = _run.PreviewLineOrder();
            IReadOnlyList<ProductPoint> supports = ActiveSupports(_snapshot);
            ProductPoint from = supports.Count == 0
                ? ActiveLineStart()
                : supports[^1];
            targetPreview = new FirstLightTargetPreview(
                ToGrid(from),
                ToGrid(ActiveTarget()),
                order.Error != ProductCommandError.SpanTooLong);
        }

        bool IsUnavailable(string projectId) =>
            hospital.Incident.Active &&
            hospital.Incident.UnavailableProjectIds.Contains(projectId, StringComparer.Ordinal);

        var lines = new List<FirstLightLineVisual>
        {
            new(
                FirstLightLineKind.Town,
                "마을 회선",
                ToGrid(_snapshot.Substation.Position ?? _fixture.ExistingSource.Position),
                _snapshot.Line.SupportPositions.Select(ToGrid).ToArray(),
                VisualState(_snapshot.Line.ProjectState, IsUnavailable(_fixture.LineProject.ProjectId)),
                _snapshot.Phase == ProductPhase.LinePlanning),
            new(
                FirstLightLineKind.HospitalPrimary,
                "병원 주회선",
                ToGrid(_hospitalFixture.Position),
                hospital.PrimaryLine.SupportPositions.Select(ToGrid).ToArray(),
                VisualState(
                    hospital.PrimaryLine.ProjectState,
                    IsUnavailable(hospital.PrimaryLine.ProjectId)),
                _snapshot.Phase == ProductPhase.PrimaryPlanning),
            new(
                FirstLightLineKind.HospitalBackup,
                "병원 예비회선",
                ToGrid(_hospitalFixture.Position),
                hospital.BackupLine.SupportPositions.Select(ToGrid).ToArray(),
                VisualState(
                    hospital.BackupLine.ProjectState,
                    IsUnavailable(hospital.BackupLine.ProjectId)),
                _snapshot.Phase == ProductPhase.BackupPlanning),
        };
        if (factory.PlantPosition is not null &&
            factory.PlantProjectState == ProductProjectState.Commissioned)
        {
            lines.Add(new FirstLightLineVisual(
                FirstLightLineKind.PlantConnection,
                "발전소 접속선",
                ToGrid(_fixture.ExistingSource.Position),
                factory.ConnectionLine.SupportPositions.Select(ToGrid).ToArray(),
                VisualState(factory.ConnectionLine.ProjectState, false),
                _snapshot.Phase == ProductPhase.PlantConnectionPlanning,
                ToGrid(factory.PlantPosition)));
        }

        ProductRiskRect risk = _incidentFixture.RiskRect;

        return new FirstLightMapModel(
            new FirstLightGridBounds(
                _fixture.MapBounds.MinX,
                _fixture.MapBounds.MaxX,
                _fixture.MapBounds.MinY,
                _fixture.MapBounds.MaxY),
            _fixture.BlockedCells.Select(ToGrid).ToArray(),
            ToGrid(_fixture.ExistingSource.Position),
            ToGrid(_fixture.Town.Position),
            DisplayTownUtility(hospital, factory, heatwave),
            ToGrid(_hospitalFixture.Position),
            DisplayHospitalUtility(hospital, factory, heatwave),
            IsHeatwaveDisplayStage(_snapshot.Phase, heatwave)
                ? heatwave.HospitalDeliveredKw
                : IsFactoryDisplayStage(_snapshot.Phase, factory)
                    ? factory.HospitalDeliveredKw
                : hospital.HospitalP0DeliveredKw,
            new FirstLightRiskRect(
                new FirstLightGridPoint(risk.MinX, risk.MinY),
                new FirstLightGridPoint(risk.MaxX, risk.MaxY),
                hospital.Incident.Active),
            _snapshot.Substation.Position is null ? null : ToGrid(_snapshot.Substation.Position),
            _fixture.SubstationProject.ServiceRadiusGridUnit,
            VisualState(_snapshot.Substation.ProjectState, false),
            lines,
            _pointerPreview,
            targetPreview,
            CurrentPhaseText(hospital, factory, heatwave),
            StatusText(hospital, factory, heatwave),
            (_fixture.GasPlantSites ?? Array.Empty<ProductGasPlantSite>())
                .Select(site => new FirstLightPlantSiteVisual(
                    site.SiteId,
                    ToGrid(site.Position),
                    string.Equals(site.SiteId, factory.SelectedSiteId, StringComparison.Ordinal)))
                .ToArray(),
            new FirstLightFactoryVisual(
                ToGrid(_factoryFixture.Position),
                ToGrid(_fixture.ExistingSource.Position),
                DisplayFactoryUtility(factory, heatwave),
                FactoryFeederVisualState(heatwave),
                DisplayFactoryFeederRating(heatwave)),
            factory.PlantPosition is null
                ? null
                : new FirstLightGasPlantVisual(
                    ToGrid(factory.PlantPosition),
                    VisualState(factory.PlantProjectState, false),
                    factory.PlantGridConnected,
                    IsHeatwaveDisplayStage(_snapshot.Phase, heatwave)
                        ? heatwave.GasPlantDispatchKw
                        : factory.GasPlantDispatchKw),
            IsFactoryDisplayStage(_snapshot.Phase, factory) ||
                IsHeatwaveDisplayStage(_snapshot.Phase, heatwave));
    }

    private FirstLightTaskPanelModel BuildPanelModel(
        ProductHospitalSnapshot hospital,
        ProductFactorySnapshot factory,
        ProductHeatwaveSnapshot heatwave)
    {
        FirstLightActionPresentation hidden = Action(false, false, string.Empty, string.Empty);
        FirstLightActionPresentation cancel = hidden;
        FirstLightActionPresentation undo = hidden;
        FirstLightActionPresentation order = hidden;
        FirstLightActionPresentation advance = hidden;
        FirstLightActionPresentation settle = hidden;

        string instruction;
        string preview;
        switch (_snapshot.Phase)
        {
            case ProductPhase.SubstationPlanning:
                {
                    ProductOrderPreview quote = _run.PreviewSubstationOrder();
                    instruction =
                        "지도에서 배전 변전소의 초안을 놓으세요. 서비스 권역은 접속 가능 범위이며, 선로가 완공돼야 실제 공급됩니다.";
                    preview = SubstationPreviewText(quote);
                    cancel = Action(true, true, "변전소 초안 취소", "현재 변전소 초안을 지웁니다.");
                    order = Action(
                        true,
                        quote.Accepted,
                        quote.CostCashUnit.HasValue
                            ? $"변전소 발주 · {CashText(quote.CostCashUnit.Value)}"
                            : "변전소 발주",
                        "현재 초안 위치에 변전소 공사를 발주합니다.");
                    break;
                }
            case ProductPhase.SubstationBuilding:
                instruction = "변전소 공사가 발주됐습니다. 공사 중인 설비는 아직 전기를 전달하지 않습니다.";
                preview = CompletionText(_snapshot.Substation.CompletionMinute);
                advance = Action(true, true, "변전소 완공까지 진행", "현재 변전소 공사의 완공 시각으로 진행합니다.");
                break;
            case ProductPhase.LinePlanning:
                {
                    ProductOrderPreview quote = _run.PreviewLineOrder();
                    instruction =
                        "기존 발전원에서 완공된 변전소까지 이어지도록 지지물을 순서대로 놓으세요. 마지막 span도 거리 제한 안에 있어야 합니다.";
                    preview = LinePreviewText(quote);
                    cancel = Action(true, true, "선로 초안 전체 취소", "놓은 지지물을 모두 지웁니다.");
                    undo = Action(
                        true,
                        _snapshot.Line.SupportPositions.Count > 0,
                        "마지막 지지물 되돌리기",
                        "가장 마지막에 놓은 지지물 하나를 지웁니다.");
                    order = Action(
                        true,
                        quote.Accepted,
                        quote.CostCashUnit.HasValue
                            ? $"선로 발주 · {CashText(quote.CostCashUnit.Value)}"
                            : "선로 발주",
                        "현재 지지물 순서로 선로 공사를 발주합니다.");
                    break;
                }
            case ProductPhase.LineBuilding:
                instruction = "선로 공사가 발주됐습니다. 모든 span은 완공 전까지 통전되지 않습니다.";
                preview = CompletionText(_snapshot.Line.CompletionMinute);
                advance = Action(true, true, "선로 완공까지 진행", "선로 전체의 완공 시각으로 진행합니다.");
                break;
            case ProductPhase.SettlementReady:
                instruction = _snapshot.SupplyFailure == ProductSupplyFailure.None
                    ? "마을에 전기가 도착했습니다. 첫 결산 뒤 병원 회선 건설로 이어집니다."
                    : "공사는 끝났지만 마을 공급 조건을 충족하지 못했습니다. 결과를 결산한 뒤 임무를 다시 시작할 수 있습니다.";
                preview = $"첫 점등 확인 · {SupplyText(_snapshot.SupplyFailure)}";
                settle = Action(true, true, "첫 결산까지 진행", "고정된 첫 공급 기간을 진행하고 실제 인도분만 결산합니다.");
                break;
            case ProductPhase.PrimaryPlanning:
            case ProductPhase.BackupPlanning:
                {
                    bool primary = _snapshot.Phase == ProductPhase.PrimaryPlanning;
                    string label = primary ? "병원 주회선" : "병원 예비회선";
                    ProductOrderPreview quote = _run.PreviewLineOrder();
                    instruction = primary
                        ? "병원 주회선의 지지물을 순서대로 놓으세요. 위험구역 노출은 발주 전에 표시됩니다."
                        : "주회선과 support를 공유하지 않는 예비회선을 만드세요. 공간 우회는 더 길고 비쌀 수 있습니다.";
                    preview = HospitalLinePreviewText(quote);
                    cancel = Action(true, true, $"{label} 초안 전체 취소", "놓은 지지물을 모두 지웁니다.");
                    undo = Action(
                        true,
                        ActiveSupports(_snapshot).Count > 0,
                        "마지막 지지물 되돌리기",
                        "가장 마지막에 놓은 지지물 하나를 지웁니다.");
                    order = Action(
                        true,
                        quote.Accepted,
                        quote.CostCashUnit.HasValue
                            ? $"{label} 발주 · {CashText(quote.CostCashUnit.Value)}"
                            : $"{label} 발주",
                        "현재 지지물 순서로 회선 공사를 발주합니다.");
                    break;
                }
            case ProductPhase.PrimaryBuilding:
                instruction = "병원 주회선 공사가 진행 중입니다. 완공 전에는 병원 경로로 쓸 수 없습니다.";
                preview = CompletionText(hospital.PrimaryLine.CompletionMinute);
                advance = Action(true, true, "주회선 완공까지 진행", "주회선 전체의 완공 시각으로 진행합니다.");
                break;
            case ProductPhase.BackupBuilding:
                instruction = "병원 예비회선 공사가 진행 중입니다. 완공 뒤 단일회선 제거 결과를 확인합니다.";
                preview = CompletionText(hospital.BackupLine.CompletionMinute);
                advance = Action(true, true, "예비회선 완공까지 진행", "예비회선 전체의 완공 시각으로 진행합니다.");
                break;
            case ProductPhase.IncidentReady:
                instruction =
                    "두 회선이 완공됐습니다. 각 회선을 하나씩 제거한 결과를 확인하고 고정 공간사건을 시작하세요.";
                preview = ReliabilityText(_run.PreviewReliability());
                settle = Action(true, true, "고정 공간사건 시작", "사건 시작 경계로 진행하고 닿는 회선을 사용불가로 만듭니다.");
                break;
            case ProductPhase.IncidentActive:
                instruction =
                    "공간사건이 진행 중입니다. 사용불가 회선과 병원 utility·P0를 확인하고 복구·결산까지 진행하세요.";
                preview = IncidentText(hospital);
                settle = Action(true, true, "복구·결산까지 진행", "사건을 적분하고 회선을 복구한 뒤 경제를 결산합니다.");
                break;
            case ProductPhase.PlantPlanning:
                {
                    ProductOrderPreview quote = _run.PreviewPlantOrder();
                    instruction =
                        "공장 증설이 이미 발효되어 기존 발전용량만으로는 부족합니다. 지도에 같은 방식으로 표시된 두 허용 부지 중 하나를 선택하세요.";
                    preview = PlantPreviewText(quote, factory);
                    cancel = Action(true, true, "발전소 초안 취소", "선택한 발전소 부지 초안을 지웁니다.");
                    order = Action(
                        true,
                        quote.Accepted,
                        quote.CostCashUnit.HasValue
                            ? $"발전소 발주 · {CashText(quote.CostCashUnit.Value)}"
                            : "발전소 발주",
                        "기본비와 선택 부지비를 지불하고 가스발전소 공사를 발주합니다.");
                    break;
                }
            case ProductPhase.PlantBuilding:
                instruction =
                    "가스발전소 공사가 진행 중입니다. 완공 전 출력은 0이며 접속선은 아직 만들 수 없습니다.";
                preview = $"{CompletionText(factory.PlantCompletionMinute)} · 출력 {PowerText(factory.GasPlantDispatchKw)}";
                advance = Action(true, true, "발전소 완공까지 진행", "가스발전소의 완공 시각으로 진행합니다.");
                break;
            case ProductPhase.PlantConnectionPlanning:
                {
                    ProductOrderPreview quote = _run.PreviewLineOrder();
                    instruction =
                        "완공된 가스발전소 terminal에서 기존 계통 접속점까지 지지물을 순서대로 놓으세요. 접속선 완공 전 출력은 0입니다.";
                    preview = LinePreviewText(quote);
                    cancel = Action(true, true, "접속선 초안 전체 취소", "놓은 접속선 지지물을 모두 지웁니다.");
                    undo = Action(
                        true,
                        factory.ConnectionLine.SupportPositions.Count > 0,
                        "마지막 지지물 되돌리기",
                        "가장 마지막에 놓은 접속선 지지물 하나를 지웁니다.");
                    order = Action(
                        true,
                        quote.Accepted,
                        quote.CostCashUnit.HasValue
                            ? $"접속선 발주 · {CashText(quote.CostCashUnit.Value)}"
                            : "접속선 발주",
                        "현재 지지물 순서로 발전소 접속선 공사를 발주합니다.");
                    break;
                }
            case ProductPhase.PlantConnectionBuilding:
                instruction =
                    "발전소 접속선 공사가 진행 중입니다. 선 전체가 완공될 때까지 발전소 출력은 0입니다.";
                preview = $"{CompletionText(factory.ConnectionLine.CompletionMinute)} · 출력 {PowerText(factory.GasPlantDispatchKw)}";
                advance = Action(true, true, "접속선 완공까지 진행", "접속선 전체의 완공 시각으로 진행합니다.");
                break;
            case ProductPhase.FactorySettlementReady:
                instruction =
                    "가스발전소가 계통에 접속됐습니다. 고정 merit order 급전과 세 수요처의 공급을 확인하고 다음 운영기간을 결산하세요.";
                preview = FactoryDispatchText(factory);
                settle = Action(true, true, "공장 공급기간 결산", "고정 공급기간의 실제 인도·발전비·미공급을 결산하고 폭염 예고를 확인합니다.");
                break;
            case ProductPhase.MaintenanceDecision:
                {
                    ProductOrderPreview quote = _run.PreviewPreventiveMaintenanceOrder();
                    instruction =
                        "폭염 예보가 확정됐습니다. 공장 노후 feeder를 예방정비하거나 비용 없이 건너뛰세요. 두 선택을 모두 보여주지만 추천하지 않습니다.";
                    preview =
                        $"{HeatwaveMilestonesText(heatwave)}\n" +
                        $"예고 · 마을 {PowerText(heatwave.ForecastTownDemandKw)} · " +
                        $"feeder 유효정격 {PowerText(heatwave.ForecastFactoryFeederRatingKw)}\n" +
                        MaintenanceQuoteText(quote);
                    order = Action(
                        true,
                        quote.Accepted,
                        quote.CostCashUnit.HasValue
                            ? $"예방정비 발주 · {CashText(quote.CostCashUnit.Value)}"
                            : "예방정비 발주",
                        "노후 feeder 예방정비 비용을 지불하고 공사를 발주합니다.");
                    settle = Action(
                        true,
                        true,
                        "정비 없이 진행",
                        "비용과 시간을 쓰지 않고 예방정비를 생략합니다.");
                    break;
                }
            case ProductPhase.MaintenanceBuilding:
                instruction =
                    "공장 노후 feeder 예방정비가 진행 중입니다. 공사 중 상태를 확인한 뒤 완공시각으로 진행하세요.";
                preview =
                    $"{HeatwaveMilestonesText(heatwave)}\n" +
                    CompletionText(heatwave.MaintenanceCompletionMinute);
                advance = Action(
                    true,
                    true,
                    "예방정비 완공까지 진행",
                    "예방정비 완공시각으로 진행합니다.");
                break;
            case ProductPhase.HeatwaveReady:
                instruction =
                    "예방정비 선택이 고정됐습니다. 예고된 폭염 시작시각과 수요·정격 변화를 다시 확인하세요.";
                preview =
                    $"{HeatwaveMilestonesText(heatwave)}\n" +
                    $"선택 · {MaintenanceChoiceText(heatwave.MaintenanceChoice)}\n" +
                    $"예고 · 마을 {PowerText(heatwave.ForecastTownDemandKw)} · " +
                    $"feeder 유효정격 {PowerText(heatwave.ForecastFactoryFeederRatingKw)}";
                settle = Action(
                    true,
                    true,
                    "폭염 시작까지 진행",
                    "고정된 시작시각으로 진행하고 폭염 수요와 feeder 상태를 적용합니다.");
                break;
            case ProductPhase.HeatwaveActive:
                instruction =
                    "폭염이 진행 중입니다. 세 수요처 공급, 발전원 급전과 노후 feeder 상태를 확인한 뒤 복구·결산으로 진행하세요.";
                preview =
                    $"{HeatwaveMilestonesText(heatwave)}\n" +
                    HeatwaveDispatchText(heatwave);
                settle = Action(
                    true,
                    true,
                    "복구·결산까지 진행",
                    "고정 폭염기간을 결산하고 노후 feeder를 복구합니다.");
                break;
            case ProductPhase.Complete:
                if (!hospital.Settlement.Completed)
                {
                    instruction =
                        "첫 점등에서 마을 공급 조건을 충족하지 못해 병원 공사를 시작하지 않았습니다. 표시된 원인을 확인하고 임무를 다시 시작할 수 있습니다.";
                    preview =
                        $"첫 결산 · {SupplyText(_snapshot.SupplyFailure)}\n" +
                        $"매출 {CashText(_snapshot.Settlement.RevenueCashUnit)} · 기말 현금 {CashText(_snapshot.Cash)}";
                }
                else if (!factory.Settlement.Completed)
                {
                    instruction = _snapshot.Outcome == ProductMissionOutcome.Success
                        ? "두 번째 심장 완료. 단일회선 제거, 실제 공간사건 utility, 병원 P0 조건을 모두 지켰습니다."
                        : "두 번째 심장 실패. 세 안전 조건을 각각 확인하고 전체 임무를 다시 시작할 수 있습니다.";
                    preview = FinalLedgerText(hospital.Settlement);
                }
                else if (!heatwave.Settlement.Completed)
                {
                    instruction = _snapshot.Outcome == ProductMissionOutcome.Success
                        ? "공장 용량 확장 완료. 병원·마을·공장을 모두 전량 공급했습니다."
                        : "공장 용량 확장 실패. 최종 공급과 급전 결과를 확인하고 전체 임무를 다시 시작할 수 있습니다.";
                    preview = FactoryFinalLedgerText(factory);
                }
                else
                {
                    instruction = _snapshot.Outcome == ProductMissionOutcome.Success
                        ? "폭염 대응 완료. 예방정비를 마친 노후 feeder와 두 발전원으로 세 수요처를 모두 공급했습니다."
                        : "폭염 대응 실패. 선택, 사건 중 공급과 결산 결과를 확인하고 전체 임무를 다시 시작할 수 있습니다.";
                    preview = HeatwaveFinalLedgerText(heatwave);
                }
                break;
            default:
                throw new ArgumentOutOfRangeException();
        }

        return new FirstLightTaskPanelModel(
            CurrentPhaseText(hospital, factory, heatwave),
            instruction,
            preview,
            StatusText(hospital, factory, heatwave),
            _lastError,
            cancel,
            undo,
            order,
            advance,
            settle);
    }

    private string SubstationPreviewText(ProductOrderPreview quote)
    {
        if (_pointerPreview?.Mode == FirstLightPointerMode.Substation)
        {
            return _pointerPreview.Description;
        }
        if (quote.Error == ProductCommandError.NoDraft)
        {
            return "초안을 배치하면 비용·공기와 예상 공급 조건을 확인할 수 있습니다.";
        }
        return OrderPreviewText(quote);
    }

    private string LinePreviewText(ProductOrderPreview quote)
    {
        if (_pointerPreview?.Mode == FirstLightPointerMode.LineSupport)
        {
            return _pointerPreview.Description;
        }
        return OrderPreviewText(quote);
    }

    private string PlantPreviewText(
        ProductOrderPreview quote,
        ProductFactorySnapshot factory)
    {
        if (_pointerPreview?.Mode == FirstLightPointerMode.GasPlant)
        {
            return _pointerPreview.Description;
        }
        if (quote.Error == ProductCommandError.NoDraft)
        {
            return "두 허용 부지는 추천 없이 같은 방식으로 표시됩니다. 부지를 선택하면 기본비를 포함한 발주 견적을 확인할 수 있습니다.";
        }
        string site = factory.SelectedSiteId is null
            ? string.Empty
            : $"선택 부지 · {factory.SelectedSiteId}\n";
        return site + OrderPreviewText(quote);
    }

    private string HospitalLinePreviewText(ProductOrderPreview quote)
    {
        if (_pointerPreview?.Mode == FirstLightPointerMode.LineSupport)
        {
            return _pointerPreview.Description;
        }
        string quoteText = quote.CostCashUnit.HasValue
            ? $"견적 {CashText(quote.CostCashUnit.Value)} · 공기 {quote.BuildMinutes!.Value.ToString("N0", CultureInfo.InvariantCulture)}분"
            : string.Empty;
        string exposure = quote.SpatialIncidentExposed switch
        {
            true => "공간사건 노출 · 있음",
            false => "공간사건 노출 · 없음",
            null => "공간사건 노출 · 경로를 완성하면 확인 가능",
        };
        string error = quote.Error.HasValue ? ErrorText(quote.Error) : string.Empty;
        return string.Join("\n", new[] { quoteText, exposure, error }.Where(text => text.Length > 0));
    }

    private static string ReliabilityText(ProductReliabilitySnapshot reliability) =>
        reliability.Evaluated
            ? $"주회선 제거 시 병원 utility · {YesNo(reliability.PrimaryRemovalKeepsHospitalUtility)}\n" +
              $"예비회선 제거 시 병원 utility · {YesNo(reliability.BackupRemovalKeepsHospitalUtility)}"
            : "두 회선이 완공되면 단일회선 제거 결과를 확인할 수 있습니다.";

    private static string IncidentText(ProductHospitalSnapshot hospital)
    {
        string unavailable = hospital.Incident.UnavailableProjectIds.Count == 0
            ? "사용불가 회선 · 없음"
            : $"사용불가 회선 · {string.Join(", ", hospital.Incident.UnavailableProjectIds)}";
        return
            $"{unavailable}\n병원 utility {PowerText(hospital.HospitalUtilityKw)} · " +
            $"P0 {PowerText(hospital.HospitalP0DeliveredKw)}\n" +
            $"마을 utility {PowerText(hospital.TownUtilityKw)}";
    }

    private static string FinalLedgerText(ProductHospitalSettlementSnapshot ledger) =>
        $"단일회선 제거 {YesNo(ledger.SingleLineRemovalConditionMet)} · " +
        $"공간사건 utility {YesNo(ledger.SpatialIncidentUtilityConditionMet)} · " +
        $"병원 P0 {YesNo(ledger.HospitalP0ConditionMet)}\n" +
        $"매출 {CashText(ledger.UtilityRevenueCashUnit)} · 발전비 {CashText(ledger.GenerationCostCashUnit)} · " +
        $"미공급 보상 {CashText(ledger.UnservedCompensationCashUnit)}\n" +
        $"현금 변화 {SignedCashText(ledger.CashChangeCashUnit)} · LostSales {CashText(ledger.LostSalesCashUnit)} (현금 미반영)\n" +
        $"UPS {EnergyText(ledger.UpsEnergyKwMinute)} · 디젤 {EnergyText(ledger.DieselEnergyKwMinute)} · " +
        $"P0 미공급 {EnergyText(ledger.HospitalP0UnservedEnergyKwMinute)}";

    private static string FactoryDispatchText(ProductFactorySnapshot factory) =>
        $"병원 {PowerText(factory.HospitalDeliveredKw)} · 마을 {PowerText(factory.TownDeliveredKw)} · " +
        $"공장 {PowerText(factory.FactoryDeliveredKw)}\n" +
        $"기존 발전원 급전 {PowerText(factory.ExistingSourceDispatchKw)} · " +
        $"새 가스발전소 급전 {PowerText(factory.GasPlantDispatchKw)}";

    private static string FactoryFinalLedgerText(ProductFactorySnapshot factory)
    {
        ProductFactorySettlementSnapshot ledger = factory.Settlement;
        return
            $"세 수요처 전량공급 {YesNo(ledger.AllLoadsFullySupplied)} · " +
            $"선택 부지 {factory.SelectedSiteId ?? "없음"}\n" +
            $"기존 발전 {EnergyText(ledger.ExistingSourceGenerationEnergyKwMinute)} · " +
            $"가스발전 {EnergyText(ledger.GasPlantGenerationEnergyKwMinute)} · " +
            $"미공급 {EnergyText(ledger.UtilityUnservedEnergyKwMinute)}\n" +
            $"매출 {CashText(ledger.UtilityRevenueCashUnit)} · " +
            $"기존 발전비 {CashText(ledger.ExistingSourceGenerationCostCashUnit)} · " +
            $"가스 발전비 {CashText(ledger.GasPlantGenerationCostCashUnit)}\n" +
            $"현금 변화 {SignedCashText(ledger.CashChangeCashUnit)} · " +
            $"LostSales {CashText(ledger.LostSalesCashUnit)} (현금 미반영)";
    }

    private static string MaintenanceQuoteText(ProductOrderPreview quote)
    {
        if (!quote.CostCashUnit.HasValue || !quote.BuildMinutes.HasValue)
        {
            return quote.Error.HasValue ? ErrorText(quote.Error) : "예방정비 견적 없음";
        }
        string value =
            $"정비 견적 {CashText(quote.CostCashUnit.Value)} · " +
            $"공기 {quote.BuildMinutes.Value.ToString("N0", CultureInfo.InvariantCulture)}분";
        return quote.Error.HasValue ? $"{value}\n{ErrorText(quote.Error)}" : value;
    }

    private string HeatwaveMilestonesText(ProductHeatwaveSnapshot heatwave) =>
        $"현재 · {_snapshot.Minute.ToString("N0", CultureInfo.InvariantCulture)}분\n" +
        $"폭염 시작 · {MinuteText(heatwave.StartMinute)}\n" +
        $"복구·결산 · {MinuteText(heatwave.RecoveryMinute)}";

    private static string HeatwaveDispatchText(ProductHeatwaveSnapshot heatwave) =>
        $"노후 feeder {(heatwave.AgedFactoryFeederCurrentlyUnavailable ? "사용불가" : "사용 가능")} · " +
        $"유효정격 {PowerText(heatwave.CurrentFactoryFeederRatingKw)}\n" +
        $"병원 {PowerText(heatwave.HospitalDeliveredKw)} · " +
        $"마을 {PowerText(heatwave.TownDeliveredKw)} · " +
        $"공장 {PowerText(heatwave.FactoryDeliveredKw)}\n" +
        $"기존 급전 {PowerText(heatwave.ExistingSourceDispatchKw)} · " +
        $"가스 급전 {PowerText(heatwave.GasPlantDispatchKw)}";

    private string HeatwaveFinalLedgerText(ProductHeatwaveSnapshot heatwave)
    {
        ProductHeatwaveSettlementSnapshot ledger = heatwave.Settlement;
        return
            $"폭염 {MinuteText(heatwave.StartMinute)} → 복구·결산 {MinuteText(heatwave.RecoveryMinute)} · " +
            $"선택 {MaintenanceChoiceText(heatwave.MaintenanceChoice)}\n" +
            $"세 수요처 전량공급 {YesNo(ledger.AllLoadsFullySupplied)} · " +
            $"사건 중 feeder {(heatwave.AgedFactoryFeederUnavailableDuringEvent ? "사용불가" : "사용 가능")} · " +
            $"유효정격 {PowerText(heatwave.ForecastFactoryFeederRatingKw)}\n" +
            $"기존 발전 {EnergyText(ledger.ExistingSourceGenerationEnergyKwMinute)} · " +
            $"가스발전 {EnergyText(ledger.GasPlantGenerationEnergyKwMinute)} · " +
            $"미공급 {EnergyText(ledger.UtilityUnservedEnergyKwMinute)}\n" +
            $"매출 {CashText(ledger.UtilityRevenueCashUnit)} · " +
            $"기존 발전비 {CashText(ledger.ExistingSourceGenerationCostCashUnit)} · " +
            $"가스 발전비 {CashText(ledger.GasPlantGenerationCostCashUnit)} · " +
            $"보상 {CashText(ledger.UnservedCompensationCashUnit)}\n" +
            $"현금 변화 {SignedCashText(ledger.CashChangeCashUnit)} · " +
            $"LostSales {CashText(ledger.LostSalesCashUnit)} (현금 미반영)";
    }

    private string StatusText(
        ProductHospitalSnapshot hospital,
        ProductFactorySnapshot factory,
        ProductHeatwaveSnapshot heatwave)
    {
        if (IsFinalHeatwaveResult(heatwave))
        {
            string eventFeeder = heatwave.AgedFactoryFeederUnavailableDuringEvent
                ? "사용불가"
                : "사용 가능";
            string recoveredFeeder = heatwave.AgedFactoryFeederCurrentlyUnavailable
                ? "사용불가"
                : heatwave.MaintenanceProjectState == ProductProjectState.Commissioned
                    ? "정비 완료"
                    : "복구 완료";
            return
                $"폭염 결과 · 사건 중 feeder {eventFeeder} · " +
                $"사건 유효정격 {PowerText(heatwave.ForecastFactoryFeederRatingKw)}\n" +
                $"복구 후 feeder {recoveredFeeder} · " +
                $"현재 정격 {PowerText(heatwave.CurrentFactoryFeederRatingKw)}";
        }
        if (IsHeatwaveDisplayStage(_snapshot.Phase, heatwave))
        {
            string feeder = heatwave.AgedFactoryFeederCurrentlyUnavailable
                ? "사용불가"
                : heatwave.MaintenanceProjectState == ProductProjectState.Building
                    ? "예방정비 중"
                    : heatwave.MaintenanceProjectState == ProductProjectState.Commissioned
                        ? "정비 완료"
                        : "정상";
            return
                $"폭염 {(_snapshot.Phase == ProductPhase.HeatwaveActive ? "진행 중" : "예고·결과")} · " +
                $"노후 feeder {feeder} · 유효정격 {PowerText(heatwave.CurrentFactoryFeederRatingKw)}\n" +
                $"기존 급전 {PowerText(heatwave.ExistingSourceDispatchKw)} · " +
                $"가스 급전 {PowerText(heatwave.GasPlantDispatchKw)}";
        }
        if (IsFactoryDisplayStage(_snapshot.Phase, factory))
        {
            string connection = factory.PlantGridConnected ? "계통접속" : "계통 미접속";
            return
                $"공장 {PowerText(factory.FactoryDeliveredKw)} / {PowerText(_factoryFixture.DemandKw)} · " +
                $"발전소 {connection}\n" +
                $"기존 급전 {PowerText(factory.ExistingSourceDispatchKw)} · " +
                $"가스 급전 {PowerText(factory.GasPlantDispatchKw)}";
        }
        if (!IsHospitalDisplayStage(_snapshot.Phase, hospital))
        {
            return SupplyText(_snapshot.SupplyFailure);
        }
        string reliability = hospital.Reliability.Evaluated
            ? $" · 단일회선 제거 안전 {YesNo(hospital.Reliability.AllSingleLineRemovalsKeepHospitalUtility)}"
            : string.Empty;
        return
            $"병원 utility {PowerText(hospital.HospitalUtilityKw)} / {PowerText(_hospitalFixture.DemandKw)} · " +
            $"P0 {PowerText(hospital.HospitalP0DeliveredKw)}{reliability}\n" +
            $"마을 utility {PowerText(hospital.TownUtilityKw)} / {PowerText(_fixture.Town.DemandKw)}";
    }

    private static string OrderPreviewText(ProductOrderPreview quote)
    {
        string projected = quote.ProjectedSupplyFailure.HasValue
            ? ProjectedSupplyText(quote.ProjectedSupplyFailure.Value)
            : "예상 공급을 계산할 수 없음";
        string error = quote.Error.HasValue ? ErrorText(quote.Error) : string.Empty;
        if (!quote.CostCashUnit.HasValue)
        {
            return error.Length > 0 ? error : projected;
        }
        string quoteText =
            $"견적 {CashText(quote.CostCashUnit.Value)} · 공기 {quote.BuildMinutes!.Value.ToString("N0", CultureInfo.InvariantCulture)}분";
        return error.Length > 0
            ? $"{quoteText}\n{error}\n{projected}"
            : $"{quoteText}\n{projected}";
    }

    private static string CompletionText(long? completionMinute) => completionMinute.HasValue
        ? $"예정 완공 · {completionMinute.Value.ToString("N0", CultureInfo.InvariantCulture)}분"
        : "예정 완공시각 없음";

    private static string MinuteText(long? minute) => minute.HasValue
        ? $"{minute.Value.ToString("N0", CultureInfo.InvariantCulture)}분"
        : "미정";

    private static string MaintenanceChoiceText(ProductMaintenanceChoice choice) => choice switch
    {
        ProductMaintenanceChoice.Undecided => "미정",
        ProductMaintenanceChoice.Ordered => "예방정비",
        ProductMaintenanceChoice.Skipped => "정비 생략",
        _ => throw new ArgumentOutOfRangeException(nameof(choice)),
    };

    private static FirstLightActionPresentation Action(
        bool visible,
        bool enabled,
        string text,
        string description) => new(visible, enabled, text, description);

    private FirstLightPointerPreview ToMapPreview(ProductSubstationPlacementPreview preview)
    {
        string description = !preview.Accepted
            ? ErrorText(preview.Error)
            : preview.TownInServiceArea
                ? "이 위치는 마을의 서비스 권역 조건을 만족합니다. 실제 공급에는 완공된 선로도 필요합니다."
                : "배치는 가능하지만 마을이 서비스 권역 밖이라 완공 뒤에도 공급되지 않습니다.";
        return new FirstLightPointerPreview(
            FirstLightPointerMode.Substation,
            ToGrid(preview.Position),
            null,
            preview.Accepted,
            description);
    }

    private static FirstLightPointerPreview ToMapPreview(ProductPlantPlacementPreview preview)
    {
        string description = preview.Accepted
            ? $"허용 부지 {preview.SiteId} · 부지비 {CashText(preview.SiteCostCashUnit!.Value)}"
            : ErrorText(preview.Error);
        return new FirstLightPointerPreview(
            FirstLightPointerMode.GasPlant,
            ToGrid(preview.Position),
            null,
            preview.Accepted,
            description);
    }

    private static FirstLightPointerPreview ToMapPreview(ProductLineSupportPreview preview)
    {
        string description = preview.Accepted
            ? $"span 거리² {preview.DistanceSquared} / 허용 {preview.MaxSpanSquared} · 배치 가능"
            : ErrorText(preview.Error);
        return new FirstLightPointerPreview(
            FirstLightPointerMode.LineSupport,
            ToGrid(preview.To),
            ToGrid(preview.From),
            preview.Accepted,
            description);
    }

    private async void RunShellSmoke()
    {
        try
        {
            await NextFrame();
            Require(_shell.Page == ProductShellPage.Title, "shell smoke did not start at Title");
            if (_options.ShellSmokeLeg == ProductShellSmokeLeg.Save)
            {
                await RunShellSaveLeg();
            }
            else if (_options.ShellSmokeLeg == ProductShellSmokeLeg.Continue)
            {
                await RunShellContinueLeg();
            }
            else
            {
                throw new InvalidOperationException("Shell smoke leg is missing.");
            }
        }
        catch (Exception exception)
        {
            GD.PushError($"PRODUCT_CAMPAIGN_SHELL_SMOKE_FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task RunShellSaveLeg()
    {
        Require(
            _settings.WindowMode == ProductWindowMode.Windowed &&
            _settings.UiScalePercent == 100 &&
            _settings.ShowControlHelp,
            "save leg did not start with default settings");

        EmitShellAction(ProductShellAction.NewGame, "start New Game");
        await NextFrame();
        if (_shell.Page == ProductShellPage.Confirm)
        {
            EmitShellAction(ProductShellAction.Confirm, "confirm New Game overwrite");
            await NextFrame();
        }
        Require(_shell.Page == ProductShellPage.Help, "New Game did not show startup control help");
        EmitShellAction(ProductShellAction.HelpBack, "close startup control help");
        await NextFrame();
        Require(_shell.Page == ProductShellPage.Hidden, "startup control help did not return to game");
        Require(_mapView.HasFocus(), "startup control help did not return keyboard focus to the map");

        FirstLightGridPoint firstSubstation = _options.SmokeSubstations[0];
        FirstLightGridPoint finalSubstation = _options.SmokeSubstations[1];
        await ClickMapPoint(firstSubstation);
        await ClickMapPoint(finalSubstation);
        Require(
            _snapshot.Substation.Position == ToProduct(finalSubstation),
            "save leg substation clicks did not round-trip through the viewport");
        EmitPanelAction(FirstLightPanelAction.Order, "order first-chapter substation");
        await NextFrame();
        EmitPanelAction(FirstLightPanelAction.Advance, "complete first-chapter substation");
        await NextFrame();
        await BuildLineThroughUi(
            _options.SmokeSupports,
            ProductPhase.LineBuilding,
            ProductPhase.SettlementReady,
            "first-chapter town line");
        EmitPanelAction(FirstLightPanelAction.Settle, "settle first chapter");
        await NextFrame();
        RequireSecondChapterStart(finalSubstation);

        EmitButton(_menuButton, "open Pause from header menu");
        await NextFrame();
        Require(_shell.Page == ProductShellPage.Pause, "header menu did not open Pause");
        EmitShellAction(ProductShellAction.PauseSettings, "open Pause settings");
        await NextFrame();
        Require(_shell.Page == ProductShellPage.Settings, "Pause settings did not open");

        OptionButton scale = _shell.GetUiScaleOption();
        scale.Select(1);
        scale.EmitSignal(OptionButton.SignalName.ItemSelected, 1L);
        await NextFrame();
        CheckButton help = _shell.GetControlHelpCheck();
        help.ButtonPressed = false;
        if (_settings.ShowControlHelp)
        {
            help.EmitSignal(BaseButton.SignalName.Toggled, false);
        }
        await NextFrame();
        Require(
            _settings.UiScalePercent == 125 && !_settings.ShowControlHelp,
            "settings controls did not persist the smoke choices in memory");

        EmitShellAction(ProductShellAction.SettingsBack, "return from settings");
        await NextFrame();
        Require(_shell.Page == ProductShellPage.Pause, "settings did not return to Pause");
        EmitShellAction(ProductShellAction.SaveAndQuit, "Save & Quit");
    }

    private async Task RunShellContinueLeg()
    {
        Require(
            _continuation is not null &&
            _settings.UiScalePercent == 125 &&
            !_settings.ShowControlHelp,
            "fresh continue process did not retain save and settings");
        EmitShellAction(ProductShellAction.Continue, "Continue saved campaign");
        await NextFrame();
        Require(_shell.Page == ProductShellPage.Hidden, "Continue did not enter gameplay");
        Require(_mapView.HasFocus(), "Continue did not return keyboard focus to the map");
        FirstLightGridPoint finalSubstation = _options.SmokeSubstations[1];
        RequireSecondChapterStart(finalSubstation);

        FirstLightGridPoint primarySupport = _options.SmokePrimarySupports.Single();
        int checkpoint = _run.ChapterStartCommandCount;
        await ClickMapPoint(primarySupport);
        Require(
            HospitalSnapshot().PrimaryLine.SupportPositions.Select(ToGrid)
                .SequenceEqual(new[] { primarySupport }) &&
            _run.CommandCount == checkpoint + 1,
            "second-chapter click was not accepted before restart");

        await PressEscape();
        Require(_shell.Page == ProductShellPage.Pause, "Escape did not open Pause");
        EmitShellAction(ProductShellAction.RestartChapter, "request Restart Chapter");
        await NextFrame();
        Require(_shell.Page == ProductShellPage.Confirm, "Restart Chapter did not ask for confirmation");
        EmitShellAction(ProductShellAction.Confirm, "confirm Restart Chapter");
        await NextFrame();

        Require(
            _shell.Page == ProductShellPage.Hidden &&
            _snapshot.Phase == ProductPhase.PrimaryPlanning &&
            HospitalSnapshot().PrimaryLine.SupportPositions.Count == 0 &&
            _run.CommandCount == checkpoint &&
            _run.ChapterStartCommandCount == checkpoint,
            "Restart Chapter did not restore the second-chapter checkpoint prefix");
        Require(_mapView.HasFocus(), "Restart Chapter did not return keyboard focus to the map");

        ProductCampaignSaveLoadResult load =
            ProductPersistenceStore.LoadCampaignSave(_campaignSavePath);
        Require(
            load.Status == ProductDocumentLoadStatus.Loaded &&
            load.Save?.Commands.Count == checkpoint,
            "Restart Chapter autosave did not replace the journal with its checkpoint prefix");
        ProductCampaignRun restored = ProductCampaignRun.Restore(
            _campaign,
            _fixture,
            _campaignHash,
            _fixtureHash,
            load.Save!);
        ProductSnapshot restoredSnapshot = restored.GetSnapshot();
        Require(
            restored.CurrentChapterId == "SECOND_HEART" &&
            restored.CommandCount == checkpoint &&
            restoredSnapshot.Phase == ProductPhase.PrimaryPlanning &&
            restoredSnapshot.Minute == _snapshot.Minute &&
            restoredSnapshot.Cash == _snapshot.Cash,
            "fresh replay after Restart Chapter did not reproduce the checkpoint state");

        GD.Print(
            $"PRODUCT_CAMPAIGN_CONTINUE_LEG_PASS session={_options.SessionId} chapter={_run.CurrentChapterId} commandCount={_run.CommandCount}");
        GetTree().Quit(0);
    }

    private void RequireSaveLegFinalState()
    {
        RequireSecondChapterStart(_options.SmokeSubstations[1]);
        ProductSettingsLoadResult settingsLoad =
            ProductPersistenceStore.LoadSettings(_settingsPath);
        ProductCampaignSaveLoadResult saveLoad =
            ProductPersistenceStore.LoadCampaignSave(_campaignSavePath);
        Require(
            settingsLoad.Status == ProductDocumentLoadStatus.Loaded &&
            settingsLoad.Settings.UiScalePercent == 125 &&
            !settingsLoad.Settings.ShowControlHelp,
            "Save & Quit did not retain changed settings");
        Require(
            saveLoad.Status == ProductDocumentLoadStatus.Loaded &&
            saveLoad.Save?.Commands.Count == _run.CommandCount,
            "Save & Quit did not retain the accepted campaign journal");
    }

    private void RequireSecondChapterStart(FirstLightGridPoint finalSubstation)
    {
        int expectedCommandCount = 7 + _options.SmokeSupports.Count;
        Require(
            _snapshot.Phase == ProductPhase.PrimaryPlanning &&
            _snapshot.Substation.Position == ToProduct(finalSubstation) &&
            _snapshot.Line.ProjectState == ProductProjectState.Commissioned &&
            _snapshot.Line.SupportPositions.Select(ToGrid)
                .SequenceEqual(_options.SmokeSupports) &&
            _snapshot.Minute == 360 &&
            _snapshot.Cash == 14_700_000 &&
            _run.CurrentChapterId == "SECOND_HEART" &&
            _run.ChapterStartCommandCount == expectedCommandCount &&
            _run.CommandCount == expectedCommandCount,
            "campaign did not preserve the exact second-chapter start state");
    }

    private async Task PressEscape()
    {
        GetViewport().PushInput(new InputEventKey
        {
            Keycode = Key.Escape,
            PhysicalKeycode = Key.Escape,
            Pressed = true,
        }, true);
        GetViewport().PushInput(new InputEventKey
        {
            Keycode = Key.Escape,
            PhysicalKeycode = Key.Escape,
            Pressed = false,
        }, true);
        await NextFrame();
    }

    private void EmitShellAction(ProductShellAction action, string description) =>
        EmitButton(_shell.GetActionButton(action), description);

    private static void EmitButton(BaseButton button, string description)
    {
        if (!button.Visible || button.Disabled)
        {
            throw new InvalidOperationException($"Missing enabled UI action for {description}.");
        }
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    private async void RunSmoke()
    {
        try
        {
            await NextFrame();
            FirstLightGridPoint firstSubstation = _options.SmokeSubstations[0];
            FirstLightGridPoint finalSubstation = _options.SmokeSubstations[1];

            await ClickMapPoint(firstSubstation);
            await ClickMapPoint(finalSubstation);
            Require(
                _snapshot.Phase == ProductPhase.SubstationPlanning &&
                _snapshot.Substation.Position == ToProduct(finalSubstation),
                "substation draft move did not round-trip through viewport input");
            EmitPanelAction(FirstLightPanelAction.Order, "order substation");
            await NextFrame();
            Require(_snapshot.Phase == ProductPhase.SubstationBuilding, "substation order failed");
            EmitPanelAction(FirstLightPanelAction.Advance, "complete substation");
            await NextFrame();
            Require(_snapshot.Phase == ProductPhase.LinePlanning, "substation completion failed");

            await BuildLineThroughUi(
                _options.SmokeSupports,
                ProductPhase.LineBuilding,
                ProductPhase.SettlementReady,
                "town line");
            Require(
                _snapshot.TownDeliveredKw == _fixture.Town.DemandKw,
                "commissioned product path did not supply the town");
            EmitPanelAction(FirstLightPanelAction.Settle, "settle first light");
            await NextFrame();
            Require(_snapshot.Phase == ProductPhase.PrimaryPlanning, "first settlement did not open primary planning");

            await BuildLineThroughUi(
                _options.SmokePrimarySupports,
                ProductPhase.PrimaryBuilding,
                ProductPhase.BackupPlanning,
                "hospital primary line");
            await BuildLineThroughUi(
                _options.SmokeBackupSupports,
                ProductPhase.BackupBuilding,
                ProductPhase.IncidentReady,
                "hospital backup line");

            ProductReliabilitySnapshot reliability = _run.PreviewReliability();
            Require(
                reliability.AllSingleLineRemovalsKeepHospitalUtility,
                "hospital lines did not survive each single-line removal");
            EmitPanelAction(FirstLightPanelAction.Settle, "start spatial incident");
            await NextFrame();
            ProductHospitalSnapshot incident = HospitalSnapshot();
            Require(
                _snapshot.Phase == ProductPhase.IncidentActive &&
                incident.Incident.Active &&
                incident.HospitalUtilityKw == _hospitalFixture.DemandKw &&
                incident.HospitalP0DeliveredKw == _hospitalFixture.DemandKw,
                "spatial incident did not preserve hospital utility and P0");

            EmitPanelAction(FirstLightPanelAction.Settle, "recover and settle incident");
            await NextFrame();
            Require(
                _snapshot.Phase == ProductPhase.PlantPlanning &&
                _snapshot.Outcome == ProductMissionOutcome.Pending,
                "hospital settlement did not open plant planning");

            FirstLightGridPoint plantPoint = _options.SmokePlant
                ?? throw new InvalidOperationException("Factory smoke plant coordinate is missing.");
            await ClickMapPoint(plantPoint);
            ProductFactorySnapshot factory = FactorySnapshot();
            Require(
                factory.PlantPosition == ToProduct(plantPoint) &&
                factory.SelectedSiteId is not null,
                "plant site click did not round-trip through viewport input");
            EmitPanelAction(FirstLightPanelAction.Order, "order gas plant");
            await NextFrame();
            Require(_snapshot.Phase == ProductPhase.PlantBuilding, "gas plant order failed");
            EmitPanelAction(FirstLightPanelAction.Advance, "complete gas plant");
            await NextFrame();
            factory = FactorySnapshot();
            Require(
                _snapshot.Phase == ProductPhase.PlantConnectionPlanning &&
                factory.PlantProjectState == ProductProjectState.Commissioned &&
                !factory.PlantGridConnected &&
                factory.GasPlantDispatchKw == 0,
                "commissioned but disconnected plant did not remain at zero output");

            await BuildLineThroughUi(
                _options.SmokePlantSupports,
                ProductPhase.PlantConnectionBuilding,
                ProductPhase.FactorySettlementReady,
                "gas plant connection line");
            factory = FactorySnapshot();
            Require(
                factory.ConnectionLine.SupportPositions.Select(ToGrid)
                    .SequenceEqual(_options.SmokePlantSupports) &&
                factory.PlantGridConnected &&
                factory.FactoryDeliveredKw == _factoryFixture.DemandKw,
                "commissioned plant connection did not supply the factory");

            EmitPanelAction(FirstLightPanelAction.Settle, "settle factory supply period");
            await NextFrame();
            factory = FactorySnapshot();
            Require(
                _snapshot.Phase == ProductPhase.MaintenanceDecision &&
                _snapshot.Outcome == ProductMissionOutcome.Pending &&
                factory.Settlement.Completed &&
                factory.Settlement.AllLoadsFullySupplied &&
                _snapshot.Cash == 5_820_000 &&
                _snapshot.Minute == 1_425,
                "factory settlement did not open the fixed heatwave forecast");

            ProductHeatwaveSnapshot heatwave = HeatwaveSnapshot();
            Require(
                heatwave.StartMinute == 1_605 &&
                heatwave.RecoveryMinute == 1_845 &&
                heatwave.MaintenanceChoice == ProductMaintenanceChoice.Undecided,
                "heatwave forecast milestones were not anchored exactly");

            EmitPanelAction(FirstLightPanelAction.Order, "order preventive maintenance");
            await NextFrame();
            heatwave = HeatwaveSnapshot();
            Require(
                _snapshot.Phase == ProductPhase.MaintenanceBuilding &&
                heatwave.MaintenanceChoice == ProductMaintenanceChoice.Ordered &&
                heatwave.MaintenanceProjectState == ProductProjectState.Building &&
                heatwave.MaintenanceCompletionMinute == 1_545 &&
                _snapshot.Cash == 3_820_000,
                "preventive maintenance order did not preserve the exact cost and completion");

            EmitPanelAction(FirstLightPanelAction.Advance, "complete preventive maintenance");
            await NextFrame();
            heatwave = HeatwaveSnapshot();
            Require(
                _snapshot.Phase == ProductPhase.HeatwaveReady &&
                heatwave.MaintenanceProjectState == ProductProjectState.Commissioned &&
                _snapshot.Minute == 1_545,
                "preventive maintenance did not complete through the standard advance button");

            EmitPanelAction(FirstLightPanelAction.Settle, "start fixed heatwave");
            await NextFrame();
            heatwave = HeatwaveSnapshot();
            Require(
                _snapshot.Phase == ProductPhase.HeatwaveActive &&
                heatwave.Active &&
                !heatwave.AgedFactoryFeederCurrentlyUnavailable &&
                heatwave.CurrentTownDemandKw == _heatwaveFixture.TownDemandKw &&
                heatwave.CurrentFactoryFeederRatingKw ==
                    _heatwaveFixture.AgedFactoryFeederHeatwaveRatingKw &&
                heatwave.HospitalDeliveredKw == _hospitalFixture.DemandKw &&
                heatwave.TownDeliveredKw == _heatwaveFixture.TownDemandKw &&
                heatwave.FactoryDeliveredKw == _factoryFixture.DemandKw &&
                _snapshot.Minute == 1_605,
                "maintained feeder did not preserve full supply during the fixed heatwave");

            EmitPanelAction(FirstLightPanelAction.Settle, "recover and settle heatwave");
            await NextFrame();
            heatwave = HeatwaveSnapshot();
            Require(
                _snapshot.Phase == ProductPhase.Complete &&
                _snapshot.Outcome == ProductMissionOutcome.Success &&
                heatwave.Settlement.Completed &&
                heatwave.Settlement.AllLoadsFullySupplied &&
                !heatwave.AgedFactoryFeederUnavailableDuringEvent &&
                !heatwave.AgedFactoryFeederCurrentlyUnavailable &&
                _snapshot.Cash == 4_660_000 &&
                _snapshot.Minute == 1_845 &&
                _finalLogged,
                "Heatwave Maintenance smoke did not reach the exact maintained settlement");

            GD.Print(
                $"PRODUCT_HEATWAVE_MAINTENANCE_SMOKE_PASS session={_options.SessionId} endingCash={_snapshot.Cash} minute={_snapshot.Minute}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"PRODUCT_HEATWAVE_MAINTENANCE_SMOKE_FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task BuildLineThroughUi(
        IReadOnlyList<FirstLightGridPoint> supports,
        ProductPhase buildingPhase,
        ProductPhase completedPhase,
        string description)
    {
        foreach (FirstLightGridPoint support in supports)
        {
            await ClickMapPoint(support);
        }
        Require(
            ActiveSupports(_snapshot).Select(ToGrid).SequenceEqual(supports),
            $"{description} support clicks did not round-trip through viewport input");
        EmitPanelAction(FirstLightPanelAction.Order, $"order {description}");
        await NextFrame();
        Require(_snapshot.Phase == buildingPhase, $"{description} order failed");
        EmitPanelAction(FirstLightPanelAction.Advance, $"complete {description}");
        await NextFrame();
        Require(_snapshot.Phase == completedPhase, $"{description} completion failed");
    }

    private async Task ClickMapPoint(FirstLightGridPoint point)
    {
        Vector2 viewportPoint = _mapView.ViewportPointForGridPoint(point);
        GetViewport().PushInput(new InputEventMouseMotion
        {
            Position = viewportPoint,
            GlobalPosition = viewportPoint,
        }, true);
        GetViewport().PushInput(new InputEventMouseButton
        {
            Position = viewportPoint,
            GlobalPosition = viewportPoint,
            ButtonIndex = MouseButton.Left,
            Pressed = true,
        }, true);
        GetViewport().PushInput(new InputEventMouseButton
        {
            Position = viewportPoint,
            GlobalPosition = viewportPoint,
            ButtonIndex = MouseButton.Left,
            Pressed = false,
        }, true);
        await NextFrame();
    }

    private void EmitPanelAction(FirstLightPanelAction action, string description)
    {
        BaseButton button = _taskPanel.GetActionButton(action);
        if (!button.Visible || button.Disabled)
        {
            throw new InvalidOperationException($"Missing enabled UI action for {description}.");
        }
        button.EmitSignal(BaseButton.SignalName.Pressed);
    }

    private async Task NextFrame() =>
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static ProductPoint ToProduct(FirstLightGridPoint point) => new(point.X, point.Y);

    private static FirstLightGridPoint ToGrid(ProductPoint point) => new(point.X, point.Y);

    private ProductHospitalSnapshot HospitalSnapshot() =>
        _snapshot.Hospital
        ?? throw new InvalidOperationException("Product session has no hospital snapshot.");

    private ProductFactorySnapshot FactorySnapshot() =>
        _snapshot.Factory
        ?? throw new InvalidOperationException("Product session has no factory snapshot.");

    private ProductHeatwaveSnapshot HeatwaveSnapshot() =>
        _snapshot.Heatwave
        ?? throw new InvalidOperationException("Product session has no heatwave snapshot.");

    private ProductPoint ActiveTarget() => _snapshot.Phase switch
    {
        ProductPhase.LinePlanning => _snapshot.Substation.Position
            ?? throw new InvalidOperationException("Town line target is missing."),
        ProductPhase.PrimaryPlanning or ProductPhase.BackupPlanning => _hospitalFixture.Position,
        ProductPhase.PlantConnectionPlanning => _fixture.ExistingSource.Position,
        _ => throw new InvalidOperationException("There is no active line target."),
    };

    private ProductPoint ActiveLineStart() => _snapshot.Phase switch
    {
        ProductPhase.PlantConnectionPlanning => FactorySnapshot().PlantPosition
            ?? throw new InvalidOperationException("Plant connection start is missing."),
        _ => _fixture.ExistingSource.Position,
    };

    private static IReadOnlyList<ProductPoint> ActiveSupports(ProductSnapshot snapshot) =>
        snapshot.Phase switch
        {
            ProductPhase.LinePlanning or ProductPhase.LineBuilding => snapshot.Line.SupportPositions,
            ProductPhase.PrimaryPlanning or ProductPhase.PrimaryBuilding =>
                snapshot.Hospital?.PrimaryLine.SupportPositions ?? Array.Empty<ProductPoint>(),
            ProductPhase.BackupPlanning or ProductPhase.BackupBuilding =>
                snapshot.Hospital?.BackupLine.SupportPositions ?? Array.Empty<ProductPoint>(),
            ProductPhase.PlantConnectionPlanning or ProductPhase.PlantConnectionBuilding =>
                snapshot.Factory?.ConnectionLine.SupportPositions ?? Array.Empty<ProductPoint>(),
            _ => Array.Empty<ProductPoint>(),
        };

    private string? ActiveProjectId(ProductSnapshot snapshot) => snapshot.Phase switch
    {
        ProductPhase.SubstationPlanning or ProductPhase.SubstationBuilding =>
            _fixture.SubstationProject.ProjectId,
        ProductPhase.LinePlanning or ProductPhase.LineBuilding => _fixture.LineProject.ProjectId,
        ProductPhase.PlantPlanning or ProductPhase.PlantBuilding => _plantFixture.ProjectId,
        ProductPhase.PlantConnectionPlanning or ProductPhase.PlantConnectionBuilding =>
            _fixture.PlantConnectionLineProject?.ProjectId,
        ProductPhase.MaintenanceDecision or ProductPhase.MaintenanceBuilding =>
            _maintenanceFixture.ProjectId,
        _ => snapshot.Hospital?.ActiveProjectId,
    };

    private static bool IsLinePlanning(ProductPhase phase) => phase is
        ProductPhase.LinePlanning or ProductPhase.PrimaryPlanning or ProductPhase.BackupPlanning or
        ProductPhase.PlantConnectionPlanning;

    private static bool IsHospitalStage(ProductPhase phase) => phase is
        ProductPhase.PrimaryPlanning or ProductPhase.PrimaryBuilding or
        ProductPhase.BackupPlanning or ProductPhase.BackupBuilding or
        ProductPhase.IncidentReady or ProductPhase.IncidentActive;

    private static bool IsFactoryStage(ProductPhase phase) => phase is
        ProductPhase.PlantPlanning or ProductPhase.PlantBuilding or
        ProductPhase.PlantConnectionPlanning or ProductPhase.PlantConnectionBuilding or
        ProductPhase.FactorySettlementReady;

    private static bool IsHeatwaveStage(ProductPhase phase) => phase is
        ProductPhase.MaintenanceDecision or ProductPhase.MaintenanceBuilding or
        ProductPhase.HeatwaveReady or ProductPhase.HeatwaveActive;

    private static bool IsFactoryDisplayStage(
        ProductPhase phase,
        ProductFactorySnapshot factory) =>
        IsFactoryStage(phase) ||
        phase == ProductPhase.Complete && factory.Settlement.Completed;

    private static bool IsHeatwaveDisplayStage(
        ProductPhase phase,
        ProductHeatwaveSnapshot heatwave) =>
        IsHeatwaveStage(phase) ||
        phase == ProductPhase.Complete && heatwave.Settlement.Completed;

    private static bool IsHospitalDisplayStage(
        ProductPhase phase,
        ProductHospitalSnapshot hospital) =>
        IsHospitalStage(phase) ||
        phase == ProductPhase.Complete && hospital.Settlement.Completed;

    private string CurrentPhaseText(
        ProductHospitalSnapshot hospital,
        ProductFactorySnapshot factory,
        ProductHeatwaveSnapshot heatwave) =>
        _snapshot.Phase != ProductPhase.Complete
            ? PhaseText(_snapshot.Phase)
            : !hospital.Settlement.Completed
                ? "6 · 첫 결산"
                : !factory.Settlement.Completed
                    ? "12 · 복구와 결산"
                    : heatwave.Settlement.Completed
                        ? "22 · 폭염 복구·결산"
                        : "17 · 공장 공급 결산";

    private long DisplayTownUtility(
        ProductHospitalSnapshot hospital,
        ProductFactorySnapshot factory,
        ProductHeatwaveSnapshot heatwave) =>
        IsHeatwaveDisplayStage(_snapshot.Phase, heatwave)
            ? heatwave.TownDeliveredKw
            : IsFactoryDisplayStage(_snapshot.Phase, factory)
            ? factory.TownDeliveredKw
            : IsHospitalDisplayStage(_snapshot.Phase, hospital)
            ? hospital.TownUtilityKw
            : _snapshot.TownDeliveredKw;

    private long DisplayHospitalUtility(
        ProductHospitalSnapshot hospital,
        ProductFactorySnapshot factory,
        ProductHeatwaveSnapshot heatwave) =>
        IsHeatwaveDisplayStage(_snapshot.Phase, heatwave)
            ? heatwave.HospitalDeliveredKw
            : IsFactoryDisplayStage(_snapshot.Phase, factory)
            ? factory.HospitalDeliveredKw
            : hospital.HospitalUtilityKw;

    private long DisplayFactoryUtility(
        ProductFactorySnapshot factory,
        ProductHeatwaveSnapshot heatwave) =>
        IsHeatwaveDisplayStage(_snapshot.Phase, heatwave)
            ? heatwave.FactoryDeliveredKw
            : factory.FactoryDeliveredKw;

    private long DisplayTownDemand(ProductHeatwaveSnapshot heatwave) =>
        IsFinalHeatwaveResult(heatwave)
            ? heatwave.ForecastTownDemandKw
            : IsHeatwaveDisplayStage(_snapshot.Phase, heatwave)
            ? heatwave.CurrentTownDemandKw
            : _fixture.Town.DemandKw;

    private long DisplayFactoryFeederRating(ProductHeatwaveSnapshot heatwave) =>
        IsFinalHeatwaveResult(heatwave)
            ? heatwave.ForecastFactoryFeederRatingKw
            : IsHeatwaveDisplayStage(_snapshot.Phase, heatwave)
                ? heatwave.CurrentFactoryFeederRatingKw
                : _factoryFixture.FeederRatingKw;

    private bool IsFinalHeatwaveResult(ProductHeatwaveSnapshot heatwave) =>
        _snapshot.Phase == ProductPhase.Complete && heatwave.Settlement.Completed;

    private FirstLightFactoryFeederVisualState FactoryFeederVisualState(
        ProductHeatwaveSnapshot heatwave)
    {
        bool unavailable = IsFinalHeatwaveResult(heatwave)
            ? heatwave.AgedFactoryFeederUnavailableDuringEvent
            : heatwave.AgedFactoryFeederCurrentlyUnavailable;
        if (unavailable)
        {
            return FirstLightFactoryFeederVisualState.Unavailable;
        }
        return heatwave.MaintenanceProjectState switch
        {
            ProductProjectState.Building => FirstLightFactoryFeederVisualState.Maintenance,
            ProductProjectState.Commissioned => FirstLightFactoryFeederVisualState.Maintained,
            _ => FirstLightFactoryFeederVisualState.Normal,
        };
    }

    private static FirstLightProjectVisualState VisualState(
        ProductProjectState state,
        bool unavailable)
    {
        if (unavailable)
        {
            return FirstLightProjectVisualState.Unavailable;
        }
        return state switch
        {
            ProductProjectState.NotOrdered => FirstLightProjectVisualState.NotOrdered,
            ProductProjectState.Building => FirstLightProjectVisualState.Building,
            ProductProjectState.Commissioned => FirstLightProjectVisualState.Commissioned,
            _ => throw new ArgumentOutOfRangeException(nameof(state)),
        };
    }

    private static string PhaseText(ProductPhase phase) => phase switch
    {
        ProductPhase.SubstationPlanning => "1 · 변전소 계획",
        ProductPhase.SubstationBuilding => "2 · 변전소 공사",
        ProductPhase.LinePlanning => "3 · 선로 계획",
        ProductPhase.LineBuilding => "4 · 선로 공사",
        ProductPhase.SettlementReady => "5 · 공급 확인",
        ProductPhase.PrimaryPlanning => "6 · 병원 주회선 계획",
        ProductPhase.PrimaryBuilding => "7 · 병원 주회선 공사",
        ProductPhase.BackupPlanning => "8 · 병원 예비회선 계획",
        ProductPhase.BackupBuilding => "9 · 병원 예비회선 공사",
        ProductPhase.IncidentReady => "10 · 신뢰도 확인",
        ProductPhase.IncidentActive => "11 · 공간사건",
        ProductPhase.PlantPlanning => "12 · 공장 증설 브리핑",
        ProductPhase.PlantBuilding => "13 · 가스발전소 공사",
        ProductPhase.PlantConnectionPlanning => "14 · 발전소 접속선 계획",
        ProductPhase.PlantConnectionBuilding => "15 · 발전소 접속선 공사",
        ProductPhase.FactorySettlementReady => "16 · 공장 공급 확인",
        ProductPhase.MaintenanceDecision => "18 · 폭염 예고와 정비 선택",
        ProductPhase.MaintenanceBuilding => "19 · 예방정비 공사",
        ProductPhase.HeatwaveReady => "20 · 폭염 시작 전 확인",
        ProductPhase.HeatwaveActive => "21 · 폭염 진행",
        ProductPhase.Complete => "22 · 폭염 복구·결산",
        _ => throw new ArgumentOutOfRangeException(nameof(phase)),
    };

    private static string SupplyText(ProductSupplyFailure failure) => failure switch
    {
        ProductSupplyFailure.SubstationNotCommissioned => "마을 미공급 · 변전소가 아직 완공되지 않음",
        ProductSupplyFailure.LineNotCommissioned => "마을 미공급 · 선로가 아직 완공되지 않음",
        ProductSupplyFailure.OutsideServiceArea => "마을 미공급 · 서비스 권역 밖",
        ProductSupplyFailure.SourceCapacityInsufficient => "마을 미공급 · 발전원 정격 부족",
        ProductSupplyFailure.LineCapacityInsufficient => "마을 미공급 · 선로 정격 부족",
        ProductSupplyFailure.SubstationCapacityInsufficient => "마을 미공급 · 변전소 정격 부족",
        ProductSupplyFailure.None => "마을 공급 중 · 완공된 경로와 서비스 권역 성립",
        _ => throw new ArgumentOutOfRangeException(nameof(failure)),
    };

    private static string ProjectedSupplyText(ProductSupplyFailure failure) => failure switch
    {
        ProductSupplyFailure.OutsideServiceArea => "완공 후 예상 미공급 · 서비스 권역 밖",
        ProductSupplyFailure.SourceCapacityInsufficient => "완공 후 예상 미공급 · 발전원 정격 부족",
        ProductSupplyFailure.LineCapacityInsufficient => "완공 후 예상 미공급 · 선로 정격 부족",
        ProductSupplyFailure.SubstationCapacityInsufficient => "완공 후 예상 미공급 · 변전소 정격 부족",
        ProductSupplyFailure.None => "완공 후 예상 공급 가능 · 경로와 서비스 권역 성립",
        ProductSupplyFailure.SubstationNotCommissioned => "완공 후 예상 미공급 · 변전소 조건 미충족",
        ProductSupplyFailure.LineNotCommissioned => "완공 후 예상 미공급 · 선로 조건 미충족",
        _ => throw new ArgumentOutOfRangeException(nameof(failure)),
    };

    private static string ErrorText(ProductCommandError? error) => error switch
    {
        ProductCommandError.WrongPhase => "WRONG_PHASE · 현재 단계에서는 실행할 수 없습니다.",
        ProductCommandError.NoDraft => "NO_DRAFT · 먼저 초안을 배치하세요.",
        ProductCommandError.OutOfBounds => "OUT_OF_BOUNDS · 지도 경계 안을 선택하세요.",
        ProductCommandError.NotBuildable => "NOT_BUILDABLE · 건설 불가 위치입니다.",
        ProductCommandError.PositionOccupied => "POSITION_OCCUPIED · 이미 사용 중인 위치입니다.",
        ProductCommandError.SpanTooLong => "SPAN_TOO_LONG · 거리 제한 안에 중간 지지물이 필요합니다.",
        ProductCommandError.NothingToUndo => "NOTHING_TO_UNDO · 되돌릴 지지물이 없습니다.",
        ProductCommandError.InsufficientCash => "INSUFFICIENT_CASH · 발주할 현금이 부족합니다.",
        null => string.Empty,
        _ => "알 수 없는 명령 오류입니다.",
    };

    private static string CashText(long cashUnit) =>
        $"{(cashUnit / 1_000_000d).ToString("0.000", CultureInfo.InvariantCulture)} M";

    private static string PowerText(long kw) =>
        $"{(kw / 1_000d).ToString("0.###", CultureInfo.InvariantCulture)} MW";

    private static string SignedCashText(long cashUnit) =>
        $"{(cashUnit / 1_000_000d).ToString("+0.000;-0.000;0.000", CultureInfo.InvariantCulture)} M";

    private static string EnergyText(long kwMinute) =>
        $"{(kwMinute / 60_000d).ToString("0.###", CultureInfo.InvariantCulture)} MWh";

    private static string YesNo(bool value) => value ? "충족" : "미충족";

    private static string Machine<T>(T value) where T : struct, Enum
    {
        string source = value.ToString();
        var result = new StringBuilder(source.Length + 8);
        for (int index = 0; index < source.Length; index++)
        {
            char current = source[index];
            if (index > 0 && char.IsUpper(current) &&
                (char.IsLower(source[index - 1]) ||
                 (index + 1 < source.Length && char.IsLower(source[index + 1]))))
            {
                result.Append('_');
            }
            result.Append(char.ToUpperInvariant(current));
        }
        return result.ToString();
    }

    private static string ComputeBuildHash()
    {
        string gameDirectory = Path.GetFullPath(ProjectSettings.GlobalizePath("res://"));
        string repositoryRoot = new DirectoryInfo(gameDirectory).Parent?.FullName
            ?? throw new InvalidOperationException("Game directory has no repository parent.");
        string coreDirectory = Path.Combine(repositoryRoot, "src", "Gridworks.Core");
        var components = new List<string>
        {
            Path.Combine(repositoryRoot, "Directory.Build.props"),
            Path.Combine(repositoryRoot, "global.json"),
            Path.Combine(coreDirectory, "Gridworks.Core.csproj"),
            Path.Combine(gameDirectory, "Gridworks.Game.csproj"),
            Path.Combine(gameDirectory, "project.godot"),
        };
        components.AddRange(Directory.EnumerateFiles(coreDirectory, "*.cs", SearchOption.AllDirectories));
        components.AddRange(Directory.EnumerateFiles(gameDirectory, "*.cs", SearchOption.TopDirectoryOnly));
        components.AddRange(Directory.EnumerateFiles(gameDirectory, "*.tscn", SearchOption.TopDirectoryOnly));

        var manifest = new StringBuilder();
        foreach (string path in components
                     .Where(path => !GeneratedPath(path))
                     .Select(Path.GetFullPath)
                     .Distinct(StringComparer.Ordinal)
                     .OrderBy(path => Path.GetRelativePath(repositoryRoot, path), StringComparer.Ordinal))
        {
            string label = Path.GetRelativePath(repositoryRoot, path).Replace('\\', '/');
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"Build-hash component '{label}' was not found.", path);
            }
            manifest.Append(label)
                .Append(':')
                .Append(LowerHex(SHA256.HashData(File.ReadAllBytes(path))))
                .Append('\n');
        }
        return LowerHex(SHA256.HashData(Encoding.UTF8.GetBytes(manifest.ToString())));
    }

    private static bool GeneratedPath(string path)
    {
        string normalized = path.Replace('\\', '/');
        return normalized.Contains("/bin/", StringComparison.Ordinal) ||
               normalized.Contains("/obj/", StringComparison.Ordinal) ||
               normalized.Contains("/.godot/", StringComparison.Ordinal);
    }

    private static string LowerHex(byte[] bytes) => Convert.ToHexString(bytes).ToLowerInvariant();

    private void ShowFatalError(string message)
    {
        var overlay = new ColorRect
        {
            Color = Color.FromHtml("071019"),
            LayoutMode = 1,
            AnchorsPreset = (int)LayoutPreset.FullRect,
            AnchorRight = 1f,
            AnchorBottom = 1f,
            GrowHorizontal = GrowDirection.Both,
            GrowVertical = GrowDirection.Both,
        };
        AddChild(overlay);
        var label = new Label
        {
            Text = $"공장 용량 확장을 시작할 수 없습니다.\n\n{message}",
            Position = new Vector2(100f, 180f),
            Size = new Vector2(1080f, 280f),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart,
            AccessibilityLive = AccessibilityServer.AccessibilityLiveMode.Assertive,
        };
        label.AddThemeFontSizeOverride("font_size", 20);
        overlay.AddChild(label);
    }
}
