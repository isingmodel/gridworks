using Gridworks.Core.Release.V2;

namespace Gridworks.Core.Release.V3;

/// <summary>
/// The single realtime authority for authored connection-count requirements.
/// Counts match the V2 approval gate: every commissioned incident edge adds one
/// connection at each endpoint, independent of route selection or event risk.
/// </summary>
public static class RealtimeConnectionRequirementEvaluator
{
    public static RealtimeConnectionRequirementAssessment? Evaluate(
        IReadOnlyList<CommercialCampaignConnectionRequirement> requirements,
        SpatialWorldDefinition world,
        long evaluatedMinute,
        bool frozenForChapter = false)
    {
        ArgumentNullException.ThrowIfNull(requirements);
        ArgumentNullException.ThrowIfNull(world);
        if (evaluatedMinute < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(evaluatedMinute));
        }
        if (requirements.Count == 0)
        {
            return null;
        }

        Dictionary<string, int> incidentCounts = world.Nodes.ToDictionary(
            item => item.NodeId,
            _ => 0,
            StringComparer.Ordinal);
        foreach (SpatialEdgeDefinition edge in world.Edges.Where(item => item.Commissioned))
        {
            if (!incidentCounts.TryGetValue(edge.FromNodeId, out int fromCount) ||
                !incidentCounts.TryGetValue(edge.ToNodeId, out int toCount))
            {
                throw new InvalidOperationException(
                    $"Commissioned edge '{edge.EdgeId}' references an unknown endpoint.");
            }
            incidentCounts[edge.FromNodeId] = checked(fromCount + 1);
            incidentCounts[edge.ToNodeId] = checked(toCount + 1);
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var facts = new RealtimeConnectionRequirementFact[requirements.Count];
        for (int index = 0; index < requirements.Count; index++)
        {
            CommercialCampaignConnectionRequirement requirement = requirements[index];
            if (string.IsNullOrWhiteSpace(requirement.NodeId) ||
                !string.Equals(requirement.NodeId, requirement.NodeId.Trim(),
                    StringComparison.Ordinal) ||
                requirement.MinimumConnections <= 0 ||
                !seen.Add(requirement.NodeId) ||
                !incidentCounts.TryGetValue(requirement.NodeId, out int current))
            {
                throw new InvalidOperationException(
                    $"Invalid realtime connection requirement at index {index}.");
            }
            facts[index] = new RealtimeConnectionRequirementFact(
                requirement.NodeId,
                current,
                requirement.MinimumConnections);
        }

        return new RealtimeConnectionRequirementAssessment(
            evaluatedMinute,
            frozenForChapter,
            facts);
    }
}
