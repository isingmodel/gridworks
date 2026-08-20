namespace Gridworks.Core.Release.V2;

public sealed record ThermalLimitDefinition(
    long ContinuousLimitKw,
    long EmergencyLimitKw);

public sealed record ThermalNodeClassDefinition(
    string ClassId,
    long ContinuousLimitKw,
    long EmergencyLimitKw);

public sealed record ThermalLineClassDefinition(
    string ClassId,
    long ContinuousLimitKw,
    long EmergencyLimitKw);

public sealed record GenerationSourceDefinition(
    string NodeId,
    long OutputCapacityKw,
    int AuthoredOrder);

public sealed record CommercialWorldDefinition(
    string SchemaVersion,
    string WorldId,
    string DisplayName,
    SpatialWorldDefinition Spatial,
    IReadOnlyList<ThermalNodeClassDefinition> ThermalNodeClasses,
    IReadOnlyList<ThermalLineClassDefinition> ThermalLineClasses,
    IReadOnlyList<GenerationSourceDefinition> GenerationSources)
{
    private IReadOnlyList<ThermalNodeClassDefinition> _thermalNodeClasses =
        Array.AsReadOnly(ThermalNodeClasses.ToArray());
    private IReadOnlyList<ThermalLineClassDefinition> _thermalLineClasses =
        Array.AsReadOnly(ThermalLineClasses.ToArray());
    private IReadOnlyList<GenerationSourceDefinition> _generationSources =
        Array.AsReadOnly(GenerationSources.ToArray());

    public IReadOnlyList<ThermalNodeClassDefinition> ThermalNodeClasses
    {
        get => _thermalNodeClasses;
        init => _thermalNodeClasses = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<ThermalLineClassDefinition> ThermalLineClasses
    {
        get => _thermalLineClasses;
        init => _thermalLineClasses = Array.AsReadOnly(value.ToArray());
    }

    public IReadOnlyList<GenerationSourceDefinition> GenerationSources
    {
        get => _generationSources;
        init => _generationSources = Array.AsReadOnly(value.ToArray());
    }
}

public sealed class CommercialWorldValidationException : Exception
{
    public CommercialWorldValidationException(string message)
        : base(message)
    {
    }

    public CommercialWorldValidationException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
