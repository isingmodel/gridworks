using Gridworks.Core.Release.V2;

namespace Gridworks.Core.Release.V3;

public sealed record RealtimeDispatchLoadPlan(
    string LoadId,
    CommercialObligationKind Obligation,
    int ObligationPriority,
    int AuthoredDispatchPriority,
    ThermalLoadRequest Request);

public static class RealtimeDispatchPlanner
{
    public static IReadOnlyList<RealtimeDispatchLoadPlan> BuildLoadPlan(
        CommercialOperatingPhaseDefinition phase,
        CommercialPromiseDecision promiseDecision)
    {
        ArgumentNullException.ThrowIfNull(phase);
        RealtimeDispatchLoadPlan[] result = phase.Loads
            .Select((load, authoredDispatchPriority) => new RealtimeDispatchLoadPlan(
                load.LoadId,
                load.Obligation,
                ObligationPriority(load.Obligation),
                authoredDispatchPriority,
                new ThermalLoadRequest(
                    load.LoadId,
                    load.DemandKw,
                    Permission(load, phase))))
            .Where(item => item.Obligation != CommercialObligationKind.CityPromise ||
                promiseDecision == CommercialPromiseDecision.Keep)
            .OrderBy(item => item.ObligationPriority)
            .ThenBy(item => item.AuthoredDispatchPriority)
            .ThenBy(item => item.LoadId, StringComparer.Ordinal)
            .ToArray();
        return Array.AsReadOnly(result);
    }

    private static int ObligationPriority(CommercialObligationKind obligation) =>
        obligation switch
        {
            CommercialObligationKind.SafetyDuty => 0,
            CommercialObligationKind.CityPromise => 1,
            CommercialObligationKind.OperatingRecord => 2,
            _ => throw new ArgumentOutOfRangeException(nameof(obligation)),
        };

    private static ThermalPermission Permission(
        CommercialLoadBundleDefinition load,
        CommercialOperatingPhaseDefinition phase) => load.Obligation switch
        {
            CommercialObligationKind.OperatingRecord => ThermalPermission.ContinuousOnly,
            CommercialObligationKind.CityPromise => ThermalPermission.EmergencyAllowed,
            CommercialObligationKind.SafetyDuty
                when phase.ThermalPolicy ==
                    CommercialPhaseThermalPolicy.SafetyEmergencyAllowed =>
                ThermalPermission.EmergencyAllowed,
            _ => ThermalPermission.ContinuousOnly,
        };
}
