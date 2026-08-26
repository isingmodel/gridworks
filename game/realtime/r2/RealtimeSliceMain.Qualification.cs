using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Gridworks.Core.Release.V3;
using Gridworks.Game.Realtime.UI;
using Godot;

namespace Gridworks.Game.Realtime.R2;

/// <summary>
/// Exact-package lifecycle qualification only. The product remains unchanged
/// unless both qualification environment variables select one fixed scenario.
/// </summary>
internal sealed partial class RealtimeSliceMain
{
    private const string QualificationScenarioEnvironment =
        "GRIDWORKS_R2_QUALIFICATION_SCENARIO";

    private enum LifecycleQualificationScenario
    {
        EmptyNewGame,
        ProgressContinue,
        CompletedContinue,
        CompletedNewGame,
        ResetNewGame,
        SettingsApply,
        SettingsRestore,
    }

    private static RealtimeProductSettings QualificationSettings { get; } = new(
        RealtimeProductSettings.SupportedSchemaVersion,
        RealtimeProductWindowMode.Fullscreen,
        200,
        0,
        25,
        75,
        ReduceMotion: true);

    private LifecycleQualificationScenario? _lifecycleQualificationScenario;
    private int _lifecyclePointerInputCount;
    private int _lifecycleKeyInputCount;

    private void ConfigureLifecycleQualification(int userArgumentCount)
    {
        string? raw = System.Environment.GetEnvironmentVariable(
            QualificationScenarioEnvironment);
        if (raw is null)
        {
            return;
        }
        if (_qualificationUserDataDirectory is null)
        {
            throw new InvalidOperationException(
                $"{QualificationScenarioEnvironment} requires " +
                $"{QualificationUserDataEnvironment}.");
        }
        if (userArgumentCount != 0)
        {
            throw new InvalidOperationException(
                $"{QualificationScenarioEnvironment} requires zero app user arguments.");
        }

        _lifecycleQualificationScenario = raw switch
        {
            "EMPTY_NEW_GAME" => LifecycleQualificationScenario.EmptyNewGame,
            "PROGRESS_CONTINUE" => LifecycleQualificationScenario.ProgressContinue,
            "COMPLETED_CONTINUE" => LifecycleQualificationScenario.CompletedContinue,
            "COMPLETED_NEW_GAME" => LifecycleQualificationScenario.CompletedNewGame,
            "RESET_NEW_GAME" => LifecycleQualificationScenario.ResetNewGame,
            "SETTINGS_APPLY" => LifecycleQualificationScenario.SettingsApply,
            "SETTINGS_RESTORE" => LifecycleQualificationScenario.SettingsRestore,
            _ => throw new InvalidOperationException(
                $"{QualificationScenarioEnvironment} is not a fixed supported scenario."),
        };
    }

    private void StartLifecycleQualificationIfConfigured()
    {
        if (_lifecycleQualificationScenario.HasValue)
        {
            _ = RunLifecycleQualificationAsync(
                _lifecycleQualificationScenario.Value);
        }
    }

    private async Task RunLifecycleQualificationAsync(
        LifecycleQualificationScenario scenario)
    {
        try
        {
            await SettleLifecycleFrames(4);
            RealtimeProductTitle title =
                (_ui ?? throw new InvalidOperationException(
                    "Lifecycle qualification lost the UI owner."))
                .GetNode<RealtimeProductTitle>("%ProductTitle");
            ValidateLifecycleTitleBootstrap(scenario, title);

            switch (scenario)
            {
                case LifecycleQualificationScenario.EmptyNewGame:
                    await QualifyEmptyNewGame(title);
                    break;
                case LifecycleQualificationScenario.ProgressContinue:
                    await QualifyProgressContinue(title);
                    break;
                case LifecycleQualificationScenario.CompletedContinue:
                    await QualifyCompletedContinue(title);
                    break;
                case LifecycleQualificationScenario.CompletedNewGame:
                    await QualifyCompletedNewGame(title);
                    break;
                case LifecycleQualificationScenario.ResetNewGame:
                    await QualifyResetNewGame(title);
                    break;
                case LifecycleQualificationScenario.SettingsApply:
                    await QualifySettingsApply(title);
                    break;
                case LifecycleQualificationScenario.SettingsRestore:
                    await QualifySettingsRestore(title);
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(scenario));
            }

            RealtimeAudioQualificationFacts audio =
                (_audio ?? throw new InvalidOperationException(
                    "Lifecycle qualification lost the audio owner."))
                .CaptureQualificationFacts();
            RequireLifecycle(
                audio is
                {
                    AmbientStarts: 1,
                    LiveCues: 0,
                    AmbientReady: true,
                    SfxQuiet: true,
                },
                "Generated audio was not one-start ambient with quiet SFX.");

            (string expectedTitle, string expectedSession,
                string expectedSettings, string expectedSave) =
                ExpectedLifecycleOutcome(scenario);
            string actualTitle = title.Visible ? "VISIBLE" : "HIDDEN";
            string actualSession = ClassifyLifecycleSession();
            RequireLifecycle(
                string.Equals(actualTitle, expectedTitle, StringComparison.Ordinal) &&
                string.Equals(actualSession, expectedSession, StringComparison.Ordinal),
                $"Lifecycle outcome drifted: title={actualTitle}, session={actualSession}.");

            GD.Print(
                "REALTIME_R2_QUALIFICATION_LIFECYCLE_READY " +
                $"scenario={ScenarioName(scenario)} " +
                $"pointer_inputs={_lifecyclePointerInputCount} " +
                $"key_inputs={_lifecycleKeyInputCount} " +
                $"title={actualTitle} session={actualSession} " +
                $"settings={expectedSettings} save={expectedSave} " +
                "audio=AMBIENT_READY_SFX_QUIET");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            _progressPersistenceOwnership = ProgressPersistenceOwnership.None;
            GD.PushError(
                $"R2 lifecycle qualification failed: {exception.Message}");
            GetTree().Quit(1);
        }
    }

    private void ValidateLifecycleTitleBootstrap(
        LifecycleQualificationScenario scenario,
        RealtimeProductTitle title)
    {
        RequireLifecycle(
            _launch.Kind == RealtimeLaunchKind.ProductTitle &&
            _session is null &&
            _productTitlePresentation is not null &&
            title.Visible &&
            _worldControl?.Visible == false &&
            _ui is { SettingsVisible: false },
            "Lifecycle qualification did not start at the exact product title.");

        QualificationContinuationClass expectedContinuation = scenario switch
        {
            LifecycleQualificationScenario.EmptyNewGame or
            LifecycleQualificationScenario.SettingsApply or
            LifecycleQualificationScenario.SettingsRestore =>
                QualificationContinuationClass.Missing,
            LifecycleQualificationScenario.ProgressContinue or
            LifecycleQualificationScenario.ResetNewGame =>
                QualificationContinuationClass.Restorable,
            LifecycleQualificationScenario.CompletedContinue or
            LifecycleQualificationScenario.CompletedNewGame =>
                QualificationContinuationClass.Completed,
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };
        RequireLifecycle(
            _qualificationContinuation == expectedContinuation,
            $"Lifecycle continuation fixture was {_qualificationContinuation}, " +
            $"expected {expectedContinuation}.");

        bool restoredSettings =
            scenario == LifecycleQualificationScenario.SettingsRestore;
        RequireLifecycle(
            _settingsLoadStatus == (restoredSettings
                ? RealtimeProductSettingsLoadStatus.Loaded
                : RealtimeProductSettingsLoadStatus.Missing) &&
            _settings == (restoredSettings
                ? QualificationSettings
                : RealtimeProductSettings.Default),
            "Lifecycle settings fixture did not match the selected scenario.");
        ValidateSettingsRuntime(restoredSettings
            ? QualificationSettings
            : RealtimeProductSettings.Default);
    }

    private async Task QualifyEmptyNewGame(RealtimeProductTitle title)
    {
        RequireMissingSave();
        RequireLifecycle(
            title.ContinueButton.Disabled && !title.NewGameButton.Disabled,
            "Empty title actions drifted.");
        PushLifecyclePointer(title.ContinueButton);
        await SettleLifecycleFrames(2);
        RequireLifecycle(
            _session is null && title.Visible,
            "Disabled Continue started a session.");
        PushLifecyclePointer(title.NewGameButton);
        await SettleLifecycleFrames(4);
        ValidateInitialBriefing(title);
    }

    private async Task QualifyProgressContinue(RealtimeProductTitle title)
    {
        string path = ResolveSavePath();
        byte[] original = ReadRequiredSave(path);
        RequireLifecycle(
            !title.ContinueButton.Disabled && !title.NewGameButton.Disabled,
            "Restorable title actions drifted.");
        PushLifecyclePointer(title.ContinueButton);
        await SettleLifecycleFrames(4);
        ValidateInitialBriefing(title);
        RequireLifecycle(
            File.ReadAllBytes(path).SequenceEqual(original),
            "Continue changed progress before normal exit.");
    }

    private async Task QualifyCompletedContinue(RealtimeProductTitle title)
    {
        string path = ResolveSavePath();
        byte[] original = ReadRequiredSave(path);
        RequireLifecycle(
            !title.ContinueButton.Disabled && !title.NewGameButton.Disabled,
            "Completed title actions drifted.");
        PushLifecyclePointer(title.ContinueButton);
        await SettleLifecycleFrames(4);
        RequireLifecycle(
            _session is not null &&
            !title.Visible &&
            Session.CoreSnapshot.CampaignComplete &&
            !Session.CoreSnapshot.ChapterStarted &&
            Session.InteractionState.Simulation == RealtimeSimulationState.Ended &&
            Session.InteractionState.PauseReason ==
                RealtimePauseReason.CampaignResult &&
            Session.LatestPresentation.Modal is null &&
            _ui!.ModalHost.Depth == 0 &&
            _progressPersistenceOwnership == ProgressPersistenceOwnership.Product &&
            File.ReadAllBytes(path).SequenceEqual(original),
            "Completed Continue did not open the exact read-only terminal world.");
    }

    private async Task QualifyCompletedNewGame(RealtimeProductTitle title)
    {
        string path = ResolveSavePath();
        byte[] original = ReadRequiredSave(path);
        RequireLifecycle(
            _productTitlePresentation?.NewGameAction ==
                RealtimeProductNewGameAction.Immediate,
            "Completed title did not expose immediate New Game.");
        PushLifecyclePointer(title.NewGameButton);
        await SettleLifecycleFrames(4);
        ValidateInitialBriefing(title);
        RequireLifecycle(
            File.ReadAllBytes(path).SequenceEqual(original),
            "Completed New Game changed the primary before normal exit.");
    }

    private async Task QualifyResetNewGame(RealtimeProductTitle title)
    {
        string path = ResolveSavePath();
        byte[] original = ReadRequiredSave(path);
        string[] beforeBackups = EnumerateLifecycleResetBackups(path);
        RequireLifecycle(
            _productTitlePresentation?.NewGameAction ==
                RealtimeProductNewGameAction.Reset,
            "Restorable title did not expose reset confirmation.");

        PushLifecyclePointer(title.NewGameButton);
        await SettleLifecycleFrames(2);
        RequireLifecycle(
            _session is null &&
            title.Visible &&
            _productTitlePresentation?.NewGameAction ==
                RealtimeProductNewGameAction.ConfirmReset &&
            File.ReadAllBytes(path).SequenceEqual(original) &&
            EnumerateLifecycleResetBackups(path).SequenceEqual(beforeBackups),
            "First reset activation was not confirmation-only.");

        PushLifecyclePointer(title.NewGameButton);
        await SettleLifecycleFrames(4);
        string[] created = EnumerateLifecycleResetBackups(path)
            .Except(beforeBackups, StringComparer.Ordinal)
            .ToArray();
        RequireLifecycle(
            created.Length == 1 &&
            File.ReadAllBytes(created[0]).SequenceEqual(original) &&
            File.ReadAllBytes(path).SequenceEqual(original),
            "Confirmed reset did not create one byte-exact sibling backup.");
        ValidateInitialBriefing(title);
    }

    private async Task QualifySettingsApply(RealtimeProductTitle title)
    {
        RequireMissingSave();
        string settingsPath = ResolveSettingsPath();
        RequireLifecycle(
            !File.Exists(settingsPath) && !Directory.Exists(settingsPath),
            "Settings-apply fixture was not missing.");
        RealtimeSettingsSurface surface = await OpenLifecycleSettings(title);

        await SelectLifecycleOption(surface.WindowModeOption, 1);
        await SelectLifecycleOption(surface.UiScaleOption, 200);
        await SelectLifecycleOption(surface.MasterVolumeOption, 0);
        await SelectLifecycleOption(surface.AmbientVolumeOption, 25);
        await SelectLifecycleOption(surface.SfxVolumeOption, 75);
        PushLifecyclePointer(surface.ReduceMotionCheck);
        await SettleLifecycleFrames(2);
        RequireLifecycle(
            surface.ReduceMotionCheck.ButtonPressed,
            "Reduce Motion pointer input did not change its candidate.");

        PushLifecyclePointer(surface.ApplyButton);
        await SettleLifecycleFrames(10);
        RequireLifecycle(
            _settings == QualificationSettings &&
            File.Exists(settingsPath) &&
            !File.Exists($"{settingsPath}.tmp") &&
            !Directory.Exists($"{settingsPath}.tmp"),
            "Settings pointer/key candidate was not atomically applied.");
        ValidateSettingsControls(surface, QualificationSettings);
        ValidateSettingsRuntime(QualificationSettings);
        PushLifecycleKey(Key.Escape);
        await SettleLifecycleFrames(4);
        RequireLifecycle(
            _ui is { SettingsVisible: false } && title.Visible,
            "Settings keyboard close did not restore the title.");
    }

    private async Task QualifySettingsRestore(RealtimeProductTitle title)
    {
        RequireMissingSave();
        string settingsPath = ResolveSettingsPath();
        byte[] original = File.ReadAllBytes(settingsPath);
        RealtimeSettingsSurface surface = await OpenLifecycleSettings(title);
        ValidateSettingsControls(surface, QualificationSettings);
        ValidateSettingsRuntime(QualificationSettings);
        PushLifecycleKey(Key.Escape);
        await SettleLifecycleFrames(4);
        RequireLifecycle(
            _ui is { SettingsVisible: false } &&
            title.Visible &&
            File.ReadAllBytes(settingsPath).SequenceEqual(original),
            "Fresh settings restore changed bytes or lost the title.");
    }

    private async Task<RealtimeSettingsSurface> OpenLifecycleSettings(
        RealtimeProductTitle title)
    {
        RequireLifecycle(
            title.SettingsButton.Visible && !title.SettingsButton.Disabled,
            "Title settings action is unavailable.");
        PushLifecyclePointer(title.SettingsButton);
        await SettleLifecycleFrames(4);
        RealtimeSettingsSurface surface = _ui!.SettingsSurface;
        RequireLifecycle(
            _ui.SettingsVisible &&
            surface.Visible &&
            _activeSettingsJourney == RealtimeSettingsJourney.ProductTitle,
            "Pointer input did not open the product settings journey.");
        return surface;
    }

    private async Task SelectLifecycleOption(OptionButton option, int targetId)
    {
        int targetIndex = -1;
        for (int index = 0; index < option.ItemCount; index++)
        {
            if (option.GetItemId(index) == targetId)
            {
                targetIndex = index;
                break;
            }
        }
        RequireLifecycle(targetIndex >= 0, $"Settings option {targetId} is missing.");
        PushLifecyclePointer(option);
        await SettleLifecycleFrames(2);
        PopupMenu popup = option.GetPopup();
        RequireLifecycle(popup.Visible, "Settings option popup did not open.");
        Vector2 itemPoint = new(
            popup.Position.X + (popup.Size.X / 2f),
            popup.Position.Y +
                (popup.Size.Y * ((targetIndex + 0.5f) / option.ItemCount)));
        PushLifecyclePointer(GetViewport(), itemPoint);
        await SettleLifecycleFrames(3);
        RequireLifecycle(
            !option.GetPopup().Visible &&
            option.GetItemId(option.Selected) == targetId,
            $"Settings input did not select option {targetId}: " +
            $"selected={option.GetItemId(option.Selected)}, " +
            $"popup_visible={popup.Visible}.");
    }

    private void ValidateInitialBriefing(RealtimeProductTitle title)
    {
        RealtimeChapterStoryModalRequest story =
            Session.ActiveChapterStoryModal ??
            throw new InvalidOperationException(
                "Initial product briefing is not active.");
        var snapshot = Session.CoreSnapshot;
        RequireLifecycle(
            !title.Visible &&
            _worldControl?.Visible == true &&
            _launch.Kind == RealtimeLaunchKind.NativeRelease &&
            ReferenceEquals(
                _launch.NativeRoute,
                RealtimeNativeRouteCatalog.ProductCampaign) &&
            _progressPersistenceOwnership == ProgressPersistenceOwnership.Product &&
            story is
            {
                ModalId: RealtimeR2Ids.ChapterBriefingModal,
                Purpose: RealtimeChapterStoryModalPurpose.ChapterBriefing,
                ChapterId: RealtimeCampaignOverlayLoader.FirstReleaseChapterId,
                EventId: null,
            } &&
            snapshot.ChapterStarted &&
            snapshot.Minute == snapshot.ChapterStartMinute &&
            snapshot.CommandCount == 0 &&
            !snapshot.CampaignComplete &&
            Session.InteractionState is
            {
                Simulation: RealtimeSimulationState.AutoPaused,
                RunningSpeed: RealtimeSimulationSpeed.Normal,
                ActiveModalId: var modalId,
            } &&
            modalId == story.ModalId &&
            Session.LatestPresentation.Modal?.Id == story.ModalId &&
            _ui!.ModalHost.ActiveModal?.Id == story.ModalId,
            "Input did not reach the exact initial product briefing.");
    }

    private void ValidateSettingsControls(
        RealtimeSettingsSurface surface,
        RealtimeProductSettings expected)
    {
        RequireLifecycle(
            SelectedLifecycleOption(surface.WindowModeOption) ==
                (expected.WindowMode == RealtimeProductWindowMode.Fullscreen ? 1 : 0) &&
            SelectedLifecycleOption(surface.UiScaleOption) ==
                expected.UiScalePercent &&
            SelectedLifecycleOption(surface.MasterVolumeOption) ==
                expected.MasterVolumePercent &&
            SelectedLifecycleOption(surface.AmbientVolumeOption) ==
                expected.AmbientVolumePercent &&
            SelectedLifecycleOption(surface.SfxVolumeOption) ==
                expected.SfxVolumePercent &&
            surface.ReduceMotionCheck.ButtonPressed == expected.ReduceMotion,
            "Settings controls do not show the expected committed values.");
    }

    private void ValidateSettingsRuntime(RealtimeProductSettings expected)
    {
        RequireLifecycle(
            _ui!.UiScalePercent == expected.UiScalePercent &&
            BusMatches("Master", expected.MasterVolumePercent) &&
            BusMatches("Ambient", expected.AmbientVolumePercent) &&
            BusMatches("SFX", expected.SfxVolumePercent),
            "Settings did not project into the UI/audio runtime.");
        if (!string.Equals(
                DisplayServer.GetName(),
                "headless",
                StringComparison.OrdinalIgnoreCase))
        {
            Window.ModeEnum expectedMode = expected.WindowMode ==
                RealtimeProductWindowMode.Fullscreen
                    ? Window.ModeEnum.Fullscreen
                    : Window.ModeEnum.Windowed;
            RequireLifecycle(
                GetWindow().Mode == expectedMode,
                "Settings did not project into the native window mode.");
        }
    }

    private static bool BusMatches(string busName, int percent)
    {
        int bus = AudioServer.GetBusIndex(busName);
        return bus >= 0 &&
            AudioServer.IsBusMute(bus) == (percent == 0) &&
            Mathf.IsEqualApprox(
                AudioServer.GetBusVolumeLinear(bus),
                percent / 100f);
    }

    private void RequireMissingSave()
    {
        string path = ResolveSavePath();
        RequireLifecycle(
            !File.Exists(path) && !Directory.Exists(path),
            "Lifecycle fixture unexpectedly contains a product save.");
    }

    private static byte[] ReadRequiredSave(string path)
    {
        if (!File.Exists(path) || Directory.Exists(path))
        {
            throw new InvalidOperationException(
                "Lifecycle fixture is missing its required product save.");
        }
        return File.ReadAllBytes(path);
    }

    private static string[] EnumerateLifecycleResetBackups(string savePath)
    {
        string directory = Path.GetDirectoryName(savePath) ??
            throw new InvalidOperationException(
                "Lifecycle reset save has no sibling directory.");
        string fileName = Path.GetFileName(savePath);
        string absolutePrefix = Path.Combine(directory, $"{fileName}.reset-");
        return Directory.GetFiles(directory, $"{fileName}.reset-*.bak")
            .Where(path =>
            {
                if (!path.StartsWith(absolutePrefix, StringComparison.Ordinal) ||
                    !path.EndsWith(".bak", StringComparison.Ordinal))
                {
                    return false;
                }
                string suffix = path[
                    absolutePrefix.Length..^".bak".Length];
                return Guid.TryParseExact(suffix, "N", out _);
            })
            .OrderBy(path => path, StringComparer.Ordinal)
            .ToArray();
    }

    private string ClassifyLifecycleSession()
    {
        if (_session is null)
        {
            return "NONE";
        }
        if (Session.ActiveChapterStoryModal is
            {
                ModalId: RealtimeR2Ids.ChapterBriefingModal,
                Purpose: RealtimeChapterStoryModalPurpose.ChapterBriefing,
                ChapterId: RealtimeCampaignOverlayLoader.FirstReleaseChapterId,
                EventId: null,
            } &&
            Session.InteractionState.Simulation == RealtimeSimulationState.AutoPaused)
        {
            return "INITIAL_BRIEFING";
        }
        if (Session.InteractionState.Simulation == RealtimeSimulationState.PlayerPaused)
        {
            return "PLAYER_PAUSED";
        }
        if (Session.InteractionState.Simulation == RealtimeSimulationState.Ended)
        {
            return "ENDED";
        }
        throw new InvalidOperationException(
            $"Lifecycle session state is unclassified: " +
            $"{Session.InteractionState.Simulation}.");
    }

    private static (string Title, string Session, string Settings, string Save)
        ExpectedLifecycleOutcome(LifecycleQualificationScenario scenario) =>
        scenario switch
        {
            LifecycleQualificationScenario.EmptyNewGame =>
                ("HIDDEN", "INITIAL_BRIEFING", "DEFAULT", "MISSING_TO_INITIAL"),
            LifecycleQualificationScenario.ProgressContinue =>
                ("HIDDEN", "INITIAL_BRIEFING", "DEFAULT", "PROGRESS_UNCHANGED"),
            LifecycleQualificationScenario.CompletedContinue =>
                ("HIDDEN", "ENDED", "DEFAULT", "COMPLETED_UNCHANGED"),
            LifecycleQualificationScenario.CompletedNewGame =>
                ("HIDDEN", "INITIAL_BRIEFING", "DEFAULT", "COMPLETED_TO_INITIAL"),
            LifecycleQualificationScenario.ResetNewGame =>
                ("HIDDEN", "INITIAL_BRIEFING", "DEFAULT",
                    "PROGRESS_TO_INITIAL_BACKUP"),
            LifecycleQualificationScenario.SettingsApply =>
                ("VISIBLE", "NONE", "APPLIED", "MISSING"),
            LifecycleQualificationScenario.SettingsRestore =>
                ("VISIBLE", "NONE", "RESTORED", "MISSING"),
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };

    private static string ScenarioName(LifecycleQualificationScenario scenario) =>
        scenario switch
        {
            LifecycleQualificationScenario.EmptyNewGame => "EMPTY_NEW_GAME",
            LifecycleQualificationScenario.ProgressContinue => "PROGRESS_CONTINUE",
            LifecycleQualificationScenario.CompletedContinue => "COMPLETED_CONTINUE",
            LifecycleQualificationScenario.CompletedNewGame => "COMPLETED_NEW_GAME",
            LifecycleQualificationScenario.ResetNewGame => "RESET_NEW_GAME",
            LifecycleQualificationScenario.SettingsApply => "SETTINGS_APPLY",
            LifecycleQualificationScenario.SettingsRestore => "SETTINGS_RESTORE",
            _ => throw new ArgumentOutOfRangeException(nameof(scenario)),
        };

    private void PushLifecyclePointer(Control target)
    {
        RequireLifecycle(
            target.IsInsideTree() && target.IsVisibleInTree(),
            $"Pointer target '{target.Name}' is not visible in the scene tree.");
        Vector2 point = target.GetGlobalRect().GetCenter();
        PushLifecyclePointer(GetViewport(), point);
    }

    private void PushLifecyclePointer(Viewport viewport, Vector2 point)
    {
        PushLifecycleInput(viewport, new InputEventMouseMotion
        {
            Position = point,
            GlobalPosition = point,
        }, pointer: true);
        PushLifecycleInput(viewport, new InputEventMouseButton
        {
            Position = point,
            GlobalPosition = point,
            ButtonIndex = MouseButton.Left,
            ButtonMask = MouseButtonMask.Left,
            Pressed = true,
        }, pointer: true);
        PushLifecycleInput(viewport, new InputEventMouseButton
        {
            Position = point,
            GlobalPosition = point,
            ButtonIndex = MouseButton.Left,
            ButtonMask = (MouseButtonMask)0,
            Pressed = false,
        }, pointer: true);
    }

    private void PushLifecycleKey(Key key)
    {
        PushLifecycleInput(GetViewport(), new InputEventKey
        {
            Keycode = key,
            PhysicalKeycode = key,
            Pressed = true,
        }, pointer: false);
        PushLifecycleInput(GetViewport(), new InputEventKey
        {
            Keycode = key,
            PhysicalKeycode = key,
            Pressed = false,
        }, pointer: false);
    }

    private void PushLifecycleInput(
        Viewport viewport,
        InputEvent inputEvent,
        bool pointer)
    {
        viewport.PushInput(inputEvent, inLocalCoords: true);
        if (pointer)
        {
            _lifecyclePointerInputCount++;
        }
        else
        {
            _lifecycleKeyInputCount++;
        }
    }

    private async Task SettleLifecycleFrames(int count)
    {
        for (int index = 0; index < count; index++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        }
    }

    private static int SelectedLifecycleOption(OptionButton option) =>
        option.GetItemId(option.Selected);

    private static void RequireLifecycle(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
