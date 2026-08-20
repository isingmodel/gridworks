using System;
using System.Collections.Generic;
using Godot;

namespace Gridworks.Game.Realtime.UI;

internal sealed partial class RealtimeInputRouter : Node
{
    private static readonly StringName PauseAction = "realtime_pause";
    private static readonly StringName NormalSpeedAction = "realtime_speed_1";
    private static readonly StringName FastSpeedAction = "realtime_speed_2";
    private static readonly StringName VeryFastSpeedAction = "realtime_speed_4";
    private static readonly StringName AnalysisAction = "realtime_analysis";
    private static readonly StringName BuildShelfAction = "realtime_build_shelf";

    private readonly List<ContextEntry> _contexts = [];
    private long _nextToken;
    private bool _panCaptured;

    public event Action<RealtimeInputRequest>? InputRequested;

    public RealtimeInputPriority ActivePriority => _contexts.Count == 0
        ? RealtimeInputPriority.EmptyTerrain
        : _contexts[^1].Priority;

    public string ActiveOwner => _contexts.Count == 0 ? "world" : _contexts[^1].Owner;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
    }

    public long PushContext(string owner, RealtimeInputPriority priority)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(owner);
        long token = checked(++_nextToken);
        _contexts.Add(new ContextEntry(token, owner, priority));
        _contexts.Sort(static (left, right) =>
        {
            int priority = left.Priority.CompareTo(right.Priority);
            return priority != 0 ? priority : left.Token.CompareTo(right.Token);
        });
        return token;
    }

    public bool PopContext(long token)
    {
        int index = _contexts.FindIndex(item => item.Token == token);
        if (index < 0)
        {
            return false;
        }
        _contexts.RemoveAt(index);
        return true;
    }

    public bool CanReceive(RealtimeInputPriority priority) => priority >= ActivePriority;

    public bool PanCaptured => _panCaptured;

    public bool CancelPanCapture()
    {
        if (!_panCaptured)
        {
            return false;
        }
        _panCaptured = false;
        InputRequested?.Invoke(Request(
            RealtimeInputCommand.EndPan,
            RealtimeInputPriority.PanCapture));
        return true;
    }

    public override void _Input(InputEvent inputEvent)
    {
        if (inputEvent is not InputEventKey key ||
            !IsPhysicalKey(key, Key.Space))
        {
            return;
        }

        // Physical Space is reserved for press-and-hold map panning. Handle it
        // before Control/BaseButton can translate it into ui_accept. Once a
        // pan owns the press it also owns echoes, duplicate presses, and the
        // eventual release even if keyboard focus changes in the meantime.
        if (_panCaptured)
        {
            if (!key.Pressed)
            {
                _panCaptured = false;
                InputRequested?.Invoke(Request(
                    RealtimeInputCommand.EndPan,
                    RealtimeInputPriority.PanCapture));
            }
            GetViewport().SetInputAsHandled();
            return;
        }

        if (key.Echo || !key.Pressed || IsTextEntryFocused() ||
            !CanReceive(RealtimeInputPriority.PanCapture))
        {
            return;
        }

        _panCaptured = true;
        InputRequested?.Invoke(Request(
            RealtimeInputCommand.BeginPan,
            RealtimeInputPriority.PanCapture));
        GetViewport().SetInputAsHandled();
    }

    public override void _UnhandledInput(InputEvent inputEvent)
    {
        bool textEntryFocused = IsTextEntryFocused();
        RealtimeInputRequest? request = null;
        if (!textEntryFocused && inputEvent.IsActionPressed(PauseAction))
        {
            request = Request(RealtimeInputCommand.TogglePause, RealtimeInputPriority.Hud);
        }
        else if (!textEntryFocused && inputEvent.IsActionPressed(NormalSpeedAction))
        {
            request = Request(RealtimeInputCommand.SetNormalSpeed, RealtimeInputPriority.Hud);
        }
        else if (!textEntryFocused && inputEvent.IsActionPressed(FastSpeedAction))
        {
            request = Request(RealtimeInputCommand.SetFastSpeed, RealtimeInputPriority.Hud);
        }
        else if (!textEntryFocused && inputEvent.IsActionPressed(VeryFastSpeedAction))
        {
            request = Request(RealtimeInputCommand.SetVeryFastSpeed, RealtimeInputPriority.Hud);
        }
        else if (!textEntryFocused && inputEvent.IsActionPressed(AnalysisAction))
        {
            request = Request(RealtimeInputCommand.ToggleAnalysis, RealtimeInputPriority.Hud);
        }
        else if (!textEntryFocused && inputEvent.IsActionPressed(BuildShelfAction))
        {
            request = Request(RealtimeInputCommand.ToggleBuildShelf, RealtimeInputPriority.Hud);
        }
        else if (inputEvent.IsActionPressed("ui_cancel"))
        {
            request = Request(RealtimeInputCommand.CancelOrBack, ActivePriority);
        }
        else if (!textEntryFocused && inputEvent.IsActionPressed("ui_accept"))
        {
            request = Request(RealtimeInputCommand.ConfirmOrSelect, ActivePriority);
        }
        else if (inputEvent is InputEventKey key && !key.Echo)
        {
            request = KeyRequest(key, textEntryFocused);
        }

        if (request.HasValue &&
            (request.Value.Command == RealtimeInputCommand.EndPan ||
             CanReceive(request.Value.SourcePriority)))
        {
            InputRequested?.Invoke(request.Value);
            GetViewport().SetInputAsHandled();
        }
    }

    private RealtimeInputRequest? KeyRequest(InputEventKey key, bool textEntryFocused)
    {
        Key physical = PhysicalKey(key);
        if (textEntryFocused || !key.Pressed)
        {
            return null;
        }
        if (key.Pressed)
        {
            return physical switch
            {
                Key.Backspace => Request(
                    RealtimeInputCommand.UndoDraftStep,
                    RealtimeInputPriority.DraftHandle),
                Key.Q => Request(
                    RealtimeInputCommand.CycleCandidatePrevious,
                    RealtimeInputPriority.WorldCandidate),
                Key.E => Request(
                    RealtimeInputCommand.CycleCandidateNext,
                    RealtimeInputPriority.WorldCandidate),
                Key.Home => Request(
                    RealtimeInputCommand.TimelineHome,
                    RealtimeInputPriority.Hud),
                Key.Bracketleft => Request(
                    RealtimeInputCommand.TimelinePrevious,
                    RealtimeInputPriority.Hud),
                Key.Bracketright => Request(
                    RealtimeInputCommand.TimelineNext,
                    RealtimeInputPriority.Hud),
                Key.I => Request(
                    RealtimeInputCommand.SelectInspectTool,
                    RealtimeInputPriority.Hud),
                Key.N => Request(
                    RealtimeInputCommand.SelectFirstNodeTool,
                    RealtimeInputPriority.Hud),
                Key.L => Request(
                    RealtimeInputCommand.SelectFirstLineTool,
                    RealtimeInputPriority.Hud),
                _ => null,
            };
        }
        return null;
    }

    private static bool IsPhysicalKey(InputEventKey key, Key expected) =>
        PhysicalKey(key) == expected;

    private static Key PhysicalKey(InputEventKey key) =>
        key.PhysicalKeycode != Key.None ? key.PhysicalKeycode : key.Keycode;

    private static RealtimeInputRequest Request(
        RealtimeInputCommand command,
        RealtimeInputPriority priority) => new(command, priority);

    private bool IsTextEntryFocused() => GetViewport().GuiGetFocusOwner() is
        LineEdit or TextEdit;

    private sealed record ContextEntry(
        long Token,
        string Owner,
        RealtimeInputPriority Priority);
}
