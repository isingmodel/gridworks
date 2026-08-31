namespace Gridworks.Core;

public sealed class Scope1PlacementSession
{
    private readonly Scope1Fixture _fixture;
    private readonly List<Scope1Point> _supportPositions = [];
    private readonly long _maxSpanSquared;

    private int _minute;
    private Scope1Phase _phase = Scope1Phase.Drafting;
    private int? _completionMinute;

    public Scope1PlacementSession(Scope1Fixture fixture)
    {
        ArgumentNullException.ThrowIfNull(fixture);
        Scope1FixtureLoader.Validate(fixture);

        _fixture = fixture;
        _minute = fixture.InitialMinute;
        _maxSpanSquared = checked((long)fixture.MaxSpan * fixture.MaxSpan);
    }

    public Scope1View GetView() => new(
        _minute,
        _phase,
        Array.AsReadOnly(_supportPositions.ToArray()),
        _completionMinute);

    public Scope1PreviewResult PreviewSpan(Scope1Point position)
    {
        ArgumentNullException.ThrowIfNull(position);

        Scope1Point from = LastEndpoint();
        long distanceSquared = SafeDistanceSquared(from, position);
        Scope1ErrorCode? errorCode = _phase != Scope1Phase.Drafting
            ? Scope1ErrorCode.WrongPhase
            : !IsAvailable(position)
                ? Scope1ErrorCode.InvalidPosition
                : distanceSquared > _maxSpanSquared
                    ? Scope1ErrorCode.SpanTooLong
                    : null;

        return new Scope1PreviewResult(
            errorCode is null,
            errorCode,
            from,
            position,
            distanceSquared,
            _maxSpanSquared);
    }

    public Scope1PreviewResult PreviewTarget()
    {
        Scope1Point from = LastEndpoint();
        long distanceSquared = SafeDistanceSquared(from, _fixture.Target);
        Scope1ErrorCode? errorCode = _phase != Scope1Phase.Drafting
            ? Scope1ErrorCode.WrongPhase
            : distanceSquared > _maxSpanSquared
                ? Scope1ErrorCode.SpanTooLong
                : null;

        return new Scope1PreviewResult(
            errorCode is null,
            errorCode,
            from,
            _fixture.Target,
            distanceSquared,
            _maxSpanSquared);
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
        _completionMinute = checked(_minute + _fixture.BuildMinutes);
        return Accepted();
    }

    public Scope1CommandResult AdvanceToCompletion()
    {
        if (_phase != Scope1Phase.Building)
        {
            return Rejected(Scope1ErrorCode.WrongPhase);
        }

        _minute = _completionMinute
            ?? throw new FixtureValidationException(
                "Scope 1 building state has no completion minute.");
        _phase = Scope1Phase.Commissioned;
        return Accepted();
    }

    private bool IsAvailable(Scope1Point position) =>
        Scope1FixtureLoader.Contains(_fixture.MapBounds, position) &&
        position != _fixture.Source &&
        position != _fixture.Target &&
        !_supportPositions.Contains(position);

    private Scope1Point LastEndpoint() => _supportPositions.Count == 0
        ? _fixture.Source
        : _supportPositions[^1];

    private static long SafeDistanceSquared(Scope1Point from, Scope1Point to)
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

    private Scope1CommandResult Accepted() => new(true, null, GetView());

    private Scope1CommandResult Rejected(Scope1ErrorCode errorCode) =>
        new(false, errorCode, GetView());
}
