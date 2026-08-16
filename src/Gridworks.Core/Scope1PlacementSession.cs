using System.Collections.ObjectModel;

namespace Gridworks.Core;

public sealed class Scope1PlacementSession
{
    private readonly Scope1Scenario _scenario;
    private readonly List<Scope1Point> _supportPositions = [];
    private readonly long _maxSpanSquared;

    private int _minute;
    private Scope1Phase _phase = Scope1Phase.Drafting;
    private int? _completionMinute;

    public Scope1PlacementSession(Scope1Scenario scenario)
    {
        Scope1FixtureLoader.ValidateScenario(scenario);
        _scenario = scenario;
        _minute = scenario.InitialMinute;
        _maxSpanSquared = checked((long)scenario.MaxSpan * scenario.MaxSpan);
    }

    public Scope1View GetView() => new(
        _minute,
        _phase,
        new ReadOnlyCollection<Scope1Point>(_supportPositions.ToArray()),
        _completionMinute);

    public Scope1PreviewResult PreviewSpan(Scope1Point position)
    {
        Scope1Point from = LastEndpoint;
        long distanceSquared = PreviewDistanceSquared(from, position);

        if (_phase != Scope1Phase.Drafting)
        {
            return RejectedPreview(
                Scope1ErrorCode.WrongPhase, from, position, distanceSquared);
        }
        if (!IsValidSupportPosition(position))
        {
            return RejectedPreview(
                Scope1ErrorCode.InvalidPosition, from, position, distanceSquared);
        }
        if (distanceSquared > _maxSpanSquared)
        {
            return RejectedPreview(
                Scope1ErrorCode.SpanTooLong, from, position, distanceSquared);
        }

        return new Scope1PreviewResult(
            true, null, from, position, distanceSquared, _maxSpanSquared);
    }

    public Scope1PreviewResult PreviewTarget()
    {
        Scope1Point from = LastEndpoint;
        Scope1Point target = _scenario.Target;
        long distanceSquared = Scope1FixtureLoader.DistanceSquared(from, target);

        if (_phase != Scope1Phase.Drafting)
        {
            return RejectedPreview(
                Scope1ErrorCode.WrongPhase, from, target, distanceSquared);
        }
        if (distanceSquared > _maxSpanSquared)
        {
            return RejectedPreview(
                Scope1ErrorCode.SpanTooLong, from, target, distanceSquared);
        }

        return new Scope1PreviewResult(
            true, null, from, target, distanceSquared, _maxSpanSquared);
    }

    public Scope1CommandResult AddSupport(Scope1Point position)
    {
        Scope1PreviewResult preview = PreviewSpan(position);
        if (!preview.Accepted)
        {
            return Rejected(preview.ErrorCode!.Value);
        }

        _supportPositions.Add(position);
        return Accepted();
    }

    public Scope1CommandResult UndoSupport()
    {
        if (_phase != Scope1Phase.Drafting)
        {
            return Rejected(Scope1ErrorCode.WrongPhase);
        }
        if (_supportPositions.Count == 0)
        {
            return Rejected(Scope1ErrorCode.NothingToUndo);
        }

        _supportPositions.RemoveAt(_supportPositions.Count - 1);
        return Accepted();
    }

    public Scope1CommandResult OrderLine()
    {
        Scope1PreviewResult preview = PreviewTarget();
        if (!preview.Accepted)
        {
            return Rejected(preview.ErrorCode!.Value);
        }

        _phase = Scope1Phase.Building;
        _completionMinute = checked(_minute + _scenario.BuildMinutes);
        return Accepted();
    }

    public Scope1CommandResult AdvanceToCompletion()
    {
        if (_phase != Scope1Phase.Building)
        {
            return Rejected(Scope1ErrorCode.WrongPhase);
        }

        _minute = _completionMinute
            ?? throw new InvalidOperationException("Building phase requires a completion minute.");
        _phase = Scope1Phase.Commissioned;
        return Accepted();
    }

    private Scope1Point LastEndpoint =>
        _supportPositions.Count == 0 ? _scenario.Source : _supportPositions[^1];

    private bool IsValidSupportPosition(Scope1Point position) =>
        _scenario.MapBounds.Contains(position) &&
        position != _scenario.Source &&
        position != _scenario.Target &&
        !_supportPositions.Contains(position);

    private static long PreviewDistanceSquared(Scope1Point from, Scope1Point to)
    {
        try
        {
            return Scope1FixtureLoader.DistanceSquared(from, to);
        }
        catch (OverflowException)
        {
            return long.MaxValue;
        }
    }

    private Scope1PreviewResult RejectedPreview(
        Scope1ErrorCode errorCode,
        Scope1Point from,
        Scope1Point to,
        long distanceSquared) =>
        new(false, errorCode, from, to, distanceSquared, _maxSpanSquared);

    private Scope1CommandResult Accepted() => new(true, null, GetView());

    private Scope1CommandResult Rejected(Scope1ErrorCode errorCode) =>
        new(false, errorCode, GetView());
}
